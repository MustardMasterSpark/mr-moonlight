# ELVTR Assignments 6–10 — Working Guide

**Purpose.** One MD per remaining assignment, written for **Claude Code** to execute against the real project. Each says what is graded, what to build, and — most importantly — **what NOT to build**.

**The governing principle for all five:** the course grades *"The Game Itself"* at **50%** and everything else at 50% combined. Class 0 states it outright: *"A sophisticated pipeline that didn't ship a game is a 20."* **Every hour spent gold-plating an assignment is an hour not spent on Mr. Moonlight.** These MDs are deliberately written to get full marks with minimum effort, not to be impressive.

---

## Status board

| # | Title | Due (ET) | Status | Effort |
|---|---|---|---|---|
| **6** | Build a GER Pipeline | **18 Aug** | 🔴 **MANDATORY — OVERDUE** | ~2h |
| **7** | Style Guide Agent | **20 Aug, 11:59 PM ET** | 🔴 **MANDATORY — DUE TODAY** | ~1h *(if 6 is done first)* |
| **8** | Narrative Engine Prototype | 25 Aug | ⚪ **OPTIONAL — recommend skip** | 4–6h |
| **9** | Adversarial QA Agent | 27 Aug | ⚪ **OPTIONAL — recommend skip** | 4–6h |
| **10** | Complete AI Dev Pipeline | **1 Sept** | 🔴 **MANDATORY — the big one** | ~3h + a working build |

**11:59 PM ET = 9:59 PM Mexico City.** Not midnight your time.

---

## The single most important thing on this page

**#6 and #7 are the same architecture.** Both are Generator → Evaluator → Refiner loops. #6 additionally wants a **Circuit Breaker**; #7 additionally wants **SCORE + REASON** output and **3 before/after demonstrations of different violation classes**.

> **Build ONE pipeline. Submit it TWICE with different ReadMes.**

That is not a shortcut around the rubric — both rubrics are satisfied honestly by the same code, and #7's brief explicitly permits deriving from *"prior assignments."* You are not building two things tonight.

**Order of operations tonight:**
1. Build the pipeline (Assignment06.md) — ~2 hours
2. Write ReadMe A + the Pre-Build Declaration → **submit #6**
3. Run the three before/after demos and write ReadMe B → **submit #7**

---

## Skip #8 and #9. The course tells you to.

Assignment #9's own brief says:

> *"If your game isn't testable by an adversarial agent yet, you should spend this time working on your capstone project instead."*

Assignment #8 says it is **standalone** and *"does not need to connect to your capstone game."*

Both are optional, both cost 4–6 hours, and on 25 and 27 August you will be days from the itch.io release. **That is ~10 hours back.** The MDs for both exist in this folder, marked clearly, in case you change your mind — but the recommendation is to skip them and put the time into MRM-62 (the event script) instead.

---

## Start logging API costs TODAY

Assignment #10's rubric demands:

> *"Total actual run cost — must be calculated from the actual content generation run, not a hypothesis."*

**You cannot reconstruct this afterwards.** The runs you do tonight for #6 and #7 *are* the pipeline you will report on for #10. Log them:

- Tokens in / tokens out per run
- Number of API calls
- Which model
- Wall-clock time

The pipeline in Assignment06.md writes a `runs/costs.csv` for exactly this reason. Do not disable it.

**Also for #10:** record a **screen capture of the pipeline running** at least once. Video evidence is required for the Pipeline-to-Game Connection criterion, worth **3 points**. Two minutes of screen recording tonight saves you a scramble on 1 September.

---

## What every one of these assignments actually rewards

Read across all five rubrics and the same three things appear every time:

1. **Name your game and be specific.** Every assignment has an anti-slop gate that zeroes generic submissions. Say "Mr. Moonlight", say "Tracey", say "Aanniarvik", say "Zealot". Quote your own documents.
2. **Show, don't claim.** *"The correction is shown, not just claimed."* Paste the actual before, the actual evaluator output, the actual after.
3. **Be honest about what is manual.** Assignment #10: *"Directness and accuracy are rewarded; architectural sophistication is not."* Partial credit is explicitly available for a documented manual step. Do not pretend to full automation.
