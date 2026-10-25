---
module: M12-SaveProgression
status: Draft
owner: shobhit
last-verified: 2026-09-19
verified-against: (pre-git)
asmdef: SlapMania.Runtime
---

# M12 — SaveProgression

> **One-liner:** Persists player progress and settings to a JSON file with atomic writes and a migration path.

## 1. Purpose

v1 is a single bout, so "progression" is thin: highest round reached, total slaps landed, best crit streak, settings (volume, tier override). But the save system itself must be robust from day one because corruption is unrecoverable on mobile (no "load last save" when there is only one slot) and migration is required the moment v2 adds a field.

## 2. Responsibilities and non-goals

**Owns**
- `SaveData` — a plain C# class serialized with `JsonUtility`.
- `SaveManager` — atomic write (write to `.tmp`, then rename), periodic auto-save, on-demand save.
- `SaveMigrator` — reads `saveVersion`, applies migration steps.
- Load on boot, save on bout end, save on app pause.

**Explicitly does NOT do**
- Cloud sync → not in v1.
- Manage match recordings → M13 handles those separately.
- Own settings UI → M10.
- Encrypt save data → not needed for a single-player game with no economy.

## 3. Public API surface

| Member | Signature | Notes |
|---|---|---|
| `SaveManager.Load` | `SaveData Load()` | Returns defaults if file missing or corrupt |
| `SaveManager.Save` | `void Save(SaveData data)` | Atomic write |
| `SaveManager.AutoSave` | `void AutoSave()` | Saves if dirty flag is set |
| `SaveData.saveVersion` | `int` | Incremented on schema changes |
| `SaveData.highestRound` | `int` | |
| `SaveData.totalSlaps` | `int` | |
| `SaveData.bestCritStreak` | `int` | |
| `SaveData.settings` | `PlayerSettings` | Volume levels, tier override |

**Consumed by:** M00 (load), M01 (write stats), M10 (display stats, settings).
**Stability:** stable — deliberately simple.

## 4. Configuration

Save path: `Application.persistentDataPath + "/SlapMania/save.json"`.

| Field | Default | Notes |
|---|---|---|
| `autoSaveInterval` | 60 s | Timer-based, plus on-demand on bout end and app pause |
| `maxBackups` | 2 | `save.json.bak1`, `save.json.bak2` — rotated on each save |

## 5. Events

None. Save is an infrastructure service, not a gameplay system.

## 6. State machine

None — load once at boot, write on demand.

## 7. Dependencies

- **Hard:** `SlapMania.Core`.
- **Soft:** none.
- **Unity APIs:** `JsonUtility`, `Application.persistentDataPath`, `File.Move` (atomic rename), `Application.focusChanged` (auto-save on pause).
- **Must initialize after:** M00.

## 8. Failure modes

| Failure | Detection | Behaviour | Player-visible? |
|---|---|---|---|
| Save file missing | `File.Exists` | Return default `SaveData`, log Info | No — fresh start |
| Save file corrupt (invalid JSON) | `JsonUtility` returns null or throws | Return defaults, log Warn, keep `.bak` | Progress lost |
| Write fails (disk full, permissions) | IOException | Retry once, log Error, do not crash | No immediate effect; progress not saved |
| Migration from unknown version | `saveVersion` > expected | Return defaults, log Error | Progress lost |

## 9. Debug hooks

- **Log channel:** `LogChannel.Save`
- **Cheats:** reset save, set stats, force save, force corrupt save (for testing recovery).
- **Recorder events:** `SaveWritten`, `SaveLoaded`.

## 10. Tuning notes

**Atomic writes are non-negotiable.** Writing directly to `save.json` risks a half-written file if the app is killed mid-write (common on Android when the OS reclaims memory). Write to `save.json.tmp`, `fsync`, then rename. `File.Move` on both platforms is atomic at the filesystem level if source and destination are on the same volume (they are — both are in `persistentDataPath`).

**`JsonUtility` is sufficient.** The save data is flat, has no polymorphism, no dictionaries, no nested collections. `JsonUtility` is zero-alloc on read (into an existing object), fast, and built-in. Do not add Newtonsoft.Json for this.

## 11. Open questions

- [ ] Should match recordings (M13) be bundled in the save or stored separately? Separately is better for size, but sharing a backup scheme reduces the number of things that can go wrong.
- [ ] Is a "new game" button needed in v1, or does the single-bout loop make it redundant?

## 12. Changelog

| Date | Commit | Change |
|---|---|---|
| 2026-09-19 | (pre-git) | Created (Draft) |
