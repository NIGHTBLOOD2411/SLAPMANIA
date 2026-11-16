namespace SlapMania.Core
{
    public readonly struct DamageInput
    {
        public readonly float RawPower;
        public readonly float Multiplier;
        public readonly int SlapIndex;
        public readonly int DefenderCurrentHp;
        public readonly int DefenderMaxHp;
        public readonly float Reduction;
        /// The champions' stat scale for this slap (ChampionRules.SlapScale): attacker power x
        /// defender chin. 1 = no champion effect.
        public readonly float StatScale;

        public DamageInput(float rawPower, float multiplier, int slapIndex,
                           int defenderCurrentHp, int defenderMaxHp, float reduction, float statScale = 1f)
        {
            RawPower = rawPower;
            Multiplier = multiplier;
            SlapIndex = slapIndex;
            DefenderCurrentHp = defenderCurrentHp;
            DefenderMaxHp = defenderMaxHp;
            Reduction = reduction;
            StatScale = statScale;
        }
    }
}
