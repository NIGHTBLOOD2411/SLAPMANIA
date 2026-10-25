using UnityEngine;

namespace SlapMania.Data
{
    /// The in-bout HUD's attention budget (M10, POLISH §9): how much of the screen each phase may
    /// cover, how bright, and for how long, so the arena, the fighters and the camera stay the
    /// picture and the HUD stays the caption. Tune it from the Tuning Hub; Play Mode edits
    /// persist because this is an asset.
    ///
    /// Every alpha here is a CanvasGroup alpha: it multiplies the art the HUD builder baked.
    /// Solid washes (intro dim, K.O. tint) are baked at alpha 1, so their value is the final one;
    /// gradients name the resulting peak in the tooltip. Times are milliseconds (R8). Scales are
    /// against the V2 reference sizes (1 = the mockup).
    [CreateAssetMenu(fileName = "HudAttention", menuName = "SlapMania/HUD Attention")]
    public sealed class HudAttentionConfig : ScriptableObject
    {
        [Header("Rest · scorebug (C4/C5/D1/D2 top)")]
        [Tooltip("Scorebug alpha between hits: present, not shouting.")]
        [Range(0f, 1f)] public float scorebugRestAlpha = 0.75f;
        [Tooltip("After a hit the bar is at full alpha this long, then drops back to rest.")]
        [Min(0)] public int scorebugHitHoldMs = 600;
        [Tooltip("Top shade behind the bar (baked 0.92 -> 0 over 140 px).")]
        [Range(0f, 1f)] public float topShadeAlpha = 0.6f;
        [Tooltip("Blink the LIVE dot forever. Off: a looping blink in the corner pulls the eye off the fighters.")]
        public bool liveDotPulse = false;

        [Header("Intro · C4 countdown")]
        [Tooltip("Black wash over the arena while the intro camera plays (final alpha).")]
        [Range(0f, 1f)] public float introDimAlpha = 0.15f;
        [Tooltip("Countdown ring + digit, against the 260 px mockup ring (0.62 = 160 px).")]
        [Range(0.3f, 1f)] public float introRingScale = 0.62f;

        [Header("Charge · C6")]
        [Tooltip("Height of each cinematic letterbox bar, reference px (mockup 70).")]
        [Range(0f, 120f)] public float letterboxPx = 36f;
        [Tooltip("Red edge vignette (baked edge 0.85; 0.47 -> 0.4).")]
        [Range(0f, 1f)] public float chargeVignetteAlpha = 0.47f;
        [Tooltip("Power arc, against the 300 px mockup gauge (0.73 = 220 px).")]
        [Range(0.4f, 1f)] public float chargeGaugeScale = 0.73f;
        [Tooltip("TAP FAST! / HOLD! prompt, against the 64 pt mockup (0.625 = 40 pt).")]
        [Range(0.3f, 1f)] public float chargePromptScale = 0.625f;
        [Tooltip("After this many accepted taps the player has the idea: the prompt dims. 0 = never.")]
        [Min(0)] public int chargePromptFadeAfterTaps = 3;
        [Range(0f, 1f)] public float chargePromptFadedAlpha = 0.5f;
        [Tooltip("Speed lines between taps (baked 0.2; 0.3 -> 0.06).")]
        [Range(0f, 1f)] public float chargeRaysRestAlpha = 0.3f;
        [Tooltip("Speed lines on an accepted tap, decaying back to rest: motion that answers the finger. " +
                 "A real mash kicks every ~100 ms, so this is close to the level held while mashing (0.5 -> 0.1).")]
        [Range(0f, 1f)] public float chargeRaysKickAlpha = 0.5f;
        [Min(0)] public int chargeRaysKickMs = 120;
        [Tooltip("The filled red dot in the lock-on. Off: the opponent's face shows through the reticle.")]
        public bool chargeLockDot = false;

        [Header("Timing · C5 ring")]
        [Tooltip("YOUR TURN tag stays this long, then fades; the caption under it stays.")]
        [Min(0)] public int bannerHoldMs = 800;
        [Min(0)] public int bannerFadeMs = 200;
        [Tooltip("The locked power meter on the right. Off: the power was just shown on C6.")]
        public bool showPowerMeter = false;
        [Tooltip("Edge vignette and bottom shade (baked 0.45 / 0.95).")]
        [Range(0f, 1f)] public float timingShadeAlpha = 0.6f;

