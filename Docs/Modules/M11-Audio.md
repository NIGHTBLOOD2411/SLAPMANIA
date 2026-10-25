---
module: M11-Audio
status: Draft
owner: shobhit
last-verified: 2026-09-19
verified-against: (pre-git)
asmdef: SlapMania.Audio
---

# M11 — Audio

> **One-liner:** A complete arena audio system — music stems, beat clock, announcer VO, crowd, SFX layering, and impact scheduling that beats Android output latency by scheduling backwards from a predicted contact time.

## 1. Purpose

The arena should sound like a live event. Loud music synced to gameplay intensity, an announcer who calls the action, a crowd that reacts, and impacts that land with physicality. All of it must run within 28 real voices, 10% of one core's DSP budget, and 20 MB of audio RAM on a phone — and it must survive iOS audio session interruptions without dying silently.

The beat clock is the backbone. Music, lighting (M14), VFX (M15), and camera (M16) all sync to the same `IBeatSource`. When the beat drops, everything pulses together. When the intensity rises, stems fade in and the crowd gets louder. The clock runs on `AudioSettings.dspTime`, never on `Time.time`, because `Time.time` drifts under frame-rate variation and `dspTime` is driven by the audio hardware at sample accuracy.

## 2. Responsibilities and non-goals

**Owns**
- The Unity `AudioMixer` tree (master → music / SFX / VO / crowd / UI, plus duck buses).
- `BeatClock` — anchored on `AudioSettings.dspTime`, exposes `IBeatSource`.
- `MusicDirector` — three-stem vertical layering with bar-accurate `PlayScheduled` section changes.
- `IntensityDriver` — maps match state + health + streak to a 0–1 intensity that drives stem fades and crowd volume.
- `VODirector` — priority queue, bag-shuffle anti-repetition, carrier+name stitching.
- `CrowdController` — layered ambient beds that react to events.
- `SFXPlayer` — tap ticks, impact layers, UI sounds.
- `ImpactScheduler` — schedules impact audio backwards from a predicted `contactDsp`.
- Content loading and memory management.

**Explicitly does NOT do**
- Own the music composition → content is authored externally, this module plays it.
- Own lighting pulses → M14 reads `IBeatSource`.
- Own VFX timing → M15 reads `IBeatSource`.
- Own the mixer volume settings UI → M10 (if a settings screen exists).

## 3. Public API surface

| Member | Signature | Notes |
|---|---|---|
| `AudioSystem.Init` | `void Init(AudioConfig cfg, DeviceTier tier)` | Loads mixer, starts BeatClock |
| `IBeatSource.CurrentBeat` | `double { get; }` | Fractional beat count from `dspTime` |
| `IBeatSource.CurrentBar` | `int { get; }` | 4/4 bars elapsed |
| `IBeatSource.BPM` | `float { get; }` | 140 in v1 |
| `IBeatSource.OnBeat` | `event Action<int>` | Fires on each beat (1–4) |
| `IBeatSource.OnBar` | `event Action<int>` | Fires on bar boundaries |
| `IBeatSource.NextBeatDsp` | `double { get; }` | DSP time of the next beat — used for scheduling |
| `MusicDirector.SetSection` | `void SetSection(MusicSection section)` | Quantized to next bar boundary |
| `IntensityDriver.Intensity` | `float { get; }` | 0–1, updated per signal |
| `VODirector.Queue` | `void Queue(VOLine line, VOPriority priority)` | Priority queue, ducks music |
| `CrowdController.React` | `void React(CrowdReaction reaction)` | One-shot crowd burst |
| `SFXPlayer.PlayImpact` | `void PlayImpact(ImpactTier tier, double scheduledDsp)` | Pre-scheduled |
| `SFXPlayer.PlayTapTick` | `void PlayTapTick()` | During charge |
| `ImpactScheduler.ScheduleImpact` | `void ScheduleImpact(double contactDsp, ImpactTier tier)` | Called at WindupStart |

**Consumed by:** M01 (signals drive everything), M08 (impact timing), M14/M15/M16 (beat source).
**Stability:** mixer tree is stable; content and scheduling will churn.

