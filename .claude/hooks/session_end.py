#!/usr/bin/env python3
"""Slap Mania - session-end Definition of Done reminder (Stop hook).

Advisory only: prints the R12 checklist and lists any touched script files whose module
docs should be re-checked (R1). NEVER blocks stopping (always exit 0).

Prereq: Python 3 on PATH. Disable by removing the Stop hook from settings.json.
"""
import sys, os, subprocess

CHECK = """\
Slap Mania - Definition of Done (R12) before you call it done:
  [ ] EditMode green (R3), boundaries covered (0, 1, zone edges, NaN, negative, huge dt)
  [ ] PlayMode: full match, no LogType.Error; Charge/Timing alloc == 0 B (R5)
  [ ] Module doc synced in THIS change (R1): sections + last-verified today + Changelog row
  [ ] DEBUG.md row for any bug found (R6); ADR for any real design choice (R7)
  [ ] Perf/BASELINE re-captured if a hot path / snapshot / audio / canvas path changed
  [ ] No package / scene / singleton / asmdef / ProjectSettings change made silently (R9)
  [ ] Inspector / .asset values relied on are marked UNVERIFIED, not stated as checked (R11)
State which docs you updated. If none, say why."""


def touched_script_files():
    try:
        root = os.environ.get("CLAUDE_PROJECT_DIR", ".")
        out = subprocess.run(
            ["git", "diff", "--name-only"],
            cwd=root, capture_output=True, text=True, timeout=5,
        )
        return [l for l in out.stdout.splitlines()
                if "Assets/_Project/Scripts/" in l.replace("\\", "/")]
    except Exception:
        return []  # not a git repo, or git missing - degrade quietly


def main():
    sys.stdout.write(CHECK + "\n")
    files = touched_script_files()
    if files:
        sys.stdout.write("\nScript files changed on this branch - confirm their module docs are synced (R1):\n")
        for x in files[:20]:
            sys.stdout.write("  " + x + "\n")
    return 0


if __name__ == "__main__":
    try:
        sys.exit(main())
    except Exception:
        sys.exit(0)
