# Asset import & update process — the repeatable "new asset from the list" workflow

**Trigger phrase:** Carlos says something like *"Hey, I have this new asset"* / *"I downloaded more
assets from the spreadsheet."* That sentence alone is enough to start this whole process — no other
instructions needed. This doc exists so that sentence is a complete spec.

First worked end-to-end 2026-09-17 on four version updates (AST-009 Damage Numbers Pro, AST-018
Procedural Lightning, AST-029 Sounds Good, AST-055 Highlight Plus 2). Treat that session as the
worked example if anything below is ambiguous.

## The three folders

| Folder | Role |
|---|---|
| `C:\Users\calva\Documents\Asset Collection\01_DOWNLOAD` | Landing zone for freshly downloaded `.zip` / `.unitypackage` files, however Carlos names them. |
| `C:\Users\calva\Documents\Asset Collection\02_extracted` | Scratch extraction area, one subfolder per asset ID (`AST-018\`, etc.). Safe to delete and re-extract at any time — nothing here is a source of truth. |
| `C:\Users\calva\Desktop\assets\ASSETS - Index 2026-09-16.xlsx` | **The master spreadsheet.** Source of truth for IDs, owned/wishlist status, versions. See "The spreadsheet" below. |

## Step 0 — identify: repeat or new?

For every file that lands in `01_DOWNLOAD`, check the spreadsheet (`Asset Index` sheet) for a
name match **before** assuming it's new:

- **Match found, already OWNED** → this is a **version update**. Reuse its existing `AST-###` ID.
- **Match found, currently WISHLIST** → Carlos already owns something he'd flagged to buy. Reuse
  its existing `AST-###` ID (wishlist IDs are pre-assigned, not reused from a pool) and move it to
  the new "owned from wishlist" section (see below) — **don't install it anywhere yet.**
- **No match anywhere** → genuinely new. Assign the next free `AST-###` number (highest existing
  ID + 1, wherever it currently sits — owned, yellow-review, or wishlist all share one number
  space).

Every asset we've downloaded so far has turned out to already have a row (owned or wishlist). Don't
assume that pattern holds forever — check every time.

## Step 1 — rename into `01_DOWNLOAD`, keep a log

Rename the file to `AST-###.zip` / `AST-###.unitypackage` (match the original extension) **in
place**, inside `01_DOWNLOAD`. Do this for every file, repeat or new.

Log every rename into a dated CSV in the same folder: `_rename-log <YYYY-MM-DD>.csv`
(`new name,original name,action` — see `_rename-log 2026-09-17.csv` for the format, including how
to phrase the `action` column for a version-update vs. a wishlist-item-now-owned).

**Version updates only:** before renaming the new file to `AST-###.zip`, delete the old
`AST-###.zip` that's about to be superseded. Don't leave both around — the old one is spent once
its content has been merged into the project (see Step 3).

## Step 2 — extract

```
02_extracted\AST-###\<original folder name>\<Asset Name vX.Y.Z>.unitypackage
```

A `.unitypackage` is a gzipped tarball of GUID-named folders, each containing `asset`,
`asset.meta`, and `pathname`. Unpack it directly to read the real structure:

```bash
mkdir _unpacked
tar -xzf "Asset Name vX.Y.Z.unitypackage" -C _unpacked
```

`pathname` inside each GUID folder gives you the path the vendor intended
(`Assets/<PackageName>/...` or, increasingly, `Packages/<vendor.package-id>/...` for UPM-style
packages later flattened into `Assets/`). This is how you find where a specific file lives without
guessing.

**Bundle packages** (Highlight Plus 2 is one) ship as a thin wrapper containing *nested*
`.unitypackage` files, one per render pipeline (`HighlightPlusBundle/URP/...unitypackage`,
`.../Builtin/...unitypackage`). Extract the outer package, find the pipeline you actually use
(URP, for this project — see `Docs/pc-build-target.md`), and unpack that one too.

## Step 3 — decide the target project, then sync by GUID

**First: is this asset already installed anywhere, and where?** Check both:
- Mr. Moonlight: `Assets/ThirdParty/AST-###/` and `Assets/_Project/Code/Vendor/<PackageName>/`
- Playground: `Assets/PLAYGROUND/AST-###/`

Only one of these will usually be true (see `Docs/dual-project-workflow.md` — new/unproven assets
get evaluated in Playground first; only things Carlos has actually adopted into the game live in
Mr. Moonlight). If it's a version update, sync into wherever it already lives. If it's genuinely
new, it doesn't go anywhere yet — extract and leave it in `02_extracted` until Carlos says to stage
it in Playground.

**The sync itself is GUID-matching, not a folder overwrite.** Vendor packages keep stable GUIDs
across minor/major versions (verified every time we've checked). That means:

1. Build a map of `guid → current file path` for every `.meta` file under the asset's current
   project folder(s).
2. Build a map of `guid → (new asset bytes, new asset.meta bytes)` from the freshly unpacked
   package, using each GUID folder's `pathname` to identify it.
3. For every GUID present in **both** maps: overwrite the current file's content and `.meta` with
   the new version's bytes, in place. Do not change the file's path — GUID stability is what keeps
   every scene/prefab/material reference intact, and the project's own folder layout
   (`AST-###`-named, vendor script logic split into `_Project/Code/Vendor/`) is deliberately
   different from the vendor's own `Assets/<PackageName>/` layout.
4. GUIDs in the new package **not yet present** in the project are new-to-this-version content.
   Do **not** blindly import all of them — that reintroduces Demo/Sample bloat Carlos already
   chose to exclude on the original import. Instead:
   - Directory-only entries and files under folders that don't exist in the project → skip. This
     is expected; matched-file counts close to (but usually a little under) total current-file
     counts is normal and healthy.
   - **Files under a folder that already exists in the project** → these are real, and skipping
     them can break something. This is exactly what happened with Sounds Good v2.2.2: it shipped
     a new `SG_Input.cs` helper inside the already-imported demo `Player/` folder, and two
     already-present demo scripts started referencing it. The compile broke
     (`CS0103: The name 'SG_Input' does not exist`) until that one new file was added. When you
     see a compile error naming a type that isn't in the project, check whether the new package
     version added it as a sibling of files you just updated — it usually has.
5. GUIDs present in the **project but missing** from the new package are almost always Unity's own
   auto-generated per-scene bake artifacts (`LightingData.asset`, `ReflectionProbe-N.exr`,
   `*.lighting` files) — not vendor content, safe to ignore. If a real script or prefab GUID is
   missing from a new package version, stop and flag it to Carlos; that's a genuine vendor
   removal/rename and needs a judgment call, not an automatic skip.

Write throwaway Python for this (`os.walk` + a `guid:` regex over `.meta` files, `pathname` reads
over the unpacked package) — there's no tooling for it beyond that. Windows paths, not POSIX, when
calling it (this project's shells mix Git Bash and native Windows Python).

## Step 4 — verify

1. `refresh_unity` (or the Playground equivalent) with `compile: request`, `mode: force`.
2. If the Editor isn't OS-focused, the refresh call will time out waiting for readiness — this is
   the known focus trap (`Docs/dual-project-workflow.md`, `[[unity_editor_focus_traps]]`), not a
   real failure. Ask Carlos to click into the window; nothing more.
3. `read_console` with `types: ["error"]`. Two categories of error are expected residents and are
   **not yours to fix**: the `custom elements added to the ... main toolbar` warning, and Crest's
   `DirectoryNotFoundException` on `Settings.Crest.iOS.hlsl` in Playground (folder-copied Crest,
   not embedded — see `Docs/dual-project-workflow.md`). Anything else naming a file you just
   touched is real; go back to Step 3.4.
4. Compile-clean is necessary but not sufficient. If a demo scene got its GUIDs refreshed and
   someone (Carlos) actually presses Play on it, **runtime** errors can surface that a static
   compile check won't catch — see the Highlight Plus 2 case below.

### Recurring runtime failure: legacy `Input` class vs. Input System package

Both Mr. Moonlight and Playground run **Input System package only** (no "Both" fallback). Any
vendor demo script written against the old `UnityEngine.Input` class throws
`InvalidOperationException` the moment it runs, and Unity's own `EventSystem` does too if it's
still carrying the legacy `StandaloneInputModule`. This has now bitten twice on two unrelated
assets (Gore Simulator, then Highlight Plus 2's `Demo5_Effects.unity`) — **a version-update sync
will silently revert this fix** if the affected script or scene was among the files overwritten,
because the vendor's own shipped code still uses legacy `Input`. Check for it every time a version
update touches a project that has a Play-testable demo scene:

1. **Scripts:** swap `UnityEngine.Input` calls for `PampelGames.Shared.Utility.PGHybridInput`
   (already in the project at `Assets/PLAYGROUND/AST-040/Shared/Utility/PGHybridInput.cs` in
   Playground — copy it across if the target project doesn't have it yet). It mirrors the legacy
   API (`GetKey`, `GetKeyDown`, `MouseDelta`, `LeftClick`, …) so call-site edits are mechanical.
2. **Scenes:** any `EventSystem` GameObject still holding `UnityEngine.EventSystems.
   StandaloneInputModule` needs it removed and `UnityEngine.InputSystem.UI.InputSystemUIInputModule`
   added instead (`manage_components` remove/add by component type name). This is a **scene edit**
   — ask Carlos first, per the hard rule in `CLAUDE.md`, and make sure he's out of Play Mode before
   you touch it (Play Mode edits silently revert on stop).
3. If a version update touches multiple demo scenes in one package (Highlight Plus 2 shipped six),
   check all of them for an `EventSystem` GameObject, not just the one that happened to be open —
   the sync overwrites every scene file it matches, whether or not anyone opens it that day.

## Step 5 — update the spreadsheet

Structure of the `Asset Index` sheet (see the Legend sheet for full column meanings):

- Sections, top to bottom: `OWNED — downloaded assets` → `OWNED / IN PROJECT — missing from the
  original list (yellow = review these)` → **`OWNED (FROM WISHLIST) — not yet in project`** (added
  2026-09-17, see below) → `WISHLIST — P1` through `P5`.
- Columns that change on every import/update: `downloaded file` (the exact versioned filename, so
  there's a log of what was actually pulled in), `up to date?` (`Yes` if the downloaded version
  string matches the `latest version (store)` column exactly, `No` otherwise — don't round or
  guess, compare the literal strings), and, for a wishlist item that just became owned, `status`
  (`Wishlist` → `Owned`) and `priority` (keep the original tier/rank as a note, e.g.
  `Owned (was P1 · #10)`, so the ranking isn't lost).
- **Never renumber the `#N` ranks of the wishlist items left behind** when some move out — gaps are
  fine and cheaper than risking a wrong re-rank. That's Carlos's call if he ever wants it tidied.
- A version update never changes `id`, `asset name`, `category`, or any of the store-info columns
  (`collection url`, `asset store / source`, `publisher`, hyperlinks) — only touch the columns
  listed above.

**Moving a wishlist row into "OWNED (FROM WISHLIST)":** this sheet has no merged cells and no
formulas, so it's safe to script with `openpyxl`, but a straight `insert_rows`/`delete_rows` in the
middle of the sheet **does not move `Hyperlink` objects with their cells** — verified as an
openpyxl limitation, not a hypothetical one. The safe method: build the full new row order in
Python (old rows kept in place, new section + moved rows spliced in, moved-out rows filtered from
their old section), write it into a **fresh sheet** cell-by-cell (value, font, fill, border,
alignment, number_format, hyperlink all copied explicitly with `copy.copy`), then delete the old
sheet and rename the new one into its place. Verify afterward: total `AST-###` ID count must be
identical before and after (263 in, 263 out, last time), and hyperlinks must still resolve on a
handful of spot-checked rows.

**After editing:** copy the saved file over `Docs/asset-index/ASSETS-Index.xlsx` in this repo (see
next section) — every time, not just on request.

**If the spreadsheet is open in Excel** when you need to write it, `openpyxl` can't save over the
lock (`~$ASSETS - Index ....xlsx` appears next to it). Ask Carlos to close it — "Don't Save" is
fine unless he made deliberate edits, in which case get those first.

## The spreadsheet: two copies, one master

- **Master, editable:** `C:\Users\calva\Desktop\assets\ASSETS - Index 2026-09-16.xlsx` (outside
  this repo — Carlos opens and browses it directly in Excel). This is what Step 5 edits.
- **Repo mirror, read-only reference:** `Docs/asset-index/ASSETS-Index.xlsx` (this repo, tracked in
  git, no date in the filename so the path never needs updating). Purpose: so a future session — or
  anyone reading this repo cold — can see current asset status without knowing the Desktop path,
  and so it's backed up by git instead of living solely on one machine.
- **The mirror is not a second source of truth.** Every time Step 5 edits the master, copy it
  over the mirror in the same breath. Never edit the mirror directly. If they ever disagree, the
  Desktop master wins — the mirror is stale until the next copy.

`Docs/external-assets.md`'s "2026-09-16 audit" section and the `[[asset_index_spreadsheet]]` memory
both describe the spreadsheet's ID ranges and section meanings in more depth than this doc repeats.

## Worked example (2026-09-17)

Four files landed in `01_DOWNLOAD`, all turned out to be version updates of already-owned assets:

| ID | Asset | Old → new version | Project |
|---|---|---|---|
| AST-009 | Damage Numbers Pro | 4.55 → 4.56 | Mr. Moonlight (`ThirdParty/AST-009` + `Vendor/DamageNumbersPro`) |
| AST-018 | Procedural Lightning | 3.0.1 → 3.0.3 | Playground (`PLAYGROUND/AST-018`) |
| AST-029 | Sounds Good | 2.2.0 → 2.2.2 | Playground (`PLAYGROUND/AST-029`) — needed the Step 3.4 new-file fix (`SG_Input.cs`) |
| AST-055 | Highlight Plus 2 | 32.6.1 → 34.1 | Playground (`PLAYGROUND/AST-055`) — needed the Step 4 input-system fix on `Demo5_Effects.unity` |

Same session, seven more files turned out to already be wishlist rows Carlos had pre-flagged (Feel,
The Complete UI Sound Effects Library, Zombie Voices Audio Pack, Monster Sounds - Volume II,
Realistic Blood VFX, Technie Collider Creator 2, Cat - Simple) — renamed into `01_DOWNLOAD` under
their existing IDs, moved into the new "OWNED (FROM WISHLIST)" section, **not installed anywhere**.
Technie Collider Creator 2 (AST-116) is the next one scheduled to actually go through Step 3, for
the vegetation-collider work — see the next-session context prompt.
