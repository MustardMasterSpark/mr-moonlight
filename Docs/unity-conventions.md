# Unity Conventions — Mr. Moonlight

Unity 6.3 LTS · URP · **Windows 64-bit standalone, 1920×1080**.

> **Corrected 2026-08-27.** This line read *"WebGL target"* until now. **WebGL was dropped on
> 2026-08-25** — see `Docs/pc-build-target.md`. Everything else in this document still applies;
> where a rule below justifies itself with a browser constraint, the **rule stands and the reasoning
> has moved** — the replacement reasoning is in `pc-build-target.md`.

---

## Unity 6.3 Editor UI — toolbar changes from older tutorials/docs

**Read this before following any older Unity tutorial, screenshot, or doc for in-editor steps.**
Unity 6.3 restructured several inspector toolbars into fewer grouped icons with dropdown modes
inside them, so an older guide's icon layout is not reliable.

**Terrain inspector, specifically:** there are only **5 toolbar icons** — Create Neighbor Terrains,
Paint Terrain, Paint Trees, Paint Details, Terrain Settings. There is **no standalone "Paint
Texture" icon.** Texture painting (and terrain layer creation) is a **mode inside "Paint Terrain"**
(the 2nd icon): click it, then use the dropdown that appears below the toolbar (defaults to
"Raise or Lower Terrain") and pick **"Paint Texture"** from the list — that's where **Edit Terrain
Layers → Create Layer...** lives. Confirmed 2026-08-30 during MRM-70 biome-layer painting.

General rule: verify toolbar icons against what Carlos actually sees in his inspector before
giving in-editor instructions — don't assume an older screenshot or doc's layout still applies.

---

## Folder structure

Everything the project owns lives under `Assets/_Project/`. `Assets/ThirdParty/` is git-ignored
project-wide (`.gitignore`, keeps large vendor packages out of the repo) — anything that needs to
survive a re-clone cannot live there.

**Updated 2026-08-30, Carlos's call.** That used to mean "never edit third-party assets, wrap
instead" as a blanket rule. In practice, engines like the Burntwax FPS Engine (MRM-9) need direct
in-place edits to their own logic — wrapping every gameplay tweak isn't realistic. The actual
split now: a vendor package's **3D/binary content** (models, textures, materials, animations,
prefabs referencing them) stays in `Assets/ThirdParty/<Package>/`, untouched and untracked, same
as before. A vendor package's **script logic** — anything with a heavy asmdef/code surface we're
likely to edit — gets **moved into `Assets/_Project/Code/Vendor/<Package>/`** (same relative
folder structure, moved via `AssetDatabase.MoveAsset` so GUIDs/references survive) so it's tracked
and diffable like the rest of the project, and can be edited directly rather than wrapped. Done for
Burntwax's `Scripts/` folder this way 2026-08-30 (`Burntwax.Core.asmdef` and all 62 scripts, e.g.
`PlayerStateMachine.cs` is now at `Assets/_Project/Code/Vendor/Burntwax FPS Engine/Scripts/Player/
Movement/PlayerStateMachine.cs`). **Not retroactively applied to every other already-integrated
package** — only do this for a package when its logic is actually being edited enough to be worth
tracking, same trigger as Burntwax.

```
Assets/
├── _Project/
│   ├── Art/            Models, textures, materials — see the breakdown below
│   ├── Audio/          Clips, mixer assets
│   ├── Code/
│   │   ├── Runtime/     Player, weapons, enemies, systems, audio, UI, VFX, data, world
│   │   ├── Editor/      Editor tools, CSV bakers, custom inspectors
│   │   ├── Tests/       EditMode tests
│   │   └── Vendor/      Tracked, editable copies of third-party packages' SCRIPT logic only
│   │                    (e.g. Burntwax FPS Engine/Scripts/) — see note above. The package's own
│   │                    3D/binary assets stay behind in Assets/ThirdParty/.
│   ├── Data/           ScriptableObjects — tunables, baked CSV data
│   ├── Prefabs/        Player, enemies, items, props, UI
│   ├── Scenes/         MainMenu, Demo, Sandbox
│   └── Settings/       URP assets, input actions, quality settings
└── ThirdParty/         Vendor 3D/binary content — git-ignored, never committed
```

