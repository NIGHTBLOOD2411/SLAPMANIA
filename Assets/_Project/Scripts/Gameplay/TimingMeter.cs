using System;
using UnityEngine;
using SlapMania.Core;
using SlapMania.Data;
using SlapMania.Logging;

namespace SlapMania.Gameplay
{
    /// Pure C#. Marker position is a function of elapsed time; latency is compensated per tap.
    ///
    /// Mash carry-over (DEBUG S-093): the phase opens on an AIM cue (aimCueMs) with the marker
    /// frozen at its start position, then the marker runs and the arm delay counts from there.
    /// Every press feeds a spam gap: a commit counts only if the press before it, charge taps
    /// included, was at least spamGapMs earlier. A finger still mashing never stops the marker.
    public sealed class TimingMeter
    {
        public enum State : byte { Idle, Arming, Armed, Resolved }

        public State CurrentState => _state;
        public float NormalizedPosition
        {
            get
            {
                float pos; int dir;
                MarkerOscillator.PositionAt(_elapsed, _startPhase, _sweep, out pos, out dir);
                return pos;
            }
        }
        public int Direction
        {
            get
            {
                float pos; int dir;
                MarkerOscillator.PositionAt(_elapsed, _startPhase, _sweep, out pos, out dir);
                return dir;
            }
        }

        public double Sweep => _sweep;
        public double StartPhase => _startPhase;
        public float ZoneScale => _zoneScale;

        /// The AIM cue: the ring is shown but frozen and no tap can commit yet.
        public bool InCue => (_state == State.Arming || _state == State.Armed) && _clock.Now < _startTime;
        /// Seconds from Begin until the marker starts to move.
        public double CueSeconds => _startTime - _beganAt;
        /// Marker time (seconds after it starts to move) from which a commit can first count:
        /// the arm delay, or the spam gap after the last press seen, whichever is later. The AI
        /// schedules its commit from this (M07), so it plays by the same gate.
        public double EarliestCommitElapsed
        {
            get
            {
                double gapEnds = _lastTapAt + SpamGap - _startTime;
                return gapEnds > _cfg.armDelay ? gapEnds : _cfg.armDelay;
            }
        }

        private TimingPhaseConfig _cfg;
        private TimingZoneLayout _layout;
        private ITapSource _source;
        private IClock _clock;
        private State _state = State.Idle;
        private double _beganAt;
        private double _startTime;   // the marker starts to move: Begin + the AIM cue
        private double _lastTapAt;
        private double _elapsed;
        private double _startPhase;
        private double _sweep;
        private float _zoneScale = 1f;
        private TimingResult _result;
        private readonly Action<TapEvent> _onTap;

        public TimingMeter()
        {
            // Cached once: subscribing a method group allocates a delegate each time (S-089).
            _onTap = HandleTap;
        }

        /// `lastTapAt`: the attacker's last charge tap (PowerMeter.LastTapTime), where the spam
        /// gap starts. -inf (the default) when there was none.
        public void Begin(TimingPhaseConfig cfg, TimingZoneLayout layout, ITapSource source, IClock clock,
                          int roundIndex, IRandom rng, float overcharge01 = 0f, float overchargeSpeedPenalty = 0f,
                          double lastTapAt = double.NegativeInfinity)
        {
            SlapAssert.NotNull(cfg, nameof(cfg));
            SlapAssert.NotNull(layout, nameof(layout));
            SlapAssert.NotNull(source, nameof(source));
            SlapAssert.NotNull(clock, nameof(clock));
            SlapAssert.NotNull(rng, nameof(rng));
            _cfg = cfg;
            _layout = layout;
            _source = source;
            _clock = clock;
            _state = State.Arming;
            _beganAt = clock.Now;
            _startTime = _beganAt + (cfg.aimCueMs > 0 ? cfg.aimCueMs * 0.001 : 0.0);
            _lastTapAt = double.IsNaN(lastTapAt) ? double.NegativeInfinity : lastTapAt;
            _elapsed = 0;
            _sweep = cfg.baseTraversalsPerSecond * Mathf.Max(0.01f, cfg.speedByRound.Evaluate(roundIndex))
                    * (1f + overchargeSpeedPenalty * overcharge01);
            _zoneScale = Mathf.Clamp(cfg.zoneScaleByRound.Evaluate(roundIndex), 0.5f, 1.5f);

            for (int attempt = 0; attempt < 8; attempt++)
            {
                _startPhase = rng.NextFloat01() * 2.0;
                float p; int d;
                MarkerOscillator.PositionAt(0, _startPhase, _sweep, out p, out d);
                if (Mathf.Abs(p - _cfg.perfectCentre) > _cfg.startPhaseGuard) break;
            }

            _result = default;
            _source.OnTap += _onTap;
            _source.Enable();
        }

