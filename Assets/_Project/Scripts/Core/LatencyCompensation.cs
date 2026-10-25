namespace SlapMania.Core
{
    /// M04 §5 latency compensation, shared by the tap marker and the hold ring. Pure (R2).
    public static class LatencyCompensation
    {
        /// How far to back-date an input: the measured delivery latency plus the display latency
        /// the Input System cannot see, clamped to [0, maxComp] so a GC hitch or a frame spike
        /// cannot back-date a tap into a free Perfect. A NaN latency counts as 0.
        public static double For(double rawLatency, double displayLatencyBias, double maxComp)
        {
            if (double.IsNaN(rawLatency)) rawLatency = 0.0;
            if (double.IsNaN(displayLatencyBias)) displayLatencyBias = 0.0;
            if (double.IsNaN(maxComp) || maxComp < 0.0) maxComp = 0.0;
            double comp = rawLatency + displayLatencyBias;
            if (comp < 0.0) comp = 0.0;
            if (comp > maxComp) comp = maxComp;
            return comp;
        }
    }
}
