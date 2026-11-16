using UnityEngine;
using SlapMania.Data;

namespace SlapMania.Presentation
{
    /// Humanoid IK for the slap (M08). Lives on the Animator's GameObject, because Unity only
    /// calls OnAnimatorIK there (and only with "IK Pass" on the layer).
    ///
    ///   * Look-at: both fighters keep their eyes on each other.
    ///   * Strike: from `blendIn` before contact the striking hand is pulled onto the
    ///     opponent's cheek, held through contact, then released — so whatever the clip does,
    ///     the hand actually meets the face on the FSM's impact frame. The palm is turned to
    ///     face the cheek and, in LateUpdate, the fingers straighten out of the Mixamo fist
    ///     into an open-hand slap.
    ///
    /// Timing runs on scaled time so hitstop freezes the pose at contact with everything else.
    [RequireComponent(typeof(Animator))]
    public sealed class SlapIK : MonoBehaviour
    {
        [SerializeField] private FighterRigProfile _profile;

        private static readonly HumanBodyBones[] RightFingers =
        {
            HumanBodyBones.RightThumbProximal, HumanBodyBones.RightThumbIntermediate, HumanBodyBones.RightThumbDistal,
            HumanBodyBones.RightIndexProximal, HumanBodyBones.RightIndexIntermediate, HumanBodyBones.RightIndexDistal,
            HumanBodyBones.RightMiddleProximal, HumanBodyBones.RightMiddleIntermediate, HumanBodyBones.RightMiddleDistal,
            HumanBodyBones.RightRingProximal, HumanBodyBones.RightRingIntermediate, HumanBodyBones.RightRingDistal,
            HumanBodyBones.RightLittleProximal, HumanBodyBones.RightLittleIntermediate, HumanBodyBones.RightLittleDistal,
        };
        private static readonly HumanBodyBones[] LeftFingers =
        {
            HumanBodyBones.LeftThumbProximal, HumanBodyBones.LeftThumbIntermediate, HumanBodyBones.LeftThumbDistal,
            HumanBodyBones.LeftIndexProximal, HumanBodyBones.LeftIndexIntermediate, HumanBodyBones.LeftIndexDistal,
            HumanBodyBones.LeftMiddleProximal, HumanBodyBones.LeftMiddleIntermediate, HumanBodyBones.LeftMiddleDistal,
            HumanBodyBones.LeftRingProximal, HumanBodyBones.LeftRingIntermediate, HumanBodyBones.LeftRingDistal,
            HumanBodyBones.LeftLittleProximal, HumanBodyBones.LeftLittleIntermediate, HumanBodyBones.LeftLittleDistal,
        };

        private const float ReachShare = 0.95f;   // never ask the arm for its full, locked-out length

        private Animator _anim;
        private IFighterView _opponent;
        private Transform _rightShoulder, _leftShoulder;
        private readonly Transform[] _rFingers = new Transform[15];
        private readonly Transform[] _lFingers = new Transform[15];
        private readonly Quaternion[] _rOpen = new Quaternion[15];
        private readonly Quaternion[] _lOpen = new Quaternion[15];

        private bool _striking;
        private bool _rightHand = true;
        private float _contactAt;
        private float _armLength, _torsoLength;

        // Feet planted on the idle stance (charge, slap, hit): captured clips walk and spin.
        private bool _planted, _hasStance;
        private float _plant;
        private Vector3 _stanceL, _stanceR;
        private Quaternion _stanceRotL, _stanceRotR;

        /// 0..1 — how strongly the hand is pinned to the cheek this frame.
        public float StrikeWeight { get; private set; }

        /// The current (or last) strike's hand: the physical-animation layer pins that arm.
        public bool StrikingRight => _rightHand;

        public FighterRigProfile Profile { get => _profile; set => _profile = value; }

        private void Awake()
        {
            _anim = GetComponent<Animator>();
            if (!_anim.isHuman) { enabled = false; return; }
            _rightShoulder = _anim.GetBoneTransform(HumanBodyBones.RightUpperArm);
            _leftShoulder = _anim.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            // The prefab's rest pose is the Mixamo T-pose: fingers straight. Remember it as "open".
            CacheFingers(RightFingers, _rFingers, _rOpen);
            CacheFingers(LeftFingers, _lFingers, _lOpen);
        }

        private void CacheFingers(HumanBodyBones[] bones, Transform[] t, Quaternion[] open)
        {
            for (int i = 0; i < bones.Length; i++)
            {
                t[i] = _anim.GetBoneTransform(bones[i]);
                open[i] = t[i] != null ? t[i].localRotation : Quaternion.identity;
            }
        }

        public void SetOpponent(IFighterView opponent) => _opponent = opponent;

