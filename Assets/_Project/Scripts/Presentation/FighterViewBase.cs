using UnityEngine;

namespace SlapMania.Presentation
{
    /// Serializable base so a scene can wire an IFighterView by reference (Unity cannot
    /// serialize a bare interface field). PlaceholderFighterView, AnimatorFighterView and
    /// HumanoidFighterView all derive from it, so the bout driver holds a FighterViewBase and
    /// never cares which.
    public abstract class FighterViewBase : MonoBehaviour, IFighterView
    {
        public abstract void PlayIdle();
        public abstract void PlayChargePose();
        public abstract void PlayWindup(float secondsToContact);
        public abstract void PlayImpactRecoil(float intensity, Vector3 hitDirection);
        public abstract void PlayKnockout(float intensity, Vector3 hitDirection);
        public abstract void PlayFinisher();
        public abstract void PlayVictoryPose();
        public abstract void PlayDefeatPose();

        public virtual void BindOpponent(IFighterView opponent) { }

        /// Defaults to a point above the root for views without a skeleton.
        public virtual Transform HeadAnchor => transform;

        public virtual Vector3 GetStrikePoint(Vector3 attackerPosition)
        {
            Vector3 head = HeadAnchor.position;
            Vector3 toward = attackerPosition - head;
            toward.y = 0f;
            return toward.sqrMagnitude > 1e-6f ? head + toward.normalized * 0.12f : head;
        }
    }
}
