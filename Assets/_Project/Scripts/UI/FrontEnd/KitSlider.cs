using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace SlapMania.UI.FrontEnd
{
    /// The kit's `.rng` range input: a 6 px track filled red up to the value, with a 20 px
    /// white thumb. Value 0..1. The visible box is the mockup's 6 px track, but a finger needs
    /// more: an invisible hit zone reaches 14 px above and below it.
    [UxmlElement]
    public partial class KitSlider : VisualElement, INotifyValueChanged<float>
    {
        public const string UssClass = "kit-slider";

        private const float HitSlopPx = 14f;

        private readonly VisualElement _track, _fill, _rail, _thumb, _hit;
        private float _value = 0.5f;
        private int _pointer = -1;

        public event Action<KitSlider, float> Changed;

        [UxmlAttribute("value")]
        public float value
        {
            get => _value;
            set
            {
                value = Mathf.Clamp01(float.IsNaN(value) ? 0f : value);
                if (Mathf.Approximately(_value, value)) return;
                float previous = _value;
                SetValueWithoutNotify(value);
                using (var e = ChangeEvent<float>.GetPooled(previous, value))
                {
                    e.target = this;
                    SendEvent(e);
                }
                Changed?.Invoke(this, value);
            }
        }

        public KitSlider()
        {
            AddToClassList(UssClass);
            _track = new VisualElement { pickingMode = PickingMode.Ignore };
            _track.AddToClassList(UssClass + "__track");
            _fill = new VisualElement { pickingMode = PickingMode.Ignore };
            _fill.AddToClassList(UssClass + "__fill");
            _rail = new VisualElement { pickingMode = PickingMode.Ignore };
            _rail.AddToClassList(UssClass + "__rail");
            _thumb = new VisualElement { pickingMode = PickingMode.Ignore };
            _thumb.AddToClassList(UssClass + "__thumb");
            _track.Add(_fill);
            _rail.Add(_thumb);
            _hit = new VisualElement { name = "hit" };
            _hit.style.position = Position.Absolute;
            _hit.style.left = 0f; _hit.style.right = 0f;
            _hit.style.top = -HitSlopPx; _hit.style.bottom = -HitSlopPx;
            Add(_track);
            Add(_rail);
            Add(_hit);
            SetValueWithoutNotify(_value);

            RegisterCallback<PointerDownEvent>(OnDown);
            RegisterCallback<PointerMoveEvent>(OnMove);
            RegisterCallback<PointerUpEvent>(OnUp);
        }

        public void SetValueWithoutNotify(float newValue)
        {
            _value = Mathf.Clamp01(newValue);
            _fill.style.width = Length.Percent(_value * 100f);
            _thumb.style.left = Length.Percent(_value * 100f);
        }

        private void OnDown(PointerDownEvent e)
        {
            _pointer = e.pointerId;
            this.CapturePointer(_pointer);
            SetFromX(this.WorldToLocal(e.position).x);
            e.StopPropagation();
        }

        private void OnMove(PointerMoveEvent e)
        {
            if (e.pointerId != _pointer || !this.HasPointerCapture(_pointer)) return;
            SetFromX(this.WorldToLocal(e.position).x);
        }

        private void OnUp(PointerUpEvent e)
        {
            if (e.pointerId != _pointer) return;
            this.ReleasePointer(_pointer);
            _pointer = -1;
        }

        private void SetFromX(float x)
        {
            // The thumb's centre travels 10 px .. width - 10 px (Kit.uss `.kit-slider__rail`).
            float travel = layout.width - 20f;
            if (travel > 0f) value = (x - 10f) / travel;
        }
    }
}
