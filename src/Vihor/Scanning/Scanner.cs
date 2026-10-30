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
    /// <summary>What the user sets in Options (or for one device in Advanced rescan).</summary>
    internal sealed class ScanProfile
    {
        public int Speed = 3;
        public List<int> ServicePorts = new List<int>(Settings.DefaultServicePorts);
        public List<int> CustomPorts = new List<int>();
        public string PortRanges = "";
        public bool RangesOnlyRescan = true;

        public static ScanProfile FromSettings() => new ScanProfile
        {
            Speed = Settings.Speed,
            ServicePorts = new List<int>(Settings.ServicePorts),
            CustomPorts = new List<int>(Settings.CustomPorts),
            PortRanges = Settings.PortRanges,
            RangesOnlyRescan = Settings.RangesOnlyRescan,
        };

        public void SaveToSettings()
        {
            Settings.Speed = Speed;
            Settings.ServicePorts = new List<int>(ServicePorts);
            Settings.CustomPorts = new List<int>(CustomPorts);
            Settings.PortRanges = PortRanges ?? "";
            Settings.RangesOnlyRescan = RangesOnlyRescan;
            Settings.Save();
        }
    }

    internal sealed class ScanOptions
    {
        public int PingTimeoutMs = 700;
        public int PingAttempts = 2;
        public int Threads = 256;
        public bool ScanPorts = true;
        public int PortTimeoutMs = 700;
        public int NameTimeoutMs = 1500;
        public int NameAttempts = 2;
        public int ProbeTimeoutMs = 2500;
        /// <summary>Extra ARP round after a short wait, for devices that wake up slowly (phones, IoT).</summary>
        public bool ExtraArp = false;

        /// <summary>Known service ports to check (HostInfo.Ports).</summary>
        public HashSet<int> ServicePorts = new HashSet<int>(Settings.DefaultServicePorts);

        /// <summary>User-defined single ports (not in HostInfo.Ports), checked one by one.</summary>
        public List<int> CustomPorts = new List<int>();

        /// <summary>Ports from the user's port ranges, checked in parallel.</summary>
        public List<int> RangePorts = new List<int>();

        /// <summary>Max. simultaneous connections for port ranges (whole scan).</summary>
        public int RangeParallel = 128;

        /// <summary>Max. simultaneous TCP connections for the whole scan (keeps endpoint IPS/AV calm).</summary>
        public int TcpParallel = 16;

        /// <summary>Pause between two port checks on the same host.</summary>
        public int PortGapMs = 120;

        public int Level = 3;

        /// <summary>Speed slider: 1 = fast … 3 = balanced (0.3.x behaviour) … 5 = thorough.</summary>
        /// <summary>Options from Settings. <paramref name="rescan"/>: also use port ranges when they are "rescan only".</summary>
        public static ScanOptions FromSettings(bool rescan = false) => From(ScanProfile.FromSettings(), rescan);

        public static ScanOptions From(ScanProfile p, bool rescan)
        {
            var o = new ScanOptions { Level = Math.Max(1, Math.Min(5, p.Speed)) };
            switch (o.Level)
            {
                case 1: o.Threads = 384; o.PingTimeoutMs = 400; o.PingAttempts = 1; o.PortTimeoutMs = 400; o.NameTimeoutMs = 800; o.NameAttempts = 1; o.ProbeTimeoutMs = 1500; o.TcpParallel = 32; o.PortGapMs = 0; break;
                case 2: o.Threads = 320; o.PingTimeoutMs = 550; o.PingAttempts = 2; o.PortTimeoutMs = 550; o.NameTimeoutMs = 1200; o.NameAttempts = 2; o.ProbeTimeoutMs = 2000; o.TcpParallel = 24; o.PortGapMs = 60; break;
                case 4: o.Threads = 192; o.PingTimeoutMs = 1000; o.PingAttempts = 3; o.PortTimeoutMs = 1000; o.NameTimeoutMs = 2400; o.NameAttempts = 3; o.ProbeTimeoutMs = 3500; o.TcpParallel = 12; o.PortGapMs = 150; o.ExtraArp = true; break;
                case 5: o.Threads = 128; o.PingTimeoutMs = 1500; o.PingAttempts = 4; o.PortTimeoutMs = 1500; o.NameTimeoutMs = 3000; o.NameAttempts = 3; o.ProbeTimeoutMs = 5000; o.TcpParallel = 8; o.PortGapMs = 200; o.ExtraArp = true; break;
            }
            o.ScanPorts = Settings.ScanPorts || rescan;
            o.ServicePorts = new HashSet<int>(p.ServicePorts);
            o.CustomPorts = p.CustomPorts.Where(x => !HostInfo.Ports.Any(k => k.Port == x)).ToList();
            if (rescan || !p.RangesOnlyRescan)
            {
                var ranges = Settings.ParseRanges(p.PortRanges, out _) ?? new List<int>();
                o.RangePorts = ranges.Where(x => !o.ServicePorts.Contains(x) && !o.CustomPorts.Contains(x) && !HostInfo.Ports.Any(k => k.Port == x))
                                     .Take(Settings.MaxRangePorts).ToList();
            }
            return o;
        }
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
        private readonly SemaphoreSlim rangeGate;

        public Scanner(ScanOptions options)
        {
            opt = options;
            tcpGate = new SemaphoreSlim(Math.Max(1, opt.TcpParallel));
            rangeGate = new SemaphoreSlim(Math.Max(1, opt.RangeParallel));
            subnets = IpRange.GetLocalSubnets();
            gateways = GetGateways();
        }

        public async Task RunAsync(IList<uint> targets, CancellationToken ct)
        {
            await OuiDb.LoadAsync().ConfigureAwait(false);
            Diag.Reset($"scan start: {targets.Count} addresses, level={opt.Level}, threads={opt.Threads}, ping={opt.PingTimeoutMs}x{opt.PingAttempts}, " +
                       $"ports={(opt.ScanPorts ? string.Join(",", opt.ServicePorts.Concat(opt.CustomPorts)) : "off")}, rangePorts={opt.RangePorts.Count}, tcpParallel={opt.TcpParallel}");

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
                if (host.Mac == null && opt.ExtraArp)
                {
                    await Task.Delay(400).ConfigureAwait(false);
                    host.Mac = await Task.Run(() => Native.ResolveMac(addr)).ConfigureAwait(false);
                }
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
            Diag.Host(ip, $"names  dns={(string.IsNullOrEmpty(dnsTask.Result) ? "-" : dnsTask.Result)}  netbios={nb?.ComputerName ?? "-"}{(nb != null ? " (" + nb.Source + ")" : "")}");

            // 2) Services: one port at a time per host, globally limited, with a short pause in between
            if (opt.ScanPorts && !ct.IsCancellationRequested)
            {
                foreach (var p in HostInfo.Ports)
                {
                    if (ct.IsCancellationRequested) break;
                    if (!opt.ServicePorts.Contains(p.Port)) continue;
                    var state = await CheckPortAsync(addr, p.Port).ConfigureAwait(false);
                    Diag.Host(ip, $"tcp/{p.Port,-5} {state}");
                    if (state == "open") host.Services |= p.Svc;
                    if (opt.PortGapMs > 0) await Task.Delay(opt.PortGapMs).ConfigureAwait(false);
                }
                foreach (var port in opt.CustomPorts)
                {
                    if (ct.IsCancellationRequested) break;
                    var state = await CheckPortAsync(addr, port).ConfigureAwait(false);
                    Diag.Host(ip, $"tcp/{port,-5} {state} (custom)");
                    if (state == "open") { host.Services |= Service.Custom; host.CustomOpen.Add(port); }
                    if (opt.PortGapMs > 0) await Task.Delay(opt.PortGapMs).ConfigureAwait(false);
                }

                // Port ranges: many ports, so checked in parallel (limited for the whole scan)
                if (opt.RangePorts.Count > 0 && !ct.IsCancellationRequested)
                {
                    var open = new System.Collections.Concurrent.ConcurrentBag<int>();
                    var checks = opt.RangePorts.Select(async port =>
                    {
                        if (ct.IsCancellationRequested) return;
                        await rangeGate.WaitAsync().ConfigureAwait(false);
                        try { if (await ConnectAsync(addr, port).ConfigureAwait(false)) open.Add(port); }
                        finally { rangeGate.Release(); }
                    }).ToList();
                    await Task.WhenAll(checks).ConfigureAwait(false);
                    foreach (var port in open.OrderBy(x => x)) { host.Services |= Service.Custom; host.CustomOpen.Add(port); }
                    Diag.Host(ip, $"range  {opt.RangePorts.Count} ports checked, open: {(open.IsEmpty ? "-" : string.Join(",", open.OrderBy(x => x)))}");
                }
            }

            // 3) Describe open services (page title, server, banner) — also through the TCP gate
            if (host.Services != Service.None && !ct.IsCancellationRequested)
            {
                var details = new List<ServiceDetail>();
                foreach (var p in HostInfo.Ports.Where(p => host.Has(p.Svc)).OrderBy(p => p.Svc == Service.Printer ? 0 : 1))
                {
                    await tcpGate.WaitAsync().ConfigureAwait(false);
                    try { details.Add(await ServiceProbe.DescribeAsync(addr, p, opt.ProbeTimeoutMs).ConfigureAwait(false)); }
                    finally { tcpGate.Release(); }
                }
                foreach (var port in host.CustomOpen)
                {
                    await tcpGate.WaitAsync().ConfigureAwait(false);
                    try { details.Add(await ServiceProbe.DescribeCustomAsync(addr, port, opt.ProbeTimeoutMs).ConfigureAwait(false)); }
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
                // Some stacks need a second probe after ARP resolution; higher levels try more often
                for (int i = 0; i < Math.Max(1, opt.PingAttempts); i++)
                {
                    var r = await p.SendPingAsync(addr, opt.PingTimeoutMs).ConfigureAwait(false);
                    if (r.Status == IPStatus.Success) return r.RoundtripTime;
                }
                return -1;
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
            NetBiosInfo r = null;
            int attempts = Math.Max(1, opt.NameAttempts);
            for (int i = 0; i < attempts && r == null; i++)
                r = await NetBios.QueryAsync(addr, Math.Max(300, opt.NameTimeoutMs / attempts)).ConfigureAwait(false);
            if (r != null) { r.Source = "udp"; return r; }
            // Fallback: Windows NetBIOS API (like "nbtstat -A") — not affected by app firewall rules
            r = await Task.Run(() => NetBiosApi.Query(addr)).ConfigureAwait(false);
            if (r != null) r.Source = "api";
            return r;
        }

        /// <summary>Quick TCP connect for port ranges (no global TCP gate, no logging per port).</summary>
        private async Task<bool> ConnectAsync(IPAddress addr, int port)
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
            catch { return false; }
            finally { c.Close(); }
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
