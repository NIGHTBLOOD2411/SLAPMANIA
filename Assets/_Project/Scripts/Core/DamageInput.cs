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

        public DamageInput(float rawPower, float multiplier, int slapIndex,
                           int defenderCurrentHp, int defenderMaxHp, float reduction)
        {
            RawPower = rawPower;
            Multiplier = multiplier;
            SlapIndex = slapIndex;
            DefenderCurrentHp = defenderCurrentHp;
            DefenderMaxHp = defenderMaxHp;
            Reduction = reduction;
        }
    }
}
