# Change log — Mr. Moonlight

Newest first. One entry per merged issue.
Structure is **BUILT / DECISIONS / FAILED / NEXT** — see `Claude Code Context MDs/kickstart.md` §B.2.

---

## MRM-8 (follow-up) — Look X-axis was silently inverted

**BUILT**

- `InputSystem_Actions.inputactions`: the `Look` action's processor changed from
  `invertVector2(invertY=false)` to `invertVector2(invertX=false,invertY=false)`.

**DECISIONS**

- **Root cause, confirmed not guessed:** `UnityEngine.InputSystem.Processors.InvertVector2Processor`
  defaults **both** `invertX` and `invertY` to `true` (verified live by instantiating one via
  `execute_code`). MRM-8's binding only explicitly set `invertY=false` — to make Y toggleable via
  the `InvertYAxis` tunable — never touching `invertX`, which silently stayed at its inverted
  default. Symptom: moving the stick or mouse right turned the camera left. Caught by Carlos
  during MRM-9 look testing.
- Done inline on MRM-9's branch, same deliberate exception as the mouse-scroll and sprint/jump
  fixes earlier this session — not re-confirmed individually since the pattern was already agreed.

**FAILED**

Nothing to record.

**NEXT**

- **Carlos:** confirm stick/mouse right now turns the camera right in a live test.

---

## MRM-9 (wrap-up) — All acceptance criteria confirmed, ready to commit

**BUILT**

- Confirmed live on itch.io (build `7 - Player Controller`): move, look, jump, crouch (toggle,
  smooth transition), sprint, jump-blocked-while-crouched, look-down shows placeholder body, all
  working in an actual WebGL build. Every tunable reads `Tunables.I` live each frame by
  construction (no caching), covering the "changes take effect in play mode" criterion by design.
- `Player.prefab` and `Controller UI Test.prefab` — Carlos saved both so they're reusable across
  scenes, not just live in `Sandbox`.
- All six MRM-9 acceptance criteria ticked in Linear.

**DECISIONS**

- **Incidental, unexplained diff worth flagging for the commit:** `ProjectSettings.asset`'s
  `preloadedAssets` list dropped its one entry (the Input System actions asset) to empty at some
  point during this session's build/testing cycle. Not an intentional change, cause not
  identified — likely a Unity-internal side effect of the build or a settings save. Everything
  tested fine regardless (WebGL build works, input capture works), but flagged rather than
  silently included in the commit.

**NEXT**

- Ready to commit and merge to `main`. See commit proposal in this session's conversation.
- **Recommended next issue: MRM-12** (core stat framework) — fully unblocked, Sonnet-scoped,
  doesn't need terrain/environment, and plugs directly into the `OnJumped`/`OnSprinting` hooks
  this issue already exposed.

---

## MRM-9 (in progress) — Sprint-backward and stationary-jump bugs found in testing

**BUILT**

- **Sprint now requires a forward move component** (`moveInput.y > 0f`), not just any movement.
  Holding Sprint while backing up or pure-strafing now walks instead. Flagged by Carlos: sprint
  backwards felt wrong.
- **Jump/landing no longer trusts `CharacterController.isGrounded`.** Confirmed live via the
  Unity MCP bridge: dropped the player onto the flat `Sandbox` plane, let it settle, and
  `isGrounded` read `false` continuously while completely at rest (not a one-frame flicker —
  sampled twice, ~11 seconds apart, both `false`). That's why jump only worked while moving:
  continuous horizontal collision resolution was masking the same underlying flakiness. Replaced
  with `PlayerController.CheckGrounded()` — a short downward `SphereCast` against a new `Ground`
  physics layer — used for both the vertical-velocity-reset check and jump eligibility. Confirmed
  fixed the same way: same drop-and-settle test, `CheckGrounded()` now reads `true` at rest.
