using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace RedlineDiagnostics.Rendering3D
{
    public enum ViewMode { ThreeD = 0, TwoD = 1, XRay = 2 }

    public sealed class Camera
    {
        public float Yaw = -0.62f;        // radians, around Y
        public float Pitch = 0.38f;       // radians, around X
        public float Distance = 7.6f;
        public float FovDegrees = 30f;
        public Vec3 Target = new Vec3(0f, 0.55f, 0f);

        public void Clamp()
        {
            if (Pitch < 0.05f) Pitch = 0.05f;
            if (Pitch > 1.35f) Pitch = 1.35f;
            if (Distance < 5f) Distance = 5f;
            if (Distance > 18f) Distance = 18f;
        }
    }

    /// <summary>Colours used by the holographic renderer.</summary>
    public sealed class RenderStyle
    {
        public Color BodyFill = Color.FromArgb(16, 70, 130);
        public Color BodyEdge = Color.FromArgb(34, 211, 238);
        public Color GlassFill = Color.FromArgb(40, 170, 220);
        public Color GlassEdge = Color.FromArgb(120, 230, 250);
        public Color WheelFill = Color.FromArgb(70, 80, 110);
        public Color WheelEdge = Color.FromArgb(150, 170, 200);
        public Color DetailEdge = Color.FromArgb(80, 200, 230);
        public Color XRayEdge = Color.FromArgb(255, 80, 110);
        public Color Ground = Color.FromArgb(229, 23, 60);
        public Color Grid = Color.FromArgb(34, 211, 238);
        public float FillOpacity = 1f;
    }

    /// <summary>
    /// Software 3D renderer on top of GDI+. Perspective (3D / X-Ray) or orthographic top-down (2D),
    /// painter's algorithm for translucent faces, and projection helpers for overlays.
    /// </summary>
    public sealed partial class Renderer
    {
        private Mat4 _view;
        private float _scale, _cx, _cy;
        private bool _ortho;
        private Rectangle _vp;
        private const float Near = 0.5f;

        // per-frame vertex cache
        private Vec3[] _viewVerts = new Vec3[0];
        private PointF[] _screen = new PointF[0];
        private bool[] _visible = new bool[0];
        private int[] _order = new int[0];
        private float[] _depth = new float[0];
        private readonly PointF[] _tri = new PointF[3];
        private readonly PointF[] _quad = new PointF[4];
        private sbyte[] _facing = new sbyte[0];   // per face: 1 front, -1 back, 0 not drawn

        public bool IsOrtho => _ortho;
        public Rectangle Viewport => _vp;

        public void BeginFrame(Rectangle viewport, Camera cam, ViewMode mode)
        {
            _vp = viewport;
            _cx = viewport.X + viewport.Width / 2f;
            _cy = viewport.Y + viewport.Height / 2f;
            cam.Clamp();
            if (mode == ViewMode.TwoD)
            {
                _ortho = true;
                // Top-down blueprint: rotate so +Y (up) points at the viewer, front of the car to the left.
                _view = Mat4.Translation(-cam.Target.X, 0, -cam.Target.Z) * Mat4.RotationY((float)Math.PI) * Mat4.RotationX((float)(Math.PI / 2));
                _scale = Math.Min(viewport.Width / 6.2f, viewport.Height / 3.4f);
            }
            else
            {
                _ortho = false;
                _view = Mat4.Translation(-cam.Target.X, -cam.Target.Y, -cam.Target.Z)
                        * Mat4.RotationY(cam.Yaw)
                        * Mat4.RotationX(-cam.Pitch) // positive pitch = camera above the ground, looking down
                        * Mat4.Translation(0, 0, cam.Distance);
                float f = 1f / (float)Math.Tan(cam.FovDegrees * Math.PI / 360.0);
                _scale = viewport.Height / 2f * f;
            }
        }

        public Vec3 ToView(Vec3 world) => _view.TransformPoint(world);

        /// <summary>Projects a world point; returns false when it is behind the camera.</summary>
        public bool Project(Vec3 world, out PointF p, out float depth)
        {
            var v = _view.TransformPoint(world);
            return ProjectView(v, out p, out depth);
        }

        public PointF Project(Vec3 world)
        {
            PointF p; float d;
            Project(world, out p, out d);
            return p;
        }

        private bool ProjectView(Vec3 v, out PointF p, out float depth)
        {
            if (_ortho)
            {
                p = new PointF(_cx + v.X * _scale, _cy - v.Y * _scale);
                depth = -v.Z; // higher points (larger view Z) are nearer to the top-down camera
                return true;
            }
            depth = v.Z;
            if (v.Z < Near)
            {
                p = new PointF(_cx, _cy);
                return false;
            }
            float k = _scale / v.Z;
            p = new PointF(_cx + v.X * k, _cy - v.Y * k);
            return true;
        }

        /// <summary>Approximate screen pixels per world unit at a given world position.</summary>
        public float ScaleAt(Vec3 world)
        {
            if (_ortho) return _scale;
            var v = _view.TransformPoint(world);
            return v.Z < Near ? 0 : _scale / v.Z;
        }

        // ------------------------------------------------------------------ mesh

        public void DrawMesh(Graphics g, Mesh mesh, ViewMode mode, RenderStyle style)
        {
            int n = mesh.Vertices.Count;
            if (n == 0) return;
            if (_viewVerts.Length < n)
            {
                _viewVerts = new Vec3[n];
                _screen = new PointF[n];
                _visible = new bool[n];
            }
            for (int i = 0; i < n; i++)
            {
                _viewVerts[i] = _view.TransformPoint(mesh.Vertices[i]);
                float d;
                _visible[i] = ProjectView(_viewVerts[i], out _screen[i], out d);
            }

            var faces = mesh.Faces;
            int fc = faces.Count;
            if (_order.Length < fc)
            {
                _order = new int[fc];
                _depth = new float[fc];
            }
            if (_facing.Length < fc) _facing = new sbyte[fc];
            Array.Clear(_facing, 0, fc);
            for (int i = 0; i < fc; i++)
            {
                var f = faces[i];
                float sum = 0;
                for (int k = 0; k < f.Indices.Length; k++)
                {
                    var v = _viewVerts[f.Indices[k]];
                    sum += _ortho ? -v.Z : v.Z;
                }
                _depth[i] = sum / f.Indices.Length;
                _order[i] = i;
            }
            Array.Sort(_depth, _order, 0, fc);
            // _order is now nearest-first; iterate from the back.

            var old = g.SmoothingMode;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            bool xray = mode == ViewMode.XRay;
            bool flat = mode == ViewMode.TwoD;

            using (var penBody = new Pen(style.BodyEdge, 1f))
            using (var penGlass = new Pen(style.GlassEdge, 1f))
            using (var penWheel = new Pen(style.WheelEdge, 1f))
            using (var penDetail = new Pen(style.DetailEdge, 1f))
            using (var penXRay = new Pen(style.XRayEdge, 1f))
            {
                var pts = new PointF[16];
                for (int oi = fc - 1; oi >= 0; oi--)
                {
                    var f = faces[_order[oi]];
                    int m = f.Indices.Length;
                    if (m > pts.Length) pts = new PointF[m];
                    bool ok = true;
                    for (int k = 0; k < m; k++)
                    {
                        int vi = f.Indices[k];
                        if (!_visible[vi]) { ok = false; break; }
                        pts[k] = _screen[vi];
                    }
                    if (!ok) continue;

                    // Facing via screen-space signed area (positive = front-facing for CCW winding).
                    float area = 0;
                    for (int k = 0; k < m; k++)
                    {
                        var a = pts[k];
                        var b = pts[(k + 1) % m];
                        area += a.X * b.Y - b.X * a.Y;
                    }
                    if (Math.Abs(area) < 0.5f) { _facing[_order[oi]] = 0; continue; }
                    bool front = area < 0;
                    _facing[_order[oi]] = front ? (sbyte)1 : (sbyte)-1;

                    // shading from the view-space normal
                    float shade = 0.5f;
                    if (!flat)
                    {
                        var a = _viewVerts[f.Indices[0]];
                        var b = _viewVerts[f.Indices[1]];
                        var c = _viewVerts[f.Indices[m - 1]];
                        var nrm = Vec3.Cross(b - a, c - a).Normalized();
                        var toCam = (-a).Normalized();
                        shade = Math.Abs(Vec3.Dot(nrm, toCam));
                    }

                    PointF[] poly;
                    if (m == 3) { poly = _tri; Array.Copy(pts, poly, 3); }
                    else if (m == 4) { poly = _quad; Array.Copy(pts, poly, 4); }
                    else poly = m == pts.Length ? pts : Slice(pts, m);

                    if (!xray)
                    {
                        Color fill;
                        int alpha;
                        switch (f.Part)
                        {
                            case MeshPart.Glass: fill = style.GlassFill; alpha = (int)(36 + 60 * shade); break;
                            case MeshPart.Wheel: fill = style.WheelFill; alpha = (int)(70 + 70 * shade); break;
                            case MeshPart.Detail: fill = style.BodyFill; alpha = (int)(22 + 34 * shade); break;
                            default: fill = style.BodyFill; alpha = (int)(52 + 96 * shade); break;
                        }
                        if (!front) alpha = alpha * 50 / 100;
                        alpha = (int)(alpha * style.FillOpacity);
                        if (alpha > 0)
                            using (var br = new SolidBrush(Color.FromArgb(Math.Min(255, alpha), fill)))
                                g.FillPolygon(br, poly);
                    }

                    if (mesh.UseFeatureEdges) continue; // outlines come from the feature-edge pass below

                    Pen pen;
                    int edgeAlpha;
                    if (xray)
                    {
                        pen = f.Part == MeshPart.Detail ? penDetail : (f.Part == MeshPart.Wheel ? penWheel : penXRay);
                        edgeAlpha = front ? 150 : 95;
                    }
                    else
                    {
                        switch (f.Part)
                        {
                            case MeshPart.Glass: pen = penGlass; break;
                            case MeshPart.Wheel: pen = penWheel; break;
                            case MeshPart.Detail: pen = penDetail; break;
                            default: pen = penBody; break;
                        }
                        edgeAlpha = front ? 160 : 48;
                        if (flat) edgeAlpha = 130;
                    }
                    var baseColor = pen.Color;
                    pen.Color = Color.FromArgb(edgeAlpha, baseColor.R, baseColor.G, baseColor.B);
                    g.DrawPolygon(pen, poly);
                    pen.Color = Color.FromArgb(255, baseColor.R, baseColor.G, baseColor.B);
                }

                if (mesh.UseFeatureEdges)
                {
                    // Crease / silhouette edges of an imported model. Nearer edges are drawn brighter for depth.
                    float zMin = float.MaxValue, zMax = float.MinValue;
                    for (int i = 0; i < n; i++)
                    {
                        float z = _ortho ? -_viewVerts[i].Z : _viewVerts[i].Z;
                        if (z < zMin) zMin = z;
                        if (z > zMax) zMax = z;
                    }
                    float zRange = Math.Max(0.01f, zMax - zMin);
                    foreach (var e in mesh.EdgeAdjacency)
                    {
                        if (!_visible[e.A] || !_visible[e.B]) continue;
                        // draw creases and boundaries always; otherwise only view-dependent silhouettes
                        bool silhouette = e.F1 >= 0 && _facing[e.F0] != 0 && _facing[e.F1] != 0 && _facing[e.F0] != _facing[e.F1];
                        if (!e.Crease && !silhouette) continue;
                        // interior detail parts only get outlines in X-Ray mode; in 3D they show as faint fills
                        if (!xray && e.Part == MeshPart.Detail) continue;
                        bool frontEdge = _facing[e.F0] > 0 || (e.F1 >= 0 && _facing[e.F1] > 0);
                        Pen pen;
                        if (xray) pen = e.Part == MeshPart.Wheel ? penWheel : e.Part == MeshPart.Detail ? penDetail : penXRay;
                        else pen = e.Part == MeshPart.Glass ? penGlass : e.Part == MeshPart.Wheel ? penWheel : e.Part == MeshPart.Detail ? penDetail : penBody;
                        float za = _ortho ? -_viewVerts[e.A].Z : _viewVerts[e.A].Z;
                        float zb = _ortho ? -_viewVerts[e.B].Z : _viewVerts[e.B].Z;
                        float t = ((za + zb) / 2 - zMin) / zRange; // 0 = nearest
                        int alpha = flat ? 150 : (int)(200 - 120 * t);
                        if (silhouette) alpha = Math.Min(255, alpha + 40);
                        else alpha = alpha * 60 / 100;
                        if (e.Part == MeshPart.Wheel && !silhouette) alpha = alpha * 60 / 100;
                        if (!frontEdge && !xray) alpha = alpha * 40 / 100;
                        if (xray) alpha = (int)((silhouette ? 230 : 150) - 90 * t);
                        var c = pen.Color;
                        pen.Color = Color.FromArgb(Math.Max(28, alpha), c.R, c.G, c.B);
                        g.DrawLine(pen, _screen[e.A], _screen[e.B]);
                        pen.Color = Color.FromArgb(255, c.R, c.G, c.B);
                    }
                }

                // free-standing detail lines
                foreach (var e in mesh.Lines)
                {
                    if (!_visible[e.A] || !_visible[e.B]) continue;
                    var pen = e.Part == MeshPart.Wheel ? penWheel : penDetail;
                    var c = pen.Color;
                    pen.Color = Color.FromArgb(xray ? 170 : 120, c.R, c.G, c.B);
                    g.DrawLine(pen, _screen[e.A], _screen[e.B]);
                    pen.Color = Color.FromArgb(255, c.R, c.G, c.B);
                }
            }
            g.SmoothingMode = old;
        }

        private static PointF[] Slice(PointF[] src, int count)
        {
            var r = new PointF[count];
            Array.Copy(src, r, count);
            return r;
        }

        // ------------------------------------------------------------------ ground

        /// <summary>Glowing red platform ring with radial ticks and a faint grid.</summary>
        public void DrawGround(Graphics g, RenderStyle style, float radius, float phase)
        {
            var old = g.SmoothingMode;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            if (_ortho)
            {
                using (var pen = new Pen(Color.FromArgb(26, style.Grid), 1f))
                {
                    for (float x = -radius; x <= radius + 0.01f; x += 0.5f)
                    {
                        var a = Project(new Vec3(x, 0, -radius));
                        var b = Project(new Vec3(x, 0, radius));
                        g.DrawLine(pen, a, b);
                    }
                    for (float z = -radius; z <= radius + 0.01f; z += 0.5f)
                    {
                        var a = Project(new Vec3(-radius, 0, z));
                        var b = Project(new Vec3(radius, 0, z));
                        g.DrawLine(pen, a, b);
                    }
                }
                g.SmoothingMode = old;
                return;
            }

            // faint grid inside the disc
            using (var pen = new Pen(Color.FromArgb(22, style.Grid), 1f))
            {
                for (float x = -3f; x <= 3.01f; x += 1f)
                {
                    float half = (float)Math.Sqrt(Math.Max(0, radius * radius - x * x));
                    if (half <= 0) continue;
                    g.DrawLine(pen, Project(new Vec3(x, 0, -half)), Project(new Vec3(x, 0, half)));
                    g.DrawLine(pen, Project(new Vec3(-half, 0, x)), Project(new Vec3(half, 0, x)));
                }
            }

            // rings
            DrawRing(g, radius, style.Ground, 3.5f, 40);
            DrawRing(g, radius, style.Ground, 1.6f, 200);
            DrawRing(g, radius * 1.09f, style.Ground, 1f, 70);
            DrawRing(g, radius * 0.82f, style.Ground, 1f, 45);

            // radial ticks
            using (var pen = new Pen(Color.FromArgb(120, style.Ground), 1f))
            {
                for (int i = 0; i < 36; i++)
                {
                    double a = Math.PI * 2 * i / 36;
                    float r0 = i % 3 == 0 ? radius * 1.03f : radius * 1.06f;
                    var p0 = Project(new Vec3((float)Math.Cos(a) * r0, 0, (float)Math.Sin(a) * r0));
                    var p1 = Project(new Vec3((float)Math.Cos(a) * radius * 1.09f, 0, (float)Math.Sin(a) * radius * 1.09f));
                    g.DrawLine(pen, p0, p1);
                }
            }

            // rotating sweep arc for a "radar" feel
            using (var pen = new Pen(Color.FromArgb(150, style.Ground), 2.2f))
            {
                var pts = new List<PointF>();
                for (int i = 0; i <= 18; i++)
                {
                    double a = phase + Math.PI * 2 * i / 36 * 0.9;
                    pts.Add(Project(new Vec3((float)Math.Cos(a) * radius * 1.09f, 0, (float)Math.Sin(a) * radius * 1.09f)));
                }
                if (pts.Count > 1) g.DrawLines(pen, pts.ToArray());
            }
            g.SmoothingMode = old;
        }

        private void DrawRing(Graphics g, float radius, Color color, float width, int alpha)
        {
            var pts = new PointF[72];
            for (int i = 0; i < 72; i++)
            {
                double a = Math.PI * 2 * i / 72;
                pts[i] = Project(new Vec3((float)Math.Cos(a) * radius, 0, (float)Math.Sin(a) * radius));
            }
            using (var pen = new Pen(Color.FromArgb(alpha, color), width))
                g.DrawPolygon(pen, pts);
        }

        // ------------------------------------------------------------------ overlays

        /// <summary>A small glowing cube marker at a world position.</summary>
        public void DrawCube(Graphics g, Vec3 centre, float size, Color color, float glow)
        {
            float h = size / 2;
            var c = new[]
            {
                new Vec3(centre.X - h, centre.Y - h, centre.Z - h), new Vec3(centre.X + h, centre.Y - h, centre.Z - h),
                new Vec3(centre.X + h, centre.Y - h, centre.Z + h), new Vec3(centre.X - h, centre.Y - h, centre.Z + h),
                new Vec3(centre.X - h, centre.Y + h, centre.Z - h), new Vec3(centre.X + h, centre.Y + h, centre.Z - h),
                new Vec3(centre.X + h, centre.Y + h, centre.Z + h), new Vec3(centre.X - h, centre.Y + h, centre.Z + h),
            };
            var s = new PointF[8];
            var d = new float[8];
            for (int i = 0; i < 8; i++)
                if (!Project(c[i], out s[i], out d[i])) return;

            int[][] faces = { new[] { 0, 1, 2, 3 }, new[] { 4, 5, 6, 7 }, new[] { 0, 1, 5, 4 }, new[] { 1, 2, 6, 5 }, new[] { 2, 3, 7, 6 }, new[] { 3, 0, 4, 7 } };
            var old = g.SmoothingMode;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            PointF centreS = Project(centre);
            float px = ScaleAt(centre) * size;
            if (glow > 0)
            {
                for (int i = 4; i >= 1; i--)
                {
                    float rr = px * (0.7f + i * 0.45f * glow);
                    using (var b = new SolidBrush(Color.FromArgb((int)(22 * glow) - i * 3 < 0 ? 0 : (int)(22 * glow) - i * 3, color)))
                        g.FillEllipse(b, centreS.X - rr, centreS.Y - rr, rr * 2, rr * 2);
                }
            }

            // sort faces far to near
            var order = new int[6];
            var depth = new float[6];
            for (int i = 0; i < 6; i++)
            {
                order[i] = i;
                float sum = 0;
                foreach (var vi in faces[i]) sum += d[vi];
                depth[i] = sum;
            }
            Array.Sort(depth, order);
            using (var pen = new Pen(Color.FromArgb(230, Lighten(color, 0.35f)), 1f))
            {
                for (int oi = 5; oi >= 0; oi--)
                {
                    var f = faces[order[oi]];
                    var poly = new[] { s[f[0]], s[f[1]], s[f[2]], s[f[3]] };
                    float area = 0;
                    for (int k = 0; k < 4; k++) area += poly[k].X * poly[(k + 1) % 4].Y - poly[(k + 1) % 4].X * poly[k].Y;
                    bool front = _ortho ? oi < 3 : area < 0;
                    int alpha = front ? 210 : 110;
                    using (var b = new SolidBrush(Color.FromArgb(alpha, color)))
                        g.FillPolygon(b, poly);
                    g.DrawPolygon(pen, poly);
                }
            }
            g.SmoothingMode = old;
        }

        /// <summary>A translucent vertical plane at world X used as the scanning sweep.</summary>
        public void DrawSweepPlane(Graphics g, float x, float halfWidth, float height, Color color)
        {
            PointF a, b, c, d; float dd;
            if (!Project(new Vec3(x, 0.05f, -halfWidth), out a, out dd)) return;
            if (!Project(new Vec3(x, 0.05f, halfWidth), out b, out dd)) return;
            if (!Project(new Vec3(x, height, halfWidth), out c, out dd)) return;
            if (!Project(new Vec3(x, height, -halfWidth), out d, out dd)) return;
            var old = g.SmoothingMode;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var poly = new[] { a, b, c, d };
            using (var br = new LinearGradientBrush(new PointF(0, Math.Min(c.Y, d.Y)), new PointF(0, Math.Max(a.Y, b.Y)), Color.FromArgb(10, color), Color.FromArgb(70, color)))
                g.FillPolygon(br, poly);
            using (var pen = new Pen(Color.FromArgb(200, color), 1.5f))
                g.DrawPolygon(pen, poly);
            using (var pen = new Pen(Color.FromArgb(70, color), 5f))
                g.DrawLine(pen, a, b);
            g.SmoothingMode = old;
        }

        public static Color Lighten(Color c, float t)
        {
            return Color.FromArgb(c.A, (int)(c.R + (255 - c.R) * t), (int)(c.G + (255 - c.G) * t), (int)(c.B + (255 - c.B) * t));
        }
    }
}
