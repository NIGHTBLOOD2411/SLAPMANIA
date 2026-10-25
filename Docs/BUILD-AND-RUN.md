# Slap Mania — Build and Run

Status: Draft — Last verified 2026-09-26

## Editor

**Unity `6000.3.22f1` exactly** (revision `1c726e1fb402`). Pinned by `ProjectSettings/ProjectVersion.txt`. Do not open in another version — Unity will silently upgrade the project and the diff is unreviewable.

Install via Unity Hub with the **Android Build Support** (including OpenJDK and Android SDK/NDK Tools) and, on macOS, **iOS Build Support** modules.

## First-time setup

1. Clone the repo. `Library/` is **not** committed (it measures 1.9 GB and is a machine-local, non-deterministic import cache) — the first open will take several minutes while Unity rebuilds it. That is expected.
2. Reproducibility comes from three committed files, not from `Library/`: `Packages/manifest.json`, `Packages/packages-lock.json` and `ProjectSettings/ProjectVersion.txt`.
3. **Git LFS** — `git lfs install` once per machine. Art and audio binaries are LFS-tracked via `.gitattributes`.
4. **Unity YAML merge driver**, once per machine:
   ```
   git config merge.unityyamlmerge.name "Unity SmartMerge"
   git config merge.unityyamlmerge.driver '"C:/Program Files/Unity/Hub/Editor/6000.3.22f1/Editor/Data/Tools/UnityYAMLMerge.exe" merge -p %O %B %A %A'
   ```
5. **Import TMP Essentials** — `Window > TextMeshPro > Import TMP Essential Resources`. uGUI 2.0 folds TextMeshPro in, but the resources are not present in the repo and every TMP component errors until this runs. Hard blocker for any UI work.
6. Confirm **Enter Play Mode Options** is enabled (`Edit > Project Settings > Editor`). Iteration drops to near-instant, and the static-state discipline it forces is cheap to maintain from the start and miserable to retrofit.

## Play the prototype

[ADR-0011](Decisions/ADR-0011-adopt-v2-design-prototype-first.md). One menu command builds everything the bout needs; nothing is hand-wired.

1. `Tools > Slap Mania > Prototype > Build Playable Prototype`. If TMP Essentials are missing (setup step 5), it offers the import instead — press **Import** in the package dialog, then run the command again.
2. It creates:
   - `Assets/_Project/Scenes/Boot.unity` and `Proto_Bout.unity`;
   - the configs in `Assets/_Project/Data/Prototype/` and `Assets/_Project/Resources/GameConfig.asset`;
   - the humanoid fighter prefab (M08);
   - the **generated arena** ([ADR-0012](Decisions/ADR-0012-arena-mobile-fakes.md)): textures and meshes in `Art/Arena/Generated/`, materials in `Art/Arena/Materials/`, and `ArenaLook.asset` + `ArenaPost.asset` in `Data/Prototype/`. It bakes the arena reflection probe last, so a build takes noticeably longer than before (texture painting plus the bake).

   It puts Boot and Proto_Bout at Build Settings index 0 and 1, and opens Proto_Bout with the **Tuning Hub** selected.
3. Set the Game view to a portrait resolution (e.g. **1080x1920**).
4. Press **Play**. `Tools > Slap Mania > Prototype > Play From Boot` is on by default, so Play starts at Boot, which loads Proto_Bout additively, the same path as a device build. Turn it off to play Proto_Bout on its own (the `BoutController` then uses its serialized `GameConfig`).
5. Controls — tap, left-click or Space: **YOUR TURN / TAP FAST**, mash to charge power. Then the white **closing ring** shrinks over the rival's face: tap as it touches the gold band (that is the M04 marker, drawn the V2 way). **REMATCH** on the result card reloads the bout.
6. Tune: select `Tuning Hub` (or `Tools > Slap Mania > Prototype > Select Tuning Hub`).
   - Config assets are drawn inline and **keep Play Mode edits**: GameConfig and its parts, the fighter's rig profile, **ArenaLook** (lights, fog, flash) and **ArenaPost** (the colour grade).
   - Scene components (camera rig, fighter views, `ArenaDirector`, `DamageCam`) revert like any scene edit.
   - Arena surface looks are the materials in `Art/Arena/Materials/`, edited directly; they also persist.
7. Arena tools:
   - `Tools > Slap Mania > Prototype > Regenerate Arena Art` repaints the textures only.
   - `Reset Arena Look` deletes the arena materials, ArenaLook and ArenaPost after a confirm, so the next build recreates the defaults.

### The front end (menus)

[ADR-0013](Decisions/ADR-0013-ui-toolkit-front-end-from-mockups.md). The menus are UI Toolkit screens generated from the Design-canvas mockups.

1. When the mockups change, run `python Tools/mockup2uxml/mockup2uxml.py` from the repo root.
   - It converts both canvas pages: V2 into `Prefabs/UI/Screens/*.uxml`, V1 into `Prefabs/UI/Screens/V1/*.uxml`
     ([ADR-0015](Decisions/ADR-0015-v1-page-behind-a-tuning-switch.md)). `--page V1` converts one page;
     only a run over both pages prunes stale icons and writes the report.
   - It regenerates `Art/UI/Icons/*.svg` and `Art/UI/Mockup/Masked/*.png`.
   - It updates `Docs/Reference/Mockups/CONVERSION-REPORT.md`.
   - Do not hand-edit the UXML.
