> **SUPERSEDED 2026-10-09: ROLLED BACK AND THE GENERATED MESHES DELETED on Carlos's word.** All 59 prefabs are back on their original FBX meshes and
> `Art/Nature/Renormalized Trees/` no longer exists. The text below is history; the end-of-demo re-run (checklist step 6) is cancelled. See `Docs/demo-update.md`
> "Side session - renormalizer rolled back, native Mesh LOD applied". Do not use Re-apply.

# Foliage Renormalizer (AST-301), tree normals pass + end-of-demo POLISH re-run

Added 2026-10-08, branch `mrm-88`. **This is a polish item: re-run it at the end of the demo (verification step) so every
vegetation prefab placed after the first pass is covered.** Carlos's verdict on the first pass, 2026-10-08: "not sure it made
an improvement, but at least a placebo, so keep it." It stays applied. Rollback is one menu click (below).

## What it is

AST-301 Ultimate Foliage Renormalizer (v1.3.4 imported; store is 1.5.6) rewrites a foliage mesh's **vertex normals**. It builds
a smooth SDF proxy blob around the leaf cards and bends each card's normal toward the blob, so a canopy shades as one rounded
mass instead of flat planes. It does **not** need a normal map (RetroLit's normal map just layers on top). It does not change
vertex count, UVs or tangents. Vendor code: `Assets/ThirdParty/AST-301 (Foliage Renormalizer)/` (git-ignored, like all of ThirdParty).

## Our batch script (what the first pass used)

`Assets/_Project/Tools/Editor/FoliageRenormalizerBatch.cs`. It sits outside `Code/` on purpose: the vendor scripts have no
asmdef (they compile into Assembly-CSharp-Editor), and `MrMoonlight.Editor` cannot reference that assembly.

Menu: `Tools > Foliage Renormalizer >`
- **Apply to trees in open scene**: processes every prefab under `Assets/_Project/Prefabs/Nature/Trees/` that is placed as a root
  in the currently open scene. Skips prefabs already in the manifest.
- **ROLLBACK to original meshes**: every prefab in the manifest goes back to its original FBX mesh.
- **Re-apply generated meshes (after a rollback)**: swaps the generated meshes back in without regenerating.
- From MCP/code: `FoliageRenormalizerBatch.Status()` returns how many manifest prefabs use generated vs original meshes.

Rules the script follows (all deliberate):
1. Only **alpha-clipped submeshes** (`_AlphaClip == 1` on the material) are renormalized. Trunk/bark submeshes keep their normals.
2. A prefab with **no alpha-clipped submesh is skipped** (status `skipped-no-cutout`): solid single-material trees (GraveKeepers,
   Heretic, Curse, Dry, Burnt, Lake RoundTree, MonsterTreeBark, BrokenTree) have nothing to smooth.
3. Settings are the vendor inspector defaults: SDF resolution 128, tightness 0.7, blur 5, proxy normal influence 1.0, upward bias 0.25.
   **AO bake is OFF** (RetroLit samples only BaseColor + Normal; vertex colors are untouched).
4. **No component is added to the prefab.** A throwaway `FoliageRenormalizerUtility` lives on a hidden temp object during the run.
5. Output meshes go to `Assets/_Project/Art/Nature/Renormalized Trees/<Mesh>_Renormalized.asset` (committed, not in ThirdParty).
6. Every change is recorded in `Assets/_Project/Tools/Editor/FoliageRenormalizerManifest.json` (prefab, child path, original
   mesh GUID + fileID, generated mesh path, submeshes processed, seconds, status). Rollback reads this file, so **do not delete it
   and commit it with the meshes**.
7. About 4 s per tree. Unity does not answer MCP calls while the batch runs (it blocks the main thread); poll the manifest instead.

## First pass result (2026-10-08, scene 05 `VegetationGallery_TechnieColliderTest`)

- 83 manifest entries: **59 applied, 24 skipped (no cutout), 0 failed.** Generated meshes total 17.4 MB.
- Rollback and re-apply were both tested: 59 originals, then 59 generated, as read back through `Status()`.
- Scene 05 had no per-instance mesh overrides, so instances followed the prefabs.
- Skipped (all solid): AP_GraveKeepers_B01/B02/B03_2/B04/B06/B07/C01/C02, AP_M6_Tree_MonsterTreeBark_SM_PHJ_2, AP_Tree_Burnt_04_SM,
  AP_Tree_Curse_H01_2/J07/J08/K01, AP_Tree_Dry_D01/N02, AP_Tree_Heretic_A01_2/B03/B05/D02_01/D03/D03_02,
  AP_Tree_Lake_RoundTree_01_SM, AP_TurtleLake_Tree_BrokenTree01_SM.
- Not covered by this pass (not in scene 05's Trees list): bushes and plants outside `Nature/Trees`, the `Old_*` rough-collider
  duplicates, Technie grass/flower copies, rocks, logs, stumps, fallen trees, and anything added to the project after 2026-10-08.

## POLISH: end-of-demo re-run (verification step)

Do this once, at the very end, after the last prop/vegetation placement and **before the final build**. Ask Carlos first
(prefab and asset writes), as for any Unity work.

1. **Make the target list complete.** The batch only sees prefabs placed as roots in the open scene. Either (a) open scene 05 after
   adding any new tree prefabs to it, or (b) run `Tools > Foliage Renormalizer > Apply to ALL prefabs in the Trees folder`
   (`FoliageRenormalizerBatch.ApplyAllInTreeFolder()`). Option (b) is the safer way to cover everything under
   `Prefabs/Nature/Trees/` including prefabs added since the first pass. Also decide whether to widen the folder list
   to the other vegetation folders (`Nature/Grass & Plants`, bushes). That is a **scope change, ask Carlos**, and use grass mode
   (`GenerateGrassNormals`, upward normals) for ground plants, not the tree/blob path.
2. **Check the `_AlphaClip` rule still holds** for newly added prefabs. A tree whose leaves use a different material setup
   (e.g. alpha blend, or a custom shader) would be skipped as "no cutout"; read the `skipped-no-cutout` list and eyeball it.
3. **Run Apply.** Already-processed prefabs are skipped by manifest, so only new ones are generated. Confirm the console line
   `[FoliageRenormalizerBatch] applied=N skipped=M failed=0`. Any `failed` entry is logged with its reason in the manifest `note`.
4. **Verify (do not trust the log alone):**
   - `FoliageRenormalizerBatch.Status()`: `usingGenerated` equals the number of applied entries, `other=0`.
   - Open one new tree in the scene: its `MeshFilter` mesh path is under `Renormalized Trees/`.
   - **Painted/instanced trees**: pick one tree painted on the island in scene 07 and one in Island_Legion and confirm they render
     the new mesh (Flora/instanced renderers might cache the mesh). This was NOT verified in the first pass.
   - Screenshot a few conifers, broadleaves and dead trees under the flashlight at night; compare with the pre-pass video if needed.
     Watch the dead trees (DeadTree01/02/03/04/06, WhiteFir_MD_Dead_03): their leaf-card branches were smoothed too and could look odd.
5. **Size and build:** the generated meshes are non-readable copies (+17 MB for the first 59). Re-check the build size against the
   1 GB itch.io ceiling after the re-run (cheap, but log it in `Docs/performance-sessions.md` §8 like any change).
6. **Colliders:** WoodColliderTool meshes are separate from the renderer mesh and unaffected, but spot-check one tree anyway.
7. **Record it:** change-record row (`C-nnn`) in `Docs/performance-sessions.md` §8, comment on Linear MRM-85, lighting history row
   in `Docs/lighting.md` §8 (this changes how foliage takes light). Log signature: none expected in the session log (same vertex
   count, same draw calls); a visual difference in foliage shading only.
8. **Rollback if wanted:** `Tools > Foliage Renormalizer > ROLLBACK to original meshes`, then `git status` should show the prefabs
   back to their original mesh references (the generated assets can stay or be deleted **only on Carlos's explicit word**).

## Traps

- Unity is unresponsive to MCP while the batch runs (about 4 s per tree). Do not retry the call; poll the manifest file.
- `PrefabUtility.SaveAsPrefabAsset` is used for the swap; read the mesh back from the saved prefab (as `Status()` does) rather than
  assuming it saved.
- The vendor copy is v1.3.4 and has one harmless CS0219 warning in `FoliageNormalTransfer.cs`. If the store version (1.5.6) is
  downloaded later, check that `Transfer`, `SDFProxyBuilder.Build` and `FoliageRenormalizerUtilityEditor.FoliageSavePath` keep
  their signatures, since the batch script calls them directly.
- Fog was switched off in the **in-memory** scene 05 for the comparison only (HAZE Global Fog and HAZE Explorable Area Fog
  deactivated). Do not save scene 05 with them off unless Carlos says so.
