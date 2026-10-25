# Slap Mania - Development Workflow

Status: Draft - Last verified 2026-09-26

The one loop every task runs, from "pick it up" to "it ships". [CLAUDE.md](../CLAUDE.md)
is the contract; this is how you *execute* it. If you read one process doc, read this one.

---

## 0. The loop

```
  pick up  ->  plan  ->  implement  ->  test  ->  debug  ->  polish  ->  sync docs  ->  ship-gate
   (§1)      (§2)       (§3)          (R3)     (R6)       (§6)        (R1)          (R12)
```

Nothing is "done" at *implement*. The three stages after it - test, debug, polish -
are where a slap goes from *computed correctly* to *feels like a slap*. Skipping them
is the difference between a prototype and a product.

---

## 1. Picking up a task

1. **Find the module.** Everything is numbered M00-M19 ([INDEX](INDEX.md)). The number
   is the data-flow order through one slap, so "where does this live" is usually obvious.
2. **Read the module doc as a map, then trust the code.** Open `Docs/Modules/M**.md`
   for orientation - components, lifecycle, integration points. Then read the actual
   `.cs` at the point of change. The doc can lag; code is truth (R1 keeps the gap small,
   never zero).
3. **Check status.** [INDEX](INDEX.md) and [ROADMAP](ROADMAP.md) carry a status ladder:
   `Draft -> InProgress -> Implemented -> Stable -> Deprecated`. A `Draft - stub` module
   (M14/M15/M17/M18) has **verified constraints and open questions, not a design** -
   starting one is a design pass, not a coding task. Surface that (R10) before writing code.
4. **Scope it out loud.** "Fix the bug" and "fix the bug + add the guard + the DEBUG row"
   are different sizes. State the recommended scope and let the human confirm (R10).

---

## 2. Plan

- **Gameplay before pixels.** If the change is a rule (damage, zone lookup, marker
  motion, finisher, tap sampling, AI selection), it lives in `SlapMania.Core` as pure C#
  (R2) and is provable in the EditMode runner with **no scene, no prefab, no Play Mode**.
  Plan the test first (R3) - it is faster than the Editor and it is the spec.
- **Predict, branch, commit.** Damage is *computed* in one state (`DamageResolver.Resolve`,
  mutates nothing), *branched on* by the FSM (finisher or normal), and *applied* at the
  impact frame (`Commit`). If your plan mutates health during resolution, it is wrong -
  see [ARCHITECTURE §4](ARCHITECTURE.md).
- **One code path for player and AI.** `AiTapSource` feeds the same meters the human
  drives. A change that special-cases the AI is a smell; retune the shared curve instead.
- **Does it need an ADR?** A choice with a real rejected alternative gets one *before* the
  code (R7). Use [`/new-adr`](../.claude/commands/new-adr.md).
- **Does it touch a gated path?** Package, scene, singleton, asmdef edge, `ProjectSettings/`
  -> stop and ask (R9). The tools will prompt you; that prompt is the reminder to write
  the ADR, not a click-through.

---

## 3. Implement

Follow [CONVENTIONS.md](CONVENTIONS.md) to the letter - it is not preference, it is what
keeps the assembly graph one-way and the hot paths allocation-free. The five that bite:

- `_camelCase` privates; **no public fields on MonoBehaviours**; `internal` by default.
- ScriptableObjects are **read-only at runtime**; mutate a runtime copy, never the asset.
- Async is Unity 6 `Awaitable`, not coroutines, not UniTask. The FSM stays synchronous
  (`Tick(dt)`); async lives in `Presentation`.
- Logging is `SlapLog.Trace/Info/Warn(LogChannel.X, ...)` - **never** `Debug.Log`
  (R4). The channels that exist in code today are `Boot, Match, Input, Power, Timing,
  Damage, Health, AI, Presentation, Snapshot, UI, Audio, Save, Perf`.
- Time: `double` elapsed seconds for anything continuous, **ms** for authored windows
  (R8); presentation tweens on **unscaled** time; audio on `AudioSettings.dspTime`.

Keep the assembly boundaries honest: `Gameplay` must not reference `Input`, `UI`,
`Presentation` or URP. The answer to "I need that over here" is a new interface in
`Core` or a new signal on `MatchSignals` - see [ADR-0002](Decisions/ADR-0002-assembly-graph.md).

---

## 4. Test (R3)

Full strategy in [TESTING.md](TESTING.md). The gate for *this stage*:

- Every new/changed Core rule has an EditMode test with the boundary matrix: **0, 1,
  exact zone edges, NaN, negative, huge `dt`**.
- `EditMode` is green. Run [`/run-tests`](../.claude/commands/run-tests.md).
- If you touched a hot path, the PlayMode zero-alloc test still passes (0 bytes).
- If you touched a `.asset` tunable, `TuningInvariantTests` still passes.

