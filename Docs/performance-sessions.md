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
| H1 | FPS falls with **kills** because corpses stay in the scene (`EnemyCorpseCleanup` never destroys the body) and their render cost piles up | **Strong (session 2).** SetPass calls climb from ~300-450 to ~1,800-2,200 as corpses go 0 -> ~50, then plateau, in BOTH scenes while the player stood in one area; GPU time ~= frame time (GPU-bound); alive count stayed 8-25 | A/B build: destroy or hide corpses after N s (or cap at ~10) and compare SetPass/fps at the same kill count | **supported, A/B pending: build 43 (2026-09-30) carries the corpse optimization, see C-012 and `corpse-optimization.md`** **Builds 38-42 (2026-09-30): frame ~9.7 ms at 0 corpses -> 24-29 ms at 60-100 corpses with SetPass ~600 -> ~2,000, the same in builds with and without culling.** |
| H2 | **Dense forest** costs a lot at rest | Draws 12-34k and 60-110M tris in Forest vs 2-8k draws and 25-50M tris in FlakTower/open ground; Island FlakTower at rest 137-162 fps vs Forest ~87 fps at similar corpses | Stand still in plains vs forest, no combat, log 30 s each | **supported** |
| H3 | Per-kill **gore/blood** (splash on every kill, dismemberment spill) accumulates render cost (particles, decals, mesh pieces) | Not separable from H1 in the current log (managed MB rises 14 -> 55 (Island), 30 -> 74 (Legion) with kills) | Log active particle systems / decal count; run with gore off | open |
| H4 | **Shotgun** costs more per kill than the AKM | **Weakened.** Island's AKM segment (kills 17-64) degraded as fast as Legion's shotgun segment (kills 20-98) | Same route, same kill count, each weapon | weakened |
| H5 | Legion's **non-convex wood MeshColliders** cost physics/CPU time | **Weakened.** Degraded windows are GPU-bound; CPU logic is not the limiter | Only matters if a later run turns CPU-bound | weakened |
| H6 | Legion's +47% vegetation (8,786 vs 5,990) costs render time | **Weak signal.** Whole-scene averages equal (Island 73.5 fps / Legion 79.4). At rest in FlakTower Legion showed ~3x the draws (7-8k vs 2.2-2.5k) and ~1.5x tris at ~20% lower fps, but at a different spot | Same spot, no combat, both scenes (needs a fixed benchmark position) | open |
| H7 | Corpses pile up **in front of the camera** (player fights from one spot) so every corpse is rendered with shadow passes and several materials | Consistent with the SetPass plateau ~50 kills, position nearly constant (222,107) / (380,172) | Set corpse renderers to no-shadow, or count visible renderers | **implemented in build 43 (`CorpseShadowsOff`, culling), result pending** |
| H8 | Each kill **builds convex hulls at runtime** for gore pieces ("Couldn't create a Convex Mesh ... 256 polygons" warning, 36x Island / 47x Legion per 100 kills) | Warning count ~ kill count, source mesh name empty | Find which GoreSimulator/ragdoll path builds them; pre-bake or skip colliders on gore pieces | open |
| H9 | The **flashlight** (Spot, intensity 60, range 40, cookie, no shadows) plus lights on the hands costs GPU time, or its fog interaction does | None yet: added with MRM-44 (2026-09-18/19). One incidental Editor window pair (flashlight on 139.7 fps vs a slower loading window) is NOT comparable | Same spot, same scene, no combat: 30 s with the flashlight off, 30 s on; the `[PERF]` lines carry `flashlight on/off` since v4 | open |
| H10 | The hands' **world-light scan** (`MoonlightViewModelLighting.ScanWorldLights`, `FindObjectsByType<Light>` every 0.5 s) or many Spotter lamps on the hands (soft-shadowed Point lights within 15 m) costs CPU/GPU | None yet. Expected small | Compare `[PERF]` at the same kill count with `ViewModelWorldLightsEnabled` off; watch `handLights` in the line for how many lights reach the hands | open |
| H11 | **Tree fires** (60 burning, 10 real lights) cost GPU time at rest | **Supported, refined.** Fountain (old start, build 38): -28 fps / +2.6 ms. Dense-forest start: A (no culling) +3.1 ms (9.32 -> 12.42), C (FullDisable) +2.2 ms (6.89 -> 9.11): the cost shrinks when fewer trees are drawn (less lit geometry). SetPass +150-180, draws and tris unchanged by the fires | Vary `maxFires` / light count / base fire at the same spot | **supported** |
| H12 | **Occlusion culling of the 8,786 GameObject trees** (AST-145 Dynamic) cuts draws/tris in forest views | **Supported in the dense forest (builds 40-42, 2026-09-30).** Spawn still +13% (KeepShadows) / +19% (FullDisable), Fountain still +31% / +35%, circuit +13% / +17%, draws -66% to -87%; FullDisable beats KeepShadows by 3-5%. Fight not separable (corpses dominate). Earlier: build 39 glade +15%, forest views +53-95% | Decide B vs C once shadows in C are confirmed fine; FullDisable vs KeepShadows visual check | **supported** |
| H13 | **Dynamic lights** (alive Spotter lamps + dropped lamps burning 22 s after each kill + 10 fire lights, several with soft shadows) now limit the frame at 60-100 corpses, not the corpse geometry | **Supported by build 43, strongly by build 44 (2026-09-30): with corpses held at 14-21 by the dissolve the frame stayed 23 ms at 50 lights.** After the corpse geometry fell 40% (draws) the frame stayed 24.4 ms; `handLights` ~52 in both builds; URP reduced the additional-light shadow atlas at start; regression: ms ~ 4.5 + 1.1 per 1000 draws + light term | Same fight with lamp-light shadows off (one flag), or with corpse dissolve on (C-013, removes dropped lamps after ~17 s) and compare `handLights` and ms | **strongly supported by build 44 (R2 0.94, ~0.3 ms per light); next: lamp shadows off / light cap** |
| H14 | **Volumetric Fog & Mist 2 vs HAZE**: which fog is cheaper at the same look (static phases, then the fight) | **Unmeasured.** Editor only: scene 08 (VF2) ran 135-147 fps / 6.8-7.4 ms and scene 07 (HAZE) similar, but the editor is slower than a build and the two were not run in the same conditions | Build 45 with scenes 07 (HAZE) and 08 (VF2): same start spot (218.06, 33.37, -5.23, yaw 311.3), spawn still 10 s, Fountain fires off 30 s, fires on 30 s, then the fight; run A = HAZE, B = VF2 (matched look), C = fog off (F6) | **open: parked 2026-10-01 for the demo task; resume from `Docs/mrm85-fog-vf2-continue-prompt.txt`** |

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


