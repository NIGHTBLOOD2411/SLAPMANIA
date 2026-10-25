# Slap Mania — Performance Baseline

Status: Draft — Last verified 2026-09-19 — No captures yet; the project has no code.

## How to measure

**In a Development build with `SLAPMANIA_DEBUG` off, on hardware. Never in the Editor.** Editor numbers are meaningless for a mobile frame budget.

Capture a baseline **before** optimizing and re-capture at every milestone. Without a dated series, "is it slower than last month?" is unanswerable.

Two named reference devices, filled in when they are chosen:

| Role | Device | Chipset / GPU | OS | Screen |
|---|---|---|---|---|
| **Mid-tier** — the honest target | *(TBD)* | | | |
| **Min-spec** — the cheapest supported | *(TBD)* | | | |

## Targets

| Metric | Where | Mid-tier target | Min-spec floor | Measured with |
|---|---|---|---|---|
| Frame time, idle duel | Charge / Timing | ≤ 12 ms (60 fps with headroom) | ≤ 30 ms (33 fps) | On-device Profiler |
| Frame time, tap spam at 15 taps/s | Charge phase | ≤ 14 ms, **no frame > 20 ms** | no frame > 40 ms | Overlay min/avg/max + Profiler |
| Frame time, impact frame | 3 frames around contact | ≤ 20 ms | ≤ 45 ms | Profiler capture around `DamageComputed` |
| Hitches > 50 ms | Whole match | **0** | ≤ 1 | `FrameHitch` events in the recorder |
| GC alloc per frame | Charge and Timing | **0 B** | 0 B | Profiler GC Alloc column |
| GC alloc per slap | Turn start to end | ≤ 4 KB | ≤ 8 KB | Memory Profiler diff |
| Managed heap, steady | Mid-match | ≤ 90 MB | ≤ 60 MB | Memory Profiler |
| Total app memory | Mid-match | ≤ 400 MB | ≤ 300 MB (2 GB device) | `adb shell dumpsys meminfo` / Xcode |
| SetPass calls | Arena + HUD | ≤ 40 | ≤ 40 | Frame Debugger / rendering stats |
| Batches | Arena + HUD | ≤ 150 | ≤ 150 | Stats window, verified on device |
| Overdraw | Arena with full VFX | ≤ 3× screen | ≤ 2× | Overdraw view; **this, not particle count, is what kills tile GPUs** |
| Snapshot capture | Per slap | ≤ 3 ms GPU, ≤ 1 ms CPU | ≤ 6 ms GPU | Render Graph Viewer + Profiler GPU module |
| Snapshot memory | Full 16-tile atlas | 15.5 MiB (Mid tier) | 6.9 MiB (Low tier) | Memory Profiler |
| Audio DSP CPU | One core | ≤ 10 % | ≤ 14 % | Audio Profiler module |
| Audio RAM | In-bout | ≤ 20 MB | ≤ 20 MB | Memory Profiler |
| Audio voices | Finisher peak | ≤ 28 | ≤ 28 | Overlay Audio tab |
| Canvas rebuilds per frame | HUD during Charge | **0** | 0 | Profiler UI module |
| Input to visual latency | Tap to bar movement | ≤ 2 frames | ≤ 2 frames | High-speed camera, or `Tap` → `PowerChanged` Δt in the recorder |
| Cold start to main menu | Launch | ≤ 3 s | ≤ 5 s | `adb logcat` timestamps |
| Build size (AAB) | Release | ≤ 80 MB | — | Build Report |

## Frame budget at 60 fps (16.6 ms), mid-tier

Provisional. The arena spectacle numbers are estimates pending the M14/M15/M17 design passes, and the whole table must be re-derived from a real capture at P8.

| Component | Budget |
|---|---|
| Opaque geometry — ring, arena, two fighters | 3.5 ms |
| Crowd | 1.0 ms |
| Transparents and particles | 3.0 ms |
| Post-processing (bloom, tonemap, vignette) | 2.0 ms |
| Arena displays / jumbotrons | 1.5 ms |
| UI canvas | 1.0 ms |
| Scripts, simulation, audio mixing | 2.0 ms |
| Headroom | 2.6 ms |

The **photo camera render (≈ 2–5 ms)** is deliberately excluded: it fires only during the hard freeze, on a frame the player perceives as frozen. Capturing during hitstop is not merely convenient — it is the only place where that cost is free.

## Device tiers

`DeviceTierDetector` picks a tier at Boot from `SystemInfo.systemMemorySize`, `graphicsDeviceType` and `processorCount`, plus a rolling frame-time watchdog over the first 20 s of the first bout.

| Tier | Gate | Snapshot | Audio | Post / VFX |
|---|---|---|---|---|
| **High** | ≥ 4 GB RAM and Vulkan and ≥ 6 cores | 512×640 tiles, 27.5 MiB | 3 music stems, 3 crowd beds, reverb, DSP 512 | Full |
| **Mid** (default) | — | 384×480 tiles, 15.5 MiB | 3 stems, 2 crowd beds, reverb, DSP 512 | Chromatic aberration off |
| **Low** | ≤ 4 cores or < 3 GB RAM | 256×320 tiles, 6.9 MiB, or Off | Single pre-mixed loop, 2 crowd beds, reverb bypassed, DSP 1024 | Bloom only; impact Volume off |

## Capture log

Dated capture reports go in `Docs/Perf/Captures/YYYY-MM-DD-<device>-<build>.md`, each holding the device, build id, the targets table filled in, and a link to the raw `.data` profiler file (stored outside the repo — those are gitignored).

| Date | Device | Build | Notes |
|---|---|---|---|
| 2026-09-26 | **Editor only** (Windows, PC_RPAsset, Game view 437×778) | Proto_Bout, generated arena | Not a device capture and not comparable to the targets. First data point: [2026-09-26-editor-arena.md](Captures/2026-09-26-editor-arena.md). Batches 264 avg / SetPass 75 avg / 2.18 M tris, all cameras. Fighter meshes are ~175k tris each |
