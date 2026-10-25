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

        /// 0..1 — how strongly the hand is pinned to the cheek this frame.
        public float StrikeWeight { get; private set; }

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
        }

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
            if (_profile == null || _opponent == null) return;

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

            Vector3 target = _opponent.GetStrikePoint(transform.position);
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
