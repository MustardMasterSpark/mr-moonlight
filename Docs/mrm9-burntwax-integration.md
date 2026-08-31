# MRM-9 — Burntwax FPS Engine controller swap

**Built 2026-08-29 on branch `mrm-9-controller-swap`.** Replaces the hand-written
`PlayerController` with the Burntwax FPS Engine's Rigidbody state-machine controller, and adopts
its weapon, reload, pickup and aim-down-sights systems as the base for several other issues.

Read this before touching the player, the camera, weapons or pause.

---

## 1. The asset, and the three checks that sank the last candidate

**Burntwax FPS Engine**, Burntwax Collective, Asset Store. Carlos owns it; installed via Package
Manager. `Docs/mrm9-player-controller-asset-kickoff.md` required it to be checked against the three
grounds that rejected `FPS Engine` (cowsins) on 2026-08-28.

| Check | Result |
|---|---|
| **Full-body look-down** | **Consciously deferred, not solved.** The controller does not *fight* a body — nothing in it forces an arms-only architecture — but Burntwax ships only a capsule (renderer disabled, which is its default for a first-person rig), and **Carlos's call on 2026-08-29 was to use Burntwax's own body and animations as-is and drop our `Jill` placeholder**: *"I would rather have right now a functional character controller and then add the things on their respective issues once I have a 3D model."* So MRM-9's *"looking straight down shows placeholder body geometry"* criterion is **currently not met** and is owed back once the real Tracey model exists. This is the one check FPS Engine failed, so it is called out plainly rather than buried. |
| **`ProjectSettings` overwrite** | **It is exactly as aggressive as FPS Engine** — the package ships all 19 `ProjectSettings/*.asset` files *plus* `Packages/manifest.json`. It is a Complete Project export, not a systems package. **It did not matter, because the import dialog was never used.** The `.unitypackage` was read straight out of the Asset Store cache, its entries listed, and only the `Assets/` subtree extracted to disk. Those 20 files never touched the project. |
| **Class-name collisions** | **None.** All 76 scripts are inside `namespace Burntwax` in their own `Burntwax.Core` assembly definition; our code is `MrMoonlight.*` in `MrMoonlight.Runtime`. Even `Interactable`, `InputManager`, `PauseMenu` and `PlayerHealth` — the literal collisions that sank FPS Engine — are safe. |

**Reusable technique:** any Complete Project package can be de-fanged this way. The cache lives at
`%APPDATA%/Unity/Asset Store-5.x/<Publisher>/<Category>/<Name>.unitypackage`; it is a `.tar.gz` of
`<guid>/{asset,asset.meta,pathname}` triples. List the `pathname` entries, confirm what is in
there, and extract only what you want. This is the same trick used for AllSky.

---

## 2. What was taken, changed and dropped

### Taken as-is
- **Rigidbody floating-capsule movement** — a spring ride-height raycast, not a `CharacterController`.
  This is the source of the inertia/momentum Carlos liked in the store video.
- **Procedural weapon feel** — `BobAndSway.cs`: sway, bob, velocity sway, falling inertia, landing jolt.
- **Gun state machine** — Rest / Shoot / Reload / IncrementalReload / Aim / Stow / NoShoot.
- **Reload, including shell-by-shell** (`GunIncrementalReloadState`) for the shotgun.
- **Aim-down-sights** as a two-Cinemachine-camera priority blend.
- **Sprint-blocks-fire** via `InputPrioritySorter` — a last-pressed-wins stack over sprint/aim/shoot.
  A soft interrupt, not a hard lock: press fire while sprinting and firing wins.
- **Pickups** (gun, ammo, health) and **per-surface bullet impacts** (`SurfaceEffectSO`).