**Sandbox scene.** Keep a `Sandbox` scene with a flat plane, a sparring dummy and a spawn point. Every system gets tested there before it goes near the demo scene. This is faster than loading the island every time and it is where most acceptance criteria get checked.

---

## Art folder breakdown

Decided 2026-08-21, during MRM-9. `Art/` mirrors the category split `Prefabs/` and `Scripts/` already use, rather than a new taxonomy:

```
Art/
├── Characters/    One subfolder per character — model, materials and textures
│   └── Tracey/    co-located, not split into parallel Models/Materials/Textures trees
├── Enemies/       Spotter/, Zealot/, Wolf/, Furman/
├── Weapons/       Pickaxe/, Pistol/, Shotgun/, Turret/
├── Items/         Pickups — Bandages, Crackers, Canteen, Flashlight, etc.
├── Props/         One-off set-dressing — tents, coolers, the RV, the telescope
└── Environment/   Terrain textures, foliage, rocks, skyboxes (only the 4 kept from AllSky 220)
```

**One folder per subject, everything for it in one place.** A model's materials and textures live next to it, not in a parallel `Materials/` or `Textures/` tree — same reasoning as the C# conventions' governing principle: open one place, not three. Only nest a `Textures/` subfolder under a subject if it actually accumulates enough variants to need one.

**Placeholder art still uses the canonical name.** A stand-in model gets the folder name of what it represents, not what it currently is (e.g. a placeholder for Tracey goes in `Characters/Tracey/`, regardless of the source asset). This is the same reasoning `glossary.md` gives for spelling — naming for the final thing avoids a rename-everywhere pain later.

---

## Naming

| Thing | Convention | Example |
|---|---|---|
| Scripts | PascalCase, matches class name | `EnemyVisionCone.cs` |
| Prefabs | PascalCase, category prefix | `Enemy_Spotter`, `Item_Bandages`, `Prop_Telescope` |
| ScriptableObject assets | PascalCase + type | `MoonlightTunables`, `DialogueData` |
| Scenes | PascalCase | `MainMenu`, `Demo`, `Sandbox` |
| Materials | `M_` prefix | `M_TraceyParka` |
| Textures | `T_` prefix + suffix | `T_Wolf_Albedo` |
| Audio clips | `SFX_` / `VO_` / `AMB_` / `MUS_` / `ENM_` / `UI_` / `PLR_` | `VO_D-08-043`, `SFX_Footstep_Leaves_Boots_01`, `ENM_Spotter_Pain_01`, `UI_MenuSelect`, `PLR_Death_01` |
| Animation clips | `A_` + subject + action | `A_Tracey_PickaxeSwing01` |

**Voice-over files use the dialogue line ID** (`VO_D-08-043`). That ID already exists for all ~250 lines in `01-screenplay-demo.md`. **Agree this with Carlos before the actors deliver** — renaming 250 files afterwards is avoidable pain.

**`PLR_` is for Tracey's own non-dialogue vocalizations** — death yells, pain grunts, jump/effort exertion — added 2026-08-22 (MRM-17). Deliberately separate from `VO_`, which is reserved for actual spoken dialogue lines mapped to a screenplay line ID; a death yell has no line ID and shouldn't be shoehorned into that mapping. Mirrors `ENM_` (enemy vocalizations) — same idea, player-side. Auto-routes to the `Aud_PlayerVox` preset (Compressed In Memory, Vorbis 40%, mono, 22050 Hz override) — see `Docs/audio-import-workflow.md`.

