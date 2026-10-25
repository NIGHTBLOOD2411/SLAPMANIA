namespace SlapMania.Core
{
    public readonly struct TurnInfo
    {
        public readonly int SlapIndex;
        public readonly int RoundIndex;
        public readonly FighterSlot Attacker;
        public readonly FighterSlot Defender;

        public TurnInfo(int slapIndex, int roundIndex, FighterSlot attacker)
        {
            SlapIndex = slapIndex;
            RoundIndex = roundIndex;
            Attacker = attacker;
            Defender = attacker.Other();
        }
    }
}
