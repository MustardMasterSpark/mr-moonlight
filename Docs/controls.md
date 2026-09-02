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

| Gamepad | Keyboard/Mouse | Action |
|---|---|---|
| Left Stick / D-Pad | W/A/S/D or Arrow Keys | Navigate |
| A / South button | Enter / Space | Submit |
| B / East button | Escape | Cancel / Back |
| — | Mouse | Point and click |
