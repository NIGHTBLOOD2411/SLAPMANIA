# Slap Mania - Glossary

**The rule (also in CONVENTIONS.md): if a concept is in this glossary, the code
spells it that way and nothing else.** This is what stops `power` / `charge` /
`strength` becoming three names for one number.

## Match structure

| Term | Definition | Code identifier | Not to be confused with |
|---|---|---|---|
| **Bout** | One full tournament run - the player versus a sequence of opponents | `Bout` | Match |
| **Match** | One opponent, fought until someone's health reaches zero | `Match` | Bout, Round |
| **Round** | One player-turn plus one AI-turn pair, within a match | `Round` | Turn |
| **Turn** | One fighter's single slap attempt, from start to resolution | `Turn` | Slap |
| **Slap** | The resolved attack: power x multiplier -> damage | `SlapResult` | Turn |
| **Fighter** | A combatant (player or AI) with stats, health and a view | `Fighter` | Champion |
| **Champion** | The narrative framing of a fighter - a universe's representative | `ChampionDefinition` | Fighter |
| **Universe** / **Plane** | A champion's origin, used for entrance identity and VO | `OriginId` | Arena |
| **Pause** | The bout frozen on the pause menu: the simulation clock, scaled time and the HUD stop and taps are dropped; play resumes from the exact paused instant. Orthogonal to the match state | `MatchDirector.IsPaused`, `RequestPause` | **Hitstop** (a freeze *inside* play); `MatchState.Paused` (never entered) |
| **Forfeit** | The player gives up a live match from the pause menu; the opponent wins at once. Refused once the lethal slap has landed | `MatchDirector.Forfeit`, `MatchResult.Forfeited` | KO |
| **AI ladder** | The opponent's difficulty across a session: the player's **ladder level** (a tier index) rises one on a win and falls one on a loss, and each bout's opponent is rolled near it ([ADR-0018](Decisions/ADR-0018-ai-ladder.md)) | `AiLadder`, `GameRoot.AiLadderLevel` | Career, Division (M12's planned meta) |
| **AI tier** | One rung of the AI ladder: an `AiProfile` in `GameConfig.aiProfiles`, easiest first (ROOKIE ... CHAMPION). Always "AI tier" in prose, never bare "tier" | `AiLadder.RollTier`, `GameConfig.ProfileForTier` | **Tier** (a device performance bucket, `DeviceTier`): same word, different thing |

## The two minigames

