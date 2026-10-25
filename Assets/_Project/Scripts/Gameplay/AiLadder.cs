using SlapMania.Core;
using SlapMania.Logging;

namespace SlapMania.Gameplay
{
    /// The AI ladder (M07 §4b, DEBUG S-095). Pure (R2): GameConfig.aiProfiles are difficulty
    /// tiers, easiest first. The player holds a ladder level - one tier index - that a win moves
    /// up and a loss moves down. Each bout rolls the opponent's tier near that level, so bouts
    /// vary without ever jumping far from what the player has earned.
    ///
    /// The level lives in GameRoot for the session (no save system until M12); a bout with no
    /// GameRoot plays level 0.
    public static class AiLadder
    {
        /// The tier this bout's opponent plays, in [0, tierCount). `level` is clamped first, so
        /// any int is safe; tierCount below 1 is treated as 1 (tier 0).
        public static int RollTier(int level, int tierCount, IRandom rng)
        {
            SlapAssert.NotNull(rng, nameof(rng));
            int top = tierCount > 1 ? tierCount - 1 : 0;
            int lvl = level < 0 ? 0 : level > top ? top : level;

            // TODO(human): pick the bout's tier around `lvl` using `rng`, then clamp it to [0, top].
            int tier = lvl;

            return tier < 0 ? 0 : tier > top ? top : tier;
        }

        /// The ladder level after a bout: one up on a win, one down on a loss (a forfeit is a
        /// loss), clamped to [0, tierCount).
        public static int NextLevel(int level, bool playerWon, int tierCount)
        {
            int top = tierCount > 1 ? tierCount - 1 : 0;
            long next = (long)level + (playerWon ? 1 : -1);
            return next < 0 ? 0 : next > top ? top : (int)next;
        }
    }
}
