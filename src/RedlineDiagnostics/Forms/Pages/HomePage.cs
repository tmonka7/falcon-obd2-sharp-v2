using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using RedlineDiagnostics.App;
using RedlineDiagnostics.Controls;
using RedlineDiagnostics.Database;
using RedlineDiagnostics.Diagnostics;
using RedlineDiagnostics.Localization;
using RedlineDiagnostics.Obd;

namespace RedlineDiagnostics.Forms.Pages
{
    /// <summary>
    /// Dashboard: welcome / active vehicle, adapter connection, system health, quick actions, recent scans,
    /// DTC lookup and a promo panel. Every card is custom painted; clickable areas are registered while painting.
    /// </summary>
    public sealed class HomePage : BasePage
    {
        private readonly HeroCard _hero;
        private readonly ConnectionCard _conn;
        private readonly StatusCard _status;
        private readonly QuickActionsCard _actions;
        private readonly RecentCard _recent;
        private readonly DtcCard _dtc;
        private readonly PromoCard _promo;

        public override string TitleKey => "nav.home";

        public HomePage()
        {
            _hero = new HeroCard(this) { Bounds = new Rectangle(10, 10, 486, 252) };
            _conn = new ConnectionCard(this) { Bounds = new Rectangle(506, 10, 340, 252) };
            _status = new StatusCard(this) { Bounds = new Rectangle(856, 10, 340, 252) };
            _actions = new QuickActionsCard(this) { Bounds = new Rectangle(10, 272, 1186, 128) };
            _recent = new RecentCard(this) { Bounds = new Rectangle(10, 410, 514, 292) };
            _dtc = new DtcCard(this) { Bounds = new Rectangle(534, 410, 330, 292) };
            _promo = new PromoCard(this) { Bounds = new Rectangle(874, 410, 322, 292) };
            Controls.AddRange(new Control[] { _hero, _conn, _status, _actions, _recent, _dtc, _promo });

            var st = AppState.Instance;
            st.ConnectionChanged += RefreshAll;
            st.VehicleChanged += RefreshAll;
            st.ScanStateChanged += RefreshAll;
            st.HistoryChanged += RefreshAll;
            CarImages.Changed += RefreshAll;
            ApplyLocalization();
        }

        protected override void OnLocalize() => RefreshAll();

        public override void OnShown() => RefreshAll();

        private void RefreshAll()
        {
            foreach (Control c in Controls) c.Invalidate();
        }

        internal void Go(int page) => Navigate(page);

        // ------------------------------------------------------------------ actions

        internal void ToggleConnect()
        {
            var st = AppState.Instance;
            if (st.IsConnected) st.Disconnect();
            else st.ConnectAsync();
        }

        internal void ReadDtcs()
        {
            var st = AppState.Instance;
            if (!st.IsConnected) { Info(Loc.T("scan.notConnected")); return; }
            if (st.IsScanning || _actions.Busy) return;
            st.StopMonitor();
            _actions.Busy = true;
            var ui = st.Ui;
            new Thread(() =>
            {
                string msg;
                try
                {
                    var list = st.ReadEcmDtcs();
                    if (list.Count == 0) msg = Loc.T("panel.noDtcs");
                    else msg = Loc.T("home.dtcsFound", list.Count) + "\n\n" + string.Join("\n", list.Select(d => d.Code + "  [" + Loc.T("dtc." + d.State.ToString().ToLowerInvariant()) + "]  " + DtcDatabase.Describe(d.Code)));
                }
                catch (Exception ex) { msg = ex.Message; }
                ui.Post(_ => { _actions.Busy = false; Info(msg); }, null);
            }) { IsBackground = true }.Start();
        }

        internal void ClearDtcs()
        {
            var st = AppState.Instance;
            if (!st.IsConnected) { Info(Loc.T("scan.notConnected")); return; }
            if (st.IsScanning || _actions.Busy) return;
            if (Confirm(Loc.T("home.clearConfirm")) != DialogResult.Yes) return;
            st.StopMonitor();
            _actions.Busy = true;
            var ui = st.Ui;
            new Thread(() =>
            {
                string msg;
                try { msg = st.ClearEcmDtcs() ? Loc.T("home.cleared") : Loc.T("status.noresponse"); }
                catch (Exception ex) { msg = ex.Message; }
                ui.Post(_ => { _actions.Busy = false; Info(msg); }, null);
            }) { IsBackground = true }.Start();
        }

        internal void ShowServiceMenu(Control owner, Point at)
        {
            var st = AppState.Instance;
            var menu = new ContextMenuStrip { Renderer = new DarkMenuRenderer(), ShowImageMargin = false, Font = Theme.Font(10f), BackColor = Theme.SurfaceAlt, ForeColor = Theme.Text };
            Action<string, Action> add = (key, act) =>
            {
                var item = new ToolStripMenuItem(Loc.T(key)) { ForeColor = Theme.Text, Padding = new Padding(6, 6, 6, 6) };
                item.Click += (s, e) => act();
                menu.Items.Add(item);
            };
            add("svc.mil", ClearDtcs);
            add("svc.readiness", () => Go(3));
            add("svc.battery", () => Info(Loc.T("svc.batteryValue", st.BatteryVoltage.HasValue ? st.BatteryVoltage.Value.ToString("0.0") + " V" : "—")));
            add("svc.resetAdapter", () => st.ConnectAsync());
            menu.Closed += (s, e) => owner.BeginInvoke((Action)(() => menu.Dispose()));
            menu.Show(owner, at);
        }

        internal static void ShowDtc(DtcInfo d)
        {
            Info(d.Code + "\n\n" + d.Description + "\n\n" + Loc.T("common.description") + ": " + d.SystemName);
        }

        // ------------------------------------------------------------------ data helpers

        /// <summary>0–100 score from the last scan: faults cost 8 points, warnings 3, silent modules 1.</summary>
        internal static int? HealthScore(ScanResult s)
        {
            if (s == null) return null;
            return Math.Max(0, Math.Min(100, 100 - s.Faults * 8 - s.Warnings * 3 - s.NoResponse));
        }

        internal static string FormatDate(DateTime t)
        {
            return Loc.Current == Language.EN
                ? t.ToString("MMM d, yyyy  HH:mm", CultureInfo.InvariantCulture)
                : t.ToString("yyyy/MM/dd  HH:mm", CultureInfo.InvariantCulture);
        }

        internal static VehicleProfile FindVehicle(string vin)
        {
            if (string.IsNullOrEmpty(vin)) return null;
            return AppState.Instance.Vehicles.FirstOrDefault(v => string.Equals(v.Vin, vin, StringComparison.OrdinalIgnoreCase));
        }

        // ================================================================== base card

        /// <summary>Custom-painted card with clickable hot spots registered during paint.</summary>
        internal abstract class DashCard : BaseControl
        {
            private sealed class Hot
            {
                public string Id;
                public RectangleF Rect;
                public Action Click;
            }

            private readonly List<Hot> _hots = new List<Hot>();
            protected readonly HomePage Page;
            protected string HoverId;
            protected Color BorderColor = Theme.Border;
            protected Color FillColor = Theme.Surface;

            protected DashCard(HomePage page)
            {
                Page = page;
                BackColor = Theme.Background;
            }

            // ---- optional drag-scroll region (rows inside Viewport move by Scroller.Offset) ----
            protected TouchScroller Scroller;
            protected Rectangle Viewport;
            protected int ContentHeight;

