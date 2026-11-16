using System;

namespace SlapMania.Core
{
    /// A champion's six roster-card stats (M08, ADR-0022), 0..99 like the V2 cards. Three of
    /// them change the bout through ChampionRules: power (damage dealt), chin (damage taken)
    /// and stamina (max health). Speed, technique and rage are shown on the card only, for now.
    ///
    /// Mutable public fields so a ChampionDefinition asset can serialize it; treat a copy as a
    /// value.
    [Serializable]
    public struct ChampionStats
    {
        public int power;
        public int speed;
        public int chin;
        public int technique;
        public int rage;
        public int stamina;

        /// Every stat at ChampionRules.StatNeutral: the bout plays exactly as it did before
        /// champions existed.
        public static ChampionStats Neutral => new ChampionStats
        {
            power = ChampionRules.StatNeutral, speed = ChampionRules.StatNeutral,
            chin = ChampionRules.StatNeutral, technique = ChampionRules.StatNeutral,
            rage = ChampionRules.StatNeutral, stamina = ChampionRules.StatNeutral,
        };

        /// The card's big number: the mean of the six stats, rounded.
        public int Overall => (int)Math.Round((power + speed + chin + technique + rage + stamina) / 6.0,
                                              MidpointRounding.AwayFromZero);
    }
}
