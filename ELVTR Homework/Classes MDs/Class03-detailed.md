# Class 3 — Stress-Testing a GDD with an Agent Review Crew · Detailed Notes

## Agenda
1. Positive parts & stress areas of the GDD
2. Simulating player feedback with agents
3. Demo #2: stress-testing a GDD with an agent review crew
4. Moving on to real people
5. Breakout: share GDD drafts & give feedback
6. Assignment #2: GDD final draft

## 1. What to validate first (the "positive parts")

Before active development, evaluate the GDD for:
- **Core loop** — validate the central gameplay loop is clearly defined and internally consistent
- **Agent roles** — confirm each agent has a well-scoped, non-overlapping responsibility
- **Token budget** — establish realistic allocations per agent so the pipeline stays cost-efficient

## 2. Stress areas — what the agents flag

| Stress area | The question | Red flag |
|---|---|---|
| **Scope creep** | Can this be built in the time you have? | 5+ agent types for a solo developer |
| **Unclear agent roles** | What exactly does each agent produce? | Agents described by vibe, not by output format |
| **Missing mechanics** | What happens when the player takes an action? | The GDD describes a world but not a game loop |

## 3. The synthetic audience

AI agents deployed as a synthetic audience "play through" the game's concepts mentally and generate simulated feedback before any code exists.

- **Distinct AI personas** — multiple unique agent personas each review the same document from a different player archetype: the optimizer, the explorer, the casual player.
- **Simulated feedback loop** — each agent generates responses based on the proposed rules, exposing how different player types might exploit or struggle with the mechanics.
- **Rapid iteration** — compresses weeks of early playtesting into a single session, surfacing critical design issues at the document stage.

### Worked logic-gap example

**The GDD says:** *"Players earn 10 gold per enemy defeated, and the final boss requires 500 gold to unlock — but the game caps player gold at 200."*
**Logic gap:** the win condition is mathematically unreachable under the stated constraints.
**Agent's breakdown:** flags the hard cap inconsistency, notes the blocked progression path, and proposes either raising the cap, reducing the cost, or introducing a secondary currency mechanic.

> *"This approach transforms the GDD from a static document into a living, testable prototype."*

## 4. Demo #2 — the agent review crew

Three specialised reviewers:
- **Exploit Hunter Agent**
- **Narrative Consistency Agent**
- **Pacing & Flow Agent**

To prove efficacy, the demo feeds **famous historical design documents** into the crew:
- **David Brevik's original Diablo pitch document** — agents expose early tension points in the loot economy and flag the real-time vs. turn-based design pivot as a structural risk.
- **Black Isle's original Fallout 3 (Van Buren) design document** — agents surface scope concerns around open-world quest density and highlight strong faction-design logic as a validated pattern.

## 5. Moving on to real people — the limits of synthetic feedback

| What agents can verify | What only humans can verify |
|---|---|
| Mathematical consistency of rules | Whether a game is actually fun |
| Logical contradictions in design | Emotional engagement and satisfaction |
| Edge cases and exploitable loops | Intuitive feel of controls and pacing |
| Narrative inconsistencies | Cultural context and social dynamics |

> *"Agents can verify if a game works mathematically, but only human players can verify if a game is actually fun."*
> *"Agents find logic gaps. Humans find fun gaps."*

**Getting human feedback — the three questions to ask:**
1. *"Can you tell me what the player does?"* — if they can't, the document isn't clear enough.
2. *"Does this sound fun to you?"* — agents can't answer this.
3. *"What's missing?"* — fresh eyes catch what you stopped seeing.

**Where to get it:** post in the course Discord · ask a friend who plays games (**not** a developer) · read your own document after not looking at it for 24 hours.

## 6. What good revision looks like

| Before | After |
|---|---|
| *"The AI will make the game more interesting by generating dynamic content."* | *"The Content Agent generates NPC dialogue lines in JSON format. Each line includes: speaker_id, tone (from a fixed list of 5), and a max length of 40 words. The Style Guide Agent evaluates each line against the game's dark fantasy tone."* |

**What to revise:** unclear agent roles → name each agent, define its output format · vague mechanics → describe exactly what happens when the player takes an action · scope creep → cut anything a solo developer can't build in the remaining weeks.

**How to revise:** work from the agent crew's flagged stress areas first · address the most critical human feedback next · **read the revised section aloud — if it sounds vague, it is vague.**

## 7. Assignment #2 — GDD final draft (due 23 July, complete)

Should reflect: what the agent stress-test flagged, what humans caught, and your own judgment.

**Rubric:** Game Specificity /3.0 · **Revision & Growth /2.5** (at least one meaningful change visible and explained) · Agent Role Clarity /2.0 · Scope Realism /1.5 · Document Quality /1.0.

---

## Mr. Moonlight application

**Directly relevant — and genuinely worth doing**
- **Run the three-persona review over the Linear issue set instead of a GDD.** This is the one technique from the session that pays for itself on an FPS. Concrete things an Exploit Hunter would probe in Mr. Moonlight:
  - Can the player break the **Zealot sneak state** by spinning the camera continuously, so the zealot never satisfies "player is not looking at me"?
  - Can the **wolf circle formation** pin the player against terrain, or does the circle radius fail on a slope?
  - Does the **stretcher collider** allow the player to wedge Scott through geometry, or get permanently stuck between two trees?
  - Can the player **skip the telescope minigame** by walking away, leaving the event director waiting forever?
  - Does the **spotter flare** spawning 3–10 new spotters have any upper bound if two spotters fire flares near each other?
  - Does the **stamina lock** in Scene 1 release if the player dies before drinking?
- The **"before/after revision"** example is a good template for how to phrase Linear issue acceptance criteria: name the format, name the fields, name the limits.
- The **read-it-aloud test** applies directly to issue descriptions.

**Not relevant**
- Assignment #2 is complete and the GDD is deprecated.
- The Diablo/Fallout document analysis is a demo, not a technique you need.

**Watch out for**
- The "5+ agent types for a solo developer is a red flag" line is worth remembering when writing Assignment #10. Mr. Moonlight's honest agent count is small — that is a strength under this rubric, not a weakness. Do not invent agents to look sophisticated.
