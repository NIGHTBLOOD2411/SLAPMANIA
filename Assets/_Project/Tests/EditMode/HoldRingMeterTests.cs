using System;
using System.Text;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.TestTools.Constraints;
using SlapMania.Core;
using SlapMania.Data;
using SlapMania.Gameplay;
using Is = UnityEngine.TestTools.Constraints.Is;

namespace SlapMania.Tests.EditMode
{
    /// HoldRingMeter (M04 §5b) and the HoldRing attack mode through MatchDirector (M01) and the
    /// AI (M07). The ring closes in exactly 1 s here (flat speed curve) unless a test says so.
    public class HoldRingMeterTests
    {
        private const double Dt = 0.01;

        private sealed class Rig
        {
            public readonly ManualClock Clock = new ManualClock(10.0);
            public readonly ManualTapSource Src = new ManualTapSource();
            public readonly HoldRingMeter M = new HoldRingMeter();
            public readonly TimingPhaseConfig Timing = TestFactories.TimingCfg();
            public readonly PowerPhaseConfig Power = TestFactories.PowerCfg();
            public readonly TimingZoneLayout Layout = TestFactories.ZoneLayout();
            public readonly HoldRingConfig Ring = TestFactories.HoldRingCfg();
            public double PressedAt;

            public Rig(bool flatSpeed = true)
            {
                if (!flatSpeed) return;
                Timing.speedByRound = AnimationCurve.Constant(0f, 20f, 1f);
                Timing.zoneScaleByRound = AnimationCurve.Constant(0f, 20f, 1f);
            }

            public void Begin(int round = 1) => M.Begin(Ring, Timing, Power, Layout, Src, Clock, round);
            public void Step(double dt = Dt) { Clock.Advance(dt); M.Tick(dt); }

            /// Begin, wait out the arm delay, press now.
            public void Press()
            {
                Begin();
                Step(0.06);
                Assert.AreEqual(HoldRingMeter.State.Ready, M.CurrentState);
                PressedAt = Clock.Now;
                Src.Push(PressedAt);
                Assert.AreEqual(HoldRingMeter.State.Holding, M.CurrentState);
            }

            /// Hold until `seconds` after the press, then release with an event `latency` old.
            public void HoldThenRelease(double seconds, double latency = 0.0)
            {
                Clock.Set(PressedAt + seconds);
                M.Tick(Dt);
                Src.PushRelease(Clock.Now - latency);
            }
        }

        // ── Resolution ───────────────────────────────────────────────
        [Test]
        public void ReleaseOnGoldCentre_IsPerfect_AtFullPower()
        {
            var r = new Rig();
            r.Press();
            r.HoldThenRelease(0.53); // what the player saw: 0.53 - displayLatencyBias 0.03 = 0.50
            Assert.AreEqual(HoldRingMeter.State.Resolved, r.M.CurrentState);
            var t = r.M.TimingResult;
            Assert.AreEqual(TimingZone.Perfect, t.Zone);
            Assert.AreEqual(3f, t.Multiplier, 1e-6);
            Assert.AreEqual(0.5f, t.Position, 1e-5);
            Assert.AreEqual(0.03f, t.LatencyCompensated, 1e-6);
            Assert.AreEqual(+1, t.Direction);
            Assert.IsFalse(t.TimedOut);
            var p = r.M.PowerResult;
            Assert.AreEqual(1f, p.Fill, 1e-6, "the wind-up peaks on the gold band");
            Assert.AreEqual(r.Power.maxPower, p.RawPower, 1e-6);
            Assert.AreEqual(0f, p.Overcharge);
            Assert.AreEqual(1, p.TapsAccepted);
        }

        [Test]
        public void Release_IsBackDatedByLatencyAndBias()
        {
            var r = new Rig();
            r.Press();
            r.HoldThenRelease(0.62, latency: 0.04); // comp = 0.04 + 0.03 -> held 0.55
            var t = r.M.TimingResult;
            Assert.AreEqual(0.55f, t.Position, 1e-5);
            Assert.AreEqual(0.07f, t.LatencyCompensated, 1e-6);
            Assert.AreEqual(0.04f, t.RawLatency, 1e-6);
            Assert.AreEqual(TimingZone.Great, t.Zone);
        }

