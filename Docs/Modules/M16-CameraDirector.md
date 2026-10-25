---
module: M16-CameraDirector
status: InProgress
owner: shobhit
last-verified: 2026-10-25
verified-against: (pre-git)
asmdef: SlapMania.Presentation (+ Core.Cinematics math, Data.CameraSequence, Diagnostics recorder)
---

# M16 — CameraDirector

> **One-liner:** A hand-rolled camera stack: the rig frames gameplay, the `CameraDirector` layers recorded keyframe sequences, trauma shake and a DoF lens on top, and a runtime recorder authors those sequences in the running game. No Cinemachine.

## 1. Purpose

The camera is the audience's eye into the arena. It needs to frame the action — wide for the charge, medium for the timing, tight for the impact, cinematic for the finisher. It swaps sides each turn, shakes on hits, and holds steady during snapshots. All of this is done with a custom rig, not Cinemachine, because Cinemachine's complexity budget (blend trees, state-driven cameras, noise profiles, confiner volumes) is overkill for a portrait game with a fixed arena and two characters, and its runtime cost (200+ components, constant `LateUpdate` polling) is unjustifiable on mobile.

## 2. Responsibilities and non-goals

**Owns**
- `CameraRig` — a transform hierarchy (pivot → arm → camera) with blend-capable position/rotation.
- `ShotLibrary` — predefined camera poses: Wide, MediumAttacker, MediumDefender, CloseupImpact, Finisher.
- Shot transitions — smooth blends between poses, timed to match signals.
- Trauma-based screen shake — `AddTrauma(float)` decays over time, drives random offset via Perlin noise.
- Turn swap — camera swings to face the current attacker.
- Finisher camera sequence — a scripted series of shots for the cutscene.
- Portrait composition constraints — the 9:16 frame limits vertical headroom, so shots must be tighter than landscape equivalents.

**Landed so far: an interim `CameraRig` (2026-09-26, with the arena, [ADR-0012](../Decisions/ADR-0012-arena-mobile-fakes.md))**

`SlapMania.Presentation.CameraRig` (`Presentation/Arena/CameraRig.cs`) is the minimal camera language, until the shot library lands:

- **Intro fly-in.** `PlayIntro(seconds)` flies from a wide, raised establishing shot to the gameplay pose. The shot matches the reference clip: podium lower-centre, jumbotron upper-right. Easing is `InOutCubic`; time is unscaled. `BoutController` calls it on `BoutIntroStart` with `BoutConfig.introMs`.
- **Impact kick.** `Kick(strength)` shakes the child camera with the in-house `Tweener`, applying position and roll scaled by damage. `BoutController` calls it on `ImpactFrame` (1.0 on a lethal slap). This stands in for `AddTrauma`.
- **Composition.** The rig moves and the camera is its child, so a kick composes on top of any move.
- **Prototype poses** (set by the builder):
  - Gameplay: (0, 1.6, −4.7) looking at (0, 1.22, 0), FOV 50. Both fighters, the podium and a KO fall fit the 9:16 frame, with the jumbotron in the upper third.
  - Intro: (2.4, 3.4, −10.5), Euler (9, −11, 0).
- **Main camera:** post-processing on, FXAA, far clip 90 m, clear colour #050307.

**Landed: CameraDirector, keyframe sequences and the runtime recorder (2026-10-23, [ADR-0020](../Decisions/ADR-0020-camera-keyframe-tracks-and-runtime-recorder.md))**

```
Camera Rig (CameraRig: intro fly-in, gameplay pose)        <- moves first
 └─ Main Camera (CameraDirector [order 100], CameraLens)   <- owns the final pose
      base = rig pose -> recorder override -> sequence (blend in/out) -> shake -> lens
```