### Changed
| Change | Why |
|---|---|
| `InputManager` reduced to a passive data holder; `BurntwaxInputBridge` writes it | Burntwax shipped its own action asset **with no control schemes** — keyboard/mouse only. MRM-8 already had Keyboard&Mouse + Gamepad. Two assets would fight over devices and silently drop gamepad support. |
| Cinemachine 2.10.5 → **Cinemachine 3.1.7** | Unity 6.3 ships CM3; CM2 is deprecated. Only two files referenced it. CM3 kept CM2's assembly and script GUIDs, so `CinemachineImpulseSource` on the weapon prefabs still resolves. |
| Look driven by us, not `CinemachineInputAxisController` | CM's input controller would have taken ownership of look and discarded MRM-9's stick acceleration, separate mouse/stick sensitivities, and the pitch clamps. The bridge writes `CinemachinePanTilt` axes directly using the retired controller's exact maths. |
| Speeds read from `MoonlightTunables` | No hardcoded values (CLAUDE.md). Setters added to `PlayerStateMachine` for walk/sprint/crouch. |
| `PlayerHealth` put in reporting-only mode | `PlayerStats` (MRM-12) owns health, participates in the stat/modifier stack, and raises the death event MRM-17 listens to. Two health floats would desync. The component stays because Burntwax pickups use it as their "is this the player?" marker. |
| `[DefaultExecutionOrder]` added to `InputManager` (-100), `InputPrioritySorter` (-90), `BurntwaxInputBridge` (-80) | Awake order across GameObjects is arbitrary. Burntwax's demo scene *happened* to initialise `InputManager.Instance` before `PlayerStateMachine.Awake()` read it; in our prefab it did not, and the state machine threw on frame one — and Unity then disables a component whose `Awake` throws, so the whole controller silently went dead. Now guaranteed rather than incidental. |
| Project gravity −9.81 → **−19.81** | Burntwax's jump, spring strength and fall inertia are tuned against it. `MoonlightTunables.Gravity` was **already −20** for the retired controller, which never used `Physics.gravity` — so this aligns physics with the feel the project already had. |

### Dropped
Wall running, wall jumping, wall charge and the glove mechanic (Carlos: "I don't want that at all");
the save system (`SaveSystem`, `GameManager`, `ObjectManager`, scene loader) — unused, and its hooks
were already commented out in the pickups; Burntwax's `MainMenu` (MRM-18 owns menus); Burntwax's own
input action asset; all four demo scenes.

---

## 3. Architecture — read this before adding to the player

### The assembly boundary runs one way

```
MrMoonlight.Runtime  ──references──>  Burntwax.Core
```

**Never the reverse.** Assembly definitions cannot reference each other circularly. This is why
Burntwax code knows nothing about Mr. Moonlight, and why every piece of glue is a bridge component
on our side. If you find yourself wanting a Burntwax script to call a `MrMoonlight` type, add an
event or a public field to the Burntwax script and have a bridge read it instead.

### The camera rig must move with the root, and must not sit under the yawing transform

Two separate constraints, and **getting one of them wrong is what caused the first bug** (2026-08-29:
the body moved, the camera and gun stayed at the origin).

1. **It must be a descendant of the root**, because the Rigidbody is on the root — that is the
   transform physics actually moves. A rig parented as a *sibling* never travels with the player.
2. **It must not be under `psm.player`**, which is `Body`. `PlayerStateMachine.SetRotation()` yaws
   that transform to match `Camera.main`'s flattened forward; a camera underneath it would re-apply
   its own pan every frame and the player would spin.

Burntwax sidesteps both by leaving the camera rig loose in the scene — which is why their
`Player.prefab` ships with `fpsCam`/`aimCam` **null** and must be hand-wired per scene. That
violates our drag-and-drop prefab rule, so the rig lives inside the prefab, under the root but
outside `Body`.

⚠️ **And a `CinemachineCamera` needs a *position* control component, not just a rotation one.**
Setting `Follow` alone does nothing. `CinemachinePanTilt` is rotation-only; without
`CinemachineHardLockToTarget` the camera never leaves its own transform. This was the actual
first-bug cause and it fails *silently* — no error, no warning, just a camera stuck at the origin.

