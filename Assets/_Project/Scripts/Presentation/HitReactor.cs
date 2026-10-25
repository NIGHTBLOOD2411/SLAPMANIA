using UnityEngine;
using SlapMania.Data;

namespace SlapMania.Presentation
{
    /// Procedural hit reaction layered on top of the hit clip (M08): a slap kicks a damped
    /// angular spring that is applied to spine, chest, neck and head after animation, so the
    /// head snaps away from the hand — tipping over and turning — then settles, scaled by the
    /// slap's damage and aimed by its direction.
    ///
    /// Runs on scaled time: during hitstop the head holds its displaced pose, then whips —
    /// which is exactly the freeze-frame read. Integrated with fixed sub-steps so a frame spike
    /// cannot blow the spring up. Suspended while the ragdoll owns the bones.
    [RequireComponent(typeof(Animator))]
    public sealed class HitReactor : MonoBehaviour
    {
        [SerializeField] private FighterRigProfile _profile;

        private const float SubStep = 1f / 90f;
        private const int MaxSubSteps = 6;

        private Transform _spine, _chest, _neck, _head;
        private Vector3 _angle;     // rotation vector (degrees, world space)
        private Vector3 _velocity;  // degrees / second

        public bool Suspended { get; set; }
        public FighterRigProfile Profile { get => _profile; set => _profile = value; }

        private void Awake()
        {
            var anim = GetComponent<Animator>();
            if (!anim.isHuman) { enabled = false; return; }
            _spine = anim.GetBoneTransform(HumanBodyBones.Spine);
            _chest = anim.GetBoneTransform(HumanBodyBones.UpperChest);
            if (_chest == null) _chest = anim.GetBoneTransform(HumanBodyBones.Chest);
            _neck = anim.GetBoneTransform(HumanBodyBones.Neck);
            _head = anim.GetBoneTransform(HumanBodyBones.Head);
        }

        /// `direction` is where the slap is travelling (from attacker to defender).
        public void Hit(Vector3 direction, float intensity)
        {
            if (_profile == null || Suspended) return;
            direction.y = 0f;
            if (direction.sqrMagnitude < 1e-6f) direction = -transform.forward;
            direction.Normalize();

            // Tip away from the hand, and turn the face away from it.
            Vector3 tipAxis = Vector3.Cross(Vector3.up, direction);
            float turn = Mathf.Sign(Vector3.Dot(Vector3.Cross(transform.forward, direction), Vector3.up));
            Vector3 kick = tipAxis * (1f - _profile.twistShare) + Vector3.up * (turn * _profile.twistShare);
            _velocity += kick.normalized * (_profile.impulseDegPerSecond * Mathf.Clamp(intensity, 0.15f, 1.25f));
        }

        private void LateUpdate()
        {
            if (_profile == null || Suspended) return;
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            int steps = Mathf.Min(MaxSubSteps, Mathf.CeilToInt(dt / SubStep));
            float h = dt / steps;
            float omega = 2f * Mathf.PI * _profile.springFrequency;
            for (int i = 0; i < steps; i++)
            {
                Vector3 accel = -omega * omega * _angle - 2f * _profile.springDamping * omega * _velocity;
                _velocity += accel * h;
                _angle += _velocity * h;
            }
            if (_angle.magnitude > _profile.maxAngle)
            {
                _angle = _angle.normalized * _profile.maxAngle;
                _velocity *= 0.5f;
            }
            if (_angle.sqrMagnitude < 1e-4f && _velocity.sqrMagnitude < 1e-2f) { _angle = Vector3.zero; _velocity = Vector3.zero; return; }

            float deg = _angle.magnitude;
            Vector3 axis = _angle / deg;
            Apply(_spine, deg * _profile.spineWeight, axis);
            Apply(_chest, deg * _profile.chestWeight, axis);
            Apply(_neck, deg * _profile.neckWeight, axis);
            Apply(_head, deg * _profile.headWeight, axis);
        }

        private static void Apply(Transform bone, float degrees, Vector3 axis)
        {
            if (bone == null || degrees <= 0f) return;
            bone.rotation = Quaternion.AngleAxis(degrees, axis) * bone.rotation;
        }
    }
}
