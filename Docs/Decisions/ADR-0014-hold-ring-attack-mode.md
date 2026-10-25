# ADR-0014 — The hold-ring attack is an Inspector-selectable attack mode

**Status:** Accepted, amended 2026-10-23 (the default mode is now TapMarker; see *Amendment* below)
**Date:** 2026-10-23 (decisions taken 2026-10-20)
**Deciders:** shobhit

## Amendment 2026-10-23: the default reverts to TapMarker (owner)

The owner reported that the bout was unplayable: taps barely did damage and the AI won fast
([DEBUG S-090](../DEBUG.md)). In HoldRing a tap is a press and an instant release. With the
shipped tuning, a ~90 ms tap closes the ring to ~0.05, a Miss ×0.5 at power ~0.33. A simulation
through `MatchDirector` with the shipped assets (20 bouts vs `Ai_Round1`) gave:

| Player | Mode | Player wins | Player dmg/slap | AI dmg/slap |
|---|---|---|---|---|
| taps | HoldRing (was default) | 0/20 | 4.7 | 27.4 |
| holds, releases on gold ±40 ms | HoldRing | 19/20 | 48.6 | 31.3 |
| mashes 7 taps/s, then taps at a random moment | **TapMarker (new default)** | 9/20 | 25.9 | 33.7 |

**Decision:** `BoutConfig.attackMode` defaults to **TapMarker**: the `Data/Prototype/Bout.asset`
value and the C# field default, so a fresh prototype build does not bring the ring back. Point 1
below is otherwise unchanged. The hold ring stays fully supported and one Tuning Hub switch away,
for the device A/B this ADR was written for.

**Rejected for now:** (a) keep the ring and turn a quick press-release (< ~200 ms) into "tap to
start, tap to stop". (b) Keep the ring and cancel a quick release with a HOLD! coach prompt. Both
change the V2 verb and need their own design pass. Before the ring is the default again, the
simulation above also shows it is **too strong when held well**: 48.6 a slap ends a 100 HP bout
in about two player slaps.

## Context

