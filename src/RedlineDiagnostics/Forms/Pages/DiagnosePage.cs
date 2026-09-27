using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using RedlineDiagnostics.App;
using RedlineDiagnostics.Controls;
using RedlineDiagnostics.Diagnostics;
using RedlineDiagnostics.Localization;

namespace RedlineDiagnostics.Forms.Pages
{
    /// <summary>The "Full System Scan" screen: 3D vehicle, right diagnostic panel and module card strip.</summary>
    public sealed class DiagnosePage : BasePage
    {
        private readonly Vehicle3DView _view;
        private readonly DiagnosticAreaPanel _area;
        private readonly ModuleCardStrip _strip;
        private readonly NeonButton _scanBtn;
        private readonly NeonButton _reportBtn;
        private readonly Timer _timer = new Timer { Interval = 200 };
        private readonly Rectangle _leftPanel = new Rectangle(10, 10, 858, 520);
        private readonly Rectangle _rightPanel = new Rectangle(878, 10, 318, 520);
        private LiveDataMonitor _selMonitor;

        public override string TitleKey => "scan.fullSystem";

        public DiagnosePage()
        {
            var st = AppState.Instance;
            _view = new Vehicle3DView
            {
                Bounds = new Rectangle(_leftPanel.X + 1, _leftPanel.Y + 52, _leftPanel.Width - 2, _leftPanel.Height - 53),
                Mesh = st.VehicleMesh,
                Modules = st.Modules,
                AutoRotate = st.Settings.AutoRotate,
                ShowHarness = st.Settings.ShowHarness
            };
            _view.ModuleSelected += OnModuleSelected;
            Controls.Add(_view);

            _area = new DiagnosticAreaPanel { Bounds = _rightPanel };
            Controls.Add(_area);

            _strip = new ModuleCardStrip { Bounds = new Rectangle(10, 566, 1186, 128), Modules = st.Modules };
            _strip.ModuleSelected += OnModuleSelected;
            Controls.Add(_strip);

            _scanBtn = new NeonButton { TextKey = "scan.start", Icon = "scan", Size = new Size(140, 32), Location = new Point(_leftPanel.Right - 152, _leftPanel.Y + 62), FontSize = 9f };
            _scanBtn.Click += (s, e) => ToggleScan();
            Controls.Add(_scanBtn);
            _scanBtn.BringToFront();

            _reportBtn = new NeonButton { TextKey = "scan.viewReport", Icon = "reports", Size = new Size(140, 32), Location = new Point(_leftPanel.Right - 300, _leftPanel.Y + 62), FontSize = 9f, Filled = false, Accent = Theme.Cyan, Visible = false };
            _reportBtn.Click += (s, e) => Navigate(4);
            Controls.Add(_reportBtn);
            _reportBtn.BringToFront();

            st.ModuleChanged += OnModuleChanged;
            st.ScanStateChanged += OnScanStateChanged;
            st.ConnectionChanged += () => { UpdateButtons(); Invalidate(); };
            st.MeshChanged += () => _view.Mesh = st.VehicleMesh;
            _timer.Tick += (s, e) => { if (Visible) Invalidate(new Rectangle(_leftPanel.X, _leftPanel.Y, _leftPanel.Width, 52)); };
            _timer.Start();
            UpdateButtons();
            ApplyLocalization();
        }

        public override void OnShown()
        {
            var st = AppState.Instance;
            _view.AutoRotate = st.Settings.AutoRotate;
            _view.ShowHarness = st.Settings.ShowHarness;
            _view.Mesh = st.VehicleMesh;
            UpdateButtons();
            if (_area.Module == null)
            {
                var m = st.Modules.FirstOrDefault(x => x.Status == ModuleStatus.Scanning) ?? st.Modules.FirstOrDefault(x => x.IsDone) ?? st.Modules.FirstOrDefault();
                _area.Module = m;
                _strip.SelectedModule = m;
                _view.SelectedModule = m;
            }
        }

        public override void OnHidden()
        {
            StopSelectionMonitor();
        }

        private void ToggleScan()
        {
            var st = AppState.Instance;
            if (st.IsScanning)
            {
                st.CancelScan();
                return;
            }
            if (!st.IsConnected)
            {
                Info(Loc.T("scan.notConnected"));
                return;
            }
            StopSelectionMonitor();
            _view.SelectedModule = null;
            _strip.SelectedModule = null;
            st.StartScan();
        }

        public void StartScanIfPossible()
        {
            if (!AppState.Instance.IsScanning) ToggleScan();
        }

        public void SetViewMode(Rendering3D.ViewMode mode)
        {
            _view.Mode = mode;
            _view.Invalidate();
        }

        private void UpdateButtons()
        {
            var st = AppState.Instance;
            _scanBtn.TextKey = st.IsScanning ? "scan.stop" : (st.LastScan != null && !st.IsScanning && st.Modules.Any(m => m.IsDone) ? "scan.rescan" : "scan.start");
            _scanBtn.Icon = st.IsScanning ? "cross" : "scan";
            _scanBtn.Accent = st.IsScanning ? Theme.Grey : Theme.Red;
            _scanBtn.ApplyLocalization();
            _reportBtn.Visible = !st.IsScanning && st.LastScan != null && st.Modules.Any(m => m.IsDone);
            _view.Scanning = st.IsScanning;
        }

        private void OnScanStateChanged()
        {
            var st = AppState.Instance;
            UpdateButtons();
            if (!st.IsScanning)
            {
                _view.ActiveModule = null;
                _strip.ActiveModule = null;
                var worst = st.Modules.Where(m => m.IsDone).OrderByDescending(m => m.Status == ModuleStatus.Fault ? 2 : m.Status == ModuleStatus.Warning ? 1 : 0).FirstOrDefault();
                if (worst != null) OnModuleSelected(worst);
            }
            Invalidate();
        }

