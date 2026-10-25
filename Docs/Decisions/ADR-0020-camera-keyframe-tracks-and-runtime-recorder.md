# ADR-0020 — Camera keyframe tracks, a layered CameraDirector, and a runtime recorder

**Status:** Accepted
**Date:** 2026-10-23
**Deciders:** shobhit

## Context

M16 planned five fixed shots blended by `SetShot`. In practice the shots need authoring
*by eye, in the running game*. Framing two fighters in a 9:16 frame is something you see,
not something you type into an Inspector, and Play Mode edits to the interim `CameraRig`
revert anyway (M16 §4). We want cinematic moves (intro, impact, finisher) authored as
keyframes at runtime: pose, FOV, depth of field, look-at/follow target, per-key shake, and a
transition into each key (path + easing + hold). The same data must play in gameplay
with zero per-frame allocation.

[ADR-0005](ADR-0005-no-cinemachine.md) still holds: no Cinemachine, and no DOTween
([ADR-0007](ADR-0007-no-third-party-packages.md)).

## Decision

1. **Track math is pure Core** (`SlapMania.Core.Cinematics`, R2). `CameraTrack.Evaluate(keys, ms, targets)`
   is a pure function of elapsed `double` milliseconds (R8). Position paths are Cut / Linear /
   Catmull-Rom spline. Rotation is slerp. Easing comes from the shared `Easing` table (extended)
   or a custom `AnimationCurve`. Each key has a hold time. Trauma shake is Perlin noise scaled by
   trauma², computed in `CameraShakeMath`. Everything is EditMode-tested (R3).
2. **`CameraDirector` lives on the camera** (the rig's child), not on the rig. Each `LateUpdate`
   it layers: base = the rig's pose (so `CameraRig` keeps owning gameplay framing) →
   recorder override → playing sequence (with blend in/out) → shake → lens (FOV, DoF).
   When a sequence stops, the camera blends back to its local identity. The rig never
   needs to know a sequence ran.
3. **Data:** `CameraSequenceData` (Core, `[Serializable]`) is the one format. At runtime the
   recorder writes it as JSON to `persistentDataPath/CameraSequences/`. An Editor menu imports
   the JSON into a `CameraSequence` ScriptableObject under `Assets/_Project/Data/Camera/`.
   Gameplay plays assets, bound to cues (`CameraCue.Intro/Impact/Finisher/...`).
4. **The recorder is a new assembly, `SlapMania.Diagnostics`**, with Define Constraint
   `SLAPMANIA_DEBUG` (R4, M13). It has an IMGUI panel and free-fly controls via the Input System,
   and it is not compiled in release. This required adding `SLAPMANIA_DEBUG` to Player Settings,
   which had never been defined (DEBUG S-098).
5. **Depth of field:** `SlapMania.Presentation` now references `Unity.RenderPipelines.Core.Runtime`
   and `Unity.RenderPipelines.Universal.Runtime`. `CameraLens` owns a private global `Volume`
   with a runtime `DepthOfField` (Bokeh) override, active only while a key asks for DoF. The
   arena's own profile is never touched.

## Consequences

### Positive

- Shots are authored in the real game, on the real device if needed, and survive Play Mode.
- One evaluation path for the recorder preview and for gameplay, so what you record is what plays.
- `CameraRig` and `BoutController` barely change: a cue call per signal.

### Negative

- Bokeh DoF is expensive on mobile GPUs. It is off unless a key enables it; re-capture perf before
  shipping a sequence that uses it.
- Two assembly-graph edges added (Presentation → URP, and the new Diagnostics assembly).
- Uniform Catmull-Rom with unequal key spacing changes speed at keys. That is accepted for
  v1; switch to centripetal/time-parameterised if it reads badly.

### Risks

- `SLAPMANIA_DEBUG` must be removed for release (RELEASE-CHECKLIST, S-060). `BuildVerifyPreprocessor`
  is still only planned.
- JSON written on a device lives in that device's `persistentDataPath`, so it must be pulled
  (adb) to import. Recording in the Editor is the main workflow.

## Alternatives considered

| Alternative | Why rejected |
|---|---|
| Unity Timeline for camera moves | Editor-only authoring, can't record at runtime; heavier playable graph per sequence. Still possible later for M18 full cutscenes. |
| Recorder as an EditorWindow | No device recording; user chose runtime IMGUI |
| Recorder inside Presentation behind `[Conditional]` | Debug UI could leak into release; R4 prefers a define-constrained assembly |
| Write recordings straight to a ScriptableObject | Play Mode SO edits are fragile and Editor-only; JSON is the runtime-safe format |
| Defer DoF | User chose to include it in v1 |
| `pos += velocity * dt` dolly | Violates R8; can't scrub or loop deterministically |
