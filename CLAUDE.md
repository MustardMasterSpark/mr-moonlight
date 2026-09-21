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
5. `Docs/lighting.md` — **anything that is a light or decides how light looks**: sun / day-night,
   fog, sky, the flashlight, the hands' lighting, enemy lamps, flares, Light Layers, every lighting
   tunable. The single place for lighting; update it whenever you touch a light (added 2026-09-19)

## Making a 3D asset — fire the wizard
Any prop, character, or weapon work: **`/prop`**, or read
`Docs/3d-prop-pipeline-wizard.md` (MRM-72) and run its intake. It is an executable
instruction set, not a reference — it asks the path, the source, and the texture
resolution, automates every step it can, and **writes its own lessons back after each
prop** so the next one is faster. Per-prop history lives in `Docs/prop-log.md`.

**Anything that takes over the hands** (a new weapon, the map and compass, a drug, a tool held
in both hands) also needs animation, a wieldable prefab, an item definition and a key: read
**`Docs/hands-items-and-weapons-pipeline.md`** (MRM-87) first. It has the checklist, the traps,
and what must stay compatible with Tracey's own arms and QuickMagic mocap.

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
- **Create issue branches with `git switch -c <name> --no-track origin/main`**, never plain
  `git checkout -b <name> origin/main`: that sets the branch's upstream to `origin/main`, and GitHub
  Desktop then pushes the branch's commits **straight to `main`** with no pull request (happened to both
  MRM-44 commits, 2026-09-18 and 2026-09-19). After creating a branch, check
  `git config --get branch.<name>.merge` prints nothing (or `refs/heads/<name>`, never `refs/heads/main`).
  Carlos publishes the branch from GitHub Desktop and opens the PR to `main`.
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

## Performance sessions — "I'm going to play a build, check the performance"

The game writes a scene-tagged session log in every build (`SessionLog`, no setup needed) to
`%USERPROFILE%\AppData\LocalLow\Mustard Master Spark\MrMoonlight\Logs\`. When Carlos says he is going
to play (or has played) a build and wants the performance checked, follow **`Docs/performance-sessions.md`**
§1 without re-asking: read the newest BUILD logs, ask him a short batch of questions about the run,
then record the entry — numbers, conditions, what changed recently, findings and **hypotheses** — both
in that doc and as a comment on Linear **MRM-85** ("Performance reports", a living issue, never closed).
Keep the open-hypotheses table current. First implementation (2026-09-18); it will change a lot.

The session log is a general debugging and analysis tool, not only for performance. **Whenever the log
system changes (new field, line type, fix), add a row to the changelog in `Docs/performance-sessions.md`
§7 and post it as a comment on MRM-85.** It is a dev tool: switch it off for the final release build
(`MoonlightTunables.SessionLogEnabled`, plus Frame Timing Stats in Player Settings).

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

### "Run the final instructions" — Carlos's end-of-session trigger phrase

When Carlos says **"run the final instructions"** (or close enough — "wrap up," "final steps," this
exact phrase said in past sessions), it means all four of the following, every time, without
re-asking what he means (step 4 added 2026-09-19):

1. **Commit message.** Summary + description for the current issue branch, in the repo's usual
   style, ending with the required attribution footer (see this session's system instructions for
   the exact footer text — it can change between sessions, don't hardcode it here). Text only —
   never actually commit or push (see the hard rule above).
2. **Close documentation gaps.** Update whatever's now stale: the relevant `Docs/*.md` build/design
   doc for the issue (add a dated session section rather than editing history away), any other doc
   that referenced the old state, and a Linear comment on the issue(s) actually touched — including
   any issue whose scope was touched incidentally (cross-issue work, flag it explicitly in the
   comment, same as the one-issue-one-branch exception above).
3. **Context prompt for the next session.** Same file and shape as the handoff described just above
   in this section — what's done, what's next, traps found. This step and step 2 overlap in
   research but not in audience: the doc/Linear updates are the permanent record, this file is the
   "read me first, cold" pointer for whoever picks the branch back up.
4. **Change trace.** Running the final instructions means an important change was made, so record it
   where problems can be traced back to it (Carlos, 2026-09-19: "a record of every time we introduce
   new changes... to expand our ability to trace down problems and look at log information"):
   - add a row (`C-nnn`, never reused) to the **change record, `Docs/performance-sessions.md` §8**: date,
     branch and commit (`pending` + the commit-message file until Carlos commits and tells you the hash,
     then fill it in), what actually changed, **what it could show up as in the session logs**, links to
     the docs, and the Linear issues;
   - post the same record as a comment on **Linear MRM-85** ("Performance reports");
   - if the change touches the log system itself, also add the §7 changelog row (existing rule);
   - if it touches anything light-related, update `Docs/lighting.md` too (its §8 change history).
   Keep it as simple as it can be while still traceable: date, what, where, which log lines.

Do all four before reporting back — this phrase is the signal that the session is wrapping, not a
request to ask which of the four he wants.