        [Test]
        public void HitchedRelease_IsClampedToMaxLatencyComp()
        {
            var r = new Rig();
            r.Press();
            r.HoldThenRelease(0.90, latency: 0.5);
            Assert.AreEqual(0.12f, r.M.TimingResult.LatencyCompensated, 1e-6);
            Assert.AreEqual(0.78f, r.M.TimingResult.Position, 1e-5);
        }

        [Test]
        public void EarlyRelease_IsWeakEarly_AndPartlyWound()
        {
            var r = new Rig();
            r.Press();
            r.HoldThenRelease(0.28); // closure 0.25
            var t = r.M.TimingResult;
            Assert.AreEqual(TimingZone.Weak, t.Zone);
            Assert.AreEqual(0.25f, t.Position, 1e-5);
            // Linear wind-up (exponent 1): half way to the peak -> half way from min to max power.
            Assert.AreEqual(Mathf.Lerp(r.Power.minPower, r.Power.maxPower, 0.5f), r.M.PowerResult.RawPower, 1e-5);

            var stats = new SlapStats();
            stats.RecordTiming(FighterSlot.Player, t, 0.5f);
            Assert.AreEqual(1, stats[FighterSlot.Player].Early, "a ring released before gold is coached as early");
        }

        [Test]
        public void HeldPastFullClose_AutoReleasesLate_AtFullPower()
        {
            var r = new Rig();
            r.Press();
            for (int i = 0; i < 300 && r.M.CurrentState != HoldRingMeter.State.Resolved; i++) r.Step();
            Assert.AreEqual(HoldRingMeter.State.Resolved, r.M.CurrentState, "the ring must release itself");
            var t = r.M.TimingResult;
            Assert.AreEqual(1f, t.Position);
            Assert.AreEqual(TimingZone.Miss, t.Zone);
            Assert.IsFalse(t.TimedOut, "a full close is a late release, not a no-show");
            Assert.AreEqual(r.Power.maxPower, r.M.PowerResult.RawPower, 1e-6);
            Assert.LessOrEqual(r.Clock.Now - r.PressedAt, 1.0 + Dt + 1e-9, "released on the first tick past full close");

            var stats = new SlapStats();
            stats.RecordTiming(FighterSlot.Player, t, 0.5f);
            Assert.AreEqual(1, stats[FighterSlot.Player].Late);
        }

        [Test]
        public void NoPress_TimesOut_AsAnUnwoundMiss()
        {
            var r = new Rig();
            r.Begin();
            for (int i = 0; i < 1000 && r.M.CurrentState != HoldRingMeter.State.Resolved; i++) r.Step();
            Assert.AreEqual(HoldRingMeter.State.Resolved, r.M.CurrentState);
            var t = r.M.TimingResult;
            Assert.IsTrue(t.TimedOut);
            Assert.AreEqual(TimingZone.Miss, t.Zone);
            Assert.AreEqual(0f, t.Position);
            Assert.AreEqual(r.Power.minPower, r.M.PowerResult.RawPower, 1e-6, "never zero power");
            Assert.AreEqual(0, r.M.PowerResult.TapsAccepted);
        }

        // ── Input edges ──────────────────────────────────────────────
        [Test]
        public void PressDuringArmDelay_IsRejected_ThenAFreshPressCounts()
        {
            var r = new Rig();
            r.Begin();
            r.Src.Push(r.Clock.Now);
            Assert.AreEqual(HoldRingMeter.State.Arming, r.M.CurrentState);
            Assert.AreEqual(1, r.M.PressesRejected);
            Assert.AreEqual(0f, r.M.Closure);
            r.Step(0.06);
            r.Src.Push(r.Clock.Now);
            Assert.AreEqual(HoldRingMeter.State.Holding, r.M.CurrentState);
        }

        [Test]
        public void PressMadeAfterTheArmMoment_CountsEvenBeforeThisFramesTick()
        {
            // S-087: a long frame delivers the press before Tick has moved Arming -> Ready.
            var r = new Rig();
            r.Begin();
            r.Clock.Advance(0.2);           // armDelay 0.05 has passed, but no Tick yet
            r.Src.Push(r.Clock.Now - 0.01);
            Assert.AreEqual(HoldRingMeter.State.Holding, r.M.CurrentState);
            Assert.AreEqual(0, r.M.PressesRejected);
        }

