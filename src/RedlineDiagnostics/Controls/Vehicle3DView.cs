using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;
using RedlineDiagnostics.App;
using RedlineDiagnostics.Diagnostics;
using RedlineDiagnostics.Localization;
using RedlineDiagnostics.Rendering3D;

namespace RedlineDiagnostics.Controls
{
    /// <summary>
    /// The centre-stage 3D vehicle: holographic body, glowing module markers, animated data bus,
    /// scan sweep, floating callouts and the view-mode / legend / navigator overlays.
    /// </summary>
    public sealed class Vehicle3DView : BaseControl
    {
        private readonly Renderer _renderer = new Renderer();
        private readonly Camera _camera = new Camera();
        private readonly RenderStyle _style = new RenderStyle();
        private readonly Timer _timer = new Timer { Interval = 33 };
        private float _phase;
        private Point _lastMouse;
        private bool _dragging;
        private DateTime _lastDrag = DateTime.MinValue;
        private readonly Dictionary<ControlModule, PointF> _markerScreen = new Dictionary<ControlModule, PointF>();
        private readonly Dictionary<ControlModule, int> _slotAssignment = new Dictionary<ControlModule, int>();
        private int _slotSignature;
        private DateTime _slotAssignedAt = DateTime.MinValue;
        private int _frame;
        private RectangleF[] _modeRects = new RectangleF[3];
        private RectangleF _refreshRect, _navLeft, _navRight, _navUp, _navDown;

        public Mesh Mesh { get; set; }
        public IList<ControlModule> Modules { get; set; } = new List<ControlModule>();
        public ControlModule ActiveModule { get; set; }
        public ControlModule SelectedModule { get; set; }
        public ViewMode Mode { get; set; } = ViewMode.ThreeD;
        public bool AutoRotate { get; set; } = true;
        public bool ShowHarness { get; set; } = true;
        public bool Scanning { get; set; }
        public bool ShowOverlays { get; set; } = true;
        public int MaxCallouts { get; set; } = 6;

        public event Action<ControlModule> ModuleSelected;
        public event Action<ViewMode> ModeChanged;

        public Vehicle3DView()
        {
            BackColor = Theme.Background;
            _timer.Tick += (s, e) => Tick();
            _timer.Start();
        }

        // ------------------------------------------------------------------ pinch zoom (touch)

