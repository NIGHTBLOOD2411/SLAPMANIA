using UnityEngine;
using UnityEngine.UI;

namespace SlapMania.UI
{
    /// Geometry shared by the procedural V2 shapes: convex polygon clipping, fan fills and
    /// inset outlines, written straight into a VertexHelper (itself pooled). Scratch buffers
    /// are static and fixed-size — mesh rebuilds happen on the main thread only and allocate
    /// nothing (R5).
    internal static class ShapeMesh
    {
        public const int MaxPoints = 24;

        private static readonly Vector2[] s_clipA = new Vector2[MaxPoints];
        private static readonly Vector2[] s_clipB = new Vector2[MaxPoints];

        public static void AddQuad(VertexHelper vh, Vector2 a, Vector2 b, Vector2 c, Vector2 d,
                                   Color32 ca, Color32 cb, Color32 cc, Color32 cd)
        {
            int i = vh.currentVertCount;
            vh.AddVert(a, ca, Vector4.zero);
            vh.AddVert(b, cb, Vector4.zero);
            vh.AddVert(c, cc, Vector4.zero);
            vh.AddVert(d, cd, Vector4.zero);
            vh.AddTriangle(i, i + 1, i + 2);
            vh.AddTriangle(i, i + 2, i + 3);
        }

        /// Fan-fills a convex polygon, colouring each vertex by the gradient at its position.
        public static void FillConvex(VertexHelper vh, Vector2[] pts, int count, in Gradient3 g)
        {
            if (count < 3) return;
            int start = vh.currentVertCount;
            for (int i = 0; i < count; i++) vh.AddVert(pts[i], g.At(pts[i]), Vector4.zero);
            for (int i = 1; i < count - 1; i++) vh.AddTriangle(start, start + i, start + i + 1);
        }

        /// Fills a convex polygon with an exact piecewise-linear 3-stop gradient: the polygon is
        /// cut at the middle stop so the mid colour lands on a real edge instead of being
        /// averaged across a triangle.
        public static void FillConvexGradient(VertexHelper vh, Vector2[] pts, int count, in Gradient3 g)
        {
            if (!g.HasMid) { FillConvex(vh, pts, count, g); return; }
            Vector2 n = g.Axis;             // unit, pointing from stop 0 to stop 1
            float cut = g.Offset + g.Mid * g.Length;
            int a = ClipHalfPlane(pts, count, n, cut, keepBelow: true, s_clipA);
            FillConvex(vh, s_clipA, a, g);
            int b = ClipHalfPlane(pts, count, n, cut, keepBelow: false, s_clipB);
            FillConvex(vh, s_clipB, b, g);
        }

        /// Sutherland–Hodgman against the line dot(p, n) = d. Keeps dot <= d when keepBelow.
        public static int ClipHalfPlane(Vector2[] src, int count, Vector2 n, float d, bool keepBelow, Vector2[] dst)
        {
            int outCount = 0;
            if (count == 0) return 0;
            Vector2 prev = src[count - 1];
            float prevDist = Side(prev, n, d, keepBelow);
            for (int i = 0; i < count; i++)
            {
                Vector2 cur = src[i];
                float curDist = Side(cur, n, d, keepBelow);
                bool curIn = curDist <= 0f, prevIn = prevDist <= 0f;
                if (curIn != prevIn && outCount < dst.Length)
                {
                    float t = prevDist / (prevDist - curDist);
                    dst[outCount++] = Vector2.LerpUnclamped(prev, cur, t);
                }
                if (curIn && outCount < dst.Length) dst[outCount++] = cur;
                prev = cur;
                prevDist = curDist;
            }
            return outCount;
        }

        private static float Side(Vector2 p, Vector2 n, float d, bool keepBelow)
        {
            float s = Vector2.Dot(p, n) - d;
            return keepBelow ? s : -s;
        }

        /// Insets a convex, clockwise polygon (y-up) by `d` using miter offsets.
        public static void Inset(Vector2[] src, int count, float d, Vector2[] dst)
        {
            for (int i = 0; i < count; i++)
            {
                Vector2 p0 = src[(i - 1 + count) % count];
                Vector2 p1 = src[i];
                Vector2 p2 = src[(i + 1) % count];
                Vector2 n1 = RightNormal(p1 - p0);
                Vector2 n2 = RightNormal(p2 - p1);
                float denom = 1f + Vector2.Dot(n1, n2);
                dst[i] = denom > 1e-4f ? p1 + (n1 + n2) * (d / denom) : p1 + n2 * d;
            }
        }

        private static Vector2 RightNormal(Vector2 dir)
        {
            float len = dir.magnitude;
            return len > 1e-6f ? new Vector2(dir.y, -dir.x) / len : Vector2.zero;
        }
    }

    /// A 2- or 3-stop linear gradient in local space, multiplied by the Graphic's colour.
    internal readonly struct Gradient3
    {
        public readonly Vector2 Axis;
        public readonly float Offset;
        public readonly float Length;
        public readonly float Mid;
        private readonly Color _a, _b, _c;

        public bool HasMid => Mid > 0f && Mid < 1f;

        public Gradient3(Rect rect, bool vertical, Color a, Color b, Color c, float mid, Color tint)
        {
            // Vertical runs top -> bottom (CSS 180deg); horizontal runs left -> right (CSS 90deg).
            Axis = vertical ? Vector2.down : Vector2.right;
            Offset = vertical ? -rect.yMax : rect.xMin;
            Length = vertical ? rect.height : rect.width;
            Mid = mid;
            _a = a * tint; _b = b * tint; _c = c * tint;
        }

        public Color32 At(Vector2 p)
        {
            float t = Length > 1e-5f ? (Vector2.Dot(p, Axis) - Offset) / Length : 0f;
            t = t < 0f ? 0f : t > 1f ? 1f : t;
            if (!HasMid) return Color.LerpUnclamped(_a, _c, t);
            return t < Mid ? Color.LerpUnclamped(_a, _b, t / Mid) : Color.LerpUnclamped(_b, _c, (t - Mid) / (1f - Mid));
        }
    }
}
