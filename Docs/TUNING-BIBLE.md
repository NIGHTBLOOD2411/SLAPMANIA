# Slap Mania — Tuning Bible

Status: Draft — Last verified 2026-09-26

Every designer-facing number in one place. This duplicates section 4 of each module doc **by design**: the module doc is for the engineer changing that module; this file is for the designer balancing across modules, who must never have to open eighteen files.

**Rule:** if changing a number here requires a recompile, the data-driven design has failed and that is the bug to fix.

---

## 1. Charge phase — `PowerPhaseConfig`

| Field | Default | Sane range | Meaning / what breaks outside it |
|---|---|---|---|
| `phaseDuration` | **2.50 s** | 1.5 – 4.0 | Below 1.5 s there is no room for the crescendo arc; above 4 s the thumb tires and it stops being fun |
| `impulsePerTap` | **0.072** | 0.04 – 0.12 | Fill added per accepted tap before modifiers |
| `decayPerSecond` | **0.12** | 0.05 – 0.30 | The "keep going or lose it" pressure. At 0 the phase becomes a pure count and the arc disappears |
| `maxFill` | **1.00** | fixed | |
| `minTapInterval` | **0.045 s** | 0.035 – 0.060 | ~22 taps/s ceiling. Also guards against touch-driver double reports. Below 0.035 macros win |
| `diminishWindow` | **0.30 s** | 0.20 – 0.50 | Sliding window for the tap-rate count |
| `diminishSoftTaps` | **3** (= 10 tps) | 2 – 5 | Where diminishing returns begin |
| `diminishHardTaps` | **6** (= 20 tps) | 5 – 9 | Where diminishing returns bottom out |
| `diminishFloor` | **0.50** | 0.35 – 0.75 | At 0.35 the curve *inverts* past 15 tps and hard mashing becomes actively worse — only do that with telemetry justifying it |
| `phaseShape` | curve **1.00 @0 → 1.00 @0.6 → 1.60 @1.0** | peak 1.2 – 2.0 | Late taps are worth more. This is what creates the finishing sprint |
| `feedbackCurve` | **x^1.15** | x^1.0 – x^1.4 | Maps fill to power; slight ease-in |
| `minPower` | **0.25** | 0.15 – 0.35 | Zero taps still yields a weak slap. **Never 0** — a zero-damage state risks a non-terminating match |
| `maxPower` | **1.00** | fixed | |
| `overchargeRatePerTap` | **0.09** | 0.05 – 0.15 | Accrual once fill is capped |
| `overchargeGain` | **0.12** | 0.0 – 0.25 | Max additive power from overcharge (+12 %) |
| `overchargeSpeedPenalty` | **0.0** (off) | 0.0 – 0.25 | If enabled, high overcharge speeds up the timing marker — turns the charge phase from pure upside into a genuine gamble. Playtest at the vertical slice |

### The shape these numbers produce

Net fill rate at a sustained tap rate, ignoring the crescendo:

| taps/s | diminish `d` | gain/s | net/s | fill at 2.5 s |
|---|---|---|---|---|
| 0 | — | 0 | −0.12 | 0.00 → power 0.25 |
| 3 | 1.00 | 0.216 | 0.096 | 0.24 |
| 5 | 1.00 | 0.360 | 0.240 | 0.60 |
| 7 | 1.00 | 0.504 | 0.384 | 0.96 |
| 10 | 1.00 | 0.720 | 0.600 | **full at 1.67 s**, then 0.83 s of overcharge |
| 15 | 0.675 | 0.729 | 0.609 | full at 1.64 s |
| 22 (cap) | 0.50 | 0.792 | 0.672 | full at 1.49 s |

**The anti-exploit shape is a plateau, not a penalty.** A 22 taps/s autoclicker beats a 10 taps/s human by about 11 %, not 120 %. Making hard mashing actively *worse* reads as the game punishing enthusiasm, and players correctly report it as a bug.

Optimal play is a sustained 8–10 taps/s with a finishing sprint — which is exactly the pantomime of winding up a slap.

