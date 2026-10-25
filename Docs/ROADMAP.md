# Slap Mania — Roadmap

Status: Draft — Last verified 2026-09-26

## Module status board

Mirrors `Docs/INDEX.md`. The `DocStatusWindow` editor tool will generate this automatically once M13 lands; until then it is maintained by hand.

| # | Module | Doc status | Code status | Blocked on |
|---|---|---|---|---|
| M00 | Bootstrap | InProgress | P5 partial (GameRoot) | Phase 0 |
| M01 | MatchFlow | InProgress | P3 done (FSM) | — |
| M02 | Input | InProgress | P5 partial (InputSystemTapSource) | — |
| M03 | PowerCharge | InProgress | P3 done (`PowerMeter`) | — |
| M04 | TimingMultiplier | InProgress | P3 done (`TimingMeter`, `MarkerOscillator`, `TimingZoneLayout`); V2 hold-ring attack mode landed 2026-10-23 (`HoldRingMeter`, [ADR-0014](Decisions/ADR-0014-hold-ring-attack-mode.md)), not yet A/B'd on a device | device A/B vs TapMarker |
| M05 | DamageResolution | InProgress | P3 done (`DamageResolver`) | — |
| M06 | Health | InProgress | P3 done (`HealthTracker`) | — |
| M07 | OpponentAI | InProgress | P3 done (`AiTapSource`) | — |
| M08 | Presentation | InProgress | P5 partial (humanoid fighter: IK, hit spring, KO ragdoll; hitstop) | M01 |
| M09 | SnapshotCapture | Draft | Not started | M08 |
| M10 | UIScreenFlow | InProgress | P5 partial (V2 in-match HUD, 8 screens) | M00 |
| M11 | Audio | Draft (fully specified) | Not started | M00 |
| M12 | SaveProgression | Draft (outline) | Not started | deferred past v1 |
| M13 | DebugTools | InProgress | Editor tooling landed (prototype, V2 HUD, fighter and arena builders; Tuning Hub); Diagnostics not started | M00 |
| M14 | ArenaLighting | InProgress | Arena rig landed (4 spots + moon, `ArenaLookConfig`, impact flash, sweep, tiers) | M11 for beat sync |
| M15 | ArenaVFX | InProgress | Ambient fakes + generated set landed; gameplay VFX not started | device overdraw capture |
| M16 | CameraDirector | InProgress | Interim `CameraRig` (intro fly-in, impact kick) | **design pass** for shots + cutscene integration |
| M17 | ArenaDisplays | InProgress | Damage Cam jumbotron + LED ribbon/boards landed | M11 for beat pulse |
| M18 | Cutscenes | **Stub** | Not started | **design pass** |
| M19 | AssetDelivery | Draft | Not started | package add (deferred to P8) |

---

## Immediate next step: finish the spectacle design pass

> **2026-09-26:** M14, M15 and M17 had their pass and first landing with the generated arena ([ADR-0012](Decisions/ADR-0012-arena-mobile-fakes.md)). It leads with fakes and tiering, as the risk row below demands. Still open from the list below:
> - device frame-budget captures (overdraw, crowd shader, damage cam);
> - beat sync (M11);
> - gameplay VFX, portals and the `SnapshotOnly` layer;
> - the M16 shot library.

Four modules were honest stubs because the design work was interrupted. They carry the verified engine constraints and the open questions, not a guessed design. Before any of their code is written, they need a dedicated pass covering:

