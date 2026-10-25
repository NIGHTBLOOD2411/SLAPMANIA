---
module: M03-PowerCharge
status: InProgress
owner: shobhit
last-verified: 2026-10-23
verified-against: (pre-git)
asmdef: SlapMania.Gameplay
---

# M03 — PowerCharge

> **One-liner:** Turns a burst of rapid taps into a single normalized `power` value between 0.25 and 1.12.

## 1. Purpose

The charge phase is the first half of every slap. The player taps as fast as they can for about 2.5 seconds; this module converts that tap train into one number the damage formula can use. It has to feel kinetic on every single tap, punish stopping, reward a finishing sprint, and refuse to make an autoclicker meaningfully better than a fast human.

## 2. Responsibilities and non-goals

**Owns**
- The fill accumulator and its decay.
- The tap-rate diminishing-returns curve.
- The late-phase crescendo shape.
- Overcharge accrual once fill is capped.
- The final `fill → rawPower` mapping.

**Explicitly does NOT do**
- Detect or de-duplicate taps → owned by `M02-Input`, which delivers `TapEvent`s.
- Decide when the phase starts or ends → owned by `M01-MatchFlow`.
- Know whether a human or the AI is tapping → both arrive through `ITapSource`.
- Draw anything → `PowerBarView` in `M10` polls `Fill` in `LateUpdate`.
- Play the tap tick → `M11-Audio` subscribes to `PowerTapRegistered`.
- Run in the **HoldRing** attack mode ([ADR-0014](../Decisions/ADR-0014-hold-ring-attack-mode.md)). There, power is the hold's **wind-up** (M04 §5b): `rawPower = Lerp(minPower, maxPower, windUp)`, using this module's `minPower`/`maxPower` so both modes share one damage scale. `PowerMeter` is not begun. The `PowerResult` it hands M05 reuses the fields: `Fill` = the wind-up (0..1), `Overcharge` = 0, `TapsAccepted` = 1 for a press (0 if the player never pressed), `PeakTps` = 0.

## 3. Public API surface

| Member | Signature | Notes |
|---|---|---|
| `PowerMeter.Begin` | `void Begin(PowerPhaseConfig cfg, ITapSource source)` | Resets all state |
| `PowerMeter.Tick` | `void Tick(double dt)` | Applies decay and advances the phase clock |
| `PowerMeter.RegisterTap` | `bool RegisterTap(in TapEvent e)` | Returns false if rejected; never allocates |
| `PowerMeter.End` | `PowerResult End()` | Maps fill to `rawPower` |
| `PowerMeter.LastTapTime` | `double { get; }` | **2026-10-23.** Event time of the last accepted tap (−inf before the first). M04's spam gap starts from it (S-093) |
| `PowerMeter.Fill` | `float { get; }` | **Polled** by the view, never evented |
| `PowerMeter.Overcharge` | `float { get; }` | Polled |
| `PowerMeter.IsComplete` | `bool { get; }` | |

`PowerResult` is a `readonly struct` carrying `fill`, `overcharge`, `rawPower`, `tapsAccepted`, `tapsRejected`, `peakTps`.

**Consumed by:** M01 (drives it), M05 (uses `rawPower`), M10 (polls), M11 (tap feedback), M13 (overlay).
**Stability:** churning until the vertical slice.

## 4. Configuration

Asset: `Assets/_Project/Data/Balance/PowerPhaseConfig.asset`

