# ADR-0017 — Mash carry-over: an AIM cue and a spam gap in the timing meter

**Status:** Accepted
**Date:** 2026-10-23
**Deciders:** shobhit

## Context

Owner report (DEBUG S-093): the timing multiplier "gets consumed as we are spamming to charge the first meter". In TapMarker the charge phase asks for mashing, and it ends on a clock the player does not control. The timing meter ignored taps only during `armDelay` (0.15 s). People take about 250 ms to notice a change on screen and stop, so the next mash tap committed the marker at a random spot. That made the player's multiplier noise. The AI aims (M07), so it looked "too powerful". DEBUG S-013 predicted this exact bug, and its 0.15 s fix was too short.

The owner chose the "AIM beat + gate" option over a gap gate alone and over a longer arm delay.

## Decision

Two mash guards in `TimingMeter`, authored in ms in `TimingPhaseConfig` (R8):

1. **AIM cue** (`aimCueMs`, 400). The meter begins, the ring is shown, and the marker holds at its start position for the cue. Then it runs. The arm delay counts from the moment the marker starts. `TimingMeter.InCue` drives HUD copy ("AIM · STOP MASHING"). The marker stays a pure function of elapsed time (ADR-0004): `elapsed = max(0, now − markerStart)`.
2. **Spam gap** (`spamGapMs`, 220). A commit counts only if the press before it was at least the gap earlier. That includes the attacker's last charge tap, passed in as `PowerMeter.LastTapTime`. Every press moves the gap on, rejected or not, so a finger that keeps mashing never stops the marker. If the player mashes through the whole phase, the timeout resolves it as before.

Also: a press whose event time is before the meter began never commits, however late it is delivered (S-096).

The AI goes through the same gate (M07: one code path). `BeginTiming` gets the cue and `TimingMeter.EarliestCommitElapsed`, and never aims earlier than that. This also closes the S-088 tuning trap.

**Implemented inside `TimingMeter`, not as a new `MatchState`.** The cue needs the meter to see taps (so a masher's taps during the cue count toward the gap) and the HUD to show the frozen ring. A separate FSM state would have needed a second tap subscription in `MatchDirector` and a HUD state for the in-between. `CurrentState` reads `Timing` throughout the cue.

**Rejected: raise `armDelay` to ~0.45 s.** One number, but it slows every turn by 300 ms, and a player who mashes past it still commits by accident.

**Rejected: spam gap alone.** It works, but it gives no visual beat that says "the phase changed". The cue is the readable part, and the gap is the guarantee.

## Consequences

- Each TapMarker timing phase is 400 ms longer (cue), and the shot clock (3 s timeout) counts from the end of the cue.
- A player who taps twice within 220 ms in the timing phase has the second tap ignored. That is the intended trade.
- `TestFactories.TimingCfg()` turns both guards off (0 ms) so older tests keep their timing. New tests opt in.
- Hold ring mode is unaffected (a hold is one deliberate press).
