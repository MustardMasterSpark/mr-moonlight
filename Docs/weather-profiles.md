# Weather profiles (MRM-86)

**Created 2026-09-29.** A *weather profile* describes what one moment of the game looks like: its sky, its light and
its fog. Later it will also carry rain, wind, particles, audio cues and the player's own two lights. There is one
profile per story part and per Legion act, the same 27 moments as `sky-catalog.md`.

**Status:** the profiles exist and can be tuned and saved in the **Lighting Test Scene** only. Nothing in the real
game (Island, Island_Legion, Sandbox) uses them yet: how they get wired into the game is Carlos's next call. Island still
runs the old `SUN` / `TimeManager` / `SkyboxSwitcher` / `Scene Effects Toggle` / HAZE objects, which this work replaces
later, not now.

All values start **clean** (Carlos, 2026-09-29): Unity's default sun and ambient, HAZE's default fog. Nothing was carried
over from Island's old presets.

## Where things are

| What | Path |
|---|---|
| The profiles (one asset, 27 entries) | `Assets/_Project/Data/Weather/WeatherProfiles.asset` |
| Code | `Assets/_Project/Code/Runtime/World/Weather/` (`WeatherProfile`, `WeatherProfileLibrary`, `WeatherSystem`, `HazeFogAdapter`, `WeatherLightSource`, `WeatherTuningPanel`) |
| Sky blend | `World/SkyBlender.cs` + `Art/Environment/Skies/M_Sky_Blend.mat` (shader `Skybox/SkyboxBlender`, AST-054, tracked copy in `Code/Vendor/AST-054 Skybox Blender/`) |
| Test circuit | `World/SkyProximityCircuit.cs` (debug spheres by Draw Debug Tools, AST-271, `Code/Vendor/AST-271 Draw Debug Tools/`) |
| **Control board** (the Inspector you tune on) | `WeatherSystem` on the scene's **`Weather`** object; buttons from `Code/Editor/WeatherSystemInspector.cs` (2026-09-30) |
| O key | `DevTools/EnemyVisibilityToggle.cs` |
| I key (tree fire experiment) | `DevTools/TreeFireToggle.cs`, effect `Prefabs/VFX/TreeFire_FX.prefab`; full record `tree-fire-experiment.md` |
| Test scene | `Assets/_Project/Scenes/LightingTestScene.unity` + folder `LightingTestScene/` (own fog profile `VP_LightingTest_Fog`, marker material) |

## What one profile holds

Open `WeatherProfiles.asset`; each list entry is titled with its moment ("Story 1 · Campsite" … "Legion W7 A2 · Conflict").

| Section | Contents |
|---|---|
| **Skybox** | The act's `Skybox/Cubemap` material |
| **Sun** | Elevation, azimuth, colour, colour temperature (on/off + Kelvin), intensity, indirect multiplier, shadow type, shadow strength |
| **Environment** | Ambient source (Skybox / Gradient / Color), skybox intensity, sky/equator/ground colours, reflection intensity |
| **\* Special light sources** | **Lamps** (Spotter lamps + dropped lamps), **Flares**, **Moon Glow**, **Tree Fires** (the tree fire experiment's lights, added 2026-09-30). Each: an **Override** switch, colour, intensity, range, shadow type, shadow strength. **Override off = those lights keep their own prefab values.** One edit restyles every light of that kind at once |
| **Fog** | On/off; **Global** (every HAZE global-fog parameter); **Area** (the "HAZE Explorable Area Fog" box: weight, density, colours, height fog, lighting); **Noise** (HAZE noise + multiple scattering, with its own Override switch) |

### "Special light sources" (the \* group)

In the Inspector this group has its own header, **"\* Special light sources (not the sun)"**, and every tooltip in it
starts with "\*". These are individual lights placed in the world, not the global illumination. They work differently
from the Sun and Environment: they are **tuned in the profile only**, not by saving from the scene, because one edit must
restyle every lamp in the level at once.

**The player's own lights will join this group later** (Carlos, 2026-09-29): the personal light around the player
(`PlayerAmbientLight`) and the hands' light (`ViewModelLight`, `MoonlightViewModelLighting`). They are deliberately
not in the profiles yet. Weapon muzzle flashes and the flashlight are not planned for profiles.

Which lights count is decided by a small marker component, `WeatherLightSource` (category Lamp / Flare / MoonGlow), on the
Light's GameObject in the prefab: `Prop_Lamp` (also covers the Spotter's lamp, which is a nested `Prop_Lamp`), `VFX_Flare`,
`Prop_Moon`. A new kind of world light needs the marker plus a category. Lights spawned at runtime pick up the current
weather when they appear. In scenes without a `WeatherSystem` the marker does nothing.

### The fog section may change

