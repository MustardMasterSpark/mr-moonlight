# Retarget Pro V5 — adoption ruling and working strategy

**Ruled 2026-08-31. Written to be read cold by Sonnet.** Everything below was verified by reading the
real files in Playground (`E:\playground\My project`), not from store pages or memory.

---

## 0. The 60-second version

| Question | Answer |
|---|---|
| Which animation asset do we use? | **Retarget Pro V5** (KINEMATION). |
| Which do we skip? | **FPS Animation Baker Toolkit** (CatRabbit). Skip it. |
| Does Retarget Pro go into Mr. Moonlight? | **No. It never enters the project.** It lives in Playground. Only **baked clips** cross over. |
| Does it fix our weapon animations? | **No — and nothing needs to.** Every FP weapon animation the demo needs is already owned (§2). |
| So what is it actually for? | **Enemies and Tracey's body**: the Wendigo, the wolf (a quadruped), and full-body Tracey (§3). |
| Was the console error its fault? | **No.** Pre-existing Crest misplacement in Playground (§7). |

> ### ⚠️ The single most important fact in this document
>
> **The reason we were going to buy an FPS animation tool no longer exists.** `Docs/new-asset-list.md`
> and `Claude Code Context MDs/Assets MDs/Toolkit.md` both justify the FPS Animation Baker Toolkit
> with *"roughly 15 hand animations across the demo that Claude cannot author."* Those 15 animations
> **are already in the project's owned assets**, on one shared skeleton. Verified clip-by-clip in §2.
> Do not re-open this. Do not buy an FPS animation tool for the weapons.

---

## 1. What Retarget Pro actually is (verified from source)

Package: `E:\playground\My project\Assets\PLAYGROUND\Retarget Pro V5\` — 16 MB, 111 C# files,
plus an 8.8 MB `RetargetPro/Documentation.pdf` inside the package.

It transfers animation from a **source rig** to a **target rig** and bakes the result to a
**standard Unity `AnimationClip` or an FBX**. It is an **editor tool**. Nothing it produces depends
on it at runtime.

**Assemblies** (`.asmdef`, read directly):

| Assembly | Platforms | Ships in a build? |
|---|---|---|
| `RetargetPro.Editor` | Editor only | No |
| `KAnimationCore.Editor` | Editor only | No |
| `FbxExporter.Editor` | Editor only | No |
| `RetargetPro.Runtime` | All platforms | Only if you put `RetargetProComponent` on a GameObject — **we never will** |
| `KAnimationCore.Runtime` | All platforms | Same |

**The seven features it ships** (`RetargetPro/Runtime/Features/`):
`Basic` · `IK` · `FPS` · `Root/Pelvis` · `Mirror` · `Transform Modifier` · `Copy Bone`.

**Why it beats Unity's built-in retargeting — the part that matters to us.** Unity's Humanoid
retargeting requires **both** rigs to be Humanoid and maps through the fixed `HumanBodyBones`
enum. Retarget Pro has **no `HumanBodyBones` dependency anywhere**. `RetargetProfile` takes two plain
`GameObject`s and composes rig data "from the model hierarchy and skinned mesh bones"
(`RetargetPro/Runtime/RetargetProfile.cs`). That means it works on:

- Humanoid → Generic (impossible in Unity)
- Generic → Generic (impossible in Unity)
- **quadrupeds and creatures** (impossible in Unity)
- partial rigs with no hips/spine/legs, e.g. an arms-only viewmodel

**Bundled T-pose presets** (`RetargetPro/Poses/`): `A_TPose_UE4`, `A_TPose_UE5`, `A_TPose_Mixamo`,
`A_TPose_Synty`, `A_TPose_CC4`, `A_TPose_Humanoid`. Two of these matter to us — see §3.1.

**Baking** (`Editor/Scripts/Bakers/`): `GenericAnimationBaker` walks the target hierarchy, samples
local TRS per bone per frame into `AnimationCurve`s with linear tangents, and writes an
`AnimationClip`. `HumanoidAnimationBaker` is a thin wrapper that **delegates to the generic one** —
so the "no Humanoid baking" line on the public welcome page is **out of date for v5**; both work.
Batch mode: select many clips → right-click → *Retarget Animations* → assign profile → *Bake*.

---

## 2. Why we do NOT need it for weapons — the clip inventory, verified

`HQ FPS Weapons 2.0` (owned, staged in Playground) ships **19 first-person weapons whose FBXs all
copy their avatar from one file**: `HQFPS/Art/Meshes/Wieldables/Arms/FP_Arms.fbx`,
guid `81dcb03d49282f446b0aaf2b603ea7e8`. **Any weapon's clip plays on any other weapon's rig.**

Enumerated from the actual FBXs on 2026-08-31:

| Weapon | Clips | Covers |
|---|---|---|
| **M1911** | 8 — AimFire, Equip, Fire, Hold, Idle, Reload, ReloadEmpty, Unequip | **MRM-22 — the exact pistol the issue names** |
| **DoubleBarrelShotgun** | 8 — AimFire, Equip, Fire, Hold, Holster, Idle, Reload, ReloadEmpty | **MRM-24 — the exact shotgun the issue names** |
| **BaseballBat** (the Club) | 6 — ComboAttack, Equip, Hold, Idle, StrongAttack, Unequip | **MRM-23** swings 1 and 2 |
| **FireAxe** | 5 — Equip, Hold, Idle, StrongAttack, Unequip | **MRM-23** swing 3 (the 60-frame overhead) |
| **CombatKnife** | 6 — Equip, Hold, Idle, Stab1, Stab2, Unequip | spare swings if needed |
| **Flashlight** | 5 — Equip, Hold, Idle, Switch, Unequip | **MRM-43** map/compass handling analogue |
| every gun | `Aim` / `AimFire` | **MRM-21** aim down sights |
| every weapon | `Equip` / `Unequip` / `Holster` | **MRM-25** weapon switching |

Also shipped and unused by the demo: AKM, Crossbow, F1, FlareGun, FragGrenade, HuntingRifle, M1A,
MP5, MolotovCocktail, R870, Revolver, Syringe.

**Conclusion.** MRM-21, MRM-22, MRM-23, MRM-24, MRM-25 and MRM-43 need **zero new animation and zero
animation tooling**. They need the migration already written up in MRM-23 ("Files to migrate") and
our own gameplay code. If you are picking up one of those issues: **do not import an animation tool.**

### 2.1 The `FP_Arms` rig, for reference

`animationType = Generic`, 54 transforms:

```
FP_Arms
  Arm_Shirt&Gloves_l / _r / Arm_Standard&Gloves_l / _r ...   (8 skinned mesh variants)
  Arms_Root
    UpperArm.L → Forearm.L → ForearmTwist.1-4.L
                           → Hand.L → Index/Middle/Pinky/Ring/Thumb .1-.3 .L
    UpperArm.R → Forearm.R → ForearmTwist.1-4.R
                           → Hand.R → (same five fingers)
