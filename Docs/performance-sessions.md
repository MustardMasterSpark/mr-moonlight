# Performance sessions — build-mode session log and how we use it

**First implementation (2026-09-18, MRM-84). Expect this to change a lot.** Carlos plays a build,
the game writes a scene-tagged log, Claude reads it, asks Carlos about the run, and records the
findings plus hypotheses. Two records are kept: **this file** (context for Claude inside the repo)
and the **Linear issue "Performance reports"** (the external history). Numeric history of individual
optimisations stays in `Docs/performance-log.md`; this file is the per-*run* record.

**The session log is not only for performance.** Carlos's stated purpose: any useful information that
helps at any time, especially bug fixing and analysis (it already caught a per-kill runtime warning and
will catch display-mode flicker). **Every change to the log system (new field, new line type, fixed bug,
changed behaviour) MUST be recorded in the changelog in section 7 AND as a comment on MRM-85.**
**Every *important change to the game* (anything that could plausibly change performance, behaviour or what the logs
show) MUST be recorded in the change record in section 8 AND as a comment on MRM-85** - this is part of the "run the
final instructions" routine (Carlos, 2026-09-19), so a regression can be traced back to the change that caused it.

Linear: **MRM-85** "Performance reports" - https://linear.app/mrmoonlight/issue/MRM-85/performance-reports-running-log-of-build-play-sessions (a living log issue: never closed, one comment per session).

---

## 1. Trigger and procedure

