using UnityEngine;

namespace SlapMania.Core.Paint
{
    /// One CSS background layer: a colour or a (repeating) linear / radial / conic gradient,
    /// optionally tiled at `TileW` x `TileH` px (the kit's `.dots` uses background-size).
    /// Plain data produced by CssPaintParser and consumed by PaintRaster.
    public sealed class PaintLayer
    {
        public PaintKind Kind;
        public bool Repeating;
        public Color Solid;

        /// Linear: the CSS angle (0 = to top, 90 = to right). Conic: the `from` angle.
        public float AngleDeg = 180f;

        // Radial
        public bool Circle;
        public bool HasSize;
        public CssLength SizeX, SizeY;

        // Radial / conic centre ("at X Y"), default 50% 50%.
        public CssLength AtX = CssLength.Pct(50f);
        public CssLength AtY = CssLength.Pct(50f);

        public ColorStop[] Stops = System.Array.Empty<ColorStop>();

        public float TileW, TileH;

        public bool IsTiled => TileW > 0f && TileH > 0f;

        public static PaintLayer FromColor(Color c) => new PaintLayer { Kind = PaintKind.Solid, Solid = c };
    }
}
