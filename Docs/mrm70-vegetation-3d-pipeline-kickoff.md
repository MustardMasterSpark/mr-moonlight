# MRM-70 — Vegetation 3D pipeline kickoff

**Written 2026-08-29, end of the Gaia-planning session.** Read this first in the new chat that
runs the 3D prop pipeline over the vegetation assets. It exists so that session doesn't re-derive
what this one already settled, and doesn't repeat two mistakes just caught in the pipeline docs.

## What happened this session, in order

1. **Explained Gaia's Spawner system to Carlos** in plain terms — Spawner/SpawnRule/PolyMask,
   weighted resolution modes as the percentage-mix mechanism for his biome content lists.
2. **Removed the custom slope-slide mechanic from `PlayerController.cs`.** The `onSteepSlope` +
   `SlideSpeed` logic (added MRM-58, meant to stop jump-climbing steep terrain) is gone. The
   controller now just relies on the stock `CharacterController.slopeLimit` — no custom sliding.
   `SlideSpeed` deleted from `MoonlightTunables`. Compiles clean (verified via
   `mcp__UnityMCP__refresh_unity` + `read_console`). **Not yet build-verified** — editor
   verification is known-unreliable on this project; Carlos should confirm movement feel in a
   real build before this is considered done.
3. **Answered Carlos's Gaia questions** about Flora rendering, collision, and editability — the
   short version: Gaia's Spawner can output either **Terrain Tree/Detail** (native `TerrainData`,
   same mechanism the old custom tool used, which is exactly what `FloraTerrainProvider` reads —
   Flora renders it, same performance) or **Game Object** (real GameObjects, individually
   editable, but *not* batched by Flora). Bulk biome vegetation should use Terrain Tree/Detail;
   anything needing hand-editing (like the barrier walls, already decided) should be a GameObject.
4. **Confirmed Gaia is already imported and scriptable** — `Assets/Procedural Worlds/.../Gaia/
   Scripts/Core/Spawning System/` has `Spawner.cs`, `SpawnRule.cs`, `BiomeController.cs`, all
   plain C#. Building Spawners/rules from Carlos's asset+percentage list via `execute_code` is
   feasible, not just theoretical — untested hands-on, but the API surface exists.
5. **Carlos is compiling a per-biome list**: asset/prefab × relative frequency, per biome. Before
   handing it over, he wants to run the 3D pipeline first, because a lot of the assets that list
   will name currently live in the **Playground** project, not as finished Mr. Moonlight
   prefabs.
6. **While preparing this handoff, found and fixed two real bugs in the pipeline docs** — see
   below. Both would have bitten on the very first vegetation prop otherwise.

## Fixed in the pipeline docs this session — read the diffs, not just this summary

- **`SKILL.md` had "Bulk runs" contradicting its own shakedown banner.** Nothing has cleared
  shakedown yet (`Docs/prop-log.md` is empty) — the wizard's explicit rule is "never batch, one
  prop only" until a path finishes end-to-end with Carlos's sign-off. A vegetation list is
  inherently a bulk request. **Fixed**: bulk runs are now explicitly gated on shakedown clearing.
  **This is the one thing most likely to matter in the next session** — the first vegetation
  species through the pipeline is a shakedown run, not the start of a batch. Narrate every step,
  stop at every boundary, one prop, before touching the rest of the list.
- **`SKILL.md` had a stale Playground path** (`E:\playground\test`, pre-2026-08-28-move). Fixed
  to `E:\playground\My project`.
- **Added the eye-height silhouette check** that `Docs/new-asset-list.md` promised for
  **Topdown Nature Library (1000)** and **Low Poly Plant Collections** — both adopted for MRM-70,
  both authored to be seen from above, so undersides/sides are often unfinished or flat
  billboards. Was never actually written into the wizard until now.
