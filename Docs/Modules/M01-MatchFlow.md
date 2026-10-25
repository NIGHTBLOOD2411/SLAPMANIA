---
module: M01-MatchFlow
status: InProgress
owner: shobhit
last-verified: 2026-10-23
verified-against: (pre-git)
asmdef: SlapMania.Gameplay
---

# M01 — MatchFlow

> **One-liner:** The finite state machine that drives one complete bout from intro to victory screen, owning every phase transition and the turn order.

## 1. Purpose

Everything in the game happens because MatchFlow says so. It decides whose turn it is, tells the power and timing modules to begin and end, asks the damage module to resolve, triggers the finisher branch, and ultimately declares a winner. Without it the game is a pile of subsystems with no sequencing. The FSM is the contract that every other module reads to know what phase the game is in.

## 2. Responsibilities and non-goals

**Owns**
- The bout state machine and every transition.
- Turn order (alternating, player-first in v1).
- Phase sequencing within a turn: charge → timing → resolve → impact → (finisher?) → snapshot → next turn.
- The round counter and the slap index.
- Broadcasting `MatchSignal` events at every transition so listeners do not need to poll.

**Explicitly does NOT do**
- Compute power → `M03`.
- Compute multiplier → `M04`.
- Compute or apply damage → `M05` / `M06`.
- Decide AI taps → `M07`.
- Animate anything → `M08`.
- Capture photos → `M09`.
- Own UI transitions → `M10`.
- Play sounds → `M11`.

## 3. Public API surface

| Member | Signature | Notes |
|---|---|---|
| `MatchDirector.StartBout` | `void StartBout(BoutConfig cfg)` | Entry point from menu |
| `MatchDirector.CurrentState` | `MatchState { get; }` | Polled by debug overlay |
| `MatchDirector.CurrentTurn` | `TurnInfo { get; }` | Who is slapping, who is receiving, slap index |
| `MatchDirector.Signal` | `event Action<MatchSignal>` | The backbone event — every module subscribes |
| `MatchDirector.Tick` | `void Tick(double dt)` | Called by the MonoBehaviour driver in `Update` |
| `MatchDirector.RequestPause` | `bool RequestPause()` | **Landed 2026-10-20.** Stops the director's clock. Refused (false) before `StartBout`, once the bout is over (`!IsLive`) and when already paused |
| `MatchDirector.RequestResume` | `bool RequestResume()` | **Landed.** False when not paused |
| `MatchDirector.IsPaused` / `IsLive` | `bool { get; }` | `IsLive`: started and not KO/BoutEnd |
| `MatchDirector.Now` | `double { get; }` | Simulation time: the injected clock minus every pause. Presentation that times a phase reads this (M10 shot clock, intro countdown) |
| `MatchDirector.Forfeit` | `bool Forfeit(FighterSlot quitter)` | **Landed.** Ends a live bout now; the other fighter wins, `MatchResult.Forfeited` is set. Works paused; refused once `IsOutcomeDecided` |
| `MatchDirector.IsOutcomeDecided` | `bool { get; }` | A lethal slap has landed (Impact .. FinisherSnapshot) or the bout is over |
| `MatchDirector(..., HoldRingConfig holdRingCfg = null)` | ctor | **Landed 2026-10-23** ([ADR-0014](../Decisions/ADR-0014-hold-ring-attack-mode.md)). Optional last argument; required for HoldRing |
| `MatchDirector.CurrentAttackMode` | `AttackMode { get; }` | **Landed 2026-10-23.** The current turn's mode, fixed at `TurnStart` (and at `StartBout` for the intro hints). Read from `BoutConfig.attackMode` every turn, so a Play Mode switch applies to the next turn. HoldRing falls back to TapMarker, with one Error, when there is no `HoldRingConfig` or the player's source cannot report releases |
| `MatchDirector.HoldRingMeter` | `HoldRingMeter { get; }` | **Landed 2026-10-23.** The HoldRing turn's meter (M04 §5b), polled by the HUD |

