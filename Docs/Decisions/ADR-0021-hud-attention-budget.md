# ADR-0021 — HUD attention budget: a tunable per-phase budget, one headline at a time

**Status:** Accepted
**Date:** 2026-10-23
**Deciders:** shobhit

## Context

The V2 HUD (M10, [ADR-0011](ADR-0011-adopt-v2-design-prototype-first.md)) was built coordinate
for coordinate from static mockups. In motion, with the M16 cinematic camera behind it, it hid
the show. An audit of every phase in Play (`Captures/HudAudit`) showed:

- D1 covered ~65% of the screen at its peak and stayed for 1.25 s.
- C6 covered ~40% and ran six moving elements, with full-screen speed lines.
- D2 looped a red edge pulse for the whole rival turn.
- The K.O. slammed over a 0.55 red tint on the same frame as the finisher shot.
- D1's headline was still up when D2's INCOMING! slid in (S-100), and C5's tags left the screen (S-099).

The owner approved a per-phase budget (coverage, alpha, lifetime) grounded in attention and
perception rules ([POLISH §9](../POLISH.md)), with the K.O. slam delayed 350 ms and the C5 side
meter hidden.

## Decision

1. **The budget is data.** `Data.HudAttentionConfig` holds every alpha, scale and ms of the
   budget. It lives at `Data/UI/HudAttention.asset`, is referenced by `GameConfig.hudAttention`,
   and is shown in the Tuning Hub. It is optional: with no asset, `V2Hud` uses the class
   defaults. Its `Validate` encodes the rules that make a change a design change rather than a
   tweak (headline gone within 1 s and within the D1 screen, yield <= 300 ms, slam delay
   <= 1 s, NaN rejected), and `GameConfig.Validate` runs it.
2. **Scales and alphas apply at runtime; layout stays in the builder.** Views scale roots and
   drive CanvasGroups from the budget, so Play-mode tuning is live. Only positions (the intro
   ring's lower-third placement), removed layers (D1's bottom shade) and baked-opaque washes are
   builder changes.
3. **One headline at a time.** On `ChargeStart`, `V2Hud` calls `HudImpact.Yield()`: the
   headline and number leave in `impactYieldMs`, and D2's show is deferred, on sim time, until
   the headline is off.
4. **The HUD waits for the camera, never the reverse.** The K.O. delay is a HUD-side timer; no
   camera file is touched.

## Consequences

### Positive

- The arena, the fighters and the camera are visible in every phase. Rest coverage is ~5%.
- Designers tune feel without a rebuild, and the hard rules fail tests instead of failing playtests.
- The D1/D2 overlap and the C5 tag clipping are fixed structurally, not by retimed delays.

### Negative

- The HUD no longer matches the V2 mockups pixel for pixel (sizes, alphas, the hidden meter).
  The mockups are now the art direction, not the spec, for the in-bout HUD.
- Each budgeted element needs a CanvasGroup or a scalable root, so the builder wires more
  references.

### Risks

- The `Tweener` shows a delayed tween's start value at once, and replaces a running tween of the
  same kind on the same target. Delayed beats must start hidden or use an `OnTween` timer. This
  was found in Play: the K.O. slam showed at 2.6x during its delay.
- Coverage numbers are estimates from geometry checked against captures, not per-pixel
  measurements. The serialized asset values remain R11 until the tests load them.

## Alternatives considered

| Alternative | Why rejected |
|---|---|
| Alpha-only pass (alphas, durations, loops; no size or layout changes) | Offered to the owner, who chose the full budget. About half the gain: D1's 470x200 headline and C6's gauge still cross the fighters at any alpha |
| Bake the budget sizes into `V2HudBuilder` | Every tuning pass would need a prototype rebuild, and Play-mode edits would not persist. Runtime scales keep the Tuning Hub loop |
| Keep both headlines, rely on shorter delays | Two independent timers will always overlap under some pacing. Sequencing through `V2Hud` makes the rule structural |
| Shorten D1 only, show D2 immediately | D2 would still slide in under a fading SMASH! for up to `impactYieldMs`. The deferral costs at most 150 ms of INCOMING! and none of the rival's charge |
| Delay the finisher camera instead of the K.O. slam | It is the camera's shot (M16); the HUD must not drive it |
