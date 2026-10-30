using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Win32;

namespace Vihor
{
    /// <summary>
    /// Light / dark colours. "system" follows Windows (Settings → Personalization → Colors → app mode).
    /// WinForms has no built-in dark mode, so menus/toolbars use a custom renderer, the window frame and
    /// scrollbars use the Windows dark-mode themes, and the list header is owner-drawn.
    /// </summary>
    internal static class Theme
    {
        public static bool Dark { get; private set; }

        public static Color Back, Fore, Panel, Border, Muted, Link, NewHost, MissingHost, HeaderBack, Hover, Input;

        public static void Resolve(string preference)
        {
            Dark = preference == "dark" || (preference != "light" && SystemPrefersDark());
            if (Dark)
            {
                Back = Color.FromArgb(30, 31, 34);
                Panel = Color.FromArgb(43, 45, 49);
                Input = Color.FromArgb(56, 58, 64);
                Fore = Color.FromArgb(230, 232, 235);
                Muted = Color.FromArgb(150, 155, 163);
                Border = Color.FromArgb(70, 73, 80);
                HeaderBack = Color.FromArgb(43, 45, 49);
                Hover = Color.FromArgb(62, 66, 74);
                Link = Color.FromArgb(110, 168, 254);
                NewHost = Color.FromArgb(87, 200, 120);
                MissingHost = Color.FromArgb(125, 129, 137);
            }
            else
            {
                Back = SystemColors.Window;
                Panel = SystemColors.Control;
                Input = SystemColors.Window;
                Fore = SystemColors.WindowText;
                Muted = SystemColors.GrayText;
                Border = SystemColors.ControlDark;
                HeaderBack = SystemColors.Control;
                Hover = SystemColors.ControlLight;
                Link = Color.FromArgb(31, 111, 235);
                NewHost = Color.FromArgb(26, 127, 55);
                MissingHost = Color.FromArgb(140, 146, 154);
            }
        }

        private static bool SystemPrefersDark()
        {
            try
            {
                using var k = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
                return k?.GetValue("AppsUseLightTheme") is int v && v == 0;
            }
            catch { return false; }
        }

        /// <summary>Applies colours to a form and all of its controls (call again after switching).</summary>
        public static void Apply(Form form)
        {
            ToolStripManager.Renderer = Dark ? new DarkRenderer() : (ToolStripRenderer)new ToolStripProfessionalRenderer();
            form.BackColor = Panel;
            form.ForeColor = Fore;
            ApplyControls(form.Controls);
            TitleBar(form);
        }

        private static void ApplyControls(Control.ControlCollection controls)
        {
            foreach (Control c in controls)
            {
                switch (c)
                {
                    case ListView lv:
                        lv.BackColor = Back; lv.ForeColor = Fore;
                        DarkScroll(lv);
                        break;
                    case TextBox _:
                    case ComboBox _:
                    case ListBox _:
                        c.BackColor = Input; c.ForeColor = Fore;
                        break;
                    case Button b:
                        b.UseVisualStyleBackColor = !Dark;
                        if (Dark) { b.BackColor = Input; b.ForeColor = Fore; b.FlatStyle = FlatStyle.Flat; b.FlatAppearance.BorderColor = Border; }
                        else b.FlatStyle = FlatStyle.Standard;
                        break;
                    case LinkLabel ll:
                        ll.LinkColor = Link; ll.ActiveLinkColor = Link; ll.BackColor = Color.Transparent;
                        break;
                    case ToolStrip ts:
                        ts.BackColor = Panel; ts.ForeColor = Fore;
                        foreach (ToolStripItem i in ts.Items)
                        {
                            if (i is ToolStripComboBox cb) { cb.ComboBox.BackColor = Input; cb.ComboBox.ForeColor = Fore; cb.ComboBox.FlatStyle = Dark ? FlatStyle.Flat : FlatStyle.Standard; }
                            else if (i is ToolStripTextBox tb) { tb.TextBox.BackColor = Input; tb.TextBox.ForeColor = Fore; tb.BorderStyle = Dark ? BorderStyle.FixedSingle : BorderStyle.Fixed3D; }
                            else if (i is ToolStripStatusLabel sl && sl.IsLink) { sl.LinkColor = Link; sl.ActiveLinkColor = Link; }
                        }
                        break;
                    default:
                        if (c is PictureBox) break;
                        c.BackColor = Panel;
                        if (!(c is Label l && l.Tag as string == "muted")) c.ForeColor = Fore;
                        else c.ForeColor = Muted;
                        break;
                }
                if (c.HasChildren) ApplyControls(c.Controls);
            }
        }

        // ---- native dark mode helpers (Windows 10 1809+; silently ignored elsewhere)

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

