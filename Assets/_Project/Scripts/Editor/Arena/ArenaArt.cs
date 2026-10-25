using UnityEngine;
using static SlapMania.Editor.SceneBuildUtil;

namespace SlapMania.Editor.Arena
{
    /// Generates every arena texture from code (ADR-0012): sponsor ribbon, ring-side boards,
    /// floor (albedo + metal/gloss), podium top, medallions, jumbotron overlay and signs.
    /// Files are overwritten in place, so their GUIDs — and the materials using them — survive
    /// a rebuild.
    internal static class ArenaArt
    {
        // Sponsor copy, as in the reference clip. Placeholder brands for the prototype: clear
        // them (trademark search) before anything public ships.
        public const string RibbonA = "MEGATRON ENERGY DRINK";
        public const string RibbonB = "MAX-IMPACT HANDS";
        public const string BoardA = "MEGATRON";
        public const string BoardASub = "ENERGY DRINK";
        public const string BoardB = "MALE";
        public const string SignText = "DAMAGE CAM";
        public const string SideText = "MEGATRON";

        private const string FontDir = "Assets/_Project/Art/Fonts/Barlow/";
        public static Font Italic => Paint.LoadFont(FontDir + "BarlowCondensed-ExtraBoldItalic.ttf");
        public static Font Black => Paint.LoadFont(FontDir + "BarlowCondensed-BlackItalic.ttf");
        public static Font Bold => Paint.LoadFont(FontDir + "BarlowCondensed-ExtraBold.ttf");
        public static Font Semi => Paint.LoadFont(FontDir + "Barlow-SemiBold.ttf");

        public static readonly Color Gold = Hex("C9963F");

        /// 16:1 tile of two slanted panels (purple / red) for the LED ribbon on the fascia.
        public static Texture2D Ribbon(string path)
        {
            const int W = 2048, H = 128;
            var cv = new Canvas2D(W, H, Color.black);
            Color aTop = Hex("7438D8"), aBot = Hex("37158A"), bTop = Hex("F02C46"), bBot = Hex("9A0C24");
            const float lean = 0.38f;
            for (int y = 0; y < H; y++)
            {
                float t = (y + 0.5f) / H;
                float sheen = 1f + 0.2f * Mathf.Exp(-Mathf.Pow((t - 0.8f) / 0.07f, 2f));
                for (int x = 0; x < W; x++)
                {
                    float xs = Mathf.Repeat(x + 0.5f - (y + 0.5f - H * 0.5f) * lean, W);
                    bool a = xs < W * 0.5f;
                    Color c = a ? Color.Lerp(aBot, aTop, t) : Color.Lerp(bBot, bTop, t);
                    c *= sheen;
                    float dsep = Mathf.Min(xs, Mathf.Abs(xs - W * 0.5f), W - xs);
                    if (dsep < 16f) c *= Mathf.Lerp(0.55f, 1f, Mathf.Clamp01((dsep - 6f) / 10f));
                    c = Color.Lerp(Hex("F2ECFF"), c, Mathf.Clamp01(dsep - 3f));
                    if (y >= H - 4) c = Color.Lerp(c, Color.white, 0.35f);
                    if (y < 3) c *= 0.35f;
                    c.a = 1f;
                    cv.Px[y * W + x] = c;
                }
            }
            Paint.Text(cv, Italic, RibbonA, W * 0.25f, H * 0.5f, 56f, 900f, Hex("E6DEFF"), Color.white, 0.02f, 0f, Hex("16063A", 0.9f), 3f);
            Paint.Text(cv, Italic, RibbonB, W * 0.75f, H * 0.5f, 56f, 900f, Hex("FFE0E4"), Color.white, 0.02f, 0f, Hex("46030E", 0.9f), 3f);
            return cv.Save(path, srgb: true, repeatU: true, repeatV: false, maxSize: 2048, alpha: false);
        }

