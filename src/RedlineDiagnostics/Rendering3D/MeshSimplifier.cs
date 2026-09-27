using System;
using System.Collections.Generic;

namespace RedlineDiagnostics.Rendering3D
{
    /// <summary>
    /// Quadric error metric edge-collapse simplification (Garland &amp; Heckbert) for dense imported meshes.
    /// Preserves silhouettes, creases and open panel edges far better than vertex clustering, so a
    /// 200k-triangle model still reads as a car at 6-8k triangles in the holographic renderer.
    /// </summary>
    public static class MeshSimplifier
    {
        private struct Quadric
        {
            public double A, B, C, D, E, F, G, H, I, J; // symmetric 4x4: [A B C D; B E F G; C F H I; D G I J] (J is constant)

            public static Quadric FromPlane(double a, double b, double c, double d, double w)
            {
                return new Quadric
                {
                    A = a * a * w, B = a * b * w, C = a * c * w, D = a * d * w,
                    E = b * b * w, F = b * c * w, G = b * d * w,
                    H = c * c * w, I = c * d * w,
                    J = d * d * w
                };
            }

            public static Quadric operator +(Quadric p, Quadric q)
            {
                return new Quadric
                {
                    A = p.A + q.A, B = p.B + q.B, C = p.C + q.C, D = p.D + q.D,
                    E = p.E + q.E, F = p.F + q.F, G = p.G + q.G,
                    H = p.H + q.H, I = p.I + q.I, J = p.J + q.J
                };
            }

            public double Evaluate(double x, double y, double z)
            {
                return A * x * x + 2 * B * x * y + 2 * C * x * z + 2 * D * x
                     + E * y * y + 2 * F * y * z + 2 * G * y
                     + H * z * z + 2 * I * z + J;
            }

            /// <summary>Solves for the point minimising the error; false if the system is singular.</summary>
            public bool Optimal(out double x, out double y, out double z)
            {
                double det = A * (E * H - F * F) - B * (B * H - F * C) + C * (B * F - E * C);
                x = y = z = 0;
                if (Math.Abs(det) < 1e-12) return false;
                double inv = 1.0 / det;
                // solve M * v = -[D G I]
                double rx = -D, ry = -G, rz = -I;
                x = inv * (rx * (E * H - F * F) - B * (ry * H - F * rz) + C * (ry * F - E * rz));
                y = inv * (A * (ry * H - F * rz) - rx * (B * H - F * C) + C * (B * rz - ry * C));
                z = inv * (A * (E * rz - ry * F) - B * (B * rz - ry * C) + rx * (B * F - E * C));
                return !(double.IsNaN(x) || double.IsNaN(y) || double.IsNaN(z) || double.IsInfinity(x) || double.IsInfinity(y) || double.IsInfinity(z));
            }
        }

        private struct HeapItem
        {
            public double Cost;
            public int A, B;
            public int VerA, VerB;
            public float X, Y, Z;
        }

        private sealed class MinHeap
        {
            private readonly List<HeapItem> _items = new List<HeapItem>();
            public int Count => _items.Count;

            public void Push(HeapItem item)
            {
                _items.Add(item);
                int i = _items.Count - 1;
                while (i > 0)
                {
                    int p = (i - 1) / 2;
                    if (_items[p].Cost <= _items[i].Cost) break;
                    var t = _items[p]; _items[p] = _items[i]; _items[i] = t;
                    i = p;
                }
            }

            public HeapItem Pop()
            {
                var top = _items[0];
                var last = _items[_items.Count - 1];
                _items.RemoveAt(_items.Count - 1);
                if (_items.Count > 0)
                {
                    _items[0] = last;
                    int i = 0;
                    while (true)
                    {
                        int l = 2 * i + 1, r = l + 1, s = i;
                        if (l < _items.Count && _items[l].Cost < _items[s].Cost) s = l;
                        if (r < _items.Count && _items[r].Cost < _items[s].Cost) s = r;
                        if (s == i) break;
                        var t = _items[s]; _items[s] = _items[i]; _items[i] = t;
                        i = s;
                    }
                }
                return top;
            }
        }

