# ADR-0012 — The arena is built from mobile-true fakes, generated from code

**Status:** Accepted
**Date:** 2026-09-26
**Deciders:** shobhit

## Context

The owner supplied two cinematic reference clips (frames in
[`Docs/Reference/Arena/`](../Reference/Arena/README.md)) and asked for the bout arena "with the
same dramatic cinematic lighting, exact detailed". The look is built from:

- a dark bowl with a full crowd and phone flashes;
- a purple/red LED sponsor ribbon and backlit ring-side boards;
- a truss rig of white lamp heads raking beams through haze;
- one warm key spot pooled on a brass-trimmed podium;
- red/blue rims;
- a `DAMAGE CAM / LIVE` jumbotron above a red-lit tunnel.

The clips are offline renders. They use volumetric lighting, thousands of lights, dense
geometry and path-traced reflections. The target is portrait mobile: Android ARM64 with Vulkan
and a GLES3 fallback, and iOS Metal. The budgets are BASELINE's 3.5 ms for opaque geometry,
3.0 ms for transparents and 1.5 ms for displays, with overdraw at 3× or less on mid-tier.

The owner's choices (2026-09-26): **mobile-true cinematic**, **procedural and generated art**,
the fighters **slap across the podium**, and a **live damage-cam jumbotron**.

## Decision

1. **Every expensive effect is faked**. Each fake has one draw call and no per-frame CPU work
   beyond a few transforms:

   | Reference effect | Fake |
   |---|---|
   | Volumetric light shafts | Additive cone mesh, `SlapMania/Arena/Beam`: soft edges, length falloff, scrolling dust. No depth or shadow sampling. |
   | Haze / atmosphere | Three large additive noise sheets (`SlapMania/Arena/Soft`) that fade near the camera, plus exponential-squared fog. |
   | Thousands of spectators | One procedural SDF impostor strip per stand row (`SlapMania/Arena/Crowd`): head, hair, shoulders and raised arms, jittered and overlapping, lit by a height wash. Phone flashes and screens are computed in the same shader, so there are no particles. |
   | Lamp heads, house lights | Unlit HDR lenses and red dots feeding URP Bloom, plus soft additive flare quads. |
   | LED ribbon, boards, jumbotron | `SlapMania/Arena/Led`: an emissive texture with a dot-matrix mask that fades before it would moiré. |
   | Contact shadows | Multiplicative blob quads (`SlapMania/Arena/Blob`). |
   | Reflections on brass and floor | **One baked reflection probe**, pinned as a custom cubemap. |
   | Colour grade | A URP Volume with ACES, Bloom, Colour Adjustments, Vignette and Split Toning. |

2. **Real lights are capped at four spots plus one directional**: key (down on the podium),
   two rims and a fill. Four is exactly `Mobile_RPAsset`'s additional-lights-per-object
   limit. The key spot casts soft shadows. `Mobile_RPAsset` has additional-light shadows off,
   so the key shadows on PC only and costs nothing on mobile. There, the blob quads carry
   contact.

3. **Art is generated from code, not bought or hand-painted** (`SlapMania.Editor.Arena`):
   meshes (bowl, stands, crowd strips, truss, lamps, podium, jumbotron frame) and textures
   (ribbon, boards, floor with a metallic/gloss mask, podium top, medallion, jumbotron overlay,
   signs), with text rasterised from the bundled Barlow TTFs. Textures are overwritten in
   place, which keeps their GUIDs, so material references survive a rebuild.

4. **The jumbotron's live feed is a second camera** (`DamageCam`). It renders to a 512×288 sRGB
   RenderTexture every `_renderEvery`-th frame (default 2) with no post-processing, shadows,
   MSAA or depth/colour textures. It frames the defender's face from the attacker's side.

5. **Tuning follows ADR-0011 §5.**
   - Lights, ambient, fog, flash and sweep live on the `ArenaLookConfig` asset, which
     `ArenaDirector` applies live.
   - Surface looks live on the arena materials (`Art/Arena/Materials`).
   - The grade lives on the `ArenaPost` Volume profile.
   - All three are created once and never overwritten. `Tools > Slap Mania > Prototype >
     Reset Arena Look` recreates them.

6. **Device tiering is a switch, not a second arena**. `ArenaDirector.Tier.Low` turns off the
   beams, the haze and light pool, and the damage-cam camera. The jumbotron then holds its
   last frame.

## Consequences

### Positive

- The whole arena is about 45 draw calls before static batching: floor 1, bowl 12, tunnel 4,
  rig 4, beams 11, jumbotron 5, podium 6, atmosphere 2. It uses no compute and no particles,
  so it runs unchanged on GLES3 (M15 §3 rules out VFX Graph).
- There are zero bought assets (ADR-0007). A rebuild regenerates everything, and sponsor copy
  is one constant each in `ArenaArt`.
- Tuning persists: every look value is in an asset visible from the Tuning Hub.

### Negative

- Beams do not occlude. A fighter standing in a cone does not cut a shadow into it.
- The crowd is one flat strip per row: it does not parallax within a row, and a camera close
  to the stands would read as cut-outs.
- The reflection probe is static. Fighters and beams do not appear in the brass or the floor.
- Beam sweep runs on `Time.time`, not the M11 BeatClock (which does not exist yet). M14 §3
  requires beat sync, so the sweep moves onto `IBeatSource` when M11 lands.

### Risks

- **Overdraw.** Beams, haze sheets and the light pool are large additive surfaces. They are
  unmeasured on device. The High-tier default is unproven against the 3× budget until a Perf
  capture exists.
- **Crowd shader cost.** Three SDF evaluations per crowd pixel, which covers a large share of
  the screen. It needs a Mali/Adreno GPU capture before shipping. A Low-tier crowd material
  with a single evaluation is the fallback.
- **Sponsor names** (`MEGATRON ENERGY DRINK`, `MAX-IMPACT HANDS`, `MALE`) are copied from the
  reference as placeholders and have not been cleared. "Megatron" in particular is a
  well-known third-party character name. Replace them, or clear them with a trademark search,
  before any public build.

## Alternatives considered

| Alternative | Why rejected |
|---|---|
| Real volumetric lighting (a raymarched fog/volumetric renderer feature) | Full-screen raymarch per frame; over budget on mid-tier tile GPUs, and URP 17 has no built-in volumetrics for mobile. |
| Particle systems for haze, phone flashes and confetti-style life | The overdraw and CPU cost of simulating thousands of phone flashes; the crowd shader gets them for free inside pixels it already shades. Particles stay the plan for **gameplay** VFX (impact, confetti) in M15. |
| 3D crowd (instanced skinned or impostor meshes per spectator) | Thousands of instances or a baked impostor atlas pipeline; the camera never gets close enough to the stands to need it. |
| Many realtime lights (Forward+ everywhere, one light per lamp) | The mobile asset is Forward with 4 additional lights per object; lamps are emissive + bloom instead. |
| Buying an arena asset pack | ADR-0007 (no third-party packages) and the owner's choice of procedural art; bought art would also not match the reference's sponsor dressing. |
| Hand-painted textures | Not reproducible from code; the builder could not regenerate the arena, and sponsor copy changes would need an artist. |
