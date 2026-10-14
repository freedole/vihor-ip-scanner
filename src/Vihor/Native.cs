using System;
using System.Net;
using System.Runtime.InteropServices;

namespace Vihor
{
    internal static class Native
    {
        [DllImport("iphlpapi.dll", ExactSpelling = true)]
        private static extern int SendARP(uint destIp, uint srcIp, byte[] macAddr, ref uint physAddrLen);

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        /// <summary>Resolves the MAC address of a host on the local L2 segment. Returns null if no reply.</summary>
        public static byte[] ResolveMac(IPAddress ip)
        {
            try
            {
                var mac = new byte[6];
                uint len = (uint)mac.Length;
#pragma warning disable CS0618 // Address is fine for IPv4
                uint dest = (uint)ip.Address;
#pragma warning restore CS0618
                int r = SendARP(dest, 0, mac, ref len);
                if (r != 0 || len < 6) return null;
                bool allZero = true;
                foreach (var b in mac) if (b != 0) { allZero = false; break; }
                return allZero ? null : mac;
            }
            catch
            {
                return null;
            }
        }

        [DllImport("iphlpapi.dll", ExactSpelling = true)]
        private static extern int GetIpNetTable(IntPtr pIpNetTable, ref int pdwSize, bool bOrder);

        /// <summary>Reads the Windows ARP cache (IPv4 address in network order -> MAC). Dynamic and static entries only.</summary>
        public static System.Collections.Generic.Dictionary<uint, byte[]> ReadArpCache()
        {
            var map = new System.Collections.Generic.Dictionary<uint, byte[]>();
            int size = 0;
            GetIpNetTable(IntPtr.Zero, ref size, false);
            if (size <= 0) return map;
            IntPtr buf = Marshal.AllocHGlobal(size);
            try
            {
                if (GetIpNetTable(buf, ref size, false) != 0) return map;
                int count = Marshal.ReadInt32(buf);
                IntPtr row = buf + 4;
                const int rowSize = 24; // dwIndex, dwPhysAddrLen, bPhysAddr[8], dwAddr, dwType
                for (int i = 0; i < count; i++, row += rowSize)
                {
                    int len = Marshal.ReadInt32(row, 4);
                    int type = Marshal.ReadInt32(row, 20);
                    if (len != 6 || (type != 3 && type != 4)) continue;
                    var mac = new byte[6];
                    Marshal.Copy(row + 8, mac, 0, 6);
                    bool zero = true, bcast = true;
                    foreach (var x in mac) { if (x != 0) zero = false; if (x != 0xFF) bcast = false; }
                    if (zero || bcast) continue;
                    uint addr = (uint)Marshal.ReadInt32(row, 16);
                    // network byte order -> host order used by IpRange
                    uint ip = ((addr & 0xFF) << 24) | ((addr & 0xFF00) << 8) | ((addr >> 8) & 0xFF00) | (addr >> 24);
                    map[ip] = mac;
                }
            }
            catch { }
            finally { Marshal.FreeHGlobal(buf); }
            return map;
        }

        public static void BringExistingToFront(string titlePrefix)
        {
            try
            {
                foreach (var p in System.Diagnostics.Process.GetProcessesByName("VihorIPScanner"))
                {
                    if (p.MainWindowHandle != IntPtr.Zero && p.MainWindowTitle.StartsWith(titlePrefix, StringComparison.Ordinal))
                    {
                        ShowWindow(p.MainWindowHandle, 9 /* SW_RESTORE */);
                        SetForegroundWindow(p.MainWindowHandle);
                        return;
                    }
                }
            }
            catch { /* best effort */ }
        }
    }
}
