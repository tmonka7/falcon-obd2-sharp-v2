using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using RedlineDiagnostics.App;
using RedlineDiagnostics.Controls;
using RedlineDiagnostics.Diagnostics;
using RedlineDiagnostics.Localization;

namespace RedlineDiagnostics.Forms.Pages
{
    /// <summary>Grid of live ECM parameters with sparklines.</summary>
    public sealed class LiveDataPage : BasePage
    {
        private readonly NeonButton _toggle;
        private readonly ThemeLabel _title, _sub, _rate;
        private readonly Dictionary<string, double> _latest = new Dictionary<string, double>();
        private readonly Dictionary<string, List<double>> _hist = new Dictionary<string, List<double>>();
        private LiveDataMonitor _monitor;
        private readonly Timer _timer = new Timer { Interval = 250 };
        private readonly Rectangle _grid = new Rectangle(10, 76, 1186, 626);
        private const int Cols = 4, Gap = 10, TileHeight = 202;
        private readonly TouchScroller _scroller;

        private int ContentHeight
        {
            get
            {
                int rows = (LiveParam.Ecm.Length + Cols - 1) / Cols;
                return rows * TileHeight + (rows - 1) * Gap;
            }
        }

        public override string TitleKey => "nav.livedata";

        public LiveDataPage()
        {
            _title = new ThemeLabel { TextKey = "live.title", FontSize = 18f, Bold = true, Bounds = new Rectangle(14, 10, 500, 36) };
            _sub = new ThemeLabel { TextKey = "live.subtitle", FontSize = 10f, Color = Theme.TextMuted, Bounds = new Rectangle(15, 44, 600, 22) };
            _rate = new ThemeLabel { FontSize = 9f, Color = Theme.TextMuted, Bounds = new Rectangle(760, 24, 240, 24), Align = ContentAlignment.MiddleRight };
            _toggle = new NeonButton { TextKey = "live.start", Icon = "livedata", Bounds = new Rectangle(1016, 20, 180, 38), Accent = Theme.Green };
            _toggle.Click += (s, e) => Toggle();
            Controls.AddRange(new Control[] { _title, _sub, _rate, _toggle });
            _timer.Tick += (s, e) => { if (Visible) { UpdateRate(); Invalidate(_grid); } };
            AppState.Instance.ConnectionChanged += () => { if (!AppState.Instance.IsConnected) StopMonitor(); UpdateButton(); };
            AppState.Instance.ScanStateChanged += () => { if (AppState.Instance.IsScanning) StopMonitor(); UpdateButton(); };
            _scroller = new TouchScroller(this) { Max = () => Math.Max(0, ContentHeight - _grid.Height) };
            _scroller.Scrolled += () => Invalidate(_grid);
            ApplyLocalization();
        }

        protected override void OnMouseDown(MouseEventArgs e) { if (e.Button == MouseButtons.Left && _grid.Contains(e.Location)) _scroller.MouseDown(e.Location); base.OnMouseDown(e); }
        protected override void OnMouseMove(MouseEventArgs e) { _scroller.MouseMove(e.Location); base.OnMouseMove(e); }
        protected override void OnMouseUp(MouseEventArgs e) { _scroller.MouseUp(); base.OnMouseUp(e); }
        protected override void OnMouseWheel(MouseEventArgs e) { _scroller.Wheel(e.Delta, TileHeight / 2); base.OnMouseWheel(e); }

        public override void OnShown()
        {
            UpdateButton();
            _timer.Start();
        }

        public override void OnHidden()
        {
            _timer.Stop();
            StopMonitor();
        }

        public void StartMonitoring()
        {
            if (_monitor == null) Toggle();
        }

        private void Toggle()
        {
            var st = AppState.Instance;
            if (_monitor != null) { StopMonitor(); return; }
            if (!st.IsConnected) { Info(Loc.T("live.notConnected")); return; }
            if (st.IsScanning) return;
            _monitor = st.StartMonitor(LiveParam.Ecm, null);
            if (_monitor == null) return;
            var mon = _monitor;
            mon.Sample += sample =>
            {
                if (_monitor != mon) return;
                foreach (var kv in sample)
                {
                    _latest[kv.Key] = kv.Value;
                    List<double> l;
                    if (!_hist.TryGetValue(kv.Key, out l)) _hist[kv.Key] = l = new List<double>();
                    l.Add(kv.Value);
                    if (l.Count > 80) l.RemoveAt(0);
                }
            };
            UpdateButton();
        }

