# View-model lighting and URP Light Layers

> **Part of the lighting docs: start from `Docs/lighting.md`** (the single reference for every light, the day/night system
> and all lighting tunables). This file is the deep dive on the hands.

**Added 2026-09-18 (MRM-44). Reworked the same evening** (see "History"). This is the project's first use of URP
**Light Layers** (a.k.a. rendering layers). Read it before adding any light, any first-person mesh, or any change to
how the sun / time of day works.

## The rules in five lines

1. The player's hands and weapons live on the **`ViewModel`** rendering layer *only*. Lights on `Default` only
   (the flashlight, the personal light, **the world sun**) do **not** touch them. **Other world lights do** (Spotter
   lamps, fires, flares...): a periodic scan gives them the `ViewModel` bit while they are within 15 m.
2. The hands are lit by **one dedicated directional light** (`ViewModelLight`, child of the player camera, `ViewModel`
   layer only). Its brightness **follows the world sun every frame**: `max(ViewModelLightFloor, sun.intensity * ViewModelSunFactor)`.
3. **A light's layers are set on `UniversalAdditionalLightData.renderingLayers`, NOT `Light.renderingLayerMask`.**
   URP ignores the latter for lighting (see Traps).
4. **Guns are PBR, hands are matte.** The guns use their vendor mask maps (metallic/smoothness/AO); the arm materials
   have `_SmoothnessIntensity` = 0.25.
5. **The sun will become dynamic.** Everything here reads the *live* sun, never a preset. Do not add a preset table for
   the hands.

## FOR THE FULL GAME: the sun moves continuously

Today the island has fixed lighting presets (`TimeManager` presets: skybox + `SunState` for morning, night, etc.). The
**full game will not: the sun will change gradually, through a continuous day/night cycle.** Carlos, 2026-09-18:
*"Lights are going to be dynamic. The sun is going to gradually change... When we change the lighting we also change how
the hands are getting their value."*

So the hands' light is deliberately **not** driven by presets. `MoonlightViewModelLighting.Update` reads
`RenderSettings.sun.intensity` and `.color` every frame, so:

- a preset switch, a 3-second `TimeManager` transition, and a slow continuous day cycle all just work, with no change
  to the hands' code;
- if the future system moves the sun by another means (a new controller, a different Light), it only has to keep
  `RenderSettings.sun` pointing at the real sun (the component falls back to the brightest directional light if it is
  unset, and pins it as `RenderSettings.sun` in `Start` so the hands' own light can never be picked as URP's main light);
- the **night look of the hands is `ViewModelLightFloor`**, not a preset: the sun's intensity near midnight is close to
  zero, and copying it would leave the hands black. Carlos asked for the night hands to be *bumped up*, hence the floor.

**If the day cycle ends up needing a different curve than "half the sun, with a floor"** (say brighter hands at dusk),
add a curve to the tunables (`AnimationCurve` of sun intensity -> hands intensity) inside `MoonlightViewModelLighting.Update`.
Do not start reading presets.

## Why (history of the problem)

The first-person hands and weapons use `LitFieldOfView` (a normal Lit shader graph), so every light shone on them:

1. **`PlayerAmbientLight`** (the small point light on the camera that lets you see things you hug in the dark): too
   strong on the hands.
2. **The flashlight** (a metre or less from the hands): blew them out to white.
3. **The world sun**: nothing shades the hands from it. In a forest the canopy darkens the world but the hands stayed in
   full, unshadowed sun, so in the morning they were far brighter than everything around them.
4. **A point "fill" light** (first attempt at "a little bit lit"): a point light gets stronger by distance squared, so at
   0.3 m from the M1A's sights an intensity of 1 acted like ~11 and the sights went pure white.
5. **The materials were chrome.** See "Materials".

### Why not a second camera

Camera stacking (an overlay camera for the hands) was rejected: **URP lights affect every camera's renderers**, so a
separate camera does not exclude a light. Light Layers is the tool built for this, and costs no extra render or culling.

## What is set up

