using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using RedlineDiagnostics.App;
using RedlineDiagnostics.Localization;

namespace RedlineDiagnostics.Controls
{
    /// <summary>Header bar: logo, page title / vehicle line, connection pills, language switch, clock.</summary>
    public sealed class TopBar : BaseControl
    {
        private readonly Timer _clock = new Timer { Interval = 1000 };
        private RectangleF _backRect, _connRect, _adapterRect, _langRect;
        private readonly RectangleF[] _langSeg = new RectangleF[3];
        private int _hoverLang = -1;
        private bool _hoverBack, _hoverConn;

        public string Title { get; set; } = "";
        public string Subtitle { get; set; } = "";
        public bool ShowBack { get; set; }

        public event Action BackClicked;
        public event Action ConnectClicked;

        public TopBar()
        {
            Height = 56;
            BackColor = Theme.TopBar;
            _clock.Tick += (s, e) => Invalidate(new Rectangle(Width - 180, 0, 180, Height));
            _clock.Start();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            int hl = -1;
            for (int i = 0; i < 3; i++) if (_langSeg[i].Contains(e.Location)) hl = i;
            bool hb = ShowBack && _backRect.Contains(e.Location);
            bool hc = _connRect.Contains(e.Location);
            if (hl != _hoverLang || hb != _hoverBack || hc != _hoverConn)
            {
                _hoverLang = hl; _hoverBack = hb; _hoverConn = hc;
                Cursor = (hl >= 0 || hb || hc) ? Cursors.Hand : Cursors.Default;
                Invalidate();
            }
            base.OnMouseMove(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            _hoverLang = -1; _hoverBack = _hoverConn = false;
            Invalidate();
            base.OnMouseLeave(e);
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            for (int i = 0; i < 3; i++)
                if (_langSeg[i].Contains(e.Location)) { Loc.Current = (Language)i; return; }
            if (ShowBack && _backRect.Contains(e.Location)) { BackClicked?.Invoke(); return; }
            if (_connRect.Contains(e.Location)) { ConnectClicked?.Invoke(); return; }
            base.OnMouseClick(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            Theme.Setup(g);
            using (var pen = new Pen(Theme.BorderSoft, 1f))
                g.DrawLine(pen, 0, Height - 1, Width, Height - 1);

            // ---- logo ----
            Icons.Logo(g, new RectangleF(22, 14, 28, 28), Theme.Red);
            using (var f = new Font("Segoe UI", 14f, FontStyle.Bold, GraphicsUnit.Point))
                Theme.DrawText(g, Loc.T("app.brand"), f, Theme.Text, new Point(60, 9));
            using (var f = new Font("Segoe UI", 6.5f, FontStyle.Bold, GraphicsUnit.Point))
                Theme.DrawText(g, "— " + Loc.T("app.brandSub"), f, Theme.Red, new Point(64, 34));
            using (var pen = new Pen(Theme.Border, 1f))
                g.DrawLine(pen, 182, 12, 182, Height - 12);

            // ---- back + title ----
            float x = 196;
            if (ShowBack)
            {
                _backRect = new RectangleF(x - 4, 14, 28, 28);
                if (_hoverBack) Theme.FillRounded(g, _backRect, 6, Theme.WithAlpha(Theme.Cyan, 30));
                Icons.Draw(g, "back", new RectangleF(x, 18, 20, 20), Theme.Text, 2f);
                x += 30;
            }
            else _backRect = RectangleF.Empty;
            using (var f = F(13f, true))
                Theme.DrawText(g, Title, f, Theme.Text, new Point((int)x, 7));
            using (var f = F(8.5f))
                Theme.DrawText(g, Subtitle, f, Theme.TextMuted, new Point((int)x, 32));

            // ---- right side ----
            float rx = Width - 18;

            // status icons
            var st = AppState.Instance;
            Icons.Draw(g, "batteryLevel", new RectangleF(rx - 18, 20, 18, 16), Theme.Text, 1.4f); rx -= 24;
            Icons.Draw(g, "bluetooth", new RectangleF(rx - 14, 19, 14, 18), st.Settings.Adapter == Obd.AdapterType.Elm327Bluetooth || st.Settings.Adapter == Obd.AdapterType.ObdLink ? Theme.Cyan : Theme.Text, 1.4f); rx -= 20;
            Icons.Draw(g, "wifi", new RectangleF(rx - 18, 19, 18, 18), st.Settings.Adapter == Obd.AdapterType.Elm327WiFi ? Theme.Cyan : Theme.Text, 1.4f); rx -= 30;

            // clock
            using (var f = F(11f, true))
            {
                var t = DateTime.Now.ToString("HH:mm");
                var sz = Theme.Measure(t, f);
                rx -= sz.Width;
                Theme.DrawText(g, t, f, Theme.Text, new Point((int)rx, 18));
            }
            rx -= 24;

            // language segmented control
            string[] labels = { "EN", "日本語", "中文" };
            using (var fEn = new Font("Segoe UI", 8.5f, FontStyle.Bold, GraphicsUnit.Point))
            using (var fJa = new Font(Theme.FontFamilyName, 8.5f, FontStyle.Bold, GraphicsUnit.Point))
            {
                float segW = 46, segH = 26;
                float totalW = segW * 3 + 4;
                _langRect = new RectangleF(rx - totalW, (Height - segH) / 2 - 1, totalW, segH + 2);
                Theme.FillRounded(g, _langRect, 6, Theme.SurfaceAlt);
                Theme.DrawRounded(g, _langRect, 6, Theme.Border);
                for (int i = 0; i < 3; i++)
                {
                    _langSeg[i] = new RectangleF(_langRect.X + 2 + i * segW, _langRect.Y + 2, segW - 1, segH - 2);
                    bool active = (int)Loc.Current == i;
                    if (active) Theme.FillRounded(g, _langSeg[i], 5, Theme.Red);
                    else if (_hoverLang == i) Theme.FillRounded(g, _langSeg[i], 5, Theme.WithAlpha(Theme.Cyan, 25));
                    var f = i == 0 ? fEn : fJa;
                    Theme.DrawText(g, labels[i], f, active ? Color.White : Theme.Text, Rectangle.Round(_langSeg[i]), TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                }
                rx = _langRect.X - 20;
            }

            // adapter pill + connection pill in one rounded group
            using (var f = F(9f))
            {
                string connText;
                Color dot;
                switch (st.Connection)
                {
                    case ConnectionState.Connected: connText = Loc.T("status.connected"); dot = Theme.Green; break;
                    case ConnectionState.Connecting: connText = Loc.T("status.connecting"); dot = Theme.Orange; break;
                    case ConnectionState.Error: connText = Loc.T("status.error"); dot = Theme.Red; break;
                    default: connText = Loc.T("status.disconnected"); dot = Theme.Grey; break;
                }
                var adapterText = st.AdapterLabel;
                var szA = Theme.Measure(adapterText, f);
                var szC = Theme.Measure(connText, f);
                float adW = szA.Width + 44, cW = szC.Width + 34;
                float groupW = adW + cW + 6;
                var group = new RectangleF(rx - groupW, 14, groupW, 28);
                Theme.FillRounded(g, group, 8, Theme.SurfaceAlt);
                Theme.DrawRounded(g, group, 8, Theme.Border);
                _connRect = new RectangleF(group.X, group.Y, cW, group.Height);
                _adapterRect = new RectangleF(group.X + cW + 2, group.Y + 2, adW - 2, group.Height - 4);
                if (_hoverConn) Theme.FillRounded(g, new RectangleF(_connRect.X + 2, _connRect.Y + 2, _connRect.Width - 2, _connRect.Height - 4), 6, Theme.WithAlpha(Theme.Cyan, 25));
                Theme.GlowDot(g, new PointF(_connRect.X + 14, _connRect.Y + 14), 4, dot, 3);
                Theme.DrawText(g, connText, f, st.Connection == ConnectionState.Connected ? Theme.Green : Theme.Text, new Point((int)_connRect.X + 24, 20));
                using (var pen = new Pen(Theme.Border, 1f))
                    g.DrawLine(pen, _adapterRect.X, group.Y + 6, _adapterRect.X, group.Bottom - 6);
                Icons.Draw(g, "plug", new RectangleF(_adapterRect.X + 10, 20, 16, 16), Theme.TextMuted, 1.5f);
                Theme.DrawText(g, adapterText, f, Theme.Text, new Point((int)_adapterRect.X + 32, 20));
            }
        }
    }
}
