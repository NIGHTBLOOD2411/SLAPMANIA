using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools.Constraints;
using SlapMania.Core.Motion;
using Is = UnityEngine.TestTools.Constraints.Is;

namespace SlapMania.Tests.EditMode
{
    public class EasingTests
    {
        private static readonly Ease[] All = (Ease[])System.Enum.GetValues(typeof(Ease));

        [Test]
        public void EveryEase_MapsZeroToZero_AndOneToOne()
        {
            foreach (var e in All)
            {
                Assert.AreEqual(0f, Easing.Evaluate(e, 0f), 1e-4f, e + " at 0");
                Assert.AreEqual(1f, Easing.Evaluate(e, 1f), 1e-4f, e + " at 1");
            }
        }

        [Test]
        public void OutOfRange_And_NaN_AreClamped()
        {
            foreach (var e in All)
            {
                Assert.AreEqual(0f, Easing.Evaluate(e, -3f), e + " negative");
                Assert.AreEqual(1f, Easing.Evaluate(e, 42f), e + " above 1");
                Assert.AreEqual(0f, Easing.Evaluate(e, float.NaN), e + " NaN");
            }
        }

        [Test]
        public void Linear_IsIdentity_AndOutBackOvershoots()
        {
            Assert.AreEqual(0.37f, Easing.Evaluate(Ease.Linear, 0.37f), 1e-6f);
            Assert.Greater(Easing.Evaluate(Ease.OutBack, 0.6f), 1f, "OutBack should overshoot past 1 mid-way");
        }

        [Test]
        public void InCurves_MirrorTheirOutCurves()
        {
            // f_in(t) = 1 - f_out(1 - t) for each symmetric family.
            var pairs = new[]
            {
                (Ease.InSine, Ease.OutSine), (Ease.InQuint, Ease.OutQuint), (Ease.InCirc, Ease.OutCirc),
                (Ease.InElastic, Ease.OutElastic), (Ease.InBounce, Ease.OutBounce)
            };
            foreach (var (inE, outE) in pairs)
                foreach (var t in new[] { 0.1f, 0.33f, 0.5f, 0.8f })
                    Assert.AreEqual(1f - Easing.Evaluate(outE, 1f - t), Easing.Evaluate(inE, t), 1e-4f, inE + " at " + t);
        }

        [Test]
        public void InOutCurves_PassThroughHalf_AtHalf()
        {
            foreach (var e in new[] { Ease.InOutSine, Ease.InOutQuart, Ease.InOutQuint, Ease.InOutExpo,
                                      Ease.InOutCirc, Ease.InOutBack, Ease.InOutElastic, Ease.InOutBounce })
                Assert.AreEqual(0.5f, Easing.Evaluate(e, 0.5f), 1e-3f, e.ToString());
        }

        [Test]
        public void InOutBounce_IsHalfInBounce_ThenHalfOutBounce()
        {
            Assert.AreEqual(Easing.Evaluate(Ease.InBounce, 0.5f) / 2f, Easing.Evaluate(Ease.InOutBounce, 0.25f), 1e-4f);
            Assert.AreEqual(0.5f + Easing.Evaluate(Ease.OutBounce, 0.5f) / 2f, Easing.Evaluate(Ease.InOutBounce, 0.75f), 1e-4f);
        }
    }

    public class TweenerTests
    {
        private GameObject _go;
        private Transform T => _go.transform;

        [SetUp] public void SetUp() { _go = new GameObject("tween-target"); }
        [TearDown] public void TearDown() { if (_go != null) Object.DestroyImmediate(_go); }

        private sealed class Sink : ITweenSink, ITweenCallback
        {
            public float Last = float.NaN;
            public int Completed;
            public int LastId = -1;
            public void OnTweenValue(int channel, float value) { Last = value; }
            public void OnTweenComplete(int id) { Completed++; LastId = id; }
        }

        [Test]
        public void Scale_ReachesTargetExactly_AtDuration_ThenDeactivates()
        {
            var tw = new Tweener(4);
            var h = tw.Scale(T, 0f, 2f, 0.5f, Ease.OutBack);
            Assert.AreEqual(Vector3.zero, T.localScale, "from is applied immediately");
            tw.Tick(0.25f);
            Assert.IsTrue(h.IsActive);
            tw.Tick(0.25f);
            Assert.AreEqual(new Vector3(2f, 2f, 2f), T.localScale);
            Assert.IsFalse(h.IsActive);
            Assert.AreEqual(0, tw.ActiveCount);
        }

