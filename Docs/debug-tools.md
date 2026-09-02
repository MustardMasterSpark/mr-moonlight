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
| **F6** | `SceneEffectsDebugToggle` (`Runtime/DevTools/`) | Toggles HAZE fog on/off at runtime by driving `SceneEffectsToggle` | Judge a look with and without fog without leaving play mode (2026-09-02, MRM-11) |
| **F7** | `SceneEffectsDebugToggle` (`Runtime/DevTools/`) | Toggles the CRT post effect on/off — same component as F6 | Same reason as F6; the retro filter hides a lot, so turning it off is how you see what's actually on screen |
| **F8** | `TimeOfDayDebugCycle` (`Runtime/DevTools/`) | Steps to the next `TimeManager` preset and wraps: Morning → Sunset → Night → Apocalypse → Morning | Compare the island's four skies without hunting for the TimeManager's context menu (2026-09-02, MRM-11) |

All of them follow the same shape: a `[SerializeField] private bool` toggle (or the state they
drive), an `Update()` check against `Keyboard.current`, and an `OnGUI()` label only while active.
Copy that pattern for the next one rather than inventing a new shape. Labels are stacked down the
screen by y-offset (F3/F4 at 40-70, F6/F7 at 100, F8 at 130) so several can be on at once.

### F6/F7 restore themselves on exit — do not remove that

`SceneEffectsDebugToggle` drives `SceneEffectsToggle` rather than reimplementing it, because that
component already knows two awkward things: HAZE and Retro Shaders Pro live in assemblies it cannot
reference (hence its lookup by type *name*), and **fog has two independent sources** — a global
Volume override *and* every `HazeDensityVolume` in the scene. Duplicating either would give you a
key that looks like it works while fog keeps rendering.

But `SceneEffectsToggle` writes to the shared Volume **profile asset**, by design, so its inspector
checkboxes persist like any other manual edit. An asset edited during play mode is **not** rolled
back when play mode ends, unlike a scene object — so without care a cheat-key press would quietly
change the project's shipping look. `SceneEffectsDebugToggle` snapshots the fog/CRT state in
`Awake` and puts it back in `OnDisable`, and only if a key was actually pressed, so it never fights
a deliberate inspector setting.

F8 needs no such thing: `TimeManager` drives *scene* objects (the Sun light, the skybox), and play
mode rolls those back on its own. That asymmetry is the whole reason one restores and one doesn't.

**Font (2026-09-02, now on its third pick):** every live debug overlay (F2 `PlayerStatsDebugOverlay`,
F3 `InfiniteStaminaDebugToggle`, F4 `InvulnerableDebugToggle`, F5 `HealthRegenDebugToggle`, plus
`DifficultyDebugOverlay` — 5 spots) renders via a `[SerializeField] private Font font` on each
script instead of Unity's default GUI font. A matching TMP Font Asset drives the same swap on the
3 TextMeshPro HUD elements (Game Over text, FPS counter, ammo counter — 3 spots, 8 total) — set via
the real `TMP_Text.font` setter, not just the serialized field, so the matching generated material
follows.

Current live font: **`SpecialElite.ttf`** / `SpecialElite SDF.asset`. First pick was `HitMePunk.ttf`,
swapped for `Punktype.ttf` same day, swapped again for Special Elite — Carlos is still comparing
looks. All three fonts' raw `.ttf` and SDF `.asset` stay in the project (`Assets/_Project/Art/UI/Fonts/`)
so switching back is just re-pointing the 8 spots, not regenerating anything.

**Bug, hit once and fixed (2026-09-02):** the HitMePunk and Punktype TMP Font Assets were created
via `TMP_FontAsset.CreateFontAsset(...)` but only the generated *material* was persisted as a saved
sub-asset (`AssetDatabase.AddObjectToAsset`) — not the generated *atlas texture*. An unpersisted
`Texture2D` reference serializes to `{fileID: 0}` (null) on disk, so every TMP object using either
font threw `UnassignedReferenceException` on *every canvas repaint*, including in Edit Mode with the
window unfocused — the flood of errors is the likely cause of an editor freeze that cost a whole
session restart. Fix: after `CreateFontAsset(...)` and `TryAddCharacters(...)`, call
`AssetDatabase.AddObjectToAsset` on **both** `fontAsset.atlasTextures[0]` and `fontAsset.material`
before `SaveAssets()` — not just the material. Applied retroactively to fix HitMePunk and Punktype
in place (same GUID/path, so no scene references needed updating) and used correctly from the start
for Special Elite. If a future font swap ever brings back the `m_AtlasTextures` exception, this is
the fix.

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

## Text Animator for Unity

Installed 2026-09-02 (Febucci) — Carlos already owned it; the Asset Store cache had it as a
UPM-format `.unitypackage` (`.../Febucci/ScriptingGUI/Text Animator for Unity UI Toolkit and Text
Mesh Pro.unitypackage`), which imports as an embedded local package at
`Packages/com.febucci.text-animator-unity/` rather than a loose `Assets/` folder — modern Febucci
ships as real UPM now. Five assemblies (Runtime, TMP integration, UI Toolkit integration, Input
System integration, Attributes).

**One import gotcha, already hit once:** the package's own first-run "Setup" window
(`Tools > Febucci > TextAnimator > About Window` to reopen it) has "Install Default Content" /
"Install Built In Effects" buttons that can silently no-op if the Editor loses focus right after
clicking — same class of problem as the general "background work stalls unfocused" trap below.
If clicking them does nothing, the fix is `AssetDatabase.ImportPackage` on
`Packages/com.febucci.text-animator-unity/Data~/BuiltIn.unitypackage` (an absolute disk path, not
a virtual `Packages/...` asset path — `Data~` is tilde-hidden from Unity's own AssetDatabase) —
that's confirmed to land the same content the buttons install
(`Assets/Plugins/Febucci/Text Animator for Unity/...`: Settings, 13 built-in effects, curves,
playbacks, timing presets).

**Not wired to any text yet** — installed and content-populated only. Carlos's plan is to drive
some future display text (dialogue/subtitle-style, not decided which) through it, likely alongside
TextMesh Pro rather than replacing it.

## Known gaps

- **Removal triggers.** F3/F4/F5 and `EnemyDebugControls` are all placeholders for MRM-32 (real
  player damage, hitboxes, damage reactions). When that lands, revisit every row above — most of
  this file should shrink, not grow.
