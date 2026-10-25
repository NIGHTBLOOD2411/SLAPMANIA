using UnityEngine;
using UnityEngine.UI;
using TMPro;
using SlapMania.UI;
using static SlapMania.Editor.SceneBuildUtil;
using static SlapMania.Editor.Hud.UiKit;
using S = SlapMania.Editor.Hud.HudStyle;

namespace SlapMania.Editor.Hud
{
    internal static partial class V2HudBuilder
    {
        private static readonly Color GoldTop = new Color(1f, 0.953f, 0.788f);     // #FFF3C9
        private static readonly Color GoldBottom = new Color(0.722f, 0.510f, 0.059f); // #B8820F
        private static readonly Color Clear = new Color(0f, 0f, 0f, 0f);

        // ── D1 SMASH Impact ──────────────────────────────────────────
        private static HudImpact BuildImpact(RectTransform canvas)
        {
            var l = View<HudImpact>(canvas, "Impact", out var v);
            var impactVignette = Stretch(l.Bleed, "Vignette");
            Glow(impactVignette, Clear, Clear, S.Rgba(110, 0, 8, 0.6f), 0.6f, true);
            var impactVignetteGroup = Group(impactVignette, 0f);
            // No bottom shade (the mockup's 162 px at 0.95): the next-turn caption reads on the
            // arena floor, and the fighters' feet stay in the picture (attention budget, POLISH §9).

            var fx = Local(l.World, "Fx", 0f, 0f, 0f, 0f);
            var speed = Local(fx, "SpeedLines", 0f, 0f, 900f, 900f);
            Rays(speed, 60, 1f, 0.2f, Clear, new Color(1f, 1f, 1f, 0.24f), new Color(1f, 1f, 1f, 0.24f), 0.62f);
            var speedGroup = Group(speed, 0f);
            var flash = Local(fx, "Flash", 0f, 0f, 380f, 380f);
            Glow(flash, new Color(1f, 1f, 1f, 0.8f), S.Rgba(255, 240, 220, 0.35f), Clear, 0.37f);
            var flashGroup = Group(flash, 0f);
            var ring1 = Local(fx, "Ring1", 0f, 0f, 160f, 160f);
            Arc(ring1, 70f, 4f, new Color(1f, 1f, 1f, 0.9f), 20f, new Color(1f, 1f, 1f, 0.8f));
            var ring1Group = Group(ring1, 0f);
            var ring2 = Local(fx, "Ring2", 0f, 0f, 280f, 280f);
            Arc(ring2, 130f, 2f, new Color(1f, 1f, 1f, 0.35f));
            var ring2Group = Group(ring2, 0f);

            var dmg = Rotate(Local(l.World, "Damage", 0f, 0f, 260f, 120f), -6f);
            var dmgGroup = Group(dmg, 0f);
            var dmgText = GradientText(Text(Local(dmg, "Number", 0f, 14f, 260f, 96f), "-34", S.DisplayBlack, 96f, Color.white,
                                            TextAlignmentOptions.Left, 0f, true, S.GoldShadow), GoldTop, GoldBottom);
            var dmgCaption = Cap(Local(dmg, "Caption", 8f, -44f, 260f, 14f), "CRITICAL X1.4", 12f, S.GoldLight, TextAlignmentOptions.Left);

            var head = Rotate(Box(l.Safe, "Headline", -20f, 560f, 470f, 200f, V.Center), -6f);
            var headGroup = Group(head, 0f);
            var word = Chroma(Local(head, "Word", 0f, 34f, 470f, 132f), "SMASH!", S.DisplayBlack, 132f, -3f);
            var tag = Local(head, "Tag", 0f, -60f, 290f, 34f);
            Shape(Panel(tag, S.Red), skew: 10f);
            var tagText = Text(Stretch(tag, "Label"), "PERFECT · 96 POWER", S.Display, 22f, Color.white, TextAlignmentOptions.Center, 6f);

            var next = Cap(Box(l.Safe, "Next", 0f, 878f, 430f, 40f, V.Bottom), "RIVAL'S TURN NEXT", 10f, S.Muted);

            Set(v, "_world", l.World);
            Set(v, "_vignette", impactVignetteGroup);
            Set(v, "_fxRoot", fx);
            Set(v, "_flash", flash);
            Set(v, "_flashGroup", flashGroup);
            Set(v, "_speed", speed);
            Set(v, "_speedGroup", speedGroup);
            Set(v, "_ring1", ring1);
            Set(v, "_ring1Group", ring1Group);
            Set(v, "_ring2", ring2);
            Set(v, "_ring2Group", ring2Group);
            Set(v, "_damageRoot", dmg);
            Set(v, "_damageGroup", dmgGroup);
            Set(v, "_damage", dmgText);
            Set(v, "_damageCaption", dmgCaption);
            Set(v, "_headlineRoot", head);
            Set(v, "_headlineGroup", headGroup);
            Set(v, "_headline", word);
            Set(v, "_tag", tagText);
            Set(v, "_next", next);
            return v;
        }

