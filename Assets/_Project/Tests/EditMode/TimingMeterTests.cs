using System;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools.Constraints;
using SlapMania.Core;
using SlapMania.Data;
using SlapMania.Gameplay;
using Is = UnityEngine.TestTools.Constraints.Is;

namespace SlapMania.Tests.EditMode
{
    /// TimingMeter (M04, the TapMarker attack) and the AI's commit on it (M07). Arming is judged
    /// on the commit's own event time (DEBUG S-087), and the AI schedules its commit one display
    /// bias late so it is judged where it aimed (S-079). TestFactories values unless a test says
    /// so: arm delay 0.05 s, timeout 3 s, bias 0.03 s, comp clamp 0.12 s, round-1 sweep 0.85/s.
    public class TimingMeterTests
    {
        private const double Dt = 0.01;
        private const double Frame = 1.0 / 60.0;

        private sealed class Rig
        {
            public readonly ManualClock Clock = new ManualClock(10.0);
            public readonly ManualTapSource Src = new ManualTapSource();
            public readonly TimingMeter M = new TimingMeter();
            public readonly TimingPhaseConfig Cfg = TestFactories.TimingCfg();
            public readonly TimingZoneLayout Layout = TestFactories.ZoneLayout();
            public double Start;

            public Rig(int aimCueMs = 0, int spamGapMs = 0)
            {
                Cfg.aimCueMs = aimCueMs;
                Cfg.spamGapMs = spamGapMs;
            }

            /// `Start` is when the meter begins; the marker moves aimCueMs later.
            public void Begin(double lastTapAt = double.NegativeInfinity)
            {
                Start = Clock.Now;
                M.Begin(Cfg, Layout, Src, Clock, 1, new SeededRandom(7), 0f, 0f, lastTapAt);
            }
            public void Step(double dt = Dt) { Clock.Advance(dt); M.Tick(dt); }
            /// The earliest event time a commit may carry.
            public double ArmMoment => Start + Cfg.armDelay;
            public bool Resolved => M.CurrentState == TimingMeter.State.Resolved;
        }

        // ── Arming is judged on the commit's event time (S-087) ──────
        [Test]
        public void CommitDuringArmDelay_IsRejected_ThenAFreshCommitCounts()
        {
            var r = new Rig();
            r.Begin();
            r.Src.Push(r.Clock.Now);
            Assert.AreEqual(TimingMeter.State.Arming, r.M.CurrentState);
            r.Step(0.06);
            Assert.AreEqual(TimingMeter.State.Armed, r.M.CurrentState);
            r.Src.Push(r.Clock.Now);
            Assert.IsTrue(r.Resolved);
            Assert.IsFalse(r.M.Result.TimedOut);
        }

        [Test]
        public void CommitMadeAfterTheArmMoment_CountsEvenBeforeThisFramesTick()
        {
            // A long frame delivers the commit before Tick has moved Arming -> Armed.
            var r = new Rig();
            r.Begin();
            r.Clock.Advance(0.2);            // armDelay 0.05 has passed, but no Tick yet
            r.Src.Push(r.Clock.Now - 0.01);
            Assert.IsTrue(r.Resolved, "a commit made after the arm moment was dropped");
            // The compensation is unchanged: judged at 0.2 - (0.01 latency + 0.03 bias).
            float pos; int dir;
            MarkerOscillator.PositionAt(0.16, r.M.StartPhase, r.M.Sweep, out pos, out dir);
            Assert.AreEqual(pos, r.M.Result.Position, 1e-5);
            Assert.AreEqual(0.04f, r.M.Result.LatencyCompensated, 1e-6);
        }

        [Test]
        public void CommitMadeDuringArming_IsRejected_HoweverLateItIsDelivered([Values] bool armTickRanFirst)
        {
            var r = new Rig();
            r.Begin();
            if (armTickRanFirst) r.Step(0.1); else r.Clock.Advance(0.1);
            r.Src.Push(r.Start + 0.01);      // tapped 10 ms into a 50 ms arm delay
            Assert.IsFalse(r.Resolved, "a commit made during the arm delay counted");
            r.Step();
            r.Src.Push(r.Clock.Now);
            Assert.IsTrue(r.Resolved, "a fresh commit must still count");
        }

