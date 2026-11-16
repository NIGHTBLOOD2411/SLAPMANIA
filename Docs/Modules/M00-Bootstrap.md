---
module: M00-Bootstrap
status: InProgress
owner: shobhit
last-verified: 2026-10-25
verified-against: (pre-git)
asmdef: SlapMania.App
---

# M00 — Bootstrap

> **One-liner:** Boots the game from a single persistent scene, loads configs, and hands control to the menu or match flow.

## 1. Purpose

The game needs exactly one entry point that works whether Play Mode starts from the Boot scene, the Menu scene, or the Arena scene. Bootstrap owns the load order, the persistent root objects, and the handoff to whichever flow comes next. It also gates on TMP Essentials being imported, because every UI element errors without them and the failure message is "pink quad" rather than anything diagnosable.

## 2. Responsibilities and non-goals

**Owns**
- The `Boot` scene and its `GameRoot` MonoBehaviour (the only singleton in the project).
- Loading all ScriptableObject config assets from `Assets/_Project/Data/`.
- Setting runtime player settings (`targetFrameRate`, `vSyncCount`, `orientation`, `sleepTimeout`, `pollingFrequency`, `EnhancedTouchSupport`).
- `DeviceTierDetector` — picks High / Mid / Low from `SystemInfo` at first boot, cached in `PlayerPrefs`.
- Scene composition: `Boot` is persistent, `Menu` and `Bout_Arena01` are loaded/unloaded additively.

**Explicitly does NOT do**
- Own the match loop → `M01-MatchFlow`.
- Own any UI → `M10-UIScreenFlow`.
- Own audio mixer init → `M11-Audio` self-inits from `GameRoot.Awake` after config is loaded.

## 3. Public API surface

