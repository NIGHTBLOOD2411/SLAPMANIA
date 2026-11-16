using UnityEngine;
using SlapMania.Core.Motion;
using SlapMania.Data;

namespace SlapMania.Presentation
{
    /// Physical animation (M08, ADR-0024): the clip is the *intent*, the body is a set of
    /// springs that chase it. After the Animator (and SlapIK) pose the skeleton each frame,
    /// every upper-body bone's local rotation is pulled toward that pose through a damped
    /// spring (Core RotationSpring), so:
    ///
    ///   * the slapping arm whips - shoulder, elbow, wrist and hand each lag a little more than
    ///     the bone before (softer springs down the chain), and overshoot on the follow-through;
    ///   * the torso carries momentum through the anticipation and settles after the swing;
    ///   * a slap kicks the slapped fighter's head, neck, chest and arms with an angular impulse
    ///     at the cheek, and they wobble back - replacing HitReactor's single spring.
    ///
    /// The striking arm is pinned to its IK pose in proportion to SlapIK.StrikeWeight, so the
    /// palm still lands on the cheek on the impact frame. Scaled time: hitstop freezes the
    /// springs with everything else. Legs are not simulated (the feet stay planted). Suspended
    /// while the ragdoll owns the bones. Fixed arrays, no allocation per frame (R5).
    [RequireComponent(typeof(Animator))]
    [DefaultExecutionOrder(50)]   // after SlapIK's LateUpdate (the open hand), before rendering
    public sealed class PhysicalAnimationLayer : MonoBehaviour
    {
        private enum Group : byte { Torso, NeckHead, UpperArm, Forearm, Hand }

        private struct BoneDef
        {
            public HumanBodyBones Bone;
            public Group Group;
            public float HitShare;   // share of the head's kick this bone takes
            public sbyte Side;       // -1 left arm, +1 right arm, 0 the trunk
            public BoneDef(HumanBodyBones b, Group g, float share, sbyte side) { Bone = b; Group = g; HitShare = share; Side = side; }
        }

        private static readonly BoneDef[] Defs =
        {
            new BoneDef(HumanBodyBones.Spine,         Group.Torso,    0.12f, 0),
            new BoneDef(HumanBodyBones.Chest,         Group.Torso,    0.22f, 0),
            new BoneDef(HumanBodyBones.UpperChest,    Group.Torso,    0.32f, 0),
            new BoneDef(HumanBodyBones.Neck,          Group.NeckHead, 0.65f, 0),
            new BoneDef(HumanBodyBones.Head,          Group.NeckHead, 1.00f, 0),
            new BoneDef(HumanBodyBones.LeftShoulder,  Group.UpperArm, 0.25f, -1),
            new BoneDef(HumanBodyBones.LeftUpperArm,  Group.UpperArm, 0.45f, -1),
            new BoneDef(HumanBodyBones.LeftLowerArm,  Group.Forearm,  0.40f, -1),
            new BoneDef(HumanBodyBones.LeftHand,      Group.Hand,     0.35f, -1),
            new BoneDef(HumanBodyBones.RightShoulder, Group.UpperArm, 0.25f, 1),
            new BoneDef(HumanBodyBones.RightUpperArm, Group.UpperArm, 0.45f, 1),
            new BoneDef(HumanBodyBones.RightLowerArm, Group.Forearm,  0.40f, 1),
            new BoneDef(HumanBodyBones.RightHand,     Group.Hand,     0.35f, 1),
        };

        private const float TrembleSpeed = 9f;   // noise cycles per second-ish: a muscle shake

        [SerializeField] private FighterRigProfile _profile;
        [SerializeField] private SlapIK _ik;

        private readonly Transform[] _bones = new Transform[Defs.Length];
        private readonly Quaternion[] _sim = new Quaternion[Defs.Length];
        private readonly Vector3[] _vel = new Vector3[Defs.Length];
        private bool _primed;

        public FighterRigProfile Profile { get => _profile; set => _profile = value; }
        public SlapIK StrikeIK { get => _ik; set => _ik = value; }

        /// True while something else (the ragdoll) owns the bones.
        public bool Suspended { get; set; }

        /// Degrees of noise-driven spring deviation: the strain of holding a loaded slap. 0 = none.
        public float TrembleDeg { get; set; }

