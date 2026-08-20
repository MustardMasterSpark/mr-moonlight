# C# Conventions — Mr. Moonlight

Written for a **solo developer with an AI pair, a 19-day deadline, and a WebGL build**. Optimised for readability and for Carlos being able to change things without asking. Not for architectural elegance.

---

## The governing principle

> **Carlos must be able to open any file, find the thing he wants to change, and change it — without reading three other files first.**

He is a systems engineer with a decade of experience. He does not need hand-holding. He does need code that does not hide its behaviour behind four layers of abstraction, because in 19 days there is no time to trace an event through an abstract factory.

**Prefer boring, explicit code.** One indirection is fine. Three is a problem.

---

## Naming

| Thing | Convention |
|---|---|
| Classes, methods, properties, events | `PascalCase` |
| Private fields | `_camelCase` |
| Serialized private fields | `camelCase` (no underscore — it shows in the inspector) |
| Local variables, parameters | `camelCase` |
| Constants | `PascalCase` |
| Interfaces | `IPascalCase` |
| Enums and members | `PascalCase` |

```csharp
public sealed class SpotterEnemy : MonoBehaviour
{
    [SerializeField] private Transform lampAnchor;   // shows as "Lamp Anchor"
    private float _reloadTimer;
    public bool HasFiredFlare { get; private set; }
}
```

**Use the canonical spellings** from `glossary.md`. `Tracey`, not `Tracy`. `Rylee`, not `Riley`. `Furman`, not `Fureman`. `Pickaxe`, not `Axe`. Getting this wrong in a class name is a rename across the codebase later.

---

## Structure

**One class per file. File name matches the class.**

**Order within a class:**
1. Serialized fields
2. Private fields
3. Public properties
4. Unity lifecycle (`Awake`, `OnEnable`, `Start`, `Update`, `OnDisable`, `OnDestroy`)
5. Public methods
6. Private methods
7. `OnDrawGizmos` / editor-only

**`sealed` by default.** Unseal only when something actually inherits. It documents intent and it is marginally faster.

**Namespaces:** `MrMoonlight.Player`, `MrMoonlight.Enemies`, `MrMoonlight.Systems`, and so on, matching the folder.

---

## Comments

**Comment the why, never the what.**

```csharp
// BAD — restates the code
// Set the speed to walk speed
_speed = Tunables.I.WalkSpeed;

// GOOD — explains a decision that is not obvious
// Barefoot deliberately halves speed. This is the cost side of the boots
// trade-off — she is quieter but slower. See MRM-40.
_speed = Tunables.I.WalkSpeed * (_hasBoots ? 1f : Tunables.I.BarefootSpeedMultiplier);
```

**Reference issue IDs in comments** wherever a decision came from an issue. When Carlos wonders why something behaves oddly, the issue is the answer.

**XML doc comments on anything public**, and on **every tunables field** — those are load-bearing, not decorative.

---

## The no-hardcoded-values rule

**This is the project's hardest rule.** From the working agreements:

> *"Whenever you are going to use a value that can be a constant or a declared variable, please use this way and put it with a constant or a declared variable and add it to that file."*

```csharp
// BAD
if (health < 60f) { ApplyFear(0.10f); }

// GOOD
if (health < Tunables.I.FearLowHealthThreshold)
{
    ApplyFear(Tunables.I.FearPerDamageBelowThreshold);
}
```

**The exceptions are narrow:** `0`, `1`, `-1`, `0.5f` used as a genuine mathematical half, and array indices. Everything else goes in the tunables.

**When adding a tunable**, add it with:
- A `[Header]` for its system, naming the issue
- An XML comment naming the issue that owns it
- A sensible default so the game runs before Carlos tunes it

---

## Performance patterns that matter in WebGL

WebGL is single-threaded and garbage collection stalls are visible. These are not micro-optimizations; they are the difference between smooth and stuttery.

**Cache component references. Never `GetComponent` in `Update`.**

```csharp
private Rigidbody _rb;
private void Awake() => _rb = GetComponent<Rigidbody>();
```