| Piece | Where | Value |
|---|---|---|
| Layer name | Project Settings > Tags and Layers > Rendering Layers (`TagManager.asset`, `m_RenderingLayers[1]`) | **`ViewModel`** (renamed from "Light Layer 1") = bit `2` |
| Hands, weapons (every `MeshRenderer` / `SkinnedMeshRenderer` under the player camera) | `Renderer.renderingLayerMask` | `Default` bit **removed**, `ViewModel` **added**. Other bits preserved (e.g. the knife's mask is `258`) |
| Flashlight, `PlayerAmbientLight`, **the sun** | `UniversalAdditionalLightData.renderingLayers` | `Default` only (`1`) |
| **Every other non-sun light within range** (Spotter lamps, dropped lamps, flares, fires, enemy muzzle flashes) | `renderingLayers`, set by the periodic scan | `Default` + `ViewModel` (`3`) while within `ViewModelWorldLightMaxDistance` (15 m), `1` otherwise |
| Weapon muzzle-flash lights (nested deep under the camera) | `UniversalAdditionalLightData.renderingLayers` | `Default` + `ViewModel` (`3`): a shot lights the gun *and* the world |
| **`ViewModelLight`** (Directional, child of the player camera, local rotation (40, 20, 0)) | `renderingLayers` | `ViewModel` only (`2`). No shadows |

The URP asset (`PC_RPAsset`) already had **Light Layers** enabled (`m_SupportsLightLayers = True`); nothing changed there.
The renderer is **Forward+**.

## Who does the work: `MoonlightViewModelLighting`

`Assets/_Project/Code/Runtime/Player/MoonlightViewModelLighting.cs`, on the **player camera** in `Player_Tracey.prefab`.

- `Awake`: moves every mesh under the camera to `ViewModel`; adds `ViewModel` to the weapon lights; sets the hands' light
  to `ViewModel` only.
- `Start`: if `RenderSettings.sun` is unset, pins it to the found sun.
- `Update`: finds the sun (cached, re-found if it goes away), sets the hands' light intensity, colour and direction;
  every `ViewModelWorldLightRescanSeconds` (0.5 s) runs `ScanWorldLights`.
- `ScanWorldLights`: for every active non-directional light that is **not under the player root** (so not the flashlight,
  personal light or the hands' light), sets the `ViewModel` bit if it is within `ViewModelWorldLightMaxDistance`, clears it
  if not. Only writes when the bit changes. A periodic rescan because lamps, flares and fires spawn at runtime; nothing to
  author per prefab. `FindObjectsByType` + `GetComponent` on a few hundred lights every 0.5 s is negligible.

By rule, not per renderer: a weapon or scene added later needs **no setup**. Consequence: **edit-mode Scene view does
not show the final lighting; only Play Mode does.**

The light's direction (`ViewModelLightPitch` / `ViewModelLightYaw`), colour and intensity are **driven every frame** from
the tunables, so rotating the `ViewModelLight` child by hand does nothing: change the tunables (or the live-tuning fields).

## Tuning

`MoonlightTunables` > *View-model lighting* (all applied by `MoonlightViewModelLighting`):

| Tunable | Default | Meaning |
|---|---|---|
| `ViewModelSunFactor` | 0.5 | Fraction of the sun's intensity the hands get. **The day profile** |
| `ViewModelLightFloor` | 0.3 | Minimum brightness. **The night profile**: the sun is near zero at night, so this is the hands' night look |
| `ViewModelLightPitch` | 40 | Degrees the hands' light points down from straight ahead of the camera |
| `ViewModelLightYaw` | 20 | Degrees it is turned to the player's right (so the light comes from the left) |
| `ViewModelLightFollowsSunColor` | true | Take the sun's colour every frame (warm at dusk, cold at night) |
| `ViewModelLightColor` | white | Fixed colour, used only when the toggle above is off |
| `ViewModelWorldLightsEnabled` | true | Whether non-sun world lights also light the hands |
| `ViewModelWorldLightMaxDistance` | 15 m | A world light only lights the hands within this distance of the player |
| `ViewModelWorldLightRescanSeconds` | 0.5 | Seconds between scans (lights spawn at runtime) |

**World lights on the hands (added 2026-09-18, evening).** Carlos: the hands should also react to lights that are not the
sun, e.g. a dead Spotter's dropped lamp (`Lamp Light`: Point, intensity 2.5, range 14, **soft shadows on**, so trees occlude
it on the world and the hands). The player's own lights stay off the hands (they were the blow-out); everything else that
is a light reaches them. Verified live: a test lamp 2 m away got layers 3, one 40 m away stayed 1, flashlight/personal
light/sun stayed 1, hands' light 2; a lamp behind the camera turned the hand and knife edge orange.
Limits: a world light *without* shadows can light the hands through cover (mitigated by the distance cap); walking right
next to a lamp will burn the hands out like the ground beside it; a light in front of the hands lights them from behind,
which looks dark (physically right, not a bug).

**The two profiles saved on 2026-09-18** are the current day/night behaviour: *day* = `sun x 0.5` (0.425 at the morning
preset's sun of 0.85) and *night* = the 0.3 floor (verified: sun 0.03 gives 0.3). **They are a snapshot for the current
fixed presets.** When the scene's lighting becomes dynamic they will be re-tuned, but the mechanism (follow the live sun,
floor for the dark) is meant to survive. Values are guesses until Carlos tunes them.

Live tuning, same pattern as the flashlight: in Play Mode tick **Live tuning** on *Moonlight View Model Lighting* (player
camera), edit the fields (the *Read-only* block shows the sun being followed and the current intensity), and tell Claude
the values. Play Mode edits are discarded on stop; fields re-seed from the tunables on every scene start (and every ~64
frames while Live tuning is off, so editing the `MoonlightTunables` asset during Play Mode also works). Right-click the
component > *Reload values from MoonlightTunables* to reset.

**The hands will be replaced** by Tracey's own custom model later. The whole setup (layer, light, tunables) is
model-agnostic: anything under the player camera is put on the `ViewModel` layer automatically, so the new model needs
only its own mask map / materials sorted out, not this system.

## What this did NOT touch (which weapons are affected)

Asked 2026-09-18: did the PBR change hit only the first-person weapons, or every weapon?

- **Only the player's first-person set.** The `ViewModel` layer, the hands' light and the arm smoothness apply to meshes
  under the **player camera** (`Player_Tracey`). The mask maps restored are the HQ FPS ones, used by the `FP_*` materials
  (`LitFieldOfView` shader).
- **Enemy weapons are untouched.** The Spotter's double-barrel shotgun (`Pickup_DoubleBarrelShotgun_*`) uses
  `Art/Weapons/Shotgun/M_Shotgun.mat` and the flare gun `Art/Weapons/FlareGun/M_FlareGun.mat`: both
  **`Retro Shaders Pro/Retro Lit`** (the project's own look, BaseColor + Normal only, no mask map, world lights, rendering
  layer `Default`). None of the changes above reach them, and they were never PBR-chrome.
- The restored mask files are *also* referenced by the HQ **world-model** materials (`AKM.mat`, `M1A.mat`, `DBShotgun.mat`,
  ... in `ThirdParty/AST-046`, the non-`FP_` ones), so those now resolve a mask too. **None of them are used anywhere in
  `Assets/_Project`** (checked by GUID across prefabs, scenes and assets), so no visible effect today. If a pickup or drop
  ever uses them, they now render as proper PBR instead of chrome, but they would light like any world object.

## Materials (guns PBR, hands matte)

The hands and weapons use the vendor shader graphs `LitFieldOfView` / `LitFieldOfView_SSS`
(`Assets/_Project/Code/Vendor/PolymindGames/Shaders/`), which read **BaseColor, Normal and a Mask map** (R = metallic,
A = smoothness, plus AO), with `_SmoothnessIntensity` as a multiplier.

**The migration had dropped the mask maps** (only BaseColor + Normal came over, following the RetroLit two-map rule that
does not apply to these vendor materials). A missing mask defaults to **white**, i.e. fully metallic and fully smooth:
every hand and gun was chrome, reflecting the sky and flaring under any light. That, plus the unshadowed sun, is why they
looked "too bright".

Fixed 2026-09-18:
- **21 mask maps copied** from the Weapon project (`E:\playground\weapon`, path
  `Assets/PolymindGames/...` -> `Assets/ThirdParty/AST-046/...`), `.meta` files with them so GUIDs match the materials:
  18 in `HQFPS/Art/Meshes/Wieldables/*/Materials/*_MaskMap.png`, plus `Ammo_MaskMap`, `SniperScope1_MaskMap`,
  `ScopeLens_MaskMap`. **All 21 materials on the player resolve a mask now.** Three weapons' masks (F1, FlareGun, MP5) were
  not copied because those weapons are not in this project.
- **Arms matte:** `FP_Arm_Standard` (was 0.75) and `FP_Arm_Shirt&Gloves` (was 1.0) `_SmoothnessIntensity` = **0.25**.
  Guns stay on their vendor values (1.0).
- `Assets/ThirdParty/**` is git-ignored, so none of these show in GitHub Desktop (expected). **On a fresh machine the mask
  maps must be re-copied from the Weapon project** or the guns turn to chrome again: add them to the asset-restore notes.

**Pixelation:** these HQ textures are the vendor's originals (512x512) and the materials are not RetroLit, so they have
**not** had the pixelation pass. Not done here; part of the still-unfinished RetroLit port for first-person
(`memory: mrm9_weapon_darkness_deferred`).

## Rules for future work

1. **A new first-person mesh** (weapon, hands, held prop): put it under the player camera and it is handled at `Awake`.
   Runtime-instantiated ones would need a refresh call added.
2. **A new light that should light the hands** (muzzle flash, held lantern): give its `UniversalAdditionalLightData`
   the `ViewModel` bit as well as `Default`. Lights that are direct children of the camera are skipped by the automatic
   rule on purpose.
3. **A new light that must NOT light the hands:** do nothing. `Default` only is the default.
4. **Never put a `ViewModel` mesh back on `Default`**; that re-opens the flashlight blow-out.
5. **Never use a point light for the hands' fill.** Distance-squared falloff blows out anything close.
6. **Light Layers are not GameObject layers.** URP ignores `Light.cullingMask`.
7. A new first-person weapon imported from HQ FPS needs its **mask map** copied with it, or it renders as chrome.
8. Only 7 rendering layers besides `Default` exist; 1 is used. Name new ones in Tags and Layers before using them.

## Traps found

- **THE BIG ONE: URP lighting reads `UniversalAdditionalLightData.renderingLayers`, not `Light.renderingLayerMask`.**
  Every light's additional data was `1` (Default) whatever `Light.renderingLayerMask` said. The first version set only
  `Light.renderingLayerMask`, so ViewModel-only hands matched **no light at all** and rendered pure black, day and night.
  It looked "fixed" for a while: the flashlight stopped blowing the hands out only because *nothing* lit them. Setting
  `renderingLayers` on the additional data (which also updates `Light.renderingLayerMask`, not the other way round) fixed
  it. **Lesson: a "no longer lit by X" result proves nothing until the thing is also lit by what *should* light it.
  Verify both directions.** Found by control tests: mesh `ViewModel`+`Default` was lit, mesh `ViewModel` only never was.
- **`PlayerAmbientLight` is a direct child of the camera**, so an early "weapon lights" rule swept it in. The rule now
  only takes lights nested deeper than the camera's direct children.
- **Main-light trap:** URP picks the "main light" (the one with shadows and cookies) from `RenderSettings.sun`, else the
  brightest directional light. At night the hands' light (floor 0.3) can be brighter than the sun (~0.1); if the scene had
  no `RenderSettings.sun`, the hands' light would become the main light and the real sun would lose its shadows.
  `RenderSettings.sun` is `SUN` in the Island scenes, and the component pins it if a scene forgets.
- **Editor screenshots are slow** (1-30 FPS unfocused; the intro cinematic hides the hands for ~10 s). Still usable for A/B
  control tests. Do not `Thread.Sleep` inside `execute_code`: it freezes the editor's main thread and the frame with it.
- `Renderer.renderingLayerMask` is `uint` and is correct for meshes; only *lights* need the additional-data route.
  `UniversalAdditionalLightData.renderingLayers` is a `RenderingLayerMask` struct: cast from/to `uint` (`.value`).
- A `UniversalAdditionalLightData` is not added automatically when a Light is created from a script; add it explicitly.

## History

- 2026-09-18 (1): created (MRM-44). Rendering layer `ViewModel`; the hands excluded from the flashlight and personal light.
  Flashlight defaults saved the same session: intensity 60, range 40, outer 70, inner 25, colour `#FFE094`, shadows None, cookie on.
- 2026-09-18 (2): hands were pure black (the `renderingLayers` trap above); fixed.
- 2026-09-18 (3): hands still far too bright by day and at night (Carlos's screenshots: M1A and revolver blown to white).
  Found chrome materials (missing mask maps) and the unshadowed sun. Reworked: mask maps restored, arms matte, the sun no
  longer lights the hands, point fill replaced by a directional light that follows the sun with a night floor.
  Tunables `ViewModelFillIntensity`/`ViewModelFillRange` removed; `ViewModelSunFactor`/`ViewModelLightFloor` added.