```
Player                      Rigidbody, PlayerStateMachine, CamStateMachine, PlayerHealth,
│                           PlayerAmmo, PlayerCrosshair, AudioSource   ← Burntwax, unmodified
├── CameraFollow            vcam Follow target
├── Body                    capsule mesh + CapsuleCollider — THE ONLY TRANSFORM THAT YAWS
├── PlayerUI                health / sprint / ammo / crosshair canvas
├── CameraRig               under the root (so it moves), outside Body (so it does not spin)
│   ├── Main Camera         Camera, CinemachineBrain, AudioListener, Interactor
│   │   └── Weapon Parent   BobAndSway
│   │       └── Arms        Animator, RigBuilder, GunStateMachine, weapon models
│   ├── vcam_FPS            CinemachineCamera + CinemachineHardLockToTarget + CinemachinePanTilt
│   └── vcam_Aim            same, lower FOV
└── MrMoonlight Systems     ALL our code lives here, on one child
    │                       InputManager, InputPrioritySorter, PlayerStats,
    │                       BurntwaxInputBridge, BurntwaxPlayerBridge, BurntwaxHealthBridge,
    │                       BurntwaxStartingLoadout, DeathSequence, HealthRedTintSource,
    │                       PlayerStatsDebugOverlay
    └── Death Scream Source AudioSource (MRM-17)
```

### Our code sits on one child, deliberately

Carlos's instruction on 2026-08-29, after the first bug: *"pull everything from Burntwax, and then
add our behaviors and our script as another game object attached to the Burntwax prefab."*

So the prefab is built by instantiating Burntwax's `Player.prefab` **unmodified** and adding two
children: `CameraRig` and `MrMoonlight Systems`. Nothing of ours is mixed into their components.
That keeps their hierarchy re-importable and makes the seam between the asset and our code obvious.

Consequence for anyone adding a bridge: **look components up from `transform.root`**, not from
`GetComponentInChildren`. The engine's components are in a sibling branch, not below us. Every
bridge does this and says so in a comment.

`Burntwax.InputManager` and `InputPrioritySorter` live on `MrMoonlight Systems` too — they were
scene singletons in Burntwax's demo, never on its prefab, so putting them there costs nothing and
satisfies `BurntwaxInputBridge`'s `RequireComponent`.

### The bridges

| Component | Responsibility |
|---|---|
| `BurntwaxInputBridge` | Writes MRM-8 input into Burntwax's `InputManager`; drives `CinemachinePanTilt` with MRM-9's look maths; gates sprint/jump on stamina; mirrors pause into action-map switching. |
| `BurntwaxPlayerBridge` | **The controller's public face.** Re-exposes `OnJumped`, `OnSprinting`, `Input`, `SetMovementLocked`, `DisableControl`, `ResetCameraPitch`, `CameraPivot`. Pushes tunables and upholds the MRM-12 speed contract. |
| `BurntwaxHealthBridge` | Routes Burntwax damage/heal calls into `PlayerStats.Health`, pushes the result back for the health bar. |
| `BurntwaxStartingLoadout` | Equips starting weapons via `GunStateMachine.Pickup()`. |

### The MRM-12 speed contract is unchanged

Whichever mode speed applies is written to `PlayerStats.Speed.BaseValue`, and the engine then moves
at the modifier-stacked `Speed.Value`. A speed item or debuff affects movement without movement code
knowing items exist.

**Do not change this to read `PlayerVelocity` back.** Burntwax states assign `PlayerVelocity` only in
`EnterState`, never per frame — reading it back, applying modifiers and rewriting it would feed the
modified value in as next frame's base and compound every frame. The mode is derived from the current
substate instead.

### `PlayerController` is gone

It was a hard `[RequireComponent]` for five components across MRM-12/16/17/41/42. All five now
require `BurntwaxPlayerBridge`, whose API is deliberately identical. `DebugFlyController` was deleted
outright — it required a `CharacterController`, which cannot coexist with the Rigidbody engine.

---

## 4. The pause contract (project-wide)

Adopted from Burntwax at Carlos's request. **`PauseMenu` is the pause authority.**

```csharp
Time.timeScale = 0f;
AudioListener.pause = true;
```

Every other system must be consistent with that:

- Drive logic from `Time.deltaTime`, **never** `unscaledDeltaTime`.
- Leave Animators on `AnimatorUpdateMode.Normal`.
- Use `WaitForSeconds`, not `WaitForSecondsRealtime`, in coroutines.
- Gate any `Update` that must not run while paused on `Time.timeScale == 0f`.
- The only exception is UI that must stay live while paused — the pause menu itself.

**This binds MRM issues that are not built yet:** enemy AI, the event system, animation and timers
all have to obey it. `GunStateMachine` already gates on `Time.timeScale == 0f` rather than on
`PauseMenu.Instance`, so the engine stays independent of which pause implementation is in the scene.

