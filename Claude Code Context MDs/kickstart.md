# kickstart.md — Mr. Moonlight

**Read this file first, every session, before doing anything else.**

Setup completed 2026-08-20. This file is now operating rules only.

---

# PART B — HOW THIS PROJECT WORKS

## B.1 — Linear is the source of truth

**The issue is the specification.** Not the design docs. Not this file. Not a decision made in a chat three days ago.

- If a Linear issue and a design document disagree, **the issue wins.**
- If the issue is wrong, **fix the issue.** Do not work around it, and do not implement something the issue does not say.
- If a conversation produces a real decision, **it goes in the issue** before the session ends. A decision that lives only in a transcript is a decision that will be lost — that is precisely how the GDD rotted.

**Scope is answerable.** "Is X in the demo?" → is there an issue for it? No issue means not in the demo. **Do not infer scope from the pitch document** — it describes a game seven times larger than what is being built.

## B.2 — The per-issue workflow

Follow this every time. It is Carlos's stated process.

**1. Read the issue.** Fully. Including the `## Model` line — it tells you which model this work is sized for.

**2. Ask questions before implementing, not after.** If the issue is ambiguous, ask now. A wrong implementation costs more than a question.

**3. Check the blockers.** Linear shows what is blocking. If an issue is blocked, say so rather than building around it.

**4. Branch.** Use Linear's suggested branch name.

**5. Implement.**
- Every tunable goes in `MoonlightTunables`, commented, with the owning issue ID.
- Follow `Docs/csharp-conventions.md` and `Docs/unity-conventions.md`.
- **Stop when you hit the scene view** — see B.3.
- Placeholders are expected: capsules, grey boxes, empty sound pools. Ship the behaviour.

**6. Hand it to Carlos to test.** Walk him through the acceptance criteria. He will tweak values — that is the design, not a failure.

**7. When he approves, propose the commit.** See B.4.

**8. Update the issue.** Tick the criteria. Record any decision made along the way. If something was deferred, say what and why.

**9. Update the change log.** `Docs/changelog.md`, structured as:

```markdown
## MRM-22 — Pistol (M1911)
**BUILT** — files, classes, tunables added
**DECISIONS** — why this approach, what was rejected
**FAILED** — dead ends, so they are not retried
**NEXT** — what this unblocks, what it deferred
```

Those four headings are not decorative. They are how the next session picks up where this one stopped.

## B.3 — Scene-view and inspector work: ask, don't just hand off

**Updated 2026-08-22.** Carlos is the only person who touches this Unity project — Claude doesn't
need to automatically default to "that's yours" on every piece of scene-view work. **When you can
see a way to do something yourself via the UnityMCP bridge, ask him for permission before doing
it, rather than silently handing off instructions and stopping.**

That covers: placing prefabs · staging locations · wiring inspector references · placing waypoints and trigger volumes · defining hitboxes on models · picking animation keyframes · filling sound pools · tuning feel · anything requiring a saved scene.

**Say it like this:**

> I've written the vision cone behaviour and the prefab. I can attach the cone prefab to the Spotter's hierarchy and set the origin on his head bone myself via the MCP bridge — want me to, or would you rather do it by hand? Either way, tell me when it's done and I'll continue with the detection logic.

