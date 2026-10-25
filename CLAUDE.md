# Slap Mania - AI Session Contract

Unity **6000.3.22f1** (Unity 6.3) - URP **17.3.0** - Linear color space -
**New Input System only** (`UnityEngine.Input` does not exist in this project) -
portrait mobile (Android ARM64 Vulkan-first + GLES3 fallback, iOS Metal) - IL2CPP -
.NET Standard 2.1 - Test Framework 1.6.0 - **zero third-party packages; do not add one
without an ADR.**

> This file is the contract - intentionally strict and intentionally short. The
> *reasoning* lives in `Docs/` and is linked throughout. When the contract and a doc
> disagree, the contract wins and the doc is stale - fix the doc (R1).
> New here? Read [`Docs/WORKFLOW.md`](Docs/WORKFLOW.md) once, then this file every session.

## Stack at a glance

| Area | Value | Source of truth |
|---|---|---|
| Engine | Unity `6000.3.22f1`, pinned - do not let the Hub upgrade it | `ProjectSettings/ProjectVersion.txt` |
| Render | URP `17.3.0`, **Linear** color space (required for the photo grade) | `Packages/manifest.json`, [BUILD-AND-RUN](Docs/BUILD-AND-RUN.md) |
| Input | New Input System `1.20.0` **only** - count taps from events, never per-frame polling | [M02-Input](Docs/Modules/M02-Input.md) |
| Scripting | IL2CPP, .NET Standard 2.1, managed stripping Minimal until release | [BUILD-AND-RUN](Docs/BUILD-AND-RUN.md) |
| Platforms | Android ARM64 (Vulkan -> GLES3), iOS Metal, **portrait only** | Player Settings |
| Identity | `com.dot9games.slapmania` / Dot9 Games | Player Settings |
| Tests | Test Framework `1.6.0` - EditMode green before every commit | [TESTING](Docs/TESTING.md) |
| Packages | No third-party **runtime** packages ([ADR-0007](Docs/Decisions/ADR-0007-no-third-party-packages.md)). One build-excluded **editor-tooling** exception: Unity MCP `com.coplaydev.unity-mcp@v10.2.0` ([ADR-0009](Docs/Decisions/ADR-0009-unity-mcp-editor-tooling.md)). Addressables+CCD adopted for M19 ([ADR-0008](Docs/Decisions/ADR-0008-addressables-and-ccd.md)), not yet installed | `Packages/manifest.json` |
| Thermals | `com.unity.modules.adaptiveperformance` present - budget for throttling | [DEBUG §6](Docs/DEBUG.md) |

## The game

Turn-based **multiversal slap tournament**. Each slap is two chained minigames:

1. **Charge phase** - rapid-tap (~2.5 s) to fill a **power** bar -> `power` in 0..1.
2. **Timing phase** - a **marker** ping-pongs across a bar of colored **zones**;
   tap to stop it. Red is the jackpot (x3.0).

`damage = power x multiplier`. Health <= 0 means that slap is a **finisher**.
Every slap captures a **snapshot** of the recipient; the victory screen shows the
**gallery**.

Vocabulary is fixed by `Docs/GLOSSARY.md`. Use those exact words in code and docs.

## Read before you write

1. [`Docs/WORKFLOW.md`](Docs/WORKFLOW.md) - how a task flows dev -> test -> debug -> polish -> ship, and the **Definition of Done**. Read once; it is the map for everything below.
2. [`Docs/ARCHITECTURE.md`](Docs/ARCHITECTURE.md) - assembly graph and the "life of one slap" sequence.
3. `Docs/Modules/M**-<Module>.md` - for every module you are about to touch. Treat the doc as a map, then read the actual code at the point of change (docs can drift; code is truth).
4. [`Docs/DEBUG.md`](Docs/DEBUG.md) - before debugging anything. The answer may already be there.

## How we work - the loop

Every task runs the same loop. The short version is here; the full version, with the
per-stage exit criteria, is [`Docs/WORKFLOW.md`](Docs/WORKFLOW.md).

```
plan -> implement -> test (R3) -> debug (R6) -> polish -> sync docs (R1) -> ship-gate (R12)
```

