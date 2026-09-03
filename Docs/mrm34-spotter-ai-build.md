# MRM-34 — Spotter AI, combat, flare and reinforcements

Built 2026-09-01/02 on branch `mrm-75` (Carlos's call — see memory `mrm75_branch_scope_exception`;
the branch discipline slip is known and accepted for demo speed).

This document is the **as-built record**, not a plan. Everything below was verified by reading real
component state back out of Unity, not by intending to set it.

---

## 1. What landed

| Area | What |
|---|---|
| **Blaze AI Engine** | Transferred into Mr. Moonlight as a **runtime dependency** (was Playground-only) |
| **Shared enemy framework** | Health/damage, ranged weapon + rhythm, reinforcement spawner, death drops, audio hooks, patrol modes — all reusable by Zealot/Wolf/Furman |
| **Spotter specifics** | The flare call and the low-health panic call |
| **Flare VFX** | Built from scratch (the Asset Store one is unusable — §6) |
| **Lamp** | Imported, textured, prefab with a real `Light`, socketed on the Spotter, drops on death |
| **Island NavMesh** | Baked, bounded above sea level, **MRM-27's open question answered** (§7) |

---

## 2. Blaze AI transfer

**Source:** `E:\playground\My project\Assets\PLAYGROUND\Blaze AI Engine\Scripts\` (48 scripts, 832 KB).
**Destination:** `Assets/_Project/Code/Vendor/Blaze AI Engine/Scripts/` — tracked and editable, per the
vendor-code policy in `unity-conventions.md` (script logic to `Code/Vendor/`, binaries stay in
`ThirdParty/`). The 285 MB `Demos` folder was **not** transferred and is not needed.

Three things had to change to make it work here:

1. **It ships with no asmdef.** Without one it lands in `Assembly-CSharp`, which an asmdef assembly
   like `MrMoonlight.Runtime` *cannot reference*. Added `Blaze.Core.asmdef` (runtime) and
   `Blaze.Editor.asmdef` (Editor-only), and added `Blaze.Core` to both
   `MrMoonlight.Runtime.asmdef` and `MrMoonlight.Editor.asmdef`.
2. **Its editor scripts were in three separate `Editor/` folders.** Inside an asmdef tree an
   `Editor` folder is *not* automatically split out, so three folders would have meant three
   editor asmdefs. Consolidated into one `Scripts/Editor/` tree (`Editor/`, `Editor/Behaviours/`,
   `Editor/Additive/`) covered by a single asmdef. `.meta` files travelled, so GUIDs are intact.
3. **The `Tags&Layers.preset` was deliberately NOT applied.** MRM-27 warns that layer order is
   load-bearing for terrain splatmaps and footstep surfaces. It turned out to be unnecessary: the
   project already has a `Player` tag, an `Enemy` tag, and `Player`/`Enemy`/`Ground`/`Destructible`
   layers, which is everything Blaze's vision and avoidance masks need. **No layer or tag changed.**

Dependencies: `UnityEngine`, `UnityEngine.AI`, `UnityEngine.Events`, `UnityEditor` only.
`com.unity.ai.navigation` 2.0.14 was already installed.

---

## 3. Two Blaze facts that contradict what the issues assumed

These cost time to find and are worth not re-deriving.

### 3.1 Blaze drives the existing Animator Controller by state name

Resolved the open question carried since the character work (`mrm75-resume-2026-09-01-evening.md` §4).
`AnimationManager.Play(state, time, overplay)` calls `animator.CrossFadeInFixedTime(state, …)`.
It **plays states in whatever Animator Controller is on the enemy, by name**. The hand-authored
13-state `AC_Spotter.controller` is exactly what Blaze wants — none of that work was wasted, and no
migration to "string-keyed clips" is needed because that *is* what the controller already provides.

Every animation name Blaze is configured with here matches a real state:
`Idle · Walk · Run · Shoot · Reload · Flare · Hit · Death · StrafeLeft · StrafeRight`
(unused so far: `Fall`, `Downed`, `DeathDowned`).

Use `overplay: true` to replay the *same* state twice in a row — `Play` early-outs when the
requested state equals the current one, which would otherwise swallow the second barrel.

### 3.2 A `BlazeBehaviour` subclass REPLACES a state — it cannot run alongside one

**MRM-29 and MRM-27 both describe the extension pattern as "write Carlos's spec as a
`BlazeBehaviour` subclass".** That is right for anything that substitutes for a state, and wrong for
anything that has to watch conditions while a normal state is running.

`BlazeAI.RunBehaviour` (`BlazeAI.cs` ~line 2314) only ever ticks the single behaviour assigned to
the **current state slot** (`normalStateBehaviour`, `attackStateBehaviour`, …). There is no list of
extra behaviours and no way to add a new state to the `BlazeAI.State` enum.

So the Spotter's flare — which must be monitored *during* the attack state — is a plain
`MonoBehaviour`, and the flare action itself goes through Blaze's actual sanctioned custom-state
hook: **`BlazeAISpareState` / `blaze.SetSpareState(name)`**. A spare state is a real
`BlazeAI.State.spareState`, interrupts cleanly, plays animations, fires enter/exit UnityEvents, and
returns to the previous state on a timer.

**This changes MRM-29's plan for the three-blind-run search**: it *can* legitimately be a
`BlazeBehaviour` (it replaces the attack state's search), but any future "watch for a condition
while patrolling" behaviour cannot be.

### 3.3 `AddComponent<BlazeAI>()` leaves `vision` and `waypoints` null

Blaze's nested `[System.Serializable]` class fields (`Vision`, `Waypoints`) have no field
initialisers. Unity's serializer default-constructs them when a component is *deserialized*, but
`AddComponent` does not run the serializer — so from script they come back `null` and the first
field write throws. The inspector never shows this because it only ever sees deserialized copies.
**Building a Blaze agent from script must `new` them explicitly.**

---

## 4. The shared enemy framework

All under `Assets/_Project/Code/Runtime/Enemies/`, namespace `MrMoonlight.Enemies`. Nothing here is
Spotter-specific — the split is deliberate so the Zealot (MRM-35) and Wolf (MRM-33) reuse it and
only change numbers and the one behaviour that is theirs.

| File | Responsibility |
|---|---|
| `IDamageable` / `DamageInfo` | The one damage entry point. **Moved to `Code/Runtime/Combat/` in session 2 — see §13.3** |
| `EnemyHealth` | Health, damage, death, `Damaged` / `LowHealthReached` / `Died` events. Drives Blaze if present, works without it |
| `EnemyHitbox` | A collider that forwards damage with a multiplier. **A deliberate stub of MRM-32**, not MRM-32 |
| `EnemyIdentity` | `EnemyKind` + alive flag, so "is another Spotter nearby?" needs no name comparison |
| `EnemyFirearm` | One shot: pellets, cone spread, hitscan, damage falloff, pooled tracer streaks |
| `EnemyRangedAttack` | The **rhythm**: aim → fire → pause → fire → reload lock |
| `EnemyReinforcementSpawner` | Scattered, non-stacking wave placement on the NavMesh |
| `EnemyDeathDrop` | Detaches carried props on death so they fall and roll |
| `EnemyPatrolRoute` | Idle / random wander / authored waypoint route (§5) |
| `EnemyAudioHooks` | Named, silent audio call sites (§8) |
| `IReinforcementCaller` | Lets the spawner silence every caller on a spawned reinforcement at once |

### Why the firing rhythm is ours and not Blaze's

Blaze can pace shots itself, but both mechanisms (`attackInIntervals`, and the cover shooter's
`totalShootTime` / `delayBetweenEachShot`) express cadence as **randomised windows**. MRM-34 asks
for something exact: *two* shots, then a reload, every time. Deriving "exactly two" from a random
total-time window is arithmetic that silently breaks the moment a tunable moves.

So Blaze's `attackEvent` is treated as *"you may open fire"*, and `EnemyRangedAttack` runs the burst
as one readable coroutine, ignoring repeat requests while a burst or reload is in flight. Blaze
keeps what it is genuinely better at: closing to engagement distance, facing, line-of-sight checks,
strafing, backing off, and deciding when to attack at all.

`EnemyRangedAttack.CycleDuration` is derived from the tunables and **written into Blaze's
`attacks[0].attackDuration` at `Awake`**, so the two can never drift apart.

### Why `AttackStateBehaviour` and not `CoverShooterBehaviour`

MRM-34's triage note points at `CoverShooterBehaviour`, and on paper it is the better fit. In
practice it needs real cover objects registered with `BlazeAICoverManager`, and **the island has
none placed**. With no cover in the world it degrades to a plain ranged attacker anyway. The plain
attack state gets the same result today with far fewer moving parts. Swapping the cover shooter in
later only changes which UnityEvent calls `EnemyRangedAttack.RequestFire` — `EnemyRangedAttack`
already syncs engagement distance into both.

### The deliberate miss

MRM-34 asks for ~30% of shots to miss. This is implemented as a **deliberate miss, not a failure to
aim**: the shot still fires, still draws tracers, still hits the world — it is aimed wide by
`SpotterMissAngle`. The player reads it as being shot at, rather than as the enemy having lost them.

The miss offset is applied **once to the whole shell**, not per pellet — the cone stays a cone and
just points somewhere else. Rolling per pellet would only widen the spread and half of it would
still connect. The roll happens **per shot, not per burst**, so both barrels missing together (which
reads as the enemy being broken) is rare rather than guaranteed.

### The two reinforcement triggers are deliberately separate

Carlos was explicit (2026-09-01) that the low-health call is an **additional** trigger, not a
replacement for the flare. They stay separate code paths because they mean different things:

|  | `SpotterFlareCall` | `SpotterPanicCall` |
|---|---|---|
| Nature | Proactive | Reactive |
| Trigger | Fighting **and** no other Spotter within `SpotterAloneCheckRadius`, for `SpotterFlareTimer` seconds | Health crosses below `EnemyLowHealthThreshold` |
| Visual | Flare fired into the sky | None — it is a shout, not a signal |
| Wave size | 3–10 | 1–3 |
| Once only | Yes | Yes, independently |

Folding them together would couple "hurt" to "isolated" and make both harder to tune.

### Runaway guard (not in the issue — flag for Carlos)

`EnemyReinforcementSpawner.suppressCallsOnSpawned` is **on by default**. Ten Spotters each summoning
ten is exponential and a guaranteed frame-rate cliff; MRM-34's stated worst case is *ten total*.
Spawned reinforcements have every `IReinforcementCaller` on them silenced. Untick it if the runaway
wave is actually wanted.

---

## 5. Patrol modes — the inspector control Carlos asked for

`EnemyPatrolRoute` + a custom inspector (`EnemyPatrolRouteInspector`). One dropdown at the top; the
options below it change to match, so there is never a wander radius sitting next to a route
inviting "which of these is actually being used?".

| Mode | Behaviour | Options revealed |
|---|---|---|
| **Idle** | Stands at his spawn point, sweeping his cone. Still chases and searches when he sees something, then returns | none |
| **Random wander** *(default)* | Roams a radius around wherever he is dropped | wander radius, avoid-points-behind-obstacles |
| **Waypoints** | Walks an ordered route | **Linear** (walk once, idle at the end) or **Loop**, the waypoint list, and ground-snapping settings |

Random wander is the default purely because it makes the prefab work with **zero setup**, which is
the drag-and-drop requirement. It is not in MRM-29's spec; Idle and Waypoints are.

**A floating waypoint never makes an enemy fly** (MRM-29's acceptance criterion). Only a marker's
horizontal position is used — the height is re-derived by raycasting onto the ground and then
settling the result onto the NavMesh. A marker over water or off-mesh is **reported by name at
startup** rather than silently producing a broken route. The route is drawn as a gizmo when the
enemy is selected, with the closing segment drawn only for Loop, so the two modes are
distinguishable at a glance.

Idle mode calls `blaze.StayIdle()` **every frame while in the normal state only** — not in alert.
Blaze clears the flag when it returns to patrol after a search, and holding through alert as well
(which is what Blaze's own `StayInPosition.cs` does) would stop a static guard from ever
investigating, which MRM-29 explicitly wants him to do.

---

## 6. The flare VFX — why it was built, not imported

Carlos pointed at the Asset Store **"Flare Gun" (Rokay3D)** package. It was extracted from the
Asset Store cache and inspected **without importing it**. Its VFX is unusable:

- `flarebullet.prefab`'s `ParticleSystemRenderer` points at material GUID `473d6d3ec0d161b4a85e466c8c6da3fb`,
  **which is not in the package at all** — it referenced Unity's long-removed Standard Assets smoke
  material. A dangling reference, not a fixable material.
- The two materials it *does* ship use the built-in **`Standard`** shader (`m_Shader: {fileID: 46}`),
  not URP. They render magenta here.
- It is a 2013-era asset. No Shader Graph, no URP anything.
- Its gun model is redundant — `Weapon_FlareGun.prefab` already exists in the project.

So the flare is ours. It is composed of four things rather than one, because no single one of them
does the job:

| Part | Why |
|---|---|
| **`MrMoonlight/VFX/FlareCore`** shader on a billboard quad | The burning chemical glow. Additive, three stacked radial falloffs (white-hot core, coloured body, wide soft halo), camera-facing in the vertex stage with a time-driven flicker and size pulse |
| **`TrailRenderer`** | The arc through the air |
| **Two particle systems** | Rising smoke, falling sparks |
| **A real `Light`** | **The standing project rule** — glowing objects get a Light, never an emission map. It is also the point: a flare that does not change how the forest is lit is just a sprite |

`FlareProjectile.cs` drives it — launch at `FlareLaunchPitch` above the aim direction with
below-1 gravity so it hangs and reads as a signal, flicker at a fixed rate (per-frame randomisation
reads as television static; a slower held value reads as combustion), then burn out and fade.

**Hand-authored `.shader`, not Shader Graph.** Carlos's stated preference was Shader Graph. The
project's existing `Art/Environment/Water/Water.shader` sets the precedent, and the look here is a
handful of radial gradients plus a flicker — a dozen lines of HLSL versus a forty-node graph. **If
it ever needs hand-tweaking visually, the maths in the file is the spec for converting it.**

Assets: `Art/VFX/Flare/FlareCore.shader`, `M_FlareCore.mat`, `M_FlareSmoke.mat`,
`Art/VFX/Shared/T_SoftDot.png` (generated, not imported — no licence, no import settings to drift),
`M_EnemyTracer.mat`, prefab `Prefabs/VFX/VFX_Flare.prefab`.

---

## 7. The Island NavMesh — MRM-27's open question, answered

**MRM-27 asks: "Do terrain trees rasterise as NavMesh obstacles?" The question turned out to be
moot, in a way neither MRM-27 nor MRM-29 anticipated.**

The live terrain has **`treeInstances = 0` and `treePrototypes = 0`**. There are no terrain trees at
all. The vegetation pass (MRM-70) spawns everything as **real GameObjects**, and the scene carries
**5,891 `CapsuleCollider`s** plus 158 `BoxCollider`s and the `TerrainCollider`. So the vegetation
rasterises into the NavMesh normally, as ordinary physics colliders. **No collider pass and no path
exclusion mask are needed.** The risk MRM-27 told us to budget for does not exist.

### The bake

`NavMesh Surface` GameObject in `Island.unity`, baked to
`Assets/_Project/Scenes/Island/NavMesh-Island.asset`.

| Setting | Value | Why |
|---|---|---|
| Collect objects | Volume | MRM-27's "bake only the walkable area" |
| Volume | 1024 × 67.5 × 1024 m, centred Y = 41.25 | Starts at **Y = 7.5**, just under Crest's sea level of 8, so submerged terrain is excluded and enemies cannot path across the sea floor. Tops out at 75 m; the island summit is 69.4 m |
| Geometry | **Physics Colliders** | Cheaper and more accurate than render meshes, given 5,891 collider-bearing plants |
| Voxel size | 0.2 m (override) | |
| Tile size | 256 | |
| Min region area | 4 | Drops unreachable slivers on rock tops |

**Bake cost: 681 ms.** 169,205 vertices / 75,671 triangles. Far cheaper than expected — a 128 m
probe bake was run first specifically to avoid committing blind to a full-island bake.

### Measured connectivity — read this before placing enemies

Sampling a 50 m grid over the island gives **299 on-mesh points in 21 connected components**, the
largest covering **41%**. The island's walkable surface is *not* one connected body.

This is mostly **correct behaviour** — MRM-27's own acceptance criterion is "enemies cannot climb a
cliff", and the fragments are beaches separated by water and plateaus separated by >45° slopes. But
it has a practical consequence: **an enemy dropped on one landmass cannot reach another.** Anything
that assumes island-wide chase needs to check this.

### Left as-is, deliberately — for MRM-27 to tune

The bake uses the **default `Humanoid` agent type** (radius 0.5 m, height 2 m, slope 45°, step
0.75 m). The Spotter's own `NavMeshAgent` is radius 0.35 / height 1.8. The mismatch is *safe* — the
bake is conservative, so enemies keep slightly more clearance from trees than they strictly need —
but MRM-27 lists agent radius, slope limit and step height as tunables, and changing them means
either editing the project-wide `Humanoid` agent or adding a custom agent type. **Neither was done
without asking**, since it is a project-settings change.

---

## 8. Audio — placeholder hooks only

Carlos's instruction (2026-09-01): no sounds exist yet, so wire the call sites and nothing else.

`EnemyAudioHooks` has a named method per event (`PlayFire`, `PlayReload`, `PlayFlareFire`,
`PlayPain`, `PlayDeath`, `PlayAlerted`, `PlayFootstep`) with a serialized clip slot behind each.
**Drop a clip in and it plays; leave it empty and nothing happens and nothing warns.** So the audio
pass later is an inspector job, not a code job. `PlayDeath` and `PlayPain` are already wired to
`EnemyHealth`'s events on the prefab.

Clip naming when they arrive: `ENM_Spotter_*` — the prefix drives the import preset, see
`Docs/audio-import-workflow.md`.

---

## 9. The lamp

Source: `FLASHLIGHT LATERN PACK 6 OF 6 BY RETROS/FBX/Lamp_3.fbx`, Carlos's file.

- **1,518 verts, 0.08 × 0.32 × 0.14 m** — real-world scale, no import scale fix needed. (Note: this
  is *not* the AccuRig case from memory `mrm75_accurig_scale_bug` — that 0.01 global scale is
  specific to AccuRig FBX exports.)
- Texture: the **DIRT** variant (Carlos's choice), **512 px**, run through the standard pixelation
  pass (`Tools/pipeline/texture_pass.py run … --size 512`). **No normal map** — Carlos is providing
  one later.
- Two materials, `M_Lamp` (body) and `M_LampGlass`. Both **cloned from `M_FlareGun.mat`** rather
  than built from scratch — that carries the exact `m_Ints` *and* the matching `m_ValidKeywords`
  across in one step, which is the only reliable way to avoid the RetroLit `KeywordEnum` desync
  documented in `3d-prop-pipeline-wizard.md` §3.2. Verified: `_SnapMode = 3` with `_SNAPMODE_OFF`
  active (small-prop rule).
- `Prefabs/World/Prop_Lamp.prefab`: mesh + **real point `Light`** (warm kerosene 1.0/0.82/0.55,
  intensity 2.5, range 14, soft shadows) + BoxCollider. Rotation (0,0,0), scale (1,1,1).

**Moved 2026-09-03 (§16): socketed at `CC_Base_Hip/Socket_Lamp`**, not the hand — Carlos's call, to
free the hand for its own hitbox. Local offset `(0.18, -0.05, 0.10)` is still a placeholder for
Carlos to nudge by hand. It is listed in `EnemyDeathDrop` so it detaches on death and keeps burning.
It now also carries `LampSwayEffect` (tilts on local X with NavMeshAgent speed) and sits on the
`DroppedProp` physics layer (ignores collision with `Enemy`, excluded from every gun's `HitMask`) —
see §16 for all three.

---

## 10. Prefab as built

`Assets/_Project/Prefabs/Enemies/Enemy_Spotter.prefab` — layer `Enemy`, tag `Enemy`.

```
Enemy_Spotter                     Animator, NavMeshAgent, CapsuleCollider, AudioSource,
                                  BlazeAI + Normal/Alert/Attack/Surprised/Hit + SpareState,
                                  EnemyIdentity, EnemyHealth, EnemyAudioHooks, EnemyFirearm,
                                  EnemyRangedAttack, EnemyReinforcementSpawner, EnemyDeathDrop,
                                  EnemyPatrolRoute, SpotterFlareCall, SpotterPanicCall,
                                  EnemyDebugControls
