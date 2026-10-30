using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using Vihor.Scanning;

namespace Vihor
{
    /// <summary>
    /// Command line mode, e.g. for Task Scheduler:
    ///   VihorIPScanner.exe /scan 192.168.1.1-254 /export C:\Reports\lan.csv [/speed 1-5] [/ranges]
    /// Uses the services and ports from Options. Compares with the previous scan of the same range
    /// (Status column: new / missing) and remembers this one, just like the window.
    /// </summary>
    internal static class Cli
    {
        [DllImport("kernel32.dll")]
        private static extern bool AttachConsole(int processId);

        private static bool Is(string arg, string name) =>
            arg.Length > 1 && (arg[0] == '/' || arg[0] == '-') && arg.TrimStart('/', '-').Equals(name, StringComparison.OrdinalIgnoreCase);

        public static bool IsCli(string[] args) => args.Any(a => Is(a, "scan") || Is(a, "?") || Is(a, "help"));

        public static int Run(string[] args)
        {
            AttachConsole(-1); // print into the console we were started from (no-op from Task Scheduler)
            try { Console.OutputEncoding = System.Text.Encoding.UTF8; } catch { }
            Console.WriteLine();

            string range = null, export = null;
            int? speed = null;
            bool ranges = false;
            for (int i = 0; i < args.Length; i++)
            {
                string Next() => i + 1 < args.Length ? args[++i] : null;
                if (Is(args[i], "scan")) range = Next();
                else if (Is(args[i], "export")) export = Next();
                else if (Is(args[i], "speed") && int.TryParse(Next(), out var sp)) speed = Math.Max(1, Math.Min(5, sp));
                else if (Is(args[i], "ranges")) ranges = true;
                else if (Is(args[i], "?") || Is(args[i], "help")) { Usage(); return 0; }
                else { Console.WriteLine(Lang.T("cli.unknown", args[i])); Usage(); return 1; }
            }
            if (string.IsNullOrWhiteSpace(range)) { Usage(); return 1; }

            var targets = IpRange.Parse(range, out var err);
            if (err != null || targets.Count == 0)
            {
                Console.WriteLine(err == "too_many" ? Lang.T("msg.too_many", IpRange.MaxAddresses) : Lang.T("msg.bad_range", err ?? range));
                return 1;
            }

            var profile = ScanProfile.FromSettings();
            if (speed.HasValue) profile.Speed = speed.Value;
            var opt = ScanOptions.From(profile, rescan: ranges);
            var scanner = new Scanner(opt);
            var found = new ConcurrentBag<HostInfo>();
            scanner.HostFound += h => found.Add(h);

            Console.WriteLine($"{AppInfo.Name} {AppInfo.Version} - {range} ({targets.Count})");
            var clock = System.Diagnostics.Stopwatch.StartNew();
            try { scanner.RunAsync(targets, CancellationToken.None).GetAwaiter().GetResult(); }
            catch (Exception ex) { Console.WriteLine(ex.Message); return 2; }

            var hosts = found.OrderBy(h => h.Ip).ToList();
            var history = ScanHistory.Load(range);
            foreach (var h in hosts) h.IsNew = history.IsNew(h);
            var missing = history.Missing(hosts);
            ScanHistory.Save(range, hosts);

            foreach (var h in hosts.Concat(missing).OrderBy(h => h.Ip))
            {
                var status = h.IsMissing ? Lang.T("status.missing") : h.IsNew ? Lang.T("status.new") : "";
                Console.WriteLine(string.Join("  ", new[]
                {
                    h.IpText.PadRight(15), Cut(h.Name, 28).PadRight(28), Cut(h.Vendor, 24).PadRight(24),
                    Cut(h.IsMissing ? "" : h.ServicesText, 40), status
                }.Where(x => x != null)).TrimEnd());
            }
            Console.WriteLine();
            Console.WriteLine(Lang.T("status.done", hosts.Count, targets.Count, clock.Elapsed.TotalSeconds.ToString("0.0")) +
                              (history.HasPrevious ? " · " + Lang.T("status.compare", hosts.Count(h => h.IsNew), missing.Count, history.When?.ToString("g")) : ""));

            if (!string.IsNullOrWhiteSpace(export))
            {
                try
                {
                    var path = Path.GetFullPath(export);
                    var rows = hosts.Concat(missing).OrderBy(h => h.Ip).ToList();
                    if (path.EndsWith(".html", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".htm", StringComparison.OrdinalIgnoreCase))
                        Export.Html(path, rows, range);
                    else Export.Csv(path, rows);
                    Console.WriteLine(Lang.T("cli.saved", path));
                }
                catch (Exception ex) { Console.WriteLine(ex.Message); return 2; }
            }
            return 0;
        }

        private static string Cut(string s, int max) => string.IsNullOrEmpty(s) ? "" : s.Length <= max ? s : s.Substring(0, max - 1) + "…";

        private static void Usage()
        {
            Console.WriteLine($"{AppInfo.Name} {AppInfo.Version}");
            Console.WriteLine();
            Console.WriteLine("  VihorIPScanner.exe /scan <range> [/export <file.csv|file.html>] [/speed 1-5] [/ranges]");
            Console.WriteLine();
            Console.WriteLine("  /scan    " + Lang.T("cli.help_scan"));
            Console.WriteLine("  /export  " + Lang.T("cli.help_export"));
            Console.WriteLine("  /speed   " + Lang.T("cli.help_speed"));
            Console.WriteLine("  /ranges  " + Lang.T("cli.help_ranges"));
            Console.WriteLine();
            Console.WriteLine("  VihorIPScanner.exe /scan 192.168.1.1-254 /export C:\\Reports\\lan.csv");
        }
    }
}
