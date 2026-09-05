# Tracey's rig — the three-animation-sources problem, and the strategy

**Written 2026-09-05.** Analysis session, no code changed. This is the first document on the
subject. Read alongside `Docs/mrm9-hqfps-integration.md` (the controller) and
`Docs/mrm34-spotter-ai-build.md` (the Humanoid precedent that already works).

Carlos's question, in his words: Tracey is a full body and the player must be able to look down and
see her feet. The new controller ships arms only. Can one mesh play locomotion on the body and the
vendor's weapon animations on the arms at the same time — and still take QuickMagic mocap later?

---

## 1. The short answer

**No — and that is good news, because the alternative is far less work.**

The vendor's weapon animations **cannot** be retargeted onto Tracey's body. Not "it would look bad";
it is structurally impossible in Unity. Section 3 gives the proof.

The right architecture is **two skeletons that never touch each other**:

| | Rig A — Hands | Rig B — Body |
|---|---|---|
| Skeleton | Vendor `Arms_Root`, 44 bones, **unchanged forever** | Tracey's full biped |
| Unity avatar | **Generic** | **Humanoid** |
| Lives under | The camera (ParentConstraint to the motion mixer) | The character root |
| Layer / FOV | `ViewModel` (22), 60° via `LitFieldOfView.shadergraph` | World layers, world FOV |
| Plays | Every weapon animation, from HQ FPS | Locomotion, deaths, QuickMagic mocap |
| Animation source | HQ FPS only, forever | Mixamo / libraries / QuickMagic |
| Who else needs it | Only playable characters | **Every** character in the game |

Tracey stays "one character" to the player because the **body's arms and head are hidden**. You look
down and see her coat, hips, legs and boots. You look forward and see her gloved hands on the weapon.
The two are different meshes and the player never notices, because no camera angle shows the seam.

This is what Call of Duty, Battlefield, Half-Life and almost every "you can see your legs" shooter
actually does. The one-skeleton alternative (Escape from Tarkov) requires **authoring every weapon
animation on the full body**, which we cannot do — we own HQ FPS's clips, not the ability to remake
them.

---

## 2. Verified facts this strategy rests on

Measured 2026-09-05 from the live Blender file (`C:\Users\calva\Desktop\3D Characters\Tracey\tracey
low poly.blend`) and from the repo.

**The arms rig**
- `Arms_Root`, **44 bones**, 22 per arm.
- **Two root bones**: `UpperArm.L` and `UpperArm.R`. There is **no hips, no spine, no chest, no
  clavicle**. The two arms are independent floating chains.
- 4 `ForearmTwist` bones per arm (Unity Humanoid supports exactly **one** twist per limb).
- `FP_Arms_AKM.fbx.meta` → `animationType: 2` (**Generic**), `avatarSetup: 2`.
- The armature is bound at runtime by a `ParentConstraint` onto `IMotionMixer.TargetTransform`
  (`WieldableArmsHandler.EnableArms`) — i.e. **onto the camera**.

**There is only ONE arms mesh in the whole game**
- 15 `FP_Arms_*.fbx` files exist (one per weapon), but they are **animation-clip containers only**.
- The runtime arms live in a single prefab:
  `Assets/ThirdParty/PolymindGames/HQFPS/Prefabs/Wieldables/HQFPS_Wieldable_Arms.prefab`,
  nested inside `Player_Tracey.prefab`.
- Its skinned meshes come from `FP_Arms_AKM.fbx` — that one FBX is the master mesh + skeleton.
- Equipping a weapon only does `_animator.runtimeAnimatorController = _clips.OverrideController`
  (`WieldableArmsAnimator.OnEnable`). The mesh and skeleton never change.

**The vendor already built the skin-swap hook we need**
`WieldableArmsHandler` holds `ArmSet[] _armSets` — `{ string Name; SkinnedMeshRenderer LeftArm;
SkinnedMeshRenderer RightArm; }` — plus `ToggleNextArmSet()` and an input binding
(`FPSArmsChangeInput.cs`). Four sets already ship, and the same four appear as separate meshes in the
Blender file: `Standard`, `Standard & Shirt`, `Shirt & Gloves`, `Standard & Gloves`.

**Tracey's rendering**
- Single camera. The viewmodel is **not** a second camera — it is a vertex-shader FOV override
  (`_baseViewModelFOV = 60f`, global shader props `_FOV` / `_FOVEnabled`,
  `Assets/_Project/Code/Vendor/PolymindGames/Shaders/LitFieldOfView.shadergraph`).
