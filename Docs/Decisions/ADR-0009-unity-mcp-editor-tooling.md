# ADR-0009 — Unity MCP bridge as build-excluded editor tooling

**Status:** Accepted (2026-09-26), **partially supersedes [ADR-0007](ADR-0007-no-third-party-packages.md)** (editor-only-tooling clause)
**Date:** 2026-09-26
**Deciders:** shobhit

## Context

We want an AI assistant (Claude Code, and any other MCP client) to drive the Unity
Editor directly: run EditMode/PlayMode tests, read the Console, and inspect or
modify scenes and assets from chat. The Model Context Protocol (MCP) is the wire
for this. The only mechanism that gives an MCP client live control of *this*
editor is an in-editor bridge, and the maintained implementation that fits our
toolchain is CoplayDev's **MCP for Unity** (`com.coplaydev.unity-mcp`): a Unity
package that opens a local stdio/TCP bridge, paired with a Python server the
client launches via `uvx`.

That bridge is a third-party, editor-only package — exactly the category
[ADR-0007](ADR-0007-no-third-party-packages.md) rejected. Its
"Alternatives considered" table closes the door by name:

> | Use packages in Editor only (Odin) | Editor-only packages still affect domain reload and can leak runtime dependencies through generated code |

So enabling Editor MCP is not a routine package add; it reverses a deliberate
decision. This ADR records that reversal, narrowly.

## Decision

Carve a **narrow, build-excluded exception** into ADR-0007 for *developer-only
editor tooling that no shipping assembly references and that never enters a player
build*. Under that exception we admit exactly one package today:

- `com.coplaydev.unity-mcp` @ `#v10.2.0`, pinned by tag (not `#main`).

Constraints that keep ADR-0007's intent intact:

1. **Editor-only.** The bridge's assemblies stay Editor-platform-only. No
   `SlapMania.*` runtime assembly (Core, Data, Gameplay, Input, Snapshot,
   Presentation, Audio, UI, Meta, App) may reference it. `SlapMania.Diagnostics`
   and `SlapMania.Editor` must not reference it either — it is a *tool*, not a
   project dependency.
2. **Never shipped.** It contributes zero bytes to the IL2CPP player / APK / IPA.
   If a build ever pulls it in, the exception is void and the package comes out.
3. **Pinned.** Track a tag; bump deliberately with a Changelog row below — never
   float `#main`.
4. **Not a precedent for runtime packages.** ADR-0007's ban on runtime
   third-party packages (DOTween, UniTask, Odin, NaughtyAttributes, …) stands
   unchanged. This exception is for build-excluded tooling only.

The MCP client side is a project-scoped `.mcp.json` at the repo root. **Amended
2026-09-26:** it connects over **HTTP to `http://127.0.0.1:8080/mcp`**, the server the
Unity Editor itself launches (`uvx --from "mcpforunityserver==10.2.0" mcp-for-unity
--transport http --http-url http://127.0.0.1:8080 --project-scoped-tools`, pidfile in
`Library/MCPForUnity/RunState/`). Prerequisites (`uv`, Python 3.10+) are per-developer,
not project assets.

### Amendment (2026-09-26) — HTTP transport, not a client-launched stdio server

The original `.mcp.json` launched its own stdio server (`uvx --from mcpforunityserver
mcp-for-unity --transport stdio`, **unpinned**). The v10.2.0 package's editor window
defaults to HTTP and starts its *own* server, so the Editor attached to that one and the
client's stdio server saw **0 instances** — every MCP tool call failed while both sides
looked healthy (DEBUG S-068). Decision: the client connects to the Editor-launched HTTP
server. It is the package's default path, it is already version-pinned to the package
(`==10.2.0`, which also closes the unpinned-PyPI gap above), and there is one server per
Editor instead of two. Cost: the Editor must be open with the session active *before* the
Claude session starts.

| Alternative | Why rejected |
|---|---|
| Keep client-launched stdio and switch the Unity window to stdio | Fights the package default on every machine, and the stdio server was unpinned |
| Register via the window's "Configure" button (`claude mcp add --scope local UnityMCP`) | Per-user, not in the repo; alongside the project `.mcp.json` it duplicates every tool |

## Consequences

### Positive

- **AI can drive the editor.** Run the Test Framework, read Console output, and
  edit scenes/assets from chat — a tighter loop than round-tripping through the CLI.
- **Nothing ships.** Editor-only means the runtime footprint ADR-0007 cares about
  (APK size, runtime supply chain) is unaffected.
- **Reversible.** Delete one manifest line and one `.mcp.json`; no runtime code
  ever depended on it.

### Negative

- **Domain-reload cost.** An extra editor assembly compiles and reloads — the
  precise cost ADR-0007 named. Accepted for the tooling benefit.
- **Dev-machine supply chain.** `uvx` pulls `mcpforunityserver` from PyPI and the
  package from GitHub. Pinning the Unity tag reduces, but does not remove, this.
- **Per-developer setup.** Each contributor needs `uv` + Python and must approve
  the MCP server in their client.

### Risks

- **Powerful surface.** The bridge lets an LLM edit scripts and run editor
  actions. Treat it like a shell: review what it does, and don't point untrusted
  MCP clients at it. The bridge port is loopback-only by default.
- **Upstream churn.** A fast-moving package may change the `?path=` or server
  entrypoint on a bump. Mitigated by the tag pin; verify on each bump.

## Alternatives considered

| Alternative | Why rejected |
|---|---|
| Keep ADR-0007 absolute | Forgoes AI editor control entirely; the build-excluded exception was judged worth its narrow cost |
| `mcp-unity` (CoderGamester, Node) | Requires Node, which is not installed; more moving parts than the `uv`/Python path already present |
| Headless CLI test runner only (`-runTests`, built-in Test Framework, zero packages) | Covers running tests but gives no live Console reads or scene/asset control — the reasons to want Editor MCP |
| Float `#main` | Cheaper updates, but contradicts ADR-0007's supply-chain stance; pinned tag chosen instead |

## Changelog

| Date | Change |
|---|---|
| 2026-09-26 | Initial. Admit `com.coplaydev.unity-mcp` @ `#v10.2.0` as build-excluded editor tooling. |
| 2026-09-26 | Amended: `.mcp.json` moved from a client-launched, unpinned stdio server to HTTP at `127.0.0.1:8080/mcp` (the Editor-launched, `==10.2.0`-pinned server). Fixes the 0-instances failure (DEBUG S-068). |
