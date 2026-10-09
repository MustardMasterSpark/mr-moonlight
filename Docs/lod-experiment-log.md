# LOD / optimizer experiment log (scene 05 gallery run)

Started 2026-10-08 (MRM-88 / MRM-85). **One place to compare every build made for the tree-LOD and optimizer-asset experiments** (native Mesh LOD, LODGroup LODs,
MeshFusion Pro, Super Level Optimizer 2). Each build gets a column/section below with the SAME run, so the numbers line up. The general per-build analysis still goes in
`Docs/performance-sessions.md` section 5; this file is only the A/B ledger. Handoff for the work itself: `Docs/demo-update-lod-sonnet-prompt.txt`.

## The standard run (do it exactly like this every time)

- Build of **scene 05 only** (`05 VegetationGallery_TechnieColliderTest`, one of each vegetation prefab, ~155 renderers). Fog ON as saved (F6 toggles; do not touch it), weapon = Combat Knife, no flashlight, no enemies.
- Carlos presses **F3 (infinite stamina)** and **runs in a straight line along +x** from the spawn (8, 2, 3) to the last prefab (~467, 2, -4), looking straight ahead (yaw ~90-100, pitch ~ -3),
  then quits normally (Alt+F4) so the scene SUMMARY is written. About 90-95 s. Windows are 5 s each, matched between builds by the `pos x` column (not by time).
- Machine: AMD Ryzen 7 9800X3D, RX 9070 XT 16 GB, 1920x1080 exclusive fullscreen, vSync off, no cap. **This PC is so fast that frame times are ~1 ms; fps alone will barely show a LOD win.**
  Compare **triangles, vertices, shadow casters, draws, SetPass** first, then GPU ms, then fps.
- NOTE: the `tris`/`verts` of a `[PERF]` window include the SHADOW passes (peak 1,343k tris against a census of 499k at highest detail, about 2.7x), so a LOD win should show up in them twice.

## Build 47 - Gallery Baseline (2026-10-08, commit `4bd146a7`) - NO LODs, the reference

Log: `session-20261008-213201.log` (93.3 s in the scene, 86,331 frames, no warnings or errors). Header: Unity 6000.3.21f1, `lodBias 2 meshLodThreshold 1 maxLOD 0`.

**Census (taken 1.6 s into the scene, cost 1.5 ms):** 155 mesh renderers + 3 skinned, 143 distinct meshes, 116 distinct materials, **499k triangles / 419k vertices at the highest detail**,
14 LODGroups (all 1 level, the vendor flower prefabs), 0 lower-level renderers, 0 meshes with native Mesh LOD, 155 shadow-casting renderers, 112 colliders, 3 lights (1 with shadows), 0 terrain trees.

