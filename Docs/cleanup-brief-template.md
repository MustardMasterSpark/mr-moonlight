# Cleanup brief: template and prep process

Written 2026-09-19. This is the input for the **big cleanup / new direction** session. That session runs on
**Fable**, and its context prompt is `Docs/cleanup-architecture-fable-prompt.txt`.

The pipeline has three stages. Only the last one uses Fable credits.

| Stage | Who / model | Input | Output |
|---|---|---|---|
| A. Brain dump | Carlos (dictated, any order, typos fine) | notebook notes | `Docs/cleanup-brief-raw.txt` |
| B. Polish + briefing pack | Opus (or Sonnet) session | the raw file, Linear, the repo | `Docs/cleanup-brief.md` + `Docs/cleanup-briefing-pack.md` |
| C. Architecture session | **Fable** | B's two files + the context prompt | new strategy doc, issue-by-issue plan, then Linear updates |

---

## Stage A: how Carlos writes the raw notes

Write the way you talk. You don't need to polish anything, and you don't need to follow the section order below.
Stage B sorts it. The only habits that matter:

1. **Say what you want and why.** Leave out how to build it. "Enemies should feel like they hunt as a group,
   because right now each one feels alone" beats "add a squad manager".
2. **Tag how sure you are**, even as one word at the end of the line:
   - **DECIDED**: don't argue with it.
   - **LEANING**: push back if there's a better way.
   - **OPEN**: I don't know, help me think.
3. **Say what bothers you now.** Things you don't trust, things that confuse you, things that feel fragile.
4. **Say what must not change.** Things that work and that you love.
5. If you remember an issue number (MRM-xx) or a system name, mention it. If you don't, that's fine.

## The shape Stage B produces (`Docs/cleanup-brief.md`)

```
0. ONE-PARAGRAPH SUMMARY: what this cleanup is, in Carlos's terms

1. THE GOAL
   What the demo has to be by Halloween 2026. What the Kickstarter needs to show.
   What "done" feels like as a player.

2. THE LIST: one entry per idea, numbered B-01, B-02, ...
   - What:     what Carlos wants
   - Why:      the reason (marked "(why missing)" if he didn't give one)
   - Certainty: DECIDED / LEANING / OPEN
   - Touches:  MRM-xx issues / systems / assets it likely affects (Stage B fills this in)
   - Conflict: where it contradicts a current Linear issue or doc (Stage B fills this in)
   - Carlos's original words (verbatim quote, typos fixed only where the meaning is certain)

3. WHAT BUGS ME: cleanup pain points, things he doesn't trust, fragile or confusing parts

4. DON'T TOUCH: what works and stays, and the hard constraints

5. NEW ASSETS: owned or bought assets he wants used (AST-### IDs from the asset index)

6. WHAT I WANT BACK FROM THE FABLE SESSION
   Defaults, unless Carlos changes them: strategy/architecture doc, issue-by-issue plan,
   questions first, no Linear writes before approval.

7. STAGE B NOTES
   - Gaps: entries with no "why", or ambiguous ones, and what Carlos answered
   - Contradictions found between entries
   - Things Stage B could not place
```

## Stage B: instructions for the polish session (Opus or Sonnet)

Trigger: Carlos hands over `Docs/cleanup-brief-raw.txt` (or pastes Notepad text) and asks to "fix and optimize it
for Fable". Do not start before he says so.

1. **Keep his meaning; don't add ideas.** Fix spelling and dictation errors, merge duplicates, split entries that
   hold two ideas, and sort everything into the shape above. Keep his original words under each entry, so Fable
   can see the tone and intent behind the cleaned-up version. Never upgrade a LEANING to DECIDED, and never drop
   an item you think is a bad idea. Flag it in section 7 instead.
2. **Fill "Touches" and "Conflict"** by checking the current Linear issues (project `MrMoonlightDemo`, team `MRM`)
   and the docs. This is the mechanical cross-referencing the Fable session shouldn't pay for.
3. **Ask Carlos one batch of questions** about the gaps: a missing why, ambiguous wording, contradictions. Put the
   answers into the brief.
4. **Build the briefing pack**, `Docs/cleanup-briefing-pack.md`, so Fable reasons instead of reading 200 files:
   - **Linear inventory.** Every MRM issue: ID, title, status, a one-line real state (Linear status is not the same
     as completion, since Carlos merges to main as checkpoints), and parent/sub-issue links.
   - **System map.** Each runtime system (player/Tracey + PolymindGames FPSCore, weapons, enemies (Spotter + Blaze
     AI + DemoSpotterPopulationManager), Event Director, audio (mixer, EnemyAudioHooks, vendor AudioManager),
     lighting (TimeManager, HAZE, flashlight, ViewModel layer), UI/menus, gore, vegetation/terrain/water, SessionLog):
     its main files, what it depends on, whether it's demo-only/legacy (as its own comments say), and known debt.
   - **Vendor exposure.** What's copied into `Assets/_Project/Code/Vendor/`, and any self-applying code
     (`RuntimeInitializeOnLoadMethod`, singletons), per the vSync lesson in
     `Docs/demo-wrapup-audio-music-sonnet-prompt.txt`.
   - **Asset state.** Installed vs. owned vs. wishlist, from `Docs/external-assets.md` ("2026-09-16 audit") and the
     asset index. List only what's relevant to the brief's entries.
   - **Performance baseline.** The latest numbers and open hypotheses from `Docs/performance-sessions.md` (MRM-85).
   - **Known traps.** A short list of the memory/doc traps that would bite a rework: prefab propagation,
     Play-Mode edits reverting, the tag-on-collider rule, the Blaze state-replacement rule, VolumeProfile sub-assets,
     serialized-data migration, and so on. One line each, with a pointer to the full doc.
   Keep it factual and compact. Every claim should point to a file or an issue. No recommendations; those are
   Fable's job.
5. **Stop there.** Don't propose architecture, don't edit Linear, and don't change code. Tell Carlos both files are
   ready and that the next step is the Fable session with `Docs/cleanup-architecture-fable-prompt.txt`.