A rule without a test is not implemented, it is drafted.

---

## 5. Debug (R6)

The inversion that saves days: **write the symptom row before you investigate.**

1. Add a row to [DEBUG.md §2](DEBUG.md) with `Cause: unknown` and `Status: Confirmed`.
   Use [`/log-bug`](../.claude/commands/log-bug.md).
2. Work the [first-response checklist](DEBUG.md#1-first-response-checklist): is
   `SLAPMANIA_DEBUG` on? Editor or device-only? Open the overlay; dump the match JSON.
3. Most of the symptom table is *predicted* failure modes - your bug may already be
   S-0XX with a diagnostic and a fix. Delegate deep rendering bugs to
   [`urp-snapshot-doctor`](../.claude/agents/urp-snapshot-doctor.md), gameplay bugs to
   [`slap-debugger`](../.claude/agents/slap-debugger.md).
4. Fix it, fill in cause + fix, and open a `KI-####` if it cost more than an hour.

Never delete a DEBUG row. Mark it Resolved. A wrong prediction is data, not embarrassment.

---

## 6. Polish

Feel is the product. [POLISH.md](POLISH.md) is the checklist; the essentials:

- **Hitstop, shake, impact stack** land on the impact frame and read as one hit, not three.
- **Audio leads the visual** by >=180 ms of scheduling (Android output latency is
  50-150 ms) - see [M11](Modules/M11-Audio.md).
- **Unscaled tweens only** - a scaled tween during a 0.02 hitstop is the #1 "why is my
  UI frozen" bug (S-041).
- **Safe area** applied on every full-screen canvas; the tap zone must clear the iOS
  home indicator (S-063).
- **First-slap hitch** warmed up (shader variants, VFX pool, snapshot RT) - S-044.

---

## 7. Sync docs (R1) & the Definition of Done

### Definition of Done

A change is done when **all** of these are true. This is the R12 gate; the session-end
hook prints it.

- [ ] **Compiles** with no new warnings in the touched assemblies.
- [ ] **EditMode green** (R3), including the new boundary tests.
- [ ] **PlayMode**: full match reaches `MatchEnd` with no `LogType.Error`; the
      Charge/Timing zero-alloc test reads **0 bytes** (R5).
- [ ] **Module doc synced** in the *same* change (R1): affected sections + `last-verified`
      today + a Changelog row. If code and doc drifted, [`/doc-check`](../.claude/commands/doc-check.md).
- [ ] **DEBUG row** exists for any bug found (R6); ADR exists for any real design choice (R7).
- [ ] **Perf re-captured** in [Perf/BASELINE](Perf/BASELINE.md) if you touched a hot path,
      snapshot, audio DSP, or canvas rebuild path.
- [ ] **No gated path changed silently** (R9): package/scene/singleton/asmdef/ProjectSettings.
- [ ] **Unverified values called out** (R11): anything you inferred from an Inspector/asset
      value is marked unverified, not stated as checked.

### Session end

State which docs you updated. If none, say why. If you touched
`Assets/_Project/Scripts/<Module>/` and could not update its doc, say so explicitly -
a silent skip is the one failure mode R1 exists to prevent.

---

## 8. Which tool for which job

| You are... | Use |
|---|---|
| adding/adjusting a Core rule + its tests | [`core-math-tester`](../.claude/agents/core-math-tester.md), then [`/run-tests`](../.claude/commands/run-tests.md) |
| worried about a frame spike / GC in Charge or Timing | [`/perf-audit`](../.claude/commands/perf-audit.md) -> [`perf-alloc-auditor`](../.claude/agents/perf-alloc-auditor.md) |
| chasing a black/flipped/missing snapshot | [`urp-snapshot-doctor`](../.claude/agents/urp-snapshot-doctor.md) |
| chasing any gameplay bug | [`/log-bug`](../.claude/commands/log-bug.md) -> [`slap-debugger`](../.claude/agents/slap-debugger.md) |
| checking docs are synced before a commit | [`/doc-check`](../.claude/commands/doc-check.md) -> [`doc-sync-guardian`](../.claude/agents/doc-sync-guardian.md) |
| about to tag a build | [`/ship-check`](../.claude/commands/ship-check.md) -> [`release-gatekeeper`](../.claude/agents/release-gatekeeper.md) |
| starting a new module or decision | [`/new-module`](../.claude/commands/new-module.md), [`/new-adr`](../.claude/commands/new-adr.md) |

---

## Changelog

| Date | Change |
|---|---|
| 2026-09-26 | Created. Encodes the dev -> ship loop, Definition of Done (R12), tool routing. |
