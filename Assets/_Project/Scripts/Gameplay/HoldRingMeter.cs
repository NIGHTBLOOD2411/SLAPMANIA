using System;
using UnityEngine;
using SlapMania.Core;
using SlapMania.Data;
using SlapMania.Logging;

namespace SlapMania.Gameplay
{
    /// The V2 hold-ring attack (M04 §5b, ADR-0014): press and hold to wind up while the ring
    /// closes, release on the gold band. One gesture yields both halves of a slap - a
    /// PowerResult (the wind-up) and a TimingResult (where the ring was) - so M05 and SlapStats
    /// read it exactly as they read M03 + M04.
    ///
    /// Pure C#: no MonoBehaviour, no Time.* read. The ring's closure is a pure function of the
    /// press's own event time (HoldRing.ClosureAt, R8/ADR-0004), and the release is latency-
    /// compensated with M04's rule (LatencyCompensation.For).
    ///
    ///   Idle --Begin--> Arming --armDelay--> Ready --press--> Holding --release---------> Resolved
    ///                                          |                  \--ring fully closed--/   ^
    ///                                          \--pressTimeout (no press, timedOut)--------/
    public sealed class HoldRingMeter
    {
        public enum State : byte { Idle, Arming, Ready, Holding, Resolved }

        public State CurrentState => _state;
        /// True from the accepted press on (also after a resolve that had a press).
        public bool HasPressed => _pressed;

        /// The ring now, 0 open .. 1 closed: polled by the HUD. 0 until the press; frozen where it
        /// stopped once resolved. Frame time, not compensated - the compensation is for the release.
        public float Closure
        {
            get
            {
                if (_state == State.Holding) return HoldRing.ClosureAt(_clock.Now - _pressTime, _closeSeconds);
                if (_state == State.Resolved) return _timingResult.Position;
                return 0f;
            }
        }

        /// The wind-up built so far, 0..1 (polled by the HUD power arc). 0 before the press.
        public float WindUp01
        {
            get
            {
                if (_state == State.Resolved) return _powerResult.Fill;
                if (_state != State.Holding) return 0f;
                return HoldRing.WindUpAt(Closure, _ring.windUpPeakClosure, _ring.windUpExponent);
            }
        }

        /// The power a release now would carry (M03's floor and ceiling). Polled by the HUD.
        public float Power => _power == null ? 0f : Mathf.Lerp(_power.minPower, _power.maxPower, WindUp01);

        public double CloseSeconds => _closeSeconds;
        public float ZoneScale => _zoneScale;
        public double ArmDelaySeconds => _armDelay;
        public double PressTimeoutSeconds => _pressTimeout;
        public int PressesRejected => _rejected;
        public PowerResult PowerResult => _powerResult;
        public TimingResult TimingResult => _timingResult;

        private readonly Action<TapEvent> _onPress;
        private readonly Action<TapEvent> _onRelease;

        private HoldRingConfig _ring;
        private TimingPhaseConfig _timing;
        private PowerPhaseConfig _power;
        private TimingZoneLayout _layout;
        private IHoldSource _source;
        private IClock _clock;
        private State _state = State.Idle;
        private double _startTime;
        private double _pressTime;
        private double _closeSeconds;
        private double _armDelay;
        private double _pressTimeout;
        private float _zoneScale = 1f;
        private bool _pressed;
        private int _rejected;
        private PowerResult _powerResult;
        private TimingResult _timingResult;

        public HoldRingMeter()
        {
            // Cached once: subscribing a method group allocates a delegate each time.
            _onPress = HandlePress;
            _onRelease = HandleRelease;
        }

        public void Begin(HoldRingConfig ring, TimingPhaseConfig timing, PowerPhaseConfig power,
                          TimingZoneLayout layout, IHoldSource source, IClock clock, int roundIndex)
        {
            SlapAssert.NotNull(ring, nameof(ring));
            SlapAssert.NotNull(timing, nameof(timing));
            SlapAssert.NotNull(power, nameof(power));
            SlapAssert.NotNull(layout, nameof(layout));
            SlapAssert.NotNull(source, nameof(source));
            SlapAssert.NotNull(clock, nameof(clock));
            if (_source != null) Unbind(); // a Begin without a resolve must not leave a stale subscription
            _ring = ring;
            _timing = timing;
            _power = power;
            _layout = layout;
            _source = source;
            _clock = clock;
            _state = State.Arming;
            _startTime = clock.Now;
            _pressTime = 0;
            _pressed = false;
            _rejected = 0;
            _powerResult = default;
            _timingResult = default;

            // The round ramp and the zone scale are M04's, so both attack modes get harder together.
            _closeSeconds = HoldRing.CloseSeconds(ring.closeMs, timing.speedByRound.Evaluate(roundIndex));
            _zoneScale = Mathf.Clamp(timing.zoneScaleByRound.Evaluate(roundIndex), 0.5f, 1.5f);
            _armDelay = ring.armDelayMs * 0.001;
            _pressTimeout = ring.pressTimeoutMs * 0.001;

            _source.OnTap += _onPress;
            _source.OnRelease += _onRelease;
            _source.Enable();
        }

