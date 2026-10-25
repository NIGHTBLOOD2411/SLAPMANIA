---
module: M07-OpponentAI
status: InProgress
owner: shobhit
last-verified: 2026-10-23
verified-against: (pre-git)
asmdef: SlapMania.Gameplay
---

# M07 — OpponentAI

> **One-liner:** Feeds synthetic taps into the same `ITapSource` interface the player uses, so the charge and timing meters cannot distinguish human from AI.

## 1. Purpose

The AI is not a separate code path. It is a tap source. When it is the AI's turn, `AiTapSource` replaces `InputSystemTapSource` as the active `ITapSource` on the power and timing meters. M03 and M04 run identically — same math, same state machines, same debug output. The only difference is where the taps come from.

This is the single most important architectural decision in the AI module: **one code path for player and AI.** It means every balance fix, every latency tweak, every debug tool works for both. And it means the AI visibly plays the same game the player does — you can watch its power bar fill and its timing marker stop, which makes it feel honest even when it is tuned to lose.

## 2. Responsibilities and non-goals

**Owns**
- `AiTapSource : ITapSource` — generates synthetic `TapEvent` structs.
- The tap-rate profile for the charge phase (how fast and how consistently the AI taps).
- The timing aim for the timing phase (which zone the AI targets and how precisely).
- Per-round difficulty scaling via `AiProfile` ScriptableObjects.

**Explicitly does NOT do**
- Run the power meter → M03 runs it, consuming taps from `AiTapSource`.
- Run the timing meter → M04 runs it, consuming the commit tap from `AiTapSource`.
- Animate the AI's body → `M08-Presentation`'s `IFighterView` drives the visual.
- Decide turn order → `M01`.

## 3. Public API surface

| Member | Signature | Notes |
|---|---|---|
| `AiTapSource` | implements `ITapSource` | Drop-in replacement for `InputSystemTapSource` |
| `AiTapSource.Configure` | `void Configure(AiProfile profile)` | Called once per bout, by `MatchDirector.BindOpponent`, with the profile of the tier the ladder rolled (§4b). It was never called per turn (S-095) |
| `AiLadder.RollTier` | `static int RollTier(int level, int tierCount, IRandom rng)` | **2026-10-23 ([ADR-0018](../Decisions/ADR-0018-ai-ladder.md)).** Pure. This bout's AI tier, in range and at most one tier from the level |
| `AiLadder.NextLevel` | `static int NextLevel(int level, bool playerWon, int tierCount)` | Pure. Win +1, loss (including a forfeit) −1, clamped |
| `GameConfig.ProfileForTier` | `AiProfile ProfileForTier(int tier)` | Replaces `ProfileForRound`. 0-based, clamped |
| `AiTapSource.BeginCharge` | `void BeginCharge()` | Starts generating taps at the profile's rate |
| `AiTapSource.BeginTiming` | `void BeginTiming(double startPhase, double sweep, TimingZoneLayout layout, float zoneScale, double timeout, double displayLatencyBias, double markerStartsIn = 0, double earliestCommit = 0)` | Schedules one commit with `MarkerOscillator.NextTimeAtPosition`, `+ displayLatencyBias` (S-079). Since 2026-10-23 every time is counted from the end of M04's AIM cue (`markerStartsIn` = `TimingMeter.CueSeconds`). The minimum lead is max(`reactionDelay`, `earliestCommit − bias`), where `earliestCommit` = `TimingMeter.EarliestCommitElapsed`, so the AI never aims at a commit the meter would reject (S-088 trap closed, S-093). It waits for a second pass only if that pass lands before `timeout` (S-097) |
| `AiTapSource.Tick` | `void Tick(double dt, double currentTime)` | Generates taps on schedule |
| `AiTapSource` | implements `IHoldSource` | **Landed 2026-10-23.** Also raises `OnRelease` for the hold ring |
| `AiTapSource.BeginHoldRing` | `void BeginHoldRing(TimingZoneLayout layout, float zoneScale, double closeSeconds, double armDelay, double displayLatencyBias)` | **Landed 2026-10-23** ([ADR-0014](../Decisions/ADR-0014-hold-ring-attack-mode.md)). Schedules one press and one release |