2. `Tools > Slap Mania > Front End > Build Front End Scene` (unattended:
   `SlapMania.Editor.FrontEnd.FrontEndSceneBuilder.BuildNonInteractive`). It generates `FrontEnd.unity`, points Boot at it and inserts it after Boot in Build Settings.
3. Play (Play From Boot). The flow is:
   - Title -> Loading -> Main Menu;
   - **PLAY RANKED** -> finding opponent -> walkout -> tale of the tape -> the bout;
   - the result card's **MAIN MENU** returns to the menus.

   Back is Escape or Android back.

   **The V1 page:** set Tuning Hub > FRONT END > Menu page to `V1` (`GameConfig.frontEndPage`), then Play.
   The flow is Splash -> Loading -> Home -> SLAP NOW -> Matchmaking -> VS Intro -> the bout (the V2
   HUD), and MAIN MENU returns to V1 Home.
4. Tune the motion in `Data/UI/UiMotion.asset` (also inside GameConfig in the Tuning Hub). Edits persist.
5. Fidelity check (optional):
   1. Render a mockup with headless Edge: `msedge --headless=new --force-device-scale-factor=2 --window-size=430,932 --virtual-time-budget=3000 --screenshot=m.png file:///<preview>.html`.
      The preview is a copy of the mockup with `/_blob/<hash>` pointing at `Art/UI/Mockup/*` (the tool's `BLOBS`)
      and the Google Fonts `<link>` replaced by `@font-face` rules on the files in `Art/Fonts`, so the
      render does not depend on the network.
   2. Capture the same screen from an 860x1864 Game view (`PlayModeWindow.SetCustomRenderingResolution`), shown
      with `ScreenManager.ResetTo(id, animate: false)` and its tweens stopped.
   3. Run `python Tools/mockup2uxml/compare.py m.png u.png out.png`.

Fonts (all SIL Open Font License; keep each folder's `OFL.txt` if they ship; CONTENT-MANIFEST, Fonts):
- **Barlow / Barlow Condensed** (`Art/Fonts/Barlow/`, from google/fonts): the HUD, the arena signage and the V2 menus.
- **Cabin** and **Archivo Black** (`Art/Fonts/Cabin/`, `Art/Fonts/ArchivoBlack/`, Google Fonts static TTFs): the V1 menus.

Re-running the builder regenerates both scenes (manual edits to them are lost) but **never overwrites a config asset**, so tuning survives.

Unattended (CI, batchmode, the Unity MCP bridge): call `SlapMania.Editor.PrototypeSceneBuilder.BuildNonInteractive` — e.g. `Unity.exe -batchmode -projectPath "C:/Unity/SlapMania 2.0" -executeMethod SlapMania.Editor.PrototypeSceneBuilder.BuildNonInteractive -quit`. It shows no dialogs and throws instead (Play Mode, missing TMP Essentials, unsaved open scenes, a humanoid fighter that cannot be built - it never falls back to capsules unattended, S-085). When driving Play Mode through MCP with Unity in the background, see DEBUG S-069.

## Build Profiles and scripting defines

Defines are managed through Unity 6 **Build Profiles** (`Assets/_Project/Settings/BuildProfiles/`), not by hand-editing Player Settings. This is exactly the problem Build Profiles were added to solve, and it removes the "I forgot to strip the defines" failure class.

| Define | Purpose | Dev | QA | Release |
|---|---|---|---|---|
| `SLAPMANIA_DEBUG` | Master switch: the `Diagnostics` assembly, `SlapLog`, the overlay, the recorder, assertions | yes | yes | **no** |
| `SLAPMANIA_CHEATS` | The cheat panel only — separated so QA gets the overlay without the ability to break a build under test | yes | no | no |
| `SLAPMANIA_VERBOSE` | Per-frame trace logs (marker position, every tap) | opt-in | no | no |
| `SLAPMANIA_PERF_HUD` | FPS and frame-time counter only, no overlay | yes | yes | no |

`SlapMania.Diagnostics.asmdef` carries `"defineConstraints": ["SLAPMANIA_DEBUG"]`. Without the define the assembly is **not compiled and not included in the build** — not one byte of overlay, cheat or recorder code ships. That is stronger than scattering `#if` through files, because it is impossible to forget.

`BuildVerifyPreprocessor` (`IPreprocessBuildWithReport`) **fails** a Release build if `SLAPMANIA_DEBUG` or `SLAPMANIA_CHEATS` is defined, or if the orientation is not portrait-locked.

## Player settings

| Setting | Value |
|---|---|
| Company / Product | `Dot9 Games` / `Slap Mania` |
| Bundle identifier | `com.dot9games.slapmania` |
| Orientation | **Portrait only** — all three other autorotate flags off |
| Scripting backend | IL2CPP |
| API compatibility | .NET Standard 2.1 |
| Color space | **Linear** (required for the photo grade — do not change) |
| Android architectures | **ARM64 only** |
| Android graphics APIs | **Vulkan first, GLES3 fallback** — both must be tested |
| Android min SDK | 25 (revisit before submission) |
| Managed stripping | Minimal until release, then re-test on device |
| Active quality level, Android / iOS | **Mobile** (index 0) — the template default is PC, which silently invalidates device testing |

Set at runtime in `GameRoot.Awake()`:

```
Application.targetFrameRate = 60;          // nothing caps FPS today: vSyncCount is 0 in both levels
QualitySettings.vSyncCount  = 0;
Screen.orientation          = ScreenOrientation.Portrait;
Screen.sleepTimeout         = SleepTimeout.NeverSleep;    // during bouts only
InputSystem.settings.pollingFrequency = 120;              // 60 loses taps at 8+ taps/s
EnhancedTouchSupport.Enable();
```

## Tags and layers

> **Planned, not created.** `ProjectSettings/TagManager.asset` has only Unity's built-in layers today. Adding these is a ProjectSettings change (R9, ask first). Until then the arena, fighters and FX all sit on Default. The damage cam excludes only the built-in UI layer.

| Layer | Name | Purpose |
|---|---|---|
| 6 | `Fighter` | Fighter meshes — appear in photos |
| 7 | `FighterFX` | Sweat, spit, impact particles — appear in photos |
| 8 | `Arena` | Ring, floor, props |
| 9 | `Crowd` | Background crowd; cullable at low tier |
| 10 | `SnapshotOnly` | Rim lights, speed lines, confetti that appear **only** in photographs |
| 11 | `HideInSnapshot` | Gameplay gizmos, world-space helpers, debug |
| 12 | `PostFXOnly` | Reserved |

Tags: `Fighter`, `Player`, `Opponent`, `SnapshotCam`, `UIRoot`.

Photo camera culling mask: `Fighter | FighterFX | Arena | Crowd | SnapshotOnly`. Main camera excludes `SnapshotOnly`. The HUD is **Screen Space - Overlay**, so it is rendered by no camera and is structurally incapable of appearing in a photograph. Do not use Screen Space - Camera for the HUD.

## Running on Android

```bash
# Build to a device from the Dev profile, then:
adb logcat -s Unity:V

# Pull recordings and crash logs
adb pull /sdcard/Android/data/com.dot9games.slapmania/files/SlapMania/Recordings/
adb pull /sdcard/Android/data/com.dot9games.slapmania/files/SlapMania/Logs/

# Memory check
adb shell dumpsys meminfo com.dot9games.slapmania
```

### The GLES3 smoke test

**Required before any snapshot work is called done.** Temporarily reorder Graphics APIs in Player Settings so GLES3 is first, build, run the snapshot checks, then revert. Vulkan is first in production, but GLES3 is where flipped-Y, black-RT and context-loss bugs live, and Vulkan-only testing will not find them.

Checks on both builds:
- A captured tile is not uniformly black.
- A rendered 50 % grey card (`#808080` sRGB) reads back as `128 ± 3`.
- A "TOP" marker quad on the `SnapshotOnly` layer appears the right way up.
- Memory delta after 16 slaps is ≤ 16 MiB.

## Running on iOS

Requires macOS and Xcode. Build produces an Xcode project; signing is configured there. Keystores and provisioning profiles are **never** committed.

Pull the app container via Xcode > Window > Devices and Simulators > select device > Download Container, then inspect `AppData/Documents/SlapMania/`.

## Packages

No third-party packages — see [ADR-0007](Decisions/ADR-0007-no-third-party-packages.md). Adding one requires an ADR.

Removed from the template because nothing uses them and they cost build size and domain-reload time: `com.unity.visualscripting`, `com.unity.multiplayer.center`, `com.unity.ai.navigation`, `com.unity.pipeline` (experimental), `com.unity.collab-proxy`.

Kept and relied on: `com.unity.inputsystem`, `com.unity.render-pipelines.universal`, `com.unity.ugui` (TMP), `com.unity.timeline` (finisher cinematics, M18), `com.unity.test-framework`, `com.unity.modules.imageconversion` (snapshot JPEG archive), `com.unity.modules.jsonserialize` (save and recordings).

## Tests

`Window > General > Test Runner`.

- **EditMode** must be green before every commit. This is where all the gameplay math lives, and it runs in under a second because `Core` and `Gameplay` have no MonoBehaviour dependency.
- **PlayMode** includes a full scripted match reaching `MatchEnd` in under 60 s with no `LogType.Error` (`LogAssert.NoUnexpectedReceived`), and an allocation test that **fails at anything above 0 bytes** across a charge phase.
- `TuningInvariantTests` loads the real `.asset` files and asserts designer-facing invariants: zone widths sum to 1.0 ± 1e-5, multipliers strictly ordered, the narrowest zone is at least 40 ms of travel, and maximum single-slap damage is below starting HP. These catch *designer* mistakes, which no amount of unit-testing the code will.

## Troubleshooting

Start at [DEBUG.md §1](DEBUG.md#1-first-response-checklist).
