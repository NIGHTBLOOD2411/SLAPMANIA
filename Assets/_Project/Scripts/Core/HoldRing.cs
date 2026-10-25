namespace SlapMania.Core
{
    /// The hold-ring attack's pure math (ADR-0014, M04 §5b). Stateless: time arrives as double
    /// seconds, config as plain numbers (R2, R8, ADR-0004).
    ///
    /// Closure 0 is the ring fully open (the moment of the press), 1 is the ring closed on the
    /// target. Closure is a TimingZoneLayout position, so the ring lands in exactly the bands
    /// the M04 marker does: the Perfect band is the gold band, releasing before it is "early"
    /// (closure &lt; perfectCentre) and after it "late".
    public static class HoldRing
    {
        /// Below this the round's speed is treated as broken config and clamped (M04 does the same).
        public const double MinRoundSpeed = 0.01;

        /// Seconds for the ring to close fully this round: closeMs (authored in ms, R8) divided by
        /// M04's speedByRound value, so the round ramp is shared by both attack modes.
        /// closeMs NaN or &lt;= 0 gives 0 (an instantly closed ring: a Miss, never a hang).
        public static double CloseSeconds(double closeMs, double roundSpeed)
        {
            if (double.IsNaN(closeMs) || closeMs <= 0.0) return 0.0;
            if (double.IsNaN(roundSpeed)) roundSpeed = 1.0;
            if (roundSpeed < MinRoundSpeed) roundSpeed = MinRoundSpeed;
            return closeMs * 0.001 / roundSpeed;
        }

        /// Closure in [0, 1] after `heldSeconds` of hold. Linear in time, so a zone band of width w
        /// is a window of w x closeSeconds - the same ms window the marker gives at one traversal
        /// per closeSeconds. NaN or negative hold is 0 (open: never a free Perfect); a ring with
        /// no close time (closeSeconds NaN or &lt;= 0) is already closed.
        public static float ClosureAt(double heldSeconds, double closeSeconds)
        {
            if (double.IsNaN(heldSeconds) || heldSeconds <= 0.0) return 0f;
            if (double.IsNaN(closeSeconds) || closeSeconds <= 0.0) return 1f;
            double c = heldSeconds / closeSeconds;
            return c >= 1.0 ? 1f : (float)c;
        }

        /// Inverse of ClosureAt: the hold at which the ring reaches `closure01` (clamped to
        /// [0, 1]). Closed form, so the AI aims a release exactly (M07). NaN closure is 0.
        public static double HeldSecondsAt(float closure01, double closeSeconds)
        {
            if (double.IsNaN(closeSeconds) || closeSeconds <= 0.0) return 0.0;
            if (float.IsNaN(closure01) || closure01 <= 0f) return 0.0;
            if (closure01 >= 1f) return closeSeconds;
            return closure01 * closeSeconds;
        }

        /// The wind-up: how much of the power range the hold has built by `closure01`, in [0, 1].
        /// 0 at the press, 1 from `peakClosure` on (the owner's rule: power peaks on the gold band
        /// and then holds - releasing late costs multiplier, not power). The power itself is
        /// Lerp(minPower, maxPower, WindUpAt(...)) with M03's floor and ceiling, so both attack
        /// modes share one damage scale.
        public static float WindUpAt(float closure01, float peakClosure, float exponent)
        {
            if (float.IsNaN(closure01) || closure01 <= 0f) return 0f;
            if (float.IsNaN(peakClosure) || peakClosure <= 0f || closure01 >= peakClosure) return 1f;
            float t = closure01 / peakClosure; // in (0, 1): how far through the wind-up

            // Ease-in t^e: e = 1 is linear, e > 1 holds power back until near gold (an early release
            // is weak twice over), e < 1 front-loads it. A broken exponent (NaN, <= 0, infinite)
            // falls back to linear rather than to a flat or NaN wind-up.
            if (float.IsNaN(exponent) || float.IsInfinity(exponent) || exponent <= 0f || exponent == 1f) return t;
            float w = (float)System.Math.Pow(t, exponent);
            return w < 0f ? 0f : w > 1f ? 1f : w;
        }
    }
}
