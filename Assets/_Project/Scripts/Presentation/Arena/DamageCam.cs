using UnityEngine;

namespace SlapMania.Presentation
{
    /// The live "DAMAGE CAM" jumbotron feed (M17, ADR-0012): a small camera beside the fight,
    /// looking at the defender's face from the attacker's side so the screen shows the slap
    /// landing. It renders into a low-resolution RenderTexture only every Nth frame — the
    /// staggered low-rate option from M17 — and holds the last frame in between. Before the
    /// first turn (the intro) it holds a wide ring shot, so the screen is never stale.
    [RequireComponent(typeof(Camera))]
    public sealed class DamageCam : MonoBehaviour
    {
        [SerializeField, Range(1, 6)] private int _renderEvery = 2;
        [Tooltip("Distance from the face, towards the attacker (m).")]
        [SerializeField] private float _distance = 0.95f;
        [Tooltip("Sideways offset so the attacking arm is in shot (m).")]
        [SerializeField] private float _side = 0.55f;
        [SerializeField] private float _height = 0.02f;
        [SerializeField, Min(0.1f)] private float _follow = 6f;

        [Header("Idle shot (no target yet)")]
        [SerializeField] private Vector3 _idlePosition = new Vector3(-2.4f, 1.9f, -2.6f);
        [SerializeField] private Vector3 _idleLookAt = new Vector3(0f, 1.2f, 0f);

        private Camera _cam;
        private Transform _target;
        private Vector3 _attacker;
        private bool _snap = true;

        private void Awake()
        {
            _cam = GetComponent<Camera>();
        }

        public void SetTarget(Transform face, Vector3 attackerPosition)
        {
            if (face != _target) _snap = true;
            _target = face;
            _attacker = attackerPosition;
        }

        private void LateUpdate()
        {
            if (_cam == null) return;
            _cam.enabled = Time.frameCount % _renderEvery == 0;
            if (_target == null)
            {
                transform.SetPositionAndRotation(_idlePosition, Quaternion.LookRotation(_idleLookAt - _idlePosition, Vector3.up));
                return;
            }

            Vector3 face = _target.position;
            Vector3 toAttacker = _attacker - face;
            toAttacker.y = 0f;
            if (toAttacker.sqrMagnitude < 1e-6f) toAttacker = Vector3.back;
            toAttacker.Normalize();
            Vector3 desired = face + toAttacker * _distance + Vector3.Cross(Vector3.up, toAttacker) * _side + Vector3.up * _height;
            if (_snap) { transform.position = desired; _snap = false; }
            else transform.position = Vector3.Lerp(transform.position, desired, 1f - Mathf.Exp(-_follow * Time.unscaledDeltaTime));
            transform.rotation = Quaternion.LookRotation(face - transform.position, Vector3.up);
        }
    }
}
