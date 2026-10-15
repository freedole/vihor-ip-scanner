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
        public int Threads = 256;
        public bool ScanPorts = true;
        public int PortTimeoutMs = 700;
        public int NameTimeoutMs = 1500;
        public int ProbeTimeoutMs = 2500;

        /// <summary>false = only HTTP, HTTPS, RDP, SMB (gentle default); true = all known ports.</summary>
        public bool ExtendedPorts = false;

        /// <summary>Max. simultaneous TCP connections for the whole scan (keeps endpoint IPS/AV calm).</summary>
        public int TcpParallel = 16;

        /// <summary>Pause between two port checks on the same host.</summary>
        public int PortGapMs = 120;
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
        private readonly SemaphoreSlim tcpGate;

        /// <summary>Ports checked by default: the management interfaces admins actually open.</summary>
        private static readonly Service BasicServices = Service.Http | Service.Https | Service.Rdp | Service.Smb;

        public Scanner(ScanOptions options)
        {
            opt = options;
            tcpGate = new SemaphoreSlim(Math.Max(1, opt.TcpParallel));
            subnets = IpRange.GetLocalSubnets();
            gateways = GetGateways();
        }

        public async Task RunAsync(IList<uint> targets, CancellationToken ct)
        {
            await OuiDb.LoadAsync().ConfigureAwait(false);
            Diag.Reset($"scan start: {targets.Count} addresses, threads={opt.Threads}, ports={(opt.ScanPorts ? (opt.ExtendedPorts ? "extended" : "basic") : "off")}, tcpParallel={opt.TcpParallel}");

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
                    catch (Exception ex) { Diag.Host(ip, "exception: " + ex.Message); }
                    finally
                    {
                        gate.Release();
                        Progress?.Invoke(Interlocked.Increment(ref done), total);
                    }
                }, CancellationToken.None));
            }

            try { await Task.WhenAll(tasks).ConfigureAwait(false); }
            catch { /* individual tasks swallow their own errors */ }
            Diag.Log($"scan end: {done}/{total} processed{(ct.IsCancellationRequested ? " (cancelled)" : "")}");
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

            // Sleepy devices (ESP, phones) often miss the first ARP request: after a successful ping
            // the OS cache usually has the entry, so ask again and fall back to the ARP table.
            if (host.Mac == null && local && self == null)
            {
                host.Mac = await Task.Run(() => Native.ResolveMac(addr)).ConfigureAwait(false);
                if (host.Mac == null && Native.ReadArpCache().TryGetValue(ip, out var cached)) host.Mac = cached;
            }

            Diag.Host(ip, $"alive  ping={(rtt >= 0 ? rtt + " ms" : "no reply")}  mac={(host.Mac == null ? "-" : host.MacText)}");

            // 1) Names first (DNS PTR + NetBIOS) — quiet, and before anything that could look like a port scan
            var dnsTask = ReverseDnsAsync(addr);
            var nbTask = NetBiosWithRetryAsync(addr);
            await Task.WhenAll(dnsTask, nbTask).ConfigureAwait(false);

            var nb = nbTask.Result;
            host.Name = !string.IsNullOrEmpty(dnsTask.Result) ? dnsTask.Result : nb?.ComputerName ?? "";
            host.Workgroup = nb?.Workgroup ?? "";
            if (host.Mac == null && nb?.Mac != null) host.Mac = nb.Mac;
            if (host.IsSelf && string.IsNullOrEmpty(host.Name)) host.Name = Environment.MachineName;
            host.Vendor = OuiDb.Lookup(host.Mac);
            Diag.Host(ip, $"names  dns={(string.IsNullOrEmpty(dnsTask.Result) ? "-" : dnsTask.Result)}  netbios={nb?.ComputerName ?? "-"}");

            // 2) Services: one port at a time per host, globally limited, with a short pause in between
            if (opt.ScanPorts && !ct.IsCancellationRequested)
            {
                foreach (var p in HostInfo.Ports)
                {
                    if (ct.IsCancellationRequested) break;
                    if (!opt.ExtendedPorts && (BasicServices & p.Svc) == 0) continue;
                    var state = await CheckPortAsync(addr, p.Port).ConfigureAwait(false);
                    Diag.Host(ip, $"tcp/{p.Port,-5} {state}");
                    if (state == "open") host.Services |= p.Svc;
                    if (opt.PortGapMs > 0) await Task.Delay(opt.PortGapMs).ConfigureAwait(false);
                }
            }

            // 3) Describe open services (page title, server, banner) — also through the TCP gate
            if (host.Services != Service.None && !ct.IsCancellationRequested)
            {
                var details = new List<ServiceDetail>();
                foreach (var p in HostInfo.Ports.Where(p => host.Has(p.Svc)))
                {
                    await tcpGate.WaitAsync().ConfigureAwait(false);
                    try { details.Add(await ServiceProbe.DescribeAsync(addr, p, opt.ProbeTimeoutMs).ConfigureAwait(false)); }
                    finally { tcpGate.Release(); }
                }
                host.Details = details;
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

        private async Task<NetBiosInfo> NetBiosWithRetryAsync(IPAddress addr)
        {
            var r = await NetBios.QueryAsync(addr, opt.NameTimeoutMs / 2).ConfigureAwait(false);
            if (r == null) r = await NetBios.QueryAsync(addr, opt.NameTimeoutMs / 2).ConfigureAwait(false);
            return r;
        }

        /// <summary>TCP connect check. Returns "open", "closed" (RST), "timeout" or "error: …".</summary>
        private async Task<string> CheckPortAsync(IPAddress addr, int port)
        {
            await tcpGate.WaitAsync().ConfigureAwait(false);
            var c = new TcpClient(AddressFamily.InterNetwork);
            try
            {
                var t = c.ConnectAsync(addr, port);
                var done = await Task.WhenAny(t, Task.Delay(opt.PortTimeoutMs)).ConfigureAwait(false);
                if (done != t) { _ = t.ContinueWith(x => _ = x.Exception, TaskContinuationOptions.OnlyOnFaulted); return "timeout"; }
                await t.ConfigureAwait(false);
                return c.Connected ? "open" : "closed";
            }
            catch (SocketException se)
            {
                return se.SocketErrorCode == SocketError.ConnectionRefused ? "closed" : "error: " + se.SocketErrorCode;
            }
            catch (Exception ex)
            {
                var se2 = ex.InnerException as SocketException;
                if (se2 != null) return se2.SocketErrorCode == SocketError.ConnectionRefused ? "closed" : "error: " + se2.SocketErrorCode;
                return "error: " + ex.GetType().Name;
            }
            finally
            {
                c.Close();
                tcpGate.Release();
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