        [Test]
        public void Commit_AtExactlyTheArmMoment_Counts()
        {
            var r = new Rig();
            r.Begin();
            r.Clock.Advance(0.1);
            r.Src.Push(r.ArmMoment);
            Assert.IsTrue(r.Resolved);
        }

        [Test]
        public void Commit_OneMicrosecondBeforeTheArmMoment_IsRejected()
        {
            var r = new Rig();
            r.Begin();
            r.Clock.Advance(0.1);
            r.Src.Push(r.ArmMoment - 1e-6);
            Assert.IsFalse(r.Resolved);
        }

        [Test]
        public void CommitStampedNaN_CountsAsMadeNow()
        {
            var r = new Rig();
            r.Begin();
            r.Src.Push(double.NaN);
            Assert.IsFalse(r.Resolved, "now is inside the arm delay");
            r.Clock.Advance(0.1);
            r.Src.Push(double.NaN);
            Assert.IsTrue(r.Resolved, "now is past the arm moment");
            Assert.IsFalse(float.IsNaN(r.M.Result.Position));
        }

        [Test]
        public void CommitStampedInTheFuture_CannotSkipTheArmDelay()
        {
            var r = new Rig();
            r.Begin();
            r.Src.Push(r.Clock.Now + 1.0);
            Assert.IsFalse(r.Resolved);
        }

        [Test]
        public void PressMadeBeforeThePhase_NeverCommits_HoweverLateItIsDelivered([Values(0.1, 1.0, 1e6)] double deliveredAfter)
        {
            // S-096: a stamp 5 s old used to be clamped up to now - 0.12 s, which lands past the
            // arm moment once delivery is late enough. It is a leftover charge tap either way.
            var r = new Rig();
            r.Begin();
            r.Clock.Advance(deliveredAfter);
            r.Src.Push(r.Start - 5.0);
            Assert.IsFalse(r.Resolved, "a charge tap made before the timing phase committed it");
        }

        [Test]
        public void StaleCommit_MadeInsideThePhase_IsClampedToMaxLatencyComp()
        {
            // Made 0.3 s into the phase, delivered 1 s in: back-dated by 0.12 s, not 0.7 s.
            var r = new Rig();
            r.Begin();
            r.Clock.Advance(1.0);
            r.Src.Push(r.Start + 0.3);
            Assert.IsTrue(r.Resolved);
            Assert.AreEqual(0.12f, r.M.Result.LatencyCompensated, 1e-6);
        }

        // ── Mash carry-over: AIM cue and spam gap (S-093) ────────────
        // Shipped shape: cue 400 ms, gap 220 ms; arm delay 50 ms from TestFactories.
        private const double Cue = 0.4, Gap = 0.22;

        [Test]
        public void AimCue_HoldsTheMarkerStill_AndNoTapCommits()
        {
            var r = new Rig(400, 0);
            r.Begin();
            float atStart = r.M.NormalizedPosition;
            Assert.IsTrue(r.M.InCue);
            Assert.AreEqual(Cue, r.M.CueSeconds, 1e-9);
            for (int i = 0; i < 39; i++) { r.Step(); r.Src.Push(r.Clock.Now); }
            Assert.IsFalse(r.Resolved, "a tap during the AIM cue committed");
            Assert.AreEqual(atStart, r.M.NormalizedPosition, 1e-6, "the marker moved during the cue");
            Assert.AreEqual(TimingMeter.State.Arming, r.M.CurrentState);

            r.Step(0.02);                                  // 0.41 s: the marker runs, still arming
            Assert.IsFalse(r.M.InCue);
            Assert.AreNotEqual(atStart, r.M.NormalizedPosition);
            r.Step(0.05);                                  // past cue + arm delay
            r.Src.Push(r.Clock.Now);
            Assert.IsTrue(r.Resolved, "a clean tap after the cue and arm delay must count");
        }