            protected void EnableScrolling()
            {
                Scroller = new TouchScroller(this) { Max = () => Math.Max(0, ContentHeight - Viewport.Height) };
                Scroller.Scrolled += Invalidate;
            }

            protected int ScrollOffset => Scroller?.Offset ?? 0;

            /// <summary>Clips drawing to the scroll viewport; pair with <see cref="EndViewport"/>.</summary>
            protected GraphicsState BeginViewport(Graphics g, Rectangle viewport, int contentHeight)
            {
                Viewport = viewport;
                ContentHeight = contentHeight;
                Scroller?.Clamp();
                var state = g.Save();
                g.SetClip(viewport);
                return state;
            }

            protected void EndViewport(Graphics g, GraphicsState state)
            {
                g.Restore(state);
                Scroller?.DrawIndicator(g, Viewport, ContentHeight);
            }

            /// <summary>True when the scroll content is taller than its viewport (rows leave room for the indicator).</summary>
            protected bool Overflows(int contentHeight, int viewportHeight) => contentHeight > viewportHeight;

            /// <summary>Registers a hot spot for a row inside the viewport, clipped so hidden parts cannot be tapped.</summary>
            protected void AddRowHot(string id, RectangleF r, Action click)
            {
                var c = RectangleF.Intersect(r, Viewport);
                if (c.Width > 0 && c.Height > 0) AddHot(id, c, click);
            }

            protected override void OnMouseDown(MouseEventArgs e)
            {
                if (Scroller != null && e.Button == MouseButtons.Left && Viewport.Contains(e.Location)) Scroller.MouseDown(e.Location);
                base.OnMouseDown(e);
            }

            protected override void OnMouseUp(MouseEventArgs e)
            {
                Scroller?.MouseUp();
                if (Touch.IsTouchMessage() && HoverId != null) { HoverId = null; Invalidate(); }
                base.OnMouseUp(e);
            }

            protected override void OnMouseWheel(MouseEventArgs e)
            {
                Scroller?.Wheel(e.Delta, 48);
                base.OnMouseWheel(e);
            }

            protected override void Dispose(bool disposing)
            {
                if (disposing) Scroller?.Dispose();
                base.Dispose(disposing);
            }

            protected override void OnMouseMove(MouseEventArgs e)
            {
                if (Scroller != null && Scroller.MouseMove(e.Location))
                {
                    if (HoverId != null) { HoverId = null; Invalidate(); }
                    base.OnMouseMove(e);
                    return;
                }
                var h = _hots.LastOrDefault(x => x.Rect.Contains(e.Location));
                var id = h?.Id;
                if (id != HoverId)
                {
                    HoverId = id;
                    Cursor = id != null ? Cursors.Hand : Cursors.Default;
                    Invalidate();
                }
                base.OnMouseMove(e);
            }

            protected override void OnMouseLeave(EventArgs e)
            {
                if (HoverId != null) { HoverId = null; Invalidate(); }
                base.OnMouseLeave(e);
            }

            protected override void OnMouseClick(MouseEventArgs e)
            {
                if (e.Button == MouseButtons.Left && !(Scroller != null && Scroller.SuppressClick))
                {
                    var h = _hots.LastOrDefault(x => x.Rect.Contains(e.Location));
                    h?.Click?.Invoke();
                }
                base.OnMouseClick(e);
            }

            protected void AddHot(string id, RectangleF r, Action click) => _hots.Add(new Hot { Id = id, Rect = r, Click = click });
            protected bool IsHover(string id) => HoverId == id;

            protected override void OnPaint(PaintEventArgs e)
            {
                var g = e.Graphics;
                Theme.Setup(g);
                _hots.Clear();
                var r = new RectangleF(0.5f, 0.5f, Width - 1, Height - 1);
                PaintBackground(g, r);
                PaintCard(g);
            }

            protected virtual void PaintBackground(Graphics g, RectangleF r)
            {
                using (var path = Theme.RoundedRect(r, 12))
                using (var b = new LinearGradientBrush(new RectangleF(0, -1, Width, Height + 2), Theme.Lerp(FillColor, Color.White, 0.02f), Theme.Lerp(FillColor, Color.Black, 0.18f), LinearGradientMode.Vertical))
                    g.FillPath(b, path);
                Theme.DrawRounded(g, new RectangleF(1, 1, Width - 2, Height - 2), 12, BorderColor);
            }

            protected abstract void PaintCard(Graphics g);

            // ---- drawing helpers ----

            protected void Text(Graphics g, string text, float size, bool bold, Color color, Rectangle rect, TextFormatFlags flags = TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis)
            {
                using (var f = F(size, bold)) Theme.DrawText(g, text, f, color, rect, flags);
            }

            protected Size Measure(string text, float size, bool bold)
            {
                using (var f = F(size, bold)) return TextRenderer.MeasureText(text, f, new Size(2000, 200), TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);
            }

            /// <summary>Largest font size from <paramref name="size"/> down to <paramref name="min"/> at which the text fits.</summary>
            protected float Fit(string text, float size, float min, bool bold, int width)
            {
                for (float s = size; s > min; s -= 0.5f)
                    if (Measure(text, s, bold).Width <= width) return s;
                return min;
            }

            /// <summary>Header row: tinted icon badge and title.</summary>
            protected void Header(Graphics g, string icon, string title, Color accent, bool badge = true, int titleRight = 0)
            {
                if (badge)
                {
                    var br = new RectangleF(16, 14, 28, 28);
                    Theme.FillRounded(g, br, 7, Theme.WithAlpha(accent, 38));
                    Theme.DrawRounded(g, br, 7, Theme.WithAlpha(accent, 150));
                    Icons.Draw(g, icon, RectangleF.Inflate(br, -6, -6), accent, 1.7f);
                }
                else Icons.Draw(g, icon, new RectangleF(16, 14, 26, 28), accent, 1.8f);
                int right = titleRight > 0 ? titleRight : Width - 16;
                Text(g, title, Fit(title, 11.5f, 9.5f, true, right - 54), true, Theme.Text, new Rectangle(54, 12, right - 54, 32));
            }

            /// <summary>Rounded dark "pill" button with a chevron.</summary>
            protected void Pill(Graphics g, string id, RectangleF r, string text, Action click, Color? accent = null, bool chevron = true)
            {
                bool hov = IsHover(id);
                var a = accent ?? Theme.TextMuted;
                if (accent.HasValue)
                {
                    using (var path = Theme.RoundedRect(r, r.Height / 2))
                    using (var b = new LinearGradientBrush(RectangleF.Inflate(r, 1, 1), Theme.Lerp(a, Color.White, hov ? 0.2f : 0.08f), Theme.Lerp(a, Color.Black, 0.2f), LinearGradientMode.Vertical))
                        g.FillPath(b, path);
                    if (hov) Theme.Glow(g, r, r.Height / 2, a, 4, 90);
                }
                else
                {
                    Theme.FillRounded(g, r, r.Height / 2, hov ? Theme.WithAlpha(Color.White, 22) : Theme.WithAlpha(Color.Black, 110));
                    Theme.DrawRounded(g, r, r.Height / 2, hov ? Theme.WithAlpha(Color.White, 110) : Theme.WithAlpha(Color.White, 55));
                }
                var tc = accent.HasValue ? Color.White : Theme.Text;
                var tr = new Rectangle((int)r.X + 14, (int)r.Y, (int)r.Width - (chevron ? 40 : 28), (int)r.Height);
                Text(g, text, 9f, false, tc, tr, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
                if (chevron) Icons.Draw(g, "forward", new RectangleF(r.Right - 24, r.Y + r.Height / 2 - 6, 12, 12), tc, 1.6f);
                AddHot(id, r, click);
            }

            protected static Color Alpha(Color c, int a) => Theme.WithAlpha(c, a);
        }

