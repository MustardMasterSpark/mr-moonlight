# PS1 wobble: one global switch (2026-10-02, MRM-88)

**What it is.** RetroLit has two PS1 effects: vertex snapping (`_SnapsPerUnit`, `_SnapMode`) and the affine texture swim (`_AffineTextureStrength`). Both were per material, 179 materials all at full strength.
Up close they made the church's planks look terrible, so they are now **off everywhere by default**.

**How.** One shader global, `_RetroWobbleScale` (0 = off, 1 = the full per-material effect, in between = a blend).
- `RetroSurfaceInput.hlsl` declares it and the `RetroWobbleSnap()` helper. RetroLit's vertex snap, the depth, depth-normals and meta passes use the helper, and the affine UV lerp strength is multiplied by it.
- A global nobody sets reads 0, so the effect is off even with no script running (Edit Mode included).
- `MrMoonlight.VFX.RetroWobble` sets it at boot from `MoonlightTunables.RetroWobbleScale` (default 0) and exposes `RetroWobble.Set(float)`.
- **No material was edited.** The per-material values are untouched, so 1.0 restores today's look exactly.

**Live use (drugs/shrooms).** `RetroWobble.Set(0.6f)` from gameplay code. It is a global float, so it is instant and costs nothing: no material swap, no keyword change. The shader only multiplies, so ramping with DOTween is fine.

**To see it again when drawing:** set `RetroWobbleScale` to 1 in MoonlightTunables and enter Play, or call `RetroWobble.Set(1)`.

**Not covered:** the AST-079 terrain shaders (`RetroTerrainLit*`) have their own vertex snap at 128 per meter, fixed and hidden. That folder is git-ignored vendor code (see the ThirdParty policy) and the snap is too fine to notice, so it was left alone. They have no affine effect.

**Log trace.** None expected. This is a visual change only.

## Church texture look (2026-10-02)
The church's 9 materials have `_ResolutionLimit` 256 (was 8192) as a live, reversible preview of a chunkier texture. The PNGs on disk are unchanged (BaseColor at most 512, about 10 levels per channel plus Bayer dither, Point filter; normals 256). If Carlos keeps 256, the follow-up is to re-bake the PNGs smaller with `Tools/pipeline/texture_pass.py run <folder> --size 256 --map-size 128` from the original sources (not from the quantised 512 PNGs). To roll back, set `_ResolutionLimit` to 8192 on those materials.