        public static string LastStats = "";

        /// <summary>Simplifies in place until at most <paramref name="targetFaces"/> triangles remain.</summary>
        public static void Simplify(Mesh mesh, int targetFaces)
        {
            long pops = 0, pushes = 0, flips = 0, collapses = 0, flipFaces = 0, maxDeg = 0;
            var swTotal = System.Diagnostics.Stopwatch.StartNew();
            long tInit = 0;
            // ---- triangulate ----
            var tris = new List<int[]>();
            var parts = new List<MeshPart>();
            foreach (var f in mesh.Faces)
            {
                for (int i = 1; i + 1 < f.Indices.Length; i++)
                {
                    tris.Add(new[] { f.Indices[0], f.Indices[i], f.Indices[i + 1] });
                    parts.Add(f.Part);
                }
            }
            int nv = mesh.Vertices.Count, nf = tris.Count;
            if (nf <= targetFaces) return;

            var px = new double[nv]; var py = new double[nv]; var pz = new double[nv];
            for (int i = 0; i < nv; i++) { px[i] = mesh.Vertices[i].X; py[i] = mesh.Vertices[i].Y; pz[i] = mesh.Vertices[i].Z; }
            var fa = new int[nf]; var fb = new int[nf]; var fc = new int[nf];
            var alive = new bool[nf];
            for (int i = 0; i < nf; i++) { fa[i] = tris[i][0]; fb[i] = tris[i][1]; fc[i] = tris[i][2]; alive[i] = true; }

            var q = new Quadric[nv];
            var vfaces = new List<int>[nv];
            for (int i = 0; i < nv; i++) vfaces[i] = new List<int>(6);
            var edgeFaceCount = new Dictionary<long, int>(PackedKeyComparer.Instance);
            for (int i = 0; i < nf; i++)
            {
                vfaces[fa[i]].Add(i); vfaces[fb[i]].Add(i); vfaces[fc[i]].Add(i);
                double nx, ny, nz, area;
                Normal(px, py, pz, fa[i], fb[i], fc[i], out nx, out ny, out nz, out area);
                if (area <= 0) { alive[i] = false; continue; }
                double d = -(nx * px[fa[i]] + ny * py[fa[i]] + nz * pz[fa[i]]);
                var pq = Quadric.FromPlane(nx, ny, nz, d, area);
                q[fa[i]] += pq; q[fb[i]] += pq; q[fc[i]] += pq;
                Count(edgeFaceCount, fa[i], fb[i]); Count(edgeFaceCount, fb[i], fc[i]); Count(edgeFaceCount, fc[i], fa[i]);
            }
            int aliveFaces = 0;
            foreach (var a in alive) if (a) aliveFaces++;

            // ---- boundary preservation: add a perpendicular plane quadric along open edges ----
            for (int i = 0; i < nf; i++)
            {
                if (!alive[i]) continue;
                double nx, ny, nz, area;
                Normal(px, py, pz, fa[i], fb[i], fc[i], out nx, out ny, out nz, out area);
                AddBoundary(edgeFaceCount, q, px, py, pz, fa[i], fb[i], nx, ny, nz, area);
                AddBoundary(edgeFaceCount, q, px, py, pz, fb[i], fc[i], nx, ny, nz, area);
                AddBoundary(edgeFaceCount, q, px, py, pz, fc[i], fa[i], nx, ny, nz, area);
            }

            // ---- initial heap ----
            var ver = new int[nv];
            var dead = new bool[nv];
            var heap = new MinHeap();
            foreach (var kv in edgeFaceCount)
            {
                int a = (int)(kv.Key >> 32), b = (int)(kv.Key & 0xFFFFFFFF);
                heap.Push(MakeItem(q, px, py, pz, ver, a, b));
            }

            // ---- collapse loop ----
            tInit = swTotal.ElapsedMilliseconds;
            pushes = heap.Count;
            var touched = new List<int>();
            while (aliveFaces > targetFaces && heap.Count > 0)
            {
                var it = heap.Pop();
                pops++;
                int a = it.A, b = it.B;
                if (dead[a] || dead[b] || ver[a] != it.VerA || ver[b] != it.VerB) continue;
                flipFaces += vfaces[a].Count + vfaces[b].Count;
                if (vfaces[a].Count > maxDeg) maxDeg = vfaces[a].Count;

                // flip check on faces around a and b that will survive
                bool flip = false;
                for (int pass = 0; pass < 2 && !flip; pass++)
                {
                    int v = pass == 0 ? a : b, other = pass == 0 ? b : a;
                    foreach (var fi in vfaces[v])
                    {
                        if (!alive[fi]) continue;
                        if (Has(fa[fi], fb[fi], fc[fi], other)) continue; // collapses away
                        int i0 = fa[fi], i1 = fb[fi], i2 = fc[fi];
                        double ox, oy, oz, oa;
                        Normal(px, py, pz, i0, i1, i2, out ox, out oy, out oz, out oa);
                        double sx = px[v], sy = py[v], sz = pz[v];
                        px[v] = it.X; py[v] = it.Y; pz[v] = it.Z;
                        double mx, my, mz, ma;
                        Normal(px, py, pz, i0, i1, i2, out mx, out my, out mz, out ma);
                        px[v] = sx; py[v] = sy; pz[v] = sz;
                        if (ma <= 1e-14 || ox * mx + oy * my + oz * mz < 0.15) { flip = true; break; }
                    }
                }
                if (flip) { flips++; continue; }
                collapses++;

                // perform collapse b -> a
                px[a] = it.X; py[a] = it.Y; pz[a] = it.Z;
                q[a] += q[b];
                foreach (var fi in vfaces[b])
                {
                    if (!alive[fi]) continue;
                    if (Has(fa[fi], fb[fi], fc[fi], a))
                    {
                        alive[fi] = false;
                        aliveFaces--;
                        continue;
                    }
                    if (fa[fi] == b) fa[fi] = a;
                    if (fb[fi] == b) fb[fi] = a;
                    if (fc[fi] == b) fc[fi] = a;
                    vfaces[a].Add(fi);
                }
                vfaces[b].Clear();
                dead[b] = true;
                ver[a]++; ver[b]++;
                vfaces[a].RemoveAll(fi => !alive[fi]);

                // re-evaluate edges around a
                touched.Clear();
                foreach (var fi in vfaces[a])
                {
                    if (fa[fi] != a) touched.Add(fa[fi]);
                    if (fb[fi] != a) touched.Add(fb[fi]);
                    if (fc[fi] != a) touched.Add(fc[fi]);
                }
                touched.Sort();
                int prev = -1;
                foreach (var o in touched)
                {
                    if (o == prev || dead[o]) continue;
                    prev = o;
                    heap.Push(MakeItem(q, px, py, pz, ver, a, o));
                    pushes++;
                }
            }
            LastStats = "init " + tInit + " ms, loop " + (swTotal.ElapsedMilliseconds - tInit) + " ms, pops " + pops + ", pushes " + pushes + ", collapses " + collapses + ", flipRejects " + flips + ", flipFaces " + flipFaces + ", maxDeg " + maxDeg + ", heapLeft " + heap.Count;

            // ---- rebuild ----
            var newIndex = new int[nv];
            for (int i = 0; i < nv; i++) newIndex[i] = -1;
            var verts = new List<Vec3>();
            var faces = new List<Face>();
            for (int i = 0; i < nf; i++)
            {
                if (!alive[i]) continue;
                int a = fa[i], b = fb[i], c = fc[i];
                if (a == b || b == c || a == c) continue;
                if (newIndex[a] < 0) { newIndex[a] = verts.Count; verts.Add(new Vec3((float)px[a], (float)py[a], (float)pz[a])); }
                if (newIndex[b] < 0) { newIndex[b] = verts.Count; verts.Add(new Vec3((float)px[b], (float)py[b], (float)pz[b])); }
                if (newIndex[c] < 0) { newIndex[c] = verts.Count; verts.Add(new Vec3((float)px[c], (float)py[c], (float)pz[c])); }
                faces.Add(new Face(new[] { newIndex[a], newIndex[b], newIndex[c] }, parts[i]));
            }
            mesh.Vertices.Clear();
            mesh.Vertices.AddRange(verts);
            mesh.Faces.Clear();
            mesh.Faces.AddRange(faces);
            mesh.Lines.Clear();
            mesh.InvalidateCaches();
        }

