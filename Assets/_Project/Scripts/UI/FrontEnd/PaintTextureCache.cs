using System.Collections.Generic;
using UnityEngine;
using SlapMania.Core.Paint;

namespace SlapMania.UI.FrontEnd
{
    /// Reference-counted textures baked by PaintBox, keyed by everything that shaped them.
    /// The kit repeats itself (every `.bp` button, every tile glow), so identical paints on
    /// one screen - and across screens kept alive - share one texture. The last Release
    /// destroys it; nothing is kept for screens nobody is showing.
    internal static class PaintTextureCache
    {
        private sealed class Entry
        {
            public Texture2D Texture;
            public int Refs;
        }

        private static readonly Dictionary<string, Entry> s_entries = new Dictionary<string, Entry>();

        public static int Count => s_entries.Count;

        /// The texture for `key`, created (blank, `fresh` = true) when it does not exist yet.
        public static Texture2D Acquire(string key, out bool fresh, int w, int h, bool repeat)
        {
            if (s_entries.TryGetValue(key, out var e) && e.Texture != null)
            {
                e.Refs++;
                fresh = false;
                return e.Texture;
            }
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false, false)
            {
                name = "PaintBox",
                hideFlags = HideFlags.DontSave,
                wrapMode = repeat ? TextureWrapMode.Repeat : TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };
            s_entries[key] = new Entry { Texture = tex, Refs = 1 };
            fresh = true;
            return tex;
        }

        public static void Release(string key)
        {
            if (key == null || !s_entries.TryGetValue(key, out var e)) return;
            if (--e.Refs > 0) return;
            s_entries.Remove(key);
            if (e.Texture != null) Object.Destroy(e.Texture);
        }

        public static void Fill(Texture2D tex, in PaintRaster.Request req)
        {
            var px = new Color32[req.TexW * req.TexH];
            PaintRaster.Rasterize(req, px);
            Upload(tex, px);
        }

        public static void FillShadows(Texture2D tex, BoxShadow[] shadows, float w, float h, float radius, float pad)
        {
            var px = new Color32[tex.width * tex.height];
            PaintRaster.RasterizeShadows(shadows, w, h, radius, pad, tex.width, tex.height, px);
            Upload(tex, px);
        }

        private static void Upload(Texture2D tex, Color32[] px)
        {
            tex.SetPixels32(px);
            tex.Apply(false, true); // GPU copy only; frees the CPU-side pixels
        }
    }
}