        /// 6.4:1 tile of two backlit ring-side boards: the drink wordmark and the MALE crest.
        public static Texture2D Boards(string path)
        {
            const int W = 2048, H = 320;
            var cv = new Canvas2D(W, H, Hex("080B14"));
            for (int p = 0; p < 2; p++)
            {
                float x0 = p * 1024f, x1 = x0 + 1024f;
                cv.VGradient(x0, 0f, x1, H, Hex("05070F"), Hex("141B32"));
                cv.Shape(x0, 0f, x1, H, (x, y) => Mathf.Abs(Sdf.Box(x, y, (x0 + x1) * 0.5f, H * 0.5f, 492f, 138f, 6f)) - 1.5f, Hex("B8893A", 0.75f));
            }
            for (int p = 0; p <= 2; p++)
            {
                float x = p * 1024f;
                cv.Rect(x - 9f, 0f, x + 9f, H, Hex("010205"));
                cv.Rect(x + 9f, 0f, x + 11f, H, Hex("2A3040"));
            }
            cv.VGradient(0f, H - 10f, W, H, Hex("3A404E"), Hex("6A7080"));
            cv.Rect(0f, 0f, W, 8f, Hex("020306"));

            Paint.Text(cv, Bold, BoardA, 512f, 196f, 96f, 900f, Hex("B4B9C6"), Color.white, 0.03f, 0f, Hex("000000", 0.8f), 4f);
            Paint.Text(cv, Semi, BoardASub, 512f, 88f, 40f, 700f, Hex("C18A34"), Hex("F4D48A"), 0.2f);

            // MALE crest: a gold M-chevron over the wordmark.
            float cx = 1536f, cy = 214f;
            var pts = new[] { new Vector2(-92, -22), new Vector2(-46, 34), new Vector2(0, -8), new Vector2(46, 34), new Vector2(92, -22) };
            cv.Shape(cx - 110f, cy - 50f, cx + 110f, cy + 60f, (x, y) =>
            {
                float d = 1e9f;
                for (int i = 0; i < pts.Length - 1; i++)
                    d = Mathf.Min(d, Sdf.Capsule(x, y, cx + pts[i].x, cy + pts[i].y, cx + pts[i + 1].x, cy + pts[i + 1].y, 10f));
                return d;
            }, (x, y) => Color.Lerp(Hex("B07A2A"), Hex("F6D98E"), Mathf.InverseLerp(cy - 30f, cy + 40f, y)));
            Paint.Text(cv, Bold, BoardB, cx, 104f, 82f, 700f, Hex("C18A34"), Hex("F4D48A"), 0.14f, 0f, Hex("000000", 0.8f), 4f);
            return cv.Save(path, srgb: true, repeatU: true, repeatV: false, maxSize: 2048, alpha: false);
        }

        /// Floor across a disc of `radius` metres: slate centre, two gold rings, maroon apron.
        /// The mask is URP's metallic map: R = metallic (gold), A = smoothness.
        public static void Floor(string albedoPath, string maskPath, float radius, out Texture2D albedo, out Texture2D mask)
        {
            const int N = 2048, M = 1024;
            var cv = new Canvas2D(N, N, Color.black);
            var mk = new Canvas2D(M, M, Color.black);
            Color gold = Hex("C99A45");
            for (int pass = 0; pass < 2; pass++)
            {
                int size = pass == 0 ? N : M;
                var target = pass == 0 ? cv : mk;
                float mPerPx = 2f * radius / size;
                for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                    {
                        float wx = (x + 0.5f) * mPerPx - radius, wz = (y + 0.5f) * mPerPx - radius;
                        float r = Mathf.Sqrt(wx * wx + wz * wz);
                        float aa = mPerPx;
                        float inner = Mathf.Clamp01((3.0f - r) / aa + 0.5f);
                        float apron = Mathf.Clamp01((r - 4.62f) / aa + 0.5f);
                        float under = Mathf.Clamp01((r - 7.85f) / aa + 0.5f);
                        float g = Mathf.Max(Mathf.Clamp01((0.055f - Mathf.Abs(r - 3.02f)) / aa + 0.5f),
                                            Mathf.Clamp01((0.075f - Mathf.Abs(r - 4.6f)) / aa + 0.5f));
                        float n = Noise.Value(wx * 2.1f, wz * 2.1f, 3) * 0.6f + Noise.Value(wx * 19f, wz * 19f, 9) * 0.4f;
                        if (pass == 0)
                        {
                            Color slate = Color.Lerp(Hex("141823"), Hex("262C3C"), Mathf.Clamp01(1f - r / 3f));
                            Color band = Color.Lerp(Hex("161A25"), Hex("0F121A"), Mathf.Clamp01((r - 3f) / 1.6f));
                            Color maroon = Color.Lerp(Hex("520E18"), Hex("2A060C"), Mathf.Clamp01((r - 4.6f) / 3.2f));
                            Color c = Color.Lerp(band, slate, inner);
                            c = Color.Lerp(c, maroon, apron);
                            c = Color.Lerp(c, Hex("090305"), under);
                            c *= 1f + 0.14f * (n - 0.5f);
                            c = Color.Lerp(c, gold * (0.92f + 0.16f * n), g);
                            c.a = 1f;
                            target.Px[y * size + x] = c;
                        }
                        else
                        {
                            float smooth = Mathf.Lerp(Mathf.Lerp(0.62f, 0.66f, inner), 0.5f, apron);
                            smooth = Mathf.Lerp(smooth, 0.82f, g) + 0.1f * (n - 0.5f);
                            target.Px[y * size + x] = new Color(g, g, g, Mathf.Clamp01(smooth));
                        }
                    }
            }
            albedo = cv.Save(albedoPath, srgb: true, repeatU: false, repeatV: false, maxSize: 2048, alpha: false);
            mask = mk.Save(maskPath, srgb: false, repeatU: false, repeatV: false, maxSize: 1024, alpha: true);
        }

