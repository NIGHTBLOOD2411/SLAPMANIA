using UnityEngine;
using UnityEngine.UI;

namespace SlapMania.UI
{
    /// Elliptical radial gradient filling the rect: the impact flash (D1), the K.O. hot-spot
    /// (D3) and — with a clear centre and `coverCorners` — the red "INCOMING!" edge vignette
    /// (D2). Three stops: centre, `mid` at `midAt`, edge at the ellipse. `coverCorners` adds a
    /// ring out to the rect corners in the edge colour so a full-screen vignette leaves no gap.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class SlapGlow : MaskableGraphic
    {
        [SerializeField] private Color _center = Color.white;
        [SerializeField] private Color _mid = new Color(1f, 1f, 1f, 0.35f);
        [SerializeField] private Color _edge = new Color(1f, 1f, 1f, 0f);
        [SerializeField, Range(0.01f, 0.99f)] private float _midAt = 0.37f;
        [SerializeField] private bool _coverCorners;
        [SerializeField, Range(12, 96)] private int _segments = 48;

        public void Configure(Color center, Color mid, Color edge, float midAt, bool coverCorners)
        {
            _center = center; _mid = mid; _edge = edge;
            _midAt = Mathf.Clamp(midAt, 0.01f, 0.99f);
            _coverCorners = coverCorners;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rect r = rectTransform.rect;
            if (r.width <= 0f || r.height <= 0f) return;
            Vector2 c = r.center;
            Vector2 e = new Vector2(r.width * 0.5f, r.height * 0.5f);
            Color tint = color;
            Color cc = _center * tint, cm = _mid * tint, ce = _edge * tint;

            int centre = vh.currentVertCount;
            vh.AddVert(c, cc, Vector4.zero);
            int ringMid = vh.currentVertCount;
            for (int i = 0; i < _segments; i++) vh.AddVert(c + Scale(Dir(i), e) * _midAt, cm, Vector4.zero);
            int ringEdge = vh.currentVertCount;
            for (int i = 0; i < _segments; i++) vh.AddVert(c + Scale(Dir(i), e), ce, Vector4.zero);
            int ringOut = -1;
            if (_coverCorners)
            {
                ringOut = vh.currentVertCount;
                for (int i = 0; i < _segments; i++) vh.AddVert(c + Scale(Dir(i), e) * 1.4143f, ce, Vector4.zero);
            }

            for (int i = 0; i < _segments; i++)
            {
                int j = (i + 1) % _segments;
                vh.AddTriangle(centre, ringMid + i, ringMid + j);
                vh.AddTriangle(ringMid + i, ringEdge + i, ringEdge + j);
                vh.AddTriangle(ringMid + i, ringEdge + j, ringMid + j);
                if (ringOut >= 0)
                {
                    vh.AddTriangle(ringEdge + i, ringOut + i, ringOut + j);
                    vh.AddTriangle(ringEdge + i, ringOut + j, ringEdge + j);
                }
            }

            Vector2 Dir(int i)
            {
                float a = i * Mathf.PI * 2f / _segments;
                return new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            }
        }

        private static Vector2 Scale(Vector2 v, Vector2 s) => new Vector2(v.x * s.x, v.y * s.y);

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            SetVerticesDirty();
        }
#endif
    }
}
