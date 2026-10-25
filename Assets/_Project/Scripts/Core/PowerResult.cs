namespace SlapMania.Core
{
    public readonly struct PowerResult
    {
        public readonly float Fill;
        public readonly float Overcharge;
        public readonly float RawPower;
        public readonly int TapsAccepted;
        public readonly int TapsRejected;
        public readonly float PeakTps;

        public PowerResult(float fill, float overcharge, float rawPower,
                           int tapsAccepted, int tapsRejected, float peakTps)
        {
            Fill = fill;
            Overcharge = overcharge;
            RawPower = rawPower;
            TapsAccepted = tapsAccepted;
            TapsRejected = tapsRejected;
            PeakTps = peakTps;
        }
    }
}
