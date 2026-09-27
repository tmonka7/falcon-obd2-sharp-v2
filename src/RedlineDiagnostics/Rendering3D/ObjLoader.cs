using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace RedlineDiagnostics.Rendering3D
{
    /// <summary>Minimal Wavefront OBJ reader (v / f records, polygons of any size, negative indices).</summary>
    public static class ObjLoader
    {
        public static Mesh Load(string path, float targetLength = 4.85f, int targetFaces = 7000)
        {
            var mesh = new Mesh();
            var ci = CultureInfo.InvariantCulture;
            MeshPart part = MeshPart.Body;
            foreach (var raw in File.ReadLines(path))
            {
                var line = raw.Trim();
                if (line.Length == 0 || line[0] == '#') continue;
                var parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 0) continue;
                switch (parts[0])
                {
                    case "v":
                        if (parts.Length >= 4)
                        {
                            float x, y, z;
                            if (float.TryParse(parts[1], NumberStyles.Float, ci, out x) &&
                                float.TryParse(parts[2], NumberStyles.Float, ci, out y) &&
                                float.TryParse(parts[3], NumberStyles.Float, ci, out z))
                                mesh.AddVertex(x, y, z);
                        }
                        break;
                    case "g":
                    case "o":
                    case "usemtl":
                        {
                            string name = parts.Length > 1 ? string.Join(" ", parts, 1, parts.Length - 1).ToLowerInvariant() : "";
                            if (name.Contains("glass") || name.Contains("window") || name.Contains("windshield")) part = MeshPart.Glass;
                            else if (name.Contains("wheel") || name.Contains("tire") || name.Contains("tyre") || name.Contains("rim")) part = MeshPart.Wheel;
                            else if (parts[0] != "usemtl") part = MeshPart.Body; // a material name alone never overrides the group's part
                        }
                        break;
                    case "f":
                        {
                            var idx = new List<int>();
                            for (int i = 1; i < parts.Length; i++)
                            {
                                var tok = parts[i];
                                int slash = tok.IndexOf('/');
                                if (slash >= 0) tok = tok.Substring(0, slash);
                                int v;
                                if (!int.TryParse(tok, NumberStyles.Integer, ci, out v)) continue;
                                if (v < 0) v = mesh.Vertices.Count + v; else v = v - 1;
                                if (v >= 0 && v < mesh.Vertices.Count) idx.Add(v);
                            }
                            if (idx.Count >= 3) mesh.AddFace(part, idx.ToArray());
                        }
                        break;
                    case "l":
                        {
                            var idx = new List<int>();
                            for (int i = 1; i < parts.Length; i++)
                            {
                                int v;
                                if (!int.TryParse(parts[i], NumberStyles.Integer, ci, out v)) continue;
                                if (v < 0) v = mesh.Vertices.Count + v; else v = v - 1;
                                if (v >= 0 && v < mesh.Vertices.Count) idx.Add(v);
                            }
                            if (idx.Count >= 2) mesh.AddPolyline(MeshPart.Detail, idx.ToArray());
                        }
                        break;
                }
            }
            if (mesh.Vertices.Count == 0) throw new InvalidDataException("OBJ file contains no vertices.");
            mesh.Normalize(targetLength);
            if (mesh.Faces.Count > targetFaces)
            {
                mesh.Decimate(targetFaces);
                mesh.FeatureAngleDegrees = 52f;
            }
            mesh.UseFeatureEdges = true;
            return mesh;
        }
    }
}
