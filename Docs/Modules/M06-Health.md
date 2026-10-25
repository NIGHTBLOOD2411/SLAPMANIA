---
module: M06-Health
status: InProgress
owner: shobhit
last-verified: 2026-09-19
verified-against: (pre-git)
asmdef: SlapMania.Gameplay
---

# M06 — Health

> **One-liner:** Tracks each fighter's current HP, exposes it for the HUD, and is the only place where health is mutated.

## 1. Purpose

A single authoritative integer per fighter. Every "how much HP does X have" question is answered here, and the only code path that changes it is `DamageResolver.Commit()` calling `ApplyDamage()`. If health changes anywhere else, it is a bug.

## 2. Responsibilities and non-goals

**Owns**
- `currentHp` per fighter, clamped to `[0, maxHp]`.
- The `HealthChanged` event.
- The `IsDead` check.
- Healing (not in v1, but the interface supports it so the signature does not change).

**Explicitly does NOT do**
- Compute damage → `M05`.
- Display the health bar → `M10`.
- Decide what happens at 0 HP → `M01` reads `IsDead` and transitions to KO.

## 3. Public API surface

| Member | Signature | Notes |
|---|---|---|
| `HealthTracker.Init` | `void Init(int maxHp)` | Resets to full |
| `HealthTracker.CurrentHp` | `int { get; }` | Polled by HUD |
| `HealthTracker.MaxHp` | `int { get; }` | |
| `HealthTracker.NormalizedHp` | `float { get; }` | `currentHp / (float)maxHp`, for the bar fill |
| `HealthTracker.IsDead` | `bool { get; }` | `currentHp <= 0` |
| `HealthTracker.ApplyDamage` | `void ApplyDamage(int amount)` | Clamps to 0, fires `HealthChanged`. Called only by `DamageResolver.Commit`. |
| `HealthTracker.OnHealthChanged` | `event Action<HealthChangedEvent>` | |

`HealthChangedEvent` is a `readonly struct`: `int previousHp`, `int currentHp`, `int maxHp`, `int delta`, `bool isDead`.

**Consumed by:** M01 (death check), M05 (commit target), M10 (bar fill), M11 (crowd intensity), M13 (overlay).
**Stability:** stable — this is intentionally trivial.

## 4. Configuration

No dedicated asset. `maxHp` comes from `BoutConfig.startingHp` (M01), passed to `Init()`.

| Field | Type | Default | Sane range | Notes |
|---|---|---|---|---|
| `startingHp` | int | **100** | 60 – 200 | In `BoutConfig`, not in a Health-specific SO |

**Invariants**
- `maxHp > 0`.
- `currentHp` is never negative (clamped in `ApplyDamage`).

## 5. Events

**Emits**

| Event | Payload | When |
|---|---|---|
| `OnHealthChanged` | `HealthChangedEvent` | After every `ApplyDamage` |

**Consumes**

None — Health is a passive store that others write to and read from.

## 6. State machine

None. `HealthTracker` is a value container with one mutation method. It does not tick.

## 7. Dependencies

- **Hard:** `SlapMania.Core`.
- **Soft:** none.
- **Unity APIs:** none.
- **Must initialize after:** M00 (for config), before M01 `StartBout`.

## 8. Failure modes

| Failure | Detection | Behaviour | Player-visible? |
|---|---|---|---|
| `ApplyDamage` with negative amount | Assert | Refuse, log Error | No |
| `ApplyDamage` called before `Init` | Assert — `maxHp == 0` guard | Log Error, ignore | No |
| Double init without a bout reset | Allowed — `Init` resets cleanly | No | No |
| `currentHp` somehow > `maxHp` | Assert in `ApplyDamage` exit | Clamp | No |

## 9. Debug hooks

- **Log channel:** `LogChannel.Health`
- **Overlay (Match tab):** both fighters' HP as number and bar, delta from last hit, `IsDead` flag.
- **Cheats:** set HP (slider per fighter), heal to full, set to 1 HP (sets up a finisher test).
- **Recorder events:** `HealthChanged` with before/after.

## 10. Tuning notes

**This module is deliberately dumb.** All the intelligence is in M05 (how much damage) and M01 (what happens at 0). Keeping health as a pure container means it is impossible to have a "health desync" bug — there is one integer, one mutation path, and one event. If you are tempted to add shields, armour, or damage-over-time, add them as modifiers *inside M05's formula*, not as parallel mutation paths on `HealthTracker`. The moment two systems can change HP independently, you have a concurrency bug waiting to happen.

**`startingHp = 100` is the design anchor.** Every number in the TUNING-BIBLE is calibrated to 100 HP. Changing it is a full rebalance: `baseSlapDamage`, `minDamageFloor`, `escalationBase`, and the AI profiles all assume 100. If you want longer matches, increase `startingHp` *and* decrease `baseSlapDamage` proportionally, or just increase it and accept that matches will take more turns.

## 11. Open questions

- [ ] Should `HealthTracker` support asymmetric starting HP (e.g. boss fights with 200 HP vs player's 100)? The API supports it (`Init(int maxHp)` per fighter), but the UI and balance are not designed for it.

## 12. Changelog

| Date | Commit | Change |
|---|---|---|
| 2026-09-19 | (pre-git) | Created (Draft) |
| 2026-09-19 | (pre-git) | **P3 code landed.** `HealthTracker` per-fighter (`Init`, `ApplyDamage`, `OnHealthChanged`), asserts on negative amounts and uninitialised max. `HealthChangedEvent` payload lives in `SlapMania.Core`. EditMode tests cover init, clamp-to-zero, event delta. |
