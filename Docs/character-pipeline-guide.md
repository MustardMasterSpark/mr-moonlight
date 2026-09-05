# Character Pipeline — Start to Finish

**The one guide for building any character in Mr. Moonlight**, from "I'm about to start modelling"
to "she's in the game, animated, holding a weapon."

Written 2026-09-05. Companion docs, both still valid — this guide is the **spine**, they are the
**detail**:

- `Docs/3d-prop-pipeline-wizard.md` §4 — the deep detail for Stages 2–6 (already written, don't duplicate it)
- `Docs/tracey-rig-strategy.md` — *why* the architecture is what it is, and the build record for the arms

---

## Where am I? — jump table

Say the phrase in the last column and work continues from there.

| Stage | What happens | Who | Say this |
|---|---|---|---|
| **0** | Rules to know **before** you model | 👤 you | — read it, it's short |
| **1** | Model the character | 👤 you | *"Stage 1 done, here's the mesh"* |
| **2** | Blender cleanup, scale, T-pose check | 🤖 Claude | *"Stage 2, clean up <name>"* |
| **3** | AccuRig | 👤 you | *"Stage 3 done, here's the rigged FBX"* |
| **4** | Confirm the rig survived | 🤖 Claude | *"Stage 4, verify <name>'s rig"* |
| **5** | Unity import as Humanoid | 🤖 Claude | *"Stage 5, import <name>"* |
| **6** | Body animations | 👤 you + 🤖 | *"Stage 6, wire <name>'s animations"* |
| — | **Enemies and NPCs stop here. Done.** | | |
| **7** | Viewmodel arms *(player only)* | 👤 you + 🤖 | *"Stage 7, build <name>'s FP arms"* |
| **8** | The visible body *(player only)* | 🤖 Claude | *"Stage 8, build <name>'s visible body"* |
| **9** | Final checklist | 🤖 Claude | *"Stage 9, check <name>"* |

---

## STAGE 0 — Four rules to know before you model anything

These are the ones that are **cheap now and expensive later**. Everything else can be fixed.

### Rule 1 — Hands must have real fingers 🚨

**This is the one that cannot be fixed later.** The weapon animations drive **15 finger bones per
hand** — trigger pulls, thumb on the hammer, fingers wrapping a magazine. A mitten or a paddle has
nothing to drive, and the reload animations will read as broken, not stylized.

You need **5 fingers, 3 visible segments each**, on both hands. Low-poly blocky fingers are fine —
PS1-era hands were exactly that. What is not fine is fingers fused into one shape.

> Tracey's current blockout has a 152-vert paddle for each arm with no fingers. That needs to change
> before Stage 1 is done.

### Rule 2 — Real human height, feet at the origin

**1 Blender unit = 1 metre.** An adult is ~1.7 m. Feet at Z = 0. Apply all transforms.

> Tracey's blockout is currently exactly **1.000 m tall**. Scale her to ~1.70 m and *apply* the
> scale in Blender — don't fix it with an import setting later.

### Rule 3 — T-pose, arms dead level

Arms straight out sideways, level, palms down. Not an A-pose, not "close enough". Stage 2 checks
this numerically, and `3d-prop-pipeline-wizard.md` §4.7 explains the day this costs when it's wrong.

### Rule 4 — Edge loops where the body bends

Three loops minimum at each of: **shoulders, elbows, wrists, hips, knees, ankles**. This is the
single thing that decides whether the character deforms or folds.

**Plus, if the character will ever be dismembered** (any enemy going through Gore Simulator): denser,
more even triangles at neck, shoulders, elbows, hips, knees. See wizard §4.11 — the Spotter is
currently stuck without this and the fix is deferred because retrofitting it is expensive.

---

## The map, in one picture

```
                    👤 YOU MODEL                 🤖 CLAUDE                 👤 YOU
                    ───────────                  ─────────                 ──────
  STAGE 1 ────────► full body mesh
                         │
  STAGE 2 ───────────────┴──────────────────► cleanup, scale, T-pose check
                                                       │
  STAGE 3 ◄────────────────────────────────────────────┴────────────────► AccuRig
                                                                              │
  STAGE 4/5 ◄─────────────────────────────────────────────────────────────────┘
                    verify rig survived → Unity import as HUMANOID
                                                       │
  STAGE 6 ─────────────────────────────► locomotion + Mixamo/QuickMagic clips
                                                       │
                    ┌──────────────────────────────────┴─────────────────────┐
                    │                                                        │
            ENEMIES / NPCs                                          THE PLAYER ONLY
              ✅ FINISHED                                                    │
                                            ┌────────────────────────────────┴────────┐
                                            │                                         │
                                     STAGE 7 — arms                          STAGE 8 — body
                              👤 you hand over the arms mesh          🤖 hide arms + head,
                              🤖 binds them to the vendor                  drive locomotion
                                 weapon skeleton (Arms_Root)               from the controller
```

**Why the fork.** The player needs **two skeletons**: a Humanoid body (Stages 1–6, same as every
enemy) *and* a separate 44-bone Generic arms rig that lives under the camera and plays the HQ FPS
weapon animations. They never merge. The full reasoning is in `Docs/tracey-rig-strategy.md` §1–3;
the short version is that the weapon clips are authored in **camera space** on a skeleton with no
spine, so they physically cannot be retargeted onto a body.

---

## STAGE 1 — 👤 Model the character

Your step. Tripo → Blender, or modelled from scratch. Follow the four Stage 0 rules.

**Hand over:** the `.blend` (or an FBX) with the body in a T-pose.

**If this is the player**, also read Stage 7 now — there's a second, small thing to model, and it's
easier to do while you're already in the arms.

➡️ Say: *"Stage 1 done, here's the mesh"* + the file path.

---

## STAGE 2 — 🤖 Blender cleanup, scale, T-pose check

Claude does this over the Blender MCP bridge. Detail in wizard §4.2 / §4.2a / §4.3.

- Apply transforms, 1 unit = 1 m, origin at the feet
- Join the loose parts into one mesh; one material, one UV set
- UVs get real attention — auto-unwrap is fine for a rock, not for a face
- **T-pose verified numerically, not by eye** — arm chain Y-delta, leg straightness, symmetry.
  You get the actual numbers back ("left arm Y delta 0.003 m, right 0.041 m"), not a verdict

**You get back:** a cleaned `.blend`, an FBX for AccuRig, and the T-pose numbers.

---

## STAGE 3 — 👤 AccuRig

Your step, unchanged from the Spotter. Three settings that matter:

| Setting | Value | Why |
|---|---|---|
| Rig type | **Full body + fingers** | Finger bones are required. Without them Stage 7 is impossible |
| Input pose | **T-pose, explicitly** | Never "auto-detect", never A-pose |
| Export | FBX | — |

**Note AccuRig's reported "Character height: N cm"** — Stage 5 needs it to fix the import scale.

➡️ Say: *"Stage 3 done, here's the rigged FBX"* + the path + the height AccuRig reported.

---

## STAGE 4 — 🤖 Confirm the rig survived

AccuRig has a documented habit of **silently reverting a T-pose toward its own A-pose**. Nothing
errors when it does — the character imports, the avatar configures, the clips play. They just play
wrong, and it reads as "the animation is bad." Wizard §4.5 / §4.7.

Claude re-imports the FBX and re-runs the numeric T-pose check. **If it reverted, it gets fixed in
AccuRig — never compensated downstream.**

---

## STAGE 5 — 🤖 Unity import as Humanoid

Wizard §4.6 has the full detail. The three that bite:

1. **Animation Type = Humanoid**, Configure Avatar, all required bones green
2. **Map the finger slots by hand.** Unity does not reliably auto-detect fingers, and both
   QuickMagic hand retargeting *and* Stage 7 depend on them
3. **⚠ Scale is not guessable.** On the Spotter, Unity's auto-heuristic chose `0.5561879` (garbage),
   and `1.0` was wrong too — AccuRig writes raw centimetres with unit metadata left at 1.
   `0.01` fixed that character. **Verify per character** by comparing the imported bounds against
   AccuRig's reported height. You get the actual measured bounds back.

---

## STAGE 6 — 🤖 Body animations

The character is now Humanoid, so **any Humanoid clip retargets onto it** — that's the whole point
of the format, and it's already proven on the Spotter with Mixamo clips.

| Source | Used for | Notes |
|---|---|---|
| **Mixamo** | Locomotion, deaths, the general library | Already working on the Spotter |
| **QuickMagic** | Custom mocap, especially cutscenes | Exports FBX/BVH with skeleton presets |

**QuickMagic's 20-minute acceptance test, before you commit to it for anything:** export one clip as
FBX → import in Unity with Animation Type = **Humanoid**, Avatar = *Create From This Model* → open
**Configure Avatar**. Bones green = done, it retargets onto every character in the project. Partly
green = hand-map the gaps once and save the avatar. Only if the hierarchy is unreadable do you need
Retarget Pro (`Docs/retarget-pro-strategy.md` §13).

**⚠ Test with ONE simple clip before importing forty.** Wizard §4.9. Batching before this check means
finding a rig bug four characters deep instead of one.

### ✅ Enemies and NPCs are finished here.

Spotter, Zealot, Wendigo, Holly, Vernon, Scott — all stop at Stage 6. Nothing below applies to them.
(Wendigo may not be Humanoid at all if its skeleton isn't a biped; that's the Retarget Pro case.)

---

## STAGE 7 — 👤+🤖 Viewmodel arms — **the player only**

This is the new territory, and the part worth reading twice.

### What this is

The hands you see holding a weapon are **not** the character's body arms. They are a separate mesh
on a separate 44-bone skeleton called `Arms_Root`, living under the camera, rendered at its own 60°
FOV. Every HQ FPS weapon animation — all 13 weapons, equip/idle/fire/reload/holster — is authored
for **that exact skeleton** and cannot be retargeted onto anything else.

So Stage 7 is: **take your character's arms, and put them on that skeleton.**

### The good news, up front

- **It is ONE job, not thirteen.** There are 15 `FP_Arms_*.fbx` files in the asset, but they're
  animation containers. At runtime there's a single arms mesh, and equipping a weapon just swaps the
  animator controller. Register your arms once and all 13 weapons pick them up.
- **The vendor already built the slot for it.** `WieldableArmsHandler` holds an `ArmSet[]` list with
  four sets already in it. Yours becomes a fifth, at index 0 (the default).
- **The whole downstream half is already automated and tested.** See §"What's already built".

### 👤 What you hand over

**Two routes. Pick one — route A is the default.**

#### Route A — hand over nothing extra *(default, least work)*

Claude extracts the arms from your **AccuRig-rigged body FBX** from Stage 3.

This works because **AccuRig's arm bones map 1:1 onto the vendor arm bones by name**:

| AccuRig | Vendor |
|---|---|
| `CC_Base_L_Upperarm` | `UpperArm.L` |
| `CC_Base_L_Forearm` | `Forearm.L` |
| `CC_Base_L_Hand` | `Hand.L` |
| `CC_Base_L_Index1/2/3` | `Index.1.L / .2.L / .3.L` |
| `CC_Base_L_Mid1/2/3` | `Middle.1.L / .2.L / .3.L` |
| `CC_Base_L_Ring / Pinky / Thumb 1-3` | `Ring / Pinky / Thumb .1-.3 .L` |
| *(no counterpart)* | `ForearmTwist.1–4.L` → folded into `Forearm` |

**19 of 22 bones per arm, exact.** So converting the weights is a dictionary lookup, not a guess.

**Use route A when** the body's hands already have proper fingers and you're happy with them
close-up.

#### Route B — hand over a dedicated arms mesh *(better close-up quality)*

The viewmodel arms fill the screen and are the most-looked-at asset in the game. A dedicated mesh
lets you give them more finger detail and a proper sleeve cuff without paying for it on the whole
body. **Use route B when** the body's hands are simplified, or you want a rolled sleeve / glove cuff.

**Spec — hand it over like this:**

| | |
|---|---|
| **Pose** | The **same T-pose** as the body. Do **not** try to match the vendor's rest pose — Claude does that automatically |
| **Space** | Same Blender file / same world position as the body, at final 1.7 m scale |
| **Cut** | Just **above the shoulder joint**, not at the elbow. `UpperArm` is the root bone and needs geometry |
| **Ends** | Leave the shoulder end open or roughly capped — it's off-screen behind the camera |
| **Fingers** | 5 fingers, 3 segments, both hands. **Non-negotiable** (Stage 0 Rule 1) |
| **Objects** | Two objects (`<Name>_Arm_L` / `_R`) or one — either is fine, just name them clearly |
| **Material** | One material, one UV set |
| **Weights** | Not needed. Claude does the weighting |

**One expectation to set:** the vendor bone lengths **cannot change** — the animations are authored
for those exact proportions, and stretching a bone would throw the weapon out of the hand. So your
arm mesh gets **re-fitted onto the vendor arm proportions**. The viewmodel arm may end up slightly
differently proportioned from the body's arm. Nobody ever sees both at once, and every FPS on the
market does this.

### 🤖 What Claude does, automatically

1. Renames your AccuRig arm vertex groups to the vendor names via the 1:1 map above
2. Folds the `ForearmTwist` chain into `Forearm`
3. **Pose-matches** — bridges your T-posed arm onto the vendor's relaxed rest pose using a temporary
   armature, so the mesh ends up in the exact pose the skeleton expects
4. Exports the FBX with the settings that work *(see Trap 1 — the default settings are wrong)*
5. Runs **`Tools > MrMoonlight > Character > Build Tracey FP Arms`**, which:
   - forces the import settings (Generic, no animation, no materials)
   - **transplants the vendor bindposes** *(see Trap 2 — this is not optional)*
   - builds the material on `LitFieldOfView_SSS`
   - wires both renderers into the arms prefab, bound to the live skeleton
   - registers the arm set at index 0 so it becomes the default hands
6. **Renders both arms offscreen, posed by a real reload clip**, and shows you the picture before
   calling it done

➡️ Say: *"Stage 7, build \<name\>'s FP arms"* — and say which route.

### 👤 Then: the texture

You repaint `<Name>_Arms_BaseColor.png` — 512×512, same UV layout. Everything else is automated.

---

## STAGE 8 — 🤖 The visible body — **the player only**

So you can look down and see your own legs.

The body from Stages 1–6 gets attached to the player, with its **arms and head bones scaled to zero**
so you never see them from inside. You see coat, hips, legs, boots looking down; you see the Stage 7
hands looking forward. Different meshes, different FOVs — no camera angle shows the seam.

What Claude builds:

1. `AC_<Name>Body.controller` — locomotion blend tree (idle/walk/run/crouch/jump/land) plus an
   upper-body Avatar Mask layer for lean
2. `MoonlightBodyDriver` — reads the controller for speed/grounded/crouched, feeds the Animator,
   matches body yaw to camera yaw with lag, hides the arm and head bones. All numbers in
   `MoonlightTunables`
3. A shadow split so the *shadow* keeps its arms while the *visible* mesh doesn't

**Three rules baked in, so you don't get surprised:**

- **The camera is never parented to the head bone.** The FPS controller already owns head bob, sway
  and recoil; stacking an animated head bone on top fights it and makes people motion sick
- **The body follows camera yaw only, never pitch.** Pitch becomes a small torso lean at most
- **The body is cosmetic.** No colliders, no movement, no hit detection. A puppet on a string

**Bonus you get for free:** the player finally casts a real flashlight shadow. In a dark horror game
that's one of the highest-value visual returns available.

---

## STAGE 9 — 🤖 Final checklist

Claude verifies and reports actual numbers, not verdicts:

- [ ] Imported height matches the modelled height
- [ ] Avatar is Humanoid, all required bones + fingers mapped
- [ ] One test clip plays clean
- [ ] *(player)* All 13 weapons show the new hands
- [ ] *(player)* Looking down shows legs, not a floating camera
- [ ] *(player)* No arms or head visible from inside
- [ ] Materials are RetroLit *(body)* / `LitFieldOfView_SSS` *(viewmodel arms — see Trap 3)*
- [ ] Verified **on a built exe**, not just in the editor — `verification_requires_a_build`

---

## Appendix A — What's already built and tested

Done 2026-09-05 as a dry run of Stage 7, using the vendor's own arm mesh as a stand-in. **The mesh
was a placeholder and will be replaced by yours** — but everything downstream of the mesh is built,
tested and reusable:

| Built | Where |
|---|---|
| The build tool (idempotent, re-runnable, **mesh-agnostic**) | `Assets/_Project/Code/Editor/Migration/MoonlightTraceyArmsBuild.cs` |
| Bindpose transplant + abort-on-mismatch | same file |
| Arm-set registration at index 0 | same file |
| The matching **Remove** tool | same file |
| Full build record + measurements | `Docs/tracey-rig-strategy.md` §9 |

**✅ The game is clean — nothing placeholder is live.** The dry run briefly registered a "Tracey" arm
set using the *vendor's* mesh; Carlos had it reverted the same day, correctly, since it wasn't the
real art. Verified from disk: the arms prefab is back to its vendor baseline (8 renderers, 4 arm
sets, `Standard` at index 0).

**What survived is the machinery, and that's the point.** The tool works on **any** mesh bound to
`Arms_Root`, so Stage 7 is now a matter of handing it your arms — the import settings, bindpose
transplant, material, prefab wiring, arm-set registration and verification render are all written
and proven. Until your arms exist it aborts cleanly with *"export the arms from Blender first"*.

> **Why there's a `Remove` menu item and not just "undo it in git":** the arms prefab lives under
> `Assets/ThirdParty/`, which `.gitignore` excludes wholesale. Git cannot revert an edit it never
> saw. Anything this pipeline writes into vendor territory needs a scripted way back out.

---

## Appendix B — The three traps, and why they're silent

All three were found the hard way on 2026-09-05. Detail in `Docs/tracey-rig-strategy.md` §9.

### Trap 1 — Blender's default FBX export is 100× off

Exporting with Blender's defaults produced a mesh **exactly 100× too small** in Unity. The vendor FBX
is centimetre-native; Blender writes metres and Unity's *Convert Units* then applies the wrong
conversion.

**Fix in Blender, not the importer:** `apply_scale_options='FBX_SCALE_ALL'` with `global_scale=1.0`.

⚠ This is the **opposite direction** to the AccuRig scale bug (which needs `0.01`). Don't reason
about which multiplier applies — **compare the bounds against a known-good mesh** and let the numbers
decide. Stage 5 and Stage 7 both do this automatically.

### Trap 2 — Blender silently twists bones on a round trip

Blender recalculates bone roll when it imports a rigged FBX. Coming back out, **15 of 44 bones** had
bindpose rotations off by up to `0.109` — every left-hand finger, plus the whole right forearm chain.
Binding that to the vendor skeleton twists exactly those joints, and it only shows up **mid-reload,
on one hand**, which reads as "the animation is broken."

**Fix:** the bone *order* survives, so the tool copies the vendor's bindpose array wholesale onto the
new mesh. Exact, not approximate. It runs on every build, and aborts loudly if the skeleton ever
stops matching rather than binding something plausible-looking.

### Trap 3 — The viewmodel arms are **not** RetroLit

MRM-9 recorded *"hand material finish → RetroLit."* That cannot hold literally. The vendor arms use
`Shader Graphs/LitFieldOfView_SSS`, and the FOV nodes in that graph are what pull the arms to their
own 60° FOV. RetroLit has no such node — RetroLit arms would render at world FOV and **clip through
walls**.

**Open question for Carlos:** if the hands must be RetroLit for style, the FOV nodes need porting
into a RetroLit viewmodel variant first. That's very likely the same unfinished
`RetroLitViewModel.shader` already blamed for the dark-weapons bug — worth solving once for weapons
and hands together, as its own job.

---

## Appendix C — Which characters get what

| Character | Stages 1–6 | Stage 7 (arms) | Stage 8 (body) |
|---|---|---|---|
| **Tracey** (player) | ✅ | ✅ | ✅ |
| Spotter | ✅ *(done)* | — | — |
| Zealot, Holly, Vernon, Scott | ✅ | — | — |
| Wendigo | ✅ *(may not be Humanoid — non-biped)* | — | — |
| Robert, William, Rylee, Shannon | **No 3D model** — Polaroid faces only | — | — |

**The rule in one line:** anything the player sees *from the outside* needs Stages 1–6. Only what the
player sees *from the inside* also needs 7 and 8 — and that's the player alone, plus any future
playable character.
