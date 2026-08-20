# Class 10 — Emergent Chronicles: Narrative Engines · Executive Summary

**Sets Assignment #8 — OPTIONAL. Recommend skipping.**

**One line:** how to make an AI dungeon master remember what the player did, and stay consistent about it.

## The shift from Class 9

> *"S09 was about consistency with your game's identity. S10 is about consistency with the player's history."*
> Class 9's agent asks *"does this output match my game?"* Class 10's asks *"does this output know what the player just did?"*

## The facts ledger

A JSON object as the master source of truth that every narrative agent queries before generating:

```json
{
  "player_name": "Kael",
  "faction_allegiance": "Shadow Guild",
  "crimes_committed": ["stole_crown_jewels", "burned_market_district"],
  "allies": ["Mira the Fence"],
  "enemies": ["City Guard"],
  "active_quests": ["retrieve_the_amulet"],
  "world_state": { "king_alive": false, "city_on_alert": true }
}
```

> **⚠ Persist to disk after every update.** *"A ledger that isn't written to disk is lost on restart."*

The ledger pattern works for any genre — only the keys change.

## Dynamic dialogue trees and pruning

Rather than writers predicting every conversational outcome, options are generated and pruned in real time based on NPC emotional state and the ledger. **Pruning is mandatory** — infinite branching overwhelms the context window and inflates cost. Prune by depth and by branch count, keeping highest-relevance children.

A good footnote from the slides: *relevance_score must be set when branches are generated, not when they are pruned.*

## Assignment #8 (OPTIONAL, 25 August, 4–6 hours)

A virtual DM agent in Python + Claude API, maintaining a JSON facts ledger, generating reactive dialogue, consistent over 5+ turns. **Explicitly standalone — it does not need to connect to your capstone game.**

## Takeaway for Mr. Moonlight

**Skip it.** It is optional, it is 4–6 hours, and it is standalone by design — the course itself says it doesn't have to connect to your game. Building a text-based dungeon master has close to zero carry-over into a linear, voice-acted, hand-authored FPS demo that ships in 12 days.

The **one** idea worth stealing without doing the assignment: the **persisted JSON ledger** is exactly the right shape for your **checkpoint/save system**. Player position, health, stamina, inventory, equipped weapon, boots on/off, current event-director step, current objective, live enemies and their states — one serializable object, written to disk (or PlayerPrefs/IndexedDB in WebGL) at each checkpoint trigger. Same pattern, real use.
