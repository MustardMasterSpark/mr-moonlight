# Assignment #10 — Complete AI Dev Pipeline

> **MANDATORY · Due 1 September 2026, 11:59 PM ET**
> **This is the assignment that decides your grade. It has a gate worth half the total score.**

---

## The gate — read this before anything else

> ⚠ **"A stranger must be able to open this link and play your game within 2 minutes without setup instructions. No link or a broken link results in a maximum 50% score across the entire assignment."**

That is not a criterion. It is a **multiplier on everything else**. A perfect pipeline, a perfect audit and a perfect cost analysis with no working link scores 5/10.

**This is why MRM-6 (WebGL spike) and MRM-10 (first itch.io build) are the first two issues in the project.** Get an ugly build live this week. An ugly build that loads beats a beautiful build that does not exist.

**Assignment #10 and the M1 milestone are the same deadline and substantially the same deliverable.** Ship M1 and this assignment is mostly written.

---

## What is graded

| Criterion | Pts | Notes |
|---|---|---|
| **Playable Link** | 2.0 | **GATE.** Broken or missing = max 50% overall |
| **Pipeline-to-Game Connection** | 3.0 | Content in the game was **traceably produced by your pipeline**. Evidence = the video + the output. Anti-slop rule applies |
| **Engine Integration** | 2.0 | Output lands in the engine and works **without manual reformatting**. Partial credit for **one documented manual step** |
| **Cost Analysis** | 2.0 | **From the actual run, not a hypothesis.** Names the most expensive step and judges solo-dev sustainability |
| **Pipeline Audit** | 1.0 | 1 page. One architectural change you'd make (0.5) + one real before/after cost reduction (0.5) |

And the line that should shape your whole tone:

> *"Directness and accuracy are rewarded; architectural sophistication is not."*

**Be honest. Honesty is literally the rubric.**

---

## Deliverable 1 — The playable link

**itch.io, Unity WebGL, 1920×1080 fullscreen, under 1 GB.**

Checklist:
- [ ] The build loads in a browser on a machine that is not yours
- [ ] It plays from the main menu to an ending without instructions
- [ ] **No setup steps.** No "press F to enable", no readme, no download
- [ ] Under 2 minutes from click to playing — **watch the first-load time**, it is the usual killer
- [ ] The page is public, or the link works for a logged-out stranger

**Test it in a private browser window on a phone tether if you can.** Every failure mode here is a cold-cache, cold-machine failure.

---

## Deliverable 2 — Pipeline source + video

- **Repository link** — your GER / Style Guide Agent pipeline from Assignments #6 and #7
- **Video** showing the pipeline running and generating the output that ends up in the build

**Record the video the first time you run a full generation pass.** Two minutes of screen capture: generator produces lines → evaluator scores one badly → refiner fixes it → the resulting CSV → the same text visible in the running game. That last shot is what earns the 3 points for Pipeline-to-Game Connection.

**Integration breakdown to write:**
- **Engine:** Unity 6.3 LTS, WebGL target
- **Automated flow:** pipeline generates and vets text → CSV → baked into a ScriptableObject at build time → consumed by the Dialogue System (MRM-13), System Messages (MRM-14) and the Event Director (MRM-11) → visible in the running game

**On WebGL specifically:** say that data is **baked at build time rather than read at runtime**, because WebGL has no filesystem. That is a real, engine-specific integration decision and it reads as competence.

---

## Deliverable 3 — Pipeline audit and cost analysis (1 page)

### 1. Pipeline production and functionality
- **What did it produce?** Name the specific lines present in the playable build. Not "dialogue" — the actual thought lines, system messages and objective strings, with a count.
- **What manual steps remain?** Be honest. Likely candidates: pasting the CSV into the project, triggering the ScriptableObject bake, running the Unity build, uploading to itch.io.
- **What would eliminate them?** A Unity Editor script watching the CSV folder and re-baking on change; a build script that zips and uploads.

