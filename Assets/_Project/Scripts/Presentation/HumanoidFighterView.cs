using UnityEngine;
using SlapMania.Data;
using SlapMania.Logging;

namespace SlapMania.Presentation
{
    /// A real humanoid fighter driven through IFighterView (M08).
    ///
    /// The windup is timed, not triggered: each attack move carries the clip time where the
    /// hand meets the face, and the clip is started (and if needed slowed) so that moment lands
    /// exactly on the FSM's impact frame. SlapIK then pins the hand to the cheek around it.
    /// Hits play the hit clip plus the HitReactor spring; the lethal slap hands the body to the
    /// ragdoll. A missing piece logs once and no-ops — placeholder-first failure mode.
    public sealed class HumanoidFighterView : FighterViewBase
    {
        private static readonly int AttackSpeedId = Animator.StringToHash("AttackSpeed");

        [SerializeField] private Animator _animator;
        [SerializeField] private FighterRigProfile _profile;
        [SerializeField] private SlapIK _ik;
        [SerializeField] private HitReactor _hitReactor;
        [SerializeField] private RagdollController _ragdoll;
        [Tooltip("Objects hidden on this fighter (e.g. a sword on a slapper).")]
        [SerializeField] private GameObject[] _hidden = new GameObject[0];

        private IFighterView _opponent;
        private Transform _head;
        private int _nextAttack;
        private bool _warned;

        public override Transform HeadAnchor => _head != null ? _head : transform;
        public RagdollController Ragdoll => _ragdoll;

        private void Awake()
        {
            if (_animator == null) _animator = GetComponentInChildren<Animator>();
            if (_animator != null && _animator.isHuman) _head = _animator.GetBoneTransform(HumanBodyBones.Head);
            for (int i = 0; i < _hidden.Length; i++) if (_hidden[i] != null) _hidden[i].SetActive(false);
            if (_animator == null || _profile == null)
                SlapLog.Warn(LogChannel.Presentation, name + ": HumanoidFighterView missing Animator or FighterRigProfile; calls will no-op.");
        }

        public override void BindOpponent(IFighterView opponent)
        {
            _opponent = opponent;
            if (_ik != null) _ik.SetOpponent(opponent);
        }

        public override Vector3 GetStrikePoint(Vector3 attackerPosition)
        {
            Vector3 head = HeadAnchor.position;
            Vector3 toward = attackerPosition - head;
            toward.y = 0f;
            if (toward.sqrMagnitude < 1e-6f) return head;
            toward.Normalize();
            Vector2 o = _profile != null ? _profile.strikeOffset : new Vector2(0.11f, -0.03f);
            return head + toward * o.x + Vector3.up * o.y;
        }

        public override void PlayIdle() => Cross(_profile != null ? _profile.idleState : null);
        public override void PlayChargePose() => Cross(_profile != null ? _profile.chargeState : null);
        public override void PlayVictoryPose() => Cross(_profile != null ? _profile.victoryState : null);
        public override void PlayFinisher() => Cross(_profile != null ? _profile.idleState : null);

        public override void PlayDefeatPose()
        {
            if (_ragdoll != null && _ragdoll.IsActive) return;
            Cross(_profile != null ? _profile.hitState : null);
        }

        public override void PlayWindup(float secondsToContact)
        {
            if (!Ready() || _profile.attacks == null || _profile.attacks.Length == 0) return;
            var move = _profile.attacks[_nextAttack % _profile.attacks.Length];
            _nextAttack++;

            float contact = move.contactMs * 0.001f;
            float s = Mathf.Max(0.05f, secondsToContact);
            // Contact later than the windup allows: skip ahead. Earlier: slow the clip down so it
            // still arrives on the impact frame rather than swinging through thin air.
            float speed = contact > s ? 1f : Mathf.Max(0.2f, contact / s);
            float offset = contact > s ? contact - s : 0f;
            _animator.SetFloat(AttackSpeedId, speed);
            _animator.CrossFadeInFixedTime(move.state, _profile.crossFadeMs * 0.001f, 0, offset);
            if (_ik != null) _ik.BeginStrike(move.rightHand, s);
        }

        public override void PlayImpactRecoil(float intensity, Vector3 hitDirection)
        {
            if (!Ready()) return;
            _animator.CrossFadeInFixedTime(_profile.hitState, 0.05f, 0, 0f);
            if (_hitReactor != null) _hitReactor.Hit(hitDirection, intensity);
        }

        public override void PlayKnockout(float intensity, Vector3 hitDirection)
        {
            if (_ik != null) { _ik.Cancel(); _ik.enabled = false; }
            if (_hitReactor != null) _hitReactor.Suspended = true;
            if (_ragdoll != null && _ragdoll.BodyCount > 0) _ragdoll.Activate(hitDirection, intensity, _profile);
            else PlayImpactRecoil(1f, hitDirection); // no ragdoll built: at least react
        }

        private void Cross(string state)
        {
            if (!Ready() || string.IsNullOrEmpty(state)) return;
            if (_ragdoll != null && _ragdoll.IsActive) return;
            _animator.CrossFadeInFixedTime(state, _profile.crossFadeMs * 0.001f, 0, 0f);
        }

        private bool Ready()
        {
            if (_animator != null && _animator.isActiveAndEnabled && _profile != null) return true;
            if (!_warned) { _warned = true; SlapLog.Warn(LogChannel.Presentation, name + ": fighter not ready (animator/profile)."); }
            return false;
        }
    }
}
