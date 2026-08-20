# Class 7 — Autonomous Agency: Engineering Goal-Oriented Reasoning & Intent · Detailed Notes

## Agenda
1. Goal-directed planning
2. Utility scoring
3. Codebase perception
4. Persistent state & priority scoring
5. Frameworks vs. raw orchestration
6. Demo #6: blackboarding to make agency visible
7. Assignment #5: goal-oriented coding agent

**Opening:** *"In S06 you connected agent output to your game engine. Today's question: what if the agent could look at your codebase, figure out what's missing, and write it? We're going from agents that execute tasks to agents that decide what tasks to execute."*

## 1. The shift

- **Your agents so far** follow instructions — *"generate this dialogue," "create this content."* They execute tasks you define.
- **At S07 you already know what's missing.** You have a running game, a design doc, and a list. *"The question isn't what's missing — you already know that. The question is whether you can close those gaps."*
- **The goal-oriented agent does the mechanical build work while you stay focused on the game that needs to exist. You decide what to build. The agent executes.**

## 2. Codebase perception

The agent reads your project the way a new developer would:
1. **Directory structure** — scans folder layout, file names, project organization
2. **Existing files & imports** — reads what's wired up vs what's stubbed out
3. **Diff against the design doc** — compares the feature list to what's actually been built
4. **Identify gaps** — determines what's missing and what needs building next

> *"What the agent sees determines what it decides to build next."*

## 3. Utility scoring for development priorities

The problem: multiple things need building — the inventory system, save/load logic, the enemy spawner, a broken import in the main scene. *"You can't build the shop UI before the inventory system exists. The agent must understand dependency order, blockers, and what the project actually needs right now."*

**The scoring system accounts for:**
- **Dependency order** — what must exist first
- **Blockers** — what's preventing other work
- **Project priority** — what the design doc marks as critical
- **Current state** — what's partially built vs untouched

**Worked example**
> Design doc lists: inventory system, enemy AI, save/load, level generation, UI polish.
> Codebase has: player movement, basic combat, one hardcoded level.
> The agent scores:
> - Inventory system ← blocking shop UI and loot drops
> - Enemy AI ← blocking core game feel
> - Save/load ← blocking progression
> - Level generation ← not blocking anything yet
> - UI polish ← last, depends on everything above

> *"The order isn't random. It's derived from your GDD's dependency graph."*

## 4. Taking action — before/after

```
BEFORE (empty starter scaffold)      AFTER (goal-oriented agent has run)
/my-game                             /my-game
  /src                                 /src
    game.py     <- exists                game.py       <- unchanged
    player.py   <- exists                player.py     <- unchanged
    inventory.py  <- MISSING             inventory.py  <- WRITTEN by agent
    dialogue.py   <- MISSING             dialogue.py   <- WRITTEN by agent
  /data                                /data
    items.json    <- MISSING             items.json    <- GENERATED from spec
```

> **Your step after the agent runs:** *"Read every generated file before it goes into your game. 'Matches player.py patterns' is not the same as 'matches your game's design intent.' The agent writes the mechanical code. You decide if it's right."*

## 5. Persistent state via markdown

Agents track their own progress in simple markdown files. **No databases, no complex infrastructure — just readable, editable text files.**

| Section | Contents |
|---|---|
| **BUILT** | Every file created, feature implemented, class scaffolded |
| **FAILED** | Failed attempts, compile errors and dead ends, recorded so the agent doesn't repeat them |
| **NEXT** | Remaining features not yet started or completed, priority order, known blockers |
| **DECISIONS** | Why the agent chose one approach over another, patterns used, architecture notes — with timestamps |

> *"The student can read it, edit it, and feed it back into the next session. The agent picks up where it left off."*

## 6. Frameworks vs raw orchestration — the pivot

> *"CrewAI is great for learning multi-agent patterns. But when your agent needs to read your specific file structure, run your build tool, check for compile errors, and decide what to do next based on the results — you need raw orchestration. Manual API calls, custom parsing, explicit control over every step. No abstraction layer between you and the model."*

