using System;
using System.Collections.Generic;
using NUnit.Framework;
using SlapMania.Core;
using SlapMania.Gameplay;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;

namespace SlapMania.Tests.EditMode
{
    /// M01 pause and forfeit. Time steps are 1/128 s so every sum is exact in double and a
    /// paused run can be compared with an unpaused one bit for bit.
    public class MatchPauseTests
    {
        private const double Step = 1.0 / 128.0;

        private sealed class Rig
        {
            public readonly ManualClock Source = new ManualClock();
            public readonly ManualTapSource Player = new ManualTapSource();
            public readonly MatchDirector D;
            public bool Ended;
            public MatchResult Result;

            public Rig(int hp = 100, float chargeSeconds = 2.5f, int seed = 42)
            {
                D = new MatchDirector(TestFactories.Bout(hp: hp), TestFactories.PowerCfg(chargeSeconds),
                                      TestFactories.TimingCfg(), TestFactories.ZoneLayout(),
                                      TestFactories.DamageCfg(), Source, new SeededRandom(seed));
                D.BindPlayerSource(Player);
                D.BindOpponent(TestFactories.Ai(tps: 9f));
                D.Signals.BoutEnd += r => { Ended = true; Result = r; };
            }

            public void Step1() { Source.Advance(Step); D.Tick(); }

            public void Run(double seconds)
            {
                int n = (int)Math.Round(seconds / Step);
                for (int i = 0; i < n; i++) Step1();
            }

            public void RunUntil(Func<bool> done, double maxSeconds = 120.0)
            {
                int n = (int)(maxSeconds / Step);
                for (int i = 0; i < n && !done(); i++) Step1();
                Assert.IsTrue(done(), "condition not reached in " + maxSeconds + " s");
            }

            public void ToPlayerCharge()
            {
                D.StartBout();
                RunUntil(() => D.CurrentState == MatchState.Charge && D.CurrentTurn.Attacker == FighterSlot.Player);
            }

            public void Tap() => Player.Push(Source.Now, TapEvent.SourcePlayerTouch);
        }

        // ── Pause ────────────────────────────────────────────────────
        [Test]
        public void PauseMidCharge_ChargeEndsAfterExactlyTheRemainingTime()
        {
            var r = new Rig(chargeSeconds: 0.6f);
            double chargeEndAt = -1;
            r.D.Signals.ChargeEnd += _ => chargeEndAt = r.D.Now;
            r.ToPlayerCharge();
            double chargeStart = r.D.Now;

            r.Run(0.25);
            Assert.IsTrue(r.D.RequestPause());
            r.Run(10.0);                                    // ten seconds on the pause menu
            Assert.AreEqual(MatchState.Charge, r.D.CurrentState, "the FSM moved while paused");
            Assert.AreEqual(chargeStart + 0.25, r.D.Now, 1e-12, "simulation time passed while paused");
            Assert.Less(chargeEndAt, 0, "charge ended while paused");

            Assert.IsTrue(r.D.RequestResume());
            r.RunUntil(() => chargeEndAt >= 0, 5.0);
            Assert.AreEqual(0.6, chargeEndAt - chargeStart, Step + 1e-9, "charge length changed by the pause");
        }

        [Test]
        public void PausedTaps_AreDropped_LiveTapsCount()
        {
            var r = new Rig();
            r.ToPlayerCharge();
            r.Tap();
            Assert.AreEqual(1, r.D.PowerMeter.TapsAccepted);

            r.D.RequestPause();
            for (int i = 0; i < 20; i++) { r.Source.Advance(0.1); r.Tap(); r.D.Tick(); }
            Assert.AreEqual(1, r.D.PowerMeter.TapsAccepted, "taps on the pause menu charged the slap");

            r.D.RequestResume();
            r.Player.Push(r.Source.Now - 0.5, TapEvent.SourcePlayerTouch); // made during the pause, delivered late
            Assert.AreEqual(1, r.D.PowerMeter.TapsAccepted, "a late-delivered paused tap counted");
            r.Source.Advance(0.1);
            r.Tap();
            Assert.AreEqual(2, r.D.PowerMeter.TapsAccepted);
        }