```

Two things to know if you ever *do* run an FPS retarget onto it:

1. The arm chain Retarget Pro wants ("upper arm, forearm, hand") is exactly
   `UpperArm.L → Forearm.L → Hand.L`. It needs **≥ 3 bones** and the source chain must be the
   **same length** (`FPSRetargetFeature.IsArmPairValid`).
2. **There is no weapon bone on this rig.** No `ik_hand_gun`, nothing. HQ FPS Weapons drives the
   weapon straight off `Hand.R`. `FPSRetargetFeature` requires a weapon chain on **both** sides, so
   you must add one: create an empty GameObject under `Hand.R` at the grip, name it `weapon_bone`,
   save the model as a **prefab variant**, and point the profile at the variant. This is the
   documented workflow, not a hack.

---

## 3. What Retarget Pro IS for — the three real jobs

### 3.1 The Wendigo (MRM-36, MRM-31, MRM-32, MRM-37)

Verified: `Assets/PLAYGROUND/Wendigo Forest Beast Collection/Mesh/Wendigo.fbx` is
`animationType = Human` on an **Unreal Engine mannequin skeleton**:

```
root
  ik_foot_root → ik_foot_l / ik_foot_r
  ik_hand_root → ik_hand_gun → ik_hand_l / ik_hand_r
  pelvis → spine_01 → spine_02 → spine_03 → clavicle_l/r → upperarm → lowerarm → hand
                                          → neck_01 → head → Jaw
        → thigh_l/r → calf → foot → ball
```

**Because it is Humanoid, Unity's own retargeting will technically work.** Retarget Pro is not
unblocking anything here — it is buying **quality**. The Wendigo has deliberately non-human
proportions (long limbs, hunched spine), and that is precisely the case where Unity's
`HumanBodyBones` mapping produces sliding feet, floating hands and a rubbery spine. Retarget Pro maps
per-bone-chain with IK correction instead.

It also matches the bundled `A_TPose_UE4` / `A_TPose_UE5` poses exactly, so setup is a preset pick,
not a manual mapping job.

### 3.2 The wolf (MRM-33) — the one true capability gap

MRM-33 needs a wolf, alone and in a pack. **A wolf is a quadruped. Unity cannot retarget onto a
quadruped at all** — Humanoid avatars have no mapping for it, and there is no workaround. Retarget
Pro's `Basic` + `IK` + `Root/Pelvis` features are bone-chain based and have no such limit; quadruped
support is one of its advertised cases.

**⚠️ No wolf model is present in Playground.** MRM-33's asset block claims *Wolfenemu / Monster Wolf
Boss — already staged in Playground at `Assets/Wolf_enemu/`*; **that folder does not exist**, and no
directory matching `*wolf*` exists anywhere under Playground's `Assets/`. Flagged on MRM-33 and open
for Carlos. Until a model arrives this is the one part of the ruling that cannot be validated — but
when one does arrive, this is the only tool in the project that can animate it.

### 3.3 Tracey's full body (MRM-9's unmet criterion)

`Docs/mrm9-burntwax-integration.md` records that **"looking straight down shows placeholder body
geometry" is currently NOT met** and is owed back once a real Tracey model exists. The
*"Tracey must see her own feet"* requirement is also the stated reason FPS Engine was rejected
(`Docs/external-assets.md`, `Docs/new-asset-list.md` §1).

When the Tracey model exists, her locomotion has to come from somewhere. It comes from the owned
mocap (§4), retargeted onto her rig. That is this tool's job.

### 3.4 The build-size argument, which is bigger than it looks

`Ultimate Animation Collection` holds **3,068 Humanoid clips across 3,121 models**. `new-asset-list.md`
flags it as *"one of the three real build-size risks — import only the clips actually used."*

Retarget Pro's **batch bake** turns that warning into a procedure: select the ~20 clips we actually
want in Playground, bake them onto our own rig, and migrate **only the baked clips**. The 3,068-clip
library never enters Mr. Moonlight. This is a measurable win against a documented risk, and it is
the reason the tool earns its place even where Unity's retargeting would have worked.

---

## 4. The animation libraries we own (all Playground, all Humanoid)

| Pack | Models | Clips | Type |
|---|---|---|---|
| Ultimate Animation Collection | 3,121 | **3,068** | Human |
| Knife MocapAnimPack | 196 | 206 | Human |
| Cult Animations | 29 | 29 | Human |

All three are Humanoid sources, so they feed Retarget Pro directly with no preparation.

---

## 5. 🔴 The working rule: Retarget Pro never enters Mr. Moonlight

This is the whole strategy in one line, and it is the same shape as the Gaia Pro adoption
(*editor-tools only*) and the dual-project workflow.

```
  PLAYGROUND  (E:\playground\My project)          MR. MOONLIGHT  (E:\MrMoonlight)
  ────────────────────────────────────────        ──────────────────────────────
  Retarget Pro V5          ← the tool
  Ultimate Animation Coll. ← 3,068 clips
  Cult Animations          ← 29 clips
  Knife MocapAnimPack      ← 206 clips
  Wendigo / wolf / Tracey  ← target rigs
            │
            │  bake  →  N standalone clips
            └──────────────────────────────────→  Assets/_Project/Art/Animations/<Character>/
                                                  (plain .anim or .fbx — zero dependency)
