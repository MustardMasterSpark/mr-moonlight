# 3D Prop Pipeline Wizard

**Owner issue:** MRM-72 · **Branch:** `mrm-72` · **Created:** 2026-08-27
**Fire it with:** `/prop` · **Log:** `Docs/prop-log.md`

> **Read `.claude/skills/prop-wizard/SKILL.md` first.** It carries the intake questions and the
> **complete static-prop hot path** — the 90% case — so an ordinary prop never has to load this
> file at all. Come here for the character path (§4), weapons (§6), special cases (§7), the
> high-poly branch (§8), and the reference material the hot path compresses.
>
> **This document is a living instruction set, not a reference.** It is written to be *executed*
> by Claude, one prop at a time. Every finished prop must leave it slightly better
> than it found it — see **§9, the write-back protocol**. If you are reading this and about to
> start a prop, the protocol at §9 is not optional cleanup; it is the reason this file exists.

**Governing goal: minutes of Carlos's attention per finished prop, trending down.**
Not "the pipeline is documented." Every rule below earns its place by removing a decision,
a round-trip, or a mistake. A rule that does none of those should be deleted.

---

## 0. Ground rules that never change

These are load-bearing. Violating one costs a re-export or a re-bake, not a tweak.

| Rule | Why |
|---|---|
| **1 Blender unit = 1 metre**, transforms applied, origin at the base/handhold | Anything else imports at the wrong scale or spins on its axis |
| **FBX export: tick "Apply Transform" (`bake_space_transform`) — but only while the object has no armature** | The single fix for sideways imports/oversized bounds, and for a Unity root that won't read Rotation (0,0,0). **Once an armature exists, leave it OFF** — it's the exporter's own risky, experimental toggle and can corrupt bone orientations. For rigged content, get the same clean-root result from Unity's importer instead: `ModelImporter.bakeAxisConversion = true`. See §4.2a |
| **Unity mesh import: Scale Factor = 1, Materials = None** | Never compensate scale in Unity — fix the export. Never let the importer generate materials |
| **The placed instance must read Rotation (0,0,0), Scale (1,1,1)** | Any hand-tuned rotation/scale means the *export* is wrong. Fix it upstream |
| **`Filter Mode = Point` on BaseColor** | Bilinear blurs the quantised pixels back into mush and silently undoes the whole pixelation pass. `MoonlightTextureImporter.cs` enforces this automatically |
| **Never pixelate a Normal map** | Banding in a normal map becomes faceted lighting |
| **Ask permission before each piece of Unity or Blender MCP work** | `CLAUDE.md` hard rule. Ask → do → verify by reading real state back → document |

### Who does each step — automate by default

**The bridges are live.** Claude has Blender MCP, Unity MCP (Mr. Moonlight on port 8080) and
Playground MCP (port 8081). **Automation is the default; a manual step is a failure to be
recorded, not a normal state.**

| Marker | Meaning |
|---|---|
| 🤖 **AUTO** | Claude runs a script. No round-trips, no permission needed |
| 🔧 **MCP** | Claude drives Blender or Unity directly. **Ask permission first**, then do it, then verify by reading real state back |
| 👤 **CARLOS** | No automation path exists. Claude prepares everything around it and hands over |

Every step below carries one of these. **If a 👤 step turns out to be automatable, that is a §9.3
backlog entry** — do not quietly accept it as manual forever.

The only genuinely un-automatable steps today are the ones behind third-party GUIs with no API
(Tripo, modddif, AccuRig, QuickMagic) and Carlos's own review judgement. Everything else should
be 🤖 or 🔧.

### `E:\Props` is a dump, not a manifest

It is where Carlos drops everything he downloads or generates. **Nothing in it is game-ready by
default, and its contents are not the game's asset list.** It is only ever a *source*. The
shipping asset lives in `Assets/_Project/Art/<Category>/<Prop>/`.

Sources the wizard must ask about, every time:

| Source | What it means |
|---|---|
| **`E:\Props`** | The download/generation dump. Treat as raw |
| **Playground project** (`E:\playground\My project`) | A pack Carlos staged there for testing. Extract the working files out — see `Docs/dual-project-workflow.md` |
| **Fresh Tripo output** | Carlos just generated it. Ask *where he put it* |

---

## 0.5 Shakedown mode — the first run of each path

**Nothing has been through this pipeline yet.** Until a path has completed once, it is a plan,
not a process. Carlos's sequence: **static prop → character → weapon.** After those three, the
decisions here can be called solid. Before them, they are provisional.

**On the first run of a path, both Carlos and Claude are watching the pipeline, not just the
prop.** The prop is the test case; the pipeline is what's under test.

