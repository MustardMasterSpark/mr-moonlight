# 3D Asset Pipeline — maps, process, and Unity settings

**Owner issue:** MRM-70 · **Written:** 2026-08-24 · **Status:** pipeline definition, pre-first-asset

> **Platform note, 2026-08-27.** This document was written against a WebGL target that was **dropped
> on 2026-08-25** (see `Docs/pc-build-target.md`). Every rule here survived the change — the
> constraints that shaped it (draw calls, one material per tree, three LODs, Point filtering on
> BaseColor) are hardware facts, not browser facts. §6 and §6.2 have been re-grounded in place;
> **§6.2's numbers have since been superseded by measurement**, noted inline.
>
> ✅ **That gap is closed.** The texture reduction + pixelation pass for imported third-party 3D
> assets — which §4 did not cover — is now **MRM-72**, `Docs/3d-prop-pipeline-wizard.md`.
> `MoonlightTextureImporter.cs` was extended rather than replaced, as required.
>
> ⚠️ **Read the wizard first for any new prop.** This document remains authoritative for Blender
> export conventions, baking mechanics, UVs, poly reduction, Unity mesh/texture import settings,
> LOD rules and the vegetation budget — but **§2's map set is superseded** (see the banner there).

How every 3D asset in Mr. Moonlight gets from wherever it came from to a prefab in the scene,
with the same look. This is the reusable recipe — trees, rocks, props, and eventually characters
all run through it.

**Style baseline:** the Retro Realism *Lonely Forest* pack (`E:\Props\Environment\LonelyForest`).
Its texture treatment is the target. Its *resolution* is not — see §4.

**Prerequisite:** the Blender export conventions (1 unit = 1 m, apply transforms, feet origin,
`bake_space_transform=True`) are unchanged and still apply to every export. This document
extends them; it does not replace them.

---

## 0. Start here — getting Retro Realism vegetation into the game

**No Blender. No Substance. About ten minutes per batch.** Do this first, before worrying
about anything else in this document.

### Step 1 — unzip the pack

