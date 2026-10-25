using SlapMania.Core;

namespace SlapMania.Gameplay
{
    public enum TimingTendency : byte { None, Early, Late, NoTap }

    /// Per-fighter tallies for one bout — what the V2 KO / Victory / Defeat screens report
    /// (D3, E2, E3): slaps landed, best slap, perfect hits, average power, charge speed,
    /// timing accuracy and whether the player tends to tap early or late.
    ///
    /// Pure (R2): the Record* methods take plain results and are unit-tested directly; Attach
    /// wires them to a live MatchDirector. Holds fixed-size arrays only, so recording during a
    /// bout allocates nothing (R5).
    public sealed class SlapStats
    {
        public struct Fighter
        {
            public int Slaps;            // impacts dealt
            public int TotalDamage;
            public int BestSlap;
            public int DamageTaken;
            public int Commits;          // timing results, including timeouts
            public int Perfects;
            public int Early;
            public int Late;
            public int OnTime;           // landed in the Perfect zone
            public int TimedOut;
            public float SumCloseness;   // 1 at the centre, 0 at either end
            public float SumPower;
            public float PeakTps;
            public int Charges;

            public float TimingAccuracy01 => Commits > 0 ? SumCloseness / Commits : 0f;
            public float AveragePower => Charges > 0 ? SumPower / Charges : 0f;
            public bool Flawless => DamageTaken == 0;

            /// The single most useful thing to tell this fighter, or None if timing was fine.
            public TimingTendency Tendency
            {
                get
                {
                    if (Commits == 0) return TimingTendency.None;
                    if (TimedOut * 2 > Commits) return TimingTendency.NoTap;
                    int misses = Early + Late;
                    if (misses * 2 < Commits) return TimingTendency.None;
                    return Early >= Late ? TimingTendency.Early : TimingTendency.Late;
                }
            }
        }

        private readonly Fighter[] _fighters = new Fighter[2];
        private MatchDirector _director;
        private float _perfectCentre = 0.5f;

        public Fighter this[FighterSlot slot] => _fighters[(int)slot];
        public int LastFinalBlow { get; private set; }

        public void Reset()
        {
            _fighters[0] = default;
            _fighters[1] = default;
            LastFinalBlow = 0;
        }

        // ── Pure recording ───────────────────────────────────────────
        public void RecordCharge(FighterSlot attacker, in PowerResult r)
        {
            ref var f = ref _fighters[(int)attacker];
            f.Charges++;
            f.SumPower += r.RawPower;
            if (r.PeakTps > f.PeakTps) f.PeakTps = r.PeakTps;
        }

        public void RecordTiming(FighterSlot attacker, in TimingResult r, float perfectCentre)
        {
            ref var f = ref _fighters[(int)attacker];
            f.Commits++;
            if (float.IsNaN(r.Position)) { f.TimedOut++; return; } // no credit for a broken reading
            float pos = r.Position;
            float dist = System.Math.Abs(pos - perfectCentre);
            float closeness = 1f - dist / 0.5f;
            f.SumCloseness += closeness < 0f ? 0f : closeness > 1f ? 1f : closeness;

            if (r.Zone == TimingZone.Perfect) { f.Perfects++; f.OnTime++; return; }
            if (r.TimedOut) { f.TimedOut++; return; }
            if ((pos - perfectCentre) * r.Direction < 0f) f.Early++;
            else f.Late++;
        }

        public void RecordImpact(FighterSlot attacker, in DamageResult d)
        {
            ref var a = ref _fighters[(int)attacker];
            a.Slaps++;
            a.TotalDamage += d.FinalDamage;
            if (d.FinalDamage > a.BestSlap) a.BestSlap = d.FinalDamage;
            _fighters[(int)attacker.Other()].DamageTaken += d.FinalDamage;
            if (d.IsLethal) LastFinalBlow = d.FinalDamage;
        }

        // ── Live wiring ──────────────────────────────────────────────
        public void Attach(MatchDirector director, float perfectCentre)
        {
            Detach();
            Reset();
            _director = director;
            _perfectCentre = perfectCentre;
            if (_director == null) return;
            _director.Signals.ChargeEnd   += OnChargeEnd;
            _director.Signals.TimingEnd   += OnTimingEnd;
            _director.Signals.ImpactFrame += OnImpact;
        }

        public void Detach()
        {
            if (_director == null) return;
            _director.Signals.ChargeEnd   -= OnChargeEnd;
            _director.Signals.TimingEnd   -= OnTimingEnd;
            _director.Signals.ImpactFrame -= OnImpact;
            _director = null;
        }

        private void OnChargeEnd(PowerResult r) => RecordCharge(_director.CurrentTurn.Attacker, r);
        private void OnTimingEnd(TimingResult r) => RecordTiming(_director.CurrentTurn.Attacker, r, _perfectCentre);
        private void OnImpact(DamageResult d) => RecordImpact(_director.CurrentTurn.Attacker, d);
    }
}
