# Class 8 — The Level Architect: The GER Pattern · Executive Summary

**⚠ This is the session behind Assignment #6 (mandatory).**

**One line:** Generate → Evaluate → Refine — the loop that stops your pipeline shipping bad content without you reading every line.

## The problem it solves

> *"You built a pipeline in S06. You ran it. Some outputs were good. Some were broken. Some were technically valid — but completely wrong for your game. You reviewed them manually. That doesn't scale."*

## GER

| Stage | What it does |
|---|---|
| **Generate** | An agent produces an output. Prioritize speed and variety — produce enough options to explore directions. |
| **Evaluate** | A second agent **or rule set** checks it. |
| **Refine** | If evaluation fails, loop back with the specific error. |

*"You've seen this pattern before under other names: test-driven development, red-green-refactor, QA loops. Same principle."*

## Evaluation happens in two layers

1. **Deterministic checks** — parsing, compiling, test cases. *"If code can verify it, use code."* Objective and repeatable.
2. **Verifier agent** — handles what code cannot: quality, design patterns, whether the implementation matches intent.

> *"Don't pretend code can catch everything."*

## The Refiner and the Circuit Breaker

Up to **3 passes**. Pass 1: smallest fix. Pass 2: tighten using the same error context. Pass 3: final attempt — then **escalate** with a clear problem statement.

The refiner receives *the specific error plus the original output* and fixes that exact issue, rather than blindly regenerating the whole thing.

> *"You're the developer. The agent works for you. The refiner is not an autonomous guesser — it is a controlled tool that reports back when it hits a limit, so you can decide the next move."*

## The GER loop in action (the slide's own example)

```
Generator (pass 1):  Room 7: treasure chest in northeast corner
                     Room 7: northeast corner marked IMPASSABLE
Evaluator:           FAIL — item placement conflicts with navigation mesh
                     Reason: northeast corner has no valid pathfinding node
Refiner:             Move treasure chest to centre of room (valid node confirmed)
Generator (pass 2):  PASS ✓
```
> *"This is the GER loop in action — no human reviewed this placement."*

## Case study: No Man's Sky

Launched 2016 with procedural generation at enormous scale and **no evaluation rules**. Planets technically valid, completely empty of meaning. Players called it *"infinite boredom."* Rule layers were added post-launch, after the damage.

> *"This is what your pipeline produces today if you ship it without constraints. You have the advantage: you can build the evaluator first."*

## Assignment #6 — mandatory, was due 18 August

Pipeline code (**Generator, Evaluator, Refiner, Circuit Breaker**) + a Pre-Build Declaration + a ReadMe. The Pre-Build Declaration is three questions, under 150 words, submitted *before writing any code*:

1. What content type does your game generate manually, inconsistently, or not at all?
2. What specific rule must every piece of that content satisfy?
3. What does a failure look like — concretely, in your game's terms?

**Rubric:** Working Pipeline /3.0 · Evaluator Quality /3.0 (a **specific** rule, not a generic validity check) · Game Connection /2.0 · ReadMe /2.0.

## Takeaway for Mr. Moonlight

You can answer all three declaration questions honestly, today, without inventing anything. See `Outputs/Assignments MDs/Assignment06.md` for the filled-in version — the short answer is **Tracey's thought lines and system messages**, the rule is **her voice as defined in the character profile and style guide**, and a failure is **Tracey sounding cheerful, or explaining something the "show don't tell" rule forbids.**
