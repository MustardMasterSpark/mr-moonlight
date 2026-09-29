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
| O key | `DevTools/EnemyVisibilityToggle.cs` |
| Test scene | `Assets/_Project/Scenes/LightingTestScene.unity` + folder `LightingTestScene/` (own fog profile `VP_LightingTest_Fog`, marker material) |

## What one profile holds

Open `WeatherProfiles.asset`; each list entry is titled with its moment ("Story 1 · Campsite" … "Legion W7 A2 · Conflict").

| Section | Contents |
|---|---|
| **Skybox** | The act's `Skybox/Cubemap` material |
| **Sun** | Elevation, azimuth, colour, colour temperature (on/off + Kelvin), intensity, indirect multiplier, shadow type, shadow strength |
| **Environment** | Ambient source (Skybox / Gradient / Color), skybox intensity, sky/equator/ground colours, reflection intensity |
| **\* Special light sources** | **Lamps** (Spotter lamps + dropped lamps), **Flares**, **Moon Glow**. Each: an **Override** switch, colour, intensity, range, shadow type, shadow strength. **Override off = those lights keep their own prefab values.** One edit restyles every light of that kind at once |
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
3. **Tune the weather you are standing in** (anywhere outside a pink sphere):
   - edit its entry in `WeatherProfiles.asset`: the scene follows immediately, one section at a time; **or**
   - edit the `SUN` light, the Lighting window's Environment values, the `HAZE Global Fog` volume or the
     `HAZE Explorable Area Fog` box directly, then save.
4. **P** opens the panel (bottom-right) and frees the mouse. **Save Lighting Values** copies the live SUN + Environment
   into the current profile; **Save Fog Values** copies the live fog. Greyed out while a blend runs. P again to play.
5. **O** hides every enemy and pauses spawning; O again brings the same enemies back where they were.

The bottom-right corner always shows the current weather and the key reminder: `[P] tuning panel   [O] enemies ON/OFF`.

**Saving works only in the editor's Play Mode** (it writes the asset, and asset edits survive stopping Play). A build can
apply profiles but not save them. Unsaved direct edits are lost when the next blend starts.

## Keys (Lighting Test Scene only)

| Key | What |
|---|---|
| **P** | Weather tuning panel (Save Lighting / Save Fog), frees the mouse |
| **O** | Enemies off / on (hide + pause spawning, then restore) |
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
- Add the player's two lights as special light sources.
- Rain, wind, particles and audio cues per profile (Carlos's per-act notes in `sky-catalog.md`).
- HAZE vs Volumetric Fog & Mist 2 comparison, including a performance build.
