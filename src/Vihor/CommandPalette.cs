using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace Vihor
{
    /// <summary>Ctrl+K: type a few letters of any command (menus, toolbar, device actions) and press Enter.</summary>
    internal sealed class CommandPalette : Form
    {
        internal sealed class Command
        {
            public string Text, Path, Shortcut;
            public Action Run;
            public override string ToString() => Shortcut.Length > 0 ? $"{Text}    ({Shortcut})" : Text;
        }

        private readonly TextBox box = new TextBox();
        private readonly ListBox items = new ListBox();
        private readonly List<Command> all;

        public CommandPalette(List<Command> commands)
        {
            all = commands;
            FormBorderStyle = FormBorderStyle.FixedToolWindow;
            ControlBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            AutoScaleMode = AutoScaleMode.Dpi;
            Font = new Font(SystemFonts.MessageBoxFont.FontFamily, SystemFonts.MessageBoxFont.SizeInPoints + 1);
            Size = new Size(LogicalToDeviceUnits(520), LogicalToDeviceUnits(360));
            Text = Lang.T("palette.title");
            KeyPreview = true;

            box.Dock = DockStyle.Top;
            items.Dock = DockStyle.Fill;
            items.IntegralHeight = false;
            items.BorderStyle = BorderStyle.None;
            Controls.Add(items);
            Controls.Add(box);

            box.TextChanged += (s, e) => Fill();
            items.DoubleClick += (s, e) => RunSelected();
            KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Escape) { Close(); e.Handled = true; }
                else if (e.KeyCode == Keys.Enter) { RunSelected(); e.Handled = e.SuppressKeyPress = true; }
                else if (e.KeyCode == Keys.Down && items.Items.Count > 0) { items.SelectedIndex = Math.Min(items.Items.Count - 1, items.SelectedIndex + 1); e.Handled = true; }
                else if (e.KeyCode == Keys.Up && items.Items.Count > 0) { items.SelectedIndex = Math.Max(0, items.SelectedIndex - 1); e.Handled = true; }
            };
            Deactivate += (s, e) => Close();
            Theme.Apply(this);
            Fill();
        }

        private void Fill()
        {
            var words = box.Text.Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            var found = all.Where(c => words.All(w => (c.Text + " " + c.Path).IndexOf(w, StringComparison.OrdinalIgnoreCase) >= 0))
                           .OrderBy(c => words.Length > 0 && c.Text.StartsWith(words[0], StringComparison.OrdinalIgnoreCase) ? 0 : 1)
                           .ToList();
            items.BeginUpdate();
            items.Items.Clear();
            foreach (var c in found) items.Items.Add(c);
            items.EndUpdate();
            if (items.Items.Count > 0) items.SelectedIndex = 0;
        }

        private void RunSelected()
        {
            if (!(items.SelectedItem is Command c)) return;
            Close();
            c.Run();
        }

        /// <summary>Collects every enabled leaf item of the given menus (texts without the &amp; marker).</summary>
        public static void Collect(ToolStripItemCollection src, string path, List<Command> into)
        {
            foreach (ToolStripItem i in src)
            {
                if (!(i is ToolStripMenuItem m) || !m.Available) continue;
                var text = (m.Text ?? "").Replace("&", "").Trim();
                if (text.Length == 0 || text == "…") continue;
                if (m.HasDropDownItems)
                {
                    Collect(m.DropDownItems, path.Length == 0 ? text : path + " › " + text, into);
                    continue;
                }
                if (!m.Enabled) continue;
                var shortcut = m.ShortcutKeyDisplayString ?? (m.ShortcutKeys != Keys.None ? new KeysConverter().ConvertToString(m.ShortcutKeys) : "");
                into.Add(new Command { Text = path.Length == 0 ? text : path + " › " + text, Path = path, Shortcut = shortcut ?? "", Run = m.PerformClick });
            }
        }
    }

    /// <summary>Small one-line input dialog.</summary>
    internal static class Prompt
    {
        public static string Ask(IWin32Window owner, string title, string label, string value)
        {
            using var f = new Form
            {
                Text = title, FormBorderStyle = FormBorderStyle.FixedDialog, MinimizeBox = false, MaximizeBox = false,
                StartPosition = FormStartPosition.CenterParent, AutoScaleMode = AutoScaleMode.Dpi, Font = SystemFonts.MessageBoxFont,
                ShowInTaskbar = false, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Padding = new Padding(12)
            };
            var t = new TableLayoutPanel { AutoSize = true, ColumnCount = 1, Dock = DockStyle.Fill };
            var lbl = new Label { Text = label, AutoSize = true };
            var box = new TextBox { Text = value ?? "", Width = 360 };
            var buttons = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.RightToLeft, Dock = DockStyle.Fill };
            var ok = new Button { Text = "OK", DialogResult = DialogResult.OK, AutoSize = true };
            var cancel = new Button { Text = Lang.T("btn.cancel"), DialogResult = DialogResult.Cancel, AutoSize = true };
            buttons.Controls.Add(cancel);
            buttons.Controls.Add(ok);
            t.Controls.Add(lbl);
            t.Controls.Add(box);
            t.Controls.Add(buttons);
            f.Controls.Add(t);
            f.AcceptButton = ok;
            f.CancelButton = cancel;
            Theme.Apply(f);
            return f.ShowDialog(owner) == DialogResult.OK ? box.Text.Trim() : null;
        }
    }
}
