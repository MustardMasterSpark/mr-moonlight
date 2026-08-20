# Class 2 — The GDD Anatomy: Theory & Strategy · Detailed Notes

## Agenda
1. Game idea scoping: "One Agent, One Wow"
2. The three new pillars of an AI GDD
3. The Dev Crew
4. GDD templates
5. Demo #1: stress-testing a GDD
6. Case study #2: Van Buren
7. Assignment #1: first draft of GDD

## Session objectives
Define the three new pillars (AI dev pipeline, technical constraints, token budgets) · assign agent roles using the Dev Crew model · complete Assignment #1 with a full Technical Strategy section · identify at least one logic gap in your own GDD **before writing a single line of code**.

> *"Your GDD is not a creative writing exercise. It's an engineering contract — specifying what each agent does in your game, what it costs, and how it connects to your engine."*
> *"Debug it on paper, not in code."*

## 1. Game idea scoping: "One Agent, One Wow"

**The constraint:** pick a game that could theoretically be built **without AI in a single weekend**.
**Why:** it ensures the core loop is achievable before introducing complex AI layers. *"If the game can't exist without AI, it can't be validated, iterated, or shipped reliably."*
**The test:** *"Could two developers build a rough but playable version of this game in 48 hours? If yes, it's scopeable for AI augmentation."*

**The rule:** one exceptional task. Name your game, describe what the player does in one sentence, then identify **the one AI-driven moment that makes it worth playing**. That moment is your "One Agent, One Wow". Design the entire Technical Strategy section around making it work — before building anything else.

## 2. The three new pillars

### Pillar 1 — The AI dev pipeline
A comprehensive AI-centric GDD explicitly maps how the pipeline functions: raw text data → AI node → JSON scripts → game engine (Unity/Unreal).
**Why it matters:** ensures a clear understanding of exactly how agents talk to the engine. *"Without this map, integration breaks silently and unpredictably."*

### Pillar 2 — Technical requirements & constraints
Rigorously define API limits, context window size, processing latency.
**Risk of skipping:** systems that cannot be sustained in live production. *"Constraints are a feature, not a limitation."*

### Pillar 3 — Token budgets & projections
Project token budgets and generation costs **before a single line of code is written**, to stay commercially viable and avoid runaway API bills at scale.

### The checkpoint (fill these in for your game)
- **AI dev pipeline:** *"In my game, the [agent name] agent will take [input] and produce [output] that the player sees as [effect]."*
- **Technical constraints:** *"My biggest constraint is [X] because [reason]."*
- **Token budget:** *"Each [core game action] costs approximately [N] tokens, which means [N] sessions costs approximately $[X] at current Claude Haiku pricing."*

> *"If you can't answer all three, your Technical Strategy section isn't ready to write yet."*

## 3. The Dev Crew

- **Dedicated & specialized** — every piece of generation requires a dedicated specialized agent rather than a generalized model. *"Specialization is the foundation of a reliable pipeline."*
- **Mapped in the GDD** — agent roles are not improvised at runtime; they are formally documented with defined inputs, outputs and scope of responsibility.
- **Mirroring a real studio** — the crew structure mirrors a studio hierarchy for efficiency and accountability.

**Role spectrum:** *Lead Architect Agent* (high-level game logic reasoning, examining blueprint schematics, systemic design decisions) ←→ *Asset Scraper Agent* (low-level utility: gathering raw data files, assets and references to feed the pipeline).

## 4. Worked Technical Strategy example (the "thoroughly completed" bar)

**Game:** Dungeon Crawler Roguelite (Phaser + Claude API)

**Agent roles**
- *Floor Generator Agent* — Input: seed, difficulty_level (int 1-5) → Output: JSON room layout (exits, secrets, loot_table)
- *Enemy Behavior Agent* — Input: player_level, room_layout → Output: JSON array of enemy spawn configs
- *Loot Narrator Agent* — Input: item_name, rarity → Output: string (max 40 words, dark fantasy tone)

**Token budget**
- Per floor generation: ~1,200 tokens
- Per session (10 floors): ~12,000 tokens
- Cost at Claude Haiku pricing: ~$0.002/session
- Monthly at 1,000 sessions: ~$2.00

**API constraints**
- Rate limit: 60 req/min → max floor generation speed: 1/sec
- Context window: 200K → no pagination needed at current scope

> *"This is what 'thoroughly completed' looks like. Write this section for YOUR game before any code."*

## 5. GDD template sections

- **Prompt constraints section** — defining agent prompt constraints for consistent, repeatable outputs
- **Engine integration section** — bridging creative vision and the technical execution Unity/Unreal require
- **Technical strategy section** — *"the most critical section"* — token budgets, API constraints, agent role definitions formalized into an actionable strategy