### 2026-09-30 (evening) — builds 37-39 (Lighting Baseline, Unlocked FPS, Culling Test) — LightingTestScene (SessionLog v5)
Log files: build 37 `session-20260930-145704.log` (and earlier 14:36-15:05 files), build 38 `session-20260930-152234.log`. Conditions: Windows build, 1920x1080
exclusive fullscreen, Ryzen 7 9800X3D / RX 9070 XT, vSync 0 cap -1 (build 38 only), weather "Story 6 - Fountain", cheats: enemy invincibility + infinite ammo in the last phase.
Recent changes since last session: commit `8ecee2b4` (tree fire tuning) + SessionLog v5 + `DisplayBootSettings` (C-010). AST-145 imported but unused in these builds.
Numbers: build 37 sat at **75.0 fps in every window** (avg, 1% low and best all 75.0): vSync on a 75 Hz monitor, not a cap. Build 38 (vsync 0): circuit 85-208 fps; **Fountain still, fires off 118.1 fps
(8.5 ms, GPU 8.2, 19.6k draws, 335 SetPass, 89.2 M tris); fires on 89.8 fps (11.1 ms, 19.8k draws, 486 SetPass)**; walking and killing 58-122 fps, ~60 at 100 corpses (SetPass 1,780).
Worst frames 151.8 ms (enemies switched on), 96 ms, 48 ms (fires switched on).
Carlos's notes: played the order fountain still 30 s, fires on 30 s, enemies + invincibility + infinite ammo, walk and kill.
Findings: (1) the 75 fps lock was Polymind's forced vSync in any scene other than MainMenu (fixed, C-010). (2) Tree fires cost -24% fps at rest (H11). (3) Draw calls depend on view direction
(2.9k-31.6k), the target for occlusion culling (H12). (4) SessionLog v5 bug: `[WORLD] weather` fired on every blend frame (675 lines); fixed after build 38, verified in build 39's code. (5) Raw build size
1.12 GB (zip 473 MB); build 24 was 446 MB raw: not investigated yet. CPU ms ~= GPU ms: GPU-bound throughout.
Hypotheses: H11 supported, H12 testing, H1 consistent again (corpses 0 -> 100 with SetPass 490 -> 1,780).
Next: Carlos plays build 39 on the same route; compare with `Docs/culling-ast145.md` section 4; check pop-in, shots vs layer 29, tree fires on culled trees.

