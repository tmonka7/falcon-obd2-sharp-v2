using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;
using RedlineDiagnostics.Localization;

namespace RedlineDiagnostics.App
{
    /// <summary>Colors, fonts and GDI+ drawing helpers shared by every custom-painted control.</summary>
    public static class Theme
    {
        // Backgrounds
        public static readonly Color Background = ColorTranslator.FromHtml("#000000");
        public static readonly Color Surface = ColorTranslator.FromHtml("#0E0E10");
        public static readonly Color SurfaceAlt = ColorTranslator.FromHtml("#17171A");
        public static readonly Color Card = ColorTranslator.FromHtml("#0A0A0C");
        public static readonly Color Border = ColorTranslator.FromHtml("#2A2A2F");
        public static readonly Color BorderSoft = ColorTranslator.FromHtml("#1D1D21");
        public static readonly Color TopBar = ColorTranslator.FromHtml("#060607");
        public static readonly Color Sidebar = ColorTranslator.FromHtml("#060607");

        // Accents
        public static readonly Color Red = ColorTranslator.FromHtml("#E5173C");
        public static readonly Color RedBright = ColorTranslator.FromHtml("#FF2A4D");
        public static readonly Color RedDark = ColorTranslator.FromHtml("#6B0F22");
        public static readonly Color Cyan = ColorTranslator.FromHtml("#22D3EE");
        public static readonly Color Blue = ColorTranslator.FromHtml("#3B82F6");
        public static readonly Color Green = ColorTranslator.FromHtml("#22C55E");
        public static readonly Color Orange = ColorTranslator.FromHtml("#F59E0B");
        public static readonly Color Grey = ColorTranslator.FromHtml("#64748B");
        public static readonly Color Purple = ColorTranslator.FromHtml("#A78BFA");

        // Text
        public static readonly Color Text = ColorTranslator.FromHtml("#E8ECF5");
        public static readonly Color TextMuted = ColorTranslator.FromHtml("#8B95AB");
        public static readonly Color TextDim = ColorTranslator.FromHtml("#5B647A");

        public static Color WithAlpha(Color c, int alpha) => Color.FromArgb(Math.Max(0, Math.Min(255, alpha)), c);

        public static Color Lerp(Color a, Color b, float t)
        {
            t = Math.Max(0, Math.Min(1, t));
            return Color.FromArgb(
                (int)(a.A + (b.A - a.A) * t),
                (int)(a.R + (b.R - a.R) * t),
                (int)(a.G + (b.G - a.G) * t),
                (int)(a.B + (b.B - a.B) * t));
        }

        // ---------- Fonts ----------
        private static string _family;

        public static string FontFamilyName
        {
            get
            {
                if (_family == null) _family = ResolveFamily();
                return _family;
            }
        }

        public static void ResetFonts()
        {
            _family = null;
        }

        private static string ResolveFamily()
        {
            string[] candidates;
            switch (Loc.Current)
            {
                case Language.JA: candidates = new[] { "Yu Gothic UI", "Meiryo UI", "Segoe UI" }; break;
                case Language.ZH: candidates = new[] { "Microsoft YaHei UI", "Microsoft YaHei", "Segoe UI" }; break;
                default: candidates = new[] { "Segoe UI", "Arial" }; break;
            }
            using (var fonts = new InstalledFontCollection())
            {
                foreach (var c in candidates)
                    foreach (var f in fonts.Families)
                        if (string.Equals(f.Name, c, StringComparison.OrdinalIgnoreCase)) return c;
            }
            return "Segoe UI";
        }

        public static Font Font(float size, FontStyle style = FontStyle.Regular)
        {
            try { return new Font(FontFamilyName, size, style, GraphicsUnit.Point); }
            catch { return new Font("Segoe UI", size, style, GraphicsUnit.Point); }
        }

        public static Font Bold(float size) => Font(size, FontStyle.Bold);
        public static Font Mono(float size) => new Font("Consolas", size, FontStyle.Regular, GraphicsUnit.Point);

