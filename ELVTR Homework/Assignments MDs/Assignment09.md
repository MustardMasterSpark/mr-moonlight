# Assignment #9 — Adversarial QA Agent

> **OPTIONAL · Due 27 August 11:59 PM ET · Time estimate 4–6 hours**
> ## Recommendation: **SKIP THIS — and the course brief agrees.**

---

## The brief tells you to skip it

Verbatim from the assignment:

> ⚠ *"If your game isn't testable by an adversarial agent yet, you should spend this time working on your capstone project instead."*

On **27 August** you will be **4 days from the itch.io release**. Your game will be mid-integration, not stable enough to instrument. This is the clearest cut on the whole list, and the course itself sanctions it.

**Skip it. Take the 4–6 hours back.**

---

## What to do instead — one hour, not six

The genuinely transferable idea from Class 11 is *"define failure parameters explicitly."* Do that manually. It costs an hour and it catches the same class of bug the agent would.

**Add this checklist to MRM-62 and run it before the Sept 1 build.**

### Softlocks and event-director deadlocks
*(the most likely way a stranger's playthrough ends badly)*

- [ ] Player walks away from the telescope mid-sequence — does the director wait forever?
- [ ] Player dies during a cutscene — is damage genuinely disabled, per MRM-15?
- [ ] Player leaves the cabin before Vernon's dialogue chain finishes — is the pistol still granted?
- [ ] Player drops the stretcher and walks to the chapel without Scott — does the Furman still trigger?
- [ ] Player kills the Furman *before* retrieving the radio — does the door sequence still fire?
- [ ] Player picks up the boots before the telescope sequence — does anything break?
- [ ] Player reloads a checkpoint mid-cutscene

### Geometry and movement
- [ ] Stretcher wedged between two trees, or in the mine tunnel — can it be freed?
- [ ] Player out of bounds at the island edge
- [ ] Falling through the terrain seam at the mine entrance or exit
- [ ] Wolves or Spotters stuck or sliding on steep slopes
- [ ] Camera clipping through the cabin walls during the long cutscene
- [ ] Player jumps onto geometry that lets them skip a gated area

### State and stat exploits
- [ ] Stamina lock in Scene 1 — does it release if the player dies before drinking?
- [ ] Drug relapse multiplier — can stacking blunts push a stat above 1.0?
- [ ] Boots equip animation — is the player genuinely vulnerable, and can it be cancelled?
- [ ] Storage cap — can the player exceed 4 item types before the backpack?
- [ ] Turret — does exiting and re-entering reset the 30-round belt?
- [ ] Shotgun pickup — does a second shotgun correctly give +2 shells rather than a second weapon?
- [ ] Can fear or a drug stat go negative or exceed 1?

### WebGL-specific *(the class never covers these, and they will bite you)*
- [ ] Alt-tab or lose window focus during a cutscene
- [ ] Toggle browser fullscreen mid-scene
- [ ] Refresh the page and reload a checkpoint
- [ ] Gamepad connected mid-session
- [ ] First load on a cold cache

**Anything this finds becomes a Linear issue.** That is the same output the agent would have produced, at a sixth of the cost, and it directly protects the Assignment #10 gate.

---

## The one idea worth remembering for Assignment #10

Class 11's balance-testing demo has a genuinely smart cost architecture worth citing later:

- **Layer 1:** thousands of **rule-based bots**, no LLM calls, generating raw data — **free**
- **Layer 2:** **one** LLM call reading the aggregate and writing a human-readable report

That is a legitimate, citable **cost-reduction pattern** for Assignment #10's before/after requirement, even if you never build the QA agent. The same principle already applies in your GER pipeline: deterministic checks first, API calls only when code cannot decide.

---

## If you decide to do it anyway

You would need: a testing loop running inside the game cycling movement, interaction and boundary-probing; bug and edge-case logging; and a structured **JSON or CSV** report with `location`, `error_type` and `game_context`.

**Rubric:** Findings 4.0 (*at least one real bug; names the specific mechanic*) · Agent Logic 3.0 (*actively tries to break the game*) · Structured Report 2.0 · ReadMe 1.0.

**The cheapest honest route:** a Unity script driving the player controller with random input plus deliberate boundary-probing, logging position, current event-director step and any exception to CSV. Run it for 20 minutes on the M1 build. It will find the stretcher wedging problem, because that problem is real.

**But the recommendation stands: skip it, and run the manual checklist instead.**

## Model

**Haiku**, if you do it at all.