        [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
        private static extern int SetWindowTheme(IntPtr hwnd, string appName, string idList);

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        public static void TitleBar(Form f)
        {
            if (!f.IsHandleCreated) { f.HandleCreated += (s, e) => TitleBar(f); return; }
            try
            {
                int on = Dark ? 1 : 0;
                if (DwmSetWindowAttribute(f.Handle, 20, ref on, 4) != 0) DwmSetWindowAttribute(f.Handle, 19, ref on, 4);
            }
            catch { }
        }

        public static void DarkScroll(ListView lv)
        {
            if (!lv.IsHandleCreated) { lv.HandleCreated += (s, e) => DarkScroll(lv); return; }
            try
            {
                // light: null/null restores the default look the app always had
                SetWindowTheme(lv.Handle, Dark ? "DarkMode_Explorer" : null, null);
                var header = SendMessage(lv.Handle, 0x101F /* LVM_GETHEADER */, IntPtr.Zero, IntPtr.Zero);
                if (header != IntPtr.Zero) SetWindowTheme(header, Dark ? "DarkMode_ItemsView" : null, null);
            }
            catch { }
        }

        /// <summary>Menus, toolbars and the status bar in dark colours.</summary>
        private sealed class DarkRenderer : ToolStripProfessionalRenderer
        {
            public DarkRenderer() : base(new DarkColors()) { RoundedEdges = false; }

            protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
            {
                if (!"accent".Equals(e.Item.Tag)) e.TextColor = e.Item.Enabled ? Fore : Muted;
                base.OnRenderItemText(e);
            }

            protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
            {
                e.ArrowColor = Fore;
                base.OnRenderArrow(e);
            }

            protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e)
            {
                var r = e.ImageRectangle;
                using (var b = new SolidBrush(Hover)) e.Graphics.FillRectangle(b, r);
                using var pen = new Pen(Fore, 1.6f);
                e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                e.Graphics.DrawLines(pen, new[]
                {
                    new PointF(r.Left + r.Width * 0.22f, r.Top + r.Height * 0.52f),
                    new PointF(r.Left + r.Width * 0.42f, r.Top + r.Height * 0.72f),
                    new PointF(r.Left + r.Width * 0.78f, r.Top + r.Height * 0.30f),
                });
            }
        }

        private sealed class DarkColors : ProfessionalColorTable
        {
            public DarkColors() { UseSystemColors = false; }
            public override Color MenuStripGradientBegin => Panel;
            public override Color MenuStripGradientEnd => Panel;
            public override Color ToolStripGradientBegin => Panel;
            public override Color ToolStripGradientMiddle => Panel;
            public override Color ToolStripGradientEnd => Panel;
            public override Color ToolStripBorder => Panel;
            public override Color ToolStripDropDownBackground => Panel;
            public override Color ToolStripContentPanelGradientBegin => Panel;
            public override Color ToolStripContentPanelGradientEnd => Panel;
            public override Color StatusStripGradientBegin => Panel;
            public override Color StatusStripGradientEnd => Panel;
            public override Color ImageMarginGradientBegin => Panel;
            public override Color ImageMarginGradientMiddle => Panel;
            public override Color ImageMarginGradientEnd => Panel;
            public override Color MenuBorder => Border;
            public override Color MenuItemBorder => Hover;
            public override Color MenuItemSelected => Hover;
            public override Color MenuItemSelectedGradientBegin => Hover;
            public override Color MenuItemSelectedGradientEnd => Hover;
            public override Color MenuItemPressedGradientBegin => Hover;
            public override Color MenuItemPressedGradientMiddle => Hover;
            public override Color MenuItemPressedGradientEnd => Hover;
            public override Color ButtonSelectedHighlight => Hover;
            public override Color ButtonSelectedBorder => Hover;
            public override Color ButtonSelectedGradientBegin => Hover;
            public override Color ButtonSelectedGradientMiddle => Hover;
            public override Color ButtonSelectedGradientEnd => Hover;
            public override Color ButtonPressedGradientBegin => Border;
            public override Color ButtonPressedGradientMiddle => Border;
            public override Color ButtonPressedGradientEnd => Border;
            public override Color ButtonCheckedGradientBegin => Hover;
            public override Color ButtonCheckedGradientMiddle => Hover;
            public override Color ButtonCheckedGradientEnd => Hover;
            public override Color CheckBackground => Hover;
            public override Color CheckSelectedBackground => Hover;
            public override Color CheckPressedBackground => Hover;
            public override Color SeparatorDark => Border;
            public override Color SeparatorLight => Panel;
            public override Color OverflowButtonGradientBegin => Panel;
            public override Color OverflowButtonGradientMiddle => Panel;
            public override Color OverflowButtonGradientEnd => Panel;
            public override Color GripDark => Border;
            public override Color GripLight => Panel;
        }
    }
}
