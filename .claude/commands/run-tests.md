---
description: Run EditMode (default), PlayMode, or all tests and summarize failures. Prefers the Unity MCP; falls back to CLI batchmode.
argument-hint: "[editmode|playmode|all]"
---

Run the Slap Mania test suite. Target: `$ARGUMENTS` (default `editmode` if empty).

Primary path - **Unity MCP** (`unityMCP`, ADR-0009), when the Editor is open:
- Trigger the Test Framework via `mcp__unityMCP__execute_code` (invoke the EditMode/PlayMode
  runner) and read results with `mcp__unityMCP__read_console`.

Fallback path - **CLI batchmode**, when the Editor is closed (it holds the project lock).
Adjust the Hub path to this machine:

```bash
"C:/Program Files/Unity/Hub/Editor/6000.3.22f1/Editor/Unity.exe" \
  -runTests -batchmode -projectPath "C:/Unity/SlapMania 2.0" \
  -testPlatform EditMode -testResults "Logs/editmode-results.xml" -logFile "Logs/editmode-run.log"
```
(Swap `-testPlatform PlayMode` for the allocation + full-match suite. Do not add `-quit`.)

Then:
1. Parse results (MCP console, or `Logs/editmode-results.xml` NUnit XML).
2. Report: total / passed / failed, and for each failure the **test name + first failing
   assertion + file:line**.
3. If the **PlayMode allocation test** failed at >0 bytes, hand off to `perf-alloc-auditor`.
4. If a **Core rule** failed, hand off to `core-math-tester`.
5. Do not "fix" a test by loosening its assertion - a red boundary test (R3) is usually a
   real bug. See `Docs/TESTING.md`.
