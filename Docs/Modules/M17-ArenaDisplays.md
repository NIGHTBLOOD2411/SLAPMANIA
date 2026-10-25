---
module: M17-ArenaDisplays
status: InProgress
owner: shobhit
last-verified: 2026-09-26
verified-against: (pre-git)
asmdef: SlapMania.Presentation
---

# M17 — ArenaDisplays

> **One-liner:** In-arena jumbotron screens that show a live camera feed, replays, fighter stats, and hype graphics — the crowd's view of the action.

## 1. Purpose

Real combat sport arenas have jumbotrons. The slap ring has them too — large screens mounted above the crowd that display the live action, replay the last hit, show fighter stats, and flash hype graphics on beat. They are world-space objects in the scene, rendering to `RenderTexture` or using `RawImage` on world-space canvases.

**Status (2026-09-26):** the live **Damage Cam** jumbotron and the static LED surfaces (ribbon, boards, signs) have landed with the generated arena ([ADR-0012](../Decisions/ADR-0012-arena-mobile-fakes.md)). **Not yet:** replay, stats and hype graphics, beat pulse and scroll, the finisher crest.

## 1a. Look reference

Concept frames in [`Docs/Reference/Arena/`](../Reference/Arena) (`mag_*`, `wa_*`). Two distinct display surfaces, both in scope:

- **Overhead jumbotron** — a single large ~16:9 LED slab hung from the far truss, showing the `DAMAGE CAM / LIVE` broadcast feed and, during a finisher, the fighter's champion portrait. This is what §2 already describes.
- **Ring-apron LED ribbon** — a continuous 360° low-resolution ribbon around the ring apron carrying alternating sponsor cards (`MEGATRON ENERGY DRINK`, `MAX-IMPACT HANDS`) that scroll and pulse subtly on beat. It is a wider surface than the jumbotron and appears in every shot — treat it as first-class content, not decoration. Cheapest implementation is a scrolling emissive texture on a bent-cylinder mesh; do not spend a `RenderTexture` on it.
- **Broadcast tickers** on the crowd-facing panels — sponsor text sliding horizontally at a fixed BPM-quantised speed. Same scrolling shader as the apron ribbon.
- **On-screen graphics style** — real live-event lower-thirds: a red `LIVE` bug top-left, a `DAMAGE CAM` caption bottom-centre, and a small round champion crest bug during finishers. Author these as separate camera-space UI layers composited into the jumbotron RT so they can change without re-rendering the feed.

The apron ribbon and tickers are the *most-visible* part of the arena. They deserve the design attention M17 currently reserves for the jumbotron.

## 1b. What landed

- **Jumbotron** (one, answering the open question below).
  - **Placement:** hung on cables above the tunnel on the far side of the ring, 5° right of the gameplay camera's axis at r 12.6 m, centre y 4.45 m. A 4.0 × 2.25 m LED screen sits in a dark frame, with red vertical side panels and a red `DAMAGE CAM` sign underneath.
  - **Layers:** the screen is `SlapMania/Arena/Led` sampling the feed with a dot-matrix mask. A transparent overlay quad carries the broadcast furniture (viewfinder corners and a red `LIVE` bug bottom-left, as in the reference frame), so graphics change without touching the feed.
- **`DamageCam`** (`SlapMania.Presentation`), answering the "different angle" open question: yes. It is a second camera rendering into a **512×288 sRGB RenderTexture asset** (`Art/Arena/Generated/DamageCam.renderTexture`) with no post-processing, shadows, MSAA or depth/colour textures, and without the UI layer.
  - It is enabled only every `_renderEvery`-th frame (default **2**), holding the last frame in between.
  - On `TurnStart`, `BoutController` → `ArenaDirector.FocusDamageCam(defender.HeadAnchor, attacker position)`. It frames the defender's face from the attacker's side (`_distance` 0.95 m, `_side` 0.55 m) so the slap lands on screen, easing on unscaled time.
  - With no target yet (the intro), it holds an idle wide ring shot.
- **LED sponsor ribbon** on the upper-tier fascia: a 360° band, 0.8 m tall, with alternating purple/red slanted panels (`MEGATRON ENERGY DRINK` / `MAX-IMPACT HANDS`).
- **Ring-side boards:** backlit navy panels with gold trim (`MEGATRON · ENERGY DRINK`, the `MALE` crest).
- **All display art is generated** by `ArenaArt` (M13), with text rasterised from the bundled Barlow TTFs. No `RenderTexture` is spent on the ribbon or boards.
- **Sponsor names are placeholders** copied from the reference and **not cleared for trademark**: clear or replace them before any public build (ADR-0012 Risks). Each is one constant in `ArenaArt`.

## 2. Responsibilities and non-goals

