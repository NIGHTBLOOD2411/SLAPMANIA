using UnityEngine;

namespace SlapMania.Data
{
    /// The arena's lighting and atmosphere (M15, ADR-0012) as an asset, so a Play Mode tuning
    /// pass from the Tuning Hub persists — scene light components would revert. ArenaDirector
    /// applies it on Awake and again whenever the asset changes in the Editor. Surface looks
    /// (crowd wash, beam colour, LED brightness) live on the arena materials; the colour grade
    /// lives on the Volume profile. Mobile Forward allows 4 additional lights per object:
    /// key + two rims + fill is exactly that budget.
    [CreateAssetMenu(fileName = "ArenaLook", menuName = "SlapMania/Arena/Arena Look")]
    public sealed class ArenaLookConfig : ScriptableObject
    {
        [Header("Key (spot straight down on the podium)")]
        [ColorUsage(false)] public Color keyColour = new Color(1f, 0.95f, 0.9f);
        [Tooltip("URP spot intensity. It falls off with distance squared: ~110 lights a face 7.5 m below.")]
        [Min(0f)] public float keyIntensity = 110f;
        [Range(5f, 120f)] public float keySpotAngle = 48f;

        [Header("Rims (behind the fighters)")]
        [ColorUsage(false)] public Color rimLeftColour = new Color(1f, 0.19f, 0.25f);
        [Min(0f)] public float rimLeftIntensity = 60f;
        [ColorUsage(false)] public Color rimRightColour = new Color(0.35f, 0.55f, 1f);
        [Min(0f)] public float rimRightIntensity = 55f;

        [Header("Fill (from the camera side, low)")]
        [ColorUsage(false)] public Color fillColour = new Color(0.72f, 0.77f, 1f);
        [Min(0f)] public float fillIntensity = 9f;

        [Header("Moon (directional, no shadows) - shape in the dark")]
        [ColorUsage(false)] public Color moonColour = new Color(0.5f, 0.56f, 0.75f);
        [Range(0f, 2f)] public float moonIntensity = 0.15f;

        [Header("Ambient (trilight)")]
        [ColorUsage(false, true)] public Color ambientSky = new Color(0.05f, 0.045f, 0.1f);
        [ColorUsage(false, true)] public Color ambientEquator = new Color(0.07f, 0.025f, 0.035f);
        [ColorUsage(false, true)] public Color ambientGround = new Color(0.03f, 0.022f, 0.028f);
        [Range(0f, 2f)] public float reflectionIntensity = 1f;

        [Header("Fog (exponential squared)")]
        [ColorUsage(false)] public Color fogColour = new Color(0.022f, 0.015f, 0.035f);
        [Range(0f, 0.1f)] public float fogDensity = 0.02f;

        [Header("Impact flash")]
        [Tooltip("Key light multiplier added at full flash (1 + boost).")]
        [Range(0f, 5f)] public float keyFlashBoost = 1.4f;
        [Tooltip("Beam and lamp-flare multiplier added at full flash.")]
        [Range(0f, 5f)] public float beamFlashBoost = 1.5f;
        [Tooltip("Flash decay per second (unscaled, so it reads during hitstop).")]
        [Range(0.5f, 20f)] public float flashDecayPerSecond = 6f;

        [Header("Beam sweep")]
        [Range(0f, 30f)] public float sweepDegrees = 8f;
        [Tooltip("One full sweep cycle, milliseconds (R8).")]
        [Range(500, 30000)] public int sweepPeriodMs = 7000;

        /// Bumped by OnValidate so ArenaDirector re-applies live edits without polling fields.
        public int Revision { get; private set; }

        private void OnValidate() => Revision++;
    }
}