- **Track math (Core, pure).** `SlapMania.Core.Cinematics`: `CameraPose`, `CameraKeyframe`, `CameraSequenceData`, `CameraTrack`, `CameraShakeMath`, `ICameraTargets`. The pose is a function of elapsed `double` ms (R8). Tests: `CameraTrackTests`, `CameraShakeMathTests`, and the extended `EasingTests`.
- **Key semantics.** Each key is reached at `timeMs` and stays for `holdMs`, then the next transition starts. Every transition setting belongs to the *incoming* key: `path` (Cut / Linear / Spline), `ease` (any `Ease`, or a custom `AnimationCurve`), `lookAt`, `follow` and `shake`. Cut holds the previous pose and jumps exactly at the key time. If a hold runs past the next key, the camera arrives at once. Rotation uses SlerpUnclamped, so Back and Elastic overshoot rotation too. FOV, focus and aperture are clamped by `CameraPose.Sanitized`.
- **Easing.** `Ease` gained the full Sine, Quart, Quint, Expo, Circ, Back, Elastic and Bounce In/Out/InOut set. Values are appended (the enum serializes by value).
- **Targets.** A key's `lookAt` aims at a named target and keeps the key's roll. `follow` makes the stored position an offset from the target. A name that isn't bound falls back to the raw pose. `BoutController` binds `attacker`, `attackerHead`, `defender` and `defenderHead` on every TurnStart. Other targets can go in `CameraDirector._targets`.
- **Shake.** Trauma uses `ShakeProfile` presets on the director: 0 Light, 1 Impact, 2 Finisher, 3 Rumble. The offset scales with trauma² and uses Perlin noise, applied in camera space. `SetRumble(floor)` holds trauma at a floor. `CameraRig.Kick` now forwards to `AddTrauma` (lethal → Finisher preset). A key's `shake` fires once when the playhead reaches it.
- **Lens.** `CameraLens` owns a private global Volume (priority 50) with a runtime Bokeh `DepthOfField`. It is active only while the pose has `dof`, and the arena profile is never touched. This needs the Presentation → URP asmdef edges.
- **Cues.** `CameraDirector._cues` binds a `CameraSequence` asset to a `CameraCue` (BoutIntro, TurnStart, Impact, LethalImpact, Finisher, KO, BoutEnd). An unbound cue changes nothing.
- **Recorder (Diagnostics, debug builds only).** `CameraRecorder` attaches itself to the scene's director. F8 or the CAM button opens it, F9 toggles fly. Fly controls: WASD, Q/E, right mouse look, Z/C roll, wheel FOV; on touch, the left third moves and the rest looks. You can add, update, go to and delete keys, and set time/hold, path, ease, look-at/follow, shake type and amount. Playback offers play/pause/stop/loop, 0.25x-2x, and a scrubber. It saves JSON to `persistentDataPath/CameraSequences/`. It can also open a bound cue's sequence as a copy.
- **Per-state transitions (2026-10-24).** `CameraDirector._states` holds one `StateShot` per `CameraCue`. Every game state fires a cue: BoutIntro, TurnStart, ChargeStart, ChargeEnd, TimingStart, TimingEnd, Windup, Impact, LethalImpact, Finisher, KO, TurnEnd and BoutEnd. Each state has one of four modes:
  - **Keep**: nothing changes, except that a bound sequence still plays (the old behaviour; the default).
  - **Gameplay**: blend back to the rig over `blendMs` with `blendEase`.
  - **Shot**: blend into the state's own key over `blendMs` / `blendEase` (0 ms = a cut). Then either hold `holdMs` and return over `returnMs` / `returnEase`, or, with `holdMs` 0, hold until a later state changes the camera. A shot can be **relative to** attacker / defender / their heads (`followRotation`: offset and rotation in the target's local space), so one authored shot mirrors itself when turns swap. It can also look at a target and carry a shake.
  - **Sequence**: play the bound recorded sequence.

  A Shot is played as a one-key `CameraSequenceData` that is built once and refreshed in place, so firing a state allocates nothing after the first time.
