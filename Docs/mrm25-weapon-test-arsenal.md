# MRM-25 — Weapon test arsenal: thirteen weapons on number keys

**Branch:** `mrm-25` (off `main` at `16f6b3c`, 2026-09-04)
**Builds on:** `Docs/mrm9-hqfps-integration.md` (the controller swap), `Docs/weapon-audio-system.md`

Carlos's brief, 2026-09-04: bring nine more weapons across from the Weapons project, make them
fully functional, put them on number keys by category, give the player all of them at once with
infinite ammo, and move body lean onto Q/E.

> **This whole arrangement is a testing scaffold.** Carlos: *"This method of swapping weapons by
> numbers is just for testing purposes. In the future this will change, of course, with the actual
> game modes, but it would be nice to remember this distribution of weapons and infinite ammo
> capabilities for testing purposes."* §9 is the record of how to switch it back on after MRM-26's
> pickups replace it.

---

## 1. What the player has now

Thirteen weapons plus the Syringe, all in the holster from spawn. **Slot 0 is the Combat Knife** —
Carlos: *"From the start the player will always spawn with the combat knife."*

| Key | Category | Weapons, in press order |
|---|---|---|
| **1** | Melee | Combat Knife → Fire Axe → **the Club** |
| **2** | Pistols | M1911 → Revolver |
| **3** | Shotguns | R870 → Double Barrel |
| **4** | Rifles | M1A (M14) → AKM |
| **5** | Precision | Crossbow → Hunting Rifle (scoped) |
| **7** *or* **G** | Throwables | Frag Grenade → Molotov Cocktail |
| **H** | — | Use the Syringe (heals; see §6) |

"The Club" is the BaseballBat asset — `Docs/glossary.md`, ruled 2026-08-28. "Trench Club" in
Carlos's message is the superseded name for the same object.

**Pressing a key you are already in advances one weapon and wraps.** Pressing a key you are not in
equips that category's **first** weapon. There is a `rememberLastInCategory` toggle on the component
for last-used-instead; it is off because predictable beats clever when the point is A/B testing.

### Other keys this changed

| Key | Was | Now | Why |
|---|---|---|---|
| **Q / E** | Q = next weapon | **Lean left / right** | Carlos: *"The new keys will replace Q. And now we will use the keys Q and E."* |
| **F** | Flashlight | **Interact** | E was Interact and had to move. F was the nearest free, conventional key |
| **L** | — | **Flashlight** | Displaced by Interact |
| **H** | — | **Heal** | Makes the Syringe work at all (§6) |
| Right bumper | Next weapon | unchanged | The only weapon control a controller still has |

**Keyboard only, deliberately.** Carlos: *"all of these new keys obviously don't have an equivalent
on the controller so don't try to integrate those... as well the Q and E functions to tilt, won't be
available for a controller."* No category key, lean or heal binding carries a gamepad path. The
gamepad keeps the right-bumper cycle so a controller is not left unable to change weapon at all.

---

## 2. Where everything lives

Carlos asked for the weapon set to be stored *"in an orderly fashion within the project."* It is:

```
Assets/_Project/Prefabs/Weapons/        <- TRACKED IN GIT. 14 prefab variants.
  Melee/      MRM_Weapon_CombatKnife, MRM_Weapon_FireAxe, MRM_Weapon_Club
  Pistol/     MRM_Weapon_M1911, MRM_Weapon_Revolver
  Shotgun/    MRM_Weapon_R870, MRM_Weapon_DBShotgun
  Rifle/      MRM_Weapon_M1A, MRM_Weapon_AKM
  Precision/  MRM_Weapon_Crossbow, MRM_Weapon_HuntingRifle
  Throwable/  MRM_Weapon_FragGrenade, MRM_Weapon_Molotov
  Item/       MRM_Item_Syringe
```

Each is a **prefab variant** of the corresponding vendor `HQFPS_Wieldable_*`, carrying only Mr.
Moonlight's overrides: damage, infinite reserve ammo, audio containers, and the crossbow's descope.

**Why variants, and why this matters.** `Assets/ThirdParty/` is **entirely git-ignored** by project
policy (`.gitignore` line 256). Before this issue every weapon override the project had made was
therefore untracked — a fresh clone had no record of them at all. Putting our layer in `_Project`
as variants fixes that, keeps our changes visibly separate from vendor data, and still inherits
vendor fixes automatically.

