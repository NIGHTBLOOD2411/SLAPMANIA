---
module: M04-TimingMultiplier
status: InProgress
owner: shobhit
last-verified: 2026-10-23
verified-against: (pre-git)
asmdef: SlapMania.Gameplay
---

# M04 — TimingMultiplier

> **One-liner:** A marker ping-pongs across a bar of colored zones; the tap that stops it picks the damage multiplier.
> In the V2 **HoldRing** attack mode the same zones are read by a closing ring instead: hold to wind up, release on the gold band (§5b, [ADR-0014](../Decisions/ADR-0014-hold-ring-attack-mode.md)).

## 1. Purpose

The second half of every slap, and the skill ceiling of the whole game. The charge phase measures how hard you can mash; this measures whether you can hit a 60-millisecond window. Red pays 3×, so landing it is the single most satisfying moment available, and everything about this module exists to make that moment fair.

Fairness here is almost entirely a latency problem. The red band is 60 ms wide and one frame at 30 fps is 33 ms — if the marker is evaluated at frame time rather than at the input event's own timestamp, more than half the band is noise.

## 2. Responsibilities and non-goals

**Owns**
- Marker position as a pure function of elapsed time.
- The zone layout and the position-to-zone lookup.
- Latency compensation on the committing tap.
- The arm delay and the timeout.
- `NextTimeAtPosition`, which the AI uses to aim.
- The **hold ring** (`AttackMode.HoldRing`, §5b): closure as a pure function of hold time, the press/release meter, and the wind-up that sets power in that mode.

**Explicitly does NOT do**
- Draw the bar → `TimingBarView` in `M10` builds it from the layout asset at runtime.
- Decide the multiplier's effect → `M05` owns the damage formula.
- Know who is tapping → `ITapSource`, same as M03.

## 3. Public API surface

| Member | Signature | Notes |
|---|---|---|
| `TimingMeter.Begin` | `void Begin(TimingPhaseConfig cfg, TimingZoneLayout layout, ITapSource source, IClock clock, int roundIndex, IRandom rng, float overcharge01 = 0, float overchargeSpeedPenalty = 0, double lastTapAt = -inf)` | `lastTapAt` = the attacker's last charge tap (`PowerMeter.LastTapTime`), where the spam gap starts (S-093) |
| `TimingMeter.InCue` | `bool { get; }` | **2026-10-23.** True during the AIM cue: the ring shows, the marker holds still, no commit. Polled by `HudTiming` |
| `TimingMeter.CueSeconds` | `double { get; }` | Seconds from `Begin` to the moment the marker moves |
| `TimingMeter.EarliestCommitElapsed` | `double { get; }` | Marker time from which a commit can first count: max(`armDelay`, last press + spam gap). M07's AI aims no earlier |
| `TimingMeter.Tick` | `void Tick(double dt)` | |
| `TimingMeter.TryCommit` | `bool TryCommit(in TapEvent e)` | False for a commit whose *event time* (clamped to `[now − maxLatencyComp, now]`) is inside the arm delay, or after commit. Judged on the event time, not on whether this frame's `Tick` has armed the meter (S-087) |
| `TimingMeter.ForceResolve` | `TimingResult ForceResolve()` | Timeout path |
| `TimingMeter.NormalizedPosition` | `float { get; }` | **Polled** by the view |
| `TimingMeter.Direction` | `int { get; }` | +1 or −1 |
| `TimingMeter.NextTimeAtPosition` | `double NextTimeAtPosition(float p, double minLead)` | Closed-form ping-pong solve; used by `M07` |
| `MarkerOscillator.PositionAt` | `static float PositionAt(double elapsed, in MarkerConfig cfg)` | **Pure, static, stateless** |
| `TimingZoneLayout.Evaluate` | `ZoneHit Evaluate(float pos01)` | Pure |
| `LatencyCompensation.For` (Core) | `static double For(double rawLatency, double displayLatencyBias, double maxComp)` | §5's rule, shared by `TimingMeter` and `HoldRingMeter`. NaN-safe, never negative |
| `HoldRing.ClosureAt` (Core) | `static float ClosureAt(double heldSeconds, double closeSeconds)` | **Pure.** Linear, clamped to [0,1]; NaN/negative hold → 0; no close time → 1 |
| `HoldRing.HeldSecondsAt` (Core) | `static double HeldSecondsAt(float closure01, double closeSeconds)` | Closed-form inverse; used by M07 |
| `HoldRing.CloseSeconds` (Core) | `static double CloseSeconds(double closeMs, double roundSpeed)` | `closeMs / speedByRound`, speed clamped ≥ 0.01, NaN → 1 |
| `HoldRing.WindUpAt` (Core) | `static float WindUpAt(float closure01, float peakClosure, float exponent)` | 0..1, `t^exponent` up to the peak, then 1 |
| `HoldRingMeter.Begin` | `void Begin(HoldRingConfig, TimingPhaseConfig, PowerPhaseConfig, TimingZoneLayout, IHoldSource, IClock, int round)` | |
| `HoldRingMeter.TryPress` / `TryRelease` / `Tick` / `ForceResolve` | | Press only when armed; release only while holding |
| `HoldRingMeter.Closure` / `WindUp01` / `Power` / `HasPressed` | polled | Read by M10 `HudTiming` / `HudCharge` |
| `HoldRingMeter.PowerResult` / `TimingResult` | | Both halves of the slap from one gesture |

