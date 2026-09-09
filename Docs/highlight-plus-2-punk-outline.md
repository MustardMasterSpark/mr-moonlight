## 2026-09-08 update — ported to main-menu buttons as a custom uGUI shader, NOT the HighlightEffect component

Confirmed directly in the vendor source (`HighlightEffect.cs:2860`,
`return r is MeshRenderer || r is SpriteRenderer || r is SkinnedMeshRenderer;`): Highlight Plus 2
has **no uGUI/CanvasRenderer support at all**. The main-menu buttons are a Screen-Space-Overlay
Canvas, so the real `HighlightEffect` component cannot be dropped on them - confirmed the "hasn't
been scoped yet" flag from the prior handoff was a real blocker, not a formality.

Presented Carlos three options (reimplement as a uGUI shader / convert buttons to world-space
SpriteRenderer objects so the real component works / camera+RenderTexture bridge - the last one
ruled out up front, this project has a known unresolved URP RenderTexture-alpha bug). **Carlos
chose the uGUI shader reimplementation** - same visual language, not the same component, kept for
main-menu buttons specifically. If a later system (MRM-16 interaction highlight, MRM-42 inventory
UI) is on 3D-rendered objects rather than Canvas UI, the real `HighlightEffect` + tuned settings
below are still the right call there.

**What got built** (`Assets/_Project/Art/UI/Shaders/UI_PunkOutline.shader`,
`Assets/_Project/Code/Runtime/UI/ButtonPunkOutlineHighlight.cs`):
- The shader samples the button's OWN sprite alpha and ring-marches outward (8 rings x 12 angles,
  `#define RING_STEPS`/`ANGLE_STEPS` - see trap below) to find the silhouette edge, so the outline
  hugs whatever shape the current background sprite is (the torn-paper cutouts) rather than
  drawing a rectangle. Each ring tap's angle is jittered by a noise texture sampled with the same
  "stop-motion" re-sample trick as the traced HighlightComposeOutline.shader math
  (`floor(_Time.y * _StopMotionScale) / _StopMotionScale`) for the boiling/redraw feel.
- Gradient: black held until `_GradientKnee` (default 0.6, matching the tuned value below) then
  transitions to white - same lever, reimplemented.
- `ButtonPunkOutlineHighlight` instantiates its own material clone per button in `Awake()` (so
  each button's `_HighlightAmount` fade is independent) and DOTweens it 0→1 on
  `IPointerEnter/Exit` AND `ISelect/Deselect` (mouse hover and gamepad/keyboard nav both trigger
  it - this project has gamepad nav wired on these buttons).
- **Trap, confirmed this session:** originally exposed ring/angle step counts as material float
  uniforms so Carlos could tune them like everything else. A uniform-bounded shader loop
  (`for (int a = 0; a < _AngleSteps; a++)`) silently compiled to Unity's magenta error-shader
  fallback with **zero Console output** - no error, no warning, just wrong color. Confirmed by
  reverting one button to the default material (correct sprite render returned) then bisecting.
  Fix: loop bounds must be compile-time `#define` constants, not uniforms; also switched the
  in-loop `tex2D` calls to `tex2Dlod` (explicit LOD 0) since gradient-dependent sampling inside
  non-uniform control flow is the same class of problem. Tune ring/angle counts by editing the
  shader's `#define` lines, not the material Inspector - everything else (`_OutlineWidth`,
  `_DistortionAmount`, `_PatternScale`, `_StopMotionScale`, `_GradientKnee`, colors) is still a
  normal tunable material property.
- Verified live in Play Mode: reflection-invoked `OnPointerEnter`/`OnPointerExit` on StartButton,
  confirmed the material instance's `_HighlightAmount` animates 0→1→0 while the shared asset stays
  at 0 (per-button isolation works), and screenshotted the actual jagged outline hugging the paper
  sprite's torn silhouette (not a rectangle).
- **Not yet tuned by Carlos** - thickness/wobble/color read against real backgrounds needs his own
  eyes live, same as the original Playground pass took "three rounds of make it thicker." Current
  defaults are ported-and-converted from the tuned Highlight Plus values where a direct unit
  mapping existed (`_StopMotionScale` = 7, matches exactly; `_GradientKnee` = 0.6, matches exactly)
  and reasonable starting guesses elsewhere since the original values were in different unit spaces
  (HighlightEffect's outlineWidth and its screen-space UV distortion don't convert 1:1 into this
  button-local-UV technique).

### 2026-09-08, same day - "much wider" pass: two more real bugs, plus an architecture change

Carlos: "make the highlight effect much wider" (with a Playground screenshot as the size
reference), then independently spotted the actual problem live and proposed the fix himself before
it was finished: "the shader isn't able to go beyond the size of the button container... make the
container of the button bigger but the texture stays in the same place and the same size."

**Bug 1 - `_MainTex_TexelSize` is never populated for a uGUI Image.** CanvasRenderer binds the
sprite texture directly at draw time, bypassing `Material.SetTexture` - so Unity's usual automatic
`_MainTex_TexelSize` companion property silently reads back as the `(1,1,1,1)` default instead of
the sprite's real pixel size. No error, just wrong math (the ring-march radius was off by ~1000x).
Confirmed by reading it back at runtime. Fix: a custom `_SpriteTexelSize` uniform, set manually
from `image.sprite.texture.width/height` in script (`ButtonPunkOutlineHighlight.cs`), not the
shader's automatic one. Re-applied on every fade-in, not just `Awake()`, since a raw
`image.material = new Material(...)` anywhere (including ad-hoc debug/test code) silently replaces
the instance and drops this back to zero with no warning - lost real testing time to this exact
self-inflicted trap mid-session before recognizing it.

