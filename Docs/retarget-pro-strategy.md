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

1. **Not yet proven on our rigs.** Nothing has been baked. §6.1 is the validation, and it must
   happen before any issue schedules work around this tool. **~30 minutes.**
2. **No wolf model is staged.** MRM-33's quadruped argument is the strongest reason to own this tool
   and the one we cannot test yet. MRM-33 says a wolf pack is staged at `Assets/Wolf_enemu/`; it is
   not there. Open question for Carlos, flagged on the issue.
3. **Source-pose choice is the main quality lever.** A wrong `A_TPose_*` pick is the most likely way
   to get bad output that looks like the tool's fault. Bake one clip and look at it (§6.1).
4. **Unity 6.3 deprecation drift.** 11 `CS0618` warnings today. Harmless now; if a future Unity turns
   them into errors, the tool is editor-only and in Playground, so the game is never at risk — and
   the already-baked clips are unaffected.