        private void Awake()
        {
            var anim = GetComponent<Animator>();
            if (!anim.isHuman) { enabled = false; return; }
            for (int i = 0; i < Defs.Length; i++) _bones[i] = anim.GetBoneTransform(Defs[i].Bone);
            if (_ik == null) _ik = GetComponent<SlapIK>();
        }

        /// The slap lands: `direction` is where it travels (attacker to defender), `intensity`
        /// 0..1 from the damage. The face turns and tips away; the trunk and arms take a share.
        public void Hit(Vector3 direction, float intensity)
        {
            if (_profile == null || Suspended || !_primed) return;
            direction.y = 0f;
            if (direction.sqrMagnitude < 1e-6f) direction = -transform.forward;
            direction.Normalize();

            Vector3 tip = Vector3.Cross(Vector3.up, direction);
            float turn = Mathf.Sign(Vector3.Dot(Vector3.Cross(transform.forward, direction), Vector3.up));
            Vector3 kick = (tip * (1f - _profile.hitTwistShare) + Vector3.up * (turn * _profile.hitTwistShare)).normalized
                         * (_profile.hitImpulseDegPerSecond * Mathf.Deg2Rad * Mathf.Clamp(intensity, 0.15f, 1.25f));

            for (int i = 0; i < Defs.Length; i++)
            {
                var b = _bones[i];
                if (b == null) continue;
                // The spring integrates in the bone's parent space.
                Quaternion parent = b.parent != null ? b.parent.rotation : Quaternion.identity;
                // Arms flail the other way round to the head: they get flung, not turned.
                float share = Defs[i].Side == 0 ? Defs[i].HitShare : -Defs[i].HitShare;
                _vel[i] += Quaternion.Inverse(parent) * (kick * share);
            }
        }

        private void LateUpdate()
        {
            if (_profile == null || Suspended || _profile.physicsWeight <= 0f) { _primed = false; return; }
            float dt = Time.deltaTime;
            float weight = _profile.physicsWeight;
            float pin = _ik != null ? _ik.StrikeWeight : 0f;
            sbyte strikeSide = _ik != null && !_ik.StrikingRight ? (sbyte)-1 : (sbyte)1;

            for (int i = 0; i < Defs.Length; i++)
            {
                var b = _bones[i];
                if (b == null) continue;
                Quaternion target = b.localRotation;   // this frame's animated (and IK'd) pose
                if (!_primed) { _sim[i] = target; _vel[i] = Vector3.zero; continue; }

                float hz = Frequency(Defs[i].Group);
                if (TrembleDeg > 0f && dt > 0f) _vel[i] += Tremble(i, hz) * dt;
                RotationSpring.Step(ref _sim[i], ref _vel[i], target, hz,
                                    _profile.dampingRatio, dt, _profile.maxDeviationDeg);

                // The striking arm belongs to the IK at contact: the palm must meet the cheek.
                if (pin > 0f && Defs[i].Side == strikeSide)
                {
                    _sim[i] = Quaternion.Slerp(_sim[i], target, pin);
                    _vel[i] *= 1f - pin;
                }
                b.localRotation = weight >= 1f ? _sim[i] : Quaternion.Slerp(target, _sim[i], weight);
            }
            _primed = true;
        }

        // An angular acceleration whose steady spring deviation is about TrembleDeg. Perlin, so
        // the bones strain smoothly and differently rather than jittering together.
        private Vector3 Tremble(int bone, float hz)
        {
            float t = Time.time * TrembleSpeed;
            float s = bone * 13.7f;
            var n = new Vector3(Mathf.PerlinNoise(t, s) - 0.5f, Mathf.PerlinNoise(s, t) - 0.5f, Mathf.PerlinNoise(t + s, s * 0.5f) - 0.5f) * 2f;
            float omega = 2f * Mathf.PI * hz;
            return n * (TrembleDeg * Mathf.Deg2Rad * omega * omega);
        }

        private float Frequency(Group g)
        {
            switch (g)
            {
                case Group.Torso:    return _profile.torsoHz;
                case Group.NeckHead: return _profile.neckHeadHz;
                case Group.UpperArm: return _profile.upperArmHz;
                case Group.Forearm:  return _profile.forearmHz;
                default:             return _profile.handHz;
            }
        }
    }
}
