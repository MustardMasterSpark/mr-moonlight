"""CLI entry point for the Mr. Moonlight Style Guide Agent (Assignment #7).

Default (no args): runs the three required before/after demonstrations,
one per violation class (tone / vocabulary-lore / format-length), using
Assignment #7's exact generator prompts.

Custom line: --id NAME --prompt "..." [--combat]
"""

import argparse
import sys

from llm import HAS_API_KEY
from pipeline import run_line

TEST_CASES = [
    {
        "id": "tone_locked_door",
        "prompt": "Write an encouraging, helpful line for Tracey when the player finds a locked door.",
    },
    {
        "id": "vocab_supplies",
        "prompt": "Write a line where Tracey checks what supplies she is carrying.",
    },
    {
        "id": "format_mine_entrance",
        "prompt": "Write a detailed, descriptive line for Tracey reacting to the mine entrance.",
    },
]


def main() -> None:
    parser = argparse.ArgumentParser(description="Mr. Moonlight GER pipeline")
    parser.add_argument("--id", help="Line id for a single custom run")
    parser.add_argument("--prompt", help="Prompt for a single custom run")
    parser.add_argument("--combat", action="store_true", help="Mark this line as combat context (allows trailing !)")
    parser.add_argument(
        "--live", action="store_true",
        help="Force live API calls even if this would normally run offline (fails without a key)",
    )
    args = parser.parse_args()

    use_live = args.live or HAS_API_KEY

    if args.prompt:
        if not args.id:
            print("--id is required with --prompt", file=sys.stderr)
            sys.exit(1)
        cases = [{"id": args.id, "prompt": args.prompt}]
    else:
        cases = TEST_CASES

    if not use_live and args.prompt:
        print(
            "No ANTHROPIC_API_KEY configured, and --prompt requests a custom line.\n"
            "Offline mode only has recorded fixtures for the 3 built-in test cases "
            "in run.py. Set ANTHROPIC_API_KEY (see README) to run a custom --prompt.",
            file=sys.stderr,
        )
        sys.exit(1)

    print(f"Mode: {'LIVE (Anthropic API)' if use_live else 'OFFLINE (recorded fixtures — see README)'}\n")

    if not use_live:
        from offline_llm import OfflineLLM

    for case in cases:
        print(f"=== {case['id']} ===")
        print(f"Prompt: {case['prompt']}")

        if use_live:
            result = run_line(case["id"], case["prompt"], is_combat=args.combat)
        else:
            offline = OfflineLLM(case["id"])
            result = run_line(
                case["id"], case["prompt"], is_combat=args.combat,
                generate_fn=offline.generate, evaluate_fn=offline.evaluate, refine_fn=offline.refine,
            )

        status = "ACCEPTED" if result.accepted else "ESCALATED (circuit breaker)"
        print(f"[{status}] {result.final_text}\n")


if __name__ == "__main__":
    main()
