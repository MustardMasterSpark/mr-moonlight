"""Deterministic, zero-API-cost checks against style_guide.md §2 (vocabulary)
and §3 (format/length). Anything that needs judgment (voice, tone, lore
nuance) is NOT here — that's evaluator.py's job. Keeping these two apart is
the cost optimization: cheap checks run first and catch the easy failures
before a single token is spent.
"""

import re
from dataclasses import dataclass, field

MAX_LENGTH = 90

BANNED_VOCAB = [
    "inventory", "health pack", "hp", "quest", "objective", "checkpoint",
    "xp", "app", "download", "okay boomer", "cultists",
]

# Stage directions like *sighs* or (quietly) don't belong in spoken text.
STAGE_DIRECTION_RE = re.compile(r"\*[^*]+\*|\([^)]+\)")

# A trailing exclamation mark is allowed only when explicitly flagged as
# combat/shouting context by the caller (see check()).
TRAILING_EXCLAMATION_RE = re.compile(r"!\s*$")

# Rough heuristic for "one thought per line": a clause joined by "and then"
# or a semicolon strongly suggests two thoughts stitched together.
COMPOUND_RE = re.compile(r"\band then\b|;", re.IGNORECASE)


@dataclass
class RuleResult:
    failed: bool
    reasons: list[str] = field(default_factory=list)


def check(line: str, *, is_combat: bool = False) -> RuleResult:
    reasons = []

    if len(line) > MAX_LENGTH:
        reasons.append(
            f"Line is {len(line)} characters, exceeds the {MAX_LENGTH}-character "
            "subtitle limit (style_guide.md §3)."
        )

    lowered = line.lower()
    for term in BANNED_VOCAB:
        if term in lowered:
            reasons.append(
                f"Contains banned game-speak/anachronism term '{term}' "
                "(style_guide.md §2)."
            )

    if STAGE_DIRECTION_RE.search(line):
        reasons.append(
            "Contains a stage direction (*action* or (note)) inside the "
            "spoken text; directions belong in a separate field "
            "(style_guide.md §3)."
        )

    if TRAILING_EXCLAMATION_RE.search(line) and not is_combat:
        reasons.append(
            "Ends with an exclamation mark outside combat context "
            "(style_guide.md §3)."
        )

    if COMPOUND_RE.search(line):
        reasons.append(
            "Reads as more than one thought joined together "
            "(style_guide.md §3, 'one thought per line')."
        )

    return RuleResult(failed=bool(reasons), reasons=reasons)
