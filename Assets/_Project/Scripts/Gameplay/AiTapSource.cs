using System;
using UnityEngine;
using SlapMania.Core;
using SlapMania.Data;
using SlapMania.Logging;

namespace SlapMania.Gameplay
{
    /// Drop-in tap/hold source for the AI. Same interface the player uses, for both attack modes.
    public sealed class AiTapSource : IHoldSource
    {
        public event Action<TapEvent> OnTap;
        public event Action<TapEvent> OnRelease;

        private enum Phase : byte { Idle, Charging, Aiming, Done, HoldWaiting, Holding }

        private readonly IClock _clock;
        private readonly IRandom _rng;
        private AiProfile _profile;
        private bool _enabled;
        private Phase _phase = Phase.Idle;

        // Charge scheduling
        private double _nextTapAt;
        private double _chargeStartedAt;
        private double _chargeEndsAt;

        // Timing aim scheduling
        private double _commitAt;
        private bool _committed;

        // Hold-ring scheduling (ADR-0014)
        private double _pressAt;
        private double _releaseAt;

        public AiTapSource(IClock clock, IRandom rng)
        {
            SlapAssert.NotNull(clock, nameof(clock));
            SlapAssert.NotNull(rng, nameof(rng));
            _clock = clock;
            _rng = rng;
        }

        public void Configure(AiProfile profile) { _profile = profile; }
        public void Enable() { _enabled = true; }
        public void Disable() { _enabled = false; }

        public void BeginCharge(double phaseDuration)
        {
            SlapAssert.NotNull(_profile, "profile");
            _phase = Phase.Charging;
            _chargeStartedAt = _clock.Now;
            _chargeEndsAt = _chargeStartedAt + phaseDuration;
            _nextTapAt = _chargeStartedAt + NextInterval(0);
        }

        public void EndCharge() { _phase = Phase.Idle; }

        /// Tap-marker attack: aims at the zone centre + aimSigma, scaled by the round's zone scale,
        /// and commits on the marker's next crossing of that point after the reaction delay.
        ///
        /// The commit is scheduled `displayLatencyBias` late, like a player who taps when they
        /// *see* the marker on target: the meter back-dates every commit by that bias (M04 §5), so
        /// without it the AI would be judged one bias early (S-079). Same rule as BeginHoldRing.
        ///
        /// `markerStartsIn` is the meter's AIM cue (TimingMeter.CueSeconds): every time above is
        /// counted from the moment the marker moves. `earliestCommit` is the meter's
        /// EarliestCommitElapsed (arm delay, spam gap): the AI never aims at a crossing the meter
        /// would reject, whatever its reactionDelay (closes the S-088 tuning trap).
        public void BeginTiming(double startPhase, double sweep, TimingZoneLayout layout,
                                float zoneScale, double timeout, double displayLatencyBias,
                                double markerStartsIn = 0.0, double earliestCommit = 0.0)
        {
            SlapAssert.NotNull(layout, nameof(layout));
            _committed = false;
            _phase = Phase.Aiming;
            if (double.IsNaN(markerStartsIn) || markerStartsIn < 0.0) markerStartsIn = 0.0;
            // The stamp lands one bias after the aimed crossing; 1 us keeps float rounding on the right side.
            double minLead = _profile.reactionDelay;
            double gateLead = earliestCommit - displayLatencyBias + 1e-6;
            if (gateLead > minLead) minLead = gateLead;

            float centre = ZoneCentre(_profile.timingAimZone, layout);
            float aimed = centre + _rng.NextGaussian(0f, _profile.aimSigma);
            aimed = Mathf.Clamp(aimed, 0.02f, 0.98f);
            float scaled = TimingZoneLayout.ScaleAboutCentre(aimed, zoneScale);

            double when;
            bool ok = MarkerOscillator.NextTimeAtPosition(startPhase, sweep, scaled,
                minLead: minLead, maxLookahead: timeout, elapsed: out when);
            if (!ok)
            {
                _commitAt = _clock.Now + markerStartsIn + (timeout * 0.5); // mid-timeout fallback
                return;
            }
            if (_rng.NextFloat01() > _profile.commitOnFirstPass)
            {
                // Wait for the next pass; add one traversal period - unless that pass lands
                // after the timeout, which would forfeit the slap (S-097).
                double period = 1.0 / sweep;
                if (when + period + displayLatencyBias < timeout) when += period;
            }
            _commitAt = _clock.Now + markerStartsIn + when + displayLatencyBias;
        }

