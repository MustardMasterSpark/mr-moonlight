# MRM-18 — Main menu scene: build summary

Built 2026-08-26 on branch `mrm-18`. Functional layer only, per the issue — no styling, no logo,
no background staging (Carlos's later pass).

## Code (Assets/_Project/Code/Runtime/)

- `Data/Difficulty.cs` — `Conformist` / `Punk` enum, canonical names per `Docs/glossary.md`.
- `Data/GameSettings.cs` — static, PlayerPrefs-backed: `Difficulty`, `MasterVolume`,
  `VoicesVolume`, `SFXVolume` (0-1 linear). Survives the scene load into the demo scene without a
  DontDestroyOnLoad object or a second singleton.
- `Audio/AudioMixerVolume.cs` — converts a slider's 0-1 linear value to the mixer's decibel scale.
- `UI/FadeOverlay.cs` — the shared full-screen black fade (opening reveal, Start's fade-to-black,
  Quit's fade-to-black).
- `UI/SettingsPanel.cs` — difficulty ToggleGroup + three volume sliders, writes through to
  `GameSettings` and the mixer live.
- `UI/CreditsController.cs` — scrolls the placeholder Lorem Ipsum, skips on any click/key/gamepad
  button (`InputSystem.onAnyButtonPress`, same idiom as `InputDebugOverlay`), blocks raycasts to
  the menu underneath for its entire visible duration (including mid-fade).
- `UI/MainMenuController.cs` — orchestrates all four buttons and the opening reveal.
- `UI/DifficultyDebugOverlay.cs` — OnGUI readout of `GameSettings.Difficulty`, dropped into
  `Island.unity` as the acceptance criterion's "readable in the game scene" proof. No
  difficulty-scaling systems exist yet for it to drive — that's later work, not this issue's.
- `MoonlightTunables` — new "Main Menu — MRM-18" section: `MenuOpeningFadeDuration`,
  `MenuTransitionFadeDuration`, `CreditsScrollSpeed`, `DefaultMasterVolume`/`DefaultVoicesVolume`/
  `DefaultSFXVolume`, `MixerMuteDecibels`.

## Scene (built live via UnityMCP, with permission)

`Assets/_Project/Scenes/MainMenu.unity` — Main Camera + AudioListener, Directional Light,
EventSystem (`InputSystemUIInputModule`, default actions assigned), Canvas (Screen Space Overlay,
CanvasScaler Scale With Screen Size, 1920×1080 reference, match 0.5).

Canvas children:
- **FadeOverlay** — full-screen black Image + CanvasGroup + `FadeOverlay`.
- **MainButtons** — CanvasGroup, "MR. MOONLIGHT" placeholder title (TMP text, no logo art), four
  buttons (Start/Settings/Credits/Quit) built via `TMP_DefaultControls.CreateButton` so labels are
  native TextMeshProUGUI, not a legacy-Text swap.
- **Settings** — CanvasGroup (alpha 0 at rest), title, a `ToggleGroup` holding Conformist/Punk
  toggles (Punk on by default), three sliders (Master/Voices/SFX, built via
  `UnityEngine.UI.DefaultControls.CreateSlider`), a Back button.
- **Credits** — CanvasGroup + opaque black Image background, a masked Viewport containing a TMP
  `Content` text (a long placeholder Lorem Ipsum, `ContentSizeFitter` vertical) that
  `CreditsController` scrolls.
- **MainMenuController** — the orchestrator, plus the menu's `AudioSource` (routed to the mixer's
  Master group).

All private `[SerializeField]` references are wired (verified by reading the component data back
after building, not assumed) and all five button `OnClick()` events are hooked to
`MainMenuController`'s public methods via `UnityEventTools.AddPersistentListener` — same
persistent-listener mechanism a manual Inspector drag produces.

`Assets/_Project/Scenes/Island.unity` — added one new GameObject, `DifficultyDebugOverlay`, no
other changes.

**Build Settings** — now lists `MainMenu.unity` (index 0) then `Island.unity` (index 1), both
enabled. `Sandbox.unity` stays excluded, per `Docs/unity-conventions.md`.

## AudioMixer

`Assets/_Project/Audio/MoonlightMixer.mixer` — Master group with two children, Voices and SFX.
Three exposed parameters: `MasterVolume`, `VoicesVolume`, `SFXVolume` (one per group's volume,
matching the strings `SettingsPanel`/`AudioMixerVolume` call `AudioMixer.SetFloat` with).

Built via reflection against `UnityEditor.Audio.AudioMixerController` — that type and its group
creation/exposed-parameter APIs are Editor-internal, there is no public scripting entry point for
"create a mixer with groups and exposed params" the way there is for most other asset types.
Structurally verified (exposed parameter GUIDs read back correctly, matching each group's own
`GetGUIDForVolume()`); functionally verified live in Play Mode — `AudioMixer.SetFloat("MasterVolume", ...)`
returns `true` and reads back correctly once actually playing. (It reliably returns `false` outside
Play Mode — that's Unity's own runtime-only resolution of exposed parameters, not a defect in this
mixer.)

## Stale-doc corrections applied (per CLAUDE.md, the issue wins)

- **Reference resolution:** the issue has an old comment demanding 960×540 (WebGL-era, tied to
  itch.io's embedded-page framing). Built against **1920×1080** instead, per CLAUDE.md's platform
  switch and `Docs/pc-build-target.md`.
- **Loading-screen / "no percentage bar" constraint:** the issue's other comment is about itch.io
  serving the WebGL build without a `Content-Length` header, which made real load progress
  impossible to compute. That's a browser-streaming problem; a Windows download has no equivalent,
  so no loading screen or additive-load progress UI was built. Start simply fades to black then
  calls `SceneManager.LoadScene`.
- **Quit:** real `Application.Quit()` (falls back to `EditorApplication.isPlaying = false` in the
  Editor for convenience) — no `#if UNITY_WEBGL` hiding, per the issue's own superseding note.

## Pre-menu splash cards (added 2026-08-26, Carlos's request)

Two black-screen title cards now play before the opening reveal, inside the same `MainMenu` scene:
studio name, then a disclaimer, each fading its text in, holding, then fading out
(`SplashCardFadeDuration`/`SplashCardHoldDuration` in `MoonlightTunables`). New
`UI/SplashSequence.cs`, added as a component on `FadeOverlay` itself with two child CanvasGroups
(`StudioNameCard`, `DisclaimerCard`) riding on top of its black background. Text is placeholder —
`[STUDIO NAME]` and a bracketed disclaimer paragraph — for Carlos to edit by hand.
`MainMenuController.Start()` now runs the splash sequence first and only starts the existing
opening reveal once it finishes.

**Bug fix found and fixed while wiring this in:** `FadeOverlay` was the *first* child under Canvas,
which in Unity UI means it rendered **behind** MainButtons/Settings/Credits, not in front of them —
the opening black screen was never actually covering the menu buttons. Moved it to the last sibling
(topmost). Unrelated to the splash request itself, but the splash cards would have inherited the
same bug since they're children of FadeOverlay.

## Follow-ups from Carlos's 2026-08-26 review

- **Scene naming — resolved, no code change needed.** `Island` stays the canonical asset/branch
  name; when Carlos says "Demo" out loud he means Island. Doc/reality mismatch in
  `Docs/unity-conventions.md`'s scene table is just stale wording, not a discrepancy to fix.
- **Start no longer freezes on load.** `MainMenuController.RunStartGame()` now kicks off
  `SceneManager.LoadSceneAsync(demoSceneName)` with `allowSceneActivation = false` at the same time
  the fade-to-black starts, and only flips activation on once both the fade is done and
  `loadOp.progress` hits Unity's 0.9 "ready" ceiling. Previously it called the synchronous
  `SceneManager.LoadScene`, which froze the whole app for the load duration regardless of the black
  screen.
- **Difficulty selection is a placeholder, by design.** `GameSettings.Difficulty` persists the pick
  and `DifficultyDebugOverlay` proves it reaches the demo scene, but nothing reads it yet to affect
  gameplay (health, enemy behavior, spawn rates, etc.) — no difficulty-scaling systems exist in the
  project yet for it to drive. That's later work, not something missing from this issue.

## Known gaps / things to flag for Carlos

- **Full interactive verification still needs a hands-on pass or a real build.** Confirmed via
  UnityMCP: compiles clean, the scene hierarchy and every serialized reference read back exactly as
  wired, and the mixer's `SetFloat` genuinely works once in Play Mode. What I could **not** verify
  headlessly is animation over time — the Editor doesn't tick `Update()`/coroutines while its
  window is unfocused (the same limitation already noted in project memory for screenshots/
  UnityStats), so I couldn't watch the opening fade actually complete, click through Start/Settings/
  Credits, or watch the credits scroll from this session. Worth a quick play-mode pass on your end
  before calling this done, or I can do a full Windows build and launch it if you'd rather I verify
  that way.
- **"Missing script" console warnings during Island.unity load/play are pre-existing**, not
  something this change introduced — matches `Docs/pc-build-target.md` §7's documented HAZE/Retro
  Shaders Pro absence on a machine that hasn't re-downloaded those two ThirdParty packages.
- Menu music `AudioSource` has no clip assigned yet (no menu theme exists) — silently plays nothing,
  by design (`PlayOneShot`/`Play()` on a null clip no-ops rather than erroring).

## 2026-09-06/07 session — blood pool water, SSRR removal, Crest reflections, rain ripple port

Backdrop water under the intro feather (`CrestWater_BloodPool`) went through two iterations this
session: a hand-authored Fresnel plane/shader first, then — per Carlos's own follow-up call — real
**Crest 5 water** (matching the rest of the project) with **Shiny SSRR** reflections layered on
top. Both were gotten genuinely working and pixel-diff-verified. Carlos then reported the SSRR/Crest
combination looked wrong in practice and asked for four things, all done this session:

1. **ShinySSRR removed entirely from Mr. Moonlight** — the renderer feature off `PC_Renderer.asset`,
   the `GlobalVolume_ShinySSRR` GameObject, `PP_ShinySSRR.asset`, both `ShinySSRR.Reflections` /
   `ShinySSRR.ShinyTransparentSupport` components off `CrestWater_BloodPool`, and the whole
   `Assets/ThirdParty/ShinySSRR/` folder. Confirmed zero remaining references, clean compile.
2. **Crest's own planar reflections maxed out** on `M_CrestBloodPool.mat` and the
   `CrestWater_BloodPool` → `WaterRenderer.Reflections` object: `_Crest_PlanarReflectionsEnabled`
   on (float **and** keyword — same two-part gotcha as the RetroLit properties), intensity 1,
   roughness 0, distortion 0.3, smoothness 0.98, reflection `Resolution` 256→1024,
   `Layers = -49` (everything except Water(4)/UI(5), so the water doesn't reflect itself or the
   Canvas). Verified genuinely live via the project's standard frozen-frame pixel-diff test (71%
   of pixels changed toggling the flag alone).
3. **Rain ripple replaced with the tuned Dynamic Radial Masks "Ripple" effect**, ported from the
   Playground project's `RippleTuner` presets (`duration=0.45, maxRadius=2.25, startIntensity=2.0,
   ringFrequency=18, lineThinness=6, waveTravel=15`) — used as-is, not retuned. New standalone
   pieces (not a Crest shader-graph edit, lower risk): `RippleOverlay.shader` (hand-authored HLSL,
   samples the ported `DynamicRadialMasks_Ripple_8_Advanced_Additive_ID1_Local` mask function) +
   `M_RippleOverlay.mat` on a `RippleOverlay` quad sized to match the water plane, driven by
   `RippleController` (`DRMController`) + `RippleLiveObjectsPool` (`DRMLiveObjectsPool`). Trigger
   is the package's own ready-made `DRMOnParticleCollision` on the **Light Rain** particle system —
   no custom trigger script needed. The old "cartoonish" `RainRipple` sub-emitter is **disabled**
   (not deleted); `RainBubble`/`RainSplash - 1`/`RainSplash - 2`/the rain drop emission itself are
   untouched.
   - **Bug found + fixed:** the rain's `ParticleSystem.collision.collidesWith` was `-1` (Everything),
     so most collisions landed on the terrain under the dock, not the water plane, wasting ripple
     pool slots. Set to `1 << 4` (Water layer only) — also correctly restricts the bubble/splash
     sub-emitters to water-only hits as a side effect. Re-verified live afterward: ripples now
     visibly cluster on the open water, screenshot on file
     (`Assets/Screenshots/screenshot-20260907-103038.png`).
   - **Deferred, per Carlos's own words, not started:** a second, different ripple for the sparrow
     feather's water impact. `FeatherLandingRipple.cs` still targets the now-disabled `RainRipple`
     GameObject — calling `.Emit()` on an inactive GameObject silently no-ops. This needs to be
     re-pointed at the new ripple system (or a dedicated `DRMLiveObject` preset) once Carlos
     specifies the feather-ripple's own values.
4. **Vendor-code fix (found during session wrap-up, not requested but necessary):** the ported
   Dynamic Radial Masks scripts/shaders were initially copied into `Assets/ThirdParty/`, which is
   **entirely git-ignored** (`.gitignore` line ~256) — meaning none of it would ever reach a commit.
   Relocated to `Assets/_Project/Code/Vendor/Dynamic Radial Masks/{Scripts,Shaders}` (tracked),
   matching the existing Burntwax/PolymindGames/etc. precedent for vendor script+shader logic.
   `RippleOverlay.shader`'s `#include` path updated to match. Verified after the move: no missing
   script references on `RippleController`/`RippleLiveObjectsPool`, shader still compiles, scene
   saved.

This session also carried forward earlier main-menu work (feather-fall intro sequence, Eagle/Sparrow
Feather decorative props) already reflected in the working tree — see `git status` on branch
`mrm-18` for the current full file list; that work predates this doc section and isn't re-described
here.

## 2026-09-07 afternoon/evening session — ripple tuner, blood rain, feather wobble, RetroLit wind, CRT

Picked up the same afternoon as the session above. Carlos was staging the scene directly (adding
trees/flowers, reporting what he saw) and asking for behavior/tooling on top of it.

1. **Diagnosed a false alarm, no fix needed.** Carlos reported the water "disappeared" and the
   plane looked "small." Crest's `WaterRenderer` builds no visible mesh in Edit Mode — only at
   runtime — so selecting `CrestWater_BloodPool` while stopped shows only its `BoxCollider` gizmo
   (30×30, sized to catch rain-particle collisions across a wide area, not the visual footprint).
   Confirmed via a Play Mode screenshot that the pool renders exactly as before.

2. **Ported `RippleTuner.cs`** from the Playground project into
   `Assets/_Project/Code/Vendor/Dynamic Radial Masks/Scripts/`, retargeted from Playground's demo
   trigger (`DRMOnMouseRaycast`) to this project's actual trigger (`DRMOnParticleCollision`).
   Attached as a component on the existing `Light Rain` object (not a new hierarchy entry) with
   six live sliders defaulted to the already-shipped values, so nothing changed until touched.

3. **Retinted the ripple** (`M_RippleOverlay.mat`'s `_RippleColor`, white → `(1, 0.12, 0.08, 1)`)
   and **the rain itself** (`Light Rain`/`RainBubble`/`RainSplash - 1`/`RainSplash - 2`'s
   `main.startColor`, all retargeted to `(1.0, 0.15, 0.12)` at each system's original alpha) —
   blood rain and blood ripples instead of white/water-colored ones. Verified via
   `ParticleSystem.GetParticles()` that live particles actually sample the new color.

4. **Fixed "only a few raindrops generate ripples."** Root cause: `DRMLiveObjectsPool.AddItem()`
   hard-caps concurrent ripples at `DRMController.count`, which was 8 — baked into the ported
   `.cginc`'s fixed array size (Amazing Assets' shader-per-count naming,
   `..._Ripple_8_Advanced_...`). Rain collision rate far exceeds 8 ripples per 0.45s lifetime.
   Hand-authored a new count=200 variant (`DynamicRadialMasks_Ripple_200_Advanced_Additive_ID1_Local.cginc`,
   same Vendor folder; the unused count=8 file was left in place) and bumped
   `RippleController`'s `DRMController.count` to 200 — the vendor's own Inspector-slider ceiling.
   Even 200 stays saturated at the current rain density (would need ~450+ for literally every
   drop, which risks real GPU/GC cost for diminishing visual return, so not pursued further), but
   it now reads as continuous, dense rain-dappled water instead of "just a few" rings.

5. **Added `FeatherWaterWobble.cs`** (`Code/Runtime/World/`) to both `Prop_EagleFeather` and
   `Prop_SparrowFeather` — a gentle two-axis rotational sway to sell "resting on water." Auto-finds
   a `FeatherFall` on the same object and stays inactive while it's falling (so it never fights the
   fall animation), capturing whatever pose the fall settles on as its new rest pose the instant it
   lands. The static Eagle feather (no `FeatherFall`) wobbles from frame one. Four sliders: Pitch
   Amplitude, Roll Amplitude, Wobble Speed, Randomize Phase.

6. **Built a reusable wind-sway feature for RetroLit vegetation** — explicitly *not*
   menu-specific, per Carlos's own instruction to keep it separate and documented for reuse on the
   Island later. Full design writeup: `Docs/retrolit-wind-sway.md`.
   - Relocated `RetroLit.shader` and its `.hlsl` includes from the git-ignored
     `Assets/ThirdParty/Retro Shaders Pro/Shaders/` into tracked
     `Assets/_Project/Code/Vendor/Retro Shaders Pro/Shaders/` (the shader's `CustomEditor` GUI
     script stayed in `ThirdParty/` — found by class name, not path, so it didn't need to move).
   - New `RetroWind.hlsl` holds the shared `ApplyMoonlightWind()` vertex-displacement function,
     called from the main forward pass **and** the shadow-caster/depth-only/depth-normals passes,
     so a swaying mesh's shadow and depth sway with it instead of staying static.
   - Per-material properties (`_WindEnabled` off by default — zero cost/visual change for every
     other RetroLit material in the game unless explicitly opted in): `_WindCategory` (0=Trees,
     1=Flowers), `_WindHeight` (local-space sway falloff), `_WindFlexibility` (per-species
     multiplier — the two "dead/burnt" tree species flex less).
   - `RetroLitWindController.cs` (`Code/Runtime/World/`) on a new `WindController` hierarchy
     object drives four globals every frame: Wind Direction, Wind Speed, Tree Intensity, Flower
     Intensity — the two intensity sliders Carlos asked for by name.
   - All 6 tree species and 3 flower species currently staged in the scene are opted in with
     per-species tuned height/flexibility. `AP_Flower_001_09` has no renderer yet (a staging
     placeholder) — nothing to enable wind on until it has a mesh.
   - Verified live: trunk held pixel-identical between two frames while the canopy visibly shifted.

7. **Added a CRT post-process filter scoped to `MainMenu` only.** New `GlobalVolume_CRT`
   (`Volume`, `isGlobal=true`) + `VP_MainMenuCRT.asset` profile
   (`Assets/_Project/Settings/`) — scanlines plus light chromatic aberration, barrel
   distortion/tracking left off. The renderer feature that runs it (`CRTEffect` on
   `PC_Renderer.asset`) was already enabled project-wide; this just gave it a Volume to read from,
   scoped to this one scene (`Island` and the shared `DefaultVolumeProfile` untouched). UI needed
   no masking work — both Canvases in this scene are Screen Space - Overlay, which Unity always
   composites after the camera and all post-processing finishes.
   - **Bug hit and fixed:** the first attempt at building the Volume Profile via script
     (`VolumeProfile.Add<T>()`) left the `CRTSettings` sub-object un-persisted — it needs an
     explicit `AssetDatabase.AddObjectToAsset(component, profile)` call before
     `CreateAsset`/`SaveAssets`, or it silently reads back as a fresh default (`enabled=false`) on
     next load. Caught by checking live values in Play Mode rather than trusting the write.

**Not hands-on verified:** never got a screenshot with the actual menu title/buttons visible
alongside the CRT effect — the intro's disclaimer/feather-fall/fade sequence runs longer than the
verification window, and force-activating the UI Canvas out of sequence just showed its own intro
black panel rather than the real menu. The Overlay-canvas exemption from CRT is a structural Unity
guarantee (not something that needs empirical proof), so this isn't treated as an open risk, but a
real playtest pass would close the loop.

**Incidental, not part of this issue's work:** `git status` shows a one-line diff in
`Packages/com.waveharmonic.crest/Runtime/Materials/Water Volume.mat` (Crest auto-recording an
invalid-shader-keyword cache entry from repeated Play Mode sessions this session). Harmless, not
an intentional edit, flagged for awareness before committing.

**Cross-reference found for the next ask (skybox + clouds + moon, not yet started):** MRM-47's own
issue text already rules on this — **Altos Volumetric Clouds is parked/rejected for the Island**
(it replaces `RenderSettings.skybox` and takes over ambient GI outright, colliding with
`SkyboxSwitcher`/`TimeManager`/the apocalyptic-red swap) **but explicitly adopted for the main
menu**, since this scene has none of those systems to collide with. MRM-47's progress log also
notes 6 skies already extracted from the owned AllSky 220 pack at
`Assets/_Project/Art/Environment/Skyboxes/` (`Skybox/Cubemap` materials, ready to assign) — built
for the Island, but the same pack/technique is available for a menu-specific sky too. Worth reading
MRM-47 in full before starting that work; see `Docs/new-asset-list.md` §36/§46 for the fuller
triage reasoning.
