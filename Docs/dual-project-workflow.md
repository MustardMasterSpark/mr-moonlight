# Two Unity projects — Mr. Moonlight (dev) + Playground (sandbox)

> **⚠ RULING REVERSED, 2026-09-03 (MRM-9).** This document previously stated that HQ FPS Weapons
> migrations take *"FBXs and textures only, never `FPSCore/`"*. Carlos's decision to adopt HQ FPS as
> the character controller **and** weapon system made that impossible to honour: FPSCore is a single
> dependency graph (every gameplay class is a `CharacterBehaviour` resolving its siblings through
> `ICharacter`), so the whole framework came across. It is 8.3 MB of source in its own assembly and
> unused types strip from the build; the art was cut hard instead — 3.6 GB → 115 MB.
> The migration *mechanics* described below (filesystem copy, folder + every `.meta`, verify after)
> are unchanged and were used for it. See `Docs/mrm9-hqfps-integration.md`.

**Decision (2026-08-27/28, Carlos).** From now on, two separate Unity projects run side by side:

| Project | Path | Role |
|---|---|---|
| **Mr. Moonlight** | `E:\MrMoonlight` (this repo) | The shipping game. Git-tracked. Nothing untested lands here. |
| **Playground** | `E:\playground\My project` | Sandbox. Not this repo, not git-tracked by it. Where new/bulk-imported assets get opened, checked, and proven out *before* anything moves into Mr. Moonlight. |
| **Weapons** | `E:\playground\weapon` | **Third project, added 2026-08-28.** HQ FPS Weapons 2.0 in isolation. It ships `ProjectSettings` overrides that clobber a host project, so it gets its own. MCP port **8082**. This is where weapon models/animations are pulled from when MRM-22/23/24/25/52 are worked. |

Why: Carlos is about to bring in a batch of new assets (scope described in a later prompt — no
Linear issue yet). Testing them in an isolated project first, instead of importing straight into
Mr. Moonlight, means a bad or bloated asset pack never touches the shipping project, and Mr.
Moonlight's `Assets/` never accumulates evaluation debt. This is the same instinct behind
`Docs/terrain-vegetation-tooling-decision.md`'s "evaluated and rejected" table — prove it first,
commit second.

## Running both at once — the MCP-bridge setup

Both projects use the same **MCP for Unity** bridge (`com.coplaydev.unity-mcp`) so Claude Code can
inspect and act on either one. Unity happily runs two different projects in two Editor windows at
once (it only blocks reopening the *same* project — the `Library` lock) — but the bridge's HTTP
port is a value **typed into the MCP for Unity window**, not something each project silently
auto-negotiates. Get this wrong and both projects share one server and one session, and starting
or stopping one silently affects the other.

**Current assignment (2026-08-28):**

| Project | Port | Unity instance ID |
|---|---|---|
| Mr. Moonlight | `8080` | `MrMoonlight@87580c9df5a077ae` |
| Playground | `8081` | `test@7e551030e70862e2` |

If a second project ever needs adding, or this pairing breaks again: open its **MCP for Unity**
window → **Connect** tab → **Stop Server** → change **HTTP URL** to an unused port → **Connect** →
confirm the status line shows *that* project's own name (not the other project's, not "No
Session") → **Client Configuration → Configure All Detected Clients**.

Claude Code reaches both through `E:\MrMoonlight\.mcp.json`:

```json
{
  "mcpServers": {
    "unityMCP": { "type": "http", "url": "http://127.0.0.1:8080/mcp" },
    "unityMCP_playground": { "type": "http", "url": "http://127.0.0.1:8081/mcp" }
  }
}
```

New MCP servers only load at Claude Code session start — after editing this file, the session has
to be restarted before the new tools appear.

## Moving an asset from Playground into Mr. Moonlight

**Not through the Package Manager.** The chosen method is a direct filesystem copy of the asset's
folder, `.meta` files included:

1. Confirm the asset's folder in Playground is **self-contained** — nothing inside it references a
   shared material, shader, or texture that lives elsewhere in Playground's `Assets/`. A bulk-import
   pack is exactly the case where this can be false; check before copying, not after.
