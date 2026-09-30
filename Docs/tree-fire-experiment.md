# Tree fire experiment (MRM-86, Lighting Test Scene)

**The living record for fire on the trees.** Every change to it goes here as a new dated entry in section 4
(newest first), with what changed, why, and what it measured. Carlos, 2026-09-30: "Document every change that we make
from now on regarding these tree illuminations because we might tweak it... change the fog and see how this works...
even have a different culling strategy." Read section 4 before changing the strategy: it says what was already tried.

Started 2026-09-30 on branch `mrm-86`. Experiment only: it lives in `LightingTestScene` and touches nothing in the
real island scenes.

---

## 1. What it is

**I** in the Lighting Test Scene sets the forest on fire; **I** again puts it out. The effect is Ian's Fire Pack
(AST-015) "Burning Tree URP", with its own tree removed, fitted to our trees. The key reminder bottom-right shows
`[I] tree fires ON/OFF`.

**Heads-up:** in the vendor controller's input asset (`FPS_InputActions`) **I** is also bound to **Inventory**. If an
inventory screen ever reacts, move the fire key (it is `Keyboard.current.iKey` in `TreeFireToggle.Update`).

## 2. Where everything is

| What | Where |
|---|---|
| The component (all settings in its Inspector) | `TreeFireToggle` on the scene's **`Weather`** object. Code `Assets/_Project/Code/Runtime/DevTools/TreeFireToggle.cs` |
| The effect | `Assets/_Project/Prefabs/VFX/TreeFire_FX.prefab`: 4 particle systems (Smoke, LogFire, Fire, glow), one point Light with the vendor `LightFlicker`, one AudioSource ("Big Fire") |
| Vendor files | `Assets/ThirdParty/AST-015/` (git-ignored, like all ThirdParty). The Burning Tree prefab, its flame materials and textures, the Shimmer shader graph, `ScotsPine.fbx` + pine materials and `Big Fire.wav` were copied from the Playground project on 2026-09-30 with their `.meta` (GUIDs kept). `Fire Small URP` and its files were already there |
| Session log | Every `[PERF]` line ends with `treeFires N fireLights M` (fires burning, fire point lights on). Switches log `[MRM-86] Tree fires ON/OFF: ...` and the first build logs `[MRM-86] Tree fires built: ...` |

**Which trees count:** every Gaia-spawned vegetation instance (children of each `Gaia Game Object Spawns` container)
whose name has no "Rock" and whose renderers reach at least `minTreeHeight` (3 m) above its base: **8,128** of the
scene's 8,786 instances (stumps, logs, bushes, rocks are skipped). Each fire is scaled by
`tree height / referenceTreeHeight` (12 m, the vendor Scots pine). Tree heights run from 3 m to 41 m.

## 3. How `TreeFire_FX` differs from the vendor prefab

| Change | Why |
|---|---|
| Tree mesh (MeshRenderer + MeshFilter on the root) removed; root at origin, no rotation, scale 1 | We burn our own trees |
| Particle **Scaling Mode: Shape to Hierarchy** on all 4 systems | So the flames and smoke grow with the tree (Shape mode only stretches the emitter, flames stay 12 m-tree sized) |
| **Audio 2D to 3D** (spatial blend 1, linear rolloff, 3 to 50 m) | The vendor sound is 2D: every fire would play at full volume everywhere |