`BurntwaxInputBridge` mirrors `PauseMenu.Paused` into MRM-8 map switching (Gameplay ↔ UI) so the menu
is gamepad-navigable and gameplay bindings go quiet. **`UI.Cancel` is wired as the resume path**,
because the `Pause` action lives in the Gameplay map that pause disables.

---

## 5. Layers, tags and gravity

Layers were added at **Burntwax's own indices** so the serialized layer ints and `LayerMask`s inside
its prefabs keep their meaning:

| Index | Layer | Note |
|---|---|---|
| 3 | `Player` | Was empty. Also fixes the long-standing issue that the player sat on `Default`. |
| 4, 5 | `Water`, `UI` | Already matched. |
| 6, 7 | `Enemy`, `Destructible` | Were empty. |
| **8** | **`Ground` (ours)** | Burntwax had `Wall` here — the **only** index clash. It was the wall-running layer, which we removed, so it is moot. |
| 10–13 | `Health`, `Consumable`, `Weapon`, `NPC` | Were empty. |

Tags added: `Enemy`, `Ground`, `FxTemporaire`, `Wall`, `Rock`, `Glass` (`Metal`, `Crate`, `Target`
already existed). The per-surface bullet impact system keys off these.

---

## 6. Known gaps and deliberate deferrals

1. **Full-body look-down is not met.** MRM-9's acceptance criterion *"looking straight down shows
   placeholder body geometry, not empty space"* currently fails — the player is Burntwax's capsule
   with its renderer disabled, plus an arms viewmodel. **Carlos's explicit call on 2026-08-29**, to
   get a functional controller now and restore the body once a real Tracey model exists. It is owed
   back, in this issue or MRM-51. Do not treat it as silently dropped.
2. **`AmmoConfig` stores ammo on the ScriptableObject.** `currentAmmo`/`currentClip` are fields on a
   shared asset, so ammo is global and persists across play sessions in the editor. It "works" in a
   demo and will bite us. **Fix before the inventory issue lands** — move ammo state to a runtime
   component. This is the single worst thing in the asset.
3. **Revolver and SMG were not removed**, despite Carlos asking to keep only pistol and shotgun.
   `GunScriptableObject.ID` indexes directly into `GunStateMachine.OrderedGuns`, and the shotgun is
   ID 2 — deleting SMG (ID 1) leaves a hole that `WeaponSwitch` would index into. Removing them
   safely means re-IDing the SOs, editing `OrderedGuns`, the `Arms` prefab and the shared
   `Arms.controller`. Deferred as unnecessary risk before Carlos has playtested; the cost is a few MB
   against a 1 GB ceiling that a 54 MB build is nowhere near.
4. **A duplicate `TextMesh Pro` folder** (4.8 MB) ships inside the asset. Left alone — it caused no
   GUID conflict, and removing it risks breaking the asset's UI font references.
5. **`blackScreenImage` and `gameOverPanel` on `DeathSequence` are still per-scene nulls**, exactly as
   they were on the retired prefab. Not a regression.
6. **Weapon switching is bound to a single `SwitchWeapon` action**, not Burntwax's nine number keys.
   Fine for two weapons; revisit if the roster grows.
7. **`Docs/input-map.md` has not been re-checked** against the new bridge.

---

## 7. Verified in play mode

Console clean (0 errors, 0 warnings) on `VegetationGallery`:

- `PlayerStateMachine` enabled, `PlayerGroundedState` / `PlayerIdleState`, grounded.
- `InputManager.Instance` and `InputPrioritySorter.Instance` resolved.
- `PlayerStats`: Speed base 4 (= `MoonlightTunables.WalkSpeed`), Health 100, Stamina 100.
- Pistol equipped, model active, `camShake` resolved through CM3, hand IK weight 1, ammo 10/128.
- `vcam_FPS` active at FOV 60, `Follow = CameraFollow`, position control `CinemachineHardLockToTarget`.
- **Camera and Arms sit exactly on `CameraFollow`** (distance 0.0000), and moving the root by
  (25, 0, 13) moved both by exactly (25, 0, 13). The camera is a descendant of the root and **not**
  a descendant of `Body`, so it travels with the player and cannot spin.