`TimingResult` is a `readonly struct`: `position`, `zone`, `multiplier`, `timedOut`, `latencyCompensated`, `rawLatency`, and **`direction`** (landed 2026-09-26). `direction` is the marker's travel direction at the commit, +1 or −1, passed by `TimingMeter` as the 7th constructor argument. With `position` it separates early from late: `(position − perfectCentre) × direction < 0` means the marker had not reached the centre on this pass. `SlapStats` (M01) uses it for the result screen's coaching line.

**Consumed by:** M01, M05, M07, M10, M11, M13.
**Stability:** churning until latency is measured on real hardware.

## 4. Configuration

### `TimingZoneLayout.asset`

Zones **tile `[0,1]` completely** — the user's "the colors cover the bar" — and are symmetric about centre so red is the geometric target.

| Zone | Ranges | Width | Colour | Multiplier | Window @ 1.0 traversal/s |
|---|---|---|---|---|---|
| `Miss` | 0.00–0.12, 0.88–1.00 | 0.24 | steel blue | **0.50** | — |
| `Weak` | 0.12–0.30, 0.70–0.88 | 0.36 | yellow | **0.85** | 180 ms/side |
| `Good` | 0.30–0.42, 0.58–0.70 | 0.24 | orange | **1.40** | 120 ms/side |
| `Great` | 0.42–0.47, 0.53–0.58 | 0.10 | red-orange | **2.00** | 50 ms/side |
| **`Perfect`** | 0.47–0.53 | 0.06 | **RED** | **3.00** | **60 ms** |

Expected value of a uniformly random tap: `0.24(0.50) + 0.36(0.85) + 0.24(1.40) + 0.10(2.00) + 0.06(3.00) = ` **1.142**.

### `HoldRingConfig.asset` (HoldRing mode, [ADR-0014](../Decisions/ADR-0014-hold-ring-attack-mode.md))

`Data/Prototype/HoldRing.asset`, `GameConfig.holdRing`. The round's speed and zone-scale curves, the zone layout and the latency compensation are this module's `TimingPhaseConfig`/`TimingZoneLayout`; the power floor and ceiling are M03's. Values below are the C# defaults the prototype builder created on 2026-10-20 (read back from the asset over MCP that day).

| Field | Type | Default | Sane range | Meaning / what breaks outside it |
|---|---|---|---|---|
| `closeMs` | int ms | **1000** | 700 – 1600 | Press to fully closed at round speed 1.0 (÷ `speedByRound`). 1000 gives every zone the marker's window at 1 traversal/s: Perfect = **60 ms** |
| `armDelayMs` | int ms | **150** | 100 – 250 | Presses ignored after the ring opens. A stray tap from the previous screen cannot start a hold |
| `pressTimeoutMs` | int ms | **4000** | 2500 – 6000 | No press by then: an un-wound Miss, `timedOut` |
| `windUpPeakClosure` | float | **0.50** | 0.4 – 0.6 | Where the wind-up reaches full power (0.5 = the gold centre). It then holds |
| `windUpExponent` | float | **1.0** | 0.5 – 2.5 | `t^e`: 1 linear; > 1 power arrives late (an early release is weak twice over); < 1 front-loaded |

### `TimingPhaseConfig.asset`

