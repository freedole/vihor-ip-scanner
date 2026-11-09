using System;
using System.Drawing;
using System.Net;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Vihor
{
    /// <summary>Continuous ping or traceroute in a small window, with live statistics. Several can be open at once.</summary>
    internal sealed class PingForm : Form
    {
        private readonly TextBox output = new TextBox();
        private readonly Label stats = new Label();
        private readonly Button btnStop = new Button();
        private readonly Button btnCopy = new Button();
        private readonly CancellationTokenSource cts = new CancellationTokenSource();
        private readonly string target, title;
        private readonly bool trace;

        private int sent, received;
        private long min = long.MaxValue, max, sum;

        public PingForm(string ip, string name, bool tracert)
        {
            target = ip;
            trace = tracert;
            title = string.IsNullOrEmpty(name) || name == ip ? ip : $"{name} ({ip})";
            Text = (tracert ? Lang.T("ctx.tracert") : Lang.T("ctx.ping")) + " - " + title;
            Icon = MainForm.LoadIcon();
            AutoScaleMode = AutoScaleMode.Dpi;
            Font = SystemFonts.MessageBoxFont;
            Size = new Size(LogicalToDeviceUnits(620), LogicalToDeviceUnits(420));
            StartPosition = FormStartPosition.CenterParent;
            ShowInTaskbar = true;

            output.Multiline = true;
            output.ReadOnly = true;
            output.ScrollBars = ScrollBars.Vertical;
            output.Dock = DockStyle.Fill;
            output.Font = new Font("Consolas", Font.SizeInPoints);
            output.BorderStyle = BorderStyle.None;

            var bottom = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(LogicalToDeviceUnits(8)) };
            btnStop.Text = Lang.T("btn.stop");
            btnStop.AutoSize = true;
            btnStop.Click += (s, e) => Stop();
            btnCopy.Text = Lang.T("ctx.copy");
            btnCopy.AutoSize = true;
            btnCopy.Click += (s, e) => HostActions.Copy(output.Text);
            stats.AutoSize = true;
            stats.Margin = new Padding(0, LogicalToDeviceUnits(6), LogicalToDeviceUnits(16), 0);
            bottom.Controls.Add(btnStop);
            bottom.Controls.Add(btnCopy);
            bottom.Controls.Add(stats);

            Controls.Add(output);
            Controls.Add(bottom);
            Theme.Apply(this);

            Shown += async (s, e) => { if (trace) await RunTrace(); else await RunPing(); };
            FormClosing += (s, e) => cts.Cancel();
            KeyPreview = true;
            KeyDown += (s, e) => { if (e.KeyCode == Keys.Escape) { if (!cts.IsCancellationRequested) Stop(); else Close(); } };
        }

        private void Stop()
        {
            cts.Cancel();
            btnStop.Enabled = false;
        }

        private void Line(string s)
        {
            if (IsDisposed) return;
            output.AppendText(s + Environment.NewLine);
        }

        private async Task RunPing()
        {
            Line(Lang.T("ping.start", title));
            using var p = new Ping();
            var addr = IPAddress.Parse(target);
            while (!cts.IsCancellationRequested)
            {
                sent++;
                PingReply r = null;
                try { r = await p.SendPingAsync(addr, 1000); } catch { }
                if (cts.IsCancellationRequested || IsDisposed) break;
                if (r != null && r.Status == IPStatus.Success)
                {
                    received++;
                    long t = r.RoundtripTime;
                    min = Math.Min(min, t); max = Math.Max(max, t); sum += t;
                    Line($"{DateTime.Now:HH:mm:ss}  {target}  {(t == 0 ? "<1" : t.ToString())} ms  TTL={r.Options?.Ttl}");
                }
                else Line($"{DateTime.Now:HH:mm:ss}  {Lang.T("ping.timeout")}{(r != null && r.Status != IPStatus.TimedOut ? " (" + r.Status + ")" : "")}");
                UpdateStats();
                try { await Task.Delay(1000, cts.Token); } catch { break; }
            }
            if (!IsDisposed) Line(Lang.T("ping.stopped"));
        }

        private void UpdateStats()
        {
            int lost = sent - received;
            stats.Text = received > 0
                ? Lang.T("ping.stats", sent, received, lost, lost * 100 / Math.Max(1, sent), min, sum / received, max)
                : Lang.T("ping.stats_none", sent, lost);
        }

        private async Task RunTrace()
        {
            Line(Lang.T("tracert.start", title));
            using var p = new Ping();
            var addr = IPAddress.Parse(target);
            var buffer = new byte[32];
            for (int ttl = 1; ttl <= 30 && !cts.IsCancellationRequested; ttl++)
            {
                PingReply r = null;
                var sw = System.Diagnostics.Stopwatch.StartNew();
                try { r = await p.SendPingAsync(addr, 2000, buffer, new PingOptions(ttl, true)); } catch { }
                sw.Stop();
                if (cts.IsCancellationRequested || IsDisposed) break;
                if (r == null || r.Status == IPStatus.TimedOut) { Line($"{ttl,3}   *"); continue; }
                var hop = r.Address?.ToString() ?? "?";
                string name = "";
                try
                {
                    var t = Dns.GetHostEntryAsync(r.Address);
                    if (await Task.WhenAny(t, Task.Delay(1500)) == t && t.Status == TaskStatus.RanToCompletion && t.Result.HostName != hop) name = "  " + t.Result.HostName;
                }
                catch { }
                Line($"{ttl,3}   {sw.ElapsedMilliseconds,4} ms   {hop}{name}");
                if (r.Status == IPStatus.Success) { Line(Lang.T("tracert.done")); break; }
            }
            if (!IsDisposed) btnStop.Enabled = false;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) cts.Dispose();
            base.Dispose(disposing);
        }
    }
}
