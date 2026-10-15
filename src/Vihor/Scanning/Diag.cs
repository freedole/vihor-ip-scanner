using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace Vihor.Scanning
{
    /// <summary>
    /// In-memory diagnostic log of the last scan (what was sent, what answered, timings).
    /// Help → Save diagnostic log writes it to a text file for troubleshooting.
    /// </summary>
    internal static class Diag
    {
        private static ConcurrentQueue<string> lines = new ConcurrentQueue<string>();
        private static readonly Stopwatch Clock = new Stopwatch();
        private const int MaxLines = 50000;

        public static void Reset(string header)
        {
            lines = new ConcurrentQueue<string>();
            Clock.Restart();
            Log(header);
        }

        public static void Log(string text)
        {
            if (lines.Count >= MaxLines) return;
            lines.Enqueue($"{Clock.ElapsedMilliseconds,7} ms  {text}");
        }

        public static void Host(uint ip, string text) => Log($"{IpRange.ToStr(ip),-15}  {text}");

        public static bool IsEmpty => lines.IsEmpty;

        public static void Save(string path)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"{AppInfo.Name} {AppInfo.Version} — diagnostic log");
            sb.AppendLine($"Windows {Environment.OSVersion.Version}, .NET {Environment.Version}, {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine("Local adapters:");
            foreach (var s in IpRange.GetLocalSubnets())
                sb.AppendLine($"  {s.Label}  LAN={s.IsLan} gateway={s.HasGateway}");
            sb.AppendLine(new string('-', 80));
            foreach (var l in lines.ToArray()) sb.AppendLine(l);
            File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
        }
    }
}
