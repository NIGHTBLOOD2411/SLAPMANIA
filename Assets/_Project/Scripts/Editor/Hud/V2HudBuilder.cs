using UnityEngine;
using UnityEngine.UI;
using TMPro;
using SlapMania.UI;
using static SlapMania.Editor.SceneBuildUtil;
using static SlapMania.Editor.Hud.UiKit;
using S = SlapMania.Editor.Hud.HudStyle;

namespace SlapMania.Editor.Hud
{
    /// Builds the V2 in-match HUD from the reference screens (V2 HTML: C4, C5, C6, D1, D2, D3,
    /// E2, E3), coordinate for coordinate, and wires every serialized reference (R11).
    ///
    /// Each screen is a full-screen root with three layers:
    ///   Bleed — shades, vignettes, letterbox: reach the physical screen edge;
    ///   World — elements that track a fighter's face (rings, flashes); full-screen space;
    ///   Safe  — the 430-wide content column, inside Screen.safeArea.
    /// Only systems that exist are built (ADR-0011): no hype, rounds, economy, brace. The
    /// pause button is on the scorebug; the menu it opens is the UI Toolkit overlay (M10).
    internal static partial class V2HudBuilder
    {
        public sealed class Result
        {
            public GameObject Canvas;
            public V2Hud Hud;
        }

        private struct Layers
        {
            public RectTransform Root, Bleed, World, Safe;
        }