        // ================================================================== welcome / vehicle

        internal sealed class HeroCard : DashCard
        {
            public HeroCard(HomePage page) : base(page) { }

            protected override void PaintBackground(Graphics g, RectangleF r)
            {
                using (var path = Theme.RoundedRect(r, 12))
                {
                    var oldClip = g.Clip;
                    g.SetClip(path);
                    using (var b = new LinearGradientBrush(r, Color.FromArgb(34, 8, 16), Color.FromArgb(12, 13, 22), LinearGradientMode.Horizontal))
                        g.FillRectangle(b, r);
                    // red studio floor glow
                    using (var gp = new GraphicsPath())
                    {
                        var e = new RectangleF(-80, Height * 0.45f, Width * 0.95f, Height * 0.95f);
                        gp.AddEllipse(e);
                        using (var pb = new PathGradientBrush(gp) { CenterColor = Color.FromArgb(120, 229, 23, 60), SurroundColors = new[] { Color.FromArgb(0, 229, 23, 60) } })
                            g.FillPath(pb, gp);
                    }
                    // neon light strips on the studio ceiling
                    DrawStrip(g, new PointF(Width * 0.08f, 6), new PointF(Width * 0.42f, 34));
                    DrawStrip(g, new PointF(Width * 0.3f, -4), new PointF(Width * 0.58f, 22));
                    DrawStrip(g, new PointF(-10, Height * 0.9f), new PointF(Width * 0.5f, Height + 6));
                    g.Clip = oldClip;
                }
                Theme.DrawRounded(g, new RectangleF(1, 1, Width - 2, Height - 2), 12, Alpha(Theme.Red, 110));
            }

