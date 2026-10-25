using NUnit.Framework;
using SlapMania.Core;

namespace SlapMania.Tests.EditMode
{
    /// R3 boundary matrix for the hold ring's pure Core math (ADR-0014, M04 §5b):
    /// 0, 1, exact zone edges, NaN, negative and a huge hold for every function.
    public class HoldRingTests
    {
        private const double Close = 1.0;

        // ── CloseSeconds ─────────────────────────────────────────────
        [Test]
        public void CloseSeconds_DividesMsByRoundSpeed()
        {
            Assert.AreEqual(1.0, HoldRing.CloseSeconds(1000, 1.0), 1e-12);
            Assert.AreEqual(1.0 / 0.85, HoldRing.CloseSeconds(1000, 0.85), 1e-12); // round 1 default
            Assert.AreEqual(1.0 / 1.45, HoldRing.CloseSeconds(1000, 1.45), 1e-12); // round 10 default
        }

        [Test]
        public void CloseSeconds_BrokenSpeed_IsClampedOrNominal()
        {
            Assert.AreEqual(100.0, HoldRing.CloseSeconds(1000, 0.0), 1e-9);      // 0 -> MinRoundSpeed
            Assert.AreEqual(100.0, HoldRing.CloseSeconds(1000, -2.0), 1e-9);     // negative -> MinRoundSpeed
            Assert.AreEqual(1.0, HoldRing.CloseSeconds(1000, double.NaN), 1e-12); // NaN -> nominal 1.0
            Assert.Greater(HoldRing.CloseSeconds(1000, 1e9), 0.0);               // huge speed: tiny, still positive
        }

        [Test]
        public void CloseSeconds_NoCloseTime_IsZero()
        {
            Assert.AreEqual(0.0, HoldRing.CloseSeconds(0, 1.0));
            Assert.AreEqual(0.0, HoldRing.CloseSeconds(-500, 1.0));
            Assert.AreEqual(0.0, HoldRing.CloseSeconds(double.NaN, 1.0));
        }

        // ── ClosureAt ────────────────────────────────────────────────
        [Test]
        public void ClosureAt_Zero_IsOpen_AndCloseTime_IsClosed()
        {
            Assert.AreEqual(0f, HoldRing.ClosureAt(0.0, Close));
            Assert.AreEqual(1f, HoldRing.ClosureAt(Close, Close));
        }

        [Test]
        public void ClosureAt_IsLinearInTime()
        {
            Assert.AreEqual(0.25f, HoldRing.ClosureAt(0.25, Close), 1e-6);
            Assert.AreEqual(0.5f, HoldRing.ClosureAt(0.6, 1.2), 1e-6);
        }

        [TestCase(1f)]    // rounds 1-5
        [TestCase(0.7f)]  // round 10's zone scale
        public void ClosureAt_ExactZoneEdges_LandInTheLayoutsBand(float zoneScale)
        {
            // Every band edge (start inclusive), reached by holding exactly as long as the inverse
            // says: the ring must agree with the layout to the last bit, at any zone scale.
            const double close = 1.0 / 0.85;
            var layout = TestFactories.ZoneLayout();
            for (int i = 1; i < layout.bands.Length; i++)
            {
                float edge = SlapMania.Data.TimingZoneLayout.ScaleAboutCentre(layout.bands[i].start, zoneScale);
                float c = HoldRing.ClosureAt(HoldRing.HeldSecondsAt(edge, close), close);
                Assert.AreEqual(edge, c, 1e-6f, "band " + i);
                layout.Evaluate(edge, zoneScale, out var atEdge, out _);
                layout.Evaluate(edge - 1e-4f, zoneScale, out var before, out _);
                Assert.AreEqual(layout.bands[i].zone, atEdge, "at the start of band " + i);
                Assert.AreEqual(layout.bands[i - 1].zone, before, "just before band " + i);
            }
        }

        [Test]
        public void ClosureAt_GoldBand_IsSixtyMsAtOneSecondClose()
        {
            // The design number (M04 §4): Perfect is 0.06 of the bar, so 60 ms of ring at 1000 ms.
            var layout = TestFactories.ZoneLayout();
            layout.Evaluate(HoldRing.ClosureAt(0.4705, Close), 1f, out var open, out _);
            layout.Evaluate(HoldRing.ClosureAt(0.5295, Close), 1f, out var close, out _);
            layout.Evaluate(HoldRing.ClosureAt(0.4695, Close), 1f, out var early, out _);
            layout.Evaluate(HoldRing.ClosureAt(0.5305, Close), 1f, out var late, out _);
            Assert.AreEqual(TimingZone.Perfect, open);
            Assert.AreEqual(TimingZone.Perfect, close);
            Assert.AreEqual(TimingZone.Great, early);
            Assert.AreEqual(TimingZone.Great, late);
        }

        [Test]
        public void ClosureAt_NaNOrNegativeHold_IsOpen_NeverAFreePerfect()
        {
            Assert.AreEqual(0f, HoldRing.ClosureAt(double.NaN, Close));
            Assert.AreEqual(0f, HoldRing.ClosureAt(-0.3, Close));
            Assert.AreEqual(0f, HoldRing.ClosureAt(double.NegativeInfinity, Close));
            Assert.AreEqual(0f, HoldRing.ClosureAt(double.NaN, 0.0)); // NaN wins over a broken close time
        }

        [Test]
        public void ClosureAt_HugeHold_ClampsAtClosed()
        {
            Assert.AreEqual(1f, HoldRing.ClosureAt(1e9, Close));
            Assert.AreEqual(1f, HoldRing.ClosureAt(double.PositiveInfinity, Close));
        }

