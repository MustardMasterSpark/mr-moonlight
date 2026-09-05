# Weapon audio system — AudioRandomContainer, 2026-09-04

**Standing rule, Carlos's call:** every sound slot on every ranged/melee weapon uses Unity's native
**`AudioRandomContainer`** (a built-in asset type, not a custom one) — a pool of clips with
weighted-random selection plus pitch/volume randomization per playback. This is the *only* variety
mechanism to use going forward; do not reach for the vendor's `AudioSequence` type for weapon
sounds (see §3 for why).

## 1. How to add a new clip yourself (Carlos: this is the workflow for the sounds you're sourcing)

1. Import the `.wav` into the project (anywhere sensible — `Assets/_Project/Data/PolymindGames/Audio/Wieldables/` for weapon-specific clips is the existing convention).
2. Open the container asset for the slot you want (table in §2) — e.g. `AudioRC_Fire_DBShotgun.asset`.
3. In the Inspector, add your clip to the **Elements** list (or multi-select several clips and use
   **Create > Audio Random Container** if starting fresh — see §4 for the one gotcha there).
4. That's it — no code, no per-weapon wiring, no prefab edits. The container is what every weapon
   already points to.
5. Pitch/volume randomization is already turned on for every container built this session (±75
   cents pitch, -5..0 dB volume, Shuffle playback so it won't repeat the same take twice in a row).
   Tune those ranges on the container itself if a new batch of takes needs a different amount of
   variation.

## 2. Container map — what plays where, right now

| Slot | Weapon(s) | Container | Clips today | Notes |
|---|---|---|---|---|
| Fire | M1911 | `AudioRC_Fire_M1911` | `HQFPS_M1911_Shoot1` | `HQFPS_M1911_Shoot2.wav` sits unused in the same folder — free variety, just add it |
| Fire | Crossbow | `AudioRC_Fire_Crossbow` | `HQFPS_Crossbow_LaunchArrow1` | Same deal — `LaunchArrow2.wav` unused |
| Fire | DBShotgun | `AudioRC_Fire_DBShotgun` | `HQFPS_R870_Shoot1` + `Shoot2` | Was a flare-gun stand-in; **replaced with real shotgun takes 2026-09-04** (MRM-25), see §5 |
| Equip | M1911, DBShotgun | `AudioRC_Equip_Foley5` | 1 clip | Shared generic handling sound |
| Equip | Crossbow | `AudioRC_Equip_Crossbow` | 1 clip | |
| Equip | BaseballBat | `AudioRC_Equip_BaseballBat` | 2 clips | |
| Holster | all 4 weapons | `AudioRC_Holster_Foley4` | 1 clip | Shared across every weapon — editing this affects all of them |
| Reload | M1911 | `AudioRC_Reload_M1911` | 1 clip | |
| Reload | DBShotgun | `AudioRC_Reload_DBShotgun` | 1 clip | |
| Reload | Crossbow | `AudioRC_Reload_Crossbow` | 1 clip | |
| Empty reload | M1911 | `AudioRC_EmptyReload_M1911` | 1 clip | |
| Empty reload | DBShotgun | `AudioRC_EmptyReload_DBShotgun` | 1 clip | Crossbow has no empty-reload state |
| Aim start | all firearms | `FPS_Firearm_Aim` | 2 clips | Pre-existing (shipped with the original HQFPS import), not built this session |
| Dry fire (empty-trigger click) | all firearms | `FPS_Firearm_DryFire` | 1 clip | Also pre-existing. See `mrm9-hqfps-integration.md` §14 for the rule that makes this fire on every empty pull instead of auto-reloading |
| Change mode | Crossbow | `FPS_Firearm_ChangeMode` | pre-existing | |
| Melee attack | BaseballBat | `FPS_Melee_Attack1` / `FPS_Melee_Attack2` | pre-existing | One container per swing in the combo |

> **This table describes the four-weapon loadout of 2026-09-04 (morning).** MRM-25 added nine more
> weapons the same day and built **17 further containers** for them — fire, fire-tail, reload,
> empty-reload, cartridge-swap, throw and equip across the AKM, M1A, R870, Revolver, Hunting Rifle,
> Combat Knife, Fire Axe, Frag Grenade, Molotov and Syringe. The rule, the workflow in §1 and the
> trap in §4 are all unchanged and still apply. The full new-weapon container list is in
> `Docs/mrm25-weapon-test-arsenal.md` §5.

All containers live in `Assets/_Project/Data/PolymindGames/Audio/Wieldables/` (the `AudioRC_*`
ones are new this session; the `FPS_*` ones predate it and already followed this exact pattern —
this session extended the convention to fire/equip/holster/reload, it didn't invent it).

**Still genuinely silent** — no clip assigned, nothing to swap, worth sourcing if wanted:
Aim stop (all firearms), Fire tail (M1911/DBShotgun/Crossbow — see below for what this is),
Eject (M1911, shell-casing-hits-ground), Swap cartridge (DBShotgun/Crossbow, break-action
mechanism), Hit/impact (BaseballBat connecting with something).

**"Fire tail"** — a second sound slot on the firearm's barrel-effect component that plays a beat
*after* the main gunshot (timed to recoil-recovery settling, not simultaneous with it). The idea:
a real gunshot has a sharp "crack" transient right at the muzzle plus a longer, lower "tail" — the
pressure wave rolling out and echoing across terrain. Splitting them lets the tail carry the
environment (wilderness echo, in this game's case) independently of the crack, which is what
matters for gameplay punch. Purely optional/atmospheric — nothing currently uses it.

## 3. Why AudioRandomContainer and not the vendor's AudioSequence

Two systems existed in the vendor code before this session, and it matters which one is used:

- **`AudioSequence`** (`_clips[]` array + `_pitch`/`_randomness` fields) — despite the name, this
  does **not** pick one random clip. It plays every clip in the array, back-to-back, at authored
  delay offsets — built for a multi-stage *layered* sound (e.g. a two-part mechanical clack), not
  for repetition-fatigue variety. Its own code actively rejects `AudioRandomContainer` references
  dropped into its clip list (a `[ClampClipDelays]` editor callback strips them with a warning) —
  the two systems are mutually exclusive by design.
- **`AudioRandomContainer`** (Unity's built-in type) — genuine weighted-random selection plus a
  real pitch/volume randomization *range* per playback. This is what Carlos asked to standardize
  on, and it's what the vendor's own aim-start/dry-fire/change-mode/melee-attack sounds already
  used before this session — this work just extended the same pattern to fire/equip/holster/reload,
  which had been on `AudioSequence` (in practice with exactly one clip each, so nothing about
  actual behavior was lost by switching).

**Code changed to make the switch possible** — `_equipAudio`/`_holsterAudio`/`_reloadAudio`/
`_emptyReloadAudio`/`_ejectAudio` fields were retyped from `AudioSequence` to `AudioData` (the
type that already accepts an `AudioRandomContainer`), in `Wieldable.cs` (the base class for *every*
wieldable, not just weapons — equip/holster live there), `FirearmBasicReloadableMagazine.cs`, and
`FirearmBasicShellEjector.cs`. Fire sounds (`_fireAudio`) didn't need a type change, only a clip
assignment — they were already the right type. One minor behavior change: holster audio lost its
old per-clip delay/speed-scaling timing (nothing was actually using multi-clip sequencing there, so
nothing audible changed).

## 4. Building a container from script — the one real trap

If a container ever needs to be built or edited via code/tooling again (not just dragging clips in
the Inspector): `AudioRandomContainer`'s and `AudioContainerElement`'s constructors are
**internal**, and `ScriptableObject.CreateInstance` and `UnityEditor.ObjectFactory.CreateInstance`
both fail on it silently or with "constructor is not accessible." The only working path found is
Unity's own creation flow: `UnityEditor.ProjectWindowCallback.DoCreateAudioRandomContainer` — set
its `selection` field to the `AudioClip[]` to pool, call its (internal)
`CreateAudioRandomContainerFromSelectedClips(string path)` method. That's literally what "select
clips → right-click → Create Audio Random Container From Selection" does in the Editor; going
through it (even via reflection) produces a real, valid asset. A raw `Activator.CreateInstance`
bypass produces a broken one (`'AudioRandomContainer' is missing the class attribute
'ExtensionOfNativeClass'` on load) — found and discarded during this session's work, mentioned here
so nobody re-discovers it the hard way.

**Separately:** a prefab edited by hand-writing YAML (not through the Editor UI) can keep showing
stale/null values for a newly-created reference until it's force-reimported —
`AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate)` fixed this during verification.
Not an AudioRandomContainer-specific issue, just worth knowing if a future direct-YAML edit doesn't
seem to take.

## 5. ~~Open~~ CLOSED 2026-09-04: DBShotgun's fire sound was a stand-in

> **Resolved by MRM-25.** The R870 pump shotgun came across on 2026-09-04, bringing the first real
> shotgun report the project owns. `AudioRC_Fire_DBShotgun` now holds `HQFPS_R870_Shoot1` and
> `HQFPS_R870_Shoot2` instead of the flare-gun clip — same 12 gauge, different action, far closer
> than a flare gun. Still not a *double-barrel* recording, so replace it if real takes are ever
> sourced; but it is no longer a placeholder from a different weapon class.
> See `Docs/mrm25-weapon-test-arsenal.md` §5.

### Original entry, for context

`AudioRC_Fire_DBShotgun` currently holds `HQFPS_FlareGun_Shoot.wav` — the vendor's HQ FPS Weapons
2.0 pack never shipped a dedicated double-barrel-shotgun fire sound at all (confirmed: only
`EmptyReload`/`TacticalReload` exist for it, in both the Playground and Weapons source projects —
this isn't a missed migration, the file genuinely doesn't exist anywhere owned). The flare-gun
sound was picked as a closer mechanical match than the pistol's crack (also break-action, similar
report character), at 2x volume per Carlos's request. **Replace `AudioRC_Fire_DBShotgun`'s element
with real shotgun takes as soon as they're sourced** — per §1, that's a pure asset swap, no code.
