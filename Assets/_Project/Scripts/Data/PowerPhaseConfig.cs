using UnityEngine;

namespace SlapMania.Data
{
    [CreateAssetMenu(fileName = "PowerPhaseConfig", menuName = "SlapMania/Balance/Power Phase Config")]
    public sealed class PowerPhaseConfig : ScriptableObject
    {
        [Header("Duration")]
        [Min(0.1f)] public float phaseDuration = 2.50f;

        [Header("Impulse")]
        [Min(0f)] public float impulsePerTap = 0.072f;
        [Min(0f)] public float decayPerSecond = 0.12f;
        public float maxFill = 1.0f;

        [Header("Rate gating")]
        [Min(0.001f)] public float minTapInterval = 0.045f;
        [Min(0.05f)] public float diminishWindow = 0.30f;
        [Min(1)] public int diminishSoftTaps = 3;
        [Min(1)] public int diminishHardTaps = 6;
        [Range(0.1f, 1f)] public float diminishFloor = 0.50f;

        [Header("Shape")]
        public AnimationCurve phaseShape = new AnimationCurve(
            new Keyframe(0f, 1.0f),
            new Keyframe(0.6f, 1.0f),
            new Keyframe(1.0f, 1.6f));
        public AnimationCurve feedbackCurve = new AnimationCurve(
            new Keyframe(0f, 0f),
            new Keyframe(1f, 1f));

        [Header("Power mapping")]
        [Range(0.01f, 0.5f)] public float minPower = 0.25f;
        public float maxPower = 1.0f;

        [Header("Overcharge")]
        [Min(0f)] public float overchargeRatePerTap = 0.09f;
        [Range(0f, 0.5f)] public float overchargeGain = 0.12f;
        [Range(0f, 0.5f)] public float overchargeSpeedPenalty = 0.0f;

        private void OnValidate()
        {
            if (minPower <= 0f) minPower = 0.01f;
            if (diminishHardTaps <= diminishSoftTaps) diminishHardTaps = diminishSoftTaps + 1;
        }
    }
}
