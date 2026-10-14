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