- **Two-sided shoulder cams (2026-10-25).** A Shot state can be **two-sided** (`StateShot.perSide`): `key` is used on the PLAYER's attacking turns and `opponentKey` on the OPPONENT's, chosen by `CameraDirector.Attacker`, which BoutController sets on every TurnStart. Each side is its own authored camera, not a mirror. BoutController binds the fixed-side targets `player`, `playerHead`, `opponent` and `opponentHead` from BoutIntro onwards (they never swap), alongside `attacker`/`defender`. The Rig tab has **Right / Left shoulder** presets: follow the fighter's **root** (the head bone rotates with animation and would wobble), offset (±0.95, 1.5, -1.0) in the fighter's space, look at the other fighter's head, FOV 42. Tuned in Play on Proto_Bout, where the fighters stand about 1 m apart with heads at about 1.45 m. The first values, (±0.42, 1.62, -0.85), put the near head over the far face. **Side / Height / Fwd** sliders tune it, and **Preview** shows the side being edited.
- **Impact shake (2026-10-25).** Every landed slap calls `CameraDirector.Impact(intensity, lethal)`: trauma = `CameraShakeMath.ImpactTrauma(intensity, _impactMinTrauma 0.35, _impactScale 1)` on `_impactPreset` (Impact); a lethal slap gets full trauma on `_lethalPreset` (Finisher). These are code defaults, unverified on the scene (R11). Before this, a 0.3 slap gave trauma 0.3, shake 0.09 of max (about 1 cm). Now it gets 0.55 / 0.30. The Rig tab's IMPACT SHAKE section tunes all four and tests weak/strong/lethal hits. All of it is part of `CameraRigSetup` (saved and undoable).
- **Rig & states tab + saves (2026-10-24).** The recorder's second tab live-edits:
  - the gameplay angle and intro angle (*Set gameplay = view*, *Set intro = view*, *View gameplay*, *Play intro*)
  - the gameplay FOV and the intro ease
  - every state's transition (*Capture view -> shot*, *Preview*, *Back to gameplay*)
  - every shake preset (*Test*)

  It has Undo/Redo (64 steps; a slider drag is one step) and named saves in `<project>/Library/CameraRigSaves/*.json` as `CameraRigSetup` (local, never committed). After a save, **stopping Play asks to apply it** to the open scene's CameraRig / Camera / CameraDirector through Undo (`Editor/Cinematics/CameraRigSetupApplier.cs`). `Tools > Slap Mania > Camera > Apply Camera Rig Save...` applies any save later. Note that *Build Playable Prototype* still rebuilds the rig from `PrototypeSceneBuilder`'s constants.
- **Editor.** `Tools > Slap Mania > Camera > Import Recorded Sequences` turns the JSON into `Assets/_Project/Data/Camera/<name>.asset`. `Send Selected Sequence To Recorder` goes the other way.

**Explicitly does NOT do**
- Own the photo camera → M09.
- Own post-processing → URP Volume.
- Own VFX → M15.
- Own hitstop → M08 (but the camera holds its position during hitstop).
- Provide a player-controllable camera → this is not an open-world game.

## 3. Public API surface

