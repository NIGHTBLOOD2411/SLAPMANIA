using NUnit.Framework;
using SlapMania.Core;
using SlapMania.Gameplay;

namespace SlapMania.Tests.EditMode
{
    /// ChampionRules (ADR-0022) and how the bout uses it: stat scales, stamina -> health, the
    /// opponent pick, and the resolver's StatScale. R3 boundaries: 0, 99, neutral, out of range,
    /// NaN, negative and huge sensitivities.
    public class ChampionRulesTests
    {
        private const float K = 0.004f;

        // ── StatScale ─────────────────────────────────────────────────
        [Test]
        public void StatScale_NeutralIsExactlyOne()
        {
            Assert.AreEqual(1f, ChampionRules.StatScale(ChampionRules.StatNeutral, K), 1e-6f);
            Assert.AreEqual(1f, ChampionRules.StatScale(ChampionRules.StatNeutral, -K), 1e-6f);
        }

        [Test]
        public void StatScale_ZeroSensitivity_IsOneForEveryStat()
        {
            Assert.AreEqual(1f, ChampionRules.StatScale(0, 0f), 1e-6f);
            Assert.AreEqual(1f, ChampionRules.StatScale(99, 0f), 1e-6f);
        }

        [Test]
        public void StatScale_RisesWithTheStat_AndNegativeSensitivityInverts()
        {
            float lo = ChampionRules.StatScale(50, K), hi = ChampionRules.StatScale(99, K);
            Assert.Less(lo, 1f);
            Assert.Greater(hi, 1f);
            Assert.Greater(ChampionRules.StatScale(50, -K), 1f, "inverted: a low chin takes more");
            Assert.Less(ChampionRules.StatScale(99, -K), 1f, "inverted: a high chin takes less");
        }

        [Test]
        public void StatScale_OutOfRangeStats_ClampToTheEnds()
        {
            Assert.AreEqual(ChampionRules.StatScale(ChampionRules.StatMax, K), ChampionRules.StatScale(500, K), 1e-6f);
            Assert.AreEqual(ChampionRules.StatScale(ChampionRules.StatMin, K), ChampionRules.StatScale(-40, K), 1e-6f);
        }

        [Test]
        public void StatScale_StaysInsideTheBounds_ForAnySensitivity()
        {
            foreach (float k in new[] { 1f, -1f, 1000f, -1000f, float.MaxValue, float.MinValue })
            foreach (int s in new[] { 0, 99 })
            {
                float v = ChampionRules.StatScale(s, k);
                Assert.GreaterOrEqual(v, ChampionRules.MinScale, "stat " + s + " k " + k);
                Assert.LessOrEqual(v, ChampionRules.MaxScale, "stat " + s + " k " + k);
            }
        }

        [Test]
        public void StatScale_NaNOrInfiniteSensitivity_IsOne()
        {
            Assert.AreEqual(1f, ChampionRules.StatScale(99, float.NaN), 1e-6f);
            Assert.AreEqual(1f, ChampionRules.StatScale(10, float.PositiveInfinity), 1e-6f);
            Assert.AreEqual(1f, ChampionRules.StatScale(10, float.NegativeInfinity), 1e-6f);
        }

        [Test]
        public void SlapScale_IsAttackerPowerTimesDefenderChin()
        {
            var a = ChampionStats.Neutral; a.power = 99;
            var d = ChampionStats.Neutral; d.chin = 99;
            float expected = ChampionRules.StatScale(99, K) * ChampionRules.StatScale(99, -K);
            Assert.AreEqual(expected, ChampionRules.SlapScale(a, d, K, K), 1e-6f);
            Assert.AreEqual(1f, ChampionRules.SlapScale(ChampionStats.Neutral, ChampionStats.Neutral, K, K), 1e-6f);
        }

        // ── MaxHp ─────────────────────────────────────────────────────
        [Test]
        public void MaxHp_NeutralKeepsTheBase()
        {
            Assert.AreEqual(100, ChampionRules.MaxHp(100, ChampionRules.StatNeutral, 0.5f));
        }

        [Test]
        public void MaxHp_ScalesPerPoint_AndClampsTheStat()
        {
            Assert.AreEqual(112, ChampionRules.MaxHp(100, 99, 0.5f), "99 - 75 = 24 points x 0.5 = +12");
            Assert.AreEqual(63, ChampionRules.MaxHp(100, 0, 0.5f), "-75 x 0.5 = -37.5, rounded away from zero");
            Assert.AreEqual(112, ChampionRules.MaxHp(100, 250, 0.5f), "stat clamps to 99");
        }

        [Test]
        public void MaxHp_NeverBelowOne_AndIgnoresNaN()
        {
            Assert.AreEqual(1, ChampionRules.MaxHp(20, 0, 100f));
            Assert.AreEqual(1, ChampionRules.MaxHp(-50, 75, 0.5f));
            Assert.AreEqual(100, ChampionRules.MaxHp(100, 99, float.NaN));
        }