├── old___timer_001                skinned mesh
└── root/…/CC_Base_R_Hand/Socket_Weapon/DBShotgun/Muzzle      ← null, +X barrel, rot (0,90,0)
    …/CC_Base_L_Hand/Socket_Flare/FlareGun/Muzzle             ← null, +X barrel, rot (0,90,0)
    …/CC_Base_L_Hand/Socket_Lamp/Lamp                         ← Prop_Lamp instance
```

The two `Muzzle` nulls were added to `Weapon_DoubleBarrelShotgun.prefab` and
`Weapon_FlareGun.prefab` themselves, not to the Spotter's instances of them, so any other holder of
those weapons gets them for free. **Both weapon models run along local +X**, so the nulls are
rotated 90° on Y to make their forward (`+Z`) point down the barrel — everything that fires reads
`muzzle.forward`.

### Event wiring (verified by reading the persistent listeners back)

| Event | Listeners |
|---|---|
| `AttackStateBehaviour.attackEvent` | `EnemyRangedAttack.RequestFire` |
| `EnemyHealth.Died` | `EnemyDeathDrop.DropAll`, `EnemyRangedAttack.CancelBurst`, `EnemyAudioHooks.PlayDeath` |
| `EnemyHealth.Damaged` | `EnemyAudioHooks.PlayPain` |
| `EnemyHealth.LowHealthReached` | `SpotterPanicCall` subscribes in code (`OnEnable`) |

The flare spare state (`MoonlightSpotterFlare`) is **registered from code at `Awake`**, not left for
the inspector — confirmed present at runtime. That is what keeps the prefab genuinely drag-and-drop.

---

## 11. What was verified in session 1, and how

| Claim | Evidence |
|---|---|
| Everything compiles | Type reflection per assembly; `MrMoonlight/VFX/FlareCore` reports `isSupported = True`, zero shader compiler messages |
| Prefab is fully wired | Every serialized reference and every persistent UnityEvent listener read back out of the saved asset |
| Patrols on the real island | Play mode: 3 Spotters, all `agent.isOnNavMesh = true`, state `normal`, two already walking to random destinations, animation `Walk` |
| Flare spare state auto-registers | Play mode: `spareStates = [MoonlightSpotterFlare]` on all three |
| Wave spawns 10 | `SpawnWave(…, 10)` → 10 placed |
| **They do not stack** | Closest pair **3.84 m** vs the 2.5 m minimum spacing tunable |
| They land on walkable ground | 0 of 10 off-NavMesh |
| Scatter respected | Furthest spawn 18.7 m vs the 20 m radius |
| Runaway guard holds | **10/10** spawned Spotters refused to flare |
| **Lamp and shotgun detach on death** | Both reparent to scene root, **world position drift 0.0000 m** |
| **Lamp keeps burning after it falls** | `Light` still enabled, intensity 2.5, range 14, no longer under the Spotter's hierarchy |

> Session 1 could not watch the combat rhythm run, because play mode does not tick while the Unity
> editor is unfocused. **That was resolved in session 2 — see §14.**

---

## 12. Open items after session 1

1. `Island.unity` unsaved — **resolved, Carlos saved it.**
2. **NavMesh agent settings** — see §7. Project-wide change, not made without asking. Still open.
3. **`PlayerSettings.runInBackground` was turned on.** See §13.10 for the wrinkle.
4. **The lamp's position on the hand is a placeholder**, as agreed.
5. **The lamp has no normal map**, as agreed — Carlos is providing one.
6. **MRM-29's three-blind-run search is not built.** Blaze's own search (move to last known
   position, search a radius, return to patrol) is configured in its place. Per §3.2 the authored
   version *can* legitimately be a `BlazeBehaviour` replacing the attack state.
7. **Shotgun drops but has no pickup behaviour**, as agreed — MRM-26 owns that.
8. **Ten-Spotter frame cost is not measured in a build** (MRM-34 acceptance criterion, ties to
   MRM-64). Still open after session 2.

---

## 13. Session 2 (2026-09-02) — making the fight actually happen

Everything above was built without the two sides being connected. Carlos asked the obvious question
— *"if I stand in front of him, he won't follow me, right?"* — and the answer was no, for a reason
worth recording.

### 13.1 🐛 The Spotter could never see the player

**Blaze matches hostiles by the tag on the collider's own GameObject** (`BlazeAI.cs:1441`,
`System.Array.IndexOf(vision.hostileTags, visionHitArr[i].tag)`). Not the tag on the parent — the
tag on the object the collider is attached to.

The player was set up like this:

```
Player          tag = "Player"     ← has the tag, but NO collider
└── Body        tag = "Untagged"   ← has the ONLY collider
```

So the vision sphere found `Body`, read `"Untagged"`, saw it was not in `hostileTags` (`["Player"]`),
and skipped it. Every frame, forever. The Spotter was looking straight at the player and concluding
there was nothing there.

**Fix:** `Body` is now tagged `Player` on `Player.prefab`. Checked first that nothing depended on it
being Untagged — only Burntwax's bullet-impact surface lookup reads collider tags
(`GunScriptableObject`), and that does a `List.Find` which simply returns null for an unknown tag.

> **Rule for any future enemy or vision system: the tag has to be on the collider.** A tag on a
> parent with no collider is invisible to physics-driven detection.

### 13.2 The player can now be hurt — `PlayerDamageReceiver`

The Spotter's pellets were hitting the player and doing nothing, because nothing on the player
implemented `IDamageable`.

`PlayerDamageReceiver` (on the **Player root**) closes that. It deliberately **owns no health**:
`PlayerStats.Health` is already the single source of truth (MRM-12), Burntwax's `PlayerHealth` is
display-and-marker only, and `BurntwaxHealthBridge` connects them. A third float would desync
exactly the way that bridge exists to prevent, so this routes damage through the same
`Stat.Deplete` call — defense modifiers, the death event and MRM-17's death sequence all keep
working untouched.

**It goes on the Player root, not on `MrMoonlight Systems` where `PlayerStats` lives.** A shot
resolves its target with `GetComponentInParent<IDamageable>()` from the collider it hit, and
`Body`'s parent chain reaches the root, not the Systems child. On Systems, every shot would pass
straight through.

> ⚠️ **Testing gotcha:** `GetComponentInParent<T>()` skips inactive GameObjects by default, and a
> **prefab asset counts as inactive**. Verifying the wiring against the asset returns null and looks
> like a bug. Test against the live scene instance, or pass `includeInactive: true`.

### 13.3 `MrMoonlight.Combat` — a new assembly, and why it had to exist

`IDamageable` and `DamageInfo` moved from `Code/Runtime/Enemies/` to **`Code/Runtime/Combat/`**,
namespace `MrMoonlight.Enemies` → **`MrMoonlight.Combat`**, with their own
`MrMoonlight.Combat.asmdef` (no references — it is the bottom of the stack).

Two reasons, one forced and one that was right anyway:

1. **Forced:** `MrMoonlight.Runtime` already references `Burntwax.Core`. For Burntwax's gun to call
   `IDamageable`, `Burntwax.Core` would have had to reference `MrMoonlight.Runtime` — **a circular
   assembly reference, which Unity forbids.** A third assembly both can depend on is the only way.
2. **Right anyway:** `IDamageable` was never enemy-specific. The player implements it too, and
   `MrMoonlight.Player.PlayerDamageReceiver : MrMoonlight.Enemies.IDamageable` read wrong.

Assembly graph now:

```
MrMoonlight.Combat        (no references — IDamageable, DamageInfo)
   ↑               ↑