**Do not allocate per frame.** No `new` in `Update`, no LINQ in hot paths, no string concatenation for UI every frame. Cache the string; update it when the value changes.

**Prefer `TryGetComponent`** over `GetComponent` + null check.

**Use `CompareTag`,** not `gameObject.tag == "x"` (which allocates).

**Do not run every check every frame.** Vision cones, hearing spheres and audible-distance checks do **not** need 60 Hz. Stagger them:

```csharp
// Enemies check detection ~10x/second, offset per-instance so they
// don't all spike on the same frame. With 10 spotters after a flare,
// this is the difference between smooth and not. See MRM-27, MRM-34.
private void Awake() => _detectionOffset = Random.Range(0f, DetectionInterval);
```

**That staggering trick matters most in this project's worst case:** a Spotter flare spawning 10 enemies, each with a vision cone, a hearing sphere and an A* agent. Do not let them all tick on the same frame.

**Object-pool anything spawned repeatedly:** bullet tracers, shell casings, damage numbers, footstep audio sources, particle bursts. Instantiate/Destroy churn is a GC stall you will feel.

---

## Events and coupling

Use **C# events** for cross-system notification. Keep them narrow and typed.

```csharp
public static class GameEvents
{
    public static event Action<float> OnPlayerHealthChanged;
    public static event Action<DamageInfo> OnPlayerDamaged;
    public static event Action<string> OnObjectiveChanged;
}
```

**Always unsubscribe in `OnDisable`.** A leaked subscription in WebGL is a leak that survives scene reloads and will eventually crash the tab.

**Do not build a general message bus.** Typed events are traceable — Carlos can find every subscriber with a right-click. A string-keyed bus is not, and in 19 days traceability beats flexibility.

---

## Async and coroutines

- **Coroutines for anything tied to a GameObject's lifetime** — animations, sequences, timed effects. They die with the object, which is what you want.
- **DOTween for tweening** — it is owned and it is the project's answer to "smooth". Always `SetLink(gameObject)` so the tween dies with its target. An orphaned tween on a destroyed object is a classic WebGL crash.
- **Avoid `async void`.** If async is genuinely needed, `async Task` with a cancellation token.
- **No `Task.Run`.** WebGL is single-threaded.

---

## Error handling

**Fail loudly in the editor, degrade gracefully in the build.**

```csharp
if (dialogueData == null)
{
    Debug.LogError($"[Dialogue] Missing data asset on {name}. See MRM-13.");
    return;  // do not throw — a thrown exception in WebGL can kill the frame loop
}
```

- **Prefix log messages with the system:** `[Dialogue]`, `[EventDirector]`, `[Spotter]`. Filtering the browser console is otherwise miserable.
- **Strip or gate logging for release builds.** `Debug.Log` is not free.
- **Null-check data assets on `Awake`** and say which issue owns them. Half the bugs in a data-driven project are a missing reference.

---

## Testing

There is no unit-test suite and there should not be one — 19 days, and this is a game where almost everything is felt rather than asserted.

**Instead:**
- **Every issue's acceptance criteria are the test.** They are written to be checkable in play mode. If one is not checkable, the criterion is badly written — fix it.
- **The Sandbox scene is the test harness.** Sparring dummy, flat plane, one of each enemy.
- **Debug visualisation is testing.** Vision cones, hearing spheres, aim cones and stat readouts are all specified as toggleable for exactly this reason. Build them when the issue says to, not later.

---

## Things not to do

- **No singletons except `Tunables`.** They hide dependencies and they bite on scene reload.
- **No `SendMessage` / `BroadcastMessage`.** Untraceable and slow.
- **No `Find` / `FindObjectOfType` at runtime.** Serialize the reference. `Awake`-time lookup is tolerable; per-frame is not.
- **No premature abstraction.** There is one player, four enemy types and one level. An interface with a single implementation is noise. Wait until the second implementation exists.
- **No partial implementations without a note.** If something is stubbed, `// TODO(MRM-XX):` with the issue number, so it appears in a search.
