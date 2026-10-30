using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;
using Vihor.Scanning;

namespace Vihor
{
    /// <summary>
    /// User notes per device (e.g. "Ricoh, 2nd floor, office 12"), kept in %APPDATA%\Vihor\notes.tsv.
    /// Devices are recognised by MAC address, or by IP address when the MAC is unknown.
    /// </summary>
    internal static class Notes
    {
        private static Dictionary<string, string> map;

        private static string FilePath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Vihor", "notes.tsv");

        private static Dictionary<string, string> Map
        {
            get
            {
                if (map != null) return map;
                map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                try
                {
                    if (File.Exists(FilePath))
                        foreach (var line in File.ReadAllLines(FilePath, Encoding.UTF8))
                        {
                            int tab = line.IndexOf('\t');
                            if (tab > 0 && !line.StartsWith("#", StringComparison.Ordinal)) map[line.Substring(0, tab)] = line.Substring(tab + 1);
                        }
                }
                catch { }
                return map;
            }
        }

        public static string Get(HostInfo h) => h != null && Map.TryGetValue(h.Key, out var n) ? n : "";

        public static void Set(HostInfo h, string note)
        {
            note = (note ?? "").Replace('\t', ' ').Replace('\r', ' ').Replace('\n', ' ').Trim();
            if (note.Length == 0) Map.Remove(h.Key); else Map[h.Key] = note;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
                var sb = new StringBuilder("# Vihor IP Scanner - device notes (MAC or ip:address <TAB> note)\r\n");
                foreach (var kv in Map) sb.Append(kv.Key).Append('\t').Append(kv.Value).Append("\r\n");
                File.WriteAllText(FilePath, sb.ToString(), new UTF8Encoding(false));
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, AppInfo.Name, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>Small dialog to edit the note of one device. Returns null if cancelled.</summary>
        public static string Edit(IWin32Window owner, HostInfo h)
        {
            using var f = new Form
            {
                Text = Lang.T("note.title", string.IsNullOrEmpty(h.Name) ? h.IpText : $"{h.Name} ({h.IpText})"),
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false,
                MinimizeBox = false,
                ShowInTaskbar = false,
                StartPosition = FormStartPosition.CenterParent,
                AutoScaleMode = AutoScaleMode.Dpi,
                Font = SystemFonts.MessageBoxFont,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Icon = MainForm.LoadIcon(),
            };
            f.Padding = new Padding(f.LogicalToDeviceUnits(16));
            int w = f.LogicalToDeviceUnits(380);
            var root = new TableLayoutPanel { ColumnCount = 1, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink };
            var label = new Label { Text = Lang.T("note.prompt"), AutoSize = true, MaximumSize = new Size(w, 0), Margin = new Padding(0, 0, 0, f.LogicalToDeviceUnits(6)) };
            var box = new TextBox { Width = w, Text = Get(h), MaxLength = 200 };
            var buttons = new FlowLayoutPanel { FlowDirection = FlowDirection.RightToLeft, Width = w, AutoSize = true, Margin = new Padding(0, f.LogicalToDeviceUnits(12), 0, 0) };
            var cancel = new Button { Text = Lang.T("btn.cancel"), DialogResult = DialogResult.Cancel, AutoSize = true, MinimumSize = new Size(f.LogicalToDeviceUnits(88), 0) };
            var ok = new Button { Text = "OK", DialogResult = DialogResult.OK, AutoSize = true, MinimumSize = new Size(f.LogicalToDeviceUnits(88), 0) };
            buttons.Controls.Add(cancel);
            buttons.Controls.Add(ok);
            root.Controls.Add(label);
            root.Controls.Add(box);
            root.Controls.Add(buttons);
            f.Controls.Add(root);
            f.AcceptButton = ok;
            f.CancelButton = cancel;
            Theme.Apply(f);
            return f.ShowDialog(owner) == DialogResult.OK ? box.Text : null;
        }
    }
}
