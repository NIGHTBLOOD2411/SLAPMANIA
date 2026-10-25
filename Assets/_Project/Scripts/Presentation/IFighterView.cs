using UnityEngine;

namespace SlapMania.Presentation
{
    /// The seam that lets a capsule stand in for a character, permanently (M08,
    /// placeholder-first). The simulation never references Animator or Transform; the
    /// bout driver turns MatchSignal moments into these calls, so a finished fighter and
    /// a capsule are driven identically.
    ///
    /// The windup carries the seconds until contact so a real clip can be timed to land on
    /// the FSM's impact frame; hits carry a direction so bodies react the right way; the
    /// head / strike-point anchors feed the strike IK and the world-anchored HUD rings.
    public interface IFighterView
    {
        void PlayIdle();
        void PlayChargePose();
        void PlayWindup(float secondsToContact);
        void PlayImpactRecoil(float intensity, Vector3 hitDirection); // intensity 0..1
        void PlayKnockout(float intensity, Vector3 hitDirection);     // the lethal slap
        void PlayFinisher();
        void PlayVictoryPose();
        void PlayDefeatPose();

        /// The fighter this one faces: look-at and strike targets come from it.
        void BindOpponent(IFighterView opponent);

        /// The head, for look-at IK and for anchoring HUD elements over the face.
        Transform HeadAnchor { get; }

        /// Where a slap from `attackerPosition` lands: the cheek facing the attacker.
        Vector3 GetStrikePoint(Vector3 attackerPosition);
    }
}
