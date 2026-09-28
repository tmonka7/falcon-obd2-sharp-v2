using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Threading;
using RedlineDiagnostics.App;
using RedlineDiagnostics.Rendering3D;

namespace RedlineDiagnostics.Controls
{
    public enum CarShot
    {
        /// <summary>Front three-quarter view, black paint, red studio rim light (welcome card).</summary>
        Hero,
        /// <summary>Rear view with a lit tail-light bar (promo card).</summary>
        Rear,
        /// <summary>Small silver side three-quarter view (recent scan rows).</summary>
        Thumb,
        /// <summary>Red-lit side view for the navigation rail footer.</summary>
        Nav,
        /// <summary>Low front three-quarter view with red headlights (splash screen).</summary>
        Splash
    }

    /// <summary>
    /// Renders the active vehicle mesh as opaque, lit "product shots" with transparent backgrounds and caches
    /// them per size. The cache is cleared whenever the model changes.
    /// </summary>
    public static class CarImages
    {
        private static readonly Dictionary<string, Bitmap> Cache = new Dictionary<string, Bitmap>();
        private static readonly HashSet<string> Pending = new HashSet<string>();
        private static Mesh _mesh;

        /// <summary>Raised on the UI thread when a requested image has been rendered or the model changed.</summary>
        public static event Action Changed;

        static CarImages()
        {
            AppState.Instance.MeshChanged += () =>
            {
                Clear();
                Changed?.Invoke();
            };
        }

        public static void Clear()
        {
            foreach (var b in Cache.Values) b.Dispose();
            Cache.Clear();
        }

        /// <summary>
        /// Returns the cached image, or null while it is being rendered on a worker thread
        /// (<see cref="Changed"/> fires when it is ready). Call from the UI thread.
        /// </summary>
        public static Bitmap Get(CarShot shot, Size size)
        {
            var mesh = AppState.Instance.VehicleMesh;
            if (mesh != null && mesh.Detailed != null) mesh = mesh.Detailed;
            if (mesh == null || size.Width < 8 || size.Height < 8) return null;
            if (!ReferenceEquals(mesh, _mesh)) { Clear(); _mesh = mesh; }
            var key = shot + ":" + size.Width + "x" + size.Height;
            Bitmap bmp;
            if (Cache.TryGetValue(key, out bmp)) return bmp;
            var ui = SynchronizationContext.Current;
            if (ui == null)
            {
                bmp = RenderSafe(mesh, shot, size);
                if (bmp != null) Cache[key] = bmp;
                return bmp;
            }
            if (!Pending.Add(key)) return null;
            ThreadPool.QueueUserWorkItem(_ =>
            {
                var result = RenderSafe(mesh, shot, size);
                ui.Post(__ =>
                {
                    Pending.Remove(key);
                    if (result == null) return;
                    if (!ReferenceEquals(mesh, _mesh) || Cache.ContainsKey(key)) { result.Dispose(); return; }
                    Cache[key] = result;
                    Changed?.Invoke();
                }, null);
            });
            return null;
        }

        private static Bitmap RenderSafe(Mesh mesh, CarShot shot, Size size)
        {
            try { return Render(mesh, shot, size); }
            catch (Exception ex) { AppState.Instance.Log("car image failed: " + ex.Message); return null; }
        }

        /// <summary>Supersampling factor: the mesh is drawn without anti-aliasing at this scale and then downsampled.</summary>
        private const int Supersample = 2;

