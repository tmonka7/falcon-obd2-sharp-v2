using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using RedlineDiagnostics.App;

namespace RedlineDiagnostics.Controls
{
    /// <summary>
    /// Touch helpers. Windows turns a finger drag into a pan gesture by default, which custom controls never see as a
    /// mouse drag. <see cref="Configure"/> switches pan (and press-and-hold / flicks) off per window so a drag arrives
    /// as ordinary mouse-down / move / up, which the scrolling and 3D orbit code handle for finger and mouse alike.
    /// Pinch zoom can stay enabled and is delivered as WM_GESTURE.
    /// </summary>
    public static class Touch
    {
        public const int WM_GESTURE = 0x0119;
        public const int GID_BEGIN = 1, GID_END = 2, GID_ZOOM = 3, GID_PAN = 4, GID_ROTATE = 5, GID_TWOFINGERTAP = 6, GID_PRESSANDTAP = 7;
        private const int GC_ALLGESTURES = 1;

        [StructLayout(LayoutKind.Sequential)]
        private struct GESTURECONFIG
        {
            public int dwID;
            public int dwWant;
            public int dwBlock;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct GESTUREINFO
        {
            public int cbSize;
            public int dwFlags;
            public int dwID;
            public IntPtr hwndTarget;
            public short ptsLocationX;
            public short ptsLocationY;
            public int dwInstanceID;
            public int dwSequenceID;
            public ulong ullArguments;
            public int cbExtraArgs;
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetGestureConfig(IntPtr hwnd, int dwReserved, int cIDs, [In] GESTURECONFIG[] pGestureConfig, int cbSize);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool GetGestureInfo(IntPtr hGestureInfo, ref GESTUREINFO pGestureInfo);

        [DllImport("user32.dll")]
        public static extern bool CloseGestureInfoHandle(IntPtr hGestureInfo);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern bool SetProp(IntPtr hWnd, string lpString, IntPtr hData);

        [DllImport("user32.dll")]
        private static extern IntPtr GetMessageExtraInfo();

        /// <summary>Disable pan / rotate / press-and-hold on the window; keep pinch zoom when requested.</summary>
        public static void Configure(IntPtr hwnd, bool allowZoom)
        {
            try
            {
                var cfg = new[]
                {
                    new GESTURECONFIG { dwID = GID_ZOOM, dwWant = allowZoom ? GC_ALLGESTURES : 0, dwBlock = allowZoom ? 0 : GC_ALLGESTURES },
                    new GESTURECONFIG { dwID = GID_PAN, dwWant = 0, dwBlock = 0x1F },
                    new GESTURECONFIG { dwID = GID_ROTATE, dwWant = 0, dwBlock = GC_ALLGESTURES },
                    new GESTURECONFIG { dwID = GID_TWOFINGERTAP, dwWant = 0, dwBlock = GC_ALLGESTURES },
                    new GESTURECONFIG { dwID = GID_PRESSANDTAP, dwWant = 0, dwBlock = GC_ALLGESTURES },
                };
                SetGestureConfig(hwnd, 0, cfg.Length, cfg, Marshal.SizeOf(typeof(GESTURECONFIG)));
                // TABLET_DISABLE_PRESSANDHOLD | TABLET_DISABLE_PENTAPFEEDBACK | TABLET_DISABLE_PENBARRELFEEDBACK | TABLET_DISABLE_FLICKS
                SetProp(hwnd, "MicrosoftTabletPenServiceProperty", (IntPtr)(0x00000001 | 0x00000008 | 0x00000010 | 0x00010000));
            }
            catch (EntryPointNotFoundException) { }
            catch (DllNotFoundException) { }
        }

        /// <summary>True when the mouse message being processed was synthesised from touch or pen input.</summary>
        public static bool IsTouchMessage()
        {
            try { return ((long)GetMessageExtraInfo() & 0xFFFFFF00) == 0xFF515700; }
            catch { return false; }
        }
    }

    /// <summary>
    /// Drag-to-scroll with inertia for one axis. A press that moves less than <see cref="Threshold"/> pixels is a tap;
    /// once it moves further it becomes a drag and the following click is suppressed.
    /// </summary>
    public sealed class TouchScroller : IDisposable
    {
        public const int Threshold = 10;
        private readonly Control _owner;
        private readonly bool _horizontal;
        private readonly Timer _timer = new Timer { Interval = 15 };
        private bool _down, _dragging;
        private Point _start;
        private int _startOffset, _lastPos;
        private DateTime _lastTime;
        private float _velocity; // px per ms, positive = content moves towards larger offsets
        private int _offset;

