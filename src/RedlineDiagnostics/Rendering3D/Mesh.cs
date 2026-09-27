using System;
using System.Collections.Generic;

namespace RedlineDiagnostics.Rendering3D
{
    public enum MeshPart { Body = 0, Glass = 1, Wheel = 2, Detail = 3 }

    public sealed class Face
    {
        public int[] Indices;
        public MeshPart Part;

        public Face(int[] indices, MeshPart part)
        {
            Indices = indices;
            Part = part;
        }
    }

    public struct Edge
    {
        public int A, B;
        public MeshPart Part;

        public Edge(int a, int b, MeshPart part)
        {
            A = a; B = b; Part = part;
        }
    }

    /// <summary>
    /// Hash comparer for packed 64-bit keys such as edge (a&lt;&lt;32 | b) or grid cells. The default
    /// <c>long.GetHashCode()</c> XORs the two halves, which collapses to a handful of buckets for mesh
    /// edges whose endpoints have nearby indices and makes dictionary operations quadratic.
    /// </summary>
    public sealed class PackedKeyComparer : IEqualityComparer<long>
    {
        public static readonly PackedKeyComparer Instance = new PackedKeyComparer();

        public bool Equals(long x, long y) => x == y;

        public int GetHashCode(long key)
        {
            ulong h = (ulong)key * 0x9E3779B97F4A7C15UL;
            h ^= h >> 29;
            h *= 0xBF58476D1CE4E5B9UL;
            h ^= h >> 32;
            return (int)h;
        }
    }

    /// <summary>Indexed polygon mesh plus free-standing detail lines (seams, arches, spokes).</summary>
    public sealed class Mesh
    {
        public readonly List<Vec3> Vertices = new List<Vec3>();
        public readonly List<Face> Faces = new List<Face>();
        public readonly List<Edge> Lines = new List<Edge>();

        private List<Edge> _edges;
        private List<Edge> _featureEdges;

        /// <summary>
        /// True for imported (triangulated) meshes: draw only silhouette / crease edges instead of every
        /// triangle outline so the model reads as body panels rather than a triangle web.
        /// </summary>
        public bool UseFeatureEdges;

        /// <summary>Crease angle (degrees) above which an edge between two faces is drawn.</summary>
        public float FeatureAngleDegrees = 24f;

        /// <summary>An edge with its two adjacent faces (F1 = -1 on a boundary) and whether it is a crease.</summary>
        public struct EdgeAdj
        {
            public int A, B, F0, F1;
            public MeshPart Part;
            public bool Crease;
        }

        private List<EdgeAdj> _adjacency;

        /// <summary>Every unique edge with face adjacency; creases are pre-computed from the feature angle.</summary>
        public List<EdgeAdj> EdgeAdjacency
        {
            get
            {
                if (_adjacency == null) BuildAdjacency();
                return _adjacency;
            }
        }

        /// <summary>Boundary edges plus edges whose adjacent faces meet at more than the feature angle.</summary>
        public List<Edge> FeatureEdges
        {
            get
            {
                if (_featureEdges == null)
                {
                    _featureEdges = new List<Edge>();
                    foreach (var e in EdgeAdjacency)
                        if (e.Crease || e.F1 < 0) _featureEdges.Add(new Edge(e.A, e.B, e.Part));
                }
                return _featureEdges;
            }
        }

        private void BuildAdjacency()
        {
            var map = new Dictionary<long, int>(PackedKeyComparer.Instance);
            var list = new List<EdgeAdj>();
            for (int fi = 0; fi < Faces.Count; fi++)
            {
                var f = Faces[fi];
                int n = f.Indices.Length;
                for (int i = 0; i < n; i++)
                {
                    int a = f.Indices[i], b = f.Indices[(i + 1) % n];
                    if (a == b) continue;
                    long key = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
                    int idx;
                    if (map.TryGetValue(key, out idx))
                    {
                        var e = list[idx];
                        if (e.F1 < 0) { e.F1 = fi; list[idx] = e; }
                    }
                    else
                    {
                        map[key] = list.Count;
                        list.Add(new EdgeAdj { A = Math.Min(a, b), B = Math.Max(a, b), F0 = fi, F1 = -1, Part = f.Part });
                    }
                }
            }
            var normals = new Vec3[Faces.Count];
            for (int fi = 0; fi < Faces.Count; fi++) normals[fi] = FaceNormal(Faces[fi]);
            float cosLimit = (float)Math.Cos(FeatureAngleDegrees * Math.PI / 180.0);
            for (int i = 0; i < list.Count; i++)
            {
                var e = list[i];
                if (e.F1 >= 0)
                {
                    float d = Vec3.Dot(normals[e.F0], normals[e.F1]);
                    e.Crease = d < cosLimit || Faces[e.F0].Part != Faces[e.F1].Part;
                }
                else e.Crease = true;
                list[i] = e;
            }
            _adjacency = list;
        }

