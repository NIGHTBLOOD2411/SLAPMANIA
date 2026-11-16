# Slap Mania - Documentation Index

Last updated: 2026-09-26

Every doc in the repo, with its status. A module doc's status here must match the
`status:` field in its own YAML front-matter. The `DocStatusWindow` editor tool
(planned, M13) parses the front-matter and flags mismatches and staleness.

**Status ladder:** `Draft` (no code yet) -> `InProgress` (code exists, doc lags) ->
`Implemented` (doc matches code) -> `Stable` (unchanged 30+ days, tested) ->
`Deprecated`.

## Top level

| Doc | Purpose | Status |
|---|---|---|
| [WORKFLOW.md](WORKFLOW.md) | The dev -> test -> debug -> polish -> ship loop; Definition of Done (R12) | Draft |
| [TESTING.md](TESTING.md) | Test pyramid, R3 boundary matrix, determinism, allocation test, runners | Draft |
| [POLISH.md](POLISH.md) | Game-feel & pre-ship polish checklist | Draft |
| [RELEASE-CHECKLIST.md](RELEASE-CHECKLIST.md) | Pre-ship gate (R12): build integrity, perf, GLES3, secrets, go/no-go | Draft |
| [ARCHITECTURE.md](ARCHITECTURE.md) | Assembly graph, scene composition, life of one slap | Draft |
| [GLOSSARY.md](GLOSSARY.md) | Canonical vocabulary, enforced in code identifiers | Draft |
| [CONVENTIONS.md](CONVENTIONS.md) | Naming, namespaces, folders, asmdefs, commit format | Draft |
| [BUILD-AND-RUN.md](BUILD-AND-RUN.md) | Editor setup, defines, build profiles, device deploy | Draft |
| [TUNING-BIBLE.md](TUNING-BIBLE.md) | Every designer-facing number in one place | Draft |
| [ROADMAP.md](ROADMAP.md) | Module status board and build order | Draft |
| [DEBUG.md](DEBUG.md) | The debug handbook - symptoms, known issues, perf, platforms | Draft |
| [CONTENT-MANIFEST.md](CONTENT-MANIFEST.md) | Audio / art / VFX shopping list | Draft |
| [Perf/BASELINE.md](Perf/BASELINE.md) | Performance targets and capture log | Draft |
| [Reference/Arena/](Reference/Arena/README.md) | Arena look reference (concept frames for M14/M15/M17) | Draft |

## Modules

Numbered so alphabetical order equals the order data flows through a single slap.
That ordering is itself documentation.

| # | Doc | Owns | Status |
|---|---|---|---|
| M00 | [Bootstrap](Modules/M00-Bootstrap.md) | Boot order, service registry, config loading | InProgress |
| M01 | [MatchFlow](Modules/M01-MatchFlow.md) | Bout/round/turn FSM, win/lose, finisher gating | InProgress |
| M02 | [Input](Modules/M02-Input.md) | Raw touch -> `TapEvent` with device timestamp, de-dupe | InProgress |
| M03 | [PowerCharge](Modules/M03-PowerCharge.md) | Tap rate -> power 0..1, decay, overcharge | InProgress |
| M04 | [TimingMultiplier](Modules/M04-TimingMultiplier.md) | Marker motion, zone table, stop resolution | InProgress |
| M05 | [DamageResolution](Modules/M05-DamageResolution.md) | power x multiplier x modifiers, crit, finisher test | InProgress |
| M06 | [Health](Modules/M06-Health.md) | HP state, clamping, death detection | InProgress |
| M07 | [OpponentAI](Modules/M07-OpponentAI.md) | AI power/timing selection, difficulty, seeded RNG | InProgress |
| M08 | [Presentation](Modules/M08-Presentation.md) | Hitstop, shake, impact FX, animation sync | InProgress |
| M09 | [SnapshotCapture](Modules/M09-SnapshotCapture.md) | Photo camera, atlas, gallery storage | InProgress (pre-atlas gallery, ADR-0016) |
| M10 | [UIScreenFlow](Modules/M10-UIScreenFlow.md) | Screens, transitions, safe area, HUD | InProgress |
| M11 | [Audio](Modules/M11-Audio.md) | Mixer, music, announcer VO, crowd, SFX, BeatClock | Draft |
| M12 | [SaveProgression](Modules/M12-SaveProgression.md) | Save file, unlocks, migration | Draft |
| M13 | [DebugTools](Modules/M13-DebugTools.md) | Logging, overlay, recorder, cheats, editor tools | InProgress |
| M14 | [ArenaLighting](Modules/M14-ArenaLighting.md) | Party light rig, cues, beat sync | InProgress |
| M15 | [ArenaVFX](Modules/M15-ArenaVFX.md) | Particles, portals, ring FX, impact FX; the generated arena set | InProgress |
| M16 | [CameraDirector](Modules/M16-CameraDirector.md) | Director, keyframe sequences, trauma shake, DoF lens, runtime recorder | InProgress |
| M17 | [ArenaDisplays](Modules/M17-ArenaDisplays.md) | Jumbotron live feeds, replays, hype graphics | InProgress |
| M18 | [Cutscenes](Modules/M18-Cutscenes.md) | Entrances, transitions, finisher cinematics | **Draft - stub** |
| M19 | [AssetDelivery](Modules/M19-AssetDelivery.md) | Addressables + Unity CCD, local vs remote groups, per-tier variants | Draft |

