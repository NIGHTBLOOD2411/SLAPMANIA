using UnityEngine;

namespace SlapMania.Core.Motion
{
    /// A damped angular spring that pulls a simulated rotation toward a target (M08 physical
    /// animation, ADR-0024). Pure: time arrives as `dt`, nothing is read from Time (R2).
    ///
    /// The rotation and the target live in the same space (a bone's parent space when used for
    /// physical animation); `velocity` is an angular velocity in that space, radians/second,
    /// as a rotation vector. Semi-implicit Euler on fixed sub-steps: unconditionally stable for
    /// the frequencies used here, and a long frame is clamped, never integrated in one leap.
    public static class RotationSpring
    {
        /// Sub-step length and the most simulated time one call may advance. A longer `dt` (a
        /// hitch, a resume from the pause menu) is clamped to MaxDt: the spring loses that time
        /// instead of exploding.
        public const float SubStep = 1f / 120f;
        public const float MaxDt = 1f / 15f;

        /// Advances `current` / `velocity` by `dt` toward `target`. `frequencyHz` <= 0 (or any
        /// NaN input) snaps to the target at rest. `maxDeviationDeg` > 0 keeps the result within
        /// that angle of the target.
        public static void Step(ref Quaternion current, ref Vector3 velocity, Quaternion target,
                                float frequencyHz, float dampingRatio, float dt, float maxDeviationDeg = 0f)
        {
            if (!(dt > 0f)) return;                                   // also rejects NaN
            if (!(frequencyHz > 0f) || float.IsInfinity(frequencyHz) || float.IsNaN(dampingRatio))
            {
                current = target;
                velocity = Vector3.zero;
                return;
            }
            if (dt > MaxDt) dt = MaxDt;

            float omega = 2f * Mathf.PI * frequencyHz;
            float k = omega * omega;
            float c = 2f * Mathf.Max(0f, dampingRatio) * omega;
            int steps = Mathf.Max(1, Mathf.CeilToInt(dt / SubStep));
            float h = dt / steps;

            for (int i = 0; i < steps; i++)
            {
                Vector3 error = ToRotationVector(target * Quaternion.Inverse(current));
                velocity += (error * k - velocity * c) * h;
                current = FromRotationVector(velocity * h) * current;
            }
            current = Normalize(current);

            if (maxDeviationDeg > 0f)
            {
                float angle = Quaternion.Angle(current, target);
                if (angle > maxDeviationDeg)
                {
                    current = Quaternion.Slerp(target, current, maxDeviationDeg / angle);
                    velocity *= 0.5f;   // hitting the limit bleeds energy, like a joint stop
                }
            }

            if (float.IsNaN(current.x) || float.IsNaN(velocity.x))
            {
                current = target;
                velocity = Vector3.zero;
            }
        }

        /// The shortest-arc rotation vector (axis * radians) of `q`.
        public static Vector3 ToRotationVector(Quaternion q)
        {
            if (q.w < 0f) { q.x = -q.x; q.y = -q.y; q.z = -q.z; q.w = -q.w; }
            float s = Mathf.Sqrt(q.x * q.x + q.y * q.y + q.z * q.z);
            if (s < 1e-6f) return new Vector3(q.x, q.y, q.z) * 2f;    // small angle: 2 * imaginary part
            float angle = 2f * Mathf.Atan2(s, q.w);
            return new Vector3(q.x, q.y, q.z) * (angle / s);
        }

        public static Quaternion FromRotationVector(Vector3 v)
        {
            float angle = v.magnitude;
            if (angle < 1e-6f) return Normalize(new Quaternion(v.x * 0.5f, v.y * 0.5f, v.z * 0.5f, 1f));
            float half = angle * 0.5f;
            float s = Mathf.Sin(half) / angle;
            return new Quaternion(v.x * s, v.y * s, v.z * s, Mathf.Cos(half));
        }

        private static Quaternion Normalize(Quaternion q)
        {
            float m = Mathf.Sqrt(q.x * q.x + q.y * q.y + q.z * q.z + q.w * q.w);
            if (m < 1e-6f) return Quaternion.identity;
            return new Quaternion(q.x / m, q.y / m, q.z / m, q.w / m);
        }
    }
}
