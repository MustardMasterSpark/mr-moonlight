# Class 4 — The Virtual Studio: Orchestrating Agentic Crews · Executive Summary

**One line:** CrewAI, manager agents, shared memory, and how to keep a multi-agent system from eating its own context window.

## Execution models

- **Sequential** — tasks move down a rigid line, each completing before the next. Predictable but inflexible; bottlenecks cascade through the whole chain.
- **Hierarchical** — a top-level manager agent dynamically delegates based on context, enabling parallel resolution and adaptive problem-solving.

## Manager agents & task decomposition

Analyze the core objective → split into **atomic** tasks → assign each to the specialist whose capabilities match. Dependencies gate execution: the Asset Generation Agent doesn't start until the Concept Description Agent's output is confirmed valid.

## Prompt engineering for developer agents

Three rules, and they are the practical core of the session:

1. **Define the input format** — the agent must know exactly what data structure it receives. Ambiguous inputs → unpredictable parsing.
2. **Specify the output schema** — field names, types, nesting, defined upfront.
3. **Set constraints early** — hard boundaries on what the agent must *never* do, to prevent runaway generation and protect file integrity.

## Preventing context collapse

Memory pools overflow fast. Three mitigations: **summarize past actions** into concise digests, **archive resolved tasks** out of active context into retrievable storage, and thereby **maintain team coherence** across long sessions.

## Documenting the architecture

Mermaid diagrams defined in markdown — version-controllable, diff-friendly, and they expose **circular dependencies** that would otherwise stall the workflow. Assignment #3 requires one.

## Assignment #3 (done)

3+ agents coordinating to produce game-ready output, plus a Mermaid diagram and a ReadMe naming the game. Key rubric line: *"No agent could be removed without breaking the pipeline."*

## Takeaway for Mr. Moonlight

The transferable part is the **three prompt rules** — they are exactly what makes Claude Code reliable on your Unity project, and they belong in your `CLAUDE.md`. The CrewAI machinery itself you can ignore: Class 7 later reverses this advice and tells you to drop the framework for raw orchestration when you're writing code in your own codebase. You already work that way with Claude Code.
