// Copyright © Magnetic Arcade. All Rights Reserved.

using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

namespace MA.Flora
{
    internal partial class CullingSystem
    {
        [BurstCompile(DisableSafetyChecks = true, OptimizeFor = OptimizeFor.Performance)]
        private struct GatherIncludeExcludeBitsJob : IJobParallelFor
        {
            [ReadOnly] public IncludeExcludeListFilter IncludeExcludeListFilter;
            [ReadOnly] public NativeArray<CullingChunkIndex> Chunks;
            [ReadOnly] public NativeArray<int> ChunkCounts;
            [ReadOnly] public NativeArray<int> InstanceIndices;
            [ReadOnly] public NativeArray<FloraInstanceHandle> InstanceHandles;

            [WriteOnly] public NativeArray<ulong> IncludedInstances;

            public void Execute(int index)
            {
                var chunk = Chunks[index];
                if (chunk >= ChunkCounts.Length)
                {
                    IncludedInstances[index] = 0ul;
                    return;
                }

                int baseInstanceIndex = chunk * InstanceManager.ChunkCapacity;
                int instanceCount = ChunkCounts[chunk];

                var included = 0ul;
                for (int i = 0; i < instanceCount; ++i)
                {
                    var instanceIndex = InstanceIndices[baseInstanceIndex + i];
                    var instanceHandle = InstanceHandles[instanceIndex];
#if UNITY_6000_5_OR_NEWER
                    EntityId floraEntityId = EntityId.FromULong(UnsafeUtility.As<FloraInstanceHandle, ulong>(ref instanceHandle));
#else
                    EntityId floraEntityId = instanceHandle.Index;
#endif
                    if (IncludeExcludeListFilter.InstanceIncluded(floraEntityId))
                        included |= (1ul << i);
                }

                IncludedInstances[index] = included;
            }
        }

        [BurstCompile(DisableSafetyChecks = true, OptimizeFor = OptimizeFor.Performance)]
        private struct GatherVisibleAuthoringEntityIds : IJobParallelFor
        {
            [ReadOnly] public NativeArray<CullingChunkIndex> Chunks;
            [ReadOnly] public NativeArray<int> ChunkCounts;
            [ReadOnly] public NativeArray<FloraInstanceHandle> InstanceHandles;

            [WriteOnly,NativeDisableParallelForRestriction] public NativeArray<EntityId> AuthoringEntityIds;

            public void Execute(int index)
            {
                var chunk = Chunks[index];
                int baseInstanceIndex = chunk * InstanceManager.ChunkCapacity;
                int instanceCount = ChunkCounts[chunk];
                int baseOutputIndex = index * InstanceManager.ChunkCapacity;

                for (int i = 0; i < instanceCount; ++i)
                {
                    var instanceHandle = InstanceHandles[baseInstanceIndex + i];
                    var authoringEntityId = InstanceRegistry.Data.GetSceneEntityId(instanceHandle);
                    AuthoringEntityIds[baseOutputIndex + i] = authoringEntityId;
                }
            }
        }

        [BurstCompile(DisableSafetyChecks = true, OptimizeFor = OptimizeFor.Performance)]
        private struct FilterChunksByAuthoringEntityId : IJobParallelFor
        {
            [ReadOnly] public NativeArray<CullingChunkIndex> Chunks;
            [ReadOnly] public NativeArray<int> ChunkCounts;
            [ReadOnly] public NativeArray<bool> FilteredSceneObjects;

            [WriteOnly] public NativeArray<ulong> IncludedInstances;

            public void Execute(int index)
            {
                var chunk = Chunks[index];
                int baseInstanceIndex = chunk * InstanceManager.ChunkCapacity;
                int instanceCount = ChunkCounts[chunk];

                ulong included = 0ul;

                for (int i = 0; i < instanceCount; ++i)
                {
                    if (FilteredSceneObjects[baseInstanceIndex + i])
                        included |= 1ul << i;
                }

                IncludedInstances[index] = included;
            }
        }