Rules that apply **only** during a path's first run:

| Rule | Why |
|---|---|
| **Narrate each step before doing it**, and report what actually happened after | Silent success hides a step that only worked by luck |
| **Stop at every step boundary.** Do not chain steps | If step 3 is wrong, we want to know before step 7 built on it |
| **Fix the pipeline mid-run, not at the end** | The normal write-back protocol (§9) waits for Carlos's sign-off. **During shakedown, correct the doc the moment a gap appears** — waiting means re-deriving it |
| **Never batch.** One prop only | Bulk mode is itself unproven |
| **Report anything that surprised you**, even if it worked | A step that worked for a reason you didn't expect will fail on the next prop |
| **Prefer asking over assuming**, even where the doc gives a default | The defaults are guesses until one prop validates them |

**What to watch for, per path:**

- **Static prop** — do the source texture names match what `texture_pass.py` expects? Is
  Tripo's own texture already in style, or does it need quantising? Does the prefab destination
  and collider choice actually make sense? (Gaps G1, G2, G5, G7 below.)
- **Character** — the AccuRig T-pose reversion (§4.7) is documented from Reallusion's forums,
  **not from our experience.** §4.3/§4.5 are the checks that would catch it. Confirm they
  actually can. **Do not start a second character until the first retargets cleanly.**
- **Weapon** — does a texture-only pass genuinely preserve the rig and clips? Does a Blender
  cleanup break them?

**Exit criterion:** a path leaves shakedown when one prop has completed it end to end, Carlos has
approved the result in Unity, and the gaps it exposed are either fixed or recorded below.

---

## 1. The wizard — entry point

When `/prop` fires, or Carlos says "let's do a prop", ask these in order.
**Do not skip ahead and do not assume.**

### Q1 — Which path?

```
1  Character   — animated, rigged, retargeted        (§4, the long one)
2  Static prop — no rig, no animation                (§5, the short one)
3  Weapon      — already rigged + animated           (§6, retexture only)
4  Special     — doesn't fit the above               (§7)
```

### Q2 — Which prop, and where is it?

Name it, then locate it. Use the canonical name from `Docs/glossary.md` if one exists —
the folder is named for **what it represents**, not what the source file is called.

### Q3 — Has Tripo already run?

Generation in Tripo is **Carlos's step**. He normally arrives with it done.
**Always ask; never assume.** If not done, stop and wait — there is nothing to work on.

### Q4 — Is this a high-poly source?

**Default assumption: no.** Tripo delivers near-game-ready meshes, and the default path
does no baking at all.

**Carlos flags high-poly sources by hand.** If he does, do not guess the remedy — go to §8
and ask *decimate vs. build a low-poly and bake*.

### Q5 — Texture resolution?

**Ask before generating any texture. Every prop. Every time.** This is an acceptance
criterion, not a courtesy.

| Category | Default | Notes |
|---|---|---|
| Static prop | **512** | Anything larger is a per-prop decision with a stated reason |
| Weapon | **1024** | Held close to camera, seen constantly |
| Character | **1024–2048** | Face texture space is scarce; ask which |
| Small item | **256–512** | A can of beans does not need 512 |

Normal maps are **half** the BaseColor resolution. That is automatic — don't ask separately.

---

## 2. What every finished asset ships — the two-map standard

**Superseded `Docs/3d-asset-pipeline.md` §2 on 2026-08-27.** That document mandates a third
packed Mask texture. `RetroLit.shader` does not sample one.

Read directly out of `Assets/ThirdParty/Retro Shaders Pro/Shaders/RetroLit.shader`, the shader
exposes exactly:

```
_BaseMap          Base Texture              <- our BaseColor
_NormalMap        Normal Texture            <- our Normal
_NormalStrength   0..2
_Glossiness       1..20      scalar, not a map
_ReflectionCubemap + _CubemapColor + _CubemapRotation
```

There is **no `_MetallicGlossMap`, no `_OcclusionMap`, and no emission map anywhere in Retro
Shaders Pro.** A Mask texture would be shipped, compressed, loaded into memory, and sampled by
nothing.

So every prop ships **two files**:

| File | Treatment |
|---|---|
| `T_<Prop>_BaseColor.png` | **AO multiplied in**, quantised + Bayer dithered, Point filtered |
| `T_<Prop>_Normal.png` | Half resolution, Lanczos, **never** quantised or dithered |

**Occlusion goes into the albedo.** Multiplying AO into BaseColor is free, it is what PSX-era
art actually did, and it is the only way RetroLit will ever show it. `texture_pass.py` does this
automatically when an `_AO` input is present.

