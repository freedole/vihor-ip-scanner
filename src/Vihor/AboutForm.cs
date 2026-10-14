using System.Drawing;
using System.Windows.Forms;

namespace Vihor
{
    internal sealed class AboutForm : Form
    {
        public AboutForm()
        {
            Text = Lang.T("menu.about");
            Icon = MainForm.LoadIcon();
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = MinimizeBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            AutoScaleMode = AutoScaleMode.Dpi;
            Font = SystemFonts.MessageBoxFont;
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
            Padding = new Padding(LogicalToDeviceUnits(20));

            var layout = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.TopDown,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                WrapContents = false,
            };

            var logo = new PictureBox
            {
                Image = new Icon(Icon, LogicalToDeviceUnits(64), LogicalToDeviceUnits(64)).ToBitmap(),
                SizeMode = PictureBoxSizeMode.AutoSize,
                Margin = new Padding(0, 0, 0, LogicalToDeviceUnits(8)),
            };
            var title = new Label { Text = AppInfo.Name, AutoSize = true, Font = new Font(Font.FontFamily, Font.Size * 1.6f, FontStyle.Bold) };
            var version = new Label { Text = Lang.T("about.version", AppInfo.Version), AutoSize = true };
            var tagline = new Label
            {
                Text = Lang.T("about.tagline") + "\n" + Lang.T("about.name_meaning"),
                AutoSize = true,
                MaximumSize = new Size(LogicalToDeviceUnits(380), 0),
                Margin = new Padding(0, LogicalToDeviceUnits(8), 0, LogicalToDeviceUnits(8)),
            };
            var author = new Label { Text = "© 2026 " + AppInfo.Author + " · MIT License", AutoSize = true };
            var web = Link("cyber-security.ba", AppInfo.Website);
            var repo = Link("github.com/freedole/vihor-ip-scanner", AppInfo.Repository);
            var made = new Label { Text = Lang.T("about.made_in"), AutoSize = true, ForeColor = SystemColors.GrayText,
                                   Margin = new Padding(0, LogicalToDeviceUnits(8), 0, 0) };
            var ok = new Button { Text = "OK", DialogResult = DialogResult.OK, AutoSize = true,
                                  Margin = new Padding(0, LogicalToDeviceUnits(14), 0, 0) };

            layout.Controls.AddRange(new Control[] { logo, title, version, tagline, author, web, repo, made, ok });
            Controls.Add(layout);
            AcceptButton = ok;
            CancelButton = ok;
        }

        private static LinkLabel Link(string text, string url)
        {
            var l = new LinkLabel { Text = text, AutoSize = true };
            l.LinkClicked += (s, e) => MainForm.OpenUrl(url);
            return l;
        }
    }
}