        /// Podium top inlay (0.47 x 0.92 m): dark slate, brass inlay border, the gold palm.
        public static void PodiumTop(string albedoPath, string maskPath, out Texture2D albedo, out Texture2D mask)
        {
            const int W = 512, H = 1024;
            var cv = new Canvas2D(W, H, Hex("15171E"));
            var mk = new Canvas2D(W, H, new Color(0f, 0f, 0f, 0.55f));
            cv.Grain(0.16f, 60f, 21);
            System.Func<float, float, float> border = (x, y) => Mathf.Abs(Sdf.Box(x, y, W * 0.5f, H * 0.5f, W * 0.5f - 26f, H * 0.5f - 26f, 10f)) - 6f;
            System.Func<float, float, float> line = (x, y) => Mathf.Abs(Sdf.Box(x, y, W * 0.5f, H * 0.5f, W * 0.5f - 52f, H * 0.5f - 52f, 6f)) - 1.5f;
            cv.Shape(0f, 0f, W, H, border, Gold);
            cv.Shape(0f, 0f, W, H, line, Gold * 0.85f);
            mk.ShapeMix(0f, 0f, W, H, border, new Color(1f, 0f, 0f, 0.8f));
            mk.ShapeMix(0f, 0f, W, H, line, new Color(1f, 0f, 0f, 0.8f));
            Paint.Hand(cv, mk, W * 0.5f, H * 0.52f, 380f, Gold, 16f);
            albedo = cv.Save(albedoPath, srgb: true, repeatU: false, repeatV: false, maxSize: 1024, alpha: false);
            mask = mk.Save(maskPath, srgb: false, repeatU: false, repeatV: false, maxSize: 1024, alpha: true);
        }

        /// Brass medallion with a milled rim and the palm, transparent outside (alpha-clipped).
        public static Texture2D Medallion(string path)
        {
            const int S = 512;
            var cv = new Canvas2D(S, S, new Color(0.1f, 0.08f, 0.05f, 0f));
            float c = S * 0.5f;
            cv.Shape(0f, 0f, S, S, (x, y) => Sdf.Circle(x, y, c, c, 246f),
                     (x, y) => Color.Lerp(Hex("6E4C1C"), Hex("EDC677"), Mathf.Clamp01(((x - y) / S) * 0.5f + 0.5f)));
            cv.Shape(0f, 0f, S, S, (x, y) =>
            {
                float a = Mathf.Atan2(y - c, x - c);
                float notch = Mathf.Abs(Mathf.Repeat(a / (2f * Mathf.PI) * 56f, 1f) - 0.5f) * 2f; // 0 at a notch centre
                float ring = Mathf.Abs(Sdf.Circle(x, y, c, c, 236f)) - 9f;
                return Mathf.Max(ring, (notch - 0.45f) * 30f);
            }, Hex("4E3410"));
            cv.Shape(0f, 0f, S, S, (x, y) => Sdf.Circle(x, y, c, c, 204f), Hex("111218"));
            cv.Shape(0f, 0f, S, S, (x, y) => Sdf.Ring(x, y, c, c, 210f, 3.5f), Hex("F0CD80"));
            Paint.Hand(cv, null, c, c + 6f, 300f, Gold, 11f);
            return cv.Save(path, srgb: true, repeatU: false, repeatV: false, maxSize: 512, alpha: true);
        }