        // ── D2 Opponent Turn ─────────────────────────────────────────
        private static HudOpponentTurn BuildOpponentTurn(RectTransform canvas)
        {
            var l = View<HudOpponentTurn>(canvas, "OpponentTurn", out var v);
            var vignette = Stretch(l.Bleed, "Vignette");
            var vignetteGroup = Group(vignette);
            Glow(Stretch(vignette, "Radial"), Clear, Clear, S.Rgba(170, 0, 12, 0.65f), 0.35f, true);
            Shape(Panel(Stretch(vignette, "Edge"), Color.clear), border: 3f, borderColor: S.Rgba(255, 75, 83, 0.7f), fill: false);

            var tag = Box(l.Safe, "Incoming", 115f, 196f, 200f, 44f);
            Shape(Panel(tag, S.Red), skew: 10f);
            Text(Stretch(tag, "Label"), "INCOMING!", S.DisplayBlack, 30f, Color.white);
            var tagGroup = Group(tag);

            var caption = Box(l.Safe, "Caption", 0f, 248f, 430f, 14f);
            var captionGroup = Group(caption);
            var lead = Cap(Child(caption, "Lead", 0f, 0f, 262f, 14f), "RIVAL WINDS UP ·", 11f, Color.white, TextAlignmentOptions.Right);
            var power = Cap(Child(caption, "Power", 268f, 0f, 162f, 14f), "0 POWER", 11f, S.Gold, TextAlignmentOptions.Left);

            Set(v, "_vignette", vignetteGroup);
            Set(v, "_tag", tag);
            Set(v, "_tagGroup", tagGroup);
            Set(v, "_caption", captionGroup);
            // Brace (ADR-0019): cue and result, low on the screen, clear of the faces.
            var brace = Box(l.Safe, "Brace", 40f, 600f, 350f, 96f, V.Center);
            var braceGroup = Group(brace, 0f);
            var bracePrompt = Text(Child(brace, "Prompt", 0f, 0f, 350f, 58f), "BRACE!", S.DisplayBlack, 54f, Color.white);
            var braceSub = Cap(Child(brace, "Sub", 0f, 66f, 350f, 14f), "TAP AS THE HAND LANDS", 12f, S.Chalk);

            Set(v, "_lead", lead);
            Set(v, "_power", power);
            Set(v, "_brace", braceGroup);
            Set(v, "_braceRoot", brace);
            Set(v, "_bracePrompt", bracePrompt);
            Set(v, "_braceSub", braceSub);
            return v;
        }

        // ── D3 Knockout ──────────────────────────────────────────────
        private static readonly Vector3[] KoConfetti =
        {
            new Vector3(60f, 150f, 30f), new Vector3(340f, 190f, -20f), new Vector3(110f, 560f, 60f), new Vector3(360f, 520f, 10f),
            new Vector3(200f, 120f, 80f), new Vector3(30f, 380f, -40f), new Vector3(390f, 360f, 45f), new Vector3(260f, 600f, -70f),
        };

