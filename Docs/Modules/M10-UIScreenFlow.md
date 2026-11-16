---
module: M10-UIScreenFlow
status: InProgress
owner: shobhit
last-verified: 2026-11-07
verified-against: (pre-git)
asmdef: SlapMania.UI
---

# M10 — UIScreenFlow

> **One-liner:** Manages the screen stack (menu, HUD, victory), the safe-area fitter, and every UI element the player interacts with outside of tapping.

## 1. Purpose

The game's UI is portrait-only, phone-first, and must work inside every notch, cutout, and rounded corner shipping in 2026. This module owns the screen manager (which screen is visible), the `SafeAreaFitter` (which keeps UI out of the danger zones), the in-bout HUD (health bars, power bar, timing bar, damage numbers), and the victory screen (snapshot gallery).

The HUD is **Screen Space - Overlay**. This is a structural decision, not a preference: Overlay mode renders the HUD with no camera, which means it cannot accidentally appear in a snapshot (M09's photo camera cannot see it) and it cannot be affected by post-processing (bloom and color grading stay on the game, not the numbers).

## 2. Responsibilities and non-goals

**Owns**
- `ScreenManager` — a stack of `IScreen` implementations (Menu, HUD, Victory, Settings, Pause).
- `SafeAreaFitter` — a `RectTransform` driver that reads `Screen.safeArea` and constrains the HUD root.
- `PowerBarView` — polls `PowerMeter.Fill` in `LateUpdate`, drives a slider.
- `TimingBarView` — reads `TimingZoneLayout` at phase start, generates one `Image` per zone, polls `TimingMeter.NormalizedPosition` for the marker.
- `HealthBarView` — polls `HealthTracker.NormalizedHp`, animates a delayed drain.
- `DamagePopup` — floating number on impact, scaled by damage tier.
- `VictoryGalleryView` — reads `SnapshotSystem.Atlas` and `Gallery`, lays out tiles with metadata.
- `TurnIndicator` — shows whose turn it is with a swap animation.

**Landed so far: the V2 in-match HUD ([ADR-0011](../Decisions/ADR-0011-adopt-v2-design-prototype-first.md))**

Built to the V2 reference frames (`SlapMania_V2_AllScreens_for_Figma.html`) for the required systems only:

- **`V2Hud`** routes `MatchSignals` to one `HudView` per V2 screen, owns the shared `HudContext`, and raises `RematchRequested`. It never drives gameplay.
- **`HudView`** is the base for every screen: a `CanvasGroup` fade (to `ShownAlpha`, 1 except the scorebug's rest alpha) plus a private `Core.Motion.Tweener` (32 slots) ticked on **unscaled** time. Screens sequence with `Tw.Delay(..., this, id)` and `OnTween(id)`, with no coroutines or closures (R5). Two Tweener rules shape that sequencing: a delayed tween shows its *start* value at once, and a new tween of the same kind on the same target replaces the running one. A beat that must stay hidden, or a second step on the same target, is therefore started from an `OnTween` timer, not with a delay (`HudKnockout` slam and tint).
- **Attention budget** (2026-10-23, [POLISH §9](../POLISH.md)): every view reads `HudContext.Attention` (`HudAttentionConfig`, §4) for its coverage, alpha and lifetime. `V2Hud` keeps **one headline on screen at a time**: on `ChargeStart` it calls `HudImpact.Yield()` and defers D2's show until the headline is off (S-100).
- **Screens:**

  | Class | V2 screen |
  |---|---|
  | `HudScorebug` | Portraits, names, round, HP bars. `HpBar`: the fill tracks HP at once; the damage chunk behind it flashes white, turns red and drains after a short hold. Rests at `scorebugRestAlpha` (0.75) and goes to 1 for `scorebugHitHoldMs` on a hit; 140 px top shade; the LIVE dot is still unless `liveDotPulse` |
  | `HudIntro` | C4 round intro and 3-2-1 countdown. Its two hint lines follow the attack mode (TAP FAST TO CHARGE / TAP ON THE GOLD RING, or HOLD TO WIND UP / RELEASE ON THE GOLD RING) |
  | `HudCharge` | C6 letterbox, rays, power arc and number. **TapMarker:** shown for the charge phase; TAP FAST! kicks on every accepted tap. **HoldRing** ([ADR-0014](../Decisions/ADR-0014-hold-ring-attack-mode.md)): shown at the press. The arc follows `HoldRingMeter.WindUp01`, the red lock-on reticle is hidden (C5's closing ring stays on the face), and the prompt reads HOLD! until the wind-up peaks, then RELEASE! (C6: MAX POWER and "Release!" together) |
  | `HudTiming` | C5, the V2 closing ring. Its radius is a pure function of the active meter: `TimingMeter.NormalizedPosition` (TapMarker) or `HoldRingMeter.Closure` (HoldRing: open until the press, then closing with the hold). The gold band is the Perfect zone from `TimingZoneLayout` × the round's zone scale, so the UI cannot disagree with the rules. HoldRing: at the press the C5 chrome (Safe layer: banner, caption, meter, hints; Bleed: vignette) fades and only the World ring stays. Caption and hint copy follow the mode |
  | `HudImpact` | D1 zone callout, damage number, crit. Headline at 0.73 of the mockup and gone by 700 ms, number at 0.75 and gone by 1.1 s, no bottom shade. `Yield()` hurries both off when the next turn starts |
  | `HudOpponentTurn` | D2. Live power while the rival charges, or while they hold the ring (`HoldRingMeter.Power`). In HoldRing it appears when the rival's ring opens (`V2Hud.OnTimingStart`), before their press |
  | `HudKnockout` | D3. The slam waits `koSlamDelayMs` (350) so the finisher camera shot reads first, over a 0.25 tint that rises to 0.5 after 1.2 s; the winner line and stats wait for BoutEnd. Only the HUD is delayed, never the camera (M16) |
  | `HudResult` | E2/E3 result card from `SlapStats`, with Rematch. Since 2026-10-23 both cards open on the **loser's slap gallery** (M09): three framed photos with damage, from `HudContext.Gallery`/`GalleryDamage`/`GalleryCount`, popping in staggered. Empty frames hide. `V2HudBuilder` builds `_vGallery*`/`_dGallery*` (Victory 48-194 px, Defeat 622-768 px of the 430 x 932 frame) |

- **Procedural uGUI graphics** (`UI/Graphics`, all generated by `ShapeMesh`, no sprites):
  - `SlapPanel`: 3-stop gradient fills, skew, chamfer, corner radius, border.
  - `SlapArc`: an arc with a sweep gradient and glow.
  - `SlapRays` and `SlapGlow`.
- **Text and widgets:**
  - `ChromaText`: TMP with red/cyan chromatic split layers.
  - `PressScale`: button squash.
  - `SafeAreaFitter`: the `Safe` layer.
- **Fonts:** Barlow / Barlow Condensed TMP SDF assets (OFL; `Art/Fonts/Barlow`).
- **Layers:** each screen has two.
  - **Bleed** is full-screen art such as the letterbox, rays and vignette.
  - **Safe** holds text and controls, fitted to `Screen.safeArea` by `SafeAreaFitter`.
  - Rings over faces are positioned every frame from the fighters' head anchors through `Camera.WorldToScreenPoint`.
- **Generated, not hand-placed.** `Hud.V2HudBuilder` (M13) builds the whole canvas from the reference coordinates.
- **Removed:** `BoutHud`, `BoutCallouts`, `BoutResultPanel` (superseded 2026-09-26).
- **Results "MAIN MENU"** (2026-10-19). `HudResult` has a ghost button under REMATCH. It raises
  `MenuRequested`; `V2Hud` forwards the event and `BoutController` asks `GameRoot` for the front
  end.

**Landed: the V2 front end in UI Toolkit ([ADR-0013](../Decisions/ADR-0013-ui-toolkit-front-end-from-mockups.md), 2026-10-19)**

Twenty V2 screens come from the `Slap Mania Game UI` Design canvas (sources in
`Docs/Reference/Mockups/V2`):
- **Front end:** A1–A7, B1–B6, C1–C3.
- **Match-adjacent:** D4 Replay, D5 PauseMenu, E1 RoundEnd, E4 RankUp.

They are UI Toolkit screens in `Scenes/FrontEnd.unity`, loaded additively from Boot. The match
HUD stays uGUI. The four match-adjacent screens are also hosted by the bout's own overlay stack
(see *the bout overlay and pause*, below).

- **Generated screens.** `Tools/mockup2uxml` turns each mockup into
  `Prefabs/UI/Screens/<Name>.uxml` by resolving every element's computed CSS (cascade,
  inheritance, grid → wrapped rows, `gap` → margins, `em` → px, the CSS strut for text boxes). It
  also writes:
  - inline SVGs as `Art/UI/Icons/ico_<hash>.svg`, imported as `VectorImage` with the viewBox kept;
  - masked portraits pre-multiplied into `Art/UI/Mockup/Masked`;
  - glyphs Barlow lacks (★ ▾ ●) as vector icons;
  - every approximation, listed in `Docs/Reference/Mockups/CONVERSION-REPORT.md`.

  **Do not hand-edit the UXML; re-run the tool.**
- **`ScreenManager`** (not a singleton) owns the stack: Push / Replace / PopTo / ResetTo / Back.
  - Screens are created from the serialized `ScreenId → VisualTreeAsset` table on first use.
  - Links named `nav-<Screen>` call `Navigate`, and `ResolveNav` picks the stack operation.
  - A link into the bout (Ready, Aim, …) raises `MatchRequested`; the App (`FrontEndBridge`)
    turns that into a `GameRoot` scene swap.
- **`ScreenView`** gives every screen its behaviour from the converter's `k-<css class>` classes:
  - staggered enter and exit;
  - `k-bar` meters filling from zero;
  - `k-rays`/`k-speed` turning, `k-live` dots blinking;
  - press squash on links and buttons.

  Subclasses add the screen-specific parts:
  - Title: tap anywhere. Two spotlight beams sway out of phase and blink in a looped pattern
    (`Core.Motion.BlinkPattern`, a pure function of elapsed ms; each level holds, then eases
    into the next over the last 30 % of its step). The mockup has one beam (`title-beam`); the
    second is cloned from it at bind, 110 px right and tilted 14° from its top edge, so the
    generated UXML stays untouched.
  - Loader: a timed fill, then Main Menu replaces it.
  - Main Menu: a scrolling ticker.
  - Search: radar sweep and timer, then Walkout.
  - Walkout: the VS slam, then Tale of the Tape.
  - Tale of the Tape: a countdown that starts the bout.
  - PackOpen: the card reveal.
  - Settings: values persist under PlayerPrefs `fe.<name>` until M11/M12 own them.

  They find their elements through converter-pinned hooks (`HOOKS` in the tool).
- **`PaintBox`** draws the CSS UI Toolkit lacks: gradients, patterns, `clip-path` shapes, masks,
  `box-shadow`, and borders clipped with their shape (`edge`). The maths is `Core.Paint`, pure and
  EditMode-tested. It renders in one of three ways:
  - flat colour, natively;
  - clip shapes as `Painter2D` vectors;
  - everything else as reference-counted baked textures (`PaintTextureCache`).
- **`VisualTween`** is `Core.Motion.Tweener`'s sibling for UI Toolkit: fixed slots, unscaled
  time, no closures, and poses relative to the element's UXML pose. Durations come from
  `UiMotionConfig` (ms, R8; `GameConfig.uiMotion`, Tuning Hub).
- **`GammaPresenter`** keeps blending browser-faithful. The project is Linear, so UI alpha would
  blend in linear light: a 6 % white fill over near-black reads 70/255 instead of the mockup's 21.
  The panel therefore renders with `forceGammaRendering` into a screen-sized linear RenderTexture,
  and a full-screen `RawImage` shows it through `SlapMania/UI/GammaComposite`. Touches reach the
  panel through `SetScreenToPanelSpaceFunction`, as the identity: UI Toolkit already passes
  top-left pixels and the texture is the screen 1:1 (a y flip there mirrored every tap, S-091).
- **`KitSwitch` / `KitSlider`** are the kit's `.sw` and `.rng`. The slider's visible box is the
  6 px track; an invisible hit zone reaches 14 px above and below it.
- **Fidelity is measured, not eyeballed.** Each screen is rendered by headless Edge at 2× and
  captured from the Game view at 860×1864. `Tools/mockup2uxml/compare.py` reports the difference.

  2026-10-19 result:
  - every screen has at most 3.0 % of pixels differing by more than 48/255;
  - mean absolute difference is 3.0–7.2/255;
  - the remainder is glyph anti-aliasing (Chrome vs TextCore) and deliberate motion (rays,
    ticker, meters).

**Landed: the bout overlay and pause (2026-10-20)**

`Proto_Bout` has its own UI Toolkit screen stack, drawn **over** the uGUI `V2Hud`: D5 PauseMenu,
D4 Replay, E1 RoundEnd, E4 RankUp, and B6 Settings for the pause menu's Settings link. The HUD
stays uGUI.

- **Generated with the bout.** `PrototypeSceneBuilder` calls
  `FrontEndSceneBuilder.BuildScreenHost` - the same UIDocument + `GammaPresenter` +
  `ScreenManager` the front end uses (`BoutOverlayScreens` picks the five). Its output canvas sorts
  at **20**, above the HUD's 10. No new scene, singleton, package or asmdef edge.
- **Opens empty** (initial `None`) and `ScreenManager.Close()` empties it again.
- **Costs nothing while closed.** With `_presentWhileOpen` set, `GammaPresenter` turns its
  full-screen composite and the target clear off whenever the stack is empty, so Charge and Timing
  pay no extra blend. It clears the target once on the way out, so a re-open never flashes a stale
  frame.
- **Input follows draw order.** The presenter copies its output canvas's sorting order onto the
  runtime panel, so an overlay drawn over the HUD also wins the clicks there. With no screen open,
  nothing in the panel picks, and taps reach the HUD and the slap.
- **Pause buttons** (`HudPauseButton`, routed by `V2Hud._pauseButtons` into `PauseRequested`): a 38 px
  glass square with the two-bar icon.
  - **Scorebug**, top-left as in the Ready/Aim/Brace mockups: intro, opponent's turn, the player's
    timing.
  - **C6 charge**, in the top-left corner of the top letterbox (Safe 16,16). The mockup has none;
    the owner asked for one (2026-10-20). It sits in the black band, as far from the mashing area
    (the middle of the screen) as the frame allows. The trade-off is that a stray mash tap in that
    corner pauses the slap.
  - A button counts only while its own screen is shown (`HudPauseButton.Covers`). Device back /
    Escape pauses anywhere.
  - **A press on it is not a slap tap.** The whole screen is the tap target, so without a guard the
    press-down would add charge, or *commit* the timing slap, before the click even opened the menu.
    `V2Hud.BlocksTap(screenPos)` is the player tap source's `Blocker` (M02).
- **Pausing** (`BoutController.Pause`, App) runs `MatchDirector.RequestPause` (M01: the simulation
  clock stops and taps are dropped), then `HitstopController.SetPaused` (M08: scaled time stops, and a
  freeze in progress keeps its remaining time). Then it shows PauseMenu.
  - The HUD freezes too. `HudView.Update` returns while `HudContext.IsPaused`.
  - The shot clock and the intro countdown read `HudContext.Now`, which is `MatchDirector.Now`.
    Before this they read realtime and kept counting down behind the menu.
- **Links.** The overlay screens are the mockups, so their links name mockup screens. In the bout
  they mean:

  | Link | In the bout |
  |---|---|
  | PauseMenu "Resume" (`nav-Aim`) | Close the overlay at once and resume |
  | PauseMenu "Settings" | B6 on top. Its back arrow (`nav-Profile`, not hosted here) comes back |
  | PauseMenu "Last replay" | D4 placeholder. "Close" (`nav-Knockout`) comes back. "Next" (`nav-RoundEnd`) shows E1, whose "Round 2" (`nav-Ready`) or Win link resumes |
  | PauseMenu "Forfeit match" (`nav-Loss`) | `MatchDirector.Forfeit(Player)`, then the E3 Defeat card. Refused once a lethal slap has landed; play resumes |
  | Results MAIN MENU **after a win** | E4 RankUp first (a placeholder until divisions exist, M12). "Collect all" (`nav-MainMenu`) leaves for the front end. A defeat leaves at once |

  Mechanically:
  - links into the match (`ScreenIds.IsMatch`) raise `MatchRequested`;
  - links to screens this stack does not host resolve to the new `NavOp.Leave` and raise
    `LeaveRequested`.

  `BoutController` maps both. Back / Escape pops the overlay; at its bottom it closes (resuming a
  pause), and with nothing open it pauses. The app going to the background pauses a live bout
  (`OnApplicationPause`).
- **Placeholders.**
  - The pause menu's score line, round and "pause limit" copy are the mockup's.
  - Its quick-settings switches toggle but nothing reads them.
  - Replay shows no replay.
  - RankUp's division and rewards are the mockup's (ADR-0011 §3).

**Landed: the V1 page beside V2 ([ADR-0015](../Decisions/ADR-0015-v1-page-behind-a-tuning-switch.md), 2026-10-20)**

The canvas's earlier page, 16 screens from `Docs/Reference/Mockups/V1` (01 Splash ... 16 Defeat),
is in the game alongside V2.

- **Same pipeline, own folder.** `Tools/mockup2uxml` converts both pages (`PAGES`). V1 output goes
  to `Prefabs/UI/Screens/V1/<Name>.uxml`; the builder turns the subfolder into the id prefix
  (`V1/Home.uxml` -> `ScreenId.V1Home`). Ids are `V1Main` = 100 ... `V1Defeat` = 115, appended.
- **What the converter does differently for V1.**
  - Root: V1 has no `.scr`; the first `<div>` in `<x-dc>` is the frame.
  - Kit: no `kit.css`. Each page's own `<style>` (`.lbl`, `.dsp`, `.panel`, `.btn-red`, ...) is the
    cascade, and its full-frame layers are spelled `430px x 932px`, which bleed like `.fill`.
  - **Sizing:** V1 has no `*{box-sizing:border-box}`, so an element is content-box and its
    padding and border sit outside its width and height. UI Toolkit is always border-box, so the
    converter adds them to every px size (buttons and inputs stay border-box, as in Chrome).
  - **Fonts:** Cabin (400-700) and Archivo Black, each with its own vertical metrics (`FONTS`).
    Edge lays Archivo Black out with its win metrics (1.035/0.312 em) but TextCore places glyphs
    with hhea (0.878/0.21), so its labels get a baseline correction. CSS weight matching picks the
    face; 600+ asked of a lighter face is faux bold (`<b>`), as in Chrome.
  - **Gradients:** `.redtxt` / `.goldtxt` / `.silvertxt` use their own presets `V1Red`, `V1Gold`,
    `V1Silver` (the colours differ from V2's).
  - **Switch:** the V1 `.sw` is 48 x 28 with a 22 px knob: `KitSwitch` + `kit-switch--v1` (Kit.uss).
- **Choosing the page.** `GameConfig.frontEndPage` (Tuning Hub > FRONT END > Menu page). On V1,
  `FrontEndBridge` opens `V1Main` instead of Title, and a bout's MAIN MENU returns to `V1Home`
  (`ScreenIds.ForPage`). There is no player-facing switch; neither design has one.
- **The bout is the real one.** V1's Countdown, SlapTurn, SmashHit, Defend, Pause, RoundKO,
  Victory and Defeat are `ScreenIds.IsMatch`, so VS Intro's link starts the bout (the V2 HUD).
  Their UXML is converted and scored as a reference but never shown by navigation
  (`ResetTo` still shows them, which is how they are captured).
- **Behaviour.** V1 Loading reuses `LoaderView` (hooks `loader-pct` / `loader-fill`) and hands
  over to V1 Home. V1's bottom nav (Home, Fighters, Shop, Ranks) is `ScreenIds.IsTab`. Every
  other V1 screen is a plain `ScreenView` (links, buttons, press squash, enter motion).
- **Gradient text never carries a text-shadow (DEBUG S-082).** UI Toolkit draws a label that has
  both a `text-shadow` and `<gradient>` runs in its plain colour. V1's gradient titles all have a
  CSS `drop-shadow`, so the converter splits them:
  - a **hard** shadow (no blur) becomes a *shadow twin*: a container in the label's place, a plain
    label in the shadow colour with its glyphs pushed down by the offset (`fe-shadow`), and an
    absolute gradient label over it (`fe-shadow-face`). Siblings, not a child: a Label with
    children stops measuring its text;
  - a soft shadow is dropped (V2 Walkout's "VS", which was rendering white because of this).
- **Wrapping is predicted (DEBUG S-084).** The theme's labels are `nowrap`. The converter measures
  each text with the font's advance widths (`ttfmetrics.py`) against its box (`Converter.avail`);
  a label CSS wraps gets `white-space: normal` and a box of that many lines.
- **Fidelity (2026-10-20, V1):** all 16 screens at **0.1–2.6 %** of pixels differing by more than
  48/255 (SmashHit 2.6 %, Leaderboard 1.5 %, everything else ≤ 1.2 %), mean absolute difference
  2.5–5.5/255. Captured from an 860×1864 Game view with `ResetTo(id, animate: false)` and tweens
  stopped (V1 Loading pinned at the mockup's 68 %). The remainder is glyph anti-aliasing, the
  CSS middle gradient stop, dropped soft shadows, and dashed borders drawn solid. V2 Walkout,
  re-scored after its "VS" lost the soft shadow: 1.3 % (1.2 % on a second, independent capture;
  the "VS" shows its silver gradient).
- **The switch path, played (2026-10-20).** With `frontEndPage = V1`, Play from Boot opened
  `V1Main`; its link (a real `ClickEvent` on `nav-V1Loading`) went to V1 Loading, which handed
  over to `V1Home` by itself (~1.2 s, stack `[V1Home]`); SLAP NOW -> Matchmaking -> VS Intro.
  V1 Matchmaking does not advance by itself (V2 Search does): its VS Intro link is the "OPPONENT
  ???" card. VS Intro's Countdown link raised `MatchRequested(V1Countdown)` in the frame of the
  click (nothing else called `RequestMatch`), which unloaded FrontEnd and loaded `Proto_Bout`.
  After a KO loss, the KO card's Continue and the defeat card's MAIN MENU (real `Button` clicks)
  came back to `V1Home`. No errors or warnings. Back on V2, Boot opened Title. Not played: the
  win path (MAIN MENU -> E4 RankUp -> Collect all), which ends in the same `RequestFrontEnd(MainMenu)`.

**Explicitly does NOT do**
- Own gameplay state → all views poll or subscribe to gameplay modules.
- Own the snapshot atlas → `M09`.
- Own audio feedback for UI actions → `M11`.
- Handle tap input → `M02` (the HUD has no tap targets during gameplay; menu buttons use standard Unity UI `Button.onClick`).

## 3. Public API surface

| Member | Signature | Notes |
|---|---|---|
| `ScreenManager.Push` | `void Push(IScreen screen)` | Activates screen, deactivates previous |
| `ScreenManager.Pop` | `void Pop()` | Returns to previous screen |
| `ScreenManager.Replace` | `void Replace(IScreen screen)` | Pop + Push |
| `ScreenManager.Current` | `IScreen { get; }` | |
| `IScreen.Show` | `Awaitable Show()` | Transition-in animation |
| `IScreen.Hide` | `Awaitable Hide()` | Transition-out animation |
| `SafeAreaFitter.Apply` | `void Apply()` | Called in `OnRectTransformDimensionsChange` |
| `V2Hud.Bind` | `void Bind(MatchDirector director, SlapStats stats, GameConfig config, Camera cam, Transform playerHead, Transform opponentHead)` | **Landed.** Subscribes to signals (re-Bind unsubscribes first); the camera and head anchors place the world-anchored rings |
| `V2Hud.SetPortraits` | `void SetPortraits(Texture player, Texture opponent)` | **Landed.** One-shot portraits captured by `FighterPortrait` (M08) two frames after bout start |
| `V2Hud.RematchRequested` | `event Action` | **Landed.** UI cannot reference App, so the composition root (`BoutController`) decides how to reload |
| `HudView.Show` / `Hide` / `HideImmediate` | `void` | **Landed.** Fade on unscaled time |
| `V2Hud.MenuRequested` | `event Action` | **Landed 2026-10-19.** Results' MAIN MENU; the composition root returns to the front end |
| `ScreenManager.Navigate` | `void Navigate(ScreenId to)` | **Landed (front end).** Applies `ResolveNav(Current, to)` |
| `ScreenManager.ResolveNav` | `NavOp ResolveNav(ScreenId from, ScreenId to)` | The stack rule for a link: Ignore / Push / Replace / PopTo / ResetTo / Match |
| `ScreenManager.Push` / `Replace` / `PopTo` / `ResetTo` | `void (ScreenId, bool animate = true)` | Direct stack operations (flows call these, e.g. Loader -> `ResetTo(MainMenu)`) |
| `ScreenManager.Back` | `bool Back()` | Device back / Escape (wired by `FrontEndBridge`); false at the bottom of the stack |
| `ScreenManager.MatchRequested` | `event Action<ScreenId>` | A link into the bout; the App swaps scenes |
| `ScreenManager.StartScreen` / `Motion` | properties | Set by `FrontEndBridge` from `GameRoot` before the stack opens |
| `ScreenId` / `ScreenIds.IsMatch` / `IsTab` / `TryParse` | enum + helpers | Named after the mockup files; values are serialized, append only. V1 ids are `V1<Name>` = 100-115; V1's bout screens are `IsMatch`, its bottom-nav four `IsTab` |
| `ScreenIds.ForPage` | `ScreenId ForPage(ScreenId id, FrontEndPage page)` | Where a request lands on a page: on V1, Title -> `V1Main`, MainMenu -> `V1Home`; otherwise unchanged ([ADR-0015](../Decisions/ADR-0015-v1-page-behind-a-tuning-switch.md)) |
| `GameConfig.frontEndPage` | `FrontEndPage` (Data) | V2 (default) or V1; read by `FrontEndBridge` (App) |
| `ScreenManager.Close` | `void Close(bool animate = true)` | **Landed 2026-10-20.** Hide the top screen and empty the stack (`Current == None`): the bout overlay on Resume |
| `ScreenManager.Hosts` | `bool Hosts(ScreenId id)` | Whether the serialized table has a UXML for it |
| `ScreenManager.LeaveRequested` | `event Action<ScreenId>` | A link to a screen this stack does not host (`NavOp.Leave`), e.g. the bout overlay's RankUp -> MainMenu |
| `GammaPresenter._presentWhileOpen` | serialized `ScreenManager` | Present only while that stack shows a screen (the bout overlay). Empty: always |
| `V2Hud.PauseRequested` | `event Action` | Any HUD pause button; the composition root pauses |
| `HudPauseButton.Pressed` / `Covers` | `event Action` / `bool Covers(Vector2 screenPx)` | One per pause control (scorebug, C6 letterbox); `Covers` is true only while its owning screen is shown |
| `V2Hud.BlocksTap` | `bool BlocksTap(Vector2 screenPx)` | True when any pause button covers the press, so it is not a slap tap; wired as the tap source's `Blocker` (M02). Walks an array, no allocation |
| `HudContext.Now` / `IsPaused` | `double` / `bool` | Instance members now (the static realtime `Now` is gone): `MatchDirector.Now`, so phase timers freeze on pause |
| `BoutController.Pause` / `Resume` / `Forfeit` (App) | `bool ()` | Pause = director + hitstop + PauseMenu; Resume closes the overlay at once; Forfeit -> Defeat card |

**Consumed by:** M01 (triggers screen transitions), M00 (initial menu push).
**Stability:** screen stack is stable; individual views will churn with art.

### Roster tab ([ADR-0022](../Decisions/ADR-0022-champion-roster-and-stats.md))

`RosterView` replaces the `k-card` grid's children with generated cards on first show and keeps the header, chips and bottom bar. `ScreenManager.Roster` / `SelectedChampionId` are set by `FrontEndBridge` from `GameConfig.roster` / `GameRoot`; a tap raises `ChampionSelected(id)`. The gold `k-bgold` button is relinked to `Search` and relabelled PLAY. V1 `FighterSelect` still shows its mockup.

## 4. Configuration

### Portrait layout

**Landed (V2 HUD):** the reference resolution is the V2 frame, **430×932 CSS px**. `CanvasScaler` uses `ScaleWithScreenSize` with **`Expand`**, so the whole reference frame is always visible and taller or wider devices gain margin instead of cropping. Bleed art stretches to the screen edges; Safe content stays inside `Screen.safeArea`. The table below is the original pre-V2 plan and is kept for the ScreenManager work.

| Element | Position (from top) | Size | Notes |
|---|---|---|---|
| Turn indicator | 80 px | 120×60 | "YOUR TURN" / "OPPONENT'S TURN" |
| Opponent health bar | 160 px | 800×40 | Right-to-left fill |
| Player health bar | 240 px | 800×40 | Left-to-right fill |
| Power bar | Bottom + 320 px | 600×80 | Centered, visible during charge |
| Timing bar | Bottom + 320 px | 600×80 | Same position, swaps with power bar |
| Damage popup | Center | Dynamic | Scales with damage tier |

### In-bout HUD attention budget (`HudAttentionConfig`)

`Data/UI/HudAttention.asset`, wired into `GameConfig.hudAttention` by Build Playable Prototype and
shown in the Tuning Hub. With no asset, `V2Hud` uses the class defaults. Alphas are **CanvasGroup**
alphas over the builder's art; solid washes (intro dim, K.O. tint) are baked opaque, so their
value is final. Times are ms (R8). `Validate` encodes the hard rules (headline gone within 1 s and
within the D1 screen, yield <= 300 ms, slam delay <= 1 s, NaN rejected); `GameConfig.Validate`
runs it. The rationale and the before/after numbers are in [POLISH §9](../POLISH.md).

| Phase | Coverage (HUD ink, alpha >= ~0.3) before -> after | Main knobs |
|---|---|---|
| Intro C4 | ~30% + dim 0.45 -> ~12% + dim 0.15 | `introDimAlpha`, `introRingScale` (ring moved to the lower third in the builder) |
| Charge C6 | ~40% + full rays/vignette -> ~18% | `letterboxPx`, `chargeVignetteAlpha`, `chargeGaugeScale`, `chargePromptScale`, `chargePromptFadeAfterTaps`, `chargeRays*`, `chargeLockDot` |
| Timing C5 | ~25% -> ~10% | `bannerHoldMs`, `showPowerMeter` (off), `timingShadeAlpha` |
| Impact D1 | ~65% peak, 1.25 s -> <=35% peak, <=10% by 700 ms | `impactHeadline*`, `impactDamage*`, `impactRays*`, `impactVignetteAlpha`, `impactYieldMs` |
| Opponent D2 | ~15% + looping red edge -> ~8%, still edge | `opponentVignetteAlpha`, `opponentVignettePulse`, `braceCuePulseMs`, `incoming*` |
| K.O. D3 | 0.55 tint at once -> 0.25 tint, slam +350 ms | `koSlamDelayMs`, `koTint*`, `koBurstAlpha`, `koStatsAtBoutEnd` |
| Scorebug | ~9% at 1.0 -> ~5% at 0.75 | `scorebugRestAlpha`, `scorebugHitHoldMs`, `topShadeAlpha`, `liveDotPulse` |

Coverage figures are estimates from the builder geometry, checked against Play captures
(`Captures/HudAudit` before, `Captures/HudAfter` after). They are not measured per pixel, and the
values in the built scene and `.asset` are R11 until the tests load them. E2/E3 are menus and are
outside the budget.

### Front end (UI Toolkit)

- **Panel.** `Settings/UI/FrontEnd.PanelSettings.asset`: reference 430×932, `ScaleWithScreenSize`,
  `Expand` (the HUD's rule). The template's `clearColor` is **off**; only `GammaPresenter`'s
  runtime copy clears its render target (S-077).
- **Frame and bleed.** Each screen is a 430×932 frame centred on the panel. The mockup's
  full-screen `.fill` layers and the screen background bleed to the panel edges, so taller or
  wider phones gain image, not bars.
- **Motion.** `Data/UI/UiMotion.asset` (`UiMotionConfig`) holds all durations in ms, including
  screen in/out, stagger, press, count-up, rays, LIVE pulse, ticker speed and the Title beam
  blink (`titleBeamStepMs` 110, `titleBeamPattern` levels 0..1, `titleBeamOffsetMs` 440 for the
  second beam; the existing asset picks up the C# defaults until re-saved, unverified). The flow timings
  live there too (loader, search, walkout, tape auto-start, pack reveal). Mockups are static, so
  these are design choices.
- **Text.** TTFs via `-unity-font-definition`: Barlow for V2, Cabin and Archivo Black for V1
  (`Art/Fonts`, CONTENT-MANIFEST). `letter-spacing` is a `<cspace>` tag (USS
  letter-spacing does nothing here, S-076). Gold, red and silver text are TextCore gradient
  presets in `Resources/UI/TextGradients` (two stops; the CSS middle stop is dropped): `Gold`,
  `Red`, `Silver` for V2 and `V1Gold`, `V1Red`, `V1Silver` for V1. A gradient label must not
  have a `text-shadow` (S-082). Labels are `nowrap` in the theme; the converter sets
  `white-space: normal` where CSS wraps (S-084).

### `SafeAreaFitter`

Reads `Screen.safeArea` and insets the HUD root `RectTransform`'s anchors. Handles:
- Top notch (iPhone Dynamic Island, Android punch-hole) — insets top.
- Bottom home indicator (iPhone) — insets bottom.
- Rounded corners — no additional inset needed if the HUD root has 16 px padding.
- Landscape safe area is ignored (portrait-locked).

Recalculates on `OnRectTransformDimensionsChange` and on a 1-second poll (some Android OEMs do not fire the callback on orientation lock).

## 5. Events

**Emits**

None — the UI is reactive, it does not drive gameplay.

**Consumes**

| Event | From | Reaction |
|---|---|---|
| `MatchSignal.ChargeStart` | M01 | Show power bar, hide timing bar |
| `MatchSignal.TimingStart` | M01 | Hide power bar, show timing bar |
| `MatchSignal.ImpactFrame` | M01 | Spawn `DamagePopup` |
| `MatchSignal.TurnStart` | M01 | Update turn indicator |
| `MatchSignal.BoutEnd` | M01 | Push victory screen |
| `HealthChanged` | M06 | Animate health bar drain |

## 6. State machine

`ScreenManager` is a stack, not a FSM. Screens are pushed and popped.

Typical flow (as built, 2026-10-20). The HUD is uGUI and outside the stack; the bout overlay is
its own stack in `Proto_Bout`:
```
FrontEnd stack:  [Title] → [Loader] → [MainMenu] → … → link into the match → (scene swap)
Bout overlay:    (empty) → pause → [PauseMenu] → Resume → (empty)
                                    [PauseMenu] → [Settings] → back → [PauseMenu]
                                    [PauseMenu] → [Replay] → [RoundEnd] → Round 2 → (empty, resumed)
                                    [PauseMenu] → Forfeit → (empty) → HUD Defeat card
                 HUD Victory card → MAIN MENU → [RankUp] → Collect all → (scene swap) → FrontEnd [MainMenu]
```

## 7. Dependencies

- **Hard:** `SlapMania.Core` (signals), `SlapMania.Data`, Unity UI (`UnityEngine.UI`, `TMPro` — the `Unity.TextMeshPro` asmdef reference landed 2026-09-26).
- **Soft:** M03 (poll fill), M04 (poll position + zone layout), M06 (poll HP), M09 (atlas texture).
- **Unity APIs:** `Canvas`, `CanvasScaler`, `Image`, `Slider`, `TextMeshProUGUI`, `Screen.safeArea`, `RectTransform`.
- **Must initialize after:** M00.

## 8. Failure modes

| Failure | Detection | Behaviour | Player-visible? |
|---|---|---|---|
| Safe area returns zero rect | Guard — fall back to full screen | Log Warn | No |
| TMP Essentials not imported | Pink quads | BUILD-AND-RUN step 5. The prototype builder refuses to run without them and offers the import | Yes — all text pink |
| Filled bar never drains | `Image.type = Filled` with no sprite draws the full rect and ignores `fillAmount` | The builder assigns a generated white sprite (`Art/Prototype/UI/Proto_White.png`) | Yes — bars stuck full |
| Canvas rebuild every frame | Profiler UI module shows rebuild count > 0 | Use `LayoutRebuilder.ForceRebuildLayoutImmediate` only when values change, not per-frame | Frame hitch during charge |
| Timing bar zones do not match layout | `TimingBarView` generates from the asset at runtime | This cannot desync — it is generated, not hand-placed | Impossible by construction |
| Screen stack underflow (pop with nothing below) | Assert | Ignore, log Error | No |
| Front end shows only the clear colour | `ScreenManager.Current == None`; `_screens[i].tree` null (S-075) | `FrontEndSceneBuilder` now throws on a null tree | Yes - blank menus |
| Captions look cramped | USS `letter-spacing` ignored by TextCore (S-076) | Converter emits `<cspace>` + trailing `padding-right` | Yes - tracking lost |
| Grey screen after returning from a bout | A panel built from the clearing template outlived FrontEnd (S-077) | Template never clears; `GammaPresenter` never re-attaches it | Yes - grey menus |
| Exception on the first Loader -> Main Menu hand-over | A tick navigated and created a view while `_views` was enumerated (S-078) | Views tick from an index-based list | No (logged) |
| Translucent UI too light | Linear-space blending | `GammaPresenter` (gamma-rendered panel texture) | Yes - washed-out glass |
| Front end suddenly in linear blending mid-Play, "GammaPresenter is not wired" | An `OnDisable` -> `OnEnable` cycle (a recompile during Play) found `panelSettings` nulled by the S-077 rule (S-081) | The presenter keeps its template in a hidden serialized `_template` and re-instantiates from it | Was: washed-out menus until restart |
| Pressing the HUD pause button commits the timing slap / adds charge | The tap action listens to the whole screen; the press-down fires before the button's click | `InputSystemTapSource.Blocker = V2Hud.BlocksTap`: a press inside the visible pause button is dropped | Would be: a wasted slap |
| HUD shot clock or intro countdown keeps running on the pause menu | They read realtime | They read `HudContext.Now` (`MatchDirector.Now`, pausable) and `HudView` stops ticking while paused | Would be: wrong clock after resume |
| Game stays frozen after leaving a paused bout | `Time.timeScale` left at 0 | `HitstopController.OnDisable` restores the pre-pause scale (and any freeze) when the bout unloads | Would be: frozen next bout |
| Taps land on the wrong element; the top tab bar does nothing (S-091) | `GammaPresenter.ScreenToPanel` flipped y, but UI Toolkit hands it top-left pixels already: every touch mirrored to `932 - y` | `ScreenToPanel` is the identity | Yes - dead tab bars |
| Overlay costs a full-screen blend during Charge/Timing | `GammaPresenter` composites every frame | `_presentWhileOpen`: output and target clear off while the stack is empty | No (perf) |
| Pause menu clicks fall through to the HUD | Panel input sorted below the HUD canvas | The presenter copies its output canvas's sorting order (20) onto the panel | Yes - buttons under the menu |
| A screen hook missing (`loader-fill`, `ticker`...) | `ScreenView.Find` logs Warn; the converter fails loudly when a hook matches nothing | Re-run the converter; update `HOOKS` if the mockup changed | Motion missing on that screen |
| Gradient text renders plain white (S-082) | The label also has a USS `text-shadow`; UI Toolkit then ignores its `<gradient>` runs | The converter never combines them: hard shadow -> shadow twin, soft shadow -> dropped. Hand-written UI must follow the same rule | Yes - white titles |
| A paragraph runs on as one line past its panel (S-084) | Theme labels are `nowrap`; the box was sized as one line | Converter predicts wrapping (`ttfmetrics.py`) and sets `white-space: normal` + an n-line box. A width it cannot resolve means no prediction (one line) | Yes - text off the panel |
| V1 boxes a few px short, layout drifts down a column | V1 is content-box (no `*{box-sizing:border-box}`), UI Toolkit is border-box | The converter adds padding + border to px sizes on content-box pages; `%` sizes are reported in CONVERSION-REPORT | Yes - ~1 px per bordered row |

## 9. Debug hooks

- **Log channel:** `LogChannel.UI`
- **Overlay (UI tab):** screen stack contents, safe area rect, canvas scale factor, rebuild count this frame.
- **Cheats:** push/pop any screen, force safe area insets (slider), show layout guides.
- **Recorder events:** `ScreenPushed`, `ScreenPopped`.

## 10. Tuning notes

**Zero canvas rebuilds per frame during Charge.** The power bar is a `Slider` whose `value` property only triggers a rebuild if the value actually changed. If the bar is polled at 60 fps but the fill changes at 10-15 tps (the tap rate), most frames have no rebuild. Do not use `SetVerticesDirty()` in `Update` — it forces a rebuild every frame.

**`TimingBarView` must generate zones from the `TimingZoneLayout` asset.** If zones are hand-placed `Image` objects in the scene, any change to the zone widths in the config requires a matching manual edit to the UI. They will desync, and when they do, the game lies to the player about where red is. Generation from the asset makes this impossible.

**Long prompts auto-size; the punch-in still overshoots for a moment** (measured on the pre-V2 HUD; the V2 `ChromaText` prompts use the same TMP auto-size and `FeelConfig` punch). The prompt is TMP auto-sized (56–108 pt), so "OPPONENT AIMING" settles at ~101 pt inside the 1040-wide rect. For the first `FeelConfig.calloutPunchMs` (140 ms) it is scaled by `calloutPunchScale` (1.35x), which briefly pushes the longest prompts past the screen edge — a deliberate "slam-in", verified on 2026-09-26. If it reads as sloppy, lower `calloutPunchScale` toward 1.15 in the Tuning Hub; no code change needed.

**The damage popup must not allocate.** Use a pre-instantiated pool of `TextMeshProUGUI` objects (8 is enough for any realistic scenario). Dequeue on impact, return on fade complete. `TextMeshProUGUI.SetText(float value, ...)` does not allocate; `SetText(string)` with `ToString()` does. Use the float overload or a pre-formatted `StringBuilder`.

**The victory gallery is a `RawImage` per tile, all referencing the same atlas `RenderTexture`.** Each `RawImage.uvRect` is set to the tile's UV rect from `SnapshotSystem.GetTileUV`. This is one draw call for up to 16 photos. Do not instantiate separate `Texture2D` objects per photo.

**Tune the HUD from its budget, not from the views.** A HUD element that "needs to be bigger" goes through `HudAttentionConfig` first: raise its scale or hold, and see whether `Validate` and POLISH §9 still hold. A change that breaks a rule (two headlines at once, an effect still up after 1 s) is a design change and gets a note in POLISH §9.

**Mashing turns a "kick" into a level.** At 8-15 taps/s a per-tap effect never decays, so C6's speed lines sit at `chargeRaysKickAlpha` for the whole charge. Tune that value as the mashing level, not as a flash (0.5 -> 0.1 final, seen in Play 2026-10-23).

## 11. Open questions

- [ ] Should the victory screen have a "share" button that captures a screenshot of the gallery? Platform-specific and potentially out of scope for v1.
- [ ] Should damage popups use world-space canvas (positioned near the defender's head) or screen-space overlay (positioned above the health bar)? World-space is more immersive but requires camera-to-screen projection. *Prototype answer (V2 HUD):* screen-space overlay, **anchored to the defender's head** every frame through `Camera.WorldToScreenPoint`, so it follows the interim `CameraRig` intro fly-in and impact kick (M16).
- [ ] Is a settings screen needed in v1, or is the single-bout loop simple enough that there is nothing to configure? *2026-10-20:* B6 Settings is reachable from the front end and from the bout's pause menu; its values persist (`fe.*` PlayerPrefs) but nothing reads them yet (M11/M12).
- [x] Should the player's own charge (C6) get a pause control? **Yes (owner, 2026-10-20):** a second `HudPauseButton` in the top-left of the C6 top letterbox. Watch playtests for accidental pauses mid-mash; if they happen, the knob is position/size in `V2HudBuilder.BuildCharge`.
- [ ] RankUp is shown from the Victory card's MAIN MENU (the mockup's "Continue -> RankUp"); PLAY AGAIN skips it. Revisit when divisions (M12) make rank-up conditional.

## 12. Changelog

| Date | Commit | Change |
|---|---|---|
| 2026-11-07 | (pre-git) | **Slapshot Polaroid (M09).** New `HudSlapshot` (HudView), built in code by `V2Hud.Bind` under the HUD canvas (not by V2HudBuilder, so the bout scene's HUD is not regenerated); fonts borrowed from the HUD (BarlowCondensed BlackItalic / Bold). `V2Hud.ShowSlapshot(photo, defender, damage)` (from `BoutController` when `LastWasSlapshot` and not lethal): shutter flash, tilted Polaroid with a SLAPSHOT! header and the round, the photo developing from white, the slapped fighter's name and damage / zone / power. Flies off before the hold ends; `Yield` on ChargeStart, hidden by the K.O. and the results. Never takes a raycast. `V2Hud.Bind` takes `playerName` (the player's champion; was the 'ALUCARD' placeholder). Verified in Play over MCP at 430x932 |
| 2026-10-25 | (pre-git) | **Roster tab is live ([ADR-0022](../Decisions/ADR-0022-champion-roster-and-stats.md)).** New `RosterView`: one card per `ScreenManager.Roster` champion (mockup fonts read off the UXML), in a scroll view; tap selects (EQUIPPED pill, others dim), the bottom bar names the pick, and the gold UNLOCK button becomes PLAY (-> Search). `ScreenManager.Roster`, `SelectedChampionId`, `ChampionSelected`; `ScreenView.Relink`. `FrontEndBridge` wires roster and pick to `GameRoot`. Without a roster the mockup cards stay. V1 FighterSelect is not wired yet. |
| 2026-10-23 | (pre-git) | **HUD attention budget ([POLISH §9](../POLISH.md)).** New `Data.HudAttentionConfig` (`Data/UI/HudAttention.asset`, `GameConfig.hudAttention`, Tuning Hub), `HudContext.Attention`, `HudView.ShownAlpha`/`Budget`/`Sec`. Per view: scorebug rest alpha 0.75 with full alpha on a hit, 140 px top shade, still LIVE dot. C4: dim 0.15, ring at 0.62 moved to the lower third. C6: letterbox 36 px, vignette 0.47, gauge 0.73, prompt 0.625 dimming after 3 taps, speed lines 0.3 rest / 0.5 per tap, no lock-on dot, reticle glow 12. C5: YOUR TURN fades after 800 ms, side power meter hidden (`_meterGroup`), shades 0.6, ring glow 8, PERFECT/x3 tags mirror instead of leaving the screen (**S-099 fixed**). D1: headline 0.73 and gone by 700 ms, number 0.75, rays 0.35 for 350 ms, vignette fades with the headline, no bottom shade, `Yield()`. D2: still vignette 0.46, one pulse on BRACE!, 3 px edge, INCOMING! fades after 1 s. D3: slam +350 ms via an `OnTween` timer, tint 0.25 then 0.5, burst group 0.5 (rays, hot spot, confetti), stats at BoutEnd. `V2Hud`: one headline at a time, deferring D2 behind D1 (**S-100 fixed**). Builders: new CanvasGroups (`_topShade`, `_dim`, `_vignette`, `_rays`, `_promptGroup`, `_bannerGroup`, `_meterGroup`, `_tagGroup`, `_burst`), intro ring position, D1 bottom shade removed, D2 edge 3 px, intro dim and K.O. tint baked opaque. Charge/Timing per-frame work is unchanged except reading floats from the budget. Every new tween is per tap or per edge, from the fixed-slot Tweener (R5). **Verified in Play over MCP** with auto-played bouts and captures (`Captures/HudAudit` before, `Captures/HudAfter` after); the K.O. slam was sampled per frame because Game-view captures lag while the Editor is unfocused. **Rebuild the prototype** to get the new groups. §2, §4, §10 updated. Decision: [ADR-0021](../Decisions/ADR-0021-hud-attention-budget.md) |
| 2026-10-23 | (pre-git) | **D2 brace cue ([ADR-0019](../Decisions/ADR-0019-brace-tap-defence.md)).** `HudOpponentTurn` gains a brace block (`_brace`, `_braceRoot`, `_bracePrompt`, `_braceSub`, built by `V2HudBuilder`): BRACE! / TAP AS THE HAND LANDS during the rival's wind-up, then PERFECT BRACE / BRACED (BLOCKED n%), FLINCHED (TOO EARLY/LATE) or NO BRACE. `V2Hud` routes `WindupStart`/`BraceResolved`, and on the rival's impact calls `HideSoon()` (0.8 s) instead of hiding at once. The mockup's hold-ring block and Lean are not built. **Rebuild the prototype** to get the brace block |
| 2026-10-23 | (pre-git) | **D4 Instant Replay is live** (M09, [ADR-0016](../Decisions/ADR-0016-slap-gallery-pre-atlas-capture.md)). New `Screens/ReplayView` (registered in `ScreenViews`): on show it puts `ScreenManager.Replay.Photo` (the latest slap's 640 x 800 snapshot) into the `fe-img` panel, and fills the header (`ROUND n · FINAL BLOW / LAST SLAP`), the three `k-tl` tiles (DAMAGE, POWER, TIMING ×mult ZONE) and the timeline (`IMPACT · mm:ss.s`). Its hooks are shapes, not names, because the UXML is generated; a missing hook logs once at bind. New `ReplayShot` and `ScreenManager.Replay`/`ReplayFinished`. With `EndOfBout` set, Close and Next both raise `ReplayFinished`. `V2Hud`: `OfferReplay` + `ReplayRequested`, so the K.O.'s CONTINUE hands over to D4, and `ShowResultScreen()` brings E2/E3 back. `BoutController`: K.O. -> D4 -> results, and Escape/back on that D4 also goes to results. With no photo, D4 shows the mockup still as before. Camera/speed chips, Save and Share remain placeholders. No scene rebuild needed: the overlay already hosts Replay |
| 2026-10-23 | (pre-git) | **AIM cue, slap gallery, opponent name.** `HudTiming` (TapMarker) opens on M04's AIM cue (`TimingMeter.InCue`): copy "AIM · STOP MASHING / GET READY...", then the tap copy and a ring punch when the marker starts. Copy is set on edges only (R5) (S-093, [ADR-0017](../Decisions/ADR-0017-aim-cue-and-spam-gap.md)). `HudResult` shows the loser's three-photo gallery on E2/E3 ([ADR-0016](../Decisions/ADR-0016-slap-gallery-pre-atlas-capture.md)), and `HudContext` gained `Gallery`, `GalleryDamage`, `GalleryCount`. `V2Hud.Bind` takes an optional `opponentName`, the AI tier's `displayName` (M07 ladder), because views read names at Bind. **Rebuild the prototype** (Tools > Slap Mania > Prototype > Build Playable Prototype) so the scene gets the gallery frames. Until then the gallery fields are empty arrays and nothing shows. §2 screen table updated |
| 2026-09-19 | (pre-git) | Created (Draft) with screen stack and HUD layout |
| 2026-09-26 | (pre-git) | **P5 partial.** `SlapMania.UI` asmdef created (`-> Core, Data, Gameplay, Logging, UnityEngine.UI`; TMP deferred). A single `BoutHud` component landed — the minimal playable HUD folding together this module's `PowerBarView`/`TimingBarView`/`HealthBarView` responsibilities: both HP bars always on, power bar shown during `Charge`, timing bar shown during `Timing` with the marker riding over zone bands **generated from `TimingZoneLayout` at bind** (per §10, generation-not-hand-placement). Per-frame values are polled in `LateUpdate` (§5) with zero allocation. **Known simplifications:** no `ScreenManager`/`IScreen` stack, no `SafeAreaFitter`, no `DamagePopup`, no victory gallery, no TMP text (bars only) — those are the rest of M10. HP/power bars require `Image.type = Filled` set in the scene, an Inspector value **unverified from code** (R11). |
| 2026-09-26 | (pre-git) | **Playable prototype HUD ([ADR-0011](../Decisions/ADR-0011-adopt-v2-design-prototype-first.md)).** `SlapMania.UI` now references `Unity.TextMeshPro` (the edge CONVENTIONS already listed). `BoutHud` gained HP numbers, a slap counter and a power number — each written with TMP `SetText(format, value)` **only when the integer changes**, so a steady frame costs no canvas rebuild and nothing allocates (R5). New `BoutCallouts` (prompt, zone, damage and crit pops; punch/hold/fade on unscaled time; timings from `FeelConfig`) and `BoutResultPanel` (result card + `RematchRequested`). The canvas (1080x1920, match 0.5, Screen Space - Overlay, V2 UI-kit palette) and all wiring are generated by `PrototypeHudBuilder` in M13, which resolves the R11 note in the row above for the prototype scene. §2, §3, §7, §8, §11 updated. Still missing: `ScreenManager`/`IScreen`, `SafeAreaFitter` (S-063), the victory gallery. |
| 2026-10-19 | (pre-git) | **V2 front end in UI Toolkit ([ADR-0013](../Decisions/ADR-0013-ui-toolkit-front-end-from-mockups.md)).** 20 screens generated from the Design canvas by `Tools/mockup2uxml`. Landed: `ScreenManager`/`ScreenId`/`NavOp`, `ScreenView` + 8 screen behaviours, `PaintBox` (+ `PaintTextureCache`, Core.Paint), `VisualTween`, `GammaPresenter` + `GammaComposite` shader, `KitSwitch`, `KitSlider`, `Kit.uss`, and `UiMotionConfig` (Data). The flow Boot -> FrontEnd <-> bout was verified end to end in Play. HUD: results MAIN MENU (`HudResult`/`V2Hud.MenuRequested`). Fidelity measured against headless-Edge renders, every screen <= 3.0 % differing. §2, §3, §4, §8 updated. **Still open:** the link stack rule in `ResolveNav` (TODO), the V1 page, and the mechanics behind the placeholder screens (ADR-0011 §3). D4/D5/E1/E4 are converted but not yet shown in the bout. |
| 2026-09-26 | (pre-git) | **V2 in-match HUD replaces the prototype HUD.** `V2Hud` + one `HudView` per V2 screen (scorebug, C4 intro, C6 charge, C5 timing ring over the M04 meter, D1 impact, D2 opponent turn, D3 KO, E2/E3 result), procedural uGUI graphics (`SlapPanel`, `SlapArc`, `SlapRays`, `SlapGlow` on `ShapeMesh`), `ChromaText`, `HpBar`, `PressScale`, `SafeAreaFitter`. Animation is the in-house `Core.Motion.Tweener` on unscaled time (owner chose in-house over DOTween; ADR-0007 stands). Canvas reference 430×932 with `Expand`; Bleed/Safe layers per screen. Barlow fonts (OFL). `BoutHud`, `BoutCallouts` and `BoutResultPanel` deleted. §2, §3, §4, §10, §11 updated. Still missing: `ScreenManager`/`IScreen`, the victory gallery (M09). |
| 2026-10-20 | (pre-git) | **D5/D4/E1/E4 in the bout: the overlay and pause.** `Proto_Bout` gains a UI Toolkit screen stack over the uGUI HUD (`FrontEndSceneBuilder.BuildScreenHost`, sorting 20; PauseMenu, Replay, RoundEnd, RankUp, Settings) and the scorebug a pause button (Ready/Aim/Brace mockups; none in C6). `ScreenManager.Close` / `Hosts` / `NavOp.Leave` + `LeaveRequested` (the `ResolveNav` TODO(human) is untouched); `GammaPresenter` presents only while its stack is open and copies its canvas order onto the panel; `V2Hud.PauseRequested` / `BlocksTap`; `HudContext.Now` is the director's pausable time and `HudView` stops ticking while paused; `V2Hud.OnBoutEnd` also hides the intro (a forfeit can end it). Pause mechanics: M01 (director), M08 (hitstop), M02 (tap blocker), M00 (`BoutController.Overlay.cs`). **S-081 fixed:** `GammaPresenter` survives an `OnDisable`/`OnEnable` cycle. **Verified in Play over MCP** (Boot -> FrontEnd -> bout, TapMarker turns): paused mid-Charge for 26 s on one director: sim clock, fill and tap count frozen, 4 taps on the menu dropped, `timeScale` 0, PauseMenu presenting at sort 20; Resume (a real `ClickEvent` on `nav-Aim`) continued from the exact instant (sim since pause = real since resume, 0.4 ms apart); scorebug button mid-Timing: `BlocksTap` true on the button and false mid-screen, marker held at 0.4769 and meter `Armed` through ~80 paused frames, Escape resumed it; pause inside hitstop held `timeScale` at 0 with the freeze kept, resume restored 0.02 then 1; Settings -> back, Replay -> Close, Replay -> Next -> RoundEnd -> Round 2 all routed; Forfeit -> BoutEnd, `Forfeited`, Defeat card, `timeScale` 1; a won bout -> Victory MAIN MENU -> RankUp -> Collect all -> FrontEnd MainMenu. **R5 in Play** (profiler, 278 frames of Charge -> Timing with a pause in each): no allocation from the pause path in steady play frames. The steady-frame allocations seen are Editor-only or pre-existing: TMP numeric `SetText` (Editor-only `m_text` sync), UI Toolkit `UpdateAssetsInEditor`, and `SendMouseEvents` + `EventSystem.Update`, which predate this change and need a device capture (S-083). Showing or hiding the overlay allocates on those frames only. EditMode 195/195. Screenshots: `Temp/BoutOverlayShots/`. §2, §3, §6, §8, §11 updated. |
| 2026-10-20 | (pre-git) | **The V1 page beside V2 ([ADR-0015](../Decisions/ADR-0015-v1-page-behind-a-tuning-switch.md)).** 16 V1 screens converted into `Prefabs/UI/Screens/V1` (`ScreenId.V1Main`=100 ... `V1Defeat`=115, appended; V1 bout screens `IsMatch`, bottom-nav four `IsTab`). New `ScreenIds.ForPage`; `GameConfig.frontEndPage` (Tuning Hub > FRONT END) picks the page, `FrontEndBridge` maps the start screen (M00). `LoaderView` takes its next screen (V1 Loading -> V1 Home). `Kit.uss` gained `.kit-switch--v1`. Converter: per-page `PAGES`, Cabin + Archivo Black with measured metrics (Archivo baseline correction), CSS weight matching + faux bold, content-box pages, V1 gradient presets (`V1Red`/`V1Gold`/`V1Silver`, created by `EnsureGradients` on every build), shadow twins for hard shadows under gradient text (S-082) and predicted wrapping (S-084, `ttfmetrics.py`). Fidelity: all 16 V1 screens 0.1-2.6 % (§2). V2 output unchanged except Walkout's "VS" (soft shadow dropped so its silver gradient shows). Verified over MCP: compile clean, EditMode 195/195, Boot -> FrontEnd Play with captures; the V1 switch path played end to end (Boot -> V1Main -> V1 Loading -> V1Home -> Matchmaking -> VS Intro -> bout -> MAIN MENU -> V1Home, no errors; V2 boot -> Title) and V2 Walkout re-scored at 1.3 %. Re-played clean in a second session (another session's runner had called `RequestMatch` inside the first run): `MatchRequested(V1Countdown)` was the only bout trigger, the defeat card's MAIN MENU button (after KO Continue) returned to `V1Home`, V2 boot -> Title, Walkout 1.2 %, 0 errors/warnings; the win path through RankUp was not played. `GameConfig.frontEndPage` left at V2. §2, §3, §4, §8 updated. |
| 2026-10-20 | (pre-git) | **C6 pause button (owner's request).** Pause controls are now a reusable `HudPauseButton` (`Pressed`, `Covers`), routed by `V2Hud._pauseButtons` into `PauseRequested`. `V2Hud.BlocksTap` walks the array with no allocation. The scorebug's pause code moved into the component (`HudScorebug` lost `_pause` / `PauseRequested` / `BlocksTap`). A second button sits in the top-left of the C6 top letterbox (Safe 16,16), away from the mashing area. **Verified in Play over MCP:** during the player's charge the C6 button is live and covers its own centre, while the hidden scorebug button does not; `BlocksTap` is true on it and false mid-screen; the tap source's `Blocker` agrees. Clicking it paused mid-Charge (`timeScale` 0, PauseMenu, fill and sim time frozen) and Resume continued the charge. Game-view capture checked. EditMode 195/195. Prototype rebuilt; note DEBUG S-085, the builder's pre-existing capsule-fighter fallback, which is unrelated to this change. §2, §3, §11 updated. |
| 2026-10-23 | (pre-git) | **HUD for the hold-ring attack ([ADR-0014](../Decisions/ADR-0014-hold-ring-attack-mode.md)).** Each view reads `MatchDirector.CurrentAttackMode`. `HudTiming`: ring from `HoldRingMeter.Closure`, chrome fade at the press (new `_chrome`/`_shade` groups), mode copy (`_captionText`, `_hint`, `_hintSub`). `HudCharge`: wind-up arc, reticle hidden, HOLD!/RELEASE! (`_prompt`, `_hint`). `HudIntro`: mode hints (`_hintCharge`, `_hintTiming`). `HudScorebug`: the shot clock counts the press timeout, then the time to full close. `HudOpponentTurn`: live hold power. `V2Hud`: the opponent-turn screen also shows at `TimingStart`. Copy is constant strings written once per show; nothing new runs per frame except the existing polls (R5). `V2HudBuilder` wires the new fields (M13). §2 screen table updated. |
| 2026-10-23 | (pre-git) | **Fix: mirrored touches on the front end (S-091).** `GammaPresenter.ScreenToPanel` flipped y on positions UI Toolkit had already flipped to top-left, so a tap at y hit `932 - y` (the Store tab bar was dead). It is now the identity. Found over the Unity MCP: hit-test, wiring and a synthetic `ClickEvent` were all fine; only the screen-to-panel mapping was wrong |
| 2026-10-25 | (uncommitted) | **Title: second spotlight + pattern blink.** `TitleView` clones the mockup beam (no UXML edit), sways the pair out of phase and drives both opacities from `BlinkPattern.Evaluate` (new, `Core.Motion`, 16 EditMode tests) with the second beam offset by `titleBeamOffsetMs`. New `UiMotionConfig` fields `titleBeamStepMs`, `titleBeamPattern`, `titleBeamOffsetMs`. No per-frame allocation (opacity is a struct write). Clone geometry copies the source's laid-out rect on `GeometryChangedEvent`, not its inline style at bind (S-101). Checked in Play Mode: both beams draw and trade off |
