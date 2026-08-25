// Copyright © Magnetic Arcade. All Rights Reserved.

using System;
using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;

namespace MA.Flora
{
    #region Detail Prototype

    internal readonly struct TerrainDetailPrototype : IEquatable<TerrainDetailPrototype>
    {
        public readonly EntityObjectRef<Terrain> Terrain;
        public readonly int Index;
        public readonly ushort MaxDistance;
        public readonly bool UsePrototypeMesh;
        public readonly DetailRenderMode RenderMode;
        public readonly EntityObjectRef<GameObject> Prototype;
        public readonly EntityObjectRef<Texture> PrototypeTexture;
        public readonly Color HealthyColor;
        public readonly Color DryColor;
        public readonly float3 Scale;
        public readonly float MinWidth;
        public readonly float MaxWidth;
        public readonly float MinHeight;
        public readonly float MaxHeight;
        public readonly int NoiseSeed;
        public readonly float NoiseSpread;
        public readonly float Density;
        public readonly float HoleEdgePadding;
        public readonly float TargetCoverage;
        public readonly bool UseDensityScaling;
        public readonly float AlignToGround;
        public readonly float PositionJitter;

        public bool IsValid() => Prototype.IsValid() || PrototypeTexture.IsValid();

        public TerrainDetailPrototype(Terrain terrain, DetailPrototype prototype, int index)
        {
            Terrain = terrain;
            Index = index;
            MaxDistance = (ushort)math.clamp(terrain.detailObjectDistance, 0, 65535);
            GameObject gameObject = prototype.usePrototypeMesh ? prototype.PrototypeRootGameObject() : null;
            UsePrototypeMesh = prototype.usePrototypeMesh;
            RenderMode = prototype.usePrototypeMesh ? DetailRenderMode.VertexLit : prototype.renderMode;
            Prototype = gameObject;
            PrototypeTexture = prototype.prototypeTexture;
            HealthyColor = prototype.healthyColor;
            DryColor = prototype.dryColor;
            Scale = gameObject ? gameObject.transform.localScale : Vector3.one;
            MinWidth = prototype.minWidth;
            MaxWidth = prototype.maxWidth;
            MinHeight = prototype.minHeight;
            MaxHeight = prototype.maxHeight;
            NoiseSeed = prototype.noiseSeed;
            NoiseSpread = prototype.noiseSpread;
            Density = prototype.density;
            HoleEdgePadding = prototype.holeEdgePadding;
            TargetCoverage = prototype.targetCoverage;
            UseDensityScaling = prototype.useDensityScaling;
            AlignToGround = prototype.alignToGround;
            PositionJitter = prototype.positionJitter;
        }

        public bool Equals(TerrainDetailPrototype other)
        {
            return Terrain.Equals(other.Terrain) &&
                   Index == other.Index &&
                   MaxDistance == other.MaxDistance &&
                   UsePrototypeMesh == other.UsePrototypeMesh &&
                   RenderMode == other.RenderMode &&
                   Prototype.Equals(other.Prototype) &&
                   PrototypeTexture.Equals(other.PrototypeTexture) &&
                   HealthyColor.Equals(other.HealthyColor) &&
                   DryColor.Equals(other.DryColor) &&
                   Scale.Equals(other.Scale) &&
                   MinWidth.Equals(other.MinWidth) &&
                   MaxWidth.Equals(other.MaxWidth) &&
                   MinHeight.Equals(other.MinHeight) &&
                   MaxHeight.Equals(other.MaxHeight) &&
                   NoiseSeed == other.NoiseSeed &&
                   NoiseSpread.Equals(other.NoiseSpread) &&
                   Density.Equals(other.Density) &&
                   HoleEdgePadding.Equals(other.HoleEdgePadding) &&
                   TargetCoverage.Equals(other.TargetCoverage) &&
                   UseDensityScaling == other.UseDensityScaling &&
                   AlignToGround.Equals(other.AlignToGround) &&
                   PositionJitter.Equals(other.PositionJitter);
        }

        public override int GetHashCode()
        {
            xxHash3.StreamingState hash = new xxHash3.StreamingState(true);
            hash.Update(Terrain);
            hash.Update(Index);
            hash.Update(MaxDistance);
            hash.Update(Prototype.Value);
            hash.Update(PrototypeTexture.Value);
            hash.Update(HealthyColor);
            hash.Update(DryColor);
            hash.Update(Scale);
            hash.Update(MinWidth);
            hash.Update(MaxWidth);
            hash.Update(MinHeight);
            hash.Update(MaxHeight);
            hash.Update(NoiseSeed);
            hash.Update(NoiseSpread);
            hash.Update(Density);
            hash.Update(HoleEdgePadding);
            hash.Update(UsePrototypeMesh);
            hash.Update(TargetCoverage);
            hash.Update(UseDensityScaling);
            hash.Update(AlignToGround);
            hash.Update(PositionJitter);
            return hash.DigestHash64().GetHashCode();
        }
    }

    #endregion

    #region Detail Layer

    internal struct TerrainDetailLayer : IDisposable
    {
        [BurstCompile(OptimizeFor = OptimizeFor.Performance, DisableSafetyChecks = true)]
        private struct BuildDetailInstancesJob : IJobParallelFor
        {
            public const int BatchSize = 256;

            [ReadOnly] public float3 TerrainPosition;
            [ReadOnly] public float AlignToGround;
            [ReadOnly] public float3 PrototypeScale;

            [ReadOnly] public NativeArray<DetailInstanceTransform> DetailTransforms;
            [ReadOnly] public NativeArray<float3> DetailNormals;

            [WriteOnly,NativeDisableContainerSafetyRestriction] public NativeArray<FloraLocalToWorld> InstanceLocalToWorld;

            public void Execute(int index)
            {
                DetailInstanceTransform detailTransform = DetailTransforms[index];
                float3 terrainNormal = AlignToGround > 0 ? DetailNormals[index] : math.up();

                float3 position = TerrainPosition + new float3(detailTransform.posX, detailTransform.posY, detailTransform.posZ);
                quaternion alignToTerrain = FromToRotation(math.up(), terrainNormal);
                quaternion yawRotation = quaternion.RotateY(detailTransform.rotationY);
                quaternion aligned = math.mul(alignToTerrain, yawRotation);
                quaternion rotation = math.slerp(yawRotation, aligned, AlignToGround);

                float3 scale = PrototypeScale * new float3(detailTransform.scaleXZ, detailTransform.scaleY, detailTransform.scaleXZ);
                InstanceLocalToWorld[index] = FloraLocalToWorld.FromPositionRotationScale(position, rotation, scale);
            }