| Member | Signature | Notes |
|---|---|---|
| `CameraRig.SetShot` | `void SetShot(ShotType shot, float blendTime)` | Blends to the target pose |
| `CameraRig.AddTrauma` | `void AddTrauma(float amount)` | 0–1, decays at `shakeDecay` per second |
| `CameraRig.Trauma` | `float { get; }` | Current trauma level |
| `CameraRig.LockForSnapshot` | `void LockForSnapshot()` | Freezes position/rotation until unlocked |
| `CameraRig.Unlock` | `void Unlock()` | Resumes normal operation |
| `CameraRig.PlayFinisherSequence` | `Awaitable PlayFinisherSequence(FinisherType type)` | Scripted camera moves |
| `CameraRig.PlayIntro` | `void PlayIntro(float seconds)` | **Landed (interim).** Establishing fly-in |
| `CameraRig.Kick` | `void Kick(float strength)` | **Landed (interim).** 0–1; stands in for `AddTrauma` |
| `CameraRig.SnapToGameplay` | `void SnapToGameplay()` | **Landed (interim).** Also runs on `Awake` |
| `CameraRig.Director` | `CameraDirector { get; }` | **Landed.** The director on the child camera, or null |
| `CameraDirector.Play` | `void Play(CameraSequence)` / `void Play(CameraSequenceData, double startMs = 0)` | **Landed.** Blends in from the current pose over `blendInMs` |
| `CameraDirector.Stop` | `void Stop(bool immediate = false)` | **Landed.** Blends back to the rig over `blendOutMs` |
| `CameraDirector.Cue` | `bool Cue(CameraCue)` | **Landed.** False when nothing is bound |
| `CameraDirector.Seek` / `Paused` / `PlaybackSpeed` | `void Seek(double ms)`, `bool`, `float 0..4` | **Landed.** Seek never fires key shakes |
| `CameraDirector.AddTrauma` | `void AddTrauma(float amount, int preset = 1)` | **Landed.** Replaces the planned `CameraRig.AddTrauma` |
| `CameraDirector.SetRumble` | `void SetRumble(float floor, int preset = 3)` | **Landed.** 0 clears |
| `CameraDirector.SetTarget` | `void SetTarget(string name, Transform t)` | **Landed.** For look-at/follow keys |
| `CameraDirector.SetOverride` / `ClearOverride` | `void (in CameraPose)` / `void ()` | **Landed.** Recorder free-fly; clearing blends back over 300 ms |
| `CameraTrack.TryEvaluate` | `static bool (List<CameraKeyframe>, double ms, ICameraTargets, out CameraPose)` | **Landed.** Pure; NaN/negative ms read as 0 |
| `CameraDirector.FindState` / `EnsureState` | `StateShot (CameraCue)` | **Landed 2026-10-24.** Per-state transition entries |
| `CameraDirector.ReturnToGameplay` | `void ReturnToGameplay(double ms, Ease ease)` | **Landed 2026-10-24.** No-op when already on the rig |
| `CameraDirector.RestFov` | `float { get; set; }` | **Landed 2026-10-24.** Settable: the gameplay FOV |
| `CameraDirector.Get/SetShakePreset`, `Copy/SetShakePresets`, `Copy/SetStateShots` | | **Landed 2026-10-24.** For `CameraRigSetup` |
| `CameraRig.GameplayPosition/Euler`, `IntroPosition/Euler`, `IntroEase` | properties | **Landed 2026-10-24.** Setting does not move the rig; call `SnapToGameplay` |
| `CameraRigSetup.Capture` / `ApplyTo` | `static CameraRigSetup (rig, cam, director)` / `void (rig, cam, director)` | **Landed 2026-10-24.** One save/undo unit; Play or Edit mode |
| `CameraDirector.Attacker` | `FighterSlot { get; set; }` | **Landed 2026-10-25.** Picks two-sided shots' side |
| `CameraDirector.Impact` | `void Impact(float intensity, bool lethal)` | **Landed 2026-10-25.** Called by `CameraRig.Kick(strength, lethal)` |
| `CameraShakeMath.ImpactTrauma` | `static float (float intensity, float minTrauma, float scale)` | **Landed 2026-10-25.** Pure; 0/NaN/negative intensity = 0 |
| `CameraShakeMath.Sample` | `static void (in ShakeProfile, float trauma, double t, out Vector3 offset, out Vector3 angles)` | **Landed.** Pure |

**Consumed by:** M01 (shot changes on signals), M08 (trauma on impact), M09 (lock during capture).
**Stability:** shot list is stable; poses and blend times will churn with art.

## 4. Configuration

Asset: `Assets/_Project/Data/Camera/CameraConfig.asset` (planned).

**Camera sequences:** `CameraSequence` assets in `Assets/_Project/Data/Camera/` (created by the importer). Fields per sequence: `loop`, `unscaledTime` (default true), `blendInMs` 250 and `blendOutMs` 350. These are the code defaults; a recorded file's values are unverified until read (R11). Shake presets are serialized on `CameraDirector._shakePresets`. Code defaults (unverified on the scene instance, R11): Light 0.04 m / 18 Hz / decay 3; Impact 0.15 m / (2,2,3) deg / 25 Hz / 3; Finisher 0.28 m / (3.5,3.5,6) deg / 22 Hz / 1.6; Rumble 0.05 m / 6 Hz / 0.8.

