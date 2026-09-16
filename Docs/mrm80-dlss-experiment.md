# MRM-80 — DLSS experiment (2026-09-15) — REVERTED 2026-09-16

**Status: abandoned and fully reverted.** Carlos tried `danielblnc/DLSS-NR-on-AMD`'s setup tool
against Forza Horizon 5 (a known FSR-capable title, per the recommended test order below) instead
of Cyberpunk 2077. The setup itself ran fine, but Forza Horizon 5 then refused to launch through
Steam at all (`Steamwebhelper no responde`, Steam showing offline) — repeated restarts didn't clear
it. Carlos judged this an external Steam/game blocker unrelated to Mr. Moonlight, decided not to
keep pursuing it, and asked for everything below to be rolled back. If he wants to see real DLSS
output later he'll test on a friend's Nvidia machine instead. Linear issue closed as Canceled, with
the rollback summary in its final comment.

**Everything described below has been removed from the project** — the three `Packages/top.kuanmi.*`
folders are deleted, the renderer feature and both `DlssNrVolume` overrides (`VP_MainMenuCRT.asset`
**and** `DefaultVolumeProfile.asset` — the latter wasn't documented below originally, found during
rollback) are gone, `GameSettings.cs`/`SettingsPanel.cs` are back to FSR-only (MRM-78 untouched),
`GraphicsVendorInfo.cs` and the `DlssToggle` scene GameObject are deleted, and `.gitignore`/
`Docs/external-assets.md` no longer mention it. Verified via a clean project recompile and raw file
reads on every touched asset (not just in-Editor state, which can lag — see
`unity_volumeprofile_subasset_persistence_trap` in memory).

This file is kept only as a historical record of what was tried and why — nothing below reflects
current project state.

---

## Original write-up (historical)

Carlos's own curiosity project: can a Unity build ever show real DLSS output on his AMD card (RX
9070 XT)? Tracked in Linear as MRM-80, status Backlog — this is hands-on experimentation, not a
demo deliverable. **Everything below is designed to be removed in a few minutes.** No part of it is
required for M1/M2/the Kickstarter build.

## Two unrelated external tools — do not conflate them

Research this session found these are separate codebases with separate hardware requirements, easy
to confuse because both consume the same `nvngx_dlssnr.dll` file and both showed up while looking
into "DLSS on AMD":

