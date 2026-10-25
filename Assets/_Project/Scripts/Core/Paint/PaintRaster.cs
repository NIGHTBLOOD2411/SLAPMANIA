using UnityEngine;

namespace SlapMania.Core.Paint
{
    /// Evaluates parsed CSS paint per pixel, following the CSS Images spec closely enough
    /// that a baked texture matches the browser mockup:
    ///   * linear: gradient line through the centre, length |w sin a| + |h cos a|;
    ///   * radial: explicit size, else farthest-corner (ellipses keep the farthest-side ratio);
    ///   * conic: 0 deg at 12 o'clock, clockwise; repeating variants wrap by the stop span;
    ///   * stops: unset positions spread evenly, positions clamp to be non-decreasing, and
    ///     colours interpolate in premultiplied alpha (so a fade to transparent never greys);
    ///   * layers composite with source-over, index 0 on top.
    ///
    /// Pure: geometry in CSS px, output in straight-alpha sRGB bytes, no Unity objects
    /// touched, so EditMode tests can check exact values (R2/R3).
    public static class PaintRaster
    {
        /// Everything one bake needs. `TexW/TexH` may differ from the CSS size; each texel is
        /// sampled at its centre mapped into CSS px.
        public struct Request
        {
            public PaintLayer[] Layers;
            public PaintLayer[] Mask;   // optional: multiplies alpha (mask-image)
            public ClipShape Clip;      // optional: clip-path, anti-aliased
            public float CssW, CssH;
            public int TexW, TexH;
        }

        private struct Prepared
        {
            public PaintLayer L;
            public float W, H;               // geometry box (tile or element)
            public float Cx, Cy;             // radial/conic centre
            public float Rx, Ry;             // radial radii
            public float Dx, Dy, Sx, Sy, Len;// linear direction, start, length
            public float[] Pos;              // resolved stop fractions
            public Color[] Pre;              // premultiplied stop colours
        }

        // ── Public entry points ──────────────────────────────────────
        public static void Rasterize(in Request r, Color32[] dst)
        {
            if (dst == null || dst.Length < r.TexW * r.TexH) throw new System.ArgumentException("dst too small");
            var layers = Prepare(r.Layers, r.CssW, r.CssH);
            var mask = r.Mask != null ? Prepare(r.Mask, r.CssW, r.CssH) : null;
            float sx = r.CssW / r.TexW, sy = r.CssH / r.TexH;
            float pxPerCss = r.TexW / Mathf.Max(1e-4f, r.CssW);
            ResolveClip(r.Clip, r.CssW, r.CssH, out var cpx, out var cpy);

            for (int j = 0; j < r.TexH; j++)
            {
                float y = (j + 0.5f) * sy;
                int row = (r.TexH - 1 - j) * r.TexW; // Unity textures start at the bottom row
                for (int i = 0; i < r.TexW; i++)
                {
                    float x = (i + 0.5f) * sx;
                    Color c = Composite(layers, x, y);
                    if (mask != null) c *= Composite(mask, x, y).a;
                    if (r.Clip != null)
                        c *= r.Clip.Kind == ClipShape.ShapeKind.Circle
                            ? CircleCoverage(r.Clip, r.CssW, r.CssH, x, y, pxPerCss)
                            : Mathf.Clamp01(0.5f + PolygonSignedDistance(cpx, cpy, x, y) * pxPerCss);
                    dst[row + i] = ToStraight32(c);
                }
            }
        }

        /// Straight-alpha colour of the composited layers at CSS point (x, y) in a w x h box.
        public static Color Sample(PaintLayer[] layers, float x, float y, float w, float h)
        {
            var c = Composite(Prepare(layers, w, h), x, y);
            return Unpremultiply(c);
        }

