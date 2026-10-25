using NUnit.Framework;
using UnityEngine;
using SlapMania.Data;

namespace SlapMania.Tests.EditMode
{
    public class GameConfigTests
    {
        private static GameConfig FullyWired()
        {
            var c = ScriptableObject.CreateInstance<GameConfig>();
            c.bout = TestFactories.Bout();
            c.powerPhase = TestFactories.PowerCfg();
            c.timingPhase = TestFactories.TimingCfg();
            c.timingZoneLayout = TestFactories.ZoneLayout();
            c.damage = TestFactories.DamageCfg();
            c.aiProfiles = new[] { TestFactories.Ai() };
            c.feel = ScriptableObject.CreateInstance<FeelConfig>();
            return c;
        }

        [Test]
        public void Validate_Passes_WhenEverythingIsWired()
        {
            Assert.IsTrue(FullyWired().Validate(out string err), err);
            Assert.IsNull(err);
        }

        [Test]
        public void Validate_Fails_WhenFeelMissing()
        {
            var c = FullyWired();
            c.feel = null;
            Assert.IsFalse(c.Validate(out string err));
            StringAssert.Contains("feel", err);
        }

        [Test]
        public void Validate_RequiresHoldRingConfig_OnlyInHoldRingMode()
        {
            var c = FullyWired();
            c.holdRing = null;
            Assert.IsTrue(c.Validate(out string err), "TapMarker runs without it: " + err);

            c.bout.attackMode = SlapMania.Core.AttackMode.HoldRing;
            Assert.IsFalse(c.Validate(out err));
            StringAssert.Contains("holdRing", err);

            c.holdRing = TestFactories.HoldRingCfg();
            Assert.IsTrue(c.Validate(out err), err);
        }

        [Test]
        public void Validate_Fails_WhenAiProfilesEmpty()
        {
            var c = FullyWired();
            c.aiProfiles = new AiProfile[0];
            Assert.IsFalse(c.Validate(out _));
        }

        [Test]
        public void ProfileForTier_ClampsBothEnds()
        {
            var c = FullyWired();
            var a = TestFactories.Ai();
            var b = TestFactories.Ai();
            c.aiProfiles = new[] { a, b };
            Assert.AreSame(a, c.ProfileForTier(-1));   // below range -> easiest
            Assert.AreSame(a, c.ProfileForTier(int.MinValue));
            Assert.AreSame(a, c.ProfileForTier(0));
            Assert.AreSame(b, c.ProfileForTier(1));
            Assert.AreSame(b, c.ProfileForTier(99));   // past the end -> hardest
            Assert.AreSame(b, c.ProfileForTier(int.MaxValue));
        }
    }
}
