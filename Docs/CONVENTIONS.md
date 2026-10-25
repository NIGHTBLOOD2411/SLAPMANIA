# Slap Mania - Conventions

## Vocabulary

If a concept appears in [GLOSSARY.md](GLOSSARY.md), the code spells it that way and
nothing else. Renaming a concept means updating the glossary in the same commit.

## Namespaces

`SlapMania.<Assembly>[.<Area>]` - matching the assembly name exactly.

```
SlapMania.Core            SlapMania.Presentation
SlapMania.Data            SlapMania.Audio
SlapMania.Logging         SlapMania.UI
SlapMania.Gameplay        SlapMania.Meta
SlapMania.Input           SlapMania.App
SlapMania.Snapshot        SlapMania.Diagnostics
SlapMania.Editor          SlapMania.Tests.EditMode / .PlayMode
```

## Folders

```
Assets/_Project/          # underscore pins it above imported packages
  Scripts/<Assembly>/     # one folder per asmdef, folder name == assembly suffix
  Tests/{EditMode,PlayMode}/
  Data/                   # ScriptableObject assets, grouped by kind
  Prefabs/{Fighters,Arena,UI/Screens,UI/Widgets,VFX,Systems}/
  Art/{Characters,Arenas,VFX,UI,Fonts}/
  Audio/{SFX,Music,VO,Crowd,Mixers,Banks}/
  Animation/{Fighter,Camera,UI,Timelines}/
  Scenes/                 # Boot.unity, Menu.unity, Bout_Arena01.unity
  Settings/               # URP assets live here after the Phase 0 move
  Shaders/
Assets/Plugins/{Android,iOS}/
Docs/                     # repo root, never under Assets/ - ADR-0001
```

`Assets/_Sandbox/` is gitignored scratch space. Nothing in it may be referenced by
anything in `_Project`.

## Assemblies

Every script lives in an asmdef. `Auto Referenced = false` on all of them, so a
stray script at the `Assets/` root cannot create a hidden dependency.

Dependency edges point one way and **never reverse**:

```
Logging       -> (nothing)
Core          -> Logging
Data          -> Core, Logging
Gameplay      -> Core, Data, Logging
Input         -> Core, Logging, Unity.InputSystem
Snapshot      -> Core, Logging, URP
Audio         -> Core, Data, Logging
Presentation  -> Core, Data, Gameplay, Snapshot, Audio, URP, Timeline
UI            -> Core, Data, Gameplay, Unity.InputSystem, TextMeshPro
Meta          -> Core, Data, Logging
App           -> all of the above (composition root only)
Diagnostics   -> Core, Data, Gameplay, Logging   [defineConstraints: SLAPMANIA_DEBUG]
Editor        -> all  [includePlatforms: Editor]
```

**`Gameplay` must not reference `Input`, `UI`, `Presentation` or URP.** If you want
to add that edge, the answer is a new interface in `Core` or a new signal - see
[ADR-0002](Decisions/ADR-0002-assembly-graph.md).

## C# style

- Private fields `_camelCase`. Serialized private fields `[SerializeField] private T _x;`.
- **No public fields on MonoBehaviours.** Expose read-only properties.
- ScriptableObjects are read-only at runtime. Anything that mutates during play is a
  runtime copy, never the asset.
- `const` and `static readonly` for tuning constants only when they are genuinely not
  designer-facing; otherwise it belongs in a ScriptableObject.
- One public type per file, file named after the type.
- `internal` by default within an assembly; `public` only for the documented API
  surface listed in that module's doc (section 3).
- Prefer `readonly struct` for value payloads crossing module boundaries
  (`TapEvent`, `SlapResult`, `BeatEvent`).
- Async work uses Unity 6's `Awaitable`, not coroutines and not UniTask.
  Gameplay's FSM stays synchronous (`Tick(dt)`); async lives in `Presentation`.

## Performance rules

Hard requirements inside the Charge and Timing phases (they are the most
input-sensitive windows in the game, and a GC spike there is directly felt):

- **Zero managed allocations per frame.** No LINQ, no string concatenation, no
  capturing lambdas, no `foreach` over an interface-typed collection, no `params`
  arrays. Cache every `WaitForSeconds`.
- No `GetComponent` after `Awake`.
- No `Resources.Load` on any gameplay path.
- Fixed-size buffers for queues and ring buffers, allocated once.
- A PlayMode test measures allocations across a full charge phase and **fails at
  anything above 0 bytes**.

## Time

- Marker position, charge progress and every zone window are functions of **`double`
  elapsed seconds**, never accumulated with `+= speed * dt`.
- Zone windows, phase durations and hitstop are authored in **milliseconds**.
- All presentation tweens run on **unscaled** time. The shared `Tweener` utility
  defaults to unscaled - in a hitstop game, scaled-time tweens are the single largest
  source of "why is my UI frozen". It is in-house (`SlapMania.Core.Motion.Tweener` +
  `Easing`, landed 2026-09-26; no DOTween, ADR-0007). The owner **ticks it with the dt it
  chooses** (`Tick(Time.unscaledDeltaTime)`); the tweener never reads `Time.*` itself (R2).
  It is zero-alloc (R5): fixed struct slots, and callbacks and value sinks are interfaces
  (`ITweenCallback`, `ITweenSink`), never lambdas. A new tween on the same target and
  property replaces the old one, and a restarted shake or punch keeps the original rest
  pose. When the tweener is full, a new tween snaps to its end value instead of
  allocating. Tests: `TweenerTests`, `EasingTests`.
- Audio scheduling uses `AudioSettings.dspTime`, never `Time.time`.

## Logging and assertions

- Never `Debug.Log` in gameplay code. Use `SlapLog.Trace/Info/Warn(LogChannel.X, ...)`,
  which is `[Conditional("SLAPMANIA_DEBUG")]` and so deletes the entire call site
  including argument expressions in release. `SlapLog.Error` is not conditional.
- `SlapAssert.That(...)` for **programmer errors** - null config, illegal state
  transition, a zone table that does not tile, NaN reaching damage.
- **Never assert on environment** - a missing save file, no touchscreen, a failed
  write. Those are conditions: handle them and log an Error.
- Assertions are stripped in release, so an assert must never have side effects and
  must never be the only thing validating player-facing data.

## Commits

`type(M0X): summary`

`type` is one of `feat | fix | chore | docs | perf | test | refactor`.
`M0X` is the module number, or `repo` for cross-cutting changes.

Examples:
```
feat(M04): ms-based zone windows
fix(M09): clear atlas RT on bout start, Pixel 6a showed stale tiles
docs(M11): record the 140 BPM decision and stem naming
```

Branches: `main` is always buildable; work happens on short-lived
`feat/<module>-<slug>` branches, squash-merged so one module change is one commit is
one doc update.

## Documentation

- A change under `Assets/_Project/Scripts/<Module>/` updates
  `Docs/Modules/M**-<Module>.md` in the **same commit** - the affected sections, plus
  `last-verified` and a Changelog row.
- A design choice with a rejected alternative gets an ADR in `Docs/Decisions/`.
- A new bug gets a `DEBUG.md` symptom row **before** investigation begins.
