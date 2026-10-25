using System;
using SlapMania.Core;
using SlapMania.Logging;

namespace SlapMania.Gameplay
{
    public sealed class HealthTracker
    {
        public FighterSlot Fighter { get; }
        public int MaxHp { get; private set; }
        public int CurrentHp { get; private set; }
        public float NormalizedHp => MaxHp <= 0 ? 0f : (float)CurrentHp / MaxHp;
        public bool IsDead => CurrentHp <= 0;

        public event Action<HealthChangedEvent> OnHealthChanged;

        public HealthTracker(FighterSlot slot) { Fighter = slot; }

        public void Init(int maxHp)
        {
            SlapAssert.That(maxHp > 0, "maxHp must be > 0");
            MaxHp = maxHp;
            CurrentHp = maxHp;
        }

        public void ApplyDamage(int amount)
        {
            SlapAssert.That(MaxHp > 0, "HealthTracker not initialised");
            if (amount < 0)
            {
                SlapLog.Error(LogChannel.Health, "Negative damage rejected: " + amount);
                return;
            }
            int prev = CurrentHp;
            CurrentHp = System.Math.Max(0, CurrentHp - amount);
            OnHealthChanged?.Invoke(new HealthChangedEvent(Fighter, prev, CurrentHp, MaxHp));
        }
    }
}