        public static Bitmap Render(Mesh mesh, CarShot shot, Size size)
        {
            var cam = new Camera { Target = new Vec3(0f, 0.6f, 0f), FovDegrees = 24f, Distance = 11f };
            var style = new StudioStyle();
            float margin = 4f;
            float yAlign = 0.5f; // 0 = top, 1 = bottom placement of the fitted car
            bool floor = true, headlights = false, taillights = false, bothHeadlights = false;
            var headlightColor = Color.FromArgb(235, 245, 255);
            switch (shot)
            {
                case CarShot.Hero:
                    cam.Yaw = 2.3f; cam.Pitch = 0.16f;
                    headlights = LightsEnabled;
                    yAlign = 0.62f;
                    margin = 10f;
                    break;
                case CarShot.Rear:
                    cam.Yaw = -(float)(Math.PI / 2) + 0.1f; cam.Pitch = 0.12f;
                    style.Rim = 0.8f; style.Bounce = 0.4f;
                    taillights = LightsEnabled;
                    yAlign = 0.7f;
                    margin = 8f;
                    break;
                case CarShot.Thumb:
                    cam.Yaw = 2.45f; cam.Pitch = 0.2f;
                    style.Paint = Color.FromArgb(150, 156, 168);
                    style.Glass = Color.FromArgb(24, 30, 42);
                    style.RimColor = Color.FromArgb(180, 200, 230);
                    style.Rim = 0.5f; style.Bounce = 0.15f; style.Ambient = 0.45f;
                    floor = false;
                    margin = 2f;
                    break;
                case CarShot.Splash:
                    cam.Yaw = (float)(Math.PI / 2) - 0.45f; cam.Pitch = 0.07f; cam.FovDegrees = 30f; cam.Distance = 8.5f;
                    style.Paint = Color.FromArgb(12, 12, 15);
                    style.Rim = 0.8f; style.Bounce = 0.45f; style.Specular = 0.75f; style.Sky = 0.12f; style.Ambient = 0.18f;
                    headlights = false; bothHeadlights = true; // the model's own lamp shapes read better than estimated glows
                    headlightColor = Color.FromArgb(255, 36, 60);
                    floor = false;
                    yAlign = 1f;
                    margin = 2f;
                    break;
                case CarShot.Nav:
                    cam.Yaw = 2.2f; cam.Pitch = 0.14f;
                    style.Paint = Color.FromArgb(70, 12, 22);
                    style.Rim = 0.9f; style.Bounce = 0.45f;
                    headlights = LightsEnabled;
                    yAlign = 0.65f;
                    break;
            }

            int ss = Supersample;
            var big = new Size(size.Width * ss, size.Height * ss);
            margin *= ss;
            using (var hi = new Bitmap(big.Width, big.Height, PixelFormat.Format32bppPArgb))
            {
                using (var g = Graphics.FromImage(hi))
                {
                    g.Clear(Color.Transparent);
                    Theme.Setup(g);
                    var r = new Renderer();
                    r.BeginFrame(new Rectangle(0, 0, big.Width, big.Height), cam, ViewMode.ThreeD);

                    // Fit the projected model into the image.
                    float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
                    foreach (var v in mesh.Vertices)
                    {
                        var p = r.Project(v);
                        if (p.X < minX) minX = p.X;
                        if (p.X > maxX) maxX = p.X;
                        if (p.Y < minY) minY = p.Y;
                        if (p.Y > maxY) maxY = p.Y;
                    }
                    float bw = Math.Max(1f, maxX - minX), bh = Math.Max(1f, maxY - minY);
                    float extraBottom = floor ? bh * 0.12f : 0f;
                    float s = Math.Min((big.Width - 2 * margin) / bw, (big.Height - 2 * margin) / (bh + extraBottom));
                    float drawnH = (bh + extraBottom) * s;
                    float top = margin + (big.Height - 2 * margin - drawnH) * yAlign;
                    g.TranslateTransform((big.Width - bw * s) / 2f, top);
                    g.ScaleTransform(s, s);
                    g.TranslateTransform(-minX, -minY);
                    style.PixelScale = s;

                    Vec3 bmin, bmax;
                    mesh.GetBounds(out bmin, out bmax);
                    if (floor)
                    {
                        var glow = shot == CarShot.Nav ? Color.FromArgb(150, 229, 23, 60) : Color.FromArgb(120, 229, 23, 60);
                        r.DrawFloorGlow(g, (bmax.X - bmin.X) * 0.72f, (bmax.Z - bmin.Z) * 1.1f, glow);
                        r.DrawFloorGlow(g, (bmax.X - bmin.X) * 0.5f, (bmax.Z - bmin.Z) * 0.62f, Color.FromArgb(235, 0, 0, 0));
                    }

                    r.DrawMeshStudio(g, mesh, style);

                    float h = bmax.Y - bmin.Y;
                    if (headlights)
                    {
                        // Only the headlight nearer the camera; the far one is hidden by the nose.
                        float y = bmin.Y + h * 0.5f;
                        float xs, zs;
                        SurfaceAt(mesh, true, y, h * 0.06f, out xs, out zs);
                        float z = zs * 0.72f;
                        var near = r.ToView(new Vec3(xs, y, z)).Z < r.ToView(new Vec3(xs, y, -z)).Z ? z : -z;
                        foreach (var side in bothHeadlights ? new[] { near, -near } : new[] { near })
                        {
                            float sign = Math.Sign(side);
                            LightBar(g, r, new Vec3(xs - 0.1f, y, side + 0.12f * sign), new Vec3(xs - 0.02f, y + 0.02f, side - 0.2f * sign), headlightColor, ss / s);
                        }
                    }
                    if (taillights)
                    {
                        float y = bmin.Y + h * 0.6f;
                        float xs, zs;
                        SurfaceAt(mesh, false, y, h * 0.06f, out xs, out zs);
                        LightBar(g, r, new Vec3(xs + 0.02f, y, -zs * 0.86f), new Vec3(xs + 0.02f, y, zs * 0.86f), Color.FromArgb(255, 30, 60), ss / s);
                    }
                }
                var bmp = new Bitmap(size.Width, size.Height, PixelFormat.Format32bppPArgb);
                using (var g = Graphics.FromImage(bmp))
                {
                    g.Clear(Color.Transparent);
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    g.CompositingQuality = CompositingQuality.HighQuality;
                    g.DrawImage(hi, new Rectangle(0, 0, size.Width, size.Height));
                }
                return bmp;
            }
        }