---

## 2. Timing phase — `TimingPhaseConfig` and `TimingZoneLayout`

### Zone layout (default)

Zones **tile `[0,1]` completely** — validated for contiguity and full coverage, and symmetric about centre so red is the geometric target.

| Zone | Ranges | Total width | Colour | Multiplier | Window @ 1.0 traversal/s |
|---|---|---|---|---|---|
| `Miss` | 0.00–0.12, 0.88–1.00 | 0.24 | steel blue | **0.50** | — |
| `Weak` | 0.12–0.30, 0.70–0.88 | 0.36 | yellow | **0.85** | 180 ms per side |
| `Good` | 0.30–0.42, 0.58–0.70 | 0.24 | orange | **1.40** | 120 ms per side |
| `Great` | 0.42–0.47, 0.53–0.58 | 0.10 | red-orange | **2.00** | 50 ms per side |
| **`Perfect`** | 0.47–0.53 | 0.06 | **RED** | **3.00** | **60 ms** |

Widths sum to exactly **1.00** — asserted by `TimingZoneLayout_TilesUnitInterval`.

**Expected value of a uniformly random tap: 1.142×.** A skilled player gets 3.00×. That is a 2.6× skill ceiling over noise — enough that mastery is visible, while the worst case (0.50×) is only 2.3× below random, so a miss costs a turn rather than the match.

Red at 6 % of the bar makes "I hit red" a genuine event, worth a screen flash and a crowd roar.

### Marker and commit

| Field | Default | Sane range | Notes |
|---|---|---|---|
| `baseTraversalsPerSecond` | **1.00** | 0.7 – 1.6 | One full left-to-right pass per second |
| Sweep style | **ping-pong** | fixed | Not looping. A marker teleporting 1→0 reads as a glitch and makes both ends unreachable in practice |
| `startPhase` | random per slap | — | Resampled if it lands within `perfectCentre ± 0.05`, otherwise you occasionally gift a free Perfect on frame one |
| `armDelay` | **0.15 s** | 0.10 – 0.25 | Taps ignored at phase start. **Without this the player's still-moving mash finger instantly commits a garbage roll** — the number-one bug in this genre |
| `timeout` | **3.0 s** | 2.0 – 5.0 | Resolves at the marker's current position with `timedOut = true`. A real roll, usually mediocre; never a hang, never a free pass |
| `displayLatencyBias` | **0.030 s** | 0.015 – 0.060 | Covers render + compositor + display latency the Input System cannot see. **Measure on real hardware and set from that** |
| `maxLatencyComp` | **0.120 s** | 0.080 – 0.150 | Hard clamp so a GC hitch cannot back-date a tap into a free Perfect |

Compensation: `comp = clamp(realtimeNow − tapEvent.eventTime + displayLatencyBias, 0, maxLatencyComp)`, then evaluate the marker at `elapsed − comp`.

### Difficulty scaling

| Curve | Round 1 | Round 5 | Round 10 |
|---|---|---|---|
| `speedByRound` | 0.85 | 1.15 | 1.45 |
| `zoneScaleByRound` | 1.00 | 1.00 | 0.70 |

**Scale speed only for rounds 1–5.** Speed changes are legible — the player sees the marker move faster and adapts. Start shrinking zone widths only at round 6+, and never below 0.7×; width shrinkage is invisible on a small screen unless the bar actually redraws (it does — `TimingBarView` builds from the layout at runtime), and it feels unfair when overused.

At round 10: 1.45 traversals/s × 0.042 red width = **29 ms**. That is the intended brutal endgame, but **verify it on a real device before committing**. If it reads as random, cap shrinkage at 0.8 and push difficulty into speed and opponent health instead.

**Hard floor: never ship red narrower than 50 ms of wall clock at any difficulty.** Below that it is a coin flip and players correctly perceive it as broken.

---

### Hold ring — `HoldRingConfig` (V2 attack mode, [ADR-0014](Decisions/ADR-0014-hold-ring-attack-mode.md))