| Tool | What it is | Hardware | Verdict |
|---|---|---|---|
| **Deep Fried Chicken** (`jlrouzies-fr/DLSS5-Feeder`) | ReShade add-on that manufactures a fake DLSS request for any game, then runs the real neural pass on it | **Nvidia GPUs only** — its own docs check for Nvidia-specific GL/Vulkan extensions and refuse to run without them | Dead end on Carlos's machine. Only useful if a friend with an Nvidia card wants to try it. |
| **`danielblnc/DLSS-NR-on-AMD`** | From-scratch AMD reimplementation of the DLSS neural runtime (HIP kernels) | **RX 9000 series confirmed** (tested on 9070 XT — Carlos's card); RX 7000 "should work, needs testing" | The only tool with any real shot. Hooks a game's **AMD FSR 3/4** call specifically (its docs: "enable FSR 3/4 in-game") — Windows 11, Adrenalin ≥26.1.1, anti-cheat off, needs `nvngx_dlssnr.dll` v310.8.0.0 obtained separately. |

Recommended test order (told to Carlos, his call on timing): prove the AMD tool works at all on a
real supported title first (its own example is Cyberpunk 2077) before pointing it at Mr. Moonlight
— isolates "is my setup right" from "does our build give it anything to hook."

## What's wired into the project

**Package (local-only, git-ignored, NOT shipped):** `Kuan-Mi/UnityDLSSNR`
(github.com/Kuan-Mi/UnityDLSSNR, no version tag used — pulled from `main` + the `v1.0.3` native
release on 2026-09-15). **This repo has no LICENSE file at all** — legally all-rights-reserved by
default. It must never be committed or appear in a build. Three embedded UPM packages, dropped
directly into `Packages/` (same mechanism as Flora/Crest — no `manifest.json` entry needed, Unity
auto-discovers them):

- `Packages/top.kuanmi.unityrhi.native/` — 119 MB, prebuilt Windows x64 DLLs (D3D12 RHI, NGX/DLSS
  loaders). Ships with `nvngx_dlss.dll`, `nvngx_dlssd.dll`, `nvngx_dlssg.dll` but only a
  `.meta`-only placeholder for `nvngx_dlssnr.dll` — **that one file has to come from Carlos**, same
  DLL the DLSS-NR-on-AMD tool needs.
- `Packages/top.kuanmi.unityrhi/` — the C# RHI wrapper.
- `Packages/top.kuanmi.dlss.urp/` — the URP-side DLSS Super Resolution / Frame Generation / Neural
  Rendering integration. We only use **Neural Rendering** (matches "Deep Fried Chicken"'s own
  "Neural passes" terminology) — Super Resolution needs `ENABLE_UPSCALER_FRAMEWORK` and changes to
  the URP asset's upscaling filter, which we deliberately did **not** touch, since FSR already owns
  that field (MRM-78) and NR doesn't need it.

All three are listed in `.gitignore` (search "MRM-80") and in `Docs/external-assets.md`'s load-
bearing table.

**Renderer feature:** `Assets/_Project/Settings/PC_Renderer.asset` gained a fourth renderer feature,
`UnityRhi.Dlss.Urp.DlssNrRenderFeature` ("DLSS Neural Rendering"), `renderPassEvent =
BeforeRenderingPostProcessing` (the package's own recommended default — NR runs before the rest of
post, ahead of FSR). Added as a sub-asset via `AssetDatabase.AddObjectToAsset` + appending to
`rendererFeatures`, same technique as any scripted renderer-feature edit. It self-gates: if D3D12 or
the NGX/NR runtime isn't available it logs one warning (`[UnityRHI.DLSS-NR] Pass disabled:
D3D12/NR runtime unavailable...`) and does nothing — confirmed live, see Verification below.

**Volume override:** `Assets/_Project/Settings/VP_MainMenuCRT.asset` (the existing MainMenu global
volume profile, `GlobalVolume_CRT` GameObject) gained a `UnityRhi.Dlss.Urp.DlssNrVolume` override.
`active = true` (the override block participates), `enabled.overrideState = true`,
`enabled.value = false` (off by default). Only in the MainMenu scene's profile — the Island demo
scene has no equivalent override yet.

**Code (permanent, ships fine — this part has zero dependency on the gitignored package):**
- `Assets/_Project/Code/Runtime/Data/GameSettings.cs` — new `DlssEnabled` bool (PlayerPrefs-backed,
  default off, same pattern as `UpscalingEnabled`).
- `Assets/_Project/Code/Runtime/Data/GraphicsVendorInfo.cs` — new. `IsNvidia` / `IsAmd` from
  `SystemInfo.graphicsDeviceVendor`. **Not wired to anything** — Carlos explicitly asked to keep
  both the FSR and DLSS checkboxes manually toggleable regardless of detected vendor for now, so he
  can force DLSS on despite having an AMD card while testing the DLSS-NR-on-AMD tool. This helper
  exists for a later pass that hides/disables the wrong checkbox per GPU.
- `Assets/_Project/Code/Runtime/UI/SettingsPanel.cs` — new `dlssToggle` (Toggle) and
  `dlssVolumeProfile` (VolumeProfile, assigned to `VP_MainMenuCRT.asset`) serialized fields;
  `OnDlssToggled`/`OnUpscalingToggled` now cross-disable each other (checking one unchecks the
  other, both in code and in `GameSettings`); `ApplyDlss` reaches the `DlssNrVolume` override
  **by reflection** (`GetType().FullName == "UnityRhi.Dlss.Urp.DlssNrVolume"`), not a direct type
  reference — so if the gitignored package is ever deleted, this method just logs a warning and
  no-ops instead of breaking compilation for the whole project.

**Scene:** `MainMenu.unity`, `Settings` panel gained a `DlssToggle` GameObject (duplicated from
`UpscalingToggle`, repositioned to `anchoredPosition (0, -415)`, label "Enable DLSS (Nvidia only,
experimental)"), wired to `SettingsPanel.dlssToggle`. Same "deliberately unpolished, default Unity
Toggle look" as the rest of the MRM-78 Display group.

## Verification done this session

- Compiled clean — no errors from any of the three new packages or the code changes.
- Entered Play Mode in the Editor, toggled both checkboxes via script: confirmed mutual exclusion
  works both directions (turning FSR on turns DLSS off and vice versa, in both `GameSettings` and
  the UI).
- Confirmed the render feature actually activates end-to-end: enabling the DLSS toggle produced
  console log `[UnityRHI.DLSS-NR] Pass disabled: D3D12/NR runtime unavailable (init=0xBAD00001)` —
  expected and correct, since the Editor Game view runs D3D11 and there's no `nvngx_dlssnr.dll` yet.
  No exceptions, no crash — exactly the defensive behavior the package promises.
- **Trap hit and fixed:** Volume profile (ScriptableObject asset) edits made during Play Mode did
  **not** revert on Stop, unlike scene GameObject edits — the known exception documented in memory
  (`unity_playmode_edits_discarded_on_stop`). The test above left `VP_MainMenuCRT.asset`'s
  `DlssNrVolume.enabled.value` at `true`, and left `MrMoonlight.DlssEnabled`/`UpscalingEnabled`
  PlayerPrefs keys at the test's end-state. Both were manually reset to the correct off-by-default
  state and re-saved after the test.

## Session 2 (2026-09-15, continued) — real DLL in place, genuine progress on AMD

Carlos sourced `nvngx_dlssnr.dll` v310.8.0.0 from the RenoDX Discord's `#dlss5-downloads` channel
(exact version match to what DLSS-NR-on-AMD's docs specify) and dropped it into
`Packages/top.kuanmi.unityrhi.native/Plugins/x86_64/nvngx_dlssnr.dll`. Only that one file was
needed from the downloaded bundle — the sibling `nvngx_dlss.dll`/`nvngx_dlssd.dll` were already
bundled by Kuan-Mi's package, and the `sl.*.dll` Streamline files aren't used by this integration
(UnityRHI loads NGX directly, not through Streamline — that path is only relevant to the separate
ReShade/"Deep Fried Chicken" tool).

**Trap hit: the new DLL's auto-generated `.meta` defaulted to `CompatibleWithEditor = false`** (only
enabled for `StandaloneWindows64`), unlike its siblings which shipped proper `.meta` files. Fixed by
matching the sibling's `PluginImporter` settings exactly (`SetCompatibleWithAnyPlatform(false)`,
`SetCompatibleWithEditor(true)`, `EditorData["CPU"]="x86_64"`, `EditorData["OS"]="Windows"`,
`SetCompatibleWithPlatform(StandaloneWindows64, true)`). Native plugins only load once per Editor
process, so this required a full Editor restart to take effect — a plain reimport isn't enough.

