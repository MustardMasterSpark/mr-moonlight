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

## 2026-09-07 late session — Crest reflections/Moon/clouds (see `mrm18-sonnet-prompt-2.txt`), then rain/ripple density tuning

Two sessions picked up back-to-back. The first (already committed as `7cc7899`) maxed out Crest's
planar reflections, replaced Altos with InfiniCloud's "Clouds Single Mesh" for the menu sky, and
built the Moon prop (live tint + glow aura) — see that commit's message for the full writeup, not
repeated here.

The second session attempted, then fully reverted, real Crest **Dynamic Waves** displacement for
rain impacts (layered on top of the existing DRM shader ripple, per Carlos's ask to try it) —
toggling `WaterRenderer.DynamicWavesLod.Enabled` via scripted Editor automation threw a
reproducible null-ref inside Crest's own code and, once it did succeed without error, broke the
water's reflection and left a shader glitch (see `[[mrm18_crest_dynamicwaves_reverted]]` in
memory). Rolled back via `git checkout` + a forced scene reload, confirmed byte-clean against the
last commit. **Not attempted again this session** — needs a human clicking the checkbox in an
already-focused, already-rendering Editor session, not further scripting. A camera-frustum-based
shrink of the oversized (4838-unit) InfiniCloud plane was tried in the same pass and also reverted
along with everything else, since it was bundled into the same rollback — worth revisiting on its
own later if build-size/draw-call headroom becomes a concern; the plane only needs to cover
roughly a 700×430-unit footprint (measured against the main camera's mirrored reflection frustum,
with margin), a ~10x reduction from its current size.

Carlos then reported (correctly) that the water still looked too cluttered with raindrops/ripples,
following up on the same saturation issue this doc already flagged in the 2026-09-07 afternoon
session above ("even 200 stays saturated at the current rain density"). This time it was actually
fixed:

- **Raindrop emission rate halved twice**: 1000 → 500 → 250/sec on the `Light Rain`
  `ParticleSystem` (`EmissionModule.rateOverTime`). The rain's collision-triggered sub-emitters
  (`RainBubble`, `RainSplash - 1`/`- 2`, the particle-based `RainRipple`) all scale proportionally
  since they fire per parent-collision-event, not on independent timers — confirmed via each
  sub-emitter's own `rateOverTime = 0`.
- **Found and fixed a real regression along the way, self-inflicted:** lowered
  `DRMController.count` from 200 to 100 to try to reduce ripple density directly — this silently
  killed every ripple. Root cause: Amazing Assets' DRM system bakes `count` into both the
  generated shader property names (`DynamicRadialMasks_Ripple_{count}_..._DATA1/2/3`, built as a
  plain string in `DRMUtility.GetMaterialPropertyName`) and the hand-written
  `RippleOverlay.shader`'s `#include`/function-call, which is fixed at compile time to the
  `_200_` variant. Only `count` values with a matching pre-generated `.cginc` (8, 64, 200 exist in
  `Code/Vendor/Dynamic Radial Masks/Shaders/`) can ever work, and using one other than 200 means
  also hand-editing `RippleOverlay.shader` to match. Reverted to 200. Full detail in memory
  (`[[mrm18_drm_ripple_count_baked_into_shader]]`) so this isn't rediscovered.
- **The actual fix for "still too many ripples" was ripple *lifetime*, not count or raindrop
  rate.** With `count=200` fixed and unable to move, the pool's turnover capacity is
  `count / duration` — at the previously-shipped `duration=1.26s` that's only ~159 ripples/sec,
  well under the raindrop collision rate even after halving it twice, so the pool stayed pinned
  at its 200-ripple cap regardless of raindrop rate (this is *why* the first halving alone didn't
  visibly reduce ripples — Carlos's own follow-up report caught exactly this). Carlos then
  hand-tuned `RippleTuner`'s sliders directly in Play Mode to their current shipped values —
  `duration=1.51, maxRadius=0.88, startIntensity=0.06, ringFrequency=15.4, lineThinness=11.84,
  waveTravel=19.6` — a longer-lived but far fainter/thinner ripple than before. Restored twice
  after being lost to Unity's Play-Mode-discards-script-edits behavior (once fully, once partially
  — the raindrop rate specifically needed a third re-application since it was edited mid-Play-
  session and silently reverted on stop). Final values confirmed saved and verified via git diff
  before this doc was written.
