using System.Globalization;

namespace SlapMania.Core.Paint
{
    /// A CSS length: pixels, a percentage of some reference the caller supplies, or the
    /// `calc(P% ± Npx)` form the kit's `.skew` / `.cc` clip-paths use.
    public readonly struct CssLength
    {
        public readonly float Value;
        public readonly bool IsPercent;
        /// Extra pixels added after resolving a percentage (calc). Zero otherwise.
        public readonly float OffsetPx;

        public CssLength(float value, bool isPercent, float offsetPx = 0f)
        {
            Value = value;
            IsPercent = isPercent;
            OffsetPx = offsetPx;
        }

        public static CssLength Px(float v) => new CssLength(v, false);
        public static CssLength Pct(float v) => new CssLength(v, true);

        /// Pixels, resolving a percentage against `reference` px.
        public float Resolve(float reference) => (IsPercent ? Value * 0.01f * reference : Value) + OffsetPx;

        /// "12px", "12", "40%", "0", "calc(100% - 10px)". Keywords left/top/center/right/bottom
        /// map to 0/50/100 %.
        public static bool TryParse(string s, out CssLength l)
        {
            l = default;
            if (string.IsNullOrEmpty(s)) return false;
            s = s.Trim().ToLowerInvariant();
            switch (s)
            {
                case "left": case "top":     l = Pct(0f);   return true;
                case "center":               l = Pct(50f);  return true;
                case "right": case "bottom": l = Pct(100f); return true;
            }
            if (s.StartsWith("calc(") && s.EndsWith(")")) return TryCalc(s.Substring(5, s.Length - 6), out l);
            return TrySimple(s, out l);
        }

        private static bool TrySimple(string s, out CssLength l)
        {
            l = default;
            bool pct = s.EndsWith("%");
            if (pct) s = s.Substring(0, s.Length - 1);
            else if (s.EndsWith("px")) s = s.Substring(0, s.Length - 2);
            if (!float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out float v)) return false;
            if (float.IsNaN(v) || float.IsInfinity(v)) return false;
            l = pct ? Pct(v) : Px(v);
            return true;
        }

        /// "A% - Bpx", "A% + Bpx", "Bpx + A%": one percentage and one pixel term at most.
        private static bool TryCalc(string body, out CssLength l)
        {
            l = default;
            string[] t = body.Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
            if (t.Length == 1) return TrySimple(t[0], out l);
            if (t.Length != 3 || (t[1] != "+" && t[1] != "-")) return false;
            if (!TrySimple(t[0], out var a) || !TrySimple(t[2], out var b)) return false;
            float sign = t[1] == "-" ? -1f : 1f;
            if (a.IsPercent && !b.IsPercent) { l = new CssLength(a.Value, true, sign * b.Value); return true; }
            if (!a.IsPercent && b.IsPercent)
            {
                // "10px - 50%" would need a negative percentage; the kit never writes it.
                if (sign < 0f) return false;
                l = new CssLength(b.Value, true, a.Value);
                return true;
            }
            if (!a.IsPercent && !b.IsPercent) { l = Px(a.Value + sign * b.Value); return true; }
            l = Pct(a.Value + sign * b.Value);
            return true;
        }
    }
}
