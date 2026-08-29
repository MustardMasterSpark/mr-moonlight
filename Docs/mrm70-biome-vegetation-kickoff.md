# MRM-70 — Biome painting + vegetation spawning kickoff, round 2

**Written 2026-08-29, end of the terrain-settling session.** This is the starting point for the
next session — read this, then wait for Carlos's go-ahead before touching anything (standing rule
across this project, see `Claude Code Context MDs/kickstart.md` §B.3 and every prior kickoff doc).

## What changed since the last vegetation pass

The terrain vegetation was originally spawned once already (2026-08-25, 17,350 trees, full writeup
in `Docs/mrm70-biome-vegetation-strategy.md`). **That terrain no longer exists.** It was fully
regenerated twice since: once via Gaia Stamps (2026-08-28), then again via Gaia **World Designer**
(2026-08-29), which is what actually shipped — Carlos hand-shaped it live in the Inspector and
explicitly settled on the result this session. Read `Docs/mrm70-gaia-kickoff.md`'s **"TERRAIN
SETTLED, 2026-08-29"** section first for the exact final state. Short version: new footprint,
centered on world origin, roughly 1024×1024m, sea level (Crest water) at world Y=8, **zero terrain
layers, zero tree prototypes, zero detail prototypes painted or placed.** This is a real from-zero
restart on vegetation, not a continuation.

## Read first, in this order

1. `Docs/mrm70-gaia-kickoff.md` — "TERRAIN SETTLED" section, for the current terrain's exact state.
2. `Docs/mrm71-crest-water-kickoff.md` — water is in and at its current sea level (Y=8, may still
   move if the terrain gets further deformation).
3. `Docs/mrm70-biome-vegetation-strategy.md` — **read its 2026-08-29 stale-content banner at the
   top before trusting anything in the body.** §3 (world coordinates) is invalid against the new
   terrain. §2 (terrain layers as biome masks), §4 (per-biome content plan/species palette), and §5
   (technical fixes — no LODGroups, GFF grass can't be a detail mesh, MeshCollider vs
   CapsuleCollider) are still conceptually good and worth reusing.
4. `Docs/mrm70-vegetation-spawner-facts.md` (if referenced from memory) for the old Vegetation
   Spawner tool's real behavior — no species cap, hard-cutoff layer masks, per-species-only Poisson
   spacing.

## What's actually gone, confirmed via `git status`

`Assets/_Project/Code/Editor/BiomeGrassSetup.cs` and `BiomeVegetationSetup.cs` — the custom tools
`mrm70-biome-vegetation-strategy.md` §6b describes building (menu `Tools/Mr. Moonlight/Vegetation/`)
— **both show deleted in git status, not just uncommitted.** Confirm with Carlos whether that's
intentional before assuming they should be rebuilt.

## Two real paths for vegetation this round — pick one before starting

**Path A — rebuild the custom tools.** Known quantity: exact same approach that got 17,350 trees
placed cleanly last time, strategy doc §2/§4/§5 already describe the plan. Costs: rebuilding
`BiomeGrassSetup.cs`/`BiomeVegetationSetup.cs` from scratch (or from git history if Carlos wants
them recovered rather than rewritten), and the same hard-cutoff/overlap limitations from
`mrm70-vegetation-spawner-facts.md` apply again.

**Path B — use Gaia's own native Spawner system.** Researched this session, not yet proven out
hands-on. Gaia has a full rule-based scatter system (`Spawner`/`SpawnRule`, the same machinery
World Designer's "World Detail" rules use) that can paint `TerrainTexture`, spawn `TerrainTree`,
`TerrainDetail`, and arbitrary `GameObject` props all from one rule set, with fitness driven by
painted texture layer / slope / height / noise / a hand-drawn `PolyMask` region, and — the part
that's actually better than Path A — configurable rule-resolution modes (`Fittest`/
`WeightedFittest`/`Random`/`All`) that address the old tool's hard-cutoff-mask problem instead of
needing a manual post-pass. Gaia's **PolyMask** tool (`Window/Procedural Worlds/Gaia/Create
Polymask`) is the direct answer if biome regions get hand-drawn on the terrain rather than
inferred from coordinates. **Not confirmed:** whether Gaia's spawner has any cross-species
minimum-spacing guarantee — the old tool's actual overlap pain point might or might not be solved,
untested either way.

**Recommendation, not a decision:** given Gaia is already this project's terrain tool and Path B's
rule-competition model is a genuine improvement over a known limitation, it's worth at least a
small real test (one biome, a handful of species) before committing either way — but this is
Carlos's call, same as the terrain-tool switch was.

## Standing rules, same as every session

- Ask permission before Unity work, then do it, verify by reading state back, document
  (`CLAUDE.md` hard rule).
- No hardcoded values — `MoonlightTunables` (though `feedback_tunables_during_prototyping` memory
  says don't push this for vegetation/staging numbers until a real perf problem shows up).
- The 9 MRM-58 location blockouts (Camp/Dock/Glade/Cabin/Mine Entrance/Flak Tower/Mine Exit/Well/
  Chapel) are still sitting at positions from the *original* terrain, never repositioned across
  either regeneration. They'll need re-placement before biome regions can be anchored to them again
  — or before anything else assumes they're in the right place.
- Verification requires a real build — editor screenshots and `UnityStats` have both given false
  readings on this project (`Docs/pc-build-target.md` §6).

## Open questions for Carlos, carried over or new

1. Path A vs Path B above — worth a small test of B before committing?
2. Are `BiomeGrassSetup.cs`/`BiomeVegetationSetup.cs` intentionally deleted, or should they be
   recovered from git history?
3. The 9 location blockouts — reposition now against the new footprint, or wait until biome
   regions are drawn (since the biomes are partly built around where those locations sit)?
4. Everything in `mrm70-biome-vegetation-strategy.md` §7 (birch vs pine, tree LOD cull distance,
   vision-block mechanic layer, barrier polylines) is still open and unrelated to the terrain
   change — still needs answers whichever path gets picked.
