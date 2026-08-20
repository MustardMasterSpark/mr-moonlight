# Class 1 — Foundations of Agency · Executive Summary

**One line:** what an "agent" is, and the single distinction that defines the whole course — agents that *build* your game vs. AI that *runs inside* your game.

## The core distinction

| Agents in your pipeline | AI in your game |
|---|---|
| Write code | Control an NPC |
| Generate level content | Drive enemy behaviour |
| Review your GDD | Power a dialogue system |
| Run QA passes | Simulate a crowd |

> *"This course is the left column. Everything in the right column is a different course."*

**This is the most useful slide in the course for you.** It means the course is *not* asking you to put an LLM inside Mr. Moonlight. Your wolves and zealots are finite state machines with A* pathfinding, and that is correct — no session is going to tell you otherwise.

## The mental model: Sense → Think → Act

- **Sense** — the agent reads environment data: file states, asset manifests, test logs
- **Think** — it queries an LLM to reason and plan multi-step actions
- **Act** — it executes a script or tool call that produces a measurable change

**Reactive vs deliberative agents:** reactive = trigger-response, cheap and fast, for high-frequency low-complexity work. Deliberative = multi-step planning, expensive, for work where output quality justifies the cost. *"Selecting the wrong cognitive depth wastes budget and introduces latency."*

## The AI contract

The explicit design commitment defining **where the system is free to generate** and **where it must obey rules**. Enforced by three guardrails:

1. **Lore consistency** — outputs validated against the design doc
2. **Format validation** — structured schemas enforced before downstream consumption
3. **Safety filters** — moderation before anything reaches players or the pipeline

## Case studies

**Ubisoft** — AI for asset creation, dialogue and world-building; writers focus on critical narrative, AI handles volume and variation.
**Epic** — AI tooling inside Unreal workflows for fast prototyping.
**Netflix** — betting that AI-powered tools let small teams build games that used to need large studios. *"AI doesn't replace the developer — it multiplies what one developer can do."*

## Takeaway for Mr. Moonlight

You are the "small team" thesis in practice. Nothing here needs to change in your Unity project. The one durable idea to carry forward is the **guardrail triad** — it is exactly what the Style Guide Agent in Assignment #7 implements, and what your dialogue-line generator should enforce.
