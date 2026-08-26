# MRM-18 — main menu kickoff

Paste the prompt below to start a fresh session. Written 2026-08-26 right after MRM-70 (vegetation/
terrain) was paused and merged to `main`, and branch `mrm-18` was created off that merge.

---

## Prompt to paste

> Resuming **Mr. Moonlight**, branch `mrm-18`, Linear issue **MRM-18** ("Main menu scene
> (unstyled)"). Read `CLAUDE.md` first. **Linear is the source of truth for scope — read MRM-18
> directly** (`get_issue`, id `MRM-18`) rather than trusting a paraphrase; if this doc and the issue
> disagree, the issue wins.
>
> **Before touching vegetation/terrain work:** MRM-70 is *paused, not done* — it's still
> "In Progress" in Linear on purpose. Do not resume it without being asked, and do not treat its
> merge to `main` as completion. If that work ever comes back up, read
> `Docs/mrm70-pause-2026-08-26.md` first (lists everything still open: mine/cavern locations, new
> tree models, wind shader, a few open shader/rendering gaps).
>
> **MRM-18 scope, in short:** a separate, unstyled main menu scene — Start / Settings / Credits /
> Quit, every transition a fade, no hard cuts. Start fades to the demo scene. Settings has a
> Conformist/Punk difficulty pick (Punk default, passed through to the game scene), Master/Voices/SFX
> volume sliders wired to the correct audio mixer groups, and a Back button. Credits scroll a
> TextMeshPro Lorem Ipsum placeholder (Carlos writes real text later), skip on any input, and block
> raycasts to whatever's underneath while showing. Quit fades out then quits.
>
> **One thing in the issue text is stale:** its Quit note ("`Application.Quit` does nothing in
> WebGL...") is from before the platform switch. The project is Windows standalone now and
> `Application.Quit()` works for real (`Docs/pc-build-target.md`) — just quit normally, no browser
> workaround needed.
>
> **Not in scope** (per the issue): button styling, typography, logo art, background scene staging —
> that's a later UI pass and Carlos's own handoff. Carlos stages the background scenario scene and
> supplies the logo when it exists; build the functional layer now against placeholders.
>
> **Tunables the issue calls out explicitly:** fade durations, credit scroll speed, default slider
> values — these go in `MoonlightTunables` per the CLAUDE.md hard rule, no exception here (unlike the
> vegetation/staging numbers from the MRM-70 pass).
>
> **Standing rules:** ask Carlos before Unity scene/inspector work, then do it, verify by reading
> real state back, and document. Never commit or push — Carlos does that himself via GitHub Desktop.
> One issue, one branch, one PR (already satisfied: `mrm-18` / MRM-18).

---

## Quick facts to not re-derive

- `main` currently has MRM-70's work merged (PR #12, 2026-08-26) — Flora/HAZE/CRT rendering stack,
  PSX material migration, the `Scene Effects Toggle` dev tool. All of that is now baseline, not
  something this issue touches.
- Acceptance criteria (from the issue): all 4 menu options work; difficulty selection reaches and is
  readable in the game scene; sliders affect the correct mixer groups; credits scroll, skip on
  input, and block clicks underneath; every transition is a fade; Quit behaves sensibly.
