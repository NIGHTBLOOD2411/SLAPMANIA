#!/usr/bin/env python3
"""Slap Mania - advisory C# guard (PostToolUse: Edit|Write|MultiEdit).

Flags CLAUDE.md rule violations the moment a C# file under Assets/_Project/Scripts is
saved. Advisory and FAIL-OPEN: it never crashes the tool and never blocks legitimate work.
- Hard violations (R4 Debug.Log; new-Input-System-only) exit 2 so Claude sees the message
  and self-corrects.
- Softer smells (LINQ, Find, Resources.Load, Camera.main) are printed and exit 0.

Prereq: Python 3 on PATH. Disable by removing the PostToolUse hook from settings.json.
"""
import sys, json, re


def main():
    raw = sys.stdin.read()
    data = json.loads(raw) if raw.strip() else {}
    ti = data.get("tool_input", {}) or {}
    path = ti.get("file_path") or ti.get("path") or ""
    p = path.replace("\\", "/")

    if not p.endswith(".cs"):
        return 0
    if "Assets/_Project/Scripts" not in p:
        return 0
    if "/Logging/" in p:  # SlapLog is the one place UnityEngine.Debug is allowed
        return 0

    try:
        with open(path, "r", encoding="utf-8", errors="replace") as f:
            lines = f.readlines()
    except OSError:
        return 0

    hard_pats = [
        (re.compile(r"\bDebug\s*\.\s*Log"),
         "R4: use SlapLog.<Channel>(...), never Debug.Log in gameplay code"),
        (re.compile(r"\bUnityEngine\s*\.\s*Input\b"),
         "New Input System only: UnityEngine.Input does not exist in this project"),
        (re.compile(r"\bInput\s*\.\s*(Get|mouse|touch|anyKey|GetAxis|GetButton)"),
         "New Input System only: read taps from InputAction/EnhancedTouch events"),
    ]
    soft_pats = [
        (re.compile(r"\busing\s+System\.Linq\b"),
         "R5: LINQ allocates - keep it off Charge/Timing paths"),
        (re.compile(r"\bGameObject\s*\.\s*Find"),
         "avoid GameObject.Find; wire references in Awake/Inspector"),
        (re.compile(r"\bFindObjectOfType\b"),
         "avoid FindObjectOfType on any per-frame path"),
        (re.compile(r"\bResources\s*\.\s*Load"),
         "no Resources.Load on a gameplay path (R5); GameConfig boot entry is the only exception"),
        (re.compile(r"\bCamera\s*\.\s*main\b"),
         "Camera.main does a find + allocates; cache the camera"),
    ]

    hard, soft = [], []
    for i, line in enumerate(lines, 1):
        # skip obvious single-line comments to cut false positives
        stripped = line.lstrip()
        if stripped.startswith("//") or stripped.startswith("*") or stripped.startswith("///"):
            continue
        for rx, msg in hard_pats:
            if rx.search(line):
                hard.append(f"  {p}:{i}  {msg}")
        for rx, msg in soft_pats:
            if rx.search(line):
                soft.append(f"  {p}:{i}  {msg}")

    if hard:
        sys.stderr.write("Slap Mania rule check - HARD violations (see CLAUDE.md):\n"
                         + "\n".join(hard) + "\n")
        if soft:
            sys.stderr.write("Also worth a look:\n" + "\n".join(soft) + "\n")
        return 2
    if soft:
        sys.stderr.write("Slap Mania rule check - advisory (verify; not blocking):\n"
                         + "\n".join(soft) + "\n")
    return 0


if __name__ == "__main__":
    try:
        sys.exit(main())
    except Exception:
        sys.exit(0)  # fail-open: a broken guard must never block editing
