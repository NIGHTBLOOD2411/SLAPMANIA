using UnityEngine;
using SlapMania.Data;

namespace SlapMania.Presentation
{
    /// The knock-out ragdoll (M08, KO-only per ADR-0011). The bodies, colliders and joints are
    /// generated on the fighter prefab by the editor RagdollBuilder; at runtime they sit
    /// kinematic with colliders off, costing nothing, until the lethal slap. Activate() hands
    /// the skeleton to physics and launches it along the slap, the head leading.
    ///
    /// Hitstop is running when this fires, so the launch plays in slow motion first;
    /// HitstopController scales fixedDeltaTime with timeScale so that stays smooth (S-042).
    public sealed class RagdollController : MonoBehaviour
    {
        [SerializeField] private Animator _animator;
        [SerializeField] private Rigidbody[] _bodies = new Rigidbody[0];
        [SerializeField] private Collider[] _colliders = new Collider[0];
        [SerializeField] private Rigidbody _head;

        public bool IsActive { get; private set; }
        public int BodyCount => _bodies.Length;

        private void Awake() => SetPhysical(false);

        public void Activate(Vector3 direction, float intensity, FighterRigProfile profile)
        {
            if (IsActive || _bodies.Length == 0) return;
            IsActive = true;
            if (_animator != null) _animator.enabled = false; // bones keep the last pose; physics takes over
            SetPhysical(true);

            direction.y = 0f;
            if (direction.sqrMagnitude < 1e-6f) direction = -transform.forward;
            direction.Normalize();

            float kick = profile != null ? profile.koVelocity : 3f;
            float up = profile != null ? profile.koUpVelocity : 1f;
            float headExtra = profile != null ? profile.koHeadExtra : 2.5f;
            Vector3 v = direction * (kick * Mathf.Lerp(0.8f, 1.25f, Mathf.Clamp01(intensity))) + Vector3.up * up;
            for (int i = 0; i < _bodies.Length; i++)
                if (_bodies[i] != null) _bodies[i].AddForce(v, ForceMode.VelocityChange);
            if (_head != null)
            {
                _head.AddForce(direction * headExtra, ForceMode.VelocityChange);
                _head.AddTorque(Vector3.Cross(Vector3.up, direction) * 6f, ForceMode.VelocityChange);
            }
        }

        private void SetPhysical(bool on)
        {
            for (int i = 0; i < _bodies.Length; i++)
            {
                var rb = _bodies[i];
                if (rb == null) continue;
                rb.isKinematic = !on;
                rb.interpolation = on ? RigidbodyInterpolation.Interpolate : RigidbodyInterpolation.None;
            }
            for (int i = 0; i < _colliders.Length; i++)
                if (_colliders[i] != null) _colliders[i].enabled = on;
        }
    }
}
