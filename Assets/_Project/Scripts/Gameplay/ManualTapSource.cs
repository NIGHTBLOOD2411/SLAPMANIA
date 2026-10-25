using System;
using UnityEngine;
using SlapMania.Core;

namespace SlapMania.Gameplay
{
    /// Trivial tap/hold source for tests and replay — caller pushes presses and releases.
    public sealed class ManualTapSource : IHoldSource
    {
        public event Action<TapEvent> OnTap;
        public event Action<TapEvent> OnRelease;
        private bool _enabled;
        public void Enable() { _enabled = true; }
        public void Disable() { _enabled = false; }

        /// A press (a tap, or the start of a hold).
        public void Push(double eventTime, byte sourceId = TapEvent.SourceReplay)
        {
            if (!_enabled) return;
            OnTap?.Invoke(new TapEvent(eventTime, new Vector2(0.5f, 0.5f), 1f, sourceId));
        }

        /// The end of a hold.
        public void PushRelease(double eventTime, byte sourceId = TapEvent.SourceReplay)
        {
            if (!_enabled) return;
            OnRelease?.Invoke(new TapEvent(eventTime, new Vector2(0.5f, 0.5f), 1f, sourceId));
        }
    }
}