Raw art and audio deliberately stay in `Assets/ThirdParty/PolymindGames/`:

| What | Where |
|---|---|
| Meshes + textures | `HQFPS/Art/Meshes/Wieldables/<Weapon>/` |
| Weapon audio | `HQFPS/Audio/SFX/Wieldables/<Weapon>/` |
| Vendor wieldable prefabs (the variant bases) | `HQFPS/Prefabs/Wieldables/` |
| The 5x scope | `FPSCore/Art/Models/Attachments/SniperScope1/` |
| Item definitions | `_Project/Data/PolymindGames/Resources/Definitions/Item/` (tracked) |
| Audio containers | `_Project/Data/PolymindGames/Audio/Wieldables/` (tracked) |

That split is `Docs/unity-conventions.md`'s rule, not a preference — and moving the binaries would
break the GUID links that make the entire vendor prefab set resolve.

---

## 3. The three tools, and the order they run in

All under **Tools > MrMoonlight > Weapons**. All re-runnable and idempotent.

1. **Build Weapon Library** (`MoonlightWeaponSetBuild.BuildLibrary`) — makes the 14 variants,
   applying damage from `MoonlightTunables` and swapping every firearm's reserve-ammo provider.
2. **Upgrade Player_Tracey** (`MoonlightWeaponSetBuild.UpgradePlayer`) — points the player at the
   variants, restores lean, adds the healing handler and scope overlay, resizes the holster, and
   writes the loadout and category map.
3. **Build + wire weapon audio** (`MoonlightWeaponAudioBuild.Run`) — creates the
   `AudioRandomContainer` assets and assigns them.

`MoonlightWeaponSet.cs` holds the single description of the weapon set — identity, category, order.
Everything else reads it, so the loadout and the number keys cannot drift apart.

### Why the player is upgraded in place rather than rebuilt

