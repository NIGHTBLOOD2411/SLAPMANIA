using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using SlapMania.Core.Cinematics;
using SlapMania.Core.Motion;

namespace SlapMania.Tests.EditMode
{
    public class CameraTrackTests
    {
        private const float Eps = 1e-4f;

        private sealed class Targets : ICameraTargets
        {
            public readonly Dictionary<string, Vector3> Map = new Dictionary<string, Vector3>();
            public readonly Dictionary<string, Quaternion> Rot = new Dictionary<string, Quaternion>();
            public bool TryGetPosition(string name, out Vector3 position) => Map.TryGetValue(name, out position);
            public bool TryGetRotation(string name, out Quaternion rotation) => Rot.TryGetValue(name, out rotation);
        }

        private static CameraKeyframe Key(double ms, Vector3 pos, float fov = 50f, PathMode path = PathMode.Linear,
                                          Ease ease = Ease.Linear, double hold = 0)
            => new CameraKeyframe
            {
                timeMs = ms, holdMs = hold, path = path, ease = ease,
                pose = new CameraPose(pos, Quaternion.identity, fov)
            };

        private static CameraPose Eval(List<CameraKeyframe> keys, double ms, ICameraTargets t = null)
        {
            Assert.IsTrue(CameraTrack.TryEvaluate(keys, ms, t, out var p));
            return p;
        }

        private static void AreClose(Vector3 expected, Vector3 actual, string msg = null)
            => Assert.Less(Vector3.Distance(expected, actual), Eps, msg + " expected " + expected + " got " + actual);

        // ── Empty / single / out-of-range time ───────────────────────

        [Test]
        public void EmptyOrNullTrack_DoesNotEvaluate()
        {
            Assert.IsFalse(CameraTrack.TryEvaluate(new List<CameraKeyframe>(), 0, null, out _));
            Assert.IsFalse(CameraTrack.TryEvaluate(null, 0, null, out _));
            Assert.AreEqual(0, CameraTrack.DurationMs(null));
        }

        [Test]
        public void SingleKey_HoldsItsPose_AtAnyTime_IncludingNaN_Negative_Huge()
        {
            var keys = new List<CameraKeyframe> { Key(0, new Vector3(1, 2, 3)) };
            foreach (var ms in new[] { 0.0, -500.0, double.NaN, 1e12, double.PositiveInfinity })
                AreClose(new Vector3(1, 2, 3), Eval(keys, ms).position, "ms=" + ms);
        }

        [Test]
        public void BeforeFirstKey_HoldsFirst_AfterLast_HoldsLast()
        {
            var keys = new List<CameraKeyframe> { Key(500, Vector3.zero), Key(1500, Vector3.right) };
            AreClose(Vector3.zero, Eval(keys, 100).position);
            AreClose(Vector3.right, Eval(keys, 99999).position);
        }

        // ── Exact edges, hold, transition ────────────────────────────

        [Test]
        public void ExactKeyTimes_ReturnThatKeysPose()
        {
            var keys = new List<CameraKeyframe> { Key(0, Vector3.zero), Key(1000, Vector3.up * 4), Key(2000, Vector3.right) };
            AreClose(Vector3.zero, Eval(keys, 0).position);
            AreClose(Vector3.up * 4, Eval(keys, 1000).position);
            AreClose(Vector3.right, Eval(keys, 2000).position);
        }

        [Test]
        public void Linear_Midpoint_IsHalfway_ForPositionAndFov()
        {
            var keys = new List<CameraKeyframe> { Key(0, Vector3.zero, 40f), Key(1000, new Vector3(2, 0, 0), 60f) };
            var p = Eval(keys, 500);
            AreClose(new Vector3(1, 0, 0), p.position);
            Assert.AreEqual(50f, p.fov, Eps);
        }

        [Test]
        public void Hold_KeepsThePose_ThenTransitionUsesTheRemainingTime()
        {
            // Key 0 holds 0..400, transition 400..1000.
            var keys = new List<CameraKeyframe> { Key(0, Vector3.zero, hold: 400), Key(1000, new Vector3(6, 0, 0)) };
            AreClose(Vector3.zero, Eval(keys, 400).position, "end of hold");
            AreClose(new Vector3(3, 0, 0), Eval(keys, 700).position, "halfway through the 600 ms move");
            Assert.AreEqual(1000, CameraTrack.DurationMs(keys));
        }