- **M14 ArenaLighting** — how many real realtime lights a portrait-mobile URP frame can actually afford; the faking toolkit (baked lightmaps, animated emissive via `MaterialPropertyBlock`, light cookies, fake volumetric cones) ranked by cost; the concrete light rig; a beat-synced cue system; the flash-rate cap and accessibility toggle; and the rule that keeps fighters legibly lit no matter what the party lights are doing.
- **M15 ArenaVFX** — Shuriken versus VFX Graph given the GLES3 fallback; the overdraw budget (overdraw, not particle count, is what kills tile-based mobile GPUs); the pooled `VfxService`; the full effect catalogue including multiversal portals and the `SnapshotOnly`-layer effects that appear only in photographs.
- **M17 ArenaDisplays** — how many jumbotrons at what resolution and update rate; whether to reuse the main camera's output, run staggered low-rate cameras, or display the snapshot atlas as instant replays; the RT memory budget on top of the existing 15.5 MiB.
- **M18 Cutscenes** — the Timeline-animates-a-target-transform pattern, the cutscene catalogue, transition types, skippability, and runtime binding so one Timeline serves any fighter pair.
- **M16 CameraDirector** — settle whether the hand-rolled rig still holds once entrances, cutscenes and jumbotron feeds are in scope, and specify the motion-blur-on-hit approach (a smear mesh on the recipient blurs the thing that actually moved; URP's full-screen Motion Blur is both expensive and aimed at the wrong target).

---

## Build order

Two sequencing decisions carry most of the value:

- **Debug infrastructure lands before the gameplay it debugs** (P2 and P4 before P5). Tooling built after the bugs appear gets built under pressure, and gets built badly.
- **The snapshot system lands before any art** (P6 before P8). It is the signature feature and it carries all the platform risk — Render Graph, GLES3, sRGB, flip, memory. A fundamental failure must surface while the design can still absorb it.

| Phase | Deliverable | Gate |
|---|---|---|
| **P0** | Git init, template strip, project settings, portrait lock, layers and tags, TMP Essentials, Build Profiles with defines, Enter Play Mode Options on, package pruning | Clean `git log`; project opens and builds empty |
| **P1** | The `Docs/` tree — **done 2026-09-19** | Every module doc exists; stubs honestly marked |
| **P2** | `Logging` + `SlapLog` + `CrashLogger` + `SlapAssert` + the asmdef graph + `DebugSettings` | A forced exception writes a log file on device |
| **P3** | `Core` + `Data` + `Gameplay` — all math, the FSM, `AiTapSource`, EditMode tests. **No MonoBehaviour anywhere** | A full duel terminates in the Test Runner with zero scene content |
| **P3-done** | 2026-09-19 — `Logging`+`Core`+`Data`+`Gameplay` asmdefs and code shipped. `MatchDirector` FSM runs the full loop (Charge→Timing→Resolve→Windup→Impact→Hitstop→Snapshot→[Finisher→FinisherSnapshot]?→TurnEnd/KO/BoutEnd). EditMode tests cover it. |
| **P4** | `Diagnostics` — overlay, gesture, `DebugStateBoard`, `MatchRecorder` | Overlay opens on device; a match dumps readable JSON |
| **P5** | Boot + Bout scenes, `SlapControls`, `PlaceholderFighterView`, the real HUD | **Playable capsule duel on device, portrait, 60 fps.** Measure real touch latency here and set `displayLatencyBias` |
| **P5-wip** | 2026-09-26 — **scripts landed** for the MonoBehaviour layer: `SlapMania.Input` (`InputSystemTapSource`), `SlapMania.Presentation` (`IFighterView`, `PlaceholderFighterView`, `AnimatorFighterView`, `HitstopController`), `SlapMania.UI` (`BoutHud`), `SlapMania.App` (`UnityClock`, `GameRoot`, `BoutController`), and `GameConfig` in `SlapMania.Data`. Assembly split per [ADR-0010](Decisions/ADR-0010-assembly-graph-refinement.md). **Still pending (needs the Unity Editor open):** `Boot.unity` + `Bout_Arena01.unity`, the `Data/*.asset` + `Resources/GameConfig.asset` config assets, capsule fighter prefabs, HUD canvas wiring, build-settings scene list, EditMode green re-check, and the on-device latency pass. |
| **P5-proto** | 2026-09-26 — **Playable prototype tooling** ([ADR-0011](Decisions/ADR-0011-adopt-v2-design-prototype-first.md)). `Tools > Slap Mania > Prototype > Build Playable Prototype` generates what P5-wip listed as pending and needing the Editor: `Boot.unity` + `Proto_Bout.unity`, the `Data/Prototype/*.asset` configs + `Resources/GameConfig.asset`, capsule fighters, the full HUD canvas (TMP), the build-settings scene list, and a Tuning Hub. MatchDirector pacing and the presentation feel moved into assets (`BoutConfig` ms fields, new `FeelConfig`). **Still pending:** run the builder in the Editor, EditMode green re-check, on-device latency pass (`displayLatencyBias`). **Next:** the V2 hold→release ring attack as a switchable attack mode, then defence, Hype/Signature, rounds — one module at a time (ADR-0011 §3). |
| **P6** | Snapshot system + gallery + the in-moment flash and polaroid | **Grey card reads 128 ± 3 on both a Vulkan and a GLES3-forced build; memory delta ≤ 16 MiB after 16 slaps** |
| **P7** | Camera rig, hitstop, shake, damage numbers, audio banks, the looping tap tick | **VERTICAL SLICE — go/no-go.** If it is not fun with capsules, art will not fix it |
| **P8** | Arena spectacle: lighting rig, VFX, jumbotrons, cutscenes, finisher cinematics | Frame budget held on a mid-tier device with the full show running |
| **P9** | Editor tooling (zone editor, tuning window, doc board, snapshot window), cheat panel | The zone editor flags a non-tiling table |
| **P10** | First device perf capture into `Docs/Perf/Captures/`; tuning pass | **The tuning pass requires zero recompiles.** If it needs one, the data-driven design failed |
| **P8-M19** | AssetDelivery: adopt Addressables + CCD per [ADR-0008](Decisions/ADR-0008-addressables-and-ccd.md), stand up the group/label taxonomy from [M19](Modules/M19-AssetDelivery.md), pin catalog per Build Profile, ship the offline `Local_Core` set | `Local_Core` boots the tutorial with airplane mode on; a fresh install downloads the `preload` label under 30 s on a mid-tier phone |
| **P11** | Meta: ladder, coins, upgrades, save with atomic writes and migration | Full loop persists across an app kill |

Meta (M12) is designed in the docs now and built after the vertical slice, per the single-bout-first scope decision.

---

## Weekly maintenance

A ten-minute sweep, every week:

1. Open `Tools -> Slap Mania -> Doc Status`. Clear every red row.
2. Promote `Predicted` symptom rows in `DEBUG.md` to `Confirmed` or `Wrong`.
3. Close verified `KI-####` entries.
4. Check the `Time lost` column — if one module dominates, it is asking to be refactored.

If after a month the doc status board is all red, **cut the module template down to sections 1, 3, 4, 9, 10 and 12 rather than abandoning it.** A maintained six-section doc beats an unmaintained twelve-section one.

---

## Risks

| Risk | Mitigation |
|---|---|
| Doc rot — twelve sections across eighteen modules is a lot of surface | The table-shaped template (cheap edits), the same-commit rule, and the Doc Status window making staleness visible. Fallback: cut to six sections |
| The core loop is not fun | The vertical slice at P7 is an explicit go/no-go, deliberately reached with capsules and before any art spend |
| Snapshot feature fails on real hardware | Built at P6 against capsules, with a hard device gate on both Vulkan and GLES3, and a `NullSnapshotCapture` fallback so the victory screen works regardless |
| The spectacle brief (party lighting + heavy VFX + jumbotrons + motion blur) does not fit a portrait mobile frame budget | This is the largest open risk. The M14/M15/M17 design passes must lead with the frame budget and be explicit about what is faked. Device tiering is mandatory, not optional |
| Audio DSP cost exceeds budget | The three-tier fallback is part of the design, not an afterthought: the Low tier drops to a single pre-mixed music loop and bypasses reverb, taking DSP from ~14 % to ~5.5 % |
| Overlay gesture collides with rapid tapping or URP's Rendering Debugger | The FSM-state gate (three fingers only outside Charge and Timing) is what makes any gesture safe. Verify URP's default gesture before choosing |
