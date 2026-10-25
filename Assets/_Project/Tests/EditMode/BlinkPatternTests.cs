using NUnit.Framework;
using SlapMania.Core.Motion;

namespace SlapMania.Tests.EditMode
{
    public class BlinkPatternTests
    {
        private static readonly float[] Pattern = { 1f, 0.2f, 0.9f, 0f };
        private const double Step = 100.0;

        [TestCase(0.0, 1f)]
        [TestCase(100.0, 0.2f)]
        [TestCase(200.0, 0.9f)]
        [TestCase(300.0, 0f)]
        public void StepStart_IsThatStepsLevel(double ms, float expected)
        {
            Assert.That(BlinkPattern.Evaluate(Pattern, ms, Step), Is.EqualTo(expected).Within(1e-5f));
        }

        [Test]
        public void Loops_AfterOneCycle()
        {
            Assert.That(BlinkPattern.Evaluate(Pattern, 400.0, Step), Is.EqualTo(1f).Within(1e-5f));
            Assert.That(BlinkPattern.Evaluate(Pattern, 4100.0, Step), Is.EqualTo(0.2f).Within(1e-5f));
        }

        [Test]
        public void StaysInRange_EverywhereInTheCycle()
        {
            for (double ms = 0.0; ms < 800.0; ms += 7.3)
            {
                float v = BlinkPattern.Evaluate(Pattern, ms, Step);
                Assert.That(v, Is.InRange(0f, 1f), "at " + ms + " ms");
            }
        }

        [Test]
        public void HugeElapsed_StaysInRange()
        {
            float v = BlinkPattern.Evaluate(Pattern, 1e12, Step);
            Assert.That(v, Is.InRange(0f, 1f));
        }

        [Test]
        public void OutOfRangeLevels_AreClamped()
        {
            var wild = new[] { 3f, -2f };
            Assert.That(BlinkPattern.Evaluate(wild, 0.0, Step), Is.EqualTo(1f));
            Assert.That(BlinkPattern.Evaluate(wild, 100.0, Step), Is.EqualTo(0f));
        }

        [Test]
        public void NaNLevel_IsZeroNotNaN()
        {
            Assert.That(BlinkPattern.Evaluate(new[] { float.NaN }, 0.0, Step), Is.EqualTo(0f));
        }

        [Test]
        public void NullOrEmpty_IsSteadyFull()
        {
            Assert.That(BlinkPattern.Evaluate(null, 50.0, Step), Is.EqualTo(1f));
            Assert.That(BlinkPattern.Evaluate(new float[0], 50.0, Step), Is.EqualTo(1f));
        }

        [TestCase(double.NaN)]
        [TestCase(-250.0)]
        [TestCase(double.PositiveInfinity)]
        public void BadElapsed_ReadsAsZero(double ms)
        {
            Assert.That(BlinkPattern.Evaluate(Pattern, ms, Step), Is.EqualTo(1f).Within(1e-5f));
        }

        [TestCase(0.0)]
        [TestCase(-10.0)]
        [TestCase(double.NaN)]
        public void BadStep_HoldsFirstLevel(double step)
        {
            Assert.That(BlinkPattern.Evaluate(Pattern, 150.0, step), Is.EqualTo(1f));
        }
    }
}
