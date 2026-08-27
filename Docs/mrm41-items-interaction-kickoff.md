# MRM-41 / MRM-16 / MRM-42 — items, interaction & inventory kickoff

Paste the prompt below to start a fresh session. Written 2026-08-26. **Supersedes
`Docs/mrm16-interaction-kickoff.md`** — Carlos decided the same day, before that work started, to
build MRM-16, MRM-41 and MRM-42 together on one branch instead of MRM-16 alone.

**Branch `mrm-41` already exists and is checked out** — created by Claude via `git checkout -b
mrm-41` directly off `mrm-18`'s tip (one commit, "Add unstyled main menu scene", not yet merged to
`main` as of this writing) at Carlos's explicit request. A fresh session does not need to create it
again; just confirm it's still the active branch.

---

## Prompt to paste

> Resuming **Mr. Moonlight**, branch `mrm-41`. **Carlos's explicit call (2026-08-26): work MRM-16,
> MRM-41 and MRM-42 together on this one branch**, not the usual one-issue-one-branch-one-PR split
> `CLAUDE.md` otherwise mandates. That's a deliberate, flagged deviation for this specific case (the
> three are tightly coupled — 42 needs items from 41, 41 needs the pickup hook from 16) — read all
> three issues directly (`get_issue` for `MRM-16`, `MRM-41`, `MRM-42`) rather than trusting a
> paraphrase; if this doc and an issue disagree, the issue wins.
>
> **Build order matters even though the commit doesn't split:** MRM-16's `Interactable` +
> detection/prompt/highlight first, then MRM-41's item framework (pickup consumes MRM-16's hook,
> adds storage), then MRM-42's inventory UI (displays what MRM-41 stored). Don't skip ahead —
> each layer's acceptance criteria assume the one before it works.
>
> ### MRM-16 — Interaction system (highlight, prompt, pickup hooks)
> `Interactable` component (display name + interaction type). Detection is **proximity + aim** —
> within a tunable nearby distance *and* screen-centre within a tunable angle. Both true → prompt
> **fades in** (action symbol), object **highlights**. Looking away fades the prompt back out —
> never a dry pop. `X` triggers it. Hooks only, for three kinds: pick up (item), use (turret,
> stretcher), event-gated (director-locked door) — MRM-16 itself doesn't implement what's behind
> the hooks. Two close-together interactables must resolve to the one actually being looked at.
> **Not in scope even here:** inventory storage, pickup-disappears, weapon-specific behavior — those
> are MRM-41/42 calling into this, not the reverse.
>
> ### MRM-41 — Item framework + demo catalogue
> Item = GameObject with a 3D prop + `Interactable` + type + effect. Picked up via MRM-16's hook,
> added to inventory, **prop removed from the world** (the universal pickup rule). Demo catalogue:
> Crackers/Soda (food, small heal+stamina), Bandages (small heal), Vodka/Beer (+drunkenness),
> Marijuana blunt (+weed high), Morphine vial (+morphine high), Pistol ammo/Shotgun shells (refill,
> stack as one type regardless of count). Plus non-consumable equipment covered elsewhere but the
> framework should account for it: canteen, walkie-talkie, matches, map+compass, flashlight, boots,
> backpack, Polaroid, tent key. **Storage counts distinct item types, not total items** — Pocket: 4
> types (early game), Backpack: 10 types (after the cabin event raises the cap). The Scene 2 blue
> cooler deliberately holds more than pocket capacity — that's the tutorial for the cap, so refusing
> a 5th type with clear feedback must actually work.
>
> ### MRM-42 — Inventory UI (spinning 3D items, no pause)
> **Blocked on Carlos, don't guess:** the issue's own text says *"ACTION REQUIRED: GIVE MOCK UP
> IMAGE ON ISSUE... Do not guess the layout."* Carlos also owns the two open animations (backpack vs.
> no-backpack) and the close/return animation. **Check MRM-16 and MRM-41 first for whether Carlos
> has attached the mockup / provided animations yet** — if not, build and verify MRM-16+41 fully,
> and either ask Carlos for the mockup or stop there and flag it rather than guessing a layout the
> issue explicitly warns against. Once unblocked: opens on D-pad left/right or mouse wheel, plays
> the entry animation, **does not pause the game** — Tracey is locked in place and can still be
> attacked and damaged while it's open, which is the deliberate point of the design, not a bug to
> "fix." Items render as **spinning 3D models**, navigate left/right, `A` uses, `B` closes with a
> fade + return animation. Each item shows a text description and a plain-number quantity (matters
> most for ammo stacks). Opening during a cutscene must be prevented; dying with it open must close
> it cleanly (ties into MRM-17's death sequence, already Done).
>
> **Acceptance criteria, consolidated:**
> - MRM-16: prompt fades in/out correctly on approach/look-away; object highlights; `X` triggers;
>   two close interactables resolve correctly; distances tunable and live in play mode
> - MRM-41: every catalogue item can be placed/picked up/used; prop disappears on pickup; a 5th
>   pocket type is refused with clear feedback; the cabin event raises the cap to 10; ammo stacks as
>   one type; all effect values tunable
> - MRM-42: opens/closes on both input methods; correct animation for backpack state; **an enemy
>   can damage Tracey while it's open (verify this explicitly)**; items spin and navigate; ammo
>   shows a count; blocked during cutscenes; dying with it open closes cleanly
>
> **Tunables, all three issues, all in `MoonlightTunables` per the CLAUDE.md hard rule:**
> - MRM-16: nearby distance, screen-centre angle tolerance, prompt fade-in/out duration, highlight
>   intensity and colour
> - MRM-41: per-item heal amount, stamina amount, drug stat increase; pocket storage cap (4);
>   backpack storage cap (10)
> - MRM-42: open/close animation durations, item spin speed, fade durations
>
> **Handoff split, per the issues:**
> - MRM-16: Claude builds the system; **Carlos tags props as interactable in the scene** later, once
>   real props exist there — don't tag Island props as part of this pass
> - MRM-41: **Carlos provides every item prop model** — see the 3D-models section below
> - MRM-42: **Carlos attaches the mockup image and the two/three animations** — hard blocker, see
>   above
>
> **Standing rules:** ask Carlos before Unity scene/inspector work (and Blender work, same extended
> rule), then do it, verify by reading real state back, and document. Never commit or push — Carlos
> does that himself via GitHub Desktop. **This branch is the deliberate exception to
> one-issue-one-branch-one-PR** — all three issues' work lands in one PR when it's ready, by
> Carlos's own call.

---

## The 3D models step (carried over from the MRM-16-only kickoff)

Carlos is supplying prop models for this pass, possibly final assets (named the flashlight
specifically) — not placeholders to be swapped later. Same treatment MRM-70 gave the vegetation
props, not a new pipeline:

1. **Blender prep** — `[[blender-export-process]]` memory / MRM-70's checklist: 1 Blender unit = 1 m
   at true real-world size, `Ctrl+A` Apply All Transforms, origin at the feet or the natural
   handhold/pickup point (use judgment — MRM-9's feet-origin reasoning is player-rig-specific), FBX
   export with `Apply Transform` checked (`bake_space_transform=True` via the Blender MCP bridge).
2. **Unity import + material treatment** — `Docs/pc-build-target.md` §7: `_BaseColor` → Point filter
   (usually already covered by `MoonlightTextureImporter`), and a per-prop call on plain `URP/Lit`
   vs. migrating to `RetroLit.shader` for the PSX look — ask Carlos which per prop rather than
   assuming; a held item up close may read differently than a distant environment prop.
3. **Ask permission before touching Blender or the Unity project with these models**, same hard
   rule as always — then do it, verify by reading the actual mesh/material/import state back, and
   document what changed (short build-summary doc, same shape as
   `Docs/mrm70-prefab-build-summary.md`).

**Don't block MRM-16's own logic on models arriving** — proximity/aim math, the prompt fade, the
highlight mechanism, disambiguation, are all buildable and testable in Sandbox against placeholder
primitives first.

## Quick facts to not re-derive

- Player/Input/Stats are Done (MRM-9, MRM-8, MRM-12) — hook into these, don't duplicate them.
- MRM-20 (Sparring dummy) is still Backlog — not needed for MRM-16/41/42's own testing; simple
  placeholder objects with `Interactable` are enough in Sandbox.
- MRM-18 (main menu) is **In Review, not closed** — merged to `main` as an unfinished checkpoint per
  Carlos. Don't touch its Linear status or docs; polish backlog for it lives on **MRM-67**.
- Difficulty selection (built in MRM-18) is intentionally inert — no difficulty-scaling systems
  exist yet for it to affect. Not this branch's concern, just don't be surprised by it.
