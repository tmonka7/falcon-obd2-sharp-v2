using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;

namespace RedlineDiagnostics.Rendering3D
{
    /// <summary>
    /// glTF 2.0 reader (.glb binary and .gltf JSON with external or data-URI buffers). Only geometry is
    /// imported: node hierarchy with transforms, triangle primitives (lists, strips, fans) and part
    /// classification from node / mesh / material names. Materials, textures, skins and animations are ignored.
    /// </summary>
    public static class GltfLoader
    {
        private const uint GlbMagic = 0x46546C67;   // "glTF"
        private const uint ChunkJson = 0x4E4F534A;  // "JSON"
        private const uint ChunkBin = 0x004E4942;   // "BIN\0"

        private static readonly string[] SkipWords = { "wiper", "gasket", "pedal", "emblem", "license", "floormat", "handle", "hardware", "bolt", "nut", "logo", "plate", "badge" };
        private static readonly string[] GlassWords = { "glass", "window", "windshield", "windscreen" };
        private static readonly string[] WheelWords = { "wheel", "tire", "tyre", "rim", "brake", "disc" };
        private static readonly string[] DetailWords = { "interior", "seat", "steering", "dash", "engine", "cage", "floor", "mirror", "exhaust", "suspension", "chassis", "underside" };

        public static Mesh Load(string path, float targetLength = 4.85f, int targetFaces = 7000)
        {
            var bytes = File.ReadAllBytes(path);
            string json;
            byte[] bin = null;
            if (bytes.Length >= 12 && BitConverter.ToUInt32(bytes, 0) == GlbMagic)
            {
                int pos = 12;
                json = null;
                while (pos + 8 <= bytes.Length)
                {
                    uint len = BitConverter.ToUInt32(bytes, pos);
                    uint type = BitConverter.ToUInt32(bytes, pos + 4);
                    pos += 8;
                    if (pos + len > bytes.Length) break;
                    if (type == ChunkJson) json = Encoding.UTF8.GetString(bytes, pos, (int)len);
                    else if (type == ChunkBin && bin == null)
                    {
                        bin = new byte[len];
                        Buffer.BlockCopy(bytes, pos, bin, 0, (int)len);
                    }
                    pos += (int)len;
                }
                if (json == null) throw new InvalidDataException("GLB file has no JSON chunk.");
            }
            else json = Encoding.UTF8.GetString(bytes);

            var ser = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
            var root = ser.DeserializeObject(json) as Dictionary<string, object>;
            if (root == null) throw new InvalidDataException("Not a glTF document.");

            var ctx = new Context
            {
                Root = root,
                BaseDir = Path.GetDirectoryName(path) ?? ".",
                Buffers = LoadBuffers(root, bin, Path.GetDirectoryName(path) ?? "."),
                Mesh = new Mesh()
            };

            var nodes = Arr(root, "nodes");
            var scenes = Arr(root, "scenes");
            var roots = new List<int>();
            if (scenes != null && scenes.Length > 0)
            {
                int sceneIdx = Int(root, "scene", 0);
                var scene = scenes[Math.Min(sceneIdx, scenes.Length - 1)] as Dictionary<string, object>;
                var sn = Arr(scene, "nodes");
                if (sn != null) foreach (var n in sn) roots.Add(Convert.ToInt32(n));
            }
            if (roots.Count == 0 && nodes != null)
            {
                var isChild = new HashSet<int>();
                foreach (var n in nodes)
                {
                    var ch = Arr(n as Dictionary<string, object>, "children");
                    if (ch != null) foreach (var c in ch) isChild.Add(Convert.ToInt32(c));
                }
                for (int i = 0; i < nodes.Length; i++) if (!isChild.Contains(i)) roots.Add(i);
            }
            foreach (var r in roots) Visit(ctx, r, Mat4.Identity, 0);

            var mesh = ctx.Mesh;
            if (mesh.Vertices.Count == 0) throw new InvalidDataException("glTF file contains no triangle geometry.");
            mesh.Normalize(targetLength);
            if (mesh.Faces.Count > targetFaces)
            {
                mesh.Decimate(targetFaces);
                mesh.FeatureAngleDegrees = 52f;
            }
            mesh.UseFeatureEdges = true;
            return mesh;
        }