- FPSCore has **zero** third-person / full-body support. No body animator, no shadow proxy. The body
  is entirely ours to build.

**The Humanoid precedent already works here**
`Spotter.fbx.meta` → `animationType: 3` (Humanoid), and every `Src_*_Mixamo*.fbx` clip is Humanoid
too. Retargeting library clips onto a Humanoid character is proven in this project.

**Tracey's current state — she is a blockout, not a character**
- Five loose objects, no armature: `body` (1009 v), `head` (881 v), `hands` (152 v), `boots` (388 v),
  `ear` (36 v). ~2.5k verts before mirrors.
- **0 vertex groups on every object** — nothing is rigged or weighted.
- One auto-generated material, `tripo_mat_5fb676ae`, on all five.
- **She is exactly 1.000 m tall**, feet at Z = 0. She must be scaled to ~1.70 m before export, or
  she imports as a child-sized player (`blender_export_process`: 1 unit = 1 m).

---

## 3. Why retargeting weapon animations onto a body is impossible

Three independent reasons, any one of which is fatal.

**(a) The source skeleton cannot be a Humanoid avatar.** Unity's Humanoid avatar requires a
hips→spine→chest→shoulder→arm chain. `Arms_Root` has two root bones and no torso at all. Unity will
refuse to configure it. That is exactly why the vendor shipped it as Generic. Generic→Generic
retargeting requires *identical bone hierarchies*, which a full body by definition does not have.

**(b) The clips are authored in camera space, not body space.** Because the arm roots are constrained
to the camera, a reload clip means *"put the left hand here relative to the lens"* — never *"rotate
the left shoulder by X degrees."* There is no shoulder rotation in the data to retarget. Feeding that
to a body would require inventing the entire torso.

**(c) Humanoid retargeting is rotation-based and would break weapon alignment anyway.** Unity
normalizes Humanoid clips to a T-pose and replays them as *muscle rotations*, deliberately discarding
exact positions so a clip works on differently-proportioned characters. Viewmodel animation depends
on exact positions — the gun must sit in the hand to the millimetre. This is the reason no studio
Humanoid-retargets first-person weapon animation. Ever.

And even if all three were solved: viewmodel arms are **deliberately anatomically false**. They are
foreshortened, the elbows sit outside where a real ribcage is, and the weapon is held closer to the
face than a human could. Rendered at a separate 60° FOV, it reads correctly. Put those same poses on
a real body at world FOV and the elbows pass through her own chest.

### Was the instinct wrong?

No — it was the right tool for a different job. Unity **can** run different animations on different
body parts, via Animator layers + an Avatar Mask, and **we will use that** — on Rig B, so Tracey's
legs run while her torso leans and aims. It just cannot consume *these particular clips*.

---

## 4. The architecture, concretely

```
Player_Tracey (CharacterController, FPSCore)
├── Camera  ──────────────────────────── world FOV
│   └── MotionMixer target
│       └── HQFPS_Wieldable_Arms  ← Rig A, Generic, layer ViewModel(22), 60° shader FOV
│           ├── ArmSet 0..3  (vendor: Standard / Shirt / Gloves ...)
│           └── ArmSet 4     ← TRACEY  (new: parka sleeve + her gloves)  ★ the only art job
└── TraceyBody               ← Rig B, Humanoid, one Animator, world FOV
    ├── Renderer A: full mesh, ShadowCastingMode = ShadowsOnly   (correct full silhouette)
    └── Renderer B: same mesh, arms + head bones scaled to 0     (what she sees looking down)
```

**Rules that make it work**

1. **Never parent the camera to the body's head bone.** The FPSCore motion mixer already owns head
   bob, sway and recoil. Stacking an animated head bone on top causes motion sickness and fights the
   mixer. The camera stays where the controller puts it; the body *follows* the camera.
2. **The body follows camera yaw only** — never pitch. Add turn-in-place lag so she does not
   snap-spin. Pitch becomes at most a small torso lean, on an upper-body Avatar Mask layer.
3. **The body's eye position will not match the camera.** That is fine and expected — the head is
   hidden, so nobody sees the mismatch. The only things that must line up are: feet on the ground,
   hips below the camera, and the crouch pose roughly matching the controller's crouch height.
4. **Hide, do not delete.** Scaling the arm and head bones to zero in `LateUpdate` is ~10 lines and
   reversible — restore the scale and you have a full third-person Tracey for cutscenes and the death
   cam, for free. Deleting geometry throws that away.
