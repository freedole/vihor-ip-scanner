using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using Vihor.Scanning;

namespace Vihor
{
    internal sealed class MainForm : Form
    {
        // --- UI ---
        private readonly MenuStrip menu = new MenuStrip();
        private readonly ToolStrip tools = new ToolStrip();
        private readonly ToolStripLabel lblRange = new ToolStripLabel();
        private readonly ToolStripComboBox cmbRange = new ToolStripComboBox();
        private readonly ToolStripButton btnScan = new ToolStripButton();
        private readonly ToolStripSplitButton btnLocal = new ToolStripSplitButton();
        private readonly ToolStripButton btnExport = new ToolStripButton();
        private readonly ToolStripTextBox txtFilter = new ToolStripTextBox();
        private readonly ToolStripLabel lblFilter = new ToolStripLabel();
        private readonly HostListView list = new HostListView();
        private readonly StatusStrip status = new StatusStrip();
        private readonly ToolStripStatusLabel lblStatus = new ToolStripStatusLabel();
        private readonly ToolStripProgressBar progress = new ToolStripProgressBar();
        private readonly ContextMenuStrip ctx = new ContextMenuStrip();
        private readonly ImageList icons = new ImageList();
        private readonly System.Windows.Forms.Timer uiTimer = new System.Windows.Forms.Timer { Interval = 120 };

        // menus (kept as fields so texts can be re-applied on language change)
        private ToolStripMenuItem mFile, mExportCsv, mExportHtml, mExit;
        private ToolStripMenuItem mScan, mScanStart, mScanLocal, mScanPorts;
        private ToolStripMenuItem mSettings, mLanguage;
        private ToolStripMenuItem mHelp, mWebsite, mGithub, mAbout;

        // context menu items
        private ToolStripMenuItem cCopy, cCopyIp, cCopyName, cCopyMac, cCopyVendor, cCopyRow;
        private ToolStripMenuItem cRdp, cSsh, cHttp, cHttps, cHttpAlt, cFtp, cExplore;
        private ToolStripMenuItem cTools, cPing, cTracert, cWol, cRescan, cShutdown, cRestart, cAbort;

        // --- state ---
        private readonly List<HostInfo> hosts = new List<HostInfo>();
        private readonly ConcurrentQueue<HostInfo> incoming = new ConcurrentQueue<HostInfo>();
        private CancellationTokenSource cts;
        private int progDone, progTotal;
        private bool scanning;
        private string lastScannedRange = "";
        private readonly Stopwatch scanClock = new Stopwatch();
        private int sortColumn = 1;
        private bool sortAsc = true;

        private const int ColName = 0, ColIp = 1, ColVendor = 2, ColMac = 3, ColServices = 4, ColPing = 5;

        public MainForm()
        {
            Text = $"{AppInfo.Name} {AppInfo.Version}";
            Icon = LoadIcon();
            StartPosition = FormStartPosition.CenterScreen;
            AutoScaleMode = AutoScaleMode.Dpi;
            Font = SystemFonts.MessageBoxFont;
            Size = new Size(Settings.WindowWidth, Settings.WindowHeight);
            MinimumSize = new Size(700, 420);
            if (Settings.WindowMaximized) WindowState = FormWindowState.Maximized;
            KeyPreview = true;

            BuildIcons();
            BuildMenu();
            BuildToolbar();
            BuildList();
            BuildContextMenu();
            BuildStatus();

            Controls.Add(list);
            Controls.Add(tools);
            Controls.Add(menu);
            Controls.Add(status);
            MainMenuStrip = menu;

            uiTimer.Tick += (s, e) => FlushIncoming();
            KeyDown += OnKeyDown;
            Shown += (s, e) => { _ = OuiDb.LoadAsync(); cmbRange.Focus(); };
            FormClosing += OnClosing;

            LoadRanges();
            ApplyTexts();
        }

        // ------------------------------------------------------------------ build UI

        private void BuildIcons()
        {
            int sz = LogicalToDeviceUnits(16);
            icons.ImageSize = new Size(sz, sz);
            icons.ColorDepth = ColorDepth.Depth32Bit;
            icons.Images.Add("alive", Dot(sz, Color.FromArgb(46, 160, 67)));
            icons.Images.Add("self", Dot(sz, Color.FromArgb(31, 111, 235)));
            icons.Images.Add("gateway", Dot(sz, Color.FromArgb(219, 130, 20)));
        }