- **Gameplay before pixels.** Core/Gameplay run headless in the EditMode runner before a prefab exists. Prove the math there first ([TESTING](Docs/TESTING.md)).
- **Feel is a deliverable, not a nicety.** Hitstop, screen shake, audio lead and safe-area are tracked in [`Docs/POLISH.md`](Docs/POLISH.md). "It works" is not "it ships".
- **Definition of Done** (R12): tests green, docs synced, DEBUG row for any bug, perf re-captured if you touched a hot path, zero new alloc in Charge/Timing. Pre-release gate: [`Docs/RELEASE-CHECKLIST.md`](Docs/RELEASE-CHECKLIST.md).

## Non-negotiable rules

**R1 - Docs ship with code.**
Changing `Assets/_Project/Scripts/<Module>/**` means updating
`Docs/Modules/M**-<Module>.md` in the *same* change: the affected sections, plus
`last-verified` (today) and a Changelog row. Never batch doc updates for later.

**R2 - Gameplay math is pure C#.**
Damage, zone lookup, tap-rate sampling, marker position and the finisher test live
in `SlapMania.Core` as `static` or plain classes with **no** MonoBehaviour, no
`Time.*` read inside, no singletons. Time and config arrive as parameters.
If it cannot be unit-tested without entering Play Mode, it is in the wrong assembly.

**R3 - Every new Core rule gets an EditMode test** in
`Assets/_Project/Tests/EditMode/`, including boundaries: 0, 1, exact zone edges,
NaN, negative, and a huge `dt`.

**R4 - Debug tooling is stripped, not disabled.**
Debug-only methods carry `[Conditional("SLAPMANIA_DEBUG")]`. Debug-only types live
in `SlapMania.Diagnostics`, whose asmdef has Define Constraint `SLAPMANIA_DEBUG`, so
the assembly is not compiled at all in release. Never use `if (debugEnabled)` as the
only guard. Never call `Debug.Log` in gameplay code - use `SlapLog.<Channel>(...)`.

**R5 - Zero allocations per frame** during the Charge and Timing phases.
No LINQ, no string concatenation, no capturing lambdas, no `foreach` over an
interface-typed collection in those paths. Cache every `WaitForSeconds`.
Prefer `Awaitable` over nested coroutines.

**R6 - New bug found means `Docs/DEBUG.md` gets a row.**
Add to the Symptom -> Cause -> Fix table **before** you start investigating, with
`Cause: unknown`. Open a `KI-####` entry if it is not fixed the same day.
Appending takes 60 seconds; reconstructing it in six months takes a day.

**R7 - An architectural choice with a rejected alternative gets an ADR.**
New file in `Docs/Decisions/ADR-####-<slug>.md`. Do not bury reasoning in a commit.

**R8 - Timing is authored in milliseconds, never pixels or frames.**
Zone windows, charge duration and hitstop are all ms. Marker position is a pure
function of elapsed `double` time - never `pos += speed * dt`.

**R9 - Ask before:** adding a package, adding a singleton, adding a scene, changing
an asmdef graph edge, or changing anything in `ProjectSettings/`.

**R10 - Ask, with a recommendation and a concrete example.**
When a choice is ambiguous, or getting it wrong would change the work, **stop and
ask** - never build on a silent assumption. Every question carries (a) a
recommendation, (b) the reason, and (c) a **concrete example**: a `file:line`, an
asset field, a config value, or the exact output each option produces. A
recommendation with no example is an incomplete answer. Only trivial, reversible
details with an obvious convention (a local variable's name, import order) skip the
ask - and if the ambiguity survives one re-read, it is not trivial.

**R11 - Inspector and serialized values are unverified from code.**
Serialized array lengths, `[SerializeField]` defaults, prefab wiring, scene
references and ScriptableObject `.asset` numbers cannot be confirmed by reading
`.cs`. Never state such a value as "checked". Say what you derived it from and mark
it unverified, and confirm before you change behaviour that depends on it. This is
exactly why `TuningInvariantTests` loads the real `.asset` files - see
[TESTING](Docs/TESTING.md).

**R12 - Done means shippable.**
A change is not done when it compiles. It is done when: EditMode is green (R3), the
module doc is synced (R1), any new bug has a DEBUG row (R6), a perf-sensitive change
has a re-captured [`Perf/BASELINE`](Docs/Perf/BASELINE.md) entry, and nothing new
allocates in the Charge/Timing paths (R5). Full gate:
[`Docs/WORKFLOW.md` §Definition of Done](Docs/WORKFLOW.md). Release gate:
[`Docs/RELEASE-CHECKLIST.md`](Docs/RELEASE-CHECKLIST.md).

## Ask before you touch these

R9 in file terms. These paths are also gated in `.claude/settings.json` so the tools
prompt you; treat a prompt as the reminder to open an ADR (R7) or a design note, not
as a speed bump to click through:

- `ProjectSettings/**` - any change (quality, graphics APIs, defines, player settings).
- `Packages/manifest.json`, `Packages/packages-lock.json` - adding/removing/upgrading a package.
- `**/*.asmdef` - a new assembly or a new dependency edge **is** an asmdef graph change.
- A new `*.unity` scene, or a new runtime singleton.

## Agent tooling

This repo ships a Claude toolchain under `.claude/` - full guide in
[`.claude/README.md`](.claude/README.md).

- **Subagents** (`.claude/agents/`) - `perf-alloc-auditor`, `core-math-tester`,
  `doc-sync-guardian`, `urp-snapshot-doctor`, `slap-debugger`, `release-gatekeeper`.
  Delegate the matching specialised task to them; each one already knows the relevant
  rules and DEBUG rows.
- **Slash commands** (`.claude/commands/`) - `/run-tests`, `/doc-check`, `/perf-audit`,
  `/log-bug`, `/new-adr`, `/new-module`, `/ship-check`.
- **Hooks** (`.claude/hooks/`) - an advisory C# scan on save (flags `Debug.Log`,
  `UnityEngine.Input`, `GameObject.Find`, hot-path alloc smells) and a session-end
  Definition-of-Done reminder. Advisory, fail-open; see the README to disable.
