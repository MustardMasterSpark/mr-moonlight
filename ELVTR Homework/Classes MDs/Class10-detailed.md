# Class 10 — Emergent Chronicles: Engineering Multi-Agent Narrative Engines · Detailed Notes

> Sets **Assignment #8 — OPTIONAL** (due 25 August, 4–6 hours, standalone).

## Agenda
1. Emergent narrative loops
2. Narrative consistency agents
3. Dynamic dialogue trees
4. Exercise: choose your own adventure — AI edition
5. Assignment #8 (optional): narrative engine prototype

**Framing:** *"In S09, you built a Style Guide Agent that enforces your game's aesthetic rules — tone, voice, visual style. That agent says: 'Does this output match my game?' Today's question is different: 'Does this output know what the player just did?' S09 was about consistency with your game's identity. S10 is about consistency with the player's history."*

## 1. Emergent narrative loops

| Pre-generated narratives | Emergent narrative loops |
|---|---|
| Rely on massive pre-generated decision trees that inherently limit player freedom. Every branch must be written in advance, creating rigid, finite outcomes. | Engineer stories that dynamically react and change based strictly on unpredictable player actions — no pre-written path required. |

**Engineering the loop:** continuous evaluation → analyze context → synthesize response → update state. *"This real-time synthesis loop replaces static branching trees with a living, adaptive narrative system."*

## 2. Narrative consistency agents

**The problem:** *"The greatest risk of LLM-generated narratives is hallucination — where characters forget established facts or contradict earlier events."* Without oversight, models contradict established character decisions, forget key plot details, or break logical continuity.

**The solution:** Narrative Consistency Agents — dedicated systems tasked exclusively with tracking active plot threads and preventing contradictions from ever reaching the player.

### The facts ledger

A JSON object as the master ledger — *"the ultimate source of truth that all narrative agents must query before generating new dialogue."*

```json
{
  "player_name": "Kael",
  "faction_allegiance": "Shadow Guild",
  "crimes_committed": ["stole_crown_jewels", "burned_market_district"],
  "allies": ["Mira the Fence", "Lord Voss"],
  "enemies": ["City Guard", "The Merchant Council"],
  "active_quests": ["retrieve_the_amulet"],
  "world_state": {
    "king_alive": false,
    "city_on_alert": true,
    "rumor_spread": true
  }
}
```

> *"The ledger pattern works for any genre — the keys are just different. A sci-fi game tracks ship_status and crew_loyalty; a mystery game tracks suspects_cleared and clues_found."*

> **⚠ IMPORTANT: PERSIST TO DISK AFTER EVERY UPDATE.**
> `json.dump(ledger, open('state/facts.json', 'w'), indent=2)`
> *"A ledger that isn't written to disk is lost on restart."*

**Remembering player actions:** `PLAYER ACTION → CONSISTENCY AGENT → LEDGER → NARRATIVE AGENTS`. The Consistency Agent automatically updates the ledger after every player message. *"This persistent feedback mechanism is what separates a genuine narrative engine from a simple chatbot — every consequence echoes forward through the entire story."*

## 3. Dynamic dialogue trees

**Real-time generation** — instead of writers manually predicting every possible conversational outcome, trees are generated and pruned in real time.
- **Context-aware dialogue** — the AI generates tailored options based on the immediate emotional state of the NPC and the stored facts in the ledger
- **No pre-written paths** — writers define personality archetypes and world rules; the agent handles every individual conversation turn

### The importance of pruning

Infinite dialogue generation quickly overwhelms the context window. A pruning script aggressively deletes old, irrelevant branches.

```python
def prune_dialogue_tree(tree, max_depth=3, max_branches=4):
    """Aggressively prune dialogue tree to fit LLM context window.
    Removes branches beyond max_depth and trims siblings
    to max_branches per node, prioritizing highest-weight edges."""
    if tree.depth > max_depth:
        tree.children = []
        return tree
    tree.children = sorted(
        tree.children,
        key=lambda x: x.relevance_score,
        reverse=True
    )[:max_branches]
    for child in tree.children:
        prune_dialogue_tree(child, max_depth, max_branches)
    return tree
```

