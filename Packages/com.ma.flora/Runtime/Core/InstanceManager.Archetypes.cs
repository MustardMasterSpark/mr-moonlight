// Copyright © Magnetic Arcade. All Rights Reserved.

using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.SceneManagement;

namespace MA.Flora
{
    internal unsafe partial struct InstanceManager
    {
        private const int ArchetypeInitialCapacity = 16;
        private const int MaxPossibleArchetypeCount = 524288;

        internal struct ArchetypeStore
        {
            private struct StaticIdentifier
            {
                internal static readonly SharedStatic<ArchetypeStore> Ref = SharedStatic<ArchetypeStore>.GetOrCreate<StaticIdentifier>();
            }

            public static PerArchetypeData* Data => StaticIdentifier.Ref.Data.m_PerArchetypeData;

            public struct PerArchetypeData
            {
                public static PerArchetypeData Default => default;

                public ArchetypeKey Key;
                public int Version;
                public int ChunkCount;
                public int InstanceCount;
            }

            private PerArchetypeData* m_PerArchetypeData;

            [BurstDiscard]
            internal static void Initialize()
            {
                if (StaticIdentifier.Ref.Data.m_PerArchetypeData == null)
                {
                    var data = AllocatorManager.Allocate<PerArchetypeData>(Allocator.Persistent, MaxPossibleArchetypeCount);
                    StaticIdentifier.Ref.Data.m_PerArchetypeData = data;

                    void Shutdown()
                    {
                        AllocatorManager.Free(Allocator.Persistent, StaticIdentifier.Ref.Data.m_PerArchetypeData);
                        StaticIdentifier.Ref.Data.m_PerArchetypeData = null;
                    }

                    AppDomain.CurrentDomain.DomainUnload += (_, _) => Shutdown();
                    AppDomain.CurrentDomain.ProcessExit += (_, _) => Shutdown();
                }

                UnsafeUtility.MemClear(StaticIdentifier.Ref.Data.m_PerArchetypeData, sizeof(PerArchetypeData) * MaxPossibleArchetypeCount);
            }
        }

        private void SetArchetypeDataDirty(ArchetypeIndex archetype)
        {
            if (m_ArchetypeDataDirty.TryAdd(archetype))
            {
                m_PendingArchetypeDataUploads.Add(default, (uint)archetype.Index);
                UpdateContentVersion();
            }
        }

        private ArchetypeIndex FindOrCreateArchetype(in InstantiateParams instantiateParams)
        {
            return FindOrCreateArchetype(
                instantiateParams.AdditionalTags,
                instantiateParams.Scene,
                instantiateParams.Layer,
                instantiateParams.MaxRenderDistance,
                instantiateParams.LightmapIndex,
                instantiateParams.SceneCullingMask,
                instantiateParams.Template,
                instantiateParams.ContainerEntity);
        }

        private ArchetypeIndex FindOrCreateArchetype(
            InstanceTag tags,
            Scene scene,
            byte layer,
            float maxRenderDistance,
            int lightmapIndex,
            ulong sceneCullingMask,
            TemplateIndex template,
            EntityId containerEntity)
        {
            ArchetypeKey archetypeKey = new ArchetypeKey
            {
                Tags = InstanceTag.Enabled | tags,
                Scene = scene,
                Layer = layer,
                MaxRenderDistance = (ushort)math.floor(maxRenderDistance),
                LightmapIndex = lightmapIndex,
                Template = template,
                ContainerEntity = containerEntity,
#if UNITY_EDITOR
                SceneCullingMask = sceneCullingMask,
#endif
            };

            return FindOrCreateArchetype(archetypeKey);
        }

        private ArchetypeIndex FindOrCreateArchetype(ArchetypeKey archetypeKey)
        {
            if (m_CachedArchetype != ArchetypeIndex.None && m_CachedArchetypeKey.Equals(archetypeKey))
                return m_CachedArchetype;

            if (!m_ArchetypeLookup.TryGetValue(archetypeKey, out ArchetypeIndex archetype))
            {
                archetype = m_ArchetypeFreeList.Length > 0 ? m_ArchetypeFreeList.Pop() : new ArchetypeIndex(m_NextArchetypeIndex++);
                Assert.IsTrue(archetype > 0 && archetype < MaxPossibleArchetypeCount);

                if (archetype >= m_ArchetypeDefaultVariationColors.Length)
                {
                    int newCapacity = m_ArchetypeDefaultVariationColors.Length * 2;
                    m_ArchetypeDefaultVariationColors.ResizeArraySafe(newCapacity);
                    m_ArchetypeChunks.Resize(newCapacity);
                    m_ArchetypeChunksWithFreeSlots.Resize(newCapacity);
                }

                m_ArchetypeLookup.Add(archetypeKey, archetype);
                m_ArchetypeAllocated.Add(archetype);
                m_SceneHandleArchetypes.Add(GetSceneHandleRaw(archetypeKey.Scene), archetype);
                m_TemplateArchetypes.Add(archetypeKey.Template, archetype);

                ArchetypeStore.Data[archetype] = new ArchetypeStore.PerArchetypeData
                {
                    Key = archetypeKey,
                    Version = 1,
                    ChunkCount = 0,
                    InstanceCount = 0,
                };

                m_ArchetypeDefaultVariationColors[archetype] = archetypeKey.Template.InitialVariationColor;
            }

            m_CachedArchetype = archetype;
            m_CachedArchetypeKey = archetypeKey;

            return archetype;
        }

