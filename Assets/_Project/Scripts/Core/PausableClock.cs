namespace SlapMania.Core
{
    /// A clock that can stop (M01 pause). While paused, Now holds still; on resume it carries
    /// on from where it stopped, so every timer measured on it (state durations, the marker's
    /// elapsed time, the AI's tap schedule) simply never sees the pause.
    ///
    /// Timestamps taken on the source clock (Input System event times) are moved onto this
    /// timeline with FromSource. Pure: no Unity time is read here; the source is injected.
    public sealed class PausableClock : IClock
    {
        private readonly IClock _source;
        private double _pausedTotal;
        private double _pausedAt;
        private double _resumedAt = double.NegativeInfinity;

        public PausableClock(IClock source)
        {
            SlapMania.Logging.SlapAssert.NotNull(source, nameof(source));
            _source = source;
        }

        public bool IsPaused { get; private set; }

        public double Now => (IsPaused ? _pausedAt : _source.Now) - _pausedTotal;

        /// Total source time spent paused so far.
        public double PausedTotal => _pausedTotal;

        public void Pause()
        {
            if (IsPaused) return;
            _pausedAt = _source.Now;
            IsPaused = true;
        }

        public void Resume()
        {
            if (!IsPaused) return;
            double now = _source.Now;
            double spent = now - _pausedAt;
            // A source that went backwards (or NaN) must not move this clock backwards.
            if (spent > 0.0) _pausedTotal += spent;
            _resumedAt = now;
            IsPaused = false;
        }

        /// A source-clock timestamp on this clock's timeline.
        public double FromSource(double sourceTime) => sourceTime - _pausedTotal;

        /// True when `sourceTime` falls inside a pause: now, or the one that just ended (an
        /// event made during the pause can be delivered on the frame after Resume).
        public bool WasPausedAt(double sourceTime) => IsPaused || sourceTime < _resumedAt;
    }
}
