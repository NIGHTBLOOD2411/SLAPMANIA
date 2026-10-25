namespace SlapMania.Core
{
    public readonly struct DamageResult
    {
        public readonly float RawDamage;
        public readonly int FinalDamage;
        public readonly bool IsCrit;
        public readonly bool IsLethal;
        public readonly bool IsFinisher;
        public readonly int Overkill;
        public readonly int HpAfter;

        public DamageResult(float rawDamage, int finalDamage, bool isCrit,
                            bool isLethal, bool isFinisher, int overkill, int hpAfter)
        {
            RawDamage = rawDamage;
            FinalDamage = finalDamage;
            IsCrit = isCrit;
            IsLethal = isLethal;
            IsFinisher = isFinisher;
            Overkill = overkill;
            HpAfter = hpAfter;
        }
    }
}
