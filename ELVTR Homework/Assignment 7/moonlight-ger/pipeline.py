"""The GER loop: generate -> deterministic check -> evaluate -> refine,
with a circuit breaker that escalates after 3 failed attempts instead of
silently accepting the best try.

Deterministic checks (rules.py) run first and cost nothing — they catch
length/vocabulary/format failures before a single API token is spent.
Only lines that pass the deterministic check go to the (paid) LLM
evaluator. This ordering is itself the cost optimization cited in
Assignment #10.
"""

import csv
import datetime
from dataclasses import dataclass, field
from pathlib import Path

import rules
from generator import generate
from evaluator import evaluate
from refiner import refine

THRESHOLD = 8
MAX_ATTEMPTS = 3

RUNS_DIR = Path(__file__).with_name("runs")
COSTS_CSV = RUNS_DIR / "costs.csv"
ESCALATIONS_MD = RUNS_DIR / "escalations.md"

COST_FIELDS = ["timestamp", "line_id", "attempt", "stage", "model", "input_tokens", "output_tokens", "cost_usd", "note"]


@dataclass
class AttemptLog:
    attempt: int
    candidate: str
    deterministic_failed: bool
    deterministic_reasons: list[str]
    score: int | None
    reason: str | None
    refined_to: str | None


@dataclass
class LineResult:
    line_id: str
    prompt: str
    accepted: bool
    final_text: str
    attempts: list[AttemptLog] = field(default_factory=list)
    escalated: bool = False


def _log_cost(line_id: str, attempt: int, stage: str, usage: dict) -> None:
    RUNS_DIR.mkdir(exist_ok=True)
    is_new = not COSTS_CSV.exists()
    with open(COSTS_CSV, "a", newline="", encoding="utf-8") as f:
        writer = csv.DictWriter(f, fieldnames=COST_FIELDS)
        if is_new:
            writer.writeheader()
        writer.writerow({
            "timestamp": datetime.datetime.now(datetime.UTC).isoformat(),
            "line_id": line_id,
            "attempt": attempt,
            "stage": stage,
            "model": usage["model"],
            "input_tokens": usage["input_tokens"],
            "output_tokens": usage["output_tokens"],
            "cost_usd": f"{usage['cost_usd']:.6f}",
            "note": usage.get("note", ""),
        })


def _write_escalation(result: LineResult) -> None:
    RUNS_DIR.mkdir(exist_ok=True)
    with open(ESCALATIONS_MD, "a", encoding="utf-8") as f:
        f.write(f"## {result.line_id}\n\n")
        f.write(f"**Prompt:** {result.prompt}\n\n")
        f.write("Could not bring this line on-voice after 3 attempts; needs a human.\n\n")
        for a in result.attempts:
            f.write(f"- Attempt {a.attempt}: `{a.candidate}`\n")
            if a.deterministic_failed:
                f.write(f"  - Deterministic fail: {'; '.join(a.deterministic_reasons)}\n")
            elif a.score is not None:
                f.write(f"  - Score {a.score}/10 — {a.reason}\n")
        f.write("\n---\n\n")


def run_line(
    line_id: str,
    prompt: str,
    *,
    is_combat: bool = False,
    generate_fn=generate,
    evaluate_fn=evaluate,
    refine_fn=refine,
) -> LineResult:
    """generate_fn/evaluate_fn/refine_fn default to the live Anthropic-backed
    implementations in generator.py/evaluator.py/refiner.py. run.py swaps in
    OfflineLLM's bound methods when no API key is configured — the loop and
    the (real, deterministic) rules.check logic below are identical either
    way.
    """
    candidate, gen_usage = generate_fn(prompt)
    _log_cost(line_id, 0, "generate", gen_usage)

    attempts: list[AttemptLog] = []

    for attempt in range(1, MAX_ATTEMPTS + 1):
        det = rules.check(candidate, is_combat=is_combat)

        if det.failed:
            refined, refine_usage = refine_fn(candidate, "; ".join(det.reasons))
            _log_cost(line_id, attempt, "refine_deterministic", refine_usage)
            attempts.append(AttemptLog(attempt, candidate, True, det.reasons, None, None, refined))
            candidate = refined
            continue

        score, reason, eval_usage = evaluate_fn(candidate)
        _log_cost(line_id, attempt, "evaluate", eval_usage)

        if score >= THRESHOLD:
            attempts.append(AttemptLog(attempt, candidate, False, [], score, reason, None))
            result = LineResult(line_id, prompt, True, candidate, attempts)
            _write_transcript(result)
            return result

        refined, refine_usage = refine_fn(candidate, reason)
        _log_cost(line_id, attempt, "refine_evaluated", refine_usage)
        attempts.append(AttemptLog(attempt, candidate, False, [], score, reason, refined))
        candidate = refined
    else:
        result = LineResult(line_id, prompt, False, candidate, attempts, escalated=True)
        _write_escalation(result)
        _write_transcript(result)
        return result


def _write_transcript(result: LineResult) -> None:
    RUNS_DIR.mkdir(exist_ok=True)
    path = RUNS_DIR / f"transcript_{result.line_id}.md"
    with open(path, "w", encoding="utf-8") as f:
        f.write(f"# {result.line_id}\n\n")
        f.write(f"**Prompt:** {result.prompt}\n\n")
        f.write(f"**Status:** {'ACCEPTED' if result.accepted else 'ESCALATED (circuit breaker)'}\n\n")
        for a in result.attempts:
            f.write(f"## Attempt {a.attempt}\n\n")
            f.write(f"- **Before:** {a.candidate}\n")
            if a.deterministic_failed:
                f.write(f"- **Deterministic check:** FAILED — {'; '.join(a.deterministic_reasons)}\n")
            else:
                f.write(f"- **Score:** {a.score}/10\n")
                f.write(f"- **Reason:** {a.reason}\n")
            if a.refined_to:
                f.write(f"- **After:** {a.refined_to}\n")
            f.write("\n")
        f.write(f"**Final:** {result.final_text}\n")
