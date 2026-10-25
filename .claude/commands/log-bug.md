---
description: R6 - append a Docs/DEBUG.md symptom row (Cause unknown) BEFORE investigating, then optionally hand to slap-debugger.
argument-hint: "<one-line symptom>"
---

Enforce R6's inversion: write the symptom down **before** investigating, because fixing it
erases the memory of what it looked like.

Symptom: `$ARGUMENTS`

1. Open `Docs/DEBUG.md` §2 and add a row to the correct sub-table (Input / Timing meter /
   Damage, health, flow / Snapshot / Presentation / Audio / Build) using its columns:
   `ID | Symptom | Likely cause | Diagnostic | Fix | Module | Status`. Set the next
   `S-0xx` id, fill Symptom, leave `Likely cause: unknown`, set `Status: Confirmed`, and
   guess the Module if you can.
2. Show me the row you added and ask if the sub-table/module is right (R10) before digging.
3. Then hand off to the **slap-debugger** subagent (for gameplay) or **urp-snapshot-doctor**
   (for rendering) to investigate.
4. When fixed: fill in the cause + fix in the row (never delete it - mark Resolved), point
   its regression guard at a test, and open a `KI-####` in §3 if it cost more than an hour.