It is shaped after **HAZE** (AST-078), the fog we use today. Carlos is considering **Volumetric Fog & Mist 2** (Kronnect),
mainly because it ships a sandstorm preset and wind (Legion W3 A3 / W4 A2), and may compare both, performance included.
If the fog asset changes, only `FogSettings` (in `WeatherProfile.cs`) and `HazeFogAdapter.cs` change; the rest of the
profile does not. Comparison notes: MRM-86 Linear comment of 2026-09-29.

## Using the Lighting Test Scene

It is a copy of Island_Legion (trees, terrain, water, Spotters), with the old sky/time controllers removed from the copy.
The circuit stands on the flattest open ground near the Glade, about 58 m south of it (centre 414.6, −65.2), corners 22 m
apart; the player spawns 2 m inside corner 0.

1. **Play.** You start on the scene's own look (the clean defaults, and the scene's starting sky).
2. **Walk to the yellow cylinder.** Inside the pink sphere (8 m) everything blends toward that cylinder's weather: sky,
   sun, ambient, special light sources and fog together. At the green sphere (2 m) it locks in, and the cylinder moves to
   the next corner (clockwise) with the next weather. After the last it loops.
3. **Tune the weather you are standing in** (anywhere outside a pink sphere) on the **control board**: select the
   **`Weather`** object; the Inspector shows every section (sky, sun, environment, special light sources, fog) at the top.
   The board owns nothing: each change is written straight to the real sun, ambient, lights and fog, live. Editing the
   current weather's entry in `WeatherProfiles.asset` also works, and the board follows it. **Do not tune on the `SUN`
   light or the HAZE objects directly**: it shows on screen, but the board does not see it and Save will not keep it.
4. **Save** with the buttons at the top of the `Weather` Inspector (Save everything / Save lighting / Save fog, and Revert
   board to saved profile), or with **P**, which opens the panel bottom-right and frees the mouse (Save Lighting Values =
   sky, sun, environment and special lights; Save Fog Values = fog). Greyed out while a blend runs.
5. **O** hides every enemy and pauses spawning; O again brings the same enemies back where they were.

The bottom-right corner always shows the current weather and the key reminder: `[P] tuning panel   [O] enemies ON/OFF`.

**Saving works only in the editor's Play Mode** (it writes the asset, and asset edits survive stopping Play). A build can
apply profiles but not save them. The board is reloaded from the profile whenever a blend locks: unsaved board edits are
lost then.

## Keys (Lighting Test Scene only)

| Key | What |
|---|---|
| **P** | Weather tuning panel (Save Lighting / Save Fog), frees the mouse |
| **O** | Enemies off / on (hide + pause spawning, then restore) |
| **I** | Tree fires on / off (`tree-fire-experiment.md`). Also bound to the vendor Inventory action |
| **F11** | Draw Debug Tools free-fly debug camera (freezes time). Moved from F9, which is Infinite Ammo |

## Tunables (`MoonlightTunables`, "Sky proximity blend" section)

`SkyBlendStartRadius` 8, `SkyBlendCompleteRadius` 2, `SkyBlendSphereHeight` 1, `SkyBlendSphereSegments` 24,
`SkyBlendStartColor` pink, `SkyBlendCompleteColor` green, `SkyBlendCaptureResolution` 512, `WeatherAmbientRefreshSeconds`
0.25. The circuit has a live-tuning block for the sphere values.

## How it works (for whoever wires it into the game)

- `WeatherSystem.BeginTransition(i)` → `SetBlend(0..1)` → `Lock()`. The blend runs from the current weather to the target,
  everything with the same value. The test circuit drives it by distance; the game could drive it by time, an Event
  Director verb, or a trigger.
- Colour blends use the final colour (colour × temperature), so a Kelvin-driven sun blends smoothly into an RGB one.
  Switches (shadow type, ambient source, noise override) change at the end of a blend.
- Fog "off" fades (density and box weight go to zero) instead of cutting.
- Skybox ambient is recomputed (`DynamicGI.UpdateEnvironment`) every 0.25 s during a blend and once at the lock.
- The fog is written to the Volume's **runtime copy** of its profile, so Play Mode never changes the fog asset itself.
- `SkyBlender` snapshots the starting sky with a sky-only camera (`Camera.RenderToCubemap`); realtime reflection probes
  are switched off in this project's Quality settings, so a probe capture fails.

## Traps found building it

- `GameObject.FindGameObjectWithTag("Player")` can return the player's **`Body/Hitbox`** child (also tagged Player). Take
  `.transform.root`. A scripted move of "the player" moved the hitbox off the body once this session.
- `MoonTint` runs in the editor and **writes its tint into the shared `M_Moon.mat`** whenever a `Prop_Moon` is loaded,
  including when the prefab is opened for editing. If `M_Moon.mat` shows up modified, revert it.