        public void BeginStrike(bool rightHand, float secondsToContact)
        {
            _striking = true;
            _rightHand = rightHand;
            _contactAt = Time.time + Mathf.Max(0f, secondsToContact);
            MeasureReach();
        }

        // At strike time, not Awake: ChampionSpawner scales the fighter after it is instantiated.
        private void MeasureReach()
        {
            var upper = _anim.GetBoneTransform(_rightHand ? HumanBodyBones.RightUpperArm : HumanBodyBones.LeftUpperArm);
            var lower = _anim.GetBoneTransform(_rightHand ? HumanBodyBones.RightLowerArm : HumanBodyBones.LeftLowerArm);
            var hand = _anim.GetBoneTransform(_rightHand ? HumanBodyBones.RightHand : HumanBodyBones.LeftHand);
            var hips = _anim.GetBoneTransform(HumanBodyBones.Hips);
            if (upper == null || lower == null || hand == null || hips == null) { _armLength = _torsoLength = 0f; return; }
            _armLength = Vector3.Distance(upper.position, lower.position) + Vector3.Distance(lower.position, hand.position);
            _torsoLength = Vector3.Distance(hips.position, upper.position);
        }

        /// Charge, slap and hit: feet held on the idle stance, whole-body turn reduced (the mocap
        /// was captured walking and spinning; the table is 0.24 m away).
        public void SetPlanted(bool planted) => _planted = planted;

        public void Cancel()
        {
            _striking = false;
            StrikeWeight = 0f;
        }

        private float EvaluateWeight()
        {
            if (!_striking || _profile == null) return 0f;
            float t = Time.time - _contactAt;
            float blendIn = _profile.ikBlendInMs * 0.001f;
            float hold = _profile.ikHoldMs * 0.001f;
            float blendOut = _profile.ikBlendOutMs * 0.001f;
            if (t < -blendIn) return 0f;
            if (t < 0f) return blendIn > 0f ? Smooth((t + blendIn) / blendIn) : 1f;
            if (t < hold) return 1f;
            if (t < hold + blendOut) return blendOut > 0f ? 1f - Smooth((t - hold) / blendOut) : 0f;
            _striking = false;
            return 0f;
        }

        private static float Smooth(float x)
        {
            x = Mathf.Clamp01(x);
            return x * x * (3f - 2f * x);
        }

        private void OnAnimatorIK(int layerIndex)
        {
            if (_profile == null) return;
            UpdatePlant();
            if (_opponent == null) return;

            var head = _opponent.HeadAnchor;
            if (head != null)
            {
                _anim.SetLookAtWeight(_profile.lookWeight, _profile.lookBodyWeight, _profile.lookHeadWeight, 0f, _profile.lookClamp);
                _anim.SetLookAtPosition(head.position);
            }

            StrikeWeight = EvaluateWeight();
            var goal = _rightHand ? AvatarIKGoal.RightHand : AvatarIKGoal.LeftHand;
            var other = _rightHand ? AvatarIKGoal.LeftHand : AvatarIKGoal.RightHand;
            _anim.SetIKPositionWeight(other, 0f);
            _anim.SetIKRotationWeight(other, 0f);
            if (StrikeWeight <= 0f)
            {
                _anim.SetIKPositionWeight(goal, 0f);
                _anim.SetIKRotationWeight(goal, 0f);
                return;
            }

            // A slap lands on the cheek on the striking hand's side, not the front of the face: a
            // right hand swinging across meets the cheek on the attacker's right.
            Vector3 target = _opponent.GetStrikePoint(transform.position)
                           + transform.right * ((_rightHand ? 1f : -1f) * _profile.cheekSideOffset);
            LeanIntoReach(target);
            _anim.SetIKPositionWeight(goal, StrikeWeight * _profile.ikPositionWeight);
            _anim.SetIKPosition(goal, target);

            var shoulder = _rightHand ? _rightShoulder : _leftShoulder;
            Vector3 reach = shoulder != null ? target - shoulder.position : transform.forward;
            if (reach.sqrMagnitude > 1e-6f)
            {
                Quaternion palm = Quaternion.LookRotation(reach, Vector3.up) * Quaternion.Euler(
                    _rightHand ? _profile.handRotationOffset : Vector3.Scale(_profile.handRotationOffset, new Vector3(1f, -1f, -1f)));
                _anim.SetIKRotationWeight(goal, StrikeWeight * _profile.ikRotationWeight);
                _anim.SetIKRotation(goal, palm);
            }
        }

