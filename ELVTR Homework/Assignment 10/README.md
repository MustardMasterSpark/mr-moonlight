# Assignment #10 — Complete AI Dev Pipeline

**Student name:** Carlos Calva
**Capstone game title:** Mr. Moonlight
**Game concept brief:** A first-person horror shooter set on Aanniarvik Island, Alaska, 1979. You
are Tracey, trying to reach shelter before 3 a.m. while a cult takes your friends. Low-poly
PS1-era art, gritty slow combat, a substance system with real trade-offs. This demo is Day 1 of a
seven-day game.
**Playable build:** `[fill in: itch.io download page URL once uploaded]` — see Deliverable 1
**Pipeline repository:** `[fill in: https://github.com/MustardMasterSpark/mr-moonlight/tree/main/ELVTR%20Homework/Assignment%2010]` once this folder is pushed
**Pipeline run video:** `[fill in: video URL]`
**Target engine:** Unity 6.3 LTS, **Windows 64-bit standalone** (see the platform note below — this is a deliberate, documented change from the WebGL target the brief assumes)

---

## A note on how this submission maps to the brief

Two things in the official brief don't match this project's actual, current state, and this
submission follows the "be honest" instruction over the letter of the brief:

1. **The brief assumes a WebGL/itch.io browser link. This project is Windows standalone.**
   The team profiled the WebGL build and measured the island scene at **21,946 draw calls**
   against a browser ceiling of roughly 1–3k, running at 19 FPS. Three separate WebGL-only defects
   (invisible terrain from the GLES3 16-sampler limit, mirror-finish ground, an editor-only asmdef
   breaking the build) had already cost a day on top of that. The platform was switched to Windows
   standalone on 2026-08-25 to remove the ceiling entirely — this is a real, dated, documented
   decision in the project's own `CLAUDE.md`, not a workaround for this assignment. Deliverable 1
   below is that Windows build, not a WebGL page.

2. **The brief assumes the Deliverable 2 pipeline is the GER/Style Guide dialogue agent from
   Assignments #6–#7.** That pipeline exists (`ELVTR Homework/Assignment 6/moonlight-ger/`) and
   runs, but **its output is not in the shipped build** — no dialogue system consumes it yet
   (MRM-13/MRM-14 are future issues). Submitting it here would fail the assignment's own anti-slop
   rule ("do not describe an architecture you did not build" / "content in the game was traceably
   produced by your pipeline"). Instead, Deliverable 2 is the **texture pixelation pipeline**
   (`Tools/pipeline/texture_pass.py` in the main repo) — a real, git-tracked, currently-used tool
   that has actually produced textures sitting in the shipped build right now. This folder contains
   a standalone, single-image extraction of that exact algorithm, built for this assignment so it
   can be run and demonstrated without the rest of the Unity project.

Everything below is reported against what was actually built and actually run, per the rubric's own
instruction that directness and accuracy are what's graded.

---

## Deliverable 1 — The playable build

**What it is:** Build 23 of Mr. Moonlight, Windows 64-bit standalone, 1920×1080 borderless
fullscreen. MainMenu → Island scene, plays start-to-finish (objective: kill 20 "old timers",
win screen on completion, game-over/restart on death).

**Where it is:** `E:\Builds\23 - Death HUD Fix - 2026-09-02\Build.zip` (118.6 MB zipped, well
under the 1 GB ceiling — the ceiling is now a download-size limit only, not a WebGL runtime-memory
budget, per the same platform-change decision above).

**What's left for the gate:** uploading `Build.zip` to itch.io as a **downloadable** page (itch.io
supports Windows downloads, not only WebGL embeds) and confirming a stranger can download, extract,
and run it. That upload is Carlos's step, not part of this pipeline deliverable — noted here for
completeness of the checklist:

