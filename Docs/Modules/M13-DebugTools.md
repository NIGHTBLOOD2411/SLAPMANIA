---
module: M13-DebugTools
status: InProgress
owner: shobhit
last-verified: 2026-10-24
verified-against: (pre-git)
asmdef: SlapMania.Diagnostics (runtime, SLAPMANIA_DEBUG only) + SlapMania.Editor (editor tools)
---

# M13 — DebugTools

> **One-liner:** Logging, overlay, match recorder, cheats, and editor tools — all compiled out of Release builds via `defineConstraints: ["SLAPMANIA_DEBUG"]`.

## 1. Purpose

Every other module has a "Debug hooks" section. This module is where those hooks live. It provides the infrastructure — the log channels, the overlay renderer, the match recorder, the cheat panel — that every module plugs into. The critical property is that **none of this ships**: the `SlapMania.Diagnostics` asmdef has `defineConstraints: ["SLAPMANIA_DEBUG"]`, so without the define the entire assembly is not compiled. Not stripped, not disabled — *not compiled*. Zero bytes in the APK.

## 2. Responsibilities and non-goals

**Owns**
- `SlapLog` — channel-based logging with `[Conditional("SLAPMANIA_DEBUG")]` methods.
- `DebugOverlay` — a runtime IMGUI overlay with tabbed panels, one per module.
- `MatchRecorder` — records every `MatchSignal`, `TapEvent`, and diagnostic event to a JSON file.
- `ReplayTapSource : ITapSource` — plays back recorded taps for deterministic replay.
- `CheatPanel` — runtime buttons for every cheat declared in module docs.
- `FrameHitchDetector` — logs and records frames exceeding a configurable threshold.
- `DocStatusWindow` — an editor window showing each module doc's `last-verified` freshness.
- **Prototype tooling** (`SlapMania.Editor`, Editor-only, landed 2026-09-26, [ADR-0011](../Decisions/ADR-0011-adopt-v2-design-prototype-first.md)):
  - `PrototypeSceneBuilder` — `Tools > Slap Mania > Prototype > Build Playable Prototype`. Generates the config assets, `Boot.unity`, `Proto_Bout.unity` and the Build Settings entries. Proto_Bout contains the humanoid fighters across the podium, the camera rig, the generated arena, the V2 HUD, the EventSystem and the Tuning Hub. Gates on TMP Essentials. `PrototypeSceneBuilder.BuildNonInteractive()` is the same build with no modal dialogs (they would hang an unattended Editor) — for the MCP bridge, CI and `-executeMethod`; it throws on Play Mode, missing TMP Essentials or unsaved open scenes rather than guessing. It also throws, before any scene is touched, when the humanoid fighter cannot be built (S-085). Only the interactive menu still falls back to capsule fighters, and it says so in its "Prototype built" dialog and a Console warning.
  - `PlayFromBoot` — a checkable menu item (on by default) that sets `EditorSceneManager.playModeStartScene` to Boot.
  - **`Hud.V2HudBuilder`** (+ `HudStyle`, `UiKit`) builds the whole V2 HUD canvas (M10) from the reference frame's coordinates. It replaced `PrototypeHudBuilder`. Since 2026-10-20 the scorebug and the C6 letterbox carry pause buttons (`HudPauseButton`).
  - **`FrontEnd.FrontEndSceneBuilder.BuildScreenHost`** builds a UI Toolkit screen stack (UIDocument + `GammaPresenter` + `ScreenManager` + its output canvas) in the open scene. `FrontEnd.unity` and the bout's overlay (`BoutOverlayScreens`, sorting 20 over the HUD) both come from it.
    - Fonts are TMP SDF assets made from the bundled Barlow TTFs.
    - `UiKit` has one helper per reference primitive: panel, gradient, glass, arc, rays, glow, chroma text, button, bar, chip.
  - **`Characters.FighterImportSetup` / `FighterBuilder` / `RagdollBuilder`** build the humanoid fighter (M08).
    - **FighterImportSetup:** Humanoid avatar with the Hips remap (S-072), and clip import settings (S-071).
      - The base model and its clips live in different folders. `FindBaseModel(AlucardStem)` finds the base `Alucard Sword Animation.fbx` by exact file name anywhere under `Assets/_Project` (today `Characters/FBX/Alucard/`, with its materials and textures). It throws when there is none or more than one.
      - The `@clip` files are read from `AlucardClipFolder` (`Animations/`, `AlucardClipPath(clip)`). `SetUpHumanoidSet(basePath, clipFolder)` takes both folders and reports when it finds no clips.
    - **FighterBuilder:** the Animator controller with measured contact frames, and the prefab with the sword hidden.
    - **RagdollBuilder:** the 11-body ragdoll.
    - `FighterBuilder` re-runs the import setup whenever it finds the wrong settings.
  - **`Arena.ArenaBuilder`** (+ `ArenaArt`, `ArenaPaint`, `ArenaMeshes`) generates the bout arena into the open scene ([ADR-0012](../Decisions/ADR-0012-arena-mobile-fakes.md); M14, M15, M16, M17).
    - It builds meshes, textures, materials, lights, the Volume profile and `ArenaDirector` wiring.
    - It bakes the reflection probe last (only static objects render into it).
    - Textures are painted on a CPU canvas: SDF shapes, plus glyphs rasterised from the Barlow TTFs by Unity's dynamic font at the exact pixel size. They are overwritten in place so GUIDs survive.
    - **Materials, `ArenaLook.asset` and `ArenaPost.asset` are created once and then yours to tune.**
    - Menus: `Tools > Slap Mania > Prototype > Regenerate Arena Art` (textures only) and `Reset Arena Look` (confirm dialog; deletes the look assets so the next build recreates them). `ArenaBuilder.ResetLookAssets()` is the dialog-free form.
  - `TuningHub` (component, `SlapMania.App`) + `TuningHubEditor` — one Inspector for every config asset (inline), the scene's feel components, and a live FSM readout in Play Mode. The builder lists `Fighter_Alucard_Rig`, `ArenaLook` and `ArenaPost` as extra assets. Scene components are the fighters, hitstop, camera rig, camera, `ArenaDirector` and `DamageCam`; their edits revert after Play. Its GameObject is tagged `EditorOnly`, so it never reaches a build. A FRONT END section picks the menus' canvas page (`GameConfig.frontEndPage`: V2 or V1, [ADR-0015](../Decisions/ADR-0015-v1-page-behind-a-tuning-switch.md)).

