using NUnit.Framework;
using SlapMania.Core;
using SlapMania.Gameplay;

namespace SlapMania.Tests.EditMode
{
    public class SlapStatsTests
    {
        private static TimingResult T(float pos, TimingZone zone, int dir, bool timedOut = false) =>
            new TimingResult(pos, zone, 1f, timedOut, 0f, 0f, dir);

        private static DamageResult D(int dmg, bool lethal = false) =>
            new DamageResult(dmg, dmg, false, lethal, lethal, 0, 0);

        [TestCase(0.30f, +1, true)]   // closing in, not yet at the centre
        [TestCase(0.70f, +1, false)]  // passed the centre
        [TestCase(0.70f, -1, true)]   // coming back, not yet at the centre
        [TestCase(0.30f, -1, false)]  // passed it on the way back
        public void EarlyOrLate_DependsOnDirection(float pos, int dir, bool early)
        {
            var s = new SlapStats();
            s.RecordTiming(FighterSlot.Player, T(pos, TimingZone.Good, dir), 0.5f);
            Assert.AreEqual(early ? 1 : 0, s[FighterSlot.Player].Early);
            Assert.AreEqual(early ? 0 : 1, s[FighterSlot.Player].Late);
        }

        [Test]
        public void PerfectZone_CountsOnTime_WhateverTheDirection()
        {
            var s = new SlapStats();
            s.RecordTiming(FighterSlot.Player, T(0.49f, TimingZone.Perfect, +1), 0.5f);
            s.RecordTiming(FighterSlot.Player, T(0.51f, TimingZone.Perfect, -1), 0.5f);
            var f = s[FighterSlot.Player];
            Assert.AreEqual(2, f.Perfects);
            Assert.AreEqual(2, f.OnTime);
            Assert.AreEqual(0, f.Early + f.Late);
        }

        [Test]
        public void Closeness_IsOneAtCentre_ZeroAtEnds_AndNaNGetsNoCredit()
        {
            var s = new SlapStats();
            s.RecordTiming(FighterSlot.Player, T(0.5f, TimingZone.Perfect, +1), 0.5f);
            Assert.AreEqual(1f, s[FighterSlot.Player].TimingAccuracy01, 1e-5f);
            s.RecordTiming(FighterSlot.Player, T(0f, TimingZone.Miss, +1), 0.5f);
            s.RecordTiming(FighterSlot.Player, T(1f, TimingZone.Miss, -1), 0.5f);
            s.RecordTiming(FighterSlot.Player, T(float.NaN, TimingZone.Miss, +1), 0.5f);
            Assert.AreEqual(0.25f, s[FighterSlot.Player].TimingAccuracy01, 1e-5f);
            Assert.AreEqual(1, s[FighterSlot.Player].TimedOut, "NaN counts as a non-tap");
        }

        [Test]
        public void Tendency_None_Early_Late_NoTap()
        {
            var s = new SlapStats();
            Assert.AreEqual(TimingTendency.None, s[FighterSlot.Player].Tendency, "no commits yet");

            s.RecordTiming(FighterSlot.Player, T(0.2f, TimingZone.Weak, +1), 0.5f); // early
            s.RecordTiming(FighterSlot.Player, T(0.3f, TimingZone.Good, +1), 0.5f); // early
            s.RecordTiming(FighterSlot.Player, T(0.5f, TimingZone.Perfect, +1), 0.5f);
            Assert.AreEqual(TimingTendency.Early, s[FighterSlot.Player].Tendency);

            s.Reset();
            s.RecordTiming(FighterSlot.Player, T(0.8f, TimingZone.Weak, +1), 0.5f); // late
            s.RecordTiming(FighterSlot.Player, T(0.5f, TimingZone.Perfect, +1), 0.5f);
            Assert.AreEqual(TimingTendency.Late, s[FighterSlot.Player].Tendency, "half the commits missing late");

            s.Reset();
            s.RecordTiming(FighterSlot.Player, T(0.5f, TimingZone.Perfect, +1), 0.5f);
            s.RecordTiming(FighterSlot.Player, T(0.5f, TimingZone.Perfect, +1), 0.5f);
            s.RecordTiming(FighterSlot.Player, T(0.8f, TimingZone.Weak, +1), 0.5f);
            Assert.AreEqual(TimingTendency.None, s[FighterSlot.Player].Tendency, "mostly on time");

            s.Reset();
            s.RecordTiming(FighterSlot.Player, T(0.9f, TimingZone.Miss, +1, timedOut: true), 0.5f);
            s.RecordTiming(FighterSlot.Player, T(0.1f, TimingZone.Miss, -1, timedOut: true), 0.5f);
            s.RecordTiming(FighterSlot.Player, T(0.5f, TimingZone.Perfect, +1), 0.5f);
            Assert.AreEqual(TimingTendency.NoTap, s[FighterSlot.Player].Tendency);
        }

