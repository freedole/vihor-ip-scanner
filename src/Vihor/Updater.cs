using System;
using System.IO;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Vihor
{
    /// <summary>
    /// Once a day asks GitHub for the latest release (installer / portable only; the Store updates by itself).
    /// Nothing is downloaded or installed: the user only sees a link in the status bar.
    /// </summary>
    internal static class Updater
    {
        public static bool IsStore => AppDomain.CurrentDomain.BaseDirectory.IndexOf(@"\WindowsApps\", StringComparison.OrdinalIgnoreCase) >= 0;

        public static string ReleasesUrl => AppInfo.Repository + "/releases/latest";

        /// <summary>Returns the newer version ("0.7.0") or null.</summary>
        public static Task<string> CheckAsync(bool force) => Task.Run(() =>
        {
            try
            {
                if (IsStore || (!Settings.CheckUpdates && !force)) return null;
                var today = DateTime.Now.ToString("yyyy-MM-dd");
                if (!force && Settings.LastUpdateCheck == today) return null;
                Settings.LastUpdateCheck = today;

                ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
                var req = (HttpWebRequest)WebRequest.Create("https://api.github.com/repos/freedole/vihor-ip-scanner/releases/latest");
                req.UserAgent = "VihorIPScanner/" + AppInfo.Version;
                req.Accept = "application/vnd.github+json";
                req.Timeout = 8000;
                using var resp = req.GetResponse();
                using var r = new StreamReader(resp.GetResponseStream());
                var m = Regex.Match(r.ReadToEnd(), "\"tag_name\"\\s*:\\s*\"v?([0-9.]+)\"");
                if (!m.Success) return null;
                return Version.TryParse(m.Groups[1].Value, out var latest) && Version.TryParse(AppInfo.Version, out var mine) && latest > mine
                    ? m.Groups[1].Value : null;
            }
            catch { return null; }
        });
    }
}