| Field | Type | Default | Sane range | Meaning / what breaks outside it |
|---|---|---|---|---|
| `baseTraversalsPerSecond` | float | **1.00** | 0.7 – 1.6 | One full left-to-right pass per second |
| `speedByRound` | curve | 0.85 @r1 → 1.15 @r5 → 1.45 @r10 | — | Legible difficulty: the player sees it and adapts |
| `zoneScaleByRound` | curve | 1.00 @r1–r5 → 0.70 @r10 | ≥ 0.70 | Width shrinkage; never below 0.7 |
| `armDelay` | float s | **0.15** | 0.10 – 0.25 | Taps ignored after the marker starts (after the AIM cue) |
| `aimCueMs` | int ms | **400** | 250 – 600 | **2026-10-23 (S-093, [ADR-0017](../Decisions/ADR-0017-aim-cue-and-spam-gap.md)).** The ring is shown frozen before the marker moves, and no tap commits. Below ~250 ms a masher has not reacted yet. Above ~600 ms the turn drags. The timeout counts from the end of the cue |
| `spamGapMs` | int ms | **220** | 150 – 300 | **2026-10-23 (S-093).** A commit counts only if the press before it (charge taps included) was at least this long ago. 220 ms rejects mashing at 4.5 taps/s and up. Above ~300 ms a player's deliberate second try feels ignored |
| `timeout` | float s | **3.0** | 2.0 – 5.0 | Resolves in place with `timedOut = true` |
| `displayLatencyBias` | float s | **0.030** | 0.015 – 0.060 | **Measure on hardware and set from that** |
| `maxLatencyComp` | float s | **0.120** | 0.080 – 0.150 | Hard clamp |
| `startPhaseGuard` | float | **0.05** | 0.03 – 0.08 | Resample radius around red |

**Invariants** (asserted on load, covered by `TuningInvariantTests`)
- Zone widths sum to **1.0 ± 1e-5** with no gaps and no overlaps.
- Multipliers strictly ordered: Perfect > Great > Good > Weak > Miss.
- The narrowest zone is at least **40 ms of marker travel** at the fastest configured speed.
- **Red is never narrower than 50 ms of wall clock at any round.** Below that it is a coin flip and players correctly perceive it as broken. At round 10 the default gives 1.45 × 0.042 = **29 ms** — verify this on a real device before committing, and if it reads as random, cap `zoneScaleByRound` at 0.8 and push difficulty into speed and opponent health instead.

## 5. The math

### Marker

**Ping-pong, not loop.** A marker that teleports from 1 back to 0 reads as a glitch, and it makes both ends of the bar unreachable in practice because you cannot anticipate a teleport. Ping-pong reads as a pendulum, passes through red twice per cycle, and gives the player a rhythm to lock onto.

```
sweep     = baseTraversalsPerSecond
          * speedByRound.Evaluate(roundNorm)
          * (1 + overchargeSpeedPenalty * overcharge)    // 0 by default
phase     = (startPhase + elapsedComp * sweep) mod 2
position  = phase <= 1 ? phase : 2 - phase
direction = phase <= 1 ? +1 : -1
```

`position` is a **pure function of `double` elapsed time**. It is never accumulated with `pos += speed * dt` — see [ADR-0004](../Decisions/ADR-0004-ms-based-timing.md).

`startPhase` is drawn per slap from the seeded RNG on `[0, 2)` so the pattern cannot be memorised, but is **resampled if the resulting start position lands within `perfectCentre ± startPhaseGuard`**, otherwise you occasionally gift a free Perfect on the first frame.

### Latency compensation — non-negotiable

```
latency     = Time.realtimeSinceStartupAsDouble - tapEvent.eventTime
comp        = clamp(latency + displayLatencyBias, 0, maxLatencyComp)
elapsedComp = phaseElapsed - comp
```

- `displayLatencyBias` covers render, compositor and display latency the Input System cannot see. It is overridable per-device by a `LatencyCalibration` value in `PlayerPrefs`.
- `maxLatencyComp` is a hard clamp so a GC hitch or a frame spike cannot back-date a tap into a free Perfect.

The debug HUD shows measured `latency` and applied `comp` per tap. You will need it on real hardware: the difference between a 16 ms and a 50 ms pipeline is the difference between "tight" and "broken".

### Zone scaling

`zoneScale` scales zone widths about the bar centre and redistributes the slack into the adjacent zone, so the layout still tiles `[0,1]` exactly at every round.