### 2026-09-30 (night) — build 39 (Culling Test) — LightingTestSceneCullingTest (SessionLog v5.1)
Log file: `session-20260930-153715.log` (280 s, 27,126 frames, avg 97.5 fps, 1% low 35.5, worst frame 51.5 ms, 129 spawned / 100 killed / 100 corpses, AKM). Conditions: same as build 38 (vsync 0 cap -1, 1920x1080 exclusive fullscreen,
9800X3D / RX 9070 XT), AST-145 Dynamic, KeepShadows, 8,786 trees, 1500 rays. Route: same start circuit, Fountain 30 s still, fires on 30 s, then AKM / infinite ammo / vulnerability and a fight in a **tight, dense forest** (build 38's fight was on a plain).
Recent changes since build 38: log flood fix; the culling scene itself. Numbers: see `Docs/culling-ast145.md` section 5 (tables A-C). Fountain still: fires off 136.3 fps vs 118.1, fires on 99.6 vs 89.8, draws 5.1k vs 19.6k.
Circuit forest views: 31k draws / 85-89 fps -> 5k / 136-167 fps. Fight: 33-110 fps, worst 18.5k draws and 100 M tris at 33-44 fps with 60 fires, 20 alive, up to 100 corpses: not comparable to the plain fight.
Findings: culling clearly helps forest views; draw-call swing with view direction is gone (max 6.6k); tris only fall 12% at the fountain so most of the 78 M tris are not these trees; no culling errors; no logger cost; combat still GPU-bound.
Hypotheses: H12 supported (dense-forest A/B pending), H2 and H1 visible again in the fight but confounded by the area.
Next: Carlos's visual answers (pop-in, bullets into canopies, enemy sight, fires on hidden trees); fixed dense-forest A/B; FullDisable variant; per-object triangle census.

### 2026-09-30 (late night) — builds 40, 41, 42 (Forest A no culling, B KeepShadows, C FullDisable) — dense-forest A/B/C (SessionLog v5.1)
Log files: A `session-20260930-160202.log` (Carlos ran A twice, the later one is used), B `session-20260930-160731.log`, C `session-20260930-161241.log`. Conditions: new start (218,35,-5) in a dense forest, circuit moved there, vsync 0 cap -1,
1920x1080 exclusive fullscreen, 9800X3D / RX 9070 XT; each run: spawn still, circuit, Fountain still (fires off, on), AKM + infinite ammo + vulnerability fight to 100 kills. C: Alt-Tab at t=112-115 (log `window LOST focus`).
Recent changes since build 39: the start moved, build C (FullDisable) added. Numbers: `Docs/culling-ast145.md` section 10. Spawn still A 111.7 / B 126.1 / C 132.9 fps; Fountain still fires off 107.3 / 140.6 / 145.1; fires on A 80.5 / C 109.7 (B unreliable);
circuit 113.9 / 129.1 / 132.7; whole session avg 81.0 / 95.4 / 96.8 fps. Fires cost 3.1 ms in A, 2.2 ms in C. Fight, by corpse count (different spots): 0-9 corpses 94/103/103 fps, 60-100 corpses 38/34/42 fps, SetPass ~2,000 in all three.
Carlos's notes: no blinking trees, nothing odd, enemies and fires normal (build 39); tried to keep all three runs under the same conditions.
Findings: (1) culling gains hold and grow in the dense forest (+13-19% spawn, +31-35% Fountain). (2) FullDisable beats KeepShadows by 3-5%. (3) **Corpses dominate**: ~9.7 ms at 0 corpses to 24-29 ms at 60-100, SetPass 600 -> 2,000, identical in A, B, C: culling cannot help there. (4) Fewer drawn trees also lowers the fire cost (3.1 -> 2.2 ms). (5) C's 327/301 ms frames were an Alt-Tab, not the game. (6) yaw is not logged, so a still player who looks around changes the numbers (B's fires-on windows).
Hypotheses: H12 supported (dense forest now measured); H1 strongly supported (same SetPass per corpse count in all builds); H11 refined (fire cost depends on what is drawn).
Next: decide B or C once Carlos confirms no shadow problems in C; attack the corpses (H1/H7 test: no-shadow / cap / cleanup, A/B at the same kill count); add camera yaw to `[PERF]`; census of the remaining ~70 M triangles.

### 2026-09-30 (evening) — build 43 (Corpse Opt) — LightingTestScene (SessionLog v5.2)

Build 43 = commit `e95455f2` + corpse optimization (C-012). Run: log `session-20260930-165159.log`, 337 s, 100 kills (AKM), same start spot and plan as build 42 (C). Session avg 96.0 fps (build 42: 96.8), 1% low 36.9 (33.0), worst frame 54.6 ms.

| Corpses | fps 42 -> 43 | draws | SetPass | ms 42 -> 43 | handLights |
|---|---|---|---|---|---|
| 0-9 | 104.2 -> 100.6 | 4.5k -> 4.7k | 591 -> 611 | 9.7 -> 10.1 | 3.7 -> 4.4 |
| 10-29 | 93.0 -> 83.8 | 5.6k -> 5.4k | 768 -> 712 | 11.0 -> 12.2 | 8.3 -> 9.5 |
| 30-59 | 56.0 -> **67.9 (+21%)** | 10.7k -> **6.0k (-44%)** | 1604 -> **980 (-39%)** | 18.2 -> 15.8 | 36 -> 27 |
| 60-100 | 43.1 -> 41.2 | 15.4k -> **9.1k (-41%)** | 2006 -> **1551 (-23%)** | 24.0 -> 24.4 | 53 -> 52 |

- **The geometry side worked:** culling registered 88 of 100 corpses, about 57% of settled bodies switched off at a time (`culling 50/88`); draws and SetPass fell 23-44% at 30+ corpses and the 30-59 bucket gained 21% fps.
- **The frame did not get faster at 60-100 corpses (24.4 ms, same as before) although draws fell 41%.** Frame time fits `ms = 4.5 + ~1.1 per 1000 draws + a light term` in both builds (R2 0.97, but draws, lights and corpses move together, so this is a hint, not proof).
  The light term is big at the end: `handLights` stays ~52 (20 alive lamps + ~20 dropped lamps still burning, a kill about every 1 s x a 22 s lamp life, + 10 fire lights), and URP logged at the start that it had to shrink the additional-light shadow maps to fit 12 into the 2048 atlas. **New hypothesis H13.**
- Two user observations from the run: a tree that popped in (nothing in the log; possible cause: the corpse proxy boxes block culling rays to trees behind them, test with `CorpseCullingEnabled` off) and an enemy that stood upright as a corpse (nothing in the log; suspect the settle pass freezing the Animator on a fixed timer, better to wait until the pose is still). Not reproduced in Play Mode (test corpses fell flat).
- 9 `NullReferenceException`s at quit only, from the culling asset's observer (`DC_SingleSource.RemoveCullingTarget` after its source was already destroyed on shutdown). Harmless.
- Superseded the same evening by C-013 (corpse dissolve and removal), which removes corpses and their lamps after ~17 s: the next run of this fight will show whether H13 (lights) or H1 (corpses) was the limiter.

---
### 2026-09-30 (night) — build 44 (Corpse Dissolve) — LightingTestScene (SessionLog v5.3)

