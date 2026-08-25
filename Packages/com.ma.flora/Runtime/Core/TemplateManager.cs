// Copyright © Magnetic Arcade. All Rights Reserved.

using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Assertions;

namespace MA.Flora
{
    [GenerateBurstMonoInterop]
    internal unsafe partial struct TemplateManager : IDisposable
    {
        private NativeDataReference<InstanceManager> m_InstanceManager;
        private NativeDataReference<InstanceBuffer> m_InstanceBuffer;
        private NativeDataReference<DrawManager> m_DrawManager;

        private int m_NextTemplateId;
        private UnsafeList<TemplateIndex> m_TemplateFreeList;
        private NativeBitSet m_TemplateAllocated;
        private NativeBitSet m_TemplatesAreGrass;

        private UnsafeParallelHashMap<TemplateKey, TemplateIndex> m_TemplateByKey;
        private int m_NextSourceRecordId;
        private UnsafeList<SourceRecordIndex> m_SourceRecordFreeList;
        private NativeBitSet m_SourceRecordAllocated;
        private UnsafeParallelHashMap<EntityId, SourceRecordIndex> m_SourceRecordBySource;
        private UnsafeParallelHashMap<EntityId, SourceRecordIndex> m_SourceRecordByComponent;
        private NativeArray<SourceRecord> m_SourceRecords;
        private NativeBufferArray<EntityId> m_SourceRecordComponentIds;
        private NativeBufferArray<EntityId> m_SourceRecordRendererIds;
        private NativeBufferArray<TemplateIndex> m_SourceRecordTemplates;
        private NativeBufferArray<FloraInstanceHandle> m_SourceRecordInstances;

        private int m_NextRendererStateId;
        private UnsafeList<RendererStateIndex> m_RendererStateFreeList;
        private NativeBitSet m_RendererStateAllocated;
        private UnsafeParallelMultiHashMap<RendererStateKey, RendererStateIndex> m_RendererStateByKey;
        private NativeArray<RendererStateRecord> m_RendererStateRecords;
        private NativeBufferArray<DrawDescriptor> m_RendererStateDrawDescriptors;
        private NativeBufferArray<DrawBatchIndex> m_RendererStateRegisteredDrawIndices;
        private NativeBufferArray<DrawBatchIndex> m_RendererStateCameraDrawIndices;
        private NativeBufferArray<DrawBatchIndex> m_RendererStateShadowDrawIndices;
        private NativeBufferArray<EntityId> m_RendererStateMaterialInstanceIds;
        private NativeBufferArray<EntityId> m_RendererStateMeshInstanceIds;
        private UnsafeParallelMultiHashMap<EntityId, RendererStateIndex> m_RendererStatesByMaterial;
        private UnsafeParallelMultiHashMap<EntityId, RendererStateIndex> m_RendererStatesByMesh;

        private int m_NextRendererGroupId;
        private UnsafeList<RendererGroupIndex> m_RendererGroupFreeList;
        private NativeBitSet m_RendererGroupAllocated;
        private UnsafeParallelMultiHashMap<RendererGroupKey, RendererGroupIndex> m_RendererGroupByKey;
        private UnsafeParallelMultiHashMap<RendererStateIndex, RendererGroupIndex> m_RendererGroupsByState;
        private NativeArray<RendererGroupRecord> m_RendererGroupRecords;
        private NativeBufferArray<RendererStateIndex> m_RendererGroupStates;
        private NativeBufferArray<DrawBatchIndex> m_RendererGroupRegisteredDrawIndices;
        private NativeBufferArray<DrawBatchIndex> m_RendererGroupCameraDrawIndices;
        private NativeBufferArray<DrawBatchIndex> m_RendererGroupShadowDrawIndices;
        private NativeBufferArray<EntityId> m_RendererGroupMaterialInstanceIds;
        private NativeBufferArray<EntityId> m_RendererGroupMeshInstanceIds;