```

**Consequences, and they are all good:**

1. **Mr. Moonlight's build footprint from this asset is zero bytes.** No asmdef, no runtime
   component, nothing to strip before shipping.
2. If Retarget Pro ever disappoints or breaks on a Unity upgrade, **the baked clips still work** —
   they are ordinary `AnimationClip`s.
3. It cannot conflict with Burntwax, Blaze AI or anything else in the project, because it is not there.
4. `Assets/PLAYGROUND/` is git-ignored, so nothing about this is committed to the game repo.

**Do not migrate the `Retarget Pro V5` folder into `E:\MrMoonlight`.** If you think you need to,
you have misread the task — re-read this section.

---

## 6. The procedure — step by step, for Sonnet

Run all of this in **Playground** (`unityMCP_playground`, port 8081). Ask Carlos before doing
editor work, per the `CLAUDE.md` hard rule.

### 6.1 First time only — prove it on one clip

Do this before anyone plans work around the tool.

1. `Assets → Create → KINEMATION → Retarget Profile`. Name it `RP_<Target>`.
2. **Source Character** = a model from the animation pack you want to steal from
   (e.g. any `Humanoid@*.fbx` under `Ultimate Animation Collection`).
3. **Target Character** = the model you want the animation on (e.g. `Wendigo.fbx`).
4. **Source Pose / Target Pose** = pick from `Retarget Pro V5/RetargetPro/Poses/`.
   Wendigo is a UE mannequin → `A_TPose_UE4`. Check the source pack's skeleton before choosing;
   if unsure, `A_TPose_Humanoid` is the safe default for Unity Humanoid sources.
5. Open the **Retarget Pro window**, generate/verify the source and target rigs, then add features
   in this order: `Root/Pelvis` → `Basic` (spine, arms, legs) → `IK` if feet slide.
6. Bake **one** clip. Look at it in the preview. **Then** decide whether to batch.

**Do not batch-bake before one clip has been looked at by a human.** A wrong pose pick produces 200
subtly broken clips just as fast as 200 good ones.

### 6.2 Batch, once one clip is approved

Select the clips → right-click → **Retarget Animations** → assign the profile → set **Rig type**
(`Generic` or `Humanoid`), **FPS**, root motion → **Bake Animations**.

Export format: **FBX is recommended by the tool**; `AnimationClip` is fine and simpler for clips that
will only ever be played by an Animator in this project. Prefer `AnimationClip` unless you need to
re-import into Blender.

### 6.3 Only if you ever retarget onto `FP_Arms` (you probably never will — see §2)

1. Add a `weapon_bone` empty under `Hand.R` on both source and target; save prefab variants.
2. In *Bone Chain Settings*, add the **FPS** feature **after arm retargeting and before the finger
   features**.
3. Map: source/target right arm (3 bones), left arm (3 bones), weapon chain (≥ 1 bone).
4. Add **five** `Basic` features for the finger chains (thumb, index, middle, ring, pinky).
5. Position hands/weapon/elbows with the gizmo handles on the IK target cubes.

**Auto-mapping gotcha:** the tool fuzzy-matches bone names (`FuzzySharp.dll`). `FP_Arms` has
`ForearmTwist.1-4` bones; the tool's avoid-list includes `twist` and `roll`, so they are correctly
skipped — but **always eyeball the mapping before baking**, because the avoid-list is name-based and
our rigs did not come from Kinemation.

### 6.4 Where baked clips land in Mr. Moonlight

`Assets/_Project/Art/Animations/<Character>/` — e.g. `.../Animations/Wendigo/Wendigo_Idle.anim`.
Name them `<Character>_<Action>` and record the source clip in `Docs/changelog.md` so the provenance
is not lost.

---

## 7. The Playground console error — diagnosed, NOT Retarget Pro's fault

**Retarget Pro compiles clean.** It produces **zero errors** and **11 `CS0618` obsolete warnings**
(deprecated `TreeView` / `TreeViewItem` / `TreeViewState` in `KAnimationCore/Editor/Rig/RigTreeView.cs`,
and one `EditorUtility.InstanceIDToObject` in `ProfileContextMenu.cs`). Those are Unity 6.3
deprecations in the vendor's code, cosmetic, and safe to ignore.

The two real errors in the Playground console are **both pre-existing and unrelated**:

**1. `DirectoryNotFoundException: ...\Packages\com.waveharmonic.crest\Runtime\Shaders\Library\Settings\Settings.Crest.iOS.hlsl`**

Root cause: **Crest Water 5 was folder-copied into `Assets/PLAYGROUND/Crest Water 5/` instead of
living at `Packages/com.waveharmonic.crest/`.** Its C# hardcodes the package path in nine
`[GenerateHLSL(sourcePath = "Packages/com.waveharmonic.crest/...")]` attributes
(`Editor/Scripts/ShaderSettings.cs`, `Runtime/Scripts/WaterRenderer.SerializedFields.cs`). URP's
`CSharpToHLSL` generator tries to write generated `.hlsl` into that folder on every recompile. The
folder does not exist — Crest is not in `Packages/` in Playground at all, and is not in
`manifest.json` or `packages-lock.json` either.

**Why it looked like Retarget Pro caused it:** importing any package triggers a script recompile,
the recompile runs the shader generator, and the generator throws. Retarget Pro surfaced a
pre-existing bug; it did not create one.

**Note it does NOT affect Mr. Moonlight.** There, Crest correctly lives at
`E:\MrMoonlight\Packages\com.waveharmonic.crest\` as an embedded package, which is exactly why this
error does not appear in the game project. See `mrm71-crest-water-kickoff.md`.

Fix options for Playground (both need Carlos's go-ahead — neither is urgent, the error is inert):
- **Move** `Assets/PLAYGROUND/Crest Water 5/` → `Packages/com.waveharmonic.crest/` (it already has a
  `package.json`, so this is a straight move and matches Mr. Moonlight's layout); or
- **Delete** `Assets/PLAYGROUND/Crest Water 5/` — Crest already migrated to Mr. Moonlight and
  MRM-71 no longer needs the Playground copy.

Do **not** create an empty `Packages/com.waveharmonic.crest/` folder to satisfy the path — a folder
under `Packages/` without a `package.json` makes the Package Manager error instead.

**2. `custom elements added to the Unity Editor's main toolbar using unsupported methods`**

