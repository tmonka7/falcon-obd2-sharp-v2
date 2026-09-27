using System;

namespace RedlineDiagnostics.Rendering3D
{
    public struct Vec3
    {
        public float X, Y, Z;

        public Vec3(float x, float y, float z) { X = x; Y = y; Z = z; }

        public static readonly Vec3 Zero = new Vec3(0, 0, 0);
        public static readonly Vec3 UnitY = new Vec3(0, 1, 0);

        public static Vec3 operator +(Vec3 a, Vec3 b) => new Vec3(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
        public static Vec3 operator -(Vec3 a, Vec3 b) => new Vec3(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
        public static Vec3 operator -(Vec3 a) => new Vec3(-a.X, -a.Y, -a.Z);
        public static Vec3 operator *(Vec3 a, float s) => new Vec3(a.X * s, a.Y * s, a.Z * s);
        public static Vec3 operator *(float s, Vec3 a) => new Vec3(a.X * s, a.Y * s, a.Z * s);
        public static Vec3 operator /(Vec3 a, float s) => new Vec3(a.X / s, a.Y / s, a.Z / s);

        public float Length => (float)Math.Sqrt(X * X + Y * Y + Z * Z);

        public Vec3 Normalized()
        {
            float l = Length;
            return l < 1e-6f ? this : this / l;
        }

        public static float Dot(Vec3 a, Vec3 b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;

        public static Vec3 Cross(Vec3 a, Vec3 b) =>
            new Vec3(a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X);

        public static Vec3 Lerp(Vec3 a, Vec3 b, float t) => a + (b - a) * t;

        public static Vec3 Min(Vec3 a, Vec3 b) => new Vec3(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y), Math.Min(a.Z, b.Z));
        public static Vec3 Max(Vec3 a, Vec3 b) => new Vec3(Math.Max(a.X, b.X), Math.Max(a.Y, b.Y), Math.Max(a.Z, b.Z));

        public override string ToString() => $"({X:0.00}, {Y:0.00}, {Z:0.00})";
    }

    /// <summary>Row-major 4x4 matrix, vectors are treated as rows (v * M).</summary>
    public struct Mat4
    {
        public float M11, M12, M13, M14;
        public float M21, M22, M23, M24;
        public float M31, M32, M33, M34;
        public float M41, M42, M43, M44;

        public static Mat4 Identity
        {
            get
            {
                var m = new Mat4();
                m.M11 = m.M22 = m.M33 = m.M44 = 1;
                return m;
            }
        }

        public static Mat4 RotationX(float rad)
        {
            float c = (float)Math.Cos(rad), s = (float)Math.Sin(rad);
            var m = Identity;
            m.M22 = c; m.M23 = s;
            m.M32 = -s; m.M33 = c;
            return m;
        }

        public static Mat4 RotationY(float rad)
        {
            float c = (float)Math.Cos(rad), s = (float)Math.Sin(rad);
            var m = Identity;
            m.M11 = c; m.M13 = -s;
            m.M31 = s; m.M33 = c;
            return m;
        }

        public static Mat4 RotationZ(float rad)
        {
            float c = (float)Math.Cos(rad), s = (float)Math.Sin(rad);
            var m = Identity;
            m.M11 = c; m.M12 = s;
            m.M21 = -s; m.M22 = c;
            return m;
        }

        public static Mat4 Translation(float x, float y, float z)
        {
            var m = Identity;
            m.M41 = x; m.M42 = y; m.M43 = z;
            return m;
        }

        public static Mat4 Scale(float s)
        {
            var m = Identity;
            m.M11 = m.M22 = m.M33 = s;
            return m;
        }

        public static Mat4 operator *(Mat4 a, Mat4 b)
        {
            var r = new Mat4();
            r.M11 = a.M11 * b.M11 + a.M12 * b.M21 + a.M13 * b.M31 + a.M14 * b.M41;
            r.M12 = a.M11 * b.M12 + a.M12 * b.M22 + a.M13 * b.M32 + a.M14 * b.M42;
            r.M13 = a.M11 * b.M13 + a.M12 * b.M23 + a.M13 * b.M33 + a.M14 * b.M43;
            r.M14 = a.M11 * b.M14 + a.M12 * b.M24 + a.M13 * b.M34 + a.M14 * b.M44;
            r.M21 = a.M21 * b.M11 + a.M22 * b.M21 + a.M23 * b.M31 + a.M24 * b.M41;
            r.M22 = a.M21 * b.M12 + a.M22 * b.M22 + a.M23 * b.M32 + a.M24 * b.M42;
            r.M23 = a.M21 * b.M13 + a.M22 * b.M23 + a.M23 * b.M33 + a.M24 * b.M43;
            r.M24 = a.M21 * b.M14 + a.M22 * b.M24 + a.M23 * b.M34 + a.M24 * b.M44;
            r.M31 = a.M31 * b.M11 + a.M32 * b.M21 + a.M33 * b.M31 + a.M34 * b.M41;
            r.M32 = a.M31 * b.M12 + a.M32 * b.M22 + a.M33 * b.M32 + a.M34 * b.M42;
            r.M33 = a.M31 * b.M13 + a.M32 * b.M23 + a.M33 * b.M33 + a.M34 * b.M43;
            r.M34 = a.M31 * b.M14 + a.M32 * b.M24 + a.M33 * b.M34 + a.M34 * b.M44;
            r.M41 = a.M41 * b.M11 + a.M42 * b.M21 + a.M43 * b.M31 + a.M44 * b.M41;
            r.M42 = a.M41 * b.M12 + a.M42 * b.M22 + a.M43 * b.M32 + a.M44 * b.M42;
            r.M43 = a.M41 * b.M13 + a.M42 * b.M23 + a.M43 * b.M33 + a.M44 * b.M43;
            r.M44 = a.M41 * b.M14 + a.M42 * b.M24 + a.M43 * b.M34 + a.M44 * b.M44;
            return r;
        }

        /// <summary>Transform a point (w = 1), ignoring the projective component.</summary>
        public Vec3 TransformPoint(Vec3 v)
        {
            return new Vec3(
                v.X * M11 + v.Y * M21 + v.Z * M31 + M41,
                v.X * M12 + v.Y * M22 + v.Z * M32 + M42,
                v.X * M13 + v.Y * M23 + v.Z * M33 + M43);
        }

        /// <summary>Transform a direction (w = 0).</summary>
        public Vec3 TransformNormal(Vec3 v)
        {
            return new Vec3(
                v.X * M11 + v.Y * M21 + v.Z * M31,
                v.X * M12 + v.Y * M22 + v.Z * M32,
                v.X * M13 + v.Y * M23 + v.Z * M33);
        }
    }
}