Used when `BoutConfig.attackMode = HoldRing`. It was the default from 2026-10-20; since 2026-10-23 the default is **TapMarker** (ADR-0014 amendment, DEBUG S-090: a tap in the ring is an instant release worth ~4 damage). Before making it the default again, retune it: held well, it averages 48.6 a slap against 100 HP. The zones, round speed curve, zone scale and latency values above apply unchanged. Full table: M04 §4.

| Knob | Default | What it does |
|---|---|---|
| `closeMs` | 1000 | Press to fully closed at round speed 1.0. With the round curve: 1176 ms at round 1, 690 ms at round 10. The gold (Perfect) window is 6 % of it × the round's zone scale: 71 ms at round 1, 29 ms at round 10 (0.06 × 0.70 × 690). That is the same as the marker, including M04 §4's round-10 warning |
| `armDelayMs` | 150 | Presses ignored after the ring opens |
| `pressTimeoutMs` | 4000 | No press: an un-wound Miss |
| `windUpPeakClosure` | 0.50 | Power is full from the gold centre on |
| `windUpExponent` | 1.0 | 1 linear; 2 means a half-way release carries 25 % of the power range instead of 50 % |

Release outcomes at round 1 (linear): 290 ms gives Weak × power 0.63 = **0.53**; 590 ms gives Perfect × 1.0 = **3.0**; 900 ms gives Weak × 1.0 = **0.85**. Holding to full close gives Miss × 1.0 = **0.5**.

## 3. Damage — `MatchRules` and `FighterStats`

```
raw    = baseSlapDamage · rawPower · multiplier · (1 + slapIndexDamageBonus)^slapIndex
                        · (1 − defender.damageReduction) · globalDamageScale
damage = max(minDamageFloor, round(raw))
```

| Field | Default | Sane range | Notes |
|---|---|---|---|
| `baseSlapDamage` | **22** | 10 – 40 | Per-fighter |
| `maxHealth` | **100** | 60 – 300 | Per-fighter; scaled per ladder node |
| `damageReduction` | **0.0** | 0.0 – 0.4 | Above 0.4 fights drag |
| `slapIndexDamageBonus` | **0.06** | 0.0 – 0.12 | Per-slap ramp; guarantees termination |
| `minDamageFloor` | **ceil(maxHealth × 0.02)** | 1 – 5 % | Guarantees termination |
| `critThreshold` | **2.00** | 1.4 – 3.0 | Multiplier at which a slap counts as a crit |
| `maxTurns` | **40** | 30 – 60 | Forced sudden death backstop. A fuzz test asserts it is never reached under normal tuning |
| `globalDamageScale` | **1.00** | 0.5 – 2.0 | The single knob for "fights are too long / too short" |

### Worked outcomes at base 22 / HP 100 / no mitigation / slapIndex 0

| Case | fill | overcharge | zone | rawPower | mult | **damage** |
|---|---|---|---|---|---|---|
| Floor — no taps, missed | 0.00 | 0 | Miss 0.50 | 0.250 | 0.50 | **3** |
| Poor | 0.24 | 0 | Weak 0.85 | 0.394 | 0.85 | **7** |
| Median | 0.60 | 0 | Good 1.40 | 0.669 | 1.40 | **21** |
| Good | 0.96 | 0 | Great 2.00 | 0.969 | 2.00 | **43** |
| Ceiling | 1.00 | 1.0 | Perfect 3.00 | 1.120 | 3.00 | **74** |

Two slaps to kill at ceiling, about five at median. Target: round-1 opponents die in **3–4 median player slaps** (≈ 80 HP), round-10 in 6–7 (≈ 150 HP).

**Bout length estimate: 10–13 slaps total, tail to 16.** This is what sizes the snapshot atlas at 16 tiles.

---

## 4. Opponent AI — `AiProfile` and `AiDifficultyConfig`

