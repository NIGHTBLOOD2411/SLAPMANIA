using System;

namespace SlapMania.Core
{
    /// A tap source that also reports when the press ends (M02, ADR-0014). OnTap is the press;
    /// OnRelease is the finger lifting (or the AI letting go), stamped with its own event time
    /// so the hold ring can compensate latency exactly as M04 does for a tap.
    public interface IHoldSource : ITapSource
    {
        event Action<TapEvent> OnRelease;
    }
}
