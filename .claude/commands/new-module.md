---
description: Scaffold a new module doc from the template and wire it into INDEX. Reminds that a new assembly is an asmdef graph change (R9).
argument-hint: "<M##> <ModuleName>   e.g. M20 Replays"
---

Scaffold a module doc. Input: `$ARGUMENTS` (`<M##> <ModuleName>`).

1. Copy the template @Docs/Modules/_TEMPLATE.md into `Docs/Modules/M##-<ModuleName>.md`.
   Fill the YAML front-matter (`status: Draft`, owner, `last-verified` today) and the
   section skeleton (overview, public API surface, components, lifecycle, integration
   points, open questions). Do **not** invent a design for a stub - carry the verified
   engine constraints and the open questions honestly (see how M14/M15/M17/M18 read).
2. Add the module's row to the Modules table in `Docs/INDEX.md` with matching status, and
   to `Docs/ROADMAP.md` in build order.
3. **Stop and flag (R9):** if this module needs its own assembly, that is a new
   `*.asmdef` and a new edge in the assembly graph - it requires a decision, and likely an
   ADR that references [ADR-0002](../../Docs/Decisions/ADR-0002-assembly-graph.md). Do not
   create the asmdef silently; recommend the placement (which existing assembly, or a new
   one and why) with a concrete example (R10) and let me approve.
4. Show me the new doc + index edits for review.
