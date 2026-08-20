# Assignment #7 — Style Guide Agent

> **MANDATORY · Due 20 August 2026, 11:59 PM ET — that is 9:59 PM Mexico City TODAY.**
> Estimated effort: **~1 hour, if Assignment #6 is done first.** It is the same pipeline.

---

## Read this first

**You are not building a second thing.** Assignment #6 built a Generator → Evaluator → Refiner loop with a style guide. Assignment #7 asks for a Generator → Evaluator → Refiner loop with a style guide.

The differences are only in **emphasis and packaging**:

| | #6 | #7 |
|---|---|---|
| Headline deliverable | The **pipeline** | The **style guide** (4.5 of 10 points) |
| Evaluator output | Any check | **Must be SCORE + REASON — binary pass/fail scores 0** |
| Demonstrations | "did it catch something?" | **Exactly 3 before/afters, each a DIFFERENT violation class** |
| Circuit breaker | Required | Not mentioned (keep it, it costs nothing) |
| Extra | Pre-Build Declaration | **One sentence** on where it fits your pipeline |

**If #6 is done, you are 45 minutes from submitting this.**

---

## What is graded

| Criterion | Pts | What satisfies it |
|---|---|---|
| **Game Specificity** | 3.0 | Rules derive from your docs. *"Rules are specific enough that a stranger reading them would understand what game they're for."* Generic rulesets score **0** |
| **Enforcement Accuracy** | 3.0 | The agent correctly finds violations and its rewrites genuinely make content more on-brand **for this game** |
| **Before/After Demonstration** | 2.0 | **Three** examples using real capstone content, **each a distinct violation class**. Placeholder content scores **0** |
| **Style Guide Depth** | 1.5 | **At least three distinct constraint types**, each specific to your game |
| **Pipeline Connection** | 0.5 | **One sentence** on where this runs in your actual workflow |

### The four "DO NOTs" — these are how people lose points

1. **DO NOT invent a new universe.** Pull from your existing docs.
2. **DO NOT use generic content.** *"If a stranger can't tell exactly what game the rules are for, you will get a 0 for Specificity."*
3. **DO NOT use binary pass/fail grading.** The Evaluator **must output a SCORE and a REASON.**
4. **DO NOT intervene in the loop.** The agent identifies and fixes violations independently.

---

## Why Mr. Moonlight is unusually well placed for this

Most students in this class are reverse-engineering a style guide out of a GDD written six weeks ago. **You have an actual, dedicated style document** — `Style.pdf` — plus a character profile document with per-character voice notes and sample lines, plus a pitch that states the narrative rule explicitly.

The rubric's bar is *"a stranger reading them would understand what game they're for."* Rules that say *"the aesthetic is the 90s Orthodox punk zine Death to the World"* and *"Tracey never uses game-speak because it is 1979"* clear that bar without effort.

**Cite the documents by name in the submission.** The graders are checking traceability, and you have more of it than the assignment expects.

---

## The style guide — the 4.5-point deliverable

This is `style_guide.md` from Assignment #6. Present it as the headline here. Three constraint types, all traceable:

### Constraint type 1 — VOICE AND TONE
*Traceable to: `MrMoonlight Character Profiles.pdf` (Tracey), `MrMoonlight Pitch Document.pdf`*

Tracey Gallagher, 25, Irish-American, college dropout, addict, library clerk. **Grumpy, profane, terse.** Documented sample lines: *"Yes… this is fun…"* · *"What the fuck did you even call me for?"* · *"If you really are my sister, then pass me that bottle."*

Rules:
- Profanity is **habitual, not emphatic**
- **Never cheerful.** No enthusiastic exclamation marks
- **Never tutorializes** — the Pitch: *"show, don't tell... punctual, sparse dialogue that hints at much deeper character development"*
- **Never states her feelings.** Sarcasm is armour; she deflects
- Under stress she gets **shorter**, not more eloquent

### Constraint type 2 — VOCABULARY AND LORE
*Traceable to: `MrMoonlight Pitch Document.pdf`, `Style.pdf`, `MrMoonlight screenplay demo.pdf`*

- **1979.** No anachronism, no modern slang, no post-1979 technology
- **No game-speak.** Not "inventory", "health pack", "HP", "quest", "checkpoint". She says *bandages*, *bullets*, *my stuff*
- **Fixed proper nouns:** the island is **Aanniarvik**; enemies are **Zealots**, **Spotters**, the **Furman** — though Tracey does not know those names and says *"them"* or *"those things"*
- **Fixed nicknames:** Holly = *Miss Perfect* · Rylee = *Lady Jester* · Scott = *Rocketman* · Robert = *Chief* · Vernon = *the boogeyman* · Tracey = *Trey* / *Punk* / *Princess*
- **Religion is present, never explained.** *"Oh God"* after her first kill is refusal, not devotion

### Constraint type 3 — FORMAT AND LENGTH
*Traceable to: `MrMoonlight Pitch Document.pdf` — "plain white text, akin to the early Silent Hill games"*

- **Maximum 90 characters.** One subtitle band
- **One thought per line.** No compound sentences
- **No stage directions in the text** — `*sighs*`, `(quietly)` live in a separate column
- **No speaker name in the text**
- **No trailing exclamation marks** outside genuine shouting

