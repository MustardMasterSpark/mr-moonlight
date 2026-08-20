# Class 2 — The GDD Anatomy · Executive Summary

**One line:** how to write a GDD for an AI-assisted project, and the scoping rule that is worth more than the rest of the session.

## "One Agent, One Wow"

The scoping rule of the course:

> Pick a game that could theoretically be built **without AI in a single weekend**. *"Could two developers build a rough but playable version of this in 48 hours? If yes, it's scopeable for AI augmentation."*

Then: name the **one** AI-driven moment that makes the game worth playing. Design the whole technical strategy around making that moment work — before building anything else.

## The three new pillars of an AI-native GDD

1. **The AI dev pipeline** — explicitly map how raw text flows through an AI node, through JSON, into the engine. *"Without this map, integration breaks silently and unpredictably."*
2. **Technical requirements & constraints** — API limits, context window size, processing latency. *"Constraints are a feature, not a limitation."*
3. **Token budgets & projections** — project generation costs before writing a line of code.

The checkpoint the slides ask you to fill in:
- *In my game, the [agent] will take [input] and produce [output] that the player sees as [effect].*
- *My biggest constraint is [X] because [reason].*
- *Each [core action] costs ~[N] tokens, so [N] sessions ≈ $[X].*

## The Dev Crew

Agent roles are documented in the GDD with defined inputs, outputs and scope — not improvised at runtime. Every piece of generation gets a **specialized** agent, not a generalized one. The structure mirrors a real studio, from a Lead Architect Agent (high-level systemic decisions) down to an Asset Scraper Agent (gathering raw files).

## Case study: Van Buren (Black Isle's cancelled Fallout 3, 2003)

The only publicly available production-scale AAA GDD. It was **excellent** — and the game never shipped, because Interplay's finances collapsed.

> *"GDD rigor is necessary. It is not sufficient. The developers who shipped games with AI pipelines in 2024 didn't have better ideas than the Van Buren team. They had smaller scope, clearer constraints, and pipelines that ran on day one."*

## Assignment #1 (done)

First draft GDD. Anti-slop gate: generic or placeholder games get zero on Game Specificity and Player Experience Clarity regardless of theoretical correctness.

## Takeaway for Mr. Moonlight

Two things survive from this session even though you dropped the GDD:

1. **"One Agent, One Wow"** still applies — but for you the "wow" is not an AI feature, it's the **island that reshapes each night and the 3 a.m. deadline**. Say that plainly in Assignment #10 rather than pretending an agent produces it.
2. **The Van Buren lesson is your lesson.** A beautiful document that doesn't ship is worth nothing. That is precisely why you replaced the GDD with Linear issues — and it is a defensible, on-message answer if the course asks why.
