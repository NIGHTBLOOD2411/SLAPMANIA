namespace SlapMania.Core
{
    /// How a turn's slap is played (ADR-0014). Serialized in BoutConfig: append values only.
    public enum AttackMode : byte
    {
        /// M03 rapid-tap charge, then M04's ping-pong marker: tap to commit.
        TapMarker = 0,
        /// V2 C5/C6: press and hold to wind up while the closing ring shrinks; release on the
        /// gold band. Power and multiplier both come from the one hold (M04 §5b).
        HoldRing = 1
    }
}
