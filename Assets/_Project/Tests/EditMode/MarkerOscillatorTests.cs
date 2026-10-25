using NUnit.Framework;
using SlapMania.Gameplay;

namespace SlapMania.Tests.EditMode
{
    public class MarkerOscillatorTests
    {
        [Test]
        public void At0_ReturnsStartPhase()
        {
            MarkerOscillator.PositionAt(0, 0.25, 1.0, out var pos, out var dir);
            Assert.AreEqual(0.25f, pos, 1e-4);
            Assert.AreEqual(+1, dir);
        }

        [Test]
        public void PingPongs_NotLoops()
        {
            MarkerOscillator.PositionAt(0.5, 0, 1.0, out var atHalf, out _);
            MarkerOscillator.PositionAt(1.5, 0, 1.0, out var atOneAndHalf, out _);
            Assert.AreEqual(0.5f, atHalf, 1e-4);
            Assert.AreEqual(0.5f, atOneAndHalf, 1e-4);
        }

        [Test]
        public void NoDrift_At10000Seconds()
        {
            MarkerOscillator.PositionAt(0, 0, 1.0, out var p0, out _);
            MarkerOscillator.PositionAt(10000.0, 0, 1.0, out var p10k, out _);
            Assert.AreEqual(p0, p10k, 1e-3);
        }

        [Test]
        public void NextTimeAtPosition_FindsForwardCrossing()
        {
            bool ok = MarkerOscillator.NextTimeAtPosition(0, 1.0, 0.5f, 0.1, 5.0, out var when);
            Assert.IsTrue(ok);
            MarkerOscillator.PositionAt(when, 0, 1.0, out var pos, out _);
            Assert.AreEqual(0.5f, pos, 1e-2);
            Assert.Greater(when, 0.1);
        }
    }
}
