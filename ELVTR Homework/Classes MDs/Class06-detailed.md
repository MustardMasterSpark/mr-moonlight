# Class 6 — From Agent Output to Playable Game · Detailed Notes

Opening prompt: *"In S05, your agents generated content for your game. What is still sitting in a text file that your players cannot touch yet? This session closes that gap."*

## 1. What engines consume

> *"Your agent produces output. Your engine needs to consume it. The format question is simple: what does your engine read? Start there."*

| Engine | Ingestion |
|---|---|
| **Unity** | Expects **JSON mapped to ScriptableObjects or C# serializable classes. Strong typing is enforced at compile time.** |
| **Unreal** | Consumes **DataTables from CSV** inputs. Blueprint-accessible structs must match the column schema exactly. |
| **Browser** (Phaser, Three.js) | Consumes **JSON directly via fetch calls**, requiring UTF-8 clean outputs with **no BOM characters**. |

Format roles: **JSON** — the most universally consumable; ideal for dialogue trees, item databases, quest parameters, any hierarchical data. **CSV** — best for tabular data: enemy stats, loot tables, level configuration; maps directly to Unreal DataTables and Unity ScriptableObject arrays. **Plain text** — localization strings, UI copy, narrative barks; parsed line-by-line and indexed for runtime lookup.

> *"These format specs are reference material — you don't need to memorize them. When you need the integration code, describe what you need to your LLM and have it write it."*

## 2. Forcing structured outputs

| | |
|---|---|
| **The problem** | LLMs default to natural language prose. Conversational filler text will break any data parser downstream and crash the integration pipeline. |
| **The solution** | Explicit system-level instructions: *"Respond ONLY with valid JSON. No explanations. No markdown. No preamble."* Constrain the output schema completely. |
| **Validation layer** | Always follow structured output prompts with a downstream validator that checks schema compliance **before** the data is passed to the engine integration script. |

> *"Never assume an LLM will produce clean, parseable output without explicit constraints and a validation step."*

## 3. The automation workflow

**GENERATE → VALIDATE → REVIEW → IMPORT**

- **GENERATE** — the AI agent processes its prompt and emits structured output (JSON, CSV, or plain text) formatted per engine spec.
- **VALIDATE** — a translation script intercepts the output, validates the schema, sanitizes the data, and flags anything that doesn't conform.
- **REVIEW** — a developer or designer inspects flagged or newly generated content before it moves forward. *"This is where judgment calls happen — not every output that passes schema validation is actually good."*
- **IMPORT** — once reviewed and approved, content is written into the engine's asset folder and loaded into the project.

> *"A well-structured Generate → Validate → Review → Import pipeline keeps human judgment in the loop at the review stage, ensuring only approved content reaches the engine."*

**What happens when you skip review:** the slide's example — your agent generates 20 NPC descriptions, you load them all directly with no review, and a player finds the bad ones in the first 10 minutes.

## 4. File watchers — a note for later

> *"File watchers and batch importers are production infrastructure — useful at scale, but not what you need right now. At capstone stage, you need to know which folder to put your file in. That's it."*

When you are ready: a file watcher monitors the agent output directory and moves new files to a **staging** folder for review; a batch importer processes multiple files in one pass. **File watchers feed a staging directory, not the engine directly** — the human review step between detection and import is what keeps the pipeline trustworthy.

## 5. Your integration plan — three things to know

1. **The file** — what agent output do you need in your game? Name it and its format.
2. **The target** — where does that file need to land in your engine's project structure?
3. **The prompt** — *"I have [this file] in [this format]. I'm using [this engine]. Write me a loader that reads this file and creates [game objects] from it."*

> *"That prompt is your integration plan. The LLM writes the code. You review it and test it."*

## 6. The translation layer

> *"Your agent outputs JSON. Your engine reads JSON. So why do you need a translation layer? Because the agent's JSON schema and your engine's expected schema are almost never the same."*

Agent produces output in its format → translation script converts to your engine's format → validation checks the result before loading.

```python
import json, os

def process_agent_output(raw_output, target_dir):
    # Sanitize and parse
    data = json.loads(raw_output.strip())
    out_path = os.path.join(target_dir, "dialogue.json")
    with open(out_path, "w") as f:
        json.dump(data, f, indent=2)
    print(f"Written to engine: {out_path}")
```

## 7. Handling bad data

| | |
|---|---|
| **Detect malformed JSON** | Wrap all JSON parsing in a try/except block. A `json.JSONDecodeError` signals the LLM generated invalid syntax — **do not proceed to the engine directory.** |
| **Debug before you retry** | *"Automatic retry sounds clean, but in practice you need to understand why the output was bad before resubmitting. Was it the prompt? The schema? An edge case in the data? Diagnose first, adjust the prompt or schema, then regenerate deliberately. This debugging step is where the real learning happens."* |
| **Log & alert** | Every error event logged with the raw output preserved — an audit trail for debugging, and it helps identify recurring hallucination patterns. |

> *"Never let malformed LLM output reach the game engine. A single bad import can corrupt project assets or trigger a compilation failure."*

## 8. The Integration Checkpoint (pass/fail milestone)

| PASS | FAIL |
|---|---|
| Agent-generated content successfully loads into the engine via the automated conversion script with **zero compilation errors**. The content is live and playable. | The conversion script throws an error — typically malformed JSON, schema mismatch, or LLM hallucination. **The pipeline halts.** The developer diagnoses why the output was bad, adjusts the prompt or schema, then regenerates deliberately. |

> *"The Integration Checkpoint is non-negotiable. Content that fails this gate must never proceed to the engine — doing so risks crashing the project build."*

**No assignment is set by this session** — the deliverable is the checkpoint itself: agent output loading in your running game before S07.

---

## Mr. Moonlight application

**Directly relevant**
- **Unity's row is the spec:** JSON → **ScriptableObjects or C# serializable classes, strong typing at compile time.** This is the right shape for the dialogue, objective, system-message and localization data.
- **ScriptableObjects solve two problems at once.** They are the engine-native ingestion target *and* the cleanest implementation of the project's "no hardcoded values — all tunables in one findable place, editable in the inspector" requirement. One `MoonlightTunables` ScriptableObject asset, referenced by every system, with comments linking each field to its Linear issue.
- **Generate → Validate → Review → Import maps onto the developer's stated workflow**: Claude generates, a validator checks the schema, the developer reviews in Unity and tweaks, then it goes in. The "review" step is not a compromise here — it is the tandem model the project is built around.
- **"Diagnose first, don't blind-retry"** is a good rule to write into the kickstart doc for Claude Code.
- **Don't build a file watcher.** The slides say so. Manual placement is correct at this scale, and the WebGL build budget is a better use of the hours.

**Not relevant**
- Unreal DataTables, Phaser fetch, BOM handling.

**Watch out for**
- **WebGL specifically:** `Resources.Load` and runtime file I/O behave differently in a browser build than in the editor. Data that is authored as JSON/CSV should be **baked into ScriptableObjects at build time**, not read from disk at runtime. This is a real WebGL trap and it is not covered by the course. Flag it in the Project MDs.
- UTF-8 without BOM still matters for the WebGL build — Tracey's dialogue contains apostrophes and em dashes, and the localization plan includes Spanish and Russian.
