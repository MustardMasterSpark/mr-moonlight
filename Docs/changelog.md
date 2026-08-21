# Change log — Mr. Moonlight

Newest first. One entry per merged issue.
Structure is **BUILT / DECISIONS / FAILED / NEXT** — see `Claude Code Context MDs/kickstart.md` §B.2.

---

## MRM-7 — MoonlightTunables — central constants asset + inspector pattern

**BUILT**

- `Assets/_Project/Code/Runtime/Data/MoonlightTunables.cs` — the `MoonlightTunables`
  `ScriptableObject`. Sixteen fields across three `[Header]` groups, each with an XML doc
  comment naming its owning issue: **Player Movement — MRM-9** (walk/sprint/crouch speed,
  mouse and stick look speed, look acceleration, jump height and speed, crouch height delta,
  crouch transition duration, slope limit, gravity — defaults per MRM-9's proposed starting
  values where it gave one, sensible placeholders otherwise), **Pathfinding — MRM-27** (the
  three tunables MRM-6 obliged this asset to carry: ms-per-frame budget, max concurrent
  agents, repath interval), **Mine Lighting — MRM-60** (max real-time lights).
- `Assets/_Project/Code/Runtime/Data/Tunables.cs` — the single access point, `Tunables.I`,
  a lazy `Resources.Load<MoonlightTunables>("MoonlightTunables")`. The project's only
  sanctioned singleton and only sanctioned `Resources.Load` call, per
  `Docs/csharp-conventions.md` and `Docs/unity-conventions.md`.
- Per-instance override pattern documented on the `MoonlightTunables` class doc comment
  (mirrors the example already in `Docs/unity-conventions.md`): a tunables value is the
  default, a component may carry `[SerializeField] bool overrideX` + `[SerializeField] float
  xOverride`, and a computed property picks between them. Both fields show in the inspector,
  not just a checkbox. Reuse this shape everywhere a value needs a shared default plus a
  per-instance override (per-enemy cone distance, per-weapon spread, etc.) rather than
  inventing a new one per system.

**DECISIONS**

- **Player-movement values seeded from MRM-9's own issue text**, not left empty. MRM-9 is
  blocked by this issue and hasn't started, but its description already proposes
  walk/sprint/crouch/crouch-transition defaults — using those (and sensible placeholders for
  the rest of its listed tunables: look speed/acceleration, jump height/speed, crouch height
  delta, slope limit, gravity) means MRM-9 lands with values to tune rather than a still-empty
  asset. This is the one exception to "populating it is not in scope" that the issue itself
  calls out.
- **`JumpHeight` and `JumpSpeed` are both fields**, not one derived from the other, even
  though a physics controller could compute takeoff velocity from height and gravity. MRM-9's
  tunables list names them separately; keeping both lets Carlos tune the felt takeoff speed
  without back-solving the math.
- **Script location follows the repo's actual `Code/Runtime` / `Code/Editor` split** (from
  MRM-6), not the `Scripts/Player`, `Scripts/Weapons`, etc. subfolders shown in
  `Docs/unity-conventions.md` — that split doesn't exist yet in this repo. Used a `Data/`
  subfolder under `Code/Runtime` to match the ScriptableObject-definitions intent from the
  conventions doc.

**FAILED**

Nothing to record.

**NEXT**

- **Carlos:** create the `MoonlightTunables` asset instance — `Create > MrMoonlight >
  Tunables` — and place it inside a folder literally named `Resources` so `Tunables.I`
  resolves it (e.g. `Assets/_Project/Data/Resources/MoonlightTunables.asset`), named exactly
  `MoonlightTunables`. Confirm a value change in the inspector takes effect in play mode
  without a recompile, then this issue's acceptance criteria are done.
- **Unblocks MRM-9** (player controller), which now has a tunables asset and seeded defaults
  to build against.
- **Feeds MRM-27** and **MRM-60** with the four tunables MRM-6's comment obliged.

---

## MRM-6 — [SPIKE] WebGL viability decision + build budget

**BUILT**

- `Docs/webgl-budget.md` — the viability decision, the MB budget table, 17 WebGL traps with
  mitigations, texture and audio import preset specs, the project setup sequence, and the
  results of the first live test.
- `Docs/changelog.md` — this file.
- `Assets/_Project/Settings/Web_RPAsset.asset` + `Web_Renderer.asset` — a dedicated WebGL
  render tier. Forward+, depth and opaque textures on, render scale 0.8, MSAA off, main-light
  shadows at 1024 with 2 cascades, additional-light shadows off, soft shadows off.
- `Assets/_Project/Settings/Presets/` — **17 import presets** (9 texture, 8 audio), wired into
  the Preset Manager so imports self-sort by filename prefix.
- Project settings: web canvas 960×600 → **1920×1080**; WebGL initial memory 32 → **512 MB**;
  `nameFilesAsHashes` on; managed stripping **Medium**; audio real voices 32 → **24**, DSP
  buffer → Best Performance.
