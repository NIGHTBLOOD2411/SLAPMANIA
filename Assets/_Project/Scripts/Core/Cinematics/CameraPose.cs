using System;
using UnityEngine;

namespace SlapMania.Core.Cinematics
{
    /// Everything a camera key captures: where the camera is, where it points, and its lens.
    /// Plain data (R2); `CameraTrack` blends two of these, `CameraLens` applies the lens part.
    [Serializable]
    public struct CameraPose
    {
        public Vector3 position;
        public Quaternion rotation;
        /// Vertical field of view, degrees.
        public float fov;
        /// Depth of field. Off by default; Bokeh is costly on mobile (ADR-0020).
        public bool dof;
        /// Metres from the lens to the plane in focus.
        public float focusDistance;
        /// f-stop: lower is a shallower focus.
        public float aperture;

        public const float MinFov = 1f, MaxFov = 179f;
        public const float MinFocus = 0.1f;
        public const float MinAperture = 1f, MaxAperture = 32f;

        public CameraPose(Vector3 position, Quaternion rotation, float fov)
        {
            this.position = position;
            this.rotation = rotation;
            this.fov = fov;
            dof = false;
            focusDistance = 3f;
            aperture = 5.6f;
        }

        /// Clamps the lens into range and repairs NaN or a zero quaternion, so a bad key or an
        /// overshooting ease can never feed a broken value into a Camera.
        public CameraPose Sanitized()
        {
            var p = this;
            if (!IsFinite(p.position)) p.position = Vector3.zero;
            float qm = p.rotation.x * p.rotation.x + p.rotation.y * p.rotation.y
                     + p.rotation.z * p.rotation.z + p.rotation.w * p.rotation.w;
            p.rotation = float.IsNaN(qm) || qm < 1e-8f ? Quaternion.identity : Normalize(p.rotation, qm);
            p.fov = Clamp(p.fov, MinFov, MaxFov, 60f);
            p.focusDistance = Clamp(p.focusDistance, MinFocus, float.MaxValue, 3f);
            p.aperture = Clamp(p.aperture, MinAperture, MaxAperture, 5.6f);
            return p;
        }

        private static float Clamp(float v, float lo, float hi, float fallback) =>
            float.IsNaN(v) ? fallback : v < lo ? lo : v > hi ? hi : v;

        private static bool IsFinite(Vector3 v) =>
            !(float.IsNaN(v.x) || float.IsNaN(v.y) || float.IsNaN(v.z)
              || float.IsInfinity(v.x) || float.IsInfinity(v.y) || float.IsInfinity(v.z));

        private static Quaternion Normalize(Quaternion q, float sqrMag)
        {
            float inv = 1f / (float)Math.Sqrt(sqrMag);
            return new Quaternion(q.x * inv, q.y * inv, q.z * inv, q.w * inv);
        }
    }
}
