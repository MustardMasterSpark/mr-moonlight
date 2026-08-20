# Class 1 — Foundations of Agency: An Introduction to Multi-Agent AI in Gaming · Detailed Notes

## Agenda
1. From scripts to agents
2. Agents in dev vs. in-game use
3. Establishing the AI contract
4. How major companies use AI today
5. Agent architectures for dev pipelines
6. Case study #1: Ubisoft & Netflix
7. Exercise: agent or not?

## Learning objectives
Explain what an agent can do in your development pipeline · identify one task in your current workflow an agent could replace · understand the contract between your vision and the agent's output · describe the difference between an agent that builds your game and AI that runs inside it.

## 1. Two different conversations — the course boundary

| AGENTS IN YOUR PIPELINE | AI IN YOUR GAME |
|---|---|
| Write code | Control an NPC |
| Generate level content | Drive enemy behaviour |
| Review your GDD | Power a dialogue system |
| Run QA passes | Simulate a crowd |

> *"This course is the left column. Everything in the right column is a different course."*

## 2. From scripts to agents

**Why build scripts break down:**
- Pre-programmed rules only — *if file changes, trigger lint; if lint fails, halt*. No emergent reasoning, only if/then. Anything outside the programmed states fails silently.
- Cannot adapt to unforeseen edge cases or evolving project structure without costly manual rewrites. Every new scenario needs an engineer to program a new branch.
- As projects grow, the combinatorial explosion of pipeline states makes classic procedural automation unmanageable and prohibitively expensive to maintain.

**Progression shown:** Finite State Machines → Behaviour Trees → Utility AI → **LLM Agents**.

## 3. Building with AI: the dev pipeline

- **Orchestrating production pipelines** — AI agents as synthetic co-developers: triaging tasks, calling tools, writing scripts, generating structured game assets (JSON) without human intervention.
- **Scale without budget inflation** — automating localization, level generation, QA to increase output velocity without proportional headcount.
- Named constraints: **latency**, **token cost management**, **narrative emergence**.

## 4. The AI contract

**Creative emergence** — generative AI enables content, behaviour and narrative that no human explicitly authored.
**Predictable outputs** — players and developers must trust the system will not break core mechanics, violate lore, or introduce logic gaps.

> **The contract** is the explicit design commitment that defines where the system is free to generate and where it must reliably adhere to defined rules and constraints.

### Enforcing guardrails (three layers)

| Guardrail | What it does |
|---|---|
| **Lore consistency** | Agent outputs validated against the design document so generated content never contradicts established world-building or narrative canon |
| **Format validation** | Structured output schemas enforce that every response conforms to the expected data format **before** it is consumed by downstream game systems |
| **Safety filters** | Content moderation screens outputs for harmful, inappropriate or off-brand material before it surfaces to players or enters the pipeline |

## 5. Agent architectures — Sense-Think-Act

- **SENSE** — reads environment/game data: file states, asset manifests, test logs. Provides contextual input for reasoning.
- **THINK** — queries an LLM to reason over inputs, plan multi-step actions, determine the optimal response to current state.
- **ACT** — executes a targeted script or tool call (modifying game files, writing code, triggering pipelines) to produce a measurable change.

**Quick check from the slides:** *"A linter that reads your code files and flags broken JSON schemas — reactive or deliberative?"* → **Reactive**: it responds directly to a stimulus (file change) without planning ahead.

### Reactive vs deliberative

| | Reactive | Deliberative |
|---|---|---|
| Mechanism | Trigger-response | Multi-step planning trees, deep reasoning chains |
| Cost / latency | Minimal latency, low token cost | Higher API cost and latency |
| Use for | High-frequency, low-complexity tasks where speed outweighs depth | Complex content generation where output quality justifies the cost |

> *"Selecting the wrong cognitive depth wastes budget and introduces latency. Match the architecture to the task's complexity requirements and production constraints."*

## 6. How major studios use AI today

- **Ubisoft — AI in the AAA pipeline.** Accelerating asset creation, dialogue generation and world-building across production teams. Writers focus on critical narrative; AI handles volume and variation.
- **Epic Games.** Integrating AI tooling into Unreal Engine workflows, enabling developers to prototype mechanics at unprecedented speed.
- **Netflix — AI as force multiplier.** Investing in generative pipelines that let smaller teams ship bigger games. *"The thesis: AI doesn't replace the developer — it multiplies what one developer can do."*

**On the Netflix bet, the slides are notably hedged:** *"Whether that bet pays off is an open question. What's not open: the tools are real, and you're learning to use them right now."*

## 7. Closing

> *"By the end of this course, every one of you should ship a playable game. Not a demo. Not a prototype. A game someone can pick up, play, and finish. The tools exist right now to make that possible for a single developer in 7 weeks."*

---

## Mr. Moonlight application

**Directly relevant**
- **The left-column/right-column distinction settles a real design question.** The course is not asking for LLM-driven NPCs. Mr. Moonlight's enemy AI is FSM + A* pathfinding + vision cones + audio detection, all deterministic. That is not a shortfall against the course; it is orthogonal to it. Say so explicitly in Assignment #10's architecture write-up.
- **The guardrail triad maps onto real needs:** *lore consistency* → the Style Guide Agent (Assignment #7) checking generated Tracey lines against `03-style-guide.md` and `04-character-profiles.md`; *format validation* → schema-checking the dialogue/objective spreadsheets before Unity ingests them; *safety filters* → not needed, the game is deliberately Mature-rated and the content is authored.
- **Reactive vs deliberative is a real cost lever** for Assignment #10's cost analysis: use a cheap model for bulk line generation, an expensive one for the evaluator.

**Not relevant**
- Ubisoft/Netflix/Epic case studies are context, not technique.
- The FSM → Behaviour Tree → Utility AI → LLM progression is framed as "old vs new", but for Mr. Moonlight's enemies the "old" end of that progression is the *correct* engineering choice — a WebGL build under 1 GB cannot afford LLM calls at runtime, and horror enemies need predictable, learnable behaviour.

**Watch out for**
- Do not let "narrative emergence" tempt you toward runtime generation. Every line in the demo is voiced by a real actor. Generation belongs in the **authoring** pipeline, never at runtime.