## 4. Configuration

Asset: `Assets/_Project/Data/Audio/AudioConfig.asset`

### BeatClock

| Field | Type | Default | Notes |
|---|---|---|---|
| `bpm` | float | **140** | F minor in v1 |
| `beatsPerBar` | int | **4** | 4/4 time |
| `dspBufferSize` | int | **512** | `AudioSettings.SetDSPBufferSize(512, 512)` — 10.7 ms at 48 kHz |
| `sampleRate` | int | **48000** | Forced via `AudioSettings` |
| `stuckClockTimeout` | float s | **2.0** | If `dspTime` does not advance for this long, force-restart the audio session |

### Music

| Field | Type | Default | Notes |
|---|---|---|---|
| `stemCount` | int | **3** | Core, Melody, Hype |
| `sectionsPerTrack` | int | varies | Each section is one loop (8 or 16 bars) |
| `crossfadeBars` | int | **1** | Section transitions crossfade over 1 bar |
| `melodyFadeInIntensity` | float | **0.3** | Melody stem fades in above this intensity |
| `hypeFadeInIntensity` | float | **0.6** | Hype stem fades in above this intensity |

Three stems run simultaneously as `AudioSource` components, volume-controlled by `IntensityDriver`:
- **Core** — always playing, base groove (drums + bass).
- **Melody** — fades in at mid intensity.
- **Hype** — fades in at high intensity (brass hits, risers).

Section changes are bar-quantized via `PlayScheduled(nextBarDsp)` — the current section fades out over `crossfadeBars` while the next fades in. No audible gap, no beat skip.

### VO Director

| Field | Type | Default | Notes |
|---|---|---|---|
| `maxQueueDepth` | int | **3** | Lines beyond this are dropped |
| `minLineSeparation` | float s | **1.5** | Minimum gap between VO lines |
| `duckVolume` | float dB | **-12** | Music ducks by this during VO |
| `duckAttack` | float s | **0.05** | |
| `duckRelease` | float s | **0.3** | |
| `antiRepeatBagSize` | int | **4** | Bag-shuffle — a line cannot repeat within the last N plays |

VO lines are **carrier + name** stitched: "AND THE CHALLENGER IS..." + "[FIGHTER NAME]". The carrier is a full AudioClip; the name is a short clip played immediately after with a 50 ms overlap. This avoids needing to record every combination.

### Crowd

| Field | Type | Default (Mid) | Notes |
|---|---|---|---|
| `ambientBedCount` | int | **2** | Looping crowd ambience layers (one "murmur", one "cheer") |
| `reactionClipCount` | int | **6** | One-shot "ooh!", "gasp", "roar" clips, randomly selected |
| `cheerVolumeByIntensity` | curve | 0.2 @0 → 0.5 @0.5 → 1.0 @1.0 | Crowd gets louder as the match heats up |

### Impact layering

Each impact is a **five-layer recipe**, mixed per-tier:

| Layer | Purpose | Per-tier variation |
|---|---|---|
| Body | Low thud, the physical mass | Louder on high tier |
| Skin | Mid slap crack | Present on all tiers |
| Sweetener | High-end transient, the "snap" | Only on crit/finisher |
| Sub | Sub-bass rumble, felt more than heard | Only on crit/finisher |
| Reverb tail | Arena reverb send | Longer on high tier |

Each layer has independent per-layer **pitch randomization** (±5%) and **millisecond timing offsets** (skin leads body by 2 ms for perceptual sharpness). All five layers are `PlayScheduled` at the same `contactDsp` (adjusted by their individual offsets).

### Tap tick

Solved as **2 loops + ≤2 decimated accents = ≤5 voices**, not 20–38 one-shots:
- `TickLoop` — a short rhythmic loop at 140 BPM, volume-controlled by `PowerMeter.Fill`.
- `AccentLoop` — a second loop at half the rate, triggered by `phaseShape > 1.2` (the late-phase crescendo).
- Up to 2 accent one-shots on specific taps (every 4th tap) for variation.

