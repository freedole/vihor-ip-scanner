using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Vihor.Scanning;

namespace Vihor
{
    /// <summary>
    /// Options: scan speed / thoroughness, services to detect, own ports and port ranges.
    /// The same window is used for "Advanced rescan" of one device: then nothing is saved and
    /// the chosen settings are returned in <see cref="Profile"/>.
    /// </summary>
    internal sealed class OptionsForm : Form
    {
        private readonly TrackBar speed = new TrackBar();
        private readonly Label speedInfo = new Label();
        private readonly CheckedListBox services = new CheckedListBox();
        private readonly TextBox customPorts = new TextBox();
        private readonly TextBox portRanges = new TextBox();
        private readonly Label rangeInfo = new Label();
        private readonly CheckBox rangesOnlyRescan = new CheckBox();
        private readonly ComboBox sshClient = new ComboBox();
        private readonly bool deviceMode;

        /// <summary>The settings chosen in this window (valid after OK / Scan).</summary>
        public ScanProfile Profile { get; private set; }

        /// <param name="device">null = global Options (saved); otherwise Advanced rescan of this device (not saved).</param>
        public OptionsForm(string device = null)
        {
            deviceMode = device != null;
            var start = ScanProfile.FromSettings();

            Text = deviceMode ? Lang.T("adv.title", device) : Lang.T("opt.title");
            Icon = MainForm.LoadIcon();
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = MinimizeBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            AutoScaleMode = AutoScaleMode.Dpi;
            Font = SystemFonts.MessageBoxFont;
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
            Padding = new Padding(LogicalToDeviceUnits(16));

            int w = LogicalToDeviceUnits(440);
            var root = new TableLayoutPanel { ColumnCount = 1, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink };

            if (deviceMode)
                root.Controls.Add(Hint(Lang.T("adv.info"), w, 12));

            // --- speed
            root.Controls.Add(Header(Lang.T("opt.speed")));
            speed.Minimum = 1;
            speed.Maximum = 5;
            speed.TickFrequency = 1;
            speed.LargeChange = 1;
            speed.Width = w;
            speed.Value = System.Math.Max(1, System.Math.Min(5, start.Speed));
            speed.ValueChanged += (s, e) => UpdateSpeedInfo();
            root.Controls.Add(speed);

            var ends = new TableLayoutPanel { ColumnCount = 2, Width = w, Height = LogicalToDeviceUnits(36), Margin = Padding.Empty };
            ends.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            ends.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            ends.Controls.Add(new Label { Text = Lang.T("opt.speed_fast"), AutoSize = true, ForeColor = Theme.Muted, Tag = "muted", Anchor = AnchorStyles.Left | AnchorStyles.Top });
            ends.Controls.Add(new Label { Text = Lang.T("opt.speed_thorough"), AutoSize = true, ForeColor = Theme.Muted, Tag = "muted", TextAlign = ContentAlignment.TopRight, Anchor = AnchorStyles.Right | AnchorStyles.Top });
            root.Controls.Add(ends);

            speedInfo.AutoSize = true;
            speedInfo.MaximumSize = new Size(w, 0);
            speedInfo.Margin = new Padding(0, LogicalToDeviceUnits(2), 0, LogicalToDeviceUnits(12));
            root.Controls.Add(speedInfo);
            UpdateSpeedInfo();

            // --- services
            root.Controls.Add(Header(Lang.T("opt.services")));
            services.CheckOnClick = true;
            services.IntegralHeight = false;
            services.BorderStyle = BorderStyle.FixedSingle;
            services.Width = w;
            services.Height = LogicalToDeviceUnits(150);
            foreach (var p in HostInfo.Ports)
                services.Items.Add(new ServiceItem(p), start.ServicePorts.Contains(p.Port));
            root.Controls.Add(services);

            var defaults = new LinkLabel { Text = Lang.T("opt.defaults"), AutoSize = true, Margin = new Padding(0, LogicalToDeviceUnits(4), 0, LogicalToDeviceUnits(12)) };
            defaults.LinkClicked += (s, e) =>
            {
                for (int i = 0; i < services.Items.Count; i++)
                    services.SetItemChecked(i, Settings.DefaultServicePorts.Contains(((ServiceItem)services.Items[i]).Def.Port));
                speed.Value = 3;
            };
            root.Controls.Add(defaults);

            // --- own ports
            root.Controls.Add(Header(Lang.T("opt.custom_ports")));
            customPorts.Width = w;
            customPorts.Text = string.Join(", ", start.CustomPorts);
            root.Controls.Add(customPorts);
            root.Controls.Add(Hint(Lang.T("opt.custom_ports_hint"), w, 12));

            // --- port ranges
            root.Controls.Add(Header(Lang.T("opt.port_ranges")));
            portRanges.Width = w;
            portRanges.Text = start.PortRanges;
            portRanges.TextChanged += (s, e) => UpdateRangeInfo();
            root.Controls.Add(portRanges);
            rangeInfo.AutoSize = true;
            rangeInfo.MaximumSize = new Size(w, 0);
            rangeInfo.Tag = "muted";
            rangeInfo.ForeColor = Theme.Muted;
            rangeInfo.Margin = new Padding(0, LogicalToDeviceUnits(4), 0, LogicalToDeviceUnits(4));
            root.Controls.Add(rangeInfo);
            if (!deviceMode)
            {
                rangesOnlyRescan.Text = Lang.T("opt.ranges_only_rescan");
                rangesOnlyRescan.AutoSize = true;
                rangesOnlyRescan.Checked = start.RangesOnlyRescan;
                rangesOnlyRescan.Margin = new Padding(0, LogicalToDeviceUnits(2), 0, LogicalToDeviceUnits(14));
                root.Controls.Add(rangesOnlyRescan);
            }
            else rangeInfo.Margin = new Padding(0, LogicalToDeviceUnits(4), 0, LogicalToDeviceUnits(14));
            UpdateRangeInfo();

            if (!deviceMode)
            {
                // --- SSH client
                root.Controls.Add(Header(Lang.T("opt.ssh_client")));
                sshClient.DropDownStyle = ComboBoxStyle.DropDownList;
                sshClient.Width = LogicalToDeviceUnits(260);
                sshClient.Items.AddRange(new object[]
                {
                    new Choice("auto", Lang.T("opt.ssh_auto")),
                    new Choice("wt", "Windows Terminal" + (HostActions.WindowsTerminal() == null ? "  (" + Lang.T("opt.not_installed") + ")" : "")),
                    new Choice("putty", "PuTTY" + (HostActions.Putty() == null ? "  (" + Lang.T("opt.not_installed") + ")" : "")),
                    new Choice("cmd", Lang.T("opt.ssh_cmd")),
                });
                sshClient.SelectedItem = sshClient.Items.Cast<Choice>().FirstOrDefault(c => c.Value == Settings.SshClient) ?? sshClient.Items[0];
                sshClient.Margin = new Padding(0, 0, 0, LogicalToDeviceUnits(14));
                root.Controls.Add(sshClient);
            }

            // --- buttons
            var buttons = new FlowLayoutPanel { FlowDirection = FlowDirection.RightToLeft, Width = w, AutoSize = true, Margin = Padding.Empty };
            var cancel = new Button { Text = Lang.T("btn.cancel"), DialogResult = DialogResult.Cancel, AutoSize = true, MinimumSize = new Size(LogicalToDeviceUnits(88), 0) };
            var ok = new Button { Text = deviceMode ? Lang.T("btn.scan") : "OK", AutoSize = true, MinimumSize = new Size(LogicalToDeviceUnits(88), 0) };
            ok.Click += (s, e) => Accept();
            buttons.Controls.Add(cancel);
            buttons.Controls.Add(ok);
            root.Controls.Add(buttons);

            Controls.Add(root);
            AcceptButton = ok;
            CancelButton = cancel;
            Theme.Apply(this);
        }

