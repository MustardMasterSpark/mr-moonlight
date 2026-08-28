# MRM-70 — Gaia phase kickoff

**Written 2026-08-28.** Read this, then wait for Carlos's go-ahead. **This is context, not a start
signal** — the standing rule (`feedback_dont_auto_start_on_kickoff`).

---

## What this session is

Resume **MRM-70 (Island vegetation + terrain texturing pass)**, paused 2026-08-26, using **Gaia Pro
VS** — adopted 2026-08-27, scheduled explicitly for *after Sept 1*, now current.

Two jobs, in this order:

1. **Improve the terrain *shape*** — erosion and surface detail on the existing heightmap. Carlos's
   stated priority: *"make the terrain as nice and realistic as possible, then fill it with
   vegetation."*
2. **Re-spawn vegetation through Gaia's biome/spawner system**, replacing the hand-rolled
   `BiomePainter` / `TerrainComposer` pass.

---

## Why now, and why terrain before enemies

**Deliberate sequencing, not preference.** MRM-27 (enemy navigation) now bakes a **Unity
NavMeshSurface** over the island. Gaia's erosion pass **changes the heightmap** and the respawn
**changes every tree position**. Baking navigation first would mean baking it twice.

**Terrain → vegetation → navigation.** Do not reorder.

---

## Read first, in this order

| # | Document | Why |
|---|---|---|
| 1 | `Docs/terrain-vegetation-tooling-decision.md` **§2, §2b, §2c** | The Gaia decision, the keep/decline boundary, the temporary-install cycle, and **7 recorded gaps** |
| 2 | `Docs/mrm70-pause-2026-08-26.md` | Where the issue actually stopped — **8 open gaps** |
| 3 | `Docs/mrm70-biome-vegetation-strategy.md` §6b, §7 | The biome plan and its still-open questions |
| 4 | `Docs/new-asset-list.md` — the Gaia brief | The Round-2 install manifest (below) |
| 5 | `Docs/pc-build-target.md` | Platform + rendering stack |

---

## ✅ What Round 2 already settled — do not re-derive

**Gaia is an installer, not a package.** Its 4.9 GB in Playground is **entirely nested
`.unitypackage` cache** — `Gaia Pro VS/` contains only `Packages - Cache/` plus two PDFs. **Nothing
is installed yet.** The Gaia Manager menu Carlos saw is that installer, and it is **not a
dealbreaker — it is precisely the mechanism that lets us decline the colliding modules.**

**This closes gap #1 of `terrain-vegetation-tooling-decision.md` §2c.** The keep/drop list is no
longer strategy; it is a file list:

| ✅ Install | ❌ Decline — and why |
|---|---|
| `Gaia/Gaia.unitypackage` | `Gaia/Gaia Water.unitypackage` — collides with Crest (MRM-71) |
| `Gaia/Stamps.unitypackage` | `Gaia/Unity URP Water` + `Unity HDRP Water` — same |
| | `Gaia/Sky & Lighting Presets` — collides with AllSky + `TimeManager` (MRM-47/69) |
| | `Gaia Pro/Procedural Worlds Sky` — same |
| | `Gaia Pro/GTS` — **a terrain shader system. It would fight `RetroTerrainLit`, which *is* our PSX terrain look.** The single most important decline |
| | `Gaia/Asset Samples` + `Asset Samples - Synty Studios` — sample art, wrong style |
| | `Gaia Pro/Gaia Pro Assets and Biomes` — sample biomes |
| | `Gaia/Controller Support` — we have MRM-8 |

---

## The order of work

### Phase 1 — Erosion only (reversible, do this first)

Touches **only the heightmap**. Independently useful, and it closes gaps 2, 4, 5 and 6 of §2c at low
risk before any vegetation is at stake.

