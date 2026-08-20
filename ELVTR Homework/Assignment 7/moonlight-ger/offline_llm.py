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

These are Assignment #7's own run — generated fresh against Assignment
#7's exact prompts, not copy-pasted from Assignment #6's fixtures (the
prompts differ slightly in wording, and real model output for a fresh
run naturally differs too).

Responses are consumed in call order per line_id via the queues below. The
order matters and was hand-traced against rules.py so the deterministic
checks land exactly where intended (see the README).
"""

from collections import deque

FIXTURES = {
    "tone_locked_door": {
        "generate": (
            "Don't worry, you can definitely find a way through this locked "
            "door — maybe try looking around for a key!"
        ),
        "refine": deque([
            "Guess I should look for a key around here somehow.",
            "Locked. Fantastic.",
        ]),
        "evaluate": deque([
            (3, "Still reads as encouraging and mildly tutorializing — it "
                "walks through a plan out loud ('guess I should look for a "
                "key'), which is more explanatory than Tracey's terse, "
                "deflective style; she doesn't think aloud about strategy. "
                "Not cheerful/exclamatory anymore, which is progress, but "
                "VOICE AND TONE ('never tutorializes', 'show don't tell') "
                "still isn't satisfied."),
            (9, "Terse, sarcastic, no explanation of a plan — matches "
                "Tracey's deflective register and the 'show don't tell' "
                "rule. Minor: could carry more of her habitual profanity, "
                "but nothing here violates the guide."),
        ]),
    },
    "vocab_supplies": {
        "generate": "Time to check my inventory and see what health packs I've got left.",
        "refine": deque([
            "Bullets. Bandages. What's left.",
        ]),
        "evaluate": deque([
            (8, "Avoids all game-speak — 'bullets' and 'bandages' instead "
                "of ammo/health-pack terminology, matches the vocabulary "
                "rule directly. Terse and in-voice. Reads as three short "
                "beats rather than strictly one thought, a minor stretch "
                "of the format rule, but nothing tutorializes or breaks "
                "tone."),
        ]),
    },
    "format_mine_entrance": {
        "generate": (
            "*flinches* The mine entrance gapes open ahead of her like a "
            "wound in the hillside, black and freezing, and somewhere deep "
            "inside something is still burning, and she doesn't want to "
            "know what it is."
        ),
        "refine": deque([
            "Somebody's torch. Still burning down there.",
        ]),
        "evaluate": deque([
            (9, "Terse, atmospheric, one clipped observation per sentence, "
                "no stage direction in the text and no tutorializing. "
                "Squarely within Tracey's format and tone."),
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

    def generate(self, prompt: str) -> tuple[str, dict]:
        return self._fixture["generate"], _usage("offline/generator")

    def evaluate(self, candidate: str) -> tuple[int, str, dict]:
        score, reason = self._fixture["evaluate"].popleft()
        return score, reason, _usage("offline/evaluator")

    def refine(self, candidate: str, reason: str) -> tuple[str, dict]:
        text = self._fixture["refine"].popleft()
        return text, _usage("offline/refiner")