        [Test]
        public void MashingStraightThroughThePhase_NeverCommits_AndTimesOut()
        {
            // 10 taps/s from the charge phase on, never pausing: every press is 100 ms after the last.
            var r = new Rig(400, 220);
            r.Begin(lastTapAt: r.Clock.Now - 0.1);
            for (int i = 0; i < 600 && !r.Resolved; i++)
            {
                r.Step();
                if (i % 10 == 9) r.Src.Push(r.Clock.Now);
            }
            Assert.IsTrue(r.Resolved);
            Assert.IsTrue(r.M.Result.TimedOut, "a mash tap stopped the marker");
        }

        [Test]
        public void MasherWhoPausesForTheGap_CommitsOnTheNextTap()
        {
            var r = new Rig(400, 220);
            r.Begin(lastTapAt: r.Clock.Now - 0.05);
            // Keep mashing through the cue and 200 ms past it, then stop.
            double lastMash = r.Clock.Now;
            for (int i = 0; i < 60; i++)
            {
                r.Step();
                if (i % 10 == 9) { r.Src.Push(r.Clock.Now); lastMash = r.Clock.Now; }
            }
            Assert.IsFalse(r.Resolved);
            r.Step(0.25);                                  // a 250 ms pause
            r.Src.Push(r.Clock.Now);
            Assert.IsTrue(r.Resolved, "the first deliberate tap after the pause must count");
            Assert.Greater(r.Clock.Now - lastMash, Gap);
        }

        [Test]
        public void SpamGap_OneMicrosecondPast_Counts_OneMicrosecondShort_IsRejected()
        {
            var ok = new Rig(0, 220);
            ok.Begin();
            ok.Clock.Advance(1.0);
            ok.Src.Push(ok.Start + 0.5);                   // early enough, armed: but now the gap runs
            Assert.IsTrue(ok.Resolved, "no press before it: the gap does not apply");

            var r = new Rig(0, 220);
            r.Begin(lastTapAt: r.Clock.Now);               // the last charge tap is at Begin
            r.Clock.Advance(1.0);
            r.Src.Push(r.Start + Gap - 1e-6);
            Assert.IsFalse(r.Resolved, "a press inside the gap counted");
            r.Src.Push(r.Start + Gap - 1e-6 + Gap + 1e-6); // just past one gap after the rejected one
            Assert.IsTrue(r.Resolved, "a press one gap after the last must count");
        }

        [Test]
        public void SpamGap_NaNOrHugeLastTap_IsSafe([Values(double.NaN, double.NegativeInfinity, double.PositiveInfinity)] double lastTapAt)
        {
            var r = new Rig(0, 220);
            r.Begin(lastTapAt);
            r.Step(1.0);
            r.Src.Push(r.Clock.Now);
            // +inf means "a press is always too close": no commit, but the turn still ends.
            if (double.IsPositiveInfinity(lastTapAt)) Assert.IsFalse(r.Resolved);
            else Assert.IsTrue(r.Resolved);
            r.Step(10.0);
            Assert.IsTrue(r.Resolved, "the timeout must always end the turn");
            Assert.IsFalse(float.IsNaN(r.M.Result.Position));
        }

        [Test]
        public void EarliestCommitElapsed_IsTheArmDelay_OrTheGapAfterTheLastTap()
        {
            var r = new Rig(400, 220);
            r.Begin();
            Assert.AreEqual(r.Cfg.armDelay, r.M.EarliestCommitElapsed, 1e-9, "no last tap: the arm delay");
            r.Begin(lastTapAt: r.Clock.Now);               // gap ends 220 ms after Begin, inside the cue
            Assert.AreEqual(r.Cfg.armDelay, r.M.EarliestCommitElapsed, 1e-9);
            var c = new Rig(0, 220);
            c.Begin(lastTapAt: c.Clock.Now);
            Assert.AreEqual(Gap, c.M.EarliestCommitElapsed, 1e-6, "no cue: the gap outlasts the arm delay");
        }