- HAZE's area box ignores property changes in Play Mode if its GameObject is marked **static**. The test scene's box is not.
- Volume parameters only apply with their override switch on; `HazeFogAdapter` sets it on every write.
- Opening Island_Legion in the editor marks it dirty with no edit made (probably Gaia / Flora / Water editor scripts).
  Don't save it unless you meant to change it.
- Recompiling scripts while Play Mode is running throws `NullReferenceException` in Polymind's
  `MotionBehaviour.OnEnable`. That is the mid-play recompile, not a bug in new code: stop Play before compiling.

## Open items

- How profiles are wired into the real game (Carlos to describe). Island's old objects get replaced then.
- Tune all 27 profiles in the test scene (they are all identical defaults now).
- Add the player's two lights as special light sources (they would go next to Tree Fires in the \* group, and onto
  the control board automatically).
- Rain, wind, particles and audio cues per profile (Carlos's per-act notes in `sky-catalog.md`).
- HAZE vs Volumetric Fog & Mist 2 comparison, including a performance build.

## Session 2026-09-30

- **Control board.** Carlos wanted one object whose Inspector has a section for everything a weather holds, wired live
  to the real objects without owning them, like the tunables but for this scene. The existing `Weather` object
  (`WeatherSystem`) was reshaped into that: a serialized `live` profile at the top of its Inspector, synced section by
  section (a profile-asset edit flows onto the board, a board edit flows to the scene), plus Save everything / Save
  lighting / Save fog / Revert buttons (`WeatherSystemInspector`). The P panel's saves now copy the board (Save Lighting
  now includes the sky and the special lights). Direct edits of `SUN` / HAZE objects are no longer captured by Save.
  Wiring fields moved under a "Wiring (set once)" header; serialized names unchanged, so the scene needed no rewiring.
- **Tree Fires special light source.** `WeatherProfile.TreeFires` (Override, colour, intensity, range, shadows, shadow
  strength; defaults = the vendor fire light: colour (1, 0.545, 0.25), intensity 0.89, range 30, no shadows). Not a
  `WeatherLightSource` category: the fire lights belong to `TreeFireToggle`, which reads
  `WeatherSystem.TryGetTreeFireLights` every frame (board values, or the blend of two weathers during a transition; a
  weather with Override off contributes the effect's own light). The flicker is added on top of Intensity. All 27
  profiles were written with the defaults, Override off. Verified in Play Mode: a green override with range 12 reached
  the fire lights.
- **Demo intro removed from the test scene.** The ELVTR-demo `Letterbox` bars and `Demo Intro Subtitles` objects were
  deleted from `LightingTestScene` (Carlos: they were only for the first demo; strip them from new scenes). Island and
  Island_Legion still have them.
- **Tree fire experiment** (I key): see `tree-fire-experiment.md`.

## Session 2026-09-30 (evening): two weather systems, one per fog asset
- The weather code is now split so each fog asset has its own system and data (Carlos's ruling: no HAZE values in the VF2
  scene, no VF2 values in the HAZE scene).
  - `WeatherProfileBase` = name, sky, sun, environment, lamps/flares/moon/tree fires. `WeatherProfile` (HAZE) adds `Fog` =
    `FogSettings`; `Vf2WeatherProfile` adds `Fog` = `Vf2FogSettings`.
  - `WeatherProfileLibraryBase` -> `WeatherProfileLibrary` (HAZE, `Data/Weather/WeatherProfiles.asset`) and
    `Vf2WeatherProfileLibrary` (`Data/Weather/WeatherProfiles_VF2.asset`). A ScriptableObject class must live in a file named
    after it (the VF2 library was first in the wrong file and its asset got `m_Script: 0`).
  - `WeatherSystemBase` (abstract; blend, board, save, tree fire lights) -> `WeatherSystem` (HAZE) and `Vf2WeatherSystem`
    (menu "Weather System (Volumetric Fog 2)"). `WeatherSystemBase.Active` is what the circuit, panel, tree fires, light
    sources and session log use.
  - `IFogAdapter`: `HazeFogAdapter`, `Vf2FogAdapter` (works on a runtime copy of the VF2 fog profile; "Enabled" off hides the
    fog mesh).
- Scene 07 `LightingTestScene` uses the HAZE system and library (unchanged); scene 08 uses the VF2 ones.
- VF2 inputs (about 45) are in `Vf2FogSettings`; what the main ones do and the traps are in `fog-experiment-ast282.md`.
- The `Scene Effects Toggle` prefab (F6 fog, F7 CRT, inspector checkboxes) was added to scene 07 on 2026-10-01 (it was only in
  Island and Island_Legion).
