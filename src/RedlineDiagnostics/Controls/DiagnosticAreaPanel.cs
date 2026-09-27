using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;
using RedlineDiagnostics.App;
using RedlineDiagnostics.Database;
using RedlineDiagnostics.Diagnostics;
using RedlineDiagnostics.Localization;
using RedlineDiagnostics.Obd;

namespace RedlineDiagnostics.Controls
{
    /// <summary>Right-hand "Current Diagnostic Area" panel of the scan screen.</summary>
    public sealed class DiagnosticAreaPanel : BaseControl
    {
        private readonly Timer _timer = new Timer { Interval = 250 };
        private readonly TouchScroller _scroller;
        private Rectangle _listViewport;
        private int _listContent;
        private ControlModule _module;

        public ControlModule Module
        {
            get { return _module; }
            set { if (_module != value) { _module = value; _scroller?.Reset(); } }
        }
        public Dictionary<string, double> LiveOverride { get; set; }

        public DiagnosticAreaPanel()
        {
            BackColor = Theme.Background;
            _timer.Tick += (s, e) => { if (Visible) Invalidate(); };
            _timer.Start();
            _scroller = new TouchScroller(this) { Max = () => Math.Max(0, _listContent - _listViewport.Height) };
            _scroller.Scrolled += Invalidate;
        }

        /// <summary>Scrolls the live data / trouble code list to its end (used by the automated UI check).</summary>
        public void ScrollToEnd() => _scroller.SetOffset(int.MaxValue);

        protected override void OnMouseDown(MouseEventArgs e) { if (e.Button == MouseButtons.Left && _listViewport.Contains(e.Location)) _scroller.MouseDown(e.Location); base.OnMouseDown(e); }
        protected override void OnMouseMove(MouseEventArgs e) { _scroller.MouseMove(e.Location); base.OnMouseMove(e); }
        protected override void OnMouseUp(MouseEventArgs e) { _scroller.MouseUp(); base.OnMouseUp(e); }
        protected override void OnMouseWheel(MouseEventArgs e) { _scroller.Wheel(e.Delta, 24); base.OnMouseWheel(e); }