        // ── Bad dt ───────────────────────────────────────────────────
        [Test]
        public void NaNOrNegativeDt_ChangesNothing()
        {
            var r = new Rig();
            r.Begin();
            r.Clock.Advance(0.06);
            r.M.Tick(double.NaN);
            r.M.Tick(-1.0);
            Assert.AreEqual(TimingMeter.State.Arming, r.M.CurrentState);
            r.M.Tick(0.06);
            Assert.AreEqual(TimingMeter.State.Armed, r.M.CurrentState);
        }

        [Test]
        public void HugeDt_WithoutACommit_TimesOutInPlace()
        {
            var r = new Rig();
            r.Begin();
            r.Step(1e6);
            Assert.IsTrue(r.Resolved);
            Assert.IsTrue(r.M.Result.TimedOut);
        }

        [Test]
        public void HugeDt_ACommitMadeInThatFrame_IsClampedNotDropped()
        {
            var r = new Rig();
            r.Begin();
            r.Clock.Advance(1e6);
            r.Src.Push(r.Start + 1.0);       // made in time; delivered after a huge hitch
            Assert.IsTrue(r.Resolved);
            Assert.IsFalse(r.M.Result.TimedOut);
            Assert.AreEqual(0.12f, r.M.Result.LatencyCompensated, 1e-6);
        }

        // ── R5 ───────────────────────────────────────────────────────
        [Test]
        public void CommitAndTimeout_AllocateNothing()
        {
            // S-089: TryCommit and ForceResolve unsubscribed with `-= HandleTap`, a method group,
            // which allocates a fresh delegate on the commit frame.
            Assert.That(() => { GC.KeepAlive(new object[4]); }, Is.AllocatingGCMemory(), "the allocation meter is blind");

            // Warm up both paths once (JIT) before measuring.
            var warm = new Rig();
            warm.Begin(); warm.Step(0.06); warm.Src.Push(warm.Clock.Now);
            warm.Begin(); warm.Step(5.0);

            var c = new Rig();
            c.Begin();
            TestDelegate commit = () =>
            {
                for (int i = 0; i < 10; i++) c.Step(); // through the arm delay
                c.Src.Push(c.Clock.Now);
            };
            Assert.That(commit, Is.Not.AllocatingGCMemory(), "the commit frame allocated (R5)");
            Assert.IsTrue(c.Resolved);
            Assert.IsFalse(c.M.Result.TimedOut);

            var t = new Rig();
            t.Begin();
            TestDelegate timeout = () => { for (int i = 0; i < 400 && !t.Resolved; i++) t.Step(); };
            Assert.That(timeout, Is.Not.AllocatingGCMemory(), "the timeout frame allocated (R5)");
            Assert.IsTrue(t.M.Result.TimedOut);
        }

        // ── The AI's commit (M07) ────────────────────────────────────
        private sealed class AiRig
        {
            public readonly ManualClock Clock = new ManualClock(5.0);
            public readonly TimingMeter M = new TimingMeter();
            public readonly TimingPhaseConfig Cfg;
            public readonly TimingZoneLayout Layout;
            public readonly AiProfile Profile;
            public readonly AiTapSource Ai;

            public AiRig(int seed, AiProfile profile, TimingPhaseConfig cfg = null, TimingZoneLayout layout = null)
            {
                Profile = profile;
                Cfg = cfg ?? TestFactories.TimingCfg();
                Layout = layout ?? TestFactories.ZoneLayout();
                Ai = new AiTapSource(Clock, new SeededRandom(seed));
                Ai.Configure(profile);
            }

            /// What MatchDirector.EnterTiming does on the AI's turn.
            public void Begin(int round, int seed, double lastTapAt = double.NegativeInfinity)
            {
                M.Begin(Cfg, Layout, Ai, Clock, round, new SeededRandom(1000 + seed), 0f, 0f, lastTapAt);
                Ai.Enable();
                Ai.BeginTiming(M.StartPhase, M.Sweep, Layout, M.ZoneScale, Cfg.timeout, Cfg.displayLatencyBias,
                               M.CueSeconds, M.EarliestCommitElapsed);
            }

            /// One frame in MatchDirector.Tick's order: the AI ticks before the meter.
            public void Step(double dt) { Clock.Advance(dt); Ai.Tick(dt); M.Tick(dt); }