### 5b. The hold ring (`AttackMode.HoldRing`, [ADR-0014](../Decisions/ADR-0014-hold-ring-attack-mode.md))

The V2 attack (mockups C5 Aim, C6 Charge): **press and hold** to wind up while a white ring closes over the defender's face, **release** as it touches the gold band. One gesture yields both halves of the slap.

```
close    = closeMs / 1000 / speedByRound(round)          // this module's round curve
closure  = clamp01(held / close)                          // 0 open (press) .. 1 closed
zone     = TimingZoneLayout.Evaluate(closure, zoneScale)  // the marker's bands and zone scale
windUp   = closure >= peak ? 1 : (closure / peak)^exponent
rawPower = Lerp(minPower, maxPower, windUp)               // M03's floor and ceiling; overcharge 0
```

- **Closure is a layout position**, so the ring needs no zone table of its own. The Perfect band is the gold band; closure below `perfectCentre` is *early* and above is *late*. `TimingResult.Direction` is always +1, so `SlapStats`' early/late coaching works unchanged.
- **Pure function of time (R8, ADR-0004).** `held` is measured from the press's own event time, clamped to `[now − maxLatencyComp, now]` and to no earlier than the moment the ring armed. Nothing is accumulated.
- **The release is compensated exactly like a tap:** `held = (now − pressTime) − LatencyCompensation.For(now − releaseTime, displayLatencyBias, maxLatencyComp)`. The press needs no display bias: the player does not react to their own press.
- **Holding to full close** auto-releases at closure 1: a late Miss with `timedOut = false` (owner, 2026-10-20). **No press by `pressTimeoutMs`** gives closure 0 at `minPower` with `timedOut = true`.
- **Worked values** (round 1, speed 0.85, so close = 1176 ms; linear wind-up):
  - Release at 290 ms: closure 0.25 (Weak ×0.85), power 0.63, so **0.53** of a base slap.
  - Release on gold at 590 ms: closure 0.50 (Perfect ×3.0), power 1.0, so **3.0**.
  - Release at 900 ms: closure 0.77 (Weak), power 1.0, so **0.85**.

## 6. State machine

```
Idle --Begin--> Arming --aimCue + armDelay--> Armed --commit--> Resolved --> Idle
                  |   (AIM cue: InCue, marker frozen)  \--timeout-----/    ^
                  \--commit stamped past the arm moment------------------/   (one long frame, S-087)
```

Every commit (both states) also has to pass the spam gap, and its press has to be made after `Begin` (S-093, S-096). Every press moves the gap on, rejected or not.

| State | Entry | Exit | Timeout | Illegal (assert) |
|---|---|---|---|---|
| `Idle` | construction / after resolve | `Begin()` | — | `TryCommit` |
| `Arming` | `Begin()` | `armDelay` elapsed, or a commit whose *event time* is past the arm moment (S-087) | — | — (a commit *made* during the arm delay is silently rejected, however late it is delivered; not counted) |
| `Armed` | arm delay done | first accepted tap | `timeout` → `ForceResolve()` | second commit |
| `Resolved` | commit or timeout | read by M01 | — | `TryCommit` |

On timeout, resolve **at the marker's current position** with `timedOut = true`. That is a real roll — usually mediocre, never a hang and never a free pass. Forcing a `Miss` instead is defensible but feels arbitrary; resolving in place is honest.

**`HoldRingMeter`** (HoldRing mode):

```
Idle --Begin--> Arming --armDelay--> Ready --press--> Holding --release--------------> Resolved
                                       |                  \--ring fully closed (late)--/   ^
                                       \--pressTimeout, no press (timedOut)-----------------/
```

| State | Entry | Exit | Illegal (ignored) |
|---|---|---|---|
| `Arming` | `Begin()` | `armDelayMs` elapsed, or a press whose *event time* is past the arm moment (S-087) | a press made during the arm delay (counted in `PressesRejected`), release |
| `Ready` | arm delay done | the press; or `pressTimeoutMs` → un-wound Miss | release |
| `Holding` | accepted press | the release; or closure 1 → auto-release | a second press |
| `Resolved` | release, full close or timeout | read by M01 | everything |

## 7. Dependencies

- **Hard:** `SlapMania.Core` (`ITapSource`, `TapEvent`), `SlapMania.Data`.
- **Soft:** none.
- **Unity APIs:** `AnimationCurve`, `Time.realtimeSinceStartupAsDouble` (passed in via `IClock`, not read directly).
- **Must initialize after:** M00.

