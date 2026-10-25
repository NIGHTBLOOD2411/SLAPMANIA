# AGENTS.md

Rules for AI coding agents working in this repo live in **[CLAUDE.md](CLAUDE.md)** - the
single source of truth: the contract (R1-R12), the stack, the dev -> ship loop, and the
tooling. Read it first, every session. Do not duplicate rules here; link to CLAUDE.md so
there is exactly one place that can be wrong.

## By agent

- **Claude Code** auto-loads `CLAUDE.md` and the `.claude/` toolchain (subagents, slash
  commands, hooks). See [`.claude/README.md`](.claude/README.md).
- **Cursor** - mirror the essentials into `.cursor/rules/*.mdc` or `.cursorrules`.
- **GitHub Copilot** - `.github/copilot-instructions.md`.
- **Codex / Aider / others** - per-tool rule file; point it at `CLAUDE.md`.

None of those mirror files exist yet. If the team standardizes on one, create it and
mirror **at least** these, which cause the most damage when an agent does not know them:

- **R1** - docs ship with code (module doc updated in the same change).
- **R2 / R8** - gameplay math is pure C#; timing authored in ms, marker is a pure
  function of `double` elapsed time.
- **R10** - ask with a recommendation + a concrete example; never build on a silent
  assumption.
- **R11** - Inspector / serialized / `.asset` values are unverified from code; say so.

## Deeper process docs

[WORKFLOW](Docs/WORKFLOW.md) · [TESTING](Docs/TESTING.md) · [POLISH](Docs/POLISH.md) ·
[RELEASE-CHECKLIST](Docs/RELEASE-CHECKLIST.md) · [DEBUG](Docs/DEBUG.md) ·
[ARCHITECTURE](Docs/ARCHITECTURE.md) · [CONVENTIONS](Docs/CONVENTIONS.md)
