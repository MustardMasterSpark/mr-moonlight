using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Burst.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Jobs.LowLevel.Unsafe;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace MA.Flora
{
    [Flags]
    internal enum InstanceTag : uint
    {
        // States
        None     = 0,
        Enabled  = 1 << 0,

        // Additional Data
        RandomID       = 1 << 10,
        VariationColor = 1 << 11,

        // Types
        Renderer       = 1 << 20,
        LODGroup       = 1 << 21,
        Billboard      = 1 << 22,
        TerrainTree    = 1 << 23,
        TerrainDetail  = 1 << 24,
        ContainerOwned = 1 << 25,
    }

    [BurstCompile]
    [GenerateBurstMonoInterop]
    internal unsafe partial struct InstanceManager : IDisposable
    {
        private const int InitialFrameAllocatorSize = 16 * 1024 * 1024;

        #region Chunk Fields

        private int m_NextChunkIndex;
        private NativeList<ChunkIndex> m_ChunkFreeList;
        private NativeBitSet m_ChunkAllocated;
        private NativeBitSet m_ChunkEnabled;
        private NativeBitSet m_ChunkStatic;
        private NativeBitSet m_ChunkDynamic;
        private NativeBitSet m_ChunkHasProbes;
        private NativeBitSet m_ChunkHasRandomValue;
        private NativeBitSet m_ChunkHasColorVariation;
        private NativeBitSet m_ChunkHasLightmapST;

        // Chunk CPU change tracking
        private NativeBitSet m_DirtyChunkTransforms;
        private NativeBitSet m_PendingSpatialUpdates;

        // Chunks GPU upload tracking
        private NativeBitSet m_PendingInstanceUpload;
        private NativeBitSet m_PendingVariationColorUpload;
        private NativeBitSet m_PendingLightmapSTUpload;
        private NativeBitSet m_PendingTransformUpload;

        private NativeBitSet m_TerrainDetailChunks;

        private GraphicsBufferRef m_ArchetypeDataBuffer;

        internal int MaxChunkCount => m_ChunkAllocated.MaxLength;
        internal GraphicsBufferRef ArchetypeDataBuffer => m_ArchetypeDataBuffer;

        #endregion

        #region Instance Fields

        private NativeArray<FloraInstanceHandle> m_InstanceHandles;
        private NativeArray<GraphicsMatrix> m_InstanceLocalToWorld;
        private NativeArray<GraphicsMatrix> m_InstancePrevLocalToWorld;
        private NativeArray<AABB> m_InstanceAABBs;
        private NativeArray<CellLocation> m_InstanceLocations;
        private NativeArray<InstanceInCullingChunk> m_InstanceInCullingChunks;
        private NativeArray<float> m_InstanceRandomIDs;
        private NativeArray<float4> m_InstanceVariationColors;
        private NativeArray<float4> m_InstanceLightmapSTs;
        private NativeArray<byte> m_InstanceMovedThisFrame;
        private NativeArray<byte> m_InstanceMovedLastFrame;
        private NativeArray<byte> m_InstanceFlippedWinding;
#if UNITY_EDITOR
        private ParallelBitArray m_InstanceEditorSelected;
        private ParallelBitArray m_InstanceEditorHidden;
#endif

        internal bool ForceLightProbeUpdate { get; set; }
        internal NativeBitSet TerrainDetailChunks => m_TerrainDetailChunks;
        internal NativeArray<FloraInstanceHandle> InstanceHandles => m_InstanceHandles;
        internal NativeArray<GraphicsMatrix> InstanceLocalToWorld => m_InstanceLocalToWorld;
        internal NativeArray<GraphicsMatrix> InstancePrevLocalToWorld => m_InstancePrevLocalToWorld;
        internal NativeArray<AABB> InstanceAABBs => m_InstanceAABBs;
        internal NativeArray<CellLocation> InstanceLocations => m_InstanceLocations;
        internal NativeArray<InstanceInCullingChunk> InstanceInCullingChunks => m_InstanceInCullingChunks;
        internal NativeArray<float> InstanceRandomIDs => m_InstanceRandomIDs;
        internal NativeArray<float4> InstanceVariationColors => m_InstanceVariationColors;
        internal NativeArray<float4> InstanceLightmapSTs => m_InstanceLightmapSTs;

        internal NativeArray<byte> InstanceMovedThisFrame => m_InstanceMovedThisFrame;
        internal NativeArray<byte> InstanceMovedLastFrame => m_InstanceMovedLastFrame;
        internal NativeArray<byte> InstanceFlippedWinding => m_InstanceFlippedWinding;
        internal readonly bool HasPendingSpatialUpdate(ChunkIndex chunk) => m_PendingSpatialUpdates.Contains(chunk);
        internal readonly bool HasPendingTransformUpload(ChunkIndex chunk) => m_PendingTransformUpload.Contains(chunk);
        internal readonly bool HasPendingLightmapSTUpload(ChunkIndex chunk) => m_PendingLightmapSTUpload.Contains(chunk);
        internal readonly bool ChunkHasLightProbes(ChunkIndex chunk) => m_ChunkHasProbes.Contains(chunk);
        internal readonly bool ChunkHasLightmapST(ChunkIndex chunk) => m_ChunkHasLightmapST.Contains(chunk);
#if UNITY_EDITOR
        internal ParallelBitArray InstanceEditorSelected => m_InstanceEditorSelected;
        internal ParallelBitArray InstanceEditorHidden => m_InstanceEditorHidden;
#endif

        internal JobHandle DataDependencies
        {
            get => m_DataDependencies;
            set => m_DataDependencies = value;
        }

        #endregion

        #region Archetype Fields

        private int m_NextArchetypeIndex;
        private NativeList<ArchetypeIndex> m_ArchetypeFreeList;
        private NativeParallelHashMap<ArchetypeKey, ArchetypeIndex> m_ArchetypeLookup;
        private NativeBitSet m_ArchetypeAllocated;
        private NativeBitSet m_ArchetypeDataDirty;
        private NativeArray<float4> m_ArchetypeDefaultVariationColors;
        private NativeBufferArray<ChunkIndex> m_ArchetypeChunks;
        private NativeBufferArray<ChunkIndex> m_ArchetypeChunksWithFreeSlots;
        private NativeParallelMultiHashMap<ulong, ArchetypeIndex> m_SceneHandleArchetypes;

        private static ulong GetSceneHandleRaw(Scene scene)
        {
#if UNITY_6000_5_OR_NEWER
            return scene.handle.GetRawData();
#else
            return unchecked((uint)scene.handle);
#endif
        }
        private NativeParallelMultiHashMap<TemplateIndex, ArchetypeIndex> m_TemplateArchetypes;

        private ArchetypeIndex m_CachedArchetype;
        private ArchetypeKey m_CachedArchetypeKey;

        #endregion

        #region Fields

        private NativeDataReference<CullingGrid> m_CullingGrid;
        private NativeDataReference<TemplateManager> m_TemplateManager;
        private NativeDataReference<InstanceBuffer> m_InstanceBuffer;

        private bool m_IsInitialized;
        private uint m_FrameVersion;
        private uint m_ContentVersion;
        private DoubleRewindableAllocators m_FrameAllocators;

        internal uint FrameVersion => m_FrameVersion;
        internal uint ContentVersion => m_ContentVersion;

        private JobHandle m_DataDependencies;
        private NativeScatterList<PackedArchetypeData> m_PendingArchetypeDataUploads;
        private BufferScatterData<uint> m_InstanceRandomIdScatterData;
        private BufferScatterData<uint2> m_InstanceEntityIdScatterData;
        private BufferScatterData<uint4> m_InstanceColorVariationScatterData;
        private BufferScatterData<uint4> m_InstanceLightmapSTScatterData;
        private BufferScatterData<GraphicsMatrix> m_InstanceStaticMatrixScatterData;
        private BufferScatterData<GraphicsMatrix> m_InstanceInitDynamicMatrixScatterData;
        private BufferScatterData<GraphicsMatrix> m_InstanceUpdateDynamicMatrixScatterData;
        private ChunkLightProbeScatterData m_InstanceLightProbeScatterData;

        private InstanceManager* Self => (InstanceManager*)UnsafeUtility.AddressOf(ref this);

        #endregion

        #region Lifecycle

        public void Initialize(in InstanceContext instanceContext)
        {
            Assert.IsFalse(m_IsInitialized, "InstanceManager is already initialized.");

            m_IsInitialized = true;
            m_FrameVersion = 1;

            ChunkStore.Initialize();
            ArchetypeStore.Initialize();

            // Chunks

            m_NextChunkIndex = 1;
            m_ChunkFreeList = new NativeList<ChunkIndex>(64, Allocator.Persistent);

            m_ChunkAllocated = new NativeBitSet(ChunkInitialCapacity, Allocator.Persistent);
            m_ChunkEnabled = new NativeBitSet(ChunkInitialCapacity, Allocator.Persistent);
            m_ChunkStatic = new NativeBitSet(ChunkInitialCapacity, Allocator.Persistent);
            m_ChunkDynamic = new NativeBitSet(ChunkInitialCapacity, Allocator.Persistent);
            m_ChunkHasProbes = new NativeBitSet(ChunkInitialCapacity, Allocator.Persistent);
            m_ChunkHasRandomValue = new NativeBitSet(ChunkInitialCapacity, Allocator.Persistent);
            m_ChunkHasColorVariation = new NativeBitSet(ChunkInitialCapacity, Allocator.Persistent);
            m_ChunkHasLightmapST = new NativeBitSet(ChunkInitialCapacity, Allocator.Persistent);

            m_DirtyChunkTransforms = new NativeBitSet(ChunkInitialCapacity, Allocator.Persistent);
            m_PendingSpatialUpdates = new NativeBitSet(ChunkInitialCapacity, Allocator.Persistent);

            m_PendingTransformUpload = new NativeBitSet(ChunkInitialCapacity, Allocator.Persistent);
            m_PendingInstanceUpload = new NativeBitSet(ChunkInitialCapacity, Allocator.Persistent);
            m_PendingVariationColorUpload = new NativeBitSet(ChunkInitialCapacity, Allocator.Persistent);
            m_PendingLightmapSTUpload = new NativeBitSet(ChunkInitialCapacity, Allocator.Persistent);

            m_TerrainDetailChunks = new NativeBitSet(ChunkInitialCapacity, Allocator.Persistent);

            m_ArchetypeDataBuffer = new GraphicsBufferRef(GraphicsBuffer.Target.Structured, ChunkInitialCapacity, UnsafeUtility.SizeOf<PackedArchetypeData>(), "Flora.ArchetypeDataBuffer");

            // Chunk Instance Data

            m_InstanceHandles = new NativeArray<FloraInstanceHandle>(ChunkInitialInstanceCapacity, Allocator.Persistent);
            m_InstanceLocalToWorld = new NativeArray<GraphicsMatrix>(ChunkInitialInstanceCapacity, Allocator.Persistent);
            m_InstancePrevLocalToWorld = new NativeArray<GraphicsMatrix>(ChunkInitialInstanceCapacity, Allocator.Persistent);
            m_InstanceAABBs = new NativeArray<AABB>(ChunkInitialInstanceCapacity, Allocator.Persistent);
            m_InstanceLocations = new NativeArray<CellLocation>(ChunkInitialInstanceCapacity, Allocator.Persistent);
            m_InstanceInCullingChunks = new NativeArray<InstanceInCullingChunk>(ChunkInitialInstanceCapacity, Allocator.Persistent);
            m_InstanceRandomIDs = new NativeArray<float>(ChunkInitialInstanceCapacity, Allocator.Persistent);
            m_InstanceVariationColors = new NativeArray<float4>(ChunkInitialInstanceCapacity, Allocator.Persistent);
            m_InstanceLightmapSTs = new NativeArray<float4>(ChunkInitialInstanceCapacity, Allocator.Persistent);
            m_InstanceMovedThisFrame = new NativeArray<byte>(ChunkInitialInstanceCapacity, Allocator.Persistent);
            m_InstanceMovedLastFrame = new NativeArray<byte>(ChunkInitialInstanceCapacity, Allocator.Persistent);
            m_InstanceFlippedWinding = new NativeArray<byte>(ChunkInitialInstanceCapacity, Allocator.Persistent);
#if UNITY_EDITOR
            m_InstanceEditorHidden = new ParallelBitArray(ChunkInitialInstanceCapacity, Allocator.Persistent);
            m_InstanceEditorSelected = new ParallelBitArray(ChunkInitialInstanceCapacity, Allocator.Persistent);
#endif

            // Archetypes

            m_NextArchetypeIndex = 1;
            m_ArchetypeFreeList = new NativeList<ArchetypeIndex>(ArchetypeInitialCapacity, Allocator.Persistent);
            m_ArchetypeLookup = new NativeParallelHashMap<ArchetypeKey, ArchetypeIndex>(ArchetypeInitialCapacity, Allocator.Persistent);
            m_ArchetypeAllocated = new NativeBitSet(ArchetypeInitialCapacity, Allocator.Persistent);
            m_ArchetypeDataDirty = new NativeBitSet(ArchetypeInitialCapacity, Allocator.Persistent);
            m_ArchetypeDefaultVariationColors = new NativeArray<float4>(ArchetypeInitialCapacity, Allocator.Persistent);
            m_ArchetypeChunks = new NativeBufferArray<ChunkIndex>(ArchetypeInitialCapacity, 0, Allocator.Persistent);
            m_ArchetypeChunksWithFreeSlots = new NativeBufferArray<ChunkIndex>(ArchetypeInitialCapacity, 0, Allocator.Persistent);
            m_SceneHandleArchetypes = new NativeParallelMultiHashMap<ulong, ArchetypeIndex>(ArchetypeInitialCapacity, Allocator.Persistent);
            m_TemplateArchetypes = new NativeParallelMultiHashMap<TemplateIndex, ArchetypeIndex>(ArchetypeInitialCapacity, Allocator.Persistent);

            // Managers

            m_CullingGrid = instanceContext.CullingGrid;
            m_TemplateManager = instanceContext.TemplateManager;
            m_InstanceBuffer = instanceContext.InstanceBuffer;
            InitializeTrackedContainers();

            m_ContentVersion = 1;
            m_FrameAllocators = new DoubleRewindableAllocators(Allocator.Persistent, InitialFrameAllocatorSize);

            // Scatter Data
            m_PendingArchetypeDataUploads = new NativeScatterList<PackedArchetypeData>(64, Allocator.Persistent);
        }

        public void Dispose()
        {
            if (!m_IsInitialized)
                return;

            SyncJobsForMainThread();
            m_IsInitialized = false;

            m_DataDependencies.Complete();
            m_FrameAllocators.Dispose();

            // Archetypes

            foreach (ArchetypeIndex archetype in m_ArchetypeAllocated.AsType<ArchetypeIndex>())
            {
                var archetypeChunks = m_ArchetypeChunks[archetype];
                for (int c = 0; c != archetypeChunks.Length; c++)
                {
                    var chunk = archetypeChunks[c];
                    var chunkInstances = GetInstanceHandlesRW(chunk, 0, chunk.Count);
                    InstanceRegistry.Data.DeallocateInstances(chunkInstances, chunk.Count);
                }

                ArchetypeStore.Data[archetype] = default;
            }

            m_ArchetypeFreeList.Dispose();
            m_ArchetypeLookup.Dispose();
            m_ArchetypeAllocated.Dispose();
            m_ArchetypeDataDirty.Dispose();
            m_ArchetypeDefaultVariationColors.Dispose();
            m_ArchetypeChunks.Dispose();
            m_ArchetypeChunksWithFreeSlots.Dispose();
            m_SceneHandleArchetypes.Dispose();
            m_TemplateArchetypes.Dispose();

            // Chunks

            m_ChunkFreeList.Dispose();
            m_ChunkAllocated.Dispose();
            m_ChunkEnabled.Dispose();
            m_ChunkStatic.Dispose();
            m_ChunkDynamic.Dispose();
            m_ChunkHasProbes.Dispose();
            m_ChunkHasRandomValue.Dispose();
            m_ChunkHasColorVariation.Dispose();
            m_ChunkHasLightmapST.Dispose();

            m_DirtyChunkTransforms.Dispose();
            m_PendingSpatialUpdates.Dispose();

            m_PendingTransformUpload.Dispose();
            m_PendingInstanceUpload.Dispose();
            m_PendingVariationColorUpload.Dispose();
            m_PendingLightmapSTUpload.Dispose();

            m_TerrainDetailChunks.Dispose();

            m_ArchetypeDataBuffer.Dispose();

            // Instance Data

            m_InstanceHandles.Dispose();
            m_InstanceLocalToWorld.Dispose();
            m_InstancePrevLocalToWorld.Dispose();
            m_InstanceAABBs.Dispose();
            m_InstanceLocations.Dispose();
            m_InstanceInCullingChunks.Dispose();
            m_InstanceRandomIDs.Dispose();
            m_InstanceVariationColors.Dispose();
            m_InstanceLightmapSTs.Dispose();
            m_InstanceMovedThisFrame.Dispose();
            m_InstanceMovedLastFrame.Dispose();
            m_InstanceFlippedWinding.Dispose();
#if UNITY_EDITOR
            m_InstanceEditorHidden.Dispose();
            m_InstanceEditorSelected.Dispose();
#endif
            DisposeTrackedContainers();

            // Scatter Data
            m_PendingArchetypeDataUploads.Dispose();
        }

        private void IncrementFrameVersion()
        {
            m_FrameVersion++;
            if (m_FrameVersion == 0)
                m_FrameVersion++;
        }

        private void UpdateContentVersion()
        {
            m_ContentVersion = m_FrameVersion;
        }

        #endregion

        #region Frame Allocations

        internal ref RewindableAllocator FrameAllocator
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref m_FrameAllocators.Allocator;
        }

        internal AllocatorManager.AllocatorHandle FrameAllocatorHandle
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => FrameAllocator.Handle;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal NativeArray<T> NewFrameArray<T>(int length, NativeArrayOptions options = NativeArrayOptions.ClearMemory) where T : unmanaged
        {
            return CollectionHelper.CreateNativeArray<T, RewindableAllocator>(length, ref FrameAllocator, options);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal NativeArray<T> NewFrameArray<T>(NativeArray<T> array) where T : unmanaged
        {
            var copy = CollectionHelper.CreateNativeArray<T, RewindableAllocator>(array.Length, ref FrameAllocator, NativeArrayOptions.UninitializedMemory);
            if (copy.Length > 0)
                UnsafeUtility.MemCpy(copy.GetUnsafePtr(), array.GetUnsafePtr(), array.Length * UnsafeUtility.SizeOf<T>());
            return copy;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal NativeArray<T> NewFrameArray<T>(T* ptr, int length) where T : unmanaged
        {
            var copy = CollectionHelper.CreateNativeArray<T, RewindableAllocator>(length, ref FrameAllocator, NativeArrayOptions.UninitializedMemory);
            if (length > 0)
                UnsafeUtility.MemCpy(copy.GetUnsafePtr(), ptr, length * UnsafeUtility.SizeOf<T>());
            return copy;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal NativeList<T> NewFrameList<T>(int length) where T : unmanaged
        {
            return FrameAllocator.AllocateNativeList<T>(length);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal T* NewFrameStruct<T>(int length = 1, NativeArrayOptions options = NativeArrayOptions.ClearMemory) where T : unmanaged
        {
            var ptr = (T*)FrameAllocator.Allocate(length * UnsafeUtility.SizeOf<T>(), UnsafeUtility.AlignOf<T>());
            if (length > 0 && options == NativeArrayOptions.ClearMemory)
            {
                if (length == 1)
                    *ptr = default;
                else
                    UnsafeUtility.MemClear(ptr, length * UnsafeUtility.SizeOf<T>());
            }
            return ptr;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal T* NewFrameData<T>(int length, NativeArrayOptions options) where T : unmanaged
        {
            var ptr = (T*)FrameAllocator.Allocate(length * UnsafeUtility.SizeOf<T>(), UnsafeUtility.AlignOf<T>());
            if (length > 0 && options == NativeArrayOptions.ClearMemory)
                UnsafeUtility.MemClear(ptr, length * UnsafeUtility.SizeOf<T>());
            return ptr;
        }

        #endregion

        #region Instances

        public int InstancesAllocated => InstanceRegistry.Data.ThreadUnsafeInstanceCount;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Exists(FloraInstanceHandle instance)
        {
            return instance != FloraInstanceHandle.Null && InstanceRegistry.Data.Exists(instance);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal bool Exists(int instanceIndex)
        {
            return InstanceRegistry.Data.Exists(m_InstanceHandles[instanceIndex]);
        }

        internal bool HasTag(FloraInstanceHandle instance, InstanceTag tag)
        {
            if (!instance.Exists())
                return false;

            var instanceInChunk = InstanceRegistry.Data.GetInstanceInChunk(instance);
            var sharedValues = instanceInChunk.Chunk.Archetype.Key;
            return (sharedValues.Tags & tag) != 0;
        }

        public bool IsEnabled(FloraInstanceHandle instance)
        {
            if (instance.Exists())
            {
                InstanceInChunk instanceInChunk = InstanceRegistry.Data.GetInstanceInChunk(instance);
                return instanceInChunk.Chunk.IsEnabled;
            }

            return false;
        }

        public void SetEnabled(FloraInstanceHandle instance, bool enabled)
        {
            CheckInstance(instance);

            if (enabled)
            {
                AddTagsToInstanceWithBurst(Self, &instance, InstanceTag.Enabled);
            }
            else
            {
                RemoveTagsFromInstanceWithBurst(Self, &instance, InstanceTag.Enabled);
            }
        }

        public void SetEnabled(NativeArray<FloraInstanceHandle> instances, bool enabled)
        {
            if (instances.Length == 0)
                return;

            if (enabled)
            {
                AddTagsToInstances(instances, InstanceTag.Enabled);
            }
            else
            {
                RemoveTagsFromInstances(instances, InstanceTag.Enabled);
            }
        }

        public GameObject GetIdentitySourceObject(FloraInstanceHandle instance)
        {
            if (instance.Exists())
            {
                InstanceInSourceRecord instanceInSourceRecord = InstanceRegistry.Data.GetInstanceInSourceRecord(instance);
                if (!instanceInSourceRecord.Equals(InstanceInSourceRecord.None))
                    return m_TemplateManager.ValueRO.GetIdentitySource(instanceInSourceRecord.SourceRecord);
            }

            return null;
        }

        public GameObject GetRenderSourceObject(FloraInstanceHandle instance)
        {
            if (instance.Exists())
            {
                InstanceInSourceRecord instanceInSourceRecord = InstanceRegistry.Data.GetInstanceInSourceRecord(instance);
                if (!instanceInSourceRecord.Equals(InstanceInSourceRecord.None))
                    return m_TemplateManager.ValueRO.GetRenderSource(instanceInSourceRecord.SourceRecord);
            }

            return null;
        }

        public GameObject GetSceneGameObject(FloraInstanceHandle instance)
        {
            if (instance.Exists())
            {
                EntityId sceneEntityId = InstanceRegistry.Data.GetSceneEntityId(instance);
                return sceneEntityId.ToObject<GameObject>();
            }

            return null;
        }

        public float GetRandomID(FloraInstanceHandle instance)
        {
            CheckInstance(instance);

            var instanceInChunk = InstanceRegistry.Data.GetInstanceInChunk(instance);
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (!instanceInChunk.Chunk.Archetype.HasRandomID)
                throw new InvalidOperationException("Instance does not have a random ID.");
#endif

            return m_InstanceRandomIDs[instanceInChunk.IndexInChunk];
        }

        public float4 GetVariationColor(FloraInstanceHandle instance)
        {
            CheckInstance(instance);

            var instanceInChunk = InstanceRegistry.Data.GetInstanceInChunk(instance);
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (!instanceInChunk.Chunk.Archetype.HasVariationColor)
                throw new InvalidOperationException("Instance does not have a variation color.");
#endif

            return m_InstanceVariationColors[instanceInChunk.IndexInChunk];
        }

        public void SetVariationColor(FloraInstanceHandle instance, float4 color)
        {
            CheckInstance(instance);

            var instanceInChunk = InstanceRegistry.Data.GetInstanceInChunk(instance);
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (!instanceInChunk.Chunk.Archetype.HasVariationColor)
                throw new InvalidOperationException("Instance does not have a variation color.");
#endif

            m_InstanceVariationColors[instanceInChunk.IndexInChunk] = color;
            m_PendingVariationColorUpload.Add(instanceInChunk.Chunk);
            UpdateContentVersion();
        }

        public void SetVariationColors(NativeArray<FloraInstanceHandle> instances, NativeArray<float4> colors)
        {
            if (instances.Length == 0)
                return;

            if (instances.Length != colors.Length)
                throw new ArgumentException("Instance and color array lengths do not match.");

            SetVariationColorsWithBurst(Self, instances.GetUnsafePtrT(), colors.GetUnsafePtrT(), instances.Length);
        }

        private static void SetVariationColorsWithBurst(InstanceManager* data, FloraInstanceHandle* instances, float4* colors, int count)
        {
            data->SyncJobsForMainThread();
            data->UpdateContentVersion();

            ChunkIndex previousChunk = ChunkIndex.None;

            for (int i = 0; i < count; i++)
            {
                var instance = instances[i];
                if (!instance.Exists())
                    continue;

                var instanceInChunk = InstanceRegistry.Data.GetInstanceInChunk(instance);
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                if (!instanceInChunk.Chunk.Archetype.HasVariationColor)
                    throw new InvalidOperationException("Instance does not have a variation color.");
#endif
                data->m_InstanceVariationColors[instanceInChunk.IndexInChunk] = colors[i];
                if (instanceInChunk.Chunk != previousChunk)
                {
                    data->m_PendingVariationColorUpload.Add(instanceInChunk.Chunk);
                    previousChunk = instanceInChunk.Chunk;
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        private void CheckInstance(FloraInstanceHandle instance)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (Hint.Unlikely(!instance.Exists()))
                throw new InvalidOperationException("Invalid instance.");
#endif
        }

        #endregion

        #region Create

        private struct InstantiateParams
        {
            public InstanceTag AdditionalTags;
            public Scene Scene;
            public SourceRecordIndex SourceRecord;
            public TemplateIndex Template;
            public EntityId SceneEntityId;
            public int TerrainDetailLayerIndex;
            public EntityId ContainerEntity;
            public int UnityRendererID;
            public int LightmapIndex;
            public byte Layer;
            public float MaxRenderDistance;
            public float4 LightmapST;
            public ulong SceneCullingMask;
            [MarshalAs(UnmanagedType.U1)]
            public bool HasLocalToWorld;
            public FloraLocalToWorld ParentLocalToWorld;
        }

        [ExcludeFromBurstCompatTesting("Takes managed objects")]
        public FloraInstanceHandle CreateInstance(GameObject source, GameObject owner, FloraLocalToWorld localToWorld)
        {
            SourceTemplateBinding binding = m_TemplateManager.ValueRW.RegisterSourceBinding(source, source, TemplateOptions.None);
            FloraInstanceHandle newInstance;
            CreateInstances(&newInstance, &localToWorld, 1, new InstantiateParams
            {
                AdditionalTags = GetSourceInstanceTags(source),
                Scene = owner.scene,
                SourceRecord = binding.SourceRecord,
                Template = binding.Template,
                SceneEntityId = owner.GetEntityId(),
                LightmapIndex = -1,
                Layer = (byte)owner.layer,
                LightmapST = new float4(1f, 1f, 0f, 0f),
                SceneCullingMask = owner.sceneCullingMask,
            });
            return newInstance;
        }

        [ExcludeFromBurstCompatTesting("Takes managed objects")]
        public FloraInstanceHandle CreateInstance(GameObject source, Transform parent, FloraInstanceTransform localInstanceTransform)
        {
            SourceTemplateBinding binding = m_TemplateManager.ValueRW.RegisterSourceBinding(source, source, TemplateOptions.None);
            FloraInstanceHandle newInstance;
            CreateInstances(&newInstance, &localInstanceTransform, 1, new InstantiateParams
            {
                AdditionalTags = GetSourceInstanceTags(source),
                Scene = parent.gameObject.scene,
                SourceRecord = binding.SourceRecord,
                Template = binding.Template,
                SceneEntityId = parent.gameObject.GetEntityId(),
                LightmapIndex = -1,
                Layer = (byte)parent.gameObject.layer,
                LightmapST = new float4(1f, 1f, 0f, 0f),
                SceneCullingMask = parent.gameObject.sceneCullingMask,
                HasLocalToWorld = true,
                ParentLocalToWorld = parent.localToWorldMatrix,
            });
            return newInstance;
        }

        [ExcludeFromBurstCompatTesting("Takes managed objects")]
        public FloraInstanceHandle CreateInstance(GameObject identitySource, GameObject renderSource, Transform transform, int lightmapIndex, float4 lightmapST)
        {
            SourceTemplateBinding binding = m_TemplateManager.ValueRW.RegisterSourceBinding(identitySource, renderSource, TemplateOptions.None);
            bool hasLightmaps = binding.Template.HasLightmaps;
            FloraInstanceHandle newInstance;
            FloraLocalToWorld localToWorld = transform.localToWorldMatrix;
            CreateInstances(&newInstance, &localToWorld, 1, new InstantiateParams
            {
                AdditionalTags = GetSourceInstanceTags(renderSource),
                Scene = transform.gameObject.scene,
                SourceRecord = binding.SourceRecord,
                Template = binding.Template,
                SceneEntityId = transform.gameObject.GetEntityId(),
                LightmapIndex = hasLightmaps ? lightmapIndex : -1,
                Layer = (byte)transform.gameObject.layer,
                LightmapST = hasLightmaps ? lightmapST : new float4(1f, 1f, 0f, 0f),
                SceneCullingMask = transform.gameObject.sceneCullingMask,
            });
            return newInstance;
        }

        [ExcludeFromBurstCompatTesting("Takes managed objects")]
        internal FloraInstanceHandle CreateInstance(GameObject source, Transform parent, EntityId containerEntity, FloraInstanceTransform localInstanceTransform)
        {
            SourceTemplateBinding binding = m_TemplateManager.ValueRW.RegisterSourceBinding(source, source, TemplateOptions.None);
            FloraInstanceHandle newInstance;
            CreateInstances(&newInstance, &localInstanceTransform, 1, new InstantiateParams
            {
                AdditionalTags = GetSourceInstanceTags(source) | InstanceTag.ContainerOwned,
                Scene = parent.gameObject.scene,
                SourceRecord = binding.SourceRecord,
                Template = binding.Template,
                SceneEntityId = parent.gameObject.GetEntityId(),
                ContainerEntity = containerEntity,
                LightmapIndex = -1,
                Layer = (byte)parent.gameObject.layer,
                LightmapST = new float4(1f, 1f, 0f, 0f),
                SceneCullingMask = parent.gameObject.sceneCullingMask,
                HasLocalToWorld = true,
                ParentLocalToWorld = parent.localToWorldMatrix,
            });
            return newInstance;
        }

        [ExcludeFromBurstCompatTesting("Takes managed objects")]
        public FloraInstanceHandle CreateInstance(GameObject source, Transform transform)
        {
            SourceTemplateBinding binding = m_TemplateManager.ValueRW.RegisterSourceBinding(source, source, TemplateOptions.None);
            FloraInstanceHandle newInstance;
            FloraLocalToWorld localToWorld = transform.localToWorldMatrix;
            CreateInstances(&newInstance, &localToWorld, 1, new InstantiateParams
            {
                AdditionalTags = GetSourceInstanceTags(source),
                Scene = transform.gameObject.scene,
                SourceRecord = binding.SourceRecord,
                Template = binding.Template,
                SceneEntityId = transform.gameObject.GetEntityId(),
                LightmapIndex = -1,
                Layer = (byte)transform.gameObject.layer,
                LightmapST = new float4(1f, 1f, 0f, 0f),
                SceneCullingMask = transform.gameObject.sceneCullingMask,
            });
            return newInstance;
        }

        [ExcludeFromBurstCompatTesting("Takes managed objects")]
        public void CreateInstances(GameObject source, Transform parent, NativeArray<FloraInstanceHandle> instances, NativeArray<FloraInstanceTransform> localTransforms)
        {
            SourceTemplateBinding binding = m_TemplateManager.ValueRW.RegisterSourceBinding(source, source, TemplateOptions.None);
            CreateInstances(instances.GetUnsafePtrT(), localTransforms.GetUnsafePtrT(), instances.Length, new InstantiateParams
            {
                AdditionalTags = GetSourceInstanceTags(source),
                Scene = parent.gameObject.scene,
                SourceRecord = binding.SourceRecord,
                Template = binding.Template,
                SceneEntityId = parent.gameObject.GetEntityId(),
                LightmapIndex = -1,
                Layer = (byte)source.gameObject.layer,
                LightmapST = new float4(1f, 1f, 0f, 0f),
                SceneCullingMask = parent.gameObject.sceneCullingMask,
                HasLocalToWorld = true,
                ParentLocalToWorld = parent.localToWorldMatrix,
            });
        }

        [ExcludeFromBurstCompatTesting("Takes managed objects")]
        internal void CreateInstances(GameObject source, Transform parent, EntityId containerEntity, NativeArray<FloraInstanceHandle> instances, NativeArray<FloraInstanceTransform> localTransforms)
        {
            SourceTemplateBinding binding = m_TemplateManager.ValueRW.RegisterSourceBinding(source, source, TemplateOptions.None);
            CreateInstances(instances.GetUnsafePtrT(), localTransforms.GetUnsafePtrT(), instances.Length, new InstantiateParams
            {
                AdditionalTags = GetSourceInstanceTags(source) | InstanceTag.ContainerOwned,
                Scene = parent.gameObject.scene,
                SourceRecord = binding.SourceRecord,
                Template = binding.Template,
                SceneEntityId = parent.gameObject.GetEntityId(),
                ContainerEntity = containerEntity,
                LightmapIndex = -1,
                Layer = (byte)source.gameObject.layer,
                LightmapST = new float4(1f, 1f, 0f, 0f),
                SceneCullingMask = parent.gameObject.sceneCullingMask,
                HasLocalToWorld = true,
                ParentLocalToWorld = parent.localToWorldMatrix,
            });
        }

        [ExcludeFromBurstCompatTesting("Takes managed objects")]
        public void CreateInstances(GameObject source, GameObject sceneObject, NativeArray<FloraInstanceHandle> instances, NativeArray<FloraInstanceTransform> localTransforms)
        {
            SourceTemplateBinding binding = m_TemplateManager.ValueRW.RegisterSourceBinding(source, source, TemplateOptions.None);
            CreateInstances(instances.GetUnsafePtrT(), localTransforms.GetUnsafePtrT(), instances.Length, new InstantiateParams
            {
                AdditionalTags = GetSourceInstanceTags(source),
                Scene = sceneObject.scene,
                SourceRecord = binding.SourceRecord,
                Template = binding.Template,
                SceneEntityId = sceneObject.GetEntityId(),
                LightmapIndex = -1,
                Layer = (byte)source.layer,
                LightmapST = new float4(1f, 1f, 0f, 0f),
                SceneCullingMask = sceneObject.sceneCullingMask,
            });
        }

        [ExcludeFromBurstCompatTesting("Takes managed objects")]
        public void CreateInstances(GameObject source, GameObject sceneObject, NativeArray<FloraInstanceHandle> instances, NativeArray<FloraLocalToWorld> localToWorlds)
        {
            SourceTemplateBinding binding = m_TemplateManager.ValueRW.RegisterSourceBinding(source, source, TemplateOptions.None);
            CreateInstances(instances.GetUnsafePtrT(), localToWorlds.GetUnsafePtrT(), instances.Length, new InstantiateParams
            {
                AdditionalTags = GetSourceInstanceTags(source),
                Scene = sceneObject.gameObject.scene,
                SourceRecord = binding.SourceRecord,
                Template = binding.Template,
                SceneEntityId = sceneObject.GetEntityId(),
                LightmapIndex = -1,
                Layer = (byte)source.gameObject.layer,
                LightmapST = new float4(1f, 1f, 0f, 0f),
                SceneCullingMask = sceneObject.gameObject.sceneCullingMask,
            });
        }

        private void CreateInstances(FloraInstanceHandle* instances, FloraInstanceTransform* transforms, int instanceCount, in InstantiateParams parameters)
        {
            if (instanceCount > 0)
            {
                InstantiateInstancesWithBurst(Self, instances, transforms, instanceCount, parameters);
            }
        }

        private void CreateInstances(FloraInstanceHandle* instances, FloraLocalToWorld* localToWorlds, int instanceCount, in InstantiateParams parameters)
        {
            if (instanceCount > 0)
            {
                InstantiateInstancesWithBurst(Self, instances, localToWorlds, instanceCount, parameters);
            }
        }

        private static InstanceTag GetSourceInstanceTags(GameObject source)
        {
            return source.TryGetComponent(out LODGroup _) ? InstanceTag.LODGroup : InstanceTag.Renderer;
        }

        #endregion

        #region Destroy

        public void Destroy(FloraInstanceHandle instance)
        {
            Destroy(&instance, 1);
        }

        public void Destroy(NativeArray<FloraInstanceHandle> instances)
        {
            Destroy(instances.GetUnsafePtrT(), instances.Length);
        }

        private void Destroy(FloraInstanceHandle* instances, int instanceCount)
        {
            if (instanceCount > 0)
            {
                DestroyWithBurst(Self, instances, instanceCount);
            }
        }

        [BurstCompile]
        private static void DestroyWithBurst(InstanceManager* data, FloraInstanceHandle* instances, int instanceCount)
        {
            data->DestroyInstances(instances, instanceCount);
        }

        #endregion

        #region Transforms

        public void UpdateLocalToWorld(FloraInstanceHandle instance, FloraLocalToWorld localToWorld)
        {
            CheckInstance(instance);
            SyncJobsForMainThread();
            InstanceInChunk instanceInChunk = InstanceRegistry.Data.GetInstanceInChunk(instance);
            int instanceIndex = instanceInChunk.Chunk * ChunkCapacity + instanceInChunk.IndexInChunk;
            UpdateInstanceTransformData(instanceInChunk, instanceIndex, localToWorld);
            MarkChunkTransformDirty(instanceInChunk.Chunk);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void UpdateInstanceTransformData(InstanceInChunk instanceInChunk, int instanceIndex, FloraLocalToWorld localToWorld)
        {
            var prevLocalToWorld = m_InstancePrevLocalToWorld[instanceIndex];
            var changed = !localToWorld.NearlyEquals(prevLocalToWorld);
            if (Hint.Likely(changed))
            {
                AABB localAABB = instanceInChunk.Chunk.LocalAABB;
                AABB worldAABB = AABB.TransformAABB(localToWorld, localAABB);

                m_InstanceLocalToWorld[instanceIndex] = localToWorld;
                m_InstanceAABBs[instanceIndex] = worldAABB;

                m_InstanceMovedThisFrame[instanceIndex] = 1;
                m_InstanceFlippedWinding[instanceIndex] = localToWorld.IsFlipped ? (byte)1 : (byte)0;
            }
        }

        public FloraLocalToWorld GetLocalToWorld(FloraInstanceHandle instance)
        {
            SyncJobsForMainThread();
            CheckInstance(instance);
            InstanceInChunk instanceInChunk = InstanceRegistry.Data.GetInstanceInChunk(instance);
            int instanceIndex = instanceInChunk.Chunk * ChunkCapacity + instanceInChunk.IndexInChunk;
            return m_InstanceLocalToWorld[instanceIndex];
        }

        public NativeArray<FloraLocalToWorld> GetLocalToWorlds(NativeArray<FloraInstanceHandle> instances, Allocator allocator)
        {
            GetLocalToWorldsWithBurst(Self, instances, allocator, out var result);
            return result;
        }

        [BurstCompile]
        private static void GetLocalToWorldsWithBurst(
            InstanceManager* data,
            in NativeArray<FloraInstanceHandle> instances,
            Allocator allocator,
            out NativeArray<FloraLocalToWorld> result)
        {
            data->SyncJobsForMainThread();

            var instanceMatrices = new NativeArray<FloraLocalToWorld>(instances.Length, allocator);
            for (int i = 0; i < instances.Length; i++)
            {
                var instance = instances[i];
                if (instance.Exists())
                {
                    InstanceInChunk instanceInChunk = InstanceRegistry.Data.GetInstanceInChunk(instance);
                    int instanceIndex = instanceInChunk.Chunk * ChunkCapacity + instanceInChunk.IndexInChunk;
                    instanceMatrices[i] = data->m_InstanceLocalToWorld[instanceIndex];
                }
            }
            result = instanceMatrices;
        }

        public NativeArray<FloraInstanceTransform> GetWorldTransforms(NativeArray<FloraInstanceHandle> instances, Allocator allocator)
        {
            GetWorldTransformsWithBurst(Self, instances, allocator, out var result);
            return result;
        }

        [BurstCompile]
        private static void GetWorldTransformsWithBurst(
            InstanceManager* data,
            in NativeArray<FloraInstanceHandle> instances,
            Allocator allocator,
            out NativeArray<FloraInstanceTransform> result)
        {
            data->SyncJobsForMainThread();

            var instanceMatrices = new NativeArray<FloraInstanceTransform>(instances.Length, allocator, NativeArrayOptions.UninitializedMemory);
            for (int i = 0; i < instances.Length; i++)
            {
                var instance = instances[i];
                if (instance.Exists())
                {
                    InstanceInChunk instanceInChunk = InstanceRegistry.Data.GetInstanceInChunk(instance);
                    int instanceIndex = instanceInChunk.Chunk * ChunkCapacity + instanceInChunk.IndexInChunk;
                    instanceMatrices[i] = FloraInstanceTransform.FromMatrix(data->m_InstanceLocalToWorld[instanceIndex]);
                }
            }
            result = instanceMatrices;
        }

        public NativeArray<float3> GetPositions(NativeArray<FloraInstanceHandle> instances, Allocator allocator)
        {
            GetPositionsWithBurst(Self, instances, allocator, out var result);
            return result;
        }

        [BurstCompile]
        private static void GetPositionsWithBurst(
            InstanceManager* data,
            in NativeArray<FloraInstanceHandle> instances,
            Allocator allocator,
            out NativeArray<float3> result)
        {
            data->SyncJobsForMainThread();

            result = new NativeArray<float3>(instances.Length, allocator, NativeArrayOptions.UninitializedMemory);
            for (int i = 0; i < instances.Length; i++)
            {
                var instance = instances[i];
                if (instance.Exists())
                {
                    InstanceInChunk instanceInChunk = InstanceRegistry.Data.GetInstanceInChunk(instance);
                    int instanceIndex = instanceInChunk.Chunk * ChunkCapacity + instanceInChunk.IndexInChunk;
                    result[i] = data->m_InstanceLocalToWorld[instanceIndex].Position;
                }
            }
        }

        #endregion

        #region Bounds

        public Bounds GetBounds(FloraInstanceHandle instance)
        {
            CheckInstance(instance);
            SyncJobsForMainThread();
            InstanceInChunk instanceInChunk = InstanceRegistry.Data.GetInstanceInChunk(instance);
            int instanceIndex = instanceInChunk.Chunk * ChunkCapacity + instanceInChunk.IndexInChunk;
            return m_InstanceAABBs[instanceIndex].ToBounds();
        }

        public NativeArray<Bounds> GetBounds(NativeArray<FloraInstanceHandle> instances, Allocator allocator)
        {
            GetBoundsWithBurst(Self, instances, allocator, out var result);
            return result;
        }

        [BurstCompile]
        private static void GetBoundsWithBurst(
            InstanceManager* data,
            in NativeArray<FloraInstanceHandle> instances,
            Allocator allocator,
            out NativeArray<Bounds> result)
        {
            data->SyncJobsForMainThread();

            result = new NativeArray<Bounds>(instances.Length, allocator);
            for (int i = 0; i < instances.Length; i++)
            {
                var instance = instances[i];
                if (instance.Exists())
                {
                    InstanceInChunk instanceInChunk = InstanceRegistry.Data.GetInstanceInChunk(instance);
                    int instanceIndex = instanceInChunk.Chunk * ChunkCapacity + instanceInChunk.IndexInChunk;
                    result[i] = data->m_InstanceAABBs[instanceIndex].ToBounds();
                }
            }
        }

        public Bounds CalculateInstanceBounds(NativeArray<FloraInstanceHandle> instances, float4x4 inSpace, bool useInSpace)
        {
            if (instances.Length == 0)
                return AxisAlignedBox.Empty;

            SyncJobsForMainThread();

            var threadedAABBArray = NewFrameArray<AABBMinMax>(JobsUtility.MaxJobThreadCount, NativeArrayOptions.UninitializedMemory);
            threadedAABBArray.Fill(AABBMinMax.Empty);

            var boundsJob = new ComputeBoundsForInstancesJob
            {
                HasInSpace = useInSpace,
                InSpace = inSpace,
                Instances = instances.AsReadOnly(),
                InstanceBounds = m_InstanceAABBs.AsReadOnly(),
                BoundsPerThread = threadedAABBArray,
            };

            boundsJob
                .ScheduleParallel(instances.Length, ComputeBoundsForInstancesJob.BatchSize, default)
                .Complete();

            var bounds = AABBMinMax.Empty;
            for (var i = 0; i < threadedAABBArray.Length; i++)
                bounds += threadedAABBArray[i];

            return bounds.ToBounds();
        }

        [BurstCompile(OptimizeFor = OptimizeFor.Performance, DisableSafetyChecks = true)]
        private struct ComputeBoundsForInstancesJob : IJobFor
        {
            public const int BatchSize = 128;

            [ReadOnly] public bool HasInSpace;
            [ReadOnly] public float4x4 InSpace;
            [ReadOnly] public NativeArray<FloraInstanceHandle>.ReadOnly Instances;
            [ReadOnly] public NativeArray<AABB>.ReadOnly InstanceBounds;

            [NativeDisableParallelForRestriction] public NativeArray<AABBMinMax> BoundsPerThread;
            [NativeSetThreadIndex] private int m_ThreadIndex;

            public void Execute(int index)
            {
                var instance = Instances[index];
                if (instance.Exists())
                {
                    var instanceInChunk = InstanceRegistry.Data.GetInstanceInChunk(instance);
                    var instanceIndex = instanceInChunk.Chunk * ChunkCapacity + instanceInChunk.IndexInChunk;
                    var instanceBounds = InstanceBounds[instanceIndex];

                    if (HasInSpace)
                        instanceBounds = AABB.TransformAABB(InSpace, instanceBounds);

                    BoundsPerThread[m_ThreadIndex] += instanceBounds;
                }
            }
        }

        #endregion

        #region Instance Containers

        public FloraInstanceContainer GetParentInstanceContainer(FloraInstanceHandle instance)
        {
            if (!instance.Exists())
                return null;

            InstanceInContainer instanceInContainer = InstanceRegistry.Data.GetInstanceInContainer(instance);
            return instanceInContainer.Container;
        }

        public int GetIndexInInstanceContainer(FloraInstanceHandle instance)
        {
            if (!instance.Exists())
                return -1;

            InstanceInContainer instanceInContainer = InstanceRegistry.Data.GetInstanceInContainer(instance);
            return instanceInContainer.IndexInContainer;
        }

        public void SetInstanceInContainer(FloraInstanceHandle instance, FloraInstanceContainer instanceContainer, int startIndex = 0)
        {
            if (instance.Exists())
            {
                SetInstanceInContainerIndicesWithBurst(&instance, 1, instanceContainer, startIndex);
            }
        }

        public void SetInstanceInContainerIndices(NativeArray<FloraInstanceHandle> instances, FloraInstanceContainer instanceContainer, int startIndex = 0)
        {
            if (instances.Length > 0)
            {
                SetInstanceInContainerIndicesWithBurst(instances.GetUnsafePtrT(), instances.Length, instanceContainer, startIndex);
            }
        }

        [BurstCompile]
        private static void SetInstanceInContainerIndicesWithBurst(FloraInstanceHandle* instances, int instanceCount, in EntityObjectRef<FloraInstanceContainer> instanceContainer, int startIndex = 0)
        {
            if (instanceCount > 0)
            {
                for (var i = 0; i < instanceCount; i++)
                {
                    InstanceRegistry.Data.SetInstanceInContainer(instances[i],
                        new InstanceInContainer { ContainerEntity = instanceContainer.Value, IndexInContainer = startIndex + i });
                }
            }
        }

        public void GetInstanceContainers(NativeArray<FloraInstanceHandle> instances, List<FloraInstanceContainer> containers)
        {
            using (HashSetPool<FloraInstanceContainer>.Get(out var containerSet))
            {
                foreach (var instance in instances)
                {
                    if (instance.Exists())
                    {
                        var instanceInContainer = InstanceRegistry.Data.GetInstanceInContainer(instance);
                        if (instanceInContainer.Container.IsValid())
                            containerSet.Add(instanceInContainer.Container);
                    }
                }

                containers.Clear();
                containers.AddRange(containerSet);
            }
        }

        public NativeParallelMultiHashMap<EntityId, int> GetInstanceContainerIndexMap(NativeArray<FloraInstanceHandle> instances, Allocator allocator)
        {
            GetInstanceContainerIndexMapWithBurst(Self, instances, allocator, out var result);
            return result;
        }

        [BurstCompile]
        private static void GetInstanceContainerIndexMapWithBurst(
            InstanceManager* data,
            in NativeArray<FloraInstanceHandle> instances,
            Allocator allocator,
            out NativeParallelMultiHashMap<EntityId, int> result)
        {
            result = new NativeParallelMultiHashMap<EntityId, int>(instances.Length, allocator);

            for (int i = 0; i < instances.Length; i++)
            {
                if (data->Exists(instances[i]))
                {
                    var instanceInContainer = InstanceRegistry.Data.GetInstanceInContainer(instances[i]);
                    if (!instanceInContainer.Equals(InstanceInContainer.None))
                    {
                        result.Add(instanceInContainer.Container.Value, instanceInContainer.IndexInContainer);
                    }
                }
            }
        }

        private struct InstanceInContainerIndexPair : IComparable<InstanceInContainerIndexPair>
        {
            public InstanceInContainer Instance;
            public int Index;
            public int CompareTo(InstanceInContainerIndexPair other) => Instance.CompareTo(other.Instance);
        }

        public NativeArray<InstanceInContainer> GetInstanceInContainersAndIndices(NativeArray<FloraInstanceHandle> instances, Allocator allocator, out NativeArray<int> indices)
        {
            GetInstanceInContainersAndIndicesWithBurst(Self, instances, allocator, out indices, out var result);
            return result;
        }

        [BurstCompile]
        private static void GetInstanceInContainersAndIndicesWithBurst(
            InstanceManager* data,
            in NativeArray<FloraInstanceHandle> instances,
            Allocator allocator,
            out NativeArray<int> indices,
            out NativeArray<InstanceInContainer> result)
        {
            using var hash = new NativeParallelMultiHashMap<EntityId, InstanceInContainerIndexPair>(instances.Length, allocator);
            var count = 0;

            for (int i = 0; i < instances.Length; i++)
            {
                if (data->Exists(instances[i]))
                {
                    var instance = InstanceRegistry.Data.GetInstanceInContainer(instances[i]);
                    if (instance.Container.IsValid())
                    {
                        hash.Add(instance.Container.Value, new InstanceInContainerIndexPair { Instance = instance, Index = i });
                        count++;
                    }
                }
            }

            using var keys = hash.GetKeyArray(Allocator.TempJob);
            var keyCount = keys.Unique();
            var writeIndex = 0;
            result = new NativeArray<InstanceInContainer>(count, allocator, NativeArrayOptions.UninitializedMemory);
            indices = new NativeArray<int>(count, allocator, NativeArrayOptions.UninitializedMemory);

            for (int i = 0; i < keyCount; i++)
            {
                foreach (var pair in hash.GetValuesForKey(keys[i]))
                {
                    result[writeIndex] = pair.Instance;
                    indices[writeIndex] = pair.Index;
                    writeIndex++;
                }
            }
        }

        #endregion

        #region Instantiate

        private static readonly ProfilerMarker InstantiateTransformsMarker = new("Flora.InstantiateTransforms");
        private static readonly ProfilerMarker ConvertAndUpdateLocalToWorldsMarker = new("Flora.ConvertAndUpdateLocalToWorlds");
        private static readonly ProfilerMarker UpdateLocalToWorldsMarker = new("Flora.UpdateLocalToWorlds");

        [BurstCompile]
        private static void InstantiateInstancesWithBurst(
            InstanceManager* data,
            FloraInstanceHandle* instances, FloraInstanceTransform* transforms, int instanceCount,
            in InstantiateParams parameters)
        {
            using var _ = InstantiateTransformsMarker.Auto();

            FloraLocalToWorld* localToWorlds;
            if (instanceCount < 32)
            {
                var ptr = stackalloc FloraLocalToWorld[instanceCount];
                localToWorlds = ptr;
            }
            else
            {
                localToWorlds = MemoryUtility.Allocate<FloraLocalToWorld>(instanceCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            }

            if (parameters.HasLocalToWorld)
            {
                var parentLocalToWorld = parameters.ParentLocalToWorld;
                for (int i = 0; i < instanceCount; i++)
                    localToWorlds[i] = parentLocalToWorld.Transform(transforms[i]);
            }
            else
            {
                for (int i = 0; i < instanceCount; i++)
                    localToWorlds[i] = transforms[i].ToMatrix();
            }

            data->AllocateInstances(instances, localToWorlds, instanceCount, parameters);
        }


        [BurstCompile]
        private static void InstantiateInstancesWithBurst(
            InstanceManager* data,
            FloraInstanceHandle* instances, FloraLocalToWorld* localToWorlds, int count,
            in InstantiateParams parameters)
        {
            data->AllocateInstances(instances, localToWorlds, count, parameters);
        }

        #endregion

        #region Update LocalToWorld

        private const int UpdateJobBatchSize = 256;

        private void MarkInstanceTransformsInternal(NativeArray<FloraInstanceHandle> instances)
        {
            UpdateContentVersion();

            ChunkIndex prevChunk = ChunkIndex.None;
            for (int i = 0; i < instances.Length; i++)
            {
                InstanceInChunk instanceInChunk = InstanceRegistry.Data.GetInstanceInChunk(instances[i]);
                if (Hint.Unlikely(instanceInChunk.Chunk == ChunkIndex.None))
                    continue;

                if (instanceInChunk.Chunk != prevChunk)
                {
                    MarkChunkTransformDirty(instanceInChunk.Chunk);
                    prevChunk = instanceInChunk.Chunk;
                }
            }
        }

        [BurstCompile(OptimizeFor = OptimizeFor.Performance, DisableSafetyChecks = true)]
        private static void MarkInstanceTransformsDirtyWithBurst(InstanceManager* data, in NativeArray<FloraInstanceHandle> instances)
        {
            data->MarkInstanceTransformsInternal(instances);
        }

        internal void MarkInstanceTransformsDirty(NativeArray<FloraInstanceHandle> instances)
        {
            MarkInstanceTransformsDirtyWithBurst(Self, instances);
        }

        public void UpdateLocalToWorlds(NativeArray<FloraInstanceHandle> instances, NativeArray<FloraLocalToWorld> localToWorlds)
        {
            if (instances.Length == 0)
                return;

            SyncJobsForMainThread();
            MarkInstanceTransformsDirtyWithBurst(Self, instances);

            var job = new UpdateLocalToWorldsJob
            {
                InputInstances = instances, InputLocalToWorlds = localToWorlds,
                PrevLocalToWorlds = m_InstancePrevLocalToWorld, LocalToWorlds = m_InstanceLocalToWorld, InstanceAABBs = m_InstanceAABBs,
                FlippedWinding = m_InstanceFlippedWinding, MovedThisFrame = m_InstanceMovedThisFrame,
            };

            if (instances.Length <= UpdateJobBatchSize)
            {
                job.RunBatch(instances.Length);
            }
            else
            {
                job.ScheduleParallel(instances.Length, UpdateJobBatchSize, m_DataDependencies).Complete();
            }
        }

        public JobHandle ScheduleUpdateLocalToWorlds(NativeArray<FloraInstanceHandle> instances, NativeArray<FloraLocalToWorld> localToWorlds, JobHandle inputDeps)
        {
            if (instances.Length == 0)
                return inputDeps;

            MarkInstanceTransformsDirtyWithBurst(Self, instances);

            m_DataDependencies = JobHandle.CombineDependencies(m_DataDependencies, inputDeps);
            m_DataDependencies = new UpdateLocalToWorldsJob {
                InputInstances = instances, InputLocalToWorlds = localToWorlds,
                PrevLocalToWorlds = m_InstancePrevLocalToWorld, LocalToWorlds = m_InstanceLocalToWorld, InstanceAABBs = m_InstanceAABBs,
                FlippedWinding = m_InstanceFlippedWinding, MovedThisFrame = m_InstanceMovedThisFrame,
            }.ScheduleParallel(instances.Length, UpdateJobBatchSize, m_DataDependencies);

            return m_DataDependencies;
        }

        public void UpdateWorldTransforms(NativeArray<FloraInstanceHandle> instances, NativeArray<FloraInstanceTransform> transforms)
        {
            if (instances.Length == 0)
                return;

            SyncJobsForMainThread();
            MarkInstanceTransformsDirtyWithBurst(Self, instances);

            var job = new UpdateWorldTransformsJob {
                InputInstances = instances, InputTransforms = transforms,
                PrevLocalToWorlds = m_InstancePrevLocalToWorld, LocalToWorlds = m_InstanceLocalToWorld, InstanceAABBs = m_InstanceAABBs,
                FlippedWinding = m_InstanceFlippedWinding, MovedThisFrame = m_InstanceMovedThisFrame
            };

            if (instances.Length <= UpdateJobBatchSize)
            {
                job.RunBatch(instances.Length);
            }
            else
            {
                job.ScheduleParallel(instances.Length, UpdateJobBatchSize, m_DataDependencies).Complete();
            }
        }

        public JobHandle ScheduleUpdateWorldTransforms(NativeArray<FloraInstanceHandle> instances, NativeArray<FloraInstanceTransform> transforms, JobHandle inputDeps)
        {
            if (instances.Length == 0)
                return inputDeps;

            MarkInstanceTransformsDirtyWithBurst(Self, instances);

            m_DataDependencies = JobHandle.CombineDependencies(m_DataDependencies, inputDeps);
            m_DataDependencies = new UpdateWorldTransformsJob {
                InputInstances = instances, InputTransforms = transforms,
                PrevLocalToWorlds = m_InstancePrevLocalToWorld, LocalToWorlds = m_InstanceLocalToWorld, InstanceAABBs = m_InstanceAABBs,
                FlippedWinding = m_InstanceFlippedWinding, MovedThisFrame = m_InstanceMovedThisFrame,
            }.ScheduleParallel(instances.Length, UpdateJobBatchSize, m_DataDependencies);

            return m_DataDependencies;
        }

        public void UpdateLocalTransforms(FloraLocalToWorld parentLocalToWorld, NativeArray<FloraInstanceHandle> instances, NativeArray<FloraInstanceTransform> transforms)
        {
            if (instances.Length == 0)
                return;

            SyncJobsForMainThread();
            MarkInstanceTransformsDirtyWithBurst(Self, instances);

            var job = new UpdateLocalTransformsJob {
                InputInstances = instances, InputTransforms = transforms, ParentLocalToWorld = parentLocalToWorld,
                PrevLocalToWorlds = m_InstancePrevLocalToWorld, LocalToWorlds = m_InstanceLocalToWorld, InstanceAABBs = m_InstanceAABBs,
                FlippedWinding = m_InstanceFlippedWinding, MovedThisFrame = m_InstanceMovedThisFrame,
            };

            if (instances.Length <= UpdateJobBatchSize)
            {
                job.RunBatch(instances.Length);
            }
            else
            {
                job.ScheduleParallel(instances.Length, UpdateJobBatchSize, m_DataDependencies).Complete();
            }
        }

        public JobHandle ScheduleUpdateLocalTransforms(FloraLocalToWorld parentLocalToWorld, NativeArray<FloraInstanceHandle> instances, NativeArray<FloraInstanceTransform> transforms, JobHandle inputDeps)
        {
            if (instances.Length == 0)
                return inputDeps;

            MarkInstanceTransformsDirtyWithBurst(Self, instances);

            m_DataDependencies = JobHandle.CombineDependencies(m_DataDependencies, inputDeps);
            m_DataDependencies = new UpdateLocalTransformsJob {
                InputInstances = instances, InputTransforms = transforms, ParentLocalToWorld = parentLocalToWorld,
                PrevLocalToWorlds = m_InstancePrevLocalToWorld, LocalToWorlds = m_InstanceLocalToWorld, InstanceAABBs = m_InstanceAABBs,
                FlippedWinding = m_InstanceFlippedWinding, MovedThisFrame = m_InstanceMovedThisFrame,
            }.ScheduleParallel(instances.Length, UpdateJobBatchSize, m_DataDependencies);

            return m_DataDependencies;
        }

        [BurstCompile(OptimizeFor = OptimizeFor.Performance, DisableSafetyChecks = true)]
        private struct UpdateLocalToWorldsJob : IJobParallelForBatch
        {
            [ReadOnly] public NativeArray<FloraInstanceHandle> InputInstances;
            [ReadOnly] public NativeArray<FloraLocalToWorld> InputLocalToWorlds;
            [ReadOnly] public NativeArray<GraphicsMatrix> PrevLocalToWorlds;

            [WriteOnly,NativeDisableParallelForRestriction] public NativeArray<GraphicsMatrix> LocalToWorlds;
            [WriteOnly,NativeDisableParallelForRestriction] public NativeArray<AABB> InstanceAABBs;
            [WriteOnly,NativeDisableParallelForRestriction] public NativeArray<byte> MovedThisFrame;
            [WriteOnly,NativeDisableParallelForRestriction] public NativeArray<byte> FlippedWinding;

            public void Execute(int startIndex, int count)
            {
                for (int i = 0; i < count; i++)
                {
                    var instance = InputInstances[startIndex + i];
                    var instanceInChunk = InstanceRegistry.Data.GetInstanceInChunk(instance);
                    if (Hint.Unlikely(instanceInChunk.Chunk == ChunkIndex.None))
                        continue;

                    var instanceIndex = instanceInChunk.Chunk * ChunkCapacity + instanceInChunk.IndexInChunk;
                    var localToWorld = InputLocalToWorlds[startIndex + i];
                    var prevLocalToWorld = PrevLocalToWorlds[instanceIndex];
                    var changed = !localToWorld.NearlyEquals(prevLocalToWorld);
                    if (Hint.Likely(changed))
                    {
                        AABB localAABB = instanceInChunk.Chunk.LocalAABB;
                        AABB worldAABB = AABB.TransformAABB(localToWorld, localAABB);

                        LocalToWorlds[instanceIndex] = localToWorld;
                        InstanceAABBs[instanceIndex] = worldAABB;

                        MovedThisFrame[instanceIndex] = 1;
                        FlippedWinding[instanceIndex] = localToWorld.IsFlipped ? (byte)1 : (byte)0;
                    }
                }
            }
        }

        [BurstCompile(OptimizeFor = OptimizeFor.Performance, DisableSafetyChecks = true)]
        private struct UpdateWorldTransformsJob : IJobParallelForBatch
        {
            [ReadOnly] public NativeArray<FloraInstanceHandle> InputInstances;
            [ReadOnly] public NativeArray<FloraInstanceTransform> InputTransforms;
            [ReadOnly] public NativeArray<GraphicsMatrix> PrevLocalToWorlds;

            [WriteOnly,NativeDisableParallelForRestriction] public NativeArray<GraphicsMatrix> LocalToWorlds;
            [WriteOnly,NativeDisableParallelForRestriction] public NativeArray<AABB> InstanceAABBs;
            [WriteOnly,NativeDisableParallelForRestriction] public NativeArray<byte> MovedThisFrame;
            [WriteOnly,NativeDisableParallelForRestriction] public NativeArray<byte> FlippedWinding;

            public void Execute(int startIndex, int count)
            {
                for (int i = 0; i < count; i++)
                {
                    var instance = InputInstances[startIndex + i];
                    var instanceInChunk = InstanceRegistry.Data.GetInstanceInChunk(instance);
                    if (Hint.Unlikely(instanceInChunk.Chunk == ChunkIndex.None))
                        continue;

                    var instanceIndex = instanceInChunk.Chunk * ChunkCapacity + instanceInChunk.IndexInChunk;
                    var localToWorld = InputTransforms[startIndex + i].ToLocalToWorld();
                    var prevLocalToWorld = PrevLocalToWorlds[instanceIndex];
                    var changed = !localToWorld.NearlyEquals(prevLocalToWorld);
                    if (Hint.Likely(changed))
                    {
                        AABB localAABB = instanceInChunk.Chunk.LocalAABB;
                        AABB worldAABB = AABB.TransformAABB(localToWorld, localAABB);

                        LocalToWorlds[instanceIndex] = localToWorld;
                        InstanceAABBs[instanceIndex] = worldAABB;

                        MovedThisFrame[instanceIndex] = 1;
                        FlippedWinding[instanceIndex] = localToWorld.IsFlipped ? (byte)1 : (byte)0;
                    }
                }
            }
        }

        [BurstCompile(OptimizeFor = OptimizeFor.Performance, DisableSafetyChecks = true)]
        private struct UpdateLocalTransformsJob : IJobParallelForBatch
        {
            public FloraLocalToWorld ParentLocalToWorld;
            [ReadOnly] public NativeArray<FloraInstanceHandle> InputInstances;
            [ReadOnly] public NativeArray<FloraInstanceTransform> InputTransforms;
            [ReadOnly] public NativeArray<GraphicsMatrix> PrevLocalToWorlds;

            [WriteOnly,NativeDisableParallelForRestriction] public NativeArray<GraphicsMatrix> LocalToWorlds;
            [WriteOnly,NativeDisableParallelForRestriction] public NativeArray<AABB> InstanceAABBs;
            [WriteOnly,NativeDisableParallelForRestriction] public NativeArray<byte> MovedThisFrame;
            [WriteOnly,NativeDisableParallelForRestriction] public NativeArray<byte> FlippedWinding;

            public void Execute(int startIndex, int count)
            {
                for (int i = 0; i < count; i++)
                {
                    var instance = InputInstances[startIndex + i];
                    var instanceInChunk = InstanceRegistry.Data.GetInstanceInChunk(instance);
                    if (Hint.Unlikely(instanceInChunk.Chunk == ChunkIndex.None))
                        continue;

                    var instanceIndex = instanceInChunk.Chunk * ChunkCapacity + instanceInChunk.IndexInChunk;
                    var localToWorld = ParentLocalToWorld.Transform(InputTransforms[startIndex + i]);
                    var prevLocalToWorld = PrevLocalToWorlds[instanceIndex];
                    var changed = !localToWorld.NearlyEquals(prevLocalToWorld);
                    if (Hint.Likely(changed))
                    {
                        AABB localAABB = instanceInChunk.Chunk.LocalAABB;
                        AABB worldAABB = AABB.TransformAABB(localToWorld, localAABB);

                        LocalToWorlds[instanceIndex] = localToWorld;
                        InstanceAABBs[instanceIndex] = worldAABB;

                        MovedThisFrame[instanceIndex] = 1;
                        FlippedWinding[instanceIndex] = localToWorld.IsFlipped ? (byte)1 : (byte)0;
                    }
                }
            }
        }

        [BurstCompile]
        private static void WriteLocalToWorldsBatched(
            InstanceManager* data,
            FloraInstanceHandle* instances,
            FloraLocalToWorld* srcLocalToWorlds,
            int count)
        {
            FloraLocalToWorld* dstLocalToWorlds = (FloraLocalToWorld*)data->m_InstanceLocalToWorld.GetUnsafePtr();
            InstanceBatchInChunk instanceBatch = default;

            int srcArrayStart = 0;
            int dstArrayStart = 0;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            void FlushBatch()
            {
                if (instanceBatch.Chunk == ChunkIndex.None || instanceBatch.Count == 0)
                    return;

                var src = srcLocalToWorlds + srcArrayStart;
                var dst = dstLocalToWorlds + dstArrayStart;

                if (instanceBatch.Count > MemCpyThreshold)
                {
                    UnsafeUtility.MemCpy(dst, src, instanceBatch.Count * sizeof(FloraLocalToWorld));
                }
                else
                {
                    for (int i = 0; i < instanceBatch.Count; i++)
                        dst[i] = src[i];
                }

                data->MarkChunkTransformDirty(instanceBatch.Chunk);

                instanceBatch.Chunk = ChunkIndex.None;
                instanceBatch.Count = 0;
            }

            // Try to coalesce contiguous destinations (and, when lucky, contiguous sources) *without* sorting

            var instanceInChunk = InstanceRegistry.Data.GetInstanceInChunk(instances[0]);
            instanceBatch.Chunk = instanceInChunk.Chunk;
            instanceBatch.StartIndex = instanceInChunk.IndexInChunk;
            instanceBatch.Count = 1;
            srcArrayStart = 0;
            dstArrayStart = instanceInChunk.Chunk * ChunkCapacity + instanceInChunk.IndexInChunk;

            for (int i = 1; i < count; i++)
            {
                instanceInChunk = InstanceRegistry.Data.GetInstanceInChunk(instances[i]);
                if (Hint.Unlikely(instanceInChunk.Chunk == ChunkIndex.None))
                {
                    FlushBatch();
                    continue;
                }

                var chunkBreak = (instanceInChunk.Chunk != instanceBatch.Chunk);
                var indexBreak = (instanceInChunk.IndexInChunk != (instanceBatch.StartIndex + instanceBatch.Count));
                var runBreak = chunkBreak || indexBreak;
                if (runBreak)
                {
                    FlushBatch();
                    instanceBatch.Chunk = instanceInChunk.Chunk;
                    instanceBatch.StartIndex = instanceInChunk.IndexInChunk;
                    srcArrayStart = i;
                    dstArrayStart = instanceInChunk.Chunk * ChunkCapacity + instanceInChunk.IndexInChunk;
                    instanceBatch.Count = 1;
                }
                else
                {
                    instanceBatch.Count++;
                }
            }

            FlushBatch();
        }

        #endregion
    }
}