        /// Box-shadows (outer and inset) of a w x h border box with corner `radius`, drawn into
        /// a texture that extends `pad` px past the box on every side. Outer shadows are cut
        /// out of the box interior and inset ones are confined to it, as in CSS.
        public static void RasterizeShadows(BoxShadow[] shadows, float w, float h, float radius, float pad,
                                            int texW, int texH, Color32[] dst)
        {
            float fullW = w + 2f * pad, fullH = h + 2f * pad;
            float sx = fullW / texW, sy = fullH / texH;
            float pxPerCss = texW / Mathf.Max(1e-4f, fullW);
            for (int j = 0; j < texH; j++)
            {
                float y = (j + 0.5f) * sy - pad;
                int row = (texH - 1 - j) * texW;
                for (int i = 0; i < texW; i++)
                {
                    float x = (i + 0.5f) * sx - pad;
                    float boxD = RoundRectDistance(x, y, 0f, 0f, w, h, radius);
                    float inside = Mathf.Clamp01(0.5f - boxD * pxPerCss); // AA coverage of the box
                    Color acc = default;
                    for (int k = shadows.Length - 1; k >= 0; k--)
                    {
                        var s = shadows[k];
                        float a = s.Color.a * ShadowAlpha(s, x, y, w, h, radius) * (s.Inset ? inside : 1f - inside);
                        acc = Over(new Color(s.Color.r * a, s.Color.g * a, s.Color.b * a, a), acc); // premultiplied
                    }
                    dst[row + i] = ToStraight32(acc);
                }
            }
        }

        /// Alpha of one shadow at (x, y), before the inside/outside cut.
        public static float ShadowAlpha(in BoxShadow s, float x, float y, float w, float h, float radius)
        {
            float spread = s.Inset ? -s.Spread : s.Spread;
            float r = Mathf.Max(0f, radius + spread);
            float d = RoundRectDistance(x - s.OffsetX, y - s.OffsetY, -spread, -spread, w + 2f * spread, h + 2f * spread, r);
            float a = s.Blur <= 0.01f ? Mathf.Clamp01(0.5f - d) : 0.5f * Erfc(d / (s.Blur * 0.5f * 1.41421356f));
            return s.Inset ? 1f - a : a;
        }

        // ── Preparation ──────────────────────────────────────────────
        private static Prepared[] Prepare(PaintLayer[] layers, float w, float h)
        {
            if (layers == null) return System.Array.Empty<Prepared>();
            var p = new Prepared[layers.Length];
            for (int i = 0; i < layers.Length; i++)
            {
                var l = layers[i];
                ref var q = ref p[i];
                q.L = l;
                q.W = l.IsTiled ? l.TileW : w;
                q.H = l.IsTiled ? l.TileH : h;
                float length = 1f;
                switch (l.Kind)
                {
                    case PaintKind.Linear:
                    {
                        float a = l.AngleDeg * Mathf.Deg2Rad;
                        q.Dx = Mathf.Sin(a);
                        q.Dy = -Mathf.Cos(a);
                        q.Len = Mathf.Abs(q.W * q.Dx) + Mathf.Abs(q.H * q.Dy);
                        if (q.Len < 1e-4f) q.Len = 1e-4f;
                        q.Sx = q.W * 0.5f - q.Dx * q.Len * 0.5f;
                        q.Sy = q.H * 0.5f - q.Dy * q.Len * 0.5f;
                        length = q.Len;
                        break;
                    }
                    case PaintKind.Radial:
                    {
                        q.Cx = l.AtX.Resolve(q.W);
                        q.Cy = l.AtY.Resolve(q.H);
                        if (l.HasSize)
                        {
                            q.Rx = l.SizeX.Resolve(q.W);
                            q.Ry = l.Circle ? q.Rx : l.SizeY.Resolve(q.H);
                        }
                        else if (l.Circle)
                        {
                            float fx = Mathf.Max(q.Cx, q.W - q.Cx), fy = Mathf.Max(q.Cy, q.H - q.Cy);
                            q.Rx = q.Ry = Mathf.Sqrt(fx * fx + fy * fy);
                        }
                        else
                        {
                            q.Rx = Mathf.Max(q.Cx, q.W - q.Cx) * 1.41421356f;
                            q.Ry = Mathf.Max(q.Cy, q.H - q.Cy) * 1.41421356f;
                        }
                        q.Rx = Mathf.Max(q.Rx, 1e-4f);
                        q.Ry = Mathf.Max(q.Ry, 1e-4f);
                        length = q.Rx;
                        break;
                    }
                    case PaintKind.Conic:
                        q.Cx = l.AtX.Resolve(q.W);
                        q.Cy = l.AtY.Resolve(q.H);
                        length = 1f;
                        break;
                }
                ResolveStops(l.Stops, length, out q.Pos, out q.Pre);
            }
            return p;
        }