| Term | Definition | Code identifier | Not to be confused with |
|---|---|---|---|
| **Charge** | Phase 1 - the rapid-tap window | `ChargePhase` | Power |
| **Power** | The normalized 0..1 output of the charge phase | `power` | Damage, Charge |
| **Fill** | The raw accumulator inside the charge phase, before mapping to power | `fill` | Power |
| **Overcharge** | Surplus accrued by tapping after the fill is capped | `overcharge` | Power |
| **Tap** | One accepted input press during the charge or timing phase | `TapEvent` | Touch, Finger |
| **Marker** | The moving indicator on the timing meter | `MarkerPosition` | Zone, Cursor |
| **Zone** | A colored band on the timing meter, carrying a multiplier | `MultiplierZone` | Band, Region |
| **Zone Window** | A zone's width expressed in **milliseconds of marker travel** | `zoneWindowMs` | zone width in pixels or in bar fraction |
| **Multiplier** | The ratio awarded by the zone the marker stopped in | `multiplier` | Crit |
| **Commit** | The act of stopping the marker | `TryCommit` | Tap |
| **Arm delay** | The window after the timing phase opens during which taps are ignored | `armDelay` | Cooldown |
| **AIM cue** | The start of the timing phase: the ring is shown but the Marker holds still and no tap commits, so a masher can see the phase change. The arm delay follows it ([ADR-0017](Decisions/ADR-0017-aim-cue-and-spam-gap.md)) | `aimCueMs`, `TimingMeter.InCue` | Arm delay (counts after the cue), Countdown |
| **Brace** | The player's defence on the rival's turn: one tap as the rival's hand comes in, graded Perfect / Good / Flinch / None by its distance from the brace target (impact − `braceLeadMs`). It cuts the slap's damage through `DamageInput.Reduction` ([ADR-0019](Decisions/ADR-0019-brace-tap-defence.md)) | `BraceJudge`, `BraceResult`, `MatchSignals.BraceResolved` | Block, Guard, Parry |
| **Spam gap** | The minimum time since the previous press (charge taps included) for a press to count as a Commit. A finger that keeps mashing never stops the Marker | `spamGapMs` | Debounce, `minTapInterval` (M03's charge-tap filter) |
| **Attack mode** | How a turn's slap is played: **TapMarker** (Charge, then stop the Marker) or **HoldRing** (one hold). Per bout in `BoutConfig`, read at every turn start ([ADR-0014](Decisions/ADR-0014-hold-ring-attack-mode.md)) | `AttackMode` | Difficulty, Round |
| **Hold ring** | The V2 attack (mockups C5/C6): press and **hold** to wind up while the closing ring shrinks, **release** on the gold band. One gesture yields both power and multiplier | `HoldRingMeter`, `HoldRingConfig`, `MatchState.HoldRing` | Ring (the platform), Closing ring (its visual) |
| **Hold** | A press kept down during the hold ring; the ring starts at the press's own event time | `IHoldSource`, `TryPress` | Tap |
| **Release** | The end of a hold. In the hold ring it commits the slap, latency-compensated like a tap | `OnRelease`, `TryRelease` | Commit (stopping the Marker) |
| **Closure** | The hold ring's 0..1 state: 0 open (the press), 1 closed on the target. A pure function of hold time, and a `TimingZoneLayout` position, so it lands in the Marker's zones | `Closure`, `HoldRing.ClosureAt` | Marker position, Radius (pixels) |
| **Wind-up** | The 0..1 power a hold has built, peaking on the gold band; power = lerp(minPower, maxPower, wind-up). V2's C6 word | `WindUp01`, `HoldRing.WindUpAt` | **Windup** (`MatchState.Windup`, the slap animation between Resolve and Impact): same sound, different thing. In code the hold's is always `WindUp…`, the FSM's `Windup` |
| **Gold band** | The Perfect zone drawn on the closing ring, in both attack modes | `HudTiming._goldBand` | Zone (the rule), Perfect |

## Resolution

| Term | Definition | Code identifier | Not to be confused with |
|---|---|---|---|
| **Contact** | The frame the hand touches the recipient | `AnimEvt_SlapContact` | Impact |
| **Impact** | The gameplay moment damage is committed (fires at contact) | `ImpactFrame` | Contact |
| **Hitstop** | The `timeScale` dip around contact that sells the hit | `HitStopController` | Slow-mo |
| **Crit** | A slap whose multiplier meets `critThreshold` (default x2.0) | `isCrit` | Finisher |
| **Finisher** | The slap that reduces health to <= 0 | `isFinisher` | Critical hit, KO |
| **KO** | The defeated fighter's fall/defeat state, after the finisher | `PlayDefeat` | Finisher |

## Snapshots

| Term | Definition | Code identifier | Not to be confused with |
|---|---|---|---|
| **Snapshot** | The photo of the recipient's reaction, captured at contact + 35 ms | `SlapSnapshot` | Screenshot, Mixer snapshot |
| **Atlas** | The single persistent RenderTexture holding every snapshot as a tile | `SnapshotAtlas` | Texture atlas (art) |
| **Tile** | One snapshot's slot in the atlas | `SnapshotHandle` | Cell |
| **Gallery** | The ordered collection of a match's snapshots. Today the result screens (victory **and** defeat) show the loser's three: the finisher plus their hardest hits, in order ([ADR-0016](Decisions/ADR-0016-slap-gallery-pre-atlas-capture.md)) | `SnapshotGallery` (planned); today `SlapSnapshotter.FillGallery`, `HudContext.Gallery` | Album |
| **Album** | The persisted cross-bout archive of saved JPEGs on disk | `SnapshotArchive` | Gallery |
| **Polaroid** | The in-bout pop-up card showing the snapshot just taken | `PolaroidPop` | Gallery card |

Note the collision hazard: **Snapshot** means a photo in gameplay, but Unity also
calls an `AudioMixer` state a snapshot. In audio code always write
`AudioMixerSnapshot` or `MixerSnapshot` in full - never bare `Snapshot`.

## Audio

| Term | Definition | Code identifier | Not to be confused with |
|---|---|---|---|
| **Beat clock** | The dspTime-anchored musical clock other systems sync to | `BeatClock` | Game clock |
| **Intensity** | 0..1 musical energy driver, fed by the fight state | `Intensity01` | Hype |
| **Hype** | 0..1 crowd energy, spikier than intensity | `Hype01` | Intensity |
| **Stem** | One vertically layered music track (Core / Melody / Hype) | `MusicStem` | Section |
| **Section** | A horizontal music segment (Intro / LoopA / LoopB / Break / Outro) | `MusicSection` | Stem |
| **VO** | Announcer voiceover | `VoDirector` | Dialogue |
| **Carrier** | A VO line with a slot for a stitched name or number | `VoCarrier` | Stinger |
| **Stinger** | A short stitched fragment (a champion name, an origin, a numeral) | `VoStinger` | Carrier |
| **Duck** | Automatic level reduction of one bus while another plays | `duck_*` | Fade |

## Presentation

| Term | Definition | Code identifier | Not to be confused with |
|---|---|---|---|
| **Shot** | A named camera framing with position, rotation and FOV | `ICameraShot` | Snapshot |
| **Trauma** | The 0..1 shake accumulator, decayed linearly; the shake scales with trauma² | `CameraDirector.AddTrauma`, `CameraShakeMath` | Shake amount |
| **Camera key** | One recorded camera pose (position, rotation, FOV, DoF) at a time in ms, with the transition *into* it: path, ease, hold, look-at/follow target, shake (M16) | `CameraKeyframe` | Keyframe (animation clip), Snapshot |
| **Camera sequence** | An ordered track of camera keys played over the rig, recorded at runtime and imported as an asset (M16) | `CameraSequenceData`, `CameraSequence` | Cutscene (M18, the whole scripted moment) |
| **Camera cue** | A match moment a camera sequence can be bound to (BoutIntro, Impact, Finisher...) | `CameraCue` | Signal (the M01 event that triggers it) |
| **State shot** | What the camera does when one game state begins (Keep, Gameplay, Shot, Sequence) and how it transitions in and back out (M16) | `StateShot`, `StateCameraMode` | Shot (a planned fixed framing), Camera sequence |
| **Two-sided shot** | A state shot with a separate camera key for each attacker (player's turn / opponent's turn), e.g. a shoulder cam per fighter (M16) | `StateShot.perSide`, `opponentKey` | A mirrored attacker-relative shot (one key for both) |
| **Rig setup** | One saved/undoable bundle of the rig's angles, FOV, shake presets and state shots | `CameraRigSetup` | Camera sequence |
| **Path mode** | How a camera key's position is reached: Cut, Linear or Spline (Catmull-Rom) | `PathMode` | Ease (the timing curve) |
| **Smear** | The ghost-mesh trail used instead of full-screen motion blur | `SmearTrail` | Motion blur |
| **Ring** | The central slap platform | `SlapRing` | Ring (jewellery), ring buffer |
| **Jumbotron** | An in-arena display screen showing a feed | `ArenaDisplay` | HUD |
| **Podium** | The slap table at the ring centre that the two fighters slap across (brass-rimmed top, medallion apron, four legs) | `Podium` (generated GameObject; no class) | Ring (the whole platform) |
| **Damage Cam** | The jumbotron's **live** feed: a second camera framing the defender's face from the attacker's side (M17). Never recorded; never enters the **Gallery** | `DamageCam` | Snapshot, Replay |
| **Closing ring** | The V2 timing visual: a white ring over the defender's face whose radius is a pure function of the active meter: the M04 marker position (TapMarker) or the hold ring's **Closure** (HoldRing). The Perfect zone is drawn as the **Gold band** (M10 `HudTiming`). A *view*, not a mechanic | `closingRing` | Ring (platform), **Hold ring** (the mechanic) |

## Systems vocabulary

| Term | Definition | Code identifier |
|---|---|---|
| **Tap source** | The interface that supplies taps - human, AI, replay or test | `ITapSource` |
| **Hold source** | A tap source that also reports releases; required by the hold ring | `IHoldSource` |
| **Fighter view** | The interface the simulation drives to make a fighter visible | `IFighterView` |
| **Timing source** | The interface supplying contact / snapshot / recover beats | `ISlapTimingSource` |
| **Signal** | A discrete gameplay event other systems react to | `MatchSignals` |
| **Tier** | A device performance bucket (High / Mid / Low / Off) | `DeviceTier` (planned; the arena's interim switch is `ArenaDirector.Tier` High/Low) |
| **Channel** | A logging category that can be enabled independently | `LogChannel` |
| **Feel config** | The asset of presentation knobs felt on every slap (hitstop, recoil, callout timings) - not rules | `FeelConfig` |
| **Tuning Hub** | The editor-only object whose Inspector shows every config asset and scene feel component in one place | `TuningHub` |
| **Arena look** | The asset of arena lighting and atmosphere values (lights, ambient, fog, impact flash, beam sweep), applied live by `ArenaDirector` | `ArenaLookConfig` |
| **Rig profile** | A fighter's animation, IK, hit-spring and knock-out tuning asset | `FighterRigProfile` |

## Front end (M10, [ADR-0013](Decisions/ADR-0013-ui-toolkit-front-end-from-mockups.md))

| Term | Meaning |
|---|---|
| **Front end** | Everything outside a bout: the menus in `FrontEnd.unity`, built in UI Toolkit. The match **HUD** stays uGUI. |
| **Screen** | One front-end page, named by `ScreenId` after its mockup file (`MainMenu`, `Store`, `TaleOfTape`, ...). A screen name is a *place*, not a mechanic: `Store` existing does not mean an economy exists (ADR-0011 §3). |
| **Screen stack** | `ScreenManager`'s history; Back pops it. Tabs (Play, Career, Fighters, Store, Season, Ranks) are siblings, not a stack. |
| **Hook** | A name the converter pins on a mockup element (`loader-fill`, `ticker`, ...) so screen code can find it after any re-conversion. |
| **Mockup** | A Design-canvas frame in `Docs/Reference/Mockups/`: the source of a screen's layout and copy. |
| **Overlay** (bout overlay) | The bout scene's own screen stack, drawn over the uGUI HUD: PauseMenu, Replay, RoundEnd, RankUp and Settings. Opens empty and presents only while a screen is up. Its `Replay` and `RoundEnd` are placeholder *places*, not the pending Replay / Round mechanics below. |
| **Page** | One page of the Design canvas, and the set of screens converted from it: **V2** (the current design) or **V1** (the earlier one). `GameConfig.frontEndPage` (`FrontEndPage`) picks which page the menus open on. |
| **V1 page** | The earlier design's 16 screens, kept playable for comparison: `ScreenId.V1Main` ... `V1Defeat`, UXML in `Prefabs/UI/Screens/V1`. Its words (Splash, Matchmaking, VS Intro, Countdown, ...) name V1 *places* only; they are not V2 mechanics. Its eight bout screens (Countdown onwards) start the real bout, which is the V2 HUD. Not "Classic UI" or "legacy menus". |

## Pending V2 vocabulary (not adopted yet)

[ADR-0011](Decisions/ADR-0011-adopt-v2-design-prototype-first.md) adopts the V2 design one mechanic at a time. These words come from the V2 mockups and **must not appear in code until their module lands and this glossary is updated in the same change**. Until then, the meanings above win.

| V2 word | Collision / status | Settled by |
|---|---|---|
| **Hype** (meter → Signature Slap) | Collides with **Hype** above (crowd energy for audio). One of them gets a new name | Hype/Signature module |
| **Round** (best-of-3) | Collides with **Round** above (one player+AI turn pair) | Rounds module |
| **Lean** | New: a dodge verb on the opponent's turn (D2 mockup). **Brace** was adopted 2026-10-23, see above | Defence module |
| **Combo** | New: streak multiplier for consecutive perfect slaps | Hype/Signature module |
| **Signature Slap** | New: the special unlocked by a full Hype meter | Hype/Signature module |
| ~~**Ring** (closing timing ring)~~ | **Settled 2026-10-23.** The visual is **Closing ring**, the mechanic is **Hold ring**, and its parts are **Hold**, **Release**, **Closure**, **Wind-up** and **Gold band** (tables above). Bare "Ring" still means the slap platform (`SlapRing`) | [ADR-0014](Decisions/ADR-0014-hold-ring-attack-mode.md) |
| **Replay** | Must not silently replace **Snapshot** / **Gallery**. (**Damage Cam** is adopted above as the *live* feed only, 2026-09-26) | M17/M18 |
