namespace SlapMania.Core
{
    public enum MatchState : byte
    {
        Idle,
        BoutIntro,
        TurnStart,
        Charge,
        Timing,
        Resolve,
        Windup,
        Impact,
        Hitstop,
        Snapshot,
        Finisher,
        FinisherSnapshot,
        TurnEnd,
        KO,
        BoutEnd,
        /// Never entered: pause is MatchDirector.IsPaused, orthogonal to the phase, so the
        /// state keeps naming the real phase while paused (M01 §6). Kept so values stay stable.
        Paused,
        /// The V2 hold-ring attack (ADR-0014): replaces Charge + Timing when the turn's attack
        /// mode is HoldRing. Appended, so the values above stay stable.
        HoldRing
    }
}