        /// CSS stop fix-up: px -> fraction, first/last default to 0/1, positions clamp to be
        /// non-decreasing, unset runs spread evenly between their neighbours.
        public static void ResolveStops(ColorStop[] stops, float lengthPx, out float[] pos, out Color[] premul)
        {
            int n = stops?.Length ?? 0;
            pos = new float[n];
            premul = new Color[n];
            if (n == 0) return;
            var set = new bool[n];
            for (int i = 0; i < n; i++)
            {
                var s = stops[i];
                var c = s.Color;
                premul[i] = new Color(c.r * c.a, c.g * c.a, c.b * c.a, c.a);
                if (s.HasPosition)
                {
                    pos[i] = s.Unit == ColorStop.PositionUnit.Px ? s.Position / Mathf.Max(lengthPx, 1e-4f) : s.Position;
                    set[i] = true;
                }
            }
            if (!set[0]) { pos[0] = 0f; set[0] = true; }
            if (!set[n - 1]) { pos[n - 1] = Mathf.Max(1f, MaxSetBefore(pos, set, n - 1)); set[n - 1] = true; }

            float max = pos[0];
            for (int i = 1; i < n; i++)
                if (set[i]) { if (pos[i] < max) pos[i] = max; max = pos[i]; }

            for (int i = 1; i < n; i++)
            {
                if (set[i]) continue;
                int a = i - 1, b = i;
                while (!set[b]) b++;
                for (int k = a + 1; k < b; k++) pos[k] = Mathf.Lerp(pos[a], pos[b], (float)(k - a) / (b - a));
                i = b;
            }
        }

        private static float MaxSetBefore(float[] pos, bool[] set, int end)
        {
            float m = 0f;
            for (int i = 0; i < end; i++) if (set[i] && pos[i] > m) m = pos[i];
            return m;
        }

        // ── Sampling ─────────────────────────────────────────────────
        private static Color Composite(Prepared[] layers, float x, float y)
        {
            Color acc = default;
            for (int i = layers.Length - 1; i >= 0; i--) acc = Over(Eval(ref layers[i], x, y), acc);
            return acc;
        }

        /// Premultiplied colour of one layer at (x, y).
        private static Color Eval(ref Prepared q, float x, float y)
        {
            var l = q.L;
            if (l.Kind == PaintKind.Solid)
            {
                var c = l.Solid;
                return new Color(c.r * c.a, c.g * c.a, c.b * c.a, c.a);
            }
            if (l.IsTiled)
            {
                x = Mod(x, q.W);
                y = Mod(y, q.H);
            }
            float t;
            switch (l.Kind)
            {
                case PaintKind.Linear:
                    t = ((x - q.Sx) * q.Dx + (y - q.Sy) * q.Dy) / q.Len;
                    break;
                case PaintKind.Radial:
                {
                    float dx = (x - q.Cx) / q.Rx, dy = (y - q.Cy) / q.Ry;
                    t = Mathf.Sqrt(dx * dx + dy * dy);
                    break;
                }
                default: // Conic
                {
                    float deg = Mathf.Atan2(x - q.Cx, -(y - q.Cy)) * Mathf.Rad2Deg;
                    t = Mod(deg - l.AngleDeg, 360f) / 360f;
                    break;
                }
            }
            return AtStops(q.Pos, q.Pre, t, l.Repeating);
        }

        public static Color AtStops(float[] pos, Color[] pre, float t, bool repeating)
        {
            int n = pos.Length;
            if (n == 0) return default;
            if (n == 1) return pre[0];
            if (repeating)
            {
                float span = pos[n - 1] - pos[0];
                if (span <= 1e-6f) return pre[n - 1];
                t = pos[0] + Mod(t - pos[0], span);
            }
            if (float.IsNaN(t) || t <= pos[0]) return pre[0];
            if (t >= pos[n - 1]) return pre[n - 1];
            for (int i = 1; i < n; i++)
            {
                if (t > pos[i]) continue;
                float span = pos[i] - pos[i - 1];
                if (span <= 1e-6f) return pre[i];
                return Color.LerpUnclamped(pre[i - 1], pre[i], (t - pos[i - 1]) / span);
            }
            return pre[n - 1];
        }

