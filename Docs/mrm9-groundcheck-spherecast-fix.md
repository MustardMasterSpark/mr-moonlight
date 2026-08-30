# MRM-9 — ground-check SphereCast fix (super-jump bug)

**Written 2026-08-29.** Read this if the player launches unexpectedly off the ground again,
before assuming it's something new.

## Symptom

Carlos, while hand-placing colliders on vegetation props in `VegetationGallery`: jumping and
landing near — or on top of — a prop's collider sometimes catapulted the player ("super jump")
instead of a normal landing.

## Root cause

`PlayerStateMachine.ApplyFloatingForce()` (Burntwax FPS Engine's rigidbody "floating capsule"
ground check, `Assets/ThirdParty/Burntwax Collective/Burntwax FPS Engine/Scripts/Player/Movement/PlayerStateMachine.cs`)
used a single `Physics.Raycast` straight down from `rb.position` every physics step, then applied
an unclamped spring force scaled by that one distance reading:

```csharp
Physics.Raycast(rb.position, Vector3.down, out groundCheckHit, rayDistance)
float x = groundCheckHit.distance - rideHeight;
float springForce = (x * rideSpringStrength) - (relVel * rideSpringDampener);
rb.AddForce(rayDir * springForce);
```

A single point sample is fine over one continuous surface, but fragile at a **seam** — where a
prop's collider meets `Ground`'s own collider, or the rounded end of a capsule collider. Landing
exactly on a seam, the ray either missed for a frame (falling into the gap between two colliders)
or the falling Rigidbody had already sunk slightly into the prop's collider before the ray
resampled. Either way `groundCheckHit.distance` could snap to a much smaller value than the frame
before. Since `rideSpringStrength = 100` with no clamp on `x` or `springForce`, one bad sample
produced a large single-step upward force.

This gets worse, not better, as more colliders are added — every vegetation prop's collider sits
right at or near the `Ground` plane, i.e. right where this bug triggers — and will matter a lot
more once Gaia scatters thousands of instances across the terrain.

## Fix

Widened the single ray to a `Physics.SphereCast`, radius `groundCheckRadius = 0.3` (new
`[SerializeField]`, public `GroundCheckRadius` property, same pattern as the sibling `rideHeight` /
`rideSpringStrength` / `rayDistance` fields — **not yet piped through `MoonlightTunables`**, same
as its siblings). `0.3` is intentionally smaller than the player's own `capsuleCollider.radius`
(`0.4`, on the `Body` child) — wide enough to bridge a seam under the feet, not so wide it starts
catching geometry beside the player.

Changed only `ApplyFloatingForce()` — the method that actually produces the launching force.
`PlayerStateMachine.cs` has two other `Physics.Raycast(rb.position, Vector3.down, ...)` calls
(around line 236 and 270, inside grounded/slope **state transition** checks) that share the same
seam-fragility but weren't touched. Their failure mode is different — a misdetected grounded/slope
*state* flicker, not a force spike — and wasn't the reported symptom. Widen those too if state
flicker (e.g. spurious airborne/slope transitions near a prop) turns up later.

## Consequences / what changed in feel

- SphereCast treats the player's ground contact as a small volume instead of a point, so very
  fine geometry (a thin edge, a narrow gap between two adjacent colliders) that used to
  legitimately drop the player through now gets bridged over instead. Intentional — that's the
  fix — but it means landings near any collider edge feel slightly more "forgiving"/rounded than
  before, project-wide, not just in the gallery.
- Because the origin sphere starts already overlapping the player's own `Body` capsule collider,
  Unity/PhysX's normal SphereCast behavior (a cast doesn't register a hit against a collider the
  origin already starts inside) still applies — this doesn't cause the player to detect its own
  collider as ground, same as the original Raycast.
- No change to `rideHeight`, `rideSpringStrength`, `rideSpringDampener`, or `rayDistance` — only
  the shape of the query changed, not the spring math.

## Verified

Compiled clean (`mcpforunity://editor/state` — `is_compiling:false`, no domain reload errors, 0
console errors after refresh). `GroundCheckRadius` reads back `0.3` on the live `Player` instance
in `VegetationGallery`.

## Status — 2026-08-29: NOT fixed, deferred as polish

