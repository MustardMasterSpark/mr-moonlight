# Input Map — Mr. Moonlight

The full action list and both control schemes, read straight from
`Assets/InputSystem_Actions.inputactions` (2026-08-21) — so you don't have to open that asset
to remember a binding. If this doc and the asset ever disagree, **the asset wins**; update this
file to match, not the other way around.

Both schemes stay bound and enabled simultaneously (MRM-8) — a newly-connected gamepad works
instantly, no restart, no scheme-switch step. See `InputMapController.cs`.

---

## Gameplay map

| Action | Keyboard & Mouse | Gamepad (Xbox) |
|---|---|---|
| **Move** | W / A / S / D | Left Stick |
| **Look** | Mouse delta | Right Stick |
| **Fire** | Mouse Left Button | Right Trigger |
| **Interact** | E | X (West) |
| **Crouch** | C | Right Stick Press |
| **Jump** | Space | A (South) |
| **Sprint** | Left Shift | Left Stick Press |
| **Aim Down Sights** | Mouse Right Button | Left Trigger |
| **Reload** | R | Y (North) |
| **Switch Weapon** | Q | Right Bumper |
| **Equip Melee** | V | B (East) |
| **Flashlight Toggle** | F | D-Pad Up |
| **Boots Toggle** | B | D-Pad Down |
| **Inventory Scroll** | `[` / `]`, mouse scroll wheel | D-Pad Left / Right |
| **Pause** | Escape | Start |

**Keyboard bindings for Reload (R), Switch Weapon (Q), Equip Melee (V), Flashlight Toggle (F),
Boots Toggle (B), Pause (Escape), and Inventory Scroll (`[`/`]`) are MRM-8's placeholders** —
chosen conventionally, not Carlos's prepared template (it wasn't in the repo when MRM-8 was
built). All Xbox bindings are final, straight from MRM-8's issue table. Swap the keyboard column
for the real template whenever Carlos supplies it — Xbox stays as-is.

**Look processors:** `invertVector2(invertX=false, invertY=false)`. `invertY` is overridden at
runtime from `Tunables.I.InvertYAxis` (`InputMapController.ApplyInputTunables()`). `invertX` is
hardcoded `false` and should stay that way — `InvertVector2Processor` defaults **both** axes to
`true`, and leaving `invertX` unset here is exactly the bug fixed during MRM-9 testing
(2026-08-21): stick/mouse right turned the camera left. If this binding string is ever touched
again, keep `invertX=false` explicit.

**Gamepad stick deadzone** is a device-wide default (`InputSystem.settings.defaultDeadzoneMin`),
set from `Tunables.I.StickDeadzone` — not a per-binding processor. See `InputMapController.cs`.

---

## UI map

Unity's default UI navigation actions, trimmed of Touch/Joystick/XR bindings (not relevant to
WebGL + Xbox + KB/M). Not hand-tuned per this project's design — the defaults:

| Action | Keyboard & Mouse | Gamepad |
|---|---|---|
| Navigate | WASD / Arrow keys | Left Stick, D-Pad |
| Submit | *(context: Enter/Space)* | *(context: South button)* |
| Cancel | *(context: Escape)* | *(context: East button)* |
| Point | Mouse position | — |
| Click | Mouse Left Button | — |
| Right Click | Mouse Right Button | — |
| Middle Click | Mouse Middle Button | — |
| Scroll Wheel | Mouse scroll | — |

---

## Empty action maps (switch targets, not yet bound)

`Turret`, `Stretcher`, `Cutscene` exist as `InputMode` targets for `InputMapController.SetMode()`
but carry no bindings yet — each fills in with its own issue (turret emplacement, stretcher
sequence, cutscene lockout).

---

## Control schemes

`Keyboard&Mouse` and `Gamepad` only — trimmed from Unity's default template (which also includes
Touch, Joystick, XR) since none of those apply to this project's WebGL + Xbox + KB/M target.