| Field | Type | Default | Sane range | Meaning / what breaks outside it |
|---|---|---|---|---|
| `phaseDuration` | float s | **2.50** | 1.5 – 4.0 | Below 1.5 s there is no room for the crescendo arc; above 4 s the thumb tires |
| `impulsePerTap` | float | **0.072** | 0.04 – 0.12 | Fill added per accepted tap before modifiers |
| `decayPerSecond` | float | **0.12** | 0.05 – 0.30 | The "keep going or lose it" pressure. At 0 the phase is a pure count and the arc disappears |
| `maxFill` | float | 1.00 | fixed | |
| `minTapInterval` | float s | **0.045** | 0.035 – 0.060 | ~22 tps ceiling; also guards against touch-driver double reports |
| `diminishWindow` | float s | **0.30** | 0.20 – 0.50 | Sliding window for the rate count |
| `diminishSoftTaps` | int | **3** | 2 – 5 | Where diminishing returns begin (3 in 0.30 s = 10 tps) |
| `diminishHardTaps` | int | **6** | 5 – 9 | Where they bottom out (20 tps) |
| `diminishFloor` | float | **0.50** | 0.35 – 0.75 | At 0.35 the curve inverts past 15 tps and hard mashing becomes actively worse |
| `phaseShape` | curve | **1.00 @0 → 1.00 @0.6 → 1.60 @1.0** | peak 1.2 – 2.0 | Late taps worth more; creates the finishing sprint |
| `feedbackCurve` | curve | **x^1.15** | x^1.0 – x^1.4 | Maps fill to power |
| `minPower` | float | **0.25** | 0.15 – 0.35 | Zero taps still yields a weak slap. **Never 0**. Also the hold ring's un-wound floor (M04 §5b) |
| `maxPower` | float | 1.00 | fixed | Also the hold ring's full wind-up (M04 §5b) |
| `overchargeRatePerTap` | float | **0.09** | 0.05 – 0.15 | Accrual once fill is capped |
| `overchargeGain` | float | **0.12** | 0.0 – 0.25 | Max additive power from overcharge |
| `overchargeSpeedPenalty` | float | **0.0** | 0.0 – 0.25 | If > 0, high overcharge speeds up the M04 marker |

**Invariants**
- `minPower > 0` — a zero-power floor risks a non-terminating match.
- `diminishSoftTaps < diminishHardTaps`.
- `minTapInterval < 1 / diminishHardTaps × diminishWindow` or the hard threshold is unreachable.
- `phaseShape` is non-negative across `[0,1]`.

## 5. The math

Per frame:
```
fill    = max(0, fill - decayPerSecond * dt)
elapsed += dt
tNorm    = elapsed / phaseDuration
```

Per tap, from the input callback with its own timestamp:
```
if (t - lastTapTime < minTapInterval) { rejected++; return false; }

n    = taps registered in (t - diminishWindow, t]
d    = Lerp(1.0, diminishFloor, InverseLerp(diminishSoftTaps, diminishHardTaps, n))
s    = phaseShape.Evaluate(tNorm)
gain = impulsePerTap * d * s

if (fill < maxFill) fill       = min(maxFill, fill + gain)
else                overcharge = min(1, overcharge + overchargeRatePerTap * d)

lastTapTime = t
```

At phase end:
```
fillNorm = fill / maxFill
rawPower = Lerp(minPower, maxPower, feedbackCurve.Evaluate(fillNorm))
         + overcharge * overchargeGain      // range [0.25, 1.12]
```