Carlos play-tested against the original repro (jump next to a gallery prop's collider, land on
it) and the super-jump **still happens** with the SphereCast in place. So the diagnosis above may
be incomplete — either `groundCheckRadius = 0.3` isn't wide enough to bridge whatever seam is
actually being hit, or there's a second contributing cause not yet identified (e.g. PhysX's own
Rigidbody-vs-collider penetration resolution independent of this script's raycast, one of the two
still-unwidened grounded/slope Raycasts feeding a bad `playerIsGrounded` value into the guard
condition, or something specific to how two colliders overlap near the `Ground` plane in this
scene). The `SphereCast` change is left in place (it's a real improvement per the reasoning
above, just not sufficient on its own) rather than reverted.

**Explicitly deprioritized by Carlos** — not blocking vegetation collider work. Revisit as a
polish-pass item, not before. Next step when picked back up: reproduce in Play mode with
`groundCheckHit`/`springForce` logged per-frame to see what the ground check actually reads in
the frame the launch happens, rather than guessing further from code reading alone.

## Update — 2026-08-30: found the actual boundary bug, second fix applied

Carlos reported something that looked unrelated at first but wasn't: standing **completely
still** on flat ground, `PlayerStateMachine`'s `Current State Text` was visibly flickering between
`PlayerGroundedState` and `PlayerFallState`, and `Rigidbody.Linear Damping` was flickering between
`10` and `0.5` in the Inspector (two screenshots, same frame-to-frame flip). Those are exactly
`groundDrag`/`airDrag` — proof the state machine itself was toggling grounded/airborne with no
input at all.

**Root cause:** `PlayerGroundCheck()` and `PlayerSlopeCheck()` (unchanged since the 2026-08-29
fix — that fix only touched `ApplyFloatingForce()`) cast `Physics.Raycast(rb.position,
Vector3.down, out groundCheckHit, rideHeight)` — **max range exactly equal to `rideHeight`**, the
same distance the floating-capsule spring is *trying to hold the player at*. The spring is a
damped oscillator (`springForce = x * rideSpringStrength - relVel * rideSpringDampener`), not a
rigid lock, so the true ground distance constantly ticks a hair above and below `rideHeight` even
while standing still on flat, seam-free ground. Every time it ticked past `rideHeight`, the ray's
range was too short to reach the ground and missed — `playerIsGrounded` flipped false for that
frame — `PlayerGroundedState.CheckSwitchStates()` saw `!playerIsGrounded` and switched to
`PlayerFallState`, which sets `rb.linearDamping = AirDrag (0.5)`. Next frame the ray usually hit
again and it flipped back to `PlayerGroundedState` (`GroundDrag`, 10). This is a different bug
class from the seam/gap theory in the 2026-08-29 fix above — it reproduces in the *middle* of flat
ground, no seam needed, purely because the sensor's range and the spring's target distance were
set to the same number.

This is also a strong second candidate for the super-jump itself: while flickered into
`PlayerFallState`, `linearDamping` drops to `0.5`. `ApplyFloatingForce()` keeps running every
`FixedUpdate()` regardless of which state is active (it only gates on `playerIsGrounded ||
playerIsSloped`, not on state), so a spring force computed the same frame is applied against a
nearly undamped rigidbody — a normal spring overshoot that would have been absorbed at
`groundDrag = 10` can instead accumulate across the flickering frames.

**Second fix:** widened both `PlayerGroundCheck()` and `PlayerSlopeCheck()` from
`Physics.Raycast(..., rideHeight)` to `Physics.SphereCast(rb.position, groundCheckRadius, ...,
rayDistance)` — same sensor shape and range `ApplyFloatingForce()` already uses successfully
(`rayDistance = 2.0`, well past the `rideHeight = 1.2` target, so normal spring jitter can no
longer exit sensor range). All three ground-sensing call sites in `PlayerStateMachine.cs` now
agree on one sensor definition instead of two different ones (a wide/long one in
`ApplyFloatingForce`, a point/boundary-exact one in the other two). Compiled clean, verified via
`mcpforunity://editor/state` (`is_compiling:false`, no domain-reload errors, only an unrelated MCP
transport warning in the console).

