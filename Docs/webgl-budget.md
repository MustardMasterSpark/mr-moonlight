# WebGL Budget — the viability decision and the MB allocation

**Owner issue:** MRM-6 · **Status:** awaiting Carlos's sign-off · **Written:** 2026-08-20
**Audited against:** Unity 6000.3.21f1, URP 17.3.0, project at `E:\MrMoonlight` on branch `MRM-6`

---

## The answer, in one sentence

**GO** — Mr. Moonlight can ship as a Unity 6.3 WebGL build on itch.io, but the number that
governs the project is **not 1 GB, it is a ~300 MB built size**, because the graded gate is
*load time*, not disk space, and a 1 GB build fails that gate on any ordinary connection.

Everything below exists to defend that 300 MB.

---

## 1. Why the real ceiling is 300 MB, not 1 GB

The Assignment #10 gate: *"A stranger must be able to open this link and play your game
within 2 minutes without setup instructions."* That is a **wall-clock** criterion, and the
grader's connection is the variable we do not control.

Budget the 120 seconds:

| Phase | Time |
|---|---|
| Page + loader + `.framework.js` | ~3 s |
| `.wasm` download and instantiate | ~8 s |
| `.data` download | **the variable** |
| IndexedDB write (`webGLDataCaching` is on) | ~5 s |
| First scene load + shader warm-up | ~10 s |
| **Fixed overhead** | **~26 s** |

That leaves **~94 s of download**. On the wire, at a realistic grader connection:

| Connection | Bytes in 94 s | Verdict |
|---|---|---|
| 10 Mbps | 118 MB | Only a tiny build passes |
| **25 Mbps** | **294 MB** | **The planning number** |
| 50 Mbps | 588 MB | Comfortable |
| 100 Mbps | 1 175 MB | 1 GB would pass — do not bet the grade on it |

Brotli is already enabled (`webGLCompressionFormat: 0`). Its real effect on *this* content
is modest, because the bulk of a built game is already compressed: Vorbis audio gains ~0–3 %,
DXT textures ~10–20 %. Only the WASM compresses hard (~4:1). **Assume wire size ≈ 0.85 ×
built size.**

So: **built target 300 MB → ~255 MB on the wire → ~82 s at 25 Mbps → ~108 s total.** Passes
with 12 s of margin.

