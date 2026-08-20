# Class 11 — The Chaos Crew: Adversarial AI for QA · Executive Summary

**Sets Assignment #9 — OPTIONAL. Recommend skipping, and the course agrees.**

**One line:** agents that play your game to *break* it, not to win, and report bugs in a format you can act on.

## The core distinction

> *"Standard bots play to win. Adversarial agents play to break. The architecture is fundamentally different — and that difference is what makes them valuable."*

**Architecture:** define failure parameters explicitly (out-of-bounds access, infinite loops, unwinnable states) → a Critic Agent whose objective is to *expose vulnerabilities*, seeking boundary-breaking behaviour at every decision point → structured output logging every anomaly with location, state, input sequence and a reproducibility score.

## Structured bug reports

The difference between useless and useful:

| Unstructured log | Agent bug report |
|---|---|
| Walls of raw console output, no context, no location data, no reproducibility notes. Developers spend hours triaging noise. | `bug_id`, `severity`, `location`, `state`, `sequence`, `engine` — instantly assignable |

```json
{ "bug_id": "BUG-042", "severity": "high",
  "location": {"x": 1240, "y": 88}, "state": "stuck_loop",
  "sequence": ["MOVE_R","JUMP","WALL"], "engine": "Phaser_HTML5" }
```

> *"The goal is zero-friction handoff."*

## Balance testing at scale — the honest architecture

The demo runs 3,000 simulations, and the smart part is that **almost none of them use an LLM**:

- **Layer 1: rule-based bots.** Thousands of fast, free bots following scripted heuristics. **No LLM calls.** They generate raw match data: win rates, time-to-kill, resource curves.
- **Layer 2: LLM interpretation.** *One* LLM call reads the aggregated results afterwards and writes a human-readable balance report.

That's a genuinely good cost pattern worth remembering for Assignment #10.

## Case study: Riot Games & League of Legends

160+ champions, two-week patch cycle, thousands of matchup combinations. Human testers cannot predict second- and third-order effects at that scale. Riot uses automated testing to verify champion interactions before patches ship.

The workshop's lesson: after 1,000 simulated battles, **the data frequently contradicts human intuition.**

## Assignment #9 (OPTIONAL, 27 August, 4–6 hours)

An adversarial tester running against your capstone, logging bugs to structured JSON/CSV.

**The brief tells you to skip it:**
> *"If your game isn't testable by an adversarial agent yet, you should spend this time working on your capstone project instead."*

## Takeaway for Mr. Moonlight

Skip the assignment. But the **failure-parameter list** is worth ten minutes of thought regardless, because Mr. Moonlight has genuinely fragile spots that a bored playtester will find: getting the stretcher wedged in geometry, walking away mid-cutscene, dying during a scripted sequence, the event director waiting forever on an objective the player has already bypassed, and enemy pathfinding on steep terrain. Write those down as a manual test checklist for the Sept 1 build — that costs you an hour instead of six and catches the same class of bug.
