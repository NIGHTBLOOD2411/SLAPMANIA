namespace SlapMania.Core.Motion
{
    public enum Ease : byte
    {
        Linear,
        InQuad, OutQuad, InOutQuad,
        InCubic, OutCubic, InOutCubic,
        OutQuart, OutExpo,
        InBack, OutBack,
        OutElastic, OutBounce,
        // Appended for M16 camera keys (ADR-0020). Enums serialize by value: append only,
        // never insert, or every Ease already saved in a scene or asset changes curve.
        InSine, OutSine, InOutSine,
        InQuart, InOutQuart,
        InQuint, OutQuint, InOutQuint,
        InExpo, InOutExpo,
        InCirc, OutCirc, InOutCirc,
        InOutBack,
        InElastic, InOutElastic,
        InBounce, InOutBounce
    }

    /// Pure easing curves (ADR-0007: hand-written, no DOTween). Every curve maps 0 -> 0 and
    /// 1 -> 1; Back and Elastic overshoot in between on purpose. Input outside [0,1] or NaN is
    /// clamped, so a bad normalized time can never throw or return NaN into a transform.
    public static class Easing
    {
        private const float BackC1 = 1.70158f;
        private const float BackC3 = BackC1 + 1f;
        private const float BackC2 = BackC1 * 1.525f;
        private const float ElasticC4 = 2f * (float)System.Math.PI / 3f;
        private const float ElasticC5 = 2f * (float)System.Math.PI / 4.5f;
        private const float HalfPi = (float)System.Math.PI * 0.5f;

        public static float Evaluate(Ease ease, float t)
        {
            if (float.IsNaN(t) || t <= 0f) return 0f;
            if (t >= 1f) return 1f;
            switch (ease)
            {
                case Ease.InQuad:     return t * t;
                case Ease.OutQuad:    return 1f - (1f - t) * (1f - t);
                case Ease.InOutQuad:  return t < 0.5f ? 2f * t * t : 1f - Pow2(-2f * t + 2f) / 2f;
                case Ease.InCubic:    return t * t * t;
                case Ease.OutCubic:   { float u = 1f - t; return 1f - u * u * u; }
                case Ease.InOutCubic: return t < 0.5f ? 4f * t * t * t : 1f - Pow3(-2f * t + 2f) / 2f;
                case Ease.OutQuart:   { float u = 1f - t; return 1f - u * u * u * u; }
                case Ease.OutExpo:    return 1f - (float)System.Math.Pow(2.0, -10.0 * t);
                case Ease.InBack:     return BackC3 * t * t * t - BackC1 * t * t;
                case Ease.OutBack:    { float u = t - 1f; return 1f + BackC3 * u * u * u + BackC1 * u * u; }
                case Ease.OutElastic: return (float)(System.Math.Pow(2.0, -10.0 * t) * System.Math.Sin((t * 10f - 0.75f) * ElasticC4)) + 1f;
                case Ease.OutBounce:  return OutBounce(t);

                case Ease.InSine:     return 1f - (float)System.Math.Cos(t * HalfPi);
                case Ease.OutSine:    return (float)System.Math.Sin(t * HalfPi);
                case Ease.InOutSine:  return -((float)System.Math.Cos(System.Math.PI * t) - 1f) / 2f;
                case Ease.InQuart:    return t * t * t * t;
                case Ease.InOutQuart: return t < 0.5f ? 8f * t * t * t * t : 1f - Pow2(Pow2(-2f * t + 2f)) / 2f;
                case Ease.InQuint:    return t * t * t * t * t;
                case Ease.OutQuint:   { float u = 1f - t; return 1f - u * u * u * u * u; }
                case Ease.InOutQuint: return t < 0.5f ? 16f * t * t * t * t * t : 1f - Pow2(Pow2(-2f * t + 2f)) * (-2f * t + 2f) / 2f;
                case Ease.InExpo:     return (float)System.Math.Pow(2.0, 10.0 * t - 10.0);
                case Ease.InOutExpo:  return t < 0.5f ? (float)System.Math.Pow(2.0, 20.0 * t - 10.0) / 2f
                                                      : (2f - (float)System.Math.Pow(2.0, -20.0 * t + 10.0)) / 2f;
                case Ease.InCirc:     return 1f - (float)System.Math.Sqrt(1f - t * t);
                case Ease.OutCirc:    return (float)System.Math.Sqrt(1f - Pow2(t - 1f));
                case Ease.InOutCirc:  return t < 0.5f ? (1f - (float)System.Math.Sqrt(1f - Pow2(2f * t))) / 2f
                                                      : ((float)System.Math.Sqrt(1f - Pow2(-2f * t + 2f)) + 1f) / 2f;
                case Ease.InOutBack:  return t < 0.5f ? Pow2(2f * t) * ((BackC2 + 1f) * 2f * t - BackC2) / 2f
                                                      : (Pow2(2f * t - 2f) * ((BackC2 + 1f) * (t * 2f - 2f) + BackC2) + 2f) / 2f;
                case Ease.InElastic:  return -(float)(System.Math.Pow(2.0, 10.0 * t - 10.0) * System.Math.Sin((t * 10f - 10.75f) * ElasticC4));
                case Ease.InOutElastic:
                    return t < 0.5f
                        ? -(float)(System.Math.Pow(2.0, 20.0 * t - 10.0) * System.Math.Sin((20f * t - 11.125f) * ElasticC5)) / 2f
                        : (float)(System.Math.Pow(2.0, -20.0 * t + 10.0) * System.Math.Sin((20f * t - 11.125f) * ElasticC5)) / 2f + 1f;

                // TODO(human): InBounce and InOutBounce, built from OutBounce(t) below.
                // Until then they fall through to linear.

                default:              return t;
            }
        }

        private static float Pow2(float x) => x * x;
        private static float Pow3(float x) => x * x * x;

        private static float OutBounce(float t)
        {
            const float n1 = 7.5625f, d1 = 2.75f;
            if (t < 1f / d1) return n1 * t * t;
            if (t < 2f / d1) { t -= 1.5f / d1;  return n1 * t * t + 0.75f; }
            if (t < 2.5f / d1) { t -= 2.25f / d1; return n1 * t * t + 0.9375f; }
            t -= 2.625f / d1;
            return n1 * t * t + 0.984375f;
        }
    }
}