        // ── PickOpponent ──────────────────────────────────────────────
        [Test]
        public void PickOpponent_EmptyIsMinusOne_SingleIsMirror()
        {
            var rng = new SeededRandom(1);
            Assert.AreEqual(-1, ChampionRules.PickOpponent(0, 0, rng));
            Assert.AreEqual(0, ChampionRules.PickOpponent(1, 0, rng));
        }

        [Test]
        public void PickOpponent_NeverThePlayer_AndReachesEveryOther()
        {
            var rng = new SeededRandom(7);
            var seen = new bool[4];
            for (int i = 0; i < 400; i++)
            {
                int o = ChampionRules.PickOpponent(4, 2, rng);
                Assert.AreNotEqual(2, o);
                Assert.That(o, Is.InRange(0, 3));
                seen[o] = true;
            }
            Assert.IsTrue(seen[0] && seen[1] && seen[3]);
        }

        [Test]
        public void PickOpponent_UnknownPlayer_PicksAnyValidIndex()
        {
            var rng = new SeededRandom(3);
            for (int i = 0; i < 50; i++)
                Assert.That(ChampionRules.PickOpponent(3, -1, rng), Is.InRange(0, 2));
        }

        [Test]
        public void PickOpponent_SameSeed_SamePick()
        {
            Assert.AreEqual(ChampionRules.PickOpponent(5, 1, new SeededRandom(99)),
                            ChampionRules.PickOpponent(5, 1, new SeededRandom(99)));
        }

        // ── DamageResolver.StatScale ─────────────────────────────────
        [Test]
        public void Resolver_StatScaleOne_MatchesTheDefault()
        {
            var cfg = TestFactories.DamageCfg();
            var plain = DamageResolver.Resolve(new DamageInput(1f, 1f, 0, 200, 200, 0f), cfg);
            var one = DamageResolver.Resolve(new DamageInput(1f, 1f, 0, 200, 200, 0f, 1f), cfg);
            Assert.AreEqual(plain.RawDamage, one.RawDamage, 1e-5f);
        }

        [Test]
        public void Resolver_StatScale_MultipliesRaw_AndIsClamped()
        {
            var cfg = TestFactories.DamageCfg();
            float baseRaw = DamageResolver.Resolve(new DamageInput(1f, 1f, 0, 500, 500, 0f), cfg).RawDamage;
            Assert.AreEqual(baseRaw * 1.5f, DamageResolver.Resolve(new DamageInput(1f, 1f, 0, 500, 500, 0f, 1.5f), cfg).RawDamage, 1e-4f);
            Assert.AreEqual(baseRaw * ChampionRules.MaxScale, DamageResolver.Resolve(new DamageInput(1f, 1f, 0, 500, 500, 0f, 1e9f), cfg).RawDamage, 1e-3f);
            Assert.AreEqual(baseRaw * ChampionRules.MinScale, DamageResolver.Resolve(new DamageInput(1f, 1f, 0, 500, 500, 0f, -3f), cfg).RawDamage, 1e-4f);
            Assert.AreEqual(baseRaw, DamageResolver.Resolve(new DamageInput(1f, 1f, 0, 500, 500, 0f, float.NaN), cfg).RawDamage, 1e-4f);
        }

        // ── MatchDirector ────────────────────────────────────────────
        [Test]
        public void Director_StaminaSetsEachFightersMaxHp()
        {
            var bout = TestFactories.Bout(hp: 100);
            bout.staminaHpPerPoint = 0.5f;
            var d = new MatchDirector(bout, TestFactories.PowerCfg(), TestFactories.TimingCfg(), TestFactories.ZoneLayout(),
                                      TestFactories.DamageCfg(), new ManualClock(), new SeededRandom(1));
            d.BindPlayerSource(new ManualTapSource());
            d.BindOpponent(TestFactories.Ai());
            var tank = ChampionStats.Neutral; tank.stamina = 99;
            var glass = ChampionStats.Neutral; glass.stamina = 55;
            d.BindChampions(tank, glass);
            d.StartBout();
            Assert.AreEqual(112, d.PlayerHealth.MaxHp);
            Assert.AreEqual(90, d.OpponentHealth.MaxHp);
        }

        [Test]
        public void Director_WithoutChampions_UsesStartingHp()
        {
            var d = new MatchDirector(TestFactories.Bout(hp: 80), TestFactories.PowerCfg(), TestFactories.TimingCfg(), TestFactories.ZoneLayout(),
                                      TestFactories.DamageCfg(), new ManualClock(), new SeededRandom(1));
            d.BindPlayerSource(new ManualTapSource());
            d.BindOpponent(TestFactories.Ai());
            d.StartBout();
            Assert.AreEqual(80, d.PlayerHealth.MaxHp);
            Assert.AreEqual(80, d.OpponentHealth.MaxHp);
        }

        [Test]
        public void Overall_IsTheRoundedMean()
        {
            var s = new ChampionStats { power = 76, speed = 93, chin = 68, technique = 91, rage = 79, stamina = 82 };
            Assert.AreEqual(82, s.Overall, "489 / 6 = 81.5 rounds up");
            Assert.AreEqual(ChampionRules.StatNeutral, ChampionStats.Neutral.Overall);
        }
    }
}
