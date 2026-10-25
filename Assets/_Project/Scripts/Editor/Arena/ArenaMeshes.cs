using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace SlapMania.Editor.Arena
{
    /// Procedural geometry for the generated arena (M15, ADR-0012). Angles run around the ring
    /// centre: position = (sin a * r, y, cos a * r). Every quad is wound against the normal it
    /// is meant to face, so surfaces point the right way by construction.
    internal sealed class MeshBuilder
    {
        private readonly List<Vector3> _v = new List<Vector3>();
        private readonly List<Vector3> _n = new List<Vector3>();
        private readonly List<Vector2> _uv = new List<Vector2>();
        private readonly List<Vector2> _uv2 = new List<Vector2>();
        private readonly List<int> _t = new List<int>();

        public int VertexCount => _v.Count;

        public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 normal,
                         Vector2 ua, Vector2 ub, Vector2 uc, Vector2 ud, Vector2 uv2 = default)
        {
            int i = _v.Count;
            _v.Add(a); _v.Add(b); _v.Add(c); _v.Add(d);
            for (int k = 0; k < 4; k++) { _n.Add(normal); _uv2.Add(uv2); }
            _uv.Add(ua); _uv.Add(ub); _uv.Add(uc); _uv.Add(ud);
            if (Vector3.Dot(Vector3.Cross(b - a, c - a), normal) >= 0f)
            { _t.Add(i); _t.Add(i + 1); _t.Add(i + 2); _t.Add(i); _t.Add(i + 2); _t.Add(i + 3); }
            else
            { _t.Add(i); _t.Add(i + 2); _t.Add(i + 1); _t.Add(i); _t.Add(i + 3); _t.Add(i + 2); }
        }

        /// A quad with per-vertex normals (smooth cylinders and cones).
        public void QuadSmooth(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 na, Vector3 nb, Vector3 nc, Vector3 nd,
                               Vector2 ua, Vector2 ub, Vector2 uc, Vector2 ud)
        {
            int i = _v.Count;
            _v.Add(a); _v.Add(b); _v.Add(c); _v.Add(d);
            _n.Add(na); _n.Add(nb); _n.Add(nc); _n.Add(nd);
            _uv.Add(ua); _uv.Add(ub); _uv.Add(uc); _uv.Add(ud);
            for (int k = 0; k < 4; k++) _uv2.Add(Vector2.zero);
            Vector3 avg = na + nb + nc + nd;
            if (Vector3.Dot(Vector3.Cross(b - a, c - a), avg) >= 0f)
            { _t.Add(i); _t.Add(i + 1); _t.Add(i + 2); _t.Add(i); _t.Add(i + 2); _t.Add(i + 3); }
            else
            { _t.Add(i); _t.Add(i + 2); _t.Add(i + 1); _t.Add(i); _t.Add(i + 3); _t.Add(i + 2); }
        }

        public void Box(Vector3 center, Vector3 size, Quaternion rot)
        {
            Vector3 h = size * 0.5f;
            Vector3 P(float x, float y, float z) => center + rot * new Vector3(x * h.x, y * h.y, z * h.z);
            Vector3 N(Vector3 n) => rot * n;
            Vector2 o = Vector2.zero, x1 = Vector2.right, y1 = Vector2.up, xy = Vector2.one;
            Quad(P(-1, -1, 1), P(1, -1, 1), P(1, 1, 1), P(-1, 1, 1), N(Vector3.forward), o, x1, xy, y1);
            Quad(P(1, -1, -1), P(-1, -1, -1), P(-1, 1, -1), P(1, 1, -1), N(Vector3.back), o, x1, xy, y1);
            Quad(P(1, -1, 1), P(1, -1, -1), P(1, 1, -1), P(1, 1, 1), N(Vector3.right), o, x1, xy, y1);
            Quad(P(-1, -1, -1), P(-1, -1, 1), P(-1, 1, 1), P(-1, 1, -1), N(Vector3.left), o, x1, xy, y1);
            Quad(P(-1, 1, 1), P(1, 1, 1), P(1, 1, -1), P(-1, 1, -1), N(Vector3.up), o, x1, xy, y1);
            Quad(P(-1, -1, -1), P(1, -1, -1), P(1, -1, 1), P(-1, -1, 1), N(Vector3.down), o, x1, xy, y1);
        }

        /// A box spanning from `a` to `b` with a square cross-section.
        public void Beam(Vector3 a, Vector3 b, float thickness)
        {
            Vector3 d = b - a;
            float len = d.magnitude;
            if (len < 1e-4f) return;
            Box((a + b) * 0.5f, new Vector3(thickness, thickness, len), Quaternion.LookRotation(d / len, Mathf.Abs(d.y / len) > 0.95f ? Vector3.forward : Vector3.up));
        }

        /// A capped cylinder (prism) from `a` along `dir`.
        public void Cylinder(Vector3 a, Vector3 dir, float length, float radius, int sides)
        {
            dir.Normalize();
            var rot = Quaternion.LookRotation(dir, Mathf.Abs(dir.y) > 0.95f ? Vector3.forward : Vector3.up);
            Vector3 b = a + dir * length;
            for (int i = 0; i < sides; i++)
            {
                float a0 = i * Mathf.PI * 2f / sides, a1 = (i + 1) * Mathf.PI * 2f / sides;
                Vector3 r0 = rot * new Vector3(Mathf.Cos(a0), Mathf.Sin(a0), 0f) * radius;
                Vector3 r1 = rot * new Vector3(Mathf.Cos(a1), Mathf.Sin(a1), 0f) * radius;
                QuadSmooth(a + r0, a + r1, b + r1, b + r0, r0.normalized, r1.normalized, r1.normalized, r0.normalized,
                           new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1));
                Quad(a, a + r0, a + r1, a, -dir, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
            }
        }

        public Mesh ToMesh(string name)
        {
            var m = new Mesh { name = name };
            if (_v.Count > 65000) m.indexFormat = IndexFormat.UInt32;
            m.SetVertices(_v);
            m.SetNormals(_n);
            m.SetUVs(0, _uv);
            m.SetUVs(1, _uv2);
            m.SetTriangles(_t, 0);
            m.RecalculateBounds();
            m.RecalculateTangents();
            return m;
        }
    }

    internal static class ArenaMeshes
    {
        public static Vector3 Around(float angle, float radius, float y) =>
            new Vector3(Mathf.Sin(angle) * radius, y, Mathf.Cos(angle) * radius);

        private static Vector3 Inward(float angle) => new Vector3(-Mathf.Sin(angle), 0f, -Mathf.Cos(angle));

        /// Flat disc on the floor, planar UVs (0..1 across the diameter).
        public static Mesh Disc(string name, float radius, int segs)
        {
            var mb = new MeshBuilder();
            for (int i = 0; i < segs; i++)
            {
                float a0 = i * Mathf.PI * 2f / segs, a1 = (i + 1) * Mathf.PI * 2f / segs;
                Vector3 p0 = Around(a0, radius, 0f), p1 = Around(a1, radius, 0f);
                Vector2 U(Vector3 p) => new Vector2(p.x / (2f * radius) + 0.5f, p.z / (2f * radius) + 0.5f);
                mb.Quad(Vector3.zero, p0, p1, Vector3.zero, Vector3.up, new Vector2(0.5f, 0.5f), U(p0), U(p1), new Vector2(0.5f, 0.5f));
            }
            return mb.ToMesh(name);
        }

        /// Vertical ring band facing the ring centre. u wraps `uTiles` times around.
        public static Mesh InnerBand(string name, float radius, float y0, float y1, int segs, float uTiles)
        {
            var mb = new MeshBuilder();
            for (int i = 0; i < segs; i++)
            {
                float a0 = i * Mathf.PI * 2f / segs, a1 = (i + 1) * Mathf.PI * 2f / segs;
                float u0 = (float)i / segs * uTiles, u1 = (float)(i + 1) / segs * uTiles;
                mb.Quad(Around(a0, radius, y0), Around(a1, radius, y0), Around(a1, radius, y1), Around(a0, radius, y1),
                        Inward((a0 + a1) * 0.5f), new Vector2(u0, 0), new Vector2(u1, 0), new Vector2(u1, 1), new Vector2(u0, 1));
            }
            return mb.ToMesh(name);
        }

        /// A cut through the lowest rows of a bowl (a vomitory): segments whose centre angle is
        /// within `HalfAngle` of `Centre` are skipped for rows below `Rows`.
        public struct Gap
        {
            public float Centre, HalfAngle;
            public int Rows;
            public bool Skips(int row, float angle)
            {
                if (row >= Rows) return false;
                float d = Mathf.DeltaAngle(angle * Mathf.Rad2Deg, Centre * Mathf.Rad2Deg) * Mathf.Deg2Rad;
                return Mathf.Abs(d) < HalfAngle;
            }
        }

        /// Stepped stands: for each row a riser (facing the ring) and a tread (facing up).
        public static Mesh Stands(string name, float r0, float depth, float y0, float rise, int rows, int segs, Gap gap = default)
        {
            var mb = new MeshBuilder();
            for (int row = 0; row < rows; row++)
            {
                float r = r0 + row * depth, rn = r + depth;
                float yb = y0 + row * rise, yt = yb + rise;
                for (int i = 0; i < segs; i++)
                {
                    float a0 = i * Mathf.PI * 2f / segs, a1 = (i + 1) * Mathf.PI * 2f / segs;
                    float am = (a0 + a1) * 0.5f;
                    if (gap.Skips(row, am)) continue;
                    mb.Quad(Around(a0, r, yb), Around(a1, r, yb), Around(a1, r, yt), Around(a0, r, yt), Inward(am),
                            Vector2.zero, Vector2.right, Vector2.one, Vector2.up);
                    mb.Quad(Around(a0, r, yt), Around(a1, r, yt), Around(a1, rn, yt), Around(a0, rn, yt), Vector3.up,
                            Vector2.zero, Vector2.right, Vector2.one, Vector2.up);
                }
            }
            return mb.ToMesh(name);
        }

        /// One crowd strip per stand row, set back on the tread, 1 m tall, facing the ring.
        /// u = people along the row (0.5 m seats), uv2.x = row index for per-row variety.
        public static Mesh Crowd(string name, float r0, float depth, float y0, float rise, int rows, int segs, int rowIndexBase, Gap gap = default)
        {
            var mb = new MeshBuilder();
            for (int row = 0; row < rows; row++)
            {
                float r = r0 + row * depth + depth * 0.45f;
                float yb = y0 + row * rise + rise;
                float circumference = Mathf.PI * 2f * r;
                float people = Mathf.Round(circumference / 0.5f);
                for (int i = 0; i < segs; i++)
                {
                    float a0 = i * Mathf.PI * 2f / segs, a1 = (i + 1) * Mathf.PI * 2f / segs;
                    if (gap.Skips(row, (a0 + a1) * 0.5f)) continue;
                    float u0 = (float)i / segs * people, u1 = (float)(i + 1) / segs * people;
                    mb.Quad(Around(a0, r, yb), Around(a1, r, yb), Around(a1, r, yb + 1f), Around(a0, r, yb + 1f),
                            Inward((a0 + a1) * 0.5f), new Vector2(u0, 0), new Vector2(u1, 0), new Vector2(u1, 1), new Vector2(u0, 1),
                            new Vector2(rowIndexBase + row, 0f));
                }
            }
            return mb.ToMesh(name);
        }

        /// Open cone for light beams: apex at the origin, base radius 1 at z = 1, uv.y = z.
        public static Mesh Cone(int segs)
        {
            var mb = new MeshBuilder();
            for (int i = 0; i < segs; i++)
            {
                float a0 = i * Mathf.PI * 2f / segs, a1 = (i + 1) * Mathf.PI * 2f / segs;
                Vector3 b0 = new Vector3(Mathf.Cos(a0), Mathf.Sin(a0), 1f), b1 = new Vector3(Mathf.Cos(a1), Mathf.Sin(a1), 1f);
                Vector3 n0 = new Vector3(Mathf.Cos(a0), Mathf.Sin(a0), -1f).normalized;
                Vector3 n1 = new Vector3(Mathf.Cos(a1), Mathf.Sin(a1), -1f).normalized;
                Vector3 apex0 = b0 * 0.001f, apex1 = b1 * 0.001f;
                mb.QuadSmooth(apex0, apex1, b1, b0, n0, n1, n1, n0,
                              new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1));
            }
            return mb.ToMesh("Arena_BeamCone");
        }

        /// Unit quad in the XY plane facing -Z (towards a viewer looking along +Z), UV 0..1.
        public static Mesh Quad(string name)
        {
            var mb = new MeshBuilder();
            mb.Quad(new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f), new Vector3(0.5f, 0.5f, 0f), new Vector3(-0.5f, 0.5f, 0f),
                    Vector3.back, new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1));
            return mb.ToMesh(name);
        }

        /// A vertical-ish quad centred on `c`, facing `normal`, u running to the viewer's right.
        public static void FaceQuad(MeshBuilder mb, Vector3 c, Vector3 normal, float w, float h)
        {
            normal.Normalize();
            Vector3 upRef = Mathf.Abs(normal.y) > 0.95f ? Vector3.forward : Vector3.up;
            Vector3 right = Vector3.Cross(upRef, -normal).normalized;
            Vector3 up = Vector3.Cross(-normal, right).normalized;
            Vector3 hr = right * (w * 0.5f), hu = up * (h * 0.5f);
            mb.Quad(c - hr - hu, c + hr - hu, c + hr + hu, c - hr + hu, normal,
                    new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1));
        }

        /// A flat disc facing `normal` (lamp lenses).
        public static void DiscFacing(MeshBuilder mb, Vector3 c, Vector3 normal, float radius, int segs)
        {
            normal.Normalize();
            Vector3 upRef = Mathf.Abs(normal.y) > 0.95f ? Vector3.forward : Vector3.up;
            Vector3 x = Vector3.Cross(upRef, normal).normalized, y = Vector3.Cross(normal, x);
            for (int i = 0; i < segs; i++)
            {
                float a0 = i * Mathf.PI * 2f / segs, a1 = (i + 1) * Mathf.PI * 2f / segs;
                Vector3 p0 = c + (x * Mathf.Cos(a0) + y * Mathf.Sin(a0)) * radius;
                Vector3 p1 = c + (x * Mathf.Cos(a1) + y * Mathf.Sin(a1)) * radius;
                mb.Quad(c, p0, p1, c, normal, new Vector2(0.5f, 0.5f), new Vector2(0.5f + Mathf.Cos(a0) * 0.5f, 0.5f + Mathf.Sin(a0) * 0.5f),
                        new Vector2(0.5f + Mathf.Cos(a1) * 0.5f, 0.5f + Mathf.Sin(a1) * 0.5f), new Vector2(0.5f, 0.5f));
            }
        }

        /// A flat ring facing up (rail tops).
        public static void Annulus(MeshBuilder mb, float r0, float r1, float y, int segs)
        {
            for (int i = 0; i < segs; i++)
            {
                float a0 = i * Mathf.PI * 2f / segs, a1 = (i + 1) * Mathf.PI * 2f / segs;
                mb.Quad(Around(a0, r0, y), Around(a1, r0, y), Around(a1, r1, y), Around(a0, r1, y), Vector3.up,
                        Vector2.zero, Vector2.right, Vector2.one, Vector2.up);
            }
        }

        /// Square box truss along a polyline: four chords plus zig-zag lacing.
        public static void Truss(MeshBuilder mb, Vector3 a, Vector3 b, float section = 0.36f, float chord = 0.045f, float lace = 0.022f)
        {
            Vector3 d = b - a;
            float len = d.magnitude;
            if (len < 0.01f) return;
            Vector3 f = d / len;
            Vector3 side = Vector3.Cross(Mathf.Abs(f.y) > 0.95f ? Vector3.forward : Vector3.up, f).normalized;
            Vector3 up = Vector3.Cross(f, side).normalized;
            float h = section * 0.5f;
            Vector3[] c = { (side + up) * h, (side - up) * h, (-side - up) * h, (-side + up) * h };
            for (int k = 0; k < 4; k++) mb.Beam(a + c[k], b + c[k], chord);
            int bays = Mathf.Max(1, Mathf.RoundToInt(len / section));
            for (int i = 0; i < bays; i++)
            {
                Vector3 p0 = a + f * (len * i / bays), p1 = a + f * (len * (i + 1) / bays);
                for (int k = 0; k < 4; k++)
                {
                    Vector3 c0 = c[k], c1 = c[(k + 1) % 4];
                    mb.Beam(p0 + (i % 2 == 0 ? c0 : c1), p1 + (i % 2 == 0 ? c1 : c0), lace);
                }
            }
        }
    }
}
