# Assignment #8 — Narrative Engine Prototype

> **OPTIONAL · Due 25 August 11:59 PM ET · Time estimate 4–6 hours**
> ## Recommendation: **SKIP THIS.**

---

## Why skip

Three reasons, and the third is the strongest:

1. **It is optional.** It does not affect your certificate.
2. **The brief says it is standalone:** *"Does not need to connect to your capstone game, though you may adapt it later if you wish."* So it earns you nothing on the 50% that is the game.
3. **On 25 August you will be 7 days from the itch.io release and 14 days into a 19-day sprint.** Four to six hours is roughly one full evening. That evening is worth more spent on **MRM-62 (the event script)** — the issue that turns your systems into a playable demo.

Building a text-based dungeon master has close to zero carry-over into a linear, voice-acted, hand-authored first-person shooter. The techniques do not transfer: your dialogue is written and cast, your narrative is fixed, and you must not put an LLM in a sub-1 GB WebGL runtime.

**Skip it. Spend the evening on the game.**

---

## The one idea worth stealing without doing the assignment

The **JSON facts ledger** is exactly the right shape for your **checkpoint system** (MRM-45). You are building this anyway — recognise that it is the same pattern and take the two good rules that come with it.

The class version:

```json
{
  "player_name": "Kael",
  "crimes_committed": ["stole_crown_jewels"],
  "world_state": { "king_alive": false, "city_on_alert": true }
}
```

Your version, which MRM-45 already specifies:

```json
{
  "event_step": "S09_scott_treated",
  "objective": "Get Scott out of the mine",
  "player": {
    "position": [x, y, z], "rotation": [x, y, z],
    "health": 72.0, "stamina": 45.0, "stance": "standing",
    "boots_equipped": true, "flashlight_on": false, "equipped": "pistol"
  },
  "stats": { "fear": 0.2, "drunkenness": 0.0, "weed_high": 0.0, "morphine_high": 0.0 },
  "inventory": { "storage_cap": 10, "items": ["bandages", "blunt"], "pistol_rounds": 14 },
  "world": { "sound_layers_active": ["mine"], "skybox": "night", "stretcher_carried": false },
  "enemies": [ { "id": "spotter_03", "state": "patrol", "waypoint": 2, "health": 100 } ]
}
```

**The two rules worth carrying over:**

1. **Persist immediately after every update.** The class slide is blunt: *"A ledger that isn't written to disk is lost on restart."* In WebGL there is no filesystem — serialize to `PlayerPrefs` or IndexedDB, and do it on write, not on quit. **Browser tabs get refreshed.**
2. **Do not over-serialize.** Your own issue list already says ambient sound state need not be tracked deterministically. The class arrives at the same instinct from the other direction. Track what restores the *gameplay* state, nothing more.

---

## If you decide to do it anyway

You would need:

- A **JSON facts ledger** updating from player *actions*, not just their words
- **Reactive dialogue** that changes with ledger state — *"a player who betrayed an ally gets different dialogue than one who didn't"*
- **Consistency over 5+ turns** with no contradictions
- Python + the Claude API, plus a ReadMe describing the world, what the ledger tracks, and one moment the agent surprised you

**Rubric:** State Tracking 4.0 (*ledger state must be visible in output or logs*) · Reactive Dialogue 3.0 · Consistency 2.0 · ReadMe 1.0.

**The cheapest honest route,** if you want the marks: build the DM around **Aanniarvik on a different day** — Day 3, say, which is outside the demo. Tracey, Vernon and the cult already exist, the ledger tracks `days_survived`, `friends_rescued`, `shelter_reached_before_3am`, `drug_stat`. That reuses your lore, keeps it standalone as the brief allows, and gives you a plausible answer to the "one moment it surprised you" question.

**But the honest advice remains: skip it and go build the event script.**

## Model

**Haiku**, if you do it at all. It is a scripted loop with a JSON file.
