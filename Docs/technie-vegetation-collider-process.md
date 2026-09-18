# Vegetation collider process — Technie Collider Creator 2 (AST-116)

**Status: rebuilt 2026-09-17 (Opus session), in active use under MRM-84.** This is the "how to do
the next tree" reference. The first version of this process (ring-seam splitter, 2026-09-17 Sonnet
sessions) is superseded; its full record is kept under **History** at the bottom. Performance
numbers and the original VHACD evaluation live in `Docs/performance-log.md`.

## Why this exists

1. **Gameplay bug:** a rifle shot passed straight through a tree trunk on Island because the old
   hand-placed capsule didn't cover that geometry.
2. Technie's automatic VHACD ("Auto") mode explodes on branchy meshes (up to 31,485 colliders across
   154 objects). It's still right for solid props — rocks, logs, stumps get one clean collider each
   (`AST116TechnieBatchCollider.RunOnNames`) — but not for trees.

Goal for trees (Carlos): colliders **one-to-one with the wood, or as tight as possible**, in
manageable pieces, and **never a hull that wraps empty space** (between roots, in a crotch, inside a
bend). Leaves and twigs nobody paints get no collider — that's intended.

## Doing a tree

### 1. Carlos paints (Technie, Prefab Mode, on the `Visual` child)

- One hull per limb: the trunk (`log_1`), then each branch (`branch_1`, `branch_2`, ...). A painted
  limb may fork — the tool handles forks — but painting each branch separately is fine too.
- Brush **Precise**; rotate the camera between clicks (the picker hits whatever's frontmost, so a
  leaf in front steals the click); **Shift** = add only, **Ctrl** = remove only.
- Leave the Type column alone; the tool sets it.
- **Don't worry about double-painting.** Technie does not remove a triangle from a hull when you
  paint it into another one, so a trunk painted first usually still holds every branch painted
  after it. The tool resolves this (rule below). Exact duplicate hulls are dropped automatically.
- **Never use "🗙 All" in the Hulls list** — it deletes the painted selections themselves
  (`PaintingData.RemoveAllHulls()`), not just the colliders.

### 2. Claude runs the tool — plan, look, apply, inspect

All commands live in `Assets/_Project/Code/Editor/Migration/TreeColliderTool.cs` and must be run
with the tree open in Prefab Mode. From the MCP `execute_code` tool (which can't name the namespace
directly), call them by reflection:

```csharp
System.Type t = null;
foreach (var a in System.AppDomain.CurrentDomain.GetAssemblies()) { var tt = a.GetType("MrMoonlight.EditorTools.Migration.TreeColliderTool"); if (tt != null) { t = tt; break; } }
try { return t.GetMethod("Plan").Invoke(null, new object[] { "AP_Tree_Juniper02_SMIK", null }).ToString(); }
catch (System.Reflection.TargetInvocationException e) { return "ERROR: " + e.InnerException; }
```

