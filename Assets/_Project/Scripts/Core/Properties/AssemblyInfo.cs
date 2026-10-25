using System.Runtime.CompilerServices;

// MatchSignals (Core) exposes public `event`s for anyone to subscribe to, but keeps
// its Raise* methods `internal` so only the match's owner fires signals — the
// deliberate encapsulation described in ARCHITECTURE.md §5. The sole legitimate
// raiser, MatchDirector, lives in SlapMania.Gameplay, so Gameplay is granted friend
// access. No other assembly calls Raise* (subscribers only touch the public events).
[assembly: InternalsVisibleTo("SlapMania.Gameplay")]