5. **The body is cosmetic.** It never carries a collider, never drives movement, never feeds the
   hit system. It is a puppet on a string.

### Shadows — worth calling out

Right now Tracey casts no shadow at all, because there is no body. Adding Rig B gives her a real
flashlight shadow, which in a dark horror game is one of the highest-value visual returns available.
The two-renderer split above is what keeps the *shadow* arms intact while the *visible* arms are
hidden. For a first pass, one renderer with the bones scaled to zero is acceptable — a missing arm in
your own shadow is essentially never noticed. Split it for October.

---

## 5. The plan, in order

### Phase 0 — Finish Tracey as a mesh (blocks everything, ~1 session)
Prerequisite for both rigs. Run `/prop` (`Docs/3d-prop-pipeline-wizard.md`), character path.
1. **Scale to ~1.70 m.** She is 1.000 m today. Apply the scale; do not fix it at import time.
2. Apply the mirror modifiers; join `body` + `head` + `hands` + `boots` + `ear` into one mesh
   (keep `ear` separate only if it must move).
3. **Retopo the deformation zones**: edge loops at shoulders, elbows, wrists, hips, knees, ankles.
   Three loops minimum at each. This is the single thing that decides whether she deforms or folds.
4. Feet at origin, facing **−Y** in Blender (→ +Z forward in Unity), transforms applied.
5. One UV set, one baked texture. RetroLit takes **BaseColor + Normal only** — AO multiplied into
   the albedo, no mask, no metallic, no emission.

### Phase 1 — Tracey's hands in the viewmodel — machinery BUILT, mesh PENDING

➡️ **The step-by-step lives in `Docs/character-pipeline-guide.md` Stage 7.** This section keeps only
what that guide does not: the reasoning, and the record of the dry run.

**Corrected 2026-09-05, after Carlos's ruling.** The dry run in §9 used the vendor `Arm_Standard`
mesh as a stand-in, and the plan written here first assumed that mesh would *be* Tracey's arms.
It will not — Carlos: *"those arms are not the ones I'm going to use."* The real sequence is:
he finishes every character model, then hands over **the full-body base** and, separately, **that
character's own arms**, and Claude puts them on the vendor skeleton.

**What that changes:** the vendor mesh arrives pre-weighted, so the dry run had no weight-transfer
step. Carlos's arms will not, so Stage 7 adds one — renaming AccuRig vertex groups through the 1:1
map in §8, folding the `ForearmTwist` chain into `Forearm`, and pose-matching his T-posed arm onto
the vendor rest pose through a bridging armature.

**What it does not change:** everything downstream of the mesh. The bindpose transplant, the
material, the prefab wiring, the arm-set registration and the offscreen verification render are
built, tested and mesh-agnostic — `MoonlightTraceyArmsBuild` works on any mesh bound to `Arms_Root`.
The two traps in §9.1–9.2 were paid for once and are now handled automatically.

**The one hard modelling constraint**, and the reason the dry run could not simply reuse her body's
arms: her `hands` object is 152 verts in a single connected piece — **no separated fingers** — and
proportionally short (37 cm shoulder-to-tip at 1.7 m scale, against ~72 cm real). The weapon clips
animate **15 finger bones per hand**. A paddle cannot drive that, and the mesh does not reach the
bones. Fingers have to exist before Stage 7 can run at all.

**After Stage 7, first-person Tracey is finished.** Phase 2 / Stage 8 is only for looking down.

### Phase 2 — The body (October work, not a Sept gate item)
1. Rig Tracey with **AccuRig**, the same tool used for the Spotter — same rig family means the
   Spotter's whole clip library works on her without surprises. (Trap: AccuRig FBX → Unity needs
   `globalScale = 0.01`, cm→m — see `mrm75_accurig_scale_bug`.)
2. Import **Humanoid** (`animationType: 3`), same as `Spotter.fbx`. Verify the avatar maps cleanly in
   Configure Avatar before writing any code.
3. Build `AC_TraceyBody.controller`: base layer locomotion blend tree (idle / walk / run / crouch /
   jump / land), plus an upper-body Avatar Mask layer for lean and reactions.
4. Write `MoonlightBodyDriver` — reads the FPSCore motor for speed, grounded, crouched; feeds the
   Animator; matches body yaw to camera yaw with lag; scales arm + head bones to zero in
   `LateUpdate`. All numbers in `MoonlightTunables`.
