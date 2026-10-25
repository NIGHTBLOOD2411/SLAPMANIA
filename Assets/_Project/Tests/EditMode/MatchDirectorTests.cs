using NUnit.Framework;
using SlapMania.Core;
using SlapMania.Gameplay;

namespace SlapMania.Tests.EditMode
{
    public class MatchDirectorTests
    {
        [Test]
        public void FullDuel_Terminates_WithSnapshotPerHit_AndFinisher()
        {
            var clock = new ManualClock();
            var rng = new SeededRandom(42);
            var d = new MatchDirector(TestFactories.Bout(hp: 60, maxTurns: 40),
                                      TestFactories.PowerCfg(0.6f),
                                      TestFactories.TimingCfg(),
                                      TestFactories.ZoneLayout(),
                                      TestFactories.DamageCfg(),
                                      clock, rng);

            // Player: a scripted tap source firing 10 tps during charge and one commit tap in timing.
            var playerSrc = new ManualTapSource();
            d.BindPlayerSource(playerSrc);
            d.BindOpponent(TestFactories.Ai(tps: 9f));

            int snapshots = 0;
            int finisherSnapshots = 0;
            bool boutEnded = false;
            MatchResult result = default;

            d.Signals.SnapshotRequested += _ => snapshots++;
            d.Signals.FinisherSnapshotRequested += _ => finisherSnapshots++;
            d.Signals.BoutEnd += r => { boutEnded = true; result = r; };

            // Wire the player: on ChargeStart, script a burst of taps; on TimingStart, one commit ~0.4s in.
            double chargeStartAt = -1, timingStartAt = -1;
            d.Signals.ChargeStart += _ => chargeStartAt = clock.Now;
            d.Signals.ChargeEnd   += _ => chargeStartAt = -1;
            d.Signals.TimingStart += _ => timingStartAt = clock.Now;
            d.Signals.TimingEnd   += _ => timingStartAt = -1;

            d.StartBout();

            double lastPlayerTap = -1;
            for (int i = 0; i < 20000; i++) // upper bound; will exit on BoutEnd
            {
                clock.Advance(0.010);

                // Player mash during charge (only when player is attacker).
                if (chargeStartAt >= 0 && d.CurrentTurn.Attacker == FighterSlot.Player
                    && clock.Now - lastPlayerTap >= 0.10)
                {
                    playerSrc.Push(clock.Now, TapEvent.SourcePlayerTouch);
                    lastPlayerTap = clock.Now;
                }
                // Player commit ~0.4s after timing starts.
                if (timingStartAt >= 0 && d.CurrentTurn.Attacker == FighterSlot.Player
                    && clock.Now - timingStartAt > 0.4)
                {
                    playerSrc.Push(clock.Now, TapEvent.SourcePlayerTouch);
                    timingStartAt = -1;
                }

                d.Tick();
                if (boutEnded) break;
            }

            Assert.IsTrue(boutEnded, "bout did not end within 200 seconds sim time");
            Assert.Greater(snapshots + finisherSnapshots, 0, "no snapshots requested");
            Assert.AreEqual(1, finisherSnapshots, "exactly one finisher snapshot expected");
            Assert.AreEqual(snapshots + finisherSnapshots, d.TotalSlaps, "snapshots must match slap count");
            Assert.IsFalse(result.ReachedMaxTurns, "should not hit sudden-death safety valve at HP 60");
        }

        [Test]
        public void MashingOnFromTheChargeIntoTheTimingPhase_DoesNotCommitIt([Values] bool pauseThenTap)
        {
            // S-093, the owner's report: a player mashing at 10 taps/s does not stop when the
            // charge ends. With the shipped cue (400 ms) and gap (220 ms), those taps never stop
            // the marker. A player who pauses for the gap and taps once commits normally.
            var clock = new ManualClock();
            var timing = TestFactories.TimingCfg();
            timing.armDelay = 0.15f;
            timing.aimCueMs = 400;
            timing.spamGapMs = 220;
            var d = new MatchDirector(TestFactories.Bout(hp: 400), TestFactories.PowerCfg(0.6f), timing,
                                      TestFactories.ZoneLayout(), TestFactories.DamageCfg(), clock, new SeededRandom(3));
            var src = new ManualTapSource();
            d.BindPlayerSource(src);
            d.BindOpponent(TestFactories.Ai());

            bool ended = false;
            TimingResult result = default;
            double timingAt = -1;
            d.Signals.TimingStart += t => { if (t.Attacker == FighterSlot.Player) timingAt = clock.Now; };
            d.Signals.TimingEnd += r => { if (d.CurrentTurn.Attacker == FighterSlot.Player) { result = r; ended = true; } };
            d.StartBout();

            double lastTap = -1;
            for (int i = 0; i < 2000 && !ended; i++)
            {
                clock.Advance(0.01);
                bool mashing = d.CurrentState == MatchState.Charge
                            || (timingAt >= 0 && (!pauseThenTap || clock.Now - timingAt < 0.7));
                if (mashing && clock.Now - lastTap >= 0.0999) { src.Push(clock.Now); lastTap = clock.Now; }
                if (pauseThenTap && timingAt >= 0 && clock.Now - timingAt >= 1.0 && clock.Now - lastTap >= 0.25)
                {
                    src.Push(clock.Now);
                    lastTap = clock.Now;
                }
                d.Tick();
            }
            Assert.IsTrue(ended, "the player's timing phase never ended");
            if (pauseThenTap) Assert.IsFalse(result.TimedOut, "the deliberate tap after the pause must commit");
            else Assert.IsTrue(result.TimedOut, "a mash tap committed the marker (S-093)");
        }

        [Test]
        public void Duel_TerminatesEvenWithNoPlayerTaps()
        {
            var clock = new ManualClock();
            var rng = new SeededRandom(7);
            var d = new MatchDirector(TestFactories.Bout(hp: 40, maxTurns: 40),
                                      TestFactories.PowerCfg(0.3f),
                                      TestFactories.TimingCfg(),
                                      TestFactories.ZoneLayout(),
                                      TestFactories.DamageCfg(),
                                      clock, rng);
            d.BindPlayerSource(new ManualTapSource()); // never pushes
            d.BindOpponent(TestFactories.Ai(tps: 9f));

            bool ended = false;
            d.Signals.BoutEnd += _ => ended = true;
            d.StartBout();

            for (int i = 0; i < 50000 && !ended; i++) { clock.Advance(0.010); d.Tick(); }
            Assert.IsTrue(ended, "duel with silent player never terminated");
        }
    }
}
