using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Linq;
using System.Windows.Forms;
using RedlineDiagnostics.App;
using RedlineDiagnostics.Controls;
using RedlineDiagnostics.Database;
using RedlineDiagnostics.Localization;
using RedlineDiagnostics.Obd;
using RedlineDiagnostics.Rendering3D;

namespace RedlineDiagnostics.Forms
{
    /// <summary>
    /// Start-up screen. Shows real progress while the databases, the 3D model and the adapter check load, lets the
    /// user pick the language by tapping, then raises <see cref="Finished"/> so the main window can take over.
    /// Everything is painted into one 1366 x 768 frame that is centred on a black backdrop in full-screen mode.
    /// </summary>
    public sealed class SplashForm : Form
    {
        private const int W = 1366, H = 768;
        private const double MinVisibleSeconds = 3.0;
        private const double MaxCarWaitSeconds = 2.5;

        private readonly Timer _timer = new Timer { Interval = 30 };
        private readonly DateTime _start = DateTime.Now;
        private DateTime _stepStart = DateTime.Now, _readyAt = DateTime.MinValue, _carShownAt = DateTime.MinValue;
        private int _step;
        private float _progress, _target = 0.08f;
        private string _statusKey = "splash.init";
        private Func<string> _detail = () => ""; // evaluated at draw time so a language tap re-translates it
        private bool _meshReady, _finished, _closed;
        private Bitmap _backdrop, _frame, _holo, _reflection, _reflectionSource;
        private readonly RectangleF[] _langRects = new RectangleF[3];
        private int _hoverLang = -1;
        private Point _origin;

        /// <summary>Raised once when loading is complete and the main window should be shown.</summary>
        public event Action Finished;

        /// <summary>When set, the splash runs off-screen, writes captures here and closes instead of finishing.</summary>
        public string AutoTestDir { get; set; }

        /// <summary>With <see cref="AutoTestDir"/>: hand over to the main window as in a normal start instead of closing.</summary>
        public bool AutoTestContinue { get; set; }

        private static readonly Rectangle CarRect = new Rectangle(560, 196, 760, 404);

