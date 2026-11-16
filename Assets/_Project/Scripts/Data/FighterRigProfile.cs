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
        [Tooltip("When a slap's contact comes later than the bout's windup (a long mocap anticipation), the clip may " +
                 "play up to this much faster before its start is skipped. 1 = always skip.")]
        [Range(1f, 2f)] public float maxWindupSpeedup = 1.35f;
        [Tooltip("Crossfade into the Charge wind-up: a captured load starts turned away, so it needs longer than crossFadeMs.")]
        [Min(0)] public int chargeCrossFadeMs = 350;

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
        [Tooltip("How far the slap lands to the striking hand's side of the face (metres): the cheek, not the nose.")]
        [Range(0f, 0.2f)] public float cheekSideOffset = 0.08f;
        [Tooltip("Most the body may pitch toward the cheek (degrees) when it is beyond arm's reach. Feet stay planted by IK. 0 = never.")]
        [Range(0f, 45f)] public float maxStrikeLeanDeg = 32f;
        [Tooltip("While planted (charge, slap, hit) the feet stay on their idle stance by IK: how much of a captured whole-body turn is kept. 1 = all, 0 = hips stay square.")]
        [Range(0f, 1f)] public float plantedYawKeep = 0.45f;
        [Min(1)] public int plantBlendMs = 200;
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

        [Header("Physical animation (spring layer over the clip, ADR-0024)")]
        [Tooltip("0 = the clip exactly; 1 = every upper-body bone chases the clip through its spring.")]
        [Range(0f, 1f)] public float physicsWeight = 1f;
        [Tooltip("Spine and chest: stiff, so the body leads and the arm trails.")]
        [Range(1f, 15f)] public float torsoHz = 7f;
        [Range(1f, 15f)] public float neckHeadHz = 4.5f;
        [Tooltip("Shoulder to hand get softer down the chain: the lag compounds into the whip.")]
        [Range(1f, 15f)] public float upperArmHz = 5f;
        [Range(1f, 15f)] public float forearmHz = 4f;
        [Range(1f, 15f)] public float handHz = 3.2f;
        [Tooltip("Below 1 the bones overshoot and settle: follow-through. 1 = no overshoot.")]
        [Range(0.1f, 1.5f)] public float dampingRatio = 0.45f;
        [Tooltip("No simulated bone strays further than this from its animated rotation.")]
        [Range(5f, 90f)] public float maxDeviationDeg = 50f;
        [Tooltip("Head angular kick (degrees/second) at full slap intensity; the neck, chest and arms get a share.")]
        [Min(0f)] public float hitImpulseDegPerSecond = 1100f;
        [Tooltip("Share of the kick that turns the face away (vs tipping it over).")]
        [Range(0f, 1f)] public float hitTwistShare = 0.6f;
        [Tooltip("Muscle tremble while charging (degrees of spring deviation): the held wind-up strains instead of freezing.")]
        [Range(0f, 10f)] public float chargeTrembleDeg = 2.5f;

        [Header("Knock-out (ragdoll)")]
        [Tooltip("Body velocity along the slap at the moment of the KO (m/s).")]
        [Min(0f)] public float koVelocity = 3.2f;
        [Min(0f)] public float koUpVelocity = 1.1f;
        [Tooltip("Extra head velocity so the head leads the fall (m/s).")]
        [Min(0f)] public float koHeadExtra = 2.6f;
    }
}
