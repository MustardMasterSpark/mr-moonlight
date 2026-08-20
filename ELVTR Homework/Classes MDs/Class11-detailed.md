# Class 11 — The Chaos Crew: Adversarial AI for Game QA & Balance Testing · Detailed Notes

> Sets **Assignment #9 — OPTIONAL** (due 27 August, 4–6 hours).

## Agenda
1. Adversarial playtesting agents
2. Automated bug reporting
3. Demo #9: balance testing at scale
4. Case study #7: Riot Games & League of Legends
5. Exercise: guess the win rate
6. Assignment #9 (optional): adversarial QA agent

## 1. Adversarial playtesting agents — the synthetic audience

AI agents as tireless, scalable playtesters that identify stress areas human testers would take months to uncover.
- **AI as playtester** — agents iterate through thousands of movement patterns, attack sequences and boundary interactions far faster than any human QA team
- **Stress testing by design** — unlike human players who follow intuitive paths, synthetic agents are **explicitly programmed to probe edge cases, exploit geometry, and break intended flows**
- **Rapid iteration** — *"where a human tester discovers one bug per hour, an agent running 3,000 simulations can surface systemic imbalances in under 20 minutes"*

### Adversarial agent architecture

> *"Standard bots play to win. Adversarial agents play to break. The architecture is fundamentally different — and that difference is what makes them valuable."*

| Component | What it does |
|---|---|
| **Define failure parameters** | Explicitly specify what "breaking" the game looks like: out-of-bounds access, infinite loops, unwinnable states |
| **Critic Agent directive** | The Critic Agent's core objective is **not to win but to expose vulnerabilities** — it seeks boundary-breaking behaviours at every decision point |
| **Structured output** | Every anomaly logged with context: location, state, input sequence, and a reproducibility score for the dev team |

## 2. Automated bug reporting

> *"The transition from random testing to actionable engineering data requires teaching agents to produce output that human developers can immediately parse and assign."*

| ✗ Unstructured log | ✓ Agent bug report |
|---|---|
| Walls of raw console output with no context, no location data, and no reproducibility notes. Developers spend hours triaging noise. | Structured documentation: bug ID, severity, affected state, input sequence to reproduce, and suggested priority — instantly assignable to a developer. |

> *"The goal is zero-friction handoff: the agent's output should slot directly into your team's existing project management workflow."*

**Raw decision stream → structured JSON:**
```
MOVE -> RIGHT              {
MOVE -> RIGHT                "bug_id": "BUG-042",
JUMP -> triggered            "severity": "high",
COLLISION -> wall_type_03    "location": { "x": 1240, "y": 88 },
STATE -> stuck_loop          "state": "stuck_loop",
BOUNDARY -> exceeded_x       "sequence": ["MOVE_R","JUMP","WALL"],
                             "engine": "Phaser_HTML5"
                           }
```

## 3. Demo #9 — balance testing at scale

Mass simulation compresses months of QA into minutes: single level → clone & distribute → aggregate results.

### The two-layer architecture (the important part)

| Layer | What runs | Cost |
|---|---|---|
| **Layer 1: rule-based bots** | Thousands of fast, free bots following scripted heuristics — random walks and stat-weighted decision tables. **No LLM calls.** They generate raw match data: win rates, time-to-kill, resource curves. | Free |
| **Layer 2: LLM interpretation** | **One** LLM call reads the aggregated results after the simulation finishes and produces a human-readable balance report. | One call |

*"By leveraging localized LLM pipelines or fast-executing heuristic bots, entire QA cycles that once required dedicated teams are reduced to a single automated run — surfacing balance issues before any human tester ever loads the build."*

## 4. Case study #7 — Riot Games & League of Legends

