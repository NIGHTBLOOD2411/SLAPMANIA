# ADR-0019 — Brace: a one-tap defence on the rival's turn, through DamageInput.reduction

**Status:** Accepted
**Date:** 2026-10-23
**Deciders:** shobhit

## Context

On the rival's turn the player had nothing to do: half of every bout was spent watching. The owner asked for "the Brace defense: a timing tap during the AI's slap that reduces the damage you take, using DamageInput's reduction". `DamageInput.Reduction` has been in the formula since P3. M05 §10 said it must stay 0.0 in v1 until an item/perk system existed.

The V2 mockup D2 "Opponent Turn · Brace" shows a different gesture: hold, then release on a gold ring, with a second verb, Lean ("dodges but risks ×2"), and copy "blocks up to 50%".

## Decision

- **One tap during the rival's Windup.** `MatchDirector` opens a brace window when a rival `Windup` starts (if `DamageConfig.braceEnabled`). It enables the player's source with a cached handler (R5), keeps the **first** tap, and judges it as Windup ends, before entering `Impact`.
- **The target sits `braceLeadMs` (100) before the impact.** The player times the tap to what they see, one display bias (30 ms) late. A target at the impact itself would put every well-timed tap after the impact was committed. Leading it means the whole Perfect window lands before the commit ("raise your guard as the hand comes in"). Invariant: `braceLeadMs >= displayLatencyBias + bracePerfectMs`.
- **Grades** (pure `BraceJudge`, R2), by the distance from the target: within `bracePerfectMs` (70) Perfect, −`bracePerfectReduction` (0.50, the mockup's "up to 50%"); within `braceGoodMs` (160) Good, −0.25; further away a Flinch, 0. No tap is None, 0. Mashing flinches, because only the first tap counts and it is early.
- **Applied by re-resolving.** If the reduction is > 0, the slap is resolved again with `DamageInput.Reduction`, so `ImpactFrame` and the commit carry the braced damage. Lethality is recomputed, so **a brace can save the player from a finisher**. `MatchSignals.BraceResolved` fires before `ImpactFrame`. `DamageResolved` keeps the unbraced prediction; nothing listens to it.
- HUD D2 shows a BRACE! cue during the wind-up and the result after it, and stays up 0.8 s past the impact.

**Rejected: the mockup's hold-and-release ring block.** The owner asked for a tap. The hold ring also proved that a hold gesture punishes instinctive taps (S-090).

**Rejected: Lean (dodge, ×2 on failure).** It is a second verb with a risk branch. It stays in the mockup for later.

**Rejected: the AI braces too.** It would be symmetric (M07: one code path), but it would also cut the player's damage, which works against "the AI is too strong". It can be a later ladder lever (AI tiers with brace skill).

## Consequences

- M05 §10's "reduction stays 0.0 in v1" is superseded: the brace is the only source of reduction. Perks and armour remain out.
- Expected rival damage drops by up to half for a skilled player. Re-check the AI ladder's numbers (M07 §4b) in play.
- Late taps, made after the impact is committed, count as None ("NO BRACE"), never "TOO LATE". The HUD's TOO LATE copy shows only for a tap seen more than 160 ms after the target that still lands before the impact, which the shipped numbers make rare.
- `windupMs` (500) now also sets the brace window. M01 §10 already calls it the most coupled number in the game.
