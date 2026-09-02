# Change log — Mr. Moonlight

Newest first. One entry per merged issue.
Structure is **BUILT / DECISIONS / FAILED / NEXT** — see `Claude Code Context MDs/kickstart.md` §B.2.

---

## MRM-34 session 2 — the fight actually works, verified live (2026-09-02)

Continues the entry below. Full record: **`Docs/mrm34-spotter-ai-build.md`** §13–14.

**BUILT**

- **`PlayerDamageReceiver`** on the Player root — the player side of `IDamageable`. Owns no health;
  routes damage into `PlayerStats.Health` through the same `Stat.Deplete` the rest of the stack
  uses, so defense modifiers, the death event and MRM-17's death sequence keep working.
- **`InvulnerableDebugToggle`** — **F4**, one key along from F3's infinite stamina and built to the
  same shape. Blocks the damage but *not* the hit: health never drops, but every absorbed shot is
  counted with a running total and a `HIT` flash on screen.
- **`MrMoonlight.Combat`** — new bottom-of-stack assembly holding `IDamageable` + `DamageInfo`,
  moved out of `Code/Runtime/Enemies/`.
- A ~15-line hook in `GunScriptableObject.PlayTrail` routing player hits into `IDamageable`, with a
  guard against damaging the shooter. **Carlos later descoped player-gun damage**; the code was left
  rather than reverted and is trivially removable (one block, marked `MRM-34:`).

**DECISIONS**

- **`IDamageable` moved to its own assembly, and it had to.** `MrMoonlight.Runtime` already
  references `Burntwax.Core`; for the gun to call `IDamageable`, `Burntwax.Core` would have needed
  to reference `MrMoonlight.Runtime` — **a circular assembly reference Unity forbids.** A third
  assembly both depend on is the only way. It was also right anyway: `IDamageable` was never
  enemy-specific, and `PlayerDamageReceiver : MrMoonlight.Enemies.IDamageable` read wrong.
- **F4 does not use `Stat.Lock`** the way F3 does. Locking health would freeze healing, item effects
  and the red-tint feedback, and would hide the hits. Gating at the damage entry point leaves the
  stat stack behaving normally underneath.
- **`PlayerDamageReceiver` goes on the Player root, not on `MrMoonlight Systems`** where
  `PlayerStats` lives — shots resolve targets with `GetComponentInParent` from the collider they
  hit, and `Body`'s parent chain reaches the root, not the Systems child.

**FOUND**

- **BUG — The Spotter could never see the player.** Blaze matches hostiles by the tag on the
  **collider's own GameObject** (`BlazeAI.cs:1441`). The player's only collider is `Body`, which was
  **`Untagged`** — the `Player` tag was on the parent, which has no collider. Every vision check
  silently skipped him. Fixed by tagging `Body`. **Standing rule: the tag has to be on the
  collider.**
- **`GetComponentInParent<T>()` skips inactive GameObjects, and a prefab asset counts as inactive.**
  Verifying wiring against the asset returns null and looks like a bug. Test the scene instance.
- **A `PlayerSettings` change made *during* play mode is reverted on exit** — which is why the first
  attempt at enabling `runInBackground` silently did not stick.
- **Play mode only ticks while the Unity editor window has focus.** MCP cannot give it focus; ask
  Carlos to click into Unity rather than burning calls rediscovering this.
- **The Spotter is not small** — measured 1.86 m against the player's 1.80 m capsule. He *reads*
  small because his head bone (1.51 m) sits just under the player's eye height (1.57 m), so you look
  slightly down at him.
- **Resizing an enemy does not require a NavMesh re-bake.** The bake depends on the baked geometry
  and the agent *type* settings, neither of which is the enemy's scale. Only a radius above the
  baked 0.5 m would warrant one. Four fields move together on a rescale — scale, capsule, agent,
  and `vision.visionPosition.y`; the last is the easy one to miss.

**VERIFIED LIVE** — watched running on the island, zero console errors

One Spotter placed 12.9 m in front of the player, then nothing scripted:

- Detection `visionMeter` → **1.00**, target `Body` — the tag fix works
- Chased 12.9 m → 5.7 m, `state = attack`, `anim = Shoot`, bursting
- **Player health 100 → 0** — the full damage pipeline works end to end
- **The flare fired on its own** after the alone-timer, and **7 reinforcements spawned**, all engaging
- **They did not stack** — closest pair 1.88 m after converging, visibly spread across the beach
- **Runaway guard held** — all 7 refused to call waves of their own
- Strafing animations cycling; each Spotter casting a lamp light pool on the sand
- Carlos's own Spotter 206 m away stayed patrolling in `normal` — unaffected
- **The flare VFX renders correctly**: burning core, sparks, rising smoke, and a strong orange light
  pool. Arcs at the configured 45°.

Screenshots: `Assets/Screenshots/mrm34_live_fight.png`, `mrm34_spotters.png`, `mrm34_flare_closeup.png`.

**FAILED**

- **Deleted a Spotter Carlos had placed himself**, via a cleanup filter that matched on
  `EnemyIdentity` rather than on the test holder this session created. Recovered in full by
  reloading the scene from disk. **Lesson: scoped cleanup matches on what the cleanup created,
  never on a component type real content also carries.**
- **Truncated `Docs/mrm34-spotter-ai-build.md`** — a Python patch script opened the file `'w'`
  (which truncates immediately) and then threw a `UnicodeEncodeError` mid-write, destroying §1–12.
  Rebuilt from source. **Lesson: never write in place; write a temp file and move it.**

**NEXT**

- Spark particles read as elongated shards rather than embers (`lengthScale 2.5` → ~1.2); flare core
  is small relative to its light (`_Intensity`, `Core` scale). Both pure numbers.
- Ten-Spotter frame cost in a build (MRM-64) — still the one unmeasured acceptance criterion.
- NavMesh agent radius/slope/step tuning (MRM-27) — still a project-settings change, not made unasked.

---

## MRM-34 — Spotter AI, combat, flare reinforcements, lamp (2026-09-01/02)

Built on `mrm-75` (deliberate branch-discipline exception, see memory `mrm75_branch_scope_exception`).
Full as-built record: **`Docs/mrm34-spotter-ai-build.md`**.

**BUILT**

- **Blaze AI Engine transferred into Mr. Moonlight** — 48 scripts to
  `Assets/_Project/Code/Vendor/Blaze AI Engine/Scripts/`, given `Blaze.Core` / `Blaze.Editor`
  asmdefs (it ships with none, so it would otherwise land in `Assembly-CSharp` and be
  unreferenceable). Demos folder (285 MB) left behind. **No layer or tag changed.**
- **Shared enemy framework** (`MrMoonlight.Enemies`) — `IDamageable`/`DamageInfo`, `EnemyHealth`,
  `EnemyHitbox`, `EnemyIdentity`, `EnemyFirearm`, `EnemyRangedAttack`, `EnemyReinforcementSpawner`,
  `EnemyDeathDrop`, `EnemyPatrolRoute`, `EnemyAudioHooks`, `IReinforcementCaller`. Reusable by
  Zealot (MRM-35) and Wolf (MRM-33); only numbers and the one unique behaviour change.
- **Spotter specifics** — `SpotterFlareCall` (alone-check sphere, timer, one flare per Spotter,
  3–10 scattered reinforcements) and `SpotterPanicCall` (low-health, 1–3, separate trigger).
- **Patrol modes with a real inspector control** — `EnemyPatrolRoute` + custom inspector:
  Idle / Random wander / Waypoints (Linear or Loop). Waypoints contribute horizontal position only;
  height is re-derived from ground + NavMesh, and unresolvable markers are named at startup.
- **Flare VFX, built from scratch** — `MrMoonlight/VFX/FlareCore` shader (additive billboard,
  three stacked radial falloffs, vertex-stage flicker), trail, smoke + spark particles, and a real
  `Light`. Prefab `Prefabs/VFX/VFX_Flare.prefab`.
- **The lamp** — `Lamp_3.fbx` imported, DIRT texture at 512 with the pixelation pass, two RetroLit
  materials cloned from `M_FlareGun.mat`, `Prefabs/World/Prop_Lamp.prefab` with a real point Light.
  Socketed on `CC_Base_L_Hand/Socket_Lamp`. **Awaiting Carlos's review gate — not logged in
  `prop-log.md` yet.**
- **`Muzzle` nulls** added to `Weapon_DoubleBarrelShotgun.prefab` and `Weapon_FlareGun.prefab`
  themselves, rotated 90° Y because both models run along local +X.
- **Island NavMesh baked** — `NavMesh Surface` bounded to Y 7.5–75 m (above Crest's sea level),
  Physics Colliders, voxel 0.2 m. **681 ms**, 75,671 triangles →
  `Assets/_Project/Scenes/Island/NavMesh-Island.asset`.
- **`EnemyDebugControls`** dev tool — Damage / Kill / Attack Player / Fire Flare / Log State from
  the component context menu. Exists because the player cannot deal damage yet (MRM-32 Backlog),
  so half of MRM-34 is otherwise untestable. Delete it once the player can shoot back.
- **~40 new tunables** in `MoonlightTunables` under three new headers.

**DECISIONS**

- **The firing rhythm is ours, not Blaze's.** Blaze paces shots as randomised windows; MRM-34 asks
  for exactly two shots then a reload. Blaze's `attackEvent` is treated as "you may open fire" and
  `EnemyRangedAttack` runs the burst as one coroutine. `CycleDuration` is written into Blaze's
  `attackDuration` at `Awake` so the two cannot drift.
- **`AttackStateBehaviour`, not `CoverShooterBehaviour`** — despite MRM-34's triage note. The cover
  shooter needs cover objects registered with `BlazeAICoverManager`, and the island has none placed;
  with no cover it degrades to a plain ranged attacker anyway. Swapping it in later changes one
  UnityEvent wiring.
- **The 30% miss is deliberate, not incompetence** — the shot fires, draws tracers and hits the
  world, just aimed wide. Offset applied once per shell (the cone stays a cone), rolled per shot
  rather than per burst.
- **The two reinforcement triggers stay separate code paths** (Carlos, explicit). Flare = proactive
  and fires when isolated; panic = reactive and fires when hurt. Folding them would couple "hurt"
  to "isolated".
- **Runaway guard on by default** — spawned reinforcements cannot call reinforcements. Ten Spotters
  each summoning ten is exponential; MRM-34's worst case is ten *total*. Not in the issue; flagged.
- **The flare VFX is built, not imported.** The Asset Store "Flare Gun" (Rokay3D) flare is
  unusable — see FOUND.
- **Blaze's `Tags&Layers.preset` deliberately not applied.** MRM-27 warns layer order is
  load-bearing; the project already has every tag and layer Blaze needs.
- **Hand-authored `.shader` rather than Shader Graph**, matching `Water.shader`'s precedent. Carlos
  preferred Shader Graph; the effect is a few radial gradients and a flicker, and the HLSL is the
  spec for converting it if hand-tweaking is ever wanted.

**FOUND** — read out of the real source and the real scene, not assumed

- **Blaze drives the existing Animator Controller by state name.** `AnimationManager.Play` calls
  `CrossFadeInFixedTime`. The hand-authored 13-state `AC_Spotter.controller` is exactly what Blaze
  wants — **the open question from the character work is closed, and none of that work was wasted.**
- **A `BlazeBehaviour` subclass REPLACES a state; it cannot run alongside one.** `RunBehaviour`
  (`BlazeAI.cs` ~2314) only ticks the behaviour in the current state slot. **This corrects the
  extension pattern written into MRM-27 and MRM-29.** Conditions watched *during* a state must be
  plain components; custom states go through `BlazeAISpareState` / `SetSpareState`.
- **`AddComponent<BlazeAI>()` leaves `vision` and `waypoints` null** — Unity's serializer
  default-constructs nested `[Serializable]` classes on *deserialize*, not on `AddComponent`.
  Building a Blaze agent from script must `new` them.
- **MRM-27's terrain-tree question is moot.** The live terrain has `treeInstances = 0` and
  `treePrototypes = 0` — there are no terrain trees. MRM-70's vegetation is real GameObjects
  carrying **5,891 CapsuleColliders**, which rasterise into the NavMesh normally. **No collider pass
  or exclusion mask needed.**
- **The island's walkable surface is 21 disconnected components**, largest covering 41% of sampled
  points. Mostly correct (beaches split by water, plateaus split by >45° slopes — "enemies cannot
  climb a cliff" is an acceptance criterion), but **an enemy on one landmass cannot reach another.**
- **The Asset Store flare is broken, not merely dated.** `flarebullet.prefab`'s particle material
  GUID `473d6d3e…` **is not in the package** (it referenced Unity's removed Standard Assets smoke
  material); its two shipped materials use built-in `Standard`, which renders magenta under URP.
- **Blaze finishes wiring itself in `Start`, not `Awake`.** Calling `SetEnemy` on the frame a
  reinforcement is instantiated is a null-reference waiting for a slow frame —
  `EnemyReinforcementSpawner` now defers engagement by one frame.

**FAILED**

- **The firing rhythm, tracers and flare-in-flight have not been watched running.** Play mode does
  not tick while the Unity editor is unfocused and MCP cannot give it focus; `Time.time` stayed at
  0.02 across calls. `PlayerSettings.runInBackground` was turned **on** (it was off) so a future
  play session ticks, but that only applies on entering play mode. Stated plainly per memory
  `feedback_confirm_before_declaring_fixed`. Everything else was verified by reading real state back
  — including 10-strong waves not stacking (closest pair 3.84 m vs a 2.5 m minimum) and the lamp
  still burning after detaching with 0.0000 m world drift.

**NEXT**

- **`Island.unity` is unsaved** — it was already dirty before this session, so Carlos's in-progress
  edits were not committed. **The `NavMesh Surface` GameObject is lost if it is not saved**; the
  baked asset survives.
- Carlos's hands-on playtest of the combat rhythm, using `EnemyDebugControls`.
- The lamp's hand position, and its normal map — both agreed placeholders.
- NavMesh agent radius/slope/step tuning (MRM-27) — a project-settings change, not made unasked.
- MRM-29's three-blind-run search; Blaze's own search stands in for now.
- Ten-Spotter frame cost in a build (MRM-34 criterion, ties to MRM-64).

## Animation tooling ruling (2026-08-31) — Retarget Pro V5 adopted, FPS Animation Baker Toolkit rejected

Not an issue — an asset decision Carlos asked for, with the package read in Playground rather than
judged from store pages. Full ruling and procedure: **`Docs/retarget-pro-strategy.md`**.

**BUILT**

- `Docs/retarget-pro-strategy.md` — the adoption ruling, the verified clip inventory, the
  bake-in-Playground/migrate-clips-only rule, a step-by-step Sonnet-facing procedure, and the
  Playground console-error diagnosis.
- `Docs/new-asset-list.md` **R4** — the ruling in the asset-triage document, plus a correction to R3.1.
- `Docs/external-assets.md` — new *Adopted — Playground-only* section; Baker Toolkit and FPS
  Animation Framework added to *Rejected*.
- `Docs/dual-project-workflow.md` — the two permanent Playground console errors, and Playground's
  new role as the animation bench.
- `Docs/00-INDEX.md` — new row, 🔴 *Read first for animation*.

**DECISIONS**

- **Retarget Pro V5 (KINEMATION) adopted — but it never enters Mr. Moonlight.** Editor bake tool;
  clips are baked in Playground and only `.anim`/`.fbx` migrate. **Zero build footprint.**
- **It is not for weapons.** It is for the **quadruped wolf** (MRM-33 — the one thing Unity's
  retargeting cannot do at all), the **Wendigo** (MRM-36 — quality on non-human proportions;
  it is a UE-mannequin Humanoid, so Unity *works*, just badly), **Tracey's full body** (MRM-9's
  unmet look-down criterion), and taking ~20 clips out of Ultimate Animation Collection's **3,068**
  without importing the library — a documented build-size risk.
- **FPS Animation Baker Toolkit rejected**, reversing `Toolkit.md`'s "clearest buy on the list".
  Primary reason is not the publisher — it is that **the job no longer exists**.
- **⛔ KINEMATION's FPS Animation Framework stays out.** Runtime system; would reopen MRM-9/12/21/22/25
  exactly as FPS Engine would.

**FOUND** — verified from the real FBXs, not remembered

- **Every FP weapon animation the demo needs is already owned.** HQ FPS Weapons 2.0 ships 19 weapons
  on one shared `FP_Arms` skeleton, including the **M1911** (8 clips) and **DoubleBarrelShotgun**
  (8 clips) — the exact weapons MRM-22 and MRM-24 name. MRM-21/22/23/24/25/43 need no animation tool.
- `FP_Arms` is `Generic`, 54 transforms, `UpperArm → Forearm → Hand` + 5×3 finger bones per side,
  `ForearmTwist.1-4` chains, and **no weapon bone** — HQ drives the weapon off `Hand.R`.
- The **Wendigo is `animationType = Human` on an Unreal mannequin skeleton** (`root/pelvis/spine_01/
  clavicle_l/...` with `ik_hand_gun`, `ik_foot_l/r`). Retarget Pro ships matching `A_TPose_UE4/UE5`.
- Owned Humanoid clip libraries: Ultimate Animation Collection **3,068**, Knife MocapAnimPack **206**,
  Cult Animations **29**.
- Retarget Pro's public docs claim *"does not support baking Humanoid rigs"* — **out of date for v5**;
  `HumanoidAnimationBaker` delegates straight to `GenericAnimationBaker`.

**FAILED / NOT A BUG**

- **The Playground console error is not Retarget Pro's.** Retarget Pro compiles clean — 0 errors,
  11 cosmetic `CS0618` Unity-6.3 deprecation warnings. The `DirectoryNotFoundException` on
  `Packages\com.waveharmonic.crest\...\Settings.Crest.iOS.hlsl` is caused by **Crest Water 5 being
  folder-copied into `Assets/PLAYGROUND/` instead of installed at `Packages/com.waveharmonic.crest/`**;
  its C# hardcodes that path in nine `[GenerateHLSL(sourcePath = ...)]` attributes. Importing any
  package triggers a recompile that re-runs the shader generator, which is why it looked new.
  The second error (main-toolbar deprecation) is HQ FPS Weapons' bundled EditorToolbox, present
  since May. **Neither affects Mr. Moonlight.**

**NEXT**

- **~30 min validation, not yet done:** bake **one** clip from Ultimate Animation Collection onto the
  Wendigo in Playground and look at it before any issue schedules work around this tool
  (`retarget-pro-strategy.md` §6.1).
- Decide the Playground Crest fix: move it to `Packages/`, or delete it. Needs Carlos.
- **Stale asset claim found:** MRM-33 says a wolf pack is *"already staged in Playground at
  `Assets/Wolf_enemu/`"* — **that folder does not exist**, and nothing matching `*wolf*` is under
  Playground's `Assets/`. Flagged on the issue; MRM-33's quadruped case cannot be tested until a
  model actually exists.

---

## MRM-70 (in progress, 2026-08-31) — Grass/ground-detail tier built; slope caps found to be choking every spawn rule

**BUILT**

- **30 `GRASS_*` detail prefabs** at `Assets/_Project/Art/VegetationPrefabs/GRASS PREFABS/` —
  20 `GRASS_TSA_*` (Unity TerrainSampleAssets) + 10 `GRASS_Gaia_*` (5 LawnGrass, 5 WildGrass).
  All verified as 1 MeshFilter / 1 MeshRenderer / 0 LODGroup / 0 Collider, which is what Unity's
  terrain detail system requires. Carlos then moved 42 further no-collider prefabs (flowers, ferns,
  mushrooms, small plants) into the same folder, so the grass tier now holds 72.
- Source art at `Art/Environment/Vegetation/GrassDetail/{TSA,Gaia}/` — 10 FBX, 12 pixelated
  textures, 10 RetroLit materials.
- `Docs/mrm70-unused-vegetation-inventory.md` — what the project owns but never spawns; identified
  the 53 first-island prefabs sitting in `Prefabs/World/Vegetation/` that the Gaia spawn never saw.
- `Docs/vegetation-distribution-brief.md` — the ChatGPT brief: player scale, Gaia placement
  mechanics, the live 78-rule config, and all 95 in-scope prefabs with measured sizes.
- `Tools/vegetation/current_spawn_setup.csv` — the live spawner configuration dumped from the scene,
  so the original numbers survive any retune.

**DECISIONS**

- **Grass is a Gaia spawner rule, not a separate tool.** `SpawnerResourceType.TerrainDetail` is
  native, and `ResourceProtoDetail` carries mesh-or-texture prototype, min/max size, density,
  coverage, colour and the same spawn criteria the tree rules use. The deleted `BiomeGrassSetup`
  (commit `f306acc`) is **not** being rebuilt. Rules survive a regeneration; hand-painted detail
  does not, so Unity's Paint Details brush is a last-step touch-up only.
- **`Gaia Pro Assets and Biomes` stays declined as a package but is now a cherry-pick source.**
  Invokes the clause already written into `terrain-vegetation-tooling-decision.md`. 14 files
  extracted by GUID, never installed, pixelated through our own pass.
- **Gaia atlases go to 1024, not our usual 512.** `PW_LawnGrass_00_D` packs 12 meshes' UV islands;
  512 would leave each around 128 px. Single-object textures stay at 512.
- **The grass tier is out of scope for the ChatGPT distribution pass** — it is a coverage/density
  problem, not per-species spawn weights, and it has no colliders.

**FAILED**

- Nothing failed, but an audit of the live spawners found three problems, all unfixed pending
  ChatGPT's pass:
  - **Every one of the 78 rules caps max slope at 5-10°.** Measured against the terrain, that
    confines each species to 10-29% of the land; 42% of the island is 10-20° hillside and is
    effectively bare. This is the likeliest cause of the island reading dull — bigger than any
    weight choice. Proposed fix (not applied): 32° for trees, 30° for dead trees, 50° for rocks,
    which takes the plantable area from ~29% to ~91% without touching the terrain.
  - **44 of the 95 in-scope prefabs are never spawned**, including the entire GraveKeepers family,
    all Curse/Heretic trees, `AP_S_Tree_01` (33.8 m) and `AP_GangshiTree_2` (34.8 m).
  - **Every species is locked at scale 1.00-1.00**, so each instance of a tree is dimensionally
    identical to every other one.

**NEXT**

- ChatGPT returns the per-biome distribution; apply it along with the slope-cap fix.
- Register the 72 grass prefabs as `TerrainDetail` rules in the 9 biome spawners.
- Remove the 5 ground plants still spawning as GameObjects in the Glade spawner — they moved to the
  detail tier.

---

## MRM-70 (in progress, 2026-08-30) — Biome vegetation distribution rebuilt from measured geometry; live terrain found to be 1024 m, not 4000 m

**BUILT**

- `Docs/mrm70-biome-distribution-measured.md` — 1,574 lines. Per-biome species distribution for all
  9 biomes (incl. Heretic Forest), derived from **measured prefab geometry** rather than estimates.
  Replaces the numbers in `Docs/Design/Island-Terrain-Reference/Vibe/GPT biome analysis.md`, which
  stays valid as art intent only.
- `Tools/vegetation/` — the generator behind it: `measure_prefabs.cs` (UnityMCP snippet measuring
  all 190 prefabs), `veg_sizes.csv`, `gen.py`, `biomes.py` (the 9 biome specs — the file to edit
  when retuning), `appendix.py`, `build.sh`. Re-running `build.sh` regenerates the document, so the
  numbers cannot drift out of sync with the assets.
- Measured all 190 vegetation prefabs: visual-mesh bounds, footprint, burial depth, visible height,
  collider blocking radius, triangle count.

**DECISIONS**

- **Size reference is the `Visual` child's renderer bounds, never colliders.** Carlos's explicit
  correction — the MRM-70 batch capsules span full mesh height including below-ground root, so
  collider height is not a size signal. Colliders are used only for blocking width.
- **Spacing is derived, not absolute: `spacing = k x footprint`.** Five density tiers
  (D/M/S/A/H) map to k multipliers. Retune tiers, never metres. Counts follow
  `instances/ha ~= 8000 / spacing^2`.
- **Density comes from stacking 4-7 strata per biome, not from crown overlap.** Forest reaches
  ~1,345 instances/ha with no layer interpenetrating itself.
- **Grass stays out of the per-biome budget** and returns to the terrain detail layer — Carlos's
  call, and confirmed correct: the old build's grass was 27 detail prototypes, not prefabs.
- **All 154 curated prefabs are in the distribution.** Verified programmatically; 8 added. Carlos
  was explicit that the curated set is curated.
- **Trees do not align to normal; rocks, logs and root formations do.** The buried tree pivots are
  what make vertical placement work on slope — they are load-bearing, not a bug.

**FAILED**

- First pass put the GraveKeepers/Curse trees on accent tier, whose 67-200 m minimum spacing
  silently overrode the density they were given — `AP_GraveKeepers_B03_2` came out at **3 instances
  in the whole of Eerie Forest**. Caught by Carlos asking whether anything had been dropped. Fixed
  (sparse tier, now 12-22 each) and the tables now carry a computed **count column** =
  min(density target, spacing ceiling) so the contradiction cannot hide again.
- First pass also used the old survey's biome areas, which summed to ~116 ha — more land than the
  island has. Absolute counts were ~2x too high. Re-anchored.

**FOUND — terrain state, unrelated to vegetation but blocking it**

- **The live terrain is `Assets/Gaia User Data/Sessions/GS-20260829 - 011148/Terrain Data/
  Terrain_0_0-20260829 - 035828.asset` — 1024 x 1024 m at origin (-512, 0, -512).** Not
  `Island_TerrainData.asset` (4000 m, essentially flat, max height 20 m) and not the
  4103 x 7085 m backup. **64.2 ha of land above sea level Y=8; max height ~60 m above sea.**
- **It has 0 terrain layers, 0 detail prototypes, 0 tree prototypes, 0 tree instances.** Terrain
  layers are the biome masks, so no biome-masked spawn can run until they are rebuilt. The previous
  8-layer / 27-detail-prototype configuration survives in
  `Island_Original_TerrainData_Backup.asset`.
- **Every biome coordinate in `mrm70-biome-vegetation-strategy.md` §3 is dead** — it refers to the
  4103 x 7085 m terrain. Landmarks and the player spawn need re-anchoring to the -512...+512 frame.
- Slope measured on the live terrain: 25% under 9 deg, 40% at 9-18, 33% over 18, steepest ~70 deg.

**NEXT**

1. Carlos defines the biome map (§6.3) — the hard blocker.
2. Rebuild terrain layers + the 27-prototype grass detail pass (biome-independent, can start now).
3. Mountain needs a terrain decision: max height is ~60 m and the tallest rock we own is 4.03 m
   visible, so "Mountain" is currently a hill with knee-high rocks.
4. 8 trip-wall props block 2.5-8.6x wider than their visible height (Appendix A) — fix at the
   prefab before scattering thousands.
5. Gaia execution mapping is written up in §10 with real `GaiaCore` field names.

## MRM-71 (created 2026-08-27, not started) — Water system: Crest Water 5

**BUILT**

Nothing in-engine. **MRM-71** created as a **sub-issue of MRM-67 (Polishing Details)**, M2
milestone, branch `mrm-71` — the first sub-issue on MRM-67, which until now held its backlog as a
checklist inside its own description. Carlos's preference going forward: real items become
sub-issues, so each gets its own identifier and branch. Documented in
`Docs/terrain-vegetation-tooling-decision.md` section 6 and `Docs/external-assets.md`.

**DECISIONS**

- **Crest Water 5** (Wave Harmonic, $240, 60.6 MB, owned) replaces IgniteCoders "Simple Water
  Shader URP". Crest 4 URP was the initial pick and was superseded the same day.
  Deciding fact: Crest's Underwater Renderer runs **between the transparent pass and
  post-processing**, so HAZE and the Retro CRT land on top of it instead of it punching an
  unfiltered hole through the frame.
- **KWS2 rejected** (also owned). Stronger simulation, but its value and its cost both live in
  photoreal flourishes that fight a PSX look.
- **CRT only on the water — no PSX treatment.** Carlos's explicit call. No full-screen CRT
  pixelation to "unify" it, no migration to `RetroLit`, **no editing Crest's shader graph** (third
  party assets stay unedited). Accepted consequence: water is the smoothest thing on screen.
