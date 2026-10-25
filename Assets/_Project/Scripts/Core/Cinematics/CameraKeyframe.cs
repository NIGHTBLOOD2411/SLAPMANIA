using System;
using UnityEngine;
using SlapMania.Core.Motion;

namespace SlapMania.Core.Cinematics
{
    /// How the camera's position travels *into* a key from the previous one.
    public enum PathMode : byte
    {
        /// Hold the previous pose, then jump to this key exactly at its time.
        Cut,
        /// Straight line.
        Linear,
        /// Catmull-Rom through the neighbouring keys: smooth curved fly-throughs.
        Spline
    }

    /// One camera key on a track (GLOSSARY: **Camera key**). All transition settings describe
    /// the move *into* this key; the first key's are unused. Timing is milliseconds (R8).
    ///
    ///   prev.timeMs ── prev.holdMs ──┐ transition (path, ease) ┌── timeMs ── holdMs ──> next
    [Serializable]
    public sealed class CameraKeyframe
    {
        /// When the camera arrives at this pose.
        public double timeMs;
        /// How long it stays here before the next transition starts.
        public double holdMs;
        public CameraPose pose = new CameraPose(Vector3.zero, Quaternion.identity, 50f);

        public PathMode path = PathMode.Spline;
        public Ease ease = Ease.InOutCubic;
        /// When set, `curve` (0..1 in, 0..1 out, overshoot allowed) replaces `ease`.
        public bool useCurve;
        public AnimationCurve curve;

        /// Target name to aim at; empty = use `pose.rotation`. Roll is kept from `pose.rotation`.
        public string lookAt = "";
        /// Target name to ride with; empty = world space. When set, `pose.position` is an
        /// offset from that target (world axes), so the shot tracks a moving fighter.
        public string follow = "";
        /// With `follow`: the offset and the rotation are in the target's *local* space, so a shot
        /// authored behind the player's attacker frames the opponent's attacker the same way
        /// when turns swap. `lookAt` still overrides the rotation.
        public bool followRotation;

        /// Trauma (0..1) added the moment the playhead reaches this key; 0 = none.
        public float shake;
        /// Index into `CameraShake`'s presets for that trauma.
        public int shakePreset;

        public CameraKeyframe Clone()
        {
            var k = (CameraKeyframe)MemberwiseClone();
            if (curve != null) k.curve = new AnimationCurve(curve.keys);
            return k;
        }
    }
}
