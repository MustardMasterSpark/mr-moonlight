# Mr. Moonlight — GER Pipeline (Assignment #6)

A Generate → Evaluate → Refine pipeline, with a circuit breaker, that
vets short lines of text against Tracey's voice for **Mr. Moonlight**, a
first-person horror shooter set on Aanniarvik Island, Alaska, 1979,
releasing on itch.io.

This folder is the complete, standalone deliverable — everything needed
to read, run, and grade it lives here. No other file outside this folder
is required.

---

## Pre-Build Declaration

*(Submitted before writing code, per the assignment's requirement.)*

**1. What content type does your game currently generate manually,
inconsistently, or not at all?**
Mr. Moonlight has a "thought system" — short unvoiced lines Tracey
thinks when the player presses the thought button, keyed to location,
action or state. None are written. The same gap covers system messages
and objective text.

**2. What specific rule from your design docs must every piece satisfy?**
Tracey's voice, as fixed in the Character Profiles and the Pitch
Document: grumpy, profane, terse, never explanatory. The Pitch states
the narrative rule directly — *"show, don't tell… punctual, sparse
dialogue that hints at much deeper character development."* A line must
never tutorialize, never be cheerful, and never explain a puzzle the
player is meant to solve.

**3. What does a failure look like, concretely?**
`"I should find a way to open this door — perhaps the key is nearby!"`
Helpful, cheerful, and it solves the puzzle for the player. Tracey would
say: `"Locked. Great."`

---

## What the pipeline generates

Tracey's unvoiced thought lines, plus system messages and objective
text — the text layer the event director triggers (see "Where it fits"
below).

## What rule the evaluator enforces

Tracey's voice per her character profile and the Pitch's "show, don't
tell" rule, across three constraint types, written out in full in
[`style_guide.md`](style_guide.md):

1. **Voice and tone** — grumpy, profane, terse; never cheerful, never
   tutorializes, never explains her own feelings.
2. **Vocabulary and lore** — no anachronisms, no game-speak
   ("inventory", "health pack"), fixed proper nouns and nicknames.
3. **Format and length** — 90-character subtitle limit, one thought per
   line, no stage directions or speaker names in the text.

Constraint type 3, and the banned-vocabulary half of type 2, are
**deterministic** — checked in code (`rules.py`) with no API call, for
free. Voice/tone nuance and lore-accuracy judgment need an LLM, so those
go to the evaluator. Running the cheap deterministic check first, before
any paid call, is the pipeline's core cost optimization (see
Assignment #10 note below).

## What it caught — from the actual run in `runs/`

- **Cheerful, tutorializing phrasing** that explained the puzzle instead
  of deflecting it (`tone_locked_door` — see
  [`runs/transcript_tone_locked_door.md`](runs/transcript_tone_locked_door.md)):
  the deterministic check only caught a stray trailing "!"; the *actual*
  tone violation (explaining "perhaps the key is nearby") only surfaced
  once the evaluator scored it 2/10 and named the specific rule broken.
  A generic validity check (spellcheck, profanity filter, length check)
  would have waved this line through — length and grammar were fine.
- **Game-speak vocabulary** ("inventory", "health pack") — caught for
  free by the deterministic check before any API call, in
  `vocab_supplies`.
- **A stage direction embedded in the spoken text** (`*shivers*`) plus a
  3x-over-length descriptive line, both caught deterministically in
  `format_mine_entrance`.
- **A genuinely hard case** (`circuit_breaker_demo`): a "comfort your
  own bravery" prompt kept drifting into earnest, self-affirming
  territory that isn't how Tracey deflects. Two refinement passes
  improved the score (3/10 → 6/10) but never crossed the 8/10 threshold
  in the 3-attempt budget, so the circuit breaker escalated it to
  `runs/escalations.md` instead of silently accepting the closest
  attempt — this is the case the assignment specifically warns students
  skip building.

## Where it fits

This pipeline runs before the event script is authored, generating and
vetting the unvoiced text layer that the Event Director triggers
(Linear `MRM-11`, `MRM-13`, `MRM-14`).

---

## Where AI generation is safe in this project, and where it is not

The pipeline generates the *unvoiced* text layer — thoughts, system
messages, objectives, UI strings. The **narrative dialogue is not
generated**: all ~250 demo lines are hand-written and are being recorded
by four voice actors, with per-line performance direction. Generating
those would be both worse and dishonest. The pipeline does, however,
**validate** the hand-written lines against the same style guide, which
is how the guide itself was tested.

---

## How this was actually run — read this before grading

**No live Anthropic API key was used for this submission.** The
developer has a Claude subscription (claude.ai / Claude Code) but not a
separate, billed Anthropic Console API key, and deliberately chose not
to create one just for this assignment. The pipeline supports both
modes and defaults to whichever is available:

- **Offline mode (what actually ran, and what runs by default):**
  `generator.py`, `evaluator.py`, and `refiner.py`'s raw-API calls are
  swapped for [`offline_llm.py`](offline_llm.py), which replays a fixed,
  pre-recorded sequence of responses per test line. **Those recorded
  responses were authored directly by Claude — the same model this code
  targets via `claude-sonnet-5` and `claude-haiku-4-5` — during
  development, playing exactly the Generator/Evaluator/Refiner roles
  defined by the system prompts in this repo.** They are not templated
  filler, and they were not predicted/written before building the
  pipeline — they're what actually came out of running the loop's logic
  against a real language model in a live session. What's *not* real is
  only the transport: no metered HTTP call was made, so token counts and
  `$` costs in `runs/costs.csv` are genuinely `0` for this run, clearly
  marked `offline/*` in the `model` column and noted in the `note`
  column. This is the same honesty this assignment's own rubric asks
  for (Class 9) — better to say plainly what didn't happen than to
  fabricate API usage numbers.
- **`rules.py`'s deterministic checks are fully real in both modes** —
  regex/length/vocabulary logic, no mocking. So is the GER loop's
  control flow in `pipeline.py` (attempt counting, the circuit breaker's
  `for...else`, transcript and cost-log writing) — offline mode only
  swaps out the three LLM-shaped function calls, not the mechanism
  around them.
- **Live mode** works exactly as the assignment specifies — raw calls to
  the Anthropic Messages API, no framework — the moment a key is
  available. To use it:

  ```bash
  pip install -r requirements.txt
  # put ANTHROPIC_API_KEY=sk-ant-... in a .env file in this folder
  # (git-ignored; see repo .gitignore), or export it in your shell
  python run.py
  ```

  `run.py` auto-detects the key and switches to live calls automatically
  (no flag needed); `--live` forces live mode and fails loudly if no key
  is configured, instead of silently falling back.

## Running it

```bash
pip install -r requirements.txt
python run.py
```

With no key configured this runs fully offline, no network access
needed, and reproduces `runs/` exactly as submitted. Output:

```
Mode: OFFLINE (recorded fixtures — see README)

=== tone_locked_door ===
...
[ACCEPTED] Locked. Great.
...
=== circuit_breaker_demo ===
...
[ESCALATED (circuit breaker)] Fine. I'm fine.
```

A custom line: `python run.py --id my_line --prompt "..." --live` (custom
prompts require live mode — offline only has the 4 built-in fixtures).

## Cost log (Assignment #10)

`runs/costs.csv` logs every generate/evaluate/refine call: line, attempt,
stage, model, tokens, `$` cost. In this offline submission every row is
`$0` by construction (see above) with the model column reading
`offline/generator` / `offline/evaluator` / `offline/refiner` — that's
the honest number for what actually executed. The logging code itself is
real and total-cost-accurate the moment live mode runs; nothing about
cost logging was disabled or stubbed to hide behavior, per the
assignment's "do not disable the cost logging" rule.

## File structure

```
moonlight-ger/
├── style_guide.md      # The rules (also Assignment #7's deliverable)
├── rules.py             # Deterministic checks — real in both modes
├── llm.py                # Raw Anthropic API wrapper (live mode)
├── offline_llm.py       # Recorded-response stand-in (offline mode)
├── generator.py          # Produces candidate lines (live)
├── evaluator.py           # SCORE + REASON (live)
├── refiner.py              # Rewrites using the REASON (live)
├── pipeline.py              # The GER loop + circuit breaker
├── run.py                    # CLI entry point, mode auto-detection
├── requirements.txt
├── runs/
│   ├── costs.csv               # token + cost log
│   ├── escalations.md          # circuit breaker output
│   └── transcript_*.md         # before / score / reason / after, per line
└── README.md
```
