using UnityEngine;
using UnityEngine.UIElements;
using SlapMania.Core.Motion;

namespace SlapMania.UI.FrontEnd.Screens
{
    /// A2 Loading: the bar fills over UiMotion.loaderMs with a live percentage, then the main
    /// menu replaces this screen (Back never returns to a loader). Content streaming (M19)
    /// will drive the fill from real progress; until then it is a timed reveal.
    /// V1's Loading reuses it with V1 Home as the destination (same hook names).
    internal sealed class LoaderView : ScreenView
    {
        private const int IdDone = 1;

        private readonly ScreenId _next;
        private VisualElement _fill;
        private Label _percent;
        private float _elapsed;
        private int _shownPercent = -1;

        public LoaderView(ScreenId next = ScreenId.MainMenu) => _next = next;

        protected override void OnBind()
        {
            _fill = Find<VisualElement>("loader-fill");
            _percent = Find<Label>("loader-pct");
        }

        protected override void OnShow()
        {
            _elapsed = 0f;
            _shownPercent = -1;
            float seconds = Motion != null ? Motion.loaderMs * 0.001f : 2f;
            Tw.To(_fill, VisualTween.Prop.WidthPercent, 0f, 100f, seconds, Ease.InOutQuad);
            Tw.Delay(seconds + 0.15f, this, IdDone);
        }

        protected override void OnTick(float dt)
        {
            if (_percent == null) return;
            _elapsed += dt;
            float seconds = Motion != null ? Motion.loaderMs * 0.001f : 2f;
            int pct = Mathf.Clamp(Mathf.RoundToInt(Easing.Evaluate(Ease.InOutQuad, _elapsed / seconds) * 100f), 0, 100);
            if (pct == _shownPercent) return;
            _shownPercent = pct;
            _percent.text = pct + "%"; // front end, once per whole percent
        }

        protected override void OnTween(int id)
        {
            if (id == IdDone && IsShown) Manager.ResetTo(_next);
        }
    }
}
