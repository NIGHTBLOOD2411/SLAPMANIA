using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using SlapMania.Data;

namespace SlapMania.Tests.EditMode
{
    /// The HUD attention budget (M10, POLISH §9) as executable rules: the defaults, the real
    /// asset (R11 - its serialized numbers are only trusted once loaded), and each Validate edge.
    public class HudAttentionConfigTests
    {
        private const string AssetPath = "Assets/_Project/Data/UI/HudAttention.asset";

        private static HudAttentionConfig Defaults() => ScriptableObject.CreateInstance<HudAttentionConfig>();

        [Test]
        public void Defaults_Validate()
        {
            Assert.IsTrue(Defaults().Validate(out string err), err);
            Assert.IsNull(err);
        }

        [Test]
        public void Defaults_MeetTheAttentionBudget()
        {
            // The owner-approved budget (2026-10-23, POLISH §9) as limits on the shipped defaults.
            var b = Defaults();
            Assert.LessOrEqual(b.impactHeadlineHoldMs + b.impactHeadlineFadeMs, 700, "D1 headline gone by 700 ms");
            Assert.LessOrEqual(b.introDimAlpha, 0.2f, "the intro camera shows through");
            Assert.Less(b.scorebugRestAlpha, 1f, "the scorebug rests below full alpha");
            Assert.IsFalse(b.liveDotPulse, "no looping motion in the periphery");
            Assert.IsFalse(b.opponentVignettePulse, "no looping motion in the periphery");
            Assert.IsFalse(b.showPowerMeter, "C5 side meter hidden (owner)");
            Assert.AreEqual(350, b.koSlamDelayMs, "K.O. slam after the finisher shot (owner)");
            Assert.LessOrEqual(b.letterboxPx, 40f, "thin C6 letterbox");
            Assert.LessOrEqual(b.chargeRaysKickAlpha * 0.2f, 0.1f, "mashing holds the speed lines at <= 0.1 final alpha");
            Assert.Greater(b.impactYieldMs, 0, "one headline at a time needs a yield");
        }

        [Test]
        public void RealAsset_Validates_WhenPresent()
        {
            var asset = AssetDatabase.LoadAssetAtPath<HudAttentionConfig>(AssetPath);
            if (asset == null) Assert.Ignore("No " + AssetPath + " yet (Build Playable Prototype creates it).");
            Assert.IsTrue(asset.Validate(out string err), err);
        }

        [Test]
        public void RealAsset_IsWiredIntoGameConfig_WhenPresent()
        {
            var cfg = AssetDatabase.LoadAssetAtPath<GameConfig>("Assets/_Project/Resources/GameConfig.asset");
            var asset = AssetDatabase.LoadAssetAtPath<HudAttentionConfig>(AssetPath);
            if (cfg == null || asset == null) Assert.Ignore("Prototype config assets not built.");
            Assert.AreSame(asset, cfg.hudAttention);
        }

        [Test]
        public void Seconds_ConvertsMilliseconds()
        {
            Assert.AreEqual(0f, HudAttentionConfig.Seconds(0));
            Assert.AreEqual(0.35f, HudAttentionConfig.Seconds(350), 1e-6f);
            Assert.AreEqual(-0.1f, HudAttentionConfig.Seconds(-100), 1e-6f);
            Assert.AreEqual(2147483.647f, HudAttentionConfig.Seconds(int.MaxValue), 1f);
        }

        [Test]
        public void Headline_ExactlyOneSecond_Passes_OneMsMore_Fails()
        {
            var b = Defaults();
            b.impactAutoHideMs = 1100;
            b.impactHeadlineHoldMs = 750; b.impactHeadlineFadeMs = 250;   // 1000: the edge
            Assert.IsTrue(b.Validate(out string err), err);
            b.impactHeadlineFadeMs = 251;
            Assert.IsFalse(b.Validate(out err));
            StringAssert.Contains("1000 ms", err);
        }

