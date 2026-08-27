# Mr. Moonlight — Project MDs (Claude Code toolkit)

**What this is.** The technical ground rules for the Unity project. Read these before writing code, not after.

**Where these belong.** Copy this folder into the repo at `E:\MrMoonlight\Docs\`. Reference them from `CLAUDE.md` so Claude Code picks them up automatically.

| File | Read it when | Priority |
|---|---|---|
| `pc-build-target.md` | **Before anything.** The Windows 64-bit / 1920×1080 target, player settings, quality settings, and the third-party rendering stack | 🔴 **Read first** |
| `webgl-constraints.md` | ~~Before anything~~ — **HISTORICAL as of 2026-08-25.** WebGL is no longer a target; do not apply its rules to new work | ⚪ Historical |
| `terrain-vegetation-tooling-decision.md` | Any terrain, biome, vegetation or vegetation-renderer question — which assets we use and, more usefully, which we rejected and why | 🟡 Reference |
| `unity-conventions.md` | Any Unity work — folders, prefabs, ScriptableObjects, scenes, the tunables pattern | 🔴 High |
| `csharp-conventions.md` | Any C# — naming, structure, the no-hardcoded-values rule, performance patterns | 🔴 High |
| `3d-asset-pipeline.md` | **Any 3D asset work** — the map set every asset ships, Blender baking, the pixelation pass, and the Unity import settings per prop | 🔴 High |
| `system-architecture.md` | Understanding how systems talk to each other. Contains the dependency and interaction diagrams | 🟡 Reference |
| `data-schemas.md` | Anything spreadsheet-driven — dialogue, system messages, objectives, the event script | 🟡 When building those systems |
| `changelog.md` | Picking up any issue — what actually got built, decided, or failed, per issue, newest first | 🟡 Reference |
| `webgl-budget.md` | Any asset that ships in the build — the real MB allocation per category | 🟡 Reference |
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
