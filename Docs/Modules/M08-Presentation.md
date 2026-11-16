---
module: M08-Presentation
status: InProgress
owner: shobhit
last-verified: 2026-11-09
verified-against: (pre-git)
asmdef: SlapMania.Presentation
---

# M08 — Presentation

> **One-liner:** Bridges the headless gameplay simulation to the visual world — hitstop, fighter animation, screen shake, and the `IFighterView` interface that lets capsules stand in for characters.

## 1. Purpose

Core and Gameplay asmdefs know nothing about GameObjects, Animators, or transforms. This module is the translation layer. When M01 says "impact happened with 47 damage," Presentation decides: 8 frames of hitstop, a heavy recoil animation, a screen shake at 0.7 intensity, and a motion smear on the defender. It consumes `MatchSignal` events and drives the visual scene.

The **placeholder-first** contract is the other half of this module's job. `IFighterView` is an interface with methods like `PlayWindup()`, `PlayImpactRecoil(float intensity)`, `PlayFinisher(FinisherType type)`. Day one, `PlaceholderFighterView` implements it on a capsule with color flashes and position tweens. When real characters arrive, `AnimatorFighterView` implements the same interface with Animator triggers and blend trees. Neither M01 nor any gameplay system changes.

## 2. Responsibilities and non-goals

**Owns**
- `IFighterView` and its implementations (`PlaceholderFighterView`, future `AnimatorFighterView`).
- `ISlapTimingSource` — provides animation-level timing (windup duration, contact frame, recovery duration) so M01 knows when to fire `ImpactFrame`.
- Hitstop — freezing `Time.timeScale` for N frames (or, if that causes problems, freezing animation and physics while letting UI and audio continue).
- Screen shake — translating a shake intensity into camera position noise via M16's `CameraRig.AddTrauma()`.
- Impact smear — a mesh distortion on the defender's face at the impact frame, held through hitstop.

**Landed so far: the humanoid fighter ([ADR-0011](../Decisions/ADR-0011-adopt-v2-design-prototype-first.md))**
- **`HumanoidFighterView`**, built from the owner's Mixamo-rigged FBX (`Alucard Sword Animation`) by `Characters.FighterBuilder` (M13). The base model sits in `Characters/FBX/Alucard/` with its materials and textures and is found by file name. The `@clip` files are in `Animations/`.
  - An Animator controller with Idle, Charge, Hit, Block, Victory and two attacks (Hook, Cross), all cross-faded.
  - Attack playback speed (`AttackSpeed` parameter) scaled so the measured contact frame lands exactly on the FSM's windup.
  - The sword is hidden at `Awake`.
- **`SlapIK`**: look-at IK (the fighters watch each other), plus strike IK in `OnAnimatorIK`. The attacking hand is driven onto the defender's `GetStrikePoint` (the cheek) through a blend-in / hold / blend-out, so every slap physically lands.
- **`HitReactor`**: a damped spring on spine, chest, neck and head, kicked along the slap direction and scaled by intensity. It is layered on top of the animation in `LateUpdate`.
- **`RagdollController`**: KO only. There are 11 bodies and CharacterJoints, built by `Characters.RagdollBuilder`. They stay kinematic with colliders off until the lethal slap, then physics takes over, launched along the slap with the head leading.
- **`FighterPortrait`**: a one-shot sRGB RenderTexture of a fighter's face for the scorebug (M10).
- **`FighterRigProfile`** (`SlapMania.Data`, `Data/Prototype/Fighter_Alucard_Rig.asset`) holds every value above and is listed in the Tuning Hub.
- **Placement:** roots at x = ±0.49 m, facing each other across the arena's podium, a slap table (M15). The spacing is measured, not guessed: at contact the hand reaches 0.77–0.79 m forward and the rival's cheek sits 0.20 m inside their root.
- The arena runtime (`ArenaDirector`, `DamageCam`, `CameraRig`) also lives in `SlapMania.Presentation` (`Presentation/Arena/`) but is documented in M14, M15, M16 and M17.