**The champion balance problem:**
- **The cascade effect** — a small tweak to a single champion's ability, even a 2% damage increase, can cascade into massive meta shifts across the roster
- **Scale of interaction** — 160+ champions means thousands of possible matchup combinations. Human testers cannot predict second- and third-order effects at this scale without computational simulation
- **Patch frequency** — balance patches on a roughly two-week cycle, requiring rapid, data-driven confidence before each update ships to millions of players

**Applied agentic simulation:** player path heatmaps revealing where engagement clusters, where choke points form, which zones are never contested · pre-patch meta prediction · *"The simulation pipeline you build in Demo #9 mirrors — at a smaller scale — exactly what professional studios use for live-service balance management."*

## 5. Exercise — guess the win rate

Study two character stat cards, predict the winner, then see the simulation results. *"After 1,000 simulated agent battles, the data frequently contradicts human intuition. This is the core lesson of balance testing."*

## 6. Assignment #9 — adversarial QA agent (OPTIONAL, due 27 August)

**Core requirements:** run a testing loop continuously inside your game, cycling through movement, interaction and boundary-probing behaviours · log bugs and edge cases (boundary breaks, stuck states, unintended collisions, exploits, gameplay logic violations) · output a structured report (JSON or CSV) with **location**, **error type**, **game context**.

**Deliverables:** agent code · structured report from at least one run · short ReadMe (what did the agent find, were you surprised).

**Rubric:** Findings /4.0 (*at least one real bug, exploit or edge case; the report names the specific mechanic or system*) · Agent Logic /3.0 (*actively tries to break the game, with a clear strategy for what "broken" means*) · Structured Report /2.0 · ReadMe /1.0.

> **⚠ The brief's own advice:** *"If your game isn't testable by an adversarial agent yet, you should spend this time working on your capstone project instead."*

---

## Mr. Moonlight application

**Recommendation: SKIP THIS ASSIGNMENT.** It is optional and the brief explicitly tells students in this position to spend the time on the capstone instead. On 27 August the demo will be four days from the itch.io release.

**What to take instead — a one-hour manual test checklist for the Sept 1 build**

The valuable transferable idea is *"define failure parameters explicitly"*. Mr. Moonlight has specific, predictable failure modes worth writing down and hand-testing:

**Softlocks / event director deadlocks**
- Player walks away from the telescope mid-sequence — does the event director wait forever?
- Player dies during a scripted cutscene — is damage actually disabled as the cutscene issue requires?
- Player leaves the cabin before Vernon finishes the dialogue chain — does the pistol still get granted?
- Player drops the stretcher and walks to the chapel without Scott — does the Furman fight still trigger?
- Player kills the Furman *before* picking the radio back up — does the door sequence fire?

**Geometry and movement**
- Stretcher collider wedged between two trees or in the mine tunnel — can the player free it?
- Player out of bounds on the island terrain edge / falls through the terrain seam at the mine entrance
- Enemy A* pathfinding on steep slopes — do wolves or spotters get stuck or slide?
- Camera clipping through the cabin walls during the long interior cutscene

**State and stat exploits**
- Stamina lock in Scene 1: does it release if the player dies before drinking?
- Drug relapse multiplier: can stacking blunts push a stat above 1.0?
- Boots equip animation: is the player really vulnerable, and can the animation be cancelled?
- Storage cap: can the player exceed 4 item types before the backpack?
- Turret: can the player exit and re-enter to reset the 30-round belt?

**WebGL-specific**
- Alt-tab / lose focus during a cutscene
- Browser fullscreen toggle mid-scene
- Checkpoint reload after a browser refresh

**The cost pattern is worth remembering for Assignment #10:** thousands of free rule-based runs + **one** LLM call to interpret the aggregate. That is a legitimate, citable cost-reduction architecture even if you never build the QA agent.

**Not relevant**
- League of Legends balance simulation. Mr. Moonlight has 3 weapons and 4 enemy types on a fixed linear route — the balance surface is small enough to tune by hand.
- Heatmaps, meta prediction, 3,000-run simulation infrastructure.