        public SplashForm()
        {
            AppState.Instance.Ui = System.Threading.SynchronizationContext.Current ?? new WindowsFormsSynchronizationContext();
            Text = Loc.T("app.title");
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            BackColor = Color.Black;
            DoubleBuffered = true;
            ShowInTaskbar = true;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
            PositionWindow();
            _timer.Tick += (s, e) => Tick();
            AppState.Instance.MeshChanged += OnMeshChanged;
            CarImages.Changed += OnCarImageChanged;
        }

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.Style |= 0x00020000; // WS_MINIMIZEBOX: taskbar click can minimize / restore
                return cp;
            }
        }

        private void PositionWindow()
        {
            var screen = Screen.PrimaryScreen.Bounds;
            bool full = AppState.Instance.Settings.FullScreen && string.IsNullOrEmpty(AutoTestDir);
            if (full) Bounds = screen;
            else
            {
                ClientSize = new Size(W, H);
                Location = new Point(screen.X + Math.Max(0, (screen.Width - W) / 2), screen.Y + Math.Max(0, (screen.Height - H) / 2));
            }
            _origin = new Point(Math.Max(0, (ClientSize.Width - W) / 2), Math.Max(0, (ClientSize.Height - H) / 2));
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Touch.Configure(Handle, false);
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            if (!string.IsNullOrEmpty(AutoTestDir))
            {
                Location = new Point(Screen.PrimaryScreen.Bounds.Right + 20, 0);
                System.IO.Directory.CreateDirectory(AutoTestDir);
            }
            _timer.Start();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            _origin = new Point(Math.Max(0, (ClientSize.Width - W) / 2), Math.Max(0, (ClientSize.Height - H) / 2));
            Invalidate();
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            _timer.Stop();
            AppState.Instance.MeshChanged -= OnMeshChanged;
            CarImages.Changed -= OnCarImageChanged;
            _closed = true; // a Tick already in progress must not touch the bitmaps disposed below
            foreach (var b in new[] { _backdrop, _frame, _holo, _reflection }) b?.Dispose();
            _backdrop = _frame = _holo = _reflection = null;
            base.OnFormClosed(e);
        }

        // ------------------------------------------------------------------ loading sequence

        private void OnMeshChanged()
        {
            _meshReady = true;
            BuildHolo();
            CarImages.Get(CarShot.Splash, CarRect.Size); // start the background render now
        }

        private void OnCarImageChanged()
        {
            if (_carShownAt == DateTime.MinValue && CarImages.Get(CarShot.Splash, CarRect.Size) != null) _carShownAt = DateTime.Now;
        }

        private void Tick()
        {
            if (_finished || _closed) return;
            var st = AppState.Instance;
            double inStep = (DateTime.Now - _stepStart).TotalSeconds;
            switch (_step)
            {
                case 0:
                    if (inStep > 0.25)
                    {
                        _statusKey = "splash.db";
                        Render();
                        Refresh(); // show the message before the synchronous load
                        st.Initialize();
                        _detail = () => Loc.T("splash.dbDone", DtcDatabase.Count, PidDatabase.Count, ModuleCatalog.Count);
                        _target = 0.35f;
                        Next(1);
                    }
                    break;
                case 1:
                    _statusKey = "splash.model";
                    if (inStep > 0.3) _target = Math.Min(0.78f, 0.4f + (float)inStep * 0.08f);
                    if (_meshReady && inStep > 0.3)
                    {
                        var modelLine = string.IsNullOrEmpty(st.ActiveModelName) ? "3D: built-in" : "3D: " + st.ActiveModelName + " · " + st.ActiveModelInfo;
                        _detail = () => modelLine;
                        _target = 0.8f;
                        Next(2);
                    }
                    else if (inStep > 15) Next(2); // never block start-up on a slow model
                    break;
                case 2:
                    _statusKey = "splash.iface";
                    if (inStep > 0.15 && _target < 0.9f)
                    {
                        var iface = CheckInterface; // the port check itself is cheap
                        _detail = iface;
                        _target = 0.93f;
                    }
                    if (inStep > 0.7) { _target = 1f; _statusKey = "splash.ready"; _readyAt = DateTime.Now; Next(3); }
                    break;
                case 3:
                    double total = (DateTime.Now - _start).TotalSeconds;
                    bool carDone = _carShownAt != DateTime.MinValue && (DateTime.Now - _carShownAt).TotalSeconds > 0.7;
                    bool carGaveUp = (DateTime.Now - _readyAt).TotalSeconds > MaxCarWaitSeconds;
                    if (_progress > 0.995f && total > MinVisibleSeconds && (carDone || carGaveUp || !_meshReady)) Finish();
                    break;
            }
            // Finish() shows the main window and closes (disposes) this form; nothing may render after it.
            if (_finished || _closed) return;
            _progress += (_target - _progress) * 0.12f;
            if (Math.Abs(_target - _progress) < 0.002f) _progress = _target;
            RunAutoTestCaptures();
            if (_finished || _closed) return;
            Render();
            Invalidate();
        }

        private void Next(int step)
        {
            _step = step;
            _stepStart = DateTime.Now;
        }

        private static string CheckInterface()
        {
            var s = AppState.Instance.Settings;
            string name = ObdProtocolInfo.AdapterName(s.Adapter);
            switch (s.Adapter)
            {
                case AdapterType.Simulator:
                    return Loc.T("splash.ifaceSim");
                case AdapterType.Elm327WiFi:
                    return Loc.T("splash.ifaceWifi", name, s.Host + ":" + s.TcpPort);
                default:
                    var ports = SerialTransport.AvailablePorts();
                    return !string.IsNullOrEmpty(s.SerialPort) && ports.Contains(s.SerialPort)
                        ? Loc.T("splash.ifacePort", name, s.SerialPort)
                        : Loc.T("splash.ifaceMissing", name, string.IsNullOrEmpty(s.SerialPort) ? "—" : s.SerialPort);
            }
        }

        private void Finish()
        {
            if (_finished) return;
            _finished = true;
            _timer.Stop();
            if (!string.IsNullOrEmpty(AutoTestDir) && !AutoTestContinue) { Close(); return; }
            Finished?.Invoke();
        }

        // ------------------------------------------------------------------ automated captures

        private int _shotStage;

        private void RunAutoTestCaptures()
        {
            if (string.IsNullOrEmpty(AutoTestDir) || _frame == null) return;
            double t = (DateTime.Now - _start).TotalSeconds;
            if (_shotStage == 0 && t > 0.6) { Save("splash_start"); _shotStage = 1; }
            else if (_shotStage == 1 && _step == 1 && t > 1.2) { Save("splash_loading"); _shotStage = 2; }
            else if (_shotStage == 2 && _step == 3 && _progress > 0.99f && (_carShownAt != DateTime.MinValue && (DateTime.Now - _carShownAt).TotalSeconds > 0.8 || (DateTime.Now - _readyAt).TotalSeconds > MaxCarWaitSeconds))
            {
                Save("splash_ready");
                Loc.Current = Language.JA; Render(); Save("splash_ja");
                Loc.Current = Language.ZH; Render(); Save("splash_zh");
                Loc.Current = Language.EN;
                _shotStage = 3;
                Finish();
            }
        }

        private void Save(string name)
        {
            try { _frame.Save(System.IO.Path.Combine(AutoTestDir, name + ".png"), ImageFormat.Png); }
            catch (Exception ex) { AppState.Instance.Log("splash shot failed: " + ex.Message); }
        }

        // ------------------------------------------------------------------ input (language pills)

        protected override void OnMouseMove(MouseEventArgs e)
        {
            var p = new PointF(e.X - _origin.X, e.Y - _origin.Y);
            int h = -1;
            for (int i = 0; i < 3; i++) if (_langRects[i].Contains(p)) h = i;
            if (h != _hoverLang) { _hoverLang = h; Cursor = h >= 0 ? Cursors.Hand : Cursors.Default; }
            base.OnMouseMove(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            if (Touch.IsTouchMessage()) _hoverLang = -1;
            base.OnMouseUp(e);
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            var p = new PointF(e.X - _origin.X, e.Y - _origin.Y);
            for (int i = 0; i < 3; i++)
                if (_langRects[i].Contains(p))
                {
                    Loc.Current = (Language)i;
                    var s = AppState.Instance.Settings;
                    s.Language = Loc.Code;
                    s.Save();
                    Text = Loc.T("app.title");
                    break;
                }
            base.OnMouseClick(e);
        }

        // ------------------------------------------------------------------ painting

        protected override void OnPaintBackground(PaintEventArgs e) { }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            using (var b = new SolidBrush(Color.Black))
            {
                if (_origin.X > 0) { g.FillRectangle(b, 0, 0, _origin.X, ClientSize.Height); g.FillRectangle(b, _origin.X + W, 0, ClientSize.Width - _origin.X - W, ClientSize.Height); }
                if (_origin.Y > 0) { g.FillRectangle(b, 0, 0, ClientSize.Width, _origin.Y); g.FillRectangle(b, 0, _origin.Y + H, ClientSize.Width, ClientSize.Height - _origin.Y - H); }
            }
            if (_closed) return;
            if (_frame == null) Render();
            if (_frame != null) g.DrawImageUnscaled(_frame, _origin);
        }

        private void Render()
        {
            if (_closed) return;
            if (_frame == null) _frame = new Bitmap(W, H, PixelFormat.Format32bppPArgb);
            if (_backdrop == null) _backdrop = BuildBackdrop();
            using (var g = Graphics.FromImage(_frame))
            {
                Theme.Setup(g);
                g.DrawImageUnscaled(_backdrop, 0, 0);
                DrawHoloContent(g);
                DrawCar(g);
                DrawBrand(g);
                DrawFeatures(g);
                DrawProgress(g);
                DrawLanguages(g);
                using (var f = new Font("Segoe UI", 9f))
                    Theme.DrawText(g, "v" + Application.ProductVersion.Split('+')[0], f, Color.FromArgb(120, 125, 135), new Point(1284, 690));
            }
        }

        // ---- static backdrop: garage, neon ceiling, holographic panels ----

        private static Bitmap BuildBackdrop()
        {
            var bmp = new Bitmap(W, H, PixelFormat.Format32bppPArgb);
            using (var g = Graphics.FromImage(bmp))
            {
                Theme.Setup(g);
                using (var b = new LinearGradientBrush(new Rectangle(0, 0, W, H), Color.FromArgb(8, 8, 11), Color.FromArgb(14, 6, 8), LinearGradientMode.Horizontal))
                    g.FillRectangle(b, 0, 0, W, H);
                RadialGlow(g, new PointF(1000, 330), 640, 380, Color.FromArgb(70, 170, 10, 30));

                // ceiling beams converging on a vanishing point, with red neon strips
                var vp = new PointF(760, 330);
                using (var pen = new Pen(Color.FromArgb(40, 90, 95, 110), 1f))
                    for (int i = 0; i < 12; i++) g.DrawLine(pen, vp, new PointF(560 + i * 90, -40));
                for (int i = 0; i < 9; i++)
                {
                    var top = new PointF(640 + i * 110, -60);
                    float t0 = 0.5f + (i % 3) * 0.08f, t1 = t0 + 0.2f;
                    var a = Lerp(vp, top, t0);
                    var c = Lerp(vp, top, t1);
                    Neon(g, a, c, 4f + i * 0.4f);
                }
                Neon(g, new PointF(0, 150), new PointF(185, 0), 3f);
                Neon(g, new PointF(1070, 175), new PointF(1366, 118), 2.5f);
                Neon(g, new PointF(990, 100), new PointF(1366, 32), 3.5f);

                // pillars
                FillVertical(g, new RectangleF(588, 190, 46, 420), Color.FromArgb(20, 20, 24), Color.FromArgb(10, 10, 12));
                FillVertical(g, new RectangleF(826, 150, 30, 300), Color.FromArgb(24, 18, 22), Color.FromArgb(10, 8, 10));
                Neon(g, new PointF(962, 250), new PointF(962, 470), 2f);

                // floor
                using (var b = new LinearGradientBrush(new RectangleF(0, 590, W, H - 590), Color.FromArgb(18, 10, 12), Color.FromArgb(4, 4, 6), LinearGradientMode.Vertical))
                    g.FillRectangle(b, 0, 590, W, H - 590);
                Neon(g, new PointF(0, 640), new PointF(600, 598), 3f);
                Neon(g, new PointF(1235, 592), new PointF(1366, 588), 3f);
                Neon(g, new PointF(1210, 380), new PointF(1366, 368), 2.5f);
                for (int i = 0; i < 6; i++)
                {
                    float x = 650 + i * 120;
                    using (var br = new LinearGradientBrush(new RectangleF(x - 3, 600, 6, 168), Color.FromArgb(60, 229, 23, 60), Color.FromArgb(0, 229, 23, 60), LinearGradientMode.Vertical))
                        g.FillRectangle(br, x - 2, 600, 4, 168);
                }

                DrawHoloPanels(g);

                // left vignette so the brand and text read clearly
                using (var b = new LinearGradientBrush(new RectangleF(0, 0, 720, H), Color.FromArgb(235, 4, 4, 6), Color.FromArgb(0, 4, 4, 6), LinearGradientMode.Horizontal))
                    g.FillRectangle(b, 0, 0, 720, H);
            }
            return bmp;
        }

        private static void DrawHoloPanels(Graphics g)
        {
            // main panel (car wireframe + module list), slightly in perspective
            var main = new[] { new PointF(905, 118), new PointF(1228, 86), new PointF(1232, 338), new PointF(910, 352) };
            HoloPanel(g, main);
            using (var f = new Font("Segoe UI", 5.5f))
            using (var b = new SolidBrush(Color.FromArgb(150, 255, 70, 90)))
                g.DrawString("SYSTEM SCAN", f, b, 918, 124);
            string[] mods = { "ECM", "TCM", "ABS", "SRS", "BCM", "TPMS", "HVAC" };
            var list = new[] { new PointF(1160, 96), new PointF(1224, 90), new PointF(1226, 272), new PointF(1162, 278) };
            HoloPanel(g, list);
            using (var f = new Font("Segoe UI", 9f, FontStyle.Regular))
                for (int i = 0; i < mods.Length; i++)
                {
                    float y = 104 + i * 24;
                    Theme.GlowDot(g, new PointF(1172, y + 8), 3f, Color.FromArgb(255, 40, 60), 2);
                    using (var b = new SolidBrush(Color.FromArgb(225, 255, 120, 130))) g.DrawString(mods[i], f, b, 1182, y);
                }
            // lower data panel
            var data = new[] { new PointF(1152, 290), new PointF(1230, 286), new PointF(1232, 346), new PointF(1154, 350) };
            HoloPanel(g, data);
            using (var pen = new Pen(Color.FromArgb(90, 255, 80, 95), 1f))
                for (int i = 0; i < 5; i++) g.DrawLine(pen, 1162, 300 + i * 10, 1162 + 30 + (i * 13 % 35), 300 + i * 10);

            // chart panel
            var chart = new[] { new PointF(1244, 72), new PointF(1366, 58), new PointF(1366, 222), new PointF(1244, 230) };
            HoloPanel(g, chart);
            var rnd = new Random(7);
            var pts = Enumerable.Range(0, 18).Select(i => new PointF(1252 + i * 6.4f, 190 - (float)(Math.Abs(Math.Sin(i * 0.9)) * 50 + rnd.Next(0, 30)))).ToArray();
            using (var pen = new Pen(Color.FromArgb(60, 255, 40, 60), 4f)) g.DrawLines(pen, pts);
            using (var pen = new Pen(Color.FromArgb(230, 255, 50, 70), 1.4f)) g.DrawLines(pen, pts);
            using (var pen = new Pen(Color.FromArgb(40, 255, 255, 255), 1f))
                for (int i = 0; i < 4; i++) g.DrawLine(pen, 1250, 96 + i * 30, 1362, 94 + i * 30);

            // OBD2 gauge
            var c = new PointF(1318, 290);
            for (int i = 0; i < 3; i++)
                using (var pen = new Pen(Color.FromArgb(70 + i * 50, 229, 23, 60), 1.2f + i * 0.6f))
                    g.DrawEllipse(pen, c.X - 34 - i * 10, c.Y - 34 - i * 10, (34 + i * 10) * 2, (34 + i * 10) * 2);
            using (var pen = new Pen(Color.FromArgb(220, 255, 40, 60), 3f))
                g.DrawArc(pen, c.X - 54, c.Y - 54, 108, 108, 200, 110);
            using (var f = new Font("Segoe UI", 7f))
            using (var b = new SolidBrush(Color.FromArgb(160, 255, 255, 255)))
            using (var sf = new StringFormat { Alignment = StringAlignment.Center })
            {
                g.DrawString("OBD2", f, b, c.X, c.Y - 26, sf);
                using (var b2 = new SolidBrush(Color.FromArgb(200, 255, 50, 70))) g.DrawString("OBD2", f, b2, c.X, c.Y + 14, sf);
            }
            var conn = new[] { new PointF(c.X - 20, c.Y - 9), new PointF(c.X + 20, c.Y - 9), new PointF(c.X + 16, c.Y + 9), new PointF(c.X - 16, c.Y + 9) };
            using (var pen = new Pen(Color.FromArgb(230, 235, 235, 240), 1.6f)) g.DrawPolygon(pen, conn);
            using (var b = new SolidBrush(Color.FromArgb(220, 235, 235, 240)))
                for (int i = 0; i < 8; i++) { g.FillEllipse(b, c.X - 15 + i * 4.2f, c.Y - 5, 2, 2); g.FillEllipse(b, c.X - 13 + i * 3.6f, c.Y + 2, 2, 2); }
        }

        private static void HoloPanel(Graphics g, PointF[] quad)
        {
            using (var b = new SolidBrush(Color.FromArgb(150, 16, 6, 9))) g.FillPolygon(b, quad);
            using (var pen = new Pen(Color.FromArgb(110, 229, 23, 60), 1.2f)) g.DrawPolygon(pen, quad);
            using (var pen = new Pen(Color.FromArgb(30, 229, 23, 60), 5f)) g.DrawPolygon(pen, quad);
        }

        /// <summary>Wireframe of the loaded model for the holographic panel (red X-ray style).</summary>
        private void BuildHolo()
        {
            var mesh = AppState.Instance.VehicleMesh;
            if (mesh == null) return;
            var bmp = new Bitmap(240, 130, PixelFormat.Format32bppPArgb);
            using (var g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.Transparent);
                Theme.Setup(g);
                var r = new Renderer();
                var cam = new Camera { Yaw = (float)(Math.PI / 2) - 0.75f, Pitch = 0.3f, Distance = 6.2f, FovDegrees = 30f, Target = new Vec3(0, 0.6f, 0) };
                r.BeginFrame(new Rectangle(0, 0, 240, 130), cam, ViewMode.ThreeD);
                var style = new RenderStyle
                {
                    BodyFill = Color.FromArgb(120, 10, 22), BodyEdge = Color.FromArgb(255, 70, 90),
                    GlassFill = Color.FromArgb(90, 12, 20), GlassEdge = Color.FromArgb(255, 130, 140),
                    WheelFill = Color.FromArgb(50, 10, 14), WheelEdge = Color.FromArgb(255, 90, 100),
                    DetailEdge = Color.FromArgb(255, 60, 80), FillOpacity = 0.7f
                };
                r.DrawMesh(g, mesh, ViewMode.ThreeD, style);
                var engine = r.Project(new Vec3(1.5f, 0.75f, 0f));
                using (var gp = new GraphicsPath())
                {
                    gp.AddEllipse(engine.X - 34, engine.Y - 22, 68, 44);
                    using (var pb = new PathGradientBrush(gp) { CenterColor = Color.FromArgb(170, 255, 40, 60), SurroundColors = new[] { Color.FromArgb(0, 255, 40, 60) } })
                        g.FillPath(pb, gp);
                }
            }
            _holo?.Dispose();
            _holo = bmp;
        }

        private void DrawHoloContent(Graphics g)
        {
            if (_holo == null) return;
            float pulse = 0.85f + 0.15f * (float)Math.Sin((DateTime.Now - _start).TotalSeconds * 3);
            DrawWithAlpha(g, _holo, new Rectangle(918, 160, 240, 130), pulse);
        }

        // ---- the car with a wet-floor reflection ----

        private void DrawCar(Graphics g)
        {
            RadialGlow(g, new PointF(CarRect.X + CarRect.Width * 0.5f, 600), 420, 70, Color.FromArgb(150, 229, 23, 60));
            if (!_meshReady) return;
            var car = CarImages.Get(CarShot.Splash, CarRect.Size);
            if (car == null) return;
            if (_carShownAt == DateTime.MinValue) _carShownAt = DateTime.Now;
            float a = (float)Math.Min(1.0, (DateTime.Now - _carShownAt).TotalSeconds / 0.6);
            if (!ReferenceEquals(_reflectionSource, car)) { _reflection?.Dispose(); _reflection = BuildReflection(car); _reflectionSource = car; }
            DrawWithAlpha(g, _reflection, new Rectangle(CarRect.X, CarRect.Bottom - 6, CarRect.Width, _reflection.Height), a);
            DrawWithAlpha(g, car, CarRect, a);
        }

        private static Bitmap BuildReflection(Bitmap car)
        {
            int h = car.Height / 2;
            var bmp = new Bitmap(car.Width, h, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.Transparent);
                g.DrawImage(car, new Rectangle(0, h, car.Width, -car.Height)); // vertical flip, top half of the flipped image
            }
            var data = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height), ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
            try
            {
                var row = new byte[data.Stride];
                for (int y = 0; y < bmp.Height; y++)
                {
                    float k = 0.38f * (1f - (float)y / bmp.Height);
                    var ptr = data.Scan0 + y * data.Stride;
                    System.Runtime.InteropServices.Marshal.Copy(ptr, row, 0, row.Length);
                    for (int x = 3; x < bmp.Width * 4; x += 4) row[x] = (byte)(row[x] * k);
                    System.Runtime.InteropServices.Marshal.Copy(row, 0, ptr, row.Length);
                }
            }
            finally { bmp.UnlockBits(data); }
            return bmp;
        }

        // ---- brand, tagline, features ----

        private void DrawBrand(Graphics g)
        {
            Icons.Logo(g, new RectangleF(132, 102, 96, 96), Theme.Red);

            using (var fam = new FontFamily("Segoe UI"))
            using (var path = new GraphicsPath())
            {
                path.AddString("REDLINE", fam, (int)(FontStyle.Bold | FontStyle.Italic), 66f, new PointF(250, 96), StringFormat.GenericTypographic);
                using (var shadow = new SolidBrush(Color.FromArgb(120, 0, 0, 0)))
                {
                    var m = new Matrix(); m.Translate(3, 4);
                    using (var sp = (GraphicsPath)path.Clone()) { sp.Transform(m); g.FillPath(shadow, sp); }
                }
                using (var b = new LinearGradientBrush(new RectangleF(250, 100, 10, 66), Color.White, Color.FromArgb(150, 156, 168), LinearGradientMode.Vertical))
                {
                    b.InterpolationColors = new ColorBlend
                    {
                        Colors = new[] { Color.FromArgb(250, 250, 252), Color.FromArgb(214, 218, 226), Color.FromArgb(150, 156, 168), Color.FromArgb(235, 238, 242) },
                        Positions = new[] { 0f, 0.45f, 0.62f, 1f }
                    };
                    g.FillPath(b, path);
                }
            }
            using (var f = new Font("Segoe UI Semibold", 17f, FontStyle.Italic))
                DrawSpaced(g, "DIAGNOSTICS", f, Color.FromArgb(215, 220, 228), new PointF(262, 164), 12.5f);

            using (var b = new LinearGradientBrush(new RectangleF(82, 199, 500, 2), Theme.Red, Color.FromArgb(0, 200, 205, 215), LinearGradientMode.Horizontal))
            {
                b.InterpolationColors = new ColorBlend { Colors = new[] { Color.FromArgb(0, 229, 23, 60), Theme.RedBright, Color.FromArgb(220, 210, 214, 222), Color.FromArgb(0, 210, 214, 222) }, Positions = new[] { 0f, 0.2f, 0.55f, 1f } };
                g.FillRectangle(b, 82, 199, 500, 2);
            }

            using (var f = Theme.Font(12f))
                DrawSpaced(g, Loc.T("splash.tagline"), f, Color.FromArgb(235, 238, 244), new PointF(84, 229), Loc.Current == Language.EN ? 2.6f : 1.5f);
            using (var f = Theme.Font(10.5f))
            {
                var words = Loc.T("splash.words");
                Theme.DrawText(g, words, f, Color.FromArgb(200, 205, 214), new Point(84, 264));
            }
            using (var pen = new Pen(Color.FromArgb(40, 255, 255, 255), 1f))
                g.DrawLine(pen, 84, 306, 520, 306);
        }

        private void DrawFeatures(Graphics g)
        {
            string[] icons = { "vehicle", "diagnose", "settings", "reports" };
            float[] centers = { 118, 218, 318, 422 };
            for (int i = 0; i < 4; i++)
            {
                float cx = centers[i];
                Icons.Draw(g, icons[i], new RectangleF(cx - 16, 322, 32, 32), Theme.RedBright, 1.9f);
                var lines = Loc.T("splash.f" + (i + 1)).Split('\n');
                using (var f = Theme.Font(8.5f))
                    for (int k = 0; k < lines.Length; k++)
                        Theme.DrawText(g, lines[k], f, Color.FromArgb(215, 220, 228), new Rectangle((int)cx - 50, 364 + k * 16, 100, 16), TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                if (i < 3)
                    using (var pen = new Pen(Color.FromArgb(45, 255, 255, 255), 1f))
                        g.DrawLine(pen, (cx + centers[i + 1]) / 2, 326, (cx + centers[i + 1]) / 2, 392);
            }
        }

        private void DrawProgress(Graphics g)
        {
            using (var f = Theme.Font(12.5f))
                Theme.DrawText(g, Loc.T(_statusKey), f, Color.FromArgb(240, 242, 246), new Point(84, 458));
            var track = new RectangleF(84, 500, 424, 6);
            Theme.FillRounded(g, track, 3, Color.FromArgb(46, 48, 56));
            if (_progress > 0.005f)
            {
                var fill = new RectangleF(track.X, track.Y, Math.Max(6, track.Width * _progress), track.Height);
                Theme.Glow(g, fill, 3, Theme.Red, 5, 150);
                using (var path = Theme.RoundedRect(fill, 3))
                using (var b = new LinearGradientBrush(RectangleF.Inflate(fill, 1, 1), Theme.RedDark, Theme.RedBright, LinearGradientMode.Horizontal))
                    g.FillPath(b, path);
                Theme.GlowDot(g, new PointF(fill.Right - 2, fill.Y + 3), 2.5f, Color.FromArgb(255, 150, 160), 3);
            }
            float blink = 0.55f + 0.45f * (float)Math.Abs(Math.Sin((DateTime.Now - _start).TotalSeconds * 4));
            Theme.GlowDot(g, new PointF(92, 537), 3.5f, Color.FromArgb((int)(255 * blink), 229, 23, 60), 2);
            var d = _detail();
            var detail = string.IsNullOrEmpty(d) ? Loc.T(_statusKey) : d;
            using (var f = Theme.Font(9f))
                Theme.DrawText(g, detail, f, Color.FromArgb(205, 210, 218), new Rectangle(106, 528, 520, 20), TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }

        private void DrawLanguages(Graphics g)
        {
            Icons.Draw(g, "globe", new RectangleF(84, 670, 24, 24), Color.FromArgb(220, 224, 232), 1.5f);
            string[] labels = { "English", "日本語", "中文" };
            float x = 120;
            for (int i = 0; i < 3; i++)
            {
                bool active = (int)Loc.Current == i;
                Size sz;
                using (var f = new Font(i == 0 ? "Segoe UI" : i == 1 ? "Yu Gothic UI" : "Microsoft YaHei UI", 10.5f)) sz = Theme.Measure(labels[i], f);
                var r = new RectangleF(x, 664, sz.Width + 62, 38);
                _langRects[i] = RectangleF.Inflate(r, 4, 6); // finger-sized hit area
                if (active)
                {
                    Theme.Glow(g, r, 19, Theme.Red, 5, 120);
                    using (var path = Theme.RoundedRect(r, 19))
                    using (var b = new LinearGradientBrush(RectangleF.Inflate(r, 1, 1), Theme.RedBright, Theme.Lerp(Theme.Red, Color.Black, 0.25f), LinearGradientMode.Vertical))
                        g.FillPath(b, path);
                }
                else if (_hoverLang == i) Theme.FillRounded(g, r, 19, Color.FromArgb(30, 255, 255, 255));
                DrawFlag(g, i, new RectangleF(r.X + 14, r.Y + 8, 22, 22));
                using (var f = new Font(i == 0 ? "Segoe UI" : i == 1 ? "Yu Gothic UI" : "Microsoft YaHei UI", 10.5f))
                    Theme.DrawText(g, labels[i], f, Color.White, new Rectangle((int)r.X + 44, (int)r.Y, sz.Width + 8, (int)r.Height), TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
                x = r.Right + 18;
                if (i < 2)
                    using (var pen = new Pen(Color.FromArgb(70, 255, 255, 255), 1f))
                        g.DrawLine(pen, x - 9, 670, x - 9, 696);
            }
        }

        /// <summary>Round flag badges: United Kingdom, Japan, China.</summary>
        private static void DrawFlag(Graphics g, int which, RectangleF r)
        {
            var state = g.Save();
            using (var clip = new GraphicsPath())
            {
                clip.AddEllipse(r);
                g.SetClip(clip);
                float cx = r.X + r.Width / 2, cy = r.Y + r.Height / 2;
                if (which == 0)
                {
                    using (var b = new SolidBrush(Color.FromArgb(1, 33, 105))) g.FillRectangle(b, r);
                    using (var w = new Pen(Color.White, 4.5f)) { g.DrawLine(w, r.Left, r.Top, r.Right, r.Bottom); g.DrawLine(w, r.Right, r.Top, r.Left, r.Bottom); }
                    using (var red = new Pen(Color.FromArgb(200, 16, 46), 1.5f)) { g.DrawLine(red, r.Left, r.Top, r.Right, r.Bottom); g.DrawLine(red, r.Right, r.Top, r.Left, r.Bottom); }
                    using (var b = new SolidBrush(Color.White)) { g.FillRectangle(b, cx - 3.5f, r.Top, 7, r.Height); g.FillRectangle(b, r.Left, cy - 3.5f, r.Width, 7); }
                    using (var b = new SolidBrush(Color.FromArgb(200, 16, 46))) { g.FillRectangle(b, cx - 2, r.Top, 4, r.Height); g.FillRectangle(b, r.Left, cy - 2, r.Width, 4); }
                }
                else if (which == 1)
                {
                    using (var b = new SolidBrush(Color.White)) g.FillRectangle(b, r);
                    using (var b = new SolidBrush(Color.FromArgb(188, 0, 45))) g.FillEllipse(b, cx - r.Width * 0.3f, cy - r.Height * 0.3f, r.Width * 0.6f, r.Height * 0.6f);
                }
                else
                {
                    using (var b = new SolidBrush(Color.FromArgb(238, 28, 37))) g.FillRectangle(b, r);
                    using (var b = new SolidBrush(Color.FromArgb(255, 222, 0)))
                    {
                        g.FillPolygon(b, Star(new PointF(r.X + r.Width * 0.34f, r.Y + r.Height * 0.36f), r.Width * 0.17f));
                        g.FillPolygon(b, Star(new PointF(r.X + r.Width * 0.6f, r.Y + r.Height * 0.2f), r.Width * 0.06f));
                        g.FillPolygon(b, Star(new PointF(r.X + r.Width * 0.7f, r.Y + r.Height * 0.36f), r.Width * 0.06f));
                        g.FillPolygon(b, Star(new PointF(r.X + r.Width * 0.7f, r.Y + r.Height * 0.54f), r.Width * 0.06f));
                        g.FillPolygon(b, Star(new PointF(r.X + r.Width * 0.6f, r.Y + r.Height * 0.68f), r.Width * 0.06f));
                    }
                }
            }
            g.Restore(state);
            using (var pen = new Pen(Color.FromArgb(120, 255, 255, 255), 1f)) g.DrawEllipse(pen, r);
        }

        private static PointF[] Star(PointF c, float radius)
        {
            var pts = new PointF[10];
            for (int i = 0; i < 10; i++)
            {
                double a = -Math.PI / 2 + i * Math.PI / 5;
                float rr = i % 2 == 0 ? radius : radius * 0.4f;
                pts[i] = new PointF(c.X + (float)Math.Cos(a) * rr, c.Y + (float)Math.Sin(a) * rr);
            }
            return pts;
        }

        // ---- helpers ----

        private static void DrawSpaced(Graphics g, string text, Font f, Color color, PointF at, float spacing)
        {
            float x = at.X;
            foreach (var ch in text)
            {
                var s = ch.ToString();
                Theme.DrawText(g, s, f, color, new Point((int)x, (int)at.Y));
                var w = TextRenderer.MeasureText(g, s, f, new Size(200, 100), TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix).Width;
                if (ch == ' ') w = (int)(f.SizeInPoints * 0.45f);
                x += w + spacing;
            }
        }

        private static void DrawWithAlpha(Graphics g, Image img, Rectangle dest, float alpha)
        {
            if (alpha >= 0.999f) { g.DrawImage(img, dest); return; }
            using (var ia = new ImageAttributes())
            {
                ia.SetColorMatrix(new ColorMatrix { Matrix33 = alpha });
                g.DrawImage(img, dest, 0, 0, img.Width, img.Height, GraphicsUnit.Pixel, ia);
            }
        }

        private static void RadialGlow(Graphics g, PointF c, float rx, float ry, Color centre)
        {
            using (var gp = new GraphicsPath())
            {
                gp.AddEllipse(c.X - rx, c.Y - ry, rx * 2, ry * 2);
                using (var pb = new PathGradientBrush(gp) { CenterPoint = c, CenterColor = centre, SurroundColors = new[] { Color.FromArgb(0, centre) } })
                    g.FillPath(pb, gp);
            }
        }

        private static void Neon(Graphics g, PointF a, PointF b, float width)
        {
            float[] w = { width * 4.5f, width * 2.4f, width, width * 0.4f };
            int[] al = { 20, 45, 200, 255 };
            for (int i = 0; i < w.Length; i++)
            {
                var c = i == 3 ? Color.FromArgb(al[i], 255, 150, 160) : Color.FromArgb(al[i], 235, 20, 50);
                using (var pen = new Pen(c, w[i]) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                    g.DrawLine(pen, a, b);
            }
        }

        private static void FillVertical(Graphics g, RectangleF r, Color top, Color bottom)
        {
            using (var b = new LinearGradientBrush(RectangleF.Inflate(r, 0, 1), top, bottom, LinearGradientMode.Vertical))
                g.FillRectangle(b, r);
        }

        private static PointF Lerp(PointF a, PointF b, float t) => new PointF(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t);
    }
}
