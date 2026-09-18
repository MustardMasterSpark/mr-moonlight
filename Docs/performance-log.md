# Performance log

Started 2026-09-17, per Carlos's request: track Island scene FPS across changes so we can see
what helps, what hurts, and which changes are worth doing again at scale (or not) before the
October Kickstarter push. Append one dated entry per change/session — don't edit history, add a
new section instead. Not every entry will have a real Island FPS number (see note below); log
what's actually measurable and say so honestly when it isn't.

**How to measure "Island FPS" correctly:** editor Play Mode with the Editor window unfocused
lies (see `Docs/kickstart.md` / memory "Verification requires a build" — Unity throttles
rendering when its own window isn't the focused OS window). A real number needs either a launched
build (`.exe`) or a focused-Editor Play Mode session with an explicit note that that's what it
was. Always record which method was used.

## Entry format

- **Date / issue**
- **Change:** what was actually done
- **Island FPS:** number + method (build vs. focused-Editor Play Mode) + where measured (route/
  standing point) — or "N/A" with why, if the change didn't touch Island or wasn't measured live
- **Other numbers:** collider counts, draw calls, whatever's relevant to that change
- **Notes:** anything useful for optimization decisions later

---

## 2026-09-17 — MRM-84: Technie Collider Creator 2 (AST-116) evaluation

- **Change:** Extracted Technie Collider Creator 2 v1.3.1 into the project
  (`Assets/Technie/PhysicsCreator/`, demo/example content skipped). Duplicated
  `VegetationGallery.unity` and the 154 vegetation prefabs it references into an isolated test
  folder (`Assets/_Project/Art/VegetationPrefabs/AST116_ColliderTest/`) — the live scene and
  prefabs were not touched. Wrote a reusable batch tool
  (`Assets/_Project/Code/Editor/Migration/AST116TechnieBatchCollider.cs`) that strips existing
  colliders and runs Technie's automated VHACD convex-hull generation on a named set of scene
  objects, then applies the result back onto the copied prefabs.
- **Island FPS:** N/A for Island itself (untouched). On the isolated test scene, focused-Editor
  Play Mode: one-time hit of ~99.6ms on the frame colliders first cook, settling to ~1.5ms/frame
  (~670 FPS) at steady state with no player/AI in the scene. The cost is front-loaded (cook/load
  time), not a per-frame tax — consistent with the static-broadphase reasoning below.
- **Other numbers:**
  - Baseline (before Technie, hand-authored primitives): 124 colliders across 110/162 root
    objects in the copied scene.
  - "High" VHACD preset: ~70 seconds/object. At 154 objects that's ~3 hours — far too slow to be
    usable, let alone at Island scale (~5,900 vegetation instances, per memory "Island NavMesh
    facts"). Root cause: Technie's High preset sets VHACD voxelization `resolution` to 5,000,000
    (vs. 100,000 for Medium) — a 50x jump. Switched the remainder of the batch to Medium (faster,
    still produced the collider explosion below).
  - **Result after full batch (23 objects on High, 131 on Medium, all 154 done): 31,485 total
    colliders (31,482 MeshColliders), vs. 124 baseline — a ~254x increase.** This is not evenly
    distributed: solid single-mesh props (rocks/logs/stumps, e.g. `RF_Boulder1`) got exactly 1
    MeshCollider each, correct and better-fit than the old primitives. Leafy/carded vegetation
    (trees, grass, flowers) exploded: 122/154 objects exceeded 50 colliders, 23 exceeded 200, worst
    case `AP_AlaskaCedar_001_2` alone hit 820 (verified independently by grepping the saved
    `.prefab` file, not just the in-memory count) and `African_violet_blue_LOD` hit 1024 (the
    preset's hard `maxConvexHulls` cap). Root cause: VHACD's "Auto" hull generation appears to
    treat each disconnected mesh island (each leaf card, each branch cluster) as its own convex
    decomposition candidate — it works exactly as intended on a single-surface solid mesh, and
    badly on carded/leafy foliage.
- **Verdict: reject Technie's "Auto"/VHACD generation for leafy/carded vegetation as configured.**
  Usable only for solid single-mesh prop types (rocks, logs, stumps) where it produced clean 1-hull
  results. Not adopted for trees/grass/flowers in this pass. See MRM-84 for the full writeup.
- **Notes for optimization:**
  - Island's vegetation colliders sit on `isStatic` GameObjects. PhysX bakes static geometry into
    a broad-phase structure (BVH) once at scene load; per-frame query cost scales with what's
    spatially near the player, not total scene collider count — confirmed above by the steady-state
    ~670 FPS despite 31K+ colliders in the scene. This means a "only enable colliders within a
    radius of the player" scheme is solving a problem that mostly isn't there for *static*
    colliders, while adding real complexity and risking two things that need colliders present
    everywhere at all times: NavMesh baking, and Spotter's vision raycasts (memory "Tag must be on
    the collider").
  - The real cost at this kind of hull count is one-time: cook time and memory for the baked convex
    mesh data, and build size (this project has a hard 1GB itch.io ceiling). At Island scale
    (~5,900 vegetation instances), extrapolating this ratio would mean well over a million
    colliders if applied uniformly — clearly not viable as configured.
  - If Technie is revisited for vegetation, the fix isn't a runtime activation scheme, it's making
    VHACD stop per-leaf decomposing: either a much lower `maxConvexHulls` cap, a non-VHACD hull type
    (single enclosing `ConvexHull` instead of `Auto`), or pre-simplifying the collision proxy mesh
    (e.g. trunk-only, ignoring leaf cards) before generation.

**Follow-up same day, after Carlos reviewed the test scene live:** two real bugs found and fixed —
1. The batch had wrongly given a new collider to every object with a mesh, including the 46
   species that never had a collider before (flowers, ferns, mushrooms, small grass, the
   RetroRealism bushes). That's what caused a fall-through-the-ground bug when walking over
   flowers. Fixed: cross-checked each of the 154 against its *original*, untouched source prefab
   (108 had a collider before, 46 didn't) and stripped colliders back off the 46 entirely —
   verified back to 0 colliders on all of them.
2. Carlos flagged the VHACD "Auto" hulls as both too dense to reason about and, on decorative
   fabric/cloth hanging off some trees (the Heretic series), tightly hugging geometry he'd rather
   stay walk-through. Switched the remaining 108 (the ones that legitimately had a collider) from
   `HullType.Auto` to `HullType.Capsule` — Technie's simplest single-primitive fit, no VHACD at
   all. Carlos accepted that a single bounding capsule won't selectively exclude the fabric; he'll
   hand-remove any specific colliders he doesn't want after this pass.
- **Result: 110/162 root objects have colliders (exactly matching the original 110), 112 total
  colliders** (108 new Capsules + 2 pre-existing non-vegetation colliders + Player's
  CharacterController) — down from the 31,485 collider explosion, and even slightly below the
  124-collider baseline, since every vegetation object with a collider now has exactly one.
- Confirms the earlier root cause diagnosis: Technie's "Auto"/VHACD mode is the problem for organic
  meshes, not Technie itself — its simple primitive-fit modes (Capsule/Box/Sphere) behave sanely
  and are a plausible real replacement for the hand-authored primitives, pending Carlos's live
  test pass.

**Second follow-up same day, after Carlos live-tested the Capsule pass:** a single bounding
primitive (Capsule/Box/Sphere/ConvexHull) over a whole tree's branch spread reads as an oversized
"invisible wall" in practice — confirmed visually (screenshot: a giant sphere-ish blob enclosing
`AP_GraveKeepers_B03_2`'s entire canopy). ConvexHull was tried next: still 1 piece, follows the
actual silhouette instead of ballooning into a sphere, but a convex shape still can't have gaps
between spread branches, so it still blocks that space (screenshot confirms). Went back to
VHACD, tuned manually on that one tree as a testbed:
- **Auto/Low** (resolution 10,000, concavity 0.01, maxConvexHulls 256): 27 colliders on
  `AP_GraveKeepers_B03_2` — visible gaps between branch clusters this time, a real improvement
  over Medium/High.
- **Custom** (resolution 25,000, concavity 0.007, minVolumePerCH 0.003, maxConvexHulls 256 — a
  modest nudge above Low): 100 colliders on the same tree — roughly 4x for that nudge, showing
  hull count is highly sensitive to these parameters on branchy meshes. Carlos reviewed and liked
  the fit (screenshot comparison wasn't usable this round — Editor was unfocused, Scene View
  renders degraded/washed out when Unity doesn't have OS focus).
- **Applied Custom to all 108** (after a full clean/reset first, so no residue from earlier
  passes): **3,321 total colliders, average ~30.7/object** — well below the ~254x-baseline
  explosion Medium/High produced, and still meaningfully tighter-fitting than Low.
- **Play Mode re-check:** ~1.36ms/frame observed on re-entry, no large one-time spike caught this
  time (either it completed faster than the round-trip needed to sample it, or cook cost at this
  collider count is small enough to not show up as a distinct spike — for reference, 31,485
  colliders cost ~99.6ms one-time earlier). Consistent with cook cost scaling with total collider
  count.
- **Open question, explicitly flagged to Carlos, not yet answered:** none of this measures real
  Island-scale load time. Island has ~5,900 vegetation instances today; this test only covers 108
  unique species at 1 instance each. Extrapolating from the one proxy data point we have (cook
  time scales with total collider count, ~100ms at 31K) suggests the physics-cook portion would
  likely stay well under a few seconds even at Island scale, but that is an estimate from a
  different scene, not a measurement of Island itself. A real answer needs either a build or a
  live test on Island, which hasn't been done and would need explicit sign-off first since it
  touches the live production scene.
- Motivation reminder for why this whole effort matters: Carlos reported a live gameplay bug on
  Island — a clean rifle shot at an enemy visually passed straight through a tree trunk, because
  the old hand-placed capsule collider didn't actually cover that geometry. Tighter, VHACD-fit
  colliders are meant to fix exactly this (accurate cover/line-of-sight blocking), not just be a
  performance exercise.

**Incident, same day: the batch tool corrupted all 108 original vegetation prefabs.** Carlos
spotted a tree with no visible collider on the root object and, checking further, found the
*original* `VegetationGallery.unity` (not the test copy) now had the new Custom-preset colliders
baked into its prefabs too. Root cause: `AST116TechnieBatchCollider.FindTargets` located objects
by name in whatever scene Unity reported as the active scene, with no check that it was actually
the intended test scene. The original and test scenes are visually near-identical (one is a
straight copy of the other), so at some point the active scene was the original when a batch ran,
and the tool had no way to know or refuse.
- **Fixed:** `git checkout --` on all 108 affected original prefab paths, verified clean via
  `git status` and spot-checked (`AP_AlaskaCedar_001_2` back to its 1 original capsule,
  `AP_DeadTree04` back to its 1 original collider). The original scene *file* itself was
  confirmed never touched beyond one unrelated pre-existing line from before this session.
- **Hardened:** the tool now checks `EditorSceneManager.GetActiveScene().path` against the exact
  required test-scene path before doing anything, and throws instead of running if it doesn't
  match. This class of mistake should now be structurally impossible, not just less likely.
- Lesson for any future batch tooling in this project: a script that's supposed to be scoped to a
  test/copy asset must verify its target's identity explicitly (scene path, folder prefix, GUID —
  whatever applies), never just trust "whatever the editor currently has open."

## 2026-09-17 — manual-split process replaces Auto/VHACD for branchy trees

After the density/tightness problems above, Carlos worked out a better process by hand: rough-
paint each limb as one whole Convex Hull selection, then split each at the mesh's real geometry
joints (not VHACD decomposition). Full step-by-step process, tool reference, and lessons learned
(including a bad first attempt at automating the split — over-segmented badly, recovered
losslessly) are in **`Docs/technie-vegetation-collider-process.md`**, not duplicated here. This
doc stays the numeric/perf history; that one is the "how to do the next tree" reference.

First two trees done (`AP_ENV_tree_Nokmyung`, `AP_ENV_tree_SaGeeSukRim`), ~106 remaining. No new
Island FPS numbers this entry — this work is still confined to the `AST116_ColliderTest` scene
copy.
