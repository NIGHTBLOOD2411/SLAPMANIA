using UnityEngine.UIElements;
using SlapMania.Core.Motion;

namespace SlapMania.UI.FrontEnd.Screens
{
    /// A1 Title: "tap to start" blinks, two spotlight beams sway and pulse in a pattern, and a
    /// tap anywhere goes on. The mockup has one beam; the second is cloned from it at bind so
    /// the generated UXML stays untouched (M10 §2: re-run the converter, never hand-edit).
    internal sealed class TitleView : ScreenView
    {
        // Second beam pose, relative to the mockup beam: right of it, hung from its top edge
        // and angled back in toward the fighter.
        private const float Beam2OffsetPx = 110f;
        private const float Beam2TiltDeg = 14f;

        private VisualElement _tap;
        private PaintBox _beam, _beam2;
        private double _pulseMs;

        protected override void OnBind()
        {
            _tap = Find<VisualElement>("title-tap");
            _beam = Find<PaintBox>("title-beam");
            if (_beam != null) _beam2 = CloneBeam(_beam);
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
            _pulseMs = 0.0;
            var m = Motion;
            if (m == null) return;
            Tw.To(_tap, VisualTween.Prop.Opacity, 1f, 0.35f, m.livePulseMs * 0.001f, Ease.InOutQuad, 0f, -1, true);
            Tw.To(_beam, VisualTween.Prop.Rotate, -2.5f, 2.5f, 3.2f, Ease.InOutQuad, 0f, -1, true);
            Tw.To(_beam2, VisualTween.Prop.Rotate, 2.5f, -2.5f, 2.7f, Ease.InOutQuad, 0f, -1, true); // out of phase
        }

        protected override void OnTick(float dt)
        {
            var m = Motion;
            if (m == null) return;
            _pulseMs += dt * 1000.0;
            if (_beam != null)
                _beam.style.opacity = BlinkPattern.Evaluate(m.titleBeamPattern, _pulseMs, m.titleBeamStepMs);
            if (_beam2 != null)
                _beam2.style.opacity = BlinkPattern.Evaluate(m.titleBeamPattern, _pulseMs + m.titleBeamOffsetMs, m.titleBeamStepMs);
        }

        private static PaintBox CloneBeam(PaintBox src)
        {
            var beam = new PaintBox { name = "title-beam-2", pickingMode = PickingMode.Ignore, Paint = src.Paint, Clip = src.Clip };
            var b = beam.style;
            b.position = Position.Absolute;
            b.flexDirection = FlexDirection.Column;
            b.transformOrigin = new TransformOrigin(Length.Percent(50f), Length.Percent(0f));
            b.rotate = new Rotate(Beam2TiltDeg);
            var parent = src.parent;
            parent.Insert(parent.IndexOf(src) + 1, beam);
            // The UXML's inline geometry reads back empty at bind (S-101), so copy the source's
            // laid-out rect once layout has run, and again whenever it changes.
            src.RegisterCallback<GeometryChangedEvent, PaintBox>(OnBeamLaidOut, beam);
            return beam;
        }

        private static void OnBeamLaidOut(GeometryChangedEvent e, PaintBox clone)
        {
            var r = e.newRect;
            var b = clone.style;
            b.left = r.x + Beam2OffsetPx;
            b.top = r.y;
            b.width = r.width;
            b.height = r.height;
        }

        private void OnTap(ClickEvent e)
        {
            if (IsShown) Manager.Navigate(ScreenId.Loader);
        }
    }
}