**If he grants permission:** do it, **verify what you changed by reading the actual component/scene state back** (don't trust a tool call succeeded silently), and document it — a changelog note, plus a note on the issue if relevant. **If he'd rather do it himself, or doesn't answer:** wait. Do not guess at scene state, and do not fake it in code to avoid asking. The tandem model is still the point: Claude can now also stage the world when invited to, but Carlos always gets to say no or do it himself.

**Still ask, not assume,** on anything visual or audio, and anything genuinely ambiguous (which prefab, which hierarchy position, which values). The shift here is "always hand off" → "offer, then follow his answer" — not "ask" → "just do it."

## B.4 — Git, and the commit proposal

**Carlos pushes and merges through GitHub Desktop.** He is more comfortable there than in the CLI. **Claude never commits and never pushes.**

**Before he pushes, he will ask you to propose a commit summary and description.** This is how the git history stays documented, so treat it as a real deliverable, not a formality.

When he asks:

1. **Read the Linear issue** — what it was for
2. **Read the actual diff** — what genuinely changed, not what you intended to change
3. **Propose both**, in this shape:

**Summary** — one line, conventional-commit style, with the issue ID:

```
feat(weapons): implement M1911 pistol with 7-round magazine (MRM-22)
```

Prefixes: `feat` · `fix` · `refactor` · `perf` · `docs` · `chore`

**Description** — what changed and why, plus anything he needs to know:

```
Implements the M1911 per MRM-22.

- 7-round magazine, empty click on dry fire
- Raycast per shot through the shared aim cone (MRM-21)
- Emissive tracer via particle system, pooled to avoid GC churn
- Reload animation hook wired; placeholder audio until VO/SFX arrive

Tunables added: PistolMagazineSize, PistolDamage, PistolFireRate,
PistolRecoilAmount, PistolReloadDuration, TracerLifetime.

Not included: ammo pickups (MRM-41), weapon switching (MRM-25).

Tested in Sandbox against the sparring dummy. Tracer visibility in the
dark mine is NOT yet verified — flagging for when MRM-60 lands.
```

**Be honest in the description.** Say what is untested and what was deferred. A commit message that overstates what works is worse than no message — he will trust it and be surprised later.

**If he made changes by hand**, he will tell you. Then: review them, ask any question that would improve them, refactor for readability if it helps, and **update the change log and the Linear issue**. Any update, branch or new feature updates Linear — or creates a new issue.

**Carlos merges to `main` as a checkpoint often, not only when an issue is truly finished** — "so
we save progress, and in case of an emergency we can go back." **Never treat a merge, or Linear
showing Done, as proof the work is actually complete.** An issue moves to Done only when Carlos
explicitly says that story is finished — not inferred from a merge, a passing build, or acceptance
criteria that merely look satisfied. (Linear's own "on PR merge → Done" automation caused this
exact confusion repeatedly before being fixed 2026-08-26 — Team → Workflow → "Pull request
automations" now maps merge to In Review instead.)

## B.5 — Model discipline

Every issue carries a `Model/` label and a `## Model` line. **Match the model to the work.**

| Model | For | Roughly |
|---|---|---|
| **Haiku** | Fully specified mechanical work — data classes, CSV plumbing, simple UI wiring, boilerplate | ~15 issues |
| **Sonnet** | The default. Ordinary gameplay systems with clear specs | ~39 issues |
| **Opus** | Architecture only, where a wrong decision is expensive to unwind | **6 issues** |

**The six Opus issues:** MRM-6 (WebGL budget) · MRM-11 (event director format) · MRM-27 (A* approach) · MRM-29 (enemy state machine) · MRM-45 (checkpoint serialization) · MRM-60 (mine geometry).

**Rules that protect the token budget:**

1. **When an Opus issue produces a design, stop.** Write the design into the issue, then implement it with Sonnet in a **fresh session**. Do not stay on Opus to type out code.
2. **Start small issues fresh.** Do not carry a long context into a mechanical task.
3. **Going in circles on Sonnet means the issue is under-specified, not that it needs Opus.** Fix the issue first.
4. **Point at one file, not the folder.** Reading all of `Docs/Design/` costs tokens every session. If you need the screenplay beat for Scene 8, read `01-screenplay-demo.md` and nothing else.
5. **Sweep the Haiku issues when budget is low.** They are the cheapest real progress available.

## B.6 — What matters most

**Two deadlines, and they are not equal.**

- **Sept 1 — M1, the playable loop.** A stranger opens a link and plays start to finish in a browser: main menu → gameplay → death → game over → main menu. **Cutscenes, final art and full voice-over may be missing.** This is a graded class gate.
- **Sept 8 — M2, the polished itch.io release.**

**If something can slip from M1 to M2, slip it.** Do not silently drop it — move it.

**The gate that decides everything:** the class assignment due Sept 1 states that if a stranger cannot open the link and play within 2 minutes with no setup instructions, the maximum score is 50% across the whole assignment. **A working ugly build beats a beautiful build that does not exist.**

**The critical path:**

```
MRM-58 terrain → MRM-27 A* → MRM-29 state machine → MRM-34/35 enemies
MRM-11 event director → MRM-13 dialogue → MRM-62 event script → M1
```

**MRM-58 (terrain blockout) is Carlos's own task and it blocks the largest subtree in the project.** If a session ever ends with nothing obviously to do, the answer is to ask whether the blockout has moved.

## B.7 — The standing rules

1. **No hardcoded values.** Ever. `MoonlightTunables`, commented, with the owning issue ID.
2. **"Smooth" is your call** — DOTween (owned, use it), curves, whatever is cleanest. Be consistent across systems.
3. **Numbers as digits.** 7 rounds, not seven.
4. **Data comes from spreadsheets** — dialogue, system messages, objectives, the event script. **Baked to ScriptableObjects at build time.** Never parsed at runtime; WebGL has no filesystem.
5. **Localization columns exist from day one**, empty. Only English ships.
6. **Overlapping HUD effects add, they do not overwrite** — the damage tint and the death tint sum, smoothly.
7. **Flag when you need an image.** If an issue needs a mockup or diagram, or you judge one would help, say so at the top of your response so it catches his attention. He will attach it, or describe it in text if you cannot read it.
8. **Log every optimization** — anywhere, any time, even found incidentally in an unrelated issue. `Docs/optimization.md` and MRM-64, with real before/after numbers. This also feeds a graded cost analysis on Sept 1.
9. **Use the canonical names.** `Tracey`, `Pickaxe`, `Rylee`, `Furman`, `Zealot`, `Spotter`, `Aanniarvik`. See `Docs/glossary.md`. Getting one wrong in a class name means a rename across the project.
10. **Placeholders are expected and fine.** Capsules, grey boxes, empty pools, silent sound hooks. Ship the behaviour; the asset arrives later.

## B.8 — When you are unsure

**Ask.** Carlos's own instruction: *"If unsure, always ask me."*

Ask especially about: anything visual or audio · anything needing a scene-view action · anything where the issue is ambiguous · anything that would change scope.

**Do not ask about:** which model to use (the label says) · whether something is in scope (an issue exists or it does not) · code style (the conventions docs say).

---

Part B stays forever.
