# Slap Mania - Testing

Status: Draft - Last verified 2026-09-26

Why this project can be tested harder than most Unity games: the simulation is
**headless** (R2/[ADR-0003](Decisions/ADR-0003-gameplay-math-is-pure-csharp.md)). The
entire duel - power, timing, damage, health, AI, the FSM - runs in the EditMode Test
Runner with no scene, no camera, no Play Mode. That is where the balance bugs live, so
that is where the tests live.

---

## 1. The pyramid

| Layer | Assembly under test | Runner | Speed | Fails the build? |
|---|---|---|---|---|
| **Core math** | `Core`, `Gameplay` (pure) | EditMode | < 1 s | Yes - green before every commit |
| **Tuning invariants** | real `Data/*.asset` files | EditMode | < 1 s | Yes |
| **Integration** | full `MatchDirector` with `NullPresentation` | EditMode | seconds | Yes |
| **Fuzz / determinism** | seeded 10k-run duels | EditMode | seconds | Yes |
| **Allocation** | Charge/Timing path in a built player | PlayMode | seconds | Yes - **fails at > 0 bytes** |
| **Full-match smoke** | scripted match to `MatchEnd` | PlayMode | < 60 s | Yes - no `LogType.Error` |
| **On-device** | GLES3 snapshot, perf capture | build + hardware | minutes | Gate at milestones ([RELEASE](RELEASE-CHECKLIST.md)) |

The top four are the daily loop. They are fast because `Core`/`Gameplay` have no
MonoBehaviour dependency. Keep them that way (R2) and the loop stays under a second.

Existing EditMode files (mirror their structure):
`PowerMeterTests`, `MarkerOscillatorTests`, `TimingZoneLayoutTests`, `DamageResolverTests`,
`HealthTrackerTests`, `MatchDirectorTests`, and shared `TestFactories`. `CssPaintTests.cs` holds
`CssColorTests`, `CssLengthTests`, `CssPaintParserTests` and `PaintRasterTests`. They pin the
CSS semantics behind the front end's baked paint (Core.Paint, M10): stop fix-up, gradient-line
length, premultiplied interpolation, clip coverage and box-shadow alpha, with the R3 boundaries
(NaN, negative, huge, exact stop edges).

**Front-end fidelity** is a visual check, not an EditMode test:
- render each mockup with headless Edge;
- capture the same screen from Unity;
- score the pair with `Tools/mockup2uxml/compare.py` (BUILD-AND-RUN, "The front end").

---

## 2. R3 - the boundary matrix

Every new Core rule gets an EditMode test, and every such test covers the boundaries,
not just the happy path. The bugs in this genre live at the edges:

| Boundary | Why it bites here |
|---|---|
| **0** | empty charge, marker at rest, zero damage floor |
| **1** | full power, marker at the far wall, exact overcharge cap |
| **exact zone edges** | is the boundary red or yellow? off-by-epsilon ships a wrong jackpot |
| **NaN** | a bad config or a divide can send NaN into damage; assert it never reaches health |
| **negative** | negative `dt`, negative power from a decay underflow |
| **huge `dt`** | a device stall or a breakpoint resume must not teleport the marker past the red band (S-010) |

If a rule takes `dt` or a normalized value, its test file has all six or it is not done.

---

## 3. Naming

`Subject_Scenario_Expectation`, matching the existing suite and the ones DEBUG.md
already names as regression guards:

```
MarkerOscillator_At10000Seconds_NoDrift          // S-012: float accumulation
MatchDirector_WithNullPresentation_CompletesFullDuel
MatchDirector_FuzzedDuel_TerminatesWithin40Turns // S-023: 10k seeded runs
FinisherRule_ExactZeroHealth_IsFinisher          // S-021: the <=0 after clamp trap
DamageResolver_RedZoneFullPower_MatchesTable
```

A test's name is documentation. When a DEBUG row is fixed, its `Regression guard` field
should point at a test by exactly this name.

---

## 4. Determinism & replay - the payoff for the constraints

R2 (pure math) + the seeded-`IRandom` rule (S-024) buy something rare: a match is
**reproducible from a header**. A recording carries the `matchSeed`, the `configHashes`
and the tap timestamps ([DEBUG §9](DEBUG.md)). `ReplayDriver` re-feeds them and the
duel plays out identically.

- **Never** use `UnityEngine.Random` in gameplay - it is global static state and it
  breaks replay. Every draw goes through the injected `IRandom`.
- A determinism test seeds two `IRandom`s identically, runs two duels, and asserts the
  event streams are byte-identical.
- `configHashes` means a recording from before a balance change is **not** silently
  compared against current behaviour - assert the hash matches before replaying.

---

## 5. Tuning invariants - catching *designer* mistakes

`TuningInvariantTests` loads the real `.asset` files (this is R11 made executable - the
only way to verify Inspector values is to load them) and asserts:

- zone widths sum to `1.0 ± 1e-5` (a table that does not tile is a silent jackpot bug);
- multipliers are strictly ordered;
- the narrowest zone is `>= 40 ms` of travel (below that it is unhittable on a 30 fps
  low-end device - S-010);