**Consumed by:** M01 (wires it to the meters on the AI's turn), M03 and M04 (indirectly, via `ITapSource`).
**Stability:** churning until playtests.

## 4. Configuration

Asset: `Assets/_Project/Data/AI/AiProfile_Round{N}.asset` (one per difficulty level, indexed by M01)

| Field | Type | Default (Round 1) | Round 5 | Round 10 | Notes |
|---|---|---|---|---|---|
| `targetTps` | float | **6.0** | 8.0 | 11.0 | Mean taps per second during charge |
| `tpsVariance` | float | **0.15** | 0.10 | 0.05 | Gaussian noise σ as fraction of `targetTps`. High = sloppy. |
| `tapFatigue` | float | **0.08** | 0.05 | 0.02 | TPS drops this fraction over the charge phase. Simulates thumb tiring. |
| `timingAimZone` | `TimingZone` | **Good** | Great | Perfect | The zone the AI *targets*. Not the zone it hits. |
| `aimSigma` | float | **0.08** | 0.05 | 0.025 | Gaussian σ in normalized bar position around the target. Determines how often it misses. |
| `reactionDelay` | float s | **0.12** | 0.08 | 0.04 | Marker: the minimum lead from the phase start; the AI commits on the first crossing at least this late. Ring: added after the arm delay before the press. Simulates human reaction. |
| `commitOnFirstPass` | float | **0.6** | 0.7 | 0.9 | Probability of committing on the first marker pass through the target zone vs. waiting for the second. |

**Invariants**
- `targetTps` in [3, 15] — below 3 the AI looks broken, above 15 it is superhuman.
- `aimSigma > 0` — at exactly 0 the AI never misses and the game is unwinnable.
- `reactionDelay >= 0`.
- ~~**`reactionDelay + displayLatencyBias >= armDelay`**~~ No longer needed since 2026-10-23: the AI takes M04's `EarliestCommitElapsed` as a floor. The original note follows.
- **`reactionDelay + displayLatencyBias >= armDelay`** (M04's `TimingPhaseConfig`). Below that, the marker AI can aim at a crossing inside the arm delay. The meter rejects the commit, the AI does not try again, and the turn times out ([DEBUG S-088](../DEBUG.md)). The shipped values sit exactly on the edge: 0.12 + 0.03 = 0.15. The round-5 and round-10 values in the table above (0.08, 0.04) break it. Not enforced in code yet.

### 4b. The AI ladder ([ADR-0018](../Decisions/ADR-0018-ai-ladder.md), 2026-10-23)

`GameConfig.aiProfiles` holds the **AI tiers**, easiest first. They are no longer indexed by round. The player's **ladder level** (`GameRoot.AiLadderLevel`, session only until M12) picks the bout's tier through `AiLadder.RollTier`. `BoutController` rolls it at bout start from the bout's seeded RNG and logs the seed, level and tier. At `BoutEnd`, `AiLadder.NextLevel` moves the level. `AiProfile.displayName` is the opponent's HUD name.

Assets in `Data/Prototype/`, created by `PrototypeSceneBuilder.EnsureAiLadder` (values in `LadderTiers`; an existing asset keeps its tuned values):

| Tier | `displayName` | `targetTps` | `tpsVariance` | `tapFatigue` | `timingAimZone` | `aimSigma` | `reactionDelay` | `commitOnFirstPass` | Sim: AI damage / slap |
|---|---|---|---|---|---|---|---|---|---|
| 1 | ROOKIE | 3.5 | 0.25 | 0.20 | Weak | 0.10 | 0.30 | 0.40 | 10.7 |
| 2 | BRAWLER | 4.5 | 0.20 | 0.14 | Good | 0.12 | 0.24 | 0.50 | 23.1 |
| 3 | CONTENDER | 5.5 | 0.15 | 0.10 | Good | 0.09 | 0.18 | 0.60 | 28.2 |
| 4 | PRO | 6.5 | 0.12 | 0.06 | Great | 0.07 | 0.14 | 0.70 | 38.1 |
| 5 | CHAMPION | 8.0 | 0.10 | 0.04 | Perfect | 0.045 | 0.12 | 0.80 | 44.9 |

The last column comes from an Editor `execute_code` sim through `MatchDirector`, with `Resources/GameConfig` and the shipped assets: 40 bouts per tier, the first 8 slaps, 2026-10-23. It ran before the S-097 fix, so ROOKIE's figure includes 7 timeouts in 160 turns and is slightly low. The old `Ai_Round1` (6 tps, Good, σ 0.08) sat at about CONTENDER. It is no longer referenced.

### How the AI aims

The timing commit works like this:

1. At `BeginTiming`, the AI picks a target position: the centre of `timingAimZone`, offset by `Normal(0, aimSigma)`, clamped to 0.02..0.98, then scaled by the round's zone scale.
2. It calls `MarkerOscillator.NextTimeAtPosition(startPhase, sweep, target, minLead: reactionDelay, maxLookahead: timeout)` to get the first crossing at least `reactionDelay` after the phase starts. With no crossing before the timeout, it commits at half the timeout instead.
3. If the `commitOnFirstPass` roll fails, it adds one traversal (`1 / sweep`) and commits on a later pass.
4. It adds `displayLatencyBias` (since 2026-10-23, [DEBUG S-079](../DEBUG.md)). The meter back-dates every commit by that bias (M04 §5) for a player watching a screen, so without it the AI is judged 30 ms before the crossing it aimed at.
5. On the `Tick` that crosses that time, it fires a `TapEvent` stamped at the scheduled time, `sourceId = 2` (AI). The meter judges arming on that stamp, so a long frame cannot drop it ([DEBUG S-087](../DEBUG.md)).

**Hold ring** (`BeginHoldRing`, M04 §5b): the same aim point becomes a closure. The AI presses `armDelay + reactionDelay` after the ring opens and releases `HoldRing.HeldSecondsAt(closure, close) + displayLatencyBias` later. That is a closed-form inverse, with no crossing search and no second pass. The bias is there for the same reason as step 4. Both attack modes now apply it.

The result is a bell curve of outcomes centred on the target zone. **Measured 2026-10-23** (2000 simulated marker turns, `Ai_Round1.asset` plus the shipped `TimingPhase`/`TimingZoneLayout` assets loaded in the Editor, 60 fps, rounds 1 and 5 identical): the round-1 AI (Good, `aimSigma = 0.08`) lands **Good 54%**, Great 15.5%, Perfect 7.2%, Weak 23%, Miss 0.15%, and never times out, for a mean multiplier of **×1.478**. The design target written before code ("Perfect about 15%, Good about 40%") was for this profile and is not what it does. Round 10 (`aimSigma = 0.025`, Perfect ~60%) is still a prediction, because no round-10 profile exists yet.

## 5. Events

**Emits**

Taps via `ITapSource.OnTap` — same event the player fires.

**Consumes**

| Event | From | Reaction |
|---|---|---|
| `MatchSignal.TurnStart` | M01 | Configure for this turn's profile |
| `MatchSignal.ChargeStart` | M01 | `BeginCharge()` |
| `MatchSignal.TimingStart` | M01 | `BeginTiming()` |

## 6. State machine

```
Idle --Configure--> Ready --BeginCharge--> Charging --ChargeEnd--> Ready
                                                                     |
                                                              BeginTiming
                                                                     |
                                                                  Aiming --commit--> Done --> Idle
```

| State | What happens |
|---|---|
| `Idle` | No taps generated |
| `Ready` | Profile loaded, waiting for M01 to start a phase |
| `Charging` | Generating taps at `targetTps ± noise` with fatigue decay |
| `Aiming` | Waiting for the marker to reach the target position |
| `Done` | Commit tap fired, waiting for M01 to end the timing phase |

## 7. Dependencies

- **Hard:** `SlapMania.Core` (`ITapSource`, `TapEvent`), `SlapMania.Data` (`AiProfile`), M04 (`TimingMeter.NextTimeAtPosition`).
- **Soft:** none.
- **Unity APIs:** none — uses a seeded `System.Random` for reproducibility.
- **Must initialize after:** M00.

## 8. Failure modes

| Failure | Detection | Behaviour | Player-visible? |
|---|---|---|---|
| `NextTimeAtPosition` returns no valid time before timeout | Guard — returns timeout instant | AI taps at a random-ish time, likely a mediocre zone | Weak AI turn, not a hang |
| Profile null | Assert | Fall back to round 1 profile | Easier-than-expected AI |
| Seeded RNG desync in replay | Recorder stores the seed | Deterministic from the same seed | No |
| AI taps faster than `minTapInterval` | M03 rejects the excess taps | The AI's effective fill is slightly lower than its profile suggests | No — indistinguishable from human rejection |

## 9. Debug hooks

- **Log channel:** `LogChannel.AI`
- **Overlay (AI tab):** target TPS vs actual TPS, current fatigue factor, timing target position, scheduled commit time, aim offset from zone centre, the zone it thinks it is targeting, the zone it actually hit.
- **Cheats:** force AI profile (dropdown), force AI perfect, force AI miss, AI plays the player's turn (watch mode).
- **Recorder events:** `AiTapGenerated` (with target TPS and noise), `AiTimingScheduled` (with target position, scheduled time, actual commit time).
- **Editor tools:** `AiProfilePreview` — given a profile, runs 1000 simulated turns and shows a histogram of zone hits and average power. This replaces playtesting for gross balance checks.

## 10. Tuning notes

**Since the ladder (2026-10-23), "round 1" and "round 10" in the two notes below read as "ladder level 0" and "the top tier".** A new player meets ROOKIE or BRAWLER. Every win moves them one tier closer to CHAMPION.

**The AI must be beatable on round 1 by a player who has never seen the game.** That means `targetTps = 6` (a casual thumb), `aimSigma = 0.08` (misses often), and `reactionDelay = 0.12` (sluggish). The AI should feel like a clumsy opponent, not a rubber-banded handicap. If a first-time player loses round 1, the AI is too hard.

**The AI must feel unbeatable on round 10 without literally being unbeatable.** `aimSigma = 0.025` at round 10 means the AI hits Perfect about 60% of the time. A skilled player hits Perfect maybe 25% of the time. But the player can read the AI's pattern (it always targets the same zone), and the player's 3.0× crits compound with the escalation curve, so a skilled player who lands two Perfects in a row can still win. The skill ceiling must remain above the AI's ceiling.

**`commitOnFirstPass` is the most subtle tuning lever.** At 0.6, the round-1 AI often waits for the second pass through the target zone, which gives the player more time to watch the AI's bar and more visual drama. At 0.9, the round-10 AI commits instantly when it sees the zone, which reads as confidence. This is purely about *feel*, not about the zone it hits — that is `aimSigma`'s job.

**The 2026-10-23 bias fix changed the shipped AI's feel slightly.** Every marker commit, the Good-aiming ones included, now fires 30 ms later in real time, so the AI is judged where it aims. Before, it was judged `30 ms × sweep` against the marker's travel: 0.026 of the bar at round 1 and 0.044 at round 10. That widened its spread both ways, and the result depended on the round's speed. Same 2000-turn simulation, round 1, before → after: Good 47.7% → 54.0%, Perfect 11.0% → 7.2%, Great 18.4% → 15.5%, Miss 0.9% → 0.15%, timeouts 1.85% → 0% (S-088), mean multiplier ×1.556 → ×1.478 (−5%). The round-1 AI is now a little *weaker* and more consistent: it lands the band it aims at more often and the 3× tail less often. Re-check balance in the next playtest. To restore the old strength, raise `aimSigma` slightly or aim Great at a later round; do not remove the bias.

**The seeded RNG is critical for replays and for bug reports.** Every AI turn is deterministic from `(profileIndex, seed)`. The seed is stored in the match recording. A bug report that says "the AI did something weird on turn 4" can be reproduced exactly by loading the recording and stepping to turn 4.

## 11. Open questions

- [ ] Should the AI visibly "react" to the player's performance? E.g. if the player lands three Perfects in a row, does the AI's profile shift up mid-match? This is rubber-banding and should be approached very carefully.
- [ ] Should the AI's tap pattern be audible (M11 plays the same tap tick for AI taps)? It is honest — the AI is using the same system — but it may be annoying during the AI's turn.
- [ ] Is a single `aimSigma` enough, or should the AI have separate sigma for each zone (e.g. more precise when aiming for Good, less for Perfect)?

## 12. Changelog

| Date | Commit | Change |
|---|---|---|
| 2026-10-23 | (pre-git) | **AI ladder + fixes (DEBUG S-094, S-095, S-097, S-088 trap; [ADR-0018](../Decisions/ADR-0018-ai-ladder.md)).** Owner: "AI is too powerful; random levels, progressive". New pure `AiLadder` (`RollTier`, `NextLevel`), `AiProfile.displayName`, `GameConfig.ProfileForTier` (replaces `ProfileForRound`), five tier assets, `GameRoot.AiLadderLevel`, and `BoutController` rolls the tier. `BoutConfig.matchSeed` 0 means a fresh seed per bout (S-094: the fixed seed replayed every rematch). `BeginTiming` takes M04's AIM cue and `EarliestCommitElapsed` (S-093/ADR-0017), which closes the S-088 tuning trap. It no longer waits for a second pass that would land after the timeout (S-097). §3, §4, §4b, §10 updated. Tests: `AiLadderTests`, `GameConfigTests.ProfileForTier_ClampsBothEnds`, `TimingMeterTests.Ai_ThroughTheCueAndGap_*` and `Ai_WaitingForTheNextPass_*`. `RollTier`'s roll itself is left to the owner (TODO(human)); until then it returns the level unchanged |
| 2026-09-19 | (pre-git) | Created (Draft) with profile table and aim algorithm |
| 2026-09-19 | (pre-git) | **P3 code landed.** `AiTapSource : ITapSource` and `AiProfile` SO. Charge phase samples per-tap intervals from a jittered `targetTps` with fatigue decay; timing phase picks a target zone centre + Gaussian aim offset, then calls `MarkerOscillator.NextTimeAtPosition` to schedule the commit tap through the same `ITapSource` the player uses. RNG is a Box-Muller `SeededRandom` in `SlapMania.Core` for reproducibility. |
| 2026-10-23 | (pre-git) | **The AI plays the hold ring ([ADR-0014](../Decisions/ADR-0014-hold-ring-attack-mode.md)).** `AiTapSource` implements `IHoldSource`; `BeginHoldRing` schedules a press and a release from the same aim (§4 *How the AI aims*). Found while testing: the TapMarker AI's commits are judged one `displayLatencyBias` early, because the AI does not watch a display. Logged as DEBUG S-079. The hold-ring AI adds the bias; TapMarker is unchanged. Tested: `HoldRingMeterTests.Ai_AimingPerfect_LandsPerfect` (12 seeds, aimSigma 0.005, 60 fps ticks) and the HoldRing duel. §3 and §4 updated. |
| 2026-10-23 | (pre-git) | **TapMarker AI commits one display bias late (DEBUG S-079 fixed for TapMarker).** `BeginTiming` takes `displayLatencyBias` (M01 `EnterTiming` passes `TimingPhaseConfig.displayLatencyBias`) and schedules `_commitAt = now + when + displayLatencyBias`, the same rule as `BeginHoldRing`. The mid-timeout fallback is unchanged. With M04's event-time arming (S-087), this also ends the shipped AI's ~2% TapMarker timeouts from aiming inside the arm delay (S-088, new; fixed for the shipped values, still a tuning trap). **Feel change:** see §10 (round 1: Good 48% → 54%, Perfect 11% → 7%, mean ×1.556 → ×1.478). §3, §4 (aim steps rewritten to match the code: `reactionDelay` is a minimum lead, not an addition; a new invariant) and §10 updated. Tests in `TimingMeterTests`: `Ai_AimingPerfect_LandsPerfect_OnTheFastestRound` (12 seeds, round 10, where 30 ms is twice the gold half-width; all 12 landed Great before), `Ai_HitchSpanningArmAndCommit_StillCommits` (6 seeds), `Ai_WithTheShippedArmDelayAndReaction_NeverTimesOut` (200 seeds; 4 timed out before) and `Director_GivesTheAiTheBias_SoItIsJudgedWhereItAimed` (through `MatchDirector`; 0.028 off its aim before, under 0.001 after). |
