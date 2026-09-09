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

**Same bug, worse, hit again 2026-09-08 (title-card fonts) — the in-place fix above is NOT safe,
use this recipe instead:** `GutenbergTextura SDF`, `GabrieleBandAah SDF`, `Kurland SDF`, and
`NotoSerif SDF` (the four title-card fonts) all had the identical dead-atlas symptom, but this
time `AssetDatabase.AddObjectToAsset` on the *existing* asset instance (via `CopySerialized` from a
temp object, or via `AssetDatabase.CreateAsset` overwriting the same path) **silently broke the
scene's font references** on every consuming `TMP_Text` — they fell back to `LiberationSans SDF`
with no error, confirmed twice with two different techniques. Root cause not fully isolated (some
interaction between Unity's asset-replace-in-place path and already-loaded scene references to the
old object), not worth re-deriving. **The fix that actually holds up:**
1. `TMP_FontAsset.CreateFontAsset(...)` with `AtlasPopulationMode.Dynamic` (required for step 2 -
   `TryAddCharacters` on a freshly-created `Static`-mode asset fails immediately, 0 characters
   added, `Static` only works as a *lock* applied after populating, not as the creation mode).
2. `TryAddCharacters(fullCharSet)` — include every character actually used by any text on that
   font, Greek included for `Kurland`/`NotoSerif` (`Κύριε ἐλέησον`).
3. `fontAsset.atlasPopulationMode = AtlasPopulationMode.Static;` — locks it, no runtime dependency.
4. `AssetDatabase.CreateAsset(fontAsset, newPath)` at a **new** file path (`... SDF v3.asset`, not
   overwriting the broken original) — this is the step that must not target an existing path.
5. Explicitly re-point every consuming `TMP_Text.font` by direct object reference in script and
   verify each one read back correctly — do not trust GUID/fileID resolution to carry over
   correctly from a recreated asset, confirmed unreliable here.
6. Cold-reload check (exit and re-enter Play Mode with no manual intervention) before trusting it —
   an early attempt looked fixed while "warm" (same Editor session, object already touched) and
   then failed on a genuinely cold load.
The old, broken originals plus one intermediate `v2` attempt were deleted after confirming nothing
referenced them (`grep` the GUID across `Assets/`) — if a future session finds `... SDF v3.asset`
files, that versioning is why; feel free to rename back to the plain name in a quiet moment, it's
cosmetic only.

**Important correction, same session:** the actual bug Carlos reported ("Greek text, the studio
year, and the disclaimer are missing") turned out to be **two unrelated bugs co-occurring**, not
one. The atlas bug above was real and did need the fix above — but the text was *still* invisible
after that fix alone, because `GreekText`, `Year2026`, `StudiosText`, `DisclaimerParagraph1`, and
`DisclaimerParagraph2`'s `TextMeshProUGUI` components had `m_Enabled: 0` directly in the saved
scene — a plain disabled-component checkbox, nothing to do with fonts at all. **When "text isn't
showing" doesn't fully resolve after an atlas fix, check the component's own `m_Enabled` in the
scene YAML before assuming the atlas fix was incomplete** — don't assume one bug explains 100% of a
symptom just because it explains most of it.

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
exactly what the shortcut does internally, so nothing is lost. **Same gap confirmed on
`CanvasGroup.DOFade` too** (2026-09-08, `TitleLetterReveal.cs`) — the UI module's shortcut extension
doesn't resolve despite the module compiling fine and being present on disk (`DOTweenModuleUI.cs`,
no `DOTWEEN_NOUI` define set); same `DOTween.To(() => group.alpha, a => group.alpha = a, ...)`
workaround. Treat any DOTween *shortcut extension* (`.DOFade`, `.DOScale`, etc. as a one-liner on a
component type) as suspect in this project and reach for `DOTween.To` on the raw property first
rather than debugging the module — the core engine (`DOTween.To`, `Ease`, `.SetDelay` etc.) has
never had this problem, only the convenience shortcuts.

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

**Evaluated twice for the main menu title letter reveal (2026-09-08), ruled out both times —
`TitleLetterReveal.cs` is a small custom driver instead, not a gap to fill later:**
1. **Reveal order.** Febucci's typewriter (`Febucci.TextAnimatorCore`, a compiled DLL) is
   hard-coded to reveal characters strictly by increasing string index. Carlos's letter order for
   "MR. MOONLIGHT" jumps around (the 'R' before the first 'M', etc.) — structurally impossible for
   a typewriter, confirmed by reading the DLL's public surface, not worth re-checking.
2. **Per-letter blur/colour after the title was rebuilt as one GameObject per letter.** Text
   Animator's per-character effects operate on multiple *characters within one `TMP_Text`*'s
   rich-text string — once the title became 12 separate `TextMeshProUGUI` objects (Carlos's own
   request, for real per-object DOTween control), Text Animator's whole model stopped applying;
   there's no "one string" for it to tag characters within.
Both times, plain TMP + a small script covered it: rich-text `<color=...>` for static per-letter
colour (no package needed, see the Screen 3 disclaimer note above), and `TMP_Text.fontMaterial`'s
`_Sharpness` SDF property (TextMeshPro/Mobile/Distance Field shader, present on every TMP font
asset in this project) animated per-object via DOTween for a blur-to-focus reveal.

## Known gaps

- **Removal triggers.** F3/F4/F5 and `EnemyDebugControls` are all placeholders for MRM-32 (real
  player damage, hitboxes, damage reactions). When that lands, revisit every row above — most of
  this file should shrink, not grow.
