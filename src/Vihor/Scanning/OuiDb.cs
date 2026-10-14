using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Vihor.Scanning
{
    /// <summary>
    /// Offline MAC vendor lookup from the IEEE registries (MA-L / MA-M / MA-S),
    /// embedded as a gzip "PREFIX\tVendor" list. Built by tools/build_oui.py.
    /// </summary>
    internal static class OuiDb
    {
        private static readonly Dictionary<string, string> Map = new Dictionary<string, string>(StringComparer.Ordinal);
        private static Task loadTask;

        public static Task LoadAsync() => loadTask ??= Task.Run(Load);

        private static void Load()
        {
            try
            {
                using var s = Assembly.GetExecutingAssembly().GetManifestResourceStream("Vihor.oui.txt.gz");
                if (s == null) return;
                using var gz = new GZipStream(s, CompressionMode.Decompress);
                using var r = new StreamReader(gz, Encoding.UTF8);
                string line;
                while ((line = r.ReadLine()) != null)
                {
                    int tab = line.IndexOf('\t');
                    if (tab < 6) continue;
                    Map[line.Substring(0, tab)] = Prettify(line.Substring(tab + 1));
                }
            }
            catch { /* vendor lookup is optional */ }
        }

        public static string Lookup(byte[] mac)
        {
            if (mac == null || mac.Length < 6) return "";
            loadTask?.Wait();

            // Locally administered / randomized MAC (e.g. phone "private Wi-Fi address")
            if ((mac[0] & 0x02) != 0) return Lang.T("vendor.random");

            var hex = BitConverter.ToString(mac).Replace("-", "");
            if (Map.TryGetValue(hex.Substring(0, 9), out var v)) return v; // MA-S (36 bit)
            if (Map.TryGetValue(hex.Substring(0, 7), out v)) return v;     // MA-M (28 bit)
            if (Map.TryGetValue(hex.Substring(0, 6), out v)) return v;     // MA-L (24 bit)
            return "";
        }

        /// <summary>"TP-LINK TECHNOLOGIES CO.,LTD." -> "TP-LINK TECHNOLOGIES" — keeps the table readable.</summary>
        private static string Prettify(string name)
        {
            var suffixes = new[]
            {
                " CO.,LTD.", " CO., LTD.", " CO.,LTD", " CO., LTD", " Co.,Ltd.", " Co., Ltd.", " Co.,Ltd", " Co., Ltd",
                ", Inc.", " Inc.", ", Inc", " Inc", ", LLC", " LLC", " GmbH", " AG", " S.A.", " s.r.o.", " Ltd.", " Ltd",
                " Corporation", " Corp.", " Corp", " Limited", " B.V.", " AB", " Oy", " S.p.A.", " d.o.o."
            };
            name = name.Trim().TrimEnd(',');
            bool changed = true;
            while (changed)
            {
                changed = false;
                foreach (var s in suffixes)
                {
                    if (name.Length > s.Length + 2 && name.EndsWith(s, StringComparison.OrdinalIgnoreCase))
                    {
                        name = name.Substring(0, name.Length - s.Length).TrimEnd(',', ' ');
                        changed = true;
                    }
                }
            }
            return name;
        }
    }
}
