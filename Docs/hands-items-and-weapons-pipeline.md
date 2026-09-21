# Hands items and weapons — how to add one, and what must stay compatible

Written 2026-09-21 (MRM-87), from reading the framework code while adding the Syringe. **Read this before adding
any new weapon, tool or hand-held item** — anything that "takes over the hands": the map and compass, a lighter,
a radio, a new gun, a new drug. It is a reference plus a checklist. It is **not** a wizard yet; when the first new
item is added through it, fold the lessons back in here (the `/prop` wizard does the same for props).

Status of the claims below: everything marked **(read)** was read in the code or assets on 2026-09-20/21.
Everything marked **(inferred)** or **(untested)** is a hypothesis; verify before relying on it.

Related: `Docs/mrm25-weapon-test-arsenal.md` (the 13-weapon arsenal, the tools that build it),
`Docs/mrm9-hqfps-integration.md` (why HQ FPS is the controller and weapon system),
`Docs/character-pipeline-guide.md` + `Docs/tracey-rig-strategy.md` (Tracey's arms and body),
`Docs/retarget-pro-strategy.md` (animation retargeting), `Docs/3d-prop-pipeline-wizard.md` (the mesh and material side),
`Docs/controls.md` (which keys are taken).

---

## 1. The mental model (read)

Anything held in the hands is a **Wieldable**: a prefab under the player's `WieldablesController`, all children
disabled except the active one. Five ideas explain almost everything:

1. **One active wieldable, plus an equip stack.** `WieldablesController` keeps a stack of equipped entries (index 0 is
   the "hands"/null wieldable). `TryEquipWieldable(x)` pushes `x` on top; the old top is holstered (Holster animation),
   then `x` is equipped (Equip animation). `TryHolsterWieldable(x)` removes `x` from the stack, and if it was on top the
   entry underneath comes back. **This is why "use the syringe, then get my weapon back" costs nothing.** The stack
   also keeps history, so switching A → B → syringe → holster returns to B.
2. **Equip and holster timing are numbers on the prefab, not read from the clips.** `Wieldable._equipDuration` and
   `_holsterDuration` (both 0.5 s by default) are how long the code *waits*. The animation clips run independently.
   If a clip is longer or shorter than the number, the item is either usable mid-animation or frozen after it. **Match
   them by hand when you add a clip.**
3. **Two animators per item, one controller template.** Each wieldable prefab has a `WieldableAnimator` (the *prop*,
   for example the syringe mesh) and a `WieldableArmsAnimator` (the *arms*, driving Tracey's hands). `Wieldable.Awake`
   collects every `IAnimatorController` beneath it, and a `MultiAnimator` sends every trigger to both. Each one takes an
   `AnimationOverrideClips`: a base **controller** plus **Original → Override** clip pairs. So a new item is a
   controller template + the clips you drop into its slots, not a new state machine.
4. **The hands are one shared arm mesh.** There is exactly one arms mesh in the game (`HQFPS_Wieldable_Arms.prefab`,
   the `Arms_Root` skeleton, 44 bones, **Generic** rig). Equipping an item only swaps the arms' `runtimeAnimatorController`.
   The 15 `FP_Arms_*.fbx` files are **animation containers**, not meshes (`Docs/tracey-rig-strategy.md`).
5. **Items are inventory items.** A wieldable is tied to an `ItemDefinition` by `WieldableItem._referencedItem`. The
   player's `Holster` container holds the item; `WieldableInventory` builds a lookup from item id to wieldable and
   equips by slot. Anything in the holster can be selected by index (`SelectAtIndex`).

### The interfaces the input layer dispatches to

`FPSWieldablesInput` looks at the active wieldable and calls whichever of these it implements. Implement only what
the item needs:

| Interface | Called for | Notes |
|---|---|---|
| `IUseInputHandler.Use(phase)` | Left mouse (Start / Hold / End) | Return true to say "handled". `WieldableTool` fires a `UnityEvent`. `UseBlocker` is an `ActionBlockHandler` |
| `IAimInputHandler` | Right mouse | Firearms. A map does not need it |
| `IReloadInputHandler` | R | Firearms |
| none | (nothing) | A passive item that only equips, idles and holsters needs just `Wieldable` |

Handlers on the character (not on the item) do special routing: `WieldableHealingHandler` (heal key), 
`WieldableThrowableHandler` (throw key), `MoonlightWeaponCategorySwitcher` (number keys), `MoonlightWeaponCycler`
(gamepad bumper). A new "press this key to pull the thing out" item needs a handler like `WieldableHealingHandler`.

---

## 2. What the HQ asset already gives us (read)

**Scripts:** `Wieldable` (base), `WieldableTool` (hold + use + equip/holster `UnityEvent`s, no code needed for simple
cases), `HealingWieldable`, `MeleeWeapon`, `Firearm`, `WieldableThrowableHandler`.

**Animator controller templates** (`Assets/ThirdParty/AST-046/FPSCore/Art/Animations/Wieldables/Controllers/`):
`Template_Wieldable` (Equip → Idle → Holster; **the one for a passive item like a map**), `Template_Tool` (adds Use),
`Template_Unarmed`, `Template_Throwable`, `Template_MeleeBasic`, `Template_MeleePolearm`, `Template_FirearmDefault`,
`Template_FirearmCharge`, `Template_FirearmProgressive`. `Templates/` holds generic placeholder clips
(`Template_Equip`, `_Idle`, `_Hold`, `_Holster`, `_Use` and the firearm ones) so a new item can be built before its
real animation exists.

**The Syringe is the best working example of a non-weapon item.** Its prefab
(`HQFPS_Wieldable_Syringe`, variant `_Project/Prefabs/Weapons/Item/MRM_Item_Syringe.prefab`) is:

| Part | What it is |
|---|---|
| Root | `HealingWieldable` + `WieldableItem` (item id 7270836). Heal 50, 2 s, movement ×0.75 while healing |
| `ViewModel` child | Animator + `WieldableAnimator` (prop) + `WieldableArmsAnimator` (arms) |
| Controller | `Template_Wieldable`. Equip/Holster/Idle slots overridden with the syringe clips |
| Clips | `Arm_Syringe_Hold_` (1 frame) and `Arm_Syringe_Use_` (~78 frames) on the arms; `Syringe_Hold_` and `Syringe_Use_` on the prop. **(inferred)** the Use clip is the one played as *Equip* (it is the only long clip and Equip Speed is 1.5) |
| Audio | `HQFPS_Syringe_Use.wav`, three-part `AudioSequence` on heal |

**Not usable:** `HQFPS_Wieldable_Flashlight.prefab` exists, but its mesh folder holds only materials (the FBX was not
migrated), and MRM-44 ruled the flashlight is a plain toggled Spot Light on F. Do not build on it.

**Not provided anywhere:** a map, compass, notebook, radio, or any "flat object held up in both hands" pose. The
Syringe's hand poses grip a small cylinder and will look wrong on a map. That clip has to be authored (§5).

---

## 3. Checklist — adding a new hand-held item or weapon

Owner column: 👤 Carlos, 🤖 Claude. **Ask Carlos before touching Unity or Blender** (CLAUDE.md hard rules).

| # | Step | Owner | Where / how | Traps |
|---|---|---|---|---|
| 1 | **Decide the type**: passive item, tool, consumable, melee, firearm, throwable | 👤+🤖 | Picks the script and controller template (§1, §2) | A drug is a consumable: reuse the Syringe's structure, not a new system |
| 2 | **Model + textures** | 👤 model, 🤖 wizard | Run `/prop` (`Docs/3d-prop-pipeline-wizard.md`) | RetroLit samples **BaseColor + Normal only**. Glowing parts get a real Light on the prefab, never emission. Put the mesh on the **ViewModel** rendering layer (`Docs/viewmodel-light-layers.md`, handled by `MoonlightViewModelLighting` for everything under the camera) |
| 3 | **Hand animation clips** | 👤 author or mocap, 🤖 import | On the `FP_Arms` **Generic** skeleton (§5): Equip, Holster, Idle/Hold, plus Use or the item's own actions | A body-rig clip cannot simply be dropped here (§6). Clips are authored **in camera space** |
| 4 | **Prop clips** (only if the prop itself moves) | 👤 | Second `WieldableAnimator` on the prop, or a `WieldableMovingPart` | Optional. A static map needs none |
| 5 | **Override-clips asset** | 🤖 | `AnimationOverrideClips`: base controller = the right `Template_*`, Original → Override pairs | Set `_equipDuration` / `_holsterDuration` on the wieldable to the real clip lengths. Equip Speed is a default parameter on the `WieldableAnimator` |
| 6 | **Script** | 🤖 | `MoonlightXxxWieldable : Wieldable` in `_Project/Code/Runtime/Player` (or a Mr. Moonlight folder). **Do not add scripts to `Vendor/`** unless it is a fix to vendor behaviour (§7) | Override `OnStateChanged` (Equipping/Equipped/Holstering/Hidden), `IsCrosshairActive() => false` for a non-weapon. Every tunable value goes in `MoonlightTunables`, not the script |
| 7 | **Prefab** | 🤖 | Variant of a vendor wieldable, or new from the Syringe. Needs: the script, `WieldableItem`, both animators, and a **`WieldableMotion`** | `Wieldable.Awake` logs *"No motion handler found"* without a `WieldableMotion`. Put it in `_Project/Prefabs/Weapons/<Category>/MRM_<Category>_<Name>.prefab` (tracked in git; `ThirdParty/` is not) |
| 8 | **Item definition** | 🤖 | `_Project/Data/PolymindGames/Resources/Definitions/Item/`. Set the **Wieldable tag `6549466`**, a `StackSize`, an icon and a pickup prefab | **Untagged, the Holster silently refuses it** (the Syringe shipped that way). **StackSize 1 means one use empties the slot for good** (§7). The stack size is the ceiling, whatever the loadout asks for |
| 9 | **Register it** | 🤖 | Add an `Entry` to `MoonlightWeaponSet.All` (`Editor/Migration/`), category `Item` if it has no number key. Then run the tools in `Tools > MrMoonlight > Weapons` **1 → 2** (idempotent), and 3 for audio | The Holster has **16 slots** (`PolymindPlayerBuild.HolsterSlots`); 14 are used today. A full holster fails *quietly*. **Never run "Build Player_Tracey from FPS_Player"**; it destroys the scene instance's extra components (`mrm25` doc §3) |
| 10 | **Key + handler** | 🤖 | Add an action to `Assets/InputSystem_Actions.inputactions`; that is the asset the player reads. The generated wrapper `Code/Runtime/Input/InputSystem_Actions.cs` regenerates on reimport. Then a handler on the character like `WieldableHealingHandler` | `FPS_InputActions.inputactions` (`_Project/Data/PolymindGames/Input`) is the **vendor's original, not read by the player**, and its bindings are misleading (Heal on J, V on "Melee"). Check `Docs/controls.md` for free keys. Keyboard-only is fine (Carlos's ruling for the arsenal) |
| 11 | **Starting loadout / pickup** | 🤖 | Testing: add to `MoonlightStartingLoadout`. Shipping: MRM-26 pickups | A serialised list on the scene or prefab does **not** update when you change a C# initializer. Change the data on `Player_Tracey.prefab` |
| 12 | **Verify in Play Mode, with Carlos** | 👤 | Equip, use, holster, return to the previous weapon, switch mid-use, use at full health | Editor screenshots and "no errors" are not proof |
| 13 | **Document** | 🤖 | A row in `Docs/prop-log.md`, this doc's lessons, `Docs/controls.md`, and the change record (`Docs/performance-sessions.md` §8) | |

### Item types at a glance

| Type | Script | Controller template | Needs a handler on the character? |
|---|---|---|---|
| Passive (map, compass, notebook) | new `Wieldable` subclass | `Template_Wieldable` | Yes: a key that equips and holsters it |
| Tool with a use action | `WieldableTool` (events) or a subclass | `Template_Tool` | Optional |
| Consumable (syringe, drug, food) | `HealingWieldable` or a subclass | `Template_Wieldable` (as the Syringe) | Yes: `WieldableHealingHandler` pattern |
| Melee | `MeleeWeapon` | `Template_MeleeBasic` / `Polearm` | No: number key |
| Firearm | `Firearm` | `Template_Firearm*` | No: number key. Infinite reserve comes from swapping the ammo provider |
| Throwable | vendor throwable | `Template_Throwable` | Throw key (`WieldableThrowableHandler`) |

---

## 4. Worked plans

### 4.1 Map and compass (not built)

A passive wieldable that shows a map UI while equipped. **Minimum viable, ~half a day, no custom animation:**
`MoonlightMapWieldable : Wieldable` (or `WieldableTool` with events), `Template_Wieldable` controller with the
`Template_Equip/Idle/Holster` clips, a plain quad as the map, a new key with a handler that toggles equip. The
Equip/Holster placeholders will look generic; that is acceptable to prove the key → equip → UI → return-to-previous-weapon
chain first. **Real version:** author a two-handed "hold a flat object up" Hold clip plus short Equip and Holster on
`FP_Arms` (§5). Also decide: does the map block movement? (`SpeedModifier`), the crosshair (`IsCrosshairActive`),
fire (`UseBlocker`), and does the compass needle need its own prop animator?

### 4.2 Drugs and morphine (not built; hook identified)

The Syringe is the morphine. **Design intent (Carlos, 2026-09-20): any item can be used at any time, even if wasted;
that is the player's responsibility.** So the full-health gate was removed from `WieldableHealingHandler.TryHeal`.
Effects beyond healing (a defence buff, and so on) belong in `HealingWieldable.HealDelayed`, the point where the
2-second timer completes and health is restored, or in a subclass per drug. Rules for whoever builds this:
values in `MoonlightTunables`, effects as timed modifiers on the existing stat systems (MRM-12 stats, the MRM-41
item catalogue), and each drug is an item definition + prefab as in §3. Do not add a "can this be used now?" gate
unless Carlos asks for one.

---

## 5. Animation: what a hands clip is, and how to make one

**The target is the vendor `Arms_Root` skeleton**, not Tracey's body. (read: `Docs/tracey-rig-strategy.md`)

- 44 bones, **Generic** rig, two roots (`UpperArm.L`, `UpperArm.R`), no hips, spine or clavicle. **It cannot be a
  Humanoid avatar at all.**
- Clips are authored in **camera space**: the arms are constrained to the camera's motion mixer
  (`IMotionMixer.TargetTransform`). A clip made for a body in world space will not sit right.
- Import the FBX with **Animation Type = Generic**, **Avatar = Copy from** the vendor FP_Arms avatar, one clip per take
  (the Syringe FBX splits `Arm_Syringe_Hold_` frames 0-1 and `Arm_Syringe_Use_` frames 1-79). Set the frame ranges
  in the import settings.
- **Clip length must match the Wieldable's `_equipDuration` / `_holsterDuration`** (§1 point 2). Frame rate is not
  recorded here **(untested)**; read the clip length in the Inspector before setting the numbers.
- Blender round-trip traps that apply here: 100× scale (use `FBX_SCALE_ALL`) and recalculated bone roll twisting the
  fingers (`memory: blender_fbx_rig_roundtrip_traps`, `blender_export_process`). Our own exports are 1 unit = 1 m
  and feet-origin; **AccuRig exports are centimetres and need `globalScale = 0.01`** (`mrm75_accurig_scale_bug`).

**Authoring routes, cheapest first:**

1. **Reuse a Template clip** as a placeholder (zero cost, generic look).
2. **Hand-key it in Blender** on the FP_Arms rig (best control; the pose for a map is only a few keys).
3. **Retarget an existing clip** with Retarget Pro in the Playground project (`Docs/retarget-pro-strategy.md`, zero
   build footprint: only the baked `.anim` or `.fbx` enters Mr. Moonlight). **(untested for arms → arms)**.
4. **Video/mocap** (§6). Mentioned in the pitch as heavy use for arm and hand animation.

---

## 6. Provision for Tracey's own hands and for QuickMagic mocap

**Goal: nothing built now may need redoing when Tracey's real model, her own arms and QuickMagic motion arrive.**
The architecture is already decided (`Docs/tracey-rig-strategy.md`; do not re-litigate): **two skeletons that never touch.**

| | Rig A: viewmodel arms | Rig B: Tracey's body |
|---|---|---|
| Rig type | Generic (`Arms_Root`, 44 bones) | Humanoid (AccuRig) |
| Where | Under the camera, own 60° FOV, ViewModel light layer | The character, hidden arms and head, legs visible looking down |
| Plays | **Every** weapon and hand-item clip | Locomotion, deaths, cutscenes, **QuickMagic mocap** |
| Clips from | HQ FPS + our own authored clips | Mixamo, libraries, QuickMagic |

What this means for adding items:

1. **Hand-item clips belong to Rig A, always.** Never author a new item's clips against Tracey's body. QuickMagic clips
   go on Rig B only. A map's hold pose is an Arms_Root clip.
2. **New hands are one swap, not thirteen.** Tracey's arms (`Docs/character-pipeline-guide.md` Stage 7, route A from
   AccuRig or route B, a dedicated mesh) become one more `ArmSet` in `WieldableArmsHandler`; every clip keeps working
   **as long as the bone names and the rest pose are preserved.** AccuRig's arm bones map 1:1 by name (19 of 22 per arm;
   the twist bones fold into the forearm). Rebuild with `Tools > MrMoonlight > Character > Build Tracey FP Arms`
   (undo: `Remove Tracey FP Arms`). **Consequence for us: author every new hand clip against the vendor
   skeleton and rest pose, never against a proportion that only exists on a new arm mesh.**
