---
module: M19-AssetDelivery
status: Draft
owner: shobhit
last-verified: 2026-09-19
verified-against: (pre-git)
asmdef: SlapMania.AssetDelivery
---

# M19 — AssetDelivery

> **One-liner:** Addressables + Unity Cloud Content Delivery — the only asset-loading path in the game; ships a minimal in-build bundle so the tutorial is offline-playable, streams everything else on demand with a signed remote catalog and per-tier variants.

## 1. Purpose

The game's content grows past the AAB budget the moment the champion roster expands beyond a handful of fighters (see [ADR-0008](../Decisions/ADR-0008-addressables-and-ccd.md)). Baking everything into the build pushes the install size past Play/App Store limits and turns every balance tweak into a store update. This module runs the delivery pipeline: it decides what ships in-build, what streams from a CDN, how the game recovers from bad networks and cache eviction, and how per-device-tier variants are picked.

**Placeholder-first still holds.** `IAssetHandle<T>` is the interface every gameplay module uses; the default implementation resolves everything from a **synchronous in-build fallback** so tests and headless P3 code never hit Addressables. The Addressables-backed implementation is chosen at composition time in `SlapMania.App`.

## 2. Responsibilities and non-goals

**Owns**
- The `IAssetService` and `IAssetHandle<T>` abstractions in `SlapMania.Core` (interfaces only).
- `AddressablesAssetService` in `SlapMania.AssetDelivery` — the production implementation.
- The **group / label taxonomy** in the Addressables settings (see §4).
- The **remote catalog** URL scheme, versioning and pinning per Build Profile.
- **Download UX** — first-run bundle prefetch, per-champion on-demand grab with a progress overlay, silent background top-ups.
- **Cache policy** — how much local disk we hold, what gets evicted first, and how "you're offline and the tile you need is gone" is handled.
- **Per-tier variants** — mapping a `DeviceTier` (M00) to which label of the same address is loaded (`champion_yakuza[High]` vs `[Mid]` vs `[Low]`).
- **Catalog integrity** — CCD-signed catalog hashes pinned in the build so a malicious CDN swap cannot inject bundles.

**Explicitly does NOT do**
- Own gameplay logic — asset loading is a service, not a system.
- Own the `GameConfig` boot load → M00 uses `Resources.Load` for the single boot entry point so the game can start before the catalog resolves.
- Own network transport → Addressables uses UnityWebRequest under the hood; we do not wrap it.
- Own save/serialization → M12.

## 3. Public API surface

| Member | Signature | Notes |
|---|---|---|
| `IAssetService.LoadAsync<T>` | `Awaitable<IAssetHandle<T>> LoadAsync<T>(AssetKey key)` | The one call. Cancellable via the returned handle. |
| `IAssetHandle<T>.Value` | `T { get; }` | Null until loaded. |
| `IAssetHandle<T>.IsReady` | `bool { get; }` | |
| `IAssetHandle<T>.Progress` | `float { get; }` | 0..1, for download UI |
| `IAssetHandle<T>.Release` | `void Release()` | Ref-count decrement; the impl decides when to unload |
| `AssetKey` | `readonly struct { string address }` | Stringly-typed on purpose — cheap, allocation-free, and validatable at build time by `AddressablesValidatorWindow` |
| `IAssetService.PrefetchAsync` | `Awaitable PrefetchAsync(AssetLabel label, IProgress<float> progress)` | Bulk grab a whole label — used for champion unlock flows |
| `IAssetService.EstimateDownloadSize` | `Awaitable<long> EstimateDownloadSize(AssetLabel label)` | Bytes; drives the "will download 42 MB, continue on cellular?" prompt |
| `IAssetService.ClearCacheOlderThan` | `void ClearCacheOlderThan(TimeSpan age)` | Called on low-memory warnings |

**Consumed by:** M08 fighter view loading, M09 optional snapshot post-fx pack, M11 audio banks per tier, M14/M15 arena packs, M18 finisher timelines.
**Stability:** interface is stable from day one; implementations churn.

## 4. Addressables configuration

### Groups

