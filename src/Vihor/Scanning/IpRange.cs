using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace Vihor.Scanning
{
    /// <summary>A local IPv4 interface (address + mask) used for default ranges and L2 (ARP) decisions.</summary>
    internal sealed class LocalSubnet
    {
        public uint Address;
        public uint Mask;
        public byte[] Mac;
        public string InterfaceName;
        public bool HasGateway;

        public bool Contains(uint ip) => (ip & Mask) == (Address & Mask);
    }

    internal static class IpRange
    {
        public const int MaxAddresses = 65536;

        public static uint ToUInt(IPAddress ip)
        {
            var b = ip.GetAddressBytes();
            return ((uint)b[0] << 24) | ((uint)b[1] << 16) | ((uint)b[2] << 8) | b[3];
        }

        public static IPAddress ToIp(uint v)
            => new IPAddress(new[] { (byte)(v >> 24), (byte)(v >> 16), (byte)(v >> 8), (byte)v });

        public static string ToStr(uint v) => $"{v >> 24}.{(v >> 16) & 255}.{(v >> 8) & 255}.{v & 255}";

        public static List<LocalSubnet> GetLocalSubnets()
        {
            var list = new List<LocalSubnet>();
            try
            {
                foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (ni.OperationalStatus != OperationalStatus.Up) continue;
                    if (ni.NetworkInterfaceType == NetworkInterfaceType.Loopback ||
                        ni.NetworkInterfaceType == NetworkInterfaceType.Tunnel) continue;

                    var props = ni.GetIPProperties();
                    bool gw = props.GatewayAddresses.Any(g => g.Address.AddressFamily == AddressFamily.InterNetwork &&
                                                              !g.Address.Equals(IPAddress.Any));
                    foreach (var ua in props.UnicastAddresses)
                    {
                        if (ua.Address.AddressFamily != AddressFamily.InterNetwork || ua.IPv4Mask == null) continue;
                        var addr = ToUInt(ua.Address);
                        if ((addr >> 16) == 0xA9FE) continue; // 169.254.x.x APIPA
                        list.Add(new LocalSubnet
                        {
                            Address = addr,
                            Mask = ToUInt(ua.IPv4Mask),
                            Mac = ni.GetPhysicalAddress()?.GetAddressBytes(),
                            InterfaceName = ni.Name,
                            HasGateway = gw
                        });
                    }
                }
            }
            catch { /* no adapters info */ }

            // Adapters with a default gateway first (the "real" LAN), then the rest
            return list.OrderByDescending(s => s.HasGateway).ToList();
        }

        /// <summary>Default scan range for the primary adapter, limited to a /24 around the host.</summary>
        public static string DefaultRange(List<LocalSubnet> subnets)
        {
            var s = subnets.FirstOrDefault();
            if (s == null) return "192.168.1.1-192.168.1.254";

            uint mask = s.Mask;
            if (mask < 0xFFFFFF00) mask = 0xFFFFFF00; // larger than /24 -> scan the host's /24 by default
            uint net = s.Address & mask;
            uint bcast = net | ~mask;
            if (bcast - net < 2) return ToStr(s.Address);
            return $"{ToStr(net + 1)}-{ToStr(bcast - 1)}";
        }

        /// <summary>
        /// Parses ranges like:
        ///   192.168.1.1-192.168.1.254 | 192.168.1.1-254 | 192.168.1.0/24 | 10.0.0.5 | combinations separated by , ; or space
        /// </summary>
        public static List<uint> Parse(string text, out string error)
        {
            error = null;
            var result = new List<uint>();
            var seen = new HashSet<uint>();
            if (string.IsNullOrWhiteSpace(text)) { error = "empty"; return result; }

            foreach (var part0 in text.Split(new[] { ',', ';', ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var part = part0.Trim();
                uint start, end;

                if (part.Contains("/"))
                {
                    var p = part.Split('/');
                    if (p.Length != 2 || !TryIp(p[0], out var baseIp) || !int.TryParse(p[1], out int bits) || bits < 0 || bits > 32)
                    { error = part; return new List<uint>(); }
                    uint mask = bits == 0 ? 0 : 0xFFFFFFFF << (32 - bits);
                    uint net = baseIp & mask, bcast = net | ~mask;
                    if (bits <= 30) { start = net + 1; end = bcast - 1; }
                    else { start = net; end = bcast; }
                }
                else if (part.Contains("-"))
                {
                    var p = part.Split('-');
                    if (p.Length != 2 || !TryIp(p[0], out start)) { error = part; return new List<uint>(); }
                    if (TryIp(p[1], out end)) { }
                    else if (byte.TryParse(p[1], out byte last)) end = (start & 0xFFFFFF00) | last;
                    else { error = part; return new List<uint>(); }
                }
                else
                {
                    if (!TryIp(part, out start)) { error = part; return new List<uint>(); }
                    end = start;
                }

                if (end < start) { var t = start; start = end; end = t; }
                if ((ulong)end - start + 1 + (ulong)result.Count > MaxAddresses)
                { error = "too_many"; return new List<uint>(); }

                for (ulong ip = start; ip <= end; ip++)
                    if (seen.Add((uint)ip)) result.Add((uint)ip);
            }
            return result;
        }

        private static bool TryIp(string s, out uint value)
        {
            value = 0;
            var p = s.Trim().Split('.');
            if (p.Length != 4) return false;
            foreach (var o in p)
            {
                if (!byte.TryParse(o, out byte b)) return false;
                value = (value << 8) | b;
            }
            return true;
        }
    }
}
