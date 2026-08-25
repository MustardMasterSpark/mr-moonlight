using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace MA.Flora
{
    internal struct IndirectCullingRequestHandles
    {
        public BufferHandle DrawArgsBuffer;
        public BufferHandle VisibilityBuffer;

        public void UseWith(IComputeRenderGraphBuilder builder)
        {
            builder.UseBuffer(DrawArgsBuffer, AccessFlags.Write);
            builder.UseBuffer(VisibilityBuffer, AccessFlags.Write);
        }
    }

    internal struct IndirectCullingRequestParameters
    {
        public int FrameIndex;
        public BatchCullingContext Context;
        public DrawBinConfig BinConfig;
        public float ScreenRelativeMetric;
        public float MeshLodSelectionConstant;
        public FrustumPlaneCuller FrustumPlaneCuller;
        public int DrawInstanceCapacity;
        public int DrawCommandCapacity;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public bool EnableDebugInstanceVisibility;
        public bool EnableDebugGPUCullingStats;
#endif
#if UNITY_EDITOR
        public bool IsSceneViewCamera;
#endif
    }

    internal enum IndirectCullingRequestState
    {
        None,
        Initialized,
        Scheduled,
        Completed,
    }

    internal sealed unsafe class IndirectCullingRequest : IDisposable
    {
        public int RequestID;
        public int LastUsedFrameIndex;
        public IndirectCullingRequestState State;

        // Context info
        public BatchCullingViewType ViewType;
        public BatchPackedCullingViewID ViewID;
        public LODParameters LODParameters;
        public int SplitCount;
        public float ScreenRelativeMetric;
        public float MeshLodSelectionConstant;

        // Culling info
        public JobHandle CullingHandle;
        public DrawBinConfig BinConfig;
        public NativeArray<IndirectCullingOutput> CullingOutput;
        public CullingPlanBuffers PlanBuffers;
        public NativeList<Plane> Planes;
        public NativeList<FrustumPlaneCuller.SplitInfo> SplitInfos;

        // Buffers
        public ConstantBufferRef<CullingViewShaderVariables> ShaderVariables;
        public GraphicsBufferRef DrawChunkBuffer;
        public GraphicsBufferRef DrawPartitionBuffer;
        public GraphicsBufferRef DrawBinBuffer;
        public GraphicsBufferRef DrawInfoBuffer;
        public GraphicsBufferRef DrawArgsBuffer;
        public GraphicsBufferRef WorkGroupDataBuffer;
        public GraphicsBufferRef WorkGroupArgsBuffer;
        public GraphicsBufferRef VisibilityBuffer;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public GraphicsBufferRef DebugInstanceVisibilityBuffer;
        public GraphicsBufferRef DebugDispatchCounterBuffer;
        public GraphicsBufferRef DebugErrorCountBuffer;
        public GraphicsBufferRef DebugErrorBuffer;
#endif
#if UNITY_EDITOR
        public bool IsSceneViewCamera;
        public NativeList<ulong> IncludedInstances;
        public GraphicsBufferRef IncludedInstancesBuffer;
        public bool HasIncludedInstances;
        public int IncludedChunkCount;
#endif

        public bool IsCreated => CullingOutput.IsCreated;
        public bool IsValid => State != IndirectCullingRequestState.None;
        public bool IsScheduled => State == IndirectCullingRequestState.Scheduled;
        public bool IsCompleted => State == IndirectCullingRequestState.Completed;

        public const int DebugMaxErrorRecords = 1024;

        private const int InitialChunkCapacity    = 64;
        private const int InitialTemplateCapacity = 64;
        private const int InitialDrawBinCapacity  = 64;
        private const int InitialDrawInfoCapacity = 256;
        internal const int CullingWorkGroupStride  = sizeof(uint) * 8;

        public IndirectCullingRequest(int requestID)
        {
            RequestID = requestID;
            State = IndirectCullingRequestState.None;
            Assert.AreEqual(16, UnsafeUtility.SizeOf<IndirectDrawPartition>(), "IndirectDrawPartition must remain 16 bytes.");
            Assert.AreEqual(16, UnsafeUtility.SizeOf<IndirectDrawChunk>(), "IndirectDrawChunk must remain 16 bytes.");
            Assert.AreEqual(16, UnsafeUtility.SizeOf<IndirectDrawBin>(), "IndirectDrawBin must remain 16 bytes.");
            Assert.AreEqual(16, UnsafeUtility.SizeOf<IndirectDrawInfo>(), "IndirectDrawInfo must remain 16 bytes.");
            Assert.AreEqual(32, CullingWorkGroupStride, "CullingWorkGroup stride must remain 32 bytes.");

            CullingOutput = new NativeArray<IndirectCullingOutput>(1, Allocator.Persistent);
            Planes = new NativeList<Plane>(16, Allocator.Persistent);
            SplitInfos = new NativeList<FrustumPlaneCuller.SplitInfo>(4, Allocator.Persistent);
            ShaderVariables = new ConstantBufferRef<CullingViewShaderVariables>("Flora.CullingViewShaderVariables");
            DrawChunkBuffer = new GraphicsBufferRef(GraphicsBuffer.Target.Structured, InitialChunkCapacity, sizeof(IndirectDrawChunk), "Flora.DrawChunkBuffer");
            DrawPartitionBuffer = new GraphicsBufferRef(GraphicsBuffer.Target.Structured, InitialTemplateCapacity, sizeof(IndirectDrawPartition), "Flora.DrawPartitionBuffer");
            DrawBinBuffer = new GraphicsBufferRef(GraphicsBuffer.Target.Structured, InitialDrawBinCapacity, sizeof(IndirectDrawBin), "Flora.DrawBinBuffer");
            DrawInfoBuffer = new GraphicsBufferRef(GraphicsBuffer.Target.Structured, InitialDrawInfoCapacity, sizeof(IndirectDrawInfo), "Flora.DrawInfoBuffer");
            DrawArgsBuffer = new GraphicsBufferRef(GraphicsBuffer.Target.IndirectArguments | GraphicsBuffer.Target.Structured, InitialDrawInfoCapacity * 5, 4, "Flora.DrawArgsBuffer");
            WorkGroupDataBuffer = new GraphicsBufferRef(GraphicsBuffer.Target.Structured, InitialChunkCapacity, CullingWorkGroupStride, "Flora.WorkGroupDataBuffer");
            WorkGroupArgsBuffer = new GraphicsBufferRef(GraphicsBuffer.Target.IndirectArguments, 4, 4, "Flora.WorkGroupArgsBuffer");
            VisibilityBuffer = new GraphicsBufferRef(GraphicsBuffer.Target.Raw, 256, 4, "Flora.VisibilityBuffer");

#if UNITY_EDITOR
            IncludedInstances = new NativeList<ulong>(0, Allocator.Persistent);
            HasIncludedInstances = false;
            IncludedChunkCount = 0;
#endif

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            DebugInstanceVisibilityBuffer = default;
            DebugDispatchCounterBuffer = default;
#endif
        }

        public void Dispose()
        {
            CullingHandle.Complete();

            if (CullingOutput[0].IsCreated)
                CullingOutput[0].Dispose();

            CullingOutput.Dispose();
            PlanBuffers.Dispose();
            Planes.Dispose();
            SplitInfos.Dispose();

            ShaderVariables.Dispose();
            DrawChunkBuffer.Dispose();
            DrawPartitionBuffer.Dispose();
            DrawBinBuffer.Dispose();
            DrawInfoBuffer.Dispose();
            DrawArgsBuffer.Dispose();
            WorkGroupDataBuffer.Dispose();
            WorkGroupArgsBuffer.Dispose();
            VisibilityBuffer.Dispose();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            DebugInstanceVisibilityBuffer.Dispose();
            DebugDispatchCounterBuffer.Dispose();
            DebugErrorCountBuffer.Dispose();
            DebugErrorBuffer.Dispose();
#endif
#if UNITY_EDITOR
            IncludedInstances.Dispose();
            IncludedInstancesBuffer.Dispose();
            HasIncludedInstances = false;
            IncludedChunkCount = 0;
#endif
            State = IndirectCullingRequestState.None;
        }

        public void Release()
        {
            CullingHandle.Complete();

            if (CullingOutput[0].IsCreated)
                CullingOutput[0].Dispose();

            CullingOutput[0] = default;

            Planes.Clear();
            SplitInfos.Clear();
#if UNITY_EDITOR
            IncludedInstances.Clear();
            HasIncludedInstances = false;
            IncludedChunkCount = 0;
#endif
            State = IndirectCullingRequestState.None;
        }

        public void Initialize(in IndirectCullingRequestParameters parameters)
        {
            // Just in case, complete and release previous resources
            CullingHandle.Complete();

            if (CullingOutput[0].IsCreated)
                CullingOutput[0].Dispose();
            CullingOutput[0] = default;

            LastUsedFrameIndex = parameters.FrameIndex;
            State = IndirectCullingRequestState.Initialized;
            ViewType = parameters.Context.viewType;
            ViewID = parameters.Context.viewID;
            LODParameters = parameters.Context.lodParameters;
            SplitCount = parameters.Context.cullingSplits.Length;
            ScreenRelativeMetric = parameters.ScreenRelativeMetric;
            MeshLodSelectionConstant = parameters.MeshLodSelectionConstant;

            BinConfig = parameters.BinConfig;
            Planes.CopyFrom(parameters.FrustumPlaneCuller.Planes);
            SplitInfos.CopyFrom(parameters.FrustumPlaneCuller.SplitInfos);
#if UNITY_EDITOR
            IncludedInstances.Clear();
            IsSceneViewCamera = parameters.IsSceneViewCamera;
            HasIncludedInstances = false;
            IncludedChunkCount = 0;
#endif

            DrawArgsBuffer.ResizeIfNeeded(parameters.DrawCommandCapacity * 5, GraphicsBufferGrowPolicy.WithSlack, keepContents: false);
            VisibilityBuffer.ResizeIfNeeded(parameters.DrawInstanceCapacity, GraphicsBufferGrowPolicy.WithSlack, keepContents: false);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            bool validInstanceVisualizationType = ViewType == BatchCullingViewType.Camera;
            if (validInstanceVisualizationType && parameters.EnableDebugInstanceVisibility)
            {
                if (!DebugInstanceVisibilityBuffer.IsCreated)
                    DebugInstanceVisibilityBuffer = new GraphicsBufferRef(GraphicsBuffer.Target.Structured, parameters.DrawInstanceCapacity, sizeof(uint), "Flora.DebugInstanceVisibilityBuffer");

                DebugInstanceVisibilityBuffer.ResizeIfNeeded(parameters.DrawInstanceCapacity, GraphicsBufferGrowPolicy.WithSlack, keepContents: false);
            }
            else
            {
                DebugInstanceVisibilityBuffer.Dispose();
            }

            bool validDispatchCounterType = ViewType is BatchCullingViewType.Camera or BatchCullingViewType.Light;
            if (validDispatchCounterType && parameters.EnableDebugGPUCullingStats)
            {
                if (!DebugDispatchCounterBuffer.IsCreated)
                    DebugDispatchCounterBuffer = new GraphicsBufferRef(GraphicsBuffer.Target.Structured, (int)IndirectDispatchCounter.Count, 4, "Flora.DebugDispatchCounterBuffer");
            }
            else
            {
                DebugDispatchCounterBuffer.Dispose();
            }
#endif
        }

        public JobHandle Schedule(ref BatchCullingOutput batchCullingOutput, JobHandle dependency)
        {
            Assert.IsTrue(State == IndirectCullingRequestState.Initialized, "CullingRequest must be initialized before scheduling.");
            State = IndirectCullingRequestState.Scheduled;
            CullingHandle = dependency;
            batchCullingOutput.customCullingResult[0] = (IntPtr)RequestID;
            return CullingHandle;
        }

        public IndirectCullingRequestHandles ImportBuffers(RenderGraph renderGraph)
        {
            IndirectCullingRequestHandles handles;
            handles.DrawArgsBuffer   = renderGraph.ImportBuffer(DrawArgsBuffer);
            handles.VisibilityBuffer = renderGraph.ImportBuffer(VisibilityBuffer);
            return handles;
        }

        public bool CompleteAndUpdate(CommandBuffer cmd, VolumeStack volumeStack, in AnimatedCrossFadeData animatedCrossFadeData, out IndirectCullingOutput indirectCullingOutput)
        {
            indirectCullingOutput = default;
            CullingHandle.Complete();
            if (State != IndirectCullingRequestState.Scheduled)
            {
                State = IndirectCullingRequestState.Completed;
                return false;
            }

            indirectCullingOutput = CullingOutput[0];
            if (indirectCullingOutput.DrawChunkCount == 0 || indirectCullingOutput.DrawCount == 0)
            {
                State = IndirectCullingRequestState.Completed;
                return false;
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (DebugDisplayFlora.Active && DebugDisplayFlora.Properties.EnableGPUChecks)
                ValidateCpuCullingOutput(indirectCullingOutput);
#endif

            UpdateVariablesData(volumeStack, in animatedCrossFadeData);

            ShaderVariables.UpdateData(cmd);

            WorkGroupDataBuffer.ResizeIfNeeded(indirectCullingOutput.DrawChunkCount, GraphicsBufferGrowPolicy.WithSlack, keepContents: false);
            DrawChunkBuffer.ResizeIfNeeded(indirectCullingOutput.DrawChunkCount, GraphicsBufferGrowPolicy.WithSlack, keepContents: false);
            cmd.SetBufferData(DrawChunkBuffer, indirectCullingOutput.DrawChunks, indirectCullingOutput.DrawChunkCount, UnsafeUtility.SizeOf<IndirectDrawChunk>());

            DrawPartitionBuffer.ResizeIfNeeded(indirectCullingOutput.DrawPartitionCount, GraphicsBufferGrowPolicy.WithSlack, keepContents: false);
            cmd.SetBufferData(DrawPartitionBuffer, indirectCullingOutput.DrawPartitions, indirectCullingOutput.DrawPartitionCount, UnsafeUtility.SizeOf<IndirectDrawPartition>());

            DrawBinBuffer.ResizeIfNeeded(indirectCullingOutput.DrawBinCount, GraphicsBufferGrowPolicy.WithSlack, keepContents: false);
            cmd.SetBufferData(DrawBinBuffer, indirectCullingOutput.DrawBins, indirectCullingOutput.DrawBinCount, UnsafeUtility.SizeOf<IndirectDrawBin>());

            DrawInfoBuffer.ResizeIfNeeded(indirectCullingOutput.DrawCount, GraphicsBufferGrowPolicy.WithSlack, keepContents: false);
            cmd.SetBufferData(DrawInfoBuffer, indirectCullingOutput.DrawInfos, indirectCullingOutput.DrawCount, UnsafeUtility.SizeOf<IndirectDrawInfo>());

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (DebugDispatchCounterBuffer.IsCreated)
            {
                GraphicsBufferUtility.Memset(cmd, DebugDispatchCounterBuffer, 0, 0, (int)IndirectDispatchCounter.Count);
            }

            if (DebugInstanceVisibilityBuffer.IsCreated)
            {
                cmd.SetGlobalBuffer(DebugShaderPropertyId.flora_DebugDrawVisibility, DebugInstanceVisibilityBuffer);
            }

            if (DebugDisplayFlora.Active && DebugDisplayFlora.Properties.EnableGPUChecks)
            {
                if (!DebugErrorCountBuffer.IsCreated)
                    DebugErrorCountBuffer = new GraphicsBufferRef(GraphicsBuffer.Target.Structured, 1, 4, "Flora.DebugErrorCountBuffer");
                if (!DebugErrorBuffer.IsCreated)
                    DebugErrorBuffer = new GraphicsBufferRef(GraphicsBuffer.Target.Structured, DebugMaxErrorRecords, 16, "Flora.DebugErrorBuffer");

                GraphicsBufferUtility.Memset(cmd, DebugErrorCountBuffer, 0, 0, 1);
            }
            else
            {
                DebugErrorCountBuffer.Dispose();
                DebugErrorBuffer.Dispose();
            }
#endif

#if UNITY_EDITOR
            UpdateIncludedInstancesBuffer(cmd);
#endif

            State = IndirectCullingRequestState.Completed;
            return true;
        }

#if UNITY_EDITOR
        internal void UpdateIncludedInstancesBuffer(CommandBuffer cmd)
        {
            HasIncludedInstances = false;
            IncludedChunkCount = 0;

            if (IncludedInstances.Length <= 0)
            {
                IncludedInstancesBuffer.Dispose();
                return;
            }

            if (!IncludedInstancesBuffer.IsCreated)
                IncludedInstancesBuffer = new GraphicsBufferRef(GraphicsBuffer.Target.Structured, IncludedInstances.Length, sizeof(ulong), "Flora.IncludedInstancesBuffer");

            IncludedInstancesBuffer.ResizeIfNeeded(IncludedInstances.Length, GraphicsBufferGrowPolicy.WithSlack, keepContents: false);
            cmd.SetBufferData(IncludedInstancesBuffer, IncludedInstances.AsArray());

            HasIncludedInstances = true;
            IncludedChunkCount = IncludedInstances.Length;
        }
#endif

        public bool HasDebugDispatchCounters()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            return DebugDispatchCounterBuffer.IsCreated;
#else
            return false;
#endif
        }

        public void RequestDebugDispatchCounters(CommandBuffer cmd, Action<GPUCullingStats> onComplete)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (DebugDispatchCounterBuffer.IsCreated)
            {
                EntityId viewId = ViewID.GetEntityIdCompat();
                cmd.RequestAsyncReadback(DebugDispatchCounterBuffer, req =>
                {
                    if (!req.hasError)
                    {
                        var data = req.GetData<int>();
                        var stat = new GPUCullingStats
                        {
                            ViewId = viewId,
                            ViewType = ViewType,
                            FrameIndex = LastUsedFrameIndex,
                            VisibleDraws = data[(int)IndirectDispatchCounter.VisibleDraws],
                            VisibleInstances = data[(int)IndirectDispatchCounter.VisibleInstances],
                            OccludedInstances = data[(int)IndirectDispatchCounter.OccludedInstances],
                        };
                        onComplete?.Invoke(stat);
                    }
                });
            }
#endif
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private void ValidateCpuCullingOutput(in IndirectCullingOutput indirectCullingOutput)
        {
            int previousVisibleEnd = 0;
            int previousCommandEnd = 0;

            for (int binIndex = 0; binIndex < indirectCullingOutput.DrawBinCount; binIndex++)
            {
                IndirectDrawBin drawBin = indirectCullingOutput.DrawBins[binIndex];
                int reservedVisibleCount = indirectCullingOutput.DrawBinCapacities != null ? indirectCullingOutput.DrawBinCapacities[binIndex] : 0;
                if (drawBin.commandCount == 0)
                {
                    if (reservedVisibleCount != 0)
                    {
                        Debug.LogError(
                            $"[Flora][IndirectCullingRequest][{ViewID}:{ViewType}] Draw bin {binIndex} reserved {reservedVisibleCount} visible slots without commands. " +
                            $"{FormatDrawBinReservation(binIndex, drawBin, reservedVisibleCount)}");
                    }

                    continue;
                }

                int visibleStart = (int)drawBin.visibleStart;
                int visibleEnd = visibleStart + reservedVisibleCount;
                if (visibleStart < previousVisibleEnd)
                {
                    Debug.LogError(
                        $"[Flora][IndirectCullingRequest][{ViewID}:{ViewType}] Draw bin {binIndex} overlaps or reorders visible range reservations. " +
                        $"PreviousVisibleEnd: {previousVisibleEnd}, {FormatDrawBinReservation(binIndex, drawBin, reservedVisibleCount)}");
                }

                if (visibleEnd > indirectCullingOutput.VisibilityBufferCapacity)
                {
                    Debug.LogError(
                        $"[Flora][IndirectCullingRequest][{ViewID}:{ViewType}] Draw bin {binIndex} reserved visible range past capacity. " +
                        $"{FormatDrawBinReservation(binIndex, drawBin, reservedVisibleCount)}, VisibilityCapacity: {indirectCullingOutput.VisibilityBufferCapacity}");
                }

                int commandStart = (int)drawBin.commandStart;
                int commandEnd = commandStart + (int)drawBin.commandCount;
                if (commandStart < previousCommandEnd)
                {
                    Debug.LogError(
                        $"[Flora][IndirectCullingRequest][{ViewID}:{ViewType}] Draw bin {binIndex} overlaps or reorders command reservations. " +
                        $"PreviousCommandEnd: {previousCommandEnd}, {FormatDrawBinReservation(binIndex, drawBin, reservedVisibleCount)}");
                }

                if (commandEnd > indirectCullingOutput.DrawCount)
                {
                    Debug.LogError(
                        $"[Flora][IndirectCullingRequest][{ViewID}:{ViewType}] Draw bin {binIndex} reserved commands past capacity. " +
                        $"{FormatDrawBinReservation(binIndex, drawBin, reservedVisibleCount)}, DrawCount: {indirectCullingOutput.DrawCount}");
                    commandEnd = indirectCullingOutput.DrawCount;
                }

                for (int commandIndex = commandStart; commandIndex < commandEnd; commandIndex++)
                {
                    if (indirectCullingOutput.DrawCommandInfos[commandIndex].BatchIndex == DrawBatchIndex.None)
                    {
                        Debug.LogError(
                            $"[Flora][IndirectCullingRequest][{ViewID}:{ViewType}] Draw command {commandIndex} was reserved but left uninitialized. " +
                            $"{FormatDrawBinReservation(binIndex, drawBin, reservedVisibleCount)}");
                    }
                }

                previousVisibleEnd = math.max(previousVisibleEnd, visibleEnd);
                previousCommandEnd = math.max(previousCommandEnd, commandEnd);
            }

            if (previousVisibleEnd != indirectCullingOutput.VisibilityBufferCapacity)
            {
                Debug.LogError(
                    $"[Flora][IndirectCullingRequest][{ViewID}:{ViewType}] Visible reservation extent mismatch. End: {previousVisibleEnd}, Capacity: {indirectCullingOutput.VisibilityBufferCapacity}");
            }

            if (previousCommandEnd != indirectCullingOutput.DrawCount)
            {
                Debug.LogError(
                    $"[Flora][IndirectCullingRequest][{ViewID}:{ViewType}] Command reservation extent mismatch. End: {previousCommandEnd}, Capacity: {indirectCullingOutput.DrawCount}");
            }
        }

        private static string FormatDrawBinReservation(int binIndex, in IndirectDrawBin drawBin, int reservedVisibleCount)
        {
            int visibleStart = (int)drawBin.visibleStart;
            int visibleEnd = visibleStart + reservedVisibleCount;
            int commandStart = (int)drawBin.commandStart;
            int commandEnd = commandStart + (int)drawBin.commandCount;
            return $"Bin: {binIndex}, VisibleRange: [{visibleStart}, {visibleEnd}), ReservedVisibleCount: {reservedVisibleCount}, " +
                   $"CommandRange: [{commandStart}, {commandEnd}), CommandCount: {drawBin.commandCount}";
        }
#endif

        public void OnPostDispatchCulling(CommandBuffer cmd)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (DebugDisplayFlora.Active && DebugDisplayFlora.Properties.EnableGPUChecks)
            {
                if (DebugErrorCountBuffer.IsCreated && DebugErrorBuffer.IsCreated)
                {
                    cmd.RequestAsyncReadback(DebugErrorCountBuffer, reqCount =>
                    {
                        try
                        {
                            if (!reqCount.hasError)
                            {
                                var countData = reqCount.GetData<int>();
                                var errorCount = countData[0];
                                if (errorCount > 0)
                                {
                                    errorCount = math.min(errorCount, DebugMaxErrorRecords);
                                    cmd.RequestAsyncReadback(DebugErrorBuffer, reqErrors =>
                                    {
                                        try
                                        {
                                            if (!reqErrors.hasError)
                                            {
                                                var errorData = reqErrors.GetData<uint4>();
                                                for (int i = 0; i < errorCount; ++i)
                                                {
                                                    GPUErrorCode errorCode = (GPUErrorCode)errorData[i].x;
                                                    switch (errorCode)
                                                    {
                                                        case GPUErrorCode.StateKeyOutOfRange:
                                                        {
                                                            var templateIndex = errorData[i].y;
                                                            var stateKey = errorData[i].z;
                                                            Debug.LogError(
                                                                $"[Flora][IndirectCullingRequest][{ViewID}:{ViewType}] GPU culling error: " +
                                                                $"State key out of range! TemplateIndex: {templateIndex}, StateKey: {stateKey}");
                                                            break;
                                                        }
                                                        case GPUErrorCode.LodIndexOutOfRange:
                                                        {
                                                            var templateIndex = errorData[i].y;
                                                            var lodIndex = errorData[i].z;
                                                            var lodMax = errorData[i].w;
                                                            Debug.LogError(
                                                                $"[Flora][IndirectCullingRequest][{ViewID}:{ViewType}] GPU culling error: " +
                                                                $"LOD index out of range! TemplateIndex: {templateIndex}, LodIndex: {lodIndex}, LodMax: {lodMax}");
                                                            break;
                                                        }
                                                        case GPUErrorCode.TemplateLodInconsistent:
                                                        {
                                                            var templateIndex = errorData[i].y;
                                                            var lodIndex = errorData[i].z;
                                                            var lodMax = errorData[i].w;
                                                            Debug.LogError(
                                                                $"[Flora][IndirectCullingRequest][{ViewID}:{ViewType}] GPU culling error: " +
                                                                $"Template LOD inconsistent! TemplateIndex: {templateIndex}, LodIndex: {lodIndex}, LodMax: {lodMax}");
                                                            break;
                                                        }
                                                        case GPUErrorCode.CommandCountZero:
                                                        {
                                                            var binIndex = errorData[i].y;
                                                            var countInBin = errorData[i].z;
                                                            var stateKey = errorData[i].w;
                                                            Debug.LogError(
                                                                $"[Flora][IndirectCullingRequest][{ViewID}:{ViewType}] GPU culling error: " +
                                                                $"Draw bin has zero command count but non-zero visible instances! BinIndex: {binIndex}, CountInBin: {countInBin}, StateKey: {stateKey}");
                                                            break;
                                                        }
                                                        case GPUErrorCode.PackedKeyOrLodOutOfRange:
                                                        {
                                                            var key = errorData[i].y;
                                                            var lodIndex = errorData[i].z;
                                                            Debug.LogError(
                                                                $"[Flora][IndirectCullingRequest][{ViewID}:{ViewType}] GPU culling error: " +
                                                                $"Packed key or LOD out of range! PackedKey: {key}, LodIndex: {lodIndex}");
                                                            break;
                                                        }
                                                    }
                                                }
                                            }
                                        }
                                        catch (Exception e)
                                        {
                                            Debug.LogError($"[Flora][IndirectCullingRequest][{ViewID}:{ViewType}] Exception during GPU culling error readback: {e}");
                                        }
                                    });
                                }
                            }
                        }
                        catch (Exception e)
                        {
                            Debug.LogError($"[Flora][IndirectCullingRequest][{ViewID}:{ViewType}] Exception during GPU culling error count readback: {e}");
                        }
                    });
                }

                if (DrawBinBuffer.IsCreated)
                {
                    var indirectCullingOutput = CullingOutput[0];
                    var drawBinCount = indirectCullingOutput.DrawBinCount;
                    var drawBinCapacities = MemoryUtility.Allocate<int>(drawBinCount, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
                    UnsafeUtility.MemCpy(drawBinCapacities, indirectCullingOutput.DrawBinCapacities, drawBinCount * UnsafeUtility.SizeOf<int>());

                    cmd.RequestAsyncReadback(DrawBinBuffer, req =>
                    {
                        try
                        {
                            if (!req.hasError)
                            {
                                var indirectDrawBins = req.GetData<IndirectDrawBin>();
                                for (int i = 0; i < drawBinCount; ++i)
                                {
                                    var bin = indirectDrawBins[i];
                                    if (bin.commandCount == 0 && bin.visibleCount > 0)
                                    {
                                        Debug.LogError(
                                            $"[Flora][IndirectCullingRequest][{ViewID}:{ViewType}] Draw bin with zero commands has visible instances! BinIndex: {i}, VisibleCount: {bin.visibleCount}");
                                    }

                                    int recordedCapacity = drawBinCapacities[i];
                                    if (bin.visibleCount > recordedCapacity)
                                    {
                                        Debug.LogError(
                                            $"[Flora][IndirectCullingRequest][{ViewID}:{ViewType}] Draw bin capacity overflow detected! BinIndex: {i}, VisibleCount: {bin.visibleCount}, RecordedCapacity: {recordedCapacity}");
                                    }
                                }
                            }
                        }
                        finally
                        {
                            UnsafeUtility.Free(drawBinCapacities, Allocator.Persistent);
                        }
                    });
                }
            }
            else
            {
                DebugErrorCountBuffer.Dispose();
                DebugErrorBuffer.Dispose();
            }
#endif
        }

        private void UpdateVariablesData(VolumeStack volumeStack, in AnimatedCrossFadeData animatedCrossFadeData)
        {
            CullingViewShaderVariables cb = ShaderVariables.Value;
            FloraRenderSettings cullingSettings = volumeStack.GetComponent<FloraRenderSettings>();
            FloraDensitySettings densitySettings = volumeStack.GetComponent<FloraDensitySettings>();

            int planeOffsetCPU = 0;
            int planeOffsetGPU = 0;
            NativeArray<Plane> cullingPlanes = Planes.AsArray();
            int splitInfoCount = SplitInfos.Length;
            for (int splitIndex = 0; splitIndex < splitInfoCount; ++splitIndex)
            {
                FrustumPlaneCuller.SplitInfo split = SplitInfos[splitIndex];
                NativeArray<Plane> frustumPlanes = cullingPlanes.GetSubArray(planeOffsetCPU, split.PlaneCount);

                int planeCount = math.min(5, frustumPlanes.Length);
                for (int planeIndex = 0; planeIndex < planeCount; ++planeIndex)
                {
                    float4 plane = new float4(frustumPlanes[planeIndex].normal, frustumPlanes[planeIndex].distance);
                    int baseFloatIndex = (planeOffsetGPU + planeIndex) * 4;
                    for (int planeElementIndex = 0; planeElementIndex < 4; ++planeElementIndex)
                    {
                        cb._ViewFrustumPlanes[baseFloatIndex + planeElementIndex] = plane[planeElementIndex];
                    }
                }

                planeOffsetCPU += frustumPlanes.Length;
                planeOffsetGPU += planeCount;
            }

            LODGroup.crossFadeAnimationDuration = Mathf.Max(cullingSettings.CrossFadeDuration.value, 0.001f);

            float maxRenderDistance = cullingSettings.MaxRenderDistance.value;
            if (ViewType == BatchCullingViewType.Light) maxRenderDistance = cullingSettings.MaxShadowDistance.value;
            int minLOD = ViewType == BatchCullingViewType.Light ? math.max(cullingSettings.MinShadowLOD.value, QualitySettings.maximumLODLevel) : QualitySettings.maximumLODLevel;

            float meshLodSelectionConstantSq = MeshLodSelectionConstant * MeshLodSelectionConstant;
            float screenRelativeMetricSq = math.lengthsq(ScreenRelativeMetric);
            cb._ViewCameraPosition_ScreenMetric = new float4(LODParameters.cameraPosition, screenRelativeMetricSq);
            cb._ViewCullingParams0 = new float4(math.asfloat(SplitCount), maxRenderDistance, meshLodSelectionConstantSq, math.asfloat(minLOD));
            cb._ViewCullingParams1 = new float4(math.asfloat((uint)ViewType), 0f, 0f, 0f);

            float prevScreenRelativeMetricSq = math.lengthsq(animatedCrossFadeData.AnimatedLODCameraScreenRelativeMetric0);
            float currScreenRelativeMetricSq = math.lengthsq(animatedCrossFadeData.AnimatedLODCameraScreenRelativeMetric1);
            cb._ViewAnimLodPositionPrev = new float4(animatedCrossFadeData.AnimatedLODCameraPosition0, prevScreenRelativeMetricSq);
            cb._ViewAnimLodPositionCurr = new float4(animatedCrossFadeData.AnimatedLODCameraPosition1, currScreenRelativeMetricSq);

            float minimumScreenSize = cullingSettings.MinScreenSizeMode == FloraMinimumScreenSizeMode.Disabled ? 0f : cullingSettings.MinScreenSize.value;
            float minimumScreenSizeAffectsLODGroups = cullingSettings.MinScreenSizeMode == FloraMinimumScreenSizeMode.RenderersAndLODGroups ? 1f : 0f;
            float animatedCrossFadeAlpha = animatedCrossFadeData.ComputeAlpha();

            float randomizeLodTransition = math.saturate(cullingSettings.RandomizeLODTransition.value) * 0.5f;
            int globalDensityMask = densitySettings.GlobalDensityMode != FloraDensityMode.Disabled ? densitySettings.GlobalDensityMask.value : 0;
            if (densitySettings.GlobalDensity.value >= 1.0f) globalDensityMask = 0;
            float globalDensityAffectsLODGroups = densitySettings.GlobalDensityMode == FloraDensityMode.RenderersAndLODGroups ? 1f : 0f;

            int rangeDensityMask = densitySettings.RangeDensityMode != FloraDensityMode.Disabled ? densitySettings.RangeDensityMask.value : 0;
            if (densitySettings.RangeDensity.value >= 1.0f) rangeDensityMask = 0;

            float allowedScreenSizeRange = 1.0f - minimumScreenSize;
            float densityRangeMax = densitySettings.RangeDensityScreenPercentage.value.x * allowedScreenSizeRange + minimumScreenSize;
            float densityRangeMin = densitySettings.RangeDensityScreenPercentage.value.y * allowedScreenSizeRange + minimumScreenSize;
            float densityRange = densityRangeMax - densityRangeMin;
            float densityFalloffExp = math.exp2(math.lerp(-6.0f, +6.0f, math.saturate(densitySettings.RangeDensityFalloff.value)));
            float rangeDensityAffectsLODGroups = densitySettings.RangeDensityMode == FloraDensityMode.RenderersAndLODGroups ? 1f : 0f;
            if (densityRange <= 1e-4f)
                rangeDensityMask = 0;

            cb._ViewVolumeParams0 = new float4(minimumScreenSize, minimumScreenSizeAffectsLODGroups, LODParameters.isOrthographic ? 1f : 0f, animatedCrossFadeAlpha);
            cb._ViewVolumeParams1 = new float4(randomizeLodTransition, math.asfloat(globalDensityMask), densitySettings.GlobalDensity.value, densitySettings.GlobalDensitySizeThreshold.value);
            cb._ViewVolumeParams2 = new float4(math.asfloat(rangeDensityMask), densitySettings.RangeDensity.value, densityFalloffExp, rangeDensityAffectsLODGroups);
            cb._ViewVolumeParams3 = new float4(densityRangeMin, densityRangeMax, globalDensityAffectsLODGroups, 0f);

            ShaderVariables.Value = cb;
        }
    }
}