        private int m_NextTemplateLayoutId;
        private UnsafeList<TemplateLayoutIndex> m_TemplateLayoutFreeList;
        private NativeBitSet m_TemplateLayoutAllocated;
        private UnsafeParallelMultiHashMap<TemplateLayoutKey, TemplateLayoutIndex> m_TemplateLayoutByKey;
        private UnsafeParallelMultiHashMap<RendererGroupIndex, TemplateLayoutIndex> m_TemplateLayoutsByGroup;
        private NativeArray<TemplateLayoutIndex> m_TemplateLayoutBindings;
        private NativeArray<TemplateLayoutRecord> m_TemplateLayoutRecords;
        private NativeBufferArray<RendererGroupIndex> m_TemplateLayoutGroups;
        private NativeBufferArray<DrawBatchIndex> m_TemplateLayoutRegisteredDrawIndices;
        private NativeBufferArray<DrawBatchIndex> m_TemplateLayoutCameraDrawIndices;
        private NativeBufferArray<DrawBatchIndex> m_TemplateLayoutShadowDrawIndices;

        private NativeArray<EntityId> m_GrassMaterialIds;
        private NativeArray<TemplateOptions> m_TemplateOptions;
        private NativeArray<EntityId> m_TemplateRepresentativeRenderSourceIds;
        private NativeBufferArray<SourceRecordIndex> m_TemplateSourceRecords;
        private NativeBufferArray<DrawBatchIndex> m_RegisteredDrawIndices;
        private NativeBufferArray<ChunkIndex> m_Chunks;
        private NativeBufferArray<CullingChunkIndex> m_CullingChunks;
        private NativeBufferArray<DrawBatchIndex> m_CameraDrawIndices;
        private NativeBufferArray<DrawBatchIndex> m_CameraDrawIndicesPerLod;
        private NativeBufferArray<DrawBatchIndex> m_ShadowDrawIndices;
        private NativeBufferArray<DrawBatchIndex> m_ShadowDrawIndicesPerLod;
        private int m_MaxDrawBatchIndices;
        private int m_MaxUsedLodCount;

        private NativeArray<TemplateData> m_TemplateDataArray;
        private GraphicsBufferRef m_TemplateDataBuffer;
        private NativeBitSet m_DirtyTemplateData;
        private NativeBufferArray<TemplateIndex> m_DrawTemplates;
        private NativeBitSet m_DirtyDrawChunks;
        private bool m_TemplateDataNeedsUpload;

        public int MaxCount => m_TemplateAllocated.MaxLength;
        public int MaxUsedLodCount => math.clamp(m_MaxUsedLodCount, 1, CullingConstants.MaxLodCount);

        public NativeBitSet Allocated => m_TemplateAllocated;
        public NativeBufferArray<DrawBatchIndex> CameraDrawIndices => m_CameraDrawIndices;
        public NativeBufferArray<DrawBatchIndex> CameraDrawIndicesPerLod => m_CameraDrawIndicesPerLod;
        public NativeBufferArray<DrawBatchIndex> ShadowDrawIndices => m_ShadowDrawIndices;
        public NativeBufferArray<DrawBatchIndex> ShadowDrawIndicesPerLod => m_ShadowDrawIndicesPerLod;
        public NativeBufferArray<CullingChunkIndex> CullingChunks => m_CullingChunks;
        public GraphicsBufferRef TemplateDataBuffer => m_TemplateDataBuffer;

        private bool CanInstancesHaveMotionVectors => FloraSystem.Instance.AllowPerObjectMotionVectors;
        private bool CanInstancesHaveLightProbes => FloraSystem.Instance.AllowLegacyLightProbes;

        private const int InitialCapacity = 16;

