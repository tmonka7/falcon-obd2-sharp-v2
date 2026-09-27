using System;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;
using RedlineDiagnostics.App;
using RedlineDiagnostics.Controls;
using RedlineDiagnostics.Forms.Pages;
using RedlineDiagnostics.Localization;

namespace RedlineDiagnostics.Forms
{
    /// <summary>Borderless 1366x768 shell: top bar, side navigation and the page host.</summary>
    public sealed class MainForm : Form
    {
        public const int DesignWidth = 1366;
        public const int DesignHeight = 768;

        private readonly TopBar _topBar;
        private readonly SideNav _nav;
        private readonly Panel _host;
        private readonly Panel _canvas;
        private readonly BasePage[] _pages;
        private int _current = -1;

        public MainForm()
        {
            AppState.Instance.Ui = SynchronizationContext.Current ?? new WindowsFormsSynchronizationContext();

            Text = Loc.T("app.title");
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            ClientSize = new Size(DesignWidth, DesignHeight);
            BackColor = Color.Black;
            AutoScaleMode = AutoScaleMode.None;
            DoubleBuffered = true;
            KeyPreview = true;
            Icon = MakeIcon();

            _topBar = new TopBar { Bounds = new Rectangle(0, 0, DesignWidth, 56) };
            _topBar.BackClicked += () => ShowPage(0);
            _topBar.ConnectClicked += () =>
            {
                var st = AppState.Instance;
                if (st.IsConnected) st.Disconnect(); else st.ConnectAsync();
            };
            _nav = new SideNav { Bounds = new Rectangle(0, 56, 160, DesignHeight - 56) };
            _nav.Navigate += ShowPage;
            _host = new Panel { Bounds = new Rectangle(160, 56, BasePage.PageWidth, BasePage.PageHeight), BackColor = Theme.Background };

            var settings = new SettingsPage();
            settings.ExitRequested += () => Close();
            settings.FullScreenChanged += PositionWindow;
            _pages = new BasePage[]
            {
                new HomePage(), new DiagnosePage(), new LiveDataPage(), new VehiclePage(),
                new ReportsPage(), new HistoryPage(), new GaragePage(), settings
            };
            foreach (var p in _pages)
            {
                p.Visible = false;
                p.Location = Point.Empty;
                p.NavigateRequested += OnPageNavigate;
                p.TitleChanged += UpdateTitle;
                _host.Controls.Add(p);
            }
            // Fixed 1366x768 design canvas. In full-screen mode the form covers the whole display and the
            // canvas is centred on a black backdrop, so the layout is identical on every screen size.
            _canvas = new Panel { Bounds = new Rectangle(0, 0, DesignWidth, DesignHeight), BackColor = Theme.Background };
            _canvas.Controls.Add(_host);
            _canvas.Controls.Add(_nav);
            _canvas.Controls.Add(_topBar);
            Controls.Add(_canvas);

            var st0 = AppState.Instance;
            st0.ConnectionChanged += () => { UpdateTitle(); _topBar.Invalidate(); };
            st0.VehicleChanged += UpdateTitle;
            Loc.LanguageChanged += OnLanguageChanged;

            PositionWindow();
            ShowPage(0);
        }

        /// <summary>When set, the form runs an unattended scripted session off-screen and writes PNG captures here.</summary>
        public string AutoTestDir { get; set; }

        /// <summary>With <see cref="AutoTestDir"/>: capture only the Home page (EN / JA / ZH) and exit.</summary>
        public bool AutoTestQuick { get; set; }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            if (!string.IsNullOrEmpty(AutoTestDir))
            {
                RunAutoTest();
                return;
            }
            Activate();
            // Auto-connect the simulator so the demo is live immediately.
            if (AppState.Instance.Settings.Adapter == Obd.AdapterType.Simulator) AppState.Instance.ConnectAsync();
        }

