using NUnit.Framework;
using UnityEngine;
using SlapMania.Core.Motion;

namespace SlapMania.Tests.EditMode
{
    /// RotationSpring (M08 physical animation, ADR-0024): the bone spring behind the whip and
    /// the hit wobble. R3 boundaries: 0 / NaN / negative / huge dt, 0 frequency, the limit.
    public sealed class RotationSpringTests
    {
        private static readonly Quaternion Target = Quaternion.Euler(0f, 60f, 0f);

        private static float Settle(float hz, float zeta, float seconds, float dt = 1f / 60f)
        {
            var q = Quaternion.identity;
            var v = Vector3.zero;
            for (float t = 0f; t < seconds; t += dt) RotationSpring.Step(ref q, ref v, Target, hz, zeta, dt);
            return Quaternion.Angle(q, Target);
        }

        [Test]
        public void ConvergesOnTheTarget() => Assert.Less(Settle(4f, 0.6f, 3f), 0.5f);

        [Test]
        public void LowDamping_Overshoots()
        {
            var q = Quaternion.identity;
            var v = Vector3.zero;
            float maxYaw = 0f;
            for (int i = 0; i < 120; i++)
            {
                RotationSpring.Step(ref q, ref v, Target, 4f, 0.25f, 1f / 60f);
                float yaw = Mathf.DeltaAngle(0f, q.eulerAngles.y);
                if (yaw > maxYaw) maxYaw = yaw;
            }
            Assert.Greater(maxYaw, 62f, "an underdamped spring should swing past 60 degrees (follow-through)");
        }

        [Test]
        public void CriticalDamping_DoesNotOvershootMuch()
        {
            var q = Quaternion.identity;
            var v = Vector3.zero;
            float maxYaw = 0f;
            for (int i = 0; i < 180; i++)
            {
                RotationSpring.Step(ref q, ref v, Target, 4f, 1f, 1f / 60f);
                maxYaw = Mathf.Max(maxYaw, Mathf.DeltaAngle(0f, q.eulerAngles.y));
            }
            Assert.LessOrEqual(maxYaw, 60.5f);
        }

        [TestCase(0f)]
        [TestCase(-0.1f)]
        [TestCase(float.NaN)]
        public void NoTime_ChangesNothing(float dt)
        {
            var q = Quaternion.identity;
            var v = new Vector3(1f, 0f, 0f);
            RotationSpring.Step(ref q, ref v, Target, 4f, 0.5f, dt);
            Assert.AreEqual(0f, Quaternion.Angle(q, Quaternion.identity), 1e-4f);
            Assert.AreEqual(1f, v.x);
        }

        [TestCase(0f)]
        [TestCase(-3f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void NoStiffness_SnapsToTargetAtRest(float hz)
        {
            var q = Quaternion.identity;
            var v = new Vector3(5f, 5f, 5f);
            RotationSpring.Step(ref q, ref v, Target, hz, 0.5f, 1f / 60f);
            Assert.AreEqual(0f, Quaternion.Angle(q, Target), 1e-3f);
            Assert.AreEqual(Vector3.zero, v);
        }

        [Test]
        public void HugeDt_IsClampedAndStaysFinite()
        {
            var q = Quaternion.identity;
            var v = Vector3.zero;
            RotationSpring.Step(ref q, ref v, Target, 12f, 0.1f, 1000f);
            Assert.IsFalse(float.IsNaN(q.x) || float.IsNaN(v.x));
            Assert.AreEqual(1f, Mathf.Sqrt(q.x * q.x + q.y * q.y + q.z * q.z + q.w * q.w), 1e-4f);
        }

        [Test]
        public void HugeImpulse_IsHeldByTheDeviationLimit()
        {
            var q = Target;
            var v = new Vector3(0f, 400f, 0f);   // a slap far beyond anything real
            RotationSpring.Step(ref q, ref v, Target, 4f, 0.5f, 1f / 60f, maxDeviationDeg: 50f);
            Assert.LessOrEqual(Quaternion.Angle(q, Target), 50.01f);
        }

        [Test]
        public void RotationVector_RoundTrips()
        {
            var r = Quaternion.Euler(20f, -130f, 75f);
            var back = RotationSpring.FromRotationVector(RotationSpring.ToRotationVector(r));
            Assert.AreEqual(0f, Quaternion.Angle(r, back), 1e-2f);
        }

        [Test]
        public void RotationVector_TakesTheShortArc()
        {
            // q and -q are the same rotation; the vector must be the short way round (< pi).
            var q = Quaternion.Euler(0f, 170f, 0f);
            var neg = new Quaternion(-q.x, -q.y, -q.z, -q.w);
            Assert.LessOrEqual(RotationSpring.ToRotationVector(neg).magnitude, Mathf.PI + 1e-4f);
        }
    }
}