5. Split into the two-renderer shadow setup.

### Phase 3 — QuickMagic (whenever, no dependency on the above)
QuickMagic exports **FBX and BVH with selectable skeleton presets** and documents retargeting to a
production character. So it is format-flexible, and the question is not "which rig does it use" — it
is only "does Unity read its FBX as Humanoid."

**Run this 20-minute test before committing to anything:**
1. Export any one QuickMagic clip as FBX.
2. Drop it in Unity, set Animation Type = **Humanoid**, Avatar = *Create From This Model*.
3. Open **Configure Avatar**. If the bones map green, you are done — it retargets onto Tracey and
   every other Humanoid character in the project, exactly as the Spotter's Mixamo clips already do.
4. If it maps only partly, hand-map the missing bones once and save the avatar. Still done.
5. Only if the hierarchy is genuinely unreadable do you need Retarget Pro — and
   `Docs/retarget-pro-strategy.md` §13 already covers that path.

Humanoid is the universal adapter. Once Tracey is Humanoid, **you never have to care again what rig
any animation source uses.** That is the whole point, and it is why Rig B must be Humanoid even
though Rig A cannot be.

---

## 6. What this means for every other character

The rule generalises cleanly, which was Carlos's other requirement:

- **Anything the player sees from the outside** — Spotter, Zealot, Wendigo, NPCs, animals — needs
  **Rig B only**. Humanoid, one animator, library + QuickMagic clips. Exactly what the Spotter
  already is. Nothing here changes for them.
- **Anything the player sees from the inside** — Tracey, and any future playable character — needs
  Rig B *plus* an arms skin on the shared vendor `Arms_Root`. That extra step is **one mesh**, and it
  is a modelling job, not a rigging job.
- **Bosses and animals** that never hold a weapon need Rig B only, and may drop Humanoid entirely if
  their skeleton is non-biped (Wendigo) — that is the Retarget Pro case.

So the "three rigs" in the original question are really **two**, and only the player needs both.

---

## 7. Risks and open questions

| Risk | Mitigation |
|---|---|
| Body crouch pose does not match the controller's crouch height → legs look wrong when crouched | Tunable offset in `MoonlightTunables`; check it the first time the body goes in |
| Near clip plane slices the neck/collar when looking down hard | Head bone already scaled to 0; clamp look-down pitch if needed |
| Weight transfer on the Tracey arms produces artefacts at the sleeve cuff | Vendor mesh is the donor and the silhouette is close, so this should be clean — check the wrist twist range specifically |
| Extra draw calls from the body + shadow renderer | Two renderers on one character. Against the island's 21,946 draw calls this is noise |
| Tracey's 1 m scale gets baked in somewhere before it is fixed | Fix it in Phase 0 step 1, before anything downstream exists |

**Closed 2026-09-05:** Tracey is **bare-handed**. The vendor's glove variants are not used.

---

## 8. AccuRig — where it fits, and why no Frankenstein rig is needed

Carlos, 2026-09-05: *"Accurig will build our first skeleton. Like we did with the spotter."* His
proposal was to then graft the vendor arms structure onto that skeleton, producing one rig that
serves Humanoid, QuickMagic and the weapon animations at once.

**AccuRig stays exactly as it is. Nothing about the Spotter's rigging step changes.** The graft is
neither needed nor possible, and the reason is a happy one.

### The two rigs are already the same arm

Read out of `Spotter.fbx.meta`'s `humanDescription` and set against the vendor `Arms_Root`:

| AccuRig (CC_Base) | Vendor arms | |
|---|---|---|
| `CC_Base_L_Upperarm` | `UpperArm.L` | ✅ |
| `CC_Base_L_Forearm` | `Forearm.L` | ✅ |
| `CC_Base_L_Hand` | `Hand.L` | ✅ |
| `CC_Base_L_Index1/2/3` | `Index.1.L / .2.L / .3.L` | ✅ |
| `CC_Base_L_Mid1/2/3` | `Middle.1.L / .2.L / .3.L` | ✅ |
| `CC_Base_L_Ring1/2/3` | `Ring.1.L / .2.L / .3.L` | ✅ |
| `CC_Base_L_Pinky1/2/3` | `Pinky.1.L / .2.L / .3.L` | ✅ |
| `CC_Base_L_Thumb1/2/3` | `Thumb.1.L / .2.L / .3.L` | ✅ |
| *(none)* | `ForearmTwist.1–4.L` | fold into `Forearm` |

