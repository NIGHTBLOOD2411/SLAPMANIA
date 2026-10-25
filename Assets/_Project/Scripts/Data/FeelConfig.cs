using UnityEngine;

namespace SlapMania.Data
{
    /// Presentation feel knobs (M08 hitstop/recoil, M10 callouts). Not rules - nothing here
    /// changes damage or turn order - but every value is felt on every slap, so it lives in an
    /// asset: Play Mode edits persist, and the tuning pass needs zero recompiles (ROADMAP P10).
    /// Timing is authored in milliseconds (R8).
    [CreateAssetMenu(fileName = "FeelConfig", menuName = "SlapMania/Feel/Feel Config")]
    public sealed class FeelConfig : ScriptableObject
    {
        [Header("Hitstop (visual freeze, milliseconds)")]
        [Tooltip("Wall-clock timeScale dip on every impact. The FSM's own Hitstop state is BoutConfig.hitstopMs.")]
        [Range(0, 300)] public int hitstopMs = 90;
        [Tooltip("Added on a crit (multiplier >= DamageConfig.critThreshold).")]
        [Range(0, 200)] public int critHitstopBonusMs = 40;
        [Tooltip("Added on the finisher slap.")]
        [Range(0, 400)] public int finisherHitstopBonusMs = 120;

        [Header("Recoil")]
        [Tooltip("Damage that produces a full-strength defender recoil. Lower makes every hit look huge.")]
        [Min(1f)] public float recoilDamageForFull = 45f;

        [Header("Callouts (HUD text pops)")]
        [Tooltip("Scale a callout starts at before settling to 1.")]
        [Range(1f, 3f)] public float calloutPunchScale = 1.35f;
        [Range(0, 600)] public int calloutPunchMs = 140;
        [Tooltip("How long a zone callout (PERFECT x3.0) stays fully visible.")]
        [Range(100, 3000)] public int calloutHoldMs = 700;
        [Range(0, 1500)] public int calloutFadeMs = 250;
        [Tooltip("How long a damage number stays fully visible.")]
        [Range(100, 3000)] public int damageHoldMs = 900;
    }
}
