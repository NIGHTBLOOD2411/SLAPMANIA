using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace SlapMania.Core.Paint
{
    /// Parses the CSS paint values the V2 mockups use — `background`, `mask-image`,
    /// `clip-path` and `box-shadow` — into plain data for PaintRaster.
    ///
    /// Parsing happens once per element when a screen is built, never per frame, so it may
    /// allocate. Anything it does not understand returns false/null rather than throwing:
    /// a bad value in a UXML file must degrade to "no paint", not take the screen down.
    public static class CssPaintParser
    {
        // ── background / mask-image ─────────────────────────────────
        /// Layers in CSS order: index 0 is the top layer.
        public static bool TryParseBackground(string css, out PaintLayer[] layers)
        {
            layers = null;
            if (string.IsNullOrWhiteSpace(css)) return false;
            var parts = SplitTopLevel(css, ',');
            var list = new List<PaintLayer>(parts.Count);
            foreach (var raw in parts)
            {
                if (!TryParseLayer(raw.Trim(), out var layer)) return false;
                list.Add(layer);
            }
            layers = list.ToArray();
            return layers.Length > 0;
        }

        public static bool TryParseLayer(string s, out PaintLayer layer)
        {
            layer = null;
            if (string.IsNullOrEmpty(s)) return false;
            if (CssColor.TryParse(s, out var solid)) { layer = PaintLayer.FromColor(solid); return true; }

            int open = s.IndexOf('(');
            int close = s.LastIndexOf(')');
            if (open < 0 || close <= open) return false;
            string fn = s.Substring(0, open).Trim().ToLowerInvariant();
            var args = SplitTopLevel(s.Substring(open + 1, close - open - 1), ',');
            if (args.Count == 0) return false;

            bool repeating = fn.StartsWith("repeating-");
            if (repeating) fn = fn.Substring("repeating-".Length);
            switch (fn)
            {
                case "linear-gradient": return TryLinear(args, repeating, out layer);
                case "radial-gradient": return TryRadial(args, repeating, out layer);
                case "conic-gradient":  return TryConic(args, repeating, out layer);
                default: return false;
            }
        }

        private static bool TryLinear(List<string> args, bool repeating, out PaintLayer layer)
        {
            layer = new PaintLayer { Kind = PaintKind.Linear, Repeating = repeating, AngleDeg = 180f };
            int first = 0;
            string head = args[0].Trim().ToLowerInvariant();
            if (TryAngle(head, out float deg)) { layer.AngleDeg = deg; first = 1; }
            else if (head.StartsWith("to "))
            {
                layer.AngleDeg = SideAngle(head.Substring(3).Trim());
                first = 1;
            }
            return TryStops(args, first, false, out layer.Stops);
        }

        private static bool TryRadial(List<string> args, bool repeating, out PaintLayer layer)
        {
            layer = new PaintLayer { Kind = PaintKind.Radial, Repeating = repeating };
            int first = 0;
            string head = args[0].Trim();
            if (!StartsWithColor(head))
            {
                first = 1;
                var tokens = SplitTopLevel(head, ' ');
                var sizes = new List<CssLength>(2);
                for (int i = 0; i < tokens.Count; i++)
                {
                    string t = tokens[i].ToLowerInvariant();
                    if (t == "circle") layer.Circle = true;
                    else if (t == "ellipse") layer.Circle = false;
                    else if (t == "at")
                    {
                        if (i + 1 < tokens.Count && CssLength.TryParse(tokens[i + 1], out var ax)) layer.AtX = ax;
                        if (i + 2 < tokens.Count && CssLength.TryParse(tokens[i + 2], out var ay)) layer.AtY = ay;
                        else if (i + 1 < tokens.Count) layer.AtY = CssLength.Pct(50f);
                        break;
                    }
                    else if (CssLength.TryParse(t, out var len)) sizes.Add(len);
                    // size keywords other than the default farthest-corner are not used by the kit
                }
                if (sizes.Count == 1) { layer.HasSize = true; layer.SizeX = layer.SizeY = sizes[0]; layer.Circle = true; }
                else if (sizes.Count >= 2) { layer.HasSize = true; layer.SizeX = sizes[0]; layer.SizeY = sizes[1]; }
            }
            return TryStops(args, first, false, out layer.Stops);
        }

        private static bool TryConic(List<string> args, bool repeating, out PaintLayer layer)
        {
            layer = new PaintLayer { Kind = PaintKind.Conic, Repeating = repeating, AngleDeg = 0f };
            int first = 0;
            string head = args[0].Trim();
            if (!StartsWithColor(head))
            {
                first = 1;
                var tokens = SplitTopLevel(head, ' ');
                for (int i = 0; i < tokens.Count; i++)
                {
                    string t = tokens[i].ToLowerInvariant();
                    if (t == "from" && i + 1 < tokens.Count && TryAngle(tokens[i + 1], out float from)) { layer.AngleDeg = from; i++; }
                    else if (t == "at")
                    {
                        if (i + 1 < tokens.Count && CssLength.TryParse(tokens[i + 1], out var ax)) layer.AtX = ax;
                        if (i + 2 < tokens.Count && CssLength.TryParse(tokens[i + 2], out var ay)) layer.AtY = ay;
                        break;
                    }
                }
            }
            return TryStops(args, first, true, out layer.Stops);
        }

        /// Colour stops from args[first..]. Supports "c", "c pos" and the double-position
        /// "c pos1 pos2" (two stops of one colour). Conic positions are degrees or %.
        private static bool TryStops(List<string> args, int first, bool conic, out ColorStop[] stops)
        {
            var list = new List<ColorStop>(args.Count);
            for (int i = first; i < args.Count; i++)
            {
                var tokens = SplitTopLevel(args[i].Trim(), ' ');
                if (tokens.Count == 0) continue;
                if (!CssColor.TryParse(tokens[0], out var c))
                {
                    // A lone position is an interpolation hint; the kit never uses one.
                    stops = null;
                    return false;
                }
                if (tokens.Count == 1) { list.Add(new ColorStop(c)); continue; }
                for (int k = 1; k < tokens.Count && k <= 2; k++)
                {
                    if (!TryStopPosition(tokens[k], conic, out float pos, out var unit)) { stops = null; return false; }
                    list.Add(new ColorStop(c, pos, unit));
                }
            }
            stops = list.ToArray();
            return stops.Length >= 1;
        }

        private static bool TryStopPosition(string t, bool conic, out float pos, out ColorStop.PositionUnit unit)
        {
            unit = ColorStop.PositionUnit.Fraction;
            t = t.Trim().ToLowerInvariant();
            if (conic && TryAngle(t, out float deg)) { pos = deg / 360f; return true; }
            if (t.EndsWith("%"))
            {
                bool ok = float.TryParse(t.Substring(0, t.Length - 1), NumberStyles.Float, CultureInfo.InvariantCulture, out float p);
                pos = p / 100f;
                return ok;
            }
            if (t.EndsWith("px"))
            {
                unit = ColorStop.PositionUnit.Px;
                return float.TryParse(t.Substring(0, t.Length - 2), NumberStyles.Float, CultureInfo.InvariantCulture, out pos);
            }
            // Unitless: only 0 is legal CSS.
            bool parsed = float.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out pos);
            if (parsed && pos != 0f) unit = ColorStop.PositionUnit.Px;
            return parsed;
        }

        public static bool TryAngle(string t, out float deg)
        {
            deg = 0f;
            t = t.Trim().ToLowerInvariant();
            if (t.EndsWith("deg"))
                return float.TryParse(t.Substring(0, t.Length - 3), NumberStyles.Float, CultureInfo.InvariantCulture, out deg);
            if (t.EndsWith("turn"))
            {
                bool ok = float.TryParse(t.Substring(0, t.Length - 4), NumberStyles.Float, CultureInfo.InvariantCulture, out float turns);
                deg = turns * 360f;
                return ok;
            }
            if (t.EndsWith("rad"))
            {
                bool ok = float.TryParse(t.Substring(0, t.Length - 3), NumberStyles.Float, CultureInfo.InvariantCulture, out float rad);
                deg = rad * Mathf.Rad2Deg;
                return ok;
            }
            return false;
        }

        private static float SideAngle(string side)
        {
            switch (side)
            {
                case "top":    return 0f;
                case "right":  return 90f;
                case "bottom": return 180f;
                case "left":   return 270f;
                case "top right": case "right top":       return 45f;
                case "bottom right": case "right bottom": return 135f;
                case "bottom left": case "left bottom":   return 225f;
                case "top left": case "left top":         return 315f;
                default: return 180f;
            }
        }

        private static bool StartsWithColor(string arg)
        {
            var tokens = SplitTopLevel(arg, ' ');
            return tokens.Count > 0 && CssColor.TryParse(tokens[0], out _);
        }

        // ── clip-path ───────────────────────────────────────────────
        public static ClipShape ParseClip(string css)
        {
            if (string.IsNullOrWhiteSpace(css)) return null;
            css = css.Trim();
            int open = css.IndexOf('(');
            int close = css.LastIndexOf(')');
            if (open < 0 || close <= open) return null;
            string fn = css.Substring(0, open).Trim().ToLowerInvariant();
            string body = css.Substring(open + 1, close - open - 1);

            if (fn == "polygon")
            {
                var pts = SplitTopLevel(body, ',');
                var xs = new CssLength[pts.Count];
                var ys = new CssLength[pts.Count];
                for (int i = 0; i < pts.Count; i++)
                {
                    var xy = SplitTopLevel(pts[i].Trim(), ' ');
                    if (xy.Count != 2 || !CssLength.TryParse(xy[0], out xs[i]) || !CssLength.TryParse(xy[1], out ys[i])) return null;
                }
                return pts.Count >= 3 ? new ClipShape { Kind = ClipShape.ShapeKind.Polygon, Xs = xs, Ys = ys } : null;
            }
            if (fn == "circle")
            {
                var tokens = SplitTopLevel(body.Trim(), ' ');
                var shape = new ClipShape { Kind = ClipShape.ShapeKind.Circle, Radius = CssLength.Pct(50f) };
                for (int i = 0; i < tokens.Count; i++)
                {
                    if (tokens[i] == "at")
                    {
                        if (i + 1 < tokens.Count && CssLength.TryParse(tokens[i + 1], out var cx)) shape.CenterX = cx;
                        if (i + 2 < tokens.Count && CssLength.TryParse(tokens[i + 2], out var cy)) shape.CenterY = cy;
                        break;
                    }
                    if (CssLength.TryParse(tokens[i], out var r)) shape.Radius = r;
                }
                return shape;
            }
            return null;
        }

        // ── box-shadow ──────────────────────────────────────────────
        public static BoxShadow[] ParseShadows(string css)
        {
            if (string.IsNullOrWhiteSpace(css) || css.Trim() == "none") return System.Array.Empty<BoxShadow>();
            var parts = SplitTopLevel(css, ',');
            var list = new List<BoxShadow>(parts.Count);
            foreach (var part in parts)
            {
                var tokens = SplitTopLevel(part.Trim(), ' ');
                bool inset = false;
                Color color = new Color(0f, 0f, 0f, 1f);
                var lens = new List<float>(4);
                foreach (var t in tokens)
                {
                    if (t == "inset") { inset = true; continue; }
                    if (CssColor.TryParse(t, out var c)) { color = c; continue; }
                    if (CssLength.TryParse(t, out var l) && !l.IsPercent) lens.Add(l.Value);
                }
                if (lens.Count < 2) continue;
                list.Add(new BoxShadow(inset, lens[0], lens[1], lens.Count > 2 ? lens[2] : 0f, lens.Count > 3 ? lens[3] : 0f, color));
            }
            return list.ToArray();
        }

        // ── Tokenising ──────────────────────────────────────────────
        /// Splits on `sep` outside parentheses; empty pieces are dropped for spaces.
        public static List<string> SplitTopLevel(string s, char sep)
        {
            var result = new List<string>();
            int depth = 0, start = 0;
            for (int i = 0; i < s.Length; i++)
            {
                char ch = s[i];
                if (ch == '(') depth++;
                else if (ch == ')') depth--;
                else if (depth == 0 && (ch == sep || (sep == ' ' && char.IsWhiteSpace(ch))))
                {
                    Add(result, s.Substring(start, i - start), sep);
                    start = i + 1;
                }
            }
            Add(result, s.Substring(start), sep);
            return result;
        }

        private static void Add(List<string> list, string piece, char sep)
        {
            piece = piece.Trim();
            if (piece.Length > 0 || sep != ' ') list.Add(piece);
        }
    }
}
