using NUnit.Framework;
using SlapMania.Core;
using SlapMania.Data;
using SlapMania.Gameplay;

namespace SlapMania.Tests.EditMode
{
    /// M01 pacing moved from private consts into BoutConfig (ms, R8). These pin that the FSM
    /// honours the asset values and that the 0 and negative boundaries cannot hang a bout (R3).
    public class MatchPacingTests
    {
        private const double Step = 0.001; // 1 ms sim steps so durations resolve to within a few ms

        private static MatchDirector NewDirector(BoutConfig bout, ManualClock clock, ManualTapSource player)
        {
            var d = new MatchDirector(bout, TestFactories.PowerCfg(0.2f), TestFactories.TimingCfg(),
                                      TestFactories.ZoneLayout(), TestFactories.DamageCfg(),
                                      clock, new SeededRandom(1));
            d.BindPlayerSource(player);
            d.BindOpponent(TestFactories.Ai(tps: 9f));
            return d;
        }

        /// Advances the sim, committing the player's timing tap as soon as the meter is armed.
        private static void Run(MatchDirector d, ManualClock clock, ManualTapSource player, System.Func<bool> until, int maxSteps)
        {
            for (int i = 0; i < maxSteps && !until(); i++)
            {
                clock.Advance(Step);
                if (d.CurrentState == MatchState.Timing && d.CurrentTurn.Attacker == FighterSlot.Player)
                    player.Push(clock.Now, TapEvent.SourcePlayerTouch);
                d.Tick();
            }
        }

        [Test]
        public void Windup_LastsBoutConfigWindupMs()
        {
            var clock = new ManualClock();
            var player = new ManualTapSource();
            var bout = TestFactories.Bout();
            bout.introMs = 0;
            bout.windupMs = 800;
            var d = NewDirector(bout, clock, player);

            double windupAt = -1, impactAt = -1;
            d.Signals.WindupStart += _ => { if (windupAt < 0) windupAt = clock.Now; };
            d.Signals.ImpactFrame += _ => { if (impactAt < 0) impactAt = clock.Now; };

            d.StartBout();
            Run(d, clock, player, () => impactAt >= 0, 200000);

            Assert.GreaterOrEqual(windupAt, 0, "windup never started");
            Assert.GreaterOrEqual(impactAt, 0, "impact never fired");
            // Windup state exits at >= 800 ms; the Impact state raises on the following tick.
            Assert.AreEqual(0.800, impactAt - windupAt, 0.003);
        }

        [Test]
        public void TurnTransition_LastsSnapshotPlusTurnTransitionMs()
        {
            var clock = new ManualClock();
            var player = new ManualTapSource();
            var bout = TestFactories.Bout(hp: 300);
            bout.introMs = 0;
            bout.snapshotMs = 0;
            bout.turnTransitionMs = 1200;
            var d = NewDirector(bout, clock, player);

            double snapshotAt = -1, turnEndAt = -1;
            d.Signals.SnapshotRequested += _ => { if (snapshotAt < 0) snapshotAt = clock.Now; };
            d.Signals.TurnEnd += _ => { if (turnEndAt < 0) turnEndAt = clock.Now; };

            d.StartBout();
            Run(d, clock, player, () => turnEndAt >= 0, 200000);

            Assert.GreaterOrEqual(snapshotAt, 0, "no snapshot - the first slap should not be lethal at 300 HP");
            Assert.GreaterOrEqual(turnEndAt, 0, "turn never ended");
            Assert.AreEqual(1.200, turnEndAt - snapshotAt, 0.004);
        }

        [Test]
        public void ZeroPacing_DuelStillTerminates()
        {
            var clock = new ManualClock();
            var player = new ManualTapSource();
            var bout = TestFactories.Bout(hp: 40);
            bout.introMs = bout.windupMs = bout.hitstopMs = bout.snapshotMs = 0;
            bout.finisherMs = bout.finisherSnapshotMs = bout.turnTransitionMs = 0;
            var d = NewDirector(bout, clock, player);

            bool ended = false;
            d.Signals.BoutEnd += _ => ended = true;
            d.StartBout();
            Run(d, clock, player, () => ended, 500000);

            Assert.IsTrue(ended, "a bout with every pacing value at 0 ms never terminated");
        }

        [Test]
        public void NegativePacing_BehavesAsZero_NoHang()
        {
            // [Min(0)] guards the Inspector; code writing a negative must still not wedge the FSM.
            var clock = new ManualClock();
            var player = new ManualTapSource();
            var bout = TestFactories.Bout(hp: 40);
            bout.introMs = bout.windupMs = bout.hitstopMs = bout.snapshotMs = -500;
            bout.finisherMs = bout.finisherSnapshotMs = bout.turnTransitionMs = -500;
            var d = NewDirector(bout, clock, player);

            bool ended = false;
            d.Signals.BoutEnd += _ => ended = true;
            d.StartBout();
            Run(d, clock, player, () => ended, 500000);

            Assert.IsTrue(ended, "negative pacing wedged the FSM");
        }

        [Test]
        public void DefaultPacing_MatchesThePreviousConstants()
        {
            // Moving the consts into BoutConfig was meant to change nothing by default.
            var b = UnityEngine.ScriptableObject.CreateInstance<BoutConfig>();
            Assert.AreEqual(500, b.introMs);
            Assert.AreEqual(500, b.windupMs);
            Assert.AreEqual(90, b.hitstopMs);
            Assert.AreEqual(50, b.snapshotMs);
            Assert.AreEqual(2500, b.finisherMs);
            Assert.AreEqual(100, b.finisherSnapshotMs);
            Assert.AreEqual(300, b.turnTransitionMs);
        }
    }
}
