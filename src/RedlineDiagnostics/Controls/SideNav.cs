using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using RedlineDiagnostics.App;
using RedlineDiagnostics.Localization;

namespace RedlineDiagnostics.Controls
{
    /// <summary>Left navigation rail.</summary>
    public sealed class SideNav : BaseControl
    {
        public static readonly string[] Keys = { "nav.home", "nav.diagnose", "nav.livedata", "nav.vehicle", "nav.reports", "nav.history", "nav.garage", "nav.settings" };
        private static readonly string[] IconNames = { "home", "diagnose", "livedata", "vehicle", "reports", "history", "garage", "settings" };

        private int _selected;
        private int _hover = -1;
        public int ItemHeight { get; set; } = 66;
        public int TopOffset { get; set; } = 10;

        public event Action<int> Navigate;

        public SideNav()
        {
            Width = 160;
            BackColor = Theme.Sidebar;
            CarImages.Changed += Invalidate;
        }

        public int SelectedIndex
        {
            get { return _selected; }
            set { if (_selected != value) { _selected = value; Invalidate(); } }
        }

        private int HitTest(Point p)
        {
            int idx = (p.Y - TopOffset) / ItemHeight;
            return p.Y >= TopOffset && idx >= 0 && idx < Keys.Length ? idx : -1;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            int h = HitTest(e.Location);
            if (h != _hover) { _hover = h; Cursor = h >= 0 ? Cursors.Hand : Cursors.Default; Invalidate(); }
            base.OnMouseMove(e);
        }

        protected override void OnMouseLeave(EventArgs e) { _hover = -1; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            if (Touch.IsTouchMessage()) { _hover = -1; Invalidate(); }
            base.OnMouseUp(e);
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            int h = HitTest(e.Location);
            if (h >= 0)
            {
                SelectedIndex = h;
                Navigate?.Invoke(h);
            }
            base.OnMouseClick(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            Theme.Setup(g);
            using (var pen = new Pen(Theme.BorderSoft, 1f))
                g.DrawLine(pen, Width - 1, 0, Width - 1, Height);

            using (var f = F(10.5f))
            using (var fb = F(10.5f, true))
            {
                for (int i = 0; i < Keys.Length; i++)
                {
                    var r = new Rectangle(0, TopOffset + i * ItemHeight, Width, ItemHeight);
                    bool sel = i == _selected, hov = i == _hover;
                    if (sel)
                    {
                        var rr = new RectangleF(0, r.Y + 4, Width - 1, ItemHeight - 8);
                        using (var b = new LinearGradientBrush(rr, Theme.WithAlpha(Theme.Red, 150), Theme.WithAlpha(Theme.Red, 10), LinearGradientMode.Horizontal))
                            g.FillRectangle(b, rr);
                        using (var b = new SolidBrush(Theme.RedBright))
                            g.FillRectangle(b, 0, rr.Y, 4, rr.Height);
                    }
                    else if (hov)
                    {
                        Theme.FillRounded(g, new RectangleF(8, r.Y + 6, Width - 16, ItemHeight - 12), 8, Theme.WithAlpha(Theme.Cyan, 18));
                    }
                    var color = sel ? Color.White : hov ? Theme.Text : Theme.TextMuted;
                    Icons.Draw(g, IconNames[i], new RectangleF(24, r.Y + ItemHeight / 2f - 12, 24, 24), color, 1.7f);
                    Theme.DrawText(g, Loc.T(Keys[i]), sel ? fb : f, color, new Rectangle(60, r.Y, Width - 64, ItemHeight), TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
                    using (var pen = new Pen(Theme.WithAlpha(Theme.BorderSoft, 160), 1f))
                        g.DrawLine(pen, 16, r.Bottom - 1, Width - 16, r.Bottom - 1);
                }
            }
            PaintFooter(g);
        }

        /// <summary>Red-lit car and slogan in the free space under the menu.</summary>
        private void PaintFooter(Graphics g)
        {
            int menuBottom = TopOffset + Keys.Length * ItemHeight;
            int top = Height - 168;
            if (top < menuBottom + 6) return;
            using (var gp = new GraphicsPath())
            {
                gp.AddEllipse(-60, top + 10, Width + 60, 150);
                using (var pb = new PathGradientBrush(gp) { CenterColor = Theme.WithAlpha(Theme.Red, 70), SurroundColors = new[] { Theme.WithAlpha(Theme.Red, 0) } })
                    g.FillPath(pb, gp);
            }
            var img = CarImages.Get(CarShot.Nav, new Size(148, 76));
            if (img != null) g.DrawImage(img, 4, top, 148, 76);
            using (var f = F(9.5f))
            {
                var lines = Loc.T("home.sideSlogan").Split('\n');
                for (int i = 0; i < lines.Length; i++)
                    Theme.DrawText(g, lines[i], f, Theme.Text, new Point(18, top + 92 + i * 19));
            }
            var bar = new RectangleF(18, Height - 24, 68, 3);
            using (var b = new LinearGradientBrush(new RectangleF(bar.X - 1, bar.Y, bar.Width + 2, bar.Height), Theme.RedBright, Theme.WithAlpha(Theme.Red, 60), LinearGradientMode.Horizontal))
                g.FillRectangle(b, bar);
        }
    }
}
