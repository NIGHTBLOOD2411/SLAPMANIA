# Capture — 2026-09-26 — Editor — generated arena

> **Editor only. Not a device capture.** It uses the **PC** render-pipeline asset (Forward+, additional-light shadows on), because the Editor quality level is PC (DEBUG S-065). Mobile numbers will differ. The key-spot shadow pass does not exist on `Mobile_RPAsset`, and render scale is 0.8 there. Treat this as a first breakdown to aim the device capture, not as a result.

| | |
|---|---|
| Build | Proto_Bout from `Build Playable Prototype`, generated arena ([ADR-0012](../../Decisions/ADR-0012-arena-mobile-fakes.md)), High tier |
| Where | Unity 6000.3.22f1 Editor, Windows, Game view 437×778, Play From Boot |
| How | `UnityStats` sampled on every Editor update for 20 s of live bout (2003 samples), via the Unity MCP bridge |

## Numbers (all cameras in the frame: main, damage cam, shadow pass)

| Metric | Avg | Max | Mobile target (BASELINE) |
|---|---|---|---|
| Batches | 264 | 329 | ≤ 150 |
| SetPass calls | 75 | 98 | ≤ 40 |
| Draw calls | 266 | — | — |
| Triangles | 2.18 M | — | — |
| Editor frame time | 16.6 ms | — | not meaningful in the Editor |

## Where it goes

- **Fighters: ~175k triangles each** (`Hair` 82k, `High poly.001` 59k, `human.Body` 34k). Each is drawn up to three times per frame: main camera, damage cam (every 2nd frame) and the key-spot shadow map (PC only). That is roughly 1 M of the 2.18 M. The source FBX is a high-poly character. **A mobile LOD or decimated mesh is required (M08)**; the arena cannot fix this.
- **The damage cam re-renders the arena** at 512×288 every 2nd frame. That roughly doubles arena batches on the frames it runs.
- **The arena itself is ~45 draw calls before static batching** (ADR-0012), and static batching merges most of the bowl in Play.

## Levers, in expected order of payoff

1. Fighter LOD / decimation (M08).
2. `DamageCam._renderEvery` 2 → 4 (the M17 Mid-tier rate). Alternatively, give the damage cam a far clip of ~8 m so only the ring renders into the feed.
3. Low tier (`ArenaDirector.Tier.Low`): no beams, haze, pool or damage cam.
4. Device capture on a mid-tier Mali/Adreno for the overdraw of the additive fakes and the crowd shader (not measurable here).
