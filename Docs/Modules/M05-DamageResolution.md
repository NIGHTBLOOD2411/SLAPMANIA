---
module: M05-DamageResolution
status: InProgress
owner: shobhit
last-verified: 2026-10-25
verified-against: (pre-git)
asmdef: SlapMania.Gameplay
---

# M05 — DamageResolution

> **One-liner:** A pure function that turns power and multiplier into damage, decides whether the slap is lethal, and produces a `DamageResult` that the FSM uses to branch before any frame renders.

## 1. Purpose

This is the join point where M03's `rawPower` and M04's `multiplier` become a number that changes the match state. Its most important property is that it is **pure**: `Resolve()` reads config and inputs, returns a result, and mutates nothing. Health is only changed later by `Commit()`, which M01 calls at the impact frame. This separation — predict, then branch, then commit — is what makes finishers work: the FSM knows a slap is lethal *before* the impact animation plays, so it can route into the finisher cutscene rather than discovering the KO mid-animation and doing an awkward retroactive correction.

## 2. Responsibilities and non-goals

**Owns**
- The damage formula.
- `Resolve()` — pure, returns `DamageResult`.
- `Commit()` — applies `DamageResult` to `HealthTracker` (M06).
- Crit detection (landing in the Perfect zone).
- Escalation via `slapIndex` (later slaps hit harder).
- The `minDamageFloor` that guarantees match termination.

**Explicitly does NOT do**
- Compute `rawPower` → `M03`.
- Compute `multiplier` → `M04`.
- Own or store health → `M06`.
- Decide the finisher type or play the animation → `M01` branches, `M08`/`M18` animate.
- Apply damage reduction from armour or perks → not in v1; the `reduction` parameter is a hook.

## 3. Public API surface

| Member | Signature | Notes |
|---|---|---|
| `DamageResolver.Resolve` | `static DamageResult Resolve(in DamageInput input, DamageConfig cfg)` | **Pure, static, no side effects** |
| `DamageResolver.Commit` | `static void Commit(in DamageResult result, HealthTracker target)` | Mutates `target.CurrentHp`. Called once per slap by M01 at the impact frame. |

### `DamageInput`

| Field | Type | Source |
|---|---|---|
| `rawPower` | `float` | M03 `PowerResult.rawPower` |
| `multiplier` | `float` | M04 `TimingResult.multiplier` |
| `slapIndex` | `int` | M01 — zero-based, increments each slap |
| `baseSlapDamage` | `float` | From `DamageConfig` |
| `defenderCurrentHp` | `int` | M06 — read, not mutated |
| `defenderMaxHp` | `int` | M06 |
| `reduction` | `float` | The player's brace on the rival's turn (0, 0.25 or 0.50 shipped; [ADR-0019](../Decisions/ADR-0019-brace-tap-defence.md)). 0 everywhere else. Perks and armour are still not in v1 |

### `DamageResult`

`readonly struct`:

| Field | Type | Notes |
|---|---|---|
| `rawDamage` | `float` | Before floor and clamping |
| `finalDamage` | `int` | After floor, rounded, clamped to remaining HP |
| `isCrit` | `bool` | multiplier ≥ 2.5 (Great or Perfect zone) |
| `isLethal` | `bool` | `finalDamage >= defenderCurrentHp` |
| `isFinisher` | `bool` | `isLethal && finisherEnabled` |
| `overkill` | `int` | `finalDamage - defenderCurrentHp` if lethal, else 0 |
| `hpAfter` | `int` | `max(0, defenderCurrentHp - finalDamage)` |

**Consumed by:** M01 (branching), M06 (commit), M08 (impact intensity), M09 (snapshot tier), M10 (damage number popup), M11 (impact audio tier), M13 (recorder).
**Stability:** formula stable; `reduction` and crit threshold may shift.

## 4. Configuration

Asset: `Assets/_Project/Data/Balance/DamageConfig.asset`

| Field | Type | Default | Sane range | Notes |
|---|---|---|---|---|
| `baseSlapDamage` | float | **22** | 10 – 40 | The constant in the formula |
| `escalationBase` | float | **1.06** | 1.00 – 1.15 | `escalationBase ^ slapIndex` — later slaps hit harder to prevent stalling |
| `minDamageFloor` | int | **3** | 1 – 5 | Absolute minimum damage per slap, after all modifiers. **Guarantees match termination.** |
| `critThreshold` | float | **2.50** | 2.00 – 3.00 | Multiplier at or above which `isCrit = true` |
| `finisherEnabled` | bool | **true** | — | |
| `finisherHpThreshold` | float | **0.0** | 0.0 – 0.15 | Fraction of maxHp below which a lethal hit is a finisher. At 0, every lethal hit is a finisher. At 0.15, only kills from ≤15% HP trigger one. |