        protected override void Dispose(bool disposing)
        {
            if (disposing) { _timer.Dispose(); _scroller.Dispose(); }
            base.Dispose(disposing);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            Theme.Setup(g);
            var outer = new RectangleF(0.5f, 0.5f, Width - 1, Height - 1);
            Theme.Panel(g, outer, 12, Theme.Surface, Theme.Border);

            // header
            Theme.GlowDot(g, new PointF(28, 24), 12, Theme.WithAlpha(Theme.Red, 45), 1);
            Icons.Draw(g, "target", new RectangleF(19, 15, 18, 18), Theme.Red, 1.6f);
            using (var f = F(11f, true))
                Theme.DrawText(g, Loc.T("panel.currentArea"), f, Theme.Text, new Point(50, 14));

            var m = Module;
            int y = 50;
            int pad = 12;
            int innerW = Width - pad * 2;

            if (m == null)
            {
                using (var f = F(9.5f))
                    Theme.DrawText(g, Loc.T("panel.noModule"), f, Theme.TextMuted, new Rectangle(pad, y, innerW, 80), TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.WordBreak);
                DrawChart(g, pad, Height - 120, innerW, 70);
                DrawFooter(g, pad, Height - 40, innerW);
                return;
            }

            var c = Theme.StatusColor(m.Status);

            // module card
            var card = new RectangleF(pad, y, innerW, 92);
            Theme.FillRounded(g, card, 10, Theme.Card);
            Theme.DrawRounded(g, card, 10, Theme.WithAlpha(c, 120));
            var badge = new RectangleF(card.X + 12, card.Y + 12, 46, 46);
            Theme.Glow(g, badge, 8, c, 4, 90);
            Icons.Badge(g, m.Icon, badge, c, 8);
            using (var f = F(11.5f, true))
                Theme.DrawText(g, m.Name.Length > 22 ? m.Short : m.Name, f, Theme.Text, new Rectangle((int)card.X + 70, (int)card.Y + 12, (int)card.Width - 80, 22), TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            using (var f = F(9f))
                Theme.DrawText(g, m.StatusText, f, c, new Point((int)card.X + 70, (int)card.Y + 38));
            var pct = m.Status == ModuleStatus.Scanning || m.IsDone ? m.Progress : 0;
            using (var f = F(8.5f))
            {
                var t = pct + "%";
                var sz = Theme.Measure(t, f);
                Theme.DrawText(g, t, f, Theme.Text, new Point((int)(card.Right - 12 - sz.Width), (int)card.Y + 62));
            }
            Theme.ProgressBar(g, new RectangleF(card.X + 12, card.Y + 70, card.Width - 76, 5), pct / 100f, m.IsDone ? c : Theme.Red, Theme.SurfaceAlt);
            y += 100;

            // details
            var elapsed = m.Status == ModuleStatus.Scanning && m.StartedAt.HasValue ? DateTime.Now - m.StartedAt.Value : m.Elapsed;
            var rows = new[]
            {
                new KeyValuePair<string, string>(Loc.T("panel.protocol"), string.IsNullOrEmpty(m.ProtocolName) ? "—" : m.ProtocolName),
                new KeyValuePair<string, string>(Loc.T("panel.moduleAddress"), m.Address),
                new KeyValuePair<string, string>(Loc.T("panel.ecuId"), string.IsNullOrEmpty(m.EcuId) ? "—" : m.EcuId),
                new KeyValuePair<string, string>(Loc.T("panel.timeElapsed"), m.Status == ModuleStatus.Pending ? "—" : elapsed.ToString(@"mm\:ss")),
            };
            using (var fl = F(9f))
            using (var fv = F(9f))
            {
                foreach (var r in rows)
                {
                    Theme.DrawText(g, r.Key, fl, Theme.TextMuted, new Point(pad + 6, y));
                    Theme.DrawText(g, r.Value, fv, Theme.Text, new Point(pad + 140, y));
                    y += 24;
                }
            }
            y += 4;

            // live data and trouble codes: every row, in a clipped viewport that scrolls by drag / wheel
            var pars = LiveParam.For(m.Short);
            var dtcs = m.Dtcs;
            var liveRows = new List<KeyValuePair<string, string>>();
            foreach (var p in pars)
            {
                double v;
                if (LiveOverride != null && LiveOverride.TryGetValue(p.Key, out v)) liveRows.Add(new KeyValuePair<string, string>(p.Label, p.Format(v)));
                else if (m.LiveValues.TryGetValue(p.Key, out v)) liveRows.Add(new KeyValuePair<string, string>(p.Label, p.Format(v)));
            }

            // header bar for live data
            var hdr = new RectangleF(pad, y, innerW, 30);
            Theme.FillRounded(g, hdr, 8, Theme.Card);
            Theme.DrawRounded(g, hdr, 8, Theme.BorderSoft);
            using (var f = F(9.5f, true))
                Theme.DrawText(g, Loc.T("panel.liveData") + " (" + m.Short + ")", f, Theme.Text, new Point(pad + 10, y + 7));
            y += 34;

            _listViewport = new Rectangle(pad, y, innerW, Math.Max(24, Height - 128 - y));
            _listContent = Math.Max(1, liveRows.Count) * 24 + (dtcs.Count > 0 ? 20 + dtcs.Count * 22 : 0);
            _scroller.Clamp();
            var state = g.Save();
            g.SetClip(_listViewport);
            int ly = y - _scroller.Offset;
            int right = _listContent > _listViewport.Height ? Width - pad - 12 : Width - pad - 6;
            using (var fl = F(9f))
            using (var fv = F(9f))
            {
                if (liveRows.Count == 0)
                {
                    Theme.DrawText(g, m.Status == ModuleStatus.NoResponse ? Loc.T("status.noresponse") : "—", fl, Theme.TextMuted, new Point(pad + 6, ly));
                    ly += 24;
                }
                foreach (var row in liveRows)
                {
                    Theme.DrawText(g, row.Key, fl, Theme.TextMuted, new Point(pad + 6, ly));
                    var sz = Theme.Measure(row.Value, fv);
                    Theme.DrawText(g, row.Value, fv, Theme.Text, new Point(right - sz.Width, ly));
                    ly += 24;
                }
            }
            if (dtcs.Count > 0)
            {
                using (var f = F(8.5f, true))
                    Theme.DrawText(g, Loc.T("panel.dtcs"), f, Theme.Red, new Point(pad + 6, ly));
                ly += 20;
                using (var fc = F(8.5f, true))
                using (var fd = F(8f))
                {
                    foreach (var d in dtcs)
                    {
                        var col = d.State == DtcState.Pending ? Theme.Orange : Theme.Red;
                        Theme.DrawText(g, d.Code, fc, col, new Point(pad + 6, ly));
                        Theme.DrawText(g, DtcDatabase.Describe(d.Code), fd, Theme.TextMuted, new Rectangle(pad + 62, ly + 1, right - pad - 62, 18), TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
                        ly += 22;
                    }
                }
            }
            g.Restore(state);
            _scroller.DrawIndicator(g, _listViewport, _listContent);

            DrawChart(g, pad, Height - 120, innerW, 70);
            DrawFooter(g, pad, Height - 40, innerW);
        }

        private void DrawChart(Graphics g, int x, int y, int w, int h)
        {
            var r = new RectangleF(x, y, w, h);
            Theme.Panel(g, r, 8, Theme.Card, Theme.BorderSoft);
            var inner = new RectangleF(x + 10, y + 10, w - 20, h - 20);
            using (var pen = new Pen(Theme.WithAlpha(Theme.TextDim, 90), 1f) { DashStyle = DashStyle.Dash })
                g.DrawLine(pen, inner.Left, inner.Top + inner.Height / 2, inner.Right, inner.Top + inner.Height / 2);
            using (var pen = new Pen(Theme.WithAlpha(Theme.TextDim, 120), 1f))
                g.DrawLine(pen, inner.Left, inner.Bottom, inner.Right, inner.Bottom);

            var samples = AppState.Instance.CommSnapshot();
            int n = Math.Min(60, samples.Length);
            if (n < 2) return;
            int max = Math.Max(60, samples.Skip(samples.Length - n).Max());
            var pts = new PointF[n];
            for (int i = 0; i < n; i++)
            {
                int v = samples[samples.Length - n + i];
                pts[i] = new PointF(inner.Left + inner.Width * i / (n - 1), inner.Bottom - inner.Height * Math.Min(1f, v / (float)max) * 0.95f);
            }
            using (var pen = new Pen(Theme.WithAlpha(Theme.Red, 70), 4f) { LineJoin = LineJoin.Round }) g.DrawLines(pen, pts);
            using (var pen = new Pen(Theme.Red, 1.5f) { LineJoin = LineJoin.Round }) g.DrawLines(pen, pts);
            Theme.GlowDot(g, pts[n - 1], 2.5f, Theme.RedBright, 3);
        }

        private void DrawFooter(Graphics g, int x, int y, int w)
        {
            var st = AppState.Instance;
            var samples = st.CommSnapshot();
            bool recent = (DateTime.Now - st.LastCommAt).TotalSeconds < 3 && st.IsConnected;
            string stateText;
            Color stateColor;
            if (!st.IsConnected) { stateText = Loc.T("status.disconnected"); stateColor = Theme.Grey; }
            else if (!recent) { stateText = Loc.T("panel.idle"); stateColor = Theme.Cyan; }
            else
            {
                var last = samples.Skip(Math.Max(0, samples.Length - 20)).ToArray();
                bool unstable = last.Length > 3 && (last.Max() > 1200 || last.Count(v => v > 600) > last.Length / 3);
                stateText = unstable ? Loc.T("panel.unstable") : Loc.T("panel.stable");
                stateColor = unstable ? Theme.Orange : Theme.Green;
            }
            using (var f = F(8.5f))
            {
                Theme.GlowDot(g, new PointF(x + 10, y + 10), 4, recent ? Theme.Green : Theme.Grey, 3);
                Theme.DrawText(g, Loc.T("panel.communication"), f, Theme.TextMuted, new Point(x + 20, y + 2));
                var sz = Theme.Measure(stateText, f);
                Theme.DrawText(g, stateText, f, stateColor, new Point(x + w - 6 - sz.Width, y + 2));
                Theme.GlowDot(g, new PointF(x + w - 16 - sz.Width, y + 10), 4, stateColor, 3);
            }
        }
    }
}
