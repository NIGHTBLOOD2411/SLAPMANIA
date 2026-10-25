# Slap Mania - Polish & Game Feel

Status: Draft - Last verified 2026-10-23

A slap that computes the right damage but does not *land* is a bug with a passing test.
This is the checklist that turns correct into satisfying. It is a shipping gate, not a
stretch goal - [WORKFLOW §6](WORKFLOW.md) runs it before Definition of Done.

The numbers here are authored in **milliseconds** (R8) and live in the
[TUNING-BIBLE](TUNING-BIBLE.md) / ScriptableObject assets, not in code. This doc says
*what to feel for*; the assets say *how much*.

---

## 1. The impact stack - one hit, not three

On the contact frame, several things fire together and must read as a single event, not
a stutter of separate ones. Order and timing matter:

- [ ] **Hitstop** freezes on contact for the authored ms. `Time.timeScale` drops **and
      `Time.fixedDeltaTime` scales with it** or physics explodes on resume (S-042).
      Always restored to `1.0`.
- [ ] **Screen shake** is a decaying impulse on unscaled time, composed on top of the
      camera rig - never applied to the camera transform Timeline animates (S-043).
- [ ] **Impact VFX + rim flash** on the `SnapshotOnly`/`FighterFX` layers.
- [ ] **The audio impact stack** is scheduled **backwards from a predicted `contactDsp`**
      published at windup with >=180 ms lead (S-050) - see §3.
- [ ] **Snapshot** at contact + ~35 ms, during the hitstop, with white flash + shutter +
      polaroid pop, all on **unscaled** time ([ARCHITECTURE §4](ARCHITECTURE.md) step 13).

Acceptance: at 0.25x speed the stack reads as one beat with a clean anticipation ->
impact -> reaction arc. If it reads as three pops, the offsets are wrong.

---

## 2. Everything presentational runs on unscaled time

The single largest source of "why did the UI freeze" is a tween on scaled time during a
0.02 hitstop (S-041). The shared `Tweener` **defaults to unscaled** - keep it that way.

- [ ] HUD juice (power-bar jolt on each tap, lock flash, marker glow) - unscaled.
- [ ] Screen transitions and the `TransitionOverlay` - unscaled.
- [ ] The only thing on *simulation* time is the simulation.

---

## 3. Audio feel

Android output latency is 50-150 ms; a `PlayOneShot` on the contact frame arrives late
and the hit feels mushy. The whole audio design is built around scheduling ahead:

- [ ] Impact stack scheduled from `contactDsp` (windup + >=180 ms), not played on contact.
- [ ] Charge ticks are **two looping sources** (drone + ratchet) whose pitch tracks power
      and tap rate, plus <=2 decimated accents - **not** one one-shot per tap (S-051).
- [ ] Lights/chants/marker pulses read beats from `IBeatSource` (latency-compensated),
      never re-derived from `Time.time` (S-053) - that is how a light show drifts.
- [ ] Recovery routine survives headphone plug, call interruption, backgrounding
      (S-052/054). Test it: plug/unplug during a bout.

---

## 4. Readability under mashing

The player is tapping 8-15x/s and looking at the marker. The screen must stay legible.

- [ ] Marker is readable at min-spec frame time - no strobing, no sub-pixel shimmer
      (S-012 covers the math; this is the visual check).
- [ ] Power bar communicates fill rate *and* overcharge distinctly.
- [ ] Red zone is unmistakably the jackpot - color, width and a distinct sound.
- [ ] The overlay / URP Rendering Debugger cannot open from a mash gesture (S-064).

---

## 5. Safe area & portrait

- [ ] `SafeAreaFitter` on every full-screen canvas, re-applied on orientation/resolution
      change (S-063).
- [ ] The bottom tap zone clears the iOS home indicator and Android gesture bar - this is
      exactly where the thumb lives.
- [ ] HUD is **Screen Space - Overlay** so it is structurally incapable of appearing in a
      snapshot (S-037). Verified in the Device Simulator with a notched profile.

---

## 6. First-run & first-slap