## 8. Failure modes

| Failure | Detection | Behaviour | Player-visible? |
|---|---|---|---|
| Zone layout does not tile | Load-time assert + invariant test | Loud editor error; refuse to enter the phase in a dev build | Editor only |
| Player never taps | `timeout` | Resolve in place | Yes — a mediocre slap |
| Tap during arm delay | Its event time, clamped | Ignored, also when a long frame delivers it after the meter armed | No |
| Tap marker: one long frame covers the arm delay and a commit (the AI ticks before the meter in `MatchDirector.Tick`; DEBUG S-087) | Arming judged on the commit's event time | The commit counts although `Tick` has not yet moved Arming -> Armed | No (before 2026-10-23: a lost turn, `timedOut`) |
| Tap marker: commit stamped long ago (hitch) or in the future | Clamp to `[now − maxLatencyComp, now]` before the arming check | A leftover charge tap delivered early in the phase is still rejected; the compensation is unchanged | No |
| Latency spikes past the clamp | `maxLatencyComp` | Compensation capped | No |
| Negative or NaN `dt` | Guard | Clamp to 0, Warn once | No |
| `NextTimeAtPosition` has no solution before `timeout` | Guard | Return the timeout instant; M07 falls back to a mid-bar aim | No |
| Hold ring: no press | `pressTimeoutMs` | Closure 0, `minPower`, `timedOut` | Yes — a weak Miss |
| Hold ring: held past the target | closure reaches 1 in `Tick` | Auto-release: late Miss at full power | Yes — coached "late" |
| Hold ring: press stamped long ago (hitch) or in the future | Clamp to `[now − maxLatencyComp, now]` and to the arm moment | The ring cannot pre-close or start late | No |
| Hold ring: release without a press (it began on the pause button, or while disabled) | State check | Ignored | No |
| Hold ring: one long frame covers the arm delay and a press (DEBUG S-087) | Arming judged on the press's event time | The press counts even though `Tick` has not yet moved Arming -> Ready | No (before the fix: a lost turn) |
| HoldRing selected but no `HoldRingConfig` | `GameConfig.Validate`; the director's fallback | Validate refuses the bout; a director built without it plays TapMarker with one Error | Editor only |

## 9. Debug hooks

- **Log channel:** `LogChannel.Timing`
- **Overlay (Slap tab):** normalized marker position **computed at event time and computed at frame time, side by side** — this single pair makes S-011 visible at a glance. Plus direction, sweep speed, the zone under the marker with its colour swatch and window in ms, measured latency, applied compensation, and the last resolution.
- **Cheats:** force zone (Red / Great / Good / Weak / Miss buttons), auto-perfect toggle, replay last slap, force `targetFrameRate` 30 to reproduce low-fps timing bugs on a fast phone.
- **Recorder events:** `TimingBegin`, `TimingStop`, `ZoneResolved`.
- **Editor tools:** `TimingZoneLayoutEditor` renders the zones as a horizontal colour bar with drag handles, shows each zone's width **in milliseconds** at the current speed, and puts up a red banner if the widths do not tile to 1.0 or if any zone is narrower than one frame at 30 fps. The S-010 guard, surfaced at authoring time.

## 10. Tuning notes

**The risk/reward shape is the point.** A random flail nets about 1.14×; a skilled player nets 3.00×. That 2.6× skill ceiling over noise is enough that mastery is visible, and the worst case (0.50×) is only 2.3× below random, so a bad tap costs a turn rather than the match. If you widen red, the ceiling drops and the game gets boring; if you narrow it, it stops being a skill and starts being a lottery. 6 % is the number that makes "I hit red" worth a screen flash and a crowd roar.

**Scale speed before you scale width.** Speed changes are legible — the player watches the marker move faster and adapts. Width shrinkage is nearly invisible on a phone screen unless the bar actually redraws, which it does because `TimingBarView` builds from the layout asset at runtime. Even then it feels unfair when overused. Rounds 1–5 should be speed only; start shrinking at round 6 and never past 0.7.

**The AIM cue and the spam gap now do the heavy lifting (2026-10-23, S-093).** The 150 ms arm delay alone was not enough: a masher needs ~250 ms to notice the phase changed. Treat the three together: `aimCueMs` gives the eye time, `spamGapMs` is the guarantee for a finger that keeps going, and `armDelay` covers the first instant of a moving marker.