        private static HudKnockout BuildKnockout(RectTransform canvas)
        {
            var l = View<HudKnockout>(canvas, "Knockout", out var v);
            var tint = Stretch(l.Bleed, "Tint");
            // Baked opaque: HudKnockout drives the group from the budget (koTintAlpha, then koSettledTintAlpha).
            Panel(tint, S.Rgba(142, 15, 22, 1f));
            var tintGroup = Group(tint, 0f);
            var burst = Stretch(l.Bleed, "Burst");
            var burstGroup = Group(burst, 0f);
            var rays = Box(burst, "Rays", -235f, -40f, 900f, 900f, V.Top);
            Rays(rays, 24, 5f, 0f, S.Rgba(255, 75, 83, 0.2f), S.Rgba(255, 75, 83, 0.1f), Clear, 0.4f);
            Glow(Box(burst, "HotSpot", -110f, 20f, 650f, 740f, V.Top), S.Rgba(255, 200, 120, 0.35f), S.Rgba(255, 200, 120, 0.08f), Clear, 0.5f);
            var confetti = Confetti(burst, KoConfetti, 8f, 16f);

            var header = Box(l.Safe, "Header", 20f, 56f, 390f, 22f);
            Cap(Child(header, "Label", 0f, 4f, 200f, 14f), "BOUT · K.O.", 11f, Color.white, TextAlignmentOptions.Left);
            Live(Child(header, "Live", 334f, 0f, 56f, 22f));

            var tilt = Rotate(Box(l.Safe, "KoTilt", 0f, 150f, 430f, 250f, V.Center), -5f);
            var ko = Local(tilt, "KO", 0f, 30f, 430f, 190f);
            GradientText(Text(Stretch(ko, "Text"), "K.O.", S.DisplayBlack, 230f, Color.white, TextAlignmentOptions.Center, -4f, true, S.GoldShadow),
                         GoldTop, GoldBottom);
            var koTag = Local(tilt, "Knockout", 0f, -96f, 250f, 50f);
            Shape(Panel(koTag, Color.white), skew: 10f);
            Text(Stretch(koTag, "Label"), "KNOCKOUT", S.DisplayBlack, 34f, S.Hex("B3121B"), TextAlignmentOptions.Center, 6f);
            var koTagGroup = Group(koTag, 0f);

            var winner = Box(l.Safe, "Winner", 40f, 500f, 350f, 44f, V.Center);
            var winnerGroup = Group(winner, 0f);
            var portraitRt = Child(winner, "Portrait", 0f, 0f, 44f, 44f);
            Panel(portraitRt, S.Tile);
            var portrait = Stretch(portraitRt, "Image").gameObject.AddComponent<RawImage>();
            portrait.raycastTarget = false;
            Shape(Panel(Stretch(portraitRt, "Border"), Color.clear), border: 2f, borderColor: S.Gold, fill: false);
            var line = Text(Child(winner, "Line", 56f, 0f, 294f, 44f), "YOU WIN THE BOUT", S.Display, 26f, Color.white, TextAlignmentOptions.Left);

            var stats = Box(l.Safe, "Stats", 20f, 580f, 390f, 96f, V.Center);
            Glass(stats);
            var statsGroup = Group(stats, 0f);
            string[] labels = { "FINAL BLOW", "BEST SLAP", "PERFECT" };
            Color[] colors = { Color.white, S.RedLight, S.Gold };
            var values = new TextMeshProUGUI[3];
            for (int i = 0; i < 3; i++)
            {
                var col = Child(stats, "Col" + i, i * 130f, 0f, 130f, 96f);
                values[i] = Text(Child(col, "Value", 0f, 18f, 130f, 36f), "0", S.Display, 32f, colors[i]);
                Cap(Child(col, "Caption", 0f, 62f, 130f, 10f), labels[i], 9f, S.Muted);
                if (i > 0) Panel(Child(stats, "Sep" + i, i * 130f, 12f, 1f, 72f), new Color(1f, 1f, 1f, 0.1f));
            }

            var cont = Box(l.Safe, "Continue", 20f, 790f, 390f, 60f, V.Bottom);
            var contGroup = Group(cont, 0f);
            var button = MakeButton(Child(cont, "Button", 0f, 0f, 390f, 60f), "CONTINUE", 24f);

            Set(v, "_tint", tintGroup);
            Set(v, "_burst", burstGroup);
            Set(v, "_rays", rays);
            SetArray(v, "_confetti", confetti);
            Set(v, "_koRoot", ko);
            Set(v, "_koTag", koTag);
            Set(v, "_koTagGroup", koTagGroup);
            Set(v, "_winnerGroup", winnerGroup);
            Set(v, "_winnerLine", line);
            Set(v, "_winnerPortrait", portrait);
            Set(v, "_statsGroup", statsGroup);
            Set(v, "_statFinal", values[0]);
            Set(v, "_statBest", values[1]);
            Set(v, "_statPerfect", values[2]);
            Set(v, "_continueGroup", contGroup);
            Set(v, "_continue", button);
            return v;
        }

