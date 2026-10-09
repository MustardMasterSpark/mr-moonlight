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
| 48 Mesh LOD | 2026-10-08 | `5419dab3` | native Mesh LOD on 80 FBX meshes (importer), renormalizer rolled back (original normals) | 499 (unchanged) | 1,111 / x=71 (build 47: 1,343 / x=63, -17%) | 949 (-9%) | 859 (+1%) | 707 (+3%) | 0.86 (same) | 182 | 604 / 145 (same) | triangles -9% dense half, -12..-23% in x 35-125, ~0 beyond x 150; draws/GPU/fps unchanged; vertices NOT lower. See section below |

How to fill a row: dense-half = windows with x <= 260. Take triangles/fps/GPU from the per-window table of the new log, the census line for the first column.

## Build 48 - Mesh LOD (2026-10-08, commit `5419dab3`) - RESULT

Log: `session-20261008-222108.log` (91.0 s in scene, 85,416 frames, no warnings or errors). Census confirms **80 meshes with native Mesh LOD** (build 47: 0); census tris/verts at the highest
detail unchanged (499k / 419k). Header `lodBias 2 meshLodThreshold 1 maxLOD 0`. Scene summary: avg **960.4 fps** (47: 952.3), 1% low **706.7** (47: 688.9), worst frame 14.0 ms (47: 40.9 ms).

Windows matched by `pos x` (47 -> 48). Windows start ~8 m apart in time, so x differs by up to 8; the first window (x=8 vs 16) is not comparable (different start of the run).

| x (47/48) | tris k 47 -> 48 | change | verts k 47 -> 48 | gpu ms | fps | draws 47 -> 48 | casters 47 -> 48 |
|---|---|---|---|---|---|---|---|
| 35/44 | 1319 -> 1101 | -16.5% | 1081 -> 1100 | 0.6 -> 0.6 | 779 -> 803 | 589 -> 571 | 145 -> 141 |
| 63/71 | 1343 -> 1111 | -17.3% | 1091 -> 1080 | 0.8 -> 0.7 | 842 -> 863 | 539 -> 525 | 134 -> 128 |
| 90/98 | 1120 -> 861 | -23.1% | 934 -> 882 | 0.9 -> 0.9 | 844 -> 874 | 517 -> 521 | 131 -> 138 |
| 117/125 | 901 -> 792 | -12.1% | 803 -> 821 | 1.0 -> 0.9 | 817 -> 820 | 531 -> 528 | 158 -> 162 |
| 145/150 | 924 -> 883 | -4.4% | 837 -> 861 | 0.9 -> 0.9 | 823 -> 830 | 528 -> 526 | 178 -> 180 |
| 172/173 | 1007 -> 1003 | -0.4% | 891 -> 917 | 0.9 | 852 -> 878 | 503 -> 496 | 180 -> 178 |
| 199/200 | 1023 -> 983 | -3.9% | 904 -> 919 | 0.8 -> 0.9 | 925 -> 919 | 481 -> 478 | 185 -> 182 |
| 226/227 | 920 -> 905 | -1.6% | 801 -> 824 | 0.9 | 920 -> 922 | 420 -> 421 | 155 -> 154 |
| 253/254 | 799 -> 793 | -0.8% | 672 -> 698 | 0.9 | 883 -> 893 | 371 -> 365 | 139 -> 133 |
| 306/309 | 383 -> 355 | -7.3% | 333 -> 341 | 0.8 | 961 -> 997 | 270 -> 263 | 101 -> 97 |
| 333/336 | 217 -> 200 | -7.8% | 195 -> 197 | 0.7 | 1024 -> 1046 | 252 -> 246 | 110 -> 106 |
| 360/364 | 103 -> 97 | -5.8% | 87 -> 82 | 0.6 | 1129 -> 1152 | 193 -> 186 | 82 -> 76 |
(x 388-445: -1.4..-2.7%, noise level.)

Dense half (x <= 260), averages: tris **1,042k -> 949k (-9%)**, verts 888k -> 916k (+3%), fps 851 -> 859, GPU 0.85 -> 0.86 ms, CPU 1.18 -> 1.16 ms, draws 497 -> 504, SetPass 125 -> 126.
Peak window 1,343k -> 1,111k (-17%). `mem mesh/tex/gfx` still n/a (release build).

Reading it (my interpretation; hypotheses marked):
- **Mesh LOD works and does what it should: triangles fall, nothing else moves.** Draws, SetPass and shadow casters are unchanged, as predicted.
- **The saving is concentrated in the first ~125 m (-12..-23%) and almost nothing from x ~150-260, although that stretch is just as dense.** Hypothesis: the gallery places one of each prefab in a row
  and the straight route passes every tree within a few metres, so most trees are at close range where Unity keeps LOD0/LOD1; the bigger trees in the first stretch are seen from further away. This is exactly why
  a gallery understates it. The real test is a forest seen from many distances (Island_Legion / scene 07).
- **Vertices did not fall (+3%).** Mesh LOD only trims index ranges; the vertex buffer is shared. The frame counter counts whole-mesh vertices, so it cannot show a vertex-shading win even if the GPU skips unreferenced vertices (unverified).
- **No fps or GPU change**, as expected at ~0.9 ms GPU on this PC. The win is triangles/shadow-pass geometry, which matters on weaker GPUs.
- Possible levers to check before judging the LODs "done": `QualitySettings.meshLodThreshold` (1 now) and per-mesh `lodSelectionCurve`/`lodBias` (2 now) decide how early levels switch; a more aggressive setting would
  push the x 150-260 stretch lower, at the cost of visible popping. Not tried.
- Not yet looked at by eye: Carlos should check the lower levels do not visibly pop or thin out the leaves at the distances where they switch.

## Build 49 (MeshFusion pilot) - setup, results pending (2026-10-08)

Scene 05, same standard run. Setup: `MeshFusion Pilot` controller (cell 80, Standard mesh, 65,535 vertex cap) + `StaticMeshFusionSource` on 89 specimens at x <= 260; Read/Write on their 89 FBX; Batching Static off on those children.
Change record C-028. A first build 49 was deleted (no commit behind it); build 49 is remade from the C-028 commit.
What to read when the log exists (compare by `pos x` against 47 and 48): `draws` and `SetPass` (should fall), `tris` (should climb back toward build 47: merged objects lose Mesh LOD), `verts`, `shadowcasters`,
GPU/CPU ms, fps, first-seconds hitch (combine), `[SCENE] CENSUS` renderers.

### Build 49 result (log `session-20261008-224122.log`, commit `d2f6fe7f`)

Avg 959.4 fps, 1% low 702.9, 90.0 s. Dense half (x <= 260) averages, 47 / 48 / 49: draws 497 / 504 / 488, SetPass 125 / 126 / 126, tris 1,042k / 949k / 1,060k, verts 888k / 916k / 999k,
shadow casters 153 / 154 / 145, GPU 0.8 / 0.9 / 0.9 ms, CPU 1.2 ms in all, fps 851 / 859 / 854. Census: 166 renderers (+11 combined cells), no combine hitch (first-window worst 2.4 ms vs 40.9 ms in 47).
Reading: MeshFusion merged correctly but the gallery has one specimen per species (116 materials for 155 renderers), so there is almost nothing to share per cell: draws -3..-7%, SetPass 0. The Mesh LOD triangle win is gone on merged objects (tris back to the 47 level).
The gallery cannot prove the draw win; the island can. Full entry: `Docs/performance-sessions.md` section 5.
