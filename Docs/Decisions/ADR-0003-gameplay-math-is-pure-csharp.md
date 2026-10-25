# ADR-0003 — Gameplay math is pure C#, no MonoBehaviour

**Status:** Accepted
**Date:** 2026-09-19
**Deciders:** shobhit

## Context

The core gameplay loop (power charging, timing multiplier, damage resolution, health tracking, AI tap generation) is deterministic math over floats and integers. If these systems are MonoBehaviours, they require a scene, a GameObject, and Play Mode to test. An EditMode test runs in under a second without entering Play Mode; a PlayMode test takes 3–10 seconds including domain reload and scene load.

## Decision

All gameplay simulation code lives in `SlapMania.Gameplay` and `SlapMania.Core` as **plain C# classes** — no `MonoBehaviour`, no `ScriptableObject`, no `UnityEngine` dependency beyond `Mathf` and `AnimationCurve`. These classes are constructed with `new`, receive `Tick(double dt)` calls from a thin MonoBehaviour driver in `SlapMania.Runtime`, and are fully testable in EditMode.

The pattern:
- `PowerMeter` is a `class`, not a `MonoBehaviour`. It has `Begin()`, `Tick(dt)`, `RegisterTap()`, `End()`.
- `MatchDirector` is a `class`. It has `StartBout()`, `Tick(dt)`.
- A single `MatchRunner : MonoBehaviour` in `SlapMania.Runtime` calls `MatchDirector.Tick(Time.deltaTime)` in `Update`. It is the only MonoBehaviour in the gameplay loop.

## Consequences

### Positive

- **EditMode tests for all gameplay math.** A test can `new PowerMeter()`, call `Tick()` 100 times, and assert the result in 2 ms.
- **Determinism.** No frame-rate dependency, no `Time.deltaTime` inside the simulation — `dt` is passed in.
- **Replay.** Feed recorded `TapEvent` structs and `dt` values to get identical results.
- **No scene dependency.** Gameplay tests do not load a scene, create GameObjects, or enter Play Mode.

### Negative

- **Indirect Unity API access.** `AnimationCurve.Evaluate` is a Unity API, but it is a pure function on floats, so it is acceptable. `Time.realtimeSinceStartupAsDouble` is read by the MonoBehaviour driver and passed in as a parameter, never read directly.
- **One more class in the dependency chain.** `MatchRunner` (MonoBehaviour) → `MatchDirector` (plain C#) → modules.

### Risks

- Developers unfamiliar with this pattern may put gameplay logic in MonoBehaviours out of habit. The asmdef boundary catches this at compile time — `SlapMania.Gameplay` cannot reference `UnityEngine.CoreModule` (except for `Mathf` and `AnimationCurve`, which are whitelisted via assembly reference includes).

## Alternatives considered

| Alternative | Why rejected |
|---|---|
| MonoBehaviour-based gameplay | Requires Play Mode for testing, 10× slower iteration, non-deterministic |
| ECS (DOTS) | Massive complexity for a two-entity game; no mobile maturity in Unity 6 |
| Static methods only | No state management; harder to test setup/teardown |
