using UnityEngine;

namespace SlapMania.Data
{
    /// Tuning for the V2 hold-ring attack (M04 §5b, ADR-0014). Used when BoutConfig.attackMode is
    /// HoldRing. The round's speed and zone-scale curves, the zone layout and the latency
    /// compensation are M04's (TimingPhaseConfig, TimingZoneLayout); the power floor and ceiling
    /// are M03's (PowerPhaseConfig), so the two attack modes differ only in the verb.
    [CreateAssetMenu(fileName = "HoldRingConfig", menuName = "SlapMania/Balance/Hold Ring Config")]
    public sealed class HoldRingConfig : ScriptableObject
    {
        [Header("Ring (milliseconds, R8)")]
        [Tooltip("Press to fully closed at round speed 1.0, divided by TimingPhase.speedByRound. " +
                 "At 1000 every zone gets the window the M04 marker gives at 1 traversal/s (Perfect = 60 ms).")]
        [Min(200)] public int closeMs = 1000;
        [Tooltip("Presses ignored after the phase opens, so a stray tap from the previous screen cannot start the ring.")]
        [Min(0)] public int armDelayMs = 150;
        [Tooltip("No press by then: the slap resolves un-wound at the open ring (a Miss, timedOut). Never a hang.")]
        [Min(500)] public int pressTimeoutMs = 4000;

        [Header("Wind-up power")]
        [Tooltip("Ring closure at which the wind-up reaches full power; it then holds. 0.5 = the centre of the gold band.")]
        [Range(0.05f, 1f)] public float windUpPeakClosure = 0.5f;
        [Tooltip("Shape of the wind-up between the press and the peak. 1 = linear.")]
        [Range(0.25f, 4f)] public float windUpExponent = 1f;
    }
}
