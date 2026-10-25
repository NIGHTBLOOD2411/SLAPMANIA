namespace SlapMania.Core.Paint
{
    /// A CSS clip-path: polygon(x y, ...) or circle(r at x y). Coordinates are lengths that
    /// resolve against the element's width (x) and height (y).
    public sealed class ClipShape
    {
        public enum ShapeKind : byte { Polygon, Circle }

        public ShapeKind Kind;
        public CssLength[] Xs = System.Array.Empty<CssLength>();
        public CssLength[] Ys = System.Array.Empty<CssLength>();

        // Circle
        public CssLength Radius;
        public CssLength CenterX = CssLength.Pct(50f);
        public CssLength CenterY = CssLength.Pct(50f);

        public int PointCount => Xs.Length;
    }
}
