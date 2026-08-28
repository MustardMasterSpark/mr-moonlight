# Two Unity projects — Mr. Moonlight (dev) + Playground (sandbox)

**Decision (2026-08-27/28, Carlos).** From now on, two separate Unity projects run side by side:

| Project | Path | Role |
|---|---|---|
| **Mr. Moonlight** | `E:\MrMoonlight` (this repo) | The shipping game. Git-tracked. Nothing untested lands here. |
| **Playground** | `E:\playground\My project` | Sandbox. Not this repo, not git-tracked by it. Where new/bulk-imported assets get opened, checked, and proven out *before* anything moves into Mr. Moonlight. |
| **Weapons** | `E:\playground\weapon` | **Third project, added 2026-08-28.** HQ FPS Weapons 2.0 in isolation. It ships `ProjectSettings` overrides that clobber a host project, so it gets its own. MCP port **8082**. This is where weapon models/animations are pulled from when MRM-22/23/24/25/52 are worked. |

Why: Carlos is about to bring in a batch of new assets (scope described in a later prompt — no
Linear issue yet). Testing them in an isolated project first, instead of importing straight into
Mr. Moonlight, means a bad or bloated asset pack never touches the shipping project, and Mr.
Moonlight's `Assets/` never accumulates evaluation debt. This is the same instinct behind
`Docs/terrain-vegetation-tooling-decision.md`'s "evaluated and rejected" table — prove it first,
commit second.

## Running both at once — the MCP-bridge setup

Both projects use the same **MCP for Unity** bridge (`com.coplaydev.unity-mcp`) so Claude Code can
inspect and act on either one. Unity happily runs two different projects in two Editor windows at
once (it only blocks reopening the *same* project — the `Library` lock) — but the bridge's HTTP
port is a value **typed into the MCP for Unity window**, not something each project silently
auto-negotiates. Get this wrong and both projects share one server and one session, and starting
or stopping one silently affects the other.

**Current assignment (2026-08-28):**

| Project | Port | Unity instance ID |
|---|---|---|
| Mr. Moonlight | `8080` | `MrMoonlight@87580c9df5a077ae` |
| Playground | `8081` | `test@7e551030e70862e2` |

If a second project ever needs adding, or this pairing breaks again: open its **MCP for Unity**
window → **Connect** tab → **Stop Server** → change **HTTP URL** to an unused port → **Connect** →
confirm the status line shows *that* project's own name (not the other project's, not "No
Session") → **Client Configuration → Configure All Detected Clients**.

Claude Code reaches both through `E:\MrMoonlight\.mcp.json`:

```json
{
  "mcpServers": {
    "unityMCP": { "type": "http", "url": "http://127.0.0.1:8080/mcp" },
    "unityMCP_playground": { "type": "http", "url": "http://127.0.0.1:8081/mcp" }
  }
}
```

New MCP servers only load at Claude Code session start — after editing this file, the session has
to be restarted before the new tools appear.

## Moving an asset from Playground into Mr. Moonlight

**Not through the Package Manager.** The chosen method is a direct filesystem copy of the asset's
folder, `.meta` files included:

1. Confirm the asset's folder in Playground is **self-contained** — nothing inside it references a
   shared material, shader, or texture that lives elsewhere in Playground's `Assets/`. A bulk-import
   pack is exactly the case where this can be false; check before copying, not after.
2. Copy the folder **and every `.meta` file with it** (including the folder's own `.meta`) into
   Mr. Moonlight's `Assets/`. The `.meta` files are what keep GUIDs stable, which is what keeps any
   prefab/material/scene reference inside that folder intact on the other side.
3. Trigger an Editor refresh in Mr. Moonlight (`refresh_unity`, or just let it regain focus) and
   verify the import actually landed — search the AssetDatabase for the new path and check the
   console for errors. Don't take the filesystem copy on faith.

**Verified working, 2026-08-27:** `Wolf_enemu` (prefab, FBX, texture, material, test scene) copied
Playground → Mr. Moonlight this way. All 9 assets imported, GUIDs preserved, zero console errors.
**It was a connectivity test only, not a real asset — Carlos deleted the Mr. Moonlight copy on
2026-08-28 once the transfer was confirmed working.** `Wolf_enemu` is not part of Mr. Moonlight;
the only place it currently exists is Playground (see below), unevaluated.

## What's already staged in Playground

`Docs/external-assets.md`'s "Also acquired, not yet integrated" table lists **Wolfenemu / 01
Monster Wolf Boss (AsAlex / HATOGAME)** as owned but not yet imported anywhere. It is now sitting in
Playground at `Assets/Wolf_enemu/` and `Assets/Hatogame_new/BossMonsterPack1/Wolfboss/` — the
natural first real candidate to run through this workflow once evaluation actually starts.

## Relationship to the existing 3D pipeline

`Docs/3d-asset-pipeline.md` (Lanes A–D) is a different track — it's for **raw source art**
(Retro Realism, ALP packs) that needs Blender retopo/baking and the Pixel8r pass before it's usable.
This Playground workflow is for **pre-built Unity content** (asset-pack prefabs, rigged characters,
whole mini-scenes) that mostly just needs checking, not rebuilding. An asset can go through both —
staged and checked in Playground first, then, if it needs the pixelation/map-set treatment before it
matches the PSX look, into the Lane B/C pipeline once it's in Mr. Moonlight.

## Documentation and tracking gaps found while writing this

- **No Linear issue owns "bulk asset import" yet.** Carlos said the actual scope is coming in a
  later prompt — this doc exists so there's somewhere for that issue to point once it's created.
  `MRM-63` ("Content pipelines — 3D props, characters...") already has Claude keeping its
  documentation current and its step 1 is "acquire from an asset pack" — the natural parent. Added
  a short pointer to this doc there (see Linear); did not create a new issue, since Carlos hasn't
  scoped the bulk-import work yet.
- **`external-assets.md`'s Wolfenemu row didn't note it's already staged in Playground** — fixed
  (see above table).
- **The Mr. Moonlight/Playground port assignment lives only in this doc and in memory, not in
  Linear anywhere.** If the bridge setup ever needs redoing on a different machine, this file is the
  only record — keep it current if the ports change again.
