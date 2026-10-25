---
module: M09-SnapshotCapture
status: InProgress
owner: shobhit
last-verified: 2026-10-23
verified-against: (pre-git)
asmdef: SlapMania.Presentation
---

# M09 — SnapshotCapture

> **One-liner:** A dedicated photo camera captures the slap recipient at impact + 35 ms into a persistent 4x4 render-texture atlas, producing the victory-screen gallery.

## 0. What exists today (2026-10-23, [ADR-0016](../Decisions/ADR-0016-slap-gallery-pre-atlas-capture.md))

The rest of this doc is the target design (atlas + `SubmitRenderRequest`). The shipped pre-atlas path is:

| Piece | Where | What it does |
|---|---|---|
| `SlapSnapshotter` | Presentation | One disabled photo camera per bout (`CopyFrom` the gameplay camera; culling `+SnapshotOnly −HideInSnapshot −UI`; no MSAA). `Capture(defenderHead, attackerPosition, SlapShot)` renders the defender's face, 35° off the attacker's line at 1.05 m with a 36° FOV, into its own 256 x 320 `R8G8B8A8_SRGB` RenderTexture. It keeps at most 16 photos, then overwrites the weakest non-lethal one (S-037). `FillGallery(loser, Texture[], int[])`. `Dispose()` frees everything (S-033) |
| `SlapShot`, `SnapshotPicker` | Gameplay (pure, R2) | `PickLoserGallery`: the lethal slap if any, then the loser's hardest other hits, up to the frame count, in the order they happened. Ties go to the later slap. `SlotToEvict`. Tested in `SnapshotPickerTests` |
| Wiring | `BoutController` (App) | `SnapshotRequested` **and** `FinisherSnapshotRequested` -> `Capture` (every landed slap). `BoutEnd` -> `FillGallery(winner.Other())` into `HudContext.Gallery` before the HUD's handler |
| Display | `HudResult` (M10) | Three framed photos with the slap's damage on E2 Victory (above the title) and E3 Defeat (under the coaching card) |
| Hero photo + D4 | `SlapSnapshotter.LatestHero` / `ReplayView` (M10) | The latest slap is also rendered at 640 x 800 (one more `Camera.Render`, one 2 MiB RT for the bout). `BoutController` fills `ScreenManager.Replay` (`ReplayShot`: photo, round, damage, power, zone, multiplier, impact time) on every snapshot. D4 Instant Replay shows it in place of the mockup still. After a K.O. the K.O.'s CONTINUE plays D4 before E2/E3; from the pause menu, "Last replay" shows the latest slap. It is a **still**: GLOSSARY's "Replay" (a recorded clip) does not exist, so D4's camera/speed chips, Save and Share stay placeholders |

**Differences from the design below:** the capture happens on entering the `Snapshot` state (after the ~90 ms hitstop state), not at impact + 35 ms. There is no atlas, no `SnapshotConfig` and no tier scaling. The GLES3 smoke test has **not** been run.

## 1. Purpose

Every slap produces a photograph. Not a screenshot — a *photograph*, composed by a dedicated camera that only renders fighters and arena, excludes the HUD, and fires at a specific moment chosen for maximum visual impact. The gallery of these photos on the victory screen is the game's signature output, the thing players screenshot and share.

This module is also the most platform-sensitive system in the game. URP 17's Render Graph, Vulkan vs GLES3 differences, sRGB vs Linear color handling, and mobile GPU memory limits all converge here. The failure modes section is unusually long because every one of them has been seen in production on other projects.

## 2. Responsibilities and non-goals

**Owns**
- The photo camera (`Camera.enabled = false`, GameObject active) and its culling mask.
- The persistent 4x4 render-texture atlas (allocated once at Boot, never released mid-match).
- Capture timing (contact + 35 ms offset, during hitstop).
- `RenderPipeline.SubmitRenderRequest` into a specific atlas tile.
- The `SnapshotGallery` data structure (ordered list of tile indices + metadata per slap).
- Tier-based resolution scaling.
- The GLES3 fallback path.

