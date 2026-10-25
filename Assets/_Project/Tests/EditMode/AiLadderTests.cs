using NUnit.Framework;
using SlapMania.Core;
using SlapMania.Gameplay;

namespace SlapMania.Tests.EditMode
{
    /// The AI ladder (M07 §4b, DEBUG S-095). RollTier is checked on its contract (in range, near
    /// the level), not on one distribution, so the roll can be retuned without these changing.
    public class AiLadderTests
    {
        [Test]
        public void NextLevel_WinUp_LossDown_ClampedBothEnds()
        {
            Assert.AreEqual(1, AiLadder.NextLevel(0, true, 5));
            Assert.AreEqual(0, AiLadder.NextLevel(0, false, 5), "below the bottom");
            Assert.AreEqual(4, AiLadder.NextLevel(4, true, 5), "above the top");
            Assert.AreEqual(3, AiLadder.NextLevel(4, false, 5));
            Assert.AreEqual(0, AiLadder.NextLevel(int.MinValue, false, 5), "no overflow");
            Assert.AreEqual(4, AiLadder.NextLevel(int.MaxValue, true, 5), "no overflow");
            Assert.AreEqual(0, AiLadder.NextLevel(3, true, 1), "one tier: always 0");
            Assert.AreEqual(0, AiLadder.NextLevel(3, true, 0), "no tiers: 0, never negative");
        }

        [Test]
        public void RollTier_StaysInRange_AndWithinOneOfTheLevel([Values(-5, 0, 1, 2, 3, 4, 99)] int level)
        {
            const int tiers = 5;
            int clamped = level < 0 ? 0 : level > tiers - 1 ? tiers - 1 : level;
            var rng = new SeededRandom(1234 + level);
            for (int i = 0; i < 2000; i++)
            {
                int t = AiLadder.RollTier(level, tiers, rng);
                Assert.That(t, Is.InRange(0, tiers - 1), "roll " + i);
                Assert.LessOrEqual(System.Math.Abs(t - clamped), 1, "roll " + i + " jumped more than one tier");
            }
        }

        [Test]
        public void RollTier_DegenerateTierCounts_ReturnZero([Values(0, 1, -3)] int tierCount)
        {
            var rng = new SeededRandom(5);
            for (int i = 0; i < 100; i++) Assert.AreEqual(0, AiLadder.RollTier(2, tierCount, rng));
        }

        [Test]
        public void RollTier_IsReproducibleFromTheSeed()
        {
            var a = new SeededRandom(77);
            var b = new SeededRandom(77);
            for (int i = 0; i < 200; i++)
                Assert.AreEqual(AiLadder.RollTier(2, 5, a), AiLadder.RollTier(2, 5, b));
        }
    }
}
