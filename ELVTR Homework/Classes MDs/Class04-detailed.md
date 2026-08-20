# Class 4 — The Virtual Studio: Orchestrating Agentic "Crews" · Detailed Notes

## Agenda
1. Demo #3: creating agents using Claude
2. CrewAI architecture
3. Manager agents & delegation
4. Shared memory pools
5. File system & engine integration
6. Documenting your architecture
7. Assignment #3: build an agent crew

## 1. Demo #3 — the roguelike baseline

The demo uses a roguelike as its source game *"so you can see exactly how agents map to a real project, not a sanitized workspace."*
- **Real game as source** — the roguelike's actual files, structure and design decisions are the input. No abstraction.
- **Prompt-first focus** — all effort goes into shaping how each agent interacts with the codebase and design.
- **Observable behaviour** — with a concrete game as context, agent behaviour is easier to study and replicate.

Agent design principles stated: each agent must have a **clearly scoped responsibility**; goals must be **unambiguous and measurable** so agents can evaluate their own task completion; each agent needs the right **contextual grounding**.

## 2. Prompt engineering for developer agents

> *"Crafting prompts for development agents requires strict formatting and unambiguous instructions. Establishing constraints early prevents catastrophic code generation failures."*

| Rule | Detail |
|---|---|
| **Define the input format** | The agent must know exactly what data structure it will receive. Ambiguous inputs lead to unpredictable parsing behaviour and unreliable outputs. |
| **Specify the output schema** | The exact schema the agent must return, defined upfront — field names, types, and nesting included. |
| **Set constraints early** | Hard boundaries — what the agent must **never** do — prevent runaway generation and protect the project's file integrity. |

## 3. CrewAI architecture

### Hierarchical vs sequential task execution
- **Sequential** — tasks move down a rigid line, each completing before the next begins. Predictable but inflexible; bottlenecks cascade through the entire chain.
- **Hierarchical** — a top-level agent dynamically delegates tasks based on current context, enabling parallel resolution and adaptive problem-solving across the crew.

### What the framework handles for you
- **Context window tracking** — continuously monitors token consumption across all active agents to prevent overflow failures.
- **Rate limiting** — *"behaviour depends on your CrewAI version and configuration. Test your crew with a small batch before running at scale."*
- **Tool output parsing** — the loop of parsing tool outputs back into the LLM's reasoning engine is handled under the hood.

## 4. Manager agents & delegation

**Task decomposition:**
1. **Analyze core objective** — the Manager Agent receives the high-level goal and evaluates scope and complexity
2. **Split into atomic tasks** — decomposed into the smallest independently solvable units
3. **Assign to specialists** — each task delegated to the sub-agent whose capabilities best match

**Dependency gating example:** the *Concept Description Agent* finalizes the creative brief and outputs a structured description → **dependency resolved** (prerequisite output confirmed valid and available) → the *Asset Generation Agent* begins work only then, preventing wasted generation cycles.

## 5. Shared memory pools

**Structured message passing** — agents communicate through structured messages, ensuring context transfers accurately without polluting global state.
- **Accurate context transfer** — a schema prevents context being misread or silently dropped between handoffs
- **Global state protection** — isolating message payloads avoids cross-contamination between independent agent threads
- **Simultaneous access** — multiple agents can reference the same foundational knowledge without conflict

### Preventing context collapse
| Strategy | What it does |
|---|---|
| **Summarize past actions** | Periodically compress resolved conversation turns into concise summaries to free context capacity without losing key decisions |
| **Archive resolved tasks** | Move completed tasks out of the active context window into long-term storage, retrievable only when explicitly needed |
| **Maintain team coherence** | A well-managed memory pool keeps the agent team responsive and coherent during extended, complex generation sessions |

## 6. Filesystem & engine integration

Agents are granted capability to **read existing scene files** and **modify configuration documents** directly — *"transforming them from text generators into active participants within the project's file structure."*
- Read scene files: parse and analyze existing scene data to understand current world state before making changes
- Modify config documents: update configuration files directly, enabling real-time tuning of game parameters without manual intervention

> *"This capability elevates agents beyond chatbots — they become genuine collaborators within the project's file architecture."*

**Error handling at integration points:** detect malformed output → validate before compile → prompt self-correction.

## 7. Documenting your architecture

- **Defined in markdown** — diagrams written as plain text, making them version-controllable and diff-friendly alongside source code
- **Agent hierarchy visualization** — the full manager-to-sub-agent delegation tree renders automatically from the diagram definition
- **Memory & dependency maps** — shared memory access patterns and task dependency chains visualized in the same diagram

**Why it matters:** diagrams reveal bottlenecks before they cause failures, **expose circular dependencies** that can stall agent workflows, and make system behaviour legible to external stakeholders.

## 8. Assignment #3 — build an agent crew (due 28 July, complete)

**Deliverables:** crew code (3+ agents coordinating without crashing) · Mermaid diagram (agent roles, connections, data flow) · ReadMe naming the game.

**Rubric:** Working Crew /3.0 · Game Connection /3.0 · Role Clarity /2.0 (*"No agent could be removed without breaking the pipeline"*) · Architecture Diagram /1.0 · ReadMe /1.0.

---

## Mr. Moonlight application

**Directly relevant**
- **The three prompt rules are the reusable asset from this session.** They are precisely what makes Claude Code reliable on the Unity project, and they belong in the project's `CLAUDE.md` / kickstart doc:
  - *Define the input format* → tell Claude Code exactly what a Linear issue looks like and what the tunables file looks like
  - *Specify the output schema* → C# naming conventions, where constants live, what a PR description contains
  - *Set constraints early* → **never** edit scene files directly, **never** hardcode a value, **always** stop and hand off when a step needs the Unity inspector
- **"Expose circular dependencies"** is a real concern in the Mr. Moonlight system graph: the enemy speed behaviour depends on the animation tree, which depends on the model, which depends on the 3D pipeline. A Mermaid system diagram is worth building for the Project MDs.
- **Filesystem-participant agents** describes exactly what Claude Code + the Unity MCP already is. You are further along here than the course assumes.

**Not relevant**
- **CrewAI itself.** Class 7 explicitly reverses this: *"For closing GDD gaps in a running game, raw orchestration gives you more visibility."* You already use raw orchestration (Claude Code). Do not adopt CrewAI now — it would be a net loss with 19 days left.
- Manager-agent delegation trees: overkill for a solo project with two or three real agent roles.

**Watch out for**
- The "3+ agents" requirement in Assignment #3 pushes toward inventing agents. In your remaining assignments, resist this — Class 3's own rubric flags *"5+ agent types for a solo developer"* as a red flag, and Class 12's rubric says *"architectural sophistication is not"* rewarded.
