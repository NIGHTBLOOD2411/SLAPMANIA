using UnityEngine.UIElements;
using SlapMania.Core.Motion;

namespace SlapMania.UI.FrontEnd.Screens
{
    /// C2 Main Event Walkout: the VS slams in, the fighter plates slide from their sides, and
    /// the tale of the tape follows after UiMotion.walkoutMs (or on "tap to skip").
    internal sealed class WalkoutView : ScreenView
    {
        private const int IdNext = 1;

        private VisualElement _vs, _champion, _challenger;

        protected override void OnBind()
        {
            _vs = Find<VisualElement>("walkout-vs");
            _champion = Find<VisualElement>("walkout-champion");
            _challenger = Find<VisualElement>("walkout-challenger");
        }

        protected override void OnShow()
        {
            Tw.To(_vs, VisualTween.Prop.Scale, 2.2f, 1f, 0.42f, Ease.OutBack, 0.18f);
            Tw.To(_vs, VisualTween.Prop.Opacity, 0f, 1f, 0.2f, Ease.OutQuad, 0.18f);
            Tw.To(_champion, VisualTween.Prop.TranslateX, -340f, 0f, 0.5f, Ease.OutCubic, 0.05f);
            Tw.To(_challenger, VisualTween.Prop.TranslateX, 340f, 0f, 0.5f, Ease.OutCubic, 0.12f);
            Tw.Delay(Motion != null ? Motion.walkoutMs * 0.001f : 4.5f, this, IdNext);
        }

        protected override void OnTween(int id)
        {
            if (id == IdNext && IsShown) Manager.Replace(ScreenId.TaleOfTape);
        }
    }
}