        [Test]
        public void Impacts_TrackBestSlap_DamageTaken_AndFinalBlow()
        {
            var s = new SlapStats();
            s.RecordImpact(FighterSlot.Player, D(12));
            s.RecordImpact(FighterSlot.Opponent, D(30));
            s.RecordImpact(FighterSlot.Player, D(41, lethal: true));
            var p = s[FighterSlot.Player];
            Assert.AreEqual(2, p.Slaps);
            Assert.AreEqual(53, p.TotalDamage);
            Assert.AreEqual(41, p.BestSlap);
            Assert.AreEqual(30, p.DamageTaken);
            Assert.IsFalse(p.Flawless);
            Assert.IsTrue(s[FighterSlot.Opponent].DamageTaken == 53);
            Assert.AreEqual(41, s.LastFinalBlow);
        }

        [Test]
        public void Charges_AverageAndPeak()
        {
            var s = new SlapStats();
            s.RecordCharge(FighterSlot.Player, new PowerResult(0.5f, 0f, 0.6f, 10, 0, 8f));
            s.RecordCharge(FighterSlot.Player, new PowerResult(0.9f, 0f, 1.0f, 18, 1, 12f));
            Assert.AreEqual(0.8f, s[FighterSlot.Player].AveragePower, 1e-5f);
            Assert.AreEqual(12f, s[FighterSlot.Player].PeakTps, 1e-5f);
        }

        [Test]
        public void Attached_ToARealDuel_TotalsAgreeWithTheDirector()
        {
            var clock = new ManualClock();
            var d = new MatchDirector(TestFactories.Bout(hp: 60), TestFactories.PowerCfg(0.4f), TestFactories.TimingCfg(),
                                      TestFactories.ZoneLayout(), TestFactories.DamageCfg(), clock, new SeededRandom(9));
            var player = new ManualTapSource();
            d.BindPlayerSource(player);
            d.BindOpponent(TestFactories.Ai(tps: 9f));
            var stats = new SlapStats();
            stats.Attach(d, 0.5f);

            bool ended = false;
            d.Signals.BoutEnd += _ => ended = true;
            d.StartBout();
            for (int i = 0; i < 200000 && !ended; i++)
            {
                clock.Advance(0.005);
                if (d.CurrentState == MatchState.Timing && d.CurrentTurn.Attacker == FighterSlot.Player)
                    player.Push(clock.Now, TapEvent.SourcePlayerTouch);
                d.Tick();
            }

            Assert.IsTrue(ended);
            var p = stats[FighterSlot.Player];
            var o = stats[FighterSlot.Opponent];
            Assert.AreEqual(d.TotalSlaps, p.Slaps + o.Slaps);
            Assert.AreEqual(60 - d.PlayerHealth.CurrentHp, p.DamageTaken);
            Assert.AreEqual(60 - d.OpponentHealth.CurrentHp, o.DamageTaken);
            Assert.AreEqual(p.Slaps + o.Slaps, p.Commits + o.Commits, "every slap had one timing result");
            stats.Detach();
        }
    }
}
