# Changelog

All notable changes to this package will be documented in this file.

The format is based on [Keep a Changelog](http://keepachangelog.com/en/1.0.0/)
and this project adheres to [Semantic Versioning](http://semver.org/spec/v2.0.0.html).


## [6.3.35] - 2026-08-20

- Fixed player build with Unity 6.5 due to one last `int` not being converted to `EntityId` in the API.


## [6.3.34] - 2026-08-10

### Changed
- Added an option to the Examples stats controller to disable draw-call profiling and automatically remove that stat from its layout.

### Fixed
- Fixed Flora debug shading leaking onto normally shaded instances and ignoring unsupported debug modes.
- Fixed Unity 6.5's 64-bit `EntityId` corrupting container and terrain ownership records, including the Wrecking Ball sample's terrain owner lookup.
- Fixed the Examples stats display reporting zero draw calls in Unity 6.5 after Unity split the aggregate draw-call profiler counter into per-path counters.
- Fixed GPU occlusion shader includes for Unity 6.5's eight-digit `UNITY_VERSION` encoding while retaining compatibility with the legacy encoding.
- Fixed Unity 6.5 Rendering Debugger registration by using its replacement debug UI recreation API while retaining the prior API on earlier Editors.


## [6.3.33] - 2026-08-09

### Fixed
- Fixed a Unity 6.5 D3D11 crash in indirect instance culling by using the platform's typed indirect-argument buffer instead of combining indirect and raw targets.


## [6.3.32] - 2026-08-07

### Added
- Added compact live CPU/GPU culling statistics, density and occlusion culling overrides, debug shading opacity, color legends, and an Editor bridge to the Rendering Inspector.

### Changed
- Changed Unity object identity APIs and instance filters to use `EntityId`, preserving Unity 6.5's full 64-bit object identity.
- Updated editor picking, selection, hierarchy, culling, and occlusion paths for Unity 6.5 `EntityId` semantics.
- Renamed the Rendering Debugger's spatial hash section to Culling Grid and limited Flora debug compute variants to active Flora culling diagnostics.

### Fixed
- Fixed Unity 6.5 compilation by removing unused legacy GPU-driven bridges and updating Batch Renderer Group, terrain marshalling, and Package Manager internal API adapters.
- Fixed indirect chunk culling on Intel Iris Xe D3D11 by storing culling work-group dispatch arguments in a raw buffer.
- Fixed density culling layer masks affecting instances outside the selected layers.
- Fixed runtime range-density overrides being clamped to the full screen-size range.
- Fixed density and LOD randomization losing precision from instance random IDs.
- Fixed development-build debug shading modes using values that did not match generated shader constants.
- Fixed debug shading opacity being ignored by URP and block-level culling-grid colors using world size as a palette index.

### Removed
- Removed the obsolete camera-freeze UI and documentation after transient culling requests replaced the cached view state it depended on.


## [6.3.31] - 2026-06-20

### Fixed
- Fixed shadows on `LODGroup`s with animated cross-fade osscillating continuously when the camera isn't moving.


## [6.3.30] - 2026-05-17

### Added
- Added experimental rendering inspector window for inspecting internal rendering data and instances.
- Added instance-origin sphere and bounds query APIs for selecting instances by transform position instead of render-bounds intersection.
- Added caller-owned result overloads for instance sphere and bounds queries to reduce repeated native result allocations.

### Changed
- Improved the scene settings inspector to be more user friendly and informative, with better organization and descriptions.
- Reduced native over-allocation in instance query result lists by reserving against actual candidate instance counts.


## [6.3.29] - 2026-04-14

### Fixed
- Fixed Switch2 platform support: added switch2 to only_renderers pragma in compute shaders. (@shipmates_js)
- Fixed static memory allocations in both `InstanceManager` and `TemplateManager` due to incorrect API usage. (@shipmates_js)
- Fixed compute shader variable name collision on PS5. (@shipmates_js)


## [6.3.28] - 2026-04-14

### Fixed
- Fixed missing using in `FloraSystem` causing compile errors during player builds.
- Fixed detail patches sometimes unloading before the max detail distance.
- Fixed render distance being compared to the screen relative distance instance of the actual instance distance in the culling shader.


## [6.3.27] - 2026-03-13

### Fixed
- Fixed incorrect package version in `package.json` causing issues with the package manager and installation.


## [6.3.26] - 2026-03-12

### Changed
- Terrain detail loading now honors the configured patch load budget from the first frame.
- Reduced spikes caused by terrain detail streaming creating, destroying, and unloading too much work in a single frame.
- Added simpler terrain detail streaming controls with `Immediate`, `Streamed`, and `Custom` modes, a responsiveness slider, and an unload grace period.

### Fixed
- Fixed terrain detail rebuild scheduling overshooting load budgets on multi-layer terrains and repeatedly syncing main-thread work.
- Fixed culling layout reservations being emitted out of order, causing flickering.


## [6.3.25] - 2026-03-09

### Added
- Added scene-backed instance support with shared templates and optional prefab sources.
- Added lightmap-aware culling and indirect draw command partitioning.
- Added `GetInstanceIdentitySource`, `GetInstanceRenderSource`, and `GetInstanceOwner*` methods to `FloraSystem` for querying instance prefab sources and ownership.

### Changed
- Optimized container instance tracking and transformations.
- Optimized filtered instance queries.
- Deprecated `AuthoringGameObjectID`, `GetInstancePrefab` `GetAuthoring*` methods in favor of optional prefab sources and instance ownership queries.

### Fixed
- Fixed template refreshes leaving stale instance bounds, probe upload state, and template ownership.
- Fixed tests being compiled automatically in user projects.


## [6.3.24] - 2026-03-06

### Changed
- Per-prefab render and shadow distance settings now act as caps that combine with scene and volume distance limits.

### Fixed
- Fixed terrain tree refreshes after terrain streaming, delayed terrain updates, and other terrain snapshot changes.
- Fixed localized template changes sometimes leaving stale draw batches or forcing unnecessary full template buffer uploads.
- Fixed `FloraInstanceTransform` point transforms, stale instance bounds, and stale spatial-query state after runtime transform updates.
- Fixed spatial queries for default prefab instances and layer filtering, and fixed selection-plane queries when using `Allocator.Temp`.
- Fixed CPU culling layout generation to preserve draw-batch state correctly and avoid unnecessary rebuild work.


## [6.3.23] - 2026-03-03

### Added
- Added template state caching.

### Fixed
- Fixed missing orthographic uniform data.
- Fixed chunk flags dispatching incorrect group count.
- Fixed chunk references in editor-only jobs.
- Fixed sample asset guid collisions with Unity sample assets.


## [6.3.22] - 2025-02-06

### Fixed
- Fixed TempJob warning during object tracking (surviving multiple frames) in `FloraSystem`. (@misty2023)


## [6.3.21] - 2025-02-01

### Fixed
- Fixed terrain disabled state not being respected.
- Fixed animated cross-fade bands differing from transition based cross-fades.
- Fixed culling workgroups not being initialized to zero.
- Fixed typo affecting _ViewAnimScreenRelativeMetricSq1 in CullingViewShaderVariables.hlsl (@milk_drinker01)
- Fixed possible invalid culling workgroup reads when cells are indirectly culled on the GPU. (@milk_drinker01)

### Changed
- Improved instance visibility capacity estimation.

### Added
- Added CPU job debug checks to detect invalid writes.
- Added GPU error checks for improved debugging of GPU culling issues. Available in the rendering debugger display.


## [6.3.20] - 2025-01-12

### Fixed
- Fixed obsolete method warnings on Unity 6000.3 LTS.
- Fixed unused variable warning when using HDRP in `FloraSceneSettingsEditor`.


## [6.3.19] - 2025-12-07

### Fixed
- Fixed a function delegate in the terrain bridge that changed signature on Unity 6000.3 LTS.


## [6.3.18] - 2025-11-18

### Fixed
- Fixed shadow split capacity underestimation causing shadow flickering.


## [6.3.17] - 2025-11-12

### Fixed
- Fixed minor issues in the template manager.


## [6.3.16] - 2025-11-08

### Fixed
- Fixed array resizing in `TemplateManager` causing out of range exceptions with large numbers of templates.
- Fixed `MaxLength` in `NativeBitSet` returning the capacity instead of the maximum allocated length, causing out of range exceptions.


## [6.3.15] - 2025-11-05

### Fixed
- Fixed compile error in `DebugDisplayFlora` when compiling non-development builds.


## [6.3.14] - 2025-11-03

### Fixed
- Fixed shader warning in `CullingGrid.hlsl`.
- Fixed range density using the UI min/max values instead of the actual specified range in `FloraDensitySettings`.


## [6.3.13] - 2025-10-30

### Fixed
- Fixed null reference exception in `FloraSystem` when scene settings cause a re-initialization in the editor.
- Fixed GPU memory issues when resizing instance buffers.
- Fixed GPU stall during indirect culling due to views sometimes re-using the same culling buffers within the same frame.

### Changed
- Improved stability of GPU uploads and culling.
- Improved culling buffer memory management to reduce GPU memory usage.
- Internal refactoring of culling system for better maintainability and performance.
- Added detailed culling and memory statistics to the `FloraSceneSettings` statistics panel.


## [6.3.12] - 2025-10-27

### Fixed
- Fixed group ID not being correctly unwrapped in `InstanceBufferCopy.compute` causing instance buffer copies to be incomplete.
- Fixed `uint` and `uint2` scatter kernels sometimes writing out of bounds in `InstanceBufferUpload.compute`.
- Fixed `EditorUtility.SetDirty` warning in `FloraDensitySettings` and `FloraRenderSettings` when upgrading from older versions.
- Fixed `AutoRegisterTerrains` registering terrains when disabled or non-existent in `FloraSceneSettings`.


## [6.3.11] - 2025-10-27

### Fixed
- Fixed kernel error on Windows in `InstanceBufferUpload.compute` due to an early return before `GroupMemoryBarrierWithGroupSync`.
- Fixed vector truncation warning in `ComputeUtility.hlsl`.


## [6.3.1] - 2025-10-26

### Fixed
- Fixed `PostLateUpdate` phase running after rendering in the player, causing issues with upload timing.
- Fixed scheduled uploads sometimes not being processed leading to missing instances in player builds.
- Fixed compile errors on Unity 6.3 beta.
- Fixed debug frozen camera not working correctly and improved the UI for selecting which camera to freeze.

### Changed
- Improved general stability of the upload system in player builds.
- Optimized all upload paths on both CPU and GPU.
- Optimized dynamic matrix uploads by updating previous frame matrices on the GPU.
- Added GPU grid chunk culling, reducing CPU build overhead and allowing for tighter packed cells and GPU occlusion of chunks.
- Added GPU procedural line debugger for visualizing the culling grid. Visualization now works in player builds.


## [6.3.0] - 2025-10-20

### Fixed
- Fixed compile error in `DrawManager` on Unity 6.2+.
- Fixed instance corruption when switching scenes in player builds.
- Fixed uploads being processed out of order in player builds.
- Fixed memory corruption in `NativeBufferArray` when resizing large buffers.
- Fixed crash on application quit due to `FloraSystem` not disposing.
- Fixed exception in culling jobs due to invalid draw commands making it through the culling pipeline.
- Fixed MeshLod materials not being registered correctly.
- Fixed terrain details not loading on the first frame.

### Changed
- Optimized instance buffer layout for better memory usage and cache performance.
- Reduced the instance culling shader variant count.


## [6.2.97] - 2025-10-11

### Fixed
- Fixed invalid (destroyed) chunk archetypes being uploaded.


## [6.2.96] - 2025-10-11

### Fixed
- Fixed chunks sometimes being culled by distance earlier than they should be on the CPU.
- Fixed instances allocating space for light probe data when disabled in `FloraSceneSettings`.
- Fixed race condition when updating chunk bounds.
- Fixed all instances within the density range being affected by fading when the density value is greater than zero.
- Fixed container sample instances flickering when moving due to missing stable random IDs.

### Changed
- Added `FloraAdditionalPerInstanceData` to control per-instance data allocations on a per-prefab basis in `FloraAdditionalRendererSettings`.
- Added `flora_VariationColor` DOTS property for optional per-instance color variation in shaders. Can be enabled in `FloraAdditionalRendererSettings`.
- Added methods to `FloraSystem` to get and set per-instance color variation.
- Deprecated `RequiresPerInstanceRandomID` on `FloraAdditionalRendererSettings`, use `FloraAdditionalPerInstanceData` instead.
- Show tree and details probe and motion settings as disabled when the parent property is not enabled in `FloraSceneSettings`.
- Made distance fade out falloff sharper and closer to the max distance threshold.
- Editor GPU instance handle ids are now no longer included in development builds.
- Allow `DetailLoadBudgetPerFrame` to be zero to disable the budget entirely in `FloraSceneSettings`.


## [6.2.95] - 2025-10-06

### Fixed
- Fixed package installer corrupting the Flora package when upgrading or installing.
- Fixed missing renderer feature in URP samples' render pipeline asset.
- Fixed NativeArray disposed error in `ScheduleUpdateChunkBounds`.

### Changed
- Removed all legacy shader files and code.


## [6.2.94] - 2025-10-05

### Fixed
- Fixed bad package upload causing installation issues.
- Fixed instance flags not uploading when there are changes, affecting selection and motion vectors.
- Fixed missing SupportedOnRenderPipeline attribute on `FloraRuntimeShaders`.


## [6.2.93] - 2025-10-04

### Fixed
- Fixed ambient lighting on first frame sometimes being incorrect in the editor.
- Fixed `FloranstanceRenderer` not re-enabling rendering on the default Unity renderers when disabled.
- Fixed culling on `LODGroup` instances evaluating to zero when the culling transition is not present.
- Fixed a null reference error in `FloraSceneSettings` when Flora isn't active.
- Fixed terrain trees and details not respecting the detail or tree distance when they don't have an `LODGroup`.
- Fixed terrain detail patches loading and unloading being off by one patch cell.
- Fixed max render and shadow distances being incorrectly scaled by the camera's screen metric when culling.

### Changed
- Added support for terrain detail prototype textures.
- Non `LODGroup` based terrain foliage instances now fade out before exceeding the detail or tree distance.


## [6.2.92] - 2025-10-01

### Fixed
- Fixed `GraphicsBufferStore` not releasing per-frame pooled buffers in the editor, causing GPU memory leaks.
- Fixed `Add Render Settings Volume` button in `FloraSceneSettings` not being undoable.
- Fixed `FloraAdditionalRendererSettings` UI when inspecting on a prefab in the project.
- Fixed buttons in `PackageInstallerWindow` not being working on first click.

### Changed
- Improved memory usage in `InstanceBuffer` by using tighter instance packing.
- Improved statistics on `FloraSceneSettings`, now refreshes accurately and shows more accurate memory usage.
- Made the falloff value in `FloraDensitySettings` more intuitive by changing the range to `0-1` and scaling it internally.
- Made the per-instance random ID value optional to save memory when not needed. Can be enabled in `FloraAdditionalRendererSettings`.


## [6.2.91] - 2025-09-30

### Fixed
- Fixed `InstanceBuffer` resize strategy being far too aggressive, causing excessive memory usage.


## [6.2.90] - 2025-09-29

### Fixed
- Fixed player build error in `NativeBufferArray`.
- Fixed player build error in `IncludeExcludeListFilter`.
- Fixed range based density culling fading out twice due to minimum screen size overriding it.
- Fixed range based density culling popping due to an invalid early out distance check.

### Changed
- Added terrain detail budgets for unloading and loading patches per frame in `FloraSceneSettings`.
- Added additional light probe options for trees and details in `FloraSceneSettings`.


## [6.2.80] - 2025-09-25

### Fixed
- Fixed `FloraInstanceRenderer` sometimes resolving its prefab to the incorrect original prefab.
- Fixed `Revert To GameObject` not being undoable.
- Fixed terrain registration when `AutoRegisterTerrains` is disabled in `FloraSceneSettings`.
- Fixed instance selection rotation not being applied correctly in the selection context.
- Fixed instance selection and manipulation not updating correctly when interacting with handles.
- Fixed shaders in some sample scenes not supporting HDRP.

### Changed
- Added editor support for multi-terrain editing in the `FloraTerrainProvider` inspector.
- Added `FloraMinimumScreenSizeMode` and `FloraDensityMode` modes to `FloraRenderSettings` and `FloraDensitySettings` for more explicit control.
- Added compilation support for Unity 6000.3 beta (not supported).
- Removed all legacy code and references to the old culling pipeline and tools package.
- Renamed `FloraToolContext` to `InstanceSelectionContext` as it now handles instance selection and manipulation only.
- Deprecated `FloraCullingPipeline`, the `BatchRendererGroup` culling pipeline is now always used.
- Deprecated `FloraShader` which no longer used with the `BatchRendererGroup` culling pipeline.
- Deprecated all shader patching functionality, as it is no longer needed with the `BatchRendererGroup` culling pipeline.
- Deprecated `TerrainTreeDistance` and `TerrainDetailDistance` on `FloraSceneSettings`, use `FloraTerrainProvider`'s GUI instead.


## [6.2.70] - 2025-09-22

### Fixed
- Fixed leak in terrain detail manager.
- Fixed incorrect patch y-offset in terrain detail manager.
- Fixed `FromToRotation` in detail manager sometimes returning an incorrect quaternion rotation.
- Fixed light probe upload job not being correctly scheduled into the data job queue.
- Fixed crash in parallel radix sort due to incorrect swap. Affected rect selection in instance edit mode.
- Fixed undo/redo re-enabling Unity's default rendering on `FloraInstanceRenderer` objects.
- Fixed `GameObject/Create Instance Container(s)` menu item only available when more than one object was selected.
- Fixed `FloraTerrainProvider` disabling incorrectly when a terrain was un-registered.

### Changed
- Samples improvements along with additional `FloraInstanceContainer` sample scenes.
- Density culling is now always disabled when the editor context is active.
- Added additional warning to `FloraAdditionalRendererSettings` when trying to edit on an instance instead of a prefab.


## [6.2.60] - 2025-09-21

### Fixed
- Fixed instance flags uploading past beyond the changed count.
- Fixed chunk moves sometimes being executed out of order during processing.
- Fixed `NativeBufferArray` metadata being allocated incorrectly.
- Fixed `NativeBufferArray` materials and meshes not being resized in template manager.
- Fixed `LOD_FADE_CROSSFADE` state not being enabled.
- Fixed internal delayed call in `FloraSystem` outliving the system.
- Fixed Burst serialization error.

### Changed
- Added help and error messages when detail prototypes are not using `DetailPrototype.usePrototypeMesh` or their prototype field is null.
- Changed disabling renderer's within `FloraSystem` to instead use the `forceRenderingOff` property, allowing for the renderer enabled property to stay intact.


## [6.2.50] - 2025-09-19

### Fixed
- Fixed animated fade duration being doubled.
- Fixed instances getting the cross-fade state when their materials don't support it.

### Changed
- Optimized detail execution and loading in large scenes with many terrain instances.
- Added warning when materials don't support the cross-fade keyword and have cross-fade enabled.
- Added `ScheduleUpdateInstanceLocalToWorlds`, `ScheduleUpdateInstanceLocalToWorldMatrices`, `ScheduleUpdateInstanceWorldTransforms` and `ScheduleUpdateInstanceLocalTransforms` to `FloraSystem`. Allows scheduling local-to-world updates into the internal update queue, instead of causing a sync point each call.
- Reverted change that moved local-to-world updates to the main thread.


## [6.2.40] - 2025-09-18

### Fixed
- Fixed terrain manager throwing index out of range error when a terrain is unregistered.
- Fixed compile error in Unity 6.2.
- Fixed rendering debugger OnlyLOD option not respecting the lod index.

### Changed
- Removed internal sync point when updating instance cells. Now cell updates are executed at the beginning of the next frame.
- Improved rendering debugger spatial hash visualization colors and options.
- Made `FloraLocalToWorld` a public structure.


## [6.2.30] - 2025-09-17

### Fixed
- Fixed terrain null reference error when unregistering a terrain on a scene change.
- Fixed buffer handles possibly being incorrect when adding to a BatchID.

### Changed
- Refactored instance buffer for better safety at runtime.
- Refactored internal prefab system.


## [6.2.20] - 2025-09-17

### Fixed
- Fixed user settings spamming warnings when open in `Flora` project settings.
- Fixed moving instances being incorrectly batched when changing cells causing slow updates.
- Fixed `FloraInstanceRenderer` source prefab not being correctly set on project prefabs.

### Changed
- Optimized transform update operations.
- Optimized transform and spatial hash change detection.
- Optimized instance moves when changing cells or tags.
- Improved `FloraSystem` lifecycle management, reducing recreation of internal data and better stability when changing scenes.
- Improved `FloraInstanceRenderer` source prefab handling.
- Added material checks for `DOTS_INSTANCING_ON` keyword.


## [6.2.10] - 2025-09-12

### Fixed
- Fixed instances not being disabled on request when using `FloraSystem.SetInstanceEnabled`.
- Fixed disabled instances invalidating other instances in the same chunk.
- Fixed ambient light flickering when a preview camera is open.
- Fixed motion vectors and cross-fade keyword always being set on certain material batches.
- Fixed terrain scene view visibility only working once.
- Fixed wrecking ball sample scene using out dated code.

### Changed
- Improved directional shadow cascade culling. Instances are now correctly culled per shadow cascade split.


## [6.2.00] - 2025-09-11

### Added
- Added support for dynamic draw variants per batch, allowing indirect switching between states per-instance (Cross-Fade, Motion Vector Pass, Flip Winding).
- Added support for the flip winding state in BRG to support negative scaled transforms.
- Added additional checks to prevent materials without the LOD_FADE_CROSSFADE keyword from getting cross-fade states.
- 
### Changed
- Disabled instances are no longer processed, uploaded, or rendered. Disabling instances is now the best way unload instances from the GPU.
- Draw batch states are now dynamically culled on the GPU:
  * Only actively fading instances will have the cross-fade keyword enabled.
  * Only instances actively moving and requesting motion vectors will have the motion vector pass enabled.

### Fixed
- Fixed scene view parameter being used outside an editor define.
- Fixed instance buffer size being calculated in int instead of long, causing buffer overflows to be undetected.


## [6.1.30] - 2025-09-09

### Fixed
- Fixed GPU occlusion with XR rendering.
- Fixed instance multiplier not being set when XR rendering is enabled.
- Fixed compute distance cull check in indirect culling shader.
- Fixed compute compiler error on Windows with enable_d3d11_debug_symbols in indirect culling shader.
- Fixed compute WebGPU support in indirect culling shader.
- Fixed possible null references when disabling child renderers in `FloraSystem`.


## [6.1.20] - 2025-09-02

### Added
- Added flags for minimum screen size and range based density culling affecting LODGroups.

### Fixed
- Fixed LOD transition randomization not working correctly.
- Fixed LOD fade value being asymmetrical when cross-fading, causing artifacts.
- Fixed minimum screen size and range based density screen sizes being overly aggressive.
- Fixed motion vectors not being applied to draws requesting them.
- Fixed LODGroup size taking into account the prefab's local scale, before transformation.
- Fixed debug display errors on 6.2 with URP.
- Fixed shadows not being rendered when no main light is present in the legacy culling pipeline.

## [6.1.10] - 2025-08-29

### Added
- Added MeshLod support to Unity 6.2+.


## [6.1.00] - 2025-08-28

### Added
- Added fade out cross-fade to instances failing minimum screen size and range density tests.
- Added `AffectedByMinimumScreenSize` to `FloraAdditionalRendererSettings`.
- Added LODCrossFade keyword indirection so that non-transitioning instances skip cross-fade calculations in the shader.
- Added additional checks for invalid batch ids in `BatchRendererGroup`.

### Changed
- Improved culling shader performance and range based density culling quality.

### Fixed
- Fixed invalid mesh and material references being held across scene loads.
- Fixed light view LOD calculations not using the correct camera projection mode.
- Fixed `FloraSceneSettings` activating in prefab stages.


## [6.0.6] - 2025-08-14

### Changed
- Improved load operations in the culling compute shader, increasing performance slightly.
- Improved package installer styling.
- Rename `AllowInstanceRenderersInEditMode` to `DisableInstanceRenderersInEditMode` better fit other project variable names.

### Fixed
- Fixed builtin terrain `drawTreesAndFoliage` flag not being reset when `EnableTerrainFoliage` is disabled.
- Fixed warning in wrecking ball sample scene.
- Fixed `FloraGraphicsSettings` warning due to class missing in the render pipeline global settings.
- Fixed package installer windows USS warnings.
- Fixed culling still being executed rendering is disabled in `BatchRendererGroup`, leading to leaks in the system.
- Fixed batches changing handle ids unexpectedly when the instance buffer layout changes.


## [6.0.50] - 2025-08-12

### Changed
- Improved sample code documentation.

### Fixed
- Fixed function pointer exceptions in standalone build with IL2CPP.
- Fixed null reference errors after `EnableTerrainFoliage` is changed in `FloraSceneSettings`
- Fixed package installer not correctly removing old files before upgrading.


## [6.0.40] - 2025-08-11

### Added
- Added `FloraEditorSettings` to allow for editor-only Flora settings.
- Added `AllowInstanceRenderersInEditMode` to `FloraEditorSettings` to optionally disable instance renderers in edit mode.

### Changed
- Renamed `FloraGraphicsSettings` to `FloraRuntimeSettings`, to distinguish between runtime and editor settings.
- Package installer window now actually checks for updates from the package manager asset store.

### Fixed
- Fixed a null reference error in `FloraShaderUtility`.
- Fixed package installer window refreshing constantly.


## [6.0.30] - 2025-08-10

### Added
- Added editor-only and development-only compute variant shader stripping to reduce shader build times.

### Changed
- Made `BatchRendererGroup` picking and selection outlines the default in the editor, regardless of the culling pipeline used.

### Fixed
- Fixed development code being included in non-development builds, causing compilation errors.
- Fixed instances not rendering in non-development builds.
- Fixed random id values not being uploaded in player builds.
- Fixed some distance culling calculations being calculated from the LOD point instead of the AABB center.
- Fixed package manager not correctly removing old files when upgrading.
- Fixed main light calculations executing when `BatchRendererGroup` is used.
- Fixed sample `BatchRendererGroup` toggle not correctly reflecting the current culling pipeline.


## [6.0.20] - 2025-08-03

### Added
- Added `CreateInstances` convenience methods to `FloraSystem` for creating instances.
- Added debug visualization modes to `BatchRendererGroup`.
- Added per-instance random id values, available in ShaderGraph.

### Changed
- Made `BatchRendererGroup` the default culling pipeline, `RenderMeshIndirect` is now legacy.
- Improved `BatchRendererGroup` selection outlines.
- Improved `BatchRendererGroup` picking priority.

### Fixed
- Fixed shader patching when using `Convert Material To Flora` in the editor.
- Fixed ShaderGraph injection still injecting code when disabled in the editor preferences.
- Fixed terrain auto registration be enabled by default when no `FloraSceneSettings` is present.
- Fixed non-LODGroup debug dispatch counts not being calculated.
- Fixed material changes incorrectly unregistering from `BatchRendererGroup`.
- Fixed tree selection causing full rebuilds of tree instance data.
- Fixed demo scene lighting in URP.


## [6.0.10] - 2025-07-29

### Added
- Added terrain tree and detail distance GUI to `FloraTerrainProviderEditor` and `FloraSceneSettings`.

### Fixed
- Fixed player build issue due to editor using in `FloraRuntimeShaders`.
- Fixed hidden documentation folder causing directory naming issues with package manager.
- Fixed possible invalid cross-fade duration values in `FloraRenderSettings`.
- Fixed package manager window not refreshing after a package install or other change.
- Fixed the editor context not correctly unregistering the default scene view selection handler.
- Fixed debug counter out of range error when using the rendering debugger and dispatch statistics.
- Fixed debug counter visible draw call counter.


## [6.0.00] - 2025-07-23

### Changed
- Complete rewrite with new renderer, Batch Renderer Group support, and deep terrain integration.
