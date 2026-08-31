# Terrain detail (grass/flower) demo pass — 2026-08-31 late night

**Status: presentation-only placeholder, explicitly not the final approach.** Carlos: "this is just
for presentation for tomorrow's deliverable. We'll do this the right way later." This doc exists so
that "later" pass can cleanly start from zero instead of untangling this one.

## What this did

Registered all **72 prefabs** in `Assets/_Project/Art/VegetationPrefabs/GRASS PREFABS/` as Unity
`TerrainData.detailPrototypes` (mesh-mode, `usePrototypeMesh=true`, `renderMode=VertexLit`,
`useInstancing=true`) on the live terrain, then painted each one's density map directly via
`TerrainData.SetDetailLayer` (detailScatterMode was already `InstanceCountMode`, detailResolution
already 512 — neither was changed).

**This bypasses the Gaia `SpawnRule`/`ImageMask` system entirely** — unlike the 174 tree/rock
GameObject rules built earlier this session, these are NOT Gaia spawn rules and will NOT survive
"generate new Gaia" clean+respawn cycles (which only touch `Gaia Game Object Spawns`, never
`TerrainData.detailPrototypes`). That's fine for a one-off demo layer, but it's also why this is
explicitly flagged as not-the-final-architecture: `Docs/mrm70-resume-2026-08-31-night.md` §4
already established grass should eventually be real `SpawnerResourceType.TerrainDetail` Gaia
rules, for exactly this reason (rules persist through regen; hand/scripted paint doesn't).

**Placement logic** (per-cell, 512×512 grid over the 1024×1024m terrain, ~2m/cell):
- Beach and underwater (Seafloor/NoSpawn) alphamap weight > 0.15 → **excluded entirely**, per
  "spread across all biomes with the exception of the beach."
- All 72 layers: ~2% per-cell chance of a light base density (1-2 instances) everywhere on land
  outside Forest/Autumn — the "spread across all biomes" general scatter for Eerie, Heretic,
  Mountain, FlakTower, Fountain, Glade.
- **Forest + AutumnForest specifically** (alphamap weight > 0.15): boosted to ~22% per-cell chance,
  density 1-4 instead of 1-2, for all 72 layers — Carlos: "go crazy with the foliage spawn... try
  to cover all the terrain, like a lot." This is the biggest chunk of usable land (~43.3 of ~55.5
  ha non-beach land), so it dominates the total instance count.
- 23 layers whose prefab name contains flower/violet/aster/daisy/poppy/bells/cupcake: **additional**
  12% chance of extra density (2-4 instances) specifically where Fountain+Glade alphamap weight
  exceeds 0.15 — the "much more flowers... in the glade and the fountain" ask. Unchanged by the
  Forest/Autumn boost (additive, independent check).
- History: first attempt used 35%/60% base/flower chance and produced ~25 overlapping species per
  cell (3.5M non-zero cells, a solid carpet not a scatter) — repainted at 2%/12%, giving 208,992
  non-zero cells / 327,964 instance-units. Then Forest+Autumn were boosted to 22%/1-4 per Carlos's
  follow-up ask; final repaint: **1,768,029 non-zero cells / 4,384,290 total instance-units**, of
  which 1,715,275 cell-hits are inside the boosted Forest/Autumn zone. A scripted `manage_scene
  save` call disconnected mid-command right after this repaint (Unity was likely still processing
  the large `SetDetailLayer` batch) — Unity auto-reconnected, `detailPrototypes.Length` was
  verified intact (72) before retrying the save, which then succeeded.

## How to reverse this completely

Run this in the Unity Editor (via UnityMCP `execute_code` or a menu script) to return the terrain
to its pre-this-pass state (0 detail prototypes, 0 painted layers — confirmed that was the actual
starting state before this pass):

```csharp
var terrain = Terrain.activeTerrain;
var td = terrain.terrainData;
int detailRes = td.detailResolution;
int[,] zero = new int[detailRes, detailRes];
for (int layer = 0; layer < td.detailPrototypes.Length; layer++) {
    td.SetDetailLayer(0, 0, layer, zero);
}
td.detailPrototypes = new UnityEngine.DetailPrototype[0];
UnityEditor.EditorUtility.SetDirty(td);
```

Then save the scene. This does **not** touch anything else built this session (the 174 Gaia
tree/rock rules, terrain layers, or the biome masks) — it only clears the detail-prototype array
and its painted density maps.

## When doing this "the right way" later

Per `Docs/mrm70-resume-2026-08-31-night.md` §5.3 and `Docs/mrm70-unused-vegetation-inventory.md`
§7: register these same 72 prefabs as proper Gaia `SpawnerResourceType.TerrainDetail` spawn rules
(one per biome spawner, masked the same way the GameObject rules are), with density tuned
per-biome rather than a single flat presentation-pass number, and `detailObjectDistance` tuned
against a real build (memory: max ~250m, stock default 40).
