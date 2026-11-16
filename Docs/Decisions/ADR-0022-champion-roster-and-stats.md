# ADR-0022 — Champion roster: FBX-built champions, runtime spawn, gameplay stats

**Status:** Accepted
**Date:** 2026-10-25
**Deciders:** shobhit

## Context

The bout had one fighter: Alucard, built by an Alucard-only `FighterBuilder` and wired into
`Proto_Bout` twice (player and rival). The V2 Roster tab showed 12 hard-coded mockup cards with
stats (PWR / SPD / CHN / TEC / RAG / STA) that meant nothing. We want to drop in a rigged FBX,
get a playable champion, pick it on the Roster tab, and have the stats matter.

## Decision

1. **Data.** `ChampionDefinition` (id, card text, portrait, card finish, fighter prefab, rig
   profile, `Core.ChampionStats`) and `ChampionRoster` (ordered list), referenced by the optional
   `GameConfig.roster`. A null or empty roster keeps the scene's own fighters, so nothing
   changes until a champion exists.
2. **Creator.** `Tools > Slap Mania > Characters > Champion Creator` (`ChampionCreator` +
   window). `FighterBuilder.Build(id, fbx, ...)` is the old Alucard pipeline made generic: Humanoid
   import, the **shared Mixamo clip set retargeted through Humanoid**, optional per-champion
   overrides `<stem>@<Role>.fbx`, contacts measured on the champion's own rig. It also renders a
   portrait (`PortraitRenderer`) and appends to the roster. Alucard is a champion like any other
   (id `Alucard` keeps the `Fighter_Alucard.*` paths the prototype builder uses).
3. **Spawn.** `BoutController` keeps the scene fighters as *marks*: `ChampionSpawner` instantiates
   each champion's prefab at its mark's pose, layer and parent, moves the contact shadow over, and
   switches the mark off. It runs once in `Start`, before anything binds to the views.
4. **Selection.** The Roster tab (`RosterView`) builds one card per champion; a tap writes
   `ScreenManager.SelectedChampionId`, which `FrontEndBridge` copies to
   `GameRoot.SelectedChampionId` (session memory until M12). The opponent is
   `ChampionRules.PickOpponent`: uniform over the others, from the bout's seeded rng, drawn after
   the AI tier so a pinned `matchSeed` keeps its tier (S-094).
5. **Stats change the bout** - three of them, as pure Core rules (R2) with per-point tuning:
   - **PWR** (attacker) and **CHN** (defender) multiply raw damage: `DamageInput.StatScale =
     ChampionRules.SlapScale(...)`, using `DamageConfig.powerPerPoint` / `chinPerPoint`.
   - **STA** sets max health: `ChampionRules.MaxHp(startingHp, stamina, BoutConfig.staminaHpPerPoint)`.
   - 75 is neutral (scale exactly 1); every scale is held in `[0.25, 4]`.
   - **SPD, TEC, RAG are card-only for now.**

## Consequences

### Positive

- One FBX is enough for a playable champion; moves can be specialised per champion later without
  touching code.
- Neutral stats reproduce the old game exactly, so every existing test and tuning still holds.
- Opponent choice is reproducible from the bout seed.

### Negative

- A seed now consumes one more rng draw (the opponent pick) when a roster has >1 champion, so a
  seed recorded before this change replays different marker/AI rolls after the tier.
- The scene still carries two Alucard instances that are switched off at runtime.

### Risks

- Non-Mixamo rigs (e.g. Tripo auto-rigs) may not auto-map to Humanoid; the creator then stops
  with a pointer to the Avatar configurator.
- The portrait render relies on `PreviewRenderUtility` preserving alpha under URP - unverified;
  a portrait can always be assigned by hand.

## Alternatives considered

| Alternative | Why rejected |
|---|---|
| Require a full Mixamo `Name@Clip.fbx` set per champion | Every new character costs 7 clip exports for moves that retarget fine. Overrides keep the option. |
| Rebuild `Proto_Bout` per selection (scene-wired fighters) | Selection is a runtime choice; baking it into a scene needs one scene per pairing. |
| Stats display-only | Chosen against by the user: stats should matter. Kept the change to three stats with neutral = 1 so the blast radius stays in `DamageResolver` and `MatchDirector.StartBout`. |
| A new "Choose Fighter" screen between PLAY and Search | The V2 design already has the Roster tab with a selected-champion bar; a second picker would duplicate it. |