**Scene summary:** avg **952.3 fps**, 1% low **688.9 fps**, worst frame 40.9 ms (the first window, right after the warm-up; every later window's worst is 1.6-14.6 ms).

**Per-window (5 s each, along the line):**

| t (s) | x | avg fps | 1% low | worst ms | cpu ms | gpu ms | draws | setpass | tris k | verts k | shadow casters |
|---|---|---|---|---|---|---|---|---|---|---|---|
| 8 | 8 | 825 | 583 | 40.9 | 1.2 | 0.8 | 493 | 125 | 1060 | 867 | 121 |
| 13 | 35 | 779 | 646 | 6.7 | 1.3 | 0.6 | 589 | 141 | 1319 | 1081 | 145 |
| 18 | 63 | 842 | 685 | 14.6 | 1.2 | 0.8 | 539 | 133 | 1343 | 1091 | 134 |
| 23 | 90 | 844 | 652 | 2.4 | 1.2 | 0.9 | 517 | 129 | 1120 | 934 | 131 |
| 28 | 117 | 817 | 677 | 1.8 | 1.2 | 1.0 | 531 | 129 | 901 | 803 | 158 |
| 33 | 145 | 823 | 722 | 2.4 | 1.2 | 0.9 | 528 | 129 | 924 | 837 | 178 |
| 38 | 172 | 852 | 684 | 2.4 | 1.2 | 0.9 | 503 | 122 | 1007 | 891 | 180 |
| 43 | 199 | 925 | 762 | 1.9 | 1.1 | 0.8 | 481 | 117 | 1023 | 904 | 185 |
| 48 | 226 | 920 | 705 | 1.8 | 1.1 | 0.9 | 420 | 113 | 920 | 801 | 155 |
| 53 | 253 | 883 | 693 | 2.6 | 1.1 | 0.9 | 371 | 111 | 799 | 672 | 139 |
| 58 | 280 | 930 | 741 | 1.9 | 1.1 | 0.8 | 311 | 92 | 666 | 550 | 114 |
| 63 | 306 | 961 | 717 | 5.9 | 1.0 | 0.8 | 270 | 85 | 383 | 333 | 101 |
| 68 | 333 | 1024 | 850 | 2.5 | 1.0 | 0.7 | 252 | 78 | 217 | 195 | 110 |
| 73 | 360 | 1129 | 838 | 1.6 | 0.9 | 0.6 | 193 | 66 | 103 | 87 | 82 |
| 78 | 388 | 1139 | 794 | 1.6 | 0.9 | 0.6 | 155 | 54 | 75 | 62 | 65 |
| 83 | 415 | 1120 | 853 | 2.7 | 0.9 | 0.6 | 133 | 55 | 71 | 58 | 56 |
| 88 | 443 | 1081 | 839 | 1.7 | 0.9 | 0.7 | 115 | 59 | 67 | 54 | 42 |
| 93 | 467 | 1205 | 956 | 2.7 | 0.8 | 0.5 | 52 | 43 | 48 | 35 | 11 |

Reading it: the dense part of the gallery is x ~ 0-260 (draws 420-590, SetPass 113-141, triangles 0.8-1.3 M, 114-185 shadow casters, ~780-920 fps, GPU ~0.9 ms, CPU ~1.2 ms); it thins out past
x ~ 300 (43-383k triangles, 11-101 shadow casters, 1,000-1,290 fps). CPU and GPU time are about equal in the dense part, so neither dominates at this scale. **Peak window: x = 63 (1,343k tris, 539 draws).**
Average over all windows: 669k triangles; over the dense half (x <= 260): 1,042k triangles at 851 fps.

**Gap found:** `mem mesh / tex / gfx` print **n/a** in this release build (those profiler counters only exist in development builds). The next session should replace them with an estimate
computed in the census (sum of mesh vertex-buffer and index-buffer bytes over the distinct meshes), because mesh memory is exactly what a LOD or MeshFusion build changes.

## Comparison ledger (fill one row per build; same run, same route)

| Build | Date | Commit | What changed vs baseline | Census tris (k) | Peak window tris (k) / x | Dense-half avg tris (k) | Dense-half avg fps | 1% low (scene) | Dense-half avg GPU ms | Max shadow casters | Max draws / SetPass | Verdict |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 47 Gallery Baseline | 2026-10-08 | `4bd146a7` | nothing (reference) | 499 | 1,343 / x=63 | 1,042 | 851 | 689 | ~0.9 | 185 | 589 / 141 | reference |
| 48 Mesh LOD (pending) | 2026-10-09 | `pending` | native Mesh LOD on 80 FBX meshes (importer), renormalizer rolled back (original normals) | | | | | | | | | to be filled after the run |

How to fill a row: dense-half = windows with x <= 260. Take triangles/fps/GPU from the per-window table of the new log, the census line for the first column.

## Build 48 - Mesh LOD (planned, NOT built yet)

What changes vs build 47: `Generate Mesh LODs` on 80 FBX importers (see `Docs/demo-update.md`, side session 2026-10-09), renormalized meshes gone (original FBX normals). Same standard run.
Expected: census tris at highest detail ~unchanged (~499k, and `meshes with native Mesh LOD` > 0); window triangles/verts and shadow-pass cost lower for the dense part (x 0-260);
draws and SetPass about the same. Pilot numbers from the importer: GTree01_05 4146 -> 2109 -> 1088 -> 577 -> ... 64 tris, Curse_H01_2 27,975 -> 64. Built only after Carlos gives the commit hash.
Compare with `lodBias 2 meshLodThreshold 1 maxLOD 0` as in the header. If the lower levels look bad, tune `QualitySettings.meshLodThreshold` / per-importer `maximumMeshLod` before judging.
