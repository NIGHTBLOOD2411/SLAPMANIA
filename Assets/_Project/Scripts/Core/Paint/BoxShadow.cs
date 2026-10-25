using UnityEngine;

namespace SlapMania.Core.Paint
{
    /// One CSS box-shadow: `[inset] x y [blur [spread]] color`. The kit uses outer glows
    /// (`0 0 34px rgba(...)`), hard rings (`0 0 0 1px ...`) and inset frames.
    public readonly struct BoxShadow
    {
        public readonly bool Inset;
        public readonly float OffsetX, OffsetY, Blur, Spread;
        public readonly Color Color;

        public BoxShadow(bool inset, float offsetX, float offsetY, float blur, float spread, Color color)
        {
            Inset = inset;
            OffsetX = offsetX;
            OffsetY = offsetY;
            Blur = blur < 0f ? 0f : blur;
            Spread = spread;
            Color = color;
        }

        /// How far the shadow can reach past the border box on any side, in px.
        public float Reach => Inset ? 0f : Mathf.Abs(Spread) + Blur + Mathf.Max(Mathf.Abs(OffsetX), Mathf.Abs(OffsetY));
    }
}
