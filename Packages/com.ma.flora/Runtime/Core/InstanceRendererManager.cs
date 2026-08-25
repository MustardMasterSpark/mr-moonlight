// Copyright © Magnetic Arcade. All Rights Reserved.

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using MA.InternalBridge;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.Rendering;

namespace MA.Flora
{
    internal struct InstanceRendererIndex : IEquatable<InstanceRendererIndex>, IComparable<InstanceRendererIndex>
    {
        public static readonly InstanceRendererIndex None = default;

        public int Index;
        public bool IsCreated => Index > 0;

        public InstanceRendererIndex(int index) => Index = index;

        public int CompareTo(InstanceRendererIndex other) => Index - other.Index;
        public bool Equals(InstanceRendererIndex other) => Index == other.Index;
        public override bool Equals(object obj) => obj is InstanceRendererIndex other && Equals(other);
        public override int GetHashCode() => Index;
        public override string ToString() => Equals(None) ? "InstanceRendererIndex.None" : $"InstanceRendererIndex({Index})";

        public static implicit operator int(InstanceRendererIndex prefab) => prefab.Index;
        public static bool operator ==(InstanceRendererIndex a, InstanceRendererIndex b) => a.Index == b.Index;
        public static bool operator !=(InstanceRendererIndex a, InstanceRendererIndex b) => a.Index != b.Index;
    }

    [BurstCompile]
    internal sealed unsafe class InstanceRendererManager : IDisposable
    {
        private NativeDataReference<InstanceManager> m_InstanceManager;

        private int m_NextIndex;
        private UnsafeList<InstanceRendererIndex> m_FreeRendererIndices;
        private NativeParallelHashMap<EntityId, InstanceRendererIndex> m_RendererIndexHash;
        private NativeArray<FloraInstanceHandle> m_Instances;
        private NativeArray<EntityId> m_EntityIds;

        public InstanceRendererManager(InstanceContext instanceContext)
        {
            m_InstanceManager = instanceContext.InstanceManager;

            const int initialCapacity = 256;

            m_NextIndex = 1;
            m_FreeRendererIndices = new UnsafeList<InstanceRendererIndex>(initialCapacity, Allocator.Persistent);
            m_RendererIndexHash = new NativeParallelHashMap<EntityId, InstanceRendererIndex>(initialCapacity, Allocator.Persistent);
            m_Instances = new NativeArray<FloraInstanceHandle>(initialCapacity, Allocator.Persistent);
            m_EntityIds = new NativeArray<EntityId>(initialCapacity, Allocator.Persistent);
        }

        public void Dispose()
        {
            m_FreeRendererIndices.Dispose();
            m_Instances.Dispose();
            m_RendererIndexHash.Dispose();
            m_EntityIds.Dispose();
        }

        public GameObject GetGameObject(InstanceRendererIndex instanceRendererIndex)
        {
            if (instanceRendererIndex.Index < 0 || instanceRendererIndex.Index >= m_EntityIds.Length)
                return null;

            var componentId = m_EntityIds[instanceRendererIndex.Index];
            if (!componentId.IsValid())
                return null;

            return componentId.ToObject<GameObject>();
        }

        public void GetRenderSourceObjects(List<GameObject> sources)
        {
            sources.Clear();
            using var entityIds = m_RendererIndexHash.GetKeyArray(Allocator.Temp);
            foreach (var entityId in entityIds)
            {
                var instanceRenderer = entityId.ToObject<FloraInstanceRenderer>();
                if (instanceRenderer != null && instanceRenderer.RenderSource != null)
                    sources.Add(instanceRenderer.RenderSource);
            }
        }

        public void GetInstanceRendererObjects(NativeArray<FloraInstanceHandle> instances, List<GameObject> sources)
        {
            using (HashSetPool<GameObject>.Get(out var gameObjects))
            {
                foreach (var instance in instances)
                {
                    if (m_InstanceManager.ValueRO.Exists(instance))
                    {
                        var rendererId = InstanceRegistry.Data.GetInstanceRendererIndex(instance);
                        if (rendererId != InstanceRendererIndex.None)
                        {
                            GameObject go = GetGameObject(rendererId);
                            if (go != null)
                                gameObjects.Add(go);
                        }
                    }
                }

                sources.Clear();
                sources.AddRange(gameObjects);
            }
        }

        public void OnRendererChanged(UnityTypeDispatchData rendererChangedData)
        {
            if (rendererChangedData.changedID.Length > 0)
            {
                foreach (var entityId in rendererChangedData.changedID)
                {
                    if (!m_RendererIndexHash.TryGetValue(entityId, out _))
                        Register(entityId.ToObject<FloraInstanceRenderer>());
                }
            }

            if (rendererChangedData.destroyedID.Length > 0)
            {
                foreach (var entityId in rendererChangedData.destroyedID)
                {
                    if (m_RendererIndexHash.TryGetValue(entityId, out _))
                        Destroy(entityId.ToObject<FloraInstanceRenderer>());
                }
            }

            rendererChangedData.Dispose(default);
        }

