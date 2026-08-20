# Class 9 — Generating Content: Maintaining the Human Touch · Detailed Notes

> **This session sets Assignment #7 (mandatory, due 20 August 2026, 11:59 PM ET).**

## Agenda
1. AI generative vs. human created assets
2. Using AI to find assets
3. Case study: Codex Mortis
4. Ethics & differences with stakeholders
5. Focusing on your audience
6. Workshop #1: constructing a "Style Guide Agent"
7. Assignment #7: Style Guide Agent

**Guest speaker:** Michael Sutherland — composer for film, TV and games; 11+ years running his own business scoring Sundance short films and Steam games; Master of Music in Screen Scoring from USC; currently composing for Japanese visual media with LEGENDOOR; previously Technology Assistant at Wonderbird Music and intern to Henry Jackman (X-Men, Marvel).

## 1. The aesthetic divide

> *"Comparing AI-generative assets to human-created assets reveals stark differences in intentionality. While AI pipelines excel at producing vast quantities of technically proficient content, they often lack the micro-imperfections and deliberate design choices that human artists use to convey subtle narrative subtext."*

| AI-generated | Human-created |
|---|---|
| High-volume output, technical proficiency, perfect symmetry — but often missing the deliberate imperfections that carry narrative weight | Battle-scarred details, intentional asymmetry, and micro-choices that communicate backstory and emotional context |

### When to use which

**Requires the human touch:**
- Critical narrative centrepieces
- Primary character designs
- Emotional story beats
- Core player-facing experiences

> *"Understanding this distinction is key to scaling production without sacrificing the game's soul."*

