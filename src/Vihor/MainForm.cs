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
        private readonly ToolStripSplitButton btnExport = new ToolStripSplitButton();
        private readonly ToolStripDropDownButton btnFav = new ToolStripDropDownButton();
        private readonly ToolStrip actions = new ToolStrip();
        private readonly ToolStripButton aWeb = new ToolStripButton(), aRdp = new ToolStripButton(), aSsh = new ToolStripButton(), aSmb = new ToolStripButton();
        private readonly ToolStripButton aPing = new ToolStripButton(), aTrace = new ToolStripButton(), aWol = new ToolStripButton(), aPalette = new ToolStripButton();
        private readonly ToolStripSplitButton aPower = new ToolStripSplitButton();
        private readonly ToolStripDropDownButton aCopy = new ToolStripDropDownButton();
        private readonly ToolStripLabel lblFilterIcon = new ToolStripLabel();
        private readonly ToolStripStatusLabel lblUpdate = new ToolStripStatusLabel();
        private readonly ContextMenuStrip headerMenu = new ContextMenuStrip();
        private readonly Dictionary<DeviceType, Bitmap> typeIcons = new Dictionary<DeviceType, Bitmap>();
        private readonly ToolStripButton btnExpand = new ToolStripButton();
        private readonly ToolStripButton btnCollapse = new ToolStripButton();
        private readonly ToolStripButton btnRescan = new ToolStripButton();
        private readonly ToolStripButton btnClearFilter = new ToolStripButton();
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
        private ToolStripMenuItem mFile, mExportCsv, mExportHtml, mExportJson, mExit;
        private ToolStripMenuItem mColumns, mShowTools, mIconsOnly, mCheckUpdates, mPalette;
        private ToolStripMenuItem mScan, mScanStart, mScanLocal, mScanPorts, mRescan, mDiag;
        private ToolStripMenuItem mView, mExpandAll, mCollapseAll, mTheme, mThemeSystem, mThemeLight, mThemeDark;
        private ToolStripMenuItem mSettings, mOptions, mLanguage;
        private ToolStripMenuItem mHelp, mWebsite, mGithub, mAbout;

        // context menu items
        private ToolStripMenuItem cCopy, cCopyIp, cCopyName, cCopyMac, cCopyVendor, cCopyRow;
        private ToolStripMenuItem cRdp, cSsh, cHttp, cHttps, cHttpAlt, cFtp, cExplore;
        private ToolStripMenuItem cTools, cPing, cTracert, cWol, cRescan, cShutdown, cRestart, cAbort;
        private ToolStripMenuItem cOpenService, cExpandAll, cCollapseAll, cAdvancedRescan, cNote;
        private ToolStripSeparator cOpenServiceSep;
        private readonly HashSet<uint> expanded = new HashSet<uint>();
        private bool chevronClicked;

        // --- state ---
        private readonly List<HostInfo> hosts = new List<HostInfo>();
        private List<HostInfo> missing = new List<HostInfo>();
        private ScanHistory history = new ScanHistory();
        private bool compared;
        private readonly ConcurrentQueue<HostInfo> incoming = new ConcurrentQueue<HostInfo>();
        private CancellationTokenSource cts;
        private int progDone, progTotal;
        private bool scanning;
        private string lastScannedRange = "";
        private readonly Stopwatch scanClock = new Stopwatch();
        private int sortColumn = 1;
        private bool sortAsc = true;

        private const int ColName = 0, ColIp = 1, ColType = 2, ColOs = 3, ColVendor = 4, ColMac = 5, ColWorkgroup = 6, ColServices = 7, ColNote = 8, ColPing = 9;
        private static readonly string[] ColIds = { "name", "ip", "type", "os", "vendor", "mac", "workgroup", "services", "note", "ping" };
        private static readonly int[] ColDefault = { 180, 110, 120, 110, 160, 125, 100, 220, 140, 60 };
        private int[] colWidth;
        private readonly HashSet<int> hiddenCols = new HashSet<int>();

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
            Theme.Resolve(Settings.Theme);

            BuildIcons();
            BuildMenu();
            BuildToolbar();
            BuildList();
            BuildContextMenu();
            BuildStatus();

            Controls.Add(list);
            Controls.Add(actions);
            Controls.Add(tools);
            Controls.Add(menu);
            Controls.Add(status);
            MainMenuStrip = menu;

            uiTimer.Tick += (s, e) => FlushIncoming();
            KeyDown += OnKeyDown;
            Shown += async (s, e) => { _ = OuiDb.LoadAsync(); cmbRange.Focus(); await CheckUpdate(false); };
            FormClosing += OnClosing;

            LoadRanges();
            ApplyTexts();
            ApplyTheme();
        }

        // ------------------------------------------------------------------ build UI

        private int iconSz;

        private void BuildIcons()
        {
            int sz = iconSz = LogicalToDeviceUnits(16);
            icons.ImageSize = new Size(sz * 2, sz);   // [chevron][status dot]
            icons.ColorDepth = ColorDepth.Depth32Bit;
            var colors = new Dictionary<string, Color>
            {
                ["alive"] = Color.FromArgb(46, 160, 67),
                ["self"] = Color.FromArgb(31, 111, 235),
                ["gateway"] = Color.FromArgb(219, 130, 20),
                ["missing"] = Color.FromArgb(150, 155, 163),
            };
            foreach (var kv in colors)
            {
                icons.Images.Add(kv.Key, HostIcon(sz, kv.Value, 0));
                icons.Images.Add(kv.Key + "_c", HostIcon(sz, kv.Value, 1));
                icons.Images.Add(kv.Key + "_e", HostIcon(sz, kv.Value, 2));
            }
            foreach (var k in new[] { "web", "term", "folder", "rdp", "printer", "other" })
                icons.Images.Add("svc_" + k, ServiceIcon(sz, k));
        }

        /// <summary>chevron: 0 = none, 1 = collapsed (›), 2 = expanded (⌄)</summary>
        private static Bitmap HostIcon(int sz, Color c, int chevron)
        {
            var b = new Bitmap(sz * 2, sz);
            using var g = Graphics.FromImage(b);
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            if (chevron != 0)
            {
                using var pen = new Pen(Color.FromArgb(110, 118, 129), Math.Max(1.5f, sz / 9f));
                float cx = sz / 2f, cy = sz / 2f, r = sz / 5f;
                if (chevron == 1) g.DrawLines(pen, new[] { new PointF(cx - r / 2, cy - r), new PointF(cx + r / 2, cy), new PointF(cx - r / 2, cy + r) });
                else g.DrawLines(pen, new[] { new PointF(cx - r, cy - r / 2), new PointF(cx, cy + r / 2), new PointF(cx + r, cy - r / 2) });
            }
            int pad = Math.Max(2, sz / 5);
            using var br = new SolidBrush(c);
            g.FillEllipse(br, sz + pad, pad, sz - 2 * pad, sz - 2 * pad);
            return b;
        }

        private static Bitmap ServiceIcon(int sz, string kind)
        {
            var b = new Bitmap(sz * 2, sz);
            using var g = Graphics.FromImage(b);
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            var col = Color.FromArgb(31, 111, 235);
            using var pen = new Pen(col, Math.Max(1.2f, sz / 12f));
            float p = sz * 0.15f, w = sz - 2 * p;
            switch (kind)
            {
                case "web": // globe
                    g.DrawEllipse(pen, p, p, w, w);
                    g.DrawEllipse(pen, p + w * 0.3f, p, w * 0.4f, w);
                    g.DrawLine(pen, p, sz / 2f, sz - p, sz / 2f);
                    break;
                case "term": // >_
                    g.DrawRectangle(pen, p, p + w * 0.1f, w, w * 0.8f);
                    g.DrawLines(pen, new[] { new PointF(p + w * 0.2f, p + w * 0.35f), new PointF(p + w * 0.4f, p + w * 0.5f), new PointF(p + w * 0.2f, p + w * 0.65f) });
                    g.DrawLine(pen, p + w * 0.5f, p + w * 0.68f, p + w * 0.8f, p + w * 0.68f);
                    break;
                case "folder":
                    g.DrawPolygon(pen, new[] { new PointF(p, p + w * 0.2f), new PointF(p + w * 0.4f, p + w * 0.2f), new PointF(p + w * 0.5f, p + w * 0.35f),
                                               new PointF(p + w, p + w * 0.35f), new PointF(p + w, p + w * 0.85f), new PointF(p, p + w * 0.85f) });
                    break;
                case "printer":
                    g.DrawRectangle(pen, p + w * 0.25f, p, w * 0.5f, w * 0.3f);
                    g.DrawRectangle(pen, p, p + w * 0.3f, w, w * 0.4f);
                    g.DrawRectangle(pen, p + w * 0.25f, p + w * 0.6f, w * 0.5f, w * 0.35f);
                    break;
                case "rdp": // monitor
                    g.DrawRectangle(pen, p, p + w * 0.05f, w, w * 0.65f);
                    g.DrawLine(pen, sz / 2f, p + w * 0.7f, sz / 2f, p + w * 0.9f);
                    g.DrawLine(pen, p + w * 0.25f, p + w * 0.92f, p + w * 0.75f, p + w * 0.92f);
                    break;
                default:
                    g.DrawEllipse(pen, p + w * 0.2f, p + w * 0.2f, w * 0.6f, w * 0.6f);
                    break;
            }
            return b;
        }

        private void BuildMenu()
        {
            mExportCsv = new ToolStripMenuItem("", null, (s, e) => DoExport("csv"));
            mExportHtml = new ToolStripMenuItem("", null, (s, e) => DoExport("html"));
            mExportJson = new ToolStripMenuItem("", null, (s, e) => DoExport("json"));
            mExit = new ToolStripMenuItem("", null, (s, e) => Close());
            mFile = new ToolStripMenuItem("", null, mExportCsv, mExportHtml, mExportJson, new ToolStripSeparator(), mExit);

            mScanStart = new ToolStripMenuItem("", null, (s, e) => ToggleScan()) { ShortcutKeys = Keys.F5 };
            mScanLocal = new ToolStripMenuItem();
            mScanLocal.DropDownItems.Add(new ToolStripMenuItem("…"));
            mScanLocal.DropDownOpening += (s, e) => FillAdapterMenu(mScanLocal.DropDownItems);
            mScanPorts = new ToolStripMenuItem("", null, (s, e) => { Settings.ScanPorts = !Settings.ScanPorts; mScanPorts.Checked = Settings.ScanPorts; })
            { Checked = Settings.ScanPorts };
            mRescan = new ToolStripMenuItem("", null, async (s, e) => await RescanSelected()) { ShortcutKeys = Keys.Control | Keys.R };
            mScan = new ToolStripMenuItem("", null, mScanStart, mScanLocal, mRescan, new ToolStripSeparator(), mScanPorts);

            mExpandAll = new ToolStripMenuItem("", null, (s, e) => ExpandAll(true)) { ShortcutKeyDisplayString = "Ctrl++" };
            mCollapseAll = new ToolStripMenuItem("", null, (s, e) => ExpandAll(false)) { ShortcutKeyDisplayString = "Ctrl+-" };
            mColumns = new ToolStripMenuItem();
            mColumns.DropDownItems.Add(new ToolStripMenuItem("…"));
            mColumns.DropDownOpening += (s, e) => FillColumnsMenu(mColumns.DropDownItems);
            mShowTools = new ToolStripMenuItem("", null, (s, e) => { Settings.ShowToolsBar = !Settings.ShowToolsBar; ApplyToolbarLayout(); Settings.Save(); });
            mIconsOnly = new ToolStripMenuItem("", null, (s, e) => { Settings.IconsOnly = !Settings.IconsOnly; ApplyToolbarLayout(); Settings.Save(); });
            mPalette = new ToolStripMenuItem("", null, (s, e) => ShowPalette()) { ShortcutKeyDisplayString = "Ctrl+K" };
            mThemeSystem = new ToolStripMenuItem("", null, (s, e) => SetTheme("system"));
            mThemeLight = new ToolStripMenuItem("", null, (s, e) => SetTheme("light"));
            mThemeDark = new ToolStripMenuItem("", null, (s, e) => SetTheme("dark"));
            mTheme = new ToolStripMenuItem("", null, mThemeSystem, mThemeLight, mThemeDark);
            mView = new ToolStripMenuItem("", null, mExpandAll, mCollapseAll, new ToolStripSeparator(), mColumns, mShowTools, mIconsOnly, mTheme,
                                          new ToolStripSeparator(), mPalette);

            mLanguage = new ToolStripMenuItem();
            foreach (var l in Lang.Available)
            {
                var item = new ToolStripMenuItem(l.NativeName) { Tag = l.Code };
                item.Click += (s, e) => SetLanguage((string)((ToolStripMenuItem)s).Tag);
                mLanguage.DropDownItems.Add(item);
            }
            mOptions = new ToolStripMenuItem("", null, (s, e) => ShowOptions());
            mSettings = new ToolStripMenuItem("", null, mOptions, mLanguage);

            mWebsite = new ToolStripMenuItem("", null, (s, e) => OpenUrl(AppInfo.Website));
            mGithub = new ToolStripMenuItem("", null, (s, e) => OpenUrl(AppInfo.Repository));
            mAbout = new ToolStripMenuItem("", null, (s, e) => { using var a = new AboutForm(); a.ShowDialog(this); });
            mDiag = new ToolStripMenuItem("", null, (s, e) => SaveDiagnostics());
            mCheckUpdates = new ToolStripMenuItem("", null, async (s, e) => await CheckUpdate(true)) { Visible = !Updater.IsStore };
            mHelp = new ToolStripMenuItem("", null, mWebsite, mGithub, new ToolStripSeparator(), mDiag, mCheckUpdates, new ToolStripSeparator(), mAbout);

            menu.Items.AddRange(new ToolStripItem[] { mFile, mScan, mView, mSettings, mHelp });
        }

        private void BuildToolbar()
        {
            tools.GripStyle = actions.GripStyle = ToolStripGripStyle.Hidden;
            tools.Padding = new Padding(LogicalToDeviceUnits(6), LogicalToDeviceUnits(4), LogicalToDeviceUnits(6), LogicalToDeviceUnits(4));
            actions.Padding = new Padding(LogicalToDeviceUnits(6), LogicalToDeviceUnits(2), LogicalToDeviceUnits(6), LogicalToDeviceUnits(4));
            tools.ImageScalingSize = new Size(LogicalToDeviceUnits(16), LogicalToDeviceUnits(16));
            actions.ImageScalingSize = new Size(LogicalToDeviceUnits(18), LogicalToDeviceUnits(18));

            // --- row 1: what to scan
            cmbRange.AutoSize = false;
            cmbRange.Width = LogicalToDeviceUnits(300);
            cmbRange.DropDownStyle = ComboBoxStyle.DropDown;
            cmbRange.Font = new Font(FontFamily.GenericMonospace, Font.SizeInPoints);
            cmbRange.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; if (!scanning) StartScan(); } };

            btnScan.DisplayStyle = ToolStripItemDisplayStyle.ImageAndText;
            btnScan.Font = new Font(Font, FontStyle.Bold);
            btnScan.BackColor = Color.FromArgb(31, 111, 235);
            btnScan.ForeColor = Color.White;
            btnScan.Margin = new Padding(LogicalToDeviceUnits(6), 0, LogicalToDeviceUnits(2), 0);
            btnScan.Padding = new Padding(LogicalToDeviceUnits(10), LogicalToDeviceUnits(2), LogicalToDeviceUnits(12), LogicalToDeviceUnits(2));
            btnScan.Click += (s, e) => ToggleScan();
            btnScan.Tag = "accent"; // keeps its white text in the dark theme

            btnLocal.ButtonClick += (s, e) => cmbRange.Text = IpRange.DefaultRange(IpRange.GetLocalSubnets());
            btnLocal.DropDownOpening += (s, e) => FillAdapterMenu(btnLocal.DropDownItems);
            btnLocal.DropDownItems.Add(new ToolStripMenuItem("…")); // placeholder so the arrow is active

            btnRescan.Click += async (s, e) => await RescanSelected();

            btnFav.DropDownItems.Add(new ToolStripMenuItem("…"));
            btnFav.DropDownOpening += (s, e) => FillFavorites(btnFav.DropDownItems);

            btnExport.ButtonClick += (s, e) => DoExport("csv");
            btnExport.DropDownItems.Add("CSV", null, (s, e) => DoExport("csv"));
            btnExport.DropDownItems.Add("HTML", null, (s, e) => DoExport("html"));
            btnExport.DropDownItems.Add("JSON", null, (s, e) => DoExport("json"));

            txtFilter.AutoSize = false;
            txtFilter.Width = LogicalToDeviceUnits(200);
            txtFilter.TextChanged += (s, e) => { btnClearFilter.Visible = txtFilter.Text.Length > 0; RebuildList(); };
            txtFilter.KeyDown += (s, e) => { if (e.KeyCode == Keys.Escape && txtFilter.Text.Length > 0) { txtFilter.Clear(); e.Handled = e.SuppressKeyPress = true; } };

            btnClearFilter.DisplayStyle = ToolStripItemDisplayStyle.Text;
            btnClearFilter.Text = "✕";
            btnClearFilter.Visible = false;
            btnClearFilter.Click += (s, e) => { txtFilter.Clear(); txtFilter.Focus(); };
            lblFilterIcon.DisplayStyle = ToolStripItemDisplayStyle.Image;
            foreach (var i in new ToolStripItem[] { btnClearFilter, txtFilter, lblFilterIcon }) i.Alignment = ToolStripItemAlignment.Right;

            tools.Items.AddRange(new ToolStripItem[]
            {
                lblRange, cmbRange, btnScan, btnLocal, new ToolStripSeparator(), btnRescan, btnFav, btnExport,
                btnClearFilter, txtFilter, lblFilterIcon
            });

            // --- row 2: tools for the selected device
            HostInfo H() => Selected().FirstOrDefault();
            aWeb.Click += (s, e) => { var h = H(); if (h != null) HostActions.BestWeb(h); };
            aRdp.Click += (s, e) => { var h = H(); if (h != null) HostActions.Rdp(h); };
            aSsh.Click += (s, e) => { var h = H(); if (h != null) HostActions.Ssh(h); };
            aSmb.Click += (s, e) => { var h = H(); if (h != null) HostActions.Explore(h); };
            aPing.Click += (s, e) => OpenPing(false);
            aTrace.Click += (s, e) => OpenPing(true);
            aWol.Click += (s, e) => { foreach (var h in Selected()) HostActions.WakeOnLan(h); };
            aPower.ButtonClick += (s, e) => { var h = H(); if (h != null) HostActions.Shutdown(h, false); };
            aPower.DropDownItems.Add(new ToolStripMenuItem("", null, (s, e) => { var h = H(); if (h != null) HostActions.Shutdown(h, false); }) { Name = "shutdown" });
            aPower.DropDownItems.Add(new ToolStripMenuItem("", null, (s, e) => { var h = H(); if (h != null) HostActions.Shutdown(h, true); }) { Name = "restart" });
            aPower.DropDownItems.Add(new ToolStripMenuItem("", null, (s, e) => { var h = H(); if (h != null) HostActions.AbortShutdown(h); }) { Name = "abort" });
            btnExpand.Click += (s, e) => ExpandAll(true);
            btnCollapse.Click += (s, e) => ExpandAll(false);
            aCopy.DropDownItems.Add(new ToolStripMenuItem("", null, (s, e) => CopySel(h => h.IpText)) { Name = "ip", ShortcutKeyDisplayString = "Ctrl+C" });
            aCopy.DropDownItems.Add(new ToolStripMenuItem("", null, (s, e) => CopySel(h => h.Name)) { Name = "name" });
            aCopy.DropDownItems.Add(new ToolStripMenuItem("", null, (s, e) => CopySel(h => h.MacText)) { Name = "mac" });
            aCopy.DropDownItems.Add(new ToolStripMenuItem("", null, (s, e) => CopySel(h => h.Vendor)) { Name = "vendor" });
            aCopy.DropDownItems.Add(new ToolStripMenuItem("", null, (s, e) => CopySel(RowText)) { Name = "row" });
            aPalette.Click += (s, e) => ShowPalette();
            aPalette.Alignment = ToolStripItemAlignment.Right;

            actions.Items.AddRange(new ToolStripItem[]
            {
                aWeb, aRdp, aSsh, aSmb, new ToolStripSeparator(), aPing, aTrace, new ToolStripSeparator(), aWol, aPower,
                new ToolStripSeparator(), btnExpand, btnCollapse, new ToolStripSeparator(), aCopy, aPalette
            });
            ApplyToolbarLayout();
        }

        private static string RowText(HostInfo h) => string.Join("\t", h.IpText, h.Name, h.TypeText, h.Os, h.Vendor, h.MacText, h.ServicesText);

        private void CopySel(Func<HostInfo, string> f) => HostActions.Copy(string.Join(Environment.NewLine, Selected().Select(f)));

        private void OpenPing(bool trace)
        {
            var h = Selected().FirstOrDefault();
            if (h == null) return;
            new PingForm(h.IpText, h.Name, trace).Show(this);
        }

        /// <summary>Icons + text, or icons only (View menu); second row can be hidden.</summary>
        private void ApplyToolbarLayout()
        {
            actions.Visible = Settings.ShowToolsBar;
            mShowTools.Checked = Settings.ShowToolsBar;
            mIconsOnly.Checked = Settings.IconsOnly;
            var style = Settings.IconsOnly ? ToolStripItemDisplayStyle.Image : ToolStripItemDisplayStyle.ImageAndText;
            foreach (var i in new ToolStripItem[] { btnLocal, btnRescan, btnFav, btnExport, aWeb, aRdp, aSsh, aSmb, aPing, aTrace, aWol, aPower, aCopy, aPalette })
                i.DisplayStyle = style;
            btnExpand.DisplayStyle = btnCollapse.DisplayStyle = ToolStripItemDisplayStyle.Image;
        }

        /// <summary>Tools on the second row work on the selected device; greyed out without a selection.</summary>
        private void UpdateToolStates()
        {
            var sel = Selected().ToList();
            bool single = sel.Count == 1;
            var h = single ? sel[0] : null;
            foreach (var i in new ToolStripItem[] { aWeb, aRdp, aSsh, aSmb, aPing, aTrace, aPower }) i.Enabled = single;
            aWol.Enabled = sel.Any(x => x.Mac != null);
            aCopy.Enabled = sel.Count > 0;
            // Services the device really has are shown in bold
            void Mark(ToolStripItem i, bool on) => i.Font = new Font(actions.Font, on ? FontStyle.Bold : FontStyle.Regular);
            Mark(aWeb, h != null && h.Details.Any(d => d.Def.IsWeb));
            Mark(aRdp, h != null && h.Has(Service.Rdp));
            Mark(aSsh, h != null && h.Has(Service.Ssh));
            Mark(aSmb, h != null && h.Has(Service.Smb));
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
            LoadColumnLayout();
            for (int i = 0; i < ColIds.Length; i++)
                list.Columns.Add("", hiddenCols.Contains(i) ? 0 : colWidth[i], i == ColPing ? HorizontalAlignment.Right : HorizontalAlignment.Left);
            list.AllowColumnReorder = true;
            ApplyColumnOrder();
            list.SelectedIndexChanged += (s, e) => UpdateToolStates();
            list.ColumnWidthChanging += (s, e) => { if (hiddenCols.Contains(e.ColumnIndex)) { e.Cancel = true; e.NewWidth = 0; } };
            list.ColumnClick += (s, e) =>
            {
                if (e.Column == sortColumn) sortAsc = !sortAsc; else { sortColumn = e.Column; sortAsc = true; }
                RebuildList();
            };
            list.OwnerDraw = true;
            list.DrawColumnHeader += (s, e) =>
            {
                if (!Theme.Dark) { e.DrawDefault = true; return; }
                using (var b = new SolidBrush(Theme.HeaderBack)) e.Graphics.FillRectangle(b, e.Bounds);
                using (var pen = new Pen(Theme.Border))
                {
                    e.Graphics.DrawLine(pen, e.Bounds.Right - 1, e.Bounds.Top + 3, e.Bounds.Right - 1, e.Bounds.Bottom - 4);
                    e.Graphics.DrawLine(pen, e.Bounds.Left, e.Bounds.Bottom - 1, e.Bounds.Right, e.Bounds.Bottom - 1);
                }
                var flags = TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis |
                            (e.Header.TextAlign == HorizontalAlignment.Right ? TextFormatFlags.Right : TextFormatFlags.Left);
                var r = Rectangle.Inflate(e.Bounds, -LogicalToDeviceUnits(5), 0);
                TextRenderer.DrawText(e.Graphics, e.Header.Text, list.Font, r, Theme.Fore, flags);
            };
            // Every row is drawn by Vihor itself: the same readable selection in the light and dark theme
            list.DrawItem += (s, e) =>
            {
                e.DrawDefault = false;
                if (e.Item.Tag is ServiceRow) return;
                using var b = new SolidBrush(RowBack(e.Item));
                e.Graphics.FillRectangle(b, e.Bounds);
            };
            list.DrawSubItem += (s, e) =>
            {
                if (e.Item.Tag is ServiceRow sr) { if (e.ColumnIndex == 0) DrawServiceRow(e.Graphics, e.Item, sr); }
                else if (e.ColumnIndex == ColType && e.Item.Tag is HostInfo th) DrawTypeCell(e, th);
                else DrawCell(e);
            };
            list.MouseMove += (s, e) =>
            {
                int i = list.GetItemAt(e.X, e.Y)?.Index ?? -1;
                if (i == hoverIndex) return;
                InvalidateRow(hoverIndex);
                hoverIndex = i;
                InvalidateRow(hoverIndex);
            };
            list.MouseLeave += (s, e) => { InvalidateRow(hoverIndex); hoverIndex = -1; };
            list.GotFocus += (s, e) => list.Invalidate();
            list.LostFocus += (s, e) => list.Invalidate();
            list.MouseClick += (s, e) =>
            {
                chevronClicked = false;
                if (e.Button != MouseButtons.Left) return;
                var hit = list.HitTest(e.Location);
                if (hit.Item?.Tag is HostInfo h && h.Details.Count > 0)
                {
                    var icon = list.GetItemRect(hit.Item.Index, ItemBoundsPortion.Icon);
                    if (e.X >= icon.Left - 2 && e.X < icon.Left + iconSz + 2) { chevronClicked = true; ToggleExpand(h); }
                }
            };
            list.DoubleClick += (s, e) =>
            {
                if (chevronClicked) return;
                var item = list.SelectedItems.Count > 0 ? list.SelectedItems[0] : null;
                if (item?.Tag is ServiceRow sr) HostActions.OpenService(sr.Host, sr.Detail);
                else if (item?.Tag is HostInfo) DefaultAction(); // smart action: web panel, RDP or SSH
            };
            list.KeyDown += (s, e) =>
            {
                var item = list.FocusedItem;
                if (e.KeyCode == Keys.Enter)
                {
                    if (item?.Tag is ServiceRow sr) HostActions.OpenService(sr.Host, sr.Detail); else DefaultAction();
                    e.Handled = true;
                }
                if (e.KeyCode == Keys.Right && item?.Tag is HostInfo hr && hr.Details.Count > 0 && !expanded.Contains(hr.Ip)) { ToggleExpand(hr); e.Handled = true; }
                if (e.KeyCode == Keys.Left && item?.Tag is HostInfo hl && expanded.Contains(hl.Ip)) { ToggleExpand(hl); e.Handled = true; }
                if (e.KeyCode == Keys.Left && item?.Tag is ServiceRow sl) { ToggleExpand(sl.Host); e.Handled = true; }
                if (e.Control && e.KeyCode == Keys.C) { CopySel(e.Shift ? (Func<HostInfo, string>)RowText : h => h.IpText); e.Handled = true; }
                if (e.Control && e.KeyCode == Keys.A) { foreach (ListViewItem i in list.Items) i.Selected = true; e.Handled = true; }
            };
            list.ContextMenuStrip = ctx;
            list.ClientSizeChanged += (s, e) => FitColumns();
            list.ColumnWidthChanged += (s, e) =>
            {
                if (fitting) return;
                int w = list.Columns[e.ColumnIndex].Width;
                if (w > 0 && !hiddenCols.Contains(e.ColumnIndex)) colWidth[e.ColumnIndex] = w;
                if (e.ColumnIndex != ColServices) FitColumns();
            };
        }

        // ------------------------------------------------------------------ columns

        private void LoadColumnLayout()
        {
            colWidth = ColDefault.Select(w => LogicalToDeviceUnits(w)).ToArray();
            foreach (var part in (Settings.ColumnWidths ?? "").Split(','))
            {
                var kv = part.Split('=');
                int i = Array.IndexOf(ColIds, kv[0].Trim());
                if (kv.Length == 2 && i >= 0 && int.TryParse(kv[1], out var w) && w >= 20 && w < 2000) colWidth[i] = LogicalToDeviceUnits(w);
            }
            hiddenCols.Clear();
            foreach (var id in (Settings.HiddenColumns ?? "").Split(','))
            {
                int i = Array.IndexOf(ColIds, id.Trim());
                if (i > 0) hiddenCols.Add(i); // the Name column is always shown
            }
            if (!Settings.ShowWorkgroup && string.IsNullOrEmpty(Settings.HiddenColumns)) hiddenCols.Add(ColWorkgroup); // 0.4/0.5 setting
        }

        private void ApplyColumnOrder()
        {
            var order = (Settings.ColumnOrder ?? "").Split(',').Select(x => Array.IndexOf(ColIds, x.Trim())).Where(i => i >= 0).Distinct().ToList();
            if (order.Count != ColIds.Length) return;
            try { for (int d = 0; d < order.Count; d++) list.Columns[order[d]].DisplayIndex = d; } catch { }
        }

        private void SaveColumnLayout()
        {
            Settings.ColumnWidths = string.Join(",", ColIds.Select((id, i) => id + "=" + (int)Math.Round(colWidth[i] * 96.0 / DeviceDpi)));
            Settings.ColumnOrder = string.Join(",", list.Columns.Cast<ColumnHeader>().OrderBy(c => c.DisplayIndex).Select(c => ColIds[c.Index]));
            Settings.HiddenColumns = string.Join(",", hiddenCols.OrderBy(i => i).Select(i => ColIds[i]));
            Settings.ShowWorkgroup = !hiddenCols.Contains(ColWorkgroup);
        }

        private void ApplyColumns()
        {
            fitting = true;
            try { for (int i = 0; i < ColIds.Length; i++) list.Columns[i].Width = hiddenCols.Contains(i) ? 0 : colWidth[i]; }
            finally { fitting = false; }
            FitColumns();
        }

        /// <summary>View → Columns and right-click on the column header.</summary>
        private void FillColumnsMenu(ToolStripItemCollection items)
        {
            items.Clear();
            for (int i = 1; i < ColIds.Length; i++)
            {
                int col = i;
                var item = new ToolStripMenuItem(list.Columns[i].Text) { Checked = !hiddenCols.Contains(i), CheckOnClick = false };
                item.Click += (s, e) =>
                {
                    if (!hiddenCols.Remove(col)) hiddenCols.Add(col);
                    ApplyColumns();
                    SaveColumnLayout();
                    Settings.Save();
                };
                items.Add(item);
            }
            items.Add(new ToolStripSeparator());
            items.Add(new ToolStripMenuItem(Lang.T("menu.columns_reset"), null, (s, e) =>
            {
                Settings.ColumnWidths = Settings.ColumnOrder = Settings.HiddenColumns = "";
                Settings.ShowWorkgroup = true;
                LoadColumnLayout();
                for (int i = 0; i < ColIds.Length; i++) list.Columns[i].DisplayIndex = i;
                ApplyColumns();
                Settings.Save();
            }));
        }

        private int HeaderHeight()
        {
            try
            {
                var hdr = NativeHeader.SendMessage(list.Handle, 0x101F /* LVM_GETHEADER */, IntPtr.Zero, IntPtr.Zero);
                if (hdr != IntPtr.Zero && NativeHeader.GetWindowRect(hdr, out var r)) return r.Bottom - r.Top;
            }
            catch { }
            return LogicalToDeviceUnits(24);
        }

        private int hoverIndex = -1;

        private void InvalidateRow(int i)
        {
            if (i >= 0 && i < list.Items.Count) try { list.Invalidate(list.Items[i].Bounds); } catch { }
        }

        /// <summary>Selected + focused: accent blue with white text; selected without focus: soft blue; hover: light tint.</summary>
        private Color RowBack(ListViewItem item)
        {
            if (item.Selected)
                return list.Focused ? (Theme.Dark ? Color.FromArgb(31, 95, 190) : Color.FromArgb(0, 103, 192))
                                    : (Theme.Dark ? Color.FromArgb(44, 62, 92) : Color.FromArgb(204, 228, 247));
            if (item.Index == hoverIndex) return Theme.Dark ? Color.FromArgb(42, 45, 51) : Color.FromArgb(229, 241, 251);
            return list.BackColor;
        }

        private Color RowFore(ListViewItem item, Color normal) =>
            item.Selected && list.Focused ? Color.White : normal;

        /// <summary>Plain cell (also the Name column with its expand chevron and status dot).</summary>
        private void DrawCell(DrawListViewSubItemEventArgs e)
        {
            var bounds = e.Bounds;
            if (e.ColumnIndex == 0)
            {
                // Sub-item 0 reports the whole row; use the real first column
                var label = list.GetItemRect(e.ItemIndex, ItemBoundsPortion.Label);
                bounds = new Rectangle(label.Right - list.Columns[0].Width, bounds.Top, list.Columns[0].Width, bounds.Height);
            }
            if (bounds.Width <= 0) return;
            using (var b = new SolidBrush(RowBack(e.Item))) e.Graphics.FillRectangle(b, bounds);
            int x = bounds.Left + LogicalToDeviceUnits(6);
            if (e.ColumnIndex == 0 && e.Item.ImageList != null && !string.IsNullOrEmpty(e.Item.ImageKey))
            {
                var icon = list.GetItemRect(e.ItemIndex, ItemBoundsPortion.Icon);
                int idx = icons.Images.IndexOfKey(e.Item.ImageKey);
                var gs = e.Graphics.Save();
                e.Graphics.SetClip(bounds);
                if (idx >= 0) icons.Draw(e.Graphics, icon.Left, bounds.Top + (bounds.Height - icons.ImageSize.Height) / 2, idx);
                e.Graphics.Restore(gs);
                x = icon.Left + icons.ImageSize.Width + LogicalToDeviceUnits(4);
            }
            var r = new Rectangle(x, bounds.Top, Math.Max(0, bounds.Right - x - LogicalToDeviceUnits(4)), bounds.Height);
            var flags = TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix |
                        (list.Columns[e.ColumnIndex].TextAlign == HorizontalAlignment.Right ? TextFormatFlags.Right : TextFormatFlags.Left);
            TextRenderer.DrawText(e.Graphics, e.SubItem.Text, e.Item.Font ?? list.Font, r, RowFore(e.Item, e.Item.ForeColor), flags);
        }

        /// <summary>Type column: device icon + name, and a warning sign when something needs attention.</summary>
        private void DrawTypeCell(DrawListViewSubItemEventArgs e, HostInfo h)
        {
            bool sel = e.Item.Selected;
            var fg = RowFore(e.Item, e.Item.ForeColor);
            using (var b = new SolidBrush(RowBack(e.Item))) e.Graphics.FillRectangle(b, e.Bounds);
            if (e.Bounds.Width < 8) return;
            int x = e.Bounds.Left + LogicalToDeviceUnits(4);
            var t = h.Type;
            if (t != DeviceType.Unknown)
            {
                if (!typeIcons.TryGetValue(t, out var icon))
                    typeIcons[t] = icon = ToolIcons.Get(DeviceKind.Icon(t), iconSz, Theme.Dark ? Color.FromArgb(170, 178, 189) : Color.FromArgb(90, 99, 112));
                var g = e.Graphics.Save();
                e.Graphics.SetClip(e.Bounds);
                e.Graphics.DrawImage(icon, x, e.Bounds.Top + (e.Bounds.Height - iconSz) / 2, iconSz, iconSz);
                e.Graphics.Restore(g);
                x += iconSz + LogicalToDeviceUnits(5);
            }
            var text = h.TypeText + (h.HasWarning ? "  ⚠" : "");
            var r = new Rectangle(x, e.Bounds.Top, Math.Max(0, e.Bounds.Right - x - 2), e.Bounds.Height);
            TextRenderer.DrawText(e.Graphics, text, e.Item.Font ?? list.Font, r, fg,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
        }

        private bool fitting;

        /// <summary>The Services column takes the remaining width so long service lists are readable.</summary>
        private void FitColumns()
        {
            if (fitting || list.Columns.Count <= ColPing) return;
            fitting = true;
            try
            {
                int others = 0;
                for (int i = 0; i < list.Columns.Count; i++) if (i != ColServices) others += list.Columns[i].Width;
                int w = list.ClientSize.Width - others - 2;
                list.Columns[ColServices].Width = Math.Max(LogicalToDeviceUnits(120), w);
            }
            finally { fitting = false; }
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
            cNote = new ToolStripMenuItem("", null, (s, e) =>
            {
                var h = H();
                if (h == null) return;
                var note = Notes.Edit(this, h);
                if (note != null) { Notes.Set(h, note); RebuildList(); }
            });

            cPing = new ToolStripMenuItem("", null, (s, e) => OpenPing(false));
            cTracert = new ToolStripMenuItem("", null, (s, e) => OpenPing(true));
            cWol = new ToolStripMenuItem("", null, (s, e) => { foreach (var h in Selected()) HostActions.WakeOnLan(h); });
            cShutdown = new ToolStripMenuItem("", null, (s, e) => HostActions.Shutdown(H(), false));
            cRestart = new ToolStripMenuItem("", null, (s, e) => HostActions.Shutdown(H(), true));
            cAbort = new ToolStripMenuItem("", null, (s, e) => HostActions.AbortShutdown(H()));
            cTools = new ToolStripMenuItem("", null, cPing, cTracert, cWol, new ToolStripSeparator(), cShutdown, cRestart, cAbort);

            cRescan = new ToolStripMenuItem("", null, async (s, e) => await RescanSelected());
            cAdvancedRescan = new ToolStripMenuItem("", null, async (s, e) => await AdvancedRescan());
            cOpenService = new ToolStripMenuItem("", null, (s, e) =>
            {
                if (list.FocusedItem?.Tag is ServiceRow sr) HostActions.OpenService(sr.Host, sr.Detail);
            }) { Font = new Font(ctx.Font, FontStyle.Bold) };
            cOpenServiceSep = new ToolStripSeparator();
            cExpandAll = new ToolStripMenuItem("", null, (s, e) => ExpandAll(true));
            cCollapseAll = new ToolStripMenuItem("", null, (s, e) => ExpandAll(false));

            ctx.Items.AddRange(new ToolStripItem[]
            {
                cRdp, cSsh, cHttp, cHttps, cHttpAlt, cFtp, cExplore, new ToolStripSeparator(),
                cCopy, cNote, cTools, new ToolStripSeparator(), cRescan, cAdvancedRescan, cExpandAll, cCollapseAll
            });
            ctx.Items.Insert(0, cOpenServiceSep);
            ctx.Items.Insert(0, cOpenService);

            ctx.Opening += (s, e) =>
            {
                // Right-click on the column header: choose columns
                var pt = list.PointToClient(Cursor.Position);
                if (pt.Y >= 0 && pt.Y < HeaderHeight())
                {
                    e.Cancel = true;
                    FillColumnsMenu(headerMenu.Items);
                    headerMenu.Show(Cursor.Position);
                    return;
                }
                var sel = Selected().ToList();
                if (sel.Count == 0) { e.Cancel = true; return; }
                var h = sel[0];
                bool onService = list.FocusedItem?.Tag is ServiceRow;
                cOpenService.Visible = cOpenServiceSep.Visible = onService;
                if (onService) cOpenService.Text = Lang.T("ctx.open") + "  " + ((ServiceRow)list.FocusedItem.Tag).Detail.Def.Label +
                    (((ServiceRow)list.FocusedItem.Tag).Detail.Def.IsWeb ? " :" + ((ServiceRow)list.FocusedItem.Tag).Detail.Def.Port : "");
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
                cRescan.Enabled = !scanning && !rescanning;
                cAdvancedRescan.Enabled = !scanning && !rescanning && sel.Count == 1;
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
            lblUpdate.IsLink = true;
            lblUpdate.Visible = false;
            lblUpdate.Click += (s, e) => OpenUrl(Updater.ReleasesUrl);
            status.Items.AddRange(new ToolStripItem[] { lblStatus, progress, lblUpdate, link });
        }

        // ------------------------------------------------------------------ texts

        private void ApplyTexts()
        {
            mFile.Text = Lang.T("menu.file");
            mExportCsv.Text = Lang.T("menu.export_csv");
            mExportHtml.Text = Lang.T("menu.export_html");
            mExportJson.Text = Lang.T("menu.export_json");
            mColumns.Text = Lang.T("menu.columns");
            mShowTools.Text = Lang.T("menu.show_tools");
            mIconsOnly.Text = Lang.T("menu.icons_only");
            mPalette.Text = Lang.T("menu.palette");
            mCheckUpdates.Text = Lang.T("menu.check_updates");
            mExit.Text = Lang.T("menu.exit");
            mScan.Text = Lang.T("menu.scan");
            mScanStart.Text = scanning ? Lang.T("btn.stop") : Lang.T("btn.scan");
            mScanLocal.Text = Lang.T("btn.my_network");
            mScanPorts.Text = Lang.T("menu.scan_ports");
            mRescan.Text = Lang.T("menu.rescan_selected");
            mView.Text = Lang.T("menu.view");
            mExpandAll.Text = Lang.T("ctx.expand_all");
            mCollapseAll.Text = Lang.T("ctx.collapse_all");
            mTheme.Text = Lang.T("menu.theme");
            mThemeSystem.Text = Lang.T("menu.theme_system");
            mThemeLight.Text = Lang.T("menu.theme_light");
            mThemeDark.Text = Lang.T("menu.theme_dark");
            mThemeSystem.Checked = Settings.Theme == "system";
            mThemeLight.Checked = Settings.Theme == "light";
            mThemeDark.Checked = Settings.Theme == "dark";
            mOptions.Text = Lang.T("menu.options");
            mDiag.Text = Lang.T("menu.diagnostics");
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
            btnScan.Image = ToolIcons.Get(scanning ? "stop" : "scan", LogicalToDeviceUnits(16), Color.White);
            btnLocal.Text = Lang.T("btn.my_network");
            btnLocal.ToolTipText = Lang.T("tip.my_network");
            btnExport.Text = Lang.T("btn.export");
            btnExport.ToolTipText = Lang.T("tip.export");
            btnExpand.Text = btnExpand.ToolTipText = Lang.T("ctx.expand_all") + " (Ctrl++)";
            btnCollapse.Text = btnCollapse.ToolTipText = Lang.T("ctx.collapse_all") + " (Ctrl+-)";
            btnRescan.Text = Lang.T("btn.rescan");
            btnRescan.ToolTipText = Lang.T("menu.rescan_selected") + " (Ctrl+R)";
            btnFav.Text = Lang.T("btn.favorites");
            btnFav.ToolTipText = Lang.T("tip.favorites");
            lblFilterIcon.ToolTipText = Lang.T("label.filter") + " (Ctrl+F)";
            txtFilter.ToolTipText = Lang.T("tip.filter");
            void A(ToolStripItem i, string key, string tip) { i.Text = Lang.T(key); i.ToolTipText = Lang.T(tip); }
            A(aWeb, "btn.web", "tip.web");
            A(aRdp, "btn.rdp", "tip.rdp");
            A(aSsh, "btn.ssh", "tip.ssh");
            A(aSmb, "btn.smb", "tip.smb");
            A(aPing, "btn.ping", "tip.ping");
            A(aTrace, "btn.tracert", "tip.tracert");
            A(aWol, "btn.wol", "tip.wol");
            A(aPower, "btn.power", "tip.power");
            A(aCopy, "btn.copy", "tip.copy");
            aPalette.Text = Lang.T("btn.palette");
            aPalette.ToolTipText = Lang.T("tip.palette") + " (Ctrl+K)";
            aPower.DropDownItems["shutdown"].Text = Lang.T("ctx.shutdown");
            aPower.DropDownItems["restart"].Text = Lang.T("ctx.restart");
            aPower.DropDownItems["abort"].Text = Lang.T("ctx.abort_shutdown");
            aCopy.DropDownItems["ip"].Text = Lang.T("col.ip");
            aCopy.DropDownItems["name"].Text = Lang.T("col.name");
            aCopy.DropDownItems["mac"].Text = Lang.T("col.mac");
            aCopy.DropDownItems["vendor"].Text = Lang.T("col.vendor");
            aCopy.DropDownItems["row"].Text = Lang.T("ctx.copy_row");
            ((ToolStripMenuItem)aCopy.DropDownItems["row"]).ShortcutKeyDisplayString = "Ctrl+Shift+C";
            cmbRange.ToolTipText = Lang.T("tip.range");

            list.Columns[ColName].Text = Lang.T("col.name");
            list.Columns[ColIp].Text = Lang.T("col.ip");
            list.Columns[ColType].Text = Lang.T("col.type");
            list.Columns[ColOs].Text = Lang.T("col.os");
            list.Columns[ColVendor].Text = Lang.T("col.vendor");
            list.Columns[ColMac].Text = Lang.T("col.mac");
            list.Columns[ColWorkgroup].Text = Lang.T("col.workgroup");
            list.Columns[ColServices].Text = Lang.T("col.services");
            list.Columns[ColPing].Text = Lang.T("col.ping");
            list.Columns[ColNote].Text = Lang.T("col.note");

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
            cNote.Text = Lang.T("ctx.note");
            cAdvancedRescan.Text = Lang.T("ctx.advanced_rescan");
            btnClearFilter.ToolTipText = Lang.T("tip.clear_filter");
            cExpandAll.Text = Lang.T("ctx.expand_all");
            cCollapseAll.Text = Lang.T("ctx.collapse_all");

            UpdateStatus();
            RebuildList();
            UpdateToolStates();
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
            missing.Clear();
            compared = false;
            history = ScanHistory.Load(range);
            expanded.Clear();
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
            var scanner = new Scanner(ScanOptions.FromSettings());
            scanner.HostFound += h => incoming.Enqueue(h);
            scanner.Progress += (d, t) => { progDone = d; };

            bool cancelled = false;
            try
            {
                await System.Threading.Tasks.Task.Run(() => scanner.RunAsync(targets, cts.Token));
                cancelled = cts.IsCancellationRequested;
            }
            catch (OperationCanceledException) { cancelled = true; }
            finally
            {
                scanClock.Stop();
                scanning = false;
                uiTimer.Stop();
                FlushIncoming();
                Scanner.MarkSameMac(hosts);
                // Compare with the previous complete scan of this range, then remember this one
                if (!cancelled)
                {
                    missing = history.Missing(hosts);
                    compared = history.HasPrevious;
                    ScanHistory.Save(range, hosts);
                }
                progress.Visible = false;
                cts.Dispose();
                cts = null;
                ApplyTexts();
            }
        }

        private bool rescanning;

        /// <summary>Rescans the selected devices with the Options settings (incl. port ranges), or with <paramref name="options"/>.</summary>
        private async System.Threading.Tasks.Task RescanSelected(ScanOptions options = null, List<HostInfo> targets = null)
        {
            var sel = targets ?? Selected().ToList();
            if (sel.Count == 0 || scanning || rescanning) return;
            rescanning = true;
            btnRescan.Enabled = mRescan.Enabled = false;
            var scanner = new Scanner(options ?? ScanOptions.FromSettings(rescan: true));

            // Progress: counted per TCP port check (port ranges can be thousands of ports)
            int portsDone = 0, portsTotal = Math.Max(1, scanner.PortChecksPerHost * sel.Count);
            string current = "";
            var clock = Stopwatch.StartNew();
            scanner.PortChecked += () => Interlocked.Increment(ref portsDone);
            progress.Maximum = portsTotal;
            progress.Value = 0;
            progress.Visible = true;
            void Show()
            {
                int d = Math.Min(Volatile.Read(ref portsDone), portsTotal);
                progress.Value = d;
                lblStatus.Text = Lang.T("status.rescan_progress", current, d, portsTotal, clock.Elapsed.TotalSeconds.ToString("0"));
            }
            using var timer = new System.Windows.Forms.Timer { Interval = 250 };
            timer.Tick += (s, e) => Show();
            timer.Start();
            try
            {
                foreach (var old in sel)
                {
                    current = old.IpText;
                    Show();
                    var fresh = await System.Threading.Tasks.Task.Run(() => scanner.ScanHostAsync(old.Ip, CancellationToken.None));
                    int idx = hosts.FindIndex(h => h.Ip == old.Ip);
                    if (idx < 0) continue;
                    if (fresh != null)
                    {
                        fresh.IsNew = old.IsNew;
                        hosts[idx] = fresh;
                        if (fresh.Details.Count > 0) expanded.Add(fresh.Ip); // show what was found
                    }
                    else hosts.RemoveAt(idx);
                    RebuildList();
                }
            }
            finally
            {
                timer.Stop();
                progress.Visible = false;
                rescanning = false;
                Scanner.MarkSameMac(hosts);
                btnRescan.Enabled = mRescan.Enabled = true;
                RebuildList();
                UpdateStatus();
            }
        }

        /// <summary>Opens the Options window for one device, then rescans it with those settings (not saved).</summary>
        private async System.Threading.Tasks.Task AdvancedRescan()
        {
            var h = Selected().FirstOrDefault();
            if (h == null || scanning || rescanning) return;
            ScanProfile profile;
            using (var f = new OptionsForm(string.IsNullOrEmpty(h.Name) ? h.IpText : $"{h.Name} ({h.IpText})"))
            {
                if (f.ShowDialog(this) != DialogResult.OK) return;
                profile = f.Profile;
            }
            await RescanSelected(ScanOptions.From(profile, rescan: true), new List<HostInfo> { h });
        }

        private void FlushIncoming()
        {
            bool added = false;
            while (incoming.TryDequeue(out var h)) { h.IsNew = history.IsNew(h); hosts.Add(h); added = true; }
            if (added) RebuildList();
            if (scanning) progress.Value = Math.Min(progress.Maximum, progDone);
            UpdateStatus();
        }

        private void UpdateStatus()
        {
            if (scanning)
                lblStatus.Text = Lang.T("status.scanning", progDone, progTotal, hosts.Count);
            else if (progTotal > 0)
            {
                lblStatus.Text = Lang.T("status.done", hosts.Count, progTotal, scanClock.Elapsed.TotalSeconds.ToString("0.0"));
                if (compared)
                    lblStatus.Text += " · " + Lang.T("status.compare", hosts.Count(h => h.IsNew), missing.Count,
                                                     history.When?.ToString("g") ?? "");
                int warn = hosts.Count(h => h.HasWarning);
                if (warn > 0) lblStatus.Text += " · ⚠ " + Lang.T("status.warnings", warn);
            }
            else
                lblStatus.Text = Lang.T("status.ready");
        }

        // ------------------------------------------------------------------ list

        private void RebuildList()
        {
            var filter = txtFilter.Text.Trim();
            IEnumerable<HostInfo> view = hosts.Concat(missing);
            if (filter.Length > 0)
                view = view.Where(h =>
                    h.IpText.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    h.Name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    h.Vendor.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    h.MacText.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    h.Workgroup.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    Notes.Get(h).IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    h.ServicesText.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    h.TypeText.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    h.Os.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    (h.HasWarning && (filter == "⚠" || filter.Equals(Lang.T("filter.warning"), StringComparison.OrdinalIgnoreCase))) ||
                    (h.IsNew && filter.Equals(Lang.T("filter.new"), StringComparison.OrdinalIgnoreCase)) ||
                    (h.IsMissing && filter.Equals(Lang.T("filter.missing"), StringComparison.OrdinalIgnoreCase)));

            view = Sort(view);

            var selectedIps = new HashSet<uint>(list.SelectedItems.Cast<ListViewItem>().Where(i => i.Tag is HostInfo x && !x.IsMissing).Select(i => ((HostInfo)i.Tag).Ip));
            var focusedKey = KeyOf(list.FocusedItem);
            int topIndex = list.TopItem?.Index ?? 0;
            list.BeginUpdate();
            list.Items.Clear();
            foreach (var h in view)
            {
                var name = string.IsNullOrEmpty(h.Name) ? h.IpText : h.Name;
                var baseKey = h.IsMissing ? "missing" : h.IsSelf ? "self" : h.IsGateway ? "gateway" : "alive";
                bool isOpen = !h.IsMissing && expanded.Contains(h.Ip);
                var item = new ListViewItem(name) { Tag = h, ImageKey = h.Details.Count == 0 ? baseKey : baseKey + (isOpen ? "_e" : "_c") };
                item.SubItems.Add(h.IpText);
                item.SubItems.Add(h.TypeText);
                item.SubItems.Add(h.Os);
                item.SubItems.Add(h.Vendor);
                item.SubItems.Add(h.MacText);
                item.SubItems.Add(h.Workgroup);
                item.SubItems.Add(h.IsMissing ? Lang.T("tip.missing_short") : h.ServicesText);
                item.SubItems.Add(Notes.Get(h));
                item.SubItems.Add(h.IsMissing || h.PingMs < 0 ? "-" : h.PingMs == 0 ? "<1" : h.PingMs.ToString());
                if (h.IsMissing)
                {
                    item.ForeColor = Theme.MissingHost;
                    item.ToolTipText = Lang.T("tip.missing", history.When?.ToString("g") ?? "");
                }
                else
                {
                    item.ToolTipText = Tooltip(h);
                    if (h.HasWarning) item.ForeColor = Theme.Warning;
                    if (h.IsNew)
                    {
                        item.ForeColor = Theme.NewHost;
                        item.Font = new Font(list.Font, FontStyle.Bold);
                        item.ToolTipText = (Lang.T("tip.new") + "\n" + item.ToolTipText).Trim();
                    }
                }
                if (!h.IsMissing && selectedIps.Contains(h.Ip)) item.Selected = true;
                list.Items.Add(item);
                if (KeyOf(item) == focusedKey) item.Focused = true;

                if (!isOpen) continue;
                foreach (var d in h.Details)
                {
                    var child = new ListViewItem(d.Text) { Tag = new ServiceRow { Host = h, Detail = d }, ImageKey = ServiceIconKey(d), IndentCount = 1 };
                    for (int c = 1; c < list.Columns.Count; c++) child.SubItems.Add("");
                    child.ToolTipText = d.Text + "\n" + HostActions.ServiceTarget(h, d);
                    list.Items.Add(child);
                    if (KeyOf(child) == focusedKey) { child.Focused = true; child.Selected = true; }
                }
            }
            list.EndUpdate();
            if (list.Items.Count > 0) try { list.TopItem = list.Items[Math.Min(topIndex, list.Items.Count - 1)]; } catch { }
        }

        private IEnumerable<HostInfo> Sort(IEnumerable<HostInfo> src)
        {
            Func<HostInfo, IComparable> key = sortColumn switch
            {
                ColName => h => string.IsNullOrEmpty(h.Name) ? "￿" : h.Name.ToLowerInvariant(),
                ColVendor => h => string.IsNullOrEmpty(h.Vendor) ? "￿" : h.Vendor.ToLowerInvariant(),
                ColType => h => h.TypeText.Length == 0 ? "￿" : h.TypeText.ToLowerInvariant(),
                ColOs => h => h.Os.Length == 0 ? "￿" : h.Os.ToLowerInvariant(),
                ColMac => h => h.MacText,
                ColWorkgroup => h => string.IsNullOrEmpty(h.Workgroup) ? "￿" : h.Workgroup.ToLowerInvariant(),
                ColServices => h => (int)h.Services,
                ColNote => h => Notes.Get(h) is var n && n.Length > 0 ? n.ToLowerInvariant() : "￿",
                ColPing => h => h.PingMs < 0 ? long.MaxValue : h.PingMs,
                _ => h => h.Ip,
            };
            var ordered = sortAsc ? src.OrderBy(key).ThenBy(h => h.Ip) : src.OrderByDescending(key).ThenBy(h => h.Ip);
            return ordered;
        }

        private IEnumerable<HostInfo> Selected()
        {
            var seen = new HashSet<uint>();
            foreach (ListViewItem i in list.SelectedItems)
            {
                var h = i.Tag as HostInfo ?? (i.Tag as ServiceRow)?.Host;
                if (h != null && !h.IsMissing && seen.Add(h.Ip)) yield return h;
            }
        }

        private void ToggleExpand(HostInfo h)
        {
            if (!expanded.Remove(h.Ip)) expanded.Add(h.Ip);
            RebuildList();
        }

        private static string KeyOf(ListViewItem i) =>
            i?.Tag is HostInfo h ? (h.IsMissing ? "m" : "h") + h.Ip : i?.Tag is ServiceRow r ? $"s{r.Host.Ip}:{r.Detail.Def.Port}" : null;

        private static string ServiceIconKey(ServiceDetail d)
        {
            if (d.Def.IsWeb) return "svc_web";
            switch (d.Def.Svc)
            {
                case Service.Ssh: case Service.Telnet: return "svc_term";
                case Service.Smb: case Service.Ftp: return "svc_folder";
                case Service.Rdp: case Service.Vnc: return "svc_rdp";
                case Service.Printer: return "svc_printer";
                default: return "svc_other";
            }
        }

        /// <summary>Service rows span the whole width (like a tree node), drawn as a link.</summary>
        private void DrawServiceRow(Graphics g, ListViewItem item, ServiceRow sr)
        {
            var row = item.Bounds;
            bool sel = item.Selected;
            var bg = RowBack(item);
            var fg = RowFore(item, Theme.Link);
            using (var b = new SolidBrush(bg)) g.FillRectangle(b, row);

            var iconRect = list.GetItemRect(item.Index, ItemBoundsPortion.Icon);
            int x = iconRect.Left;
            int y = row.Top + (row.Height - icons.ImageSize.Height) / 2;
            icons.Draw(g, x, y, icons.Images.IndexOfKey(item.ImageKey));
            x += iconSz + LogicalToDeviceUnits(4);

            var textRect = new Rectangle(x, row.Top, Math.Max(0, row.Right - x - 4), row.Height);
            TextRenderer.DrawText(g, item.Text, list.Font, textRect, fg,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine |
                TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
        }

        /// <summary>Double-click / Enter: opens what the device offers - a recognised admin panel, RDP, a web page, SSH or a share.</summary>
        private void DefaultAction()
        {
            var h = Selected().FirstOrDefault();
            if (h == null) return;
            var panel = h.Details.FirstOrDefault(d => d.Def.IsWeb && d.Product.Length > 0);
            if (panel != null) HostActions.OpenService(h, panel);
            else if (h.Has(Service.Rdp)) HostActions.Rdp(h);
            else if (h.Details.Any(d => d.Def.IsWeb)) HostActions.BestWeb(h);
            else if (h.Has(Service.Ssh)) HostActions.Ssh(h);
            else if (h.Has(Service.Smb)) HostActions.Explore(h);
            else OpenPing(false);
        }

        /// <summary>Full information about a device (the columns can be too narrow).</summary>
        private string Tooltip(HostInfo h)
        {
            var sb = new System.Text.StringBuilder();
            if (h.IsSelf) sb.AppendLine(Lang.T("tip.this_pc"));
            else if (h.IsGateway) sb.AppendLine(Lang.T("tip.gateway"));
            if (h.Name.Length > 0) sb.AppendLine(h.NameSource.Length > 0 ? Lang.T("tip.name_source", h.Name, h.NameSource) : h.Name);
            if (h.TypeText.Length > 0 || h.Os.Length > 0) sb.AppendLine(string.Join(" · ", new[] { h.TypeText, h.Os, h.Ttl > 0 ? "TTL " + h.Ttl : "" }.Where(x => x.Length > 0)));
            if (h.Workgroup.Length > 0) sb.AppendLine(Lang.T("col.workgroup") + ": " + h.Workgroup);
            if (h.ServicesText.Length > 0) sb.AppendLine(Lang.T("col.services") + ": " + h.ServicesText);
            if (h.SameMac.Count > 0) sb.AppendLine("⚠ " + Lang.T("tip.same_mac", string.Join(", ", h.SameMac.Select(IpRange.ToStr))));
            if (h.ConflictMac != null) sb.AppendLine("⚠ " + Lang.T("tip.conflict", h.MacText, h.ConflictMacText));
            foreach (var d in h.Details.Where(d => d.CertWarning)) sb.AppendLine("⚠ " + d.Def.Label + ":" + d.Def.Port + " " + d.CertText);
            return sb.ToString().TrimEnd();
        }

        private void ExpandAll(bool expand)
        {
            if (expand) { foreach (var h in hosts) if (h.Details.Count > 0) expanded.Add(h.Ip); }
            else expanded.Clear();
            RebuildList();
        }


        private void ShowOptions()
        {
            using var f = new OptionsForm();
            if (f.ShowDialog(this) == DialogResult.OK) mScanPorts.Checked = Settings.ScanPorts;
        }

        private void SetTheme(string theme)
        {
            Settings.Theme = theme;
            Settings.Save();
            ApplyTheme();
            ApplyTexts();
        }

        private void ApplyTheme()
        {
            Theme.Resolve(Settings.Theme);
            Theme.Apply(this);
            int sz = LogicalToDeviceUnits(16), big = LogicalToDeviceUnits(18);
            var accent = Theme.Link;
            Bitmap I(string n, int z) => ToolIcons.Get(n, z, accent);
            btnScan.Image = ToolIcons.Get(scanning ? "stop" : "scan", sz, Color.White);
            btnLocal.Image = I("home", sz);
            btnRescan.Image = I("rescan", sz);
            btnFav.Image = I("star", sz);
            btnExport.Image = I("export", sz);
            lblFilterIcon.Image = ToolIcons.Get("filter", sz, Theme.Muted);
            aWeb.Image = I("web", big);
            aRdp.Image = I("rdp", big);
            aSsh.Image = I("ssh", big);
            aSmb.Image = I("smb", big);
            aPing.Image = I("ping", big);
            aTrace.Image = I("tracert", big);
            aWol.Image = I("wol", big);
            aPower.Image = ToolIcons.Get("power", big, Color.FromArgb(207, 34, 46));
            btnExpand.Image = I("expand", big);
            btnCollapse.Image = I("collapse", big);
            aCopy.Image = I("copy", big);
            aPalette.Image = ToolIcons.Get("command", big, Theme.Muted);
            foreach (var b in typeIcons.Values) b.Dispose();
            typeIcons.Clear();
            ApplyColumns();
            list.Invalidate();
        }

        // ------------------------------------------------------------------ misc

        private void DoExport(string kind)
        {
            if (hosts.Count == 0 && missing.Count == 0) { MessageBox.Show(Lang.T("msg.nothing_to_export"), AppInfo.Name); return; }
            using var dlg = new SaveFileDialog
            {
                Filter = kind == "html" ? "HTML (*.html)|*.html" : kind == "json" ? "JSON (*.json)|*.json" : "CSV (*.csv)|*.csv",
                FileName = $"vihor-scan-{DateTime.Now:yyyyMMdd-HHmm}." + kind
            };
            if (dlg.ShowDialog(this) != DialogResult.OK) return;
            var rows = Sort(hosts.Concat(missing)).ToList(); // includes devices missing since the last scan (Status column)
            try
            {
                if (kind == "html") Export.Html(dlg.FileName, rows, lastScannedRange);
                else if (kind == "json") Export.Json(dlg.FileName, rows, lastScannedRange);
                else Export.Csv(dlg.FileName, rows);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, AppInfo.Name, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void SaveDiagnostics()
        {
            if (Diag.IsEmpty) { MessageBox.Show(Lang.T("msg.nothing_to_export"), AppInfo.Name); return; }
            using var dlg = new SaveFileDialog
            {
                Filter = "Text (*.txt)|*.txt",
                FileName = $"vihor-diagnostics-{DateTime.Now:yyyyMMdd-HHmm}.txt"
            };
            if (dlg.ShowDialog(this) != DialogResult.OK) return;
            try { Diag.Save(dlg.FileName); }
            catch (Exception ex) { MessageBox.Show(ex.Message, AppInfo.Name, MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }

        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.F5) { ToggleScan(); e.Handled = true; }
            if (e.KeyCode == Keys.Escape && scanning) { cts?.Cancel(); e.Handled = true; }
            if (e.Control && e.KeyCode == Keys.F) { txtFilter.Focus(); e.Handled = true; }
            if (e.Control && e.KeyCode == Keys.K) { ShowPalette(); e.Handled = e.SuppressKeyPress = true; }
            if (e.Control && (e.KeyCode == Keys.Oemplus || e.KeyCode == Keys.Add)) { ExpandAll(true); e.Handled = e.SuppressKeyPress = true; }
            if (e.Control && (e.KeyCode == Keys.OemMinus || e.KeyCode == Keys.Subtract)) { ExpandAll(false); e.Handled = e.SuppressKeyPress = true; }
        }

        private void OnClosing(object sender, FormClosingEventArgs e)
        {
            cts?.Cancel();
            Settings.WindowMaximized = WindowState == FormWindowState.Maximized;
            var b = WindowState == FormWindowState.Normal ? Bounds : RestoreBounds;
            Settings.WindowWidth = b.Width;
            Settings.WindowHeight = b.Height;
            if (!string.IsNullOrWhiteSpace(cmbRange.Text)) Settings.LastRange = cmbRange.Text.Trim();
            SaveColumnLayout();
            Settings.Save();
        }

        // ------------------------------------------------------------------ favourites, palette, updates

        /// <summary>Favourites: saved ranges or devices ("name|range"); click = load and scan.</summary>
        private void FillFavorites(ToolStripItemCollection items)
        {
            items.Clear();
            var favs = Settings.Favorites.Select(f => f.Split(new[] { '|' }, 2)).Where(p => p.Length == 2).ToList();
            foreach (var p in favs)
            {
                var range = p[1];
                var item = new ToolStripMenuItem(p[0], ToolIcons.Get("star", LogicalToDeviceUnits(16), Theme.Link)) { ToolTipText = range };
                item.Click += (s, e) => { cmbRange.Text = range; if (!scanning) StartScan(); };
                items.Add(item);
            }
            if (favs.Count == 0) items.Add(new ToolStripMenuItem(Lang.T("fav.empty")) { Enabled = false });
            items.Add(new ToolStripSeparator());
            items.Add(new ToolStripMenuItem(Lang.T("fav.add_range"), null, (s, e) => AddFavorite(cmbRange.Text.Trim(), cmbRange.Text.Trim())));
            var sel = Selected().ToList();
            var addSel = new ToolStripMenuItem(Lang.T("fav.add_selected"), null, (s, e) =>
            {
                var first = sel.FirstOrDefault();
                AddFavorite(string.Join(", ", sel.Select(h => h.IpText)), sel.Count == 1 && first.Name.Length > 0 ? first.Name : string.Join(", ", sel.Select(h => h.IpText)));
            }) { Enabled = sel.Count > 0 };
            items.Add(addSel);
            if (favs.Count > 0)
            {
                var remove = new ToolStripMenuItem(Lang.T("fav.remove"));
                foreach (var f in Settings.Favorites.ToList())
                {
                    var entry = f;
                    remove.DropDownItems.Add(new ToolStripMenuItem(entry.Split('|')[0], null, (s, e) => { Settings.Favorites.Remove(entry); Settings.Save(); }));
                }
                items.Add(remove);
            }
        }

        private void AddFavorite(string range, string suggested)
        {
            if (string.IsNullOrWhiteSpace(range)) return;
            IpRange.Parse(range, out var err);
            if (err != null) { MessageBox.Show(Lang.T("msg.bad_range", err), AppInfo.Name, MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
            var name = Prompt.Ask(this, Lang.T("btn.favorites"), Lang.T("fav.name", range), suggested);
            if (string.IsNullOrWhiteSpace(name)) return;
            Settings.Favorites.RemoveAll(f => f.StartsWith(name.Replace("|", "/") + "|", StringComparison.Ordinal));
            Settings.Favorites.Add(name.Replace("|", "/") + "|" + range);
            Settings.Save();
        }

        /// <summary>Ctrl+K: every menu command, toolbar tool and device action, searchable by typing.</summary>
        private void ShowPalette()
        {
            var cmds = new List<CommandPalette.Command>();
            CommandPalette.Collect(menu.Items, "", cmds);
            void Tool(ToolStripItem i, Action a) { if (i.Enabled) cmds.Add(new CommandPalette.Command { Text = i.Text, Path = i.ToolTipText ?? "", Shortcut = "", Run = a }); }
            Tool(aWeb, aWeb.PerformClick); Tool(aRdp, aRdp.PerformClick); Tool(aSsh, aSsh.PerformClick); Tool(aSmb, aSmb.PerformClick);
            Tool(aPing, aPing.PerformClick); Tool(aTrace, aTrace.PerformClick); Tool(aWol, aWol.PerformClick);
            if (Selected().Any())
            {
                foreach (var i in new[] { cCopyIp, cCopyMac, cCopyRow, cNote, cShutdown, cRestart, cAdvancedRescan })
                    if (i.Enabled) cmds.Add(new CommandPalette.Command { Text = (i == cCopyIp || i == cCopyMac || i == cCopyRow ? cCopy.Text + " › " : "") + i.Text, Path = "", Shortcut = "", Run = i.PerformClick });
            }
            foreach (var f in Settings.Favorites.Select(x => x.Split(new[] { '|' }, 2)).Where(p => p.Length == 2))
            {
                var range = f[1];
                cmds.Add(new CommandPalette.Command { Text = Lang.T("btn.favorites") + " › " + f[0], Path = range, Shortcut = "", Run = () => { cmbRange.Text = range; if (!scanning) StartScan(); } });
            }
            var p = new CommandPalette(cmds);
            var at = PointToScreen(new Point((ClientSize.Width - p.Width) / 2, tools.Bottom));
            p.Location = at;
            p.Show(this);
        }

        private async System.Threading.Tasks.Task CheckUpdate(bool manual)
        {
            var v = await Updater.CheckAsync(manual);
            if (v != null)
            {
                lblUpdate.Text = Lang.T("status.update", v);
                lblUpdate.Visible = true;
                if (manual && MessageBox.Show(Lang.T("msg.update", v), AppInfo.Name, MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
                    OpenUrl(Updater.ReleasesUrl);
            }
            else if (manual) MessageBox.Show(Lang.T("msg.up_to_date", AppInfo.Version), AppInfo.Name, MessageBoxButtons.OK, MessageBoxIcon.Information);
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

    /// <summary>Child row under a host: one detected service.</summary>
    internal sealed class ServiceRow
    {
        public HostInfo Host;
        public ServiceDetail Detail;
    }

    internal static class NativeHeader
    {
        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        internal struct RECT { public int Left, Top, Right, Bottom; }
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        internal static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        internal static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);
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