        private Label Header(string text) => new Label
        {
            Text = text,
            AutoSize = true,
            Font = new Font(Font, FontStyle.Bold),
            Margin = new Padding(0, 0, 0, LogicalToDeviceUnits(4)),
        };

        private Label Hint(string text, int width, int bottom) => new Label
        {
            Text = text,
            AutoSize = true,
            ForeColor = Theme.Muted,
            Tag = "muted",
            MaximumSize = new Size(width, 0),
            Margin = new Padding(0, LogicalToDeviceUnits(4), 0, LogicalToDeviceUnits(bottom)),
        };

        private void UpdateSpeedInfo() => speedInfo.Text = Lang.T("opt.speed_" + speed.Value);

        private void UpdateRangeInfo()
        {
            var ports = Settings.ParseRanges(portRanges.Text, out var err);
            if (err != null) { rangeInfo.Text = Lang.T("opt.invalid_ports", err); return; }
            if (ports.Count == 0) { rangeInfo.Text = Lang.T("opt.port_ranges_hint"); return; }
            rangeInfo.Text = Lang.T("opt.port_ranges_count", ports.Count) +
                             (ports.Count > Settings.MaxRangePorts ? "  " + Lang.T("opt.ranges_too_many", Settings.MaxRangePorts) : "");
        }