**19 of 22 bones per arm map 1:1 by name.** So converting weights between an AccuRig arm and the
vendor arm is a **dictionary lookup, not a spatial transfer** — deterministic, scriptable, no
hand-painting. That is the whole benefit the Frankenstein was reaching for, obtained without
touching either skeleton.

### Why they must still be separate GameObjects

Beyond §3's three reasons, a mechanical one specific to this asset: `WieldableArmsHandler` binds the
arms with a **`ParentConstraint` onto `IMotionMixer.TargetTransform`** — the camera — and calls
`gameObject.SetActive(false)` whenever no weapon is out. Bones living inside Tracey's body hierarchy
would inherit her hips' motion and could not be deactivated independently. Separation is what makes
the vendor system work, not a compromise around it.

---

## 9. Build record — Phase 1, 2026-09-05

Built and verified in one sitting. Blender file:
`C:\Users\calva\Desktop\3D Characters\Tracey\tracey low poly weapons arms.blend` (Carlos's working
copy; the original is untouched).

### What shipped

### ⚠ REVERTED the same day — read this first

Carlos, once the dry run was confirmed working: *"revert those changes to MrMoonlight since these
are not the arms we're going to be using."* Correct call — the dry run used the **vendor's** mesh,
and shipping it as Tracey's default hands would have been a placeholder pretending to be art.

**What was removed** (via `Tools > MrMoonlight > Character > Remove Tracey FP Arms`, then an asset
delete): the `Tracey` arm set and both renderers from `HQFPS_Wieldable_Arms.prefab`, the whole of
`Assets/_Project/Art/Characters/Tracey/Arms/` (FBX, two mesh assets, material, placeholder PNG),
and the two verification screenshots. **Verified from disk afterwards:** the prefab is back to the
vendor baseline — 8 renderers, 4 arm sets, `Standard` at index 0.

**What was kept, deliberately:** `MoonlightTraceyArmsBuild.cs` and every doc. The tool is
**mesh-agnostic** — it works on any mesh bound to `Arms_Root` — so it is the Stage 7 machinery, not
part of the discarded art. With the assets gone it aborts cleanly with *"export the arms from
Blender first"*, which is exactly the right behaviour until Carlos's real arms arrive.

**Why the revert needed a menu item rather than git:** `HQFPS_Wieldable_Arms.prefab` lives under
`Assets/ThirdParty/`, which `.gitignore` excludes wholesale. Git could not have undone that edit.
The `Remove` menu item exists for precisely this, and is idempotent.

### What the dry run produced, and what it proved

| Path | What | Status |
|---|---|---|
| `…/Tracey/Arms/Tracey_FP_Arms.fbx` | Arms + `Arms_Root`, Generic, no animation | reverted |
| `…/Tracey_Arm_L.asset`, `…/Tracey_Arm_R.asset` | Bindpose-corrected meshes (§9.2) | reverted |
| `…/Tracey_Arms_BaseColor.png` | 512×512 placeholder skin | reverted |
| `…/Tracey_Arms.mat` | `Shader Graphs/LitFieldOfView_SSS` | reverted |
| `Assets/Screenshots/tracey_arms_phase1*.png` | Offscreen verification renders | reverted |
| `Assets/_Project/Code/Editor/Migration/MoonlightTraceyArmsBuild.cs` | Build **and** Remove tools | **kept** |

While it was live, the arm sets read
`[0] Tracey · [1] Standard · [2] Standard & Shirt · [3] Shirt & Gloves · [4] Standard & Gloves` —
index 0 being what `WieldableArmsHandler.Awake` enables, which is how a new arm set becomes the
default hands. **That is the mechanism Stage 7 will use**, and it is proven to work.

**Verified**, not assumed: bindpose delta against the vendor mesh is `0.000000` across all 44 bones;
`bones[]` holds the same `Transform` instances as the vendor renderer; the prefab was re-read from
disk after saving; and both arms were rendered offscreen posed by a real clip
(`Arm_AKM_Reload_` @1.2 s) in a preview scene, showing correct finger, twist and cuff deformation.

### 9.1 ⚠ Trap — the FBX export scale is 100× off by default

Blender exported at `apply_scale_options='FBX_SCALE_NONE'` produced a mesh **exactly 100× too
small** in Unity (vendor bindpose translation `1.025`, ours `0.010`). The vendor FBX is
centimetre-native; Blender's default writes metres and Unity's *Convert Units* then applies the
wrong conversion.

