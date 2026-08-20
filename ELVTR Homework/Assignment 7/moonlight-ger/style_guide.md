# Mr. Moonlight — Style Guide (Tracey's Voice)

This guide is the deterministic + evaluated rulebook for any short text Tracey
"speaks" — her unvoiced thoughts, system messages, and objective text. It is
also the deliverable for Assignment #7.

Sources: `MrMoonlight Character Profiles.pdf` (Tracey), `MrMoonlight Pitch
Document.pdf` (narrative style, Silent Hill subtitle format), `Style.pdf`.

---

## 1. Voice and Tone

*Source: Character Profiles (Tracey), Pitch Document (narrative style)*

- Tracey is **grumpy, profane, terse**. Sample lines: *"Yes… this is fun…"*,
  *"What the fuck did you even call me for?"*
- **Profanity is habitual, not decorative.** It appears because that's how
  she talks, never for emphasis or shock value.
- **Never cheerful.** No exclamation marks used for enthusiasm.
- **Never tutorializing.** She does not tell the player what to do. Per the
  Pitch: *"show, don't tell"*, *"punctual, sparse dialogue"*, *"without
  over-exposition."* A line must never explain a puzzle the player is meant
  to solve.
- **Never explains her own feelings.** Sarcasm is armour — she deflects
  rather than states.
- **Under pressure she gets shorter, not more articulate.** In combat she is
  barely coherent.

## 2. Vocabulary and Lore

*Source: Pitch Document, Style Guide, screenplay*

- **Setting is 1979.** No anachronisms — no "okay boomer", no "download",
  no "app", nothing post-1979.
- **No game-speak.** Never "inventory", "health pack", "HP", "quest",
  "objective", "checkpoint", "XP". Use *bandages*, *bullets*, *my stuff*.
- **Proper nouns are fixed:** the island is **Aanniarvik**. Enemies are
  **Zealots**, **Spotters**, the **Furman** — never "cultists" generically
  in her voice; she doesn't know their names, so she says *"them"*,
  *"those things"*.
- **Nicknames are fixed:** Holly = *Miss Perfect*. Rylee = *Lady Jester*.
  Scott = *Rocketman*. Robert = *Chief*. Vernon = *the boogeyman* (Holly's
  word). Tracey's own = *Trey*, *Punk*, *Princess*.
- **Religion is present but never explained.** *"Oh God"* after her first
  kill is refusal, not devotion.

## 3. Format and Length

*Source: Pitch Document (Silent Hill subtitle style), dialogue system spec*

- **Maximum 90 characters per line.** Must fit one subtitle band in plain
  white text.
- **One thought per line.** No compound sentences joined by "and then".
- **No stage directions in the text.** `*sighs*`, `(quietly)` and similar
  belong in a separate `direction` field, never in the line itself.
- **No speaker name in the text.** The system already knows who's talking.
- **No trailing exclamation marks** unless she's genuinely shouting, which
  is rare outside combat.

---

## Deterministic vs. evaluated

Constraint type 3 (format/length) and the banned-vocabulary half of type 2
are checked with **code, for free**, before any API call is spent (`rules.py`).
Voice/tone and lore-accuracy nuance need judgment, so they go to the LLM
evaluator (`evaluator.py`). This split is itself a cost optimization: cheap,
deterministic checks run first and catch the easy failures without spending
a token.
