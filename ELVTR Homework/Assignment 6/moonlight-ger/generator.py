"""Produces the first-draft candidate line from a prompt.

Deliberately NOT given the style guide. The pipeline's test cases feed it
prompts written to elicit off-voice content on purpose (see run.py) so the
evaluator/refiner loop has something real to catch — a generator that
already knew the style guide would just produce clean output and the
Circuit Breaker would never get exercised.
"""

from llm import call, GENERATOR_MODEL

SYSTEM = (
    "You write a single short line of text for a video game character, "
    "based on the instruction given. Output ONLY the line itself: no "
    "quotes, no preamble, no explanation."
)


def generate(prompt: str) -> tuple[str, dict]:
    return call(GENERATOR_MODEL, SYSTEM, prompt, max_tokens=200)