        /// Hold-ring attack: the same aim as the marker (zone centre + aimSigma, scaled by the
        /// round's zone scale), but turned into a release time with the closed-form inverse
        /// HoldRing.HeldSecondsAt. Presses once the ring is armed, after the reaction delay.
        ///
        /// The release is scheduled `displayLatencyBias` late, like a player who lifts when they
        /// *see* the ring on gold: the meter back-dates every release by that bias (M04 §5), so
        /// without it the AI's aim would land one bias early (S-079).
        public void BeginHoldRing(TimingZoneLayout layout, float zoneScale, double closeSeconds, double armDelay,
                                  double displayLatencyBias)
        {
            SlapAssert.NotNull(layout, nameof(layout));
            SlapAssert.NotNull(_profile, "profile");
            _phase = Phase.HoldWaiting;

            float centre = ZoneCentre(_profile.timingAimZone, layout);
            float aimed = centre + _rng.NextGaussian(0f, _profile.aimSigma);
            aimed = Mathf.Clamp(aimed, 0.02f, 0.98f);
            float scaled = TimingZoneLayout.ScaleAboutCentre(aimed, zoneScale);

            _pressAt = _clock.Now + armDelay + _profile.reactionDelay;
            _releaseAt = _pressAt + HoldRing.HeldSecondsAt(scaled, closeSeconds) + displayLatencyBias;
        }

        public void Tick(double dt)
        {
            if (!_enabled || _profile == null) return;
            double now = _clock.Now;
            if (_phase == Phase.Charging)
            {
                while (now >= _nextTapAt && now <= _chargeEndsAt)
                {
                    OnTap?.Invoke(new TapEvent(_nextTapAt, new Vector2(0.5f, 0.5f), 1f, TapEvent.SourceAI));
                    double t = _nextTapAt - _chargeStartedAt;
                    _nextTapAt += NextInterval((float)t);
                }
            }
            else if (_phase == Phase.Aiming && !_committed)
            {
                if (now >= _commitAt)
                {
                    OnTap?.Invoke(new TapEvent(_commitAt, new Vector2(0.5f, 0.5f), 1f, TapEvent.SourceAI));
                    _committed = true;
                    _phase = Phase.Done;
                }
            }
            else if (_phase == Phase.HoldWaiting || _phase == Phase.Holding)
            {
                // A long frame can carry both edges; press first, then release, each at its own time.
                if (_phase == Phase.HoldWaiting && now >= _pressAt)
                {
                    OnTap?.Invoke(new TapEvent(_pressAt, new Vector2(0.5f, 0.5f), 1f, TapEvent.SourceAI));
                    _phase = Phase.Holding;
                }
                if (_phase == Phase.Holding && now >= _releaseAt)
                {
                    OnRelease?.Invoke(new TapEvent(_releaseAt, new Vector2(0.5f, 0.5f), 1f, TapEvent.SourceAI));
                    _phase = Phase.Done;
                }
            }
        }

        private double NextInterval(float elapsedInCharge)
        {
            float tps = _profile.targetTps;
            float fatigueFactor = 1f - _profile.tapFatigue * Mathf.Clamp01(elapsedInCharge / 2.5f);
            tps *= Mathf.Max(0.1f, fatigueFactor);
            float noise = _rng.NextGaussian(1f, _profile.tpsVariance);
            tps *= Mathf.Max(0.1f, noise);
            return 1.0 / Mathf.Max(0.5f, tps);
        }

        private static float ZoneCentre(TimingZone target, TimingZoneLayout layout)
        {
            float bestCentre = 0.5f;
            float bestMult = -1f;
            for (int i = 0; i < layout.bands.Length; i++)
            {
                if (layout.bands[i].zone != target) continue;
                float c = (layout.bands[i].start + layout.bands[i].end) * 0.5f;
                if (layout.bands[i].multiplier > bestMult ||
                    (Mathf.Approximately(layout.bands[i].multiplier, bestMult) && Mathf.Abs(c - 0.5f) < Mathf.Abs(bestCentre - 0.5f)))
                {
                    bestCentre = c;
                    bestMult = layout.bands[i].multiplier;
                }
            }
            return bestCentre;
        }
    }
}
