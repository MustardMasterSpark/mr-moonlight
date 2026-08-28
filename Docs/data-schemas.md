# Data Schemas — the spreadsheet templates

Carlos's stated preference, from the working agreements:

> *"The system message system, the dialog system, and the event system should take their inputs from some kind of text file. It could be a spreadsheet or a line text file. I have preference for spreadsheets since it leaves less possibility for mistakes."*

These are the templates. **Claude provides them empty, or pre-populated where the content already exists.**

**Every sheet ships with empty `text_es` and `text_ru` columns from day one.** The demo is English only, but retrofitting a localization column into a populated sheet is worse than carrying two empty columns.

**All of these are baked to ScriptableObjects at build time.** CSV never ships.

> **Citation corrected 2026-08-27.** This line used to cite `webgl-constraints.md`, which is
> **historical** — WebGL was dropped on 2026-08-25. **The rule itself is unchanged**: runtime CSV
> parsing is fragile regardless of platform, and baking catches malformed rows in the editor where
> they can be fixed. Current reasoning lives in `Docs/pc-build-target.md` §2.

---

## 1. `dialogue.csv` — MRM-13

**Pre-populate this.** All ~250 demo lines already exist with IDs and performance direction in `Outputs/DesignContext MDs/01-screenplay-demo.md`. Carlos should never type them.

| Column | Type | Notes |
|---|---|---|
| `line_id` | string | `D-<scene>-<nnn>` e.g. `D-08-043`. **Primary key.** Also the VO filename stem |
| `speaker` | enum | `Tracey` · `Rylee` · `Holly` · `Vernon` · `Scott` · `Shannon` |
| `scene` | int | 1–10. For filtering while authoring |
| `text_en` | string | The subtitle. **Max 90 characters.** Plain, no stage directions, no speaker name |
| `text_es` | string | *(empty)* |
| `text_ru` | string | *(empty)* |
| `audio_file` | string | `VO_D-08-043`. Empty until the actor delivers |
| `duration_override` | float | Seconds. Used only when `audio_file` is empty. Blank → default 2.0 |
| `radio` | bool | TRUE routes through the radio filter bus |
| `direction` | string | Performance notes for the VO session. **Never displayed in game** |

**Example rows:**

```csv
line_id,speaker,scene,text_en,text_es,text_ru,audio_file,duration_override,radio,direction
D-01-003,Tracey,1,"All right, all right already!",,,,,FALSE,"Snapping awake into irritation"
D-08-011,Vernon,8,"WHAT HAVE YOU DONE?!",,,,,FALSE,"Full volume and it costs him. The shout should audibly tear something."
D-03-004,Rylee,3,"Calm down girl! Good morning to you too!",,,,,TRUE,"Amused, unbothered. Voice furred with sleep."
```

**Radio-flagged lines:** all of Rylee in Scene 3, Scott and Shannon in Scene 8, Vernon in Scenes 9–10.

**Validation the baker must do:**
- `line_id` unique
- `text_en` ≤ 90 characters — **warn, do not fail**, so authoring is not blocked
- `speaker` in the enum
- `audio_file` exists in the project, or is blank

---

## 2. `system_messages.csv` — MRM-14

Blue text, no audio.

| Column | Type | Notes |
|---|---|---|
| `msg_id` | string | `SYS-001` |
| `text_en` / `text_es` / `text_ru` | string | Blue on-screen text |
| `duration` | float | Seconds. Blank → tunables default |
| `notes` | string | Author's note, not displayed |

---

## 3. `objectives.csv` — MRM-14

**Pre-populate this too** — all 17 are listed in the screenplay MD.

| Column | Type | Notes |
|---|---|---|
| `obj_id` | string | `OBJ-01` |
| `order` | int | 1–17 |
| `text_en` / `text_es` / `text_ru` | string | Shown in the system message channel and the pause menu |
| `scene` | int | Which scene it belongs to |

```csv
obj_id,order,text_en,text_es,text_ru,scene
OBJ-01,1,"Turn off the alarm",,,1
OBJ-02,2,"Find water",,,1
OBJ-03,3,"Find your boots",,,2
OBJ-05,5,"Get the compass from Will's tent",,,4
OBJ-11,11,"Find Scott",,,9
OBJ-16,16,"Kill the Furman",,,10
```

---

## 4. `event_script.csv` — MRM-11, MRM-62

**The most important file in the project.** The level runs from this.

> **The schema below is a starting proposal.** MRM-11 requires Claude to propose the final format and **stop for Carlos's approval before implementing.** Do not treat this as settled.