        private void OnModuleChanged(ControlModule m)
        {
            var st = AppState.Instance;
            if (m.Status == ModuleStatus.Scanning)
            {
                _view.ActiveModule = m;
                _strip.ActiveModule = m;
                _area.Module = m;
                _strip.EnsureVisible(m);
            }
            _strip.Invalidate();
            if (_area.Module == m) _area.Invalidate();
            Invalidate(new Rectangle(_leftPanel.X, _leftPanel.Y, _leftPanel.Width, 52));
        }

        private void OnModuleSelected(ControlModule m)
        {
            _view.SelectedModule = m;
            _strip.SelectedModule = m;
            _strip.EnsureVisible(m);
            _area.Module = m;
            _area.LiveOverride = null;
            _area.Invalidate();
            _strip.Invalidate();
            StartSelectionMonitor(m);
        }

        /// <summary>When idle and connected, stream the selected module's live channels into the panel.</summary>
        private void StartSelectionMonitor(ControlModule m)
        {
            StopSelectionMonitor();
            var st = AppState.Instance;
            if (!st.IsConnected || st.IsScanning || m == null || m.Status == ModuleStatus.NoResponse || m.Status == ModuleStatus.Pending) return;
            var pars = LiveParam.For(m.Short);
            if (pars.Length == 0) return;
            var target = m.Short == "ECM" ? null : m.Definition;
            _selMonitor = st.StartMonitor(pars, target);
            if (_selMonitor == null) return;
            var mon = _selMonitor;
            mon.Sample += sample =>
            {
                if (_selMonitor != mon || _area.Module != m) return;
                foreach (var kv in sample) m.LiveValues[kv.Key] = kv.Value;
                _area.Invalidate();
            };
        }

        private void StopSelectionMonitor()
        {
            if (_selMonitor != null)
            {
                AppState.Instance.StopMonitor();
                _selMonitor = null;
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            Theme.Setup(g);
            var st = AppState.Instance;

            // left panel frame + header
            var lp = new RectangleF(_leftPanel.X + 0.5f, _leftPanel.Y + 0.5f, _leftPanel.Width - 1, _leftPanel.Height - 1);
            Theme.Panel(g, lp, 12, Theme.Surface, Theme.Border);
            using (var pen = new Pen(Theme.BorderSoft, 1f))
                g.DrawLine(pen, lp.X + 1, lp.Y + 52, lp.Right - 1, lp.Y + 52);

            float hx = lp.X + 16, hy = lp.Y + 12;
            Theme.GlowDot(g, new PointF(hx + 14, hy + 14), 14, Theme.WithAlpha(Theme.Red, 40), 1);
            Icons.Draw(g, "scan", new RectangleF(hx + 4, hy + 4, 20, 20), Theme.Red, 1.7f);
            using (var f = F(12.5f, true))
                Theme.DrawText(g, Loc.T("scan.fullSystem"), f, Theme.Text, new Point((int)hx + 40, (int)hy + 4));

            int total = st.Modules.Count;
            int done = st.Modules.Count(m => m.IsDone);
            var scan = st.Scan;
            float fraction = total == 0 ? 0 : done / (float)total;
            if (st.IsScanning && scan?.Current != null) fraction += scan.Current.Progress / 100f / total;
            var elapsed = scan != null ? scan.Elapsed : (st.LastScan != null ? st.LastScan.Duration : TimeSpan.Zero);

            float bx = hx + 260;
            Theme.ProgressBar(g, new RectangleF(bx, hy + 11, 280, 8), fraction, Theme.Red, Theme.SurfaceAlt);
            using (var f = F(14f, true))
                Theme.DrawText(g, ((int)Math.Round(fraction * 100)) + "%", f, Theme.Text, new Point((int)bx + 296, (int)hy + 2));
            using (var pen = new Pen(Theme.Border, 1f))
                g.DrawLine(pen, bx + 360, hy + 2, bx + 360, hy + 28);
            using (var f = F(9.5f))
                Theme.DrawText(g, done + " / " + total + " " + Loc.T("scan.modules"), f, Theme.Text, new Point((int)bx + 376, (int)hy + 7));
            using (var pen = new Pen(Theme.Border, 1f))
                g.DrawLine(pen, bx + 500, hy + 2, bx + 500, hy + 28);
            using (var f = F(8f))
                Theme.DrawText(g, Loc.T("scan.elapsed"), f, Theme.TextMuted, new Point((int)bx + 516, (int)hy - 2));
            using (var f = F(11f, true))
                Theme.DrawText(g, elapsed.ToString(@"mm\:ss"), f, Theme.Text, new Point((int)bx + 516, (int)hy + 13));

            // status text under the scan button area
            if (!st.IsScanning)
            {
                string msg = !st.IsConnected ? Loc.T("scan.notConnected") : (st.LastScan != null && st.Modules.Any(m => m.IsDone)
                    ? Loc.T("scan.summary", st.LastScan.Scanned, st.LastScan.Faults, st.LastScan.Warnings) : Loc.T("scan.ready"));
                using (var f = F(8.5f))
                {
                    var sz = Theme.Measure(msg, f);
                    Theme.DrawText(g, msg, f, Theme.TextMuted, new Point((int)(lp.Right - 16 - sz.Width), (int)lp.Y + 100));
                }
            }

            // section label
            using (var f = F(11f, true))
                Theme.DrawText(g, Loc.T("scan.controlModules"), f, Theme.Text, new Point(12, 540));
        }
    }
}