        public void Initialize(InstanceContext instanceContext)
        {
            TemplateStore.Initialize();

            m_InstanceManager = instanceContext.InstanceManager;
            m_InstanceBuffer = instanceContext.InstanceBuffer;
            m_DrawManager = instanceContext.DrawManager;

            m_NextTemplateId = 1;
            m_TemplateFreeList = new UnsafeList<TemplateIndex>(InitialCapacity, Allocator.Persistent);
            m_TemplateAllocated = new NativeBitSet(InitialCapacity, Allocator.Persistent);
            m_TemplatesAreGrass = new NativeBitSet(InitialCapacity, Allocator.Persistent);

            m_TemplateByKey = new UnsafeParallelHashMap<TemplateKey, TemplateIndex>(InitialCapacity, Allocator.Persistent);

            m_NextSourceRecordId = 1;
            m_SourceRecordFreeList = new UnsafeList<SourceRecordIndex>(InitialCapacity, Allocator.Persistent);
            m_SourceRecordAllocated = new NativeBitSet(InitialCapacity, Allocator.Persistent);
            m_SourceRecordBySource = new UnsafeParallelHashMap<EntityId, SourceRecordIndex>(InitialCapacity, Allocator.Persistent);
            m_SourceRecordByComponent = new UnsafeParallelHashMap<EntityId, SourceRecordIndex>(InitialCapacity, Allocator.Persistent);
            m_SourceRecords = new NativeArray<SourceRecord>(InitialCapacity, Allocator.Persistent);
            m_SourceRecordComponentIds = new NativeBufferArray<EntityId>(InitialCapacity, 0, Allocator.Persistent);
            m_SourceRecordRendererIds = new NativeBufferArray<EntityId>(InitialCapacity, 0, Allocator.Persistent);
            m_SourceRecordTemplates = new NativeBufferArray<TemplateIndex>(InitialCapacity, 0, Allocator.Persistent);
            m_SourceRecordInstances = new NativeBufferArray<FloraInstanceHandle>(InitialCapacity, 0, Allocator.Persistent);

            m_NextRendererStateId = 1;
            m_RendererStateFreeList = new UnsafeList<RendererStateIndex>(InitialCapacity, Allocator.Persistent);
            m_RendererStateAllocated = new NativeBitSet(InitialCapacity, Allocator.Persistent);
            m_RendererStateByKey = new UnsafeParallelMultiHashMap<RendererStateKey, RendererStateIndex>(InitialCapacity, Allocator.Persistent);
            m_RendererStateRecords = new NativeArray<RendererStateRecord>(InitialCapacity, Allocator.Persistent);
            m_RendererStateDrawDescriptors = new NativeBufferArray<DrawDescriptor>(InitialCapacity, 0, Allocator.Persistent);
            m_RendererStateRegisteredDrawIndices = new NativeBufferArray<DrawBatchIndex>(InitialCapacity, 0, Allocator.Persistent);
            m_RendererStateCameraDrawIndices = new NativeBufferArray<DrawBatchIndex>(InitialCapacity, 0, Allocator.Persistent);
            m_RendererStateShadowDrawIndices = new NativeBufferArray<DrawBatchIndex>(InitialCapacity, 0, Allocator.Persistent);
            m_RendererStateMaterialInstanceIds = new NativeBufferArray<EntityId>(InitialCapacity, 0, Allocator.Persistent);
            m_RendererStateMeshInstanceIds = new NativeBufferArray<EntityId>(InitialCapacity, 0, Allocator.Persistent);
            m_RendererStatesByMaterial = new UnsafeParallelMultiHashMap<EntityId, RendererStateIndex>(InitialCapacity, Allocator.Persistent);
            m_RendererStatesByMesh = new UnsafeParallelMultiHashMap<EntityId, RendererStateIndex>(InitialCapacity, Allocator.Persistent);

            m_NextRendererGroupId = 1;
            m_RendererGroupFreeList = new UnsafeList<RendererGroupIndex>(InitialCapacity, Allocator.Persistent);
            m_RendererGroupAllocated = new NativeBitSet(InitialCapacity, Allocator.Persistent);
            m_RendererGroupByKey = new UnsafeParallelMultiHashMap<RendererGroupKey, RendererGroupIndex>(InitialCapacity, Allocator.Persistent);
            m_RendererGroupsByState = new UnsafeParallelMultiHashMap<RendererStateIndex, RendererGroupIndex>(InitialCapacity, Allocator.Persistent);
            m_RendererGroupRecords = new NativeArray<RendererGroupRecord>(InitialCapacity, Allocator.Persistent);
            m_RendererGroupStates = new NativeBufferArray<RendererStateIndex>(InitialCapacity, 0, Allocator.Persistent);
            m_RendererGroupRegisteredDrawIndices = new NativeBufferArray<DrawBatchIndex>(InitialCapacity, 0, Allocator.Persistent);
            m_RendererGroupCameraDrawIndices = new NativeBufferArray<DrawBatchIndex>(InitialCapacity, 0, Allocator.Persistent);
            m_RendererGroupShadowDrawIndices = new NativeBufferArray<DrawBatchIndex>(InitialCapacity, 0, Allocator.Persistent);
            m_RendererGroupMaterialInstanceIds = new NativeBufferArray<EntityId>(InitialCapacity, 0, Allocator.Persistent);
            m_RendererGroupMeshInstanceIds = new NativeBufferArray<EntityId>(InitialCapacity, 0, Allocator.Persistent);

            m_NextTemplateLayoutId = 1;
            m_TemplateLayoutFreeList = new UnsafeList<TemplateLayoutIndex>(InitialCapacity, Allocator.Persistent);
            m_TemplateLayoutAllocated = new NativeBitSet(InitialCapacity, Allocator.Persistent);
            m_TemplateLayoutByKey = new UnsafeParallelMultiHashMap<TemplateLayoutKey, TemplateLayoutIndex>(InitialCapacity, Allocator.Persistent);
            m_TemplateLayoutsByGroup = new UnsafeParallelMultiHashMap<RendererGroupIndex, TemplateLayoutIndex>(InitialCapacity, Allocator.Persistent);
            m_TemplateLayoutBindings = new NativeArray<TemplateLayoutIndex>(InitialCapacity, Allocator.Persistent);
            m_TemplateLayoutRecords = new NativeArray<TemplateLayoutRecord>(InitialCapacity, Allocator.Persistent);
            m_TemplateLayoutGroups = new NativeBufferArray<RendererGroupIndex>(InitialCapacity, 0, Allocator.Persistent);
            m_TemplateLayoutRegisteredDrawIndices = new NativeBufferArray<DrawBatchIndex>(InitialCapacity, 0, Allocator.Persistent);
            m_TemplateLayoutCameraDrawIndices = new NativeBufferArray<DrawBatchIndex>(InitialCapacity, 0, Allocator.Persistent);
            m_TemplateLayoutShadowDrawIndices = new NativeBufferArray<DrawBatchIndex>(InitialCapacity, 0, Allocator.Persistent);

            m_GrassMaterialIds = new NativeArray<EntityId>(InitialCapacity, Allocator.Persistent);
            m_TemplateOptions = new NativeArray<TemplateOptions>(InitialCapacity, Allocator.Persistent);
            m_TemplateRepresentativeRenderSourceIds = new NativeArray<EntityId>(InitialCapacity, Allocator.Persistent);
            m_TemplateSourceRecords = new NativeBufferArray<SourceRecordIndex>(InitialCapacity, 0, Allocator.Persistent);
            m_RegisteredDrawIndices = new NativeBufferArray<DrawBatchIndex>(InitialCapacity, 0, Allocator.Persistent);
            m_Chunks = new NativeBufferArray<ChunkIndex>(InitialCapacity, 0, Allocator.Persistent);
            m_CullingChunks = new NativeBufferArray<CullingChunkIndex>(InitialCapacity, 0, Allocator.Persistent);
            m_CameraDrawIndices = new NativeBufferArray<DrawBatchIndex>(InitialCapacity, 0, Allocator.Persistent);
            m_CameraDrawIndicesPerLod = new NativeBufferArray<DrawBatchIndex>(InitialCapacity * CullingConstants.MaxLodCount, 0, Allocator.Persistent);
            m_ShadowDrawIndices = new NativeBufferArray<DrawBatchIndex>(InitialCapacity, 0, Allocator.Persistent);
            m_ShadowDrawIndicesPerLod = new NativeBufferArray<DrawBatchIndex>(InitialCapacity * CullingConstants.MaxLodCount, 0, Allocator.Persistent);
            m_DirtyTemplateData = new NativeBitSet(InitialCapacity, Allocator.Persistent);

            Assert.IsTrue(UnsafeUtility.SizeOf<TemplateData>() % 16 == 0, "FloraPrefabCullingData size must be a multiple of 16 bytes.");
            m_TemplateDataArray = new NativeArray<TemplateData>(InitialCapacity, Allocator.Persistent);
            m_TemplateDataBuffer = new GraphicsBufferRef(GraphicsBuffer.Target.Raw, InitialCapacity, UnsafeUtility.SizeOf<TemplateData>(), "Flora.PrefabData");
            m_MaxDrawBatchIndices = 64;
            m_DrawTemplates = new NativeBufferArray<TemplateIndex>(m_MaxDrawBatchIndices, 0, Allocator.Persistent);
            m_DirtyDrawChunks = new NativeBitSet(m_MaxDrawBatchIndices, Allocator.Persistent);
        }