        [Test]
        public void HoldOverlappingNextKey_HoldsUntilThatKey_ThenArrives()
        {
            var keys = new List<CameraKeyframe> { Key(0, Vector3.zero, hold: 5000), Key(1000, Vector3.right) };
            AreClose(Vector3.zero, Eval(keys, 999.9).position);
            AreClose(Vector3.right, Eval(keys, 1000).position);
        }

        [Test]
        public void NegativeOrNaNHold_CountsAsZero()
        {
            var keys = new List<CameraKeyframe> { Key(0, Vector3.zero, hold: -300), Key(1000, Vector3.right * 2) };
            AreClose(Vector3.right, Eval(keys, 500).position);
            keys[0].holdMs = double.NaN;
            AreClose(Vector3.right, Eval(keys, 500).position);
        }

        [Test]
        public void Cut_HoldsPrevious_UntilExactlyTheKeyTime()
        {
            var keys = new List<CameraKeyframe> { Key(0, Vector3.zero), Key(1000, Vector3.right, path: PathMode.Cut) };
            AreClose(Vector3.zero, Eval(keys, 999.999).position);
            AreClose(Vector3.right, Eval(keys, 1000).position);
        }

        [Test]
        public void Ease_IsAppliedFromTheIncomingKey()
        {
            var keys = new List<CameraKeyframe> { Key(0, Vector3.zero), Key(1000, Vector3.right, ease: Ease.InCubic) };
            Assert.AreEqual(0.125f, Eval(keys, 500).position.x, Eps);
        }

        [Test]
        public void OutBack_Overshoots_PastTheTarget()
        {
            var keys = new List<CameraKeyframe> { Key(0, Vector3.zero), Key(1000, Vector3.right, ease: Ease.OutBack) };
            Assert.Greater(Eval(keys, 700).position.x, 1f);
        }

        [Test]
        public void CustomCurve_ReplacesEase()
        {
            var b = Key(1000, Vector3.right, ease: Ease.Linear);
            b.useCurve = true;
            b.curve = AnimationCurve.Constant(0f, 1f, 0.25f);
            var keys = new List<CameraKeyframe> { Key(0, Vector3.zero), b };
            Assert.AreEqual(0.25f, Eval(keys, 500).position.x, Eps);
        }

        // ── Spline / rotation / lens ─────────────────────────────────

        [Test]
        public void Spline_PassesThroughEveryKey_AndMatchesLinear_OnEvenlySpacedCollinearKeys()
        {
            var keys = new List<CameraKeyframe>
            {
                Key(0, Vector3.zero, path: PathMode.Spline), Key(1000, Vector3.right, path: PathMode.Spline),
                Key(2000, Vector3.right * 2, path: PathMode.Spline), Key(3000, Vector3.right * 3, path: PathMode.Spline)
            };
            AreClose(Vector3.right, Eval(keys, 1000).position);
            AreClose(Vector3.right * 1.5f, Eval(keys, 1500).position);
        }

        [Test]
        public void Spline_Curves_ThroughACorner()
        {
            var keys = new List<CameraKeyframe>
            {
                Key(0, Vector3.zero), Key(1000, Vector3.right, path: PathMode.Spline),
                Key(2000, new Vector3(1, 0, 1), path: PathMode.Spline)
            };
            // The midpoint of the second leg bulges outside the straight chord x = 1.
            Assert.Greater(Eval(keys, 1500).position.x, 1f);
        }

        [Test]
        public void Rotation_Slerps()
        {
            var a = Key(0, Vector3.zero);
            var b = Key(1000, Vector3.zero);
            b.pose.rotation = Quaternion.Euler(0, 90, 0);
            var p = Eval(new List<CameraKeyframe> { a, b }, 500);
            Assert.AreEqual(45f, Quaternion.Angle(Quaternion.identity, p.rotation), 0.01f);
        }

