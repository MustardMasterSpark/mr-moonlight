# Class 5 — Dynamic Content Generation: RAG-Powered Pipelines · Executive Summary

**One line:** make the agent read *your* documents before it writes anything, so the output sounds like your game instead of a generic one.

## The honest version of RAG

The slides say it plainly: at capstone scale you do **not** need a vector database.

> *"Put your GDD and game docs into a folder your agent can read. That's it. At this scale, your game's lore fits in the context window — no database required."*

The pattern: load the relevant sections of your docs into the prompt **first**, then generate. *"The output sounds like your game because it's reading your game before it writes."*

## Structured output for the engine

Generating content is only half the pipeline; **formatting** it is the other half. Raw prose cannot be parsed. JSON for hierarchical data (dialogue trees, quest data), CSV for tabular data (item databases, stat tables).

## "Generate 10, keep 3"

Produce variations, evaluate, autonomously select only the best for engine integration. Guarantees only lore-accurate, tonally consistent content reaches the player — without manual review at scale.

## The Critic Agent

The pattern, stated as a recipe:

1. Load your game docs as context
2. Feed the new generated content **+** your game docs to the LLM in one prompt
3. Ask: *"Does this new content contradict anything in the existing lore?"*
4. If yes → regenerate. If no → keep it.

## Case study: Epic's Persona Device

A tool letting any developer build AI NPCs in Fortnite from a prompt plus a voice selection — Gemini for dialogue, ElevenLabs for voice. *"This used to require a writing team, voice actors, and a dialogue system engineer."*

## Assignment #4 (done)

Pipeline generating three content types the game actually needs, with a critic agent catching at least one lore break — **shown, not claimed**.

## Takeaway for Mr. Moonlight

This is the session that actually maps onto your game, and it maps onto **one specific thing**: the dialogue/objective/system-message spreadsheets that the event director reads. Your design docs are the retrieval corpus (that's what the DesignContext MDs are for), and the "generate 10, keep 3" + critic pattern is the seed of the GER pipeline you owe for Assignment #6.

Note the tension though: Mr. Moonlight has **four voice actors recording real lines**. Generated dialogue can only ever be the *unvoiced* text — barks, objectives, system messages, Tracey's thought lines. Say that honestly rather than implying the screenplay was generated.
