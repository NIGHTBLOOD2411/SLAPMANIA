using System;
using System.Collections.Generic;

namespace SlapMania.Core.Cinematics
{
    /// A whole recorded camera move: the one format for the runtime recorder's JSON and the
    /// `CameraSequence` asset (ADR-0020). JsonUtility-friendly: fields only, no properties.
    [Serializable]
    public sealed class CameraSequenceData
    {
        public string name = "untitled";
        /// Sorted by `timeMs` (`CameraTrack.Sort`).
        public List<CameraKeyframe> keys = new List<CameraKeyframe>();
        public bool loop;
        /// Play on unscaled time, so the move keeps going through hitstop and slow-mo.
        public bool unscaledTime = true;
        /// Blend from wherever the camera is into the first key, and back out at the end.
        public double blendInMs = 250;
        public double blendOutMs = 350;
        public SlapMania.Core.Motion.Ease blendInEase = SlapMania.Core.Motion.Ease.InOutCubic;
        public SlapMania.Core.Motion.Ease blendOutEase = SlapMania.Core.Motion.Ease.InOutCubic;
        /// Stay on the last pose when the track ends, until the next Play or Stop, instead of
        /// blending back to the rig. A per-state shot that holds "until the next state" uses it.
        public bool holdAtEnd;

        public double DurationMs => CameraTrack.DurationMs(keys);

        public CameraSequenceData Clone()
        {
            var c = (CameraSequenceData)MemberwiseClone();
            c.keys = new List<CameraKeyframe>(keys.Count);
            for (int i = 0; i < keys.Count; i++) c.keys.Add(keys[i].Clone());
            return c;
        }
    }

    /// Resolves the target names a key may aim at or follow ("attacker", "defenderHead"...).
    /// The scene implements it; Core only asks for a position (R2).
    public interface ICameraTargets
    {
        bool TryGetPosition(string name, out UnityEngine.Vector3 position);
        bool TryGetRotation(string name, out UnityEngine.Quaternion rotation);
    }
}