        [Test]
        public void OvershootingFov_IsClampedIntoRange()
        {
            var keys = new List<CameraKeyframe> { Key(0, Vector3.zero, 170f), Key(1000, Vector3.zero, 178f, ease: Ease.OutElastic) };
            for (double ms = 0; ms <= 1000; ms += 25)
            {
                float fov = Eval(keys, ms).fov;
                Assert.That(fov, Is.InRange(CameraPose.MinFov, CameraPose.MaxFov), "ms=" + ms);
            }
        }

        [Test]
        public void Dof_FadesIn_FromDeepFocus()
        {
            var a = Key(0, Vector3.zero);
            var b = Key(1000, Vector3.zero);
            b.pose.dof = true; b.pose.focusDistance = 2f; b.pose.aperture = 2f;
            var p = Eval(new List<CameraKeyframe> { a, b }, 500);
            Assert.IsTrue(p.dof);
            Assert.AreEqual(2f, p.focusDistance, Eps);
            Assert.AreEqual((CameraPose.MaxAperture + 2f) / 2f, p.aperture, Eps);
        }

        [Test]
        public void Sanitized_RepairsNaN_AndZeroQuaternion()
        {
            var p = new CameraPose(new Vector3(float.NaN, 0, 0), new Quaternion(0, 0, 0, 0), float.NaN).Sanitized();
            Assert.AreEqual(Vector3.zero, p.position);
            Assert.AreEqual(Quaternion.identity, p.rotation);
            Assert.AreEqual(60f, p.fov);
        }

        // ── Targets ──────────────────────────────────────────────────

        [Test]
        public void LookAt_AimsAtTheTarget_KeepingRoll()
        {
            var t = new Targets(); t.Map["defender"] = new Vector3(0, 0, 10);
            var k = Key(0, Vector3.zero);
            k.lookAt = "defender";
            k.pose.rotation = Quaternion.Euler(30, 70, 12); // pitch/yaw are replaced, roll kept
            var p = Eval(new List<CameraKeyframe> { k }, 0, t);
            AreClose(Vector3.forward, p.rotation * Vector3.forward);
            Assert.AreEqual(12f, p.rotation.eulerAngles.z, 0.01f);
        }

        [Test]
        public void Follow_TreatsPositionAsOffset_FromTheTarget()
        {
            var t = new Targets(); t.Map["attacker"] = new Vector3(5, 0, 0);
            var k = Key(0, new Vector3(0, 1, -2));
            k.follow = "attacker";
            AreClose(new Vector3(5, 1, -2), Eval(new List<CameraKeyframe> { k }, 0, t).position);
        }

        [Test]
        public void FollowRotation_PutsTheShotInTheTargetsLocalSpace_SoATurnSwapMirrorsIt()
        {
            // "Behind and above the attacker, looking where they look".
            var k = Key(0, new Vector3(0, 1, -2));
            k.follow = "attacker"; k.followRotation = true;
            var t = new Targets();
            t.Map["attacker"] = new Vector3(0, 0, -1); t.Rot["attacker"] = Quaternion.identity;
            var p = Eval(new List<CameraKeyframe> { k }, 0, t);
            AreClose(new Vector3(0, 1, -3), p.position, "player attacking +z");
            AreClose(Vector3.forward, p.rotation * Vector3.forward);

            t.Map["attacker"] = new Vector3(0, 0, 1); t.Rot["attacker"] = Quaternion.Euler(0, 180, 0);
            p = Eval(new List<CameraKeyframe> { k }, 0, t);
            AreClose(new Vector3(0, 1, 3), p.position, "opponent attacking -z: same shot, mirrored");
            AreClose(Vector3.back, p.rotation * Vector3.forward);
        }

        [Test]
        public void FollowRotation_WithoutARotation_FallsBackToWorldOffset()
        {
            var k = Key(0, new Vector3(0, 1, -2));
            k.follow = "attacker"; k.followRotation = true;
            var t = new Targets(); t.Map["attacker"] = new Vector3(5, 0, 0);
            AreClose(new Vector3(5, 1, -2), Eval(new List<CameraKeyframe> { k }, 0, t).position);
        }