Source: `Assets/PLAYGROUND/HQ FPS Weapons 2.0/FPSCore/3rdParty/EditorToolbox/Editor/ToolboxEditorToolbar.cs:89`.
A Unity 6.3 deprecation notice from HQ FPS Weapons' bundled EditorToolbox, present since that pack
was staged in May. Cosmetic. **It will not follow us into Mr. Moonlight** — MRM-23's migration list
takes FBXs and textures only, never `FPSCore/`.

---

## 8. Why the FPS Animation Baker Toolkit is rejected

`assetstore.unity.com/packages/tools/animation/fps-animation-baker-toolkit-370556` — CatRabbit, $22.

1. **The job is already done.** §2 — the 15 hand animations it was bought for are owned. This alone
   settles it.
2. **Unverifiable.** Version 1.0, released May 2026, **"not enough ratings"** (zero reviews), no
   feature list on the store page, no public documentation, no forum thread, no search footprint.
   The documentation is inside the package, so it cannot be evaluated before buying.
3. Against a Sept 1 gate and an October Kickstarter, a v1.0 from an unknown publisher is a bet that
   cannot be de-risked in advance. The cost is not the $22, it is a lost day.

Retarget Pro, by contrast: KINEMATION (FPS Animation Framework, Tactical Shooter Pack, PRAS),
**v5.2.1** updated 2026-08-26, 31 reviews, full public docs at `docs.kinemation.xyz` **plus an
`llms.txt` index** — which is why the "documentation is bad" reviews matter much less to us than to
a solo buyer: Claude can read the whole doc set directly.

### 8.1 ⛔ Do not let this pull in FPS Animation Framework

KINEMATION's sibling asset, **FPS Animation Framework**, is a *runtime* system. Adopting it would
reopen MRM-9/12/21/22/25 exactly the way **FPS Engine (cowsins) was rejected** for
(`Docs/new-asset-list.md` §1, `Docs/external-assets.md`), and it fights the Burntwax controller swap
that MRM-9 just completed. **Retarget Pro is the editor-only, bake-out half. That is the half we want.**

---

## 9. Corrections this document makes to existing docs

| Document | What changes |
|---|---|
| `Claude Code Context MDs/Assets MDs/Toolkit.md` | Its "**Buy** the FPS Animation Baker Toolkit — the clearest buy on the list" verdict is **reversed**. It was written before HQ FPS Weapons was inventoried. |
| `Docs/new-asset-list.md` §R3.1 | *"Unity retargeting only works between Humanoid avatars, so no third-person full-body animation can be retargeted onto these arms"* — **true of Unity, no longer true of the project.** Retarget Pro can do it. It is moot for MRM-23 (Round 3 solved that with owned clips), but it is no longer a structural wall. |
| `Docs/new-asset-list.md` §207, `Docs/external-assets.md` | *"FPS Animation Baker Toolkit therefore stays"* — **superseded.** It is rejected. |
| `Docs/new-asset-list.md` (Ultimate Animation Collection) | *"import only the clips actually used"* now has a mechanism: bake in Playground, migrate clips only (§3.4). |

---

## 10. Open risks

1. **Retarget Pro's own GUI is still not yet proven on our rigs.** §6.1 is the validation, and it
   must happen before any issue schedules work around the tool itself. **~30 minutes.** Note:
   this is *not* the same as "no clip has ever been retargeted onto our rig" — see §11, which
   used a different mechanism (Unity's built-in Humanoid retarget, baked via
   `GameObjectRecorder`) to get real clips onto the Spotter without touching this GUI. §11's
   method has no IK/foot-sliding correction; that gap is exactly what proving Retarget Pro's own
   GUI would buy.
2. **No wolf model is staged.** MRM-33's quadruped argument is the strongest reason to own this tool
   and the one we cannot test yet. MRM-33 says a wolf pack is staged at `Assets/Wolf_enemu/`; it is
   not there. Open question for Carlos, flagged on the issue.
3. **Source-pose choice is the main quality lever.** A wrong `A_TPose_*` pick is the most likely way
   to get bad output that looks like the tool's fault. Bake one clip and look at it (§6.1).
4. **Unity 6.3 deprecation drift.** 11 `CS0618` warnings today. Harmless now; if a future Unity turns
   them into errors, the tool is editor-only and in Playground, so the game is never at risk — and
   the already-baked clips are unaffected.

---

## 11. Method B — importing an external clip and baking it via Unity's built-in Humanoid
retarget, no Retarget Pro GUI (proven, MRM-75, 2026-09-01)

**Everything in this section happens in Playground (`E:\playground\My project`), never in
Mr. Moonlight — same rule as §5.** Use this when the source and target are both Humanoid avatars
(fingers not required to match) and you don't need IK foot-sliding correction. It sidesteps
Retarget Pro's bone-chain GUI entirely, which matters because that GUI has no safe way to drive
blind: §6.1 explicitly requires a human to look at the preview before trusting a bake, and
scripting the window via reflection with no eyes on it defeats that safeguard. Reach for Method A
(§6) instead when the target has non-human proportions (Wendigo) or isn't Humanoid at all
(a quadruped) — Unity's built-in retargeting can't do either of those, which is the whole reason
Retarget Pro was adopted in the first place (§3).

### 11.1 Getting the source clip

- **⚠️ Check "In Place" on Mixamo's preview panel BEFORE downloading anything — this is the single
  most important step in this whole section.** It sits right below "Mirror" in the same parameters
  panel as Overdrive/Character Arm-Space/Trim. Checking it strips root translation out of the clip
  at the source, before it's ever exported. **Skipping this cost an entire session on MRM-75**
  (2026-09-01): five different debugging passes — a Transform-vs-muscle-curve binding bug, a stale
  `HumanPoseHandler` read, a misunderstood `keepOriginalPositionY` setting that turned out to be
  inert for an already-baked clip, then finally hand-flattening `RootT.x/y/z` to zero after baking
  — all in an attempt to fix root drift that a single checkbox at the source would have prevented
  outright. **The lesson: always check the free, built-in option at the source before writing any
  code to fix something after the fact.** Re-download every existing clip with this checked before
  repeating any of the work below.
