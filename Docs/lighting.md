# Lighting — the single reference

**Created 2026-09-19 (MRM-44).** Everything that is an actual light in Unity, or decides how light looks,
in one place: the sun and day/night, fog, the sky, the player's lights, the hands' lighting, enemy lamps,
flares, the render-pipeline settings and every lighting tunable. **When you change anything light-related,
update this file in the same session** (and add a row to "Change history" at the bottom).

This file is the map and the index of facts. Deep dives stay in their own docs and are linked, not copied:

| Deep dive | What it covers |
|---|---|
| `Docs/viewmodel-light-layers.md` | The hands' lighting in full: `ViewModel` rendering layer, the hands' directional light, world lights on the hands, chrome-material fix, every trap found |
| `Docs/pc-build-target.md` §HAZE | HAZE fog parameters and the post-processing trap |
| `Docs/debug-tools.md` | F6 fog, F7 CRT, F8 time-of-day cheats |
| `Docs/performance-sessions.md` | Session log fields (incl. flashlight/hand-light state) and the change record |

> **STATUS: the day/night system is about to be reworked** (Carlos, 2026-09-19): `TimeManager` + fog + skyboxes
> are to be rebuilt and merged with the lamp/flashlight/light-source work, and the full game's sun will move
> **continuously**, not by preset. Everything below describes **what exists today**. Anything that lights the
> player's hands must keep reading the **live sun** (`RenderSettings.sun`), never a preset.

---

## 1. Map of every light and lighting system

### 1.1 The world: sun, sky, fog

| Piece | What it is | Where | Notes |
|---|---|---|---|
| **SUN** | The scene's one Directional light. `RenderSettings.sun` = SUN in Island / Island_Legion | scene object with `SunController` | Only ONE directional light per scene (real-time lighting cost). No shadow-casting alternatives |
| `SunController` | Applies a `SunState` (elevation, azimuth, colour, intensity, optional colour temperature) instantly. Also owns the cabin's fast indoor dim (`SetIndoorDim`, placeholder trigger) | `Code/Runtime/World/SunController.cs` | Pure mechanism, no preset knowledge (MRM-47) |
| `TimeManager` | Named presets = skybox + `SunState`. `ApplyPreset(index/name, seconds)`; the sun lerps over the duration, **the skybox swaps instantly** | `Code/Runtime/World/TimeManager.cs` | MRM-69. `CurrentPresetIndex`, context menu "Apply Test Preset" |
| `SkyboxSwitcher` | Swaps `RenderSettings.skybox` from a list and refreshes ambient/reflections | `World/SkyboxSwitcher.cs` | Swaps are never blended: the story hides them (MRM-47) |
| `MoonTint` | Hue/intensity tint on the Moon's BaseColor (RetroLit `_BaseColor`) | `World/MoonTint.cs` | Cosmetic, `[ExecuteAlways]` |
| `IslandStartupPreset` | **Demo-only LEGACY.** On scene start picks Morning + fog ON or Night + fog OFF at random | `World/IslandStartupPreset.cs` | Added 2026-09-10 for the class demo |
| **HAZE fog** | Volumetric fog: `HazeRendererFeature` on `PC_Renderer`, global Volume `VP_HazeGlobalFog` + a `HazeDensityVolume` box | `Assets/ThirdParty/HAZE` (git-ignored) | Needs camera **post-processing ON**; lit by punctual lights (`_additionalLightContribution` = 1): the flashlight and Spotter lamps light the fog. See `pc-build-target.md` |
| Ambient / reflection | Ambient mode **Skybox**, intensity **0.4**; reflection intensity **0.25** (Island_Legion, 2026-09-19) | Lighting settings | Skybox changes move both |
| Debug keys | **F6** fog on/off, **F7** CRT on/off, **F8** next time-of-day preset | `Runtime/DevTools/` | See `debug-tools.md` |

**The four presets today** (read live from the Island_Legion `TimeManager`, 2026-09-19):

| Preset | Skybox | Elevation | Azimuth | Sun intensity | Sun colour |
|---|---|---|---|---|---|
| Morning | `M_Sky_FishHoekBeach` | 25 | 330 | **0.85** | (0.92, 0.94, 0.97) |
| Sunset | `M_Sky_FantasyClouds1Low` | 8 | 260 | 0.90 | (1.00, 0.55, 0.28) |
| Night | `M_Sky_NightSkyglowHeavy` | 35 | 90 | **0.12** | (0.55, 0.65, 0.90) |
| Apocalypse | `M_Sky_FantasySkyFire` | 15 | 330 | 1.20 | (0.85, 0.12, 0.08) |

