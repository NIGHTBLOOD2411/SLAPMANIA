# ADR-0016 — Slap gallery: one RenderTexture per photo before the atlas

**Status:** Accepted
**Date:** 2026-10-23
**Deciders:** shobhit

## Context

The owner asked for a snapshot of every landed slap, and for three of the **loser's** snapshots on the game-over screens (E2 Victory and E3 Defeat). M09 designs the full system: a persistent 4x4 atlas in Boot, `RenderPipeline.SubmitRenderRequest` into a tile, and a tier-scaled `SnapshotConfig` ([ADR-0006](ADR-0006-snapshot-atlas.md)). None of that exists yet. M09 also requires a GLES3 smoke test before any snapshot milestone. `MatchDirector` has raised `SnapshotRequested` and `FinisherSnapshotRequested` since P3, and nothing listened to them.

`FighterPortrait` (M10's scorebug tiles) already renders a fighter with a temporary camera and `Camera.Render()` into an sRGB RenderTexture, and it works in this project.

## Decision

Ship the gallery now on the portrait path, and keep the atlas as the target:

- `SlapSnapshotter` (Presentation, M09) photographs the defender on both snapshot signals. It uses one disabled photo camera per bout (`Camera.enabled = false`, `CopyFrom` the gameplay camera, culling mask `+SnapshotOnly −HideInSnapshot −UI`, no MSAA). Each photo goes into its own 256 x 320 `R8G8B8A8_SRGB` RenderTexture (M09 §4 Low tier).
- It keeps at most 16 photos per bout. When full, it overwrites the weakest non-lethal one (S-037).
- What to show is a pure rule in Gameplay, `SnapshotPicker`, so EditMode can test it (R2/R3): the lethal slap, then the loser's hardest other hits, up to three, in the order they happened. Ties go to the later slap.
- `BoutController` fills `HudContext.Gallery` at `BoutEnd`, before the HUD's own handler runs. `HudResult` shows three frames on each result screen, built by `V2HudBuilder`.

**Rejected: build the M09 atlas first.** It is the right end state. But it is several days of URP Render Graph work, it is gated on a device GLES3 test, and the owner wants the feature now. The gallery API (`FillGallery` into `Texture[]`) does not expose RenderTextures, so the atlas can replace the store underneath it.

**Rejected: capture at the impact frame.** M09 §10: the contact frame is the worst photo. The `Snapshot` state starts after the hitstop state (~90 ms after impact), when the recoil reads.

## Consequences

- Up to 16 x 320 KiB of RenderTextures (5 MiB) per bout, freed in `BoutController.OnDestroy` (S-033). That is more than the atlas would use at Low tier, and less than Mid.
- One `Camera.Render()` per landed slap, outside the Charge/Timing paths (R5 does not apply). It has not been profiled on device. Add a `Perf/BASELINE` entry before release.
- The GLES3 smoke test (S-031 Y-flip, S-030 black tile) is still owed. The photo uses the same path as the scorebug portraits, so a GLES3 failure would show there too.
- The capture timing is "the Snapshot state", not "impact + 35 ms" (M09 §4 `captureDelay`). M09 records this.
