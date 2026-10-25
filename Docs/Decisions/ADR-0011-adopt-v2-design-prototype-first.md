# ADR-0011 — Adopt the V2 design organically, prototype-first

**Status:** Accepted
**Date:** 2026-09-26
**Deciders:** shobhit

## Context

A 29-screen V2 mockup set (`SlapMania_V2_AllScreens_for_Figma.html`: title → menu → career →
roster → store → season pass → matchmaking → walkout → in-match → replay → results, plus a
UI kit) describes a broader game than the contract and GLOSSARY did:

| V2 mockup | Contract before this ADR |
|---|---|
| Attack = **hold to wind up, release when the closing white ring touches the gold band** (C5/C6, E3 coaching) | Rapid-**tap** charge, then stop a **ping-pong marker** on coloured zones (M03/M04) |
| **Defence** minigame on the opponent's turn: Brace (release on the gold ring) / Lean (D2) | No player defence; the AI plays the same meter |
| **Hype** meter filled by perfect slaps → **Signature Slap**; **combo** multipliers | `Hype` = 0..1 crowd energy for **audio** (GLOSSARY) |
| **Best-of-3 rounds**, ranked Division Rivals, economy, packs, season pass | One single-player bout vs AI; `Round` = one player+AI turn pair |
| Damage Cam / Instant Replay | Still-photo **Snapshot → Gallery** (M09) |

On 2026-09-26 the owner decided: the docs are not final, **V2 is the direction**, and the
project grows organically, starting with a **playable prototype** plus tooling that makes
every value tunable in the Inspector (camera, audio, commentary, fighters, finishers,
defence, perks, power-ups).

## Decision

1. **V2 is the design direction.** The contract, GLOSSARY and module docs change **per
   mechanic, in the same change that lands it** (R1), not up front. Until a V2 term's module
   lands, the GLOSSARY meaning stands and the V2 term must not appear in code.
2. **Prototype v0 plays the existing, tested loop** (tap-charge → ping-pong marker, M03/M04).
   The V2 **hold→release closing-ring attack is the next module**, added as an
   Inspector-selectable attack mode (e.g. `attackMode = TapMarker | HoldRing`) so both can be
   compared on a device. The ring reuses `TimingZoneLayout` bands and M04's latency
   compensation; its radius is a pure function of elapsed `double` time (R8, ADR-0004).
3. **Each V2 mechanic lands as its own module**: pure-Core math + EditMode boundary tests (R2,
   R3) + a config asset listed in the Tuning Hub. Proposed order: hold-ring attack → defence
   (Brace/Lean) → Hype/Signature Slap + combo → best-of-3 rounds → camera director →
   audio/crowd/broadcast commentary → finishers → perks and power-ups.
4. **Prototype scenes are generated from code**, not hand-authored.
   `Tools > Slap Mania > Prototype > Build Playable Prototype` (SlapMania.Editor) builds
   `Boot.unity` + `Proto_Bout.unity`. Scenes are regenerated on every run; config assets are
   created only when missing and are never overwritten, so tuning survives rebuilds.
5. **Tuning lives in assets wherever a value is a rule or a feel knob** (`BoutConfig` pacing,
   the new `FeelConfig`), because Play Mode edits to assets persist and Play Mode edits to
   scene components do not. The `TuningHub` Inspector shows every asset inline.
6. **Boot + additive bout from day one** (ARCHITECTURE §3), and **TextMeshPro for HUD text
   now** (the `UI -> Unity.TextMeshPro` edge already listed in CONVENTIONS).

### Vocabulary collisions to settle when each mechanic lands

| Term | GLOSSARY today | V2 meaning | Settle in |
|---|---|---|---|
| Hype | 0..1 crowd energy driving audio (M11) | Player-filled meter unlocking the Signature Slap | the Hype/Signature module |
| Round | One player-turn + one AI-turn pair | One game of a best-of-3 match | the rounds module (a new term for one of them) |
| Replay / Damage Cam | not defined | Video replay of the final blow (D4) | M17/M18; must not replace **Snapshot**/**Gallery** silently |
| Brace, Lean, Combo, Signature Slap | not defined | Defence verbs, streak multiplier, special | their modules |

## Consequences

### Positive

- Hands-on feel today: v0 needs no new gameplay math, only a scene, assets and a HUD.
- Nothing is thrown away. The tested M03/M04 code keeps working, and the V2 ring is judged
  against it on a real device before either is retired.
- The generated scene cannot drift from the components: every wire goes through a
  `SerializedObject` write that throws on a renamed field, so Inspector wiring stops being an
  unverified (R11) risk for the prototype.
- One Inspector holds every number, and asset edits persist from Play Mode — the P10 goal of
  "a tuning pass with zero recompiles" starts now rather than at P10.

### Negative

- Manual edits to `Boot.unity` / `Proto_Bout.unity` are lost on rebuild. Real art scenes
  (`Bout_Arena01`) will be hand-authored and must not be generated.
- For a while the prototype plays a loop that differs from the V2 screens. The docs say so
  explicitly (M03/M04 stay authoritative until the ring module lands).
- Two hitstop numbers exist: the FSM's `BoutConfig.hitstopMs` (state length) and the visual
  freeze in `FeelConfig.hitstopMs`. Both are labelled in the Inspector.

### Risks

- Glossary drift: V2 words leaking into code before their module lands. Mitigated by the
  table above and the GLOSSARY "Pending V2 vocabulary" section.
- Scope creep into meta (economy, packs, season pass) before the core loop is fun. The
  vertical-slice go/no-go (ROADMAP P7) still applies — with capsules.

## Alternatives considered

| Alternative | Why rejected |
|---|---|
| Rewrite charge + timing as the V2 hold→release ring **before** anything is playable | Slower to first feel, and it discards tested M03/M04 math before the new verb has been proven better on a device |
| Keep the pre-V2 contract and treat the mockups as marketing art | Owner decision: V2 is the direction |
| Hand-build the prototype scene in the Inspector | Wiring is unverifiable from code (R11) and drifts silently when a field is renamed |
| A single self-contained sandbox scene | Owner chose Boot + additive now, to match the shipping architecture |
| Legacy uGUI `Text` for the HUD | Owner chose TextMeshPro now; it avoids a second migration when the V2 UI kit styling lands |
