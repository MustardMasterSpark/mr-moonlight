# New Asset List — triage of the 2026-08-27 acquisition batch

**Written 2026-08-27 (Carlos + Claude, Opus).** This is the output of the triage pass promised in
`Docs/asset-triage-kickoff.md` and in `Docs/terrain-vegetation-tooling-decision.md` §5.

**This supersedes nothing.** It is a *second* document alongside `Docs/external-assets.md`, which
remains the register of what is installed and how to restore a machine. This one is the **reasoning
log**: for each asset, take it or not, why, and what it changes.

> **Status: feasibility + preparation only.** Nothing here has been imported, and no asset has been
> integrated. The work happens inside the individual Linear issues, each of which now carries a
> **Related assets** header naming exactly what to stage before starting it.

> ## ⭐ Read the Round 2 section first
>
> **Round 1 (2026-08-27) was written from documentation and store pages. Round 2 (2026-08-28) was
> written with every package open in Playground.** Six verdicts changed and one reversed completely.
> **Where the two disagree, Round 2 wins.** Jump to [Round 2](#-round-2--verified-against-the-actual-files-2026-08-28).

---

## How this was decided

Five rules, applied to every row. They are recorded because several of them **overturn instincts
that were reasonable a week ago**.

1. **Download size is not a criterion.** Carlos's explicit direction, 2026-08-27. Every package is
   staged in **Playground** (`E:\playground\My project`) first; only the files we actually use are copied
   into Mr. Moonlight. A 4.8 GB tool can cost ~0 MB in the build. Build 21 was **54 MB zipped**
   against a 1 GB limit. See `Docs/dual-project-workflow.md`.
2. **Texture size is not a criterion either.** Carlos's direction, 2026-08-27: **no 4K texture will
   ever ship.** Everything routes through the 3D pipeline
   (`Docs/3d-prop-pipeline-wizard.md`) and `MoonlightTextureImporter.cs` (prefix-routed sRGB /
   Point filter / **512 cap** / compression). Art packs are therefore judged on *silhouette and
   style fit*, never on source resolution.
3. **The 1 GB ceiling still stands as a target**, but only three things can realistically threaten
   it, and none of them is a texture: **uncompressed audio**, **large VFX flipbook sheets**, and
   **animation clips imported uncompressed**. Those three are called out individually below.
4. **`RetroLit` compatibility decides anything that renders.** Unchanged from
   `terrain-vegetation-tooling-decision.md` §3. A shader that needs patching is a permanent
   maintenance tax on the game's signature look.
5. **An asset must save more time than it costs.** Where our own implementation is *bounded and
   well-specified* and the asset's adaptation is *unbounded*, we write it ourselves. This rule is
   what rejects five of the most tempting packages in the list.

---

## Part 1 — Three findings that cut across the whole list

These are the highest-value results of the pass. Each one is a conflict that would have been
discovered late and expensively.

### Finding 1 — Three systems now need an *additive contribution registry*, not a setter

The project already solved this problem once. `ScreenTint` (MRM-17) keeps a dictionary of named
red-tint contributions, sums them, and clamps to `MoonlightTunables.RedTintCeiling`, so the death
tint and the low-health tint **add** instead of overwriting each other.

This batch creates the same collision in **two more places**, and both would produce the same bug
— the last writer wins and the effect visibly pops:

| Channel | Contributors after this batch | Assets involved |
|---|---|---|
| **Red screen tint** | death (MRM-17) · low health (MRM-53) · damage hit (MRM-53) | *(already solved)* |
| **Camera shake** ⚠️ | earthquake (MRM-57) · lightning strike (MRM-57) · explosions (MRM-57) · weapon recoil (MRM-21/22/24) · taking damage (MRM-53) · Furman charge (MRM-36) | Smooth Shake Free · Lightning VFX URP · HQ Realistic Explosions |
| **Radial blur** ⚠️ | fear tunnel vision (MRM-54) · low health vignette (MRM-53) | Artistic Radial Blur |

**Decision: build one `CameraShakeService` and one radial-blur contribution channel, both modelled
directly on `ScreenTint`.** Smooth Shake Free (already owned) is the *backend* for the shake
service, never called directly by six different systems. **No asset in this list may install its
own camera shake** — this is why the Lightning VFX URP shake is not being extracted (see §37).

This is the single most valuable finding in the pass, because six unrelated issues would each have
reached for their own shake and the conflict only shows up when two fire at once — in the finale,
which is the least testable moment in the game.

### Finding 2 — The post-processing stack has a required order, and the CRT must be last

Carlos asked directly (context #13) whether the HUD effects would clutter each other. **Answer: they
will not fight, but only if the order is fixed now.**

Verified facts:

- **Every Fronkon Games "Spice Up" effect is a URP Renderer Feature + a Volume component.** Same
  mechanism as HAZE and Retro Shaders Pro. Each is one full-screen blit.
- **`PC_Renderer.asset` currently has three features**: `HazeRendererFeature`, `CRT`,
  `ScreenSpaceAmbientOcclusion`.
- **HAZE bails out entirely when `!cameraData.postProcessEnabled`** (`HazeRendererFeature.cs:546`).
- **Our red damage tint is a UI Canvas overlay, not post-processing** (`ScreenTintRenderer.cs`).
  It therefore sits *above* everything below and cannot conflict with any of it. Good news, and
  worth not re-deriving later.

**The required order on `PC_Renderer`:**

```
HAZE (volumetric fog)
  → SSAO
  → Crest underwater pass            (MRM-71, ships disabled)
  → gameplay screen effects          (drunk / weed / morphine / fear / blood / low-health blur)
  → Bodycam                          (MRM-49 telescope only)
  → Retro CRT                        ← MUST BE LAST
─────────────────────────────────────────────────────────
  UI Canvas overlay: red tint + wound overlays   (not a pass at all)
```

**The CRT going last is a hard rule.** It is the pass that makes everything look like the game. Any
effect registered after it renders *un-retro*, on top of the scanlines, and will read as a modern
overlay pasted onto a PSX frame.

**Cost — stated honestly, because the worst case is bigger than it first looks.** N enabled effects
= N full-screen blits at 1920×1080. **MRM-55's scope is explicit that the three substances *may* be
active simultaneously**, so they cannot be assumed mutually exclusive:

| Concurrent passes, worst case | |
|---|---|
| Drunk + Marijuana + Morphine | 3 |
| Fear blur + low-health blur | 1 — **shared channel, one pass** (Finding 1) |
| HAZE + SSAO + CRT | 3 |
| **Total** | **~7 full-screen passes** |

Each is Volume-weight driven and early-outs at zero weight, so **idle cost is near zero** and the
common case is 3–4 passes. But the ceiling is real. Two mitigations, in order: **measure it in a
build** (baseline 535 draw calls / ~505 FPS gives genuine headroom, and editor `UnityStats` lies on
this project), and if it does not hold, **cap concurrency as a design rule in MRM-48** — a new
substance suppresses the weakest active one — rather than clamping silently in code. Telescope mode
disables the rest regardless.

**One genuine overlap resolved:** Carlos proposed Ghost Vision for *both* morphine and fear, and
Artistic Radial Blur also for fear. Ruling — **Ghost Vision = morphine** (its organic-noise tunnel
matches the brief), **Artistic Radial Blur = fear**, because MRM-53 *already* specifies radial blur
for low health and one blur implementation serving both, summed through Finding 1's registry, is
cleaner than two.

### Finding 3 — Blaze AI and A\* Pathfinding Project are mutually exclusive. Pick one.

**Blaze AI is NavMesh-based** — its waypoints are "pre-set routes or randomly generated from
navmesh". A\* Pathfinding Project replaces the navigation backend entirely. **You cannot adopt
both**; this is a fork, not a menu.

MRM-27 already frames the decision ("Unity's built-in NavMesh vs. a third-party A\* package") and
states **A\* is a requirement, not a suggestion**. Full reasoning in §9 and §29 below. The call is
**A\* Pathfinding Project Pro, and Blaze AI parked** — but this is the one decision in the pass
where a document could change my mind, and Carlos has offered it (see Open Questions).

---

## Part 2 — The five headline calls

| # | Question | Call | One-line reason |
|---|---|---|---|
| 1 | **Replace our player controller with FPS Engine?** | ❌ **No** | It would reopen seven issues (five already **Done**) and it fights the hard "Tracey must see her own feet" requirement, which every FPS template is built against |
| 2 | **Navigation + enemy AI** | ✅ **A\* PP Pro**, ❌ Blaze AI | Mutually exclusive; MRM-27 names A\* as a requirement and the 4103×7085 m tree-dense terrain is exactly Recast's case |
| 3 | **Audio manager** | ✅ **Sounds Good** as the playback backend, ❌ Ambient Sounds | Sounds Good buys Carlos self-service sound authoring; Ambient Sounds duplicates MRM-38's own sound-layer system |
| 4 | **Save system** | ❌ **Crystal Save** | The demo needs in-session respawn checkpoints, not serialization. Ours is smaller than the adapter would be |
| 5 | **Event bus + localization assets** | ❌ **All four rejected** | Both problems are already solved by our existing architecture; adopting either adds a second data model |

Five rejections out of five headline questions looks harsh, so the counterweight is worth stating
plainly: **26 of the 47 assets are adopted**, and the ones adopted are the ones that buy content and
look — models, animations, VFX, audio, terrain tooling — which is exactly where the remaining time
is going. The rejections are concentrated in *framework* assets, where our architecture already
exists and is small.

---

### Call 1 — FPS Engine (cowsins). Reject as foundation, keep as reference.

This got the most scrutiny because Carlos flagged it as "the base of the gameplay."

**What it would replace.** FPS Engine ships `PlayerMovement`, `PlayerStats`, `InputManager`,
`InteractManager`, `Interactable`, `Item_SO`, `UIController`, `PauseMenu`, `Checkpoint`,
`SoundManager`, `GameSettingsManager`, `Crosshair`, `Compass`. Mapped onto our board that is
**MRM-8, MRM-9, MRM-12, MRM-16, MRM-17, MRM-41, MRM-42, MRM-19, MRM-45, MRM-43, MRM-38** — and note
`PlayerStats` and `Interactable` are **literal class-name collisions** with ours.

Five of those issues are **Done**. MRM-16/41/42 are code-complete on `mrm-41`. Adopting the template
means either deleting that work and rebuilding on cowsins' idioms, or running both stacks in
parallel — which is precisely the spaghetti Carlos asked to avoid.

**The deciding fact is not the rework — it is the body.** MRM-9's acceptance criteria include
*"Looking straight down shows placeholder body geometry, not empty space"*, and Carlos restated it
as a hard requirement (Tracey looks down and realises she is barefoot). **Every FPS template,
cowsins included, is architected around an arms-only viewmodel on a separate camera with its own
near-clip.** Retrofitting a true full body into that is fighting the asset's core assumption — the
same shape of mistake as patching `RetroLit` for Nature Renderer: adopting a tool that requires
modifying the thing that makes the game itself.

**The honest counter-argument, stated fairly.** FPS Engine is *more modular than expected*. It
documents `IPlayerMovementActionsProvider` / `IPlayerMovementEventsProvider` /
`IPlayerMovementStateProvider`, and its weapon layer uses separate interfaces
(`IWeaponReferenceProvider`, `IWeaponBehaviourProvider`, `IWeaponRecoilProvider`). A ~20-member
adapter driving its weapon system from our `PlayerController` is a real path, and its weapon layer
(`Weapon_SO` with a **Melee `ShootStyle`**, `WeaponController`, `ProceduralShot`, `WeaponSway`,
attachments, ammo/reload, crosshair, hitmarker) covers MRM-21/22/23/24/25/26 — the largest unbuilt
cluster on the board.

**Why it is still a no.** That adapter's cost is *unknown* and its failure mode is late — you find
out it does not work after a day inside it, and the full-body problem is waiting on the other side
regardless. Our weapon layer is bounded, well-specified across six issues, and we control it.
Rule 5 applies.

**What we do instead — and this is not "ignore the asset".** Stage FPS Engine in Playground and use
it as a **read-only reference and parts donor**. Specifically worth studying or lifting:
`ProceduralShot_SO` (recoil curves), `WeaponSway`, `Crosshair`/`Hitmarker`, and the `Weapon_SO`
field taxonomy as the template for our own `WeaponDefinition` ScriptableObject. Reading a
well-structured paid implementation before writing ours is cheap and legitimate.

**Consequence for the toolkit:** ~~**FPS Animation Baker Toolkit stays.**~~ **⚠️ SUPERSEDED 2026-08-31 —
see R4.** The Baker Toolkit is now **rejected**: the animations it was meant to author are already
owned. The FPS-Engine ruling itself is unchanged — it does not enter the project as runtime code.

**Nothing is reopened by this call.** MRM-9, MRM-12, MRM-17, MRM-8 stay **Done**.

---

### Call 2 — A\* Pathfinding Project Pro. Adopt. Blaze AI parked.

**A\* PP Pro answers MRM-27 line by line**, and the platform switch improved the answer:

| MRM-27 requirement | A\* PP Pro |
|---|---|
| "A\* (Dijkstra family) — a stated requirement" | It is literally the asset |
| "The terrain is not flat… heavily edited forest with many slopes" | **RecastGraph** — "handles both detailed features, and very large worlds", generated from scene geometry |
| "Handle collisions… with each other" | **RVO local avoidance**, built in |
| "Slope limit matching the enemy collider rule" | Per-graph slope limit |
| ~~"WebGL where threading is restricted"~~ | **Void, and now an upgrade** — Pro's multithreading is a Pro feature that only works off WebGL. The platform switch unlocked it |

**Why not Blaze AI, given it would deliver MRM-28/29/30/31 partly pre-built.** Three reasons:

1. **It is NavMesh-based**, so adopting it means *not* adopting A\*, against MRM-27's stated
   requirement. NavMesh baking across a 4103×7085 m terrain carrying 17,350 tree instances is a
   genuinely hard problem — bake time, memory, and terrain trees are not NavMesh-static geometry.
   Recast is the right shape of tool for this island.
2. **MRM-29's spec is unusually precise** and is Carlos's own design: the three-blind-run search
   with its specific 180°-then-360°-then-free sequence, waypoints contributing **only X and Y**
   with Z snapped to ground, per-state cone radius/distance overrides, and distinct return
   behaviour for originally-static vs originally-patrolling enemies. Bending a third-party
   behaviour tree to match that exactly is unbounded; writing it is roughly 1–2 days and it is
   ours.
3. Blaze's genuinely attractive parts — vision, local avoidance, animation hookup — are covered by
   A\* PP's RVO plus MRM-28 and MRM-31, both of which are bounded.

#### Why the sloped, irregular terrain is the deciding factor — not a side note

Carlos raised this explicitly, and it is the single strongest argument in the whole comparison. The
island is **4103 × 7085 m** of hand-edited heightmap with heavy slope variation, and MRM-27 already
states *"the navigation solution must handle that, not assume a plane."*

Both Unity NavMesh and A\* PP's RecastGraph work the same way in principle — voxelise the world,
extract a walkable surface. The difference is **how much control you get over the voxelisation**,
and on terrain this size that control is the whole game:

| | Unity NavMesh | **A\* PP Pro RecastGraph** |
|---|---|---|
| Cell/voxel size | One global setting | **Per-graph, tunable** — trade accuracy against bake cost where it matters |
| Large worlds | One monolithic bake | **Tiled** — scan, update and rebuild per tile |
| Slope handling | Max slope only | Max slope **+ step height + character radius/height per graph**, so a graph can be tuned per enemy type |
| Runtime updates | Requires NavMeshSurface rebuilds | Graph updates on a region, cheaply |
| Threading | Limited | **Multithreaded (Pro)** — and this now works, because we are off WebGL |

There is also a second graph type worth knowing about: A\* PP's **GridGraph samples terrain height
directly**, conforming exactly to the heightmap rather than approximating it with polygons. For a
game whose navigation problem is *"walk over irregular ground without floating or sinking"* — MRM-27's
first acceptance criterion — that is a genuinely useful fallback if Recast's tessellation proves
too coarse on the steeper slopes. Having both options in one asset is itself an argument for it.

**Scope note that makes this much cheaper than it sounds:** the graph does **not** need to cover
4103 × 7085 m. `Docs/Design/Island-Terrain-Reference/Map/player walkable area.png` already defines
the playable region, and enemies only exist where the player can go. **Bound the Recast graph to the
walkable area** — this is the difference between a scan that is painful and one that is routine, and
it should be the first thing set up in MRM-27.

**Known gap to verify on install:** whether Recast rasterises **Unity terrain trees** as obstacles.
Our trees are terrain instances drawn by Flora, and the project has already been bitten once here
(`webgl_gles3_gotchas`: *terrain trees silently reject MeshColliders*). A\* PP has explicit terrain
support and a tree-rasterisation option, but this must be confirmed before MRM-29 starts, because
"enemies path around trees" is an MRM-27 acceptance criterion. **If terrain trees do not rasterise,
the fallback is a collider pass or a spawn-position exclusion mask** — solvable, but budget it.

---

### Call 3 — Sounds Good adopted; Ambient Sounds rejected; Carlos's zone idea adopted anyway.

**Sounds Good — adopt as MRM-38's playback backend.** The deciding argument is not code quality, it
is Carlos's throughput: *"allow me to add sounds myself quickly without having to hassle you every
time."* MRM-38's own Handoff section already says **"Carlos fills the pools"**, so a visual editor
for exactly that job is directly on the critical path of how this project actually works. It brings
object pooling and multiple audio outputs, which MRM-38 would otherwise hand-roll.

**What it does *not* replace:** MRM-38's **audible-distance sphere** and its **sound layers**
(`island` / `cavern` / `mine` / `chapel`, faded by the event director). Those are bespoke gameplay
logic. **Sounds Good becomes the emission/pooling half; our layer + distance gating stays on top.**

**Ambient Sounds (Procedural Worlds) — reject, but take the idea.** Carlos's instinct is right and
worth acting on: *"place a box and once you are inside you get a pool of sounds"* is a better design
than an emitter on every tree, and it should change MRM-38. But **MRM-38's sound-layer system
already is that zone system** — adding Ambient Sounds means two overlapping ambience systems and a
second Procedural Worlds dependency, with the event director only able to drive one of them.

**So: adopt the design, reject the asset.** MRM-38's scope is updated to make zone volumes the
primary ambience mechanism and per-prop emitters the exception. That is a real design improvement
that costs nothing.

---

### Call 4 — Crystal Save. Reject for the demo.

Carlos's own framing decides this: *"For the demo the player won't have the ability to save or
reload states. It just would have the ability to restart from a pre-established respawn point."*

**That is not a save system.** It is: reposition the player, restore stats, reset the enemies in the
group, and set the event director's step index. **In-session, in-memory, no serialization at all.**
Crystal Save is a good asset solving a problem the demo does not have, and the hard part of MRM-45 —
deciding what constitutes restorable state and how the director resumes — is our design either way.

**MRM-45 needs rescoping regardless**, and not because of this asset: its current text is written
against WebGL (*"there is no filesystem… serialize to PlayerPrefs or IndexedDB… state survives a
browser refresh"*). All of that is void as of 2026-08-25. Rescoped below.

**Revisit post-demo.** For the full game with real saves, Crystal Save is a reasonable pick and this
rejection should not be read as a verdict on the asset.

---

### Call 5 — Event bus (×2) and localization (×2). All four rejected.

**Event Manager / Game Event Hub — reject both.** Carlos's goal was *"reduce spaghetti code, make it
cleaner and more understandable for me and for you."* A publish/subscribe bus does the opposite here:

- The runtime is **55 files**, and every cross-system hook already carries an explicit ownership
  comment (`PlayerController.OnJumped` — *"Owner: MRM-9, consumed by MRM-12"*). The call graph is
  currently **readable by grep**. A string-keyed bus deletes that.
- We already use the right patterns: C# `event Action` for one-to-one, and a named-contribution
  registry (`ScreenTint.RedContributions`) for many-to-one — which Finding 1 now extends to shake
  and blur. That *is* the decoupling, and it is type-safe.
- **Neither asset helps MRM-11.** The Event Director is a *sequencer* that runs an ordered
  spreadsheet with blocking `wait_for` steps. That is not pub/sub, and this is worth stating
  because the name collision makes it look like a fit.

If decoupling is ever wanted, it is ~60 lines of our own typed static bus — not a dependency. **Not
recommended before Sept 8**; it is a refactor with no player-visible benefit.

**Evo Localization / I2 Localization — reject both.** MRM-65 records that *"all displayed text…
already comes from spreadsheets by design"*, keyed by ID, with `text_en` / `text_es` / `text_ru`
columns planned, and all ~250 demo lines already carry IDs (`D-08-043`).

**Our data layer already is a localization system.** What is missing is small: a
`LocalizationManager` holding the active column, and a `LocalizedLabel` component for the handful of
UI strings that do not come from a sheet (menu buttons, settings). Adopting either asset means
migrating dialogue, system messages and objectives into a **second data model** — Evo's
ScriptableObject Tables in particular are a different architecture from ours, and its binding is
per-UI-component, which fits our sheet-driven text badly.

**Direct answer to Carlos's question (which of the two):** if one were forced, **I2** — it is
spreadsheet-native with Google Sheets sync, supports code-side lookup by key, and is far more mature.
Evo is the newer and prettier package but it is UI-component-centric, which is the wrong shape for
us. **But the recommendation is neither**, and Spanish text for the demo comes from adding the
`text_es` column that MRM-65 already plans.

---

## Part 3 — The full log, all 47 entries

Verdict key: ✅ **Adopt** · 🟡 **Adopt with a caveat** · 🅿️ **Park** (owned, revisit later) ·
❌ **Reject**

### Terrain, water and world

| # | Asset | Issues | Milestone | Verdict | Reasoning |
|---|---|---|---|---|---|
| 1 | **Crest Water 5** | **MRM-71**, MRM-68 | M2 | ✅ | Already decided 2026-08-27. **This pass adds a confirmation Carlos asked for:** Crest natively supports **multiple separate Water Bodies**, with **Flow inputs for rivers**, **Shorelines & Shallows** for the beach, and depth/water-level inputs — so *aggressive open sea / shallow breaking shore / calm rivers and lake* is a supported configuration, not a hack. Underwater renderer ships **disabled** (Tracey cannot enter water). |
| 2 | **Gaia Pro VS** | **MRM-70**, MRM-58, MRM-59/60/61 | M2 (**after Sept 1**) | ✅ | Already decided. Editor-time tools only, installed temporarily, recipe saved to `_Project`. **Do the erosion-only heightmap pass first** — it is reversible, independently useful, and closes gaps 1/2/4/6 in `terrain-vegetation-tooling-decision.md` §2c. Terrain shaping is exactly what Carlos wants and is the low-risk half. |
| 4 | **Topdown Nature Library (1000)** | **MRM-70** | M2 | 🟡 | Adopt for biome variety, but the risk is **not** poly count or scale — it is that top-down packs are authored to be seen from above, so undersides and side silhouettes are often unfinished or billboarded. **Verify a sample at first-person eye height in Playground before committing the set.** Add this check to the prop wizard. |
| 36 | **Altos Volumetric Clouds** | MRM-47, MRM-69, MRM-18 | M2 | 🅿️ | **Genuine conflict, needs a Playground test.** Altos is a *skybox and weather* system; MRM-69's `SkyboxSwitcher` swaps `RenderSettings.skybox` between AllSky materials and MRM-47 owns lighting. If Altos renders clouds **over** an existing skybox it is adoptable; if it **is** the skybox it collides with AllSky, `SkyboxSwitcher` and `TimeManager` at once. Do not install into Mr. Moonlight until this is answered. |
| 46 | **Skybox Blender** | MRM-69, MRM-47 | 🅿️ | 🅿️ | MRM-47 already made the opposite call deliberately: *"skybox swaps happen instantly, never blended — the story hides every swap behind a beat instead (the cabin interior, the mine entrance)."* The asset solves a problem the design already routed around. Park; revisit only if a swap ends up visible on screen. |
| 47 | **URP Wet Shaders** | **MRM-18** | M2 | ✅ | Material-only, no renderer features, no runtime system. Main-menu staging (wet ground + rain ripples). Zero conflict risk. |
| 23 | **Northern Lights Pack** | MRM-47, MRM-57 | M2 | 🟡 | Adopt for the well scene. Age is not the risk — **Built-in-RP particle shaders rendering magenta under URP is**, and the fix is remapping to `Universal Render Pipeline/Particles/Unlit`. Budget 20 minutes, not a day. |

### Enemies, AI and animation

| # | Asset | Issues | Milestone | Verdict | Reasoning |
|---|---|---|---|---|---|
| 9 | **A\* Pathfinding Project Pro** | **MRM-27** → unblocks MRM-29/33/34/35 | **M1** | ✅ | See Call 2. Resolves MRM-27's open NavMesh-vs-A\* decision. **The single highest-leverage adopt in the list** — MRM-27 blocks the entire enemy subtree in `system-architecture.md` §1. |
| 29 | **Blaze AI Engine** | MRM-28/29/30/31/33/34/35 | — | 🅿️ | See Call 2 and Finding 3. NavMesh-based, therefore mutually exclusive with #9. Parked rather than rejected outright — **Carlos's PDF could change this** (see Open Questions). |
| 3 | **Wendigo Forest Beast Collection** | **MRM-36** | M2 | ✅ | The boss model. Carlos's call to switch from a bespoke Furman to a wendigo form is a **good one for a reason beyond looks**: a humanoid skeleton means the Ultimate Animation Collection (#7) and Cult Animations (#43) retarget straight onto it, which is most of MRM-36's stated handoff ("Carlos provides model, skeleton and animations"). ⚠️ **Naming:** the canonical name **Furman stays** — it appears in the screenplay, dialogue IDs, glossary and Linear. Only the creature's *form* changes. Flagged in Open Questions. |
| 7 | **Ultimate Animation Collection** | MRM-31, MRM-29, MRM-33, MRM-35, MRM-37, MRM-51 | M1/M2 | ✅ | Mocap humanoid clips, retargetable. Covers enemy locomotion blending (MRM-31) and the downed state (MRM-37). ⚠️ **One of the three real build-size risks** — import only the clips actually used, and check clip compression. Do not import the collection wholesale into Mr. Moonlight. |
| 31 | **Knife MocapAnimPack** | **MRM-35** | M2 | ✅ | Zealot sneak + backstab is a very specific animation need and this is exactly it. |
| 43 | **Cult Animations** | MRM-59, MRM-62, MRM-29 | M2 | ✅ | Carlos's stated use — enemies surrounding Tracey at the well — is a *staging* need the generic collections cover badly. Cheap variety for the finale. |
| 42 | **AnimSet 2-Handed Melee Weapon** | **MRM-23** | M1 | ✅ | Supplies the **third (overhead) swing** that HQ FPS Weapons' trench club lacks. Together they satisfy MRM-23's 3-swing spec exactly — see #5. |
| 10 | **Gore Simulator** | **MRM-32**, MRM-36 | M2 | ✅ | Dismemberment via points on the rig. Pairs with #6 by design (same publisher). Boss explodes on the lightning strike — a real payoff for MRM-36/57. ⚠️ Depends on our own damage system feeding it; since FPS Engine is rejected there is no `IDamageable` collision to resolve. Verify against the retargeted wendigo rig in Playground. |
| 6 | **Blood Factory** | **MRM-32**, MRM-53 | M2 | ✅ | Hit spatter. Same publisher as #10 and designed to integrate. |
| 41 | **Body Poser** | **MRM-59**, MRM-60, MRM-61 | M2 | ✅ | **Editor-only tool, zero runtime footprint.** Directly serves Carlos's staging work (posed enemies in the Glade). Exactly the kind of asset with no downside. |

### Weapons

| # | Asset | Issues | Milestone | Verdict | Reasoning |
|---|---|---|---|---|---|
| 30 | **FPS Engine (cowsins)** | MRM-9 + 10 others | — | ❌ | See Call 1. Rejected as foundation; staged in Playground as a **reference and parts donor**. |
| 5 | **HQ FPS Weapons 2.0** | **MRM-22, MRM-23, MRM-24, MRM-25, MRM-34, MRM-52** | **M1** | ✅ | **Art and animation only** — models, weapon animations, hand animations. Ignore its logic entirely. Covers the M1911, the double-barrel, a **flare gun** (MRM-34's Spotter flare — a model we did not have), and the **trench club**. ⚠️ **Changes MRM-23: the Pickaxe becomes the Club.** See below. |
| 37 | **Realistic Gun VFX** (muzzle flashes) | MRM-22, MRM-24, MRM-52 | M1/M2 | 🟡 | Carlos's plan — muzzle flashes from here, impacts from #17 — is right, and prevents two overlapping impact systems. **Recommend Realistic Gun VFX over Shots VFX** for the muzzle: it is documented and newer. But this is a *look* call; both are owned, so compare them side by side in Playground and keep one. **Import only the muzzle-flash folder.** |
| 37b | **Shots VFX URP** | as above | — | 🅿️ | The alternative to #37. Keep whichever looks better; do not ship both. |
| 17 | **Bullet Impact VFX + Decals** | **MRM-22, MRM-24**, MRM-39 | M1/M2 | ✅ | Impacts + decals + sounds across wood / concrete / dirt / glass. **Synergy worth noting:** the surface-type mapping this needs is the *same* mapping MRM-39 needs for footstep audio. Build one surface-type lookup and both issues consume it. Start with the two Carlos named (wood for trees, rock/concrete for the mine) and extend. |

### VFX and screen effects

| # | Asset | Issues | Milestone | Verdict | Reasoning |
|---|---|---|---|---|---|
| 11 | **Spice Up: Rain** (blood on lens) | **MRM-53**, MRM-32 | M2 | ✅ | Blood spatter on the HUD when an enemy bursts nearby. Pairs with #10. Gated by Volume weight per Finding 2. |
| 12 | **Spice Up: Stoned** | **MRM-55** | M2 | ✅ | The marijuana effect. MRM-55 is labelled *Cut candidate* — this asset makes it cheap enough to keep. |
| 13 | **Spice Up: Bodycam** | **MRM-49** | M1 | ✅ | **The best single-asset fit in the list.** MRM-49 asks for *"a circle at centre with soft, blurred edges — no hard mask"*; Bodycam ships lens warping, radial blur, vignette and sensor noise. ✅ **Carlos's second-camera plan is also the right call** — and `terrain-vegetation-tooling-decision.md` §4 confirms Flora needs no work for it: its culling is **screen-coverage based with no distance clamp**, so a narrowed FOV reveals distant trees automatically. |
| 14 | **Spice Up: Ghost Vision** | **MRM-55** (morphine) | M2 | ✅ | Assigned to **morphine**, not fear — see Finding 2. |
| 34 | **Artistic Radial Blur** | **MRM-54**, MRM-53 | M2 | ✅ | Assigned to **fear** *and* low-health, through one shared contribution channel (Finding 1). ✅ Confirmed it does **not** replace Blur Shaders 2 — different effect, different issue (MRM-56's selective opening blur). |
| 33 | **Drunk Color Pulse** | **MRM-55** | M2 | ✅ | The drunk effect. Different publisher from the Spice Up set but the same Renderer-Feature + Volume mechanism, so it slots into the same ordering. |
| 18 | **Procedural Lightning** | **MRM-57**, MRM-36 | M2 | ✅ | The bolt itself. Needs an origin and an endpoint — straightforward to drive from the event director. |
| 18b | **Lightning VFX URP** (shake + flash) | MRM-57 | — | ❌ | **Do not extract the shake.** Finding 1 forbids a second camera-shake system, and the flash is ~15 lines against our existing `FadeOverlay`/`ScreenTint`. Combining fragments of two lightning assets is exactly the clutter Carlos asked to avoid, for two effects we can already produce. |
| 20 | **HQ Realistic Explosions** | **MRM-57** | M2 | 🟡 | Vernon's distraction (mortar shells). ⚠️ **The second real build-size risk** — realistic explosion packs are large flipbook sheets. Import 2–3 explosions, not the library. |
| 21 | **Ian's Fire Pack** | **MRM-57**, MRM-61 | M2 | ✅ | Candles for Vernon's cabin and general fire. Note MRM-57's **blue ring of fire** is a bespoke spreading-ignition effect and stays custom — this asset supplies ambient fire, not that. |
| 16 | **Insect VFX** | MRM-60, MRM-61, MRM-59 | M2 | 🟡 | Cockroaches and moths for the mine and cabin. ⚠️ Carlos's question — whether the moth effect bundles its own light — is unverified. **If it does, strip the light and use ours**; MRM-47 owns lighting and a stray point light would fight GI and the red-sky pass. Verify in Playground. |
| 19 | **Birds VFX (Fab)** | **MRM-59** | M2 | 🟡 | Circling birds at the Glade. Undocumented, so treat as unknown until opened: confirm it is a particle/VFX-Graph effect, and mute any bundled audio (MRM-38 owns sound). |
| 22 | **Fly Particle System** | MRM-60 | M2 | 🟡 | Nice-to-have for the mine. Same URP shader-remap caveat as #23. If it resists in 15 minutes, drop it — flies are replaceable with a simple particle system. |
| 39 | **Volumetric Light Beam** | **MRM-44** | M2 | 🟡 | The flashlight cone. ⚠️ **Check HAZE first.** We already run a volumetric fog and lighting system, and it may already produce a usable light shaft from a spot light. Adding VLB on top means two volumetric systems doing overlapping work. **Test the flashlight with HAZE alone before installing VLB.** |

### Audio

| # | Asset | Issues | Milestone | Verdict | Reasoning |
|---|---|---|---|---|---|
| 24 | **Sounds Good** | **MRM-38**, MRM-39, MRM-65 | **M1** | ✅ | See Call 3. Playback/pool backend; our layer + distance gating stays on top. |
| 40 | **Ambient Sounds** (Procedural Worlds) | MRM-38 | — | ❌ | See Call 3. **The idea is adopted into MRM-38; the asset is not.** |
| 25 | **Outdoor Atmospheres SFX** | **MRM-38**, MRM-59 | M1/M2 | ✅ | Forest ambience. ⚠️ **The third real build-size risk is audio** — import as compressed (Vorbis), never as WAV. `Docs/audio-import-workflow.md` already routes this by clip prefix. |
| 44 | **Underground Atmospheres SFX** | **MRM-38**, MRM-60 | M2 | ✅ | Mine and cavern sound layers. Same compression note. |

### Props and environments

| # | Asset | Issues | Milestone | Verdict | Reasoning |
|---|---|---|---|---|---|
| 45 | **Sewer / Underground Modular Pack** | **MRM-60** | M2 | ✅ | The mine's modular kit. MRM-60 carries a `Needs: Decision` label on non-Euclidean geometry — a modular kit is what makes that decision cheap to prototype either way. Route through the prop wizard. |
| 27 | **Abandoned Hospital v2** | **MRM-60** | M2 | ✅ | ✅ Confirmed against the screenplay: the **infirmary is inside the mine** (Scene 9), not a separate location — so this feeds MRM-60, not a new issue. |
| 26 | **Industrial Shipping Containers** | **MRM-60** | M2 | ✅ | Mine props. Low-poly already; pipeline handles textures. |
| 28 | **Aged Medieval PBR Tools** (sickle) | **MRM-35** | M2 | ✅ | The Zealot's weapon. Extract the sickle only — a single prop through the wizard. |

### Editor tooling and frameworks

| # | Asset | Issues | Milestone | Verdict | Reasoning |
|---|---|---|---|---|---|
| 15 | **Evo Localization** | MRM-65 | — | ❌ | See Call 5. |
| 15b | **I2 Localization** | MRM-65 | — | ❌ | See Call 5. The better of the two if either were used. |
| 32 | **Crystal Save** | MRM-45 | — | ❌ | See Call 4. Revisit post-demo. |
| 35 | **Event Manager** | MRM-11 | — | ❌ | See Call 5. |
| 35b | **Game Event Hub** | MRM-11 | — | ❌ | See Call 5. |
| 38 | **Asset Cleaner Pro** | MRM-64 | M2 | 🟡 | Editor-only, no runtime footprint. **Use its find-references feature; never its bulk-delete.** Our third-party folders are gitignored and MRM-70's TSA prefabs reference meshes *inside* a 1.9 GB external pack — an automated "unused asset" sweep is exactly the tool that would break those references invisibly. Useful with that guardrail, dangerous without it. |

---

## Part 4 — Two content decisions this batch forces

### The Pickaxe becomes the Trench Club (MRM-23)

Carlos's call, made because HQ FPS Weapons already ships a club with two swing animations.

**This is a good trade and it is thematically *better*, not just cheaper.** The glossary currently
rules *"Pickaxe — the Inuit Pickaxe from the Pitch"*, but Aanniarvik already carries a **Japanese
Type 92 turret** (MRM-52) and a **flak tower** on the skyline. A WWII trench club found on that
island is more coherent than an Inuit pickaxe, not less.

**What it changes:**
- **MRM-23** — retitled and rescoped. The 3-swing spec is unchanged and is now *fully covered by
  owned animation*: two horizontal swings from #5, the overhead from #42.
- **MRM-49** — the telescope sequence is triggered by *"the pickaxe pickup"*. Same beat, new object.
- **Glossary** and the screenplay reference need updating; `01-screenplay-demo.md` is a design
  document (background only), so the ruling lives in the glossary and MRM-23.

### The Furman becomes a wendigo *in form* (MRM-36)

**Recommendation: change the creature design, keep the name.** "Furman" appears in the screenplay,
the character/enemy tables, dialogue line IDs and the Linear issue title. Renaming ripples a long
way for no gameplay gain, and MRM-36 already specifies *"not human, does not use the human behaviour
machine… berserker charge"* — which a wendigo satisfies perfectly.

The real win is that a **humanoid skeleton** turns MRM-36's biggest handoff item ("Carlos provides
model, skeleton and animations") into a retarget job against assets we now own. Flagged in Open
Questions in case Carlos wants the name changed too.

---

## Part 5 — Do this before Sept 1

M1 is a **basic playable loop**, not a finished game. Only four assets genuinely shorten that path;
everything else is M2 polish and should not be installed yet.

| Priority | Asset | Why it is M1 | Blocking |
|---|---|---|---|
| 1 | **A\* Pathfinding Project Pro** | MRM-27 blocks the entire enemy subtree. Nothing enemy-shaped starts without it | Install + Recast scan on the island |
| 2 | **HQ FPS Weapons 2.0** | Weapons are the largest unbuilt M1 cluster and this removes the whole art/animation handoff | Playground staging + wizard pass |
| 3 | **Sounds Good** | MRM-38 is Urgent/M1 and is the stated main optimization strategy; this is also what lets Carlos add sound himself | Install + one pool proven end to end |
| 4 | **Spice Up: Bodycam** | MRM-49 is M1 and this is a near-exact fit for its hardest visual requirement | Renderer-feature registration in the documented order |

**Explicitly not before Sept 1:** Gaia (already scheduled after), Crest, all gore/blood, every
environment pack, every remaining screen effect. They are M2 and installing them early only adds
risk to the gate.

---

## Part 6 — Linear changes made in this pass

Every adopted asset is now named in a **`> **Related assets**`** header at the top of its issue, so
the staging work is visible before the issue is opened.

| Issue | Change |
|---|---|
| **MRM-27** | Asset header. **NavMesh-vs-A\* decision resolved → A\* PP Pro.** WebGL threading paragraph marked void; multithreading is now an upgrade. Tree-rasterisation gap recorded |
| **MRM-29** | Asset header. Blaze AI evaluated and parked, with the reason, so it is not re-litigated |
| **MRM-23** | **Retitled** Pickaxe → Trench Club. Rescoped; animation sources named |
| **MRM-36** | Asset header (Wendigo). Handoff reduced to a retarget job. Naming ruling recorded |
| **MRM-38** | Asset header. **Rescoped:** Sounds Good as backend, zone volumes as the primary ambience mechanism |
| **MRM-45** | **Rescoped** — WebGL/PlayerPrefs text void; demo scope is in-session respawn checkpoints. Crystal Save rejection recorded |
| **MRM-49** | Asset header. Second-camera approach endorsed; Flora zoom behaviour confirmed |
| **MRM-53 / MRM-54** | Asset headers. **Shared radial-blur contribution channel** specified across both |
| **MRM-57** | Asset header. **Single camera-shake service** specified; lightning shake/flash extraction ruled out |
| **MRM-65** | Asset header. **Carlos's open question answered** — no localization asset; add the `text_es` column |
| **MRM-11** | Event-bus assets evaluated and rejected, with the reason |
| **MRM-9** | Note that FPS Engine was evaluated and rejected, so the controller is not re-litigated. **Stays Done** |
| **MRM-70 / MRM-71** | Asset headers confirming Gaia and Crest, with the multi-water-body finding |
| **MRM-60 / MRM-59 / MRM-61** | Asset headers for the environment and staging packs |
| **MRM-22 / MRM-24 / MRM-52 / MRM-35 / MRM-32 / MRM-44 / MRM-55 / MRM-18 / MRM-64** | Asset headers |
| **MRM-66** | ⚠️ **Recommended for cancellation** — "WebGL resolution swap checklist" is entirely void |
| **MRM-10** | ⚠️ **Needs retitling** — "First WebGL build live on itch.io" is stale |

**No issue needed reopening.** The FPS Engine rejection is what avoided it: adopting it would have
forced MRM-8, MRM-9, MRM-12 and MRM-17 back out of **Done**, and that cost is part of why it was
rejected.

---

## Part 7 — Open questions for Carlos

1. **Blaze AI — send the PDF?** It is the one call a document could overturn. The blocker is
   specific: **if Blaze ships a documented A\* Pathfinding Project integration**, the calculus
   changes and it may be worth adopting on top of #9. If it is NavMesh-only, the parking stands.
2. **The Furman's name** — keep "Furman" and change only the creature's form (recommended), or
   rename the boss to something wendigo-flavoured? Renaming touches the screenplay, dialogue IDs,
   the glossary and Linear.
3. **MRM-66** — confirm cancellation, and **MRM-10** — confirm retitling to a PC build issue.
4. **Muzzle flash: #37 or #37b?** Both owned, both fine. A visual side-by-side in Playground is a
   five-minute call that only you can make.
5. **MRM-55 was labelled `Cut candidate`.** With Stoned, Ghost Vision and Drunk Color Pulse all
   owned, the substance effects are now cheap. Promote it out of cut-candidate status?

---


---

## Part 8 — Integration briefs (one per adopted asset)

**Purpose, per Carlos's instruction 2026-08-27:** when work starts on an issue, the approach for its
assets should already exist. Each brief below is written to be read **cold**, by a session with no
memory of this triage: what to pull, where it goes, the first steps, and the trap specific to *this*
project.

**Universal rules that apply to every brief — not repeated in each one:**

1. **Stage in Playground first** (`E:\playground\My project`), evaluate, then copy only the needed files +
   their `.meta` into Mr. Moonlight. File+meta copy, never Package Manager. See
   `Docs/dual-project-workflow.md`.
2. **Every mesh and texture routes through the prop wizard** (`/prop`). Textures land at ≤512, Point
   filter, compressed, via `MoonlightTextureImporter.cs`. **No 4K texture ever ships.**
3. **RetroLit samples BaseColor + Normal only.** No mask, no metallic, no emission map. AO is
   multiplied into the albedo.
4. **Glowing objects get a real Light on the prefab**, never an emission map.
5. **Record the exact version in `Docs/external-assets.md` on install.** GUID stability depends on it.
6. **Drop `Demo/`, `Samples~/`, `Documentation~/`** unless a brief says otherwise.

---

### A\* Pathfinding Project Pro → MRM-27 *(M1 — do first)*

**Take:** the whole package; it is a coherent system, not a parts bin. Drop `ExampleScenes`.

**Approach, in order:**

1. Add one `AstarPath` GameObject to `Island.unity`. **Bound its Recast graph to the playable
   region**, not the full 4103 × 7085 m — use
   `Docs/Design/Island-Terrain-Reference/Map/player walkable area.png` for the extents. This is the
   difference between a painful scan and a routine one.
2. Enable **tiling**, so the graph can be rebuilt per region rather than wholesale.
3. Scan. Check the mesh conforms on the steep slopes and does not bridge across cliffs.
4. Set slope limit / step height / agent radius to match the `CharacterController` values already in
   `MoonlightTunables`, so enemies and Tracey obey the same terrain rules (an MRM-27 criterion).
5. Put one agent (`FollowerEntity` + `AIDestinationSetter`) on a capsule and walk it across a slope.
6. Enable **RVO** and test ten agents converging — MRM-27's "do not stack" criterion.

**The trap: terrain trees.** Confirm Recast rasterises them as obstacles **before MRM-29 starts.**
This project has been bitten by terrain-tree collider behaviour before. If they do not rasterise,
fall back to a collider pass or a path-exclusion mask — solvable, but it must be budgeted, not
discovered.

**Fallback if Recast is too coarse on slopes:** A\* PP's **GridGraph samples terrain height
directly**. Same asset, no re-purchase, different graph. Try that before concluding the asset is
wrong for us.

**Measure:** frame cost with 10 agents, **in a build**, recorded against MRM-64. Pro's multithreading
works now we are off WebGL — a genuine gain, so measure rather than reusing WebGL-era estimates.

---

### HQ FPS Weapons 2.0 → MRM-22 / 23 / 24 / 25 / 34 / 52 *(M1 — do second)*

**Take:** meshes, weapon animations, hand animations. **Ignore every script.** This is an art asset.

**Approach:**

1. Open in Playground and inventory what is actually there against our list: **M1911**,
   **double-barrel shotgun**, **flare gun**, **trench club**. Confirm each exists before planning
   around it.
2. Per weapon: `/prop` → poly check → RetroLit (BaseColor + Normal only) → 512 textures.
3. **The animations set the rig convention, and that is the real decision.** Settle the hand rig
   once, from these clips, and make every later weapon conform. Defining a rig first and retargeting
   these onto it costs more.
4. The **club has only two swings**; the overhead comes from AnimSet 2-Handed Melee. Retarget it onto
   the same rig **in the same session**, or the third swing will not blend.

**The trap:** this ships a full FPS sample setup. **Do not import its scenes or controllers** — FPS
Engine is rejected (MRM-9), and its logic entering the project reopens that decision by accident.

**Bonus find:** the flare gun. MRM-34's Spotter flare had no model.

---

### Sounds Good → MRM-38 *(M1 — do third)*

**Take:** the whole package. Small, and it is a system.

**Approach:**

1. Install, create the audio outputs, and route them to the **existing AudioMixer groups** — do not
   let it build a parallel mixer. `AudioMixerVolume.cs` and the MRM-18 settings sliders already drive
   Master / Voices / SFX.
2. Prove **one** pool end to end before converting anything: clip set, random selection, pitch range,
   playing in scene.
3. Then make our `SoundPool` ScriptableObject a **thin wrapper** over it, so `TryGetRandomClip`'s
   callers do not change.
4. Build the **layer + audible-distance gating on top** — MRM-38's own work; no asset provides it.

**The trap:** it is tempting to let Sounds Good own zone/ambience logic too. **Do not.** The Event
Director must be the only thing that activates sound layers, or MRM-11 loses control of the
soundscape.

**Import setting that actually matters:** compressed (Vorbis), never WAV. Audio is one of only three
genuine build-size risks.

---

### Spice Up: Bodycam → MRM-49 *(M1 — do fourth)*

**Take:** the effect only.

**Approach:**

1. Register the Renderer Feature on `PC_Renderer.asset` **before the CRT feature.** Order is not
   cosmetic — see Finding 2.
2. Add the override to a **local Volume**, weight 0.
3. Drive weight from the telescope state, blended across the blink transition.
4. Tune toward MRM-49's brief: soft-edged circular aperture, **no hard mask**. Turn sensor noise and
   flare down — bodycam flavour, not telescope flavour.

**The trap:** HAZE bails out entirely when `!cameraData.postProcessEnabled`
(`HazeRendererFeature.cs:546`). If the telescope camera has post-processing off, fog vanishes and the
effect will not appear. **Check that first** if it works in Scene view but not Game view.

**Also:** the second camera is right, and Flora needs no changes — its culling is screen-coverage
based, so a narrowed FOV reveals distant trees for free.

---

### Gaia Pro VS → MRM-70 *(M2 — after Sept 1)*

Approach is already fully specified in `Docs/terrain-vegetation-tooling-decision.md` §2 / §2b / §2c.
The three things that matter most:

1. **`tar`-list the cached `.unitypackage` and produce a keep/drop list before anything touches
   `Assets/`.**
2. **Erosion-only pass first** — reversible, closes four gaps, and is exactly the "make the terrain
   shape nicer before filling it" step Carlos asked for.
3. **Snapshot the 8 TerrainLayers' order before and after** the first Gaia operation. Gap 5 is the
   highest-risk unknown: a silent reorder breaks vegetation masks *and* footstep surfaces at once,
   and the footstep break is inaudible until someone listens.

**Save the recipe into `Assets/_Project/`,** not `Assets/Procedural Worlds/`, or removing Gaia
destroys the ability to adjust the island later.

---

### Crest Water 5 → MRM-71 *(M2)*

Approach is in `terrain-vegetation-tooling-decision.md` §6 and on the issue. New from this pass, and
the part Carlos asked about — **three water regions, one system.** `WaterRenderer` once, then a
`WaterBody` per region:

| Region | Components |
|---|---|
| Open sea | Base `WaterBody`; distance-varying wave scale is default behaviour |
| Shore | **Shorelines & Shallows** + a depth probe over the beach biome |
| Rivers | **Flow Input** along the channel |
| Lake | Separate `WaterBody`, calm settings, no flow |

**Do the ocean first and stop.** Get one body looking right through the CRT before adding rivers —
three bodies configured at once is three times the debugging surface for a call that has to be made
visually anyway.

**Ship the Underwater Renderer disabled.** Do not delete it.

---

### Wendigo · Ultimate Animation Collection · Cult Animations · Knife Mocap · AnimSet 2-Handed → MRM-36 / 31 / 29 / 35 / 23 / 37

**Treat these as one workstream**, because they share a single decision: **the humanoid rig.**

**Approach:**

1. **Settle the enemy rig first, on the Wendigo**, before retargeting anything. Every later clip
   conforms to it. Doing this per-enemy produces per-enemy rigs and no shared animator.
2. Configure the Wendigo as **Humanoid** in the model importer. Confirm the avatar maps cleanly —
   this is the step that fails, and it fails visibly (twisted limbs), so it is cheap to check.
3. Retarget a **single** locomotion clip and look at it before importing more.
4. Then bulk-import **only the clips actually used.**

**The trap, and it is the build-size one:** animation collections are large and it is tempting to
import everything "to see what's there". **Browse in Playground; import selectively into Mr.
Moonlight.** Check clip compression on import.

**Sequencing:** MRM-32 (hitboxes) and Gore Simulator both need points on this same rig. **Do them in
the same pass**, or every enemy prefab gets touched twice.

---

### Gore Simulator + Blood Factory → MRM-32, MRM-36 *(M2)*

**Approach:**

1. **Prove it on one retargeted enemy before any other.** A retargeted skeleton is the case most
   likely to need adjustment, and that is what all our enemies are.
2. Define dismemberment points **at the same time as MRM-32's hitboxes** — same rig, same pass.
3. Wire our damage system to it: our data (which hitbox, what damage type, how hard) drives its
   triggers. **No `IDamageable` collision**, because FPS Engine was rejected.
4. Mesh-explode is the boss death (MRM-36) and the point-blank shotgun kill (MRM-24).

**The trap:** gore in a terrain-instanced forest means severed parts are loose rigidbodies among
trees. **Cap their count and lifetime** before the finale, where several enemies die at once.

---

### The screen-effect family → MRM-53 / 54 / 55 / 49 *(M2)*

Spice Up **Rain / Stoned / Ghost Vision / Bodycam**, **Artistic Radial Blur**, **Drunk Color Pulse**.

**Approach — do this once for all six, not per issue:**

1. **Register every Renderer Feature on `PC_Renderer` in the Finding 2 order, CRT last.** One sitting
   is far cheaper than six separate ordering arguments.
2. Every effect is driven by **Volume weight**, never by enabling/disabling the feature at runtime.
3. **Build the radial-blur contribution channel** the first time either MRM-53 or MRM-54 is opened —
   modelled on `ScreenTint.RedContributions`.
4. Measure the worst case (~7 passes) **in a build**, once, and record it in MRM-64.

**The trap:** these look independent and are not. The CRT ordering rule and the shared blur channel
are both invisible until two effects run together — which is exactly what will not happen during
single-issue testing.

---

### Procedural Lightning → MRM-57, MRM-36 *(M2)*

**Take:** the bolt generator. **Do not extract the shake or flash from Lightning VFX URP.**

**Approach:** origin transform + target transform, triggered by the Event Director. Tune generations
/ chaos / duration **down** — the demo defaults are showier than a 1979 Alaskan storm wants. Shake
goes through `CameraShakeService`; the white flash is our existing `FadeOverlay`.

---

### Environment and prop packs → MRM-59 / 60 / 61

**Sewer Underground Modular · Abandoned Hospital · Shipping Containers · Medieval Tools (sickle) ·
Ian's Fire · Insect VFX · Northern Lights · Fly Particles · Birds (Fab)**

**Approach:** identical for all. Stage in Playground → pick the specific pieces → `/prop` → into
`Assets/_Project/Art/`.

**Two traps recur across this whole group:**

1. **Built-in-RP particle shaders render magenta under URP.** Affects the older packs (Northern
   Lights, Fly Particles, possibly Insect VFX). Fix: remap to
   `Universal Render Pipeline/Particles/Unlit`. ~20 minutes — **do not reject an asset over this.**
2. **Bundled lights and bundled audio.** Insect VFX may ship its own light; the Birds pack may ship
   audio. **Strip both** — MRM-47 owns lighting, MRM-38 owns sound. A stray point light fights GI and
   the red-sky pass.

---

### Editor-only tools → MRM-59, MRM-64

**Body Poser** — zero runtime footprint, no integration, no risk. Install when staging starts.

**Asset Cleaner Pro** — **find-references only, never bulk-delete.** Third-party folders are
gitignored (deletions unrecoverable), and MRM-70's prefabs reference meshes inside the 1.9 GB TSA
pack.

---

### Volumetric Light Beam → MRM-44 *(M2, conditional)*

**Do not install first.** Build the flashlight with a plain spot light and **HAZE alone**, look at it
in the mine, and only then decide. Two volumetric systems doing overlapping work is a real risk. If
adopted: **SD** beams, not HD. Mesh-based, so it adds no renderer feature and does not affect
Finding 2's ordering.

---

### URP Wet Shaders → MRM-18 *(M2)*

Material-only. Apply to the menu ground plane, tune ripple rate, done. No renderer features, no
conflicts. The lowest-risk asset in the batch.

---

## Keeping this current

When an asset here is actually installed, record it in **`Docs/external-assets.md`** with its exact
version — that file remains the restore register, and the GUID-pinning rule applies to everything in
this list. This document stays as the *reasoning*, so a rejection is not re-litigated in three
weeks.

---

# ⭐ ROUND 2 — verified against the actual files, 2026-08-28

**Everything above was written from documentation and store pages. This section was written with the
packages open in Playground** (`E:\playground\My project`, a fresh project Carlos created and
imported everything into). **Where Round 2 disagrees with Round 1, Round 2 wins.**

Six verdicts changed. Four assets were found that were not in the original list. One decision
reversed completely.

---

## R2.1 — 🔄 THE BIG REVERSAL: adopt Blaze AI, drop A\* Pathfinding Project

**Round 1 said the opposite.** That call was made from a search-result snippet. Reading the source
changes it.

### What is actually true about Blaze AI

**It is NavMesh-based, and deeply so** — that part of Round 1 was right, and is now proven rather
than assumed:

```
BlazeAI.cs:14    [RequireComponent(typeof(NavMeshAgent))]
BlazeAI.cs:3332  NavMesh.CalculatePath(...)      ← static, no A* equivalent
BlazeAI.cs:3238  NavMesh.SamplePosition(...)     ← waypoint randomisation
BlazeAI.cs:3422  NavMesh.FindClosestEdge(...)    ← cover finding
BlazeAI.cs:492   navmeshAgent.nextPosition += rootMotionDelta   ← root motion
```

64 NavMesh references across the package, inside a 4,079-line core. **It cannot be adapted to A\*.**
So the two remain mutually exclusive — Round 1's Finding 3 stands.

**What Round 1 got wrong is the value on the other side of that fork.** Blaze is not "a behaviour
tree we would have to bend". It is a complete enemy AI system whose feature list maps onto our
issues almost line for line:

| Blaze component | Our issue | Fit |
|---|---|---|
| `Vision.cs` — **separate cone angle + sight range per state** (Normal / Alert / Attack) | **MRM-28**, and MRM-29's "per-state cone overrides" | Near-exact |
| `Waypoints.cs` — ordered list, loop toggle, per-point rotation, **`minAndMaxLevelDiff` ground snapping**, randomise-within-radius | **MRM-29** waypoints, including *"a waypoint contributes only X and Y, Z snaps to ground"* | Near-exact |
| `NormalStateBehaviour` + `AlertStateBehaviour` + `SurprisedStateBehaviour` | **MRM-29** idle / patrol / chase | Strong |
| `BlazeAIDistraction` — radius sphere, **`blockingLayers`**, priority, public `Distract(position)` | **MRM-30** hearing sphere and reactions | Strong — walls block sound for free |
| `CoverShooterBehaviour` + `BlazeAICoverManager` | **MRM-34** Spotter | Strong, and MRM-34 is otherwise expensive |
| `AnimationManager.cs`, root-motion support, turn animations | **MRM-31** locomotion + blending | Strong |
| `HitStateBehaviour`, `BlazeAIRagdollData` | **MRM-32**, **MRM-37** | Good |
| `BlazeAIDistanceCulling` | **MRM-64** | Free perf win in a forest |
| `AudioScriptable` (per-state audio) | **MRM-38** hookup | Good |

### The fact that actually decided it

**Blaze's behaviours are user-extensible by design.** `BlazeBehaviour` is an abstract MonoBehaviour
with exactly three members:

```csharp
public abstract class BlazeBehaviour : MonoBehaviour
{
    public abstract void Open();
    public abstract void Main();
    public abstract void Close();
}
```

Behaviours are separate components on the agent. **So Carlos's precise specs get written as our own
`BlazeBehaviour` subclasses** — the three-blind-run search, the wolf circle formation, the Zealot's
curved sneak approach — while reusing Blaze's vision, waypoints, navigation, animation, audio and
culling underneath.

Round 1 framed this as "bend the asset or write our own." **It is neither.** The extension point is
three methods, and that is the difference between a tool and a straitjacket.

### The honest cost of switching to NavMesh

- The project already has **`com.unity.ai.navigation` 2.0.14** — modern `NavMeshSurface`, not legacy. It bakes terrain and can be **bounded to a volume**, so the same "only bake the walkable area" mitigation applies.
- **The terrain-tree question applies equally to both systems**, so it stops being a differentiator. It was one of Round 1's three arguments; it is now neutral.
- What we genuinely give up is A\*'s finer graph control and its multithreading. **That buys navigation quality; Blaze buys five issues.**

### Verdict

| | Round 1 | **Round 2** |
|---|---|---|
| **Blaze AI Engine** | 🅿️ Park | ✅ **Adopt** |
| **A\* Pathfinding Project Pro** | ✅ Adopt (M1, do first) | ❌ **Drop** — mutually exclusive, and it only solves navigation |

**Why this is the right trade given the calendar:** the enemy subtree is the largest unbuilt cluster
and the #1 schedule risk. A\* would have solved MRM-27 and left MRM-28/29/30/31/34 entirely to us.
Blaze substantially delivers all five and leaves us writing only the parts Carlos specified
precisely — which were always going to be bespoke.

**Keep A\* PP Pro in Playground.** If NavMesh baking on the island turns out to be genuinely
unworkable, that is the fallback, and the cost of finding out is one bake.

---

## R2.2 — ❌ Altos Volumetric Clouds: rejected for the Island, approved for the Main Menu

Round 1 parked this pending a binary test. **The test is answered, and it is the bad case:**

```csharp
// Altos Volumetric Clouds/Runtime/Scriptable Objects/SkyDefinition.cs:430
public void SetLightingEnvironment(SkyColorSet skyColorSet)
{
    RenderSettings.skybox = null;
    RenderSettings.ambientMode = AmbientMode.Trilight;
    ...
}
```

Unconditional. **Altos does not render clouds over a skybox — it replaces the skybox *and* takes
over ambient global illumination.** That collides simultaneously with `SkyboxSwitcher` (MRM-47),
the AllSky materials, the apocalyptic-red GI swap (MRM-47) and `TimeManager` (MRM-69).

- ❌ **Island — reject.** Four collisions with systems that are already built and tuned.
- ✅ **Main menu (MRM-18) — adopt.** Separate scene, no `TimeManager`, no `SkyboxSwitcher`, nothing to collide with. It will look excellent behind the title, and it pairs with URP Wet Shaders for a rain-and-cloud menu backdrop.

*(Aside: Altos also ships a procedural lightning system — `SimpleBolt`, `RadialBolt`, attractors. Not
needed; Procedural Lightning is already adopted for MRM-57 and is the better-targeted tool.)*

---

## R2.3 — 🔄 Muzzle flash: **Shots VFX URP wins**, decisively

Carlos asked me to pick with the files in hand. This is not close.

**Realistic Gun VFX — reject.**
- It imported **Built-in and HDRP variants only**. Its **URP support is an unextracted nested `.unitypackage`** sitting in `GunFX/URP/` — so as imported, it contributes *nothing usable*.
- Its HDRP script `ParticleCollisionSpawner.cs` references `DecalProjector` and **was the single compile error breaking your project.**
- It shipped a folder literally named `HDRP(Default` — an unclosed parenthesis in a directory name is a quality signal.

**Shots VFX URP — adopt.** Native URP, already extracted, and it ships exactly what we need:

```
Shots/Muzzle Flash/Once/    VFX_Muzzle_Flash_Handgun      → M1911    (MRM-22)
                            VFX_Muzzle_Flash_Shotgun      → DB       (MRM-24)
                            VFX_Muzzle_Flash_Rifle        → Type 92  (MRM-52)
Shots/Muzzle Flash/Looped/  ...Looped variants            → sustained turret fire
Shots/Muzzle Flash/Light/   VFX_Point_Light_01            → the muzzle light
```

The **Looped** variants matter for MRM-52's belt-fed turret, and the **Point Light** prefab matters
because our standing rule is that glowing things get a real Light, never an emission map. Neither
was visible from the store pages.

**I removed the Built-in and HDRP folders from Realistic Gun VFX** to unblock compilation. Its URP
`.unitypackage` is still there if you ever want a visual comparison.

---

## R2.4 — 🔄 The melee weapon: **there is no club.** Ruling corrected to the Fire Axe

Round 1 rescoped MRM-23 around "a trench club with two swing animations". **That was built on a
recollection, and the files say otherwise.** HQ FPS Weapons 2.0's actual melee wieldables are:

| Weapon | Attack animations shipped |
|---|---|
| **FireAxe** | `StrongAttack` — **one** |
| **BaseballBat** | `ComboAttack`, `StrongAttack` — **two** |
| **CombatKnife** | `Stab1`, `Stab2` — two |

**Ruling: the Fire Axe.** A baseball bat on a 1979 Alaskan island reads as a different game
entirely; a fire axe is at home in a mine, a cabin or a campsite, and it preserves Carlos's original
"Inuit Pickaxe" intent far better than either alternative.

**But MRM-23's three-swing combo is no longer covered by owned animation** — it has one swing, not
three. Three options, in the order I would take them:

1. **Retarget two more swings onto the shared `FP_Arms` rig** from AnimSet 2-Handed Melee Weapon (owned, not yet imported). Every HQ weapon shares one arms rig, so this is a well-defined retarget rather than open-ended work.
2. **Descope MRM-23 to a two-hit combo** for Sept 1 and add the third later.
3. Switch to the BaseballBat purely for the animation count — **not recommended**, it costs the game's tone.

MRM-23 has been updated with this and the glossary now rules **Fire Axe**.

---

## R2.5 — ✅ Gaia's installer menu is not a dealbreaker. It is the mechanism we wanted

Carlos asked whether the custom menu Gaia triggers is a problem. **It is the opposite of a problem.**

`Assets/PLAYGROUND/Gaia Pro VS/` contains **only two PDFs and a `Packages - Cache` folder.**
**Gaia is not installed at all** — the entire 4.9 GB is a cache of nested `.unitypackage` files, and
the menu Carlos saw is the installer that asks which of them to install.

That means the keep/drop boundary in `terrain-vegetation-tooling-decision.md` §2 — written as
strategy — **is now an exact file list, and gap #1 of §2c is closed:**

| Install | Decline |
|---|---|
| `Gaia/Gaia.unitypackage` — the tool itself | `Gaia/Gaia Water.unitypackage` — collides with Crest |
| `Gaia/Stamps.unitypackage` — terrain shaping | `Gaia/Unity URP Water.unitypackage` · `Unity HDRP Water` |
| | `Gaia/Sky & Lighting Presets.unitypackage` — collides with AllSky + TimeManager |
| | `Gaia Pro/Procedural Worlds Sky.unitypackage` — same |
| | `Gaia/Asset Samples.unitypackage` · `Asset Samples - Synty Studios` — sample art |
| | `Gaia Pro/Gaia Pro Assets and Biomes.unitypackage` — sample biomes |
| | `Gaia Pro/GTS.unitypackage` — a whole terrain shader system; would fight `RetroTerrainLit` |
| | `Gaia/Controller Support.unitypackage` — we have MRM-8 |

**GTS is the one to be most careful about** — it is a full terrain-shader replacement and it would
directly fight the PSX terrain material. It was not visible from the store page.

---

## R2.6 — ✅ FPS Engine: rejection confirmed, and the evidence is much stronger

Round 1 rejected it on the full-body requirement. The import dialog Carlos screenshotted adds a
second, harder reason: **both FPS Engine and HQ FPS Weapons ship `ProjectSettings` overrides.**

The dialog offers to overwrite `TagManager.asset`, `GraphicsSettings.asset`, `QualitySettings.asset`,
`InputManager.asset`, `Physics2DSettings.asset`, `DynamicsManager.asset`, `EditorSettings.asset`,
`ProjectSettings.asset`, `AudioManager.asset` and `VFXManager.asset`.

**In Mr. Moonlight that would have clobbered the URP renderer assignment, the quality settings, the
physics layers and the tag/layer table** — the exact things `pc-build-target.md` and the
`unity_layers_state` note document as load-bearing and fragile. It would have been a genuinely bad
day, and the damage would have been silent.

**Rule, now recorded: when importing any of these, uncheck the entire `ProjectSettings` group.** It
applies to HQ FPS Weapons too, which we *are* adopting — see the manifest below.

---

## R2.7 — Four assets found that were not on the list

Carlos imported these without listing them. Three are genuinely useful.

| Asset | Issue | Verdict |
|---|---|---|
| **Highlight Plus 2** | **MRM-16** | ✅ **Adopt.** Ships `HighlightPlusRenderPassFeature.cs` — a URP renderer feature for object outline/glow/overlay. MRM-16 needs exactly this ("highlight, prompt, pickup") and it was going to be hand-written. ⚠️ Adds a renderer feature — register **before CRT** (Finding 2). |
| **Log Cabin** | **MRM-61** | ✅ **Adopt.** MRM-61 says *"a cabin model with all its interior props already exists"* — this is almost certainly it. Ships `Prefabs/Props`, `Prefabs/Particles`, `Source/Meshes`, `Source/Audio`. Route through `/prop`. |
| **Low Poly Plant Collections** | **MRM-70** | ✅ **Adopt.** Bushes, big-leaf plants, flowers, scatter variants. Same eye-height silhouette check as TopDown Nature. |
| **Screenspace VFX** | MRM-53 | 🟡 **Adopt selectively.** Screen-space material library — has a `Blood` set useful for MRM-53's HUD wounds, and a `Pixel-8Bit` set worth a look for the PSX treatment. Take folders, not the package. |

---

## R2.8 — Confirmations: three adopts that got stronger

**Sounds Good — better than assessed.** The fluent `Sound` API confirms every MRM-38 requirement and
adds two we did not ask for:

```
SetRandomClip()                      → the pool
SetRandomPitch(Vector2 range)        → MRM-38's pitch range
SetRandomVolume()                    → per-clip volume variation
SetHearDistance(min, max)            → 3D spatial falloff
SetVolumeRolloffCurve() / SetCustomVolumeRolloffCurve()
SetPlayProbability(float)            → ⭐ MRM-39's detection probability, free
SetFollowTarget(Transform)           → moving emitters
SetLoop / SetFadeOut / SetId
```

`SetHearDistance` + custom rolloff means **MRM-38's audible-distance sphere is largely covered by the
asset**, not just its pooling. Our remaining work is the sound *layers* and the Event Director hooks.

**Gore Simulator — confirmed, and it works on the wolf too.** It operates on generic
`SkinnedMeshRenderer` + bone hierarchies (`GoreBone`, `BonesUtility.GetDirectChildBones`) with
**no `HumanBodyBones` or humanoid-avatar dependency**. So it works on retargeted humanoids *and* on
the quadruped wolf — which Round 1 did not expect. Ships `GorePuppetSetup` (an editor setup tool),
ragdoll utilities, pooling and decals.

**Crest — install to `Packages/`, not `Assets/`.** It has a `package.json`: it is a UPM package that
was dropped into `Assets/`. That is the cause of the lingering
`DirectoryNotFoundException: ...Packages\com.waveharmonic.crest\...Settings.Crest.iOS.hlsl` in the
console. It also ships `Editor/Scripts/Integrations/Gaia/` — **an official Gaia integration**, which
is a real bonus given both are adopted. Its platform settings folder has Android / Default / Server /
**Standalone** / Web — Standalone is ours; there is no iOS file, which is what the error is about.

---

## R2.9 — URP conversion: use each package's own updater, not the global converter

Carlos suggested running **Window → Rendering → Render Pipeline Converter (Built-in → URP)**. **Do
not run that first.** It is a project-wide sweep and it will touch every material in the project,
including ones that are already correct.

Three packages ship their **own** URP updater, which is the targeted and safer fix:

| Package | Do this |
|---|---|
| **Bullet Impact VFX + Decals** | Import `URP_Updater/URP_Updater.unitypackage` |
| **Flying Birds VFX** | Import `HDRP_and_URP_Pipelines/Flying_Birds_VFX_URP.unitypackage` |
| **Blood Factory** | Already URP-compatible; its nested package is HDRP-only — ignore it |

**Then**, if individual materials are still magenta, run the global converter as a mop-up. Older
particle packs (Northern Lights, Fly Particle System, Insect VFX) are the likely candidates, and the
manual fix is remapping to `Universal Render Pipeline/Particles/Unlit`.

---

## R2.10 — Playground state after this pass

- ✅ **Compiling.** The one blocking error is fixed — Realistic Gun VFX's HDRP `ParticleCollisionSpawner.cs` referenced `DecalProjector` (HDRP-only). **Removed `GunFX/HDRP(Default` and `GunFX/Built-IN`.** We are URP; neither was usable.
- ⚠️ **One benign editor warning remains:** "custom elements added to the Unity Editor's main toolbar using unsupported methods." Source is **Blood Factory's** shared inspector framework (`Shared/Tools/PGInspector/Editor/Modules/PGModuleEditorUtility.cs`), which Gore Simulator also uses. Editor-only, cosmetic, **not a dealbreaker** — but it means adopting either one brings `PG.Shared` along.
- ⚠️ **One console exception remains:** Crest's `Settings.Crest.iOS.hlsl` path. Fixed by moving Crest to `Packages/com.waveharmonic.crest/` where it belongs. Left as-is for now since Crest is M2.

---

## R2.11 — Revised verdict table (changes only)

| Asset | Round 1 | **Round 2** | Why it changed |
|---|---|---|---|
| **Blaze AI Engine** | 🅿️ Park | ✅ **Adopt** | `BlazeBehaviour` is a 3-method extension point; covers 5 issues |
| **A\* Pathfinding Project Pro** | ✅ Adopt (M1 #1) | ❌ **Drop** *(keep in Playground as fallback)* | Mutually exclusive with Blaze; solves only navigation |
| **Altos Volumetric Clouds** | 🅿️ Park | ❌ **Reject (Island)** / ✅ **Adopt (Main Menu)** | `RenderSettings.skybox = null` — it *is* the sky |
| **Shots VFX URP** | 🅿️ Park (alternative) | ✅ **Adopt** | Native URP, has Handgun/Shotgun/Looped + muzzle light |
| **Realistic Gun VFX** | 🟡 Adopt (preferred) | ❌ **Reject** | URP content unextracted; its HDRP script broke the build |
| **Melee weapon** | Trench Club | **Fire Axe** | No club exists in the package |
| **The boss** | "Furman" (name kept) | **Wendigo** | Carlos's call, 2026-08-28 |
| **Highlight Plus 2** | *not listed* | ✅ **Adopt** → MRM-16 | URP outline renderer feature |
| **Log Cabin** | *not listed* | ✅ **Adopt** → MRM-61 | The cabin MRM-61 assumed existed |
| **Low Poly Plant Collections** | *not listed* | ✅ **Adopt** → MRM-70 | Extra biome variety |
| **Screenspace VFX** | *not listed* | 🟡 **Adopt selectively** → MRM-53 | Blood + Pixel-8Bit sets |

**Everything not listed here is unchanged from Round 1.**


---

# Part 9 — Migration manifests: exactly what moves into Mr. Moonlight

**Written 2026-08-28 from the real files.** Carlos's requirement: *"just the necessary files, so we
don't clutter the Moonlight project."*

**How to read these.** Source paths are relative to `E:\playground\My project\Assets\PLAYGROUND\`.
Destination paths are inside Mr. Moonlight. **Copy the file *and* its `.meta`** — the GUID is what
keeps references intact. Nothing here moves until its issue is actually opened.

### Five rules that apply to every migration

1. **Never copy a `Demo/`, `Demos/`, `Samples/`, `ExampleScene*/` or `Documentation/` folder.**
2. **Never accept a `ProjectSettings` override on import** — see R2.6. It silently clobbers
   `TagManager`, `GraphicsSettings`, `QualitySettings` and the URP renderer assignment.
3. **Third-party code goes to `Assets/ThirdParty/<Package>/`** (gitignored). Our own content and
   finished art goes to `Assets/_Project/` (tracked).
4. **Meshes and textures route through `/prop`. VFX does not** — Carlos's call, 2026-08-28: visual
   effects stay flexible, are exempt from the pixelation pipeline, and may overlay the CRT.
5. **Record the version in `Docs/external-assets.md`** at the moment of the copy, not later.

---

## ⭐ Blaze AI Engine → MRM-27 / 28 / 29 / 30 / 31 / 34 *(M1, do first)*

**Copy:**
```
Blaze AI Engine/Scripts/          →  Assets/ThirdParty/BlazeAI/Scripts/
Blaze AI Engine/Tags&Layers.preset →  Assets/ThirdParty/BlazeAI/
```
**Drop:** `Demos/` (Standard RP + URP demo scenes), both PDFs, `Readme.md`.

**Setup order — do not improvise this:**
1. **Apply `Tags&Layers.preset` first.** Blaze needs its own tags/layers, and this preset is the
   *safe* way to add them — unlike FPS Engine, Blaze does **not** ship a ProjectSettings override.
   ⚠️ Our project currently has only Default / TransparentFX / Ignore Raycast / Water / UI / Ground,
   so **diff the layer list before and after** and record it — layer order is load-bearing for
   terrain splatmaps and footstep surfaces.
2. Bake a `NavMeshSurface` **bounded to the walkable area** (`player walkable area.png`), not the
   whole 4103 × 7085 m terrain.
3. One agent: `BlazeAI` + `NavMeshAgent` + `NormalStateBehaviour`, walking a waypoint route on a
   slope.
4. Confirm terrain trees register as obstacles. **Same open risk as A\*** — if they do not, add a
   collider pass.
5. Then write our custom `BlazeBehaviour` subclasses for the specs Blaze does not cover.

**What we still write ourselves** (as `BlazeBehaviour` subclasses — `Open()` / `Main()` / `Close()`):
the three-blind-run search (MRM-29), the wolf circle formation (MRM-33), the Zealot's curved sneak
and backstab (MRM-35).

---

## ⭐ HQ FPS Weapons 2.0 → MRM-22 / 23 / 24 / 25 / 26 / 34 / 44 / 52 *(M1, do second)*

**The package is 3.9 GB. We need roughly 500 MB of it, and none of its code.**

**Copy — art only, per weapon, three FBX each:**
```
HQFPS/Art/Meshes/Wieldables/Arms/                  → the shared FP arms rig  ⚠️ REQUIRED FIRST
HQFPS/Art/Meshes/Wieldables/M1911/                 → MRM-22
HQFPS/Art/Meshes/Wieldables/DoubleBarrelShotgun/   → MRM-24
HQFPS/Art/Meshes/Wieldables/FireAxe/               → MRM-23
HQFPS/Art/Meshes/Wieldables/FlareGun/              → MRM-34
HQFPS/Art/Meshes/Wieldables/Flashlight/            → MRM-44  ⭐ bonus, we had no model
                                → Assets/_Project/Art/Weapons/<Weapon>/
```
Each folder contains exactly three FBX plus `Materials/`:
- `<Weapon>.fbx` — world / pickup model (MRM-26)
- `FP_<Weapon>.fbx` — first-person weapon model
- `FP_Arms_<Weapon>.fbx` — **first-person arms + weapon, animations embedded**

**Optionally also:** `HQFPS/Audio/` (weapon SFX) and `HQFPS/Art/Sprites/Inventory/` (item icons for
MRM-42) — check these against Carlos's own art direction before taking them.

**Drop everything else, explicitly:**
- ❌ **`FPSCore/` entirely** — the framework (PolymindGames, OdinSerializer, EditorToolbox). We use none of it.
- ❌ All 13 other wieldables (AKM, Crossbow, MP5, Revolver, grenades…) — not in the demo.
- ❌ `HQFPS/Demo/`, `HQFPS/Prefabs/`, `HQFPS/Data/`, `HQFPS/Integrations/`.
- ❌ **The `ProjectSettings` override group.** See R2.6 — this is the one that would have hurt.

**Animations shipped, verified:**

| Weapon | Clips |
|---|---|
| **M1911** | Equip · Unequip · Idle · Hold · Fire · **AimFire** · Reload · **ReloadEmpty** |
| **DoubleBarrelShotgun** | Equip · **Holster** · Idle · Hold · Fire · **AimFire** · Reload · ReloadEmpty |
| **FlareGun** | Equip · Unequip · Idle · Hold · Fire · AimFire · Reload |
| **Flashlight** | Equip · Unequip · Idle · Hold · **Switch** |
| **FireAxe** | Equip · Unequip · Idle · Hold · **StrongAttack** ⚠️ *one attack only* |

MRM-21's ADS is covered by the `AimFire` clips. MRM-25's equip/unequip is covered for every weapon.

---

## ⭐ Sounds Good → MRM-38 / 39 / 65 *(M1, do third)*

**Copy:**
```
Sounds Good/Runtime/  →  Assets/ThirdParty/SoundsGood/Runtime/
Sounds Good/Editor/   →  Assets/ThirdParty/SoundsGood/Editor/
Sounds Good/package.json
```
**Drop:** `Samples/` (includes `SoundsGood.Demo.asmdef`), `README.md`, `CHANGELOG.md`.

It has a `package.json`, so it can alternatively live at `Packages/com.melenitas.soundsgood/`.
⚠️ **`Packages/` is not gitignored** — that would commit it. Prefer `Assets/ThirdParty/`.

**Setup:** create the audio outputs and route them to the **existing** mixer groups
(`AudioMixerVolume.cs`, MRM-18's sliders). Do not let it build a parallel mixer. Then wrap it behind
our `SoundPool` ScriptableObject so callers do not change.

---

## ⭐ Shots VFX URP → MRM-22 / 24 / 52 *(M1, do fourth)*

**Copy — muzzle flashes and shared deps only:**
```
Shots VFX URP/Shots VFX URP/Shots/Muzzle Flash/   → Assets/_Project/Art/VFX/MuzzleFlash/
Shots VFX URP/Shots VFX URP/Shared/               → Assets/_Project/Art/VFX/_ShotsShared/
```
Take `Once/` (Handgun → M1911, Shotgun → DB), `Looped/` (Rifle → the Type 92's sustained fire) and
`Light/VFX_Point_Light_01` (the muzzle light — our standing rule says glowing things get a real
Light). `Shared/` carries the materials, meshes, shaders and textures the prefabs reference — **copy
it or the prefabs come across pink.**

**Drop:** `Demo/`, `Documentation/`, `Audio/`, `Others/`, `Shots/Squibs/`,
`Shots/Props Destruction/`, and **`Shots/Decals/`** — decals come from Bullet Impact VFX instead, per
Carlos's split.

**Not adopted:** Realistic Gun VFX. Its URP content is an unextracted nested `.unitypackage` and its
HDRP script broke the build. Left in Playground for a visual comparison only.

---

## Spice Up ×4 · Artistic Radial Blur · Drunk Color Pulse → MRM-49 / 53 / 54 / 55 *(M2)*

All six are tiny (1–28 MB) and structurally identical — a `Runtime/` (the renderer feature +
Volume component) and an `Editor/`.

**Copy per package:**
```
<Package>/SpiceUp/<Effect>/Runtime/  →  Assets/ThirdParty/FronkonGames/<Effect>/Runtime/
<Package>/SpiceUp/<Effect>/Editor/   →  Assets/ThirdParty/FronkonGames/<Effect>/Editor/
```
(Artistic Radial Blur uses `Artistic/RadialBlur/` instead of `SpiceUp/<Effect>/`.)

**Register all of them on `PC_Renderer` in one sitting, in the Finding 2 order, CRT last.** Doing
this six separate times invites six separate ordering arguments.

⚠️ **Carlos's relaxation, 2026-08-28:** if an effect ends up drawing over the CRT, that is acceptable
for demo quality. The ordering rule is still the default — it just is not worth a fight.

---

## Gore Simulator + Blood Factory → MRM-32 / 36 / 37 *(M2)*

**Copy:**
```
Gore Simulator/GoreSimulator/{Scripts,Content,Editor}/  → Assets/ThirdParty/GoreSimulator/
Blood Factory/BloodFactory/{Scripts,Editor,...}/        → Assets/ThirdParty/BloodFactory/
Blood Factory/Shared/                                   → Assets/ThirdParty/PGShared/   ⚠️ REQUIRED
```
**Drop:** both `Demo/` folders, `BloodFactory_HDRP.unitypackage`.

⚠️ **`Blood Factory/Shared/` (`PG.Shared`) is a shared dependency of both packages** — it is not
optional, and it is the source of the benign "custom main toolbar" editor warning. Take it once.

**Confirmed:** no humanoid-avatar requirement — it works on any `SkinnedMeshRenderer` + bone
hierarchy, **including the quadruped wolf.** Use `GorePuppetSetup` (its editor window) to define cut
points, and do it **in the same pass as MRM-32's hitboxes** — same rig, same sitting.

---

## Highlight Plus 2 → MRM-16 *(M1/M2)* — new in Round 2

**Copy:**
```
Highlight Plus 2/Runtime/  →  Assets/ThirdParty/HighlightPlus/Runtime/
Highlight Plus 2/Editor/   →  Assets/ThirdParty/HighlightPlus/Editor/
```
**Drop:** `Demo/`, `Documentation/`, `README.txt`.

Register `HighlightPlusRenderPassFeature` on `PC_Renderer` **before CRT**. This replaces the
hand-written highlight MRM-16 planned. Profile the effect down — default outline/glow settings are
far louder than a 1979 horror game wants.

---

## Crest Water 5 → MRM-71 *(M2)*

⚠️ **Install to `Packages/com.waveharmonic.crest/`, not `Assets/`.** It has a `package.json`; the
`Settings.Crest.iOS.hlsl` exception in the Playground console is exactly the symptom of it sitting in
the wrong place.

**Copy:** `Runtime/`, `Editor/`, `Shared/`, `package.json`.
**Drop:** `Samples~/`, `Documentation~/` (tilde folders do not import anyway).

Its platform settings ship Android / Default / Server / **Standalone** / Web — **Standalone is ours.**
There is no iOS file; that is the error, and it is harmless once the package is located correctly.

⭐ **It ships `Editor/Scripts/Integrations/Gaia/`** — an official Gaia integration. Given both are
adopted, use it rather than hand-placing water against the terrain.

⚠️ `Packages/` is **not** gitignored — Crest will be committed to the repo (~76 MB). Still Carlos's
call; unchanged from Round 1, MRM-71 risk 1.

---

## Gaia Pro VS → MRM-70 *(M2, after Sept 1)*

**Do not copy folders.** Gaia is not installed even in Playground — the 4.9 GB is installer cache.
Copy `Gaia Pro VS/Packages - Cache/`, run the Gaia Manager, and **install only:**

| ✅ Install | ❌ Decline |
|---|---|
| `Gaia/Gaia.unitypackage` | `Gaia/Gaia Water` · `Unity URP Water` · `Unity HDRP Water` |
| `Gaia/Stamps.unitypackage` | `Gaia/Sky & Lighting Presets` · `Gaia Pro/Procedural Worlds Sky` |
| | `Gaia/Asset Samples` · `Asset Samples - Synty Studios` |
| | `Gaia Pro/Gaia Pro Assets and Biomes` · **`Gaia Pro/GTS`** · `Gaia/Controller Support` |

⚠️ **GTS is the dangerous one** — a complete terrain-shader system that would fight
`RetroTerrainLit`. It was not visible from the store page.

> **⚠️ PARTIAL REVERSAL, 2026-08-31 — `Gaia Pro Assets and Biomes` is no longer a blanket decline.**
> The package is still **not installed** and the decline stands as a *package* decision: it is 3.7 GB
> of realistic art in the wrong style, and running its installer would drag in biomes, spawners and
> materials we do not want.
>
> But it is now a **source we extract single files from**, the same way AllSky was. On 2026-08-31,
> 14 files were pulled out of it by GUID (10 grass detail FBX + 2 texture atlases + 2 normals) for
> the terrain-detail grass tier, pixelated through `Tools/pipeline/texture_pass.py` and rebuilt as
> `GRASS_Gaia_*` prefabs under our own art direction. Nothing from the package ships as-is.
>
> **Rule going forward:** do not install it; *do* treat it as a library to extract from when it holds
> something we would otherwise have to make. Still available and unextracted: 11 more `PW_LawnGrass`
> meshes and 16 legacy grass billboard cards.
> See `Docs/mrm70-unused-vegetation-inventory.md` §6.4.

**Save spawn/biome recipes into `Assets/_Project/`**, strip Gaia components off the Terrain before
removing the package, and pin the version.

---

## Environment, prop and VFX packs → MRM-59 / 60 / 61 / 70 *(M2)*

Per-package, take the meshes/prefabs you actually place; drop `Demo`/`Scene`/`Documentation`.

| Package | Take | For |
|---|---|---|
| **Log Cabin** ⭐new | `Prefabs/Props/`, `Prefabs/Particles/`, `Source/Meshes/`, `Source/Materials/` | **MRM-61** — the cabin MRM-61 assumed already existed |
| **Sewer Underground Modular** | modular tunnel pieces | MRM-60 |
| **Abandoned Hospital** | infirmary props | MRM-60 (the infirmary is *inside* the mine) |
| **Aged Medieval PBR Tools** | **the sickle only** | MRM-35 |
| **Low Poly Plant Collections** ⭐new | `Meshes/` + `Material/` | MRM-70 |
| **TopDown Nature** | selected plants — ⚠️ **check silhouette at eye height first** | MRM-70 |
| **Ian's Fire Pack** · **HQ Realistic Explosions** | 2–3 effects each, not the libraries | MRM-61 · MRM-57 |
| **Insect VFX** · **Northern Lights** · **Fly Particle System** | the effect prefabs — ⚠️ **strip bundled lights** | MRM-60/61 · MRM-47 · MRM-60 |
| **Flying Birds VFX** | ⚠️ **import `Flying_Birds_VFX_URP.unitypackage` first** | MRM-59 |
| **Bullet Impact VFX** | `Bullet_Hit_FX/`, `Bullet_Hole_Decals/`, `Bullet_Impact_Sounds/` — ⚠️ **run `URP_Updater.unitypackage` first** | MRM-22/24/39 |
| **Screenspace VFX** ⭐new | `Materials/Blood/` and `Materials/Pixel-8Bit/` only | MRM-53 |
| **Procedural Lightning** | `Prefab/` + its scripts; drop `Demo/` | MRM-57 · MRM-36 |
| **Body Poser** | `Scripts/`, `Prefabs/`, `Materials/`; drop `ExampleScene/`, `StarterAssets/` | MRM-59 — editor-only |
| **URP Wet Shaders** | the shaders + materials | MRM-18 |
| **Altos** | ⚠️ **main menu scene only** — never on the Island | MRM-18 |

⚠️ **Volumetric Light Beam is an unextracted installer** (`Installer.asset` +
`VolumetricLightBeam.unitypackage`). Do not plan around it until it is extracted — and per R2 it is
conditional on HAZE alone proving insufficient anyway (MRM-44).

---

## Staying in Playground — not migrating

| Package | Why it stays |
|---|---|
| **FPS Engine (cowsins)** | Rejected. Read-only reference for recoil/sway/crosshair patterns |
| **A\* Pathfinding Project Pro** | Superseded by Blaze AI. **Keep as the fallback** if NavMesh baking fails on the island |
| **Realistic Gun VFX** | Superseded by Shots VFX URP |
| **Ambient Sounds** | Rejected — MRM-38's layers already are a zone system |
| **Asset Cleaner Pro** | Editor-only; run it *in Playground*, never point its delete at Mr. Moonlight |
| **Skybox Blender** | MRM-47 chose instant, story-hidden swaps |

---

# R4 — Animation tooling, ruled 2026-08-31. **Full detail: `Docs/retarget-pro-strategy.md`**

Carlos asked which of two animation assets to buy. Both were read against the real files in
Playground, not the store pages. **Verdict: Retarget Pro V5 in, FPS Animation Baker Toolkit out** —
but the reasoning inverted along the way, so read R4.1 before R4.2.

## R4.1 — ⚠️ The premise for buying *any* FPS animation tool is dead

Both `Toolkit.md` and this document justify the Baker Toolkit with *"roughly 15 hand animations
across the demo"* that Claude cannot author. **Those 15 animations are already owned.** Enumerated
from the FBXs on 2026-08-31 — every clip below is on the one shared `FP_Arms` skeleton (R3.1):

| Issue | Weapon in the issue | Clips already shipped |
|---|---|---|
| **MRM-22** Pistol | **M1911** | 8 — AimFire, Equip, Fire, Hold, Idle, Reload, ReloadEmpty, Unequip |
| **MRM-24** Shotgun | **DoubleBarrelShotgun** | 8 — AimFire, Equip, Fire, Hold, Holster, Idle, Reload, ReloadEmpty |
| **MRM-23** Club | BaseballBat + FireAxe | 6 + 5 — the three swings, already ruled in R3.2 |
| **MRM-25** Switching | every weapon | Equip / Unequip / Holster |
| **MRM-21** ADS | every gun | Aim / AimFire |
| **MRM-43** Map + compass | Flashlight, Syringe | Equip / Hold / Idle / Switch / Use |

HQ FPS Weapons ships 19 weapons in total (AKM, Crossbow, F1, FlareGun, FragGrenade, HuntingRifle,
M1A, MP5, MolotovCocktail, R870, Revolver + the above). **MRM-21/22/23/24/25/43 need no new
animation and no animation tool.** They need the migration in MRM-23 and our own gameplay code.

## R4.2 — ❌ FPS Animation Baker Toolkit — rejected

Primary reason: R4.1. Secondary: CatRabbit, **v1.0**, May 2026, **zero reviews**, no feature list on
the store page, no public documentation (it is inside the package), no forum or search footprint. A
bet that cannot be de-risked before buying, 1 day before an M1 gate. **This reverses `Toolkit.md`'s
"✅ Buy — the clearest buy on the list" verdict**, which was written before HQ FPS Weapons was inventoried.

## R4.3 — ✅ Retarget Pro V5 (KINEMATION) — adopted, Playground-only

Not for weapons. For the three things we genuinely cannot do today:

1. **The wolf (MRM-33) is a quadruped.** Unity Humanoid retargeting cannot target a quadruped at
   all — there is no workaround. This is the one hard capability gap, and only this tool fills it.
2. **The Wendigo (MRM-36)** is `animationType = Human` on a **UE mannequin skeleton** (`root/pelvis/
   spine_01/clavicle_l/upperarm_l/...`, plus `ik_hand_gun`). Unity's retargeting *works*, but on a
   creature with deliberately non-human proportions it is exactly the case that produces sliding feet
   and rubbery spines. Retarget Pro maps per bone-chain with IK correction, and ships `A_TPose_UE4` /
   `A_TPose_UE5` presets that match this skeleton exactly.
3. **Tracey's full body** — MRM-9's *"looking straight down shows body geometry"* criterion is
   recorded as **not met** and owed back once a real Tracey model exists.

**Plus a direct hit on a documented build-size risk.** This document flags Ultimate Animation
Collection as *"one of the three real build-size risks — import only the clips actually used."* It
holds **3,068 clips across 3,121 models**. Retarget Pro's batch bake turns that warning into a
procedure: bake the ~20 clips we want in Playground, migrate only those, never import the library.

**The adoption rule: it never enters Mr. Moonlight.** It is an editor tool; its `.Editor` asmdefs are
Editor-platform-only and the runtime component is never placed on anything. Clips are baked in
Playground and only the resulting `.anim`/`.fbx` cross over → **zero build footprint**, and the baked
clips keep working even if the tool never opens again. Same shape as the Gaia Pro *editor-tools-only*
adoption.

**⛔ Do not let this pull in KINEMATION's FPS Animation Framework.** That is the *runtime* sibling and
would reopen MRM-9/12/21/22/25 exactly as FPS Engine (cowsins) would (§1). Retarget Pro is the
editor-only half.

## R4.4 — Correction to R3.1

R3.1 states: *"Unity retargeting only works between Humanoid avatars, so no third-person full-body
animation can be retargeted onto these arms."* **True of Unity; no longer true of the project.**
`RetargetProfile` has no `HumanBodyBones` dependency — it composes rig data from the model hierarchy,
so Humanoid → Generic and arms-only targets are supported. It is **moot for MRM-23**, which R3.2
already solved with owned clips, but it is no longer a structural wall for any future weapon.

---

# R3 — HQ FPS Weapons: verified manifest from the isolated weapon project

**2026-08-28.** HQ FPS Weapons was too invasive to live alongside the other packages, so Carlos gave
it its own project: **`E:\playground\weapon`, MCP port 8082** (registered in `.mcp.json` as
`unityMCP_weapons`). Everything below was read from the real files there.

## R3.1 — The single most useful fact: all 19 weapons share ONE skeleton

Every `FP_Arms_<Weapon>.fbx` has `avatarSetup: 2` (**Copy From Other Avatar**) pointing at the same
source — guid `81dcb03d49282f446b0aaf2b603ea7e8`, which is `Arms/FP_Arms.fbx`.

**Consequences, and they are all good:**

1. **Any weapon's animation plays on any other weapon's rig.** They are one arms skeleton with 19
   weapon meshes attached.
2. **`Arms/FP_Arms.fbx` migrates once** and every weapon copies its avatar from it.
3. It settles the hand-rig question that MRM-22/25 were going to have to decide — **the rig is
   already decided, by the asset.**

> ### ⚠️ The rig is **Generic**, not Humanoid (`animationType: 2`)
>
> This is the constraint that governs every animation decision on the player's weapons.
> **Unity retargeting only works between Humanoid avatars**, so **no third-person full-body
> animation can be retargeted onto these arms.** Concretely:
>
> **AnimSet 2-Handed Melee Weapon cannot supply the third swing.** It is a third-person, full-body,
> Humanoid set; `FP_Arms` is a Generic arms-only rig with no hips, spine or legs to map to. This was
> the plan in Round 1 and Round 2 and **it does not work.** Removed from MRM-23.

## R3.2 — The third vertical strike, solved for free

MRM-23 needs three swings. The BaseballBat ships **two**:

| Clip | Frames | Use |
|---|---|---|
| `Arm_BaseballBat_ComboAttack_` | 33 | Swing 1 — horizontal |
| `Arm_BaseballBat_StrongAttack_` | 38 | Swing 2 — horizontal |
| **`Arm_FireAxe_StrongAttack_`** | **60** | **Swing 3 — the vertical overhead** |

Because the skeleton is shared (R3.1), **the FireAxe's overhead chop plays on the Club rig with no
retargeting, no Blender, and no extra asset.** An axe chops downward; with a bat in hand the same
arm motion reads as an overhead smash. It is also the longest of the three at 60 frames, which
suits MRM-23's "third swing does visibly more damage".

**Migrate `FireAxe/FP_Arms_FireAxe.fbx` for its animation only** — not its mesh, not its materials.

**Risk, and the fallback.** The axe is gripped differently from the bat, so the bat may sit at a
slightly wrong angle mid-swing. **Check it visually first — it is a five-minute test.** If it reads
badly, the fallback is authoring one overhead swing on the FP_Arms rig in Blender (the rig is right
there and it is ~30 keyframes on an arms-only skeleton). Do **not** reach for AnimSet; see R3.1.

## R3.3 — ✅ The URP conversion problem is moot. Do not solve it.

The weapon materials use the **Built-in Standard shader** (`m_Shader: {fileID: 46, guid:
0000000000000000f000000000000000}`), which is why they look wrong in URP and why the Render Pipeline
Converter was frustrating.

**We do not need them.** `RetroLit` samples **BaseColor + Normal only**, and the prop wizard builds a
**fresh** RetroLit material per weapon. Every source `.mat` is discarded on the way in.

**So: skip the Render Pipeline Converter for the weapons entirely.** Run it only if you want the
weapons to *look* right inside the weapon project while browsing them — it is cosmetic, not a
prerequisite. This removes a blocked task rather than scheduling one.

## R3.4 — The exact migration manifest

Root: `E:\playground\weapon\Assets\PolymindGames\HQFPS\Art\Meshes\Wieldables\`

**Copy the file *and* its `.meta`** (the `.meta` carries the clip definitions and the avatar link —
without it the animations are lost and the avatar copy breaks).

### Once — the shared rig

```
Arms/FP_Arms.fbx                          ← the shared avatar. Migrate FIRST.
Arms/Materials/Arm_Standard.png           ← BaseColor
Arms/Materials/Arm_Standard_NRM.png       ← Normal
```

### Per weapon

| Weapon | Issue | Take |
|---|---|---|
| **M1911** | MRM-22 | `M1911.fbx` · `FP_Arms_M1911.fbx` · `Materials/M1911.png` · `Materials/M1911_NRM.png` |
| **Double-barrel** | MRM-24 | `DBShotgun.fbx` · `FP_Arms_DoubleBarrelShotgun.fbx` · `Materials/DBShotgun.png` · `Materials/DBShotgun_NRM.png` |
| **Club (BaseballBat)** | MRM-23 | `BaseballBat.fbx` · `FP_Arms_BaseballBat.fbx` · `Materials/BaseballBat.png` · `Materials/BaseballBat_NRM.png` |
| **Flare gun** | MRM-34 | `FlareGun.fbx` · `FP_Arms_FlareGun.fbx` · `Materials/FlareGun.png` · `Materials/FlareGun_NRM.png` |
| **Fire axe** | MRM-23 | `FP_Arms_FireAxe.fbx` **only** — for `Arm_FireAxe_StrongAttack_`. No mesh, no textures |
| **Flashlight** *(bonus)* | MRM-44 | `Flashlight.fbx` · `FP_Arms_Flashlight.fbx` · `Materials/Flashlight*.png` + `_NRM` — we had no flashlight model |

### Explicitly DO NOT take

- **`_AO`, `_MaskMap`, `_MET`, `_EMM` textures** — RetroLit samples neither. AO is multiplied into the albedo by the pipeline. This drops each weapon from 5–6 textures to **2**.
- **Every `.mat` file** — replaced by fresh RetroLit materials.
- **`FP_<Weapon>.fbx`** — the weapon-only first-person mesh. `FP_Arms_<Weapon>.fbx` already contains it with the arms. *Check before deleting; if the prefab wants a separate weapon transform, take it.*
- **`FPSCore/` — the entire folder.** This is the PolymindGames framework: OdinSerializer, EditorToolbox, the whole wieldable/inventory/UI runtime. **None of it enters Mr. Moonlight.**
- **`HQFPS/Prefabs/`** — the pickup prefabs (`HQFPS_Pickup_BaseballBat` etc.) reference Polymind components. Use them as a **visual reference for how the weapon is posed**, then build our own prefabs.
- **`HQFPS/Demo/`, `Integrations/`** (BIRP, HDRP, EmeraldAI).

### ⚠️ The import trap, for both HQ FPS Weapons and FPS Engine

Both ship a **`ProjectSettings` override group** in their import dialog — `TagManager`,
`GraphicsSettings`, `QualitySettings`, `InputManager`, `Physics`, `AudioManager`. Importing it
**overwrites project-wide settings.** In Mr. Moonlight that would clobber the URP renderer
assignment and the layer list, and the breakage would be silent and horrible to trace.

**Always click `None` on that group.** This is why the weapon project exists — so the question never
comes up again.

## R3.5 — Audio, as a bonus

`HQFPS/Audio/SFX/Wieldables/<Weapon>/` has per-weapon fire/reload/equip sounds for the M1911 and the
FlareGun. Worth auditioning for MRM-22/34 before Carlos records anything — but **import compressed**
(audio is one of the three real build-size risks).
