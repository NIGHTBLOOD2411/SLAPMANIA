using UnityEngine;
using SlapMania.Core;
using SlapMania.Data;
using SlapMania.Logging;

namespace SlapMania.Gameplay
{
    public static class DamageResolver
    {
        public static DamageResult Resolve(in DamageInput input, DamageConfig cfg)
        {
            SlapAssert.NotNull(cfg, nameof(cfg));
            float power = Mathf.Clamp(input.RawPower, 0f, 2f);
            float mult = Mathf.Clamp(input.Multiplier, 0f, 10f);
            int slap = System.Math.Max(0, input.SlapIndex);
            float reduction = Mathf.Clamp01(input.Reduction);

            float escalation = Mathf.Pow(cfg.escalationBase, slap);
            float raw = cfg.baseSlapDamage * power * mult * escalation * (1f - reduction) * cfg.globalDamageScale;

            int rounded = Mathf.RoundToInt(raw);
            int floored = System.Math.Max(cfg.minDamageFloor, rounded);
            int final = System.Math.Min(floored, input.DefenderCurrentHp);

            bool isCrit = mult >= cfg.critThreshold;
            bool isLethal = final >= input.DefenderCurrentHp && input.DefenderCurrentHp > 0;
            bool isFinisher = isLethal && cfg.finisherEnabled &&
                              (cfg.finisherHpThreshold <= 0f ||
                               (float)input.DefenderCurrentHp / input.DefenderMaxHp <= cfg.finisherHpThreshold);
            int overkill = isLethal ? floored - input.DefenderCurrentHp : 0;
            int hpAfter = System.Math.Max(0, input.DefenderCurrentHp - final);

            return new DamageResult(raw, final, isCrit, isLethal, isFinisher, overkill, hpAfter);
        }

        public static void Commit(in DamageResult result, HealthTracker target)
        {
            SlapAssert.NotNull(target, nameof(target));
            target.ApplyDamage(result.FinalDamage);
        }
    }
}
