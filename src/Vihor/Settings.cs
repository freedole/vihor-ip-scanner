using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Vihor
{
    /// <summary>
    /// Simple INI-style settings.
    /// Defaults come from &lt;app dir&gt;\vihor.ini (written by the installer, e.g. chosen language);
    /// per-user overrides live in %APPDATA%\Vihor\settings.ini.
    /// </summary>
    internal static class Settings
    {
        public static string Language = "en";
        public static string LastRange = "";
        public static bool ScanPorts = true;

        /// <summary>1 = fastest … 5 = most thorough (3 = balanced default).</summary>
        public static int Speed = 3;

        /// <summary>Known service ports that are checked (see HostInfo.Ports).</summary>
        public static readonly int[] DefaultServicePorts = { 80, 443, 3389, 445, 22, 21, 9100 };
        public static List<int> ServicePorts = new List<int>(DefaultServicePorts); // must come after DefaultServicePorts

        /// <summary>Extra TCP ports chosen by the user, e.g. 8123 (Home Assistant).</summary>
        public static List<int> CustomPorts = new List<int>();

        public static bool ShowWorkgroup = true;

        /// <summary>"system", "light" or "dark".</summary>
        public static string Theme = "system";
        public static int WindowWidth = 1100;
        public static int WindowHeight = 650;
        public static bool WindowMaximized = false;

        private static string UserDir => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Vihor");
        private static string UserFile => Path.Combine(UserDir, "settings.ini");
        private static string AppFile => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "vihor.ini");

        public static void Load()
        {
            var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            Read(AppFile, d);
            Read(UserFile, d);

            if (d.TryGetValue("Language", out var l) && !string.IsNullOrWhiteSpace(l)) Language = Lang.Normalize(l);
            else Language = Lang.Normalize(System.Globalization.CultureInfo.CurrentUICulture.Name); // e.g. Store install: follow Windows
            if (d.TryGetValue("LastRange", out var r)) LastRange = r;
            ScanPorts = GetInt(d, "ScanPorts", ScanPorts ? 1 : 0) != 0;
            Speed = Clamp(GetInt(d, "Speed", Speed), 1, 5);
            if (d.TryGetValue("ServicePorts", out var sp)) ServicePorts = ParsePorts(sp);
            else if (GetInt(d, "ExtendedPorts", 0) != 0) // 0.3.x "extended" setting -> all known services
                ServicePorts = Scanning.HostInfo.Ports.Select(p => p.Port).ToList();
            if (d.TryGetValue("CustomPorts", out var cp)) CustomPorts = ParsePorts(cp);
            ShowWorkgroup = GetInt(d, "ShowWorkgroup", 1) != 0;
            if (d.TryGetValue("Theme", out var th) && (th == "light" || th == "dark" || th == "system")) Theme = th;
            WindowWidth = Clamp(GetInt(d, "WindowWidth", WindowWidth), 600, 10000);
            WindowHeight = Clamp(GetInt(d, "WindowHeight", WindowHeight), 400, 10000);
            WindowMaximized = GetInt(d, "WindowMaximized", 0) != 0;
        }

        public static void Save()
        {
            try
            {
                Directory.CreateDirectory(UserDir);
                var sb = new StringBuilder();
                sb.AppendLine("[Settings]");
                sb.AppendLine("Language=" + Language);
                sb.AppendLine("LastRange=" + LastRange);
                sb.AppendLine("ScanPorts=" + (ScanPorts ? 1 : 0));
                sb.AppendLine("Speed=" + Speed);
                sb.AppendLine("ServicePorts=" + string.Join(",", ServicePorts));
                sb.AppendLine("CustomPorts=" + string.Join(",", CustomPorts));
                sb.AppendLine("ShowWorkgroup=" + (ShowWorkgroup ? 1 : 0));
                sb.AppendLine("Theme=" + Theme);
                sb.AppendLine("WindowWidth=" + WindowWidth);
                sb.AppendLine("WindowHeight=" + WindowHeight);
                sb.AppendLine("WindowMaximized=" + (WindowMaximized ? 1 : 0));
                File.WriteAllText(UserFile, sb.ToString(), new UTF8Encoding(false));
            }
            catch { /* settings are best effort */ }
        }

        private static void Read(string path, Dictionary<string, string> d)
        {
            try
            {
                if (!File.Exists(path)) return;
                foreach (var raw in File.ReadAllLines(path, Encoding.UTF8))
                {
                    var line = raw.Trim();
                    if (line.Length == 0 || line[0] == ';' || line[0] == '#' || line[0] == '[') continue;
                    int eq = line.IndexOf('=');
                    if (eq <= 0) continue;
                    d[line.Substring(0, eq).Trim()] = line.Substring(eq + 1).Trim();
                }
            }
            catch { /* ignore unreadable file */ }
        }

        private static int GetInt(Dictionary<string, string> d, string key, int def)
            => d.TryGetValue(key, out var s) && int.TryParse(s, out var v) ? v : def;

        /// <summary>"8123, 9090 8443" -> [8123, 9090, 8443]; ignores anything that is not a valid TCP port.</summary>
        public static List<int> ParsePorts(string text)
        {
            var list = new List<int>();
            foreach (var part in (text ?? "").Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries))
                if (int.TryParse(part.Trim(), out var p) && p > 0 && p <= 65535 && !list.Contains(p)) list.Add(p);
            return list;
        }

        private static int Clamp(int v, int min, int max) => v < min ? min : v > max ? max : v;
    }
}
