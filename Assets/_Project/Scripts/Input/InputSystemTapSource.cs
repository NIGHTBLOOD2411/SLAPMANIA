using System;
using UnityEngine;
using UnityEngine.InputSystem;
using SlapMania.Core;

namespace SlapMania.Input
{
    /// Live player input as an IHoldSource (M02). Two verbs share one action: a press is a tap
    /// (OnTap, from `performed`), and in the hold-ring attack the end of that press is a release
    /// (OnRelease, from `canceled`, ADR-0014). Both are stamped with the Input System event time
    /// (ctx.time) so they share the clock UnityClock reads (realtimeSinceStartup) and M04's
    /// latency compensation is meaningful.
    ///
    /// The action is always listening; Enable()/Disable() only gate emission, which is
    /// exactly what the meters expect: PowerMeter/TimingMeter call Enable() on Begin() and
    /// Disable() on End(). During the opponent's turn the meters bind the AiTapSource
    /// instead, so this source stays disabled and human taps are ignored.
    ///
    /// M02 owns SlapControls.inputactions in the target design; the action is built in code
    /// here so the input path has no asset dependency to break. See the M02 changelog.
    ///
    /// The whole screen is the tap target, so a press on an on-screen control (the HUD's pause
    /// button) would also count as a slap tap - and in the timing phase one tap commits.
    /// Blocker vetoes taps by screen position; the pointer position is read from the device
    /// that fired, after its state event is applied (same event, same frame).
    public sealed class InputSystemTapSource : IHoldSource, IDisposable
    {
        public event Action<TapEvent> OnTap;
        public event Action<TapEvent> OnRelease;

        /// Screen position (pixels, bottom-left origin) -> true to swallow the tap. Set once
        /// at bind; invoked per tap, so it must not allocate (R5).
        public Func<Vector2, bool> Blocker { get; set; }

        private readonly InputAction _tap;
        private readonly byte _sourceId;
        private bool _enabled;

        public InputSystemTapSource(byte sourceId = TapEvent.SourcePlayerTouch)
        {
            _sourceId = sourceId;
            _tap = new InputAction("Tap", InputActionType.Button);
            _tap.AddBinding("<Touchscreen>/primaryTouch/press");
            _tap.AddBinding("<Mouse>/leftButton");
            _tap.AddBinding("<Keyboard>/space"); // editor testing only
            _tap.performed += OnPerformed;
            _tap.canceled += OnCanceled;
            _tap.Enable();
        }

        public void Enable() { _enabled = true; }
        public void Disable() { _enabled = false; }

        private void OnPerformed(InputAction.CallbackContext ctx)
        {
            if (!_enabled) return;
            // Keyboard (Space) has no position: never blocked.
            Vector2 pos = Vector2.zero;
            if (ctx.control.device is Pointer pointer)
            {
                pos = pointer.position.ReadValue();
                var blocker = Blocker;
                if (blocker != null && blocker(pos)) return;
            }
            double eventTime = ctx.time > 0.0 ? ctx.time : Time.realtimeSinceStartupAsDouble;
            OnTap?.Invoke(new TapEvent(eventTime, pos, 1f, _sourceId));
        }

        // A Button action with no interaction cancels when the control is released. Releases are
        // never blocked: a hold that started on the play area ends wherever the finger lifts, and
        // a release whose press was blocked (or began while disabled) is ignored by the hold ring,
        // which only accepts a release while it is holding.
        private void OnCanceled(InputAction.CallbackContext ctx)
        {
            if (!_enabled) return;
            Vector2 pos = ctx.control.device is Pointer pointer ? pointer.position.ReadValue() : Vector2.zero;
            double eventTime = ctx.time > 0.0 ? ctx.time : Time.realtimeSinceStartupAsDouble;
            OnRelease?.Invoke(new TapEvent(eventTime, pos, 1f, _sourceId));
        }

        public void Dispose()
        {
            if (_tap == null) return;
            _tap.performed -= OnPerformed;
            _tap.canceled -= OnCanceled;
            _tap.Disable();
            _tap.Dispose();
        }
    }
}