**Stubs** (M18) carry the verified engine constraints and the open questions, not a
guessed design. They are honest placeholders awaiting a dedicated design pass - see
[ROADMAP.md](ROADMAP.md). M14, M15 and M17 had theirs on 2026-09-26 with the generated
arena ([ADR-0012](Decisions/ADR-0012-arena-mobile-fakes.md)).

## Decisions (ADRs)

| ADR | Title | Status |
|---|---|---|
| [0001](Decisions/ADR-0001-docs-outside-assets.md) | Docs live outside `Assets/` | Accepted |
| [0002](Decisions/ADR-0002-assembly-graph.md) | The assembly graph and its one-way edges | Accepted (list superseded by 0010) |
| [0003](Decisions/ADR-0003-gameplay-math-is-pure-csharp.md) | Gameplay math is MonoBehaviour-free | Accepted |
| [0004](Decisions/ADR-0004-ms-based-timing.md) | Timing is authored in milliseconds | Accepted |
| [0005](Decisions/ADR-0005-no-cinemachine.md) | Hand-rolled camera rig, no Cinemachine | Accepted (revisit at M16) |
| [0006](Decisions/ADR-0006-snapshot-atlas.md) | One persistent snapshot atlas, not per-slap textures | Accepted |
| [0007](Decisions/ADR-0007-no-third-party-packages.md) | No third-party packages in v1 | Accepted (Addressables clause superseded by 0008; editor-tooling clause amended by 0009) |
| [0008](Decisions/ADR-0008-addressables-and-ccd.md) | Addressables + Unity CCD for asset streaming | Accepted |
| [0009](Decisions/ADR-0009-unity-mcp-editor-tooling.md) | Unity MCP bridge as build-excluded editor tooling | Accepted (amended 2026-09-26: HTTP transport) |
| [0010](Decisions/ADR-0010-assembly-graph-refinement.md) | Assembly graph refinement (Input/Presentation/UI/App split) | Accepted |
| [0011](Decisions/ADR-0011-adopt-v2-design-prototype-first.md) | Adopt the V2 design organically, prototype-first | Accepted |
| [0012](Decisions/ADR-0012-arena-mobile-fakes.md) | The arena is built from mobile-true fakes, generated from code | Accepted |
| [0013](Decisions/ADR-0013-ui-toolkit-front-end-from-mockups.md) | The front end is UI Toolkit, generated from the V2 mockups | Accepted |
| [0014](Decisions/ADR-0014-hold-ring-attack-mode.md) | The hold-ring attack is an Inspector-selectable attack mode | Accepted; amended 2026-10-23 (default TapMarker) |
| [0015](Decisions/ADR-0015-v1-page-behind-a-tuning-switch.md) | The V1 page ships beside V2, chosen by a Tuning Hub switch | Accepted |
| [0016](Decisions/ADR-0016-slap-gallery-pre-atlas-capture.md) | Slap gallery: one RenderTexture per photo before the atlas | Accepted |
| [0017](Decisions/ADR-0017-aim-cue-and-spam-gap.md) | Mash carry-over: an AIM cue and a spam gap in the timing meter | Accepted |
| [0018](Decisions/ADR-0018-ai-ladder.md) | AI ladder: difficulty tiers chosen per bout from the player's level | Accepted |
| [0019](Decisions/ADR-0019-brace-tap-defence.md) | Brace: a one-tap defence on the rival's turn, through DamageInput.reduction | Accepted |
| [0020](Decisions/ADR-0020-camera-keyframe-tracks-and-runtime-recorder.md) | Camera keyframe tracks, a layered CameraDirector, and a runtime recorder | Accepted |
| [0021](Decisions/ADR-0021-hud-attention-budget.md) | HUD attention budget: a tunable per-phase budget, one headline at a time | Accepted |
| [0022](Decisions/ADR-0022-champion-roster-and-stats.md) | Champion roster: FBX-built champions, runtime spawn, PWR/CHN/STA change the bout | Accepted |
| [0023](Decisions/ADR-0023-authored-slap-clips-and-fighter-fit.md) | Authored slap clips (no Mixamo boxing), one fighter size, table fitted to measurements | Accepted |
| [0024](Decisions/ADR-0024-physical-animation-and-mocap-drop-in.md) | Physical animation spring layer (whip, follow-through, hit impulse); shared mocap drop-in folder | Accepted |

## Tooling

| Doc | Purpose |
|---|---|
| [../.claude/README.md](../.claude/README.md) | The Claude toolchain: subagents, slash commands, hooks, settings, MCP |
| [../AGENTS.md](../AGENTS.md) | Pointer for non-Claude agents -> CLAUDE.md |
| [../Tools/mockup2uxml/mockup2uxml.py](../Tools/mockup2uxml/mockup2uxml.py) | Design-canvas mockups -> front-end UXML, both pages V2 and V1 (M10, ADR-0013, ADR-0015); `compare.py` scores fidelity, `ttfmetrics.py` measures text for wrapping |
| [Reference/Mockups/](Reference/Mockups/) | The V1/V2 mockup sources and the conversion report |

## Templates

- [Modules/_TEMPLATE.md](Modules/_TEMPLATE.md)
- [Decisions/_TEMPLATE.md](Decisions/_TEMPLATE.md)