> **Partial credit is explicitly available for one documented manual step.** Do not pretend to full automation. Class 12: *"Full automation is a stretch goal, not a requirement."*

### 2. Architectural reflection — one decision you would change

Pick one and give a **specific alternative**. Two honest candidates:

- **"I dropped the GDD for Linear issues mid-project."** You would do it from the start. The alternative: begin with issue-level specs and treat the design docs as background context only, which is exactly what the project now does. *(This is a genuinely good answer — it is a real decision, honestly reported, with a concrete alternative.)*
- **"I ran the evaluator on a large model for every candidate line."** The alternative: deterministic checks first (length, banned vocabulary, format) and only call the model when code cannot decide. **You already built it this way — so report it as the cost reduction below rather than the change.**

### 3. Cost analysis — from the real run

> *"Must be calculated from the actual content generation run, not a hypothesis."*

Report:
- **Total actual cost** of the full generation run
- **Most expensive step** — almost certainly the **evaluator**, since it runs on every candidate and re-runs on every refine pass
- **Solo/small-team sustainability** — answer it directly with the number

Class 12's worked example, for shape:
> 500 lines × 80 input tokens = 40,000 in = $0.12 · 500 × 60 out = 30,000 out = $0.45 · **total ≈ $0.57**

**Your `runs/costs.csv` from Assignment #6 has these numbers.** That is why it exists.

### 4. Mid-project cost reduction — before/after

You need a real before and after. **You have at least three genuine ones:**

| Change | Before | After |
|---|---|---|
| **Deterministic checks before API calls** | Every candidate hit the evaluator, including ones failing a 90-character limit | Length, vocabulary and format rejected in code for free; the model only judges voice |
| **Model split** | One model for generate, evaluate and refine | **Haiku** generates and refines, **Sonnet** evaluates — the reasoning step is the only one that needs it |
| **Style guide sent once per batch** | The full guide in every single call | Batched, so the guide is paid for once per run |

Any one of these gives a legitimate before/after token comparison. **Measure it — do not estimate it.**

---

## The submission template

The brief provides one. Fill it exactly:

- **Student name:** Carlos Calva
- **Capstone game title:** Mr. Moonlight
- **Game concept brief:** *A first-person horror shooter set on Aanniarvik Island, Alaska, 1979. You are Tracey, trying to reach shelter before 3 a.m. while a cult takes your friends. Low-poly PS1-era art, gritty slow combat, a substance system with real trade-offs. This demo is Day 1 of a seven-day game.*
- **Playable link:** *[itch.io URL]*
- **Pipeline repository:** *[GitHub URL]*
- **Pipeline run video:** *[URL]*
- **Target engine:** Unity 6.3 LTS, WebGL

---

## The four things to do NOW, not on 31 August

1. **Get an ugly WebGL build onto itch.io this week.** MRM-10. The gate is binary and it is worth half the assignment.
2. **Keep `runs/costs.csv` from every pipeline run.** You cannot reconstruct actual costs after the fact, and the rubric refuses hypotheticals.
3. **Record the pipeline video once, early.** Two minutes of screen capture, worth 3 points.
4. **Write down the manual steps as you notice them.** By 1 September you will have forgotten which parts were manual, and honesty about them is worth partial credit.

---

## Do NOT

- Do not leave the build to the last day. **Every WebGL problem is discovered in a browser, not the editor.**
- Do not claim full automation. Partial credit exists precisely for documented manual steps.
- Do not estimate costs. The rubric explicitly rejects hypotheticals.
- Do not describe an architecture you did not build. The anti-slop gate zeroes untraceable claims.
- Do not spend 1 September polishing the write-up. **The game is 50%; this assignment is a fraction of the rest.** If you must choose, fix the build.

## Model

**Sonnet** for the write-up. **Haiku** for the cost arithmetic and the checklist work.
