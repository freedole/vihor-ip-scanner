using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace Vihor
{
    /// <summary>
    /// Line icons drawn in code (Fluent-like, one stroke width), so they stay sharp at any DPI
    /// and take the theme colour. Coordinates are in a 24 x 24 grid.
    /// </summary>
    internal static class ToolIcons
    {
        public static Bitmap Get(string name, int sz, Color c)
        {
            var b = new Bitmap(sz, sz);
            using var g = Graphics.FromImage(b);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            float k = sz / 24f;
            g.ScaleTransform(k, k);
            using var pen = new Pen(c, 1.8f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
            using var br = new SolidBrush(c);
            Draw(g, name, pen, br, c);
            return b;
        }

        private static void L(Graphics g, Pen p, params float[] xy)
        {
            var pts = new PointF[xy.Length / 2];
            for (int i = 0; i < pts.Length; i++) pts[i] = new PointF(xy[2 * i], xy[2 * i + 1]);
            g.DrawLines(p, pts);
        }

        private static void R(Graphics g, Pen p, float x, float y, float w, float h, float r = 2f)
        {
            using var path = new GraphicsPath();
            float d = r * 2;
            path.AddArc(x, y, d, d, 180, 90);
            path.AddArc(x + w - d, y, d, d, 270, 90);
            path.AddArc(x + w - d, y + h - d, d, d, 0, 90);
            path.AddArc(x, y + h - d, d, d, 90, 90);
            path.CloseFigure();
            g.DrawPath(p, path);
        }

        private static void Draw(Graphics g, string name, Pen p, Brush br, Color c)
        {
            switch (name)
            {
                case "scan": // play
                    g.FillPolygon(br, new[] { new PointF(7, 4.5f), new PointF(19.5f, 12), new PointF(7, 19.5f) });
                    break;
                case "stop":
                    g.FillRectangle(br, 6, 6, 12, 12);
                    break;
                case "home":
                    L(g, p, 3.5f, 11.5f, 12, 4, 20.5f, 11.5f);
                    L(g, p, 6, 9.5f, 6, 20, 18, 20, 18, 9.5f);
                    R(g, p, 10, 14, 4, 6, 0.8f);
                    break;
                case "rescan":
                    g.DrawArc(p, 4.5f, 4.5f, 15, 15, 300, 290);
                    L(g, p, 19.5f, 4.5f, 19.5f, 9, 15, 9);
                    break;
                case "star":
                    {
                        var pts = new PointF[10];
                        for (int i = 0; i < 10; i++)
                        {
                            double a = -Math.PI / 2 + i * Math.PI / 5;
                            float r = i % 2 == 0 ? 9f : 4f;
                            pts[i] = new PointF(12 + (float)(r * Math.Cos(a)), 12.6f + (float)(r * Math.Sin(a)));
                        }
                        g.DrawPolygon(p, pts);
                    }
                    break;
                case "export":
                    L(g, p, 12, 3.5f, 12, 14.5f);
                    L(g, p, 7.5f, 10, 12, 14.5f, 16.5f, 10);
                    L(g, p, 4, 15, 4, 20, 20, 20, 20, 15);
                    break;
                case "web":
                    g.DrawEllipse(p, 3.5f, 3.5f, 17, 17);
                    g.DrawEllipse(p, 8.5f, 3.5f, 7, 17);
                    L(g, p, 3.5f, 12, 20.5f, 12);
                    break;
                case "rdp":
                    R(g, p, 3, 4, 18, 12);
                    L(g, p, 12, 16, 12, 20);
                    L(g, p, 8, 20, 16, 20);
                    break;
                case "ssh":
                    R(g, p, 3, 4, 18, 16);
                    L(g, p, 7, 9, 10, 12, 7, 15);
                    L(g, p, 12.5f, 15, 17, 15);
                    break;
                case "smb":
                    L(g, p, 3, 6.5f, 3, 19, 21, 19, 21, 8.5f, 12, 8.5f, 10, 5.5f, 4, 5.5f, 3, 6.5f);
                    L(g, p, 3, 11, 21, 11);
                    break;
                case "ping":
                    g.FillEllipse(br, 10.5f, 16.5f, 3, 3);
                    g.DrawArc(p, 7.5f, 13.5f, 9, 9, 225, 90);
                    g.DrawArc(p, 4f, 10f, 16, 16, 225, 90);
                    g.DrawArc(p, 0.5f, 6.5f, 23, 23, 225, 90);
                    break;
                case "tracert":
                    g.DrawEllipse(p, 3, 15, 5, 5);
                    g.DrawEllipse(p, 16, 4, 5, 5);
                    L(g, p, 8, 17.5f, 12, 17.5f, 12, 6.5f, 16, 6.5f);
                    break;
                case "wol": // bolt
                    g.DrawPolygon(p, new[] { new PointF(13.5f, 3), new PointF(5.5f, 13.5f), new PointF(11.5f, 13.5f), new PointF(10.5f, 21), new PointF(18.5f, 10.5f), new PointF(12.5f, 10.5f) });
                    break;
                case "power":
                    g.DrawArc(p, 4.5f, 5, 15, 15, 300, 300);
                    L(g, p, 12, 3, 12, 11);
                    break;
                case "expand":
                    L(g, p, 6, 6, 12, 11, 18, 6);
                    L(g, p, 6, 13, 12, 18, 18, 13);
                    break;
                case "collapse":
                    L(g, p, 6, 11, 12, 6, 18, 11);
                    L(g, p, 6, 18, 12, 13, 18, 18);
                    break;
                case "copy":
                    R(g, p, 8, 8, 12, 13);
                    L(g, p, 16, 5, 16, 4.5f, 15, 3.5f, 5, 3.5f, 4, 4.5f, 4, 16, 5, 17);
                    break;
                case "command": // search box with key
                    g.DrawEllipse(p, 4, 4, 11, 11);
                    L(g, p, 13.5f, 13.5f, 20, 20);
                    break;
                case "filter":
                    g.DrawPolygon(p, new[] { new PointF(4, 5), new PointF(20, 5), new PointF(14, 12.5f), new PointF(14, 19), new PointF(10, 21), new PointF(10, 12.5f) });
                    break;

                // ---- device types
                case "type_thispc":
                case "type_computer":
                    R(g, p, 3, 4, 18, 12);
                    L(g, p, 9, 20, 15, 20);
                    L(g, p, 12, 16, 12, 20);
                    if (name == "type_thispc") g.FillEllipse(br, 10, 8, 4, 4);
                    break;
                case "type_gateway":
                case "type_router":
                    R(g, p, 3, 12, 18, 7);
                    g.FillEllipse(br, 6, 14.5f, 2, 2);
                    g.FillEllipse(br, 9.5f, 14.5f, 2, 2);
                    L(g, p, 16, 12, 18, 5);
                    if (name == "type_gateway") { g.DrawArc(p, 6, 3, 8, 8, 200, 140); }
                    break;
                case "type_network":
                    R(g, p, 3, 9, 18, 7);
                    for (int i = 0; i < 4; i++) g.FillRectangle(br, 5.5f + i * 3.4f, 11.5f, 2, 2);
                    L(g, p, 12, 16, 12, 20);
                    break;
                case "type_hypervisor":
                    R(g, p, 3, 3.5f, 18, 7);
                    R(g, p, 3, 13.5f, 18, 7);
                    g.FillEllipse(br, 16, 6, 2, 2);
                    g.FillEllipse(br, 16, 16, 2, 2);
                    L(g, p, 6, 7, 11, 7);
                    L(g, p, 6, 17, 11, 17);
                    break;
                case "type_server":
                    R(g, p, 5, 3, 14, 18);
                    L(g, p, 8, 8, 16, 8);
                    L(g, p, 8, 12, 16, 12);
                    g.FillEllipse(br, 11, 15.5f, 2, 2);
                    break;
                case "type_virtualmachine":
                    using (var dash = (Pen)p.Clone()) { dash.DashPattern = new[] { 1.6f, 1.4f }; R(g, dash, 3, 4, 18, 16); }
                    L(g, p, 8, 9, 12, 16, 16, 9);
                    break;
                case "type_nas":
                    R(g, p, 4, 3, 16, 18);
                    L(g, p, 4, 9, 20, 9);
                    L(g, p, 4, 15, 20, 15);
                    g.FillEllipse(br, 15.5f, 5.2f, 2, 2);
                    g.FillEllipse(br, 15.5f, 11.2f, 2, 2);
                    g.FillEllipse(br, 15.5f, 17.2f, 2, 2);
                    break;
                case "type_printer":
                    R(g, p, 3, 9, 18, 8);
                    L(g, p, 7, 9, 7, 4, 17, 4, 17, 9);
                    R(g, p, 7, 14, 10, 6, 1);
                    break;
                case "type_camera":
                    R(g, p, 3, 6, 13, 9);
                    L(g, p, 16, 9, 21, 6.5f, 21, 14.5f, 16, 12);
                    L(g, p, 9.5f, 15, 9.5f, 20, 6, 20);
                    break;
                case "type_phone":
                    R(g, p, 7, 2.5f, 10, 19, 2.5f);
                    L(g, p, 10.5f, 18, 13.5f, 18);
                    break;
                case "type_iot":
                    R(g, p, 6, 6, 12, 12);
                    R(g, p, 9.5f, 9.5f, 5, 5, 0.8f);
                    foreach (var o in new[] { 9.5f, 14.5f })
                    {
                        L(g, p, o, 3, o, 6); L(g, p, o, 18, o, 21);
                        L(g, p, 3, o, 6, o); L(g, p, 18, o, 21, o);
                    }
                    break;
                default: // unknown
                    g.DrawEllipse(p, 5, 5, 14, 14);
                    break;
            }
        }
    }
}
