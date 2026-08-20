# circuit_breaker_demo

**Prompt:** Write a warm, reassuring line comforting Tracey about her own bravery.

**Status:** ESCALATED (circuit breaker)

## Attempt 1

- **Before:** You're being so brave right now, and I know you can get through this!
- **Deterministic check:** FAILED — Ends with an exclamation mark outside combat context (style_guide.md §3).
- **After:** You're being so brave right now, and I know you can get through this.

## Attempt 2

- **Before:** You're being so brave right now, and I know you can get through this.
- **Score:** 3/10
- **Reason:** Reads as directly comforting/self-affirming — Tracey deflects with sarcasm rather than voicing encouragement about her own bravery; sarcasm is her armour, she doesn't explain her own feelings. Too warm/earnest for her register.
- **After:** Guess I'm brave. Doesn't feel like it.

## Attempt 3

- **Before:** Guess I'm brave. Doesn't feel like it.
- **Score:** 6/10
- **Reason:** Better — sarcastic self-deflection is closer to her voice and drops the second-person comfort framing. Still slightly reflective/earnest for her usual clipped delivery; needs to be terser to match her under-pressure brevity.
- **After:** Fine. I'm fine.

**Final:** Fine. I'm fine.