        private void DestroyArchetype(ArchetypeIndex archetype)
        {
            m_ArchetypeLookup.Remove(archetype.Key);
            m_ArchetypeAllocated.Remove(archetype);
            m_SceneHandleArchetypes.Remove(GetSceneHandleRaw(archetype.Key.Scene), archetype);
            m_TemplateArchetypes.Remove(archetype.Key.Template, archetype);

            m_ArchetypeChunks[archetype].Clear();
            m_ArchetypeChunksWithFreeSlots[archetype].Clear();
            m_ArchetypeFreeList.Add(archetype);

            ArchetypeStore.Data[archetype] = default;
            m_CachedArchetype = ArchetypeIndex.None;
            m_CachedArchetypeKey = default;
        }

        private void AddChunkToArchetype(ArchetypeIndex archetype, ChunkIndex chunk)
        {
            var chunks = m_ArchetypeChunks[archetype];
            chunk.IndexInArchetype = chunks.Length;
            chunks.Add(chunk);
            chunk.Archetype = archetype;
            archetype.Version++;
            archetype.ChunkCount = chunks.Length;

            if ((archetype.Key.Tags & InstanceTag.TerrainDetail) != 0)
                m_TerrainDetailChunks.Add(chunk);

            SetArchetypeDataDirty(archetype);
        }

        private void RemoveChunkFromArchetype(ArchetypeIndex archetype, ChunkIndex chunk)
        {
            var chunks = m_ArchetypeChunks[archetype];
            var chunkListIndex = chunk.IndexInArchetype;
            chunks.RemoveAtSwapBack(chunkListIndex);
            archetype.Version++;
            archetype.ChunkCount = chunks.Length;
            SetArchetypeDataDirty(archetype);

            if (chunkListIndex < chunks.Length)
            {
                var chunkThatMoved = chunks[chunkListIndex];
                chunkThatMoved.IndexInArchetype = chunkListIndex;
            }

            if ((archetype.Key.Tags & InstanceTag.TerrainDetail) != 0)
                m_TerrainDetailChunks.Remove(chunk);

            if (chunks.Length == 0)
            {
                DestroyArchetype(archetype);
            }
        }

        private void AddChunkToArchetypeFreeSlotList(ArchetypeIndex archetype, ChunkIndex chunk)
        {
            var chunksWithEmptySlots = m_ArchetypeChunksWithFreeSlots[archetype];
            chunk.IndexInArchetypeFreeSlotList = chunksWithEmptySlots.Length;
            chunksWithEmptySlots.Add(chunk);
        }

        private void RemoveChunkFromArchetypeFreeSlotList(ArchetypeIndex archetype, ChunkIndex chunk)
        {
            var chunksWithEmptySlots = m_ArchetypeChunksWithFreeSlots[archetype];
            var index = chunk.IndexInArchetypeFreeSlotList;
            Assert.IsTrue(index >= 0 && index < chunksWithEmptySlots.Length);
            Assert.IsTrue(chunksWithEmptySlots[index] == chunk);
            chunksWithEmptySlots.RemoveAtSwapBack(index);

            if (index < chunksWithEmptySlots.Length)
            {
                var chunkThatMoved = chunksWithEmptySlots[index];
                chunkThatMoved.IndexInArchetypeFreeSlotList = index;
            }
        }

        private bool TryGetArchetypeChunkWithFreeSlots(ArchetypeIndex archetype, out ChunkIndex chunk)
        {
            var chunksWithEmptySlots = m_ArchetypeChunksWithFreeSlots[archetype];
            if (chunksWithEmptySlots.Length > 0)
            {
                chunk = chunksWithEmptySlots[0];
                return true;
            }

            chunk = ChunkIndex.None;
            return false;
        }

        internal void OnTemplateHandleStateChanged(
            TemplateIndex template,
            TemplateStateChangeMask changeMask,
            TemplateLayoutIndex oldState,
            TemplateLayoutIndex newState)
        {
            if (template == TemplateIndex.None || changeMask == TemplateStateChangeMask.None)
                return;

            bool updateDomain = (changeMask & TemplateStateChangeMask.DomainChanged) != 0;
            bool updateCapabilities = (changeMask & TemplateStateChangeMask.CapabilityChanged) != 0;
            bool updateTemplateData = (changeMask & TemplateStateChangeMask.TemplateDataChanged) != 0;
            if (!updateDomain && !updateCapabilities && !updateTemplateData)
                return;

            foreach (ArchetypeIndex archetype in m_TemplateArchetypes.GetValuesForKey(template))
            {
                SetArchetypeDataDirty(archetype);

                NativeBuffer<ChunkIndex> chunks = m_ArchetypeChunks[archetype];
                for (int i = 0; i < chunks.Length; i++)
                {
                    ChunkIndex chunk = chunks[i];
                    if (updateDomain || updateCapabilities)
                        TemplateBufferTypeChanged(chunk, template, updateBatchAllocation: updateDomain);

                    if (updateTemplateData)
                        RefreshChunkTemplateData(chunk);
                }
            }

            if (updateDomain)
            {
                BatchDomainIndex batchDomain = template.BatchDomainIndex;
                NativeBuffer<CullingChunkIndex> cullingChunks = m_TemplateManager.ValueRO.CullingChunks[template];
                for (int i = 0; i < cullingChunks.Length; i++)
                {
                    m_CullingGrid.ValueRW.UpdateChunkBatchDomain(cullingChunks[i], batchDomain);
                    Assert.AreEqual(batchDomain, m_CullingGrid.ValueRO.GetChunkBatchDomain(cullingChunks[i]), "Culling chunk batch domain cache is stale.");
                }
            }
        }
    }
}