**Bug 2 - the source PNGs have almost no transparent margin.** Inspected `paper-3l-05.png`
directly: the torn-paper shape fills nearly the entire canvas and touches the texture's own left
edge with zero padding. A ring-march-outward outline computed in the sprite's own texture UV space
has nowhere to draw on any side where the shape already reaches the image boundary - this, not a
math bug, is why the first "wider" attempt only ever showed a small lopsided blob near the one
tapered corner that happened to have spare margin (confirmed by inspecting the source art, not by
guessing).

**Fix - a separate, larger overlay quad, not the button's own Image.** The real button Image is
untouched (same size Carlos set, same sprite, default material, no shader). Each button now has a
`HighlightOverlay` child (`Image`, `raycastTarget = false`) sized `overlayPaddingPx` (default 25)
bigger than the button's own rect on every side - purely so the outline has room to exist.
`_PaddingUV` (a new shader uniform, set from script) remaps the overlay's own larger 0-1 UV range
back down to where the real sprite sits in the middle, and `SampleAlpha()` explicitly treats
anything outside that as transparent rather than trusting the texture's hardware wrap mode (Clamp
would otherwise repeat whatever opaque edge pixel is nearest - exactly the wrong answer once
sampling goes outside the real image). The overlay never draws the sprite's own content (returns
transparent inside the shape) - the real button Image already shows that - it only ever draws the
outline band, avoiding a doubled/redundant paper texture.

**Known accepted limitation, not fixed:** since `HighlightOverlay` is a child of its own button, it
draws at that button's position in `ButtonGroup`'s sibling order - so a highlight that spills onto
a LATER sibling (e.g. Start's overlay reaching down into Legion's row) draws BEHIND that neighbor,
not on top of it. Carlos explicitly said not to worry about this for now ("don't worry about that
right now" / "we shouldn't be constrained... it's cut off" was about the texture-margin clipping,
not this). If it matters later, the fix is reparenting the active button's overlay to be the last
sibling in `ButtonGroup` while highlighted (and restoring it after) rather than a structural
redesign.

**Final tuned values this pass** (material `M_PunkOutline.mat`): `_OutlineWidth` = 45 (down from
150/90 - Carlos: "make it a little bit thinner" once reach was no longer the bottleneck),
`_DistortionAmount` = 10, `_PatternScale` = 2.5, `overlayPaddingPx` = 25. Verified live in Play
Mode via reflection-invoked `OnPointerEnter` + screenshot: outline now wraps the full torn-paper
silhouette evenly (not lopsided) and visibly spills past the button's own bounds toward its
neighbor, matching the reference screenshot Carlos drew on. Not yet re-confirmed by Carlos himself
live - flag if he wants further thickness/reach tuning.

### 2026-09-08, same day - white tint on the paper itself: a third real bug

Carlos wanted the button's own paper sprite to also brighten toward white while highlighted, "so
it makes a bigger impact on the highlight." First attempt tweened `Image.color` to
`(1.35,1.35,1.35,1)` - Carlos reported "it looks exactly the same as the other [buttons]."

**Bug 3 - `UnityEngine.UI.Graphic.color` can never brighten a uGUI Image.** `Image.color` is packed
into the mesh as a byte-precision vertex color (`Color32`); any channel above 1.0 silently clamps
to 1.0 with **no error and no visual difference from exactly 1.0**, which is already a button's
normal, un-highlighted color. So a ">1 multiply" trick - which genuinely works on a real
Renderer/Material, where the multiply happens in the fragment shader before the GPU's own HDR/LDR
clamp - has zero effect through `Graphic.color`, structurally, on every uGUI Image, not just this
one. Confirmed two ways: (1) read `image.color` back in code immediately after the tween finished -
it correctly reported `(1.35,1.35,1.35,1)`; (2) sampled actual rendered pixel values from a
screenshot at matching coordinates on a highlighted vs. non-highlighted button - byte-identical.
The color was set correctly and had literally no rendering effect.

**Fix** - a second tiny shader, `Assets/_Project/Art/UI/Shaders/UI_WhiteTint.shader` +
`Assets/_Project/Art/UI/ButtonHighlight/M_ButtonWhiteTint.mat`, assigned to the button's own Image
(not the overlay). It lerps the sampled texture color toward white by a plain material float
uniform (`_TintAmount`, not byte-packed, no clamp problem) in the fragment stage instead. Verified
live: forced `_TintAmount` on the material directly and screenshotted - the highlighted button
reads unmistakably brighter than its neighbors this time.

`ButtonPunkOutlineHighlight.cs` now drives THREE things per button on hover/select, all through
material float uniforms (never `Graphic.color`): the outline overlay's `_HighlightAmount`, and the
button's own `_TintAmount`. `highlightTintAmount` (default 0.45, range 0-1) is the Inspector knob -
0 = no change, 1 = fully white. `tintMaterialSource` points at `M_ButtonWhiteTint.mat`.

**General lesson for any future "make a uGUI Image brighter/whiter" ask**: never reach for
`Image.color` values above 1 - it will silently no-op. Use a shader that lerps toward white (or
any HDR-style multiply) in the fragment stage instead.

---

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
