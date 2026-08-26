# MRM-16 — interaction system kickoff

> **SUPERSEDED 2026-08-26, same day this was written.** Carlos decided to work MRM-16 together with
> MRM-41 and MRM-42 on one shared branch (`mrm-41`) instead of MRM-16 alone. Use
> `Docs/mrm41-items-interaction-kickoff.md` instead — kept here only for the reasoning trail.

Paste the prompt below to start a fresh session. Written 2026-08-26 right after MRM-18 (main menu)
went to In Review and branch `mrm-16` was picked as the next piece of work — general mechanics,
picking up from Player/Input/Stats (MRM-9/8/12), all already Done.

---

## Prompt to paste

> Resuming **Mr. Moonlight**, branch `mrm-16`, Linear issue **MRM-16** ("Interaction system —
> highlight, prompt, pickup"). Read `CLAUDE.md` first. **Linear is the source of truth for scope —
> read MRM-16 directly** (`get_issue`, id `MRM-16`) rather than trusting a paraphrase; if this doc
> and the issue disagree, the issue wins.
>
> **MRM-16 scope, in short:** an `Interactable` component (display name + interaction type) that
> any world object can carry. Detection is **proximity + aim** — the player must be within a
> tunable nearby distance *and* have the screen centre within a tunable angle of the object; when
> both hold, an on-screen prompt (the action symbol) **fades in**, and the object itself
> **highlights**. Looking away fades the prompt back out — never a dry pop, per the issue. `X`
> triggers the interaction. The component exposes hooks for three interaction kinds: pick up
> (item), use (turret, stretcher), and event-gated (a door the level's Event Director must unlock
> first) — but this issue only builds the hooks, not what's behind them.
>
> **Explicitly NOT in scope, even though it sounds adjacent:** inventory storage, the
> pickup-disappears rule, and weapon-specific pickup behaviour. Those belong to **MRM-41** (item
> framework + demo catalogue) and **MRM-42** (inventory UI) — separate Backlog issues that call
> *into* MRM-16's `Interactable`, not the other way around. If Carlos talks about "the interaction
> and inventory system" as one thing, that's the topic area, not today's ticket — stay inside
> MRM-16's own scope and flag it explicitly if inventory-shaped work seems to be creeping in.
>
> **Acceptance criteria (from the issue):**
> - Approaching an interactable and looking at it fades the prompt in; looking away fades it out
> - The object visibly highlights
> - `X` triggers the interaction
> - Two interactables close together resolve to the one the player is actually looking at
> - Distances are tunable and take effect in play mode
>
> **Tunables the issue calls out explicitly:** nearby distance, screen-centre angle tolerance,
> prompt fade-in duration, prompt fade-out duration, highlight intensity, highlight colour — all go
> in `MoonlightTunables` per the CLAUDE.md hard rule.
>
> **Handoff split, per the issue:** Claude builds the system; **Carlos tags props as interactable
> in the scene** once real props exist there. That's a later, separate step — don't go tagging
> Island props as part of this issue. For building and testing the system itself, Sandbox is the
> right place (`Docs/unity-conventions.md`'s "every system gets tested there before it goes near
> the demo scene").
>
> **Standing rules:** ask Carlos before Unity scene/inspector work (and before Blender work, same
> extended rule), then do it, verify by reading real state back, and document. Never commit or
> push — Carlos does that himself via GitHub Desktop. One issue, one branch, one PR (`mrm-16` /
> MRM-16).

---

## The 3D models step (Carlos's heads-up, 2026-08-26)

Carlos said he'll be supplying 3D models for this pass — **possibly final assets** (he named the
flashlight specifically), not placeholders. These get **the same treatment MRM-70 gave the
vegetation props**, not a new pipeline:

1. **Blender prep** — see `[[blender-export-process]]` memory / the checklist MRM-70 used: 1 Blender
   unit = 1 m at true real-world size, `Ctrl+A` Apply All Transforms, origin at the feet (or the
   natural handhold/pickup point for a small prop — use judgment, MRM-9's feet-origin reasoning is
   player-rig-specific and may not apply to a held item), FBX export with `Apply Transform` checked
   (`bake_space_transform=True` if exporting via the Blender MCP bridge directly).
2. **Unity import + material treatment** — matching `Docs/pc-build-target.md` §7's Retro Shaders Pro
   section: `_BaseColor` → Point filter (usually already covered by `MoonlightTextureImporter`),
   and a material call to make at the time — `URP/Lit` if the prop doesn't need the PSX look, or a
   migration to `RetroLit.shader` if it should visually match the terrain/vegetation's pixelated
   treatment. Ask which one Carlos wants per prop rather than assuming; small held items (a
   flashlight in Tracey's hand, up close) may read differently than a distant environment prop.
3. **Ask permission before touching Blender or the Unity project with these models**, same
   hard rule as always — then do it, verify by reading the actual mesh/material/import state back,
   and document what changed (matches `Docs/mrm70-prefab-build-summary.md`'s shape: a short
   build-summary doc per pass).

**Don't block on this.** The interaction system's actual logic (proximity/aim math, the prompt's
fade, the highlight mechanism, the `X` trigger, disambiguating two close interactables) is fully
buildable and testable in Sandbox against placeholder primitives (a cube, a capsule) before any
real model arrives. Build and prove the system first; swap in Carlos's models once they're prepped,
without needing to touch the interaction code itself.

## Quick facts to not re-derive

- Player/Input/Stats are Done (MRM-9, MRM-8, MRM-12) — `PlayerController`, `InputMapController`
  (`InputSystem_Actions.Gameplay`/`.UI` maps already exist), `PlayerStats`. The interaction system
  hooks into whichever of these already expose what it needs (camera transform for the aim test,
  an input action for `X`) rather than duplicating them.
- MRM-20 (Sparring dummy — damage numbers + health bar test target) is **still Backlog**, not
  built. Sandbox doesn't have a dedicated test dummy yet — for MRM-16's own testing, simple
  placeholder objects with `Interactable` attached are enough; don't build the sparring dummy as
  part of this issue, that's its own ticket.
- MRM-18 (main menu) is **In Review, not closed** — Carlos is merging `mrm-18` to `main` now as a
  checkpoint even though it's not finished (fine-tuning/staging is still open on MRM-67). Don't
  treat that merge as completion; don't touch MRM-18's Linear status or docs as a side effect of
  starting this issue.
