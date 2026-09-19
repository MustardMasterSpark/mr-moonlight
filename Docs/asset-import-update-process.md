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
| `C:\Users\calva\Documents\Asset Collection\03_documentation\ASSETS - Index 2026-09-16.xlsx` | **The master spreadsheet.** Source of truth for IDs, owned/wishlist status, versions. See "The spreadsheet" below. |

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

- **Master, editable:** `C:\Users\calva\Documents\Asset Collection\03_documentation\ASSETS - Index 2026-09-16.xlsx` (outside
  this repo — Carlos opens and browses it directly in Excel). This is what Step 5 edits.
- **Repo mirror, read-only reference:** `Docs/asset-index/ASSETS-Index.xlsx` (this repo, tracked in
  git, no date in the filename so the path never needs updating). Purpose: so a future session — or
  anyone reading this repo cold — can see current asset status without knowing the master's path,
  and so it's backed up by git instead of living solely on one machine.
- **The mirror is not a second source of truth.** Every time Step 5 edits the master, copy it
  over the mirror in the same breath. Never edit the mirror directly. If they ever disagree, the
  master wins — the mirror is stale until the next copy.

> **Master moved 2026-09-19.** It used to live at `C:\Users\calva\Desktop\assets\`; Carlos moved it to
> `...\Asset Collection\03_documentation\` so the whole collection (`01_DOWNLOAD`, `02_extracted`,
> `03_documentation`) sits under one root. Same filename. His original `ASSETS.xlsx` sits beside it, untouched.

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
the vegetation-collider work — that's an **interim task**, not a version update, so its handoff
lives in `Docs/interim-small-tasks-prompt.txt`'s Task Slot, not a separate file. See "Which kind of
task is this?" below.

## Worked example 2 (2026-09-19) — 14 files, 3 genuinely new

Carlos said "four completely new assets" and pointed at four URLs. The check in Step 0 found that
**one of the four (Vertex Ambient Occlusion Generator) was already wishlist AST-119**, and that ten
more files in the folder he hadn't mentioned were wishlist rows too. So: **never trust "these are
new" — match every file against the sheet by name.** Result:

| Outcome | Files |
|---|---|
| Wishlist → owned, moved to "OWNED (FROM WISHLIST)", ID kept | AST-118 STORY Wildlands Props, 119 Vertex AO Generator, 120 Final IK, 121 Zombie Animations Set, 122 Ghost Creature Anims, 123 Witch Hag Animations, 124 Killer Doll Animations, 125 The Rake, 126 Skeleton Zombies (sheet says "V1", file has no version), 144 PuppetMaster, 147 Asset Optimizer Pro |
| Genuinely new, next free IDs (highest was 263) | **AST-264** SensorToolkit 2, **AST-265** Emerald AI 2026, **AST-266** Advanced Cable Creator |

None was installed in either project (checked `ThirdParty/AST-###` and `PLAYGROUND/AST-###`), so all
14 stay un-extracted, nothing synced. Rename log: `01_DOWNLOAD\_rename-log 2026-09-19.csv`.

What the sheet update added beyond the Step 5 rules above:
- **New-asset rows** get the store data scraped from the Asset Store page's embedded JSON (publisher,
  full price, latest version, updated date — see `[[asset_index_spreadsheet]]`), the collection URL
  Carlos supplied, `notes` starting `[Claude] Added <date>`, the **yellow** fill (the Legend's meaning
  is exactly "owned, missing from your original list"), and `priority` = `Owned (new · not on original list)`.
- **Zebra striping is static fill, not conditional formatting** — moving rows breaks it, so re-stripe
  each section after reordering (yellow rows excepted).
- **Legend counts go stale on every move.** Recompute the P1–P5 "N assets · ~$X" lines from the
  remaining wishlist rows, the category counts from all rows, and add a dated line under "Changes".
  (The 09-17 move had left P1 at 41; it's 25 now.) Verified the formula by reproducing P3–P5 exactly.
- **`up to date?` when the filename has no version** (Skeleton Zombies) or the store has none
  (Asset Optimizer Pro, sold on Fab) → `Unknown`. PuppetMaster's file is named `v1.5 (11 Apr 2025)`
  while the store's 1.5 is dated 2026-04-10 — string-equal so marked `Yes`, but the date suggests
  Carlos's copy may be a year older than the store's; worth a glance when it's installed.
- **`ls | head -100` on `01_DOWNLOAD` silently hid a file** (Zombie Animations Set). List the whole folder
  and filter out `AST-*` instead of truncating.

## Worked example 3 (2026-09-19, later) — 13 files, all already wishlist

Carlos said "the new assets are the ones that don't have the ID name" and told me to ignore a file named
`Assets.zip` (a 2.7 GB leftover from 09-05 that was never an asset row — left untouched). Filtering out
`AST-*` on the **full** listing gave 13 files; every one matched a wishlist row by name, so **no new IDs**:
AST-127, 128, 129, 131, 132, 134, 135, 137 (P1), 142, 143, 156, 157 (P2), 232 (P4). Renamed in place,
rows appended to `_rename-log 2026-09-19.csv` (same date, so same file), nothing extracted or installed.

Sheet update: 266 IDs in, 266 out; the rows now sit in ID order inside "OWNED (FROM WISHLIST)". New since
example 2:
- **Rewrite the sheet in place, not into a fresh sheet.** Snapshot every row (values, `_style`, hyperlink,
  row height) first, then write them back in the new order over rows 3..N. The row count never changes when
  rows only move between sections, so freeze panes, widths and the 552 hyperlinks all survive. Reset
  `ws._hyperlinks = []` first; openpyxl rebuilds it on save.
- **Dry-run validations before writing:** the zebra rule (even index none, odd `F7F8FA`, yellow rows
  excluded) reproduced the existing fills with 0 mismatches, and the tier formula (count + sum of numeric
  prices) reproduced all five Legend lines exactly, so the recomputed ones can be trusted.
- New tier totals: P1 17 / $204.38, P2 24 / $1,026.62, P3 unchanged, P4 19 / $796.94, P5 unchanged.
- `up to date?`: AST-143 Cineaster = **No** (file v1.0.2, store 1.0.3), the other 12 = Yes.
- The Legend's "Source file" line moved down one row to make room for a second 2026-09-19 change line.

## Which kind of task is this? (process doc vs. interim-task prompt)

This doc covers the *mechanics* of moving asset bytes around and keeping the spreadsheet honest —
it applies regardless of what the asset is for. It does **not** decide whether the resulting work
needs its own Linear issue or folds into the `MRM-81` interim-tasks umbrella. That classification
is `Docs/interim-small-tasks-prompt.txt`'s job (§2 there draws the line): trying out a new asset
for something additive (vegetation, VFX, staging) is an interim task and gets a sub-issue of
MRM-81, filed and closed inside that file's process — **don't write this doc's steps up as a new
one-off handoff file**, that's exactly the mistake this note is here to prevent. Upgrading an
*already-installed* package's version, on the other hand, is explicitly called out as **not** an
interim task (GUIDs are load-bearing) — that gets a normal standalone issue, the way the four
version updates in the worked example above did (`MRM-83`).
