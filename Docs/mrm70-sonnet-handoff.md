# MRM-70 — Sonnet session kickoff: Gaia biome vegetation spawn

Paste the block below as the first message of a fresh Sonnet session. Everything after it is
context for whoever is reading this file directly.

---

## The prompt

> We're continuing **MRM-70** on branch `mrm-70` — populating the Aanniarvik Island terrain with
> vegetation using **Gaia**. I've already done the analysis pass with Opus; your job is execution.
>
> **Read these first, in order:**
> 1. `Docs/mrm70-biome-distribution-measured.md` — **the plan.** All 9 biomes, measured spacing and
>    weights, slope/altitude rules, clustering settings, and a Gaia field-by-field execution guide
>    in §10. Read §6 (terrain state and the biome-map blocker) and §10 carefully.
> 2. `Docs/mrm70-resume-2026-08-30.md` — where the asset-prep work left off.
> 3. `Docs/mrm70-biome-vegetation-strategy.md` — **numbers and coordinates are superseded**, but §2
>    (terrain layers *are* the biome masks) and §5 are still the plan.
>
> **Do not use** `Docs/Design/Island-Terrain-Reference/Vibe/GPT biome analysis.md` for any number.
> It's art intent only — its spacing was written from screenshots with no prefab access.
>
> **Five things that will trip you up if you don't know them:**
>
> - **The live terrain is `Assets/Gaia User Data/Sessions/GS-20260829 - 011148/Terrain Data/
>   Terrain_0_0-20260829 - 035828.asset`** — 1024 × 1024 m at origin (−512, 0, −512), 64.2 ha of
>   land above sea level Y=8. `Island_TerrainData.asset` is a 4000 m **decoy** — flat, unused,
>   editing it changes nothing visible.
> - **The terrain has 0 layers, 0 detail prototypes, 0 trees.** Terrain layers *are* the biome
>   masks, so nothing can be biome-masked until they're rebuilt. The old 8-layer / 27-detail-
>   prototype config survives in `Island_Original_TerrainData_Backup.asset`.
> - **Prefab size = the `Visual` child's renderer bounds, never the collider.** The colliders span
>   full mesh height including below-ground root, so collider height is not a size signal.
> - **Trees must NOT align to normal; rocks, logs and root formations must.** The buried tree pivots
>   are what make vertical placement work on slope — don't "fix" them.
> - **All 154 curated prefabs must appear in the game.** Carlos was explicit. If you retune, change
>   **tiers**, not metres, and re-run `sh Tools/vegetation/build.sh` — it prints a warning if a
>   spacing rule has started silently starving its stratum target.
>
> **If the first generation looks sparse, patchy, or bald:** the slope and height filters are not
> baked in — they're per-rule `ImageMask` entries and can be relaxed or deleted freely. §3.5 has a
> table of what's safe to open up (grass, ground cover, and trees — that's where the coverage is)
> and the ~12 assets that genuinely need their cap (logs and the wide flat root formations, which
> tear through sloped terrain). Carlos's stated preference: he wants vegetation on those areas more
> than he wants ecological realism. Don't compensate by turning align-to-normal on for trees.
>
> **Where to start:** the biome map doesn't exist yet and is Carlos's call (§6.3). Ask him for it.
> Meanwhile the biome-independent groundwork can start: rebuilding the terrain layers and the
> 27-prototype grass detail pass (§4).
>
> **Project rules:** never commit or push (Carlos uses GitHub Desktop). Ask permission before any
> Unity scene/inspector work via the UnityMCP bridge, then verify by reading state back and document
> what changed. Read `CLAUDE.md` and `Claude Code Context MDs/kickstart.md`.

---

## What is already done

- **158 vegetation prefabs built** (137 `AP_*`/LOD + 21 `RF_*`), scale and pivot hand-corrected by
  Carlos, colliders on all of them, foliage-card backface bug fixed. Carlos: *"our assets are
  ready."* Don't re-open asset prep.
- **All 190 prefabs measured** — visual bounds, footprint, burial depth, visible height, blocking
  radius, triangle count. In `Tools/vegetation/veg_sizes.csv`.
- **The distribution plan** — `Docs/mrm70-biome-distribution-measured.md`, generated from that CSV.
- **`VegetationGallery` scene** has a lighting/fog/CRT preview rig for inspecting trees under real
  conditions.

## What is blocked, and on what

| # | Blocker | Owner |
|---|---|---|
| 1 | **The biome map** — which part of the island is which biome (§6.3 offers three formats) | **Carlos** |
| 2 | Nine landmark positions + player spawn, re-anchored to the −512…+512 frame | **Carlos** |
| 3 | **Mountain terrain** — max height is ~60 m and the tallest rock we own is 4.03 m visible, so it's currently a hill with knee-high rocks. Raising it in Gaia's Stamper is probably cheaper than any asset fix | **Carlos** |
| 4 | Terrain layers + grass detail pass rebuilt | Claude, can start now |
| 5 | 8 trip-wall prop colliders (Appendix A) | Claude, before scattering |

## Key numbers to carry into the session

| | |
|---|---|
| Player capsule | h = 1.8 m, r = 0.4 m |
| Sea level (Crest) | Y = 8 |
| Land above sea | 64.2 ha |
| Slope | 25% under 9°, 40% at 9-18°, **33% over 18°**, steepest ~70° |
| Spacing formula | `spacing = k × footprint`, footprint = max(bounds X, Z) |
| Tiers | D 0.6-0.9 · M 1.0-1.6 · S 2.0-3.5 · A 4.5-8.0 · H hand-placed |
| Count conversion | `instances/ha ≈ 8000 / spacing²` |
| Planned total | ~63,400 instances across ~57.5 ha |

## Gaia fields (read from the installed `GaiaCore` assembly, not docs)

On `Gaia.SpawnRule`: `m_locationIncrementMin` / `m_locationIncrementMax` (spacing),
`m_noiseMask` (`None`/`Perlin`/`Billow`/`Ridged`), `m_noiseZoom`, `m_noiseStrength`,
`m_noiseMaskSeed` (**unique per species**), `m_noiseMaskOctaves`, `m_minRequiredFitness`,
`m_terrainDetailDensity`.

On `Gaia.ImageMask` in `m_imageMasks[]`: `m_slopeMin` / `m_slopeMax`,
`m_heightMaskType = RelativeToSeaLevel`, `m_seaLevelRelativeHeightMin` / `Max`, `m_strength`,
`m_imageMaskLocation = SpawnRule`.

On `Gaia.ResourceProtoTree`: `m_spawnScale = FitnessRandomized`, `m_minHeight`, `m_maxHeight`,
`m_heightRandomPercentage` (use ±15-20%).

## Two known Unity traps

- **Terrain trees silently reject MeshColliders.** Anything with a value in the "Blocks" column of
  §7 must spawn as a **game object**, not a terrain tree.
- **Terrain trees ignore the prefab's root transform.** Check `localRotation == identity` before
  trusting any species placed as a terrain tree.