        // ---------- Drawing helpers ----------
        public static GraphicsPath RoundedRect(RectangleF r, float radius)
        {
            var path = new GraphicsPath();
            float d = radius * 2;
            if (d <= 0 || r.Width <= 0 || r.Height <= 0)
            {
                path.AddRectangle(new RectangleF(r.X, r.Y, Math.Max(1, r.Width), Math.Max(1, r.Height)));
                return path;
            }
            d = Math.Min(d, Math.Min(r.Width, r.Height));
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        public static GraphicsPath RoundedRect(Rectangle r, float radius) => RoundedRect(new RectangleF(r.X, r.Y, r.Width, r.Height), radius);

        public static void FillRounded(Graphics g, RectangleF r, float radius, Color fill)
        {
            using (var p = RoundedRect(r, radius))
            using (var b = new SolidBrush(fill))
                g.FillPath(b, p);
        }

        public static void DrawRounded(Graphics g, RectangleF r, float radius, Color stroke, float width = 1f)
        {
            using (var p = RoundedRect(r, radius))
            using (var pen = new Pen(stroke, width))
                g.DrawPath(pen, p);
        }

        public static void Panel(Graphics g, RectangleF r, float radius, Color fill, Color border)
        {
            FillRounded(g, r, radius, fill);
            DrawRounded(g, new RectangleF(r.X + 0.5f, r.Y + 0.5f, r.Width - 1, r.Height - 1), radius, border);
        }

        /// <summary>Draws a soft outer glow around a rounded rectangle.</summary>
        public static void Glow(Graphics g, RectangleF r, float radius, Color color, int spread = 6, int maxAlpha = 90)
        {
            var old = g.SmoothingMode;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            for (int i = spread; i >= 1; i--)
            {
                int alpha = (int)(maxAlpha * (1f - (float)i / (spread + 1)) / spread * 2);
                var rr = RectangleF.Inflate(r, i, i);
                using (var p = RoundedRect(rr, radius + i))
                using (var pen = new Pen(WithAlpha(color, alpha), 1.5f))
                    g.DrawPath(pen, p);
            }
            g.SmoothingMode = old;
        }

        public static void GlowDot(Graphics g, PointF c, float radius, Color color, int rings = 4)
        {
            var old = g.SmoothingMode;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            for (int i = rings; i >= 1; i--)
            {
                float rr = radius + i * radius * 0.9f;
                using (var b = new SolidBrush(WithAlpha(color, 26 - i * 4)))
                    g.FillEllipse(b, c.X - rr, c.Y - rr, rr * 2, rr * 2);
            }
            using (var b = new SolidBrush(color))
                g.FillEllipse(b, c.X - radius, c.Y - radius, radius * 2, radius * 2);
            g.SmoothingMode = old;
        }

        public static void DrawText(Graphics g, string text, Font font, Color color, Rectangle rect, TextFormatFlags flags)
        {
            // PreserveGraphicsClipping: TextRenderer ignores Graphics.SetClip otherwise, which scroll viewports rely on.
            TextRenderer.DrawText(g, text, font, rect, color, flags | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | TextFormatFlags.PreserveGraphicsClipping);
        }

        public static void DrawText(Graphics g, string text, Font font, Color color, Point at)
        {
            TextRenderer.DrawText(g, text, font, at, color, TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | TextFormatFlags.PreserveGraphicsClipping);
        }

        public static Size Measure(string text, Font font)
        {
            return TextRenderer.MeasureText(text, font, new Size(int.MaxValue, int.MaxValue), TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);
        }

        public static void ProgressBar(Graphics g, RectangleF r, float fraction, Color color, Color track)
        {
            fraction = Math.Max(0, Math.Min(1, fraction));
            var old = g.SmoothingMode;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            FillRounded(g, r, r.Height / 2, track);
            if (fraction > 0)
            {
                var fr = new RectangleF(r.X, r.Y, Math.Max(r.Height, r.Width * fraction), r.Height);
                using (var p = RoundedRect(fr, r.Height / 2))
                using (var lg = new LinearGradientBrush(new RectangleF(fr.X - 1, fr.Y - 1, fr.Width + 2, fr.Height + 2), Lerp(color, Color.White, 0.15f), color, LinearGradientMode.Horizontal))
                    g.FillPath(lg, p);
                for (int i = 3; i >= 1; i--)
                {
                    using (var p = RoundedRect(RectangleF.Inflate(fr, i, i), r.Height / 2 + i))
                    using (var pen = new Pen(WithAlpha(color, 30 - i * 8), 1f))
                        g.DrawPath(pen, p);
                }
            }
            g.SmoothingMode = old;
        }

        public static void Setup(Graphics g)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        }

        public static Color StatusColor(Diagnostics.ModuleStatus status)
        {
            switch (status)
            {
                case Diagnostics.ModuleStatus.Scanning: return Cyan;
                case Diagnostics.ModuleStatus.Passed: return Green;
                case Diagnostics.ModuleStatus.Warning: return Orange;
                case Diagnostics.ModuleStatus.Fault: return Red;
                case Diagnostics.ModuleStatus.NoResponse: return Grey;
                default: return Blue;
            }
        }
    }
}
