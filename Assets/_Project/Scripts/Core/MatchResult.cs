namespace SlapMania.Core
{
    public readonly struct MatchResult
    {
        public readonly FighterSlot Winner;
        public readonly int TotalSlaps;
        public readonly int Rounds;
        public readonly bool ReachedMaxTurns;
        /// The loser gave up (PauseMenu's Forfeit) rather than being knocked out.
        public readonly bool Forfeited;

        public MatchResult(FighterSlot winner, int totalSlaps, int rounds, bool reachedMaxTurns, bool forfeited = false)
        {
            Winner = winner;
            TotalSlaps = totalSlaps;
            Rounds = rounds;
            ReachedMaxTurns = reachedMaxTurns;
            Forfeited = forfeited;
        }
    }
}
