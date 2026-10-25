---
module: M02-Input
status: InProgress
owner: shobhit
last-verified: 2026-10-23
verified-against: (pre-git)
asmdef: SlapMania.Input
---

# M02 — Input

> **One-liner:** Converts raw touch and click events into `TapEvent` structs and delivers them through `ITapSource` so every consumer — player, AI, replay — uses the same interface.

## 1. Purpose

The game has exactly one input verb: **tap**. But that verb arrives through different channels (touch, mouse, synthetic AI, recorded replay) and carries metadata the downstream systems need (timestamp, screen position, pressure). This module normalises all of them into `TapEvent` and delivers them through `ITapSource`, which M03 and M04 consume without knowing or caring who tapped.

The critical contract is the **event timestamp**. A tap's `eventTime` is the Input System's own hardware timestamp, not `Time.time` or the frame it was processed on. M04's latency compensation depends on this being accurate to within a millisecond.

## 2. Responsibilities and non-goals

**Owns**
- The `SlapControls.inputactions` asset and its generated C# class.
- `InputSystemTapSource : ITapSource` — the live player input path.
- `EnhancedTouch` setup and teardown.
- De-duplicating multi-finger contacts into a single tap stream.
- Providing `eventTime` on every `TapEvent` from `InputEventPtr.time`.

**Explicitly does NOT do**
- Decide what a tap *means* → M03 scores it, M04 commits on it.
- Generate AI taps → `M07-OpponentAI` owns `AiTapSource`.
- Generate replay taps → `M13-DebugTools` owns `ReplayTapSource`.
- Handle UI button presses → standard Unity UI `Button.onClick`, not routed through `ITapSource`.

## 3. Public API surface