- **Underwater Renderer disabled for the demo** — Tracey is being blocked from entering the water.
  Disable the component, do not delete it.
- Crest still earns its place without underwater: it **closes the near-calm/far-aggressive distance
  blend that MRM-68 explicitly descoped**, and adds shoreline foam — both surface features visible
  from land.
- **Crest 5 over Crest 4 is a logistical call, not an aesthetic one** — 60.6 MB vs 1.5 GB, UPM
  package (so `Samples~` never import and no lean extraction is needed), actively maintained
  (5.10.0 Aug 2026 vs 4.23.1 Jul 2026), Unity 6 support since 5.1.0. **Neither version is more
  PSX-friendly**; Crest 5's extra realism is opt-in switches that look like Crest 4 when left off.
  This was explicitly misread once, so it is written into the decision doc as a callout.
- **New consequence of Crest 5:** it installs to `Packages/`, which **is not gitignored**. Crest will
  therefore be committed to the repo like Flora, unlike HAZE and Retro. Whether ~65 MB of paid Asset
  Store code belongs on the GitHub remote is now a live decision (MRM-71 risk 1), not a hypothetical
  one — and it is Carlos's call.

**FAILED**

Nothing. One useful finding: `PC_RPAsset` already has `m_RequireDepthTexture: 1` /
`m_RequireOpaqueTexture: 1` / `m_OpaqueDownsampling: 1`, so **the biggest hidden cost of adopting
any water system is already being paid** by the current shader. Crest inherits it rather than adding
it.

An earlier concern that Crest might not support Unity 6 RenderGraph (the reason WaterWorks was
rejected in MRM-68) was **checked and disproved** — Crest's URP build *requires* Unity 6 and has
current RenderGraph fixes plus pass merging. Unity **6.3** specifically remains unverified.

**NEXT**

Deferred to M2 polish. Six gaps listed in the decision doc section 6; the first is renderer-feature
ordering on `PC_Renderer`, where HAZE's `postProcessEnabled` early-out is the known trap.

---

## Tooling evaluation (no issue, 2026-08-27) — Terrain/vegetation asset decisions

**BUILT**

Documentation only, no code or scene changes. `Docs/terrain-vegetation-tooling-decision.md` (new,
the decision record); `Docs/external-assets.md` gained a Gaia Pro row and an
**Evaluated and rejected** table; `Docs/00-INDEX.md` corrected — it still listed
`webgl-constraints.md` as read-first and carried two "five rules" entries that contradicted
`CLAUDE.md` (the old "stop at the scene view" handoff rule, and the WebGL rule).

**DECISIONS**

- **Gaia Pro VS — adopt, editor-time tools only.** Terraform/erosion + Spawner/biome mask stacks.
  It is the only tool evaluated that spawns onto our *existing* terrain and takes `biomes.png` as
  an Image Mask directly. **Decline its Runtime, Terrain Loader, Water, Lighting/skies and all
  sample art** — they collide with HAZE, Retro Shaders Pro, Simple Water Shader and TimeManager.
  Note the two mechanisms: art is declined at *import*; Water/Lighting/Runtime are declined at the
  *scene* level by not running the Gaia Manager's world-creation flow. Do **not** deselect Gaia's
  code folders at import.
