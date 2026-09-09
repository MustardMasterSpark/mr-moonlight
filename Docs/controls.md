# Controls — Mr. Moonlight

Every input the player can give and what it does, for both control schemes. Both schemes are
bound simultaneously (Unity Input System, `InputSystem_Actions.inputactions`) — no menu toggle is
needed, the game just responds to whichever device you touch.

## Gamepad (Xbox layout)

| Input | Action | What it does |
|---|---|---|
| **Left Stick** | Move | Walk / run around |
| **Right Stick** | Look | Aim the camera |
| **Right Trigger (RT)** | Fire | Fire the currently held weapon |
| **Left Trigger (LT)** | Aim Down Sights | Raise the weapon to aim down its sights |
| **A** | Jump | Jump |
| **B** | Crouch | Crouch (hold; also click Right Stick) |
| **X** | Interact | Pick up items / interact with the world (hold) |
| **Y** | Reload | Reload the current weapon |
| **Left Stick (click)** | Sprint | Sprint while moving forward |
| **Right Shoulder (RB)** | Switch Weapon | Cycle to the next held weapon |
| **D-Pad Left / Right** | Inventory Navigate | Open the inventory, then step the selection |
| **Start** | Pause | Open / close the pause menu |

**Not yet wired to gameplay** (defined in the input asset, no behaviour attached): D-Pad Up
(Flashlight Toggle), D-Pad Down (Boots Toggle), East button/`B` composite for Equip Melee — melee
equip is currently keyboard-only (see below) and closes the inventory when pressed.

## Keyboard & Mouse

| Input | Action | What it does |
|---|---|---|
| **W / A / S / D** | Move | Walk / run around |
| **Mouse movement** | Look | Aim the camera |
| **Left Mouse Button** | Fire | Fire the currently held weapon |
| **Right Mouse Button** | Aim Down Sights | Raise the weapon to aim down its sights |
| **Space** | Jump | Jump |
| **C** | Crouch | Crouch (hold) |
| **E** | Interact | Pick up items / interact with the world (hold) |
| **R** | Reload | Reload the current weapon |
| **Left Shift** | Sprint | Sprint while moving forward |
| **Q** | Switch Weapon | Cycle to the next held weapon |
| **Mouse Wheel / `[` / `]`** | Inventory Navigate | Open the inventory, then step the selection |
| **V** | Equip Melee (in inventory) | Closes the inventory once it's open |
| **Escape** | Pause | Open / close the pause menu |

**Not yet wired to gameplay:** `F` (Flashlight Toggle), `B` (Boots Toggle) — bound in the input
asset but no system reads them yet.

### Inventory mini-flow (both schemes)

Scrolling the inventory axis away from zero while closed **opens** it; the same scroll then steps
the selection. **Jump** (Space / A) uses the selected item. **Equip Melee** (V / East button)
closes it. The player is not paused or immobilized while it's open — Tracey can still be attacked.

## Debug / cheat keys (keyboard only — do not ship, dev builds only)

These are development-only overlays and toggles for testing; none of them are part of the
intended player experience.

| Key | What it does |
|---|---|
| **F1** | Input debug overlay — shows the last key/button pressed on any device and which action it's bound to |
| **F2** | Player stats overlay — health, stamina, speed, melee, defense, audio pitch |
| **F3** | Infinite stamina toggle — sprint never runs out |
| **F4** | Invulnerability toggle — player takes no real damage (hits still flash/register) |
| **F5** | Health regen toggle — health ramps back to full a couple of seconds after the last hit |
| **F6** | Toggles the HAZE fog on/off |
| **F7** | Toggles the CRT retro filter on/off |
| **F8** | Cycles the time of day: Morning → Sunset → Night → Apocalypse → Morning |

## UI navigation (menus, when not in gameplay)

**Updated 2026-09-08 (MRM-18, Carlos's ask) — the two schemes are no longer symmetric.** A menu now
picks one of two input schemes at a time based on whether a gamepad is connected, rather than
accepting D-pad and arrow keys/WASD simultaneously the way gameplay input does:

| Scheme | Navigate | What's selected by default |
|---|---|---|
| **Gamepad** (a `Gamepad` is connected) | Left Stick / D-Pad moves focus between buttons | The menu's first button is auto-selected the instant this scheme is entered |
| **Keyboard & Mouse** (no gamepad connected) | None — arrow keys/WASD do nothing in menus | Nothing. No button is highlighted until the mouse hovers one |

A / South button and Enter/Space still submit; B/East button and Escape still cancel/back, in
either scheme.

### Why menus behave differently from gameplay here

Every other control in this doc is deliberately scheme-agnostic — gameplay binds keyboard and
gamepad simultaneously, no toggle, whichever device you touch fires (see `InputMapController`'s
own doc comment). Menus are the one place that isn't true anymore, because a highlighted button is
*visible state*, not just an input binding: leaving both schemes live at once means a mouse user
either sees a permanently-highlighted button they never chose (confusing — looks like a stuck
focus ring) or has arrow keys silently able to move a highlight they never asked for. Gameplay has
no such visible-selection artifact, so it doesn't need this.

### `MenuInputSchemeController` — general-purpose, not main-menu-specific

`Assets/_Project/Code/Runtime/UI/MenuInputSchemeController.cs` is the reusable fix, built for the
main menu (MRM-18) but intended to be dropped onto **any** screen with `Selectable`s (pause menu,
a future settings-only screen, etc.) — point its `firstSelected` field at whichever button should
get focus first in gamepad scheme.

**The mechanism is one `LateUpdate` check, not a pile of per-button logic:** in Keyboard & Mouse
scheme, the controller clears `EventSystem.current.currentSelectedGameObject` back to `null` every
frame it finds one set. That single check fixes two symptoms that turned out to be the same root
cause:

1. **A button stuck highlighted yellow even while the mouse hovers a different button.** That
   persistent tint is `Selectable`'s *Selected* colour state, which sticks to whatever the
   EventSystem last selected until something deselects it — entirely independent of mouse hover
   (hover only drives the separate *Highlighted* state). This is what Carlos saw: Start button was
   selected once by the reveal sequence's gamepad-support code and then never deselected.
2. **Arrow keys/WASD silently navigating the menu.** Unity's UI Move action can only move focus
   *away from* a currently-selected object — with nothing ever selected, Move has nothing to act
   on and does nothing. No need to disable the Move action itself, or touch the input asset at
   all.

Because the check runs every frame regardless of *what* set the selection, no other script needs
to know which scheme is active — a menu's own reveal-selection call (`MainMenuController`'s
`SelectForGamepad`) can keep firing unconditionally in both schemes; in Keyboard & Mouse scheme
it's simply overwritten back to `null` before the next frame renders, so there's no visible flash.

Scheme detection itself is just `Gamepad.current != null` on enable, plus subscribing to
`InputSystem.onDeviceChange` so plugging in (or unplugging) a controller mid-menu switches schemes
live and fires `OnSchemeChanged` for anything that wants to react.