        /// <summary>Largest valid offset (content size minus viewport size).</summary>
        public Func<int> Max { get; set; } = () => 0;

        /// <summary>Raised whenever <see cref="Offset"/> changes.</summary>
        public event Action Scrolled;

        public TouchScroller(Control owner, bool horizontal = false)
        {
            _owner = owner;
            _horizontal = horizontal;
            _timer.Tick += (s, e) => Glide();
        }

        public int Offset => _offset;
        public bool IsDragging => _dragging;
        public bool IsPressed => _down;

        /// <summary>True after a drag until the next press; callers skip the click that ends a drag.</summary>
        public bool SuppressClick { get; private set; }

        public void SetOffset(int value)
        {
            int v = Math.Max(0, Math.Min(Math.Max(0, Max()), value));
            if (v == _offset) return;
            _offset = v;
            Scrolled?.Invoke();
        }

        public void Reset()
        {
            _timer.Stop();
            _velocity = 0;
            _offset = 0;
            Scrolled?.Invoke();
        }

        /// <summary>Re-clamp after the content or viewport size changed.</summary>
        public void Clamp() => SetOffset(_offset);

        public void Wheel(int delta, int step) => SetOffset(_offset - Math.Sign(delta) * step);

        private int Pos(Point p) => _horizontal ? p.X : p.Y;

        public void MouseDown(Point p)
        {
            _timer.Stop();
            _down = true;
            _dragging = false;
            SuppressClick = false;
            _start = p;
            _startOffset = _offset;
            _lastPos = Pos(p);
            _lastTime = DateTime.UtcNow;
            _velocity = 0;
        }

        /// <summary>Returns true while the gesture is a scroll drag (callers then ignore the move for hover / hit tests).</summary>
        public bool MouseMove(Point p)
        {
            if (!_down) return false;
            int d = Pos(p) - Pos(_start);
            int cross = _horizontal ? p.Y - _start.Y : p.X - _start.X;
            if (!_dragging && Math.Abs(d) >= Threshold && Math.Abs(d) >= Math.Abs(cross) && Max() > 0)
            {
                _dragging = true;
                _owner.Capture = true;
            }
            if (!_dragging) return false;
            SetOffset(_startOffset - d);
            var now = DateTime.UtcNow;
            double dt = Math.Max(1, (now - _lastTime).TotalMilliseconds);
            float v = (float)((_lastPos - Pos(p)) / dt);
            _velocity = _velocity * 0.4f + v * 0.6f;
            _lastPos = Pos(p);
            _lastTime = now;
            return true;
        }

        /// <summary>Ends the press; returns true when it was a drag (the click should be ignored).</summary>
        public bool MouseUp()
        {
            bool was = _dragging;
            _down = false;
            _dragging = false;
            SuppressClick = was;
            if (was)
            {
                _owner.Capture = false;
                if ((DateTime.UtcNow - _lastTime).TotalMilliseconds > 80) _velocity = 0; // finger rested before lifting
                if (Math.Abs(_velocity) > 0.15f) _timer.Start();
            }
            return was;
        }

        private void Glide()
        {
            int before = _offset;
            SetOffset(_offset + (int)Math.Round(_velocity * _timer.Interval));
            _velocity *= 0.93f;
            if (Math.Abs(_velocity) < 0.05f || _offset == before) { _timer.Stop(); _velocity = 0; }
        }

        /// <summary>Thin scroll position indicator along the right (or bottom) edge of <paramref name="viewport"/>.</summary>
        public void DrawIndicator(Graphics g, Rectangle viewport, int contentSize)
        {
            int view = _horizontal ? viewport.Width : viewport.Height;
            int max = Max();
            if (max <= 0 || contentSize <= view) return;
            float track = view - 8;
            float thumb = Math.Max(28, track * view / contentSize);
            float pos = 4 + (track - thumb) * _offset / max;
            var c = Theme.WithAlpha(Theme.TextMuted, _dragging || _timer.Enabled ? 190 : 110);
            if (_horizontal) Theme.FillRounded(g, new RectangleF(viewport.X + pos, viewport.Bottom - 5, thumb, 3), 1.5f, c);
            else Theme.FillRounded(g, new RectangleF(viewport.Right - 5, viewport.Y + pos, 3, thumb), 1.5f, c);
        }

        public void Dispose() => _timer.Dispose();
    }
}
