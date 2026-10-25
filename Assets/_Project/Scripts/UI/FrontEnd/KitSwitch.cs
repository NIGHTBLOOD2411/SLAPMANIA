using System;
using UnityEngine.UIElements;

namespace SlapMania.UI.FrontEnd
{
    /// The kit's `.sw` toggle (46 x 26 track, 20 px knob, red when on). The knob slides on a
    /// USS transition (Kit.uss `.kit-switch__knob`), so the motion is tunable in one place.
    [UxmlElement]
    public partial class KitSwitch : VisualElement, INotifyValueChanged<bool>
    {
        public const string UssClass = "kit-switch";
        public const string OnClass = "kit-switch--on";
        public const string KnobClass = "kit-switch__knob";

        private bool _value;

        public event Action<KitSwitch, bool> Changed;

        [UxmlAttribute("value")]
        public bool value
        {
            get => _value;
            set
            {
                if (_value == value) return;
                bool previous = _value;
                SetValueWithoutNotify(value);
                using (var e = ChangeEvent<bool>.GetPooled(previous, value))
                {
                    e.target = this;
                    SendEvent(e);
                }
                Changed?.Invoke(this, value);
            }
        }

        public KitSwitch()
        {
            AddToClassList(UssClass);
            var knob = new VisualElement { pickingMode = PickingMode.Ignore };
            knob.AddToClassList(KnobClass);
            Add(knob);
            RegisterCallback<ClickEvent>(OnClick);
        }

        public void SetValueWithoutNotify(bool newValue)
        {
            _value = newValue;
            EnableInClassList(OnClass, newValue);
        }

        private void OnClick(ClickEvent e)
        {
            value = !_value;
            e.StopPropagation();
        }
    }
}
