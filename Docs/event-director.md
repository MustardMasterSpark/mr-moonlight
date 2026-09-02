# Event Director

**MRM-11.** The thing that runs a level from start to finish: objectives, messages, spawns,
waits, and the two endings. Carlos writes the level in a text file; the director reads it.

Script file: `Assets/_Project/Data/Events/IslandEvents.txt`
Code: `Assets/_Project/Code/Runtime/Events/`

---

## The one rule

> **Every line fires and the director moves straight to the next one.
> The only line that ever blocks is `wait`.**

That is the whole flow model. Scan the left column for `wait` and you know exactly where the
level pauses. Nothing else has hidden blocking behaviour.

This is the deliberate correction to the deprecated SLDD (`Docs/SLDD (deprecated)/`), which gave
every event type its own `Blocking: T/F` parameter, never enforced any of it, and forced a fixed
list of parameters onto every line so that a real script was 80% `N/A`. Here you **write only
what the line actually needs** — arguments are named, order-free, and every one is optional with
a documented default.

---

## Grammar

```
# a comment, to the end of the line

[sequence_name]                    a sequence header, alone on its line

verb  "positional value"  key=value  key="value with spaces"
```

* One event per line. A line never wraps.
* `[main]` runs automatically when the level starts.
* Lines written before any header belong to an implicit `[main]`, so a file with no headers at
  all is still a valid script.
* Quote any value containing spaces. Quotes are stripped.
* A line has **at most one** positional value, and it must come immediately after the verb.
  Everything else is `key=value`. A stray unquoted word is an error, not a silent swallow.

### Sequences

A script is one or more named blocks. `[main]` is the level. Anything else is started by a `run`
line, or by an **Event Zone** in the world naming it. Several can run at once — that is how an
optional ambush or a background beat happens without threading it through the main line.

---

## Verbs

### Flow

| Line | What it does |
|---|---|
| `wait seconds=N` | Pause N seconds. |
| `wait objective=<id>` | Pause until that objective completes. **Waits forever by design.** |
| `wait kills=N [kind=Spotter]` | Pause until N more enemies die, counting from now. **Forever by design.** |
| `wait group=<name>` | Pause until every enemy in a spawned group is dead. **Forever by design.** |
| `wait zone=<name>` | Pause until the player enters that Event Zone. Times out. |
| `wait signal=<name>` | Pause until that signal is raised. Times out. |
| `wait sequence=<name>` | Pause until that sequence finishes. Times out. |
| `run <sequence>` | Start another sequence alongside this one. Never blocks. |
| `stop [sequence]` | End this sequence, or another one by name. |
| `log "text"` | Print to the console. Costs nothing; worth its weight when a sequence does not fire. |

Any `wait` accepts `timeout=N`. `timeout=0` means "wait forever", explicitly.

### Text and objectives

| Line | What it does |
|---|---|
| `message "text" [for=seconds] [color=#RRGGBB]` | Centre-bottom system message. |
| `objective <id> text="..." [kills=N] [kind=Spotter] [for=seconds] [announce=false] [byplayer=true]` | Sets **and** announces an objective. |
| `complete <id>` | Finishes an objective that is not a kill count. |

`objective` does both halves in one line on purpose: the words the player reads and the words the
pause menu will hold can then never drift apart.

* `kills=` makes it a kill objective, which completes itself.
* `kind=` limits which enemy counts (`Spotter`, `Zealot`, `Wolf`, `Furman`, `Wendigo`). Leave it
  off to count every enemy.
* `byplayer=true` counts only kills the player landed. Off by default — a Spotter shot by another
  Spotter still leaves one fewer old timer on the island, and making the player prove authorship
  reads as the objective being broken.
* An objective without `kills=` waits for a `complete` line.

### World

| Line | What it does |
|---|---|
| `spawn [kind] at=<point> [count=N] [chase=false] [group=name]` | Spawn enemies at a named Enemy Spawn Point. |
| `signal <name>` | Raise a named signal. |