At night the "sun" is a dim blue light (a moon stand-in), intensity 0.12. **Anything that scales with the sun's
intensity is near zero at night**; that is why the hands' light has a floor.

### 1.2 The player's own lights

| Light | What | Where | Values | Reaches the hands? |
|---|---|---|---|---|
| **Flashlight** | Spot Light with a cookie, toggled with **F** / D-Pad Up. No arm, no animation. `MoonlightFlashlight` | prefab `Player_Tracey` > `Body/Head/Camera/Flashlight` | `Flashlight*` tunables: intensity **60**, range **40**, outer **70°**, inner **25°**, colour `#FFE094`, shadows **None**, cookie **on** | **No** |
| **PlayerAmbientLight** | Small point light so you see things you hug in the dark | **Scene-level only**: a child of the camera in `Island.unity` and `Island_Legion.unity` (NOT in the prefab; Sandbox has none) | Point, intensity **0.34**, range **6** (values set on the scene object) | **No** |
| **ViewModelLight** | Directional light for the hands only (day: `sun x factor`, night: floor) | prefab, child of the camera | `ViewModel*` tunables, see `viewmodel-light-layers.md` | It IS the hands' light |
| Muzzle-flash lights | One Point per weapon (7 in the prefab), off until a shot | under each `MRM_Weapon_*` | intensity **2.5**, range **1.5** | **Yes** (layer 3) |

The flashlight's known look issue (a flat, milky disc when HAZE fog is on) is logged on **MRM-67 "Polishing Details"**.
The flashlight's cookie is `Flashlight_Cookie.png` (a dark-centred ring from the HQ FPS asset); `FlashlightUseCookie`
turns it off for a plainer, brighter beam.

### 1.3 Enemy and effect lights

| Light | What | Values / tunables | Notes |
|---|---|---|---|
| **Spotter lamp** | Point light on `Socket_Lamp/Lamp` of `Enemy_Spotter.prefab` | intensity **2.5**, range **14**, **soft shadows ON**, Default layer | Values live **on the prefab**: `SpotterLampIntensity`/`SpotterLampRange` in the tunables exist but **nothing reads them** (see 4). Sway = `LampSwayEffect` (`SpotterLampSway*` tunables, wired) |
| **Dropped lamp** | The lamp's `Light` keeps burning on the ground when the Spotter dies (`EnemyDeathDrop`); `LampFireEffect` lights a fire on it when it comes to rest | `LampFireVfxFadeDuration` | The look Carlos wants the hands to react to |
| **Flare light** | Real Light on the flare projectile (`FlareProjectile`), flickers | `FlareLightIntensityMin` **7**, `Max` **14**, `FlareLightRange` **26** (all wired) | Rule: glowing objects get a real Light, never an emission map |
| Feather glow | Light on the falling sparrow feather in the main menu | `FeatherGlowIntensity`/`DimmedIntensity`/`DimDuration` (wired) | MRM-18 |
| Mine lights | Cap on concurrent real-time lights in the mine | `MineMaxRealtimeLights` = 8 | **Not wired yet** (MRM-60) |

### 1.4 Render pipeline settings that matter for light

- **URP, `PC_RPAsset` / `PC_Renderer`, Forward+.** Forward+ is required by HAZE (punctual light contribution) and
  means there is no per-object light limit to plan around.
- **Light Layers are ON** (`m_SupportsLightLayers`). Rendering layer 1 is named **`ViewModel`**. See section 2.
- **Shadows:** quality presets set distance and cascades through tunables: Minimal 20 m / 1 cascade, Medium 45 / 2,
  High 70 / 3, Highest 90 / 4 (`Quality*Shadow*`). The frame is **GPU-bound** with shadow-atlas warnings (MRM-85), so every
  new shadow-casting light must be measured. The flashlight ships with shadows **off** on purpose; Spotter lamps cast soft
  shadows (a known GPU cost, hypothesis space in MRM-85).
- **Standing rule:** a glowing object gets a real `Light` on the prefab, never an emission map (RetroLit does not sample
  emission).

---

## 2. Light Layers, in five lines (full detail in `viewmodel-light-layers.md`)