**Interim:** the rig's poses and kick (`_kickMetres` 0.06, `_kickDegrees` 1.4, `_kickSeconds` 0.28) are **serialized on the `CameraRig` component**, not an asset. The Tuning Hub lists the rig under scene components, so Play Mode edits there **revert**. Move them into `CameraConfig` when the shot library lands (ADR-0011 §5).

### Shots

| Shot | FOV | Distance | Height | Use |
|---|---|---|---|---|
| `Wide` | 40° | 5.0 | 2.0 | Default, during charge phase |
| `MediumAttacker` | 35° | 3.5 | 1.5 | During timing phase, framed on attacker |
| `MediumDefender` | 35° | 3.5 | 1.5 | During windup, framed on defender |
| `CloseupImpact` | 28° | 2.0 | 1.2 | Impact frame through hitstop |
| `Finisher` | varies | varies | varies | Multiple poses in a scripted sequence |

### Shake

| Field | Type | Default | Sane range | Notes |
|---|---|---|---|---|
| `maxTrauma` | float | **1.0** | — | Clamped |
| `shakeDecay` | float | **3.0** | 1.0 – 5.0 | Trauma per second |
| `maxShakeOffset` | float | **0.15** | 0.05 – 0.30 | World units |
| `maxShakeRotation` | float deg | **2.0** | 0.5 – 5.0 | |
| `shakeFrequency` | float | **25.0** | 15 – 40 | Perlin noise frequency |

### Blend

| Field | Type | Default | Notes |
|---|---|---|---|
| `defaultBlendTime` | float s | **0.5** | For standard shot changes |
| `impactBlendTime` | float s | **0.15** | Fast push-in on impact |
| `turnSwapBlendTime` | float s | **0.8** | Matches `turnTransitionDelay` in M01 |
| `blendCurve` | AnimationCurve | ease-in-out | |

## 5. Events

**Emits**

None — the camera is driven, it does not drive.

**Consumes**

| Event | From | Reaction |
|---|---|---|
| `MatchSignal.BoutIntroStart` | M01 | Fly-in from a high wide angle to `Wide` |
| `MatchSignal.TurnStart` | M01 | Swap side, blend to `Wide` |
| `MatchSignal.ChargeStart` | M01 | Hold `Wide` |
| `MatchSignal.TimingStart` | M01 | Blend to `MediumAttacker` |
| `MatchSignal.WindupStart` | M01 | Blend to `MediumDefender` |
| `MatchSignal.ImpactFrame` | M01 | Snap to `CloseupImpact`, `AddTrauma(intensity)` |
| `MatchSignal.HitstopEnd` | M08 | Blend back to `Wide` |
| `MatchSignal.FinisherStart` | M01 | `PlayFinisherSequence()` |
| `MatchSignal.SnapshotCaptured` | M09 | `Unlock()` |

**As landed (2026-10-23):** `BoutController` calls `CameraDirector.Cue` on BoutIntroStart, TurnStart, ImpactFrame (`LethalImpact` when lethal), FinisherStart and BoutEnd. A bound sequence plays over the rig; unbound cues leave the planned shot logic above to the rig. The snapshot lock is still planned.

## 6. State machine

```
Idle --SetShot--> Blending --complete--> Holding --SetShot--> Blending
                                           \--LockForSnapshot--> Locked --Unlock--> Holding
```

During `Locked`, all blends are suspended and trauma is frozen. The snapshot camera (M09) captures during this state.

## 7. Dependencies

- **Hard:** `SlapMania.Core` (signals), `SlapMania.Data` (`CameraConfig`).
- **Soft:** M08 (trauma source), M09 (lock/unlock).
- **Unity APIs:** `Camera`, `Transform`, `Mathf.PerlinNoise`, `AnimationCurve`, `Awaitable`.
- **Must initialize after:** M00 (config + scene).