        [Header("Impact · D1 (peak, then out of the way)")]
        [Tooltip("SMASH! headline, against the 132 pt mockup (0.73 = 96 pt).")]
        [Range(0.3f, 1f)] public float impactHeadlineScale = 0.73f;
        [Tooltip("Headline at full alpha this long after the impact frame, then it fades.")]
        [Min(0)] public int impactHeadlineHoldMs = 450;
        [Min(0)] public int impactHeadlineFadeMs = 250;
        [Tooltip("Damage number, against the 96 pt mockup (0.75 = 72 pt).")]
        [Range(0.3f, 1f)] public float impactDamageScale = 0.75f;
        [Min(0)] public int impactDamageHoldMs = 800;
        [Min(0)] public int impactDamageFadeMs = 300;
        [Tooltip("The whole D1 screen leaves after this long.")]
        [Min(0)] public int impactAutoHideMs = 1100;
        [Tooltip("Speed lines at the impact frame (baked 0.24), fading to 0.")]
        [Range(0f, 1f)] public float impactRaysAlpha = 0.35f;
        [Min(0)] public int impactRaysMs = 350;
        [Tooltip("Red edge vignette (baked edge 0.6; 0.58 -> 0.35), fading with the headline.")]
        [Range(0f, 1f)] public float impactVignetteAlpha = 0.58f;
        [Tooltip("When the next turn starts while a headline is up, the headline leaves this fast " +
                 "and the next screen waits for it: one headline on screen at a time.")]
        [Min(0)] public int impactYieldMs = 150;

        [Header("Opponent turn · D2")]
        [Tooltip("Red radial + edge (baked edge 0.65; 0.46 -> 0.3).")]
        [Range(0f, 1f)] public float opponentVignetteAlpha = 0.46f;
        [Tooltip("Loop the vignette for the whole turn. Off: it is still, and pulses once on the BRACE! cue.")]
        public bool opponentVignettePulse = false;
        [Min(0)] public int braceCuePulseMs = 300;
        [Tooltip("INCOMING! tag stays this long, then fades; the 'winds up · power' caption stays.")]
        [Min(0)] public int incomingHoldMs = 1000;
        [Min(0)] public int incomingFadeMs = 250;

        [Header("Knock-out · D3")]
        [Tooltip("The finisher camera shot reads first; the K.O. slam lands this much later.")]
        [Min(0)] public int koSlamDelayMs = 350;
        [Tooltip("Red tint while the finisher plays out (final alpha).")]
        [Range(0f, 1f)] public float koTintAlpha = 0.25f;
        [Tooltip("Then the tint rises for the stats and CONTINUE to read on.")]
        [Min(0)] public int koTintHoldMs = 1200;
        [Range(0f, 1f)] public float koSettledTintAlpha = 0.5f;
        [Tooltip("Burst rays, hot spot and confetti behind the K.O.")]
        [Range(0f, 1f)] public float koBurstAlpha = 0.5f;
        [Tooltip("Winner line and stats wait for BoutEnd (CONTINUE) instead of piling onto the slam.")]
        public bool koStatsAtBoutEnd = true;

        public static float Seconds(int ms) => ms * 0.001f;

        /// The budget as rules (POLISH §9): an effect that breaks one is a design change, not a
        /// tweak. Checked by HudAttentionConfigTests against both the defaults and the real asset.
        public bool Validate(out string error)
        {
            // NaN passes every comparison below (and Range() only clamps in the Inspector).
            if (float.IsNaN(scorebugRestAlpha + topShadeAlpha + introDimAlpha + introRingScale + letterboxPx +
                            chargeVignetteAlpha + chargeGaugeScale + chargePromptScale + chargePromptFadedAlpha +
                            chargeRaysRestAlpha + chargeRaysKickAlpha + timingShadeAlpha + impactHeadlineScale +
                            impactDamageScale + impactRaysAlpha + impactVignetteAlpha + opponentVignetteAlpha +
                            koTintAlpha + koSettledTintAlpha + koBurstAlpha))
            { error = "a HUD attention value is NaN"; return false; }
            if (impactHeadlineHoldMs + impactHeadlineFadeMs > 1000)
            { error = "impact headline must be gone within 1000 ms (hold + fade)"; return false; }
            if (impactHeadlineHoldMs + impactHeadlineFadeMs > impactAutoHideMs)
            { error = "impact headline outlives the D1 screen (hold + fade > impactAutoHideMs)"; return false; }
            if (impactDamageHoldMs + impactDamageFadeMs > impactAutoHideMs + 100)
            { error = "damage number outlives the D1 screen"; return false; }
            if (impactYieldMs > 300)
            { error = "impactYieldMs over 300 ms delays the next turn's screen noticeably"; return false; }
            if (koSlamDelayMs > 1000)
            { error = "koSlamDelayMs over 1000 ms reads as a missed K.O."; return false; }
            if (chargeRaysKickAlpha < chargeRaysRestAlpha)
            { error = "chargeRaysKickAlpha must be >= chargeRaysRestAlpha (a kick is brighter than rest)"; return false; }
            if (koSettledTintAlpha < koTintAlpha)
            { error = "koSettledTintAlpha must be >= koTintAlpha"; return false; }
            error = null;
            return true;
        }
    }
}
