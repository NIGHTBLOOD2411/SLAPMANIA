using NUnit.Framework;
using SlapMania.Core;

namespace SlapMania.Tests.EditMode
{
    public class TimingZoneLayoutTests
    {
        [Test]
        public void DefaultLayout_TilesUnitInterval()
        {
            var l = TestFactories.ZoneLayout();
            Assert.IsTrue(l.TilesUnitInterval(out var err), err);
        }

        [Test]
        public void CentreIsRed()
        {
            var l = TestFactories.ZoneLayout();
            l.Evaluate(0.50f, 1f, out var zone, out var mult);
            Assert.AreEqual(TimingZone.Perfect, zone);
            Assert.AreEqual(3.00f, mult, 1e-4);
        }

        [Test]
        public void EndpointsAreMiss()
        {
            var l = TestFactories.ZoneLayout();
            l.Evaluate(0.01f, 1f, out var z1, out _);
            l.Evaluate(0.99f, 1f, out var z2, out _);
            Assert.AreEqual(TimingZone.Miss, z1);
            Assert.AreEqual(TimingZone.Miss, z2);
        }

        [Test]
        public void ZoneScale_ShrinksTowardCentre()
        {
            var l = TestFactories.ZoneLayout();
            // at scale 0.5, a point at 0.30 (Weak boundary at scale 1.0) should now sit further from centre → Miss
            l.Evaluate(0.30f, 0.5f, out var zone, out _);
            Assert.AreEqual(TimingZone.Miss, zone);
        }
    }
}
