using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Vihor.Scanning
{
    /// <summary>Light, read-only probes that describe an open port: web page title + Server header, or a text banner.</summary>
    internal static class ServiceProbe
    {
        private static readonly Regex TitleRx = new Regex(@"<title[^>]*>(.*?)</title>",
            RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

        public static Task<ServiceDetail> DescribeAsync(IPAddress ip, PortDef def, int timeoutMs)
        {
            if (def.IsWeb) return Task.Run(() => Web(ip, def, timeoutMs));
            if (def.Svc == Service.Ssh || def.Svc == Service.Ftp || def.Svc == Service.Vnc)
                return BannerAsync(ip, def, timeoutMs);
            return Task.FromResult(new ServiceDetail { Def = def });
        }

        private static ServiceDetail Web(IPAddress ip, PortDef def, int timeoutMs)
        {
            var d = new ServiceDetail { Def = def };
            try
            {
                var url = $"{def.Scheme}://{ip}:{def.Port}/";
                var req = (HttpWebRequest)WebRequest.Create(url);
                req.Method = "GET";
                req.Timeout = timeoutMs;
                req.ReadWriteTimeout = timeoutMs;
                req.AllowAutoRedirect = true;
                req.MaximumAutomaticRedirections = 3;
                req.UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) VihorIPScanner";
                req.Accept = "text/html,*/*";
                req.Proxy = null;
                // LAN devices almost always use self-signed certificates; we only read the title.
                req.ServerCertificateValidationCallback = (s, c, ch, e) => true;

                HttpWebResponse resp;
                try { resp = (HttpWebResponse)req.GetResponse(); }
                catch (WebException wex) when (wex.Response is HttpWebResponse r) { resp = r; } // 401/403/404 still tell us the server

                using (resp)
                {
                    d.Server = Clean(resp.Headers["Server"], 40);
                    using var s = resp.GetResponseStream();
                    if (s == null) return d;
                    var buf = new byte[65536];
                    int total = 0, n;
                    while (total < buf.Length && (n = s.Read(buf, total, buf.Length - total)) > 0) total += n;
                    var html = Encoding.UTF8.GetString(buf, 0, total);
                    var m = TitleRx.Match(html);
                    if (m.Success) d.Title = Clean(WebUtility.HtmlDecode(m.Groups[1].Value), 70);
                }
            }
            catch { /* port open but no usable HTTP answer */ }
            return d;
        }

        private static async Task<ServiceDetail> BannerAsync(IPAddress ip, PortDef def, int timeoutMs)
        {
            var d = new ServiceDetail { Def = def };
            using var c = new TcpClient(AddressFamily.InterNetwork);
            try
            {
                var conn = c.ConnectAsync(ip, def.Port);
                if (await Task.WhenAny(conn, Task.Delay(timeoutMs)).ConfigureAwait(false) != conn) return d;
                await conn.ConfigureAwait(false);

                var stream = c.GetStream();
                var buf = new byte[256];
                var read = stream.ReadAsync(buf, 0, buf.Length);
                if (await Task.WhenAny(read, Task.Delay(timeoutMs)).ConfigureAwait(false) != read) return d;
                int n = await read.ConfigureAwait(false);
                if (n <= 0) return d;

                var text = Encoding.ASCII.GetString(buf, 0, n).Split('\r', '\n')[0];
                if (def.Svc == Service.Ssh && text.StartsWith("SSH-", StringComparison.Ordinal))
                {
                    // "SSH-2.0-OpenSSH_9.2p1 Debian-2+deb12u3" -> "OpenSSH_9.2p1 Debian-2+deb12u3"
                    int i = text.IndexOf('-', 4);
                    text = i > 0 ? text.Substring(i + 1) : text;
                }
                else if (def.Svc == Service.Ftp && text.Length > 4 && text.StartsWith("220", StringComparison.Ordinal))
                {
                    text = text.Substring(3).Trim(' ', '-');
                }
                else if (def.Svc == Service.Vnc && text.StartsWith("RFB", StringComparison.Ordinal))
                {
                    text = "RFB " + text.Substring(3).Trim();
                }
                d.Banner = Clean(text, 60);
            }
            catch { }
            return d;
        }

        private static string Clean(string s, int max)
        {
            if (string.IsNullOrEmpty(s)) return "";
            var sb = new StringBuilder(s.Length);
            bool space = false;
            foreach (var ch in s)
            {
                if (char.IsWhiteSpace(ch) || char.IsControl(ch)) { if (!space && sb.Length > 0) { sb.Append(' '); space = true; } }
                else { sb.Append(ch); space = false; }
            }
            var r = sb.ToString().Trim();
            return r.Length > max ? r.Substring(0, max - 1) + "…" : r;
        }
    }
}
