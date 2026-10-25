using UnityEngine;
using SlapMania.Core;

namespace SlapMania.App
{
    /// The real-time simulation clock (ARCHITECTURE §6). It reads realtimeSinceStartup so the
    /// simulation shares the Input System's event-time base — making M04 latency compensation
    /// meaningful — and stays immune to the hitstop timeScale drop, so the FSM's fixed-duration
    /// states (windup, hitstop, turn transition) measure true seconds. Never used for
    /// presentation tweens, which run on unscaled time.
    public sealed class UnityClock : IClock
    {
        public double Now => Time.realtimeSinceStartupAsDouble;
    }
}
