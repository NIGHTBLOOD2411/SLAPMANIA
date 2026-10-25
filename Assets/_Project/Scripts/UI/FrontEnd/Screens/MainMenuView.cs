using UnityEngine;
using UnityEngine.UIElements;

namespace SlapMania.UI.FrontEnd.Screens
{
    /// A3 Main Menu: the bottom LIVE ticker scrolls (the base class already blinks LIVE dots,
    /// fills the season and career meters, and staggers the tiles in).
    ///
    /// The headlines move into an inner strip that scrolls inside the ticker, which clips to
    /// its own box - so the copy slides under nothing and never over the red LIVE label beside it.
    internal sealed class MainMenuView : ScreenView
    {
        private VisualElement _ticker, _strip;
        private float _x;

        protected override void OnBind()
        {
            _ticker = Find<VisualElement>("ticker");
            if (_ticker == null) return;
            _strip = new VisualElement { name = "ticker-strip", pickingMode = PickingMode.Ignore };
            _strip.style.flexDirection = FlexDirection.Row;
            _strip.style.alignItems = Align.Center;
            _strip.style.flexShrink = 0f;
            while (_ticker.childCount > 0) _strip.Add(_ticker[0]);
            _ticker.Add(_strip);
            _ticker.style.overflow = Overflow.Hidden;
            _ticker.style.flexGrow = 1f;
            _ticker.style.flexShrink = 1f;
        }

        protected override void OnShow() => _x = 0f;

        protected override void OnTick(float dt)
        {
            if (_strip == null || Motion == null) return;
            float w = _strip.layout.width;
            float viewport = _ticker.layout.width;
            if (float.IsNaN(w) || w <= 0f || float.IsNaN(viewport)) return;
            _x -= Motion.tickerPxPerSecond * dt;
            // Once the copy has scrolled out on the left, it re-enters from the right edge.
            if (_x < -w) _x = viewport;
            _strip.style.translate = new Translate(_x, 0f);
        }
    }
}
