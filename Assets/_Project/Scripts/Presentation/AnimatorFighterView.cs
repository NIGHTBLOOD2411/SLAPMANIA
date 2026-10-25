using UnityEngine;
using SlapMania.Logging;

namespace SlapMania.Presentation
{
    /// Drives a real Animator from the same IFighterView calls (M08). This is the component
    /// to hook your Animator Controller to: drop it on the fighter prefab, assign the
    /// Animator, and match the trigger/parameter names below to your controller.
    ///
    /// A missing Animator or an unknown trigger logs a Warn and no-ops — the bout still runs
    /// (placeholder-first failure mode), so a half-built controller never hard-stops a match.
    public sealed class AnimatorFighterView : FighterViewBase
    {
        [SerializeField] private Animator _animator;

        [Header("Trigger names (match your Animator Controller)")]
        [SerializeField] private string _idleTrigger     = "Idle";
        [SerializeField] private string _chargeTrigger   = "Charge";
        [SerializeField] private string _windupTrigger   = "Windup";
        [SerializeField] private string _impactTrigger   = "Impact";
        [SerializeField] private string _finisherTrigger = "Finisher";
        [SerializeField] private string _victoryTrigger  = "Victory";
        [SerializeField] private string _defeatTrigger   = "Defeat";

        [Header("Parameters")]
        [SerializeField] private string _recoilIntensityParam = "RecoilIntensity";

        private void Reset() { _animator = GetComponentInChildren<Animator>(); }

        private void Awake()
        {
            if (_animator == null) _animator = GetComponentInChildren<Animator>();
            if (_animator == null)
                SlapLog.Warn(LogChannel.Presentation, name + ": AnimatorFighterView has no Animator; view calls will no-op.");
        }

        public override void PlayIdle()        => Fire(_idleTrigger);
        public override void PlayChargePose()  => Fire(_chargeTrigger);
        public override void PlayWindup(float secondsToContact) => Fire(_windupTrigger);
        public override void PlayFinisher()    => Fire(_finisherTrigger);
        public override void PlayVictoryPose() => Fire(_victoryTrigger);
        public override void PlayDefeatPose()  => Fire(_defeatTrigger);
        public override void PlayKnockout(float intensity, Vector3 hitDirection) => Fire(_defeatTrigger);

        public override Transform HeadAnchor
        {
            get
            {
                var head = _animator != null && _animator.isHuman ? _animator.GetBoneTransform(HumanBodyBones.Head) : null;
                return head != null ? head : transform;
            }
        }

        public override void PlayImpactRecoil(float intensity, Vector3 hitDirection)
        {
            if (_animator == null) return;
            if (!string.IsNullOrEmpty(_recoilIntensityParam))
                _animator.SetFloat(_recoilIntensityParam, Mathf.Clamp01(intensity));
            Fire(_impactTrigger);
        }

        private void Fire(string trigger)
        {
            if (_animator == null || string.IsNullOrEmpty(trigger)) return;
            _animator.SetTrigger(trigger);
        }
    }
}
