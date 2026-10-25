using System;
using System.Collections.Generic;
using UnityEngine;
using SlapMania.Core;
using SlapMania.Core.Cinematics;
using SlapMania.Core.Motion;

namespace SlapMania.Presentation
{
    public enum StateCameraMode : byte
    {
        /// Do nothing on this state: whatever the camera is doing carries on.
        Keep,
        /// Blend back to the rig's gameplay pose.
        Gameplay,
        /// Blend to this state's own camera key (pose, target, shake), then hold or return.
        Shot,
        /// Play the recorded CameraSequence bound to this cue on the director.
        Sequence
    }

    /// One game state's camera transition (M16, GLOSSARY: **State shot**): what the camera
    /// does when the state begins, how it gets there, and when it hands back to the rig.
    /// Every field is serialized with the rig setup, so the recorder's Rig tab can save it.
    [Serializable]
    public sealed class StateShot
    {
        public CameraCue cue;
        public StateCameraMode mode = StateCameraMode.Keep;

        /// The shot itself. Its `follow`/`followRotation` make it attacker-relative, so one
        /// authored shot covers both players' turns.
        public CameraKeyframe key = new CameraKeyframe { path = PathMode.Linear };

        [Tooltip("Two-sided: `key` frames the PLAYER's attacking turns, `opponentKey` the OPPONENT's. Off: `key` for both (mirrored if it is attacker-relative).")]
        public bool perSide;
        public CameraKeyframe opponentKey = new CameraKeyframe { path = PathMode.Linear };

        public CameraKeyframe KeyFor(FighterSlot attacker) =>
            perSide && attacker == FighterSlot.Opponent && opponentKey != null ? opponentKey : key;

        [Tooltip("Transition in: ms and ease. Also used by Gameplay mode.")]
        public double blendMs = 400;
        public Ease blendEase = Ease.InOutCubic;

        [Tooltip("Shot mode: ms to hold before returning to the rig. 0 = hold until the next state changes the camera.")]
        public double holdMs;
        public double returnMs = 350;
        public Ease returnEase = Ease.InOutCubic;

        [NonSerialized] private CameraSequenceData _seq;

        /// The one-key sequence the director plays for Shot mode. Built once and refreshed in
        /// place, so firing a state allocates nothing after the first time (R5).
        public CameraSequenceData AsSequence(FighterSlot attacker)
        {
            var k = KeyFor(attacker);
            if (_seq == null)
            {
                _seq = new CameraSequenceData { name = "state:" + cue, keys = new List<CameraKeyframe>(1) };
                _seq.keys.Add(k);
            }
            _seq.keys[0] = k;
            k.timeMs = 0;
            k.holdMs = holdMs > 0 ? holdMs : 0;
            _seq.holdAtEnd = !(holdMs > 0);
            _seq.unscaledTime = true;
            _seq.loop = false;
            _seq.blendInMs = blendMs;
            _seq.blendInEase = blendEase;
            _seq.blendOutMs = returnMs;
            _seq.blendOutEase = returnEase;
            return _seq;
        }

        public StateShot Clone()
        {
            var s = (StateShot)MemberwiseClone();
            s.key = key.Clone();
            s.opponentKey = opponentKey != null ? opponentKey.Clone() : new CameraKeyframe { path = PathMode.Linear };
            s._seq = null;
            return s;
        }
    }
}