**Explicitly does NOT do**
- Own gameplay logic → cheats call into gameplay modules, they do not bypass them.
- Own the Unity Profiler → that is a Unity tool, not ours.
- Render in Release → it cannot, it is not compiled.

## 3. Public API surface

| Member | Signature | Notes |
|---|---|---|
| `SlapLog.Info` | `[Conditional] static void Info(LogChannel ch, string msg)` | Call sites stripped in Release |
| `SlapLog.Warn` | `[Conditional] static void Warn(LogChannel ch, string msg)` | |
| `SlapLog.Error` | `static void Error(LogChannel ch, string msg)` | **Not** conditional — errors survive Release (CONVENTIONS §Logging) |
| `SlapLog.Verbose` | `[Conditional("SLAPMANIA_VERBOSE")] static void Verbose(LogChannel ch, string msg)` | Double-gated |
| `MatchRecorder.Record` | `[Conditional] static void Record(RecorderEvent evt)` | |
| `MatchRecorder.SaveToFile` | `void SaveToFile(string path)` | JSON, one object per line |
| `DebugOverlay.Register` | `static void Register(string tab, Action drawCallback)` | Modules register their panels |

`LogChannel` enum: `Boot`, `Match`, `Input`, `Power`, `Timing`, `Damage`, `Health`, `AI`, `Presentation`, `Snapshot`, `UI`, `Audio`, `Save`.

**Consumed by:** every module (logging), M01 (recorder), M07 (replay source).
**Stability:** stable infrastructure; individual panel contents churn with their modules.

## 4. Configuration

| Field | Type | Default | Notes |
|---|---|---|---|
| `overlayEnabled` | bool | **true** | In dev builds |
| `overlayScale` | float | **1.0** | For high-DPI devices |
| `hitchThreshold` | float ms | **50** | Frames above this are logged and recorded |
| `recorderEnabled` | bool | **true** | In dev builds |
| `recorderPath` | string | `persistentDataPath/SlapMania/Recordings/` | |

## 5. Events

**Emits**

| Event | Payload | When |
|---|---|---|
| `FrameHitch` | `float ms, string context` | A frame exceeds `hitchThreshold` |