**Explicitly does NOT do**
- Compose the victory screen → `M10-UIScreenFlow`.
- Own the main game camera → `M16-CameraDirector`.
- Apply post-processing to the photo → the photo camera has its own minimal Volume (bloom only, no color grading — the photo should look like the game, not like Instagram).
- Archive photos to disk → `M12-SaveProgression` may do this post-v1.

## 3. Public API surface

| Member | Signature | Notes |
|---|---|---|
| `SnapshotSystem.Init` | `void Init(SnapshotConfig cfg, DeviceTier tier)` | Allocates the atlas |
| `SnapshotSystem.Capture` | `Awaitable<SnapshotResult> Capture(int slapIndex, SnapshotTier tier)` | Renders one tile |
| `SnapshotSystem.GetTile` | `Rect GetTileUV(int slapIndex)` | UV rect for the gallery UI |
| `SnapshotSystem.Atlas` | `RenderTexture { get; }` | The single atlas texture |
| `SnapshotSystem.Gallery` | `IReadOnlyList<SnapshotEntry> { get; }` | Ordered capture metadata |
| `SnapshotSystem.Release` | `void Release()` | Frees the atlas RT on bout end |

`SnapshotResult`: `readonly struct` — `int tileIndex`, `bool success`, `string failReason`.
`SnapshotEntry`: `readonly struct` — `int slapIndex`, `int tileIndex`, `SnapshotTier tier`, `bool isCrit`, `bool isFinisher`, `int damage`.
`SnapshotTier`: `enum` — `Normal`, `Crit`, `Finisher` (determines if extra effects are composited).

**Consumed by:** M01 (triggers capture), M10 (reads atlas for gallery), M13 (debug overlay).
**Stability:** churning until tested on real hardware with both Vulkan and GLES3.

## 4. Configuration

Asset: `Assets/_Project/Data/SnapshotConfig.asset`

| Field | Type | Default (Mid tier) | High | Low | Notes |
|---|---|---|---|---|---|
| `tileWidth` | int px | **384** | 512 | 256 | Per tile |
| `tileHeight` | int px | **480** | 640 | 320 | 4:5 portrait aspect |
| `atlasColumns` | int | **4** | 4 | 4 | |
| `atlasRows` | int | **4** | 4 | 4 | 16 tiles max |
| `format` | RTFormat | `R8G8B8A8_SRGB` | same | same | **Must be sRGB**, not Linear — see §10 |
| `captureDelay` | float s | **0.035** | 0.035 | 0.035 | After the impact frame, not after contact — contact is the worst photo |
| `antiAliasing` | int | **1** | 1 | 1 | MSAA off on the photo camera — MSAA + Render Graph + SubmitRenderRequest = black tile on some drivers |
| `renderScale` | float | **0.8** | 1.0 | 0.6 | Photo camera bypasses the main camera's render scale |
| `bloomEnabled` | bool | **true** | true | false | Minimal bloom only |

### Atlas memory

| Tier | Tile | Atlas | + Depth + Scratch | Total |
|---|---|---|---|---|
| High | 512×640 | 5.0 MiB | ~22.5 MiB | **27.5 MiB** |
| Mid | 384×480 | 2.8 MiB | ~12.7 MiB | **15.5 MiB** |
| Low | 256×320 | 1.25 MiB | ~5.65 MiB | **6.9 MiB** |

Allocated once in `Boot`, not per-slap. The depth buffer and scratch RT are transient Render Graph resources.

**Invariants**
- `format` must be sRGB — Linear produces washed-out photos.
- `antiAliasing == 1` — any other value is a known driver bug vector.
- `atlasColumns * atlasRows >= 16` — a 10-round match with escalation can reach 16 slaps.

## 5. Events

**Emits**