        [Test]
        public void MissingTarget_FallsBackToTheRawPose()
        {
            var k = Key(0, new Vector3(0, 1, -2));
            k.follow = "nobody"; k.lookAt = "nobody";
            var p = Eval(new List<CameraKeyframe> { k }, 0, new Targets());
            AreClose(new Vector3(0, 1, -2), p.position);
            Assert.AreEqual(Quaternion.identity, p.rotation);
        }

        // ── Lookup / crossing / sort / data ──────────────────────────

        [Test]
        public void FindKey_Boundaries()
        {
            var keys = new List<CameraKeyframe> { Key(100, Vector3.zero), Key(200, Vector3.zero), Key(300, Vector3.zero) };
            Assert.AreEqual(-1, CameraTrack.FindKey(keys, 99.999));
            Assert.AreEqual(0, CameraTrack.FindKey(keys, 100));
            Assert.AreEqual(0, CameraTrack.FindKey(keys, 199.999));
            Assert.AreEqual(1, CameraTrack.FindKey(keys, 200));
            Assert.AreEqual(2, CameraTrack.FindKey(keys, 1e9));
            Assert.AreEqual(-1, CameraTrack.FindKey(keys, double.NaN));
        }

        [Test]
        public void KeysReached_CountsEachKeyOnce_FromExclusive_ToInclusive()
        {
            var keys = new List<CameraKeyframe> { Key(0, Vector3.zero), Key(100, Vector3.zero), Key(200, Vector3.zero) };
            Assert.AreEqual(1, CameraTrack.KeysReached(keys, -1, 0, out int f)); Assert.AreEqual(0, f);
            Assert.AreEqual(0, CameraTrack.KeysReached(keys, 0, 99, out _));
            Assert.AreEqual(1, CameraTrack.KeysReached(keys, 99, 100, out f)); Assert.AreEqual(1, f);
            Assert.AreEqual(0, CameraTrack.KeysReached(keys, 100, 150, out _));
            Assert.AreEqual(2, CameraTrack.KeysReached(keys, 50, 1e9, out f), "a huge dt reaches every key"); Assert.AreEqual(1, f);
            Assert.AreEqual(0, CameraTrack.KeysReached(keys, 150, 50, out _), "backwards is never a crossing");
            Assert.AreEqual(0, CameraTrack.KeysReached(keys, double.NaN, 50, out _));
        }

        [Test]
        public void Sort_IsByTime_AndStable()
        {
            var a = Key(500, Vector3.zero); var b = Key(100, Vector3.zero);
            var c = Key(500, Vector3.one); var d = Key(0, Vector3.zero);
            var keys = new List<CameraKeyframe> { a, b, c, d };
            CameraTrack.Sort(keys);
            CollectionAssert.AreEqual(new[] { d, b, a, c }, keys);
        }

        [Test]
        public void SequenceData_SurvivesJsonRoundTrip_AndCloneIsDeep()
        {
            var k = Key(250, new Vector3(1, 2, 3), 33f, PathMode.Spline, Ease.InOutBack, 120);
            k.lookAt = "defender"; k.shake = 0.6f; k.shakePreset = 2;
            var data = new CameraSequenceData { name = "finisher", loop = true };
            data.keys.Add(k);

            var back = JsonUtility.FromJson<CameraSequenceData>(JsonUtility.ToJson(data));
            Assert.AreEqual("finisher", back.name);
            Assert.AreEqual(250, back.keys[0].timeMs);
            Assert.AreEqual(Ease.InOutBack, back.keys[0].ease);
            Assert.AreEqual("defender", back.keys[0].lookAt);
            AreClose(new Vector3(1, 2, 3), back.keys[0].pose.position);

            var clone = data.Clone();
            clone.keys[0].timeMs = 9;
            Assert.AreEqual(250, data.keys[0].timeMs);
        }
    }

    public class CameraShakeMathTests
    {
        private static readonly ShakeProfile Impact = new ShakeProfile("Impact", 0.15f, new Vector3(2, 2, 3), 25f, 3f);