- **Mixamo**: upload the target character (or reuse one already uploaded to the account), pick a
  clip, hit **Download**. In the dialog: **Skin = Without Skin** unless you specifically want the
  bundled mesh too — this is what keeps the download to a few hundred KB instead of several MB,
  and it's all that's needed since only the skeleton + keyframes get used. Format
  `FBX Binary (.fbx)`, FPS and Keyframe Reduction can stay at their defaults (30 / none) unless a
  reason emerges to change them.
- **Sketchfab or any other source with a full mesh attached**: no special handling needed — a
  bundled mesh doesn't block anything below, it's simply unused. Don't waste time stripping it out
  before import.
- **Provenance**: keep the raw downloaded file's origin visible in the imported asset's name (e.g.
  `Src_<Purpose>_<SourceHint>.fbx`) rather than renaming it away — it's the only record of where a
  clip came from once it's sitting in the project.

### 11.2 Import into Playground

`import_model_file`, `output_folder` = the same folder as the target character (e.g.
`Assets/Character Playground/<Character>/`), `animation_type = "humanoid"`.

### 11.3 ⚠ Check `globalScale` on the TARGET always — on the source, only if you'll look at it directly

**Clarified 2026-09-01, after a second Mixamo clip (`Reloading.fbx`) showed proportions matching
the already-baked sources exactly, at a scale that looked "wrong" by the check below — it wasn't.**
The source clip's own absolute scale does not affect this bake method's output at all. §11.5's
`GameObjectRecorder` samples the **target's** bones, not the source's — Humanoid retargeting
works in muscle-space (normalized joint angles), so the source rig is purely a puppet driving
motion; its own bone lengths never reach the baked clip. Skip the scale check entirely for a
source you're only ever going to retarget-and-bake, never render or use as a target elsewhere.

The target character still needs this check — same ground rule as `3d-prop-pipeline-wizard.md`
§4.6, restated here because it bites external animation sources just as often as rigged
characters:

- A clean Mixamo "Without Skin" download usually self-corrects: `fileScale` reads correctly and
  `useFileScale` fixes it, landing on `globalScale = 1` with no manual change needed. Verify, don't
  assume.
- Anything authored in a DCC tool at centimeter scale with no reliable unit metadata — AccuRig
  exports, and (confirmed this session) a Sketchfab-hosted re-upload of a Mixamo clip — needs
  `globalScale` set explicitly (`0.01` worked both times so far, but recalculate per file, don't
  hardcode it).
- **Calibrate off a stable bone-to-bone distance (e.g. thigh length, hip-to-knee, ~0.4–0.45 m for
  an adult), not the overall bounding box.** A source clip that starts mid-fall, curled up, or
  convulsing throws the bounding box wildly off; a same-side limb bone pair doesn't lie to you the
  way a pose-dependent bounding box does.

### 11.4 Confirm both rigs are actually Humanoid

Both the source clip's avatar and the target character's avatar need `isValid == true` and
`isHuman == true` (`AssetDatabase.LoadAllAssetsAtPath` → find the `Avatar` sub-asset → check both
fields). If either is false, this method cannot work — that clip needs Method A (§6) instead,
since Retarget Pro has no `HumanBodyBones` dependency.

### 11.5 Bake: temp controller + `HumanPoseHandler` — NOT `GameObjectRecorder`

**⚠️ Corrected 2026-09-01, after the first batch of five clips baked this way played as an
invisible/frozen model in Carlos's Animator preview.** The original version of this section used
`GameObjectRecorder`, which only knows how to write raw per-bone `Transform` curves
(position/rotation/scale). That is a real bug, not a preview-only quirk: a **Humanoid** Animator
(which is what every character in this project uses) refuses to let generic Transform curves
drive bones it already owns for muscle-space posing. The Inspector shows this as a binding
warning ("Some generic clip(s) animate transforms that are already bound by a Humanoid avatar.
These transforms can only be changed by Humanoid clips") and at runtime it doesn't error — it
just silently fails to produce a valid pose, which reads exactly like "the model disappeared."
Confirmed: `GameObjectRecorder`-baked clips read `AnimationClip.humanMotion == false`.

The fix is to bake **Humanoid muscle curves** instead of Transform curves, via
`UnityEngine.HumanPoseHandler` — the same curve format (`EditorCurveBinding` on `typeof(Animator)`
with a muscle name, plus `RootT.x/y/z` + `RootQ.x/y/z/w` for root motion) that Unity's own FBX
importer produces for a real Humanoid animation. Interestingly, Retarget Pro's own
`GenericAnimationBaker.cs` (`Editor/Scripts/Bakers/`) has this exact same Transform-curve
limitation for body bones — it only handles root motion through the correct `Animator`-typed
curves (see its `WriteRootMotion` method), which is presumably why the tool's documented workflow
recommends an FBX export/reimport round-trip rather than using its `AnimationClip` output
directly on a Humanoid Animator. The `HumanPoseHandler` approach below sidesteps that round-trip
entirely and stays in pure C#, with no FBX file ever touching disk.

Temporary `AnimatorController` with one state whose motion is the source clip, played on an
instance of the **target's own rig** (this is what makes Unity's Humanoid muscle-space
retargeting kick in), then re-read back out as muscle values every frame:

```csharp
var targetInst = (GameObject)Object.Instantiate(targetPrefab);
var animator = targetInst.GetComponent<Animator>();
animator.applyRootMotion = true;   // required, or root motion (falls, lunges) gets lost

var tempController = new AnimatorController();
tempController.AddLayer("Base Layer");
var state = tempController.layers[0].stateMachine.AddState("Bake");
state.motion = sourceClip;
animator.runtimeAnimatorController = tempController;

var poseHandler = new HumanPoseHandler(animator.avatar, targetInst.transform);
var humanPose = new HumanPose();
int muscleCount = HumanTrait.MuscleCount;              // 95
string[] muscleNames = HumanTrait.MuscleName;
var muscleCurves = new AnimationCurve[muscleCount];
for (int m = 0; m < muscleCount; m++) muscleCurves[m] = new AnimationCurve();
var rootTPos = new[] { new AnimationCurve(), new AnimationCurve(), new AnimationCurve() };
var rootQRot = new[] { new AnimationCurve(), new AnimationCurve(), new AnimationCurve(), new AnimationCurve() };

animator.Play("Bake", 0, 0f);
animator.Update(0f);

float dt = 1f / (sourceClip.frameRate > 0 ? sourceClip.frameRate : 30f);
int frameCount = Mathf.CeilToInt(sourceClip.length / dt);
for (int i = 0; i <= frameCount; i++) {
    animator.Update(dt);
    float t = i * dt;
    poseHandler.GetHumanPose(ref humanPose);
    for (int m = 0; m < muscleCount; m++) muscleCurves[m].AddKey(t, humanPose.muscles[m]);
    rootTPos[0].AddKey(t, humanPose.bodyPosition.x);
    rootTPos[1].AddKey(t, humanPose.bodyPosition.y);
    rootTPos[2].AddKey(t, humanPose.bodyPosition.z);
    rootQRot[0].AddKey(t, humanPose.bodyRotation.x);
    rootQRot[1].AddKey(t, humanPose.bodyRotation.y);
    rootQRot[2].AddKey(t, humanPose.bodyRotation.z);
    rootQRot[3].AddKey(t, humanPose.bodyRotation.w);
}

// write into the EXISTING placeholder clip asset if one exists — preserves its GUID, so any
// AnimatorController state already pointing at it stays wired with zero extra work
existingClip.ClearCurves();
for (int m = 0; m < muscleCount; m++) {
    var binding = EditorCurveBinding.FloatCurve("", typeof(Animator), muscleNames[m]);
    AnimationUtility.SetEditorCurve(existingClip, binding, muscleCurves[m]);
}
string[] rootTNames = { "RootT.x", "RootT.y", "RootT.z" };
string[] rootQNames = { "RootQ.x", "RootQ.y", "RootQ.z", "RootQ.w" };
for (int i = 0; i < 3; i++) AnimationUtility.SetEditorCurve(existingClip, EditorCurveBinding.FloatCurve("", typeof(Animator), rootTNames[i]), rootTPos[i]);
for (int i = 0; i < 4; i++) AnimationUtility.SetEditorCurve(existingClip, EditorCurveBinding.FloatCurve("", typeof(Animator), rootQNames[i]), rootQRot[i]);
EditorUtility.SetDirty(existingClip);
```

**Sanity check this actually worked**, before anything else: `existingClip.humanMotion` must read
`true`. If it reads `false`, something in the muscle-curve writing above didn't take and the clip
will fail the exact same way.

**⚠️ Second correction, same day, caught by Carlos in the actual Animator preview: the loop above
can produce `humanMotion == true` with a valid `RootT`/`RootQ` (so the character visibly moves
through space) while every body muscle curve is completely flat — the character travels in a
frozen T-pose.** Root motion and body muscles are evidently *not* equally reliable to read back
via `HumanPoseHandler.GetHumanPose()` immediately after `animator.Update()` in an Editor-script
context (outside Play Mode): root motion came through correctly every time; muscle data came back
as a constant 0 for every key, silently, with no error.