**Invariants**
- `baseSlapDamage > 0`.
- `escalationBase >= 1.0`.
- `minDamageFloor >= 1` — at 0, a match can loop forever if both players miss every timing.
- `critThreshold` is within the range of configured multipliers.
- Max single-slap damage (`baseSlapDamage * 1.12 * 3.0 * escalation_at_max_round`) is **below** `startingHp`. A one-hit KO from full health is a balance failure, not a feature.

## 5. The math

```
raw       = baseSlapDamage * rawPower * multiplier * (escalationBase ^ slapIndex) * (1 - reduction)
floored   = max(minDamageFloor, round(raw))
final     = min(floored, defenderCurrentHp)
```

### Worked outcomes at `baseSlapDamage = 22`, `startingHp = 100`

| Scenario | rawPower | multiplier | slapIndex | raw | final |
|---|---|---|---|---|---|
| **Floor** — no taps, timeout | 0.25 | 0.50 | 0 | 2.75 → floor **3** | 3 |
| **Low** — weak mash, Weak zone | 0.50 | 0.85 | 0 | 9.35 → **9** | 9 |
| **Median** — decent mash, Good zone | 0.75 | 1.40 | 2 | 22 × 0.75 × 1.40 × 1.124 = **26** | 26 |
| **High** — strong mash, Great zone | 0.90 | 2.00 | 3 | 22 × 0.90 × 2.00 × 1.191 = **47** | 47 |
| **Ceiling** — perfect mash + overcharge, Perfect zone | 1.12 | 3.00 | 5 | 22 × 1.12 × 3.00 × 1.338 = **99** | clamped to remaining HP |
| **Escalated floor** — no taps, timeout, slap 8 | 0.25 | 0.50 | 8 | 2.75 × 1.594 = 4.38 → **4** | 4 |

The ceiling at slap 5 is 99 — just under 100 HP, so a one-shot from full health requires overcharge *and* Perfect *and* five prior slaps. In practice, matches end in 4–7 slaps.

**Why escalation matters:** without it, two bad players trading floor-3 slaps play a 34-turn match (100/3 each). With `1.06^slapIndex`, slap 8 deals floor 4 and slap 15 deals floor 7. The practical worst case drops to ~20 turns. The `maxRounds` safety valve in M01 catches anything beyond that.

### Champion stat scale ([ADR-0022](../Decisions/ADR-0022-champion-roster-and-stats.md))

`raw *= clamp(StatScale, 0.25, 4)` (NaN -> 1). `StatScale = ChampionRules.SlapScale(attacker, defender, powerPerPoint, chinPerPoint)` = `StatScale(attacker.power, +k) * StatScale(defender.chin, -k)`; 75 is neutral. Applied before the floor, so the 3-damage floor still holds.

## 6. State machine

None — this module is stateless. `Resolve` is a pure function; `Commit` is a one-shot mutator.

## 7. Dependencies

- **Hard:** `SlapMania.Core`, `SlapMania.Data` (`DamageConfig`), M06 (`HealthTracker`).
- **Soft:** none.
- **Unity APIs:** `Mathf.RoundToInt`, `Mathf.Pow`, `Mathf.Max`, `Mathf.Min`.
- **Must initialize after:** nothing — it is a static utility.

## 8. Failure modes

| Failure | Detection | Behaviour | Player-visible? |
|---|---|---|---|
| `rawPower` outside [0.25, 1.12] | Assert in `Resolve` | Clamp, log Warn | No |
| `multiplier` outside [0.50, 3.00] | Assert in `Resolve` | Clamp, log Warn | No |
| `escalationBase` = 0 | Config invariant | Refused at load time | No |
| `finalDamage` > `defenderCurrentHp` | Clamped in `Resolve` | `overkill` field records the excess | No |
| `Commit` called twice for the same `DamageResult` | Guard — `result.committed` flag | Assert, refuse second commit | No |
| Negative `slapIndex` | Guard | Clamp to 0, log Warn | No |

## 9. Debug hooks

- **Log channel:** `LogChannel.Damage`
- **Overlay (Slap tab):** last `DamageResult` broken down: rawPower, multiplier, escalation factor, raw damage, floor applied?, final damage, isCrit, isLethal, isFinisher, overkill.
- **Cheats:** force damage (slider), force crit, force lethal, force finisher, one-hit-kill toggle.
- **Recorder events:** `DamageResolved` with the full `DamageResult`.
- **Editor tools:** `DamagePreviewWindow` — enter rawPower, multiplier, slapIndex, and see the full `DamageResult` without entering Play Mode. Useful for balance iteration.