| Group | Included in build? | Update mode | Contents |
|---|---|---|---|
| `Local_Core` | **Yes** (Local) | n/a | `GameConfig`, `PowerPhaseConfig`, `TimingPhaseConfig`, `TimingZoneLayout`, `DamageConfig`, `BoutConfig`, placeholder capsule fighter view, the two default `AiProfile` assets. Everything needed to boot and play a capsule duel offline. |
| `Local_MinimalUI` | Yes (Local) | n/a | Default TMP font, HUD sprites, one arena mesh + textures at Low-tier resolution. The tutorial arena. |
| `Remote_Fighters` | No (Remote) | Can Change Post-Release | One label per champion (`fighter_yakuza`, `fighter_streetking`, ...). Each label carries all skins + variants for that fighter. |
| `Remote_Arenas` | No (Remote) | Can Change Post-Release | One label per arena. Includes the arena scene, lighting rig, VFX prefabs, jumbotron content. |
| `Remote_Audio_High` | No (Remote) | Can Change Post-Release | Full 3-stem music banks + reverbs. Loaded only on High/Mid tiers. |
| `Remote_Audio_Low` | No (Remote) | Can Change Post-Release | Pre-mixed loops per M11's Low-tier fallback. Loaded on Low tier. |
| `Remote_Cinematics` | No (Remote) | Can Change Post-Release | Finisher Timeline assets (M18) per champion pair. |
| `Remote_Cosmetic` | No (Remote) | Can Change Post-Release | Snapshot frames, gallery decorations, seasonal palettes. |

### Labels (orthogonal to groups)

- `tier:high`, `tier:mid`, `tier:low` — a single address like `champion/yakuza/model` resolves to one of these based on `DeviceTier`. Addressables' variant system handles this; we do not branch in code.
- `season:2026-q4` — cosmetic content pinned to a season.
- `preload` — everything the frontend needs before the main menu can render.

### Remote paths

Build Profile drives the CCD environment:

| Profile | CCD environment | Bucket | Badge |
|---|---|---|---|
| Dev | `dev` | `slapmania-dev` | `latest` (auto-promoted on catalog upload) |
| QA | `qa` | `slapmania-qa` | `qa-week-N` (manual promotion) |
| Release | `production` | `slapmania-prod` | Immutable per-release (e.g. `1.0.4`) — never re-badged |

The catalog hash for the chosen badge is **pinned** in the build via `AddressableAssetSettings.CheckForContentUpdate = false` for Release; the runtime refuses a catalog whose hash does not match.

## 5. Download UX

Three flows, each with a decided policy:

- **First-run boot.** After M00 finishes and before the main menu shows, kick a background prefetch of the `preload` label. If < 5 MB, block on it silently. If >= 5 MB or the connection is metered, show a one-time "Setting up your arena — X MB" screen.
- **Champion unlock.** When the player unlocks a new fighter (from a bout reward), fire `IAssetService.EstimateDownloadSize(fighter_<name>)`. Under 20 MB or on Wi-Fi: silent background download. Over 20 MB on cellular: modal confirm.
- **Missing asset at runtime.** If `LoadAsync` for a `Remote_*` key fires and the network is offline, the handle stays `IsReady=false` and reports the failure; the calling module falls back to a placeholder (capsule fighter, silent stem, etc.) exactly as it would for a null-object service. Never a hard crash, never a hang.

## 6. Cache policy

- Cache location: `Application.persistentDataPath + "/com.unity.addressables/"` (Addressables default).
- Cap: **500 MB** on High-tier, **250 MB** on Mid, **150 MB** on Low. Configurable in `AssetDeliveryConfig`.
- Eviction: LRU across bundle files, with the exception that anything referenced by the currently loaded scene is pinned. On `Application.lowMemory`, evict the oldest 25 % of the cache regardless.
- **Never** cache under `Application.temporaryCachePath` — some Android OEMs wipe it aggressively.

## 7. Dependencies

- **Hard:** `SlapMania.Core` (interfaces), `com.unity.addressables`, `com.unity.services.ccd.management`.
- **Soft:** M00 (device tier detection), M12 (offline-mode flag persistence).
- **Unity APIs:** `Addressables.LoadAssetAsync`, `Addressables.DownloadDependenciesAsync`, `Addressables.GetDownloadSizeAsync`, `Caching`, `Application.internetReachability`, `Application.lowMemory`.
- **Must initialize after:** M00, before M10 (main menu needs the `preload` label).

## 8. Failure modes

