# ADR-0005 — Hand-rolled camera rig, no Cinemachine

**Status:** Accepted
**Date:** 2026-09-19
**Deciders:** shobhit

## Context

Cinemachine is Unity's standard camera system. It provides virtual cameras, blend trees, noise profiles, confiner volumes, state-driven switching, and a visual authoring workflow. It is excellent for open-world games, cutscene-heavy narratives, and projects with complex camera requirements.

Slap Mania has five camera shots, one arena, two characters, and a fixed portrait frame. The camera does: blend between shots on signal, shake on impact, lock during snapshots, and play a scripted finisher sequence.

## Decision

Use a hand-rolled `CameraRig` class: a transform hierarchy (pivot → arm → camera) with lerp-based blending, Perlin-noise trauma shake, and an `Awaitable`-based finisher sequence. No Cinemachine package.

## Consequences

### Positive

- **Zero runtime overhead** beyond the blend lerp. No `CinemachineBrain.LateUpdate` polling 200+ components.
- **Complete control.** The FSM drives the camera directly; there is no intermediate state-driven virtual camera layer to reason about.
- **Smaller build size.** Cinemachine adds ~1.5 MB to the APK.
- **Simpler debugging.** The camera state is one transform, one shot enum, and one trauma float. Cinemachine's state is spread across multiple GameObjects, blend stacks, and channel priorities.

### Negative

- **No visual authoring.** Camera shots are defined as data in `CameraConfig`, not as positioned virtual cameras in the scene. This is harder for artists who expect to scrub through blends in the Editor.
- **No built-in noise profiles.** Shake is a simple Perlin offset. Cinemachine's noise library is richer.
- **No confiner.** If the camera ever needs to be constrained to a volume, it must be hand-written.

### Risks

- If the game expands to multiple arenas with different geometries, a hand-rolled rig may need to be generalized in ways Cinemachine already handles. At that point, reconsidering Cinemachine is appropriate.

## Alternatives considered

| Alternative | Why rejected |
|---|---|
| Cinemachine | Runtime overhead, complexity budget, APK size — all unjustified for 5 shots |
| Unity 6's built-in camera components | Not mature enough; Cinemachine is still the recommended path, so this is Cinemachine-lite with fewer features |
| DOTween-based camera | DOTween is a third-party package; see ADR-0007 |
