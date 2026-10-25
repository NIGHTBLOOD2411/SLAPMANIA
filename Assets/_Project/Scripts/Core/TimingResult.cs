namespace SlapMania.Core
{
    public readonly struct TimingResult
    {
        public readonly float Position;
        public readonly TimingZone Zone;
        public readonly float Multiplier;
        public readonly bool TimedOut;
        public readonly float LatencyCompensated;
        public readonly float RawLatency;
        /// Marker travel direction at the commit, +1 or -1. With Position it tells early from
        /// late: (Position - perfectCentre) * Direction < 0 means the marker had not reached the
        /// centre yet on this pass (M04; used by SlapStats for coaching).
        public readonly int Direction;

        public TimingResult(float position, TimingZone zone, float multiplier,
                            bool timedOut, float latencyCompensated, float rawLatency, int direction)
        {
            Position = position;
            Zone = zone;
            Multiplier = multiplier;
            TimedOut = timedOut;
            LatencyCompensated = latencyCompensated;
            RawLatency = rawLatency;
            Direction = direction >= 0 ? 1 : -1;
        }
    }
}