1. **Record the exact Gaia version** → `Docs/external-assets.md`. The whole install/remove/re-install cycle depends on GUID stability (§2b condition 2).
2. **Snapshot the 8 TerrainLayers' order before touching anything.** ⚠️ **The highest-risk unknown in the entire pass** (§2c gap 5): layer order drives *both* vegetation spawn masks *and* footstep surface mapping (MRM-39). A silent reorder breaks two systems at once, and **the footstep break stays inaudible until someone listens.**
3. Run Terraform / erosion on the existing heightmap. **Preserve the island silhouette** — it is load-bearing for gameplay and for audio design already built against it.
4. **Diff the TerrainLayer order again.** Record the result either way.
5. Verify Gaia handles a **single non-square 4103 × 7085 m terrain** (§2c gap 6).
6. Check whether Gaia attached components to the Terrain GameObject (§2c gap 2) — `Island.unity` is tracked in git and must not end up with missing scripts.

### Phase 2 — Vegetation respawn

**Do this in the same session as the new-tree respawn** (pause-doc gap #2: *"clear + respawn in one
motion"*). Doing both at once turns two days into one. **Do not clear early** — the island must stay
demoable throughout.

1. **The biome mask does not exist yet** (§2c gap 7). `biomes.png` is a **scene-view screenshot, not a top-down map** — it is not usable as a Gaia Image Mask. **Produce an orthographic top-down render of the terrain first** to get correct registration. Budget this; it is not free.
2. `Spawner → Advanced → Resource Management → Import Terrain Resources` to turn the 8 existing painted TerrainLayers into spawn rules.
3. Build biome spawn rules pointing at **our own** Retro Realism prefabs. Gaia places; **Flora draws.** No conflict.
4. **Save every spawner / biome / session asset into `Assets/_Project/`**, never `Assets/Procedural Worlds/` (§2b condition 1) — or removing Gaia destroys the recipe.
5. Re-verify in this order: Flora reads the new tree data → PSX material migration survived → tree collision still blocks (raycast sweep) → FPS holds. **In a build, not the editor** — `UnityStats` has lied on this project.

### Phase 3 — Strip and remove

Per §2b: strip Gaia components off the Terrain GameObject, then remove the package. The terrain is
baked `TerrainData` and survives. Re-import the *same version* whenever changes are wanted.

---

## Two new inputs since the pause

- **Low Poly Plant Collections** and **TopDown Nature Library** are staged in Playground and adopted for biome variety. ⚠️ **Check silhouette at first-person eye height before committing the set** — top-down packs often have unfinished undersides, and this is a first-person game. Poly count and texture size are *not* concerns (the pipeline caps textures at 512).
- **Pause-doc gap #7 has a new answer.** *"Whether enemy vision needs trees on a dedicated layer"* — **yes, plan for it.** Blaze AI's `Vision.cs` uses `LayerMask layersToDetect` and `blockingLayers`, and `BlazeAIDistraction` uses `blockingLayers` for sound occlusion. Trees needing their own layer is now a known requirement, not an open question. Coordinate with the layer-order snapshot in Phase 1 step 2.

---

## Still open, not solved by Gaia

From the pause doc — Gaia does **not** address these, so do not expect them to close:

- **Wind** (gap 3) — still a Shader Graph someone has to write.
- **14 of 27 detail prototypes don't render under Flora** (gap 4) — rebuild as single-mesh crossed quads on `RetroLit`. Scoped to the respawn job; **this is the session to do it.**
- **`RetroTerrainLit` d3d11 compile error** (gap 5) — a Retro Shaders Pro asset bug. We run d3d12 where it is fine, but `pc-build-target.md` allows "Direct3D12, Direct3D11 (auto)", so some player machines could hit it. **Not fixable by our material settings.** Decide whether to force d3d12 in Player Settings.
- **Barrier rock walls** (gap 6) and the **birch-vs-pine / LOD cull distance** questions (gap 7).

---

## Working rules

- **Do not auto-start.** Wait for Carlos.
- **Ask permission before Unity or Blender work**, then do it, verify by reading state back, and document. `CLAUDE.md` hard rule.
- **Never commit or push.** Carlos uses GitHub Desktop.
- **Vegetation and staging numbers are exempt from `MoonlightTunables`** until a real perf problem appears (`feedback_tunables_during_prototyping`).
- **Verification needs a real build.**

## Model

**Sonnet** for execution — the decisions are made and recorded. **Opus only** if the TerrainLayer
order changes or Gaia cannot handle the non-square terrain, because both invalidate the plan.
