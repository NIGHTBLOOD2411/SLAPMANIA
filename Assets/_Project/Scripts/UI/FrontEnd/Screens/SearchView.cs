using UnityEngine;
using UnityEngine.UIElements;
using SlapMania.Core.Motion;

namespace SlapMania.UI.FrontEnd.Screens
{
    /// C1 Finding Opponent: the radar sweeps, the timer counts up, and after
    /// UiMotion.searchMs the opponent is "found" and the walkout replaces this screen.
    /// Online matchmaking does not exist yet: the opponent is always the bout's AI.
    internal sealed class SearchView : ScreenView
    {
        private const int IdFound = 1;

        private VisualElement _sweep;
        private Label _timer;
        private float _elapsed;
        private int _shownSeconds = -1;

        protected override void OnBind()
        {
            _sweep = Find<VisualElement>("search-sweep");
            _timer = Find<Label>("search-timer");
        }

        protected override void OnShow()
        {
            _elapsed = 0f;
            _shownSeconds = -1;
            Tw.To(_sweep, VisualTween.Prop.Rotate, 0f, 360f, 1.8f, Ease.Linear, 0f, -1);
            Tw.Delay(Motion != null ? Motion.searchMs * 0.001f : 3f, this, IdFound);
        }

        protected override void OnTick(float dt)
        {
            if (_timer == null) return;
            _elapsed += dt;
            int s = Mathf.FloorToInt(_elapsed);
            if (s == _shownSeconds) return;
            _shownSeconds = s;
            _timer.text = (s / 60).ToString("00") + ":" + (s % 60).ToString("00");
        }

        protected override void OnTween(int id)
        {
            if (id == IdFound && IsShown) Manager.Replace(ScreenId.Walkout);
        }
    }
}