- **Ruled out as a cause, not a bug:** confirmed only one `DRMOnParticleCollision` exists in the
  whole scene, and the rain's own collision-triggered sub-emitters don't independently drive it,
  so the shader ripple system is genuinely 1:1 with actual raindrop/water collisions — no double-
  triggering. `Prop_SparrowFeather`'s `FeatherLandingRipple.cs` *does* independently fire a
  ripple (the small particle-based `RainRipple` effect, not the DRM shader ring) whenever the
  feather finishes a fall-and-land loop, unrelated to rain entirely — flagged to Carlos as a
  possible source of "ripples with no raindrop," left in place pending his call on whether to keep
  it (deliberate MRM-18 feature, not obviously broken).

Session closed on Carlos's approval of the current staging ("I think it looks pretty nice").
**Issue stays open (not closed)** — the next piece of MRM-18 scope is the title screen(s) and
initial disclaimers; see `Docs/mrm18-sonnet-prompt-4.txt` for that handoff.

## 2026-09-08 session — title sequence built from scratch (cross, logo, disclaimer, feather sync)

Carlos discarded the old placeholder splash cards entirely and specified a precise, music-anchored
replacement: a reference PowerPoint/screenshots for three screens (Orthodox cross + Greek text,
studio logo, two-part disclaimer) plus exact second-by-second breakpoints from a reference track
(`Rising_Storm_2_Layin_Low.wav`) telling exactly when each element appears, grows, and fades. Full
sequence built, tuned live with Carlos across several rounds, and approved ("Thank you, that
worked.").

**Assets imported** (`Assets/_Project/Art/UI/Fonts/`: Kurland, GutenbergTextura, NotoSerif,
GabrieleBandAah + generated TMP SDF Font Assets for each; `Assets/_Project/Art/UI/TitleCards/`:
T_Cross_ICXCNika, T_DivineRays, T_Beam, T_Dove sprites; `Assets/_Project/Audio/
MUS_RisingStorm2LayinLow.wav`, routed through the `Aud_Music` preset). Skipped importing
`hit-me-punk.02.ttf` (byte-identical to the already-present `HitMePunk.ttf`) and `Gothic Mother.ttf`
(present in the asset folder but unused by any instruction given — Carlos: "don't import anything
that we don't use right now").

**Bug found and fixed in the source audio file itself:** the WAV's RIFF/data chunk sizes were
unfinalized placeholders (`0xFFFFFFFF`), which Unity flatly refused to import ("Unspecified error").
Patched the two size fields directly in the project's copy (pure header metadata, zero audio
sample bytes touched, confirmed lossless) — Carlos's original file on his Desktop is untouched.

**New `TitleSequenceController.cs`** (`Code/Runtime/UI/`, replaces the deleted `SplashSequence.cs`)
drives everything off `AudioSource.time` on the music track directly — every breakpoint is a
literal second read from `MoonlightTunables` (`TitleBreakpointCross`, `TitleBreakpointLogo`,
`TitleBreakpointDisclaimer1`, `TitleBreakpointDisclaimer2`, `TitleBreakpointWorldReveal`, plus the
grow/hold/fade-window tunables), not a fixed animation timeline — re-timing anything is a Tunables
edit. Cross and logo cards share one `GrowThenFade` routine (appear instantly, grow from
`TitleElementStartScale` to full size, hold, fade out, timed to finish exactly on the next
breakpoint); the logo's "2026" text sits outside the scaling group so only its alpha animates, per
spec. The disclaimer's two paragraphs fade in independently and share one fade-out.

**Real scheduling conflict found and resolved, twice, with Carlos's direct input both times:**
the sparrow feather's fall must land exactly on `TitleBreakpointWorldReveal` (16.115s) but the
feather's own tuned fall duration (4.0s) didn't fit the original disclaimer timing Carlos gave
(paragraph 2 at 12.149s left only 3.966s total before landing — less than the fall alone needed).
First attempt shortened the fall to fit (wrong call - Carlos: "it looks very fast and bad, don't
change the time it has to fall"). Correct fix: moved `TitleBreakpointDisclaimer2` earlier instead
(12.149s → 10.315s), preserving the exact fall duration and landing time - see
`TitleSequenceController.ComputeFeatherStartSongTime()`, which is the single source of truth for
when the feather should start falling given the disclaimer's timing. **The feather now visibly
starts falling at exactly 12.115s of the song** — worth knowing if Carlos gives new breakpoints for
future disclaimer content, since anything after that timestamp needs to be fully cleared off screen
by then.

**Added `MoonlightTunables.TitleSequenceStartDelay`** (1.5s) - a fixed black-screen buffer before
the music/sequence starts at all, requested so Play Mode's own load/settle time doesn't eat into
the first second of music-anchored timing and desync everything Carlos sees from what the numbers
say should be happening.

**A real, deep rendering bug found and fixed - the most significant technical finding this
session.** The feather-only intro overlay (a separate camera rendering to a RenderTexture, composited
via a higher-sort-order Canvas so the feather stays visible while `fadeOverlay` covers everything
else) needs a transparent background so the world-reveal fade is visible through it without also
hiding/dimming the feather. **Confirmed empirically that URP does not output a usable alpha channel
for this camera's render, full stop** - read back individual pixels after rendering and got
`alpha=1.0` everywhere, including areas cleared to `(0,0,0,0)`. Tried and ruled out: disabling
post-processing, disabling HDR, adding a proper `UniversalAdditionalCameraData` (was missing
entirely), a freshly-created RenderTexture, and the URP pipeline asset's own `allowPostProcessAlphaOutput`
toggle - none of them changed the result. This directly explains BOTH previous bug reports, which
were two sides of the same broken-alpha coin: switching the overlay off immediately at landing
(so the fade dims the feather with everything else) vs. keeping it on through the whole fade (so
its opaque output blocks the entire reveal, then snaps to the final result when switched off).
**Fix:** since the feather is essentially static the instant it lands, capture ONE snapshot via
difference matting instead of relying on the broken continuous render - render the camera once
against pure black, once against pure white, and recover true per-pixel alpha from how the two
differ (`CaptureMattedFeatherSnapshot`/`ReadRenderTexturePixels` in `MainMenuController.cs`). Only
RGB needs to survive the render for this to work, which URP does correctly - alpha is reconstructed
entirely on the CPU side, sidestepping the broken channel rather than fighting it. The resulting
static `Texture2D` displays via ordinary UI alpha blending (works perfectly, same as every other
sprite in this menu), so the overlay can now safely stay active for the whole reveal fade with no
regression. Verified twice in Play Mode: snapshot captures a proper feather-shaped silhouette
(confirmed via opaque-pixel count during testing, since read on-screen), zero errors, feather stays
at full brightness throughout, world fades in smoothly, final state (buttons interactive, overlay
switched off) reached correctly both times. **If this class of bug resurfaces anywhere else in the
project** (any future "render a 3D object to a transparent texture for UI compositing" need), skip
straight to difference matting rather than re-diagnosing the URP alpha behavior - it's confirmed
broken in this project's exact URP/pipeline-asset configuration, at least for a Base-type camera
manually rendered via `Camera.Render()`.

Session closed on Carlos's confirmation the fix worked. **Issue stays open** - scope covered so far
is the intro/title sequence up through the feather landing and world reveal; the actual main menu
buttons/font/styling rework Carlos mentioned wanting eventually ("we will change the main menu, the
font, and the buttons") has not been started. See `Docs/mrm18-sonnet-prompt-5.txt` for the handoff.

## 2026-09-08 session (continued) - screens 1/2/3 finished, recurring TMP font-atlas bug found and
## fixed three times, feather intro static/fringe polish

Picked up from `Docs/mrm18-sonnet-prompt-6.txt`. Carlos worked screen-by-screen in the Unity Editor
(selecting/resizing/repositioning elements live), calling out what to fix after each pass. **All
three title cards (cross, logo, disclaimer) are now considered done** - next session moves on to
the actual main menu (title, buttons) per Carlos: "We are done with the initial cards."

**Screen 1 (Cross):** Changed from grow-then-fade to static-then-fade at Carlos's request - no more
scale animation, and visible from the moment the scene loads (through the pre-music black-screen
delay too, via a new `TitleSequenceController.Awake()`), not just at breakpoint 0. New `HoldThenFade`
coroutine added; `crossScaleRoot` field removed (no longer needed). Carlos then manually resized/
positioned the cross + Greek text elements in-Editor (including a temporary 1.8x scale on a
`ScaleRoot` wrapper); baked that back into clean absolute per-element sizes at his request
(`ScaleRoot` scale normalized to 1, `CrossImage`/`GreekText` sized to match exactly).

**Screen 2 (Logo):** Biggest chunk of the session.
- Replaced the separate `DivineRays`/`Beam`/`Dove` images with one combined art asset Carlos
  supplied (`T_RaysBeamDove.png`, imported to `Assets/_Project/Art/UI/TitleCards/`) - first import
  attempt silently capped it to 512px on the long edge (project's default texture max size); fixed
  by explicitly setting `TextureImporter.maxTextureSize = 2048` before reimporting, confirmed at
  full native 2038x771.
- Carlos duplicated the "Master\nSpark" text object to isolate "Master" and "Spark" as separate
  objects; his Inspector text edits kept reverting (cause not confirmed - possibly Undo). Fixed by
  setting `.text` directly via script instead, then resized each box tight to its single word using
  `TMP_Text.GetPreferredValues(text, wideWidth, 0)` - **note:** `preferredWidth`/`preferredHeight`
  read directly off the component are unreliable right after a `sizeDelta` change (feed back off the
  *current* rect, producing wildly wrong results like a 373px height for one line) - always pass an
  explicit wide width constraint instead.
- Carlos resized/repositioned everything within `GrowingElements` (children's own `localScale`, plus
  shifted `GrowingElements` itself left) as a "this is the starting grow-in size" pass, then a second
  pass increasing `GrowingElements`' own scale as "this is the final grown size." Both baked clean:
  children's per-object scale multipliers folded into `sizeDelta`/`fontSize` (never leave a non-1
  `localScale` on a static element - see `Docs/unity-conventions.md`-style convention already used
  for screen 1), `GrowingElements` position zeroed to origin. Technique used both times: capture each
  child's **world** `transform.position` before changing the parent, do all the scale/position
  cleanup, then set `transform.position` back to the captured value - lets Unity solve the local
  offset math instead of hand-computing it, which is far less error-prone.
- New tunables registered from this: `TitleElementStartScale` changed from the old pre-bake 0.6 to
  1.0 (matches the now-baked "natural" children pose); new `TitleElementEndScale = 1.0972` (Carlos's
  subtle final-grown scale). Logo also gained a real fade-in (previously an instant alpha pop, "no
  fade-in" was literally the original MRM-18 spec) plus its own independent fade-out duration split
  out from the cross's shared one - new `TitleLogoFadeInDuration`/`TitleLogoFadeOutDuration` (0.4s
  each), so tuning one card's fade never silently retimes the other's.

**Recurring bug - TMP Dynamic font assets with a dead atlas (hit 3 times this session):**
`GutenbergTextura SDF`, `GabrieleBandAah SDF`, and `NotoSerif SDF` all turned out to be freshly
created **Dynamic**-population font assets whose `m_CharacterTable`/`m_GlyphTable` are empty on disk
and whose runtime glyph generation was silently broken - two different failure faces of the same
root cause:
- **Total lookup failure** (Gutenberg, on "Master\nSpark"): every character comes back with zero
  glyph data, so TMP renders its "missing glyph" placeholder - a solid box - for every character.
  This is what "boxes of white" looks like.
- **Metrics resolve, rasterization doesn't** (Gabriele, on "studios"/"2026"; NotoSerif, on both
  disclaimer paragraphs): TMP successfully finds real glyph metrics and reserves a real atlas rect,
  so the mesh/UVs/layout are all correct - but the atlas texture pixels at that rect are 100% alpha
  0. Confirmed by directly sampling `((Texture2D)font.atlasTexture).GetPixels(...)` at the glyph's
  rect - metrics alone are **not** proof a font is working, sample actual pixel alpha. Text is
  simply invisible, occupying its layout space normally.

**Fix, both cases, all three fonts:** reimport the source `.ttf` (`manage_asset` action=`import`),
then call `TMP_FontAsset.ClearFontAssetData(true)` on the `.asset` and save - forces a full clean
regeneration against the freshly-reimported font. Verified each time by re-sampling atlas pixel
alpha before/after (0 -> ~0.77-0.82) and a Scene-view screenshot. **If any other TMP font asset in
this project hasn't been proven to actually render text yet (especially ones for the upcoming main
menu buttons/title work), check it the same way before trusting it** - this looks like a systemic
issue with how these font assets got created, not a one-off.

**Screen 3 (Disclaimer):** Originally two paragraphs on staggered breakpoints
(`TitleBreakpointDisclaimer1`/`2` + a hold + fade-out); Carlos simplified this twice in the same
session:
1. First pass: keep `TitleBreakpointDisclaimer1` (8.114s) as the appear time, but retime so the
   fade-out starts at 11.5s and finishes at exactly 12.0s (was 11.315s/11.815s) - done by adjusting
   `DisclaimerHoldAfterParagraph2`, and `TitleGapBeforeFeatherStarts` lowered 0.3s->0.115s so the
   feather's own start (12.115s) and `TitleBreakpointWorldReveal` (16.115s, a fixed musical beat)
   stayed untouched, same "retime the text, not the feather" precedent already used once on
   `TitleBreakpointDisclaimer2`.
2. Second pass: dropped the two-paragraph staggering entirely - both paragraphs now fade in/out
   together as one slide. `TitleBreakpointDisclaimer2` and `DisclaimerHoldAfterParagraph2` removed;
   new `TitleBreakpointDisclaimerFadeOutStart = 11.5f` replaces the derived chain.
   `ComputeFeatherStartSongTime` simplified to match - feather timing (12.115s/16.115s) unaffected
   either time.
- What looked like "text not showing" during this retiming was actually the `NotoSerif SDF` dead-
  atlas bug above, not a timing or overlap issue - found by checking proactively once the same
  symptom showed up a third time.
- Per-word rich-text coloring added at Carlos's request - plain TMP `<color=#RRGGBB>...</color>`
  tags (already supported, `richText` was already on, no extra package needed): "Christianity" ->
  gold `#FFD700`, "Player discretion is advised" -> red `#FF0000`. Carlos asked whether a Package
  Manager asset he has (likely Text Animator, per the 2026-09-02 import log) was needed for this -
  it isn't; that asset is for animated text effects, overkill for static per-word color.

**Feather intro polish (not the title cards, but touched this session):** two follow-ups to the
already-closed difference-matting fix above, from Carlos noticing the feather "is static for a
second" after landing with "red pixels around" it before it starts moving:
- The wobble script (`FeatherWaterWobble`) was already starting immediately on landing (confirmed in
  code) - the *static matted snapshot* overlay was just hiding the already-moving live feather for
  the entire 1.5s reveal fade (only switched off once 100% complete). New
  `FadeOverlay.Alpha` getter + new tunable `FeatherOverlaySwitchAlphaThreshold = 0.35f`: the overlay
  now switches to the live feather once the world fade is ~65% through instead of waiting for 100%,
  shortening the static window substantially. Deliberately not switching at 0% - that exact approach
  was already tried and reverted earlier (see the flicker-investigation doc above).
- Red fringe pixels: a known difference-matting failure mode - un-premultiplying color at very low
  recovered alpha amplifies ordinary render noise into stray saturated pixels. Old cutoff (`0.003f`)
  was far too permissive; new tunable `FeatherMatteAlphaCutoff = 0.05f` plus explicit per-channel
  clamping in `CaptureMattedFeatherSnapshot`.
- **Not yet verified live** - this only manifests in Play Mode during the actual feather-fall/reveal,
  Carlos still needs to test it.

**Other traps confirmed/reconfirmed this session:**
- `manage_camera` screenshot action, when it auto-picks a specific camera, renders direct-camera
  (excludes Screen Space - Overlay canvases entirely, so the whole title-sequence UI comes back
  blank). Use `capture_source: "scene_view"` for anything with Overlay UI in it.
- `CrestWater_BloodPool`'s `WaterRenderer._FollowSceneCamera` (Editor-only convenience feature) can
  make the water auto-scale to its max LOD scale and spam "Screen position out of view frustum"
  console errors when the Scene view camera is parked far from the water (e.g. up near a world-space
  UI canvas while editing it) - confirmed via source (`#if UNITY_EDITOR` + `!Application.isPlaying`
  guards in `WaterRenderer.cs`) that this is 100% Editor-only and cannot affect Play Mode or a build.
  Carlos chose to live with it rather than disable `Follow Scene Camera` - noted here so it isn't
  re-investigated as a real bug later.

See `Docs/mrm18-sonnet-prompt-7.txt` for the handoff into main menu (title/buttons) work.
