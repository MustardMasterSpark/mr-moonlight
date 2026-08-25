// Copyright © Magnetic Arcade. All Rights Reserved.

using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Rendering;

namespace MA.Flora
{
    internal unsafe partial struct InstanceManager
    {
        #region Scatter Jobs

        [BurstCompile(OptimizeFor = OptimizeFor.Performance, DisableSafetyChecks = true)]
        private struct ScatterArchetypeDataJob : IJobFor
        {
            public const int BatchSize = 256;

            [ReadOnly] public NativeArray<ArchetypeIndex> Archetypes;
            [WriteOnly] public NativeArray<PackedArchetypeData> PackedArchetypeData;

            public void Execute(int index)
            {
                var packedArchetypeData = new PackedArchetypeData(Archetypes[index]);
                PackedArchetypeData[index] = packedArchetypeData;
            }
        }

        [BurstCompile(OptimizeFor = OptimizeFor.Performance, DisableSafetyChecks = true)]
        private struct ScatterRandomIdsJob : IJobFor
        {
            public const int BatchSize = 8;

            [ReadOnly] public NativeArray<ChunkIndex> Chunks;
            [ReadOnly] public NativeArray<float> RandomValues;

            [WriteOnly] public NativeArray<PackedChunkUploadHeader> ChunkDataHeaders;
            [WriteOnly,NativeDisableParallelForRestriction] public NativeArray<float> ScatterValues;

            public void Execute(int index)
            {
                var chunk = Chunks[index];
                var count = chunk.Count;

                var src = RandomValues.GetSubArray(chunk.AsInstanceOffset(), count).GetUnsafeReadOnlyPtrT();
                var dst = ScatterValues.GetSubArray(index * ChunkCapacity, count).GetUnsafePtrT();
                UnsafeUtility.MemCpy(dst, src, count * UnsafeUtility.SizeOf<float>());

                ChunkDataHeaders[index] = new PackedChunkUploadHeader(chunk);
            }
        }

        [BurstCompile(OptimizeFor = OptimizeFor.Performance, DisableSafetyChecks = true)]
        private struct ScatterVariationColorJob : IJobFor
        {
            public const int BatchSize = 8;

            [ReadOnly] public NativeArray<ChunkIndex> Chunks;
            [ReadOnly] public NativeArray<float4> VariationColors;

            [WriteOnly] public NativeArray<PackedChunkUploadHeader> ChunkDataHeaders;
            [WriteOnly,NativeDisableParallelForRestriction] public NativeArray<float4> ScatterValues;

            public void Execute(int index)
            {
                var chunk = Chunks[index];
                var count = chunk.Count;

                var src = VariationColors.GetSubArray(chunk.AsInstanceOffset(), count).GetUnsafeReadOnlyPtrT();
                var dst = ScatterValues.GetSubArray(index * ChunkCapacity, count).GetUnsafePtrT();
                UnsafeUtility.MemCpy(dst, src, count * UnsafeUtility.SizeOf<float4>());

                ChunkDataHeaders[index] = new PackedChunkUploadHeader(chunk);
            }
        }

        [BurstCompile(OptimizeFor = OptimizeFor.Performance, DisableSafetyChecks = true)]
        private struct ScatterLightmapSTJob : IJobFor
        {
            public const int BatchSize = 8;

            [ReadOnly] public NativeArray<ChunkIndex> Chunks;
            [ReadOnly] public NativeArray<float4> LightmapSTs;

            [WriteOnly] public NativeArray<PackedChunkUploadHeader> ChunkDataHeaders;
            [WriteOnly,NativeDisableParallelForRestriction] public NativeArray<float4> ScatterValues;

            public void Execute(int index)
            {
                var chunk = Chunks[index];
                var count = chunk.Count;

                var src = LightmapSTs.GetSubArray(chunk.AsInstanceOffset(), count).GetUnsafeReadOnlyPtrT();
                var dst = ScatterValues.GetSubArray(index * ChunkCapacity, count).GetUnsafePtrT();
                UnsafeUtility.MemCpy(dst, src, count * UnsafeUtility.SizeOf<float4>());

                ChunkDataHeaders[index] = new PackedChunkUploadHeader(chunk);
            }
        }

