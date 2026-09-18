# Vegetation collider process — Technie Collider Creator 2 (AST-116)

**Status: validated workflow, in active use.** Started 2026-09-17 under MRM-84. This is the
process for giving trees tight, accurate colliders — not a one-off experiment writeup (see
`Docs/performance-log.md` for that history and the numeric before/after data). This doc is the
"how to do the next tree" reference.

## Why this exists

Two real problems, not just a performance exercise:
1. **Gameplay bug:** a clean rifle shot at an enemy on Island visually passed straight through a
   tree trunk, because the old hand-placed capsule collider didn't actually cover that geometry.
2. Technie's automated VHACD ("Auto") decomposition, tried first, explodes on leafy/branchy
   meshes (up to 31,485 colliders across 154 test objects — see performance-log.md). It's fine for
   solid single-mesh props (rocks/logs/stumps, which get a clean 1 collider) but wrong for trees.

The validated fix for trees: **manually mark the main limbs, then auto-split each one at the
mesh's real geometry joints** — tight, low-count, matches the visible silhouette.

## The workflow, step by step

1. **Open the prefab in Prefab Mode** (double-click it in `AST116_ColliderTest/`, or the
   subfolder it's in). Persistence depends on saving/exiting Prefab Mode when done — same as any
   other prefab edit.
2. **Clear anything already there.** Select the "Visual" child (the object with the MeshFilter —
   colliders always attach there, not the root, for this asset library). In the Inspector, if a
   "Rigid Collider Creator" component exists, open it and use **"Delete generated colliders and
   game objects"** (top Tools row, right side) to clear old output — or ask Claude to run
   `RemoveCollidersOnly` for a full reset including the painting data itself.
   - **Do NOT use "🗙 All" in the Hulls list** — that deletes the hull *definitions* (your painted
     selections), not just the generated colliders. Confirmed via source: it calls
     `PaintingData.RemoveAllHulls()`, a different and much more destructive operation than
     "Delete generated colliders and game objects" (`RigidColliderCreator.RemoveAllGenerated()`).
3. **Rough-paint each major limb as one whole selection.** One hull per log/branch, using the
   paint tool:
   - Brush size **Precise** (not Small/Medium/Large) — avoids grabbing nearby leaf triangles.
   - Rotate the camera between clicks — the picker hits whatever's frontmost on screen, so an
     overlapping leaf steals the click even when you're aiming at the trunk behind it.
   - Hold **Shift** while painting to force "add" (can't accidentally deselect). Hold **Ctrl** to
     force "remove" (fixes a mistaken leaf click without starting over).
   - Name each hull sensibly (`log`, `branch_1`, `branch_2`, ...). Leave Type as the default —
     it gets overwritten anyway in the next step.
4. **Hand off for the real split.** Tell Claude which prefab and that you're ready. It runs
   `AST116TechnieBatchCollider.SplitPaintedHullsAtRingSeams(prefabName)` from inside your open
   Prefab Stage, which:
   - Finds each hull's long axis (PCA on its selected triangles).
   - Finds the mesh's **real ring seams** along that axis — actual jumps in vertex height, not an
     even/approximate split — by looking for the biggest *relative* jump in gap sizes between
     sorted vertex positions (an "elbow" in the gap distribution), not a fixed noise threshold.
   - Splits each limb into one Convex Hull per real segment, named `<original>_<n>`.
   - A hard safety net merges any segment under 4 triangles into its neighbor, so a noisy
     ring-detection can't produce a degenerate/unbuildable hull.
   - Generates all colliders in one pass.
5. **Review.** If a spot looks wrong or uncovered, check first whether it was ever part of your
   original painted selection — see "How to verify a suspected bug" below before assuming
   the split step is at fault.
6. **Adding something you missed:** paint the missing bit as a new named hull (e.g. `branch_5`),
   then re-run the split **with a name filter** so already-split pieces aren't touched again:
   `SplitPaintedHullsAtRingSeams(prefabName, new[] { "branch_5" })`. Without the filter it
   reprocesses every hull in the file, including ones already split into small pieces, and
   over-fragments them further.
7. **Save / exit Prefab Mode** to persist. Nothing here writes to the file until you do.

## How to verify a suspected bug (do this before concluding something broke)

Compare the total selected-triangle count across every hull, before your last operation and after.
It should match exactly — the split/generate step never drops a triangle, it only redistributes
whichever ones were already selected. If the count matches, whatever looks wrong was never part
of the original painted selection (a painting-step gap, fixable by painting more, not a tool bug).
If the count doesn't match, that's a real regression worth investigating.

Quick check (run from Claude, needs the Prefab Stage or scene instance):
```
mesh.triangles.Length / 3                              -> total triangles in the mesh
sum of selectedFaces.arraySize across all current hulls -> what's actually covered
```

## Lessons learned (2026-09-17, first two trees)

- **Convex Hull is correct for a pre-split limb, not a fallback.** VHACD/Auto exists to
  decompose one messy *concave* selection into multiple convex pieces automatically. Once a limb
  is manually split into individually near-convex segments, there's nothing left for Auto to
  usefully decompose — running it on an already-simple selection just adds noise from surface
  bumps. This is *why* Auto kept giving a bad result "no matter the preset."
- **A single hull over a whole tapering/forking limb overshoots badly.** A convex hull connects
  its extreme points with straight lines; between a narrow base and a wider point where a branch
  forks off, that straight edge cuts outside the real curved surface. This is what "the collider
  covers space the enemy's body isn't in" looks like. Splitting at the real joint fixes it.
- **First ring-detection attempt over-segmented badly**: a fixed noise-multiplier threshold
  (8x the low-quartile gap) treated organic tree geometry's natural bumps as real seams — one
  33-triangle hull split into 13 pieces, several too small to form a valid convex hull at all
  ("Could not generate convex hull" errors). Fixed by switching to relative "elbow" detection
  (biggest ratio jump between sorted gap sizes, requiring at least a 2x jump and the smallest
  "real" gap to be ≥2% of the selection's total span) plus the minimum-triangle safety net.
  Recovered the bad attempt losslessly via `MergeSplitHullsBack` (unions every `<base>_<n>` hull
  back into one `<base>` hull — safe because the split never drops triangles, only redistributes
  them, so the union always reconstructs the original selection exactly).

## Tool reference (`Assets/_Project/Code/Editor/Migration/AST116TechnieBatchCollider.cs`)

- `SplitPaintedHullsAtRingSeams(prefabName, onlyHullNames = null)` — the main step 4 operation.
  Must be run from inside the matching Prefab Stage.
- `MergeSplitHullsBack(prefabName, originalNames[])` — recovery: undoes a bad split by unioning
  `<base>` / `<base>_<n>` hulls back into one `<base>` hull per name given.
- `RemoveCollidersOnly(names[])` / `RunOnNames(...)` / `RunCustomOnNames(...)` — the earlier
  scene-wide batch tools from the original whole-tree VHACD evaluation. Still correct for simple
  solid props (rocks/logs/stumps) but superseded by the manual-split process above for anything
  branchy. These are scene-scoped (guarded to the test scene) and separate from the Prefab-Stage
  methods above.

## Progress

- `AP_ENV_tree_Nokmyung` — done (first pass had the even-split bug, corrected).
- `AP_ENV_tree_SaGeeSukRim` — done. Two branch tips looked uncovered on first review; turned out
  to be a visual-only artifact of the collider gizmo overlay being hard to see through the active
  Mesh Renderer, not a real gap — confirmed fully covered once checked with that in mind. Worth
  remembering: don't trust "looks uncovered" from a screenshot with the mesh visible without
  double-checking (e.g. hide the renderer, or check selected-triangle counts as below).
- Remaining ~106 "had collider" species: not started. This is expected to take a while per tree
  since the rough-painting step is manual; the split/generate step itself is fast and repeatable.
