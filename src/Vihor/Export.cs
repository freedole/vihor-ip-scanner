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
            Lang.T("col.services"), Lang.T("col.ping"), Lang.T("col.workgroup"), Lang.T("col.note"), Lang.T("col.status")
        };

        private static string[] Row(HostInfo h) => new[]
        {
            h.IpText, h.Name, h.Vendor, h.MacText, h.ServicesText,
            h.IsMissing || h.PingMs < 0 ? "" : h.PingMs.ToString(CultureInfo.InvariantCulture), h.Workgroup,
            Notes.Get(h), h.IsMissing ? Lang.T("status.missing") : h.IsNew ? Lang.T("status.new") : ""
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

        /// <summary>JSON for scripts: one object per device, with every detected service.</summary>
        public static void Json(string path, IEnumerable<HostInfo> hosts, string range)
        {
            var sb = new StringBuilder();
            sb.Append("{\n  \"tool\": ").Append(J(AppInfo.Name + " " + AppInfo.Version))
              .Append(",\n  \"range\": ").Append(J(range))
              .Append(",\n  \"time\": ").Append(J(System.DateTime.Now.ToString("s")))
              .Append(",\n  \"devices\": [");
            bool first = true;
            foreach (var h in hosts)
            {
                sb.Append(first ? "\n" : ",\n");
                first = false;
                sb.Append("    { \"ip\": ").Append(J(h.IpText))
                  .Append(", \"name\": ").Append(J(h.Name))
                  .Append(", \"nameSource\": ").Append(J(h.NameSource))
                  .Append(", \"type\": ").Append(J(h.Type.ToString()))
                  .Append(", \"os\": ").Append(J(h.Os))
                  .Append(", \"mac\": ").Append(J(h.MacText))
                  .Append(", \"vendor\": ").Append(J(h.Vendor))
                  .Append(", \"workgroup\": ").Append(J(h.Workgroup))
                  .Append(", \"pingMs\": ").Append(h.PingMs < 0 ? "null" : h.PingMs.ToString(CultureInfo.InvariantCulture))
                  .Append(", \"ttl\": ").Append(h.Ttl > 0 ? h.Ttl.ToString(CultureInfo.InvariantCulture) : "null")
                  .Append(", \"status\": ").Append(J(h.IsMissing ? "missing" : h.IsNew ? "new" : ""))
                  .Append(", \"self\": ").Append(h.IsSelf ? "true" : "false")
                  .Append(", \"gateway\": ").Append(h.IsGateway ? "true" : "false")
                  .Append(", \"note\": ").Append(J(Notes.Get(h)))
                  .Append(", \"sameMacAs\": [");
                sb.Append(string.Join(", ", h.SameMac.ConvertAll(ip => J(IpRange.ToStr(ip)))));
                sb.Append("], \"conflictMac\": ").Append(h.ConflictMac == null ? "null" : J(h.ConflictMacText));
                sb.Append(", \"services\": [");
                bool f2 = true;
                foreach (var d in h.Details)
                {
                    sb.Append(f2 ? "" : ", ");
                    f2 = false;
                    sb.Append("{ \"port\": ").Append(d.Def.Port)
                      .Append(", \"name\": ").Append(J(d.Def.Label))
                      .Append(", \"product\": ").Append(J(d.Product))
                      .Append(", \"title\": ").Append(J(d.Title))
                      .Append(", \"server\": ").Append(J(d.Server))
                      .Append(", \"banner\": ").Append(J(d.Banner));
                    if (d.CertExpires.HasValue)
                        sb.Append(", \"certExpires\": ").Append(J(d.CertExpires.Value.ToString("yyyy-MM-dd")))
                          .Append(", \"certIssuer\": ").Append(J(d.CertIssuer))
                          .Append(", \"certSelfSigned\": ").Append(d.CertSelfSigned ? "true" : "false");
                    sb.Append(" }");
                }
                sb.Append("] }");
            }
            sb.Append("\n  ]\n}\n");
            File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
        }

        private static string J(string s)
        {
            var sb = new StringBuilder("\"");
            foreach (var c in s ?? "")
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4")); else sb.Append(c);
                        break;
                }
            }
            return sb.Append('"').ToString();
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
