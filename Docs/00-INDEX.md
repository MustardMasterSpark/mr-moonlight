# Mr. Moonlight — Project MDs (Claude Code toolkit)

**What this is.** The technical ground rules for the Unity project. Read these before writing code, not after.

**Where these belong.** Copy this folder into the repo at `E:\MrMoonlight\Docs\`. Reference them from `CLAUDE.md` so Claude Code picks them up automatically.

| File | Read it when | Priority |
|---|---|---|
| `pc-build-target.md` | **Before anything.** The Windows 64-bit / 1920×1080 target, player settings, quality settings, and the third-party rendering stack | 🔴 **Read first** |
| `webgl-constraints.md` | ~~Before anything~~ — **HISTORICAL as of 2026-08-25.** WebGL is no longer a target; do not apply its rules to new work | ⚪ Historical |
| `mrm70-biome-distribution-measured.md` | **Before any Gaia vegetation spawn.** Measured per-biome species distribution for all 9 biomes — spacing, weights, slope/altitude rules, clustering, and a Gaia field-by-field execution guide. Supersedes the GPT biome report's numbers and `mrm70-biome-vegetation-strategy.md` §3-4 | 🔴 **Read first for vegetation** |
| `terrain-vegetation-tooling-decision.md` | Any terrain, biome, vegetation or vegetation-renderer question — which assets we use and, more usefully, which we rejected and why | 🟡 Reference |
| `unity-conventions.md` | Any Unity work — folders, prefabs, ScriptableObjects, scenes, the tunables pattern | 🔴 High |
| `csharp-conventions.md` | Any C# — naming, structure, the no-hardcoded-values rule, performance patterns | 🔴 High |
| `3d-prop-pipeline-wizard.md` | **Any prop, character or weapon work — read this first.** The executable wizard (`/prop`): four paths, the two-map RetroLit standard, the Unity finish, and the write-back protocol that makes each prop faster than the last | 🔴 **Read first for assets** |
| `retarget-pro-strategy.md` | **Before any character, enemy or weapon *animation* work.** The 2026-08-31 ruling: Retarget Pro V5 adopted (Playground-only, zero build footprint), FPS Animation Baker Toolkit rejected, and the verified proof that every FP weapon animation the demo needs is **already owned** | 🔴 **Read first for animation** |
| `prop-log.md` | One entry per finished prop — what it cost, what was learned. Glance at it before starting a prop | 🟡 Reference |
| `3d-asset-pipeline.md` | Blender export conventions, baking mechanics, UVs, poly reduction, Unity mesh/texture import settings, LOD rules, vegetation budget. ⚠️ **Its §2 map set is superseded** by the wizard's §2 | 🔴 High |
| `system-architecture.md` | Understanding how systems talk to each other. Contains the dependency and interaction diagrams | 🟡 Reference |
| `data-schemas.md` | Anything spreadsheet-driven — dialogue, system messages, objectives, the event script | 🟡 When building those systems |
| `changelog.md` | Picking up any issue — what actually got built, decided, or failed, per issue, newest first | 🟡 Reference |
| `external-assets.md` | **Before proposing any Asset Store package** — what is owned, installed, and explicitly rejected, plus how to restore a clean machine | 🟡 Reference |
| `new-asset-list.md` | **Before starting any issue that names an asset.** The 2026-08-27 triage of the 47-package batch: take/park/reject per asset with reasoning, the three cross-cutting conflicts (camera shake, post-process order, navigation fork), and a **per-asset integration brief** written to be read cold | 🔴 **Read first for assets** |
| `dual-project-workflow.md` | Working with Playground (`E:\playground\My project`), the sandbox project for testing new/bulk-imported assets before they enter Mr. Moonlight — how the two MCP bridges are kept separate, and how assets actually move between the two | 🟡 Reference |
| `webgl-budget.md` | ⚠️ **Partially historical** (banner added 2026-08-27). Its *verdict and size ceilings are void* — build 21 was 54 MB zipped against 1 GB. Still cite it for the **audio import presets (§9)** and **"ship 4 skyboxes, not 220" (§4.12/§10)** | ⚪ Use with care |
| `glossary.md` | Naming anything. Canonical spellings and terms | 🟢 Quick lookup |
| `input-map.md` | Need a binding without opening the `.inputactions` asset | 🟢 Quick lookup |

---

## The five rules that matter most

If you read nothing else:

1. **Linear is the source of truth.** Not the design docs, not these files. If a Linear issue disagrees with a document, the issue wins. If the issue is wrong, fix the issue.
2. **No hardcoded values.** Every tunable lives in `MoonlightTunables`, commented, with the owning issue ID. This is a hard project rule.
3. **Offer before handing off scene work.** Placement, staging, inspector wiring and saved scenes are Carlos's domain — but when you can see a way to do it yourself via the UnityMCP or Blender MCP bridge, **ask his permission first** rather than silently handing off instructions. If yes: do it, verify by reading the actual component/scene state back, and document it. If he'd rather do it himself, wait. (Superseded the old "stop at the scene view" rule; see `CLAUDE.md`.)
4. **The target is Windows 64-bit standalone at 1920×1080.** WebGL was dropped 2026-08-25. The 1 GB ceiling is still real but it is itch.io's *upload* limit, not a runtime budget — build 21 shipped at 54 MB zipped. See `pc-build-target.md`.
5. **Placeholders are expected.** Capsules, grey boxes, empty sound pools. Ship the behaviour; the asset arrives later.

---

## Other MDs worth adding as the project develops

Not written yet, but each will earn its place:

| File | When to create it | Why |
|---|---|---|
| `optimization.md` | With MRM-64 | Every optimization, with real before/after numbers. Feeds Assignment #10's cost analysis |
| `event-verbs.md` | After MRM-11 | The event director's verb reference — the thing Carlos will read most often while authoring the script |
| `audio-map.md` | After MRM-38 | Which sound pools exist, which are empty, which layer each prop belongs to. This will become the single most useful "what do I still need to record" document |
| `testing-checklist.md` | Before the Sept 1 build | The manual QA sweep from `Assignments MDs/Assignment09.md` |

**Do not create these speculatively.** An empty document is worse than no document — it reads as done.
- `mrm9-burntwax-integration.md` — MRM-9 Burntwax FPS Engine controller swap: what was taken/changed/dropped, the prefab architecture, the project-wide pause contract, bug history.
- `mrm9-resume-2026-08-29.md` — MRM-9 pickup point: state, settled decisions, the traps that cost time, open items.
- `mrm70-unused-vegetation-inventory.md` — MRM-70: what vegetation the project owns but does not spawn, the 30 `GRASS_*` detail prefabs built from it, and why grass is a Gaia spawner rule rather than a separate tool.
- `vegetation-distribution-brief.md` — the self-contained brief handed to ChatGPT for the biome distribution pass: player scale, Gaia placement mechanics, the live 78-rule configuration, and all 95 in-scope prefabs with measured sizes.
- `mrm70-resume-2026-08-31-night.md` — MRM-70 pickup point: the two-tier prefab split, the three unfixed problems in the live spawn config (slope caps first), and what is waiting on ChatGPT.
- `retarget-pro-strategy.md` — the animation-tooling ruling: what Retarget Pro is for (Wendigo, the quadruped wolf, Tracey's body), what it is *not* for (weapons — already covered), the bake-in-Playground-migrate-clips-only rule, and the diagnosis of the Playground Crest console error.