**Every audio clip's prefix also drives its import preset** — the six prefixes above map to
Unity's Preset Manager filters, which auto-apply the correct `Aud_*` preset (compression,
sample rate, mono/stereo) on import by filename. See `Docs/audio-import-workflow.md` for the
step-by-step and the full preset reference table; don't hand-tune an audio clip's import
settings — per `Docs/webgl-budget.md` §9, a clip imported outside a preset is a bug. *(That doc is
WebGL-era; the preset rule itself is unchanged on the PC target.)*

---

## The tunables pattern — the project's hardest rule

**There are no hardcoded values.** Every number that could ever want tuning lives in `MoonlightTunables`, a ScriptableObject.

```csharp
[CreateAssetMenu(menuName = "MrMoonlight/Tunables")]
public sealed class MoonlightTunables : ScriptableObject
{
    [Header("Player Movement — MRM-9")]

    /// <summary>Walking speed in metres per second. Owner: MRM-9</summary>
    public float WalkSpeed = 3.0f;

    /// <summary>Sprint speed in m/s. Consumes stamina. Owner: MRM-9, MRM-12</summary>
    public float SprintSpeed = 5.5f;

    /// <summary>How long the crouch transition takes. Owner: MRM-9</summary>
    public float CrouchTransitionDuration = 0.25f;

    [Header("Pistol — MRM-22")]

    /// <summary>M1911 magazine capacity. Owner: MRM-22</summary>
    public int PistolMagazineSize = 7;
}
```

**Every field carries:**
- A `[Header]` naming the system **and its issue ID**
- An XML doc comment saying what it does and **which issue owns it**

**Access.** One accessor, so scripts do not each hold a copy and drift:

```csharp
public static class Tunables
{
    private static MoonlightTunables _instance;
    public static MoonlightTunables I =>
        _instance ??= Resources.Load<MoonlightTunables>("MoonlightTunables");
}
```

*(`Resources.Load` is acceptable here — it is baked into the build, resolved once, and this is exactly the case Resources is still appropriate for. Do not use it for anything else.)*

**Per-instance overrides.** Some values need a default plus a per-enemy or per-weapon override — cone distances, spread, engagement range. The pattern:

```csharp
[SerializeField] private bool overrideConeDistance = false;
[SerializeField] private float coneDistanceOverride = 0f;

private float ConeDistance =>
    overrideConeDistance ? coneDistanceOverride : Tunables.I.DefaultConeDistance;
```

The tunables value is the default; the component may override it; **the inspector shows both.** Document this pattern once in MRM-7 and reuse it everywhere.

**Curves.** Anything the issue list calls "smooth" or "with a curve" is an `AnimationCurve` field in the tunables, not a magic easing constant.

---

## ScriptableObjects

Use them for:
- **Tunables** (above)
- **Baked CSV data** — dialogue, system messages, objectives, the event script. Authored as CSV, converted by an editor script, **never parsed at runtime** (see `pc-build-target.md` §2 — still correct on PC: runtime CSV parsing is fragile regardless of platform)
- **Sound pools** — a pool is a ScriptableObject holding clips + pitch range + volume, so the same pool can be shared across props
- **Item definitions** — one asset per item type

Do **not** use them for runtime mutable state. A ScriptableObject that changes during play retains that change in the editor between sessions and will confuse everyone. Runtime state lives in MonoBehaviours or plain classes.

---

## Prefabs

- **Everything placed more than once is a prefab.** Enemies, items, props, checkpoint volumes, waypoints.
- **Prefab variants** for enemy types sharing a base: `Enemy_Base` → `Enemy_Spotter`, `Enemy_Zealot`.
- **The vision cone is a child prefab** attached to the enemy hierarchy (MRM-28) so each type defines its own origin and direction. Placing it is a **Carlos handoff** — Claude stops there.
- Never break a prefab connection to make a one-off change. Make a variant.

---

## Scenes

Three scenes only:

