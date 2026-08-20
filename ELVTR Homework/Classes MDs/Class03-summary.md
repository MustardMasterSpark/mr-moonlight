# Class 3 — Stress-Testing the GDD / The Synthetic Audience · Executive Summary

**One line:** use agents as a fake playtest to find logic gaps in a document, then remember that only humans can tell you if it's fun.

## The synthetic audience

Deploy multiple agent personas — the optimizer, the explorer, the casual player — to read the same design document from different player archetypes and generate simulated feedback **before any code is written**. Compresses weeks of early playtesting into one session.

Demo used three specialised reviewers: an **Exploit Hunter Agent**, a **Narrative Consistency Agent**, and a **Pacing & Flow Agent**. They were fed real historical design documents (Brevik's Diablo pitch, Black Isle's Fallout 3) and surfaced genuine tension points.

**Worked example of a logic gap:**
> *"Players earn 10 gold per enemy defeated, the final boss requires 500 gold to unlock — but the game caps player gold at 200."* The win condition is mathematically unreachable. The agent flags it and proposes three fixes.

## The three stress areas agents flag

| Area | Red flag |
|---|---|
| **Scope creep** | 5+ agent types for a solo developer |
| **Unclear agent roles** | Agents described by vibe, not by output format |
| **Missing mechanics** | The document describes a world but not a game loop |

## The limit — and it's the honest slide of the session

| Agents can verify | Only humans can verify |
|---|---|
| Mathematical consistency of rules | Whether a game is actually **fun** |
| Logical contradictions | Emotional engagement and satisfaction |
| Edge cases and exploitable loops | Intuitive feel of controls and pacing |
| Narrative inconsistencies | Cultural context and social dynamics |

> *"Agents find logic gaps. Humans find fun gaps."*

## What good revision looks like

Before: *"The AI will make the game more interesting by generating dynamic content."*
After: *"The Content Agent generates NPC dialogue lines in JSON. Each line includes speaker_id, tone (from a fixed list of 5), and a max length of 40 words. The Style Guide Agent evaluates each line against the game's dark fantasy tone."*

## Takeaway for Mr. Moonlight

The genuinely reusable idea: **run a three-persona review over your Linear issue set**, not over a GDD. An exploit hunter reading your enemy behaviour issues would catch things — for instance, whether the Zealot's "only moves when your back is turned" rule can deadlock against the player's own vision cone, or whether the wolf circle formation can trap the player against terrain. That is a real bug-prevention exercise, and it doubles as an artefact you can cite.