| Command | What it does | Changes anything? |
|---|---|---|
| `Plan(prefab, onlyHullNames = null)` | Splits every unsplit painted hull in memory, computes the exact hulls Technie would build, writes a report + a 3-angle image | No |
| `PlanFocus(prefab, hullName, int[] pieces)` | Same plan, 4-sided close-up framed on the listed pieces of one hull (report numbering, 1 = s01), all other hulls drawn for context | No |
| `Apply(prefab, onlyHullNames = null)` | Splits the paint into `<name>_s01`, `_s02`, ... (each piece its own colour in Technie's list), generates colliders synchronously, verifies them, renders the **actually generated** colliders | Yes — Undo works; auto-saves if the Prefab Mode "Auto Save" box is on |
| `Inspect(prefab)` | Checks the tree **as it is now**: every hull's paint next to the collider Technie actually built, collider bookkeeping, 10 largest gaps | No |
| `Regenerate(prefab)` | Rebuilds colliders from the current hulls (after a hand edit), then verifies | Colliders only |
| `Restore(prefab)` | Merges every `<name>_sNN` back into `<name>` and removes the generated colliders | Yes — Undo works |

`TreeColliderReset.Run(string[] keep, bool dryRun)` (`TreeColliderReset.cs`) wipes Technie paint,
data files and all colliders from every test-copy prefab not in `keep`, then re-reads them from
disk. Dry-run first; `keep` must include every Done prefab in the tracker. Asset deletes through
MCP `execute_code` need `safety_checks: false`.

Reports and images: `<project>/Temp/TreeColliders/<prefab>_{plan|focus|applied|inspect}.{txt|png}`.
Images: **left** = each piece's painted triangles in its colour, **right** = the hull built from
them, same colour, same camera. Only the painted wood is drawn (no leaves, no neighbouring trees).

**The order, every tree:**

1. `Plan`. Read the whole report — `PAINT:` lines first (duplicates dropped, triangles handed to
   smaller hulls), then per hull `NOTE: fork at ...` lines — then **open the image and look**.
   **If there are any `PAINT:` lines, tell Carlos before applying** — quote them and name the
   hulls. He believed painting into a new hull subtracted from the old one (it doesn't, in
   Technie), so double paint is expected, but it can also be a real mistake (a hull painted onto
   the wrong limb, a copy he meant to be a different branch). The automatic rule is usually right;
   he still decides whether to apply as-is or fix the paint first.
2. For anything that looks off — or any piece marked `<-- CHECK` that isn't an obvious fluted trunk
   ring — run `PlanFocus` on it and look again. A fork that came out wrong shows up in its `NOTE:
   fork` line (see Troubleshooting).
3. If the plan is right: `Apply`. The report must end with `VERIFY OK` (every hull has exactly one
   collider, all convex, none stale).
4. `Inspect` and look at the image of the **real** colliders.
5. Ask Carlos to look in his own editor before calling the tree done.

Don't skip the looking. Two earlier sessions declared trees "done" from numbers and a single
far-away screenshot; Carlos found crotch wedges and scattered pieces both times.

## How the algorithm works (`LimbSegmenter.cs`)

Each painted hull is one or more limbs (tubes that may fork). For each:

1. **Weld** vertices by position (1e-4 local units). Imported meshes duplicate a vertex at every UV
   or hard-normal seam; without welding, a continuous log looks like hundreds of islands.
2. **Connected parts** are processed separately.
3. **Start point:** where the limb is nearest the tree's base (the `Visual`'s local origin). If an
   open edge of the paint is there (a branch where it leaves its parent), start from that whole ring.
   Otherwise, if the part stands on the ground (its nearest point is in its bottom quarter): a
   **closed-bottom trunk** — start from every ground-level vertex inside the trunk's footprint, so
   distance grows as height and slices are level rings (2026-09-18; starting from one vertex made
   slanted strips, DeadTree01). Otherwise (a drooping branch) from that one vertex. The footprint
   rule is **the unsolved part** — see Troubleshooting "Roots fused into the trunk at the ground".
4. **Geodesic distance** from the start, along the limb's own surface (Dijkstra over welded edges,
   metric units, the `Visual`'s non-uniform scale applied).
