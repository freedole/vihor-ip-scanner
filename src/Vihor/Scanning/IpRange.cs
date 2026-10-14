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
        public string InterfaceName;   // e.g. "Ethernet", "Wi-Fi"
        public string Description;     // e.g. "Realtek PCIe GbE Family Controller"
        public bool HasGateway;
        public bool IsVirtual;         // VPN / hypervisor / container adapter
        public bool IsWireless;
        public bool IsHostVSwitch;     // Hyper-V vEthernet with a gateway = the host's real LAN through an external switch

        /// <summary>Physical LAN, or a Hyper-V external switch carrying the host's LAN.</summary>
        public bool IsLan => !IsVirtual || IsHostVSwitch;

        public bool Contains(uint ip) => (ip & Mask) == (Address & Mask);

        public int PrefixLength
        {
            get { int n = 0; for (uint m = Mask; (m & 0x80000000) != 0; m <<= 1) n++; return n; }
        }

        /// <summary>Range to scan for this adapter; networks larger than /24 are limited to the host's /24.</summary>
        public string ScanRange
        {
            get
            {
                uint mask = Mask < 0xFFFFFF00 ? 0xFFFFFF00 : Mask;
                uint net = Address & mask, bcast = net | ~mask;
                if (bcast - net < 2) return IpRange.ToStr(Address);
                return IpRange.FormatRange(net + 1, bcast - 1);
            }
        }

        /// <summary>Menu label, e.g. "Wi-Fi — 192.168.16.25/24 (Intel(R) Wi-Fi 6 AX201)".</summary>
        public string Label
        {
            get
            {
                var d = string.IsNullOrEmpty(Description) || Description == InterfaceName ? "" : $"  ({Description})";
                return $"{InterfaceName} — {IpRange.ToStr(Address)}/{PrefixLength}{d}";
            }
        }
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

        // Adapter descriptions / names that indicate a VPN, hypervisor or container network
        private static readonly string[] VirtualHints =
        {
            "virtual", "hyper-v", "vethernet", "vmware", "virtualbox", "vbox", "radmin", "hamachi", "zerotier",
            "tailscale", "wireguard", "openvpn", "tap-windows", "tap adapter", "wintun", "vpn", "docker", "wsl",
            "loopback", "bluetooth", "npcap", "fortinet", "cisco anyconnect", "globalprotect", "pangp", "nordlynx",
            "proton", "softether", "parallels", "kerio", "sonicwall", "teamviewer", "anydesk"
        };

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

                    var text = (ni.Name + " " + ni.Description).ToLowerInvariant();
                    bool isVirtual = VirtualHints.Any(h => text.Contains(h));
                    bool isWireless = ni.NetworkInterfaceType == NetworkInterfaceType.Wireless80211;
                    if (!isVirtual && ni.NetworkInterfaceType != NetworkInterfaceType.Ethernet &&
                        ni.NetworkInterfaceType != NetworkInterfaceType.GigabitEthernet &&
                        ni.NetworkInterfaceType != NetworkInterfaceType.FastEthernetT &&
                        ni.NetworkInterfaceType != NetworkInterfaceType.FastEthernetFx && !isWireless)
                        isVirtual = true; // PPP, unknown, etc.

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
                            Description = ni.Description,
                            HasGateway = gw,
                            IsVirtual = isVirtual,
                            IsWireless = isWireless,
                            IsHostVSwitch = gw && (text.Contains("vethernet") || text.Contains("hyper-v virtual ethernet"))
                                               && !text.Contains("default switch") && !text.Contains("wsl"),
                        });
                    }
                }
            }
            catch { /* no adapters info */ }

            // Physical LAN first (wired before Wi-Fi, with gateway before without), virtual adapters last
            return list.OrderBy(s => !s.IsLan)
                       .ThenBy(s => s.IsVirtual)
                       .ThenByDescending(s => s.HasGateway)
                       .ThenBy(s => s.IsWireless)
                       .ToList();
        }

        /// <summary>Default scan range: all physical adapters together; virtual ones only if nothing else exists.</summary>
        public static string DefaultRange(List<LocalSubnet> subnets)
        {
            var physical = subnets.Where(s => s.IsLan).ToList();
            var use = physical.Count > 0 ? physical : subnets.Take(1).ToList();
            if (use.Count == 0) return "192.168.1.1-254";
            return JoinRanges(use);
        }

        public static string JoinRanges(IEnumerable<LocalSubnet> subnets)
            => string.Join(", ", subnets.Select(s => s.ScanRange).Distinct());

        /// <summary>"192.168.1.1-192.168.1.254" -> "192.168.1.1-254" when only the last octet differs.</summary>
        public static string FormatRange(uint start, uint end)
        {
            if (start == end) return ToStr(start);
            if ((start & 0xFFFFFF00) == (end & 0xFFFFFF00)) return $"{ToStr(start)}-{end & 255}";
            return $"{ToStr(start)}-{ToStr(end)}";
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
