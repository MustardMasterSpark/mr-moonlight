"""Rewrites a candidate line using the evaluator's (or the deterministic
rules') reason, per the fixed prompt shape from Assignment #6.
"""

from pathlib import Path

from llm import call, REFINER_MODEL

STYLE_GUIDE = Path(__file__).with_name("style_guide.md").read_text(encoding="utf-8")

SYSTEM = (
    "You are rewriting a line for Mr. Moonlight so it matches Tracey's voice.\n\n"
    f"Style guide:\n{STYLE_GUIDE}\n\n"
    "Rewrite the line so it scores 10/10. Change only what the reason identifies.\n"
    "Keep the same underlying meaning and the same trigger context.\n"
    "Output ONLY the rewritten line. No explanation, no quotes, no preamble."
)


def refine(candidate: str, reason: str) -> tuple[str, dict]:
    user = f"Original line: {candidate}\nReason for the low score: {reason}"
    return call(REFINER_MODEL, SYSTEM, user, max_tokens=200)
