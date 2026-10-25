using UnityEngine;
using UnityEngine.UI;
using SlapMania.Core.Motion;

namespace SlapMania.UI
{
    /// The V2 scorebug health bar (`.bar.tick` with `.fg` + `.fdmg`/`.fw`). The fill tracks HP
    /// at once; the damage chunk behind it flashes white (D1), turns red (C5) and drains after
    /// a short hold, so the player reads *how much* a slap took. Widths are driven by anchors,
    /// so there is no sprite and no fill-method dependency. `reverse` fills from the right
    /// (the player's bar, `flex-direction: row-reverse`).
    public sealed class HpBar : MonoBehaviour, ITweenSink
    {
        [SerializeField] private RectTransform _fill;
        [SerializeField] private RectTransform _trail;
        [SerializeField] private Graphic _trailGraphic;
        [SerializeField] private bool _reverse;

        [Header("Feel")]
        [SerializeField] private Color _flashColor = Color.white;
        [SerializeField] private Color _trailColor = new Color(0.890f, 0.149f, 0.184f); // #E3262F
        [SerializeField, Min(0f)] private float _flashSeconds = 0.12f;
        [SerializeField, Min(0f)] private float _holdSeconds = 0.35f;
        [SerializeField, Min(0.01f)] private float _drainSeconds = 0.45f;

        private const int ChTrail = 0;
        private const int ChColor = 1;

        private readonly Tweener _tw = new Tweener(4);
        private float _value = 1f;
        private float _trailValue = 1f;

        public float Value => _value;

        public void SetImmediate(float v)
        {
            _tw.KillAll();
            _value = _trailValue = Mathf.Clamp01(v);
            Layout(_fill, _value);
            Layout(_trail, _trailValue);
        }

        public void SetValue(float v)
        {
            v = Mathf.Clamp01(v);
            if (Mathf.Approximately(v, _value)) return;
            bool damage = v < _value;
            _value = v;
            Layout(_fill, _value);
            if (!damage) { _trailValue = v; Layout(_trail, v); return; }

            // The chunk keeps showing the pre-hit HP, then drains down to the new value.
            _tw.Float(this, ChColor, 0f, 1f, _flashSeconds, Ease.Linear);
            _tw.Float(this, ChTrail, _trailValue, v, _drainSeconds, Ease.OutCubic, _holdSeconds);
        }

        public void OnTweenValue(int channel, float value)
        {
            if (channel == ChTrail) { _trailValue = value; Layout(_trail, value); }
            else if (_trailGraphic != null) _trailGraphic.color = Color.Lerp(_flashColor, _trailColor, value);
        }

        private void Layout(RectTransform rt, float v)
        {
            if (rt == null) return;
            if (_reverse) { rt.anchorMin = new Vector2(1f - v, 0f); rt.anchorMax = Vector2.one; }
            else          { rt.anchorMin = Vector2.zero;            rt.anchorMax = new Vector2(v, 1f); }
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private void Update() => _tw.Tick(Time.unscaledDeltaTime);
    }
}