[ADR-0011](ADR-0011-adopt-v2-design-prototype-first.md) makes the V2 mockups the direction and
names the first mechanic to land: the **hold → release closing-ring attack** (C5 Aim, C6 Charge,
and the E3 coaching line "You released early on 5 of 7 slaps. Wait for the white ring to touch
the gold band"). It requires an Inspector-selectable mode, so the V2 verb can be compared with
the tested tap loop (M03 tap-charge → M04 ping-pong marker) on a real device. It also requires
the ring to reuse `TimingZoneLayout` and M04's latency compensation, and its radius to be a pure
function of elapsed `double` time (R8, [ADR-0004](ADR-0004-ms-based-timing.md)).

Three questions changed the shape of the work, and the owner answered them on 2026-10-20 (R10):
where power comes from, which mode the existing `Bout.asset` starts in, and what happens when the
player holds until the ring has fully closed.

## Decision

1. **`AttackMode { TapMarker, HoldRing }`** (`SlapMania.Core`) on **`BoutConfig.attackMode`**,
   default **HoldRing** (owner). `MatchDirector` reads it at every `TurnStart`, so a Tuning Hub
   switch in Play Mode applies to the next turn. If HoldRing cannot run (no `HoldRingConfig`, or a
   player source with no release events), the director logs one Error and plays TapMarker.
   `GameConfig.Validate` refuses HoldRing without `GameConfig.holdRing`.
2. **Pure Core math, `Core.HoldRing`** (R2):
   - `ClosureAt(held, close) = clamp01(held / close)`: 0 is the ring open at the press, 1 is
     closed on the target. It is linear in time, so a band of width w is a window of w × close.
   - `close = HoldRingConfig.closeMs / speedByRound(round)`, using M04's round curve. At the
     default 1000 ms, every zone has exactly the window the marker gives at 1 traversal/s
     (Perfect = 60 ms), so the device A/B compares verbs, not difficulty.
   - **Closure is a `TimingZoneLayout` position.** The ring lands in M04's bands at M04's zone
     scale. The Perfect band is the gold band; below the centre is *early*, above it *late*.
   - `HeldSecondsAt` is the closed-form inverse, used by the AI.
3. **One gesture decides both halves** (owner):
   `rawPower = Lerp(minPower, maxPower, WindUpAt(closure, windUpPeakClosure, windUpExponent))`.
   - The floor and ceiling are M03's, so both modes share one damage scale.
   - The wind-up is `t^exponent` with `t = closure / peak`; it peaks at the gold centre (0.5) and
     then **holds** at full.
   - Releasing early costs both power and multiplier; releasing late costs only multiplier.
     `Overcharge` is 0.
4. **Timing rules.**
   - The ring starts at the press's own event time. It is clamped to no earlier than
     `now − maxLatencyComp` (and no earlier than the moment it armed) and no later than now.
   - The release is back-dated with M04's rule, now shared as `Core.LatencyCompensation.For`.
     `TimingMeter` uses the same function.
   - **Holding to full close auto-releases** at closure 1: a late Miss with `timedOut = false`
     (owner).
   - **No press by `pressTimeoutMs`** resolves an un-wound Miss with `timedOut = true`: never a
     hang, never a free pass.
5. **Input: `Core.IHoldSource : ITapSource` adds `OnRelease`.**
   - `InputSystemTapSource` raises it from the action's `canceled` callback.
   - `PausableTapSource` gates releases like taps; a release made while paused is dropped.
   - `AiTapSource` and `ManualTapSource` implement it.
   - Tap-only sources are untouched.
6. **FSM: one new state, no new signals.** `MatchState.HoldRing` (appended) replaces
   `Charge → Timing`. It raises the existing signals in gesture order:
   - `TimingStart` when the ring opens (C5);
   - `ChargeStart` at the press (the wind-up: the attacker's charge pose, the C6 HUD);
   - `ChargeEnd` then `TimingEnd` at the release.

   `SlapStats`, `BoutController` and `V2Hud` work unchanged. `V2Hud` gained one line: it shows the
   opponent-turn screen at `TimingStart` too.
7. **The AI plays the same ring.** It aims a zone centre plus `aimSigma`, the same as for the
   marker. Its release is scheduled `+ displayLatencyBias`, like a player who lets go when they
   *see* gold; otherwise it is judged one bias early ([DEBUG S-079](../DEBUG.md); the TapMarker AI
   still has that bias).
8. **HUD (M10).**
   - `HudTiming` (C5): the ring is open until the press, then follows `HoldRingMeter.Closure`. Its
     chrome fades at the press.
   - `HudCharge` (C6): shown at the press. The arc follows the wind-up, the lock-on reticle is
     hidden, and the prompt reads HOLD!, then RELEASE! when the wind-up peaks.
   - The intro, scorebug and opponent-turn screens read the mode.

## Consequences

### Positive

- The two loops are one dropdown apart and share zones, windows, round ramp, latency
  compensation and damage scale, so a device comparison isolates the verb.
- No listener had to learn a new signal. Every result still arrives as a `PowerResult` +
  `TimingResult`, so M05, `SlapStats` and the result screens did not change.
- The AI's aim is a closed form, not M04's 200-sample crossing scan.
- One latency formula for both modes, tested in Core.

### Negative

- `PowerResult` fields mean slightly different things in HoldRing: `Fill` is the wind-up,
  `TapsAccepted` is 1 for a press, and `PeakTps` is 0. These are documented in M03, not renamed.
- Signal order differs by mode: TimingStart comes before ChargeStart in HoldRing. A future
  listener that assumes Charge-then-Timing will misbehave in HoldRing.
- "Wind-up" (the hold's power, V2's C6 word) sits next to `MatchState.Windup` (the slap animation
  between Resolve and Impact). The GLOSSARY flags the pair.

### Risks

- The feel is untested on hardware. Release timing goes through the `canceled` callback, whose
  device timestamps have not been measured. Touch release latency may differ from press latency,
  and the compensation assumes they match.
- With power peaking on gold, the C6 arc reaching MAX is itself a timing cue. That is faithful to
  the mockup (MAX POWER and "Release!" together), but it may make Perfect easier than the bare
  ring. Playtest it.

## Alternatives considered

| Alternative | Why rejected |
|---|---|
| Keep M03's tap-charge for power; the ring replaces only the marker | Owner chose power from hold time (2026-10-20): C6 is one wind-up, not a mash then a hold |
| Flat power, the ring sets only the multiplier | Nothing visibly charges; C6's power arc would be decoration |
| Ring reopens (M04 ping-pong) after a full close | Owner chose auto-release as a late Miss. Reopening turns holding into a free second chance |
| The ring closes from phase open, independent of the press | The press would carry no decision ("always press at once"), and "hold to wind up" would not start anything |
| Add `OnRelease` to `ITapSource` | Every tap-only source would have to fake a release; `IHoldSource` leaves them untouched |
| New `HoldStart` / `Release` signals | Every listener (HUD, stats, presentation, tests) would branch per mode; the existing signals in gesture order carry the same moments |
| A separate close-speed curve for the ring | Two difficulty ramps to keep in sync; `closeMs` plus M04's `speedByRound` gives parity by default and a knob when wanted |
| Accumulated ring radius (`r -= speed * dt`) | ADR-0004: drift, non-deterministic, uncompensatable |
