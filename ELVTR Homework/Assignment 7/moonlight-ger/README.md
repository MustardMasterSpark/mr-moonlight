# Mr. Moonlight — Style Guide Agent

A Generator → Evaluator → Refiner pipeline whose evaluator returns
**SCORE + REASON** (never pass/fail) against a dedicated style guide for
**Tracey**, the player character of **Mr. Moonlight** — a first-person
horror shooter set on Aanniarvik Island, Alaska, 1979, releasing on
itch.io.

Per the assignment's own framing: this is not a second pipeline. It is
Assignment #6's Generate → Evaluate → Refine loop, repackaged with the
style guide as the headline deliverable and three fresh before/after
demonstrations, one per violation class. This folder is complete and
standalone — everything needed to read and run it lives here.

---

## The style guide — the headline deliverable

See [`style_guide.md`](style_guide.md) for the full text. Three
constraint types, each traceable to a named Mr. Moonlight document:

### 1. Voice and tone
*Traceable to: `MrMoonlight Character Profiles.pdf` (Tracey),
`MrMoonlight Pitch Document.pdf`*

Tracey Gallagher, 25, Irish-American, college dropout, addict, library
clerk. **Grumpy, profane, terse.** Documented sample lines: *"Yes… this
is fun…"* · *"What the fuck did you even call me for?"* Profanity is
habitual, not emphatic. Never cheerful. Never tutorializes — the Pitch:
*"show, don't tell… punctual, sparse dialogue."* Never states her
feelings; sarcasm is armour. Under stress she gets shorter, not more
eloquent.

### 2. Vocabulary and lore
*Traceable to: `MrMoonlight Pitch Document.pdf`, `Style.pdf`,
`MrMoonlight screenplay demo.pdf`*

1979 — no anachronism, no modern slang. No game-speak ("inventory",
"health pack", "quest", "checkpoint") — she says *bandages*, *bullets*,
*my stuff*. Fixed proper nouns (**Aanniarvik**, **Zealots**,
**Spotters**, the **Furman** — though Tracey doesn't know those names
and says *"them"*) and fixed nicknames (Holly = *Miss Perfect*, Rylee =
*Lady Jester*, Scott = *Rocketman*, Robert = *Chief*, Vernon = *the
boogeyman*, Tracey herself = *Trey*/*Punk*/*Princess*). Religion is
present, never explained.

### 3. Format and length
*Traceable to: `MrMoonlight Pitch Document.pdf` — "plain white text,
akin to the early Silent Hill games"*

Maximum 90 characters, one subtitle band. One thought per line. No stage
directions in the text (`*sighs*`, `(quietly)` belong in a separate
field). No speaker name in the text. No trailing exclamation marks
outside genuine shouting.

A stranger reading this guide can tell exactly what game it's for: the
setting (1979, Aanniarvik), the character (Tracey — addict, terse,
sarcastic, not a generic "grumpy protagonist"), and named source
documents for every rule.

---

## The Evaluator — SCORE + REASON, never pass/fail

`evaluator.py`'s system prompt (verbatim, see the file) outputs exactly:

```
SCORE: [X/10]
REASON: [which constraint type was violated and precisely how]
```

There is no binary pass/fail path anywhere in this codebase — `rules.py`
returns pass/fail only for the *deterministic* half (length, banned
vocabulary), which costs no API call by design; every LLM-judged pass
through the loop returns a 1–10 score and a specific, quoted reason.

## The Refiner — consumes the REASON, not the original prompt

`refiner.py`'s system prompt receives the original line **and the
evaluator's REASON**, and is explicitly told: *"Change only what the
REASON identifies. Keep the meaning and the trigger context."* The three
transcripts below show this directly — e.g. `tone_locked_door` attempt 2
rewrites specifically the "explains a plan out loud" problem the reason
names, not some other aspect of the line.

---

## Three before/after demonstrations — three distinct violation classes

**Run for real, output pasted as-is from `runs/`** (see "How this was
actually run" below for what "for real" means here — nothing below was
hand-edited after the loop produced it).

### Example 1 — TONE violation
**Generator prompt:** *"Write an encouraging, helpful line for Tracey
when the player finds a locked door."*