        public void Dispose()
        {
            m_TemplateFreeList.Dispose();
            m_TemplateAllocated.Dispose();
            m_TemplatesAreGrass.Dispose();

            m_TemplateByKey.Dispose();

            m_SourceRecordFreeList.Dispose();
            m_SourceRecordAllocated.Dispose();
            m_SourceRecordBySource.Dispose();
            m_SourceRecordByComponent.Dispose();
            m_SourceRecords.Dispose();
            m_SourceRecordComponentIds.Dispose();
            m_SourceRecordRendererIds.Dispose();
            m_SourceRecordTemplates.Dispose();
            m_SourceRecordInstances.Dispose();

            m_RendererStateFreeList.Dispose();
            m_RendererStateAllocated.Dispose();
            m_RendererStateByKey.Dispose();
            m_RendererStateRecords.Dispose();
            m_RendererStateDrawDescriptors.Dispose();
            m_RendererStateRegisteredDrawIndices.Dispose();
            m_RendererStateCameraDrawIndices.Dispose();
            m_RendererStateShadowDrawIndices.Dispose();
            m_RendererStateMaterialInstanceIds.Dispose();
            m_RendererStateMeshInstanceIds.Dispose();
            m_RendererStatesByMaterial.Dispose();
            m_RendererStatesByMesh.Dispose();

            m_RendererGroupFreeList.Dispose();
            m_RendererGroupAllocated.Dispose();
            m_RendererGroupByKey.Dispose();
            m_RendererGroupsByState.Dispose();
            m_RendererGroupRecords.Dispose();
            m_RendererGroupStates.Dispose();
            m_RendererGroupRegisteredDrawIndices.Dispose();
            m_RendererGroupCameraDrawIndices.Dispose();
            m_RendererGroupShadowDrawIndices.Dispose();
            m_RendererGroupMaterialInstanceIds.Dispose();
            m_RendererGroupMeshInstanceIds.Dispose();

            m_TemplateLayoutFreeList.Dispose();
            m_TemplateLayoutAllocated.Dispose();
            m_TemplateLayoutByKey.Dispose();
            m_TemplateLayoutsByGroup.Dispose();
            m_TemplateLayoutBindings.Dispose();
            m_TemplateLayoutRecords.Dispose();
            m_TemplateLayoutGroups.Dispose();
            m_TemplateLayoutRegisteredDrawIndices.Dispose();
            m_TemplateLayoutCameraDrawIndices.Dispose();
            m_TemplateLayoutShadowDrawIndices.Dispose();

            m_GrassMaterialIds.Dispose();
            m_TemplateOptions.Dispose();
            m_TemplateRepresentativeRenderSourceIds.Dispose();
            m_TemplateSourceRecords.Dispose();
            m_RegisteredDrawIndices.Dispose();
            m_Chunks.Dispose();
            m_CullingChunks.Dispose();
            m_CameraDrawIndices.Dispose();
            m_CameraDrawIndicesPerLod.Dispose();
            m_ShadowDrawIndices.Dispose();
            m_ShadowDrawIndicesPerLod.Dispose();
            m_DirtyTemplateData.Dispose();

            m_TemplateDataArray.Dispose();
            m_TemplateDataBuffer.Dispose();
            m_DrawTemplates.Dispose();
            m_DirtyDrawChunks.Dispose();
        }