**Consumes**

Everything — the recorder subscribes to `MatchSignal` and every module's diagnostic events.

## 6. State machine

None — always active when the define is present.

## 7. Dependencies

- **Hard:** `SlapMania.Core` (log channels, recorder events).
- **Soft:** every module (for their debug panels).
- **Unity APIs:** `IMGUI` (`OnGUI`), `JsonUtility`, `File`, `Debug.Log`.
- **Must initialize after:** M00.
- **defineConstraints:** `SLAPMANIA_DEBUG` — the assembly is excluded from compilation without this define.

## 8. Failure modes

| Failure | Detection | Behaviour | Player-visible? |
|---|---|---|---|
| Module does not register a panel | Missing tab in overlay | No crash — the tab simply does not appear | Dev only |
| Recording file write fails | IOException | Log Error (via Unity's `Debug.LogError`, not `SlapLog` — avoid circular dependency) | Dev only |
| Overlay IMGUI causes GC alloc | Profiler | Minimize string formatting; use `StringBuilder` pool | Dev only; not in Release |
| `[Conditional]` stripping fails (define leak) | `BuildVerifyPreprocessor` checks for `SLAPMANIA_DEBUG` in Release | Build fails | Dev only |
| The prototype builder cannot build the humanoid fighter (S-085: base FBX moved) | `FighterBuilder.BuildAlucard` throws | Interactive: capsule fighters, a Console warning and a note in the "Prototype built" dialog. `BuildNonInteractive`: throws before any scene is touched, so the last good `Proto_Bout` stays | Dev only (capsules in the prototype) |

## 9. Debug hooks

This module *is* the debug hooks. Meta-debugging: the overlay has a "Debug" tab showing `SlapLog` buffer contents, recorder event count, frame hitch history, and memory used by debug systems.

## 10. Tuning notes

**`[Conditional("SLAPMANIA_DEBUG")]` strips call sites including argument expressions.** This is stronger than `#if`: a call like `SlapLog.Info(LogChannel.Power, $"Fill: {meter.Fill}")` is not just skipped — the string interpolation is never evaluated, so there is zero runtime cost. This is why `SlapLog` uses `[Conditional]` instead of `#if SLAPMANIA_DEBUG` around the method body.

**The overlay uses IMGUI, not uGUI.** IMGUI draws on top of everything, does not require a Canvas, does not trigger canvas rebuilds, and does not interfere with the game's UI layout. It is ugly but it is free. A pretty debug overlay in uGUI would cost canvas rebuilds every frame and compete with the HUD for draw calls.

**The match recorder writes one JSON object per line (JSONL format).** This is greppable, appendable, and does not require reading the entire file to parse one event. The recording schema is documented in [DEBUG.md §9](../DEBUG.md).

**Tune in assets, note-and-copy for scene components.** The Tuning Hub draws config assets and scene components in separate, labelled sections because they behave differently: an asset edit made in Play Mode persists, a component edit reverts when Play Mode ends. Anything that is a rule or a feel knob therefore belongs in an asset (ADR-0011 §5); the scene section is for camera/light framing that has no asset yet.

**The prototype builder is safe to re-run.** Scenes are regenerated; config assets are created only when missing and `GameConfig` references are filled only when empty. Wiring goes through `SceneBuildUtil.Set`, which throws on a renamed serialized field instead of leaving it null.

**`DocStatusWindow` is the enforcement mechanism for doc freshness.** It reads each module doc's YAML front-matter, compares `last-verified` to the current date and `verified-against` to HEAD, and shows a red/yellow/green indicator. A doc verified against a commit that is more than 20 commits old is yellow; more than 50 is red. This is an editor tool, not a build gate — it nags, it does not block.

## 11. Open questions

- [ ] Should the overlay be a toggle (one key) or a radial menu (hold + select tab)? Toggle is simpler; radial is faster for switching tabs on device.
- [ ] Should the recorder buffer in memory and flush on bout end, or stream to disk per-event? Buffering risks data loss on crash; streaming risks disk I/O hitches.
- [ ] Is IMGUI viable on high-DPI phones (text too small), or do we need a minimal uGUI fallback for the overlay?

## 12. Changelog

| Date | Commit | Change |
|---|---|---|
| 2026-10-23 | (pre-git) | **HUD attention budget in the builders ([ADR-0021](../Decisions/ADR-0021-hud-attention-budget.md), M10).** `V2HudBuilder` adds and wires the budget's CanvasGroups and roots (`_topShade`, `_dim`, `_vignette`, `_rays`, `_lockDot`, `_gaugeRoot`, `_promptGroup`, `_bannerGroup`, `_perfectTag`/`_multTagRoot`, `_meterGroup`, `_tagGroup`, `_burst`). It moves the C4 ring to the lower third, cuts the top shade to 140 px, removes D1's bottom shade, thins D2's edge to 3 px, lowers the C6/C5 ring glows, and bakes the intro dim and K.O. tint opaque. `PrototypeSceneBuilder.BuildConfigAssets` creates `Data/UI/HudAttention.asset` and wires `GameConfig.hudAttention` when empty. Verified: `BuildNonInteractive()` rebuilt clean over MCP, 0 Console errors/warnings |
| 2026-09-19 | (pre-git) | Created (Draft) |
| 2026-09-19 | (pre-git) | **Foundation landed.** `SlapMania.Logging` asmdef (zero deps) shipped as an early spike so R4 holds from day one: `SlapLog.Trace/Info/Warn/Verbose` are `[Conditional]` and strip in Release; `SlapLog.Error` is unconditional per CONVENTIONS/DEBUG §8 (M13 §3's earlier "`Error` is Conditional" note is wrong and should be fixed). `LogChannel` enum and `SlapAssert` also here. The `SlapMania.Diagnostics` asmdef with the `SLAPMANIA_DEBUG` defineConstraint is **not yet created** — no overlay, recorder or cheats yet. |
| 2026-09-26 | (pre-git) | **Editor tooling landed ([ADR-0011](../Decisions/ADR-0011-adopt-v2-design-prototype-first.md)).** New `SlapMania.Editor` asmdef (Editor-only; refs Core, Data, Gameplay, Logging, Input, Presentation, UI, App, Unity.InputSystem, Unity.TextMeshPro, UnityEngine.UI) — the `Editor` row of ARCHITECTURE §2, now real. Contents: `PrototypeSceneBuilder` + `PrototypeHudBuilder` + `SceneBuildUtil` (generate configs, `Boot.unity`, `Proto_Bout.unity`, Build Settings), `PlayFromBoot` (menu toggle, on by default), `TuningHubEditor` (the Inspector for `SlapMania.App.TuningHub`). Menu root follows the documented `Tools > Slap Mania` convention. §2 and §10 updated. Still not created: `SlapMania.Diagnostics`, overlay, recorder, cheats, `DocStatusWindow`. Also fixed the stale §3 row the entry above flagged: `SlapLog.Error` is listed as unconditional, matching the code. |
| 2026-09-26 | (pre-git) | **Verified in the Editor over MCP.** Compiled clean (all assemblies incl. `SlapMania.Editor.dll`), EditMode **32/32**, TMP Essentials imported, `BuildNonInteractive()` added and run: Boot + Proto_Bout generated, Build Settings Boot(0)/Proto_Bout(1), every serialized reference non-null, Play From Boot armed, Tuning Hub tagged `EditorOnly`. Played Boot -> additive Proto_Bout to BoutEnd twice and exercised Rematch (`GameRoot.RequestBoutReload`) — zero Console errors/warnings. Two fixes from the screenshots: the camera moved back to z = -8.4 so the defeat recoil stays in frame (damage-number X 0.68/0.32), and the prompt/sub-prompt auto-size (56–108 / 26–40 pt) so "OPPONENT'S TURN" fits 1080 wide. Found S-068 (MCP 0 instances — stdio vs HTTP) and S-069 (unfocused Editor freezes Play Mode). |
| 2026-10-19 | (pre-git) | **Front-end tooling ([ADR-0013](../Decisions/ADR-0013-ui-toolkit-front-end-from-mockups.md)).** `FrontEnd.FrontEndSceneBuilder` (`Tools > Slap Mania > Front End > Build Front End Scene`, plus `BuildNonInteractive`). It generates: PanelSettings, theme and text-gradient presets in `Settings/UI`; `Data/UI/UiMotion.asset` (wired into `GameConfig.uiMotion`); `FrontEnd.unity` (UIDocument + `GammaPresenter` + `ScreenManager` + `FrontEndBridge`, the overlay canvas, EventSystem, clearing camera). It also points Boot at FrontEnd and inserts it after Boot in Build Settings. Assets are created only when missing and every wire is asserted (S-075). `FrontEnd.UiAssetImportRules` imports `Art/UI/Icons/*.svg` as VectorImage with `PreserveViewport`, and `Art/UI/Mockup/*` without power-of-two rescale or mipmaps. The prototype builder now creates the motion asset and keeps FrontEnd through rebuilds. Outside Unity: `Tools/mockup2uxml` (mockup -> UXML converter with named hooks and an approximation report), `pngio.py`, and `compare.py` (fidelity score vs headless-Edge renders). Verified over MCP: compile clean, EditMode 116/116, every screen captured and scored. |
| 2026-09-26 | (pre-git) | **Builders for the V2 HUD, the humanoid fighter and the arena.** `Hud.V2HudBuilder` (+ `HudStyle`, `UiKit`) replaces `PrototypeHudBuilder`; `Characters.FighterImportSetup` / `FighterBuilder` / `RagdollBuilder`; `Arena.ArenaBuilder` (+ `ArenaArt`, `ArenaPaint`, `ArenaMeshes`) with the `Regenerate Arena Art` and `Reset Arena Look` menus; `SceneBuildUtil.Set(Vector3)`. The `SlapMania.Editor` asmdef gained **`Unity.RenderPipelines.Core.Runtime` + `Unity.RenderPipelines.Universal.Runtime`** (owner-approved asmdef edge, R9; Editor-only, so it cannot reach a player build) for the Volume profile and camera data. §2 updated. Verified over MCP: compile clean, EditMode 63/63, `BuildNonInteractive()` builds and bakes, Play reaches BoutEnd with a KO ragdoll landing on the floor. |
| 2026-10-20 | (pre-git) | **Bout overlay generation (M10).** `FrontEndSceneBuilder.BuildScreenHost(name, sortingOrder, initial, only, overlay)` + `BoutOverlayScreens` (PauseMenu, Replay, RoundEnd, RankUp, Settings); `BuildScene` uses the same private `CreateHost`, so FrontEnd.unity is unchanged except that its canvas is named "Front End Output". `PrototypeSceneBuilder.BuildBoutScene` adds the overlay ("Bout Screens", sorting 20) and wires `BoutController._screens` (asserted). `V2HudBuilder.BuildScorebug` adds the pause button (38 px glass, two-bar icon, `PressScale`) and wires `HudScorebug._pause`. §2 updated. |
| 2026-10-20 | (pre-git) | **V1 page tooling ([ADR-0015](../Decisions/ADR-0015-v1-page-behind-a-tuning-switch.md), M10).** `FrontEndSceneBuilder.CollectScreens` prefixes a subfolder's screens with its name (`Screens/V1/Home.uxml` -> `ScreenId.V1Home`). New `EnsureGradients` runs on every build, not only when the PanelSettings is first created, so a page added later gets its presets (`V1Red`, `V1Gold`, `V1Silver`). `TuningHubEditor` gained a FRONT END section (`DrawFrontEnd`) drawing `GameConfig.frontEndPage` as "Menu page"; `DrawRules` is unchanged. `Tools/mockup2uxml` converts both canvas pages (`PAGES`, `--page`), with per-family font metrics, CSS weight matching, synthetic bold, `box-sizing: content-box` pages and per-page gradient presets. It also splits a hard shadow under gradient text into a *shadow twin* and drops a soft one (S-082), and predicts CSS wrapping from the fonts' advance widths (new `ttfmetrics.py`, S-084). V2 output is unchanged except Walkout's "VS" (soft shadow dropped). |
| 2026-10-20 | (pre-git) | **`V2HudBuilder`: C6 pause button.** `PauseButton(rt, owner)` now adds a `HudPauseButton` (wired `_button`, `_owner`). `BuildCharge` places one in the C6 top letterbox. `Build()` collects every `HudPauseButton` into `V2Hud._pauseButtons` and throws unless there are exactly 2 (R11). Found while rebuilding: the prototype builder has fallen back to capsule fighters since the base FBX moved (DEBUG S-085, open). |
| 2026-10-23 | (pre-git) | **S-085 fixed: humanoid fighters again, and no silent capsules.** `FighterImportSetup.AlucardBase`/`AlucardFolder` (a stale `Animations/` path for the base FBX) are replaced by `FindBaseModel(AlucardStem)` (exact file name under `Assets/_Project`, throws on none or two), `AlucardClipFolder` and `AlucardClipPath(clip)`. `SetUpHumanoidSet(basePath, clipFolder)` no longer assumes the clips sit next to the base, and reports zero clips. `FighterBuilder` uses the new members. `PrototypeSceneBuilder`: `BuildNonInteractive` throws (wrapping the fighter error) instead of falling back to capsules; the interactive menu keeps the capsule fallback, its warning and its dialog note; a prefab that vanishes after the scene switch now throws in both modes. §2, §8 updated. Verified over MCP: compile clean, EditMode 195/195, `BuildNonInteractive` -> `BoutController` player/opponent are `HumanoidFighterView` (0 `PlaceholderFighterView`); Play reached BoutEnd (player KO) with the ragdoll at rest on the floor (hips y = 0.14 m), 0 errors/warnings. Re-verified after a second rebuild through the real path: Boot -> FrontEnd (Title, MainMenu, PLAY RANKED, Search, Walkout, Tale of the Tape auto-start) -> Proto_Bout. The Animator played Idle, Charge, Attack_Hook, Attack_Cross, Hit and Victory, and the KO ragdoll landed again. |
| 2026-10-23 | (pre-git) | **Hold-ring tooling ([ADR-0014](../Decisions/ADR-0014-hold-ring-attack-mode.md)).** `PrototypeSceneBuilder.BuildConfigAssets` creates `Data/Prototype/HoldRing.asset` and wires `GameConfig.holdRing` (created only when missing, like every config). The build dialog's controls text describes both attack modes. `TuningHubEditor.DrawRules` gains a "Hold ring" section; `DrawLive` shows the attack mode and, during a HoldRing turn, the meter state, closure, wind-up, power and close time. `V2HudBuilder` wires the new mode-copy and fade fields: `HudTiming` (`_captionText`, `_chrome` = Safe group, `_shade` = Bleed group, `_hint`, `_hintSub`), `HudCharge` (`_prompt`, `_hint`) and `HudIntro` (`_hintCharge`, `_hintTiming`). Verified 2026-10-23 over MCP: `BuildNonInteractive` wired every new field (read back with `SerializedObject`, humanoid fighters). Play from Boot -> FrontEnd -> bout (HoldRing) reached BoutEnd with Space held and released through the Input System. |
| 2026-10-23 | (pre-git) | **`SlapMania.Diagnostics` created; first tool: the camera recorder ([0020](../Decisions/ADR-0020-camera-keyframe-tracks-and-runtime-recorder.md), M16).** The asmdef has Define Constraint `SLAPMANIA_DEBUG` and references Core, Data, Logging, Presentation and Unity.InputSystem. **`SLAPMANIA_DEBUG` was never defined** (DEBUG S-098), so every `SlapLog.Info/Warn/Trace` call had been stripped, Editor included. It is now set for Standalone, Android and iOS in Player Settings, and must be removed for release (S-060). `CameraRecorder` attaches itself to the scene's `CameraDirector` on scene load: IMGUI panel (F8 or the CAM button), free-fly (F9), keys, transitions, preview and JSON save. Editor: `Tools > Slap Mania > Camera > Import Recorded Sequences / Send Selected Sequence To Recorder / Open Recordings Folder` (`Editor/Cinematics/CameraSequenceImporter.cs`). `PrototypeSceneBuilder.BuildCamera` adds `CameraLens` + `CameraDirector` to the Main Camera. |
| 2026-10-24 | (pre-git) | **Camera recorder: Rig & states tab (M16).** `CameraRecorder` is now `partial`; `CameraRecorder.Rig.cs` adds live editing of rig angles, FOV, shake presets and per-state transitions, Undo/Redo, and named saves to `Library/CameraRigSaves/`. Editor `CameraRigSetupApplier` (`[InitializeOnLoad]`) offers to apply the last save when Play stops; there is also `Tools > Slap Mania > Camera > Apply Camera Rig Save... / Open Camera Rig Saves Folder`. **S-098 follow-up:** the define set over MCP had not persisted to `ProjectSettings.asset`. It is now written in the asset. |