**The actual fix — and it's simpler than the loop above for the muscle part:** Humanoid muscle
values are avatar-independent by design (that's the entire mechanism retargeting relies on), so
there's no need to re-derive them by sampling a live Animator at all — **copy them straight from
the source clip's own muscle curves onto the destination clip**:

```csharp
string[] muscleNames = HumanTrait.MuscleName;
for (int m = 0; m < HumanTrait.MuscleCount; m++) {
    var binding = EditorCurveBinding.FloatCurve("", typeof(Animator), muscleNames[m]);
    var curve = AnimationUtility.GetEditorCurve(sourceClip, binding);   // null if that muscle
    if (curve != null) AnimationUtility.SetEditorCurve(destClip, binding, curve);  // was never keyed
}
```

A `null` curve for a given muscle (commonly ~40 of the 95, almost always the finger muscles) just
means the source mocap never animated that muscle — not a bug, the avatar's default/rest value
applies. Root motion (`RootT`/`RootQ`) keeps using the `HumanPoseHandler` capture loop above,
which was confirmed correct in the live preview — only the muscle-reading part of that loop was
the problem, so §11.5's code sample should be read as: **run the loop for root motion only,
then copy muscle curves directly as shown here, skip trying to read muscles from the loop.**

**Verify with a spot-check on a couple of limb muscles** (e.g. `Left Arm Down-Up`, `Left Upper
Leg Front-Back`) — read `min`/`max` across the curve's keys and confirm they're not both `0`. A
flat muscle curve is exactly what a silent T-pose-while-moving bug looks like, and it will not
show up in the `humanMotion` check or the root-motion position-delta check from §11.6 — both of
those passed on the broken clips. This is now a required third check alongside those two.

Set `AnimationUtility.GetAnimationClipSettings(clip).loopTime` afterward according to how the
state is meant to behave (loop for sustained locomotion/pain states, one-shot for falls/deaths/
single actions), same as any other clip in this project.

### 11.6 Verify before trusting the batch — same discipline as §6.1, different mechanism

Retarget Pro's doc insists a human look at one baked clip before batching; this method has no
preview window to look at, so two numeric checks substitute for it. **Both are required — the
first one alone produced a false pass on the very first attempt of this method (§11.5's history)
and the resulting clips didn't play at all.**

1. **`clip.humanMotion` must be `true`.** This is the structural check and the one that actually
   caught the bug: a clip built from raw Transform curves can still show a perfectly convincing
   position delta under `SampleAnimation` (see next point) while being completely unplayable on a
   Humanoid Animator. `humanMotion` is Unity's own flag for "this clip has valid muscle curves" —
   check it first, before anything else.
2. **Drive it through a real `Animator`, not `clip.SampleAnimation`.** `SampleAnimation` writes
   curves straight onto the target's transforms and will happily apply a broken Transform-curve
   clip too — it bypasses the exact Humanoid-avatar restriction that's being tested for, which is
   why it gave a false positive last time. Instead: build the same kind of temporary
   `AnimatorController` used for baking, assign the new clip as a state's motion, call
   `animator.Play(...)` + `animator.Update(dt)`, and read a named, stable bone's world position at
   the start and partway through. A real delta confirms real, Animator-playable motion; a
   near-zero delta or a bone that can't be found means something's still wrong — before it goes
   anywhere near a batch of five.

### 11.7 What this method does NOT give you

No IK correction, no foot-sliding cleanup, no handling for source/target proportion mismatches
beyond what Unity's own Humanoid muscle-space retargeting already does for free. That gap is
exactly Retarget Pro's value-add (§3) — if a baked clip looks wrong in a way that reads as
sliding feet or floating hands rather than "wrong clip," that is the signal to go run it through
the real Retarget Pro GUI (§6) instead, with eyes on the preview.

---

## 12. Using the real Retarget Pro GUI — lessons from the first full walkthrough (MRM-75,
2026-09-01)

After the root-motion trouble in §11, Carlos asked to switch to the actual Retarget Pro window
with eyes on the preview, rather than more script-only bakes. These are the concrete lessons from
that first real walkthrough, on top of the general procedure in §6.

### 12.1 IK "Pole Weight" — set it to 0 if the pole gizmo goes wild

Under a limb's IK Solve / Stability settings, **"Pole Weight"** controls how strongly the pole
target's offset bends the joint. Left at its default (non-zero), a *tiny* gizmo drag on the pole
target can send the joint wildly out of position — the offset fields readable as multiple meters
(e.g. `1.44, 2.04, 5.99`) instead of the small fractional values a pole offset should normally
need. **Confirmed fix: set Pole Weight to 0.** If you need to fine-tune a bend direction by hand
afterward, do it by typing small values directly into the Joint Offset X/Y/Z fields (increments
well under 1.0) rather than dragging the gizmo — the gizmo's screen-space-to-world-space
sensitivity is also tied to how zoomed-in the preview camera is, so zooming in close on the joint
before dragging helps too.

### 12.2 Which profile edits are safe to script, and which need the real GUI

Carlos's core worry after §11 was Claude's script silently corrupting animation *data* again.
The actual dividing line that resolves this: **editing what the `RetargetProfile` asset points
to and how its chains are mapped is plain data assignment — safe to script, no different from
clicking the same field in the Inspector.** What's *not* safe to script blind is the actual
*bake* (§6's `RetargetAnimBaker`, a ~3000-line stateful class built around a live preview scene)
— that's the part requiring eyes-on verification, not the setup around it. Concretely, all of
the following are safe, ordinary field/list assignments and were done via script successfully:

- Swapping `profile.sourceCharacter` to a different source model, then calling
  `RetargetProfileModelRigUtility.TryComposeProfileRigs(profile, true, out message)` to rebuild
  the rig + re-run the chain auto-mapper (this is the exact same call the GUI's own
  "Remap All Chains" button makes).
- Setting a `BasicRetargetFeature`'s (or its `IKRetargetFeature`/`RootPelvisRetargetFeature`
  subclass's) `sourceChain.elementChain` / `targetChain.elementChain` list directly, once you
  know the correct bone names — see §12.3. This is the scriptable equivalent of manually
  checking boxes in the "Element Chain Selection" tree window, and it's far faster once the
  correct bone list is known from a previous manual pass.

### 12.3 The "root" chain needs manual fixing on BOTH sides, every time the source model changes

The chain auto-mapper (`RetargetProfileMappingUtility.TryRebuildProfileMappings`, triggered by
"Remap All Chains") reliably resolves `pelvis`/`toes`/`spine`/`neck` and the finger chains, but
**consistently fails to resolve the `root` chain on its own**, on both sides:

- **Target side** (our AccuRig/CC-based rig): the fix is a single-bone chain containing just the
  literal Transform named `root` (the node directly above `CC_Base_Hip` — not the hip itself, and
  not the FBX's outer GameObject).
- **Source side** (any Mixamo-derived skeleton): there is no separate root bone above Hips at
  all — the fix is a single-bone chain containing the **source model's own top-level GameObject**
  (i.e. whatever the source FBX asset is named, e.g. `Src_Flare_MixamoShootingGun`).

**This resets every time `sourceCharacter` changes**, even though the target-side fix persists.
Swapping the source model for a new clip (Fall → Flare → Reload, etc.) silently drops the source
half of the `root` chain back to unresolved — check for a warning on `root` after every source
swap, not just the first time. rightLeg/leftLeg/rightArm/leftArm chains, once fixed on the target
side, **do** persist correctly across a source-character swap (only `root` regresses), so those
don't need to be redone each time — confirmed by re-testing this exact sequence.

### 12.4 The chain-picker tree's checkboxes are group-linked to whatever's currently row-selected

In the "Element Chain Selection" tree window (opened via a chain's "Edit" button), toggling a
bone's checkbox toggles **every currently row-selected bone at once**, not just the one you
clicked — if several bones are highlighted as a multi-selection (e.g. left over from a stale
prior chain spanning root→spine→head), checking or unchecking any one of them drags all of them
along. **Fix: single-click a bone's name/label first to isolate it as the only selected row,
*then* click its checkbox** — repeat per bone. This is confirmed source-level behavior
(`RigTreeView.cs`): a checkbox toggle applies to the whole current `TreeView` selection when the
clicked item is part of one, and to just that item otherwise.

---

## 13. The actual fix for the Spotter's 5 external clips — bypass baking entirely (MRM-75,
2026-09-01)

§11 and §12 above were both eventually abandoned for the Spotter's five Mixamo/Sketchfab clips
(Flare, Fall, DeathDowned, Downed, Reload). Neither the script-bake method nor a full, careful
Retarget Pro GUI pass (profile rebuilt from scratch twice, root chain fixed both sides per §12.3,
Pole Weight zeroed per §12.1) produced a correct standalone `.anim` file — every bake, regardless
of method, reproduced the same symptom: the character's hip collapsed to near-root height while
other joints stayed plausible, as if only part of the pose had been captured. This took an entire
session and 8+ measured Retarget Pro bake attempts plus several script-bake rewrites to actually
solve.

### 13.1 What was proven, by direct measurement, not assumption

- **Root motion was never the cause.** Root translation/rotation was forced to exact identity on
  a test bake and the hip-collapse symptom persisted unchanged — the bug lives in the muscle
  data, not the root curve.
- **Retarget Pro V5's actual bake output ignores several of its own settings for this rig
  pairing.** "Use Root Motion", "Root Node", a feature's "Offset" field, and "Target Pose"/
  "Source Pose" were each changed and re-baked in isolation; all visibly changed the *live
  preview* but produced **byte-identical saved `.anim` output** every time, confirmed by reading
  the actual saved file, not the preview. This reads as a real bug in Retarget Pro V5's bake
  pipeline for a Mixamo→AccuRig pairing specifically — never root-caused to a specific line, but
  reproduced 8 times, so treat it as a known limitation, not user error.
- **All of Retarget Pro's bundled T-pose presets except `A_TPose_Humanoid` are broken.**
  `A_TPose_CC4`, `A_TPose_Mixamo`, `A_TPose_UE4`, `A_TPose_UE5`, `A_TPose_Synty` all have
  `humanMotion = false` and sample to a degenerate near-zero pose. This is a genuine bug in the
  shipped presets — but fixing `profile.sourcePose`/`profile.targetPose` to point at the one good
  preset **still did not fix the bake output** (see previous point: Source/Target Pose is a
  preview-only field for this pipeline too).
- **Live Humanoid-to-Humanoid retargeting was correct every single time it was sampled**, across
  every clip, across the whole session. Unity's own Humanoid Avatar system retargets a source
  clip onto a differently-proportioned target skeleton correctly and automatically, with zero
  setup, the moment that clip plays through an `Animator` whose `Avatar` is the target's — this
  was true from the very first import and never actually needed fixing.

### 13.2 The actual fix

**Skip baking. Assign the source clip directly as the Animator state's `motion`.** Do not run it
through Retarget Pro's baker and do not run it through the §11 script-bake method — both
extract-and-repackage the muscle curves into a *new* clip, and that extraction step is where the
corruption happens (see §13.3). Playing the original clip live is not a workaround with a
downside; it is Unity's normal, intended way to retarget Humanoid animation and it has no
observable quality cost versus a baked clip for this use case.

```csharp
var sourcePath = "Assets/Character Playground/Oldtimer/Src_<Name>.fbx";
var sourceAssets = UnityEditor.AssetDatabase.LoadAllAssetsAtPath(sourcePath);
UnityEngine.AnimationClip sourceClip = null;
foreach (var a in sourceAssets)
    if (a is UnityEngine.AnimationClip c && !c.name.StartsWith("__preview__")) { sourceClip = c; break; }

// loopTime: true for a repeatable action (Reload, Downed-writhing), false for a clip that should
// end in a held pose (Fall, DeathDowned — the corpse/knockdown state).
var settings = UnityEditor.AnimationUtility.GetAnimationClipSettings(sourceClip);
settings.loopTime = false;
UnityEditor.AnimationUtility.SetAnimationClipSettings(sourceClip, settings);

var ac = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEditor.Animations.AnimatorController>(controllerPath);
foreach (var cs in ac.layers[0].stateMachine.states)
    if (cs.state.name == "<StateName>") cs.state.motion = sourceClip;
```

**Verification that actually catches the hip-collapse bug** — the same false-positive trap as
§11.6 applies here too. Don't trust a single frame or the Scene-view preview; instantiate the
*target* prefab, `AnimationMode.StartAnimationMode()` → `BeginSampling()` →
`SampleAnimationClip(targetInstance, sourceClip, t)` → `EndSampling()` at several points across
the clip's full duration, and read `animator.GetBoneTransform(HumanBodyBones.Hips).position.y`
and `...Head....y` at each one. A collapsed bake shows the hip snapping to a near-zero,
near-root value while other joints stay plausible; a correct one shows the hip height tracking
what the clip's choreography actually calls for (e.g. Fall's hip legitimately drops from ~0.93m
standing to ~0.16m collapsed over the clip — that's correct pose data, not the bug).

### 13.3 Best-supported theory for *why* extraction corrupts it (unconfirmed — flagged as such)

Unity's Humanoid muscles are documented as avatar-independent, but the live retargeting pipeline
likely does more than copy those values — it probably applies some per-avatar normalization (tied
to each avatar's own calibrated proportions/"Human Scale") as part of turning muscle values into
an actual pose, and does this invisibly, every frame, as part of `Animator` playback. Every
extraction method tried (raw muscle curve copy, `HumanPoseHandler` capture, combined
capture-during-`AnimationMode`-sampling) most likely skips that normalization step. This is a
reasoned hypothesis from watching the failure pattern, **not** something traced to a specific
line of Unity's Humanoid retargeting source — do not repeat it to Carlos as a confirmed root
cause, only as the best current explanation.

### 13.4 Where this leaves Retarget Pro — it is not obsolete

This bypass fixes single-clip retargeting *for this specific use case*: one external mocap clip,
onto one Humanoid target, no bone-length/proportion correction wanted beyond what Unity's own
Humanoid system already does for free. It does not replace Retarget Pro for cases needing actual
IK correction or chain remapping:

- **The Wendigo boss** — Humanoid type, but deliberately non-human proportions (per the
  MRM-70/71 asset triage). Unity's built-in retargeting will still play a clip on it without
  crashing, but Retarget Pro's IK-chain correction is expected to genuinely improve quality there
  (foot placement, hand reach) in a way plain playback won't. Budget a real Retarget Pro pass for
  the Wendigo when that work starts, informed by every §12 lesson above.
- **Any future non-Humanoid rig** (a true quadruped/Generic-type skeleton) — Unity's built-in
  Humanoid retargeting cannot touch a Generic avatar at all; Retarget Pro's bone-chain approach
  would be the only option. Not currently a need: the existing wolf animations are already
  authored for the wolf's own rig, nothing to retarget.
- **FP (first-person) weapon/arm animations** — not a Retarget Pro case at all, in either
  direction: every FP weapon animation the demo needs is already owned outright (HQ FPS Weapons,
  19 weapons on one shared arm skeleton), so there is nothing to retarget there.
