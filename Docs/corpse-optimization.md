# Corpse optimization (MRM-85) — what a dead enemy still costs, and what we strip

Linear: **MRM-85** (Performance reports). Written 2026-09-30. Code: `CorpseOptimizer.cs`, `EnemyCorpseCleanup.cs`,
`EnemyDeathDrop.cs`, `LampFireEffect.cs`, tunables in `MoonlightTunables` ("Corpse optimization — MRM-85").
Change record: `Docs/performance-sessions.md` section 8, **C-012**. Hypotheses: H1, H3, H7, H10 (section 3 of the same doc).

## 1. Why

Builds 38-42: frame time 9.7 ms at 0 corpses, 24-29 ms at 60-100 corpses, SetPass ~600 -> ~2,000, **identical with and without
tree culling**. `EnemyCorpseCleanup` (since 2026-09-10) turned the AI off but left the whole render and physics side alive.

## 2. The decision (Carlos, 2026-09-30) — Legion mode now, story mode later

Applied for **tomorrow's Legion test**. Every step is a tunable in `MoonlightTunables`, so nothing is hard-wired:

| Step | Tunable | Legion (now) | Story mode (Carlos: "I want to shoot at corpses and dismember them") |
|---|---|---|---|
| master | `CorpseOptimizeEnabled` | on | on |
| wait after the AI strip | `CorpseSettleDelay` | 5 s (death + 3.5 + 5 = ~8.5 s) | same |
| 1. body and drop renderers stop casting shadows | `CorpseShadowsOff` | on | probably on |
| 2. Animator disabled (pose frozen) | `CorpseDisableAnimator` | on | on once nothing animates the body |
| 3. GoreSimulator disabled, blood particles stopped | `CorpseDisableGore` | on | **OFF** (dismembering needs it) |
| 4. dropped lamp: Light, rigidbody, colliders removed once it has burned out, or when the dissolve dims it (section 7) | `CorpseLampCleanupAfterOut` | on | on |
| 5. ragdoll CharacterJoints + Rigidbodies destroyed | `CorpseStripPhysics` | on | **OFF** (with 3) |
| 6. dropped prop shows one LOD only | `CorpseDropKeepLodIndex` | 1 (LOD1, one renderer instead of ~5) | 1 |
| 7. corpse culled by AST-145 while no camera ray hits it | `CorpseCullingEnabled` | on | on |
| 8. burn the corpse away and destroy the enemy (section 7) | `CorpseDissolveEnabled` (+ `CorpseDissolveDelay` 3 s, `CorpseDissolveDuration` 3 s, `CorpseLampDissolveDuration` 3 s) | on | **OFF** (corpses stay) |

**Carlos will set the real story-mode corpse behaviour after the big Fable story pass**, when the requirements are reorganised.
Until then the list above is the locked Legion behaviour. Corpses are never removed from the world (no despawn, no cap).

## 3. What each step does, and what it took to get right

- **Timing.** `EnemyCorpseCleanup` (3.5 s after death) is unchanged. The new pass runs `CorpseSettleDelay` later so the death
  animation and Blaze's ragdoll have finished; a disabled Animator keeps every bone where it is.