| Member | Signature | Notes |
|---|---|---|
| `ITapSource.OnTap` | `event Action<TapEvent>` | Subscribers: M03, M04 |
| `ITapSource.Enable` | `void Enable()` | |
| `ITapSource.Disable` | `void Disable()` | |
| `IHoldSource : ITapSource` | `event Action<TapEvent> OnRelease` | **Landed 2026-10-23** ([ADR-0014](../Decisions/ADR-0014-hold-ring-attack-mode.md)). The end of a press, stamped with its own event time, for the hold-ring attack. `OnTap` is the press. Implemented by `InputSystemTapSource` (from the action's `canceled`), `PausableTapSource` (M01: releases made while paused are dropped like taps), `AiTapSource` and `ManualTapSource`. Subscriber: M04 `HoldRingMeter` |
| `TapEvent` | `readonly struct` | Fields below |
| `InputSystemTapSource.Blocker` | `Func<Vector2, bool> { get; set; }` | **Landed 2026-10-20.** Screen position (px) -> true to swallow the press. The bout sets it to `V2Hud.BlocksTap`, so a press on the HUD pause button is a click, not a slap tap (it would add charge or commit the timing slap). Set once at bind; called per tap, must not allocate (R5). Keyboard (Space) has no position and is never blocked; releases are never blocked |

### `TapEvent` fields

| Field | Type | Notes |
|---|---|---|
| `eventTime` | `double` | `InputEventPtr.time` — hardware timestamp, not frame time |
| `screenPosition` | `Vector2` | Pixel coordinates (bottom-left origin) of the pointer that fired, read from its device after the state event (2026-10-20; was always zero). Zero for the keyboard. Used by `Blocker` and the debug overlay |
| `pressure` | `float` | 0–1, from `TouchPress.pressure`; 1.0 on mouse/keyboard. Reserved for future use (e.g. heavy-press bonus). |
| `sourceId` | `byte` | 0 = player touch, 1 = player mouse, 2 = AI, 3 = replay |

**Consumed by:** M03, M04, M07 (via the interface), M13 (recording).
**Stability:** the interface is stable; the implementation will churn with device testing.

## 4. Configuration

### `SlapControls.inputactions`

One action map: **Gameplay**.

| Action | Type | Binding | Notes |
|---|---|---|---|
| `Tap` | `Button` | `<Touchscreen>/primaryTouch/press`, `<Mouse>/leftButton`, `<Keyboard>/space` | Space is for editor testing only |

**No composite actions, no hold, no swipe.** The game is tap-only. Keeping the action map minimal avoids the Input System's considerable complexity budget.

### Runtime settings (set in `GameRoot.Awake`)

| Setting | Value | Why |
|---|---|---|
| `InputSystem.pollingFrequency` | **120** | Static property (not `InputSystem.settings`). At 60 Hz, two taps within 16 ms collapse into one event. 120 Hz halves the worst-case loss. |
| `EnhancedTouchSupport.Enable()` | — | Required for `Touch.activeTouches` and per-touch `startTime`. |

## 5. Events

**Emits**

| Event | Payload | When |
|---|---|---|
| `OnTap` (via `ITapSource`) | `TapEvent` | Every accepted touch-down or click |

**Consumes**

| Event | From | Reaction |
|---|---|---|
| `MatchSignal.ChargeStart` | M01 | `Enable()` the tap source |
| `MatchSignal.TimingEnd` | M01 | `Disable()` the tap source |

## 6. State machine

Stateless relay. Enabled or disabled, nothing else.

```
Disabled --Enable()--> Enabled --Disable()--> Disabled
```

When disabled, input events are consumed by the Input System but `OnTap` is not fired.

## 7. Dependencies

- **Hard:** `SlapMania.Core` (`ITapSource`, `TapEvent`).
- **Soft:** none.
- **Unity APIs:** `InputSystem` 1.20.0, `EnhancedTouchSupport`, `InputAction`, `InputEventPtr`.
- **Must initialize after:** M00 (which calls `EnhancedTouchSupport.Enable()`).

## 8. Failure modes

| Failure | Detection | Behaviour | Player-visible? |
|---|---|---|---|
| EnhancedTouch not enabled | `Touch.activeTouches` throws | `GameRoot.Awake` enables it; if somehow missed, `InputSystemTapSource` enables it in its constructor with a Warn log | No |
| Touch driver sends duplicate down events | `minTapInterval` in M03 rejects them | Logged at Verbose level | No |
| `eventTime` is 0 or negative | Guard in `InputSystemTapSource` | Substitute `Time.realtimeSinceStartupAsDouble`, log Warn | Slightly degraded latency comp |
| Polling frequency ignored by platform | No detection (platform limitation) | M03's `minTapInterval` and M04's `maxLatencyComp` still bound the system | Possible missed taps at very high rates |
| Mouse and touch fire simultaneously (editor) | De-dup by checking both bindings in the same frame | Take the first, discard the second | No |

## 9. Debug hooks

- **Log channel:** `LogChannel.Input`
- **Overlay (Input tab):** instantaneous taps/second, accepted/rejected counts (with rejection reason breakdown), last `eventTime` vs frame `Time.time` delta, last screen position, `pollingFrequency` readback, `sourceId` of last tap.
- **Cheats:** auto-tap toggle (configurable rate), single-tap button, tap-rate slider.
- **Recorder events:** every `TapEvent` with its `eventTime` and `sourceId`.
- **Verbose:** per-tap log line with `eventTime`, `screenPosition`, `pressure`, and the M03 accept/reject decision. Guarded by `SLAPMANIA_VERBOSE`.

## 10. Tuning notes

**`pollingFrequency = 120` is non-negotiable until proven otherwise on device.** At the default 60, the Input System polls once per frame at 60 fps and twice at 30 fps. A player tapping at 12 tps generates a tap every 83 ms. At 60 Hz polling, two taps 50 ms apart can land in the same poll and one is lost. At 120 Hz the window halves to 8.3 ms, which is below human tap intervals. The cost is ~0.1 ms/frame of extra polling — invisible.

**`EnhancedTouchSupport` is required, not optional.** Without it, `Touch.activeTouches` is not available, and the legacy `Input.GetTouch` API is disabled because the project uses the New Input System exclusively (old Input Manager is OFF in Player Settings). There is no fallback.

**Do not add gesture recognition.** The temptation to detect "hard taps" via pressure or contact area is real, but the touch pressure API is unreliable across Android OEMs (some report 0 always, some report binary 0/1, some report analog values on different scales). The `pressure` field is recorded for future analysis but not consumed by any gameplay system in v1.

**Multi-finger taps are collapsed, not multiplied.** Two fingers hitting the screen simultaneously produce two `Touch` events. `InputSystemTapSource` fires `OnTap` once per `primaryTouch` press. Additional touches are ignored. This is deliberate — the game is one-thumb. If two-thumb is ever enabled, it is M03's `minTapInterval` that gates it, not the input layer.

## 11. Open questions

- [ ] Should `pressure` feed into a "heavy tap" mechanic in v2, or is it too unreliable across devices to ever be gameplay-relevant?
- [ ] Is 120 Hz polling enough for competitive fairness, or should we try the `InputSystem.onEvent` callback path for lower-latency delivery?
- [ ] Should the keyboard `Space` binding be stripped from non-editor builds, or is it harmless to leave?

## 12. Changelog

| Date | Commit | Change |
|---|---|---|
| 2026-09-19 | (pre-git) | Created (Draft) |
| 2026-09-26 | (pre-git) | **P5 partial.** `InputSystemTapSource : ITapSource, IDisposable` landed in its own `SlapMania.Input` asmdef (corrected from `SlapMania.Runtime` per [ADR-0010](../Decisions/ADR-0010-assembly-graph-refinement.md)). Emits a `TapEvent` per touch/click/space press, stamped with the Input System event time (`ctx.time`), which shares `UnityClock`'s realtime base so §1's latency contract holds. `Enable()/Disable()` gate emission only; the action stays subscribed, so the meters' `Begin/End` drive it exactly as designed, and human taps are ignored on AI turns (source stays disabled). **Known simplification:** the `Tap` action is **built in code** (three bindings), not from a `SlapControls.inputactions` asset with a generated class — deferred so the input path has no asset dependency yet. **Doc corrections:** §4 previously wrote `InputSystem.settings.pollingFrequency`; the real API is the static `InputSystem.pollingFrequency` (set in `GameRoot`, M00). |
| 2026-10-20 | (pre-git) | **Tap blocker + real tap position.** `InputSystemTapSource.OnPerformed` now reads the firing `Pointer`'s `position` (touch or mouse; no allocation) into `TapEvent.ScreenPosition`, and a new `Blocker` (`Func<Vector2,bool>`) can veto the press by position. Motivation: the bout's HUD pause button (M10) sits on the tap surface, so its press-down would otherwise add charge or commit the timing slap before the click opened the pause menu. Downstream, the director gates the player's source through `Gameplay.PausableTapSource` (M01): taps made while paused are dropped and the rest re-stamped to simulation time. Verified: EditMode (gate tests in `PausableClockTests`); in Play over MCP the live `Blocker` returns true on the pause button and false mid-screen, and taps sent through the source's `OnTap` while paused were dropped. A physical touch on the button was not exercised: the Editor was unfocused, so taps were injected. §3 updated. |
| 2026-10-23 | (pre-git) | **Release events for the hold ring ([ADR-0014](../Decisions/ADR-0014-hold-ring-attack-mode.md)).** New `Core.IHoldSource : ITapSource` with `OnRelease`. `InputSystemTapSource` implements it from the `Tap` action's `canceled` callback (Button, no interaction: performed on press, canceled on release), stamped with `ctx.time` and the pointer position; releases are never blocked. Tap-only sources are unaffected. **Unverified on device:** release timestamps have not been measured against press timestamps (M04 §11). §3 updated. |