**`armDelay` is the most important 150 milliseconds in the game.** Without it, the player's still-moving mash finger from the charge phase commits instantly and they get a garbage roll they did not choose. It is the number-one bug in this genre and it is invisible in the Editor with a mouse. Do not remove it, and do not tune it below 0.10 s.

**Never hand-place zone rects in the scene.** `TimingBarView` generates one `Image` per zone from the layout asset. If the asset changes and the UI does not, the game lies to the player about where red is, which is the worst bug this game can have.

**Latency bias is a per-device measurement, not a taste.** Set it from a real capture at the P5 milestone. If a competent player cannot land red roughly one attempt in four, the compensation is wrong, not the player.

## 11. Open questions

- [ ] Should the marker visually slow slightly near red (a "sticky" ease) to make the jackpot feel reachable, or does that make it a lie? Leaning toward no — the pure function is honest and testable.
- [ ] Does a second, faster marker at high rounds beat width shrinkage as a difficulty lever?
- [ ] Should the opponent's timing bar be rendered during the AI's turn? The AI genuinely plays the same meter, so it would be truthful — but it may just be noise the player ignores.
- [ ] Does the zone-entry audio ladder (M11) make red easier by ear alone, and is that a good thing?
- [ ] **Tap marker:** a commit delivered in a frame that also crossed `timeout` still counts (clamped to `maxLatencyComp`), because `TryCommit` runs before that frame's `Tick` and never checks the timeout. That was already true from `Armed` and is now also true from `Arming`. It is honest when the tap was made in time, but a commit *stamped* after the timeout should probably resolve as the timeout. Pinned by `TimingMeterTests.HugeDt_ACommitMadeInThatFrame_IsClampedNotDropped`.
- [ ] **Hold ring:** is touch *release* latency on device the same as press latency? The compensation assumes one `displayLatencyBias` for both. Measure both at the P5 latency capture.
- [ ] **Hold ring:** with power peaking on gold, C6's arc reaching MAX is itself a cue. Is Perfect now too easy compared with the marker? A/B on device. If so, move `windUpPeakClosure` earlier (e.g. 0.4) so the arc is full before gold.
- [ ] **Hold ring:** C6's mockup shows "Slow-mo 0.25×" during the wind-up. Not built: slowing `timeScale` would slow the fighters, not the ring (real time). Decide with M16/M08.

## 12. Changelog