## 8. Failure modes

| Failure | Detection | Behaviour | Player-visible? |
|---|---|---|---|
| Shot not found in library | Assert | Hold current shot, log Error | Stuck camera |
| Blend interrupted by another blend | Normal — latest `SetShot` wins | Blends from current interpolated position | Smooth |
| Trauma exceeds 1.0 | Clamp | | No |
| Lock never unlocked | Watchdog — 5 second timeout | Force unlock, log Error | Camera resumes |
| Finisher sequence references missing pose | Assert | Skip to next pose | Visual glitch |

## 9. Debug hooks

- **Log channel:** `LogChannel.Camera`
- **Overlay (Camera tab):** current shot, blend progress, trauma, world position/rotation, FOV, locked state.
- **Cheats:** force any shot, set trauma (slider), play finisher sequence, lock/unlock.
- **Recorder events:** `ShotChanged`, `TraumaAdded`, `SnapshotLocked`.
- **Gizmos:** frustum wireframe, target position marker, trauma offset visualization.
- **Landed:** the runtime **camera recorder** (Diagnostics, see §2). Its SHAKE TEST row fires each preset. `CameraDirector.Trauma`, `ElapsedMs` and `LastPose` are readable for an overlay.

## 10. Tuning notes

**Why no Cinemachine.** See [ADR-0005](../Decisions/ADR-0005-no-cinemachine.md). The short version: Cinemachine's `CinemachineBrain` polls every `LateUpdate`, its state-driven cameras add indirection that is hard to reason about in a strict FSM, and the game has exactly 5 shots that can be defined as transforms. A 50-line `CameraRig` class does the job with zero runtime overhead beyond the blend lerp.

**Portrait composition changes everything.** In a 16:9 landscape frame, a two-person shot has room for both fighters. In a 9:16 portrait frame, both fighters are stacked vertically and framing one means losing the other. The `Wide` shot must be higher and further back than a landscape equivalent. The `Closeup` shot is tighter than it would be in landscape because there is more vertical space to fill. Author all shots in portrait — if someone tests in landscape (Editor default), the composition will look wrong and they will "fix" it incorrectly.

**Turn swap direction matters.** The camera swings around the ring so the attacker is always on the near side (bottom of the screen in portrait). This gives the player a consistent spatial model: "my side is near, opponent is far." If the swap direction is random or inconsistent, the player loses spatial orientation.

**Shake uses Perlin noise, not random.** Random offsets per frame produce jitter. Perlin noise produces smooth, organic shake that reads as physical force. The frequency controls how fast the shake oscillates; 25 Hz is fast enough to feel violent but slow enough to be readable. Trauma^2 is used for the offset magnitude (not linear trauma) so low trauma is subtle and high trauma is dramatic.

## 11. Open questions

- [x] ~~Timeline or code-driven finisher camera?~~ Neither: recorded keyframe sequences played by the director ([ADR-0020](../Decisions/ADR-0020-camera-keyframe-tracks-and-runtime-recorder.md)). M18 may still use Timeline for whole cutscenes.
- [ ] Uniform Catmull-Rom changes speed at unevenly spaced keys. Switch to centripetal/time-parameterised if a recorded fly-through reads badly.
- [ ] Bokeh DoF cost on the low tier: capture a perf baseline before any shipped sequence enables `dof`.
- [ ] Snapshot lock (`LockForSnapshot`) is still planned. A sequence playing during a capture would currently move the camera mid-photo.
- [ ] Does the portrait letterbox (black bars if the device is wider than 9:16) need to be camera-driven or post-process? Most phones are narrower than 9:16, so the bars would only appear on tablets.
- [ ] Should the camera have a subtle idle sway (breathing motion) during charge, or is static better for the power bar readability?

## 12. Changelog