        private sealed class Context
        {
            public Dictionary<string, object> Root;
            public string BaseDir;
            public List<byte[]> Buffers;
            public Mesh Mesh;
        }

        private static List<byte[]> LoadBuffers(Dictionary<string, object> root, byte[] glbBin, string baseDir)
        {
            var list = new List<byte[]>();
            var buffers = Arr(root, "buffers");
            if (buffers == null) return list;
            foreach (var b in buffers)
            {
                var d = b as Dictionary<string, object>;
                string uri = d != null && d.ContainsKey("uri") ? d["uri"] as string : null;
                if (uri == null) { list.Add(glbBin ?? new byte[0]); continue; }
                if (uri.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
                {
                    int comma = uri.IndexOf(',');
                    list.Add(Convert.FromBase64String(uri.Substring(comma + 1)));
                }
                else
                {
                    var p = Path.Combine(baseDir, Uri.UnescapeDataString(uri));
                    list.Add(File.Exists(p) ? File.ReadAllBytes(p) : new byte[0]);
                }
            }
            return list;
        }

        private static void Visit(Context ctx, int nodeIdx, Mat4 parent, int depth)
        {
            if (depth > 64) return;
            var nodes = Arr(ctx.Root, "nodes");
            if (nodes == null || nodeIdx < 0 || nodeIdx >= nodes.Length) return;
            var node = nodes[nodeIdx] as Dictionary<string, object>;
            if (node == null) return;
            var local = LocalMatrix(node);
            var world = local * parent;
            string nodeName = Str(node, "name");

            if (node.ContainsKey("mesh"))
            {
                var meshes = Arr(ctx.Root, "meshes");
                int mi = Convert.ToInt32(node["mesh"]);
                if (meshes != null && mi >= 0 && mi < meshes.Length)
                    ImportMesh(ctx, meshes[mi] as Dictionary<string, object>, nodeName, world);
            }
            var children = Arr(node, "children");
            if (children != null)
                foreach (var c in children) Visit(ctx, Convert.ToInt32(c), world, depth + 1);
        }

        private static Mat4 LocalMatrix(Dictionary<string, object> node)
        {
            var m = Arr(node, "matrix");
            if (m != null && m.Length == 16)
            {
                var a = new float[16];
                for (int i = 0; i < 16; i++) a[i] = Convert.ToSingle(m[i]);
                return FromColumnMajor(a);
            }
            var result = Mat4.Identity;
            var s = Arr(node, "scale");
            if (s != null && s.Length == 3)
            {
                var sm = Mat4.Identity;
                sm.M11 = Convert.ToSingle(s[0]); sm.M22 = Convert.ToSingle(s[1]); sm.M33 = Convert.ToSingle(s[2]);
                result = result * sm;
            }
            var r = Arr(node, "rotation");
            if (r != null && r.Length == 4)
            {
                float x = Convert.ToSingle(r[0]), y = Convert.ToSingle(r[1]), z = Convert.ToSingle(r[2]), w = Convert.ToSingle(r[3]);
                var a = new float[16];
                a[0] = 1 - 2 * (y * y + z * z); a[1] = 2 * (x * y + w * z); a[2] = 2 * (x * z - w * y);
                a[4] = 2 * (x * y - w * z); a[5] = 1 - 2 * (x * x + z * z); a[6] = 2 * (y * z + w * x);
                a[8] = 2 * (x * z + w * y); a[9] = 2 * (y * z - w * x); a[10] = 1 - 2 * (x * x + y * y);
                a[15] = 1;
                result = result * FromColumnMajor(a);
            }
            var t = Arr(node, "translation");
            if (t != null && t.Length == 3)
                result = result * Mat4.Translation(Convert.ToSingle(t[0]), Convert.ToSingle(t[1]), Convert.ToSingle(t[2]));
            return result;
        }

        /// <summary>glTF column-major array to our row-vector matrix (i.e. the transpose, filled sequentially).</summary>
        private static Mat4 FromColumnMajor(float[] a)
        {
            var m = new Mat4();
            m.M11 = a[0]; m.M12 = a[1]; m.M13 = a[2]; m.M14 = a[3];
            m.M21 = a[4]; m.M22 = a[5]; m.M23 = a[6]; m.M24 = a[7];
            m.M31 = a[8]; m.M32 = a[9]; m.M33 = a[10]; m.M34 = a[11];
            m.M41 = a[12]; m.M42 = a[13]; m.M43 = a[14]; m.M44 = a[15];
            return m;
        }

        private static void ImportMesh(Context ctx, Dictionary<string, object> gmesh, string nodeName, Mat4 world)
        {
            if (gmesh == null) return;
            var prims = Arr(gmesh, "primitives");
            if (prims == null) return;
            string meshName = Str(gmesh, "name");
            var materials = Arr(ctx.Root, "materials");
            foreach (var po in prims)
            {
                var p = po as Dictionary<string, object>;
                if (p == null) continue;
                int mode = Int(p, "mode", 4);
                if (mode != 4 && mode != 5 && mode != 6) continue;
                var attrs = p.ContainsKey("attributes") ? p["attributes"] as Dictionary<string, object> : null;
                if (attrs == null || !attrs.ContainsKey("POSITION")) continue;

                string matName = "";
                if (p.ContainsKey("material") && materials != null)
                {
                    int mi = Convert.ToInt32(p["material"]);
                    if (mi >= 0 && mi < materials.Length) matName = Str(materials[mi] as Dictionary<string, object>, "name");
                }
                var part = Classify(nodeName + "|" + meshName + "|" + matName, out bool skip);
                if (skip) continue;

                var positions = ReadVec3(ctx, Convert.ToInt32(attrs["POSITION"]));
                if (positions == null || positions.Length == 0) continue;
                int[] indices = p.ContainsKey("indices") ? ReadIndices(ctx, Convert.ToInt32(p["indices"])) : null;
                if (indices == null)
                {
                    indices = new int[positions.Length];
                    for (int i = 0; i < indices.Length; i++) indices[i] = i;
                }

                int baseIdx = ctx.Mesh.Vertices.Count;
                foreach (var v in positions) ctx.Mesh.AddVertex(world.TransformPoint(v));

                if (mode == 4)
                {
                    for (int i = 0; i + 2 < indices.Length; i += 3)
                        AddTri(ctx.Mesh, part, baseIdx, indices[i], indices[i + 1], indices[i + 2], positions.Length);
                }
                else if (mode == 5)
                {
                    for (int i = 0; i + 2 < indices.Length; i++)
                    {
                        if (i % 2 == 0) AddTri(ctx.Mesh, part, baseIdx, indices[i], indices[i + 1], indices[i + 2], positions.Length);
                        else AddTri(ctx.Mesh, part, baseIdx, indices[i + 1], indices[i], indices[i + 2], positions.Length);
                    }
                }
                else
                {
                    for (int i = 1; i + 1 < indices.Length; i++)
                        AddTri(ctx.Mesh, part, baseIdx, indices[0], indices[i], indices[i + 1], positions.Length);
                }
            }
        }

        private static void AddTri(Mesh mesh, MeshPart part, int baseIdx, int a, int b, int c, int count)
        {
            if (a < 0 || b < 0 || c < 0 || a >= count || b >= count || c >= count) return;
            if (a == b || b == c || a == c) return;
            mesh.AddFace(part, baseIdx + a, baseIdx + b, baseIdx + c);
        }

        private static MeshPart Classify(string names, out bool skip)
        {
            var n = names.ToLowerInvariant();
            skip = false;
            foreach (var w in SkipWords) if (n.Contains(w)) { skip = true; return MeshPart.Detail; }
            foreach (var w in GlassWords) if (n.Contains(w)) return MeshPart.Glass;
            foreach (var w in WheelWords) if (n.Contains(w)) return MeshPart.Wheel;
            foreach (var w in DetailWords) if (n.Contains(w)) return MeshPart.Detail;
            return MeshPart.Body;
        }

        // ------------------------------------------------------------------ accessors

        private static Vec3[] ReadVec3(Context ctx, int accessorIdx)
        {
            var acc = GetAccessor(ctx, accessorIdx);
            if (acc == null) return null;
            int compType = Int(acc, "componentType", 5126);
            int count = Int(acc, "count", 0);
            if (Str(acc, "type") != "VEC3" || compType != 5126 || count == 0) return null;
            byte[] data; int offset, stride;
            if (!Locate(ctx, acc, 12, out data, out offset, out stride)) return null;
            var result = new Vec3[count];
            for (int i = 0; i < count; i++)
            {
                int o = offset + i * stride;
                if (o + 12 > data.Length) break;
                result[i] = new Vec3(BitConverter.ToSingle(data, o), BitConverter.ToSingle(data, o + 4), BitConverter.ToSingle(data, o + 8));
            }
            return result;
        }

        private static int[] ReadIndices(Context ctx, int accessorIdx)
        {
            var acc = GetAccessor(ctx, accessorIdx);
            if (acc == null) return null;
            int compType = Int(acc, "componentType", 5125);
            int count = Int(acc, "count", 0);
            int size = compType == 5121 ? 1 : compType == 5123 ? 2 : 4;
            byte[] data; int offset, stride;
            if (!Locate(ctx, acc, size, out data, out offset, out stride)) return null;
            var result = new int[count];
            for (int i = 0; i < count; i++)
            {
                int o = offset + i * stride;
                if (o + size > data.Length) break;
                switch (size)
                {
                    case 1: result[i] = data[o]; break;
                    case 2: result[i] = BitConverter.ToUInt16(data, o); break;
                    default: result[i] = (int)Math.Min(int.MaxValue, BitConverter.ToUInt32(data, o)); break;
                }
            }
            return result;
        }

        private static Dictionary<string, object> GetAccessor(Context ctx, int idx)
        {
            var accs = Arr(ctx.Root, "accessors");
            return accs != null && idx >= 0 && idx < accs.Length ? accs[idx] as Dictionary<string, object> : null;
        }

        private static bool Locate(Context ctx, Dictionary<string, object> acc, int elementSize, out byte[] data, out int offset, out int stride)
        {
            data = null; offset = 0; stride = elementSize;
            if (!acc.ContainsKey("bufferView")) return false;
            var views = Arr(ctx.Root, "bufferViews");
            int vi = Convert.ToInt32(acc["bufferView"]);
            if (views == null || vi < 0 || vi >= views.Length) return false;
            var view = views[vi] as Dictionary<string, object>;
            int bi = Int(view, "buffer", 0);
            if (bi < 0 || bi >= ctx.Buffers.Count) return false;
            data = ctx.Buffers[bi];
            offset = Int(view, "byteOffset", 0) + Int(acc, "byteOffset", 0);
            int bs = Int(view, "byteStride", 0);
            stride = bs > 0 ? bs : elementSize;
            return true;
        }

        // ------------------------------------------------------------------ JSON helpers

        private static object[] Arr(Dictionary<string, object> d, string key)
        {
            object o;
            return d != null && d.TryGetValue(key, out o) ? o as object[] : null;
        }

        private static int Int(Dictionary<string, object> d, string key, int def)
        {
            object o;
            if (d == null || !d.TryGetValue(key, out o) || o == null) return def;
            try { return Convert.ToInt32(o); } catch { return def; }
        }

        private static string Str(Dictionary<string, object> d, string key)
        {
            object o;
            return d != null && d.TryGetValue(key, out o) && o != null ? o.ToString() : "";
        }
    }
}
