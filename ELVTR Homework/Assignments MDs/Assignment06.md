# Assignment #6 — Build a GER Pipeline

> **MANDATORY · Due 18 August 11:59 PM ET · OVERDUE — submit as soon as it works.**
> Estimated effort: **~2 hours**. This is the one that also gives you #7 almost free.

---

## What is graded

| Criterion | Pts | What satisfies it |
|---|---|---|
| **Working Pipeline** | 3.0 | The loop runs: Generator produces, Evaluator checks against a rule, Refiner fixes, **Circuit Breaker escalates when it can't self-correct** |
| **Evaluator Quality** | 3.0 | Enforces **a specific rule from your design docs** — not a generic validity check. The rule must be findable in the docs |
| **Game Connection** | 2.0 | ReadMe names the game, the content type, and what the evaluator caught |
| **ReadMe** | 2.0 | Pre-Build Declaration included; says what it generates, what rule it enforces, and whether it caught something useful |

**The Circuit Breaker is the part most students skip.** It is inside a 3-point criterion. Build it.

---

## The Pre-Build Declaration

Submit this **before writing code**. Under 150 words, three answers. Here it is, ready to paste — edit the voice to sound like you, but the substance is correct:

> **1. What content type does your game currently generate manually, inconsistently, or not at all?**
> Mr. Moonlight has a "thought system" — short unvoiced lines Tracey thinks when the player presses the thought button, keyed to location, action or state. None are written. The same gap covers system messages and objective text.
>
> **2. What specific rule from your design docs must every piece satisfy?**
> Tracey's voice, as fixed in `MrMoonlight Character Profiles.pdf` and the Pitch: grumpy, profane, terse, never explanatory. The Pitch states the narrative rule directly — *"show, don't tell... punctual, sparse dialogue that hints at much deeper character development."* A line must never tutorialize, never be cheerful, and never explain a puzzle the player is meant to solve.
>
> **3. What does a failure look like, concretely?**
> `"I should find a way to open this door — perhaps the key is nearby!"` Helpful, cheerful, and it solves the puzzle for the player. Tracey would say: `"Locked. Great."`

**That is 148 words.** Submit it, then build.

---

## What to build

```
moonlight-ger/
├── style_guide.md        # The rules. Human-readable. THIS IS ALSO ASSIGNMENT #7's DELIVERABLE.
├── rules.py              # Deterministic checks (length, vocabulary, format)
├── generator.py          # Produces candidate lines
├── evaluator.py          # Returns SCORE (1-10) + REASON
├── refiner.py            # Rewrites using the REASON
├── pipeline.py           # The GER loop + circuit breaker
├── run.py                # CLI entry point
├── runs/
│   ├── costs.csv         # token + cost log — REQUIRED FOR ASSIGNMENT #10
│   └── transcript_*.md   # before / score / reason / after, per line
└── README.md
```

### The loop

```
for each requested line:
    candidate = generate(context)
    for attempt in 1..3:
        deterministic = rules.check(candidate)      # cheap, no API call
        if deterministic.failed:
            candidate = refine(candidate, deterministic.reasons)
            continue
        score, reason = evaluate(candidate)          # API call
        if score >= THRESHOLD:                       # THRESHOLD = 8
            accept(candidate); break
        candidate = refine(candidate, reason)        # API call
    else:
        circuit_breaker(candidate, all_reasons)      # escalate to human
```

**Two things to note in that loop, because they are worth points:**

1. **Deterministic checks run first and cost nothing.** Class 8 says it explicitly: *"If code can verify it, use code."* Length, banned vocabulary and format are regex and string length — do not spend an API call on them. This is also a legitimate **cost optimization you can cite in Assignment #10**.
2. **The circuit breaker is the `else` on the `for`.** After 3 failed attempts it stops, writes the line, every score and every reason to `runs/escalations.md`, and says plainly: *"could not bring this line on-voice after 3 attempts; needs a human."* It does **not** silently accept the best attempt.

---

## The style guide — three constraint types

Write these into `style_guide.md`. **All three trace to real documents**, which is what both rubrics check.

### 1. VOICE AND TONE
*Source: `MrMoonlight Character Profiles.pdf` (Tracey), `MrMoonlight Pitch Document.pdf` (narrative style)*

- Tracey is **grumpy, profane, terse**. Her sample lines: *"Yes… this is fun…"* · *"What the fuck did you even call me for?"*
- **Profanity is habitual, not decorative.** It appears because that is how she talks, not for emphasis.
- **Never cheerful.** Never an exclamation mark used for enthusiasm.
- **Never tutorializing.** She does not tell the player what to do. The Pitch: *"show, don't tell"*, *"punctual, sparse dialogue"*, *"without over-exposition."*
- **Never explains her own feelings.** Sarcasm is her armour — she deflects rather than states.
- Under pressure she gets **shorter**, not more articulate. In combat she is barely coherent.

### 2. VOCABULARY AND LORE
*Source: `MrMoonlight Pitch Document.pdf`, `Style.pdf`, the screenplay*