⚠️ **Play mode cannot verify motion or feel from a tool session.** Unity does not tick while the
editor is unfocused — during these checks `Time.time` sat at 0.020 while `Time.unscaledTime` reached
5.35, with `timeScale` at 1 and the game not paused. Frame count stayed at 2. So anything that needs
frames to advance (movement, inertia, ADS blending, animation) is **structurally** verified here, not
behaviourally. Same lesson as `[[verification_requires_a_build]]`.

**Not verified, needs Carlos hands-on:** movement feel, gamepad look, ADS, firing, reload, pickups,
the death sequence — and a real build.

### Bug history

**Bug 1 (2026-08-29, fixed): body moved, camera and gun stayed at the origin.** Two causes at once.
The vcams had a `Follow` target but only `CinemachinePanTilt` — a *rotation* control — so they had no
position control and never left their own transform. And `CameraRig` was parented as a **sibling** of
the transform the Rigidbody moves, so it could not inherit motion either. Fixed by adding
`CinemachineHardLockToTarget` and re-parenting the rig under the moving root. **It failed silently:
no error, no warning.** Worth remembering that a misconfigured `CinemachineCamera` produces no
diagnostic at all.

**Bugs 2-4 (2026-08-29, fixed) — found by Carlos's second playtest.**

- **Every shot threw `NullReferenceException` in `GunScriptableObject.PlayTrail`.**
  `BulletImpactEffects.Instance` was null. It is a scene singleton — Burntwax kept it on a
  `BulletSurfaceManager` GameObject in its demo scene, so it never came across with the prefab. Now
  a `Bullet Impact Effects` child under `MrMoonlight Systems`, with all 7 bullet `SurfaceEffectSO`
  assets wired. It gets its **own** child because its `Awake` destroys duplicates by destroying its
  GameObject — sharing `MrMoonlight Systems` would take our whole systems object with it.
- **Aim-down-sights spun the view and the right stick stopped controlling aim.** The bridge drove
  only `vcam_FPS`'s `CinemachinePanTilt`. ADS swaps the active camera to `vcam_Aim` by priority, so
  its axes were never written — dead stick while aiming, and blending a camera whose yaw kept moving
  against one frozen at a stale value made the view rotate. Fixed by making the bridge own a single
  authoritative yaw/pitch and push it to **every** `CinemachinePanTilt` on the rig.
  **Rule: any camera added to the rig must be driven too, or ADS breaks again.**
- **No hands, just a floating gun.** `Arms_Mesh` and `Hands_Mesh` ship as **disabled GameObjects**
  in Burntwax's `Arms.prefab`. The meshes and materials (`Shirt`, `Hands`, URP/Lit) were fine all
  along. Enabled on our prefab.

**Bug 5 (2026-08-29, fixed) — crouching squashed the entire player, camera and weapon included.**
`PlayerStateMachine.LocalScale` mapped to `transform.localScale` — the **root**. Burntwax could do
that safely because its camera rig lived loose in the scene, so only the capsule squashed. In our
prefab the camera rig, arms viewmodel and HUD canvas are all children of the root, so crouching
squeezed the whole view. Retargeted to `player` (the `Body` transform), which still squashes the
visual capsule and its `CapsuleCollider` exactly as intended.

**This is the general hazard of putting the camera rig inside the prefab.** Any Burntwax code that
touches `PlayerStateMachine.transform` now affects the camera and weapon too. When wiring further
Burntwax behaviour, check whether it means *the player root* or *the visual body* — `player`
(= `Body`) is almost always what is meant.

**A modal dialog can also block every MCP call.** With the prefab open in Prefab Mode, entering Play
Mode raises Unity's *"Risk of unwanted modifications"* warning, because Animation Rigging's
`RigBuilder` is `[ExecuteInEditMode]`. Unity stops responding to tooling until a human clicks. Choose
**Exit Prefab Mode**, not Ignore — Ignore lets `RigBuilder` write hand-IK changes into the prefab
asset during play.

