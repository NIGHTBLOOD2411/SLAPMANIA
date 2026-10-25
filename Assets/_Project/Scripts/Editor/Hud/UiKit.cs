using UnityEngine;
using UnityEngine.UI;
using TMPro;
using SlapMania.UI;

namespace SlapMania.Editor.Hud
{
    /// Layout helpers that let the builder copy coordinates straight out of the V2 HTML.
    ///
    /// The canvas reference is the reference frame itself — 430 x 932 CSS px — with CanvasScaler
    /// "Expand", so the whole frame is always visible and extra space on taller or wider
    /// screens opens up around it. Boxes take the CSS (left, top, width, height) and a vertical
    /// anchor: Top for the scorebug, Center for mid-screen blocks, Bottom for button rows. The
    /// 430-wide column stays centred horizontally, as in the mockups.
    internal static class UiKit
    {
        public const float FrameW = 430f;
        public const float FrameH = 932f;

        public enum V : byte { Top, Center, Bottom }

        // ── Rects ────────────────────────────────────────────────────
        public static RectTransform Node(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = LayerMask.NameToLayer("UI");
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            return rt;
        }

        /// Full-parent stretch.
        public static RectTransform Stretch(Transform parent, string name)
        {
            var rt = Node(parent, name);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            return rt;
        }

        /// A CSS box in the 430 x 932 frame. `pivot` defaults to the box centre.
        public static RectTransform Box(Transform parent, string name, float left, float top, float w, float h,
                                        V v = V.Top, float pivotX = 0.5f, float pivotY = 0.5f)
        {
            var rt = Node(parent, name);
            float ay = v == V.Top ? 1f : v == V.Center ? 0.5f : 0f;
            float refY = v == V.Top ? 0f : v == V.Center ? FrameH * 0.5f : FrameH;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, ay);
            rt.pivot = new Vector2(pivotX, pivotY);
            rt.sizeDelta = new Vector2(w, h);
            rt.anchoredPosition = new Vector2(left - FrameW * 0.5f + pivotX * w, refY - top - (1f - pivotY) * h);
            return rt;
        }

        /// A box spanning the full screen width (for bleeds), at CSS top/height.
        public static RectTransform Band(Transform parent, string name, float top, float h, V v = V.Top)
        {
            var rt = Node(parent, name);
            float ay = v == V.Top ? 1f : v == V.Center ? 0.5f : 0f;
            float refY = v == V.Top ? 0f : v == V.Center ? FrameH * 0.5f : FrameH;
            rt.anchorMin = new Vector2(0f, ay);
            rt.anchorMax = new Vector2(1f, ay);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.sizeDelta = new Vector2(0f, h);
            rt.anchoredPosition = new Vector2(0f, refY - top);
            return rt;
        }

        /// A box inside `parent`, in CSS coordinates relative to the parent's top-left.
        public static RectTransform Child(Transform parent, string name, float left, float top, float w, float h)
        {
            var rt = Node(parent, name);
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.sizeDelta = new Vector2(w, h);
            rt.anchoredPosition = new Vector2(left, -top);
            return rt;
        }

        /// A box centred on the parent's centre, offset in y-up pixels.
        public static RectTransform Local(Transform parent, string name, float x, float y, float w, float h,
                                          float pivotX = 0.5f, float pivotY = 0.5f)
        {
            var rt = Node(parent, name);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(pivotX, pivotY);
            rt.sizeDelta = new Vector2(w, h);
            rt.anchoredPosition = new Vector2(x, y);
            return rt;
        }

        /// Leaves raycasts on (the Unity default): a group with blocksRaycasts off would make any
        /// button inside it unclickable. Decorative graphics are non-raycast targets anyway.
        public static CanvasGroup Group(RectTransform rt, float alpha = 1f)
        {
            var g = rt.gameObject.AddComponent<CanvasGroup>();
            g.alpha = alpha;
            return g;
        }

        public static RectTransform Rotate(RectTransform rt, float degrees)
        {
            rt.localEulerAngles = new Vector3(0f, 0f, degrees);
            return rt;
        }

        // ── Graphics ─────────────────────────────────────────────────
        public static SlapPanel Panel(RectTransform rt, Color c)
        {
            var p = rt.gameObject.AddComponent<SlapPanel>();
            p.SetSolid(c);
            p.raycastTarget = false;
            return p;
        }

