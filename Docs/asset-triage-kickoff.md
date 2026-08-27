# Asset triage session — kickoff

**Written 2026-08-27.** Read this first, then wait for Carlos's go-ahead. This is context, not a
start signal.

---

## What this session is for

Carlos has acquired a large batch of Unity Asset Store packages. The task is to **triage that list
against the existing Linear issues** and work out which assets help which issues, what each would
change, and what it would cost.

**This is a planning pass, not an implementation pass.** No imports, no scene changes, no code.

---

## Where the project actually is

Today is **2026-08-27**. Two deadlines:

| Date | Gate | Days left |
|---|---|---|
| **Sept 1** | M1 — playable loop, graded class gate | **5** |
| **Sept 8** | M2 — polished itch.io release | 12 |

**M1 is a BASIC PLAYABLE LOOP, not a finished game.** Carlos's explicit clarification, 2026-08-27.
Do not treat Sept 1 as a wall that everything else must yield to — it is a gate on one vertical
slice working end to end.

Current state:

- **MRM-16 / MRM-41 / MRM-42** — code-complete on branch `mrm-41`, **nothing scene-tested**. No
  GameObjects, prefabs or `ItemDefinition` assets exist in any scene yet. See
  `Docs/mrm41-resume-2026-08-26.md`.
- **MRM-18** (main menu) — In Review, pending Carlos's hands-on playtest.
- **MRM-70** (terrain/vegetation) — paused, not closed. Seven open gaps in
  `Docs/mrm70-pause-2026-08-26.md`.
- **MRM-47** (world lighting) — In Progress.

> **Doing this triage NOW is deliberate and correct — Carlos's call, 2026-08-27.** Knowing what is
> already owned changes *how* the remaining work gets built. Building a system and then discovering
> an owned asset that replaces it is strictly worse than spending an hour finding out first. Several
> of these packages are expected to help substantially.
>
> **Still sort every asset into "helps M1" / "helps M2" / "post-demo"** — not to suppress the M1
> list, but so sequencing is visible. A long M1 list is a fine outcome if the items genuinely
> shorten the path to a working loop. The judgement to make per asset is *"does adopting this save
> more time than it costs?"* — not *"can we afford the distraction?"*

---

## Decisions already made — do not re-litigate

Full reasoning in `Docs/terrain-vegetation-tooling-decision.md`. Summary:

| Area | Decision |
|---|---|
| Terrain / biome placement | **Gaia Pro VS** — adopt, **editor-time tools only**, installed **temporarily**, scheduled **after Sept 1**. Not installed. Not linked to any issue. |
| Vegetation rendering | **Flora Renderer 6** stays. Nature Renderer 6 Pro rejected. |
| Procedural terrain | **MicroWorld rejected** — cannot preserve our authored heightmap. |
| Water | **Crest Water 5** — MRM-71, M2 polish. KWS2 and Crest 4 rejected. Not installed. |
| Art direction | **PSX / low-poly is unchanged.** Water is the one deliberate exception: **CRT only, no PSX treatment.** |

**The test that decides any future vegetation renderer:** does it work with `RetroLit` unmodified?
If it needs the shader patched, the answer is no.

---

## Read before starting

1. `CLAUDE.md` — hard rules
2. `Docs/terrain-vegetation-tooling-decision.md` — what is already decided, and the gaps
3. `Docs/external-assets.md` — **what is already owned, installed, and explicitly rejected.** Check
   here before proposing anything; several packages have already been evaluated.
4. `Docs/pc-build-target.md` — the platform, the rendering stack, and its traps
5. `Docs/00-INDEX.md` — everything else

---

## How to triage each asset

For every package Carlos names, produce a compact row. Do **not** write an essay per asset.

| Field | Notes |
|---|---|
| **Asset + publisher** | |
| **Which issue(s) it touches** | Real MRM identifiers. "None" is a valid and useful answer |
| **Milestone** | **M1 / M2 / post-demo** — the most important column |
| **What it replaces or adds** | Be specific. "Improves visuals" is not an answer |
| **Integration cost** | Hours or days, and what has to be re-verified afterwards |
| **Risk to the existing stack** | `RetroLit` / Flora / HAZE / CRT / `PC_Renderer` feature ordering |
| **Verdict** | Adopt / park / reject, with one line of reasoning |