**Metallic and smoothness are scalars.** Set `_Glossiness` on the material — roughly `5` for
ordinary surfaces, higher (`10–20`) for polished metal. For a genuinely reflective object, enable
`_USE_SPECULAR_LIGHT` and optionally assign `_ReflectionCubemap`. This is a per-material call,
made once, not a per-texel bake.

### Emission: real Lights, no maps

**Decision, 2026-08-27, demo scope.** RetroLit has no emission slot, and we are not going to
fight it. Anything that glows — a lamp, the radio LED, wolf eyes — gets a **real Light component
parented on the prefab**. The wizard prompts for this at the prefab step (§3.3), not the Blender
step.

*The full game may revisit this, possibly off RetroLit entirely. Recorded here so it is not
re-litigated per prop.*

---

## 3. The Unity finish — where every path ends

**Every path in this document — character, static prop, weapon, special case — finishes here,
identically.** Written once, before the paths, because it is the deliverable and it must not be
re-derived per prop.

The definition of done is: **the fixed model, its textures, its material and its prefab are all
inside the Mr. Moonlight project, in the right folders, and Carlos has reviewed it in Unity.**
An asset sitting finished in `E:\Props` is not done. An FBX imported with no material and no
prefab is not done.

### 3.1 🤖 AUTO — files into the project

Copy the fixed model and its finished textures out of wherever they were worked on and into the
Mr. Moonlight project:

```
Assets/_Project/Art/<Category>/<Prop>/
    <Prop>.fbx                    the fixed, game-ready mesh
    T_<Prop>_BaseColor.png        AO multiplied in, pixelated
    T_<Prop>_Normal.png           half res
    M_<Prop>.mat                  created in 3.2
```

Categories, per `Docs/unity-conventions.md`: `Characters/`, `Enemies/`, `Weapons/`, `Items/`,
`Props/`, `Environment/`.

**One folder per subject — everything for it in one place.** Model, material and textures live
together, never split into parallel `Materials/` or `Textures/` trees. Name the folder for **what
the asset represents**, not what the source file was called.

`MoonlightTextureImporter.cs` applies the import settings automatically off the `_BaseColor` /
`_Normal` suffix — sRGB, filter mode, mips, compression, and a per-category size ceiling.
Nothing to set by hand, but **read the Console and confirm it fired.**

Mesh import settings: **Scale Factor 1, Materials = None, Tangents = Calculate Mikktspace,
Read/Write off, Optimize Mesh on.** Never let the importer generate materials.

### 3.2 🔧 MCP — the material

`M_<Prop>`, shader **`Retro Shaders Pro/Retro Lit`**. These are the values
`PSXMaterialMigration.cs` already bakes in as defaults, live-tuned and approved by Carlos:

| Property | Value | Note |
|---|---|---|
| `_BaseMap` | the BaseColor | |
| `_NormalMap` | the Normal | ⚠ **`_NormalMap`, not `_BumpMap`** — RetroLit renamed it |
| `_NormalStrength` | `1.0` | |
| `_FilterMode` | `1` — **Point** | |
| `_SnapMode` | `2` — **View** | vertex snapping, the one PSX effect post-processing can't do |
| `_AffineTextureStrength` | `1.0` | Carlos tuned this up from 0 on 2026-08-25 |
| `_ResolutionLimit` | Off | |
| `_ColorBitDepth` | Off | |
| `_DitherMode` | `0` — Screen | |
| `_LightMode` | `1` — TexelLit | |
| `_Glossiness` | `5` default | raise for metal |
| `_AlphaClip` | on **only** for cutout foliage/cloth | never Surface Type = Transparent |

> ⚠ **`SetInteger`, not `SetInt` or `SetFloat`.** The retro properties are `Integer`-typed and
> `SetFloat` silently fails to persist — `PSXMaterialMigration.cs` carries a comment about losing
> time to exactly this. If you set these from script, use `SetInteger`.

### 3.3 🔧 MCP — the prefab

Create it in Unity, with the material already assigned:

```
Assets/_Project/Prefabs/World/Prop_<Name>.prefab
```

| Step | Rule |
|---|---|
| Material | **Assigned on the prefab**, not left for later |
| Collider | **Box or Capsule by default.** Mesh Collider only when the silhouette genuinely matters |
| Static flags | **Occluder + Occludee static** for anything that doesn't move |
| **Light** | If it glows — lamp, LED, wolf eyes — **the Light child goes here.** This is the §2 emission decision |
| LODs | Only where the instance count justifies it |
| Transform | Verify Rotation `(0,0,0)`, Scale `(1,1,1)`. Anything else means the *export* is wrong — fix it in Blender, don't compensate here |

