# ADR-0024 — Physical animation layer and a shared mocap drop-in

**Status:** Accepted
**Date:** 2026-11-07
**Deciders:** shobhit

## Context

Owner, after ADR-0023: "animations you made look robotic, I need mocap-like animations for
anticipation and slap, with simulated-physics type animations."

Two separate gaps:

1. **Source motion.** The authored clips (ADR-0023) are a handful of keyed poses with smooth
   tangents: no weight shift, no micro-motion, every bone arriving at once. Real mocap has
   what they lack. Searched 2026-11-07: no confirmed slap clip on Mixamo. Rokoko's free 13-clip
   fight pack (commercial use, Mixamo-skeleton FBX, 30 fps) sits behind a sign-up form, so its
   contents are unknown. Both need the owner's own account, so the owner downloads.
2. **Physicality.** Only the slapped fighter's head and neck had a spring (`HitReactor`). The
   attacker's arm and torso moved exactly as keyed: no lag, no whip, no follow-through.

## Decision

1. **`PhysicalAnimationLayer`** (Presentation) on every fighter, replacing `HitReactor`.
   After the Animator and SlapIK each frame, 13 upper-body bones (spine, chest, upper chest,
   neck, head, both shoulders, upper arms, forearms, hands) chase their animated local
   rotation through a damped spring (Core `RotationSpring`, tested). Springs get softer down
   the arm (`FighterRigProfile`: torso 7 Hz, neck/head 4.5, upper arm 5, forearm 4, hand 3.2,
   damping 0.45, limit 50°), so the lag compounds into a whip and overshoots on the
   follow-through. A slap adds an angular impulse to the slapped fighter's head (1100°/s at
   full intensity), neck, trunk and arms. The striking arm is pinned to its IK pose by
   `SlapIK.StrikeWeight`, so the palm still meets the cheek. Scaled time, so hitstop freezes it.
   Legs are not simulated. Suspended under the K.O. ragdoll.
2. **Shared mocap drop-in**: FBX files in `Assets/_Project/Animations/Mocap/` whose name starts
   with a role (Idle, Charge, Slap, Hit, Block, Victory) replace that role's authored clip for
   every champion (`FighterBuilder.LoadMocap`, `FighterImportSetup.ConfigureMocapFile`:
   Humanoid, avatar from the file's own skeleton, root baked, feet-based height). Precedence:
   per-model `<stem>@<State>.fbx` > shared mocap > authored. A mocap slap's contact is
   measured on each champion, and attack contacts are re-derived on every build.
3. A slap whose contact comes later than `windupMs` first plays faster, up to
   `FighterRigProfile.maxWindupSpeedup` (1.35), before its start is skipped. That keeps a
   mocap anticipation instead of cutting it.

## Consequences

### Positive

- Any source motion gets secondary motion for free: whip, torso lag, a physical head snap with
  overshoot (Play: about 28° whip, peak at 0.27 s, settled about 0.8 s on a weak slap).
- Mocap is a file drop plus a rebuild, with no code change. Missing roles fall back cleanly.
- No PhysX joints and no allocation: 13 bones x 2 fighters, up to 8 substeps of a quaternion step.

### Negative

- Per-frame cost on both fighters during Charge and Timing (hot path). Allocation-free by
  construction; **not profiled on device yet** (Perf/BASELINE not re-captured).
- The authored clips are still the default until mocap is supplied.
- A mocap slap that steps forward is not fixed by root baking: re-run the S-103 table
  measurement after adding one.

### Risks

- A mocap anticipation much longer than `windupMs` x 1.35 is still partly skipped. The real
  knob is `BoutConfig.windupMs` (M01 §10, the most coupled number).
- Springs that are too soft on long-armed rigs (Hikaru) could swing hands toward the table.
  The 50° limit bounds it, but this is not measured.

## Alternatives considered

| Alternative | Why rejected |
|---|---|
| Full active ragdoll (PhysX joints driven to the clip) | Owner chose the spring layer. ~11 rigidbodies x 2 on mobile, jitter at the 0.02 hitstop time scale, the palm can miss the cheek |
| Keep HitReactor, add a separate arm spring | Two springs writing the same bones in LateUpdate fight each other; one layer owns the upper body |
| Per-model overrides only (`<stem>@<State>.fbx`) | Every champion would need its own copy of the same mocap file |
| Re-author the clips "mocap-style" | Owner chose real mocap. Hand-keyed motion stays hand-keyed however well it is eased |

## Addendum 2026-11-09: what the supplied packs needed

The owner dropped 622 FBX (Rokoko Studio + Legacy packs, a Motifect pack). A Legacy take holds a
real captured slap and a real captured reaction to one. Decision 2 (a filename-prefix scan) was
**replaced before shipping**: it would have picked arbitrary pack files (`Idle_01...`,
`block_high`), and every take is a whole scene needing a trim.

- `MocapSelection` is now the table: role -> take -> seconds -> facing offset. Picks are copied to
  `Animations/Mocap/Selected/<Role>.fbx`, so the pack can leave the project without touching the
  game; the trim lives in that copy's `.meta`. Block stays authored (no state plays it).
- Charge is the 0.67 s before the Slap cut, stretched over 2.2 s, then held; it ends on the frame
  Slap starts. `chargeTrembleDeg` keeps the hold alive through the springs.
- XZ is left unbaked and the feet are planted by IK with `plantedYawKeep` 0.45 (S-106). The strike
  was captured from closer than the table allows, so `SlapIK.LeanIntoReach` pitches the body by
  the measured shortfall.
- Contact measured 435-453 ms per rig against the 500 ms windup: the anticipation plays whole.

**Where the packs live:** moved 2026-11-09 to `C:/Unity/SlapMania Mocap Source` (2.0 GB, 622 FBX), outside `Assets/` and the repo, so Unity does not import them and Git LFS cannot pick them up. The project keeps only `Selected/` (17 MB); nothing else referenced the packs.

**Licence, unresolved:** the terms seen for Rokoko (commercial use) were on the page of a
*different* pack (13 fight clips). No licence file ships with the Legacy pack. The slap take is
also named after real people; the shipped copies are named by role only. Confirm before release.
