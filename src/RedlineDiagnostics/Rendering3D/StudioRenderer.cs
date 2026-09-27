using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace RedlineDiagnostics.Rendering3D
{
    /// <summary>Material and lighting set-up for the opaque "product shot" rendering used on the Home page.</summary>
    public sealed class StudioStyle
    {
        public Color Paint = Color.FromArgb(30, 32, 38);
        public Color Glass = Color.FromArgb(10, 12, 18);
        public Color Tyre = Color.FromArgb(20, 20, 24);
        public Color Interior = Color.FromArgb(24, 24, 28);
        /// <summary>Coloured rim / bounce light (the red studio lights in the design).</summary>
        public Color RimColor = Color.FromArgb(255, 40, 70);
        public Color SkyColor = Color.FromArgb(200, 210, 230);
        /// <summary>Key light direction in view space (x right, y up, z into the screen), pointing towards the light.</summary>
        public Vec3 KeyLight = new Vec3(-0.45f, 0.8f, -0.55f);
        public float Ambient = 0.22f;
        public float Diffuse = 0.75f;
        public float Specular = 0.9f;
        public float Shininess = 30f;
        public float Rim = 0.55f;
        public float Bounce = 0.28f;
        public float Sky = 0.35f;
        /// <summary>Corners whose face normal deviates more than this from the smoothed vertex normal stay faceted.</summary>
        public float SmoothAngleDegrees = 50f;
        /// <summary>Screen pixels per drawing unit when the caller scales the Graphics; keeps seam widths at one pixel.</summary>
        public float PixelScale = 1f;
    }

    public sealed partial class Renderer
    {
        /// <summary>
        /// Opaque, smooth-shaded rendering: two-sided Blinn-Phong key light, sky reflection on upward faces, coloured
        /// fresnel rim and ground bounce, evaluated per vertex and interpolated across each triangle. Meant for cached
        /// hero images (render at 2x and downsample for anti-aliasing), not for per-frame animation.
        /// </summary>
        public void DrawMeshStudio(Graphics g, Mesh mesh, StudioStyle s)
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

            // World-space face normals (area weighted) and smoothed vertex normals.
            var faceN = new Vec3[fc];
            var vertN = new Vec3[n];
            for (int i = 0; i < fc; i++)
            {
                var idx = faces[i].Indices;
                var a = mesh.Vertices[idx[0]];
                var nrm = Vec3.Zero;
                for (int k = 1; k + 1 < idx.Length; k++)
                    nrm = nrm + Vec3.Cross(mesh.Vertices[idx[k]] - a, mesh.Vertices[idx[k + 1]] - a);
                faceN[i] = nrm;
                for (int k = 0; k < idx.Length; k++) vertN[idx[k]] = vertN[idx[k]] + nrm;
            }
            for (int i = 0; i < n; i++) vertN[i] = vertN[i].Length > 1e-12f ? vertN[i].Normalized() : Vec3.UnitY;
            float smoothCos = (float)Math.Cos(s.SmoothAngleDegrees * Math.PI / 180.0);

            if (_order.Length < fc)
            {
                _order = new int[fc];
                _depth = new float[fc];
            }
            for (int i = 0; i < fc; i++)
            {
                var f = faces[i];
                float sum = 0;
                for (int k = 0; k < f.Indices.Length; k++) sum += _viewVerts[f.Indices[k]].Z;
                _depth[i] = sum / f.Indices.Length;
                _order[i] = i;
            }
            Array.Sort(_depth, _order, 0, fc);

            var L = s.KeyLight.Normalized();
            var old = g.SmoothingMode;
            g.SmoothingMode = SmoothingMode.None; // callers supersample; AA here would leave seams between triangles
            var tri = new PointF[3];
            var cols = new Color[3];
            var corner = new Color[16];
            using (var seam = new Pen(Color.Black, 1f / s.PixelScale) { LineJoin = LineJoin.Round })
            using (var solid = new SolidBrush(Color.Black))
            {
                for (int oi = fc - 1; oi >= 0; oi--)
                {
                    int fi = _order[oi];
                    var f = faces[fi];
                    var idx = f.Indices;
                    int m = idx.Length;
                    bool ok = true;
                    for (int k = 0; k < m; k++) if (!_visible[idx[k]]) { ok = false; break; }
                    if (!ok) continue;
                    var fnLen = faceN[fi].Length;
                    if (fnLen < 1e-12f) continue;
                    var fnW = faceN[fi] / fnLen;

                    Color baseC;
                    float spec = s.Specular, rim = s.Rim, sky = s.Sky;
                    int alpha = 255;
                    switch (f.Part)
                    {
                        case MeshPart.Glass: baseC = s.Glass; spec *= 1.2f; rim *= 0.5f; sky *= 1.3f; alpha = 238; break;
                        case MeshPart.Wheel: baseC = s.Tyre; spec *= 0.45f; rim *= 0.7f; sky *= 0.4f; break;
                        case MeshPart.Detail: baseC = s.Interior; spec *= 0.3f; rim *= 0.4f; sky *= 0.3f; break;
                        default: baseC = s.Paint; break;
                    }

                    if (m > corner.Length) corner = new Color[m];
                    int ar = 0, ag = 0, ab = 0;
                    for (int k = 0; k < m; k++)
                    {
                        int vi = idx[k];
                        var nW = Vec3.Dot(vertN[vi], fnW) >= smoothCos ? vertN[vi] : fnW;
                        var nV = _view.TransformNormal(nW).Normalized();
                        var toCam = (-_viewVerts[vi]).Normalized();
                        float ndv = Vec3.Dot(nV, toCam);
                        if (ndv < 0) { nV = -nV; nW = -nW; ndv = -ndv; } // two-sided lighting
                        float diff = Math.Max(0f, Vec3.Dot(nV, L));
                        var h = (L + toCam).Normalized();
                        float sp = (float)Math.Pow(Math.Max(0f, Vec3.Dot(nV, h)), s.Shininess) * spec;
                        float fres = (float)Math.Pow(1f - Math.Min(1f, ndv), 3.2) * rim;
                        float bounce = Math.Max(0f, -nW.Y) * s.Bounce;
                        float skyR = Math.Max(0f, nW.Y) * sky * (0.35f + 0.65f * (1f - ndv));
                        float lit = s.Ambient + s.Diffuse * diff;
                        var c = Color.FromArgb(alpha,
                            Clamp(baseC.R * lit + 255f * sp + s.SkyColor.R * skyR + s.RimColor.R * (fres + bounce)),
                            Clamp(baseC.G * lit + 255f * sp + s.SkyColor.G * skyR + s.RimColor.G * (fres + bounce)),
                            Clamp(baseC.B * lit + 255f * sp + s.SkyColor.B * skyR + s.RimColor.B * (fres + bounce)));
                        corner[k] = c;
                        ar += c.R; ag += c.G; ab += c.B;
                    }
                    var avg = Color.FromArgb(alpha, ar / m, ag / m, ab / m);

                    // Fan-triangulate and fill each triangle with its interpolated corner colours.
                    for (int k = 1; k + 1 < m; k++)
                    {
                        tri[0] = _screen[idx[0]]; tri[1] = _screen[idx[k]]; tri[2] = _screen[idx[k + 1]];
                        float area = (tri[1].X - tri[0].X) * (tri[2].Y - tri[0].Y) - (tri[2].X - tri[0].X) * (tri[1].Y - tri[0].Y);
                        float areaPx = Math.Abs(area) * s.PixelScale * s.PixelScale;
                        if (areaPx < 0.02f) continue;
                        cols[0] = corner[0]; cols[1] = corner[k]; cols[2] = corner[k + 1];
                        if (areaPx < 6f)
                        {
                            solid.Color = avg;
                            g.FillPolygon(solid, tri);
                        }
                        else
                        {
                            using (var br = new PathGradientBrush(tri))
                            {
                                br.CenterPoint = new PointF((tri[0].X + tri[1].X + tri[2].X) / 3f, (tri[0].Y + tri[1].Y + tri[2].Y) / 3f);
                                br.CenterColor = Color.FromArgb(alpha, (cols[0].R + cols[1].R + cols[2].R) / 3, (cols[0].G + cols[1].G + cols[2].G) / 3, (cols[0].B + cols[1].B + cols[2].B) / 3);
                                br.SurroundColors = cols;
                                g.FillPolygon(br, tri);
                            }
                        }
                        if (alpha == 255)
                        {
                            // Close the one-pixel cracks GDI+ leaves between neighbouring non-AA polygons.
                            seam.Color = avg;
                            g.DrawPolygon(seam, tri);
                        }
                    }
                }
            }
            g.SmoothingMode = old;
        }

        /// <summary>Soft elliptical glow / shadow on the ground plane (y = 0) centred under the car.</summary>
        public void DrawFloorGlow(Graphics g, float radiusX, float radiusZ, Color centre, float offsetX = 0f)
        {
            const int seg = 48;
            var pts = new PointF[seg];
            for (int i = 0; i < seg; i++)
            {
                double t = i * Math.PI * 2 / seg;
                pts[i] = Project(new Vec3(offsetX + (float)Math.Cos(t) * radiusX, 0f, (float)Math.Sin(t) * radiusZ));
            }
            var c = Project(new Vec3(offsetX, 0f, 0f));
            var old = g.SmoothingMode;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var path = new GraphicsPath())
            {
                path.AddPolygon(pts);
                using (var br = new PathGradientBrush(path) { CenterPoint = c, CenterColor = centre, SurroundColors = new[] { Color.FromArgb(0, centre) } })
                    g.FillPath(br, path);
            }
            g.SmoothingMode = old;
        }

        private static int Clamp(float v) => v < 0 ? 0 : v > 255 ? 255 : (int)v;
    }
}
