---

## 6. Terrain facts and biome boundaries — **the open blocker**

Everything in §7 is "what to place and how far apart". It cannot run until the island is divided
into biomes. That division **does not exist yet** and is Carlos's to define.

### 6.1 The terrain, as actually measured

Read off the live `Island.unity` scene, not from any document:

| Property | Value |
|---|---|
| Terrain object | `Terrain_0_0-20260829 - 035828` |
| Terrain data | `Assets/Gaia User Data/Sessions/GS-20260829 - 011148/Terrain Data/Terrain_0_0-20260829 - 035828.asset` |
| Size | **1024 × 1024 m**, Y range configured to 1024 m |
| World origin | (−512, 0, −512) — so world X and Z both run **−512 … +512** |
| Actual height used | 0 – **68.4 m** |
| Sea level (Crest) | **Y = 8** |
| Land above sea | **64.2 ha** (61.2% of the terrain) |
| Terrain layers | **0** |
| Detail prototypes | **0** |
| Tree prototypes / instances | **0 / 0** |
| Gaia spawner in scene | `World Designer`, 7 rules |

> **Two traps for anyone picking this up.**
>
> `Assets/_Project/Art/Environment/Terrain/Island_TerrainData.asset` is **not the live terrain.**
> It is 4000 × 4000 m and essentially flat (max height 20 m, 100% of it under 9°). The scene uses
> the Gaia session asset above. Editing the `_Project` one changes nothing you can see.
>
> The old `Island_Original_TerrainData_Backup.asset` is 4103 × 7085 m. **Every biome coordinate in
> `mrm70-biome-vegetation-strategy.md` §3 refers to that terrain** and is off by roughly 4-7× in
> both axes. Those numbers cannot be scaled across; the shape is different, not just the size.

### 6.2 What this changed in this document

The first draft of §7 used the old survey's biome areas, which summed to ~116 ha — **more land than
the island has.** Absolute instance counts were roughly 2× too high. Areas have been re-anchored to
the real 64.2 ha:

| Biome | Assumed area | Note |
|---|---:|---|
| Autumn Forest | 18.0 ha | Largest; contains the Glade |
| Forest | 16.0 ha | |
| Eerie Forest | 5.0 ha | Contains the Fountain |
| Beach | 5.0 ha | Perimeter band |
| Mountain | 5.0 ha | |
| Heretic Forest | 4.0 ha | |
| Flak Tower | 3.5 ha | |
| Fountain | 1.0 ha | Inside Eerie, not additional |
| Glade | 0.5 ha | Inside Autumn, not additional |
| **Assigned** | **~57.5 ha** | leaves ~6.7 ha of transition/hinterland |

**These are placeholders chosen to be plausible and to sum correctly — they are not a design.**
The instances/ha and triangles/ha columns are the real outputs and are unaffected. Replace the areas
once the map exists and the counts follow automatically.

### 6.3 What is needed before Gaia can run

**Carlos to define — the actual biome map.** Any of these forms works:

1. **Painted terrain layers** — the strategy doc's §2 approach, and the one the whole plan assumes:
   one `TerrainLayer` per biome, painted onto the splatmap, and every spawn rule masked to its
   layer. Gaia reads this natively.
2. **Polygon regions in world coordinates** — a list of X/Z polygons on the −512…+512 frame.
   Gaia `ImageMask` supports `PolyMask`.
3. **A biome map image** — a colour-coded PNG at terrain resolution, one colour per biome, applied
   as an `ImageMask` with `m_imageMaskSpace = World`. Probably the fastest route given
   `Docs/Design/Island-Terrain-Reference/Map/biomes.png` already exists in this style — it just has
   to be redrawn against the current island shape.

Whichever form, these must also be fixed before or alongside it:

- **The nine landmark positions** in the new coordinate frame — Chapel, Well, Mine Entrance, Mine
  Exit, Flak Tower, Cabin, Camp, Dock, Glade. The old ones are on the dead terrain.
- **The player spawn point.** The old (883, 80.6, 4489) is outside this terrain entirely.
- **Terrain layers must exist at all.** Currently zero. Terrain layers are the biome masks, so
  there is nothing for a spawn rule to key off. `Island_Original_TerrainData_Backup.asset` holds
  the previous 8-layer, 27-detail-prototype configuration to copy from — see §4.

Until the biome map exists, the only work that can proceed is **rebuilding the terrain layers and
the grass detail pass** (§4), which is biome-independent groundwork.

