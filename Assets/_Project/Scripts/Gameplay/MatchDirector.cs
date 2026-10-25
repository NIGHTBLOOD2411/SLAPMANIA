using System;
using UnityEngine;
using SlapMania.Core;
using SlapMania.Data;
using SlapMania.Logging;

namespace SlapMania.Gameplay
{
    /// FSM. Pure C#. Predict-branch-commit: Resolve is pure, Commit fires at impact.
    /// The loop: TurnStart → Charge → Timing → Resolve → Windup → Impact → Hitstop
    ///           → Snapshot → (Finisher → FinisherSnapshot)? → TurnEnd|KO → BoutEnd
    /// In AttackMode.HoldRing (ADR-0014) one state, HoldRing, replaces Charge → Timing. It raises
    /// the same signals so every listener keeps working, in gesture order: TimingStart when the
    /// ring opens, ChargeStart at the press (the wind-up), ChargeEnd + TimingEnd at the release.
    ///
    /// Brace (ADR-0019): on the rival's turn, Windup listens to the player's taps. The first one
    /// is judged against the brace target (impact - braceLeadMs) as Windup ends (BraceJudge), and a brace re-resolves
    /// the slap with DamageInput.Reduction before Impact commits it.
    ///
    /// Pause is orthogonal to the state: RequestPause stops the director's own clock (a
    /// PausableClock over the injected one), so CurrentState keeps naming the real phase and
    /// every timer resumes exactly where it stopped. Forfeit ends a live bout at once.
    public sealed class MatchDirector
    {
        // ─── Public API ──────────────────────────────────────────────
        public MatchSignals Signals { get; } = new MatchSignals();
        public MatchState CurrentState { get; private set; } = MatchState.Idle;
        public TurnInfo CurrentTurn => _turn;
        public PowerMeter PowerMeter { get; } = new PowerMeter();
        public TimingMeter TimingMeter { get; } = new TimingMeter();
        public HoldRingMeter HoldRingMeter { get; } = new HoldRingMeter();
        /// The attack mode of the current turn, fixed at TurnStart (and at StartBout for the intro).
        public AttackMode CurrentAttackMode { get; private set; } = AttackMode.TapMarker;
        public HealthTracker PlayerHealth { get; }
        public HealthTracker OpponentHealth { get; }
        public int TotalSlaps { get; private set; }
        /// The brace on the latest rival slap (NoBrace on the player's own turns).
        public BraceResult LastBrace => _lastBrace;
        /// The rival is winding up and the player's brace tap is being listened for.
        public bool IsBraceWindowOpen => _braceOpen;

        /// Simulation time: the injected clock minus every pause. Presentation that times a
        /// phase (the HUD shot clock, the intro countdown) reads this so it freezes too.
        public double Now => _clock.Now;
        public bool IsPaused => _clock.IsPaused;

        /// Started and not over: pause and forfeit apply.
        public bool IsLive => CurrentState != MatchState.Idle && CurrentState != MatchState.KO
                           && CurrentState != MatchState.BoutEnd;

        // ─── Wiring ──────────────────────────────────────────────────
        private readonly BoutConfig _bout;
        private readonly PowerPhaseConfig _powerCfg;
        private readonly TimingPhaseConfig _timingCfg;
        private readonly TimingZoneLayout _zoneLayout;
        private readonly DamageConfig _damageCfg;
        private readonly HoldRingConfig _holdRingCfg;
        private readonly PausableClock _clock;
        private readonly IRandom _rng;

        private PausableTapSource _playerSource;
        private AiTapSource _opponentSource;
        private AiProfile _opponentProfile;

        // ─── Pacing (BoutConfig, authored in ms per R8) ──────────────
        // Read on every check rather than cached, so Play Mode edits to the asset apply live.
        private double IntroDuration            => _bout.introMs * 0.001;
        private double WindupDuration           => _bout.windupMs * 0.001;
        private double HitstopDuration          => _bout.hitstopMs * 0.001;
        private double SnapshotDuration         => _bout.snapshotMs * 0.001;
        private double FinisherDuration         => _bout.finisherMs * 0.001;
        private double FinisherSnapshotDuration => _bout.finisherSnapshotMs * 0.001;
        private double TurnTransitionDelay      => _bout.turnTransitionMs * 0.001;