        public static SlapPanel Gradient(RectTransform rt, Color a, Color b, Color c, float mid, bool horizontal)
        {
            var p = rt.gameObject.AddComponent<SlapPanel>();
            p.raycastTarget = false;
            var so = new UnityEditor.SerializedObject(p);
            so.FindProperty("_colorA").colorValue = a;
            so.FindProperty("_colorB").colorValue = b;
            so.FindProperty("_colorC").colorValue = c;
            so.FindProperty("_midStop").floatValue = mid;
            so.FindProperty("_axis").enumValueIndex = horizontal ? 1 : 0;
            so.ApplyModifiedPropertiesWithoutUndo();
            return p;
        }

        public static SlapPanel Shape(SlapPanel p, float skew = 0f, float chamfer = 0f, float corner = 0f,
                                      float border = 0f, Color? borderColor = null, bool fill = true)
        {
            var so = new UnityEditor.SerializedObject(p);
            so.FindProperty("_skew").floatValue = skew;
            so.FindProperty("_chamfer").floatValue = chamfer;
            so.FindProperty("_corner").floatValue = corner;
            so.FindProperty("_border").floatValue = border;
            if (borderColor.HasValue) so.FindProperty("_borderColor").colorValue = borderColor.Value;
            so.FindProperty("_fill").boolValue = fill;
            so.ApplyModifiedPropertiesWithoutUndo();
            return p;
        }

        /// V2 `.glass`: dark translucent vertical gradient with a 1 px light edge.
        public static SlapPanel Glass(RectTransform rt)
        {
            var p = Gradient(rt, HudStyle.Rgba(30, 34, 42, 0.74f), HudStyle.Rgba(30, 34, 42, 0.74f), HudStyle.Rgba(10, 11, 14, 0.84f), -1f, false);
            return Shape(p, corner: 1.5f, border: 1f, borderColor: new Color(1f, 1f, 1f, 0.12f));
        }

        /// V2 `.tile`.
        public static SlapPanel TilePanel(RectTransform rt)
        {
            var p = Panel(rt, HudStyle.Tile);
            return Shape(p, corner: 1.5f, border: 1f, borderColor: new Color(1f, 1f, 1f, 0.09f));
        }

        public static SlapArc Arc(RectTransform rt, float radius, float thickness, Color c, float glow = 0f, Color? glowColor = null,
                                  float start = 0f, float sweep = 360f, float fill = 1f)
        {
            var a = rt.gameObject.AddComponent<SlapArc>();
            a.raycastTarget = false;
            var so = new UnityEditor.SerializedObject(a);
            so.FindProperty("_radius").floatValue = radius;
            so.FindProperty("_thickness").floatValue = thickness;
            so.FindProperty("_startAngle").floatValue = start;
            so.FindProperty("_sweep").floatValue = sweep;
            so.FindProperty("_fill").floatValue = fill;
            so.FindProperty("_colorStart").colorValue = c;
            so.FindProperty("_colorMid").colorValue = c;
            so.FindProperty("_colorEnd").colorValue = c;
            so.FindProperty("_glow").floatValue = glow;
            so.FindProperty("_glowColor").colorValue = glowColor ?? new Color(c.r, c.g, c.b, 0.5f);
            so.ApplyModifiedPropertiesWithoutUndo();
            return a;
        }

        public static SlapRays Rays(RectTransform rt, int count, float widthDeg, float inner, Color ci, Color cm, Color co, float midAt)
        {
            var r = rt.gameObject.AddComponent<SlapRays>();
            r.raycastTarget = false;
            r.Configure(count, widthDeg, inner, ci, cm, co, midAt);
            return r;
        }

        public static SlapGlow Glow(RectTransform rt, Color center, Color mid, Color edge, float midAt, bool coverCorners = false)
        {
            var g = rt.gameObject.AddComponent<SlapGlow>();
            g.raycastTarget = false;
            g.Configure(center, mid, edge, midAt, coverCorners);
            return g;
        }

        // ── Text ─────────────────────────────────────────────────────
        public static TextMeshProUGUI Text(RectTransform rt, string text, TMP_FontAsset font, float size, Color color,
                                           TextAlignmentOptions align = TextAlignmentOptions.Center, float spacing = 0f,
                                           bool upper = true, Material material = null)
        {
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            t.font = font;
            if (material != null) t.fontSharedMaterial = material;
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.alignment = align;
            t.characterSpacing = spacing;
            t.fontStyle = upper ? FontStyles.UpperCase : FontStyles.Normal;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.overflowMode = TextOverflowModes.Overflow;
            t.raycastTarget = false;
            t.margin = Vector4.zero;
            return t;
        }

        /// V2 `.cap`: condensed bold caps, +14% tracking.
        public static TextMeshProUGUI Cap(RectTransform rt, string text, float size, Color color,
                                          TextAlignmentOptions align = TextAlignmentOptions.Center)
            => Text(rt, text, HudStyle.Caption, size, color, align, 14f);

