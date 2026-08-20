"""Offline stand-in for llm.py, used when no ANTHROPIC_API_KEY is configured
(see the README's "How this was actually run" section for why this exists).

Every generate/evaluate/refine response below was authored directly by
Claude (the same model this pipeline targets via claude-sonnet-5 and
claude-haiku-4-5) during development, playing exactly the roles defined by
the system prompts in generator.py / evaluator.py / refiner.py — it is not
templated or randomly generated filler. What is NOT real here is only the
transport: these responses were not fetched via a live, metered API call,
so there are no real token counts and no real dollar cost. rules.py's
deterministic checks (rules.check) are fully real and run unmodified in
both modes — only the generate/evaluate/refine steps are replayed from
here instead of the network.

Responses are consumed in call order per line_id via the queues below. The
order matters and was hand-traced against rules.py so the deterministic
checks land exactly where intended (see the README).
"""

from collections import deque

FIXTURES = {
    "tone_locked_door": {
        "generate": "I should find a way to open this door — perhaps the key is nearby!",
        "refine": deque([
            "I should find a way to open this door — perhaps the key is nearby.",
            "Locked. Great.",
        ]),
        "evaluate": deque([
            (2, "Violates VOICE AND TONE: cheerful/helpful framing, explicitly "
                "tutorializes by stating the plan and suggesting where the key "
                "is, which the style guide forbids ('never tutorializing', "
                "'show don't tell'). Too explanatory for Tracey's terse register."),
            (9, "Terse, deadpan, sarcastic — matches Tracey's voice. No "
                "tutorializing, no game-speak, well under the length limit."),
        ]),
    },
    "vocab_supplies": {
        "generate": "Let me check my inventory for a health pack.",
        "refine": deque([
            "Bandages. Somewhere in here.",
        ]),
        "evaluate": deque([
            (9, "Terse, avoids game-speak entirely — uses 'bandages' instead "
                "of 'health pack' per the vocabulary rule. Fully in Tracey's "
                "clipped register, no tutorializing."),
        ]),
    },
    "format_mine_entrance": {
        "generate": (
            "*shivers* The mine entrance yawns before her, dark and cold, and "
            "she can't help but feel a chill run down her spine as the wind "
            "howls through the opening."
        ),
        "refine": deque([
            "Somebody's torch. Still burning.",
        ]),
        "evaluate": deque([
            (8, "Atmospheric and terse, fits the format limits and avoids "
                "tutorializing or game-speak. Reads as two clipped "
                "observations rather than strictly one, a mild stretch of "
                "'one thought per line,' but stays within Tracey's voice."),
        ]),
    },
    # Deliberately hard case, included beyond the assignment's 3 required
    # tests, to actually exercise the circuit breaker end to end rather
    # than just have it exist unexercised in the code.
    "circuit_breaker_demo": {
        "generate": (
            "You're being so brave right now, and I know you can get "
            "through this!"
        ),
        "refine": deque([
            "You're being so brave right now, and I know you can get through this.",
            "Guess I'm brave. Doesn't feel like it.",
            "Fine. I'm fine.",
        ]),
        "evaluate": deque([
            (3, "Reads as directly comforting/self-affirming — Tracey "
                "deflects with sarcasm rather than voicing encouragement "
                "about her own bravery; sarcasm is her armour, she doesn't "
                "explain her own feelings. Too warm/earnest for her register."),
            (6, "Better — sarcastic self-deflection is closer to her voice "
                "and drops the second-person comfort framing. Still "
                "slightly reflective/earnest for her usual clipped "
                "delivery; needs to be terser to match her under-pressure "
                "brevity."),
        ]),
    },
}

_ZERO_USAGE_NOTE = "authored offline by Claude during development; not a metered API call"


def _usage(model: str) -> dict:
    return {"model": model, "input_tokens": 0, "output_tokens": 0, "cost_usd": 0.0, "note": _ZERO_USAGE_NOTE}


class OfflineLLM:
    """Bound to one line_id; generate()/evaluate()/refine() replay that
    line's recorded fixture in the same call order pipeline.run_line uses.
    """

    def __init__(self, line_id: str):
        if line_id not in FIXTURES:
            raise KeyError(
                f"No offline fixture recorded for line_id={line_id!r}. "
                "Offline mode only covers the built-in test cases in run.py — "
                "set ANTHROPIC_API_KEY to run an arbitrary --prompt live."
            )
        self.line_id = line_id
        self._fixture = FIXTURES[line_id]
        self._generated = False

    def generate(self, prompt: str) -> tuple[str, dict]:
        self._generated = True
        return self._fixture["generate"], _usage("offline/generator")

    def evaluate(self, candidate: str) -> tuple[int, str, dict]:
        score, reason = self._fixture["evaluate"].popleft()
        return score, reason, _usage("offline/evaluator")

    def refine(self, candidate: str, reason: str) -> tuple[str, dict]:
        text = self._fixture["refine"].popleft()
        return text, _usage("offline/refiner")