        // ─── FSM state ───────────────────────────────────────────────
        private TurnInfo _turn;
        private double _lastTickAt;
        private double _stateEnteredAt;
        private PowerResult _lastPower;
        private TimingResult _lastTiming;
        private DamageResult _lastDamage;
        private bool _windUpAnnounced;
        private bool _warnedHoldFallback;

        // Brace window (ADR-0019)
        private readonly Action<TapEvent> _onBraceTap;
        private bool _braceOpen;
        private double _braceOpenedAt;
        private double _braceTapAt = double.NaN;
        private double _braceTargetAt;
        private BraceResult _lastBrace;

        /// `holdRingCfg` may be null only while bout.attackMode is TapMarker; HoldRing without it
        /// falls back to TapMarker with one Error (GameConfig.Validate refuses that config).
        public MatchDirector(BoutConfig bout, PowerPhaseConfig powerCfg, TimingPhaseConfig timingCfg,
                             TimingZoneLayout zoneLayout, DamageConfig damageCfg,
                             IClock clock, IRandom rng, HoldRingConfig holdRingCfg = null)
        {
            SlapAssert.NotNull(bout, nameof(bout));
            SlapAssert.NotNull(powerCfg, nameof(powerCfg));
            SlapAssert.NotNull(timingCfg, nameof(timingCfg));
            SlapAssert.NotNull(zoneLayout, nameof(zoneLayout));
            SlapAssert.NotNull(damageCfg, nameof(damageCfg));
            SlapAssert.NotNull(clock, nameof(clock));
            SlapAssert.NotNull(rng, nameof(rng));
            _bout = bout;
            _powerCfg = powerCfg;
            _timingCfg = timingCfg;
            _zoneLayout = zoneLayout;
            _damageCfg = damageCfg;
            _holdRingCfg = holdRingCfg;
            _clock = new PausableClock(clock);
            _rng = rng;
            PlayerHealth = new HealthTracker(FighterSlot.Player);
            OpponentHealth = new HealthTracker(FighterSlot.Opponent);
            _onBraceTap = OnBraceTap; // cached: a method-group subscription allocates (S-089)
        }

        /// The player's taps, stamped on the injected clock's timeline (Input System event
        /// time). They reach the meters through a pause gate (PausableTapSource).
        public void BindPlayerSource(ITapSource source)
        {
            _playerSource?.Detach();
            _playerSource = source != null ? new PausableTapSource(source, _clock) : null;
        }
        public void BindOpponent(AiProfile profile)
        {
            _opponentProfile = profile;
            _opponentSource = new AiTapSource(_clock, _rng);
            _opponentSource.Configure(profile);
        }

        public void StartBout()
        {
            SlapAssert.NotNull(_playerSource, "player source");
            SlapAssert.NotNull(_opponentSource, "opponent source");
            PlayerHealth.Init(_bout.startingHp);
            OpponentHealth.Init(_bout.startingHp);
            TotalSlaps = 0;
            _turn = new TurnInfo(0, 0, _bout.playerGoesFirst ? FighterSlot.Player : FighterSlot.Opponent);
            CurrentAttackMode = ResolveAttackMode(); // the intro's hints name the verb
            _lastTickAt = _clock.Now;
            EnterState(MatchState.BoutIntro);
            Signals.RaiseBoutIntroStart();
        }

        // ─── Pause / forfeit ─────────────────────────────────────────
        /// Freeze the bout: the simulation clock stops, Tick does nothing and the player's taps
        /// are dropped until RequestResume. Refused before StartBout, once the bout is over,
        /// and when already paused. Returns whether the bout is now paused by this call.
        public bool RequestPause()
        {
            if (_clock.IsPaused || !IsLive) return false;
            _clock.Pause();
            return true;
        }

        public bool RequestResume()
        {
            if (!_clock.IsPaused) return false;
            _clock.Resume();
            return true;
        }

        /// True once a lethal slap has landed: the winner is known even though the finisher
        /// and its snapshot are still playing, so a forfeit can no longer change the result.
        public bool IsOutcomeDecided =>
            !IsLive || (_lastDamage.IsLethal && CurrentState >= MatchState.Impact && CurrentState <= MatchState.FinisherSnapshot);