        private static readonly Color[] ConfettiColors =
            { S.Gold, Color.white, S.RedLight, S.Gold, Color.white, S.GoldLight, Color.white, S.Gold };

        private static Object[] Confetti(RectTransform parent, Vector3[] spots, float w, float h)
        {
            var list = new Object[spots.Length];
            for (int i = 0; i < spots.Length; i++)
            {
                var c = Rotate(Box(parent, "Confetti" + i, spots[i].x, spots[i].y, w, h), spots[i].z);
                Panel(c, ConfettiColors[i % ConfettiColors.Length]);
                list[i] = c;
            }
            return list;
        }

        // ── E2 Victory / E3 Defeat ───────────────────────────────────
        private static readonly Vector3[] VictoryConfetti =
        {
            new Vector3(50f, 90f, 30f), new Vector3(360f, 130f, -25f), new Vector3(300f, 60f, 70f),
            new Vector3(120f, 190f, -60f), new Vector3(390f, 250f, 15f), new Vector3(24f, 260f, 50f),
        };

        private static HudResult BuildResult(RectTransform canvas)
        {
            var l = View<HudResult>(canvas, "Result", out var v);

            // ---- Victory ----
            var vRoot = Stretch(l.Root, "Victory");
            var vBleed = Stretch(vRoot, "Bleed");
            var vSafe = Stretch(vRoot, "Safe");
            vSafe.gameObject.AddComponent<SafeAreaFitter>();
            var vRays = Box(vBleed, "Rays", -235f, -260f, 900f, 900f, V.Top);
            Rays(vRays, 24, 5f, 0f, S.Rgba(255, 227, 154, 0.22f), S.Rgba(255, 227, 154, 0.1f), Clear, 0.4f);
            Fade(vBleed, 0.48f, S.Rgba(7, 8, 10, 0.3f), S.Rgba(7, 8, 10, 0.62f), 0.69f);
            var vConfetti = Confetti(vBleed, VictoryConfetti, 7f, 14f);

            // The loser's slap gallery (M09) above the title, where the scene shows through.
            var vGallery = Gallery(Box(vSafe, "Gallery", 45f, 48f, 340f, 146f, V.Center));
            var vCaption = Cap(Box(vSafe, "Caption", 0f, 196f, 430f, 14f, V.Center), "SINGLE BOUT · VS CPU", 12f, S.GoldLight);
            var vTitle = Box(vSafe, "Title", 0f, 214f, 430f, 100f, V.Center);
            GradientText(Text(Stretch(vTitle, "Text"), "VICTORY", S.DisplayBlack, 112f, Color.white, TextAlignmentOptions.Center, -3f, true, S.GoldShadow),
                         GoldTop, GoldBottom);
            var scoreRow = Box(vSafe, "Score", 0f, 322f, 430f, 30f, V.Center);
            var vScore = Text(Child(scoreRow, "Hp", 0f, 0f, 250f, 30f), "100 HP LEFT", S.Display, 28f, Color.white, TextAlignmentOptions.Right);
            var vVs = Cap(Child(scoreRow, "Vs", 262f, 9f, 168f, 14f), "VS RIVAL", 10f, S.Muted, TextAlignmentOptions.Left);

            var acc = Box(vSafe, "Accuracy", 20f, 412f, 390f, 100f, V.Center);
            TilePanel(acc);
            Cap(Child(acc, "Label", 16f, 14f, 200f, 14f), "TIMING ACCURACY", 11f, Color.white, TextAlignmentOptions.Left);
            var vAcc = Text(Child(acc, "Value", 254f, 10f, 120f, 24f), "0%", S.Display, 22f, S.Green, TextAlignmentOptions.Right);
            var vAccFill = Bar(Child(acc, "Bar", 16f, 46f, 358f, 8f), S.Hex("128A60"), S.Green);
            var vPerf = Text(Child(acc, "Perfects", 16f, 66f, 200f, 16f), "0 PERFECT HITS", S.Number, 13f, S.Muted, TextAlignmentOptions.Left);
            var vBest = Cap(Child(acc, "Best", 174f, 68f, 200f, 12f), "BEST SLAP 0", 10f, S.Gold, TextAlignmentOptions.Right);

            var pow = Box(vSafe, "Power", 20f, 534f, 390f, 26f, V.Center);
            Cap(Child(pow, "Label", 0f, 0f, 200f, 14f), "AVG POWER", 11f, Color.white, TextAlignmentOptions.Left);
            var vPower = Text(Child(pow, "Value", 270f, 0f, 120f, 16f), "0", S.Number, 14f, S.Cyan, TextAlignmentOptions.Right);
            var vPowerFill = Bar(Child(pow, "Bar", 0f, 20f, 390f, 6f), S.Hex("1478A8"), S.Cyan);

            var tiles = Box(vSafe, "Tiles", 20f, 590f, 390f, 112f, V.Center);
            string[] tileLabels = { "SLAPS LANDED", "DAMAGE DEALT", "PEAK TAPS / S" };
            var tileValues = new TextMeshProUGUI[3];
            float tw = (390f - 20f) / 3f;
            for (int i = 0; i < 3; i++)
            {
                var t = Child(tiles, "Tile" + i, i * (tw + 10f), 0f, tw, 112f);
                TilePanel(t);
                tileValues[i] = Text(Child(t, "Value", 0f, 30f, tw, 34f), "0", S.Display, 30f, Color.white);
                Cap(Child(t, "Caption", 0f, 74f, tw, 10f), tileLabels[i], 9f, S.Muted);
            }

            var chips = Box(vSafe, "Chips", 20f, 722f, 390f, 30f, V.Center);
            var row = chips.gameObject.AddComponent<HorizontalLayoutGroup>();
            row.spacing = 8f;
            row.childAlignment = TextAnchor.MiddleLeft;
            row.childControlWidth = true;
            row.childControlHeight = true;
            row.childForceExpandWidth = false;
            row.childForceExpandHeight = false;
            var chipPerfect = Chip(chips, "Perfect", "0 PERFECT");
            var chipFinish = Chip(chips, "Finish", "K.O.");
            var chipBest = Chip(chips, "Best", "BEST 0");
            var chipFlawless = Chip(chips, "Flawless", "FLAWLESS");

            var vButtonRow = Box(vSafe, "PlayAgain", 20f, 790f, 390f, 62f, V.Bottom);
            var vButton = MakeButton(Child(vButtonRow, "Button", 0f, 0f, 390f, 62f), "PLAY AGAIN", 26f);
            var vMenu = MakeButton(Box(vSafe, "Menu", 115f, 864f, 200f, 44f, V.Bottom), "MAIN MENU", 18f, primary: false);

            // ---- Defeat ----
            var dRoot = Stretch(l.Root, "Defeat");
            var dBleed = Stretch(dRoot, "Bleed");
            var dSafe = Stretch(dRoot, "Safe");
            dSafe.gameObject.AddComponent<SafeAreaFitter>();
            Fade(dBleed, 0.52f, S.Rgba(7, 8, 10, 0.4f), S.Rgba(7, 8, 10, 0.5f), 0.52f);

            var dTitle = Box(dSafe, "Title", 0f, 150f, 430f, 100f, V.Center);
            GradientText(Text(Stretch(dTitle, "Text"), "DEFEAT", S.DisplayBlack, 106f, Color.white, TextAlignmentOptions.Center, -3f, true, S.DarkShadow),
                         Color.white, S.Hex("8C929C"));
            var dRow = Box(dSafe, "Row", 0f, 262f, 430f, 30f, V.Center);
            var dVs = Cap(Child(dRow, "Vs", 0f, 9f, 206f, 14f), "VS RIVAL", 10f, S.Muted, TextAlignmentOptions.Right);
            var dDelta = Text(Child(dRow, "Delta", 222f, 2f, 208f, 26f), "0 HP LEFT", S.Display, 20f, S.RedLight, TextAlignmentOptions.Left);

            var coach = Box(dSafe, "Coach", 20f, 330f, 390f, 280f, V.Center);
            TilePanel(coach);
            Text(Child(coach, "Title", 16f, 14f, 358f, 26f), "COACH'S BREAKDOWN", S.Display, 22f, Color.white, TextAlignmentOptions.Left);
            string[] mLabels = { "TIMING ACCURACY", "CHARGE SPEED", "AVG SLAP POWER" };
            Color[] mColors = { S.RedLight, Color.white, Color.white };
            Color[] barA = { S.Hex("A90F18"), S.Hex("1478A8"), S.Hex("B8820F") };
            Color[] barB = { S.RedLight, S.Cyan, S.GoldLight };
            var mValues = new TextMeshProUGUI[3];
            var mFills = new RectTransform[3];
            for (int i = 0; i < 3; i++)
            {
                float y = 54f + i * 38f;
                Cap(Child(coach, "Label" + i, 16f, y, 250f, 12f), mLabels[i], 10f, S.Muted, TextAlignmentOptions.Left);
                mValues[i] = Text(Child(coach, "Value" + i, 254f, y - 5f, 120f, 20f), "0", S.Number, 18f, mColors[i], TextAlignmentOptions.Right);
                mFills[i] = Bar(Child(coach, "Bar" + i, 16f, y + 18f, 358f, 5f), barA[i], barB[i]);
            }
            var tip = Child(coach, "Tip", 16f, 172f, 358f, 92f);
            Panel(tip, S.Rgba(63, 210, 255, 0.08f));
            Panel(Child(tip, "Edge", 0f, 0f, 3f, 92f), S.Cyan);
            var tipText = Text(Child(tip, "Text", 14f, 10f, 334f, 72f), "Solid timing.", S.Body, 13f, S.Hex("D4D7DD"),
                               TextAlignmentOptions.TopLeft, 0f, false);
            tipText.textWrappingMode = TextWrappingModes.Normal;
            tipText.richText = true;

            // The player's own worst moments (M09), between the coaching and the rematch button.
            var dGallery = Gallery(Box(dSafe, "Gallery", 45f, 622f, 340f, 146f, V.Center));

            var dButtonRow = Box(dSafe, "Rematch", 20f, 790f, 390f, 62f, V.Bottom);
            var dButton = MakeButton(Child(dButtonRow, "Button", 0f, 0f, 390f, 62f), "REMATCH", 26f);
            var dMenu = MakeButton(Box(dSafe, "Menu", 115f, 864f, 200f, 44f, V.Bottom), "MAIN MENU", 18f, primary: false);

            // Staggered rows (CanvasGroups default to raycast-on, so the buttons stay clickable).
            var vRows = new Object[] { Group(scoreRow), Group(acc), Group(pow), Group(tiles), Group(chips), Group(vButtonRow) };
            var dRows = new Object[] { Group(dRow), Group(coach), Group(dButtonRow) };

            Set(v, "_victoryRoot", vRoot.gameObject);
            Set(v, "_vRays", vRays);
            SetArray(v, "_vConfetti", vConfetti);
            Set(v, "_vTitle", vTitle);
            Set(v, "_vScore", vScore);
            Set(v, "_vVs", vVs);
            Set(v, "_vAccuracy", vAcc);
            Set(v, "_vAccuracyFill", vAccFill);
            Set(v, "_vFooterLeft", vPerf);
            Set(v, "_vFooterRight", vBest);
            Set(v, "_vPower", vPower);
            Set(v, "_vPowerFill", vPowerFill);
            Set(v, "_vTileSlaps", tileValues[0]);
            Set(v, "_vTileDamage", tileValues[1]);
            Set(v, "_vTileSpeed", tileValues[2]);
            Set(v, "_chipPerfect", chipPerfect);
            Set(v, "_chipFinish", chipFinish);
            Set(v, "_chipBest", chipBest);
            Set(v, "_chipFlawless", chipFlawless.transform.parent.gameObject);
            SetArray(v, "_vRows", vRows);
            Set(v, "_vButton", vButton);
            Set(v, "_vMenu", vMenu);
            SetArray(v, "_vGalleryFrames", vGallery.Frames);
            SetArray(v, "_vGalleryPhotos", vGallery.Photos);
            SetArray(v, "_vGalleryDamage", vGallery.Damage);

            Set(v, "_defeatRoot", dRoot.gameObject);
            Set(v, "_dTitle", dTitle);
            Set(v, "_dVs", dVs);
            Set(v, "_dDelta", dDelta);
            Set(v, "_dAccuracy", mValues[0]);
            Set(v, "_dAccuracyFill", mFills[0]);
            Set(v, "_dSpeed", mValues[1]);
            Set(v, "_dSpeedFill", mFills[1]);
            Set(v, "_dPower", mValues[2]);
            Set(v, "_dPowerFill", mFills[2]);
            Set(v, "_dTip", tipText);
            SetArray(v, "_dRows", dRows);
            Set(v, "_dButton", dButton);
            Set(v, "_dMenu", dMenu);
            SetArray(v, "_dGalleryFrames", dGallery.Frames);
            SetArray(v, "_dGalleryPhotos", dGallery.Photos);
            SetArray(v, "_dGalleryDamage", dGallery.Damage);
            return v;
        }

