using System;
using System.Collections.Generic;

namespace RedlineDiagnostics.Rendering3D
{
    /// <summary>
    /// Builds a procedural low-poly sedan (roughly a mid-size hybrid saloon) so the app renders a
    /// vehicle without any external asset. Coordinates: X = length (+X is the front), Y = up, Z = right.
    /// Units are metres; the body is 4.85 m long, 1.84 m wide, 1.45 m tall.
    /// </summary>
    public static class CarMeshFactory
    {
        private struct Station
        {
            public float X, Y0, Y1, Y2, Y3, Wb, Wr;
            public bool Cabin;      // side glass between this station and the next
            public bool Glass;      // sloped glass between this station and the next (windshield / rear window)

            public Station(float x, float y0, float y1, float y2, float y3, float wb, float wr, bool cabin = false, bool glass = false)
            {
                X = x; Y0 = y0; Y1 = y1; Y2 = y2; Y3 = y3; Wb = wb; Wr = wr; Cabin = cabin; Glass = glass;
            }
        }

        public static Mesh CreateSedan()
        {
            var m = new Mesh();

            // ---- Body loft ----
            var st = new List<Station>
            {
                new Station(-2.42f, 0.42f, 0.78f, 0.78f, 0.78f, 0.74f, 0.74f),
                new Station(-2.38f, 0.30f, 0.92f, 0.92f, 0.92f, 0.86f, 0.86f),
                new Station(-2.25f, 0.24f, 1.00f, 1.00f, 1.00f, 0.90f, 0.90f),
                new Station(-1.85f, 0.22f, 1.03f, 1.03f, 1.03f, 0.92f, 0.92f),
                new Station(-1.55f, 0.22f, 1.04f, 1.06f, 1.06f, 0.92f, 0.88f, false, true),
                new Station(-1.15f, 0.22f, 1.02f, 1.30f, 1.36f, 0.93f, 0.72f, true),
                new Station(-0.70f, 0.22f, 1.00f, 1.40f, 1.45f, 0.93f, 0.68f, true),
                new Station(-0.10f, 0.22f, 0.99f, 1.42f, 1.46f, 0.93f, 0.67f, true),
                new Station( 0.45f, 0.22f, 0.98f, 1.40f, 1.45f, 0.93f, 0.68f, true),
                new Station( 0.80f, 0.22f, 0.97f, 1.32f, 1.38f, 0.93f, 0.70f, false, true),
                new Station( 1.15f, 0.22f, 0.95f, 1.02f, 1.02f, 0.92f, 0.82f),
                new Station( 1.65f, 0.23f, 0.90f, 0.90f, 0.90f, 0.91f, 0.91f),
                new Station( 2.10f, 0.24f, 0.82f, 0.82f, 0.82f, 0.88f, 0.88f),
                new Station( 2.35f, 0.28f, 0.72f, 0.72f, 0.72f, 0.82f, 0.82f),
                new Station( 2.43f, 0.40f, 0.58f, 0.58f, 0.58f, 0.72f, 0.72f),
            };

            var rings = new List<int[]>();
            foreach (var s in st)
            {
                var ring = new int[8];
                ring[0] = m.AddVertex(s.X, s.Y0, -s.Wb);
                ring[1] = m.AddVertex(s.X, s.Y1, -s.Wb);
                ring[2] = m.AddVertex(s.X, s.Y2, -s.Wr);
                ring[3] = m.AddVertex(s.X, s.Y3, -s.Wr * 0.55f);
                ring[4] = m.AddVertex(s.X, s.Y3, s.Wr * 0.55f);
                ring[5] = m.AddVertex(s.X, s.Y2, s.Wr);
                ring[6] = m.AddVertex(s.X, s.Y1, s.Wb);
                ring[7] = m.AddVertex(s.X, s.Y0, s.Wb);
                rings.Add(ring);
            }

            for (int s = 0; s + 1 < rings.Count; s++)
            {
                var a = rings[s];
                var b = rings[s + 1];
                var stA = st[s];
                for (int i = 0; i < 8; i++)
                {
                    int j = (i + 1) % 8;
                    MeshPart part = MeshPart.Body;
                    if (stA.Cabin && (i == 1 || i == 5)) part = MeshPart.Glass;
                    if (stA.Glass && (i >= 1 && i <= 5)) part = MeshPart.Glass;
                    // skip degenerate quads where the ring points coincide
                    if (a[i] == a[j] && b[i] == b[j]) continue;
                    m.AddFace(part, a[i], a[j], b[j], b[i]);
                }
            }
            // End caps
            m.AddFace(MeshPart.Body, rings[0][7], rings[0][6], rings[0][5], rings[0][4], rings[0][3], rings[0][2], rings[0][1], rings[0][0]);
            var last = rings[rings.Count - 1];
            m.AddFace(MeshPart.Body, last[0], last[1], last[2], last[3], last[4], last[5], last[6], last[7]);

            // ---- Wheels ----
            float[] wx = { 1.42f, -1.42f };
            float[] wz = { -0.80f, 0.80f };
            foreach (var x in wx)
                foreach (var z in wz)
                    AddWheel(m, x, 0.34f, z, 0.34f, 0.115f, z < 0 ? -1 : 1);

            // ---- Details: wheel arches ----
            foreach (var x in wx)
                foreach (var z in wz)
                {
                    float side = z < 0 ? -0.935f : 0.935f;
                    var arc = new List<int>();
                    for (int i = 0; i <= 10; i++)
                    {
                        double a = Math.PI * i / 10;
                        arc.Add(m.AddVertex(x + (float)Math.Cos(a) * 0.45f, 0.30f + (float)Math.Sin(a) * 0.42f, side));
                    }
                    m.AddPolyline(MeshPart.Detail, arc.ToArray());
                }

            // ---- Details: door seams and handles ----
            float[] seamX = { 0.95f, -0.08f, -1.08f };
            foreach (var sx in seamX)
                foreach (var side in new[] { -0.935f, 0.935f })
                {
                    int top = m.AddVertex(sx, 0.98f, side);
                    int bot = m.AddVertex(sx, 0.32f, side);
                    m.AddLine(top, bot);
                }
            foreach (var side in new[] { -0.94f, 0.94f })
            {
                foreach (var hx in new[] { 0.10f, -0.90f })
                {
                    int a = m.AddVertex(hx, 0.80f, side), b = m.AddVertex(hx - 0.16f, 0.80f, side);
                    int c = m.AddVertex(hx - 0.16f, 0.76f, side), d = m.AddVertex(hx, 0.76f, side);
                    m.AddFace(MeshPart.Detail, a, b, c, d);
                }
                // side skirt line
                int s1 = m.AddVertex(1.0f, 0.30f, side), s2 = m.AddVertex(-1.0f, 0.30f, side);
                m.AddLine(s1, s2);
            }

            // ---- Details: mirrors ----
            foreach (var side in new[] { -1, 1 })
            {
                float z0 = side * 0.93f, z1 = side * 1.08f;
                int a = m.AddVertex(0.78f, 1.02f, z0), b = m.AddVertex(0.78f, 1.02f, z1);
                int c = m.AddVertex(0.62f, 1.02f, z1), d = m.AddVertex(0.62f, 1.02f, z0);
                int e = m.AddVertex(0.78f, 1.14f, z0), f = m.AddVertex(0.78f, 1.14f, z1);
                int g = m.AddVertex(0.62f, 1.14f, z1), h = m.AddVertex(0.62f, 1.14f, z0);
                m.AddFace(MeshPart.Detail, a, b, c, d);
                m.AddFace(MeshPart.Detail, e, f, g, h);
                m.AddFace(MeshPart.Detail, a, b, f, e);
                m.AddFace(MeshPart.Detail, d, c, g, h);
                m.AddFace(MeshPart.Detail, b, c, g, f);
            }

            // ---- Details: head and tail lights, grille ----
            foreach (var side in new[] { -1, 1 })
            {
                // headlight: swept polygon on the front corner
                int h1 = m.AddVertex(2.30f, 0.76f, side * 0.84f);
                int h2 = m.AddVertex(2.42f, 0.66f, side * 0.62f);
                int h3 = m.AddVertex(2.42f, 0.56f, side * 0.66f);
                int h4 = m.AddVertex(2.18f, 0.62f, side * 0.88f);
                m.AddFace(MeshPart.Glass, h1, h2, h3, h4);
                // tail light
                int t1 = m.AddVertex(-2.36f, 0.94f, side * 0.86f);
                int t2 = m.AddVertex(-2.40f, 0.94f, side * 0.50f);
                int t3 = m.AddVertex(-2.40f, 0.80f, side * 0.50f);
                int t4 = m.AddVertex(-2.30f, 0.80f, side * 0.90f);
                m.AddFace(MeshPart.Glass, t1, t2, t3, t4);
            }
            for (int i = 0; i < 4; i++)
            {
                float y = 0.36f + i * 0.06f;
                int g1 = m.AddVertex(2.405f, y, -0.52f), g2 = m.AddVertex(2.405f, y, 0.52f);
                m.AddLine(g1, g2);
            }
            // Emblem
            int e1 = m.AddVertex(2.43f, 0.62f, -0.06f), e2 = m.AddVertex(2.43f, 0.68f, 0f), e3 = m.AddVertex(2.43f, 0.62f, 0.06f), e4 = m.AddVertex(2.43f, 0.56f, 0f);
            m.AddFace(MeshPart.Detail, e1, e2, e3, e4);

            // ---- Details: interior hints (seats, steering wheel, dash) ----
            AddBox(m, 0.05f, 0.35f, -0.45f, 0.50f, 0.20f, 0.45f, MeshPart.Detail);  // driver seat base
            AddBox(m, -0.25f, 0.55f, -0.45f, 0.12f, 0.55f, 0.45f, MeshPart.Detail); // driver seat back
            AddBox(m, 0.05f, 0.35f, 0.45f, 0.50f, 0.20f, 0.45f, MeshPart.Detail);
            AddBox(m, -0.25f, 0.55f, 0.45f, 0.12f, 0.55f, 0.45f, MeshPart.Detail);
            AddBox(m, -1.05f, 0.35f, 0f, 0.45f, 0.20f, 1.35f, MeshPart.Detail);      // rear bench
            AddBox(m, -1.32f, 0.55f, 0f, 0.12f, 0.50f, 1.35f, MeshPart.Detail);
            AddBox(m, 0.95f, 0.85f, 0f, 0.35f, 0.18f, 1.60f, MeshPart.Detail);       // dashboard
            // steering wheel
            var sw = new List<int>();
            for (int i = 0; i < 12; i++)
            {
                double a = Math.PI * 2 * i / 12;
                sw.Add(m.AddVertex(0.62f - (float)Math.Sin(a) * 0.06f, 0.92f + (float)Math.Cos(a) * 0.18f, -0.42f + (float)Math.Sin(a) * 0.17f));
            }
            m.AddFace(MeshPart.Detail, sw.ToArray());

            return m;
        }