        public void Tick(double dt)
        {
            if (_state == State.Idle || _state == State.Resolved) return;
            if (double.IsNaN(dt) || dt < 0) return;
            double sinceStart = _clock.Now - _startTime;     // negative during the AIM cue
            _elapsed = sinceStart > 0 ? sinceStart : 0;      // the marker holds still until then

            if (_state == State.Arming && sinceStart >= _cfg.armDelay)
                _state = State.Armed;

            if (_state == State.Armed && sinceStart >= _cfg.timeout)
                ForceResolve();
        }

        /// Stops the marker where the player saw it (M04 §5). Arming is judged on the commit's own
        /// event time, clamped like a hold-ring press so a hitch can neither back-date it past
        /// maxLatencyComp nor stamp it in the future, not on whether this frame's Tick has run: a
        /// long frame can deliver a commit made after the arm moment while the state still reads
        /// Arming, and dropping it costs the whole turn (S-087).
        ///
        /// Two more gates (S-093, S-096): a press made before Begin is a charge tap delivered late,
        /// never a commit; and a press closer than spamGapMs to the one before it is mashing.
        /// Every press, rejected or not, moves the spam gap on.
        public bool TryCommit(in TapEvent e)
        {
            if (_state != State.Arming && _state != State.Armed) return false;
            double now = _clock.Now;
            double made = e.EventTime;
            if (double.IsNaN(made) || made > now) made = now;
            double previous = _lastTapAt;
            if (made > _lastTapAt) _lastTapAt = made;
            if (made < _beganAt) return false;

            double t = made;
            double earliest = now - _cfg.maxLatencyComp;
            if (t < earliest) t = earliest;
            if (t < _startTime + _cfg.armDelay) return false;
            if (made - previous < SpamGap) return false;

            double rawLatency = now - e.EventTime;
            double comp = LatencyCompensation.For(rawLatency, _cfg.displayLatencyBias, _cfg.maxLatencyComp);

            // Evaluate the marker at the moment the player saw it: phaseElapsed − comp.
            double elapsedComp = (now - _startTime) - comp;
            if (elapsedComp < 0) elapsedComp = 0;

            float pos; int dir;
            MarkerOscillator.PositionAt(elapsedComp, _startPhase, _sweep, out pos, out dir);
            TimingZone zone; float mult;
            _layout.Evaluate(pos, _zoneScale, out zone, out mult);

            _result = new TimingResult(pos, zone, mult, false, (float)comp, (float)rawLatency, dir);
            _state = State.Resolved;
            _source.OnTap -= _onTap;
            _source.Disable();
            return true;
        }

        public TimingResult ForceResolve()
        {
            if (_state == State.Resolved) return _result;
            float pos; int dir;
            MarkerOscillator.PositionAt(_elapsed, _startPhase, _sweep, out pos, out dir);
            TimingZone zone; float mult;
            _layout.Evaluate(pos, _zoneScale, out zone, out mult);
            _result = new TimingResult(pos, zone, mult, true, 0f, 0f, dir);
            _state = State.Resolved;
            if (_source != null) { _source.OnTap -= _onTap; _source.Disable(); }
            return _result;
        }

        public TimingResult Result => _result;

        private double SpamGap => _cfg.spamGapMs > 0 ? _cfg.spamGapMs * 0.001 : 0.0;

        private void HandleTap(TapEvent e) => TryCommit(in e);
    }
}
