# MRM-70 — Vegetation batch build, handoff

**Written 2026-08-29, overnight run while Carlos slept.** Read this before touching the new
folders below or resuming vegetation work. **Not yet logged in `Docs/prop-log.md`** — the
prop-wizard's write-back protocol needs Carlos's sign-off first, and the pipeline is still
formally in shakedown (no prop has cleared it end-to-end with his review). This run was executed
under his direct, detailed override of the shakedown/no-batch rule (he specified scope, the
folder location, the RetroLit-only constraint, and the gallery-scene deliverable, then asked to
be left running while he slept) — it is **not** a claim that the static-prop path has cleared
shakedown. Treat everything below as *built, self-verified, awaiting his eyes*, not signed off.

## Update — first review pass, same day

Carlos looked at the gallery and reported two problems: colliders were wrong, and foliage cards
were rendering as solid opaque planes instead of cutout. Both addressed:

- **Colliders removed from all 137 new prefabs.** Carlos assigns his own. The 17 pre-existing
  `RF_*` prefabs were left untouched — they already carry a sensible, clearly-deliberate
  collider pattern from the earlier session (rocks/logs/stumps → Box, trees/saplings → Capsule,
  bushes/ferns → none), not the broken batch heuristic, so they read as out of scope for this
  complaint. Say the word if you want those stripped too.
- **Root cause of the transparency bug: a real bug in the material-creation script, not the
  source assets.** `RetroLit.shader`'s cutout test (`clip(baseColor.a - _Cutoff)`) is gated behind
  `#ifdef _ALPHATEST_ON` — a shader_feature **keyword**, separate from the `_AlphaClip` float
  property. Setting `mat.SetFloat("_AlphaClip", 1f)` via script does not toggle that keyword (only
  a custom material Inspector GUI does that automatically when you click the checkbox by hand) —
  confirmed by diffing against `M_RF_Trees.mat` (built correctly, in the Editor, in an earlier
  session), which has `_ALPHATEST_ON` in `m_ValidKeywords` where every one of my 77 materials had
  none. Fixed by calling `EnableKeyword("_ALPHATEST_ON")` on the 37 materials with real alpha data
  (`_AlphaClip=1`) — this alone should fix the great majority of what looked broken, since most of
  the "black background" cards in the screenshots came from species that had perfectly good
  alpha data all along.
- **Separately, 2 species had a genuine source-data gap**: `AP_Plant_003_07` and the
  `sunghwangdang_Tree_pagoda_01` pair share textures that are literally shard-atlas images (bark
  chunks / rock chunks) baked onto solid black with **no alpha channel at all** in the source
  `.tga`, despite their source Playground material being tagged `TransparentCutout`. Verified
  visually before touching anything. Fixed by synthesizing alpha: flood-fill from the image
  border through connected near-black pixels (not a flat luminance threshold, which was tried
  first and produced noisy holes inside the bark texture itself — see the two discarded
  `alpha_check_*` previews if curious) marks only the true background as transparent, verified
  clean visually, then reimported and the two materials got `_AlphaClip=1` + the keyword.
- **The other ~38 "no real alpha" species were spot-checked, not blanket-fixed.** Three
  different-looking ones (`AP_Tree_10_ArgassTree_02_SM`, `AP_Tree_04_GTree01_03_SM`,
  `AP_Tree_Curse_J07`) were opened and are genuine full-frame bark/moss textures with no
  black-matte background — correctly opaque, nothing to fix. The rest of that list is presumed
  the same pattern (rock/bark/trunk material) based on their names, **not individually verified
  one by one** — flag it if you spot another one that looks like a broken card.
- Re-verified after all of the above: 0 console errors/warnings, correct shader on all 77
  materials, all 137 prefabs still have valid mesh+material, gallery scene resaved.

## Update — second review pass, same day: the real "invisible trunk" bug

Carlos reported some trees had an invisible trunk. **Automated it end to end rather than asking
him to list species** — scanned every one of the 137 source Playground prefabs for its actual
submesh/material structure.

**Root cause: 51 of the 137 species have a multi-submesh mesh (trunk + canopy as separate
material slots, 2–3 per species) where the original build script only ever captured the FIRST
material slot** (`build_manifest.py`'s prefab parser resolved `material_guids[0]` and `break`,
never looking at the rest of the array). Every one of those 51 prefabs got built with a single
material covering all submeshes — the submesh that should've had a bark/trunk material got the
canopy material's UVs/alpha instead, and once alpha-clipping was correctly enabled (previous fix),
that submesh clipped away as fully transparent. Before that fix it was just wrong-textured and
easy to miss; the keyword fix is what made the gap visible.

