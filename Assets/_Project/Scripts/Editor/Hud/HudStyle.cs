using System.IO;
using UnityEditor;
using UnityEngine;
using TMPro;
using Object = UnityEngine.Object;

namespace SlapMania.Editor.Hud
{
    /// The V2 UI kit as editor data: palette, and TMP font assets for the kit's type classes,
    /// generated from the Barlow TTFs (OFL, Assets/_Project/Art/Fonts/Barlow) through TMP's own
    /// "Font Asset > SDF" command so every editor-only field is set the way TMP expects.
    ///
    ///   .d   display  Barlow Condensed ExtraBold Italic      .d9 Black Italic
    ///   .cap caption  Barlow Condensed Bold, caps, +14% track .n  numbers ExtraBold
    ///   body          Barlow Regular / SemiBold
    ///
    /// Existing font assets are reused; nothing is regenerated once it exists.
    internal static class HudStyle
    {
        public const string FontDir = "Assets/_Project/Art/Fonts/Barlow";

        // Printable ASCII plus the few symbols the V2 copy uses (x, middle dot, dashes, ellipsis).
        private const string Charset =
            " !\"#$%&'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`abcdefghijklmnopqrstuvwxyz{|}~×·–−…";

        // ── Palette (V2 UI kit) ──────────────────────────────────────
        public static readonly Color Arena    = Hex("07080A");
        public static readonly Color Tile     = Hex("15181E");
        public static readonly Color Raised   = Hex("1D2129");
        public static readonly Color Red      = Hex("E3262F");
        public static readonly Color RedLight = Hex("FF4B53");
        public static readonly Color Gold     = Hex("E9B949");
        public static readonly Color GoldLight= Hex("FFE39A");
        public static readonly Color Cyan     = Hex("3FD2FF");
        public static readonly Color Green    = Hex("2FD69B");
        public static readonly Color Chalk    = Hex("F3F4F6");
        public static readonly Color Muted    = Hex("A2A8B3");
        public static readonly Color Dim      = Hex("737985");
        public static readonly Color Ink      = Hex("0B0C0F");

        // ── Type ─────────────────────────────────────────────────────
        public static TMP_FontAsset Display;      // .d
        public static TMP_FontAsset DisplayBlack; // .d9
        public static TMP_FontAsset Caption;      // .cap
        public static TMP_FontAsset Number;       // .n
        public static TMP_FontAsset Body;
        public static TMP_FontAsset BodySemi;
        public static Material GoldShadow;        // .gt drop-shadow #3A2604
        public static Material DarkShadow;        // .st drop-shadow #16181C

        public static void Load()
        {
            Display      = LoadFontAsset("BarlowCondensed-ExtraBoldItalic");
            DisplayBlack = LoadFontAsset("BarlowCondensed-BlackItalic");
            Caption      = LoadFontAsset("BarlowCondensed-Bold");
            Number       = LoadFontAsset("BarlowCondensed-ExtraBold");
            Body         = LoadFontAsset("Barlow-Regular");
            BodySemi     = LoadFontAsset("Barlow-SemiBold");
            GoldShadow   = ShadowPreset(DisplayBlack, "Gold Shadow", Hex("3A2604"));
            DarkShadow   = ShadowPreset(DisplayBlack, "Dark Shadow", Hex("16181C"));
        }

        private static TMP_FontAsset LoadFontAsset(string ttf)
        {
            string assetPath = FontDir + "/" + ttf + " SDF.asset";
            var fa = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath);
            if (fa != null) return fa;

            var font = AssetDatabase.LoadAssetAtPath<Font>(FontDir + "/" + ttf + ".ttf");
            if (font == null) throw new FileNotFoundException("Missing font " + FontDir + "/" + ttf + ".ttf (download the Barlow set, see BUILD-AND-RUN).");
            var previous = Selection.objects;
            Selection.objects = new Object[] { font };
            bool ran = EditorApplication.ExecuteMenuItem("Assets/Create/TextMeshPro/Font Asset/SDF");
            Selection.objects = previous;
            fa = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath);
            if (!ran || fa == null) throw new System.InvalidOperationException("TMP did not create " + assetPath);

            fa.TryAddCharacters(Charset, out _);
            EditorUtility.SetDirty(fa);
            AssetDatabase.SaveAssets();
            return fa;
        }

        private static Material ShadowPreset(TMP_FontAsset fa, string suffix, Color shadow)
        {
            string path = FontDir + "/" + fa.name + " " + suffix + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m != null) return m;
            m = new Material(fa.material) { name = fa.name + " " + suffix };
            m.EnableKeyword("UNDERLAY_ON");
            m.SetColor("_UnderlayColor", shadow);
            m.SetFloat("_UnderlayOffsetX", 0f);
            m.SetFloat("_UnderlayOffsetY", -0.6f);
            m.SetFloat("_UnderlayDilate", 0.4f);
            m.SetFloat("_UnderlaySoftness", 0f);
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        public static Color Hex(string hex)
        {
            return ColorUtility.TryParseHtmlString("#" + hex, out var c) ? c : Color.magenta;
        }

        public static Color Hex(string hex, float alpha)
        {
            var c = Hex(hex);
            c.a = alpha;
            return c;
        }

        public static Color Rgba(int r, int g, int b, float a) => new Color(r / 255f, g / 255f, b / 255f, a);
    }
}
