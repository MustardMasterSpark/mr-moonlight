# Class 9 — Generating Content: Maintaining the Human Touch · Executive Summary

**⚠ This is the session behind Assignment #7 (mandatory, due 20 August).**

**One line:** where AI generation is safe, where it destroys the thing you're making, and how to build an agent that enforces your game's voice.

## The aesthetic divide

| AI-generated | Human-created |
|---|---|
| High volume, technically proficient, perfect symmetry — but often missing the deliberate imperfections that carry narrative weight | Battle-scarred details, intentional asymmetry, micro-choices that communicate backstory and emotional context |

**Requires the human touch:** critical narrative centrepieces · primary character designs · emotional story beats · core player-facing experiences.

**The exercise that becomes Assignment #7:**
> *"List three assets where AI generation is safe. Name one asset where AI generation would break the experience. Why that one? Write this down now — your answer is the foundation of your Assignment #7 style guide."*

## The AI librarian

Rather than generating new assets, use AI to **search and curate existing human-made libraries**. Vector embeddings let an agent understand the visual and thematic content of a 3D model, so *"spooky abandoned house furniture"* returns actual matches instead of nothing.

## Case study: Codex Mortis

Billed as the *"world's first fully playable game created 100% through AI"*. The slides are refreshingly honest about it:

> *"The 'world's first' and '100% AI' claims are marketing language. Treat Codex Mortis as a useful stress test of what happens when you remove human curation entirely — not as a verified industry benchmark."*

**The mirror test:** at what point does the absence of human curation become *visible to the player*? Where is that same point in **your** pipeline?

## Ethics — the three craft questions

- **Is this content mine?** Can you trace every generated asset to a deliberate creative choice?
- **Did this content come by honestly?** Would a player feel deceived if they learned how it was made?
- **What would you tell a player who asked?** *"If you can't answer clearly, the pipeline has a problem."*

> *"These aren't legal questions yet. They're craft questions."*

Case studies: **Call of Duty** — players identified AI marketing assets, significant backlash, measurable brand risk. **Rust** — procedural generation is never fire-and-forget; even sophisticated systems degrade without sustained human oversight.

## Player value

The winning strategy is **not** maximum content volume — it is maximizing **meaningful moments per hour of play**.

> *"For your capstone game, you are not in the abundance business. You are in the quality-per-scene business. Scale is not the goal. The right content is."*

## Assignment #7 — mandatory, due 20 August 11:59 PM ET

Build a **Style Guide Agent**: a capstone-anchored style guide, an Evaluator that returns a **SCORE + REASON** (not pass/fail), a Refiner that uses the reason to target the rewrite, and **three before/after demonstrations, each a different violation class**.

Hard constraints: don't invent a universe · don't use generic content · **don't use binary pass/fail grading** · don't intervene in the loop.

## Takeaway for Mr. Moonlight

This assignment fits your game better than any other in the course, because **Mr. Moonlight has an actual style document** — most students are reverse-engineering one. Your three constraint types are already written down: **tone** (grunge/acid/punk, Death to the World, brutal and uncomfortable), **vocabulary** (character nicknames, the 1979 setting, Tracey's profanity register), and **formatting/length** (Silent Hill plain white subtitles, sparse, show-don't-tell).

And you can answer the Class 9 exercise honestly: AI generation is safe for thought lines, system messages and objective text; **it would break the experience for the screenplay dialogue** — because four real voice actors are performing it, and the performance *is* the product.
