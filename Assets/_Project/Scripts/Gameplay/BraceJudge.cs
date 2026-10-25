using SlapMania.Core;
using SlapMania.Data;
using SlapMania.Logging;

namespace SlapMania.Gameplay
{
    /// The brace (M05, ADR-0019). Pure (R2): the player taps as the rival's hand comes in, and
    /// the distance from the brace target (impact - DamageConfig.braceLeadMs, chosen by the
    /// director) picks the grade. The tap is judged where the player saw
    /// it, one display bias earlier (M04 §5), so it lines up with the hand on screen.
    public static class BraceJudge
    {
        /// `tapAt`: the first tap's event time (NaN = no tap). `targetAt`: the brace target. Same
        /// clock for both. OffsetMs is seen-tap minus target.
        public static BraceResult Judge(double tapAt, double targetAt, double displayLatencyBias, DamageConfig cfg)
        {
            SlapAssert.NotNull(cfg, nameof(cfg));
            if (double.IsNaN(tapAt) || double.IsNaN(targetAt) || double.IsInfinity(tapAt)) return BraceResult.NoBrace;
            if (double.IsNaN(displayLatencyBias) || displayLatencyBias < 0) displayLatencyBias = 0;

            double offsetMs = (tapAt - displayLatencyBias - targetAt) * 1000.0;
            double abs = offsetMs < 0 ? -offsetMs : offsetMs;
            if (abs <= cfg.bracePerfectMs) return new BraceResult(BraceGrade.Perfect, Clamp01(cfg.bracePerfectReduction), (float)offsetMs);
            if (abs <= cfg.braceGoodMs)    return new BraceResult(BraceGrade.Good, Clamp01(cfg.braceGoodReduction), (float)offsetMs);
            return new BraceResult(BraceGrade.Flinch, 0f, (float)offsetMs);
        }

        private static float Clamp01(float v) => float.IsNaN(v) || v < 0f ? 0f : v > 1f ? 1f : v;
    }
}