| Date | Commit | Change |
|---|---|---|
| 2026-10-23 | (pre-git) | **Mash carry-over guards (DEBUG S-093, S-096; [ADR-0017](../Decisions/ADR-0017-aim-cue-and-spam-gap.md)).** Owner report: the multiplier was "consumed while spamming the charge". `TimingMeter` opens on an AIM cue (`aimCueMs` 400, marker frozen, `InCue`), and the arm delay and timeout count from the end of the cue. A spam gap (`spamGapMs` 220) means a commit needs the press before it to be at least that long ago, and the gap starts at `PowerMeter.LastTapTime`. A press made before `Begin` never commits (S-096: a 5 s-old stamp delivered late used to clamp past the arm moment). New `InCue`, `CueSeconds`, `EarliestCommitElapsed` and a `lastTapAt` parameter to `Begin`. `HudTiming` shows the cue copy and punches the ring when the marker starts. §3, §4, §6, §10 updated. `TestFactories.TimingCfg` turns both guards off for the older tests. New tests: cue, mash-through, pause-then-tap, gap ±1 µs, NaN/±inf last tap, `EarliestCommitElapsed`, a pre-phase press delivered after 0.1 s, 1 s and 1e6 s, and a full-FSM mash test. EditMode 267/267 |
| 2026-09-19 | (pre-git) | Created (Draft) with the zone table, marker math and latency model |
| 2026-09-19 | (pre-git) | **P3 code landed.** `MarkerOscillator` (static, pure, ping-pong from `double` elapsed per ADR-0004), `TimingMeter` (Arming/Armed/Resolved FSM, latency compensation with `maxLatencyComp` clamp, timeout resolves in place with `timedOut=true`), and `TimingZoneLayout` SO holding the symmetric 9-band table that tiles `[0,1]` and evaluates zone widths under `zoneScale`. EditMode tests cover: start-phase equality at t=0, ping-pong (not loop), no drift at 10 000 s, `NextTimeAtPosition` crossing search, `TilesUnitInterval`, red-at-centre, zone-scale shrink. **Deviation from §5:** the `NextTimeAtPosition` used by M07's AI is currently a 200-samples-per-sweep linear-scan crossing search rather than a closed-form ping-pong solve — replace with the closed form once benchmarks show it matters. |
| 2026-09-26 | (pre-git) | **`TimingResult.Direction`** (+1/−1, the marker's travel direction at the commit) added; `TimingMeter` passes it. Lets `SlapStats` (M01) tell early from late for coaching. No change to zone lookup or multipliers; EditMode suite green. The V2 ring over this meter is M10 `HudTiming`: its radius is a pure function of `NormalizedPosition`, and its gold band is this module's Perfect zone scaled by the round's zone scale. |
| 2026-10-23 | (pre-git) | **Hold-ring attack mode ([ADR-0014](../Decisions/ADR-0014-hold-ring-attack-mode.md)).** New §5b, a `HoldRingConfig` table in §4, and §3/§6/§8/§11 rows. Pure Core `HoldRing` (`ClosureAt`, `HeldSecondsAt`, `CloseSeconds`, `WindUpAt`) and `LatencyCompensation.For`; `TimingMeter.TryCommit` now uses the latter, with the same result except that a NaN latency counts as 0. Gameplay `HoldRingMeter` (Arming/Ready/Holding/Resolved; the press starts the ring at its event time, the release is compensated, full close auto-releases late, no press times out). Data `HoldRingConfig`. Owner decisions 2026-10-20: power from hold time, HoldRing default, auto-release on full close. Tests: `HoldRingTests` (Core R3 matrix: 0, 1, every band edge at zone scale 1 and 0.7, NaN, negative, huge, the 60 ms gold window, the wind-up contract over nine exponents including 0/−1/NaN/∞, latency clamps) and `HoldRingMeterTests` (Perfect at gold, compensation and clamp, early/late coaching, timeout, arm-delay rejection, stale and future press stamps, bad and huge dt, round-10 speed and zone scale, zero allocation while holding and releasing, AI lands Perfect over 12 seeds, a full HoldRing duel through the FSM, the fallback, and a per-turn mode switch). **Play-verified 2026-10-23** from Boot via the Input System (Space press/release): Perfect at pos 0.504 (×3, power 1.00, 39 ms compensation), an early release at 0.263 (Weak, power 0.65), and a full close at 1.000 (Miss, not timed out). AI rings: Weak, Good, Perfect. **Fix found in Play (DEBUG S-087):** `TryPress` judges arming on the press's event time, so a long frame can no longer drop the AI's press. Three new tests cover it. |
| 2026-10-23 | (pre-git) | **Tap marker: arming judged on the commit's event time (DEBUG S-087, TapMarker half).** `TimingMeter.TryCommit` accepts a commit in `Arming` or `Armed`, clamps its event time to `[now − maxLatencyComp, now]` (NaN or a future stamp counts as now), and rejects it only if that time is before `start + armDelay`. Same rule as `HoldRingMeter.TryPress`. Before, it required state `Armed`, and `Arming -> Armed` happens in `Tick`, so one long frame covering the arm delay and a commit dropped the commit. The AI ticks before the meter in `MatchDirector.Tick`, so its turn timed out. The opposite case was also wrong: a commit made during the arm delay but delivered after the arm tick used to count, and is now rejected. The latency compensation is unchanged. §3, §6, §8 and §11 updated. New `TimingMeterTests` (this meter had no tests of its own): commit during arming then a fresh one, a commit made after the arm moment before the frame's `Tick`, a commit made during arming delivered late (before and after the arm tick), the exact arm moment and 1 µs before it, NaN, future and stale stamps, NaN/negative dt, huge dt with and without a commit, and the M07 AI cases (S-079, S-088). 26 of the 32 failed before the fix. EditMode 230/230. |
| 2026-10-23 | (pre-git) | **R5: the commit and timeout frames no longer allocate (DEBUG S-089).** `TimingMeter` caches its tap handler once in the constructor (`_onTap`, like `HoldRingMeter._onPress`) instead of converting the `HandleTap` method group, which allocates a delegate, on every `+=`/`-=`. New `TimingMeterTests.CommitAndTimeout_AllocateNothing` (positive control, warm-up, then one commit and one timeout under `Is.Not.AllocatingGCMemory()`). It failed before the fix with "the commit frame allocated". EditMode 231/231. No behaviour change. |
