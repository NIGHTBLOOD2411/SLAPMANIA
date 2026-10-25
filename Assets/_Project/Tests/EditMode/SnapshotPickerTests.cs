using NUnit.Framework;
using SlapMania.Core;
using SlapMania.Gameplay;

namespace SlapMania.Tests.EditMode
{
    /// The loser's slap gallery (M09, ADR-0016): finisher + hardest hits, chronological.
    public class SnapshotPickerTests
    {
        private const FighterSlot P = FighterSlot.Player, O = FighterSlot.Opponent;

        private static SlapShot S(int slap, FighterSlot defender, int damage, bool lethal = false)
            => new SlapShot(slap, defender, damage, lethal);

        [Test]
        public void Finisher_PlusTwoHardest_InTheOrderTheyHappened()
        {
            var shots = new[]
            {
                S(0, O, 20), S(1, P, 50), S(2, O, 35), S(3, P, 10), S(4, O, 12), S(5, P, 99), S(6, O, 30, lethal: true),
            };
            var into = new int[3];
            int n = SnapshotPicker.PickLoserGallery(shots, shots.Length, O, into);
            Assert.AreEqual(3, n);
            // Hardest on O besides the finisher: slap 2 (35) and slap 0 (20); slap 4 (12) is left out.
            Assert.AreEqual(new[] { 0, 2, 6 }, into, "chronological: slaps 0, 2, then the finisher");
        }

        [Test]
        public void TheFinisher_IsKept_EvenWhenItIsTheWeakestHit()
        {
            var shots = new[] { S(0, P, 40), S(2, P, 45), S(4, P, 50), S(6, P, 3, lethal: true) };
            var into = new int[3];
            int n = SnapshotPicker.PickLoserGallery(shots, shots.Length, P, into);
            Assert.AreEqual(3, n);
            Assert.AreEqual(new[] { 1, 2, 3 }, into);
        }

        [Test]
        public void NoFinisher_Decision_ThreeHardest()
        {
            var shots = new[] { S(0, O, 5), S(2, O, 25), S(4, O, 15), S(6, O, 30) };
            var into = new int[3];
            Assert.AreEqual(3, SnapshotPicker.PickLoserGallery(shots, shots.Length, O, into));
            Assert.AreEqual(new[] { 1, 2, 3 }, into);
        }

        [Test]
        public void FewerHitsThanFrames_WritesOnlyWhatExists([Values(0, 1, 2)] int hits)
        {
            var shots = new SlapShot[4];
            for (int i = 0; i < hits; i++) shots[i] = S(i * 2, P, 10 + i);
            shots[3] = S(9, O, 80); // the winner's hits never show
            var into = new int[3];
            Assert.AreEqual(hits, SnapshotPicker.PickLoserGallery(shots, hits == 0 ? 0 : hits, P, into));
            Assert.AreEqual(0, SnapshotPicker.PickLoserGallery(new[] { S(0, O, 80) }, 1, P, into));
        }

        [Test]
        public void DamageTie_GoesToTheLaterSlap()
        {
            var shots = new[] { S(0, P, 20), S(2, P, 20), S(4, P, 20), S(6, P, 20) };
            var into = new int[2];
            Assert.AreEqual(2, SnapshotPicker.PickLoserGallery(shots, shots.Length, P, into));
            Assert.AreEqual(new[] { 2, 3 }, into);
        }

        [Test]
        public void BadArguments_AreSafe()
        {
            var shots = new[] { S(0, P, 20) };
            Assert.AreEqual(0, SnapshotPicker.PickLoserGallery(null, 3, P, new int[3]));
            Assert.AreEqual(0, SnapshotPicker.PickLoserGallery(shots, 1, P, null));
            Assert.AreEqual(0, SnapshotPicker.PickLoserGallery(shots, 1, P, new int[0]));
            Assert.AreEqual(1, SnapshotPicker.PickLoserGallery(shots, 99, P, new int[3]), "count past the array is clamped");
        }

        [Test]
        public void SlotToEvict_IsTheWeakestNonLethal_EarlierOnATie()
        {
            var shots = new[] { S(0, P, 9), S(1, O, 4, lethal: true), S(2, O, 4), S(3, P, 4), S(4, O, 30) };
            Assert.AreEqual(2, SnapshotPicker.SlotToEvict(shots, shots.Length));
            Assert.AreEqual(0, SnapshotPicker.SlotToEvict(new[] { S(0, P, 1, lethal: true) }, 1), "all lethal: slot 0");
            Assert.AreEqual(0, SnapshotPicker.SlotToEvict(null, 0));
        }
    }
}
