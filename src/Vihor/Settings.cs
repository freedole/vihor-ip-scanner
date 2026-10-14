using System;
using System.Collections.Generic;
using System.IO;
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
        public static int PingTimeoutMs = 700;
        public static int Threads = 128;
        public static bool ScanPorts = true;
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
            if (d.TryGetValue("LastRange", out var r)) LastRange = r;
            PingTimeoutMs = Clamp(GetInt(d, "PingTimeoutMs", PingTimeoutMs), 100, 5000);
            Threads = Clamp(GetInt(d, "Threads", Threads), 8, 512);
            ScanPorts = GetInt(d, "ScanPorts", ScanPorts ? 1 : 0) != 0;
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
                sb.AppendLine("PingTimeoutMs=" + PingTimeoutMs);
                sb.AppendLine("Threads=" + Threads);
                sb.AppendLine("ScanPorts=" + (ScanPorts ? 1 : 0));
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

        private static int Clamp(int v, int min, int max) => v < min ? min : v > max ? max : v;
    }
}