- **Step 4, the lamp (Carlos's correction 2026-09-30).** The lamp's own timeline is NOT touched: it ignites on settling, burns
  `LampFireBurnDuration` (15 s), the fire fades over 2.5 s and the light over `LampLightFadeDuration` (5 s). Only when that fade
  completes (`LampFireEffect.OnLampOut`) are the Light (plus its URP data and `WeatherLightSource`), the lamp's rigidbody and its
  colliders removed. The light is already at zero then, so nothing visible changes. The lamp mesh stays on the ground.
  (Its shadow casting is turned off by step 1 at settle time, while it is still burning.)
- **Step 6, the shotgun.** `EnemyDeathDrop.DroppedItems` is new (read-only list). The dropped shotgun is `DBShotgun` with children
  `..._LOD0` (about 5 renderers), `_LOD1`, `_LOD2`, **no LODGroup**: only LOD0 is active. `KeepOnlyLod` switches it to LOD1
  (the LOD1/LOD2 children start INACTIVE, so the chosen one must be activated, not just the others hidden; the first version
  left the shotgun invisible, found in the live test).
- **Step 7, culling.** `CorpseOptimizer` raises `CorpseCullingHooks.CorpseSettled`. The subscriber,
  `Assets/ThirdParty/AST-145/MrMoonlightBridge/CorpseCullingBridge.cs` (own asmdef; **git-ignored like the asset**), adds a hidden
  `BoxCollider` (layer `ACSCulling`, sized to the body bounds) and a custom `DC_CustomTarget` whose visible/invisible events switch
  the body renderers on and off. Corpses use **their own controller** ("Dynamic Culling (Corpses)", ID 1, `MergeInGroups` off,
  same 2 s lifetime), so a corpse is judged on its own box and not dragged along by the trees in its 10 m cell.
  Without the bridge or without a scene controller nothing happens (a clone without the asset still builds).
  The enemy's own firearm mask (`393` = layers 0, 3, 7, 8) does not include layer 29. **Player bullets against the proxy box
  are NOT verified** (same open item as bullets vs leaves).
- **Step 3 and 5 need each other** (a dismemberable corpse needs GoreSimulator AND its rigidbodies and joints).

## 4. Verified live (Play Mode, LightingTestScene, 2026-09-30)

Spawned an `Enemy_Spotter`, killed it, waited: settled count 1; Animator off; 0 rigidbodies, 0 joints; GoreSimulator off;
all shadow modes Off; shotgun LOD1 active only; proxy box on layer 29 (2.04 x 1.63 x 0.96 m); own controller created;
camera turned away for 4 s -> body renderer **off**, `culledNow 1`; turned back -> **on**, `culledNow 0`;
after the lamp's fade: no Light, no rigidbody, 0 colliders, `LampFireEffect` disabled, lamp mesh still visible.
**Not measured yet: the fps effect.** That is the next build's job (section 5).

## 5. Session log additions and the test

`[ENEMY]` lines and the `[PERF]` enemy part now read `corpses N (settled S, culling C/R)`: `S` corpses that finished the settle
pass, `R` registered with the corpse culler, `C` currently switched off. **Expected in the logs:** at the same kill count, SetPass
and frame time rise much less than in builds 38-42 (table in `culling-ast145.md` section 10); `C` grows when the player looks away.

**Build 43 "Corpse Opt"** (scenes LightingTestScene + Island_Legion, both with FullDisable tree culling): compare the fight
to build 42 (C, same start spot, same AKM + 100 kills). Buckets by corpse count: 0-9 / 10-29 / 30-59 / 60-100 in fps and SetPass.
Targets: 60-100 corpses should no longer sit at 34-42 fps with SetPass ~2,000.

## 6. Ideas not done

- Merge the body's materials / a cheaper corpse LOD (the body is already one SkinnedMeshRenderer, 4,311 verts, one material,
  so the gain is probably small; the drops were the multi-renderer part).
- Corpse count cap or fade-out (Carlos: not now).
- Stop the AudioSource on the corpse when idle (not measured).

## 7. Dissolve and removal (Carlos, 2026-09-30, later the same day)

Carlos: use the AST-063 Dissolve FX statue effect (Playground, `AlienStatue Variant` = its **Burn Dissolve**, material `Statue 1`) to dissolve the enemies; once one is on the ground wait
about 3 s, burn the body away, then the lamp (its light dims out with the dissolve), and when it is all gone **destroy everything that belonged to the enemy** so nothing stays in memory,
culling registrations included. Legion mode; story mode turns it off (tunable `CorpseDissolveEnabled`).

**Timeline per corpse** (all times are tunables): death, +3.5 s AI strip (`EnemyCorpseCleanupDelay`), +5 s settle pass (`CorpseSettleDelay`), +3 s lying still (`CorpseDissolveDelay`),
body burns 3 s (`CorpseDissolveDuration`; instant when no camera sees it), then lamp and dropped props burn 3 s (`CorpseLampDissolveDuration`) while the lamp light, the fire light and the fire sound fade to zero,
then the enemy, its drops, its culling proxy and its registrations are destroyed. About 17 s after death in total.
**This replaces step 4's rule** "the lamp keeps its own timeline" (Carlos's earlier correction): the dissolve now cuts the lamp's burn short and dims it (`LampFireEffect.FadeOutAndFinish`);
a lamp that burns out before the dissolve (burn 15 s + fades) still cleans itself up as before.