- New `Web` quality level, with WebGL pointed at it and the availability matrix locked down.
- `Packages/manifest.json` — **9 dependencies removed** (46 → 37).

No runtime code. No tunables — this issue produced a document. The four tunables it *implies*
are logged against MRM-7.

**DECISIONS**

- **GO on WebGL.** The governing number is a **~300 MB build, not 1 GB.** The Assignment #10
  gate is wall-clock: after ~26 s of fixed overhead only ~94 s of download remains, which is
  ~294 MB at 25 Mbps. A 1 GB build only passes above ~100 Mbps.
- **Overage is not a stop-work** (Carlos). Above target we ship a loading notice on the itch.io
  page rather than cutting content. 450 MB is a review line, not a hard stop. Build size is
  explicitly **not** a no-go trigger; only "does not run in a browser" is.
- **All cutscenes are in-engine runtime. No pre-rendered video ships.** Video budget fixed at
  0 MB and `com.unity.modules.video` removed.
- **One custom full-screen pass, not four.** URP already folds chromatic aberration, vignette,
  colour grading, film grain, lens distortion and tonemapping into a single `UberPost` pass, so
  those stack free. Radial blur, double vision and tunnel vision become one weighted
  `MoonlightScreenFX` feature — assigned to MRM-53, which makes MRM-54/55/56/57 "add a weight."
- **Audio is not the biggest risk — wrong import settings are.** 250 dialogue lines are 264 MB
  as stereo WAV and 20 MB as mono Vorbis. Mitigation is project-wide presets, not content cuts.
  Textures are the real pressure at 143 MB.
- **IL2CPP, not the CoreCLR backend.** Unity's manual labels CoreCLR experimental; the whole
  §8 settings chain and the 25 MB code budget assume IL2CPP.
- **WebGL 2.0, not WebGPU.** Not gambling a graded deadline on a browser feature the grader may
  not have.
- **Rejected:** giving WebGL the existing `Mobile` tier (no depth or opaque texture — silent
  VFX breakage in the browser only); hand-editing MCP for Unity's asmdef (third-party, lost on
  update, and Medium stripping should handle it).

**FAILED**

Three claims in the first draft of `webgl-budget.md` were wrong. Corrected in place, recorded
here so they are not retried:

- **"Turn on lightmap/fog/instancing shader stripping — all three are currently off."** Wrong.
  All three read `0`, which the Editor's own enums confirm is `Automatic` / `StripUnused`,
  i.e. stripping already enabled. Setting them to `Custom` requires hand-listing modes to keep
  and a wrong list breaks lighting or fog **in the build only**.
- **"Remove `com.unity.modules.screencapture`."** Wrong. MCP for Unity's `ScreenshotUtility.cs`
  calls `ScreenCapture.CaptureScreenshot`; removing it breaks the Editor bridge.
- **"Remove `com.unity.modules.umbra` — URP does not use it."** Wrong. Umbra *is* Unity's
  occlusion culling and is pipeline-independent. A forest island wants it.

Also downgraded: pruning `DefaultVolumeProfile` is **not** a build-size win — post-processing
shaders ship via the renderer's `PostProcessData` regardless. Moved to MRM-47 as tidiness.

**NEXT**

- **Validated live.** Empty build **~10 MB**, uploaded to itch.io as project kind `HTML`, runs
  fullscreen. **Brotli is served correctly** — no Decompression Fallback, no Gzip. Confirmed on
  the real platform: WebGL 2.0, DXT via `s3tc`, BPTC, `KHR_parallel_shader_compile`, PhysX
  single-threaded, and the audio context resuming on the fullscreen click.
- **Unblocks MRM-10**, which is now mostly done — what remains is the build report, the page
  loading notice, log stripping, and cold-cache timing from a machine that has never seen it.
- **Constrains** MRM-7 (4 tunables), MRM-15 (no video), MRM-18 (**no percentage on the loading
  bar** — itch.io sends no `Content-Length`), MRM-27 (single-threaded A\*), MRM-47 (4 skyboxes),
  MRM-53 (`MoonlightScreenFX`), MRM-58 (terrain tier values), MRM-63 (preset filename prefixes),
  MRM-64 (10 MB baseline).
- **Deferred:** three URP internal shaders fail under GLES 3.0 — `CoreSRP/CoreCopy`,
  `StencilDitherMaskSeed`, `HDRDebugView`. Nothing visibly broke; re-check at MRM-58 (LOD
  cross-fade) and MRM-53 (copy paths).
- **Not created:** `Docs/optimization.md`. It belongs to MRM-64; the baseline and first two
  entries are waiting in that issue's comments.
- **Open questions:** on-screen character count (MRM-63), surface-world SSAO (currently off),
  hero skybox resolution (MRM-47).