        protected override bool AllowPinchZoom => true;
        private ulong _pinchStart;
        private float _pinchDistance;

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == Touch.WM_GESTURE)
            {
                var gi = new Touch.GESTUREINFO { cbSize = System.Runtime.InteropServices.Marshal.SizeOf(typeof(Touch.GESTUREINFO)) };
                if (Touch.GetGestureInfo(m.LParam, ref gi))
                {
                    if (gi.dwID == Touch.GID_ZOOM)
                    {
                        // ullArguments holds the distance between the two fingers.
                        if ((gi.dwFlags & 1) != 0 || _pinchStart == 0) { _pinchStart = Math.Max(1UL, gi.ullArguments); _pinchDistance = _camera.Distance; }
                        else
                        {
                            float ratio = (float)_pinchStart / Math.Max(1UL, gi.ullArguments);
                            _camera.Distance = _pinchDistance * ratio;
                            _camera.Clamp();
                            _lastDrag = DateTime.Now;
                            _dragging = false; // the first finger's press must not keep orbiting
                            Invalidate();
                        }
                        if ((gi.dwFlags & 4) != 0) _pinchStart = 0; // GF_END
                        Touch.CloseGestureInfoHandle(m.LParam);
                        m.Result = IntPtr.Zero;
                        return;
                    }
                }
            }
            base.WndProc(ref m);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _timer.Dispose();
            base.Dispose(disposing);
        }

        public void ResetView()
        {
            _camera.Yaw = -0.62f;
            _camera.Pitch = 0.40f;
            _camera.Distance = 9.6f;
            Invalidate();
        }

        public void Rotate(float dYaw, float dPitch)
        {
            _camera.Yaw += dYaw;
            _camera.Pitch += dPitch;
            _camera.Clamp();
            _lastDrag = DateTime.Now;
            Invalidate();
        }

        private void Tick()
        {
            if (!Visible || Width <= 0) return;
            _phase += 0.035f;
            _frame++;
            if (AutoRotate && !_dragging && (DateTime.Now - _lastDrag).TotalSeconds > 4 && Mode != ViewMode.TwoD)
                _camera.Yaw += 0.0032f;
            Invalidate();
        }

        // ------------------------------------------------------------------ input

        protected override void OnMouseDown(MouseEventArgs e)
        {
            Focus();
            if (e.Button == MouseButtons.Left)
            {
                if (HandleOverlayClick(e.Location)) return;
                var hit = HitModule(e.Location);
                if (hit != null)
                {
                    SelectedModule = hit;
                    ModuleSelected?.Invoke(hit);
                    return;
                }
                _dragging = true;
                _lastMouse = e.Location;
            }
            base.OnMouseDown(e);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (_dragging && Mode != ViewMode.TwoD)
            {
                _camera.Yaw += (e.X - _lastMouse.X) * 0.008f;
                _camera.Pitch += (e.Y - _lastMouse.Y) * 0.006f;
                _camera.Clamp();
                _lastMouse = e.Location;
                _lastDrag = DateTime.Now;
            }
            else
            {
                Cursor = HitModule(e.Location) != null || IsOverlay(e.Location) ? Cursors.Hand : Cursors.Default;
            }
            base.OnMouseMove(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            _dragging = false;
            base.OnMouseUp(e);
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            _camera.Distance -= Math.Sign(e.Delta) * 0.6f;
            _camera.Clamp();
            _lastDrag = DateTime.Now;
            base.OnMouseWheel(e);
        }

        private ControlModule HitModule(Point p)
        {
            ControlModule best = null;
            float bestD = 26f; // finger-sized hit radius
            foreach (var kv in _markerScreen)
            {
                float dx = kv.Value.X - p.X, dy = kv.Value.Y - p.Y;
                float d = (float)Math.Sqrt(dx * dx + dy * dy);
                if (d < bestD) { bestD = d; best = kv.Key; }
            }
            return best;
        }

        private bool IsOverlay(Point p)
        {
            return _modeRects.Any(r => r.Contains(p)) || _refreshRect.Contains(p) || _navLeft.Contains(p) || _navRight.Contains(p) || _navUp.Contains(p) || _navDown.Contains(p);
        }

        private bool HandleOverlayClick(Point p)
        {
            for (int i = 0; i < 3; i++)
                if (_modeRects[i].Contains(p))
                {
                    Mode = (ViewMode)i;
                    ModeChanged?.Invoke(Mode);
                    return true;
                }
            if (_refreshRect.Contains(p)) { ResetView(); return true; }
            if (_navLeft.Contains(p)) { Rotate(-(float)Math.PI / 6, 0); return true; }
            if (_navRight.Contains(p)) { Rotate((float)Math.PI / 6, 0); return true; }
            if (_navUp.Contains(p)) { Rotate(0, 0.12f); return true; }
            if (_navDown.Contains(p)) { Rotate(0, -0.12f); return true; }
            return false;
        }

        // ------------------------------------------------------------------ painting

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            Theme.Setup(g);
            var vp = ClientRectangle;

            // background vignette
            using (var path = new GraphicsPath())
            {
                path.AddEllipse(-vp.Width * 0.2f, -vp.Height * 0.3f, vp.Width * 1.4f, vp.Height * 1.8f);
                using (var pgb = new PathGradientBrush(path))
                {
                    pgb.CenterColor = Color.FromArgb(22, 30, 58);
                    pgb.SurroundColors = new[] { Theme.Background };
                    g.FillRectangle(pgb, vp);
                }
            }

            var mesh = Mesh;
            var scene = new Rectangle(vp.X, vp.Y + 10, vp.Width, vp.Height - 40);
            _renderer.BeginFrame(scene, _camera, Mode);

            _renderer.DrawGround(g, _style, 3.15f, _phase);
            if (mesh != null) _renderer.DrawMesh(g, mesh, Mode, _style);
            if (ShowHarness) DrawHarness(g);
            if (Scanning) DrawSweep(g);
            DrawMarkers(g);
            if (ShowOverlays)
            {
                DrawCallouts(g);
                DrawViewToggle(g);
                DrawLegend(g);
                DrawNavigator(g);
            }
        }

        private static Color ModuleColor(ControlModule m)
        {
            return Theme.StatusColor(m.Status);
        }

        private void DrawSweep(Graphics g)
        {
            float t = (float)((Math.Sin(_phase * 0.9) + 1) / 2); // 0..1
            float x = -2.5f + 5f * t;
            _renderer.DrawSweepPlane(g, x, 1.05f, 1.55f, Theme.Cyan);
        }

        private void DrawHarness(Graphics g)
        {
            var modules = Modules;
            if (modules == null || modules.Count == 0) return;
            var gateway = modules.FirstOrDefault(m => m.Short == "GTW");
            var hub = gateway != null ? gateway.Position : new Vec3(0.5f, 0.5f, 0.3f);
            const float spineY = 0.30f;
            var old = g.SmoothingMode;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            // spine along the floor of the car
            var a = _renderer.Project(new Vec3(-2.2f, spineY, 0));
            var b = _renderer.Project(new Vec3(2.3f, spineY, 0));
            using (var pen = new Pen(Theme.WithAlpha(Theme.Cyan, 60), 4f)) g.DrawLine(pen, a, b);
            using (var pen = new Pen(Theme.WithAlpha(Theme.Cyan, 170), 1.4f) { DashStyle = DashStyle.Dash, DashOffset = -_phase * 6 }) g.DrawLine(pen, a, b);

            foreach (var m in modules)
            {
                var c = ModuleColor(m);
                bool active = m == ActiveModule;
                bool lit = m.Status != ModuleStatus.Pending;
                int alpha = active ? 230 : lit ? 150 : 70;
                var p0 = m.Position;
                var p1 = new Vec3(p0.X, spineY, p0.Z);
                var p2 = new Vec3(p0.X, spineY, 0);
                var s0 = _renderer.Project(p0);
                var s1 = _renderer.Project(p1);
                var s2 = _renderer.Project(p2);
                var col = lit ? c : Theme.Blue;
                if (active)
                    using (var pen = new Pen(Theme.WithAlpha(col, 60), 5f) { LineJoin = LineJoin.Round })
                        g.DrawLines(pen, new[] { s0, s1, s2 });
                using (var pen = new Pen(Theme.WithAlpha(col, alpha), active ? 1.8f : 1.1f) { LineJoin = LineJoin.Round })
                    g.DrawLines(pen, new[] { s0, s1, s2 });

                if (active && Scanning)
                {
                    // packets travelling hub -> module and back
                    var hubS = _renderer.Project(new Vec3(hub.X, spineY, 0));
                    var path = new[] { hubS, s2, s1, s0 };
                    for (int k = 0; k < 3; k++)
                    {
                        float t = (_phase * 0.55f + k * 0.33f) % 1f;
                        var pt = AlongPath(path, t);
                        Theme.GlowDot(g, pt, 3f, Theme.Cyan, 3);
                        var pt2 = AlongPath(path, 1f - t);
                        Theme.GlowDot(g, pt2, 2.2f, Theme.WithAlpha(Color.White, 200), 2);
                    }
                }
            }
            g.SmoothingMode = old;
        }

        private static PointF AlongPath(PointF[] path, float t)
        {
            float total = 0;
            var lens = new float[path.Length - 1];
            for (int i = 0; i < lens.Length; i++)
            {
                lens[i] = Dist(path[i], path[i + 1]);
                total += lens[i];
            }
            float target = t * total;
            for (int i = 0; i < lens.Length; i++)
            {
                if (target <= lens[i] || i == lens.Length - 1)
                {
                    float f = lens[i] < 1e-3f ? 0 : target / lens[i];
                    f = Math.Max(0, Math.Min(1, f));
                    return new PointF(path[i].X + (path[i + 1].X - path[i].X) * f, path[i].Y + (path[i + 1].Y - path[i].Y) * f);
                }
                target -= lens[i];
            }
            return path[path.Length - 1];
        }

        private static float Dist(PointF a, PointF b)
        {
            float dx = a.X - b.X, dy = a.Y - b.Y;
            return (float)Math.Sqrt(dx * dx + dy * dy);
        }

        private void DrawMarkers(Graphics g)
        {
            _markerScreen.Clear();
            var modules = Modules;
            if (modules == null) return;
            var ordered = modules.Select(m =>
            {
                PointF p; float d;
                bool ok = _renderer.Project(m.Position, out p, out d);
                return new { M = m, P = p, D = d, Ok = ok };
            }).Where(x => x.Ok).OrderByDescending(x => x.D).ToList();

            foreach (var x in ordered)
            {
                var m = x.M;
                bool active = m == ActiveModule;
                bool selected = m == SelectedModule;
                var c = ModuleColor(m);
                float size = active ? 0.24f : selected ? 0.21f : 0.16f;
                float glow = active ? 1.4f + 0.5f * (float)Math.Sin(_phase * 3) : m.Status != ModuleStatus.Pending ? 0.8f : 0.3f;
                if (active) size += 0.02f * (float)Math.Sin(_phase * 3);
                _renderer.DrawCube(g, m.Position, size, c, glow);
                _markerScreen[m] = x.P;

                if (active || selected)
                {
                    // pulsing ring on the floor beneath the module
                    float r = 0.35f + 0.12f * (float)((Math.Sin(_phase * 2.2) + 1) / 2);
                    var pts = new PointF[24];
                    for (int i = 0; i < 24; i++)
                    {
                        double a = Math.PI * 2 * i / 24;
                        pts[i] = _renderer.Project(new Vec3(m.Position.X + (float)Math.Cos(a) * r, 0.02f, m.Position.Z + (float)Math.Sin(a) * r));
                    }
                    using (var pen = new Pen(Theme.WithAlpha(c, active ? 150 : 90), 1.4f))
                        g.DrawPolygon(pen, pts);
                }
            }
        }

        // ------------------------------------------------------------------ callouts

        private RectangleF[] Slots()
        {
            float W = Width, H = Height;
            const float w = 200, h = 50;
            // Eight non-overlapping label slots around the viewport edge (top row, middle sides, bottom row).
            return new[]
            {
                new RectangleF(24, 58, w, h),
                new RectangleF((W - w) / 2, 44, w, h),
                new RectangleF(W - w - 24, 58, w, h),
                new RectangleF(24, H / 2 - 40, w, h),
                new RectangleF(W - w - 24, H / 2 - 40, w, h),
                new RectangleF(40, H - 120, w, h),
                new RectangleF((W - w) / 2, H - 128, w, h),
                new RectangleF(W - w - 120, H - 120, w, h),
            };
        }

        private void DrawCallouts(Graphics g)
        {
            var modules = Modules;
            if (modules == null) return;
            var candidates = modules
                .Where(m => _markerScreen.ContainsKey(m))
                .Where(m => m == ActiveModule || m == SelectedModule || m.Status != ModuleStatus.Pending)
                .OrderByDescending(m => m == ActiveModule ? 3 : m == SelectedModule ? 2 : 0)
                .ThenByDescending(m => m.StartedAt ?? DateTime.MinValue)
                .Take(MaxCallouts)
                .ToList();

            // include the next pending module as "Pending" for context, like the reference design
            if (candidates.Count < MaxCallouts)
            {
                var pend = modules.FirstOrDefault(m => m.Status == ModuleStatus.Pending && _markerScreen.ContainsKey(m) && !candidates.Contains(m));
                if (pend != null) candidates.Add(pend);
            }

            var slots = Slots();
            // Re-assign label slots when the candidate set changes or every half second (keeps labels stable while rotating).
            int signature = 17;
            foreach (var m in candidates) signature = signature * 31 + m.Definition.Id;
            bool stale = signature != _slotSignature || (DateTime.Now - _slotAssignedAt).TotalMilliseconds > 500 || _slotAssignment.Count == 0;
            if (stale)
            {
                _slotSignature = signature;
                _slotAssignedAt = DateTime.Now;
                var used = new HashSet<int>();
                var assign = new Dictionary<ControlModule, int>();
                foreach (var m in candidates)
                {
                    var p = _markerScreen[m];
                    int best = -1; float bestD = float.MaxValue;
                    for (int i = 0; i < slots.Length; i++)
                    {
                        if (used.Contains(i)) continue;
                        var c = new PointF(slots[i].X + slots[i].Width / 2, slots[i].Y + slots[i].Height / 2);
                        float d = Dist(c, p);
                        if (d < bestD) { bestD = d; best = i; }
                    }
                    if (best >= 0) { used.Add(best); assign[m] = best; }
                }
                _slotAssignment.Clear();
                foreach (var kv in assign) _slotAssignment[kv.Key] = kv.Value;
            }

            foreach (var m in candidates)
            {
                int slot;
                if (!_slotAssignment.TryGetValue(m, out slot)) continue;
                DrawCallout(g, m, slots[slot], _markerScreen[m]);
            }
        }

        private void DrawCallout(Graphics g, ControlModule m, RectangleF box, PointF marker)
        {
            var c = ModuleColor(m);
            bool active = m == ActiveModule;

            // connector: from nearest box edge midpoint to marker with an elbow
            var cx = box.X + box.Width / 2;
            var cy = box.Y + box.Height / 2;
            PointF start;
            if (marker.Y > box.Bottom + 10) start = new PointF(Math.Max(box.X + 10, Math.Min(box.Right - 10, marker.X)), box.Bottom);
            else if (marker.Y < box.Y - 10) start = new PointF(Math.Max(box.X + 10, Math.Min(box.Right - 10, marker.X)), box.Y);
            else start = marker.X > cx ? new PointF(box.Right, cy) : new PointF(box.X, cy);
            var elbow = new PointF(start.X, marker.Y - (marker.Y - start.Y) * 0.35f);
            if (Math.Abs(start.X - marker.X) < 4) elbow = start;
            using (var pen = new Pen(Theme.WithAlpha(c, 190), 1.2f))
                g.DrawLines(pen, new[] { start, elbow, marker });
            Theme.GlowDot(g, marker, 3f, c, 2);

            // box
            Theme.FillRounded(g, box, 8, Theme.WithAlpha(Theme.Card, 235));
            Theme.DrawRounded(g, box, 8, Theme.WithAlpha(c, active ? 230 : 170), active ? 1.5f : 1f);
            if (active) Theme.Glow(g, box, 8, c, 5, 90);

            var badge = new RectangleF(box.X + 8, box.Y + 11, 28, 28);
            if (m.Status == ModuleStatus.Pending || m.Status == ModuleStatus.Scanning)
                Icons.Badge(g, m.Icon, badge, c);
            else
                Icons.StatusDot(g, RectangleF.Inflate(badge, -3, -3), m.Status);

            using (var fb = F(9f, true))
            using (var fs = F(8.5f))
            {
                var textRect = new Rectangle((int)box.X + 44, (int)box.Y + 6, (int)box.Width - 50, 20);
                var label = m.Name + " (" + m.Short + ")";
                if (Theme.Measure(label, fb).Width > textRect.Width) label = m.Name;
                Theme.DrawText(g, label, fb, Theme.Text, textRect, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
                Theme.DrawText(g, m.StatusText, fs, c, new Rectangle((int)box.X + 44, (int)box.Y + 26, (int)box.Width - 50, 18), TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
            }
        }

        // ------------------------------------------------------------------ overlays

        private void DrawViewToggle(Graphics g)
        {
            float x = 16, y = Height - 48, h = 34;
            var group = new RectangleF(x, y, 252, h);
            Theme.FillRounded(g, group, 8, Theme.WithAlpha(Theme.Card, 230));
            Theme.DrawRounded(g, group, 8, Theme.Border);
            Icons.Draw(g, "cube", new RectangleF(x + 10, y + 8, 18, 18), Theme.Red, 1.5f);
            string[] keys = { "view.3d", "view.2d", "view.xray" };
            float sx = x + 38;
            using (var f = F(9f, true))
            {
                for (int i = 0; i < 3; i++)
                {
                    var label = Loc.T(keys[i]);
                    float w = Theme.Measure(label, f).Width + 22;
                    _modeRects[i] = new RectangleF(sx, y + 4, w, h - 8);
                    bool on = (int)Mode == i;
                    if (on) Theme.FillRounded(g, _modeRects[i], 6, Theme.Red);
                    Theme.DrawText(g, label, f, on ? Color.White : Theme.TextMuted, Rectangle.Round(_modeRects[i]), TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                    sx += w + 2;
                }
            }
            using (var pen = new Pen(Theme.Border, 1f)) g.DrawLine(pen, sx + 6, y + 8, sx + 6, y + h - 8);
            _refreshRect = new RectangleF(sx + 12, y + 5, 26, h - 10);
            Icons.Draw(g, "refresh", new RectangleF(sx + 16, y + 9, 16, 16), Theme.TextMuted, 1.5f);
        }

        private void DrawLegend(Graphics g)
        {
            var items = new[]
            {
                new { K = "legend.scanning", C = Theme.Cyan },
                new { K = "legend.passed", C = Theme.Green },
                new { K = "legend.warning", C = Theme.Orange },
                new { K = "legend.fault", C = Theme.Red },
                new { K = "legend.pending", C = Theme.Grey },
            };
            using (var f = F(8.5f))
            {
                float total = 0;
                var widths = new float[items.Length];
                for (int i = 0; i < items.Length; i++)
                {
                    widths[i] = Theme.Measure(Loc.T(items[i].K), f).Width + 30;
                    total += widths[i];
                }
                float w = total + 16, h = 34;
                float x = (Width - w) / 2 + 40, y = Height - 48;
                var r = new RectangleF(x, y, w, h);
                Theme.FillRounded(g, r, 8, Theme.WithAlpha(Theme.Card, 230));
                Theme.DrawRounded(g, r, 8, Theme.Border);
                float cx = x + 12;
                for (int i = 0; i < items.Length; i++)
                {
                    using (var b = new SolidBrush(items[i].C)) g.FillRectangle(b, cx, y + 13, 9, 9);
                    Theme.DrawText(g, Loc.T(items[i].K), f, Theme.TextMuted, new Point((int)cx + 15, (int)y + 10));
                    cx += widths[i];
                }
            }
        }

        private void DrawNavigator(Graphics g)
        {
            float d = 84;
            float x = Width - d - 18, y = Height - d - 20;
            var r = new RectangleF(x, y, d, d);
            var old = g.SmoothingMode;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var b = new SolidBrush(Theme.WithAlpha(Theme.Card, 235))) g.FillEllipse(b, r);
            using (var pen = new Pen(Theme.Border, 1f)) g.DrawEllipse(pen, r);
            using (var pen = new Pen(Theme.WithAlpha(Theme.Cyan, 40), 1f)) g.DrawEllipse(pen, RectangleF.Inflate(r, -12, -12));
            // tiny car top view rotated with the camera
            var cx = x + d / 2; var cy = y + d / 2;
            var st = g.Save();
            g.TranslateTransform(cx, cy);
            g.RotateTransform((float)(-_camera.Yaw * 180 / Math.PI) + 90);
            Icons.Draw(g, "car-top", new RectangleF(-11, -16, 22, 32), Theme.Text, 1.3f);
            g.Restore(st);
            _navLeft = new RectangleF(x - 2, cy - 10, 18, 20);
            _navRight = new RectangleF(x + d - 16, cy - 10, 18, 20);
            _navUp = new RectangleF(cx - 10, y - 2, 20, 18);
            _navDown = new RectangleF(cx - 10, y + d - 16, 20, 18);
            Icons.Draw(g, "back", new RectangleF(x + 2, cy - 7, 12, 14), Theme.TextMuted, 1.5f);
            Icons.Draw(g, "forward", new RectangleF(x + d - 14, cy - 7, 12, 14), Theme.TextMuted, 1.5f);
            Icons.Draw(g, "up", new RectangleF(cx - 7, y + 2, 14, 12), Theme.TextMuted, 1.5f);
            Icons.Draw(g, "down", new RectangleF(cx - 7, y + d - 14, 14, 12), Theme.TextMuted, 1.5f);
            g.SmoothingMode = old;
        }
    }
}
