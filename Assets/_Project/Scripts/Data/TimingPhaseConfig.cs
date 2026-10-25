using UnityEngine;

namespace SlapMania.Data
{
    [CreateAssetMenu(fileName = "TimingPhaseConfig", menuName = "SlapMania/Balance/Timing Phase Config")]
    public sealed class TimingPhaseConfig : ScriptableObject
    {
        [Header("Marker")]
        [Min(0.1f)] public float baseTraversalsPerSecond = 1.0f;
        public AnimationCurve speedByRound = new AnimationCurve(
            new Keyframe(1f, 0.85f),
            new Keyframe(5f, 1.15f),
            new Keyframe(10f, 1.45f));
        public AnimationCurve zoneScaleByRound = new AnimationCurve(
            new Keyframe(1f, 1.0f),
            new Keyframe(5f, 1.0f),
            new Keyframe(10f, 0.70f));

        [Header("Windows (seconds)")]
        [Min(0f)] public float armDelay = 0.15f;
        [Min(0.1f)] public float timeout = 3.0f;
        [Range(0f, 0.150f)] public float displayLatencyBias = 0.030f;
        [Range(0.020f, 0.200f)] public float maxLatencyComp = 0.120f;

        // Mash carry-over guards (DEBUG S-093). Authored in ms (R8).
        [Header("Mash carry-over (milliseconds)")]
        [Tooltip("AIM cue: the ring is on screen but frozen before the marker moves, and no tap can commit. " +
                 "Gives a masher time to see the phase change and stop.")]
        [Range(0, 1500)] public int aimCueMs = 400;
        [Tooltip("A commit counts only if the press before it (charge taps included) was at least this long ago, " +
                 "so a finger still mashing never stops the marker. ~220 ms rejects mashing at 4.5 taps/s and up.")]
        [Range(0, 600)] public int spamGapMs = 220;

        [Header("Start phase guard")]
        [Range(0.01f, 0.20f)] public float startPhaseGuard = 0.05f;
        [Range(0f, 1f)] public float perfectCentre = 0.50f;
    }
}
