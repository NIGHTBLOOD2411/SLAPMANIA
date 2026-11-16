using UnityEngine;

namespace SlapMania.Data
{
    [CreateAssetMenu(fileName = "DamageConfig", menuName = "SlapMania/Balance/Damage Config")]
    public sealed class DamageConfig : ScriptableObject
    {
        [Min(1f)] public float baseSlapDamage = 22f;
        [Min(1f)] public float escalationBase = 1.06f;
        [Min(1)] public int minDamageFloor = 3;
        [Range(1f, 4f)] public float critThreshold = 2.00f;
        public bool finisherEnabled = true;
        [Range(0f, 1f)] public float finisherHpThreshold = 0.0f;
        [Range(0.1f, 3f)] public float globalDamageScale = 1.0f;

        // Champion stats (ADR-0022): each point away from 75 moves the slap by this fraction.
        // 0.004 puts a 99 at x1.096 and a 50 at x0.9. 0 switches the stat off.
        [Header("Champion stats (per point from 75)")]
        [Tooltip("Attacker's PWR: damage dealt changes by this fraction per point from 75.")]
        [Range(0f, 0.02f)] public float powerPerPoint = 0.004f;
        [Tooltip("Defender's CHN: damage taken drops by this fraction per point above 75.")]
        [Range(0f, 0.02f)] public float chinPerPoint = 0.004f;

        // The player's defence (ADR-0019): one tap as the rival's hand comes in. Windows are the
        // distance from the brace target (impact - braceLeadMs), either side, in ms (R8). They must fit inside the
        // bout's windupMs (500), which is when the tap is listened for.
        [Header("Brace (milliseconds)")]
        [Tooltip("The player can brace during the rival's wind-up.")]
        public bool braceEnabled = true;
        [Tooltip("The brace target sits this long before the hand lands: you raise your guard as it comes in. " +
                 "It must be at least displayLatencyBias + bracePerfectMs, or a tap timed to what the player sees " +
                 "arrives after the impact has been committed and counts as no brace.")]
        [Range(0, 300)] public int braceLeadMs = 100;
        [Tooltip("|tap - target| at or under this: Perfect brace.")]
        [Range(20, 200)] public int bracePerfectMs = 70;
        [Tooltip("|tap - target| at or under this: Good brace. Further away: a flinch, no reduction.")]
        [Range(50, 400)] public int braceGoodMs = 160;
        [Tooltip("Damage taken is cut by this fraction on a Perfect brace (the D2 mockup: 'blocks up to 50%').")]
        [Range(0f, 0.9f)] public float bracePerfectReduction = 0.50f;
        [Range(0f, 0.9f)] public float braceGoodReduction = 0.25f;
    }
}
