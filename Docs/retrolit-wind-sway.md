# RetroLit wind sway — reusable vertex-shader wind for vegetation

**Built:** 2026-09-07, on branch `mrm-18`, for the Main Menu's staged trees/flowers.
**Scope:** the feature itself is **not** menu-specific — it lives in the shared `RetroLit`
shader, so any RetroLit-shaded object in any scene (Island included) can opt in. This doc is the
"how to reuse it" reference; treat it as project-wide, not part of the MainMenu build summary.

## Why a shader, not a script

Unity's built-in wind system (`WindZone`) only drives SpeedTree-imported trees, the legacy Tree
Creator system, particles, and cloth — it does nothing for arbitrary static mesh props like the
ones in this project's vegetation packs. Swaying those requires displacing individual vertices
(so the crown moves and the trunk doesn't), which a script can't do cheaply per-frame on a full
vertex buffer — that's exactly what a vertex shader is for. It also had to work with objects
marked `isStatic` (as these vegetation props are) without fighting Unity's batching/lightmapping
assumptions the way moving a Transform in script would.

`RetroLit` (`Retro Shaders Pro/Retro Lit`) is the one shader almost everything in this project
uses, so the wind feature was added there directly — off by default, so every material that
hasn't opted in (weapons, terrain, characters, everything) renders exactly as before. It defaults
to zero cost and zero visual change; the only way to notice it exists is to turn it on.

## Where the code lives

Moved out of the git-ignored `Assets/ThirdParty/Retro Shaders Pro/` into
`Assets/_Project/Code/Vendor/Retro Shaders Pro/Shaders/` (matching the project's existing
vendor-code convention — see `CLAUDE.md` / `thirdparty_gitignored` — so the change is actually
tracked). Files touched:

- `RetroWind.hlsl` — **new.** The shared `ApplyMoonlightWind()` function and the four global
  shader variables. Fully commented; read this first.
- `RetroLit.shader` — added the `_WindEnabled` / `_WindCategory` / `_WindHeight` /
  `_WindFlexibility` properties (under a "Wind Sway" header), included `RetroWind.hlsl`, and
  called `ApplyMoonlightWind()` at the top of the main forward pass's `vert()`.
- `RetroSurfaceInput.hlsl` — the four per-material properties added to the shared
  `UnityPerMaterial` CBUFFER.
- `RetroShadowCasterPass.hlsl`, `RetroDepthOnlyPass.hlsl`, `RetroDepthNormalsPass.hlsl` — each
  also calls `ApplyMoonlightWind()` on its own vertex position, so the mesh, its shadow, and its
  depth all sway in lockstep. Skipping any one of these would show a static shadow under a
  swaying tree, or wrong depth-based effects (SSAO, decals) around it.
- `RetroMetaPass.hlsl` — **untouched**, moved only because `RetroLit.shader`'s `#include`s are
  relative paths and all five files must live in the same folder to resolve. The Meta pass only
  runs during lightmap baking, never at runtime, so wind there would be meaningless anyway.

The shader's `CustomEditor` (`RetroLitShaderGUI.cs`) was **not** moved — it's looked up by class
name, not file path, so it keeps working from its original location in `ThirdParty/`.

## How the sway works

One shared wind direction + speed, plus two independently-tunable intensity categories (Trees,
Flowers), so a scene can have gently swaying flowers under aggressively thrashing trees (or vice
versa) without touching individual materials:

- **Per-material** (set once per species' material, in the Inspector):
  - `_WindEnabled` — off by default.
  - `_WindCategory` — 0 = Trees, 1 = Flowers. Picks which global intensity this material reads.
  - `_WindHeight` — local-space height (object units, from the mesh's own pivot) at which sway
    reaches full strength. Below it, sway fades to zero at the pivot — this is what keeps
    trunks/stems still while canopies/petals move. Tuned per-species from each mesh's own
    bounds (roughly 40–60% of the tree's total height for trees; near-full height for the much
    smaller flowers, since real flower stems bend far more readily than a trunk).
  - `_WindFlexibility` — per-material multiplier on top of the category's global intensity. Used
    to make the two "Dead"/"Burnt" tree species (no foliage catching wind) sway less than living
    trees at the same global setting (0.4 vs 1.0).
- **Global** (driven every frame by `RetroLitWindController`, see below):
  - `_MoonlightWindDirection` — world-space XZ direction.
  - `_MoonlightWindSpeed` — oscillation speed.
  - `_MoonlightWindTreeIntensity` / `_MoonlightWindFlowerIntensity` — master sway amplitude per
    category.

The displacement itself (`ApplyMoonlightWind` in `RetroWind.hlsl`) offsets the object-space
vertex along the wind direction by two summed sine waves (different frequencies so it doesn't
look mechanical), scaled by the height falloff and the category intensity. The phase is keyed off
the **object's** world position (not per-vertex), so the whole crown swings as one coherent
motion and neighboring instances don't sway in lockstep with each other.

**Known limitation:** if these objects are ever build-time static-batched (they are not
currently — verified `isPartOfStaticBatch == false` on the MainMenu props), Unity bakes each
object's transform directly into a combined mesh and `unity_ObjectToWorld` no longer reflects the
individual object's real position. The per-object phase offset would then collapse to one shared
phase per batch — trees in the same batch would sway in unison instead of offset. Cosmetic only
(the sway itself still works correctly), but worth knowing if this shows up again on the Island
with a build that does batch aggressively.

## The controller

`RetroLitWindController` (`Assets/_Project/Code/Runtime/World/RetroLitWindController.cs`),
`[ExecuteAlways]`, sets the four globals every frame. One instance drives every wind-enabled
material in the loaded scene (globals are, by definition, global — one controller is enough per
scene; a second instance would just overwrite the first's values, harmless but redundant). Sits
on a standalone `WindController` GameObject in the scene hierarchy. Inspector sliders:

- **Wind Direction** (0–360°), **Wind Speed** (0–3)
- **Tree Intensity** (0–3), **Flower Intensity** (0–3) — the two controls Carlos asked for by name

## Reusing this on the Island / full game

1. On the target material, enable `_WindEnabled`, pick `_WindCategory`, and set `_WindHeight` /
   `_WindFlexibility` for that mesh (eyeball against the mesh's own bounds — height in the same
   local/object space the mesh was authored in, before the GameObject's own scale is applied).
2. Make sure a `RetroLitWindController` exists somewhere in that scene (one is enough).
3. That's it — no per-object script, no WindZone, nothing else to wire.

If a future scene wants a *third* category beyond Trees/Flowers (tall grass, bushes, whatever),
extend `_WindCategory` past 0/1 and add a matching global intensity + `lerp`/`step` chain in
`RetroWind.hlsl` — the pattern is meant to extend, not be redone.

## Materials currently opted in (MainMenu)

Trees (category 0): `M_TrunkB_PineBody`, `M_AP_BC_PineTree_02`, `M_TrunkA_PTree`,
`M_AP_Tree_Conifir_A_02_SM`, `M_AP_Tree_Burnt_04_SM` (flexibility 0.4, dead),
`M_AP_Tree_Lake_RoundTree_01_SM`, `M_AP_Tree_WNT_M_03_SM_extra27`, `M_AP_WhiteFir_MD_Dead_03`
(flexibility 0.4, dead), `M_AP_AlaskaCedar_001_2_extra0`, `M_AP_Tree_Blackpoplar01_SM`,
`M_AP_Tree_Blackpoplar01_SM_extra15`.

Flowers (category 1): `M_African_violet_blue_LOD`, `M_YellowAfricanDaisy_LOD`,
`M_Blue Aster_LOD`.

`AP_Flower_001_09` (staged in the scene) has no renderer yet — nothing to enable wind on until it
has a mesh assigned.