        public void Tick(double dt)
        {
            if (_state == State.Idle || _state == State.Resolved) return;
            if (double.IsNaN(dt) || dt < 0) return;
            double now = _clock.Now;
            double elapsed = now - _startTime;

            if (_state == State.Arming && elapsed >= _armDelay) _state = State.Ready;

            if ((_state == State.Arming || _state == State.Ready) && elapsed >= _pressTimeout)
            {
                ForceResolve();
                return;
            }

            // Held past the target: the slap fires on its own, late (owner's rule, ADR-0014).
            if (_state == State.Holding && now - _pressTime >= _closeSeconds)
                Resolve(_closeSeconds, 0.0, 0.0, false);
        }

        /// The finger goes down. The ring starts at the press's own event time, clamped so a
        /// hitch can neither pre-close the ring by more than M04's maxLatencyComp nor start it
        /// in the future. Arming is judged on that time too, not on whether this frame's Tick has
        /// run: a long frame can deliver a press made after the arm moment while the state still
        /// reads Arming, and dropping it costs the whole turn (S-087).
        public bool TryPress(in TapEvent e)
        {
            if (_state != State.Arming && _state != State.Ready) return false;
            double now = _clock.Now;
            double t = e.EventTime;
            if (double.IsNaN(t) || t > now) t = now;
            double earliest = now - _timing.maxLatencyComp;
            if (t < earliest) t = earliest;
            if (t < _startTime + _armDelay)
            {
                _rejected++;
                return false;
            }
            _pressTime = t;
            _pressed = true;
            _state = State.Holding;
            return true;
        }

        /// The finger lifts: evaluate the ring at the moment the player saw it (M04 §5).
        public bool TryRelease(in TapEvent e)
        {
            if (_state != State.Holding) return false;
            double now = _clock.Now;
            double rawLatency = now - e.EventTime;
            double comp = LatencyCompensation.For(rawLatency, _timing.displayLatencyBias, _timing.maxLatencyComp);
            double held = (now - _pressTime) - comp;
            Resolve(held, comp, rawLatency, false);
            return true;
        }

        /// Ends the phase now. Without a press: an un-wound slap at the open ring, timedOut.
        /// While holding: released at the ring's current closure.
        public void ForceResolve()
        {
            if (_state == State.Resolved || _state == State.Idle) return;
            if (_state == State.Holding) Resolve(_clock.Now - _pressTime, 0.0, 0.0, false);
            else Resolve(0.0, 0.0, 0.0, true);
        }

        private void Resolve(double heldSeconds, double comp, double rawLatency, bool timedOut)
        {
            float closure = HoldRing.ClosureAt(heldSeconds, _closeSeconds);
            TimingZone zone; float mult;
            _layout.Evaluate(closure, _zoneScale, out zone, out mult);
            // The ring only ever closes, so its direction is +1: closure below the gold centre is early.
            _timingResult = new TimingResult(closure, zone, mult, timedOut, (float)comp, (float)rawLatency, +1);

            float windUp = _pressed ? HoldRing.WindUpAt(closure, _ring.windUpPeakClosure, _ring.windUpExponent) : 0f;
            float rawPower = Mathf.Lerp(_power.minPower, _power.maxPower, windUp);
            _powerResult = new PowerResult(windUp, 0f, rawPower, _pressed ? 1 : 0, _rejected, 0f);

            _state = State.Resolved;
            Unbind();
        }

        private void Unbind()
        {
            if (_source == null) return;
            _source.OnTap -= _onPress;
            _source.OnRelease -= _onRelease;
            _source.Disable();
            _source = null;
        }

        private void HandlePress(TapEvent e) => TryPress(in e);
        private void HandleRelease(TapEvent e) => TryRelease(in e);
    }
}