The 1 GB figure remains the *hard* ceiling (itch.io's per-file limit for HTML5 uploads). We
should never see it.

**DECISION (Carlos, 2026-08-20): aim for under 300 MB, but overage is not a stop-work.** If the
build lands above target, we ship it with a **"please allow a moment for the game to load"
notice on the itch.io page** rather than cutting content under deadline.

Two things that decision does *not* change, stated plainly so nobody is surprised in September:

- **The notice manages expectation, not the clock.** Assignment #10 measures seconds. A grader
  on 25 Mbps hitting a 450 MB build waits ~125 s and the gate is missed whether or not the page
  warned them. The notice is worth adding either way — it stops a slow load reading as a broken
  link, which is the worse failure — but it is not a substitute for the budget.
- **450 MB is still the review line.** Not a hard stop any more, but if a build report reads
  above it, that triggers a look at §3 to see which category ran over and whether the overage is
  cheap to claw back. Cheap wins first (skybox resolution, texture max size); content cuts only
  if those are exhausted.

> **Second-load caching is not a defence.** `webGLDataCaching: 1` makes the *grader's second
> visit* instant. The graded visit is the first one.

---

## 2. What the project actually looks like today

The audit found a **near-empty URP template**, which is the best possible position to be in:
every import preset below can be set *before* the content lands, rather than retrofitted onto
400 clips and 220 skyboxes.

| Finding | Detail | Assessment |
|---|---|---|
| Unity version | `6000.3.21f1` | Fine. WebGL 2.0 target. |
| Render pipeline | URP 17.3.0, Linear color space | Correct. |
| Assets on disk | **12 non-meta files, 127 KB total** | Blank slate. `Assets/ThirdParty/` is empty — **AllSky 220 is not imported yet.** |
| Current build size | **No build exists** | MRM-10 produces the first measurement. |
| WebGL quality tier | `m_PerPlatformDefaultQuality: WebGL: 0` → the **Mobile** level | **Problem — see §2.1** |
| Mobile tier renderer | `Mobile_Renderer`, `m_RenderingMode: 0` = **Forward** | 4 additional lights per object. The mine needs more. |
| Mobile tier RP asset | Render scale **0.8**, `m_RequireDepthTexture: 0`, `m_RequireOpaqueTexture: 0`, additional-light shadows **off**, 1 shadow cascade, soft shadows **off** | Render scale 0.8 is a keeper. The two missing textures **break the VFX stack** — see §2.1. |
| PC tier renderer | `PC_Renderer`, `m_RenderingMode: 2` = **Forward+**, SSAO renderer feature enabled | Forward+ is the right mode for the mine. Not currently what WebGL uses. |
| Compression | `webGLCompressionFormat: 0` (Brotli), `webGLDecompressionFallback: 0` | Correct default; fallback needs a verification pass at MRM-10. |
| Memory | `webGLInitialMemorySize: 32` MB, geometric growth, max 2048 MB | 32 MB initial is far too low — every growth step is a visible hitch. |
| Canvas | `defaultScreenWidthWeb: 960 × 600` | Spec at the time was 1920×1080. **Superseded 2026-08-21: the spec itself changed to 960×540, embedded not fullscreen — see `Docs/webgl-constraints.md` and MRM-10.** Coincidentally close to this row's original wrong value, but arrived at deliberately this time. |
| Exceptions | `webGLExceptionSupport: 1` (explicitly-thrown only) | Right for now; drop to None for the M2 build. |
| Code stripping | `stripEngineCode: 1`, no per-platform `managedStrippingLevel` | Engine stripping on. Managed stripping needs setting. |
| Power preference | `webGLPowerPreference: 2` (high-performance) | Correct — requests the discrete GPU. |
| Threads | `webGLThreadsSupport: 0` | Correct. Leave off. |
| Packages | `com.unity.visualscripting`, `com.unity.multiplayer.center`, ~20 unused engine modules, and **MCP for Unity with a runtime assembly** | All build-size tax. See §2.2. |
| Audio | `m_RealVoiceCount: 32`, `m_VirtualVoiceCount: 512`, DSP buffer 1024 | Voice count is above what a browser handles gracefully. |
| Volume profile | `DefaultVolumeProfile` carries every URP volume component, all `active: 1` | Every component is pulled into the build and evaluated. Prune it. |

### 2.1 The one thing that is actually mis-set

**WebGL builds will run the `Mobile` quality tier**, and that tier has
`m_RequireDepthTexture: 0` and `m_RequireOpaqueTexture: 0`.

The consequence is not a slowdown, it is silent breakage: without the depth texture, SSAO,
depth-of-field, soft particles and any depth-driven fog stop working; without the opaque
texture, every screen-distortion effect — which is how **double vision (MRM-55)** and the
**cinematic blur (MRM-56)** are built — has nothing to sample. These fail *in the browser
build only*. That is exactly the editor/WebGL gap `webgl-constraints.md` warns about, and it
is live in the project right now.

**Fix (developer, §8 step 3): give WebGL its own tier.** Do not reuse Mobile, and do not point
WebGL at PC either.

### 2.2 Package and module tax

Every unused engine module still contributes to the WASM. Concretely worth removing before
MRM-10 measures anything:

- `com.unity.visualscripting` — large, and reflection-heavy serialization that strips badly under IL2CPP. Nothing in this project uses it.
- `com.unity.multiplayer.center` — editor-only, but noise.
- **Removed (done 2026-08-20):** `cloth`, `vehicles`, `androidjni`, `tilemap`, `unityanalytics`, `vectorgraphics`, and **`video`** (cutscenes are in-engine — decided, §3).
- **Keep, despite the earlier draft of this document listing them for removal:**
  - **`umbra`** — this *is* Unity's occlusion culling system and it is pipeline-independent, not a
    built-in-render-pipeline leftover. A forest island wants it.
  - **`screencapture`** — MCP for Unity's `ScreenshotUtility.cs` calls
    `ScreenCapture.CaptureScreenshot`. Removing this breaks the MCP runtime assembly's compile
    and with it the Editor bridge.
  - **`xr`, `vr`, `physics2d`, `adaptiveperformance`** — URP and uGUI reference these, and the
    URP asset has `m_UseAdaptivePerformance: 1`. The WASM saving is not worth risking a broken
    compile before a baseline exists. **Test removing them one at a time at MRM-10**, when
    there is a build report to measure the delta against.

**MCP for Unity ships a runtime assembly** (`Packages/com.coplaydev.unity-mcp/Runtime/`,
~1 940 lines) whose asmdef has empty `includePlatforms`/`excludePlatforms` — so it compiles into
every platform, Web included. In practice it is static helper classes that nothing in the game
references, so **Medium managed stripping should remove all of it**. Do not hand-edit the
asmdef: it is third-party, the edit is lost on package update, and the Editor assembly may
reference it. **Confirm it is absent from the MRM-10 build report** instead — that is the check
that matters, and it is cheap once a report exists.

Do not remove: `ai` / `com.unity.ai.navigation` (MRM-27 may use NavMesh as a fallback),
`terrain`, `terrainphysics`, `animation`, `audio`, `particlesystem`, `director` (Timeline,
MRM-15), `ui`/`uielements`/`ugui`, `jsonserialize`, `imageconversion`.

---

## 3. The budget table

All figures are **built size** as the Unity Build Report reports it, before Brotli.
Multiply by ~0.85 for wire size.

| Category | Budget | Ceiling | Notes |
|---|---|---|---|
| **Code** — `.wasm` + framework JS | 25 MB | 35 MB | IL2CPP output. Managed stripping Medium + `link.xml`. Grows with every package. |
| **Engine data** — scenes, shader variants, serialized assets | 20 MB | 30 MB | Shader variants are the risk here, not scenes. See §4.13. |
| **Textures — environment & props** | 60 MB | 80 MB | The largest single line. 512 default, 1024 only for hero surfaces. |
| **Textures — characters** | 25 MB | 35 MB | 8 characters × albedo/normal/mask at 1024. |
| **Skyboxes** | 20 MB | 40 MB | **4 skies only.** A 2048 cubemap is 12.6 MB at DXT1; 1024 is 3.1 MB. Prefer 1024 for the three non-hero skies. |
| **Lightmaps & probes** | 30 MB | 50 MB | Bake low. The island is large; lean on light probes over lightmap resolution. |
| **Meshes, terrain data & animation** | 30 MB | 45 MB | Includes `TerrainData` (a 1024 heightmap is ~2 MB, four splatmaps ~4 MB) and mocap clips. |
| **UI, fonts, TMP atlases** | 10 MB | 15 MB | One atlas per language, never one shared giant atlas. |
| **VFX textures & flipbooks** | 8 MB | 12 MB | Flipbooks are the trap — a 4096 sheet is a texture, not a "particle effect". |
| **Audio** | **70 MB** | **102 MB** | Broken out in §3.1. |
| **Video** | **0 MB** | **0 MB** | **Decided: no video ships.** See below. |
| **Total** | **298 MB** | **444 MB** | Target ≈ 253 MB wire · Ceiling ≈ 377 MB wire |

**On video — DECIDED (Carlos, 2026-08-20): all cutscenes are in-engine runtime. No
pre-rendered video ships in this build.** Pre-rendered footage is the cheapest way to blow this
budget, so this decision is worth more than any single optimization in §4. The demo's
cinematics — **MRM-15** (cutscene framework), **MRM-56** (opening sequence), **MRM-57** (finale
set pieces) — are Timeline and in-engine VFX, never `VideoPlayer`.

Two consequences that follow from it:

- **`com.unity.modules.video` comes out of the manifest** (§2.2 / HANDOFF §8 step 8). It was
  listed there as conditional; it is now unconditional.
- **If a video ever becomes unavoidable**, it streams from a URL rather than shipping inside
  `.data`, and it gets a new row in this table before it is authored — not after.

### 3.1 Audio budget by category

This is the category the issue flagged as the biggest risk, and it is — **but only if the
import settings are wrong.** The same 250 dialogue lines are **264 MB as 44.1 kHz stereo WAV**
and **20 MB as mono Vorbis**. That 13× gap is the whole story: get the presets right once
(§6) and audio stops being the threat.

| Category | Clips (est.) | Load type | Compression | Budget | **Ceiling** | Owning issue |
|---|---|---|---|---|---|---|
| **Voice — dialogue** (4 actors, ~250 lines) | 250 | Compressed In Memory; Streaming for anything > 15 s | Vorbis q 0.40, **mono**, 22 kHz | 20 MB | **28 MB** | MRM-13, MRM-63 |
| **Ambient beds & weather loops** | ~14 | Streaming | Vorbis q 0.50, stereo | 16 MB | **22 MB** | MRM-38, MRM-47 |
| **Music & stingers** | ~8 | Streaming | Vorbis q 0.50, stereo | 12 MB | **18 MB** | MRM-38 |
| **Footsteps** (terrain type × barefoot/boots) | ~160 | Decompress On Load | ADPCM, mono, 22 kHz | 6 MB | **9 MB** | MRM-39, MRM-40 |
| **Weapons & impacts** | ~90 | Decompress On Load | ADPCM, mono | 6 MB | **9 MB** | MRM-22, MRM-23, MRM-24, MRM-52 |
| **Prop & world one-shots** (pooled) | ~120 | Decompress On Load | ADPCM, mono | 5 MB | **8 MB** | MRM-38 |
| **Enemy vocalisations & pain loops** | ~120 | Compressed In Memory | Vorbis q 0.35, mono | 4 MB | **6 MB** | MRM-30, MRM-31, MRM-37 |
| **UI** | ~25 | Decompress On Load | ADPCM, mono | 1 MB | **2 MB** | MRM-18, MRM-19, MRM-42 |
| **Total** | ~787 | | | **70 MB** | **102 MB** |

**Rules that make these numbers hold:**

1. **Mono for anything spatialised.** Stereo on a 3D-positioned source doubles the bytes for
   no audible gain. Stereo is for music, ambient beds, and 2D UI only.
2. **ADPCM, not Vorbis, for clips under ~1 second.** Vorbis has a per-clip header and a decode
   cost that a 300 ms footstep does not amortise. ADPCM is a fixed 3.5:1 and decodes free.
3. **Decompress On Load is a memory decision, not a size decision.** A "Decompress On Load"
   clip is stored compressed on disk and expanded in RAM. Keep it for short one-shots only;
   using it on a 90-second ambient bed costs ~16 MB of RAM per clip.
4. **Never Streaming for a one-shot.** Streaming has a start latency that will make a gunshot
   arrive after the muzzle flash.
5. **Force To Mono at import**, not in the DAW — it keeps the source files intact.

---

## 4. WebGL traps and their mitigations

Every row names the issue that implements it. Rows marked **HANDOFF** are developer actions in
§8 of this document, not code.

### 4.1 There is no filesystem — CSV must be baked

`Application.persistentDataPath` is IndexedDB, and `System.IO` on arbitrary paths does not
behave. Any runtime CSV read stalls or fails.

**Mitigation:** one editor baker, one menu item `MrMoonlight → Bake Data`, writing
ScriptableObjects to `Assets/_Project/Data/Baked/`. CSV never enters the build.
**Implemented by: MRM-11** (the baker and the event-script format) **and MRM-13** (dialogue
rows). Consumed by **MRM-14** (system messages, objectives) and **MRM-62** (Day 1 script).
`Resources.Load` on the baked assets is acceptable and is the one sanctioned use of Resources.

### 4.2 Audio format and load type per category

**Mitigation:** the table in §3.1, enforced by project-wide import presets set once.
**Implemented by: HANDOFF §8 step 6** (create the presets) **and MRM-63** (the content
pipeline that everyone imports through). Verified by **MRM-38**.

### 4.3 Simultaneous voice count

Browsers handle far fewer concurrent voices than desktop, and the current
`m_RealVoiceCount: 32` will thrash under a wolf pack plus ambience plus footsteps plus VO.

**Mitigation:** drop real voices to **24**, keep virtual at 512, and let the audible-distance
early-out in the pool system do the culling before a source is ever allocated. Empty pools
must stay free. **Implemented by: MRM-38.** Setting: **HANDOFF §8 step 6.**

### 4.4 Threading — A* on the main thread

`System.Threading` is unreliable; no `Thread`, no `Task.Run` parallelism.
`webGLThreadsSupport` is off and should stay off — enabling it requires cross-origin isolation
headers that itch.io does not let us set.

**Mitigation:** A* runs on the main thread, time-sliced across frames with a hard millisecond
budget per frame, in a coroutine. Path requests queue; agents keep their last path while
waiting. **Measure with 10 concurrent agents** — the Spotter flare's worst case.
**Implemented by: MRM-27** (the pathfinding approach and the frame budget) **and MRM-29**
(the state machine must tolerate a stale path for a frame or two without stuttering).
**Tunables that must exist:** `PathfindingMillisecondsPerFrame`, `PathfindingMaxConcurrentAgents`,
`PathfindingRepathInterval` — logged against **MRM-7**.

### 4.5 First-load stall

Covered by §1. Beyond the size budget:

**Mitigation:** a **tiny first scene** — the Unity loader bar, then a ~2 MB main menu scene, then
gameplay loaded additively behind a progress screen. The player must see something within a
few seconds of the WASM instantiating, not stare at a blank canvas for 90 s.
**Implemented by: MRM-18** (the main menu is the first scene, and it must be small — no terrain,
no skybox library, no character rigs) **and MRM-10** (build order, cold-cache timing from a
machine that has never seen the build).

### 4.6 Memory growth hitches

`webGLInitialMemorySize: 32` means the heap grows repeatedly during load, and each growth is a
full copy and a visible freeze.

**Mitigation:** raise initial memory to **512 MB**, keep geometric growth, keep the 2048 MB
maximum (the practical WASM32 ceiling). Watch for out-of-memory on 32-bit-ish machines.
**Implemented by: HANDOFF §8 step 5**, verified in **MRM-10**.

### 4.7 Post-processing — how many passes can be live at once

**The answer is: two custom full-screen passes, plus UberPost, plus bloom at half resolution.**

The important structural fact is that URP merges **bloom compositing, tonemapping, colour
grading, vignette, chromatic aberration, film grain and lens distortion into one pass**
(`UberPost`). Those seven are effectively free to stack — they are already sharing a pass. What
costs a pass is anything URP cannot fold in:

| Effect | Pass cost at 1536×864 (render scale 0.8) |
|---|---|
| Chromatic aberration, vignette, colour grading, film grain, tonemap | **0** — folded into UberPost |
| Bloom | ~10 small downsample/upsample passes. Affordable at half res, `maxIterations` ≤ 4 |
| SSAO (renderer feature) | 1 full-screen pass + blur. **Turn it off for the mine.** |
| Depth of field | 1–3 passes. **Bokeh mode is banned; Gaussian only.** |
| Motion blur | 1 pass + velocity. **Banned.** |
| **Radial blur, double vision, tunnel vision** (custom) | 1 pass **each** if built separately |

**The architectural call:** build **one** custom full-screen `ScriptableRendererFeature`,
`MoonlightScreenFX`, whose shader takes weighted parameters for radial blur, double-vision
offset, tunnel-vision radius and damage tint. Then "damage + fear + drunk all at once" is
**one pass**, not four, and the effects can be authored to *sum* rather than overwrite — which
is standing rule 6 anyway.

**Priority when the budget is exceeded:** damage feedback > fear > substances. Lower-priority
weights are scaled down, never hard-cut, so nothing pops.

**Implemented by: MRM-53** — which should own `MoonlightScreenFX` as the first VFX issue to
land, since MRM-54, MRM-55, MRM-56 and MRM-57 all become "add a weight" once it exists.
**Consumed by: MRM-54** (fear), **MRM-55** (substances), **MRM-56** (opening blur),
**MRM-57** (finale). **One volume with weighted overrides**, not stacked volumes — set up in
**MRM-47**.

**Test at 1920×1080 in a browser.** A full-screen blur is per-pixel; a 600×400 editor game
view proves nothing.

### 4.8 Real-time lights and shadows in the mine

The mine is flashlight-only total darkness, and every Spotter carries a lamp. That is a group
of dynamic point lights in a confined space — the worst case URP has.

**Mitigation:**
- **Forward+** for the WebGL tier, which removes the 4-lights-per-object limit that plain
  Forward imposes. **HANDOFF §8 step 3.**
- **Shadows off on lamps.** The flashlight is the only shadow-casting light in the mine, and
  even that is a candidate to cut. Spotter lamps get no shadow and a faked contact blob if
  grounding is needed. **Implemented by: MRM-34** (Spotter lamp) **and MRM-44** (flashlight).
- **Cap concurrent real-time lights in the mine at 8**, tunable, with distance-based culling
  turning off lamps outside the cap. **Implemented by: MRM-60** (mine staging owns the cap)
  with the tunable `MineMaxRealtimeLights` logged against **MRM-7**.
- **Bake everything outside the mine.** The demo's lighting is authored per story beat, so
  most of it is static and swappable. **Implemented by: MRM-47.**
- **SSAO off in the mine** — it is a full-screen pass buying nothing in the dark. **MRM-60.**

### 4.9 Checkpoint persistence

**Mitigation:** `PlayerPrefs` with an **explicit `PlayerPrefs.Save()` on every write**. In a
browser tab, "flushed later" often means never — the player closes the tab and the save is
gone. Keep the payload small (a JSON string under a few KB); do not attempt binary
serialization, and do not use `BinaryFormatter` or anything reflection-heavy, which strips
badly under IL2CPP.
**Implemented by: MRM-45.**

### 4.10 `Application.Quit()` does nothing

**Mitigation:** the main menu's Quit option is hidden under `#if UNITY_WEBGL`, or replaced with
a "return to splash" that also releases the cursor lock. Decide and implement in **MRM-18**.

### 4.11 Gamepad and cursor lock

Browser Gamepad API behaviour differs from the editor, and some browsers do not expose a pad
until the user has pressed something. `Cursor.lockState` needs a user gesture and behaves
differently per browser.

**Mitigation:** the main menu requires a click or keypress to proceed — which doubles as the
gesture that unlocks both the gamepad and pointer lock, so the requirement costs nothing.
Cursor lock is acquired on gameplay entry, released on pause, re-acquired on resume via the
resume click.
**Implemented by: MRM-8** (control schemes and the browser gamepad path), **MRM-18** (the
gesture-gated menu), **MRM-19** (the pause/unpause cursor flow). **Verified in a real browser
build in MRM-10** — the editor cannot test this.

### 4.12 Skybox library — the single biggest easy win

AllSky 220 is not yet imported. At 2048 a cubemap is ~12.6 MB DXT1; 220 of them is **~2.7 GB**,
which is three times the hard ceiling on its own.

**Mitigation:** import **4 skies only** — never import the pack and strip later, because
"strip later" means auditing 220 folders under deadline. One hero sky at 2048, three at 1024.
Budget 20 MB, ceiling 40 MB (§3).
**Implemented by: MRM-47** (which skies, and the transitions between them). **Logged as
`optimization.md` entry #1 under MRM-64** with the real before/after.

### 4.13 Shader variants and first-frame compile

WebGL compiles shaders to GLSL at load. A variant explosion shows up as both build size and a
multi-second stall on the first frame that uses a material.

**Mitigation:** enable lightmap, fog and instancing **stripping** in Graphics Settings (all
three are currently off), prune `DefaultVolumeProfile` down to the components actually used —
it presently carries every URP volume component with `active: 1`, including test components —
and add a shader variant prewarm during the loading screen.
**Implemented by: HANDOFF §8 step 7** (the settings) **and MRM-47** (the prewarm list, since
it owns the lighting and volume setup). Measured in **MRM-10**.

### 4.14 Text encoding and font atlases

**Mitigation:** all CSV saved **UTF-8 without BOM** — Tracey's dialogue is full of apostrophes
and em dashes, and a mis-encoded apostrophe is an invisible glyph found in the build, not the
editor. TMP atlases must be generated from the actual baked dialogue text, not a guessed
character set. **One atlas per language**, never one shared atlas — a Cyrillic atlas is large
and English players should not download it.
**Implemented by: MRM-13** (dialogue text and the atlas character set), **MRM-14** (system
messages), **MRM-65** (the localization scaffold and the per-language atlas split).

### 4.15 Logging in a shipped build

`Debug.Log` still costs in WebGL, and the string concatenations behind it cost more.

**Mitigation:** strip logging for the release build — `Debug.unityLogger.logEnabled = false`
in a `RuntimeInitializeOnLoadMethod` guarded by `#if !DEVELOPMENT_BUILD`, plus
`[Conditional("UNITY_EDITOR")]` on any project-level log helper.
**Implemented by: MRM-10** (build configuration), logged in **MRM-64**.

### 4.16 Terrain cost

Unity Terrain with trees and detail meshes is a per-frame CPU cost that WebGL feels harder than
desktop, and the current PC tier has `terrainTreeDistance: 5000` with `terrainMaxTrees: 50`.

**Mitigation:** aggressive tree billboard distance, low detail density, and `terrainPixelError`
raised for the WebGL tier. **Implemented by: MRM-58** (blockout and vegetation pass, Carlos's
own task) with the WebGL tier values set in **HANDOFF §8 step 3**.

### 4.17 Do not use WebGPU

Unity 6.3 offers WebGPU as an experimental graphics API for Web. It is faster where it works,
and it does not work everywhere.

**Mitigation:** **ship WebGL 2.0.** Do not gamble a graded deadline on a browser feature the
grader may not have. Revisit after Sept 8 if at all.

---

## 5. Recommended texture import presets

Set these as project-wide presets before any art arrives (§8 step 6). The default should be the
*cheap* setting, so that raising a texture is a deliberate act.

| Preset | Max size | Format | Compression | Applies to |
|---|---|---|---|---|
| **Default (everything)** | **512** | DXT1 (BC1) | Normal Quality, mipmaps on | Props, terrain layers, generic surfaces |
| **Hero surface** | 1024 | DXT1 / DXT5 | Normal | Anything the player's face is 30 cm from |
| **Normal map** | Match albedo | DXT5 (BC3) | Normal, `Normal map` type | All normals. Never above the albedo's size. |
| **Mask / MODS** | Match albedo | DXT5 | Normal, **sRGB off** | Metallic/smoothness/AO packed maps |
| **Character** | 1024 | DXT1 / DXT5 | Normal | 8 characters. 2048 only if a face reads badly at 1024 — measure first. |
| **UI / HUD** | 1024 | DXT5 | Normal, mipmaps **off** | Sprites, icons, the map |
| **VFX flipbook** | 1024 | DXT5 | Normal, mipmaps off | A 4096 sheet is 21 MB. Do not. |
| **Skybox cubemap** | 2048 hero / 1024 others | DXT1 | Normal | 4 skies total (§4.12) |
| **Lightmap** | — | Auto | — | Bake resolution is the lever, not the import setting |

**Non-negotiables:**
- **Texture Compression Format for WebGL = DXT.** ASTC is a mobile format; every desktop
  browser exposes S3TC via WebGL 2.0.
- **sRGB off on every non-colour map** (normal, mask, roughness, height). Wrong sRGB is a
  visual bug that looks like a lighting bug and costs a day to find.
- **Mipmaps ON for anything in the 3D world.** Off costs shimmering *and* bandwidth.
- **Read/Write Enabled OFF** everywhere. It doubles memory.
- **Crunch compression: no.** It trades a smaller download for a long CPU decompression on
  load, which is exactly the wrong trade for a 2-minute load gate.

---

## 6. Recommended audio import presets

| Preset | Force mono | Load type | Compression | Quality | Sample rate |
|---|---|---|---|---|---|
| **VO — dialogue** | Yes | Compressed In Memory | Vorbis | 40 % | Override 22 050 Hz |
| **VO — long (> 15 s)** | Yes | Streaming | Vorbis | 40 % | Override 22 050 Hz |
| **Ambient bed** | No | Streaming | Vorbis | 50 % | Preserve |
| **Music** | No | Streaming | Vorbis | 50 % | Preserve |
| **One-shot — short (< 1 s)** | Yes | Decompress On Load | ADPCM | — | Override 22 050 Hz |
| **One-shot — general** | Yes | Decompress On Load | Vorbis | 40 % | Optimize |
| **Enemy vocalisation** | Yes | Compressed In Memory | Vorbis | 35 % | Override 22 050 Hz |
| **UI** | Yes | Decompress On Load | ADPCM | — | Override 22 050 Hz |

Speech survives low Vorbis quality far better than music does — 40 % on a voice line is
inaudible degradation over a game's SFX bed, and it is the difference between 20 MB and 45 MB
of dialogue.

---

## 7. If the answer had been no-go — the fallback

Recorded so it does not have to be re-derived under pressure. **This is not the current plan.**

If a WebGL build proves unshippable, the fallback is a **Windows standalone hosted on itch.io
with the itch.io desktop app**, plus a **short gameplay video embedded on the page**. This
scores worse against Assignment #10 — "without setup instructions" is not satisfied by a
download-and-run — but it is not zero, and it preserves the 50 % that a broken link forfeits.

**The trigger for invoking this: MRM-10 fails twice.** Nothing else. In particular, **build size
is explicitly not a no-go trigger** (decision, §1) — a large build is an optimization problem
(MRM-64) and a page notice, not a platform change. Only "the build does not run in a browser"
is a no-go.

**If invoked, stop and re-plan before any other issue starts.**

---

## 8. Project setup steps

**Status as of 2026-08-20:** steps 1–3 done by Carlos; **steps 4–8 applied by Claude over the
Unity MCP bridge** and verified against the serialized files on disk; step 9 investigated and
resolved to "no action" (§2.2); **step 10 remains Carlos's** and is MRM-10.

Steps 1, 2 and 10 have to be Carlos's — the Hub is outside Unity, the platform switch tears down
the domain and the MCP bridge with it, and the build is his to run. Everything else is
project-wide configuration, which is not scene-view work and needs no handoff. All of it is
git-tracked, so any of it is one `git checkout` from undone.

**1. Install the Web build support module.**
Unity Hub → Installs → 6000.3.21f1 → gear → Add modules. In 6.3 the Hub offers **two** entries:
`Web Build Support` and `Web Build Support (IL2CPP)`.

**Install `Web Build Support (IL2CPP)`.** Ticking both is fine — modules are additive and
removable from the same menu — but **IL2CPP is the backend we ship on**:

- IL2CPP (C# → C++ → Emscripten → WebAssembly) is the proven Unity Web path.
- The alternative backend in this Unity generation is **CoreCLR, which Unity's own manual
  labels Experimental**. §4.17's policy applies — no experimental runtime on a graded deadline.
- **§8 step 5 and the 25 MB code budget in §3 both assume IL2CPP.** Managed stripping,
  `link.xml` and exception support are IL2CPP concepts; a different backend invalidates them.

Restart the Editor afterwards, then confirm **Player Settings → Other Settings → Scripting
Backend** reads **IL2CPP** once the platform is switched.

**2. Switch the build target.**
`File → Build Profiles` → **Web** → *Switch Platform*. This triggers a full texture
reimport — with a near-empty project it takes seconds, which is exactly why we are doing it
now rather than in September.

**3. Create a dedicated WebGL quality tier.** *(fixes §2.1 — the important one)*

- Duplicate `Assets/_Project/Settings/Mobile_RPAsset.asset` → **`Web_RPAsset`**, and
  `Mobile_Renderer.asset` → **`Web_Renderer`**. Point `Web_RPAsset`'s renderer list at
  `Web_Renderer`.
- On **`Web_Renderer`**: Rendering Path → **Forward+**. Leave the renderer feature list
  empty (no SSAO by default — MRM-60 can add it per-scene if the surface world needs it).
- On **`Web_RPAsset`**:
  - Depth Texture → **on** · Opaque Texture → **on**, Downsampling **2× Bilinear**
  - Render Scale **0.8** · MSAA **Disabled** · HDR **on**
  - Main Light shadows **on**, resolution **1024**, cascades **2**, distance **50**
  - Additional Light shadows **off**
  - Soft Shadows **off**
  - LOD Cross Fade **on**
- `Edit → Project Settings → Quality`: add a level named **Web**, assign `Web_RPAsset`, and set
  **WebGL's default level to it** (it currently points at `Mobile`). Terrain overrides on this
  level: pixel error **3**, tree distance **1500**, billboard start **30**, max mesh trees **25**,
  detail distance **40**.
- **Set the per-platform defaults, then the availability ticks — in that order.** The `Default`
  dropdown at the bottom of the Levels matrix only lists levels *available* on that platform, so
  unticking a level that is still some platform's default leaves an incoherent state.
  1. Standalone `Default` → **PC**
  2. Web `Default` → **Web** *(this is the actual §2.1 fix — it was landing on `Mobile`)*
  3. Then untick: `Mobile` → Web · `PC` → Web · `Web` → Standalone

  Target matrix — Mobile ends up ticked nowhere, which is correct; there is no mobile target.
  Leave the row rather than deleting it, since deleting shifts level indices for no gain.

  | Level | Standalone | Web |
  |---|---|---|
  | Mobile | ☐ | ☐ |
  | PC | ☑ | ☐ |
  | Web | ☐ | ☑ |

  **Why the ticks matter, not just the dropdown.** The dropdown picks the *starting* level;
  anything calling `QualitySettings.SetQualityLevel` — a future options menu, a stray line in
  MRM-65 — can move to any level still marked available. Leaving `Mobile` available on Web means
  the browser build can land back on `Mobile_RPAsset`, the tier with no depth or opaque texture,
  and double vision (MRM-55) and the cinematic blur (MRM-56) silently stop rendering — in the
  browser only. The ticks make that unreachable rather than merely unlikely. They also keep
  `PC_RPAsset` and `Mobile_RPAsset` and their shader variants out of the Web build entirely.

**4. Player Settings → Resolution and Presentation.**
Default Canvas Width **1920**, Height **1080**. Run In Background **off**.
WebGL Template: Default for now; a branded template is M2 work (MRM-65).

**5. Player Settings → Publishing Settings.**
- Compression Format: **Brotli** (already set)
- Decompression Fallback: **off** initially — **verify in MRM-10** that itch.io serves
  `Content-Encoding: br` correctly. If the browser console shows a decompression error, turn
  it on and rebuild. That is the fix; do not switch to Gzip first.
- Data Caching: **on** (already set)
- Name Files As Hashes: **on** — makes itch.io's CDN cache behave
- Initial Memory Size: **512** MB · Maximum: **2048** MB · Growth Mode: **Geometric**
- Exception Support: **Explicitly Thrown Exceptions Only** for now; **None** for the Sept 8
  release build only, and only after the game has been played end-to-end without an exception.
- Managed Stripping Level: **Medium**. Strip Engine Code: **on** (already set).
  Expect one or two `MissingMethodException`s from stripping; the fix is a `link.xml`, not
  turning stripping off.

**6. Create the import presets.**
`Assets/_Project/Settings/Presets/` — one `TextureImporter` preset per row in §5, one
`AudioImporter` preset per row in §6. Wire the defaults in
`Edit → Project Settings → Preset Manager` so a dragged-in file lands on the cheap setting
automatically. Also set `Edit → Project Settings → Audio` → **Real Voices 24**, Virtual Voices
512, DSP Buffer Size **Best Performance**.

**7. Graphics Settings hygiene — NO ACTION NEEDED. An earlier draft of this document was wrong
about both halves of this step; the corrected findings are recorded here so nobody re-does it.**

- **Shader stripping is already correct.** All three settings read `0`, and verified against the
  Editor's own enums that is the *stripping-enabled* value, not "off":
  `UnityEditor.StrippingModes { Automatic = 0, Custom = 1 }` and
  `UnityEditor.Rendering.InstancingStrippingMode { StripUnused = 0, StripAll = 1, KeepAll = 2 }`.
  **Do not switch Lightmap or Fog Modes to `Custom`** — Custom requires hand-listing which modes
  to keep, and getting that list wrong strips modes the game needs, producing broken lighting or
  fog **in the build only**.
- **`DefaultVolumeProfile` does not need pruning yet, and pruning it is not a build-size win.**
  It holds 19 components, all legitimate URP ones (plus 9 orphaned sub-assets with missing
  scripts, which are cosmetic). Removing a component from the profile only changes the global
  *default value* — the post-processing shaders ship via the renderer's `PostProcessData`
  regardless, so this buys a negligible per-frame volume-stack evaluation, not megabytes.
  **Which components the game actually needs is MRM-47's decision**; prune there, with the volume
  design in hand.

**8. Remove the package and module tax** (§2.2), then check the console for compile errors.
Nine removals applied; five modules deliberately kept, with reasons, in §2.2.

**9. Confirm MCP for Unity cannot enter a build.** Open its runtime asmdef and verify the
platform list is Editor-only. If it is not, exclude it before MRM-10.

**10. Build once and read the report.**
`Build Profiles → Build`, then `Window → Analysis → Build Report` (or the
`Editor.log` size breakdown). **Record the number in `Docs/optimization.md` as the baseline** —
every later optimization is measured against it. That is **MRM-10** and **MRM-64**.

**First baseline, 2026-08-20: ~10 MB** for an empty scene with all of §8 applied. §3 budgets
Code + Engine data at 45 MB combined, so the floor is comfortably inside it, and it confirms
engine stripping and Medium managed stripping are working.

### 8.1 Uploading to itch.io — the two gotchas

**Gotcha 1 — pick the right project kind.** itch.io's *Kind of project* dropdown lists
**`Unity ≤ 5.3 — You have a Unity3d file`**. **That is not us.** It refers to the old Unity Web
Player *browser plugin* and its `.unity3d` format, which died with NPAPI around 2016. It is not
a statement about which Unity version itch.io accepts.

**A Unity 6 Web build uploads as `HTML — You have a ZIP or HTML file that will be played in the
browser`.** Zip the *contents* of the build output so `index.html` sits at the **root of the
zip**, not inside a nested folder, then tick "This file will be played in the browser". Set the
embed viewport to match §8 step 4 and enable the fullscreen button.

**Gotcha 2 — Brotli headers.** This is the check §8 step 5 defers to MRM-10, and it is a
*known* itch.io problem: Unity's default Brotli compression needs the server to send
`Content-Encoding: br`, and itch.io has historically not always done so. Symptom is a
decompression error in the browser console on load, not a silent slowdown.

**Test it with the empty ~10 MB build — it uploads in seconds and settles the question before
any content exists.** Escalate in this order and stop at the first that works:

1. **Brotli, no fallback** (current setting) — best wire size. Try this first.
2. **Brotli + Decompression Fallback on.** Unity appends `.unityweb` and decompresses in
   JavaScript. Costs some CPU during load but keeps Brotli's ratio.
3. **Gzip** — roughly 15–20 % worse than Brotli on the WASM.

**Never "Compression: Disabled."** It is listed as a fix in community threads and it would
roughly quadruple the WASM on the wire, which is the one thing §1 says we cannot afford.

### 8.2 First live test on itch.io — 2026-08-20

Empty ~10 MB build, uploaded as kind `HTML`, run fullscreen. **It works.** Findings from the
browser console, in order of how much they change later work.

**RESOLVED — Brotli is served correctly by itch.io.** No rebuild needed, no Decompression
Fallback, no Gzip. Outcome 1 of the §8.1 escalation. Keep the current publishing settings.

```
'…/Build/….data.br' successfully revalidated and served from the browser cache
0ace010ba….framework.js.br
```

**ACTION FOR MRM-18 — there is no `Content-Length` header, so the loading bar cannot show a
real percentage.**

```
[UnityCache] Response is served without Content-Length header.
```

itch.io does not send the total size, so Unity's loader cannot compute progress. **The loading
screen must not display a percentage or a filling bar** — it will sit at zero and then jump,
which reads as a hung page and is exactly the failure mode §4.5 exists to prevent. Use an
indeterminate spinner, a rotating hint, or an animated logo. **Owned by MRM-18**, verified in
**MRM-10**.

**WATCH LIST — three URP internal shaders fail to compile under GLES 3.0.**

```
Hidden/CoreSRP/CoreCopy                                  not supported on this GPU
Hidden/Universal Render Pipeline/StencilDitherMaskSeed   not supported on this GPU
Hidden/Universal/HDRDebugView                            not supported on this GPU
```

These load with URP's resource set whether or not they are used, and **nothing visibly broke** —
the scene rendered. Assessment: `HDRDebugView` is debug-only and irrelevant.
`StencilDitherMaskSeed` only matters for *stencil* LOD cross-fade dithering, and
`Web_RPAsset` is set to `BlueNoise` (`m_LODCrossFadeDitheringType: 1`; verified enum is
`BayerMatrix=0, BlueNoise=1, Stencil=2`), so it is never invoked — **do not "fix" this by
changing the cross-fade setting.** `CoreCopy` is the only one that could matter, and an empty
scene does not exercise a copy path.

**Do not chase these now.** Re-check the console when the first real content lands: **MRM-58**
(terrain and vegetation LODs exercise cross-fade) and **MRM-53** (`MoonlightScreenFX` exercises
full-screen copy paths). If something renders wrong there, start here.

**CONFIRMED — platform capabilities, from the reported extension list:**

| Capability | Status | Confirms |
|---|---|---|
| WebGL 2.0 / GLES 3 context | ✅ | §4.17 — no need for WebGPU |
| `WEBGL_compressed_texture_s3tc` (+`_srgb`) | ✅ | §5 — DXT is the right texture format |
| `EXT_texture_compression_bptc` | ✅ | §3 — BC6H/BC7 available, good for lightmaps |
| `KHR_parallel_shader_compile` | ✅ | §4.13 — shader warm-up can overlap, easing first-frame stall |
| PhysX `Threading Mode: Single-Threaded` | ✅ | §4.4 — confirms the A\* main-thread premise |
| `Audio context resumed after 0.142 seconds` | ✅ | §4.11 — the fullscreen click satisfied the gesture requirement, which is the MRM-18 plan working |

**Benign, ignore:** `Unrecognized feature: 'monetization'/'xr'` and the `allowfullscreen`
notice are itch.io's own iframe attributes. `POST /html-callback ERR_BLOCKED_BY_CLIENT` is an ad
blocker eating itch.io's analytics. Six `INVALID_ENUM: getInternalformatParameter` lines are
Unity probing texture format support and are normal on WebGL.

---

## 9. What this document obliges every later issue to do

1. **Nothing reads a file at runtime.** Data is baked (§4.1).
2. **Every audio clip enters through a preset** (§6). If a clip is imported by hand, it is a bug.
3. **512 is the default texture size** (§5). Raising it is a decision, with a reason.
4. **No new full-screen post pass.** Add a weight to `MoonlightScreenFX` instead (§4.7).
5. **No new shadow-casting real-time light** without checking the mine's cap (§4.8).
6. **Every optimization gets logged** with real before/after numbers — `Docs/optimization.md`,
   MRM-64. Entry #1 is already written for you: the skybox library (§4.12).
7. **Nothing is verified until it has run in a browser, from itch.io, on a machine that is not
   the dev machine.**

---

## 10. Decisions taken, and what is still open

### Decided — 2026-08-20, Carlos

1. **GO on WebGL.** Target **under 300 MB**. Overage is tolerated rather than triggering content
   cuts; the mitigation is a loading notice on the itch.io page, with 450 MB as a review line
   rather than a hard stop. Recorded in §1. **Owned by MRM-10** (which measures the first real
   number) **and MRM-64** (which tracks it thereafter). The itch.io page notice itself is
   **MRM-10**.
2. **All cutscenes are in-engine runtime. No pre-rendered video ships.** Video budget is 0 MB,
   `com.unity.modules.video` is removed. Recorded in §3. **Owned by MRM-15**, with **MRM-56**
   and **MRM-57** building on it.

### Still open

1. **How many characters actually appear on screen in the demo?** §3 assumes 8 at 1024. If
   Scene 8's radio voices (Scott, Shannon) are never seen in person, that is ~7 MB back.
   Needed before **MRM-63** sets up the character pipeline.
2. **Does the surface world need SSAO at all?** It is a full-screen pass and the game is mostly
   at night. If not, `Web_Renderer` never gets the feature and §4.7's pass budget loosens.
   Needed at **HANDOFF §8 step 3** — default to *no* if undecided; adding it later is one
   checkbox, and MRM-60 can add it per-scene.
3. **Is a 2048 hero skybox worth 12.6 MB** — 4 % of the entire build — or do all four go to
   1024 for 12.6 MB total? Needed before **MRM-47** imports anything from AllSky.