- **Scheduled after Sept 1**, folded into the new-tree respawn (pause-doc gap #2), because adopting
  it costs a full re-verification sweep (Flora → PSX materials → tree collision → FPS) and fixes
  none of the seven open MRM-70 gaps. Erosion-only on the heightmap is the safe early bite.
- **MicroWorld — rejected.** Procedural level generator; cannot preserve the authored heightmap,
  which the demo's gameplay audio is already designed against.
- **Nature Renderer 6 Pro — rejected. Flora Renderer 6 stays.** Nature Renderer requires shaders to
  support its own procedural instancing; `RetroLit` is already BRG/DOTS-compatible, which is why
  Flora needed zero shader work. **The test for any future vegetation renderer: does it work with
  `RetroLit` unmodified?**
- **PSX / low-poly art direction is unchanged.** Gaia places, `RetroLit` renders. Confirmed
  explicitly because adopting a terrain tool could be misread as an art-direction change.
- **Water will move off Simple Water Shader** to a source not yet chosen — but not Gaia's.
- **Gaia is a temporary install, not a permanent dependency.** Import -> shape -> spawn -> save the
  recipe into `Assets/_Project/` -> strip Gaia components off the Terrain -> remove the package ->
  re-import the *same version* when changes are wanted. Works because Gaia's output is native
  `TerrainData`. Three conditions and the full cycle are in the decision doc section 2b.
- **Not installed, and deliberately not linked to any Linear issue.** Carlos's call: a separate
  triage pass will map newly-acquired Asset Store packages onto issues with per-issue setup plans.
  **Gaia is not attached to the vegetation story.**
- Written straight to `main` rather than an issue branch — architecture-level decisions, not issue
  work. Carlos's explicit call; the one-issue-one-branch rule is unaffected.

**FAILED**

One claim in the first draft of the decision doc was **wrong and has been corrected**: it said Gaia
consumes `biomes.png` *directly* as an Image Mask. It does not — our `biomes.png` is a **scene-view
screenshot, not a top-down map** (which is why it had to be hand-anchored against nine landmarks
originally). A correctly-projected orthographic mask has to be produced first; that is now a costed
prerequisite in the decision doc, not a free step.

**Seven unverified gaps are now listed in decision doc section 2c**, the highest-risk being
**whether Gaia reorders our 8 existing TerrainLayers** — layer order drives both the vegetation
spawn masks and the footstep surface mapping, so a silent reorder would break two systems at once
and the footstep break would stay invisible until someone listened for it.

Two verification notes worth keeping:

- The Asset Store product pages are JS-rendered and return only metadata to a fetcher; the useful
  specs came from publisher docs, support articles and the Unity forums instead.
- Gaia's exact folder split **cannot be verified until the `.unitypackage` is in the download
  cache.** Everything documented about its import is strategy; the file-level keep/drop list comes
  from listing the actual archive with `tar`, as done for AllSky.

**NEXT**

Carlos: Package Manager → My Assets → *Gaia Pro VS* → **Download** (not Import). Then Claude lists
the archive and produces the exact keep/drop list before anything touches `Assets/`.

---

## MRM-16 / MRM-41 / MRM-42 (in progress, joint session 2026-08-26) — Interaction system, item framework, inventory UI mechanics

**BUILT**

Three issues built together on one branch (`mrm-41`) as a deliberate exception — Carlos's call,
since the systems interlock (items need Interactable, inventory UI needs Input/movement-lock from
the player controller). Compiles clean; **nothing scene-tested, no GameObjects/prefabs/
ItemDefinition assets exist in any scene yet.**

- **MRM-16** — `Interaction/Interactable.cs` (+`InteractionType` enum), `Interaction/
  InteractionDetector.cs` (proximity+aim detection, disambiguation, shared fade, highlight via
  `MaterialPropertyBlock` emission), `UI/InteractionPromptUI.cs` (unstyled TMP placeholder, no
  icon art yet).
- **MRM-41** — `Items/ItemId.cs`, `ItemDefinition.cs` (+`ItemCategory`), `ItemEffectApplier.cs`,
  `Item.cs`, `Inventory.cs`; `UI/InventoryFeedbackUI.cs` (storage-full refusal toast);
  `PlayerStats` gained `Drunkenness`/`WeedHigh`/`MorphineHigh` stat pools (pool only, no gameplay
  consequence wired — future issue); `PlayerController` gained a read-only `Input` accessor
  (shared `InputMapController` for MRM-16/42, avoids a second bound `InputSystem_Actions`
  instance) and a reversible `SetMovementLocked()`.
- **MRM-42** — `UI/InventoryUIController.cs`, open/close/navigate/use state machine, stays in the
  `Gameplay` input map (confirmed correct per the MRM-8 comment thread), reversible movement lock,
  force-closes via the existing `HudCloseRequest` hook MRM-17 already raises on death.
- ~90 new `MoonlightTunables` fields across new Interaction/Items/Inventory UI headers.

**DECISIONS**

- One shared branch/PR for all three issues — Carlos's explicit, deliberate exception to the
  normal one-issue-one-branch rule, not a default to repeat without asking.
- Substance stat pools (Drunkenness/WeedHigh/MorphineHigh) exist now only because items need
  somewhere to add their effect to; the gameplay consequence (screen wobble, impaired sway, etc.)
  is scoped to a later issue on purpose.

**FAILED**

Nothing — no scene-testing has happened yet for any of the three systems, so no bugs have
surfaced. Don't read the lack of a FAILED section as "verified working."

**NEXT**

- **Blocked on Carlos.** MRM-41's 9-item catalogue (+9 equipment types) needs his prop model
  locations, and he's currently reconsidering the 3D asset pipeline itself before handing those
  over — a follow-up issue for that decision is pending, not yet created. MRM-42's layout is
  blocked on his mockup image (still unattached in Linear as of 2026-08-26).
- Committed 2026-08-26 (`45de5df`, "Add MRM-16/41/42 interaction, inventory, and item framework")
  on `mrm-41`. **Not yet merged to `main`** — Carlos intends to merge as a checkpoint despite the
  catalogue/scene-wiring being unfinished; MRM-16/41/42 stay open in Linear, not closed by that
  merge.
- See `Docs/mrm41-resume-2026-08-26.md` for the full state dump a fresh session would need.

---

## MRM-18 (in review, 2026-08-26) — Main menu scene (unstyled)

**BUILT**

`MainMenu.unity` scene (Build Settings index 0, `Island` now index 1), `MoonlightMixer.mixer`
built via reflection against the internal `AudioMixerController` API. Scripts: `Difficulty`,
`GameSettings`, `AudioMixerVolume`, `FadeOverlay`, `SettingsPanel`, `CreditsController`,
`MainMenuController`, `DifficultyDebugOverlay`. A `DifficultyDebugOverlay` GameObject was also
dropped into `Island.unity` so the selection is provably reaching the demo scene. Pre-menu splash
cards (`SplashSequence.cs`, studio-name then disclaimer, placeholder bracketed text) added same
day per Carlos's request.

**DECISIONS**

- Scene stays named `Island`, not renamed to "Demo" — Carlos confirmed "Demo" is just his verbal
  shorthand.
- Two stale WebGL-era callouts in the issue text resolved per the platform switch: 960×540 →
  built at 1920×1080; the "no loading percentage" WebGL constraint is moot on a Windows download
  (no loading-screen UI built at all). Quit is a real `Application.Quit()`.
- Difficulty selection is intentionally inert — persists and reaches the demo scene, but nothing
  reads it yet since no difficulty-scaling systems exist. Placeholder-by-design, not a gap.

**FAILED**

- `FadeOverlay` was the first Canvas child, which Unity UI renders *behind* later siblings — the
  opening black screen never actually covered the buttons. Fixed by making it the last sibling.
- `RunStartGame()`'s original synchronous `SceneManager.LoadScene` froze the whole app for the
  load duration regardless of the fade. Fixed with `LoadSceneAsync` + `allowSceneActivation =
  false`, activating once both the fade completes and `loadOp.progress` hits 0.9.

**NEXT**

- Merged to `main` as a checkpoint 2026-08-26 while still unfinished — **Carlos's explicit
  instruction was to leave MRM-18 open**, not close it. The merge's PR-linked automation flipped
  it to Done anyway (see the Linear workflow note below); manually reopened to **In Review** the
  same day pending Carlos's own hands-on playtest of fades/credits/click-through.
- **Root cause fixed, same day:** Linear's Team → Workflow → "Pull request automations" had **"On
  PR merge, move to... → Done"**, silently closing any issue whose branch merged — even a
  checkpoint merge of unfinished work. Carlos changed that rule to **In Review**. Going forward, an
  issue only reaches Done when Carlos explicitly says the story is finished — never inferred from
  a merge or from Linear's status alone.
- UI polish backlog is tracked on **MRM-67 "Polishing Details"**, not reopened here.

---

## Rendering stack (2026-08-25) — Flora + HAZE + Retro Shaders Pro imported and wired

**BUILT**

Three paid assets, all extracted **lean** (demo/sample content stripped): **Flora Renderer 6**
232 MB → **4.9 MB** (`Packages/com.ma.flora`), **HAZE** → **0.95 MB**, **Retro Shaders Pro** 95 MB →
**2.1 MB**. Flora ships as a bootstrapper with its real 140 MB package nested inside; that was
extracted and installed as an embedded package rather than running the in-editor installer.

- **Flora** — `Flora Scene Settings` auto-registers a `FloraTerrainProvider` on the Terrain and
  disables Unity's own tree/detail drawing. Reads existing terrain data, so **no respawn was
  needed**; all 34,816 instances stayed put. Same measurement before/after:
  **38,980 → 535 draw calls**, **70.4 M → 126 K triangles**; build ran **505 FPS / 2.0 ms**.
  **Tree collision verified intact** (28/31 raycasts blocked) — undocumented by Flora and the main
  adoption risk.
- **HAZE** — renderer feature on `PC_Renderer`, global fog volume + a density box over the playable
  slice. Light contribution on, which works because the PC renderer was already Forward+.
- **Retro Shaders Pro** — `CRTEffect` feature + `CRTSettings` override. **RGB subpixels and
  scanlines on**; point filtering, pixelation and interlacing off, per Carlos's spec.
- **FPS counter** — `Assets/_Project/Prefabs/UI/FPS Counter.prefab`, self-contained (own Canvas at
  1920×1080 reference, sorting order 32000), allocation-free `SetText`, unscaled time.

**DECISIONS**

- **Vegetation Spawner stays.** Carlos asked whether Flora could replace it. It cannot — Flora is a
  *renderer*, not a placer; its docs have no spawning/painting/scattering pages. The two stack:
  Spawner writes terrain data, Flora renders it. Also compared against Unity's built-in terrain
  brush and kept the Spawner: rule-based masking, Poisson spacing and a re-runnable seeded config
  are what make an 8-biome, 16-species island feasible at all.
- **Terrain owns its material** (`M_IslandTerrain`) instead of URP's shared package material, after
  Unity warned that edits to immutable package assets can be lost silently.

**FAILED**

- **Fog rendered in Scene view but not Game view.** Cause: `HazeRendererFeature.cs:546` returns early
  when `!cameraData.postProcessEnabled`, and the Main Camera had post-processing off. Fixed on
  `Player.prefab`, not the scene instance. Two rounds were wasted tuning density values that were
  being computed and discarded before reaching the screen.
- **First fog attempt was invisible, second was soup.** `_globalDensityMultiplier` was 0.06 on an
  effectively unbounded parameter — but raising it alone does nothing, because `_heightFogFactor` +
  `_maxFogHeight` decide *where* fog exists and were cancelling it out at player altitude.
- **`VolumeProfile.Add()` does not persist a component.** It must be registered as a sub-asset via
  `AssetDatabase.AddObjectToAsset`, or it silently comes back `null`.
- **Editor screenshots are not a verification tool here.** Play-mode captures returned an identical
  frame twice (same ms on the FPS counter) because the editor does not render while unfocused, so
  toggling fog appeared to change nothing. Same class of error as the earlier `UnityStats` A/B.
  Verification now means launching the build and capturing its window.

**NEXT**

- **PSX material migration** — `URP/Lit` → `RetroLit` for vertex snapping (the one PSX feature that
  cannot come from post-processing). Verified safe for Flora: `RetroLit.shader` includes `DOTS.hlsl`.
- **14 of 27 detail prototypes do not render under Flora** — they are `GrassType.Texture` billboards
  on Unity's non-BRG grass shader. Rebuild as single-mesh crossed quads.
- **Player spawns on the empty beach**, 137 m from the campsite and 35 m above ground (terrain 45.5 m,
  player Y 80.6). The beach biome is *designed* empty, which is why test builds look barren.
- Tree density/variety increase, pending Carlos's new low-poly models.

---

## PLATFORM CHANGE (2026-08-25) — WebGL dropped; target is now Windows 64-bit standalone at 1920×1080

**DECISIONS**

Carlos's call, made after the MRM-70 vegetation pass turned the browser ceiling from a theory into
a measurement. The island profiled at **21,946 draw calls at 19 FPS** — roughly **10× what WebGL
sustains** (~1,000-3,000). The cause is structural rather than a tuning error: Unity terrain trees
do not batch (`instancedBatchedDrawCalls = 0`), and **each tree costs 3 draw calls** because its
mesh carries three submeshes (bark / dirt / needles) on three materials.

Three WebGL-only defects had already cost most of a day — invisible terrain from the GLES3
**16-fragment-sampler** limit, mirror-finish ground, and an editor-only asmdef compiling into the
player. A Windows build removes the ceiling and all three failure modes at once.

**itch.io remains the distribution platform** — it hosts downloads natively; only the *target*
changed. **The 1 GB ceiling still applies** as itch's upload limit, but is now only download size
rather than runtime memory + load time + a graded time-to-play gate.

**BUILT**

- Target → `StandaloneWindows64`, Mono2x backend (IL2CPP deferred to the release build: it builds
  far slower and our bottleneck is draw calls, not C#).
- Display → **1920×1080 borderless fullscreen**, splash screen off. All Canvas Scaler reference
  resolutions moved 960×540 → 1920×1080 (`HUD Canvas` + the `FPS Counter` prefab).
- Quality level → `PC` / `PC_RPAsset`. Shadow distance 50 → 90 m (trees are 13-25 m tall and their
  shadows were clipping close to the player). MSAA left off — the foliage is alpha-clipped, which
  MSAA does not antialias without alpha-to-coverage.
- **Terrain normal maps (8) and mask maps (4) restored** after being stripped to survive GLES3.
  Mask `maskMapRemapMax` smoothness explicitly clamped to 0.12 and metallic to 0, so the restored
  masks cannot reintroduce the mirror-terrain bug.
- `detailObjectDistance` 40 → **80 m** — doubled, deliberately not quadrupled: grass area scales
  with the square of this, so the 160 m first tried would have been 16× the ground cover.
- Docs: `CLAUDE.md` hard rule rewritten; **`Docs/pc-build-target.md`** created and put in the
  read-first list; `Docs/webgl-constraints.md` banner-marked **HISTORICAL**.

**FAILED**

- First pass set `detailObjectDistance` to 160 m and `heightmapPixelError` to 2 without measuring —
  greedy. Reverted to 80 / 3.
- Tried to A/B measure draw calls in the editor via `UnityEditor.UnityStats`. **It is not a usable
  profiler here**: it includes Scene View rendering, and only updates when a frame actually draws,
  so toggling settings from a blocking script returns identical numbers every time. Real
  measurement belongs in a build, via the FPS counter.

**NEXT**

Flora Renderer 6 ($60, BRG/GPU-resident — reads terrain tree/detail data directly, supports
LODGroup detail meshes, **requires compute shaders so it was never possible on WebGL**) and the
tree-atlas merge, both deferred until after the Sept 1 gate. Collision behaviour with
`TerrainCollider` is undocumented and is the first thing to verify. Density and variety increase
waits on Carlos's new low-poly models; art direction stays low-poly/pixelated with existing
textures — the switch buys headroom, not a style change.

---

## MRM-70 (in progress) — 3D asset pipeline defined, 7 source packs prepped and built into real Unity assets; vegetation placement still to come

**BUILT (2026-08-24/25) — 3D asset pipeline defined**

Carlos described his process (Blender for low-poly/baking, a Substance Painter pixelation plugin
called Pixel8r). Full pipeline documented in `Docs/3d-asset-pipeline.md`: Lane A/B/C/D by source
quality, the uniform 3-texture map set (BaseColor/Normal/Mask, Mask = R metallic/G occlusion/A
smoothness), 512 BaseColor default for props (Carlos's explicit override of Retro Realism's own
256 baseline), Blender doing all baking with Painter kept as a manual escape hatch only. Pixel8r
turned out to be a Substance Designer `.sbsar` filter graph, not a Painter plugin — reimplemented
in Python instead (`Tools/pipeline/texture_pass.py`) since it has no mesh/UV dependency, just a 2D
image filter. See `Docs/mrm70_3d_pipeline` memory / the doc itself for the full DECISIONS log —
not repeated here.

**BUILT (2026-08-25) — all 7 source packs prepped**

Every pack Carlos listed went through the pipeline: RetroRealism, Big Poplar Tree Free, Grass
Flowers Free, Terrain Sample Assets, Terrain Textures Pack Free, Yughues, Terrain Demo Scene URP
(inventoried only — its trees are SpeedTree, structurally incompatible with this pipeline; its
vegetation overlaps Terrain Sample Assets, re-prepping would've been redundant). **99 folders**
under `E:\Props\Environment\Prepared Props\`, each with an `analysis.md` covering polycount vs.
budget, instancing verdict, and a wind/earthquake note. Full inventory, cross-cutting findings, and
open items in that folder's `_INVENTORY.md`. Worth remembering from this pass:

- **Check materials, not filenames, before assuming one texture = one prop.** Retro Realism's
  `Trees.tga` looked like it covered 4 trees; it actually covers 11 meshes. Terrain Sample Assets'
  20 mesh variants share only 8 materials. Verified via the actual material/shader graph each time,
  not filename pattern-matching.
- **PIL silently drops a real 4th image channel on some TIFFs** (Terrain Sample Assets' MaskMaps —
  confirmed by cross-checking with `tifffile`, which reads all channels correctly). Would have
  written flat/wrong Smoothness data across ~36 assets if not caught.
- **AO-baking a shared atlas from only some of its consumers reads the rest as fully-occluded
  black, not "unbaked."** Every combined bake in this pass white-prefills the canvas first.
- **Only `Poplar_Tree01` failed optimization assessment** (8,198–20,248 tris depending on LOD,
  10–65x the tree budget) — excluded from the "game ready" cut, everything else passed with
  varying degrees of flagging (heavier variants noted for sparse/accent placement, not base
  density).

**Game-ready cut**: `E:\Props\Environment\Game Ready\` — same 99 folders minus `Source/` and
`analysis.md`, minus the excluded Poplar tree, **86.1 MB deduplicated** (many textures are shared
atlases referenced from several per-species folders; NTFS hardlinks used instead of copies so
shared files only cost disk space once while every folder still stays self-contained).

**BUILT (2026-08-25, same day) — built into real Unity assets**

Everything from the game-ready cut turned into actual usable Unity assets, directly in this
project, via UnityMCP `execute_code` (not placed in any scene — assets only). **24 materials, 53
prefabs, 51 TerrainLayer assets.** Full breakdown and every optimization setting applied in
`Docs/mrm70-prefab-build-summary.md`. Headline decisions:

- **RetroRealism's trees/saplings/stumps/logs consolidated onto one shared material each**
  (`M_RF_Trees`), not their original 3 embedded submesh-slot materials (`Trees1`/`Dirt`/
  `BranchFir`) — confirmed those slots carried no real texture references and all sample the same
  atlas before consolidating, not assumed. Fixes the one-material-per-tree GPU-instancing rule
  flagged during asset-prep.
- **Terrain Sample Assets' 20 new prefabs reuse the pack's existing native Unity meshes**
  (`Assets/ThirdParty/TerrainSampleAssets/Models/*.asset`) with our pixelated materials swapped in
  — the pack's own original prefabs/materials (unpixelated, realistic PBR) are untouched, sitting
  alongside as a comparison reference.
- **Grass Flowers Free had no source mesh at all** (billboard texture cards only) — built as
  crossed double-quads (two 0.5×0.5m planes at 90°, feet-origin, shadows off) from Unity's built-in
  Quad primitive.
- Materials: URP/Lit, `enableInstancing = true` on all of them, Mask assigned to both
  `_MetallicGlossMap` and `_OcclusionMap` (project's established convention), Alpha Clip for all
  foliage. Textures: Point filter on BaseColor (Bilinear silently undoes the whole pixelation
  pass), Normal Map type + Bilinear on normals, linear color space on Mask. Meshes: Read/Write
  disabled, no animation/camera/light import. All prefab roots: `BatchingStatic` +
  `OccludeeStatic` (+`ReflectionProbeStatic` on real meshes).
- Verified via `read_console` (zero errors/warnings from any of this work) and by reading actual
  component/material/prefab state back after building, not just trusting the creation calls
  succeeded.

**DECISIONS**

- **Pixel8r reimplemented rather than used directly** — it's an `.sbsar` (Substance Designer), not
  a Painter plugin, and requires either the Substance Designer app or the (paid) Substance Engine
  SDK to run outside an interactive session. A Python nearest-palette-quantize + ordered-dither
  reimplementation matches its documented behavior without either dependency.
- **`execute_code`'s compiler here is CodeDom (C# 6), not Roslyn.** No local functions, no tuple
  syntax — `System.Func<>` lambdas work fine as the substitute. Cost some early failed calls before
  this was clear; worth remembering going in next time rather than re-discovering it.
- **Nothing placed in the `Island` scene, nothing painted onto the actual `Terrain`.** Deliberate —
  this issue's remaining acceptance criteria (vegetation placed + frame-rate held in a WebGL build,
  terrain layers assigned, all 7 locations still findable) are scene-view/Terrain-data work, next
  up, not bundled into this asset-creation pass.

**FAILED**

Nothing to record as a dead end — the TIFF-channel and CodeDom-vs-Roslyn issues above were caught
and worked around within this pass, not left broken.

**NEXT**

- **Vegetation placement itself** — the actual scene-view work: scattering the 53 prefabs across
  the terrain (GPU-instanced where the budget calls for it), likely via Vegetation Spawner per the
  issue's original scope, respecting the polycount flags from asset-prep (heavier variants sparse/
  accent only) and the 1500m fog/draw-distance stress-test setting currently in place.
- **Terrain layer painting** — the 51 TerrainLayer assets exist but aren't yet added to the actual
  `Terrain`'s layer list or painted anywhere; footstep-sound coverage (grass/leaves/wood/concrete)
  still needs checking against what's actually available once painted.
- **WebGL budget/frame-rate check** — a real build, once enough vegetation is down to matter.
- **Findability check** — once placed, verify the 7 locations aren't buried by density/sightlines.
- Terrain Demo Scene URP's Rocks (11 prefabs), `Red_Bush`, and its few non-overlapping ground
  textures are inventoried but unprepped — pick up later if wanted, not blocking.

---

## MRM-47 / MRM-69 (in progress) — Skybox library, SUN prefab, Time Manager

**BUILT (2026-08-24)**

- **Six skyboxes extracted directly from the AllSky - 220 Sky Skybox Set** Carlos already owns,
  without importing the full ~6 GB pack: located the cached `.unitypackage` in Unity's local
  Asset Store cache, read its internal GUID→path index, and pulled just the flat equirectangular
  source image behind each of the six named skies (`Fish Hoek Beach`, `FantasyClouds1_Low`,
  `Space_Nebula_DeepBlack`, `Night Skyglow Overcast`, `Night Skyglow heavy`, `FantasySky_Fire`) —
  found by reading each sky's own pre-authored "Equirect" material inside the pack to see which
  texture it actually referenced, rather than guessing from filenames. Copied to
  `Assets/_Project/Art/Environment/Skyboxes/` as `T_Sky_*.png`.
- **Imported as `TextureShape=Cube` + `GenerateCubemap=AutoCubemap`** (Unity auto-unwraps the flat
  panorama into a real Cubemap on import) using the project's existing `Tex_Skybox_Standard`
  preset (1024, DXT1) — this is exactly the pipeline that preset was already built for. Built one
  `M_Sky_*` material per sky on the built-in **Skybox/Cubemap** shader, which exposes `_Rotation`
  for real-time spin.
- **`SunController`, `SkyboxSwitcher`, `TimeManager`** (`Assets/_Project/Code/Runtime/World/`,
  namespace `MrMoonlight.World`). `SunController` applies a `SunState` (elevation/azimuth — not
  world position, which does nothing for a directional light — color, intensity, color
  temperature) to a Light and also owns the cabin's fast indoor dim
  (`SetIndoorDim`, inspector-boolean placeholder for a future trigger volume).
  `SkyboxSwitcher` swaps `RenderSettings.skybox` between an inspector list of materials.
  `TimeManager` owns both and switches between named presets — skybox instantly, Sun
  color/intensity/rotation lerped over a duration via a plain coroutine (DOTween is referenced in
  `Docs/csharp-conventions.md` as the project's "smooth" tool but isn't actually installed in this
  project yet — see FAILED).
- **The existing `Directional Light` GameObject was renamed to `SUN`** and given a
  `SunController`, rather than creating a new light — avoids orphaning the scene's existing
  lighting setup. All three (`SUN`, `SkyboxSwitcher`, `TimeManager`) saved as prefabs under
  `Assets/_Project/Prefabs/World/`, instances live in the `Island` scene.
- **Four example presets** on `TimeManager`: `Morning` (Fish Hoek Beach, soft near-white low sun),
  `Sunset` (Fish Hoek Beach, warm orange low sun), `Night` (Night Skyglow heavy, dim cool-blue),
  `Apocalypse` (FantasySky_Fire, red sun — ties directly into MRM-47's existing "apocalyptic red"
  requirement). **All four are placeholders for Carlos to retune** — MRM-69 only required one
  working example (`Morning`); built three more since the mechanism made it nearly free, and
  because visually verifying more than one preset live was the only way to catch the bug below.
- **Verified live in Play Mode**, not just read back: entered Play Mode, called
  `TimeManager.ApplyPreset` through each preset, screenshotted the actual rendered result. Caught
  a real mismatch this way — see FAILED.

**DECISIONS**

- **Equirect vs. cubemap, resolved as "both."** Carlos asked whether the equirect format serves
  hand-editing (Photoshop) and real-time rotation. It does, but not as Unity's `Skybox/Panoramic`
  shader — AllSky's own "Equirect" materials turned out to be `Skybox/Cubemap` materials whose
  single `_Tex` slot is fed by a flat panorama that Unity's importer auto-unwraps into a cubemap
  (`GenerateCubemap: AutoCubemap`). That gives the edit-a-flat-image and rotate-in-real-time
  properties Carlos wanted, via the cheaper, seamless cubemap render path, with **zero custom
  shader work** — and matches import presets already sitting in the project
  (`Tex_Skybox_Hero`/`Tex_Skybox_Standard`), which were already configured for exactly this
  pipeline before this session touched them.
- **Repositioning the skybox is not possible with this or any built-in Unity skybox** — the
  system has no position, only rotation. Flagged in MRM-47 as a future ask (a separate 3D
  sky-dome mesh) rather than silently promising something that can't be delivered.
- **6 imported, 4 ship.** Carlos wants 6 pulled in now for his own Photoshop compositing; the
  shipped build still holds to the original "4 skies only" WebGL budget line
  (`Docs/webgl-budget.md` §4.12/§10, unchanged). Which 4 make the final cut is open until his
  combined versions exist.
- **No "hero" (2048) skybox picked yet.** All 6 imported at the same 1024/DXT1 standard preset —
  picking a higher-res hero before the final 4 are chosen would likely be wasted work.
- **Worked on the `mrm-58` branch, not a new `mrm-47`/`mrm-69` branch** — Carlos's explicit call,
  acknowledged as a one-issue-one-branch exception rather than a default.
- **Time Manager split into its own issue, MRM-69**, related to MRM-47 and MRM-11 — Carlos's own
  instruction ("if it needs its own issue... decide what's best and least spaghetti"); it's
  testable standalone and will eventually be driven by the Event Director rather than MRM-47's
  story-beat logic directly.

**FAILED**

- **First "Morning" preset pick was visually wrong.** Picked `Night Skyglow Overcast` for
  "Morning" going purely off the name ("Overcast" ≈ cloudy). Live in Play Mode it rendered as a
  dark teal dusk sky — it's a *night*-category sky, not a daytime one. Second guess,
  `FantasyClouds1_Low`, rendered as a dramatic orange storm sky — also wrong. Settled on
  `Fish Hoek Beach` (the one real photographic HDRI among the six), which reads as a soft
  washed-out overcast morning and is the actual shipped placeholder now. **Worth remembering:**
  none of these six AllSky skies is a plain naturalistic daytime cloud sky — they're all
  stylized/dramatic — so "pick the one that sounds right" from the name alone doesn't work here;
  a live screenshot was the only way to catch this, exactly per the project's testing rule.
- **DOTween is referenced in the conventions docs as owned/installed but is not actually in the
  project** (`MrMoonlight.Runtime.asmdef` only references `Unity.InputSystem`). Used a plain
  coroutine + linear/angle lerp instead rather than adding a new package dependency mid-task
  without asking first. Not a blocker — csharp-conventions.md's "smooth is your call" explicitly
  sanctions a curve as the alternative — but worth fixing the docs' assumption, or actually
  installing DOTween, before another issue hits the same surprise.

**NEXT**

- Carlos: pick the final 4 shipped skyboxes once his Photoshop combined versions exist; retune
  the four preset Sun values/skybox pairings (all four are placeholders); wire a real trigger
  volume for `SetIndoorDim` in the cabin once MRM-61 stages it; decide MRM-60 (mine geometry) —
  MRM-47's mine-isolation notes branch on that decision.
- Whoever picks up MRM-47's dimming curve work can reuse `TimeManager.ApplyPreset`'s coroutine
  shape directly — same lerp-over-duration mechanism the story-beat dimming needs.
- Event Director hookup for `TimeManager.ApplyPreset` is explicitly out of MRM-69's scope; revisit
  once MRM-11 exists.

**ADDENDUM (2026-08-24, same day)** — Added a one-click Inspector way to test presets, since the
initial build only exposed `ApplyPreset` as a plain method with no way to trigger it by hand.
`TimeManager` now has a `Test Preset Index` field plus a right-click **"Apply Test Preset"**
context-menu action on the component header. `ApplyPreset` also now detects Edit Mode
(`Application.isPlaying`) and always applies instantly there — coroutines don't run outside Play
Mode, so the smooth lerp only ever fires during actual gameplay testing.

---

## MRM-68 (done, 2026-08-24) — Stylized, animated sea shader replacing the flat placeholder

**BUILT (2026-08-24) — swapped the hand-written shader for an Asset Store one**

Carlos didn't like the hand-written shader's look for blockout visualization ("breaking my visual
harmony") and asked for a free Asset Store water shader instead. Gave four candidates in
preference order and delegated the final technical call. Full option-by-option writeup (screenshots,
specs, why each was accepted/rejected) is in `Docs/water-shader.md`'s new "Asset Store options"
section — summary:

1. **BitGem "URP Stylized Water Shader – Proto Series"** (Carlos's #1 pick) — rejected. Store
   screenshots showed a bright cartoon toy-pool look (thick white foam, checkered tile walls) built
   for a "Cube World" low-poly prop pack — a hard tone mismatch for a 1979 Alaska horror game, no
   matter the recolor.
2. **IgniteCoders "Simple Water Shader URP"** — **chosen** (see below).
3. **GapperGames "WaterWorks"** — rejected as too risky this close to Sept 1: real screen-space
   reflections plus a custom underwater-fog `ScriptableRendererFeature` (full-screen blit passes,
   exactly what `webgl-constraints.md` §4 warns against), and the store page itself carries a
   community-pasted "Unity 6 fix" for its `WaterVolume.cs` — meaning the shipped package doesn't
   compile against Unity 6's RenderGraph pipeline without manual patching.
4. **Pedro Verpha "Procedural Water Shader"** — initially implemented (see superseded entry below),
   then replaced at Carlos's request in favor of option 2.

**IgniteCoders "Simple Water Shader URP" — what was imported.** Only three files pulled from the
purchased `.unitypackage` (extracted directly via the same `tar` GUID-index technique used for the
AllSky pack — see `allsky_asset_extraction.md`/memory — rather than a full package import, to avoid
dragging in its demo scene, Editor readme scripts, and the tiling `WaterBlock_50m` prefab/mesh
system the project doesn't use):
`Assets/ThirdParty/SimpleWaterShaderURP/WaterShader.shadergraph`, and its two normal-map textures
(`WaterSurface_atlas.tif`, `WaterSurface_single.tif`). `Assets/ThirdParty/` is gitignored project-wide,
so none of this reaches the repo regardless.
- **Two material presets kept as unedited vendor references**, per Carlos: `Water_mat_01_Dark.mat`
  (dark navy, the one now live) and `Water_mat_03_Clear.mat` (bright teal/clear, sitting unused for
  later). A third preset the package ships (`Water_mat_02`, a medium blue) was **not** kept — Carlos
  only asked for two.
- **`M_Sea.mat` now points at `Shader Graphs/WaterShader`** with `Water_mat_01_Dark`'s exact tuned
  property values copied over (Deep/Shallow color, Depth, Strength, Smoothness, Displacement,
  Normal Strength/Tiling). No scene or prefab edits needed — the `Sea` GameObject's `MeshRenderer`
  already pointed at `M_Sea.mat`, so the shader swap alone took effect. `SeaGrid.mesh` (the 64×64
  grid from the original build) is reused as-is.
- **The package's "Use Reflection (Experimental)" render-texture reflection system was left off** —
  it's a boolean toggle in the shader graph, `0` (off) in both kept materials, so the planar-reflection
  camera prefab/script/render-texture were never imported at all; the material's reflection texture
  slot is cleanly nulled rather than pointing at a missing asset.
- Verified: shader + material import with **zero console errors/warnings** (checked after every
  reimport). Live in a focused-camera Play Mode screenshot over open water: correct dark-navy body
  color, working fresnel/specular sun-glint, visible surface ripple texture.
- **The original hand-written `Water.shader`/custom `MrMoonlight/StylizedWater` files are left in
  place, untouched, just no longer referenced by `M_Sea.mat`** — not deleted, in case Carlos wants
  to return to it. See the original BUILT/DECISIONS entry below for that work.

**Superseded — Pedro Verpha "Procedural Water Shader" (2026-08-24, same session)**

Originally implemented as my own pick (technical case: no custom render feature, no reflection
camera, no SSR — just standard URP Depth/Opaque textures the project's `Web_RPAsset` already
requires project-wide, so zero new per-frame cost category). Fully wired, verified rendering
correctly and with real Gerstner wave motion in a focused Play Mode shot over deep water. Carlos
changed his mind mid-session and asked for IgniteCoders' shader instead — **fully rolled back**:
`Assets/ThirdParty/ProceduralWaterShader/` deleted, `M_Sea.mat` reverted via `git checkout` to its
pre-swap committed state before the IgniteCoders swap began. Kept in `Docs/water-shader.md`'s
options writeup since the technical analysis (WebGL risk, why it beat WaterWorks) is still useful
if this ever gets revisited.

**BUILT (2026-08-23) — original hand-written shader**

- New hand-written URP shader, `Assets/_Project/Art/Environment/Water/Water.shader`
  (`MrMoonlight/StylizedWater`), applied to the existing `M_Sea.mat`/`Sea` GameObject from
  MRM-58. Built from Simon Swartout's "Simple Water Shader" Medium article (Voronoi ripples,
  Radial Shear, Power-sharpened edges, vertex displacement) — fully procedural, **zero texture
  maps**. A second, older Gerstner-wave/normal-map article was read for inspiration on the
  calm-vs-aggressive split only; not implemented (see DECISIONS). Full technique writeup,
  parameter table, and a **Shader Graph reproduction spec** (Carlos wants the option to convert
  this to a node graph later) live in `Docs/water-shader.md` — kept out of this changelog entry
  to avoid duplicating a large reference doc.
- **Replaced `Sea`'s mesh.** The MRM-58 quad was 2 triangles by design (near-zero render cost) —
  far too few vertices for any vertex-displacement technique to show. Generated `SeaGrid.mesh`
  (64×64 grid, ~4,225 verts, still trivial GPU cost) via `execute_code` and swapped it onto the
  `Sea` GameObject's `MeshFilter`.
- **Distance-based calm→aggressive blend**: cell density, animation speed, edge sharpness, and
  swell amplitude all `lerp` between Near/Far values on a single `smoothstep` of
  distance-from-camera (XZ). One shader, one mesh — no seam between two separate materials.
- **Found and fixed two real bugs while tuning it**, both worth remembering for future
  procedural-noise shader work:
  1. A screen-space-derivative (`fwidth`) anti-aliasing pass that widened the *entire* brightness
     falloff instead of only softening the threshold crossing — washed the whole pattern out to
     solid bright at grazing angles. Fixed by clamping the AA band to a small fraction of the
     artistic edge width, applied only around the actual threshold.
  2. Swell wavelength (90m) far smaller than the new grid's own cell size (~470m at this plane's
     scale) — aliased into chaotic warped craters instead of a smooth roll. Fixed by using a much
     larger wavelength (1200m) for the geometric swell; fine ripple detail is unaffected since it
     lives entirely in the fragment shader, where mesh resolution isn't a constraint.
- Verified via a temporary camera + `manage_camera` screenshots (near-shore calm cells vs.
  distant chaotic multi-octave veins, both correctly distinct) and zero console errors after
  each shader recompile.

**DECISIONS**

- **Hand-written HLSL, not Shader Graph.** The available tooling creates shader scripts, not
  Shader Graph node graphs — hand-crafting a `.shadergraph`'s JSON directly is fragile enough to
  likely produce a broken/uneditable graph. All parameters are still exposed as material
  properties (Inspector sliders), so day-to-day tuning doesn't need code edits. Full reasoning
  and the node-by-node spec for rebuilding this as an actual Shader Graph are in
  `Docs/water-shader.md`.
- **Gerstner waves, paired normal/height maps, and depth-texture fog/refraction/foam** (all from
  the second article) deliberately deferred — heavier than a "not photorealistic" stylized target
  needs, and the depth-texture sampling in particular has a real WebGL cost. Documented as
  optional future polish in `Docs/water-shader.md`, not lost.
- **This landed on the `mrm-58` branch** (merged to `main` as a checkpoint commit, not a real
  finish — see below) — Carlos asked for it mid-session while that branch was still checked out.
  Given its own issue, **MRM-68**, now that the work exists — per the project's
  one-issue-one-branch rule this should move to its own branch before its own commit, Carlos's
  call via GitHub Desktop.
- **Paused here, deliberately.** Carlos wants to get back to MRM-58's blockout/vegetation pass
  first; this issue holds the water shader as a self-contained unit to resume whenever the next
  polish pass comes around. *(Superseded 2026-08-24 — see the BUILT entry above: `M_Sea.mat` now
  points at the IgniteCoders Asset Store shader instead, not this one. This shader and its writeup
  stay in the repo as a reference/fallback, not deleted.)*

**FAILED**

First two tuning passes on the ripple edge math (see DECISIONS/BUILT bug #1) — not a dead end,
just iteration, recorded above since the *reasoning* (don't let AA width scale the whole falloff)
is the reusable lesson, not just the fix.

**BUILT (2026-08-24, later same session) — animation confirmed, wave size tuned, scope reconciled**

- **Carlos confirmed hands-on, in a normal focused Play Mode session, that the water animates** —
  first real confirmation across either shader, closing the acceptance criterion that automated
  screenshots could never prove either way.
- **Wave size tuning knob:** Carlos found the ripple pattern too large/uniform ("like a giant
  pool"). `Normal Tiling` (material property `Vector2_4351ac2be1d74054986ec5378db9d578`) was
  already exposed as a plain Vector2 field on `M_Sea.mat`'s Inspector by the vendor shader graph —
  no code change needed. Bumped from the vendor default `(10, 10)` to `(40, 40)` as a first pass
  (verified in Play Mode — finer, more varied ripples, no tiling seams); Carlos has since kept
  tuning it himself directly in the Inspector (currently `(400, 400)`, plus his own adjustments to
  Normal Strength, Displacement, and Shallow Color).
- **Scope reconciled with the issue.** The original acceptance criteria were written for the
  hand-written shader's specific near-calm/far-aggressive distance blend — the IgniteCoders shader
  doesn't have that mechanic (uniform ripple everywhere). Rather than silently drop a stated
  requirement, flagged it to Carlos explicitly; he confirmed he's happy with the uniform look, so
  **the near/far blend is descoped to a future polish item**, not blocking this issue. Linear
  MRM-68 updated accordingly (status moved to **In Progress**, acceptance criteria rewritten,
  descope reasoning recorded in the issue itself).
- **Build #11 ("Water Shader")** made for Carlos to test the water live on itch.io/browser — the
  one remaining acceptance criterion (WebGL frame cost) needed his hands-on confirmation, not just
  a successful build.

**BUILT (2026-08-24, closing) — issue closed**

Carlos tested Build 11 live on itch.io: ~18.5 MB, performance read as acceptable in browser. That
closes the last open acceptance criterion. **MRM-68 marked Done in Linear.** All four criteria
resolved: animation confirmed, frame cost confirmed, near/far blend explicitly descoped (Carlos's
call, recorded in the issue), Shader Graph conversion moot (current shader already is one).

**NEXT — future polish, not blocking, revisit whenever**

- **Under this scene's current overcast/dusk sky, the dark-water material reads washed-out and
  pale at low grazing angles** — `Smoothness` was authored at `1` (full mirror) by the vendor;
  Carlos has already been adjusting related properties (Normal Strength, Displacement, Shallow
  Color) but Smoothness itself wasn't touched as of this writing. From a more front-on angle over
  open water it reads correctly.
- Calm-near/aggressive-far distance variation (descoped above, would need either a second material
  blended by distance or shader-graph changes to add back).
- Decide whether to ever revisit the deferred `MrMoonlight/StylizedWater` custom shader, or the
  Procedural Water Shader (Gerstner waves, also fully built and verified this session, rolled back
  at Carlos's request) — both still intact/documented as fallback options, see
  `Docs/water-shader.md`.

---

## MRM-58 (done, 2026-08-24) — Programmatic terrain block-out from Carlos's map: both islands shaped, water carved, chapel hill raised, sea horizon added; 9 location markers placed and reviewed; scope split — vegetation/texturing continues as MRM-70

**BUILT — terrain block-out (2026-08-23)**

Carlos supplied a topographic map, a grayscale heightmap, a real-world distance calibration
image, a location map, and a gameplay-area perimeter (now at
`Docs/Design/Island-Terrain-Reference/`, see that folder's README). Executed the plan from the
prior session: read the grayscale pixel-by-pixel, resampled to the terrain's heightmap grid,
pushed in with `TerrainData.SetHeights()`. Scene-view work, done via UnityMCP per standing
permission (Carlos directed this task explicitly this session — "Model in Unity using the
Terrain Shaper... you decide the dimensions"), verified by reading terrain/player state back
and by Scene View screenshots (below), documented here.

- **Scale derivation.** Measured Carlos's two calibration lines in `AANNIARVIK-scale-
  calibration-lines.png` (yellow 1137.8px = 1.72km, red 1225.9px = 1.82km) → real-world
  **~1.498 m/px**, cross-validated by both lines agreeing to within 2%. Carlos's own suggestion
  ("a map with around double those dimensions would be cool") became the final call: **3.0 m/px
  in-game** (~2.003× real scale — landed almost exactly on 2× by rounding to a clean number).
  Verified this against pacing (see DECISIONS) rather than taking the 2× suggestion on faith.
- **Elevation kept at 1:1 real meters**, not doubled with the footprint. `AANNIARVIK-height-
  scale-legend.png` gives a linear white(0m)→black(170m) grayscale mapping, confirmed by
  sampling the legend image's gradient bar directly (row 0 = 255 = 0m, row 1622 = 0 = 170m,
  linear between). Doubling the footprint while holding height constant **halves the average
  grade** — deliberately, since this is a walking/exploration game and gentler slopes read as
  more traversable at the larger scale.
- **Crop + buffer.** Source image is 1490×2258px, mostly empty sea. Cropped to the combined
  land bounding box (both islands + the small decorative islet south of the second island) plus
  a 20-40px margin: source px `x[160,1361] y[30,2225]` (1201×2195px). Added a **250m flat-sea
  buffer ring** on all four sides so the terrain doesn't cut off right at the coastline.
  Content: 3603×6585m. **Final `Terrain.size` = 4103 × 260 × 7085** (X × Y-height × Z),
  `heightmapResolution` **1025** (~4.0m/cell in X, ~6.9m/cell in Z — coarse by design, this is
  a first-pass block-out for Carlos to hand-detail, not final geometry).
- **Water carving, via two BFS passes** over the 1025×1025 grid (source pixel → grid-cell
  classified water via the same blue-channel test used for the land mask: `B > R+40 && B >
  G+20`): (1) flood-fill from every water cell on the grid's outer border classifies "open sea"
  vs. an enclosed body reachable from nowhere ("lake") — the north island's interior water
  system (the lake + narrow channel connecting the two named location clusters) came out
  correctly separated from the surrounding sea, no manual per-body tuning needed; (2)
  multi-source BFS from every water cell adjacent to land gives distance-from-shore in grid
  cells for every water cell. Depth = `min(cap, distance × rate)`, `seaLevel = 40m` (terrain-
  local): **sea** rate 3.5m/cell, cap 35m (floor at 5m); **lake** rate 2.0m/cell, cap 22m. A
  narrow strait/river only a few cells wide never reaches its cap and stays shallow; a wide lake
  does — "larger bodies of water go deeper" fell out of the distance model instead of needing
  per-body hand tuning. Also directly satisfies "stop digging past a certain distance, keep
  flat" — once distance-from-shore clears the cap threshold (~2-3 cells past the buffer's inner
  edge), the seafloor is already perfectly flat.
- **Chapel hill.** Chapel marker (`AANNIARVIK-locations.png`, black box, centroid px
  (407,422)) → world (991, 1426). Added a `Mathf.SmoothStep` boost, +25m at the chapel's
  position tapering to 0 at 150m radius, on top of whatever the source map's own grayscale
  already gave that spot (which sampled at ~90m elevation before the boost — the map already
  placed the chapel partway up high ground, matching "chapel is on a hill" in the task
  description; the boost pushes it the rest of the way to read clearly on the skyline).
- **Small-scale roughness.** Two-octave `Mathf.PerlinNoise` (±2.5m at ~50m wavelength, ±1m at
  ~12.5m), land cells only, added after elevation + chapel boost, before normalization.
- **Sea/water visualization**: `M_Sea.mat` (`Assets/_Project/Art/Environment/Water/`),
  `Universal Render Pipeline/Lit`, Transparent surface, base color `(0.04, 0.22, 0.38, 0.62)`,
  smoothness 0.65, shadows off both ways (doesn't cast, doesn't receive) — a stock URP shader,
  no custom shader code, itch.io-proven surface type. A single large flat quad (`Sea`
  GameObject, no collider — purely visual, never satisfies the player's `Ground`-layer
  ground-check, layer left at `Default`) at Y=40 (sea level), 30,000m across, centered on the
  terrain — one quad (2 tris) handles both "water fills the carved coastline/lake basins" (since
  the terrain's dug-out areas sit below Y=40) and "distant sea horizon" at effectively zero
  extra render cost. `Main Camera.farClipPlane` raised **1000 → 4000** so that horizon is
  actually visible instead of clipped — flagged in DECISIONS as a first pass, pairs well with
  distance fog later.
- **Player repositioned** to the campsite's world position (1006, ~79.8, 2704 — sampled from
  the baked terrain, not guessed), facing roughly toward the glade (next script location).
  Old position (0,0,0) was a corner of the old 1000×600×1000 terrain and would have spawned in
  open water/void on the new one. **Not** the same thing as placing the location markers
  themselves — Carlos still does that by hand, per his own note on `AANNIARVIK-locations.png`.
- **Live-verified**: entered Play mode, player settled at Y≈79.79 (matches the sampled terrain
  height, didn't fall through), zero console errors/warnings. `cc.isGrounded` read `false` at
  rest, same known-quirk already documented on `GroundCheckDistance` in `MoonlightTunables` —
  not a regression, `PlayerController`'s own SphereCast ground check is what actually matters
  and the terrain collider clearly held the player up.
- Three Scene View screenshots taken confirming shape fidelity against the source map (both
  islands + islet, correct silhouette), the carved lake basins (visible depression with banked
  walls), and the chapel hill (reads as a distinct raised dome). Saved under
  `Assets/Screenshots/` (Editor default location, not attached here).

**BUILT — orientation fix, beach smoothing, corrected player spawn (2026-08-23)**

Carlos added `Docs/Design/Island-Terrain-Reference/Map/AANNIARVIK orientation.png` (a compass
rose + "P" player-start marker overlaid on the same source map) and flagged two problems with
the block-out above: the island was mirrored north/south, and every land/water edge was a
vertical "cannon wall" instead of a beach. Full terrain regenerated from scratch via UnityMCP
`execute_code`, same permission basis as the prior pass, verified live (top-down and oblique
Scene View captures, Play Mode spawn check) rather than assumed.

- **Root cause of the flip, confirmed not guessed.** The chapel marker's centroid (found by
  scanning `AANNIARVIK-locations.png` for its black-square cluster: bbox x[403,411] y[414,429],
  centroid (407, 421.5) — matches the prior session's own (407,422) almost exactly, cross-
  validating the pixel-scan method itself) recomputes to world Z=5660.5 under a corrected
  north-up mapping. `7085 − 1426 = 5659`, matching the *old* (flipped) Z=1426 to within a
  meter. That is the signature of a classic Unity gotcha: `Texture2D.LoadImage` stores row 0 as
  the *bottom* of the image (OpenGL V=0 convention), but the old sampling code evidently read
  "row N from the top of the PNG" directly as the texture's row index without flipping it,
  mirroring the whole island north/south. Fixed by sampling
  `pixels[(srcHeight - 1 - rowFromTop) * srcWidth + col]` explicitly. Re-verified against the
  reference image with a temporary top-down orthographic camera (`TempTopDownCam`, deleted after
  use) — silhouette, compass orientation, and the lake lobe's position (west, matching the
  reference) all line up; screenshots in `Assets/Screenshots/`.
- **A second, unrelated bug found and fixed while rebuilding: a missing sea-level offset.** The
  height-scale legend's 0–170m is real-world elevation *above* sea level, but the regenerated
  land-height formula was writing that value directly as the terrain-local height instead of
  `seaLevel + elevation`. Silent effect: any pixel with real elevation under ~47m (a large
  fraction of a real coastal island) baked in *below* the local sea level (Y=40) regardless of
  the beach work below — confirmed by an interim run where 54% of all land cells came out
  underwater. Fixed by anchoring land height at `seaLevel + elevAboveSea` before boosts/
  roughness; re-verified (0.07% of land cells sit below sea level afterward, all inside the
  intentional beach taper's roughness noise, not a residual bug).
- **Beach/shoreline smoothing**, applied uniformly to every land/water boundary (sea, lake, and
  the inter-island channel alike — no per-body-type logic needed, same as the existing water
  BFS): a third multi-source BFS (mirroring the existing water-distance BFS) computes each land
  cell's distance-in-grid-cells from the nearest water cell. Within a 14-cell band (~55–95m,
  anisotropic since the grid cells aren't square at this heightmap resolution — coarse by
  design, same block-out caveat as before), height is `Lerp(seaLevel + 1m, naturalHeight,
  smoothstep(dist/14))`: a 1m beach lip at the waterline easing up to full natural terrain,
  replacing the old hard cliff. Beyond the band, terrain is untouched.
- **"Lower the island as a whole"**, per Carlos's direction, rather than just re-grading the
  coastline and having to compensate the interior afterward: land elevation *above* sea level is
  scaled **0.75×** before boosts/roughness (was 1.0× / unscaled in the original block-out).
  Combined with the sea-level-offset fix above, this gives gentler overall grades without
  sinking low-lying land — max terrain height came out at 138m (vs. budget of 260m), comfortable
  headroom.
- **Player spawn moved to the "P" marker**, not reused from the old campsite guess. Pixel-scanned
  the orientation image the same way as the chapel (single largest near-black cluster, isolated
  from the compass rose's 8 letter-clusters by size: 359px vs. 50–102px each): centroid (407.1,
  862.5) → world (991.3, 4337.5). That exact point sampled at ~26m — a real, natural low point on
  the narrow neck of land between the lake and the strait, not a bug — well under the 40m sea
  level. Rather than fight the surrounding beach grading (the nearest naturally-dry point was
  170m+ away — this whole neck is legitimately low), gave it the same treatment as the chapel: a
  small local landmark boost (`+16m within a 40m falloff radius, no beach-taper dilution inside a
  22m core pad`), the same pattern already established for the chapel hill. Final spawn height
  79.5m. **Flagged as a deliberate, explicit exception** to the plain distance-based beach
  grading — worth Carlos's eyes when he does his own location-placement pass, in case he'd rather
  move the campsite itself than rely on a boosted pad.
- Facing set to world +X (east, roughly toward the lake/interior) since the old rotation (Y=80°)
  was tuned for the flipped orientation and no longer means anything meaningful — a placeholder
  for Carlos to redo once he's placed the actual campsite dressing.
- **Live-verified**: Play Mode, player settled at Y≈79.62 (matches the sampled terrain height),
  zero console errors/warnings, same ground-check quirk noted previously (not a regression).
- **Build #10** — `E:\Builds\10 - Terrain Reshape - 2026-08-23\Build.zip`, WebGL, 11.84 MB,
  0 build errors (1 informational "1 URP assets included in build" log, not an issue). Verified
  the Unity MCP bridge was actually connected (`mcpforunity://instances`) before triggering,
  per the note below from the interrupted build last session. Zip built entry-by-entry with
  forward-slash paths and verified via `ZipFile` entry-name inspection (all 6 entries clean, no
  `\` separators) — `Compress-Archive`/`ZipFile.CreateFromDirectory` still avoided per the MRM-8
  zip-separator bug.

**BUILT (prior session)**

- `Assets/_Project/Scenes/Island.unity` — new scene, replaces the deleted `SampleScene`. Carlos
  copied over the HUD Canvas and Player prefab from prior work and added a `Terrain` object.
  Registered in Build Settings as scene 0 (Sandbox kept as scene 1).
- **Ground layer fix (scene-view, permission granted, done via UnityMCP, verified by reading the
  component back):** the new `Terrain` defaulted to layer `Default`, not `Ground` — jump silently
  never worked because `PlayerController.CheckGrounded()`'s SphereCast only tests the `Ground`
  layer (see MRM-9's own note on this requirement). Fixed: `Terrain` → layer `Ground`, scene saved.
- **Cross-issue fix, MRM-9 scope, done on this branch (flagged, see comment on MRM-9 too):**
  jump could "ram" up any slope regardless of `SlopeLimit`, since the ground-check only tested
  "is there ground below," never the surface angle — `CharacterController.slopeLimit` only
  governs the walking `Move()` resolution, so it never got a chance to reject repeated jump+land
  hops climbing a steep slope a step at a time. Fixed with sliding instead of a jump block:
  standing on ground steeper than `SlopeLimit` now slides Tracey back down it at a constant
  speed, jump itself stays available on any slope (including jumping off one back to flat
  ground). `PlayerController.CheckGrounded()` now also returns the ground hit's normal so
  `UpdateMove()` can derive the slope angle and downhill direction.
- **New tunable:** `SlideSpeed` (4 m/s default) on `MoonlightTunables`, next to `SlopeLimit`.
- **New debug-only tool, not tied to any acceptance criteria:** `DebugFlyController.cs` — an
  inspector-checkbox-toggleable free-fly/noclip mode for Carlos to visually inspect terrain shape
  and inter-location distances from the player's own POV, no collision/gravity/stamina/slope.
  Full 3D fly in the look direction, keyboard+mouse and gamepad both live simultaneously (same
  "whichever device fires" philosophy as MRM-8's `InputMapController`). Reads `Keyboard`/`Mouse`/
  `Gamepad` directly, not the shared Gameplay action map, and only ever flips
  `PlayerController`'s and `CharacterController`'s `enabled` flags — never reaches into
  `PlayerController`'s own fields, so normal movement is untouched. Same "debug tool, not
  shipped" category as `InputDebugOverlay`. Not attached to the Player prefab yet.
- Build `9 - Island Blockout - 2026-08-22` — Island + Sandbox both in Build Settings, uploaded
  for Carlos to test on itch.io.

**DECISIONS (terrain block-out, 2026-08-23)**

- **Pacing math behind the 2× scale call.** Location markers on `AANNIARVIK-locations.png`,
  converted to world meters at 3.0 m/px, give a straight-line surface route (campsite → glade →
  cabin → Flak Tower → mine entrance, then mine exit → well → chapel; the mine itself is a
  teleport, not surface distance) of **~1.96km straight-line**. Real walking paths around lake
  barriers and elevation typically run 1.3-1.6× straight-line, so **~2.5-3.1km actual overland
  route**. At `WalkSpeed` 3.0 m/s that's ~14-17 min of pure walking; blended with some
  `SprintSpeed` (5.5 m/s) use, closer to ~12-13 min. Carlos's target is a 40-50 min full
  playthrough with the mine (his own "~10 min section") carved out separately, leaving ~30-40
  min for the overland leg — pure movement at ~12-17 min of that leaves comfortable room (over
  half the budget) for the Flak Tower combat gauntlet, other encounters, backtracking, and
  "wasting a little time," per the brief. **This is what confirmed 2× rather than something
  larger** — a 4× scale would have pushed pure walking alone past 25-30 min, leaving too little
  room for combat inside the 30-40 min overland budget.
- **World orientation convention, since the source map is oriented (north = top of image) but
  Unity has no inherent compass:** in `Island.unity`, **+X = east, -Z = north** (i.e. image row
  0/north maps to terrain Z=0 at the buffer's inner edge, image column increasing = world +X).
  Worth remembering before placing anything with the source map open side-by-side.
- **Heightmap resolution 1025, not 513 (the placeholder default) or higher.** 513 was too coarse
  for a 4.1×7.1km footprint (~8m/cell); 2049 would be 4× the memory for detail this pass doesn't
  need, since Carlos hand-sculpts on top of this anyway. 1025 (~4-7m/cell) is the middle ground.
- **Terrain dimensions and the sea/lake depth constants are NOT routed through
  `MoonlightTunables`** — same reasoning as the vegetation/staging numbers below: this is
  one-time level-design geometry set by an editor bake, not a runtime-tunable gameplay constant
  scripts read every frame. `MoonlightTunables` stays reserved for values code actually consumes
  live.
- **Reference images moved to `Docs/Design/Island-Terrain-Reference/`, not `Assets/`.** They're
  inputs to a one-time editor bake (read via `File.ReadAllBytes` from an absolute path, not a
  Unity-imported asset), not runtime textures — same category as the existing screenplay/pitch/
  style docs already living under `Docs/Design/`, not the `Art/Environment/` folder
  `Docs/unity-conventions.md` reserves for actual in-game terrain textures. Renamed the 10 files
  from cryptic/spaced names (`AANNIARVIK y1,72 r1,82.png`, `LrNUdu.jpg`) to descriptive
  kebab-case/prefixed ones — see that folder's own README for the mapping.
- **Mine geometry is explicitly out of scope for this pass** — "the area of the mine is not
  represented here" per Carlos's brief, and it's still an open question in `00-INDEX.md` whether
  it's carved into the island or a teleport to a separate space. Its own ~10-minute pacing target
  is noted above for whenever that issue lands, but nothing about the mine was modeled.
- **No terrain texture layers assigned.** Bare/default terrain material, matching "we're not
  going to fill it with trees, just do the shape" — texturing and vegetation are explicitly
  Carlos's own pass (Vegetation Spawner, per the prior session's decision), not this one's job.

**DECISIONS**

- **This issue got auto-closed by Linear's GitHub integration** when its linked PR (#8) merged
  to `main`, then reopened same-session — Carlos merges to `main` as a checkpoint/safety commit
  (his own stated habit, likely to recur on other branches too), not necessarily as "this issue
  is finished." Worth remembering: a merged PR or a `Done` status in Linear doesn't reliably mean
  an issue's acceptance criteria are actually met for this project — check with Carlos rather
  than assuming.
- **Flora Instancer dropped from this issue's scope** — Carlos only owns the free Vegetation
  Spawner (Staggart Creations), not Flora Instancer/Renderer (a separate paid BRG-based rendering
  engine with unconfirmed WebGL support). Vegetation Spawner drives Unity's built-in terrain
  tree/detail instancing, which should be sufficient for this pass; revisit only if profiling
  later shows a rendering bottleneck that combination can't solve. Noted inline in the issue
  description.
- **Vegetation/staging numbers deliberately NOT routed through `MoonlightTunables` yet, at
  Carlos's explicit call.** He wants to place trees freely with the Vegetation Spawner first and
  see how a real WebGL build actually holds up, rather than have every count/radius decision go
  through a tunable up front. Revisit once a build shows a real frame-rate/budget problem — at
  that point tunable-izing the numbers becomes part of the actual fix, not a process step ahead
  of one.
- **Slope handling: slide, not a jump block.** First pass blocked jump outright on steep ground;
  Carlos found that felt wrong (still wanted to jump off a slope back to flat ground) and asked
  for sliding instead — jump-climbing steep terrain is prevented by the slide itself (you can't
  net gain height jumping onto ground that immediately pushes you back down), not by refusing the
  jump input.

**FAILED**

Nothing to record.

**NEXT**

- **Carlos:** hand-detail the block-out — the eight location placements (campsite, glade,
  cabin, Flak Tower, mine entrance, mine exit, well, chapel; final positions are his call per his
  own note on `AANNIARVIK-locations.png`), soften/vary the lake-basin walls if the carved banks
  read too geometric up close, add rocks/noise breakup Perlin alone won't give, and the actual
  vegetation/texture pass (Vegetation Spawner, still deliberately un-tunable-ized — see
  DECISIONS above). Walk the route before detailing, per the issue's own instruction — the
  Player now spawns at the campsite specifically so that's possible immediately.
- **Sanity-check the current numbers** now that they're chosen rather than requested: **map
  footprint 4103×7085m** (unchanged), land elevation now **0.75× scaled + a seaLevel(40m)
  offset** (was 1.0×/unscaled), **peak height ~138m** (was ~171m + chapel boost), beach taper
  **14 grid cells (~55–95m)** from every shoreline, player spawn-pad boost **+16m within 40m** of
  the "P" marker. If any of these feel wrong hands-on — too flat, beach band too wide/narrow,
  spawn pad too obviously a bump — all re-bakeable in one script run, not hand sculpting to redo.
- **Carlos: sanity-check the player spawn-pad boost specifically.** The "P" marker sits on a
  genuinely low neck of land between the lake and the strait (nearest naturally-dry ground was
  170m+ away), so it got a small deliberate raised-clearing boost rather than being moved. Worth
  a look during the location-placement pass in case a moved campsite reads better than a bump.
- **Sea plane and camera far-clip are a first pass**, not final. `M_Sea`'s color/alpha and the
  4000m far clip are guesses at "simple yet pretty" — pairing the far clip with distance fog for
  a softer horizon fade-out is a natural next step whenever lighting/atmosphere work starts.
- **Mine section not modeled** — still needs its own space (carved-in vs. teleport, per the open
  question in `00-INDEX.md`) and its own ~10-minute pacing pass once that issue is picked up.
- `DebugFlyController` needs adding to the Player prefab/GameObject in the Inspector (or ask
  Claude to do it via UnityMCP) before it's usable — not wired into any scene yet. Genuinely
  useful now for walking/flying the new terrain at scale.
- Watch the WebGL budget once vegetation starts going down — this is the acceptance criterion
  most likely to need a build-and-check cycle before it's done. The terrain itself (1025²
  heightmap, no texture layers yet) is cheap; the vegetation pass is where the real budget risk
  is.
- `SlideSpeed`'s constant-speed slide is flagged as a polish-pass candidate (friction/acceleration
  curve) now that real terrain shapes (including the lake-basin banks, which are steep enough to
  act as the "natural barrier" the brief asked for) exist to tune it against.

**BUILT — grounded/jump check widened to any collider, not just `Ground` layer (2026-08-24)**

- Cross-issue fix, MRM-9 scope, surfaced while Carlos was blockout-testing MRM-58's location
  markers: `PlayerController.CheckGrounded()`'s SphereCast only tested the `Ground` layer, so jump
  silently failed standing on `Camp Blockout` and would have failed on every other non-`Ground`
  prop (glade/cabin/mine markers, future obstacles) — the mirror image of the Ground-layer-missing
  bug fixed earlier in this same issue, this time the layer existed but was too narrow. Carlos's
  call: rather than requiring every blockout/prop object to be hand-tagged `Ground`, widened the
  check to hit any solid collider by default.
- New tunable `GroundCheckMask` (`LayerMask`, on `MoonlightTunables`) — defaults to Everything,
  narrowable in the Inspector later if a specific layer (e.g. a trigger volume) causes false
  positives.
- **First attempt excluded the player's own *layer* from the mask** (`~(1 << gameObject.layer)`)
  to avoid the SphereCast self-hitting the capsule. Broke immediately: `Player` sits on `Default`
  (layer 0) — same layer as `Camp Blockout` — and per `mcpforunity://project/layers` the project
  has no dedicated `Player` layer actually created (only `Default, TransparentFX, Ignore Raycast,
  Water, UI, Ground` exist; the conventions doc's `Player`/`EnemyMovement`/`EnemyHitbox`/
  `Interactable`/`VisionBlocker` rows were planned, never built). Excluding "the player's layer"
  silently zeroed out `Default`, breaking jump on the very box it was meant to fix.
- **Fixed by filtering hits by GameObject identity instead** (`Physics.SphereCastNonAlloc`, skip
  any hit whose collider belongs to `gameObject`) — correct regardless of what layer the player or
  the ground end up on, no scene/layer setup required. `Docs/unity-conventions.md`'s layers table
  updated to drop the stale "must be on `Ground`" hard requirement; `Ground` itself is now only
  load-bearing for waypoint Z-snapping.

**DECISIONS — location layout reviewed and approved as-is (2026-08-24)**

- Reviewed the 9 placed blockout markers (the 7 script locations + Flak Tower + a new Dock) against
  `AANNIARVIK-locations.png`'s pixel positions (via a pixel scan of the reference image run through
  the same pixel→world formula documented above, cross-validated against the chapel's own recorded
  world position) and against `WalkSpeed` pacing. Campsite/glade/cabin cluster sits ~100-110m south
  of the reference image's derived positions, tapering to <15m off by Flak Tower and Chapel —
  flagged to Carlos; he's fine with it as-is, no changes made.
- Straight-line campsite→chapel pacing (~2.48km, ~14 min at `WalkSpeed` alone, longest single legs
  Flak Tower↔Mine Entrance at 509m and Glade↔Cabin at 455m) confirmed acceptable by Carlos —
  vegetation will slow/distract the player further and dialogue/cutscenes add runtime on top, both
  pushing total playtime toward the target without needing the walk itself shortened. Consistent
  with the ~30-40 min overland budget already reasoned through in the 2026-08-23 DECISIONS entry
  above.
- **`Dock Blockout` is a deliberate addition**, not in the original 8-location reference image or
  the issue's list of 7. Carlos's call: a storytelling-only marker (a boat landing near the
  campsite) — no interaction planned, no gameplay function. Documented here since it isn't tracked
  anywhere else.

**WRAP-UP (2026-08-24) — issue closed, scope split**

- **Carlos finished his manual terrain polish pass** (beach/coast detailing on the areas the player
  actually explores; broader fine-tuning deliberately deferred to alongside vegetation, per his own
  call — "I'll do more fine tuning once we start placing stuff into the terrain").
- **`Terrain (original copy)` deleted.** The duplicate/safety-copy GameObject flagged earlier this
  session (both `Terrain` and the copy active, same `New Terrain.asset`, z-fighting-consistent
  visual artifact) is gone — Carlos deleted it once his polish pass was done. Verified via a scene
  hierarchy read-back (16 root objects, was 17; only `Terrain` remains).
- **Scope split.** Vegetation placement, terrain texturing (footstep terrain layers), and WebGL
  budget validation moved to a new issue, **MRM-70** ("Island vegetation + terrain texturing
  pass") — none of that scope had started, and Carlos expects it to be a heavier, iterative pass
  once real props go down, worth tracking separately from the now-complete shape/placement work.
  MRM-58 marked **Done** in Linear; closing summary posted as a comment there.
- **Acceptance criteria, final state:** island walkable end-to-end ✅ (jump-on-any-collider fix
  above, duplicate terrain removed), 7 locations marked ✅ (compass-findability unverifiable — no
  compass HUD system exists in the project yet, that's a different issue's scope when it lands),
  travel times feel right at normal walk speed ✅ (Carlos walked it, confirmed live) — the
  stretcher-speed half of that criterion is also unverifiable yet, no stretcher speed penalty
  mechanic exists in code. Terrain slopes/no invisible walls ✅ per Carlos's polish pass. Vegetation
  and footstep terrain layers moved to MRM-70, were never in scope for this close.
- **No raw-heightmap backup made.** Still an open, deferred item from earlier this session —
  `Assets/New Terrain.asset` stays gitignored pending Git LFS (per the existing `.gitignore` plan),
  so the terrain shape only exists in the working copy until that move happens or a `.raw`/`.r16`
  export stopgap is made. Not blocking this close since Carlos deferred the decision on who does
  it; worth raising again before anything riskier happens to the terrain.
- **Not committed by Claude** — Carlos commits via GitHub Desktop per standing project policy. This
  wrap-up entry, the MRM-58/MRM-70 Linear updates, and a prepared commit/PR description are ready
  for him to use when he does.

---

## MRM-17 (wrap-up) — 5 of 6 acceptance criteria confirmed, ready to commit

Carlos confirmed hands-on with screenshots (2026-08-22), on top of everything already verified
live during the build below.

**BUILT** — see the full build/decisions detail in the entry directly below; not repeated here.

**NEXT**

- **Confirmed by Carlos:** crouch stance preserved through the fall (capsule size and Speed
  1.50 m/s hold, camera moves from the crouched position) · capsule falls in a varied direction
  every time.
- **One caveat on the direction criterion, accepted as-is for now:** only the camera tilts — the
  `CharacterController` capsule and Tracey's placeholder body stay upright, since there's no
  ragdoll/animation to drive a real physical fall. Logged as a follow-up on MRM-67 (Polishing
  Details) for once a real model/rig exists.
- **Left deliberately open, not failing:** the inventory-cleanup criterion. No inventory exists
  yet (MRM-42/MRM-16 both Backlog) — Carlos's call is to leave this as an explicit open point on
  the issue rather than tick it or mark it N/A, and revisit once the inventory is built.
- 5 of 6 acceptance criteria now checked in Linear. Ready for Carlos to commit via GitHub
  Desktop.

---

## MRM-17 (in progress) — Death sequence built, verified live in Sandbox, handed off for testing

**BUILT**

- `Assets/_Project/Code/Runtime/Player/PlayerStats.cs` — edge-triggered `event Action OnDeath`,
  same pattern as `OnStaminaTired`, fired once when `Health.Value` reaches 0.
- `Assets/_Project/Code/Runtime/Player/PlayerController.cs` — `DisableControl()` (permanently
  stops `Update`, freezing crouch/camera state exactly as it was), `ResetCameraPitch()`, and a
  public `CameraPivot` accessor for MRM-17 to drive directly.
- `Assets/_Project/Code/Runtime/Player/DeathSequence.cs` — orchestrates the full sequence:
  disable control, force-close HUD, camera-tilt-and-shake fall while the red tint rises, scream,
  hold, cut to black, scream tail, game over.
- `Assets/_Project/Code/Runtime/VFX/ScreenTint.cs` + `ScreenTintRenderer.cs` — the shared
  additive red-tint mechanism. A static contribution registry (`SetRed`/`ClearRed` by source
  name), agnostic of who calls it, plus the one renderer that sums contributions, clamps to the
  new shared `MoonlightTunables.RedTintCeiling`, and draws them as a full-screen UI Image alpha.
  Built ahead of MRM-53 specifically so its future health tint can plug into the same mechanism
  instead of a parallel one — see the note left on that issue.
- `Assets/_Project/Code/Runtime/UI/HudCloseRequest.cs` — narrow no-op stub event for force-closing
  the inventory/map on death; no subscribers yet since MRM-42/MRM-16 are both Backlog.
- `Assets/_Project/Code/Runtime/UI/GameOverPanel.cs` — minimal unstyled landing stub (`Show()`
  only, no buttons). MRM-19 owns the real game over screen — see the note left there.
- `Assets/_Project/Code/Runtime/Audio/SoundPool.cs` + the empty `DeathScreamPool.asset` —
  matches the sound-pool shape already described in `Docs/unity-conventions.md`.
- Scene: `DeathSequence` + a scream `AudioSource` added to `Player.prefab`; a `HUD Canvas`
  (960×540 scaler) added to Sandbox with the red tint image, black-screen image and game-over
  stub, all wired and saved.
- `Assets/_Project/Code/Runtime/VFX/HealthRedTintSource.cs` — **MRM-53 scope, built ahead of
  schedule at Carlos's request the same day.** Sits on the Player prefab, continuously feeds
  current health into `ScreenTint` as a second contributor (`"HealthDamage"`) alongside the death
  tint, using the new `HealthRedTintCurve` tunable. See the follow-up note left on MRM-53.
- Carlos supplied `Assets/_Project/Art/HUD Textures/Veins_1.png` (a transparent veins overlay,
  CoD-damage-vignette style) for the tint's actual art. Imported under the existing `Tex_UI`
  preset from MRM-6 (Default texture type, no Sprite), so `ScreenTintRenderer` renders it via a
  `RawImage` rather than `Image` — both `Red Tint` in Sandbox and the renderer code were swapped
  accordingly.
- Death yell audio: Carlos recorded and exported 5 clips (WAV, mono, 22050 Hz, per the export
  guidance given this session). Renamed/reorganized from his working names
  (`Aud_PSFX_DeathN.wav` in `Audio/PLAYER_SOUNDS/`) to `PLR_Death_01.wav`…`PLR_Death_05.wav` in
  `Assets/_Project/Audio/Player/` — sibling to the project's existing (previously empty)
  `Audio/SFX/` and `Audio/VO/` category folders. Added a new **`PLR_` prefix** (Tracey's own
  non-dialogue vocalizations — death, pain, effort — as opposed to `VO_`'s spoken dialogue
  lines) and a matching **`Aud_PlayerVox` preset** (`Aud_VO_Dialogue`'s settings under its own
  name, so the two can diverge later without a rename), wired into the Preset Manager's
  filename-filter list via the `Preset`/`DefaultPreset` API — not a hand-edit of
  `ProjectSettings/PresetManager.asset`, which the running Editor won't pick up live. Documented
  in `Docs/unity-conventions.md` and `Docs/audio-import-workflow.md` (which also had a stale
  "ENM_/UI_ missing from the naming table" note removed — that gap had already been fixed
  separately and the note wasn't). All 5 clips assigned to `DeathScreamPool.asset`'s `Clips`
  array and live-verified (`TryGetRandomClip` returned a real clip and pitch in Play mode).

Tunables added: DeathFallDuration, DeathHoldBeforeBlackDuration, DeathRedTintCurve,
DeathCameraShakeAmplitude, DeathCameraShakeFrequency, DeathScreamTailDuration, RedTintCeiling
(shared with MRM-53), HealthRedTintCurve (MRM-53 scope, see above).

**DECISIONS**

- Fall is a camera-pivot tilt + Perlin-noise shake, not a physically simulated capsule tip-over —
  the player has no Rigidbody/ragdoll and building one felt out of scope. Flagged for Carlos; the
  amplitude/frequency/duration are already tunable without code, but a hand-authored fall
  (Cinemachine impulse/dolly, or a real Animation Clip) would replace `DeathSequence.FallAndTint()`
  — a legitimate polish-phase follow-up, not a rewrite of the sequencing. Cinemachine isn't
  installed in the project yet.
- Sound mute uses `AudioListener.pause` + `AudioSource.ignoreListenerPause` on the scream source,
  not an `AudioMixer` — none exists in the project yet, and this is a clean built-in fit.
- Red tint renders via a full-screen `RawImage` rather than a URP Volume override, since no
  post-processing profile exists in the project yet. `ScreenTintRenderer` is the only class that
  would need to change if a Volume-based approach lands later.
- `HealthRedTintCurve` defaults to the same shape as `DeathRedTintCurve` for now, per Carlos's
  request — MRM-53 should retune it independently rather than assume the two must stay identical.

**FAILED**

- `HealthRedTintCurve` ramping to 1.0 (matching `DeathRedTintCurve`'s shape) let the health tint
  alone saturate `RedTintCeiling` by the time health hit 0 — the death tint's own rise then had
  zero headroom to add anything, so death was visually indistinguishable from "already low
  health." Confirmed live: alpha pinned at the 0.85 ceiling through the whole death sequence
  regardless of the death curve's value. Fixed by capping `HealthRedTintCurve` at 0.4 instead —
  see NEXT.
- Discovered mid-fix: `MoonlightTunables.asset` hadn't been re-serialized since MRM-12, so
  several fields (including `HealthRedTintCurve`, moments after I'd just changed its C# default)
  were silently running on stale in-memory values rather than picking up the new default — a
  missing-from-YAML field only gets its C# default applied the *first* time it's ever
  instantiated in a session; after that, Unity's domain-reload snapshot carries the live value
  forward regardless of source changes. Fixed by explicitly setting the field on the loaded
  asset and calling `AssetDatabase.SaveAssets()`, which re-serialized every field's current value
  into the `.asset` file — worth remembering next time a tunable's default needs to change after
  it's already been touched once in a running Editor session.

**NEXT**

- Live-verified in Play mode (Health drained to 0 via code): control disabled, tint reached
  ceiling, instant cut to black, `AudioListener.pause` engaged, game over panel activated, no way
  to regain control. Compiles clean, zero console errors.
- `HealthRedTintSource`'s math verified directly (0.784 contribution at 30/100 health, matching
  the curve) — the live on-screen alpha update in Play mode couldn't be watched frame-by-frame in
  this session because the Editor runs heavily throttled while unfocused during MCP-driven
  testing (2 engine frames in ~2 minutes of wall time). Not a code issue; worth a normal
  hands-on look once Carlos is driving it directly.
- **Not yet verified by Carlos hands-on** — acceptance criteria intentionally left unticked in
  Linear pending his test pass and his call on the fall/game-over decisions above.
- ~~Death scream pool is empty~~ — filled, and **Carlos confirmed the yells play correctly**
  in-game. (An earlier note here claimed the clips were silent, based on `AudioClip.GetData()`
  reading peak/RMS 0.0 on all 5 — that reading was a false negative, not a real problem;
  correcting the record rather than leaving it stand. Likely `GetData()` behaving oddly on a
  Compressed-In-Memory Vorbis clip in-editor rather than anything wrong with the files
  themselves. Trust Carlos's hands-on ear over that specific diagnostic next time.)
- **Death tint still wasn't visible after the HealthRedTintCurve fix above — second, deeper bug,
  now actually fixed.** Carlos confirmed live: the veins texture intensified correctly, but the
  centre of the screen stayed sky-blue right through death — no red wash at all. Root cause:
  `Veins_1.png` is an edge-concentrated damage-vignette texture with a **transparent centre** —
  no amount of alpha on that texture alone can ever redden the middle of the screen, so the
  "extremely red before the blackout" the issue calls for was structurally impossible with a
  single textured layer, no matter how the curves were tuned. Fixed by giving
  `ScreenTintRenderer` a second layer: a flat, full-screen solid-red `Image` (`Red Tint Flat`,
  Sandbox) sitting *behind* the veins `RawImage`, both driven by the same computed alpha. Verified
  with an actual in-game screenshot at death (Health 0, Death tint at 1) — the screen genuinely
  floods red edge-to-edge, veins visible as detail on top. This is the fix that should have
  shipped the first time; the single-texture approach couldn't have worked as specified.
- **Third round: Carlos caught a visible border/corner where the tint didn't reach**, screenshot
  from the actual Game view at 16:9. Measured it precisely with `RectTransform.GetWorldCorners`
  rather than guessing: `Red Tint`, `Red Tint Flat`, `Black Screen` and `Game Over Panel` all had
  a **leftover non-1.0 `localScale`** (0.96–1.10 depending on the object) baked in from however
  each was created via the MCP bridge, before their stretch anchors were applied - the anchors
  and offsets were correct, but the scale was silently shrinking/growing the rect around the
  screen centre on top of that, leaving (or overshooting) a border. Also confirmed in the process
  that `offsetMin`/`offsetMax` set via the MCP `manage_components` tool's SerializedProperty path
  don't reliably take (those are computed C# properties on `RectTransform`, not raw serialized
  fields) - the actual fix had to go through real C# (`execute_code`) setting `anchorMin`,
  `anchorMax`, `offsetMin`, `offsetMax` **and `localScale = Vector3.one`** together. Also re-learned
  the hard way that Play Mode edits don't persist - the first pass of this exact fix was made
  while in Play Mode and silently reverted the moment Play Mode stopped; redone in Edit Mode and
  confirmed via a fresh Play session afterward. All four overlays now measure exactly
  `(0,0)`-`(Screen.width,Screen.height)`, verified both numerically and with an in-game
  screenshot showing full edge-to-edge red with no border.

---

## MRM-12 (wrap-up) — All acceptance criteria confirmed, ready to commit

**BUILT**

- Confirmed live in Sandbox: Health drops on contact with `HealthTest`; Melee/Defense modifiers
  visibly stack on the debug HUD (F2) walking into `MeleeTest`/`DefenseTest`; Audio Pitch glides
  audibly rather than snapping walking into `PitchTest`; stamina drains on the curve while
  sprinting and regenerates after the delay; jump/sprint both refuse at 0 stamina.
  `StatModifierStackingTests` — 5/5 passing. Fresh recompile, zero console warnings.
- All six MRM-12 acceptance criteria ticked in Linear.

**NEXT**

- Ready to commit and merge to `main`. See commit proposal in this session's conversation.
- **Recommended next issue: MRM-11** (Event Director) — the project's own stated biggest
  blocker, unblocks MRM-62 and the narrative critical path. Opus-scoped for the format/
  architecture decision; start it in a fresh session rather than carrying this one's context
  over, per the model-discipline rule in `kickstart.md` §B.5.
- **If staying in Sonnet right now instead: MRM-17** (Death sequence) — small, unblocked,
  consumes the `Health` stat this issue just built.

---

## MRM-12 (in progress) — Core stat framework built, handed off for scene wiring

**BUILT**

- `Assets/_Project/Code/Runtime/Player/StatModifier.cs` — `StatModifierType` (Additive /
  Multiplicative) and the `StatModifier` struct (source, type, value).
- `Assets/_Project/Code/Runtime/Player/Stat.cs` — the generic modifier-stacked stat. One class
  covers all six stats, per the issue's Model note. Formula: `Value = (BaseValue + sum of
  additive) * (product of multiplicative)`, documented on the class with a worked example.
  Also owns `Deplete`/`Restore` (direct pool mutation, for damage/regen) and `Lock`/`Unlock`
  (§11.6 — pins `Value`, bypasses the modifier stack, until unlocked).
- `Assets/_Project/Code/Runtime/Player/PlayerStats.cs` — owns the six `Stat` instances (Health,
  Stamina, Speed, MeleeDamage, Defense, AudioPitch). Subscribes to `PlayerController.OnJumped`
  and `OnSprinting` (MRM-9's hooks) for jump/sprint stamina costs. Stamina drains on
  `StaminaDrainCurve` (curve input: fraction of stamina remaining) while sprinting, regenerates
  at a flat rate after `StaminaRegenDelayAfterSprint` seconds of not sprinting. `OnStaminaTired`
  / `OnStaminaHyperventilating` fire edge-triggered at the 50%/20% thresholds — empty
  subscriber lists, ready for Carlos's breathing sound pools. `CurrentAudioPitch` chases the
  modifier-stack target via `Mathf.MoveTowards` at `AudioPitchTransitionSpeed`, so pitch never
  jumps instantly. `ConsumeSwingStamina()` is public and unused, ready for the Pickaxe issue.
- `Assets/_Project/Code/Runtime/Player/PlayerStatsDebugOverlay.cs` — same on-screen-overlay
  shape as MRM-8's `InputDebugOverlay`, hidden by default (`visible = false`), toggled with F2 /
  gamepad Start. Positioned below `InputDebugOverlay`'s rect so both can be on at once in
  Sandbox.
- `MoonlightTunables.cs` — new `Player Stats — MRM-12` header: `MaxHealth`, `MaxStamina`,
  `StaminaDrainCurve`, `StaminaRegenRate`, `StaminaRegenDelayAfterSprint`, `JumpStaminaCost`,
  `SwingStaminaCost`, `StaminaTiredThreshold` (50), `StaminaHyperventilateThreshold` (20),
  `BaseMeleeMultiplier` (1.0), `BaseDefenseMultiplier` (1.0), `AudioPitchTransitionSpeed`.
- `Assets/_Project/Code/Tests/EditMode/StatModifierStackingTests.cs` (new
  `MrMoonlight.Tests.EditMode` asmdef, EditMode/NUnit) — 5 tests proving the stacking rule: an
  additive + multiplicative modifier applied simultaneously produce the documented result,
  removing one source's modifier leaves the other intact, two multiplicative modifiers multiply
  their factors (not sum their percentages), values clamp to `MaxValue` even when modifiers would
  exceed it, and `Lock` bypasses modifiers until `Unlock`. All 5 pass (`MrMoonlight.Tests.EditMode`
  run via the Unity Test Runner).

**DECISIONS**

- **Speed unified with `PlayerController`, per Carlos's explicit call after reviewing the
  tradeoff.** `PlayerController` now optionally links to a sibling `PlayerStats`: it writes its
  computed base speed (walk/sprint/crouch) into `Speed.BaseValue` every frame and moves at
  `Speed.Value` (the modifier-stacked result) instead of its own raw number, so boots/weapon/
  substance modifiers actually affect movement. The link is optional (null-checked, falls back to
  MRM-9's original behaviour) so prefabs without `PlayerStats` are unaffected.
- **Same pass also gates jump and sprint on stamina**, at Carlos's request: at exactly 0 stamina,
  jump is refused and sprint silently falls back to a walk, instead of firing for free. Melee is
  not gated — no attack action exists yet to gate (Pickaxe issue's job).
- Modifier stacking rule documented on `Stat`'s class doc comment (canonical, single source) per
  the issue's "document the stacking rule" requirement, rather than a separate docs file — same
  reasoning as the tunables override pattern being documented once on `MoonlightTunables.cs`.
- Health/Stamina reuse the same `Stat` class as Melee/Defense/Speed/Pitch rather than a separate
  "pool" type — `Deplete`/`Restore` mutate `BaseValue` directly, while the modifier list stays
  available for future capacity/rate modifiers (a "difficulty" max-health boost, say), keeping
  one mechanism for all six per the Model note instead of two.
- Event director's `stat` verb (`op=lock`/`op=unlock`, MRM-11) isn't built yet, so there's no
  string-keyed dispatch — `Lock`/`Unlock` live directly on each `Stat` instance
  (`playerStats.Stamina.Lock(...)`). MRM-11 calls these directly when it exists; no premature
  abstraction added ahead of that consumer.

**FAILED**

- `create_script` rejected the first version of `Stat.cs` with a false-positive "duplicate
  method signature" on an expression-bodied `Value` property that called a private helper method
  of the same name pattern. Fixed by inlining the computation directly into `Value`'s getter
  instead of delegating to a separate method — not a real duplicate, just something about that
  shape the script validator didn't like.

**DECISIONS (cont.)**

- **Scene-view exception, at Carlos's explicit request:** he'd already created `HealthTest`,
  `MeleeTest`, `DefenseTest`, `PitchTest` GameObjects (each with a visualization Cube child) and
  asked Claude to attach the debug scripts directly rather than hand off instructions — normally
  `CLAUDE.md`'s "stop at the scene view" rule. Done via the UnityMCP bridge, verified by reading
  the components back afterward:
  - `HealthTest/Cube` — its existing `BoxCollider` set to `isTrigger = true`, plus
    `StatDebugPoolZone` (Target Pool: Health, Restores: false, Amount Per Hit: 10, Re-Trigger
    Delay: 1s).
  - `MeleeTest` — `StatDebugModifierToggle` (Target Stat: Melee Damage, Additive, +0.5, key F3).
  - `DefenseTest` — `StatDebugModifierToggle` (Target Stat: Defense, Additive, +0.5, key F4).
  - `PitchTest` — new `AudioSource` (Loop + Play On Awake on, **no clip assigned — none exists in
    the project yet**), `StatDebugAudioPitchTest`, and `StatDebugModifierToggle` (Target Stat:
    Audio Pitch, Multiplicative, ×1.3, key F5).
  - This surfaced a real bug fixed alongside it: `StatDebugModifierToggle` and
    `StatDebugAudioPitchTest` originally had `[RequireComponent(typeof(PlayerStats))]`, assuming
    they'd live on the `Player` GameObject. On a standalone object that would have cascaded into
    Unity auto-adding a duplicate `PlayerStats` → `PlayerController` → `CharacterController`
    stack. Fixed by dropping that attribute and having both find the scene's one `PlayerStats`
    via `FindFirstObjectByType` at `Awake` (Awake-time lookup, not per-frame, per
    `Docs/csharp-conventions.md`) when the serialized field is left blank.
  - Left the scene **unsaved** — Carlos is repositioning the Cubes next and will save when done.

**DECISIONS (cont. 2)**

- **Two more scene-view fixes, at Carlos's explicit request** (per the updated §B.3 policy —
  offer, get permission, act via UnityMCP, verify by reading state back):
  - **`PitchTest`'s `AudioSource` was inaudibly flat regardless of distance** — its `Spatial
    Blend` defaulted to `0` (2D) when added, which ignores listener distance entirely; the
    clip's importer being "3D-capable" doesn't set that. Fixed: `spatialBlend = 1`,
    `minDistance = 1.5`, `maxDistance = 8` — calibrated against the actual Sandbox `Plane`
    (20×20, scale 2 on the default 10×10 primitive) and `PitchTest`'s position (9 units from
    the Player's spawn), so it's inaudible at spawn and clear on approach rather than an
    arbitrary guess.
  - **`StatDebugModifierToggle` (keypress-based) replaced with `StatDebugModifierZone`
    (trigger-based),** at Carlos's request for consistency with `HealthTest`'s walk-in
    interaction model — deleted the old script (after removing its components from the scene
    first, to avoid a missing-script reference) and added the new one, which applies its
    modifier `OnTriggerEnter` and removes it `OnTriggerExit`, same shape as
    `StatDebugPoolZone`. Moved from living on the `MeleeTest`/`DefenseTest`/`PitchTest` parent
    objects onto their `Cube` children (set to `Is Trigger`), since that's where the collider
    actually is. Settings preserved from the keypress version: Melee +0.5 additive, Defense
    +0.5 additive, Pitch ×1.3 multiplicative.

**NEXT**

- **Carlos:** `PlayerStats`/`PlayerStatsDebugOverlay` are already on `Player`, and the four debug
  test objects are wired (see DECISIONS above). Remaining: reposition the Cubes, assign a looping
  test clip to `PitchTest`'s `AudioSource`, save the scene, then confirm live against the
  acceptance criteria — Tracey's movement speed comes from `PlayerStats.Speed`, jump/sprint refuse
  at 0 stamina, sprint falls back to a walk, stamina drains/regenerates correctly, jumping costs
  stamina, F2 toggles the debug HUD, pitch transitions are smooth (F5 on `PitchTest`), and F3/F4
  visibly stack on Melee/Defense via the HUD.
- Unblocks: any future item that reads `PlayerStats.MeleeDamage`/`.Defense` in a damage
  calculation, MRM-11's `stat` verb (lock/unlock), the Pickaxe issue (`ConsumeSwingStamina`),
  Carlos's breathing sound pools on `OnStaminaTired`/`OnStaminaHyperventilating`.

---

## MRM-8 (follow-up) — Look X-axis was silently inverted

**BUILT**

- `InputSystem_Actions.inputactions`: the `Look` action's processor changed from
  `invertVector2(invertY=false)` to `invertVector2(invertX=false,invertY=false)`.

**DECISIONS**

- **Root cause, confirmed not guessed:** `UnityEngine.InputSystem.Processors.InvertVector2Processor`
  defaults **both** `invertX` and `invertY` to `true` (verified live by instantiating one via
  `execute_code`). MRM-8's binding only explicitly set `invertY=false` — to make Y toggleable via
  the `InvertYAxis` tunable — never touching `invertX`, which silently stayed at its inverted
  default. Symptom: moving the stick or mouse right turned the camera left. Caught by Carlos
  during MRM-9 look testing.
- Done inline on MRM-9's branch, same deliberate exception as the mouse-scroll and sprint/jump
  fixes earlier this session — not re-confirmed individually since the pattern was already agreed.

**FAILED**

Nothing to record.

**NEXT**

- **Carlos:** confirm stick/mouse right now turns the camera right in a live test.

---

## MRM-9 (wrap-up) — All acceptance criteria confirmed, ready to commit

**BUILT**

- Confirmed live on itch.io (build `7 - Player Controller`): move, look, jump, crouch (toggle,
  smooth transition), sprint, jump-blocked-while-crouched, look-down shows placeholder body, all
  working in an actual WebGL build. Every tunable reads `Tunables.I` live each frame by
  construction (no caching), covering the "changes take effect in play mode" criterion by design.
- `Player.prefab` and `Controller UI Test.prefab` — Carlos saved both so they're reusable across
  scenes, not just live in `Sandbox`.
- All six MRM-9 acceptance criteria ticked in Linear.

**DECISIONS**

- **Incidental, unexplained diff worth flagging for the commit:** `ProjectSettings.asset`'s
  `preloadedAssets` list dropped its one entry (the Input System actions asset) to empty at some
  point during this session's build/testing cycle. Not an intentional change, cause not
  identified — likely a Unity-internal side effect of the build or a settings save. Everything
  tested fine regardless (WebGL build works, input capture works), but flagged rather than
  silently included in the commit.

**NEXT**

- Ready to commit and merge to `main`. See commit proposal in this session's conversation.
- **Recommended next issue: MRM-12** (core stat framework) — fully unblocked, Sonnet-scoped,
  doesn't need terrain/environment, and plugs directly into the `OnJumped`/`OnSprinting` hooks
  this issue already exposed.

---

## MRM-9 (in progress) — Sprint-backward and stationary-jump bugs found in testing

**BUILT**

- **Sprint now requires a forward move component** (`moveInput.y > 0f`), not just any movement.
  Holding Sprint while backing up or pure-strafing now walks instead. Flagged by Carlos: sprint
  backwards felt wrong.
- **Jump/landing no longer trusts `CharacterController.isGrounded`.** Confirmed live via the
  Unity MCP bridge: dropped the player onto the flat `Sandbox` plane, let it settle, and
  `isGrounded` read `false` continuously while completely at rest (not a one-frame flicker —
  sampled twice, ~11 seconds apart, both `false`). That's why jump only worked while moving:
  continuous horizontal collision resolution was masking the same underlying flakiness. Replaced
  with `PlayerController.CheckGrounded()` — a short downward `SphereCast` against a new `Ground`
  physics layer — used for both the vertical-velocity-reset check and jump eligibility. Confirmed
  fixed the same way: same drop-and-settle test, `CheckGrounded()` now reads `true` at rest.
- New tunable `GroundCheckDistance` (0.2, not in MRM-9's original list — added because the
  acceptance criterion "jump works" can't be met without a reliable grounded check).
- **New `Ground` physics layer created** (was documented in `Docs/unity-conventions.md` but never
  actually created in the project). Assigned to the `Sandbox` scene's test `Plane`. **This is now
  a hard requirement for every floor/terrain surface**, including MRM-58's terrain blockout — see
  the conventions doc's updated layers table.

**DECISIONS**

- **Fixed the ground check at the engine-reliability level (SphereCast), not by adding a fudge
  factor to the existing `isGrounded` reads.** `CharacterController.isGrounded` is a documented
  Unity flakiness point, not something a tunable epsilon can paper over reliably.

**FAILED**

First attempt at verifying the fix via `EditorApplication.update` polling registered from within
a single `execute_code` call showed frozen position across 114 logged "frames" — misleading, not
an actual repro. The callback fired in a rapid synchronous burst rather than at real per-frame
intervals, so the real Play Mode loop (and `PlayerController.Update()`) never got a chance to
interleave. Switched to plain sequential position/state checks with real wall-clock waits between
separate tool calls instead, which reflected genuine elapsed game time.

**NEXT**

- **Carlos:** re-test sprint (forward only), jump-while-stationary, and jump-while-crouched-blocked
  in the Sandbox scene to confirm before the next WebGL build.
- **Whoever builds MRM-58's terrain blockout must assign it to the `Ground` layer** or the player
  won't be able to jump on it. Flagged in `Docs/unity-conventions.md` and as a comment on MRM-58
  itself.
- Rest of MRM-9's acceptance criteria (WebGL build test, live-tunable spot check) still open —
  see MRM-9's own comments.

---

## MRM-8 (follow-up) — Mouse scroll wheel added to the input debug overlay

**BUILT**

- `InputDebugOverlay.cs` now also reports mouse scroll. `Mouse.scroll` is a `Vector2Control`,
  not a `ButtonControl`, so it never fired the existing `InputSystem.onAnyButtonPress` listener
  — that's why only key/button presses showed before. Added a `CheckMouseScroll()` poll in
  `Update()`: `Mouse.current.scroll.y` reports the frame's scroll delta directly (0 when idle,
  no `wasPressedThisFrame`-equivalent needed), reported through the same `FindBoundActions`
  lookup and `_lastPress` display path as button presses.

**DECISIONS**

- **Done inline on MRM-9's branch, not a new issue or MRM-8's own branch.** This is MRM-8-owned
  code (already `Done` in Linear) touched while testing MRM-9 — a deliberate, explicit exception
  to the project's one-issue-one-branch rule, confirmed with Carlos rather than assumed. Recorded
  here and as comments on both MRM-8 and MRM-9 for traceability.

**FAILED**

Nothing to record.

**NEXT**

Nothing outstanding — this was a small, complete addition.

---

## MRM-8 (wrap-up) — Two real bugs found chasing a debug overlay that "wasn't showing"

**BUILT**

- Confirmed, live on itch.io: the 960×540 embed (see below), the `InputDebugOverlay` (MRM-8),
  keyboard and gamepad capture, and action-name lookup all work correctly together. Console:
  `[InputDebugOverlay] Started`, `Gamepad added 1`, zero errors.
- **MRM-66** — a new issue capturing the full checklist of everywhere the target resolution
  number lives (Player Settings, itch.io embed, docs, every future UI Canvas Scaler, the build
  and zip steps), so a future swap to 1280×720 or anything else doesn't require re-discovering
  any of what follows.

**DECISIONS / FAILED — the two real bugs, for the record**

What looked like one mystery ("the debug overlay never shows up in the browser, no matter what
Unity setting we change") was actually two unrelated bugs stacked on top of each other. Neither
was a Unity or WebGL Template problem, despite that being the leading theory for most of the
session:

1. **A stale itch.io upload flag.** The very first build ever uploaded to this project — from
   before any of today's work — stayed flagged "This file will be played in the browser" through
   every subsequent re-upload. Every test that session was silently re-running that original
   build; newer uploads just sat there unused. Confirmed by noticing the served file's content
   hash never changed across builds with genuinely different settings. **Fix:** delete stale
   uploads rather than leaving them, and always confirm the *newest* file is the one flagged —
   checking that flag on an already-uploaded file after the fact doesn't reliably force itch to
   re-process it as HTML; a fresh upload with the flag set from the start is safer.
2. **A zip tool writing invalid path separators.** PowerShell's `Compress-Archive` — and, on
   this machine, even .NET's `ZipFile.CreateFromDirectory` — wrote literal backslash (`\`) path
   separators into nested zip entries (e.g. `Build\...loader.js`) instead of the forward slash
   (`/`) the ZIP spec requires. Windows tools don't care; itch's Linux servers extract a single
   garbled flat filename instead of a real subfolder, so `Build/...loader.js` 404'd while
   top-level files like `index.html` loaded fine. **Diagnosed by inspecting `.FullName` on the
   zip's entries — which was itself misleading (it can normalize for display); the real proof
   came from reading the zip's raw bytes and searching for the literal separator character.**
   **Fix:** never use `Compress-Archive`. Build the zip entry-by-entry, explicitly replacing `\`
   with `/` in each relative path before calling `CreateEntry`. Verify with the same raw-byte
   check before trusting a zip, going forward — recorded in the build-process memory.

**NEXT**

- **MRM-66** exists for the next resolution change; nothing else to carry forward from this
  detour — MRM-8 and MRM-10 are both otherwise complete.

---

## MRM-10 (in progress) — Display target changed to 960×540 embedded, not fullscreen

**BUILT**

- `PlayerSettings.defaultWebScreenWidth/Height` → **960×540** (was 1920×1080).
- `Docs/webgl-constraints.md` — target line updated; decision recorded inline with rationale.
- `Docs/webgl-budget.md` — original canvas-resolution audit row annotated as superseded, left
  in place rather than rewritten (it's a point-in-time record of MRM-6's spike).
- MRM-10's own Scope bullet ("itch.io embed configured: 1920x1080, fullscreen button enabled")
  rewritten to the new 960×540/embedded spec, plus a comment documenting the full decision.

**DECISIONS**

- **Embedded at a fixed 960×540, not launched fullscreen.** While testing MRM-8's input debug
  overlay in a real itch.io browser build, the persistent branding/fullscreen-button bar from
  Unity's `Default` WebGL template only fully disappeared in true fullscreen — and true
  fullscreen has its own letterboxing quirks across monitor aspect ratios (see the WebGL
  Template decision below). Carlos chose to sidestep the whole problem: embed the game at a
  fixed size in the itch.io page instead of chasing fullscreen behavior.
- **960×540, specifically, because it's an exact 2× divisor of 1920×1080** — the resolution
  everything was originally authored against — so nothing scales blurrily. It also quarters the
  fill-rate cost of every full-screen post-processing pass (fear vignette, chromatic aberration,
  etc. — the project's biggest per-pixel cost per `Docs/webgl-budget.md`), and a slightly softer
  canvas suits a 1979-set game better than a crisp full-HD window.
- **Superseded, not deleted:** MRM-6's original "1920×1080 fullscreen" target predates this
  decision by one day and was a reasonable call at the time — WebGL's fullscreen/embed quirks
  only became visible once an actual build went up against actual browsers, which is the whole
  point of MRM-10 running early (see that issue's own "why issue four, not forty" framing).

**FAILED**

Nothing to record.

**NEXT**

- ~~Carlos, itch.io account access required: switch the embed mode...~~ **Done** — itch.io embed
  mode is now "Embed in page", manually-set viewport 960×540, fullscreen button left disabled
  (Carlos wants fullscreen unavailable entirely, not just unused, so the game's own itch.io page
  — title, branding — always stays visible around the embed as a recall/marketing device).
- **A fresh build is still needed to actually ship this** — build folder `3` (`WebGL Template`
  fix, made minutes before this decision) still has the old 1920×1080 canvas baked in.
- **960×540 is now this project's UI reference resolution, not just a display setting.** Flagged
  on every not-yet-built UI/HUD issue: MRM-18 (main menu), MRM-19 (pause/game over), MRM-65 (UI
  polish — the issue that actually builds the title logo and button styling, most affected),
  MRM-46 (difficulty modes' health/stamina bars), MRM-53 (damage feedback HUD wounds + full-
  screen VFX, which also get cheaper at this resolution).
- **Added a prominent line to `CLAUDE.md` itself** (not just `Docs/webgl-constraints.md`) so the
  960×540 target is impossible to miss at the start of any future session, with an explicit note
  that any `1920×1080` reference found elsewhere is stale.
- **No camera or scene changes needed for the resolution itself** — 960×540 is exactly the same
  16:9 aspect ratio as 1920×1080 (half-scale, not a different shape), and Unity cameras frame by
  aspect ratio, not absolute pixel count. Only pixel-space UI (Canvas Scaler reference resolution,
  hardcoded layout) needed flagging, which is what the issue comments above do.
- Confirmed live in build 4: renders at 960×540 embedded, black page background applied, gamepad
  detected via console (`Gamepad added 1`). Re-verify the `InputDebugOverlay` (MRM-8) is visibly
  legible next time — it wasn't confirmed on-screen in the latest test screenshot (a DevTools
  panel was open taking half the browser width, which may just be cropping it out of view).

---

## Incidental — WebGL Template switched to Minimal

Found while diagnosing "the debug overlay isn't visible in the itch.io build" (MRM-8 testing).
Not a bug: the canvas was already scaling correctly to fill the browser window (confirmed live
against the actual itch.io page, both the direct HTML page and a fresh automated load — no
letterboxing, no fixed-size box). The "thin bar" Carlos saw at the bottom was Unity's `Default`
WebGL Template's own footer (branding + its built-in fullscreen button), confirmed via
`PlayerSettings.WebGL.template == "APPLICATION:Default"`.

**Changed:** `PlayerSettings.WebGL.template` → `APPLICATION:Minimal` (Carlos confirmed). Canvas
now goes edge-to-edge with no Unity branding strip. itch.io's own "Click to launch in
fullscreen" page setting already triggers fullscreen independently of Unity's in-canvas button,
so nothing else needed to change. Aspect-ratio letterboxing on non-16:9 monitors is expected
and unaffected — that's Unity preserving the 1920×1080 image rather than distorting it, not a
bug to fix.

Not logged against a specific issue — this affects whichever issue eventually owns "first WebGL
build on itch.io" (MRM-10). No code change, no tunable, no scene touched.

---

## MRM-8 — Input System — Xbox and keyboard/mouse control schemes

**BUILT**

- `Assets/InputSystem_Actions.inputactions` — extended in place, not replaced. `Player` map
  renamed **`Gameplay`** to match the issue's named maps. All fifteen table actions exist:
  `Move`, `Look`, `Fire`, `Interact`, `Crouch`, `Jump`, `Sprint`, `AimDownSights`, `Reload`,
  `SwitchWeapon`, `EquipMelee`, `FlashlightToggle`, `BootsToggle`, `InventoryScroll`, `Pause` —
  each bound exactly to its Xbox control from the issue's table (left/right stick, both
  triggers, both bumpers, all four face buttons, full d-pad, Start). `Crouch` moved off
  `buttonEast`(B) onto **right-stick press** and `Interact` off `buttonNorth`(Y) onto
  **`buttonWest`**(X) — the stock template had them on the wrong buttons relative to this
  issue's table. Added three new, currently-empty action maps — `Turret`, `Stretcher`,
  `Cutscene` — as switch targets for their own future issues. `UI` map kept, trimmed of
  Touch/Joystick/XR bindings and actions (`TrackedDevicePosition`/`TrackedDeviceOrientation`)
  not relevant to this project's WebGL + Xbox + KB/M target. Control schemes trimmed to just
  `Keyboard&Mouse` and `Gamepad` for the same reason.
- **C# wrapper class generation turned on** for the asset (`generateWrapperCode` in the
  importer, was off) — the wrapper lands at
  `Assets/_Project/Code/Runtime/Input/InputSystem_Actions.cs`, namespace `MrMoonlight.Input`,
  auto-regenerated on every reimport. Required adding `Unity.InputSystem` to
  `MrMoonlight.Runtime.asmdef`'s `references` (it had none before this issue).
- `Assets/_Project/Code/Runtime/Input/InputMapController.cs` — the load-bearing piece. Owns
  one `InputSystem_Actions` instance; `SetMode(InputMode)` disables every map and enables only
  the target one (verified live: Gameplay → UI → Cutscene, each transition fully exclusive).
  Not a MonoBehaviour and not a singleton — whatever needs input constructs one in `Awake` and
  calls `Dispose()` in `OnDestroy`, per this project's "no singletons but `Tunables`" rule.
- Two new tunables on `MoonlightTunables`, header **Input System — MRM-8**: `StickDeadzone`
  (0.125 default) and `InvertYAxis` (false default). Verified live via `execute_code`: the
  constructor writes `StickDeadzone` into `InputSystem.settings.defaultDeadzoneMin` (project-wide,
  since `Gamepad`'s stick controls already fall back to that default — confirmed in
  `Gamepad.cs`) and applies `InvertYAxis` as a runtime parameter override on the `Look` action's
  `invertVector2` processor.

**DECISIONS**

- **No `PlayerInput` component, no `InputUser` pairing, no explicit control-scheme-switch
  code.** Both control schemes stay bound and enabled simultaneously — nothing restricts the
  asset to one device. A newly-connected gamepad therefore "just works" the instant its stick
  or button fires, with no restart and no scheme-change plumbing needed. This satisfies the
  issue's hot-plug acceptance criterion for free; a later HUD/prompts issue can still add
  explicit current-scheme detection (e.g. via `InputUser`) if button-icon prompts need it —
  that wasn't asked for here and would have been scope creep.
- **Keyboard bindings for the nine new actions are placeholders**, not Carlos's prepared
  template — it wasn't in the repo when this issue was picked up. Chosen conventionally (R
  reload, Q switch weapon, V equip melee, F flashlight, B boots, Escape pause,
  `[`/`]` + mouse wheel for inventory scroll). **Flagging this: swap these for Carlos's real
  keyboard template as soon as he supplies it** — only the keyboard column needs touching: all
  Xbox bindings are final, straight from the issue's table.
- **Stick deadzone applied via `InputSystem.settings.defaultDeadzoneMin`, not a per-binding
  processor.** `Gamepad.cs` shows the stick controls already default to `stickDeadzone` with no
  explicit min/max, which reads project-wide settings — adding a second explicit processor on
  top would have clamped twice for no benefit.
- **`InventoryScroll` (d-pad left/right, `[`/`]`, mouse wheel) does double duty as both the
  open trigger and the navigate control, confirmed with Carlos and matching MRM-42's existing
  spec** ("Opens on D-pad left/right or mouse wheel"). No separate "open inventory" binding
  exists or is needed — a single `InventoryScroll` read, interpreted differently depending on
  whether the inventory panel is currently open, is MRM-42's job. Corrected from this entry's
  first draft, which had wrongly flagged this as a missing binding.
- **Added `InputDebugOverlay.cs`**, a throwaway `OnGUI` readout of the last button/key pressed
  on any device, via `InputSystem.onAnyButtonPress` — the same pattern shown in Unity's own API
  docs for this exact purpose. Requested by Carlos so he can visually confirm input capture
  before MRM-9 lands a player to react to it. Toggle: F1 (keyboard), Select/View (gamepad), or
  the inspector checkbox. Not wired into any scene — see NEXT.

**FAILED**

Nothing to record.

**NEXT**

- **Unblocks MRM-9** (player controller) — `Gameplay.Move`/`Look`/`Jump`/`Crouch`/`Sprint` are
  ready to read from.
- **Carlos:** confirm the Xbox scheme in an actual browser build (editor gamepad support
  differs from WebGL's Gamepad API per `Docs/webgl-constraints.md` §7) with your Logitech
  controller, and hand over the keyboard binding template so the nine placeholder keys above
  can be swapped for real ones. **Also: drop `InputDebugOverlay` onto any empty GameObject** in
  a test scene (e.g. `SampleScene`, until Sandbox exists) to try it — that wiring is yours per
  the usual rule.
- **Deferred:** `Turret`, `Stretcher`, `Cutscene` maps exist but are empty — each fills in when
  its own issue lands. Current-control-scheme detection/exposure (for button-icon HUD prompts)
  also deferred, not required by this issue's acceptance criteria.
- **MRM-42 comment added**: which underlying actions its "open/navigate" (`InventoryScroll`),
  "use" (`Jump`/A), and "close" (`EquipMelee`/B) mechanics read from, and a flagged open
  question — whether the inventory stays in the `Gameplay` map or borrows the `UI` map while
  open — left for whoever picks that issue up, since MRM-42's own "no pause, Tracey stays
  vulnerable" design means it isn't a clean full mode-switch like Turret/Stretcher/Cutscene.

---

## MRM-7 — MoonlightTunables — central constants asset + inspector pattern

**BUILT**

- `Assets/_Project/Code/Runtime/Data/MoonlightTunables.cs` — the `MoonlightTunables`
  `ScriptableObject`. Sixteen fields across three `[Header]` groups, each with an XML doc
  comment naming its owning issue: **Player Movement — MRM-9** (walk/sprint/crouch speed,
  mouse and stick look speed, look acceleration, jump height and speed, crouch height delta,
  crouch transition duration, slope limit, gravity — defaults per MRM-9's proposed starting
  values where it gave one, sensible placeholders otherwise), **Pathfinding — MRM-27** (the
  three tunables MRM-6 obliged this asset to carry: ms-per-frame budget, max concurrent
  agents, repath interval), **Mine Lighting — MRM-60** (max real-time lights).
- `Assets/_Project/Code/Runtime/Data/Tunables.cs` — the single access point, `Tunables.I`,
  a lazy `Resources.Load<MoonlightTunables>("MoonlightTunables")`. The project's only
  sanctioned singleton and only sanctioned `Resources.Load` call, per
  `Docs/csharp-conventions.md` and `Docs/unity-conventions.md`.
- Per-instance override pattern documented on the `MoonlightTunables` class doc comment
  (mirrors the example already in `Docs/unity-conventions.md`): a tunables value is the
  default, a component may carry `[SerializeField] bool overrideX` + `[SerializeField] float
  xOverride`, and a computed property picks between them. Both fields show in the inspector,
  not just a checkbox. Reuse this shape everywhere a value needs a shared default plus a
  per-instance override (per-enemy cone distance, per-weapon spread, etc.) rather than
  inventing a new one per system.

**DECISIONS**

- **Player-movement values seeded from MRM-9's own issue text**, not left empty. MRM-9 is
  blocked by this issue and hasn't started, but its description already proposes
  walk/sprint/crouch/crouch-transition defaults — using those (and sensible placeholders for
  the rest of its listed tunables: look speed/acceleration, jump height/speed, crouch height
  delta, slope limit, gravity) means MRM-9 lands with values to tune rather than a still-empty
  asset. This is the one exception to "populating it is not in scope" that the issue itself
  calls out.
- **`JumpHeight` and `JumpSpeed` are both fields**, not one derived from the other, even
  though a physics controller could compute takeoff velocity from height and gravity. MRM-9's
  tunables list names them separately; keeping both lets Carlos tune the felt takeoff speed
  without back-solving the math.
- **Script location follows the repo's actual `Code/Runtime` / `Code/Editor` split** (from
  MRM-6), not the `Scripts/Player`, `Scripts/Weapons`, etc. subfolders shown in
  `Docs/unity-conventions.md` — that split doesn't exist yet in this repo. Used a `Data/`
  subfolder under `Code/Runtime` to match the ScriptableObject-definitions intent from the
  conventions doc.

**FAILED**

Nothing to record.

**NEXT**

- **Carlos:** create the `MoonlightTunables` asset instance — `Create > MrMoonlight >
  Tunables` — and place it inside a folder literally named `Resources` so `Tunables.I`
  resolves it (e.g. `Assets/_Project/Data/Resources/MoonlightTunables.asset`), named exactly
  `MoonlightTunables`. Confirm a value change in the inspector takes effect in play mode
  without a recompile, then this issue's acceptance criteria are done.
- **Unblocks MRM-9** (player controller), which now has a tunables asset and seeded defaults
  to build against.
- **Feeds MRM-27** and **MRM-60** with the four tunables MRM-6's comment obliged.

---

## MRM-6 — [SPIKE] WebGL viability decision + build budget

**BUILT**

- `Docs/webgl-budget.md` — the viability decision, the MB budget table, 17 WebGL traps with
  mitigations, texture and audio import preset specs, the project setup sequence, and the
  results of the first live test.
- `Docs/changelog.md` — this file.
- `Assets/_Project/Settings/Web_RPAsset.asset` + `Web_Renderer.asset` — a dedicated WebGL
  render tier. Forward+, depth and opaque textures on, render scale 0.8, MSAA off, main-light
  shadows at 1024 with 2 cascades, additional-light shadows off, soft shadows off.
- `Assets/_Project/Settings/Presets/` — **17 import presets** (9 texture, 8 audio), wired into
  the Preset Manager so imports self-sort by filename prefix.
- Project settings: web canvas 960×600 → **1920×1080**; WebGL initial memory 32 → **512 MB**;
  `nameFilesAsHashes` on; managed stripping **Medium**; audio real voices 32 → **24**, DSP
  buffer → Best Performance.
- New `Web` quality level, with WebGL pointed at it and the availability matrix locked down.
- `Packages/manifest.json` — **9 dependencies removed** (46 → 37).

No runtime code. No tunables — this issue produced a document. The four tunables it *implies*
are logged against MRM-7.

**DECISIONS**

- **GO on WebGL.** The governing number is a **~300 MB build, not 1 GB.** The Assignment #10
  gate is wall-clock: after ~26 s of fixed overhead only ~94 s of download remains, which is
  ~294 MB at 25 Mbps. A 1 GB build only passes above ~100 Mbps.
- **Overage is not a stop-work** (Carlos). Above target we ship a loading notice on the itch.io
  page rather than cutting content. 450 MB is a review line, not a hard stop. Build size is
  explicitly **not** a no-go trigger; only "does not run in a browser" is.
- **All cutscenes are in-engine runtime. No pre-rendered video ships.** Video budget fixed at
  0 MB and `com.unity.modules.video` removed.
- **One custom full-screen pass, not four.** URP already folds chromatic aberration, vignette,
  colour grading, film grain, lens distortion and tonemapping into a single `UberPost` pass, so
  those stack free. Radial blur, double vision and tunnel vision become one weighted
  `MoonlightScreenFX` feature — assigned to MRM-53, which makes MRM-54/55/56/57 "add a weight."
- **Audio is not the biggest risk — wrong import settings are.** 250 dialogue lines are 264 MB
  as stereo WAV and 20 MB as mono Vorbis. Mitigation is project-wide presets, not content cuts.
  Textures are the real pressure at 143 MB.
- **IL2CPP, not the CoreCLR backend.** Unity's manual labels CoreCLR experimental; the whole
  §8 settings chain and the 25 MB code budget assume IL2CPP.
- **WebGL 2.0, not WebGPU.** Not gambling a graded deadline on a browser feature the grader may
  not have.
- **Rejected:** giving WebGL the existing `Mobile` tier (no depth or opaque texture — silent
  VFX breakage in the browser only); hand-editing MCP for Unity's asmdef (third-party, lost on
  update, and Medium stripping should handle it).

**FAILED**

Three claims in the first draft of `webgl-budget.md` were wrong. Corrected in place, recorded
here so they are not retried:

- **"Turn on lightmap/fog/instancing shader stripping — all three are currently off."** Wrong.
  All three read `0`, which the Editor's own enums confirm is `Automatic` / `StripUnused`,
  i.e. stripping already enabled. Setting them to `Custom` requires hand-listing modes to keep
  and a wrong list breaks lighting or fog **in the build only**.
- **"Remove `com.unity.modules.screencapture`."** Wrong. MCP for Unity's `ScreenshotUtility.cs`
  calls `ScreenCapture.CaptureScreenshot`; removing it breaks the Editor bridge.
- **"Remove `com.unity.modules.umbra` — URP does not use it."** Wrong. Umbra *is* Unity's
  occlusion culling and is pipeline-independent. A forest island wants it.

Also downgraded: pruning `DefaultVolumeProfile` is **not** a build-size win — post-processing
shaders ship via the renderer's `PostProcessData` regardless. Moved to MRM-47 as tidiness.

**NEXT**

- **Validated live.** Empty build **~10 MB**, uploaded to itch.io as project kind `HTML`, runs
  fullscreen. **Brotli is served correctly** — no Decompression Fallback, no Gzip. Confirmed on
  the real platform: WebGL 2.0, DXT via `s3tc`, BPTC, `KHR_parallel_shader_compile`, PhysX
  single-threaded, and the audio context resuming on the fullscreen click.
- **Unblocks MRM-10**, which is now mostly done — what remains is the build report, the page
  loading notice, log stripping, and cold-cache timing from a machine that has never seen it.
- **Constrains** MRM-7 (4 tunables), MRM-15 (no video), MRM-18 (**no percentage on the loading
  bar** — itch.io sends no `Content-Length`), MRM-27 (single-threaded A\*), MRM-47 (4 skyboxes),
  MRM-53 (`MoonlightScreenFX`), MRM-58 (terrain tier values), MRM-63 (preset filename prefixes),
  MRM-64 (10 MB baseline).
- **Deferred:** three URP internal shaders fail under GLES 3.0 — `CoreSRP/CoreCopy`,
  `StencilDitherMaskSeed`, `HDRDebugView`. Nothing visibly broke; re-check at MRM-58 (LOD
  cross-fade) and MRM-53 (copy paths).
- **Not created:** `Docs/optimization.md`. It belongs to MRM-64; the baseline and first two
  entries are waiting in that issue's comments.
- **Open questions:** on-screen character count (MRM-63), surface-world SSAO (currently off),
  hero skybox resolution (MRM-47).
