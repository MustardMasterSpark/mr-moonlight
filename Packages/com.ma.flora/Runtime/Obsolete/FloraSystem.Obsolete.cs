// Copyright © Magnetic Arcade. All Rights Reserved.

using System;
using Unity.Collections;
using UnityEngine;

namespace MA.Flora
{
    public partial struct FloraInstanceFilter
    {
        [Obsolete("Use OwnerGameObjectID instead. This authoring-named compatibility alias will be removed in a future version.")]
        public EntityId AuthoringGameObjectID
        {
            readonly get => OwnerGameObjectID;
            set => OwnerGameObjectID = value;
        }

        [Obsolete("Use ByOwner instead. This parent-named compatibility alias will be removed in a future version.")]
        public static FloraInstanceFilter ByParent(Transform parent)
            => ByOwner(parent);

        [Obsolete("Use ByIdentitySource instead. This prefab-named compatibility alias will be removed in a future version.")]
        public static FloraInstanceFilter ByPrefab(GameObject prefab)
            => ByIdentitySource(prefab);
    }

    [Obsolete]
    public enum FloraCullingPipeline
    {
        [InspectorName("Render Mesh (Legacy)")]
        RenderMesh = 0,
        [InspectorName("Batch Renderer Group")]
        BatchRendererGroup = 1,
    }

    partial class FloraSystem
    {
        [Obsolete("Flora now always uses the BatchRendererGroup culling pipeline. This setting has no effect.")]
        public FloraCullingPipeline CullingPipeline => FloraCullingPipeline.BatchRendererGroup;

        [Obsolete("MainLightOverride is no longer supported, nor has any effect. This property will be removed in a future version.")]
        public Light MainLightOverride => null;

        [Obsolete("This event is no longer used and will be removed in a future version.")]
        public static event Action BeginFrame;

        [Obsolete("This event is no longer used and will be removed in a future version.")]
        public static event Action DelayCall;

        [Obsolete("This event is no longer used and will be removed in a future version.")]
        public static event Action PostLateUpdate;

        [Obsolete("Use GetInstanceIdentitySource instead. This prefab-named compatibility alias will be removed in a future version.")]
        public GameObject GetInstancePrefab(FloraInstanceHandle instance)
            => GetInstanceIdentitySource(instance);

        [Obsolete("Use GetInstanceOwnerGameObject instead. This authoring-named compatibility alias will be removed in a future version.")]
        public GameObject GetAuthoringGameObjectOf(FloraInstanceHandle instance)
            => GetInstanceOwnerGameObject(instance);

        [Obsolete("Use GetInstanceOwnerTransform instead. This authoring-named compatibility alias will be removed in a future version.")]
        public Transform GetAuthoringTransformOf(FloraInstanceHandle instance)
            => GetInstanceOwnerTransform(instance);

        [Obsolete("Use GetInstanceOwnerTerrain instead. This authoring-named compatibility alias will be removed in a future version.")]
        public Terrain GetAuthoringTerrainOf(FloraInstanceHandle instance)
            => GetInstanceOwnerTerrain(instance);

        [Obsolete("Use GetInstanceOwnerTerrain instead. This method will be removed in a future version.")]
        public Terrain GetInstanceParentTerrain(FloraInstanceHandle instance)
            => GetInstanceOwnerTerrain(instance);

        [Obsolete("Use FindInstancesIntersectingSphere instead. This method will be removed in a future version.")]
        public NativeList<FloraInstanceHandle> FindInstancesInSphere(BoundingSphere sphere, Allocator allocator)
        {
            m_NativeContext.InstanceManager.ValueRW.FlushPendingSpatialUpdates();
            m_NativeContext.CullingGrid.FindInstancesIntersectingSphereWithBurst(sphere, allocator, out var instances);
            return instances;
        }

        [Obsolete("Use FindInstancesIntersectingSphereMatching instead. This method will be removed in a future version.")]
        public NativeList<FloraInstanceHandle> FindInstancesInSphereMatching(FloraInstanceFilter filter, BoundingSphere sphere, Allocator allocator)
        {
            m_NativeContext.InstanceManager.ValueRW.FlushPendingSpatialUpdates();
            m_NativeContext.CullingGrid.FindInstancesIntersectingSphereMatchingWithBurst(filter, sphere, allocator, out var instances);
            return instances;
        }

        [Obsolete("Use FindInstancesIntersectingSphereMatching instead. This prefab-named compatibility alias will be removed in a future version.")]
        public NativeList<FloraInstanceHandle> FindInstancesInSphereMatching(NativeArray<EntityId> prefabGameObjectIDs, BoundingSphere sphere, Allocator allocator)
        {
            m_NativeContext.InstanceManager.ValueRW.FlushPendingSpatialUpdates();
            m_NativeContext.CullingGrid.FindInstancesIntersectingSphereMatchingWithBurst(prefabGameObjectIDs, sphere, allocator, out var instances);
            return instances;
        }

        [Obsolete("Use FindInstancesIntersectingBox instead. This method will be removed in a future version.")]
        public NativeList<FloraInstanceHandle> FindInstancesInBounds(Bounds bounds, Allocator allocator)
        {
            m_NativeContext.InstanceManager.ValueRW.FlushPendingSpatialUpdates();
            m_NativeContext.CullingGrid.FindInstancesIntersectingBoxWithBurst(bounds, allocator, out var instances);
            return instances;
        }

        [Obsolete("Use FindInstancesIntersectingBoxMatching instead. This method will be removed in a future version.")]
        public NativeList<FloraInstanceHandle> FindInstancesInBoundsMatching(FloraInstanceFilter filter, Bounds bounds, Allocator allocator)
        {
            m_NativeContext.InstanceManager.ValueRW.FlushPendingSpatialUpdates();
            m_NativeContext.CullingGrid.FindInstancesIntersectingBoxMatchingWithBurst(filter, bounds, allocator, out var instances);
            return instances;
        }

        [Obsolete("Use FindInstancesIntersectingBoxMatching instead. This prefab-named compatibility alias will be removed in a future version.")]
        public NativeList<FloraInstanceHandle> FindInstancesInBoundsMatching(NativeArray<EntityId> prefabGameObjectIDs, Bounds bounds, Allocator allocator)
        {
            m_NativeContext.InstanceManager.ValueRW.FlushPendingSpatialUpdates();
            m_NativeContext.CullingGrid.FindInstancesIntersectingBoxMatchingWithBurst(prefabGameObjectIDs, bounds, allocator, out var instances);
            return instances;
        }
    }
}
