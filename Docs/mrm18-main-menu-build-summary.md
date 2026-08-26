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