**Fix at the Blender end, not in the importer:** `apply_scale_options='FBX_SCALE_ALL'` with
`global_scale=1.0`. After that the mesh bounds matched the vendor's to four decimals
(`0.3944, 0.2258, 0.5597`). Do not "fix" this with a Scale Factor of 100 in the ModelImporter — that
hides the mismatch and breaks the next re-export.

Note this is the **opposite direction** to `mrm75_accurig_scale_bug` (AccuRig needs `0.01`). The two
are unrelated: AccuRig writes raw centimetres with misleading unit metadata; Blender writes correct
metres. Always verify by comparing bounds against a known-good vendor mesh rather than reasoning
about which multiplier applies.

Full export settings that work:
```
use_selection=True, object_types={'ARMATURE','MESH'},
use_mesh_modifiers=False,        # never bake the armature modifier in
add_leaf_bones=False,            # Unity does not want leaf bones
bake_space_transform=False,      # wizard §4.2a: unsafe for rigged content
global_scale=1.0, apply_scale_options='FBX_SCALE_ALL',
axis_forward='-Z', axis_up='Y', bake_anim=False,
use_armature_deform_only=False, mesh_smooth_type='FACE'
```

### 9.2 ⚠ Trap — Blender recalculates bone roll, and it silently twists fingers

The `.blend` was made by importing the vendor FBX, and Blender's importer recalculates bone
orientation. Round-tripping back out, **15 of 44 bones** came back with a bindpose rotation
differing from the vendor's by up to `0.109`: every left-hand finger, plus `UpperArm.R`,
`Forearm.R` and all four right `ForearmTwist` bones.

Binding such a mesh to the prefab's vendor-authored skeleton twists exactly those joints — a defect
that would only show up mid-reload, on one hand, and read as "the animation is broken."

**Fix:** the bone *order* survives intact, so `MoonlightTraceyArmsBuild` copies the vendor mesh's
bindpose array onto ours wholesale. Exact, not approximate. It runs on every build, so re-exports
after remodelling are covered automatically. The tool aborts loudly if bone count or order ever
stops matching, rather than binding something plausible-looking.

### 9.3 ⚠ The arms are **not** RetroLit

`MRM-9` recorded *"weapon / hand material finish → RetroLit, same as every other prop."* That cannot
hold literally for the viewmodel. The vendor bare arms use **`Shader Graphs/LitFieldOfView_SSS`**,
and the FOV nodes in that graph are what pull the arms to their own 60° FOV
(`CameraFOVHandler`, global `_FOV` / `_FOVEnabled`). RetroLit has no such node, so RetroLit arms
would render at world FOV and clip through geometry.

Tracey's material therefore uses `LitFieldOfView_SSS`. **Open:** if the hands must be RetroLit for
style, the FOV nodes have to be ported into a RetroLit viewmodel variant first — which is very
likely the same unfinished `RetroLitViewModel.shader` port already blamed in the
`mrm9_weapon_darkness_deferred` memory. Worth resolving there once, for weapons and hands together.

### 9.4 Why the tool writes into a git-ignored prefab

`HQFPS_Wieldable_Arms.prefab` is under `Assets/ThirdParty/`, which `.gitignore` excludes wholesale
(`git check-ignore` confirms; the file is untracked). A hand edit there is invisible to version
control. Every durable asset the tool produces lands in `_Project`, which **is** tracked, and the
prefab wiring is reproducible by re-running the tool — the same pattern as the MRM-25 weapon tools.

### 9.5 Still open on Phase 1

- **Poly budget.** The arms are 7,768 tris each, 15.5 k for the pair, against
  `Docs/3d-asset-pipeline.md`'s 3–6 k *per character*. On a PC build whose bottleneck was measured
  as draw calls (21,946 on the island), this is a **style** decision, not a performance one — and
  the wizard's own rule is "do not decimate a mesh that doesn't need it." Decide by looking at the
  arms next to Tracey's body, not by the number. If they must come down, decimate with the finger
  loops protected.
- **Texture is a placeholder.** `Tracey_Arms_BaseColor.png` is the vendor albedo lifted 22 % toward
  white. It exists so the arm-set swap is visible; it is not art.
- **Not yet seen in play mode.** Verification so far is an offscreen preview-scene render. Per
  `verification_requires_a_build`, the real confirmation is the built exe.
