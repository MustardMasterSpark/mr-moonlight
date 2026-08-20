# Mr. Moonlight — Context Index

**Read this first if you're picking up this project cold.** It tells you what exists, where it lives, and what's still open. For the actual operating rules, go to `kickstart.md` in this same folder and `CLAUDE.md` at the repo root — this file is a map, not a spec.

## What this project is

First-person horror shooter. Aanniarvik Island, Alaska, 1979. Unity 6.3 LTS, URP, **WebGL target, under 1 GB**, itch.io. Solo developer (Carlos / "Mustard"), AI writing most of the code, hard external deadline. Built for a class assignment (ELVTR) but the game itself is the deliverable — not the class homework.

Two milestones:
- **Sept 1** — playable loop (main menu → gameplay → death → game over → main menu). Graded class gate. If a stranger can't open the link and play within 2 minutes with no setup, max score is 50%.
- **Sept 8** — polished itch.io release.

## Source of truth

**Linear**, project `MrMoonlightDemo`, team `MRM`, 60 issues (`MRM-6` → `MRM-65`). https://linear.app/mrmoonlight/project/mrmoonlightdemo-68fb060cd95a

Design docs are background, not spec. If an issue and a doc disagree, the issue wins. No issue = not in the demo — do not infer scope from the pitch document.

## Folder map

```
E:\MrMoonlight\
├── CLAUDE.md                          ← read automatically every session
├── Claude Code Context MDs\           ← you are here — context storage, not read automatically
│   ├── README.md                      ← this file
│   ├── kickstart.md                   ← full operating rules (Part B) — read this in full each session
│   ├── Project MDs\                   ← source copies of what's now in Docs\
│   ├── DesignContext MDs\             ← source copies of what's now in Docs\Design\
│   └── Assets MDs\
│       └── Toolkit.md                 ← asset store buy/skip/defer evaluation, not copied into Docs\
├── Docs\                              ← installed per kickstart.md A.3, referenced from CLAUDE.md
│   ├── 00-INDEX.md, webgl-constraints.md, unity-conventions.md,
│   │   csharp-conventions.md, system-architecture.md,
│   │   data-schemas.md, glossary.md
│   ├── Design\                        ← 00-INDEX.md, screenplay, pitch, style guide, character profiles
│   ├── Design Docs\                   ← pre-existing raw source material (originals + grammar-corrected)
│   ├── GDD (deprecated)\              ← discarded — described a game 7x the scope of the demo
│   └── SLDD (deprecated)\
└── ELVTR Homework\
    └── Classes MDs\                   ← class-assignment context, NOT the game. Ignore unless working the homework specifically.
```

## Key rules (full detail in `kickstart.md`)

1. **No hardcoded values.** Everything in `MoonlightTunables`, commented, with the owning issue ID.
2. **Claude stops at the scene view.** Prefab placement, staging, inspector wiring, waypoints, hitboxes, animation keyframes, sound pools, feel-tuning — all Carlos's. Say what's needed and wait.
3. **Claude never commits or pushes.** Carlos pushes/merges through GitHub Desktop. Propose commit summary + description when asked; don't execute git write operations.
4. **One issue, one branch, one PR.** Use Linear's suggested branch name (format: `mustardmarisa/mrm-22-pistol-m1911`).
5. **Model discipline** — match the model to the issue's `## Model` line: Haiku for mechanical/boilerplate work, Sonnet (default) for ordinary systems, Opus only for the 6 architecture issues (MRM-6, 11, 27, 29, 45, 60). On an Opus issue, design then stop — implement in a fresh Sonnet session.
6. **Ask before assuming**, especially anything visual, audio, or scene-related.
7. **Update the Linear issue and `Docs/changelog.md`** (BUILT / DECISIONS / FAILED / NEXT) at the end of every issue.

## Canonical names

`Tracey` (not Tracy/Stacy), `Pickaxe` (melee weapon, not "the axe"), `Rylee` (not Riley), `Furman` (boss), `Zealot` (melee cultist), `Spotter` (ranged cultist w/ lamp), `Vernon`, `Shannon`, `Aanniarvik` (the island). Full list and reasoning in `Docs/Design/00-INDEX.md`.

## Critical path

```
MRM-58 terrain (Carlos's own task — blocks the largest subtree) → MRM-27 A* → MRM-29 state machine → MRM-34/35 enemies
MRM-11 event director → MRM-13 dialogue → MRM-62 event script → M1
```

If a session ends with nothing obviously to do, check whether MRM-58 (terrain blockout) has moved.

## Setup status — CLOSED 2026-08-20

Kickstart Part A ran and is now fully retired (per its own §A.6 instructions — deleted from `kickstart.md`, which now starts directly at Part B / operating rules):
- [x] `Docs/` and `Docs/Design/` populated from `Project MDs\` and `DesignContext MDs\`
- [x] `CLAUDE.md` written at repo root
- [x] `kickstart.md` moved into this folder (was at repo root); Part A setup section removed, file now reads "Setup completed 2026-08-20. This file is now operating rules only."
- [x] **Branch naming convention confirmed with Carlos:** use the Linear issue number (e.g. `MRM-22`). Linear's own **Branch format** setting (Settings → Integrations → GitHub) was changed from the default `username/identifier-title` to **`identifier`** so "Copy git branch name" now matches this exactly.
- [x] **Linear↔GitHub integration connected.** `MustardMasterSpark` org connected (all repos), personal GitHub account connected. No separate per-repo linking step needed — it works at the org level, and issue IDs in branches/commits/PRs auto-link.
- [x] **Unity MCP verified live.** Instance `MrMoonlight@87580c9df5a077ae`, Unity 6000.3.21f1, connected — confirmed via `mcpforunity://instances` in a session started after the Editor was already running.
- [x] **`Docs/`, `CLAUDE.md`, and the `kickstart.md` retirement edit are uncommitted, on purpose.** Kickstart's own A.3 checkbox said "committed to main," but the permanent rule in Part B (§B.4) says Claude never commits or pushes. Left staged for Carlos to commit via GitHub Desktop (commit message proposed in-session).

Everything from here on is normal per-issue work under Part B — see `kickstart.md` §B.2.

## What's explicitly out of scope for the demo

Procedural terrain, day/night cycle, manual save system, shrooms, crossbow, Arisaka, Ka-Bar, stone totems, priests, the dock, the flak tower, the radio station. These appear in the old GDD/pitch document but have no Linear issue — do not build them or prepare hooks for them.

## Still-open design questions (from `Docs/Design/00-INDEX.md`)

- Mine geometry: carved into island terrain vs. teleport to a separate non-Euclidean space — flagged for a Claude recommendation (this is one of the 6 Opus issues, MRM-60).
- "Munchies" multiplier — attached to the morphine stat in the source but named like the weed stat. Needs a word from Carlos.
