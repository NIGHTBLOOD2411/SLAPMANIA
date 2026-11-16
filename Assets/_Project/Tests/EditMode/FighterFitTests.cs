using NUnit.Framework;
using SlapMania.Core;

namespace SlapMania.Tests.EditMode
{
    /// FighterFit (M08, ADR-0023): every champion scaled to one body size for the slap table.
    public sealed class FighterFitTests
    {
        [Test]
        public void SameSize_IsUnscaled() => Assert.AreEqual(1f, FighterFit.UniformScale(1.05f, 1.05f), 1e-6f);

        [TestCase(1.045526f, 1.05f, 1.00428f)]  // Alucard
        [TestCase(1.061255f, 1.05f, 0.98939f)]  // Drakonit
        [TestCase(1.238287f, 1.05f, 0.84795f)]  // Hikaru: the tall one shrinks ~15%
        public void RealRigs_ScaleToTarget(float human, float target, float expected) =>
            Assert.AreEqual(expected, FighterFit.UniformScale(human, target), 1e-4f);

        [TestCase(0f)]
        [TestCase(-1.2f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(float.NegativeInfinity)]
        public void UnusableHumanScale_LeavesTheRigAlone(float human) =>
            Assert.AreEqual(1f, FighterFit.UniformScale(human, 1.05f));

        [TestCase(0f)]
        [TestCase(-1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void UnusableTarget_LeavesTheRigAlone(float target) =>
            Assert.AreEqual(1f, FighterFit.UniformScale(1.05f, target));

        [Test]
        public void TinyRig_ClampsAtMaxScale() =>
            Assert.AreEqual(FighterFit.MaxScale, FighterFit.UniformScale(0.01f, 1.05f));

        [Test]
        public void HugeRig_ClampsAtMinScale() =>
            Assert.AreEqual(FighterFit.MinScale, FighterFit.UniformScale(100f, 1.05f));

        [Test]
        public void ExactlyAtBounds_IsNotClamped()
        {
            Assert.AreEqual(2f, FighterFit.UniformScale(0.5f, 1f), 1e-6f);
            Assert.AreEqual(0.5f, FighterFit.UniformScale(2f, 1f), 1e-6f);
        }
    }
}
