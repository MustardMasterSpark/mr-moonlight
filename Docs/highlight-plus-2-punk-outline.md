# Highlight Plus 2 — "punk" stylized outline (reference config)

**Written 2026-09-08.** Built and tuned live in the **Playground** project
(`E:\playground\My project`, port 8081 — not this repo, not git-tracked), NOT yet ported to
Mr. Moonlight. This doc is the "where it is / how to bring it over" reference for whenever
**MRM-16** (interaction highlight) or **MRM-42** (inventory UI) picks this up.

## What this is

Carlos wants this specific look used **extensively** — both the interaction-highlight system
(MRM-16) and the inventory UI (MRM-42) — not a one-off demo tweak. Visual target: Spider-Punk /
Miles Morales comic style — thick, jagged, hand-scribbled outline that "boils" (redraws slightly
differently) over time, not a clean glow. Reference images Carlos supplied lived in
`C:\Users\calva\Desktop\menu images\outline style\` (his machine, not the repo).

Iterated live with Carlos across several passes (see session history below) — this is the
**converged, Carlos-approved state**, not a first draft.

## Where it lives right now

- **Project:** Playground (`E:\playground\My project`), NOT git-tracked, Carlos does not commit it.
- **Scene:** `Assets/PLAYGROUND/Highlight Plus 2/Demo/Demo5_Effects.unity`
- **GameObject:** `Barrel` (labelText on its `HighlightEffect`: `"Outline Stylized Effect"`) — one
  of several showcase objects in the vendor's own effects-gallery demo scene.
- **Component:** `HighlightPlus.HighlightEffect`

## The exact settings (as of 2026-09-08, Carlos-approved)

| Field | Value | Why |
|---|---|---|
| `outline` | 1.0 | Outline effect on |
| `outlineColorStyle` | `Gradient` (1) | Not solid — see gradient below |
| `outlineGradient` | 3 keys: black @ t=0, **black @ t=0.6**, white @ t=1.0 | Holds black through 60% of the range before transitioning to white in the last 40%, so black reads as a thick band rather than a thin sliver. **The middle key is the actual lever for "how much black" — move it (e.g. 0.4 for even more black) to retune.** |
| `outlineWidth` | **3.5** | Started at 0.45 (vendor default), pushed up across three rounds of "make it thicker" |
| `outlineBlurPasses` | **1** — do not set to 0, see Traps | Softens the edge slightly; needed for the engine not to crash |
| `padding` | 0.15 | Sharpens the alpha falloff at the outer edge (see Traps re: what this does *not* do) |
| `outlineStylized` | `true` | Turns on the "hand-drawn redraw" distortion |
| `outlinePatternDistortionTexture` | `Runtime/Resources/HighlightPlus/charcoal.png` | The vendor's own texture for this exact effect — found by grep'ing their `Demo5_Effects.unity` for `outlineStylized: 1` and reading which object used it |
| `outlinePatternScale` | **0.12** | Low = few, broad shape-variations (hugs the object's true contour). High = many tight wiggles (looked "amoeba-blob," rejected) |
| `outlinePatternDistortionAmount` | **0.35** | This is a raw **screen-space UV offset** in the shader (see math below), not a 0–1 "intensity" — small values go a long way |
| `outlinePatternStopMotionScale` | 7 | How often the distortion re-samples to a new "frame" — this is the "alternating shapes, blinking" motion Carlos wanted kept |
| `outlineSharpness` | 1.0 (unchanged, floor value) | Can't go below 1 — see Traps |

## The shader math that actually explains this (read before re-tuning)

Traced directly in
`Runtime/Resources/HighlightPlus/HighlightComposeOutline.shader` (`frag()`, `#if HP_STYLIZED`
block) and `HighlightEffect.cs`:

```hlsl
float2 patternUV = uv * PATTERN_SCALE + floor(_Time.y * PATTERN_STOP_MOTION_SCALE) / PATTERN_STOP_MOTION_SCALE;
fixed pattern = tex2D(_DistortionTex, patternUV).x;
uv.x += (pattern - 0.5) * PATTERN_DISTORTION_AMOUNT;
```

- `uv` here is **screen-space** (`i.scrPos.xy / i.scrPos.w`), not object/mesh UV. So
  `outlinePatternDistortionAmount` shifts the sample point by a fraction of the **whole screen** —
  1.5 (an early attempt) shifted sampling by up to 0.75 of the screen, which is why the sides
  looked like a distorted blob instead of tracking the barrel. 0.35 is the value that read as
  "hugs the contour with a bit of wobble."
- `outlinePatternScale` scales `uv` **up** before sampling the noise texture — higher scale = the
  noise texture repeats *more* across the outline (more, tighter wiggles), not fewer. This is the
  opposite of the intuitive reading; verified by testing both directions.