        private struct IncludeExcludeListFilter
        {
#if UNITY_EDITOR
            public NativeParallelHashSet<EntityId> IncludeInstanceIndices;
            public NativeParallelHashSet<EntityId> ExcludeInstanceIndices;
            public bool IsIncludeEnabled;
            public bool IsExcludeEnabled;

            public bool IsEnabled => IsIncludeEnabled || IsExcludeEnabled;
            public bool IsIncludeEmpty => IncludeInstanceIndices.IsEmpty;
            public bool IsExcludeEmpty => ExcludeInstanceIndices.IsEmpty;

            public IncludeExcludeListFilter(
                NativeArray<EntityId> includeInstanceIndices,
                NativeArray<EntityId> excludeInstanceIndices,
                Allocator allocator)
            {
                IncludeInstanceIndices = default;
                ExcludeInstanceIndices = default;

                // Null NativeArray means that the list shoudln't be used for filtering
                IsIncludeEnabled = includeInstanceIndices.IsCreated;
                IsExcludeEnabled = excludeInstanceIndices.IsCreated;

                if (IsIncludeEnabled)
                {
                    IncludeInstanceIndices = new NativeParallelHashSet<EntityId>(includeInstanceIndices.Length, allocator);
                    for (int i = 0; i < includeInstanceIndices.Length; ++i)
                        IncludeInstanceIndices.Add(includeInstanceIndices[i]);
                }
                else
                {
                    // NativeParallelHashSet must be non-null even if empty to be passed to jobs. Otherwise errors happen.
                    IncludeInstanceIndices = new NativeParallelHashSet<EntityId>(0, allocator);
                }

                if (IsExcludeEnabled)
                {
                    ExcludeInstanceIndices = new NativeParallelHashSet<EntityId>(excludeInstanceIndices.Length, allocator);
                    for (int i = 0; i < excludeInstanceIndices.Length; ++i)
                        ExcludeInstanceIndices.Add(excludeInstanceIndices[i]);
                }
                else
                {
                    // NativeParallelHashSet must be non-null even if empty to be passed to jobs. Otherwise errors happen.
                    ExcludeInstanceIndices = new NativeParallelHashSet<EntityId>(0, allocator);
                }
            }

            public void Dispose()
            {
                if (IncludeInstanceIndices.IsCreated)
                    IncludeInstanceIndices.Dispose();

                if (ExcludeInstanceIndices.IsCreated)
                    ExcludeInstanceIndices.Dispose();
            }

            public JobHandle Dispose(JobHandle dependencies)
            {
                JobHandle disposeInclude = IncludeInstanceIndices.IsCreated ? IncludeInstanceIndices.Dispose(dependencies) : default;
                JobHandle disposeExclude = ExcludeInstanceIndices.IsCreated ? ExcludeInstanceIndices.Dispose(dependencies) : default;
                return JobHandle.CombineDependencies(disposeInclude, disposeExclude);
            }

            public bool InstanceIncluded(EntityId instanceIndex)
            {
                if (IsIncludeEnabled)
                {
                    if (!IncludeInstanceIndices.Contains(instanceIndex))
                        return false;
                }

                if (IsExcludeEnabled)
                {
                    if (ExcludeInstanceIndices.Contains(instanceIndex))
                        return false;
                }

                return true;
            }
#else
            public bool IsIncludeEnabled => false;
            public bool IsExcludeEnabled => false;
            public bool IsEnabled => false;
            public bool IsIncludeEmpty => true;
            public bool IsExcludeEmpty => true;
            public bool InstanceIncluded(EntityId instanceIndex) => true;
            public void Dispose() { }
            public JobHandle Dispose(JobHandle dependencies) => new JobHandle();
#endif
        }