        public void RebuildDrawBatches()
        {
            if (m_DrawManager.ValueRO.NeedsRebuild)
                m_DrawManager.ValueRW.Rebuild();

            if (!m_DirtyDrawChunks.IsEmpty)
                RebuildDirtyDrawChunks();

            if (m_TemplateDataNeedsUpload)
            {
                m_TemplateDataNeedsUpload = false;
                m_TemplateDataBuffer.SetData(m_TemplateDataArray);
                m_DirtyTemplateData.Clear();
            }
            else if (!m_DirtyTemplateData.IsEmpty)
            {
                UploadDirtyTemplateData();
            }
        }

        public void AddCullingChunk(TemplateIndex template, CullingChunkIndex chunk, NativeArray<int> chunkIndexInTemplateList)
        {
            Assert.IsTrue(chunkIndexInTemplateList[chunk] == -1, "Chunk is already in the template.");
            var chunks = m_CullingChunks[template];
            chunkIndexInTemplateList[chunk] = chunks.Length;
            m_CullingChunks[template].Add(chunk);
            MarkTemplateDrawsDirty(template);
        }

        public void RemoveCullingChunk(TemplateIndex template, CullingChunkIndex chunk, NativeArray<int> chunkIndexInTemplateList)
        {
            Assert.IsTrue(chunkIndexInTemplateList[chunk] != -1, "Chunk is not in the template.");
            var chunks = m_CullingChunks[template];
            var indexInTemplateList = chunkIndexInTemplateList[chunk];
            chunks.RemoveAtSwapBack(indexInTemplateList);
            MarkTemplateDrawsDirty(template);

            if (indexInTemplateList < chunks.Length)
            {
                var chunkThatMoved = chunks[indexInTemplateList];
                chunkIndexInTemplateList[chunkThatMoved] = indexInTemplateList;
            }
        }