        private static void AddBox(Mesh m, float cx, float cy, float cz, float sx, float sy, float sz, MeshPart part)
        {
            float hx = sx / 2, hy = sy / 2, hz = sz / 2;
            int a = m.AddVertex(cx - hx, cy - hy, cz - hz);
            int b = m.AddVertex(cx + hx, cy - hy, cz - hz);
            int c = m.AddVertex(cx + hx, cy - hy, cz + hz);
            int d = m.AddVertex(cx - hx, cy - hy, cz + hz);
            int e = m.AddVertex(cx - hx, cy + hy, cz - hz);
            int f = m.AddVertex(cx + hx, cy + hy, cz - hz);
            int g = m.AddVertex(cx + hx, cy + hy, cz + hz);
            int h = m.AddVertex(cx - hx, cy + hy, cz + hz);
            m.AddFace(part, a, b, c, d);
            m.AddFace(part, e, f, g, h);
            m.AddFace(part, a, b, f, e);
            m.AddFace(part, b, c, g, f);
            m.AddFace(part, c, d, h, g);
            m.AddFace(part, d, a, e, h);
        }

        private static void AddWheel(Mesh m, float cx, float cy, float cz, float radius, float halfWidth, int outerSign)
        {
            const int N = 18;
            var inner = new int[N];
            var outer = new int[N];
            for (int i = 0; i < N; i++)
            {
                double a = Math.PI * 2 * i / N;
                float x = cx + (float)Math.Cos(a) * radius;
                float y = cy + (float)Math.Sin(a) * radius;
                inner[i] = m.AddVertex(x, y, cz - halfWidth * outerSign);
                outer[i] = m.AddVertex(x, y, cz + halfWidth * outerSign);
            }
            for (int i = 0; i < N; i++)
            {
                int j = (i + 1) % N;
                m.AddFace(MeshPart.Wheel, inner[i], inner[j], outer[j], outer[i]);
            }
            m.AddFace(MeshPart.Wheel, outer);
            m.AddFace(MeshPart.Wheel, inner);

            // rim ring + spokes on the outer face
            var rim = new int[N];
            float rr = radius * 0.62f;
            float zOut = cz + halfWidth * outerSign * 1.02f;
            for (int i = 0; i < N; i++)
            {
                double a = Math.PI * 2 * i / N;
                rim[i] = m.AddVertex(cx + (float)Math.Cos(a) * rr, cy + (float)Math.Sin(a) * rr, zOut);
            }
            for (int i = 0; i < N; i++) m.AddLine(rim[i], rim[(i + 1) % N], MeshPart.Wheel);
            int hub = m.AddVertex(cx, cy, zOut);
            for (int i = 0; i < 5; i++)
            {
                double a = Math.PI * 2 * i / 5;
                int tip = m.AddVertex(cx + (float)Math.Cos(a) * rr * 0.92f, cy + (float)Math.Sin(a) * rr * 0.92f, zOut);
                m.AddLine(hub, tip, MeshPart.Wheel);
            }
        }
    }
}
