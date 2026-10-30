using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Vihor.Scanning
{
    /// <summary>
    /// Remembers the result of the last complete scan per IP range (%APPDATA%\Vihor\history\*.tsv),
    /// so the next scan can mark devices that are new or missing. Devices are matched by MAC address,
    /// or by IP address when the MAC is unknown (e.g. hosts behind a router).
    /// </summary>
    internal sealed class ScanHistory
    {
        private sealed class Entry
        {
            public uint Ip;
            public string Mac = "";
            public string Name = "";
            public string Vendor = "";
        }

        private readonly List<Entry> entries = new List<Entry>();
        private readonly HashSet<string> macs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<uint> ips = new HashSet<uint>();
        private readonly HashSet<uint> ipsWithoutMac = new HashSet<uint>();

        /// <summary>When the previous scan was saved; null if there is none (first scan of this range).</summary>
        public DateTime? When { get; private set; }

        public bool HasPrevious => When.HasValue;

        private static string Dir => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Vihor", "history");

        private static string FileFor(string range)
        {
            using var sha = SHA1.Create();
            var hash = sha.ComputeHash(Encoding.UTF8.GetBytes((range ?? "").Trim().ToLowerInvariant()));
            return Path.Combine(Dir, BitConverter.ToString(hash, 0, 8).Replace("-", "").ToLowerInvariant() + ".tsv");
        }

        public static ScanHistory Load(string range)
        {
            var h = new ScanHistory();
            try
            {
                var path = FileFor(range);
                if (!File.Exists(path)) return h;
                foreach (var line in File.ReadAllLines(path, Encoding.UTF8))
                {
                    if (line.StartsWith("#", StringComparison.Ordinal)) continue;
                    var f = line.Split('\t');
                    if (f.Length < 2 || !uint.TryParse(f[0], out var ip)) continue;
                    var e = new Entry { Ip = ip, Mac = f[1], Name = f.Length > 2 ? f[2] : "", Vendor = f.Length > 3 ? f[3] : "" };
                    h.entries.Add(e);
                    h.ips.Add(ip);
                    if (e.Mac.Length > 0) h.macs.Add(e.Mac); else h.ipsWithoutMac.Add(ip);
                }
                h.When = File.GetLastWriteTime(path);
            }
            catch { /* history is best effort */ }
            return h;
        }

        public static void Save(string range, IEnumerable<HostInfo> hosts)
        {
            try
            {
                Directory.CreateDirectory(Dir);
                var sb = new StringBuilder();
                sb.Append("# Vihor IP Scanner - last scan of ").AppendLine(range);
                foreach (var h in hosts.Where(x => !x.IsMissing))
                    sb.Append(h.Ip).Append('\t').Append(h.MacText).Append('\t')
                      .Append(Clean(h.Name)).Append('\t').Append(Clean(h.Vendor)).AppendLine();
                File.WriteAllText(FileFor(range), sb.ToString(), new UTF8Encoding(false));
            }
            catch { }
        }

        /// <summary>True if this device was not in the previous scan.</summary>
        public bool IsNew(HostInfo h)
        {
            if (!HasPrevious) return false;
            if (h.Mac != null) return !macs.Contains(h.MacText) && !ipsWithoutMac.Contains(h.Ip);
            return !ips.Contains(h.Ip);
        }

        /// <summary>Devices from the previous scan that were not found now, as placeholder rows.</summary>
        public List<HostInfo> Missing(IEnumerable<HostInfo> found)
        {
            var result = new List<HostInfo>();
            if (!HasPrevious) return result;
            var list = found.ToList();
            var foundMacs = new HashSet<string>(list.Where(x => x.Mac != null).Select(x => x.MacText), StringComparer.OrdinalIgnoreCase);
            var foundIps = new HashSet<uint>(list.Select(x => x.Ip));
            var foundIpsWithoutMac = new HashSet<uint>(list.Where(x => x.Mac == null).Select(x => x.Ip));
            foreach (var e in entries)
            {
                bool seen = e.Mac.Length > 0 ? foundMacs.Contains(e.Mac) || foundIpsWithoutMac.Contains(e.Ip) : foundIps.Contains(e.Ip);
                if (seen) continue;
                result.Add(new HostInfo
                {
                    Ip = e.Ip,
                    Name = e.Name,
                    Vendor = e.Vendor,
                    Mac = ParseMac(e.Mac),
                    IsMissing = true,
                });
            }
            return result;
        }

        private static byte[] ParseMac(string s)
        {
            if (string.IsNullOrEmpty(s)) return null;
            try { return s.Split(':').Select(x => Convert.ToByte(x, 16)).ToArray(); }
            catch { return null; }
        }

        private static string Clean(string s) => (s ?? "").Replace('\t', ' ').Replace('\r', ' ').Replace('\n', ' ');
    }
}