**Status: applied, not yet play-tested by Carlos.** If the super-jump still reproduces after this,
the remaining candidate is PhysX's own Rigidbody-vs-collider penetration resolution acting
independently of any of these three raycasts — that would need the per-frame
`groundCheckHit`/`springForce` logging suggested above, done live in a focused Play session (not
an MCP-driven one — Unity does not tick while unfocused, see
[[mrm9_burntwax_controller_swap]]).

## Update — 2026-08-30 (second report): improved, still happens — new trigger identified + two more fixes

The two fixes above helped but did not close it. Carlos found a cleaner repro: walking **past** a
collider, brushing/"kissing" the wall while jumping at that exact moment — not landing on top of
anything. That points at a different mechanism than either fix above: a **wall/side contact**, not
a ground-plane seam.

**Two more likely contributors, found by inspecting live state:**

1. **Friction-hop.** The player's `Body` capsule already uses a `NoFriction` `PhysicsMaterial`
   (`dynamicFriction`/`staticFriction` = 0) — but its `frictionCombine` mode was **`Average`**, and
   most vegetation colliders have **no material assigned** (Unity's built-in default, nonzero
   friction). `Average` of 0 and a nonzero default is still nonzero — so sliding along a wall while
   `PlayerMove()` continuously adds force into it every `FixedUpdate` (it doesn't check whether
   you're blocked) could still generate friction-driven torque/force at the contact. Fixed:
   `NoFriction.frictionCombine` changed **`Average` → `Minimum`**, which guarantees 0 friction wins
   against *any* other collider's material, not just ones that happen to also be frictionless.
   (`Assets/ThirdParty/Burntwax Collective/Burntwax FPS Engine/Physics Materials/NoFriction.physicMaterial`.)

2. **Unbounded depenetration + unbounded vertical velocity.** `Physics.defaultMaxDepenetrationVelocity`
   is Unity's stock `10`. Because `PlayerMove()` keeps pushing the rigidbody into a wall every
   physics step (nothing stops adding movement force just because you're blocked), the capsule can
   end up slightly overlapping the wall collider; PhysX's solver then pushes it back out along the
   contact normal at up to that depenetration speed. At a capsule-vs-box corner/edge (exactly what
   most vegetation props are), that contact normal can have a real vertical component — so part of
   a 10 u/s correction becomes upward velocity, on top of whatever the jump impulse (`jumpForce =
   8`, so a clean jump is already ~8 u/s up) added that same moment. Crucially, `SpeedControl()`
   already clamped horizontal speed but **never touched vertical velocity at all** — so once
   anything (this, the old spring-force spike, anything) handed the rigidbody a large upward
   velocity, nothing bounded it; it would carry for the full ballistic arc, which matches "flying
   for a moment." Fixed: added `maxUpwardVelocity = 12f` (new `[SerializeField]`, ~50% headroom
   over the 8 u/s clean-jump velocity) and clamp `rb.linearVelocity.y` down to it at the end of
   `SpeedControl()` every `FixedUpdate` — only clamps upward, falling is untouched, a normal jump
   arc is unaffected.

Both changes verified live via `execute_code` (`NoFriction.frictionCombine == Minimum`,
`maxUpwardVelocity == 12` on the scene's `Player`) and compiled clean. **Not yet play-tested by
Carlos.**

### Is this an easy fix, or does the controller need rethinking?

**Not a rethink.** This is a well-known category of bug for *any* `Rigidbody` + `AddForce`
character controller (as opposed to Unity's non-physics `CharacterController` component): pressing
continuous force into static geometry with a rounded collider produces occasional bad contact
normals and depenetration bursts, and the standard, well-documented fixes are exactly the two
applied here — a hard-zero friction material with `Minimum` combine, and a clamp on the vertical
velocity component. Nothing about Burntwax's floating-capsule/spring architecture is the actual
problem; both root causes found across both reports (the `rideHeight`-as-raycast-range boundary
bug, and this wall-contact pair) are narrow, well-understood physics-integration details, not
symptoms of the overall approach being wrong. If it turns out there's a third contributing cause
after this, the next escalation step is still the same: log `groundCheckHit`/`springForce`/
`OnCollisionEnter` contact normals per-frame in a focused Play session to see the actual bad
values in the frame it happens, rather than reasoning further from code alone.
