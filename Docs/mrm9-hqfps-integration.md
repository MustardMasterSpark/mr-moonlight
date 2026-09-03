# MRM-9 — HQ FPS Weapons 2.0 as the new character controller

**Branch:** `mrm-9-hqfps` (off `main`, 2026-09-03)
**Supersedes:** `Docs/mrm9-burntwax-integration.md` — Burntwax FPS Engine is being removed.

Carlos's call, 2026-09-03, after testing the raw HQ FPS Weapons 2.0 asset standalone in the Weapons
project: *"this is the new baseline even for the character controller, so it implies that we need to
take out the burnt wax controller."*

His governing constraint, stated twice during the session:

> "We're just gonna have one character controller and I would rather import this one and build the
> functions on top of that one. We don't want functionality split in several places related to the
> character controller."

and

> "Just bring in the necessary stuff. We are trying to avoid bloating the project."

Both shaped every decision below.

---

## 1. Decisions taken, and who took them

| Question | Answer | Who |
|---|---|---|
| Does the HQ FPS controller work on our terrain? | **Yes.** `CharacterControllerMotor` drives a stock Unity `CharacterController` — same basis as Burntwax, no NavMesh involvement | Claude, verified by reading the motor |
| Does it have crouch? | **Yes** — `CharacterCrouchState`, plus a slide state. Nothing had to be written | Claude |
| Can the Speed stat survive? | **Yes.** `IMovementControllerCC.SpeedModifier` is a list of multiplier delegates, so the stat multiplies the asset's tuned speeds instead of replacing them | Claude |
| Inventory baseline | **Keep MRM-41's `Inventory`**, adapt FPSCore to it | Carlos |
| Health owner | **PolymindGames `HealthManager`**; `PlayerStats.Health` becomes a mirror | Carlos |
| Weapon / hand material finish | **RetroLit**, same as every other prop | Carlos |
| Shotgun scatter | Left exactly as the asset ships it | Carlos |

### Shotgun scatter — the answer to Carlos's question

`FirearmHitscanFireSystem` on `HQFPS_Wieldable_DBShotgun`:

- `_rayCount: 8` — eight pellets, **not** a single raycast
- `_minSpread: 6°` → `_maxSpread: 8°`, widening as accuracy drops
- `_maxDistance: 100 m`

Each pellet is its own `Physics.Raycast`. **Carlos's no-double-damage rule is satisfied by
construction**: a raycast returns only the first collider it meets, so one ray can only ever reach
one hitbox. Eight pellets landing on eight hitboxes is the spread working as intended, not stacked
damage.

---

## 2. What came across, and what did not

Everything vendor-side lives in one of three places:

| Destination | Contents | Tracked in git? |
|---|---|---|
| `Assets/_Project/Code/Vendor/PolymindGames/` | `Runtime/`, `Editor/`, `Shaders/`, `OdinSerializer/`, `EditorToolbox/` — 8.3 MB of code | **Yes** |
| `Assets/_Project/Data/PolymindGames/` | ScriptableObject definitions, motion profiles, audio data — 2.9 MB | **Yes** |
| `Assets/ThirdParty/PolymindGames/` | meshes, textures, audio, VFX, prefabs | No (per the ThirdParty policy) |

### The framework came wholesale — this was not optional

The previous ruling in `Docs/dual-project-workflow.md` was *"MRM-23's migration list takes FBXs and
textures only, never `FPSCore/`."* **That ruling is reversed by this issue**, because Carlos asked
for the controller and the weapon system, not the art.

FPSCore is not cherry-pickable. Every gameplay class derives from `CharacterBehaviour` and resolves
its siblings through `ICharacter`, which in turn requires `HealthManager`, `CharacterAudioPlayer`,
the inventory, the motion mixer and the save system. Taking the movement controller means taking
that graph.

The cost is smaller than it looks: **code is 8.3 MB of source in its own assembly**, and unused
types get stripped from the build. The bloat risk was always the art, which is why the art was cut
hard (§3).

### Deliberately left behind

- `Integrations/` (~500 MB of BIRP/HDRP/URP `.unitypackage` installers) — we are URP-only, and the
  materials had already been converted in the Weapons project
- `HQFPS/Demo/` — the firing-range demo scene and its props
- The other **fourteen** weapons' meshes, textures, audio and prefabs
- `HQFPS/Prefabs/Destructables/` — crates and explosive barrels, not in scope
- `HQFPS/Audio/Ambient/` (49 MB)

---

## 3. Texture pass — 810 MB staged down to 115 MB

Source art is 4K with a six-map PBR set. RetroLit samples only BaseColor + Normal.

Run through `Tools/pipeline/texture_pass.py run <folder> --size 512 --map-size 256`, matching the
existing `T_Shotgun_BaseColor.png` (512) / `T_Shotgun_Normal.png` (256) precedent exactly.

