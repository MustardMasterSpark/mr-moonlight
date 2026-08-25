# MRM-70 — Flora phase kickoff

Paste the prompt below to start a fresh session. Written 2026-08-25 after the PC platform switch and
the Flora/HAZE/Retro import, before the PSX material migration and the new-tree vegetation pass.

---

## Prompt to paste

> Resuming **Mr. Moonlight**, branch `mrm-70`, Linear issue **MRM-70**. Read `CLAUDE.md` first, then
> **`Docs/pc-build-target.md`** (this replaces `webgl-constraints.md`, which is historical),
> **`Docs/mrm70-biome-vegetation-strategy.md`** — especially §6b–6e, which record what was actually
> built and every bug found — and **`Docs/external-assets.md`**, which lists every third-party
> dependency (none are tracked in git; that doc is the only record). Also read
> `Docs/mrm70-flora-phase-kickoff.md` (this file) and check the `rendering-stack`,
> `verification-requires-a-build`, `pc-platform-switch` and `mrm70-vegetation-spawner-facts` memory
> entries. **Do not re-derive any of it.**
>
> **Where things stand.** The project is now a **Windows 64-bit standalone build at 1920×1080**
> (WebGL was dropped 2026-08-25 — it capped draw calls at ~1–3k and the island needed 22k). The
> island has 8 painted biome terrain layers, **34,816 tree/prop instances**, 27 grass/detail
> prototypes, all placed by **Vegetation Spawner** and rendered by **Flora** (535 draw calls,
> **505 FPS** in build). **HAZE** volumetric fog and **Retro Shaders Pro** CRT (RGB subpixels +
> scanlines) are wired and working.
>
> **Three jobs are queued, in this order, and Carlos gates each one:**
>
> 1. **PSX material migration** — migrate materials `URP/Lit` → `RetroLit` so PSX **vertex snapping**
>    works. This is the only PSX feature that cannot come from post-processing; it is per-material.
>    Scope: 24 vegetation materials + the terrain (use `RetroTerrainLit.shader`). Already verified
>    safe for Flora — `RetroLit.shader` includes `DOTS.hlsl`, so it is BRG-compatible. **After
>    migrating, verify Flora still renders and tree collision still works** — that is the one thing
>    that could break.
> 2. **New tree models** — Carlos is preparing more low-poly trees for variety (the current four read
>    as repetitive). They go through the existing pipeline in `Docs/3d-asset-pipeline.md`. **Wait for
>    his instruction.** When they land: clear the old vegetation and respawn in ONE motion (do not
>    clear early — the island must stay demoable), using `Docs/Design/Island-Terrain-Reference/Map/biomes.png`
>    as the reference. The five editor tools under `Tools/Mr. Moonlight/Vegetation/` do this; run
>    order is **Prep → Composer → Painter → Setup → Cull**.
> 3. **Wind** — make grass and trees sway. Flora *enables* animated shaders and per-instance motion
>    vectors but does **not** provide wind; it is a Shader Graph we author (vertex displacement by
>    time + world position, masked so trunks stay planted). Note grass currently gets its only wind
>    from Unity's built-in detail shader, and moving off that system loses it. If the new trees ship
>    with vertex colours authored for wind, use them.
>
> **Standing rules:** ask Carlos before Unity/Blender scene work, then do it, verify by reading real
> state back, and document. Never commit or push. No hardcoded values except vegetation/staging
> numbers, which stay out of `MoonlightTunables` until a real perf problem appears.

---

## What you must not get wrong

**Verification means a build.** Unity editor screenshots and `UnityEditor.UnityStats` are *not*
reliable here — the editor does not render while unfocused, so play-mode captures return the same
stale frame and A/B toggles return identical numbers. This cost real time twice on 2026-08-25.
Build, launch the exe windowed, capture its window. See the `verification-requires-a-build` memory.

**Flora renders, it does not place.** Vegetation Spawner stays. Flora has no spawning/painting tools.

**Four traps already paid for, do not rediscover them:**

| Trap | Symptom |
|---|---|
| Unity terrain ignores a tree prefab's **root transform** | trees render flat and 1/100 size |
| Terrain smoothness comes from the **diffuse texture's alpha** | ground looks like a mirror; the layer/material smoothness fields do nothing |
| Terrain trees **reject MeshColliders** (capsule/box/sphere only) | props silently have no collision |
| `HazeRendererFeature` returns early when **camera post-processing is off** | fog works in Scene view, absent in game |

**Known open issues** (not bugs to re-diagnose — they are logged and waiting):

- **14 of 27 detail prototypes do not render under Flora** — they are `GrassType.Texture` billboards
  on Unity's built-in grass shader, which is not BRG-compatible. Fix by rebuilding the GrassFlowers
  cards as single-mesh crossed quads on a URP/Lit (or RetroLit) material. Fold into job 2.
- **Terrain normal maps may still read as corduroy ripples** at `normalScale` 0.35; if so the fix is
  raising per-layer `tileSize`, not cutting normals further.
- **CRT darkens the image** — inherent to the RGB subpixel mask. `brightness` (default 1.0, range
  0–5) compensates; left at 1.0 pending an art call.
- **Fog density is placeholder**, currently Silent-Hill-thick (global 3.0). Judge it only against the
  real skybox — the current one is still the unapproved daytime placeholder from MRM-47/69.
- **Carlos is placing the player spawn himself** — it currently sits on the empty beach, 137 m from
  the campsite. Do not move it.

## Deadlines

**Sept 1** — playable loop, graded class gate. Assignment #10's *"playing within 2 minutes"*
criterion still applies; it just became download + extract + launch, so build size still matters
indirectly. **Sept 8** — polished release.
