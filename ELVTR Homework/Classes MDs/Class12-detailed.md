# Class 12 — The AI Production Pipeline: From Agent Outputs to Shipped Game · Detailed Notes

> **Sets Assignment #10 (MANDATORY, due 1 September 2026, 11:59 PM ET) — the assignment that carries the course.**

## Agenda
1. Connecting the pipes for a shipping build
2. The full dev pipeline
3. Local LLMs for dev pipelines
4. Cost analysis & optimization
5. Case study: EA SEED — automated game testing
6. Assignment #10: complete AI dev pipeline

## 1. The only question this session answers

> **"Can a classmate play your game in 2 minutes by clicking a link?"**
> *"If the answer is no — everything between here and the assignment exists to fix that."*

**The three blockers eliminated today:**
1. Manual steps between running your agents and playing your game
2. A content generation pass you haven't costed before running it
3. A game that works on your machine but won't launch on anyone else's

## 2. The packaging checklist

| Engine | Build target | Deploy to | One-click play | Note |
|---|---|---|---|---|
| **Browser / Phaser** | HTML + JS bundle | itch.io or GitHub Pages | No download, no install | |
| **Unity** | **WebGL** | **itch.io (WebGL upload)** | **Runs in browser** | |
| **Unreal** | Windows package (.exe) | itch.io (zipped .exe) | — | Players must download and extract |
| **Pygame / Python** | PyInstaller (single .exe/.app) | itch.io | — | Test: download your own build and run it on a clean machine |

**The one-click test:** *"Can a classmate who has never seen your project play your game within 2 minutes of clicking the link? If the answer is no — you are not shipped yet."*

## 3. The full dev pipeline — map your end-to-end flow

Four questions to answer for your own project:

| Stage | The question |
|---|---|
| **Content generation** | What does your agent layer produce? What file format does it output? (JSON, CSV, plain text, images?) |
| **Validation** | How do you know a generated asset is correct for your game? What rule from your design docs does it have to satisfy? |
| **Integration** | Where does that file need to land in your engine? What happens to it between agent output and the game reading it? |
| **Build** | What is your build command? How long does it take? |

**The correct bar for this session, stated verbatim:**
> *"Reduce the manual steps between running your agents and playing your game. Full automation is a stretch goal, not a requirement."*

Ideal end state: automated scripts sweep validated JSON/CSV into the engine's directory structure → the compiler generates a playable runtime → a ship-ready build is produced with no manual intervention.

## 4. Local LLMs for dev pipelines

**The strategic alternative:** relying entirely on cloud APIs limits iteration speed and bills you for every experiment. Local LLMs offer zero per-token cost for high-volume, low-complexity generation.
**The trade-off:** *"Zero per-token cost, but bounded by your hardware and model capability. Not unlimited — predictable and free after hardware cost."*

**Frameworks:** **Ollama** (run localized models on your own hardware for bulk generation with zero per-token charges) · **llama.cpp** (efficient local inference, ~30–80 tokens/sec on consumer GPUs like an RTX 3080/4080, for background dialogue, procedural layouts, varied loot tables).

### What to run local vs cloud

| Task | Local | Cloud | Verdict |
|---|---|---|---|
| NPC barks (short dialogue) | Good | Good | **Local** |
| Item descriptions (hundreds) | Good | Good | **Local** |
| Narrative consistency check | Fails | Good | **Cloud** |
| GOAP action planning | Fails | Good | **Cloud** |
| Code generation | Simple only | Complex | Depends |
| Critic agent evaluation | Weak | Strong | **Cloud** |

**Local VRAM requirements:** 8B model ≈ 8GB VRAM (RTX 3060 / M2 MacBook Air) · 13B ≈ 14GB (RTX 3090 / M2 MacBook Pro) · 70B ≈ 48GB (A100 / multi-GPU — not consumer hardware).

> *"Bottom line: use local for volume, cloud for reasoning."*

## 5. Cost analysis & optimization

**Auditing your token spend** — *"Open your API dashboard right now."*
- What task does each agent perform?
- How many calls per full generation run?
- What is the per-call token cost?
- **Total cost = calls × per-call cost**

> *"Write the total. This number goes in your assignment."*

**Worked example from the slides:**
- Average tokens per line: ~80 input + ~60 output = 140 tokens
- Claude Sonnet pricing: ~$3 / 1M input, ~$15 / 1M output
- 500 lines × 80 input = 40,000 input tokens = **$0.12**
- 500 lines × 60 output = 30,000 output tokens = **$0.45**
- **Total for this pass: ~$0.57**

**Two optimizations named:**
- **Strategic caching** — identical queries should not trigger fresh API charges. *"Redundant computations are the enemy of a cost-efficient pipeline."*
- **Downsizing models** — aggressively test smaller models for specialized narrow tasks; reserve expensive flagship LLMs only for complex orchestration and final review.

## 6. Case study — EA SEED (Search for Extraordinary Experiences)

**The problem:** Battlefield V has **601 testable features**. Manual QA coverage: ~500,000 hours ≈ **300 work-years**. No human team can cover that; edge cases ship undetected.
**The solution:** RL agents trained with reward signals to explore and exploit game mechanics. *"The agents don't play to win — they play to break. They find geometry clipping, exploit sequences, and undefined states that human testers would take months to discover."*
Source: ea.com/seed/news/automated-game-testing-deep-reinforcement-learning

**Your version:** *"You don't have 601 features. You might have 6. What's the smallest test an agent could run on your game right now that would tell you something you don't already know? That's your pipeline's QA layer."*

## 7. Pipeline audit exercise (in-class)
1. Is your game playable right now? (Yes — link / Almost — one blocker / Not yet — main blocker)
2. What does your pipeline generate? (one line per agent)
3. Estimated total API cost per full pipeline run
4. Your biggest pipeline risk
5. One thing you'd cut if you had to reduce cost by 50%