`MatchSignal` is a `readonly struct`: `MatchSignalType type`, `TurnInfo turn`, `object payload` (boxed only for debug, never in hot path — the type enum carries enough for all production listeners).

`TurnInfo` is a `readonly struct`: `int slapIndex`, `FighterSlot attacker`, `FighterSlot defender`, `int roundIndex`.

### `SlapStats` (landed 2026-09-26)

`SlapMania.Gameplay.SlapStats` keeps the per-fighter tallies for one bout that the V2 KO, Victory and Defeat screens report (M10 `HudResult`).

- **Pure (R2):**
  - `RecordCharge(attacker, in PowerResult)`
  - `RecordTiming(attacker, in TimingResult, perfectCentre)`
  - `RecordImpact(attacker, in DamageResult)`

  They take plain results and are unit-tested directly (`SlapStatsTests`). `Attach(director, perfectCentre)` / `Detach()` wire them to a live `MatchDirector`.
- **Allocation:** fixed-size arrays only, so recording allocates nothing (R5).
- **Per fighter:** `Slaps`, `TotalDamage`, `BestSlap`, `DamageTaken`, `Commits`, `Perfects`, `Early`, `Late`, `OnTime`, `TimedOut`, `PeakTps`, `Charges`.
- **Derived:** `TimingAccuracy01`, `AveragePower`, `Flawless`, and `Tendency` (`None / Early / Late / NoTap`) for coaching. Early vs late comes from `TimingResult.Direction` (M04).
- `LastFinalBlow` is the damage of the lethal slap.

### Pause and forfeit (landed 2026-10-20)

Pause is **orthogonal to the state**, not a state: `CurrentState` keeps naming the real phase
(the HUD and `SlapStats` keep reading it), and resuming needs no "previous state" bookkeeping.

- **The clock stops, not the states.** The director wraps the injected `IClock` in a
  `Core.PausableClock` and hands *that* to `TimingMeter`, `AiTapSource` and its own state timers.
  While paused `Now` holds still; on resume it carries on from the paused instant, so the charge
  window, the marker's elapsed time (R8: a pure function of elapsed time), the windup and every
  other timer simply never see the pause. `Tick` returns at once while paused, so one-tick states
  (TurnStart, Resolve, Impact, Finisher) cannot advance either.
- **Player input is gated.** `BindPlayerSource` wraps the source in `Gameplay.PausableTapSource`:
  taps (and hold-ring releases, ADR-0014) made while paused are dropped - including one made during
  the pause but delivered on the frame after resume (`PausableClock.WasPausedAt`) - and the rest are
  re-stamped from source time (Input System event time) to simulation time with `FromSource`. That
  keeps M04's latency compensation (`now - eventTime`) honest: without it, the first commit after a
  10 s pause would read a latency of -10 s.
- **Proved in EditMode** (`MatchPauseTests`): a full scripted duel paused every 37 steps, and again
  after *every* state transition, lands the same slaps for the same damage as the unpaused duel,
  bit for bit (1/128 s steps keep the arithmetic exact); paused and late-delivered taps are dropped;
  pause/resume plus taps inside Charge and Timing allocate nothing (`Is.Not.AllocatingGCMemory`, R5).
- **Forfeit** closes any open meter (`PowerMeter.End`, `TimingMeter.ForceResolve`,
  `HoldRingMeter.ForceResolve`) and the AI source before raising `BoutEnd`, so no tap can land
  afterwards. It raises no `KO`. It is refused once the lethal slap has landed: the result stands.
- **Presentation** pauses itself from these: `HitstopController.SetPaused` (M08) stops scaled time,
  `HudView` stops ticking (M10), and `BoutController` (App) opens the pause menu.
- `MatchState.Paused` is kept for value stability but is never entered.

