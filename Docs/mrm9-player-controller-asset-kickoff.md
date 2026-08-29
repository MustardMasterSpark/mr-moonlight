# MRM-9 revisit — free player-controller asset, kickoff

**Written 2026-08-29, end of the mrm-70 session.** Read this first if you're picking up "swap in
the free player controller asset Carlos found." **Do not start implementing anything until you've
read the rejection history below and gotten the asset's name + a link/files from Carlos** — this
prompt exists because the same shape of decision was already made once this month and reversed
zero times.

## What Carlos actually said (end of the mrm-70 session, 2026-08-29)

> "We're gonna modify the player controller. I found a free asset that I think is better. It's
> kind of polished so we need to open that issue on the next chat session."

That's all that's known. **The asset's name, source, and what it actually ships were not
captured this session** — first thing to do is ask.

## Branch / issue

- Work belongs on its **own new branch**, not `mrm-70` (one issue, one branch, one PR —
  `CLAUDE.md`). `mrm-70` should be merged to `main` first (see the commit description prepared
  the same session as this doc) so this branch starts from a clean base.
- Likely home: **`MRM-9` — "Player prefab + FPS controller (move, look, jump, crouch, sprint)"**,
  currently **Done**, branch name on record: `mrm-9`. Confirm with Carlos whether this reopens
  MRM-9 or is a new issue — his phrasing ("open that issue") suggests reopening MRM-9, but don't
  assume; a controller *replacement* might warrant a fresh issue with MRM-9 linked as superseded.

## Read this before evaluating the new asset — MRM-9 already rejected one, hard

MRM-9's Linear description carries a full **Round 2 rejection of `FPS Engine` (cowsins)**, dated
2026-08-28, evaluated for exactly this same purpose ("should this replace the hand-written
controller?"). It was rejected on **three independent grounds**, and the same checklist applies to
whatever Carlos found now:

1. **The full-body look-down requirement.** MRM-9's own acceptance criterion: *"Looking straight
   down shows placeholder body geometry, not empty space."* Every FPS template evaluated so far,
   FPS Engine included, is architected around an **arms-only viewmodel on a separate camera with
   its own near-clip** — retrofitting a true full body fights the asset's core assumption. Check
   this first; it alone sank the last candidate.
2. **`ProjectSettings` overwrite risk.** Many asset-store FPS controllers ship an import dialog
   offering to overwrite `TagManager.asset`, `GraphicsSettings.asset`, `QualitySettings.asset`,
   `InputManager.asset`, `ProjectSettings.asset`, etc. — clobbering the URP renderer assignment,
   physics setup, and tag/layer table this project depends on, **silently**. Standing rule since
   that finding: **when importing any package that offers a `ProjectSettings` group, uncheck the
   whole group.**
3. **Class-name collisions + rework blast radius.** FPS Engine shipped `PlayerMovement`,
   `PlayerStats`, `InputManager`, `InteractManager`, `Interactable`, `Item_SO`, `UIController`,
   `PauseMenu`, `Checkpoint`, `SoundManager`, `Crosshair`, `Compass` — overlapping **11 other
   issues**, 5 of them already `Done` (MRM-8, MRM-12, MRM-16, MRM-17, MRM-38, MRM-41, MRM-42,
   MRM-43, MRM-45, MRM-19 among them), several with literal class-name collisions (`PlayerStats`,
   `Interactable`). Adopting a controller package wholesale reopens all of them.

**What that issue's own resolution was**: the FPS Engine package stays in Playground as a
**read-only reference and parts donor only** (recoil curves, weapon sway, crosshair/hitmarker,
`Weapon_SO` field taxonomy) — none of its runtime code entered Mr. Moonlight. If the new asset is
similar in shape, the same "extract parts, don't adopt wholesale" outcome may be the right call
again — don't treat "polished and free" as sufficient on its own.

**None of this means the new asset is automatically rejected too** — it might genuinely handle the
full-body case, or Carlos may have already checked. It means: **run it through the same three
checks before writing a line of integration code**, and don't re-litigate what MRM-9 already
settled (five other issues do not need to reopen just because a controller changes, unless the new
asset's classes actually collide with theirs — check names before assuming).

## Current PlayerController.cs baseline (as of the mrm-70 merge)

The hand-written controller being evaluated against has just had one simplification (uncommitted
on `mrm-70`, confirmed intentional 2026-08-29, folded into the merge to `main`): the steep-slope
sliding mechanic (`onSteepSlope` check + `SlideSpeed` tunable, added during MRM-58's terrain
blockout to stop jump-climbing up cliffs one hop at a time) was **removed**, and `CheckGrounded()`
simplified back to a plain "is there ground under me" bool (no longer reports `groundNormal`).
Whatever asset/approach is chosen, this is the actual current behavior to compare against, not
what's described in MRM-9's original scope table (which still lists `SlideSpeed`).

## Files that will be touched

- `Assets/_Project/Code/Runtime/Player/PlayerController.cs`
- `Assets/_Project/Code/Runtime/Data/MoonlightTunables.cs` (every value still goes here — no
  hardcoded values, `CLAUDE.md` hard rule)
- Player prefab (path TBD — find via the MRM-9 PR: `https://github.com/MustardMasterSpark/mr-moonlight/pull/5`)
- `Docs/external-assets.md` — log the new asset here (adopted/rejected/evaluated, per the existing
  table format) regardless of outcome, per `[[feedback_asset_adoption_protocol]]`-style discipline
  already practiced on this project: adopting an asset means fixing Linear **and** leaving a
  per-asset approach brief, not just writing code.

## Documentation gaps flagged while preparing this handoff

- **No standalone doc holds the "controller-replacement evaluation checklist"** — the three checks
  above live only inside MRM-9's Linear description. If another controller candidate comes up
  after this one, whoever picks it up will again have to re-read MRM-9 in full to reconstruct it.
  Worth promoting into `Docs/new-asset-list.md` or a small dedicated note if this becomes a
  recurring pattern (Carlos's call, not done here).
- **The `SlideSpeed` removal (above) has no design-rationale record anywhere** — not in a commit
  (it was uncommitted until this branch's merge), not in a doc, not in memory. The merge commit
  message will be the only record; if the reason was "the new Gaia terrain doesn't have slopes
  steep enough to need it" or similar, worth a one-line comment in `MoonlightTunables.cs` or
  `PlayerController.cs` saying so, so a future reader doesn't wonder why a documented MRM-58
  incident-driven mechanic just disappeared.
- **`Docs/glossary.md` and `Docs/csharp-conventions.md`/`Docs/unity-conventions.md` were not
  re-checked against this specific task** — read them fresh next session per the standard
  `CLAUDE.md` "read first" list; nothing vegetation-specific here should be assumed to carry over.
