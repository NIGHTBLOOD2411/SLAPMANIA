---
module: M18-Cutscenes
status: Draft
owner: shobhit
last-verified: 2026-09-19
verified-against: (pre-git)
asmdef: SlapMania.Presentation
---

# M18 — Cutscenes

> **One-liner:** Finisher cinematics and intro sequences — scripted camera, animation, VFX, and audio sequences that play during special moments.

## 1. Purpose

The finisher is the game's climax. When a lethal slap lands, the game enters a cutscene: the camera pulls in, time slows, the attacker winds up a dramatic finishing blow, the arena lights up, the crowd roars, and the impact lands with maximum spectacle. This module scripts those sequences.

**This module is a stub.** The full design pass was interrupted. The system depends on Unity Timeline and requires the presentation modules (M08, M14, M15, M16) to be substantially implemented before it can be designed in detail.

## 2. Responsibilities and non-goals

**Owns**
- Finisher cutscene sequences (scripted camera + animation + VFX + audio timing).
- Bout intro sequence (arena fly-in, fighter entrance).
- Victory sequence (winner pose, gallery transition).
- Timeline asset management and playback.
- `PlayableDirector` control — play, pause, stop, speed.

**Explicitly does NOT do**
- Own the camera rig → M16 (`PlayFinisherSequence` delegates to the `CameraRig`).
- Own fighter animation → M08 (`PlayFinisher` delegates to `IFighterView`).
- Own VFX → M15.
- Own audio → M11 (cutscene audio cues are queued through the standard audio system).
- Own the FSM → M01 enters `FinisherStart` and waits for the cutscene to complete.

## 3. Known constraints

- **Unity Timeline** (`com.unity.timeline`) is the intended authoring tool. It is already in the package list (kept for this module specifically — see [BUILD-AND-RUN.md](../BUILD-AND-RUN.md)).
- **Portrait framing:** finisher shots must be composed for 9:16. The dramatic angles that work in landscape (wide sweeping pans) do not work in portrait — vertical compositions, tight close-ups, and bottom-to-top tilts are the vocabulary.
- **Hitstop integration:** the finisher starts *after* hitstop from the triggering slap. The cutscene has its own rhythm and should not re-freeze time.
- **Duration budget:** a finisher cutscene should be **2–4 seconds**. Longer breaks the game's rhythm. The intro can be longer (up to 5 s) because it happens once.
- **Skippability:** the intro should be skippable on rematch. Finishers should *not* be skippable in v1 — they are the payoff. Revisit if playtest data shows skip demand.
- **Placeholder-first:** day-one cutscenes are `PlayFinisherSequence` on the `CameraRig` (a series of `SetShot` calls with timed blends). Timeline-authored sequences are a later milestone. The interface (`Awaitable PlayFinisherSequence(FinisherType)`) is the same either way.

## 4. Open questions

- [ ] How many finisher types should exist in v1? One generic finisher, or 2–3 variations (e.g. uppercut, backhanded slap, double slap)?
- [ ] Should the cutscene use Timeline or a code-driven sequence? Timeline is artist-friendly but adds authoring complexity; code is simpler for v1's capsule characters.
- [ ] Should the finisher include a slow-motion wind-up (0.2× time scale) before the final impact? This is genre-standard but may conflict with hitstopTimeScale > 0 requirement (M08).
- [ ] Should the UI hide completely during a finisher, or should a "FINISH!" banner overlay appear?
- [ ] Should the intro sequence show a split-screen fighter comparison (name, universe, stats) before the bout starts?
- [ ] Can the victory sequence reuse the gallery view (M10) as a world-space element, or should it be a separate full-screen UI?

## 5. Dependencies

- **Hard:** `SlapMania.Core` (signals), `com.unity.timeline`.
- **Soft:** M08 (fighter animation), M14 (lighting), M15 (VFX), M16 (camera), M11 (audio cues).
- **Unity APIs:** `PlayableDirector`, `TimelineAsset`, `SignalEmitter`, `SignalReceiver`.
- **Must initialize after:** everything else — cutscenes are the top of the presentation stack.

## 6. Changelog

| Date | Commit | Change |
|---|---|---|
| 2026-09-19 | (pre-git) | Created (Draft stub) with constraints and open questions |
