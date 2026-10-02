# Fog experiment: Volumetric Fog & Mist 2 (AST-282) next to HAZE (MRM-85 / MRM-86)

Started 2026-09-30, branch `mrm-86`. Goal: which fog is cheaper (and looks right) in the Legion fight, and can the new
asset replace HAZE. **Parked 2026-10-01** (Carlos: "the new asset is kind of uncontrollable right now"; the quick demo
task comes first). Resume with `Docs/mrm85-fog-vf2-continue-prompt.txt`.

## 1. State right now
- **HAZE scenes untouched:** `07 LightingTestScene` (HAZE, `WeatherSystem`, `WeatherProfiles.asset`), `06 Island_Legion`, `02 Island`.
- **VF2 scene:** `08 LightingTestScene Fog Experiment` (copy of 07, no HAZE data anywhere): `Vf2WeatherSystem`, `WeatherProfiles_VF2.asset`.
- **No build contains VF2.** Scenes 07 and 08 are not both in Build Settings (only 03, 02, 06 are). The HAZE vs VF2 cost is **not measured** (H14 in `performance-sessions.md`).
- The VF2 look is **not tuned**: the 27 VF2 profiles were seeded from the HAZE area box (density = 0.6 x weight x density, capped 1.5; Brightness = 1/sun for profiles with sun > 1) and Carlos only began tuning the first 7.
- Scene numbering and the deleted test scenes: `scene-numbering.md`.

## 2. What was imported
`Assets/ThirdParty/AST-282/VolumetricFog2/` (git-ignored like all ThirdParty): Scripts, Editor, Resources (shaders, noise
textures, prefabs), README and the 19 Presets (fog profiles). No demo scenes (2.7 MB). Source: `01_DOWNLOAD\AST-282.zip`
v31.4.2 (bundle, URP package, unpacked in `02_extracted\AST-282`). Scripts compile into the URP assembly through
`VolumetricFogURP.asmref`; `MrMoonlight.Runtime` already references it. Windows `tar` is needed to unpack
(`C:\Windows\System32\tar.exe`; the Git Bash one fails on `C:` paths). A fresh clone without the folder will not compile the
VF2 code (`Vf2FogAdapter`, `Vf2WeatherSystem`, `SceneEffectsToggle` only by type name), same rule as HAZE.

## 3. The VF2 scene (08) in detail
- **HAZE removed:** the density box deleted; the global volume (renamed "Global Post Volume (CRT)") uses
  `Settings/FogExperiment/VP_LightingTest_NoHaze.asset` = the scene's own fog profile minus the HAZE component.
  **That profile also carries the CRT effect, so the volume must stay.**
- **Own renderer:** `Settings/FogExperiment/PC_Renderer_VF2.asset` = PC_Renderer minus `HazeRendererFeature` plus
  `VolumetricFogRenderFeature`, renderer index 1 of `PC_RPAsset`; the PlayerCamera in scene 08 uses index 1 (scene override on
  `UniversalAdditionalCameraData.m_RendererIndex`). Scenes using index 0 are unchanged.
- **Fog:** `VF2 Island Fog`, box centre (0, 55, 0), size 1400 x 130 x 1400 (covers x/z -700..700 and y -10..120: the island is
  -512..512, sea level y = 8, terrain 0..70, tallest tree ~98). Profile `FogExperiment/VF2_Legion_Fog.asset`. Native lights
  enabled. `Volumetric Fog Manager`: sun = SUN, downscaling 2, blur passes 1, Include Transparent = Water layer.
- **Weather:** `Vf2WeatherSystem` on `Weather`, library `Data/Weather/WeatherProfiles_VF2.asset`; `SkyProximityCircuit` and the
  tuning panel point at it. Save buttons / the P panel write only into that library.
- **Missing in 08:** the `Scene Effects Toggle` prefab (F6 fog / F7 CRT); it was added to 07 on 2026-10-01 but not to 08. The
  toggle code was extended to hide the VF2 mesh; when it is added to 08, set its Target Volume to "Global Post Volume (CRT)".

