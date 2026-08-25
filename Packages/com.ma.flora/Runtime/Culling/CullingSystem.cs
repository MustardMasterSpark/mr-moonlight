// Copyright © Magnetic Arcade. All Rights Reserved.

using System;
using System.Collections.Generic;
using System.Text;
using MA.InternalBridge;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace MA.Flora
{
    internal unsafe struct IndirectCullingOutput : IDisposable
    {
        public IndirectDrawChunk* DrawChunks;
        public int DrawChunkCount;

        public IndirectDrawPartition* DrawPartitions;
        public int DrawPartitionCount;

        public IndirectDrawBin* DrawBins;
        public int DrawBinCount;

        public IndirectDrawInfo* DrawInfos;
        public IndirectDrawCommandInfo* DrawCommandInfos;
        public int DrawCount;
        public int VisibilityBufferCapacity;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public int* DrawBinCapacities;
#endif

        public bool IsCreated => DrawPartitions != null;

        public void Dispose()
        {
            UnsafeUtility.Free(DrawChunks, Allocator.TempJob);
            UnsafeUtility.Free(DrawPartitions, Allocator.TempJob);
            UnsafeUtility.Free(DrawBins, Allocator.TempJob);
            UnsafeUtility.Free(DrawInfos, Allocator.TempJob);
            UnsafeUtility.Free(DrawCommandInfos, Allocator.TempJob);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            UnsafeUtility.Free(DrawBinCapacities, Allocator.TempJob);
#endif
        }
    }

    internal struct IndirectDrawCommandInfo
    {
        public DrawBatchIndex BatchIndex;
        public BatchDrawCommandIndirect Command;
    }

    internal struct AnimatedCrossFadeData
    {
        public EntityId ViewId;
        public int LastUpdateFrameIndex;

        public float3 AnimatedLODCameraPosition0;
        public float AnimatedLODCameraScreenRelativeMetric0;
        public float3 AnimatedLODCameraPosition1;
        public float AnimatedLODCameraScreenRelativeMetric1;

        private double m_AnimatedLODTime0;
        private double m_AnimatedLODTime1;
        private double m_AnimatedLODDuration;

        public void Reset(in LODParameters lodParameters, float screenRelativeMetric)
        {
            AnimatedLODCameraPosition0 = AnimatedLODCameraPosition1 = lodParameters.cameraPosition;
            AnimatedLODCameraScreenRelativeMetric0 = AnimatedLODCameraScreenRelativeMetric1 = screenRelativeMetric;
            m_AnimatedLODTime0 = m_AnimatedLODTime1 = Time.realtimeSinceStartupAsDouble;
            m_AnimatedLODDuration = 0.0;
        }

        public void Update(in LODParameters lodParameters, float screenRelativeMetric)
        {
            double now = Time.realtimeSinceStartupAsDouble;
            m_AnimatedLODDuration = LODGroup.crossFadeAnimationDuration;

            // Start a new fade if the previous one is finished by now
            if (m_AnimatedLODTime1 <= now)
            {
                if (m_AnimatedLODTime0 < m_AnimatedLODTime1)
                {
                    // Move "old" forward
                    AnimatedLODCameraPosition0 = AnimatedLODCameraPosition1;
                    AnimatedLODCameraScreenRelativeMetric0 = AnimatedLODCameraScreenRelativeMetric1;
                    m_AnimatedLODTime0 = m_AnimatedLODTime1;
                }

                // New target is current camera
                AnimatedLODCameraPosition1 = lodParameters.cameraPosition;
                AnimatedLODCameraScreenRelativeMetric1 = screenRelativeMetric;

                m_AnimatedLODTime0 = now;                         // fade start
                m_AnimatedLODTime1 = now + m_AnimatedLODDuration; // fade end
            }
        }

        public float ComputeAlpha()
        {
            double now = Time.realtimeSinceStartupAsDouble;
            if (m_AnimatedLODDuration <= 0.0)
                return 1f;
            else if (now <= m_AnimatedLODTime0)
                return 0f; // Before the start of the fade
            else if (now >= m_AnimatedLODTime1)
                return 1f; // After the end of the fade
            else
                return (float)Math.Clamp((now - m_AnimatedLODTime0) / (m_AnimatedLODTime1 - m_AnimatedLODTime0), 0.0, 1.0);
        }
    }

    internal struct CullingSystemSetup
    {
        public FloraRenderPipeline RenderPipeline;
        public FloraRuntimeResources RuntimeResources;
    }

    internal struct CPUCullingStats
    {
        public EntityId ViewId;
        public BatchCullingViewType ViewType;
        public int FrameIndex;
        public int VisibleChunkCount;
        public int VisibleInstanceCount;
        public int DrawInstanceCount;
        public int DrawCommandCount;
    }

    internal struct GPUCullingStats
    {
        public EntityId ViewId;
        public BatchCullingViewType ViewType;
        public int FrameIndex;
        public int VisibleDraws;
        public int VisibleInstances;
        public int OccludedInstances;
    }

    [Flags]
    internal enum CullingSystemDebugFlags
    {
        None = 0,
        CPUCullingStats = 1 << 0,
        GPUCullingStats = 1 << 1,
    }

    internal sealed unsafe partial class CullingSystem : IDisposable
    {
        private NativeDataReference<InstanceManager> m_InstanceManager;
        private NativeDataReference<CullingGrid> m_CullingGrid;
        private NativeDataReference<DrawManager> m_DrawManager;
        private NativeDataReference<TemplateManager> m_TemplateManager;
        private NativeDataReference<InstanceBuffer> m_InstanceBuffer;
        private NativeDataReference<StreamingSphereManager> m_StreamingSphereManager;

        private float m_OriginalCrossFadeDuration;
        private NativeHashMap<EntityId, int> m_AnimatedCrossFadeViewMap;
        private NativeHashMap<int, int> m_AnimatedCrossFadeLodHashMap;
        private UnsafeList<AnimatedCrossFadeData> m_AnimatedCrossFadeDatas;

        private FloraRenderPipeline m_RenderPipeline;
        private SphericalHarmonicsL2 m_CachedAmbientProbe;

        private int m_FrameIndex;
        private int m_NextCullingViewRequestID;
        private IndirectCullingRequest[] m_CullingViewRequestPool;
        private Queue<IndirectCullingRequest> m_QueuedCullingRequests;
        private List<IndirectCullingRequest> m_ContextCullingRequests;

        private FloraAdditionalCameraSettings m_RenderingCameraSettings;
        private bool m_RenderingCameraIsSceneView;
        private bool m_RenderingCameraWantsGPUOcclusionCulling;

        private OcclusionCuller m_OcclusionCuller;
        private IndirectCullingPass m_IndirectCullingPass;
        private CullingScratch m_CullingScratch;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private CullingSystemDebugFlags m_DebugFlags = CullingSystemDebugFlags.None;
        private Dictionary<EntityId, CPUCullingStats> m_CPUCullingStats = new Dictionary<EntityId, CPUCullingStats>();
        private Dictionary<EntityId, GPUCullingStats> m_GPUCullingStats = new Dictionary<EntityId, GPUCullingStats>();

        public bool EnableCPUCullingStats
        {
            get => (m_DebugFlags & CullingSystemDebugFlags.CPUCullingStats) != 0 ||
                   (DebugDisplayFlora.Active && DebugDisplayFlora.Properties.EnableCPUCullingStats);
            set
            {
                if (value)
                    m_DebugFlags |= CullingSystemDebugFlags.CPUCullingStats;
                else
                    m_DebugFlags &= ~CullingSystemDebugFlags.CPUCullingStats;
            }
        }

        public bool EnableGPUCullingStats
        {
            get => (m_DebugFlags & CullingSystemDebugFlags.GPUCullingStats) != 0 ||
                   (DebugDisplayFlora.Active && DebugDisplayFlora.Properties.EnableGPUCullingStats);
            set
            {
                if (value)
                    m_DebugFlags |= CullingSystemDebugFlags.GPUCullingStats;
                else
                    m_DebugFlags &= ~CullingSystemDebugFlags.GPUCullingStats;
            }
        }

        public bool EnableDebugInstanceVisibility
            => DebugDisplayFlora.Active && DebugDisplayFlora.Properties.InstanceDrawMode != DebugInstanceDrawMode.None;
#endif

        public CullingSystem(CullingSystemSetup cullingSystemSetup, BatchRendererGroup batchRendererGroup, InstanceContext instanceContext)
        {
            m_InstanceManager = instanceContext.InstanceManager;
            m_CullingGrid = instanceContext.CullingGrid;
            m_DrawManager = instanceContext.DrawManager;
            m_TemplateManager = instanceContext.TemplateManager;
            m_InstanceBuffer = instanceContext.InstanceBuffer;
            m_StreamingSphereManager = instanceContext.StreamingManager;

            batchRendererGroup.SetEnabledViewTypes(new[] {
                BatchCullingViewType.Light,
                BatchCullingViewType.Camera,
                BatchCullingViewType.Picking,
                BatchCullingViewType.SelectionOutline,
                // BatchCullingViewType.Filtering,
            });

            m_IndirectCullingPass = new IndirectCullingPass(cullingSystemSetup.RuntimeResources);
            m_OcclusionCuller = new OcclusionCuller(cullingSystemSetup.RuntimeResources);

            m_RenderPipeline = cullingSystemSetup.RenderPipeline;
            m_CachedAmbientProbe = default;

            m_FrameIndex = 0;
            m_NextCullingViewRequestID = 0;
            m_CullingViewRequestPool = new IndirectCullingRequest[8];
            m_QueuedCullingRequests = new Queue<IndirectCullingRequest>(8);
            m_ContextCullingRequests = new List<IndirectCullingRequest>(8);

            m_OriginalCrossFadeDuration = LODGroup.crossFadeAnimationDuration;
            m_AnimatedCrossFadeViewMap = new NativeHashMap<EntityId, int>(8, Allocator.Persistent);
            m_AnimatedCrossFadeLodHashMap = new NativeHashMap<int, int>(8, Allocator.Persistent);
            m_AnimatedCrossFadeDatas = new UnsafeList<AnimatedCrossFadeData>(8, Allocator.Persistent);

            Camera[] activeCameras = Camera.allCameras;
            for (int i = 0; i < activeCameras.Length; i++)
                m_StreamingSphereManager.ValueRW.UpdateCamera(activeCameras[i]);
        }

        public void Dispose()
        {
            LODGroup.crossFadeAnimationDuration = m_OriginalCrossFadeDuration;

            for (int i = 0; i < m_CullingViewRequestPool.Length; i++)
            {
                IndirectCullingRequest request = m_CullingViewRequestPool[i];
                if (request != null)
                {
                    request.CullingHandle.Complete();
                    request.Dispose();
                    m_CullingViewRequestPool[i] = null;
                }
            }

            m_AnimatedCrossFadeViewMap.Dispose();
            m_AnimatedCrossFadeLodHashMap.Dispose();
            m_AnimatedCrossFadeDatas.Dispose();
            m_OcclusionCuller.Dispose();
            m_CullingScratch.Dispose();
        }

        #region RenderPipeline Events

        public void UpdateAmbientLighting(bool forceUpdate = false)
        {
            SphericalHarmonicsL2 ambientProbe = RenderSettings.ambientProbe;
            if (forceUpdate || !m_CachedAmbientProbe.Equals(ambientProbe))
            {
                m_CachedAmbientProbe = ambientProbe;
                m_InstanceManager.ValueRW.ForceLightProbeUpdate = true;
            }
        }

        public void BeginContextRendering()
        {
            Profiler.BeginSample("Flora.BeginContextRendering");
            {
                m_OcclusionCuller.NextFrame();
                m_FrameIndex++;
                m_NextCullingViewRequestID = 0;
                m_AnimatedCrossFadeLodHashMap.Clear();
                CleanupStaleCullingRequests();
                CleanupStaleAnimatedCrossFadeData();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                UpdateCullingStats();
#endif
            }
            Profiler.EndSample();
        }

        public void BeginCameraRendering(Camera camera)
        {
            m_RenderingCameraSettings = camera.GetComponent<FloraAdditionalCameraSettings>();
            if (m_RenderingCameraSettings == null)
                m_RenderingCameraSettings = ComponentSingleton<FloraAdditionalCameraSettings>.instance;

            m_RenderingCameraWantsGPUOcclusionCulling = m_RenderingCameraSettings.AllowGPUOcclusionCulling;
            if (FloraSystem.Instance != null && !FloraSystem.Instance.AllowGPUOcclusionCulling)
                m_RenderingCameraWantsGPUOcclusionCulling = false;
            if (FloraSceneSettings.Instance && !FloraSceneSettings.Instance.AllowGPUOcclusionCulling)
                m_RenderingCameraWantsGPUOcclusionCulling = false;

            m_RenderingCameraIsSceneView = camera.cameraType == CameraType.SceneView;
            m_RenderPipeline.EnqueueCameraPasses(camera, new FloraRenderPipelineCameraSettings
            {
                UseGPUOcclusionCulling = m_RenderingCameraWantsGPUOcclusionCulling
            });

            if (camera.cameraType is not CameraType.Preview)
                m_StreamingSphereManager.ValueRW.UpdateCamera(camera);
        }

        public void EndCameraRendering(Camera camera)
        {
            m_RenderingCameraIsSceneView = false;
        }

        public void EndContextRendering()
        {
            for (int i = 0; i < m_ContextCullingRequests.Count; i++)
                m_ContextCullingRequests[i].Release();

            m_ContextCullingRequests.Clear();
        }

        #endregion

        #region BatchRendererGroup Events

        private static readonly ProfilerMarker CameraPerformBatchCullingMarker           = new ProfilerMarker("Flora.PerformBatchCulling.Camera");
        private static readonly ProfilerMarker LightPerformBatchCullingMarker            = new ProfilerMarker("Flora.PerformBatchCulling.Light");
        private static readonly ProfilerMarker PickingPerformBatchCullingMarker          = new ProfilerMarker("Flora.PerformBatchCulling.Picking");
        private static readonly ProfilerMarker SelectionOutlinePerformBatchCullingMarker = new ProfilerMarker("Flora.PerformBatchCulling.SelectionOutline");
        private static readonly ProfilerMarker FilteringPerformBatchCullingMarker        = new ProfilerMarker("Flora.PerformBatchCulling.Filtering");
        private static readonly ProfilerMarker UnknownPerformBatchCullingMarker          = new ProfilerMarker("Flora.PerformBatchCulling.Unknown");

        private static ProfilerMarker GetPerformBatchCullingProfilerMarker(BatchCullingViewType viewType)
        {
            return viewType switch
            {
                BatchCullingViewType.Camera           => CameraPerformBatchCullingMarker,
                BatchCullingViewType.Light            => LightPerformBatchCullingMarker,
                BatchCullingViewType.Picking          => PickingPerformBatchCullingMarker,
                BatchCullingViewType.SelectionOutline => SelectionOutlinePerformBatchCullingMarker,
                BatchCullingViewType.Filtering        => FilteringPerformBatchCullingMarker,
                _                                     => UnknownPerformBatchCullingMarker
            };
        }

        public JobHandle OnPerformBatchCulling(BatchRendererGroup rendererGroup, BatchCullingContext cc, BatchCullingOutput cullingOutput, IntPtr userContext)
        {
            using ProfilerMarker.AutoScope _ = GetPerformBatchCullingProfilerMarker(cc.viewType).Auto();
            if (m_RenderingCameraSettings == null || m_RenderingCameraSettings.DisableInstanceRendering)
                return default;

            IncludeExcludeListFilter includeExcludeListFilter = GetPickingIncludeExcludeListFilterForCurrentCullingCallback(cc);
            if (includeExcludeListFilter is { IsIncludeEnabled: true, IsIncludeEmpty: true })
            {
                includeExcludeListFilter.Dispose();
                return default;
            }

            ViewCullingInputs viewInputs = BuildViewInputs(cc, includeExcludeListFilter);
            if (!RunGridCull(cc, ref viewInputs))
            {
                DisposeViewInputs(ref viewInputs, default);
                return default;
            }

            NativeBufferArray<DrawBatchIndex> templateDrawIndicesPerLod =
                cc.viewType == BatchCullingViewType.Light
                    ? m_TemplateManager.ValueRO.ShadowDrawIndicesPerLod
                    : m_TemplateManager.ValueRO.CameraDrawIndicesPerLod;

            JobHandle cullingHandle = RunChunkCull(cc, viewInputs, out NativeArray<ulong> includedInstanceBits);
            JobHandle layoutHandle = PlanTemplateLayout(cc, viewInputs, templateDrawIndicesPerLod, cullingHandle);
            layoutHandle.Complete();

            CullingLayoutCounts layoutCounts = m_CullingScratch.LayoutCounts[0];

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            bool enableLayoutValidation = DebugDisplayFlora.Active && DebugDisplayFlora.Properties.EnableGPUChecks;
            if (enableLayoutValidation)
            {
                ValidatePartitionedLayout(
                    cc.viewID,
                    cc.viewType,
                    viewInputs.BinConfig,
                    m_TemplateManager.ValueRO.Allocated,
                    m_CullingScratch.TemplateSummaries,
                    m_CullingScratch.OrderedVisibleChunkDrawPartitions,
                    m_CullingScratch.OrderedVisibleChunkStateMasks,
                    m_CullingScratch.Partitions,
                    m_CullingScratch.PartitionStateSplitMasks,
                    m_CullingScratch.TemplateCommandCounts,
                    layoutCounts);
            }
#endif

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (EnableCPUCullingStats)
            {
                m_CPUCullingStats[cc.viewID.GetEntityIdCompat()] = new CPUCullingStats
                {
                    ViewId = cc.viewID.GetEntityIdCompat(),
                    ViewType = cc.viewType,
                    FrameIndex = m_FrameIndex,
                    VisibleChunkCount = layoutCounts.VisibleChunkCount,
                    VisibleInstanceCount = layoutCounts.VisibleInstanceCount,
                    DrawInstanceCount = layoutCounts.VisibilityBufferCapacity,
                    DrawCommandCount = layoutCounts.DrawCommandCount,
                };
            }
#endif

            if (layoutCounts.VisibleChunkCount == 0 || layoutCounts.DrawCommandCount == 0)
            {
                DisposeViewInputs(ref viewInputs, includedInstanceBits);
                return default;
            }

            IndirectCullingRequest request = AllocateRequestOutput(cc, viewInputs, layoutCounts);
            PopulatePlanBuffers(request, layoutCounts);
            cullingHandle = WritePlannedOutput(
                cullingOutput,
                request,
                templateDrawIndicesPerLod,
                includedInstanceBits,
                layoutCounts.UsedDrawRangeCount);

            cullingHandle = ScheduleViewInputCleanup(cullingHandle, ref viewInputs, includedInstanceBits);

            return request.Schedule(ref cullingOutput, cullingHandle);
        }

        public void OnBatchCullingComplete(int cullingRequestID)
        {
            if (cullingRequestID < 0 || cullingRequestID >= m_CullingViewRequestPool.Length)
                return;

            IndirectCullingRequest indirectCullingRequest = m_CullingViewRequestPool[cullingRequestID];
            if (indirectCullingRequest == null || !indirectCullingRequest.IsValid)
                return;

            if (indirectCullingRequest.ViewType
                is BatchCullingViewType.Picking
                or BatchCullingViewType.SelectionOutline
                or BatchCullingViewType.Filtering)
            {
                // Immediate dispatch for editor views
                CommandBuffer cmd = CommandBufferPool.Get();
                DispatchCullingRequest(cmd, VolumeManager.instance.stack, indirectCullingRequest, default, default, default);
                Graphics.ExecuteCommandBuffer(cmd);
                CommandBufferPool.Release(cmd);
                indirectCullingRequest.State = IndirectCullingRequestState.Completed;
            }
        }

        #endregion

        #region Indirect Request Dispatch

        private class DispatchIndirectCullingPassData
        {
            public CullingSystem CullingSystem;
            public IndirectCullingRequest Request;
            public VolumeStack VolumeStack;
            public IndirectCullingRequestHandles DrawHandles;
            public OcclusionCullingSettings Settings;
            public OccluderHandles OccluderHandles;
            public InstanceOcclusionTestSubviewSettings OcclusionTestSubviewSettings;
        }

        public void DispatchQueuedCullingRequests(RenderGraph renderGraph, VolumeStack volumeStack,
            in OcclusionCullingSettings settings = default, Span<SubviewOcclusionTest> occlusionSubviews = default)
        {
            while (m_QueuedCullingRequests.Count > 0)
            {
                IndirectCullingRequest request = m_QueuedCullingRequests.Dequeue();
                if (!request.IsValid || request.State != IndirectCullingRequestState.Scheduled)
                    continue;

                DispatchCullingRequest(renderGraph, volumeStack, request, settings, occlusionSubviews);
            }
        }

        public void DispatchCullingRequest(RenderGraph renderGraph, VolumeStack volumeStack, IndirectCullingRequest request,
            in OcclusionCullingSettings settings, Span<SubviewOcclusionTest> occlusionSubviews)
        {
            if (!request.IsValid)
                return;

            using (IComputeRenderGraphBuilder builder = renderGraph.AddComputePass("Flora.DispatchIndirectCulling", out DispatchIndirectCullingPassData passData))
            {
                builder.AllowGlobalStateModification(true);

                passData.CullingSystem = this;
                passData.Request = request;
                passData.VolumeStack = volumeStack;
                passData.DrawHandles = request.ImportBuffers(renderGraph);
                passData.DrawHandles.UseWith(builder);
                passData.Settings = settings;

                if (m_OcclusionCuller.TryGetContext(request.ViewID.GetEntityIdCompat(), out OcclusionContext occluderCtx))
                {
                    passData.OccluderHandles = occluderCtx.Import(renderGraph);

                    if (passData.OccluderHandles.IsValid())
                    {
                        passData.OcclusionTestSubviewSettings = InstanceOcclusionTestSubviewSettings.FromSpan(occlusionSubviews);
                        passData.OccluderHandles.UseForOcclusionTest(builder);
                    }
                }

                builder.SetRenderFunc((DispatchIndirectCullingPassData data, ComputeGraphContext context) =>
                {
                    CommandBuffer cmd = context.cmd.GetWrappedCommandBuffer();
                    data.CullingSystem.DispatchCullingRequest(
                        cmd, data.VolumeStack, data.Request, data.Settings, data.OcclusionTestSubviewSettings, data.OccluderHandles);
                });
            }
        }

        public void DispatchQueuedCullingRequests(CommandBuffer cmd, VolumeStack volumeStack,
            in OcclusionCullingSettings indirectCullingSettings = default, Span<SubviewOcclusionTest> occlusionSubviews = default)
        {
            InstanceOcclusionTestSubviewSettings occlusionSubviewSettings = InstanceOcclusionTestSubviewSettings.FromSpan(occlusionSubviews);

            while (m_QueuedCullingRequests.Count > 0)
            {
                IndirectCullingRequest request = m_QueuedCullingRequests.Dequeue();
                if (!request.IsValid || request.State != IndirectCullingRequestState.Scheduled)
                    continue;

                DispatchCullingRequest(cmd, volumeStack, request, indirectCullingSettings, occlusionSubviewSettings, default);
            }
        }

        private void DispatchCullingRequest(CommandBuffer cmd, VolumeStack volumeStack, IndirectCullingRequest request,
            in OcclusionCullingSettings settings, in InstanceOcclusionTestSubviewSettings occlusionTestSubviewSettings, OccluderHandles occluderHandles)
        {
            AnimatedCrossFadeData animatedCrossFadeData = ResolveAnimatedCrossFadeData(request);
            if (!request.CompleteAndUpdate(cmd, volumeStack, animatedCrossFadeData, out IndirectCullingOutput cullingViewOutput))
                return;

            if (m_RenderingCameraWantsGPUOcclusionCulling && request.ViewType is BatchCullingViewType.Camera)
                occluderHandles = PrepareOcclusionForCullingDispatch(cmd, request.ViewID.GetEntityIdCompat(), settings, occlusionTestSubviewSettings, occluderHandles);

            IndirectCullingParams indirectCullingParams = new IndirectCullingParams
            {
                MaxChunkCount = cullingViewOutput.DrawChunkCount,
                DrawBinCount = cullingViewOutput.DrawBinCount,
                BinConfig = request.BinConfig,
                DrawBinBuffer = request.DrawBinBuffer,
                DrawPartitionBuffer = request.DrawPartitionBuffer,
                DrawChunkBuffer = request.DrawChunkBuffer,
                ArchetypeDataBuffer = m_InstanceManager.ValueRO.ArchetypeDataBuffer,
                BlockDataBuffer = m_CullingGrid.ValueRO.BlockDataBuffer,
                CullingChunkBuffer = m_CullingGrid.ValueRO.ChunkBatchBuffer,
                CullingChunkBatchDomainBuffer = m_CullingGrid.ValueRO.ChunkInfoBuffer,
                CullingChunkAttributeBuffer = m_CullingGrid.ValueRO.ChunkAttributeBuffer,
                CullingChunkCellBuffer = m_CullingGrid.ValueRO.ChunkCellBuffer,
                CullingChunkFlagBuffer = m_CullingGrid.ValueRO.ChunkFlagBuffer,
                CullingChunkIndirectOffsetBuffer = m_CullingGrid.ValueRO.IndirectOffsetBuffer,
                CullingWorkGroupArgsBuffer = request.WorkGroupArgsBuffer,
                CullingWorkGroupDataBuffer = request.WorkGroupDataBuffer,
                TemplateDataBuffer = m_TemplateManager.ValueRO.TemplateDataBuffer,
                InstanceBuffer = m_InstanceBuffer.ValueRO.DataBuffer,
                BatchDomainAddressBuffer = m_InstanceBuffer.ValueRO.DomainCullingAddresses,
                InstanceCountMultiplier = settings.instanceMultiplier,
                IndirectArgsBuffer = request.DrawArgsBuffer,
                DrawInfoBuffer = request.DrawInfoBuffer,
                DrawArgsCount = cullingViewOutput.DrawCount,
                VisibleInstancesBuffer = request.VisibilityBuffer,
                ViewType = request.ViewType,
                ViewShaderVariables = request.ShaderVariables,
                OccluderHandles = occluderHandles,
#if UNITY_EDITOR
                IsSceneViewCamera = request.IsSceneViewCamera,
                IncludedChunkCount = request.HasIncludedInstances ? request.IncludedChunkCount : 0,
                IncludedInstancesBuffer = request.IncludedInstancesBuffer,
#endif
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                EnableDebug = DebugDisplayFlora.NeedsCullingDebug || EnableGPUCullingStats,
                DebugDispatchCounterBuffer = request.DebugDispatchCounterBuffer,
                DebugInstanceDrawBuffer = request.DebugInstanceVisibilityBuffer,
                DebugErrorCapacity = IndirectCullingRequest.DebugMaxErrorRecords,
                DebugErrorBuffer = request.DebugErrorBuffer,
                DebugErrorCountBuffer = request.DebugErrorCountBuffer,
#endif
            };

            m_IndirectCullingPass.Dispatch(cmd, indirectCullingParams);

            request.OnPostDispatchCulling(cmd);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (request.HasDebugDispatchCounters())
            {
                request.RequestDebugDispatchCounters(cmd, indirectStats =>
                {
                    m_GPUCullingStats[indirectStats.ViewId] = indirectStats;
                });
            }
#endif
        }

        #endregion

        #region Culling Helpers

        private static IndirectCullingOutput AllocateIndirectCullingOutput(in CullingLayoutCounts counts, bool allocateDebugBinCapacities)
        {
            IndirectCullingOutput output = default;
            output.VisibilityBufferCapacity = counts.VisibilityBufferCapacity;
            if (counts.DrawPartitionCount > 0)
            {
                output.DrawPartitionCount = counts.DrawPartitionCount;
                output.DrawPartitions = MemoryUtility.Allocate<IndirectDrawPartition>(counts.DrawPartitionCount, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            }

            if (counts.VisibleChunkCount > 0)
            {
                output.DrawChunkCount = counts.VisibleChunkCount;
                output.DrawChunks = MemoryUtility.Allocate<IndirectDrawChunk>(counts.VisibleChunkCount, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            }

            if (counts.DrawBinCount > 0)
            {
                output.DrawBinCount = counts.DrawBinCount;
                output.DrawBins = MemoryUtility.Allocate<IndirectDrawBin>(counts.DrawBinCount, Allocator.TempJob, NativeArrayOptions.ClearMemory);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if (allocateDebugBinCapacities)
                    output.DrawBinCapacities = MemoryUtility.Allocate<int>(counts.DrawBinCount, Allocator.TempJob, NativeArrayOptions.ClearMemory);
#endif
            }

            if (counts.DrawCommandCount > 0)
            {
                output.DrawCount = counts.DrawCommandCount;
                output.DrawInfos = MemoryUtility.Allocate<IndirectDrawInfo>(counts.DrawCommandCount, Allocator.TempJob, NativeArrayOptions.ClearMemory);
                output.DrawCommandInfos = MemoryUtility.Allocate<IndirectDrawCommandInfo>(counts.DrawCommandCount, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            }

            return output;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private static void ValidatePartitionedLayout(
            BatchPackedCullingViewID viewId,
            BatchCullingViewType viewType,
            DrawBinConfig binConfig,
            NativeBitSet templates,
            NativeArray<TemplateVisibilitySummary> templateSummaries,
            NativeArray<int> orderedVisibleChunkDrawPartitions,
            NativeArray<byte> orderedVisibleChunkStateMasks,
            NativeArray<PartitionSummary> partitions,
            NativeArray<byte> partitionStateSplitMasks,
            NativeArray<int> templateCommandCounts,
            in CullingLayoutCounts layoutCounts)
        {
            int computedDrawPartitionCount = 0;
            int computedVisibilityCapacity = 0;
            int computedDrawCount = 0;
            int computedDrawBinCount = 0;

            foreach (TemplateIndex template in templates.AsType<TemplateIndex>())
            {
                TemplateVisibilitySummary templateSummary = templateSummaries[template];
                int orderedChunkCount = templateSummary.VisibleChunkCount;
                if (orderedChunkCount == 0)
                    continue;

                int orderedChunkOffset = templateSummary.OrderedChunkOffset;
                int partitionSummaryOffset = templateSummary.PartitionSummaryOffset;
                int partitionCount = templateSummary.DrawPartitionCount;
                if (templateSummary.VisibleInstanceCount > 0 && partitionCount == 0)
                {
                    LogLayoutValidationError(viewId, viewType, $"Template {template.Index} has visible chunks but no draw partitions.");
                    continue;
                }

                if (templateSummary.DrawPartitionOffset != computedDrawPartitionCount)
                {
                    LogLayoutValidationError(
                        viewId,
                        viewType,
                        $"Template {template.Index} draw-partition offset mismatch. Expected {computedDrawPartitionCount}, got {templateSummary.DrawPartitionOffset}.");
                }

                int sumPartitionInstanceCount = 0;
                bool[] partitionSeen = partitionCount > 0 ? new bool[partitionCount] : null;
                for (int partition = 0; partition < partitionCount; partition++)
                {
                    int partitionStorageIndex = partitionSummaryOffset + partition;
                    int partitionInstanceCount = partitions[partitionStorageIndex].VisibleInstanceCount;
                    if (partitionInstanceCount <= 0)
                    {
                        LogLayoutValidationError(viewId, viewType, $"Template {template.Index} contains an empty draw partition at index {partition}.");
                        continue;
                    }

                    sumPartitionInstanceCount += partitionInstanceCount;
                }

                if (sumPartitionInstanceCount != templateSummary.VisibleInstanceCount)
                {
                    LogLayoutValidationError(
                        viewId,
                        viewType,
                        $"Template {template.Index} partition instance total mismatch. Expected {templateSummary.VisibleInstanceCount}, got {sumPartitionInstanceCount}.");
                }

                for (int chunkIndex = 0; chunkIndex < orderedChunkCount; chunkIndex++)
                {
                    int orderedChunkIndex = orderedChunkOffset + chunkIndex;
                    int partitionIndex = orderedVisibleChunkDrawPartitions[orderedChunkIndex];
                    if ((uint)partitionIndex >= (uint)partitionCount)
                    {
                        LogLayoutValidationError(
                            viewId,
                            viewType,
                            $"Template {template.Index} produced an out-of-range partition index {partitionIndex} for ordered chunk {chunkIndex}.");
                        continue;
                    }

                    if (orderedVisibleChunkStateMasks[orderedChunkIndex] == 0)
                    {
                        LogLayoutValidationError(
                            viewId,
                            viewType,
                            $"Template {template.Index} produced an empty exact state mask for ordered chunk {chunkIndex} in partition {partitionIndex}.");
                    }

                    partitionSeen[partitionIndex] = true;
                }

                for (int partition = 0; partition < partitionCount; partition++)
                {
                    if (!partitionSeen[partition])
                    {
                        LogLayoutValidationError(
                            viewId,
                            viewType,
                            $"Template {template.Index} reserved draw partition {partition} without any visible chunk.");
                    }
                }

                int lodCount = template.LodCount;
                for (int partition = 0; partition < partitionCount; partition++)
                {
                    int partitionStorageIndex = partitionSummaryOffset + partition;
                    PartitionSummary partitionSummary = partitions[partitionStorageIndex];
                    int partitionInstanceCount = partitionSummary.VisibleInstanceCount;
                    uint partitionStateMask = partitionSummary.StateMask;
                    int slotsPerLod = math.countbits(partitionStateMask);

                    if (partitionSummary.BinOffset != computedDrawBinCount)
                    {
                        LogLayoutValidationError(
                            viewId,
                            viewType,
                            $"Template {template.Index} partition {partition} bin offset mismatch. Expected {computedDrawBinCount}, got {partitionSummary.BinOffset}. " +
                            $"StateMask: 0x{partitionStateMask:X2}, SplitCount: {binConfig.SplitCount}, Partitions: {FormatPartitionInfo(partitions, partitionSummaryOffset, partitionCount)}");
                    }

                    if (partitionSummary.CommandOffset != computedDrawCount)
                    {
                        LogLayoutValidationError(
                            viewId,
                            viewType,
                            $"Template {template.Index} partition {partition} command offset mismatch. Expected {computedDrawCount}, got {partitionSummary.CommandOffset}. " +
                            $"StateMask: 0x{partitionStateMask:X2}, SplitCount: {binConfig.SplitCount}, Partitions: {FormatPartitionInfo(partitions, partitionSummaryOffset, partitionCount)}");
                    }

                    if (partitionSummary.VisibleInstanceOffset != computedVisibilityCapacity)
                    {
                        LogLayoutValidationError(
                            viewId,
                            viewType,
                            $"Template {template.Index} partition {partition} visible-instance offset mismatch. Expected {computedVisibilityCapacity}, got {partitionSummary.VisibleInstanceOffset}. " +
                            $"StateMask: 0x{partitionStateMask:X2}, SplitCount: {binConfig.SplitCount}, Partitions: {FormatPartitionInfo(partitions, partitionSummaryOffset, partitionCount)}");
                    }

                    if (partitionStateMask != 0 && partitionInstanceCount > 0)
                    {
                        computedDrawBinCount += DrawStateUtility.ComputePartitionBinStride(binConfig.SplitCount, slotsPerLod, lodCount);

                        int partitionStateOffset = DrawStateUtility.ComputePartitionStateOffset(partitionSummaryOffset, partition);
                        uint iterationMask = partitionStateMask;
                        while (iterationMask != 0)
                        {
                            int stateKey = math.tzcnt(iterationMask);
                            iterationMask &= iterationMask - 1;

                            int visibleSplitCount = math.countbits((uint)partitionStateSplitMasks[partitionStateOffset + stateKey]);
                            if (visibleSplitCount == 0)
                                continue;

                            for (int lod = 0; lod < lodCount; lod++)
                            {
                                int commandCount = templateCommandCounts[DrawStateUtility.ComputeTemplateLodStateIndex(template, lod, stateKey)];
                                if (commandCount == 0)
                                    continue;

                                computedVisibilityCapacity += partitionInstanceCount * visibleSplitCount;
                                computedDrawCount += commandCount * visibleSplitCount;
                            }
                        }
                    }

                    computedDrawPartitionCount += 1;
                }
            }

            if (computedDrawPartitionCount != layoutCounts.DrawPartitionCount)
            {
                LogLayoutValidationError(
                    viewId,
                    viewType,
                    $"Draw partition count mismatch. Expected {layoutCounts.DrawPartitionCount}, computed {computedDrawPartitionCount}.");
            }

            if (computedVisibilityCapacity != layoutCounts.VisibilityBufferCapacity)
            {
                LogLayoutValidationError(
                    viewId,
                    viewType,
                    $"Visibility buffer capacity mismatch. Expected {layoutCounts.VisibilityBufferCapacity}, computed {computedVisibilityCapacity}.");
            }

            if (computedDrawCount != layoutCounts.DrawCommandCount)
            {
                LogLayoutValidationError(
                    viewId,
                    viewType,
                    $"Draw command count mismatch. Expected {layoutCounts.DrawCommandCount}, computed {computedDrawCount}.");
            }

            if (computedDrawBinCount != layoutCounts.DrawBinCount)
            {
                LogLayoutValidationError(
                    viewId,
                    viewType,
                    $"Draw bin count mismatch. Expected {layoutCounts.DrawBinCount}, computed {computedDrawBinCount}.");
            }
        }

        private static void LogLayoutValidationError(BatchPackedCullingViewID viewId, BatchCullingViewType viewType, string message)
        {
            Debug.LogError($"[Flora][CullingLayout][{viewId}:{viewType}] {message}");
        }

        private static string FormatPartitionInfo(NativeArray<PartitionSummary> partitions, int offset, int count)
        {
            var builder = new StringBuilder(count * 12 + 2);
            builder.Append('[');
            for (int i = 0; i < count; i++)
            {
                if (i != 0)
                    builder.Append(", ");

                PartitionSummary partition = partitions[offset + i];
                builder.Append(partition.LightmapIndex);
                builder.Append(':');
                builder.Append(partition.VisibleInstanceCount);
            }

            builder.Append(']');
            return builder.ToString();
        }
#endif

        #endregion

        #region Indirect Culling Requests

        private const int CullingRequestStaleThreshold = 2;

        private IndirectCullingRequest AllocateCullingRequest(in IndirectCullingRequestParameters parameters)
        {
            int requestID = m_NextCullingViewRequestID++;
            if (requestID >= m_CullingViewRequestPool.Length)
            {
                int newSize = Math.Max(m_CullingViewRequestPool.Length * 2, 8);
                Array.Resize(ref m_CullingViewRequestPool, newSize);
            }

            IndirectCullingRequest request = m_CullingViewRequestPool[requestID];
            if (request == null)
            {
                request = new IndirectCullingRequest(requestID);
                m_CullingViewRequestPool[requestID] = request;
            }

            request.Initialize(parameters);

            if (parameters.Context.viewType is BatchCullingViewType.Camera or BatchCullingViewType.Light)
                m_QueuedCullingRequests.Enqueue(request); // Other view types are dispatched immediately after culling

            m_ContextCullingRequests.Add(request);

            return request;
        }

        private void CleanupStaleCullingRequests()
        {
            for (int i = 0; i < m_CullingViewRequestPool.Length; i++)
            {
                IndirectCullingRequest request = m_CullingViewRequestPool[i];
                if (request == null)
                    continue;

                if (m_FrameIndex - request.LastUsedFrameIndex >= CullingRequestStaleThreshold)
                {
                    request.Dispose();
                    m_CullingViewRequestPool[i] = null;
                }
            }
        }

        #endregion

        #region Animated Fade Data

        private const int StaleAnimatedCrossFadeFrameThreshold = 8;

        private AnimatedCrossFadeData ResolveAnimatedCrossFadeData(IndirectCullingRequest request)
        {
            return request.ViewType switch
            {
                BatchCullingViewType.Camera => UpdateCameraAnimatedCrossFadeData(request),
                BatchCullingViewType.Light => ResolveLightAnimatedCrossFadeData(request),
                _ => CreateCurrentAnimatedCrossFadeData(request),
            };
        }

        private AnimatedCrossFadeData UpdateCameraAnimatedCrossFadeData(IndirectCullingRequest request)
        {
            EntityId viewID = request.ViewID.GetEntityIdCompat();
            AnimatedCrossFadeData data;

            if (m_AnimatedCrossFadeViewMap.TryGetValue(viewID, out int index))
            {
                data = m_AnimatedCrossFadeDatas.Ptr[index];
                data.LastUpdateFrameIndex = m_FrameIndex;
                data.Update(request.LODParameters, request.ScreenRelativeMetric);
                m_AnimatedCrossFadeDatas.Ptr[index] = data;
            }
            else
            {
                data = new AnimatedCrossFadeData();
                data.ViewId = viewID;
                data.LastUpdateFrameIndex = m_FrameIndex;
                data.Reset(request.LODParameters, request.ScreenRelativeMetric);
                index = m_AnimatedCrossFadeDatas.Length;
                m_AnimatedCrossFadeDatas.Add(data);
                m_AnimatedCrossFadeViewMap.TryAdd(viewID, index);
            }

            int lodHash = request.LODParameters.GetHashCode();
            m_AnimatedCrossFadeLodHashMap.TryAdd(lodHash, index);
            return data;
        }

        private AnimatedCrossFadeData ResolveLightAnimatedCrossFadeData(IndirectCullingRequest request)
        {
            // Shadow views may share view IDs across different LOD inputs, so they only borrow camera-owned fades.
            int lodHash = request.LODParameters.GetHashCode();
            return m_AnimatedCrossFadeLodHashMap.TryGetValue(lodHash, out int index)
                ? m_AnimatedCrossFadeDatas.Ptr[index]
                : CreateCurrentAnimatedCrossFadeData(request);
        }

        private static AnimatedCrossFadeData CreateCurrentAnimatedCrossFadeData(IndirectCullingRequest request)
        {
            var data = new AnimatedCrossFadeData
            {
                ViewId = request.ViewID.GetEntityIdCompat(),
                LastUpdateFrameIndex = request.LastUsedFrameIndex,
            };
            data.Reset(request.LODParameters, request.ScreenRelativeMetric);
            return data;
        }

        private void CleanupStaleAnimatedCrossFadeData()
        {
            int frameThreshold = m_FrameIndex - StaleAnimatedCrossFadeFrameThreshold;
            for (int i = m_AnimatedCrossFadeDatas.Length - 1; i >= 0; i--)
            {
                AnimatedCrossFadeData data = m_AnimatedCrossFadeDatas.Ptr[i];
                if (data.LastUpdateFrameIndex < frameThreshold)
                {
                    m_AnimatedCrossFadeDatas.RemoveAtSwapBack(i);
                    if (i < m_AnimatedCrossFadeDatas.Length)
                    {
                        AnimatedCrossFadeData movedData = m_AnimatedCrossFadeDatas.Ptr[i];
                        m_AnimatedCrossFadeViewMap[movedData.ViewId] = i;
                    }

                    m_AnimatedCrossFadeViewMap.Remove(data.ViewId);
                }
            }
        }

        #endregion

        #region Debug

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private const int MaxCullingStatsStoredFrames = 3;

        private void UpdateCullingStats()
        {
            if (EnableCPUCullingStats)
            {
                using (ListPool<EntityId>.Get(out var oldViewIds))
                {
                    foreach (var kvp in m_CPUCullingStats)
                    {
                        if (m_FrameIndex - kvp.Value.FrameIndex >= MaxCullingStatsStoredFrames)
                            oldViewIds.Add(kvp.Key);
                    }

                    foreach (var viewId in oldViewIds)
                        m_CPUCullingStats.Remove(viewId);
                }
            }
            else
            {
                m_CPUCullingStats.Clear();
            }

            if (EnableGPUCullingStats)
            {
                using (ListPool<EntityId>.Get(out var oldViewIds))
                {
                    foreach (var kvp in m_GPUCullingStats)
                    {
                        if (m_FrameIndex - kvp.Value.FrameIndex >= MaxCullingStatsStoredFrames)
                            oldViewIds.Add(kvp.Key);
                    }

                    foreach (var viewId in oldViewIds)
                        m_GPUCullingStats.Remove(viewId);
                }
            }
            else
            {
                m_GPUCullingStats.Clear();
            }
        }

        internal void GetCPUCullingStats(List<CPUCullingStats> outStats)
        {
            outStats.Clear();

            if (EnableCPUCullingStats)
            {
                foreach (CPUCullingStats cullStats in m_CPUCullingStats.Values)
                    outStats.Add(cullStats);
            }
        }

        internal void GetGPUCullingStats(List<GPUCullingStats> outStats)
        {
            outStats.Clear();

            if (EnableGPUCullingStats)
            {
                foreach (GPUCullingStats indirectStats in m_GPUCullingStats.Values)
                    outStats.Add(indirectStats);
            }
        }
#endif

        #endregion
    }
}