**Fixed properly, not patched over:**
- Re-derived the full ordered material-slot list (not just the first) for all 137 species by
  re-parsing the source prefabs.
- 22 of the 51 species' extra slot(s) already matched an existing built material (shared bark/
  trunk textures reused across several tree variants) — no new work needed, just wire up the
  existing material as the second/third array entry.
- The remaining **29 texture groups genuinely weren't captured anywhere** — ran the full pipeline
  on them (texture_pass.py, 512px cap enforced same as before — one, `AP_GangshiTree_2`'s second
  normal map, was caught oversized at 1024×1024 and downsized), built 29 new RetroLit materials,
  **this time setting the `_ALPHATEST_ON` keyword correctly from the start** (21 of the 29 needed
  it).
- Updated all 51 prefabs' `MeshRenderer.sharedMaterials` to the correct ordered array (order
  matters — must match submesh index, verified per-species submesh count == materials array
  length after the fix, 0 mismatches).
- **A separate, false-alarm check**: 8 *other* species also show `subMeshCount=2` with only 1
  material assigned. Checked these individually — in every one, the **source Playground prefab
  itself** only ever specified one material for both submeshes (both submeshes were genuinely
  meant to share it). Unity's own renderer behavior reuses the last material in the array for any
  submesh beyond the array's length, so these render correctly as-is and were **not** touched.
  Don't "fix" these if you spot the same submesh/material count mismatch — it's expected.
- Materials before this pass: 77. After: **106** (77 + 29 new). Prefabs: still 137. Re-verified:
  0 console errors, 0 null mesh/material across the board, 0 remaining colliders, gallery scene
  instances confirmed to pick up the fix automatically (proper prefab links) and scene resaved.

## Update — third review pass, same day: the last 8 "invisible trunk" species

After the 51-species fix above, Carlos reported 8 specific species were *still* showing the same
bare-trunk symptom (`AP_BC_PineTree_02`, `AP_BC_PineTree_03`, `AP_Tree_04_PTree_01_SM_2` through
`_04_SM`, `AP_Tree_Conifir_A_01_SM_2`, `AP_Tree_Conifir_A_02_SM`), with a screenshot. These were
the exact 8 species flagged as a "false alarm" in the second review pass above (subMeshCount=2,
only 1 material) — **that dismissal was wrong**, and Carlos's report is what caught it.

**Real root cause**: these 8 species' second submesh material slot is `type: 3` in the prefab
YAML — an **embedded, FBX-native material** (baked into the binary mesh file itself), not a
standalone `.mat` asset (`type: 2`). The original scanning script only ever matched `type:\s*2`
via regex, so these slots were silently invisible to it — they didn't look like "missing," they
looked like "not there to check." Confirmed via `scan_type3_full.py` (scans the actual target
renderer, not just any renderer in the prefab) — found precisely these 8, no more, no fewer.

