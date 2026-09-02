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

## Making a 3D asset — fire the wizard
Any prop, character, or weapon work: **`/prop`**, or read
`Docs/3d-prop-pipeline-wizard.md` (MRM-72) and run its intake. It is an executable
instruction set, not a reference — it asks the path, the source, and the texture
resolution, automates every step it can, and **writes its own lessons back after each
prop** so the next one is faster. Per-prop history lives in `Docs/prop-log.md`.

Two rules from it that are easy to get wrong: **RetroLit samples only BaseColor +
Normal** (no mask, no metallic, no emission — AO is multiplied into the albedo), and
**glowing objects get a real Light on the prefab**, never an emission map.

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
**Updated 2026-08-27 — the real target moved.**

| Date | Gate | What it actually means |
|---|---|---|
| **Sept 1** | M1 — playable loop, graded class gate | A **basic** loop working end to end. Not polished, not finished. Carlos's explicit clarification: do not treat this as a wall everything yields to |
| **Sept 8** | M2 — polished itch.io release | A better, more polished version. Still the class deliverable |
| **Before Halloween 2026** | 🎯 **The real target** | A **Kickstarter campaign launched alongside this demo.** The demo is the campaign's shop window, so October is where the quality bar actually sits |

**What this changes about how to work.** Sept 1 and Sept 8 are class gates; **October is the
product.** So: prefer choices that keep improving after Sept 8 over ones that only survive to the
gate, and do not cut a system to nothing for Sept 1 if a slightly larger version serves October.

**Scope note:** every asset in `Docs/new-asset-list.md` is chosen **for the demo**. If a full game
gets made it starts from a clean project with better-chosen assets — so an asset having long-term
drawbacks is not a reason to reject it here.

## Session hygiene

Carlos budgets tokens across sessions. **Proactively suggest a fresh session** at natural stopping
points — don't wait to be asked. There's no precise token-remaining readout to threshold against;
judge it qualitatively instead:
- A chunk of work just finished, got tested, and got logged (Linear comment, doc update) — the
  state is clean, which is exactly when a handoff costs the least.
- A lot of unrelated ground got covered in one sitting (several separate fixes/features, not one
  continuous task) and the next ask looks like its own separate thing.
- The conversation itself has gotten long enough that re-deriving context from scratch would be
  cheaper than carrying it forward.

**How to hand off, when it's time:** write a context prompt to a new `Docs/*-sonnet-prompt.txt` (or
`*-opus-prompt.txt`) file — same precedent as `mrm34-sonnet-prompt.txt` / `mrm70-sonnet-prompt.txt`
— covering what's done, what's next, and any traps already found. A fresh session reads that file,
not chat scrollback, so the handoff has to live in the repo, not just in a verbal summary. Also flag
if the *next* piece of work suits a different model — Opus for building a new system from scratch,
Sonnet for tuning/polish/bug-fixing on one that already exists (the MRM-34 build → tune split is the
precedent). Never commit or push as part of this — Carlos does that in GitHub Desktop; offer a
summary of what changed and a suggested commit message instead.