Build 44 = commit `f393402a` (C-012, C-013). Log `session-20260930-175705.log`, 320 s, same plan as builds 42/43 (still, circuit, Fountain 30 s fires off, 30 s on, then AKM fight to 100 kills). Session avg **102.6 fps** (42: 96.8, 43: 96.0), 1% low **40.0** (33.0, 36.9), no exceptions at all (the quit-time NullReferenceExceptions of build 43 are gone), managed memory 41 -> 65 MB (build 43 ended near 70).

- **Static phases unchanged, as expected** (the new shader feature is off for normal materials): Fountain still fires off 151 fps / 6.6 ms / 3.1k draws (build 42 C: 145 / 6.9 ms); fires on 105 fps / 9.5 ms (109.7 / 9.1). Fires cost +2.9 ms here (+2.2 in 42), within run-to-run noise.
- **Corpses stayed low:** 85 of 100 kills were already dissolved and freed at the end; corpses on screen never exceeded 21 (build 43: 100). `freed` after 3.0 s of dissolve 57 times (body not on screen, burn skipped) and 6.0 s 27 times (watched: 3 s body + 3 s lamp). 30 of 91 bodies were `seen True`. `CorpseCullingHooks` registered/culled are now near 0 because bodies live ~17 s.
- **But the frame at the end of the fight is the same:** kills 85-100: 42-44 fps, 22.8-23.9 ms, SetPass 1.5-1.7k, draws ~10k, with only 14-21 corpses (build 43: 24.4 ms at 60-100 corpses; build 42: 24.0). **Corpses were not what limited the frame at the end.**
- **The light count is:** over the fight windows with 10+ enemies alive, frame time vs `handLights` has R2 0.94 in both builds 43 and 44 with ~0.29-0.32 ms per light and an intercept of ~8.6-9.0 ms (build 42: 0.24 ms, R2 0.67); `handLights` rose from 1 to 51 as the fight went on (20 alive Spotter lamps + dropped lamps burning up to ~17 s before the dissolve dims them + 10 fire lights). Draws follow the lights too (~10k draws at 50 lights, ~3.5k at 0). **H13 supported strongly.** Caveat: `handLights` is lights within reach of the hands, a proxy for lights near the player; alive count and kill rate move with it.
- Compared with build 43 at the same kill counts the mid-fight is better (kills 30-70: ~16-22 ms with 13-17 corpses, vs 16-24 ms), but the late fight is not: the lights, not the bodies, are the cost.
- Worst frames: 129 ms at t=53 (weather transition to Fountain, not the fight), 55 ms at t=93 (tree fires switched on), 50 ms at t=143 (enemies back). No spikes at `freed` time.
- Not judged here: the burn look (Carlos to say what it looked like in the build), shadow pop from culled trees (not reported again).
- **Next steps for H13:** one build with the lamp lights' shadows off (alive and dropped), then a cap on simultaneous lamp lights (nearest N, the rest unlit or shadowless); both are lighting decisions (`Docs/lighting.md`).

---
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
| v5 | 2026-09-30 | not in a build yet | `treeFires N fireLights M` at the end of every `[PERF]` window: tree fires burning and fire point lights on (Lighting Test Scene tree fire experiment, I key; 0 elsewhere). `[MRM-86] Tree fires built/ON/OFF` lines come from the component. Code: `SessionLog.LightingState`, `TreeFireToggle.BurningCount/LitCount`. See `Docs/tree-fire-experiment.md`. |

| v5.1 | 2026-09-30 | 38, 39 | `weather <current> (-> <target> <blend>)` at the start of the `LightingState` part of every `[PERF]` window; `[WORLD] weather -> X` line on every weather change; `[WORLD] tree fires -> on/off (N burning)` line; `vsync N cap N` in every `[PERF]` window (the `[SESSION]` header may run before `DisplayBootSettings` and show the old vSync). **Bug in build 38:** the weather line was keyed on the blend value and fired every frame of a blend (675 lines); keyed on current|target names from build 39 on. Code: `SessionLog.CheckWorldState/WeatherState/LightingState`. |
| v5.2 | 2026-09-30 | 43 | The enemy part of `[ENEMY]` lines and the `[PERF]` window reads `corpses N (settled S, culling C/R)`: `S` = corpses that finished the settle pass (`CorpseOptimizer.SettledCount`), `R` = corpses registered with the AST-145 corpse culler, `C` = corpses whose renderer is switched off right now (`CorpseCullingHooks`). Code: `SessionLog.EnemyCounts`. |
| v5.3 | 2026-09-30 | not in a build yet | `corpses N (settled S, dissolved D, culling C/R)`: `D` = corpses burnt away and destroyed this session (`CorpseOptimizer.DissolvedCount`); `corpses N` is now the live count (destroyed corpses are dropped from the tally). Two `[CORPSE]` lines per dissolved corpse (`dissolve body ... seen ..., duration ...`, `freed ... after ...s of dissolve`). |

