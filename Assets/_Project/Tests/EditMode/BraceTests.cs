using System;
using NUnit.Framework;
using SlapMania.Core;
using SlapMania.Gameplay;

namespace SlapMania.Tests.EditMode
{
    /// The brace (ADR-0019): BraceJudge's windows, and the director applying it on the rival's
    /// turn. Shipped values: target 100 ms before impact, Perfect ±70 ms (−50%), Good ±160 ms
    /// (−25%), bias 30 ms. The pure tests pass the target directly.
    public class BraceTests
    {
        private const double Bias = 0.03;
        private const double Impact = 10.0;          // the target, in the pure tests
        /// Seconds after the wind-up starts for a tap seen exactly on target: 0.5 s wind-up,
        /// 0.1 s lead, plus the display bias. Still before the impact (0.5).
        private const double PerfectTapAfterWindup = 0.5 - 0.1 + Bias;

        private static BraceResult J(double tapAt) => BraceJudge.Judge(tapAt, Impact, Bias, TestFactories.DamageCfg());

        // ── BraceJudge ───────────────────────────────────────────────
        [Test]
        public void TapSeenExactlyAtImpact_IsPerfect()
        {
            var r = J(Impact + Bias);
            Assert.AreEqual(BraceGrade.Perfect, r.Grade);
            Assert.AreEqual(0.5f, r.Reduction, 1e-6);
            Assert.AreEqual(0f, r.OffsetMs, 1e-3);
        }

        [Test]
        public void WindowEdges_OneMicrosecondInside_AndOneMicrosecondPast([Values(-1, 1)] int side)
        {
            // Exactly 70.000 ms is not representable through the double sums, so the edge is
            // checked 1 us either side, on both the early and the late side.
            Assert.AreEqual(BraceGrade.Perfect, J(Impact + Bias + side * (0.070 - 1e-6)).Grade, "just inside 70 ms");
            Assert.AreEqual(BraceGrade.Good, J(Impact + Bias + side * (0.070 + 1e-6)).Grade, "just past 70 ms");
            Assert.AreEqual(BraceGrade.Good, J(Impact + Bias + side * (0.160 - 1e-6)).Grade, "just inside 160 ms");
            Assert.AreEqual(BraceGrade.Flinch, J(Impact + Bias + side * (0.160 + 1e-6)).Grade, "just past 160 ms");
        }

        [Test]
        public void Flinch_ReducesNothing_AndSaysWhichSide()
        {
            var early = J(Impact - 0.4);
            Assert.AreEqual(BraceGrade.Flinch, early.Grade);
            Assert.AreEqual(0f, early.Reduction);
            Assert.Less(early.OffsetMs, 0f, "early is negative");
            Assert.Greater(J(Impact + 0.4).OffsetMs, 0f, "late is positive");
        }

        [Test]
        public void NoTapOrBadNumbers_AreNoBrace([Values(double.NaN, double.PositiveInfinity, double.NegativeInfinity)] double tapAt)
        {
            var r = J(tapAt);
            Assert.AreEqual(BraceGrade.None, r.Grade);
            Assert.AreEqual(0f, r.Reduction);
        }

        [Test]
        public void NaNOrNegativeBias_CountsAsZero([Values(double.NaN, -1.0)] double bias)
        {
            var r = BraceJudge.Judge(Impact, Impact, bias, TestFactories.DamageCfg());
            Assert.AreEqual(BraceGrade.Perfect, r.Grade);
            Assert.AreEqual(0f, r.OffsetMs, 1e-3);
        }

        [Test]
        public void ReductionsOutsideZeroToOne_AreClamped()
        {
            var cfg = TestFactories.DamageCfg();
            cfg.bracePerfectReduction = 5f;
            cfg.braceGoodReduction = -1f;
            Assert.AreEqual(1f, BraceJudge.Judge(Impact, Impact, 0, cfg).Reduction);
            Assert.AreEqual(0f, BraceJudge.Judge(Impact + 0.1, Impact, 0, cfg).Reduction);
        }

        [Test]
        public void HugeOffset_IsAFlinch_NotAnOverflow()
        {
            Assert.AreEqual(BraceGrade.Flinch, J(Impact + 1e12).Grade);
            Assert.AreEqual(BraceGrade.Flinch, J(-1e12).Grade);
        }

        // ── Through the FSM ──────────────────────────────────────────
        private sealed class Duel
        {
            public readonly ManualClock Clock = new ManualClock();
            public readonly ManualTapSource Player = new ManualTapSource();
            public readonly MatchDirector D;
            public double ImpactAt = -1;
            public int Unbraced = -1, Braced = -1;
            public BraceResult Brace;
            public bool BraceRaised;