        [Test]
        public void HugeDt_CompletesWithoutOvershoot()
        {
            var tw = new Tweener(4);
            tw.LocalMove(T, Vector3.zero, new Vector3(10f, 0f, 0f), 0.2f, Ease.OutElastic);
            tw.Tick(10000f);
            Assert.AreEqual(10f, T.localPosition.x, 1e-5f);
            Assert.AreEqual(0, tw.ActiveCount);
        }

        [Test]
        public void NegativeAndNaNDt_DoNotAdvance()
        {
            var tw = new Tweener(4);
            var h = tw.Scale(T, 1f, 3f, 1f, Ease.Linear);
            tw.Tick(-5f);
            tw.Tick(float.NaN);
            Assert.AreEqual(1f, T.localScale.x, 1e-6f);
            Assert.IsTrue(h.IsActive);
        }

        [Test]
        public void ZeroDuration_CompletesOnFirstTick()
        {
            var tw = new Tweener(4);
            tw.Scale(T, 1f, 5f, 0f);
            tw.Tick(0f);
            Assert.AreEqual(5f, T.localScale.x, 1e-6f);
            Assert.AreEqual(0, tw.ActiveCount);
        }

        [Test]
        public void Delay_HoldsTheStartValue_ThenRuns()
        {
            var tw = new Tweener(4);
            tw.Scale(T, 0f, 1f, 1f, Ease.Linear, delay: 0.5f);
            tw.Tick(0.4f);
            Assert.AreEqual(0f, T.localScale.x, 1e-6f, "still waiting");
            tw.Tick(0.6f); // 0.1 into the delay budget + 0.5 of the tween
            Assert.AreEqual(0.5f, T.localScale.x, 1e-4f, "leftover delay time is carried into the tween");
        }

        [Test]
        public void Yoyo_ReturnsToStart_AfterTwoCycles()
        {
            var tw = new Tweener(4);
            var h = tw.Scale(T, 1f, 2f, 0.5f, Ease.Linear).SetLoops(2, LoopType.Yoyo);
            tw.Tick(0.5f);
            Assert.IsTrue(h.IsActive, "first cycle done, second running");
            tw.Tick(0.5f);
            Assert.AreEqual(1f, T.localScale.x, 1e-4f);
            Assert.IsFalse(h.IsActive);
        }

        [Test]
        public void InfiniteLoops_NeverComplete()
        {
            var tw = new Tweener(4);
            var h = tw.Scale(T, 1f, 2f, 0.1f).SetLoops(-1, LoopType.Yoyo);
            for (int i = 0; i < 1000; i++) tw.Tick(0.037f);
            Assert.IsTrue(h.IsActive);
        }

        [Test]
        public void NewTween_OnSameTargetAndProperty_ReplacesTheOld()
        {
            var tw = new Tweener(4);
            var first = tw.Scale(T, 1f, 2f, 1f);
            var second = tw.Scale(T, 1f, 3f, 1f);
            Assert.IsFalse(first.IsActive);
            Assert.IsTrue(second.IsActive);
            Assert.AreEqual(1, tw.ActiveCount);
        }

        [Test]
        public void DifferentProperties_OnSameTarget_RunTogether()
        {
            var tw = new Tweener(4);
            tw.Scale(T, 1f, 2f, 1f);
            tw.RotateZ(T, 0f, 90f, 1f);
            Assert.AreEqual(2, tw.ActiveCount);
        }

        [Test]
        public void FullCapacity_SnapsNewTweenToItsEnd_AndReturnsDeadHandle()
        {
            var tw = new Tweener(1);
            var other = new GameObject("other");
            try
            {
                tw.Scale(other.transform, 1f, 2f, 1f);
                var h = tw.LocalMove(T, Vector3.zero, new Vector3(7f, 0f, 0f), 1f);
                Assert.IsFalse(h.IsActive);
                Assert.AreEqual(7f, T.localPosition.x, 1e-6f, "never stranded half-way");
                Assert.AreEqual(1, tw.ActiveCount);
            }
            finally { Object.DestroyImmediate(other); }
        }