        private static bool Has(int a, int b, int c, int v) => a == v || b == v || c == v;

        private static void Count(Dictionary<long, int> map, int a, int b)
        {
            long key = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
            int c;
            map[key] = map.TryGetValue(key, out c) ? c + 1 : 1;
        }

        private static void AddBoundary(Dictionary<long, int> map, Quadric[] q, double[] px, double[] py, double[] pz, int a, int b, double nx, double ny, double nz, double area)
        {
            long key = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
            int c;
            if (!map.TryGetValue(key, out c) || c != 1) return;
            double ex = px[b] - px[a], ey = py[b] - py[a], ez = pz[b] - pz[a];
            // plane containing the edge and the face normal
            double mx = ey * nz - ez * ny, my = ez * nx - ex * nz, mz = ex * ny - ey * nx;
            double len = Math.Sqrt(mx * mx + my * my + mz * mz);
            if (len < 1e-12) return;
            mx /= len; my /= len; mz /= len;
            double d = -(mx * px[a] + my * py[a] + mz * pz[a]);
            var bq = Quadric.FromPlane(mx, my, mz, d, Math.Max(area, 1e-6) * 50);
            q[a] += bq; q[b] += bq;
        }

        private static HeapItem MakeItem(Quadric[] q, double[] px, double[] py, double[] pz, int[] ver, int a, int b)
        {
            var sum = q[a] + q[b];
            double x, y, z;
            if (!sum.Optimal(out x, out y, out z))
            {
                // pick the best of the endpoints and the midpoint
                double mx = (px[a] + px[b]) / 2, my = (py[a] + py[b]) / 2, mz = (pz[a] + pz[b]) / 2;
                double ca = sum.Evaluate(px[a], py[a], pz[a]), cb = sum.Evaluate(px[b], py[b], pz[b]), cm = sum.Evaluate(mx, my, mz);
                if (ca <= cb && ca <= cm) { x = px[a]; y = py[a]; z = pz[a]; }
                else if (cb <= cm) { x = px[b]; y = py[b]; z = pz[b]; }
                else { x = mx; y = my; z = mz; }
            }
            else
            {
                // guard against solutions far from the edge (ill-conditioned quadrics)
                double ex = px[b] - px[a], ey = py[b] - py[a], ez = pz[b] - pz[a];
                double elen = Math.Sqrt(ex * ex + ey * ey + ez * ez) + 1e-9;
                double dx = x - (px[a] + px[b]) / 2, dy = y - (py[a] + py[b]) / 2, dz = z - (pz[a] + pz[b]) / 2;
                if (Math.Sqrt(dx * dx + dy * dy + dz * dz) > elen * 2)
                {
                    x = (px[a] + px[b]) / 2; y = (py[a] + py[b]) / 2; z = (pz[a] + pz[b]) / 2;
                }
            }
            return new HeapItem { Cost = sum.Evaluate(x, y, z), A = a, B = b, VerA = ver[a], VerB = ver[b], X = (float)x, Y = (float)y, Z = (float)z };
        }

        private static void Normal(double[] px, double[] py, double[] pz, int a, int b, int c, out double nx, out double ny, out double nz, out double area)
        {
            double ux = px[b] - px[a], uy = py[b] - py[a], uz = pz[b] - pz[a];
            double vx = px[c] - px[a], vy = py[c] - py[a], vz = pz[c] - pz[a];
            nx = uy * vz - uz * vy; ny = uz * vx - ux * vz; nz = ux * vy - uy * vx;
            double len = Math.Sqrt(nx * nx + ny * ny + nz * nz);
            area = len / 2;
            if (len > 1e-14) { nx /= len; ny /= len; nz /= len; }
            else { nx = ny = nz = 0; }
        }
    }
}