        [Test]
        public void PauseMidTiming_MarkerHoldsStill_AndCommitLatencyIsMeasuredOnSimTime()
        {
            var r = new Rig(chargeSeconds: 0.3f);
            r.ToPlayerCharge();
            r.RunUntil(() => r.D.CurrentState == MatchState.Timing);
            r.Run(0.4);
            float before = r.D.TimingMeter.NormalizedPosition;

            r.D.RequestPause();
            r.Run(5.0);
            Assert.AreEqual(before, r.D.TimingMeter.NormalizedPosition, "marker moved while paused");
            Assert.AreEqual(TimingMeter.State.Armed, r.D.TimingMeter.CurrentState, "timed out while paused");
            r.D.RequestResume();
            Assert.AreEqual(before, r.D.TimingMeter.NormalizedPosition, "marker jumped on resume");

            r.Tap(); // stamped "now" on the source clock
            Assert.AreEqual(TimingMeter.State.Resolved, r.D.TimingMeter.CurrentState);
            Assert.AreEqual(0.0, r.D.TimingMeter.Result.RawLatency, 1e-6,
                "tap time not mapped onto the paused timeline (latency would read minus the pause)");
            Assert.IsFalse(r.D.TimingMeter.Result.TimedOut);
        }

        [Test]
        public void PauseInAnInstantState_TickDoesNotAdvanceIt()
        {
            var r = new Rig();
            int turnStarts = 0;
            r.D.Signals.TurnStart += _ => turnStarts++;
            r.D.StartBout();
            r.RunUntil(() => r.D.CurrentState == MatchState.TurnStart);
            r.D.RequestPause();
            r.Run(1.0);
            Assert.AreEqual(MatchState.TurnStart, r.D.CurrentState);
            Assert.AreEqual(0, turnStarts, "TurnStart raised while paused");
            r.D.RequestResume();
            r.Step1();
            Assert.AreEqual(1, turnStarts);
            Assert.AreEqual(MatchState.Charge, r.D.CurrentState);
        }

        [Test]
        public void RequestPause_IsRefusedOutsideALiveBout_AndWhenAlreadyPaused()
        {
            var r = new Rig(hp: 1);
            Assert.IsFalse(r.D.RequestPause(), "paused before StartBout");
            Assert.IsFalse(r.D.RequestResume(), "resumed while not paused");
            r.D.StartBout();
            Assert.IsTrue(r.D.RequestPause(), "BoutIntro must be pausable");
            Assert.IsFalse(r.D.RequestPause(), "double pause accepted");
            Assert.IsTrue(r.D.IsPaused);
            Assert.IsTrue(r.D.RequestResume());
            Assert.IsFalse(r.D.IsPaused);
            r.RunUntil(() => r.Ended);
            Assert.IsFalse(r.D.RequestPause(), "paused after BoutEnd");
        }

        public enum PausePattern { Periodic, AfterEveryTransition }

        /// The headline guarantee: pausing anywhere, for any length, changes nothing about the
        /// bout. A duel with pauses scattered through it (and taps hammered on the pause menu)
        /// must land the same slaps for the same damage as the same duel without them.
        [TestCase(PausePattern.Periodic)]
        [TestCase(PausePattern.AfterEveryTransition)]
        public void PausingIsInvisibleToTheSimulation(PausePattern pattern)
        {
            var reference = RunScriptedDuel(null, out var refResult, out _);
            var paused = RunScriptedDuel(pattern, out var pausedResult, out var pausedIn);

            CollectionAssert.AreEqual(reference, paused, "slap damage differs once pauses are added");
            Assert.AreEqual(refResult.Winner, pausedResult.Winner);
            Assert.AreEqual(refResult.TotalSlaps, pausedResult.TotalSlaps);
            PausedIn(pausedIn, MatchState.Charge);
            PausedIn(pausedIn, MatchState.Timing);
            PausedIn(pausedIn, MatchState.Windup);
            if (pattern == PausePattern.AfterEveryTransition)
            {
                PausedIn(pausedIn, MatchState.TurnStart);
                PausedIn(pausedIn, MatchState.Resolve);
                PausedIn(pausedIn, MatchState.Impact);
                PausedIn(pausedIn, MatchState.Hitstop);
            }
        }

