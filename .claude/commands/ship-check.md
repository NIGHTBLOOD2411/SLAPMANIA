---
description: R12 - run the pre-ship release gate and report go/no-go before tagging a build.
---

Use the **release-gatekeeper** subagent to walk `Docs/RELEASE-CHECKLIST.md` and return a
**go / no-go** table.

Optional focus: `$ARGUMENTS` (e.g. a section like `secrets` or `perf`).

It should machine-verify what it can from here (define stripping / `BuildVerifyPreprocessor`,
MCP build-exclusion per ADR-0009 §2, no committed secrets in the repo or `.claude/settings*`,
keystores not committed, a fresh dated `Docs/Perf/BASELINE.md` capture, LFS, version bump)
and mark the rest `NEEDS-DEVICE` (player settings in `ProjectSettings/`, the GLES3 snapshot
smoke test, on-device perf). Every **must** row must be green for Go; any red = No-Go, and
it names exactly what to fix. It reports only - it does not tag, build, or push.
