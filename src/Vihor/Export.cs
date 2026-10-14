using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Text;
using Vihor.Scanning;

namespace Vihor
{
    internal static class Export
    {
        private static string[] Header => new[]
        {
            Lang.T("col.ip"), Lang.T("col.name"), Lang.T("col.vendor"), Lang.T("col.mac"),
            Lang.T("col.services"), Lang.T("col.ping"), Lang.T("col.workgroup")
        };

        private static string[] Row(HostInfo h) => new[]
        {
            h.IpText, h.Name, h.Vendor, h.MacText, h.ServicesText,
            h.PingMs < 0 ? "" : h.PingMs.ToString(CultureInfo.InvariantCulture), h.Workgroup
        };

        /// <summary>CSV with the system list separator (";" on most European locales) so Excel opens it directly.</summary>
        public static void Csv(string path, IEnumerable<HostInfo> hosts)
        {
            var sep = CultureInfo.CurrentCulture.TextInfo.ListSeparator;
            var sb = new StringBuilder();
            sb.AppendLine(string.Join(sep, Quote(Header, sep)));
            foreach (var h in hosts) sb.AppendLine(string.Join(sep, Quote(Row(h), sep)));
            File.WriteAllText(path, sb.ToString(), new UTF8Encoding(true));
        }

        public static void Html(string path, IEnumerable<HostInfo> hosts, string range)
        {
            var sb = new StringBuilder();
            sb.Append("<!DOCTYPE html><html><head><meta charset=\"utf-8\"><title>")
              .Append(WebUtility.HtmlEncode(AppInfo.Name)).Append(" — ").Append(WebUtility.HtmlEncode(range))
              .Append("</title><style>")
              .Append("body{font-family:Segoe UI,Arial,sans-serif;margin:24px;color:#1b2430}")
              .Append("h1{font-size:20px;margin:0 0 4px}p{color:#5a6573;margin:0 0 16px}")
              .Append("table{border-collapse:collapse;width:100%;font-size:13px}")
              .Append("th,td{border-bottom:1px solid #e3e7ec;padding:6px 8px;text-align:left}")
              .Append("th{background:#f3f5f8}tr:hover td{background:#fafbfc}")
              .Append("td.mono{font-family:Consolas,monospace}")
              .Append("</style></head><body>");
            sb.Append("<h1>").Append(WebUtility.HtmlEncode(AppInfo.Name)).Append("</h1><p>")
              .Append(WebUtility.HtmlEncode(range)).Append(" · ")
              .Append(System.DateTime.Now.ToString("yyyy-MM-dd HH:mm")).Append(" · ")
              .Append("<a href=\"").Append(AppInfo.Website).Append("\">cyber-security.ba</a></p><table><tr>");
            foreach (var c in Header) sb.Append("<th>").Append(WebUtility.HtmlEncode(c)).Append("</th>");
            sb.Append("</tr>");
            foreach (var h in hosts)
            {
                sb.Append("<tr>");
                var r = Row(h);
                for (int i = 0; i < r.Length; i++)
                    sb.Append(i == 0 || i == 3 ? "<td class=\"mono\">" : "<td>").Append(WebUtility.HtmlEncode(r[i])).Append("</td>");
                sb.Append("</tr>");
            }
            sb.Append("</table></body></html>");
            File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
        }

        private static IEnumerable<string> Quote(string[] values, string sep)
        {
            foreach (var v in values)
            {
                var s = v ?? "";
                if (s.Contains(sep) || s.Contains("\"") || s.Contains("\n")) s = "\"" + s.Replace("\"", "\"\"") + "\"";
                yield return s;
            }
        }
    }
}
