# ADR-0008 — Addressables + Cloud Content Delivery for asset streaming

**Status:** Accepted (2026-09-19), **partially supersedes [ADR-0007](ADR-0007-no-third-party-packages.md)**
**Date:** 2026-09-19
**Deciders:** shobhit

## Context

[ADR-0007](ADR-0007-no-third-party-packages.md) took a hard line on packages, including:

> **No Addressables.** The game has <100 assets. `Resources.Load` for the single `GameConfig` entry point, direct references for everything else.

That call was correct **for the pre-content project**. It is no longer correct now that the design has grown teeth:

- **Signature snapshot gallery + finisher cinematics** (M09, M18) create a permanent draw toward character variety. Fifty champion universes × two skins × two arenas is well past the ~100-asset assumption behind ADR-0007.
- The [Content Manifest](../CONTENT-MANIFEST.md) already lists ~485 audio clips and 15–25 3D models. An AAB with the whole set baked in overshoots the ≤ 80 MB build-size target from [Perf/BASELINE.md](../Perf/BASELINE.md).
- Play Store limits (Google Play install size, iOS App Store 4 GB) impose hard caps that force on-demand delivery for **any** live-operated title, not just this one.
- The user wants a proper live-content pipeline (CDN, patchable content, per-tier variants). That is what Addressables + CCD exist to solve; implementing it by hand is a two-quarter distraction from gameplay.

## Decision

Adopt **Unity Addressables** as the sole asset-loading path, and **Unity Cloud Content Delivery (CCD)** as the remote host.

- Add `com.unity.addressables` and `com.unity.services.ccd.management` to the project when M19 breaks ground (P8-ish in the ROADMAP; not this pass).
- **Reverse the "No Addressables" clause** of ADR-0007. The rest of ADR-0007 (no DOTween, no Cinemachine — see [ADR-0005](ADR-0005-no-cinemachine.md), no UniTask, no Odin) stands as written.
- A new module doc, [M19-AssetDelivery](../Modules/M19-AssetDelivery.md), owns the design surface: label taxonomy, groups, remote/local split, download UX, retry policy, cache eviction, per-tier variants, catalog signing.
- `GameConfig` and the small handful of always-needed assets stay in the build (either `Resources` or a "Local" Addressables group) so the game can boot with **zero** network I/O.

## Consequences

### Positive

- **Scope headroom.** Content growth (new champions, arenas, cutscenes) no longer bloats the AAB or forces store re-review.
- **Delta patching.** Balance tweaks and content additions ship without app updates. Combined with the ScriptableObject-based tuning (`R2` / ADR-0003), a balance patch is a CCD upload.
- **Per-tier variants** ship cleanly: `SnapshotConfig_Low`/`Mid`/`High` and Low-tier audio banks are Addressables labels, not `#if` branches in code.
- **Standard tooling.** New engineers ramp on Addressables faster than on a hand-rolled AssetBundle pipeline.

### Negative

- **APK cost.** Addressables adds ~1.5–2 MB to the base build.
- **Learning curve.** Addressables' groups/profiles/labels model has real complexity. M19 must document the taxonomy tightly or it rots.
- **Live dependency.** CCD is a Unity-hosted service; if it goes down, first-time users cannot download content. The mitigation is a locally-bundled minimal-viable-content set so the tutorial and first bout are always playable offline.
- **Supply-chain footprint.** Addressables + CCD Management is Unity-first-party code, so the risk is bounded, but it is more surface than "just built-in modules".

### Risks

- Silent binding failure: an `AssetReference` with no key in the catalog throws at load. Mitigated by editor-time validation in M19 and a runtime "asset missing" fallback texture that logs `SlapLog.Error(LogChannel.Snapshot, ...)`.
- Catalog drift: shipping a build with a stale remote catalog reference. Mitigated by CCD's badged releases and pinning the catalog hash per build profile.
- iOS App Store review: on-demand downloaded content that materially changes gameplay is fine, but downloaded content that adds new store-purchasable items may need Apple's `On-Demand Resources` framing. Not a blocker for v1's cosmetic-only remote content.

## Alternatives considered

| Alternative | Why rejected |
|---|---|
| Keep ADR-0007 as-is (all-in-build) | Fails the AAB budget the moment the character roster exceeds ~5 fighters. Punting the delivery decision means punting the roster decision. |
| Custom `com.unity.modules.assetbundle` + user-owned CDN (S3 / Cloudflare) | Preserves ADR-0007's "no packages" spirit. But re-implements catalog, bundle dependency graph, cache management, versioning, and retry — 3–4 months of infra work that Addressables gives for free. Rejected on cost, not on principle. |
| Third-party solutions (BundleKit, ScriptableBuildPipeline forks) | Reintroduces the supply-chain risk ADR-0007 was written to avoid, with none of Addressables' first-party support. |

## Migration from ADR-0007

- ADR-0007's status is amended to **"Accepted, Addressables clause superseded by ADR-0008"**.
- No other clauses change. DOTween / UniTask / Odin / Cinemachine are still refused; new packages still require a new ADR each.
- `Resources.Load` is retained for the single `GameConfig` boot entry point, because Addressables' own catalog needs to be loadable before the config that describes it. This is one of Unity's documented Addressables patterns.
