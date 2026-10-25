using NUnit.Framework;
using SlapMania.Core;
using SlapMania.Gameplay;

namespace SlapMania.Tests.EditMode
{
    public class HealthTrackerTests
    {
        [Test]
        public void Init_SetsCurrentToMax()
        {
            var h = new HealthTracker(FighterSlot.Player);
            h.Init(120);
            Assert.AreEqual(120, h.CurrentHp);
            Assert.AreEqual(120, h.MaxHp);
            Assert.IsFalse(h.IsDead);
        }

        [Test]
        public void ApplyDamage_ClampsToZero()
        {
            var h = new HealthTracker(FighterSlot.Player);
            h.Init(50);
            h.ApplyDamage(999);
            Assert.AreEqual(0, h.CurrentHp);
            Assert.IsTrue(h.IsDead);
        }

        [Test]
        public void ApplyDamage_FiresEventWithDelta()
        {
            var h = new HealthTracker(FighterSlot.Opponent);
            h.Init(100);
            HealthChangedEvent? seen = null;
            h.OnHealthChanged += e => seen = e;
            h.ApplyDamage(30);
            Assert.IsTrue(seen.HasValue);
            Assert.AreEqual(-30, seen.Value.Delta);
            Assert.AreEqual(70, seen.Value.CurrentHp);
        }
    }
}
