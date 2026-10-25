# Slap Mania - Architecture

Status: Draft — Last verified 2026-09-26 — Core/Data/Gameplay (P3), the first MonoBehaviour layer (P5-wip: Input/Presentation/UI/App) and the Editor assembly exist. The §2 assembly list is now the authoritative graph, confirmed by [ADR-0010](Decisions/ADR-0010-assembly-graph-refinement.md) (which supersedes ADR-0002's coarser list). Design direction: V2, adopted module by module per [ADR-0011](Decisions/ADR-0011-adopt-v2-design-prototype-first.md).

## 1. The five load-bearing decisions

Everything else follows from these.

**1. Gameplay is headless.** `SlapMania.Core` and `SlapMania.Gameplay` are pure C# with no MonoBehaviour, no `Time.*` read inside, no singletons. The simulation is driven by `Tick(double dt)` and consumes an `ITapSource`. Consequence: the entire duel runs in the EditMode Test Runner before a single prefab exists, and the AI is not a special case. See [ADR-0003](Decisions/ADR-0003-gameplay-math-is-pure-csharp.md).

**2. Predict, branch, commit.** Damage is *computed* in one state, *branched on* by the FSM, and *applied* at the impact frame. This is the only clean way to select the finisher animation before the hit renders.

**3. One code path for player and AI.** `AiTapSource` feeds synthetic taps into the same `PowerMeter` and `TimingMeter` the human drives. Retune the power curve and the AI retunes for free.

**4. Placeholder-first, permanently.** `IFighterView` and `ISlapTimingSource` mean two capsules are playable on day one and stay playable. Nothing in the simulation ever references `Animator`.

**5. Data or it doesn't ship.** Every tunable is a ScriptableObject field. The tuning milestone must require zero recompiles — if it needs one, the data-driven design failed and that is the bug.

## 2. Assembly graph

Edges point one way and never reverse. Enforced by asmdef references, not discipline.

```
  Logging
    -> Core
         -> Data
         -> Input        (+ Unity.InputSystem)
         -> Snapshot     (+ URP)                <- only URP consumer
         -> Data -> Gameplay
         -> Data -> Audio
    Gameplay + Snapshot + Audio -> Presentation (+ URP, Timeline)
    Gameplay + Data             -> UI           (+ uGUI, TMP)
    Core + Data                 -> Meta
    Core + Data + Gameplay      -> Diagnostics  [defineConstraints: SLAPMANIA_DEBUG]
    everything                  -> App          (composition root)
```

| Assembly | Depends on | Notes |
|---|---|---|
| `Logging` | nothing | So every other assembly can call `SlapLog` |
| `Core` | Logging | Pure math, interfaces, FSM primitives |
| `Data` | Core, Logging | ScriptableObject configs |
| `Gameplay` | Core, Data, Logging | The simulation. **No Input, UI, Presentation or URP.** |
| `Input` | Core, Logging, InputSystem | `ITapSource` implementations |
| `Snapshot` | Core, Logging, URP | **The only runtime assembly that touches URP rendering** (Editor also references URP; see its row) |
| `Audio` | Core, Data, Logging | Mixer, banks, directors, `BeatClock` |
| `Presentation` | Core, Data, Gameplay, Snapshot, Audio, URP, Timeline | Views, camera, FX |
| `UI` | Core, Data, Gameplay, InputSystem, TMP | Screens and HUD |
| `Meta` | Core, Data, Logging | Save, ladder, progression |
| `App` | all | Composition root only — wires everything, owns nothing |
| `Diagnostics` | Core, Data, Gameplay, Logging | Not compiled at all in release |
| `Editor` | all, + `Unity.RenderPipelines.Core.Runtime` / `Universal.Runtime` | `includePlatforms: [Editor]`. **Exists** (2026-09-26): the prototype, V2 HUD, fighter and arena builders plus the Tuning Hub Inspector (M13). The URP edges (owner-approved 2026-09-26, R9) let the arena builder author a Volume profile and camera data; Editor-only, so they never reach a player build |

Exists today: `Logging`, `Core`, `Data`, `Gameplay`, `Input`, `Presentation`, `UI` (now with the TextMeshPro edge), `App`, `Editor`, `Tests.EditMode`. Not yet created: `Snapshot`, `Audio`, `Meta`, `Diagnostics`, `Tests.PlayMode`.

Three payoffs, in order of how much they matter day to day:

1. **Iteration speed.** The project is script-only for weeks. Editing a UI script recompiles `UI` + `App`, not the whole game.
2. **`Gameplay` has zero references to UI, URP or `UnityEngine.UI`.** The slap FSM, damage math and AI are testable in EditMode with no scene, no camera and no pipeline. That is where the balance bugs live.
3. **`Snapshot` is the only URP consumer.** It can be replaced wholesale by `NullSnapshotCapture`, which is what makes the low-tier and CI paths trivially correct instead of a maze of preprocessor branches.

See [ADR-0002](Decisions/ADR-0002-assembly-graph.md).

## 3. Scenes

**Boot (persistent, never unloaded) plus one additive content scene.**

| Scene | Contents |
|---|---|
| `Boot` | `GameRoot`, service container, `ScreenManager` + root Canvas + EventSystem, `AudioService` + voice pool, `SaveService`, **`SnapshotService` and the atlas RenderTexture (allocated here, once, for the app's life)**, `TransitionOverlay`, `DeviceTierDetector` |
| `Menu` | Menu props, 3D backdrop, tournament map dressing |
| `Bout_Arena01` | Arena geometry, lighting rig, crowd, Global Volume, fighter spawn markers, `PhotoCameraRig`, arena displays, `BoutController` |

Rejected: a **single scene** (everything coupled, no arena swapping, unusable for more than one person in the editor) and **fully separate non-additive scenes** (you lose persistent services and reallocate the 11 MiB atlas every bout, a documented Android OOM path).

**Today (prototype, [ADR-0011](Decisions/ADR-0011-adopt-v2-design-prototype-first.md)):** `Boot.unity` (GameRoot only) + `Proto_Bout.unity` (capsules, ring, camera, lights, HUD canvas, EventSystem, `BoutController`, Tuning Hub), both **generated** by `Tools > Slap Mania > Prototype > Build Playable Prototype` and placed at Build Settings index 0 and 1. Differences from the table above, all temporary: the HUD canvas and EventSystem live in the bout scene (no `ScreenManager` yet), there is no Menu scene, and `Proto_Bout` stands in for `Bout_Arena01` until the arena is built by hand. `Tools > Slap Mania > Prototype > Play From Boot` (on by default) makes the editor's Play button start at Boot, exactly like a device build.

**Front end (2026-10-19, [ADR-0013](Decisions/ADR-0013-ui-toolkit-front-end-from-mockups.md)):** `FrontEnd.unity` is the menus' content scene. Boot loads it first, and `GameRoot` swaps it with the bout and back (`RequestMatch` / `RequestFrontEnd`). It holds the UI Toolkit `ScreenManager` (not a singleton; the table's "ScreenManager in Boot" is the long-term plan) with `GammaPresenter` and `FrontEndBridge`, and is generated by `Tools > Slap Mania > Front End > Build Front End Scene`. Build Settings order: Boot, FrontEnd, Proto_Bout.

Scene loading goes through `Awaitable.FromAsyncOperation(SceneManager.LoadSceneAsync(..., LoadSceneMode.Additive))` behind the `TransitionOverlay`.

## 4. Life of one slap

The highest-value page in the repo. Every hop names the owning class and the signal.

```
 1. MatchDirector           TurnBegin      binds attacker's ITapSource to the meters
 2. MatchDirector           Windup         view.Windup(); publishes contactDsp to Audio
                                           (>=180 ms lead - see M11 for why)
 3. PowerMeter              ChargeBegin    Begin(cfg, source); HUD shows the power bar
 4. InputSystemTapSource    Tap  (xN)      ~8-15/s, each with a device timestamp
      -> PowerMeter.RegisterTap()          impulse * diminishing * phaseShape
      -> Signals.PowerTapRegistered        HUD jolt, AudioService ratchet pitch
 5. PowerMeter              ChargeEnd      -> PowerResult { fill, overcharge, rawPower }
 6. MatchDirector           PowerLock      0.25 s, bar flashes and locks
 7. TimingMeter             TimingBegin    Begin(cfg, layout, round, seed)
                                           armDelay 0.15 s - taps ignored
 8. TimingBarView           LateUpdate     polls NormalizedPosition; never evented
 9. InputSystemTapSource    Tap            -> TimingMeter.TryCommit(tapEvent)
      -> latency compensation from tapEvent.eventTime, NOT frame time
      -> Signals.TimingCommitted           -> TimingResult { position, zone, multiplier }
10. DamageResolver.Resolve()                PURE. Mutates nothing.
      -> SlapResult { damage, healthBefore, healthAfter, isLethal, isFinisher, isCrit }
      -> Signals.SlapResolved
11. MatchDirector           branch         isFinisher ? FinisherImpact : Impact
      -> view.PlayFinisher(result) or view.PlaySlap(result)
      The correct animation is chosen BEFORE any frame of it renders.
12. ISlapTimingSource       Contact        animation event, or the scripted timer
      -> Signals.ImpactFrame(result)
      -> DamageResolver.Commit(result)     NOW health actually changes
      -> Signals.HealthChanged
      -> HitStop, ScreenShake, VfxService, AudioService impact stack
         (prototype today: HitstopController, CameraRig.Kick, ArenaDirector.ImpactFlash
          - key light + beams surge on unscaled time; the KO slap goes to ragdoll)
13. ISlapTimingSource       SnapshotFrame  contact + 35 ms, during hitstop
      -> SnapshotService.Capture()         photo camera -> scratch RT -> atlas tile
      -> white flash + shutter SFX + polaroid pop, all on unscaled time
14. MatchDirector           Reaction       1.0 s (2.5 s for a finisher)
15. MatchDirector           TurnEnd        -> TurnBegin | RoundIntro | MatchOver
```

Two details worth internalising:

- **Step 10 mutates nothing and step 12 does.** A slap's outcome is fully known one animation before it is applied. That is what makes finishers, the correct VO line and the right camera shot all selectable in advance.
- **Step 4 uses the input event's own timestamp**, and step 9 compensates with it. Frame time is not accurate enough: one frame at 30 fps is 33 ms and the red zone is 60 ms wide. See [ADR-0004](Decisions/ADR-0004-ms-based-timing.md).
- **In the HoldRing attack mode** ([ADR-0014](Decisions/ADR-0014-hold-ring-attack-mode.md)), steps 3-9 collapse into one state, `MatchState.HoldRing`, and one gesture through `IHoldSource`:
  - the ring opens (`TimingStart`);
  - the press starts it at its own event time and is the wind-up (`ChargeStart`);
  - the release is latency-compensated exactly like step 9 and yields both results (`ChargeEnd`, `TimingEnd`).

  Steps 10-15 are unchanged.

## 5. Signals

`MatchSignals` is a plain class instantiated per match with explicit `event` fields. **Deliberately not a static generic bus**: explicit events are type-safe, Find Usages works, there is no boxing, no reflection, no cross-scene leak and no teardown bug. A generic bus is a net loss at this scale.

```
MatchStarted(MatchStartInfo)     RoundStarted(int)
TurnStarted(TurnInfo)            PowerPhaseStarted(PowerPhaseInfo)
PowerTapRegistered(TapFeedback)  PowerPhaseEnded(PowerResult)
TimingPhaseStarted(...)          TimingCommitted(TimingResult)
SlapResolved(SlapResult)         // predicted, not applied
ImpactFrame(SlapResult)          // damage commit + snapshot hook
HealthChanged(HealthChangeInfo)  FinisherStarted(SlapResult)
FighterDefeated(int)             TurnEnded(TurnInfo)
MatchEnded(MatchResult)
```

**Events for moments, polling for per-frame values.** `TimingMeter.NormalizedPosition` and `PowerMeter.Fill` are polled by the view in `LateUpdate`, never evented.

## 6. Cross-system clocks

Three clocks exist and they must not be confused:

| Clock | Source | Used by | Never used for |
|---|---|---|---|
| Simulation | `IClock` (`UnityClock` / `ManualClock`) | Gameplay meters, FSM | Presentation tweens |
| Unscaled | `Time.unscaledDeltaTime` | Every presentation tween, overlay, hitstop itself | Simulation |
| DSP | `AudioSettings.dspTime` | `BeatClock`, all audio scheduling | Anything visual, directly |

Lighting, VFX and camera consume beats through `IBeatSource`, which delivers **latency-compensated** events on the main thread. A consumer must never re-derive beats from `Time.time` — that is how light shows drift out of sync.

## 7. Threading

Main thread only, with exactly two exceptions:

- `AsyncGPUReadback` completion callbacks (snapshot archive encoding).
- The `CrashLogger` file writer, which hooks `Application.logMessageReceivedThreaded` and must be lock-guarded.

Nothing else. In particular, nothing in `IBeatSource` is ever raised from `OnAudioFilterRead`.

## 8. Placeholder-first mechanisms

Five, all present from the first milestone:

1. `IFighterView` with `PlaceholderFighterView` (capsules, transform punch, colour flash).
2. Every animation-driven wait has a timer fallback, plus an FSM watchdog that logs the clip name when an expected animation event never arrives.
3. `FighterViewFactory` picks `AnimatorFighterView` or `PlaceholderFighterView` by inspecting the prefab, so a finished fighter can duel a capsule.
4. Every presentation service sits behind an interface with a null-object implementation (`NullVfxService`, `NullAudioService`, `NullSnapshotCapture`). A missing bank entry logs once and no-ops; it never throws inside a state.
5. No FSM state awaits an unbounded promise. Every state has a `maxDuration`. Covered by `MatchDirector_WithNullPresentation_CompletesFullDuel`.

## 9. Open questions

- [ ] Does `Presentation` depending on `Audio` (to publish `contactDsp`) invert cleanly, or should contact prediction live in `Gameplay` and be consumed by both? Leaning toward the latter — `Gameplay` knows the windup duration.
- [ ] Where does arena lighting sit? Probably inside `Presentation`, but if it grows a Render Graph pass it may need the `Snapshot`-style URP isolation. Blocked on the M14 design pass.
- [ ] Does `Meta` need to exist for v1 given the single-bout scope, or does a thin `SaveService` in `App` suffice until the ladder lands?