5. **Arms and forks (join tree).** Sweeping from the farthest vertex inward, every tip starts an arm
   and arms that meet form a fork — if the shorter arm reaches at least `PersistenceEdges` mesh edges
   past the meeting point (so stubs and roots become arms; noise doesn't). Near-zero-length arms
   between two forks are folded into their parent, so six roots leaving together are one 7-way fork.
6. **Fork shoulders.** Just below a fork the parent and a side branch are one fused cross-section,
   and slicing by distance alone hands the branch's shoulder to the parent — whose hull then fills
   the crotch. So at every fork: the child that is thickest *where it leaves* is the limb carrying
   on; its tube (axis through the fork, radius = 95th percentile of its surface distance in clean
   stretches ±15%) keeps its vertices, and any vertex sticking out of that tube and closer to a side
   branch's axis goes to that side branch.
7. **Cutting each arm** walks bands (0.75 mesh edges wide) along the geodesic distance and cuts at
   the first of:
   - **Outline fit (2026-09-18, the main cover rule).** Around the straight axis of the candidate
     piece, the outline is sampled in `ProfileSectors` directions (outermost vertex per direction,
     binned every ¼ edge along the axis). If a convex hull would stand more than
     `ProfileTolerance` (5 cm, less on thin limbs) off any dent in that outline — a waist, a flare,
     a kink — cut. This runs before the minimum-length gate. Result: straight limbs keep long pieces,
     curved ones get sliced ring by ring (Carlos's "calculus" rule).
   - The centerline stops being straight (sag > `StraightnessTolerance` × radius).
   - The piece passes `MaxLengthPerDiameter`.

   The piece that ends at a fork gets a cut `JunctionPieceDiameters` below the fork (only cuts right
   next to that one are dropped; it used to drop *every* cut above it, which erased Deadtree06's
   waist cuts).
   **Slice floor:** hulls are built from whole triangles, so no piece can be thinner than one
   triangle row (0.4–0.7 m tall on batch-1 trunks).
8. **Tiny or flat pieces** (< `MinTrisPerPiece`, or no thickness) merge into a piece they actually
   touch, same arm preferred.
9. **Lobe split (2026-09-18, EXPERIMENTAL — not yet applied to any tree).** Each piece is viewed
   down its axis in `LobeSectors` wedges; runs of wedges reaching well past the core radius become
   separate pieces, so a star-shaped cross-section (roots fused into the base, two arms fused under
   a fork) becomes a round core + one hull per lobe. On Deadtree06 alone it didn't clear the root
   plates (the start-region problem dominates there); untested on anything else.

**Paint ownership** (in `TreeColliderTool.BuildPlans`, across all hulls even when planning one):
exact duplicate hulls are dropped (first kept); a triangle painted in several hulls belongs to the
hull with the fewest triangles — "the branch wins" (Carlos, 2026-09-17).

### Settings (`TreeColliderTool.Settings`, tuned on Juniper02)

| Setting | Value | Meaning |
|---|---|---|
| `StraightnessTolerance` | 0.2 | Max centerline sag in a piece, × local radius |
| `MaxLengthPerDiameter` | 2.0 | Longest piece, in diameters |
| `MinLengthPerDiameter` | 0.5 | Shortest piece the straightness test may cut |
| `PersistenceEdges` | 1 | How far (mesh edges) a protrusion must reach to be its own arm. 1 = stubs split out (Carlos's call) |
| `JunctionPieceDiameters` | 0.75 | Length cap for the piece ending at a fork |
| `ForkRegionDiameters` | 2.0 | How far below a fork shoulder vertices can move to a side branch |
| `TubeRadiusTolerance` | 0.15 | Slack on the carrying-on limb's tube |
| `MinTrisPerPiece` | 6 | Smaller pieces merge into a touching neighbour |
| `GapWarningRadii` / `GapFloorMetres` | 0.5 / 0.03 | Report flag: hull face stands off its paint by more than this |
| `ProfileTolerance` / `ProfileToleranceRadius` / `ProfileToleranceFloor` | 0.05 m / 0.25 / 0.015 m | Outline-fit cut: max hull stand-off over a dent = min(5 cm, ¼ radius), never below 1.5 cm (2026-09-18) |
| `ProfileSectors` | 8 | Directions the outline is sampled in around the axis |
| `LobeSectors` / `LobeCoreQuantile` / `LobeExcessRadius` / `LobeMinExcess` | 24 / 0.3 / 0.3 / 0.08 m | Lobe split (experimental) |

**Gap** (report column, `<-- CHECK`): the farthest any hull face stands off the piece's own painted
surface, with the piece's open ends capped first. On a fluted trunk, 0.15–0.3 m on trunk rings is
bark grooves — expected. On a branch or at a fork it means empty space is being wrapped — look.

## Troubleshooting

- **A crotch is filled / a branch's base wedge:** read that fork's `NOTE:` line. `continues as arm
  N` must be the limb that actually carries on (thickest where it leaves). `side arm M took 0` at a
  wide-angle branch means the tube rule kept everything — check `tube r` against the real trunk.
- **Roots or stubs wrapped into the trunk ring:** they didn't reach `PersistenceEdges` edges; a
  coarse mesh (big edges) makes this more likely.
- **`NOTE: part N: no open edge at its base`**: that part started from a single vertex — fine for a
  closed-bottom trunk; for a branch it means the paint doesn't reach its attachment.
- **A hull covers two separate places:** shouldn't happen any more (pieces are single bands of
  single arms). If it does, compare the left and right columns of the image: the left shows the
  piece's actual triangles in its colour.
- **Trunk outline bridged — waist, flare or zigzag (batch 1, Carlos's screenshots 2026-09-18):**
  fixed by the outline-fit cut (step 7). If it recurs, check the trunk pieces' length in the report:
  a trunk piece much longer than one triangle row across a visibly curved stretch means a cut was
  lost (the fork-cap bug did exactly that).
- **Slanted strips instead of level rings on a trunk:** the start was a single vertex on a
  closed-bottom trunk (report `NOTE: ... no open edge at its base`). The closed-bottom rule (step 3)
  should catch trunks; look at its `NOTE: closed bottom, started from N ...` line.
- **Roots fused into the trunk at the ground — UNSOLVED (Deadtree06, 2026-09-18).** Flat roots
  spreading from the base on the ground: the lowest rings hold the trunk *and* every root base, and
  their hulls are plates joining root to root (Carlos found it from a top view). Tried and failed,
  so the next attempt doesn't repeat them:
  1. Footprint = median ground-ring radius × 1.3 → 3.8 m on Deadtree06, low ~0.3 m plates between
     roots (this is what was applied, then undone).
  2. Lower quartile of the ground ring → a tall sliver hull standing off the trunk side.
  3. Footprint from the trunk radius at 10% height (current code) + lobe split → still a filled
     square between root bases from above.
  4. Same + ground band 1.2% of height instead of 3% → sliver hull again (reverted).
  **Workaround that works today:** paint the trunk and each root as separate hulls, overlapping a
  little where they meet — separately painted limbs split cleanly. Carlos repainted Deadtree06 on
  2026-09-18 (one hull); next session decides the approach.
- **Always check a rooty or forked base from above.** The preview image now has a top-down view as
  its last row (a close-up hides everything above the focused pieces so the crown doesn't cover
  the base). The side views hid the root plates.
- **`VERIFY PROBLEM`:** run `Regenerate`. Generation is synchronous now; the old window-driven
  generate could silently do nothing while logging "complete".

## Traps (engine / tooling)

- **Colliders live on the `Visual` child**, not the prefab root.
- **Preview rendering:** a plain scripted camera renders the main scene, not the isolated Prefab
  Stage. `Camera.scene = stage.scene` fixes it (what `TreeColliderTool` does). Scene View
  screenshots are useless when the Editor isn't the focused window — use the tool's images.
- **Convex-hull renders hide scattered selections:** a hull of two separate clusters looks like one
  solid blob. That's why the image's left column shows the triangles, not just hulls.
- **Generating through the Technie window** (`RigidColliderCreatorWindow.GenerateColliders`) does
  nothing if its target hasn't caught up with `Selection` yet. `TreeColliderTool` runs Technie's
  own generation steps directly instead.
- **Prefab Mode "Auto Save" is on** in Carlos's editor: `Apply`/`Restore` land in the prefab asset
  immediately. Undo still works.

## Progress — see `Docs/tree-collider-tracker.md`

The tracker is the source of truth for which prefabs are done, in progress or waiting. Check it
before touching any prefab and update it after every tree.

- `AP_Tree_Juniper02_SMIK` — **done, Carlos approved (2026-09-17).** Repainted as `log_1` +
  `branch_1..19`; `branch_17` (copy of `branch_12`) dropped; 609 of `log_1`'s 1506 triangles were
  also painted in 11 branch hulls and went to those. 19 painted hulls -> 104 colliders,
  `VERIFY OK`, `log_1` = trunk + 6 roots + the two-armed top fork. Collider volume 3.5 m³ (14 m³
  as single hulls). Largest gaps are fluted-trunk rings (0.12–0.25 m).
- `AP_ENV_tree_Nokmyung`, `AP_ENV_tree_SaGeeSukRim`, `AP_AlaskaCedar_001_2` — done with the
  superseded splitter; kept as they are.
- **2026-09-17: every other prefab in the test folder was reset** (`TreeColliderReset`: 104
  prefabs, 3,602 colliders, 104 Technie components and 208 Technie data files removed, plus two
  orphaned data pairs; verified from disk and in the test scene). Carlos repaints from scratch.

## Batches (plan from 2026-09-17)

Carlos paints, Claude (Opus — Carlos's call after the Sonnet sessions) processes in bulk:
1. **Batch 1: 5 trees Carlos picks to be hard** — weird shapes, buried/overlapping limbs. The point
   is to find new failure modes before he paints the rest. After it: fold lessons into this doc
   (Troubleshooting + Settings) and tell Carlos anything that changes how he should paint.
2. Then the remaining trees, 5–10 per session.

Per batch: for each tree run the full order above (Plan -> look -> PlanFocus where limbs meet ->
PAINT lines to Carlos -> Apply -> Inspect -> look), one short summary per tree, all questions for
Carlos collected into one round, the tracker updated at the end.

## Batch 1 — 2026-09-17/18 (Opus)

Carlos picked 5 hard trees. First pass (old cutter) applied all five; Carlos then checked live and
found the "clean" three still bridged the trunk outline badly — the thing that matters for cover
(an enemy behind a tree; a shot that looks clear must not hit an invisible collider). Second pass
rebuilt the cutting (outline fit, level rings on closed bottoms, fork-cap fix, top-down preview).

| Tree | State at end of session |
|---|---|
| `AP_Tree_Lake_RoundTree_01_SM` | Applied, 128 colliders, trunk ring-sliced. Awaiting Carlos. |
| `AP_Tree_DeadTree01_SM` | Applied, 69 colliders. **Carlos flagged the upper-left fork crotch** still filled. |
| `AP_Tree_Deadtree06_SM` | **No colliders.** Base unsolved (root plates). Carlos repainted it as one hull. |
| `AP_Tree_Curse_H01_2` | Applied with the OLD cutter, 442. Braided trunk strands wedge. Carlos: "fine for what they are". |
| `AP_M6_Tree_MonsterTreeBark_SM_PHJ_2` | Applied with the OLD cutter, 321. Looping root wedge. Same verdict. |

**Code state vs what's applied:** Lake_RoundTree and DeadTree01 were applied with the outline-fit
cutter + the "median ground ring × 1.3" footprint and **no** lobe split. The code now has the
10%-height footprint and the lobe split, neither verified on those two. Re-plan them before
trusting the current code.

Painting lessons for Carlos: separate hulls per limb still give the cleanest result; where roots
lie flat and fuse into the trunk at the ground, paint each root as its own hull.

## Known gaps (2026-09-17, updated 2026-09-18) — not solved yet

- **Fused ground roots** (Deadtree06) — see Troubleshooting.
- **Fork crotches on thick trunks** (DeadTree01 upper-left, Carlos 2026-09-18) — the fused
  cross-section just below a fork is still one hull. The lobe split is meant for this; untested.
- **Braided / looping strands in one paint** (Curse_H01, MonsterTreeBark) — read as one fat tube.
- **Tested on one tree.** Settings and the fork logic were tuned on Juniper02 (coarse mesh, 18 cm
  median edge, fluted trunk, closed bottom). Untested: dense meshes, conifers with many whorls,
  drooping limbs that go lower than where they start (the start point is "nearest the tree base"),
  branches whose paint stops short of their parent, branches that are separate mesh pieces stuck
  into the trunk, near-parallel limbs touching each other.
- **In-game cost unmeasured.** Juniper02 went from 1 to 104 convex colliders. Nobody has measured
  physics cost with hundreds of such trees on Island (it had ~5,900 vegetation colliders before).
  Measure in a build after batch 1 and log it in `Docs/performance-log.md`.
- **Test copies only.** Nothing has been carried over to the live vegetation prefabs or Island, and
  there is no migration step written yet.
- **Gameplay wiring unchecked:** tag/layer on the generated colliders (colliders sit on `Visual`,
  and physics reads the collider's own tag), bullet impact surface, NavMesh bake impact.
- **Solid props lost their colliders in the reset.** Rocks/boulders/logs/stumps listed as "Solid?"
  in the tracker need Technie Auto (`AST116TechnieBatchCollider.RunOnNames`, test scene) or
  painting — Carlos to confirm which.
- **The reset isn't Ctrl+Z-able** (prefab saves + asset deletes). Git commit `4454569` on
  `mrm-84` has the previous state.
- **Report flags are noisy on trunks:** `<-- CHECK` fires on most fluted trunk rings. Harmless, but
  it trains the reader to ignore it; calibrate after batch 1.
- **One prefab at a time:** Plan/Apply need the tree open in Prefab Mode; there's no multi-prefab
  runner yet (open each with `manage_prefabs open_prefab_stage`).
- **The three old-tool trees** were never re-checked with `Inspect`; they may have the kinds of
  wedges the old splitter produced.

---

## History (superseded 2026-09-17 — kept for the record, do not follow)

Everything below describes the first process: `AST116TechnieBatchCollider.SplitPaintedHullsAtRingSeams`
and its diagnostics (`CheckHullConnectivity`, `CaptureColliderPreview`, `DumpRingProjection`,
`DumpElbowSelection`, `MergeSplitHullsBack`, `RegenerateCollidersOnly`,
`RenameHullAndClearColliders`). Those methods were **deleted** on 2026-09-17 when `TreeColliderTool`
replaced them. Why it was replaced, in one paragraph: it looked for "ring seams" as gaps in vertex
positions projected on one axis. That works on a straight tube with loop cuts (AlaskaCedar) and fails
on real trees: bends smear the signal, forks put unrelated branches at the same "height", the
fallbacks it grew stitched separate regions into one hull, and five bug rounds on Juniper02 each
passed the checks of the day while Carlos kept finding wedges and scattered pieces in the editor.
The replacement segments by arms (join tree of geodesic distance) instead of seams.

### Old workflow (superseded)

1. Open the prefab in Prefab Mode. 2. Clear old colliders with "Delete generated colliders and game
objects". 3. Rough-paint each major limb as one selection. 4. Claude ran
`SplitPaintedHullsAtRingSeams(prefabName)`: PCA long axis per hull, ring seams found as the biggest
relative jump ("elbow") in sorted vertex-position gaps, one Convex Hull per segment named
`<original>_<n>`, segments under 4 tris merged, then generate. 5. Verify with a six-item checklist
(segment count sanity via `DumpRingProjection`, triangle count preserved, collider count equals hull
count, `CaptureColliderPreview` image, `CheckHullConnectivity` on every hull, Carlos's eyeball).
6. Missed limbs: paint a new hull, re-run with a name filter. 7. Save / exit Prefab Mode.

### Lessons learned (first two trees)

- **Convex Hull is correct for a pre-split limb, not a fallback.** VHACD/Auto decomposes one messy
  concave selection; on an already-simple selection it just adds noise from surface bumps.
- **A single hull over a whole tapering/forking limb overshoots badly** — straight hull edges cut
  outside the curved surface.
- **First ring-detection attempt over-segmented** with a fixed noise-multiplier threshold (one
  33-tri hull into 13 pieces, some unbuildable). Switched to relative "elbow" detection plus a
  minimum-triangle safety net.

### Bug found and fixed (`AP_AlaskaCedar_001_2`)

A single log built as one tube with ~19 evenly spaced loop cuts. The elbow detection found the real
18-gap split with a clean 6x ratio, but a "smallest real gap ≥ 2% of span" guard rejected it, leaving
one straight hull across the tapering trunk; Carlos caught it by eye. Fix: dropped the span floor,
raised the ratio bar 2x → 2.5x. Result: 18 hulls hugging the taper, triangle count identical (392).

### Second bug (`AP_Tree_Juniper02_SMIK`)

The elbow search took the single highest gap ratio anywhere in the list; on `branch_6` a noise pair
of near-zero gaps from welded seam vertices (3.9x) beat the real seam (2.7x, 26% of span), leaving
the branch unsplit. Fix: take the first qualifying gap scanning from the largest.

### Third bug (`log_1`)

A bent trunk smears ring cross-sections across one straight axis, so "no elbow" looked like "no
seams": `log_1` came back as one giant hull. Fallback added: even split into `round(tris / 150)`
segments. Also found: a plain scripted camera can't see inside an isolated Prefab Stage;
`CaptureColliderPreview` redirected the SceneView's own camera instead.

### Fourth bug (`log_1`, second round)

The even-split fallback still assigned triangles by the same straight axis, so two disconnected
branches at a similar height landed in one hull (Carlos circled them). Switched to geodesic distance
from one seed; then found 1119 of ~1160 vertices "disconnected" because UV-seam duplicate vertices
fragmented the index graph — fixed by position welding.

### Fifth bug (same session)

Distance from one seed still put different fork arms into the same band. Added a mandatory
connected-components split per band and adjacency-only merging of tiny pieces;
`CheckHullConnectivity` reported 0 disconnected of 27. Carlos then showed that the result was still
wrong: pieces cut at arbitrary distances instead of the visible ring geometry, and hulls still
wrapping empty space next to the trunk. That is where the rebuild started.

### Old progress notes

- `AP_ENV_tree_Nokmyung` — done (first pass had the even-split bug, corrected).
- `AP_ENV_tree_SaGeeSukRim` — done. Two branch tips looked uncovered on first review; a visual-only
  artifact of the gizmo overlay being hard to see through the active Mesh Renderer.
- `AP_AlaskaCedar_001_2` — done: one painted `log` hull (392 of 963 tris) -> 18 ring-based hulls.