Ideas not done yet: camera yaw in `[PERF]` (a still player who looks around changes draws/tris), particle-system and decal counts per kill, active/visible renderer counts, a physics
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
| C-004 | 2026-09-21 | `main` (Carlos works on main), commit **pending** (message: `Docs/mrm84-commit-message-2.txt`) | **Leaf-card colliders removed from 4 trees (MRM-84 follow-up).** Carlos repainted `AP_Tree_04_GTree01_03_SM`, `AP_Tree_04_PTree_02_SM`, `AP_Tree_Blackpoplar01_SM`, `AP_Tree_10_ArgassTree_SM` in Technie so no leaf-card triangles are wood; `WoodColliderTool` rebuilt their wood MeshColliders (6 prefabs re-run, 6/6 verify + ray test OK). Wood collider tri counts changed slightly (GTree01_03 mesh is the biggest change: 40 leaf tris gone). New read-only editor tool `WoodColliderLeafAudit` (no runtime code). No NavMesh rebake (removed cards were 3.5 m+ up) | Nothing in the logs directly; no runtime code changed. Indirect: fewer bullet/flare/melee impacts on invisible mid-air surfaces in the crowns of these 4 species on `Island_Legion` (a shot through canopy now passes); physics query cost per tree slightly lower. A Spotter losing line of sight through those crowns would now see through (LOS raycasts hit tree colliders). NavMesh and pathing unchanged | `Docs/tree-collider-tracker.md` (Leaf-card audit section), `Docs/technie-vegetation-collider-process.md` (Leaf-card colliders found and removed) | MRM-84 (main record), MRM-85 (this record), MRM-81 (umbrella) |