3. **Do not bake proportions into clips.** If Tracey's real arms are longer or shorter than the vendor's, clips keyed
   with hands touching a prop will drift. Keep props parented to the **hand bones of Rig A** (`Hand.L` / `Hand.R`) with
   a per-item offset, so a new ArmSet only means adjusting offsets. **Verify the first item on the real arms before
   authoring many.** **(untested)**
4. **Fingers must be real** (Character guide Stage 0, Rule 1). Fingerless hands make hand-item animation impossible.
5. **QuickMagic can help viewmodel clips, but not directly. (untested, validate with ONE clip, like the 20-minute
   acceptance test in the Character guide, Stage 6.)** QuickMagic exports FBX or BVH for a body skeleton, so:
   - *For Rig B:* import as Humanoid, check Configure Avatar; done.
   - *For Rig A:* the body clip has to be **baked onto `Arms_Root`** (Generic to Generic by bone-name mapping, the same
     idea as AccuRig's 1:1 arm map) and **re-based into camera space**. Retarget Pro is the candidate tool and lives in
     the Playground project. Success test: bake one arm-only clip, play it on the arms in a weapon slot, and check it
     against a known vendor clip for wrist and finger alignment. If it drifts, fall back to hand-keying or to using
     the video only as a **reference**.
   - The pitch document mentions a GoPro POV capture as the source for first-person arm motion; a POV capture is
     already in camera space and is the better candidate for Rig A than a third-person body capture.
6. **Provision in the doc, not in code.** Nothing needs to be built now. When the first new item is animated, record
   in `Docs/prop-log.md` which route (§5) worked, and update this section from "untested" to a result.

---

## 7. Rules and traps found while adding the Syringe (2026-09-20, MRM-87)

**Decisions (Carlos):**
- The Syringe is on **V** (was H). It works **at any health**, and is **unlimited for now**. Any item can be used
  even if it is wasted.
- **Hands locked while it runs.** No switching, throwing or firing until the heal completes; then the previous
  weapon comes back.

**Where each rule lives (all in `Code/Vendor/PolymindGames/Runtime/Wieldables/`, tracked in git):**

| Rule | File | Change |
|---|---|---|
| Any-time use | `Components/WieldableHealingHandler.cs` `TryHeal` | Removed the `IsFullHealth()` gate |
| Fire can't cancel a heal | `Implementations/Tools/HealingWieldable.cs` | `Use(...)` now returns `IsHealing` instead of cancelling; `OnDisable` still stops the routine (death, disable) |
| No switching mid-heal | `Components/WieldableInventory.cs` `SelectAtIndex` | Early return while the active wieldable is a healing one. Gated **before any state change**: gating lower down would already have removed the previous weapon from the equip stack |
| No throw mid-heal | `Components/WieldablesController.cs` `TryEquipWieldable` | Rejects any equip while a heal is running. Throwables call this directly, bypassing the inventory gate |
| Infinite | `Definitions/Item/HQFPS_Syringe.asset` `_stackSize` 1 → 3; `Syringe` added to `MoonlightInfiniteThrowables.throwables` on `Player_Tracey.prefab` (and its default list) | See traps |
| Key | `Assets/InputSystem_Actions.inputactions` (+ wrapper) | Heal path h → v; the keyboard binding on `EquipMelee` removed |

**Traps:**
- **StackSize 1 makes "infinite" impossible.** A use empties the slot for good and there is no stack left to top up.
  The infinite mechanism (`MoonlightInfiniteThrowables`) works by listening to `SlotChanged` and refilling the same
  frame; it needs a stack of at least 2 to have something to adjust. The HUD will read 3 syringes, not the 5 the
  loadout asks for.
- **The old `EquipMelee` V binding belonged to `InventoryUIController`** (V closed the inventory). That UI is not in any
  prefab or scene yet; when it is built it needs a **new keyboard Close key**. Gamepad East still closes it.
- **Vendor animations are not synced to gameplay timers.** The heal lands after 2 s, the equip wait is 0.5 s, the
  clip is whatever its length is. Fine for the stand-in; a real item should set the numbers from its clips.
- **Editing a vendor script in `Vendor/` is a real change**, not a config tweak. These five edits are the only vendor
  changes for this feature; keep them listed here so the big cleanup can find them (MRM-81 rule: one-line hooks are
  recorded).
- **Prefab data edits are text edits.** `Player_Tracey.prefab` was edited as YAML (one list entry). If Unity was in
  Play Mode, exit and re-enter to be sure it reloaded.

---

## 8. Open items for later

- Real item properties and effects (MRM-41): drug effects, defence buff, effect stacking and duration. Carlos:
  "later, when we start implementing the mechanics as they should be."
- Syringe animation vs heal timer sync; a proper Equip/Use split instead of Use-as-Equip.
- MRM-26 pickups replace the testing loadout and `MoonlightInfiniteThrowables`. The Syringe then needs a real
  StackSize decision.
- The map and compass wieldable (§4.1) and its hold-up-a-flat-object animation.
- First animation authored on the real Tracey arms: validate offsets, then update §6 from "untested".
- A first QuickMagic clip: run the Rig B acceptance test, then the Rig A bake test (§6.5).
- Turn this doc into a `/prop`-style wizard once two or three items have gone through it.

## 9. Files

**Changed for MRM-87:** `WieldableHealingHandler.cs`, `HealingWieldable.cs`, `WieldableInventory.cs`,
`WieldablesController.cs`, `MoonlightInfiniteThrowables.cs`, `PolymindPlayerBuild.cs` (comment only),
`Assets/InputSystem_Actions.inputactions`, `Code/Runtime/Input/InputSystem_Actions.cs` (generated),
`HQFPS_Syringe.asset`, `Player_Tracey.prefab`, `Docs/controls.md`, `Docs/mrm25-weapon-test-arsenal.md`, this doc.