`chase` defaults to **true**: enemies spawned by a script line are almost always spawned *because*
of the player, and one standing around waiting to be noticed reads as broken.

`group=` names the wave so a later `wait group=<name>` can hold until all of them are dead.

The `kind` positional on `spawn` is a **cross-check, not a selector** — the prefab lives on the
spawn point, because a text file cannot hold an asset reference. Naming the kind only asks the
director to complain if the point has been re-pointed at something else.

### Endings

| Line | What it does |
|---|---|
| `win "text"` | End the level as a victory: pause, show the end panel with that headline. |
| `lose "text"` | End the level as a **scripted** failure. |

The player simply dying does **not** come through `lose`. That is MRM-17's `DeathSequence`, which
has a fall-and-scream sequence to run first and lands on the same panel at the end of it.

### Reserved, not implemented yet

These verbs parse, validate, and log one warning naming the issue that owes them. The names are
claimed now so a level written today keeps working when the system behind it lands.

`dialogue` (MRM-13) · `sound` / `music` (MRM-15) · `vfx` (MRM-53/57) · `lighting` (MRM-47) ·
`cutscene` (MRM-11) · `checkpoint` (MRM-45) · `grant` (MRM-41/42) · `stat` (MRM-12)

### Custom verbs

A **custom verb starts with `!`** and is a single hand-written moment belonging to one point in
one level: `!seiko_alarm_off`, `!vernon_distraction phase=2`. The prefix does two jobs — it tells
you at a glance which lines are bespoke, and it guarantees a one-off can never collide with a
generic verb added later.

Ask for one when a moment needs real *behaviour*. It is one small class plus one line in
`EventVerbRegistry.cs`.

**Before asking, check whether `signal` already covers it.** If the moment is pure scene wiring
with no logic — enable an object, play a sound, open a door — drop an **Event Signal Receiver**
on it, name it, wire its UnityEvent in the inspector, and write `signal <that name>`. No code.

---

## Scene components

| Component | What it is for |
|---|---|
| **Event Director** | Runs the script. One per scene. Holds the TextAsset and the scene references. |
| **Objective Tracker** | Objective state and kill counting. Sits next to the director. |
| **System Message UI** | The centre-bottom text channel. |
| **Enemy Spawn Point** | A named place `spawn at=` can put enemies. Holds the prefab, the scatter radius and the spacing. |
| **Event Zone** | A trigger collider. Entering it raises a signal named after the zone, and can start a sequence directly. |
| **Event Signal Receiver** | Named UnityEvent target for `signal`. The no-code escape hatch. |

Names are case-insensitive and default to the GameObject's own name when the field is left empty.

---

## Deadlock safety

MRM-11 calls this out as *"the most likely way the demo breaks in front of a stranger"*. The
policy is per-condition, because the two kinds of wait fail in opposite directions.

**World waits** — `zone`, `signal`, `sequence` — can hang on a mis-typed name or a volume the
player squeezed past, and nothing on screen would say so. They time out after
`MoonlightTunables.EventWaitDefaultTimeout` (180s), log an error naming the file and line, and
carry on.

**Progress waits** — `objective`, `kills`, `group` — wait forever, deliberately. They resolve only
when the player does the thing, so a timeout would hand out a win nobody earned, which is a worse
failure than a pause. They cannot hang on a typo either: an unknown objective id is a load-time
error, not a silent stall.

There is a third safeguard: **signals latch**. Once raised, a signal stays raised, and a `wait` on
an already-raised signal returns immediately. Without that, the player walking through a trigger
volume one second before the director reaches the line that waits on it hangs the level forever.
The cost is that a signal cannot be waited on twice — raise a second, differently-named one.

---

## Authoring loop