        /// `quitter` gives up (PauseMenu's Forfeit): the bout ends now with the other fighter
        /// as winner and MatchResult.Forfeited set. Works paused or running. Any open meter is
        /// closed first so no tap can land afterwards. Refused once the outcome is decided.
        public bool Forfeit(FighterSlot quitter)
        {
            if (IsOutcomeDecided) return false;
            if (_clock.IsPaused) _clock.Resume();
            if (CurrentState == MatchState.Charge) PowerMeter.End();
            else if (CurrentState == MatchState.Timing) TimingMeter.ForceResolve();
            else if (CurrentState == MatchState.HoldRing) HoldRingMeter.ForceResolve();
            if (_opponentSource != null) { _opponentSource.EndCharge(); _opponentSource.Disable(); }
            CloseBraceWindow();
            EnterState(MatchState.BoutEnd);
            Signals.RaiseBoutEnd(new MatchResult(quitter.Other(), TotalSlaps, _turn.RoundIndex, false, forfeited: true));
            return true;
        }

        public void Tick()
        {
            if (_clock.IsPaused) return;
            double now = _clock.Now;
            double dt = now - _lastTickAt;
            if (dt < 0) dt = 0;
            _lastTickAt = now;

            // Sub-tick the active tap source (AI is the only one that self-ticks).
            if (_turn.Attacker == FighterSlot.Opponent) _opponentSource?.Tick(dt);

            switch (CurrentState)
            {
                case MatchState.BoutIntro:
                    if (TimeInState() >= IntroDuration)
                    {
                        Signals.RaiseBoutIntroEnd();
                        EnterState(MatchState.TurnStart);
                    }
                    break;

                case MatchState.TurnStart:
                    CurrentAttackMode = ResolveAttackMode(); // per turn: Play Mode switches apply next turn
                    Signals.RaiseTurnStart(_turn);
                    if (CurrentAttackMode == AttackMode.HoldRing) EnterHoldRing();
                    else EnterCharge();
                    break;

                case MatchState.HoldRing:
                    HoldRingMeter.Tick(dt);
                    // The press is the wind-up: announce it as the charge (attacker's charge pose, C6 HUD).
                    if (!_windUpAnnounced && HoldRingMeter.HasPressed)
                    {
                        _windUpAnnounced = true;
                        Signals.RaiseChargeStart(_turn);
                    }
                    if (HoldRingMeter.CurrentState == HoldRingMeter.State.Resolved)
                    {
                        _lastPower = HoldRingMeter.PowerResult;
                        _lastTiming = HoldRingMeter.TimingResult;
                        if (_turn.Attacker == FighterSlot.Opponent) _opponentSource.EndCharge();
                        Signals.RaiseChargeEnd(_lastPower);
                        Signals.RaiseTimingEnd(_lastTiming);
                        EnterState(MatchState.Resolve);
                    }
                    break;

                case MatchState.Charge:
                    PowerMeter.Tick(dt);
                    if (PowerMeter.IsComplete)
                    {
                        _lastPower = PowerMeter.End();
                        if (_turn.Attacker == FighterSlot.Opponent) _opponentSource.EndCharge();
                        Signals.RaiseChargeEnd(_lastPower);
                        EnterTiming();
                    }
                    break;

                case MatchState.Timing:
                    TimingMeter.Tick(dt);
                    if (TimingMeter.CurrentState == TimingMeter.State.Resolved)
                    {
                        _lastTiming = TimingMeter.Result;
                        Signals.RaiseTimingEnd(_lastTiming);
                        EnterState(MatchState.Resolve);
                    }
                    break;

                case MatchState.Resolve:
                {
                    var defenderHp = _turn.Defender == FighterSlot.Player ? PlayerHealth : OpponentHealth;
                    var input = new DamageInput(_lastPower.RawPower, _lastTiming.Multiplier, _turn.SlapIndex,
                                                defenderHp.CurrentHp, defenderHp.MaxHp, 0f);
                    _lastDamage = DamageResolver.Resolve(input, _damageCfg);
                    _lastBrace = BraceResult.NoBrace;
                    Signals.RaiseDamageResolved(_lastDamage);
                    EnterState(MatchState.Windup);
                    if (_turn.Attacker == FighterSlot.Opponent) OpenBraceWindow();
                    Signals.RaiseWindupStart(_clock.Now + WindupDuration);
                    break;
                }

                case MatchState.Windup:
                    if (TimeInState() >= WindupDuration)
                    {
                        if (_braceOpen) ResolveBrace();
                        EnterState(MatchState.Impact);
                    }
                    break;

                case MatchState.Impact:
                {
                    var defenderHp = _turn.Defender == FighterSlot.Player ? PlayerHealth : OpponentHealth;
                    Signals.RaiseImpactFrame(_lastDamage);
                    DamageResolver.Commit(_lastDamage, defenderHp);
                    Signals.RaiseHealthChanged(new HealthChangedEvent(_turn.Defender,
                        defenderHp.CurrentHp + _lastDamage.FinalDamage,
                        defenderHp.CurrentHp,
                        defenderHp.MaxHp));
                    EnterState(MatchState.Hitstop);
                    break;
                }

                case MatchState.Hitstop:
                    if (TimeInState() >= HitstopDuration)
                    {
                        // Every hit produces a snapshot. Finisher first if lethal.
                        if (_lastDamage.IsFinisher) EnterState(MatchState.Finisher);
                        else                        { EnterState(MatchState.Snapshot); Signals.RaiseSnapshotRequested(_lastDamage); }
                    }
                    break;

                case MatchState.Snapshot:
                    if (TimeInState() >= SnapshotDuration)
                    {
                        TotalSlaps++;
                        if (_lastDamage.IsLethal) FinishBoutFromKO();
                        else                     EnterState(MatchState.TurnEnd);
                    }
                    break;

                case MatchState.Finisher:
                    Signals.RaiseFinisherStart(_lastDamage);
                    EnterState(MatchState.FinisherSnapshot);
                    Signals.RaiseFinisherSnapshotRequested(_lastDamage);
                    break;

                case MatchState.FinisherSnapshot:
                    if (TimeInState() >= FinisherSnapshotDuration + FinisherDuration)
                    {
                        TotalSlaps++;
                        FinishBoutFromKO();
                    }
                    break;

                case MatchState.TurnEnd:
                    if (TimeInState() >= TurnTransitionDelay)
                    {
                        Signals.RaiseTurnEnd(_turn);
                        AdvanceTurn();
                    }
                    break;

                case MatchState.KO:
                case MatchState.BoutEnd:
                case MatchState.Idle:
                default:
                    break;
            }
        }

