using System;
using UnityEngine;

namespace SlapMania.Data
{
    /// Everything that makes a humanoid fighter move and react (M08): Animator state names,
    /// attack moves with their measured contact times, strike / look-at IK, the procedural
    /// head-snap spring, and the knock-out ragdoll launch. One asset per fighter rig; the
    /// Tuning Hub shows it inline. Timings are milliseconds (R8).
    [CreateAssetMenu(fileName = "FighterRigProfile", menuName = "SlapMania/Fighters/Fighter Rig Profile")]
    public sealed class FighterRigProfile : ScriptableObject
    {
        [Serializable]
        public struct AttackMove
        {
            [Tooltip("Animator state to play.")]
            public string state;
            [Tooltip("Where in the clip the striking hand is furthest forward — the contact. Measured by the fighter builder.")]
            [Min(0)] public int contactMs;
            [Tooltip("Striking hand; the IK pins this hand to the opponent's cheek.")]
            public bool rightHand;
        }

        [Header("Animator states")]
        public string idleState = "Idle";
        public string chargeState = "Charge";
        public string hitState = "Hit";
        public string blockState = "Block";
        public string victoryState = "Victory";
        [Min(0)] public int crossFadeMs = 120;
        [Tooltip("Played in order, one per slap, wrapping around.")]
        public AttackMove[] attacks = new AttackMove[0];

        [Header("Look-at IK (fighters watch each other)")]
        [Range(0f, 1f)] public float lookWeight = 0.75f;
        [Range(0f, 1f)] public float lookBodyWeight = 0.15f;
        [Range(0f, 1f)] public float lookHeadWeight = 0.9f;
        [Range(0f, 1f)] public float lookClamp = 0.5f;

        [Header("Strike IK (the hand lands on the cheek)")]
        [Min(0)] public int ikBlendInMs = 170;
        [Min(0)] public int ikHoldMs = 90;
        [Min(0)] public int ikBlendOutMs = 240;
        [Range(0f, 1f)] public float ikPositionWeight = 1f;
        [Range(0f, 1f)] public float ikRotationWeight = 0.45f;
        [Tooltip("Extra hand rotation (degrees) so the palm, not the knuckles, meets the face.")]
        public Vector3 handRotationOffset = new Vector3(0f, 0f, 90f);
        [Tooltip("Cheek point relative to the head bone: x = towards the attacker, y = up (metres).")]
        public Vector2 strikeOffset = new Vector2(0.11f, -0.03f);
        [Tooltip("How far the striking hand's fingers straighten into an open-hand slap.")]
        [Range(0f, 1f)] public float openHandWeight = 1f;

        [Header("Hit reaction (spring on spine, neck, head)")]
        [Range(0.5f, 15f)] public float springFrequency = 5.5f;
        [Range(0.05f, 1f)] public float springDamping = 0.35f;
        [Tooltip("Angular kick in degrees/second at full intensity.")]
        [Min(0f)] public float impulseDegPerSecond = 950f;
        [Range(5f, 90f)] public float maxAngle = 55f;
        [Tooltip("Share of the kick that turns the head away (vs tipping it over).")]
        [Range(0f, 1f)] public float twistShare = 0.55f;
        [Range(0f, 1f)] public float spineWeight = 0.08f;
        [Range(0f, 1f)] public float chestWeight = 0.18f;
        [Range(0f, 1f)] public float neckWeight = 0.3f;
        [Range(0f, 1f)] public float headWeight = 0.7f;

        [Header("Knock-out (ragdoll)")]
        [Tooltip("Body velocity along the slap at the moment of the KO (m/s).")]
        [Min(0f)] public float koVelocity = 3.2f;
        [Min(0f)] public float koUpVelocity = 1.1f;
        [Tooltip("Extra head velocity so the head leads the fall (m/s).")]
        [Min(0f)] public float koHeadExtra = 2.6f;
    }
}