| Stage | Size |
|---|---|
| Everything the Weapons project holds | 3.6 GB |
| First cut (only what we ship) | 810 MB |
| After the 512 + pixelation pass | 182 MB |
| After dropping standalone AO / Mask / Metallic / Emission maps | 115 MB |

Processed **in place** — same filename, same `.meta`, same GUID — so every wieldable prefab and
material kept working with zero relinking.

Per-texture: ~22 MB → ~170 KB.

**AO is multiplied into BaseColor** (the only way RetroLit can show it), then the standalone `_AO`
map is deleted. `_MaskMap`, `_MET` and `_EMM` are deleted outright — RetroLit has no slot for them.
**Normal maps are resampled but never quantised or dithered**, per Carlos's standing rule.

### The wobble Carlos asked us not to repeat

All six world/pickup materials are RetroLit with **`_SnapMode = 3` (Off)** and the matching
`_SNAPMODE_OFF` keyword.

This is the documented fix from `Docs/3d-prop-pipeline-wizard.md` §3.2: `_SnapsPerUnit` is a
world-space constant (64 snap points per metre), so a ~0.3 m weapon gets only ~19 snap positions
across its whole length, which reads as broken geometry rather than retro charm. Confirmed on
`M_Shotgun` / `M_FlareGun` on 2026-09-01. Texture pixelation and dithering are unchanged — this is
only about vertex snap.

### ⚠ Open: the first-person materials are still on the vendor shader

The `FP_*` materials (in-hand weapons **and** the hands) use `Shader Graphs/LitFieldOfView`, not
RetroLit. That shader does the **viewmodel FOV warp in the vertex stage**, fed by globals that
`CameraFOVHandler` sets via `Shader.SetGlobalFloat`. RetroLit has no equivalent, so a straight swap
would honour Carlos's "RetroLit everywhere" answer but lose first-person perspective correction on
the most-looked-at objects in the game.

**Resolution: port the FOV warp into a `RetroLitViewModel.shader` variant** — RetroLit's vertex
function is a clean ~20-line block, so this is contained. Not done yet. Their textures are already
512 and Point-filtered, so they read pixelated in the meantime; what differs is the lighting model.

---

## 4. Layers — the one genuinely hard blocker

FPSCore **hardcodes layer indices** in `LayerConstants` (`UnityConstants.cs`), and eight of them
collided with Mr. Moonlight layers that predate the framework and are load-bearing for Blaze AI, the
NavMesh and MRM-34's enemy hitboxes.

Ours were **not** renumbered. FPSCore's eight moved into free slots instead:

| Vendor layer | Was | Collided with | Now |
|---|---|---|---|
| Debris | 6 | **Enemy** | 18 |
| Effect | 7 | **Destructible** | 19 |
| TriggerZone | 8 | **Ground** | 20 |
| Interactable | 9 | **DroppedProp** | 21 |
| ViewModel | 10 | **Health** | 22 |
| PostProcessing | 11 | **Consumable** | 23 |
| Hitbox | 12 | **Weapon** | 24 |
| Character | 13 | **NPC** | 25 |

`Water` (4) and `UI` (5) already matched. `StaticObject`/`DynamicObject`/`Building`/
`InteractableNoCollision` (14–17) were free and kept their vendor numbers. **26 was later taken
by `Ragdoll`** (see §9b); 27–31 remain free.

`LayerConstants` also gained a `MoonlightDamageableMask` (Health | Destructible — see §9b for why
Enemy is deliberately excluded) folded into
`DamageableMask`, so HQ FPS weapon rays can actually see our enemies, and `MoonlightGround` folded
into `SimpleSolidObjectsMask`.

Assets imported from the Weapons project still carried the old numbers, so
`Tools > MrMoonlight > Migration > Remap Polymind Layers (MRM-9)` rewrites them —
**667 GameObject layers and 16 LayerMask fields** across the vendor tree. Verified: **0 objects left
on indices 6–13.**

