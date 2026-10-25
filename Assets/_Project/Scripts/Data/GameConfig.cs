using UnityEngine;

namespace SlapMania.Data
{
    /// Aggregate config and the single load point (M00): GameRoot loads this once from
    /// Resources and everything reads from its fields. No other system calls Resources.Load.
    /// Holds a reference to every per-module config asset that exists today; the Snapshot,
    /// Audio and Camera configs join when those modules land.
    [CreateAssetMenu(fileName = "GameConfig", menuName = "SlapMania/Game Config")]
    public sealed class GameConfig : ScriptableObject
    {
        [Header("Bout")]
        public BoutConfig bout;

        [Header("Slap phases")]
        public PowerPhaseConfig powerPhase;
        public TimingPhaseConfig timingPhase;
        public TimingZoneLayout timingZoneLayout;
        public DamageConfig damage;
        [Tooltip("The V2 hold-ring attack (ADR-0014). Required when bout.attackMode is HoldRing.")]
        public HoldRingConfig holdRing;

        [Header("Opponent AI ladder (M07 §4b): difficulty tiers, easiest first")]
        [Tooltip("Each bout rolls one tier near the player's ladder level (AiLadder). A win moves the level up, a loss down.")]
        public AiProfile[] aiProfiles;

        [Header("Feel (presentation)")]
        public FeelConfig feel;

        [Header("In-bout HUD (M10). Optional: without it the HUD uses the built-in budget.")]
        [Tooltip("The HUD attention budget (POLISH §9): coverage, alpha and lifetime per phase.")]
        public HudAttentionConfig hudAttention;

        [Header("Front end (M10). Optional: a bout runs without it.")]
        public UiMotionConfig uiMotion;
        [Tooltip("Which Design-canvas page the menus show. V1 is the earlier design, kept playable for comparison.")]
        public FrontEndPage frontEndPage = FrontEndPage.V2;

        /// True when every reference needed to run a bout is wired. Cheap enough to call at boot.
        public bool Validate(out string error)
        {
            if (bout == null)             { error = "bout is null"; return false; }
            if (powerPhase == null)       { error = "powerPhase is null"; return false; }
            if (timingPhase == null)      { error = "timingPhase is null"; return false; }
            if (timingZoneLayout == null) { error = "timingZoneLayout is null"; return false; }
            if (damage == null)           { error = "damage is null"; return false; }
            if (holdRing == null && bout.attackMode == Core.AttackMode.HoldRing)
                                          { error = "holdRing is null but bout.attackMode is HoldRing"; return false; }
            if (aiProfiles == null || aiProfiles.Length == 0) { error = "aiProfiles is empty"; return false; }
            if (feel == null)             { error = "feel is null"; return false; }
            if (hudAttention != null && !hudAttention.Validate(out string hudErr))
                                          { error = "hudAttention: " + hudErr; return false; }
            error = null;
            return true;
        }

        /// Tier is 0-based, easiest first (the AI ladder, M07 §4b). Clamped to the available profiles.
        public AiProfile ProfileForTier(int tier)
        {
            if (aiProfiles == null || aiProfiles.Length == 0) return null;
            int idx = Mathf.Clamp(tier, 0, aiProfiles.Length - 1);
            return aiProfiles[idx];
        }
    }
}