        private void RunAutoTest()
        {
            var st = AppState.Instance;
            st.Settings.Adapter = Obd.AdapterType.Simulator;
            PositionWindow();
            Location = new Point(Screen.PrimaryScreen.Bounds.Right + 20, 0);
            System.IO.Directory.CreateDirectory(AutoTestDir);
            st.ConnectAsync();

            var steps = new System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<int, Action>>();
            Action<int, Action> at = (ms, a) => steps.Add(new System.Collections.Generic.KeyValuePair<int, Action>(ms, a));
            Action<string> shot = name =>
            {
                try
                {
                    using (var bmp = new Bitmap(DesignWidth, DesignHeight))
                    {
                        _canvas.DrawToBitmap(bmp, new Rectangle(0, 0, DesignWidth, DesignHeight));
                        // DrawToBitmap paints nested children back-to-front incorrectly; re-paint the page's buttons on top.
                        if (_current >= 0)
                        {
                            var page = _pages[_current];
                            foreach (Control c in page.Controls)
                            {
                                if (!(c is NeonButton) || !c.Visible) continue;
                                var r = new Rectangle(_host.Left + page.Left + c.Left, _host.Top + page.Top + c.Top, c.Width, c.Height);
                                c.DrawToBitmap(bmp, r);
                            }
                        }
                        bmp.Save(System.IO.Path.Combine(AutoTestDir, name + ".png"), System.Drawing.Imaging.ImageFormat.Png);
                    }
                }
                catch (Exception ex) { st.Log("shot failed: " + ex.Message); }
            };

            var diag = (DiagnosePage)_pages[1];
            if (AutoTestQuick)
            {
                at(4000, () => shot("home_en"));
                at(4200, () => { Loc.Current = Language.JA; });
                at(5200, () => shot("home_ja"));
                at(5400, () => { Loc.Current = Language.ZH; });
                at(6400, () => shot("home_zh"));
                at(6600, () => { Loc.Current = Language.EN; ShowPage(1); });
                at(7400, () => shot("diagnose"));
                at(7600, () => Close());
            }
            else
            {
            at(4000, () => shot("01_home"));
            at(4500, () => { ShowPage(1); diag.StartScanIfPossible(); });
            at(12000, () => shot("02_scan"));
            at(24000, () => shot("03_scan_mid"));
            at(46000, () => shot("04_scan_done"));
            at(47000, () => { Loc.Current = Language.JA; });
            at(48500, () => shot("05_ja"));
            at(49000, () => { Loc.Current = Language.ZH; });
            at(50500, () => shot("06_zh"));
            at(51000, () => { Loc.Current = Language.EN; diag.SetViewMode(Rendering3D.ViewMode.XRay); });
            at(52500, () => shot("07_xray"));
            at(53000, () => diag.SetViewMode(Rendering3D.ViewMode.TwoD));
            at(54500, () => shot("08_2d"));
            at(55000, () => { diag.SetViewMode(Rendering3D.ViewMode.ThreeD); ShowPage(4); ((ReportsPage)_pages[4]).GenerateSilent(); });
            at(58000, () => shot("09_reports"));
            at(58500, () => { ShowPage(2); ((LiveDataPage)_pages[2]).StartMonitoring(); });
            at(65000, () => shot("10_live"));
            at(65500, () => ShowPage(5));
            at(67000, () => shot("11_history"));
            at(67500, () => ShowPage(7));
            at(69000, () => shot("12_settings"));
            at(69500, () => ShowPage(6));
            at(71000, () => shot("13_garage"));
            at(71500, () => { ShowPage(3); ((VehiclePage)_pages[3]).ReadFromEcu(); });
            at(75000, () => shot("14_vehicle"));
            at(76000, () => Close());
            }

            var timer = new System.Windows.Forms.Timer { Interval = 100 };
            var started = DateTime.Now;
            int next = 0;
            timer.Tick += (s, e) =>
            {
                var elapsed = (DateTime.Now - started).TotalMilliseconds;
                while (next < steps.Count && steps[next].Key <= elapsed)
                {
                    try { steps[next].Value(); } catch (Exception ex) { st.Log("autotest step failed: " + ex.Message); }
                    next++;
                }
                if (next >= steps.Count) timer.Stop();
            };
            timer.Start();
        }

