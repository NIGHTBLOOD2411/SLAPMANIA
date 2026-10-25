namespace SlapMania.Core
{
    public readonly struct HealthChangedEvent
    {
        public readonly FighterSlot Fighter;
        public readonly int PreviousHp;
        public readonly int CurrentHp;
        public readonly int MaxHp;
        public readonly int Delta;
        public readonly bool IsDead;

        public HealthChangedEvent(FighterSlot fighter, int previousHp, int currentHp, int maxHp)
        {
            Fighter = fighter;
            PreviousHp = previousHp;
            CurrentHp = currentHp;
            MaxHp = maxHp;
            Delta = currentHp - previousHp;
            IsDead = currentHp <= 0;
        }
    }
}
