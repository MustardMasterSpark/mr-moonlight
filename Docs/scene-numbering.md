# Scene numbering (2026-09-30)

**Deleted 2026-09-30 (Carlos):** `LightingTestSceneCullingTest` (was 08), `LightingTestSceneCullingTestFull` (was 09), `Island_Legion Fog Experiment` (was 10). Culling C (FullDisable, 8,786 sources) is identical in scenes 02, 06, 07, 08, so the test copies were redundant. Scenes 08/09 are still in git history (build 41/42 results in the culling docs name them).

Every scene in `Assets/_Project/Scenes` has a number prefix, in the order the scenes were created (from git history;
the first six were all in the 2026-08-03 baseline commit, so Sandbox is placed first by being the empty starter scene).
New scenes take the next number (12, 13...). **Docs, logs and Linear written before 2026-09-30 use the old names** (no number).

| # | Scene (old name) | Created |
|---|---|---|
| 01 | Sandbox | 2026-08-03 |
| 02 | Island | 2026-08-22 (MRM-58) |
| 03 | MainMenu | 2026-08-26 (MRM-18) |
| 04 | VegetationGallery | 2026-08-29 |
| 05 | VegetationGallery_TechnieColliderTest | 2026-09-17 |
| 06 | Island_Legion | 2026-09-18 (MRM-84) |
| 07 | LightingTestScene | 2026-09-29 (MRM-86) |
| 08 | LightingTestScene Fog Experiment (was 11) | 2026-09-30 (AST-282) |

**Scene names are loaded by string.** Changed with the rename: `MainMenuController` (`demoSceneName` = "02 Island",
`legionSceneName` = "06 Island_Legion"), `GameOverPanel` (`mainMenuSceneName` = "03 MainMenu"), plus the serialized values of
those fields in every scene. Session logs now show `[06 Island_Legion]` etc. (SessionLog filters by scene name).
Renaming a scene again means updating those three fields.
