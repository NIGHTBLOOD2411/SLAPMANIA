using UnityEngine;

namespace SlapMania.Data
{
    /// Front-end motion (M10, ADR-0013). The V2 mockups are static frames, so every duration
    /// here is a design choice, not a transcription - tune it in the Tuning Hub. All times
    /// are milliseconds (R8). Play Mode edits persist because this is an asset.
    [CreateAssetMenu(fileName = "UiMotion", menuName = "SlapMania/UI Motion")]
    public sealed class UiMotionConfig : ScriptableObject
    {
        [Header("Screen transitions")]
        [Tooltip("Fade + slide in of a pushed screen.")]
        [Min(0)] public int screenInMs = 240;
        [Min(0)] public int screenOutMs = 160;
        [Tooltip("How far a screen slides up while it fades in, in reference px (430 x 932 frame).")]
        [Min(0f)] public float screenSlidePx = 24f;
        [Tooltip("Delay between consecutive blocks entering a screen.")]
        [Min(0)] public int listStaggerMs = 40;
        [Tooltip("Blocks after this many enter together (a long list does not crawl in).")]
        [Min(1)] public int listStaggerMax = 10;
        [Min(0f)] public float staggerSlidePx = 12f;

        [Header("Widgets")]
        [Tooltip("Tab underline and switch knob slide (USS transitions read this at bind).")]
        [Min(0)] public int tabSwitchMs = 180;
        [Tooltip("Press squash of links and buttons.")]
        [Min(0)] public int buttonPressMs = 90;
        [Range(0.8f, 1f)] public float buttonPressScale = 0.96f;
        [Tooltip("Progress bars and meters fill from zero when their screen opens.")]
        [Min(0)] public int countUpMs = 600;

        [Header("Ambient loops")]
        [Tooltip("One full turn of the light rays behind packs, wins and titles.")]
        [Min(0.5f)] public float raysSpinSeconds = 20f;
        [Tooltip("LIVE dot and 'tap to start' blink period.")]
        [Min(50)] public int livePulseMs = 900;
        [Tooltip("Title spotlights: how long each level of the blink pattern is held.")]
        [Min(16)] public int titleBeamStepMs = 110;
        [Tooltip("Title spotlights: brightness per step (0..1), looped. Default: triple pulse, hold, flicker.")]
        public float[] titleBeamPattern = { 1f, 0.2f, 1f, 0.2f, 1f, 1f, 1f, 1f, 0.15f, 0.6f, 0.1f, 0.6f, 0.35f, 0.35f };
        [Tooltip("Title spotlights: the second beam runs this far behind the first, so they trade off.")]
        [Min(0)] public int titleBeamOffsetMs = 440;
        [Tooltip("Bottom news ticker speed.")]
        [Min(1f)] public float tickerPxPerSecond = 40f;

        [Header("Sequences")]
        [Min(0)] public int loaderMs = 2200;
        [Tooltip("Matchmaking search before the opponent is found (placeholder until online play).")]
        [Min(0)] public int searchMs = 3200;
        [Tooltip("Walkout auto-advances to the tale of the tape after this long.")]
        [Min(0)] public int walkoutMs = 4500;
        [Tooltip("Tale of the tape auto-starts the bout after this long.")]
        [Min(0)] public int tapeAutoStartMs = 8000;
        [Min(0)] public int packRevealMs = 1400;
        [Min(0)] public int rankUpMs = 1200;

        public bool Validate(out string error)
        {
            if (screenInMs > 2000 || screenOutMs > 2000) { error = "screen transitions over 2 s feel broken"; return false; }
            if (raysSpinSeconds <= 0f) { error = "raysSpinSeconds must be > 0"; return false; }
            error = null;
            return true;
        }
    }
}