The AI drives the *same* meters through synthetic taps. `targetTps` feeds the power meter; the zone weight table is the AI's **intent** and `aimSigma` is its **execution**. Keep both — intent alone is too clean, execution alone cannot express "this fighter goes for broke".

| Round | `targetTps` | `tpsSigma` | `aimSigma` | Zone weights (Perfect / Great / Good / Weak / Miss) |
|---|---|---|---|---|
| 1 | 4.5 | 1.0 | 0.140 | 3 / 10 / 32 / 38 / 17 |
| 5 | 7.0 | 0.9 | 0.085 | 10 / 22 / 40 / 23 / 5 |
| 10 | 9.0 | 0.7 | 0.050 | 22 / 32 / 36 / 9 / 1 |

| Field | Default | Notes |
|---|---|---|
| `tapJitter` | ±0.12 | Per-tap interval jitter |
| `reactionBias` | 0.0 s | Systematic early/late bias on the commit |
| `commitJitter` | 0.02 s | Gaussian noise on the commit time |
| `minAiThinkTime` | 0.25 s | Headroom before the AI commits, so the opponent's turn has a beat of anticipation |
| `rubberBandEnabled` | **false** | Designers will ask for it on day two. Keep the base tuning honest first |
| `rubberTps` | 1.5 | Applied as `tps += Δ · rubberTps` where `Δ = playerHealthFrac − aiHealthFrac` |
| `rubberSigma` | 0.03 | Applied as `aimSigma −= Δ · rubberSigma` |

Rubber banding, when enabled, is capped, one-directional (it only helps the AI) and disabled on the final node so the climax is honest.

---

## 5. Hitstop and impact — `SlapMotionConfig`

The authoritative impact timeline. Multiple systems must agree on it, so it lives here in one table.

| t (unscaled) | Event | `timeScale` |
|---|---|---|
| 0 ms | Contact. Damage committed, impact SFX stack lands, shake trauma applied | 1.0 → **0.25** |
| 0–35 ms | Smear window — animation advances slowly into recoil | 0.25 |
| **35 ms** | **Snapshot captured** + white flash + shutter SFX + polaroid pop spawn | 0.25 → **0.02** |
| 35–125 ms | Hard freeze. Flash decays, polaroid scales in, frame desaturates 60 % | 0.02 |
| 125–190 ms | Ramp back | 0.02 → 1.0 |

| Field | Light | Medium | Heavy / crit | Finisher |
|---|---|---|---|---|
| Hitstop duration | 60 ms | 90 ms | 140 ms | 400 ms @ 0.15, then a 600 ms ramp |
| Shake trauma | 0.55 | 0.75 | 1.00 | 1.40 + directional kick |
| `windupDuration` | — | **0.50 s** | — | — |
| `impactDelay` (scripted fallback) | — | **0.32 s** | — | — |
| `snapshotDelay` after contact | — | **0.035 s** | — | — |
| `reactionDuration` | — | **1.0 s** | — | 2.5 s |

**Why contact + 35 ms and not contact:** contact is the frame the hand touches, and it is the *worst* photo — no deformation yet, no recoil, no expression. Peak facial squash and maximum head rotation are about two frames later.

### What the prototype actually runs today (2026-09-26)

The table above is the *target* timeline. The playable prototype exposes these live values — all in assets, all in the Tuning Hub, all editable in Play Mode with the change kept afterwards:

