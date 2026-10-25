# ADR-0015 — The V1 page ships beside V2, chosen by a Tuning Hub switch

**Status:** Accepted
**Date:** 2026-10-20
**Deciders:** shobhit

## Context

The `Slap Mania Game UI` Design canvas has two pages. V2 became the front end in
[ADR-0013](ADR-0013-ui-toolkit-front-end-from-mockups.md). The owner asked for **both** pages
to be in the game. V1 is the earlier design: 16 screens (`Docs/Reference/Mockups/V1`), its
own CSS classes in each page's `<style>` (`.lbl`, `.dsp`, `.panel`, `.btn-red`, ...), its own
fonts (Cabin, Archivo Black), and a bout of its own (Countdown -> SlapTurn -> SmashHit ->
Defend -> RoundKO -> Victory / Defeat).

Nothing in either page links to the other, so "in the game" needed a rule for how a player or
designer reaches V1, and what V1's bout screens do.

## Decision

1. **Same pipeline.** `Tools/mockup2uxml` converts both pages (`PAGES`). V1 UXML goes to
   `Prefabs/UI/Screens/V1/`; its screens are `ScreenId.V1Main` (100) ... `V1Defeat` (115),
   and its links are `nav-V1<Name>`. The builder registers both pages in one `ScreenManager`.
2. **A Tuning Hub switch, not player UI.** `GameConfig.frontEndPage` (`FrontEndPage.V2` by
   default, or `V1`) is drawn in the Tuning Hub's FRONT END section. On V1 the menus open at V1's
   splash, and a bout's MAIN MENU returns to V1 Home (`ScreenIds.ForPage`).
3. **The real bout, like V2.** V1's eight bout screens are `ScreenIds.IsMatch`. A link into
   them (VS Intro -> Countdown) starts the real bout, which is the V2 HUD. Their UXML is
   converted and scored as a design reference, but navigation never shows it.
4. **V1's own fonts.** Cabin and Archivo Black (OFL) were downloaded with the owner's approval.
   They are the static TTFs the mockups render with, so text matches the reference metric for
   metric (CONTENT-MANIFEST, Fonts).

## Consequences

### Positive

- Both designs are playable from one build, and switching costs one Inspector field.
- No invented UI: neither mockup has a "switch design" control, and none was added.
- The converter gained what any future page needs: per-page kits, fonts with their own
  vertical metrics, `box-sizing: content-box` pages, and per-page gradient presets.

### Negative

- About 330 KB of extra fonts, and 16 more UXML files the front end carries.
- A player build can only reach V1 if the shipped `GameConfig` says so. That is intended.

### Risks

- V1 screen names (Matchmaking, Countdown, ...) could leak into code as mechanics. GLOSSARY
  ("V1 page") says they are *places* only.
- V1's titles are gradient text over a hard shadow, which UI Toolkit cannot draw on one label
  (DEBUG S-082); they are two labels (a *shadow twin*). Any hand-made UI copying the V1 style
  must do the same, or the gradient renders white.

## Alternatives considered

| Alternative | Why rejected |
|---|---|
| "Classic UI" toggle in Settings plus the Tuning Hub | Adds a player-facing control neither design has (ADR-0011: adopt designed mechanics only). The owner chose the switch. |
| A debug-menu switch | Needs the `SlapMania.Diagnostics` assembly, which does not exist yet (R9 asmdef change) for one enum. |
| Archive only (UXML generated, not registered) | V1 would not be "in the game", only viewable in UI Builder. |
| V1 bout screens as a static click-through | Shows a fake bout. Matching V2 keeps one bout implementation. |
| Substitute Barlow for V1's fonts | Text-heavy V1 screens would miss the 3 % fidelity target. |
