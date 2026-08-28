---
name: prop-wizard
description: Guided, step-by-step 3D asset production for Mr. Moonlight — takes one prop from a source mesh to a finished Unity prefab with a RetroLit material. Use when Carlos says /prop, "let's do a prop", "work on <prop name>", names an asset in E:\Props or the Playground project, or hands over fresh Tripo output. Covers characters (rig + retarget), static props, weapons (retexture), and special cases. Also use for bulk runs over a list of props.
---

# Prop Wizard

Owner issue: **MRM-72**. Full pipeline: `Docs/3d-prop-pipeline-wizard.md`. Log: `Docs/prop-log.md`.

**The common case — a static prop — is fully contained in this file. Do not open the big doc
for it.** Only open `Docs/3d-prop-pipeline-wizard.md` when the intake sends you there.

---

## ⚠ SHAKEDOWN — no path has completed a single prop yet

**As of 2026-08-27 this pipeline has never been run.** Carlos's plan: **static prop → character
→ weapon**, then the decisions can be called solid.

**On the first run of any path, the pipeline is what's under test, not just the prop.** Read
`Docs/3d-prop-pipeline-wizard.md` **§0.5 and §12 (known gaps)** before starting — even for a
static prop, which normally never needs the big doc.

While a path is in shakedown:

- **Narrate each step before doing it**; report what actually happened after
- **Stop at every step boundary.** Don't chain steps
- **Fix the pipeline mid-run**, not at the end — the normal write-back protocol waits for
  sign-off, and during shakedown that means re-deriving the gap later
- **Never batch.** One prop
- **Report anything that surprised you, even if it worked**
- **Prefer asking over assuming**, even where this file states a default

**Delete this section once all three paths have cleared shakedown.**

---

## Intake — ask these, in order, before doing anything

1. **Which path?** character · static prop · weapon · special case
2. **Which prop, and where is it?** `E:\Props` (a *dump* — nothing in it is game-ready by
   default) · the Playground project (`E:\playground\test`) · fresh Tripo output
3. **Has Tripo already run?** Always ask, never assume. If not, stop and wait
4. **Is this a high-poly source?** Default is **no**. Carlos flags high-poly by hand
5. **Texture resolution?** **Always ask before generating any texture.**
   512 props · 1024 weapons · 1024–2048 characters · 256–512 small items.
   Normals are automatically half — don't ask separately

**Then branch:**

| Answer | Go |
|---|---|
| Static prop, not high-poly | **Stay here.** Run the hot path below |
| Character | `Docs/3d-prop-pipeline-wizard.md` **§4** — rig, T-pose verification, retarget |
| Weapon | Big doc **§6** — retexture only; ask if it needs a Blender cleanup |
| Special case | Big doc **§7** |
| High-poly flagged | Big doc **§8** — stop and ask *decimate vs. build-a-lowpoly-and-bake* |

---

## Hot path — static prop

### 1. 🤖 Inspect the source

Report what you **actually found** — polycount, texture set, existing maps, UV state — before
touching anything. Extract/convert `.zip` or `.glb` first and say so.

Poly guidance: small prop 50–200 tris, rock 100–300. **If it's already in range, do nothing.**
Never decimate a mesh that doesn't need it — that's how UVs get lost for no reason.

### 2. 🤖 Texture pass

```bash
python Tools/pipeline/texture_pass.py run <folder> --size 512 --map-size 256
```

Inputs named by suffix: `_BaseColor`, `_Normal`, `_AO`. **AO is multiplied into BaseColor
automatically.** Output goes to `<folder>/_out/`, nothing overwritten in place.

- **Source already in the target pixel-art style** (Retro Realism and similar)? **Don't
  re-quantise** — pass it through. Double-processing degrades an already-correct image
- **No normal map and no hi-poly to bake from?** Ship without one. Deriving a normal from albedo
  luminance treats *colour* as *height* — a dark patch becomes a dent. **Look at one before
  generating forty**