        private static void PausedIn(HashSet<MatchState> states, MatchState s) =>
            Assert.IsTrue(states.Contains(s), "the script never paused in " + s + "; coverage is weaker than it claims");

        private static List<int> RunScriptedDuel(PausePattern? pattern, out MatchResult result, out HashSet<MatchState> pausedIn)
        {
            var r = new Rig(hp: 60, chargeSeconds: 0.6f);
            var damages = new List<int>();
            pausedIn = new HashSet<MatchState>();
            r.D.Signals.ImpactFrame += d => damages.Add(d.FinalDamage);
            double chargeAt = -1, timingAt = -1;
            r.D.Signals.ChargeStart += _ => chargeAt = r.D.Now;
            r.D.Signals.ChargeEnd += _ => chargeAt = -1;
            r.D.Signals.TimingStart += _ => timingAt = r.D.Now;
            r.D.Signals.TimingEnd += _ => timingAt = -1;
            r.D.StartBout();

            double lastTap = double.NegativeInfinity;
            int live = 0, pauseLeft = 0;
            bool resumedLastStep = false;
            for (int i = 0; i < 400000 && !r.Ended; i++)
            {
                r.Source.Advance(Step);
                if (r.D.IsPaused)
                {
                    r.Tap();     // mashing the pause menu: must be dropped
                    r.D.Tick();  // must do nothing
                    if (--pauseLeft == 0) { r.D.RequestResume(); resumedLastStep = true; }
                    continue;
                }
                if (resumedLastStep)
                {
                    // An event made during the pause but delivered after it: must be dropped.
                    r.Player.Push(r.Source.Now - 2 * Step, TapEvent.SourcePlayerTouch);
                    resumedLastStep = false;
                }

                bool playerTurn = r.D.CurrentTurn.Attacker == FighterSlot.Player;
                if (chargeAt >= 0 && playerTurn && r.D.Now - lastTap >= 0.1) { r.Tap(); lastTap = r.D.Now; }
                if (timingAt >= 0 && playerTurn && r.D.Now - timingAt > 0.4) { r.Tap(); timingAt = -1; }

                var stateBefore = r.D.CurrentState;
                r.D.Tick();
                live++;

                bool pauseNow = pattern == PausePattern.Periodic ? live % 37 == 0
                              : pattern == PausePattern.AfterEveryTransition && r.D.CurrentState != stateBefore;
                if (pauseNow && r.D.RequestPause())
                {
                    pausedIn.Add(r.D.CurrentState);
                    pauseLeft = pattern == PausePattern.Periodic ? 64 : 3;
                }
            }
            Assert.IsTrue(r.Ended, "duel did not end");
            result = r.Result;
            return damages;
        }

        // ── Forfeit ──────────────────────────────────────────────────
        [Test]
        public void ForfeitMidCharge_EndsTheBoutForTheOtherFighter_AndClosesTheMeter()
        {
            var r = new Rig();
            r.ToPlayerCharge();
            r.Tap();
            int accepted = r.D.PowerMeter.TapsAccepted;

            Assert.IsTrue(r.D.Forfeit(FighterSlot.Player));
            Assert.IsTrue(r.Ended);
            Assert.AreEqual(FighterSlot.Opponent, r.Result.Winner);
            Assert.IsTrue(r.Result.Forfeited);
            Assert.IsFalse(r.Result.ReachedMaxTurns);
            Assert.AreEqual(MatchState.BoutEnd, r.D.CurrentState);

            r.Source.Advance(0.1);
            r.Tap();
            r.Run(3.0);
            Assert.AreEqual(accepted, r.D.PowerMeter.TapsAccepted, "a tap landed after the forfeit");
            Assert.AreEqual(MatchState.BoutEnd, r.D.CurrentState);
        }