        public void OnTransformDataChanged(in UnityTransformDispatchData rendererTransformData)
        {
            int count = rendererTransformData.transformedID.Length;
            if (count != 0)
            {
                var instanceHandles = new NativeArray<FloraInstanceHandle>(count, Allocator.TempJob);
                var localToWorlds = new NativeArray<FloraLocalToWorld>(count, Allocator.TempJob);

                var gatherHandle = new GetInstanceHandles {
                    ChangedIds = rendererTransformData.transformedID,
                    LocalToWorldMatrices = rendererTransformData.localToWorldMatrices,
                    RendererIndexHash = m_RendererIndexHash,
                    InstanceHandles = m_Instances,
                    OutHandles = instanceHandles,
                    OutLocalToWorlds = localToWorlds
                }.Schedule(count, 64);

                gatherHandle = new RemoveInvalidHandles {
                    InstanceHandles = instanceHandles,
                    NewLength = (int*)UnsafeUtility.AddressOf(ref count)
                }.Schedule(gatherHandle);

                gatherHandle.Complete();

                instanceHandles.ResizeArraySafe(count);
                localToWorlds.ResizeArraySafe(count);

                var updateLocalToWorldsHandle = m_InstanceManager.ValueRW.ScheduleUpdateLocalToWorlds(instanceHandles, localToWorlds, default);
                instanceHandles.Dispose(updateLocalToWorldsHandle);
                localToWorlds.Dispose(updateLocalToWorldsHandle);
                updateLocalToWorldsHandle.Complete();
            }
        }

        public bool Register(FloraInstanceRenderer instanceRenderer)
        {
            if (instanceRenderer == null)
                return false;

            var identitySource = instanceRenderer.IdentitySource;
            var renderSource = instanceRenderer.RenderSource;
            if (identitySource == null || renderSource == null)
                return false;

            if (!renderSource.TryGetInstanceRendererSupportError(out string error))
            {
                Debug.LogError($"FloraInstanceRenderer on '{renderSource.name}' cannot be rendered by Flora. {error}", instanceRenderer);
                return false;
            }

            TemplateSourceInfo sourceInfo = renderSource.ComputeTemplateSourceInfo();

            EntityId entityId = instanceRenderer.GetEntityId();
            if (!m_RendererIndexHash.TryGetValue(entityId, out InstanceRendererIndex instanceRendererIndex))
            {
                var instanceHandle = m_InstanceManager.ValueRW.CreateInstance(
                    identitySource,
                    renderSource,
                    instanceRenderer.transform,
                    sourceInfo.LightmapIndex,
                    sourceInfo.LightmapScaleOffset);

                instanceRendererIndex = new InstanceRendererIndex(m_FreeRendererIndices.Length > 0 ? m_FreeRendererIndices.Pop() : m_NextIndex++);
                if (instanceRendererIndex >= m_Instances.Length)
                {
                    int newCapacity = m_Instances.Length * 2;
                    m_Instances.ResizeArraySafe(newCapacity);
                    m_EntityIds.ResizeArraySafe(newCapacity);
                }

                m_RendererIndexHash.Add(entityId, instanceRendererIndex);
                m_Instances[instanceRendererIndex] = instanceHandle;
                m_EntityIds[instanceRendererIndex] = entityId;

                instanceRenderer.InstanceHandle = instanceHandle;
                InstanceRegistry.Data.SetInstanceRendererIndex(instanceHandle, instanceRendererIndex);
            }

            return true;
        }

        public void Destroy(FloraInstanceRenderer instanceRenderer)
        {
            if (instanceRenderer == null)
                return;

            EntityId entityId = instanceRenderer.GetEntityId();
            if (m_RendererIndexHash.TryGetValue(entityId, out var instanceRendererIndex))
            {
                var instanceHandle = m_Instances[instanceRendererIndex];
                InstanceRegistry.Data.SetInstanceRendererIndex(instanceHandle, InstanceRendererIndex.None);

                m_RendererIndexHash.Remove(entityId);
                m_FreeRendererIndices.Add(instanceRendererIndex);
                m_InstanceManager.ValueRW.Destroy(instanceHandle);

                m_Instances[instanceRendererIndex] = FloraInstanceHandle.Null;
                m_EntityIds[instanceRendererIndex] = EntityId.None;

                instanceRenderer.InstanceHandle = FloraInstanceHandle.Null;
            }
        }

        [BurstCompile(OptimizeFor = OptimizeFor.Performance, DisableSafetyChecks = true)]
        private struct GetInstanceHandles : IJobParallelFor
        {
            [ReadOnly] public NativeArray<EntityId> ChangedIds;
            [ReadOnly] public NativeArray<Matrix4x4> LocalToWorldMatrices;

            [ReadOnly] public NativeParallelHashMap<EntityId, InstanceRendererIndex> RendererIndexHash;
            [ReadOnly] public NativeArray<FloraInstanceHandle> InstanceHandles;

            [WriteOnly] public NativeArray<FloraInstanceHandle> OutHandles;
            [WriteOnly] public NativeArray<FloraLocalToWorld> OutLocalToWorlds;

            public void Execute(int index)
            {
                EntityId entityId = ChangedIds[index];
                if (RendererIndexHash.TryGetValue(entityId, out InstanceRendererIndex rendererIndex))
                {
                    OutHandles[index] = InstanceHandles[rendererIndex];
                    OutLocalToWorlds[index] = LocalToWorldMatrices[index];
                }
            }
        }

        [BurstCompile(OptimizeFor = OptimizeFor.Performance, DisableSafetyChecks = true)]
        private struct RemoveInvalidHandles : IJob
        {
            public NativeArray<FloraInstanceHandle> InstanceHandles;
            [NativeDisableUnsafePtrRestriction] public int* NewLength;

            public void Execute()
            {
                int count = 0;
                for (int i = 0; i < InstanceHandles.Length; i++)
                {
                    FloraInstanceHandle handle = InstanceHandles[i];
                    if (handle != FloraInstanceHandle.Null)
                    {
                        InstanceHandles[count] = handle;
                        count++;
                    }
                }

                *NewLength = count;
            }
        }
    }
}
