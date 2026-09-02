# MRM-75 — resume point, 2026-09-01 evening

Branch `mrm-75`. Continues from `Docs/mrm75-resume-2026-09-01.md` (the AccuRig-rigging session).
Read that file first for the pipeline history up to "rig exists, nothing animated yet" — this
file picks up from there. **This whole character still lives in the Playground project**
(`E:\playground\My project`), not Mr. Moonlight — nothing has migrated into the main game yet.

---

## What happened this session

### 1. AccuRig rig exported and imported clean
`old timer - acurig.fbx` → `Assets/Character Playground/Oldtimer/Spotter_OldTimer.fbx` in
Playground. Confirmed the known AccuRig failure mode from `Docs/3d-prop-pipeline-wizard.md` §4.6:
`globalScale` needed to be **0.01** (cm→m) — NOT `1.0`, which is right for our own Blender
exports but wrong here (see `mrm75_accurig_scale_bug` memory and the wizard doc's new §4.6 entry).
`materialImportMode = None` also had to be set by hand, same as every other import.

### 2. Full Animator Controller built and wired — `AC_Spotter_OldTimer.controller`
13 states, 13 Trigger parameters, AnyState transitions (`hasExitTime=false`, `duration=0.1`).
Built from Blaze AI's actual needs (read directly from `CoverShooterBehaviour.cs` and
`HitStateBehaviour.cs`, not guessed) plus Carlos's own additions:

| State | Motion source | Notes |
|---|---|---|
| Idle | `IdleAimShotgun` (owned asset pack) | |
| Walk | `WalkForwardShotgun` | |
| Run | `SprintForwardShotgun` | |
| StrafeLeft / StrafeRight | `WalkForwardLeftAimingShotgun` / `...Right...` | Blaze's real `StrafeMovement()` is navmesh-aware |
| Shoot | `ShootShotgun` | |
| Hit | `GetHitFrontLightShotgun` | |
| Death | `DeathRightShotgun` | |
| Flare | `Src_Flare_MixamoShootingGun` (external, live-retargeted) | see §13 technique below |
| Fall | `Src_Fall_MixamoDeath` (external) | loop=false, ends held (collapsed pose) |
| DeathDowned | `Src_DeathDowned_MixamoDying` (external) | loop=false, plays *from* an already-downed pose |
| Downed | `Src_Downed_Convulsing` (external) | **loop=true** (writhing-in-pain state, Carlos's call) |
| Reload | `Src_Reload_MixamoReloading` (external) | loop=true |

**Equip/Unequip were deliberately NOT added** — `equipWeapon` in `CoverShooterBehaviour.cs` is an
opt-out toggle (unticked = the whole equip sub-behavior is skipped), so the Spotter doesn't need
those states at all. See `mrm75_blaze_equip_and_hitstate_facts` memory.

**Open question for next session, not yet resolved:** the *previous* resume doc's Blaze research
said Blaze drives animation by **string clip name** through `blaze.animManager.Play(name, ...)`,
not by a hand-authored Animator graph with parameters. This session built exactly that kind of
Animator graph anyway, as an **animation-preview/test harness** so Carlos could trigger and watch
each clip in Playground. Before wiring Blaze for real: confirm whether Blaze can also drive an
Animator Controller directly (some Blaze integrations support both), or whether this controller's
job ends at "verified the clips are correct" and final wiring goes through `animManager` by name
instead, making the trigger-parameter structure a preview-only artifact.

### 3. The real problem this session: retargeting the 5 external clips (Flare, Fall,
DeathDowned, Downed, Reload)

**Read `Docs/retarget-pro-strategy.md` §13 in full before touching this again** — this cost an
entire session and 8+ measured Retarget Pro bake attempts before the actual fix was found.
Short version:

- Neither script-baking (§11) nor a full, careful Retarget Pro GUI pass (§12, profile rebuilt
  from scratch twice) produced a correct standalone `.anim` — every bake, regardless of method,
  collapsed the hip toward root height while other joints stayed fine.
- Root motion was ruled out directly (forced to identity, bug persisted). Several Retarget Pro
  settings (`Use Root Motion`, `Root Node`, feature `Offset`, `Target`/`Source Pose`) were proven,
  by reading the actual saved file, to have **zero effect on the real bake output** despite
  visibly changing the live preview — a real tool limitation, not user error.
- **The actual fix: skip baking entirely.** Assign the source clip directly as the state's
  `motion` and let Unity's own Humanoid-to-Humanoid live retargeting do the work at playback time
  — it was correct every time it was sampled, all session, and needed no setup. All 5 external
  clips are now wired this way and verified by sampling hip/head height across the full clip
  duration in `AnimationMode` (not just eyeballing the preview — that's what gave false
  confidence earlier in the session).
- Retarget Pro is **not** obsolete — full rationale and the corrected scope (Wendigo boss: yes,
  will likely want a real pass for IK/proportion correction; FP weapon animations: not applicable,
  already fully owned) is in §13.4. Don't re-relitigate this from scratch; read it.

### 4. Small fix
`Downed`'s clip was set to loop (Carlos's call — it's a writhing-in-pain state, should repeat
until Blaze transitions out of it, not freeze after one pass).

---

## Still open — pick up here

1. **Attach a weapon to the Old Timer's hand.** No approach decided yet. Needs: a real hand
   socket/attach point on the AccuRig rig (likely a child Transform parented to the right-hand
   bone, positioned/oriented to the weapon's grip), and a decision on whether the regular held
   weapon (double-barrel shotgun, matching the owned clip names like `ShootShotgun`) and the flare
   gun (only held/visible during the `Flare` state) share one socket with a prop-swap, or use two.
   Carlos flagged this as the immediate next step.
2. **Once the animator + weapon attachment are both settled**, migrate the **complete** prefab —
   mesh, rig, materials, `AC_Spotter_OldTimer.controller`, all 12 clips (7 owned + 5 external,
   now all correctly retargeted), weapon prop(s) — out of Playground and into Mr. Moonlight
   proper. Follow the folder conventions already established for this character in the prior
   resume doc (`Assets/_Project/Art/Enemies/Spotter/`, `Assets/_Project/Prefabs/Enemies/`) and
   `unity-conventions.md` generally. Not started.
3. **Only after that migration lands**, start real Blaze AI Engine wiring (MRM-34): transfer the
   Blaze AI Engine package itself into Mr. Moonlight (still not done, see prior resume doc item 4),
   resolve the animator-vs-`animManager` open question above, then write the flare `BlazeBehaviour`
   subclass per MRM-34's spec. Do not start this before the prefab migration — Carlos's explicit
   ordering.
4. **Log the character** in `Docs/prop-log.md` once Carlos actually reviews/approves it (§3.4/§9
   of the wizard doc's gate) — still hasn't happened; don't log preemptively.
5. Everything in the prior resume doc's "Still open" list that this session didn't touch (sound
   pools, hitbox colliders/layers/vision cone, prop-wizard gap G3 closure) is still open too.