1. A light only affects a mesh when their **rendering-layer masks share a bit**.
2. The player's hands and weapons are on **`ViewModel` only**; world meshes are on `Default` only.
3. Set a light's layers on **`UniversalAdditionalLightData.renderingLayers`**, NOT `Light.renderingLayerMask`
   (URP ignores the latter for lighting).
4. The **sun does not light the hands**; the hands' own directional light follows the live sun.
   **Other world lights within 15 m do light the hands** (a periodic scan adds the `ViewModel` bit); the player's own
   flashlight and personal light never do.
5. New light = `Default` only = does not touch the hands (correct default). To make one light the hands, give it the
   `ViewModel` bit too.

---

## 3. Who affects what

| Light | World meshes | Hands / weapons | Fog (HAZE) |
|---|---|---|---|
| SUN | yes | **no** | its own contribution |
| ViewModelLight | **no** | yes | no |
| Flashlight | yes | **no** | yes |
| PlayerAmbientLight | yes | **no** | yes |
| Spotter / dropped lamp, flare, fire (within 15 m) | yes | **yes** | yes |
| Player's muzzle flashes | yes | yes | yes |

---

## 4. Every lighting tunable (`MoonlightTunables`), and whether anything reads it

| Group | Tunables | Wired? |
|---|---|---|
| Flashlight (MRM-44) | `FlashlightIntensity` 60, `Range` 40, `OuterSpotAngle` 70, `InnerSpotAngle` 25, `Color` #FFE094, `Shadows` None, `ShadowStrength` 1, `UseCookie` true | yes (`MoonlightFlashlight`) |
| Hands' lighting (MRM-44) | `ViewModelSunFactor` 0.5 (day), `ViewModelLightFloor` 0.3 (night), `ViewModelLightPitch` 40, `Yaw` 20, `FollowsSunColor` true, `Color` white, `WorldLightsEnabled` true, `WorldLightMaxDistance` 15, `WorldLightRescanSeconds` 0.5 | yes (`MoonlightViewModelLighting`) |
| Sun / time | `SunIndoorDimTransitionSeconds` 1.5, `TimeManagerDefaultTransitionSeconds` 3 | yes |
| Flare | `FlareLightIntensityMin`/`Max`, `FlareLightRange` | yes (`FlareProjectile`) |
| Spotter lamp sway | `SpotterLampSwayMaxAngle`, `Frequency`, `ReferenceSpeed` | yes (`LampSwayEffect`) |
| Feather glow | `FeatherGlowIntensity`, `DimmedIntensity`, `DimDuration` | yes |
| Shadows | `Quality{Minimal,Medium,High,Highest}ShadowDistance`, `...ShadowCascades` | yes (quality presets) |
| **Spotter lamp** | `SpotterLampIntensity` 2.5, `SpotterLampRange` 14 | **NO: the values are on the prefab** |
| **Player personal light** | `PlayerAmbientLightIntensity` 1.2, `PlayerAmbientLightRange` 6 | **NO: the scene object has 0.34 / 6, the tunables are stale** |
| Mine | `MineMaxRealtimeLights` 8 | **NO** (MRM-60) |

The three "NO" rows break the no-hardcoded-values rule and are prime cleanup candidates for the lighting rework.

---

## 5. Traps (all of them, in one list)

1. **URP reads `UniversalAdditionalLightData.renderingLayers`, not `Light.renderingLayerMask`.** Setting only the
   latter left the hands matching no light at all: pure black. And a "no longer lit by X" result proves nothing until the
   object is also lit by what should light it: verify both directions.
2. **Keep `RenderSettings.sun` assigned.** If the hands' light is ever brighter than the sun (it is, at night) and no sun
   is assigned, URP picks the hands' light as the *main light* and the real sun loses its shadows. `MoonlightViewModelLighting`
   pins the sun if a scene forgot.
3. **HAZE needs camera post-processing ON** or fog shows in Scene view but not in Game view. The fix lives on the
   player prefab (`pc-build-target.md`).
4. **HAZE and Retro Shaders Pro are git-ignored** (`Assets/ThirdParty/**`): a fresh clone shows missing script references
   for fog and CRT until re-downloaded. Same for the **21 HQ mask maps** restored on 2026-09-19: on a fresh machine re-copy
   them from `E:\playground\weapon` or the guns go chrome.
5. **Missing mask map = chrome.** The HQ hand/weapon shader defaults a missing mask to fully metallic + fully smooth.
6. **A point light gets stronger by distance squared.** Never use one to "fill" the hands: at 0.3 m from the M1A's sights
   an intensity of 1 acted like ~11.
