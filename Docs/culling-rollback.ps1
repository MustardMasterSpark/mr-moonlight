# MRM-85 / AST-145 culling experiment: full rollback. Run from E:\MrMoonlight with Unity CLOSED.
#   powershell -NoProfile -ExecutionPolicy Bypass -File Docs\culling-rollback.ps1
# Removes ONLY the culling experiment. It does NOT touch DisplayBootSettings.cs (the vSync/fps-unlock fix) or SessionLog v5.
# The original LightingTestScene.unity is never modified by the experiment, so it needs no restore.
# Nothing here commits or pushes.

$ErrorActionPreference = 'Stop'
Set-Location E:\MrMoonlight

# 1. Project settings changed by creating the ACSCulling layer (tracked files): back to the last commit.
git checkout -- ProjectSettings/TagManager.asset ProjectSettings/DynamicsManager.asset ProjectSettings/Physics2DSettings.asset

# 2. The culling test scenes (copies of LightingTestScene + 8,786 DC_Collider children + DC_SourceSettings + controller):
#    ...CullingTest (KeepShadows) and ...CullingTestFull (FullDisable).
Remove-Item -Force -ErrorAction SilentlyContinue `
    'Assets\_Project\Scenes\LightingTestSceneCullingTest.unity', `
    'Assets\_Project\Scenes\LightingTestSceneCullingTest.unity.meta', `
    'Assets\_Project\Scenes\LightingTestSceneCullingTestFull.unity', `
    'Assets\_Project\Scenes\LightingTestSceneCullingTestFull.unity.meta'

# 2b. LightingTestScene.unity itself was edited on 2026-09-30 (forest test spot: player + weather circuit moved).
#     It is tracked, so this restores the old start (player 403.60, 23.10, -74.20; circuit root 414.60, 22.91, -65.20).
#     REMOVE THIS LINE if you want to keep the new forest start position in the original scene.
git checkout -- Assets/_Project/Scenes/LightingTestScene.unity

# 3. The asset itself (git-ignored; the source of truth is Documents\Asset Collection\01_DOWNLOAD\AST-145.zip).
Remove-Item -Recurse -Force -ErrorAction SilentlyContinue 'Assets\ThirdParty\AST-145'
Remove-Item -Force -ErrorAction SilentlyContinue 'Assets\ThirdParty\AST-145.meta'

git status --short
Write-Host 'Rollback done. Open Unity: it reimports; the console should show no AdvancedCullingSystem errors.'
