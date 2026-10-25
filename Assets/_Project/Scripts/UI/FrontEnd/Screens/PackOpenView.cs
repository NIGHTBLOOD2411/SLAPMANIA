using UnityEngine.UIElements;
using SlapMania.Core.Motion;

namespace SlapMania.UI.FrontEnd.Screens
{
    /// B2 Pack Walkout Reveal: the card flips up out of the light (the base class turns the
    /// rays). "Next card" replays the reveal; the pack contents are the mockup's until the
    /// economy module deals real cards.
    internal sealed class PackOpenView : ScreenView
    {
        private VisualElement _card, _title;

        protected override void OnBind()
        {
            _card = Find<VisualElement>("pack-card");
            _title = Find<VisualElement>("pack-title");
        }

        protected override void OnShow() => Reveal();

        protected override void OnLink(ScreenId target)
        {
            if (target == Id) { Reveal(); return; } // "Next card"
            base.OnLink(target);
        }

        private void Reveal()
        {
            float t = Motion != null ? Motion.packRevealMs * 0.001f : 1.4f;
            Tw.To(_card, VisualTween.Prop.Scale, 0.25f, 1f, t * 0.55f, Ease.OutBack, t * 0.15f);
            Tw.To(_card, VisualTween.Prop.Rotate, -14f, 0f, t * 0.55f, Ease.OutCubic, t * 0.15f);
            Tw.To(_card, VisualTween.Prop.Opacity, 0f, 1f, t * 0.25f, Ease.OutQuad, t * 0.15f);
            Tw.To(_title, VisualTween.Prop.Scale, 1.6f, 1f, t * 0.35f, Ease.OutBack, t * 0.55f);
            Tw.To(_title, VisualTween.Prop.Opacity, 0f, 1f, t * 0.2f, Ease.OutQuad, t * 0.55f);
        }
    }
}
