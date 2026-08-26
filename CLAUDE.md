# Mr. Moonlight

First-person horror shooter. Aanniarvik Island, Alaska, 1979.
Unity 6.3 LTS, URP, **Windows 64-bit standalone, under 1 GB**, distributed as a download on itch.io.

**Display target: 1920×1080, borderless fullscreen.** All UI — Canvas Scaler reference
resolution, HUD layout, menus — targets **1920×1080**.

> **PLATFORM CHANGED 2026-08-25 — WebGL is no longer a target.** Carlos's call, after profiling
> showed the browser draw-call ceiling (~1-3k) was the hard limit on the game's look: the island
> measured **21,946 draw calls**, roughly 10x over what WebGL sustains, at 19 FPS. Three separate
> WebGL-only defects had already cost a day (invisible terrain from the GLES3 16-sampler limit,
> mirror-finish ground, an editor-only asmdef breaking the build). A Windows build removes all of
> them at once and buys ~10x the draw-call headroom.
>
> **The 1 GB ceiling still applies** — it is itch.io's upload limit — but it is now only *download
> size*, not runtime memory plus load time plus a graded time-to-play gate. Textures compress
> (BC/DXT), assets stream from disk, and there is no wasm/JS overhead.
>
> `Docs/webgl-constraints.md` is **historical** as of this date. Do not apply its rules to new
> work; the ones that still matter (build size discipline, baking spreadsheet data, capping
> simultaneous voices) are restated in `Docs/pc-build-target.md`. If you see 960×540, WebGL, or
> GLES3 anywhere, it is stale.

## Read first
1. `Claude Code Context MDs/kickstart.md` — how this project works
2. `Docs/pc-build-target.md` — read before writing code (replaces `webgl-constraints.md`)
3. `Docs/unity-conventions.md` and `Docs/csharp-conventions.md`
4. `Docs/glossary.md` — canonical names (Tracey, Pickaxe, Furman)

## Source of truth
**Linear**, project `MrMoonlightDemo`, team `MRM`.
Design docs in `Docs/Design/` are background only.
If an issue and a document disagree, the issue wins.

## Hard rules
- No hardcoded values. Everything in `MoonlightTunables`. See `Docs/unity-conventions.md`.
- Scene-view and inspector work (placement, staging, wiring, saved scenes) is not an automatic
  handoff. Carlos is the only one who touches Unity, so when you can see a way to do it yourself
  via the UnityMCP bridge, **ask him for permission first** rather than silently stopping and
  handing off instructions. If he says yes, do it, verify by reading the actual
  component/scene state back, and document what changed. If he'd rather do it himself or doesn't
  answer, wait. See `Claude Code Context MDs/kickstart.md` §B.3.
- **Same pattern now applies to Blender work** (extended 2026-08-24, for the MRM-70 3D asset
  pipeline: low-poly conversion, texturing, and beyond — trees, rocks, vegetation, eventually
  characters/staging). To save time toward the deadline, Claude helps hands-on via the Blender MCP
  bridge, not just advises — but still **ask Carlos for permission first** on each piece of actual
  Blender work, then do it, verify by reading the actual scene/mesh/material state back, and
  document what changed. Same wait-if-he'd-rather-do-it-himself rule as Unity.
- One issue, one branch, one PR.
- Never commit or push. The developer uses GitHub Desktop.

## Deadlines
Sept 1 — playable loop, graded class gate.
Sept 8 — polished itch.io release.