        private static Bitmap Dot(int sz, Color c)
        {
            var b = new Bitmap(sz, sz);
            using var g = Graphics.FromImage(b);
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            int pad = Math.Max(2, sz / 5);
            using var br = new SolidBrush(c);
            g.FillEllipse(br, pad, pad, sz - 2 * pad, sz - 2 * pad);
            return b;
        }

        private void BuildMenu()
        {
            mExportCsv = new ToolStripMenuItem("", null, (s, e) => DoExport(false));
            mExportHtml = new ToolStripMenuItem("", null, (s, e) => DoExport(true));
            mExit = new ToolStripMenuItem("", null, (s, e) => Close());
            mFile = new ToolStripMenuItem("", null, mExportCsv, mExportHtml, new ToolStripSeparator(), mExit);

            mScanStart = new ToolStripMenuItem("", null, (s, e) => ToggleScan()) { ShortcutKeys = Keys.F5 };
            mScanLocal = new ToolStripMenuItem();
            mScanLocal.DropDownItems.Add(new ToolStripMenuItem("…"));
            mScanLocal.DropDownOpening += (s, e) => FillAdapterMenu(mScanLocal.DropDownItems);
            mScanPorts = new ToolStripMenuItem("", null, (s, e) => { Settings.ScanPorts = !Settings.ScanPorts; mScanPorts.Checked = Settings.ScanPorts; })
            { Checked = Settings.ScanPorts };
            mScan = new ToolStripMenuItem("", null, mScanStart, mScanLocal, new ToolStripSeparator(), mScanPorts);

            mLanguage = new ToolStripMenuItem();
            foreach (var l in Lang.Available)
            {
                var item = new ToolStripMenuItem(l.NativeName) { Tag = l.Code };
                item.Click += (s, e) => SetLanguage((string)((ToolStripMenuItem)s).Tag);
                mLanguage.DropDownItems.Add(item);
            }
            mSettings = new ToolStripMenuItem("", null, mLanguage);

            mWebsite = new ToolStripMenuItem("", null, (s, e) => OpenUrl(AppInfo.Website));
            mGithub = new ToolStripMenuItem("", null, (s, e) => OpenUrl(AppInfo.Repository));
            mAbout = new ToolStripMenuItem("", null, (s, e) => { using var a = new AboutForm(); a.ShowDialog(this); });
            mHelp = new ToolStripMenuItem("", null, mWebsite, mGithub, new ToolStripSeparator(), mAbout);

            menu.Items.AddRange(new ToolStripItem[] { mFile, mScan, mSettings, mHelp });
        }

        private void BuildToolbar()
        {
            tools.GripStyle = ToolStripGripStyle.Hidden;
            tools.Padding = new Padding(LogicalToDeviceUnits(6), LogicalToDeviceUnits(4), LogicalToDeviceUnits(6), LogicalToDeviceUnits(4));
            tools.ImageScalingSize = new Size(LogicalToDeviceUnits(16), LogicalToDeviceUnits(16));

            cmbRange.AutoSize = false;
            cmbRange.Width = LogicalToDeviceUnits(320);
            cmbRange.DropDownStyle = ComboBoxStyle.DropDown;
            cmbRange.Font = new Font(FontFamily.GenericMonospace, Font.SizeInPoints);
            cmbRange.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; if (!scanning) StartScan(); } };

            btnScan.DisplayStyle = ToolStripItemDisplayStyle.Text;
            btnScan.Font = new Font(Font, FontStyle.Bold);
            btnScan.BackColor = Color.FromArgb(31, 111, 235);
            btnScan.ForeColor = Color.White;
            btnScan.Margin = new Padding(LogicalToDeviceUnits(6), 0, 0, 0);
            btnScan.Padding = new Padding(LogicalToDeviceUnits(10), LogicalToDeviceUnits(2), LogicalToDeviceUnits(10), LogicalToDeviceUnits(2));
            btnScan.Click += (s, e) => ToggleScan();

            btnLocal.DisplayStyle = ToolStripItemDisplayStyle.Text;
            btnLocal.ButtonClick += (s, e) => cmbRange.Text = IpRange.DefaultRange(IpRange.GetLocalSubnets());
            btnLocal.DropDownOpening += (s, e) => FillAdapterMenu(btnLocal.DropDownItems);
            btnLocal.DropDownItems.Add(new ToolStripMenuItem("…")); // placeholder so the arrow is active

