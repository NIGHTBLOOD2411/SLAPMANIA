using System;

namespace SlapMania.Core
{
    /// How a champion's stats change a bout (M08, ADR-0022). Pure: the per-point sensitivities
    /// arrive as parameters from DamageConfig / BoutConfig (R2). A stat at StatNeutral is a
    /// scale of exactly 1, so a roster of neutral champions plays like the pre-champion game.
    public static class ChampionRules
    {
        public const int StatMin = 0;
        public const int StatMax = 99;
        public const int StatNeutral = 75;

        /// The bounds every stat scale is held inside, whatever the sensitivity: a mistyped
        /// tuning value can make a champion strong, never invulnerable or one-shot.
        public const float MinScale = 0.25f;
        public const float MaxScale = 4f;

        /// The multiplier a stat applies. `perPoint` is how much one point away from
        /// StatNeutral moves it (0.004 = 0.4% per point); a negative perPoint inverts it, so a
        /// higher stat gives a smaller number (chin: damage taken).
        public static float StatScale(int stat, float perPoint)
        {
            if (float.IsNaN(perPoint) || float.IsInfinity(perPoint)) return 1f;
            int s = Clamp(stat, StatMin, StatMax);
            // Linear: easy to reason about when tuning; 99 at 0.004 is x1.096, 50 is x0.9.
            double v = 1.0 + (s - StatNeutral) * (double)perPoint;
            return (float)Math.Max(MinScale, Math.Min(MaxScale, v));
        }

        /// Damage the attacker deals, from its power stat.
        public static float DamageDealtScale(int power, float perPoint) => StatScale(power, perPoint);

        /// Damage the defender takes, from its chin stat: a higher chin takes less.
        public static float DamageTakenScale(int chin, float perPoint) => StatScale(chin, -perPoint);

        /// One slap's combined stat scale: attacker's power times defender's chin.
        public static float SlapScale(in ChampionStats attacker, in ChampionStats defender,
                                      float powerPerPoint, float chinPerPoint)
        {
            return DamageDealtScale(attacker.power, powerPerPoint) * DamageTakenScale(defender.chin, chinPerPoint);
        }

        /// Starting (and max) health from the stamina stat: baseHp plus hpPerPoint for every
        /// point above neutral (minus below it). Never below 1.
        public static int MaxHp(int baseHp, int stamina, float hpPerPoint)
        {
            if (float.IsNaN(hpPerPoint) || float.IsInfinity(hpPerPoint)) hpPerPoint = 0f;
            int s = Clamp(stamina, StatMin, StatMax);
            double hp = baseHp + (s - StatNeutral) * (double)hpPerPoint;
            return (int)Math.Max(1, Math.Round(hp, MidpointRounding.AwayFromZero));
        }

        /// The opponent's roster index: uniformly any champion except the player's. A roster of
        /// one is a mirror match (0); an empty roster is -1. One rng draw whenever count > 1.
        public static int PickOpponent(int count, int playerIndex, IRandom rng)
        {
            if (count <= 0) return -1;
            if (count == 1) return 0;
            if (playerIndex < 0 || playerIndex >= count) return rng.NextInt(0, count);
            int r = rng.NextInt(0, count - 1);
            return r >= playerIndex ? r + 1 : r;
        }

        private static int Clamp(int v, int lo, int hi) => v < lo ? lo : (v > hi ? hi : v);
    }
}
