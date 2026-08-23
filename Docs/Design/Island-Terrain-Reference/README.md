# Aanniarvik Island — Terrain Reference

Source material Carlos produced for the MRM-58 programmatic terrain block-out (2026-08-23).
Background reference only — see `Docs/changelog.md`'s MRM-58 entry for what was actually
built from it and the exact numbers used. If this README and the changelog disagree, the
changelog wins (it reflects what's in the scene now).

## Map/

| File | What it is |
|---|---|
| `AANNIARVIK-heightmap-source.png` | The island map. Blue = water (including bodies of water inside the island). Grayscale = ground height. 1490×2258px. North is up. |
| `AANNIARVIK-height-scale-legend.png` | Grayscale → elevation legend: white = 0m, black = 170m, linear. |
| `AANNIARVIK-scale-calibration-lines.png` | Two reference lines Carlos measured on the real island this was modeled from — yellow = 1.72km, red = 1.82km — used to derive real-world meters-per-pixel. |
| `AANNIARVIK-locations.png` | Colored boxes marking the 8 script locations in visit order: campsite (green) → glade (blue) → Vernon's cabin (brown) → Flak Tower (red) → mine entrance (purple) → mine exit (pink) → well (orange) → chapel (black). |
| `AANNIARVIK-demo-gameplay-area.png` | Red hand-drawn perimeter marking the area meant to be densely populated with trees/props for the explorable demo slice. |

## Vibe/

Mood/lighting reference for the forest, water, and night sky look — not geometry data.
`Vibe_SnowyRockForest`, `Vibe_WaterfallStream`, `Vibe_DuskMoonrise`, `Vibe_NightSkyMoon`,
`Vibe_FoggyForestPath`.

## Provenance

Moved here 2026-08-23 from a loose `Island Terrain Design/` folder at the repo root (not a
Unity project convention) as part of the MRM-58 terrain pass. Kept as design reference under
`Docs/Design/`, same pattern as the screenplay/pitch/style docs, rather than imported into
`Assets/` — these are inputs to a one-time editor bake, not runtime textures.