- **Added the Terrain-Tree collider exception carve-out**: the wizard's general "Mesh Collider
  when the silhouette matters" rule does not apply to anything meant to be spawned via Gaia as a
  Terrain Tree — those silently reject Mesh Colliders outright, no exception. Stick to
  Capsule/Box/Sphere, or spawn it as a GameObject instead if the silhouette really demands a mesh
  collider.
- Full detail in `Docs/3d-prop-pipeline-wizard.md` §12, gaps **G12–G15**.

Full list of what's adopted for MRM-70 vegetation and why: `Docs/new-asset-list.md` (search
"Topdown Nature Library" and "Low Poly Plant Collections").

## What's still open, not addressed this session

- **Biome region boundaries are still undefined** on the current (2026-08-29, World-Designer-built)
  terrain. Carlos's asset+frequency list gives *content*, not *placement* — each biome still needs
  either a hand-drawn Gaia PolyMask or a re-projected `biomes.png`-equivalent before Spawners can
  actually run. Not blocking for the 3D-pipeline session, but don't let it get forgotten once
  prefabs exist and the temptation is to jump straight to spawning.
- **The 9 location blockouts** (Camp/Dock/Glade/Cabin/Mine Entrance/Flak Tower/Mine Exit/Well/
  Chapel) still sit at positions from the *original* terrain, never repositioned across either
  regeneration. Biome regions partly anchor to them, so this matters before regions get drawn.
- Carlos's actual asset+frequency-per-biome list has not been handed over yet — that's the step
  right after this pipeline session finishes producing real prefabs.

## Standing rules, unchanged

- **Ask Carlos before touching Unity or Blender**, then do it, verify by reading real
  component/scene/mesh state back, document what changed. `CLAUDE.md` hard rule, extended to
  Blender MCP work.
- **Shakedown discipline is not optional for the first vegetation prop** — see above. This
  overrides the general "bulk runs over a list" framing in the `prop-wizard` skill's own
  description until the static-prop path clears.
- No hardcoded values except vegetation/staging numbers, which stay out of `MoonlightTunables`
  until a real perf problem shows up (`feedback_tunables_during_prototyping` memory).
- Verification of anything gameplay- or rendering-facing means a real build — editor screenshots
  and `UnityStats` have given false readings on this project more than once.

## Prompt to paste in the new chat

> Resuming **Mr. Moonlight**, branch `mrm-70`. Read `CLAUDE.md` first, then
> **`Docs/mrm70-vegetation-3d-pipeline-kickoff.md`** (this file) in full — it records what the
> previous session settled and two bugs just fixed in the pipeline docs. Then read
> **`.claude/skills/prop-wizard/SKILL.md`** and **`Docs/3d-prop-pipeline-wizard.md`** §0.5, §12
> (gaps G10–G15 especially), and **`Docs/new-asset-list.md`**'s Topdown Nature Library / Low Poly
> Plant Collections rows.
>
> **Goal this session:** run the 3D prop pipeline (`/prop`) over the vegetation assets Carlos
> needs for MRM-70's biomes, most of which currently live in the **Playground** project
> (`E:\playground\My project`) rather than as finished Mr. Moonlight prefabs.
>
> **Do not batch.** Nothing has cleared shakedown yet (`Docs/prop-log.md` is empty) — the first
> vegetation species through this pipeline is a shakedown run of the static-prop path: narrate
> each step, stop at every boundary, one prop only, until Carlos signs off on it end to end. Only
> then does the rest of the list run normally.
>
> Ask Carlos which specific species/assets to start with, and confirm the Playground → Mr.
> Moonlight transfer step (`Docs/dual-project-workflow.md`) before running the wizard's own
> steps. Standing rule: ask permission before Unity/Blender MCP work, then do it, verify by
> reading real state back, document.
>
> Carlos is holding a per-biome asset+frequency list for the actual Gaia Spawner setup — that
> comes **after** this pipeline session, once real prefabs exist. Don't start on Gaia Spawner
> configuration in this session unless he explicitly redirects.