**Owns**
- Jumbotron mesh placement and screen content switching.
- Live feed — a second camera rendering to a `RenderTexture` displayed on the jumbotron.
- Replay feed — playing back the last N frames of the main camera as a looping clip.
- Stats display — fighter names, HP bars, round counter.
- Hype graphics — beat-synced animations (text flashes, logo pulses).
- **Ring-apron LED ribbon** and other emissive-texture sponsor surfaces around the ring — scroll speed, beat-quantised pulse, sponsor rotation.
- **Broadcast overlays** composited into the jumbotron feed — `LIVE` bug, `DAMAGE CAM` caption, champion crest during finishers.

**Explicitly does NOT do**
- Own the main camera → M16.
- Own the photo camera → M09.
- Own the arena lighting → M14.
- Own audio → M11.

## 3. Known constraints

- **GPU budget:** the arena displays contribute to the 1.5 ms jumbotron line item in [BASELINE.md](../Perf/BASELINE.md). A full-resolution live feed is too expensive — the jumbotron camera should render at **¼ resolution** (270×480) and update at **15 fps** (every 4th frame), not every frame.
- **Render Graph compatibility:** the jumbotron camera is a second camera in the URP pipeline. With Render Graph ON, it must be configured correctly (`RecordRenderGraph`, `AllowPassCulling(false)`) — same issues as M09's photo camera.
- **World-space canvas performance:** world-space `Canvas` triggers a mesh rebuild on every content change. Stats and hype graphics should change infrequently (on signals, not per-frame).
- **Snapshot interaction:** jumbotrons should be on the `Arena` layer (8) so they appear in snapshot photos. The jumbotron's own content (the live feed texture) will show whatever was on screen at capture time — this is correct and intentional.
- **Device tier:**
  - Low: jumbotrons show static graphics only (fighter names, round number), no live feed.
  - Mid: live feed at ¼ res, 15 fps.
  - High: live feed at ½ res, 30 fps, replay capability.

### Deviations from §3, tracked

- **Resolution and rate.** The feed is 512×288 (≈ ¼ of 1080p, landscape to match the 16:9 slab) at every **2nd** frame (≈ 30 fps at 60). That is between the Mid (¼ res, 15 fps) and High (½ res, 30 fps) lines. Set `_renderEvery` to 4 for the Mid budget. Unmeasured against the 1.5 ms line.
- **Low tier holds the last frame** rather than showing static graphics: `ArenaDirector` Low disables the camera.
- **Ribbon and boards do not scroll or pulse yet.** `SlapMania/Arena/Led` already has `_Scroll`; the beat pulse waits for M11.
- **No `Arena` layer (8)**: the layers do not exist yet (R9).
- **Render Graph:** the damage cam is a plain second base camera with a target texture and needed no custom pass. It rendered correctly in the Editor (Forward+, PC asset); on-device GLES3 is unverified.

## 4. Open questions

- [x] How many jumbotrons should the arena have? 2 (one per side) or 4 (surrounding)? More looks impressive but each is a draw call. **One**, as in the reference, placed in the gameplay camera's background.
- [x] Should the live feed show the main camera's view or a different angle? A different angle adds a second render pass but makes the jumbotron feel like a real broadcast. **A different angle**: the defender's face from the attacker's side.
- [ ] Is replay feasible on mobile? It requires buffering the last 2-3 seconds of frames, which is 30-90 frames at 30fps × ¼ resolution ≈ 3.5-10.5 MiB. May be too much for low-tier devices.
- [ ] Should hype graphics be pre-rendered sprite animations or runtime text? Runtime text is localizable; sprites are cheaper and more controllable.
- [ ] Should the jumbotron flicker during impact (a physical TV shake effect), or is that too subtle to notice?
- [ ] Can the jumbotron camera share M09's photo camera to reduce the number of cameras in the scene?

## 5. Dependencies

- **Hard:** `SlapMania.Core` (signals), M11 (`IBeatSource` for hype graphics).
- **Soft:** M16 (camera feed source).
- **Unity APIs:** `Camera` + `UniversalAdditionalCameraData`, `RenderTexture`. No world-space `Canvas` was needed: overlay graphics are a texture.
- **Must initialize after:** M00, M16.

## 6. Changelog

| Date | Commit | Change |
|---|---|---|
| 2026-09-19 | (pre-git) | Created (Draft stub) with constraints and open questions |
| 2026-09-19 | (pre-git) | Added §1a look reference; expanded §2 with ring-apron LED ribbon and broadcast overlay layers (from concept frames) |
| 2026-09-26 | (pre-git) | **Design pass + Damage Cam jumbotron and LED surfaces landed with the generated arena ([ADR-0012](../Decisions/ADR-0012-arena-mobile-fakes.md)).** `DamageCam` second camera → 512×288 RT every 2nd frame, framing the defender from the attacker's side, idle ring shot before the first turn; LED screen shader with dot mask; overlay quad with viewfinder corners + LIVE bug; generated sponsor ribbon, boards, sign and side panels (placeholder brands, uncleared). Status Draft → InProgress. §1, §1b (new), §3 deviations, §4, §5 updated. |
