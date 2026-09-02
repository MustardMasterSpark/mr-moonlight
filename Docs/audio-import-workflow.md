# Audio import workflow — Mr. Moonlight

Quick, repeatable steps for getting a new audio clip into the game at the right size and
quality. **This doc is the "how"; `Docs/webgl-budget.md` §3.1 and §6 are the "why"** — the
budget math, the 13× size gap, and the per-category rationale live there. Don't duplicate that
reasoning here; read it once if you want the full story.

> **Note added 2026-08-27.** `webgl-budget.md` is **partially historical** — WebGL was dropped on
> 2026-08-25. **This workflow is unaffected:** the prefix → preset routing, the compression settings
> and the "a clip imported outside a preset is a bug" rule are all about codecs and discipline, not
> about browsers. Only the *size ceiling* framing over there has changed. See
> `Docs/pc-build-target.md`.

**The one rule that matters more than any setting below:** every clip enters the game through
one of the presets in `Assets/_Project/Settings/Presets/` (`Aud_*.preset`). If a clip's import
settings were hand-tweaked instead, that's a bug — per `webgl-budget.md` §9. This doc exists so
that never has to happen because someone forgot the numbers.

---

## 0. What's already set up (done during MRM-6 — nothing to build)

- **9 `AudioImporter` presets** in `Assets/_Project/Settings/Presets/`: `Aud_VO_Dialogue`,
  `Aud_VO_Long`, `Aud_Ambient`, `Aud_Music`, `Aud_OneShot_General`, `Aud_OneShot_Short`,
  `Aud_EnemyVox`, `Aud_UI`, `Aud_PlayerVox` (added 2026-08-22, MRM-17).
- **Wired into `Edit → Project Settings → Preset Manager`**, so a new clip auto-applies a preset
  based on its filename prefix the moment it's imported:

  | Filename starts with | Auto-applies |
  |---|---|
  | `VO_` | `Aud_VO_Dialogue` |
  | `MUS_` | `Aud_Music` |
  | `AMB_` | `Aud_Ambient` |
  | `ENM_` | `Aud_EnemyVox` |
  | `UI_` | `Aud_UI` |
  | `PLR_` | `Aud_PlayerVox` |
  | anything else (including `SFX_`) | `Aud_OneShot_General` (catch-all) |

- **Project audio settings**: Real Voices 24, Virtual Voices 512, DSP Buffer Size *Best
  Performance* (`Edit → Project Settings → Audio`).

Two presets are **not** in the auto-filter table because a filename prefix can't tell them apart
from their sibling — you decide by ear/duration (see step 3 below): `Aud_VO_Long` and
`Aud_OneShot_Short`.

`Aud_PlayerVox` is `Aud_VO_Dialogue`'s settings (Compressed In Memory, Vorbis 40%, mono, 22050 Hz
override) under a separate name, for the same reason `PLR_` is separate from `VO_`: Tracey's
death yells/pain grunts have no dialogue line ID and aren't spoken lines, so they don't belong on
the dialogue preset's naming path even though the settings happen to match today. If the two
presets' settings ever need to diverge (e.g. yells wanting a different quality), they already
have separate presets to do that from.

*(The naming table in `Docs/unity-conventions.md` now lists `ENM_`/`UI_`/`PLR_` alongside the
originals - this doc used to flag `ENM_`/`UI_` as missing from that table; that's since been
fixed, so if you're reading this looking for a gap, there isn't one anymore.)*

---

## 1. Adding a new clip — the normal path

1. **Name it with the right prefix**: `SFX_`, `VO_`, `AMB_`, `MUS_`, `ENM_` for enemy
   vocalizations, `UI_` for interface sounds, or `PLR_` for the player's own non-dialogue
   vocalizations (death, pain, effort). Voice-over files use the dialogue line ID per
   `Docs/unity-conventions.md` (`VO_D-08-043`).
2. **Drop it into `Assets/_Project/Audio/`.** The matching preset auto-applies on import — check
   the top of the clip's Inspector; it should show the preset name next to the little circular
   preset icon.