`PolymindPlayerBuild` (MRM-9's tool) deletes and recreates `Player_Tracey.prefab` from the vendor
source, which **breaks the link from the scene instance**. As of 2026-09-04 the `Island.unity`
instance carries **nine added components and two added GameObjects that exist nowhere in the
prefab**: `DeathSequence` with its death-scream pool, black-screen image and game-over panel;
`HealthRedTintSource`; four debug overlays with their font; `PauseController`; the damage-numbers
overlay camera; and a `Death Scream Source`. A rebuild drops every one of them and the resulting
nulls fail *silently* at runtime — the exact failure `mrm9-hqfps-integration.md` §9b catalogues.

`PolymindPlayerBuild` was still updated to stay correct (it now keeps all fifteen wieldables, keeps
lean, maps Lean/Heal, resizes the holster and applies damage), so the from-scratch path remains a
valid disaster-recovery route. It was **not run**. If it ever is, the scene instance has to be
replaced and `Event Director.player` re-pointed.

---

## 4. Art pipeline

`Tools/pipeline/migrate_hqfps_weapons.py` brought the art across — 10 weapon folders, the 5x scope
and the Molotov's lighter flame — applying the standard two-map pass:

- **BaseColor**: AO multiplied in, quantised to 10 levels with 4×4 Bayer dither, resampled to
  **512** nearest-neighbour, imported Point-filtered.
- **Normal**: resampled to **256** Lanczos, **never quantised or dithered** (Carlos's standing rule).
- `_AO`, `_MET`, `_MaskMap`: dropped. RetroLit has no slot for them.

Per texture: ~22 MB → ~170 KB. The whole weapon art tree is now 100 MB.

**Every `.meta` is copied verbatim, so GUIDs are preserved** — which is why all 21 vendor wieldable
prefabs, 8 magazines, 8 shells and 21 pickups relinked with zero fixup. Verified: **0 null meshes,
0 null materials, 0 missing scripts across all 15 wieldables.**

World materials are **rebuilt** on RetroLit from Mr. Moonlight's own `M1911.mat` as a template,
because the vendor's world shader GUID does not exist in this project and a straight copy renders
magenta. FP (first-person) materials are copied verbatim — their `Shader Graphs/LitFieldOfView`
does exist here and carries the viewmodel FOV warp RetroLit has no equivalent for.

### Fixed on the way past: the vertex wobble was live on two existing weapons

RetroLit's `_SnapMode` enum is `Object(0) World(1) View(2) Off(3)`, and the shader branches on the
**keyword**, not the int. `M1911.mat` and `Crossbow.mat` were on `_SNAPMODE_VIEW` — i.e. the vertex
snapping wobble Carlos asked us not to repeat (`mrm9-hqfps-integration.md` §3) was *actually
happening* on the pistol and the crossbow. `BaseballBat.mat` and `DBShotgun.mat` had the `_OFF`
keyword but an inconsistent `_SnapMode: 2`. All four corrected to `_SnapMode: 3` + `_SNAPMODE_OFF`,
and all new materials built that way.

---

## 5. Audio

Every new weapon's sound goes through Unity's native `AudioRandomContainer`, per the standing rule
in `Docs/weapon-audio-system.md`. **17 containers created, 38 slots wired.**

**Five firearms were completely silent when fired** — Revolver, Hunting Rifle, AKM, M1A, R870.
Cause: MRM-9 retyped `_equipAudio`/`_holsterAudio`/`_reloadAudio`/`_emptyReloadAudio`/`_ejectAudio`
from `AudioSequence` to `AudioData`, and **retyping a serialized field discards what it held**. Only
the four weapons hand-re-authored at the time survived. `_fireAudio` was empty for a different
reason — the clips simply were not in the project.

### The double barrel's stand-in fire sound is replaced

`weapon-audio-system.md` §5 left this open: HQ FPS ships no double-barrel fire clip at all, so the
DBShotgun had been firing a **flare gun** sound. The R870 arriving with this issue is the first real
shotgun report the project owns, and `AudioRC_Fire_DBShotgun` now holds
`HQFPS_R870_Shoot1`/`Shoot2`. Same 12-gauge, different action — far closer than a flare gun. **§5 of
that document is now closed.**

### Still genuinely silent (no clip exists to assign)

Aim-stop, fire tail on most weapons, shell eject, cartridge swap, melee hit impact, and the
Crossbow's empty-reload (which has no state to fire it). The Revolver's empty-reload reuses its one
reload take — loading a revolver from empty and topping it up are the same action.

### Creating containers from script — the trap, already paid for

`AudioRandomContainer`'s and `AudioContainerElement`'s constructors are `internal`.
`ScriptableObject.CreateInstance` and `ObjectFactory.CreateInstance` both fail; a raw
`Activator.CreateInstance` produces an asset that throws *"missing the class attribute
'ExtensionOfNativeClass'"* on load. The only working route is Unity's own creation flow —
`UnityEditor.ProjectWindowCallback.DoCreateAudioRandomContainer.CreateAudioRandomContainerFromSelectedClips`,
reached by reflection. `MoonlightWeaponAudioBuild` does this and reports clearly if the API ever
changes shape.

---

## 6. The Syringe — what it is, and why it appeared to do nothing

Carlos asked whether it is a prop. **It is fully functional**, and the reason it seemed inert is
worth recording because it is not obvious from the asset.

`HQFPS_Wieldable_Syringe` is a `HealingWieldable`. It heals a random 40–50 over 2 seconds and slows
the player to 0.75× while doing it. But **it has no input of its own**: its `Use()` only *cancels* a
heal in progress. The single call site that ever *starts* one is
`WieldableHealingHandler.TryHeal()`, raised by `FPSWieldablesInput` from the **Heal** action.

Three things were in the way, all now fixed:

1. **The Heal action was bound to nothing.** MRM-9 sank it into the bindingless `Unbound` action
   along with six other vendor actions it had no equivalent for. Now bound to **H**.
2. **`WieldableHealingHandler` was not on the player.** It is not on the vendor's own `FPS_Player`
   either — which is why the Syringe looks inert *in the Weapons project too*. This is not something
   MRM-9 dropped. It is now added by the upgrade tool, with `_containerTags` set to the **Wieldable**
   tag (a container's tags come from its `TagContainerRestriction`, and the Holster's
   `FPS_Restriction_Wieldable` is what carries that tag).
3. **The Syringe could not enter the Holster.** Its item definition shipped **untagged** (tag id 0)
   while every weapon carries the Wieldable tag `6549466`, so the Holster's tag restriction refused
   it — reporting the thoroughly misleading *"Inventory Is Full"* with 3 slots free. Tagged.

**How to use it:** press **H**, from any weapon. The handler equips the Syringe itself, plays the
heal, and holsters it again — the player never selects it manually, which is why it is on no number
key. It does nothing at full health (`TryHeal` checks `IsFullHealth`).

---

## 7. Scope, sights and attachments

Attachments are chosen by an **item property** on the held item instance — `Sight Attachment`
(id `-2298519`), where `0` = the default iron sight and `3098573` = the Sniper Scope 5x item. The
attachment component reads it when the item is attached to its holster slot.

| Weapon | Shipped as | Now | Why |
|---|---|---|---|
| Hunting Rifle | `1` — matches **neither** config, so no attachment applied | `3098573` | Carlos: *"The hunting rifle must be with the scope version."* The shipped `1` looks like a vendor data slip |
| M1A | `3098573` — **scoped by default** | `0` | Carlos, 2026-09-04: *"Make sure that the M1A (M14) doesn't have the scope."* |
| Crossbow | `0` | `0` | Unchanged. Carlos ruled "crossbow without scope" on MRM-9 |

**The 5x scope art had to be restored.** MRM-9 deleted `SniperScope1` outright during the crossbow
descope. Worse, `PolymindPlayerBuild.RemoveCrossbowScopes()` matched **any** object named
`SniperScope` anywhere under the player — so it would have deleted the Hunting Rifle's scope on
every rebuild, and the symptom would have been near-invisible (the rifle silently falling back to
iron sights). Now scoped to the crossbow's own subtree.

The crossbow's scope is **deactivated** in its variant rather than deleted: a prefab variant cannot
delete an inherited GameObject, only disable it. Nothing is lost — the attachment system already
picks the iron sight from the item property, and the scope mesh ships anyway for the Hunting Rifle.

### The scope overlay UI

Carlos: *"When I aim to use the scope on the Hunting Rifle I didn't see the image of the scope
lines, nor the black effect that only leaves us with a circle in the middle."*

The scope itself was working — `FirearmScopedAimHandler` was zooming correctly. The **overlay is a
UI element**, and MRM-9 migrated none of the vendor UI. It lives in `FPS_UI_Wieldables.prefab`:
a `Scope` branch holding the `FPS_ScopeOverlay` lens sprite plus two black border images.

It is now instantiated as `MRM_UI_Scope` **under the player prefab**, not under the HUD Canvas.
`ScopeControllerUI` is a `CharacterUIBehaviour`: its `Awake` looks for an `ICharacterUI` *in a
parent* and gives up if there is none. Hanging the canvas off the player with `CharacterUI` set to
`ToParentCharacter` makes it resolve with **no scene wiring at all** — nothing to re-point if the
player is ever re-instanced.

Canvas sorting order is **-1**, deliberately below the HUD Canvas at 0: the scope surround is
full-screen black and at a higher order would hide the damage tint, system messages and game-over
panel.

Verified live: `ScopeUI 'Scope 01' activeSelf=True alpha=1` while aiming, plus a screenshot at
`Assets/Screenshots/mrm25_hunting_rifle_scope.png`.

> **The same vendor prefab also carries an ammo counter, fire-mode indicator, crosshair set,
> hitmarker and reload bar** — all still unmigrated (`mrm9-hqfps-integration.md` §10 item 4). They
> are one edit away in `AddScopeOverlayUI`. Not done: not asked for.

---

## 8. Damage and ammunition

All thirteen weapons' damage now lives in `MoonlightTunables` under **Weapon damage — MRM-25**, and
is pushed onto the variants on every library build. The numbers are HQ FPS's own shipped values, not
invented ones — they are already internally consistent, and three were the live numbers before this
issue. Carlos: *"For now assign the values for damage that you consider. We will later fine-tune."*
Editing a tunable and re-running tool 1 is the whole tuning loop.

| Weapon | Damage | Magazine | Weapon | Damage | Magazine |
|---|---|---|---|---|---|
| Combat Knife | 20–25 / swing | — | M1A | 45 | 7 |
| Fire Axe | 35–40 / swing | — | AKM | 40 | 30 |
| Club | 25–30 / swing | — | Crossbow | 40 | 1 |
| M1911 | 30 | 8 | Hunting Rifle | 80 | 5 |
| Revolver | 50 | 6 | R870 | 15 / pellet ×8 | 7 |
| Double Barrel | 20 / pellet ×8 | 2 | | | |

Firearm damage is **per projectile, before** the hitbox zone multipliers (limb ×1, torso ×2,
head ×4 — MRM-76).

**Infinite ammo, finite chamber.** Every firearm's `FirearmInventoryAmmoProvider` is swapped for
`FirearmInfiniteAmmoProvider` in its variant. Magazines are untouched, so reloading is still
required — and per `mrm9-hqfps-integration.md` §14, reload is always **manual**: pulling the trigger
on empty dry-fires forever until the player reloads deliberately.

**Throwables** have no such seam — `MeleeThrowAttack` calls `AdjustStack(-1)` straight against the
holster slot. `MoonlightInfiniteThrowables` listens to the container's `SlotChanged` and refills in
the same frame the throw consumed it. **The ceiling is the item definition's, not ours:** HQ FPS
gives both throwables `StackSize = 3`, so a request for 99 produces a stack of 3 (measured). The
target is clamped to the definition's own stack size. If a bigger visible count is ever wanted that
is one field on `HQFPS_Frag Grenade.asset`, not a code change.

**Ammo items** need no import — all eight ammunition item definitions (`.45 ACP`, `.357M`, `.300WM`,
`5.56×45`, `7.62×39`, `7.62×51`, `12 Gauge Shell`, `Bolt`), all 8 magazine prefabs and all 8 shell
prefabs were already in the project from MRM-9's wholesale Data/Prefabs migration.

---

## 9. Restoring this mode after MRM-26 replaces it

When weapon pickups land, this comes off. To bring it back for testing:

1. On `Player_Tracey`, re-enable `MoonlightStartingLoadout`,
   `MoonlightWeaponCategorySwitcher` and `MoonlightInfiniteThrowables`.
2. Run **Tools > MrMoonlight > Weapons > 1** and **2**. Step 2 rewrites the loadout and category map
   from `MoonlightWeaponSet.cs`, so no list has to be retyped.
3. Swap each firearm's ammo provider back to `FirearmInfiniteAmmoProvider` — step 1 does this.

To turn it **off** for a real game mode: remove the three components. The weapon variants, damage,
audio and scope work stay — none of them are testing-only.

---

## 10. What was verified live, and what was not

Verified in Play mode on `Island.unity`, 2026-09-04:

- Holster holds all **14** items; player spawns on **slot 0, Combat Knife**; health 100/100.
- **Every category key cycles and wraps correctly** — driven through the switcher and read back:
  melee `Knife → Axe → Bat`, pistols `M1911 → Revolver → M1911`, shotguns `R870 → DB → R870`,
  rifles `M1A → AKM → M1A`, precision `Crossbow → Hunting Rifle → Crossbow`, throwables
  `Frag → Molotov → Frag`.
- All 8 firearms report **INFINITE** reserve ammo with magazine sizes intact.
- Damage values match the tunables on every weapon.
- Lean restored, wired to our `Gameplay/Lean`, and `SetLeanState(Left)` takes.
- Scope overlay shows while aiming (`activeSelf=True, alpha=1`) — screenshot captured.
- Hunting Rifle scope active, M1A and Crossbow on iron sights.
- Healing handler present, `HealsCount = 1`.
- **Zero** input references still pointing at the vendor action asset.
- Scene instance intact: all 9 added components, both added GameObjects, every `DeathSequence`
  reference, `Event Director.player`, and the Northern Lights culling mask (41795635).
- Compiles clean; console shows only the two known pre-existing warnings.

**Not verified — needs Carlos hands-on:**

- How the weapons *feel*. Damage is a spreadsheet until someone fights with it.
- Whether Q/E lean feels right (cooldown 0.2 s, obstruction mask is the vendor's).
- Whether F-for-Interact / L-for-Flashlight is acceptable, or Interact should go elsewhere.
- Melee reach and swing timing on the Fire Axe and Combat Knife.
- Grenade and Molotov throw arcs, explosion damage, and the Molotov fire pool.
- Per `verification_requires_a_build`: none of this is confirmed on a built exe yet.

---

## 11. Open items and things found on the way

1. **`PlayerDamageReceiver` exists twice** on the scene player — once on the root and once on
   `MrMoonlight Systems`. MRM-9 §9b moved it to the root because `GetComponentInParent` needed it
   there; the child copy is a leftover. Harmless today (the colliders are on the root, so the root
   copy is found first) but it is a live duplicate on a damage path. **Not touched** — it predates
   this issue and removing it deserves its own test.
2. **The AKM is full-auto** by default (`FirearmIndexBasedAttachments`, index 0 =
   `Mode_FullAutoTrigger`). The fire-mode switch is the vendor `FireMode` action, still sunk into
   `Unbound`, so semi-auto is currently unreachable. Bind it if wanted.
3. **`Weapon_DoubleBarrelShotgun.prefab` and `Weapon_FlareGun.prefab`** sit loose at the root of
   `_Project/Prefabs/Weapons/` from earlier issues. Left alone, but they do not follow the new
   `MRM_Weapon_*` convention and should probably be filed or removed.
4. **The vendor ammo/crosshair/fire-mode UI** is still unmigrated (§7).
5. `Island.unity`'s diff includes 174 `m_hasErrors` flag flips on Gaia spawner rules and the known
   `Gaia.NoiseSettings` YAML reorder — cosmetic, same as the MRM-9 session, flagged not hidden.
6. **`TimeOfDayDebugCycle` still auto-rotates** through all four time-of-day presets during play,
   which is why weapon screenshots come out very dark. Unchanged from MRM-9 §13.

---

## 12. Traps worth remembering

- **A malformed GUID kills the entire `.inputactions` asset, silently.** A generated binding id of
  `aaaaaaa5-25-...` (second group only 2 hex digits) made Unity's importer reject the whole file:
  *"Could not parse input actions in JSON format... Guid should contain 32 digits with 4 dashes."*
  The asset still existed, the generated C# wrapper still compiled against the **old** file, and
  every `InputActionReference` sub-asset silently resolved `.action` to null. The patch script now
  validates every id it writes with `uuid.UUID()`.
- **Changing a C# field initializer does not change an existing prefab.** `MoonlightStartingLoadout`
  kept its serialized four-weapon list from MRM-9 no matter what the initializer said; the player
  went on spawning with the old loadout. Anything that must actually change on an existing prefab
  has to be written through `SerializedObject` — which is what `ConfigureLoadout` does.
- **Matching an input action by name is not enough.** The vendor asset also contains an action
  called `Lean`, so a freshly pasted `FPSBodyLeanInput` looked correct by name while still pointing
  at the vendor asset MRM-9 worked hard to leave unreferenced. Compare by object identity.
- **A container rejecting an item reports "Inventory Is Full" regardless of the real reason.** The
  Syringe was refused by a *tag* restriction with three slots free. Check restrictions before
  capacity.
- **Read the frame before believing it.** An attachment check run in the same frame as
  `SelectAtIndex` reported every scope inactive, including the Hunting Rifle's — the equip had not
  finished. Re-reading a moment later showed the correct state. Same lesson as
  `mrm9-hqfps-integration.md` §9b's equip-animation screenshot.
- **Retyping a serialized field discards its data.** Five weapons came across mute because of a
  field type change made months earlier for good reasons. When retyping, audit every asset using it.

---

## 13. Files

**New code**
- `Assets/_Project/Code/Runtime/Player/MoonlightWeaponCategorySwitcher.cs`
- `Assets/_Project/Code/Runtime/Player/MoonlightInfiniteThrowables.cs`
- `Assets/_Project/Code/Editor/Migration/MoonlightWeaponSet.cs`
- `Assets/_Project/Code/Editor/Migration/MoonlightWeaponSetBuild.cs`
- `Assets/_Project/Code/Editor/Migration/MoonlightWeaponAudioBuild.cs`
- `Tools/pipeline/migrate_hqfps_weapons.py`
- `Tools/pipeline/patch_input_actions_mrm25.py`

**Changed**
- `Assets/InputSystem_Actions.inputactions` (+ its generated wrapper)
- `Assets/_Project/Code/Runtime/Data/MoonlightTunables.cs` — weapon damage block
- `Assets/_Project/Code/Runtime/Player/MoonlightStartingLoadout.cs`
- `Assets/_Project/Code/Editor/Migration/PolymindPlayerBuild.cs`
- `Assets/_Project/Prefabs/Player/Player_Tracey.prefab`
- `Definitions/Item/HQFPS_Hunting Rifle.asset`, `HQFPS_M1A.asset`, `HQFPS_Syringe.asset`
- `Assets/_Project/Scenes/Island.unity`

**New assets** — 14 weapon prefab variants, 18 `AudioRC_*` containers, 1 screenshot.