        private void EnsureDrawTrackingCapacity(int minCapacity)
        {
            if (minCapacity <= m_MaxDrawBatchIndices)
                return;

            int newCapacity = math.max(minCapacity, m_MaxDrawBatchIndices * 2);
            m_DrawTemplates.Resize(newCapacity);
            m_DirtyDrawChunks.ReserveCapacity(newCapacity);
            m_MaxDrawBatchIndices = newCapacity;
        }

        private void MarkTemplateDataDirty(TemplateIndex template)
        {
            m_DirtyTemplateData.Add(template);
        }

        private void ResetTemplateData(TemplateIndex template)
        {
            m_TemplateDataArray[template] = default;
            MarkTemplateDataDirty(template);
        }

        internal readonly GameObject GetTemplateRepresentativeRenderSource(TemplateIndex template)
        {
            if (!template.IsCreated || !m_TemplateAllocated.Contains(template))
                return null;

            return m_TemplateRepresentativeRenderSourceIds[template].ToObject<GameObject>();
        }

        internal readonly EntityId GetTemplateRepresentativeRenderSourceId(TemplateIndex template)
        {
            if (!template.IsCreated || !m_TemplateAllocated.Contains(template))
                return EntityId.None;

            return m_TemplateRepresentativeRenderSourceIds[template];
        }

        private void MarkTemplateDrawsDirty(TemplateIndex template)
        {
            var drawIndices = m_RegisteredDrawIndices[template];
            for (int i = 0; i < drawIndices.Length; i++)
                m_DirtyDrawChunks.Add(drawIndices[i]);
        }