        [Test]
        public void Add_ClampsTo01_AndIgnoresNaNOrNegative()
        {
            Assert.AreEqual(1f, CameraShakeMath.Add(0.8f, 0.5f));
            Assert.AreEqual(0.3f, CameraShakeMath.Add(0.3f, -1f));
            Assert.AreEqual(0.3f, CameraShakeMath.Add(0.3f, float.NaN));
            Assert.AreEqual(0.5f, CameraShakeMath.Add(float.NaN, 0.5f));
        }

        [Test]
        public void Decay_NeverGoesBelowFloor_EvenForAHugeDt()
        {
            Assert.AreEqual(0.7f, CameraShakeMath.Decay(1f, 3f, 0.1f), 1e-5f);
            Assert.AreEqual(0f, CameraShakeMath.Decay(1f, 3f, 1e9f));
            Assert.AreEqual(0.2f, CameraShakeMath.Decay(1f, 3f, 1e9f, 0.2f), 1e-6f);
            Assert.AreEqual(0.5f, CameraShakeMath.Decay(0.5f, 3f, float.NaN));
            Assert.AreEqual(0.5f, CameraShakeMath.Decay(0.5f, 3f, -1f));
        }

        [Test]
        public void ImpactTrauma_Boundaries()
        {
            Assert.AreEqual(0f, CameraShakeMath.ImpactTrauma(0f, 0.35f, 1f), "no hit, no shake");
            Assert.AreEqual(0f, CameraShakeMath.ImpactTrauma(-1f, 0.35f, 1f));
            Assert.AreEqual(0f, CameraShakeMath.ImpactTrauma(float.NaN, 0.35f, 1f));
            Assert.AreEqual(0.35f + 0.65f * 0.01f, CameraShakeMath.ImpactTrauma(0.01f, 0.35f, 1f), 1e-5f, "a weak hit gets the floor");
            Assert.AreEqual(1f, CameraShakeMath.ImpactTrauma(1f, 0.35f, 1f), 1e-6f);
            Assert.AreEqual(1f, CameraShakeMath.ImpactTrauma(0.5f, 0.35f, 1e6f), "huge scale clamps");
            Assert.AreEqual(1f, CameraShakeMath.ImpactTrauma(42f, 0f, 1f), "intensity above 1 clamps");
            Assert.AreEqual(0.5f, CameraShakeMath.ImpactTrauma(0.5f, 0f, 1f), 1e-6f, "min 0 + scale 1 = the old linear mapping");
            Assert.AreEqual(0.5f, CameraShakeMath.ImpactTrauma(0.5f, float.NaN, float.NaN), 1e-6f, "NaN settings fall back to 0 / 1");
        }

        [Test]
        public void ZeroTrauma_IsStill()
        {
            CameraShakeMath.Sample(Impact, 0f, 1.234, out var o, out var a);
            Assert.AreEqual(Vector3.zero, o);
            Assert.AreEqual(Vector3.zero, a);
        }

        [Test]
        public void Sample_StaysWithinProfileTimesTraumaSquared_AndIsDeterministic()
        {
            const float trauma = 0.5f;
            float s = trauma * trauma;
            for (double t = 0; t < 3; t += 0.013)
            {
                CameraShakeMath.Sample(Impact, trauma, t, out var o, out var a);
                Assert.LessOrEqual(Mathf.Abs(o.x), Impact.maxOffset * s + 1e-5f);
                Assert.LessOrEqual(Mathf.Abs(a.z), Impact.maxAngles.z * s + 1e-5f);
                CameraShakeMath.Sample(Impact, trauma, t, out var o2, out _);
                Assert.AreEqual(o, o2);
            }
        }

        [Test]
        public void Sample_HugeOrNaNTime_IsFinite()
        {
            CameraShakeMath.Sample(Impact, 1f, 1e15, out var o, out var a);
            Assert.IsFalse(float.IsNaN(o.x) || float.IsNaN(a.z));
            CameraShakeMath.Sample(Impact, 1f, double.NaN, out o, out _);
            Assert.AreEqual(Vector3.zero, o);
        }
    }
}