Type:3 slots can't be resolved by text-parsing (FBX is binary) — required live inspection through
the Playground Unity instance (port 8081) to read the embedded material's actual name/texture
references. Traced: `AP_BC_PineTree_02`/`03`'s embedded material belongs to a **different** source
file (`AP_BC_PineTree_01.fbx` — not even in Carlos's species list), and the 6 `PTree`/`Conifir`
variants' embedded materials trace to their own respective source files. Built 2 new trunk
materials from those textures (`T_BC_Tree_A_wood_D/N.tga` for the PineTree pair,
`T_Tree_SPR_04_PTTrunk_01_Diff/Norm.png` for the 6 PTree/Conifir variants — one material serves
all 6 since they share the trunk texture), reassigned all 8 `MeshRenderer.sharedMaterials` arrays.

Materials before this pass: 106. After: **108**. All 137 re-verified: 0 submesh/material
mismatches project-wide, 0 console errors.

**Lesson for future batches**: when scanning a prefab's material array for gaps, check *all*
`fileID`/`type` values in the array, not just `type: 2`. A `type: 3` slot is real and rendering
something — usually not what you want — and will not show up as `null` or absent in any check
that only looks for standalone `.mat` references.

## Update — parent/Visual structural standard, 2026-08-29 (after Carlos's overnight review)

Carlos set a new project-wide convention for every vegetation prefab, to unblock two things at
once: a clean Y-axis pivot fix he'll do by hand later (no Blender pivot editing needed), and a
predictable place to put the collider he assigns himself. Applied to **all 158 prefabs** — the
137 from `Art/VegetationPrefabs/` and, per his explicit follow-up ("Yes include the RF ones too.
Every asset for the Vegetation spawner should follow this logic"), all 21 `RF_*` prefabs in
`Prefabs/World/Vegetation/RetroRealism/` too (17 of which are placed in the gallery scene; the
other 4 — `RF_Fern1/2`, `RF_Sapling1/2` — exist as prefabs but aren't in Carlos's original
154-name list or the gallery).

**The standard**: every prefab root is now `<SpeciesName>` (an empty parent at local position
(0,0,0), identity rotation, scale 1) with a single child named exactly `Visual` holding the
mesh/renderer/LODGroup that used to live on the root. Mirrors how the source FBX's own LOD
children already work — nothing new, just one more level of nesting.

- **137 `Art/VegetationPrefabs/` prefabs**: had no collider (removed in the first review pass), so
  the wrap was pure re-parenting — static flags moved from the old root to the new parent, nothing
  else to migrate.
- **21 `RF_*` prefabs**: DID carry live colliders on their root (Box for Boulders/Logs/Stumps,
  Capsule for Saplings/Trees, none for Bush/Fern — the sensible pattern from the earlier session,
  untouched in content). Per Carlos's instruction ("In the collider you just do that with the
  parent and rename it"), each collider was **moved to the new parent**, not left on `Visual`:
  captured its exact params (center/size or center/radius/height/direction), destroyed the
  component on the old root before it became `Visual`, then recreated the same component with the
  same params on the new parent. No mesh colliders were present in this set, so that migration
  path (documented as skipped, not silently dropped) never triggered.
- Verified across all 158: parent name == filename, parent local position == (0,0,0), identity
  rotation, exactly one child named `Visual` with a valid MeshFilter+MeshRenderer and no null
  material references. 0 console errors.
- **Gallery scene instances did not need any manual repair.** All 154 placed instances (137 +
  17 RF) remained `Connected` (`PrefabUtility.GetPrefabInstanceStatus`) after their source
  prefabs' root GameObject was replaced — Unity re-resolves prefab instances by the asset's
  stable internal file ID, not by GameObject identity, so this was safe to do at scale without
  touching the scene file directly. Confirmed by inspecting instances directly in the open scene
  (still in `VegetationGallery`, not in Play Mode at verification time) rather than assuming it
  from the "success" result of the wrap script.

**Answering Carlos's pivot/scale question directly**: no, scaling the parent later will not
disturb this pivot, and no, the order (scale now vs. offset later) doesn't matter. Position and
Scale are independent Transform fields — changing `localScale` never moves `localPosition`. If a
future Y-offset is set on `Visual` (in `Visual`'s *local* space) after the parent gets scaled up,
the offset is applied *after* the parent's scale in the transform hierarchy
(`child.worldPos = parent.position + parent.rotation * (parent.scale ⊙ child.localPosition)`), so
it scales proportionally with whatever the parent's final scale ends up being — automatically,
with no re-fix needed regardless of which step happens first.

## What exists now

**Source list**: `C:\Users\calva\Desktop\biome assets\output\Used_Asset_Names.txt`, 154 names,
17 of them `RF_*`.

- **17 `RF_*` prefabs** — already existed at
  `Assets/_Project/Prefabs/World/Vegetation/RetroRealism/`, built in an earlier session
  (commit `f2a2ca4`, "prep 7 vegetation/terrain packs"). **Carlos believed these didn't exist —
  they do**, and pass spec (RetroLit shader, mesh+material assigned, verified this session).
  Nothing was rebuilt for these; the batch just references them.
- **137 new prefabs**, sourced from Playground's *Low Poly Plant Collections* (20) and
  *TopDown Nature* (117) packs, at **`Assets/_Project/Art/VegetationPrefabs/`** — one flat
  folder, all 137 `.prefab` files, named exactly as the source list. This diverges from the
  existing `Assets/_Project/Prefabs/World/...` convention **on purpose**, per Carlos's explicit
  instruction to keep them in one place inside `Art/` for easy browsing before the spawner work
  starts. Expect to relocate/re-tag them once Gaia Spawner setup begins.
- **Source art** for those 137, under `Assets/_Project/Art/Environment/Vegetation/`:
  - `LowPolyPlants/Meshes|Textures|Materials/`
  - `TopDownNature/Meshes|Textures|Materials/`
  - Meshes: one `.fbx` per species, freshly imported (Scale Factor 1, Materials=None, Tangents
    Mikktspace, Read/Write off, Optimize on) — not the Playground `.meta` (avoids carrying over
    Playground's own import settings).
  - Textures: **77 unique** BaseColor(+Normal) pairs — many species share a texture/material
    exactly as the source packs did (verified this isn't a lost-tint regression: the two
    "African_violet" variants use the identical material GUID in the *source* Playground prefab
    too). Every texture ran through `Tools/pipeline/texture_pass.py` (levels=10, dither=1,
    AO folded into BaseColor). Square textures over 512px got downsized to 512; non-square ones
    were quantised at native res, **except 3 that exceeded 512 on their long edge post-hoc**
    (`AP_AlaskaCedar_001_2` normal, `AP_Norway_Spruce_01` normal, `AP_Tree_Burnt_04_SM`
    basecolor) — caught after the fact by an automated size scan and manually proportionally
    downsized (NEAREST) to respect the 512 cap. Re-verified clean after the fix.
  - Materials: **77**, all `Retro Shaders Pro/Retro Lit`, all properties from
    `.claude/skills/prop-wizard/SKILL.md`'s table (`_FilterMode=1`, `_SnapMode=2`, `_LightMode=1`,
    `_DitherMode=0`, `_ResolutionLimit=8192`, `_ColorBitDepth=256` — these last two are the
    shader's own "effectively off" values, matched against the existing `M_RF_Trees.mat`
    reference rather than guessed). `_AlphaClip` set per-material from whether its processed
    BaseColor actually has a non-trivial alpha channel (37 of 77 do).
- **New scene**: `Assets/_Project/Scenes/VegetationGallery.unity` — a `Ground` plane, the
  project's `SUN` prefab, the `Player` prefab, and all 154 prefabs (137 new + 17 RF) lined up
  3m apart along +X in Carlos's original list order, spanning ~462 units. Player starts at the
  west end facing down the line. **Not added to Build Settings** — dev-only scene, add it
  yourself if you want it in a build.

All of the above verified via `execute_code` read-backs (no null mesh/material/shader, correct
static flags, correct collider type, 0 console errors/warnings throughout) — not just "the tool
call returned success."

## Judgment calls made without asking — please review these specifically

- **Mesh choice**: each source FBX carries an LOD group (LOD0/1/2); every prefab uses **LOD0
  only** (highest detail). No LODGroup was rebuilt in Mr. Moonlight.
- **Collider heuristic** (there was no time/way to hand-pick 137): **Capsule if "tree" appears
  in the species name (case-insensitive), Box otherwise.** This misses some real trees whose
  names don't say "tree" — e.g. `AP_Norway_Spruce_01`, `AP_GraveKeepers_*` (grave markers, arguably
  fine as Box) got Box colliders. Per `Docs/3d-prop-pipeline-wizard.md` G7, this is exactly the
  kind of default that should get corrected via your review rather than guessed further —
  the gallery scene is where to catch these.
- **Collider size**: fit to each mesh's own bounds (Box: exact bounds; Capsule: height = mesh
  height, radius = half the smaller of X/Z). Not hand-tuned.
- **Material naming**: shared-texture groups are named after whichever species appears first in
  Carlos's list order — arbitrary but stable, e.g. `M_AP_GraveKeepers_B01.mat` is used by 6
  GraveKeepers variants.
- **Prefab transform/scale**: left at whatever the source mesh imported at (Scale Factor 1, no
  rescaling). Carlos's own plan was to walk the gallery and correct proportions — nothing here
  pre-empts that.

## What's still open (unchanged from the kickoff doc)

- Biome region boundaries and the 9 location blockouts — not touched this session.
- Carlos's per-biome asset+frequency list — still not handed over; that's the step after this.
- Gaia Spawner configuration — not started.
- `Docs/prop-log.md` — not updated. Once Carlos reviews the gallery and either signs off or asks
  for fixes, log it then (and note whether the static-prop path itself is now considered clear of
  shakedown, since this run stress-tested it at a scale of 137, not 1).

## If something looks wrong in the gallery

The build scripts that did this are in the session's scratchpad (not committed — ask me and I'll
regenerate them if you want to re-run or tweak the batch): a Python stage (`build_manifest.py`,
`stage.py`) that resolves each species to its Playground mesh+texture via straight YAML parsing
(no Unity needed) and runs `texture_pass.py`, then a set of generated `execute_code` C# chunks
that built the materials and prefabs in Mr. Moonlight.
