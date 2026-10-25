using UnityEngine;
using UnityEngine.UI;

namespace SlapMania.UI
{
    /// Rings, arcs and discs for the V2 HUD: the closing timing ring and gold band (C5), the
    /// countdown progress ring (C4), the power gauge arc (C6), impact rings (D1). Angles are
    /// CSS-conic style — 0 is 12 o'clock, positive is clockwise. A 3-stop gradient runs along
    /// the sweep; `glow` adds a soft halo on both edges (the CSS box-shadow glow).
    ///
    /// Setters only dirty the mesh when the value actually changes, so a ring driven every
    /// frame rebuilds one small mesh and allocates nothing (VertexHelper is pooled).
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class SlapArc : MaskableGraphic
    {
        [Header("Geometry (px)")]
        [Tooltip("Outer radius; <= 0 uses half the rect's smaller side.")]
        [SerializeField] private float _radius = -1f;
        [Tooltip("Band thickness; >= radius draws a filled disc.")]
        [SerializeField, Min(0f)] private float _thickness = 4f;
        [SerializeField] private float _startAngle;
        [SerializeField, Range(0f, 360f)] private float _sweep = 360f;
        [SerializeField, Range(0f, 1f)] private float _fill = 1f;

        [Header("Colour along the sweep")]
        [SerializeField] private Color _colorStart = Color.white;
        [SerializeField] private Color _colorMid = Color.white;
        [SerializeField] private Color _colorEnd = Color.white;
        [SerializeField, Range(-1f, 1f)] private float _midStop = -1f;

        [Header("Glow")]
        [SerializeField, Min(0f)] private float _glow;
        [SerializeField] private Color _glowColor = new Color(1f, 1f, 1f, 0.5f);

        public float Radius
        {
            get => _radius;
            set { if (!Mathf.Approximately(_radius, value)) { _radius = value; SetVerticesDirty(); } }
        }

        public float Thickness
        {
            get => _thickness;
            set { if (!Mathf.Approximately(_thickness, value)) { _thickness = value; SetVerticesDirty(); } }
        }

        public float Fill
        {
            get => _fill;
            set { value = Mathf.Clamp01(value); if (!Mathf.Approximately(_fill, value)) { _fill = value; SetVerticesDirty(); } }
        }

        public void SetColors(Color start, Color mid, Color end, float midStop = -1f)
        {
            _colorStart = start; _colorMid = mid; _colorEnd = end; _midStop = midStop;
            SetVerticesDirty();
        }

        public void SetSolid(Color c) => SetColors(c, c, c);

        public void SetGlow(float px, Color c)
        {
            _glow = px; _glowColor = c;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rect r = rectTransform.rect;
            float outer = _radius > 0f ? _radius : Mathf.Min(r.width, r.height) * 0.5f;
            if (outer <= 0f || _fill <= 0f || _sweep <= 0f) return;
            float inner = Mathf.Max(0f, outer - _thickness);
            Vector2 c = r.center;

            float sweep = _sweep * _fill;
            int segs = Mathf.Clamp(Mathf.CeilToInt(outer * sweep / 360f * 0.6f) + 6, 8, 160);
            Color tint = color;
            Color glow = _glowColor * tint;
            Color glowClear = glow; glowClear.a = 0f;

            for (int i = 0; i < segs; i++)
            {
                float t0 = (float)i / segs, t1 = (float)(i + 1) / segs;
                float a0 = (_startAngle + sweep * t0) * Mathf.Deg2Rad;
                float a1 = (_startAngle + sweep * t1) * Mathf.Deg2Rad;
                Vector2 d0 = new Vector2(Mathf.Sin(a0), Mathf.Cos(a0));
                Vector2 d1 = new Vector2(Mathf.Sin(a1), Mathf.Cos(a1));
                // Colour position is along the full sweep, so a partial fill shows the
                // gradient's start, not a squashed copy of the whole thing.
                Color c0 = SweepColor(t0 * _fill) * tint;
                Color c1 = SweepColor(t1 * _fill) * tint;

                ShapeMesh.AddQuad(vh, c + d0 * inner, c + d0 * outer, c + d1 * outer, c + d1 * inner, c0, c0, c1, c1);

                if (_glow > 0f)
                {
                    ShapeMesh.AddQuad(vh, c + d0 * outer, c + d0 * (outer + _glow), c + d1 * (outer + _glow), c + d1 * outer,
                                      glow, glowClear, glowClear, glow);
                    if (inner > 0f)
                    {
                        float gi = Mathf.Max(0f, inner - _glow);
                        ShapeMesh.AddQuad(vh, c + d0 * gi, c + d0 * inner, c + d1 * inner, c + d1 * gi,
                                          glowClear, glow, glow, glowClear);
                    }
                }
            }
        }

        private Color SweepColor(float t)
        {
            if (_midStop <= 0f || _midStop >= 1f) return Color.LerpUnclamped(_colorStart, _colorEnd, t);
            return t < _midStop
                ? Color.LerpUnclamped(_colorStart, _colorMid, t / _midStop)
                : Color.LerpUnclamped(_colorMid, _colorEnd, (t - _midStop) / (1f - _midStop));
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            SetVerticesDirty();
        }
#endif
    }
}