The behaviour table for sustained tap rates lives in [TUNING-BIBLE.md §1](../TUNING-BIBLE.md#1-charge-phase--powerphaseconfig).

## 6. State machine

| State | Entry | Exit | Timeout |
|---|---|---|---|
| `Idle` | construction, or after `End()` | `Begin()` | — |
| `Charging` | `Begin()` | `elapsed >= phaseDuration` | the duration itself |

Phase end is **timer-only**. There is deliberately no commit tap: a commit tap is ambiguous with the last power tap and would misfire constantly.

## 7. Dependencies

- **Hard:** `SlapMania.Core` (`ITapSource`, `TapEvent`), `SlapMania.Data` (`PowerPhaseConfig`).
- **Soft:** none. This module is a pure state machine over floats.
- **Unity APIs:** `AnimationCurve` only. No `Time.*`, no MonoBehaviour.
- **Must initialize after:** M00 (config loading).

## 8. Failure modes

| Failure | Detection | Behaviour | Player-visible? |
|---|---|---|---|
| Config null | `SlapAssert` in `Begin` | Use a hardcoded default config, log Error | no |
| `dt` is NaN or negative | Guard in `Tick` | Clamp to 0, log Warn once | no |
| No taps at all | Normal path | `rawPower = minPower` (0.25). Never zero, never a softlock | Weak slap |
| A tap arrives after `End()` | Guard | Ignored, counted as rejected | no |
| `phaseShape` curve empty | `OnValidate` + invariant test | Treated as constant 1.0, loud editor error | no |

## 9. Debug hooks

- **Log channel:** `LogChannel.Power`
- **Overlay (Slap tab):** raw fill accumulator, normalized fill, decay applied this frame, instantaneous and smoothed taps/sec, current diminishing factor `d`, current `phaseShape` value `s`, overcharge, and the projected `rawPower` if the phase ended now.
- **Overlay (Input tab):** accepted vs rejected tap counts with rejection reasons.
- **Cheats:** force power (slider, locks the charge result), auto-max, auto-zero.
- **Recorder events:** `ChargeBegin`, `Tap`, `TapRejected`, `ChargeEnd`.
- **Editor tools:** `PowerCurveDrawer` — a property drawer showing the tap-rate-to-power response curve with the *current live* tap rate marked during Play Mode.

## 10. Tuning notes

**Why per-tap impulse rather than a rolling-average TPS model.** An averaged tap rate sampled against a curve gives a bar that moves smoothly, and a smooth bar makes every individual tap feel like it did nothing. Per-tap impulse gives a discrete bar jolt, a particle and a click on every finger press, which is where the juice lives. The decay term then supplies the "keep going or lose it" pressure that an averaged model would get for free. This was the first real design decision in the module and it should not be revisited without a playtest that shows the bar feels mushy.

**The anti-exploit shape is deliberately a plateau, not a penalty.** Effective gain is `r · d(r) · impulse`, which rises to about 0.72/s at 10 tps and then flattens (0.729 at 15, 0.792 at 22). A 22 tps autoclicker beats a 10 tps human by roughly 11 %. If you make hard mashing actively *worse* — which dropping `diminishFloor` to 0.35 does — players experience it as the game punishing enthusiasm and report it as a bug. Only invert the curve with telemetry that justifies it.

**Early taps and late taps do different jobs.** Early taps are mostly buying a buffer against decay; late taps are worth 1.6× and convert straight into score. Combined with overcharge, the phase has a real arc — a nervous fast start, a plateau, then a sprint. A player who saves themselves for the last second loses (decay eats them) and a player who stops early loses (0.12/s drain). The optimum is a sustained 8–10 tps with a finishing burst, which is exactly the pantomime of winding up a slap. That coincidence is the whole design.

**Overcharge exists to delete dead time.** At 10 tps the bar caps around 1.67 s, leaving 0.83 s where tapping does nothing. Overcharge fills that with a modest +12 % ceiling. It is currently pure upside, which makes the phase decisionless — "mash as hard as you can, always". `overchargeSpeedPenalty` is the hook that turns it into a genuine gamble by speeding up the timing marker. Ship it at 0, but **playtest it on at the vertical slice**: it is the difference between two minigames stapled together and one mechanic with internal tension.

**Do not change `minPower` to 0** without also revisiting `minDamageFloor` in M05. Between them they are what guarantees a match terminates.

## 11. Open questions

- [ ] Does the crescendo read at all, or does it just feel like the bar speeds up randomly? Needs a playtest with `phaseShape` flat as an A/B.
- [ ] Should two-thumb tapping be allowed (currently yes, handled by the plateau) or restricted to one finger? Two-thumb is a classic hyper-casual affordance and feels good; the risk is it becomes mandatory to be competitive.
- [ ] Is 2.5 s too long once the arena show is running and there is more to look at?

## 12. Changelog

| Date | Commit | Change |
|---|---|---|
| 2026-10-23 | (pre-git) | `PowerMeter.LastTapTime` exposed, read-only, for M04's spam gap (DEBUG S-093, [ADR-0017](../Decisions/ADR-0017-aim-cue-and-spam-gap.md)). No behaviour change. §3 updated |
| 2026-09-19 | (pre-git) | Created (Draft) with the full math and default tuning |
| 2026-09-19 | (pre-git) | **P3 code landed.** `PowerMeter` (pure C#, no MonoBehaviour, no `Time.*` read) and `PowerPhaseConfig` SO. Implements the §5 math verbatim including per-tap impulse, sliding-window diminishing returns via a fixed-size ring buffer (no allocations per tap, satisfies R5), late-phase crescendo curve, overcharge accrual and `minPower` floor. `RegisterTap` returns bool for the accept/reject path; `Tick(dt)` guards `NaN`/negative. EditMode tests cover zero-taps floor, sustained rate saturation, min-interval rejection, `NaN`/negative dt, and a huge-dt decay clamp. |
| 2026-10-23 | (pre-git) | **HoldRing attack mode ([ADR-0014](../Decisions/ADR-0014-hold-ring-attack-mode.md)).** No code change in this module's meter. With `BoutConfig.attackMode = HoldRing` (now the asset default), `PowerMeter` does not run; power comes from M04 §5b's wind-up within this config's `minPower`..`maxPower`. §2 and the §4 power rows updated to say so. |