**Verify by reading the real state back** — actual component values off the actual prefab, not
what you intended to set. Standing rule, and it is how the terrain-tree transform bug was caught.

### 3.4 👤 CARLOS — the review gate

**Stop. Hand it over.** Carlos reviews the prop in Unity and may ask for revisions — scale,
material tuning, collider shape, how it reads in the dark.

**Do not log the prop yet.** Apply the revisions, hand back, and ask again. Only when Carlos
says it is done does §9 fire.

---

## 4. Path 1 — Animated character

The long one. **Do not batch it.** One character all the way through and verified before the
second one starts.

### 4.1 👤 CARLOS — Tripo → mesh in T-pose

Carlos's step. The model comes out **in a T-pose** — this matters enormously later.

If Tripo's texturing isn't enough, or specific details need fixing:
**→ [modddif.com](https://modddif.com/)** for the texture detail pass.

### 4.2 🔧 MCP — Blender cleanup, scale, origin

- Apply all transforms, 1 unit = 1 m, **origin at the feet**
- Retopo/cleanup as needed
- **UVs get real attention here.** Auto-unwrap is fine for props; it is *not* fine for a head.
  Texture space on a face is scarce and seams on a deforming mesh are visible

### 4.2a ⚠ Axis conversion: exporter bake vs. importer bake — validated on Spotter, 2026-09-01

**First real finding from shakedown.** Blender's default FBX export (`-Z Forward`, `Y Up`,
`bake_space_transform=False`, matching both the exporter's own default and Carlos's export
dialog) does **not** put the character's tall axis on Unity's Y. It leaves the Z-up→Y-up
conversion as a *transform-level* correction, which Unity represents as a root GameObject that
imports with a non-identity rotation (seen as 270° / -90° on X). Bounds read correctly only if
you respect that rotation — which fails the §0 ground rule that a placed instance reads Rotation
(0,0,0). Setting `ModelImporter.bakeAxisConversion = true` on the *already-imported* asset did
**not** fix it for a plain mesh with no Animator/Avatar — that flag only bakes axis conversion
for rigged content.

**The fix that worked, for this stage of the pipeline (no armature yet):** re-export from
Blender with `bake_space_transform=True` (the exporter's "Apply Transform" checkbox). That bakes
the Z-up→Y-up rotation directly into vertex data, so Unity's root imports with a clean identity
transform and correct bounds — no importer-side correction needed.

**This will not hold once AccuRig adds an armature.** `bake_space_transform` is exactly the
toggle §0's ground rule table already warns is risky for rigged/animated content (it can corrupt
bone orientations). At that point, flip the fix to the *Unity* side instead:
`ModelImporter.bakeAxisConversion = true` on the imported FBX, which is documented as the
rigged-content-safe way to bake the same axis correction. **Not yet validated** — the first
AccuRig re-export is where this gets tested for real; if `bakeAxisConversion` doesn't clean up
the root rotation for a Humanoid/Generic avatar the way it's supposed to, come back and correct
this note.

### 4.3 🔧 MCP — ⚠ Verify the T-pose *by the numbers* before AccuRig

**This is the known failure point of the entire character path.** Read §4.7 before doing this —
it explains what goes wrong and why eyeballing it is not enough.

Check, numerically, not visually. **This is fully automatable over Blender MCP and must be** —
"it looked fine" is exactly how this failure gets through.

Read the armature's bone head/tail world positions and assert:

| Check | Assertion |
|---|---|
| Arms horizontal | `upper_arm` → `hand` chain: **Y delta ≈ 0** across the whole chain (within a few mm). A drooping arm is an A-pose creeping in |
| Arms symmetric | left and right chains mirror on X — same Y, same Z, opposite X |
| Legs straight | `thigh` → `foot`: **X and Z delta ≈ 0**, pointing straight down |
| Palms consistent | hand bone roll matches between left and right |

**Report the actual numbers**, not a verdict. "Left arm Y delta 0.003 m, right 0.041 m" tells
Carlos which arm to fix; "T-pose looks off" does not.

### 4.4 👤 CARLOS — AccuRig

- **Full-body + FINGER rig enabled.** Not body-only. Finger bones are required — hand animation
  is in scope via QuickMagic
- **T-pose input mode, explicitly.** Never "auto-detect", never A-pose
- Export FBX

### 4.5 🔧 MCP — ⚠ Re-import the FBX and confirm the T-pose survived

**Before it goes anywhere near Unity or a batch job.** If AccuRig reverted the pose, fix it in
AccuRig/iClone. **Do not compensate downstream** — a downstream correction will fight every clip
you ever retarget.

### 4.6 🔧 MCP — Unity import