        [Test]
        public void Headline_OutlivingTheScreen_Fails()
        {
            var b = Defaults();
            b.impactAutoHideMs = 600;   // headline 450 + 250 = 700
            Assert.IsFalse(b.Validate(out string err));
            StringAssert.Contains("outlives", err);
        }

        [Test]
        public void DamageNumber_MayOverrunTheScreenBy100ms_NotMore()
        {
            var b = Defaults();
            b.impactAutoHideMs = 1000;
            b.impactDamageHoldMs = 800; b.impactDamageFadeMs = 300;        // 1100 = 1000 + 100
            Assert.IsTrue(b.Validate(out string err), err);
            b.impactDamageFadeMs = 301;
            Assert.IsFalse(b.Validate(out err));
            StringAssert.Contains("damage", err);
        }

        [Test]
        public void Yield_Edge300ms()
        {
            var b = Defaults();
            b.impactYieldMs = 300;
            Assert.IsTrue(b.Validate(out string err), err);
            b.impactYieldMs = 301;
            Assert.IsFalse(b.Validate(out _));
        }

        [Test]
        public void KoSlamDelay_Edge1000ms()
        {
            var b = Defaults();
            b.koSlamDelayMs = 1000;
            Assert.IsTrue(b.Validate(out string err), err);
            b.koSlamDelayMs = 1001;
            Assert.IsFalse(b.Validate(out _));
        }

        [Test]
        public void RaysKick_EqualToRest_Passes_Dimmer_Fails()
        {
            var b = Defaults();
            b.chargeRaysKickAlpha = b.chargeRaysRestAlpha;
            Assert.IsTrue(b.Validate(out string err), err);
            b.chargeRaysKickAlpha = b.chargeRaysRestAlpha - 0.01f;
            Assert.IsFalse(b.Validate(out _));
        }

        [Test]
        public void KoTint_SettledBelowFirst_Fails()
        {
            var b = Defaults();
            b.koSettledTintAlpha = b.koTintAlpha;
            Assert.IsTrue(b.Validate(out string err), err);
            b.koSettledTintAlpha = b.koTintAlpha - 0.01f;
            Assert.IsFalse(b.Validate(out _));
        }

        [Test]
        public void NaN_IsRejected()
        {
            // NaN compares false both ways: without the explicit check it would pass "kick >= rest".
            var b = Defaults();
            b.chargeRaysKickAlpha = float.NaN;
            Assert.IsFalse(b.Validate(out string err));
            StringAssert.Contains("NaN", err);

            b = Defaults();
            b.koBurstAlpha = float.NaN;
            Assert.IsFalse(b.Validate(out _));
        }

        [Test]
        public void NegativeTimes_AreNotAHugeBudget()
        {
            // [Min(0)] only clamps in the Inspector; a script can write -1. Negative ms never
            // breaks the "gone within" rules (it only shortens), so Validate accepts it.
            var b = Defaults();
            b.impactHeadlineHoldMs = -1;
            Assert.IsTrue(b.Validate(out string err), err);
        }

        [Test]
        public void GameConfig_RejectsAnInvalidBudget_AcceptsNone()
        {
            var c = ScriptableObject.CreateInstance<GameConfig>();
            c.bout = TestFactories.Bout();
            c.powerPhase = TestFactories.PowerCfg();
            c.timingPhase = TestFactories.TimingCfg();
            c.timingZoneLayout = TestFactories.ZoneLayout();
            c.damage = TestFactories.DamageCfg();
            c.aiProfiles = new[] { TestFactories.Ai() };
            c.feel = ScriptableObject.CreateInstance<FeelConfig>();
            Assert.IsTrue(c.Validate(out string err), "no budget = built-in defaults: " + err);

            c.hudAttention = Defaults();
            c.hudAttention.koSlamDelayMs = 5000;
            Assert.IsFalse(c.Validate(out err));
            StringAssert.StartsWith("hudAttention", err);
        }
    }
}