        /// <summary>
        /// Reduces a dense mesh by vertex clustering on a uniform grid. Vertices falling in the same cell are
        /// merged (averaged) and degenerate / duplicate faces are dropped. The grid is coarsened until the face
        /// count drops below <paramref name="targetFaces"/>. Fast and robust; good enough for a holographic
        /// wireframe where silhouette and panel lines matter more than surface smoothness.
        /// </summary>
        public void InvalidateCaches()
        {
            _edges = null;
            _featureEdges = null;
            _adjacency = null;
        }

        /// <summary>Reduces the mesh to about <paramref name="targetFaces"/> faces (quadric edge collapse, clustering as fallback).</summary>
        public void Decimate(int targetFaces)
        {
            if (Faces.Count <= targetFaces || Vertices.Count == 0) return;
            try
            {
                MeshSimplifier.Simplify(this, targetFaces);
                if (Faces.Count <= targetFaces * 1.5) return;
            }
            catch { }
            DecimateByClustering(targetFaces);
        }

        private void DecimateByClustering(int targetFaces)
        {
            if (Faces.Count <= targetFaces || Vertices.Count == 0) return;
            Vec3 min, max;
            GetBounds(out min, out max);
            var size = max - min;
            float longest = Math.Max(size.X, Math.Max(size.Y, size.Z));
            if (longest <= 0) return;

            int cells = 200;
            List<Vec3> bestVerts = null;
            List<Face> bestFaces = null;
            while (cells >= 10)
            {
                float cell = longest / cells;
                var clusterOf = new Dictionary<long, int>(PackedKeyComparer.Instance);
                var sums = new List<Vec3>();
                var counts = new List<int>();
                var remap = new int[Vertices.Count];
                for (int i = 0; i < Vertices.Count; i++)
                {
                    var v = Vertices[i];
                    long ix = (long)((v.X - min.X) / cell), iy = (long)((v.Y - min.Y) / cell), iz = (long)((v.Z - min.Z) / cell);
                    long key = (ix << 42) | (iy << 21) | iz;
                    int id;
                    if (!clusterOf.TryGetValue(key, out id))
                    {
                        id = sums.Count;
                        clusterOf[key] = id;
                        sums.Add(Vec3.Zero);
                        counts.Add(0);
                    }
                    sums[id] = sums[id] + v;
                    counts[id]++;
                    remap[i] = id;
                }
                var newVerts = new List<Vec3>(sums.Count);
                for (int i = 0; i < sums.Count; i++) newVerts.Add(sums[i] / counts[i]);

                var seen = new HashSet<long>(PackedKeyComparer.Instance);
                var newFaces = new List<Face>();
                foreach (var f in Faces)
                {
                    var idx = new List<int>(f.Indices.Length);
                    foreach (var vi in f.Indices)
                    {
                        int r = remap[vi];
                        if (idx.Count == 0 || idx[idx.Count - 1] != r) idx.Add(r);
                    }
                    while (idx.Count > 1 && idx[0] == idx[idx.Count - 1]) idx.RemoveAt(idx.Count - 1);
                    if (idx.Count < 3) continue;
                    if (idx.Count == 3)
                    {
                        int a = idx[0], b = idx[1], c = idx[2];
                        int lo = Math.Min(a, Math.Min(b, c)), hi = Math.Max(a, Math.Max(b, c));
                        int mid = a + b + c - lo - hi;
                        long key = ((long)lo << 42) | ((long)mid << 21) | (long)hi;
                        if (!seen.Add(key)) continue;
                    }
                    newFaces.Add(new Face(idx.ToArray(), f.Part));
                }
                bestVerts = newVerts;
                bestFaces = newFaces;
                if (newFaces.Count <= targetFaces) break;
                cells = (int)(cells * 0.82);
            }
            if (bestVerts == null) return;
            Vertices.Clear();
            Vertices.AddRange(bestVerts);
            Faces.Clear();
            Faces.AddRange(bestFaces);
            Lines.Clear();
            _edges = null;
            _featureEdges = null;
            _adjacency = null;
        }

        /// <summary>
        /// Denser copy of an imported model without interior parts, used for the opaque studio renders on the
        /// Home page (null when the mesh was not simplified; use the mesh itself then).
        /// </summary>
        public Mesh Detailed;