2. Copy the folder **and every `.meta` file with it** (including the folder's own `.meta`) into
   Mr. Moonlight's `Assets/`. The `.meta` files are what keep GUIDs stable, which is what keeps any
   prefab/material/scene reference inside that folder intact on the other side.
3. Trigger an Editor refresh in Mr. Moonlight (`refresh_unity`, or just let it regain focus) and
   verify the import actually landed — search the AssetDatabase for the new path and check the
   console for errors. Don't take the filesystem copy on faith.

**Verified working, 2026-08-27:** `Wolf_enemu` (prefab, FBX, texture, material, test scene) copied
Playground → Mr. Moonlight this way. All 9 assets imported, GUIDs preserved, zero console errors.
**It was a connectivity test only, not a real asset — Carlos deleted the Mr. Moonlight copy on
2026-08-28 once the transfer was confirmed working.** `Wolf_enemu` is not part of Mr. Moonlight;
the only place it currently exists is Playground (see below), unevaluated.

## What's already staged in Playground

`Docs/external-assets.md`'s "Also acquired, not yet integrated" table lists **Wolfenemu / 01
Monster Wolf Boss (AsAlex / HATOGAME)** as owned but not yet imported anywhere. It is now sitting in
Playground at `Assets/Wolf_enemu/` and `Assets/Hatogame_new/BossMonsterPack1/Wolfboss/` — the
natural first real candidate to run through this workflow once evaluation actually starts.

## Relationship to the existing 3D pipeline

`Docs/3d-asset-pipeline.md` (Lanes A–D) is a different track — it's for **raw source art**
(Retro Realism, ALP packs) that needs Blender retopo/baking and the Pixel8r pass before it's usable.
This Playground workflow is for **pre-built Unity content** (asset-pack prefabs, rigged characters,
whole mini-scenes) that mostly just needs checking, not rebuilding. An asset can go through both —
staged and checked in Playground first, then, if it needs the pixelation/map-set treatment before it
matches the PSX look, into the Lane B/C pipeline once it's in Mr. Moonlight.

## Documentation and tracking gaps found while writing this

- **No Linear issue owns "bulk asset import" yet.** Carlos said the actual scope is coming in a
  later prompt — this doc exists so there's somewhere for that issue to point once it's created.
  `MRM-63` ("Content pipelines — 3D props, characters...") already has Claude keeping its
  documentation current and its step 1 is "acquire from an asset pack" — the natural parent. Added
  a short pointer to this doc there (see Linear); did not create a new issue, since Carlos hasn't
  scoped the bulk-import work yet.
- **`external-assets.md`'s Wolfenemu row didn't note it's already staged in Playground** — fixed
  (see above table).
- **The Mr. Moonlight/Playground port assignment lives only in this doc and in memory, not in
  Linear anywhere.** If the bridge setup ever needs redoing on a different machine, this file is the
  only record — keep it current if the ports change again.

---

## Known Playground-only console errors (2026-08-31) — do not chase these

Playground is deliberately polluted, and two errors are permanent residents. **Neither exists in
Mr. Moonlight, and neither is caused by whatever package you just imported.** Importing anything
triggers a recompile, the recompile re-runs both, and they look new. They are not.

| Error | Real cause | Do |
|---|---|---|
| `DirectoryNotFoundException: ...\Packages\com.waveharmonic.crest\...\Settings.Crest.iOS.hlsl` | **Crest Water 5 is folder-copied into `Assets/PLAYGROUND/` instead of `Packages/com.waveharmonic.crest/`.** Its C# hardcodes that package path in nine `[GenerateHLSL(sourcePath = "Packages/com.waveharmonic.crest/...")]` attributes; URP's `CSharpToHLSL` generator tries to write there on every recompile and the folder does not exist | Ignore, or fix properly: **move** the folder to `Packages/com.waveharmonic.crest/` (it has a `package.json`, so it is a straight move and matches Mr. Moonlight) **or delete it** (Crest already migrated). **Never** create an empty folder under `Packages/` to satisfy the path — no `package.json` makes Package Manager error instead |
| `custom elements added to the Unity Editor's main toolbar using unsupported methods` | `HQ FPS Weapons 2.0/FPSCore/3rdParty/EditorToolbox/Editor/ToolboxEditorToolbar.cs:89` — a Unity 6.3 deprecation in a bundled third-party toolbar, present since the pack was staged in May | ~~Ignore. It cannot follow us over: MRM-23's migration list takes FBXs and textures only, never `FPSCore/`~~ **SUPERSEDED 2026-09-03 by MRM-9** — `FPSCore/` *was* migrated wholesale (it is not cherry-pickable), so this warning now appears in Mr. Moonlight too. Still harmless; see `Docs/mrm9-hqfps-integration.md` §2 for why the ruling changed |

**The general lesson.** A vendor package that hardcodes `Packages/<name>/` paths **must be installed
as an embedded package, not copied into `Assets/`.** Crest is the second time this has bitten
(see `mrm71-crest-water-kickoff.md`); check for hardcoded `Packages/` strings before folder-copying
any future package into Playground.

## Weapons project — pink/magenta render fix (2026-09-03)

The Weapons project (`E:\playground\weapon`, port 8082) wouldn't compile, then wouldn't render
correctly, before Carlos could even test the raw HQ FPS Weapons 2.0 asset. Two unrelated problems,
found in this order — **worth re-checking both if this project is ever reset/reimported from a
fresh asset download**, and worth checking for on the Mr. Moonlight side too if MRM-23/74's
migration ever pulls in *unconverted* source files instead of files already fixed here.

**1. Compile errors — missing legacy Post Processing package.** ~9 `CS0234` errors in
`FPSCore/Code/Runtime/PostProcessing/*.cs`, all `UnityEngine.Rendering.PostProcessing` not found.
Fixed by installing `com.unity.postprocessing` (Package Manager). This got it compiling but was
**not the correct fix** — see next.

**2. Pink/magenta render — two compounding vendor bugs, not a Mr. Moonlight-side mistake.**
FPSCore supports both Built-in RP and URP behind a scripting define, `POLYMIND_GAMES_FPS_URP`
(`#if POLYMIND_GAMES_FPS_URP` → native `UnityEngine.Rendering.Volume`, else → legacy
`UnityEngine.Rendering.PostProcessing`). The asset ships its own pipeline-converter tool
(`FPSCore/Code/Editor/Utilities/RenderPipeline/RenderPipelineUtility.cs`, GUI button in a custom
"Tools Window" → Project page) to flip that define when you pick URP — **but it has a copy-paste
bug**: the line that computes the symbol to *add* reads `GetDefineSymbolForPipeline(fromPipeline)`
instead of `targetPipeline` (~line 149), so clicking BIRP→URP in that tool never actually sets the
define. Net effect: the project renders with URP (a correct native `Volume`/profile was already in
the demo scene) while the PostProcessing code silently kept compiling for the legacy branch —
`PostProcessingManager` was managing an empty, inert legacy `PostProcessVolume` instead of the real
one.
  - Fixed by manually adding `POLYMIND_GAMES_FPS_URP` to Player Settings → Standalone scripting
    define symbols (`PlayerSettings.SetScriptingDefineSymbols`) rather than trusting the vendor's
    own converter button.
  - That exposed a **second**, previously-unreachable vendor bug (nobody had ever gotten the define
    set before): `RenderPipelineUtility.cs:261` uses a bare `Volume` with no matching `using` alias
    in that file → `CS0246`. Fixed by qualifying it as `UnityEngine.Rendering.Volume`.
  - **The actual visible symptom** (solid magenta filling most of the Game/Scene view) turned out to
    be **separate from both bugs above**: 56 of 145 materials project-wide — including the weapon
    models themselves (AKM, Crossbow, DBShotgun, FireAxe, FlareGun, FragGrenade, etc.) — were still
    on the Built-in-only `Standard` shader, which URP can't render. `Shader.isSupported` does **not**
    catch this (it only checks platform support, not render-pipeline compatibility), which is why an
    automated shader-validity scan came back clean while the view was still solid pink. Fixed via
    Unity's own `UnityEditor.Rendering.MaterialUpgrader.UpgradeProjectFolder(...)` with
    `StandardUpgrader("Standard")`, then a second pass with `ParticleUpgrader` for ~25 more
    materials still on `Particles/Standard Unlit` and various `Legacy Shaders/Particles/*` (muzzle
    flash / fire / explosion VFX — would only have shown pink once something actually fired a
    weapon, not from the static scene). One holdout remains: `ConiferTree.fbx`'s embedded
    sub-material, low priority — the model already has a separate pre-made `Materials_URP/` set it
    can be pointed at instead.
  - Mid-fix, the MCP bridge went unresponsive for ~15s+ during the second material pass — matches
    the known `[[unity_editor_focus_traps]]` pattern (background AssetDatabase work stalls while the
    Editor window isn't OS-focused); resolved the moment Carlos focused the window, not a real hang.

**Verified 2026-09-03:** scene-view screenshot of the FiringRange demo scene shows correct
textures (stone/brick canopy structure, no magenta), zero console errors, 144/145 materials on
URP-compatible shaders.

## Playground is also the animation bench (2026-08-31)

Everything needed to retarget animation is co-located in Playground and **stays there**:
Retarget Pro V5, HQ FPS Weapons 2.0 (`FP_Arms`), Ultimate Animation Collection (3,068 clips),
Cult Animations, Knife MocapAnimPack, and the Wendigo. Clips are baked here; **only the baked
`.anim`/`.fbx` files migrate** into `Assets/_Project/Art/Animations/<Character>/`. The tool itself
never enters Mr. Moonlight. See `Docs/retarget-pro-strategy.md` §5.