        // This function does only return a meaningful IncludeExcludeListFilter object when called from a BRG culling callback.
        private IncludeExcludeListFilter GetPickingIncludeExcludeListFilterForCurrentCullingCallback(in BatchCullingContext cullingContext)
        {
#if UNITY_EDITOR
#if UNITY_6000_3_OR_NEWER
            PickingIncludeExcludeEntityIdList includeExcludeList = HandleUtility.GetPickingIncludeExcludeEntityIdList(Allocator.Temp);
            if (cullingContext.viewType == BatchCullingViewType.Picking)
            {
                includeExcludeList = HandleUtility.GetPickingIncludeExcludeEntityIdList(Allocator.Temp);
            }
            else if (cullingContext.viewType == BatchCullingViewType.SelectionOutline)
            {
                includeExcludeList = HandleUtility.GetSelectionOutlineIncludeExcludeEntityIdList(Allocator.Temp);
            }
#else
            PickingIncludeExcludeList includeExcludeList = default;
            if (cullingContext.viewType == BatchCullingViewType.Picking)
            {
                includeExcludeList = HandleUtility.GetPickingIncludeExcludeList(Allocator.Temp);
            }
            else if (cullingContext.viewType == BatchCullingViewType.SelectionOutline)
            {
                includeExcludeList = HandleUtility.GetSelectionOutlineIncludeExcludeList(Allocator.Temp);
            }
#endif

            NativeArray<EntityId> emptyArray = new NativeArray<EntityId>(0, Allocator.Temp);
#if UNITY_6000_5_OR_NEWER
            NativeArray<EntityId> includeInstanceIndices = includeExcludeList.IncludeEntities;
#else
            NativeArray<EntityId> includeInstanceIndices = includeExcludeList.IncludeEntities.Reinterpret<EntityId>();
#endif
            if (cullingContext.viewType == BatchCullingViewType.SelectionOutline)
            {
                // Make sure the include list for the selection outline is never null even if there is nothing in it.
                // Null NativeArray and empty NativeArray are treated as different things when used to construct an IncludeExcludeListFilter object:
                // - Null include list means that nothing is discarded because the filtering is skipped.
                // - Empty include list means that everything is discarded because the filtering is enabled but never passes.
                // With selection outline culling, we want the filtering to happen in any case even if the array contains nothing so that we don't highlight everything in the latter case.
                if (!includeInstanceIndices.IsCreated)
                    includeInstanceIndices = emptyArray;
            }
            else if (includeInstanceIndices.Length == 0)
            {
                includeInstanceIndices = default;
            }

#if UNITY_6000_5_OR_NEWER
            NativeArray<EntityId> excludeInstanceIndices = includeExcludeList.ExcludeEntities;
#else
            NativeArray<EntityId> excludeInstanceIndices = includeExcludeList.ExcludeEntities.Reinterpret<EntityId>();
#endif
            if (excludeInstanceIndices.Length == 0)
                excludeInstanceIndices = default;

            IncludeExcludeListFilter includeExcludeListFilter = new IncludeExcludeListFilter(
                includeInstanceIndices,
                excludeInstanceIndices,
                Allocator.TempJob);

            includeExcludeList.Dispose();
            emptyArray.Dispose();

            return includeExcludeListFilter;
#else
            return default;
#endif
        }

#if UNITY_EDITOR
        private bool CanCullHiddenInstances_EditorOnly(in BatchCullingContext cc)
        {
            bool isSceneViewCamera = m_RenderingCameraIsSceneView && cc.viewType is BatchCullingViewType.Camera or BatchCullingViewType.Light;
            bool isEditorCullingViewType = cc.viewType is BatchCullingViewType.Picking or BatchCullingViewType.SelectionOutline or BatchCullingViewType.Filtering;
            if (!isSceneViewCamera && !isEditorCullingViewType)
                return false;

            bool isEditingPrefab = PrefabStageUtility.GetCurrentPrefabStage() != null;
            bool isAnyObjectHidden = false;

            for (int i = 0; i < SceneManager.sceneCount; ++i)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (SceneVisibilityManager.instance.AreAnyDescendantsHidden(scene))
                {
                    isAnyObjectHidden = true;
                    break;
                }
            }

            return isAnyObjectHidden || isEditingPrefab;
        }
#endif
    }
}