        private struct GalleryRefs
        {
            public Object[] Frames;
            public Object[] Photos;
            public Object[] Damage;
        }

        private static readonly float[] GalleryTilt = { -6f, 0f, 6f };

        /// Three gallery frames across `box` (340 x 146): a white frame, a 4:5 photo (the snapshot's
        /// aspect, SlapSnapshotter 256 x 320) and the slap's damage in the bottom strip.
        private static GalleryRefs Gallery(RectTransform box)
        {
            var refs = new GalleryRefs { Frames = new Object[3], Photos = new Object[3], Damage = new Object[3] };
            for (int i = 0; i < 3; i++)
            {
                var frame = Rotate(Local(box, "GalleryFrame" + i, (i - 1) * 118f, i == 1 ? 3f : -2f, 104f, 142f), GalleryTilt[i]);
                Shape(Panel(frame, S.Chalk), corner: 1.5f);
                var photoRt = Child(frame, "Photo", 6f, 6f, 92f, 115f);
                Panel(photoRt, S.Tile);
                var photo = Stretch(photoRt, "Image").gameObject.AddComponent<RawImage>();
                photo.raycastTarget = false;
                var dmg = Text(Child(frame, "Damage", 0f, 121f, 104f, 19f), "-0", S.Display, 17f, S.Hex("B3121B"));
                refs.Frames[i] = frame;
                refs.Photos[i] = photo;
                refs.Damage[i] = dmg;
            }
            return refs;
        }

        /// E2/E3 background: the scene shows through the top, fading into solid arena black
        /// from `solidFrom` (a fraction of the screen height, measured from the bottom).
        private static void Fade(RectTransform bleed, float solidFrom, Color top, Color mid, float midAt)
        {
            var grad = Node(bleed, "Fade");
            grad.anchorMin = new Vector2(0f, solidFrom);
            grad.anchorMax = Vector2.one;
            grad.offsetMin = grad.offsetMax = Vector2.zero;
            Gradient(grad, top, mid, S.Arena, midAt, false);
            var solid = Node(bleed, "Solid");
            solid.anchorMin = Vector2.zero;
            solid.anchorMax = new Vector2(1f, solidFrom);
            solid.offsetMin = solid.offsetMax = Vector2.zero;
            Panel(solid, S.Arena);
        }
    }
}