        private void AddTemplateDrawOwnership(TemplateIndex template, NativeBuffer<DrawBatchIndex> drawIndices)
        {
            for (int i = 0; i < drawIndices.Length; i++)
            {
                DrawBatchIndex drawIndex = drawIndices[i];
                EnsureDrawTrackingCapacity(drawIndex + 1);
                var templates = m_DrawTemplates[drawIndex];
                bool alreadyOwned = false;
                for (int templateIndex = 0; templateIndex < templates.Length; templateIndex++)
                {
                    if (templates[templateIndex] == template)
                    {
                        alreadyOwned = true;
                        break;
                    }
                }

                Assert.IsFalse(alreadyOwned, "Template draw ownership was registered more than once.");
                templates.Add(template);
                m_DirtyDrawChunks.Add(drawIndex);
            }
        }

        private void RemoveTemplateDrawOwnership(TemplateIndex template, NativeBuffer<DrawBatchIndex> drawIndices)
        {
            for (int i = 0; i < drawIndices.Length; i++)
            {
                DrawBatchIndex drawIndex = drawIndices[i];
                bool removed = false;
                if (drawIndex < m_DrawTemplates.Length)
                {
                    var templates = m_DrawTemplates[drawIndex];
                    for (int templateIndex = 0; templateIndex < templates.Length; templateIndex++)
                    {
                        if (templates[templateIndex] == template)
                        {
                            templates.RemoveAtSwapBack(templateIndex);
                            removed = true;
                            break;
                        }
                    }
                }

                Assert.IsTrue(removed || !m_DrawManager.ValueRO.ContainsDraw(drawIndex),
                    "Tracked draw batch is missing template ownership.");
                m_DirtyDrawChunks.Add(drawIndex);
            }
        }

        private void RebuildDirtyDrawChunks()
        {
            foreach (DrawBatchIndex drawIndex in m_DirtyDrawChunks.AsType<DrawBatchIndex>())
            {
                if (!m_DrawManager.ValueRO.ContainsDraw(drawIndex))
                {
                    Assert.IsTrue(drawIndex >= m_DrawTemplates.Length || m_DrawTemplates[drawIndex].Length == 0,
                        "Released draw batch still has template ownership.");
                    continue;
                }

                m_DrawManager.ValueRW.ClearCullingChunks(drawIndex);

                var drawTemplates = m_DrawTemplates[drawIndex];
                for (int i = 0; i < drawTemplates.Length; i++)
                {
                    TemplateIndex template = drawTemplates[i];
                    Assert.IsTrue(m_TemplateAllocated.Contains(template), "Draw batch references a template that is no longer allocated.");
                    m_DrawManager.ValueRW.AddCullingChunks(drawIndex, m_CullingChunks[template].AsArray());
                }
            }

            m_DirtyDrawChunks.Clear();
        }

        private void UploadDirtyTemplateData()
        {
            Assert.IsFalse(m_TemplateDataNeedsUpload, "Partial template data uploads cannot run after the template buffer contents were discarded.");

            int startIndex = -1;
            int previousIndex = -1;
            // NativeBitSet enumerates set indices in ascending order, so contiguous dirty templates can be uploaded in ranges.
            foreach (TemplateIndex template in m_DirtyTemplateData.AsType<TemplateIndex>())
            {
                Assert.IsTrue(startIndex == -1 || template > previousIndex,
                    "Dirty template iteration must remain ascending for range uploads.");
                if (startIndex == -1)
                {
                    startIndex = previousIndex = template;
                    continue;
                }

                if (template == previousIndex + 1)
                {
                    previousIndex = template;
                    continue;
                }

                int count = previousIndex - startIndex + 1;
                m_TemplateDataBuffer.SetData(m_TemplateDataArray, startIndex, startIndex, count);
                startIndex = previousIndex = template;
            }

            if (startIndex != -1)
            {
                int count = previousIndex - startIndex + 1;
                m_TemplateDataBuffer.SetData(m_TemplateDataArray, startIndex, startIndex, count);
            }

            m_DirtyTemplateData.Clear();
        }

