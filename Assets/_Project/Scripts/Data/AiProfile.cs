using UnityEngine;
using SlapMania.Core;

namespace SlapMania.Data
{
    [CreateAssetMenu(fileName = "AiProfile", menuName = "SlapMania/AI/AI Profile")]
    public sealed class AiProfile : ScriptableObject
    {
        [Tooltip("The ladder tier's name on the HUD (scorebug, opponent turn, results), e.g. ROOKIE (M07 §4b).")]
        public string displayName = "RIVAL";
        [Range(1f, 20f)] public float targetTps = 6.0f;
        [Range(0f, 1f)] public float tpsVariance = 0.15f;
        [Range(0f, 0.5f)] public float tapFatigue = 0.08f;
        public TimingZone timingAimZone = TimingZone.Good;
        [Range(0.005f, 0.30f)] public float aimSigma = 0.08f;
        [Range(0f, 0.5f)] public float reactionDelay = 0.12f;
        [Range(0f, 1f)] public float commitOnFirstPass = 0.6f;
    }
}