## 4. Weather profiles: two separate mechanisms
See `weather-profiles.md` (session 2026-09-30 evening) for the class layout. In short: `WeatherSystemBase` +
`WeatherSystem` (HAZE) / `Vf2WeatherSystem` (VF2); `WeatherProfileLibrary` / `Vf2WeatherProfileLibrary`; `IFogAdapter` with
`HazeFogAdapter` / `Vf2FogAdapter`. The VF2 profile's `Fog` = `Vf2FogSettings` (Enabled + ~45 inputs, grouped by headers, not
by sub-foldouts). The earlier `FogBackend` switch and the `FogSettings.Vf2` field were removed.

## 5. What the VF2 inputs do (the ones that matter)
- **Brightness / sun:** the fog is lit by the scene SUN at its full intensity (profile Day Night Cycle on, manager.sun = SUN).
  A bright sun (several story profiles are 2-5.6) washes the fog to white. Brightness ~ 1 / sun intensity.
- **Motion:** Wind Direction (x, y, z) and Turbulence are applied as value x time: both 0 = a completely still fog.
- **Distance / Max Distance:** fog starts / stops at that camera distance (Distance 3 m by default keeps the player's face clear).
- **Native Lights Multiplier:** how much every URP light (lamps, fires, flashlight, `PlayerAmbientLight`) lights the fog.
  **0 = no fog at all** (verified, even at density 3). Large values blow the fog out to white.
- Everything else (noise, detail noise, height, border, shadows, quality, distant fog) is exposed with tooltips.

## 6. Findings and traps (so they are not re-discovered)
1. **The "terrible lines" (full-screen diagonal weave) = Jittering** (default 0.5). Verified one value at a time in Play Mode:
   Jittering 0 removes it with the fog still visible; Dithering 0 alone does not; Dithering 0.5 with Jittering 0 is clean.
   Not the CRT, not the box size, not Include Transparent. Banding with Jittering 0 not checked: raise the Raymarch Quality if it shows.
   **Not yet applied to the saved profiles/defaults** (they still say 0.5).
2. **Disabling the `VolumetricFog` component does not hide the fog**: it is a mesh drawn by the render feature and keeps its last
   material values. Fixed in `Vf2FogAdapter` ("Enabled" hides the MeshRenderer) and in `SceneEffectsToggle`.
3. Dense fog (density ~6-9, or a bright sun) saturates to opaque; the dither pattern shows there. Seeded densities were capped at 1.5.
4. Fog box too small: the first box was a copy of HAZE's and left the island's west 170 m and everything above y 173 / below -7 unfogged.
5. Water is not fogged unless the manager's Include Transparent has the Water layer (set); the effect is small.
6. A ScriptableObject class in a file with another name gets an asset with `m_Script: {fileID: 0}` (the VF2 library).
7. Editor screenshots can show a stale frame right after a change (editor ~10-100 fps): wait one more frame before concluding.
   (An earlier claim in this session, "Native Lights Multiplier 0 gives clean fog", was wrong for that reason.)
8. `manage_camera screenshot` with `view_position`/`view_rotation` renders from any point; the player camera cannot be aimed by script.
9. The Legion scenes have no weather system and no circuit: the weather mechanism exists only in 07 (HAZE) and 08 (VF2).

## 7. Open items (in the order to do them)
1. Set Jittering 0 (and the Brightness/Native Lights values Carlos prefers) on all 27 VF2 profiles and in `Vf2FogSettings` defaults.
2. Decide what lights the fog: the Native Lights Multiplier 0 = no fog finding means the fog needs the native path; check low
   values (0.02-0.2) with Jittering 0, or VF2's own point lights, or accept sun-only lighting with another set of values.
3. Add the toggle prefab to scene 08.
4. Build 45 with 07 (HAZE) and 08 (VF2): the H14 A/B/C runs (`performance-sessions.md`).
5. Match the look (HAZE global density, height fog and noise were not mapped) and tune the 27 profiles.
6. If VF2 wins: replace HAZE in Island / Island_Legion (not decided; HAZE stays everywhere until then).