        public static Result Build()
        {
            S.Load();

            var canvasGo = new GameObject("HUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.layer = LayerMask.NameToLayer("UI");
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay; // M10 §1: never in a snapshot
            canvas.sortingOrder = 10;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(FrameW, FrameH); // the V2 frame, CSS px
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            var root = (RectTransform)canvasGo.transform;
            var hud = canvasGo.AddComponent<V2Hud>();

            // Order = draw order: later siblings draw on top.
            var intro = BuildIntro(root);
            var charge = BuildCharge(root);
            var timing = BuildTiming(root);
            var opponent = BuildOpponentTurn(root);
            var impact = BuildImpact(root);
            var scorebug = BuildScorebug(root);
            var knockout = BuildKnockout(root);
            var result = BuildResult(root);

            Set(hud, "_scorebug", scorebug);
            Set(hud, "_intro", intro);
            Set(hud, "_charge", charge);
            Set(hud, "_timing", timing);
            Set(hud, "_impact", impact);
            Set(hud, "_opponentTurn", opponent);
            Set(hud, "_knockout", knockout);
            Set(hud, "_result", result);
            var pauses = canvasGo.GetComponentsInChildren<HudPauseButton>(true);
            if (pauses.Length != 2)
                throw new System.InvalidOperationException("Expected 2 HUD pause buttons (scorebug, C6), built " + pauses.Length);
            SetArray(hud, "_pauseButtons", pauses);
            return new Result { Canvas = canvasGo, Hud = hud };
        }

        private static Layers View<T>(RectTransform canvas, string name, out T view) where T : HudView
        {
            var l = new Layers { Root = Stretch(canvas, name) };
            var group = l.Root.gameObject.AddComponent<CanvasGroup>();
            view = l.Root.gameObject.AddComponent<T>();
            Set(view, "_group", group);
            l.Bleed = Stretch(l.Root, "Bleed");
            l.World = Stretch(l.Root, "World");
            l.Safe = Stretch(l.Root, "Safe");
            l.Safe.gameObject.AddComponent<SafeAreaFitter>();
            return l;
        }

        // A nested Canvas isolates per-frame mesh rebuilds (timing ring, gauge) from the rest of
        // the HUD, so a moving ring does not re-batch the scorebug.
        private static void Isolate(RectTransform rt)
        {
            rt.gameObject.AddComponent<Canvas>();
        }

        // ── Scorebug (C4 / C5 / D1 / D2 top) ────────────────────────
        private static HudScorebug BuildScorebug(RectTransform canvas)
        {
            var l = View<HudScorebug>(canvas, "Scorebug", out var v);
            // 140 px, not the mockup's 250: just enough to seat the bar (attention budget, POLISH §9).
            var shade = Band(l.Bleed, "TopShade", 0f, 140f);
            Gradient(shade, S.Rgba(4, 5, 7, 0.92f), S.Rgba(4, 5, 7, 0.5f), S.Rgba(4, 5, 7, 0f), 0.55f, false);
            var shadeGroup = Group(shade);

            var slide = Stretch(l.Safe, "Slide");
            Cap(Box(slide, "Arena", 140f, 52f, 150f, 12f), "PROTOTYPE ARENA", 10f, S.Muted);
            var live = Live(Box(slide, "Live", 354f, 50f, 56f, 22f));
            PauseButton(Box(slide, "Pause", 16f, 42f, 38f, 38f), v);

            var bug = Box(slide, "Bug", 16f, 92f, 398f, 72f);
            Glass(bug);

            var oppTile = Child(bug, "OpponentPortrait", 0f, 0f, 72f, 72f);
            Panel(oppTile, S.Tile);
            var oppPortrait = Stretch(oppTile, "Image").gameObject.AddComponent<RawImage>();
            oppPortrait.raycastTarget = false;
            var oppFlash = Stretch(oppTile, "HitFlash");
            Panel(oppFlash, S.Rgba(227, 38, 47, 1f));
            var oppFlashGroup = Group(oppFlash, 0f);
            Panel(Child(bug, "GoldEdge", 72f, 0f, 3f, 72f), S.Gold);

            var oppName = Text(Child(bug, "OpponentName", 84f, 9f, 88f, 22f), "RIVAL", S.Display, 22f, S.Chalk, TextAlignmentOptions.Left);
            var oppHp = HpBarAt(bug, "OpponentHp", 84f, 36f, 88f, 10f, false);
            var oppSub = Cap(Child(bug, "OpponentSub", 84f, 52f, 88f, 10f), "CPU", 9f, S.Dim, TextAlignmentOptions.Left);

            var clockBox = Child(bug, "Clock", 172f, 0f, 54f, 72f);
            Panel(clockBox, S.Red);
            var clock = Text(Child(clockBox, "Time", 0f, 14f, 54f, 30f), "0.0", S.Number, 26f, Color.white);
            var round = Cap(Child(clockBox, "Round", 0f, 47f, 54f, 10f), "RD 1", 9f, Color.white);

            var playerName = Text(Child(bug, "PlayerName", 226f, 9f, 88f, 22f), "ALUCARD", S.Display, 22f, S.Chalk, TextAlignmentOptions.Right);
            var playerHp = HpBarAt(bug, "PlayerHp", 226f, 36f, 88f, 10f, true);
            var playerSub = Cap(Child(bug, "PlayerSub", 226f, 52f, 88f, 10f), "YOU", 9f, S.Dim, TextAlignmentOptions.Right);
            Panel(Child(bug, "RedEdge", 323f, 0f, 3f, 72f), S.Red);

            var playerTile = Child(bug, "PlayerPortrait", 326f, 0f, 72f, 72f);
            Panel(playerTile, S.Tile);
            var playerPortrait = Stretch(playerTile, "Image").gameObject.AddComponent<RawImage>();
            playerPortrait.raycastTarget = false;
            var playerFlash = Stretch(playerTile, "HitFlash");
            Panel(playerFlash, S.Rgba(227, 38, 47, 1f));
            var playerFlashGroup = Group(playerFlash, 0f);
            // Box() pivots at the centre, so the shake rotates the bar about its middle.

            Set(v, "_slideRoot", slide);
            Set(v, "_bug", bug);
            Set(v, "_liveDot", live);
            Set(v, "_topShade", shadeGroup);
            Set(v, "_oppPortrait", oppPortrait);
            Set(v, "_oppFlash", oppFlashGroup);
            Set(v, "_oppName", oppName);
            Set(v, "_oppSub", oppSub);
            Set(v, "_oppHp", oppHp);
            Set(v, "_playerPortrait", playerPortrait);
            Set(v, "_playerFlash", playerFlashGroup);
            Set(v, "_playerName", playerName);
            Set(v, "_playerSub", playerSub);
            Set(v, "_playerHp", playerHp);
            Set(v, "_clock", clock);
            Set(v, "_round", round);
            return v;
        }

        /// C4/C5/D2 `aria-label="Pause"`: a 38 px `.glass` square (radius 4, the kit's tile
        /// corner) with the two-bar icon, a 24-unit SVG drawn at 14 px in the middle. `owner`
        /// is the screen it sits on; Build collects every one for V2Hud.
        private static HudPauseButton PauseButton(RectTransform rt, HudView owner)
        {
            var glass = Glass(rt);
            glass.raycastTarget = true;
            var b = rt.gameObject.AddComponent<Button>();
            b.targetGraphic = glass;
            b.transition = Selectable.Transition.None; // PressScale is the feedback
            rt.gameObject.AddComponent<PressScale>();
            const float k = 14f / 24f, inset = 12f;    // (38 - 14) / 2
            Panel(Child(rt, "BarL", inset + 5f * k, inset + 4f * k, 5f * k, 16f * k), Color.white);
            Panel(Child(rt, "BarR", inset + 14f * k, inset + 4f * k, 5f * k, 16f * k), Color.white);
            var pause = rt.gameObject.AddComponent<HudPauseButton>();
            Set(pause, "_button", b);
            Set(pause, "_owner", owner);
            return pause;
        }

        /// V2 `.bar.tick` with a `.fg` gold fill and the red/white damage chunk behind it.
        private static HpBar HpBarAt(RectTransform parent, string name, float left, float top, float w, float h, bool reverse)
        {
            var rt = Child(parent, name, left, top, w, h);
            Shape(Panel(rt, new Color(1f, 1f, 1f, 0.1f)), corner: 1f);
            var trail = Stretch(rt, "DamageChunk");
            var trailPanel = Panel(trail, S.Red);
            var fill = Stretch(rt, "Fill");
            Gradient(fill, S.Hex("B8820F"), S.Hex("B8820F"), S.GoldLight, -1f, true);
            for (int i = 1; i < 10; i++) // the `.tick` overlay: a dark line every 10 %
            {
                var tick = Node(rt, "Tick" + i);
                tick.anchorMin = new Vector2(i * 0.1f - 0.005f, 0f);
                tick.anchorMax = new Vector2(i * 0.1f + 0.005f, 1f);
                tick.offsetMin = tick.offsetMax = Vector2.zero;
                Panel(tick, new Color(0f, 0f, 0f, 0.6f));
            }
            var bar = rt.gameObject.AddComponent<HpBar>();
            Set(bar, "_fill", fill);
            Set(bar, "_trail", trail);
            Set(bar, "_trailGraphic", trailPanel);
            Set(bar, "_reverse", reverse);
            return bar;
        }

        // ── C4 Round Countdown ───────────────────────────────────────
        private static HudIntro BuildIntro(RectTransform canvas)
        {
            var l = View<HudIntro>(canvas, "Intro", out var v);
            // Baked opaque: HudIntro sets the CanvasGroup to the budget's introDimAlpha.
            var dim = Stretch(l.Bleed, "Dim");
            Panel(dim, S.Rgba(4, 5, 7, 1f));
            var dimGroup = Group(dim);

            var title = Box(l.Safe, "Title", 0f, 262f, 430f, 64f, V.Center);
            Cap(Child(title, "GetReady", 0f, 0f, 430f, 14f), "GET READY", 12f, S.RedLight);
            var round = Text(Child(title, "Round", 0f, 16f, 430f, 46f), "ROUND 1", S.DisplayBlack, 46f, Color.white);

            // Lower third (centre y 690), under the fighters' faces; HudIntro scales it to the budget.
            var ring = Box(l.Safe, "Ring", 85f, 560f, 260f, 260f, V.Bottom);
            Arc(Local(ring, "Track", 0f, 0f, 260f, 260f), 130f, 12f, new Color(1f, 1f, 1f, 0.12f));
            var progress = Arc(Local(ring, "Progress", 0f, 0f, 260f, 260f), 130f, 12f, S.RedLight, 16f, S.Rgba(255, 75, 83, 0.8f));
            Glow(Local(ring, "Inner", 0f, 0f, 228f, 228f), S.Rgba(227, 38, 47, 0.35f), S.Rgba(7, 8, 10, 0.6f), S.Rgba(7, 8, 10, 0.6f), 0.7f);
            var countRoot = Local(ring, "Count", -7f, 0f, 260f, 230f);
            var count = Chroma(countRoot, "3", S.DisplayBlack, 210f);

            var hints = Box(l.Safe, "Hints", 20f, 790f, 390f, 22f, V.Bottom);
            // Copy is rewritten per attack mode by HudIntro (ADR-0014); these are the TapMarker lines.
            var hintCharge = Cap(Child(hints, "Charge", 0f, 0f, 182f, 22f), "TAP FAST TO CHARGE", 11f, Color.white, TextAlignmentOptions.Right);
            var hintTiming = Cap(Child(hints, "Timing", 208f, 0f, 182f, 22f), "TAP ON THE GOLD RING", 11f, Color.white, TextAlignmentOptions.Left);
            var hintsGroup = Group(hints);

            var footer = Box(l.Safe, "Footer", 0f, 876f, 430f, 16f, V.Bottom);
            Cap(Child(footer, "By", 0f, 0f, 211f, 16f), "ROUND PRESENTED BY", 9f, S.Dim, TextAlignmentOptions.Right);
            Text(Child(footer, "Sponsor", 219f, 0f, 211f, 16f), "MEGATRON ENERGY", S.Display, 14f, Color.white, TextAlignmentOptions.Left);
            var footerGroup = Group(footer);

            Set(v, "_dim", dimGroup);
            Set(v, "_titleRoot", title);
            Set(v, "_roundTitle", round);
            Set(v, "_ringRoot", ring);
            Set(v, "_progress", progress);
            Set(v, "_countRoot", countRoot);
            Set(v, "_count", count);
            Set(v, "_hints", hintsGroup);
            Set(v, "_hintCharge", hintCharge);
            Set(v, "_hintTiming", hintTiming);
            Set(v, "_footer", footerGroup);
            return v;
        }

        // ── C6 Wind-up · Power Charge ────────────────────────────────
        private static HudCharge BuildCharge(RectTransform canvas)
        {
            var l = View<HudCharge>(canvas, "Charge", out var v);
            var vignette = Stretch(l.Bleed, "Vignette");
            Glow(vignette, new Color(0f, 0f, 0f, 0f), S.Rgba(90, 0, 6, 0.55f), S.Rgba(20, 0, 2, 0.85f), 0.55f, true);
            var vignetteGroup = Group(vignette);
            // 70 px is the mockup; HudCharge sets the height from the budget's letterboxPx at bind.
            var top = Band(l.Bleed, "LetterboxTop", 0f, 70f);
            Panel(top, Color.black);
            var bottom = Band(l.Bleed, "LetterboxBottom", 862f, 70f, V.Bottom);
            bottom.pivot = new Vector2(0.5f, 0f);
            bottom.anchoredPosition = Vector2.zero;
            Panel(bottom, Color.black);
            // Pause. Not in the C6 mockup (added at the owner's request, 2026-10-20): the top-left
            // corner of the top letterbox, as far from the mashing area - the middle of the
            // screen - as the frame allows. A press on it is a click, not a charge tap.
            PauseButton(Box(l.Safe, "Pause", 16f, 16f, 38f, 38f), v);

            // Lock-on over the opponent's face, with the speed lines around it.
            var lockRoot = Local(l.World, "Lock", 0f, 0f, 0f, 0f);
            var speedLines = Local(lockRoot, "SpeedLines", 0f, 0f, 860f, 860f);
            Rays(speedLines, 60, 1f, 0.2f,
                 new Color(1f, 1f, 1f, 0f), new Color(1f, 1f, 1f, 0.2f), new Color(1f, 1f, 1f, 0.2f), 0.52f);
            var raysGroup = Group(speedLines);
            // Glow 12, not the mockup's 26: the reticle frames the face instead of covering it.
            Arc(Local(lockRoot, "Ring", 0f, 0f, 120f, 120f), 60f, 3f, S.RedLight, 12f, S.Rgba(255, 75, 83, 0.7f));
            var dot = Local(lockRoot, "Dot", 0f, 0f, 44f, 44f);
            Arc(dot, 22f, 22f, S.RedLight, 22f, S.Rgba(255, 75, 83, 0.8f));
            var spin = Local(lockRoot, "Reticle", 0f, 0f, 150f, 150f);
            for (int i = 0; i < 4; i++)
                Arc(Local(spin, "Tick" + i, 0f, 0f, 150f, 150f), 75f, 2f, S.RedLight, 0f, null, i * 90f - 12f, 24f);
            var tag = Local(lockRoot, "Locked", 0f, 82f, 60f, 16f);
            Panel(tag, S.Red);
            Cap(Stretch(tag, "Label"), "LOCKED", 10f, Color.white);

            // Mini HP, top right under the letterbox.
            var hpRow = Box(l.Safe, "MiniHp", 250f, 88f, 160f, 10f);
            var hpLabel = Cap(Child(hpRow, "Label", 0f, 0f, 72f, 10f), "RIVAL HP", 10f, S.Muted, TextAlignmentOptions.Right);
            var miniHp = HpBarAt(hpRow, "Bar", 80f, 1f, 80f, 8f, false);

            // The gauge: a 260 px semicircle, stroke 16, gold -> orange -> red, max zone at the end.
            // SVG arc centre is (215, 740) in the frame: a 300 px box centred there.
            var gauge = Box(l.Safe, "Gauge", 65f, 590f, 300f, 300f, V.Bottom);
            Isolate(gauge);
            Arc(Local(gauge, "Track", 0f, 0f, 300f, 300f), 138f, 16f, new Color(1f, 1f, 1f, 0.14f), 0f, null, -90f, 180f);
            var arc = Arc(Local(gauge, "Fill", 0f, 0f, 300f, 300f), 138f, 16f, S.Gold, 0f, null, -90f, 180f, 0f);
            arc.SetColors(S.Gold, S.Hex("FF7A2E"), S.RedLight, 0.7f);
            Arc(Local(gauge, "MaxZone", 0f, 0f, 300f, 300f), 142f, 24f, S.Rgba(255, 75, 83, 0.45f), 0f, null, 50.6f, 39.4f);

            var valueRoot = Box(l.Safe, "Value", 0f, 650f, 430f, 72f, V.Bottom);
            var value = Text(Stretch(valueRoot, "Text"), "0", S.Display, 72f, Color.white);
            var caption = Cap(Box(l.Safe, "Caption", 0f, 726f, 430f, 13f, V.Bottom), "POWER", 11f, S.Muted);

            var tapPulse = Box(l.Safe, "TapPulse", 0f, 778f, 430f, 64f, V.Bottom);
            var promptGroup = Group(tapPulse);
            var tapRoot = Stretch(tapPulse, "Tap");
            var prompt = Chroma(tapRoot, "TAP FAST!", S.DisplayBlack, 64f); // HOLD! / RELEASE! in HoldRing

            var chargeHint = Cap(Box(l.Safe, "Hint", 20f, 884f, 390f, 12f, V.Bottom), "EVERY TAP ADDS POWER · 2.5 SECONDS", 10f, S.Muted);

            Set(v, "_world", l.World);
            Set(v, "_letterTop", top);
            Set(v, "_letterBottom", bottom);
            Set(v, "_vignette", vignetteGroup);
            Set(v, "_lockRoot", lockRoot);
            Set(v, "_lockSpin", spin);
            Set(v, "_lockDot", dot.gameObject);
            Set(v, "_rays", raysGroup);
            Set(v, "_gauge", arc);
            Set(v, "_gaugeRoot", gauge);
            Set(v, "_valueRoot", valueRoot);
            Set(v, "_value", value);
            Set(v, "_caption", caption);
            Set(v, "_tapPulse", tapPulse);
            Set(v, "_tapRoot", tapRoot);
            Set(v, "_prompt", prompt);
            Set(v, "_promptGroup", promptGroup);
            Set(v, "_hint", chargeHint);
            Set(v, "_miniHp", miniHp);
            Set(v, "_miniHpLabel", hpLabel);
            return v;
        }

        // ── C5 Your Turn · Aim & Timing ──────────────────────────────
        private static HudTiming BuildTiming(RectTransform canvas)
        {
            var l = View<HudTiming>(canvas, "Timing", out var v);
            Glow(Stretch(l.Bleed, "Vignette"), new Color(0f, 0f, 0f, 0f), new Color(0f, 0f, 0f, 0f), S.Rgba(4, 5, 7, 0.45f), 0.35f, true);
            var shade = Band(l.Bleed, "BottomShade", 760f, 172f, V.Bottom);
            shade.pivot = new Vector2(0.5f, 0f);
            shade.anchoredPosition = Vector2.zero;
            Gradient(shade, S.Rgba(4, 5, 7, 0f), S.Rgba(4, 5, 7, 0f), S.Rgba(4, 5, 7, 0.95f), -1f, false);

            // Banner: white skew tag + caption.
            var banner = Box(l.Safe, "Banner", 130f, 214f, 170f, 38f);
            Shape(Panel(banner, Color.white), skew: 10f);
            Text(Stretch(banner, "Label"), "YOUR TURN", S.DisplayBlack, 26f, S.Ink);
            var bannerGroup = Group(banner);
            var caption = Box(l.Safe, "Caption", 0f, 256f, 430f, 12f);
            var captionText = Cap(caption, "TAP WHEN THE WHITE RING HITS GOLD", 10f, S.Hex("E7E9ED"));
            var captionGroup = Group(caption);

            // The ring, centred on the opponent's face.
            var ring = Local(l.World, "Ring", 0f, 0f, 200f, 200f);
            Isolate(ring);
            var closing = Arc(Local(ring, "Closing", 0f, 0f, 200f, 200f), 80f, 3f, Color.white, 8f, new Color(1f, 1f, 1f, 0.55f));
            var goldPulse = Local(ring, "GoldPulse", 0f, 0f, 200f, 200f);
            var gold = Arc(Stretch(goldPulse, "Band"), 45f, 6f, S.Rgba(233, 185, 73, 0.9f), 16f, S.Rgba(233, 185, 73, 0.75f));
            Arc(Local(ring, "Target", 0f, 0f, 44f, 44f), 22f, 22f, S.Rgba(255, 75, 83, 0.35f));
            Arc(Local(ring, "TargetEdge", 0f, 0f, 44f, 44f), 22f, 2f, S.RedLight);
            Panel(Local(ring, "TickN", 0f, 23f, 2f, 14f), Color.white);
            Panel(Local(ring, "TickS", 0f, -23f, 2f, 14f), Color.white);
            Panel(Local(ring, "TickW", -23f, 0f, 14f, 2f), Color.white);
            Panel(Local(ring, "TickE", 23f, 0f, 14f, 2f), Color.white);
            var perfect = Local(ring, "PerfectTag", -97f, 70f, 76f, 15f, 1f, 0.5f);
            Panel(perfect, S.Gold);
            Cap(Stretch(perfect, "Label"), "PERFECT ZONE", 9f, S.Hex("1A1204"));
            var mult = Local(ring, "MultTag", -97f, 53f, 84f, 15f, 1f, 0.5f);
            Panel(mult, new Color(0f, 0f, 0f, 0.7f));
            var multText = Cap(Stretch(mult, "Label"), "X3.0 DAMAGE", 9f, Color.white);

            // Locked power meter on the right, under one group: off by default (budget showPowerMeter).
            var meterRoot = Stretch(l.Safe, "PowerMeterGroup");
            var meterGroup = Group(meterRoot);
            var meter = Box(meterRoot, "PowerMeter", 386f, 290f, 28f, 330f, V.Center);
            Glass(meter);
            var inner = Node(meter, "Inner");
            inner.anchorMin = Vector2.zero; inner.anchorMax = Vector2.one;
            inner.offsetMin = new Vector2(4f, 4f); inner.offsetMax = new Vector2(-4f, -4f);
            var power = Stretch(inner, "Power");
            Gradient(power, S.Hex("6E7480"), S.Hex("6E7480"), S.Hex("3A3F48"), -1f, false);
            var bonus = Stretch(inner, "Bonus");
            Gradient(bonus, S.GoldLight, S.GoldLight, S.Gold, -1f, false);
            Shape(Panel(Box(meterRoot, "MaxFrame", 380f, 356f, 40f, 34f, V.Center), Color.clear), corner: 1f, border: 2f, borderColor: S.RedLight, fill: false);
            var meterValue = Text(Box(meterRoot, "PowerValue", 366f, 624f, 64f, 24f, V.Center), "0", S.Number, 22f, Color.white);
            Cap(Box(meterRoot, "PowerLabel", 366f, 650f, 64f, 10f, V.Center), "POWER", 8f, S.Muted);

            var hint = Cap(Box(l.Safe, "Hint", 150f, 836f, 260f, 14f, V.Bottom), "TAP ON THE GOLD RING", 11f, Color.white, TextAlignmentOptions.Right);
            var hintSub = Text(Box(l.Safe, "HintSub", 110f, 854f, 300f, 16f, V.Bottom), "Perfect lands x3.0 damage · the ring stops where you tap",
                               S.Body, 12f, S.Muted, TextAlignmentOptions.Right, 0f, false);

            // Hold ring (ADR-0014): at the press HudTiming fades everything but the World ring, and
            // C6 (HudCharge) takes the screen. The copy above is rewritten per attack mode on show.
            var chrome = Group(l.Safe);
            var shadeGroup = Group(l.Bleed);

            Set(v, "_world", l.World);
            Set(v, "_banner", banner);
            Set(v, "_bannerGroup", bannerGroup);
            Set(v, "_bannerCaption", captionGroup);
            Set(v, "_captionText", captionText);
            Set(v, "_chrome", chrome);
            Set(v, "_shade", shadeGroup);
            Set(v, "_hint", hint);
            Set(v, "_hintSub", hintSub);
            Set(v, "_ringRoot", ring);
            Set(v, "_closingRing", closing);
            Set(v, "_goldBand", gold);
            Set(v, "_goldPulse", goldPulse);
            Set(v, "_multTag", multText);
            Set(v, "_perfectTag", perfect);
            Set(v, "_multTagRoot", mult);
            Set(v, "_meterGroup", meterGroup);
            Set(v, "_meterPower", power);
            Set(v, "_meterBonus", bonus);
            Set(v, "_meterValue", meterValue);
            return v;
        }
    }
}
