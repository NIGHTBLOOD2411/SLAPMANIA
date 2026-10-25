# ADR-0010 — Assembly graph refinement (Input / Presentation / UI / App split)

**Status:** Accepted
**Date:** 2026-09-26
**Deciders:** shobhit

## Context

[ADR-0002](ADR-0002-assembly-graph.md) established one-way assembly edges but described a
coarse set of assemblies, collapsing every MonoBehaviour (bootstrap, input, views, HUD)
into a single **`SlapMania.Runtime`** and giving `SlapMania.Presentation` only
`Core, Data` dependencies.

Since then the rest of the documentation evolved past that shape without ADR-0002 being
updated, and the two now contradict each other — the exact drift the contract warns about:

- [ARCHITECTURE.md §2](../ARCHITECTURE.md) and [CONVENTIONS.md](../CONVENTIONS.md) both
  describe a **finer split**: separate `Input`, `Snapshot`, `Audio`, `Presentation`, `UI`,
  `Meta`, `App`, `Diagnostics`, `Editor` assemblies, with `App` as the composition root and
  `Presentation` depending on `Gameplay`.
- The `Scripts/<Assembly>/` folder rule and the namespace table in CONVENTIONS.md assume the
  finer names and do **not** list `SlapMania.Runtime`.
- Module front-matter is mixed: `M00` and `M02` named `SlapMania.Runtime`, while `M08`
  already named `SlapMania.Presentation`.

The first MonoBehaviour code (P5) cannot be written until this is resolved, because it
decides how many asmdefs exist and which edges they carry — and a new asmdef edge is an
`R9` "ask first" change.

## Decision

Adopt the **finer split** from ARCHITECTURE.md §2 and CONVENTIONS.md as the authoritative
assembly graph, and treat ADR-0002's `SlapMania.Runtime` consolidation as **superseded**.
ADR-0002's core principle — one-way edges enforced by asmdefs, not discipline — stands
unchanged; only the assembly *list* is refined.

MonoBehaviours are split by concern rather than pooled:

```
SlapMania.Input        -> Core, Logging, Unity.InputSystem
SlapMania.Presentation -> Core, Data, Gameplay, Logging      (+ Snapshot, Audio, URP, Timeline when they land)
SlapMania.UI           -> Core, Data, Gameplay, Logging, UnityEngine.UI   (+ TMP when text lands)
SlapMania.App          -> all SlapMania.* + Unity.InputSystem  (composition root only)
```

The remaining assemblies named in ARCHITECTURE.md (`Snapshot`, `Audio`, `Meta`,
`Diagnostics`, `Editor`) are created when their modules are built; this ADR does not
pre-create empty assemblies.

`SlapMania.App` is the composition root: it holds `GameRoot` (the one singleton, M00),
`UnityClock`, and `BoutController`, and it is the only assembly that references the
presentation/input/UI layers together.

## Consequences

### Positive

- **The docs stop contradicting themselves.** One graph, matching the folder and namespace
  conventions already in force.
- **Finer incremental compilation** than the pooled `Runtime` — editing a HUD script
  recompiles `UI` + `App`, not input and presentation too.
- **`Presentation` may depend on `Gameplay`**, which the bout driver and views need to read
  meters and health; ADR-0002's `Presentation -> Core, Data` only edge would have blocked it.

### Negative

- **More asmdefs, more upfront boilerplate** — four new assembly files for P5 instead of one,
  each with its reference list to maintain.

### Risks

- If two peer assemblies later need to share a type (e.g. `Presentation` and `Audio`), it
  must be hoisted to `Core` or passed via a `Core` signal — the same design pressure
  ADR-0002 already documented, now spread across more assemblies.

## Alternatives considered

| Alternative | Why rejected |
|---|---|
| Keep ADR-0002's single `SlapMania.Runtime` | Contradicts ARCHITECTURE.md, CONVENTIONS.md, the folder rule, the namespace table and M08; would require rewriting more docs than it saves, and loses the `Presentation -> Gameplay` edge the views need |
| Leave both graphs in the docs, pick per-file | The contradiction is exactly what causes silent coupling bugs; a graph must be singular |
| Pre-create every assembly from ARCHITECTURE.md now (Snapshot, Audio, Meta, …) | Empty assemblies with no code are noise; create them with their modules |
