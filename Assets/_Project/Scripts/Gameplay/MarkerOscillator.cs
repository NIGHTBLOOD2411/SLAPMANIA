namespace SlapMania.Gameplay
{
    /// Pure, stateless. See ADR-0004 — position is a function of double elapsed time.
    public static class MarkerOscillator
    {
        /// Returns position in [0, 1] and direction ±1.
        public static void PositionAt(double elapsed, double startPhase, double sweepPerSecond,
                                      out float position, out int direction)
        {
            double raw = startPhase + elapsed * sweepPerSecond;
            double phase = raw - System.Math.Floor(raw / 2.0) * 2.0;
            if (phase <= 1.0)
            {
                position = (float)phase;
                direction = +1;
            }
            else
            {
                position = (float)(2.0 - phase);
                direction = -1;
            }
        }

        /// Earliest `elapsed >= minLead` at which the marker will be at `target`.
        /// Returns false if no such time exists within `maxLookahead`.
        public static bool NextTimeAtPosition(double startPhase, double sweepPerSecond,
                                              float target, double minLead, double maxLookahead,
                                              out double elapsed)
        {
            elapsed = 0;
            if (sweepPerSecond <= 0) return false;
            if (target < 0f || target > 1f) return false;
            double stepDt = 1.0 / (sweepPerSecond * 200.0); // 200 samples per full sweep
            double t = minLead;
            float prev; int prevDir;
            PositionAt(t, startPhase, sweepPerSecond, out prev, out prevDir);
            while (t < maxLookahead)
            {
                double nt = t + stepDt;
                float pos; int dir;
                PositionAt(nt, startPhase, sweepPerSecond, out pos, out dir);
                // Crossing between prev and pos?
                float lo = pos < prev ? pos : prev;
                float hi = pos > prev ? pos : prev;
                if (target >= lo && target <= hi)
                {
                    // Linear interpolate crossing time.
                    float span = hi - lo;
                    if (span > 1e-6f)
                    {
                        float frac = (target - prev) / (pos - prev);
                        if (frac < 0f) frac = 0f; else if (frac > 1f) frac = 1f;
                        elapsed = t + stepDt * frac;
                    }
                    else
                    {
                        elapsed = nt;
                    }
                    return true;
                }
                t = nt;
                prev = pos;
            }
            return false;
        }
    }
}