        [Test]
        public void PressMadeDuringArming_IsRejected_EvenIfDeliveredLate()
        {
            var r = new Rig();
            r.Begin();
            double start = r.Clock.Now;
            r.Clock.Advance(0.1);
            r.Src.Push(start + 0.01);       // pressed 10 ms into a 50 ms arm delay
            Assert.AreNotEqual(HoldRingMeter.State.Holding, r.M.CurrentState);
            Assert.AreEqual(1, r.M.PressesRejected);
        }

        [Test]
        public void Ai_HitchSpanningArmAndPress_StillPlaysTheRing()
        {
            // S-087 as seen in Play: one frame long enough to cover the arm delay and the AI's
            // reaction, so the AI fires its press before the meter's Tick in that frame.
            var clock = new ManualClock(5.0);
            var timing = TestFactories.TimingCfg();
            var ai = new AiTapSource(clock, new SeededRandom(3));
            ai.Configure(TestFactories.Ai(aim: TimingZone.Perfect, aimSigma: 0.005f));
            var m = new HoldRingMeter();
            m.Begin(TestFactories.HoldRingCfg(), timing, TestFactories.PowerCfg(), TestFactories.ZoneLayout(), ai, clock, 1);
            ai.BeginHoldRing(TestFactories.ZoneLayout(), m.ZoneScale, m.CloseSeconds, m.ArmDelaySeconds, timing.displayLatencyBias);

            clock.Advance(0.2); ai.Tick(0.2); m.Tick(0.2);
            Assert.IsTrue(m.HasPressed, "the AI's press was dropped");
            for (int i = 0; i < 500 && m.CurrentState != HoldRingMeter.State.Resolved; i++)
            {
                clock.Advance(1.0 / 60.0); ai.Tick(1.0 / 60.0); m.Tick(1.0 / 60.0);
            }
            Assert.IsFalse(m.TimingResult.TimedOut);
            Assert.AreEqual(TimingZone.Perfect, m.TimingResult.Zone);
        }

        [Test]
        public void ReleaseWithoutPress_IsIgnored()
        {
            var r = new Rig();
            r.Begin();
            r.Step(0.06);
            r.Src.PushRelease(r.Clock.Now);
            Assert.AreEqual(HoldRingMeter.State.Ready, r.M.CurrentState);
        }

        [Test]
        public void SecondPressWhileHolding_IsIgnored()
        {
            var r = new Rig();
            r.Press();
            r.Step(0.2);
            r.Src.Push(r.Clock.Now);
            Assert.AreEqual(0.2f, r.M.Closure, 1e-5, "the ring did not restart");
        }

        [Test]
        public void PressStampedLongAgo_IsClampedToMaxLatencyComp()
        {
            var r = new Rig();
            r.Begin();
            r.Step(1.0);
            r.Src.Push(r.Clock.Now - 5.0); // a hitch delivered a stale press
            Assert.AreEqual(0.12f, r.M.Closure, 1e-5);
        }

        [Test]
        public void PressStampedInTheFuture_StartsNow()
        {
            var r = new Rig();
            r.Begin();
            r.Step(0.5);
            r.Src.Push(r.Clock.Now + 1.0);
            Assert.AreEqual(0f, r.M.Closure);
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
            Assert.AreEqual(HoldRingMeter.State.Arming, r.M.CurrentState);
            r.M.Tick(0.06);
            Assert.AreEqual(HoldRingMeter.State.Ready, r.M.CurrentState);
        }

        [Test]
        public void HugeDt_WhileHolding_ReleasesClosed()
        {
            var r = new Rig();
            r.Press();
            r.Step(1e6);
            Assert.AreEqual(HoldRingMeter.State.Resolved, r.M.CurrentState);
            Assert.AreEqual(1f, r.M.TimingResult.Position);
        }

