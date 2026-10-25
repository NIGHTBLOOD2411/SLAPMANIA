using System;

namespace SlapMania.Core
{
    /// Per-match signal bus with explicit events. Deliberately not a static generic bus.
    /// See ARCHITECTURE.md §5.
    public sealed class MatchSignals
    {
        public event Action BoutIntroStart;
        public event Action BoutIntroEnd;
        public event Action<TurnInfo> TurnStart;
        public event Action<TurnInfo> ChargeStart;
        public event Action<PowerResult> ChargeEnd;
        public event Action<TurnInfo> TimingStart;
        public event Action<TimingResult> TimingEnd;
        public event Action<DamageResult> DamageResolved;
        public event Action<double> WindupStart;
        /// The player's brace against the rival's slap, judged as the wind-up ends, before
        /// ImpactFrame (whose DamageResult already carries the reduction). Opponent turns only.
        public event Action<BraceResult> BraceResolved;
        public event Action<DamageResult> ImpactFrame;
        public event Action<HealthChangedEvent> HealthChanged;
        public event Action<DamageResult> SnapshotRequested;
        public event Action<DamageResult> FinisherStart;
        public event Action<DamageResult> FinisherSnapshotRequested;
        public event Action<TurnInfo> TurnEnd;
        public event Action<FighterSlot> KO;
        public event Action<MatchResult> BoutEnd;

        internal void RaiseBoutIntroStart() => BoutIntroStart?.Invoke();
        internal void RaiseBoutIntroEnd() => BoutIntroEnd?.Invoke();
        internal void RaiseTurnStart(in TurnInfo t) => TurnStart?.Invoke(t);
        internal void RaiseChargeStart(in TurnInfo t) => ChargeStart?.Invoke(t);
        internal void RaiseChargeEnd(in PowerResult r) => ChargeEnd?.Invoke(r);
        internal void RaiseTimingStart(in TurnInfo t) => TimingStart?.Invoke(t);
        internal void RaiseTimingEnd(in TimingResult r) => TimingEnd?.Invoke(r);
        internal void RaiseDamageResolved(in DamageResult r) => DamageResolved?.Invoke(r);
        internal void RaiseWindupStart(double contactDsp) => WindupStart?.Invoke(contactDsp);
        internal void RaiseBraceResolved(in BraceResult r) => BraceResolved?.Invoke(r);
        internal void RaiseImpactFrame(in DamageResult r) => ImpactFrame?.Invoke(r);
        internal void RaiseHealthChanged(in HealthChangedEvent e) => HealthChanged?.Invoke(e);
        internal void RaiseSnapshotRequested(in DamageResult r) => SnapshotRequested?.Invoke(r);
        internal void RaiseFinisherStart(in DamageResult r) => FinisherStart?.Invoke(r);
        internal void RaiseFinisherSnapshotRequested(in DamageResult r) => FinisherSnapshotRequested?.Invoke(r);
        internal void RaiseTurnEnd(in TurnInfo t) => TurnEnd?.Invoke(t);
        internal void RaiseKO(FighterSlot loser) => KO?.Invoke(loser);
        internal void RaiseBoutEnd(in MatchResult r) => BoutEnd?.Invoke(r);
    }
}