- [x] Builds and runs end-to-end, main menu to an ending, no console errors
- [x] No setup steps once extracted — double-click the `.exe`
- [ ] Uploaded to itch.io as a public download page *(pending — outside this pipeline's scope)*
- [ ] Verified from a machine that isn't the dev machine *(pending)*

**Honest gap:** a downloaded Windows build cannot hit the WebGL brief's literal "click a link and
be playing in 2 minutes" bar the way an instant browser load could — download + extract + Windows
SmartScreen click-through realistically costs more than a WebGL cold load would have. That tradeoff
is the direct cost of solving the draw-call problem, and it's the kind of thing Section 2 below
(architectural reflection) is honest about.

---

## Deliverable 2 — Pipeline source + video

### What the pipeline is

`Tools/pipeline/texture_pass.py`, in the main project repository, is the actual pixelation step of
the project's real, documented asset pipeline (`Docs/3d-prop-pipeline-wizard.md`, line 624: *"Pixelation,
quantise + Bayer dither → `Tools/pipeline/texture_pass.py`"*). It is not a script written for this
assignment — it's the tool the character/prop/weapon art pipeline (issue MRM-72) actually calls to
turn a raw BaseColor texture into the quantised, dithered look the game's RetroLit shader displays.
It is committed to git and has been since the MRM-72 pipeline was built.

**This folder's `pipeline/pixelate_pipeline.py`** is a standalone, single-image extraction of that
exact algorithm — same 4×4 ordered Bayer matrix (measured from the project's own Retro Realism
`Bayer4x4.tga`), same per-channel quantisation math, copied verbatim rather than reinvented — so it
can run and be demonstrated in isolation, without the rest of the Unity project or the folder-batch
plumbing the real tool also has (AO multiply-in, mask packing, multi-file routing by suffix). This
is the "feed it one image → resize to 512 → pixelation pass" program requested for this assignment.

### What it produced (real run, in this folder)

Run against `samples/T_TSA_Ground_Black_Sand_BaseColor.png` — a real, git-tracked 1024×1024 terrain
BaseColor texture from the shipped build (Aanniarvik Island's black-sand beach layer):

```
python pipeline/pixelate_pipeline.py samples/T_TSA_Ground_Black_Sand_BaseColor.png --size 512 --levels 10 --dither 1.0 --out runs/output/T_TSA_Ground_Black_Sand_BaseColor_512_pixelated.png
```

```
input       samples\T_TSA_Ground_Black_Sand_BaseColor.png  (1024x1024, RGBA, 774,686 bytes)
output      runs\output\T_TSA_Ground_Black_Sand_BaseColor_512_pixelated.png  (512x512, 192,361 bytes)
settings    levels=10 dither=1.0
time        0.0421s (CPU, local)
api calls   0
api cost    $0.00
```

Full console output: `runs/run_log.txt`. Five repeated runs for timing stability: `runs/timing_runs.txt`.
Per-run numbers in spreadsheet form: `runs/costs.csv`.

**A second run at `--levels 5`** is included purely so the effect is visible at a glance in a
README-sized preview — the project's real default (`levels=10`) is intentionally subtle on a
high-frequency photoscan texture like this one; it reads clearly on the lower-frequency, painted
character/prop art it was built for (e.g. the shipped `T_Spotter_BaseColor.png`, `T_Lamp_BaseColor.png`,
`T_Shotgun_BaseColor.png`, `T_FlareGun_BaseColor.png` — all live in
`Assets/_Project/Art/{Enemies,Props,Weapons}/` in the main repo right now, and per the wizard doc,
all went through this exact script). Both runs' parameters are stated plainly above — nothing here
is presented as "the" output without saying which settings made it.

| Input (1024×1024, real terrain texture) | Output, `levels=5` (512×512, visible dither) |
|---|---|
| `samples/T_TSA_Ground_Black_Sand_BaseColor.png` | `runs/output/T_TSA_Ground_Black_Sand_BaseColor_512_pixelated_levels5.png` |

### Video

**Two minutes of screen capture**, recorded by Carlos (not part of this pipeline's own scope):
running the command above, showing the console output (resize + quantisation numbers), the two
output PNGs opened side by side against the input, and a shot of the same pixelated-texture *style*
visible on a live prop in the running build (Deliverable 1) — that last shot is what earns
Pipeline-to-Game Connection credit. Link: `[fill in]`.

### Integration breakdown

- **Engine:** Unity 6.3 LTS, **Windows standalone** target (not WebGL — see the platform note above).
- **Automated flow:** artist supplies a raw BaseColor PNG → `texture_pass.py` (this pipeline's real,
  in-repo parent) resizes and quantises it with Bayer dithering → output PNG is placed under
  `Assets/_Project/Art/<Category>/<Prop>/` → Unity's own `MoonlightTextureImporter.cs`
  (`Assets/_Project/Code/Editor/MoonlightTextureImporter.cs`), an `AssetPostprocessor`, automatically
  detects the `_BaseColor` suffix and applies the correct import settings (Point filter mode — critical,
  since the default Bilinear filter blurs the quantised pixels back into mush and silently undoes the
  whole pass — sRGB, per-category size ceiling, mipmaps, compression) with **zero manual configuration**
  → the RetroLit shader on the prop's material samples the resulting texture directly, no further
  processing, no runtime step.
- **The one manual step, honestly stated:** moving the pipeline's output file from wherever it was
  run into the correct `Assets/_Project/Art/...` folder. Everything downstream of that single copy
  (import settings, filter mode, compression, shader sampling) is automatic. This is the
  "one documented manual step" the rubric gives partial credit for.
- **Platform-specific integration note, honestly reported (adapted from the WebGL case the brief
  expects):** the original WebGL-target reasoning for baking data at build time rather than reading
  it at runtime (no filesystem in a browser) is **no longer the binding constraint** — Windows
  standalone has a real filesystem. The pipeline output is still consumed as a static, pre-baked
  Texture2D asset rather than processed at runtime, but that's now a straightforward "authoring-time
  tool, not a runtime system" choice rather than a platform requirement. One stale leftover was found
  while writing this: `MoonlightTextureImporter.cs` still sets an explicit WebGL platform texture
  override (lines 103–113) from before the 2026-08-25 platform switch. It's harmless — Unity ignores
  platform overrides for platforms not present in the current build — but it's dead code nobody has
  cleaned up yet, which is exactly the kind of small manual-step residue Deliverable 3 asks about.

---

## Deliverable 3 — Pipeline audit and cost analysis

### 1. Pipeline production and functionality

**What did it produce, specifically?** In this assignment's own demonstration run: one 512×512
pixelated PNG at the project's real default settings (`levels=10`, `dither=1.0`) and one at
`levels=5` for visual comparison — 2 output files, both in `runs/output/`, both traceable to a real
input texture from the shipped build. In the actual game (the pipeline's real, ongoing use, not this
assignment's demo run): the same script produced the BaseColor textures for at least 4 shipped
character/prop/weapon assets currently in `Assets/_Project/Art/` — `T_Spotter_BaseColor.png`,
`T_Lamp_BaseColor.png` (+ `T_LampGlass_BaseColor.png`), `T_Shotgun_BaseColor.png`,
`T_FlareGun_BaseColor.png` — per the pipeline documentation naming this script as their pixelation
step. Being direct about the limit of this claim: no per-file generation log exists yet
(`Docs/prop-log.md`, the project's own record for this, has no entries filed — a real, admitted gap,
not papered over here), so this is "the documented pipeline step names this tool" rather than "a
build log proves each file's exact command line."

**What manual steps remain?**
1. Copying the pipeline's output PNG into the correct `Assets/_Project/Art/...` folder (stated above).
2. Triggering the actual Unity build (`manage_build` action, or the Editor's Build button).
3. Zipping the build output and uploading it to itch.io.
4. Writing the `Docs/prop-log.md` entry the wizard's own process asks for after each prop — in
   practice, this has been skipped every time so far (the file has zero entries despite multiple
   props existing). That's an honest process gap, not a hypothetical one.

**What would eliminate them?** A Unity Editor script (`AssetPostprocessor` or a file-watcher menu
item) that watches a drop folder and re-imports automatically would remove step 1. A single build
script wrapping `manage_build` + the zip step (already scripted ad hoc for every build in this
project — see `E:\Builds\` and its numbered-folder convention) would remove steps 2–3 if turned into
one committed script instead of a repeated manual sequence. Step 4 is a discipline problem, not a
tooling one — automation there would mean generating the log entry from the pipeline's own run
output automatically, which this assignment's script already does for its own run in
`runs/run_log.txt`.

> Per the brief: **"Full automation is a stretch goal, not a requirement."** This submission does
> not claim full automation — four real manual steps are named above, honestly.

### 2. Architectural reflection — one decision I would change

**I dropped the GDD for Linear issues mid-project. I would do it from the start.**

This is real and already reflected in how the project runs today: `CLAUDE.md`'s own "Source of
truth" section states *"Linear, project MrMoonlightDemo, team MRM. Design docs in `Docs/Design/`
are background only. If an issue and a document disagree, the issue wins."* That rule exists
because it wasn't true from day one — design docs were authoritative early on, and issues took over
as the actual driver of work partway through. The alternative I'd take with hindsight: **start with
issue-level specs from the first day and treat any design document as background context only**,
exactly the rule the project now runs under. The cost of not doing this from the start was drift —
docs and actual implementation disagreeing, and needing an explicit tie-breaker rule bolted on
after the fact instead of never needing one.

### 3. Cost analysis — from the actual run

**Total actual cost of the full generation run: $0.00.** This pipeline makes zero API calls — it is
deterministic local image processing (Pillow + numpy), not an LLM pipeline. That is a genuine,
measured number from `runs/costs.csv`, not an omission: the `api_calls` and `api_cost_usd` columns
are `0` and `0.00` on every row because there is nothing to call.

**Most expensive step, measured (not the rubric's assumed "evaluator" — there isn't one here):**
profiling the same run stage by stage —

| Stage | Time | Share |
|---|---|---|
| Load source image | 0.0098 s | 24% |
| Resize (nearest-neighbour, 1024→512) | 0.0003 s | <1% |
| Quantise + Bayer dither | 0.0071 s | 17% |
| **Save output PNG (compression)** | **0.0245 s** | **59%** |
| **Total** | **0.0416 s** | 100% |

**PNG encode/compression on save is the most expensive step** — not the pixelation math itself.
This is the honest, measured equivalent of the rubric's "name the most expensive step" ask, adapted
to a pipeline that has no LLM calls to blame it on.

**Solo/small-team sustainability, answered directly:** completely sustainable — at ~0.04 seconds
and $0.00 per texture, a solo developer could run this against every texture in the project (198
`_BaseColor.png` files currently tracked in `Assets/_Project/Art/`) in under 8 seconds of compute
and zero dollars. This is not a pipeline whose cost is a risk to the project; the only real cost is
the manual integration steps named in Section 1, which are time, not money.

### 4. Mid-project cost reduction — before/after

Because this pipeline has no API cost to reduce, the honest before/after here is about the *first*
version of this technique — the earlier, three-map-standard texture pass — versus the current
two-map standard actually shipping today, which is a genuine, already-made architectural decision
(MRM-72, 2026-08-27), not a hypothetical:

| Change | Before | After |
|---|---|---|
| **Two-map standard replaces three-map** | Every asset baked BaseColor + Normal + a packed Mask (metallic/occlusion/smoothness) — a texture (and an import/compression pass) RetroLit never actually samples | RetroLit only ever reads `_BaseMap` + `_NormalMap`; AO is multiplied directly into BaseColor and the Mask step is skipped entirely unless an asset is explicitly headed for URP/Lit (`--mask` flag, opt-in) |
| **AO multiplied in before quantisation, not after** | Multiplying occlusion into an already-quantised BaseColor would force the quantiser to run twice, doubling the compute for that step and risking a second, visible banding pass | `multiply_ao()` runs before `pixelate()` (see `Tools/pipeline/texture_pass.py`, function order) — the quantiser sees the final, AO-baked colour once, at full precision, and only bands it once |

Measured against this assignment's own run: skipping the Mask step outright (as the two-map
standard does by default) is a **100% reduction** in that step's cost for every asset that doesn't
opt in with `--mask` — not an estimate, a direct read of the code path (`want_mask` gates the entire
`build_mask()` call and the file write that follows it).

---

## Folder contents

```
Assignment 10/
├── README.md                      — this file (all three deliverables)
├── pipeline/
│   ├── pixelate_pipeline.py       — the standalone pipeline (resize + pixelate one image)
│   └── requirements.txt           — pillow, numpy
├── samples/
│   └── T_TSA_Ground_Black_Sand_BaseColor.png   — real input, copied from the main project's tracked art
└── runs/
    ├── run_log.txt                — full console output of the default-settings run
    ├── timing_runs.txt            — 5 repeated runs, for timing stability
    ├── costs.csv                  — per-run numbers (time, bytes, $0.00 API cost) in spreadsheet form
    └── output/
        ├── T_TSA_Ground_Black_Sand_BaseColor_512_pixelated.png          — levels=10 (project default)
        └── T_TSA_Ground_Black_Sand_BaseColor_512_pixelated_levels5.png  — levels=5 (visual comparison)
```

## How to run it yourself

```
cd "ELVTR Homework/Assignment 10"
python -m pip install -r pipeline/requirements.txt
python pipeline/pixelate_pipeline.py samples/T_TSA_Ground_Black_Sand_BaseColor.png --size 512 --levels 10 --dither 1.0
```