| Scene | Contents |
|---|---|
| `MainMenu` | The staged background scenario, menu UI, settings, credits |
| `Island` | The island, all locations, the event director, the player. **Build index 1.** |
| `Sandbox` | Development only — **not in Build Settings**. Since 2026-08-26 this is where core mechanics and character animation are built, so it holds more than the original flat-plane/dummy description: `DefenseTest`, `HealthTest`, `MeleeTest`, `PitchTest`, `Controller UI Test`. |

> **The demo scene is called `Island`, not `Demo`.** "Demo" is Carlos's verbal shorthand only —
> confirmed 2026-08-26, MRM-18. `MainMenu` is **build index 0**, `Island` is **index 1**, and those
> are the only two scenes in `ProjectSettings/EditorBuildSettings.asset`.
>
> **Anything built in `Sandbox` must be a drag-and-drop prefab** with no Sandbox-specific
> hardcoding, so moving it into `Island` later is trivial.

**One demo scene, not one per location.** The demo is a continuous journey, and a mid-play scene load is a stall the player will see. Use the audible-distance and sound-layer systems for separation instead of scene boundaries. *(The mine may be an exception if MRM-60 chooses the teleport option — but it teleports within the same scene.)*

---

## Physics and colliders

- **Player:** capsule collider, slope limit set from tunables.
- **Enemies:** a capsule collider for movement **plus separate hitbox colliders** for damage (MRM-32). Do not use one collider for both — the multipliers depend on which was hit.
- **Layers matter.** Set them up once, early:

| Layer | Contains |
|---|---|
| `Player` | The player capsule |
| `EnemyMovement` | Enemy movement capsules |
| `EnemyHitbox` | Head / torso / limb hitboxes |
| `Interactable` | Anything with an `Interactable` component |
| `VisionBlocker` | Trees, buildings, terrain — things that block a vision cone |
| `Ground` | Walkable surfaces, for waypoint Z-snapping |

- **The player's grounded/jump check is not Ground-only.** `PlayerController`'s jump/landing logic doesn't trust `CharacterController.isGrounded` — confirmed live during MRM-9 testing that it reads `false` even at rest on flat ground, which silently blocked jumping whenever the player stood still. It's replaced with a short downward `SphereCast` against `Tunables.GroundCheckMask` (defaults to Everything, minus the player's own layer). Objects do **not** need to be on the `Ground` layer to be jumpable — widened during MRM-58 once blockout props (location markers, mine obstacles) needed to be standable without per-object layer setup. `Ground` itself is still used for waypoint Z-snapping, so keep assigning it to walkable terrain/floors for that system.
- **The vision cone occlusion check raycasts against `VisionBlocker` only.** Do not let it hit the player's own colliders or item pickups.
- **Carlos's open question — do hitboxes need a tag?** Answer: use the **layer** plus a small `Hitbox` component carrying its multiplier type. Cleaner than tags, and it survives renaming.

---

## Editor tooling worth building early

These pay for themselves within days:

1. **CSV → ScriptableObject baker** (MRM-11, MRM-13). Menu item, runs on demand, reports row errors clearly. Carlos will run this constantly.
2. **Event director runtime inspector** (MRM-11). Shows the current step while playing. Without it, debugging the level script is guesswork.
3. **Debug toggles panel.** One place to switch on: vision cones, hearing spheres, aim cone, stat readouts, current event step. These are specified across half a dozen issues; one panel beats six checkboxes scattered across components.

---

## Version control

- The repo has `.gitignore` and `.gitattributes` already configured. **Do not regenerate them.**
- `Library/`, `Logs/`, `Temp/`, `obj/` stay ignored.
- **Force text serialization** for scenes and prefabs so diffs are readable.
- **Branch per issue**, named from Linear's suggestion (`mustardmarisa/mrm-22-pistol-m1911`).
- **Carlos pushes and merges through GitHub Desktop**, not the CLI. Before he does, he asks for a commit summary and description — read the issue and the actual diff, then propose both.
