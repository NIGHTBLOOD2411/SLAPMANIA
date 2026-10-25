using System.Collections.Generic;
using UnityEngine;
using SlapMania.Core.Motion;

namespace SlapMania.Core.Cinematics
{
    /// Pure camera-track evaluation (R2, R8, ADR-0020): the pose is a function of elapsed
    /// milliseconds only, so scrubbing, looping and replaying always land on the same frame.
    /// Allocation-free: callers pass a concrete List and indexes are walked with `for` (R5).
    public static class CameraTrack
    {
        public static double DurationMs(List<CameraKeyframe> keys)
        {
            if (keys == null || keys.Count == 0) return 0;
            var last = keys[keys.Count - 1];
            return last.timeMs + Hold(last);
        }

        /// Index of the last key whose `timeMs <= ms`, or -1 when `ms` is before the first key.
        /// Keys must be sorted (`Sort`).
        public static int FindKey(List<CameraKeyframe> keys, double ms)
        {
            if (keys == null || keys.Count == 0 || double.IsNaN(ms) || ms < keys[0].timeMs) return -1;
            int lo = 0, hi = keys.Count - 1;
            while (lo < hi)
            {
                int mid = (lo + hi + 1) >> 1;
                if (keys[mid].timeMs <= ms) lo = mid;
                else hi = mid - 1;
            }
            return lo;
        }

        /// The pose at `ms`. Before the first key it holds the first; past the end it holds the
        /// last. NaN or negative time reads as 0. False only for an empty track.
        public static bool TryEvaluate(List<CameraKeyframe> keys, double ms, ICameraTargets targets, out CameraPose pose)
        {
            if (keys == null || keys.Count == 0) { pose = default; return false; }
            if (double.IsNaN(ms) || ms < 0) ms = 0;

            int i = FindKey(keys, ms);
            if (i < 0) { pose = Resolve(keys[0], targets).Sanitized(); return true; }
            if (i >= keys.Count - 1) { pose = Resolve(keys[keys.Count - 1], targets).Sanitized(); return true; }

            var a = keys[i];
            double start = a.timeMs + Hold(a);
            double end = keys[i + 1].timeMs;
            if (ms <= start) { pose = Resolve(a, targets).Sanitized(); return true; }

            // A hold running past the next key leaves no transition time: arrive at once.
            float t = end > start ? (float)((ms - start) / (end - start)) : 1f;
            pose = BlendSegment(keys, i, t, targets).Sanitized();
            return true;
        }

        /// Blends key `i` into key `i + 1` at linear progress `t` (0..1), applying the incoming
        /// key's path and ease. Ease may overshoot (Back, Elastic); the result is not clamped
        /// except by `CameraPose.Sanitized`.
        public static CameraPose BlendSegment(List<CameraKeyframe> keys, int i, float t, ICameraTargets targets)
        {
            var a = keys[i];
            var b = keys[i + 1];
            var pa = Resolve(a, targets);
            var pb = Resolve(b, targets);

            if (b.path == PathMode.Cut) return t >= 1f ? pb : pa;

            float u = EaseOf(b, t);
            var p = pa;
            if (b.path == PathMode.Spline)
            {
                Vector3 p0 = i > 0 ? Resolve(keys[i - 1], targets).position : pa.position;
                Vector3 p3 = i + 2 < keys.Count ? Resolve(keys[i + 2], targets).position : pb.position;
                p.position = CatmullRom(p0, pa.position, pb.position, p3, u);
            }
            else
            {
                p.position = Vector3.LerpUnclamped(pa.position, pb.position, u);
            }
            p.rotation = Quaternion.SlerpUnclamped(pa.rotation, pb.rotation, u);
            p.fov = pa.fov + (pb.fov - pa.fov) * u;
            BlendLens(ref p, in pa, in pb, u);
            return p;
        }

        /// Blends two poses directly (sequence blend-in/out). `u` is already eased.
        public static CameraPose Blend(in CameraPose a, in CameraPose b, float u)
        {
            var p = a;
            p.position = Vector3.LerpUnclamped(a.position, b.position, u);
            p.rotation = Quaternion.SlerpUnclamped(a.rotation, b.rotation, u);
            p.fov = a.fov + (b.fov - a.fov) * u;
            BlendLens(ref p, in a, in b, u);
            return p.Sanitized();
        }