3. **Manually check these two cases** — the filter can't make either call for you:
   - A `VO_` clip longer than **~15 seconds** → swap it from the auto-applied
     `Aud_VO_Dialogue` to **`Aud_VO_Long`** (Streaming instead of Compressed In Memory — a long
     line shouldn't sit fully decompressed in RAM).
   - Any one-shot under **~1 second** (a footstep, a UI click, a small impact) → swap it from
     whatever it landed on to **`Aud_OneShot_Short`** (ADPCM — Vorbis has a per-clip header and
     decode cost a 300 ms clip never earns back).

   To swap: select the clip in the Project window, click the preset icon (small circle, top
   right of the Import Settings header) in the Inspector, pick the other preset from the list.

That's it — no manual sample rate, mono, or compression decisions on a per-clip basis. The
preset owns all of that.

**Gotcha, seen once (2026-09-02):** a clip copied into `Assets/_Project/Audio/` by a script/external
tool (`cp` + `AssetDatabase.Refresh`, not a drag-and-drop into the Editor) landed on the
`Aud_OneShot_General` catch-all instead of its prefix's preset (`ENM_` → `Aud_EnemyVox`), even
though the filename prefix was correct. Not confirmed as a rule, just flag it if a freshly-imported
clip's Inspector doesn't show the preset you expected — fix is to apply the right `Aud_*` preset by
hand (select the clip, click the preset icon top-right of Import Settings, pick it). Every clip
dragged in through the Editor itself this project has still auto-routed correctly.

---

## 2. Applying a preset to files already sitting in the project (batch)

If clips got imported before a preset existed, or you're fixing a batch that landed on the wrong
one:

1. Select every clip that needs the same treatment in the Project window (multi-select works).
2. In the Inspector, click the preset icon (top right of Import Settings) and choose the target
   `Aud_*` preset.
3. It applies to the whole selection at once — no need to do this one file at a time.

---

## 3. Quick reference — which preset, and what it actually does

| Preset | Use for | Mono | Load type | Compression | Quality | Sample rate |
|---|---|---|---|---|---|---|
| `Aud_VO_Dialogue` | Tracey's dialogue lines, ≤15 s | Yes | Compressed In Memory | Vorbis | 40% | Override 22 050 Hz |
| `Aud_VO_Long` | Monologues, cutscene narration, >15 s | Yes | Streaming | Vorbis | 40% | Override 22 050 Hz |
| `Aud_Ambient` | Weather loops, location beds | **No** (stereo) | Streaming | Vorbis | 50% | Preserve (source rate) |
| `Aud_Music` | Score, stingers | **No** (stereo) | Streaming | Vorbis | 50% | Preserve (source rate) |
| `Aud_OneShot_General` | Most SFX — props, world one-shots | Yes | Decompress On Load | Vorbis | 40% | Optimize (Unity auto-picks) |
| `Aud_OneShot_Short` | Footsteps, weapon fire, anything <~1s | Yes | Decompress On Load | **ADPCM** | n/a | Override 22 050 Hz |
| `Aud_EnemyVox` | Enemy vocalizations, pain loops | Yes | Compressed In Memory | Vorbis | **35%** | Override 22 050 Hz |
| `Aud_UI` | Menu/HUD sounds | Yes | Decompress On Load | **ADPCM** | n/a | Override 22 050 Hz |
| `Aud_PlayerVox` | Tracey's non-dialogue vocalizations — death, pain, effort | Yes | Compressed In Memory | Vorbis | 40% | Override 22 050 Hz |

**Why mono for almost everything:** stereo on a 3D-positioned source doubles the bytes for zero
audible gain — only music, ambient beds, and (arguably) UI stay stereo, and even UI is mono here
since it's not meaningfully wider in stereo anyway. **Why ADPCM below ~1 second:** it's a fixed
3.5:1 ratio with free decode, where Vorbis's per-clip header overhead actually costs more than it
saves on something that short.

---

## 4. On the N64/PS1-era sound character you asked about

You don't need a separate "make it sound retro" pass — **the budget-driven settings above
already get you most of the way there for free.** 22 050 Hz mono, run through ADPCM (a coarse
4-bit-ish quantization) or low-quality Vorbis (0.35–0.40), sits close to what N64/PS1-era audio
hardware actually output — both platforms lived in that same low-sample-rate, heavily-quantized
territory out of genuine hardware necessity. The size optimization and the period aesthetic are
the same lever here, not a tradeoff.

**If you want it *more* pronounced than that** (a deliberately crunchier, more degraded read),
two non-destructive options — deliberately non-destructive, same reasoning as
`webgl-budget.md`'s "Force To Mono at import, not in the DAW — it keeps the source files intact":

1. **Drop the Override Sample Rate further** on a specific preset or a specific clip — 16 000 Hz
   or even 11 025 Hz reads noticeably grittier, especially on footsteps/impacts. Cheap to test,
   fully reversible (it's one import-setting field), and doesn't touch the source file.
2. **A mixer-side effect, not baked into any clip.** An `AudioMixer` group with a Lowpass Filter
   (or a custom bitcrusher effect) that diegetic SFX route through — tunable and toggleable
   without re-exporting anything. Not built, not needed right now — just flagging it exists as a
   future option if the vanilla presets above end up sounding cleaner than you want once real
   content is in.

**Don't bake either of these into the source WAV/AIFF in the DAW.** Keep sources clean; let the
import pipeline (or, later, the mixer) do the degrading — that's what keeps this reversible if
the call changes.

---

## 5. If you want this automated later

You mentioned maybe wanting a pipeline that treats incoming sound files automatically. The
Preset Manager filter in §0 already does the routing half of that (filename prefix → preset,
zero manual work). What it can't do is the two judgment calls in §1 step 3 (duration-based
preset swaps) — an editor script watching for newly-imported clips under `Assets/_Project/Audio/`
could read each clip's length via `AudioImporter`/`AudioClip` and auto-swap `Aud_VO_Dialogue` →
`Aud_VO_Long` past 15 s, or the one-shot presets at the 1 s boundary, closing that last manual
gap. Worth a small future issue if re-doing this by hand ever gets tedious — not built now since
you only asked for the instructions today.
