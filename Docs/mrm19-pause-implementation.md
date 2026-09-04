# MRM-19 — Pause toggle (narrow scope), 2026-09-03/04

**Branch:** `mrm-19` (off `main`). **Scope shipped this session:** Escape (keyboard) / Start
(gamepad) opens a pause state that halts the game cleanly and resumes cleanly. **Not shipped**:
the 4-button pause UI, the objective display, Restart/Return-to-Menu fades, and the Game Over
screen — Carlos scoped this down explicitly at the start of the session (see
`Docs/mrm19-pause-sonnet-prompt.txt` for the original full-issue handoff). Those remain open
against MRM-19's original acceptance criteria.

## 1. What exists

**`PauseController`** (`Assets/_Project/Code/Runtime/Player/PauseController.cs`) — new component,
placed on `Player_Tracey/MrMoonlight Systems` in `Island.unity` (same GameObject as `DeathSequence`,
`PlayerStats`; added via UnityMCP with Carlos's go-ahead, verified by reading the component back).

- `Toggle()` / `Pause()` / `Resume()`, plus a static `Active` lookup cache — same non-singleton
  pattern as `EventDirector.Active` (`Docs/csharp-conventions.md`'s only sanctioned singleton is
  `Tunables`; this is a scene-authored lookup, not a singleton).
- Subscribes to **both** `Gameplay/Pause` (Escape, Gamepad Start) and `UI/Cancel` (Escape,
  Gamepad East/B) on `MoonlightPlayerRig.Input`. Necessary because `InputMapController.SetMode`
  disables every map but the active one — while paused the Gameplay map (and `Pause` with it) goes
  quiet, so closing the menu has to come from the UI map instead. This means the keyboard is
  symmetric (Escape both ways) but the gamepad isn't (Start opens, B closes) — that's the existing
  binding already authored into `InputSystem_Actions`, not something introduced here, and it
  matches the conventional pause-menu control scheme.
- `[DefaultExecutionOrder(110)]` — must Awake/OnEnable after `MoonlightPlayerRig` (order 100),
  whose Awake constructs the `InputMapController` this component subscribes to.
- Guards against pausing after `EventDirector.Active.LevelEnded` — the win/loss screens already own
  timescale and control state; layering pause on top of them isn't handled and isn't needed yet.
- `OnDestroy` force-clears `Time.timeScale`/`AudioListener.pause` if destroyed while paused — same
  category of bug as the stuck-red-tint static registry from MRM-11, guarded against up front this
  time.

**`MoonlightPlayerRig.SetControlSuspended(bool)`** (new method, alongside the existing
`SetMovementLocked`/`DisableControl`) — a *reversible* full stop: kills movement, look, wieldables,
and interaction, and unlocks the cursor. Unlike `SetMovementLocked` (which deliberately leaves look
alive, for the inventory's glance-around) pause kills look too, since the menu is meant to take the
screen over completely. Unlike `DisableControl` (one-way, used for death) this turns back on.

## 2. The Time.timeScale decision

**Decision: pause uses `Time.timeScale = 0`, not a hand-rolled pause flag threaded through every
system.** This was a deliberate choice against MRM-19's own acceptance criterion — *"pausing during
a cutscene either works correctly or is blocked deliberately — decide and document"* — so here is
that decision:

Every coroutine wait in the project already runs on **scaled** time: `WaitVerb`'s `WaitForSeconds`
(the Event Director's scripted `wait` blocks), `DeathSequence`'s beats, ordinary `Animator`
playback (default Normal update mode). Freezing `Time.timeScale` freezes all of it for free,
including an `EventDirector` sequence sitting mid-`wait` — it doesn't need to be blocked, it
genuinely pauses and resumes exactly where it left off. The only thing that keeps running
regardless is the Input System itself (it polls devices on real/unscaled time), which is exactly
what lets Escape/Cancel still close the menu.

**Consequence found and fixed the same session:** this exposed a real vendor bug. See §3.

## 3. Vendor bug found: `CharacterControllerMotor` NaN velocity at `Time.timeScale = 0`

`CharacterControllerMotor.Update()` (PolymindGames' movement code,
`Assets/_Project/Code/Vendor/PolymindGames/Runtime/Movement/CharacterControllerMotor.cs:263`)
computed velocity every frame as `(position - lastPosition) / deltaTime` with no zero-guard. At
`Time.timeScale = 0`, `deltaTime` is 0 (Update still runs every frame regardless of timescale, only
its *deltaTime* reports 0). With the player stationary, that's `0 / 0 = NaN`, every paused frame,
forever.

That NaN fed into `CameraFOVHandler.GetCameraFOVMod()` (reads `_motor.Velocity`), which is
self-referential — `_cameraFOVMod = Mathf.Lerp(_cameraFOVMod, ..., ...)` — so once it went NaN it
**stayed NaN permanently, even after resuming** (confirmed by testing: resumed, waited several real
seconds, FOV was still NaN). The camera's `fieldOfView` going NaN produces a degenerate projection
matrix, which is what threw the `Assertion failed on expression: 'std::abs(det) > FLT_MIN'` spam
Carlos saw in the console the first time he tried pausing in the build.

**Fix:** `_velocity = deltaTime > 0f ? (position - lastPosition) / deltaTime : Vector3.zero;` — no
motion happened, so zero is also the *correct* answer, not just a safe one. Verified: paused for
several real seconds, FOV held steady, no assertion spam; re-tested with Carlos's own keypress live
in the build with the same result.

## 4. Build

Build 27, "Pause Toggle", `E:\Builds\27 - Pause Toggle - 2026-09-03\Build.zip` — includes the pause
toggle, the `CharacterControllerMotor` fix, and (same session, cross-issue) the shotgun fire sound
stand-in. Does **not** include the later cross-issue work from the same session (shadow distance
restore, damage-number overlay camera, the weapon `AudioRandomContainer` migration, or the
manual-reload rule) — those landed after build 27 was cut. See `Docs/mrm9-hqfps-integration.md`
§13 (cross-issue: Northern Lights) and §14 (manual reload rule) for those, and the session's Linear
comments on MRM-19 for the full list.

## 5. Still open against MRM-19's original scope

- Pause UI: current-objective display, Continue / Restart-from-checkpoint / Restart-the-game /
  Return-to-Main-Menu buttons, all with fade transitions
- Restart-from-checkpoint needs an actual checkpoint system — Carlos hasn't settled that design;
  leave a placeholder method when this is picked up, per the issue's own acceptance criteria
- Game Over screen (Restart whole-scene + placeholder checkpoint-restart, Return to Main Menu)
- Restarting-the-game genuinely clearing checkpoint state (depends on the checkpoint system above)