**Explicitly does NOT do**
- Own the camera → `M16-CameraDirector`.
- Own particles or VFX → `M15-ArenaVFX`.
- Own lighting → `M14-ArenaLighting`.
- Own audio → `M11-Audio`.
- Compute damage or decide phase transitions → M01, M05.

## 3. Public API surface

| Member | Signature | Notes |
|---|---|---|
| `IFighterView.PlayIdle` | `void PlayIdle()` | Default state |
| `IFighterView.PlayChargePose` | `void PlayChargePose()` | Leaning forward, arm cocked |
| `IFighterView.PlayWindup` | `void PlayWindup(float secondsToContact)` | **Landed.** The humanoid scales its attack clip so the contact frame lands at `secondsToContact` |
| `IFighterView.PlayImpactRecoil` | `void PlayImpactRecoil(float intensity, Vector3 hitDirection)` | **Landed.** 0–1; the humanoid kicks `HitReactor` along the direction |
| `IFighterView.PlayKnockout` | `void PlayKnockout(float intensity, Vector3 hitDirection)` | **Landed.** The lethal slap; the humanoid goes to ragdoll |
| `IFighterView.PlayFinisher` | `void PlayFinisher()` | Landed as `void`; the draft's `Awaitable PlayFinisher(FinisherType)` waits for M18 |
| `IFighterView.PlayVictoryPose` | `void PlayVictoryPose()` | |
| `IFighterView.PlayDefeatPose` | `void PlayDefeatPose()` | No-op once the ragdoll is active |
| `IFighterView.BindOpponent` | `void BindOpponent(IFighterView opponent)` | **Landed.** Look-at and strike IK need the other fighter |
| `IFighterView.HeadAnchor` | `Transform { get; }` | **Landed.** HUD rings, portraits, damage cam |
| `IFighterView.GetStrikePoint` | `Vector3 GetStrikePoint(Vector3 attackerPosition)` | **Landed.** The cheek facing the attacker (`FighterRigProfile.strikeOffset`) |
| `FighterPortrait.Capture` | `static RenderTexture Capture(Transform head, Vector3 facing, int size = 192, float distance = 0.8f, float fov = 32f)` | **Landed.** One-shot |
| `ISlapTimingSource.WindupDuration` | `float { get; }` | Seconds from WindupStart to contact |
| `ISlapTimingSource.RecoveryDuration` | `float { get; }` | Seconds from contact to idle |
| `HitstopController.Apply` | `void Apply(float seconds)` | **Landed** (wall-clock seconds; `BoutController` converts from `FeelConfig` ms). Also scales `fixedDeltaTime` with `timeScale`, so the KO ragdoll plays smoothly in slow motion (S-042) |
| `HitstopController.IsActive` | `bool { get; }` | Polled by systems that should freeze |
| `HitstopController.SetPaused` | `void SetPaused(bool paused)` | **Landed 2026-10-20.** The bout pause (M01). `true` sets `Time.timeScale = 0` (Animator, physics and `SlapIK`'s contact timer stop - so a windup cannot finish behind the menu); a freeze in progress keeps its remaining wall-clock time. `false` restores exactly what was running. Idempotent. The controller is the bout's **only** `timeScale` writer: a separate pause writer would be undone by the hitstop watchdog. `Apply` is ignored while paused; `OnDisable` restores time if the bout unloads paused |
| `HitstopController.IsPaused` | `bool { get; }` | |

**Consumed by:** M01 (timing), M09 (capture during hitstop), M11 (impact sync), M16 (shake).
**Stability:** `IFighterView` interface is stable; implementations will churn.

### Champions ([ADR-0022](../Decisions/ADR-0022-champion-roster-and-stats.md))

**Make one:** import a rigged FBX under `Assets/_Project`, open `Tools > Slap Mania > Characters > Champion Creator`, drop the FBX in, set name, card and stats, press **Create Champion**. Output:

| Asset | Path |
|---|---|
| Fighter prefab | `Prefabs/Fighters/Fighter_<id>.prefab` |
| Controller | `Animation/Fighter/Fighter_<id>.controller` |
| Rig profile (created once; tuning survives rebuilds) | `Data/Prototype/Fighter_<id>_Rig.asset` |
| Portrait (rendered unless given) | `Art/Portraits/Portrait_<id>.png` |
| Definition | `Data/Champions/Champion_<id>.asset` |
| Roster (appended; wired into `GameConfig.roster` if unset) | `Data/Champions/ChampionRoster.asset` |

**Moves ([ADR-0023](../Decisions/ADR-0023-authored-slap-clips-and-fighter-fit.md)):** states `Idle, Charge, Victory, Hit, Block, Slap_Right, Slap_Left` play the **authored slap set** in `Animation/Slap/` (`SlapClipAuthor`, `Tools > Slap Mania > Characters > Author Slap Clips`): square stance, open hand, 25-27° hip lean at contact, feet planted, no root motion. `Slap_Left` is `Slap_Forehand` on a mirrored state; contact is exactly `SlapClipAuthor.ContactSeconds` (500 ms = `windupMs`). `<FBX stem>@<State>.fbx` next to the FBX overrides a state for that champion; a pick in `MocapSelection` (a trimmed take copied to `Animations/Mocap/Selected/<Role>.fbx`) overrides it for every champion ([ADR-0024](../Decisions/ADR-0024-physical-animation-and-mocap-drop-in.md) addendum). Today Idle, Charge, Slap, Hit and Victory are captured; Block is authored. Either way the contact is measured. On top of whichever clip plays, `PhysicalAnimationLayer` springs the upper body (whip, follow-through, hit impulse). The Mixamo boxing clips are no longer used: they lunged through the table (DEBUG S-103). `Rebuild All Champion Fighters` re-runs the builder for the whole roster. **Props:** "Hide props" lists name prefixes switched off (Alucard: `sword`). Re-picking the same FBX loads its champion back into the window.

**In the bout:** `ChampionSpawner` instantiates the player's and the rival's prefabs at the scene fighters' poses (layer, parent, contact shadow carried over) and disables the scene ones. Each champion is scaled uniformly to `ChampionRoster.fighterHumanScale` (1.05; `FighterFit.UniformScale`, clamped ×0.5-×2) so every body fits the 0.36 m table at the ±0.42 m marks the same way (Hikaru ×0.848). A mirror match tints the rival crimson with instanced materials.

## 4. Configuration

### Hitstop

Authored in **milliseconds** (R8), not frames — a frame count means a different freeze at 30 and 60 fps. Asset: `FeelConfig` (`Data/Prototype/Feel.asset`), applied by `BoutController` on `ImpactFrame`.

| Field | Where | Type | Default | Sane range | Notes |
|---|---|---|---|---|---|
| `hitstopMs` | `FeelConfig` | int ms | **90** | 40 – 200 | Every impact |
| `critHitstopBonusMs` | `FeelConfig` | int ms | **40** | 20 – 100 | Added when `isCrit` |
| `finisherHitstopBonusMs` | `FeelConfig` | int ms | **120** | 60 – 400 | Added on the finisher slap (stacks with crit) |
| `recoilDamageForFull` | `FeelConfig` | float | **45** | 20 – 80 | Damage that gives a full-strength defender recoil |
| `_frozenTimeScale` | `HitstopController` | float | **0.02** | 0.01 – 0.05 | Not zero — zero breaks `AudioSettings.dspTime` progression |
| `_watchdogSeconds` | `HitstopController` | float s | **2** | — | Hard cap so time can never stick |

The FSM has its own Hitstop *state* length, `BoutConfig.hitstopMs` (M01). The two are independent: the state gates the next transition on realtime, the freeze here dips `timeScale` for the visuals.

### Screen shake

| Field | Type | Default | Sane range | Notes |
|---|---|---|---|---|
| `shakeTraumaPerDamage` | curve | 0.3 @10 → 0.7 @50 → 1.0 @80 | — | Maps finalDamage to trauma (0–1) |
| `shakeDecay` | float | **3.0** | 1.0 – 5.0 | Trauma/second decay rate |
| `maxShakeOffset` | float | **0.15** | 0.05 – 0.30 | World units; M16 uses this as its cap |

### Impact smear

| Field | Type | Default | Notes |
|---|---|---|---|
| `smearDuration` | float s | **0.12** | Held through hitstop, released on hitstop end |
| `smearIntensity` | curve | 0–1 over smearDuration | Vertex displacement magnitude |

**Invariants**
- `_frozenTimeScale > 0` — zero stops `dspTime`.
- `hitstopMs >= 0`; 0 disables the freeze without breaking anything.

The authoritative impact frame-by-frame timeline lives in [TUNING-BIBLE.md §5](../TUNING-BIBLE.md).

### Humanoid fighter (`FighterRigProfile`)

| Group | Fields (defaults) |
|---|---|
| States | `idleState` Idle, `chargeState` Charge, `hitState` Hit, `blockState` Block, `victoryState` Victory, `crossFadeMs` 120, `attacks[]` = {state, measured `contactMs`, `rightHand`} |
| Look-at IK | `lookWeight` 0.75, `lookBodyWeight` 0.15, `lookHeadWeight` 0.9, `lookClamp` 0.5 |
| Strike IK | `ikBlendInMs` 170, `ikHoldMs` 90, `ikBlendOutMs` 240, `ikPositionWeight` 1, `ikRotationWeight` 0.45, `handRotationOffset` (0,0,90), `strikeOffset` (0.11, −0.03), `cheekSideOffset` 0.08 (the palm lands on the cheek on the striking hand's side), `openHandWeight` 1 |
| Hit spring | `springFrequency` 5.5, `springDamping` 0.35, `impulseDegPerSecond` 950, `maxAngle` 55, `twistShare` 0.55; bone weights spine 0.08 / chest 0.18 / neck 0.3 / head 0.7 |
| Physical animation | `physicsWeight` 1, `torsoHz` 7, `neckHeadHz` 4.5, `upperArmHz` 5, `forearmHz` 4, `handHz` 3.2, `dampingRatio` 0.45, `maxDeviationDeg` 50, `hitImpulseDegPerSecond` 1100, `hitTwistShare` 0.6 (replaces the Hit spring row on built fighters) |
| Knock-out | `koVelocity` 3.2, `koUpVelocity` 1.1, `koHeadExtra` 2.6 (m/s) |

## 5. Events

**Emits**

| Event | Payload | When |
|---|---|---|
| `HitstopStarted` | `frames, timeScale` | Hitstop begins |
| `HitstopEnded` | — | Hitstop ends, normal time resumes |

**Consumes**

| Event | From | Reaction |
|---|---|---|
| `MatchSignal.TurnStart` | M01 | Switch active `IFighterView` pair |
| `MatchSignal.ChargeStart` | M01 | `PlayChargePose()` |
| `MatchSignal.WindupStart` | M01 | `PlayWindup()` |
| `MatchSignal.ImpactFrame` | M01 | `PlayImpactRecoil(intensity)`, start hitstop, add trauma |
| `MatchSignal.FinisherStart` | M01 | `PlayFinisher(type)` |
| `MatchSignal.BoutEnd` | M01 | `PlayVictoryPose()` / `PlayDefeatPose()` |

## 6. State machine

Presentation itself is reactive, not state-driven. It responds to signals. `HitstopController` has a small FSM:

```
Normal --Apply--> Frozen --frames elapsed--> Normal
```

## 7. Dependencies

- **Hard:** `SlapMania.Core` (signals, fighter slots), `SlapMania.Data`.
- **Soft:** M16 (camera shake via `AddTrauma`).
- **Unity APIs:** `Time.timeScale`, `Animator`, `Transform`, `MeshFilter` (for smear), `Awaitable`.
- **Must initialize after:** M00, M16 (camera rig exists).

## 8. Failure modes

| Failure | Detection | Behaviour | Player-visible? |
|---|---|---|---|
| `IFighterView` null | Assert at bout start | Use a fallback no-op implementation, log Error | Missing animations but game plays |
| Hitstop with 0 frames | Guard | Clamp to 1, log Warn | Barely perceptible freeze |
| `timeScale` never restored after hitstop | Watchdog — if hitstop exceeds 2 seconds wall clock | Force restore, log Error | Game resumes from frozen state |
| Animator trigger not found | `AnimatorFighterView` logs Warn | No animation plays | Visual glitch |
| Humanoid floats / stands ahead of its root | Avatar Hips mapped to the Blender `Armature` object (S-072) or clip roots "based upon original" (S-071) | `FighterImportSetup` remaps Hips and fixes clip root settings; `FighterBuilder` re-runs setup when it finds either | Yes — fighters interpenetrate |
| Edit-mode fighters overlap in the scene | Edit mode shows the FBX rest pose: hips 1.79 m up and 0.56 m forward of the root (S-073) | Expected. The Animator poses them in Play; judge placement only in Play | Editor only |
| KO ragdoll falls through the floor | A limb starts the KO inside a collider and is depenetrated downwards (S-074) | The podium is a table with open space under it, and the floor collider is 3 m thick | Yes — the loser vanishes |
| Bout plays with capsules (`PlaceholderFighterView`) instead of the humanoid | The fighter build failed in the prototype builder, e.g. the base FBX could not be found (S-085) | `BuildNonInteractive` throws and leaves the last scene alone. The interactive build falls back to capsules with a Console warning and a dialog note | Yes, in the prototype |

## 9. Debug hooks

- **Log channel:** `LogChannel.Presentation`
- **Overlay (Presentation tab):** current `IFighterView` implementation name, last animation triggered, hitstop active (frames remaining), current `Time.timeScale`, trauma level, smear active.
- **Cheats:** trigger any animation, force hitstop (with frame count slider), force shake, toggle smear.
- **Recorder events:** `AnimationTriggered`, `HitstopApplied`, `TraumaAdded`.

## 10. Tuning notes

**Hitstop is the most important 200 ms in the game.** It is the moment the player *feels* the slap land. Without it the impact is a blur that registers as "something happened." With it the screen freezes, the smear holds, the crowd roars, and the damage number pops. If you cut hitstop to save time, you cut the game's core satisfaction.

**`hitstopTimeScale` must never be exactly 0.** At `Time.timeScale = 0`, `AudioSettings.dspTime` stops advancing on some platforms, which breaks the BeatClock (M11) and can desync scheduled audio. `0.02` is perceptually frozen but keeps the audio clock alive. If hitstop needs to be truly frozen (e.g. for a dramatic finisher hold), use animation-level freezing (set Animator speed to 0) rather than `timeScale = 0`.

**The smear is a vertex-displacement mesh, not a post-process motion blur.** URP's full-screen Motion Blur blurs the entire frame, which is wrong — the *attacker's hand* moved, not the camera. What the player expects is the *recipient's face* deforming on contact. A smear mesh that stretches the defender's head vertices in the slap direction, held through hitstop, delivers exactly that effect at negligible GPU cost. The full-screen motion blur is reserved for the camera (M16) during finisher cutscenes where the camera itself whips.

**The clips step in hard; keep the space between the fighters open below the waist.** Measured in Play (S-074): the Charge clip ("Boxing", footwork baked into the pose) puts the charging fighter's knee at the centre line at 0.44 m high, and Attack_Hook puts a shin 0.12 m from it. That is why the podium is a table on legs, not a chest, and why a solid prop between the fighters would clip. A stationary charge clip would also fix it, and would read better (the fighters currently cross knees under the table).

**`PlaceholderFighterView` must look intentional, not broken.** It drives a capsule with color tinting (red flash on impact, green on charge, white on idle) and position lerps (recoil as a backward offset, windup as a forward lean). The capsule should read as a test dummy, not as a missing asset. Giving it a face decal (two dots and a line) costs five minutes and makes playtests dramatically more readable.

## 11. Open questions

- [ ] **Mobile LOD for the humanoid fighter.** The source FBX is ~175k triangles per fighter (Hair 82k, `High poly.001` 59k, Body 34k), drawn by the main camera, the damage cam and the shadow pass. That is about half of the 2.18 M triangles in the first Editor capture ([Perf/Captures/2026-09-26-editor-arena.md](../Perf/Captures/2026-09-26-editor-arena.md)). Decimate, or author an LOD, before any device capture is meaningful.

- [ ] Should hitstop freeze physics (`Physics.simulationMode`) or just `Time.timeScale`? TimeScale is simpler but affects all scripts; simulation mode is more surgical but may miss edge cases.
- [ ] Is the smear mesh a runtime-deformed copy of the head, or a pre-authored "squish" blendshape? Blendshape is cheaper and artist-friendly but requires the final mesh.
- [ ] Should `ISlapTimingSource` provide timing per animation clip, or should there be one set of durations shared across all characters?

## 12. Changelog

| Date | Commit | Change |
|---|---|---|
| 2026-11-09 | (pre-git) | **Real mocap in the fighters ([ADR-0024](../Decisions/ADR-0024-physical-animation-and-mocap-drop-in.md) addendum, S-106).** Idle, Charge, Slap, Hit and Victory now play trimmed captured takes chosen in `MocapSelection` (`Animations/Mocap/Selected/`, *Apply Mocap Selection*); Block stays authored. The filename-prefix scan is gone. `SlapIK`: `SetPlanted` holds the feet on the idle stance and keeps `plantedYawKeep` of the turn; `LeanIntoReach` covers the distance the capture lacked (`maxStrikeLeanDeg` 32). `PhysicalAnimationLayer.TrembleDeg` strains the held wind-up (`chargeTrembleDeg` 2.5). `chargeCrossFadeMs` 350. Verified in Play: feet drift 0, palm 0.09 m from the strike point at full IK weight, Alucard at 0.21-0.27 m of the table except the wind-up's leading hand at the table corner (0.40). **Hikaru's long arms still reach 0.5-0.9 m at tabletop height in the wind-up, slap and hit (numbers measured, not reviewed on device)** |
| 2026-11-07 | (pre-git) | **Physical animation + mocap drop-in ([ADR-0024](../Decisions/ADR-0024-physical-animation-and-mocap-drop-in.md)).** Owner: "animations look robotic, I need mocap-like anticipation and slap with simulated physics". New `PhysicalAnimationLayer` replaces `HitReactor` on built fighters: 13 upper-body bones chase the clip through Core `RotationSpring` springs (softer down the arm: whip, overshoot, torso lag), a slap kicks the slapped fighter's head/neck/trunk/arms, the striking arm is pinned by `SlapIK.StrikeWeight` (new `SlapIK.StrikingRight`). `FighterRigProfile` gains the physics group (`physicsWeight`, `torsoHz` 7, `neckHeadHz` 4.5, `upperArmHz` 5, `forearmHz` 4, `handHz` 3.2, `dampingRatio` 0.45, `maxDeviationDeg` 50, `hitImpulseDegPerSecond` 1100, `hitTwistShare` 0.6) and `maxWindupSpeedup` 1.35 (a long mocap anticipation plays faster before it is skipped). Shared mocap: FBX in `Animations/Mocap/` named `<Role>...` (Idle, Charge, Slap, Hit, Block, Victory) beats the authored clip; attacks re-measured each build. `RotationSpringTests` (14). Verified in Play: weak slap, head whips ~28° at 0.27 s and settles by ~0.8 s; palm at the cheek on the impact frame. **No mocap supplied yet; device perf not profiled; Hikaru's arms under the spring not measured against the table** |
| 2026-11-07 | (pre-git) | **Slaps, not punches; nobody in the table ([ADR-0023](../Decisions/ADR-0023-authored-slap-clips-and-fighter-fit.md), DEBUG S-103/104/105).** Owner: "characters go inside table … need slap animation instead of punch". New editor `SlapClipAuthor` writes six humanoid muscle-curve clips (foot-planting solver; based upon Original). `FighterBuilder` roles → `Slap_Right` / mirrored `Slap_Left`; stale `Attack_*` profiles re-derived; controller rebuilt **in place** (S-104: a delete+create left loaded prefabs with a null controller, fighters in bind pose). `SlapIK` + `FighterRigProfile.cheekSideOffset` (palm on the cheek). `FighterViewBase.StrikingHand` (Humanoid: the hand of the latest move). `ChampionSpawner` scales to `ChampionRoster.fighterHumanScale` via Core `FighterFit` (`FighterFitTests`). `ChampionCreator.RebuildAllFighters`. Verified in Play over MCP: Alucard vs Hikaru, Hikaru leans over the 0.36 m table and lands an open palm on Alucard's cheek, both behind their edges, heads 1.64-1.69 m. **Not verified on device; Drakonit not seen in a live bout** |
| 2026-11-07 | (pre-git) | **Champion textures (DEBUG S-102).** New editor `ChampionTextures`, run by the Champion Creator before the prefab build: extracts embedded materials, wires basecolor / normal / metallic images found next to the FBX, else generates a seamless procedural detail set (albedo + normal, seeded by id) for every material without a base map. Window: "Tint bare materials" + Base colour. Applied to Alucard (detail), Hikaru and Drakonit (detail + tint); portraits re-rendered. |
| 2026-11-07 | (pre-git) | **Portrait render fixed.** `PortraitRenderer` read `cam.targetTexture` directly, which comes back empty under URP: every portrait was blank. It now reads through `EndStaticPreview` (RGB24) and recovers alpha by rendering on black and on white (difference matting). Framing spans from below the neck to the model's top instead of aiming at the Head bone, which sits at the base of the skull and cropped tall heads. Alucard, Hikaru and Drakonit re-rendered. |
| 2026-10-25 | (pre-git) | **Champions and the Champion Creator ([ADR-0022](../Decisions/ADR-0022-champion-roster-and-stats.md)).** New `Data.ChampionDefinition` / `ChampionRoster`; editor `ChampionCreator` + window (`Tools > Slap Mania > Characters > Champion Creator`) and `PortraitRenderer`. `FighterBuilder.Build(id, fbx, hidePrefixes, ...)` generalises the Alucard pipeline: shared Mixamo clips retargeted via Humanoid, overrides `<stem>@<Role>.fbx`, contacts measured per rig; `BuildAlucard` is now `Build("Alucard", ...)`. `FighterImportSetup.SetUpHumanoidBase` / `BaseReady` split out; an override clip without a `mixamo.com` take uses its first take. App `ChampionSpawner` swaps the scene fighters for the chosen prefabs at bout start. |
| 2026-09-19 | (pre-git) | Created (Draft) with IFighterView interface and hitstop design |
| 2026-09-26 | (pre-git) | **P5 partial.** `SlapMania.Presentation` asmdef created (`-> Core, Data, Gameplay, Logging`; Snapshot/Audio/URP/Timeline refs deferred until those modules exist). `IFighterView` landed with `FighterViewBase : MonoBehaviour` (serializable so scenes wire a view by reference), plus `PlaceholderFighterView` (capsule colour-tint + positional punch on unscaled time, zero per-frame alloc via a reused `MaterialPropertyBlock`) and `AnimatorFighterView` (drives an `Animator` from the same calls; missing Animator/trigger logs Warn and no-ops). `HitstopController` freezes `Time.timeScale` (never 0) for a wall-clock duration with a watchdog restore; the sim clock reads realtime so the FSM's own hitstop timer stays honest. **Known simplifications vs §3:** `PlayFinisher()` is `void`, not `Awaitable PlayFinisher(FinisherType)` — the FSM uses a fixed finisher duration and does not await the view; screen shake / `CameraRig.AddTrauma`, the impact smear mesh, and `ISlapTimingSource` are not implemented in this pass. Bars/animation clip wiring are Inspector values — **unverified from code** (R11). |
| 2026-09-26 | (pre-git) | **Feel moved into an asset ([ADR-0011](../Decisions/ADR-0011-adopt-v2-design-prototype-first.md)).** New `FeelConfig` (`SlapMania.Data`, referenced as `GameConfig.feel`) replaces `BoutController`'s serialized `_hitstopSeconds` / `_critHitstopBonusSeconds` / `_recoilDamageForFull`, so Play Mode tuning persists. Authored in ms (R8): `hitstopMs` 90, `critHitstopBonusMs` 40, and a **new** `finisherHitstopBonusMs` 120 so the lethal slap freezes longer; `recoilDamageForFull` 45. §4 hitstop table rewritten from the frame-based draft to the real fields. The prototype builder (M13) now generates both capsule fighters (with a visor showing facing; idle tints cyan for you, gold for the opponent; windup tint set to white so it reads against gold) — the wiring is no longer unverified for the prototype scene. Shake, smear and `ISlapTimingSource` still not implemented. |
| 2026-09-26 | (pre-git) | **Humanoid fighter.** `HumanoidFighterView`, `SlapIK` (look-at and strike IK in `OnAnimatorIK`), `HitReactor` (spine-to-head spring), `RagdollController` (KO only), `FighterPortrait`; tuning in the new `FighterRigProfile` asset. `IFighterView` grew `PlayKnockout`, `BindOpponent`, `HeadAnchor`, `GetStrikePoint`; `PlayWindup` takes `secondsToContact` and `PlayImpactRecoil` a hit direction. `HitstopController.Apply(float seconds)` also scales `fixedDeltaTime`. Import fixes S-071 (clip roots) and S-072 (Hips mapping). §2, §3, §4, §8, §10 updated. Shake, smear and `ISlapTimingSource` still not implemented (the interim camera kick is `CameraRig`, M16). |
| 2026-09-26 | (pre-git) | **Fighters slap across the arena podium.** Roots moved from ±0.36 to ±0.49 m (measured contact geometry). Each fighter gets a multiplicative contact-shadow quad (M15). The KO ragdoll fell through the new floor (S-074): a knee inside the podium's box collider was depenetrated through the thin floor slab. Fixed by making the podium a table and the floor collider 3 m thick; verified in Play (the KO'd fighter came to rest on the floor, hips at 0.14 m). S-073 records that edit mode shows the FBX rest pose. |
| 2026-10-20 | (pre-git) | **`HitstopController.SetPaused`** for the bout pause (M01/M10): `timeScale` 0 while paused, a running freeze keeps its remaining time (`_restoreAt` shifted by the paused span), `Apply` ignored while paused, and `OnDisable` restores the pre-pause scale so leaving a paused bout can never carry a stopped clock into the next scene. Unscaled-time presentation (`CameraRig` fly-in, `DamageCam`, `ArenaDirector` flash, `HpBar`) keeps running on the pause; it is cosmetic and covered by the pause menu. §3 updated. |
| 2026-10-23 | (pre-git) | **Humanoid fighters are back in `Proto_Bout` (S-085).** Every prototype rebuild since the base FBX moved to `Characters/FBX/Alucard/` on 2026-09-26 had quietly produced capsules. The editor tooling (M13) now finds the base model by name, and unattended builds refuse the capsule fallback. No runtime code changed. §2, §8 updated. Verified in Play: both views `HumanoidFighterView`, bout to BoutEnd, KO ragdoll at rest on the floor (hips y = 0.14 m). |