> **Trap, cost ~20 minutes.** `AssetDatabase.LoadMainAssetAtPath` + `SetDirty` + `SaveAssets` does
> **not** persist edits to a prefab asset's own hierarchy — only to nested prefab instances, which
> save through their own asset. The first run silently remapped every wieldable while leaving
> `FPS_Player`'s own `Systems`/`Body`/`Head`/`Hitbox` on the vendor numbers. Prefab-asset edits must
> go through `PrefabUtility.LoadPrefabContents` → `SaveAsPrefabAsset` → `UnloadPrefabContents`.
>
> Separately, `SaveAsPrefabAsset` **returns success while writing nothing** if Unity holds a
> filesystem lock on the folder (the same lock makes `rm -rf` fail with *"Device or resource
> busy"*). Delete through `AssetDatabase.DeleteAsset` instead.

---

## 5. Project settings changed

| Setting | Was | Now | Why |
|---|---|---|---|
| Scripting define (Standalone) | — | `+POLYMIND_GAMES_FPS_URP` | The vendor's own BIRP→URP converter never sets it (known vendor bug). With it set, `com.unity.postprocessing` is **not needed** |
| API Compatibility (Standalone) | .NET Standard 2.0 | **.NET Framework** | OdinSerializer hard-`#error`s on .NET Standard 2.0 |
| Editor assemblies compatibility | 1 | 2 | Same reason |
| `com.unity.editorcoroutines` | absent | **1.0.0** | Used by two FPSCore editor utilities. Editor-only, no build cost |

Two asmdef references were dropped from the vendor's own asmdefs: `Unity.Postprocessing.Runtime`
(unused under the URP define) and one GUID unresolved in both projects.

`MrMoonlight.Runtime` and `MrMoonlight.Editor` now reference `PolymindGames`.

---

## 6. Input — one asset, no new code

**The best outcome of the whole migration.** FPSCore's input behaviours (`FPSMovementInput`,
`FPSLookInput`, `FPSWieldablesInput`, `FPSInteractionInput`) expose plain `InputActionReference`
fields, so they were simply **repointed at MRM-8's action asset**. No adapter layer, no
`IMovementInputProvider` implementation, one set of bindings.

Our `Gameplay` map already had every action needed, and **`SwitchWeapon` was already bound to Q and
the gamepad right shoulder** — exactly what Carlos asked for, with nothing to rebind.

| Vendor action | Ours |
|---|---|
| Move, Look, Jump, Crouch, Reload, Interact | same name |
| Run | `Sprint` |
| Use | `Fire` |
| Aim | `AimDownSights` |
| Cycle | `SwitchWeapon` |
| Escape | `Pause` |

The vendor asset is also **keyboard-only**; ours has both a Keyboard&Mouse and a Gamepad scheme,
which is by itself reason enough for ours to win.

### The `Unbound` sink

Seven vendor actions have no Mr. Moonlight equivalent (Drop, Select, Holster, Heal, Throw, FireMode,
Scroll). They could not simply be nulled — the vendor's `InputExtensions.EnsureActionIsNotNull` only
*logs* and then dereferences anyway, so null means a `NullReferenceException` on enable. Leaving them
pointed at the vendor asset was worse: its `Drop`/`Holster`/`Select` bindings would fight our weapon
cycling.

So a real action named **`Gameplay/Unbound` with no bindings at all** was added to our asset, and
they all route to it. It can never fire, and the vendor asset ends up **completely unreferenced** —
verified: 16 input references, all ours, zero vendor.

`FPSBodyLeanInput`, `BodyLeanHandler` and `FPSArmsChangeInput` were deleted outright rather than
pointed at a dead action.

---

## 7. Code — the single seam

`MoonlightPlayerRig` (`Assets/_Project/Code/Runtime/Player/`) replaces **all four** Burntwax bridges
rather than porting them one-for-one. It is the only place our code touches the PolymindGames
character.

It owns:

- **Stamina tunables.** Pushes `JumpStaminaCostNormalised` and `SprintStaminaDrainPerSecond` into the
  vendor's per-state table at startup (§8)
- **Speed stat.** Registers as a `SpeedModifier` delegate — a *multiplier*, so the asset's tuned
  speeds stay authoritative and boots/drugs/weapon modifiers still scale them
- **The stat mirror.** Copies `HealthManager.Health` and `StaminaManager.Stamina` into `PlayerStats`
  each frame, converting the vendor's 0–1 stamina to our 0–100 scale so every MRM-41 consumable
  tunable stays valid. Push-back helpers (`RestoreStamina`, `RestoreHealth`) exist because writing to
  `PlayerStats` alone would be overwritten by the next mirror tick
- **Defence.** `ApplyIncomingDamage` divides by the Defense stat in exactly one place
- **The control surface** the rest of the codebase already used: `Input`, `CameraPivot`,
  `SetMovementLocked`, `DisableControl`, `ResetCameraPitch`, `OnJumped`, `OnSprinting`

`ResetCameraPitch` is worth a note: the vendor look handler has no public view-angle setter, only an
additive-input hook. So it installs a delegate returning the negative of the current pitch — which
drives pitch to zero and then, being zero, holds it. Safe because it only runs after
`DisableControl`.

### Other code changes

- **`EnemyHitbox` now implements `PolymindGames.IDamageHandler` directly.** The vendor finds damage
  targets with `collider.TryGetComponent(out IDamageHandler)`, so the handler must live on the
  collider's own GameObject. Making the existing component answer to both damage systems avoids
  bolting a second bridge onto fifteen hitboxes across every Spotter, and funnels both through the
  same zone-multiplier path
- **`PlayerStats`** no longer drains or regenerates stamina and no longer requires a Burntwax bridge
- **`StaminaManager`** gained one public method, `TrySetStateCosts` — it can retune an existing
  state but cannot add states or change the shape Carlos signed off on