            btnExport.DisplayStyle = ToolStripItemDisplayStyle.Text;
            btnExport.Click += (s, e) => DoExport(false);

            txtFilter.AutoSize = false;
            txtFilter.Width = LogicalToDeviceUnits(180);
            txtFilter.TextChanged += (s, e) => RebuildList();

            tools.Items.AddRange(new ToolStripItem[]
            {
                lblRange, cmbRange, btnScan, btnLocal, new ToolStripSeparator(), btnExport,
                new ToolStripSeparator(), lblFilter, txtFilter
            });
        }

        private void BuildList()
        {
            list.Dock = DockStyle.Fill;
            list.View = View.Details;
            list.FullRowSelect = true;
            list.HideSelection = false;
            list.MultiSelect = true;
            list.ShowItemToolTips = true;
            list.SmallImageList = icons;
            list.BorderStyle = BorderStyle.None;
            list.Columns.Add("", LogicalToDeviceUnits(190));
            list.Columns.Add("", LogicalToDeviceUnits(115));
            list.Columns.Add("", LogicalToDeviceUnits(170));
            list.Columns.Add("", LogicalToDeviceUnits(130));
            list.Columns.Add("", LogicalToDeviceUnits(150));
            list.Columns.Add("", LogicalToDeviceUnits(65), HorizontalAlignment.Right);
            list.ColumnClick += (s, e) =>
            {
                if (e.Column == sortColumn) sortAsc = !sortAsc; else { sortColumn = e.Column; sortAsc = true; }
                RebuildList();
            };
            list.DoubleClick += (s, e) => DefaultAction();
            list.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter) { DefaultAction(); e.Handled = true; }
                if (e.Control && e.KeyCode == Keys.C) { HostActions.Copy(string.Join(Environment.NewLine, Selected().Select(h => h.IpText))); e.Handled = true; }
                if (e.Control && e.KeyCode == Keys.A) { foreach (ListViewItem i in list.Items) i.Selected = true; e.Handled = true; }
            };
            list.ContextMenuStrip = ctx;
        }

        private void BuildContextMenu()
        {
            HostInfo H() => Selected().FirstOrDefault();

            cCopyIp = new ToolStripMenuItem("", null, (s, e) => HostActions.Copy(string.Join(Environment.NewLine, Selected().Select(h => h.IpText))));
            cCopyName = new ToolStripMenuItem("", null, (s, e) => HostActions.Copy(string.Join(Environment.NewLine, Selected().Select(h => h.Name))));
            cCopyMac = new ToolStripMenuItem("", null, (s, e) => HostActions.Copy(string.Join(Environment.NewLine, Selected().Select(h => h.MacText))));
            cCopyVendor = new ToolStripMenuItem("", null, (s, e) => HostActions.Copy(string.Join(Environment.NewLine, Selected().Select(h => h.Vendor))));
            cCopyRow = new ToolStripMenuItem("", null, (s, e) => HostActions.Copy(string.Join(Environment.NewLine,
                Selected().Select(h => string.Join("\t", h.IpText, h.Name, h.Vendor, h.MacText, h.ServicesText)))));
            cCopy = new ToolStripMenuItem("", null, cCopyIp, cCopyName, cCopyMac, cCopyVendor, new ToolStripSeparator(), cCopyRow);

            cRdp = new ToolStripMenuItem("", null, (s, e) => HostActions.Rdp(H()));
            cSsh = new ToolStripMenuItem("", null, (s, e) => HostActions.Ssh(H()));
            cHttp = new ToolStripMenuItem("", null, (s, e) => HostActions.Http(H()));
            cHttps = new ToolStripMenuItem("", null, (s, e) => HostActions.Https(H()));
            cHttpAlt = new ToolStripMenuItem("", null, (s, e) => HostActions.Http(H(), 8080));
            cFtp = new ToolStripMenuItem("", null, (s, e) => HostActions.Ftp(H()));
            cExplore = new ToolStripMenuItem("", null, (s, e) => HostActions.Explore(H()));

            cPing = new ToolStripMenuItem("", null, (s, e) => HostActions.Ping(H()));
            cTracert = new ToolStripMenuItem("", null, (s, e) => HostActions.Tracert(H()));
            cWol = new ToolStripMenuItem("", null, (s, e) => { foreach (var h in Selected()) HostActions.WakeOnLan(h); });
            cShutdown = new ToolStripMenuItem("", null, (s, e) => HostActions.Shutdown(H(), false));
            cRestart = new ToolStripMenuItem("", null, (s, e) => HostActions.Shutdown(H(), true));
            cAbort = new ToolStripMenuItem("", null, (s, e) => HostActions.AbortShutdown(H()));
            cTools = new ToolStripMenuItem("", null, cPing, cTracert, cWol, new ToolStripSeparator(), cShutdown, cRestart, cAbort);

            cRescan = new ToolStripMenuItem("", null, async (s, e) => await RescanSelected());

            ctx.Items.AddRange(new ToolStripItem[]
            {
                cRdp, cSsh, cHttp, cHttps, cHttpAlt, cFtp, cExplore, new ToolStripSeparator(),
                cCopy, cTools, new ToolStripSeparator(), cRescan
            });

            ctx.Opening += (s, e) =>
            {
                var sel = Selected().ToList();
                if (sel.Count == 0) { e.Cancel = true; return; }
                var h = sel[0];
                void Mark(ToolStripMenuItem i, bool detected) => i.Font = new Font(ctx.Font, detected ? FontStyle.Bold : FontStyle.Regular);
                Mark(cRdp, h.Has(Service.Rdp));
                Mark(cSsh, h.Has(Service.Ssh));
                Mark(cHttp, h.Has(Service.Http));
                Mark(cHttps, h.Has(Service.Https));
                Mark(cExplore, h.Has(Service.Smb));
                Mark(cFtp, h.Has(Service.Ftp));
                cHttpAlt.Visible = h.Has(Service.HttpAlt);
                cFtp.Visible = h.Has(Service.Ftp);
                cWol.Enabled = sel.Any(x => x.Mac != null);
                cRescan.Enabled = !scanning;
                bool single = sel.Count == 1;
                foreach (var i in new[] { cRdp, cSsh, cHttp, cHttps, cHttpAlt, cFtp, cExplore, cPing, cTracert, cShutdown, cRestart, cAbort })
                    i.Enabled = single;
            };
        }

        private void BuildStatus()
        {
            status.SizingGrip = true;
            lblStatus.Spring = true;
            lblStatus.TextAlign = ContentAlignment.MiddleLeft;
            progress.Size = new Size(LogicalToDeviceUnits(200), LogicalToDeviceUnits(16));
            progress.Visible = false;
            var link = new ToolStripStatusLabel("cyber-security.ba") { IsLink = true, Alignment = ToolStripItemAlignment.Right };
            link.Click += (s, e) => OpenUrl(AppInfo.Website);
            status.Items.AddRange(new ToolStripItem[] { lblStatus, progress, link });
        }

        // ------------------------------------------------------------------ texts

        private void ApplyTexts()
        {
            mFile.Text = Lang.T("menu.file");
            mExportCsv.Text = Lang.T("menu.export_csv");
            mExportHtml.Text = Lang.T("menu.export_html");
            mExit.Text = Lang.T("menu.exit");
            mScan.Text = Lang.T("menu.scan");
            mScanStart.Text = scanning ? Lang.T("btn.stop") : Lang.T("btn.scan");
            mScanLocal.Text = Lang.T("btn.my_network");
            mScanPorts.Text = Lang.T("menu.scan_ports");
            mSettings.Text = Lang.T("menu.settings");
            mLanguage.Text = Lang.T("menu.language");
            foreach (ToolStripMenuItem i in mLanguage.DropDownItems) i.Checked = (string)i.Tag == Lang.Code;
            mHelp.Text = Lang.T("menu.help");
            mWebsite.Text = Lang.T("menu.website");
            mGithub.Text = Lang.T("menu.github");
            mAbout.Text = Lang.T("menu.about");

            lblRange.Text = Lang.T("label.range");
            btnScan.Text = scanning ? Lang.T("btn.stop") : Lang.T("btn.scan");
            btnScan.BackColor = scanning ? Color.FromArgb(207, 34, 46) : Color.FromArgb(31, 111, 235);
            btnLocal.Text = Lang.T("btn.my_network");
            btnLocal.ToolTipText = Lang.T("tip.my_network");
            btnExport.Text = Lang.T("btn.export");
            lblFilter.Text = Lang.T("label.filter");
            cmbRange.ToolTipText = Lang.T("tip.range");

            list.Columns[ColName].Text = Lang.T("col.name");
            list.Columns[ColIp].Text = Lang.T("col.ip");
            list.Columns[ColVendor].Text = Lang.T("col.vendor");
            list.Columns[ColMac].Text = Lang.T("col.mac");
            list.Columns[ColServices].Text = Lang.T("col.services");
            list.Columns[ColPing].Text = Lang.T("col.ping");

            cCopy.Text = Lang.T("ctx.copy");
            cCopyIp.Text = Lang.T("col.ip");
            cCopyName.Text = Lang.T("col.name");
            cCopyMac.Text = Lang.T("col.mac");
            cCopyVendor.Text = Lang.T("col.vendor");
            cCopyRow.Text = Lang.T("ctx.copy_row");
            cRdp.Text = Lang.T("ctx.rdp");
            cSsh.Text = Lang.T("ctx.ssh");
            cHttp.Text = Lang.T("ctx.http");
            cHttps.Text = Lang.T("ctx.https");
            cHttpAlt.Text = Lang.T("ctx.http_alt");
            cFtp.Text = Lang.T("ctx.ftp");
            cExplore.Text = Lang.T("ctx.explore");
            cTools.Text = Lang.T("ctx.tools");
            cPing.Text = Lang.T("ctx.ping");
            cTracert.Text = Lang.T("ctx.tracert");
            cWol.Text = Lang.T("ctx.wol");
            cShutdown.Text = Lang.T("ctx.shutdown");
            cRestart.Text = Lang.T("ctx.restart");
            cAbort.Text = Lang.T("ctx.abort_shutdown");
            cRescan.Text = Lang.T("ctx.rescan");

            UpdateStatus();
            RebuildList();
        }

        private void SetLanguage(string code)
        {
            Settings.Language = code;
            Lang.Load(code);
            Settings.Save();
            ApplyTexts();
        }

        // ------------------------------------------------------------------ scanning

        private void LoadRanges()
        {
            var subnets = IpRange.GetLocalSubnets();
            var def = IpRange.DefaultRange(subnets);
            cmbRange.Items.Add(def);
            foreach (var s in subnets)
                if (!cmbRange.Items.Contains(s.ScanRange)) cmbRange.Items.Add(s.ScanRange);
            // Always start with this computer's LAN; the previously scanned range stays in the history
            if (!string.IsNullOrWhiteSpace(Settings.LastRange) && !cmbRange.Items.Contains(Settings.LastRange))
                cmbRange.Items.Add(Settings.LastRange);
            cmbRange.Text = def;
        }

        /// <summary>"My network" menu: all LAN adapters together, then every adapter on its own.</summary>
        private void FillAdapterMenu(ToolStripItemCollection items)
        {
            items.Clear();
            var subnets = IpRange.GetLocalSubnets();
            if (subnets.Count == 0)
            {
                items.Add(new ToolStripMenuItem(Lang.T("menu.no_adapters")) { Enabled = false });
                return;
            }

            var lan = subnets.Where(s => s.IsLan).ToList();
            if (lan.Count > 0)
            {
                var all = IpRange.JoinRanges(lan);
                var allItem = new ToolStripMenuItem($"{Lang.T("menu.all_lan")}   {all}") { Font = new Font(ctx.Font, FontStyle.Bold) };
                allItem.Click += (s, e) => cmbRange.Text = all;
                items.Add(allItem);
                items.Add(new ToolStripSeparator());
            }

            bool virtualHeader = false;
            foreach (var sub in subnets)
            {
                if (!sub.IsLan && !virtualHeader)
                {
                    items.Add(new ToolStripSeparator());
                    items.Add(new ToolStripMenuItem(Lang.T("menu.virtual_adapters")) { Enabled = false });
                    virtualHeader = true;
                }
                var range = sub.ScanRange;
                var item = new ToolStripMenuItem(sub.Label) { ToolTipText = range };
                item.Click += (s, e) => cmbRange.Text = range;
                items.Add(item);
            }
        }

        private void ToggleScan()
        {
            if (scanning) cts?.Cancel();
            else StartScan();
        }

        private async void StartScan()
        {
            var range = cmbRange.Text.Trim();
            var targets = IpRange.Parse(range, out var err);
            if (err != null || targets.Count == 0)
            {
                MessageBox.Show(err == "too_many" ? Lang.T("msg.too_many", IpRange.MaxAddresses) : Lang.T("msg.bad_range", err ?? range),
                    AppInfo.Name, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            Settings.LastRange = range;
            lastScannedRange = range;
            if (!cmbRange.Items.Contains(range)) cmbRange.Items.Insert(0, range);

            hosts.Clear();
            while (incoming.TryDequeue(out _)) { }
            list.Items.Clear();

            scanning = true;
            progDone = 0;
            progTotal = targets.Count;
            progress.Value = 0;
            progress.Maximum = Math.Max(1, targets.Count);
            progress.Visible = true;
            ApplyTexts();
            scanClock.Restart();
            uiTimer.Start();

            cts = new CancellationTokenSource();
            var scanner = new Scanner(new ScanOptions
            {
                PingTimeoutMs = Settings.PingTimeoutMs,
                Threads = Settings.Threads,
                ScanPorts = Settings.ScanPorts
            });
            scanner.HostFound += h => incoming.Enqueue(h);
            scanner.Progress += (d, t) => { progDone = d; };

            try
            {
                await System.Threading.Tasks.Task.Run(() => scanner.RunAsync(targets, cts.Token));
            }
            catch (OperationCanceledException) { }
            finally
            {
                scanClock.Stop();
                scanning = false;
                uiTimer.Stop();
                FlushIncoming();
                progress.Visible = false;
                cts.Dispose();
                cts = null;
                ApplyTexts();
            }
        }

        private async System.Threading.Tasks.Task RescanSelected()
        {
            var sel = Selected().ToList();
            if (sel.Count == 0 || scanning) return;
            var scanner = new Scanner(new ScanOptions { PingTimeoutMs = Settings.PingTimeoutMs, ScanPorts = Settings.ScanPorts });
            lblStatus.Text = Lang.T("status.rescanning");
            foreach (var old in sel)
            {
                var fresh = await System.Threading.Tasks.Task.Run(() => scanner.ScanHostAsync(old.Ip, CancellationToken.None));
                int idx = hosts.FindIndex(h => h.Ip == old.Ip);
                if (idx < 0) continue;
                if (fresh != null) hosts[idx] = fresh; else hosts.RemoveAt(idx);
            }
            RebuildList();
            UpdateStatus();
        }

        private void FlushIncoming()
        {
            bool added = false;
            while (incoming.TryDequeue(out var h)) { hosts.Add(h); added = true; }
            if (added) RebuildList();
            if (scanning) progress.Value = Math.Min(progress.Maximum, progDone);
            UpdateStatus();
        }

        private void UpdateStatus()
        {
            if (scanning)
                lblStatus.Text = Lang.T("status.scanning", progDone, progTotal, hosts.Count);
            else if (progTotal > 0)
                lblStatus.Text = Lang.T("status.done", hosts.Count, progTotal, scanClock.Elapsed.TotalSeconds.ToString("0.0"));
            else
                lblStatus.Text = Lang.T("status.ready");
        }

        // ------------------------------------------------------------------ list

        private void RebuildList()
        {
            var filter = txtFilter.Text.Trim();
            IEnumerable<HostInfo> view = hosts;
            if (filter.Length > 0)
                view = view.Where(h =>
                    h.IpText.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    h.Name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    h.Vendor.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    h.MacText.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    h.ServicesText.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0);

            view = Sort(view);

            var selectedIps = new HashSet<uint>(Selected().Select(h => h.Ip));
            list.BeginUpdate();
            list.Items.Clear();
            foreach (var h in view)
            {
                var name = h.Name;
                var item = new ListViewItem(name) { Tag = h, ImageKey = h.IsSelf ? "self" : h.IsGateway ? "gateway" : "alive" };
                item.SubItems.Add(h.IpText);
                item.SubItems.Add(h.Vendor);
                item.SubItems.Add(h.MacText);
                item.SubItems.Add(h.ServicesText);
                item.SubItems.Add(h.PingMs < 0 ? "—" : h.PingMs == 0 ? "<1" : h.PingMs.ToString());
                item.ToolTipText = h.IsSelf ? Lang.T("tip.this_pc") : h.IsGateway ? Lang.T("tip.gateway") :
                                   string.IsNullOrEmpty(h.Workgroup) ? "" : Lang.T("col.workgroup") + ": " + h.Workgroup;
                if (selectedIps.Contains(h.Ip)) item.Selected = true;
                list.Items.Add(item);
            }
            list.EndUpdate();
        }

        private IEnumerable<HostInfo> Sort(IEnumerable<HostInfo> src)
        {
            Func<HostInfo, IComparable> key = sortColumn switch
            {
                ColName => h => string.IsNullOrEmpty(h.Name) ? "￿" : h.Name.ToLowerInvariant(),
                ColVendor => h => string.IsNullOrEmpty(h.Vendor) ? "￿" : h.Vendor.ToLowerInvariant(),
                ColMac => h => h.MacText,
                ColServices => h => (int)h.Services,
                ColPing => h => h.PingMs < 0 ? long.MaxValue : h.PingMs,
                _ => h => h.Ip,
            };
            var ordered = sortAsc ? src.OrderBy(key).ThenBy(h => h.Ip) : src.OrderByDescending(key).ThenBy(h => h.Ip);
            return ordered;
        }

        private IEnumerable<HostInfo> Selected()
        {
            foreach (ListViewItem i in list.SelectedItems)
                if (i.Tag is HostInfo h) yield return h;
        }

        private void DefaultAction()
        {
            var h = Selected().FirstOrDefault();
            if (h == null) return;
            if (h.Has(Service.Rdp)) HostActions.Rdp(h);
            else if (h.Has(Service.Https)) HostActions.Https(h);
            else if (h.Has(Service.Http)) HostActions.Http(h);
            else if (h.Has(Service.HttpAlt)) HostActions.Http(h, 8080);
            else if (h.Has(Service.Smb)) HostActions.Explore(h);
            else if (h.Has(Service.Ssh)) HostActions.Ssh(h);
            else HostActions.Ping(h);
        }

        // ------------------------------------------------------------------ misc

        private void DoExport(bool html)
        {
            if (hosts.Count == 0) { MessageBox.Show(Lang.T("msg.nothing_to_export"), AppInfo.Name); return; }
            using var dlg = new SaveFileDialog
            {
                Filter = html ? "HTML (*.html)|*.html" : "CSV (*.csv)|*.csv",
                FileName = $"vihor-scan-{DateTime.Now:yyyyMMdd-HHmm}" + (html ? ".html" : ".csv")
            };
            if (dlg.ShowDialog(this) != DialogResult.OK) return;
            var rows = Sort(hosts).ToList();
            try
            {
                if (html) Export.Html(dlg.FileName, rows, lastScannedRange);
                else Export.Csv(dlg.FileName, rows);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, AppInfo.Name, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.F5) { ToggleScan(); e.Handled = true; }
            if (e.KeyCode == Keys.Escape && scanning) { cts?.Cancel(); e.Handled = true; }
            if (e.Control && e.KeyCode == Keys.F) { txtFilter.Focus(); e.Handled = true; }
        }

        private void OnClosing(object sender, FormClosingEventArgs e)
        {
            cts?.Cancel();
            Settings.WindowMaximized = WindowState == FormWindowState.Maximized;
            var b = WindowState == FormWindowState.Normal ? Bounds : RestoreBounds;
            Settings.WindowWidth = b.Width;
            Settings.WindowHeight = b.Height;
            if (!string.IsNullOrWhiteSpace(cmbRange.Text)) Settings.LastRange = cmbRange.Text.Trim();
            Settings.Save();
        }

        internal static void OpenUrl(string url)
        {
            try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); } catch { }
        }

        internal static Icon LoadIcon()
        {
            try
            {
                using var s = typeof(MainForm).Assembly.GetManifestResourceStream("Vihor.vihor.ico");
                return s != null ? new Icon(s) : SystemIcons.Application;
            }
            catch { return SystemIcons.Application; }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) { uiTimer.Dispose(); cts?.Dispose(); }
            base.Dispose(disposing);
        }
    }

    /// <summary>Double-buffered ListView (no flicker while results stream in).</summary>
    internal sealed class HostListView : ListView
    {
        public HostListView()
        {
            DoubleBuffered = true;
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);
        }
    }
}
