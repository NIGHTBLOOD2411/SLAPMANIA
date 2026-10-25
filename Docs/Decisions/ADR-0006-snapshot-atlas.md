# ADR-0006 — Persistent snapshot atlas over per-slap textures

**Status:** Accepted
**Date:** 2026-09-19
**Deciders:** shobhit

## Context

Every slap produces a photograph. A match can have up to 16 slaps. Each photo at 384×480 RGBA is ~720 KB. The question is how to store them during the bout.

## Decision

Use a single persistent 4×4 `RenderTexture` atlas (1536×1920) allocated once at Boot, never released mid-match. Each slap writes to a tile at `(slapIndex % 4, slapIndex / 4)`. The victory gallery reads tile UV rects from the atlas.

## Consequences

### Positive

- **One allocation.** The atlas is created once and reused. No per-slap `RenderTexture.GetTemporary` / release cycle, which is the #1 source of mobile GPU memory fragmentation.
- **One texture bind** for the entire gallery. The victory screen renders all photos as `RawImage` elements referencing the same `RenderTexture` with different `uvRect` values. One draw call, not 16.
- **Predictable memory.** 15.5 MiB on mid-tier, known at Boot. No surprise allocations during gameplay.
- **No readback.** The atlas is GPU-resident. Displaying it in UI is a texture sample, not a CPU readback + re-upload.

### Negative

- **Fixed capacity.** 16 tiles. If a match exceeds 16 slaps, the oldest tiles are overwritten. This is acceptable because 16-slap matches are rare (the escalation curve in M05 ensures most matches end in 7–10 slaps).
- **Memory committed for the full match.** Even a 4-slap match holds 15.5 MiB for 16 tiles. The per-slap alternative would use ~2.9 MiB for 4 photos. On a 2 GB device (min-spec), the difference is meaningful — addressed by the Low tier using 256×320 tiles (6.9 MiB total).
- **Tile size is fixed per tier.** Cannot dynamically resize a tile if one photo needs more resolution. Acceptable because all photos use the same camera at the same distance.

### Risks

- If the game adds a persistent gallery (browse photos from past matches), the atlas approach does not extend — photos would need to be encoded to JPEG and saved to disk. This is a v2 concern; the `RenderTexture` → JPEG pipeline is straightforward via `ImageConversion.EncodeToJPG`.

## Alternatives considered

| Alternative | Why rejected |
|---|---|
| Per-slap `RenderTexture` | 16 separate allocations, 16 texture binds in the gallery, memory fragmentation |
| Per-slap `Texture2D` via `ReadPixels` | CPU readback stall (2–5 ms per photo), re-upload for display, GC allocation for the byte array |
| `AsyncGPUReadback` to `Texture2D` | Avoids the stall but still requires CPU memory for the pixel data, and re-upload for UI display. More complex for the same visual result. |
| `RenderTexture` array (Texture2DArray) | Not supported by `RawImage.uvRect`; requires a custom shader for UI display |
