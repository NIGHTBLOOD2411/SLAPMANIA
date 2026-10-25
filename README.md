# Slap Mania 2.0

A turn-based **multiversal slap tournament** for portrait mobile. Champions from
across the universes and planes converge on a central slap ring for the
**Slap Mania Grand Opening 2026**.

**Core loop:** rapid-tap to fill a **power** bar -> stop a sweeping **marker** on a
colored **zone** for a **multiplier** -> `damage = power x multiplier`. Player and
AI alternate slaps until one side's health is gone. The lethal slap is a
**finisher**. Every slap captures a photo **snapshot** of the recipient; the
victory screen shows the whole **gallery**.

## Open the project

- Unity **6000.3.22f1** exactly (Unity 6.3). Do not open in another version.
- URP 17.3.0, Linear color space, New Input System only.
- Target: portrait mobile, Android (ARM64, Vulkan-first) + iOS.

See [Docs/BUILD-AND-RUN.md](Docs/BUILD-AND-RUN.md) for setup.

## Documentation

Everything lives in [`Docs/`](Docs/INDEX.md) - start at
[Docs/INDEX.md](Docs/INDEX.md).

| Read this | When |
|---|---|
| [Docs/ARCHITECTURE.md](Docs/ARCHITECTURE.md) | Before writing any code |
| [Docs/Modules/](Docs/Modules/) | Before touching a module |
| [Docs/DEBUG.md](Docs/DEBUG.md) | Before debugging anything |
| [Docs/GLOSSARY.md](Docs/GLOSSARY.md) | Before naming anything |
| [CLAUDE.md](CLAUDE.md) | Before an AI session |

Docs are at the repo root, **not** under `Assets/` - see
[ADR-0001](Docs/Decisions/ADR-0001-docs-outside-assets.md).
