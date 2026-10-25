using UnityEngine.UIElements;
using SlapMania.Core.Motion;

namespace SlapMania.UI.FrontEnd.Screens
{
    /// A1 Title: "tap to start" blinks, the spotlight beam sways, and a tap anywhere goes on.
    internal sealed class TitleView : ScreenView
    {
        private VisualElement _tap, _beam;

        protected override void OnBind()
        {
            _tap = Find<VisualElement>("title-tap");
            _beam = Find<VisualElement>("title-beam");
            // The whole screen is the button, as on every console title screen.
            var screen = Root.Q("screen-Title");
            if (screen != null)
            {
                screen.pickingMode = PickingMode.Position;
                screen.RegisterCallback<ClickEvent>(OnTap);
            }
        }

        protected override void OnShow()
        {
            var m = Motion;
            if (m == null) return;
            Tw.To(_tap, VisualTween.Prop.Opacity, 1f, 0.35f, m.livePulseMs * 0.001f, Ease.InOutQuad, 0f, -1, true);
            Tw.To(_beam, VisualTween.Prop.Rotate, -2.5f, 2.5f, 3.2f, Ease.InOutQuad, 0f, -1, true);
        }

        private void OnTap(ClickEvent e)
        {
            if (IsShown) Manager.Navigate(ScreenId.Loader);
        }
    }
}