        private void PositionWindow()
        {
            var screen = Screen.PrimaryScreen.Bounds;
            bool full = AppState.Instance.Settings.FullScreen && string.IsNullOrEmpty(AutoTestDir);
            MinimumSize = MaximumSize = Size.Empty;
            if (full)
            {
                // A borderless window that exactly covers the monitor is treated by Windows as a
                // full-screen application: the taskbar drops behind it without needing TopMost.
                Bounds = screen;
                _canvas.Location = new Point(Math.Max(0, (Width - DesignWidth) / 2), Math.Max(0, (Height - DesignHeight) / 2));
            }
            else
            {
                ClientSize = new Size(DesignWidth, DesignHeight);
                Location = new Point(screen.X + Math.Max(0, (screen.Width - DesignWidth) / 2), screen.Y + Math.Max(0, (screen.Height - DesignHeight) / 2));
                _canvas.Location = Point.Empty;
            }
            MinimumSize = MaximumSize = Size;
            TopMost = false;
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (_canvas != null)
                _canvas.Location = new Point(Math.Max(0, (Width - DesignWidth) / 2), Math.Max(0, (Height - DesignHeight) / 2));
        }

        private void OnPageNavigate(int index)
        {
            ShowPage(index);
            if (index == 1)
            {
                var diag = _pages[1] as DiagnosePage;
                diag?.StartScanIfPossible();
            }
        }

        private void ShowPage(int index)
        {
            if (index < 0 || index >= _pages.Length) return;
            if (_current == index) { UpdateTitle(); return; }
            if (_current >= 0)
            {
                _pages[_current].OnHidden();
                _pages[_current].Visible = false;
            }
            _current = index;
            _nav.SelectedIndex = index;
            var page = _pages[index];
            page.Visible = true;
            page.BringToFront();
            page.OnShown();
            UpdateTitle();
        }

        private void UpdateTitle()
        {
            if (_current < 0) return;
            var page = _pages[_current];
            _topBar.Title = Loc.T(page.TitleKey);
            _topBar.Subtitle = page.Subtitle;
            _topBar.ShowBack = _current != 0;
            _topBar.ShowTagline = _current == 0;
            _topBar.Invalidate();
        }

        private void OnLanguageChanged()
        {
            var st = AppState.Instance;
            st.Settings.Language = Loc.Code;
            st.Settings.Save();
            Text = Loc.T("app.title");
            foreach (var p in _pages) p.ApplyLocalization();
            _nav.Invalidate();
            UpdateTitle();
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
            {
                if (_current != 0) { ShowPage(0); e.Handled = true; return; }
                if (MessageBox.Show(Loc.T("settings.exit") + "?", Loc.T("common.confirm"), MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes) Close();
                e.Handled = true;
            }
            else if (e.KeyCode >= Keys.F1 && e.KeyCode <= Keys.F8)
            {
                ShowPage(e.KeyCode - Keys.F1);
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.F11)
            {
                var s = AppState.Instance.Settings;
                s.FullScreen = !s.FullScreen;
                s.Save();
                PositionWindow();
                e.Handled = true;
            }
            base.OnKeyDown(e);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            AppState.Instance.Disconnect();
            base.OnFormClosing(e);
        }

        private static Icon MakeIcon()
        {
            try
            {
                using (var bmp = new Bitmap(32, 32))
                using (var g = Graphics.FromImage(bmp))
                {
                    g.Clear(Color.Transparent);
                    Theme.Setup(g);
                    Icons.Logo(g, new RectangleF(6, 2, 28, 28), Theme.Red);
                    return Icon.FromHandle(bmp.GetHicon());
                }
            }
            catch { return null; }
        }
    }
}