Submission bar: *"If you can describe what the player sees when each agent fires, you're ready to submit. If you can't, keep writing."*

## 6. Demo #1 — stress-testing a GDD with an agent team

An agent team proactively finds logic gaps in the design document before any code is written. The AI acts as a **synthetic audience**, evaluating mechanic viability and surfacing conflicting rules that would cause runtime breakdowns. It returns a **structured error report** highlighting exactly where GDD logic conflicts, giving actionable revision targets. *"This validation happens entirely at the document level, catching critical design flaws at the cheapest possible stage of development."*

Also demonstrated: **generating asset specifications** precise enough to hand to an artist as a brief. *"If the agent can't produce a structured spec from your GDD description, the description is too vague to build from."*

## 7. Case study #2 — Van Buren (Black Isle's unreleased Fallout 3, 2003)

**What it is:** a complete AAA GDD for a full Fallout RPG, cancelled when Interplay shut down Black Isle in December 2003. Black Isle released the full GDD. *"The only publicly available production-scale GDD for a major AAA franchise."* (Available via No Mutants Allowed / Internet Archive.)

**The three pillars in a pre-AI GDD**
| Pillar | Van Buren | Today's equivalent |
|---|---|---|
| Technical pipeline | Exact data flow: dialogue → Dialogue Editor tool → game engine. Every content type had a named pipeline step. | Replace each pipeline step with an agent. |
| Technical constraints | Engine: Jefferson (modified from Torment's). Memory budget per zone documented. Polygon limits per character model documented. | API rate limits, context windows, token costs. Same discipline. |
| Scope | 5 major regions, 30+ named NPCs, 4 faction alignment axes, 30-40 hour runtime. **Never shipped.** | "One Agent, One Wow." Know your scope before the first prompt. |

**Why it didn't ship:** *"NOT because the GDD was bad. The GDD was excellent."* Interplay's financial position collapsed; Black Isle was shut down mid-production. *"The constraints that mattered weren't in the document — they were in the balance sheet."*

> *"GDD rigor is necessary. It is not sufficient. The developers who shipped games with AI pipelines in 2024 didn't have better ideas than the Van Buren team. They had smaller scope, clearer constraints, and pipelines that ran on day one."*
> *"'One Agent, One Wow.' That's how you avoid being Van Buren."*

## 8. Assignment #1 — first draft of GDD (due 21 July, complete)

**Required sections:** Executive Summary · Game Mechanics (player-facing actions and loop) · AI Architecture (what each agent does, described through its effect on gameplay) · Technical Strategy (agent roles, token budget, API constraints).

**Anti-slop gate:** submissions describing a generic or placeholder game, or applying MAS concepts without grounding them in specific player-facing mechanics, receive **zero** on Game Specificity and Player Experience Clarity regardless of theoretical correctness.

**Rubric:** Game Specificity /3.0 · Player Experience Clarity /2.5 · Agent Role Definition /2.0 · Technical Feasibility /1.5 · Presentation /1.0.

---

## Mr. Moonlight application

**Directly relevant**
- **"One Agent, One Wow" survives, reframed.** Mr. Moonlight's "wow" is not agent-produced — it is the shape-shifting island and the 3 a.m. deadline. The honest framing for Assignment #10 is: *the agent layer is a production tool, not a runtime feature.* The course rewards honesty here (see Class 12: *"Directness and accuracy are rewarded; architectural sophistication is not."*).
- **The 48-hour test is a genuinely good scope check** and Mr. Moonlight's demo passes it: an FPS with a pickaxe, a pistol, wolves and a linear route through six locations is a weekend prototype. The *polish* is what takes weeks. That is the right shape.
- **Token budget discipline is now mandatory**, not optional: Assignment #10 requires actual costs from a real run. Start logging.
- **The Van Buren lesson is the direct justification for dropping the GDD.** An excellent document is not a shipped game. Linear issues are the smaller-scope, clearer-constraint, runs-on-day-one alternative. Use this case study by name if you need to defend the decision.

**Not relevant**
- The GDD template itself — abandoned in favour of Linear.
- The roguelike worked example (Floor Generator / Enemy Behavior / Loot Narrator agents). Mr. Moonlight has no procedural floors and no loot table. **Do not force an equivalent.** Your real agent roles are: a **dialogue/bark generator**, a **style evaluator**, and **Claude Code as the implementation agent**.

**Watch out for**
- Later assignments repeatedly say *"a rule from your GDD"*. You do not have a GDD. **Substitute:** `Style.pdf`, the Character Profiles, and the Linear issues — all are legitimately "your established game references / prior assignments", which is the exact wording Assignment #7 permits.