        // ── Clip ─────────────────────────────────────────────────────
        private static void ResolveClip(ClipShape clip, float w, float h, out float[] xs, out float[] ys)
        {
            xs = ys = null;
            if (clip == null || clip.Kind != ClipShape.ShapeKind.Polygon) return;
            int n = clip.PointCount;
            xs = new float[n];
            ys = new float[n];
            for (int i = 0; i < n; i++) { xs[i] = clip.Xs[i].Resolve(w); ys[i] = clip.Ys[i].Resolve(h); }
        }

        /// 0..1 coverage of a circle clip at (x, y), anti-aliased over one texel. A % radius
        /// resolves against sqrt((w² + h²) / 2), as CSS specifies.
        public static float CircleCoverage(ClipShape clip, float w, float h, float x, float y, float pxPerCss)
        {
            float cx = clip.CenterX.Resolve(w), cy = clip.CenterY.Resolve(h);
            float r = clip.Radius.Resolve(Mathf.Sqrt((w * w + h * h) * 0.5f));
            float d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
            return Mathf.Clamp01(0.5f + (r - d) * pxPerCss);
        }

        public static float PolygonSignedDistance(float[] xs, float[] ys, float x, float y)
        {
            int n = xs.Length;
            float minD2 = float.MaxValue;
            bool inside = false;
            for (int i = 0, j = n - 1; i < n; j = i++)
            {
                float ax = xs[j], ay = ys[j], bx = xs[i], by = ys[i];
                // even-odd crossing test
                if ((by > y) != (ay > y) && x < (ax - bx) * (y - by) / (ay - by + 1e-12f) + bx) inside = !inside;
                // distance to segment
                float ex = ax - bx, ey = ay - by;
                float len2 = ex * ex + ey * ey;
                float t = len2 > 0f ? Mathf.Clamp01(((x - bx) * ex + (y - by) * ey) / len2) : 0f;
                float px = bx + ex * t - x, py = by + ey * t - y;
                float d2 = px * px + py * py;
                if (d2 < minD2) minD2 = d2;
            }
            float d = Mathf.Sqrt(minD2);
            return inside ? d : -d;
        }

        // ── Maths ────────────────────────────────────────────────────
        /// Signed distance from (x, y) to a rounded rect (positive outside).
        public static float RoundRectDistance(float x, float y, float left, float top, float w, float h, float r)
        {
            float hw = w * 0.5f, hh = h * 0.5f;
            r = Mathf.Clamp(r, 0f, Mathf.Min(Mathf.Abs(hw), Mathf.Abs(hh)));
            float qx = Mathf.Abs(x - (left + hw)) - hw + r;
            float qy = Mathf.Abs(y - (top + hh)) - hh + r;
            float ox = Mathf.Max(qx, 0f), oy = Mathf.Max(qy, 0f);
            return Mathf.Sqrt(ox * ox + oy * oy) + Mathf.Min(Mathf.Max(qx, qy), 0f) - r;
        }

        /// Complementary error function, Abramowitz–Stegun 7.1.26 (|error| < 1.5e-7).
        public static float Erfc(float x)
        {
            float z = Mathf.Abs(x);
            float t = 1f / (1f + 0.3275911f * z);
            float poly = t * (0.254829592f + t * (-0.284496736f + t * (1.421413741f + t * (-1.453152027f + t * 1.061405429f))));
            float r = poly * Mathf.Exp(-z * z);
            return x >= 0f ? r : 2f - r;
        }

        private static Color Over(Color src, Color dst)
        {
            float k = 1f - src.a;
            return new Color(src.r + dst.r * k, src.g + dst.g * k, src.b + dst.b * k, src.a + dst.a * k);
        }

        private static Color Unpremultiply(Color c)
        {
            if (c.a <= 1e-6f) return new Color(0f, 0f, 0f, 0f);
            return new Color(c.r / c.a, c.g / c.a, c.b / c.a, c.a);
        }

        private static Color32 ToStraight32(Color premul)
        {
            var c = Unpremultiply(premul);
            return new Color32(B(c.r), B(c.g), B(c.b), B(c.a));
        }

        private static byte B(float v) => (byte)(Mathf.Clamp01(v) * 255f + 0.5f);

        private static float Mod(float a, float m) => a - m * Mathf.Floor(a / m);
    }
}
