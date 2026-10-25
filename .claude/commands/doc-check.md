---
description: Verify R1 doc-sync for the current change - module docs updated, dates bumped, INDEX status matches. Reports gaps, does not silently edit.
---

Use the **doc-sync-guardian** subagent to check R1 compliance for the work in progress.

Scope: the files changed this session (or `git diff --name-only` if this is a repo).
Optional focus: `$ARGUMENTS` (a module number like `M05`, or a path).

Have it report, per implicated `Docs/Modules/M**.md`: `SYNCED` or `STALE - <what's
missing>` (affected sections, `last-verified` date not today, missing Changelog row, or an
`Docs/INDEX.md` status mismatch), plus R6 (DEBUG row) and R7 (ADR) reminders. It should
recommend the smallest closing edit with a concrete example (R10) and let me decide whether
to apply - do not auto-edit docs.
