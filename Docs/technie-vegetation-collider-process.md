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
5. **Verify (mandatory — see checklist below), then review visually.** Don't save on the strength
   of "GENERATE COMPLETE" in the log alone; the 2026-09-17 AlaskaCedar incident shipped a broken
   result that logged completion normally. If a spot looks wrong or uncovered after the checklist
   passes, check first whether it was ever part of your original painted selection — see
   "Verification checklist" below before assuming the split step is at fault.
6. **Adding something you missed:** paint the missing bit as a new named hull (e.g. `branch_5`),
   then re-run the split **with a name filter** so already-split pieces aren't touched again:
   `SplitPaintedHullsAtRingSeams(prefabName, new[] { "branch_5" })`. Without the filter it
   reprocesses every hull in the file, including ones already split into small pieces, and
   over-fragments them further.
7. **Save / exit Prefab Mode** to persist. Nothing here writes to the file until you do.

## Verification checklist (mandatory, every tree, before saying "done")

Screenshots from this environment are not reliable evidence — Scene View captures render
washed-out/gizmo-less whenever the Editor window isn't OS-focused (see traps below), which is most
of the time during an unattended MCP session. **Numeric checks are the primary verification here,
not a screenshot.** Run all four before saving:

1. **Segment count isn't suspiciously low.** A hull that comes back as exactly 1 segment is not by
   itself proof the limb has no real seams — a real, fine, evenly-spaced ring structure can produce
   this same "1 segment" result if a threshold wrongly rejects it (this is exactly what happened
   with `AP_AlaskaCedar_001_2`, see "Bug found and fixed" below). If the limb visually bends or
   tapers at all, and the split result is 1 piece, run `DumpRingProjection(prefabName, hullName)`
   and look for a large ratio jump in the gap list before accepting the 1-piece result as correct.
2. **Triangle count is preserved.** Compare total selected-triangle count across every hull, before
   your last operation and after. It should match exactly — the split/generate step never drops a
   triangle, it only redistributes whichever ones were already selected. If it matches, anything
   that still looks wrong was never part of the original painted selection (a painting-step gap,
   fixable by painting more, not a tool bug). If it doesn't match, that's a real regression.
   ```
   mesh.triangles.Length / 3                              -> total triangles in the mesh
   sum of selectedFaces.arraySize across all current hulls -> what's actually covered
   ```
3. **Collider count matches hull count, with no oversized leftover.** After any re-run (especially
   after fixing a bug and re-generating), explicitly check `root.GetComponentsInChildren<Collider>
   (true).Length` equals the current hull count, and that no single collider's mesh is far larger
   than the others (a leftover un-split hull from a prior bad generation looks like one collider
   covering most of the original selection sitting alongside the new tight ones). Don't assume a
   clear step removed the old collider(s) — check the live component list. `DescribeCurrentPrefabStage`
   plus a direct `GetComponentsInChildren<Collider>` walk (see chat history 2026-09-17 for the exact
   snippet) is the standard way to do this.
4. **Ask Carlos to eyeball it in his own (focused) editor window** for anything a numeric check
   can't catch (does it *look* right, not just add up right) — don't rely on an MCP screenshot as
   the final sign-off.

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

## Bug found and fixed (2026-09-17, `AP_AlaskaCedar_001_2`)

**The opposite failure mode from the one above: a real, high-confidence seam signal was being
thrown out.** `AP_AlaskaCedar_001_2`'s single log is one continuous mesh with ~19 evenly-spaced
loop cuts along its length (built that way so it can bend), not several separate mesh pieces
welded together like `AP_ENV_tree_Nokmyung`'s log. The gap-ratio elbow detection correctly found
an 18-real-gap / noise-floor split with a clean **6x** confidence ratio (well above the 2x
requirement) — but a second guard, "the smallest real gap must be ≥2% of the whole selection's
axis span," rejected it anyway, because with 18 real gaps sharing that span each one is naturally
smaller relative to the total than Nokmyung's 3 were. Result: the hull was silently left as one
un-split piece — a straight-edged convex hull across the entire tapering trunk, the exact
"overshoots the curve" problem this whole process exists to avoid. First delivered pass in this
session shipped that broken single-hull result; Carlos caught it by eye in the editor.

Fix: dropped the fixed "≥2% of total span" floor (it doesn't scale with how many genuine rings a
limb has) and raised the ratio confidence requirement slightly (2x → 2.5x) as a partial replacement
— the ratio test is the real noise-vs-signal check, a percent-of-span floor isn't. Re-running the
split on the same hull after the fix found all 18 real segments and produced 18 Convex Hulls
hugging the taper, matching the intended look. Triangle count verified identical (392) before and
after both the broken and fixed attempts — confirms this was purely a detection-threshold bug, not
data loss.

**Takeaway for future trees:** a hull producing suspiciously few segments (especially exactly 1)
is not proof the limb has no real seams — it may mean the mesh has many *fine, evenly-spaced* real
seams that a percent-of-span-style floor would suppress. If a "single log, should be easy" case
comes back as one piece, don't take that at face value — check whether it's genuinely a smooth,
unbroken tube (rare) or a finely-ringed one with a suppressed real signal, via
`DumpRingProjection` (dumps the sorted gap list; a real signal shows as a clean, large ratio jump
somewhere in the descending gap list even if the absolute gap sizes are all small).

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
- `DescribeCurrentPrefabStage()` — read-only dump of the open Prefab Stage's hull names, selected
  face counts, and generated collider count. Use this to sanity-check state instead of guessing.
- `DumpRingProjection(prefabName, hullName)` — read-only: shows the PCA-axis projection's sorted
  gap list for one hull, biggest and smallest. Use when a split result looks suspicious (e.g.
  exactly 1 segment for a limb that should clearly bend/taper) to see whether a real seam signal
  exists but is being rejected by a threshold, versus the mesh genuinely being seamless.
- `RegenerateCollidersOnly(prefabName)` — re-runs just the generate step against whatever hulls
  currently exist, without touching selections. Useful to isolate a generation failure from a
  split failure. Non-blocking (polls via editor coroutine) — check `Log` or the console a moment
  after calling.
- `RenameHullAndClearColliders(prefabName, oldName, newName)` — renames one hull and clears
  generated colliders, so a split can be safely re-run with clean naming after fixing a bug rather
  than stacking suffixes like `log_1_1_1`.

## Progress

- `AP_ENV_tree_Nokmyung` — done (first pass had the even-split bug, corrected).
- `AP_ENV_tree_SaGeeSukRim` — done. Two branch tips looked uncovered on first review; turned out
  to be a visual-only artifact of the collider gizmo overlay being hard to see through the active
  Mesh Renderer, not a real gap — confirmed fully covered once checked with that in mind. Worth
  remembering: don't trust "looks uncovered" from a screenshot with the mesh visible without
  double-checking (e.g. hide the renderer, or check selected-triangle counts as below).
- `AP_AlaskaCedar_001_2` — done (2026-09-17), but exposed a real bug on the first pass, see lessons
  below. Final result: one painted `log` hull (392 of 963 tris) split into **18** ring-based Convex
  Hulls hugging the trunk's taper, matching the reference look Carlos wanted.
- Remaining ~105 "had collider" species: not started. This is expected to take a while per tree
  since the rough-painting step is manual; the split/generate step itself is fast and repeatable.