| Date | Commit | Change |
|---|---|---|
| 2026-09-19 | (pre-git) | Created (Draft) with shot library, shake model, and portrait composition notes |
| 2026-09-26 | (pre-git) | **Interim `CameraRig` landed with the arena ([ADR-0012](../Decisions/ADR-0012-arena-mobile-fakes.md)).** Intro establishing fly-in (unscaled, `InOutCubic`, `BoutConfig.introMs`) and a Tweener impact kick on the child camera; builder-set gameplay/intro poses; post-processing and FXAA on the main camera. Status Draft → InProgress. §2, §3, §4 updated. Shot library, turn swap, `AddTrauma`, snapshot lock and the finisher sequence are not implemented; the poses live on the component until `CameraConfig` exists. |
| 2026-10-23 | (pre-git) | **CameraDirector, keyframe sequences, trauma shake, DoF lens and the runtime recorder ([ADR-0020](../Decisions/ADR-0020-camera-keyframe-tracks-and-runtime-recorder.md)).** Core: `SlapMania.Core.Cinematics` (pose, key, sequence data, `CameraTrack`, `CameraShakeMath`) plus the full `Ease` set. Presentation: `CameraDirector`, `CameraLens`, `CameraCue`; `CameraRig.Kick` -> trauma. Data: `CameraSequence`. Diagnostics: `CameraRecorder`. Editor: the importer and the builder adding the director and lens. App: cue calls and target binding. Asmdef: Presentation -> URP Core/Universal runtime. ProjectSettings: `SLAPMANIA_DEBUG` (S-098). EditMode: 315/317; the two failing cases are `InBounce`/`InOutBounce`, left as a TODO(human) for the owner. §2, §3, §4, §5, §9, §11 updated. Not yet verified in Play (the prototype scene needs a rebuild to get the director). |
| 2026-10-24 | (pre-git) | **Per-state camera transitions and the Rig & states tab with saves and undo.** Core: `CameraKeyframe.followRotation` (attacker-relative shots), `ICameraTargets.TryGetRotation`, `CameraSequenceData.blendInEase/blendOutEase/holdAtEnd`; tests for both follow-rotation cases. Presentation: `StateShot`/`StateCameraMode`, `CameraRigSetup`, director state table + `ReturnToGameplay` + setters, `CameraRig` pose properties, six new `CameraCue` values (appended). App: every state cues (M00). Diagnostics: `CameraRecorder.Rig.cs`. Editor: `CameraRigSetupApplier`. Verified in Play over MCP: a ChargeStart Shot held at its exact pose and FOV 35; ReturnToGameplay landed on the rig pose and FOV 50; Save wrote JSON with states and the pending marker; Undo restored FOV; Delete removed the file. EditMode 332/334 (the two `InBounce`/`InOutBounce` TODO(human) cases). §2, §3 updated. |
| 2026-10-25 | (pre-git) | **Two-sided shoulder cams and tunable impact shake.** `StateShot.perSide` + `opponentKey` + `KeyFor(attacker)`; `CameraDirector.Attacker`, `Impact`, impact fields; `CameraRig.Kick(strength, lethal)` (fixes S-099); `CameraShakeMath.ImpactTrauma` + `ImpactTrauma_Boundaries` test; `CameraRigSetup` saves the impact settings; Rig tab: two-sided toggle, side tabs, shoulder presets, offset sliders, IMPACT SHAKE section. BoutController binds `player`/`opponent` targets and sets `Attacker`. §2, §3 updated. **Verified 2026-10-25 over MCP:** compile clean; EditMode 333/335 (the two InBounce/InOutBounce TODO(human) cases). In Play on Proto_Bout, the side targets were bound, and both ChargeStart keys resolved to exactly their own-space offsets with 0.0 deg aim error at the other fighter's head. Screenshots in `Library/Screenshots/shoulder_*.png`; the shoulder presets were retuned from them. Impact trauma for slaps of 0.2 / 0.8 / lethal: 0.48 / 0.87 / 1.00 (Finisher preset). Average peak shake over 64 noise phases: 2.2 cm / 8.0 cm / 22.1 cm with 2.6 deg roll. A live slap's Kick -> Impact call was not observed; it was checked by code read only. |