- New tunable `GroundCheckDistance` (0.2, not in MRM-9's original list — added because the
  acceptance criterion "jump works" can't be met without a reliable grounded check).
- **New `Ground` physics layer created** (was documented in `Docs/unity-conventions.md` but never
  actually created in the project). Assigned to the `Sandbox` scene's test `Plane`. **This is now
  a hard requirement for every floor/terrain surface**, including MRM-58's terrain blockout — see
  the conventions doc's updated layers table.

**DECISIONS**

- **Fixed the ground check at the engine-reliability level (SphereCast), not by adding a fudge
  factor to the existing `isGrounded` reads.** `CharacterController.isGrounded` is a documented
  Unity flakiness point, not something a tunable epsilon can paper over reliably.

**FAILED**

First attempt at verifying the fix via `EditorApplication.update` polling registered from within
a single `execute_code` call showed frozen position across 114 logged "frames" — misleading, not
an actual repro. The callback fired in a rapid synchronous burst rather than at real per-frame
intervals, so the real Play Mode loop (and `PlayerController.Update()`) never got a chance to
interleave. Switched to plain sequential position/state checks with real wall-clock waits between
separate tool calls instead, which reflected genuine elapsed game time.

**NEXT**

- **Carlos:** re-test sprint (forward only), jump-while-stationary, and jump-while-crouched-blocked
  in the Sandbox scene to confirm before the next WebGL build.
- **Whoever builds MRM-58's terrain blockout must assign it to the `Ground` layer** or the player
  won't be able to jump on it. Flagged in `Docs/unity-conventions.md` and as a comment on MRM-58
  itself.
- Rest of MRM-9's acceptance criteria (WebGL build test, live-tunable spot check) still open —
  see MRM-9's own comments.

---

## MRM-8 (follow-up) — Mouse scroll wheel added to the input debug overlay

**BUILT**

- `InputDebugOverlay.cs` now also reports mouse scroll. `Mouse.scroll` is a `Vector2Control`,
  not a `ButtonControl`, so it never fired the existing `InputSystem.onAnyButtonPress` listener
  — that's why only key/button presses showed before. Added a `CheckMouseScroll()` poll in
  `Update()`: `Mouse.current.scroll.y` reports the frame's scroll delta directly (0 when idle,
  no `wasPressedThisFrame`-equivalent needed), reported through the same `FindBoundActions`
  lookup and `_lastPress` display path as button presses.

**DECISIONS**

- **Done inline on MRM-9's branch, not a new issue or MRM-8's own branch.** This is MRM-8-owned
  code (already `Done` in Linear) touched while testing MRM-9 — a deliberate, explicit exception
  to the project's one-issue-one-branch rule, confirmed with Carlos rather than assumed. Recorded
  here and as comments on both MRM-8 and MRM-9 for traceability.

**FAILED**

Nothing to record.

**NEXT**

Nothing outstanding — this was a small, complete addition.

---

## MRM-8 (wrap-up) — Two real bugs found chasing a debug overlay that "wasn't showing"

**BUILT**

- Confirmed, live on itch.io: the 960×540 embed (see below), the `InputDebugOverlay` (MRM-8),
  keyboard and gamepad capture, and action-name lookup all work correctly together. Console:
  `[InputDebugOverlay] Started`, `Gamepad added 1`, zero errors.
- **MRM-66** — a new issue capturing the full checklist of everywhere the target resolution
  number lives (Player Settings, itch.io embed, docs, every future UI Canvas Scaler, the build
  and zip steps), so a future swap to 1280×720 or anything else doesn't require re-discovering
  any of what follows.

**DECISIONS / FAILED — the two real bugs, for the record**

