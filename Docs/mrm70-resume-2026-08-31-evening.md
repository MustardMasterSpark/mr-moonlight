# MRM-70 resume — 2026-08-31, evening handoff

Read this first if starting a fresh session. `Docs/mrm70-resume-2026-08-31.md` (morning-through-day)
has the full blow-by-blow — bugs found, debugging steps, exact numbers at each stage — kept for
history but you shouldn't need to read it top to bottom. This file is the current-state summary.

## Where things actually stand right now

**Terrain layers:** all 10 biome layers painted (Forest, AutumnForest, EerieForest, HereticForest,
Beach, Mountain, FlakTower, Fountain, Glade, Path) plus an 11th, `TL_Seafloor_NoSpawn`, covering
everything from the seafloor up through +3m above sea level (sea level = live Water GameObject Y,
currently 8). Beach itself now only covers **+3m to +5m above sea level** (3.37% of the terrain) —
a deliberately narrow shore ring, not the ~46% underwater sprawl it started as. **No Spawner is
wired to `TL_Seafloor_NoSpawn` and none should ever be** — that's what keeps it permanently
vegetation-free, not a height check to remember.

**Vegetation:** `Vegetation Spawners` GameObject in `Island.unity`, one `Gaia.Spawner` per biome
(9 total — Forest, AutumnForest, EerieForest, HereticForest, Beach, Mountain, FlakTower, Fountain,
Glade). **8 of them are spawned; Glade is deliberately not** (its rows are pure grass, no collision
— wrong category for GameObject-type spawning, needs the TerrainDetail path instead, not built
yet). Current live count: **21,683 real GameObject instances**, all correctly masked to their
biome's painted layer, all upright (tree-vs-rock slope-alignment fixed), density cut twice
(10% then 15%, compounding, per-spawner values documented in the `mrm70-generate-new-gaia-trigger`
memory).

**Flora:** built (`Assets/_Project/Code/Editor/FloraInstanceRendererPass.cs`,
`Tools > MrMoonlight > Vegetation > Add Flora Instance Renderers to Spawned Vegetation`), proven to
work mechanically, but **currently disabled** (`Flora Scene Settings.EnableRendering = false`) —
it has a real, unsolved rendering bug (phantom tree shapes floating over water; confirmed via a
clean on/off test, root cause not found — see `mrm70-flora-rendering-bug` memory). Not deleted;
Carlos wants to come back to it once it's fixed. **The standard regenerate algorithm does NOT run
the Flora step right now** — don't add it back without being told to.

## The standing procedure — "generate new Gaia"

When Carlos says "generate new Gaia" / "regenerate" / "run the algorithm": clean up the spawn
container, apply any new instructions he gave that message (density/spacing/etc — if none, just
re-run with rules as they stand), re-verify every rule's mask is still active (Play Mode can
silently break these, see below), spawn all 8 biomes, report the totals. Full detail, including
exact API gotchas, in the `mrm70-generate-new-gaia-trigger` memory — read it before running this by
hand, it'll save re-discovering the same two Gaia API bugs.

## Bugs found and fixed today (memory has full detail on each)

1. `spawner.Spawn(false)` silently spawns nothing from script — use `spawner.AreaSpawn(...)` +
   manual coroutine drain instead. (`mrm70_gaia_spawn_attempt1_failed`)
2. `Gaia.ImageMask`'s texture-mask resolves by matching `ResourceProtoTexture.m_texture` (a
   Texture2D) against each layer's `diffuseTexture` — **not** via `CurrentLayer`, despite that
   looking like the obvious field. (same memory)
3. Play Mode enter/exit can silently flip every rule's `ImageMask.m_active` to `false` — always
   re-check before spawning after any Play Mode test. (same memory)
4. `SpawnRule.m_maxInstances` does **not** enforce a cap when driving `AreaSpawn()` from script —
   don't rely on it; widen spacing instead. (`mrm70_vegetation_flora_pipeline`)
5. Trees defaulted to `rotateToSlope = true` (tilts with terrain slope) — wrong for trees, correct
   for rocks. Fixed by name-splitting all 78 species. (`mrm70_tree_rotate_to_slope_fix`)
6. Flora's GPU-instanced rendering shows phantom floating tree shapes — unresolved, Flora is off.
   (`mrm70_flora_rendering_bug`)

## What's next / not yet done

- Carlos is currently exploring the scene himself and will likely come back with biome tuning
  requests (species mix, density per-biome, spacing) — no action pending, this is a natural
  stopping point.
- TerrainDetail routing for decorative/no-collision species (Glade's grass, and the shrub/
  ground-cover strata of every other biome once those get built) — still not built. See
  `mrm70_vegetation_flora_pipeline` for the plan.
- Flora rendering bug — unresolved, needs real debugging time when it's a priority again.
- Only Stratum A (canopy/primary tier) has been spawned per biome so far — sub-canopy, shrub, and
  ground-cover strata from §7 of the biome doc are not yet built as spawn rules.

## Housekeeping

- Three diagnostic screenshots got saved to `Assets/Screenshots/` tonight during debugging
  (`screenshot-20260831-*.png`) — not meaningful to keep, safe to delete before committing if
  Carlos doesn't want them in the repo.
- `Assets/_Project/Code/Editor/MrMoonlight.Editor.asmdef` now references `GaiaCore` and `MA.Flora`
  (needed for `FloraInstanceRendererPass.cs` to compile) — intentional, not a stray change.
- Project rules unchanged: never commit or push (Carlos uses GitHub Desktop), ask permission before
  Unity scene/inspector work if not already clearly requested, verify + document what changed.