            private static quaternion FromToRotation(float3 a, float3 b)
            {
                quaternion q;

                float d = math.dot(a, b);
                if (d < -0.999999)
                {
                    float3 t = math.cross(math.right(), a);
                    if (math.length(t) < math.FLT_MIN_NORMAL)
                        t = math.cross(math.up(), a);

                    t = math.normalize(t);
                    q = quaternion.AxisAngle(t, math.PI);
                }
                else if (d > 0.999999)
                {
                    q = quaternion.identity;
                }
                else
                {
                    float3 t = math.cross(a, b);
                    q = new quaternion(t.x, t.y, t.z, 1.0f + d);
                }

                return math.normalizesafe(q);
            }
        }

        public NativeDataReference<InstanceManager> InstanceData;
        public TerrainDetailPrototype Prototype;
        public int LayerIndex;
        public int PatchCountPerEdge;
        public int PatchCount;

        public NativeBufferArray<FloraInstanceHandle> InstancesPerPatch;
        public NativeBitSet PatchesDirty;

        public TerrainDetailLayer(NativeDataReference<InstanceManager> instanceManager, int layerIndex)
        {
            Prototype = default;
            LayerIndex = layerIndex;
            PatchCountPerEdge = 0;
            PatchCount = 0;
            InstancesPerPatch = new NativeBufferArray<FloraInstanceHandle>(0, 0, Allocator.Persistent);
            PatchesDirty = new NativeBitSet(0, Allocator.Persistent);
            InstanceData = instanceManager;
        }

        public void Dispose()
        {
            Reset();
            InstancesPerPatch.Dispose();
            PatchesDirty.Dispose();
            InstanceData = default;
        }

        public void SetEmpty()
        {
            Reset();
            InstancesPerPatch.Clear();
            PatchesDirty.Clear();
        }

        public void Reset()
        {
            int totalInstanceCount = 0;
            for (int i = 0; i < InstancesPerPatch.Length; i++)
                totalInstanceCount += InstancesPerPatch[i].Length;

            if (totalInstanceCount > 0)
            {
                var destroyHandles = new NativeArray<FloraInstanceHandle>(totalInstanceCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
                int destroyHandleIndex = 0;

                for (int i = 0; i < InstancesPerPatch.Length; i++)
                {
                    NativeArray<FloraInstanceHandle> patchInstances = InstancesPerPatch[i].AsArray();
                    for (int j = 0; j < patchInstances.Length; j++)
                        destroyHandles[destroyHandleIndex++] = patchInstances[j];
                }

                InstanceData.ValueRW.Destroy(destroyHandles);
                destroyHandles.Dispose();
            }

            for (int i = 0; i < InstancesPerPatch.Length; i++)
                InstancesPerPatch[i].Clear();
        }

        public void SetDirty()
        {
            PatchesDirty.AddRange(0, PatchCount);
        }

        public void SetDirty(int patchIndex)
        {
            PatchesDirty.Add(patchIndex);
        }

        public bool HasDirty(int patchIndex)
        {
            return PatchesDirty.Contains(patchIndex);
        }

        public void ClearDirty(int patchIndex)
        {
            PatchesDirty.Remove(patchIndex);
        }

        public bool ResizePatchesIfNeeded(int patchesPerEdge)
        {
            if (PatchCountPerEdge == patchesPerEdge)
                return false;

            Reset();
            InstancesPerPatch.Clear();

            PatchCount = patchesPerEdge * patchesPerEdge;
            PatchCountPerEdge = patchesPerEdge;
            InstancesPerPatch.Resize(PatchCount);

            PatchesDirty.AddRange(0, PatchCount);
            return true;
        }

        public void ClearPatchInstances(int patchIndex)
        {
            NativeBuffer<FloraInstanceHandle> instances = InstancesPerPatch[patchIndex];
            if (instances.Length > 0)
            {
                InstanceData.ValueRW.Destroy(instances.AsArray());
                instances.Clear();
            }
        }

        internal struct PatchBuildResult
        {
            public int PatchIndex;
            public int RemoveCount;
            public int AddCount;
            public int UpdateCount;
            public NativeArray<FloraLocalToWorld> LocalToWorlds;
            public JobHandle BuildHandle;
            public bool IsCreated => LocalToWorlds.IsCreated;

            public void Dispose(JobHandle inputDeps)
            {
                if (LocalToWorlds.IsCreated)
                    LocalToWorlds.Dispose(inputDeps);
            }
        }

        public PatchBuildResult ScheduleBuildPatch(in TerrainSnapshot terrain, int patchIndex)
        {
            if (!Prototype.IsValid())
                return default;

            int patchX = patchIndex % PatchCountPerEdge;
            int patchY = patchIndex / PatchCountPerEdge;
            float densityScale = Prototype.UseDensityScaling ? terrain.DetailDensity : 1.0f;
            NativeArray<DetailInstanceTransform> detailTransforms;
            using (TerrainDetailManager.ComputeDetailTransformsMarker.Auto())
                detailTransforms = terrain.ComputeDetailInstanceTransforms(patchX, patchY, LayerIndex, densityScale, Allocator.TempJob);

            if (detailTransforms.Length == 0)
            {
                detailTransforms.Dispose(default);

                int instanceCount = InstancesPerPatch[patchIndex].Length;
                if (instanceCount > 0)
                {
                    return new PatchBuildResult {
                        PatchIndex    = patchIndex,
                        RemoveCount   = instanceCount,
                        AddCount      = 0,
                        UpdateCount   = 0,
                        LocalToWorlds = new NativeArray<FloraLocalToWorld>(0, Allocator.TempJob),
                        BuildHandle   = default,
                    };
                }
                else
                {
                    // No instances before and after, nothing to do
                    return default;
                }
            }

            NativeArray<float3> detailNormals;
            if (Prototype.AlignToGround > 0)
            {
                using (TerrainDetailManager.SampleTerrainNormalsMarker.Auto())
                {
                    detailNormals = new NativeArray<float3>(detailTransforms.Length, Allocator.TempJob);
                    for (int i = 0; i < detailTransforms.Length; i++)
                    {
                        float2 uv = math.saturate(math.float2(detailTransforms[i].posX / terrain.Size.x, detailTransforms[i].posZ / terrain.Size.z));
                        detailNormals[i] = terrain.GetInterpolatedNormal(uv.x, uv.y);
                    }
                }
            }
            else
            {
                detailNormals = new NativeArray<float3>(0, Allocator.TempJob);
            }

            NativeBuffer<FloraInstanceHandle> instances = InstancesPerPatch[patchIndex];
            NativeArray<FloraLocalToWorld> localToWorlds = new NativeArray<FloraLocalToWorld>(detailTransforms.Length, Allocator.TempJob);
            JobHandle buildHandle = new BuildDetailInstancesJob {
                TerrainPosition = terrain.Position,
                AlignToGround = math.saturate(Prototype.AlignToGround),
                PrototypeScale = Prototype.Scale,
                DetailTransforms = detailTransforms,
                DetailNormals = detailNormals,
                InstanceLocalToWorld = localToWorlds
            }.Schedule(detailTransforms.Length, BuildDetailInstancesJob.BatchSize);

            detailTransforms.Dispose(buildHandle);
            detailNormals.Dispose(buildHandle);

            return new PatchBuildResult {
                PatchIndex    = patchIndex,
                RemoveCount   = math.max(0, instances.Length - detailTransforms.Length),
                AddCount      = math.max(0, detailTransforms.Length - instances.Length),
                UpdateCount   = math.min(instances.Length, localToWorlds.Length),
                LocalToWorlds = localToWorlds,
                BuildHandle   = buildHandle,
            };
        }

        public bool SetPrototype(TerrainDetailPrototype newPrototype)
        {
            if (Prototype.Equals(newPrototype))
                return false;

            Prototype = newPrototype;

            if (newPrototype.IsValid())
            {
                Reset();
                SetDirty();
            }
            else
                SetEmpty();

            return true;
        }
    }