        [Test]
        public void ForfeitWhilePaused_EndsTheBout_AndLeavesNothingPaused()
        {
            var r = new Rig(chargeSeconds: 0.3f);
            r.ToPlayerCharge();
            r.RunUntil(() => r.D.CurrentState == MatchState.Timing);
            r.D.RequestPause();
            r.Run(2.0);
            Assert.IsTrue(r.D.Forfeit(FighterSlot.Player));
            Assert.IsFalse(r.D.IsPaused);
            Assert.AreEqual(TimingMeter.State.Resolved, r.D.TimingMeter.CurrentState, "timing meter left open");
            Assert.AreEqual(FighterSlot.Opponent, r.Result.Winner);
        }

        [Test]
        public void Forfeit_IsRefusedBeforeStart_OnceTheLethalSlapLanded_AndAfterTheEnd()
        {
            var r = new Rig(hp: 1, chargeSeconds: 0.3f);
            Assert.IsFalse(r.D.Forfeit(FighterSlot.Player), "forfeit before StartBout");
            bool landed = false;
            r.D.Signals.ImpactFrame += d => landed = d.IsLethal;
            r.D.StartBout();
            r.RunUntil(() => landed);
            Assert.IsTrue(r.D.IsOutcomeDecided);
            Assert.IsFalse(r.D.Forfeit(FighterSlot.Player), "forfeit flipped a decided bout");
            Assert.IsFalse(r.Ended, "the finisher should still be playing");
            r.RunUntil(() => r.Ended);
            Assert.IsFalse(r.Result.Forfeited);
            Assert.IsFalse(r.D.Forfeit(FighterSlot.Opponent), "forfeit after BoutEnd");
        }

        // ── R5 ───────────────────────────────────────────────────────
        // GC.GetAllocatedBytesForCurrentThread reads 0 in the Editor's Mono; the Test
        // Framework's constraint (a GC.Alloc profiler recorder on this thread) is the meter.
        [Test]
        public void PauseResumeAndTaps_InsideChargeAndTiming_AllocateNothing()
        {
            Assert.That(() => { GC.KeepAlive(new object[4]); }, Is.AllocatingGCMemory(), "the allocation meter is blind");

            var r = new Rig(chargeSeconds: 2.5f);
            r.ToPlayerCharge();
            // Warm up every path once (JIT) before measuring.
            r.Tap(); r.D.RequestPause(); r.Tap(); r.D.Tick(); r.D.RequestResume(); r.Step1();

            TestDelegate charge = () =>
            {
                for (int i = 0; i < 128; i++)      // one second of charge, paused in the middle
                {
                    if (i == 40) r.D.RequestPause();
                    if (i == 80) r.D.RequestResume();
                    if (i % 6 == 0) r.Tap();
                    r.Step1();
                }
            };
            Assert.That(charge, Is.Not.AllocatingGCMemory(), "Charge allocated with a pause in it (R5)");
            Assert.AreEqual(MatchState.Charge, r.D.CurrentState, "window left the charge phase");

            r.RunUntil(() => r.D.CurrentState == MatchState.Timing);
            r.Step1();
            float sink = 0f;
            TestDelegate timing = () =>
            {
                for (int i = 0; i < 128; i++)      // one second of aiming, paused in the middle
                {
                    if (i == 30) r.D.RequestPause();
                    if (i == 90) r.D.RequestResume();
                    r.Step1();
                    sink += r.D.TimingMeter.NormalizedPosition;
                }
            };
            Assert.That(timing, Is.Not.AllocatingGCMemory(), "Timing allocated with a pause in it (R5)");
            Assert.AreEqual(MatchState.Timing, r.D.CurrentState, "window left the timing phase");
            Assert.IsFalse(float.IsNaN(sink));
        }
    }
}