### 3. 🤖 Files into the project

```
Assets/_Project/Art/<Category>/<Prop>/
    <Prop>.fbx
    T_<Prop>_BaseColor.png
    T_<Prop>_Normal.png
    M_<Prop>.mat
```

Categories: `Characters/` `Enemies/` `Weapons/` `Items/` `Props/` `Environment/`.
**One folder per subject** — model, material and textures together, never split into parallel
trees. Name the folder for **what it represents**, not the source filename.

`MoonlightTextureImporter.cs` applies import settings automatically off the suffix.
**Read the Console and confirm it fired.**

Mesh import: **Scale Factor 1 · Materials = None · Tangents Calculate Mikktspace · Read/Write off
· Optimize Mesh on.**

### 4. 🔧 Material — `M_<Prop>`, shader `Retro Shaders Pro/Retro Lit`

| Property | Value |
|---|---|
| `_BaseMap` / `_NormalMap` | the two textures. ⚠ `_NormalMap`, **not** `_BumpMap` |
| `_NormalStrength` | `1.0` |
| `_FilterMode` | `1` — Point |
| `_SnapMode` | `2` — View |
| `_AffineTextureStrength` | `1.0` |
| `_LightMode` | `1` — TexelLit |
| `_DitherMode` | `0` — Screen |
| `_ResolutionLimit` / `_ColorBitDepth` | Off |
| `_Glossiness` | `5`; raise to `10–20` for metal |
| `_AlphaClip` | only for cutout foliage/cloth. **Never** Surface Type = Transparent |

⚠ **`SetInteger`, not `SetFloat`**, on the Integer-typed retro properties — `SetFloat` silently
fails to persist.

### 5. 🔧 Prefab — `Assets/_Project/Prefabs/World/Prop_<Name>.prefab`

- Material **assigned on the prefab**, not left for later
- Collider: **Box or Capsule by default**; Mesh Collider only when the silhouette matters
- **Occluder + Occludee static** if it doesn't move
- **If it glows** — lamp, LED, wolf eyes — **add a real Light child here.** There are no emission
  maps in this project
- Transform must read Rotation `(0,0,0)`, Scale `(1,1,1)`. Anything else means the *export* is
  wrong — fix it in Blender, don't compensate here

**Verify by reading real state back** off the actual prefab, not what you meant to set.

### 6. 👤 Carlos's review gate

**Stop and hand over.** He reviews in Unity and may ask for revisions — scale, material, collider,
how it reads in the dark. Apply them, hand back, ask again. **Don't log it until he says done.**

---

## Non-negotiables

- **Ask permission before each piece of Unity or Blender MCP work** — then do it, verify by
  reading real state back, document. `CLAUDE.md` hard rule
- **Two maps only.** RetroLit samples nothing else — no mask, no metallic, no emission
- **`Filter Mode = Point` on BaseColor**, never pixelate a Normal
- **Tolerate deviation.** A special request mid-run is expected. Absorb it, then say which steps
  you're resuming and finish the path

---

## When Carlos confirms it's done — the write-back protocol

This is the point of the wizard. **Ask him, then do all three:**

1. **Append the entry** to `Docs/prop-log.md`
2. **Edit the pipeline itself** wherever this prop cost time a rule would have prevented — the
   actual step, in this file if it's a hot-path lesson, in the big doc otherwise. Ask yourself:
   *what did I have to figure out that I should have already known?*
3. **Add any newly-visible automation opportunity** to the big doc's §10 backlog

> The log records what happened. The pipeline records what to **do differently**.
> A lesson that lives only in the log will not be applied.

**Keep this file short.** It is the hot path and it is loaded every single prop. Edits should
make it *sharper*, not longer — if a lesson is niche, it belongs in the big doc. Delete rules
that stop earning their place.

## Bulk runs

Carlos may hand over a list. Run end to end, stopping only where a prop genuinely needs a
decision. Batch questions — one resolution answer can cover a whole set. Log each one.