- [ ] **First-slap hitch** eliminated: `ShaderVariantCollection.WarmUp()` at load, VFX
      pool pre-warmed, snapshot scratch RT pre-allocated during the match intro (S-044).
- [ ] First launch is playable **offline** (boot + first bout need zero network I/O -
      the local content set, [ADR-0008](Decisions/ADR-0008-addressables-and-ccd.md)).
- [ ] iOS silent-switch tip shown once ("Turn off Silent Mode for the full arena", S-056).

---

## 7. Reaction & pacing

- [ ] Turn reaction beat ~1.0 s normal, ~2.5 s for a finisher ([ARCHITECTURE §4](ARCHITECTURE.md)).
- [ ] Finisher animation is selected **before** it renders (predict/branch/commit) so the
      right cinematic, VO line and camera shot are all chosen in advance.
- [ ] Snapshot gallery pop on the victory screen feels earned, not abrupt.

---

## 8. Haptics (if enabled)

- [ ] A light tap on each accepted tap (rate-limited), a heavier one on impact, the
      heaviest on a finisher. Respect the OS "reduce haptics"/low-power setting.
- [ ] Haptics scheduled on the same predicted contact as audio, not on the frame.

---

## 9. Attention budget - the scene is the picture, the HUD is the caption

Slap Mania is sold by its arena, its fighters and its cinematic camera (M16). A HUD that
covers them hides the show. Every in-bout HUD element is budgeted for **how much screen it
covers, how bright it is, and how long it stays**. The numbers live in
`Data/UI/HudAttention.asset` ([M10 §4](Modules/M10-UIScreenFlow.md)), are tuned from the
Tuning Hub, and are checked by `HudAttentionConfig.Validate` and its EditMode tests.

The perception rules behind the numbers:

- **One focal point per moment.** The eye has one sharp spot. Put the moment's single action
  there (the ring on the face, the gauge under the thumb) and keep everything else in
  low-contrast periphery. Two competing headlines mean neither is read (S-100).
- **Three moving things, at most.** People track about 3-4 moving objects at once. C6 had
  six (rays, spinning reticle, pulsing dot, punching number, pulsing prompt, mini HP);
  now the rays move only with the finger.
- **No looping motion in the periphery.** Peripheral vision is built to catch motion, and a
  loop never lets it rest: the LIVE dot and D2's edge pulse dragged the eye off the fighters
  for whole turns. Peripheral motion is now an **event**, once, when the player must act
  (the BRACE! pulse).
- **Peak, then get out of the way.** Rewards (D1, K.O.) may be big and bright, but briefly:
  the peak is under 350 ms, the screen is clear by ~700 ms, and the next phase's screen waits
  for the headline instead of stacking on it.
- **Quiet rest state.** Between interactions the HUD sits at ~5% coverage and 0.75 alpha, so
  the camera's work is what you see.
- **Learned prompts fade.** After three accepted taps the player knows to TAP FAST!, so the
  prompt drops to half alpha.
- **Let the camera land first.** The K.O. slam waits 350 ms so the finisher shot reads before
  the gold text covers it. Only the HUD is delayed; the camera is never touched for the HUD.

- [ ] Each phase stays inside its row of the M10 §4 budget table when checked in Play
      (captures in `Captures/HudAfter`).
- [ ] No HUD element loops in the periphery during a turn (`liveDotPulse`, `opponentVignettePulse` off).
- [ ] D1 headline and number are off the screen within 1 s, and D2 never appears under them.
- [ ] The finisher camera shot is visible for `koSlamDelayMs` before the K.O. slam.
- [ ] A 30-second watch of a bout with no input: can you describe the arena and both fighters?

---

## The feel bar

Ship only when a stranger, handed the phone with no instructions, lands a red-zone slap
and **grins**. That is the acceptance test this whole file serves. Everything above is
how you get there repeatably instead of by luck.

---

## Changelog

| Date | Change |
|---|---|
| 2026-09-26 | Created. Impact stack, unscaled tweens, audio lead, readability, safe area, warmup, haptics. |
| 2026-10-23 | §9 Attention budget: perception rules, per-phase coverage/alpha/lifetime budget (`HudAttentionConfig`, M10 §4), owner-approved after a Play audit of every phase. |