**Result after restart — genuinely surprising:** `RhiCore.IsD3D12Active = True`,
`RhiCore.IsDlssNrAvailable = True`, `DlssNrInitResult = 0x00000001` (success) — Unity's own D3D12 is
now active on this machine (unclear why; wasn't a deliberate setting change) and NGX's *library-level*
init succeeds on the RX 9070 XT with no AMD proxy tool involved at all. This is further than the
research predicted. Enabling the checkbox in Play Mode confirmed the pass actually starts recording
(`[UnityRHI.DLSS-NR] NR at 1225x689 (native / render resolution)`), but the **per-camera context
creation** — a separate, later call than the library init — fails cleanly:
`System.InvalidOperationException: UnityRHI could not create a DLSS Neural Rendering context.` No
crash, no hang, matches the package's defensive design. This is the more specific, more informative
failure point than session 1's blanket "runtime unavailable," and is likely exactly the call the
external `DLSS-NR-on-AMD` tool's HIP reimplementation would need to intercept to actually produce
output — worth testing once that tool is confirmed working on a real supported title.

**Trap hit and fixed — VolumeProfile sub-asset scripting doesn't reliably persist across an Editor
restart even when it looks correct in-session:** the `DlssNrVolume` override added to
`VP_MainMenuCRT.asset` in session 1 (via `ScriptableObject.CreateInstance` +
`AssetDatabase.AddObjectToAsset` + `profile.components.Add`) vanished after Carlos's Editor restart —
back to just CRTSettings + Bloom. `AssetDatabase.LoadAssetAtPath` reload checks *during the same
session* falsely showed it as present and correct; only reading the raw serialized YAML on disk
(`grep` on the `.asset` file) revealed the real bug: the `components:` list had a literal
`{fileID: 0}` (null) entry — the sub-asset object was never actually committed to disk with a real
file identifier. Root cause: the manual creation path didn't reliably register the object before the
container was saved. Fixed by using `VolumeProfile`'s own built-in generic `Add<T>()` method via
reflection (not manual `CreateInstance`), matching real sub-assets' `hideFlags`
(`HideInHierarchy | HideInInspector`, not `DontUnloadUnusedAsset`), and forcing a synchronous
reimport (`AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport)`) immediately
after adding the object and again after setting its values — confirmed by reading the raw `.asset`
file afterward and finding a real 4th YAML document with a matching non-zero `fileID` in the
`components:` list. **Lesson for any future scripted `VolumeProfile`/sub-asset work in this project:
never trust an in-session `AssetDatabase.LoadAssetAtPath` reload to validate persistence — Unity can
serve a stale cached object that looks completely valid. Only a raw file read (or an actual Editor
restart) proves it.**

## What's still needed before this does anything visible

1. ~~`nvngx_dlssnr.dll`~~ — **done**, see above.
2. ~~Direct3D 12~~ — **already active** on this machine, unexpectedly, no action taken to force it.
3. A per-camera NGX context still fails to create (see above) — this is very likely the exact point
   the external AMD proxy tool needs to intercept. Not something more Unity-side wiring can fix.
4. Carlos's own external DLSS-NR-on-AMD install (`dlssnr_on_amd_setup.exe`), pointed at a real
   Windows standalone build of Mr. Moonlight (Frame Generation and the neural pass don't run in the
   Editor Game view for Present-time hooks, though the context-creation failure above reproduces
   fine in-Editor since that part isn't Present-hook-dependent) — still recommended to prove the tool
   works on a known-supported title (e.g. Cyberpunk 2077) first, per session 1's reasoning.

## How to fully roll this back

Everything here is additive and isolated. To remove it entirely:

1. Delete the three folders: `Packages/top.kuanmi.unityrhi/`, `Packages/top.kuanmi.unityrhi.native/`,
   `Packages/top.kuanmi.dlss.urp/`.
2. Remove the three `/[Pp]ackages/top.kuanmi...` lines from `.gitignore` (search "MRM-80").
3. On `Assets/_Project/Settings/PC_Renderer.asset`: remove the "DLSS Neural Rendering" renderer
   feature (Inspector → the renderer features list, or script the same way it was added).
4. On `Assets/_Project/Settings/VP_MainMenuCRT.asset`: remove the DLSS Neural Rendering override
   (Inspector → Volume profile → the override's context menu → Remove).
5. Revert the code changes: `GameSettings.cs` (`DlssEnabled` + its key), `SettingsPanel.cs` (all
   `dlss`/`Dlss` members and the two `if (isOn && ...)` cross-disable blocks), delete
   `GraphicsVendorInfo.cs`.
6. In `MainMenu.unity`: delete the `DlssToggle` GameObject under `Canvas/Settings`.
7. Remove the entry from `Docs/external-assets.md` and this file.

None of the permanent code (`GameSettings`, `SettingsPanel`) has a compile-time reference to the
gitignored package — `ApplyDlss` reaches it by reflection — so steps 1-2 alone (just deleting the
folders) leave the project compiling and running fine with the DLSS checkbox simply logging one
warning and doing nothing, if a full code revert isn't wanted right away.