- Animation Type = **Humanoid**
- **Configure Avatar** → all required bones green
- **Map the optional finger slots manually.** Unity's Humanoid avatar supports fingers but does
  not reliably auto-detect them, and QuickMagic hand retargeting depends on them
- **Pose → Enforce T-Pose** as a safety net, even if §4.5 looked fine

### 4.7 Why all that ceremony — the failure this prevents

AccuRig has a documented history (Reallusion's own forums) of **not holding a custom T-pose
through to export** — it can silently revert toward its own default A-pose.

When that happens, nothing errors. The character imports, the avatar configures, the clips play.
They just play *wrong*: arms sit at a skewed rotation, because a T-pose-authored Mixamo clip is
being retargeted onto an A-posed skeleton. It reads as "the animation is bad" and sends you
looking in entirely the wrong place. The checks above cost minutes; missing it costs a day.

### 4.8 👤 CARLOS downloads · 🔧 MCP imports — animation sources

Both import as **Humanoid** so they retarget onto the AccuRig skeleton with no manual bone-name
remapping:

| Source | For |
|---|---|
| **Mixamo** | Body locomotion, the general animation library |
| **QuickMagic** | Hand/finger animation specifically — weapon handling, gestures |

### 4.9 🔧 MCP — ⚠ Test with ONE clip before scaling up

**One simple Mixamo clip — idle or walk. Confirm it is clean.** Only then import the QuickMagic
hand animations, and only then start the other characters.

Batching before this check means finding the T-pose bug four characters deep instead of one.

### 4.10 Characters needed for the demo

Tracey, Holly, Vernon, Scott (plus a stretcher variant). **Robert, William, Rylee and Shannon
are NOT needed as 3D models** — they appear only as faces in the Polaroid texture.

**Faces are 2D texture swaps, not rigs** — budget a face texture atlas per character with
expression states.

> **Not this pipeline:** Tracey's **FPS arm/hand viewmodel layer** — a first-person viewmodel
> (arms/hands + hair + held weapon) that swaps independently of her third-person body while
> staying rig-compatible, same skeleton and scale, so weapon attachment points and hand poses
> line up across both. **Its own Linear issue**, to be created when this path is first walked.

---

## 5. Path 2 — Static prop

The common case, and the one that should get fastest. Target: **Carlos answers three questions
and reviews the result.**

### 5.1 🤖 AUTO — locate and inspect the source

Find the mesh and whatever textures came with it. **Report what you actually found** — polycount,
texture set, existing maps, UV state — before doing anything. Never assume the dump folder is
tidy.

If it came as `.zip` or `.glb`, extract/convert first and say so.

### 5.2 🤖 AUTO — poly check

Read the actual triangle count. Guidance from `Docs/3d-asset-pipeline.md` §6:

| Asset | LOD0 |
|---|---|
| Rock / boulder | 100–300 tris |
| Small prop | 50–200 tris |
| Character | 3 000–6 000 tris |

**If it's already in range, do nothing.** Do not decimate a mesh that doesn't need it —
that is how you lose UVs for no reason. If it's way over, go to §8.

### 5.3 🤖 AUTO — texture pass

Ask the resolution (Q5), then run:

```bash
python Tools/pipeline/texture_pass.py run <folder> --size 512 --map-size 256
```

Inputs are named by suffix — `<Name>_BaseColor.png`, `_Normal`, `_AO`. AO is multiplied into
BaseColor automatically. Output lands in `<folder>/_out/`, nothing is overwritten in place.

**If the source is already in the target pixel-art style** (Retro Realism, and packs like it),
**do not re-quantise it.** Double-processing an already-correct image degrades it. Pass it
through instead.

**If there is no normal map and no hi-poly to bake from:** ship without one. Deriving a normal
map from albedo luminance treats *colour* variation as *height* variation — a dark leaf becomes
a dent, and it is frequently worse than no normal map at all. **Look at one before generating
forty.**

### 5.4 → **§3, the Unity finish**

Files into the project, material, prefab, and Carlos's review gate. **Identical for every path**,
which is why it lives before the paths rather than inside this one. Do not improvise a variant
of it here.

---

## 6. Path 3 — Weapon

Already solved upstream: the weapon assets arrive **finished, rigged and animated**. The only
work is making them match our look.

1. **Ask: does this weapon need a Blender cleanup pass?** Some third-party weapons are high-poly.
   **Ask — do not assume either way.** If yes → §8.
2. **Texture/palette pass** — same `texture_pass.py` run as §5.3, at **1024** by default
   (a weapon is held close to the camera and seen constantly).
3. **Material** to the §3 profile. Raise `_Glossiness` for gunmetal — this is where the scalar
   actually earns its keep.
4. **Preserve the rig and clips.** Do not re-export the mesh from Blender unless the cleanup pass
   genuinely required it — re-exporting risks the animation bindings for a texture change that
   didn't need it.
5. **Files, material, prefab, review gate — §3.** Then log it, §9.

---

## 7. Path 4 — Special case

For anything that doesn't fit. Also the path for **Carlos deviating mid-run**, which is expected
and explicitly supported.

**How to handle a deviation:** absorb the special request, do it, then **return to the path and
finish the remaining steps.** A one-off request for one prop does not cancel the pipeline for
that prop. Do not silently drop steps because the run went off-script — say which ones you're
resuming.

**Then record it.** A special case that happens twice is not a special case; it is a missing
path. §9 is where that gets noticed.

---

## 8. The high-poly branch — only when Carlos flags it

**Not the default.** Tripo delivers near-game-ready meshes, so most props never come here.
Carlos identifies high-poly sources by hand.

When he does, **stop and ask** rather than guessing:

| Option | When |
|---|---|
| **Decimate** | The silhouette survives reduction. Collapse for organic, Planar for hard-surface. **Enable "Keep UVs"** so the existing texture survives |
| **Decimate + normal-only bake** | Reduction cost visible surface detail. Bake the *pre*-decimation mesh's normals onto the reduced one to recover it |
| **Build a low-poly and bake fully** | The source is genuinely a sculpt. Unwrap the low-poly, bake Diffuse + Normal + AO, multiply AO into the diffuse |

**What baking is, in one line:** a detailed mesh has too many triangles to ship; baking transfers
its lost surface detail onto a cheap mesh as *texture* instead of geometry, by firing rays from
the cheap surface out to the detailed one.

⚠ **QuadriFlow and Hunyuan poly-cleanup both discard UVs.** Anything remeshed that way needs a
fresh unwrap and a full re-bake. There is no shortcut where the original texture is reused.

> **`Tools/pipeline/bake_prop.py` does not exist yet — deliberately.** It gets written the first
> time this branch is actually walked, against real geometry and real failures, rather than
> guessed at in advance. When you walk it: drive Blender over MCP (asking permission first), and
> **write the script as you go.** Then update this section and delete this note.

---

## 9. The write-back protocol — do not skip this

**This is why the file exists.** A pipeline that doesn't learn is just a checklist.

When Carlos confirms a prop is done (§3.4), **ask him**, then do all three:

### 9.1 Log the prop

Append an entry to **`Docs/prop-log.md`** using the template at the top of that file.

### 9.2 Fix the wizard

**If anything cost time that a rule would have prevented, edit the step in this document where
that time was lost.** Not a footnote at the bottom — the actual step, so the next run cannot
repeat the mistake.

Ask, explicitly: *what did I have to figure out this run that I should have already known?*

That is the difference between the two files:

> **The log records what happened. The wizard records what to do differently.**
> A lesson that lives only in the log is a lesson the wizard will not apply.

### 9.3 Feed the automation backlog

If a step was manual and *could* be automated, add it to §10 with a note on what blocked it.

### 9.4 Rules for editing this document

- **Prefer editing an existing step over appending a new one.** This file getting longer every
  prop is a failure mode — it should get *sharper*
- **Hot-path lessons go in `SKILL.md`, everything else here.** The skill is loaded on every
  single prop, so it stays short; this file is the one that can afford depth. Putting a niche
  lesson in the skill taxes every future run with it
- **Delete rules that stop earning their place.** A rule that never fires is noise
- **Date any decision that could be revisited** (like §2's emission call), so it can be
  re-opened deliberately rather than by accident
- **Second occurrence promotes.** A special case seen twice becomes a documented path

---

## 10. Automation backlog

What is automated today, and what is next. **Updated per §9.3.**

### Already automated

| Step | Tool |
|---|---|
| Pixelation, quantise + Bayer dither | `Tools/pipeline/texture_pass.py` |
| AO multiplied into BaseColor | `texture_pass.py`, automatic when `_AO` present |
| Source archival + rename + staging | `Tools/pipeline/prepare_asset.py` |
| Unity texture import settings | `MoonlightTextureImporter.cs`, on filename suffix |
| PSX material profile defaults | `PSXMaterialMigration.cs` |

### Next up

| Step | Status | Blocked on |
|---|---|---|
| Folder → material + prefab in one call | **Not built** | Worth building after 2–3 props, when the shape of the per-prop variation is known |
| Headless Blender bake | **Deferred** — §8 | The first prop that actually needs a bake |
| Automatic polycount report on a source folder | **Not built** | Trivial; do it when §5.2 gets tedious |

### Known gap — not introduced here, but worth knowing

`MoonlightTextureImporter.cs` decides compressed-vs-uncompressed by comparing the **ceiling**
against its 128 px threshold, not the texture's actual size. So a genuinely small texture (say
64×64) under a 512 ceiling still gets DXT-compressed, where uncompressed would be both smaller
*and* cleaner. **Pre-existing** — the old flat-512 code had the same bug — and fixing it needs
the source dimensions at preprocess time, which is not exposed by a public API. Left alone
deliberately; revisit if a small texture ever visibly bands.

### Not automatable *today* — each with its unblock

Recorded as **gaps with a named condition**, not as permanent facts. Automation is the default;
these are the exceptions, and exceptions get revisited.

| Step | Why it's manual | What would unblock it |
|---|---|---|
| **Tripo generation** | Web GUI, Carlos's creative call | An API tier, if one exists and is worth it |
| **modddif detailing** | Web GUI, per-asset judgement | — |
| **AccuRig rigging** | Standalone Reallusion desktop app, **no MCP server and no public scripting API** — unlike iClone / Character Creator, which do expose Python | Reallusion shipping an API, **or** switching the rig step to Blender (Rigify + auto-weights), which *is* MCP-drivable. Worth evaluating if AccuRig's T-pose reversion (§4.7) bites more than once |
| **QuickMagic mocap** | Recording is physical | Clip *import* and retarget is already 🔧 MCP |
| **Carlos's review gate** | It is judgement, deliberately | Nothing. This one stays |

**The T-pose verification steps around AccuRig (§4.3, §4.5) are 🔧 MCP and fully automated** —
so even though the rigging itself is manual, the failure it's famous for is caught by machine.

---

## 11. Pointers

| For | Read |
|---|---|
| Blender export conventions, Unity mesh import, LODs, vegetation budget | `Docs/3d-asset-pipeline.md` — **its §2 map set is superseded by §2 here** |
| Folder structure and naming | `Docs/unity-conventions.md` |
| Canonical prop and character names | `Docs/glossary.md` |
| Moving assets out of the Playground project | `Docs/dual-project-workflow.md` |
| What's owned, installed, and rejected | `Docs/external-assets.md` |
| Per-prop history and lessons | `Docs/prop-log.md` |

---

## 12. Known gaps — open, as of 2026-08-27

**Nothing here has been validated by a finished prop.** These are the things most likely to bite
on the first run. Each gets closed or corrected during shakedown (§0.5).

### G1 — Source texture names don't match what the tool expects ⚠ **most likely to bite first**

`texture_pass.py` keys off `_BaseColor` / `_Normal` / `_AO` suffixes. Real sources on disk do
not use them — `E:\Props\Props\RV` has `diffuse_model_bake.png` and `normal_model_bake.png`;
`E:\Props\Items\WalkieTalkie` has eight `walkie-view-*.png` reference renders and no map at all.

**There is a renaming/selection step between "found the source" and "run the texture pass" that
the hot path does not describe.** `prepare_asset.py` does it via a manifest, but the wizard never
invokes it. **Resolve on the first static prop:** either the hot path gains an explicit rename
step, or it calls `prepare_asset.py`, or `texture_pass.py` learns to recognise common vendor
names.

### G3 — Prefab destination is a guess for anything that isn't a prop

The hot path writes `Assets/_Project/Prefabs/World/Prop_<Name>.prefab`. But `Prefabs/World/`
currently holds **world systems** — `SUN`, `SkyboxSwitcher`, `TimeManager`, `Vegetation` — not
props. And `Docs/unity-conventions.md` specifies prefixes (`Item_`, `Enemy_`, `Prop_`) without
saying which folder each lives in.

**Unresolved: where do `Item_`, `Enemy_` and character prefabs go?** Ask Carlos on the first prop
that isn't a static set-dressing prop.

### G4 — `.glb` and `.zip` sources have no stated conversion path

`Items/WalkieTalkie/walkie_talkie.glb`, `Props/Well/*.glb`, `Props/MedievalDoor/*.zip`. The hot
path says "extract/convert first and say so" without saying how. Blender MCP can import glTF —
**confirm it round-trips cleanly to FBX with our conventions intact** on the first one.

### G5 — Is Tripo's own texture already in style?

Unknown. If it is, quantising it again **degrades it** (the same double-processing trap Retro
Realism has). If it isn't, it needs the full pass. **The hot path currently guesses.** Look at
one Tripo output before deciding the rule.

### G6 — Polycount reading is not automated

§5.2 says "read the actual triangle count." No tool does this — it needs Blender or Unity.
Currently a 🔧 MCP round-trip that could be a script. Cheap to fix once it becomes annoying.

### G7 — No rule for when a prop needs LODs, or which collider

Both are written as judgement ("only where the instance count justifies it", "Box or Capsule by
default"). That is honest but it means a decision per prop. **If Carlos's review gate keeps
returning the same collider correction, that judgement should become a default.**

### G8 — Material + prefab creation is unautomated, and is the real remaining time sink

The texture pass is one command. The material and prefab are hand-built in Unity every time.
`prop_import` is deliberately deferred until 2–3 props show what actually varies — but **this is
where the minutes will go** until it exists. If props #1 and #2 look near-identical at this step,
build it immediately rather than waiting for #3.

### G9 — The write-back protocol has no enforcement

§9 fires because this document says so. If a session ends early or Carlos switches tasks
mid-prop, the lesson is lost. No hook, no gate. Accepted deliberately — adding machinery before
the loop has proven useful would be premature — but it is a real hole.

### G10 — Bulk mode is completely untested

Never run. Do not use it until all three paths have cleared shakedown.

### G11 — Texture compression threshold compares the ceiling, not the real size

`MoonlightTextureImporter.cs` decides compressed-vs-uncompressed against its 128 px threshold
using the **category ceiling**, so a 64×64 texture under a 512 ceiling still gets DXT-compressed
where uncompressed would be smaller *and* cleaner. **Pre-existing** — the old flat-512 code had
the same bug. Fixing it needs source dimensions at preprocess time, which no public API exposes.
Revisit only if a small texture visibly bands.

### G12 — Bulk runs and shakedown contradicted each other in `SKILL.md`

The hot-path skill file had a "Bulk runs" section telling Claude to "run end to end" over a list,
sitting below its own shakedown banner saying "never batch." Nothing had cleared shakedown yet
(`prop-log.md` is empty), so the first real bulk request — a vegetation list from Carlos, sourced
from Playground — would have hit that contradiction directly. **Fixed 2026-08-29**: bulk runs are
now explicitly gated on shakedown clearing in `SKILL.md`.

### G13 — Playground-sourced assets aren't quite the same intake as `E:\Props` or Tripo

`SKILL.md`'s intake already named Playground as a source option, but didn't say Playground
sources are typically **pre-built asset-pack meshes** (already textured, sometimes already
low-poly) rather than raw geometry needing the full Tripo-style treatment, nor that
`Docs/dual-project-workflow.md`'s folder+`.meta` copy has to happen *before* the wizard's own
step 1. Also caught: `SKILL.md` still named the pre-move Playground path
(`E:\playground\test`) — stale since the 2026-08-28 move to `E:\playground\My project`. **Fixed
2026-08-29.**

### G14 — The Topdown-pack eye-height check was promised but never written down

`Docs/new-asset-list.md`'s Topdown Nature Library row says "**Add this check to the prop
wizard**" — checking a top-down-authored mesh's silhouette at first-person eye height in
Playground before adopting the whole set, since undersides/sides are often unfinished or flat
billboards on that kind of pack. It was never actually added anywhere in this doc or `SKILL.md`.
**Fixed 2026-08-29**, added to `SKILL.md`'s hot path step 1. Applies to Low Poly Plant
Collections too, same reasoning.

### G15 — The MeshCollider exception doesn't hold for Gaia-spawned vegetation

§3.3's collider rule allows Mesh Collider "when the silhouette genuinely matters." For anything
meant to be spawned as a Gaia **Terrain Tree** instance, that exception doesn't apply — Unity
terrain trees silently reject Mesh Colliders outright, a trap already paid for once (see
`mrm70-flora-phase-kickoff.md`'s trap table). A vegetation piece with a complex enough silhouette
to want a Mesh Collider still has to make do with Capsule/Box/Sphere if it's going in as a
terrain tree, or be spawned as a real GameObject instead (see
`mrm70-vegetation-3d-pipeline-kickoff.md`'s Terrain-Tree-vs-GameObject table for that tradeoff).
**Fixed 2026-08-29**, noted in `SKILL.md`'s prefab step.

### Closed gaps

Moved here with the date and what closed them, rather than deleted — knowing a gap existed is
useful when a related one appears.

**G2 — `.exr` maps were silently invisible. Closed 2026-08-27, before first run.**
`texture_pass.py` filtered its input by extension, so an `.exr` (sources really do ship them —
`E:\Props\Props\RV
ormal_model_bake.exr`) was not skipped-with-a-report, it was never seen at
all. The quietest possible failure in the pipeline. Fixed by splitting the extension list into
`READABLE` and `UNREADABLE_IMAGE` (`.exr .hdr .psd .dds`) and **reporting** the latter with
"CANNOT READ - convert these to PNG first". Verified against a fixture.
