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
        private readonly ToolStripButton btnExpand = new ToolStripButton();
        private readonly ToolStripButton btnCollapse = new ToolStripButton();
        private readonly ToolStripButton btnRescan = new ToolStripButton();
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
        private ToolStripMenuItem mScan, mScanStart, mScanLocal, mScanPorts, mRescan, mDiag;
        private ToolStripMenuItem mView, mExpandAll, mCollapseAll, mShowWorkgroup, mTheme, mThemeSystem, mThemeLight, mThemeDark;
        private ToolStripMenuItem mSettings, mOptions, mLanguage;
        private ToolStripMenuItem mHelp, mWebsite, mGithub, mAbout;

        // context menu items
        private ToolStripMenuItem cCopy, cCopyIp, cCopyName, cCopyMac, cCopyVendor, cCopyRow;
        private ToolStripMenuItem cRdp, cSsh, cHttp, cHttps, cHttpAlt, cFtp, cExplore;
        private ToolStripMenuItem cTools, cPing, cTracert, cWol, cRescan, cShutdown, cRestart, cAbort;
        private ToolStripMenuItem cOpenService, cExpandAll, cCollapseAll;
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

        private const int ColName = 0, ColIp = 1, ColVendor = 2, ColMac = 3, ColWorkgroup = 4, ColServices = 5, ColPing = 6;
        private int workgroupWidth;

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
            mRescan = new ToolStripMenuItem("", null, async (s, e) => await RescanSelected()) { ShortcutKeys = Keys.Control | Keys.R };
            mScan = new ToolStripMenuItem("", null, mScanStart, mScanLocal, mRescan, new ToolStripSeparator(), mScanPorts);

            mExpandAll = new ToolStripMenuItem("", null, (s, e) => ExpandAll(true)) { ShortcutKeyDisplayString = "Ctrl++" };
            mCollapseAll = new ToolStripMenuItem("", null, (s, e) => ExpandAll(false)) { ShortcutKeyDisplayString = "Ctrl+-" };
            mShowWorkgroup = new ToolStripMenuItem("", null, (s, e) => { Settings.ShowWorkgroup = !Settings.ShowWorkgroup; ApplyWorkgroupColumn(); Settings.Save(); })
            { Checked = Settings.ShowWorkgroup };
            mThemeSystem = new ToolStripMenuItem("", null, (s, e) => SetTheme("system"));
            mThemeLight = new ToolStripMenuItem("", null, (s, e) => SetTheme("light"));
            mThemeDark = new ToolStripMenuItem("", null, (s, e) => SetTheme("dark"));
            mTheme = new ToolStripMenuItem("", null, mThemeSystem, mThemeLight, mThemeDark);
            mView = new ToolStripMenuItem("", null, mExpandAll, mCollapseAll, new ToolStripSeparator(), mShowWorkgroup, mTheme);

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
            mHelp = new ToolStripMenuItem("", null, mWebsite, mGithub, new ToolStripSeparator(), mDiag, new ToolStripSeparator(), mAbout);

            menu.Items.AddRange(new ToolStripItem[] { mFile, mScan, mView, mSettings, mHelp });
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
            btnScan.Tag = "accent"; // keeps its white text in the dark theme

            btnLocal.DisplayStyle = ToolStripItemDisplayStyle.Text;
            btnLocal.ButtonClick += (s, e) => cmbRange.Text = IpRange.DefaultRange(IpRange.GetLocalSubnets());
            btnLocal.DropDownOpening += (s, e) => FillAdapterMenu(btnLocal.DropDownItems);
            btnLocal.DropDownItems.Add(new ToolStripMenuItem("…")); // placeholder so the arrow is active

            btnExport.DisplayStyle = ToolStripItemDisplayStyle.Text;
            btnExport.Click += (s, e) => DoExport(false);

            btnExpand.DisplayStyle = btnCollapse.DisplayStyle = btnRescan.DisplayStyle = ToolStripItemDisplayStyle.Image;
            btnExpand.Click += (s, e) => ExpandAll(true);
            btnCollapse.Click += (s, e) => ExpandAll(false);
            btnRescan.Click += async (s, e) => await RescanSelected();

            txtFilter.AutoSize = false;
            txtFilter.Width = LogicalToDeviceUnits(180);
            txtFilter.TextChanged += (s, e) => RebuildList();

            tools.Items.AddRange(new ToolStripItem[]
            {
                lblRange, cmbRange, btnScan, btnLocal, new ToolStripSeparator(), btnExport,
                new ToolStripSeparator(), btnExpand, btnCollapse, btnRescan,
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
            list.Columns.Add("", workgroupWidth = LogicalToDeviceUnits(110));
            list.Columns.Add("", LogicalToDeviceUnits(150));
            list.Columns.Add("", LogicalToDeviceUnits(65), HorizontalAlignment.Right);
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
            list.DrawItem += (s, e) => e.DrawDefault = !(e.Item.Tag is ServiceRow);
            list.DrawSubItem += (s, e) =>
            {
                if (e.Item.Tag is ServiceRow sr) DrawServiceRow(e.Graphics, e.Item, sr);
                else e.DrawDefault = true;
            };
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
                else if (item?.Tag is HostInfo h) { if (h.Details.Count > 0) ToggleExpand(h); else DefaultAction(); }
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
                if (e.Control && e.KeyCode == Keys.C) { HostActions.Copy(string.Join(Environment.NewLine, Selected().Select(h => h.IpText))); e.Handled = true; }
                if (e.Control && e.KeyCode == Keys.A) { foreach (ListViewItem i in list.Items) i.Selected = true; e.Handled = true; }
            };
            list.ContextMenuStrip = ctx;
            list.ClientSizeChanged += (s, e) => FitColumns();
            list.ColumnWidthChanged += (s, e) =>
            {
                if (e.ColumnIndex == ColWorkgroup && Settings.ShowWorkgroup && list.Columns[ColWorkgroup].Width > 0) workgroupWidth = list.Columns[ColWorkgroup].Width;
                if (e.ColumnIndex != ColServices) FitColumns();
            };
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

            cPing = new ToolStripMenuItem("", null, (s, e) => HostActions.Ping(H()));
            cTracert = new ToolStripMenuItem("", null, (s, e) => HostActions.Tracert(H()));
            cWol = new ToolStripMenuItem("", null, (s, e) => { foreach (var h in Selected()) HostActions.WakeOnLan(h); });
            cShutdown = new ToolStripMenuItem("", null, (s, e) => HostActions.Shutdown(H(), false));
            cRestart = new ToolStripMenuItem("", null, (s, e) => HostActions.Shutdown(H(), true));
            cAbort = new ToolStripMenuItem("", null, (s, e) => HostActions.AbortShutdown(H()));
            cTools = new ToolStripMenuItem("", null, cPing, cTracert, cWol, new ToolStripSeparator(), cShutdown, cRestart, cAbort);

            cRescan = new ToolStripMenuItem("", null, async (s, e) => await RescanSelected());
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
                cCopy, cTools, new ToolStripSeparator(), cRescan, cExpandAll, cCollapseAll
            });
            ctx.Items.Insert(0, cOpenServiceSep);
            ctx.Items.Insert(0, cOpenService);

            ctx.Opening += (s, e) =>
            {
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
            mRescan.Text = Lang.T("menu.rescan_selected");
            mView.Text = Lang.T("menu.view");
            mExpandAll.Text = Lang.T("ctx.expand_all");
            mCollapseAll.Text = Lang.T("ctx.collapse_all");
            mShowWorkgroup.Text = Lang.T("menu.show_workgroup");
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
            btnLocal.Text = Lang.T("btn.my_network");
            btnLocal.ToolTipText = Lang.T("tip.my_network");
            btnExport.Text = Lang.T("btn.export");
            btnExpand.Text = btnExpand.ToolTipText = Lang.T("ctx.expand_all") + " (Ctrl++)";
            btnCollapse.Text = btnCollapse.ToolTipText = Lang.T("ctx.collapse_all") + " (Ctrl+-)";
            btnRescan.Text = btnRescan.ToolTipText = Lang.T("menu.rescan_selected") + " (Ctrl+R)";
            lblFilter.Text = Lang.T("label.filter");
            cmbRange.ToolTipText = Lang.T("tip.range");

            list.Columns[ColName].Text = Lang.T("col.name");
            list.Columns[ColIp].Text = Lang.T("col.ip");
            list.Columns[ColVendor].Text = Lang.T("col.vendor");
            list.Columns[ColMac].Text = Lang.T("col.mac");
            list.Columns[ColWorkgroup].Text = Lang.T("col.workgroup");
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
            cExpandAll.Text = Lang.T("ctx.expand_all");
            cCollapseAll.Text = Lang.T("ctx.collapse_all");

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

        private async System.Threading.Tasks.Task RescanSelected()
        {
            var sel = Selected().ToList();
            if (sel.Count == 0 || scanning) return;
            var scanner = new Scanner(ScanOptions.FromSettings());
            lblStatus.Text = Lang.T("status.rescanning");
            foreach (var old in sel)
            {
                var fresh = await System.Threading.Tasks.Task.Run(() => scanner.ScanHostAsync(old.Ip, CancellationToken.None));
                int idx = hosts.FindIndex(h => h.Ip == old.Ip);
                if (idx < 0) continue;
                if (fresh != null) { fresh.IsNew = old.IsNew; hosts[idx] = fresh; } else hosts.RemoveAt(idx);
            }
            RebuildList();
            UpdateStatus();
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
                    h.ServicesText.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0 ||
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
                item.SubItems.Add(h.Vendor);
                item.SubItems.Add(h.MacText);
                item.SubItems.Add(h.Workgroup);
                item.SubItems.Add(h.IsMissing ? Lang.T("tip.missing_short") : h.ServicesText);
                item.SubItems.Add(h.IsMissing || h.PingMs < 0 ? "-" : h.PingMs == 0 ? "<1" : h.PingMs.ToString());
                if (h.IsMissing)
                {
                    item.ForeColor = Theme.MissingHost;
                    item.ToolTipText = Lang.T("tip.missing", history.When?.ToString("g") ?? "");
                }
                else
                {
                    item.ToolTipText = h.IsSelf ? Lang.T("tip.this_pc") : h.IsGateway ? Lang.T("tip.gateway") :
                                       string.IsNullOrEmpty(h.Workgroup) ? "" : Lang.T("col.workgroup") + ": " + h.Workgroup;
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
                ColMac => h => h.MacText,
                ColWorkgroup => h => string.IsNullOrEmpty(h.Workgroup) ? "￿" : h.Workgroup.ToLowerInvariant(),
                ColServices => h => (int)h.Services,
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
            var bg = sel ? (list.Focused ? SystemColors.Highlight : Theme.Hover) : list.BackColor;
            var fg = sel && list.Focused ? SystemColors.HighlightText : Theme.Link;
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

        private void ExpandAll(bool expand)
        {
            if (expand) { foreach (var h in hosts) if (h.Details.Count > 0) expanded.Add(h.Ip); }
            else expanded.Clear();
            RebuildList();
        }

        private void ApplyWorkgroupColumn()
        {
            mShowWorkgroup.Checked = Settings.ShowWorkgroup;
            list.Columns[ColWorkgroup].Width = Settings.ShowWorkgroup ? Math.Max(LogicalToDeviceUnits(60), workgroupWidth) : 0;
            FitColumns();
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
            int sz = LogicalToDeviceUnits(16);
            btnExpand.Image = ToolIcon(sz, true);
            btnCollapse.Image = ToolIcon(sz, false);
            btnRescan.Image = RescanIcon(sz);
            ApplyWorkgroupColumn();
            list.Invalidate();
        }

        /// <summary>Two stacked chevrons: pointing down = expand all, up = collapse all.</summary>
        private static Bitmap ToolIcon(int sz, bool down)
        {
            var b = new Bitmap(sz, sz);
            using var g = Graphics.FromImage(b);
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using var pen = new Pen(Theme.Fore, Math.Max(1.4f, sz / 10f));
            float l = sz * 0.22f, r = sz * 0.78f, c = sz / 2f;
            foreach (var y in new[] { sz * 0.28f, sz * 0.58f })
            {
                float tip = down ? y + sz * 0.16f : y, top = down ? y : y + sz * 0.16f;
                g.DrawLines(pen, new[] { new PointF(l, top), new PointF(c, tip), new PointF(r, top) });
            }
            return b;
        }

        /// <summary>Circular arrow (rescan).</summary>
        private static Bitmap RescanIcon(int sz)
        {
            var b = new Bitmap(sz, sz);
            using var g = Graphics.FromImage(b);
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using var pen = new Pen(Theme.Fore, Math.Max(1.4f, sz / 10f));
            float p = sz * 0.2f;
            g.DrawArc(pen, p, p, sz - 2 * p, sz - 2 * p, 300, 280);
            float ax = sz - p, ay = sz / 2f - sz * 0.12f;
            g.DrawLines(pen, new[] { new PointF(ax - sz * 0.16f, ay - sz * 0.02f), new PointF(ax, ay), new PointF(ax + sz * 0.05f, ay - sz * 0.17f) });
            return b;
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