        // ─── Brace (ADR-0019) ────────────────────────────────────────
        private void OpenBraceWindow()
        {
            if (!_damageCfg.braceEnabled || _playerSource == null) return;
            _braceOpen = true;
            _braceOpenedAt = _clock.Now;
            // Before the impact by braceLeadMs, so a tap the player times to what they see
            // (one display bias late) still arrives before the impact commits.
            _braceTargetAt = _braceOpenedAt + WindupDuration - _damageCfg.braceLeadMs * 0.001;
            _braceTapAt = double.NaN;
            _playerSource.OnTap += _onBraceTap;
            _playerSource.Enable();
        }

        private void CloseBraceWindow()
        {
            if (!_braceOpen) return;
            _braceOpen = false;
            _playerSource.OnTap -= _onBraceTap;
            _playerSource.Disable();
        }

        // First tap only: a masher's first tap is early, so mashing flinches. A tap stamped
        // before the window opened is a leftover from before the wind-up and is ignored.
        private void OnBraceTap(TapEvent e)
        {
            if (!_braceOpen || !double.IsNaN(_braceTapAt)) return;
            double t = e.EventTime;
            if (double.IsNaN(t) || t < _braceOpenedAt) return;
            _braceTapAt = t;
        }

        /// Judge the tap and, if it cut anything, re-resolve the slap before Impact commits it.
        /// A brace can save the player from a lethal slap.
        private void ResolveBrace()
        {
            CloseBraceWindow();
            _lastBrace = BraceJudge.Judge(_braceTapAt, _braceTargetAt, _timingCfg.displayLatencyBias, _damageCfg);
            if (_lastBrace.Reduction > 0f)
            {
                var defenderHp = _turn.Defender == FighterSlot.Player ? PlayerHealth : OpponentHealth;
                var input = new DamageInput(_lastPower.RawPower, _lastTiming.Multiplier, _turn.SlapIndex,
                                            defenderHp.CurrentHp, defenderHp.MaxHp, _lastBrace.Reduction);
                _lastDamage = DamageResolver.Resolve(input, _damageCfg);
            }
            Signals.RaiseBraceResolved(_lastBrace);
        }

        // ─── Helpers ─────────────────────────────────────────────────
        private void EnterState(MatchState s)
        {
            CurrentState = s;
            _stateEnteredAt = _clock.Now;
        }

        private double TimeInState() => _clock.Now - _stateEnteredAt;