Extract `RETRO REALISM - Lonely Forest - Assets.zip` somewhere outside the Unity project,
e.g. `E:\Props\Environment\LonelyForest\extracted\`.

⚠ **Do not drag the whole thing into Unity.** It contains 209 MB of audio we don't want.

### Step 2 — pick the props you want

From `Mesh/`. For a first vegetation pass, something like:

```
Tree1.fbx  Tree2.fbx  Tree3.fbx  Tree4.fbx
Bush1.fbx  Bush2.fbx  Bush3.fbx
Fern1.fbx  Fern2.fbx
Sapling1.fbx  Sapling2.fbx
Stump1.fbx  Stump2.fbx  Log1.fbx  Log2.fbx  Log3.fbx
Boulder1.fbx ... Boulder5.fbx
```

**Skip `VertexScene.fbx`** — that's the vendor's 7 MB demo scene, not a prop.
Tree collision meshes (`Tree1Collision.fbx` etc.) are useful — grab them alongside their trees.

### Step 3 — copy them into Unity

Put meshes and their textures under `Assets/_Project/Art/Environment/`.

Which textures go with what: the pack uses **atlases**, so several props share one texture.
`Trees.tga` covers all four trees, `Boulders.tga` covers all five boulders, and so on.
You need far fewer textures than meshes.

### Step 4 — rename the textures

Add `_BaseColor` before the extension. This is what makes the automatic importer fire:

```
Trees.tga     ->  Trees_BaseColor.tga
Boulders.tga  ->  Boulders_BaseColor.tga
Bush1.png     ->  Bush1_BaseColor.png
```

Keep `.tga` / `.png` as they are — no format conversion needed, Unity reads both.

**Do not run these through `texture_pass.py`.** Retro Realism's textures are *already* in the
target style — that's why we picked it as the baseline. The pixelation tool is for assets that
come from somewhere else.

### Step 5 — let Unity import them

Nothing to do. `MoonlightTextureImporter.cs` sees the `_BaseColor` suffix and applies the right
settings automatically — including **Filter Mode = Point**, which is the one that matters.
It logs what it did in the Console.

### Step 6 — make a material per texture

One material per *atlas*, not per prop.

1. Create Material, shader **URP/Lit**
2. Drag the `_BaseColor` texture into **Base Map**
3. **Metallic 0**, **Smoothness 0.1**
4. If the texture has transparency (the `.png` foliage ones): tick **Alpha Clipping**.
   Opaque `.tga` textures (trunks, rocks, cliffs) leave it off.
   **Never set Surface Type to Transparent** — it sorts badly and costs overdraw.

### Step 7 — drag into the scene, save as prefab

Assign the material, add a collider (**Capsule at the trunk for trees**, never a Mesh Collider
on a canopy), mark it **Occluder + Occludee static**, and save it to
`Assets/_Project/Prefabs/`.

**That's it — vegetation is in the game.** Everything below this section is for assets that
*aren't* already in the right style.

### Optional, later — adding Normal and Mask maps

Retro Realism ships albedo-only, and it looks correct that way in daylight. Extra maps are a
**polish pass, not a prerequisite**, and they need Blender.

Do this only when you have a reason to — a rock that reads flat under the flashlight, say —
and **do one asset first and look at it** before batching. See §2.1 for why synthesised normals
can make a flat stylised surface look worse rather than better.

---

## 1. The four lanes

Most assets do not need the whole pipeline. Pick the shortest lane that applies.

| Lane | When | Steps |
|---|---|---|
| **A — Import-ready** | Already low-poly, already carries our full map set | Unity import settings only (§5) |
| **B — Map regeneration** | Geometry is fine, maps are missing or wrong-style | Auto-unwrap if needed → bake/derive map set (§3) → pixelate (§4) → Unity (§5) |
| **C — Full build** | AI-generated or hi-poly source | Poly reduction (§2) → auto-unwrap → bake hi→lo (§3) → pixelate (§4) → Unity (§5) |
| **D — Manual retopo** | Decimate produced unusable topology | Hand or assisted retopo, then rejoin at Lane C's bake step |

**Retro Realism runs through Lane B**, not Lane A. It ships albedo-only, and the project standard
is the full map set (§2), so every prop we use from it gets its maps regenerated. That is the
first production task under MRM-70.

**Lane D is opt-in and manual.** Decimate is the default; Lane D exists for the assets where it
visibly fails. Carlos calls it per asset — Claude does not silently escalate into it.

---

## 2. The map set — what every asset ships

> ## ⚠️ SUPERSEDED 2026-08-27 — see `Docs/3d-prop-pipeline-wizard.md` §2 (MRM-72)
>
> **This section is wrong for anything on `RetroLit`, which is everything.** It mandates a third
> packed **Mask** texture. `RetroLit.shader` exposes only `_BaseMap`, `_NormalMap`,
> `_NormalStrength`, scalar `_Glossiness` and `_ReflectionCubemap` — there is **no
> `_MetallicGlossMap`, no `_OcclusionMap`, and no emission map anywhere in Retro Shaders Pro.**
> A Mask built to the table below would be shipped, compressed, loaded into memory and **sampled
> by nothing.**
>
> **The live standard is two maps:** `T_<Prop>_BaseColor` (with **AO multiplied in**) and
> `T_<Prop>_Normal`. Metallic and smoothness are the material's scalar `_Glossiness`. Emissive
> objects get a **real Light on the prefab**, not a map.
>
> The rest of this document — Blender export conventions, baking mechanics, UVs, poly reduction,
> Unity mesh/texture import settings, LOD rules and the vegetation budget — **is still current.**
> Only this section's map set is replaced.

This is the standard. It does not vary per asset, so nobody has to decide per prop.

URP Lit reads these channels, and **two of the slots share one texture file**:

| Unity material slot | Texture | Channels used |
|---|---|---|
| `_BaseMap` | **BaseColor** | RGB = albedo · A = alpha (cutout foliage) |
| `_BumpMap` | **Normal** | tangent-space normal |
| `_MetallicGlossMap` | **Mask** | **R = metallic** · **A = smoothness** |
| `_OcclusionMap` | **Mask** *(same file)* | **G = occlusion** |
| `_EmissionMap` | **Emission** | RGB = emissive colour · only when the asset emits |

**Three texture files per asset** (four if emissive) covering all five of the channels we want.
Metallic, smoothness and occlusion are packed into one **Mask** texture and that same file is
assigned to both `_MetallicGlossMap` and `_OcclusionMap` — URP reads different channels from
each slot, so this is free and it is the standard trick, not a hack.

**Mask packing layout** — R = metallic, G = occlusion, B = unused (write 0), A = smoothness.

### 2.1 Two honest caveats

**Normal maps for Lane B are synthesised, not measured.** When an asset has no hi-poly source
(all of Retro Realism), there is no real surface detail to bake. Deriving a normal map from
albedo luminance treats *colour* variation as *height* variation — a dark leaf becomes a dent.
It is often worse than no normal map at all on a flat stylised surface.

**Rule: run one asset through and look at it before committing the batch.** If the derived normal
does not visibly help under a moving flashlight, ship a flat normal (`(128,128,255)`) or omit the
map and let the material use its default. Do not generate 40 bad normal maps on principle.

**Occlusion is real even in Lane B.** AO bakes from the low-poly's own concave geometry, which is
genuine information regardless of whether a hi-poly exists. Always bake it properly.

---

## 3. Baking in Blender

Blender does all baking. Substance Painter stays available for hand-painting a hero asset
(§4.3) but is not part of the automated path.

### 3.1 Bake passes

| Our map | Cycles bake type | Settings |
|---|---|---|
| **BaseColor** | `Diffuse` | Direct **off**, Indirect **off**, Colour **on** — yields flat albedo |
| **Normal** | `Normal` | Selected-to-active, tangent space |
| **Occlusion** | `Ambient Occlusion` | Bakes from geometry |
| **Smoothness** | `Roughness` → **invert** | Smoothness = 1 − roughness |
| **Metallic** | ⚠ **no native pass** — see below | |
| **Emission** | `Emit` | Only for emissive assets |

**⚠ Cycles has no Metallic bake pass.** The standard workaround: temporarily wire the material's
Metallic input into an Emission shader and bake `Emit`. For this project it is usually moot —
almost nothing is metal, so metallic is a constant 0 and the Mask's R channel can just be filled
flat without baking at all. Bake it only for genuinely metallic props.

### 3.2 Normal map orientation

**Blender bakes +Y (OpenGL). Unity expects +Y (OpenGL). No green-channel flip.**

This trips people up because Unreal uses −Y (DirectX) and most online advice assumes Unreal. If a
surface looks lit from the wrong vertical direction, *then* suspect the green channel — but the
default path needs no flip.

### 3.3 UVs

Per Carlos's decision: **auto-unwrap, do not hand-unwrap.** Smart UV Project on the low-poly, bake
the hi-poly's detail down onto it, and fix any problem areas by painting directly on the model
afterwards rather than by fighting the unwrap.

- **Island margin ≥ 0.02** — prevents bleeding between islands once mipmaps are on
- **Bake margin 4–8 px** — extends texels past island edges so seams don't show at distance
- **Characters are the exception.** They get proper attention to UV layout, because texture space
  on a face is scarce and seams on a deforming mesh are visible. Auto-unwrap is fine for props;
  it is not fine for a head.

### 3.4 Poly reduction (Lane C)

**Default: the built-in Decimate modifier.** Free, already installed, adequate for static props at
these texture resolutions.

- **Collapse** — organic shapes (rocks, foliage, terrain features). Enable **Keep UVs**.
- **Planar** — hard-surface shapes (crates, machinery, architecture)
- **Un-Subdivide** — meshes that were subdivided from a clean base

**On paying for a retopo addon:** not yet. For static props seen in the dark at 512 textures,
Decimate is genuinely sufficient and **Quad Remesher (~$109) would be spent on quality nobody
sees**. It becomes worth buying when we reach **characters**, where edge loops around deforming
joints matter enormously and Decimate's triangle soup actively breaks skinning. Revisit at the
character milestone, not before.

**Blender's built-in QuadriFlow** (`Object → Quad Remesh`) is the free middle ground for organic
meshes needing clean quads. ⚠ **It discards UVs** — anything remeshed this way must be
re-unwrapped and fully re-baked, so it belongs in Lane C or D, never as a quick fix.

**⚠ Hunyuan's poly-cleanup does NOT preserve UVs** (confirmed by Carlos, 2026-08-24). So Lane C
always requires a fresh unwrap on the reduced mesh followed by a full bake from the hi-poly. There
is no shortcut where the generated texture is reused directly — plan for the bake every time.

---

## 4. The pixelation pass

### 4.1 What the style baseline actually does — measured

Read directly out of the Retro Realism pack, not estimated:

| Property | Measured value |
|---|---|
| Texture resolution | 256×256 dominant; 512 for cliffs/treelines; 1024 for the `Trees` atlas |
| Colours per channel | **9–12 distinct levels** (60–310 total colours per texture) |
| Normal maps | **none — the pack is albedo-only** |
| Dither | `Bayer4x4.tga` — the textbook 4×4 ordered Bayer matrix, gamma-encoded |
| `CLT4` / `CLT8` | 256×16 LUT strips: 2-bit (0/85/170/255) and 3-bit (8 levels) per channel |

Critically, the pack's prop textures **do not** conform to the CLT grids. So it does two separate
things: textures hand-authored to a tight ~10-level palette, **plus** an optional global LUT and
dither applied in screen space. We copy the first; the second is available later as a free
unifying pass (URP folds colour grading into UberPost, so it costs no extra render pass).

### 4.2 What our pass does

**Colour quantisation and dither only. Resolution is handled by saving at the right size** — we do
not downscale-then-upscale. A "512 texture" means a 512×512 file with 512 real texels, point-filtered.
If a prop reads as too smooth, save it at 256 rather than faking chunkier pixels inside a 512 file.

The operation, per the measurements above:

1. Nearest-neighbour resize to target resolution (if resizing at all)
2. Per-channel quantise to **8–12 levels**
3. **Bayer 4×4 ordered dither** before quantising, using the pack's own matrix:

```
[[ 0,  8,  2, 10],
 [12,  4, 14,  6],
 [ 3, 11,  1,  9],
 [15,  7, 13,  5]]   / 16