- **Setting is 1979.** No anachronisms — no "okay boomer", no "download", no "app", nothing post-1979.
- **No game-speak.** She does not say "inventory", "health pack", "HP", "quest", "objective", "checkpoint", "XP". She says *bandages*, *bullets*, *my stuff*.
- **Proper nouns are fixed:** the island is **Aanniarvik**. Enemies are **Zealots**, **Spotters**, the **Furman** — never "cultists" generically in her voice; she does not know their names, so she says *"them"*, *"those things"*.
- **Nicknames are fixed:** Holly = *Miss Perfect*. Rylee = *Lady Jester*. Scott = *Rocketman*. Robert = *Chief*. Vernon = *the boogeyman* (Holly's word). Tracey's own = *Trey*, *Punk*, *Princess*.
- **Religion is present but never explained.** *"Oh God"* after her first kill is refusal, not devotion.

### 3. FORMAT AND LENGTH
*Source: `MrMoonlight Pitch Document.pdf` (Silent Hill subtitle style), the dialogue system spec*

- **Maximum 90 characters per line.** It must fit one subtitle band in plain white text.
- **One thought per line.** No compound sentences joined by "and then".
- **No stage directions in the text.** `*sighs*`, `(quietly)` and similar go in the `direction` column, never in `text_en`.
- **No speaker name in the text.** The system knows who is talking.
- **No trailing exclamation marks** unless she is genuinely shouting, which is rare outside combat.

---

## The prompts

**Evaluator** — the rubric for #7 requires SCORE + REASON, so write it that way from the start:

```
You are the style evaluator for Mr. Moonlight, a 1979 Alaskan horror FPS.
The player character is Tracey: a 25-year-old college dropout, addict,
grumpy, profane, terse. Never cheerful. Never explains. Never tutorializes.

Here is the style guide:
<paste style_guide.md>

Review the following line intended to be spoken/thought by Tracey.
Grade it 1-10 against the three constraint types above.

Output EXACTLY this format and nothing else:
SCORE: [X/10]
REASON: [which constraint types were violated and precisely how]

LINE: {candidate}
```

**Refiner:**

```
You are rewriting a line for Mr. Moonlight so it matches Tracey's voice.

Style guide:
<paste style_guide.md>

Original line: {candidate}
Evaluator's reason for the low score: {reason}

Rewrite the line so it scores 10/10. Change only what the reason identifies.
Keep the same underlying meaning and the same trigger context.
Output ONLY the rewritten line. No explanation, no quotes, no preamble.
```

---

## Three test cases — one per violation class

Feed the generator prompts **designed to produce wrong content**, so the loop has something to catch. These double as Assignment #7's before/after demos, so use them for both.

| # | Class | Bad prompt | Expected before | Expected after |
|---|---|---|---|---|
| 1 | **Tone** | *"Write an encouraging, helpful line for Tracey finding a locked door."* | *"I should find a way to open this door — perhaps the key is nearby!"* | *"Locked. Great."* |
| 2 | **Vocabulary / lore** | *"Write a line where Tracey checks her supplies."* | *"Let me check my inventory for a health pack."* | *"Bandages. Somewhere in here."* |
| 3 | **Format / length** | *"Write a detailed line describing Tracey's reaction to the mine entrance."* | A 3-sentence, 180-character line with `*shivers*` in it | *"Somebody's torch. Still burning."* |

**Save the real output, not these predictions.** Both rubrics reward showing the actual transcript.

---

## The ReadMe

Cover, in this order:

1. **The Pre-Build Declaration** (above, verbatim)
2. **The game:** Mr. Moonlight — a first-person horror shooter set on Aanniarvik Island, Alaska, 1979. Player character Tracey. Releasing on itch.io.
3. **What the pipeline generates:** Tracey's unvoiced thought lines, plus system messages and objective text — the text layer the event director triggers.
4. **What rule the evaluator enforces:** Tracey's voice per the character profile and the Pitch's "show, don't tell" rule, across three constraint types.
5. **What it caught that you would have missed** — *this is the question the rubric actually asks.* Answer it honestly from a real run.
6. **Where it fits:** *"This pipeline runs before the event script is authored, generating and vetting the unvoiced text layer that the Event Director triggers (Linear MRM-11, MRM-13, MRM-14)."*

---

## The honesty paragraph — include it

Add this to the ReadMe. It costs nothing and it is what the Class 9 rubric is really testing:

> **Where AI generation is safe in this project, and where it is not.** The pipeline generates the *unvoiced* text layer — thoughts, system messages, objectives, UI strings. The **narrative dialogue is not generated**: all ~250 demo lines are hand-written and are being recorded by four voice actors, with per-line performance direction. Generating those would be both worse and dishonest. The pipeline does, however, **validate** the hand-written lines against the same style guide, which is how the guide itself was tested.

That paragraph is a direct hit on Assignment #9's *"list three assets where AI generation is safe; name one where it would break the experience"* exercise, and it makes your Game Connection score unambiguous.

---

## Do NOT

- Do not invent lore. Everything comes from the four design docs.
- Do not generate dialogue for the screenplay. It is written and cast.
- Do not build a UI. A CLI is fine — no rubric line mentions an interface.
- Do not use a framework. Raw API calls, per Class 7's own advice.
- Do not skip the circuit breaker.
- Do not disable the cost logging. Assignment #10 needs it.

## Model to run this with

**Sonnet.** The generator and refiner can be **Haiku** — that is a real cost saving and a citable optimization for #10.
