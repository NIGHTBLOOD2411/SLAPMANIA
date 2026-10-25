# Slap Mania - Release Checklist

Status: Draft - Last verified 2026-09-26

The pre-ship gate (R12). No build goes to a store track until every **must** here is
green. `release-gatekeeper` ([`/ship-check`](../.claude/commands/ship-check.md)) walks
this file and reports go / no-go. Most items are enforceable in code - prefer a failing
preprocessor over a checklist a human forgets.

Severity: **must** blocks the release · **should** blocks unless explicitly waived with a reason.

---

## 1. Build integrity (must)

- [ ] Built with Unity **`6000.3.22f1`** exactly (`ProjectVersion.txt`). A silent Hub
      upgrade is an unreviewable diff - reject the build.
- [ ] Release **Build Profile** selected, not Dev/QA.
- [ ] `SLAPMANIA_DEBUG`, `SLAPMANIA_CHEATS`, `SLAPMANIA_VERBOSE`, `SLAPMANIA_PERF_HUD`
      **all absent** from the Release profile's scripting defines. `BuildVerifyPreprocessor`
      (`IPreprocessBuildWithReport`) **fails the build** if any are present (S-060) - this
      is the enforcement, the checkbox is the double-check.
- [ ] `SlapMania.Diagnostics` is **not compiled** (its asmdef's `defineConstraints` on
      `SLAPMANIA_DEBUG` mean not one byte of overlay/cheat/recorder code ships).
- [ ] **Unity MCP is build-excluded** (ADR-0009 §2): `com.coplaydev.unity-mcp` is
      editor-only and contributes zero bytes to the player. No `SlapMania.*` runtime
      assembly, nor `Diagnostics`/`Editor`, references it, and it is absent from the
      IL2CPP build. If a build pulls it in, the ADR-0009 exception is void - remove it.
- [ ] No `LogType.Error` in a clean full-match run.

## 2. Player settings (must)

- [ ] Orientation **portrait only**, all three autorotate flags off (the preprocessor also
      fails on non-portrait).
- [ ] Scripting backend **IL2CPP**; API compatibility **.NET Standard 2.1**.
- [ ] Color space **Linear** (the photo grade depends on it - do not change).
- [ ] Android architectures **ARM64 only**.
- [ ] Android graphics APIs **Vulkan first, GLES3 fallback** - and both were tested (§4).
- [ ] Active quality level for Android/iOS is **Mobile**, not the PC template default -
      or on-device numbers are meaningless (S-065).
- [ ] Managed stripping: re-tested on device at the release level; `link.xml`/`[Preserve]`
      covers reflection/`SerializeReference`-only types (S-061).
- [ ] Bundle id `com.dot9games.slapmania`, version + build number bumped, matching the
      recording header's `appVersion`.
- [ ] Android min SDK confirmed for the submission (revisit the placeholder `25`).

## 3. Performance capture (must) - on hardware, not the Editor

Capture in a **Development build with `SLAPMANIA_DEBUG` off**, on a real min-spec and a
real mid-tier device, and paste the dated numbers into [Perf/BASELINE](Perf/BASELINE.md).
A release without a fresh capture is a regression waiting to be discovered by a player.

| Metric | Mid-tier | Min-spec floor |
|---|---|---|
| Frame time, 15 taps/s | <= 14 ms, no frame > 20 ms | no frame > 40 ms |
| GC alloc/frame, Charge & Timing | **0 B** | 0 B |
| Snapshot capture | <= 3 ms GPU, <= 1 ms CPU | <= 6 ms GPU |
| Audio DSP CPU | <= 10 % | <= 14 % |
| Canvas rebuilds/frame during Charge | 0 | 0 |
| Input -> visual latency | <= 2 frames | <= 2 frames |

- [ ] Numbers meet the table, dated, device + OS + GPU recorded.
- [ ] Compared against the previous milestone capture - no unexplained regression.

## 4. The GLES3 snapshot smoke test (must)

Vulkan is production-first, but GLES3 is where flipped-Y, black-RT and context-loss bugs
live (S-030..S-037). Reorder Graphics APIs so GLES3 is first, build, run these, revert:

- [ ] A captured tile is not uniformly black.
- [ ] A 50 % grey card (`#808080` sRGB) reads back as `128 ± 3` (linear/sRGB correct).
- [ ] A "TOP" marker quad on the `SnapshotOnly` layer is right-side-up.
- [ ] Memory delta after 16 slaps is <= 16 MiB (no per-slap RT/texture leak - S-034).
- [ ] Atlas survives a background/foreground cycle (GLES3 context loss - S-035).

## 5. Stability & data (must)

- [ ] `CrashLogger` writes to `persistentDataPath/SlapMania/Logs/`, rotates (last 5, 2 MB
      each), and dumps the match recording on an unhandled exception.
- [ ] Flush logs and saves on `OnApplicationPause(true)` - `OnApplicationQuit` often never
      fires on mobile ([DEBUG §6](DEBUG.md)).
- [ ] Save migration path tested from the previous shipped schema version (no data loss).
- [ ] Enter Play Mode Options on; no second-session static-state divergence (S-062).

## 6. Content & secrets (must)

- [ ] Keystores, provisioning profiles, signing configs are **not** committed
      ([BUILD-AND-RUN](BUILD-AND-RUN.md)) - verify `git status` and history.
- [ ] No secret, token or backend credential in the repo, in `.claude/settings*.json`, or
      in any committed config. (This is a real, recurring class of leak - grep before you tag.)
- [ ] Git LFS intact for art/audio; `.gitattributes` current.
- [ ] TMP Essentials imported in the build machine's project (else UI text errors).
- [ ] (When M19 lands) Addressables catalog hash pinned per profile; local minimal-content
      set bundled so first bout is offline-playable ([ADR-0008](Decisions/ADR-0008-addressables-and-ccd.md)).

## 7. Store readiness (should)

- [ ] Screenshots/preview captured with the Recorder from the real build.
- [ ] Age rating, permissions justification, privacy policy current.
- [ ] Rollout plan: staged % rollout, and a rollback build kept.

---

## Go / No-Go

| Gate | Owner | State |
|---|---|---|
| Build integrity (§1) | eng | |
| Player settings (§2) | eng | |
| Perf capture (§3) | eng | |
| GLES3 smoke (§4) | eng | |
| Stability & data (§5) | eng | |
| Content & secrets (§6) | eng | |
| Store readiness (§7) | producer | |

Any **must** red = **No-Go**. Record the decision and the sha here per release.

---

## Changelog

| Date | Change |
|---|---|
| 2026-09-26 | Created. Build integrity, player settings, perf, GLES3 smoke, stability, secrets, store. |