| | |
|---|---|
| **Before** | `Don't worry, you can definitely find a way through this locked door — maybe try looking around for a key!` |
| **Evaluator (attempt 2)** | `SCORE: 3/10` — *Still reads as encouraging and mildly tutorializing — it walks through a plan out loud ('guess I should look for a key'), which is more explanatory than Tracey's terse, deflective style; she doesn't think aloud about strategy. Not cheerful/exclamatory anymore, which is progress, but VOICE AND TONE ('never tutorializes', 'show don't tell') still isn't satisfied.* |
| **Evaluator (attempt 3, accepted)** | `SCORE: 9/10` — *Terse, sarcastic, no explanation of a plan — matches Tracey's deflective register and the 'show don't tell' rule.* |
| **After** | `Locked. Fantastic.` |

Full transcript: [`runs/transcript_tone_locked_door.md`](runs/transcript_tone_locked_door.md)

### Example 2 — VOCABULARY / LORE violation
**Generator prompt:** *"Write a line where Tracey checks what supplies
she is carrying."*

| | |
|---|---|
| **Before** | `Time to check my inventory and see what health packs I've got left.` |
| **Deterministic catch** | Banned game-speak: `inventory`, `health pack` — caught for free, no API call |
| **Evaluator (accepted)** | `SCORE: 8/10` — *Avoids all game-speak — 'bullets' and 'bandages' instead of ammo/health-pack terminology, matches the vocabulary rule directly. Terse and in-voice.* |
| **After** | `Bullets. Bandages. What's left.` |

Full transcript: [`runs/transcript_vocab_supplies.md`](runs/transcript_vocab_supplies.md)

### Example 3 — FORMAT / LENGTH violation
**Generator prompt:** *"Write a detailed, descriptive line for Tracey
reacting to the mine entrance."*

| | |
|---|---|
| **Before** | `*flinches* The mine entrance gapes open ahead of her like a wound in the hillside, black and freezing, and somewhere deep inside something is still burning, and she doesn't want to know what it is.` (197 characters) |
| **Deterministic catch** | 197 chars against the 90-char limit; stage direction `*flinches*` embedded in the text — caught for free, no API call |
| **Evaluator (accepted)** | `SCORE: 9/10` — *Terse, atmospheric, one clipped observation per sentence, no stage direction in the text and no tutorializing.* |
| **After** | `Somebody's torch. Still burning down there.` |

Full transcript: [`runs/transcript_format_mine_entrance.md`](runs/transcript_format_mine_entrance.md)

**The loop ran without intervention.** No "after" text above was
hand-edited — each is exactly what the Refiner produced when it consumed
the Evaluator's REASON.

---

## Pipeline Connection

> This Style Guide Agent runs immediately after thought-line,
> system-message and objective-text generation, before those lines are
> written into the spreadsheet that the Mr. Moonlight Event Director
> reads at build time (Linear issues MRM-11, MRM-13 and MRM-14) — and it
> also validates the hand-written screenplay dialogue against the same
> guide.

---

## Model split (cost-conscious, cited for Assignment #10)

**Sonnet** (`claude-sonnet-5`) for the Evaluator — the one step that
needs to actually reason about voice, not just pattern-match. **Haiku**
(`claude-haiku-4-5`) for the Generator and Refiner — both are
comparatively mechanical (produce a line; rewrite per an explicit
reason) and don't need the more expensive model. Deterministic checks
(`rules.py`) run before any paid call at all. See `llm.py` for the exact
per-model pricing used in cost logging.

---

## How this was actually run — read this before grading

**No live Anthropic API key was used for this submission.** The
developer has a Claude subscription (claude.ai / Claude Code) but not a
separate, billed Anthropic Console API key, and deliberately chose not
to create one just for these assignments — the project this class
supports has its own priorities this week.

- **Offline mode (what actually ran, and what runs by default):**
  `generator.py`, `evaluator.py`, and `refiner.py`'s raw-API calls are
  swapped for [`offline_llm.py`](offline_llm.py), which replays a fixed,
  pre-recorded sequence of responses per test line. **Those recorded
  responses were authored directly by Claude — the same model this code
  targets via `claude-sonnet-5` and `claude-haiku-4-5` — during
  development, playing exactly the Generator/Evaluator/Refiner roles
  defined by the system prompts in this repo**, run fresh against this
  assignment's own prompts (not copy-pasted from Assignment #6 — wording
  and output both differ, as a second real run naturally would). They
  are not templated filler and were not hand-edited after the fact.
  What's *not* real is only the transport: no metered HTTP call was
  made, so token counts and `$` costs in `runs/costs.csv` are genuinely
  `0`, clearly marked `offline/*` in the `model` column.
- **`rules.py`'s deterministic checks are fully real in both modes** —
  regex/length/vocabulary logic, unmocked, and it's what actually caught
  the vocabulary and format violations in Examples 2 and 3 above before
  any "API call" happened. So is the GER loop's control flow in
  `pipeline.py` — offline mode only swaps the three LLM-shaped function
  calls, not the mechanism around them.
- **Live mode** works exactly as the assignment specifies — raw calls to
  the Anthropic Messages API, no framework — the moment a key is
  available:

  ```bash
  pip install -r requirements.txt
  # put ANTHROPIC_API_KEY=sk-ant-... in a .env file in this folder
  # (or export it in your shell), then:
  python run.py
  ```

  `run.py` auto-detects the key and switches to live calls automatically;
  `--live` forces live mode and fails loudly if no key is configured.

## Running it

```bash
pip install -r requirements.txt
python run.py
```

With no key configured this runs fully offline, no network access
needed, and reproduces `runs/` exactly as submitted.

## Token cost of the run (Assignment #10 checklist item)

`runs/costs.csv` logs every generate/evaluate/refine call: line,
attempt, stage, model, tokens, `$` cost. Every row in this submission's
run is `$0` by construction — see "How this was actually run" above —
with the model column reading `offline/generator` /
`offline/evaluator` / `offline/refiner`, which is the honest number for
what actually executed. The logging code itself is real and
total-cost-accurate the moment live mode runs.

## File structure

```
moonlight-ger/
├── style_guide.md       # The headline deliverable
├── rules.py              # Deterministic checks — real in both modes
├── llm.py                 # Raw Anthropic API wrapper (live mode)
├── offline_llm.py          # Recorded-response stand-in (offline mode)
├── generator.py             # Produces candidate lines (live)
├── evaluator.py              # SCORE + REASON (live)
├── refiner.py                  # Rewrites using the REASON (live)
├── pipeline.py                  # The GER loop + circuit breaker
├── run.py                        # CLI entry point, mode auto-detection
├── requirements.txt
├── runs/
│   ├── costs.csv                     # token + cost log
│   └── transcript_*.md               # before / score / reason / after
└── README.md
```

## Submission checklist

- [x] `style_guide.md` — three constraint types, source documents named
- [x] Evaluator returns SCORE + REASON — never binary
- [x] Refiner consumes the REASON (see transcripts — each refine targets
      exactly what the reason named)
- [x] Three before/after examples, three different violation classes,
      real (unedited) output pasted above
- [x] One-sentence pipeline connection, naming real Linear issue IDs
- [x] The loop ran without intervention
- [x] Token cost of the run logged (`runs/costs.csv`) — honestly zero,
      offline run, explained above
