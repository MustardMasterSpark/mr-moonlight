# Prop Log

**Owner issue:** MRM-72 · **Wizard:** `Docs/3d-prop-pipeline-wizard.md`

One entry per finished prop, newest first. Written at the wizard's §9 write-back step, after
Carlos confirms the prop is done.

> **This file records what happened. The wizard records what to do differently.**
>
> If a lesson here would change how the *next* prop is made, it does not belong only here —
> **go edit the relevant step of the wizard.** A lesson that lives only in the log is a lesson
> the wizard will not apply. Both files get written, every prop. That is the whole mechanism.

---

## Entry template

Copy this. Keep entries short — the value is in the **Lessons** line, not the narrative.

```markdown
## <Prop name> — <YYYY-MM-DD>

| | |
|---|---|
| **Path** | 1 character / 2 static prop / 3 weapon / 4 special |
| **Source** | where it came from, and what state it arrived in |
| **Polycount** | as received → as shipped |
| **Resolution** | BaseColor / Normal |
| **Shipped to** | `Assets/_Project/Art/<Category>/<Prop>/` |
| **Prefab** | `Prop_<Name>.prefab` |
| **Manual steps** | which steps were 👤 rather than 🤖/🔧, and why |
| **Carlos's revisions** | what came back from the review gate |

**Lessons →** what cost time, and *whether it was written back into the wizard*.
If nothing was learned, write "nothing new — clean run." That is a real and good outcome.
```

---

## What to watch for across entries

Patterns only become visible once there are several. When adding an entry, glance at the
existing ones and ask:

- **Is the same manual step appearing repeatedly?** → §9.3 automation backlog entry
- **Is the same revision coming back from Carlos every time?** (scale, collider, gloss)
  → it should become a *default* in the wizard, not a correction
- **Has a "special case" now happened twice?** → it is a missing path, not a special case.
  Promote it (wizard §9.4)
- **Is a documented rule never firing?** → delete it. The wizard should get sharper, not longer

---

## Entries

*None yet. The first prop through the wizard writes the first entry here.*
