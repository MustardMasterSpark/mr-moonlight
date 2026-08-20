# Class 12 — The AI Production Pipeline: Shipping · Executive Summary

**⚠ Sets Assignment #10 (mandatory, 1 September) — the one that decides the course.**

**One line:** the only question that matters is whether a stranger can click a link and play your game in two minutes.

## The question

> *"Can a classmate play your game in 2 minutes by clicking a link? If the answer is no — everything between here and the assignment exists to fix that."*

**The three blockers the session eliminates:**
1. Manual steps between running your agents and playing your game
2. A content generation pass you haven't costed before running it
3. A game that works on your machine but won't launch on anyone else's

## The packaging checklist — the Unity row

| Engine | Build target | Deploy | One-click play |
|---|---|---|---|
| **Unity** | **WebGL** | **itch.io (WebGL upload)** | **Runs in browser** |
| Unreal | Windows .exe | itch.io (zipped) | Players must download and extract |
| Browser/Phaser | HTML+JS bundle | itch.io / GitHub Pages | No download |

**The one-click test:** *"Can a classmate who has never seen your project play your game within 2 minutes of clicking the link? If the answer is no — you are not shipped yet."*

## The bar, stated honestly

> *"Reduce the manual steps between running your agents and playing your game. Full automation is a stretch goal, not a requirement."*

Map your end-to-end flow across four questions: what does the agent layer produce and in what format · how do you know a generated asset is correct (what rule must it satisfy) · where does the file land in the engine · what is your build command and how long does it take.

## Local LLMs

**Ollama** and **llama.cpp** — zero per-token cost, bounded by your hardware. What to run where:

| Task | Local | Cloud |
|---|---|---|
| Short dialogue barks, item descriptions | ✓ | — |
| Narrative consistency check | ✗ fails | ✓ |
| Critic/evaluator agent | ✗ weak | ✓ |
| Code generation | simple only | complex |

> *"Use local for volume, cloud for reasoning."*

VRAM: 8B ≈ 8GB · 13B ≈ 14GB · 70B ≈ 48GB (not consumer hardware).

## Cost analysis

Open your API dashboard **now**. What does each agent do, how many calls per full run, what's the per-call token cost, total = calls × cost. *"Write the total. This number goes in your assignment."*

Worked example from the slides: 500 lines × 80 input tokens = 40k in ($0.12) + 500 × 60 output = 30k out ($0.45) → **~$0.57 for the pass.**

**Optimizations:** strategic caching (identical queries shouldn't re-bill) · downsizing models (reserve flagship LLMs for orchestration and final review only).

## Case study: EA SEED

Battlefield V had 601 testable features. Manual QA coverage would be ~500,000 hours ≈ 300 work-years. RL agents trained to explore and exploit — *"they don't play to win, they play to break."*

> *"You don't have 601 features. You might have 6. What's the smallest test an agent could run on your game right now that would tell you something you don't already know?"*

## Assignment #10 — the gate

Three deliverables: **a playable link** · **pipeline source code + a video of it running** · **a 1-page pipeline audit with real cost figures.**

> **GATE CRITERION: no link or a broken link = maximum 50% score across the entire assignment.**

Rubric: Playable Link /2.0 · Pipeline-to-Game Connection /3.0 · Engine Integration /2.0 · Cost Analysis /2.0 · Pipeline Audit /1.0.

And the line that should shape how you write it:
> *"Directness and accuracy are rewarded; architectural sophistication is not."*

## Takeaway for Mr. Moonlight

This session **is** your September 1 milestone, and it confirms the WebGL decision is non-negotiable rather than a preference. Three things to start now, not later:

1. **Get a WebGL build onto itch.io as a private page this week**, however ugly. The gate is binary and it's worth half the assignment. An ugly build that loads beats a beautiful one that doesn't exist.
2. **Start logging API costs today.** The rubric demands actuals from a real run, not estimates — and you cannot reconstruct them afterwards.
3. **Record the pipeline running.** The video is required evidence for the Pipeline-to-Game Connection criterion, worth 3 points.
