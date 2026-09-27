using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using RedlineDiagnostics.App;
using RedlineDiagnostics.Diagnostics;

namespace RedlineDiagnostics.Controls
{
    /// <summary>Horizontally scrolling strip of control-module cards (bottom of the scan screen).</summary>
    public sealed class ModuleCardStrip : BaseControl
    {
        public int CardWidth { get; set; } = 172;
        public int CardHeight { get; set; } = 112;
        public int Gap { get; set; } = 10;
        public IList<ControlModule> Modules { get; set; } = new List<ControlModule>();
        public ControlModule SelectedModule { get; set; }
        public ControlModule ActiveModule { get; set; }
        public event Action<ControlModule> ModuleSelected;

        private int _scroll;
        private int _hover = -1;
        private RectangleF _leftBtn, _rightBtn;

        public ModuleCardStrip()
        {
            BackColor = Theme.Background;
            Height = 124;
        }

        private int ContentWidth => Modules.Count * (CardWidth + Gap) - Gap;
        private int ViewWidth => Width - 36;
        private int MaxScroll => Math.Max(0, ContentWidth - ViewWidth);

        public void EnsureVisible(ControlModule m)
        {
            int idx = Modules.IndexOf(m);
            if (idx < 0) return;
            int x = idx * (CardWidth + Gap);
            if (x < _scroll) _scroll = x;
            else if (x + CardWidth > _scroll + ViewWidth) _scroll = x + CardWidth - ViewWidth;
            _scroll = Math.Max(0, Math.Min(MaxScroll, _scroll));
            Invalidate();
        }

        private int HitTest(Point p)
        {
            if (p.X < 0 || p.X > ViewWidth) return -1;
            int x = p.X + _scroll;
            int idx = x / (CardWidth + Gap);
            if (idx < 0 || idx >= Modules.Count) return -1;
            if (x - idx * (CardWidth + Gap) > CardWidth) return -1;
            if (p.Y < 4 || p.Y > 4 + CardHeight) return -1;
            return idx;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            int h = HitTest(e.Location);
            bool btn = _leftBtn.Contains(e.Location) || _rightBtn.Contains(e.Location);
            Cursor = h >= 0 || btn ? Cursors.Hand : Cursors.Default;
            if (h != _hover) { _hover = h; Invalidate(); }
            base.OnMouseMove(e);
        }

        protected override void OnMouseLeave(EventArgs e) { _hover = -1; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            _scroll = Math.Max(0, Math.Min(MaxScroll, _scroll - Math.Sign(e.Delta) * (CardWidth + Gap)));
            Invalidate();
            base.OnMouseWheel(e);
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            if (_rightBtn.Contains(e.Location)) { _scroll = Math.Min(MaxScroll, _scroll + (CardWidth + Gap) * 2); Invalidate(); return; }
            if (_leftBtn.Contains(e.Location)) { _scroll = Math.Max(0, _scroll - (CardWidth + Gap) * 2); Invalidate(); return; }
            int h = HitTest(e.Location);
            if (h >= 0)
            {
                SelectedModule = Modules[h];
                ModuleSelected?.Invoke(SelectedModule);
                Invalidate();
            }
            base.OnMouseClick(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            Theme.Setup(g);
            var clip = new Rectangle(0, 0, ViewWidth, Height);
            g.SetClip(clip);
            using (var fShort = F(10f, true))
            using (var fName = F(7.5f))
            using (var fStatus = F(8.5f))
            using (var fMs = F(8f))
            {
                for (int i = 0; i < Modules.Count; i++)
                {
                    int x = i * (CardWidth + Gap) - _scroll;
                    if (x + CardWidth < 0 || x > ViewWidth) continue;
                    DrawCard(g, Modules[i], new RectangleF(x, 4, CardWidth, CardHeight), i == _hover, fShort, fName, fStatus, fMs);
                }
            }
            g.ResetClip();

            // scroll buttons
            _rightBtn = new RectangleF(Width - 30, 4 + CardHeight / 2f - 18, 26, 36);
            _leftBtn = MaxScroll > 0 && _scroll > 0 ? new RectangleF(-2, 4 + CardHeight / 2f - 18, 26, 36) : RectangleF.Empty;
            Theme.FillRounded(g, _rightBtn, 8, Theme.Card);
            Theme.DrawRounded(g, _rightBtn, 8, Theme.Border);
            Icons.Draw(g, "forward", new RectangleF(_rightBtn.X + 6, _rightBtn.Y + 10, 14, 16), _scroll < MaxScroll ? Theme.Text : Theme.TextDim, 1.8f);
            if (_leftBtn.Width > 0)
            {
                Theme.FillRounded(g, _leftBtn, 8, Theme.Card);
                Theme.DrawRounded(g, _leftBtn, 8, Theme.Border);
                Icons.Draw(g, "back", new RectangleF(_leftBtn.X + 6, _leftBtn.Y + 10, 14, 16), Theme.Text, 1.8f);
            }
        }

        private void DrawCard(Graphics g, ControlModule m, RectangleF r, bool hover, Font fShort, Font fName, Font fStatus, Font fMs)
        {
            var c = Theme.StatusColor(m.Status);
            bool selected = m == SelectedModule;
            bool active = m == ActiveModule || m.Status == ModuleStatus.Scanning;
            var borderColor = selected || active ? c : hover ? Theme.WithAlpha(Theme.Cyan, 140) : Theme.Border;
            if (selected || active) Theme.Glow(g, r, 10, c, 6, active ? 130 : 90);
            Theme.FillRounded(g, r, 10, selected || active ? Theme.Lerp(Theme.Card, c, 0.08f) : Theme.Card);
            Theme.DrawRounded(g, r, 10, borderColor, selected || active ? 1.5f : 1f);

            var badgeColor = m.Status == ModuleStatus.Pending ? Theme.Blue : c;
            Icons.Badge(g, m.Icon, new RectangleF(r.X + 12, r.Y + 12, 32, 32), badgeColor, 7);
            Theme.DrawText(g, m.Short, fShort, Theme.Text, new Point((int)r.X + 54, (int)r.Y + 12));
            Theme.DrawText(g, m.Name, fName, Theme.TextMuted, new Rectangle((int)r.X + 54, (int)r.Y + 31, (int)r.Width - 62, 16), TextFormatFlags.Left | TextFormatFlags.EndEllipsis);

            float sy = r.Y + r.Height - 32;
            Icons.StatusDot(g, new RectangleF(r.X + 14, sy, 14, 14), m.Status);
            Theme.DrawText(g, m.StatusText, fStatus, c, new Point((int)r.X + 34, (int)sy - 2));

            string right = "—";
            if (m.Status == ModuleStatus.Scanning) right = m.Progress + "%";
            else if (m.IsDone && m.ResponseTimeMs >= 0 && m.Status != ModuleStatus.NoResponse) right = m.ResponseTimeMs + " ms";
            var szR = Theme.Measure(right, fMs);
            Theme.DrawText(g, right, fMs, m.Status == ModuleStatus.Scanning ? c : Theme.TextMuted, new Point((int)(r.Right - 12 - szR.Width), (int)sy - 1));

            if (m.Status == ModuleStatus.Scanning)
                Theme.ProgressBar(g, new RectangleF(r.X + 14, r.Bottom - 12, r.Width - 28, 4), m.Progress / 100f, c, Theme.SurfaceAlt);
            else if (m.IsDone)
            {
                using (var pen = new Pen(Theme.WithAlpha(c, 90), 2f))
                    g.DrawLine(pen, r.X + 14, r.Bottom - 10, r.Right - 14, r.Bottom - 10);
            }
        }
    }
}
