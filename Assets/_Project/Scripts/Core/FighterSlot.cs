namespace SlapMania.Core
{
    public enum FighterSlot : byte
    {
        Player = 0,
        Opponent = 1
    }

    public static class FighterSlotExtensions
    {
        public static FighterSlot Other(this FighterSlot s) =>
            s == FighterSlot.Player ? FighterSlot.Opponent : FighterSlot.Player;
    }
}