**Group by issue, not by asset.** Carlos's question is "what helps my issues", not "what did I buy".

### Things that will come up repeatedly

- **`RetroLit` compatibility** is the deciding constraint for anything that renders. A shader that
  needs patching is a permanent maintenance tax on the game's signature look.
- **Renderer feature ordering on `PC_Renderer`.** HAZE, CRT, and soon Crest all inject passes. HAZE
  bails out entirely when `!cameraData.postProcessEnabled` (`HazeRendererFeature.cs:546`).
- **Build size is not the constraint it used to be.** Build 21 was **54 MB zipped / 178 MB raw**
  against a 1 GB limit, while `Assets/` on disk is 2.2 GB. **Project-folder size is not build size.**
  Do not reject anything on download size without measuring.
- **`Packages/` is not gitignored, `Assets/ThirdParty/**` is.** A UPM package gets committed to the
  repo; an `Assets/` one does not. This already applies to Flora and will apply to Crest 5.
- **Texture pipeline.** Every imported 3D asset is meant to route through a reduction + pixelation
  pass matching the vegetation treatment. **That pipeline is not defined yet** — Carlos has flagged
  it as its own upcoming task. `Assets/_Project/Code/Editor/MoonlightTextureImporter.cs` already does
  part of it (prefix-routed sRGB / Point filter / 512 cap / compression) and should be **extended,
  not replaced.** See `Docs/3d-asset-pipeline.md`.

---

## Model

**Opus.** Decided 2026-08-27. The per-asset lookups are mechanical, but three things are not:

1. **Compatibility judgement against a fragile stack** — `RetroLit`, Flora, HAZE, and
   `PC_Renderer` feature ordering. The Nature Renderer rejection came from reading shader pragmas
   and knowing what `#include_with_pragmas .../DOTS.hlsl` implies for BRG. Getting one of these
   wrong costs a day of integration that then has to be undone.
2. **A many-to-many mapping held in one head** — the highest-value findings are the non-obvious
   ones: an asset that helps two unrelated issues, or that makes a *planned approach* obsolete
   before it gets built. Those only surface if the whole issue list and the whole architecture are
   in view at once.
3. **The output steers everything built afterwards.** High leverage, and expensive to reverse.

Carlos is token-constrained, so **ask for the full asset list in one message** rather than
drip-feeding, and batch the research. Spend the budget on verdicts, not on lookups.

## Working rules that apply

- **Do not auto-start.** A kickoff document is context to read, not a go-ahead. Wait for Carlos.
- **Do not link assets to Linear issues without asking.** Carlos wants to see the triage first. When
  issues do get created, prefer **sub-issues** over checklist entries inside a parent — sub-issues
  get their own identifier and their own git branch (MRM-71 under MRM-67 is the pattern).
- **Never commit or push.** Carlos uses GitHub Desktop.
- **Unity and Blender work:** when you can see a way to do it yourself through the MCP bridge, **ask
  permission first** rather than handing off instructions. If yes — do it, verify by reading the
  actual state back, and document it. See `CLAUDE.md`.
- **Verification needs a real build.** Editor `UnityStats` includes Scene View rendering and only
  updates when a frame actually draws. It has given false readings on this project.
- **`MoonlightTunables` for gameplay numbers.** Vegetation and staging numbers are explicitly exempt
  until a real perf problem appears; core mechanics are not.

---

## What good output looks like

1. A grouped table: **issue → assets that help it → milestone → verdict**
2. A short list of assets that help **nothing** currently tracked (equally valuable — it stops them
   being installed "just in case")
3. A **"do this before Sept 1"** shortlist — assets that genuinely shorten the path to a working
   loop. Length is not the measure; each entry must justify itself as *saves more time than it
   costs*
4. Open questions for Carlos, asked once at the end rather than interleaved

Then stop and wait. Issue creation happens after Carlos has read the triage, not during it.