7. **The sun has no shadows on the hands** (the canopy shades the world, not the hands), which is why the world sun was
   taken off them.
8. **Skybox swaps are instant, never blended** (MRM-47). Hide them behind a story beat.
9. **Prefab vs scene:** `PlayerAmbientLight` exists only in Island / Island_Legion, so Sandbox and any new scene lack it.
   Fixes that must survive across scenes belong on `Player_Tracey.prefab`.
10. **Play Mode edits to a component are discarded on stop.** For live tuning use the *Live tuning* block (flashlight on the
    `Flashlight` child, hands' lighting on the `Camera`, **not** on the `ViewModelLight` child), then tell Claude the values.
11. **Editor screenshots are unreliable** (editor runs at 1-30 fps unfocused; the intro cinematic hides the hands for ~10 s).
    Verify by reading light layers / values back, plus a build for real judgement (`verification_requires_a_build`).
12. **Never `Thread.Sleep` inside a UnityMCP `execute_code`:** it freezes the editor's main thread.

---

## 6. Tracing and debugging light problems

- **Session log** (`Docs/performance-sessions.md` §2): every `[PERF]` line ends with `flashlight on|off handLights N`
  (SessionLog v4), and flashlight switches are `[PLAYER] flashlight -> on|off` lines.
- **Change record** (`performance-sessions.md` §8): a dated list of important changes with the docs and Linear
  comments that describe them, so a regression can be traced to a change.
- **Linear:** **MRM-86** (the lighting rework), MRM-44 (flashlight + hands; sway, breath, detection, chest lamp stay here), **MRM-85** (performance reports, change comments), **MRM-67** (polish, incl. the
  flashlight + fog look), MRM-47 / MRM-69 (sun, skybox, TimeManager), MRM-9 (player prefab / HQ FPS viewmodel).
- **Cheats:** F6 fog, F7 CRT, F8 time-of-day; F4 invulnerability. Quickest A/B for a lighting question.

---

## 7. Open items and what is coming

- **Lighting rework (next session, planned by Carlos 2026-09-19):** rebuild `TimeManager` and the fog system, change the
  skyboxes, and merge the day/night light with the lamp / flashlight / light-source work. Wanted: **predefined baseline
  values for each time of day** (night, day, dusk...) and a **dynamic sun that changes gradually**, with the hands' light
  following it. Tracked as **Linear MRM-86** (branch `mrm-86`). Handoff: `Docs/lighting-rework-opus-prompt.txt`.
- **Fog + flashlight look** (MRM-67): the beam reads as a flat milky disc under HAZE. Fallback if a plain Spot Light cannot
  look right: Volumetric Light Beam (installer unextracted).
- Wire the three unwired tunables (4), or delete them.
- The hands will be replaced by Tracey's own model; the layer/light system is model-agnostic.
- The HQ hand/weapon textures have no pixelation pass and are not RetroLit yet.
- Idea from Carlos (drug system, later): extreme hands' light values looked interesting; the tunables could be driven by
  status effects.
- Hands' light numbers (`0.5` / `0.3` / pitch 40 / yaw 20) are guesses until Carlos tunes them; night hands were
  asked to be "bumped up".

---

## 8. Change history (newest first)

| Date | Change | Where recorded |
|---|---|---|
| 2026-09-19 | Lighting rework issue **MRM-86** created (branch `mrm-86`); process rules updated (branch `--no-track`, final-instructions step 4), change record C-002 | `performance-sessions.md` §8; Linear MRM-86, MRM-85; commit `c91ff942`, PR #34 merged as `3d207360` |
| 2026-09-19 | This file created; SessionLog v4 (`flashlight`/`handLights` in `[PERF]`, `[PLAYER] flashlight` lines); change-record system added to `performance-sessions.md` §8 | `changelog.md` MRM-44 entry; MRM-85 comment; commit `e99866fd` (change record C-001) |
| 2026-09-18 | Flashlight tunables + live tuning; hands on the `ViewModel` layer; hands' directional light following the sun with a night floor; world lights (lamps, flares, fires) light the hands; 21 HQ mask maps restored (guns PBR), arms matte | `viewmodel-light-layers.md`, `changelog.md` MRM-44 entries; commit `e99866fd` (C-001) |
| 2026-09-18 | Flashlight first iteration (F key, Spot Light, no animation) | `changelog.md` MRM-44 first-iteration entry; commit `777b0ceb` |