- **`StartupWindow`**'s `[InitializeOnLoadMethod]` auto-open was commented out (it opened blank on
  every domain reload, since its banner art was never imported)

---

## 8. Stamina — the two knobs Carlos asked for

Carlos: *"I like how it behaves right now, like the ratio at which it recovers... track its value so
we can put them on two [tunables] if we want to edit the amount of stamina that we spend when we
jump or when we sprint."*

The vendor's whole curve is kept as shipped. Two values are exposed:

| Tunable | Default | Source |
|---|---|---|
| `JumpStaminaCostNormalised` | `0.05` | Jump state `EnterChange`, read off the vendor prefab |
| `SprintStaminaDrainPerSecond` | `0.035` | Run state `ChangeRatePerSec`, likewise |

Defaults are **lifted from the asset, not invented**, so out of the box it behaves exactly as the
version Carlos played.

Untouched (still vendor values): idle regen +0.2/s, crouch regen +0.25/s, 1.35 s regen pause, slide
cost. `StaminaDrainCurve`, `StaminaRegenRate`, `StaminaRegenDelayAfterSprint` and `JumpStaminaCost`
are marked **SUPERSEDED** in `MoonlightTunables` rather than deleted, so existing references resolve.

---

## 9. `Player_Tracey.prefab`

Built reproducibly by `Tools > MrMoonlight > Migration > Build Player_Tracey from FPS_Player (MRM-9)`
— it rebuilds from the vendor source each run, so nothing is hand-wired and lost.

What it does: strips 14 of 19 wieldables, deletes the crossbow's 5x scope (Carlos: *"the crossbow
without scope"*), removes 3 out-of-scope components, repoints 16 input references, swaps 3 firearms
to infinite reserve ammo, tags Player, and adds our systems.

**Verified final state — 0 missing script references:**

```
Player_Tracey            tag=Player  layer=Character
  CharacterController, FPSPlayer, CharacterControllerMotor,
  CharacterAudioPlayer, SaveableObject,
  MoonlightPlayerRig, MoonlightWeaponCycler
  └ MrMoonlight Systems : PlayerStats, Inventory
  wieldables: Arms, BaseballBat, Crossbow, DBShotgun, M1911
```

### Weapon cycling

HQ FPS ships **direct slot selection only** (number keys 1–5); there is no next-weapon behaviour to
copy. `MoonlightWeaponCycler` adds it, driving the vendor's own `SelectAtIndex` so equip/unequip
animations and the arms handler still run as intended.

*Known limit:* it walks every slot including empty ones. Invisible with the four-slot demo loadout,
but needs revisiting for MRM-26 — `IWieldableInventoryCC` exposes no slot-occupancy query.

### Infinite ammo, finite chamber

Carlos: *"infinite ammo, not infinite chamber. We would still be in need of reloading."*

This is exactly the split the asset already draws. The **magazine** components
(`FirearmBasicReloadableMagazine`, `FirearmAdvancedReloadableMagazine`) are untouched, so reloads
still happen normally. Only the **reserve provider** was swapped from
`FirearmInventoryAmmoProvider` → `FirearmInfiniteAmmoProvider`. Testing convenience; reverting is a
one-line change once MRM-26's pickups are live.

### The animations, confirmed imported

All 40 clips imported clean on Generic rigs at scale 1 — including **`Unarmed_Run_`**, the
no-weapon running animation Carlos specifically asked for.

`Unarmed_LeftPunch_` / `Unarmed_RightPunch_` exist in the shared arms FBX but are **never wired** —
Carlos ruled the fist melee out.

---

## 9b. Scene swap and play-mode verification (2026-09-03)

Carlos gave the go-ahead to do the swap over MCP. `Island.unity` now runs on `Player_Tracey`.

**The swap itself was small** — a scan for inbound references into the old Player hierarchy found
exactly **one**: `Event Director.player`, pointing at the old `Body` child (the tag-carrying
collider, per `Docs/mrm34-spotter-ai-build.md`). It now points at the `Player_Tracey` root.

Method: instantiate the prefab at the old transform, carry the Mr. Moonlight systems across with
`ComponentUtility.CopyComponent`/`PasteComponentAsNew` (which preserves asset references, so the
death-scream pool, black-screen image and game-over panel survived untouched), re-point the four
intra-player references, then deactivate the old player rather than delete it. It is still in the
scene as **`Player (OLD Burntwax - delete after verification)`**, inactive — the reversible backup
until Carlos has played it.

Verified: **1 active AudioListener, 1 enabled Camera, 0 missing script references.**

### Three real defects the play test found