        [Test]
        public void DestroyedTarget_IsDroppedWithoutThrowing()
        {
            var tw = new Tweener(4);
            tw.Scale(T, 1f, 2f, 1f);
            Object.DestroyImmediate(_go);
            Assert.DoesNotThrow(() => tw.Tick(0.1f));
            Assert.AreEqual(0, tw.ActiveCount);
        }

        [Test]
        public void Float_DrivesTheSink_AndCallbackFiresOnce()
        {
            var tw = new Tweener(4);
            var sink = new Sink();
            tw.Float(sink, 3, 10f, 20f, 1f, Ease.Linear).OnComplete(sink, 77);
            tw.Tick(0.5f);
            Assert.AreEqual(15f, sink.Last, 1e-4f);
            tw.Tick(0.5f);
            tw.Tick(0.5f);
            Assert.AreEqual(20f, sink.Last, 1e-4f);
            Assert.AreEqual(1, sink.Completed);
            Assert.AreEqual(77, sink.LastId);
        }

        [Test]
        public void Delay_Timer_FiresCallbackAfterSeconds()
        {
            var tw = new Tweener(4);
            var sink = new Sink();
            tw.Delay(0.3f, sink, 5);
            tw.Tick(0.2f);
            Assert.AreEqual(0, sink.Completed);
            tw.Tick(0.2f);
            Assert.AreEqual(1, sink.Completed);
            Assert.AreEqual(5, sink.LastId);
        }

        [Test]
        public void ShakeAndPunch_ReturnToRest()
        {
            var tw = new Tweener(4);
            T.localPosition = new Vector3(1f, 2f, 0f);
            T.localScale = Vector3.one;
            tw.Shake(T, new Vector3(5f, 5f, 0f), 0.3f);
            tw.Punch(T, 0.4f, 0.3f);
            tw.Tick(0.1f);
            tw.Tick(0.5f);
            Assert.AreEqual(new Vector3(1f, 2f, 0f), T.localPosition);
            Assert.AreEqual(Vector3.one, T.localScale);
        }

        [Test]
        public void RestartedPunchAndShake_MidFlight_KeepTheOriginalRest()
        {
            var tw = new Tweener(4);
            T.localScale = Vector3.one;
            T.localPosition = Vector3.zero;
            for (int i = 0; i < 12; i++)   // a tap every 60 ms, each restarting the punch mid-kick
            {
                tw.Punch(T, 0.3f, 0.16f, 3);
                tw.Shake(T, new Vector3(4f, 4f, 0f), 0.16f);
                tw.Tick(0.06f);
            }
            tw.Tick(1f);
            Assert.AreEqual(Vector3.one, T.localScale, "punch must not ratchet the scale");
            Assert.AreEqual(Vector3.zero, T.localPosition, "shake must not drift the position");
        }

        [Test]
        public void Kill_StopsWhereItIs()
        {
            var tw = new Tweener(4);
            var h = tw.Scale(T, 0f, 1f, 1f, Ease.Linear);
            tw.Tick(0.25f);
            h.Kill();
            tw.Tick(1f);
            Assert.AreEqual(0.25f, T.localScale.x, 1e-4f);
            Assert.IsFalse(h.IsActive);
        }

        [Test]
        public void StaleHandle_IsHarmless_AfterSlotReuse()
        {
            var tw = new Tweener(1);
            var old = tw.Scale(T, 0f, 1f, 0.1f);
            tw.Tick(1f); // finishes, frees the slot
            var fresh = tw.RotateZ(T, 0f, 45f, 1f);
            old.Kill();   // must not kill the tween now living in the same slot
            Assert.IsTrue(fresh.IsActive);
        }

        [Test]
        public void Tick_WithRunningTweens_DoesNotAllocate()
        {
            var tw = new Tweener(8);
            var sink = new Sink();
            tw.Scale(T, 1f, 2f, 100f).SetLoops(-1, LoopType.Yoyo);
            tw.Shake(T, new Vector3(3f, 3f, 0f), 100f);
            tw.Float(sink, 0, 0f, 1f, 100f);
            tw.Tick(0.016f); // warm-up
            Assert.That(() => tw.Tick(0.016f), Is.Not.AllocatingGCMemory());
        }
    }
}