| Column | Type | Notes |
|---|---|---|
| `step_id` | string | `S01-010`. Scene-prefixed, gaps of 10 so rows can be inserted without renumbering |
| `scene` | int | 1–10 |
| `verb` | enum | See the verb table below |
| `target` | string | What it acts on — line ID, objective ID, spawn point, layer name, item name |
| `params` | string | `key=value;key=value`. Verb-specific |
| `wait` | enum | `none` · `line_end` · `trigger` · `pickup` · `kill_group` · `timer` |
| `wait_param` | string | Trigger volume name, item ID, group ID, or seconds |
| `timeout` | float | **Seconds before the wait gives up and moves on.** Blank = no timeout, which **must** be justified in `notes` |
| `notes` | string | Author's note |

### Verbs

| Verb | `target` | `params` |
|---|---|---|
| `dialogue` | line_id | `wait_for_end=true` |
| `system_message` | msg_id | `duration=3` |
| `objective` | obj_id | |
| `spawn` | prefab name | `point=SpawnA;count=3;group=wolves_glade` |
| `sound` | clip or pool | `volume=0.8` |
| `sound_layer` | layer name | `state=on;fade=2.0` |
| `vfx` | effect name | `intensity=1.0;duration=5` |
| `lighting` | skybox name or blank | `gi=0.4;fog=0.2;duration=8` |
| `cutscene_begin` | | |
| `cutscene_end` | | |
| `checkpoint` | | |
| `grant` | item / weapon / capability | `amount=2` |
| `stat` | stat name | `op=lock;value=15` · `op=set;value=100` · `op=unlock` |
| `teleport` | destination | mine entrance/exit, if MRM-60 picks the teleport option |

### Worked example — Scene 1 opening

```csv
step_id,scene,verb,target,params,wait,wait_param,timeout,notes
S01-010,1,vfx,cinematic_blur,intensity=1.0;focus=WristWatch,none,,,Opening blur, watch in focus
S01-020,1,sound,SFX_WatchAlarm,loop=true,none,,,
S01-030,1,dialogue,D-01-001,wait_for_end=true,line_end,,5,
S01-040,1,dialogue,D-01-002,wait_for_end=true,line_end,,5,
S01-050,1,objective,OBJ-01,,trigger,,,Player must turn off the alarm
S01-060,1,stat,stamina,op=lock;value=15,none,,,Locked low until she drinks
S01-070,1,vfx,vomit,,none,,,Takes control, returns it
S01-080,1,vfx,cinematic_blur,intensity=0.6;focus=Canteen,none,,,Blur eases, focus moves to the water
S01-090,1,objective,OBJ-02,,pickup,Canteen,,Find water
S01-100,1,stat,stamina,op=unlock,none,,,
S01-110,1,checkpoint,,,none,,,
```

### The timeout column is not optional

**Every `wait` that is not `line_end` needs either a `timeout` or a written justification in `notes`.**

This is the single most likely way the demo breaks in front of a stranger: a `wait_for` that never resolves because the player walked away, died, or did things out of order. The Assignment #10 gate is *"a stranger plays it within 2 minutes"* — a softlock fails that gate outright.

**Make the baker warn on any blank `timeout` with a blank `notes`.**

---

## 5. `sound_pools.csv` — MRM-38, MRM-39

Not strictly required — pools can be ScriptableObjects edited in the inspector. But a sheet listing every pool the game needs makes the **"what do I still have to record"** question answerable at a glance, and Carlos will ask it often.

| Column | Notes |
|---|---|
| `pool_id` | `FOOT_Boots_Leaves`, `VOX_Zealot_Pain`, `AMB_Tree` |
| `category` | footstep · vocalisation · ambient · weapon · UI · damage |
| `owner` | Which prefab or system uses it |
| `layer` | island · cavern · mine · chapel |
| `pitch_min` / `pitch_max` | |
| `volume` | |
| `detection_probability` | Player sounds only — read by enemy hearing |
| `clip_count` | **0 until Carlos fills it. This column is the to-do list.** |
| `notes` | |

---

## The baker

One editor script, one menu item: **`MrMoonlight → Bake Data`**.

It must:
1. Read every CSV in `Assets/_Project/Data/CSV/`
2. Validate — report **every** error at once with row numbers, not the first one and stop
3. Write ScriptableObjects to `Assets/_Project/Data/Baked/`
4. Log a summary: rows read, warnings, errors

**Carlos will run this constantly.** Slow or unclear errors here cost more time than any other tool in the project. Make the error messages say the row number and what is wrong, in plain language.