When Carlos says something like **"I'm going to play a build, check the performance"** (or "I ran it,
look at the logs"):

1. **Find the logs.** `%USERPROFILE%\AppData\LocalLow\Mustard Master Spark\MrMoonlight\Logs\session-<timestamp>.log`.
   One file per launch (Editor Play sessions write files there too: check the `[SESSION]` header line,
   `BUILD` vs `EDITOR`). Newest files first; ignore short Editor ones.
2. **Read** `[SCENE]` markers and `[SCENE] SUMMARY` lines first, then the `[PERF]` windows per scene,
   then `[ENEMY]` / `[PLAYER]` lines. Filter by the scene tag, e.g. `grep "\[Island_Legion\]"`.
3. **Ask Carlos about the run** before concluding (one short batch of questions; skip what the log
   already answers). Checklist: which build number; which scenes and in what order; weapons used;
   cheats/toggles on (CRT, fog, night, invulnerability, infinite ammo); fullscreen mode; where he went
   (biome/route); what it *felt* like and when (stutters, drops, a moment he noticed); anything
   running in the background.
4. **Write the entry** (template in section 4): numbers, conditions, what changed since the last
   session, findings, **hypotheses** with the evidence for/against and the cheapest test for each.
   Recent-change context comes from `git log`, the newest `Docs/*-sonnet-prompt.txt`, and the build
   folder name.
5. **Publish:** append the entry to section 5 of this file AND post it as a comment on the Linear
   issue. Update the "Open hypotheses" table in section 3.
6. Never conclude from one run. Say what would falsify each hypothesis.

## 2. What the log contains (SessionLog v4, see the changelog in section 7)

Code: `Assets/_Project/Code/Runtime/DevTools/SessionLog.cs` (self-installing, always on, tunables
`SessionLogPerfSampleSeconds` = 5 and `SessionLogPerfWarmupSeconds` = 2 in `MoonlightTunables`).
Frame Timing Stats is enabled in Player Settings (needed for the CPU/GPU columns).

| Line | Content |
|---|---|
| `[SESSION]` | header: build/editor, GPU/CPU/RAM, resolution, fullscreen mode, vSync, quality, frame timing on/off |
| every Unity log line | prefixed `HH:mm:ss.fffZ t=<s> f=<frame> [<active scene>]` |
| `[SCENE] ===== ACTIVE X =====` | scene switch; a `SUMMARY` for the scene just left precedes it |
| `[SCENE] SUMMARY X` | time in scene, frames (first 2 s excluded), avg fps, 1% low, worst frame, enemies spawned/killed/alive/corpses, kills by weapon |
| `[PERF] window` | every 5 s: avg/1% low/best fps, worst frame, CPU avg/max ms, GPU avg/max ms, draws / SetPass / triangles / batches, managed MB, player position, **biome** (dominant terrain layer), **weapon**, **`flashlight on/off handLights N`** (v4: flashlight state and how many world lights currently reach the player's hands), enemies alive by kind, corpses, spawned/killed this scene |
| `[ENEMY] SPAWN` / `KILL` | one line each, with `scene=`, the live counts, and on kills `by=` and `weapon=` |
| `[PLAYER] weapon -> X` | weapon switches |
| `[PLAYER] flashlight -> on/off` | every flashlight switch (v4, MRM-44) |
| `[APP]` | focus lost/regained (**fps while unfocused is not trustworthy**), quitting |

Known limits: weapon-per-kill is "the weapon equipped at the moment of death" (fine for hitscan and
close projectiles); **render stats (draws/SetPass/triangles) and CPU/GPU frame timing DO work in a
release build** (confirmed with build 36, 2026-09-18); `cpuFrameTime` includes time waiting on the GPU, so
read it together with `gpu`: when the two are nearly equal the frame is GPU-bound; the biome is the
dominant painted terrain layer, not a geometric region; scene-load `[PLAYER] weapon -> ?` lines are
harmless (the weapon is not spawned yet).

## 3. Open hypotheses (kept current)

| # | Hypothesis | Evidence so far | Cheapest test | Status |
|---|---|---|---|---|
| H1 | FPS falls with **kills** because corpses stay in the scene (`EnemyCorpseCleanup` never destroys the body) and their render cost piles up | **Strong (session 2).** SetPass calls climb from ~300-450 to ~1,800-2,200 as corpses go 0 -> ~50, then plateau, in BOTH scenes while the player stood in one area; GPU time ~= frame time (GPU-bound); alive count stayed 8-25 | A/B build: destroy or hide corpses after N s (or cap at ~10) and compare SetPass/fps at the same kill count | **supported, untested by A/B** |
| H2 | **Dense forest** costs a lot at rest | Draws 12-34k and 60-110M tris in Forest vs 2-8k draws and 25-50M tris in FlakTower/open ground; Island FlakTower at rest 137-162 fps vs Forest ~87 fps at similar corpses | Stand still in plains vs forest, no combat, log 30 s each | **supported** |
| H3 | Per-kill **gore/blood** (splash on every kill, dismemberment spill) accumulates render cost (particles, decals, mesh pieces) | Not separable from H1 in the current log (managed MB rises 14 -> 55 (Island), 30 -> 74 (Legion) with kills) | Log active particle systems / decal count; run with gore off | open |
| H4 | **Shotgun** costs more per kill than the AKM | **Weakened.** Island's AKM segment (kills 17-64) degraded as fast as Legion's shotgun segment (kills 20-98) | Same route, same kill count, each weapon | weakened |
| H5 | Legion's **non-convex wood MeshColliders** cost physics/CPU time | **Weakened.** Degraded windows are GPU-bound; CPU logic is not the limiter | Only matters if a later run turns CPU-bound | weakened |
| H6 | Legion's +47% vegetation (8,786 vs 5,990) costs render time | **Weak signal.** Whole-scene averages equal (Island 73.5 fps / Legion 79.4). At rest in FlakTower Legion showed ~3x the draws (7-8k vs 2.2-2.5k) and ~1.5x tris at ~20% lower fps, but at a different spot | Same spot, no combat, both scenes (needs a fixed benchmark position) | open |
| H7 | Corpses pile up **in front of the camera** (player fights from one spot) so every corpse is rendered with shadow passes and several materials | Consistent with the SetPass plateau ~50 kills, position nearly constant (222,107) / (380,172) | Set corpse renderers to no-shadow, or count visible renderers | open |
| H8 | Each kill **builds convex hulls at runtime** for gore pieces ("Couldn't create a Convex Mesh ... 256 polygons" warning, 36x Island / 47x Legion per 100 kills) | Warning count ~ kill count, source mesh name empty | Find which GoreSimulator/ragdoll path builds them; pre-bake or skip colliders on gore pieces | open |
| H9 | The **flashlight** (Spot, intensity 60, range 40, cookie, no shadows) plus lights on the hands costs GPU time, or its fog interaction does | None yet: added with MRM-44 (2026-09-18/19). One incidental Editor window pair (flashlight on 139.7 fps vs a slower loading window) is NOT comparable | Same spot, same scene, no combat: 30 s with the flashlight off, 30 s on; the `[PERF]` lines carry `flashlight on/off` since v4 | open |
| H10 | The hands' **world-light scan** (`MoonlightViewModelLighting.ScanWorldLights`, `FindObjectsByType<Light>` every 0.5 s) or many Spotter lamps on the hands (soft-shadowed Point lights within 15 m) costs CPU/GPU | None yet. Expected small | Compare `[PERF]` at the same kill count with `ViewModelWorldLightsEnabled` off; watch `handLights` in the line for how many lights reach the hands | open |

## 4. Entry template

```
### <date> — build <N> (<name>) — <scenes played>
Log files: <names>. Conditions: <fullscreen mode, vSync, CRT/fog/night, cheats, weapons, route>.
Recent changes since last session: <git log / handoff summary>.
Numbers: <per scene: time, avg fps, 1% low, worst frame, kills, corpses; notable windows>.
Carlos's notes: <what he saw/felt>.
Findings: <what the data shows, incl. bugs in the tool itself>.
Hypotheses: <new or updated Hn: evidence for/against, cheapest test>.
Next: <what to change or measure before the next session>.
```

## 5. Entries

### 2026-09-18 — build 35 (Legion Perf) — Island, Legion (SessionLog v1)
Log files: `session-20260918-184806.log` (run 1), `session-20260918-185300.log` (run 2).
Conditions: both runs night config, CRT scanlines, **no fog** (corrected by Carlos 2026-09-18; an earlier note
said fog), invulnerability, infinite ammo, 100-Spotter loop played to completion. **Run 1 fullscreen mode `FullScreenWindow`, run 2 `ExclusiveFullScreen`**
(confound). Run 1: Island, AKM then double-barrel shotgun, then back to the menu and ~5 s in Legion
before Alt+F4. Run 2: Legion only, mostly the shotgun, plains most of the time, dense forest at the end.
Hardware: RX 9070 XT, Ryzen 7 9800X3D, 63 GB RAM, 1920x1080 @ 75 Hz.

Recent changes since the previous build: MRM-84 Legion scene (copy of Island, Gaia vegetation stripped
and respawned with the 109 wood-MeshCollider prefabs; 8,786 instances vs 5,990; own NavMesh, 90.6k vs
75.7k tris), TMP font shader repair, SessionLog v1. Island itself unchanged.

| | Island (run 1) | Island_Legion (run 2) |
|---|---|---|
| Time in scene | 217 s | 157 s |
| Kills / spawns | 100 / 128 | 100 / 119 |
| Live enemies | 8-25 | 8-22 |
| fps, first windows | ~118 avg | ~113 avg |
| fps, kills 90-100 | ~36 avg, 1% low ~30 | ~29 avg, 1% low ~19-25 |
| Managed memory | 15 -> 56 MB | 29 -> 72 MB |
| Scene summary | avg 75.5, 1% low 32.1, worst 320 ms | avg 73.9, 1% low 26.9, worst 355 ms |

Findings: (1) fps degrades with kills, not with live enemy count, on both islands (H1). (2) Legion
drops sharply at ~t=132-142 s with kills flat (42 -> 58): consistent with entering the dense forest
(H2). (3) On plains at equal kills Legion is not clearly worse than Island. (4) Tool bugs found (fixed
in v2): scene-summary enemy counters were 0 (stats removed on unload before the summary), the menu
summary showed "10 alive" (next scene's Spotters spawn ~0.4 s before it becomes active), the first
window of a scene included the load stall.
Hypotheses: H1-H6 in section 3.
Next: run a build with SessionLog v2 (corpses, biome, position, weapon, draw stats, CPU/GPU time);
use the same fullscreen mode both runs; ideally the same route and weapon in both scenes.

### 2026-09-18 (evening) — build 36 (Session Log V2) — Island, then Legion (SessionLog v2)
Log files: `session-20260918-192546.log` (Island), `session-20260918-192949.log` (Legion).
Conditions: both `ExclusiveFullScreen`, vSync 1, 1920x1080 @ 75 Hz (same in both, good). Carlos: two
runs, Island first, Legion second, 100-Spotter loop each. Toggles (confirmed by Carlos): night, CRT, **no fog**,
invulnerability, infinite ammo, both runs. Both runs
used the whole arsenal (Island: AKM 62 kills, DBShotgun 23, M1911 9, HuntingRifle 6; Legion: DBShotgun
84, AKM 9, M1911 4, HuntingRifle 3). Island run ended by the scene **reloading itself** at t=234 s right
after the 100th kill (10 fresh Spotters, then quit); Legion run ended back on the main menu.
Recent changes: SessionLog v2 only (no gameplay or scene change since build 35).

| | Island | Island_Legion |
|---|---|---|
| Time in scene (warm-up excluded) | 202 s | 177 s |
| Summary avg / 1% low / worst frame | 73.5 / 26.8 fps / 44.5 ms | 79.4 / 25.2 fps / 52.9 ms |
| Kills / spawns | 100 / 119 | 100 / 119 |
| First windows (0-10 corpses, 10 alive) | 84-109 fps | 99-122 fps |
| Around kill 50-60 | ~30 fps, SetPass ~1,900 | ~33-48 fps, SetPass ~1,800 |
| Kills 90-100 | 28-37 fps, worst frame 38-44 ms | 27-30 fps, worst frame 46-53 ms |
| SetPass calls: start -> end | ~300-450 -> ~1,800-2,100 | ~290-400 -> ~1,800-2,230 |
| Triangles per frame | 25M (FlakTower at rest) to 130M | 27M to 113M |
| Managed memory | 14 -> 55 MB | 30 -> 74 MB |
| Warnings | 36x "Couldn't create a Convex Mesh" | 47x the same |

Findings
1. **GPU-bound.** In the slow windows GPU frame time is ~97% of CPU frame time (e.g. 34.9 ms GPU of 35.8 ms). The enemy AI/CPU logic is not the limiter, contrary to the working assumption.
2. **The cost tracks corpses/kills, on both islands, and shows up as SetPass calls** (~300-450 -> ~1,800-2,200), then plateaus around 50 kills while the corpse count keeps growing to 100. Alive count stayed 8-25 throughout.
3. **Dense forest is expensive at rest.** Island in FlakTower standing still with 7-11 corpses: 137-162 fps, ~2.2-2.7k draws, ~25M tris. Same island in Forest with 15 corpses: ~87 fps, ~16k draws, ~85M tris.
4. **Legion is not measurably worse overall** (avg 79.4 vs 73.5, same degradation curve). A weak at-rest signal (~3x draws in FlakTower, at a different spot) is worth a controlled test.
5. **Shotgun vs AKM shows no separate effect** (Island AKM kills 17-64 degraded as fast as Legion's shotgun kills 20-98).
6. **New find:** ~1 "Couldn't create a Convex Mesh ... 256 polygons" warning per 2-3 kills (36 and 47 per 100 kills): something builds convex hulls at runtime on kill (gore pieces or ragdoll).
7. Also seen: 1x "wanted 1 reinforcements but only placed 0" on Island (no NavMesh within 55 m); DOTween max-tweens warning on Legion; a URP shadow-atlas warning (pre-existing).
8. Tool: release builds DO report draw/SetPass/tri and CPU/GPU timing. v2 fixes worked (summaries carry enemies/corpses/kills-by-weapon; menu no longer shows phantom enemies).

Hypotheses: H1 supported, H2 supported, H4/H5 weakened, H6 weak signal, new H7 and H8 (section 3).
Questions for Carlos (answers to be appended below): see "Carlos's answers".
Next (cheapest experiments): (a) build with corpses destroyed/hidden after N seconds or capped, compare SetPass/fps at equal kill count; (b) stand-still benchmark: 30 s in plains and 30 s in forest, no combat, both scenes, same position; (c) find what builds convex hulls per kill (H8).

Carlos's answers (2026-09-18):
1. Toggles: night, CRT, no fog, invulnerability, infinite ammo, in both runs.
2. He noticed nothing on kills (no visible stutter or glitch): the slowdown is a steady one. His own suspicion: the blood/gore effects are not optimised (matches H1/H3/H7).
3. Scene switching: at one switch the game window **vanished for a split second (he saw the file system) and reopened** with the main menu. Log says: Legion -> MainMenu worked; the Island run instead reloaded Island at the end (`GameOverPanel` restart path: its Main Menu button targets "MainMenu", so a reload means Restart was pressed or wired; unexplained). Likely cause of the flicker: `MainMenuController.Awake` calls `SettingsPanel.ApplySavedDisplaySettings()` -> `Screen.SetResolution(..., ExclusiveFullScreen)` on EVERY menu load, which re-applies exclusive fullscreen. Suggested fix: only call it when the current width/height/mode differ from the saved ones. Logger v3 now writes `[APP] display changed` lines so the next run proves or disproves it.
4. He fought from a single spot per run, so corpses piled up around him: supports H7 (corpses in front of the camera).


## 6. Does the logger itself cost performance? (asked by Carlos, 2026-09-18)

Yes, but very little, and v3 now measures it. Editor measurement (v3, main menu, ~430 fps): **~1.5 us per
frame** for the logger's own per-frame work (frame-time list, render counters, frame timing) plus **one
~2 ms window write every 5 s** (sort + string build + file write). At 30 fps (33 ms frames) that is
~0.005% per frame, invisible next to the 3x slowdown seen in the sessions. Not included in that number:
the engine-side cost of Frame Timing Stats and the render ProfilerRecorders being enabled (small, not
separately measurable yet) and the per-line file write (~350 lines per run, synchronous flush, negligible).
Each `[PERF]` line now ends with `logger cost X us/frame, last window write Y ms` so every future run
carries its own answer. For the final release build set `MoonlightTunables.SessionLogEnabled = false`
(needs a restart) and turn Frame Timing Stats off in Player Settings.

## 7. Log system changelog (add a row for EVERY change, and comment on MRM-85)

| Version | Date | Build | Change |
|---|---|---|---|
| v1 | 2026-09-18 | 35 | First version. Every Unity log line copied to `session-*.log` with UTC time, frame and active scene; `[SCENE]` markers and per-scene summaries; 5 s `[PERF]` fps windows; `[ENEMY]` SPAWN/KILL with live counts; `[APP]` focus lines. Bugs: scene summaries lost their enemy counts, menu summary showed the next scene's enemies, first window included the load stall. |
| v2 | 2026-09-18 | 36 | Added corpses, player position and biome (dominant terrain layer), equipped weapon, killer + weapon on kills, `[PLAYER]` weapon switches, draws/SetPass/triangles/batches, CPU/GPU frame time (Frame Timing Stats turned on in Player Settings), managed memory; per-scene enemy stats keyed by scene handle (fixes v1 bugs); 2 s scene warm-up excluded (`SessionLogPerfWarmupSeconds`); `EnemyIdentity.AnySpawned/AnyDespawned` hooks; kills-by-weapon in scene summaries. |
| v3 | 2026-09-18 | not in a build yet | `[APP] display changed` lines (resolution / fullscreen-mode switches, to catch window flicker); logger self-cost on every `[PERF]` line; `SessionLogEnabled` master switch in `MoonlightTunables`. |
| v4 | 2026-09-19 | not in a build yet | `[PLAYER] flashlight -> on/off` lines and `flashlight on/off handLights N` at the end of every `[PERF]` window (state of the player's flashlight and how many world lights currently reach the hands), so a performance window can be read against the light state (H9, H10). Code: `SessionLog.TryResolveLighting`, `MoonlightViewModelLighting.WorldLightsOnHands`. Change C-001 in section 8. |

Ideas not done yet: particle-system and decal counts per kill, active/visible renderer counts, a physics
step time, a fixed stand-still benchmark trigger, log rotation (old files pile up in `Logs/`).

## 8. Change record — important changes to the game, so problems can be traced back

Started 2026-09-19 (Carlos: *"a record of every time we introduce new changes, so we can refer back to them when we
implement the log system"* / trace down problems). **One row per important change, added by the "run the final
instructions" routine** (`CLAUDE.md` §"Run the final instructions", step 4). It is the bridge between a log line and
the change that may explain it: find the date of the odd behaviour in a session log (`session-<timestamp>.log`), then
read this table to see what changed just before.

Columns: **ID** (C-nnn, never reused) | **Date** | **Branch / commit** | **What changed** | **What it could show up as in the logs** | **Read more** | **Linear**.
The commit hash is filled in **after Carlos commits** (he uses GitHub Desktop): Claude reads it with `git log -1` (Carlos can just
say "I committed"); until then the row says `pending` plus the suggested commit message file. To find the commit of any file later:
`git log --oneline -- <path>`.

| ID | Date | Branch / commit | What changed | Could show up in the logs as | Read more | Linear |
|---|---|---|---|---|---|---|
| C-001 | 2026-09-18 / 19 | `mrm-44`, commit **`e99866fd`** (2026-09-19 10:07, message `Docs/mrm44-commit-message.txt`; the earlier first-iteration commit is `777b0ceb`). **Both commits went straight to `main`, not through a PR**: `mrm-44` had been created tracking `origin/main`, so GitHub Desktop's push updated `main`. Fixed 2026-09-19 (`--unset-upstream`); see CLAUDE.md | **Flashlight and player lighting (MRM-44).** New flashlight Spot Light on F (intensity 60, range 40, cookie, shadows off) with tunables + live tuning. First-person hands and weapons moved to a URP `ViewModel` rendering layer; a dedicated directional light follows the live sun (floor at night); world lights within 15 m (lamps, flares, fires) also light the hands via a 0.5 s scan (`FindObjectsByType<Light>`). 21 HQ mask maps restored (guns PBR), arm materials matte. SessionLog v4. Docs unified into `lighting.md` | Extra realtime lights (flashlight + hands' light) in `draws`/GPU time; flashlight-on windows (`flashlight on`); a 0.5 s CPU spike pattern if the scan is costly (H10); `handLights N` rising near Spotter lamps; 18 extra small textures in memory (`managed` MB barely moves) | `Docs/lighting.md`, `Docs/viewmodel-light-layers.md`, `changelog.md` (MRM-44 entries) | MRM-44 (main record), MRM-85 (this record), MRM-9 (hand/weapon materials, cross-issue), MRM-67 (fog + flashlight look). Comments posted 2026-09-19 |
| C-002 | 2026-09-19 | `mrm-44`, commit **pending** (message: `Docs/mrm44-commit-message-2.txt`) | **Process and docs, no runtime code.** Rule: branches are created with `--no-track` so GitHub Desktop cannot push to `main` (both MRM-44 commits went to `main` because `mrm-44` tracked `origin/main`; `mrm-44` fixed, `mrm-86` created clean). Final-instructions step 4 (this change record). New issue **MRM-86** for the lighting rework | Nothing in the logs. If a session log is dated after 2026-09-18 20:21 and built from `main`, it already includes C-001 | `CLAUDE.md`, `Docs/lighting.md`, `Docs/lighting-rework-opus-prompt.txt` | MRM-86 (new), MRM-44, MRM-85 |
| C-003 | 2026-09-21 | `mrm-87`, commit **pending** (message: `Docs/mrm87-commit-message.txt`) | **Syringe on V (MRM-87).** Heal key H to V; usable at any health (full-health gate removed); hands locked while a heal runs (no weapon switch, no throw, fire cannot cancel); Syringe StackSize 1 to 3 and added to `MoonlightInfiniteThrowables` so it is unlimited; 4 vendor wieldable scripts edited (`WieldableHealingHandler`, `HealingWieldable`, `WieldableInventory`, `WieldablesController`). Keyboard binding on `EquipMelee` removed (that action closed the not-yet-built `InventoryUIController`). New doc for adding hand-held items and weapons | No dedicated log line (the heal handler logs nothing). Indirect: a player-health step of +50 about 2 s after the key press even at full health (the value is capped, so it may look like no change); a 2 s window in which weapon switches, throws and fire do nothing (input present, no weapon change); Syringe count never falls below 3 in the holster. A stuck lock would show as weapon input being ignored indefinitely with no heal in progress (would mean `HealingWieldable.IsHealing` stuck true: check the heal routine and `OnDisable`). No perf impact expected | `Docs/hands-items-and-weapons-pipeline.md`, `Docs/controls.md`, `Docs/mrm25-weapon-test-arsenal.md` (sections 6 and 8) | MRM-87 (main record), MRM-85 (this record), MRM-25 + MRM-9 (cross-issue: arsenal and vendor wieldable code), MRM-41 (`InventoryUIController` binding), MRM-81 (umbrella) |

**How to fill a row:** ID, date, branch and hash, one paragraph of *what actually changed*, the specific log lines,
fields or hypotheses it could explain (name the `Hn` rows in section 3), and links to the docs and Linear comments. Keep
it factual; interpretation goes in the session entries (section 5).

---

Linear: MRM-85, entry posted there as a comment on 2026-09-18.