        /// V2 `.gt` / `.st` / `.rt`: a vertical colour gradient over the glyphs.
        public static TextMeshProUGUI GradientText(TextMeshProUGUI t, Color top, Color bottom)
        {
            t.color = Color.white;
            t.enableVertexGradient = true;
            t.colorGradient = new VertexGradient(top, top, bottom, bottom);
            return t;
        }

        /// V2 `.chroma` headline: red and cyan copies offset behind the white text.
        public static ChromaText Chroma(RectTransform rt, string text, TMP_FontAsset font, float size, float spacing = 0f)
        {
            var red = Stretch(rt, "Red");
            red.anchoredPosition = new Vector2(4f, 0f);
            var tRed = Text(red, text, font, size, new Color(1f, 0.157f, 0.275f, 0.75f), TextAlignmentOptions.Center, spacing);
            var cyan = Stretch(rt, "Cyan");
            cyan.anchoredPosition = new Vector2(-4f, 0f);
            var tCyan = Text(cyan, text, font, size, new Color(0.247f, 0.824f, 1f, 0.7f), TextAlignmentOptions.Center, spacing);
            var main = Stretch(rt, "Main");
            var tMain = Text(main, text, font, size, Color.white, TextAlignmentOptions.Center, spacing);

            var c = rt.gameObject.AddComponent<ChromaText>();
            var so = new UnityEditor.SerializedObject(c);
            so.FindProperty("_main").objectReferenceValue = tMain;
            so.FindProperty("_cyan").objectReferenceValue = tCyan;
            so.FindProperty("_red").objectReferenceValue = tRed;
            so.ApplyModifiedPropertiesWithoutUndo();
            return c;
        }

        // ── Kit pieces ───────────────────────────────────────────────
        /// V2 `.live` badge: red pill, white dot, italic "LIVE". Returns the dot's CanvasGroup.
        public static CanvasGroup Live(RectTransform rt)
        {
            Panel(rt, HudStyle.Red);
            var dot = Child(rt, "Dot", 9f, 8f, 6f, 6f);
            Panel(dot, Color.white);
            var g = Group(dot);
            var label = Child(rt, "Label", 21f, 0f, rt.sizeDelta.x - 25f, rt.sizeDelta.y);
            Text(label, "LIVE", HudStyle.Display, 13f, Color.white, TextAlignmentOptions.Left, 8f);
            return g;
        }

        /// V2 `.bp` primary button (red gradient, skewed) or `.bg` ghost button.
        public static Button MakeButton(RectTransform rt, string label, float size, bool primary = true)
        {
            SlapPanel p;
            if (primary) p = Gradient(rt, HudStyle.Hex("FF4A53"), HudStyle.Red, HudStyle.Hex("A90F18"), 0.5f, true);
            else p = Shape(Panel(rt, new Color(1f, 1f, 1f, 0.06f)), border: 1f, borderColor: new Color(1f, 1f, 1f, 0.2f));
            Shape(p, skew: 10f);
            p.raycastTarget = true;
            var b = rt.gameObject.AddComponent<Button>();
            b.targetGraphic = p;
            b.transition = Selectable.Transition.None; // PressScale is the feedback
            rt.gameObject.AddComponent<PressScale>();
            var l = Stretch(rt, "Label");
            Text(l, label, HudStyle.Display, size, Color.white);
            return b;
        }

        /// V2 `.bar` with a coloured `i` fill whose width the runtime drives by anchors.
        public static RectTransform Bar(RectTransform rt, Color fillA, Color fillB, bool verticalGradient = false)
        {
            Shape(Panel(rt, new Color(1f, 1f, 1f, 0.1f)), corner: 1f);
            var fill = Stretch(rt, "Fill");
            fill.anchorMax = new Vector2(0f, 1f);
            Gradient(fill, fillA, fillA, fillB, -1f, !verticalGradient);
            return fill;
        }

        /// V2 `.chip`.
        public static TextMeshProUGUI Chip(RectTransform row, string name, string text)
        {
            var chip = Node(row, name);
            Shape(Panel(chip, new Color(1f, 1f, 1f, 0.06f)), corner: 1f, border: 1f, borderColor: new Color(1f, 1f, 1f, 0.12f));
            var layout = chip.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(12, 12, 0, 0);
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            var fit = chip.gameObject.AddComponent<ContentSizeFitter>();
            fit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            var le = chip.gameObject.AddComponent<LayoutElement>();
            le.minHeight = 30f;
            le.preferredHeight = 30f;
            var label = Node(chip, "Label");
            return Text(label, text, HudStyle.Caption, 12f, HudStyle.Muted, TextAlignmentOptions.Center, 10f);
        }
    }
}