```

**Applies to BaseColor and Emission only.** Never quantise or dither a Normal or Mask map —
banding in a normal map becomes faceted lighting, and banding in smoothness becomes visible
stepping in specular response.

### 4.3 Where it runs

**Automated: a Python script in `Tools/pipeline/`.** No Substance dependency, runs on a folder in
seconds, unattended. Because the operation is measured rather than guessed, output should sit very
close to Pixel8r's per-channel mode.

**Manual escape hatch: Substance Painter + `Pixel8r_2.72.sbsar`** for a hero asset that wants
hand attention. Pixel8r is a Substance *filter graph*, not a plugin — drag it into the layer stack
as a filter. The settings that correspond to the recipe above:

| Pixel8r parameter | Value |
|---|---|
| `QuantizeType` | `3` — Per Channel |
| `QuantizeColorsOrBits` | `0` — Colors Per Channel |
| `QuantizeColors` | `8`–`12` |
| `DitherType` | `2` — Bayer |
| `DitherBlendMode` | `1` — Soft Light |
| `DitherStrength` | `0.25` starting point |
| `DownscaleMode` | `0` — Nearest Neighbor |
| `FilterPreview` | `1` — None ⚠ **must be None when exporting** |

⚠ `FilterPreview` defaults to a filtered preview. Exporting with it enabled bakes blur into the
texture — the plugin's own tooltip warns about this.

---

## 5. Unity settings — the per-prop checklist

### 5.1 Texture import

**The single most important setting is Filter Mode = Point.** Bilinear filtering blurs carefully
quantised pixels straight back into mush, and it is the default. Getting this wrong undoes the
entire pipeline.

| Setting | BaseColor | Normal | Mask | Emission |
|---|---|---|---|---|
| Texture Type | Default | **Normal map** | Default | Default |
| sRGB | ✅ **on** | ❌ off | ❌ **off** | ✅ on |
| Filter Mode | **Point** | Bilinear | Bilinear | **Point** |
| Aniso Level | 0 | 0 | 0 | 0 |
| Max Size | **512** | 256 | 256 | 512 |
| Compression | DXT1 / DXT5 if alpha | DXT5 | DXT5 | DXT1 |
| Mip Maps | ✅ on | ✅ on | ✅ on | ✅ on |
| Read/Write | ❌ off | ❌ off | ❌ off | ❌ off |

**Why Normal and Mask are Bilinear, not Point:** only the *colour* is pixelated. Point-filtering a
normal map produces faceted lighting; point-filtering smoothness produces stepped highlights.
Smooth normals over chunky albedo is exactly the "retro realism" hybrid the pack is named for.

**Why Normal and Mask are half resolution:** they carry lower-frequency information than albedo,
and halving them is the cheapest quality-neutral saving available.

**⚠ sRGB off on Normal and Mask.** Wrong sRGB looks like a lighting bug and costs a day to find.

**Resolution policy (Carlos, 2026-08-24):** **512 is the default** for BaseColor. We are not bound
to Retro Realism's 256. Characters may go higher. Anything above 512 for a prop is a per-prop
decision with a stated reason, not a default.

**Sub-128 textures: skip compression.** A 128×128 uncompressed RGBA32 is 64 KB; a 512×512 DXT1 is
128 KB. For small pixel-art textures, uncompressed is both smaller *and* cleaner, because DXT
banding fights the quantisation.

### 5.2 Mesh import

| Setting | Value |
|---|---|
| Scale Factor | **1** (never compensate here — fix the Blender export) |
| Mesh Compression | Low, or Medium for dense foliage |
| Read/Write | ❌ off |
| Optimize Mesh | ✅ on |
| Normals | Import (Calculate only if the export lacks them) |
| Tangents | **Calculate Mikktspace** |
| Materials | **None** — we create materials by hand, never let the importer generate them |
| Rig / Animation | None, for static props |

**Verify on import:** the placed instance reads Position (as set), Rotation `(0,0,0)`,
Scale `(1,1,1)`. Any hand-tuned rotation or scale on the instance means the *export* is wrong —
fix it in Blender and re-export over the same filename to preserve the asset GUID.

### 5.3 Material

- Shader **URP/Lit**. Not Simple Lit — Lit keeps compatibility with volumetric fog, reflection
  probes and SSAO, and keeps one material family. Simple Lit is a *measured* optimisation to
  revisit only if a build report flags shader variants.
- Workflow **Metallic**, Metallic `0`, Smoothness `~0.1` when there is no Mask map
- Surface Type **Opaque**; foliage uses **Opaque + Alpha Clipping**, never Transparent
  (transparent foliage sorts badly and costs overdraw)
- Emission off unless the asset is one of the deliberate emitters

### 5.4 Prefab

- Collider: **Mesh Collider** only when the silhouette matters; otherwise Box or Capsule.
  Trees get a Capsule at the trunk, never a Mesh Collider on the canopy.
- **Terrain layer must match the footstep system** — grass, leaves, wood, concrete (MRM-70 scope)
- LODs where the asset count justifies it (§6)
- Static flags: **Occluder + Occludee static** for anything that does not move — the `umbra`
  occlusion module is kept in the build specifically for this

---

## 6. Budget

The environment/prop texture budget is **60 MB** (ceiling 80 MB); characters are a separate
25 MB. At the §5.1 settings, one prop's full map set is:

| Map | Size |
|---|---|
| BaseColor 512 DXT1 | 128 KB |
| Normal 256 DXT5 | 64 KB |
| Mask 256 DXT5 | 64 KB |
| **Total per asset** | **~256 KB** |

That allows roughly **230 unique prop map-sets** inside the environment budget. Comfortable.

**Atlas rather than counting props.** Retro Realism does not ship one texture per prop — `Trees.tga`
covers four trees, `Boulders.tga` covers five boulders. Regenerate maps **per atlas**, not per prop.
The whole forest set is roughly 15 atlases ≈ 10 MB, which is a fraction of the budget.

**Triangle guidance** (starting points, to be confirmed against a real build):

| Asset | LOD0 |
|---|---|
| Tree | 300–800 tris, LOD1 ~150, billboard past 30 m |
| Rock / boulder | 100–300 tris |
| Small prop | 50–200 tris |
| Character | 3 000–6 000 tris |

~~The WebGL quality tier already caps terrain trees~~ — **corrected 2026-08-27:** the live tier is
**`PC` / `PC_RPAsset`**, tree distance **1500 m**, `detailObjectDistance` **80 m**,
`heightmapPixelError` 3, `drawInstanced` **false**. See `Docs/pc-build-target.md` §5. Per MRM-70, vegetation counts are deliberately **not** routed through
`MoonlightTunables` yet — place freely, measure a real build, tunable-ise only if a problem appears.

### 6.1 ⚠ Do not bulk-import the Retro Realism pack

Measured contents of the zip:

| Category | Size |
|---|---|
| **WAV audio** | **209 MB** — 70% of the entire 300 MB build target |
| Textures (raw TGA/PNG) | 41 MB |
| FBX | 9.5 MB, of which `VertexScene.fbx` alone is 7 MB (the vendor's demo scene) |

Import **only the specific meshes and textures we use**. The audio does not enter the project at
all without going through the audio import presets and a deliberate decision.

---

## 6.2 Vegetation at scale — the rules that make thousands of trees possible

The island is **4103 × 260 × 7085 m**. Densely populating the walkable area means thousands of
trees. That is achievable, **but only if the assets are authored for it.** These are
pipeline requirements, not placement settings — getting them wrong means re-exporting every tree.

**Triangles are not the constraint. Draw calls are.** This was written about WebGL's per-draw-call
overhead, and **the platform changed on 2026-08-25 without changing the conclusion** — it is still
the governing fact, just with roughly 10× more headroom. 15 000 trees drawn as instanced terrain
trees is a handful of draw calls; the same 15 000 as individual GameObjects is 15 000 draw calls.

> **Measured since this was written.** The island hit **21,946 draw calls at 19 FPS** doing exactly
> the wrong thing — Unity terrain trees do not batch, and each tree cost **3 draw calls** because its
> mesh carried three submeshes on three materials. **Flora Renderer 6** now draws the vegetation:
> **38,980 → 535 draw calls, 70.4 M → 126 K triangles, ~505 FPS in build.** Rule 1 below (one
> material per tree) is what made that possible and is still mandatory for new assets.

### Rule 1 — every tree shares ONE material

Instancing batches by material. Four tree species on one atlas with one material batch together;
four species with four materials do not.

**So: new trees are atlased *into* the existing tree sheet, not given their own texture.** This is
the rule most likely to be broken by accident when a new pack arrives. Retro Realism already works
this way — `Trees.tga` is 1024 covering four trees.

Prefer **one 1024 atlas covering many trees** over many 512s. It is better for instancing *and*
smaller overall.

⚠ The material must have **Enable GPU Instancing** ticked, and the Terrain component must have
**Draw Instanced** ticked. Neither is on by default.

### Rule 2 — every tree ships three LODs

Authored in Blender, as part of the asset, via Decimate:

| LOD | Budget | How |
|---|---|---|
| **LOD0** | 300–800 tris | The authored mesh |
| **LOD1** | ~150 tris | Decimate the LOD0 |
| **LOD2** | 4–8 tris | Two crossed quads using the same atlas |
| Cull | — | Past the fog wall |

LOD2 being a cross-card is what makes distant forest nearly free. Note that Unity's
*Billboard Start* / *Max Mesh Trees* terrain settings apply to **SpeedTree** assets; for plain
mesh prefabs it is the **LODGroup** that does the work, so the LODs must actually exist on the
prefab.

### Rule 3 — the darkness does the culling

This is a **night** horror game with fog. The player cannot see 150 m with a flashlight, so we do
not need to draw trees at 1500 m.

Tree draw distance is **still 1500 m** on the live `PC` tier. The advice below was written when
that was expensive; **it is no longer urgent** — Flora culls on *screen coverage* rather than
distance (`MaxRenderDistance = 0`, i.e. unlimited; `MinScreenSize = 0.005`), so a hard distance clamp
is not what governs cost any more.

**It is still worth doing eventually:** match tree draw distance to the fog end distance and it costs
nothing visually, because the fog already hides what it culls. **But do not set it before checking
MRM-49** — the telescope sequence depends on distant trees *appearing* as the FOV narrows, which is a
property of Flora's screen-coverage culling and would be broken by a hard distance clamp.

### Rule 4 — density follows the walkable area

Per `Docs/Design/Island-Terrain-Reference/Map/player walkable area.png`:

| Zone | Treatment |
|---|---|
| **Green walkable area** | Full density — this is where the game happens |
| Immediate surroundings | Sparse — enough to block sightlines, no more |
| **Second island / far shore** | **`Treeline1.fbx` / `Treeline2.fbx` cards, not trees at all** |
| Behind cliffs, out of sight | Nothing |

The pack's treeline cards exist precisely for the distant-forest-wall job. The far island should
cost a few dozen triangles total, not a few thousand trees.

Vegetation Spawner (the free Staggart Creations one — **not** Flora Instancer, which we do not
own) supports density masks, so the green zone can be painted directly.

### The resulting budget

| Quantity | Figure |
|---|---|
| Tree instances on the terrain | **8 000–15 000** — comfortable |
| `TerrainData` cost | ~32 bytes/instance → **under 0.5 MB**. Storage is not the problem |
| Drawn at once (200 m + fog) | ~300–800 trees |
| Triangles on screen from vegetation | **~50–80 k** — well inside a ~150–250 k frame budget |
| Draw calls from vegetation | **A handful**, if Rules 1 and 2 hold |

**None of this is measured yet.** Per MRM-70 these numbers stay out of `MoonlightTunables` until a
real build says otherwise — place freely, build, measure, then tune.

---

## 7. Per-asset checklist

1. Pick the lane (§1)
2. Poly-reduce if needed — Decimate first, Lane D only if it fails (§3.4)
3. Auto-unwrap the low-poly, island margin ≥ 0.02 (§3.3)
4. Bake BaseColor / Normal / AO / Roughness → invert to smoothness (§3.1)
5. Pack the Mask: R = metallic, G = occlusion, B = 0, A = smoothness (§2)
6. Pixelate BaseColor (and Emission) only — never Normal or Mask (§4.2)
7. Export FBX with the standing Blender conventions (1 m units, transforms applied,
   `bake_space_transform=True`)
8. Import to Unity, apply §5.1 texture settings — **check Filter Mode = Point on BaseColor**
9. Build the material (§5.3), build the prefab (§5.4)
10. Verify the instance transform is identity (§5.2)

---

## 8. Open items

| Item | Blocking | Owner |
|---|---|---|
| ~~Does Hunyuan poly-cleanup preserve UVs?~~ | **Answered 2026-08-24: no.** Lane C always unwraps + bakes | — |
| Do derived normal maps help Retro Realism? | Whether Lane B ships normals at all | One test asset, §2.1 |
| Which Retro Realism props are in scope | MRM-70 vegetation pass | Carlos |
| The other prop packs not yet discussed | Lane assignment for them | Carlos |
| Terrain layers for leaves / wood / concrete | Footstep system | MRM-70 — real gap, unresolved |
| Global LUT + grain post-process | Optional unifying pass (§4.1) | Later; free in UberPost |
| ⚠ Haze volumetric fog | Would consume one of only **two** custom fullscreen passes; `MoonlightScreenFX` (MRM-53) claims the other | Decide before committing |