        [BurstCompile(OptimizeFor = OptimizeFor.Performance, DisableSafetyChecks = true)]
        private struct ScatterStaticMatricesJob : IJobFor
        {
            public const int BatchSize = 8;

            [ReadOnly] public NativeArray<ChunkIndex> Chunks;
            [ReadOnly] public NativeArray<GraphicsMatrix> InstanceLocalToWorld;

            [WriteOnly] public NativeArray<PackedChunkUploadHeader> ChunkDataHeaders;
            [WriteOnly,NativeDisableParallelForRestriction] public NativeArray<GraphicsMatrix> ScatterValues;

            public void Execute(int index)
            {
                var chunk = Chunks[index];
                var count = chunk.Count;

                var src = InstanceLocalToWorld.GetSubArray(chunk.AsInstanceOffset(), count).GetUnsafeReadOnlyPtrT();
                var dst = ScatterValues.GetSubArray(index * ChunkCapacity, count).GetUnsafePtrT();
                UnsafeUtility.MemCpy(dst, src, count * UnsafeUtility.SizeOf<GraphicsMatrix>());

                ChunkDataHeaders[index] = new PackedChunkUploadHeader(chunk);
            }
        }

        [BurstCompile(OptimizeFor = OptimizeFor.Performance, DisableSafetyChecks = true)]
        private struct ScatterInitDynamicMatricesJob : IJobFor
        {
            public const int BatchSize = 8;

            [ReadOnly] public NativeArray<ChunkIndex> Chunks;
            [ReadOnly] public NativeArray<GraphicsMatrix> InstanceLocalToWorld;
            [ReadOnly] public NativeArray<GraphicsMatrix> InstancePrevLocalToWorld;

            [WriteOnly] public NativeArray<PackedChunkUploadHeader> ChunkDataHeaders;
            [WriteOnly,NativeDisableParallelForRestriction] public NativeArray<GraphicsMatrix> ScatterValues;

            public void Execute(int index)
            {
                var chunk = Chunks[index];
                var count = chunk.Count;

                var srcCurr = InstanceLocalToWorld.GetSubArray(chunk.AsInstanceOffset(), count).GetUnsafeReadOnlyPtrT();
                var srcPrev = InstancePrevLocalToWorld.GetSubArray(chunk.AsInstanceOffset(), count).GetUnsafeReadOnlyPtrT();
                var dst = ScatterValues.GetSubArray(index * ChunkCapacity * 2, count * 2).GetUnsafePtrT();

                for (var indexInChunk = 0; indexInChunk < count; ++indexInChunk)
                {
                    dst[(indexInChunk * 2) + 0] = srcCurr[indexInChunk];
                    dst[(indexInChunk * 2) + 1] = srcPrev[indexInChunk];
                }

                ChunkDataHeaders[index] = new PackedChunkUploadHeader(chunk);
            }
        }

        [BurstCompile(OptimizeFor = OptimizeFor.Performance, DisableSafetyChecks = true)]
        private struct ScatterUpdateDynamicMatricesJob : IJobFor
        {
            public const int BatchSize = 8;

            [ReadOnly] public NativeArray<ChunkIndex> Chunks;
            [ReadOnly] public NativeArray<GraphicsMatrix> InstanceLocalToWorld;

            [WriteOnly] public NativeArray<PackedChunkUploadHeader> ChunkDataHeaders;
            [WriteOnly,NativeDisableParallelForRestriction] public NativeArray<GraphicsMatrix> ScatterValues;

            public void Execute(int index)
            {
                var chunk = Chunks[index];
                var count = chunk.Count;

                var src = InstanceLocalToWorld.GetSubArray(chunk.AsInstanceOffset(), count).GetUnsafeReadOnlyPtrT();
                var dst = ScatterValues.GetSubArray(index * ChunkCapacity, count).GetUnsafePtrT();
                UnsafeUtility.MemCpy(dst, src, count * UnsafeUtility.SizeOf<GraphicsMatrix>());

                ChunkDataHeaders[index] = new PackedChunkUploadHeader(chunk);
            }
        }

        [BurstCompile(OptimizeFor = OptimizeFor.Performance, DisableSafetyChecks = true)]
        private struct GatherProbePositionsJob : IJobFor
        {
            public const int BatchSize = 8;

