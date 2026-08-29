# Gaia Stamper — lessons learned, 2026-08-28 session

Written after the MRM-70 session that pivoted from "erode the existing island" to "regenerate the
island from a Gaia Stamp." Several hours went into debugging things that turned out to have simple
causes. This doc exists so the next session (or the next project using Gaia's Stamper) doesn't
re-derive any of this from scratch.

---

## The big one: a scripted `Terrain` has no material by default

`terrainGO.AddComponent<Terrain>()` defaults to **`materialType = Custom` with `materialTemplate =
null`**. That terrain is completely invisible — no error, no warning, nothing in the console. It
looks exactly like "the terrain isn't there," "the stamp didn't take," or "the Editor is broken,"
and cost the most debugging time of the whole session before being found.

**Fix:** always explicitly assign `terrain.materialTemplate` right after creating a Terrain via
script. This project's terrain material is `Assets/_Project/Art/Environment/Terrain/M_IslandTerrain.mat`
(shader: `Retro Shaders Pro/Terrain/Lit`).

## Sea Level is not a local Stamper field — it's synced from a global session asset

The Stamper Inspector's "Sea Level" slider *looks* like a per-object setting
(`Stamper.m_seaLevel`), but `FitToTerrain()` / `UpdateStamp()` re-sync it from **`Gaia.GaiaSession`**
— an asset at `Assets/Gaia User Data/Sessions/GS-<timestamp>.asset` — via an in-memory singleton
(`Gaia.GaiaSessionManager.m_sessionManager.m_session`). Setting `Stamper.m_seaLevel` directly gets
silently overwritten back to whatever the session asset says on the next Fit/Update call — happens
on a **brand new** Stamper GameObject too, so it isn't per-object corruption.

**Fix:** to actually change sea level, set `m_seaLevel` on the *session* object (both the asset on
disk and, separately, the live in-memory copy the `GaiaSessionManager` singleton is holding —
editing the asset file alone did not update the cached copy the Stamper actually reads).

## `m_baseTerrainInputType` — leave it at the Reset() default when using a Stamp Image

`Gaia.BaseTerrainInputType` has three values: `Generator`, `Image`, `ExistingTerrain`. It's tempting
to set it to `Image` when assigning a Stamp Image — **don't.** That produced flat/degenerate
results in every test. The working recipe for stamping an Island/Mountain/etc. image onto a
terrain: assign `Stamper.m_stampImage`, call `LoadStamp()`, leave `m_baseTerrainInputType` at
whatever `Reset()` set it to (`Generator`), then `FitToTerrain()` → `UpdateStamp()` → `Stamp(null, null)`.

`m_baseTerrainInputType = ExistingTerrain` *is* correct for the separate use case of refining an
already-authored heightmap (erosion, noise) — that part worked fine all night. It's specifically
the Image-stamp case where `Image` as an explicit override breaks things.

## `Terrain.activeTerrains` needs a real tick before a fresh terrain is stampable

Creating a `Terrain` GameObject and calling `Stamp()` on it *in the same script execution*
consistently produced capped/flat results. Splitting terrain creation and the stamp call into two
separate tool calls (letting a real Editor tick pass in between) fixed it — `Terrain.activeTerrains`
appears not to include a terrain created moments earlier in the same call. If scripting this again:
create the terrain, confirm `Terrain.activeTerrains.Length` includes it, *then* stamp.

## Masks need Draw Instanced on

Any Stamper `ImageMask` (SlopeMask, etc.) needs terrain **Normal Maps**, which need **Draw
Instanced** enabled on the `Terrain` component. Without it, Gaia logs a console warning
(`"Normal maps missing on terrain..."`) and the masked stamp silently no-ops. Turn on Draw Instanced
before using any mask-based Stamper operation.

## A single `RaiseHeight` pass caps around ~10% of the terrain's height ceiling

With `blendStrength=1`, varying `blendStrength` up to 10, `normaliseStamp`, and the Stamper's own
`transform.localScale.y` all had **zero effect** on the resulting max height — it consistently
landed around 10% of `TerrainData.size.y`. Repeated stamps are also idempotent (same result every
time — it behaves like "raise to at least X," not additive). Root cause not fully identified. If a
single pass isn't dramatic enough: lower the terrain's height ceiling so the same relative relief
reads as taller in absolute terms, or follow the stamp with an erosion/noise pass (see below — this
part is verified to work well).

## Erosion + noise passes on `ExistingTerrain` work great and are the reliable path to drama

Unlike Image-based stamps, running `HydraulicErosion` (via the Stamper, `ExistingTerrain` input) and
a hand-rolled multi-octave Perlin noise layer (direct `SetHeights` manipulation, bypassing Gaia
entirely) both produced real, good-looking, verifiable relief — confirmed via before/after heightmap
diffs and close-up screenshots (carved gullies, not artifacts). This is the recommended way to add
drama on top of a base shape, whether that base came from a Stamp or from hand-sculpting.

## Gaia auto-creates two scene objects the first time you touch it

`Gaia Runtime` and `Gaia Tools` GameObjects appear automatically, each holding only a
`Gaia.GaiaSceneReadMe` component (no logic). `Gaia Tools` is tagged `EditorOnly` (auto-stripped from
any build). **`Gaia Runtime` is not** — it'll ship in a real build as dead weight unless removed
manually before Phase 3 (strip and remove).

## Editor screenshots lied again — and this time so did the Editor itself

Reinforces the existing `verification_requires_a_build` lesson. `manage_camera` screenshots came
back as flat fog/color for most of the session regardless of camera angle, even when the underlying
heightmap data was independently verified (via `GetHeights()` min/max/percentiles) to have real
relief. Low grazing-angle close-ups worked; high top-down and mid-distance shots mostly didn't
(HAZE fog). **Don't trust "the screenshot looks flat" as proof the data is flat** — read the
heightmap array directly.

Separately, a real Unity Editor crash happened mid-session: a background `AssetImportWorker`
crashed inside the D3D12 driver while generating an asset-preview thumbnail (unrelated to Gaia). By
the time it happened, **11 separate `Unity.exe` processes and 9 crash-handler processes** had
accumulated since the Editor was first opened that day — a sign the session had been unstable for
hours already, likely worsened by a 393MB package import plus dozens of scripted
`AssetDatabase`/`Stamp()` calls in one sitting. A full Editor restart resolved a cluster of
symptoms that looked Gaia-specific (values reverting between calls, clicks doing nothing) but were
probably just process-level rot. **If Gaia state starts behaving inconsistently after a long,
heavy session, restart the Editor before debugging further** — cheaper than chasing a phantom bug.

## Practical checklist for the next Stamper session

1. Create Terrain → **immediately assign `materialTemplate`**.
2. Enable Draw Instanced before using any mask.
3. If setting Sea Level, check the `GaiaSession` asset, not just the Stamper field.
4. For an Image stamp: `m_stampImage` + `LoadStamp()`, leave `m_baseTerrainInputType` alone.
5. Create → tick → stamp, not create-and-stamp in one shot.
6. Verify results via `GetHeights()` stats, not screenshots, until a screenshot actually shows relief.
7. Long heavy session acting weird? Restart the Editor before assuming it's a Gaia bug.