## 8. Assignment #10 — complete AI dev pipeline (MANDATORY, due 1 September 2026)

> *"Automate the last-mile steps between your agent layer and a playable build of your capstone game, then document exactly what you built and what it cost."*

### Deliverable 1 — playable link
> **⚠ GATE CRITERION: a stranger must be able to open this link and play your game within 2 minutes without setup instructions. No link or a broken link results in a maximum 50% score across the entire assignment.**

### Deliverable 2 — pipeline source code & engine integration
- Pipeline repository link
- **Pipeline run video link** — video evidence showing the pipeline running and generating the output assets/data
- Integration breakdown: target game engine · automated flow description (how agent output lands in the engine and functions in a scene without manual reformatting)

### Deliverable 3 — pipeline audit & cost analysis (1 page)
1. **Pipeline production & functionality** — what did the pipeline produce (specific dialogue, assets, levels present in the playable build)? What manual steps remain? What would it take to eliminate them?
2. **Architectural reflection** — one decision you would now do differently, and the exact alternative you would implement
3. **Cost analysis** — total actual run cost (*"must be calculated from the actual content generation run, not a hypothesis"*) · the most expensive pipeline step · solo/small-team sustainability, and why
4. **Mid-project cost-reduction change** — strategy/prompting approach before vs after, and token/API cost before vs after

### Rubric (10 points)
| Criterion | Description | Pts |
|---|---|---|
| **Playable Link** | A stranger can play within 2 mins without instructions. **Gate: broken/missing link = max 50% total score** | 2.0 |
| **Pipeline-to-Game Connection** | Content in the game was traceably produced by the pipeline (verified via video/output). Anti-slop rule applies. Generic or placeholder content not traceable to the pipeline receives 0 | 3.0 |
| **Engine Integration** | Output lands in the engine and functions in a scene without manual reformatting. Partial credit for one documented remaining manual step | 2.0 |
| **Cost Analysis** | Calculated against the actual run. Identifies the highest-cost step and evaluates solo-dev sustainability | 2.0 |
| **Pipeline Audit** | 1 page, honest: output, manual steps, one architectural change with a specific alternative (0.5), one before/after cost reduction (0.5) | 1.0 |

> *"Directness and accuracy are rewarded; architectural sophistication is not."*

**Before you leave the session:** did you pass the one-click test? did you write down your cost estimate? did you identify your manual steps?

---

## Mr. Moonlight application

**This session is the September 1 milestone. Everything here is directly relevant.**

### The three things to start immediately

1. **Get a WebGL build onto itch.io as a private/draft page this week — however ugly.**
   The gate is binary and worth up to half of a mandatory assignment. Unity WebGL has real, specific failure modes that only show up in a browser: compression settings, `Application.persistentDataPath` behaviour, audio compression format (Unity WebGL wants Vorbis/AAC, and a demo this audio-heavy will fight the 1 GB budget), threading restrictions, and first-load stall on a large build. **Discover those now, not on 31 August.** An ugly build that loads beats a beautiful build that doesn't exist.

2. **Start logging API costs today.** The rubric says *"must be calculated from the actual content generation run, not a hypothesis."* You cannot reconstruct this after the fact. Log per-run token counts from the GER pipeline (Assignment #6) and the Style Guide Agent (Assignment #7) — those runs *are* the pipeline you'll report on.

3. **Record the pipeline running.** Video evidence is required for Pipeline-to-Game Connection (3 pts). A two-minute screen capture of the generator producing lines, the evaluator scoring them, the refiner fixing one, and the resulting CSV loading in Unity covers it.

### The cost-reduction requirement has an easy honest answer

The rubric wants a real before/after. Natural candidates for Mr. Moonlight:
- **Model downsizing** — generate candidate lines with a cheap model, evaluate with an expensive one. That is exactly the local/cloud split the slides recommend (*"use local for volume, cloud for reasoning"*), and it produces a genuine before/after number.
- **Batching** — send the whole style guide once per batch of lines instead of per line. On a 250-line pass that is a large, easily-measured saving.
- **Ollama for bulk generation** if the hardware allows — zero per-token cost for the volume half.

### The pipeline map, filled in for this project

| Stage | Mr. Moonlight |
|---|---|
| **Content generation** | Style-enforced text: Tracey's thought lines, system messages, objective strings, UI copy → **CSV** (matching the spreadsheet-driven design already specified) |
| **Validation** | Deterministic: schema, line length, speaker ID, trigger tag. Agentic: SCORE + REASON against the style guide |
| **Integration** | CSV → ScriptableObject asset baked at build time (**not** runtime file I/O — WebGL) → consumed by the dialogue/system-message/objective systems |
| **Build** | Unity 6.3 LTS → WebGL → zip → itch.io. **Time and measure this; it is slow, and you need the number.** |

### The "smallest test that tells you something you don't know"

Per the EA SEED slide's reframing: for Mr. Moonlight that is almost certainly **a scripted run through the demo's event-director sequence, checking that every objective can be reached and no step deadlocks.** Six features, not 601. Do it manually; it costs an hour.

**Watch out for**
- **The 1 GB itch.io limit is the real constraint, and audio is the risk.** Four voice actors, ~250 lines, ambient beds, sound pools per prop, per-terrain footsteps, enemy vocalisations. Budget audio explicitly and compress aggressively — this is the most likely reason the build blows the limit.
- **"Full automation is a stretch goal, not a requirement"** — the partial-credit clause explicitly allows one documented manual step. Do not burn September on automating the last mile; document the manual step honestly and spend the time on the game, which is worth 50%.
