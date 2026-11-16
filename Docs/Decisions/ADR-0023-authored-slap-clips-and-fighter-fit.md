# ADR-0023 — Authored slap clips, one fighter size, a table fitted to both

**Status:** Accepted
**Date:** 2026-11-07
**Deciders:** shobhit

## Context

Owner report (DEBUG S-103): fighters go inside the slap table, champions of different sizes do
not fit it, and the attack reads as a punch, not a slap.

Measured over MCP by baking each skinned mesh across every clip, at the old layout (0.45 m
table, marks ±0.49 m):

- The fighters played the Mixamo **boxing** set. Charge stepped a knee 0.63-0.85 m forward
  (through the whole table); the boxing idle's front thigh sat 0.13 m inside the tabletop;
  Hook/Cross Punch lunged 0.2-0.5 m. They also throw closed fists.
- Champions kept their modelled size: idle head heights 1.50 / 1.52 / 1.77 m (Alucard /
  Drakonit / Hikaru, Avatar humanScale 1.05 / 1.06 / 1.24).
- An upright fighter reaches ~0.5 m from the shoulder and needs ~0.24 m between body and table
  edge. Without a lean there is no table width that fits between two slappers.

No free, licence-clean slap clip was found; Mixamo has none we could confirm, and fetching
from it needs the owner's Adobe sign-in.

## Decision

1. **Author the slap set in code** (`Editor/Characters/SlapClipAuthor`, menu *Author Slap
   Clips*): humanoid muscle-curve `.anim` clips - Slap_Idle, Slap_Charge, Slap_Forehand
   (contact at exactly 500 ms = `windupMs`), Slap_Hit, Slap_Block, Slap_Victory. The left-hand
   slap is the forehand on a **mirrored** state. Square stance, open hand, chest twist, and a
   25-27° lean **from the hips** at contact, so the upper body crosses the table while nothing
   below the tabletop moves. A foot-planting solver (on the Alucard rig) bakes per-key
   RootT/RootQ so the feet stay on their marks; clips are "based upon Original" (S-105).
   `FighterBuilder` uses them for every champion; `<model>@<State>.fbx` still overrides.
2. **One fighter size**: every spawned champion is scaled uniformly to
   `ChampionRoster.fighterHumanScale` (1.05, Alucard as modelled) by `FighterFit.UniformScale`
   (Core, tested). 0 keeps native sizes.
3. **Fit the table to the measurements**: marks ±0.42 m (`PrototypeSceneBuilder.HumanoidX`),
   table 0.36 m wide (`ArenaBuilder.PodiumSize`), apron inside the legs at any width.
4. `SlapIK` lands the palm on the cheek on the striking hand's side
   (`FighterRigProfile.cheekSideOffset`, 0.08 m), not on the front of the face.

## Consequences

### Positive

- Measured after (forward of the root, at tabletop height 0.70-0.84 m): Alucard 0.23,
  Drakonit 0.15, Hikaru 0.31 m (recovery frames only) against 0.24 m clearance. Before: up to
  0.85 m.
- Contact is exact (no measurement), the windup plays at speed 1, and every champion slaps
  the same way. No third-party content, no licence.
- Every matchup frames the same: heads at 1.64-1.69 m.

### Negative

- The clips look stylised next to mocap. Their poses live in code.
- Size is no longer a character trait: Hikaru is 15% smaller than modelled.
- Proportions still differ: Hikaru's knee-length claws hang 0.40 m forward *under* the slab
  (between the legs, not through it), and pass 7 cm into the slab band for a few recovery frames.

### Risks

- The planter solves on the Alucard rig; on other proportions the feet drift up to ~10 cm
  in the slap (hidden by the table). A per-champion solve would need per-champion clips.
- The layout constants are derived from measurements: re-measure (S-103 diagnostic) after
  changing a clip, the target scale, or adding a champion with unusual proportions.

## Alternatives considered

| Alternative | Why rejected |
|---|---|
| Keep the Mixamo boxing clips, widen the gap | They lunge 0.6-0.85 m; no table survives that, and they are punches |
| Owner downloads Mixamo slap-like clips | Owner chose authored; no confirmed slap clip; they step too and need per-clip measuring |
| Keep native champion sizes, space each matchup | Cheek heights differ by 27 cm; table, marks and camera would vary per bout |
| Upright slap, no lean | ~0.5 m reach leaves no room for any table between two bodies |
