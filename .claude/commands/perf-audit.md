---
description: Audit hot-path code for R5 zero-allocation violations (Charge/Timing and any per-frame path).
argument-hint: "[path or module, e.g. Scripts/Gameplay/PowerMeter.cs]"
---

Use the **perf-alloc-auditor** subagent to sweep for R5 allocation violations.

Scope: `$ARGUMENTS` if given; otherwise the default hot paths - `Scripts/Gameplay/`
(`PowerMeter`, `MarkerOscillator`, `TimingMeter`, tap sources, `MatchDirector.Tick`),
`Scripts/Input/`, and any view polling that runs during a charge or timing phase.

Have it return a worst-first table (`file:line | pattern | why it allocates | fix`) and a
verdict on whether the hot path is clean. It reports only; I decide what to change. If it
finds a real allocation, the fix belongs in the same change as a re-run of the PlayMode
0-byte allocation test (`/run-tests playmode`).