1. Edit `Assets/_Project/Data/Events/IslandEvents.txt` in any text editor.
2. **Tools → Mr. Moonlight → Validate Event Scripts** — parses every script and reports grammar
   errors, unknown verbs, missing arguments and dangling sequence names, without entering play
   mode. Double-click a console line to open the file.
3. Press play. The director validates again on load, and the Event Director's inspector shows the
   running sequences and live objective progress while the game runs.

**Tools → Mr. Moonlight → Print Event Verb Reference** dumps the current verb list to the console,
which is always accurate even if this document has drifted.

Nothing needs rebuilding and no C# is touched. The script is a real text file, committed like
source; Unity bakes it into the build as a TextAsset, so MRM-11's "no runtime file reads, kept in
version control" holds without a separate bake step.

**Numbers inside the script are level content, not tunables.** The no-hardcoded-values rule points
at code, and this file *is* the data it points at. Durations and colours that should be consistent
across the whole game — default message duration, the message colour, the wait timeout — do live in
`MoonlightTunables`.

---

## Current state (2026-09-02)

`IslandEvents.txt` runs the demo loop:

```
wait       seconds=1
objective  kill_spotters  text="Homework: Kill 20 old timers"  kills=20  kind=Spotter  for=8
wait       objective=kill_spotters
win        "Good boy"
```

**51 Spotters are placed on the island**, under an `Enemies` holder in the hierarchy — 50 scattered
by script plus the one that was already hand-placed. Every one was validated with
`NavMesh.CalculatePath` from the player spawn and rejected unless the path came back
`PathComplete`, which does two jobs at once: it keeps them all on the main island (the small island
and the other disconnected NavMesh components are excluded automatically), and it guarantees the
kill-20 objective is actually completable. Nothing spawns within 90 m of the player start; the
closest is 106 m, the farthest 481 m, with 30 m minimum spacing between them.

Measured at 152–156 FPS with all 51 alive, both at the player spawn and standing in the densest
cluster, so the count is not a performance problem on this machine.

`spawn` and `wait zone=` are built and tested but **not called** — the Spotters above are hand-placed,
not director-spawned. Commented reference examples for both sit at the bottom of the script file.

### Known gaps

* **A flare on top of 51 Spotters is untested.** `SpotterFlareCall` summons a wave when a Spotter is
  isolated, and `SpotterPanicCall` another when one is hurt. With this many on the island the
  reinforcement maths has never been exercised at scale. Watch the enemy count during a real
  playthrough before assuming it is fine.
* **Each Spotter carries a shadow-casting point light** (the hand lamp). 52 in the scene now. URP
  culls them by distance so the cost is bounded, but the console still logs
  *"Reduced additional punctual light shadows resolution…"* whenever several are close together.
  That warning is URP lowering shadow resolution to fit its atlas, not an error — if the lamps ever
  look soft in a crowd, that is why.
* **The objective counts every Spotter death, not just the player's** (`byplayer` defaults off), so
  friendly fire between Spotters counts toward the 20.
* **URP now logs an error, not a warning:** *"Too many additional punctual lights shadows to look
  good, URP removed 50 shadow maps."* Caused by this issue's 51 Spotters. URP is handling it — it
  drops 50 of the 51 lamp shadow maps and renders fine at 152-156 FPS — but a red line every play
  session will eventually hide a real one. **The one-field fix is `Lamp Light` → Shadow Type →
  `No Shadows` on `Enemy_Spotter.prefab`**, or Soft → Hard to halve the atlas cost. Left alone
  deliberately: which lamps cast shadows is an art call, and 50 of them already effectively don't.
  Carlos's decision.
* **HAZE fog is currently OFF in the shared Volume profile** (`HazeGlobalFogVolumeComponent.active
  = false`). Pre-existing, not changed by this work — but worth knowing before judging the look.
  `SceneEffectsToggle` has a *"Restore Ship Defaults (Fog + CRT On)"* context menu, and **F6** now
  toggles it at runtime.