        // The cheek is often beyond arm's reach from an upright stance (a captured slap was thrown
        // from closer than the table allows). Pitch the body toward it by just the shortfall,
        // minus any lean the clip already has, with the feet held where the clip put them.
        private void LeanIntoReach(Vector3 target)
        {
            if (_profile.maxStrikeLeanDeg > 0f && _torsoLength > 1e-3f)
            {
                Vector3 toTarget = target - _anim.bodyPosition;
                toTarget.y = 0f;
                float shortfall = toTarget.magnitude - _armLength * ReachShare;
                if (shortfall > 0f && toTarget.sqrMagnitude > 1e-6f)
                {
                    float wanted = Mathf.Asin(Mathf.Clamp01(shortfall / _torsoLength)) * Mathf.Rad2Deg;
                    Vector3 fwd = _anim.bodyRotation * Vector3.forward;
                    float already = Mathf.Max(0f, -Mathf.Asin(Mathf.Clamp(fwd.y, -1f, 1f)) * Mathf.Rad2Deg);
                    float deg = Mathf.Clamp(wanted - already, 0f, _profile.maxStrikeLeanDeg) * StrikeWeight;
                    if (deg > 0.01f)
                    {
                        // Read the feet before the body moves: they stay where they were put.
                        HoldFoot(AvatarIKGoal.LeftFoot);
                        HoldFoot(AvatarIKGoal.RightFoot);
                        _anim.bodyRotation = Quaternion.AngleAxis(deg, Vector3.Cross(Vector3.up, toTarget.normalized)) * _anim.bodyRotation;
                    }
                }
            }
        }

        private void HoldFoot(AvatarIKGoal foot)
        {
            _anim.SetIKPosition(foot, _anim.GetIKPosition(foot));
            _anim.SetIKRotation(foot, _anim.GetIKRotation(foot));
            _anim.SetIKPositionWeight(foot, 1f);
            _anim.SetIKRotationWeight(foot, 1f);
        }

        // Owns the foot goals every frame. Off: remembers where the idle puts the feet. On: holds
        // them there and takes back most of the clip's body turn, so the legs stay plausible.
        private void UpdatePlant()
        {
            float step = Time.deltaTime / Mathf.Max(0.001f, _profile.plantBlendMs * 0.001f);
            _plant = Mathf.MoveTowards(_plant, _planted && _hasStance ? 1f : 0f, step);
            var root = transform;
            if (_plant <= 0f)
            {
                _stanceL = root.InverseTransformPoint(_anim.GetIKPosition(AvatarIKGoal.LeftFoot));
                _stanceR = root.InverseTransformPoint(_anim.GetIKPosition(AvatarIKGoal.RightFoot));
                _stanceRotL = Quaternion.Inverse(root.rotation) * _anim.GetIKRotation(AvatarIKGoal.LeftFoot);
                _stanceRotR = Quaternion.Inverse(root.rotation) * _anim.GetIKRotation(AvatarIKGoal.RightFoot);
                _hasStance = true;
                SetFoot(AvatarIKGoal.LeftFoot, 0f, Vector3.zero, Quaternion.identity);
                SetFoot(AvatarIKGoal.RightFoot, 0f, Vector3.zero, Quaternion.identity);
                return;
            }

            float w = Smooth(_plant);
            SetFoot(AvatarIKGoal.LeftFoot, w, root.TransformPoint(_stanceL), root.rotation * _stanceRotL);
            SetFoot(AvatarIKGoal.RightFoot, w, root.TransformPoint(_stanceR), root.rotation * _stanceRotR);

            Vector3 fwd = Vector3.ProjectOnPlane(_anim.bodyRotation * Vector3.forward, Vector3.up);
            if (fwd.sqrMagnitude > 1e-6f)
            {
                float yaw = Vector3.SignedAngle(root.forward, fwd, Vector3.up);
                _anim.bodyRotation = Quaternion.AngleAxis(-yaw * (1f - _profile.plantedYawKeep) * w, Vector3.up) * _anim.bodyRotation;
            }
        }

        private void SetFoot(AvatarIKGoal foot, float weight, Vector3 position, Quaternion rotation)
        {
            if (weight > 0f)
            {
                _anim.SetIKPosition(foot, position);
                _anim.SetIKRotation(foot, rotation);
            }
            _anim.SetIKPositionWeight(foot, weight);
            _anim.SetIKRotationWeight(foot, weight);
        }

        // After animation and IK: open the striking hand for the slap.
        private void LateUpdate()
        {
            if (_profile == null || StrikeWeight <= 0f || _profile.openHandWeight <= 0f) return;
            float w = StrikeWeight * _profile.openHandWeight;
            var bones = _rightHand ? _rFingers : _lFingers;
            var open = _rightHand ? _rOpen : _lOpen;
            for (int i = 0; i < bones.Length; i++)
                if (bones[i] != null) bones[i].localRotation = Quaternion.Slerp(bones[i].localRotation, open[i], w);
        }
    }
}
