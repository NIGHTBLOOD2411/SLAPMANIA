# Slap Mania - Claude toolchain

How Claude is wired into this repo. The rules live in [`../CLAUDE.md`](../CLAUDE.md); this
file is the operator's manual for the tooling that enforces and accelerates them.

## What's here

```
.claude/
  agents/        6 subagents - specialists that already know the rules & DEBUG rows
  commands/      7 slash commands - the everyday actions
  hooks/         2 Python hooks - advisory C# guard on save + session-end DoD reminder
  settings.suggested.json   proposed permissions + hooks (NOT active - see Activation)
  README.md      this file
../.mcp.json     Unity Editor MCP client config (server key: unityMCP) - ADR-0009
```

## Activation (one deliberate step)

`settings.suggested.json` is **not read by Claude Code**. Nothing here changes how the
agent behaves until you opt in:

1. Read `settings.suggested.json` and decide you're happy with the allow/ask lists and the
   two hooks.
2. Rename it to `.claude/settings.json` (committed, shared with the team).
3. Per-machine tweaks (a wider allow-list for your box, disabling a hook locally) go in
   `.claude/settings.local.json`, which is **gitignored** so secrets and personal choices
   never land in git.

Why suggested-not-active: a permission allow-list is the agent granting itself the right to
skip prompts, and hooks run commands on your machine. Those are your call to enable, not
Claude's to assume - so the file ships inert and you flip it on.

## Unity Editor MCP (installed, ADR-0009)

`com.coplaydev.unity-mcp@v10.2.0` is in `Packages/manifest.json` as **build-excluded editor
tooling** (never ships - [ADR-0009](../Docs/Decisions/ADR-0009-unity-mcp-editor-tooling.md)).
It lets Claude run the Test Framework, read the Console and inspect scenes/assets directly.

- Client config: [`../.mcp.json`](../.mcp.json). Server key **`unityMCP`**, **HTTP** to
  `http://127.0.0.1:8080/mcp` - the server the Unity Editor itself launches (MCP for Unity
  window -> Connect -> "Session Active"; it runs `uvx --from "mcpforunityserver==10.2.0"
  mcp-for-unity --transport http`). Tools are named `mcp__unityMCP__<tool>` (`read_console`,
  `find_gameobjects`, `execute_code`, `manage_editor`, `manage_scene`, ...). ADR-0009 amendment.
- **Do not** press "Configure" for Claude Code in the MCP for Unity window: it registers a
  second, local-scope server named `UnityMCP` next to this one and every tool appears twice.
- **Per-developer prereqs:** [`uv`](https://docs.astral.sh/uv/) + Python 3.10+ (Unity uses
  them to start the server), and you must approve the MCP server in your client on first run.
  The Unity Editor must be open with the session active before the Claude session starts;
  a client started first connects to nothing (0 instances) until it is restarted.
- **Driving Play Mode unattended:** an unfocused Editor stops ticking Play Mode
  (`Application.runInBackground` is false), so a bout started over MCP freezes at frame 2.
  Set `Application.runInBackground = true` at runtime for that session (never in Player
  Settings), or click into Unity. DEBUG S-069.
- In `settings.suggested.json` the read-only tools (`read_console`, `find_gameobjects`) are
  pre-allowed; the powerful ones (`execute_code`, `manage_*`) are in `ask` so they prompt -
  treat `execute_code` like a shell.

## Subagents (`agents/`)

| Subagent | Use it for |
|---|---|
| `perf-alloc-auditor` | R5 zero-alloc sweep of Charge/Timing + hot paths (read-only) |
| `core-math-tester` | write/extend EditMode tests with the R3 boundary matrix; keep gameplay pure C# |
| `doc-sync-guardian` | verify R1 - module docs synced with code before finishing |
| `urp-snapshot-doctor` | URP 17 / snapshot bugs (black, flipped, linear/sRGB, GLES3) - encodes DEBUG S-030..037 |
| `slap-debugger` | any gameplay bug; opens the DEBUG row first (R6) |
| `release-gatekeeper` | pre-ship go/no-go against RELEASE-CHECKLIST |

Claude picks these automatically when a task matches, or you can name one.

## Slash commands (`commands/`)

| Command | Does |
|---|---|
| `/run-tests [editmode\|playmode\|all]` | run the suite via MCP (or CLI fallback), summarize failures |
| `/perf-audit [path]` | run the allocation auditor over the hot paths |
| `/doc-check [M##]` | run the doc-sync guardian on the current change |
| `/log-bug <symptom>` | R6 - append the DEBUG row before investigating, then debug |
| `/new-adr <slug> - <title>` | R7 - scaffold the next ADR and wire the indexes |
| `/new-module <M##> <Name>` | scaffold a module doc + INDEX row (flags the asmdef R9 question) |
| `/ship-check` | R12 - the release gate |

## Hooks (`hooks/`)

Both are **advisory and fail-open** (a broken hook never blocks you) and need **Python 3**
on PATH.

- `cs_guard.py` (PostToolUse on Edit/Write/MultiEdit) - on saving a `.cs` under
  `Assets/_Project/Scripts` (except `Logging/`), flags `Debug.Log` (R4) and
  `UnityEngine.Input` usage as hard violations (exit 2, Claude self-corrects), and LINQ /
  `Find` / `Resources.Load` / `Camera.main` as advisory smells.
- `session_end.py` (Stop) - prints the Definition of Done checklist (R12) and lists changed
  script files whose module docs to re-check (R1).

Disable either by deleting its block from `settings.json`. On macOS, if `python` resolves
to Python 2 or is missing, change the command to `python3` in `settings.json`.

## Known drift (surfaced, not silently fixed)

`Docs/DEBUG.md` §8 lists a `Flow` log channel, but the code enum (`Scripts/Logging/LogChannel.cs`)
has **`Boot` + `Match`** instead of `Flow`. The subagents reference the code enum. Reconcile
the doc or the enum in a dedicated change (R1) - it wasn't touched here to avoid a silent
doc edit.

## Prereqs summary

| For | Need |
|---|---|
| MCP-driven test runs / editor control | `uv` + Python 3.10+, Editor open, server approved |
| Hooks | Python 3 on PATH |
| CLI test fallback | Unity `6000.3.22f1` (Editor closed during the run) |