            public void PlayAt60Fps()
            {
                for (int i = 0; i < 600 && M.CurrentState != TimingMeter.State.Resolved; i++) Step(Frame);
            }
        }

        [Test]
        public void Ai_HitchSpanningArmAndCommit_StillCommits([NUnit.Framework.Range(1, 6)] int seed)
        {
            // S-087 on the marker: one frame covers the arm delay and the AI's commit. The AI ticks
            // before the meter, so it commits while the state still reads Arming.
            var r = new AiRig(seed, TestFactories.Ai(aim: TimingZone.Perfect, aimSigma: 0f)); // aims at 0.5
            r.Begin(1, seed);
            double when;
            Assert.IsTrue(MarkerOscillator.NextTimeAtPosition(r.M.StartPhase, r.M.Sweep, 0.5f,
                r.Profile.reactionDelay, r.Cfg.timeout, out when));
            double hitch = when + r.Cfg.displayLatencyBias + 0.01; // ends 10 ms after the commit
            r.Step(hitch);
            Assert.AreEqual(TimingMeter.State.Resolved, r.M.CurrentState, "the AI's commit was dropped");
            Assert.IsFalse(r.M.Result.TimedOut);
            Assert.AreEqual(TimingZone.Perfect, r.M.Result.Zone, "pos " + r.M.Result.Position);
        }

        [Test]
        public void Ai_AimingPerfect_LandsPerfect_OnTheFastestRound([NUnit.Framework.Range(1, 12)] int seed)
        {
            // Round 10: sweep 1.45/s and zone scale 0.70, so 30 ms of marker travel (0.044) is
            // twice the gold band's half-width (0.021). Judged a bias early, it lands Great (S-079).
            var r = new AiRig(seed, TestFactories.Ai(aim: TimingZone.Perfect, aimSigma: 0.005f));
            r.Begin(10, seed);
            r.PlayAt60Fps();
            Assert.AreEqual(TimingMeter.State.Resolved, r.M.CurrentState);
            Assert.IsFalse(r.M.Result.TimedOut);
            Assert.AreEqual(TimingZone.Perfect, r.M.Result.Zone,
                "pos " + r.M.Result.Position + ": the AI's commit must not be judged a display bias early (S-079)");
        }

        [Test]
        public void Ai_WithTheShippedArmDelayAndReaction_NeverTimesOut()
        {
            // S-088: TimingPhase.asset armDelay 0.15 s, Ai_Round1.asset reactionDelay 0.12 s (read
            // from the YAML, 2026-10-23). The AI may aim at a crossing up to 30 ms inside the arm
            // delay. Scheduled a bias late (S-079) and judged on its event time (S-087), that
            // commit lands on the arm moment or after it. First pass only, so every aim lands
            // before the timeout.
            var cfg = TestFactories.TimingCfg();
            cfg.armDelay = 0.15f;
            var layout = TestFactories.ZoneLayout();
            var profile = TestFactories.Ai(aim: TimingZone.Good, aimSigma: 0.08f);
            profile.reactionDelay = 0.12f;
            int timedOut = 0;
            string firstSeed = "";
            for (int seed = 1; seed <= 200; seed++)
            {
                var r = new AiRig(seed, profile, cfg, layout);
                r.Begin(1, seed);
                r.PlayAt60Fps();
                if (!r.M.Result.TimedOut) continue;
                if (timedOut == 0) firstSeed = " (first: seed " + seed + ")";
                timedOut++;
            }
            Assert.AreEqual(0, timedOut, timedOut + " of 200 AI turns timed out" + firstSeed);
        }

