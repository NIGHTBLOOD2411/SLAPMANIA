using UnityEngine;
using UnityEngine.UIElements;

namespace SlapMania.UI.FrontEnd.Screens
{
    /// C3 Tale of the Tape: counts down to the bout and starts it at zero, exactly as
    /// "I'm ready" does (its link points at Ready, the bout's first screen).
    internal sealed class TaleOfTapeView : ScreenView
    {
        private Label _auto;
        private float _left;
        private int _shown = -1;
        private bool _started;

        protected override void OnBind() => _auto = Find<Label>("tape-autostart");

        protected override void OnShow()
        {
            _left = Motion != null ? Motion.tapeAutoStartMs * 0.001f : 8f;
            _shown = -1;
            _started = false;
        }

        protected override void OnTick(float dt)
        {
            if (_started) return;
            _left -= dt;
            int s = Mathf.Max(0, Mathf.CeilToInt(_left));
            if (s != _shown && _auto != null)
            {
                _shown = s;
                _auto.text = "AUTO-START IN 0:" + s.ToString("00");
            }
            if (_left <= 0f)
            {
                _started = true;
                Manager.Navigate(ScreenId.Ready);
            }
        }

        protected override void OnLink(ScreenId target)
        {
            if (target == ScreenId.Ready) _started = true;
            base.OnLink(target);
        }
    }
}
