using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Shiguang
{
    /// <summary>Theme, fonts and drawing helpers. All layout numbers are design pixels @96dpi.</summary>
    internal static class Ui
    {
        public static float Scale = 1f;
        public static int S(float v) { return (int)Math.Round(v * Scale); }
        public static float SF(float v) { return v * Scale; }
        public static Rectangle R(float x, float y, float w, float h)
        {
            return new Rectangle(S(x), S(y), Math.Max(1, S(w)), Math.Max(1, S(h)));
        }
        public static RectangleF RF(float x, float y, float w, float h)
        {
            return new RectangleF(SF(x), SF(y), SF(w), SF(h));
        }
        public static Point P(float x, float y) { return new Point(S(x), S(y)); }

        // ---------------- palette ----------------
        public static bool IsLight { get; private set; }
        public static Color BgTop;
        public static Color BgBottom;
        public static Color SidebarTop;
        public static Color SidebarBottom;
        public static Color CardTop;
        public static Color CardBottom;
        public static Color Field;
        public static Color Ink;
        public static Color Text2;
        public static Color Text3;
        public static Color Danger;
        public static Color Success;
        public static Color Warn;
        public static Color Brand = Color.FromArgb(124, 156, 255);
        public static Color Brand2 = Color.FromArgb(177, 140, 255);
        public static Color DialogTop;
        public static Color DialogBottom;
        public static Color TooltipBg;
        public static Color TooltipBorder;
        public static Color TooltipText;
        public static Color OnAccent = Color.FromArgb(13, 19, 33);

        static Ui()
        {
            SetTheme(false);
        }

        public static void SetTheme(bool light)
        {
            IsLight = light;
            if (light)
            {
                BgTop = Color.FromArgb(249, 251, 255);
                BgBottom = Color.FromArgb(232, 238, 248);
                SidebarTop = Color.FromArgb(255, 255, 255);
                SidebarBottom = Color.FromArgb(244, 247, 252);
                CardTop = Color.FromArgb(255, 255, 255);
                CardBottom = Color.FromArgb(246, 249, 254);
                Field = Color.FromArgb(237, 242, 250);
                Ink = Color.FromArgb(24, 34, 53);
                Text2 = Color.FromArgb(86, 102, 127);
                Text3 = Color.FromArgb(139, 153, 173);
                Danger = Color.FromArgb(214, 69, 69);
                Success = Color.FromArgb(19, 138, 99);
                Warn = Color.FromArgb(200, 121, 10);
                DialogTop = Color.FromArgb(255, 255, 255);
                DialogBottom = Color.FromArgb(243, 247, 253);
                TooltipBg = Color.FromArgb(255, 255, 255);
                TooltipBorder = Color.FromArgb(210, 221, 236);
                TooltipText = Color.FromArgb(55, 70, 92);
            }
            else
            {
                BgTop = Color.FromArgb(12, 18, 33);
                BgBottom = Color.FromArgb(7, 11, 21);
                SidebarTop = Color.FromArgb(13, 19, 33);
                SidebarBottom = Color.FromArgb(8, 13, 24);
                CardTop = Color.FromArgb(24, 35, 57);
                CardBottom = Color.FromArgb(15, 23, 39);
                Field = Color.FromArgb(24, 34, 54);
                Ink = Color.FromArgb(238, 243, 251);
                Text2 = Color.FromArgb(151, 163, 186);
                Text3 = Color.FromArgb(96, 108, 132);
                Danger = Color.FromArgb(255, 118, 118);
                Success = Color.FromArgb(110, 231, 183);
                Warn = Color.FromArgb(255, 196, 107);
                DialogTop = Color.FromArgb(20, 29, 47);
                DialogBottom = Color.FromArgb(10, 15, 26);
                TooltipBg = Color.FromArgb(31, 42, 65);
                TooltipBorder = Color.FromArgb(45, 255, 255, 255);
                TooltipText = Color.FromArgb(170, 181, 203);
            }
        }

        /// <summary>Overlay tint: white-alpha on dark, dark-alpha on light.</summary>
        public static Color Overlay(int alpha)
        {
            if (alpha < 0) alpha = 0; else if (alpha > 255) alpha = 255;
            if (IsLight)
                return Color.FromArgb((int)Math.Round(alpha * 0.78f), 23, 35, 55);
            return Color.FromArgb(alpha, 255, 255, 255);
        }

        public static Color Shadow(int alpha)
        {
            if (alpha < 0) alpha = 0; else if (alpha > 255) alpha = 255;
            return Color.FromArgb(alpha, 0, 0, 0);
        }

        public static int GlowAlpha(int darkAlpha)
        {
            return IsLight ? (int)Math.Round(darkAlpha * 0.55f) : darkAlpha;
        }
        public static readonly string[] Palette = new string[]
        {
            "#7C9CFF", "#FF8A7A", "#FFC46B", "#6EE7B7",
            "#5BD4E8", "#C084FC", "#F472B6", "#A3E635"
        };

        static string CnFamily = "Microsoft YaHei UI";
        static string DisplayFamily = "Bahnschrift SemiBold";
        static string DisplayLightFamily = "Bahnschrift Light";
        static readonly Dictionary<string, Font> FontCache = new Dictionary<string, Font>();

        public static void Init(float scale)
        {
            Scale = scale;
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            using (var fc = new InstalledFontCollection())
            {
                foreach (var fam in fc.Families) names.Add(fam.Name);
            }
            if (!names.Contains(CnFamily))
                CnFamily = names.Contains("Microsoft YaHei") ? "Microsoft YaHei"
                    : names.Contains("微软雅黑") ? "微软雅黑" : "Segoe UI";
            if (!names.Contains(DisplayFamily))
                DisplayFamily = names.Contains("Segoe UI Semibold") ? "Segoe UI Semibold"
                    : names.Contains("Segoe UI") ? "Segoe UI" : "Arial";
            if (!names.Contains(DisplayLightFamily))
                DisplayLightFamily = names.Contains("Segoe UI Light") ? "Segoe UI Light"
                    : names.Contains("Segoe UI") ? "Segoe UI" : "Arial";
        }

        public static Font F(float px, bool display = false, bool bold = false, bool light = false)
        {
            string fam = display ? (light ? DisplayLightFamily : DisplayFamily) : CnFamily;
            FontStyle style = bold ? FontStyle.Bold : FontStyle.Regular;
            string key = fam + "|" + px.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) + "|" + (int)style;
            Font f;
            if (FontCache.TryGetValue(key, out f)) return f;
            f = new Font(fam, Math.Max(1f, px * Scale), style, GraphicsUnit.Pixel);
            FontCache[key] = f;
            return f;
        }

        /// <summary>A disposable copy of a cached font, for short-lived using blocks.</summary>
        public static Font FClone(float px, bool display = false, bool bold = false, bool light = false)
        {
            return (Font)F(px, display, bold, light).Clone();
        }

        // ---------------- colors ----------------
        public static Color A(Color c, int alpha)
        {
            if (alpha < 0) alpha = 0; else if (alpha > 255) alpha = 255;
            return Color.FromArgb(alpha, c.R, c.G, c.B);
        }

        public static Color Mix(Color a, Color b, float t)
        {
            if (t < 0f) t = 0f; else if (t > 1f) t = 1f;
            return Color.FromArgb(
                (int)Math.Round(a.A + (b.A - a.A) * t),
                (int)Math.Round(a.R + (b.R - a.R) * t),
                (int)Math.Round(a.G + (b.G - a.G) * t),
                (int)Math.Round(a.B + (b.B - a.B) * t));
        }

        public static Color Lighten(Color c, float t) { return Mix(c, Color.White, t); }
        public static Color Darken(Color c, float t) { return Mix(c, Color.Black, t); }
        public static Color AccentInk(Color c)
        {
            if (!IsLight) return Lighten(c, 0.15f);
            Hsl hsl = RgbToHsl(c);
            return HslToRgb(hsl.H, Math.Max(hsl.S, 0.62), Math.Min(hsl.L, 0.44));
        }

        struct Hsl { public double H, S, L; }

        static Hsl RgbToHsl(Color c)
        {
            double r = c.R / 255.0, g = c.G / 255.0, b = c.B / 255.0;
            double max = Math.Max(r, Math.Max(g, b)), min = Math.Min(r, Math.Min(g, b));
            double l = (max + min) / 2.0, h = 0, s = 0;
            double d = max - min;
            if (d > 1e-6)
            {
                s = l > 0.5 ? d / (2.0 - max - min) : d / (max + min);
                if (max == r) h = (g - b) / d + (g < b ? 6.0 : 0.0);
                else if (max == g) h = (b - r) / d + 2.0;
                else h = (r - g) / d + 4.0;
                h /= 6.0;
            }
            return new Hsl { H = h, S = s, L = l };
        }

        static Color HslToRgb(double h, double s, double l)
        {
            if (s <= 0) { int v = (int)Math.Round(l * 255); return Color.FromArgb(v, v, v); }
            double q = l < 0.5 ? l * (1 + s) : l + s - l * s;
            double p = 2 * l - q;
            double r = HueToRgb(p, q, h + 1.0 / 3.0);
            double g = HueToRgb(p, q, h);
            double b = HueToRgb(p, q, h - 1.0 / 3.0);
            return Color.FromArgb(
                (int)Math.Round(Math.Max(0, Math.Min(1, r)) * 255),
                (int)Math.Round(Math.Max(0, Math.Min(1, g)) * 255),
                (int)Math.Round(Math.Max(0, Math.Min(1, b)) * 255));
        }

        static double HueToRgb(double p, double q, double t)
        {
            if (t < 0) t += 1;
            if (t > 1) t -= 1;
            if (t < 1.0 / 6.0) return p + (q - p) * 6 * t;
            if (t < 1.0 / 2.0) return q;
            if (t < 2.0 / 3.0) return p + (q - p) * (2.0 / 3.0 - t) * 6;
            return p;
        }

        public static Color HexColor(string hex, Color fallback)
        {
            if (!string.IsNullOrEmpty(hex) && hex.Length == 7 && hex[0] == '#')
            {
                int v;
                if (int.TryParse(hex.Substring(1), System.Globalization.NumberStyles.HexNumber,
                        System.Globalization.CultureInfo.InvariantCulture, out v))
                    return Color.FromArgb(255, (v >> 16) & 255, (v >> 8) & 255, v & 255);
            }
            return fallback;
        }

        public static string HexOf(Color c)
        {
            return "#" + c.R.ToString("X2") + c.G.ToString("X2") + c.B.ToString("X2");
        }

        // ---------------- shapes ----------------
        public static GraphicsPath Round(RectangleF r, float radius)
        {
            var p = new GraphicsPath();
            float rad = Math.Min(radius, Math.Min(r.Width, r.Height) / 2f);
            if (rad <= 0.6f)
            {
                p.AddRectangle(r);
                return p;
            }
            float d = rad * 2f;
            p.AddArc(r.X, r.Y, d, d, 180f, 90f);
            p.AddArc(r.Right - d, r.Y, d, d, 270f, 90f);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0f, 90f);
            p.AddArc(r.X, r.Bottom - d, d, d, 90f, 90f);
            p.CloseFigure();
            return p;
        }

        public static void FillRound(Graphics g, RectangleF r, float radius, Color color)
        {
            using (var p = Round(r, radius))
            using (var b = new SolidBrush(color))
                g.FillPath(b, p);
        }

        public static void FillRound(Graphics g, RectangleF r, float radius, Brush brush)
        {
            using (var p = Round(r, radius))
                g.FillPath(brush, p);
        }

        public static void StrokeRound(Graphics g, RectangleF r, float radius, Color color, float width = 1f)
        {
            using (var p = Round(r, radius))
            using (var pen = new Pen(color, width))
                g.DrawPath(pen, p);
        }

        public static void Glow(Graphics g, RectangleF area, Color color, int centerAlpha)
        {
            if (area.Width <= 0 || area.Height <= 0) return;
            using (var path = new GraphicsPath())
            {
                path.AddEllipse(area);
                using (var br = new PathGradientBrush(path))
                {
                    br.CenterColor = A(color, centerAlpha);
                    br.SurroundColors = new Color[] { A(color, 0) };
                    br.CenterPoint = new PointF(area.X + area.Width / 2f, area.Y + area.Height / 2f);
                    g.FillEllipse(br, area);
                }
            }
        }

        // ---------------- text ----------------
        public static SizeF Measure(Graphics g, string s, Font f)
        {
            if (string.IsNullOrEmpty(s)) return SizeF.Empty;
            return g.MeasureString(s, f, int.MaxValue, StringFormat.GenericTypographic);
        }

        public static void Text(Graphics g, string s, Font f, Color c, RectangleF r,
            StringAlignment h = StringAlignment.Near, StringAlignment v = StringAlignment.Center,
            bool trim = false)
        {
            if (string.IsNullOrEmpty(s)) return;
            using (var sf = new StringFormat(StringFormat.GenericTypographic))
            {
                sf.Alignment = h;
                sf.LineAlignment = v;
                sf.FormatFlags = StringFormatFlags.NoWrap;
                if (trim) sf.Trimming = StringTrimming.EllipsisCharacter;
                using (var b = new SolidBrush(c))
                    g.DrawString(s, f, b, r, sf);
            }
        }

        public static void TextGradient(Graphics g, string s, Font f, RectangleF r,
            Color top, Color bottom, StringAlignment h = StringAlignment.Center,
            StringAlignment v = StringAlignment.Center)
        {
            if (string.IsNullOrEmpty(s)) return;
            using (var sf = new StringFormat(StringFormat.GenericTypographic))
            {
                sf.Alignment = h;
                sf.LineAlignment = v;
                sf.FormatFlags = StringFormatFlags.NoWrap;
                using (var br = new LinearGradientBrush(r, top, bottom, 90f))
                    g.DrawString(s, f, br, r, sf);
            }
        }

        /// <summary>Draws letter-spaced text (for small english labels).</summary>
        public static void Spaced(Graphics g, string s, Font f, Color c, RectangleF r,
            float spacing, StringAlignment h = StringAlignment.Near)
        {
            if (string.IsNullOrEmpty(s)) return;
            int n = s.Length;
            var w = new float[n];
            float total = 0f, lineH = 0f;
            for (int i = 0; i < n; i++)
            {
                var sz = g.MeasureString(s[i].ToString(), f, int.MaxValue, StringFormat.GenericTypographic);
                w[i] = sz.Width;
                total += sz.Width + spacing;
                if (sz.Height > lineH) lineH = sz.Height;
            }
            total -= spacing;
            float x = h == StringAlignment.Center ? r.X + (r.Width - total) / 2f
                    : h == StringAlignment.Far ? r.Right - total : r.X;
            float y = r.Y + (r.Height - lineH) / 2f;
            using (var b = new SolidBrush(c))
            {
                for (int i = 0; i < n; i++)
                {
                    g.DrawString(s[i].ToString(), f, b, x, y, StringFormat.GenericTypographic);
                    x += w[i] + spacing;
                }
            }
        }

        // ---------------- icons ----------------
        public static void IconPlus(Graphics g, RectangleF r, Color c, float w)
        {
            using (var p = new Pen(c, w))
            {
                p.StartCap = LineCap.Round; p.EndCap = LineCap.Round;
                float cx = r.X + r.Width / 2f, cy = r.Y + r.Height / 2f;
                float k = Math.Min(r.Width, r.Height) * 0.28f;
                g.DrawLine(p, cx - k, cy, cx + k, cy);
                g.DrawLine(p, cx, cy - k, cx, cy + k);
            }
        }

        public static void IconMinus(Graphics g, RectangleF r, Color c, float w)
        {
            using (var p = new Pen(c, w))
            {
                p.StartCap = LineCap.Round; p.EndCap = LineCap.Round;
                float cx = r.X + r.Width / 2f, cy = r.Y + r.Height / 2f;
                float k = Math.Min(r.Width, r.Height) * 0.28f;
                g.DrawLine(p, cx - k, cy, cx + k, cy);
            }
        }

        public static void IconClose(Graphics g, RectangleF r, Color c, float w)
        {
            using (var p = new Pen(c, w))
            {
                p.StartCap = LineCap.Round; p.EndCap = LineCap.Round;
                float cx = r.X + r.Width / 2f, cy = r.Y + r.Height / 2f;
                float k = Math.Min(r.Width, r.Height) * 0.25f;
                g.DrawLine(p, cx - k, cy - k, cx + k, cy + k);
                g.DrawLine(p, cx + k, cy - k, cx - k, cy + k);
            }
        }

        public static void IconChevron(Graphics g, RectangleF r, Color c, float w, bool left)
        {
            using (var p = new Pen(c, w))
            {
                p.StartCap = LineCap.Round; p.EndCap = LineCap.Round;
                float cx = r.X + r.Width / 2f, cy = r.Y + r.Height / 2f;
                float kx = Math.Min(r.Width, r.Height) * 0.22f;
                float ky = Math.Min(r.Width, r.Height) * 0.32f;
                if (left)
                {
                    g.DrawLine(p, cx + kx * 0.6f, cy - ky, cx - kx, cy);
                    g.DrawLine(p, cx - kx, cy, cx + kx * 0.6f, cy + ky);
                }
                else
                {
                    g.DrawLine(p, cx - kx * 0.6f, cy - ky, cx + kx, cy);
                    g.DrawLine(p, cx + kx, cy, cx - kx * 0.6f, cy + ky);
                }
            }
        }

        public static void IconPencil(Graphics g, RectangleF r, Color c, float w)
        {
            using (var p = new Pen(c, w))
            {
                p.StartCap = LineCap.Round; p.EndCap = LineCap.Round;
                float x0 = r.X + r.Width * 0.18f, y0 = r.Bottom - r.Height * 0.18f;
                float x1 = r.Right - r.Width * 0.20f, y1 = r.Y + r.Height * 0.20f;
                g.DrawLine(p, x0, y0, x1, y1);
                g.DrawLine(p, x0 - r.Width * 0.04f, y0 + r.Height * 0.12f, x0 + r.Width * 0.12f, y0 + r.Height * 0.28f);
                g.DrawLine(p, x1 - r.Width * 0.02f, y1 - r.Height * 0.10f, x1 + r.Width * 0.14f, y1 + r.Height * 0.06f);
            }
        }

        public static void IconPin(Graphics g, RectangleF r, Color c, float w)
        {
            float cx = r.X + r.Width / 2f;
            float top = r.Y + r.Height * 0.14f;
            using (var pen = new Pen(c, w))
            {
                pen.StartCap = LineCap.Round; pen.EndCap = LineCap.Round;
                g.DrawLine(pen, cx - r.Width * 0.26f, top + r.Height * 0.02f, cx + r.Width * 0.26f, top + r.Height * 0.02f);
                g.DrawLine(pen, cx - r.Width * 0.15f, top + r.Height * 0.12f, cx + r.Width * 0.15f, top + r.Height * 0.12f);
                g.DrawLine(pen, cx - r.Width * 0.15f, top + r.Height * 0.12f, cx, top + r.Height * 0.52f);
                g.DrawLine(pen, cx + r.Width * 0.15f, top + r.Height * 0.12f, cx, top + r.Height * 0.52f);
                g.DrawLine(pen, cx, top + r.Height * 0.52f, cx, r.Bottom - r.Height * 0.08f);
            }
        }

        public static void IconClock(Graphics g, RectangleF r, Color c, float w)
        {
            using (var p = new Pen(c, w))
            {
                p.StartCap = LineCap.Round; p.EndCap = LineCap.Round;
                g.DrawEllipse(p, r);
                float cx = r.X + r.Width / 2f, cy = r.Y + r.Height / 2f;
                g.DrawLine(p, cx, cy, cx, cy - r.Height * 0.28f);
                g.DrawLine(p, cx, cy, cx + r.Width * 0.22f, cy + r.Height * 0.10f);
            }
        }

        public static void IconCalendar(Graphics g, RectangleF r, Color c, float w)
        {
            using (var p = new Pen(c, w))
            {
                p.StartCap = LineCap.Round; p.EndCap = LineCap.Round;
                var rr = new RectangleF(r.X, r.Y + r.Height * 0.10f, r.Width, r.Height * 0.82f);
                using (var path = Round(rr, r.Width * 0.16f)) g.DrawPath(p, path);
                float y = r.Y + r.Height * 0.38f;
                g.DrawLine(p, r.X + r.Width * 0.08f, y, r.Right - r.Width * 0.08f, y);
                g.DrawLine(p, r.X + r.Width * 0.28f, r.Y + r.Height * 0.02f, r.X + r.Width * 0.28f, r.Y + r.Height * 0.20f);
                g.DrawLine(p, r.X + r.Width * 0.72f, r.Y + r.Height * 0.02f, r.X + r.Width * 0.72f, r.Y + r.Height * 0.20f);
            }
        }

        public static void IconTrash(Graphics g, RectangleF r, Color c, float w)
        {
            using (var p = new Pen(c, w))
            {
                p.StartCap = LineCap.Round; p.EndCap = LineCap.Round;
                g.DrawLine(p, r.X + r.Width * 0.18f, r.Y + r.Height * 0.26f, r.Right - r.Width * 0.18f, r.Y + r.Height * 0.26f);
                g.DrawLine(p, r.X + r.Width * 0.34f, r.Y + r.Height * 0.12f, r.Right - r.Width * 0.34f, r.Y + r.Height * 0.12f);
                g.DrawLine(p, r.X + r.Width * 0.26f, r.Y + r.Height * 0.34f, r.X + r.Width * 0.32f, r.Bottom - r.Height * 0.08f);
                g.DrawLine(p, r.Right - r.Width * 0.26f, r.Y + r.Height * 0.34f, r.Right - r.Width * 0.32f, r.Bottom - r.Height * 0.08f);
                g.DrawLine(p, r.X + r.Width * 0.32f, r.Bottom - r.Height * 0.08f, r.Right - r.Width * 0.32f, r.Bottom - r.Height * 0.08f);
            }
        }

        public static void IconCheck(Graphics g, RectangleF r, Color c, float w)
        {
            using (var p = new Pen(c, w))
            {
                p.StartCap = LineCap.Round; p.EndCap = LineCap.Round;
                g.DrawLines(p, new PointF[]
                {
                    new PointF(r.X + r.Width * 0.18f, r.Y + r.Height * 0.52f),
                    new PointF(r.X + r.Width * 0.42f, r.Y + r.Height * 0.74f),
                    new PointF(r.X + r.Width * 0.84f, r.Y + r.Height * 0.26f)
                });
            }
        }

        public static void IconSun(Graphics g, RectangleF r, Color c, float w)
        {
            using (var p = new Pen(c, w))
            {
                p.StartCap = LineCap.Round; p.EndCap = LineCap.Round;
                float cx = r.X + r.Width / 2f, cy = r.Y + r.Height / 2f;
                float rad = Math.Min(r.Width, r.Height) * 0.23f;
                g.DrawEllipse(p, cx - rad, cy - rad, rad * 2, rad * 2);
                for (int i = 0; i < 8; i++)
                {
                    double a = Math.PI / 4 * i;
                    float x1 = cx + (float)Math.Cos(a) * rad * 1.55f;
                    float y1 = cy + (float)Math.Sin(a) * rad * 1.55f;
                    float x2 = cx + (float)Math.Cos(a) * rad * 2.05f;
                    float y2 = cy + (float)Math.Sin(a) * rad * 2.05f;
                    g.DrawLine(p, x1, y1, x2, y2);
                }
            }
        }

        public static void IconMoon(Graphics g, RectangleF r, Color c, float w)
        {
            using (var p = new Pen(c, w))
            {
                p.StartCap = LineCap.Round; p.EndCap = LineCap.Round;
                var arc = new RectangleF(r.X + r.Width * 0.08f, r.Y + r.Height * 0.08f,
                    r.Width * 0.84f, r.Height * 0.84f);
                g.DrawArc(p, arc, 70f, 230f);
            }
        }

        public static void IconMonitor(Graphics g, RectangleF r, Color c, float w)
        {
            using (var p = new Pen(c, w))
            {
                p.StartCap = LineCap.Round; p.EndCap = LineCap.Round;
                var rr = new RectangleF(r.X + r.Width * 0.08f, r.Y + r.Height * 0.12f,
                    r.Width * 0.84f, r.Height * 0.60f);
                using (var path = Round(rr, r.Width * 0.08f)) g.DrawPath(p, path);
                g.DrawLine(p, r.X + r.Width * 0.30f, r.Bottom - r.Height * 0.08f, r.Right - r.Width * 0.30f, r.Bottom - r.Height * 0.08f);
                g.DrawLine(p, r.X + r.Width * 0.42f, r.Y + r.Height * 0.72f, r.X + r.Width * 0.42f, r.Bottom - r.Height * 0.08f);
                g.DrawLine(p, r.Right - r.Width * 0.42f, r.Y + r.Height * 0.72f, r.Right - r.Width * 0.42f, r.Bottom - r.Height * 0.08f);
            }
        }

        public static void IconMini(Graphics g, RectangleF r, Color c, float w)
        {
            using (var p = new Pen(c, w))
            {
                p.StartCap = LineCap.Round; p.EndCap = LineCap.Round;
                var rr = new RectangleF(r.X + r.Width * 0.10f, r.Y + r.Height * 0.14f,
                    r.Width * 0.80f, r.Height * 0.66f);
                using (var path = Round(rr, r.Width * 0.12f)) g.DrawPath(p, path);
                var inner = new RectangleF(r.X + r.Width * 0.48f, r.Y + r.Height * 0.46f,
                    r.Width * 0.40f, r.Height * 0.34f);
                using (var path = Round(inner, r.Width * 0.07f))
                using (var brush = new SolidBrush(c)) g.FillPath(brush, path);
            }
        }

        public static void IconExpand(Graphics g, RectangleF r, Color c, float w)
        {
            using (var p = new Pen(c, w))
            {
                p.StartCap = LineCap.Round; p.EndCap = LineCap.Round;
                float x1 = r.X + r.Width * 0.16f, y1 = r.Y + r.Height * 0.84f;
                float x2 = r.Right - r.Width * 0.16f, y2 = r.Y + r.Height * 0.16f;
                g.DrawLine(p, x1, y1, x2, y2);
                g.DrawLine(p, x2 - r.Width * 0.28f, y2, x2, y2);
                g.DrawLine(p, x2, y2 + r.Height * 0.28f, x2, y2);
                g.DrawLine(p, x2, y1, x1, y2);
                g.DrawLine(p, x1 + r.Width * 0.28f, y1, x1, y1);
                g.DrawLine(p, x1, y1 - r.Height * 0.28f, x1, y1);
            }
        }

        public static void Switch(Graphics g, RectangleF r, bool on, Color accent)
        {
            float rad = r.Height / 2f;
            Ui.FillRound(g, r, rad, on ? accent : Overlay(28));
            if (!on) Ui.StrokeRound(g, r, rad, Overlay(38), 1f);
            float knob = r.Height - Ui.SF(4);
            float kx = on ? r.Right - knob - Ui.SF(2) : r.X + Ui.SF(2);
            var kr = new RectangleF(kx, r.Y + Ui.SF(2), knob, knob);
            Ui.FillRound(g, kr, knob / 2f, Color.White);
            Ui.StrokeRound(g, kr, knob / 2f, Ui.Shadow(35), 1f);
        }

        public static void Log(string tag, Exception ex)
        {
            try
            {
                System.IO.Directory.CreateDirectory(ConfigStore.TempFolder);
                System.IO.File.AppendAllText(System.IO.Path.Combine(ConfigStore.TempFolder, "error.log"),
                    DateTime.Now.ToString("HH:mm:ss.fff") + " [" + tag + "]\r\n" + ex + "\r\n\r\n");
            }
            catch { }
        }

        // ---------------- windows helpers ----------------
        [DllImport("dwmapi.dll")]
        static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

        public static void RoundWindow(Form f)
        {
            if (!f.IsHandleCreated) return;
            try
            {
                int pref = 2; // DWMWCP_ROUND
                DwmSetWindowAttribute(f.Handle, 33, ref pref, 4);
            }
            catch { }
        }

        public static bool IsOnScreen(Rectangle bounds)
        {
            foreach (var s in Screen.AllScreens)
            {
                var inter = Rectangle.Intersect(s.WorkingArea, bounds);
                if (inter.Width >= Math.Min(120, bounds.Width) && inter.Height >= Math.Min(60, bounds.Height))
                    return true;
            }
            return false;
        }
    }
}