This avoids voice exhaustion during rapid tapping.

### Mobile budget

| Resource | Budget | Measured with |
|---|---|---|
| Real voices (polyphony) | **28** | `AudioSettings.GetConfiguration().numRealVoices` |
| Virtual voices | **40** | AudioSource pool |
| DSP CPU (one core) | **≤ 10%** | Audio Profiler module |
| Audio RAM | **≤ 20 MB** | Memory Profiler |
| DSP buffer | **512 samples** | 10.7 ms latency at 48 kHz |

### Three-tier quality fallback

| Tier | Music | Crowd | Reverb | DSP buffer |
|---|---|---|---|---|
| High | 3 stems, section changes | 3 beds, reverb send | Full | 512 |
| Mid | 3 stems, section changes | 2 beds, reverb send | Full | 512 |
| Low | Single pre-mixed loop | 2 beds, reverb bypassed | Bypassed | 1024 |

## 5. Events

**Emits**

| Event | Payload | When |
|---|---|---|
| `BeatFired` | `int beatIndex` | Every beat (for debug/sync verification) |
| `VOLineStarted` | `VOLine` | VO playback begins |
| `VOLineEnded` | `VOLine` | VO playback ends |

**Consumes**

| Event | From | Reaction |
|---|---|---|
| `MatchSignal.BoutIntroStart` | M01 | Start music (Core stem), queue intro VO |
| `MatchSignal.TurnStart` | M01 | Queue turn VO ("Player's turn!") |
| `MatchSignal.ChargeStart` | M01 | Start tick loops |
| `MatchSignal.ChargeEnd` | M01 | Stop tick loops |
| `MatchSignal.WindupStart` | M01 | `ScheduleImpact(contactDsp, tier)` |
| `MatchSignal.ImpactFrame` | M01 | Crowd reaction, update intensity |
| `MatchSignal.FinisherStart` | M01 | Music stinger, crowd roar, VO ("FINISH HIM!") |
| `MatchSignal.KO` | M01 | Victory stinger, crowd eruption |
| `MatchSignal.BoutEnd` | M01 | Fade music, queue outro VO |
| `HealthChanged` | M06 | Update intensity driver |

## 6. State machine

Audio is reactive, not FSM-driven. Individual subsystems have their own simple states:

- **MusicDirector:** `Stopped → Playing → FadingSection → Playing → ...`
- **VODirector:** `Idle → Playing → Cooldown → Idle`
- **BeatClock:** always running once started (until bout end).

## 7. Dependencies

- **Hard:** `SlapMania.Core` (signals), `SlapMania.Data` (`AudioConfig`).
- **Soft:** M03 (tap events for tick), M08 (impact timing).
- **Unity APIs:** `AudioSource`, `AudioMixer`, `AudioSettings`, `AudioClip`, `AudioListener`.
- **Must initialize after:** M00 (config + tier).

## 8. Failure modes

| ID | Failure | Detection | Behaviour | Player-visible? |
|---|---|---|---|---|
| S-050 | `dspTime` stops advancing (iOS audio session death) | `stuckClockTimeout` watchdog | Force `AudioSession` restart, reset BeatClock, log Error | Brief audio dropout |
| S-051 | iOS silent switch — no audio | `AVAudioSession.outputVolume == 0` check at boot | Show a muted icon; do not treat as an error | Yes — user's choice |
| S-052 | Voice exhaustion (>28 real voices) | `AudioSettings.dsp` profiler or voice overflow counter | Lowest-priority voices are virtualized (Unity does this automatically) | Possible missing crowd murmur |
| S-053 | Impact audio arrives late (Android output latency) | Debug overlay: `contactDsp - actualPlayDsp` delta | `ImpactScheduler` schedules backwards from `contactDsp`; if still late, the latency model needs recalibration | Late slap sound |
| S-054 | Music section change not bar-quantized | Beat misalignment audible | `PlayScheduled` at `NextBarDsp` — if called mid-bar, waits for the boundary | No if scheduled correctly |
| S-055 | VO queue overflow | `maxQueueDepth` exceeded | Drop lowest-priority line, log Info | Missing VO line |
| S-056 | Mixer snapshot transition incomplete | Timer watchdog | Force-complete after 2× expected duration | Brief volume pop |

