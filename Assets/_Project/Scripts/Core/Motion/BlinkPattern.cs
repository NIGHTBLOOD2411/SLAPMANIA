namespace SlapMania.Core.Motion
{
    /// A looping light pattern: a list of brightness levels, each held for one step (R8: ms).
    /// The Title spotlights read it every frame, so it is a pure function of elapsed time -
    /// no state, no `level += rate * dt`, and a frame hitch never puts the pattern out of step.
    /// Bad input (null or empty levels, NaN, negative time or step) returns a steady, valid level.
    public static class BlinkPattern
    {
        /// Brightness 0..1 at `elapsedMs` into a pattern of `levels`, `stepMs` per level.
        /// At the exact start of step i the result is levels[i] (clamped to 0..1).
        public static float Evaluate(float[] levels, double elapsedMs, double stepMs)
        {
            if (levels == null || levels.Length == 0) return 1f;
            if (double.IsNaN(stepMs) || stepMs <= 0.0) return Clamp01(levels[0]);
            if (double.IsNaN(elapsedMs) || double.IsInfinity(elapsedMs) || elapsedMs < 0.0) elapsedMs = 0.0;

            double cycle = stepMs * levels.Length;
            double t = elapsedMs % cycle; // ms into the current loop, 0 <= t < cycle

            // Hold each level crisp, then ease into the next over the step's last FadeShare:
            // pulses read as blinks without a hard pop. The last step fades into the first.
            int i = (int)(t / stepMs);
            if (i >= levels.Length) i = levels.Length - 1; // t a hair under cycle can round up
            float frac = (float)((t - i * stepMs) / stepMs);
            float a = Clamp01(levels[i]);
            if (frac <= 1f - FadeShare) return a;
            float b = Clamp01(levels[(i + 1) % levels.Length]);
            float k = Easing.Evaluate(Ease.InOutSine, (frac - (1f - FadeShare)) / FadeShare);
            return a + (b - a) * k;
        }

        /// Share of each step spent fading into the next level (0 = hard cut, 1 = crossfade).
        private const float FadeShare = 0.3f;

        private static float Clamp01(float v) => v > 1f ? 1f : (v > 0f ? v : 0f); // NaN -> 0
    }
}
