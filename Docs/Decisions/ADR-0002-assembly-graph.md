# ADR-0002 — Assembly dependency graph with one-way edges

**Status:** Accepted — assembly *list* superseded by [ADR-0010](ADR-0010-assembly-graph-refinement.md) (the one-way-edge principle below stands unchanged)
**Date:** 2026-09-19
**Deciders:** shobhit

## Context

Unity projects without assembly definitions compile everything into `Assembly-CSharp.dll` — one giant assembly. This means every script can reference every other script, circular dependencies accumulate silently, and domain reload (entering Play Mode) recompiles everything. On a mobile project that will grow to 100+ scripts, this becomes a 5–15 second reload on every change.

## Decision

> **Superseded in part:** the specific assembly list below (notably the single
> `SlapMania.Runtime` and `Presentation -> Core, Data` only) was refined into
> `Input` / `Presentation` / `UI` / `App` by [ADR-0010](ADR-0010-assembly-graph-refinement.md).
> The one-way-edge principle, the rationale and the alternatives here remain authoritative.

The project uses six assembly definitions with strict one-way dependency edges:

```
SlapMania.Core          (zero dependencies — interfaces, structs, enums)
SlapMania.Data          (depends on Core — ScriptableObjects, configs)
SlapMania.Gameplay      (depends on Core, Data — pure C# simulation)
SlapMania.Runtime       (depends on Core, Data, Gameplay — MonoBehaviours, bootstrap)
SlapMania.Presentation  (depends on Core, Data — visuals, camera, audio wiring)
SlapMania.UI            (depends on Core, Data — uGUI, screens)
SlapMania.Audio         (depends on Core, Data — mixer, beat clock)
SlapMania.Diagnostics   (depends on everything — debug overlay, recorder)
```

Edges are **one-way only**. `Gameplay` cannot reference `Presentation`. `Core` cannot reference anything. This is enforced by the `.asmdef` files and verified by the compiler — a circular reference is a build error, not a code review comment.

## Consequences

### Positive

- **Incremental compilation:** changing a Presentation script recompiles only Presentation and Diagnostics, not Gameplay or Core. Domain reload drops from seconds to milliseconds.
- **Testability:** `Core` and `Gameplay` have no MonoBehaviour dependency, so all gameplay math runs in EditMode tests without entering Play Mode.
- **Enforced architecture:** the dependency direction is the architecture. A developer cannot accidentally couple the damage formula to a UI component.
- **Strippable debug:** `Diagnostics` depends on everything but nothing depends on it. Removing it (via `defineConstraints`) removes all debug tooling with zero impact on the game.

### Negative

- **Boilerplate:** cross-assembly communication requires interfaces or events in `Core`, not direct method calls. This is more code for simple cases.
- **Refactoring friction:** moving a type between assemblies requires updating `.asmdef` references and potentially adding an interface.

### Risks

- If a module needs to reference something in a peer assembly (e.g., `Presentation` needs a type from `Audio`), the type must be extracted to `Core` or communicated via an event. This is a design pressure, not a bug.

## Alternatives considered

| Alternative | Why rejected |
|---|---|
| No assembly definitions | 5–15 s domain reload, no enforced boundaries, everything can reference everything |
| Two assemblies (Runtime + Editor) | Better than one, but no enforced Gameplay/Presentation separation |
| Assembly definition references with `allowUnsafeCode` workarounds | Fragile and defeats the purpose |