| Event | Payload | When |
|---|---|---|
| `SnapshotCaptured` | `SnapshotResult` | After the render completes |

**Consumes**

| Event | From | Reaction |
|---|---|---|
| `MatchSignal.HitstopStart` | M01 | Schedule capture at `captureDelay` |
| `MatchSignal.BoutEnd` | M01 | Finalize gallery |

## 6. State machine

```
Idle --Init--> Ready --Capture--> Rendering --complete--> Ready --Release--> Idle
```

`Rendering` lasts one frame (the `SubmitRenderRequest` completes synchronously within the render pipeline's execution). The `captureDelay` is waited in `Ready` before transitioning to `Rendering`.

## 7. Dependencies

- **Hard:** `SlapMania.Core`, `SlapMania.Data`, URP 17 (`RenderPipeline`, `UniversalRenderPipelineAsset`).
- **Soft:** M08 (hitstop must be active during capture — the scene is frozen, giving a clean frame).
- **Unity APIs:** `Camera`, `RenderTexture`, `RenderPipeline.SubmitRenderRequest`, `Graphics.Blit` (atlas compositing), `SystemInfo.SupportsRenderTextureFormat`.
- **Must initialize after:** M00 (tier known), M16 (camera rig placed, so photo camera can be positioned relative to it).

## 8. Failure modes

| ID | Failure | Detection | Behaviour | Player-visible? |
|---|---|---|---|---|
| S-030 | Tile is uniformly black | Post-capture pixel sample (read one pixel via `AsyncGPUReadback`) | Log Error, mark tile as failed, gallery shows a "missed shot" placeholder | Yes — missing photo |
| S-031 | Photo is upside down (GLES3 Y-flip) | Orientation check via a "TOP" marker quad on `SnapshotOnly` layer | Flip UV in the gallery, log Warn | No if detected; jarring if not |
| S-032 | Photo washed out (Linear instead of sRGB) | Readback of a reference grey card — 128 ± 3 expected | Log Error with measured value | Yes — photos look wrong |
| S-033 | Atlas leaks on scene reload | Memory profiler delta check | `Release()` in `BoutEnd` handler; `OnDestroy` safety net | No if caught; OOM crash if not |
| S-034 | `SubmitRenderRequest` ignored (Render Graph culls the pass) | Black tile (same as S-030) | Must call `AllowPassCulling(false)` and `ImportTexture` with `discardOnLastUse = false` | Yes |
| S-035 | MSAA + Render Graph = black on some Adreno GPUs | S-030 path | `antiAliasing = 1` is the fix; it is the default | Yes if MSAA is on |
| S-036 | Photo camera renders HUD elements | Visual inspection | HUD is Screen Space - Overlay (no camera renders it); photo camera culling mask excludes layer 11 | Yes — HUD in photo |
| S-037 | Atlas texture not large enough for all slaps | `slapIndex >= columns * rows` | Wrap — overwrite the earliest tile, log Warn | Oldest photos replaced |

## 9. Debug hooks

- **Log channel:** `LogChannel.Snapshot`
- **Overlay (Snapshot tab):** atlas preview (scaled down), last captured tile highlighted, capture time relative to impact, readback pixel sample, memory usage.
- **Cheats:** force capture (button), view atlas full-size, re-capture with different delay, toggle the "TOP" marker quad.
- **Recorder events:** `SnapshotCaptureStart`, `SnapshotCaptureEnd` (with success/fail and timing).
- **Editor tools:** `SnapshotTestWindow` — captures a test photo outside of a match to verify the pipeline.

## 10. Tuning notes

**Capture at contact + 35 ms, never at contact.** The contact frame is the worst possible photo: the hand has not yet deformed the face, the face has not yet recoiled, and the smear mesh is not yet applied. At +35 ms (during hitstop, so the scene is frozen), the deformation is fully expressed, the impact particles are visible, and the defender's expression (when real faces exist) is at peak distortion. This is the "Polaroid moment."

**The atlas format must be `R8G8B8A8_SRGB`, not `R8G8B8A8_UNorm`.** URP renders in Linear color space. If the atlas is Linear (`UNorm`), the data stored is linear but the UI displays it through the sRGB gamma curve *again*, double-encoding gamma and producing washed-out photos. The `_SRGB` suffix tells the GPU to convert from Linear to sRGB on write and back on read, which is correct. This is S-032 and it is the single most common rendering bug in Unity projects that mix render textures and UI.

**MSAA must be off on the photo camera.** `SubmitRenderRequest` with MSAA on a Render Graph camera creates an unresolved MSAA target on some Adreno GPUs (the resolve pass is culled). The result is a black tile. Setting `antiAliasing = 1` on the photo camera (not on the main camera) is the fix. The photo is 384×480 — aliasing is not visible at that resolution.

**`AllowPassCulling(false)` is mandatory.** Render Graph aggressively culls passes whose outputs are not consumed by a later pass in the same frame. The photo camera's output goes to the atlas, which is not consumed by the main camera's graph. Without this flag, the entire photo render is silently culled. This is S-034.

**The photo camera is `Camera.enabled = false` with `GameObject.activeSelf = true`.** Disabling the component stops it from rendering every frame (which would double the GPU cost). Keeping the GameObject active means `SubmitRenderRequest` can still target it. Do not deactivate the GameObject — `SubmitRenderRequest` cannot find a disabled camera.

**The GLES3 smoke test is mandatory before any snapshot milestone.** See [BUILD-AND-RUN.md](../BUILD-AND-RUN.md) for the procedure. Vulkan handles render targets consistently; GLES3 does not.

## 11. Open questions

- [ ] Should the photo camera have its own URP renderer asset with a stripped feature set (no shadows, no SSAO), or share the main renderer? A stripped renderer saves ~1 ms on mid-tier but adds a maintenance surface.
- [ ] Is `AsyncGPUReadback` for the pixel check worth its complexity, or is a one-frame-delayed `ReadPixels` acceptable given the capture happens during hitstop (time is effectively frozen)?
- [ ] Should the atlas be JPEG-compressed and archived to `Application.persistentDataPath` after the match for a permanent gallery, or is that a v2 feature?
- [ ] If a match exceeds 16 slaps (unlikely but possible), should old tiles be overwritten or should the atlas grow? Overwriting is simpler and the oldest slaps are least interesting.

## 12. Changelog

| Date | Commit | Change |
|---|---|---|
| 2026-10-23 | (pre-git) | **D4 Instant Replay made live** (owner: "see the snapshot screen, implement it now"). `SlapSnapshotter` keeps a hero-size latest photo (`LatestHero`, `LatestShot`) and calls `ResetAspect` per target. The bout feeds D4 through `ScreenManager.Replay`, and the K.O.'s CONTINUE goes K.O. -> D4 -> results. §0 updated. The UXML hooks were checked against the real `Replay.uxml` over MCP (fe-img, 3 k-tl tiles, caption, timeline, both links found once). **Not seen in Play yet** |
| 2026-10-23 | (pre-git) | **First code: the pre-atlas slap gallery ([ADR-0016](../Decisions/ADR-0016-slap-gallery-pre-atlas-capture.md)).** Owner: "a snapshot per punch landing, and 3 snapshots of the player who lost on the game-over screen". New `SlapSnapshotter` (Presentation), plus `SlapShot` and `SnapshotPicker` (Gameplay, pure), wired in `BoutController` and shown by `HudResult`. New §0. Status Draft -> InProgress. Tests: `SnapshotPickerTests` (7). **Not verified in Play or on device:** the photo framing, and that `Camera.Render` produces a non-black tile (S-030) on GLES3 |
| 2026-09-19 | (pre-git) | Created (Draft) with atlas design, URP 17 constraints, and failure modes |