        private void EnterCharge()
        {
            EnterState(MatchState.Charge);
            var source = _turn.Attacker == FighterSlot.Player ? _playerSource : (ITapSource)_opponentSource;
            PowerMeter.Begin(_powerCfg, source);
            if (_turn.Attacker == FighterSlot.Opponent)
            {
                _opponentSource.Enable();
                _opponentSource.BeginCharge(_powerCfg.phaseDuration);
            }
            Signals.RaiseChargeStart(_turn);
        }

        private void EnterTiming()
        {
            EnterState(MatchState.Timing);
            var source = _turn.Attacker == FighterSlot.Player ? _playerSource : (ITapSource)_opponentSource;
            // The spam gap starts at the attacker's last charge tap (DEBUG S-093).
            TimingMeter.Begin(_timingCfg, _zoneLayout, source, _clock, _turn.RoundIndex + 1, _rng,
                              _lastPower.Overcharge, _powerCfg.overchargeSpeedPenalty, PowerMeter.LastTapTime);
            if (_turn.Attacker == FighterSlot.Opponent)
            {
                _opponentSource.Enable();
                _opponentSource.BeginTiming(TimingMeter.StartPhase, TimingMeter.Sweep, _zoneLayout,
                                            TimingMeter.ZoneScale, _timingCfg.timeout, _timingCfg.displayLatencyBias,
                                            TimingMeter.CueSeconds, TimingMeter.EarliestCommitElapsed);
            }
            Signals.RaiseTimingStart(_turn);
        }

        /// The ring opens (C5 Aim): TimingStart now; ChargeStart follows at the press (Tick).
        private void EnterHoldRing()
        {
            EnterState(MatchState.HoldRing);
            _windUpAnnounced = false;
            IHoldSource source = _turn.Attacker == FighterSlot.Player ? _playerSource : (IHoldSource)_opponentSource;
            HoldRingMeter.Begin(_holdRingCfg, _timingCfg, _powerCfg, _zoneLayout, source, _clock, _turn.RoundIndex + 1);
            if (_turn.Attacker == FighterSlot.Opponent)
            {
                _opponentSource.Enable();
                _opponentSource.BeginHoldRing(_zoneLayout, HoldRingMeter.ZoneScale, HoldRingMeter.CloseSeconds,
                                              HoldRingMeter.ArmDelaySeconds, _timingCfg.displayLatencyBias);
            }
            Signals.RaiseTimingStart(_turn);
        }

        /// BoutConfig.attackMode, unless HoldRing cannot run: no HoldRingConfig, or a player
        /// source that only taps (no release events). Then TapMarker, with one Error.
        private AttackMode ResolveAttackMode()
        {
            if (_bout.attackMode != AttackMode.HoldRing) return AttackMode.TapMarker;
            bool playerCanHold = _playerSource != null && _playerSource.SupportsHold;
            if (_holdRingCfg != null && playerCanHold) return AttackMode.HoldRing;
            if (!_warnedHoldFallback)
            {
                _warnedHoldFallback = true;
                SlapLog.Error(LogChannel.Match, _holdRingCfg == null
                    ? "attackMode is HoldRing but no HoldRingConfig was given; playing TapMarker."
                    : "attackMode is HoldRing but the player's tap source has no release events; playing TapMarker.");
            }
            return AttackMode.TapMarker;
        }

        private void AdvanceTurn()
        {
            int nextSlap = _turn.SlapIndex + 1;
            var nextAttacker = _turn.Attacker.Other();
            int nextRound = _turn.RoundIndex + (nextAttacker == (_bout.playerGoesFirst ? FighterSlot.Player : FighterSlot.Opponent) ? 1 : 0);
            _turn = new TurnInfo(nextSlap, nextRound, nextAttacker);

            if (nextSlap >= _bout.maxTurns)
            {
                var w = PlayerHealth.CurrentHp >= OpponentHealth.CurrentHp ? FighterSlot.Player : FighterSlot.Opponent;
                EnterState(MatchState.BoutEnd);
                Signals.RaiseBoutEnd(new MatchResult(w, TotalSlaps, _turn.RoundIndex, true));
                return;
            }
            EnterState(MatchState.TurnStart);
        }

        private void FinishBoutFromKO()
        {
            var loser = _turn.Defender;
            EnterState(MatchState.KO);
            Signals.RaiseKO(loser);
            EnterState(MatchState.BoutEnd);
            Signals.RaiseBoutEnd(new MatchResult(loser.Other(), TotalSlaps, _turn.RoundIndex, false));
        }
    }
}