What looked like one mystery ("the debug overlay never shows up in the browser, no matter what
Unity setting we change") was actually two unrelated bugs stacked on top of each other. Neither
was a Unity or WebGL Template problem, despite that being the leading theory for most of the
session:

1. **A stale itch.io upload flag.** The very first build ever uploaded to this project — from
   before any of today's work — stayed flagged "This file will be played in the browser" through
   every subsequent re-upload. Every test that session was silently re-running that original
   build; newer uploads just sat there unused. Confirmed by noticing the served file's content
   hash never changed across builds with genuinely different settings. **Fix:** delete stale
   uploads rather than leaving them, and always confirm the *newest* file is the one flagged —
   checking that flag on an already-uploaded file after the fact doesn't reliably force itch to
   re-process it as HTML; a fresh upload with the flag set from the start is safer.
2. **A zip tool writing invalid path separators.** PowerShell's `Compress-Archive` — and, on
   this machine, even .NET's `ZipFile.CreateFromDirectory` — wrote literal backslash (`\`) path
   separators into nested zip entries (e.g. `Build\...loader.js`) instead of the forward slash
   (`/`) the ZIP spec requires. Windows tools don't care; itch's Linux servers extract a single
   garbled flat filename instead of a real subfolder, so `Build/...loader.js` 404'd while
   top-level files like `index.html` loaded fine. **Diagnosed by inspecting `.FullName` on the
   zip's entries — which was itself misleading (it can normalize for display); the real proof
   came from reading the zip's raw bytes and searching for the literal separator character.**
   **Fix:** never use `Compress-Archive`. Build the zip entry-by-entry, explicitly replacing `\`
   with `/` in each relative path before calling `CreateEntry`. Verify with the same raw-byte
   check before trusting a zip, going forward — recorded in the build-process memory.

**NEXT**

- **MRM-66** exists for the next resolution change; nothing else to carry forward from this
  detour — MRM-8 and MRM-10 are both otherwise complete.

---

## MRM-10 (in progress) — Display target changed to 960×540 embedded, not fullscreen

**BUILT**

- `PlayerSettings.defaultWebScreenWidth/Height` → **960×540** (was 1920×1080).
- `Docs/webgl-constraints.md` — target line updated; decision recorded inline with rationale.
- `Docs/webgl-budget.md` — original canvas-resolution audit row annotated as superseded, left
  in place rather than rewritten (it's a point-in-time record of MRM-6's spike).
- MRM-10's own Scope bullet ("itch.io embed configured: 1920x1080, fullscreen button enabled")
  rewritten to the new 960×540/embedded spec, plus a comment documenting the full decision.

**DECISIONS**

- **Embedded at a fixed 960×540, not launched fullscreen.** While testing MRM-8's input debug
  overlay in a real itch.io browser build, the persistent branding/fullscreen-button bar from
  Unity's `Default` WebGL template only fully disappeared in true fullscreen — and true
  fullscreen has its own letterboxing quirks across monitor aspect ratios (see the WebGL
  Template decision below). Carlos chose to sidestep the whole problem: embed the game at a
  fixed size in the itch.io page instead of chasing fullscreen behavior.
- **960×540, specifically, because it's an exact 2× divisor of 1920×1080** — the resolution
  everything was originally authored against — so nothing scales blurrily. It also quarters the
  fill-rate cost of every full-screen post-processing pass (fear vignette, chromatic aberration,
  etc. — the project's biggest per-pixel cost per `Docs/webgl-budget.md`), and a slightly softer
  canvas suits a 1979-set game better than a crisp full-HD window.
- **Superseded, not deleted:** MRM-6's original "1920×1080 fullscreen" target predates this
  decision by one day and was a reasonable call at the time — WebGL's fullscreen/embed quirks
  only became visible once an actual build went up against actual browsers, which is the whole
  point of MRM-10 running early (see that issue's own "why issue four, not forty" framing).

**FAILED**

Nothing to record.

**NEXT**

- ~~Carlos, itch.io account access required: switch the embed mode...~~ **Done** — itch.io embed
  mode is now "Embed in page", manually-set viewport 960×540, fullscreen button left disabled
  (Carlos wants fullscreen unavailable entirely, not just unused, so the game's own itch.io page
  — title, branding — always stays visible around the embed as a recall/marketing device).
- **A fresh build is still needed to actually ship this** — build folder `3` (`WebGL Template`
  fix, made minutes before this decision) still has the old 1920×1080 canvas baked in.
- **960×540 is now this project's UI reference resolution, not just a display setting.** Flagged
  on every not-yet-built UI/HUD issue: MRM-18 (main menu), MRM-19 (pause/game over), MRM-65 (UI
  polish — the issue that actually builds the title logo and button styling, most affected),
  MRM-46 (difficulty modes' health/stamina bars), MRM-53 (damage feedback HUD wounds + full-
  screen VFX, which also get cheaper at this resolution).
- **Added a prominent line to `CLAUDE.md` itself** (not just `Docs/webgl-constraints.md`) so the
  960×540 target is impossible to miss at the start of any future session, with an explicit note
  that any `1920×1080` reference found elsewhere is stale.
- **No camera or scene changes needed for the resolution itself** — 960×540 is exactly the same
  16:9 aspect ratio as 1920×1080 (half-scale, not a different shape), and Unity cameras frame by
  aspect ratio, not absolute pixel count. Only pixel-space UI (Canvas Scaler reference resolution,
  hardcoded layout) needed flagging, which is what the issue comments above do.
- Confirmed live in build 4: renders at 960×540 embedded, black page background applied, gamepad
  detected via console (`Gamepad added 1`). Re-verify the `InputDebugOverlay` (MRM-8) is visibly
  legible next time — it wasn't confirmed on-screen in the latest test screenshot (a DevTools
  panel was open taking half the browser width, which may just be cropping it out of view).

---

## Incidental — WebGL Template switched to Minimal

Found while diagnosing "the debug overlay isn't visible in the itch.io build" (MRM-8 testing).
Not a bug: the canvas was already scaling correctly to fill the browser window (confirmed live
against the actual itch.io page, both the direct HTML page and a fresh automated load — no
letterboxing, no fixed-size box). The "thin bar" Carlos saw at the bottom was Unity's `Default`
WebGL Template's own footer (branding + its built-in fullscreen button), confirmed via
`PlayerSettings.WebGL.template == "APPLICATION:Default"`.

**Changed:** `PlayerSettings.WebGL.template` → `APPLICATION:Minimal` (Carlos confirmed). Canvas
now goes edge-to-edge with no Unity branding strip. itch.io's own "Click to launch in
fullscreen" page setting already triggers fullscreen independently of Unity's in-canvas button,
so nothing else needed to change. Aspect-ratio letterboxing on non-16:9 monitors is expected
and unaffected — that's Unity preserving the 1920×1080 image rather than distorting it, not a
bug to fix.

Not logged against a specific issue — this affects whichever issue eventually owns "first WebGL
build on itch.io" (MRM-10). No code change, no tunable, no scene touched.

---

## MRM-8 — Input System — Xbox and keyboard/mouse control schemes

**BUILT**

- `Assets/InputSystem_Actions.inputactions` — extended in place, not replaced. `Player` map
  renamed **`Gameplay`** to match the issue's named maps. All fifteen table actions exist:
  `Move`, `Look`, `Fire`, `Interact`, `Crouch`, `Jump`, `Sprint`, `AimDownSights`, `Reload`,
  `SwitchWeapon`, `EquipMelee`, `FlashlightToggle`, `BootsToggle`, `InventoryScroll`, `Pause` —
  each bound exactly to its Xbox control from the issue's table (left/right stick, both
  triggers, both bumpers, all four face buttons, full d-pad, Start). `Crouch` moved off
  `buttonEast`(B) onto **right-stick press** and `Interact` off `buttonNorth`(Y) onto
  **`buttonWest`**(X) — the stock template had them on the wrong buttons relative to this
  issue's table. Added three new, currently-empty action maps — `Turret`, `Stretcher`,
  `Cutscene` — as switch targets for their own future issues. `UI` map kept, trimmed of
  Touch/Joystick/XR bindings and actions (`TrackedDevicePosition`/`TrackedDeviceOrientation`)
  not relevant to this project's WebGL + Xbox + KB/M target. Control schemes trimmed to just
  `Keyboard&Mouse` and `Gamepad` for the same reason.
- **C# wrapper class generation turned on** for the asset (`generateWrapperCode` in the
  importer, was off) — the wrapper lands at
  `Assets/_Project/Code/Runtime/Input/InputSystem_Actions.cs`, namespace `MrMoonlight.Input`,
  auto-regenerated on every reimport. Required adding `Unity.InputSystem` to
  `MrMoonlight.Runtime.asmdef`'s `references` (it had none before this issue).
- `Assets/_Project/Code/Runtime/Input/InputMapController.cs` — the load-bearing piece. Owns
  one `InputSystem_Actions` instance; `SetMode(InputMode)` disables every map and enables only
  the target one (verified live: Gameplay → UI → Cutscene, each transition fully exclusive).
  Not a MonoBehaviour and not a singleton — whatever needs input constructs one in `Awake` and
  calls `Dispose()` in `OnDestroy`, per this project's "no singletons but `Tunables`" rule.
- Two new tunables on `MoonlightTunables`, header **Input System — MRM-8**: `StickDeadzone`
  (0.125 default) and `InvertYAxis` (false default). Verified live via `execute_code`: the
  constructor writes `StickDeadzone` into `InputSystem.settings.defaultDeadzoneMin` (project-wide,
  since `Gamepad`'s stick controls already fall back to that default — confirmed in
  `Gamepad.cs`) and applies `InvertYAxis` as a runtime parameter override on the `Look` action's
  `invertVector2` processor.

**DECISIONS**

- **No `PlayerInput` component, no `InputUser` pairing, no explicit control-scheme-switch
  code.** Both control schemes stay bound and enabled simultaneously — nothing restricts the
  asset to one device. A newly-connected gamepad therefore "just works" the instant its stick
  or button fires, with no restart and no scheme-change plumbing needed. This satisfies the
  issue's hot-plug acceptance criterion for free; a later HUD/prompts issue can still add
  explicit current-scheme detection (e.g. via `InputUser`) if button-icon prompts need it —
  that wasn't asked for here and would have been scope creep.
- **Keyboard bindings for the nine new actions are placeholders**, not Carlos's prepared
  template — it wasn't in the repo when this issue was picked up. Chosen conventionally (R
  reload, Q switch weapon, V equip melee, F flashlight, B boots, Escape pause,
  `[`/`]` + mouse wheel for inventory scroll). **Flagging this: swap these for Carlos's real
  keyboard template as soon as he supplies it** — only the keyboard column needs touching: all
  Xbox bindings are final, straight from the issue's table.
- **Stick deadzone applied via `InputSystem.settings.defaultDeadzoneMin`, not a per-binding
  processor.** `Gamepad.cs` shows the stick controls already default to `stickDeadzone` with no
  explicit min/max, which reads project-wide settings — adding a second explicit processor on
  top would have clamped twice for no benefit.
- **`InventoryScroll` (d-pad left/right, `[`/`]`, mouse wheel) does double duty as both the
  open trigger and the navigate control, confirmed with Carlos and matching MRM-42's existing
  spec** ("Opens on D-pad left/right or mouse wheel"). No separate "open inventory" binding
  exists or is needed — a single `InventoryScroll` read, interpreted differently depending on
  whether the inventory panel is currently open, is MRM-42's job. Corrected from this entry's
  first draft, which had wrongly flagged this as a missing binding.
- **Added `InputDebugOverlay.cs`**, a throwaway `OnGUI` readout of the last button/key pressed
  on any device, via `InputSystem.onAnyButtonPress` — the same pattern shown in Unity's own API
  docs for this exact purpose. Requested by Carlos so he can visually confirm input capture
  before MRM-9 lands a player to react to it. Toggle: F1 (keyboard), Select/View (gamepad), or
  the inspector checkbox. Not wired into any scene — see NEXT.

**FAILED**

Nothing to record.

**NEXT**

- **Unblocks MRM-9** (player controller) — `Gameplay.Move`/`Look`/`Jump`/`Crouch`/`Sprint` are
  ready to read from.
- **Carlos:** confirm the Xbox scheme in an actual browser build (editor gamepad support
  differs from WebGL's Gamepad API per `Docs/webgl-constraints.md` §7) with your Logitech
  controller, and hand over the keyboard binding template so the nine placeholder keys above
  can be swapped for real ones. **Also: drop `InputDebugOverlay` onto any empty GameObject** in
  a test scene (e.g. `SampleScene`, until Sandbox exists) to try it — that wiring is yours per
  the usual rule.
- **Deferred:** `Turret`, `Stretcher`, `Cutscene` maps exist but are empty — each fills in when
  its own issue lands. Current-control-scheme detection/exposure (for button-icon HUD prompts)
  also deferred, not required by this issue's acceptance criteria.
- **MRM-42 comment added**: which underlying actions its "open/navigate" (`InventoryScroll`),
  "use" (`Jump`/A), and "close" (`EquipMelee`/B) mechanics read from, and a flagged open
  question — whether the inventory stays in the `Gameplay` map or borrows the `UI` map while
  open — left for whoever picks that issue up, since MRM-42's own "no pause, Tracey stays
  vulnerable" design means it isn't a clean full mode-switch like Turret/Stretcher/Cutscene.

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
