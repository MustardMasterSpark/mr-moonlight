# Class 7 — Autonomous Agency: Goal-Oriented Reasoning · Executive Summary

**One line:** the shift from agents that execute tasks you name to agents that read your codebase, decide what's missing, and write it — and the two ideas from this session you're already using.

## The shift

> *"We're going from agents that execute tasks to agents that decide what tasks to execute."*

**Codebase perception** — the agent reads the project the way a new developer would: directory structure → existing files and imports (what's wired up vs stubbed) → diff against the design doc → identify gaps.

## Utility scoring

Multiple things need building. How does the agent choose? By scoring on **dependency order** (what must exist first), **blockers** (what's preventing other work), **project priority** (what the design marks as critical), and **current state** (partially built vs untouched).

Worked example: *inventory system ← blocking shop UI and loot drops; enemy AI ← blocking core game feel; save/load ← blocking progression; level generation ← not blocking anything yet; UI polish ← last, depends on everything above.*

> *"The order isn't random. It's derived from your GDD's dependency graph."*

## Markdown as agent memory

No databases. Four plain-text sections the agent maintains and you can edit: **BUILT** (files, classes, features completed) · **FAILED** (dead ends and compile errors, so it doesn't repeat them) · **NEXT** (remaining features, priority order, known blockers) · **DECISIONS** (why it chose one approach over another).

## Frameworks vs raw orchestration — the pivot

This session reverses Class 4. When your agent needs to read *your* file structure, run *your* build tool, check for compile errors and decide what to do next based on results — **drop the framework.**

| CrewAI | Raw direct calls |
|---|---|
| Handles routing, retries, context passing | You control every step |
| Good for complex crews | You see every decision the agent makes |
| Less visibility between steps | More work, full transparency |

> *"For this assignment: RAW. You're writing code in your own codebase. You need to see every decision the agent makes about your game before it writes a file."*

## The blackboard

Live visibility into what the agent scored, what prompts it issued, and what it generated — **logged before it touches your codebase.**

> *"Without the blackboard, you cannot tell whether the agent did what you intended. This is not a debugging tool. This is how you stay in control of your own codebase."*

## Assignment #5 (done)

## Takeaway for Mr. Moonlight

You already do all of this — Claude Code *is* the raw-orchestration goal-oriented agent, and Linear *is* the prioritized gap list, better than any utility-scoring function you could write. The markdown memory pattern maps onto the change log the Linear notes already ask for.

One line worth pinning above the desk: *"Read every generated file before it goes into your game. 'Matches player.py patterns' is not the same as 'matches your game's design intent.' The agent writes the mechanical code. You decide if it's right."*
