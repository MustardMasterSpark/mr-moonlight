// Copyright © Magnetic Arcade. All Rights Reserved.

using System;
using MA.InternalBridge;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace MA.Flora
{
    internal struct TemplateVisibilitySummary
    {
        public int VisibleChunkCount;
        public int VisibleInstanceCount;
        // Ordered chunks and partition summaries currently share the same workspace slice length,
        // but they are different logical streams. Keep both offsets explicit at use sites.
        public int OrderedChunkOffset;
        public int PartitionSummaryOffset;
        public int DrawPartitionCount;
        public int DrawPartitionOffset;
    }

    internal struct PartitionSummary
    {
        public int LightmapIndex;
        public int VisibleInstanceCount;
        public int BinOffset;
        public int CommandOffset;
        public int VisibleInstanceOffset;
        public byte CandidateStateMask;
        public byte StateMask;
        public uint StateIndices;
    }

    internal struct CullingPlanBuffers : IDisposable
    {
        public NativeArray<TemplateVisibilitySummary> TemplateSummaries;
        public NativeArray<PartitionSummary> Partitions;
        public NativeArray<int> TemplateCommandCounts;
        public NativeArray<byte> PartitionStateSplitMasks;
        public NativeArray<CullingChunkIndex> OrderedVisibleChunks;
        public NativeArray<byte> OrderedVisibleChunkStateMasks;
        public NativeArray<byte> OrderedVisibleChunkSplitMasks;
        public NativeArray<int> OrderedVisibleChunkDrawPartitions;
        public NativeArray<int> OrderedVisibleChunkSourceIndices;
        public NativeArray<int> RangeCommandCounts;
        public NativeArray<int> RangeCommandOffsets;
        public NativeArray<int> RangeCommandWriteCursors;

        public int LiveTemplateCount;
        public int LiveVisibleChunkCount;
        public int LiveRangeCount;

        public void EnsureCapacity(int liveTemplateCount, int liveVisibleChunkCount, int liveRangeCount)
        {
            LiveTemplateCount = liveTemplateCount;
            LiveVisibleChunkCount = liveVisibleChunkCount;
            LiveRangeCount = liveRangeCount;

            EnsureArrayCapacity(ref TemplateSummaries, liveTemplateCount);
            EnsureArrayCapacity(ref Partitions, liveVisibleChunkCount);
            EnsureArrayCapacity(ref TemplateCommandCounts, liveTemplateCount * DrawStateUtility.StateLodStride);
            EnsureArrayCapacity(ref PartitionStateSplitMasks, liveVisibleChunkCount * DrawStateUtility.StateKeyCount);
            EnsureArrayCapacity(ref OrderedVisibleChunks, liveVisibleChunkCount);
            EnsureArrayCapacity(ref OrderedVisibleChunkStateMasks, liveVisibleChunkCount);
            EnsureArrayCapacity(ref OrderedVisibleChunkSplitMasks, liveVisibleChunkCount);
            EnsureArrayCapacity(ref OrderedVisibleChunkDrawPartitions, liveVisibleChunkCount);
            EnsureArrayCapacity(ref OrderedVisibleChunkSourceIndices, liveVisibleChunkCount);
            EnsureArrayCapacity(ref RangeCommandCounts, liveRangeCount);
            EnsureArrayCapacity(ref RangeCommandOffsets, liveRangeCount);
            EnsureArrayCapacity(ref RangeCommandWriteCursors, liveRangeCount);
        }

        public NativeArray<TemplateVisibilitySummary> LiveTemplateSummaries => TemplateSummaries.GetSubArray(0, LiveTemplateCount);
        public NativeArray<PartitionSummary> LivePartitions => Partitions.GetSubArray(0, LiveVisibleChunkCount);
        public NativeArray<int> LiveTemplateCommandCounts => TemplateCommandCounts.GetSubArray(0, LiveTemplateCount * DrawStateUtility.StateLodStride);
        public NativeArray<byte> LivePartitionStateSplitMasks => PartitionStateSplitMasks.GetSubArray(0, LiveVisibleChunkCount * DrawStateUtility.StateKeyCount);
        public NativeArray<CullingChunkIndex> LiveOrderedVisibleChunks => OrderedVisibleChunks.GetSubArray(0, LiveVisibleChunkCount);
        public NativeArray<byte> LiveOrderedVisibleChunkStateMasks => OrderedVisibleChunkStateMasks.GetSubArray(0, LiveVisibleChunkCount);
        public NativeArray<byte> LiveOrderedVisibleChunkSplitMasks => OrderedVisibleChunkSplitMasks.GetSubArray(0, LiveVisibleChunkCount);
        public NativeArray<int> LiveOrderedVisibleChunkDrawPartitions => OrderedVisibleChunkDrawPartitions.GetSubArray(0, LiveVisibleChunkCount);
        public NativeArray<int> LiveOrderedVisibleChunkSourceIndices => OrderedVisibleChunkSourceIndices.GetSubArray(0, LiveVisibleChunkCount);
        public NativeArray<int> LiveRangeCommandCounts => RangeCommandCounts.GetSubArray(0, LiveRangeCount);
        public NativeArray<int> LiveRangeCommandOffsets => RangeCommandOffsets.GetSubArray(0, LiveRangeCount);
        public NativeArray<int> LiveRangeCommandWriteCursors => RangeCommandWriteCursors.GetSubArray(0, LiveRangeCount);

        public void Dispose()
        {
            if (!TemplateSummaries.IsCreated)
                return;

            TemplateSummaries.Dispose();
            Partitions.Dispose();
            TemplateCommandCounts.Dispose();
            PartitionStateSplitMasks.Dispose();
            OrderedVisibleChunks.Dispose();
            OrderedVisibleChunkStateMasks.Dispose();
            OrderedVisibleChunkSplitMasks.Dispose();
            OrderedVisibleChunkDrawPartitions.Dispose();
            OrderedVisibleChunkSourceIndices.Dispose();
            RangeCommandCounts.Dispose();
            RangeCommandOffsets.Dispose();
            RangeCommandWriteCursors.Dispose();

            LiveTemplateCount = 0;
            LiveVisibleChunkCount = 0;
            LiveRangeCount = 0;
        }

        private static void EnsureArrayCapacity<T>(ref NativeArray<T> array, int capacity) where T : unmanaged
        {
            int requiredCapacity = math.max(1, capacity);
            if (array.IsCreated && array.Length >= requiredCapacity)
                return;

            int grownCapacity = array.IsCreated
                ? math.max(requiredCapacity, array.Length + (array.Length >> 1))
                : requiredCapacity;
            array.ResizeArraySafe(grownCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
        }
    }

    internal unsafe partial class CullingSystem
    {
        private struct ViewCullingInputs : IDisposable
        {
            public IncludeExcludeListFilter IncludeExcludeListFilter;
            public ReceiverPlanes ReceiverPlanes;
            public ReceiverSphereCuller ReceiverSphereCuller;
            public FrustumPlaneCuller FrustumPlaneCuller;
            public DrawBinConfig BinConfig;
            public float ScreenRelativeMetric;
            public float MeshLodSelectionConstant;

            public void Dispose()
            {
                IncludeExcludeListFilter.Dispose(default);
                FrustumPlaneCuller.Dispose(default);
                ReceiverSphereCuller.Dispose(default);
                ReceiverPlanes.Dispose(default);
            }

            public JobHandle Dispose(JobHandle handle)
            {
                JobHandle includeExcludeDispose = IncludeExcludeListFilter.Dispose(handle);
                JobHandle frustumDispose = FrustumPlaneCuller.Dispose(handle);
                JobHandle sphereDispose = ReceiverSphereCuller.Dispose(handle);
                JobHandle receiverPlanesDispose = ReceiverPlanes.Dispose(handle);
                JobHandle frustumInputsDispose = JobHandle.CombineDependencies(frustumDispose, sphereDispose, receiverPlanesDispose);
                return JobHandle.CombineDependencies(includeExcludeDispose, frustumInputsDispose);
            }
        }

        private struct CullingScratch : IDisposable
        {
            public NativeArray<byte> CellVisibility;
            public NativeList<CullingChunkIndex> VisibleChunks;
            public NativeArray<DrawVisibilityMask> ChunkVisibility;
            public NativeArray<TemplateVisibilitySummary> TemplateSummaries;
            public NativeArray<byte> OrderedVisibleChunkStateMasks;
            public NativeArray<int> OrderedVisibleChunkSourceIndices;
            public NativeArray<int> OrderedVisibleChunkDrawPartitions;
            public NativeArray<CullingChunkIndex> OrderedVisibleChunks;
            public NativeArray<int> TemplateChunkWriteCursors;
            public NativeArray<int> TemplateCommandCounts;
            public NativeArray<byte> PartitionStateSplitMasks;
            public NativeArray<PartitionSummary> Partitions;
            public NativeArray<int> RangeCommandCounts;
            public NativeArray<int> RangeCommandOffsets;
            public NativeArray<CullingLayoutCounts> LayoutCounts;

            public void PrepareForView(int cellCapacity, int visibleChunkCapacity, int templateCapacity, int rangeCapacity)
            {
                EnsureCellCapacity(cellCapacity);
                EnsureCapacity(visibleChunkCapacity, templateCapacity, rangeCapacity);

                CellVisibility.MemClear(0, cellCapacity);
                ChunkVisibility.MemClear(0, visibleChunkCapacity);
                TemplateSummaries.MemClear(0, templateCapacity);
                OrderedVisibleChunkStateMasks.MemClear(0, visibleChunkCapacity);
                OrderedVisibleChunkSourceIndices.MemClear(0, visibleChunkCapacity);
                OrderedVisibleChunkDrawPartitions.MemClear(0, visibleChunkCapacity);
                TemplateChunkWriteCursors.MemClear(0, templateCapacity);
                TemplateCommandCounts.MemClear(0, templateCapacity * DrawStateUtility.StateLodStride);
                PartitionStateSplitMasks.MemClear(0, visibleChunkCapacity * DrawStateUtility.StateKeyCount);
                Partitions.MemClear(0, visibleChunkCapacity);
                RangeCommandCounts.MemClear(0, rangeCapacity);
                RangeCommandOffsets.MemClear(0, rangeCapacity);
                LayoutCounts[0] = default;
                VisibleChunks.Clear();
            }

            public void EnsureCellCapacity(int cellCapacity)
            {
                EnsureArrayCapacity(ref CellVisibility, cellCapacity);
            }

            public void EnsureCapacity(int visibleChunkCapacity, int templateCapacity, int rangeCapacity)
            {
                EnsureListCapacity(ref VisibleChunks, visibleChunkCapacity);
                EnsureArrayCapacity(ref ChunkVisibility, visibleChunkCapacity);
                EnsureArrayCapacity(ref TemplateSummaries, templateCapacity);
                EnsureArrayCapacity(ref OrderedVisibleChunkStateMasks, visibleChunkCapacity);
                EnsureArrayCapacity(ref OrderedVisibleChunkSourceIndices, visibleChunkCapacity);
                EnsureArrayCapacity(ref OrderedVisibleChunkDrawPartitions, visibleChunkCapacity);
                EnsureArrayCapacity(ref OrderedVisibleChunks, visibleChunkCapacity);
                EnsureArrayCapacity(ref TemplateChunkWriteCursors, templateCapacity);
                EnsureArrayCapacity(ref TemplateCommandCounts, templateCapacity * DrawStateUtility.StateLodStride);
                EnsureArrayCapacity(ref PartitionStateSplitMasks, visibleChunkCapacity * DrawStateUtility.StateKeyCount);
                EnsureArrayCapacity(ref Partitions, visibleChunkCapacity);
                EnsureArrayCapacity(ref RangeCommandCounts, rangeCapacity);
                EnsureArrayCapacity(ref RangeCommandOffsets, rangeCapacity);
                EnsureArrayCapacity(ref LayoutCounts, 1);
            }

            public void Dispose()
            {
                if (!CellVisibility.IsCreated)
                    return;

                CellVisibility.Dispose();
                VisibleChunks.Dispose();
                ChunkVisibility.Dispose();
                TemplateSummaries.Dispose();
                OrderedVisibleChunkStateMasks.Dispose();
                OrderedVisibleChunkSourceIndices.Dispose();
                OrderedVisibleChunkDrawPartitions.Dispose();
                OrderedVisibleChunks.Dispose();
                TemplateChunkWriteCursors.Dispose();
                TemplateCommandCounts.Dispose();
                PartitionStateSplitMasks.Dispose();
                Partitions.Dispose();
                RangeCommandCounts.Dispose();
                RangeCommandOffsets.Dispose();
                LayoutCounts.Dispose();
            }

            private static void EnsureArrayCapacity<T>(ref NativeArray<T> array, int capacity) where T : unmanaged
            {
                int requiredCapacity = math.max(1, capacity);
                if (!array.IsCreated || array.Length < requiredCapacity)
                    array.ResizeArraySafe(requiredCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            }

            private static void EnsureListCapacity<T>(ref NativeList<T> list, int capacity) where T : unmanaged
            {
                int requiredCapacity = math.max(1, capacity);
                if (!list.IsCreated)
                    list = new NativeList<T>(requiredCapacity, Allocator.Persistent);
                else if (list.Capacity < requiredCapacity)
                    list.Capacity = requiredCapacity;
            }
        }

        private ViewCullingInputs BuildViewInputs(BatchCullingContext cc, IncludeExcludeListFilter includeExcludeListFilter)
        {
            ViewCullingInputs viewInputs = default;
            viewInputs.IncludeExcludeListFilter = includeExcludeListFilter;

            ReceiverPlanes receiverPlanes;
            ReceiverSphereCuller receiverSphereCuller;
            FrustumPlaneCuller frustumPlaneCuller;
            float screenRelativeMetric;
            float meshLodSelectionConstant;

            new SetupFrustumCullingInputs {
                LODBias = m_RenderingCameraSettings.LODBiasScale * QualitySettings.lodBias,
#if UNITY_6000_2_OR_NEWER
                MeshLodThreshold = QualitySettings.meshLodThreshold,
#endif
                Context = &cc,
                FrustumPlaneCuller = &frustumPlaneCuller,
                ReceiverSphereCuller = &receiverSphereCuller,
                ReceiverPlanes = &receiverPlanes,
                ScreenRelativeMetric = &screenRelativeMetric,
                MeshLodSelectionConstant = &meshLodSelectionConstant,
            }.Run();

            viewInputs.ReceiverPlanes = receiverPlanes;
            viewInputs.ReceiverSphereCuller = receiverSphereCuller;
            viewInputs.FrustumPlaneCuller = frustumPlaneCuller;
            viewInputs.ScreenRelativeMetric = screenRelativeMetric;
            viewInputs.MeshLodSelectionConstant = meshLodSelectionConstant;

            UpdateOcclusionSilhouettePlanes(cc.viewID.GetEntityIdCompat(), viewInputs.ReceiverPlanes.SilhouettePlaneSubArray());

            viewInputs.BinConfig = new DrawBinConfig
            {
                SplitCount = cc.cullingSplits.Length,
                SupportsCrossFade = cc.viewType is BatchCullingViewType.Camera or BatchCullingViewType.Light,
                SupportsMotionCheck = FloraSystem.Instance.AllowPerObjectMotionVectors && cc.viewType == BatchCullingViewType.Camera
            };

            ref readonly CullingGrid cullingGrid = ref m_CullingGrid.ValueRO;
            m_CullingScratch.PrepareForView(
                cullingGrid.CellAllocated.MaxLength,
                cullingGrid.ChunkAllocated.MaxLength,
                m_TemplateManager.ValueRO.MaxCount,
                m_DrawManager.ValueRO.DrawRangeCount);

            return viewInputs;
        }

        private bool RunGridCull(in BatchCullingContext cc, ref ViewCullingInputs viewInputs)
        {
            ref readonly CullingGrid cullingGrid = ref m_CullingGrid.ValueRO;
            GridCullCounts gridCullCounts = default;

            new CullGrid {
                FrustumPlanePackets = viewInputs.FrustumPlaneCuller.PlanePackets.AsArray(),
                FrustumSplitInfos = viewInputs.FrustumPlaneCuller.SplitInfos.AsArray(),
                LightFacingFrustumPlanes = viewInputs.ReceiverPlanes.LightFacingFrustumPlaneSubArray(),
                ReceiverSplitInfos = viewInputs.ReceiverSphereCuller.SplitInfos.AsArray(),
                WorldToLightSpaceRotation = viewInputs.ReceiverSphereCuller.WorldToLightSpaceRotation,
                OcclusionBuffer = cc.GetOcclusionBuffer(),
                Blocks = cullingGrid.BlockAllocated,
                BlockLocations = cullingGrid.BlockLocations,
                Cells = cullingGrid.CellAllocated,
                CellInstanceCount = cullingGrid.CellInstanceCount,
                CellChunks = cullingGrid.CellChunks,
                OutCullingChunks = m_CullingScratch.VisibleChunks,
                OutCellVisibility = m_CullingScratch.CellVisibility,
                OutCullingCounts = &gridCullCounts
            }.Run();

            return !gridCullCounts.IsEmpty;
        }

        private JobHandle RunChunkCull(in BatchCullingContext cc, in ViewCullingInputs viewInputs, out NativeArray<ulong> includedInstanceBits)
        {
            NativeArray<CullingChunkIndex> visibleChunks = m_CullingScratch.VisibleChunks.AsArray();

            JobHandle cullingHandle = new CullChunks {
                ViewType = cc.viewType,
                BinConfig = viewInputs.BinConfig,
                CullingLayerMask = cc.cullingLayerMask,
                CellVisibility = m_CullingScratch.CellVisibility,
                Chunks = visibleChunks,
                ChunkCounts = m_CullingGrid.ValueRO.ChunkCount,
                ChunkCells = m_CullingGrid.ValueRO.ChunkCells,
                ChunkFlags = m_CullingGrid.ValueRO.ChunkFlags,
                ChunkArchetypes = m_CullingGrid.ValueRO.ChunkArchetypes,
                ChunkVisibility = m_CullingScratch.ChunkVisibility,
#if UNITY_EDITOR
                SceneCullingMask = cc.sceneCullingMask,
                CullHiddenChunks = CanCullHiddenInstances_EditorOnly(cc),
#endif
            }.Schedule(visibleChunks.Length, CullChunks.BatchSize);

            includedInstanceBits = default;
#if UNITY_EDITOR
            if (viewInputs.IncludeExcludeListFilter.IsEnabled)
            {
                includedInstanceBits = new NativeArray<ulong>(visibleChunks.Length, Allocator.TempJob);
                cullingHandle = new GatherIncludeExcludeBitsJob {
                    IncludeExcludeListFilter = viewInputs.IncludeExcludeListFilter,
                    Chunks = visibleChunks,
                    ChunkCounts = m_CullingGrid.ValueRO.ChunkCount,
                    InstanceIndices = m_CullingGrid.ValueRO.ChunkInstanceIndices,
                    InstanceHandles = m_InstanceManager.ValueRO.InstanceHandles,
                    IncludedInstances = includedInstanceBits
                }.Schedule(visibleChunks.Length, 1, cullingHandle);
            }
#endif

            return cullingHandle;
        }

        private JobHandle PlanTemplateLayout(in BatchCullingContext cc, in ViewCullingInputs viewInputs, NativeBufferArray<DrawBatchIndex> templateDrawIndicesPerLod, JobHandle cullingHandle)
        {
            NativeArray<CullingChunkIndex> visibleChunks = m_CullingScratch.VisibleChunks.AsArray();

            JobHandle prepassHandle = new ReduceVisibleChunksByTemplate {
                VisibleChunks = visibleChunks,
                ChunkCounts = m_CullingGrid.ValueRO.ChunkCount,
                ChunkArchetypes = m_CullingGrid.ValueRO.ChunkArchetypes,
                ChunkVisibility = m_CullingScratch.ChunkVisibility,
                TemplateSummaries = m_CullingScratch.TemplateSummaries,
                OutputCounts = m_CullingScratch.LayoutCounts,
            }.Schedule(cullingHandle);

            prepassHandle = new ComputeTemplateChunkOffsets {
                Templates = m_TemplateManager.ValueRO.Allocated,
                TemplateSummaries = m_CullingScratch.TemplateSummaries,
            }.Schedule(prepassHandle);

            prepassHandle = new OrderVisibleChunksByTemplate {
                Templates = m_TemplateManager.ValueRO.Allocated,
                TemplateSummaries = m_CullingScratch.TemplateSummaries,
                VisibleChunks = visibleChunks,
                ChunkVisibility = m_CullingScratch.ChunkVisibility,
                ChunkArchetypes = m_CullingGrid.ValueRO.ChunkArchetypes,
                TemplateChunkWriteCursors = m_CullingScratch.TemplateChunkWriteCursors,
                OrderedVisibleChunks = m_CullingScratch.OrderedVisibleChunks,
                OrderedVisibleChunkSourceIndices = m_CullingScratch.OrderedVisibleChunkSourceIndices,
            }.Schedule(prepassHandle);

            JobHandle drawPartitionPrepassHandle = new ComputeChunkStateMasks {
                BinConfig = viewInputs.BinConfig,
                Counts = m_CullingScratch.LayoutCounts,
                OrderedVisibleChunks = m_CullingScratch.OrderedVisibleChunks,
                ChunkCounts = m_CullingGrid.ValueRO.ChunkCount,
                ChunkFlags = m_CullingGrid.ValueRO.ChunkFlags,
                ChunkArchetypes = m_CullingGrid.ValueRO.ChunkArchetypes,
                OrderedVisibleChunkStateMasks = m_CullingScratch.OrderedVisibleChunkStateMasks,
            }.Schedule(visibleChunks.Length, 64, prepassHandle);

            JobHandle layoutHandle = new BuildDrawPartitions {
                Templates = m_TemplateManager.ValueRO.Allocated,
                TemplateSummaries = m_CullingScratch.TemplateSummaries,
                OrderedVisibleChunks = m_CullingScratch.OrderedVisibleChunks,
                OrderedVisibleChunkStateMasks = m_CullingScratch.OrderedVisibleChunkStateMasks,
                ChunkArchetypes = m_CullingGrid.ValueRO.ChunkArchetypes,
                ChunkCounts = m_CullingGrid.ValueRO.ChunkCount,
                ChunkVisibility = m_CullingScratch.ChunkVisibility,
                OrderedVisibleChunkDrawPartitions = m_CullingScratch.OrderedVisibleChunkDrawPartitions,
                Partitions = m_CullingScratch.Partitions,
                PartitionStateSplitMasks = m_CullingScratch.PartitionStateSplitMasks,
            }.Schedule(drawPartitionPrepassHandle);

            layoutHandle = new PlanDrawLayout {
                BinConfig = viewInputs.BinConfig,
                Templates = m_TemplateManager.ValueRO.Allocated,
                DrawBatches = m_DrawManager.ValueRO.DrawBatches,
                BatchIDs = m_InstanceBuffer.ValueRO.DomainBatches,
                DrawBatchRangeIndices = m_DrawManager.ValueRO.DrawBatchRangeIndices,
                TemplateDrawIndicesPerLod = templateDrawIndicesPerLod,
                TemplateSummaries = m_CullingScratch.TemplateSummaries,
                Partitions = m_CullingScratch.Partitions,
                PartitionStateSplitMasks = m_CullingScratch.PartitionStateSplitMasks,
                TemplateCommandCounts = m_CullingScratch.TemplateCommandCounts,
                RangeCommandCounts = m_CullingScratch.RangeCommandCounts,
                RangeCommandOffsets = m_CullingScratch.RangeCommandOffsets,
                LayoutCounts = m_CullingScratch.LayoutCounts,
            }.Schedule(layoutHandle);

            return layoutHandle;
        }

        private void PopulatePlanBuffers(IndirectCullingRequest request, in CullingLayoutCounts layoutCounts)
        {
            int templateCount = m_TemplateManager.ValueRO.MaxCount;
            int visibleChunkCount = layoutCounts.VisibleChunkCount;
            int rangeCount = m_DrawManager.ValueRO.DrawRangeCount;
            int templateCommandCount = templateCount * DrawStateUtility.StateLodStride;
            int partitionStateSplitMaskCount = visibleChunkCount * DrawStateUtility.StateKeyCount;

            request.PlanBuffers.EnsureCapacity(templateCount, visibleChunkCount, rangeCount);
            CullingPlanBuffers planBuffers = request.PlanBuffers;

            if (templateCount > 0)
            {
                NativeArray<TemplateVisibilitySummary>.Copy(m_CullingScratch.TemplateSummaries, planBuffers.TemplateSummaries, templateCount);
                NativeArray<int>.Copy(m_CullingScratch.TemplateCommandCounts, planBuffers.TemplateCommandCounts, templateCommandCount);
            }

            if (visibleChunkCount > 0)
            {
                NativeArray<PartitionSummary>.Copy(m_CullingScratch.Partitions, planBuffers.Partitions, visibleChunkCount);
                NativeArray<byte>.Copy(m_CullingScratch.PartitionStateSplitMasks, planBuffers.PartitionStateSplitMasks, partitionStateSplitMaskCount);
                NativeArray<CullingChunkIndex>.Copy(m_CullingScratch.OrderedVisibleChunks, planBuffers.OrderedVisibleChunks, visibleChunkCount);
                NativeArray<byte>.Copy(m_CullingScratch.OrderedVisibleChunkStateMasks, planBuffers.OrderedVisibleChunkStateMasks, visibleChunkCount);
                NativeArray<int>.Copy(m_CullingScratch.OrderedVisibleChunkDrawPartitions, planBuffers.OrderedVisibleChunkDrawPartitions, visibleChunkCount);
                NativeArray<int>.Copy(m_CullingScratch.OrderedVisibleChunkSourceIndices, planBuffers.OrderedVisibleChunkSourceIndices, visibleChunkCount);

                for (int i = 0; i < visibleChunkCount; i++)
                    planBuffers.OrderedVisibleChunkSplitMasks[i] = m_CullingScratch.ChunkVisibility[planBuffers.OrderedVisibleChunks[i]].SplitMask;
            }

            if (rangeCount > 0)
            {
                NativeArray<int>.Copy(m_CullingScratch.RangeCommandCounts, planBuffers.RangeCommandCounts, rangeCount);
                NativeArray<int>.Copy(m_CullingScratch.RangeCommandOffsets, planBuffers.RangeCommandOffsets, rangeCount);
                NativeArray<int>.Copy(planBuffers.RangeCommandOffsets, planBuffers.RangeCommandWriteCursors, rangeCount);
            }
        }

        private IndirectCullingRequest AllocateRequestOutput(in BatchCullingContext cc, in ViewCullingInputs viewInputs, in CullingLayoutCounts layoutCounts)
        {
            IndirectCullingRequest request = AllocateCullingRequest(new IndirectCullingRequestParameters
            {
                FrameIndex = m_FrameIndex,
                Context = cc,
                BinConfig = viewInputs.BinConfig,
                ScreenRelativeMetric = viewInputs.ScreenRelativeMetric,
                MeshLodSelectionConstant = viewInputs.MeshLodSelectionConstant,
                FrustumPlaneCuller = viewInputs.FrustumPlaneCuller,
                DrawCommandCapacity = layoutCounts.DrawCommandCount,
                DrawInstanceCapacity = layoutCounts.VisibilityBufferCapacity,
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                EnableDebugGPUCullingStats = EnableGPUCullingStats,
                EnableDebugInstanceVisibility = EnableDebugInstanceVisibility,
#endif
#if UNITY_EDITOR
                IsSceneViewCamera = m_RenderingCameraIsSceneView,
#endif
            });

            request.CullingOutput[0] = AllocateIndirectCullingOutput(
                layoutCounts,
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                DebugDisplayFlora.Active && DebugDisplayFlora.Properties.EnableGPUChecks
#else
                false
#endif
            );

            return request;
        }

        private JobHandle WritePlannedOutput(
            BatchCullingOutput cullingOutput,
            IndirectCullingRequest request,
            NativeBufferArray<DrawBatchIndex> templateDrawIndicesPerLod,
            NativeArray<ulong> includedInstanceBits,
            int usedDrawRangeCount)
        {
            JobHandle cullingHandle = default;
            CullingPlanBuffers planBuffers = request.PlanBuffers;

#if UNITY_EDITOR
            if (includedInstanceBits.IsCreated)
            {
                request.IncludedInstances.Resize(planBuffers.LiveVisibleChunkCount, NativeArrayOptions.ClearMemory);
                cullingHandle = new ReorderIncludedInstanceBits {
                    OrderedVisibleChunkSourceIndices = planBuffers.LiveOrderedVisibleChunkSourceIndices,
                    SourceIncludedInstances = includedInstanceBits,
                    OutputIncludedInstances = request.IncludedInstances.AsArray(),
                }.Schedule(planBuffers.LiveVisibleChunkCount, 64, cullingHandle);
            }
#endif

            cullingHandle = new WriteCullingOutputPerTemplate {
                BinConfig = request.BinConfig,
                VisibilityBufferHandle = request.VisibilityBuffer.BufferHandle,
                DrawArgsBufferHandle = request.DrawArgsBuffer.BufferHandle,
                Templates = m_TemplateManager.ValueRO.Allocated,
                DrawBatches = m_DrawManager.ValueRO.DrawBatches,
                BatchIDs = m_InstanceBuffer.ValueRO.DomainBatches,
                ChunkArchetypes = m_CullingGrid.ValueRO.ChunkArchetypes,
                OrderedVisibleChunks = planBuffers.LiveOrderedVisibleChunks,
                OrderedVisibleChunkStateMasks = planBuffers.LiveOrderedVisibleChunkStateMasks,
                OrderedVisibleChunkSplitMasks = planBuffers.LiveOrderedVisibleChunkSplitMasks,
                OrderedVisibleChunkDrawPartitions = planBuffers.LiveOrderedVisibleChunkDrawPartitions,
                TemplateDrawIndicesPerLod = templateDrawIndicesPerLod,
                TemplateSummaries = planBuffers.LiveTemplateSummaries,
                Partitions = planBuffers.LivePartitions,
                PartitionStateSplitMasks = planBuffers.LivePartitionStateSplitMasks,
                TemplateCommandCounts = planBuffers.LiveTemplateCommandCounts,
                CullingViewOutput = request.CullingOutput
            }.Schedule(m_TemplateManager.ValueRO.MaxCount, 1, cullingHandle);

            cullingHandle = new BuildBatchCommands {
                UsedDrawRangeCount = usedDrawRangeCount,
                CullingViewOutput = request.CullingOutput,
                DrawBatchRangeIndices = m_DrawManager.ValueRO.DrawBatchRangeIndices,
                DrawRangeKeys = m_DrawManager.ValueRO.DrawRangeKeys,
                RangeCommandCounts = planBuffers.LiveRangeCommandCounts,
                RangeCommandOffsets = planBuffers.LiveRangeCommandOffsets,
                RangeCommandWriteCursors = planBuffers.LiveRangeCommandWriteCursors,
                BatchCullingOutput = cullingOutput.drawCommands,
            }.Schedule(cullingHandle);

            return cullingHandle;
        }

        private static void DisposeViewInputs(ref ViewCullingInputs viewInputs, NativeArray<ulong> includedInstanceBits)
        {
            if (includedInstanceBits.IsCreated)
                includedInstanceBits.Dispose();

            viewInputs.Dispose();
        }

        private static JobHandle ScheduleViewInputCleanup(JobHandle handle, ref ViewCullingInputs viewInputs, NativeArray<ulong> includedInstanceBits)
        {
            JobHandle disposeHandle = handle;
            if (includedInstanceBits.IsCreated)
                disposeHandle = includedInstanceBits.Dispose(handle);

            JobHandle viewInputDispose = viewInputs.Dispose(handle);
            return JobHandle.CombineDependencies(disposeHandle, viewInputDispose);
        }
    }
}