**Bug 6 (2026-08-31, fixed) — sprint (and walk) quietly lost speed on real terrain, fine on a flat
plane.** `PlayerStateMachine.PlayerSlopeCheck()` treats **any angle over 0.03°** as "sloped" — a
threshold so tight that a Gaia heightmap terrain's ordinary vertex-level micro-bumps put the player
in `PlayerSlopeState` almost permanently, not just on visible hills. `SpeedControl()`'s Slope branch
then clamped **total 3D velocity** (horizontal + vertical) to `playerVelocity`, instead of horizontal
only like every other branch. The floating-capsule ride spring (`ApplyFloatingForce`) is constantly
making small vertical corrections to hold ride height over that uneven ground, and those corrections
ate into the same speed budget as horizontal movement — so speed silently dropped on almost any real
terrain while feeling completely normal on the hand-built flat Sandbox plane. Diagnosed live with
Carlos while testing sprint distance between MRM-70 blockout waypoints.

**Fix:** `SpeedControl()` now clamps horizontal speed only in the Slope branch too, matching the
non-slope branch — see the commented-out original block left in place at the fix site
(`PlayerStateMachine.cs`, `SpeedControl()`) for an exact revert. Contained to that one clamp — does
not touch jump, ride height, crouch, or state transitions. Side effect: walking on slopes also gets
marginally faster now, not sprint-only, since the clamp isn't sprint-specific. **If movement ever
feels too fast on steep terrain, or slope traversal starts feeling floaty/sliding, this is the first
place to check** — swap back to the commented original block to restore the old (buggy-on-real-terrain,
correct-on-flat-planes) behavior.

**Bug 7 (2026-08-31, fixed) — a stray duplicate `PlayerStats` + `BurntwaxPlayerBridge` pair sat on
the `Player` root, disconnected from everything.** §3's architecture diagram is explicit that these
two belong on `MrMoonlight Systems` only — `Player` root should carry nothing but Burntwax's own
unmodified components. In practice both had drifted onto `Player` root too at some point, as an
inert, silently-unused second copy. This bit hard while testing the infinite-stamina debug toggle
above: the toggle got attached to the wrong (Player-root) copy, so it visibly showed "locked" while
the *real* copy — the one `BurntwaxInputBridge` actually gates sprint against — kept draining
normally. Diagnosed live by reading both instances' `Stamina.Value`/`IsLocked` side by side mid-Play.

**Fix:** removed both stray components from `Player` root, so there is exactly one
`PlayerStats`/`BurntwaxPlayerBridge` pair in the whole hierarchy, matching §3's diagram exactly.
Two consumers had to change to survive this: `StatDebugPoolZone` and `StatDebugModifierZone` used
`other.GetComponentInParent<PlayerStats>()`, which can only walk *up* the hierarchy — since the real
`PlayerStats` lives on a **child** (`MrMoonlight Systems`), that call could never have found it even
before this cleanup, so those two debug zones were silently doing nothing against the real stats the
whole time. Both now use `other.transform.root.GetComponentInChildren<PlayerStats>(true)`, the same
root-then-down pattern the bridges already use.

**Removing the pair required a temporary detour:** `PlayerStats` and `BurntwaxPlayerBridge` each
`[RequireComponent]` the other, so neither can be removed — by the Editor's Remove Component command
*or* `Object.DestroyImmediate` — while the other is still present on the same GameObject. Had to
comment out both `[RequireComponent]` attributes, remove the stray pair, then restore both
attributes. If this ever needs doing again, that's the only way through.

**If a future symptom looks like "a debug/stat tool works on one code path but not another," check
for a second `PlayerStats` or `BurntwaxPlayerBridge` before assuming a logic bug** — this is now the
second time a duplicate got found this way.

---

## 8. Answering Carlos's question about animations

**The inertia is procedural, not baked.** `BobAndSway.cs` drives transform offsets from Rigidbody
velocity through `AnimationCurve`s. It never touches the Animator. The `.anim` clips are only
per-weapon aim / equip / recoil / reload / stow.

**So swapping in our own models and animation clips later keeps the movement feel for free.** The
weapon-lowering-on-stop that Carlos noticed in the store video is velocity-driven sway, and it will
apply to any future weapon model.

Bonus: `equip_Katana` and `stow_Katana` clips ship with **no katana weapon** — a free melee
equip/stow template for the Pickaxe.