        [Test]
        public void Ai_ThroughTheCueAndGap_WithAReactionShorterThanTheArmDelay_NeverTimesOut()
        {
            // S-088's tuning trap and S-093 together: M07's round-10 reactionDelay 0.04 s is well
            // under the shipped 0.15 s arm delay, and the AI's last charge tap is 10 ms before the
            // phase. The AI takes its earliest commit from the meter, so nothing is rejected.
            var cfg = TestFactories.TimingCfg();
            cfg.armDelay = 0.15f;
            cfg.aimCueMs = 400;
            cfg.spamGapMs = 220;
            var layout = TestFactories.ZoneLayout();
            var profile = TestFactories.Ai(aim: TimingZone.Great, aimSigma: 0.05f);
            profile.reactionDelay = 0.04f;
            int timedOut = 0, early = 0;
            for (int seed = 1; seed <= 200; seed++)
            {
                var r = new AiRig(seed, profile, cfg, layout);
                double begin = r.Clock.Now;
                r.Begin(1, seed, lastTapAt: begin - 0.01);
                r.PlayAt60Fps();
                if (r.M.Result.TimedOut) timedOut++;
                if (r.Clock.Now < begin + 0.4 + 0.15) early++;
            }
            Assert.AreEqual(0, timedOut, timedOut + " of 200 AI turns timed out");
            Assert.AreEqual(0, early, "the AI committed inside the AIM cue or the arm delay");
        }

        [Test]
        public void Ai_WaitingForTheNextPass_NeverWaitsPastTheTimeout()
        {
            // S-097: the ROOKIE tier (reaction 0.30 s, first-pass odds 0.4, aiming Weak) used to
            // add a traversal after a late first crossing and land past the 3 s timeout.
            var cfg = TestFactories.TimingCfg();
            cfg.armDelay = 0.15f;
            cfg.aimCueMs = 400;
            cfg.spamGapMs = 220;
            var layout = TestFactories.ZoneLayout();
            var profile = TestFactories.Ai(aim: TimingZone.Weak, aimSigma: 0.10f);
            profile.reactionDelay = 0.30f;
            profile.commitOnFirstPass = 0f;              // always try to wait
            int timedOut = 0;
            for (int seed = 1; seed <= 300; seed++)
            {
                var r = new AiRig(seed, profile, cfg, layout);
                r.Begin(1, seed);
                r.PlayAt60Fps();
                if (r.M.Result.TimedOut) timedOut++;
            }
            Assert.AreEqual(0, timedOut, timedOut + " of 300 AI turns waited past the timeout");
        }

        // ── Through the FSM (M01 hands the AI the bias) ──────────────
        [Test]
        public void Director_GivesTheAiTheBias_SoItIsJudgedWhereItAimed()
        {
            // An AI with no aim noise, aiming Good, must be judged on a Good band's centre (0.36
            // or 0.64, scaled by the round's zone scale), not 30 ms of travel (~0.026) off it.
            var clock = new ManualClock();
            var d = new MatchDirector(TestFactories.Bout(hp: 400), TestFactories.PowerCfg(0.6f),
                                      TestFactories.TimingCfg(), TestFactories.ZoneLayout(),
                                      TestFactories.DamageCfg(), clock, new SeededRandom(42));
            d.BindPlayerSource(new ManualTapSource()); // the player never taps: timeouts
            d.BindOpponent(TestFactories.Ai(aim: TimingZone.Good, aimSigma: 0f));

            int aiTurns = 0, aiTimeouts = 0;
            float worst = 0f;
            d.Signals.TimingEnd += t =>
            {
                if (d.CurrentTurn.Attacker != FighterSlot.Opponent) return;
                aiTurns++;
                if (t.TimedOut) aiTimeouts++;
                float s = d.TimingMeter.ZoneScale;
                float off = Mathf.Min(Mathf.Abs(t.Position - TimingZoneLayout.ScaleAboutCentre(0.36f, s)),
                                      Mathf.Abs(t.Position - TimingZoneLayout.ScaleAboutCentre(0.64f, s)));
                if (off > worst) worst = off;
            };

            d.StartBout();
            for (int i = 0; i < 20000 && aiTurns < 3; i++) { clock.Advance(Frame); d.Tick(); }
            Assert.AreEqual(3, aiTurns, "the bout ended before three AI turns");
            Assert.AreEqual(0, aiTimeouts);
            Assert.Less(worst, 1e-3f, "the AI was judged " + worst + " off its aim");
        }
    }
}