        [Test]
        public void ClosureAt_NoCloseTime_IsAlreadyClosed()
        {
            Assert.AreEqual(1f, HoldRing.ClosureAt(0.1, 0.0));
            Assert.AreEqual(1f, HoldRing.ClosureAt(0.1, -1.0));
            Assert.AreEqual(1f, HoldRing.ClosureAt(0.1, double.NaN));
        }

        // ── HeldSecondsAt (the AI's inverse) ─────────────────────────
        [TestCase(0f)] [TestCase(0.12f)] [TestCase(0.47f)] [TestCase(0.5f)]
        [TestCase(0.53f)] [TestCase(0.88f)] [TestCase(1f)]
        public void HeldSecondsAt_RoundTripsThroughClosureAt(float closure)
        {
            const double close = 1.0 / 0.85;
            double held = HoldRing.HeldSecondsAt(closure, close);
            Assert.AreEqual(closure, HoldRing.ClosureAt(held, close), 1e-6);
        }

        [Test]
        public void HeldSecondsAt_ClampsAndGuards()
        {
            Assert.AreEqual(0.0, HoldRing.HeldSecondsAt(float.NaN, Close));
            Assert.AreEqual(0.0, HoldRing.HeldSecondsAt(-0.5f, Close));
            Assert.AreEqual(Close, HoldRing.HeldSecondsAt(7f, Close));
            Assert.AreEqual(Close, HoldRing.HeldSecondsAt(float.PositiveInfinity, Close));
            Assert.AreEqual(0.0, HoldRing.HeldSecondsAt(0.5f, 0.0));
            Assert.AreEqual(0.0, HoldRing.HeldSecondsAt(0.5f, double.NaN));
        }

        // ── WindUpAt: the contract, whatever the curve's shape ───────
        private static readonly float[] Exponents = { 0.25f, 0.5f, 1f, 2f, 4f, 0f, -1f, float.NaN, float.PositiveInfinity };

        [Test]
        public void WindUpAt_IsZeroAtThePress_AndOneFromThePeakOn()
        {
            foreach (float e in Exponents)
            {
                Assert.AreEqual(0f, HoldRing.WindUpAt(0f, 0.5f, e), "press, exponent " + e);
                Assert.AreEqual(1f, HoldRing.WindUpAt(0.5f, 0.5f, e), "peak, exponent " + e);
                Assert.AreEqual(1f, HoldRing.WindUpAt(0.8f, 0.5f, e), "past peak holds, exponent " + e);
                Assert.AreEqual(1f, HoldRing.WindUpAt(1f, 0.5f, e), "fully closed, exponent " + e);
            }
        }

        [Test]
        public void WindUpAt_StaysInRange_AndNeverDecreases()
        {
            foreach (float e in Exponents)
            {
                float prev = 0f;
                for (int i = 0; i <= 100; i++)
                {
                    float w = HoldRing.WindUpAt(i / 100f, 0.5f, e);
                    Assert.IsFalse(float.IsNaN(w), "NaN at closure " + (i / 100f) + ", exponent " + e);
                    Assert.That(w, Is.InRange(0f, 1f), "closure " + (i / 100f) + ", exponent " + e);
                    Assert.GreaterOrEqual(w, prev - 1e-6f, "decreased at closure " + (i / 100f) + ", exponent " + e);
                    prev = w;
                }
            }
        }

        [Test]
        public void WindUpAt_LinearExponent_IsTheFractionOfThePeak()
        {
            Assert.AreEqual(0.5f, HoldRing.WindUpAt(0.25f, 0.5f, 1f), 1e-6);
        }

        [Test]
        public void WindUpAt_NaNOrNegativeClosure_IsUnwound()
        {
            Assert.AreEqual(0f, HoldRing.WindUpAt(float.NaN, 0.5f, 1f));
            Assert.AreEqual(0f, HoldRing.WindUpAt(-0.2f, 0.5f, 1f));
        }

        [Test]
        public void WindUpAt_NoPeak_IsFullFromTheFirstInstant()
        {
            Assert.AreEqual(1f, HoldRing.WindUpAt(0.01f, 0f, 1f));
            Assert.AreEqual(1f, HoldRing.WindUpAt(0.01f, -1f, 1f));
            Assert.AreEqual(1f, HoldRing.WindUpAt(0.01f, float.NaN, 1f));
        }

        // ── LatencyCompensation (shared with M04) ────────────────────
        [Test]
        public void LatencyCompensation_AddsBias_AndClamps()
        {
            Assert.AreEqual(0.030, LatencyCompensation.For(0.0, 0.030, 0.120), 1e-12);
            Assert.AreEqual(0.050, LatencyCompensation.For(0.020, 0.030, 0.120), 1e-12);
            Assert.AreEqual(0.120, LatencyCompensation.For(0.200, 0.030, 0.120), 1e-12); // a hitch is clamped
            Assert.AreEqual(0.120, LatencyCompensation.For(1e9, 0.030, 0.120), 1e-12);   // huge
        }

        [Test]
        public void LatencyCompensation_NeverNegative_AndNaNSafe()
        {
            Assert.AreEqual(0.0, LatencyCompensation.For(-0.5, 0.030, 0.120));          // event stamped in the future
            Assert.AreEqual(0.030, LatencyCompensation.For(double.NaN, 0.030, 0.120), 1e-12);
            Assert.AreEqual(0.020, LatencyCompensation.For(0.020, double.NaN, 0.120), 1e-12);
            Assert.AreEqual(0.0, LatencyCompensation.For(0.020, 0.030, -1.0));          // broken clamp: no back-dating
            Assert.AreEqual(0.0, LatencyCompensation.For(0.020, 0.030, double.NaN));
        }
    }
}