- max single-slap damage `< starting HP` (else a lucky first slap ends the match, and
  `MatchDirector_FuzzedDuel` will also catch it).

No amount of unit-testing the *code* catches a bad *number*. These do.

---

## 6. The allocation test (R5)

The most input-sensitive windows in the game are Charge and Timing; a GC spike there is
felt as a dropped tap. A PlayMode test measures managed allocations across a full charge
phase and **fails at anything above 0 bytes**.

```csharp
var before = GC.GetTotalAllocatedBytes(precise: true);
// ... drive a full charge phase via a scripted ITapSource ...
var delta = GC.GetTotalAllocatedBytes(precise: true) - before;
Assert.That(delta, Is.EqualTo(0), $"Charge phase allocated {delta} B - see R5 / CONVENTIONS §Performance");
```

**In EditMode, use the Test Framework's meter, with a positive control** (DEBUG S-080: in the
Editor's Mono, `GC.GetAllocatedBytesForCurrentThread()` reads 0 whatever you allocate, so a
delta test passes vacuously). The pure Charge/Timing path is covered this way in
`MatchPauseTests`:

```csharp
using UnityEngine.TestTools.Constraints;               // the Is.Not extension
using Is = UnityEngine.TestTools.Constraints.Is;

Assert.That(() => { GC.KeepAlive(new object[4]); }, Is.AllocatingGCMemory(), "meter is blind");
Assert.That(() => { /* ticks + taps inside one phase */ }, Is.Not.AllocatingGCMemory());
```

Keep phase *transitions* out of the measured block: `PowerMeter.Begin` / `TimingMeter.Begin`
subscribe a method group (one delegate per phase start), which is not a per-frame cost.

When it fails, the culprit is almost always in [CONVENTIONS §Performance](CONVENTIONS.md):
LINQ, string concat, a capturing lambda, `foreach` over an interface, `params`, an
uncached `WaitForSeconds`, `GetComponent` after `Awake`, or `Resources.Load` on a
gameplay path. Run [`/perf-audit`](../.claude/commands/perf-audit.md) to find it.

---

## 7. Running the tests

### In the Editor (canonical)
`Window > General > Test Runner` -> EditMode -> Run All. Sub-second; use it constantly.

### Headless (CLI) - the fallback path (CI, closed Editor)

The Editor must be **closed** (a running Editor holds the project lock). Adjust the Hub
path to your machine:

```bash
"C:/Program Files/Unity/Hub/Editor/6000.3.22f1/Editor/Unity.exe" \
  -runTests -batchmode \
  -projectPath "C:/Unity/SlapMania 2.0" \
  -testPlatform EditMode \
  -testResults "Logs/editmode-results.xml" \
  -logFile "Logs/editmode-run.log"
```

- Do **not** pass `-quit` with `-runTests` (it races the runner).
- Results are NUnit XML at `Logs/editmode-results.xml`; the process exit code is
  non-zero if any test fails.
- Swap `-testPlatform PlayMode` for the allocation + full-match suite (slower; needs a
  standalone or a play-mode context).

### The fast path (MCP - primary)

Unity Editor MCP (`com.coplaydev.unity-mcp@v10.2.0`, [ADR-0009](Decisions/ADR-0009-unity-mcp-editor-tooling.md))
runs the suite from this session and streams the Console back - no project-lock dance,
no XML parsing. This is the **primary** path when the Editor is open; the CLI above is the
fallback for CI and closed-Editor runs. `.mcp.json` at the repo root launches server
`unityMCP` via `uvx` (each dev needs `uv` + Python 3.10+). Run tests through the MCP's
`execute_code` against the Test Framework, and read failures with `read_console`.

---

## 8. What must never be untested

- The finisher test (`FinisherRule.IsFinisher`) - exact-zero and overkill (S-021).
- Zone resolution at exact edges (S-014, the free-Perfect-on-frame-1 trap).
- `armDelay` swallowing the carried-over mash finger (S-013) - the #1 genre bug.
- Match termination under fuzzing (S-023) - `maxTurns = 40` sudden-death backstop.
- Marker position vs `double` elapsed time over a long session (S-012).
- Zero allocation across a charge phase (R5).

If you are about to change any of these, the test comes first. If a test for one does
not exist yet, writing it is part of the change.

---

## Changelog

| Date | Change |
|---|---|
| 2026-09-26 | Created. Pyramid, R3 boundary matrix, determinism, tuning invariants, alloc test, runners. |
| 2026-10-19 | Core.Paint suites (M10/ADR-0013) and the front-end fidelity check. EditMode 116/116. |
| 2026-10-20 | §6: the EditMode allocation meter (`Is.Not.AllocatingGCMemory` + positive control) after `GC.GetAllocatedBytesForCurrentThread` read 0 in the Editor (S-080). New suites `PausableClockTests`, `MatchPauseTests` (M01 pause/forfeit). |
