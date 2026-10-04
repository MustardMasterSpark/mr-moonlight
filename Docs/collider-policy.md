# Collider policy: every collider request gets a per-prop evaluation

**Standing rule (Carlos, 2026-10-03):** whenever Carlos asks for anything about colliders (add, fix, replace, "make them accurate",
"mesh colliders on these") Claude does **not** just apply the requested collider to every prop. It first **evaluates each prop's use case and
whether it is worth having a collider at all**, then reports the verdict per prop (or per group) and applies it. Carlos expects to ask for
mesh-collider work on many props, so this applies to every prop and every future scenario, not only AST-270.

## What to evaluate, for each prop (or each group of identical-purpose props)

| Question | Why it matters |
|---|---|
| Can the player, an enemy, a corpse or a bullet realistically touch it? | Decor the player never reaches, or high up, needs no collider |
| Does it block movement in a way that matters (wall, well, steps, roof, trunk)? | These earn an accurate collider |
| Is it tiny or flat on the ground (coins, cups, rope, small jars, soil edges, driftwood)? | Exact collision snags the CharacterController and enemies for no gameplay gain: use none, a simple primitive, or a trigger |
| Is it thin or open (rope, fence, foliage cards, cloth)? | A mesh collider is usually wrong; consider a box/capsule or none |
| Does it move or get hit by many rigidbodies (ragdoll corpses, thrown items)? | Non-convex mesh colliders are the expensive case; the corpse settle pass (MRM-85) is where it shows |
| How many triangles, and how many instances will exist? | Static mesh colliders are cheap in memory (shared cooked data) but cost NavMesh bake time and physics-query time as tris x instances grows |
| Is it a LOD mesh? | Prefer LOD1 or LOD2 as the collider mesh when LOD0 detail adds nothing; say which was used |

## Options, cheapest first

1. **No collider** (pure decor, unreachable, or too small to matter).
2. **Primitive** (Box/Capsule/Sphere), one or a few. Required for anything spawned as a Gaia/terrain tree instance (terrain trees reject Mesh Colliders).
3. **Mesh collider on a reduced mesh** (LOD1/LOD2), non-convex, static props only.
4. **Mesh collider on LOD0**, only for large, reachable, shape-critical pieces (wells, walls, steps, roofs, big trunks).
5. **Convex mesh collider** if the object must be a moving rigidbody (non-convex is not allowed on dynamic bodies).

## How to report and apply

1. Before touching anything, list each prop or group with its verdict and one-line reason, plus the total collider triangles it would cost.
2. If Carlos has already said "mesh colliders on all of them", still show the evaluation and **apply it, flagging every deviation** (props
   you left without one or gave a cheaper one) so he can overrule it. A flagged deviation is not disobeying: it is the rule he asked for.
3. Put the collider on a dedicated `<Name>_Collision` child so the visual hierarchy stays clean; copy the root's static flags.
4. Edit prefab assets with `PrefabUtility.LoadPrefabContents` / `SaveAsPrefabAsset` / `UnloadPrefabContents`; read the saved prefab back
   (collider type, mesh, counts) and confirm the console is clean.
5. Record the decision in the prop's `Docs/prop-log.md` entry (or the issue comment): which colliders, which mesh, why.

## History

- 2026-10-03, AST-270 Medieval Wells: vendor boxes (154) looked like a loose cloud around the well, Carlos asked for accurate mesh colliders;
  applied LOD0 mesh colliders to all 42 props (26,078 tris, `Well_01` largest at 5,384). Open question at the time: clutter (coins, cups,
  ropes, small jars, driftwood, soil borders) probably should have a cheaper collider or none. This policy was created right after.