        /// A key's pose with its follow/look-at targets applied. A missing target falls back to
        /// the raw pose, so an unbound name degrades to a static shot instead of breaking.
        public static CameraPose Resolve(CameraKeyframe key, ICameraTargets targets)
        {
            var p = key.pose;
            if (targets == null) return p;

            if (!string.IsNullOrEmpty(key.follow) && targets.TryGetPosition(key.follow, out var anchor))
            {
                if (key.followRotation && targets.TryGetRotation(key.follow, out var frame))
                {
                    p.position = anchor + frame * p.position;
                    p.rotation = frame * p.rotation;
                }
                else
                {
                    p.position = anchor + p.position;
                }
            }

            if (!string.IsNullOrEmpty(key.lookAt) && targets.TryGetPosition(key.lookAt, out var aim))
            {
                Vector3 dir = aim - p.position;
                if (dir.sqrMagnitude > 1e-6f)
                    p.rotation = Quaternion.LookRotation(dir, Vector3.up) * Quaternion.Euler(0f, 0f, RollOf(key.pose.rotation));
            }
            return p;
        }

        public static float EaseOf(CameraKeyframe key, float t)
        {
            if (key.useCurve && key.curve != null && key.curve.length > 0)
            {
                if (float.IsNaN(t) || t <= 0f) t = 0f;
                else if (t > 1f) t = 1f;
                float v = key.curve.Evaluate(t);
                return float.IsNaN(v) ? t : v;
            }
            return Easing.Evaluate(key.ease, t);
        }

        /// Uniform Catmull-Rom: passes through `p1` at u = 0 and `p2` at u = 1.
        public static Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float u)
        {
            float u2 = u * u, u3 = u2 * u;
            return 0.5f * (2f * p1
                           + (p2 - p0) * u
                           + (2f * p0 - 5f * p1 + 4f * p2 - p3) * u2
                           + (3f * p1 - p0 - 3f * p2 + p3) * u3);
        }

        /// How many keys the playhead reached moving from `fromMs` (exclusive) to `toMs`
        /// (inclusive), and the first one's index. Used to fire per-key shake exactly once.
        public static int KeysReached(List<CameraKeyframe> keys, double fromMs, double toMs, out int first)
        {
            first = -1;
            if (keys == null || keys.Count == 0 || !(toMs > fromMs)) return 0;
            int a = FindKey(keys, fromMs);
            int b = FindKey(keys, toMs);
            // Keys at exactly fromMs were reached by the previous step.
            if (b <= a) return 0;
            first = a + 1;
            return b - a;
        }

        /// Stable insertion sort by time: keys on the same millisecond keep their order.
        public static void Sort(List<CameraKeyframe> keys)
        {
            if (keys == null) return;
            for (int i = 1; i < keys.Count; i++)
            {
                var k = keys[i];
                int j = i - 1;
                while (j >= 0 && keys[j].timeMs > k.timeMs) { keys[j + 1] = keys[j]; j--; }
                keys[j + 1] = k;
            }
        }

        private static double Hold(CameraKeyframe k) => double.IsNaN(k.holdMs) || k.holdMs < 0 ? 0 : k.holdMs;

        private static float RollOf(Quaternion q)
        {
            float z = q.eulerAngles.z;
            return z > 180f ? z - 360f : z;
        }

        // A side without DoF counts as deep focus (max f-stop at the other side's distance), so
        // depth of field fades in and out across a transition instead of popping.
        private static void BlendLens(ref CameraPose p, in CameraPose a, in CameraPose b, float u)
        {
            p.dof = a.dof || b.dof;
            if (!p.dof) return;
            float fa = a.dof ? a.focusDistance : b.focusDistance;
            float fb = b.dof ? b.focusDistance : a.focusDistance;
            float aa = a.dof ? a.aperture : CameraPose.MaxAperture;
            float ab = b.dof ? b.aperture : CameraPose.MaxAperture;
            p.focusDistance = fa + (fb - fa) * u;
            p.aperture = aa + (ab - aa) * u;
        }
    }
}
