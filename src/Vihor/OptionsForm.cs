using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Vihor.Scanning;

namespace Vihor
{
    /// <summary>Options: scan speed / thoroughness, which services to detect, user-defined ports.</summary>
    internal sealed class OptionsForm : Form
    {
        private readonly TrackBar speed = new TrackBar();
        private readonly Label speedInfo = new Label();
        private readonly CheckedListBox services = new CheckedListBox();
        private readonly TextBox customPorts = new TextBox();

        public OptionsForm()
        {
            Text = Lang.T("opt.title");
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

            int w = LogicalToDeviceUnits(420);
            var root = new TableLayoutPanel { ColumnCount = 1, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink };

            // --- speed
            root.Controls.Add(Header(Lang.T("opt.speed")));
            speed.Minimum = 1;
            speed.Maximum = 5;
            speed.TickFrequency = 1;
            speed.LargeChange = 1;
            speed.Width = w;
            speed.Value = System.Math.Max(1, System.Math.Min(5, Settings.Speed));
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
            services.Height = LogicalToDeviceUnits(200);
            foreach (var p in HostInfo.Ports)
                services.Items.Add(new ServiceItem(p), Settings.ServicePorts.Contains(p.Port));
            root.Controls.Add(services);

            var defaults = new LinkLabel { Text = Lang.T("opt.defaults"), AutoSize = true, Margin = new Padding(0, LogicalToDeviceUnits(4), 0, LogicalToDeviceUnits(12)) };
            defaults.LinkClicked += (s, e) =>
            {
                for (int i = 0; i < services.Items.Count; i++)
                    services.SetItemChecked(i, Settings.DefaultServicePorts.Contains(((ServiceItem)services.Items[i]).Def.Port));
                speed.Value = 3;
            };
            root.Controls.Add(defaults);

            // --- custom ports
            root.Controls.Add(Header(Lang.T("opt.custom_ports")));
            customPorts.Width = w;
            customPorts.Text = string.Join(", ", Settings.CustomPorts);
            root.Controls.Add(customPorts);
            root.Controls.Add(new Label { Text = Lang.T("opt.custom_ports_hint"), AutoSize = true, ForeColor = Theme.Muted, Tag = "muted", MaximumSize = new Size(w, 0),
                                          Margin = new Padding(0, LogicalToDeviceUnits(4), 0, LogicalToDeviceUnits(14)) });

            // --- buttons
            var buttons = new FlowLayoutPanel { FlowDirection = FlowDirection.RightToLeft, Width = w, AutoSize = true, Margin = Padding.Empty };
            var cancel = new Button { Text = Lang.T("btn.cancel"), DialogResult = DialogResult.Cancel, AutoSize = true, MinimumSize = new Size(LogicalToDeviceUnits(88), 0) };
            var ok = new Button { Text = "OK", DialogResult = DialogResult.OK, AutoSize = true, MinimumSize = new Size(LogicalToDeviceUnits(88), 0) };
            ok.Click += (s, e) => Save();
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

        private void UpdateSpeedInfo() => speedInfo.Text = Lang.T("opt.speed_" + speed.Value);

        private void Save()
        {
            Settings.Speed = speed.Value;
            Settings.ServicePorts = services.CheckedItems.Cast<ServiceItem>().Select(i => i.Def.Port).ToList();
            Settings.CustomPorts = Settings.ParsePorts(customPorts.Text);
            Settings.Save();
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
}