- **Unity Editor MCP** - **installed & accepted** ([ADR-0009](Docs/Decisions/ADR-0009-unity-mcp-editor-tooling.md)):
  `com.coplaydev.unity-mcp@v10.2.0`, a build-excluded editor tool. It lets Claude run the
  Test Framework, read the Console and inspect scenes/assets from here - the **primary**
  path in [`/run-tests`](.claude/commands/run-tests.md); CLI batchmode is the fallback.
  Client config is [`.mcp.json`](.mcp.json) (server key `unityMCP`, HTTP to the server Unity
  starts at `127.0.0.1:8080/mcp` - open the Editor before the Claude session);
  each dev needs `uv` + Python 3.10+. It must **never** enter a player build (ADR-0009 §2) -
  `release-gatekeeper` checks this.

## Commits

`type(M0X): summary` - e.g. `feat(M04): ms-based zone windows`.
`type` is one of `feat | fix | chore | docs | perf | test | refactor`.
`M0X` is the module number, or `repo` for cross-cutting changes.

## Layout

```
Assets/_Project/Scripts/{Core,Data,Logging,Gameplay,Input,Snapshot,
                         Presentation,Audio,UI,Meta,App,Diagnostics,Editor}/
Assets/_Project/Tests/{EditMode,PlayMode}/
Assets/_Project/Data/            # ScriptableObject assets
Docs/                            # never under Assets/ - see ADR-0001
  WORKFLOW.md TESTING.md POLISH.md RELEASE-CHECKLIST.md   # the dev -> ship loop
  ARCHITECTURE CONVENTIONS DEBUG BUILD-AND-RUN TUNING-BIBLE ROADMAP INDEX
  Modules/  Decisions/  Perf/  Reference/
.claude/                         # the agent toolchain - see .claude/README.md
  settings.json  agents/  commands/  hooks/
AGENTS.md                        # pointer for non-Claude agents -> this file
```

Assemblies that exist: `Logging`, `Core`, `Data`, `Gameplay`, `Input`, `Presentation`,
`UI`, `App`, `Diagnostics` (`SLAPMANIA_DEBUG` only), `Editor`, `Tests.EditMode`. The rest are documented ([INDEX](Docs/INDEX.md)) and
built in ROADMAP order.

**Design direction:** the V2 mockups, adopted one mechanic at a time
([ADR-0011](Docs/Decisions/ADR-0011-adopt-v2-design-prototype-first.md)) - a V2 word enters
code only with its module and a GLOSSARY update. **Play it:** `Tools > Slap Mania > Prototype >
Build Playable Prototype`, then Play ([BUILD-AND-RUN](Docs/BUILD-AND-RUN.md)). Tune everything
from the **Tuning Hub** Inspector.

## Session end

Before finishing, state which docs you updated. If you updated none, say why. Then
run the Definition of Done (R12) - the session-end hook prints the checklist. If you
touched `Assets/_Project/Scripts/<Module>/`, the matching module doc must be in the
same change (R1); if you could not, say so explicitly so it is not silently skipped.
