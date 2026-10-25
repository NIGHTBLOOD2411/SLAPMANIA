using UnityEngine;
using UnityEngine.UI;

namespace SlapMania.UI
{
    /// The V2 UI-kit panel as a procedural mesh: solid or 2/3-stop gradient fill, the `.skew`
    /// parallelogram (buttons, tags), the `.cc` chamfered card, small corner cuts standing in
    /// for border-radius, and an inner border (the 1 px glass edge). Crisp at any resolution,
    /// no sprite. The Graphic colour multiplies everything, so fading `color` fades the panel.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class SlapPanel : MaskableGraphic
    {
        public enum Axis : byte { Vertical, Horizontal }

        [Header("Fill")]
        [SerializeField] private bool _fill = true;
        [SerializeField] private Color _colorA = Color.white;
        [SerializeField] private Color _colorB = Color.white;
        [SerializeField] private Color _colorC = Color.white;
        [Tooltip("Position of colour B along the axis; <= 0 or >= 1 makes a 2-stop A -> C gradient.")]
        [SerializeField, Range(-1f, 1f)] private float _midStop = -1f;
        [SerializeField] private Axis _axis = Axis.Vertical;

        [Header("Shape (px)")]
        [Tooltip("V2 .skew: top-left and bottom-right pulled in by this much. Overrides the cuts.")]
        [SerializeField, Min(0f)] private float _skew;
        [Tooltip("V2 .cc: top-left and bottom-right corners cut at 45 degrees.")]
        [SerializeField, Min(0f)] private float _chamfer;
        [Tooltip("Small cut on all four corners, approximating border-radius.")]
        [SerializeField, Min(0f)] private float _corner;

        [Header("Border")]
        [SerializeField, Min(0f)] private float _border;
        [SerializeField] private Color _borderColor = new Color(1f, 1f, 1f, 0.12f);

        private static readonly Vector2[] s_outer = new Vector2[ShapeMesh.MaxPoints];
        private static readonly Vector2[] s_inner = new Vector2[ShapeMesh.MaxPoints];

        public void SetColors(Color a, Color b, Color c, float midStop = -1f)
        {
            _colorA = a; _colorB = b; _colorC = c; _midStop = midStop;
            SetVerticesDirty();
        }

        public void SetSolid(Color c) => SetColors(c, c, c);

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rect r = GetPixelAdjustedRect();
            if (r.width <= 0f || r.height <= 0f) return;

            int n = BuildOutline(r, s_outer);
            var g = new Gradient3(r, _axis == Axis.Vertical, _colorA, _colorB, _colorC, _midStop, color);

            if (_border > 0f && n >= 3)
            {
                ShapeMesh.Inset(s_outer, n, _border, s_inner);
                Color32 bc = _borderColor * color;
                for (int i = 0; i < n; i++)
                {
                    int j = (i + 1) % n;
                    ShapeMesh.AddQuad(vh, s_outer[i], s_outer[j], s_inner[j], s_inner[i], bc, bc, bc, bc);
                }
                if (_fill) ShapeMesh.FillConvexGradient(vh, s_inner, n, g);
            }
            else if (_fill)
            {
                ShapeMesh.FillConvexGradient(vh, s_outer, n, g);
            }
        }

        // Clockwise from the top-left, in local y-up space.
        private int BuildOutline(Rect r, Vector2[] p)
        {
            float x0 = r.xMin, x1 = r.xMax, y0 = r.yMin, y1 = r.yMax;
            int n = 0;
            if (_skew > 0f)
            {
                float s = Mathf.Min(_skew, r.width * 0.45f);
                p[n++] = new Vector2(x0 + s, y1);
                p[n++] = new Vector2(x1, y1);
                p[n++] = new Vector2(x1 - s, y0);
                p[n++] = new Vector2(x0, y0);
                return n;
            }
            float max = Mathf.Min(r.width, r.height) * 0.49f;
            float cut = Mathf.Min(Mathf.Max(_chamfer, _corner), max); // TL and BR
            float soft = Mathf.Min(_corner, max);                      // TR and BL
            n = Corner(p, n, new Vector2(x0, y1 - cut), new Vector2(x0 + cut, y1), cut);
            n = Corner(p, n, new Vector2(x1 - soft, y1), new Vector2(x1, y1 - soft), soft);
            n = Corner(p, n, new Vector2(x1, y0 + cut), new Vector2(x1 - cut, y0), cut);
            n = Corner(p, n, new Vector2(x0 + soft, y0), new Vector2(x0, y0 + soft), soft);
            return n;
        }

        private static int Corner(Vector2[] p, int n, Vector2 a, Vector2 b, float cut)
        {
            if (cut <= 0.01f) { p[n++] = a; return n; } // a == b when uncut
            p[n++] = a;
            p[n++] = b;
            return n;
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
