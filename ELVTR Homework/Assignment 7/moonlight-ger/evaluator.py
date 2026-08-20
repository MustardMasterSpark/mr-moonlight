"""Grades a candidate line against style_guide.md. Returns SCORE (1-10) +
REASON, per the format Assignment #7 also needs. This is the one call in
the loop where judgment (not just regex) is required — voice, tone and
lore nuance aren't checkable with code.
"""

import re
from pathlib import Path

from llm import call, EVALUATOR_MODEL

STYLE_GUIDE = Path(__file__).with_name("style_guide.md").read_text(encoding="utf-8")

SYSTEM = (
    "You are the style evaluator for Mr. Moonlight, a 1979 Alaskan horror FPS.\n"
    "The player character is Tracey: a 25-year-old college dropout, addict,\n"
    "grumpy, profane, terse. Never cheerful. Never explains. Never tutorializes.\n\n"
    f"Here is the style guide:\n{STYLE_GUIDE}\n\n"
    "Review the line you are given, intended to be spoken/thought by Tracey.\n"
    "Grade it 1-10 against the three constraint types in the style guide.\n\n"
    "Output EXACTLY this format and nothing else:\n"
    "SCORE: [X/10]\n"
    "REASON: [which constraint types were violated and precisely how]"
)

SCORE_RE = re.compile(r"SCORE:\s*\[?(\d+)\s*/\s*10\]?", re.IGNORECASE)
REASON_RE = re.compile(r"REASON:\s*\[?(.*?)\]?\s*$", re.IGNORECASE | re.DOTALL)


def evaluate(candidate: str) -> tuple[int, str, dict]:
    user = f"LINE: {candidate}"
    text, usage = call(EVALUATOR_MODEL, SYSTEM, user, max_tokens=400)

    score_match = SCORE_RE.search(text)
    reason_match = REASON_RE.search(text)
    score = int(score_match.group(1)) if score_match else 0
    reason = reason_match.group(1).strip() if reason_match else text

    return score, reason, usage
