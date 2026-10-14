using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Windows.Forms;
using Vihor.Scanning;

namespace Vihor
{
    /// <summary>Actions available from the context menu. Everything uses built-in Windows tools.</summary>
    internal static class HostActions
    {
        public static void Rdp(HostInfo h) => Start("mstsc.exe", "/v:" + h.IpText);

        public static void Ssh(HostInfo h) => Start("cmd.exe", "/k ssh " + h.IpText);

        public static void Http(HostInfo h, int port = 80) =>
            Start(port == 80 ? "http://" + h.IpText + "/" : $"http://{h.IpText}:{port}/", null);

        public static void Https(HostInfo h) => Start("https://" + h.IpText + "/", null);

        public static void Ftp(HostInfo h) => Start("explorer.exe", "ftp://" + h.IpText + "/");

        public static void Explore(HostInfo h) => Start("explorer.exe", @"\\" + h.IpText);

        public static void Ping(HostInfo h) => Start("cmd.exe", "/k ping -t " + h.IpText);

        public static void Tracert(HostInfo h) => Start("cmd.exe", "/k tracert " + h.IpText);

        /// <summary>Remote shutdown (needs admin rights on the target). 60 s delay so it can be aborted.</summary>
        public static void Shutdown(HostInfo h, bool restart)
        {
            var msg = Lang.T("msg.shutdown_confirm", h.IpText, string.IsNullOrEmpty(h.Name) ? h.IpText : h.Name);
            if (MessageBox.Show(msg, AppInfo.Name, MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) != DialogResult.Yes)
                return;
            Start("cmd.exe", $"/k shutdown {(restart ? "/r" : "/s")} /m \\\\{h.IpText} /t 60 /c \"{AppInfo.Name}\"");
        }

        public static void AbortShutdown(HostInfo h) => Start("cmd.exe", $"/k shutdown /a /m \\\\{h.IpText}");

        /// <summary>Wake-on-LAN magic packet to the limited broadcast and the subnet broadcast, ports 7 and 9.</summary>
        public static void WakeOnLan(HostInfo h)
        {
            if (h.Mac == null) { Info(Lang.T("msg.no_mac")); return; }

            var packet = new byte[102];
            for (int i = 0; i < 6; i++) packet[i] = 0xFF;
            for (int i = 1; i <= 16; i++) Array.Copy(h.Mac, 0, packet, i * 6, 6);

            try
            {
                using var udp = new UdpClient { EnableBroadcast = true };
                uint directed = h.Ip | 0x000000FF; // /24 directed broadcast as a sensible default
                foreach (var target in new[] { IPAddress.Broadcast, IpRange.ToIp(directed) })
                    foreach (var port in new[] { 9, 7 })
                        udp.Send(packet, packet.Length, new IPEndPoint(target, port));
                Info(Lang.T("msg.wol_sent", h.MacText));
            }
            catch (Exception ex)
            {
                Info(ex.Message);
            }
        }

        /// <summary>URL or command target shown in tooltips, e.g. https://192.168.16.10:8006/</summary>
        public static string ServiceTarget(HostInfo h, ServiceDetail d)
        {
            var def = d.Def;
            if (def.IsWeb)
            {
                bool std = (def.Scheme == "http" && def.Port == 80) || (def.Scheme == "https" && def.Port == 443);
                return std ? $"{def.Scheme}://{h.IpText}/" : $"{def.Scheme}://{h.IpText}:{def.Port}/";
            }
            switch (def.Svc)
            {
                case Service.Ssh: return "ssh " + h.IpText;
                case Service.Rdp: return "mstsc /v:" + h.IpText;
                case Service.Smb: return @"\\" + h.IpText;
                case Service.Ftp: return "ftp://" + h.IpText + "/";
                case Service.Telnet: return "telnet " + h.IpText;
                default: return $"{h.IpText}:{def.Port}";
            }
        }

        /// <summary>Opens a detected service with the matching Windows tool.</summary>
        public static void OpenService(HostInfo h, ServiceDetail d)
        {
            var def = d.Def;
            if (def.IsWeb) { Start(ServiceTarget(h, d), null); return; }
            switch (def.Svc)
            {
                case Service.Ssh: Ssh(h); break;
                case Service.Rdp: Rdp(h); break;
                case Service.Smb: Explore(h); break;
                case Service.Ftp: Ftp(h); break;
                case Service.Telnet: Start("cmd.exe", "/k telnet " + h.IpText); break;
                default:
                    Copy($"{h.IpText}:{def.Port}");
                    Info(Lang.T("msg.copied", $"{h.IpText}:{def.Port}"));
                    break;
            }
        }

        public static void Copy(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            try { Clipboard.SetText(text); } catch { /* clipboard busy */ }
        }

        private static void Start(string file, string args)
        {
            try
            {
                var psi = new ProcessStartInfo(file) { UseShellExecute = true };
                if (args != null) psi.Arguments = args;
                Process.Start(psi);
            }
            catch (Exception ex)
            {
                Info(ex.Message);
            }
        }

        private static void Info(string text) =>
            MessageBox.Show(text, AppInfo.Name, MessageBoxButtons.OK, MessageBoxIcon.Information);
    }
}
