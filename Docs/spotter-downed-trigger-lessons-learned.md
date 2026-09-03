# Spotter "lying down at spawn" — lessons learned, 2026-09-02 session

Carlos noticed Spotters occasionally visible to the player lying flat on the ground for a couple of
seconds before standing up and resuming normal behavior. Looked like a default-animator-state bug.
It wasn't — the real cause and the way it was finally caught are both worth keeping.

---

## The bug: a Trigger parameter's *default value* was saved as `true`

`AC_Spotter.controller` has an Animator parameter named `Downed` (type Trigger), wired to an
`AnyState -> Downed` transition (condition: `Downed` trigger). Every other trigger on this
controller (`Idle`, `Walk`, `Hit`, `Fall`, etc.) has `defaultBool = false`. `Downed` alone had
**`defaultBool = true`**, almost certainly saved that way after someone toggled it on in the
Animator window's Parameters tab for a preview and never toggled it back before saving.

Because a Trigger's "unset" state is just a serialized bool under the hood, a default of `true`
means **every fresh Animator instance spawns pre-armed** — the `AnyState -> Downed` transition
fires within the first ~0.1s, before any script (BlazeAI included) ever touches the Animator. It
self-corrects the moment real gameplay logic calls a real animation change, because Blaze's
`AnimationManager.Play()` drives the Animator via `CrossFadeInFixedTime(stateName, ...)` — a direct
state jump that overrides whatever the trigger graph did, no matter what the stuck trigger is doing.
That's why it only lasted until the enemy's own AI logic (typically triggered by starting or
stopping movement, or player detection) issued its first real animation call.

**Confirmed in total isolation** — a bare `GameObject` + `Animator` + this controller, zero other
scripts, still autonomously walked itself from Idle into Downed within one `Animator.Update(0.1f)`
call. That single test is what separated "controller asset problem" from "BlazeAI/gameplay script
problem" — worth doing early next time something like this comes up, instead of auditing every
script that touches the Animator first.

**Fix:** `AnimatorControllerParameter.defaultBool = false` for `Downed`, applied via script
(`ac.parameters[i].defaultBool = false; ac.parameters = parms;` — you have to reassign the whole
array, mutating an element in place doesn't dirty the asset) + `EditorUtility.SetDirty` +
`AssetDatabase.SaveAssets()`.

**General rule this proves:** if an Animator Controller misbehaves identically across *every*
instance with no script anywhere referencing the state/trigger by name, check parameter **default
values** in the Parameters tab before suspecting any code. A Trigger left checked at save time is
invisible in the graph view and easy to miss.

---

## How it was actually caught: an auto-arming Editor watcher, not manual timing

The pose only lasted 2-3 seconds per spawn, self-corrected on approach, and happened somewhere
between "close" and "far" unpredictably — too fast for a human "pause when you see it, then tell
Claude to check" workflow. Two things made manual timing fail specifically:

1. **Entering/exiting Play Mode reloads the domain** (default Unity setting), which wipes any
   `EditorApplication.update` delegate or state registered via MCP's `execute_code` while still in
   Edit Mode. A watcher installed *before* pressing Play does not survive into the Play session —
   it has to be armed *while already playing*, and coordinating that live, by voice, through a tool
   round-trip, could not reliably land inside a 2-3 second window.
2. Even once armed live, a human manually pausing at the right instant plus a tool round-trip was
   still too slow against a window that short.

**What actually worked:** a small persistent Editor script (`[InitializeOnLoad]`, subscribed to
`EditorApplication.playModeStateChanged`) that self-arms every time Play Mode starts — no manual
handshake needed at all. On `EnteredPlayMode` it froze every `BlazeAI` agent in place
(`NavMeshAgent.isStopped = true` + `blaze.enabled = false`) so whatever pose they spawned in would
persist indefinitely instead of self-correcting in a few seconds, and a per-frame `EditorApplication.update`
hook logged a full diagnostic (animator state, `AnimationManager.currentState`, BlazeAI state,
NavMeshAgent status, distance to player) the instant any agent showed a non-locomotion animator
state. This turned an un-catchable timing race into "hit Play once, read the console."

**General technique worth reusing:** for any bug that (a) reproduces reliably but briefly, and
(b) involves Play Mode, write a tiny `[InitializeOnLoad]` Editor script that hooks
`playModeStateChanged` and freezes/logs automatically, rather than trying to time a manual
pause-and-inspect loop through a live session. Delete it once the investigation is done — this one
lived at `Assets/_Project/Code/Editor/SpotterFreezeDebugger.cs` and was removed after the fix
was confirmed.