    #endregion

    #region Detail Manager

    internal unsafe struct TerrainDetailManager : IDisposable
    {
        internal struct DetailStreamingFrameStats
        {
            public int PatchesLoaded;
            public int PatchesUnloaded;
            public int PatchesRebuilt;
            public int InstancesCreated;
            public int InstancesDestroyed;
            public int InstancesUpdated;
            public int StructuralApplyPhases;
        }

        [BurstCompile(OptimizeFor = OptimizeFor.Performance, DisableSafetyChecks = true)]
        private struct RasterizeSpheresJob : IJobParallelFor
        {
            [ReadOnly] public float3 TerrainOrigin;
            [ReadOnly] public float2 PatchSizeXZ;
            [ReadOnly] public int PatchCountPerEdge;
            [ReadOnly] public float DetailDistance;
            [ReadOnly] public NativeArray<float4> StreamingSpheres;

            public ParallelBitArray PatchesInRange;

            public void Execute(int sphereIndex)
            {
                float4 sphere = StreamingSpheres[sphereIndex];
                float3 sphereCenter = sphere.xyz;
                float sphereRadius = math.min(sphere.w, DetailDistance);
                float sphereRadiusSq = sphereRadius * sphereRadius;
                if (sphereRadiusSq <= 0f)
                    return;

                // Grow the sphere radius by half the patch diagonal to ensure all patches are included that might intersect the sphere
                float conservativeRadius = sphereRadius + math.length(PatchSizeXZ) * 0.5f;
                float2 minXZ = new float2(sphereCenter.x - conservativeRadius, sphereCenter.z - conservativeRadius);
                float2 maxXZ = new float2(sphereCenter.x + conservativeRadius, sphereCenter.z + conservativeRadius);

                int xMin = math.clamp((int)math.floor((minXZ.x - TerrainOrigin.x) / PatchSizeXZ.x),     0, PatchCountPerEdge - 1);
                int xMax = math.clamp((int)math.ceil ((maxXZ.x - TerrainOrigin.x) / PatchSizeXZ.x) - 1, 0, PatchCountPerEdge - 1);
                int zMin = math.clamp((int)math.floor((minXZ.y - TerrainOrigin.z) / PatchSizeXZ.y),     0, PatchCountPerEdge - 1);
                int zMax = math.clamp((int)math.ceil ((maxXZ.y - TerrainOrigin.z) / PatchSizeXZ.y) - 1, 0, PatchCountPerEdge - 1);

                for (int z = zMin; z <= zMax; z++)
                {
                    int rowStart = z * PatchCountPerEdge;
                    int x = xMin;

                    float patchMinZ = TerrainOrigin.z + z * PatchSizeXZ.y;
                    float patchMaxZ = patchMinZ + PatchSizeXZ.y;

                    while (x <= xMax)
                    {
                        int chunkIndex = (rowStart + x) >> 6;
                        int bitInChunk = (rowStart + x) & 63;

                        int count = math.min(64 - bitInChunk, (xMax - x + 1));
                        ulong inRange = 0ul;

                        float xBase = TerrainOrigin.x + x * PatchSizeXZ.x;
                        for (int i = 0; i < count; i++)
                        {
                            float patchMinX = xBase + i * PatchSizeXZ.x;
                            float patchMaxX = patchMinX + PatchSizeXZ.x;
                            float2 patchMin = new float2(patchMinX, patchMinZ);
                            float2 patchMax = new float2(patchMaxX, patchMaxZ);

                            if (CircleIntersectsAABB2D(sphereCenter.xz, sphereRadiusSq, patchMin, patchMax))
                                inRange |= 1ul << (bitInChunk + i);
                        }

                        PatchesInRange.InterlockedOrChunk(chunkIndex, inRange);
                        x += count;
                    }
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static bool CircleIntersectsAABB2D(float2 center, float radiusSq, float2 min, float2 max)
            {
                float2 q = math.clamp(center, min, max);
                float2 d = center - q;
                return math.dot(d, d) <= radiusSq;
            }
        }

        [BurstCompile(OptimizeFor = OptimizeFor.Performance, DisableSafetyChecks = true)]
        private struct ComputePatchLoadChangesJob : IJobParallelFor
        {
            [ReadOnly] public ParallelBitArray InRange;
            [ReadOnly] public ParallelBitArray Loaded;

            [WriteOnly] public ParallelBitArray ToLoad;
            [WriteOnly] public ParallelBitArray ToUnload;

            public void Execute(int chunkIndex)
            {
                ulong inRangeChunk = InRange.GetChunk(chunkIndex);
                ulong loadedChunk = Loaded.GetChunk(chunkIndex);

                ulong toLoadChunk = inRangeChunk & ~loadedChunk;
                ulong toUnloadChunk = loadedChunk & ~inRangeChunk;

                ToLoad.SetChunk(chunkIndex, toLoadChunk);
                ToUnload.SetChunk(chunkIndex, toUnloadChunk);
            }
        }

        internal static readonly ProfilerMarker ComputeDetailTransformsMarker = new("Flora.DetailManager.ComputeDetailTransforms");
        internal static readonly ProfilerMarker SampleTerrainNormalsMarker = new("Flora.DetailManager.SampleTerrainNormals");
        private static readonly ProfilerMarker UpdateMarker = new("Flora.DetailManager.Update");
        private static readonly ProfilerMarker UpdateLoadedPatchesMarker = new("Flora.DetailManager.UpdateLoadedPatches");
        private static readonly ProfilerMarker MarkPatchesInRangeMarker = new("Flora.DetailManager.MarkPatchesInRange");
        private static readonly ProfilerMarker BuildWaitMarker = new("Flora.DetailManager.WaitForBuildJobs");
        private static readonly ProfilerMarker ApplyDestroysMarker = new("Flora.DetailManager.DestroyInstances");
        private static readonly ProfilerMarker ApplyCreatesMarker = new("Flora.DetailManager.InstantiateInstances");
        private static readonly ProfilerMarker ScheduleLocalToWorldUpdatesMarker = new("Flora.DetailManager.ScheduleLocalToWorldUpdates");
        private static readonly ProfilerMarker ApplyStructuralPhaseMarker = new("Flora.DetailManager.ApplyStructuralPhase");
        private static IntProfilerCounter s_PatchesLoadedCounter;
        private static IntProfilerCounter s_PatchesUnloadedCounter;
        private static IntProfilerCounter s_PatchesRebuiltCounter;
        private static IntProfilerCounter s_InstancesCreatedCounter;
        private static IntProfilerCounter s_InstancesDestroyedCounter;
        private static IntProfilerCounter s_InstancesUpdatedCounter;
        private static IntProfilerCounter s_StructuralApplyPhaseCounter;

        private NativeDataReference<InstanceManager> m_InstanceManager;
        private NativeDataReference<StreamingSphereManager> m_StreamingManager;

        private UnsafeList<TerrainDetailLayer> m_Layers;
        private ParallelBitArray m_PatchesInRange;
        private ParallelBitArray m_PatchesLoaded;
        private ParallelBitArray m_PatchesToLoad;
        private ParallelBitArray m_PatchesToUnload;
        private NativeArray<float> m_PatchesOutOfRangeTime;
        private NativeArray<int> m_PatchNextLayerIndex;
        private NativeBitSet m_PatchesQueuedForWork;
        private NativeList<int> m_WorkQueue;
        private int m_WorkQueueHead;
        private int m_PatchCount;
        private bool m_ScheduledMarkInRangeJob;
        private bool m_WasWithinDetailsRange;
        private bool m_Hidden;

        public TerrainDetailManager(InstanceContext instanceContext)
        {
            m_InstanceManager = instanceContext.InstanceManager;
            m_StreamingManager = instanceContext.StreamingManager;

            m_Layers = new UnsafeList<TerrainDetailLayer>(0, Allocator.Persistent);
            m_PatchesInRange = new ParallelBitArray(0, Allocator.Persistent);
            m_PatchesLoaded = new ParallelBitArray(0, Allocator.Persistent);
            m_PatchesToLoad = new ParallelBitArray(0, Allocator.Persistent);
            m_PatchesToUnload = new ParallelBitArray(0, Allocator.Persistent);
            m_PatchesOutOfRangeTime = new NativeArray<float>(0, Allocator.Persistent);
            m_PatchNextLayerIndex = new NativeArray<int>(0, Allocator.Persistent);
            m_PatchesQueuedForWork = new NativeBitSet(0, Allocator.Persistent);
            m_WorkQueue = new NativeList<int>(0, Allocator.Persistent);
            m_WorkQueueHead = 0;
            m_PatchCount = 0;
            m_ScheduledMarkInRangeJob = false;
            m_WasWithinDetailsRange = false;
            m_Hidden = false;
        }

        [BurstDiscard]
        private static void EnsureProfilerCountersCreated()
        {
            if (s_PatchesLoadedCounter.IsCreated)
                return;

            s_PatchesLoadedCounter = new IntProfilerCounter("Flora.DetailManager.PatchesLoaded", ProfilerMarkerDataUnit.Count);
            s_PatchesUnloadedCounter = new IntProfilerCounter("Flora.DetailManager.PatchesUnloaded", ProfilerMarkerDataUnit.Count);
            s_PatchesRebuiltCounter = new IntProfilerCounter("Flora.DetailManager.PatchesRebuilt", ProfilerMarkerDataUnit.Count);
            s_InstancesCreatedCounter = new IntProfilerCounter("Flora.DetailManager.InstancesCreated", ProfilerMarkerDataUnit.Count);
            s_InstancesDestroyedCounter = new IntProfilerCounter("Flora.DetailManager.InstancesDestroyed", ProfilerMarkerDataUnit.Count);
            s_InstancesUpdatedCounter = new IntProfilerCounter("Flora.DetailManager.InstancesUpdated", ProfilerMarkerDataUnit.Count);
            s_StructuralApplyPhaseCounter = new IntProfilerCounter("Flora.DetailManager.StructuralApplyPhases", ProfilerMarkerDataUnit.Count);
        }

        [BurstDiscard]
        internal static void PublishProfilerCounters(in DetailStreamingFrameStats stats)
        {
            EnsureProfilerCountersCreated();

            s_PatchesLoadedCounter.Sample(stats.PatchesLoaded);
            s_PatchesUnloadedCounter.Sample(stats.PatchesUnloaded);
            s_PatchesRebuiltCounter.Sample(stats.PatchesRebuilt);
            s_InstancesCreatedCounter.Sample(stats.InstancesCreated);
            s_InstancesDestroyedCounter.Sample(stats.InstancesDestroyed);
            s_InstancesUpdatedCounter.Sample(stats.InstancesUpdated);
            s_StructuralApplyPhaseCounter.Sample(stats.StructuralApplyPhases);
        }

        private static int GetRemainingBudget(int budgetPerFrame, int usedCount)
        {
            return budgetPerFrame > 0 ? math.max(0, budgetPerFrame - usedCount) : int.MaxValue;
        }

        internal static bool ShouldDeferStructuralWork(
            int structuralBudgetPerFrame,
            int remainingStructuralBudgetForBuild,
            int structuralCost,
            int structuralInstancesUsed,
            int scheduledStructuralCost)
        {
            if (structuralBudgetPerFrame <= 0 || structuralCost <= 0 || structuralCost <= remainingStructuralBudgetForBuild)
                return false;

            bool isFirstStructuralWorkItem = structuralInstancesUsed == 0 && scheduledStructuralCost == 0;
            return !isFirstStructuralWorkItem;
        }

        public void Dispose()
        {
            for (int layer = 0; layer < m_Layers.Length; layer++)
                m_Layers.Ptr[layer].Dispose();

            m_Layers.Dispose();
            m_PatchesInRange.Dispose();
            m_PatchesLoaded.Dispose();
            m_PatchesToLoad.Dispose();
            m_PatchesToUnload.Dispose();
            m_PatchesOutOfRangeTime.Dispose();
            m_PatchNextLayerIndex.Dispose();
            m_PatchesQueuedForWork.Dispose();
            m_WorkQueue.Dispose();
        }

        private void ResetTrackingState()
        {
            m_PatchesInRange.FillZeroes();
            m_PatchesLoaded.FillZeroes();
            m_PatchesToLoad.FillZeroes();
            m_PatchesToUnload.FillZeroes();

            if (m_PatchesOutOfRangeTime.IsCreated)
                m_PatchesOutOfRangeTime.Fill(0f);
            if (m_PatchNextLayerIndex.IsCreated)
                m_PatchNextLayerIndex.Fill(0);

            m_PatchesQueuedForWork.Clear();
            m_WorkQueue.Clear();
            m_WorkQueueHead = 0;
            m_ScheduledMarkInRangeJob = false;
        }

        private void ClearQueuedPatchWork()
        {
            m_PatchesQueuedForWork.Clear();
            m_WorkQueue.Clear();
            m_WorkQueueHead = 0;
        }

        private bool PatchHasDirtyLayers(int patchIndex)
        {
            for (int i = 0; i < m_Layers.Length; i++)
            {
                if (m_Layers.Ptr[i].HasDirty(patchIndex))
                    return true;
            }

            return false;
        }

        private void ClearPatchDirtyState(int patchIndex)
        {
            for (int i = 0; i < m_Layers.Length; i++)
                m_Layers.Ptr[i].ClearDirty(patchIndex);

            m_PatchNextLayerIndex[patchIndex] = 0;
            m_PatchesQueuedForWork.Remove(patchIndex);
        }

        private void EnqueuePatchForWork(int patchIndex)
        {
            if (patchIndex < 0 || patchIndex >= m_PatchCount)
                return;

            if (m_PatchesQueuedForWork.TryAdd(patchIndex))
            {
                if (m_WorkQueueHead > 4096 && m_WorkQueueHead * 2 >= m_WorkQueue.Length)
                    CompactWorkQueue();

                m_WorkQueue.Add(patchIndex);
            }
        }

        private void MarkPatchDirty(int patchIndex)
        {
            if (patchIndex < 0 || patchIndex >= m_PatchCount)
                return;

            bool hasValidLayer = false;
            for (int i = 0; i < m_Layers.Length; i++)
            {
                ref TerrainDetailLayer layer = ref m_Layers.Ptr[i];
                if (!layer.Prototype.IsValid())
                {
                    layer.ClearDirty(patchIndex);
                    continue;
                }

                layer.SetDirty(patchIndex);
                hasValidLayer = true;
            }

            if (!hasValidLayer)
            {
                ClearPatchDirtyState(patchIndex);
                return;
            }

            m_PatchNextLayerIndex[patchIndex] = 0;
            EnqueuePatchForWork(patchIndex);
        }

        private void QueueLoadedDirtyPatches()
        {
            if (m_PatchCount == 0)
                return;

            foreach (int patchIndex in m_PatchesLoaded.SetBitEnumerator())
            {
                if (PatchHasDirtyLayers(patchIndex))
                    EnqueuePatchForWork(patchIndex);
            }
        }

        private bool TryTakeNextDirtyLayer(int patchIndex, out int layerIndex)
        {
            int layerCount = m_Layers.Length;
            int nextLayerIndex = m_PatchNextLayerIndex[patchIndex];

            for (int layerOffset = 0; layerOffset < layerCount; layerOffset++)
            {
                int candidateLayerIndex = (nextLayerIndex + layerOffset) % layerCount;
                ref TerrainDetailLayer layer = ref m_Layers.Ptr[candidateLayerIndex];
                if (!layer.Prototype.IsValid())
                {
                    layer.ClearDirty(patchIndex);
                    continue;
                }

                if (!layer.HasDirty(patchIndex))
                    continue;

                layer.ClearDirty(patchIndex);
                m_PatchNextLayerIndex[patchIndex] = candidateLayerIndex + 1;
                if (m_PatchNextLayerIndex[patchIndex] >= layerCount)
                    m_PatchNextLayerIndex[patchIndex] = 0;

                layerIndex = candidateLayerIndex;
                return true;
            }

            m_PatchNextLayerIndex[patchIndex] = 0;
            layerIndex = -1;
            return false;
        }

        private void CompactWorkQueue()
        {
            if (m_WorkQueueHead == 0)
                return;

            if (m_WorkQueueHead < 128 && m_WorkQueueHead * 2 < m_WorkQueue.Length)
                return;

            int remainingCount = m_WorkQueue.Length - m_WorkQueueHead;
            for (int i = 0; i < remainingCount; i++)
                m_WorkQueue[i] = m_WorkQueue[m_WorkQueueHead + i];

            m_WorkQueue.Resize(remainingCount, NativeArrayOptions.UninitializedMemory);
            m_WorkQueueHead = 0;
        }

        private bool TryDequeuePatchForWork(out int patchIndex)
        {
            while (m_WorkQueueHead < m_WorkQueue.Length)
            {
                int candidatePatchIndex = m_WorkQueue[m_WorkQueueHead++];
                m_PatchesQueuedForWork.Remove(candidatePatchIndex);

                if (!m_PatchesLoaded.Get(candidatePatchIndex))
                {
                    ClearPatchDirtyState(candidatePatchIndex);
                    continue;
                }

                if (!PatchHasDirtyLayers(candidatePatchIndex))
                    continue;

                patchIndex = candidatePatchIndex;
                return true;
            }

            m_WorkQueue.Clear();
            m_WorkQueueHead = 0;
            patchIndex = -1;
            return false;
        }

        private static int GetStructuralCost(in TerrainDetailLayer.PatchBuildResult result)
        {
            return result.AddCount + result.RemoveCount;
        }

        private int GetPatchInstanceCount(int patchIndex)
        {
            int patchInstanceCount = 0;
            for (int layerIndex = 0; layerIndex < m_Layers.Length; layerIndex++)
                patchInstanceCount += m_Layers.Ptr[layerIndex].InstancesPerPatch[patchIndex].Length;

            return patchInstanceCount;
        }

        private void ApplyQueuedUnloads(
            NativeList<int> patchesToUnload,
            in TerrainSystemSettings settings,
            ref int unloadedCells,
            ref int structuralInstancesUsed,
            ref DetailStreamingFrameStats stats)
        {
            if (patchesToUnload.Length == 0)
                return;

            int remainingStructuralBudget = GetRemainingBudget(settings.DetailStructuralInstanceBudgetPerFrame, structuralInstancesUsed);

            using var selectedPatches = new NativeList<int>(Allocator.Temp);
            int selectedDestroyCount = 0;
            for (int patchListIndex = 0; patchListIndex < patchesToUnload.Length; patchListIndex++)
            {
                if (remainingStructuralBudget == 0)
                    break;

                int patchIndex = patchesToUnload[patchListIndex];
                int patchDestroyCount = GetPatchInstanceCount(patchIndex);
                if (settings.DetailStructuralInstanceBudgetPerFrame > 0 &&
                    patchDestroyCount > 0 &&
                    selectedPatches.Length > 0 &&
                    selectedDestroyCount + patchDestroyCount > remainingStructuralBudget)
                {
                    break;
                }

                selectedPatches.Add(patchIndex);
                selectedDestroyCount += patchDestroyCount;
            }

            if (selectedPatches.Length == 0)
                return;

            if (selectedDestroyCount > 0)
            {
                using var _ = ApplyDestroysMarker.Auto();
                var destroyHandles = new NativeArray<FloraInstanceHandle>(selectedDestroyCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
                int destroyHandleIndex = 0;

                for (int layerIndex = 0; layerIndex < m_Layers.Length; layerIndex++)
                {
                    TerrainDetailLayer layer = m_Layers.Ptr[layerIndex];
                    for (int patchListIndex = 0; patchListIndex < selectedPatches.Length; patchListIndex++)
                    {
                        NativeArray<FloraInstanceHandle> patchInstances = layer.InstancesPerPatch[selectedPatches[patchListIndex]].AsArray();
                        for (int instanceIndex = 0; instanceIndex < patchInstances.Length; instanceIndex++)
                            destroyHandles[destroyHandleIndex++] = patchInstances[instanceIndex];
                    }
                }

                m_InstanceManager.ValueRW.Destroy(destroyHandles);
                destroyHandles.Dispose();
                stats.InstancesDestroyed += selectedDestroyCount;
            }

            for (int patchListIndex = 0; patchListIndex < selectedPatches.Length; patchListIndex++)
            {
                int patchIndex = selectedPatches[patchListIndex];
                m_PatchesLoaded.Set(patchIndex, false);
                ClearPatchDirtyState(patchIndex);
                for (int layerIndex = 0; layerIndex < m_Layers.Length; layerIndex++)
                    m_Layers.Ptr[layerIndex].InstancesPerPatch[patchIndex].Clear();

                m_PatchesOutOfRangeTime[patchIndex] = 0f;
                stats.PatchesUnloaded++;
                unloadedCells++;
            }

            structuralInstancesUsed += selectedDestroyCount;
        }

        private void Clear()
        {
            for (int i = 0; i < m_Layers.Length; i++)
            {
                m_Layers.Ptr[i].Reset();
                m_Layers.Ptr[i].SetDirty();
            }

            ResetTrackingState();
        }

        private void SetEmpty()
        {
            for (int i = 0; i < m_Layers.Length; i++)
                m_Layers.Ptr[i].SetEmpty();

            ResetTrackingState();
        }

        private void UpdateLayerPrototypes(NativeArray<TerrainDetailPrototype> prototypes)
        {
            int oldLayerCount = m_Layers.Length;
            if (oldLayerCount > prototypes.Length)
            {
                for (int i = prototypes.Length; i < oldLayerCount; i++)
                    m_Layers.Ptr[i].Dispose();
            }

            m_Layers.Resize(prototypes.Length);

            if (prototypes.Length > oldLayerCount)
            {
                for (int i = oldLayerCount; i < prototypes.Length; i++)
                    m_Layers[i] = new TerrainDetailLayer(m_InstanceManager, i);
            }

            bool anyPrototypeChanged = false;
            for (int i = 0; i < prototypes.Length; i++)
                anyPrototypeChanged |= m_Layers.Ptr[i].SetPrototype(prototypes[i]);

            if (anyPrototypeChanged)
                QueueLoadedDirtyPatches();
        }

        private void ResizePatchesIfNeeded(int patchCountPerEdge, float3 terrainPosition, float3 terrainSize)
        {
            for (int i = 0; i < m_Layers.Length; i++)
                m_Layers.Ptr[i].ResizePatchesIfNeeded(patchCountPerEdge);

            int neededCount = patchCountPerEdge * patchCountPerEdge;
            if (neededCount == m_PatchCount)
                return;

            m_PatchCount = neededCount;
            if (m_PatchCount == 0)
            {
                ResetTrackingState();
                return;
            }

            m_PatchesInRange.Resize(m_PatchCount);
            m_PatchesInRange.FillZeroes();

            m_PatchesLoaded.Resize(m_PatchCount);
            m_PatchesLoaded.FillZeroes();

            m_PatchesToLoad.Resize(m_PatchCount);
            m_PatchesToLoad.FillZeroes();

            m_PatchesToUnload.Resize(m_PatchCount);
            m_PatchesToUnload.FillZeroes();

            m_PatchesOutOfRangeTime.ResizeArraySafe(m_PatchCount);
            m_PatchesOutOfRangeTime.Fill(0f);

            m_PatchNextLayerIndex.ResizeArraySafe(m_PatchCount);
            m_PatchNextLayerIndex.Fill(0);

            m_PatchesQueuedForWork.Dispose();
            m_PatchesQueuedForWork = new NativeBitSet(m_PatchCount, Allocator.Persistent);
            m_WorkQueue.Clear();
            m_WorkQueueHead = 0;
            m_ScheduledMarkInRangeJob = false;
        }

        public void SetDirty()
        {
            foreach (int patchIndex in m_PatchesLoaded.SetBitEnumerator())
                MarkPatchDirty(patchIndex);
        }

        public void SetDirty(TerrainChangedFlags flags)
        {
            if ((flags & TerrainChangedFlags.DelayedHeightmapUpdate) != 0 ||
                (flags & TerrainChangedFlags.Heightmap) != 0 ||
                (flags & TerrainChangedFlags.HeightmapResolution) != 0 ||
                (flags & TerrainChangedFlags.RemoveDirtyDetailsImmediately) != 0 ||
                (flags & TerrainChangedFlags.FlushEverythingImmediately) != 0 ||
                (flags & TerrainChangedFlags.Holes) != 0 ||
                (flags & TerrainChangedFlags.DelayedHolesUpdate) != 0)
            {
                SetDirty();
            }
        }

        private int ApplyCreatesAndDestroysOnMainThread(
            ref TerrainDetailLayer layer,
            in TerrainSnapshot terrain,
            NativeList<TerrainDetailLayer.PatchBuildResult> patchResults,
            ref DetailStreamingFrameStats stats)
        {
            int totalRemoveCount = 0;
            int totalAddCount = 0;
            for (int i = 0; i < patchResults.Length; i++)
            {
                totalRemoveCount += patchResults[i].RemoveCount;
                totalAddCount += patchResults[i].AddCount;
            }

            if (totalRemoveCount > 0)
            {
                using var _ = ApplyDestroysMarker.Auto();
                var destroyHandles = new NativeArray<FloraInstanceHandle>(totalRemoveCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
                int destroyHandleIndex = 0;

                for (int i = 0; i < patchResults.Length; i++)
                {
                    TerrainDetailLayer.PatchBuildResult result = patchResults[i];
                    if (result.RemoveCount <= 0)
                        continue;

                    NativeBuffer<FloraInstanceHandle> instances = layer.InstancesPerPatch[result.PatchIndex];
                    int newCount = instances.Length - result.RemoveCount;
                    NativeArray<FloraInstanceHandle> patchInstances = instances.AsArray();
                    for (int removeIndex = 0; removeIndex < result.RemoveCount; removeIndex++)
                        destroyHandles[destroyHandleIndex++] = patchInstances[newCount + removeIndex];
                }

                layer.InstanceData.ValueRW.Destroy(destroyHandles);
                destroyHandles.Dispose();

                for (int i = 0; i < patchResults.Length; i++)
                {
                    TerrainDetailLayer.PatchBuildResult result = patchResults[i];
                    if (result.RemoveCount > 0)
                        layer.InstancesPerPatch[result.PatchIndex].Resize(result.UpdateCount);
                }

                stats.InstancesDestroyed += totalRemoveCount;
            }

            if (totalAddCount > 0)
            {
                using var _ = ApplyCreatesMarker.Auto();
                var newHandles = new NativeArray<FloraInstanceHandle>(totalAddCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
                var newMatrices = new NativeArray<FloraLocalToWorld>(totalAddCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
                int addHandleIndex = 0;

                for (int i = 0; i < patchResults.Length; i++)
                {
                    TerrainDetailLayer.PatchBuildResult result = patchResults[i];
                    if (result.AddCount <= 0)
                        continue;

                    for (int addIndex = 0; addIndex < result.AddCount; addIndex++)
                        newMatrices[addHandleIndex + addIndex] = result.LocalToWorlds[result.UpdateCount + addIndex];

                    addHandleIndex += result.AddCount;
                }

                TerrainDetailLayer* layerPtr = (TerrainDetailLayer*)UnsafeUtility.AddressOf(ref layer);
                layer.InstanceData.ValueRW.InstantiateDetailsFromBurst(terrain.Entity, &layerPtr->Prototype, newHandles, newMatrices);

                addHandleIndex = 0;
                for (int i = 0; i < patchResults.Length; i++)
                {
                    TerrainDetailLayer.PatchBuildResult result = patchResults[i];
                    if (result.AddCount <= 0)
                        continue;

                    NativeBuffer<FloraInstanceHandle> instances = layer.InstancesPerPatch[result.PatchIndex];
                    int oldCount = instances.Length;
                    instances.Resize(oldCount + result.AddCount, NativeArrayOptions.UninitializedMemory);

                    NativeArray<FloraInstanceHandle> patchInstances = instances.AsArray();
                    for (int addIndex = 0; addIndex < result.AddCount; addIndex++)
                        patchInstances[oldCount + addIndex] = newHandles[addHandleIndex + addIndex];

                    addHandleIndex += result.AddCount;
                }

                newHandles.Dispose();
                newMatrices.Dispose();
                stats.InstancesCreated += totalAddCount;
            }

            return totalRemoveCount + totalAddCount;
        }

        private JobHandle ScheduleUpdates(
            in TerrainDetailLayer layer,
            in TerrainDetailLayer.PatchBuildResult result,
            JobHandle inputDeps = default)
        {
            if (result.UpdateCount <= 0)
                return inputDeps;

            NativeBuffer<FloraInstanceHandle> instances = layer.InstancesPerPatch[result.PatchIndex];
            NativeArray<FloraInstanceHandle> instancesToUpdate = instances.AsArray().GetSubArray(0, result.UpdateCount);
            NativeArray<FloraLocalToWorld> matricesToUpdate = result.LocalToWorlds.GetSubArray(0, result.UpdateCount);
            return layer.InstanceData.ValueRW.ScheduleUpdateLocalToWorlds(instancesToUpdate, matricesToUpdate, inputDeps);
        }

        public JobHandle ScheduleUpdate(
            in TerrainSnapshot terrain,
            TerrainSystemSettings settings,
            ref int builtCells,
            ref int unloadedCells,
            ref int structuralInstancesUsed,
            ref DetailStreamingFrameStats stats)
        {
            using ProfilerMarker.AutoScope _ = UpdateMarker.Auto();

            if (terrain.DetailPrototypes.Length == 0)
            {
                SetEmpty();
                return default;
            }

            UpdateLayerPrototypes(terrain.DetailPrototypes);
            ResizePatchesIfNeeded(terrain.DetailPatchCount, terrain.Position, terrain.Size);

            if (terrain.WithinDetailsRange)
            {
                if (!m_WasWithinDetailsRange)
                    QueueLoadedDirtyPatches();
            }
            else if (m_WasWithinDetailsRange)
            {
                ClearQueuedPatchWork();
            }

            m_WasWithinDetailsRange = terrain.WithinDetailsRange;
            using var patchesToUnload = new NativeList<int>(Allocator.Temp);
            UpdateLoadAndUnloadQueues(terrain.WithinDetailsRange, settings, patchesToUnload, ref stats);

            JobHandle buildLayersHandle = (terrain.WithinDetailsRange || patchesToUnload.Length > 0)
                ? ScheduleQueuedPatchWork(terrain, terrain.WithinDetailsRange, settings, ref builtCells, ref unloadedCells, ref structuralInstancesUsed, patchesToUnload, ref stats)
                : default;
            JobHandle markInRangeHandle = ScheduleMarkPatchesInRange(terrain);
            JobHandle buildHandle = JobHandle.CombineDependencies(buildLayersHandle, markInRangeHandle);

#if UNITY_EDITOR
            if (m_Hidden)
                SetHidden();
#endif

            return buildHandle;
        }

        private void UpdateLoadAndUnloadQueues(
            bool allowLoads,
            in TerrainSystemSettings settings,
            NativeList<int> patchesToUnload,
            ref DetailStreamingFrameStats stats)
        {
            using var _ = UpdateLoadedPatchesMarker.Auto();
            if (!m_ScheduledMarkInRangeJob)
                return;

            if (allowLoads)
            {
                foreach (int patchIndex in m_PatchesInRange.SetBitEnumerator())
                    m_PatchesOutOfRangeTime[patchIndex] = 0f;

                foreach (int patchIndex in m_PatchesToLoad.SetBitEnumerator())
                {
                    m_PatchesOutOfRangeTime[patchIndex] = 0f;
                    if (m_PatchesLoaded.Get(patchIndex))
                        continue;

                    m_PatchesLoaded.Set(patchIndex, true);
                    MarkPatchDirty(patchIndex);
                    stats.PatchesLoaded++;
                }
            }

            foreach (int patchIndex in m_PatchesToUnload.SetBitEnumerator())
            {
                m_PatchesOutOfRangeTime[patchIndex] += settings.DetailStreamingDeltaTime;
                if (m_PatchesOutOfRangeTime[patchIndex] < settings.DetailUnloadHysteresisSeconds)
                    continue;

                if (m_PatchesLoaded.Get(patchIndex))
                    patchesToUnload.Add(patchIndex);
            }

            m_PatchesToLoad.FillZeroes();
            m_PatchesToUnload.FillZeroes();
            m_ScheduledMarkInRangeJob = false;
        }

        private JobHandle ScheduleQueuedPatchWork(
            in TerrainSnapshot terrain,
            bool allowBuilds,
            in TerrainSystemSettings settings,
            ref int builtCells,
            ref int unloadedCells,
            ref int structuralInstancesUsed,
            NativeList<int> patchesToUnload,
            ref DetailStreamingFrameStats stats)
        {
            int remainingBuildBudget = GetRemainingBudget(settings.DetailPatchLayerBudgetPerFrame, builtCells);
            bool unlimitedBuildBudget = settings.DetailPatchLayerBudgetPerFrame <= 0;
            int scheduledStructuralCost = 0;

            using var buildHandles = new NativeList<JobHandle>(Allocator.Temp);
            using var resultLayerIndices = new NativeList<int>(Allocator.Temp);
            using var patchResults = new NativeList<TerrainDetailLayer.PatchBuildResult>(Allocator.Temp);

            while (allowBuilds && (unlimitedBuildBudget || remainingBuildBudget > 0))
            {
                int remainingStructuralBudgetForBuild = GetRemainingBudget(settings.DetailStructuralInstanceBudgetPerFrame, structuralInstancesUsed + scheduledStructuralCost);
                if (remainingStructuralBudgetForBuild == 0)
                    break;

                if (!TryDequeuePatchForWork(out int patchIndex))
                    break;

                if (!TryTakeNextDirtyLayer(patchIndex, out int layerIndex))
                    continue;

                ref TerrainDetailLayer layer = ref m_Layers.Ptr[layerIndex];
                TerrainDetailLayer.PatchBuildResult buildResult = layer.ScheduleBuildPatch(terrain, patchIndex);

                if (!buildResult.IsCreated)
                {
                    if (PatchHasDirtyLayers(patchIndex))
                        EnqueuePatchForWork(patchIndex);
                    continue;
                }

                int structuralCost = GetStructuralCost(buildResult);
                if (ShouldDeferStructuralWork(
                        settings.DetailStructuralInstanceBudgetPerFrame,
                        remainingStructuralBudgetForBuild,
                        structuralCost,
                        structuralInstancesUsed,
                        scheduledStructuralCost))
                {
                    m_Layers.Ptr[layerIndex].SetDirty(patchIndex);
                    m_PatchNextLayerIndex[patchIndex] = layerIndex;
                    EnqueuePatchForWork(patchIndex);
                    buildResult.Dispose(buildResult.BuildHandle);
                    break;
                }

                if (PatchHasDirtyLayers(patchIndex))
                    EnqueuePatchForWork(patchIndex);

                if (buildResult.LocalToWorlds.Length > 0)
                    buildHandles.Add(buildResult.BuildHandle);

                resultLayerIndices.Add(layerIndex);
                patchResults.Add(buildResult);
                scheduledStructuralCost += structuralCost;
                stats.PatchesRebuilt++;
                builtCells++;

                if (!unlimitedBuildBudget)
                    remainingBuildBudget--;
            }

            bool needsStructuralApply = patchesToUnload.Length > 0;
            for (int i = 0; i < patchResults.Length && !needsStructuralApply; i++)
            {
                TerrainDetailLayer.PatchBuildResult result = patchResults[i];
                needsStructuralApply = result.AddCount > 0 || result.RemoveCount > 0;
            }

            if (needsStructuralApply)
            {
                using var _ = ApplyStructuralPhaseMarker.Auto();
                stats.StructuralApplyPhases++;

                if (buildHandles.Length > 0)
                {
                    using var __ = BuildWaitMarker.Auto();
                    JobHandle.CombineDependencies(buildHandles.AsArray()).Complete();
                }

                ApplyQueuedUnloads(patchesToUnload, settings, ref unloadedCells, ref structuralInstancesUsed, ref stats);

                using var layerPatchResults = new NativeList<TerrainDetailLayer.PatchBuildResult>(Allocator.Temp);
                for (int layerIndex = 0; layerIndex < m_Layers.Length; layerIndex++)
                {
                    layerPatchResults.Clear();
                    for (int resultIndex = 0; resultIndex < patchResults.Length; resultIndex++)
                    {
                        if (resultLayerIndices[resultIndex] == layerIndex)
                            layerPatchResults.Add(patchResults[resultIndex]);
                    }

                    if (layerPatchResults.Length > 0)
                    {
                        int structuralApplyCost = ApplyCreatesAndDestroysOnMainThread(ref m_Layers.Ptr[layerIndex], terrain, layerPatchResults, ref stats);
                        structuralInstancesUsed += structuralApplyCost;
                    }
                }
            }

            using var updateHandles = new NativeList<JobHandle>(Allocator.Temp);
            using (ScheduleLocalToWorldUpdatesMarker.Auto())
            {
                for (int resultIndex = 0; resultIndex < patchResults.Length; resultIndex++)
                {
                    ref TerrainDetailLayer layer = ref m_Layers.Ptr[resultLayerIndices[resultIndex]];
                    TerrainDetailLayer.PatchBuildResult result = patchResults[resultIndex];
                    stats.InstancesUpdated += result.UpdateCount;
                    JobHandle patchHandle = ScheduleUpdates(layer, result, result.BuildHandle);
                    result.Dispose(patchHandle);

                    if (!patchHandle.Equals(default))
                        updateHandles.Add(patchHandle);
                }
            }

            return updateHandles.Length > 0
                ? JobHandle.CombineDependencies(updateHandles.AsArray())
                : default;
        }

        private JobHandle ScheduleMarkPatchesInRange(in TerrainSnapshot terrain)
        {
            using var _ = MarkPatchesInRangeMarker.Auto();
            m_PatchesInRange.FillZeroes();

            if (m_PatchCount == 0)
                return default;

            if (!terrain.WithinDetailsRange)
            {
                m_PatchesToLoad.FillZeroes();
                m_PatchesToUnload.CopyFrom(m_PatchesLoaded);
                m_ScheduledMarkInRangeJob = true;
                return default;
            }

            NativeArray<BoundingSphere> streamingSpheres = m_StreamingManager.ValueRO.StreamingSpheres;
            NativeArray<BoundingSphere> spheresCopy = new NativeArray<BoundingSphere>(streamingSpheres, Allocator.TempJob);
            int chunkCount = MathUtility.DivideAndRoundUp(m_PatchCount, 64);
            float2 patchSizeXZ = new float2(terrain.Size.x / terrain.DetailPatchCount, terrain.Size.z / terrain.DetailPatchCount);

            JobHandle inRangeJobs = new RasterizeSpheresJob {
                TerrainOrigin     = terrain.Position,
                PatchSizeXZ       = patchSizeXZ,
                PatchCountPerEdge = terrain.DetailPatchCount,
                DetailDistance    = terrain.DetailDistance,
                StreamingSpheres  = spheresCopy.Reinterpret<float4>(),
                PatchesInRange    = m_PatchesInRange
            }.Schedule(spheresCopy.Length, 1);

            inRangeJobs = new ComputePatchLoadChangesJob {
                InRange  = m_PatchesInRange,
                Loaded   = m_PatchesLoaded,
                ToLoad   = m_PatchesToLoad,
                ToUnload = m_PatchesToUnload
            }.Schedule(chunkCount, 1, inRangeJobs);

            spheresCopy.Dispose(inRangeJobs);

            m_ScheduledMarkInRangeJob = true;

            return inRangeJobs;
        }

#if UNITY_EDITOR
        internal void ClearHidden()
        {
            m_Hidden = false;
        }

        internal void SetHidden()
        {
            for (int i = 0; i < m_Layers.Length; i++)
            {
                TerrainDetailLayer layer = m_Layers.Ptr[i];
                for (int j = 0; j < layer.PatchCount; j++)
                {
                    NativeBuffer<FloraInstanceHandle> instances = layer.InstancesPerPatch[j];
                    m_InstanceManager.ValueRW.SetHidden(instances.AsArray());
                }
            }
        }
#endif
    }

    #endregion
}
