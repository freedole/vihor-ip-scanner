using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Vihor.Scanning
{
    internal sealed class ScanOptions
    {
        public int PingTimeoutMs = 700;
        public int Threads = 128;
        public bool ScanPorts = true;
        public int PortTimeoutMs = 400;
        public int NameTimeoutMs = 1500;
        public int ProbeTimeoutMs = 2500;
    }

    /// <summary>
    /// Parallel IPv4 scanner: ICMP echo + ARP (local segment) for discovery,
    /// then reverse DNS / NetBIOS for names, OUI for vendor and TCP connect for common services.
    /// No raw sockets, no admin rights required.
    /// </summary>
    internal sealed class Scanner
    {
        public event Action<HostInfo> HostFound;
        public event Action<int, int> Progress;

        private readonly ScanOptions opt;
        private readonly List<LocalSubnet> subnets;
        private readonly HashSet<uint> gateways;

        public Scanner(ScanOptions options)
        {
            opt = options;
            subnets = IpRange.GetLocalSubnets();
            gateways = GetGateways();
        }

        public async Task RunAsync(IList<uint> targets, CancellationToken ct)
        {
            await OuiDb.LoadAsync().ConfigureAwait(false);

            int done = 0, total = targets.Count;
            using var gate = new SemaphoreSlim(opt.Threads);
            var tasks = new List<Task>(total);

            foreach (var ip in targets)
            {
                if (ct.IsCancellationRequested) break;
                try { await gate.WaitAsync(ct).ConfigureAwait(false); }
                catch (OperationCanceledException) { break; }

                tasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        var host = await ScanHostAsync(ip, ct).ConfigureAwait(false);
                        if (host != null && !ct.IsCancellationRequested) HostFound?.Invoke(host);
                    }
                    catch { /* one bad host must not stop the scan */ }
                    finally
                    {
                        gate.Release();
                        Progress?.Invoke(Interlocked.Increment(ref done), total);
                    }
                }, CancellationToken.None));
            }

            try { await Task.WhenAll(tasks).ConfigureAwait(false); }
            catch { /* individual tasks swallow their own errors */ }
        }

        /// <summary>Scans one host. Returns null if the host did not answer.</summary>
        public async Task<HostInfo> ScanHostAsync(uint ip, CancellationToken ct)
        {
            var addr = IpRange.ToIp(ip);
            var self = subnets.FirstOrDefault(s => s.Address == ip);
            bool local = subnets.Any(s => s.Contains(ip));

            var pingTask = PingAsync(addr);
            var arpTask = local && self == null ? Task.Run(() => Native.ResolveMac(addr), ct) : Task.FromResult<byte[]>(null);
            await Task.WhenAll(pingTask, arpTask).ConfigureAwait(false);

            long rtt = pingTask.Result;
            byte[] mac = self != null ? self.Mac : arpTask.Result;
            bool alive = rtt >= 0 || mac != null || self != null;
            if (!alive || ct.IsCancellationRequested) return null;

            var host = new HostInfo
            {
                Ip = ip,
                Alive = true,
                PingMs = rtt,
                Mac = mac,
                IsSelf = self != null,
                IsGateway = gateways.Contains(ip),
            };

            var dnsTask = ReverseDnsAsync(addr);
            var nbTask = NetBios.QueryAsync(addr, opt.NameTimeoutMs);
            var portTask = opt.ScanPorts ? ScanPortsAsync(addr, ct) : Task.FromResult(Service.None);
            await Task.WhenAll(dnsTask, nbTask, portTask).ConfigureAwait(false);

            var nb = nbTask.Result;
            host.Name = !string.IsNullOrEmpty(dnsTask.Result) ? dnsTask.Result : nb?.ComputerName ?? "";
            host.Workgroup = nb?.Workgroup ?? "";
            if (host.Mac == null && nb?.Mac != null) host.Mac = nb.Mac;
            if (host.IsSelf && string.IsNullOrEmpty(host.Name)) host.Name = Environment.MachineName;
            host.Vendor = OuiDb.Lookup(host.Mac);
            host.Services = portTask.Result;

            // Describe each open service (page title, server, banner) — in parallel, bounded by a timeout
            if (host.Services != Service.None && !ct.IsCancellationRequested)
            {
                var probes = HostInfo.Ports.Where(p => host.Has(p.Svc))
                                           .Select(p => ServiceProbe.DescribeAsync(addr, p, opt.ProbeTimeoutMs));
                host.Details = (await Task.WhenAll(probes).ConfigureAwait(false)).ToList();
            }
            return host;
        }

        private async Task<long> PingAsync(IPAddress addr)
        {
            try
            {
                using var p = new Ping();
                var r = await p.SendPingAsync(addr, opt.PingTimeoutMs).ConfigureAwait(false);
                if (r.Status == IPStatus.Success) return r.RoundtripTime;
                // Some stacks need a second probe after ARP resolution
                r = await p.SendPingAsync(addr, opt.PingTimeoutMs).ConfigureAwait(false);
                return r.Status == IPStatus.Success ? r.RoundtripTime : -1;
            }
            catch
            {
                return -1;
            }
        }

        private async Task<string> ReverseDnsAsync(IPAddress addr)
        {
            try
            {
                var t = Dns.GetHostEntryAsync(addr);
                var done = await Task.WhenAny(t, Task.Delay(opt.NameTimeoutMs)).ConfigureAwait(false);
                if (done != t) { _ = t.ContinueWith(x => _ = x.Exception, TaskContinuationOptions.OnlyOnFaulted); return ""; }
                var name = (await t.ConfigureAwait(false)).HostName ?? "";
                return name == addr.ToString() ? "" : name;
            }
            catch
            {
                return "";
            }
        }

        private async Task<Service> ScanPortsAsync(IPAddress addr, CancellationToken ct)
        {
            var checks = HostInfo.Ports.Select(async p => await IsOpenAsync(addr, p.Port).ConfigureAwait(false) ? p.Svc : Service.None);
            var results = await Task.WhenAll(checks).ConfigureAwait(false);
            return results.Aggregate(Service.None, (a, b) => a | b);
        }

        private async Task<bool> IsOpenAsync(IPAddress addr, int port)
        {
            var c = new TcpClient(AddressFamily.InterNetwork);
            try
            {
                var t = c.ConnectAsync(addr, port);
                var done = await Task.WhenAny(t, Task.Delay(opt.PortTimeoutMs)).ConfigureAwait(false);
                if (done != t) { _ = t.ContinueWith(x => _ = x.Exception, TaskContinuationOptions.OnlyOnFaulted); return false; }
                await t.ConfigureAwait(false);
                return c.Connected;
            }
            catch
            {
                return false;
            }
            finally
            {
                c.Close();
            }
        }

        private static HashSet<uint> GetGateways()
        {
            var set = new HashSet<uint>();
            try
            {
                foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
                    foreach (var g in ni.GetIPProperties().GatewayAddresses)
                        if (g.Address.AddressFamily == AddressFamily.InterNetwork && !g.Address.Equals(IPAddress.Any))
                            set.Add(IpRange.ToUInt(g.Address));
            }
            catch { }
            return set;
        }
    }
}
