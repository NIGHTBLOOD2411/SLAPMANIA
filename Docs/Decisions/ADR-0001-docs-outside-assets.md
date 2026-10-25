# ADR-0001 — Docs live at repo root, not under Assets/

**Status:** Accepted
**Date:** 2026-09-19
**Deciders:** shobhit

## Context

Unity imports everything under `Assets/`. Putting Markdown docs there means `.meta` files per doc, GUID churn on renames, `Library/` artifacts, a domain reload on every edit in the Editor, and a real risk of the docs folder shipping in the APK (Unity includes everything in `Assets/` by default unless explicitly excluded).

## Decision

All documentation lives under `Docs/` at the repo root, outside `Assets/`. The project tree is:

```
SlapMania 2.0/
├── Assets/
├── Docs/          ← here
├── Packages/
├── ProjectSettings/
```

Project-window visibility is recovered later by a `Tools/Slap Mania/Docs/▸` editor menu that opens the files in the system editor.

## Consequences

### Positive

- No `.meta` files for docs — no GUID churn, no merge conflicts on rename.
- No domain reload when a doc is edited.
- Zero risk of docs shipping in the APK.
- Docs are accessible to non-Unity tools (CI, linters, static site generators).

### Negative

- Docs are not visible in the Unity Project window without the custom menu.
- New contributors might look in `Assets/` first and not find them.

### Risks

- None significant. `README.md` at the root points to `Docs/`.

## Alternatives considered

| Alternative | Why rejected |
|---|---|
| `Assets/Docs/` | Meta files, domain reloads, APK inclusion risk |
| `Assets/StreamingAssets/Docs/` | Ships in the APK, accessible at runtime — docs are not a runtime asset |
| Wiki / external site | Disconnected from the repo; goes stale without enforcement |