## 10. Tuning notes

**`minDamageFloor` and `minPower` together guarantee termination.** If either is 0, a sufficiently bad pair of players (or a bugged AI) can play an infinite match. `minDamageFloor = 3` means the absolute worst case is ceil(100/3) = 34 turns per player, 68 total. That is boring but finite. Do not set `minDamageFloor` to 0.

**Escalation is invisible and that is fine.** The player never sees "1.06^8 = 1.594×". They see "slaps hit harder as the fight goes on," which is a genre expectation. Making it visible (a damage multiplier badge) turns it into a mechanic the player tries to exploit by stalling, which is the opposite of its purpose.

**The crit threshold is about presentation, not damage.** Whether a hit is "crit" determines the screen flash, the crowd roar, the camera punch, and the snapshot tier — not a separate damage bonus. The damage is already high because the multiplier is high. The `isCrit` flag is a read-only label for the spectacle layer.

**Superseded 2026-10-23 by the brace ([ADR-0019](../Decisions/ADR-0019-brace-tap-defence.md)): `reduction` is the brace, and only the brace.** `BraceJudge` (Gameplay, pure) grades the player's first tap in the rival's wind-up against a target `braceLeadMs` (100) before the impact: within 70 ms is Perfect (−50%), within 160 ms is Good (−25%), anything else is a Flinch (0). `MatchDirector` re-resolves the slap with that reduction before Impact, so lethality is recomputed and a brace can save the player. `DamageConfig` gains `braceEnabled`, `braceLeadMs`, `bracePerfectMs`, `braceGoodMs`, `bracePerfectReduction`, `braceGoodReduction`. The original v1 note follows. **`reduction` exists as a parameter but must stay 0.0 in v1.** The moment damage reduction enters, the balance space explodes: what if both fighters have reduction? What if reduction stacks with escalation? Those questions belong in v2 with a proper item/perk system. The parameter is in the formula so that adding it later does not change the function signature.

## 11. Open questions

- [ ] Should `escalationBase` be per-round (resetting each round in a multi-round match) or per-slap (continuous across the bout)? Currently per-slap, which makes long bouts more volatile.
- [ ] Is `overkill` purely cosmetic, or should it feed into a score/rating system?
- [ ] Should the crit threshold be a zone check (`zone == Perfect || zone == Great`) rather than a multiplier threshold? Zone check is more honest but couples M05 to M04's zone enum.

## 12. Changelog

| Date | Commit | Change |
|---|---|---|
| 2026-10-25 | (pre-git) | **Champion stat scale ([ADR-0022](../Decisions/ADR-0022-champion-roster-and-stats.md)).** `DamageInput.StatScale` (optional, default 1) multiplies raw damage; clamped to `[ChampionRules.MinScale, MaxScale]`, NaN = 1. New Core `ChampionStats` / `ChampionRules` (`StatScale`, `SlapScale`, `MaxHp`, `PickOpponent`) and `DamageConfig.powerPerPoint` / `chinPerPoint` (0.004). Tests: `ChampionRulesTests`. |
| 2026-10-23 | (pre-git) | **Brace defence ([ADR-0019](../Decisions/ADR-0019-brace-tap-defence.md)).** New Core `BraceResult`/`BraceGrade`, pure Gameplay `BraceJudge`, and `DamageConfig` brace fields (ms, R8). `DamageInput.Reduction` now carries the brace on the rival's turn. §3 and §10 updated (the "reduction stays 0" rule is superseded). Tests: `BraceTests` (17: grades, ±1 µs window edges on both sides, NaN/±inf taps, NaN/negative bias, clamped reductions, huge offsets; FSM: a Perfect brace halves and commits, no tap, mashing flinches, disabled, a brace saves from a lethal slap, player turns never open the window). EditMode 284/284 |
| 2026-09-19 | (pre-git) | Created (Draft) with the formula and worked outcomes |
| 2026-09-19 | (pre-git) | **P3 code landed.** Static `DamageResolver.Resolve` (pure) and `Commit` in `SlapMania.Gameplay`; `DamageConfig` SO in `SlapMania.Data`. Adds a `globalDamageScale` field per TUNING-BIBLE §3 (default 1.0). **Deviation:** `critThreshold` in the config defaults to **2.00** (matching GLOSSARY + TUNING-BIBLE §3), not the **2.50** printed in §4 of this doc — §4 needs a follow-up edit. EditMode tests cover: floor applied when raw < floor, escalation makes later slaps hit harder, crit triggers at exactly the threshold, exact-zero HP is lethal without overkill, overkill is recorded when clamped. |
