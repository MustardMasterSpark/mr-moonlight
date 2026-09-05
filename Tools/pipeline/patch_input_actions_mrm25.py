#!/usr/bin/env python3
"""
MRM-25 — add the weapon-category, lean and heal bindings to InputSystem_Actions.

Carlos's brief, 2026-09-04:

  * Number keys pick a weapon CATEGORY; pressing the same key again walks the
    weapons inside it. 1 melee / 2 pistols / 3 shotguns / 4 rifles /
    5 precision / 7 (and G) throwables.
  * "The new keys will replace Q. And now we will use the keys Q and E" - Q/E
    become body lean, which HQ FPS already implements (FPSBodyLeanInput +
    BodyLeanHandler) as a single 1D axis action reading -1 / 0 / +1.

Two knock-on rebinds this forces, flagged rather than done silently:

  * Interact was on E. It moves to F.
  * FlashlightToggle was on F. It moves to L.

Also binds Heal to H. The vendor's Heal action drives WieldableHealingHandler
.TryHeal(), which is the ONLY thing that ever calls HealingWieldable.Heal() -
MRM-9 routed it to the dead `Unbound` sink, which is why the Syringe appears to
do nothing when equipped.

Q on SwitchWeapon is removed; the gamepad right shoulder keeps it, so the old
next-weapon cycle still exists on a controller.

Idempotent: re-running does nothing if the actions are already present.
"""

from __future__ import annotations

import io
import json
import sys
from pathlib import Path

ASSET = Path("E:/MrMoonlight/Assets/InputSystem_Actions.inputactions")
KBM = ";Keyboard&Mouse"
PAD = ";Gamepad"

# id prefix so these are recognisably MRM-25's, and stable across re-runs.
#
# These MUST be well-formed GUIDs — 8-4-4-4-12 hex digits. Unity's InputActionImporter parses the
# whole file with System.Guid and rejects it wholesale on the first malformed id:
#   "Could not parse input actions in JSON format ... Guid should contain 32 digits with 4 dashes"
# The failure is quiet in the worst way: the .inputactions asset still exists, the C# wrapper still
# compiles against the OLD generated file, and every InputActionReference sub-asset silently
# resolves its .action to null. Cost an hour the first time. Do not hand-shorten these groups.
PFX = "aaaaaaa5"

# action name -> (keyboard key, gamepad path or None)
BUTTON_ACTIONS = [
    ("WeaponCategoryMelee",      "<Keyboard>/1", None),
    ("WeaponCategoryPistol",     "<Keyboard>/2", None),
    ("WeaponCategoryShotgun",    "<Keyboard>/3", None),
    ("WeaponCategoryRifle",      "<Keyboard>/4", None),
    ("WeaponCategoryPrecision",  "<Keyboard>/5", None),
    # Carlos: "key number 7 (or G)". Both, so either works.
    ("WeaponCategoryThrowable",  "<Keyboard>/7", None),
    ("WeaponCategoryThrowable",  "<Keyboard>/g", None),
    ("Heal",                     "<Keyboard>/h", None),
]

REBIND = {
    # action        old path            new path
    "Interact":         ("<Keyboard>/e", "<Keyboard>/f"),
    "FlashlightToggle": ("<Keyboard>/f", "<Keyboard>/l"),
}

REMOVE = [("SwitchWeapon", "<Keyboard>/q")]


def uid(n: int) -> str:
    """A stable, well-formed GUID for MRM-25's actions and bindings: 8-4-4-4-12 hex."""
    guid = f"{PFX}-2500-4025-8025-{n:012d}"
    # Cheap self-check, because getting this wrong breaks the entire asset silently.
    import uuid as _uuid
    _uuid.UUID(guid)
    return guid


