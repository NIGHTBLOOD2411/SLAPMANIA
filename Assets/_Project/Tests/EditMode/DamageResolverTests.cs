using NUnit.Framework;
using SlapMania.Core;
using SlapMania.Gameplay;

namespace SlapMania.Tests.EditMode
{
    public class DamageResolverTests
    {
        [Test]
        public void Floor_AppliesWhenRawIsBelowIt()
        {
            var cfg = TestFactories.DamageCfg();
            var input = new DamageInput(0.25f, 0.50f, 0, 100, 100, 0f);
            var r = DamageResolver.Resolve(input, cfg);
            Assert.AreEqual(cfg.minDamageFloor, r.FinalDamage, "raw ~2.75 rounds to 3 which equals floor");
            Assert.IsFalse(r.IsCrit);
            Assert.IsFalse(r.IsLethal);
        }

        [Test]
        public void Escalation_LaterSlapsHitHarder()
        {
            var cfg = TestFactories.DamageCfg();
            var early = DamageResolver.Resolve(new DamageInput(1f, 1f, 0, 200, 200, 0f), cfg);
            var late  = DamageResolver.Resolve(new DamageInput(1f, 1f, 10, 200, 200, 0f), cfg);
            Assert.Greater(late.FinalDamage, early.FinalDamage);
        }

        [Test]
        public void Crit_TriggersAtOrAboveThreshold()
        {
            var cfg = TestFactories.DamageCfg();
            var below = DamageResolver.Resolve(new DamageInput(1f, cfg.critThreshold - 0.01f, 0, 100, 100, 0f), cfg);
            var above = DamageResolver.Resolve(new DamageInput(1f, cfg.critThreshold, 0, 100, 100, 0f), cfg);
            Assert.IsFalse(below.IsCrit);
            Assert.IsTrue(above.IsCrit);
        }

        [Test]
        public void ExactZeroHp_LethalNotOverkill()
        {
            var cfg = TestFactories.DamageCfg();
            // craft input where final damage equals defenderHp
            var input = new DamageInput(1f, 3f, 0, 66, 100, 0f); // ~66 damage
            var r = DamageResolver.Resolve(input, cfg);
            Assert.IsTrue(r.IsLethal);
            Assert.IsTrue(r.IsFinisher);
            Assert.AreEqual(66, r.FinalDamage);
            Assert.AreEqual(0, r.HpAfter);
        }

        [Test]
        public void FinalDamage_ClampedToDefenderHp()
        {
            var cfg = TestFactories.DamageCfg();
            var r = DamageResolver.Resolve(new DamageInput(1.12f, 3.0f, 5, 10, 100, 0f), cfg);
            Assert.LessOrEqual(r.FinalDamage, 10);
            Assert.IsTrue(r.IsLethal);
            Assert.Greater(r.Overkill, 0);
        }
    }
}