Vendor values kept: Point light, range 30, intensity 0.89, colour (1, 0.545, 0.25), no shadows; LightFlicker amount
0.3, speed 8, position wobble 0.1; particle systems loop 5 s with prewarm, simulation space Local, max 1,000 particles
each, lifetimes up to 6 s (smoke). Flames/glow use URP Particles/Unlit, smoke uses Particles/**Simple Lit** (lit by
every light, including the fire lights).

## 4. History (newest first)

### v2.1, 2026-09-30: fire light values in the weather profiles

**Why:** Carlos wanted to tune this fire's light per weather.

**What changed:** every weather profile has a new special light source **Tree Fires** (Override, colour, intensity,
range, shadows, shadow strength), on the `Weather` control board too. Override off (the default in all 27 profiles) =
the effect's own light: colour (1, 0.545, 0.25), intensity 0.89, range 30, no shadows. Override on = the pool lights
use the weather's values, blended during a weather transition. `flickerAmount` is added on top of Intensity; flicker,
fade and the light counts stay on `TreeFireToggle`. Code: `WeatherProfile.TreeFires`, `WorldLightSettings.Lerp`,
`WeatherSystem.TryGetTreeFireLights`, read every frame in `TreeFireToggle.UpdateLights`. Details in
`weather-profiles.md` (Session 2026-09-30).

**Verified:** Play Mode, board override set to green / intensity 3 / range 12: the fire lights took colour and range at
once and the intensity was fading in. Shadows on these lights would cost one shadow map per lit fire (up to
`maxLights`): measure before using them.

### v2, 2026-09-30: a pool of fires that follows the player

**Why:** v1 ran under 20 fps. Carlos asked for fire only on visible trees, or trees within a radius.

**What it does now** (`TreeFireToggle`, strategy described in its class comment):
- The first press only **reads the trees** (position + scale, 8,128) and builds a **pool** of `maxFires x 1.5` fires
  (90) plus `maxLights` lights, all off. No per-tree objects.
- Every `reassignSeconds` (0.25 s) the trees within `radius` (80 m) of the **camera** are scored by distance;
  a tree **behind** the camera counts as `behindWeight` (1.5) times farther; a tree **already burning** counts as
  `stickiness` (0.85) of its distance, so fires do not hop between equally distant trees. The `maxFires` (60) best get
  a fire.
- A fire leaving a tree **stops emitting and burns out** naturally (up to 6 s, then it is free again). A new fire
  starts from nothing and grows: **prewarm is off** on pool fires, so there is no hitch and no pop.
- **Lights:** a separate pool of `maxLights` (10) real point lights goes to the nearest burning fires. They fade in
  and out over `lightFadeSeconds` (1 s) and flicker like the vendor `LightFlicker` (`flickerAmount` 0.3,
  `flickerSpeed` 8, `flickerWobble` 0.1). The vendor light and its `LightFlicker` are removed from pool fires,
  because `LightFlicker` caches its world position in `Start` and would pin the light where the fire was first placed.
- **Sound:** only the `maxSounds` (6) nearest fires play.
- **Off** is instant: everything is hidden and reset.
- "Visible trees only" was not used as the rule: a fire only on what the camera sees starts every time you turn around.
  The camera direction is a weight instead (`behindWeight`). `behindWeight` very high approaches "visible only".

**Settings (Inspector, `TreeFireToggle` on `Weather`):** Effect: `firePrefab`, `referenceTreeHeight` 12,
`minTreeHeight` 3 (first press). Culling: `radius` 80, `maxFires` 60 (first press), `reassignSeconds` 0.25,
`behindWeight` 1.5, `stickiness` 0.85. Lights: `maxLights` 10 (first press), `lightFadeSeconds` 1, `flickerAmount`
0.3, `flickerSpeed` 8, `flickerWobble` 0.1. Sound: `maxSounds` 6. "First press" settings are read when the pool is
built: change them before pressing I, or restart Play. Read-only: `treesKnown`, `firesBurning`, `lightsOn`,
`soundsOn`, `lastReassignMs`.

**Verified in the editor (Play Mode, by reading values back):** 8,128 trees known; 60 fires burning, 10 lights on,
6 sounds; the nearest fire was about 10 m from the camera at scale 0.96, another at scale 2.40; lights flicker (read
1.14 against a base of 0.89), sit at the fire's light height and carry no LightFlicker; a reassignment pass costs
**0.13 ms**. Not yet judged by eye or measured in a build.

**Measured (Carlos, 2026-09-30, screenshot):** **174 fps / 5.7 ms**, weather "Story 6 · Fountain", enemies hidden
(O), infinite stamina and health regen on, fires on, standing in the circuit looking at burning trees (v1 at the same
weather: 6 fps / 178 ms and 13 fps / 77 ms). Build or editor not recorded. "Much better." The fire still reads as a
forest fire from inside it.

### v1, 2026-09-30: one fire on every tree

**What it did:** the first press instantiated `TreeFire_FX` on all 8,128 trees: 32,512 particle systems, 8,128 point
lights (each with a `LightFlicker` Update), 8,128 sounds. Built in 1.7 to 2.0 s; switching on took 3.8 s more (every
particle system prewarms 5 s). Options were lights on/off, sound on/off, a distance limit, all read at the first press.

**Found while testing:**
- The vendor sound is 2D (fixed in `TreeFire_FX`, section 3).
- 8,128 sounds overflowed Unity's virtual voices ("Ran out of virtual channels. Audio clip "Big Fire" will not be
  played", thousands of lines). Fixed by giving only fires within 60 m (126) a sound.
- "Ran out of Graphics Ring Buffer space" once, with everything on.

**Measured (Carlos, 2026-09-30):** under 20 fps. Screenshots, weather "Story 6 · Fountain", enemies hidden (O):
**6 fps / 178.2 ms** looking across the forest, **13 fps / 77.3 ms** looking up into the canopy. Whether it was a
build or the editor was not recorded. The fire looked right ("it's working").

**Why it was slow (reasoning, not measured):** 8,128 real point lights to cull and sort every frame, each visible one
lighting the fog (HAZE, Forward+), the lit smoke and every surface; 32,512 particle systems, many simulating
off-screen, with large overlapping transparent sprites (overdraw); 8,128 `LightFlicker` Updates.

## 5. Open questions and next steps

- Tune `maxFires` / `maxLights` / `radius` by eye, and the Tree Fires light per weather (night weathers first).
- Record whether measurements are from a build or the editor.
- Test with **lights off** (`maxLights` 0) to see how much of the cost is the lights (H: most of it).
- **Culling asset** (Carlos, 2026-09-30: may try a new culling asset for performance): if one replaces this pool's
  distance scoring, record it here as v3 with before/after numbers at the same spot and weather.
- Next levers if needed: drop or thin the Smoke system (large lit sprites), lower particle counts, a cheaper glow for
  distant fires.
- Fog: the fires light the HAZE fog through Forward+. When the fog changes (tuning, or Volumetric Fog & Mist 2), record
  how the fires look and cost here.
- **New fog asset** (Carlos may try Volumetric Fog & Mist 2): record how the fires look and cost under it here.