> *"This pruning mechanism is critical engineering — without it, context window overflow degrades response quality and dramatically increases API costs."*
> **Note:** *"relevance_score must be set when branches are generated, not when they are pruned. Use a simple scoring function (recency + keyword match against the ledger) and assign the score at generation time."*

## 4. Exercise — Choose Your Own Adventure, AI edition

The class inside a live AI-driven CYOA: collective decisions through live polls; a DM agent synthesizes the poll result, queries the facts ledger, and generates the next chapter instantly. Demonstrates emergent narrative loops under unpredictable real-world input.

## 5. Assignment #8 — narrative engine prototype (OPTIONAL, due 25 August)

**Project type:** *"Standalone (does not need to connect to your capstone game, though you may adapt it later if you wish)."* Time estimate 4–6 hours.

**Objective:** build a virtual Dungeon Master agent that can roleplay a game world, dynamically tracking what a player **does** — not just what they say — and using that tracked state to generate a reactive, consistent narrative over multiple turns.

**Core requirements:** JSON facts ledger that updates automatically from player actions · reactive dialogue and narration that changes with ledger state (*"a player who betrayed an ally receives different dialogue than one who remained loyal"*) · turn consistency across at least **5** player turns.

**Deliverables:** agent code (Python + Claude API) · a short ReadMe (the world you built, what the ledger tracks, one moment where the agent surprised you).

**Rubric:** State Tracking /4.0 (*ledger state must be visible in output or logs*) · Reactive Dialogue /3.0 · Consistency /2.0 · ReadMe /1.0.

---

## Mr. Moonlight application

**Recommendation: SKIP THIS ASSIGNMENT.**
It is optional, costs 4–6 hours, and the brief itself says it is standalone and does not need to connect to the capstone. With Assignment #10's playable-link gate 12 days out and half the course grade riding on the game itself, this is the clearest cut available.

**The one idea worth stealing anyway — the persisted ledger is your save system**

The checkpoint issue in the Linear backlog describes, in different words, exactly this pattern: a single serializable state object capturing everything needed to restore the player to a moment. Mr. Moonlight's ledger keys:

```json
{
  "event_step": "S09_scott_treated",
  "objective": "Get Scott out of the mine",
  "player": {
    "position": [x, y, z], "rotation": [x, y, z],
    "health": 72.0, "stamina": 45.0, "stance": "standing",
    "boots_equipped": true, "flashlight_on": false,
    "equipped": "pistol"
  },
  "stats": { "fear": 0.2, "drunkenness": 0.0, "weed_high": 0.0, "morphine_high": 0.0 },
  "inventory": { "storage_cap": 10, "items": [...], "pistol_rounds": 14 },
  "world": {
    "sound_layers_active": ["mine"], "skybox": "night",
    "stretcher_carried": false
  },
  "enemies": [ { "id": "spotter_03", "state": "patrol", "waypoint": 2, "health": 100 } ]
}
```

Two things the slides get right that apply directly:
1. **Persist immediately.** In WebGL there is no filesystem — serialize to `PlayerPrefs` or IndexedDB via a JS interop shim. *"A ledger that isn't written to disk is lost on restart"* is doubly true in a browser tab.
2. **Don't track what you don't need.** The Linear notes already say ambient sound state need not be deterministic. Same instinct, independently arrived at.

**Not relevant**
- Real-time LLM dialogue generation. Mr. Moonlight's dialogue is authored and voice-acted; there is no runtime LLM and there must not be one in a sub-1 GB WebGL build.
- Dialogue tree pruning. There are no branching conversations in the demo — the event director plays authored sequences in order.
- The DM agent, the CYOA exercise, the reactive-narration architecture.
