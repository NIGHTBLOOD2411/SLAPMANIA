using System;
using SlapMania.Core;

namespace SlapMania.Gameplay
{
    /// The player's taps on the bout's pausable timeline (M01 pause). Wraps the live source:
    /// taps made while the bout is paused are dropped, and the rest are re-stamped from the
    /// source clock (Input System event time) into simulation time, so M04's latency
    /// compensation (now - eventTime) and M03's tap window stay honest across a pause.
    /// Enable/Disable pass straight through; the meters gate emission as before.
    ///
    /// Releases (hold-ring attack, ADR-0014) go through the same gate when the wrapped source
    /// is an IHoldSource. A release made while paused is dropped like a tap: the ring is frozen
    /// then, and honouring it would let a pause line the ring up on gold for free. After resume
    /// the next lift ends the hold, or the ring closes and auto-releases late.
    public sealed class PausableTapSource : IHoldSource
    {
        public event Action<TapEvent> OnTap;
        public event Action<TapEvent> OnRelease;

        private readonly ITapSource _inner;
        private readonly IHoldSource _innerHold;
        private readonly PausableClock _clock;

        public PausableTapSource(ITapSource inner, PausableClock clock)
        {
            SlapMania.Logging.SlapAssert.NotNull(inner, nameof(inner));
            SlapMania.Logging.SlapAssert.NotNull(clock, nameof(clock));
            _inner = inner;
            _innerHold = inner as IHoldSource;
            _clock = clock;
            _inner.OnTap += Forward;
            if (_innerHold != null) _innerHold.OnRelease += ForwardRelease;
        }

        public ITapSource Inner => _inner;
        /// False when the wrapped source only taps: the director then plays TapMarker (ADR-0014).
        public bool SupportsHold => _innerHold != null;

        public void Enable() => _inner.Enable();
        public void Disable() => _inner.Disable();

        /// Stop listening to the wrapped source (a re-bind replaces this gate).
        public void Detach()
        {
            _inner.OnTap -= Forward;
            if (_innerHold != null) _innerHold.OnRelease -= ForwardRelease;
        }

        private void Forward(TapEvent e)
        {
            if (double.IsNaN(e.EventTime) || _clock.WasPausedAt(e.EventTime)) return;
            OnTap?.Invoke(new TapEvent(_clock.FromSource(e.EventTime), e.ScreenPosition, e.Pressure, e.SourceId));
        }

        private void ForwardRelease(TapEvent e)
        {
            if (double.IsNaN(e.EventTime) || _clock.WasPausedAt(e.EventTime)) return;
            OnRelease?.Invoke(new TapEvent(_clock.FromSource(e.EventTime), e.ScreenPosition, e.Pressure, e.SourceId));
        }
    }
}