        private void StopMonitor()
        {
            if (_monitor != null)
            {
                AppState.Instance.StopMonitor();
                _monitor = null;
            }
            UpdateButton();
        }

        private void UpdateButton()
        {
            bool running = _monitor != null && _monitor.IsRunning;
            _toggle.TextKey = running ? "live.stop" : "live.start";
            _toggle.Accent = running ? Theme.Grey : Theme.Green;
            _toggle.ApplyLocalization();
        }

        private void UpdateRate()
        {
            var m = _monitor;
            _rate.Text = m != null && m.IsRunning ? Loc.T("live.rate") + ": " + (m.SamplesPerSecondX10 / 10.0).ToString("0.0") + " Hz" : "";
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            Theme.Setup(g);
            var pars = LiveParam.Ecm;
            int w = (_grid.Width - Gap * (Cols - 1)) / Cols;
            // All parameters in fixed-height tiles; the grid scrolls (drag / wheel) when they do not fit.
            var state = g.Save();
            g.SetClip(_grid);
            for (int i = 0; i < pars.Length; i++)
            {
                var p = pars[i];
                int cx = _grid.X + (i % Cols) * (w + Gap);
                int cy = _grid.Y + (i / Cols) * (TileHeight + Gap) - _scroller.Offset;
                if (cy > _grid.Bottom || cy + TileHeight < _grid.Y) continue;
                DrawTile(g, new Rectangle(cx, cy, w, TileHeight), p);
            }
            g.Restore(state);
            _scroller.DrawIndicator(g, new Rectangle(_grid.X, _grid.Y, _grid.Width + 8, _grid.Height), ContentHeight);
        }

        private void DrawTile(Graphics g, Rectangle r, LiveParam p)
        {
            Theme.Panel(g, new RectangleF(r.X + 0.5f, r.Y + 0.5f, r.Width - 1, r.Height - 1), 12, Theme.Surface, Theme.Border);
            double v;
            bool has = _latest.TryGetValue(p.Key, out v);
            var accent = has ? Theme.Cyan : Theme.Grey;
            using (var f = F(9.5f))
                Theme.DrawText(g, p.Label, f, Theme.TextMuted, new Rectangle(r.X + 16, r.Y + 12, r.Width - 32, 20), TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
            using (var f = F(24f, true))
                Theme.DrawText(g, has ? v.ToString("F" + p.Decimals) : "—", f, Theme.Text, new Point(r.X + 16, r.Y + 34));
            using (var f = F(10f))
            {
                var num = has ? v.ToString("F" + p.Decimals) : "—";
                int nw;
                using (var fb = F(24f, true)) nw = Theme.Measure(num, fb).Width;
                Theme.DrawText(g, p.Unit, f, accent, new Point(r.X + 20 + nw, r.Y + 52));
            }
            // range bar
            float frac = has ? (float)((v - p.Min) / (p.Max - p.Min)) : 0;
            Theme.ProgressBar(g, new RectangleF(r.X + 16, r.Y + 82, r.Width - 32, 5), Math.Max(0, Math.Min(1, frac)), accent, Theme.SurfaceAlt);

            // sparkline
            var chart = new RectangleF(r.X + 16, r.Y + 98, r.Width - 32, r.Height - 112);
            using (var pen = new Pen(Theme.WithAlpha(Theme.TextDim, 90), 1f) { DashStyle = System.Drawing.Drawing2D.DashStyle.Dash })
                g.DrawLine(pen, chart.Left, chart.Top + chart.Height / 2, chart.Right, chart.Top + chart.Height / 2);
            List<double> hist;
            if (!_hist.TryGetValue(p.Key, out hist) || hist.Count < 2) return;
            double min = hist.Min(), max = hist.Max();
            if (max - min < 1e-6) { min -= 1; max += 1; }
            var pts = new PointF[hist.Count];
            for (int i = 0; i < hist.Count; i++)
                pts[i] = new PointF(chart.Left + chart.Width * i / (hist.Count - 1), chart.Bottom - (float)((hist[i] - min) / (max - min)) * chart.Height);
            using (var pen = new Pen(Theme.WithAlpha(accent, 60), 4f) { LineJoin = System.Drawing.Drawing2D.LineJoin.Round }) g.DrawLines(pen, pts);
            using (var pen = new Pen(accent, 1.5f) { LineJoin = System.Drawing.Drawing2D.LineJoin.Round }) g.DrawLines(pen, pts);
            Theme.GlowDot(g, pts[pts.Length - 1], 2.5f, accent, 3);
        }
    }
}
