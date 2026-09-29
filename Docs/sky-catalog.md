# Sky catalog: every sky the game will use, by act

**Created 2026-09-28.** Carlos reviewed ~450 candidate skies in the Playground project's Skybox Reviewer, picked these,
and fixed which one belongs to which moment of the game. This file is the record. **Nothing here is wired into a scene,
`TimeManager` or `SkyboxSwitcher` yet**: that is the MRM-86 lighting rework (`lighting.md` §7). The old
`Assets/_Project/Art/Environment/Skyboxes/` folder (6 skies, used by today's four `TimeManager` presets) is untouched.

## Where things are

```
Assets/_Project/Art/Environment/Skies/
  01_Story/     7 materials, one per story part
  02_Legion/    20 materials, one per act of each wave
  Textures/     20 sky textures, one per UNIQUE sky (several acts share one)
```

The number prefixes make the Project window show Story first, then Legion (alphabetical would put Legion first).
Materials are tiny; only `Textures/` costs disk and build size. That is why an act that reuses a sky gets its own
**material** (so it can be tuned or swapped alone) but points at the same **texture**.

**Naming:** `M_Sky_Story_<n>_<Part>`, `M_Sky_Legion_W<wave>_A<act>_<Beginning|Conflict|Ending>`, `T_Sky_<Name>`.
Every material is `Skybox/Cubemap`, Tint 0.5, Exposure 1, Rotation 0 (same as the old skies).

**Legion act meaning:** A1 *Beginning* (calm before), A2 *Conflict* (the fight), A3 *Ending* (get to safety, the zone is
closing in). Wave 7 is the finale and has only two acts (no Ending).

## Story mode (in play order)

| # | Material | What happens | Texture | Source sky |
|---|---|---|---|---|
| 1 | `M_Sky_Story_1_Campsite` | Waking up, campsite | `T_Sky_Overcast3High` | AllSky `AllSky_Overcast3_High` |
| 2 | `M_Sky_Story_2_MovingToGlade` | Moving to the glade | `T_Sky_CedarBridge` | AllSky `Cedar Bridge` (HDRI Haven) |
| 3 | `M_Sky_Story_3_Glade` | The glade | `T_Sky_Afternoon018` | Ether `afternoon_018` |
| 4 | `M_Sky_Story_4_MovingToCabin` | Moving to the cabin | `T_Sky_Dawn016` | Ether `dawn_016` |
| 5 | `M_Sky_Story_5_MineEntrance` | Entrance to the mine | `T_Sky_DeepDusk` | AllSky `Deep Dusk` |
| 6 | `M_Sky_Story_6_Fountain` | Fountain | `T_Sky_Dawn021Edit` | Ether `dawn_021_edit` |
| 7 | `M_Sky_Story_7_Apocalypse` | Apocalypsis | `T_Sky_FantasyFire` | AllSky `FantasySky_Fire` |

## Legion mode (waves 1 to 7)

| Act | Material | Carlos's note | Texture |
|---|---|---|---|
| W1 A1 | `M_Sky_Legion_W1_A1_Beginning` | Slight cloudy white sky | `T_Sky_DaySunHighClouds` |
| W1 A2 | `M_Sky_Legion_W1_A2_Conflict` | Heavy fog, cloudy, gray | `T_Sky_Overcast3High` |
| W1 A3 | `M_Sky_Legion_W1_A3_Ending` | Dark, cloudy, rain, flies | `T_Sky_FantasyHeavy1` |
| W2 A1 | `M_Sky_Legion_W2_A1_Beginning` | Dark, cloudy, rain | `T_Sky_FantasyHeavy1` |
| W2 A2 | `M_Sky_Legion_W2_A2_Conflict` | Green, cloudy, heavy flies | `T_Sky_MammatusDarkGreen` |
| W2 A3 | `M_Sky_Legion_W2_A3_Ending` | Clear day, gray, windy, birds | `T_Sky_Afternoon011` |
| W3 A1 | `M_Sky_Legion_W3_A1_Beginning` | Clear day, gray, windy | `T_Sky_Afternoon011` |
| W3 A2 | `M_Sky_Legion_W3_A2_Conflict` | Clear blue sky, windy, birds | `T_Sky_SunlessBlue01` |
| W3 A3 | `M_Sky_Legion_W3_A3_Ending` | Afternoon orange, sandstorm | `T_Sky_Dawn016` |
| W4 A1 | `M_Sky_Legion_W4_A1_Beginning` | Afternoon orange | `T_Sky_Dawn016` |
| W4 A2 | `M_Sky_Legion_W4_A2_Conflict` | Cloudy, orange, sandstorm | `T_Sky_FantasyHeavy1` |
| W4 A3 | `M_Sky_Legion_W4_A3_Ending` | Cloudy, dark, darkness vacuum | `T_Sky_Dawn021NoMoon` |
| W5 A1 | `M_Sky_Legion_W5_A1_Beginning` | Clear night, ambient sounds, static moon | `T_Sky_Dawn021Edit` |
| W5 A2 | `M_Sky_Legion_W5_A2_Conflict` | Denser darker night, darkness vacuum, no sounds | `T_Sky_PureBlack` |
| W5 A3 | `M_Sky_Legion_W5_A3_Ending` | Variant night, smoke, ash storm | `T_Sky_Dusk006Edit` |
| W6 A1 | `M_Sky_Legion_W6_A1_Beginning` | Variant night, smoke, ashes | `T_Sky_NightMoonlessFire` |
| W6 A2 | `M_Sky_Legion_W6_A2_Conflict` | Fairy night, forest fire | `T_Sky_HorizonOvercastEdit` |
| W6 A3 | `M_Sky_Legion_W6_A3_Ending` | Red sky, cloudy, blood storm | `T_Sky_NightSkyglowBloodstorm` |
| W7 A1 | `M_Sky_Legion_W7_A1_Beginning` | Red sky, desolated, windy | `T_Sky_Dusk028Edit` |
| W7 A2 | `M_Sky_Legion_W7_A2_Conflict` | Apocalypse sky, blood storm | `T_Sky_NebulaRedThinClouds` |

Wave 2's Beginning and Wave 1's Ending are the same sky on purpose (the "dark, cloudy, rain" carries over), and so on
across every wave boundary. Reused skies: `Overcast3High` (Story 1, W1 A2), `FantasyHeavy1` (W1 A3, W2 A1, W4 A2),
`Afternoon011` (W2 A3, W3 A1), `Dawn016` (Story 4, W3 A3, W4 A1), `Dawn021Edit` (Story 6, W5 A1).

## The 20 textures

| Texture | Kind | Face size | Origin |
|---|---|---|---|
| `T_Sky_Overcast3High` | PNG, equirect | 2048 | AllSky pack, `Overcast/Overcast High` |
| `T_Sky_CedarBridge` | PNG, equirect | 2048 | AllSky pack, HDRI Haven `cedar_bridge_16k` (61 MB PNG) |
| `T_Sky_DeepDusk` | PNG, equirect | 2048 | AllSky pack, `Version 4/Deep Dusk` (51 MB PNG) |
| `T_Sky_FantasyFire` | PNG, equirect | 2048 | AllSky pack. Same image as the old `T_Sky_FantasySkyFire`, copied so this folder is self-contained |
| `T_Sky_FantasyHeavy1` | PNG, equirect | 2048 | AllSky pack, `Fantasy Heavy` |
| `T_Sky_DaySunHighClouds` | PNG, equirect | 2048 | AllSky pack, `Day Sun High CloudsLayer` (variant 2, the approved one) |
| `T_Sky_SunlessBlue01` | PNG, equirect | 2048 | AllSky pack, `Sunless_BlueSky_01_flat` |
| `T_Sky_Afternoon018`, `Afternoon011`, `Dawn016`, `Dawn021Edit` | PNG, 6-face strip | 2048 | Ether Skybox Collection (AST-164) |
| `T_Sky_Dawn021NoMoon`, `Dusk006Edit`, `Dusk028Edit` | PNG, 6-face strip | 2048 | Ether, custom edits from ChatGPT images |
| `T_Sky_MammatusDarkGreen`, `NightMoonlessFire` | PNG, 6-face strip | 2048 | AllSky Mammatus / custom, from ChatGPT images |
| `T_Sky_HorizonOvercastEdit`, `NightSkyglowBloodstorm` | PNG, 6-face strip | **1024** | Custom, from ChatGPT images (source faces were 1024) |
| `T_Sky_NebulaRedThinClouds` | PNG, 6-face strip | 2048 | Custom. **Orientation baked in**, see below |
| `T_Sky_PureBlack` | `.cubemap` (13 KB) | 16 | Generated, every face 0,0,0 |

All import as `Cube`, Clamp, no mipmaps, `Standalone` compression BC7 (the 7 AllSky PNGs keep the settings Carlos
reviewed them with).

## How the Ether / custom skies were brought over (2026-09-28)

They were native `.cubemap` assets in the Playground project. This project serialises assets as **text**, so a native
`.cubemap` becomes hex text (50 to 67 MB each, 700 MB for the set) and a `.cubemap` is not LFS-tracked. So each was
re-rendered to six faces and stored as a **6-face strip PNG** (12 to 17 MB, LFS like every PNG). Unity imports a 6:1 PNG as
a cubemap. **Two traps for anyone repeating this:**

- The importer **flips every face vertically**, so the strip must be written with each face flipped. Verified pixel-exact
  (difference 0.0000 on all six faces at 2048).
- Faces go left to right `+X, -X, +Y, -Y, +Z, -Z`.

The render used Tint 0.5038, which makes the skybox shader's `tint * colorSpaceDouble` exactly 1, so the strip holds the
sky's data unchanged; the material's Tint 0.5 then gives the same look Carlos reviewed.

## Orientation ("origin point")

Carlos tuned one sky's orientation in the Playground reviewer: **Nebula Red Thin Clouds, yaw 0, pitch -90, roll 56**
(the saved values are in the Playground project, `AST-054/Demos/SkyboxOrientation.json`). The stock `Skybox/Cubemap`
shader only turns around Y, so pitch and roll are **baked into `T_Sky_NebulaRedThinClouds`** (rendered through the same
shader Carlos previewed with; matches within 0.002 mean difference). Every other sky is at its authored orientation. If
Carlos changes another sky's origin later, re-bake it the same way, or use the `_Rotation` property for yaw only.

## Size and open decisions

- `Textures/` is **237 MB on disk** (mostly PNG). In a build the textures are GPU-compressed: roughly **365 MB** for the
  20 skies (ten 2048 BC7 strips at 25 MB, six 2048 DXT1 equirects and Deep Dusk at 13 to 25 MB, two 1024 at 6 MB). Zipped
  it is smaller but BC7 does not zip well. Against the **1 GB itch.io ceiling** this is a large share, so it is worth
  deciding before launch whether the mostly-black night skies can drop to 1024 (saves about 19 MB each).
- Every new PNG goes through Git LFS (`.gitattributes`). About 237 MB of new LFS content
  (`Textures/`), so check the LFS quota before pushing.
- **Wave 7 has no Ending act.** If the finale should have one, it needs a sky.
- Reused skies mean `TimeManager` presets can point several acts at one texture without extra cost.
- Not wired into anything: `TimeManager` presets, `SkyboxSwitcher` lists, sun/fog values per act. That is MRM-86.