        private void EnsureTemplateCapacity(int minCapacity)
        {
            if (minCapacity <= m_GrassMaterialIds.Length)
                return;

            int newCapacity = math.max(minCapacity, m_GrassMaterialIds.Length * 2);
            m_GrassMaterialIds.ResizeArraySafe(newCapacity);
            m_TemplateOptions.ResizeArraySafe(newCapacity);
            m_TemplateRepresentativeRenderSourceIds.ResizeArraySafe(newCapacity);
            m_TemplateSourceRecords.Resize(newCapacity);
            m_TemplateLayoutBindings.ResizeArraySafe(newCapacity);
            m_RegisteredDrawIndices.Resize(newCapacity);
            m_Chunks.Resize(newCapacity);
            m_CullingChunks.Resize(newCapacity);
            m_CameraDrawIndices.Resize(newCapacity);
            m_CameraDrawIndicesPerLod.Resize(newCapacity * CullingConstants.MaxLodCount);
            m_ShadowDrawIndices.Resize(newCapacity);
            m_ShadowDrawIndicesPerLod.Resize(newCapacity * CullingConstants.MaxLodCount);
            m_TemplateDataArray.ResizeArraySafe(newCapacity);
            m_DirtyTemplateData.ReserveCapacity(newCapacity);
            m_TemplateDataBuffer.ResizeAndDiscardContents(newCapacity);
            m_TemplateDataNeedsUpload = true;
        }

        public bool AddChunk(TemplateIndex template, ChunkIndex chunk)
        {
            Assert.IsTrue(chunk.IndexInTemplateList == -1, "Chunk is already in the template.");
            var chunks = m_Chunks[template];
            chunk.IndexInTemplateList = chunks.Length;
            chunks.Add(chunk);
            return true;
        }

        public bool RemoveChunk(TemplateIndex template, ChunkIndex chunk)
        {
            Assert.IsTrue(chunk.IndexInTemplateList != -1, "Chunk is not in the template.");
            var chunks = m_Chunks[template];
            var indexInPrefabList = chunk.IndexInTemplateList;
            chunks.RemoveAtSwapBack(indexInPrefabList);

            // Fix the chunk that was swapped back
            if (indexInPrefabList < chunks.Length)
            {
                var chunkThatMoved = chunks[indexInPrefabList];
                chunkThatMoved.IndexInTemplateList = indexInPrefabList;
            }

            return true;
        }

        public void AddInstancesToSourceRecord(SourceRecordIndex sourceRecord, FloraInstanceHandle* instances, int count)
        {
            if (!sourceRecord.IsCreated || !m_SourceRecordAllocated.Contains(sourceRecord))
                return;

            var sourceInstances = m_SourceRecordInstances[sourceRecord];
            for (int i = 0; i < count; i++)
            {
                var instance = instances[i];
                var indexInList = sourceInstances.Length;
                sourceInstances.Add(instance);
                InstanceRegistry.Data.SetInstanceInSourceRecord(instance, new InstanceInSourceRecord { SourceRecord = sourceRecord, IndexInList = indexInList });
            }
        }

        public void RemoveInstancesFromSourceRecords(FloraInstanceHandle* instances, int count)
        {
            for (int i = 0; i < count; i++)
            {
                var instance = instances[i];
                var instanceInSourceRecord = InstanceRegistry.Data.GetInstanceInSourceRecord(instance);
                if (!instanceInSourceRecord.Equals(InstanceInSourceRecord.None) &&
                    m_SourceRecordAllocated.Contains(instanceInSourceRecord.SourceRecord))
                {
                    NativeBuffer<FloraInstanceHandle> sourceInstances = m_SourceRecordInstances[instanceInSourceRecord.SourceRecord];
                    int indexInList = instanceInSourceRecord.IndexInList;
                    sourceInstances.RemoveAtSwapBack(indexInList);

                    if (indexInList < sourceInstances.Length)
                    {
                        var movedInstance = sourceInstances[indexInList];
                        InstanceRegistry.Data.SetInstanceInSourceRecord(movedInstance,
                            new InstanceInSourceRecord { SourceRecord = instanceInSourceRecord.SourceRecord, IndexInList = indexInList });
                    }

                    InstanceRegistry.Data.SetInstanceInSourceRecord(instance, default);
                    TryDestroySourceRecordIfUnused(instanceInSourceRecord.SourceRecord);
                }
            }
        }

    }
}
