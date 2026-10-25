using UnityEngine;

namespace SlapMania.Core.Paint
{
    /// One gradient colour stop. `Position` is a fraction of the gradient line (or of a full
    /// turn for conic) when `Unit` is Fraction, pixels along the line when it is Px, and
    /// unset when `HasPosition` is false (CSS spreads unset stops evenly).
    public readonly struct ColorStop
    {
        public enum PositionUnit : byte { Fraction, Px }

        public readonly Color Color;
        public readonly float Position;
        public readonly bool HasPosition;
        public readonly PositionUnit Unit;

        public ColorStop(Color color, float position, PositionUnit unit)
        {
            Color = color;
            Position = position;
            HasPosition = true;
            Unit = unit;
        }

        public ColorStop(Color color)
        {
            Color = color;
            Position = 0f;
            HasPosition = false;
            Unit = PositionUnit.Fraction;
        }
    }
}