        [Test]
        public void HugeDt_BeforePress_TimesOut()
        {
            var r = new Rig();
            r.Begin();
            r.Step(1e6);
            Assert.AreEqual(HoldRingMeter.State.Resolved, r.M.CurrentState);
            Assert.IsTrue(r.M.TimingResult.TimedOut);
        }

        // ── Rounds ───────────────────────────────────────────────────
        [Test]
        public void Round10_ClosesFaster_AndShrinksTheZones_LikeTheMarker()
        {
            var r = new Rig(flatSpeed: false); // TestFactories curve: 0.85 @r1 -> 1.45 @r10, zones 1.0 -> 0.70
            r.Begin(10);
            Assert.AreEqual(1.0 / 1.45, r.M.CloseSeconds, 1e-6);
            Assert.AreEqual(0.70f, r.M.ZoneScale, 1e-5);
        }

        // ── R5 ───────────────────────────────────────────────────────
        [Test]
        public void HoldingAndReleasing_AllocateNothing()
        {
            Assert.That(() => { GC.KeepAlive(new object[4]); }, Is.AllocatingGCMemory(), "the allocation meter is blind");
            var r = new Rig();
            r.Press();
            float sink = 0f;
            TestDelegate hold = () =>
            {
                for (int i = 0; i < 40; i++)
                {
                    r.Step();
                    sink += r.M.Closure + r.M.WindUp01 + r.M.Power;
                }
                r.Src.PushRelease(r.Clock.Now);
            };
            Assert.That(hold, Is.Not.AllocatingGCMemory(), "the hold ring allocated (R5)");
            Assert.AreEqual(HoldRingMeter.State.Resolved, r.M.CurrentState);
            Assert.IsFalse(float.IsNaN(sink));
        }

        // ── The AI plays the same ring (M07) ─────────────────────────
        [Test]
        public void Ai_AimingPerfect_LandsPerfect([NUnit.Framework.Range(1, 12)] int seed)
        {
            var clock = new ManualClock(5.0);
            var rng = new SeededRandom(seed);
            var timing = TestFactories.TimingCfg();
            var ai = new AiTapSource(clock, rng);
            var profile = TestFactories.Ai(aim: TimingZone.Perfect, aimSigma: 0.005f);
            ai.Configure(profile);
            var m = new HoldRingMeter();
            var ring = TestFactories.HoldRingCfg();
            m.Begin(ring, timing, TestFactories.PowerCfg(), TestFactories.ZoneLayout(), ai, clock, 1);
            ai.BeginHoldRing(TestFactories.ZoneLayout(), m.ZoneScale, m.CloseSeconds, m.ArmDelaySeconds, timing.displayLatencyBias);
            for (int i = 0; i < 500 && m.CurrentState != HoldRingMeter.State.Resolved; i++)
            {
                clock.Advance(1.0 / 60.0);
                ai.Tick(1.0 / 60.0);
                m.Tick(1.0 / 60.0);
            }
            Assert.AreEqual(HoldRingMeter.State.Resolved, m.CurrentState);
            Assert.AreEqual(TimingZone.Perfect, m.TimingResult.Zone,
                "closure " + m.TimingResult.Position + ": the AI's release must not be judged a display-bias early (S-079)");
        }

        // ── Through the FSM (M01) ────────────────────────────────────
        private static MatchDirector Director(ManualClock clock, BoutConfig bout, HoldRingConfig ring, int seed = 42)
        {
            return new MatchDirector(bout, TestFactories.PowerCfg(0.6f), TestFactories.TimingCfg(),
                                     TestFactories.ZoneLayout(), TestFactories.DamageCfg(), clock,
                                     new SeededRandom(seed), ring);
        }