        /// <summary>Copies the faces accepted by <paramref name="keep"/> into a new mesh with compacted vertices.</summary>
        public Mesh Clone(Func<Face, bool> keep = null)
        {
            var m = new Mesh { UseFeatureEdges = UseFeatureEdges, FeatureAngleDegrees = FeatureAngleDegrees };
            var map = new Dictionary<int, int>();
            foreach (var f in Faces)
            {
                if (keep != null && !keep(f)) continue;
                var idx = new int[f.Indices.Length];
                for (int i = 0; i < idx.Length; i++)
                {
                    int ni;
                    if (!map.TryGetValue(f.Indices[i], out ni)) { ni = m.AddVertex(Vertices[f.Indices[i]]); map[f.Indices[i]] = ni; }
                    idx[i] = ni;
                }
                m.AddFace(f.Part, idx);
            }
            return m;
        }

        /// <summary>
        /// Simplifies to <paramref name="detailFaces"/>, keeps an exterior-only copy in <see cref="Detailed"/>, then
        /// continues down to <paramref name="targetFaces"/> for the real-time views. The expensive first pass is shared.
        /// </summary>
        public void DecimateWithDetail(int targetFaces, int detailFaces)
        {
            if (Faces.Count > detailFaces) Decimate(detailFaces);
            var exterior = Clone(f => f.Part != MeshPart.Detail);
            Detailed = exterior.Faces.Count > 0 ? exterior : null;
            Decimate(targetFaces);
        }

        public int AddVertex(Vec3 v)
        {
            Vertices.Add(v);
            return Vertices.Count - 1;
        }

        public int AddVertex(float x, float y, float z) => AddVertex(new Vec3(x, y, z));

        public void AddFace(MeshPart part, params int[] indices)
        {
            if (indices.Length < 3) return;
            Faces.Add(new Face(indices, part));
            _edges = null;
        }

        public void AddLine(int a, int b, MeshPart part = MeshPart.Detail)
        {
            Lines.Add(new Edge(a, b, part));
        }

        public void AddPolyline(MeshPart part, params int[] indices)
        {
            for (int i = 0; i + 1 < indices.Length; i++) AddLine(indices[i], indices[i + 1], part);
        }

        /// <summary>Unique edges derived from faces (cached).</summary>
        public List<Edge> Edges
        {
            get
            {
                if (_edges == null) BuildEdges();
                return _edges;
            }
        }

        private void BuildEdges()
        {
            var seen = new HashSet<long>(PackedKeyComparer.Instance);
            _edges = new List<Edge>();
            foreach (var f in Faces)
            {
                int n = f.Indices.Length;
                for (int i = 0; i < n; i++)
                {
                    int a = f.Indices[i], b = f.Indices[(i + 1) % n];
                    if (a == b) continue;
                    long key = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
                    if (seen.Add(key)) _edges.Add(new Edge(a, b, f.Part));
                }
            }
        }

        public void GetBounds(out Vec3 min, out Vec3 max)
        {
            min = new Vec3(float.MaxValue, float.MaxValue, float.MaxValue);
            max = new Vec3(float.MinValue, float.MinValue, float.MinValue);
            foreach (var v in Vertices)
            {
                min = Vec3.Min(min, v);
                max = Vec3.Max(max, v);
            }
            if (Vertices.Count == 0) min = max = Vec3.Zero;
        }

        /// <summary>Centre the mesh on X/Z, rest it on Y = 0 and scale its longest axis to <paramref name="targetLength"/>.</summary>
        public void Normalize(float targetLength)
        {
            if (Vertices.Count == 0) return;
            Vec3 min, max;
            GetBounds(out min, out max);
            var size = max - min;
            // Make the longest horizontal axis run along X (vehicle length).
            if (size.Z > size.X)
            {
                for (int i = 0; i < Vertices.Count; i++)
                {
                    var v = Vertices[i];
                    Vertices[i] = new Vec3(v.Z, v.Y, -v.X);
                }
                GetBounds(out min, out max);
                size = max - min;
            }
            float longest = Math.Max(size.X, Math.Max(size.Y, size.Z));
            if (longest < 1e-6f) return;
            float s = targetLength / longest;
            var centre = new Vec3((min.X + max.X) / 2, min.Y, (min.Z + max.Z) / 2);
            for (int i = 0; i < Vertices.Count; i++)
                Vertices[i] = (Vertices[i] - centre) * s;
            _edges = null;
            _featureEdges = null;
            _adjacency = null;
        }

        /// <summary>Rotate 180° around Y (swap front and back) for models authored facing the other way.</summary>
        public void FlipFrontBack()
        {
            for (int i = 0; i < Vertices.Count; i++)
            {
                var v = Vertices[i];
                Vertices[i] = new Vec3(-v.X, v.Y, -v.Z);
            }
            _edges = null;
            _featureEdges = null;
            _adjacency = null;
        }

        public Vec3 FaceNormal(Face f)
        {
            if (f.Indices.Length < 3) return Vec3.UnitY;
            var a = Vertices[f.Indices[0]];
            var b = Vertices[f.Indices[1]];
            var c = Vertices[f.Indices[2]];
            return Vec3.Cross(b - a, c - a).Normalized();
        }
    }
}
