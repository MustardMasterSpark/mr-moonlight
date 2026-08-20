# Class 0 — Welcome / Orientation · Detailed Notes

## Session agenda
1. Meet your instructor
2. Course structure
3. Assignments & final project overview
4. Environment setup walkthrough
5. Student breakout session
6. Interactive design exercise

## Instructors

**Joshua Burdick** — Localization Automation Engineer at **Epic Games**, developing AI-powered tools for the global production pipelines of **Fortnite** and **Unreal Engine**. Positioned as bridging enterprise architecture, AAA game development, and AI automation.
**Avery Shelly** — Associate Instructor.
Logos shown: Epic Games, Warner Bros. Games, TCGplayer.

## Teaching philosophy (explicit contrast slide)

| Not this | This |
|---|---|
| Basic tutorials — isolated demos, surface-level overviews, concepts disconnected from production realities | **A game players can finish** — game-first engineering where every technique exists to ship a specific playable game, not to demonstrate architectural competence |

> *"Every technique in this course exists to get one more game shipped. The pipeline is the means. The game is the point."*

## Course structure

- 14 interactive classes over 7 weeks
- Applied engineering, **not** foundational machine-learning math
- *"Build the tools of a virtual studio, not shortcuts around the work"*
- Course journey: analyze/define agent roles → build dynamic data pipelines → optimize token costs and communication patterns → deploy a completed AI-driven game project
- Discord channel for async collaboration

## Grading system breakdown — verbatim

> *"The grade reflects the thesis. A sophisticated pipeline that didn't ship a game is a 20."*

- **The Game Itself (50%)** — final playable product quality — *"the only grade that matters"*
- **AI-Driven Dev Pipeline (20%)** — multi-agent architecture and automation
- **Technical Quality (10%)** — code standards and system stability
- **Documentation & Architecture (5%)** — design logic and written clarity
- **Presentation & Demo (10%)** — communication and showcase skills
- **Peer Feedback (5%)** — collaborative engagement

## Assignment progression (as presented)

- **#1–#2 GDD draft → GDD final draft.** Define the game. Scope agents to what that game needs. Budget tokens before writing code.
- **#3–#4 Agent crew → dynamic content pipeline.** First multi-agent crew for *your* game. RAG-powered generation feeding your world, not a generic demo world.
- **#5–#7 Coding agent → GER pipeline → Style Guide Agent.** A coding agent on your capstone codebase. Evaluate and refine your own output. Enforce the aesthetic constraints your game requires.
- **#8–#9 Narrative engine + adversarial QA (OPTIONAL).**
- **#10 Complete AI dev pipeline.** Integrate everything, document the architecture, ship the game.
- **Capstone: the playable game.** *"This is the only grade that matters at 50%."*

## Environment setup

**Required:**
- Access to an LLM (Claude, ChatGPT, Gemini) — API access **or** a subscription, either works. Demos use Claude; concepts apply to any LLM.

**Recommended but not required:**
- A code editor (VS Code, Cursor)
- Git + GitHub (version control and submission)
- A game engine (Phaser, Unity, Godot, Pygame — your choice)
- Python (for scripting automations — not required)

> *"You can complete this course with just an LLM subscription and a game engine."*

**Stack named on the slides:** Python 3.10+, Visual Studio Code, Git + GitHub, Unity 2022.3 LTS / Unreal Engine 5 / browser engine.
**AI toolset named:** Claude API (primary), **Ollama** (local models, offline/cost-efficient), **Docker Desktop** (reproducible isolated environments).

**Pre-course "Game Dev Fast Track" resources** in the portal: Intro to Git for Game Projects · Choosing Your Game Engine · Understanding APIs.

## Exercises

**Breakout — group introductions.** Prompt: *"What is your primary reason for taking this course, and what specific development bottleneck do you hope multi-agent AI will solve for you?"*

**Interactive design exercise.** Part 1: describe a game you've wanted to make but haven't, in one sentence — and name the one thing that's been hardest to build alone. Part 2: *"Everything in this course exists to remove that blocker for the specific game you just described. That sentence you just wrote? That's your anchor."*

Example project shapes shown: RPG with AI-generated quest lines · strategy game with adversarial QA agents · platformer with procedural generation.

## "How to get unstuck with AI" (a genuinely useful slide)

1. **Describe the problem** — *"My agent outputs JSON but my game won't load it"*, not *"it doesn't work"*
2. **Paste the error** — copy the actual message. *"What does this error mean and how do I fix it?"*
3. **Have it read the docs** — *"Read the Phaser documentation for loading JSON files and tell me what I'm doing wrong in this code: [paste]"*
4. **Verify the answer** — *"Are you sure? Show me where in the docs this is documented."*
5. **Still stuck** → post in Discord with what you tried, the error, what the LLM suggested, and why it didn't work.

---

## Mr. Moonlight application

**Directly relevant**
- The 50% weighting is the strategic justification for keeping coursework minimal. Half the grade is Unity work.
- *"A game someone can pick up, play, and finish"* is the same bar as Assignment #10's playable-link gate — and the same bar as the Sept 1 milestone.
- Ollama is worth remembering for Assignment #10's cost-reduction requirement: it lets you claim a real before/after token saving on bulk generation.

**Not relevant**
- The engine question is settled — Unity 6.3 LTS, WebGL target. Ignore all Phaser/Pygame framing in later sessions; translate it to Unity.
- The breakout and design exercises produced nothing that carries forward.

**Watch out for**
- The course's default project shape is a roguelike/RPG with generated *content* (quests, NPCs, dialogue trees). Mr. Moonlight is a hand-authored linear FPS demo. Where later sessions assume "your game generates content at scale", the honest mapping for Mr. Moonlight is **generated dialogue barks, system messages and objective text**, plus **agent-written C# systems** — not procedural levels.