| Asset | Field | Value | Target above | Note |
|---|---|---|---|---|
| `BoutConfig` | `introMs` | 500 | — | 2000 once there is a camera fly-in (M01) |
| `BoutConfig` | `windupMs` | 500 | 0.50 s | Matches |
| `BoutConfig` | `hitstopMs` | 90 | — | FSM state length, not the visual freeze |
| `BoutConfig` | `snapshotMs` | 50 | 35 ms delay | Window, not delay; M09 not built |
| `BoutConfig` | `finisherMs` | 2500 | 2.5 s reaction | Matches |
| `BoutConfig` | `finisherSnapshotMs` | 100 | — | |
| `BoutConfig` | `turnTransitionMs` | 300 | — | M01 targets 800 once the camera swaps sides |
| `FeelConfig` | `hitstopMs` | 90 | 90 ms (medium) | One value for all hits today |
| `FeelConfig` | `critHitstopBonusMs` | +40 | 140 ms heavy/crit | 90 + 40 = 130 |
| `FeelConfig` | `finisherHitstopBonusMs` | +120 | 400 ms + ramp | 90 + 120 (+40 if crit) = 210–250 ms; the ramp does not exist yet |
| `FeelConfig` | `recoilDamageForFull` | 45 | — | Damage that gives a full recoil |
| `FeelConfig` | callout punch / hold / fade | 1.35x, 140 / 700 / 250 ms | — | Damage numbers hold 900 ms |

Freeze depth is `HitstopController._frozenTimeScale` = 0.02 (a single step, not the 0.25 → 0.02 → ramp profile above).

### Humanoid fighter — `FighterRigProfile` (full table in M08 §4)

| Knob | Value | What you feel |
|---|---|---|
| Strike IK blend in / hold / out | 170 / 90 / 240 ms | How long the hand is magnetised to the cheek around contact |
| Hit spring frequency / damping | 5.5 Hz / 0.35 | Snap and wobble of the head after a slap |
| Hit spring impulse / max angle | 950 °/s / 55° | How far a full-strength slap turns the head |
| KO launch / up / head extra | 3.2 / 1.1 / 2.6 m/s | How far the loser flies on the lethal slap (they cleared the frame at 2.5 m in a test KO) |
| Camera kick (`CameraRig`) | 0.06 m, 1.4°, 0.28 s | Impact kick, scaled by damage; full on the KO. Component values: Play edits revert (M16) |

---

## 6. Snapshot — `SnapshotConfig`

| Tier | Tile | Atlas | Atlas RGBA32 sRGB | Scratch (2× tile) | Depth 16-bit | **Total** |
|---|---|---|---|---|---|---|
| High | 512×640 | 2048×2560 | 20.00 MiB | 5.00 MiB | 2.50 MiB | **27.5 MiB** |
| **Mid (default)** | **384×480** | **1536×1920** | **11.25 MiB** | **2.81 MiB** | **1.41 MiB** | **15.5 MiB** |
| Low | 256×320 | 1024×1280 | 5.00 MiB | 1.25 MiB | 0.63 MiB | **6.9 MiB** |
| Off | — | — | 0 | 0 | 0 | **0** |

| Field | Default | Notes |
|---|---|---|
| `maxCount` | **16** (4×4 grid) | Sized from the 10–13 typical / 16 tail bout length |
| `format` | `R8G8B8A8_SRGB` | **Not RGB565** — it has no sRGB variant, so in a Linear project it bands across skin tones, on the one asset the whole feature is built around. RGB565 only at Low tier |
| `antiAliasing` | **1 (off)** | Multisampled RT readback is undefined on several Adreno drivers. Edge quality comes from the 2× supersample downsample, which is better than 2×MSAA anyway |
| `memorylessMode` | `None` | Memoryless on a tile GPU throws the contents away |
| Eviction score | `damage × (isPlayer ? 1.0 : 0.85) × (zone == Red ? 1.5 : 1.0)` | Lowest non-finisher tile evicted when full |
| Finisher slot | **reserved, never evicted** | |
| High tier gate | `systemMemorySize >= 4096 && graphicsDeviceType == Vulkan` | |

---

## 7. Audio — key numbers

Full detail in [Modules/M11-Audio.md](Modules/M11-Audio.md).

