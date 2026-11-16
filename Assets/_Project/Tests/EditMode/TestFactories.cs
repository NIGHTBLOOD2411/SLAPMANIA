using UnityEngine;
using SlapMania.Data;

namespace SlapMania.Tests.EditMode
{
    internal static class TestFactories
    {
        public static PowerPhaseConfig PowerCfg(float phaseDuration = 2.5f)
        {
            var c = ScriptableObject.CreateInstance<PowerPhaseConfig>();
            c.phaseDuration = phaseDuration;
            c.impulsePerTap = 0.072f;
            c.decayPerSecond = 0.12f;
            c.maxFill = 1.0f;
            c.minTapInterval = 0.045f;
            c.diminishWindow = 0.30f;
            c.diminishSoftTaps = 3;
            c.diminishHardTaps = 6;
            c.diminishFloor = 0.50f;
            c.phaseShape = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(0.6f, 1f), new Keyframe(1f, 1.6f));
            c.feedbackCurve = new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(1f, 1f));
            c.minPower = 0.25f;
            c.maxPower = 1.0f;
            c.overchargeRatePerTap = 0.09f;
            c.overchargeGain = 0.12f;
            c.overchargeSpeedPenalty = 0f;
            return c;
        }

        public static TimingPhaseConfig TimingCfg()
        {
            var c = ScriptableObject.CreateInstance<TimingPhaseConfig>();
            c.baseTraversalsPerSecond = 1.0f;
            c.speedByRound = new AnimationCurve(new Keyframe(1f, 0.85f), new Keyframe(10f, 1.45f));
            c.zoneScaleByRound = new AnimationCurve(new Keyframe(1f, 1.0f), new Keyframe(10f, 0.70f));
            c.armDelay = 0.05f;   // shortened so tests move fast
            c.timeout = 3.0f;
            c.displayLatencyBias = 0.030f;
            c.maxLatencyComp = 0.120f;
            c.startPhaseGuard = 0.05f;
            c.perfectCentre = 0.50f;
            c.aimCueMs = 0;       // off so the marker starts at Begin; the S-093 tests opt in
            c.spamGapMs = 0;
            return c;
        }

        public static TimingZoneLayout ZoneLayout() => ScriptableObject.CreateInstance<TimingZoneLayout>();

        /// 1000 ms close; the arm delay is shortened so tests move fast (like TimingCfg).
        public static HoldRingConfig HoldRingCfg(int closeMs = 1000, float peakClosure = 0.5f, float exponent = 1f)
        {
            var c = ScriptableObject.CreateInstance<HoldRingConfig>();
            c.closeMs = closeMs;
            c.armDelayMs = 50;
            c.pressTimeoutMs = 4000;
            c.windUpPeakClosure = peakClosure;
            c.windUpExponent = exponent;
            return c;
        }

        public static DamageConfig DamageCfg()
        {
            var c = ScriptableObject.CreateInstance<DamageConfig>();
            c.baseSlapDamage = 22f;
            c.escalationBase = 1.06f;
            c.minDamageFloor = 3;
            c.critThreshold = 2.00f;
            c.finisherEnabled = true;
            c.finisherHpThreshold = 0f;
            c.globalDamageScale = 1f;
            return c;
        }

        /// TapMarker by default, like the asset (ADR-0014 amendment, 2026-10-23): the duel tests
        /// script taps and commits, so hold-ring tests opt in explicitly.
        public static BoutConfig Bout(int hp = 100, int maxTurns = 40,
                                      SlapMania.Core.AttackMode mode = SlapMania.Core.AttackMode.TapMarker)
        {
            var b = ScriptableObject.CreateInstance<BoutConfig>();
            b.attackMode = mode;
            b.startingHp = hp;
            b.playerGoesFirst = true;
            b.maxRounds = 20;
            b.maxTurns = maxTurns;
            b.finisherEnabled = true;
            b.matchSeed = 12345;
            b.slapshotHoldMs = 0; // timing tests measure the FSM without the slapshot pause; SlapshotPacing opts in
            return b;
        }

        public static AiProfile Ai(float tps = 8f, SlapMania.Core.TimingZone aim = SlapMania.Core.TimingZone.Good, float aimSigma = 0.06f)
        {
            var p = ScriptableObject.CreateInstance<AiProfile>();
            p.targetTps = tps;
            p.tpsVariance = 0.10f;
            p.tapFatigue = 0.05f;
            p.timingAimZone = aim;
            p.aimSigma = aimSigma;
            p.reactionDelay = 0.08f;
            p.commitOnFirstPass = 1.0f;
            return p;
        }
    }
}