| Framework (CrewAI) | Raw (direct API calls) |
|---|---|
| Handles routing, retries, context passing for you | You control every step |
| Great for complex multi-agent crews | You see every decision the agent makes |
| Less visibility into what happens between steps | More work, but full transparency |

> *"It's not about which is more capable. It's about which gives you control when it matters. At S07, you are closing GDD gaps in a running game. Every agent decision has consequences for your codebase. For closing critical GDD gaps before the final sprint, raw orchestration gives you more visibility — but use whatever approach gets your game further along."*

**For Assignment #5: RAW.**

## 7. Demo #6 — the Code Architect and the blackboard

**The flow:** receive (design feature list + project directory) → scan & diff (what's built vs missing) → score & prioritize (dependency order and project needs) → build (write code for the highest-priority missing feature, update the markdown state file).

### The blackboard is not optional

| What it scored | What it issued | What it generated |
|---|---|---|
| Every missing feature, ranked. The utility score for each and why it was prioritized in that order. | The exact prompts sent to the model. What context was passed. What instructions shaped the output. | The code it wrote, the file it created, the decision it just made — **logged before it touches your codebase.** |

> *"Without the blackboard, you cannot tell whether the agent did what you intended. This is not a debugging tool. This is how you stay in control of your own codebase."*
> *"Your ReadMe should explain what the agent decided and why. If you can't explain the agent's decisions, you weren't in control of them."*

## 8. Assignment #5 — goal-oriented coding agent (due 13 August, complete)

**Starter scaffold provided:** a minimal LLM call loop (~30 lines) · a filesystem scanner stub (`list_files`, `read_file`) · a design-doc parser stub (`extract_required_features`) · an empty `priority_score()` · a print-based log.
**You build:** the priority scoring logic · the gap-detection pass · the code-writing step · the iteration loop (re-scan after each write, stop when done).

**Rubric:** Working Feature /4.0 · Agent Code /3.0 · Judgment & Review /2.0 · Code Quality /1.0.

---

## Mr. Moonlight application

**Directly relevant — and largely already done**
- **Claude Code connected to Unity over MCP *is* the raw-orchestration goal-oriented agent this session describes.** It perceives the codebase, reads the spec, writes C#, and reports back. You are not behind on this session; you are ahead of it.
- **Linear replaces utility scoring, and does it better.** A hand-ordered, human-prioritized backlog with real dependency links beats a heuristic scoring function written in an afternoon. That is a defensible architectural choice to state in Assignment #10 — the course rewards honesty about what you'd do differently.
- **The markdown memory pattern maps onto what the project already wants.** The Linear notes ask for *"a human readable implementation and change log MD list"* — that is exactly BUILT / FAILED / NEXT / DECISIONS. Worth formalising those four headings in the change log so it doubles as agent memory.
- **The blackboard principle** is why the workflow should be: Claude proposes → developer reads the diff → developer approves → commit. Never a silent write.
- **The quote to pin up:** *"The agent writes the mechanical code. You decide if it's right."* That is the tandem model in one sentence.

**Not relevant**
- The starter scaffold and `priority_score()` exercise — done, and superseded by Linear.
- Python file-scanning stubs; the Unity MCP already gives structured project access.

**Watch out for**
- **Dependency order genuinely matters for Mr. Moonlight and is worth writing down**, because a lot of the issue list is silently blocked:
  - The universal sound-pool behaviour blocks footsteps, which blocks enemy audio detection, which blocks the boots mechanic's whole point.
  - The vision cone prefab blocks idle/patrol/chase, which blocks every human enemy.
  - A* pathfinding blocks all enemy movement.
  - The event director blocks every scene, every objective and every cutscene — it is the single biggest blocker in the project.
  - The stat system (health/stamina/fear) blocks all the VFX issues.
  - **Nothing about the Furman, the turret or the red moon is reachable until the mine and stretcher work.**