            public Duel(int hp = 400, bool braceEnabled = true)
            {
                var dmg = TestFactories.DamageCfg();
                dmg.braceEnabled = braceEnabled;
                D = new MatchDirector(TestFactories.Bout(hp: hp), TestFactories.PowerCfg(0.6f), TestFactories.TimingCfg(),
                                      TestFactories.ZoneLayout(), dmg, Clock, new SeededRandom(11));
                D.BindPlayerSource(Player);
                D.BindOpponent(TestFactories.Ai(tps: 9f, aim: TimingZone.Perfect, aimSigma: 0.005f));
                D.Signals.WindupStart += at => { if (D.CurrentTurn.Attacker == FighterSlot.Opponent) ImpactAt = at; };
                D.Signals.DamageResolved += r => { if (D.CurrentTurn.Attacker == FighterSlot.Opponent) Unbraced = r.FinalDamage; };
                D.Signals.BraceResolved += r => { Brace = r; BraceRaised = true; };
                D.Signals.ImpactFrame += r => { if (D.CurrentTurn.Attacker == FighterSlot.Opponent) Braced = r.FinalDamage; };
                D.StartBout();
            }

            /// Plays to the rival's first impact. `tapAfterWindup`: seconds after the wind-up
            /// starts to tap once (null = never); `mash`: tap every 100 ms through the wind-up.
            public void PlayToRivalImpact(double? tapAfterWindup, bool mash = false)
            {
                bool tapped = false;
                double lastMash = -1;
                for (int i = 0; i < 20000 && Braced < 0; i++)
                {
                    Clock.Advance(0.005);
                    if (ImpactAt >= 0 && D.IsBraceWindowOpen)
                    {
                        double windupStart = ImpactAt - 0.5;
                        if (mash && Clock.Now - lastMash >= 0.1) { Player.Push(Clock.Now); lastMash = Clock.Now; }
                        if (!tapped && tapAfterWindup.HasValue && Clock.Now >= windupStart + tapAfterWindup.Value)
                        {
                            Player.Push(windupStart + tapAfterWindup.Value);
                            tapped = true;
                        }
                    }
                    D.Tick();
                }
                Assert.GreaterOrEqual(Braced, 0, "the rival never landed a slap");
            }
        }

        [Test]
        public void PerfectBrace_HalvesTheRivalsSlap()
        {
            var d = new Duel();
            d.PlayToRivalImpact(PerfectTapAfterWindup); // seen exactly on target
            Assert.IsTrue(d.BraceRaised);
            Assert.AreEqual(BraceGrade.Perfect, d.Brace.Grade);
            Assert.Greater(d.Unbraced, 10, "the test needs a real slap to cut");
            Assert.AreEqual(Math.Max(3, (int)Math.Round(d.Unbraced * 0.5, MidpointRounding.AwayFromZero)), d.Braced, 1,
                            "unbraced " + d.Unbraced);
            Assert.AreEqual(400 - d.Braced, d.D.PlayerHealth.CurrentHp, "the braced damage is what was committed");
            Assert.AreEqual(BraceGrade.Perfect, d.D.LastBrace.Grade);
        }

        [Test]
        public void NoTap_ChangesNothing_AndReportsNoBrace()
        {
            var d = new Duel();
            d.PlayToRivalImpact(null);
            Assert.AreEqual(BraceGrade.None, d.Brace.Grade);
            Assert.AreEqual(d.Unbraced, d.Braced);
        }

        [Test]
        public void MashingThroughTheWindup_Flinches_OnlyTheFirstTapCounts()
        {
            var d = new Duel();
            d.PlayToRivalImpact(null, mash: true);
            Assert.AreEqual(BraceGrade.Flinch, d.Brace.Grade, "the first mash tap is ~400 ms early");
            Assert.AreEqual(d.Unbraced, d.Braced);
        }

        [Test]
        public void BraceDisabled_NeverListens()
        {
            var d = new Duel(braceEnabled: false);
            d.PlayToRivalImpact(PerfectTapAfterWindup);
            Assert.IsFalse(d.BraceRaised);
            Assert.AreEqual(d.Unbraced, d.Braced);
        }

        [Test]
        public void ABrace_CanSaveThePlayerFromALethalSlap()
        {
            // HP low enough that the rival's unbraced Perfect slap is lethal, high enough that half is not.
            for (int hp = 20; hp <= 60; hp += 5)
            {
                var d = new Duel(hp: hp);
                d.PlayToRivalImpact(PerfectTapAfterWindup);
                if (d.Unbraced < hp || d.Braced >= hp) continue;   // not this HP's case
                Assert.Greater(d.D.PlayerHealth.CurrentHp, 0, "a Perfect brace should have saved hp " + hp);
                Assert.AreNotEqual(Core.MatchState.Finisher, d.D.CurrentState);
                return;
            }
            Assert.Inconclusive("no HP in 20..60 made the rival's slap lethal only when unbraced");
        }

        [Test]
        public void PlayerTurns_NeverOpenTheWindow()
        {
            var d = new Duel();
            bool openOnPlayerWindup = false;
            d.D.Signals.WindupStart += _ =>
            {
                if (d.D.CurrentTurn.Attacker == FighterSlot.Player && d.D.IsBraceWindowOpen) openOnPlayerWindup = true;
            };
            d.PlayToRivalImpact(null);
            Assert.IsFalse(openOnPlayerWindup);
        }
    }
}
