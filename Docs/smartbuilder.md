# SmartBuilder (AST-277, HeurekaGames v1.2.15), level-design prefab browser

**Added 2026-10-03 (branch `mrm-88`).** An Editor-only window that suggests and places prefabs by context, with favorites and history, plus a Smart Replacer to swap placed objects for another prefab.

**Open it:** `Tools > SmartBuilder > SmartBuilder` or `Ctrl+Shift+Alt+B` (also `Window > Heureka > SmartBuilder`). Shift+L = Toggle Lock, Ctrl+Shift+Alt+S = Force Select. The first open builds a preview cache of every prefab, which can take a minute.

**Where it lives.** Two embedded packages, `Packages/com.heurekagames.smartbuilder` and `Packages/com.heurekagames.utils` (NOT under `Assets/ThirdParty/`): the icons load from a hardcoded `Packages/com.heurekagames.*/UI/Icons` path, so moving them breaks the UI. `Packages/` is not git-ignored, so these files are tracked.
- Every assembly has `includePlatforms: Editor`, so nothing reaches a build.
- It adds the scripting define `HEUREKAGAMES_SMARTBUILDER` (ProjectSettings diff, harmless) and keeps its own data under `Assets/Heureka/` if you use favorites.
- Source: `C:\Users\calva\Documents\Asset Collection\02_extracted\AST-277`.

**Status:** installed, compiles clean, console clean. Not yet judged by Carlos. Spreadsheet row AST-277 still says "Not assigned yet": update it once he decides to keep it.
**Folder refactor note:** it finds prefabs by `t:prefab` search, so folder moves do not break it. See `Docs/folder-reorg-opus-prompt.txt`.