| # | Symptom | Cause | Fix |
|---|---|---|---|
| 1 | Duplicate `MoonlightPlayerRig` | `[RequireComponent(typeof(MoonlightPlayerRig))]` on DeathSequence / InteractionDetector / InventoryUIController forced a second rig onto whatever object they sat on | Attribute removed from all three; they now resolve with `GetComponentInParent`. The rig lives on the player root, once |
| 2 | **Screen permanently red-tinted**, health 28.4/100 | Not a health-system mismatch. The spawn point sat **12.2 m above the terrain** (y=32.9 vs ground y=20.6). Burntwax used a ride-height spring and hovered; the HQ FPS `CharacterController` falls, and `FallDamageHandler` charged 71.6 damage on landing | Spawn moved to y=20.714 (ground +0.10). Health now 100/100 |
| 3 | `ArgumentException: An item with the same key has already been added` | `AssetDatabase.CopyAsset` duplicated `SaveableObject.PrefabGuid`, so `Player_Tracey` and `FPS_Player` shared one save-system key | PrefabGuid reset to the prefab's own asset GUID. `PolymindPlayerBuild.FixSaveableGuid` now does this on every rebuild |

Defect 2 is worth remembering: **the red tint was the health chain working correctly**, all the way
from PolymindGames' `HealthManager` through `PlayerStats.Health` to `HealthRedTintSource`. It was
reporting a real injury, not a bad reading.

### Verified working in play mode

- Health **100/100**, stamina **1.0**, `PlayerStats` mirror matching both
- `SpeedModifier.EvaluateValue()` = **exactly 1.0000** — the Speed stat is neutral when unmodified,
  which is what proves it multiplies rather than overrides
- Holster filled: **[0] Double Barrel Shotgun, [1] M1911, [2] Crossbow, [3] Baseball Bat**
- Weapon cycling wraps: 0 → 1 → 2 → 3 → **0**
- Viewmodel FOV warp live: globals `_FOVEnabled = 1`, `_FOV = 48`, viewmodel scaled 0.5, weapon
  0.26 m from the camera
- 85–140 FPS on the island

Screenshots: `Assets/Screenshots/mrm9_player_tracey_settled.png`.

> **Read the frame before believing it.** An intermediate screenshot showed the weapon filling the
> whole screen, which looked exactly like a broken viewmodel FOV. It was the equip animation
> mid-sweep, triggered by a rapid five-step cycle test one frame earlier. Querying the actual shader
> globals disproved it. A single frame of an animated first-person weapon is not evidence.

### Combat fixes from Carlos's first hands-on test

He reported three things: enemies took no damage but left impact decals where they stood, enemies no
longer noticed him, and gamepad look barely moved. All four causes were layer/hierarchy fallout from
the swap, and all are fixed.

**1. Enemies took no damage — two invisible shields.**

The weapon ray never reached a hitbox. Two things stood in front of them, and neither can take damage:

| Blocker | Layer | Why it stopped the shot |
|---|---|---|
| The enemy's root **Blaze AI movement capsule** | Enemy (6) | Encloses all fifteen hitboxes, has no `IDamageHandler`. `TryGetComponent` returned false, so the shot spawned a decal and dealt nothing |
| **17 ragdoll bone colliders** (`CC_Base_*`) | Default (0) | Kinematic, enabled while alive, and Default is inside `SimpleSolidObjectsMask` |

Fixes, in the order they have to happen:

- `MoonlightDamageableMask` **drops Enemy(6)** and keeps Health + Destructible. Rays now pass through
  the Blaze capsule, which stays on Enemy because the AI needs it there.
- The fifteen `Hitbox_*` colliders moved from Enemy to **Health(10)** — the layer that exists for
  exactly this. This is what preserves MRM-34's per-limb multipliers: had the root capsule simply
  been given a hitbox instead, it would have absorbed every shot and flattened head/torso/limb
  damage into one number.
- The seventeen ragdoll bones moved to a new **`Ragdoll` layer (26)**, outside the weapon mask.
  Ragdoll-vs-terrain collision is unaffected, so death physics still works.

Verified: shot lands on `Hitbox_Stomach`, handler found, **100 → 60 hp** (the 2× torso multiplier),
`LastAttacker = Player_Tracey`.