| Member | Signature | Notes |
|---|---|---|
| `GameRoot.Instance` | `static GameRoot { get; }` | The one singleton. Survives scene loads via `DontDestroyOnLoad`. |
| `GameRoot.Config` | `GameConfig { get; }` | Aggregate SO holding refs to all per-module configs |
| `GameRoot.AiLadderLevel` | `int { get; set; }` | **2026-10-23.** The player's AI ladder level (M07 §4b, [ADR-0018](../Decisions/ADR-0018-ai-ladder.md)), read and moved by `BoutController`. Session memory only (resets on app start) until M12 saves it. Not a new singleton: a field on the existing one |
| `GameRoot.DeviceTier` | `DeviceTier { get; }` | `High`, `Mid`, `Low` — set once at boot |
| `GameRoot.LoadScene` | `Awaitable LoadScene(string name, LoadSceneMode mode)` | **Landed.** Skips an already-loaded scene; errors if not in Build Settings; cancelled by `destroyCancellationToken` |
| `GameRoot.UnloadScene` | `Awaitable UnloadScene(string name)` | **Landed.** No-op if not loaded |
| `BoutController.Pause` / `Resume` / `Forfeit` | `bool ()` | **Landed 2026-10-20** (`BoutController.Overlay.cs`, the bout's composition root). Pause = `MatchDirector.RequestPause` + `HitstopController.SetPaused` + the overlay's PauseMenu (M10); Resume closes it at once; Forfeit -> `MatchDirector.Forfeit(Player)`. Triggered by the HUD pause button, Escape / device back, Gamepad Start and `OnApplicationPause(true)` |
| `GameRoot.RequestBoutReload` | `void RequestBoutReload()` | **Landed.** Unload + re-add the bout scene (Rematch); ignored while a load is in flight. The bout scene is made the active scene after every load so its lighting applies |

**Consumed by:** every module that reads config; M01, M10 for scene transitions.
**Stability:** stable after the first week.

## 4. Configuration

Asset: `Assets/_Project/Resources/GameConfig.asset` (it must sit under a `Resources/` folder for `Resources.Load`); the per-module assets it references live in `Assets/_Project/Data/Prototype/`. Both are created by the prototype builder (M13) when missing.

`GameConfig` is a ScriptableObject that holds references to every per-module config asset. It is the single load point — `GameRoot.Awake()` does `Resources.Load<GameConfig>("GameConfig")` once and everything else reads from its fields. No module ever calls `Resources.Load` on its own.

| Field | Type | Default | Notes |
|---|---|---|---|
| `bout` | `BoutConfig` | — | M01 — HP, turn order, pacing (ms) |
| `powerPhase` | `PowerPhaseConfig` | — | M03 |
| `timingPhase` | `TimingPhaseConfig` | — | M04 |
| `timingZoneLayout` | `TimingZoneLayout` | — | M04 |
| `damage` | `DamageConfig` | — | M05 |
| `aiProfiles` | `AiProfile[]` | — | M07, indexed by round |
| `feel` | `FeelConfig` | — | M08/M10 — hitstop, recoil, callout timings (ms). Landed 2026-09-26 |
| `healthConfig` | `HealthConfig` | — | M06 — *planned* |
| `snapshotConfig` | `SnapshotConfig` | — | M09 — *planned* |
| `audioConfig` | `AudioConfig` | — | M11 — *planned* |
| `cameraConfig` | `CameraConfig` | — | M16 — *planned* |

**Invariants**
- No field is null at load time — asserted in `OnValidate` and `Awake`.

### Champions ([ADR-0022](../Decisions/ADR-0022-champion-roster-and-stats.md))

- `GameConfig.roster` - optional `ChampionRoster`. Null/empty: the bout plays the scene's own fighters.
- `GameRoot.SelectedChampionId` - the player's Roster pick (`ChampionDefinition.id`); empty = the roster's first. Session memory, not saved yet (M12).

## 5. Events

**Emits**

| Event | Payload | When | Order guarantee |
|---|---|---|---|
| `BootComplete` | `DeviceTier` | After config loaded, tier detected, persistent objects created | Before any scene transition |

**Consumes**

None — Bootstrap is the root; it only emits.

## 6. State machine

```
Entry --> LoadingConfig --> DetectingTier --> CreatingPersistentObjects --> LoadingMenu --> Ready
```

All transitions are sequential `Awaitable` calls in `GameRoot.Awake`. There is no loop, no retry, no user-facing loading screen in v1 (boot is < 1 s on mid-tier).

## 7. Dependencies

- **Hard:** `SlapMania.Core`, `SlapMania.Data`.
- **Soft:** none.
- **Unity APIs:** `SceneManager`, `Resources.Load`, `SystemInfo`, `PlayerPrefs`, `Application`, `Screen`, `QualitySettings`, `InputSystem.settings`, `EnhancedTouchSupport`.
- **Must initialize after:** nothing — it is first.

## 8. Failure modes

| Failure | Detection | Behaviour | Player-visible? |
|---|---|---|---|
| `GameConfig` asset missing | `Resources.Load` returns null | `Debug.LogError` + hard stop in dev; release should never ship without it | Black screen |
| A config sub-asset null | `SlapAssert` in `Awake` | Log Error, use hardcoded fallback | No (degraded tuning) |
| TMP Essentials not imported | Pink quads on any TextMeshPro component | Editor console error (BUILD-AND-RUN §2 step 5) | Pink text everywhere |
| Scene already loaded | `SceneManager` guard | Skip the load, log Info | No |
| `SystemInfo` returns unexpected values | Tier defaults to Mid | Log Warn | No |

## 9. Debug hooks

- **Log channel:** `LogChannel.Boot`
- **Overlay fields:** device tier, detected GPU, screen resolution, target frame rate, `dspTime` at boot.
- **Cheats:** force tier (dropdown), reload config (button), restart boot sequence.
- **Recorder events:** `BootStart`, `BootComplete`, `SceneLoaded`, `SceneUnloaded`.
- **Editor tools:** none specific to Bootstrap.

## 10. Tuning notes

**`GameRoot` is the one allowed singleton.** Every other system receives its dependencies through constructor injection or config references. If a second singleton appears, it must be justified with an ADR. The reason is testability: `PowerMeter` is a plain class you can `new` in an EditMode test; `GameRoot` is the MonoBehaviour tax you pay once so everything else stays clean.

**`DeviceTierDetector` runs once and caches.** Re-detection on every boot wastes 200 ms of `SystemInfo` queries and can flip-flop on devices near the boundary. The tier is stored in `PlayerPrefs` with a version key; a new build resets it.

**Do not add a splash screen or loading bar in v1.** Boot is a single `Resources.Load` and two additive scene loads. If it ever takes more than 2 seconds on mid-tier, the problem is asset bloat, not missing UX.

## 11. Open questions

- [ ] Should `GameConfig` be a `Resources.Load` or a direct scene reference on `GameRoot`? Resources.Load is simpler but adds a `Resources/` folder. Scene reference avoids that but couples the asset to the scene file.
- [ ] Is `PlayerPrefs` sufficient for tier caching, or should it be a JSON file alongside the save data?

## 12. Changelog

| Date | Commit | Change |
|---|---|---|
| 2026-10-25 | (pre-git) | **Champions ([ADR-0022](../Decisions/ADR-0022-champion-roster-and-stats.md)).** `GameConfig.roster` (optional `ChampionRoster`; `Validate` fails with `roster: ...` on a null entry, missing id/prefab or duplicate id). `GameRoot.SelectedChampionId` (session only until M12). |
| 2026-10-23 | (pre-git) | **`GameConfig.hudAttention`** (optional, M10 [ADR-0021](../Decisions/ADR-0021-hud-attention-budget.md)): the in-bout HUD attention budget. `GameConfig.Validate` fails with `hudAttention: ...` when a wired budget breaks its own rules, and stays valid with none (the HUD uses defaults) |
| 2026-10-23 | (pre-git) | `GameRoot.AiLadderLevel` (session-only ladder level, M07 §4b / [ADR-0018](../Decisions/ADR-0018-ai-ladder.md)). §3 updated |
| 2026-09-19 | (pre-git) | Created (Draft) |
| 2026-09-26 | (pre-git) | **P5 partial.** `GameRoot` landed in `SlapMania.App` (asmdef corrected from `SlapMania.Runtime` per [ADR-0010](../Decisions/ADR-0010-assembly-graph-refinement.md)). It is the one singleton: loads `GameConfig` (new aggregate SO in `SlapMania.Data`) via `Resources.Load`, applies `targetFrameRate`/`vSyncCount`/portrait/`sleepTimeout`, sets `InputSystem.pollingFrequency = 120` and enables `EnhancedTouchSupport`, then additively loads `Bout_Arena01`. **Known simplifications:** no `DeviceTierDetector`, no `BootComplete` signal, no `TransitionOverlay`, and scene load is synchronous `SceneManager.LoadScene` (not the `Awaitable` wrapper). `BoutController` also resolves config directly when no `GameRoot` is present, so the bout scene runs standalone in the editor. **Note:** §4 lists `Resources.Load<GameConfig>("GameConfig")` — matched. §2's `pollingFrequency` is set via `InputSystem.pollingFrequency`, not `InputSystem.settings` (the latter has no such field). Scene `.unity` and the `GameConfig.asset` values are **unverified from code** (R11) — pending the Unity-side scaffold. |
| 2026-10-19 | (pre-git) | **Boot opens the front end (M10, [ADR-0013](../Decisions/ADR-0013-ui-toolkit-front-end-from-mockups.md)).** `GameRoot` gained `_frontEndSceneName`. When it is set, Start loads `FrontEnd` instead of the bout; an empty name keeps the old straight-to-bout boot. `RequestMatch()` swaps FrontEnd -> bout, `RequestFrontEnd(ScreenId)` swaps back and leaves `PendingFrontEndScreen` for the menus to open on, and `RequestBoutReload()` is now the same swap with one scene. All swaps share one `async void` entry point (`RunSwap`). New `FrontEndBridge` (App, in FrontEnd.unity) hands `GameConfig.uiMotion` and the start screen to `ScreenManager` before it opens, turns `MatchRequested` into `RequestMatch`, and maps Escape / Android back / gamepad East to `ScreenManager.Back` through an `InputAction` (events, M02). `BoutController` routes the results' MAIN MENU to `RequestFrontEnd(MainMenu)`. `GameConfig` gained optional `uiMotion` (not required by `Validate`: a bout runs without menus). Verified in Play: Boot -> FrontEnd -> bout -> FrontEnd. |
| 2026-09-26 | (pre-git) | **Boot + additive, for real ([ADR-0011](../Decisions/ADR-0011-adopt-v2-design-prototype-first.md)).** `GameRoot` now implements the §3 `Awaitable LoadScene/UnloadScene` wrappers (via `Awaitable.FromAsyncOperation` with `destroyCancellationToken`) instead of synchronous `SceneManager.LoadScene`, makes the bout scene the **active** scene after load (so its RenderSettings/lighting apply), and adds `RequestBoutReload()` for Rematch. The one `async void` is the private fire-and-forget entry point, which owns the exception handling. `Boot.unity` (GameRoot only, `_boutSceneName = "Proto_Bout"`) and `Resources/GameConfig.asset` are now **generated** by the M13 prototype builder, which also puts Boot at Build Settings index 0. `GameConfig` gained `feel` (`FeelConfig`) and `Validate` requires it. §3 and §4 updated; §4's asset path corrected from `Data/GameConfig.asset` (which `Resources.Load` cannot find) to `Resources/GameConfig.asset`. Still omitted: `DeviceTierDetector`, `BootComplete`, `TransitionOverlay`. |
| 2026-10-20 | (pre-git) | **Bout pause and overlay wiring.** `BoutController` is now `partial`; `BoutController.Overlay.cs` holds the pause (`Pause`/`Resume`/`Forfeit`, the `BoutBack` action on Escape + Gamepad Start, `OnApplicationPause`), routes the bout overlay's links (`ScreenManager.MatchRequested` / `LeaveRequested`, M10), sets the player tap source's `Blocker` to `V2Hud.BlocksTap` (M02), and shows E4 RankUp before the front end after a win. The new serialized `_screens` is wired by the prototype builder. No new singleton or scene. §3 updated. |
| 2026-10-20 | (pre-git) | **The V1 page ([ADR-0015](../Decisions/ADR-0015-v1-page-behind-a-tuning-switch.md), M10).** `GameConfig` gained `frontEndPage` (`FrontEndPage.V2` default, or `V1`; new enum in `SlapMania.Data`, serialized - never renumber). `FrontEndBridge.Awake` maps the start screen through `ScreenIds.ForPage`: on V1 a fresh boot opens `V1Main` instead of Title, and a bout's MAIN MENU (`PendingFrontEndScreen = MainMenu`) returns to `V1Home`. Not required by `Validate`. The `GameConfig.asset` value is **unverified from code** (R11); it defaults to V2. |
| 2026-10-23 | (pre-git) | **Hold-ring config ([ADR-0014](../Decisions/ADR-0014-hold-ring-attack-mode.md)).** `GameConfig` gained `holdRing` (`HoldRingConfig`, M04 §5b). `Validate` requires it only while `bout.attackMode` is HoldRing, so a TapMarker config validates without it (`GameConfigTests.Validate_RequiresHoldRingConfig_OnlyInHoldRingMode`). `BoutConfig.attackMode` defaults to HoldRing, so a `GameConfig` with no `holdRing` now fails `Validate`: rebuild the prototype, which creates and wires `Data/Prototype/HoldRing.asset`. The shipped `Resources/GameConfig.asset` was wired over MCP on 2026-10-20 and read back valid. |
| 2026-10-23 | (pre-git) | **`BoutController` drives M16 camera cues.** `PlayCameraCue` asks `CameraRig.Director` to play the bound `CameraSequence` on BoutIntro, TurnStart, Impact / LethalImpact, Finisher and BoutEnd (a no-op when nothing is bound). On TurnStart it binds the targets `attacker`, `attackerHead`, `defender` and `defenderHead` for keys' look-at/follow. `CameraRig.Kick` now becomes director trauma. ([0020](../Decisions/ADR-0020-camera-keyframe-tracks-and-runtime-recorder.md)) |
| 2026-10-24 | (pre-git) | **`BoutController` cues every game state for the camera (M16).** It now subscribes to `TimingStart` and `KO`, and calls `PlayCameraCue` on ChargeStart, ChargeEnd, TimingStart, TimingEnd, Windup, KO and TurnEnd, in addition to the earlier cues. A cue does nothing unless its `StateShot` or a bound sequence says otherwise. Per event, not per frame: the Shot sequence is cached, and the only allocation is a debug-stripped log line (R5). |
| 2026-10-25 | (pre-git) | **Camera sides (M16).** `BindSideTargets()` binds `player`, `playerHead`, `opponent` and `opponentHead` on BoutIntro and TurnStart. TurnStart also sets `CameraDirector.Attacker = t.Attacker` before its cue. The impact kick passes `d.IsLethal` explicitly (S-099). |
| 2026-10-25 | (pre-git) | **Recovered from the HDRP/Morgan import (DEBUG S-100).** The Build Settings scene list (Boot, FrontEnd, Proto_Bout), the layers and tags, and the player, quality and graphics settings had been overwritten. They are restored, and Boot / Proto_Bout are regenerated by the builder. Boot -> FrontEnd -> Proto_Bout loads by name again. No code change. |