            [ReadOnly] public NativeArray<ChunkIndex> Chunks;
            [ReadOnly] public NativeArray<GraphicsMatrix> InstanceLocalToWorld;

            [NativeDisableParallelForRestriction][WriteOnly] public NativeArray<Vector3> GatheredPositions;

            public void Execute(int index)
            {
                var chunk = Chunks[index];
                var count = chunk.Count;

                var localAnchorPoint = chunk.Archetype.Key.Template.LocalAnchorPoint;
                var localToWorldPtr = InstanceLocalToWorld.GetSubArray(chunk.AsInstanceOffset(), count).GetUnsafeReadOnlyPtrT();

                var gatherStartIndex = index * ChunkCapacity;
                var gatheredPositionsArray = GatheredPositions.GetSubArray(gatherStartIndex, count);
                for (var i = 0; i < count; i++)
                    gatheredPositionsArray[i] = math.transform(localToWorldPtr[i], localAnchorPoint);
            }
        }

        [BurstCompile(OptimizeFor = OptimizeFor.Performance, DisableSafetyChecks = true)]
        private struct ScatterProbeDataJob : IJobFor
        {
            public const int BatchSize = 8;

            [ReadOnly] public NativeArray<ChunkIndex> Chunks;
            [ReadOnly] public NativeArray<Vector3> QueryPositions;
            [ReadOnly] public LightProbesQuery LightProbesQuery;

            [WriteOnly] public NativeArray<PackedChunkUploadHeader> ChunkDataHeaders;
            [NativeDisableParallelForRestriction] public NativeArray<int> CompactTetrahedronCache;
            [NativeDisableParallelForRestriction][WriteOnly] public NativeArray<SphericalHarmonicsL2> ProbesSphericalHarmonics;
            [NativeDisableParallelForRestriction][WriteOnly] public NativeArray<Vector4> ProbesOcclusion;

            private static readonly int GPUSizeInBytes = UnsafeUtility.SizeOf<SHCoefficients>();

            public void Execute(int index)
            {
                var chunk = Chunks[index];
                var count = chunk.Count;

                var scatterStartIndex = index * ChunkCapacity;
                var queryPositionsArray = QueryPositions.GetSubArray(scatterStartIndex, count);
                var compactTetrahedronCacheArray = CompactTetrahedronCache.GetSubArray(scatterStartIndex, count);
                var probesSphericalHarmonicsArray = ProbesSphericalHarmonics.GetSubArray(scatterStartIndex, count);
                var probesOcclusionArray = ProbesOcclusion.GetSubArray(scatterStartIndex, count);

                LightProbesQuery.CalculateInterpolatedLightAndOcclusionProbes(queryPositionsArray, compactTetrahedronCacheArray, probesSphericalHarmonicsArray, probesOcclusionArray);
                ChunkDataHeaders[index] = new PackedChunkUploadHeader(chunk);
            }
        }

        [BurstCompile(OptimizeFor = OptimizeFor.Performance, DisableSafetyChecks = true)]
        private struct ScatterEntityIdsJob : IJobFor
        {
            public const int BatchSize = 8;

            [ReadOnly] public NativeArray<ChunkIndex> Chunks;
            [ReadOnly] public NativeArray<uint2> InstanceHandles;

            [WriteOnly] public NativeArray<PackedChunkUploadHeader> ChunkDataHeaders;
            [WriteOnly,NativeDisableParallelForRestriction] public NativeArray<uint2> ScatterValues;

            public void Execute(int index)
            {
                var chunk = Chunks[index];
                var count = chunk.Count;

                var src = InstanceHandles.GetSubArray(chunk.AsInstanceOffset(), count).GetUnsafeReadOnlyPtrT();
                var dst = ScatterValues.GetSubArray(index * ChunkCapacity, count).GetUnsafePtrT();
                UnsafeUtility.MemCpy(dst, src, count * UnsafeUtility.SizeOf<uint2>());

                ChunkDataHeaders[index] = new PackedChunkUploadHeader(chunk);
            }
        }

        #endregion

        #region Upload Types

