using NUnit.Framework;
using SlapMania.Core;
using SlapMania.Gameplay;

namespace SlapMania.Tests.EditMode
{
    public class PausableClockTests
    {
        [Test]
        public void Unpaused_FollowsTheSource()
        {
            var src = new ManualClock(5.0);
            var c = new PausableClock(src);
            Assert.AreEqual(5.0, c.Now);
            src.Advance(1.25);
            Assert.AreEqual(6.25, c.Now);
            Assert.IsFalse(c.IsPaused);
        }

        [Test]
        public void Paused_HoldsStill_ThenCarriesOnWithoutAJump()
        {
            var src = new ManualClock(10.0);
            var c = new PausableClock(src);
            c.Pause();
            src.Advance(30.0);
            Assert.AreEqual(10.0, c.Now, "time passed while paused");
            c.Resume();
            Assert.AreEqual(10.0, c.Now, "resume jumped");
            src.Advance(0.5);
            Assert.AreEqual(10.5, c.Now);
            Assert.AreEqual(30.0, c.PausedTotal);
        }

        [Test]
        public void PauseAndResume_AreIdempotent_AndPausesAccumulate()
        {
            var src = new ManualClock();
            var c = new PausableClock(src);
            c.Resume();                // not paused: no-op
            Assert.AreEqual(0.0, c.PausedTotal);
            c.Pause(); src.Advance(2.0);
            c.Pause(); src.Advance(3.0); // second Pause must not restart the pause
            c.Resume(); c.Resume();
            Assert.AreEqual(5.0, c.PausedTotal);
            src.Advance(1.0);
            c.Pause(); src.Advance(4.0); c.Resume();
            Assert.AreEqual(9.0, c.PausedTotal);
            Assert.AreEqual(1.0, c.Now);
        }

        [Test]
        public void ZeroLengthPause_ChangesNothing()
        {
            var src = new ManualClock(3.0);
            var c = new PausableClock(src);
            c.Pause(); c.Resume();
            Assert.AreEqual(0.0, c.PausedTotal);
            Assert.AreEqual(3.0, c.Now);
            Assert.AreEqual(3.0, c.FromSource(3.0));
        }

        [Test]
        public void HugePause_IsExact()
        {
            var src = new ManualClock(1.0);
            var c = new PausableClock(src);
            c.Pause(); src.Advance(86400.0 * 7); c.Resume(); // a week in the background
            src.Advance(0.25);
            Assert.AreEqual(1.25, c.Now, 1e-9);
        }

        [Test]
        public void SourceRunningBackwards_OrNaN_IsNotCountedAsPausedTime()
        {
            var src = new ManualClock(10.0);
            var c = new PausableClock(src);
            c.Pause(); src.Set(4.0); c.Resume();
            Assert.AreEqual(0.0, c.PausedTotal, "negative pause applied");
            c.Pause(); src.Set(double.NaN); c.Resume();
            Assert.AreEqual(0.0, c.PausedTotal, "NaN pause applied");
        }

        [Test]
        public void FromSource_MapsEventTimesOntoTheClock()
        {
            var src = new ManualClock(2.0);
            var c = new PausableClock(src);
            c.Pause(); src.Advance(7.0); c.Resume();
            src.Advance(0.1);
            // An input event stamped "now" on the source is "now" on the clock.
            Assert.AreEqual(c.Now, c.FromSource(src.Now), 1e-12);
            Assert.AreEqual(2.0, c.FromSource(9.0), 1e-12);
        }

        [Test]
        public void WasPausedAt_CoversThePauseAndEventsDeliveredLate()
        {
            var src = new ManualClock(1.0);
            var c = new PausableClock(src);
            Assert.IsFalse(c.WasPausedAt(0.5), "no pause yet");
            c.Pause();
            Assert.IsTrue(c.WasPausedAt(1.0));
            src.Advance(2.0);
            c.Resume();                        // resumed at source 3.0
            Assert.IsTrue(c.WasPausedAt(2.9), "made during the pause, delivered after resume");
            Assert.IsFalse(c.WasPausedAt(3.0), "the resume instant itself is live");
            Assert.IsFalse(c.WasPausedAt(3.2));
        }

        // ── PausableTapSource ────────────────────────────────────────
        [Test]
        public void TapGate_DropsPausedTaps_AndRestampsTheRest()
        {
            var src = new ManualClock(0.0);
            var clock = new PausableClock(src);
            var inner = new ManualTapSource();
            var gate = new PausableTapSource(inner, clock);
            int count = 0; double last = -1;
            gate.OnTap += e => { count++; last = e.EventTime; };
            gate.Enable();

            inner.Push(0.0);
            Assert.AreEqual(1, count);
            Assert.AreEqual(0.0, last);

            clock.Pause();
            src.Advance(5.0);
            inner.Push(src.Now);                 // tapped on the pause menu
            Assert.AreEqual(1, count, "a paused tap got through");
            clock.Resume();
            inner.Push(4.0);                     // made during the pause, delivered late
            Assert.AreEqual(1, count, "a late paused tap got through");

            src.Advance(0.25);
            inner.Push(src.Now);
            Assert.AreEqual(2, count);
            Assert.AreEqual(0.25, last, 1e-12, "not re-stamped onto the clock");

            inner.Push(double.NaN);
            Assert.AreEqual(2, count, "NaN tap got through");
        }

        [Test]
        public void TapGate_EnableDisable_PassThrough_AndDetachStopsForwarding()
        {
            var clock = new PausableClock(new ManualClock());
            var inner = new ManualTapSource();
            var gate = new PausableTapSource(inner, clock);
            int count = 0;
            gate.OnTap += _ => count++;
            inner.Push(0.0);
            Assert.AreEqual(0, count, "disabled inner must not emit");
            gate.Enable();
            inner.Push(0.0);
            Assert.AreEqual(1, count);
            gate.Disable();
            inner.Push(0.0);
            Assert.AreEqual(1, count);
            gate.Enable();
            gate.Detach();
            inner.Push(0.0);
            Assert.AreEqual(1, count, "detached gate still forwards");
        }
    }
}