Burntwax.Core    MrMoonlight.Runtime → Burntwax.Core, Blaze.Core
```

### 13.4 F4 — invulnerability, and why it is not `Stat.Lock`

`InvulnerableDebugToggle` on the Player root. **F4**, deliberately one key along from F3's infinite
stamina and built to the same shape (same on-screen label style, inspector checkbox as well as the
hotkey). Its label sits *below* F3's so both can be on at once.

**It blocks the damage but not the hit.** Health never drops, but every absorbed shot is counted and
the running total shows on screen, with a `HIT` flash on each one. This is the whole point: from
behind an invulnerable player, *an enemy that is shooting and missing* and *an enemy that is
shooting and being ignored* look identical — and telling those apart is the reason to watch the
fight at all. The counter is the test result.

**It does not use `Stat.Lock`** the way F3 does. Locking the health stat would also freeze healing,
item effects and the red-tint feedback that reads from it, and would hide the hits entirely. Gating
at the damage entry point leaves the whole stat stack behaving normally underneath.

### 13.5 🚩 Player-gun → enemy damage: built, then descoped

A ~15-line hook was added to `GunScriptableObject.PlayTrail` routing player hits into `IDamageable`,
plus a guard so the gun can never damage its own shooter (the HitMask is Everything, which now
includes the player's own body).

**Carlos then said not to worry about player-gun damage — it is a later integration.** The code is
in and compiles clean; it was left rather than reverted because removing working, tested code costs
more than it saves and it makes the playtest better (a Spotter can be killed with the pistol instead
of the debug menu). **It is trivially removable** — one contiguous block, marked `MRM-34:` in the
source. MRM-32 owns the real version.

Note the `MrMoonlight.Combat` split (§13.3) stays regardless — it is correct architecture
independently of the gun hook.

### 13.6 Measured: is the Spotter too small?

Carlos thought he looked small. Measured rather than guessed:

| | Height |
|---|---|
| Spotter, feet to top of head (renderer bounds) | **1.86 m** |
| Spotter head *bone* | 1.51 m |
| Player capsule | 1.80 m |
| **Player eye height above their own feet** | **1.57 m** |

**He is not small — he is slightly taller than the player.** What makes him *read* small is that his
head bone sits at 1.51 m against the player's 1.57 m eye height, so you look very slightly **down**
at him. He is also an old-timer model with a stooped posture, which reads shorter than his actual
height.

### 13.7 Resizing him — does the NavMesh need re-baking?

**No.** The bake depends on two things, and the enemy's own scale is neither of them:

1. **The geometry baked into it** — terrain and the vegetation colliders. Unaffected by how big an
   enemy is.
2. **The agent type's settings** — the project-wide `Humanoid` type (radius 0.5 m, height 2 m,
   slope 45°, step 0.75 m). Also unaffected.

An enemy's own `NavMeshAgent.radius`/`height` are checked against the already-baked surface at
runtime. Change them freely.

**The one case where a re-bake is warranted:** if a resize pushes the agent's radius **above the
baked 0.5 m**. The navmesh is carved for a 0.5 m-radius agent, so anything fatter will clip tree
trunks and wall corners. Under 0.5 m is always safe (the bake is simply conservative).

**If you do rescale him, four things move together** — nothing reads the mesh to derive them:

| What | Where |
|---|---|
| Visual size | `Enemy_Spotter` root `Transform.localScale` |
| Body collision | `CapsuleCollider` → `height`, `radius`, `center` |
| Navigation footprint | `NavMeshAgent` → `height`, `radius` |
| Where his eyes are | `BlazeAI` → `vision.visionPosition.y` (currently **1.6**) |

Missing the last one is the subtle one: his sight rays would still originate at 1.6 m regardless of
his new size, so a shrunken Spotter would see over walls and a giant one would see through his own
knees.

### 13.8 Mistake made and recovered

While cleaning up test objects, a filter of "delete every root with an `EnemyIdentity`" also deleted
an `Enemy_Spotter` **Carlos had placed himself** at (348.09, 23.28, −70.06). It was recovered in
full by reloading the scene from disk — he had saved after placing it, so the file still had it, and
nothing of value was unsaved at the time.

**Lesson:** scoped cleanup must match on something only the cleanup created (a named holder object),
never on a component type that legitimate scene content also carries.

### 13.9 State of the two-way loop

| Direction | Status |
|---|---|
| Spotter shoots player | Pellet → `Body` (layer `Player`, in `EnemyFirearm.hitMask`) → `PlayerDamageReceiver` → `PlayerStats.Health` → health bar + death sequence |
| Player shoots Spotter | Ray → Spotter capsule (layer `Enemy`, all gun HitMasks are Everything) → `EnemyHealth` → Blaze hit reaction, panic call, death drops. Descoped by Carlos, see §13.5 |

Verified on the live scene: the `Body` collider resolves to `PlayerDamageReceiver`, the Spotter
capsule resolves to `EnemyHealth`, every gun's HitMask includes the `Enemy` layer, and Carlos's own
placed Spotter is on the navmesh (0.04 m) with a **`PathComplete`** route to the player 203 m away.

### 13.10 Play mode and editor focus

Play mode does not tick while the Unity editor window is unfocused, and MCP cannot give it focus —
`Time.time` stayed at 0.02 across calls even with `Application.runInBackground = true` (that setting
governs *builds*, not the Editor).

Two things learned the hard way:

- **A `PlayerSettings` change made *during* play mode is reverted on exit.** The first attempt at
  enabling `runInBackground` silently did not persist for that reason. It is now set outside play
  mode and saved.
- **Live verification through MCP requires Carlos to focus the editor window first.** Worth asking
  for directly rather than burning calls discovering it again.

---

## 14. ✅ Verified live in play mode (2026-09-02)

Carlos focused the Unity editor window, which is what makes play mode tick — see §13.10. The whole
loop was then watched running on the real island, not inferred. **Zero console errors throughout.**

### What happened, unprompted

A single Spotter was placed 12.9 m in front of the player, facing them, and play mode entered.
Nothing was scripted after that.

| Step | Observed |
|---|---|
| **Detection** | `visionMeter` reached **1.00**, `enemyToAttack = Body` — the tag fix works, he sees the player |
| **Chase** | Closed from 12.9 m to 5.7 m |
| **Combat** | `state = attack`, `anim = Shoot`, `IsBursting = true` — the firing rhythm runs |
| **Damage to the player** | **Player health 100 → 0.** The full pipeline works: pellet → `Body` → `PlayerDamageReceiver` → `PlayerStats.Health` |
| **The flare** | Fired **on its own** after the alone-timer elapsed — `HasFlared = true` |
| **Reinforcements** | **7 spawned**, all entered `attack` state and engaged |
| **They did not stack** | Closest pair **1.88 m** after converging, and visibly spread across the beach |
| **Runaway guard held** | All 7 reinforcements `flared = false` — none called a wave of their own |
| **Strafing** | Animations cycling `Shoot` / `StrafeLeft` / `StrafeRight` / `Idle` |
| **The lamps** | Each Spotter casts a warm light pool on the sand — visible in `Assets/Screenshots/mrm34_spotters.png` |
| **Patrol, undisturbed** | Carlos's own Spotter 206 m away stayed in `normal` state playing `Walk` — patrol runs independently |
| **Player gun → enemy** | The original Spotter dropped to **91 hp** — exactly the Pistol's 9 damage. Carlos shot him (this is the descoped hook from §13.5, working) |

### The flare VFX, seen at last

A flare was instantiated and held stationary for a close look
(`Assets/Screenshots/mrm34_flare_closeup.png`). All four layers render:

- burning orange-white **core** (the `MrMoonlight/VFX/FlareCore` billboard)
- **sparks** thrown outward
- **smoke** rising and dispersing
- the **real Light** throwing a strong orange pool across the sand and lighting the Spotters near it

In flight it arcs correctly — launched at 22 m/s, velocity `(0, 15.56, 15.56)`, i.e. the 45° pitch
from `FlareLaunchPitch`.

**Two honest tuning notes**, neither a bug:

- The **sparks read as elongated shards rather than embers.** They use a stretched billboard with
  `lengthScale 2.5` / `velocityScale 0.06`; dropping `lengthScale` toward 1.2 would make them read
  as sparks. Pure numbers, no code.
- The **core is a little small relative to how much light it throws.** `_Intensity` (5.0) and the
  `Core` child's scale (0.55) are the two dials.

### Screenshots

| File | What it shows |
|---|---|
| `Assets/Screenshots/mrm34_live_fight.png` | Game view during the fight |
| `Assets/Screenshots/mrm34_spotters.png` | Six reinforcements spread across the beach, each with a lamp light pool |
| `Assets/Screenshots/mrm34_flare_closeup.png` | The flare burning — core, sparks, smoke, light pool |

### What this closes

Every "not yet watched running" caveat in §11 and §12 is now resolved **except** the ten-Spotter
frame cost in a build (MRM-34's last acceptance criterion, ties to MRM-64). Seven simultaneous
Spotters ran in the editor without visible trouble, but editor performance is not build performance
and that measurement still has to be taken properly.

---

## 15. Handoff — known bugs and open gaps (2026-09-02)

Written at the point of handing MRM-34 to Sonnet. **Read §15.1 first; it is the one thing that is
actively broken.**

### 15.1 🐛 FIXED (unverified in play): enemy loops in his hit animation

**Reported:** "I hit the enemy once and then he looped in his stunned animation."

**Root cause — confirmed by reading the source and the live scene, not guessed:**

1. The player's gun passes `ActiveMonoBehaviour.gameObject` as the damage source. That is the
   `GunStateMachine`, which lives on **`Arms`** — a child of Player, tag **`Untagged`**.
2. `EnemyHealth.TakeDamage` handed that straight to `blaze.Hit(source)`, so
   `blaze.hitProps.hitEnemy = Arms`.
3. Blaze enters `hit` state and plays the flinch for `hitDuration` (0.6 s). Fine so far.
4. `HitStateBehaviour.FinishHitState()` ends the flinch with **`blaze.SetEnemy(hitProps.hitEnemy)`**
   — i.e. `SetEnemy(Arms)`.
5. Blaze's vision pass (`BlazeAI.cs` ~line 1293) **drops any `enemyToAttack` whose tag is not in
   `vision.hostileTags`**. `Arms` is `Untagged`, `hostileTags` is `["Player"]`, so it is rejected
   immediately.
6. The agent churns between having and not having a target, and never crossfades out of the hit
   animation. On screen: stuck in the stun.

**Fix:** `EnemyHealth.ResolveAttacker()` walks the source up to `transform.root` before handing it
to Blaze (and before storing it as `LastAttacker`). The tag that identifies a combatant lives on the
root — on the player and on every enemy alike. Verified by measurement: `Arms` tag `Untagged` →
rejected; root `Player` tag `Player` → accepted.

**Not watched running.** The diagnosis is confirmed from source; the fix follows directly and
compiles clean, but nobody has seen it work. **First thing to test.**

> **The general rule this produces:** anything handed to Blaze as an enemy must be the
> tag-carrying root, never the component that happened to fire. Same family as the collider-tag bug
> in §13.1 — Blaze identifies things by tag, and tags live in specific places.

### 15.2 ⚠️ Likely second-order: our burst coroutine ignores the hit

`EnemyRangedAttack.RunBurst()` is a coroutine that plays `Shoot` / `Reload` on its own schedule.
When a hit interrupts mid-burst, Blaze's `cancelAttackOnHit` stops *Blaze's* attack, but **nothing
cancels our coroutine** — it keeps driving the animator through and after the flinch.

`CancelBurst()` already exists and is wired to `EnemyHealth.Died`. It is **not** wired to the hit.
Suggested fix: call it from `EnemyHealth.TakeDamage`, or subscribe it to
`HitStateBehaviour.onStateEnter`. Left undone deliberately — it may well be masked by §15.1's fix,
and changing two things at once makes it impossible to tell which one mattered.

### 15.3 Open gaps, in rough priority order

| # | Gap | Notes |
|---|---|---|
| 1 | **§15.1 fix unverified** | Test first |
| 2 | **§15.2 burst-vs-hit interaction** | Only if the flinch still misbehaves after §15.1 |
| 3 | **Ten-Spotter frame cost in a build** | The last unmet MRM-34 acceptance criterion. Ties to MRM-64. Seven ran fine in the editor, but editor ≠ build |
| 4 | **MRM-29's three-blind-run search** | Blaze's own search stands in. Per §3.2 this one *can* legitimately be a `BlazeBehaviour` (it replaces the attack state's search) |
| 5 | **NavMesh agent tuning** (MRM-27) | Bake uses the default `Humanoid` (radius 0.5 / height 2 / slope 45° / step 0.75). Spotter's agent is 0.35 / 1.8. Safe but conservative. Changing it means editing the project-wide agent or adding a custom type — a **project-settings change, so ask first** |
| 6 | ~~**Lamp hand position**~~ | **Moved, still placeholder.** Now `CC_Base_Hip/Socket_Lamp` (§16), offset itself still needs Carlos's hand-tuning |
| 7 | **Lamp normal map** | Carlos is providing one. Drop into `M_Lamp`'s `_NormalMap` (**not** `_BumpMap` — RetroLit renamed it) |
| 8 | **Lamp not logged in `prop-log.md`** | The wizard says log only after Carlos's review gate. Still pending |
| 9 | **Flare spark shape** | Read as elongated shards, not embers. `Sparks` → `ParticleSystemRenderer.lengthScale` 2.5 → ~1.2. Pure numbers |
| 10 | **Flare core size** | Small relative to the light it throws. `M_FlareCore._Intensity` (5.0) and the `Core` child's scale (0.55) |
| 11 | **Shotgun drop has no pickup** | Detaches only, as agreed. MRM-26 owns pickups |
| 12 | **No audio** | `EnemyAudioHooks` call sites exist with empty clip slots. Inspector job, not a code job. Prefix `ENM_Spotter_*` |
| 13 | **Player-gun damage hook is descoped** | ~15 lines in `GunScriptableObject`, marked `MRM-34:`. Works, but is not MRM-32's real implementation. Remove or absorb |
| 14 | ~~**No per-bone hitboxes**~~ | **Done (§16).** 15 boxes replace the capsule height-band stub entirely — sizes/positions are still rough, Carlos is hand-tuning |
| 15 | **Debug tools still on the prefab** | `EnemyDebugControls` on the Spotter, `InvulnerableDebugToggle` (F4) on the Player. Delete when MRM-32 makes them redundant |
| 16 | **Hitbox gaps are now clean misses** | Removing the capsule's `EnemyHitbox` (§16) means a shot landing between the 15 boxes (armpit, neck, groin) no longer falls back to anything — Carlos's explicit call, flagged here in case it reads as a bug later |
| 17 | **`ShootConfig` HitMask edit is local-only** | The 4 Burntwax `ShootConfig` assets that exclude `DroppedProp` (§16) live under gitignored `Assets/ThirdParty/` — reapply if that package is ever re-fetched fresh |

### 15.4 Things that are done and should not be re-litigated

- Blaze transfer, asmdefs, and the decision **not** to apply `Tags&Layers.preset`
- The firing rhythm living in our code rather than Blaze's randomised windows (§4)
- `AttackStateBehaviour` over `CoverShooterBehaviour` — no cover objects exist on the island (§4)
- The flare being built rather than imported — the Asset Store one is genuinely broken (§6)
- The two reinforcement triggers staying separate code paths (§4)
- The runaway guard defaulting on (§4)
- `MrMoonlight.Combat` existing as its own assembly — forced by a circular reference (§13.3)
- The Island NavMesh bake and its settings (§7)
- The 51→102 Spotter population and the 15-box hitbox rebuild (§16) — Carlos's explicit design
  calls, not defaults picked without asking

---

## 16. Session 3 (2026-09-03) — pacing pass: population, hitboxes, lamp

Carlos ran a full demo playthrough and asked for two pacing changes, then escalated into a hitbox
redesign mid-session. All of it applied directly (scene/prefab work, permission asked and given each
time per `kickstart.md` §B.3), verified by reading the live component state back across every
placed instance afterward — not just the prefab source. Full detail: memories
`mrm34_spotter_scatter_doubling`, `unitymcp_execute_code_codedom_constraints`,
`mrm34_spotter_hitbox_rebuild` (Claude's own persistent memory, not in this repo, but the session
transcript's reasoning is reflected here).

### 16.1 Population: 51 → 102 Spotters, scattered

The 51 Spotters were hand-baked `Enemy_Spotter` clones directly in `Island.unity` (not a spawner,
not the event script) — confirmed by grepping the scene file for `Enemy_Spotter_` literal count.
Doubled by jittering one duplicate 15-40 m from each original, NavMesh-sampled (6 m tolerance),
rejected within 3 m of any other point (original or new). Result: 102 total, 0 placement failures,
closest pair 7.9 m apart, none underwater (Y range 8.1-55.3, sea level Y=8). `IslandEvents.txt`'s
comment updated from 51 to 102.

**Side effect worth knowing:** every Spotter carries a real lamp `Light`, and URP's additional-light
shadow atlas (2048×2048, shared across all shadow-casting lights in a frame) was already warning
before this change (`"Reduced additional punctual light shadows resolution..."`) — doubling the
population will make that fire more often. Purely a shadow-quality/perf signal, not an error; watch
for visibly blocky shadows in a firefight. MRM-60 already has a precedent tunable for capping
concurrent real-time lights in the mine that could be extended outdoors if this becomes a real
problem.

### 16.2 Hitboxes: capsule height-bands → 15 fixed boxes

Carlos supplied a reference image and explicitly overrode the height-band design from earlier the
same session (see §16.2's own predecessor, which briefly existed as a 6-box capsule-fallback design
before being replaced again within the hour — the capsule-fallback version never shipped past this
doc). Final design:

- **Root `EnemyHitbox` removed entirely.** The capsule is movement/NavMesh only now — no damage
  duty, no fallback. A shot landing in a gap between the 15 boxes (armpit, neck, groin) is a clean
  miss.
- **15 `BoxCollider` + `EnemyHitbox` (FixedZone) pairs**, one per bone: `Hitbox_Head` (Head zone);
  `Hitbox_Chest` / `Hitbox_Stomach` on `CC_Base_Spine02` / `CC_Base_Waist` (Torso zone);
  `Hitbox_{L,R}_Upperarm` / `Forearm` / `Hand` and `Hitbox_{L,R}_Thigh` / `Calf` / `Foot` (Limb
  zone, 12 boxes). All sizes are rough placeholders — Carlos is hand-tuning position/size/shape
  per-box now that the count and anchoring are locked in.
- Boxes over spheres was Carlos's own call, endorsed: a limb reads as a rectangle, not a sphere, so
  a box wastes less volume beyond the actual mesh and is easier to eyeball-align. No performance
  difference between `BoxCollider` and `SphereCollider` for this — a handful of raycasts a frame,
  not thousands.

**Gotcha confirmed twice in one session:** editing the prefab source (`PrefabUtility.LoadPrefabContents`
+ `SaveAsPrefabAsset`) does not reliably auto-sync already-placed scene instances for structural
component changes (destroy+add). First pass left exactly 1 of 102 stuck on stale components after a
full scene reload (the earliest-placed instance, `Enemy_Spotter_00`) and needed the same edit
applied to it directly; the second pass had zero stragglers. Not predictable which instances stick —
**always verify every placed instance after this kind of edit**, via a
`GetComponentsInChildren(typeof(EnemyHitbox), true).Length` count across all of "Enemies", not a
spot-check on one.

### 16.3 Lamp: hand → hip, plus a sway effect and a dedicated physics layer

Three changes, all in the same pass:

1. **Reparented** `Socket_Lamp` from `CC_Base_L_Hand` to `CC_Base_Hip` — Carlos's call, so the new
   `Hitbox_L_Hand` box wouldn't compete for space with it. New local offset `(0.18, -0.05, 0.10)`,
   itself still a placeholder.
2. **New `LampSwayEffect.cs`** (`Runtime/Enemies/`): tilts the lamp on local X proportional to the
   Spotter's `NavMeshAgent.velocity.magnitude` — "hanging and swinging while walking," per Carlos's
   description. Reference speed defaults to the agent's own cruising speed (3 m/s), so a normal walk
   is already full sway. Watches for a `Rigidbody` and self-disables once `EnemyDeathDrop` adds one,
   mirroring `LampFireEffect`'s existing idiom exactly (`Runtime/Enemies/LampFireEffect.cs`'s own
   `Update()`). Tunables: `SpotterLampSwayMaxAngle` (12°), `SpotterLampSwayFrequency` (1.6 Hz),
   `SpotterLampSwayReferenceSpeed` (3 m/s) — all first-guess numbers, not measured.
3. **New Physics layer `DroppedProp`** (slot 9), assigned to the lamp GameObject (persists through
   `EnemyDeathDrop`'s reparent, since layer is a GameObject property). `LampFireEffect.Awake()` now
   calls `Physics.IgnoreLayerCollision(DroppedProp, Enemy, true)` once (static guard) — this closes
   a real gap found earlier the same session: the lamp's live, non-trigger `BoxCollider` (present
   the whole time it's socketed, not just after drop) could physically clip the body/ragdoll and
   could steal a bullet raycast meant for a hitbox, since the guns' `HitMask` was `Everything`. World
   geometry (Ground, Default, Destructible, ...) is untouched, so a dropped lamp still lands and
   rolls normally — only the Enemy-layer interaction is disabled, and "clips into the body" is
   accepted as cosmetic-only per Carlos.
   Also cleared bit 9 from `HitMask` on all 4 Burntwax `ShootConfig` assets (Pistol/Revolver/
   Shotgun/Smg) so gun raycasts skip `DroppedProp` too. **These assets are gitignored**
   (`Assets/ThirdParty/**`) — local-only fix, reapply if the package is ever re-fetched.