## 9. Debug hooks

- **Log channel:** `LogChannel.Audio`
- **Overlay (Audio tab):** `dspTime`, current beat/bar, BPM, active voice count, intensity, current music section, VO queue contents, last impact schedule (contactDsp, tier, actual play time, delta), per-layer volumes, DSP CPU %.
- **Cheats:** force intensity (slider), play any VO line, trigger any crowd reaction, force section change, mute individual stems, force low-tier audio.
- **Recorder events:** `BeatFired`, `SectionChanged`, `VOPlayed`, `ImpactScheduled`, `ImpactPlayed`.
- **Editor tools:** `BeatClockMonitor` — shows the beat waveform and beat markers in real-time during Play Mode.

## 10. Tuning notes

**`dspTime` is the only clock for audio, ever.** `Time.time` is frame-rate-dependent. At 30 fps it ticks in 33 ms steps. At 60 fps it ticks in 16 ms steps. Music at 140 BPM has a beat every 428 ms — a 33 ms jitter is 7.7% of a beat, which is audible as "sloppy." `dspTime` advances at sample accuracy regardless of frame rate. The BeatClock reads `dspTime` every frame and computes the current beat position from it. If `dspTime` stops advancing (iOS audio session killed in background), the watchdog fires.

**Impact scheduling works backwards.** At `WindupStart`, M01 publishes a predicted `contactDsp` — the DSP time when the slap will land. `ImpactScheduler` calls `AudioSource.PlayScheduled(contactDsp + layerOffset)` for each of the five impact layers. The audio hardware then plays them at the right instant regardless of frame timing. This is the only way to beat Android's 50–150 ms output latency: you cannot react to the impact frame and play audio *then* — by the time you call `Play()`, the audio emerges 50–150 ms later and the slap feels disconnected. Pre-scheduling eliminates that delay entirely.

**The `contactDsp` prediction must be accurate to within 10 ms.** It is computed as `currentDsp + windupAnimationDuration`, where `windupAnimationDuration` comes from `ISlapTimingSource` (M08). If the animation is shortened or lengthened by hitstop or blend times, the prediction drifts and audio lands early or late. The prediction is logged and compared to the actual impact frame's `dspTime` in every match recording — a delta above 15 ms triggers a Warn.

**Carrier + name stitching is the VO scaling strategy.** Recording "And the challenger is [X]" for 20 characters = 20 clips. Recording "And the challenger is..." (1 carrier) + 20 name clips = 21 clips. With 10 carriers and 20 names, you get 200 combinations from 30 clips. The 50 ms overlap hides the seam.

**The tap tick is a loop, not a one-shot per tap.** At 12 tps, one-shot ticks consume 12 voices. Over a 2.5 s charge phase, that is 30 `Play()` calls, each with its own AudioSource or pooled clip. The loop approach uses 2 voices (tick + accent), volume-modulated by fill. The variation comes from the accent loop and the periodic one-shots, not from per-tap triggers.

## 11. Open questions

- [ ] Should the BeatClock support tempo changes (e.g. accelerando in late rounds), or is a fixed 140 BPM sufficient? Accelerando is dramatic but makes stem scheduling much harder.
- [ ] Is Ogg Vorbis the right codec for mobile, or should we use ADPCM for lower-quality clips (crowd, sub impacts) to save decode CPU?
- [ ] Should the VO director pause music or duck it? Current design ducks by -12 dB, but pausing the music for big announcements could be more impactful.
- [ ] How does the iOS silent switch interact with haptics? If audio is muted, should we increase haptic intensity to compensate?

## 12. Changelog

| Date | Commit | Change |
|---|---|---|
| 2026-09-19 | (pre-git) | Created (Draft) with full mixer, BeatClock, VO, crowd, impact scheduling, mobile budget |