- The black/white split is driven by `outlineGradient`, but I could not fully pin down the exact
  formula relating `outlineGradientKnee`/`outlineGradientPower` to the rendered result (numbers
  worked out on paper didn't match what was visible on screen — likely a color-space or
  HDR-gradient wrinkle I didn't chase down). **Don't trust knee/power math from first principles —
  tune by moving the gradient's own color-key stop instead** (see table above), which is
  direction-verified and doesn't depend on that formula.

## Traps (all confirmed this session, don't re-discover them)

1. **`outlineBlurPasses = 0` crashes the render pass.** `HighlightEffect.SmoothOutline()`
   (`HighlightEffect.cs:2424`) computes `bufferCount = adjustedBlurPasses * 2`; at 0 blur passes
   `bufferCount = 0` and the very next line does `mipOutlineBuffers[bufferCount - 2]` →
   `mipOutlineBuffers[-2]`, an `IndexOutOfRangeException` thrown every frame in Play Mode. This is
   a genuine bug in the vendor's own code (not URP/RenderGraph-specific, just an unguarded
   `bufferCount - 2`). **Minimum safe value is 1.**
2. **A true "gap" between the object and the outline band is not currently achievable together
   with this stylized look.** `padding`'s tooltip says "adds an empty margin between the outline
   mesh and the effects," and it genuinely does act as a real geometric inset — but **only** in
   Highlight Plus's non-`Highest` quality modes (`Fastest`/`High`/`Medium`), which use a different
   (stencil/vertex-offset) outline algorithm. `outlineStylized` (the charcoal animation) is only
   implemented in the **smooth, screen-space** compose shader, which requires
   `outlineQuality == Highest`. In `Highest` mode, `padding` only sharpens the alpha falloff at the
   outer edge — it does not shift the band inward. **You can have the animated stylized outline,
   or a true gap, not both, without a custom shader edit.** Carlos accepted no-gap for this pass.
   If a gap becomes a hard requirement later, the fix is editing
   `HighlightComposeOutline.shader` directly to add a manual inner-radius cutout — not attempted,
   flagged as a real option if needed.
3. **`outlineSharpness` cannot go below 1** — `HighlightEffect.OnValidate()` clamps it to
   `Mathf.Max(1f, outlineSharpness)`. It's a `pow(alpha, sharpness)` in the shader, so values above
   1 make edges *more* transparent, not less — it's not a lever for "make it more solid."
4. **The demo scene's `EventSystem` had the legacy `StandaloneInputModule`** (project's Active
   Input Handling is New Input System only) — threw `InvalidOperationException` every frame,
   silently killing all hover/click. Swapped to `InputSystemUIInputModule`. This is a Playground-
   only fact (per-scene leftover), not relevant to Mr. Moonlight, but worth knowing if evaluating
   more Highlight Plus 2 demo scenes there.
5. **`Demo/Scripts/SimpleCharacterController.cs`** (the demo's WASD/mouselook rig, separate from
   the core package) called raw legacy `Input.GetKey`/`Input.GetAxis` and force-locked the cursor
   every frame — patched to branch on `ENABLE_INPUT_SYSTEM` like the package's own `InputProxy.cs`
   already does. Playground-only, not relevant to the port.

## Migration checklist (when MRM-16/MRM-42 actually pick this up)

MRM-16's issue already has the load-bearing migration note — repeating it here so it's in one
place with the tuned values above:

1. `Highlight Plus 2/Runtime/` → `Assets/ThirdParty/HighlightPlus/Runtime/`,
   `Highlight Plus 2/Editor/` → `Assets/ThirdParty/HighlightPlus/Editor/`. Drop `Demo/`,
   `Documentation/`, `README.txt` — don't bring the demo scenes or `SimpleCharacterController.cs`
   over, they're Playground-only scaffolding.
2. **Register `HighlightPlusRenderPassFeature` on `PC_Renderer`** (Mr. Moonlight's actual active
   URP renderer, per `Docs/pc-build-target.md`) **before Retro CRT** in the feature list, per
   MRM-16's issue text (Finding 2 in `Docs/new-asset-list.md`) — order matters so highlights
   receive the CRT pass like everything else. This is the same class of bug this session hit in
   Playground (a renderer feature silently missing from the active renderer = highlight logic runs,
   nothing draws) — confirm with `manage_graphics` `feature_list` on `PC_Renderer` after adding it.
3. Apply the settings table above to whatever `HighlightEffect` component ends up on the real
   interactable prefabs / inventory item prefabs — these are **tuned values**, not defaults, don't
   let them reset to vendor defaults during migration.
4. **Scope/tone note for whoever picks up MRM-16:** the issue's own original triage text says
   *"Profile it down... a subtle rim, not a video-game glow"* — that guidance predates this
   session. Carlos has now explicitly approved the bold punk/comic look above for extensive use in
   both the interaction highlight and the inventory UI. Treat this doc as the current direction;
   flag the discrepancy to Carlos if it matters rather than silently picking one.
5. Re-verify `outlinePatternDistortionTexture` (`charcoal.png`) actually made the migration — it's
   a `Runtime/Resources/` asset, should come along automatically with the Runtime folder copy, but
   confirm the reference isn't broken after the move.
