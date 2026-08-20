# Class 5 — Dynamic Content Generation: RAG-Powered Pipelines for Game Content · Detailed Notes

## Agenda
1. RAG for content consistency
2. Generating diverse game content
3. Persona & tone control
4. Output quality & automated consistency check
5. Case study #3: Fortnite NPC persona consistency
6. Demo #4: RAG demo — 4 content types from one lore
7. Assignment #4: dynamic content pipeline

## 1. The problem RAG solves

> *"Your GDD is 15 pages. Your agent's context window can hold maybe 3 at a time."*

| Without RAG | With RAG |
|---|---|
| The agent generates content from whatever it remembers from its training data. The output sounds like a generic game, not yours. | Before generating anything, the agent pulls the specific sections of YOUR docs relevant to what it's about to write. The output sounds like your game — because it's working from your material. |

> *"RAG = your agent refers to your game's own documents before it writes anything."*

### How to set it up — the deliberately unglamorous version

1. **Put your GDD and game docs into a folder your agent can read.** *"That's it. At this scale, your game's lore fits in the context window — no database required."*
2. **When your agent needs to generate content, load the relevant sections into the prompt first.** *"'Write a merchant's dialogue' → load YOUR merchant lore, YOUR tone guide, YOUR world rules into context."*
3. **The agent writes FROM your material, not from scratch.**

### The formal version (also presented)
Vector-embedded pipelines encode lore, dialogue and world data into searchable vector spaces for contextual retrieval · shared memory pools give all agents a unified memory layer · **lore adherence**: grounding outputs in retrieved context eliminates hallucinations and continuity errors before they reach the engine.

**Shared lore database** as single source of truth: every agent references the identical repository → reduced hallucinations → scalable consistency as the world expands.

## 2. Structuring output for the engine

> *"Generating the content is only the first half of the pipeline: formatting it is the second."*

Outputs must be in precise formats (JSON, CSV) to be cleanly digested by Unity or Unreal. **Raw unstructured text cannot be parsed by game engine pipelines.**

- **JSON** — NPC dialogue trees, quest data
- **CSV** — item databases, stat tables

Example structure shown:
```json
{
  "npc_id": "elder_voss",
  "dialogue": [
    { "trigger": "first_meeting",
      "line": "The northern vaults have been sealed for three centuries.",
      "tone": "grave" }
  ],
  "faction": "Order of Ash",
  "lore_verified": true
}
```
> *"Structured schemas prevent parsing failures."*

## 3. Persona & tone control

> *"The examples above are someone else's game. Your game has a different voice."*

**Exercise:** open your design docs, find the section describing tone/world-feel/character voice; write two prompts — one for a high-stakes moment in your game, one for ambient world-building — and run both against your lore document.

## 4. Output quality & automated consistency checking

### "Generate 10, keep 3"
Produce variations, evaluate them, autonomously select only the highest-quality content for engine integration. *"Guarantees that only lore-accurate, tonally consistent, and mechanically valid content reaches the player — without requiring manual review at scale."*

### The Critic Agent
Automated consistency checking relies on **adversarial review**: a secondary Critic Agent compares newly generated content against your game docs, catching contradictions before they reach the player.

**The pattern:**
1. Load your game docs as context
2. Feed the new generated content **+** your game docs to your LLM in one prompt
3. Ask: *"Does this new content contradict anything in the existing lore?"*
4. If yes → regenerate. If no → keep it.

```python
def critic_agent(new_quest, lore_db):
    conflicts = lore_db.check(new_quest)
    if conflicts:
        return {"status": "fail", "reason": conflicts}
    return {"status": "pass"}
```

> *"Your LLM writes the implementation. You describe what your game docs look like, what the generated content looks like, and what 'contradiction' means for your game."*

## 5. Case study #3 — Epic's Persona Device

Epic shipped a tool letting any developer build AI-powered NPCs in Fortnite. Define who the character is with a simple prompt (personality, knowledge, behaviour), pick a voice, and the NPC talks, remembers what players have done, and can trigger gameplay events.

**Tech stack:** Google Gemini for dialogue generation + ElevenLabs for voice synthesis. Available in UEFN (Experimental mode).

> *"This used to require a writing team, voice actors, and a dialogue system engineer. Now it's a prompt and a voice selection."*

**The consistency challenge** named explicitly: *"how do you keep generated content on-voice for YOUR game?"*
Source: dev.epicgames.com — AI and NPCs in Unreal Editor for Fortnite.

## 6. Demo #4 — 27 NPCs before the playtest

**Scenario:** 10 hours before a playtest. 30 NPC slots, only 3 written characters. Your lore document exists. Your tone exists. You need 27 more NPCs who sound like they live in the same world.

Four content types generated from one lore source: **NPC voice profiles** (personality, speech pattern, behavioural traits) · **dialogue lines** (character-voiced, consistent with world tone and faction) · **backstory summaries** (grounded in existing canon) · **relationship flags** (how each NPC relates to the 3 written characters).

> *"This is what RAG solves: not 'generate content at scale.' Generate content at scale that sounds like your game — because it's reading your game before it writes."*

## 7. Assignment #4 — dynamic content pipeline (due 30 July, complete)

**Deliverables:** the pipeline · three generated outputs the game actually needs · a ReadMe (what content, does it sound like your game, what did the critic catch).

**Note on technical execution:** *"Code that does not run receives 0 across all criteria. Functional code is the minimum bar for submission, not a graded achievement."*

**Rubric:** Game-Anchored Source /2.0 · Content Fit /2.5 (*name the gap: "my game is thin on X"*) · RAG Implementation /2.0 (*show query, retrieved chunk, and output side by side*) · Consistency Checking /2.0 (*the correction is shown, not just claimed*) · Voice Judgment /1.5.

---

## Mr. Moonlight application

**Directly relevant — this is the session that maps best onto the game**
- **Your retrieval corpus already exists.** `Outputs/DesignContext MDs/` is precisely the "folder your agent can read": the style guide gives tone, the character profiles give voice, the screenplay gives canon. No vector DB needed — the whole corpus is a few hundred KB.
- **The content types Mr. Moonlight can legitimately generate:**
  - **Tracey's thought lines** (the "thought system" issue — a list of context-linked unvoiced lines; this is the single best fit in the whole project)
  - **System messages** (blue text, no audio)
  - **Objective text** (16 objectives, needs consistent phrasing)
  - **UI strings** and their localization columns
  - **Enemy vocalisation cue sheets** — which sound plays when, not the audio itself
- **JSON/CSV structuring maps directly** onto the spreadsheet-driven design already specified in the Linear notes: *"The system message, the dialog system, and the event system should take their inputs from some kind of text file... I have preference for spreadsheets."* The course and your own design already agree.
- **The critic-agent recipe is the direct ancestor of Assignment #6's GER loop.** Reuse it.

**Not relevant**
- Fortnite's Persona Device — runtime NPC generation is the opposite of what Mr. Moonlight needs.
- 27-NPC-scale generation. Mr. Moonlight has **8 characters, all hand-written, 4 of them voice-acted by real people.** The demo has ~250 authored dialogue lines and they are already written.

**Watch out for — the honest boundary**
- Do not imply in any assignment that the screenplay was generated. It wasn't, and the voice actors are recording it. State the boundary clearly: *"The narrative dialogue is hand-authored and voice-acted; the pipeline generates the unvoiced text layer — thoughts, objectives, system messages, UI strings — and validates all of it, hand-written included, against the style guide."*
- That framing is actually **stronger** under the Class 9 rubric, which rewards knowing where AI generation would break the experience.