| Field | Value | Notes |
|---|---|---|
| **BPM** | **140** | Locks the whole beat-sync layer. Confirm before commissioning music |
| Key | **F minor** | Zone cues and the red-zone stab are tuned to it |
| Loop length | 27.43 s | 16 bars at 140 BPM |
| Real voices | **28** | Down from Unity's default 32 |
| Virtual voices | 256 | Down from the template's 512 |
| `AudioSource` components | 40 | 18 dedicated + 14 2D pool + 8 3D pool |
| DSP buffer | **512** (High/Mid), 1024 (Low) | 512 buys ~43 ms over 1024, costs 1.5–3 % of a core |
| Sample rate | device native (48 kHz) | **Author everything at 48 kHz** or Unity resamples per voice at runtime |
| DSP CPU budget | ≤ 10 %, ceiling 14 % | A three-tier quality fallback is mandatory, not optional |
| Audio RAM | ≤ 20 MB in-bout, ≤ 24 MB peak | |
| Mix target | −14 LUFS integrated, ≤ −1.0 dBTP | Master limiter: threshold −2.0 dB, ratio 20:1, attack 2 ms, release 120 ms |
| `contactDsp` lead | **≥ 180 ms** | Published at windup start so the impact stack can be scheduled backwards. This is how you beat 50–150 ms Android output latency |

### Zone cue pitch ladder (semitones against F minor)

| Zone | Pitch ratio | Interval | Gain |
|---|---|---|---|
| Weak | 0.8909 | −2 st | −14 dB |
| Good | 1.0000 | root | −12 dB |
| Great | 1.1892 | +3 st | −9 dB |
| **Red** | 1.4983 | +7 st | −5 dB |

### Charge audio

| Element | Voices | Drives |
|---|---|---|
| Drone loop | 1 | **Power** — pitch 0.80 → 1.45, low-pass 600 → 9000 Hz, smoothing τ = 0.09 s |
| Ratchet loop | 1 | **Tap rate** — pitch 0.90 → 1.35 across 6 → 16 taps/s |
| Accents | ≤ 2 | Decimated to ~4–6/s, plus guaranteed accents at 25/50/75/90/100 % power |
| Overdrive | ≤ 1 | Above 92 % power |

Five voices total instead of the 20–38 a naive one-shot-per-tap would need.

---

## 8. UI layout — portrait 1080×1920 reference

> **Landed V2 HUD (2026-09-26):** the reference is the V2 frame, **430×932, `Expand`** (M10 §4). The table below is the pre-V2 plan.

`CanvasScaler`: Scale With Screen Size, reference **1080×1920**, **Match = 0 (Width)**. Matching width keeps element sizes consistent across aspect ratios and turns extra height into vertical headroom.

| y-range | Element |
|---|---|
| 0–120 | Safe-area pad |
| 120–270 | Opponent row — portrait, name, health bar |
| 270–360 | Turn indicator + `SHOT n/16` chip (the polaroid fly-to target) |
| 360–1240 | **3D viewport — no UI.** Damage numbers and callouts overlay here |
| 1240–1390 | Player row, mirrored |
| **1400–1530** | **Timing meter** — 900×110 |
| **1550–1680** | **Power bar** — 900×110 |
| **1700–1900** | **Tap zone** — full-width invisible raycast target with a pulsing prompt |

Two constraints that are gameplay decisions disguised as layout:

1. **The power bar and timing meter occupy the same screen band**, so the thumb never moves between the mash phase and the precision phase.
2. **The tap zone must not overlap the meter.** The stop-tap is accepted anywhere in the tap zone, so the thumb never occludes the thing being read. Keep a 20 px gutter.

Reserve 120 reference-px of bottom padding for the iOS home indicator — the tap zone sits exactly where a swipe-up misfire would hurt most.

---

## 8b. Arena look — `ArenaLookConfig`, `ArenaPost`, arena materials

[ADR-0012](Decisions/ADR-0012-arena-mobile-fakes.md). Lighting and atmosphere live on `Data/Prototype/ArenaLook.asset` (M14 §3 has every field), the grade on `Data/Prototype/ArenaPost.asset`, and surface looks on `Art/Arena/Materials/*.mat`. All three are created once by the builder and never overwritten; `Tools > Slap Mania > Prototype > Reset Arena Look` recreates the defaults.