**Consumed by:** every module.
**Stability:** the state list is stable; transition timing will churn until the vertical slice.

## 4. Configuration

Asset: `Assets/_Project/Data/Balance/BoutConfig.asset`

| Field | Type | Default | Sane range | Notes |
|---|---|---|---|---|
| `attackMode` | `AttackMode` | **TapMarker** (asset and C# default since 2026-10-23) | TapMarker, HoldRing | Read at every `TurnStart`. HoldRing was the default 2026-10-20 → 10-23; reverted because a tap in the ring is an instant release (DEBUG S-090, [ADR-0014](../Decisions/ADR-0014-hold-ring-attack-mode.md) amendment) |
| `startingHp` | int | **100** | 60 – 200 | Both fighters start equal in v1 |
| `playerGoesFirst` | bool | **true** | — | Alternating; first slapper has a structural advantage |
| `maxRounds` | int | **20** | 5 – 20 | Safety valve; a match should end by damage well before this. *Not read by the FSM yet — `maxTurns` is the live valve* |
| `maxTurns` | int | **40** | 20 – 100 | Sudden-death valve: at this many slaps the higher HP wins (`ReachedMaxTurns`) |
| `finisherEnabled` | bool | **true** | — | |
| `introMs` | int ms | **500** | 0 – 4000 | BoutIntro. Target **2000** once a camera fly-in exists |
| `windupMs` | int ms | **500** | 300 – 800 | Resolve → impact. The most coupled number (see §10) |
| `hitstopMs` | int ms | **90** | 40 – 200 | Length of the FSM's Hitstop *state*; the visual freeze is `FeelConfig.hitstopMs` (M08) |
| `snapshotMs` | int ms | **50** | 35 – 100 | Snapshot window after a non-lethal hit |
| `finisherMs` | int ms | **2500** | 1500 – 4000 | Finisher before BoutEnd |
| `finisherSnapshotMs` | int ms | **100** | 50 – 200 | Added to the finisher |
| `turnTransitionMs` | int ms | **300** | 0 – 1500 | TurnEnd breathing room. Target **800** once the camera swaps sides (§10) |

The pacing fields (landed 2026-09-26) replaced `private const` durations inside `MatchDirector` with identical defaults (`DefaultPacing_MatchesThePreviousConstants`). They are authored in ms (R8) and read on every tick, so Play Mode edits apply on the next state entry. Negative values behave as 0 and cannot wedge the FSM (`NegativePacing_BehavesAsZero_NoHang`).

## 5. Events (MatchSignalType)

| Signal | When | Payload |
|---|---|---|
| `BoutIntroStart` | Bout begins, camera flies in | — |
| `BoutIntroEnd` | Intro done, first turn about to start | — |
| `TurnStart` | A new turn begins | `TurnInfo` |
| `ChargeStart` | Power meter activated | `TurnInfo` |
| `ChargeEnd` | Power meter completed | `PowerResult` |
| `TimingStart` | Timing meter activated | `TurnInfo` |
| `TimingEnd` | Timing meter committed or timed out | `TimingResult` |
| *(HoldRing order)* | In `AttackMode.HoldRing` the same four signals fire in gesture order: `TimingStart` when the ring opens, `ChargeStart` at the press (the wind-up), then `ChargeEnd` + `TimingEnd` together at the release (or the timeout). A turn with no press raises no `ChargeStart` | ([ADR-0014](../Decisions/ADR-0014-hold-ring-attack-mode.md)) |
| `DamageResolved` | `Resolve()` called, damage computed but not applied | `DamageResult` |
| `WindupStart` | Slap animation windup begins | `contactDsp` (predicted audio time) |
| `ImpactFrame` | The frame `Commit()` runs and health changes | `DamageResult` |
| `HitstopStart` | Freeze frames begin | `hitstopFrames` |
| `HitstopEnd` | Freeze frames done | — |
| `SnapshotCaptured` | Photo taken at contact + 35 ms | `tileIndex` |
| `FinisherStart` | Lethal slap triggers a finisher cutscene | `FinisherType` |
| `FinisherEnd` | Cutscene done | — |
| `TurnEnd` | Turn fully resolved, about to swap sides | `TurnInfo` |
| `KO` | A fighter's health reached 0 | `FighterSlot loser` |
| `BoutEnd` | Match over, transitioning to victory screen | `BoutResult` |

## 6. State machine

```
                                ┌──────────────────────────────────────────────────────┐
                                │                                                      │
BoutIntro ──► TurnStart ──► Charge ──► Timing ──► Resolve ──► Windup ──► Impact ──► Hitstop
                 ▲                                                                     │
                 │                                                         ┌───────────┤
                 │                                                         │           │
                 │                                                    [not lethal] [lethal]
                 │                                                         │           │
                 │                                                    Snapshot    Finisher
                 │                                                         │           │
                 │                                                         │      Snapshot
                 │                                                         │           │
                 └─────────────── TurnEnd ◄────────────────────────────────┘           │
                                                                                       │
                                                                                   KO ──► BoutEnd
```

| State | Entry condition | Exit condition | Duration |
|---|---|---|---|
| `BoutIntro` | `StartBout()` | `introDuration` elapsed | Fixed timer |
| `TurnStart` | Intro done or previous turn ended | Immediate → Charge (TapMarker) or HoldRing | One frame |
| `Charge` | `TurnStart` exit | `PowerMeter.IsComplete` | `phaseDuration` (2.5 s) |
| `Timing` | `Charge` exit | `TimingMeter` committed or timed out | Up to `timeout` (3.0 s) |
| `HoldRing` | `TurnStart` exit when the turn's `AttackMode` is HoldRing; replaces `Charge` + `Timing` ([ADR-0014](../Decisions/ADR-0014-hold-ring-attack-mode.md)) | `HoldRingMeter` resolved: release, full close or no press | Up to `pressTimeoutMs` + one close (≈ 4 s + 1.2 s) |
| `Resolve` | `Timing` exit | Immediate — `Resolve()` is synchronous and pure | One frame |
| `Windup` | `Resolve` exit | Windup animation reaches the arm-back pose | ~0.4 s |
| `Impact` | Windup contact frame | `Commit()` runs, health applied | One frame |
| `Hitstop` | `Impact` exit | Freeze frame count exhausted | 4–12 frames |
| `Snapshot` | `Hitstop` exit (non-lethal) or `Finisher` exit (lethal) | Capture complete | ~35 ms after contact |
| `Finisher` | `Hitstop` exit and `isLethal` | Finisher cutscene done | ~2–4 s |
| `TurnEnd` | `Snapshot` exit | `turnTransitionDelay` elapsed | 0.8 s |
| `KO` | `Snapshot` exit after finisher | Immediate → `BoutEnd` | One frame |
| `BoutEnd` | `KO` exit | Scene transition to victory screen | — |
| *(pause)* | `RequestPause()` in any live state | `RequestResume()` (or `Forfeit`) | Indefinite. **Not a state** (2026-10-20): `IsPaused` is orthogonal and the state keeps naming the phase; see §3 *Pause and forfeit*. `MatchState.Paused` is never entered |

**The predict-branch-commit pattern:** `Resolve` computes damage and returns `isLethal` and `isFinisher` *before* a single frame renders. The FSM uses this to branch into the finisher path *before* `Impact`. `Commit` then applies health at the impact frame. This solves the "finisher-before-impact" problem: the animation pipeline knows which path to take without a retroactive correction.

## 7. Dependencies

- **Hard:** `SlapMania.Core` (signals, `ITapSource`, fighter slots), `SlapMania.Gameplay` (PowerMeter, TimingMeter, DamageResolver, HealthTracker), `SlapMania.Data`.
- **Soft:** everything else subscribes to `Signal`.
- **Unity APIs:** none directly — `Tick(dt)` receives delta time from the MonoBehaviour driver.
- **Must initialize after:** M00 (config loaded).

## 8. Failure modes

| Failure | Detection | Behaviour | Player-visible? |
|---|---|---|---|
| Match never terminates | `maxRounds` safety valve | Force a draw, log Error | Yes — unusual but not a hang |
| Signal listener throws | Catch in dispatch loop | Log the exception, continue dispatch to remaining listeners | No (unless the listener was critical) |
| Damage resolves to 0 every turn | `minDamageFloor` in M05 guarantees ≥ 1 | Should not happen | No |
| `Tick` called with negative dt | Guard — clamp to 0 | Log Warn once | No |
| State re-entry | Assert — each state entered exactly once per traversal | Loud error in dev | No |

## 9. Debug hooks

- **Log channel:** `LogChannel.Match`
- **Overlay (Match tab):** current state, turn number, slap index, attacker/defender, elapsed time in current state, last signal emitted.
- **Cheats:** skip to impact, force finisher, force KO, set HP to 1, advance turn, restart bout.
- **Recorder events:** every `MatchSignal` is recorded with its timestamp and payload.
- **Editor tools:** `MatchFlowVisualizer` — a custom editor window showing the FSM as a graph with the current state highlighted during Play Mode.

## 10. Tuning notes

**The FSM is the source of truth for "what is happening right now."** No module should maintain its own shadow state about the match phase. If a module needs to know whether it is in the charge phase, it reads `CurrentState`, it does not set a bool when it hears `ChargeStart`.

**`turnTransitionDelay` is feel, not logic.** It exists so the camera has time to swing to the other side and the player has a beat to breathe. At 0.3 s the game feels frantic (maybe good for late rounds); at 1.5 s it drags. The default 0.8 s is a starting point — tune it with the camera work, not in isolation.

**The windup-to-impact timing is the most coupled number in the game.** The animation, the audio impact schedule (M11 `contactDsp`), the camera punch (M16), the hitstop (M08), and the snapshot delay (M09) all key off the impact frame. If you change the windup duration, you must update the `contactDsp` prediction in M11 or audio arrives early/late. The authoritative timeline lives in [TUNING-BIBLE.md §5](../TUNING-BIBLE.md).

**v1 is a single bout.** There is no bracket, no tournament tree, no round-robin. `BoutConfig` describes one fight. The meta-loop (M12) wraps it with progression, but the FSM itself does not know about a series. Adding a tournament means a new state machine *above* this one, not making this one more complex.

## 11. Open questions

- [ ] Should the intro be skippable on rematches, or is the fly-in always mandatory?
- [ ] Does the `turnTransitionDelay` need to scale with round number (faster in late rounds for urgency)?
- [ ] Should `Paused` freeze `AudioSettings.dspTime` via `AudioListener.pause`, or let music continue? Leaning toward full pause. *2026-10-20:* the pause sets `Time.timeScale = 0` (M08) but leaves audio alone; M11 decides. Note `WindupStart`'s `contactDsp` is now simulation time (equal to realtime until the first pause), so M11 must map it to DSP time rather than assume the two bases agree.
- [ ] Is a draw (max rounds, both alive) a real outcome, or should sudden-death double damage kick in at round 8+?

## 12. Changelog

| Date | Commit | Change |
|---|---|---|
| 2026-10-23 | (pre-git) | **Brace window ([ADR-0019](../Decisions/ADR-0019-brace-tap-defence.md)).** On a rival `Windup`, `MatchDirector` listens to the player's taps (cached handler, R5). It judges the first one as Windup ends, re-resolves `_lastDamage` with the reduction before `Impact`, and raises the new `MatchSignals.BraceResolved` before `ImpactFrame`. New `LastBrace` and `IsBraceWindowOpen`. `Forfeit` closes the window. No new `MatchState` |
| 2026-10-23 | (pre-git) | `BoutController` (App) records each slap's `ChargeEnd`/`TimingEnd` results (struct copies, R5), fills D4 Instant Replay's `ReplayShot` on every snapshot, and after a K.O. routes K.O. -> D4 -> E2/E3 (M09/M10). No FSM change |
| 2026-10-23 | (pre-git) | **Mash carry-over, fresh seeds, AI ladder, slap gallery.** `MatchDirector.EnterTiming` passes `PowerMeter.LastTapTime` to `TimingMeter.Begin`, and the meter's AIM cue and `EarliestCommitElapsed` to the AI (M04/M07, DEBUG S-093, [ADR-0017](../Decisions/ADR-0017-aim-cue-and-spam-gap.md)). No new `MatchState`: the cue lives inside `Timing`. `BoutController` (App): `BoutConfig.matchSeed` 0 = a fresh seed per bout, logged (S-094). The opponent is rolled from the AI ladder near `GameRoot.AiLadderLevel`, and the level moves at `BoutEnd` (S-095, [ADR-0018](../Decisions/ADR-0018-ai-ladder.md)). Every `SnapshotRequested`/`FinisherSnapshotRequested` is photographed by `SlapSnapshotter`, and the loser's gallery goes to `HudContext` at `BoutEnd` (M09, [ADR-0016](../Decisions/ADR-0016-slap-gallery-pre-atlas-capture.md)). Test: `MatchDirectorTests.MashingOnFromTheChargeIntoTheTimingPhase_DoesNotCommitIt` |
| 2026-09-19 | (pre-git) | Created (Draft) with 15-state FSM and signal table |
| 2026-09-19 | (pre-git) | **P3 code landed.** `MatchDirector` + `MatchSignals` in `SlapMania.Gameplay`, driven by an `IClock`. FSM implements the loop end-to-end: BoutIntro → TurnStart → Charge → Timing → Resolve → Windup → Impact → Hitstop → Snapshot → (Finisher → FinisherSnapshot)? → TurnEnd → KO/BoutEnd. Every hit emits `SnapshotRequested`; the lethal hit emits `FinisherStart` + `FinisherSnapshotRequested`. Predict-branch-commit holds: `DamageResolver.Resolve` runs in `Resolve` state, `Commit` runs in `Impact`. **Known simplifications:** state durations (windup/hitstop/snapshot/finisher/turn transition) currently live as `private const` inside `MatchDirector` rather than in a `SlapMotionConfig` SO — extract when M08 lands. `Paused` state not implemented. |
| 2026-09-26 | (pre-git) | **Fixed cross-assembly signal raising** (18 × CS1061 in `MatchDirector.cs`). Correction to the row above: `MatchSignals` actually lives in `SlapMania.Core` (with the signal payload types, and so `Audio`/`Meta` can subscribe without referencing `Gameplay` — see §7 and ARCHITECTURE §2), while `MatchDirector` is in `SlapMania.Gameplay`. Its 17 `Raise*` methods are `internal`, so the cross-assembly call from `MatchDirector` failed to compile. Granted friend access via `[assembly: InternalsVisibleTo("SlapMania.Gameplay")]` in `Core/Properties/AssemblyInfo.cs`; `Raise*` stays `internal` so only the simulation assembly can fire signals (ARCHITECTURE §5). `MatchSignals.cs` unchanged. |
| 2026-09-26 | (pre-git) | **Pacing moved into `BoutConfig`** (resolves the P3 "state durations live as `private const`" simplification). Seven int-ms fields — `introMs`, `windupMs`, `hitstopMs`, `snapshotMs`, `finisherMs`, `finisherSnapshotMs`, `turnTransitionMs` — with the old constants as defaults, so behaviour is unchanged. `MatchDirector` reads them through expression-bodied properties every tick (live-tunable from the Tuning Hub, [ADR-0011](../Decisions/ADR-0011-adopt-v2-design-prototype-first.md)). New `MatchPacingTests`: windup and turn-transition honour the asset to within one 1 ms tick; all-zero and all-negative pacing still terminate a duel; defaults equal the old constants. §4 table corrected to the code (`maxRounds` default 20, `maxTurns` added, doc-only `introDuration`/`turnTransitionDelay` rows replaced by the real fields with their target values noted). |
| 2026-09-26 | (pre-git) | **`SlapStats` landed** (Gameplay, pure, zero-alloc, `SlapStatsTests`): per-fighter bout tallies and a timing tendency for the V2 result screens (M10), attached to the director by `BoutController`. §3 updated. |
| 2026-10-20 | (pre-git) | **Pause and forfeit landed** (closes the P3 "`Paused` state not implemented" note). `MatchDirector` runs on a `Core.PausableClock` over the injected clock and gates the player's source through `Gameplay.PausableTapSource` (drops paused taps, re-stamps the rest to simulation time). New: `RequestPause`/`RequestResume` (now `bool`), `IsPaused`, `IsLive`, `Now`, `Forfeit(quitter)`, `IsOutcomeDecided`; `MatchResult.Forfeited` (optional ctor arg, existing calls unchanged). Pause is orthogonal to `CurrentState` (`MatchState.Paused` never entered). Tests: `PausableClockTests` (clock + gate: idempotence, zero/huge pauses, NaN/backwards source, late-delivered paused taps) and `MatchPauseTests` (exact remaining charge after a pause, marker frozen and latency measured on sim time, instant states hold, refusal rules, a paused duel identical to an unpaused one, forfeit closes the meter, zero alloc in Charge/Timing). Driven from the bout by `BoutController` (pause menu, M10). §3, §6, §11 updated. |
| 2026-10-23 | (pre-git) | **HoldRing attack mode ([ADR-0014](../Decisions/ADR-0014-hold-ring-attack-mode.md)).** `MatchState.HoldRing` (appended after `Paused`, values stable) replaces Charge → Timing when `BoutConfig.attackMode` is HoldRing (the asset default). The mode is read per turn into `CurrentAttackMode`. No new signals: they fire in gesture order. The opponent's turn plays the same ring (M07). `Forfeit` closes an open hold ring. The constructor gained an optional `HoldRingConfig`; `GameConfig.Validate` requires it in HoldRing. `BoutController` passes `GameConfig.holdRing`. `TestFactories.Bout()` stays TapMarker so the existing duel tests are unchanged. New FSM tests in `HoldRingMeterTests`: a HoldRing duel to BoutEnd with the signal order checked on every player turn, the TapMarker fallback without config, and a mode switch applied at the next `TurnStart`. §3, §5 and §6 updated. |
| 2026-10-23 | (pre-git) | `EnterTiming` passes `TimingPhaseConfig.displayLatencyBias` to `AiTapSource.BeginTiming`, as `EnterHoldRing` already did, so the TapMarker AI is judged where it aims (DEBUG S-079, M07). No FSM or signal change. Covered by `TimingMeterTests.Director_GivesTheAiTheBias_SoItIsJudgedWhereItAimed`. |
| 2026-10-23 | (pre-git) | **Default attack mode is TapMarker again (owner, DEBUG S-090).** `Data/Prototype/Bout.asset` `attackMode` 1 → 0 (saved through the Editor; `Resources/GameConfig.asset` references it) and the `BoutConfig.attackMode` C# default HoldRing → TapMarker, so a fresh prototype build agrees. Simulated through `MatchDirector` with the shipped `GameConfig`: all 117 turns played TapMarker, and a tapping player now wins 9/20 against `Ai_Round1` (was 0/20 in the ring; 25.9 dmg a slap, was 4.7). §4 gained the `attackMode` row. No code-path change: the hold ring is one Tuning Hub switch away. EditMode 231/231. |