> Three distinct types, each with named source documents. That is Style Guide Depth (1.5) and Game Specificity (3.0) covered.

---

## The Evaluator — SCORE + REASON, non-negotiable

This is worth 3 points and it is the constraint most commonly failed.

```
You are the style evaluator for Mr. Moonlight, a 1979 Alaskan horror FPS.
The player character is Tracey: 25, a college dropout and addict — grumpy,
profane, terse. Never cheerful. Never explains herself. Never tutorializes.

Style guide:
<paste style_guide.md>

Review the following line. Grade it 1-10 against the three constraint types.

Output EXACTLY this and nothing else:
SCORE: [X/10]
REASON: [which constraint type was violated and precisely how]

LINE: {candidate}
```

**The Refiner must consume the REASON, not the original prompt.** That is the point of the loop — the rubric says *"the Refiner uses the reason to target the revision."*

```
Rewrite this Mr. Moonlight line so it scores 10/10.

Style guide: <paste>
Original: {candidate}
Evaluator's REASON: {reason}

Change only what the REASON identifies. Keep the meaning and the trigger context.
Output ONLY the rewritten line.
```

---

## The three before/after demonstrations

**Three examples. Three DIFFERENT violation classes.** Three tone failures scores badly.

Feed the generator prompts engineered to produce wrong content — the brief tells you to do exactly this: *"ask it to write an overly cheerful character if your game is grimdark."*

### Example 1 — TONE violation
**Generator prompt:** *"Write an encouraging, helpful line for Tracey when the player finds a locked door."*

| | |
|---|---|
| **Before** | `"I should find a way to open this door — perhaps the key is nearby!"` |
| **Evaluator** | `SCORE: 2/10` · `REASON: Violates VOICE AND TONE. The line is helpful and cheerful, both of which Tracey never is. It also tutorializes — it tells the player the solution ("the key is nearby"), which the Pitch's "show, don't tell" rule forbids. The exclamation mark is enthusiastic, not shouted.` |
| **After** | `"Locked. Great."` |

### Example 2 — VOCABULARY / LORE violation
**Generator prompt:** *"Write a line where Tracey checks what supplies she is carrying."*

| | |
|---|---|
| **Before** | `"Let me check my inventory for a health pack."` |
| **Evaluator** | `SCORE: 1/10` · `REASON: Violates VOCABULARY AND LORE twice. "Inventory" is game-speak — Tracey is a person in 1979, not a player. "Health pack" does not exist in this game or in 1979; the healing item is bandages. Also mildly violates TONE: the phrasing is neutral and procedural where Tracey is impatient.` |
| **After** | `"Bandages. Somewhere in here."` |

### Example 3 — FORMAT / LENGTH violation
**Generator prompt:** *"Write a detailed, descriptive line for Tracey reacting to the mine entrance."*

| | |
|---|---|
| **Before** | `"*shivers* The entrance to the mine looms before me, dark and forbidding, and I can see a torch still burning beside a totem with a skull mounted on it, which makes me wonder who lit it."` |
| **Evaluator** | `SCORE: 2/10` · `REASON: Violates FORMAT AND LENGTH — 178 characters against a 90-character limit, contains the stage direction "*shivers*" which belongs in the direction column, and packs three thoughts into one line. Also violates TONE: it is descriptive and articulate where Tracey is terse under stress.` |
| **After** | `"Somebody's torch. Still burning."` |

> **Run these for real and paste the actual output.** The predictions above are what should happen; the rubric wants what did happen.

---

## The Pipeline Connection sentence — 0.5 points, one sentence

The rubric explicitly refuses points for hypothetical future pipelines. Use this:

> *"This Style Guide Agent runs immediately after thought-line, system-message and objective-text generation, before those lines are written into the spreadsheet that the Mr. Moonlight Event Director reads at build time (Linear issues MRM-11, MRM-13 and MRM-14) — and it also validates the hand-written screenplay dialogue against the same guide."*

That names the game, the real workflow position, and real issue IDs. It is not hypothetical, which is what the criterion is checking.

---

## Submission checklist

- [ ] `style_guide.md` — three constraint types, source documents named
- [ ] Evaluator agent returning **SCORE + REASON** — never binary
- [ ] Refiner agent consuming the **REASON**
- [ ] **Three** before/after examples, **three different violation classes**, real output pasted
- [ ] The one-sentence pipeline connection
- [ ] ReadMe naming **Mr. Moonlight**, Tracey, and the specific content type
- [ ] The loop ran **without you intervening**
- [ ] Token cost of the run logged for Assignment #10

## Do NOT

- Do not write a new style guide from scratch. Use `Style.pdf` and the character profiles.
- Do not use pass/fail anywhere. **Score and reason, always.**
- Do not submit three examples of the same violation class.
- Do not hand-edit the "after" text. The agent must fix it independently — that is 3 points.
- Do not invent characters, factions or locations. Everything exists already.

## Model to run this with

**Sonnet** for the evaluator — it needs to actually reason about voice. **Haiku** for the generator and refiner. Mention that split in the ReadMe; it is a legitimate cost-conscious architecture choice and it is reusable evidence for Assignment #10.