        private void Accept()
        {
            var ranges = Settings.ParseRanges(portRanges.Text, out var err);
            if (err != null)
            {
                MessageBox.Show(this, Lang.T("opt.invalid_ports", err), Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                portRanges.Focus();
                return;
            }
            if (ranges.Count > Settings.MaxRangePorts)
            {
                MessageBox.Show(this, Lang.T("opt.ranges_too_many", Settings.MaxRangePorts), Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                portRanges.Focus();
                return;
            }
            if (ranges.Count > Settings.WarnRangePorts && !Settings.HideRangeWarning && !RangeWarning.Confirm(this, ranges.Count))
                return;

            Profile = new ScanProfile
            {
                Speed = speed.Value,
                ServicePorts = services.CheckedItems.Cast<ServiceItem>().Select(i => i.Def.Port).ToList(),
                CustomPorts = Settings.ParsePorts(customPorts.Text),
                PortRanges = NormalizeRanges(portRanges.Text),
                RangesOnlyRescan = deviceMode || rangesOnlyRescan.Checked,
            };
            if (!deviceMode)
            {
                Settings.SshClient = (sshClient.SelectedItem as Choice)?.Value ?? "auto";
                Profile.SaveToSettings();
            }
            DialogResult = DialogResult.OK;
        }

        /// <summary>"3000 -3500,4000-5000" -> "3000-3500, 4000-5000".</summary>
        private static string NormalizeRanges(string text)
        {
            var parts = new List<string>();
            foreach (var raw in (text ?? "").Split(new[] { ',', ';' }, System.StringSplitOptions.RemoveEmptyEntries))
            {
                var p = string.Join("-", raw.Split(new[] { '-', '–' }, System.StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim()));
                if (p.Length > 0) parts.Add(p);
            }
            return string.Join(", ", parts);
        }

        private sealed class Choice
        {
            public readonly string Value, Text;
            public Choice(string value, string text) { Value = value; Text = text; }
            public override string ToString() => Text;
        }

        private sealed class ServiceItem
        {
            public readonly PortDef Def;
            public ServiceItem(PortDef def) { Def = def; }

            public override string ToString()
            {
                var key = "svcname." + Def.Port;
                var name = Lang.T(key);
                if (name == key) name = Def.Label;
                return $"{name}  (TCP {Def.Port})";
            }
        }
    }

    /// <summary>Warning before scanning many ports from ranges, with "Don't show again".</summary>
    internal sealed class RangeWarning : Form
    {
        private RangeWarning(int count)
        {
            Text = Lang.T("warn.title");
            Icon = MainForm.LoadIcon();
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = MinimizeBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            AutoScaleMode = AutoScaleMode.Dpi;
            Font = SystemFonts.MessageBoxFont;
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
            Padding = new Padding(LogicalToDeviceUnits(16));

            int w = LogicalToDeviceUnits(400);
            var root = new TableLayoutPanel { ColumnCount = 2, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink };
            var icon = new PictureBox { Image = SystemIcons.Warning.ToBitmap(), SizeMode = PictureBoxSizeMode.AutoSize, Margin = new Padding(0, 0, LogicalToDeviceUnits(12), 0) };
            var text = new Label { Text = Lang.T("warn.ranges", count), AutoSize = true, MaximumSize = new Size(w, 0) };
            var dontShow = new CheckBox { Text = Lang.T("warn.dont_show"), AutoSize = true, Margin = new Padding(0, LogicalToDeviceUnits(14), 0, LogicalToDeviceUnits(10)) };
            var buttons = new FlowLayoutPanel { FlowDirection = FlowDirection.RightToLeft, AutoSize = true, Dock = DockStyle.Fill, Margin = Padding.Empty };
            var cancel = new Button { Text = Lang.T("btn.cancel"), DialogResult = DialogResult.Cancel, AutoSize = true, MinimumSize = new Size(LogicalToDeviceUnits(88), 0) };
            var ok = new Button { Text = Lang.T("btn.continue"), DialogResult = DialogResult.OK, AutoSize = true, MinimumSize = new Size(LogicalToDeviceUnits(88), 0) };
            ok.Click += (s, e) => { if (dontShow.Checked) { Settings.HideRangeWarning = true; Settings.Save(); } };
            buttons.Controls.Add(cancel);
            buttons.Controls.Add(ok);

            root.Controls.Add(icon, 0, 0);
            root.Controls.Add(text, 1, 0);
            root.Controls.Add(dontShow, 1, 1);
            root.Controls.Add(buttons, 1, 2);
            Controls.Add(root);
            AcceptButton = ok;
            CancelButton = cancel;
            Theme.Apply(this);
        }

        public static bool Confirm(IWin32Window owner, int count)
        {
            using var f = new RangeWarning(count);
            return f.ShowDialog(owner) == DialogResult.OK;
        }
    }
}
