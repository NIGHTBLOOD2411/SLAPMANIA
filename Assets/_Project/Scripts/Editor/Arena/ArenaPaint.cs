using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SlapMania.Editor.Arena
{
    /// A CPU paint surface for the generated arena art (ADR-0012: procedural, no bought art).
    /// Colours are authored as sRGB hex and saved as sRGB textures. y runs up (Texture2D
    /// convention); every shape is a signed distance in pixels, anti-aliased over one pixel.
    internal sealed class Canvas2D
    {
        public readonly int W, H;
        public readonly Color[] Px;

        public Canvas2D(int w, int h, Color fill)
        {
            W = w; H = h;
            Px = new Color[w * h];
            for (int i = 0; i < Px.Length; i++) Px[i] = fill;
        }

        public void Over(int x, int y, Color c, float coverage)
        {
            if ((uint)x >= (uint)W || (uint)y >= (uint)H) return;
            float a = coverage * c.a;
            if (a <= 0f) return;
            int i = y * W + x;
            Color d = Px[i];
            float outA = a + d.a * (1f - a);
            if (outA < 1e-5f) { Px[i] = Color.clear; return; }
            float k = d.a * (1f - a);
            Px[i] = new Color((c.r * a + d.r * k) / outA, (c.g * a + d.g * k) / outA, (c.b * a + d.b * k) / outA, outA);
        }

        /// Data write for masks: lerps all four channels (no alpha-over semantics).
        public void Mix(int x, int y, Color c, float coverage)
        {
            if ((uint)x >= (uint)W || (uint)y >= (uint)H || coverage <= 0f) return;
            int i = y * W + x;
            Px[i] = Color.LerpUnclamped(Px[i], c, coverage);
        }

        public void ShapeMix(float x0, float y0, float x1, float y1, Func<float, float, float> sdf, Color c)
        {
            int ix0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(x0, x1)) - 1), ix1 = Mathf.Min(W - 1, Mathf.CeilToInt(Mathf.Max(x0, x1)) + 1);
            int iy0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(y0, y1)) - 1), iy1 = Mathf.Min(H - 1, Mathf.CeilToInt(Mathf.Max(y0, y1)) + 1);
            for (int y = iy0; y <= iy1; y++)
                for (int x = ix0; x <= ix1; x++)
                    Mix(x, y, c, Mathf.Clamp01(0.5f - sdf(x + 0.5f, y + 0.5f)));
        }

        /// Fills the region where `sdf` < 0, colour chosen per pixel.
        public void Shape(float x0, float y0, float x1, float y1, Func<float, float, float> sdf, Func<float, float, Color> colour)
        {
            int ix0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(x0, x1)) - 1), ix1 = Mathf.Min(W - 1, Mathf.CeilToInt(Mathf.Max(x0, x1)) + 1);
            int iy0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(y0, y1)) - 1), iy1 = Mathf.Min(H - 1, Mathf.CeilToInt(Mathf.Max(y0, y1)) + 1);
            for (int y = iy0; y <= iy1; y++)
                for (int x = ix0; x <= ix1; x++)
                {
                    float px = x + 0.5f, py = y + 0.5f;
                    float cov = Mathf.Clamp01(0.5f - sdf(px, py));
                    if (cov > 0f) Over(x, y, colour(px, py), cov);
                }
        }

        public void Shape(float x0, float y0, float x1, float y1, Func<float, float, float> sdf, Color c) =>
            Shape(x0, y0, x1, y1, sdf, (x, y) => c);

        public void Rect(float x0, float y0, float x1, float y1, Color c) =>
            Shape(x0, y0, x1, y1, (x, y) => Sdf.Box(x, y, (x0 + x1) * 0.5f, (y0 + y1) * 0.5f, (x1 - x0) * 0.5f, (y1 - y0) * 0.5f, 0f), c);

        public void RoundRect(float x0, float y0, float x1, float y1, float r, Color c) =>
            Shape(x0, y0, x1, y1, (x, y) => Sdf.Box(x, y, (x0 + x1) * 0.5f, (y0 + y1) * 0.5f, (x1 - x0) * 0.5f, (y1 - y0) * 0.5f, r), c);

        public void VGradient(float x0, float y0, float x1, float y1, Color bottom, Color top) =>
            Shape(x0, y0, x1, y1, (x, y) => Sdf.Box(x, y, (x0 + x1) * 0.5f, (y0 + y1) * 0.5f, (x1 - x0) * 0.5f, (y1 - y0) * 0.5f, 0f),
                  (x, y) => Color.Lerp(bottom, top, Mathf.InverseLerp(y0, y1, y)));

        /// Multiplies rgb by 1 ± amp of two-octave value noise (period `scale` px).
        public void Grain(float amp, float scale, int seed)
        {
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    float n = Noise.Value(x / scale, y / scale, seed) * 0.65f + Noise.Value(x / (scale * 0.23f), y / (scale * 0.23f), seed + 7) * 0.35f;
                    float k = 1f + amp * (n * 2f - 1f);
                    int i = y * W + x;
                    var c = Px[i];
                    Px[i] = new Color(c.r * k, c.g * k, c.b * k, c.a);
                }
        }

        /// Returns a copy rotated 90° counter-clockwise (text reading bottom-to-top).
        public Canvas2D RotatedCcw()
        {
            var r = new Canvas2D(H, W, Color.clear);
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                    r.Px[x * r.W + (H - 1 - y)] = Px[y * W + x];
            return r;
        }

        /// Writes (or overwrites in place, keeping the GUID) a PNG and configures its importer.
        public Texture2D Save(string path, bool srgb, bool repeatU, bool repeatV, int maxSize, bool alpha)
        {
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false, !srgb);
            tex.SetPixels(Px);
            tex.Apply(false);
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var ti = (TextureImporter)AssetImporter.GetAtPath(path);
            ti.textureType = TextureImporterType.Default;
            ti.sRGBTexture = srgb;
            ti.alphaSource = alpha ? TextureImporterAlphaSource.FromInput : TextureImporterAlphaSource.None;
            ti.alphaIsTransparency = alpha;
            ti.wrapModeU = repeatU ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
            ti.wrapModeV = repeatV ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
            ti.mipmapEnabled = true;
            ti.anisoLevel = 4;
            ti.maxTextureSize = maxSize;
            ti.textureCompression = TextureImporterCompression.Compressed;
            ti.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
    }

    internal static class Sdf
    {
        public static float Box(float px, float py, float cx, float cy, float hx, float hy, float r)
        {
            float qx = Mathf.Abs(px - cx) - hx + r, qy = Mathf.Abs(py - cy) - hy + r;
            float ox = Mathf.Max(qx, 0f), oy = Mathf.Max(qy, 0f);
            return Mathf.Sqrt(ox * ox + oy * oy) + Mathf.Min(Mathf.Max(qx, qy), 0f) - r;
        }

        public static float Circle(float px, float py, float cx, float cy, float r)
        {
            float dx = px - cx, dy = py - cy;
            return Mathf.Sqrt(dx * dx + dy * dy) - r;
        }

        public static float Ring(float px, float py, float cx, float cy, float r, float halfWidth) =>
            Mathf.Abs(Circle(px, py, cx, cy, r)) - halfWidth;

        public static float Capsule(float px, float py, float ax, float ay, float bx, float by, float r)
        {
            float pax = px - ax, pay = py - ay, bax = bx - ax, bay = by - ay;
            float h = Mathf.Clamp01((pax * bax + pay * bay) / (bax * bax + bay * bay));
            float dx = pax - bax * h, dy = pay - bay * h;
            return Mathf.Sqrt(dx * dx + dy * dy) - r;
        }

        /// The open-palm slap emblem in unit space (about 1 wide, fingers up, centred on 0,0).
        public static float Hand(float x, float y)
        {
            float d = Box(x, y, 0f, -0.1f, 0.19f, 0.2f, 0.08f);
            d = Mathf.Min(d, Capsule(x, y, -0.138f, 0.02f, -0.158f, 0.33f, 0.042f));
            d = Mathf.Min(d, Capsule(x, y, -0.046f, 0.04f, -0.05f, 0.4f, 0.042f));
            d = Mathf.Min(d, Capsule(x, y, 0.046f, 0.04f, 0.056f, 0.37f, 0.042f));
            d = Mathf.Min(d, Capsule(x, y, 0.134f, 0.0f, 0.17f, 0.27f, 0.04f));
            d = Mathf.Min(d, Capsule(x, y, -0.17f, -0.2f, -0.35f, 0.02f, 0.052f));
            d = Mathf.Min(d, Box(x, y, 0f, -0.36f, 0.13f, 0.07f, 0.03f));
            return d;
        }
    }

    internal static class Noise
    {
        private static float Hash(int x, int y, int seed)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393 + y * 668265263 + seed * 144665);
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return (h & 0xFFFFFF) / 16777215f;
            }
        }

        public static float Value(float x, float y, int seed)
        {
            int ix = Mathf.FloorToInt(x), iy = Mathf.FloorToInt(y);
            float fx = x - ix, fy = y - iy;
            fx = fx * fx * (3f - 2f * fx);
            fy = fy * fy * (3f - 2f * fy);
            float a = Hash(ix, iy, seed), b = Hash(ix + 1, iy, seed), c = Hash(ix, iy + 1, seed), d = Hash(ix + 1, iy + 1, seed);
            return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
        }
    }

    /// Glyph coverage from a TTF, rasterised by Unity's dynamic font at the exact pixel size it
    /// is drawn at (no rescale, so no blur). The atlas and the glyph metrics are snapshotted
    /// together, because a later request at another size may repack the font texture.
    internal sealed class Glyphs
    {
        private readonly Dictionary<char, CharacterInfo> _info = new Dictionary<char, CharacterInfo>();
        private readonly Color32[] _atlas;
        private readonly int _aw, _ah;
        public readonly int Size;
        public readonly float CapHeight;

        public Glyphs(Font font, int size, string chars)
        {
            Size = size;
            string all = chars + "H";
            font.RequestCharactersInTexture(all, size, FontStyle.Normal);
            var src = font.material.mainTexture;
            var rt = RenderTexture.GetTemporary(src.width, src.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            Graphics.Blit(src, rt);
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var copy = new Texture2D(src.width, src.height, TextureFormat.RGBA32, false, true);
            copy.ReadPixels(new Rect(0, 0, src.width, src.height), 0, 0);
            copy.Apply(false);
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);
            _atlas = copy.GetPixels32();
            _aw = src.width;
            _ah = src.height;
            Object.DestroyImmediate(copy);
            foreach (char c in all)
                if (!_info.ContainsKey(c) && font.GetCharacterInfo(c, out var ci, size, FontStyle.Normal)) _info[c] = ci;
            CapHeight = _info.TryGetValue('H', out var h) ? h.maxY : size * 0.7f;
        }

        public float Advance(string s, float tracking)
        {
            float w = 0f;
            foreach (char c in s) w += (_info.TryGetValue(c, out var ci) ? ci.advance : Size * 0.3f) + tracking;
            return s.Length > 0 ? w - tracking : 0f;
        }

        private float A(int x, int y) => (uint)x < (uint)_aw && (uint)y < (uint)_ah ? _atlas[y * _aw + x].a / 255f : 0f;

        private float Alpha(Vector2 uv)
        {
            float x = uv.x * _aw - 0.5f, y = uv.y * _ah - 0.5f;
            int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
            float fx = x - x0, fy = y - y0;
            return Mathf.Lerp(Mathf.Lerp(A(x0, y0), A(x0 + 1, y0), fx), Mathf.Lerp(A(x0, y0 + 1), A(x0 + 1, y0 + 1), fx), fy);
        }

        /// Draws `s` with its baseline at `baseline`; colour runs bottom→top over the cap height.
        public void Draw(Canvas2D cv, string s, float x, float baseline, Color bottom, Color top, float slant, float tracking)
        {
            float pen = x;
            foreach (char c in s)
            {
                if (!_info.TryGetValue(c, out var ci)) { pen += Size * 0.3f + tracking; continue; }
                float gw = ci.maxX - ci.minX, gh = ci.maxY - ci.minY;
                if (gw > 0 && gh > 0)
                {
                    float s0 = slant * ci.minY, s1 = slant * ci.maxY;
                    int tx0 = Mathf.FloorToInt(pen + ci.minX + Mathf.Min(s0, s1)) - 1;
                    int tx1 = Mathf.CeilToInt(pen + ci.maxX + Mathf.Max(s0, s1)) + 1;
                    int ty0 = Mathf.FloorToInt(baseline + ci.minY) - 1, ty1 = Mathf.CeilToInt(baseline + ci.maxY) + 1;
                    for (int ty = ty0; ty <= ty1; ty++)
                    {
                        float gy = ty + 0.5f - baseline;
                        if (gy < ci.minY || gy > ci.maxY) continue;
                        Color col = Color.Lerp(bottom, top, Mathf.Clamp01(gy / CapHeight));
                        for (int tx = tx0; tx <= tx1; tx++)
                        {
                            float gx = tx + 0.5f - pen - slant * gy;
                            if (gx < ci.minX || gx > ci.maxX) continue;
                            float u = (gx - ci.minX) / gw, v = (gy - ci.minY) / gh;
                            Vector2 uv = ci.uvBottomLeft + (ci.uvBottomRight - ci.uvBottomLeft) * u + (ci.uvTopLeft - ci.uvBottomLeft) * v;
                            float a = Alpha(uv);
                            if (a > 0f) cv.Over(tx, ty, col, a);
                        }
                    }
                }
                pen += ci.advance + tracking;
            }
        }
    }

    internal static class Paint
    {
        private static readonly Dictionary<Font, float> s_capRatio = new Dictionary<Font, float>();

        public static Font LoadFont(string path)
        {
            var f = AssetDatabase.LoadAssetAtPath<Font>(path);
            if (f == null) throw new FileNotFoundException("Arena font missing: " + path);
            return f;
        }

        private static float CapRatio(Font f)
        {
            if (!s_capRatio.TryGetValue(f, out float r))
            {
                r = new Glyphs(f, 100, "H").CapHeight / 100f;
                if (r <= 0.1f) r = 0.7f;
                s_capRatio[f] = r;
            }
            return r;
        }

        /// Centred text whose capitals are `capPx` tall, shrunk to fit `maxWidth`.
        public static void Text(Canvas2D cv, Font f, string s, float cx, float cy, float capPx, float maxWidth,
                                Color bottom, Color top, float trackingEm = 0f, float slant = 0f,
                                Color shadow = default, float shadowPx = 0f)
        {
            int size = Mathf.Max(6, Mathf.RoundToInt(capPx / CapRatio(f)));
            var g = new Glyphs(f, size, s);
            float track = trackingEm * size;
            float w = g.Advance(s, track);
            if (w > maxWidth)
            {
                size = Mathf.Max(6, Mathf.FloorToInt(size * maxWidth / w));
                g = new Glyphs(f, size, s);
                track = trackingEm * size;
                w = g.Advance(s, track);
            }
            float x = cx - w * 0.5f - slant * g.CapHeight * 0.5f;
            float baseline = cy - g.CapHeight * 0.5f;
            if (shadowPx > 0f && shadow.a > 0f)
                g.Draw(cv, s, x + shadowPx, baseline - shadowPx, shadow, shadow, slant, track);
            g.Draw(cv, s, x, baseline, bottom, top, slant, track);
        }

        /// The slap emblem: gold palm with an embossed bevel lit from the upper left.
        /// `mask` (optional) receives metal = 1 where the emblem is, for a metallic map.
        public static void Hand(Canvas2D cv, Canvas2D mask, float cx, float cy, float size, Color gold, float bevelPx)
        {
            float inv = 1f / size;
            Func<float, float, float> sdf = (x, y) => Sdf.Hand((x - cx) * inv, (y - cy) * inv) * size;
            var light = new Vector2(-0.6f, 0.8f);
            Color dark = gold * 0.42f; dark.a = 1f;
            Color hi = Color.Lerp(gold, Color.white, 0.45f);
            cv.Shape(cx - size * 0.5f, cy - size * 0.5f, cx + size * 0.5f, cy + size * 0.5f, sdf, (x, y) =>
            {
                float d = sdf(x, y);
                float gx = sdf(x + 1f, y) - sdf(x - 1f, y), gy = sdf(x, y + 1f) - sdf(x, y - 1f);
                var n = new Vector2(gx, gy);
                float len = n.magnitude;
                if (len > 1e-4f) n /= len;
                float rim = Mathf.Clamp01(1f + d / bevelPx); // 1 at the edge, 0 deep inside
                float lit = Vector2.Dot(n, light);
                Color edge = lit > 0f ? Color.Lerp(gold, hi, lit) : Color.Lerp(gold, dark, -lit);
                Color c = Color.Lerp(gold, edge, rim);
                // a thin engraved outline right at the edge
                if (d > -1.6f) c = Color.Lerp(c, dark, 0.65f);
                return c;
            });
            if (mask != null)
                mask.ShapeMix(cx - size * 0.5f, cy - size * 0.5f, cx + size * 0.5f, cy + size * 0.5f, sdf, new Color(1f, 0f, 0f, 0.8f));
        }
    }
}