            private static void DrawStrip(Graphics g, PointF a, PointF b)
            {
                float[] w = { 9f, 5f, 2.2f };
                int[] al = { 18, 40, 150 };
                for (int i = 0; i < w.Length; i++)
                    using (var pen = new Pen(Color.FromArgb(al[i], 255, 42, 77), w[i]) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                        g.DrawLine(pen, a, b);
            }

            protected override void PaintCard(Graphics g)
            {
                var st = AppState.Instance;
                var v = st.ActiveVehicle;

                Text(g, Loc.T("home.welcome"), 22f, true, Color.White, new Rectangle(22, 10, 300, 42));
                var sub = st.IsConnected ? Loc.T("home.ready") : Loc.T("home.notReady");
                var subRect = new Rectangle(24, 50, 290, 22);
                int sp = sub.IndexOf(' ');
                if (Loc.Current == Language.EN && sp > 0 && st.IsConnected)
                {
                    var first = sub.Substring(0, sp);
                    // Width of "first " = whole line minus the remainder (GDI pads short strings with overhang).
                    var w = Measure(sub, 10.5f, false).Width - Measure(sub.Substring(sp + 1), 10.5f, false).Width;
                    Text(g, first, 10.5f, false, Theme.RedBright, subRect);
                    Text(g, sub.Substring(sp + 1), 10.5f, false, Theme.Text, new Rectangle(subRect.X + w, subRect.Y, subRect.Width - w, subRect.Height));
                }
                else Text(g, sub, 10.5f, false, Theme.Text, subRect);

                var img = CarImages.Get(CarShot.Hero, new Size(350, 200));
                if (img != null) g.DrawImage(img, -16, 62, 350, 200);

                // vehicle info panel
                var p = new RectangleF(Width - 192, 44, 178, 196);
                using (var path = Theme.RoundedRect(p, 10))
                using (var b = new SolidBrush(Color.FromArgb(185, 10, 12, 20)))
                    g.FillPath(b, path);
                Theme.DrawRounded(g, p, 10, Alpha(Color.White, 34));

                var make = v?.Make ?? st.VinInfo?.Manufacturer ?? "";
                DrawEmblem(g, new RectangleF(p.X + 14, p.Y + 14, 40, 30), make);
                var name = v != null ? ((v.Make + " " + v.Model).Trim()) : st.VehicleTitle;
                if (string.IsNullOrWhiteSpace(name)) name = Loc.T("home.noVehicle");
                Text(g, name, 14f, true, Color.White, new Rectangle((int)p.X + 14, (int)p.Y + 52, (int)p.Width - 22, 28));
                var parts = new List<string>();
                if (v != null && v.Year > 0) parts.Add(v.Year.ToString());
                if (v != null && !string.IsNullOrWhiteSpace(v.Engine)) parts.Add(v.Engine);
                Text(g, parts.Count > 0 ? string.Join("  •  ", parts) : "—", 9.5f, false, Theme.Text, new Rectangle((int)p.X + 14, (int)p.Y + 82, (int)p.Width - 22, 20));
                var vin = st.CurrentVin;
                Text(g, "VIN: " + (string.IsNullOrEmpty(vin) ? "—" : vin), 8.5f, false, Theme.TextMuted, new Rectangle((int)p.X + 14, (int)p.Y + 104, (int)p.Width - 18, 20));
                Pill(g, "change", new RectangleF(p.X + 14, p.Bottom - 48, p.Width - 28, 32), Loc.T("home.changeVehicle"), () => Page.Go(6));
            }

            /// <summary>Generic make emblem: an oval badge with the make's initial (no manufacturer logos).</summary>
            private void DrawEmblem(Graphics g, RectangleF r, string make)
            {
                using (var pen = new Pen(Color.FromArgb(225, 230, 238), 2f))
                    g.DrawEllipse(pen, r);
                using (var pen = new Pen(Color.FromArgb(90, 225, 230, 238), 1f))
                    g.DrawEllipse(pen, RectangleF.Inflate(r, -4, -4));
                var letter = string.IsNullOrEmpty(make) ? "?" : make.Substring(0, 1).ToUpperInvariant();
                Text(g, letter, 11f, true, Color.White, Rectangle.Round(r), TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }
        }

        // ================================================================== connection & adapter

        internal sealed class ConnectionCard : DashCard
        {
            private static readonly ObdProtocol[] Protocols = { ObdProtocol.Can11Bit500K, ObdProtocol.KWP2000Fast, ObdProtocol.ISO9141, ObdProtocol.J1850PWM };
            private static readonly string[] ProtocolLabels = { "CAN", "KWP2000", "ISO9141", "J1850" };
            private static readonly string[] ProtocolIcons = { "gateway", "target2", "chip", "plug" };
            private static readonly Color[] ProtocolColors = { Theme.Green, Theme.Blue, Theme.Purple, Theme.Orange };

            public ConnectionCard(HomePage page) : base(page) { }

            protected override void PaintCard(Graphics g)
            {
                var st = AppState.Instance;
                string connText; Color connColor;
                switch (st.Connection)
                {
                    case ConnectionState.Connected: connText = Loc.T("status.connected"); connColor = Theme.Green; break;
                    case ConnectionState.Connecting: connText = Loc.T("status.connecting"); connColor = Theme.Orange; break;
                    case ConnectionState.Error: connText = Loc.T("status.error"); connColor = Theme.Red; break;
                    default: connText = Loc.T("status.disconnected"); connColor = Theme.Grey; break;
                }
                var cw = Measure(connText, 8.5f, false).Width + 30;
                var chip = new RectangleF(Width - 16 - cw, 16, cw, 24);
                Header(g, "plug", Loc.T("home.connAdapter"), Theme.Red, true, (int)chip.X - 6);
                Theme.FillRounded(g, chip, 7, Alpha(connColor, IsHover("conn") ? 60 : 34));
                Theme.DrawRounded(g, chip, 7, Alpha(connColor, 120));
                Theme.FillRounded(g, new RectangleF(chip.X + 10, chip.Y + 8, 8, 8), 2, connColor);
                Text(g, connText, 8.5f, false, connColor, new Rectangle((int)chip.X + 24, (int)chip.Y, (int)chip.Width - 26, (int)chip.Height));
                AddHot("conn", chip, Page.ToggleConnect);

                using (var pen = new Pen(Theme.BorderSoft, 1f)) g.DrawLine(pen, 12, 54, Width - 12, 54);

                // device illustration + details
                var type = st.Settings.Adapter;
                string brand = type == AdapterType.ObdLink ? "OBDLink" : type == AdapterType.Simulator ? "SIM" : "ELM327";
                DrawDongle(g, new RectangleF(14, 66, 124, 96), brand);

                int x = 148;
                string title = type == AdapterType.ObdLink ? "OBDLink MX+" : ObdProtocolInfo.AdapterName(type);
                Text(g, title, 12f, true, Color.White, new Rectangle(x, 66, Width - x - 12, 26));
                string linkIcon, linkKey;
                switch (type)
                {
                    case AdapterType.Elm327Usb: linkIcon = "plug"; linkKey = "home.link.usb"; break;
                    case AdapterType.Elm327WiFi: linkIcon = "wifi"; linkKey = "home.link.wifi"; break;
                    case AdapterType.Simulator: linkIcon = "cube"; linkKey = "home.link.sim"; break;
                    default: linkIcon = "bluetooth"; linkKey = "home.link.bt"; break;
                }
                Icons.Draw(g, linkIcon, new RectangleF(x, 97, 16, 16), Theme.Blue, 1.6f);
                var link = Loc.T(linkKey);
                if (type == AdapterType.Elm327Usb || type == AdapterType.Elm327Bluetooth || type == AdapterType.ObdLink)
                    if (!string.IsNullOrEmpty(st.Settings.SerialPort)) link += "  ·  " + st.Settings.SerialPort;
                if (type == AdapterType.Elm327WiFi) link += "  ·  " + st.Settings.Host;
                Text(g, link, 9.5f, false, Theme.Text, new Rectangle(x + 22, 94, Width - x - 34, 22));
                var fw = st.IsConnected && st.Adapter != null && !string.IsNullOrEmpty(st.Adapter.Version) ? st.Adapter.Version : "—";
                Text(g, "FW: " + fw, 9.5f, false, Theme.TextMuted, new Rectangle(x, 118, Width - x - 12, 20));

                if (st.IsConnected)
                    Pill(g, "details", new RectangleF(x, 142, Width - x - 16, 30), Loc.T("home.deviceDetails"), () => Page.Go(7));
                else
                    Pill(g, "details", new RectangleF(x, 142, Width - x - 16, 30), Loc.T("home.connect"), Page.ToggleConnect, Theme.Red);

                // protocol strip
                using (var pen = new Pen(Theme.BorderSoft, 1f)) g.DrawLine(pen, 12, 184, Width - 12, 184);
                var active = st.IsConnected && st.Adapter != null ? st.Adapter.Protocol : ObdProtocol.Unknown;
                float colW = (Width - 24 - 58) / 4f;
                for (int i = 0; i < 4; i++)
                {
                    bool on = IsFamily(active, Protocols[i]);
                    float cx = 12 + colW * i + colW / 2;
                    var c = ProtocolColors[i];
                    var badge = new RectangleF(cx - 13, 194, 26, 26);
                    if (on) Theme.Glow(g, badge, 6, c, 5, 140);
                    Theme.FillRounded(g, badge, 6, Alpha(c, on ? 70 : 30));
                    Theme.DrawRounded(g, badge, 6, Alpha(c, on ? 230 : 110));
                    Icons.Draw(g, ProtocolIcons[i], RectangleF.Inflate(badge, -6, -6), on ? Theme.Lerp(c, Color.White, 0.3f) : c, 1.5f);
                    Text(g, ProtocolLabels[i], 8f, on, on ? Color.White : Theme.TextMuted, new Rectangle((int)(cx - colW / 2), 224, (int)colW, 18), TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                }
                var more = new RectangleF(Width - 70, 198, 58, 26);
                if (IsHover("more")) Theme.FillRounded(g, more, 6, Alpha(Color.White, 18));
                Text(g, Loc.T("home.more"), 9f, false, Theme.Text, new Rectangle((int)more.X + 4, (int)more.Y, 38, (int)more.Height), TextFormatFlags.Right | TextFormatFlags.VerticalCenter);
                Icons.Draw(g, "forward", new RectangleF(more.Right - 14, more.Y + 7, 12, 12), Theme.Text, 1.6f);
                AddHot("more", more, () => Page.Go(7));
            }

            private static bool IsFamily(ObdProtocol active, ObdProtocol family)
            {
                if (active == ObdProtocol.Unknown || active == ObdProtocol.Auto) return false;
                if (family == ObdProtocol.Can11Bit500K) return ObdProtocolInfo.IsCan(active);
                if (family == ObdProtocol.KWP2000Fast) return active == ObdProtocol.KWP2000Fast || active == ObdProtocol.KWP2000Slow;
                if (family == ObdProtocol.J1850PWM) return active == ObdProtocol.J1850PWM || active == ObdProtocol.J1850VPW;
                return active == family;
            }

            /// <summary>Stylised OBD-II dongle: tilted dark housing with the 16-pin connector and a brand label.</summary>
            private void DrawDongle(Graphics g, RectangleF r, string brand)
            {
                var state = g.Save();
                g.TranslateTransform(r.X + r.Width / 2, r.Y + r.Height / 2);
                g.RotateTransform(-14f);
                float w = r.Width * 0.82f, h = r.Height * 0.5f, depth = 12f;
                var front = new RectangleF(-w / 2, -h / 2 + depth / 2, w, h);
                // shadow
                using (var gp = new GraphicsPath())
                {
                    gp.AddEllipse(-w * 0.55f, h * 0.3f, w * 1.1f, h * 0.6f);
                    using (var pb = new PathGradientBrush(gp) { CenterColor = Color.FromArgb(150, 0, 0, 0), SurroundColors = new[] { Color.FromArgb(0, 0, 0, 0) } })
                        g.FillPath(pb, gp);
                }
                // top face
                var top = new[] { new PointF(front.X + 8, front.Y - depth), new PointF(front.Right + 6, front.Y - depth), new PointF(front.Right, front.Y + 6), new PointF(front.X, front.Y + 6) };
                using (var b = new LinearGradientBrush(new RectangleF(front.X, front.Y - depth - 1, w + 8, depth + 8), Color.FromArgb(78, 82, 92), Color.FromArgb(40, 42, 50), LinearGradientMode.Vertical))
                    g.FillPolygon(b, top);
                // connector block on the left
                var conn = new RectangleF(front.X - 16, front.Y + 6, 18, h - 12);
                using (var b = new LinearGradientBrush(conn, Color.FromArgb(58, 60, 68), Color.FromArgb(22, 22, 28), LinearGradientMode.Horizontal))
                    g.FillRectangle(b, conn);
                using (var pen = new Pen(Color.FromArgb(150, 170, 150, 90), 1.2f))
                    for (int i = 0; i < 4; i++) g.DrawLine(pen, conn.X + 3, conn.Y + 5 + i * (conn.Height - 10) / 3f, conn.X + 10, conn.Y + 5 + i * (conn.Height - 10) / 3f);
                // front face
                using (var path = Theme.RoundedRect(front, 7))
                {
                    using (var b = new LinearGradientBrush(RectangleF.Inflate(front, 1, 1), Color.FromArgb(46, 48, 56), Color.FromArgb(12, 12, 16), LinearGradientMode.Vertical))
                        g.FillPath(b, path);
                    using (var pen = new Pen(Color.FromArgb(90, 255, 255, 255), 1f)) g.DrawPath(pen, path);
                }
                // grip ribs
                using (var pen = new Pen(Color.FromArgb(60, 255, 255, 255), 1f))
                    for (int i = 0; i < 3; i++) g.DrawLine(pen, front.Right - 10 - i * 5, front.Y + 8, front.Right - 10 - i * 5, front.Bottom - 8);
                using (var f = new Font("Segoe UI", 10.5f, FontStyle.Bold | FontStyle.Italic, GraphicsUnit.Point))
                using (var b = new SolidBrush(Color.FromArgb(235, 240, 245)))
                using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                    g.DrawString(brand, f, b, new RectangleF(front.X, front.Y, front.Width - 14, front.Height), sf);
                // status LED
                var led = AppState.Instance.IsConnected ? Theme.Green : Theme.Grey;
                Theme.GlowDot(g, new PointF(front.X + 10, front.Bottom - 9), 2.2f, led, 3);
                g.Restore(state);
            }
        }

        // ================================================================== system status

        internal sealed class StatusCard : DashCard
        {
            private static readonly string[] Shorts = { "ECM", "TCM", "ABS", "SRS", "BCM", "TPMS" };
            private static readonly string[] IconNames = { "engine", "transmission", "abs", "airbag", "body", "tpms" };

            public StatusCard(HomePage page) : base(page) { }

            protected override void PaintCard(Graphics g)
            {
                var st = AppState.Instance;
                Header(g, "heart", Loc.T("home.systemStatus"), Theme.Red, false);
                using (var pen = new Pen(Theme.BorderSoft, 1f)) g.DrawLine(pen, 12, 54, Width - 12, 54);

                var last = st.LastScan;
                int? score = HealthScore(last);
                var ringC = new PointF(88, 132);
                float rad = 58;
                var ringRect = new RectangleF(ringC.X - rad, ringC.Y - rad, rad * 2, rad * 2);
                using (var pen = new Pen(Color.FromArgb(34, 40, 62), 10f)) g.DrawEllipse(pen, ringRect);
                if (score.HasValue)
                {
                    var col = score >= 90 ? Theme.Green : score >= 70 ? Theme.Red : Theme.RedBright;
                    float sweep = 360f * score.Value / 100f;
                    for (int i = 3; i >= 1; i--)
                        using (var pen = new Pen(Alpha(col, 22), 10f + i * 5) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                            g.DrawArc(pen, ringRect, -90, sweep);
                    using (var pen = new Pen(col, 10f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                        g.DrawArc(pen, ringRect, -90, sweep);
                    // value
                    var num = score.Value.ToString();
                    var nsz = Measure(num, 24f, true);
                    var psz = Measure("%", 13f, true);
                    float total = nsz.Width + psz.Width;
                    Text(g, num, 24f, true, Color.White, new Rectangle((int)(ringC.X - total / 2), (int)ringC.Y - 34, nsz.Width + 4, 44), TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
                    Text(g, "%", 13f, true, Color.White, new Rectangle((int)(ringC.X - total / 2 + nsz.Width - 2), (int)ringC.Y - 22, psz.Width + 6, 28), TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
                }
                else Text(g, "—", 24f, true, Theme.TextMuted, new Rectangle((int)(ringC.X - 40), (int)ringC.Y - 34, 80, 44), TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                Text(g, score.HasValue ? Loc.T("home.healthScore") : Loc.T("home.noScan"), 8.5f, false, Theme.Text, new Rectangle((int)(ringC.X - 54), (int)ringC.Y + 10, 108, 20), TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                Pill(g, "details", new RectangleF(18, 206, 140, 30), Loc.T("home.viewDetails"), () => Page.Go(4));

                // module list
                float x = 174, y = 62, rowH = 30.5f;
                for (int i = 0; i < Shorts.Length; i++)
                {
                    var mr = last?.Modules.FirstOrDefault(m => string.Equals(m.Short, Shorts[i], StringComparison.OrdinalIgnoreCase));
                    Color c; string status; string glyph;
                    if (mr == null) { c = Theme.Grey; status = "—"; glyph = null; }
                    else
                    {
                        var ms = mr.ModuleStatus;
                        int n = mr.Dtcs.Count;
                        if (ms == ModuleStatus.Fault) { c = Theme.Red; glyph = "warn"; status = n == 1 ? Loc.T("home.issue1") : Loc.T("home.issueN", Math.Max(1, n)); }
                        else if (ms == ModuleStatus.Warning) { c = Theme.Orange; glyph = "warn"; status = n == 1 ? Loc.T("home.issue1") : Loc.T("home.issueN", Math.Max(1, n)); }
                        else if (ms == ModuleStatus.NoResponse) { c = Theme.Grey; glyph = "cross"; status = Loc.T("status.noresponse"); }
                        else { c = Theme.Green; glyph = "check"; status = Loc.T("home.ok"); }
                    }
                    float ry = y + i * rowH;
                    var badge = new RectangleF(x, ry + 3, 24, 24);
                    Theme.FillRounded(g, badge, 6, Alpha(c, 45));
                    Theme.DrawRounded(g, badge, 6, Alpha(c, 150));
                    Icons.Draw(g, IconNames[i], RectangleF.Inflate(badge, -5, -5), Theme.Lerp(c, Color.White, 0.25f), 1.4f);
                    var ssz = Measure(status, 8.5f, false);
                    float sx = Width - 16 - ssz.Width;
                    Text(g, Loc.T("home.m." + Shorts[i]), 9f, false, Theme.Text, new Rectangle((int)x + 32, (int)ry, (int)(sx - 24 - (x + 32)), (int)rowH));
                    Text(g, status, 8.5f, false, mr == null ? Theme.TextMuted : c == Theme.Green ? Theme.Text : c, new Rectangle((int)sx, (int)ry, ssz.Width + 2, (int)rowH));
                    if (glyph != null) DrawStatusGlyph(g, new RectangleF(sx - 20, ry + rowH / 2 - 7, 14, 14), glyph, c);
                    if (i < Shorts.Length - 1)
                        using (var pen = new Pen(Alpha(Theme.BorderSoft, 200), 1f)) g.DrawLine(pen, x, ry + rowH, Width - 14, ry + rowH);
                }
            }

            private static void DrawStatusGlyph(Graphics g, RectangleF r, string glyph, Color c)
            {
                if (glyph == "warn")
                {
                    var tri = new[] { new PointF(r.X + r.Width / 2, r.Y), new PointF(r.Right, r.Bottom), new PointF(r.X, r.Bottom) };
                    using (var b = new SolidBrush(c)) g.FillPolygon(b, tri);
                    using (var pen = new Pen(Color.FromArgb(20, 10, 10), 1.6f))
                        g.DrawLine(pen, r.X + r.Width / 2, r.Y + r.Height * 0.35f, r.X + r.Width / 2, r.Y + r.Height * 0.68f);
                    using (var b = new SolidBrush(Color.FromArgb(20, 10, 10))) g.FillEllipse(b, r.X + r.Width / 2 - 1, r.Y + r.Height * 0.77f, 2, 2);
                }
                else
                {
                    using (var b = new SolidBrush(c)) g.FillEllipse(b, r);
                    Icons.Draw(g, glyph, RectangleF.Inflate(r, -2, -2), Color.FromArgb(10, 20, 14), 1.8f);
                }
            }
        }

        // ================================================================== quick actions

        internal sealed class QuickActionsCard : DashCard
        {
            private bool _busy;
            public bool Busy { get { return _busy; } set { _busy = value; Invalidate(); } }

            private sealed class Tile
            {
                public string Id, TitleKey, SubKey, Icon;
                public Color Top, Bottom, Border, IconFill, IconColor;
                public bool IconOutline;
            }

            private static readonly Tile[] Tiles =
            {
                new Tile { Id = "scan", TitleKey = "home.fullScan", SubKey = "home.scanAll", Icon = "vehicle", Top = Color.FromArgb(214, 22, 56), Bottom = Color.FromArgb(120, 10, 30), Border = Color.FromArgb(255, 70, 100), IconFill = Color.FromArgb(70, 255, 255, 255), IconColor = Color.White },
                new Tile { Id = "read", TitleKey = "home.readDtcs", SubKey = "home.viewDtcs", Icon = "filesearch", Top = Color.FromArgb(26, 32, 52), Bottom = Color.FromArgb(16, 20, 36), Border = Color.FromArgb(60, 72, 110), IconFill = Color.FromArgb(40, 90, 140, 220), IconColor = Color.FromArgb(150, 190, 255), IconOutline = true },
                new Tile { Id = "clear", TitleKey = "home.clearDtcs", SubKey = "home.eraseDtcs", Icon = "trash", Top = Color.FromArgb(70, 44, 14), Bottom = Color.FromArgb(34, 22, 10), Border = Color.FromArgb(200, 130, 30), IconFill = Color.FromArgb(235, 150, 20), IconColor = Color.FromArgb(40, 20, 0) },
                new Tile { Id = "live", TitleKey = "home.liveData", SubKey = "home.realtime", Icon = "diagnose", Top = Color.FromArgb(18, 48, 110), Bottom = Color.FromArgb(12, 24, 60), Border = Color.FromArgb(40, 100, 220), IconFill = Color.FromArgb(30, 110, 235), IconColor = Color.White },
                new Tile { Id = "service", TitleKey = "home.serviceFunctions", SubKey = "home.resetAdapt", Icon = "wrench", Top = Color.FromArgb(62, 28, 118), Bottom = Color.FromArgb(30, 16, 62), Border = Color.FromArgb(130, 80, 230), IconFill = Color.FromArgb(124, 58, 237), IconColor = Color.White },
            };

            public QuickActionsCard(HomePage page) : base(page) { }

            protected override void PaintCard(Graphics g)
            {
                Header(g, "bolt", Loc.T("home.quickActions"), Theme.Red, false);
                float gap = 10, x0 = 14, y = 48, h = 66;
                float w = (Width - 2 * x0 - gap * (Tiles.Length - 1)) / Tiles.Length;
                for (int i = 0; i < Tiles.Length; i++)
                {
                    var t = Tiles[i];
                    var r = new RectangleF(x0 + i * (w + gap), y, w, h);
                    bool hov = IsHover(t.Id);
                    bool disabled = _busy && (t.Id == "read" || t.Id == "clear");
                    if (hov && !disabled) Theme.Glow(g, r, 10, t.Border, 5, 120);
                    using (var path = Theme.RoundedRect(r, 10))
                    using (var b = new LinearGradientBrush(RectangleF.Inflate(r, 1, 1), hov ? Theme.Lerp(t.Top, Color.White, 0.08f) : t.Top, t.Bottom, 20f))
                        g.FillPath(b, path);
                    Theme.DrawRounded(g, r, 10, Alpha(t.Border, hov ? 255 : 170));

                    var ib = new RectangleF(r.X + 12, r.Y + (h - 40) / 2, 40, 40);
                    Theme.FillRounded(g, ib, 9, t.IconFill);
                    if (t.IconOutline) Theme.DrawRounded(g, ib, 9, Alpha(t.IconColor, 120));
                    Icons.Draw(g, t.Icon, RectangleF.Inflate(ib, -9, -9), t.IconColor, 1.9f);

                    var tx = (int)ib.Right + 10;
                    var tw = (int)(r.Right - 26 - tx);
                    var title = Loc.T(t.TitleKey);
                    Text(g, title, Fit(title, 10.5f, 8.5f, true, tw), true, disabled ? Theme.TextMuted : Color.White, new Rectangle(tx, (int)r.Y + 12, tw, 22));
                    Text(g, Loc.T(t.SubKey), 8.5f, false, Theme.Lerp(Theme.TextMuted, Color.White, 0.25f), new Rectangle(tx, (int)r.Y + 35, tw, 18));
                    Icons.Draw(g, "forward", new RectangleF(r.Right - 22, r.Y + h / 2 - 7, 14, 14), Color.White, 1.8f);

                    var id = t.Id;
                    var rr = r;
                    AddHot(id, r, () => OnTile(id, rr));
                }
            }

            private void OnTile(string id, RectangleF r)
            {
                switch (id)
                {
                    case "scan": Page.Go(1); break;
                    case "read": Page.ReadDtcs(); break;
                    case "clear": Page.ClearDtcs(); break;
                    case "live": Page.Go(2); break;
                    case "service": Page.ShowServiceMenu(this, new Point((int)r.X, (int)r.Bottom + 4)); break;
                }
            }
        }

        // ================================================================== recent scans

        internal sealed class RecentCard : DashCard
        {
            public RecentCard(HomePage page) : base(page) { EnableScrolling(); }

            protected override void PaintCard(Graphics g)
            {
                var st = AppState.Instance;
                Header(g, "clock", Loc.T("home.recentScans"), Theme.Red, false);
                var va = Loc.T("home.viewAll");
                var vsz = Measure(va, 9f, false);
                var vr = new RectangleF(Width - 18 - vsz.Width, 18, vsz.Width + 4, 22);
                Text(g, va, 9f, IsHover("all"), Color.FromArgb(96, 165, 250), Rectangle.Round(vr), TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
                AddHot("all", vr, () => Page.Go(5));
                using (var pen = new Pen(Theme.BorderSoft, 1f)) g.DrawLine(pen, 12, 54, Width - 12, 54);

                var items = st.History.Take(50).ToList();
                if (items.Count == 0)
                {
                    Text(g, Loc.T("history.none"), 10f, false, Theme.TextMuted, new Rectangle(0, 60, Width, Height - 70), TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                    return;
                }
                float y = 62, rowH = 56;
                var thumb = CarImages.Get(CarShot.Thumb, new Size(76, 40));
                var vp = new Rectangle(8, 60, Width - 16, Height - 66);
                int content = (int)(items.Count * rowH);
                int rowW = Width - 20 - (Overflows(content, vp.Height) ? 8 : 0);
                var clip = BeginViewport(g, vp, content);
                y -= ScrollOffset;
                for (int i = 0; i < items.Count; i++)
                {
                    var s = items[i];
                    var row = new RectangleF(10, y + i * rowH, rowW, rowH - 6);
                    if (row.Bottom < vp.Top || row.Top > vp.Bottom) continue;
                    string id = "row" + i;
                    bool hov = IsHover(id);
                    Theme.FillRounded(g, row, 8, hov ? Alpha(Theme.Cyan, 18) : Alpha(Color.White, 6));
                    Theme.DrawRounded(g, row, 8, hov ? Alpha(Theme.Cyan, 90) : Alpha(Theme.BorderSoft, 255));

                    var tb = new RectangleF(row.X + 8, row.Y + 5, 80, row.Height - 10);
                    using (var path = Theme.RoundedRect(tb, 6))
                    using (var b = new LinearGradientBrush(RectangleF.Inflate(tb, 1, 1), Color.FromArgb(44, 50, 70), Color.FromArgb(22, 26, 40), LinearGradientMode.Vertical))
                        g.FillPath(b, path);
                    if (thumb != null) g.DrawImage(thumb, tb.X + 2, tb.Y + (tb.Height - 40) / 2, 76, 40);

                    var v = FindVehicle(s.Vin);
                    string name = v != null ? (v.Make + " " + v.Model).Trim() : s.VehicleName;
                    string sub = v != null ? string.Join("  •  ", new[] { v.Year > 0 ? v.Year.ToString() : null, v.Engine }.Where(z => !string.IsNullOrWhiteSpace(z))) : s.Vin;
                    int tx = (int)tb.Right + 12;
                    int colDate = (int)(row.X + row.Width * 0.5f);
                    Text(g, name, 10f, true, Color.White, new Rectangle(tx, (int)row.Y + 6, colDate - tx - 6, 20));
                    Text(g, sub, 8.5f, false, Theme.TextMuted, new Rectangle(tx, (int)row.Y + 26, colDate - tx - 6, 18));
                    Text(g, FormatDate(s.Started), 9f, false, Theme.Text, new Rectangle(colDate, (int)row.Y + 6, 150, 20));
                    Text(g, Loc.T("home.faultsModules", s.TotalDtcs, s.Scanned), 8.5f, false, Theme.TextMuted, new Rectangle(colDate, (int)row.Y + 26, 150, 18));

                    string pill; Color pc;
                    if (s.Faults > 0) { pill = Loc.T("home.issues"); pc = Theme.Red; }
                    else if (s.Warnings > 0) { pill = Loc.T("status.warning"); pc = Theme.Orange; }
                    else { pill = Loc.T("home.healthy"); pc = Theme.Green; }
                    var psz = Measure(pill, 8.5f, false);
                    var pr = new RectangleF(row.Right - 40 - Math.Max(62, psz.Width + 20), row.Y + (row.Height - 24) / 2, Math.Max(62, psz.Width + 20), 24);
                    Theme.FillRounded(g, pr, 12, Alpha(pc, 45));
                    Theme.DrawRounded(g, pr, 12, Alpha(pc, 110));
                    Text(g, pill, 8.5f, false, Theme.Lerp(pc, Color.White, 0.2f), Rectangle.Round(pr), TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                    Icons.Draw(g, "forward", new RectangleF(row.Right - 26, row.Y + row.Height / 2 - 7, 14, 14), Theme.Text, 1.7f);
                    AddRowHot(id, row, () => Page.Go(5));
                }
                EndViewport(g, clip);
            }
        }

        // ================================================================== DTC lookup

        internal sealed class DtcCard : DashCard
        {
            private static readonly string[] Popular = { "P0301", "C0035", "B0001", "U0100" };
            private readonly TextBox _box;
            private string _query = "";

            [DllImport("user32.dll", CharSet = CharSet.Unicode)]
            private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, string lParam);

            public DtcCard(HomePage page) : base(page)
            {
                _box = new TextBox
                {
                    BorderStyle = BorderStyle.None,
                    BackColor = Color.FromArgb(11, 14, 26),
                    ForeColor = Theme.Text,
                    Font = Theme.Font(10f),
                    Bounds = new Rectangle(44, 61, Width - 150, 20),
                    MaxLength = 12
                };
                _box.TextChanged += (s, e) => { _query = _box.Text.Trim(); Scroller.Reset(); Invalidate(); };
                _box.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; OpenFirst(); } };
                _box.HandleCreated += (s, e) => SetCue();
                Controls.Add(_box);
                EnableScrolling();
            }

            protected override void OnResize(EventArgs e)
            {
                base.OnResize(e);
                if (_box != null) _box.Width = Width - 150;
            }

            protected override void OnLocalize()
            {
                if (_box == null) return;
                _box.Font = Theme.Font(10f);
                SetCue();
            }

            private void SetCue()
            {
                if (_box.IsHandleCreated) SendMessage(_box.Handle, 0x1501 /* EM_SETCUEBANNER */, (IntPtr)1, Loc.T("home.dtcPlaceholder"));
            }

            private List<DtcInfo> Results()
            {
                if (_query.Length == 0) return Popular.Select(DtcDatabase.Lookup).ToList();
                return DtcDatabase.Search(_query, 30).Take(30).ToList();
            }

            private void OpenFirst()
            {
                var r = Results();
                if (r.Count > 0) ShowDtc(r[0]);
            }

            protected override void PaintCard(Graphics g)
            {
                Header(g, "alert", Loc.T("common.dtcLookup"), Theme.Red, false);
                using (var pen = new Pen(Theme.BorderSoft, 1f)) g.DrawLine(pen, 12, 44, Width - 12, 44);

                var field = new RectangleF(16, 54, Width - 16 - 16 - 84, 34);
                Theme.FillRounded(g, field, 8, _box.BackColor);
                Theme.DrawRounded(g, field, 8, _box.Focused ? Alpha(Theme.Red, 160) : Alpha(Color.White, 40));
                Icons.Draw(g, "search", new RectangleF(field.X + 8, field.Y + 9, 16, 16), Theme.TextMuted, 1.6f);
                if (_query.Length == 0 && !_box.Focused && !_box.IsHandleCreated)
                    Text(g, Loc.T("home.dtcPlaceholder"), 9f, false, Theme.TextDim, new Rectangle((int)field.X + 30, (int)field.Y, (int)field.Width - 34, (int)field.Height));

                var btn = new RectangleF(Width - 16 - 76, 54, 76, 34);
                bool hov = IsHover("search");
                using (var path = Theme.RoundedRect(btn, 8))
                using (var b = new LinearGradientBrush(RectangleF.Inflate(btn, 1, 1), hov ? Theme.RedBright : Theme.Red, Theme.Lerp(Theme.Red, Color.Black, 0.25f), LinearGradientMode.Vertical))
                    g.FillPath(b, path);
                Text(g, Loc.T("common.search"), 9.5f, true, Color.White, Rectangle.Round(btn), TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                AddHot("search", btn, OpenFirst);

                Text(g, Loc.T(_query.Length == 0 ? "home.popular" : "home.results"), 10f, true, Theme.Text, new Rectangle(16, 96, Width - 32, 24));
                var list = Results();
                if (list.Count == 0)
                {
                    Text(g, Loc.T("home.noResults"), 9.5f, false, Theme.TextMuted, new Rectangle(16, 124, Width - 32, 60), TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                    return;
                }
                float y = 124, rowH = 41;
                var vp = new Rectangle(12, 122, Width - 20, Height - 128);
                int content = (int)(list.Count * rowH);
                int rowW = Width - 28 - (Overflows(content, vp.Height) ? 6 : 0);
                var clip = BeginViewport(g, vp, content);
                y -= ScrollOffset;
                for (int i = 0; i < list.Count; i++)
                {
                    var d = list[i];
                    var row = new RectangleF(14, y + i * rowH, rowW, rowH - 5);
                    if (row.Bottom < vp.Top || row.Top > vp.Bottom) continue;
                    string id = "dtc" + i;
                    bool h = IsHover(id);
                    Theme.FillRounded(g, row, 7, h ? Alpha(Theme.Red, 26) : Alpha(Color.White, 7));
                    Theme.DrawRounded(g, row, 7, h ? Alpha(Theme.Red, 120) : Alpha(Theme.BorderSoft, 255));
                    var cp = new RectangleF(row.X + 6, row.Y + 5, 62, row.Height - 10);
                    Theme.FillRounded(g, cp, 5, Color.FromArgb(96, 14, 32));
                    Theme.DrawRounded(g, cp, 5, Alpha(Theme.Red, 150));
                    using (var f = new Font("Consolas", 10f, FontStyle.Bold))
                        Theme.DrawText(g, d.Code, f, Color.White, Rectangle.Round(cp), TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                    int tx = (int)cp.Right + 10;
                    Text(g, d.Description, 8.5f, true, Theme.Text, new Rectangle(tx, (int)row.Y + 2, (int)(row.Right - 24 - tx), 18));
                    Text(g, d.SystemName, 7.5f, false, Theme.TextMuted, new Rectangle(tx, (int)row.Y + 18, (int)(row.Right - 24 - tx), 16));
                    Icons.Draw(g, "forward", new RectangleF(row.Right - 20, row.Y + row.Height / 2 - 6, 12, 12), Theme.TextMuted, 1.6f);
                    var dd = d;
                    AddRowHot(id, row, () => ShowDtc(dd));
                }
                EndViewport(g, clip);
            }
        }

        // ================================================================== promo

        internal sealed class PromoCard : DashCard
        {
            public PromoCard(HomePage page) : base(page) { }

            protected override void PaintBackground(Graphics g, RectangleF r)
            {
                using (var path = Theme.RoundedRect(r, 12))
                {
                    var oldClip = g.Clip;
                    g.SetClip(path);
                    using (var b = new LinearGradientBrush(r, Color.FromArgb(22, 24, 38), Color.FromArgb(8, 8, 14), LinearGradientMode.Vertical))
                        g.FillRectangle(b, r);
                    // mountain ridges
                    DrawRidge(g, 0.42f, 0.2f, 1.7f, Color.FromArgb(44, 46, 60), Color.FromArgb(20, 20, 30));
                    DrawRidge(g, 0.52f, 0.14f, 2.9f, Color.FromArgb(28, 28, 40), Color.FromArgb(12, 12, 18));
                    // red horizon glow
                    using (var gp = new GraphicsPath())
                    {
                        gp.AddEllipse(-Width * 0.2f, Height * 0.5f, Width * 1.4f, Height * 0.6f);
                        using (var pb = new PathGradientBrush(gp) { CenterColor = Color.FromArgb(140, 200, 20, 50), SurroundColors = new[] { Color.FromArgb(0, 200, 20, 50) } })
                            g.FillPath(pb, gp);
                    }
                    // road
                    var road = new[] { new PointF(Width * 0.42f, Height * 0.66f), new PointF(Width * 0.58f, Height * 0.66f), new PointF(Width * 1.1f, Height), new PointF(-Width * 0.1f, Height) };
                    using (var b = new LinearGradientBrush(new RectangleF(0, Height * 0.66f - 1, Width, Height * 0.34f + 2), Color.FromArgb(200, 14, 12, 18), Color.FromArgb(230, 6, 6, 10), LinearGradientMode.Vertical))
                        g.FillPolygon(b, road);
                    g.Clip = oldClip;
                }
                Theme.DrawRounded(g, new RectangleF(1, 1, Width - 2, Height - 2), 12, BorderColor);
            }

            private void DrawRidge(Graphics g, float baseY, float amp, float freq, Color top, Color bottom)
            {
                var pts = new List<PointF> { new PointF(0, Height) };
                for (int i = 0; i <= 40; i++)
                {
                    float x = Width * i / 40f;
                    double t = i / 40.0;
                    float y = Height * (baseY - amp * (float)(0.55 * Math.Abs(Math.Sin(t * Math.PI * freq)) + 0.3 * Math.Abs(Math.Sin(t * Math.PI * freq * 2.7 + 1.3)) + 0.15 * Math.Sin(t * 37)));
                    pts.Add(new PointF(x, y));
                }
                pts.Add(new PointF(Width, Height));
                using (var b = new LinearGradientBrush(new RectangleF(0, Height * (baseY - amp) - 1, Width, Height * (1 - baseY + amp) + 2), top, bottom, LinearGradientMode.Vertical))
                    g.FillPolygon(b, pts.ToArray());
            }

            protected override void PaintCard(Graphics g)
            {
                Text(g, Loc.T("home.promo1"), 13.5f, true, Color.White, new Rectangle(0, 22, Width, 26), TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                Text(g, Loc.T("home.promo2"), 13.5f, true, Color.White, new Rectangle(0, 48, Width, 26), TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);

                var img = CarImages.Get(CarShot.Rear, new Size(230, 116));
                if (img != null) g.DrawImage(img, (Width - 230) / 2, 88, 230, 116);

                string[] icons = { "target2", "bolt", "shield" };
                string[] keys = { "home.accurate", "home.fast", "home.reliable" };
                float colW = Width / 3f;
                for (int i = 0; i < 3; i++)
                {
                    float cx = colW * i + colW / 2;
                    Icons.Draw(g, icons[i], new RectangleF(cx - 12, 216, 24, 24), Theme.Text, 1.7f);
                    Text(g, Loc.T(keys[i]), 9.5f, false, Theme.Text, new Rectangle((int)(cx - colW / 2), 244, (int)colW, 22), TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                }
            }
        }

        // ================================================================== dark context menu

        private sealed class DarkMenuRenderer : ToolStripProfessionalRenderer
        {
            public DarkMenuRenderer() : base(new DarkColors()) { RoundedEdges = false; }

            protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
            {
                e.TextColor = Theme.Text;
                base.OnRenderItemText(e);
            }

            private sealed class DarkColors : ProfessionalColorTable
            {
                public override Color ToolStripDropDownBackground => Theme.SurfaceAlt;
                public override Color MenuBorder => Theme.Border;
                public override Color MenuItemBorder => Theme.WithAlpha(Theme.Red, 160);
                public override Color MenuItemSelected => Color.FromArgb(70, 20, 32);
                public override Color MenuItemSelectedGradientBegin => Color.FromArgb(70, 20, 32);
                public override Color MenuItemSelectedGradientEnd => Color.FromArgb(70, 20, 32);
                public override Color ImageMarginGradientBegin => Theme.SurfaceAlt;
                public override Color ImageMarginGradientMiddle => Theme.SurfaceAlt;
                public override Color ImageMarginGradientEnd => Theme.SurfaceAlt;
                public override Color SeparatorDark => Theme.Border;
                public override Color SeparatorLight => Theme.Border;
            }
        }
    }
}
