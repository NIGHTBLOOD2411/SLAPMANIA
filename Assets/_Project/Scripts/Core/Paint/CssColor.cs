using System.Globalization;
using UnityEngine;

namespace SlapMania.Core.Paint
{
    /// CSS colour values as the V2 mockups write them: #rgb, #rgba, #rrggbb, #rrggbbaa,
    /// rgb()/rgba() with commas or the space/slash syntax, and the few keywords they use.
    /// Channels come back in sRGB 0..1 (gamma space), exactly as CSS interpolates them.
    public static class CssColor
    {
        public static bool TryParse(string s, out Color c)
        {
            c = default;
            if (string.IsNullOrEmpty(s)) return false;
            s = s.Trim();

            switch (s.ToLowerInvariant())
            {
                case "transparent": c = new Color(0f, 0f, 0f, 0f); return true;
                case "white":       c = Color.white; return true;
                case "black":       c = Color.black; return true;
                case "red":         c = new Color(1f, 0f, 0f, 1f); return true;
            }

            if (s[0] == '#') return TryHex(s.Substring(1), out c);

            int open = s.IndexOf('(');
            int close = s.LastIndexOf(')');
            if (open < 0 || close <= open || close != s.Length - 1) return false;
            string fn = s.Substring(0, open).Trim().ToLowerInvariant();
            if (fn != "rgb" && fn != "rgba") return false;

            string body = s.Substring(open + 1, close - open - 1).Replace('/', ' ').Replace(',', ' ');
            string[] parts = body.Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 3 || parts.Length > 4) return false;
            if (!Channel(parts[0], out float r) || !Channel(parts[1], out float g) || !Channel(parts[2], out float b)) return false;
            float a = 1f;
            if (parts.Length == 4 && !Alpha(parts[3], out a)) return false;
            c = new Color(r, g, b, a);
            return true;
        }

        /// Parse or throw — for literals the caller knows are valid (tests, builders).
        public static Color Parse(string s)
        {
            if (!TryParse(s, out var c)) throw new System.FormatException("Not a CSS colour: '" + s + "'");
            return c;
        }

        private static bool TryHex(string h, out Color c)
        {
            c = default;
            int n = h.Length;
            if (n != 3 && n != 4 && n != 6 && n != 8) return false;
            for (int i = 0; i < n; i++) if (Nibble(h[i]) < 0) return false;
            if (n <= 4)
            {
                float r = Nibble(h[0]) * 17 / 255f, g = Nibble(h[1]) * 17 / 255f, b = Nibble(h[2]) * 17 / 255f;
                float a = n == 4 ? Nibble(h[3]) * 17 / 255f : 1f;
                c = new Color(r, g, b, a);
                return true;
            }
            c = new Color(Byte(h, 0) / 255f, Byte(h, 2) / 255f, Byte(h, 4) / 255f, n == 8 ? Byte(h, 6) / 255f : 1f);
            return true;
        }

        private static int Byte(string h, int i) => Nibble(h[i]) * 16 + Nibble(h[i + 1]);

        private static int Nibble(char ch)
        {
            if (ch >= '0' && ch <= '9') return ch - '0';
            if (ch >= 'a' && ch <= 'f') return ch - 'a' + 10;
            if (ch >= 'A' && ch <= 'F') return ch - 'A' + 10;
            return -1;
        }

        private static bool Channel(string p, out float v) => Number(p, 255f, out v);

        private static bool Alpha(string p, out float v) => Number(p, 1f, out v);

        /// A finite number or percentage, divided by `scale` (or 100 for %) and clamped to
        /// 0..1. NaN and infinities are rejected: .NET parses "NaN", CSS does not.
        private static bool Number(string p, float scale, out float v)
        {
            v = 0f;
            bool pct = p.EndsWith("%");
            if (pct) p = p.Substring(0, p.Length - 1);
            if (!float.TryParse(p, NumberStyles.Float, CultureInfo.InvariantCulture, out float raw)) return false;
            if (float.IsNaN(raw) || float.IsInfinity(raw)) return false;
            v = Mathf.Clamp01(raw / (pct ? 100f : scale));
            return true;
        }
    }
}
