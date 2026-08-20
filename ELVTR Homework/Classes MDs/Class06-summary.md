# Class 6 — From Agent Output to Playable Game · Executive Summary

**One line:** the plumbing session — how a file an agent wrote actually ends up loaded in your engine, and what to do when it's malformed.

## The workflow

**GENERATE → VALIDATE → REVIEW → IMPORT**

- **Generate** — the agent emits structured output (JSON/CSV/text) per engine spec
- **Validate** — a translation script intercepts, validates the schema, sanitizes, flags nonconformance
- **Review** — *a human looks at it.* **This step stays.** *"Not every output that passes schema validation is actually good."*
- **Import** — approved content is written into the engine's asset folder

## Forcing structured output

LLMs default to conversational prose, and prose breaks parsers. The fix is a system-level instruction plus a validator:

> *"Respond ONLY with valid JSON. No explanations. No markdown. No preamble."*

> *"Never assume an LLM will produce clean, parseable output without explicit constraints and a validation step."*

## The translation layer

Why you need one even when both sides speak JSON: **the agent's schema and your engine's schema are almost never the same.**

The prompt pattern the slides recommend: *"I have a JSON file in this format: [paste]. My engine expects this format: [paste]. Write me a Python script that converts one to the other and validates the result."*

## Handling bad data

Wrap parsing in try/except. On `JSONDecodeError`, **do not proceed to the engine directory**. And notably, the slides argue *against* blind auto-retry:

> *"Automatic retry sounds clean, but in practice you need to understand why the output was bad before resubmitting. Diagnose first, adjust the prompt or schema, then regenerate deliberately."*

## The Integration Checkpoint

A hard pass/fail milestone. Pass = agent content loads with zero compilation errors and is live and playable. Fail = the pipeline halts and you diagnose. *"Content that fails this gate must never proceed to the engine."*

## Takeaway for Mr. Moonlight

Unity's row in the format table: **JSON mapped to ScriptableObjects or C# serializable classes, strong typing enforced at compile time.** That is the shape your dialogue/objective data should take — and a ScriptableObject-based tunables asset is also the cleanest answer to your "no hardcoded values, all in one file" requirement.

Also worth stealing: the file-watcher slide says explicitly not to build one yet. *"At capstone stage, you need to know which folder to put your file in. That's it."*
