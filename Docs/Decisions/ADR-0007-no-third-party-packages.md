# ADR-0007 — No third-party packages

**Status:** Accepted 2026-09-19. **Addressables clause superseded by [ADR-0008](ADR-0008-addressables-and-ccd.md) on 2026-09-19. Editor-only-tooling clause amended by [ADR-0009](ADR-0009-unity-mcp-editor-tooling.md) on 2026-09-26** (narrow, build-excluded exception for editor dev tooling). All other clauses stand.
**Date:** 2026-09-19
**Deciders:** shobhit

## Context

The Unity ecosystem has excellent third-party packages: DOTween for animation, UniTask for async, Odin for inspectors, Addressables for asset management, NaughtyAttributes for editor tooling. Each one solves a real problem. But each one also adds: a dependency that can break on Unity version upgrades, a build-time cost, an APK size contribution, a learning curve for new contributors, and a supply-chain risk (abandoned packages, license changes, security vulnerabilities).

## Decision

No third-party packages. The project uses only Unity's built-in packages (URP, Input System, Timeline, Test Framework, uGUI/TMP, ImageConversion, JsonSerialize) and hand-written utilities.

Specifically:
- **No DOTween.** Lerps are `Mathf.Lerp` with `AnimationCurve`. Sequences are `Awaitable` chains.
- **No Cinemachine.** See ADR-0005.
- **No UniTask.** Unity 6's `Awaitable` covers the same use cases without a package.
- **No Addressables.** The game has <100 assets. `Resources.Load` for the single `GameConfig` entry point, direct references for everything else.
- **No Odin/NaughtyAttributes.** Custom property drawers where needed.

Adding a package in the future requires a new ADR justifying it.

## Consequences

### Positive

- **Zero supply-chain risk.** No dependency on external maintainers, no surprise breaking changes on Unity upgrades.
- **Smaller APK.** Each package adds 0.5–3 MB.
- **Faster domain reload.** Fewer assemblies to compile.
- **Full control.** Every line of code in the project is written for this project and can be modified without forking a package.

### Negative

- **More boilerplate.** A hand-written tween is 10 lines; DOTween is one. This adds up across the project.
- **No inspector enhancements.** Custom drawers must be written per-field instead of using `[ShowIf]`, `[Required]`, etc.
- **`Awaitable` is less featured than UniTask.** No `WhenAll`, no `DelayFrame`, no cancellation token propagation. These must be hand-written when needed.

### Risks

- If the project grows significantly (50+ unique tween sequences, complex asset pipelines), the boilerplate cost may exceed the dependency cost. Revisit at that point with a new ADR.

## Alternatives considered

| Alternative | Why rejected |
|---|---|
| Allow curated packages | Slippery slope; "just one more" leads to 15 dependencies within a quarter |
| Fork packages into the project | Maintenance burden of keeping forks up to date; loses the benefit of community fixes |
| Use packages in Editor only (Odin) | Editor-only packages still affect domain reload and can leak runtime dependencies through generated code |