def main() -> int:
    if not ASSET.is_file():
        print(f"Not found: {ASSET}")
        return 1

    with io.open(ASSET, encoding="utf-8") as fh:
        doc = json.load(fh)

    gameplay = next(m for m in doc["maps"] if m["name"] == "Gameplay")
    existing_actions = {a["name"] for a in gameplay["actions"]}
    changed = []
    n = 0

    # --- new button actions -------------------------------------------------
    for name, key, pad in BUTTON_ACTIONS:
        n += 1
        if name not in existing_actions:
            gameplay["actions"].append({
                "name": name,
                "type": "Button",
                "id": uid(n),
                "expectedControlType": "Button",
                "processors": "",
                "interactions": "",
                "initialStateCheck": False,
            })
            existing_actions.add(name)
            changed.append(f"action  + {name}")

        already = any(b.get("action") == name and b.get("path") == key for b in gameplay["bindings"])
        if not already:
            n += 1
            gameplay["bindings"].append({
                "name": "", "id": uid(n), "path": key,
                "interactions": "", "processors": "", "groups": KBM,
                "action": name, "isComposite": False, "isPartOfComposite": False,
            })
            changed.append(f"bind    + {name} -> {key}")
        if pad and not any(b.get("action") == name and b.get("path") == pad for b in gameplay["bindings"]):
            n += 1
            gameplay["bindings"].append({
                "name": "", "id": uid(n), "path": pad,
                "interactions": "", "processors": "", "groups": PAD,
                "action": name, "isComposite": False, "isPartOfComposite": False,
            })
            changed.append(f"bind    + {name} -> {pad}")

    # --- Lean: one 1D axis, negative = Q (left), positive = E (right) -------
    # FPSBodyLeanInput reads this as a float and CeilToInt's it straight into
    # BodyLeanState (Left = -1, Center = 0, Right = 1), so it must be a Value
    # action with an Axis control type, not a Button.
    if "Lean" not in existing_actions:
        gameplay["actions"].append({
            "name": "Lean",
            "type": "Value",
            "id": uid(900),
            "expectedControlType": "Axis",
            "processors": "",
            "interactions": "",
            "initialStateCheck": True,
        })
        gameplay["bindings"].extend([
            {"name": "LeanKeys", "id": uid(901), "path": "1DAxis",
             "interactions": "", "processors": "", "groups": "",
             "action": "Lean", "isComposite": True, "isPartOfComposite": False},
            {"name": "negative", "id": uid(902), "path": "<Keyboard>/q",
             "interactions": "", "processors": "", "groups": KBM,
             "action": "Lean", "isComposite": False, "isPartOfComposite": True},
            {"name": "positive", "id": uid(903), "path": "<Keyboard>/e",
             "interactions": "", "processors": "", "groups": KBM,
             "action": "Lean", "isComposite": False, "isPartOfComposite": True},
        ])
        changed.append("action  + Lean (1DAxis: Q = left, E = right)")

    # --- rebinds ------------------------------------------------------------
    for action, (old, new) in REBIND.items():
        for b in gameplay["bindings"]:
            if b.get("action") == action and b.get("path") == old:
                b["path"] = new
                changed.append(f"rebind    {action}: {old} -> {new}")

    # --- removals -----------------------------------------------------------
    before = len(gameplay["bindings"])
    for action, path in REMOVE:
        gameplay["bindings"] = [
            b for b in gameplay["bindings"]
            if not (b.get("action") == action and b.get("path") == path)
        ]
    if len(gameplay["bindings"]) != before:
        changed.append("remove    SwitchWeapon <- <Keyboard>/q (gamepad bumper kept)")

    if not changed:
        print("Already patched — nothing to do.")
        return 0

    # Write temp-then-replace: a truncating write once destroyed a doc on this
    # project (see the scoped-cleanup-and-safe-writes lesson).
    tmp = ASSET.with_suffix(".inputactions.tmp")
    with io.open(tmp, "w", encoding="utf-8", newline="\n") as fh:
        json.dump(doc, fh, indent=4)
        fh.write("\n")
    tmp.replace(ASSET)

    print("\n".join(changed))
    print(f"\n{len(changed)} change(s) written to {ASSET.name}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
