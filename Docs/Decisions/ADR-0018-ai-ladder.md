# ADR-0018 — AI ladder: difficulty tiers chosen per bout from the player's level

**Status:** Accepted
**Date:** 2026-10-23
**Deciders:** shobhit

## Context

Owner: "AI is too powerful, we need random levels of AI, not a powerful one; let's make them progressive." The code had one `AiProfile` (`Ai_Round1`), bound once per bout (`ProfileForRound(1)`). The bout seed was fixed (`matchSeed` 918273645), so every rematch replayed the same opponent (DEBUG S-094, S-095). `GameConfig.aiProfiles` was documented as "indexed by round", and nothing read it that way.

## Decision

- `GameConfig.aiProfiles` becomes **AI tiers**, easiest first. Five ship: ROOKIE, BRAWLER, CONTENDER, PRO, CHAMPION (`Data/Prototype/Ai_Tier*.asset`, values in `PrototypeSceneBuilder.LadderTiers`). `AiProfile.displayName` names the opponent on the HUD.
- The player has a **ladder level** (a tier index) in `GameRoot.AiLadderLevel`, kept for the session only until M12 can save it. `AiLadder.NextLevel`: a win moves it up one, a loss (including a forfeit) down one, clamped.
- Each bout, `AiLadder.RollTier(level, tiers, rng)` picks the opponent's tier near the level. Contract: in range, and at most one tier from the level (tested).
- `BoutConfig.matchSeed` 0 means a fresh seed per bout, logged on the Match channel. A non-zero value pins one for reproducing bugs.

**Rejected: ramp the AI within a bout (per round).** The owner chose the ladder. A per-round ramp also punishes the player for long bouts, and the bout is short anyway (100 HP, ~4 slaps each).

**Rejected: a uniformly random tier per bout.** A CHAMPION in the first bout is the "too powerful" problem again.

## Consequences

- Standalone play (Proto_Bout with Play From Boot off, so no GameRoot) always starts at level 0 and never moves.
- A replay needs the seed **and** the ladder level. Both are logged at bout start.
- Editor sim, 40 bouts per tier on the shipped assets (2026-10-23), average AI damage per slap over the first 8 slaps: ROOKIE 10.7, BRAWLER 23.1, CONTENDER 28.2, PRO 38.1, CHAMPION 44.9. The old `Ai_Round1` sat at about CONTENDER. `Ai_Round1.asset` is no longer referenced.
