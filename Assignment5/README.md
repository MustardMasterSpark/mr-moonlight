# Assignment 5 — GDD-to-Code Gap Agent

Mr. Moonlight uses the Advanced Horror FPS Kit as its base. This assignment finds one place
where the kit's stock behavior falls short of `Docs/GDD/GDD v2.pdf` section 7 (Game Mechanics),
explains why it's the right gap to close first, closes it in the Unity project, and ships a
standalone tool that can re-derive all of that on its own, without an LLM in the loop.

## What's here

| Path | What it is |
|---|---|
| `agent.py` | The standalone pipeline. Stdlib-only, re-runnable, no network calls. |
| `README.md` | This file. |

The Unity-side code change lives outside this folder, in the main project:

| Path | Change |
|---|---|
| `Assets/AdvancedMobileHorror/Scripts/FirstPersonController.cs` | Added `public void DrainStamina(float amount)` — the only external hook for spending stamina outside of sprinting. |
| `Assets/AdvancedMobileHorror/Scripts/BaseballScript.cs` | Added a tunable `public float StaminaCostPerSwing = 15f` field, and a call to `DrainStamina(StaminaCostPerSwing)` in `Hit()`, so every melee swing attempt costs stamina. |

`Docs/GDD/GDD v2.txt` is a plain-text export of the GDD PDF, generated once so the tool has a
reliable, stdlib-readable source of truth (see "Why a .txt export" below).

## The gap

**Does swinging the melee weapon drain the player's stamina?**

GDD v0.2 §7.1 (Swing verb): *"Heavy, slow pickaxe blows that drain stamina."*
GDD v0.2 §7.2 (Melee system row): *"Slow pickaxe swings that cost stamina and stagger a
Furman."* Holds: *"Damage, swing time, stamina cost, stun."*

In the stock kit, `Stamina` (a private field on `FirstPersonController`) was drained only by
sprinting. `BaseballScript`'s melee path (`Hit()` → `ReleaseHit()` → `CheckTheTarget()` →
`DemonScript.GetDamageByPistolOrBaseBallStick()`) never referenced it. **Gap: open.**

### Why this gap first

Both endpoints already existed and worked — a live `Stamina` value on the player, and a working
melee swing/hit path — so this was a one-file wiring fix, not a new subsystem: small blast
radius, no new dependencies. It's testable in Play mode in seconds (swing, watch the bar), with
zero dependency on unbuilt content (pickaxe model, Furman, mine scene). And it's the concrete
mechanism behind the GDD's stated design pillar, "combat that costs something."

### A wrinkle found during manual testing

The first implementation drained stamina only when the swing *connected* with an enemy (hooked
into `CheckTheTarget()`, next to the damage call) — a literal reading of "hook the melee hit
event." Playtesting showed this reads as broken: a missed swing (very plausible, since
`CheckTheTarget()` is a 3-unit raycast fired 0.5s after the swing) costs nothing, which doesn't
match "every blow drains stamina" or "swings that cost stamina" (the swing itself, not the
connect). The fix now drains stamina unconditionally in `Hit()`, on every swing attempt.

### The placeholder

Nothing in the GDD specifies a stamina *amount* or a drain *curve* for melee — `StaminaCostPerSwing = 15f`
is a clearly-commented placeholder, left for a design pass rather than invented as fact.

## Running the tool

```bash
python Assignment5/agent.py                # human-readable report
python Assignment5/agent.py --json          # machine-readable report
python Assignment5/agent.py --gdd "Docs/GDD/GDD v2.txt" --scripts Assets/AdvancedMobileHorror/Scripts
```

No dependencies beyond a Python 3 standard library install (`argparse`, `re`, `json`, `zlib`,
`pathlib`). It performs, independently of any LLM:

1. **Read the GDD, extract section 7.** Loads `Docs/GDD/GDD v2.txt`, locates the "07 GAME
   MECHANICS" header (case- and line-anchored so it can't be confused with the mixed-case INDEX
   entry earlier in the doc), and slices out the Melee and Stamina rows from the systems table.
2. **Scan the imported kit scripts.** Reads every `.cs` file in
   `Assets/AdvancedMobileHorror/Scripts`, and checks each of the 20 GDD §7.2 systems against a
   hand-verified signal map (a script file that is clearly that system's home, or a keyword
   fallback) to report which systems have code and which don't.
3. **Detect the gap by reading live code, not a cached answer.** Brace-matches the bodies of
   `BaseballScript.Hit()` and `BaseballScript.CheckTheTarget()` and checks each for a call to a
   stamina-draining method. This means the tool's verdict tracks the actual state of the repo:
   before the fix it reports `GAP OPEN`; after it, `GAP CLOSED`, with which of the two trigger
   points fired (every swing vs. only a landed hit). Verified by making a scratch copy of the
   scripts, stripping the fix back out, and confirming the tool correctly flipped back to open.
4. **Prioritize.** Prints the "why this gap first" rationale, plus whatever other GDD §7.2
   systems still have no code (for context on what's next).

### Why a `.txt` export instead of parsing the PDF directly

The assignment brief allows asking for a text export "if needed" — this repo already has one at
`Docs/GDD/GDD v2.txt`, generated once from the PDF. `agent.py` still ships a best-effort,
stdlib-only PDF fallback (`extract_pdf_text_best_effort`, inflating FlateDecode streams with
`zlib` and pulling `Tj`/`TJ` text-showing operators) for resilience if the `.txt` file is ever
deleted, but it's explicitly documented as a fallback: it can't handle compressed object streams
(`/ObjStm`) or ToUnicode CMaps, which many PDF exporters use, so it isn't a substitute for a real
PDF library. There is no third-party PDF dependency anywhere in this tool, by design (stdlib-only
requirement).

## Sample output

```
[3] GAP: does swinging the melee weapon drain player stamina?

  Status: GAP CLOSED
  Trigger: every swing attempt (Hit())
  BaseballScript.cs drains player stamina on every swing attempt (Hit()). Tunable cost field
  present: yes. FirstPersonController exposes a public stamina-drain method: yes.

[4] PRIORITY

  The melee-hit -> stamina-drain gap is currently closed in code. Remaining GDD systems with no
  code detected: Fear, Map and compass, Substances, Bear trap, Stretcher escort, Objective
  tracker, Subtitles, Time of day, SLDD runner. Re-run this tool after further changes to see
  whether that list has moved.
```
