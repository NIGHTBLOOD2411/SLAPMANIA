# Arena look reference

Concept frames sampled from two reference clips supplied 2026-09-19. Both clips are static cinematic shots (every frame identical across ~5–12 s), so they read as **stills**, not animation reference. Duplicates removed; three unique frames retained.

## Frames

| File | Source | Framing | What it shows |
|---|---|---|---|
| `arena_wide.png` | `magnific_master…mp4`, 1918×1080 | Wide, empty ring | Full arena reveal: red-lined slap ring with a dark wood + brass trunk (snake medallion) centrepiece. Sponsor-ribbon apron (`MEGATRON ENERGY DRINK` / `MAX-IMPACT HANDS`). Overhead `DAMAGE CAM / LIVE` jumbotron. Front-of-house truss with white spots + red/blue accents raking through haze. Full crowd, phone flashes. |
| `arena_wide_alt.png` | `magnific_master…mp4`, 1918×1080 | Same wide framing | Small lighting variation of the wide reveal — apron warmed slightly, back wall wash. Useful as a compare-shot for lighting cue design. |
| `arena_crowd_pov.png` | `WhatsApp Video 2026-09-19 at 4.09.30 PM.mp4`, 576×1024 portrait | Crowd-POV | Silhouetted spectators (one filming on a phone) foregrounded against the ring. Same sponsor apron. Jumbotron showing a green-gold snake-champion portrait with a `DAMAGE CAM` bug — this is the finisher/entrance display treatment. |

## Consumed by

- [M14 ArenaLighting §1a](../../Modules/M14-ArenaLighting.md#1a-look-reference) — truss rig, haze, warm ring key vs cool crowd wash, crowd phone sparkle.
- [M15 ArenaVFX §1a](../../Modules/M15-ArenaVFX.md#1a-look-reference) — volumetric spot cones, haze sheet, phone-flash sparkle, ring-floor pool decal.
- [M17 ArenaDisplays §1a](../../Modules/M17-ArenaDisplays.md#1a-look-reference) — overhead jumbotron feed, ring-apron LED ribbon, broadcast overlays.
- Scene composition (`Bout_Arena01`) — central trunk/pedestal on the ring, sponsor-wrapped apron, crowd-POV framing.
- **`SlapMania.Editor.Arena.ArenaBuilder`** (2026-09-26, [ADR-0012](../../Decisions/ADR-0012-arena-mobile-fakes.md)) generates the arena from these frames into `Proto_Bout`, including the sponsor ribbon and boards, the tunnel under the Damage Cam jumbotron, the truss lamp rows and the gold-ringed floor.
- **Deliberate departures:**
  - The trunk became a **slap table** on legs: the fighters' clips step a knee into the space a solid chest would fill (DEBUG S-074).
  - Its medallion motif is a **gold palm**, not the reference's snake.
  - The sponsor names are copied as **placeholders** and are not trademark-cleared.

## Not shown, still needed

- Two fighters actually on the ring (both clips are pre-show).
- The finisher cinematic camera language.
- The snapshot-composition frame (photo-camera view of a reacting fighter).