**How the effect was done.** The statue uses a URP Lit Shader Graph (PBR look, smooth). The enemies are RetroLit (hand-written HLSL, retro look), and swapping a corpse to a Lit material would make it pop
from pixelated to smooth. So the **look was ported into RetroLit** instead (`Code/Vendor/Retro Shaders Pro/Shaders/`): a `_USE_DISSOLVE` feature, off for every existing material. What was taken from the graph:
the orange HDR burn colour (9.86, 2.29, 0.52), a noisy ragged front driven by its guide texture (`Ragged_12`), a glowing edge and a charred (darkened) band in front of it, `_AlphaClip` dissolve.
What was not: triplanar UV options, vertex displacement, back-face colour, the `Dissolver` component (replaced by `CorpseDissolve`). Files moved into MrMoonlight (not the asset): only the noise texture,
`Art/VFX/Dissolve/Dissolve_Noise.png` (the 1024 px 16-bit `Ragged_12` converted to 256 px 8-bit and histogram-equalised, 41 KB).
- `RetroSurfaceInput.hlsl`: `_Dissolve*` properties in the `UnityPerMaterial` buffer (unconditional, so the layout is identical in every pass), `_DissolveNoise`, `DissolveDistance(positionWS)`.
- `RetroLit.shader`: properties under "Burn Dissolve", `shader_feature_local_fragment _USE_DISSOLVE` in the forward, DepthOnly and DepthNormals passes; in the forward fragment: clip, char, and `finalColor += burn colour * glow` after fog.
  `RetroDepthOnlyPass.hlsl` and `RetroDepthNormalsPass.hlsl` got a `positionWS` varying and the same clip (so a burnt hole is not still in the depth buffer, which HAZE fog and SSAO read).
  **The shadow caster pass was not changed**: corpse renderers have shadows off (step 1). If a dissolving object ever needs to cast shadows, add the clip there too.
- The front sweeps from the top of the object to the bottom (world Y range of the measured, baked mesh bounds) and is broken up by three planar world-space noise samples; every value is a material property.

**Material variants.** Each enemy material has a `<name>_Dissolve.mat` copy (feature on, noise set): `M_Spotter`, `M_Lamp`, `M_LampGlass`, `M_Shotgun`, `M_FlareGun`. They are listed on the new
`CorpseDissolve` component on `Enemy_Spotter.prefab` (source material -> variant) so they are serialized into the build (a runtime-only variant would lose the `_USE_DISSOLVE` shader variant to stripping).
Regenerate them with the menu **Mr. Moonlight > Corpse Dissolve > Rebuild Material Variants** after changing an enemy's materials (it overwrites the variants' dissolve settings).
The swap happens only when a corpse starts dissolving, per-corpse amount and height range go in a `MaterialPropertyBlock`, so living enemies pay nothing.
Tweak the look (colour, widths, noise scale/influence/contrast, hardness) in the `*_Dissolve.mat` files; the defaults are in `RetroLit.shader`.

**Cleanup, and what frees what.** `CorpseDissolve.Run` destroys the lamp, the shotgun and the enemy root. The AST-145 source object is destroyed by the asset's own observer; the asset only prunes its static
collider table on scene unload, so the bridge removes the corpse's proxy collider from it first (`CorpseCullingHooks.CorpseRemoving`). Live test: 3 corpses, 3 dissolved, `culling 0/0`, hitable table back to its
8,786 tree entries, 0 `DC_SingleSource` objects left. `SessionLog` forgets destroyed corpses, so `corpses N` now falls back to the live count.
Not freed (not owned by the enemy): blood decals and pieces spawned by Blood Factory / GoreSimulator in the world; if they accumulate they are the next thing to look at.

**Verified live (Play Mode, 2026-09-30):** whole timeline, counters, no console errors; the burn front on the body (screenshot `Docs/corpse-dissolve-midburn.png`: red jacket with glowing yellow-orange holes at amount 0.4).
**Bugs found while testing:** `SkinnedMeshRenderer.bounds` of a lying body was 2.5 m tall (burn front would have started late), and `BakeMesh` keeps the bind-pose bounds unless `RecalculateBounds()` is called:
the height range now comes from a baked mesh (once, when the dissolve starts). A body the culler had switched off at the start would have popped back in mid-burn: all body renderers get the dissolve material now.
**Not verified:** the look in the real build (HDR glow vs the CRT/bloom post stack), behaviour with 20+ dissolves at once, bullets landing on a half-dissolved body (colliders are already off). The `[CORPSE]` log lines
(`dissolve body ... seen True, duration 3.0s`, `freed ... after 6.0s`) are two lines per corpse; remove them if the log gets too noisy.

**Log fields:** `corpses N (settled S, dissolved D, culling C/R)` (SessionLog v5.3). Expected: `corpses` stays around (kills in the last ~17 s) instead of climbing to 100; SetPass and frame time should stop rising with the kill count.