| Knob | Value | Where | What you feel |
|---|---|---|---|
| Key spot | 110, 48°, warm white | ArenaLook | The pool of light on the podium; the reference's hero light |
| Rims L / R | red 60 / blue 55 | ArenaLook | The fighters' outline against the dark crowd |
| Fog density | 0.02 (exp²) | ArenaLook | How fast the upper tier falls into darkness |
| Impact flash | key ×(1+1.4k²), beams ×(1+1.5k²), decay 6/s unscaled | ArenaLook | The arena "pops" on a slap; full on the KO |
| Beam sweep | ±8°, 7000 ms | ArenaLook | Slow life in the rig |
| Grade | ACES; exposure +0.05; contrast 28; saturation 14; lift −0.03; bloom thr 1.0 / int 1.0 / scatter 0.65; vignette 0.38 | ArenaPost | Crushed blacks, bloomed lamp heads. A lift of −0.06 crushed the crowd to black, so −0.03 is the ceiling tested |
| Haze | 0.02 intensity, 3 sheets | `Arena_Haze.mat` | Air between the rig and the crowd. At 0.07 it veiled the whole frame lavender |
| Key beam / beams | 0.5 / 0.45 intensity | `Arena_KeyBeam.mat` / `Arena_Beam.mat` | Visible shafts |
| Crowd | jitter 0.13 m, top highlight 0.35, standing 35%, arms up 12%, phone flashes 7% | `Arena_Crowd.mat` | Reads as people, not rows of icons |
| Damage cam | 512×288, every 2nd frame | `DamageCam` + RT asset | Jumbotron smoothness vs GPU (set 4 for the Mid-tier budget) |

---

## 9. Progression (designed, not built in v1)

| Track | L0 → L8 |
|---|---|
| Toughness | maxHealth 100 → 180 |
| Power | baseSlapDamage 14 → 24 |
| Precision | red zone width 6 % → 10 %, Great 10 % → 16 % |
| Stamina | taps for a full bar 18 → 11 |

Cost curve: `cost(level) = round(50 · 1.6^level)` → 50, 80, 128, 205, 328, 524, 839, 1342. About 3,296 per track, 13,184 for all four.

Coins per bout: `base(node) + damageDealt / 10 + 150 if flawless`.

Ladder: four tournaments (Backyard → Bar → Underground → Multiversal), 8 nodes each. Across the 32 nodes: maxHealth 100 → 260, basePower 12 → 26, AI accuracy mean 0.45 → 0.88, red-zone chance 0.05 → 0.34, tap rate 4.5 → 9.0.

---

## 10. Change log

| Date | Change | Why |
|---|---|---|
| 2026-09-19 | Initial values recorded from the design pass | Baseline before any playtest |
| 2026-09-26 | §5 gained "What the prototype actually runs today": `BoutConfig` pacing (ms) and the new `FeelConfig` | MatchDirector's const durations and BoutController's feel fields moved into assets so the Tuning Hub can edit them live ([ADR-0011](Decisions/ADR-0011-adopt-v2-design-prototype-first.md)) |
| 2026-09-26 | §5 fighter rig table; §8 note on the V2 HUD canvas (430×932 Expand); new §8b arena look | The humanoid fighter, the V2 HUD and the generated arena landed; their knobs are in `FighterRigProfile`, `ArenaLookConfig`, `ArenaPost` and the arena materials. The grade and haze values were tuned against the reference frames in the Editor, not on a device |
| 2026-10-23 | §2 gained the hold-ring knobs (`HoldRingConfig`) and its release outcomes | The V2 hold-ring attack landed as an attack mode ([ADR-0014](Decisions/ADR-0014-hold-ring-attack-mode.md)); defaults chosen for window parity with the marker, not yet playtested |
| 2026-10-23 | §2 hold-ring intro: the default attack mode is TapMarker again | Owner playtest: taps barely hurt in the ring (DEBUG S-090). Simulated vs `Ai_Round1`: tapping in the ring 0/20 wins at 4.7 a slap; TapMarker 9/20 at 25.9. ADR-0014 amendment |
