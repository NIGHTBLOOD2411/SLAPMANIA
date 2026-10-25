using UnityEngine;
using UnityEngine.UI;

namespace SlapMania.UI
{
    /// Radial rays — the V2 speed lines (C6, D1: many thin rays, clear centre) and the K.O. /
    /// victory bursts (D3, E2: fewer wide rays, bright centre). Each ray fades along its length
    /// through three colour stops, which is what the CSS radial mask does. Spin it with a
    /// Tweener RotateZ loop; the mesh never changes after it is built.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class SlapRays : MaskableGraphic
    {
        [SerializeField, Range(3, 180)] private int _rays = 60;
        [Tooltip("Angular width of each ray in degrees.")]
        [SerializeField, Range(0.1f, 30f)] private float _rayWidth = 1f;
        [Tooltip("Where rays start, as a fraction of the radius.")]
        [SerializeField, Range(0f, 1f)] private float _innerRadius;
        [SerializeField] private Color _innerColor = new Color(1f, 1f, 1f, 0f);
        [SerializeField] private Color _midColor = new Color(1f, 1f, 1f, 0.24f);
        [SerializeField] private Color _outerColor = new Color(1f, 1f, 1f, 0.24f);
        [SerializeField, Range(0.01f, 0.99f)] private float _midAt = 0.62f;

        public void Configure(int rays, float widthDeg, float innerRadius, Color inner, Color mid, Color outer, float midAt)
        {
            _rays = Mathf.Clamp(rays, 3, 180);
            _rayWidth = Mathf.Clamp(widthDeg, 0.1f, 30f);
            _innerRadius = Mathf.Clamp01(innerRadius);
            _innerColor = inner; _midColor = mid; _outerColor = outer;
            _midAt = Mathf.Clamp(midAt, 0.01f, 0.99f);
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rect r = rectTransform.rect;
            float R = Mathf.Min(r.width, r.height) * 0.5f;
            if (R <= 0f) return;
            Vector2 c = r.center;
            float r0 = R * _innerRadius;
            float r1 = Mathf.Lerp(r0, R, _midAt);
            Color tint = color;
            Color ci = _innerColor * tint, cm = _midColor * tint, co = _outerColor * tint;
            float step = 360f / _rays;
            float half = _rayWidth * 0.5f;

            for (int i = 0; i < _rays; i++)
            {
                float a = i * step;
                Vector2 dl = Dir(a - half), dr = Dir(a + half);
                ShapeMesh.AddQuad(vh, c + dl * r0, c + dl * r1, c + dr * r1, c + dr * r0, ci, cm, cm, ci);
                ShapeMesh.AddQuad(vh, c + dl * r1, c + dl * R, c + dr * R, c + dr * r1, cm, co, co, cm);
            }
        }

        private static Vector2 Dir(float deg)
        {
            float a = deg * Mathf.Deg2Rad;
            return new Vector2(Mathf.Sin(a), Mathf.Cos(a));
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