**2. Enemies stopped noticing the player.** Blaze's `vision.layersToDetect` and
`vision.hostileAndAlertLayers` both look for **Player(3)**; the vendor prefab put the player root on
**Character(25)**. Root and Deathbox moved to Player. (`LayerConstants.CharacterMask` is referenced
only by `ShakeZone`, a vendor motion effect we don't use, so nothing needed it there.)

**3. Enemies could not have damaged the player either — found while fixing 2.** `EnemyFirearm`
resolves its target with `hit.collider.GetComponentInParent<IDamageable>()`. The colliders are on the
player root, but the scene-swap had parked `PlayerDamageReceiver` on the *child* systems object, so
the walk up the hierarchy never found it. Moved to the root. A reference audit at the same time
caught four `stats` fields left null by the swap (`HealthRedTintSource`, `PlayerStatsDebugOverlay`,
`InfiniteStaminaDebugToggle`, `HealthRegenDebugToggle`) — all silent failures, all repointed.

**4. Gamepad look barely moved.** `FPSLookInput` passed every device straight to
`CharacterLookHandler`, which multiplies by the *mouse* sensitivity option. That is right for a mouse
— whose Look value is a per-frame delta of tens of units — but a stick is a rate capped at 1.0, so
the right stick crawled. Stick input is now converted to a per-frame delta with an acceleration ramp;
the mouse path is untouched, so its feel and its sensitivity option are unchanged. Tuned by
`GamepadLookSpeed` / `GamepadLookAcceleration`, pushed in by `MoonlightPlayerRig` (the vendor
assembly can't reference ours — same constraint as `StaminaManager.TrySetStateCosts`).

**Impact decals switched off** at Carlos's request, via a `_decalsEnabled` flag on `SurfaceManager`
rather than by clearing every `SurfaceDefinition.DecalEffect`, so it is one checkbox to restore and
no asset data was lost.

### Aim is centred — the shotgun is just wide (measured, not changed)

Carlos reported shots not landing at screen centre, including in iron sights. Measured: **there is no
misalignment.** `BodyPoint.Head` *is* the PlayerCamera — same position, 0.000° apart — so rays and
projectiles both originate exactly at screen centre, and `PhysicsUtility.GenerateRay` scatters
symmetrically with no bias.

What he was feeling is the double-barrel shotgun, which is equipped in slot 0 by default. At the live
accuracy of 0.99:

| Range | Shotgun (6.0°) | Pistol (0.35°) | Crossbow (0.33°) |
|---|---|---|---|
| 10 m | **±1.05 m** | ±0.06 m | ±0.06 m |
| 25 m | **±2.64 m** | ±0.15 m | ±0.14 m |

A Spotter's torso is ~0.5 m wide, so past about 5 m most of the eight pellets miss. Its *minimum*
spread is 6°, which is why aiming down sights cannot tighten it either.

Projectile gravity (9.8) costs the pistol 0.03 m of drop at 15 m and the crossbow 0.10 m — both
negligible at demo ranges, and deliberate ballistics rather than a defect.

**Carlos's call, 2026-09-03: keep the vendor's 6°–8°.** Recorded here so it is not "fixed" later by
someone reading it as a bug.

### Second hands-on pass — four more, all root-caused in the swap

**1. Enemies shot the player and nothing happened.** This one was mine.
`PlayerDamageReceiver.TakeDamage` still did `stats.Health.Deplete(...)`, which was right under
Burntwax when `PlayerStats` owned health. It no longer does: `MoonlightPlayerRig` mirrors
`HealthManager` into `PlayerStats` **every frame**, so the deplete was overwritten by the very next
tick and the player was effectively immortal. It now calls `rig.ApplyIncomingDamage`, which lands on
the HealthManager and applies Defense in its one owning place; the mirror then carries it back.
Verified: 100 → 75 on the manager, and 75 on the stat several frames later instead of snapping back.

*General lesson for anything that used to write to `PlayerStats`:* a mirrored value cannot be
written to directly. `RestoreStamina` / `RestoreHealth` on the rig exist for exactly this, and
MRM-41's consumables will need the same treatment.

**2. Gamepad trigger only fired once, then went dead for a while.** `FPSWieldablesInput` decided the
fire button had been released with `ReadValue<float>() > 0.001f`. Correct for a mouse, which snaps
1 → 0 in one frame. A gamepad analog trigger *decays* over several frames, so on the release frame
the value was still above 0.001, the if/else chain took the **Hold** branch, and
`WasReleasedThisFrame()` — true for exactly one frame — was missed. The `End` phase never fired, so
`FirearmTriggerBehaviour.IsTriggerHeld` stayed true and the weapon refused to shoot again.
Now uses `action.IsPressed()`, which honours the button press point, plus an explicit held flag so
`End` is emitted exactly once. **The aim action had the identical bug** — the left trigger would
stick in ADS — and got the same fix.

**3. Mouse escaped the Game view in play mode.** Nothing ever locked the cursor: PolymindGames does
that from `GameMode`, which lives on the vendor's `FPS_GameMode` prefab — a game-flow object we
deliberately did not migrate. Clicks were landing on the Inspector and Hierarchy. `MoonlightPlayerRig`
now owns cursor state alongside the rest of the control surface: locked during gameplay, released by
`SetMovementLocked` (UI) and `DisableControl` (death), and re-asserted on `OnApplicationFocus` so
alt-tabbing back does not leave it free.

**4. CRT effect (and the HAZE fog) stopped rendering.** The vendor camera ships
`volumeLayerMask = 1 << 23` — its own PostProcessing layer only. Every Mr. Moonlight volume sits on
**Default(0)**, so none of them reached the camera. That silently disabled both the HAZE fog and the
`CRTSettings` volume component that Retro Shaders Pro's CRT renderer feature reads its settings from —
which is why toggling CRT appeared to do nothing. Mask widened to Everything (what the old Burntwax
camera used), on both the prefab and the scene instance.

> Worth noting for future vendor-prefab work: **three of these four were vendor defaults that are
> perfectly reasonable in the vendor's own demo scene and wrong in ours** — a PostProcessing-only
> volume mask, a mouse-shaped input test, and cursor handling parked on a game-flow prefab we chose
> not to take. Migrating a prefab means auditing its assumptions, not just its references.

### Performance audit, 2026-09-03

Carlos noticed a small FPS drop and asked for a pass. Measured live with the profiler at ~105 FPS.
Baseline: CPU frame **9.51 ms**, main thread **8.68 ms**, GPU **6.86 ms** — i.e. **CPU main-thread
bound**, not GPU bound.

**Nothing added by MRM-9 allocates per frame.** `MoonlightPlayerRig.Update` does two float writes
and an event invoke; `MoonlightWeaponCycler.Update` polls one action. Every subscription
(health events, the movement state listener, the speed modifier, the `InputMapController`) is
released in `OnDestroy`. No leak found on our side.

**The two real costs are both pre-existing, and both scale with MRM-34's doubled Spotter count.**

| Finding | Measurement |
|---|---|
| **Ragdoll physics.** 110 live Spotters × 16 kinematic ragdoll rigidbodies = **2,762 active kinematic bodies**, each parented to an animated bone, so PhysX re-syncs all of them every frame | `Physics.SyncColliderTransformBatchJob` = **5.75 ms** of an 8.68 ms main thread |
| **GC churn.** 172 KB allocated across **968 allocations every frame**. Disabling the Spotters drops that to 19 KB / 343 allocations — so **~89% of all garbage is the Spotters**, ~1.5 KB per Spotter per frame, from Blaze AI's own per-frame code | 172 KB/frame → 19 KB/frame |

Project physics settings are already correct and offer nothing free: `autoSyncTransforms` off,
`reuseCollisionCallbacks` on, 50 Hz fixed step, solver 6/1.

#### Applied

**Temporal AA off.** The vendor camera shipped with TAA (High); the Burntwax camera it replaced used
`None`. Beyond costing ~1 ms, TAA blends across frames, which **smears the quantised pixel dither
the whole RetroLit pipeline exists to produce** — it was actively fighting the art direction. Set to
`None` on the prefab and the scene instance. 14.03 → 13.08 ms in an A/B.

#### Recommended, deliberately not applied

**Gate the ragdolls.** Disabling ragdoll colliders on *living* enemies and re-enabling them on death
measured: 2,762 → **758** active kinematic bodies, `SyncColliderTransformBatchJob` 5.75 → **1.71 ms**,
CPU frame 9.51 → **8.55 ms** (≈105 → ≈117 FPS).

Not shipped because Blaze drives **knockdown** through the same ragdoll (`HitStateBehaviour` — see
memory `mrm75_blaze_equip_and_hitstate_facts`), so the re-enable path has to cover non-fatal
knockdown as well as death, and verifying that needs deliberate testing of both. It is the single
biggest available win and belongs to MRM-34's owner, not to a controller-swap branch.

**The GC churn is the bigger long-term problem** — 168 KB/frame will cause collection stutter
regardless of average frame rate, and it is ~89% Blaze. The lever with the best ratio is simply
**fewer simultaneously-active Spotters**, or distance-based deactivation; 110 live agents on a
1024 m island is a lot for an encounter that targets 20 kills.

> Editor caveat: allocation and frame numbers are inflated in the editor. Treat these as
> *relative* comparisons — the A/B ratios hold, the absolute values will be better in a build.
> Per `verification_requires_a_build`, confirm on the exe before drawing conclusions.

### Left cosmetic, for Carlos to call

The arms render the bare-skin **`Arm_Standard`** variant. The FBX also carries
`Arm_Shirt&Gloves`, which suits 1979 Alaska better. Both materials are imported and processed;
switching is a renderer swap on the arms wieldable.

---

## 10. Still to do on this branch

| # | Work | Notes |
|---|---|---|
| 1 | **Swap the Player in `Island.unity`** | Needs Carlos's go-ahead — scene work |
| 2 | **Play-mode verification** | Nothing below is proven until the game actually runs |
| 3 | **Remove Burntwax** | Must come *after* 1–2, or the scene breaks. Delete the 4 `Burntwax*.cs` bridges (already unreferenced), `Code/Vendor/Burntwax FPS Engine/`, `ThirdParty/Burntwax Collective/`, and the old `Player.prefab` |
| 4 | **HUD** | Ammo counter, "no ammo" message, stamina bar — replace the ones on the old `Player.prefab` |
| 5 | **`RetroLitViewModel.shader`** | §3 — the FP materials' outstanding item |
| 6 | **Interaction unification** | Our `InteractionDetector` and FPSCore's `InteractionHandler` both read Interact. Exactly the split Carlos warned against; FPSCore's should win since it drives weapon pickup |
| 7 | **Weapon pickup → MRM-41 inventory** | Carlos chose to keep our `Inventory`; the adapter is not written |
| 8 | **Input mode unification** | `InputMapController` clones the action asset; FPSCore binds the asset itself. They coexist but pause/UI modes don't cover FPSCore's actions. Collapse onto FPSCore's `InputManager` contexts |
| 9 | **Per-weapon damage values** | Carlos will supply numbers. Current: pistol 30, shotgun 20/pellet, crossbow 40, before hitbox multipliers |
| 10 | **Prune unused vendor prefabs/UI** | `HQFPS/Prefabs/UI` still throws an `InputActionLabelUI` `OnValidate` error |

---

## 11. Known noise in the console

Pre-existing, **not** caused by this work:

- 3× `NullReferenceException` in `GoreSimulatorInspector.GetDefaultReferences` (MRM-34)
- "custom elements added to the main toolbar" warning
- shadow-atlas resolution warning (MRM-34's doubled Spotter population)

Caused by this work, benign, goes away with item 10 above:

- `ArgumentNullException` in `InputActionLabelUI.OnValidate` — a vendor UI prefab we do not use,
  validating against an action asset we did not import

---

## 12. 2026-09-03 — Sonnet tuning pass: hip-fire look speed

By this point `mrm-9-hqfps` had already been merged to `main` (PR #26); this work landed directly
on `main`'s working tree, uncommitted pending Carlos's own branch/commit choice in GitHub Desktop.

Carlos: hip-fire camera turn speed felt very slow; raise it 50%, leave ADS speed exactly as it is.

The vendor's `CharacterLookHandler.GetTargetSensitivity` has exactly **one** sensitivity formula
shared by hip and ADS — `InputOptions.MouseSensitivity`, optionally scaled by
`camera.fieldOfView / GraphicsOptions.FieldOfView` if the optional `_camera` field is wired.
Raising that shared value, or the gamepad-only `GamepadLookSpeed` tunable, would have scaled ADS
by the same 50% — the opposite of what was asked.

**Fix:** added `bool IsZoomed` to `IFOVHandlerCC`, implemented on `CameraFOVHandler` as
`!Mathf.Approximately(_cameraFOVTweenMod, 1f)` (the multiplier the aim/charge systems set via
`SetCameraFOV`, distinct from the separate speed/height/airborne FOV kick applied afterward in
`Update()`, so it doesn't false-positive on sprinting or falling). `CharacterLookHandler` now
multiplies sensitivity by a new `HipFireSensitivityBoost = 1.5f` constant only when not zoomed.

**Trap hit while wiring it up:** caching the `IFOVHandlerCC` reference once in
`OnBehaviourStart` via `character.TryGetCC(out _fovHandler)` came back **null**, even though
`CameraFOVHandler` sits on the same GameObject and the character's `_components` dictionary
verifiably contains an `IFOVHandlerCC` entry moments later. Root cause not fully isolated
(candidates: `CharacterLookHandler` starts `enabled = false` in `Awake()` so its own `Start()` is
deferred until `SetLookInput` is called, or FPSCore's activation order for this prefab beats
`Character.Awake()`). The textbook "all Awakes run before any Start" assumption is **not** safe
to rely on for cross-component `TryGetCC` lookups in this framework. Fixed by resolving lazily on
first use instead: `_fovHandler ??= Character.GetCC<IFOVHandlerCC>();` inside
`GetTargetSensitivity`, rather than caching once in `OnBehaviourStart`. Self-heals regardless of
the exact race, costs nothing once resolved. Any future `CharacterBehaviour` that needs another
optional character component should do the same.

**Verification:** Unity was running (`MrMoonlight@87580c9df5a077ae`, port 8080). Entered Play mode
and puppeteered the equipped weapon's `IFirearmAimHandler.StartAiming()`/`StopAiming()` via
`execute_code`, reading `CharacterLookHandler`'s private sensitivity state back by reflection in
the same synchronous call (a separate round-trip lets FPSCore's own input polling silently revert
the manual aim state, since the real mouse button was never actually held):

| State | Sensitivity |
|---|---|
| Hip-fire | 1.5 (base 1.0 × boost) |
| ADS (zoomed) | 1.0 (boost correctly suppressed) |

Compiled clean, no new console errors. **Not yet eyeballed by Carlos hands-on** — "1.5×" is a
numeric target, not a felt one; flag for his own pass before considering this fully done.

Files touched: `IFOVHandlerCC.cs`, `CameraFOVHandler.cs`, `CharacterLookHandler.cs`.