        private struct ChunkLightProbeScatterData
        {
            public NativeArray<PackedChunkUploadHeader> ChunkHeaders;
            public NativeArray<SphericalHarmonicsL2> SH;
            public NativeArray<Vector4> Occlusion;
            public bool IsCreated => ChunkHeaders.IsCreated;
        }

        private BufferScatterData<T> NewChunkScatterData<T>(int chunkCount) where T : unmanaged
        {
            return new BufferScatterData<T>
            {
                ChunkHeaders = NewFrameArray<PackedChunkUploadHeader>(chunkCount),
                InstanceValues = NewFrameArray<T>(chunkCount * ChunkCapacity),
            };
        }

        #endregion

        #region Submit

        private static readonly ProfilerMarker SubmitToGpuMarker = new ProfilerMarker("Flora.SubmitToGpu");

        public void SubmitToGpu(BatchRendererGroup batchRendererGroup)
        {
            using var _ = SubmitToGpuMarker.Auto();

            // Always ensure layout is up to date
            m_InstanceBuffer.ValueRW.UpdateLayout(batchRendererGroup);

            // Schedule culling grid updates
            m_CullingGrid.ValueRW.ScheduleUploads();

            // Check if we have any uploads to do
            if (m_InstanceBuffer.ValueRW.ScheduleUpload(m_ContentVersion))
            {
                // Schedule all uploads
                ScheduleUploadsWithBurst(Self);

                // Light probe updates have to be separate because of the LightProbes API
                m_DataDependencies = JobHandle.CombineDependencies(m_DataDependencies, ScheduleUploadLightProbes(m_DataDependencies));
            }

            // Complete all upload jobs
            m_DataDependencies.Complete();

            // Execute uploads on GPU
            CommandBuffer cmd = CommandBufferPool.Get();
            {
                if (m_InstanceBuffer.ValueRO.IsUploadScheduled())
                {
                    // Dispatch instance data uploads
                    DispatchInstanceUploads(cmd);
                    m_InstanceBuffer.ValueRW.ApplyUpload();
                    m_ArchetypeDataDirty.Clear();
                    m_PendingInstanceUpload.Clear();
                    m_PendingTransformUpload.Clear();
                    m_PendingVariationColorUpload.Clear();
                    m_PendingLightmapSTUpload.Clear();
                }

                // Dispatch culling grid uploads
                m_CullingGrid.ValueRW.DispatchUploads(cmd);
            }
            Graphics.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        [BurstCompile]
        private static void ScheduleUploadsWithBurst(InstanceManager* data)
        {
            data->ScheduleUploadsInternal();
        }

        private void ScheduleUploadsInternal()
        {
            m_PendingTransformUpload.IntersectWith(m_ChunkEnabled);
            m_PendingInstanceUpload.IntersectWith(m_ChunkEnabled);
            m_PendingVariationColorUpload.IntersectWith(m_ChunkEnabled);
            m_PendingLightmapSTUpload.IntersectWith(m_ChunkEnabled);

            var staticTransformChanges = m_PendingTransformUpload.Clone(FrameAllocatorHandle);
            staticTransformChanges.IntersectWith(m_ChunkStatic);

            var dynamicInitTransformChanges = m_PendingTransformUpload.Clone(FrameAllocatorHandle);
            dynamicInitTransformChanges.IntersectWith(m_ChunkDynamic);

            var dynamicUpdateTransformChanges = dynamicInitTransformChanges.Clone(FrameAllocatorHandle);
            dynamicUpdateTransformChanges.ExceptWith(m_PendingInstanceUpload);
            dynamicInitTransformChanges.ExceptWith(dynamicUpdateTransformChanges);

            var scatterArchetypeData = ScheduleScatterArchetypeData(m_DataDependencies);
            var scatterInstanceInit = ScheduleScatterInstanceInit(m_PendingInstanceUpload, m_DataDependencies);
            var scatterVariationColors = ScheduleScatterVariationColors(m_PendingVariationColorUpload, m_DataDependencies);
            var scatterLightmapSTs = ScheduleScatterLightmapSTs(m_PendingLightmapSTUpload, m_DataDependencies);
            var scatterStaticMatrices = ScheduleScatterStaticMatrices(staticTransformChanges, m_DataDependencies);
            var scatterDynamicMatrices = ScheduleScatterInitDynamicMatrices(dynamicInitTransformChanges, dynamicUpdateTransformChanges, scatterStaticMatrices);

            m_DataDependencies = JobHandle.CombineDependencies(m_DataDependencies, scatterArchetypeData);
            m_DataDependencies = JobHandle.CombineDependencies(m_DataDependencies, scatterInstanceInit);
            m_DataDependencies = JobHandle.CombineDependencies(m_DataDependencies, scatterVariationColors);
            m_DataDependencies = JobHandle.CombineDependencies(m_DataDependencies, scatterLightmapSTs);
            m_DataDependencies = JobHandle.CombineDependencies(m_DataDependencies, scatterStaticMatrices);
            m_DataDependencies = JobHandle.CombineDependencies(m_DataDependencies, scatterDynamicMatrices);
        }

        private JobHandle ScheduleScatterArchetypeData(JobHandle inputDeps)
        {
            if (m_PendingArchetypeDataUploads.IsEmpty)
                return inputDeps;

            var archetypeIndices = m_PendingArchetypeDataUploads.Offsets.Reinterpret<ArchetypeIndex>();
            if (archetypeIndices.Length < ScatterArchetypeDataJob.BatchSize)
            {
                new ScatterArchetypeDataJob {
                    Archetypes = archetypeIndices,
                    PackedArchetypeData = m_PendingArchetypeDataUploads.Values,
                }.Run(archetypeIndices.Length);
                return inputDeps;
            }

            return new ScatterArchetypeDataJob {
                Archetypes = archetypeIndices,
                PackedArchetypeData = m_PendingArchetypeDataUploads.Values,
            }.ScheduleParallel(archetypeIndices.Length, ScatterArchetypeDataJob.BatchSize, inputDeps);
        }

        private JobHandle ScheduleScatterInstanceInit(NativeBitSet dirtyChunks, JobHandle inputDeps)
        {
            if (dirtyChunks.IsEmpty)
                return inputDeps;

            var chunksWithRandomValues = dirtyChunks.Clone(FrameAllocatorHandle);
            chunksWithRandomValues.IntersectWith(m_ChunkHasRandomValue);

            var uploadRandomIdsHandle = new JobHandle();
            if (!chunksWithRandomValues.IsEmpty)
            {
                var randomChunkIndices = chunksWithRandomValues.ToArray<ChunkIndex>(ref FrameAllocator);
                m_InstanceRandomIdScatterData = NewChunkScatterData<uint>(randomChunkIndices.Length);

                uploadRandomIdsHandle = new ScatterRandomIdsJob {
                    Chunks = randomChunkIndices,
                    RandomValues = m_InstanceRandomIDs,
                    ChunkDataHeaders = m_InstanceRandomIdScatterData.ChunkHeaders,
                    ScatterValues = m_InstanceRandomIdScatterData.InstanceValues.Reinterpret<float>(),
                }.ScheduleParallel(randomChunkIndices.Length, ScatterRandomIdsJob.BatchSize, inputDeps);
            }

#if UNITY_EDITOR
            var entityChunkIndices = dirtyChunks.ToArray<ChunkIndex>(ref FrameAllocator);
            m_InstanceEntityIdScatterData = NewChunkScatterData<uint2>(entityChunkIndices.Length);

            var uploadEntityIdsHandle =  new ScatterEntityIdsJob {
                Chunks = entityChunkIndices,
                InstanceHandles = m_InstanceHandles.Reinterpret<uint2>(),
                ChunkDataHeaders = m_InstanceEntityIdScatterData.ChunkHeaders,
                ScatterValues = m_InstanceEntityIdScatterData.InstanceValues,
            }.ScheduleParallel(entityChunkIndices.Length, ScatterEntityIdsJob.BatchSize, inputDeps);
            uploadRandomIdsHandle = JobHandle.CombineDependencies(uploadRandomIdsHandle, uploadEntityIdsHandle);
#endif

            return uploadRandomIdsHandle;
        }

        private JobHandle ScheduleScatterVariationColors(NativeBitSet dirtyChunks, JobHandle inputDeps)
        {
            if (dirtyChunks.IsEmpty)
                return inputDeps;

            var variationChunkIndices = dirtyChunks.ToArray<ChunkIndex>(ref FrameAllocator);
            m_InstanceColorVariationScatterData = NewChunkScatterData<uint4>(variationChunkIndices.Length);

            return new ScatterVariationColorJob {
                Chunks = variationChunkIndices,
                VariationColors = m_InstanceVariationColors,
                ChunkDataHeaders = m_InstanceColorVariationScatterData.ChunkHeaders,
                ScatterValues = m_InstanceColorVariationScatterData.InstanceValues.Reinterpret<float4>(),
            }.ScheduleParallel(variationChunkIndices.Length, ScatterVariationColorJob.BatchSize, inputDeps);
        }

        private JobHandle ScheduleScatterLightmapSTs(NativeBitSet dirtyChunks, JobHandle inputDeps)
        {
            if (dirtyChunks.IsEmpty)
                return inputDeps;

            var lightmapChunkIndices = dirtyChunks.ToArray<ChunkIndex>(ref FrameAllocator);
            m_InstanceLightmapSTScatterData = NewChunkScatterData<uint4>(lightmapChunkIndices.Length);

            return new ScatterLightmapSTJob {
                Chunks = lightmapChunkIndices,
                LightmapSTs = m_InstanceLightmapSTs,
                ChunkDataHeaders = m_InstanceLightmapSTScatterData.ChunkHeaders,
                ScatterValues = m_InstanceLightmapSTScatterData.InstanceValues.Reinterpret<float4>(),
            }.ScheduleParallel(lightmapChunkIndices.Length, ScatterLightmapSTJob.BatchSize, inputDeps);
        }

        private JobHandle ScheduleScatterStaticMatrices(NativeBitSet dirtyChunks, JobHandle inputDeps)
        {
            if (dirtyChunks.IsEmpty)
                return inputDeps;

            var staticChunkIndices = dirtyChunks.ToArray<ChunkIndex>(ref FrameAllocator);
            m_InstanceStaticMatrixScatterData = NewChunkScatterData<GraphicsMatrix>(staticChunkIndices.Length);

            return new ScatterStaticMatricesJob {
                Chunks = staticChunkIndices,
                InstanceLocalToWorld = m_InstanceLocalToWorld,
                ChunkDataHeaders = m_InstanceStaticMatrixScatterData.ChunkHeaders,
                ScatterValues = m_InstanceStaticMatrixScatterData.InstanceValues,
            }.ScheduleParallel(staticChunkIndices.Length, ScatterStaticMatricesJob.BatchSize, inputDeps);
        }

        private JobHandle ScheduleScatterInitDynamicMatrices(NativeBitSet initChunks, NativeBitSet updateChunks, JobHandle inputDeps)
        {
            if (initChunks.IsEmpty && updateChunks.IsEmpty)
                return inputDeps;

            JobHandle uploadHandle = default;
            JobHandle updateHandle = default;

            if (!initChunks.IsEmpty)
            {
                var initChunkIndices = initChunks.ToArray<ChunkIndex>(ref FrameAllocator);
                m_InstanceInitDynamicMatrixScatterData = new BufferScatterData<GraphicsMatrix>
                {
                    ChunkHeaders = NewFrameArray<PackedChunkUploadHeader>(initChunkIndices.Length),
                    InstanceValues = NewFrameArray<GraphicsMatrix>(initChunkIndices.Length * ChunkCapacity * 2), // 2 matrices per instance
                };

                uploadHandle = new ScatterInitDynamicMatricesJob {
                    Chunks = initChunkIndices,
                    InstanceLocalToWorld = m_InstanceLocalToWorld,
                    InstancePrevLocalToWorld = m_InstancePrevLocalToWorld,
                    ChunkDataHeaders = m_InstanceInitDynamicMatrixScatterData.ChunkHeaders,
                    ScatterValues = m_InstanceInitDynamicMatrixScatterData.InstanceValues,
                }.ScheduleParallel(initChunkIndices.Length, ScatterInitDynamicMatricesJob.BatchSize, inputDeps);
            }

            if (!updateChunks.IsEmpty)
            {
                var updateChunkIndices = updateChunks.ToArray<ChunkIndex>(ref FrameAllocator);
                m_InstanceUpdateDynamicMatrixScatterData = new BufferScatterData<GraphicsMatrix>
                {
                    ChunkHeaders = NewFrameArray<PackedChunkUploadHeader>(updateChunkIndices.Length),
                    InstanceValues = NewFrameArray<GraphicsMatrix>(updateChunkIndices.Length * ChunkCapacity),
                };

                updateHandle = new ScatterUpdateDynamicMatricesJob {
                    Chunks = updateChunkIndices,
                    InstanceLocalToWorld = m_InstanceLocalToWorld,
                    ChunkDataHeaders = m_InstanceUpdateDynamicMatrixScatterData.ChunkHeaders,
                    ScatterValues = m_InstanceUpdateDynamicMatrixScatterData.InstanceValues,
                }.ScheduleParallel(updateChunkIndices.Length, ScatterUpdateDynamicMatricesJob.BatchSize, inputDeps);
            }

            return JobHandle.CombineDependencies(uploadHandle, updateHandle);
        }

        private JobHandle ScheduleUploadLightProbes(JobHandle inputDeps)
        {
            if (!CullingUtility.SceneHasLightProbes())
                return default;

            if (!ForceLightProbeUpdate && (m_PendingTransformUpload.IsEmpty || m_ChunkHasProbes.IsEmpty))
                return default;

            var dirtyChunks = m_ChunkHasProbes.Clone(FrameAllocatorHandle);
            if (!ForceLightProbeUpdate)
                dirtyChunks.IntersectWith(m_PendingTransformUpload);

            if (dirtyChunks.IsEmpty)
            {
                ForceLightProbeUpdate = false;
                return default;
            }

            var probeChunkIndices = dirtyChunks.ToArray<ChunkIndex>(Allocator.TempJob);
            var probeMaxInstances = probeChunkIndices.Length * ChunkCapacity;
            var lightProbeQuery = new LightProbesQuery(Allocator.TempJob);
            var queryPositions = new NativeArray<Vector3>(probeMaxInstances, Allocator.TempJob);
            var compactTetrahedronCache = new NativeArray<int>(probeMaxInstances, Allocator.TempJob);

            var chunkDataHeaders = NewFrameArray<PackedChunkUploadHeader>(probeChunkIndices.Length);
            var probesSphericalHarmonics = NewFrameArray<SphericalHarmonicsL2>(probeMaxInstances);
            var probesOcclusion = NewFrameArray<Vector4>(probeMaxInstances);

            var gatherPositionsHandle = new GatherProbePositionsJob {
                Chunks = probeChunkIndices,
                InstanceLocalToWorld = m_InstanceLocalToWorld,
                GatheredPositions = queryPositions,
            }.ScheduleParallel(probeChunkIndices.Length, GatherProbePositionsJob.BatchSize, inputDeps);

            var scatterProbesHandle = new ScatterProbeDataJob {
                Chunks = probeChunkIndices,
                LightProbesQuery = lightProbeQuery,
                QueryPositions = queryPositions,
                ChunkDataHeaders = chunkDataHeaders,
                CompactTetrahedronCache = compactTetrahedronCache,
                ProbesSphericalHarmonics = probesSphericalHarmonics,
                ProbesOcclusion = probesOcclusion,
            }.ScheduleParallel(probeChunkIndices.Length, ScatterProbeDataJob.BatchSize, gatherPositionsHandle);

            compactTetrahedronCache.Dispose(scatterProbesHandle);
            queryPositions.Dispose(scatterProbesHandle);
            lightProbeQuery.Dispose(scatterProbesHandle);
            probeChunkIndices.Dispose(scatterProbesHandle);

            m_InstanceLightProbeScatterData = new ChunkLightProbeScatterData
            {
                ChunkHeaders = chunkDataHeaders,
                SH = probesSphericalHarmonics,
                Occlusion = probesOcclusion,
            };

            ForceLightProbeUpdate = false;
            return scatterProbesHandle;
        }

        #endregion

        #region Dispatch Uploads

        private void DispatchInstanceUploads(CommandBuffer cmd)
        {
            GraphicsBuffer instanceBuffer = m_InstanceBuffer.ValueRO.DataBuffer;

            m_ArchetypeDataBuffer.GrowIfNeeded(m_ArchetypeChunks.Length, growPolicy: GraphicsBufferGrowPolicy.WithSlack);

            if (!m_PendingArchetypeDataUploads.IsEmpty)
            {
                GraphicsBufferUtility.Scatter(cmd, m_ArchetypeDataBuffer, m_PendingArchetypeDataUploads.Values, m_PendingArchetypeDataUploads.Offsets);

                m_ArchetypeDataDirty.Clear();
                m_PendingArchetypeDataUploads.Clear();
            }

            if (m_InstanceRandomIdScatterData.IsCreated)
            {
                InstanceBufferUpload.ScatterUint(cmd, instanceBuffer, m_InstanceBuffer.ValueRO.DomainRandomIdAddresses,
                    m_InstanceRandomIdScatterData.ChunkHeaders, m_InstanceRandomIdScatterData.InstanceValues);
                m_InstanceRandomIdScatterData = default;
            }

#if UNITY_EDITOR
            if (m_InstanceEntityIdScatterData.IsCreated)
            {
                InstanceBufferUpload.ScatterUint2(cmd, instanceBuffer, m_InstanceBuffer.ValueRO.DomainEntityIdAddresses,
                    m_InstanceEntityIdScatterData.ChunkHeaders, m_InstanceEntityIdScatterData.InstanceValues);
                m_InstanceEntityIdScatterData = default;
            }
#endif

            if (m_InstanceColorVariationScatterData.IsCreated)
            {
                InstanceBufferUpload.ScatterUint4(cmd, instanceBuffer, m_InstanceBuffer.ValueRO.DomainVariationColorAddresses,
                    m_InstanceColorVariationScatterData.ChunkHeaders, m_InstanceColorVariationScatterData.InstanceValues);
                m_InstanceColorVariationScatterData = default;
            }

            if (m_InstanceLightmapSTScatterData.IsCreated)
            {
                InstanceBufferUpload.ScatterUint4(cmd, instanceBuffer, m_InstanceBuffer.ValueRO.DomainLightmapSTAddresses,
                    m_InstanceLightmapSTScatterData.ChunkHeaders, m_InstanceLightmapSTScatterData.InstanceValues);
                m_InstanceLightmapSTScatterData = default;
            }

            if (m_InstanceStaticMatrixScatterData.IsCreated)
            {
                InstanceBufferUpload.ScatterStaticTransforms(cmd, instanceBuffer, m_InstanceBuffer.ValueRO.DomainTransformAddresses,
                    m_InstanceStaticMatrixScatterData.ChunkHeaders, m_InstanceStaticMatrixScatterData.InstanceValues);
                m_InstanceStaticMatrixScatterData = default;
            }

            if (m_InstanceInitDynamicMatrixScatterData.IsCreated)
            {
                InstanceBufferUpload.ScatterInitDynamicTransforms(cmd, instanceBuffer, m_InstanceBuffer.ValueRO.DomainTransformAddresses,
                    m_InstanceInitDynamicMatrixScatterData.ChunkHeaders, m_InstanceInitDynamicMatrixScatterData.InstanceValues);

                m_InstanceInitDynamicMatrixScatterData = default;
            }

            if (m_InstanceUpdateDynamicMatrixScatterData.IsCreated)
            {
                InstanceBufferUpload.ScatterUpdateDynamicTransforms(cmd, instanceBuffer, m_InstanceBuffer.ValueRO.DomainTransformAddresses,
                    m_InstanceUpdateDynamicMatrixScatterData.ChunkHeaders, m_InstanceUpdateDynamicMatrixScatterData.InstanceValues);

                m_InstanceUpdateDynamicMatrixScatterData = default;
            }

            if (m_InstanceLightProbeScatterData.IsCreated)
            {
                InstanceBufferUpload.ScatterSH(cmd, instanceBuffer, m_InstanceBuffer.ValueRO.DomainSHCoefficientsAddresses,
                    m_InstanceLightProbeScatterData.ChunkHeaders, m_InstanceLightProbeScatterData.SH.Reinterpret<SHUpdatePacket>(), m_InstanceLightProbeScatterData.Occlusion);

                m_InstanceLightProbeScatterData = default;
            }
        }

        #endregion
    }
}
