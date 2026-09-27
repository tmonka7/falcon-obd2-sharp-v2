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

        private readonly TouchScroller _scroller;
        private int _hover = -1;
        private int _scroll => _scroller.Offset;
        private RectangleF _leftBtn, _rightBtn;

        public ModuleCardStrip()
        {
            BackColor = Theme.Background;
            Height = 124;
            _scroller = new TouchScroller(this, horizontal: true) { Max = () => MaxScroll };
            _scroller.Scrolled += Invalidate;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _scroller.Dispose();
            base.Dispose(disposing);
        }

        private int ContentWidth => Modules.Count * (CardWidth + Gap) - Gap;
        private int ViewWidth => Width - 38;
        private int MaxScroll => Math.Max(0, ContentWidth - ViewWidth);

        public void EnsureVisible(ControlModule m)
        {
            int idx = Modules.IndexOf(m);
            if (idx < 0) return;
            if (_scroller.IsPressed) return; // do not fight the user's finger
            int x = idx * (CardWidth + Gap);
            if (x < _scroll) _scroller.SetOffset(x);
            else if (x + CardWidth > _scroll + ViewWidth) _scroller.SetOffset(x + CardWidth - ViewWidth);
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

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left && !_leftBtn.Contains(e.Location) && !_rightBtn.Contains(e.Location)) _scroller.MouseDown(e.Location);
            base.OnMouseDown(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            _scroller.MouseUp();
            if (Touch.IsTouchMessage()) { _hover = -1; Invalidate(); }
            base.OnMouseUp(e);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (_scroller.MouseMove(e.Location)) { _hover = -1; base.OnMouseMove(e); return; }
            int h = HitTest(e.Location);
            bool btn = _leftBtn.Contains(e.Location) || _rightBtn.Contains(e.Location);
            Cursor = h >= 0 || btn ? Cursors.Hand : Cursors.Default;
            if (h != _hover) { _hover = h; Invalidate(); }
            base.OnMouseMove(e);
        }

        protected override void OnMouseLeave(EventArgs e) { _hover = -1; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            _scroller.Wheel(e.Delta, CardWidth + Gap);
            base.OnMouseWheel(e);
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            if (_scroller.SuppressClick) return; // the press was a swipe
            if (_rightBtn.Contains(e.Location)) { _scroller.SetOffset(_scroll + (CardWidth + Gap) * 2); return; }
            if (_leftBtn.Contains(e.Location)) { _scroller.SetOffset(_scroll - (CardWidth + Gap) * 2); return; }
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
            _rightBtn = new RectangleF(Width - 34, 4 + CardHeight / 2f - 24, 32, 48);
            _leftBtn = MaxScroll > 0 && _scroll > 0 ? new RectangleF(-2, 4 + CardHeight / 2f - 24, 32, 48) : RectangleF.Empty;
            Theme.FillRounded(g, _rightBtn, 8, Theme.Card);
            Theme.DrawRounded(g, _rightBtn, 8, Theme.Border);
            Icons.Draw(g, "forward", new RectangleF(_rightBtn.X + 9, _rightBtn.Y + 16, 14, 16), _scroll < MaxScroll ? Theme.Text : Theme.TextDim, 1.8f);
            if (_leftBtn.Width > 0)
            {
                Theme.FillRounded(g, _leftBtn, 8, Theme.Card);
                Theme.DrawRounded(g, _leftBtn, 8, Theme.Border);
                Icons.Draw(g, "back", new RectangleF(_leftBtn.X + 9, _leftBtn.Y + 16, 14, 16), Theme.Text, 1.8f);
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
