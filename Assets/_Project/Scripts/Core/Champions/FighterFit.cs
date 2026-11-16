namespace SlapMania.Core
{
    /// Fitting champions of any modelled size to one slap table (M08, ADR-0023). Pure (R2).
    ///
    /// A humanoid Avatar's humanScale is its hips height relative to Mecanim's reference
    /// human, so it is a body-size measure that does not depend on the pose or the model's
    /// import units. Scaling every champion to the same humanScale puts every head, cheek and
    /// shoulder at the same height, so the table, the fighter marks and the camera are authored
    /// once for every matchup.
    public static class FighterFit
    {
        /// The scale is held inside these bounds: a broken avatar can make a fighter small or
        /// big, never microscopic or a giant that swallows the arena.
        public const float MinScale = 0.5f;
        public const float MaxScale = 2f;

        /// The uniform scale that turns a rig of `humanScale` into one of `targetHumanScale`.
        /// 1 (leave it alone) when either is unusable: zero, negative, NaN or infinite.
        public static float UniformScale(float humanScale, float targetHumanScale)
        {
            if (!Usable(humanScale) || !Usable(targetHumanScale)) return 1f;
            float s = targetHumanScale / humanScale;
            return s < MinScale ? MinScale : (s > MaxScale ? MaxScale : s);
        }

        private static bool Usable(float v) => v > 0f && !float.IsNaN(v) && !float.IsInfinity(v);
    }
}
