# Debug / cheat tools — Mr. Moonlight

Every dev-only toggle and debug overlay in the project, in one place, so nobody has to grep the
codebase to remember what F3 does. **None of this ships** — each tool's own doc comment says so,
and most are flagged for removal once the system they stand in for lands for real (MRM-32 for the
combat toggles, a real HUD for the stat overlays).

Add a row here whenever a new one is built. Owner of the *tool itself* is whoever asked for it;
owner of the *feature it stands in for* is noted separately where relevant.

## Keyboard toggles

| Key | Component | What it does | Why it exists |
|---|---|---|---|
| **F1** | `InputDebugOverlay` (`Runtime/Input/`) | Shows the last key/button/scroll on any device and which Input Action it's bound to | Confirm the Input System is wired correctly before a player prefab exists to react to it (MRM-8) |
| **F2** | `PlayerStatsDebugOverlay` (`Runtime/Player/`) | Text readout of all six MRM-12 stats (health, stamina, speed, melee, defense, audio pitch) | No real stat bars yet — those are the Conformist/Punk difficulty issue's job (MRM-12) |
| **F3** | `InfiniteStaminaDebugToggle` (`Runtime/Player/`) | Locks stamina at max via `Stat.Lock`, so sprint never slows to a walk | Testing traversal/sprint distance between blockout waypoints without stamina cutting a run short |
| **F4** | `InvulnerableDebugToggle` (`Runtime/Player/`) | Blocks player damage at the entry point — health never drops, but every hit is still counted and flashed on screen | The player can't deal/take real damage yet (MRM-32 Backlog) — lets a Spotter fight be watched start to finish instead of ending after two shells |
| **F5** | `HealthRegenDebugToggle` (`Runtime/Player/`) | Call-of-Duty-style regen: after 2s with no hit, health ramps back up (25/s, DOTween-driven) until full or the next hit resets the delay | Recover the red damage-tint quickly between playtest passes, without needing F4 or a bandage every time (2026-09-02) |

All five follow the same shape: a `[SerializeField] private bool` toggle, an `Update()` check
against `Keyboard.current`, and an `OnGUI()` label only while active. Copy that pattern for the
next one rather than inventing a new shape.

## Context-menu tools (not keybound)

| Component | Where | What it does |
|---|---|---|
| `EnemyDebugControls` | Right-click the component header on an enemy, in Play mode | Damage / Kill / Attack Player / Fire Flare Now / Log State — the only way to test half of MRM-34 until the player can deal real damage (MRM-32) |

## DOTween Pro

Adopted 2026-09-02 (Carlos, DOTween Pro — he supplied the Asset Store package). Lives in
`Assets/Plugins/Demigiant/` (DOTween + DOTweenPro + DemiLib, ~2.3 MB; the `DOTweenPro Examples`
demo folder was deleted on import, same call as not bringing in Blaze's `Demos/`). No asmdef
needed or added — `Plugins/` assemblies compile first and are automatically visible to every
other assembly, unlike Blaze/Ian's Fire Pack which both needed one.

**Use it for any new smooth transition** (fades, ramps, VFX intensity/scale) instead of a
hand-rolled `Mathf.Lerp` coroutine — `LampFireEffect`'s fire/light fades and
`HealthRegenDebugToggle`'s regen ramp were converted the same day DOTween landed, as the first
real usage. `SetDelay(...)` on a tween replaces a `WaitForSeconds` + elapsed-time coroutine
outright — reach for that first.

**One known gap, not worth chasing further:** `AudioSource.DOFade` (the Pro Audio module
shortcut) doesn't resolve in this project — `Light.DOIntensity` and other module shortcuts work
fine, just not that one. Workaround, used in `LampFireEffect`: call
`DOTween.To(() => source.volume, v => source.volume = v, target, duration)` directly — it's
exactly what the shortcut does internally, so nothing is lost.

## Known gaps

- **Removal triggers.** F3/F4/F5 and `EnemyDebugControls` are all placeholders for MRM-32 (real
  player damage, hitboxes, damage reactions). When that lands, revisit every row above — most of
  this file should shrink, not grow.
