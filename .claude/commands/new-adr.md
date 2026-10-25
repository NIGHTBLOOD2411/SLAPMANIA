---
description: R7 - scaffold the next ADR from the template with the correct number, then wire it into the indexes.
argument-hint: "<slug> - <title>   e.g. burst-for-damage-batch - Use Burst for the damage batch"
---

Create a new Architecture Decision Record. Input: `$ARGUMENTS` (`<slug> - <title>`).

1. Glob `Docs/Decisions/ADR-*.md`, find the highest number, add 1 -> `ADR-####`.
2. Copy the template @Docs/Decisions/_TEMPLATE.md into
   `Docs/Decisions/ADR-####-<slug>.md`. Fill: title, `Status: Proposed` (do NOT mark
   Accepted yourself - that is the human's call, R10), `Date` today, `Deciders: shobhit`.
3. Draft Context / Decision / Consequences (Positive, Negative, Risks) / Alternatives
   considered - the **Alternatives** table with a real rejected option is the whole point
   of an ADR; a decision with no rejected alternative probably does not need one.
4. Add a row to the ADR tables in `Docs/INDEX.md` (§Decisions) and `Docs/DEBUG.md` (§4).
   If this ADR amends an earlier one, update that ADR's Status line too (see how ADR-0007
   references 0008 and 0009).
5. Show me the draft and the index edits for review. Leave `Status: Proposed` until I say
   otherwise; if it changes a gated path (package / asmdef / ProjectSettings, R9), the
   package/asmdef edit happens only after the ADR is Accepted.
