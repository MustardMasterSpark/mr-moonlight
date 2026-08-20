# Class 8 — The Level Architect: Agentic Layout & Logic (The GER Pattern) · Detailed Notes

> **This session sets Assignment #6 (mandatory).**

## Agenda
1. The GER pattern: generate, evaluate, refine
2. Gameplay-constrained PCG
3. Demo #7: dungeon builder system
4. Case study #5: Hello Games & No Man's Sky
5. Assignment #6 (mandatory): build a GER pipeline for your capstone

**Framing:** *"In S07 you built an agent that decides WHAT to build — scanning your codebase and writing missing code. Today's question: what if your agent could not only generate content, but evaluate whether it meets your design rules, and fix it if it doesn't?"*
S07 = goal-oriented agents that decide what to build. S08 = goal-oriented agents that **evaluate what they built — and fix it.**

**The blocker it solves:** *"You built a pipeline in S06. You ran it. Some outputs were good. Some were broken. Some were technically valid — but completely wrong for your game. You reviewed them manually. That doesn't scale. How do you build a pipeline that catches its own failures before they reach you?"*

## 1. GER = Generate → Evaluate → Refine

| Stage | Definition |
|---|---|
| **Generate** | The agent produces an output |
| **Evaluate** | A second agent **(or rule set)** checks it |
| **Refine** | If evaluation fails, loop back to Generate |

> *"You've seen this pattern before under other names: test-driven development, red-green-refactor, QA loops. Same principle. New implementation."*

### Stage 1 — Generate
Turns prompts into code, layout instructions, or draft content usable as a first pass. **This phase prioritizes speed and variety** — produce enough options to explore different directions before refining the best one. *"Sometimes the generated code or content is incomplete, inconsistent, or broken. That is exactly where the GER loop adds accountability."*

### Stage 2 — Evaluate (two layers)
1. **Deterministic checks** — parsing, compiling, test cases. *"If code can verify it, use code. Parsing, compiling, and test execution are handled automatically because they are objective and repeatable."*
2. **Verifier agent** — handles what code cannot: code quality, design patterns, and whether the implementation matches the intended solution.

> *"Don't pretend code can catch everything."*

### Stage 3 — Refine (the iterative loop)
- **Pass 1:** apply the smallest fix
- **Pass 2:** tighten the correction using the same error context
- **Pass 3:** final attempt, then **escalate** if needed

**Pass limits & circuit breakers:** refinement happens in up to 3 passes. If the issue still isn't resolved, the agent stops, escalates, and sends the developer a clear problem statement. The refiner receives **the specific error plus the original output**, then fixes that exact issue instead of regenerating the whole layout blindly.

> *"You're the developer. The agent works for you. The refiner is not an autonomous guesser — it is a controlled tool that reports back when it hits a limit, so you can decide the next move."*

## 2. Gameplay-constrained PCG

There is a fundamental tension between procedural generation and intentional design. **Pure randomness produces spaces, not games.** *"Without structure, levels lack pacing, narrative coherence, or challenge curves that players find meaningful."*

> *"Constraints are the 'walls' that turn a random space into a playable game. They transform entropy into experience."*

**Discussion prompt from the slides:** *"In your game specifically — what would 'infinite boredom' look like? What rule from your GDD would prevent it?"*

### Constraints are your evaluator's rules
Three roguelike examples given, with the note *"your game has equivalent rules even if the genre is different"*:

| Constraint | What it enforces |
|---|---|
| **Pathfinding** | Dijkstra / A* logic ensures every critical location is reachable. Agents validate traversal routes before approving a layout. |
| **Difficulty pacing** | Monster density and encounter spacing governed by rules tied to player progression. The agent enforces a rising difficulty curve. |
| **Visual/thematic consistency** | Asset placement and environmental theming must align with the narrative context. Agents reject visually incoherent layouts. |

> *"The GDD is not just documentation — it is the agent's rulebook, translated into evaluable logic."*

### The loop, worked
```
Generator output (first pass):
  Room 7: treasure chest placed in northeast corner
  Room 7: northeast corner marked IMPASSABLE (wall)
  -> Constraint violated: treasure is unreachable

Evaluator response:
  FAIL - Item placement conflicts with navigation mesh
  Reason: northeast corner has no valid pathfinding node

Refiner action:
  Move treasure chest to: center of room (valid node confirmed)

Generator output (second pass): PASS ✓
```
> *"This is the GER loop in action — no human reviewed this placement."*

## 3. Demo #7 — the Dungeon Builder team

Four specialized agents in a self-correcting code pipeline:

| Agent | Role |
|---|---|
| **The Generator** | *Feature Writer.* Writes a game feature (pickup item system, spawn manager, dialogue loader) from the input prompt |
| **The Evaluator** | *Build & Test.* Runs the generated code through build tools and validates against deterministic test cases |
| **The Verifier** | *Quality Review.* Reviews code quality and design intent, ensures the feature aligns with intended gameplay behaviour |
| **The Refiner** | *Self-Correction.* On failure, receives the exact error and fixes the implementation before the pipeline runs again |

**Pipeline flow:** lore-based text prompt → Architect generates room layout (raw JSON rendered as a 2D grid in Phaser in real time) → Decorator places assets and enemies (room connections, key item placement, enemy distribution, all using GER logic) → **Auditor validates and approves** (checks accessibility, pacing and consistency before the map is marked playable).