        /// Broadcast furniture over the damage-cam feed: viewfinder corners and the LIVE bug.
        public static Texture2D JumboOverlay(string path)
        {
            const int W = 1024, H = 576;
            var cv = new Canvas2D(W, H, new Color(1f, 1f, 1f, 0f));
            Color white = new Color(1f, 1f, 1f, 0.92f);
            const float inset = 38f, arm = 84f, th = 6f;
            for (int sx = 0; sx < 2; sx++)
                for (int sy = 0; sy < 2; sy++)
                {
                    float x = sx == 0 ? inset : W - inset, y = sy == 0 ? inset : H - inset;
                    float dx = sx == 0 ? 1f : -1f, dy = sy == 0 ? 1f : -1f;
                    cv.Rect(Mathf.Min(x, x + dx * arm), Mathf.Min(y, y + dy * th), Mathf.Max(x, x + dx * arm), Mathf.Max(y, y + dy * th), white);
                    cv.Rect(Mathf.Min(x, x + dx * th), Mathf.Min(y, y + dy * arm), Mathf.Max(x, x + dx * th), Mathf.Max(y, y + dy * arm), white);
                }
            cv.RoundRect(64f, 62f, 196f, 116f, 6f, Hex("E3262F"));
            cv.Shape(76f, 78f, 100f, 100f, (x, y) => Sdf.Circle(x, y, 88f, 89f, 8f), Color.white);
            Paint.Text(cv, Black, "LIVE", 144f, 89f, 30f, 100f, Color.white, Color.white, 0.06f);
            return cv.Save(path, srgb: true, repeatU: false, repeatV: false, maxSize: 1024, alpha: true);
        }

        /// The red "DAMAGE CAM" LED sign under the screen.
        public static Texture2D Sign(string path)
        {
            const int W = 1024, H = 160;
            var cv = new Canvas2D(W, H, Hex("8A0D1B"));
            cv.VGradient(0f, 0f, W, H, Hex("7E0A18"), Hex("E2283A"));
            cv.Shape(0f, 0f, W, H, (x, y) => Mathf.Abs(Sdf.Box(x, y, W * 0.5f, H * 0.5f, W * 0.5f - 10f, H * 0.5f - 10f, 4f)) - 2f, Hex("FF8A8A", 0.7f));
            Paint.Text(cv, Black, SignText, W * 0.5f, H * 0.5f, 80f, 940f, Hex("FFE6E6"), Color.white, 0.05f, 0f, Hex("3A0008", 0.9f), 4f);
            return cv.Save(path, srgb: true, repeatU: false, repeatV: false, maxSize: 1024, alpha: false);
        }

        /// Tall red side panel with the wordmark reading bottom-to-top.
        public static Texture2D Side(string path)
        {
            const int W = 1024, H = 160;
            var cv = new Canvas2D(W, H, Hex("8A0D1B"));
            cv.VGradient(0f, 0f, W, H, Hex("9A0E1E"), Hex("D8243A"));
            cv.Rect(0f, 0f, W, 8f, Gold);
            cv.Rect(0f, H - 8f, W, H, Gold);
            Paint.Text(cv, Black, SideText, W * 0.5f, H * 0.5f, 84f, 900f, Hex("FFE6E6"), Color.white, 0.12f, 0f, Hex("3A0008", 0.9f), 4f);
            return cv.RotatedCcw().Save(path, srgb: true, repeatU: false, repeatV: false, maxSize: 1024, alpha: false);
        }
    }
}