| C-005 | 2026-09-21/23 | `main`, commit **pending** (message: `Docs/asset-index-and-mrm86-prep-commit-message.txt`) | **No Mr. Moonlight game code or assets changed.** Two sessions of Playground-only groundwork for the upcoming MRM-86 weather/skybox/illumination rework, plus asset-index housekeeping: (1) renamed 13 new downloads into `01_DOWNLOAD`, corrected AST-116 Technie's spreadsheet row (it was already installed and in use for MRM-84, the sheet said "not yet in project"); (2) in the **Playground** project only — imported AST-054 Skybox Blender / AST-147 Asset Optimizer Pro / AST-164 Ether Skyboxes, fixed AST-054's demo scenes for the Input System, added a mouse-look and a sky-rotation script, converted all 218 Ether `Skybox/6 Sided` materials plus 2 AllSky orphans to `Skybox/Cubemap` (218 of AllSky's 220 already had one), and built a new **Skybox Reviewer** scene (`Assets/PLAYGROUND/AST-054/Demos/Skybox Reviewer.unity`) that pages through all 438 combined skies (E/Q) with a persistent approve/reject log (O/P, JSON file, survives Play Mode restarts). Carlos will use it to pick skies for MRM-86, starting next session. Only two files in this repo touched, both docs | **Nothing** — no Mr. Moonlight runtime code, scene, prefab or shipped asset changed. A session log dated in this window is unaffected by this row; if MRM-86 work starts changing `TimeManager`/`SkyboxSwitcher`/the sun in a later session, that gets its own change-record row | `Docs/asset-import-update-process.md` (both sessions' worked examples), `Docs/lighting-rework-opus-prompt.txt` (2026-09-23 addendum) | MRM-86 (comment posted 2026-09-23, flagged as prep), MRM-85 (this record) |
| C-006 | 2026-09-28 | `main`, commit **pending** (message: `Docs/sky-catalog-commit-message.txt`; Carlos was committing it right after the session) | **Sky catalog imported (no runtime code).** 20 unique skies and 27 act materials (7 story parts, 20 Legion acts) added under `Assets/_Project/Art/Environment/Skies/` (`01_Story`, `02_Legion`, `Textures`), chosen by Carlos in the Playground Skybox Reviewer. 7 AllSky PNGs copied with their reviewed import settings; 12 Ether / custom skies re-rendered as flipped 6-face strip PNGs (a native `.cubemap` would have been 700 MB of text); one 16 px pure-black `.cubemap`. Nebula Red's yaw 0 / pitch -90 / roll 56 is baked into its texture. About 237 MB of new PNG through Git LFS. **Nothing references the new materials**: no preset, `SkyboxSwitcher`, scene or prefab changed, and the old `Environment/Skyboxes/` folder is untouched. Also Playground-only: the reviewer got Yaw/Pitch/Roll orientation sliders, a per-sky origin file, a blend-shader change, and 9 custom skies | **Nothing** while the materials stay unreferenced (Unity leaves unreferenced assets out of a build). Once MRM-86 wires them: about 365 MB more GPU-compressed texture data in the build (`managed` MB is unaffected, textures count as native/GPU), longer scene-load or first-swap time on a skybox change, and a hitch on the first frame a new sky is shown (texture upload; skybox swaps are instant by design). A wrong-looking sky after wiring would point at the strip import (face flip) or the Tint 0.5 / 0.5038 convention in `sky-catalog.md` | `Docs/sky-catalog.md`, `Docs/lighting.md` (§1.1 row, §8), `Docs/asset-import-update-process.md` (worked example 6), `Docs/lighting-rework-opus-prompt.txt` (2026-09-28 addendum) | MRM-86 (comment posted 2026-09-28: assets ready, cross-issue prep), MRM-85 (this record) |
| C-007 | 2026-09-29 | `mrm-86`, commit **`6ba4d414`** (2026-09-29 15:21, message `Docs/mrm86-commit-message.txt`); follow-up **`4b03e945`** (2026-09-29 15:25): the O-key component added to `LightingTestScene` after the commit (message `Docs/mrm86-commit-message-2.txt`) | **Weather profiles + Lighting Test Scene (MRM-86 steps 1-2).** New `LightingTestScene.unity`, a copy of Island_Legion with TimeManager, SkyboxSwitcher and Scene Effects Toggle removed from the copy and its own fog profile; a weather circuit (cylinder + DDT debug spheres) 58 m south of the Glade. New `WeatherProfiles.asset` (27 profiles, one per story part / Legion act: sky, sun, environment, special light sources lamps/flares/moon, HAZE fog), all at clean defaults; `WeatherSystem` blends them by distance and saves live values (P panel, editor only). New code: `World/Weather/*`, `SkyBlender`, `SkyProximityCircuit`, `DevTools/EnemyVisibilityToggle` (O). Vendor: AST-271 Draw Debug Tools (tracked, debug camera F9 to F11) and AST-054's blend shader. **Touches the real game in only two ways:** a `WeatherLightSource` marker on `Prop_Lamp` (so every Spotter lamp), `VFX_Flare` and `Prop_Moon` (inert without a WeatherSystem), and `MrMoonlight.Runtime.asmdef` now references `DrawDebugTools` and `Haze.Runtime`. Island, Island_Legion, `VP_HazeGlobalFog` and the old sun/time code are unchanged | In Island / Island_Legion: nothing expected. If a Spotter lamp or flare looks different there, suspect the new marker (it should do nothing without a WeatherSystem). A compile error mentioning `Haze.Runtime` or `DrawDebugTools` means a clone without the git-ignored HAZE folder (AST-078). In a Lighting Test Scene session log: `[MRM-86] Weather locked: <name>` lines, `[MRM-86] Enemies hidden (n)` / `Enemies back` (the O key; the session log also shows those enemies as despawn/spawn lines, not deaths), frame dips while a blend runs from the ambient recompute every 0.25 s (`WeatherAmbientRefreshSeconds`), and the known shadow-atlas warnings from Spotter lamps | `Docs/weather-profiles.md`, `Docs/lighting.md` §8, `Docs/controls.md`, `Docs/external-assets.md` | MRM-86 (comment 2026-09-29), MRM-85 (this record) |
| C-008 | 2026-09-30 | `mrm-86`, commit **`94976b95`** (2026-09-30 12:37, message `Docs/mrm86-commit-message-3.txt`) | **Weather control board, Tree Fires light, demo intro out of the test scene, tree fire experiment (MRM-86).** (1) `WeatherSystem` on the `Weather` object is now a control board: a live profile in its Inspector with every section, applied to the real sun/ambient/lights/fog section by section, Save everything / lighting / fog + Revert buttons (`Code/Editor/WeatherSystemInspector.cs`); P-panel saves copy the board. (2) New special light source `TreeFires` in every weather profile (all 27 written with vendor defaults, Override off) and `WeatherSystem.TryGetTreeFireLights`. (3) `Letterbox` + `Demo Intro Subtitles` deleted from `LightingTestScene` only. (4) **I key: tree fires** (`DevTools/TreeFireToggle`, `Prefabs/VFX/TreeFire_FX.prefab` from AST-015 Burning Tree, 13 vendor files copied into git-ignored `Assets/ThirdParty/AST-015`). v1 fire on all 8,128 trees = 6-13 fps; v2 pool of 60 fires within 80 m of the camera, 10 real lights, 6 sounds = 174 fps. (5) SessionLog v5: `treeFires N fireLights M` on every `[PERF]` line. Island / Island_Legion untouched | Lighting Test Scene only. `[MRM-86] Tree fires built: ... / ON / OFF` lines; `treeFires`/`fireLights` above 0 on `[PERF]` lines while fires burn (compare fps windows with 0). A frame dip every `reassignSeconds` (0.25 s) would point at the reassignment pass (measured 0.13 ms in the editor). Fires on the wrong spot or lights pinned to one place = the pool assignment / the removed vendor `LightFlicker`. "Ran out of virtual channels" = too many fire sounds (`maxSounds`). A missing fire effect in a fresh clone = the git-ignored AST-015 files. Weather saves: `[MRM-86] Lighting saved to / Fog saved to / Everything saved to <weather>` | `Docs/tree-fire-experiment.md`, `Docs/weather-profiles.md` (Session 2026-09-30), `Docs/lighting.md` §8, `Docs/controls.md`, this doc §7 (v5) | MRM-86 (main record), MRM-85 (this record) |
| C-009 | 2026-09-30 | `mrm-86`, commit **`8ecee2b4`** (2026-09-30, message `Docs/mrm86-commit-message-4.txt`) | **Tree fire tuning (MRM-86, Lighting Test Scene only).** `TreeFireToggle`: new Inspector settings `fireSizeMultiplier` 1.2, `lightIntensityMultiplier` 3 (scales the Tree Fires profile intensity), `showGlowBall` off (removes every `glow` sprite). `TreeFire_FX.prefab` gained the child **Base Fire (Medium)** (AST-015 `Fire Medium URP`, scale 2, local y 0.4 so it sits at the terrain line of buried logs; its Light, AudioSource and `base` disc removed). 3 vendor files copied into git-ignored `ThirdParty/AST-015` (`Fire Medium URP.prefab`, `FloorFireAURP.mat`, `FireBaseA.png`). Bug found and fixed in the same session: `Destroy(glow)` left a dead ParticleSystem in the fire list, causing `MissingReferenceException` on every ignite as the player walked, which also aborted the reassign pass (fires lighting late, dropping out and relighting); now `DestroyImmediate`. Island / Island_Legion untouched | Lighting Test Scene only. `[MRM-86] Tree fires built: ...` line. Frame time while fires burn is higher than C-008 (about 4 more particle systems per fire, brighter lights): compare `[PERF]` windows with `treeFires` > 0 against C-008 (174 fps in the editor). `MissingReferenceException ... ParticleSystem` in the console = the glow-removal bug back. Fires late to appear while walking = pool exhausted (`maxFires` x 1.5), see `tree-fire-experiment.md` section 5. A fresh clone without the git-ignored AST-015 has no fire | `Docs/tree-fire-experiment.md` sections 6-7, `Docs/lighting.md` section 8 | MRM-86, MRM-85 (this record) |
| C-010 | 2026-09-30 | `mrm-86`, commit **`e95455f2`** (2026-09-30, builds 37, 38; message: `Docs/mrm85-commit-message.txt`) | **FPS unlock + SessionLog v5.1 (MRM-85; touches MRM-78's area).** New `Assets/_Project/Code/Runtime/Data/DisplayBootSettings.cs`: at BeforeSceneLoad applies `GameSettings.VSyncEnabled` (default off) and `targetFrameRate = -1`, because Polymind `GraphicsOptions.Apply()` forces `vSyncCount = 1` at every boot and our override ran only from MainMenu's `SettingsPanel`. `SessionLog`: weather + `[WORLD]` lines + `vsync/cap` in `[PERF]`. | Before: `vSync 1` in the `[SESSION]` header and **75.0 fps in every `[PERF]` window** on a 75 Hz screen. After: `vsync 0 cap -1` in `[PERF]`, fps above the refresh rate. `[WORLD] weather -> ...` lines (v5.1; build 38 flooded, fixed in 39). | `Docs/culling-ast145.md` section 6, section 7 above | MRM-85 (MRM-78 area) |
| C-011 | 2026-09-30 | `mrm-86`, commit **`e95455f2`** (builds 39-42; message: `Docs/mrm85-commit-message.txt`; the culling scenes are ~66 MB each, Carlos decides whether to commit them) | **AST-145 Advanced Culling System 2, Dynamic, tested in a separate scene (MRM-85).** Imported to git-ignored `Assets/ThirdParty/AST-145` (Core + manual). New scene `LightingTestSceneCullingTest` (copy of `LightingTestScene`, original untouched): `DC_Controller`, `DC_Camera` on PlayerCamera, `DC_SourceSettings` + baked `DC_Collider` on 8,786 trees, KeepShadows. New layer `ACSCulling` (29): `TagManager`, `DynamicsManager`, `Physics2DSettings` changed. Also `LightingTestScene` start moved to the dense-forest spot (player 218.06, 33.37, -5.23; was 403.60, 23.10, -74.20) and `LightingTestSceneCullingTestFull` (FullDisable). Builds 40 (A no culling), 41 (B KeepShadows), 42 (C FullDisable). Rollback: `Docs/culling-rollback.ps1`. | Only in builds of the culling scenes (39, 41, 42): lower `draws`/`tris` in forest views than build 38 at the same spot, more CPU (ray jobs, colliders), trees blinking. Nothing in any other scene. | `Docs/culling-ast145.md` | MRM-85 |
| C-012 | 2026-09-30 | `mrm-86`, commit **`f393402a`** (2026-09-30 17:53; message: `Docs/mrm85-commit-message-2.txt`; build 43 was made from `e95455f2` + this work before the commit, build 44 from `f393402a`) | **Tree culling locked to C + corpse optimization (MRM-85).** (1) AST-145 Dynamic Culling, **FullDisable**, now in `Island` (5,990 trees), `Island_Legion` (8,786) and `LightingTestScene` (8,786): `Dynamic Culling` controller (lifetime 2, merge on, cell 10), `DC_Camera` 1500 rays on PlayerCamera, `DC_SourceSettings` + baked layer-29 `DC_Collider` on every Gaia tree renderer; read back: counts, FullDisable, baked, layer 29, camera. Sandbox, MainMenu and the galleries have no Gaia trees, untouched. (2) **Corpse optimization** (`Docs/corpse-optimization.md`): new `CorpseOptimizer` settle pass 5 s after the AI strip (shadows off, Animator off, GoreSimulator + blood particles off, ragdoll joints and rigidbodies destroyed, dropped shotgun shows LOD1 only, AST-145 corpse culling), `LampFireEffect` strips Light, rigidbody and colliders after the lamp burns out, `EnemyDeathDrop.DroppedItems`, 10 `Corpse*` tunables (story mode must turn `CorpseDisableGore` and `CorpseStripPhysics` OFF). Bridge to the asset in git-ignored `Assets/ThirdParty/AST-145/MrMoonlightBridge`. SessionLog v5.2. | `corpses N (settled S, culling C/R)` in `[ENEMY]`/`[PERF]`; SetPass and frame time at the same kill count should stop climbing (builds 38-42: 600 -> 2,000 SetPass, 9.7 -> 24-29 ms at 60-100 corpses); `culling C/R` grows when the player looks away from corpses; the `Dynamic Culling (Corpses)` controller appears in the scene at the first settled corpse; a dismembered-corpse request in story mode would find GoreSimulator off if `CorpseDisableGore` is left on. | `corpse-optimization.md`, `culling-ast145.md` section 12, `lighting.md` section 8 | MRM-85 |
| C-013 | 2026-09-30 | `mrm-86`, commit **`f393402a`** (same commit as C-012; message: `Docs/mrm85-commit-message-2.txt`; build 44 is made from it) | **Corpse dissolve and removal (MRM-85).** AST-063 Dissolve FX (Playground) "Burn Dissolve" look ported into RetroLit (`_USE_DISSOLVE` feature in `RetroLit.shader`, `RetroSurfaceInput.hlsl`, `RetroDepthOnlyPass.hlsl`, `RetroDepthNormalsPass.hlsl`; off for every existing material), noise texture `Art/VFX/Dissolve/Dissolve_Noise.png`, 5 `*_Dissolve.mat` variants, new `CorpseDissolve` component on `Enemy_Spotter.prefab` (variant list) and `CorpseDissolveTool` menu; `EnemyCorpseCleanup` runs it after the settle pass: +3 s, body burns 3 s, lamp and props burn 3 s while the lamp light dims (`LampFireEffect.FadeOutAndFinish`), then the enemy, drops, culling proxy and registrations are destroyed. 4 new tunables (`CorpseDissolveEnabled/Delay/Duration`, `CorpseLampDissolveDuration`); story mode must turn `CorpseDissolveEnabled` OFF. Bridge removes the proxy from the asset's hitable table (`CorpseCullingHooks.CorpseRemoving`). **Replaces C-012's rule that the lamp keeps its own timeline.** SessionLog v5.3. | `corpses` (live count) stays low instead of reaching 100; `dissolved D` grows with kills; `culling C/R` returns to 0 after each fight; SetPass and frame time should stop rising with kills (H1, H7); `[CORPSE] dissolve body` / `freed` lines; a shader-variant or material problem would show as a corpse that does not burn (stays until freed) or as pink/black dissolved bodies; dissolving many corpses at once could show up as short frame spikes at `freed` time. | `corpse-optimization.md` section 7, `lighting.md` section 8, `corpse-dissolve-midburn.png` | MRM-85 |
| C-014 | 2026-09-30 | `mrm-86`, commit **`fbffae12`** (docs only; message: `Docs/mrm85-commit-message-3.txt`) | **Build 44 made and analysed; no game code or assets changed.** Build 44 `E:\Builds\44 - Corpse Dissolve - 2026-09-30` (Windows, LightingTestScene + Island_Legion, commit `f393402a`, zip 479.8 MB) is the first build with the corpse dissolve (C-013). Docs: build 44 entry in section 5, H13 raised to strongly supported, `corpse-optimization.md` section 8 (results), `mrm85-fog-compare-prompt.txt` updated, `CLAUDE.md` read-first list. Carlos ruled: **lamps are not to be changed yet**. | Nothing new in the logs from this commit; it records the result that `handLights` (not corpses) tracks frame time at the end of the fight (R2 0.94, ~0.3 ms per light) and that 85 of 100 kills were freed. | `corpse-optimization.md` section 8, section 5 build 44 entry | MRM-85 |
| C-015 | 2026-09-30 / 2026-10-01 | `mrm-86`, commit **pending** (message: `Docs/mrm85-commit-message-4.txt`) | **Volumetric Fog & Mist 2 (AST-282) fog experiment + weather profiles split in two + scene numbering (MRM-85, MRM-86).** AST-282 imported to git-ignored `Assets/ThirdParty/AST-282` (Scripts, Editor, Resources, 19 presets; no demos; compiles into the URP assembly through `VolumetricFogURP.asmref`). New scene `08 LightingTestScene Fog Experiment` (copy of 07; HAZE removed; renderer `Settings/FogExperiment/PC_Renderer_VF2` = PC_Renderer minus `HazeRendererFeature` plus `VolumetricFogRenderFeature`, added as renderer index 1 of `PC_RPAsset`, the scene's PlayerCamera uses index 1; one `VF2 Island Fog` box (-700..700 x, -10..120 y) and a `Volumetric Fog Manager` (downscale 2, blur 1, Include Transparent = Water). Weather code split: `WeatherSystemBase` + `WeatherSystem` (HAZE, original library) + `Vf2WeatherSystem` (own library `Data/Weather/WeatherProfiles_VF2.asset`, 27 profiles, Vf2 fog values only, ~45 inputs), `IFogAdapter`, `HazeFogAdapter`, `Vf2FogAdapter`. **All 11 scenes renamed with a creation-order number prefix** (Build Settings, `MainMenuController`, `GameOverPanel` and the serialized scene-name strings updated); old scenes 08, 09 (culling tests) and the Legion fog copy deleted. `SceneEffectsToggle`/F6 also hides the VF2 mesh; the toggle prefab was added to scene 07 (and is still missing from 08). **No build was made from this**; the HAZE vs VF2 frame cost is NOT measured yet (H14). | Session logs show scene names with the number (`[06 Island_Legion]`, `[07 LightingTestScene]`, `[08 ...]`). Only in scene 08: VF2 draws the fog as a mesh in the transparent queue (`SetPass`, `draws` +; `GPU ms` for the fog pass), no HAZE pass. Visual faults seen in the editor and what caused them: full-screen diagonal weave = VF2 **Jittering** (see `fog-experiment-ast282.md` section 6); fog washed to white = dense fog lit by a bright sun (use Brightness = 1/sun) or Native Lights Multiplier near 1; Native Lights Multiplier 0 = no fog at all. | `fog-experiment-ast282.md`, `scene-numbering.md`, `weather-profiles.md`, `lighting.md` sections 1.1 and 8 | MRM-85, MRM-86 |

**How to fill a row:** ID, date, branch and hash, one paragraph of *what actually changed*, the specific log lines,
fields or hypotheses it could explain (name the `Hn` rows in section 3), and links to the docs and Linear comments. Keep
it factual; interpretation goes in the session entries (section 5).

---

Linear: MRM-85, entry posted there as a comment on 2026-09-18.
