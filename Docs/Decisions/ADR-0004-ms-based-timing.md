# ADR-0004 — Timing authored in milliseconds, never accumulated

**Status:** Accepted
**Date:** 2026-09-19
**Deciders:** shobhit

## Context

The timing multiplier's marker sweeps across a bar at ~1 traversal per second. The red zone is 60 ms wide. If the marker position is accumulated frame-by-frame (`pos += speed * dt`), floating-point error accumulates over time: after 3 seconds at 60 fps (180 additions), the error can reach ~1e-5, which is 0.001% of the bar — negligible. But at 30 fps with frame-time jitter (±5 ms), the error compounds non-linearly and the marker drifts by up to 0.1% per pass. On a 60 ms zone, 0.1% of 1000 ms = 1 ms of drift — small but measurable, and it invalidates the latency compensation model.

More critically, accumulated position makes replays non-deterministic: the same sequence of `dt` values can produce different positions depending on floating-point ordering. And it makes the debug overlay unreliable: the "position at event time" computation requires replaying all `dt` values from zero, which is expensive and error-prone.

## Decision

The marker position is a **pure function of elapsed time**:

```csharp
float PositionAt(double elapsed, in MarkerConfig cfg)
{
    double phase = (cfg.startPhase + elapsed * cfg.sweep) % 2.0;
    return (float)(phase <= 1.0 ? phase : 2.0 - phase);
}
```

`elapsed` is `double`, computed as `currentTime - phaseStartTime`. Position is never stored; it is recomputed on every access. This is ~5 ns per call.

All timing values in config files are authored in **seconds** (Unity convention) but with **millisecond precision** in the documentation and tuning notes (e.g., "the red zone is 60 ms wide"). Comments and docs use milliseconds because humans reason about reaction times in milliseconds, not fractions of a second.

## Consequences

### Positive

- **Deterministic.** Same `elapsed` → same position, always.
- **Replayable.** No state to accumulate; feed the recorded `elapsed` and get the exact position.
- **Debuggable.** "What was the position when this tap arrived?" is `PositionAt(tapTime - phaseStart, cfg)` — no replay needed.
- **Latency compensation is trivial.** `PositionAt(elapsed - compensation, cfg)` gives the position the player saw when they tapped.

### Negative

- **Recomputation cost.** Position is computed every frame and every tap, not cached. At ~5 ns, this is negligible.
- **No easing.** A pure function of time cannot have ease-in/ease-out without adding parameters. The marker is linear (ping-pong), which is correct for this game.

### Risks

- None identified. The pure-function approach is strictly superior for this use case.

## Alternatives considered

| Alternative | Why rejected |
|---|---|
| Accumulated `pos += speed * dt` | Drift, non-deterministic replays, unreliable latency compensation |
| `AnimationCurve`-driven position | Overkill for a linear ping-pong; harder to invert for `NextTimeAtPosition` |
| Fixed-point arithmetic | Unnecessary precision for a 60 ms zone; adds complexity |