        [Test]
        public void HoldRingDuel_Terminates_WithSignalsInGestureOrder()
        {
            var clock = new ManualClock();
            // 400 HP: a Perfect hold-ring slap is 22 x 1.0 x 3.0 = 66, so both fighters get turns.
            var d = Director(clock, TestFactories.Bout(hp: 400, mode: AttackMode.HoldRing), TestFactories.HoldRingCfg());
            var player = new ManualTapSource();
            d.BindPlayerSource(player);
            d.BindOpponent(TestFactories.Ai());

            var order = new StringBuilder();
            bool ended = false;
            bool sawOpponentRing = false;
            int playerPerfects = 0;
            double ringOpenAt = -1, pressAt = -1;
            d.Signals.TimingStart += t => { if (t.Attacker == FighterSlot.Player) { order.Append('T'); ringOpenAt = clock.Now; } };
            d.Signals.ChargeStart += t => { if (t.Attacker == FighterSlot.Player) order.Append('C'); };
            d.Signals.ChargeEnd   += p => { if (d.CurrentTurn.Attacker == FighterSlot.Player) order.Append('c'); };
            d.Signals.TimingEnd   += r =>
            {
                if (d.CurrentTurn.Attacker != FighterSlot.Player) return;
                order.Append("t ");
                if (r.Zone == TimingZone.Perfect) playerPerfects++;
            };
            d.Signals.BoutEnd += _ => ended = true;

            d.StartBout();
            Assert.AreEqual(AttackMode.HoldRing, d.CurrentAttackMode);
            for (int i = 0; i < 40000 && !ended; i++)
            {
                clock.Advance(Dt);
                // The player presses 0.2 s after the ring opens and lets go when it shows gold.
                if (ringOpenAt >= 0 && pressAt < 0 && clock.Now - ringOpenAt >= 0.2)
                {
                    pressAt = clock.Now;
                    player.Push(pressAt, TapEvent.SourcePlayerTouch);
                }
                if (pressAt >= 0 && clock.Now - pressAt >= 0.03 + 0.5 / 0.85 - 1e-9)
                {
                    player.PushRelease(clock.Now, TapEvent.SourcePlayerTouch);
                    ringOpenAt = pressAt = -1;
                }
                d.Tick();
                if (d.CurrentState == MatchState.HoldRing && d.CurrentTurn.Attacker == FighterSlot.Opponent)
                    sawOpponentRing = true;
            }

            Assert.IsTrue(ended, "bout did not end");
            Assert.IsTrue(sawOpponentRing, "the AI must play the hold ring too");
            StringAssert.StartsWith("TCct ", order.ToString(), "ring opens, press winds up, release ends both halves");
            Assert.AreEqual(0, order.ToString().Replace("TCct ", "").Length, "every player turn: " + order);
            Assert.Greater(playerPerfects, 0, "a release on gold must land Perfect through the FSM");
        }

        [Test]
        public void HoldRingMode_WithoutConfig_FallsBackToTapMarker()
        {
            var clock = new ManualClock();
            var d = Director(clock, TestFactories.Bout(mode: AttackMode.HoldRing), null);
            d.BindPlayerSource(new ManualTapSource());
            d.BindOpponent(TestFactories.Ai());
            LogAssert.Expect(LogType.Error, new Regex("no HoldRingConfig"));
            d.StartBout();
            Assert.AreEqual(AttackMode.TapMarker, d.CurrentAttackMode);
            for (int i = 0; i < 1000 && d.CurrentState != MatchState.Charge; i++) { clock.Advance(Dt); d.Tick(); }
            Assert.AreEqual(MatchState.Charge, d.CurrentState);
        }

        [Test]
        public void AttackMode_IsReadAtEveryTurnStart()
        {
            var clock = new ManualClock();
            var bout = TestFactories.Bout(mode: AttackMode.TapMarker);
            var d = Director(clock, bout, TestFactories.HoldRingCfg());
            d.BindPlayerSource(new ManualTapSource());
            d.BindOpponent(TestFactories.Ai());
            d.StartBout();
            for (int i = 0; i < 2000 && d.CurrentState != MatchState.Charge; i++) { clock.Advance(Dt); d.Tick(); }
            Assert.AreEqual(MatchState.Charge, d.CurrentState);

            bout.attackMode = AttackMode.HoldRing; // a Tuning Hub edit mid-bout
            for (int i = 0; i < 4000 && !(d.CurrentState == MatchState.HoldRing); i++) { clock.Advance(Dt); d.Tick(); }
            Assert.AreEqual(MatchState.HoldRing, d.CurrentState, "the next turn plays the new mode");
            Assert.AreEqual(AttackMode.HoldRing, d.CurrentAttackMode);
        }
    }
}