## 4. The same structure, different games

> *"Whatever your game generates — that's your Generator. Whatever rules it must follow — that's your Evaluator."*

- **Card game:** Generator writes card data (name, cost, effect) → Evaluator checks balance rules → Refiner adjusts values until rules pass
- **Narrative game:** Generator writes a scene or dialogue line → Evaluator checks continuity (character alive? location valid?) → Refiner rewrites flagged lines
- **Platformer:** Generator places obstacles and pickups → Evaluator checks reachability and difficulty curve → Refiner adjusts placement

## 5. Case study #5 — Hello Games & No Man's Sky

> *"This is what your pipeline produces without an evaluator."*

**2016 launch — no evaluator.** The original release relied on noise-function math to generate planets. The result: an enormous universe of technically valid worlds, completely empty of meaning. Players called it **"infinite boredom."**
**Post-launch — evaluation rules added.** Later updates introduced rule layers ensuring worlds felt meaningful, inhabited and narratively coherent. *"These were evaluation rules added after the damage was done."*

> *"Generation at scale requires relentless evaluation and refinement. You have the advantage: you can build the evaluator first."*

**The lesson of "meaningful variety":** two radically different alien landscapes can both feel believable because they share the same underlying rules of biology and geology — variety emerges from a **constrained** system. *"Agents must be taught what makes a space fun, not just what makes it different. 'Infinite boredom' is the result of prioritizing difference over design intent."*

## 6. Assignment #6 — build a GER pipeline (MANDATORY, due 18 August)

### Pre-Build Declaration (submit before writing any code)
Plain text, under 150 words, answering all three:
1. **What content type does your game currently generate manually, inconsistently, or not at all?**
2. **What specific rule from your GDD must every piece of that content satisfy?**
3. **What does a failure look like — concretely, in your game's terms?**

> *"If you can't answer all three yet, that's what to work on first — these answers are part of your submission."*

### Deliverables
- Pipeline code: **Generator, Evaluator, Refiner, Circuit Breaker**
- The Pre-Build Declaration
- A short ReadMe: what content type does the pipeline generate, what rule does the evaluator enforce, and **did the pipeline catch something you would have missed?**

### Rubric (10 points)
| Criterion | Description | Pts |
|---|---|---|
| Working Pipeline | The GER loop runs: Generator produces content, Evaluator checks it against a rule, Refiner fixes failures, **Circuit Breaker escalates when the loop can't self-correct** | 3.0 |
| Evaluator Quality | The Evaluator enforces a **specific rule from the student's GDD — not a generic validity check.** The rule is identifiable in the GDD | 3.0 |
| Game Connection | The pipeline targets the capstone game. The ReadMe names the game, the content type, and what the evaluator caught | 2.0 |
| ReadMe | Pre-Build Declaration answers included. Describes what it generates, what rule it enforces, and whether it caught something useful | 2.0 |

**Anti-slop:** submissions generating generic content not connected to the capstone game receive **no credit** on Evaluator or Game Connection.

---

## Mr. Moonlight application

**Directly relevant — this is a mandatory deliverable, see `Outputs/Assignments MDs/Assignment06.md` for the build plan**

- **All three Pre-Build Declaration questions have real answers** without inventing anything:
  1. *Content generated manually/inconsistently/not at all:* **Tracey's thought lines** (the "thought system" issue — a list of context-triggered unvoiced lines, currently unwritten), plus **system messages** and **objective text**.
  2. *The rule every piece must satisfy:* Tracey's voice as fixed in the character profile and style guide — **grumpy, profane, terse, never explanatory, never cheerful, never over-exposing.** The pitch document states the narrative rule directly: *"show, don't tell... punctual, sparse dialogue that hints at much deeper character development"* — that is a citable, specific rule.
  3. *What a failure looks like concretely:* a thought line that reads *"I should find a way to open this door — perhaps the key is nearby!"* — helpful, cheerful, tutorializing, and explaining a puzzle the player is meant to solve. Tracey would say *"Locked. Great."*

- **The two-layer evaluation maps cleanly:** deterministic checks = line length limit, no forbidden vocabulary, valid speaker ID, valid trigger tag, CSV schema. Verifier agent = *"does this sound like Tracey?"* with a score and a reason.

- **The circuit breaker is the part most students skip and it's worth 3 points.** After 3 refine passes, escalate: log the line, the evaluator's reasons across all passes, and a plain statement — *"could not bring this line on-voice; needs a human."*

- **The No Man's Sky lesson applied to Mr. Moonlight:** the Pitch describes procedural terrain for the full game. "Infinite boredom" for Mr. Moonlight would be a procedurally shuffled island where the locations have no relationship to each other and the 3 a.m. run home is arbitrary rather than tense. **The demo sidesteps this entirely by being hand-authored** — which is the right call and worth saying out loud in the assignment.

**Not relevant**
- The Phaser dungeon builder and 2D grid rendering.
- Room-layout PCG. The demo map is hand-built in Unity terrain by the developer.

**Watch out for**
- The rubric demands the rule be *"identifiable in the GDD."* You do not have a GDD — **cite `Style.pdf`, `MrMoonlight Character Profiles.pdf` and the Pitch by name**, and quote the "show, don't tell" line directly. Assignment #7's wording explicitly permits *"your GDD, prior assignments, or established game references"*, and #6's spirit is the same. Make the citation explicit so the grader can find it.