**The exercise (explicitly the foundation of Assignment #7):**
> *"For your game specifically: list three assets where AI generation is safe. Name one asset where AI generation would break the experience. Why that one? Write this down now — your answer is the foundation of your Assignment #7 style guide."*

## 2. Using AI to find assets — the AI librarian

Instead of generating new assets from scratch, AI can intelligently **search and curate existing libraries of human-made assets**.
- **Perception agent** — trained on an internal database or a storefront (e.g. the Unreal Marketplace) to understand asset content at a semantic level
- **Natural language query** — query for specific meshes, textures or audio in plain language
- **Curated results** — perfectly matched, human-created assets in seconds, preserving artistic quality

### Vector search
| The old way | Vector embeddings | The new way |
|---|---|---|
| Manual data entry for asset tags — inconsistent, incomplete, time-consuming. Searching for "spooky furniture" returns nothing useful. | AI agents "understand" the visual and thematic content of a 3D model, enabling semantic search across entire asset libraries. | Search for "spooky abandoned house furniture" and receive perfectly matched, human-created assets in seconds — no manual tagging required. |

## 3. Case study — Codex Mortis

Billed as the *"world's first fully playable game created 100% through AI"* — a necromantic survival bullet hell with a playable Steam demo.

**The slides' own caveat, verbatim:**
> *"The 'world's first' and '100% AI' claims are marketing language. Treat Codex Mortis as a useful stress test of what happens when you remove human curation entirely — not as a verified industry benchmark."*

**The three diagnostic questions:**
- **At what point does it break?** At what point does the absence of human curation become visible to the player? Identify the specific moment where synthetic design creates friction.
- **The diagnostic question:** Where is that same point in *your* pipeline? Which part of your game would show the same failure mode if human curation were removed?
- **The mirror test:** use Codex Mortis not as a museum exhibit about someone else's failure, but as a stress test for your own pipeline's weakest link.

## 4. Ethics & stakeholders

Three questions presented as **craft questions, not legal ones**:
- **Is this content mine?** *(Can you trace every generated asset to a deliberate creative choice?)*
- **Did this content come by honestly?** *(Would a player feel deceived if they learned how it was made?)*
- **What would you tell a player who asked?** *"If you can't answer clearly, the pipeline has a problem."*

**AAA case study — Call of Duty:** players rapidly identified visibly AI-generated marketing and in-game assets, generating significant negative press and social media backlash. The studio's response and subsequent asset revisions demonstrate the reputational cost of moving too fast without player trust management. *"Replacing human-made assets with visibly AI-generated alternatives carries measurable brand risk that must be weighed against production cost savings."*

**Contrasting case — Rust (Facepunch):** *"Procedural generation is never a 'fire and forget' solution. It demands constant, rigorous human iteration to remain fun and playable."* Their developer logs prove even sophisticated procedural systems degrade in quality without sustained human oversight, playtesting and course correction.

## 5. Focusing on your audience

A 2×2 of player value against curatorial control:

| | Low curatorial control | High curatorial control |
|---|---|---|
| **High player value** | Abundant variety — vast AI output | **Discoverable gems — AI with curation** |
| **Low player value** | Raw volume — uncurated quests | Polished narratives — human-refined experiences |

> *"The winning strategy is not to maximize content volume — it is to maximize the ratio of meaningful moments per hour of play."*
> *"For your capstone game, you are not in the abundance business. You are in the quality-per-scene business. Scale is not the goal. The right content is."*

## 6. Workshop #1 — constructing a Style Guide Agent

A specialized agent acting as an **automated art director**:
1. Encode the game's aesthetic rules: colour palettes, poly budgets, texture resolution standards, stylistic constraints
2. The agent critically evaluates each generated asset against those parameters **before it is allowed into the engine**
3. Assets violating the style guide are automatically flagged and rejected, ensuring zero-deviation consistency

## 7. Assignment #7 — Style Guide Agent (MANDATORY, due 20 August 2026, 11:59 PM ET)

### Core objective
Build an automated, self-correcting AI loop (**Generator → Evaluator → Refiner**) that rigorously enforces the specific aesthetic and narrative rules of your existing capstone game.

### Critical constraints — the "do nots"
- **DO NOT invent a new universe.** The style guide must be pulled directly from your existing GDD **or prior assignments**.
- **DO NOT use generic content.** *"If a stranger can't tell exactly what game the rules are for, you will get a 0 for Specificity."*
- **DO NOT use binary pass/fail grading.** The Evaluator must output a **SCORE and a REASON**.
- **DO NOT intervene in the loop.** The agent must identify violations and fix them independently.

### Deliverables checklist for 10/10
1. **The capstone-anchored style guide (4.5 pts)** — style rules explicitly tied to your game's lore, characters, factions and tone. Derived entirely from your GDD/prior work. **Includes at least 3 distinct constraint types** (e.g. tone/vibe, specific game vocabulary, formatting conventions, length limits).
2. **The Evaluator & Refiner loop (3.0 pts)** — Evaluator analyzes output against the style guide and returns **SCORE + REASON**; Refiner takes the Evaluator's reason and automatically rewrites the content to fix the violations. Successfully identifies off-brand content and corrects it.
3. **Before/after demonstration (2.0 pts)** — three examples using **real content generated for your game**, each demonstrating a **distinct violation class** (e.g. Example 1: tone. Example 2: vocabulary/lore. Example 3: formatting/length).
4. **Pipeline connection (0.5 pts)** — exactly one sentence stating where this agent fits into your current capstone workflow.

### Step-by-step action plan (from the brief)
1. **Extract your rules.** Open your design docs, pick three distinct rules. Their examples: *Vocabulary — characters must refer to magic as "The Weave" and tech as "Rust." Tone — dialogue must be cynical and dry, avoiding overly enthusiastic exclamation marks. Formatting — all output must be formatted as in-game datapad logs.*
2. **Prompt the Evaluator:** *"Review the following text. Grade it on a scale of 1-10 based on these three rules. Output your response strictly as SCORE: [X/10] and REASON: [detailed explanation of what rules were violated]."*
3. **Prompt the Refiner:** *"Take the original text and the Evaluator's REASON, and rewrite the text so that it scores a perfect 10/10 on the style guide."*
4. **Run the tests.** Feed the Generator three prompts designed to produce "wrong" content for your game (e.g. ask it to write an overly cheerful character if your game is grimdark). Save the Before, the Evaluator's Score/Reason, and the After.
5. **Write the pipeline sentence.**

### Rubric (10 points)
| Criterion | Description | Pts |
|---|---|---|
| **Game Specificity** | The style guide derives from the student's GDD, prior assignments, or established game references. Rules specific enough that **a stranger reading them would understand what game they're for.** Generic or invented rulesets receive 0 | 3.0 |
| **Enforcement Accuracy** | Does the agent correctly identify violations and produce rewrites that would actually make the content more on-brand **for this specific game**? Evaluated against the student's own style guide, not a general quality standard | 3.0 |
| **Before/After Demonstration** | Three examples using real capstone content. Each a distinct violation class. Placeholder or invented content receives 0 | 2.0 |
| **Style Guide Depth** | At least three distinct constraint types. Depth credited only when constraints are specific to the game | 1.5 |
| **Pipeline Connection** | One sentence identifying exactly where in the workflow this agent runs. No points for hypothetical future pipelines | 0.5 |

**Important:** submitted outputs must reference specific elements of the capstone game — character names, factions, art style, setting, tone or established narrative voice.

---

## Mr. Moonlight application

**Directly relevant — this is due today, see `Outputs/Assignments MDs/Assignment07.md` for the build plan**

- **Mr. Moonlight is unusually well positioned for this assignment**, because most students are reverse-engineering a style guide out of a GDD, while this project has a **literal, dedicated style document** plus a character profile document with per-character voice notes and sample lines. The rubric's *"a stranger reading them would understand what game they're for"* bar is easy to clear here.

- **The three required constraint types, already written down:**
  1. **Tone** — grunge/acid/punk with punk-Christian elements; the *Death to the World* zine aesthetic; *"brutal and realistic... I want to make the players feel uncomfortable"*; the Pitch's **"show, don't tell"** rule and *"punctual, sparse dialogue"*.
  2. **Vocabulary/lore** — 1979 Alaska, no anachronisms; the island is **Aanniarvik**; enemies are **Zealots**, **Spotters**, **Furman**, never "cultists" generically; nicknames are fixed (**Trey**, **Miss Perfect**, **Lady Jester**, **Rocketman**, **Chief**, **the boogeyman**); Tracey's profanity register is high and habitual, not decorative.
  3. **Formatting/length** — Silent Hill-style plain white subtitles; a hard character limit per line so it fits the subtitle band; system messages are blue and have no audio; thought lines have no audio at all.

- **The Class 9 exercise answers itself for this project:**
  - *Three assets where AI generation is safe:* Tracey's thought lines · system message and objective text · ambient prop sound-cue sheets and placeholder VFX parameters.
  - *One asset where AI generation would break the experience:* **the screenplay dialogue.** Four voice actors are recording it, and the performance direction (*"the shout should audibly tear something"*, *"three different Rylees in one line"*) is the product. Generating it would be both worse and dishonest.
  - That is a strong, specific answer to a question the rubric cares about.

- **The ethics slide has a real answer here too:** every generated line passes through a human (the developer) before it enters the build, and the voiced narrative is entirely hand-written. *"What would you tell a player who asked?"* — you can answer that cleanly.

**Not relevant**
- Vector search over asset libraries — useful someday for the Unity Asset Store, not in 19 days.
- The art-director framing (poly budgets, texture resolution). The style agent here should police **text**, not meshes — text is what the pipeline actually produces.

**Watch out for**
- **"DO NOT use binary pass/fail"** — this is worth 3 points and is the most commonly failed constraint. The evaluator must return a numeric score and a written reason, and the refiner must consume the *reason*.
- The three before/after examples must be **three different violation classes**. Do not submit three tone failures.
