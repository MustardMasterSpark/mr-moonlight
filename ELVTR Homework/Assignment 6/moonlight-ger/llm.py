"""Thin wrapper around the raw Anthropic Messages API. Not a framework —
just the one HTTP call, response-text extraction, and cost math shared by
generator.py, evaluator.py and refiner.py so it isn't copy-pasted three
times.
"""

import os
from pathlib import Path

import anthropic

# Load ANTHROPIC_API_KEY from a local .env next to this file, if present and
# not already set. .env is git-ignored (see repo .gitignore) — never commit it.
if "ANTHROPIC_API_KEY" not in os.environ:
    env_path = Path(__file__).with_name(".env")
    if env_path.exists():
        for line in env_path.read_text(encoding="utf-8").splitlines():
            line = line.strip()
            if line and not line.startswith("#") and "=" in line:
                key, _, value = line.partition("=")
                os.environ.setdefault(key.strip(), value.strip())

# Whether a usable key/credential is actually present. Checked by run.py to
# decide live vs. offline mode, and used here to avoid constructing the SDK
# client (which raises immediately if unauthenticated) until it's needed.
HAS_API_KEY = bool(os.environ.get("ANTHROPIC_API_KEY") or os.environ.get("ANTHROPIC_AUTH_TOKEN"))

_client = None


def _get_client():
    global _client
    if _client is None:
        _client = anthropic.Anthropic()
    return _client


GENERATOR_MODEL = "claude-haiku-4-5"
REFINER_MODEL = "claude-haiku-4-5"
EVALUATOR_MODEL = "claude-sonnet-5"

# $ per 1M tokens. Source: Anthropic API pricing, current as of 2026-08-20.
PRICING = {
    "claude-sonnet-5": {"input": 2.00, "output": 10.00},
    "claude-haiku-4-5": {"input": 1.00, "output": 5.00},
}


def call(model: str, system: str, user: str, *, max_tokens: int = 1024) -> tuple[str, dict]:
    """Single non-streaming call. Returns (response_text, usage_dict)."""
    response = _get_client().messages.create(
        model=model,
        max_tokens=max_tokens,
        system=system,
        messages=[{"role": "user", "content": user}],
    )
    text = "".join(block.text for block in response.content if block.type == "text")
    usage = {
        "model": model,
        "input_tokens": response.usage.input_tokens,
        "output_tokens": response.usage.output_tokens,
        "cost_usd": cost(model, response.usage.input_tokens, response.usage.output_tokens),
    }
    return text.strip(), usage


def cost(model: str, input_tokens: int, output_tokens: int) -> float:
    rates = PRICING[model]
    return (input_tokens / 1_000_000) * rates["input"] + (output_tokens / 1_000_000) * rates["output"]