| ID | Failure | Detection | Behaviour | Player-visible? |
|---|---|---|---|---|
| S-100 | Remote catalog unreachable on first boot | Timeout after 15 s | Show a "no connection" screen with retry and "play offline" (tutorial only); log `SlapLog.Error(LogChannel.Boot, ...)` | Yes |
| S-101 | Catalog hash mismatch | `Addressables.InitializeAsync` reports hash mismatch vs the pinned hash | Refuse to load; log Error; force offline mode | Yes — "content update required" prompt |
| S-102 | Bundle download 404 (CCD content removed) | Addressables reports `RemoteProviderException` | Retry once, then substitute the null-object fallback for that key; record in the match recording | Depends on asset — placeholder fighter or silent audio |
| S-103 | Bundle SHA mismatch (corrupt cache) | Addressables auto-clears the corrupt bundle | Retry download; if it fails again, evict the containing group and fall back | Brief hitch |
| S-104 | Disk full on write | `IOException` from Addressables | Trigger `ClearCacheOlderThan(TimeSpan.Zero)`; retry once; then fall back | Yes if repeated — "storage full" prompt |
| S-105 | User revokes network mid-download | Addressables cancels | Handle stays `IsReady=false`; UI shows retry; no crash | Yes — retry prompt |
| S-106 | Addressables settings desync (build vs runtime) | `BuildVerifyPreprocessor` (M13) — refuse a build whose profile does not match the intended Build Profile | Build fails | Editor only |

## 9. Debug hooks

- **Log channel:** `LogChannel.Boot` (init), `LogChannel.Snapshot` / `LogChannel.Audio` / etc. (per-consumer load failures).
- **Overlay (Assets tab):** catalog badge, cache size + eviction counter, in-flight downloads with progress, per-label sizes, last N failed keys.
- **Cheats:** clear cache, force offline mode, force catalog hash mismatch, download a specific label, evict a specific bundle, simulate slow network.
- **Editor tools:** `AddressablesValidatorWindow` — scans the project for `AssetKey` string literals, flags any that do not exist in the current profile's catalog. Runs in `IPreprocessBuildWithReport` and fails the build on unknown keys.

## 10. Tuning notes

**Local group must be self-contained.** The `Local_Core` + `Local_MinimalUI` groups must let the game boot, run the tutorial and finish one capsule-vs-capsule bout with zero network. If those groups grow past ~40 MB, the offline promise no longer justifies the ADR-0008 trade-off; split content, not the promise.

**Ref-counting is a contract, not a suggestion.** Every `LoadAsync` returns a handle whose `Release()` **must** be called by the consumer, once. Addressables ref-counts bundles and leaks silently until a memory warning fires. Every module doc that lists an `IAssetHandle<T>` field in §3 must also list where it is released — typically `OnDestroy` or a matching `Unload` method.

**Do not fetch on the render thread.** `LoadAsync` returns immediately, but its completion arrives on the main thread. Cache handles by key in the calling module; do not re-request per frame. The `Assets` overlay tab shows re-request counts — anything > 0/frame on a live key is a bug.

**Catalog pinning is what makes ADR-0008 safe.** An unpinned catalog means a compromised or misconfigured CCD upload can serve arbitrary bundles to production clients. The Release Build Profile must set `AddressableAssetSettings.CheckForContentUpdate = false` and refuse a catalog whose hash does not match the pinned hash. `BuildVerifyPreprocessor` enforces this.

**Cellular caps are policy, not tech.** The 20 MB "confirm on cellular" threshold is a product choice — some regions have expensive data. Expose it as an `AssetDeliveryConfig` field; do not hard-code.

## 11. Open questions

- [ ] Should the game download the `preload` label during the store-install flow (Google Play Asset Delivery / iOS On-Demand Resources) so first-run is truly instant, or keep it a first-run Addressables prefetch? Play Asset Delivery gives a smoother first-run at the cost of another platform-specific pipeline.
- [ ] Do we sign catalogs ourselves (public key pinned in-build), or trust CCD's badge-level immutability? Self-signing is more paranoid; badge immutability is what Unity's own docs recommend.
- [ ] Is a per-champion download meaningful, or should champions ship in "packs" of 4–6? Packs reduce catalog churn and improve cache hit rate; per-champion is more surgical.
- [ ] Does the tutorial bout need its own arena in `Local_MinimalUI`, or is the placeholder ring enough? Placeholder is cheaper; a proper arena is better first impression.
- [ ] Should we integrate with Unity Gaming Services' Remote Config so tuning SOs (`PowerPhaseConfig` etc.) can be A/B-tested without a content patch? Doable via Addressables' catalog swap but Remote Config is closer to the intent.

## 12. Changelog

| Date | Commit | Change |
|---|---|---|
| 2026-09-19 | (pre-git) | Created (Draft). Owns the Addressables + CCD delivery pipeline defined in [ADR-0008](../Decisions/ADR-0008-addressables-and-ccd.md). |
