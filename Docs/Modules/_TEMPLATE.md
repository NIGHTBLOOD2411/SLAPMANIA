---
module: M0X-Name
status: Draft            # Draft | InProgress | Implemented | Stable | Deprecated
owner: shobhit
last-verified: YYYY-MM-DD
verified-against: <git short sha>
asmdef: SlapMania.<Assembly>
---

# M0X — <Module Name>

> **One-liner:** <a single sentence a stranger understands>

## 1. Purpose

Why this module exists. Two to four sentences. State the problem, not the solution.

## 2. Responsibilities and non-goals

**Owns**
- <the thing it is the single source of truth for>

**Explicitly does NOT do**
- <the thing people will assume it does> → owned by `M0Y`

## 3. Public API surface

Only what other modules may touch. Everything else is `internal`.

| Member | Signature | Notes |
|---|---|---|
| `ClassName.Method` | `ReturnType Method(Args)` | pure / allocates / main-thread-only |

**Consumed by:** M0A, M0B
**Stability:** stable | churning

## 4. Configuration

Asset: `Assets/_Project/Data/<Name>Config.asset`
Type: `SlapMania.Data.<Name>Config`

| Field | Type | Default | Sane range | Meaning / what breaks outside it |
|---|---|---|---|---|
| `fieldName` | float | 1.0 | 0.5 – 3.0 | <effect>; below 0.5, <symptom> |

**Invariants** (asserted on load and covered by `TuningInvariantTests`)
- <e.g. zone widths sum to exactly 1.0>

## 5. Events

**Emits**

| Event | Payload | When | Order guarantee |
|---|---|---|---|

**Consumes**

| Event | From | Reaction |
|---|---|---|

## 6. State machine

States, transitions, triggers. Delete this section if the module is stateless — and say so explicitly rather than leaving it blank.

```
Idle --Begin--> Active --Complete--> Resolving --> Idle
            \--Cancel-----------------------------/
```

| State | Entry | Exit | Timeout | Illegal transitions (assert) |
|---|---|---|---|---|

## 7. Dependencies

- **Hard (compile):** <asmdefs / types>
- **Soft (runtime, via event or interface):** <...>
- **Unity APIs:** <e.g. `InputSystem.EnhancedTouch`, `AsyncGPUReadback`>
- **Must initialize after:** M00, M0X

## 8. Failure modes

| Failure | Detection | Behaviour | Player-visible? |
|---|---|---|---|
| Config asset null | assert at `Awake` | fall back to hardcoded defaults, log Error | no |

## 9. Debug hooks

- **Log channel:** `LogChannel.<X>`
- **Overlay fields:** <what this module publishes to the debug HUD>
- **Cheats:** <buttons that touch this module>
- **Recorder events:** <event types emitted to `MatchRecorder`>
- **Editor tools:** <window / gizmo / inspector>
- **Gizmos:** <what is drawn, and under what toggle>

## 10. Tuning notes

Free-form prose. How it *feels*, what was tried and rejected, the "do not touch this without also touching X" warnings.

**This is the section that saves you in six months. Write prose, not bullets.**

## 11. Open questions

- [ ] <unresolved design question>

## 12. Changelog

| Date | Commit | Change |
|---|---|---|
| YYYY-MM-DD | `abc1234` | Created (Draft) |