        /// <summary>Draw head / tail light glows (positions are estimated from the model bounds).</summary>
        public static bool LightsEnabled = true;

        /// <summary>
        /// Front-most (or rear-most) surface x and the half width of the body at height <paramref name="y"/>,
        /// measured from the vertices in a thin horizontal band near that end of the car.
        /// </summary>
        private static void SurfaceAt(Mesh mesh, bool front, float y, float band, out float x, out float halfWidth)
        {
            Vec3 bmin, bmax;
            mesh.GetBounds(out bmin, out bmax);
            float end = front ? bmax.X : bmin.X;
            float depth = (bmax.X - bmin.X) * 0.12f;
            x = front ? float.MinValue : float.MaxValue;
            halfWidth = 0f;
            foreach (var v in mesh.Vertices)
            {
                if (Math.Abs(v.Y - y) > band) continue;
                if (front ? v.X < end - depth : v.X > end + depth) continue;
                if (front ? v.X > x : v.X < x) x = v.X;
                halfWidth = Math.Max(halfWidth, Math.Abs(v.Z));
            }
            if (x == float.MinValue || x == float.MaxValue) x = end;
            if (halfWidth <= 0f) halfWidth = (bmax.Z - bmin.Z) * 0.4f;
        }

        private static void LightBar(Graphics g, Renderer r, Vec3 a, Vec3 b, Color color, float unit)
        {
            var pa = r.Project(a);
            var pb = r.Project(b);
            float[] widths = { 14f, 9f, 5f, 2.6f, 1.2f };
            int[] alphas = { 22, 40, 80, 190, 255 };
            for (int i = 0; i < widths.Length; i++)
            {
                var c = i == widths.Length - 1 ? Theme.Lerp(color, Color.White, 0.55f) : color;
                using (var pen = new Pen(Color.FromArgb(alphas[i], c), widths[i] * unit) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                    g.DrawLine(pen, pa, pb);
            }
        }
    }
}
