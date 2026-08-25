// Copyright © Magnetic Arcade. All Rights Reserved.

using Unity.Burst;
using Unity.Collections;
using UnityEngine;

namespace MA.Flora
{
    [BurstCompile]
    internal static class FloraSpatialHashAccess
    {
        [BurstCompile]
        public static void CullInstancesInSelectionPlanesWithBurst(
            this in NativeDataReference<CullingGrid> hash,
            InstanceTag includeTags, InstanceTag excludeTags,
            in NativeArray<Plane> planes, in AllocatorManager.AllocatorHandle allocator, out NativeArray<FloraInstanceHandle> instances)
        {
            instances = hash.ValueRO.CullInstancesInSelectionPlanes(includeTags, excludeTags, planes, allocator);
        }

        [BurstCompile]
        public static void FindInstancesIntersectingSphereWithBurst(
            this in NativeDataReference<CullingGrid> hash, in BoundingSphere sphere,
            in AllocatorManager.AllocatorHandle allocator, out NativeList<FloraInstanceHandle> instances)
        {
            instances = hash.ValueRO.FindInstancesIntersectingSphere(sphere, allocator);
        }

        [BurstCompile]
        public static void FindInstancesIntersectingSphereNonAllocWithBurst(
            this in NativeDataReference<CullingGrid> hash, in BoundingSphere sphere,
            ref NativeList<FloraInstanceHandle> instances)
        {
            hash.ValueRO.FindInstancesIntersectingSphere(sphere, instances);
        }

        [BurstCompile]
        public static void FindInstancesIntersectingSphereMatchingWithBurst(
            this in NativeDataReference<CullingGrid> hash, in FloraInstanceFilter filter, in BoundingSphere sphere,
            in AllocatorManager.AllocatorHandle allocator, out NativeList<FloraInstanceHandle> instances)
        {
            instances = hash.ValueRO.FindInstancesIntersectingSphereMatching(filter, sphere, allocator);
        }

        [BurstCompile]
        public static void FindInstancesIntersectingSphereMatchingNonAllocWithBurst(
            this in NativeDataReference<CullingGrid> hash, in FloraInstanceFilter filter, in BoundingSphere sphere,
            ref NativeList<FloraInstanceHandle> instances)
        {
            hash.ValueRO.FindInstancesIntersectingSphereMatching(filter, sphere, instances);
        }

        [BurstCompile]
        public static void FindInstancesIntersectingSphereMatchingWithBurst(
            this in NativeDataReference<CullingGrid> hash, in NativeArray<EntityId> prefabGameObjectIDs, in BoundingSphere sphere,
            in AllocatorManager.AllocatorHandle allocator, out NativeList<FloraInstanceHandle> instances)
        {
            instances = hash.ValueRO.FindInstancesIntersectingSphereMatching(prefabGameObjectIDs, sphere, allocator);
        }

        [BurstCompile]
        public static void FindInstancesIntersectingSphereMatchingNonAllocWithBurst(
            this in NativeDataReference<CullingGrid> hash, in NativeArray<EntityId> prefabGameObjectIDs, in BoundingSphere sphere,
            ref NativeList<FloraInstanceHandle> instances)
        {
            hash.ValueRO.FindInstancesIntersectingSphereMatching(prefabGameObjectIDs, sphere, instances);
        }

        [BurstCompile]
        public static void FindInstanceOriginsWithinSphereWithBurst(
            this in NativeDataReference<CullingGrid> hash, in BoundingSphere sphere,
            in AllocatorManager.AllocatorHandle allocator, out NativeList<FloraInstanceHandle> instances)
        {
            instances = hash.ValueRO.FindInstanceOriginsWithinSphere(sphere, allocator);
        }

        [BurstCompile]
        public static void FindInstanceOriginsWithinSphereNonAllocWithBurst(
            this in NativeDataReference<CullingGrid> hash, in BoundingSphere sphere,
            ref NativeList<FloraInstanceHandle> instances)
        {
            hash.ValueRO.FindInstanceOriginsWithinSphere(sphere, instances);
        }

        [BurstCompile]
        public static void FindInstanceOriginsWithinSphereMatchingWithBurst(
            this in NativeDataReference<CullingGrid> hash, in FloraInstanceFilter filter, in BoundingSphere sphere,
            in AllocatorManager.AllocatorHandle allocator, out NativeList<FloraInstanceHandle> instances)
        {
            instances = hash.ValueRO.FindInstanceOriginsWithinSphereMatching(filter, sphere, allocator);
        }

        [BurstCompile]
        public static void FindInstanceOriginsWithinSphereMatchingNonAllocWithBurst(
            this in NativeDataReference<CullingGrid> hash, in FloraInstanceFilter filter, in BoundingSphere sphere,
            ref NativeList<FloraInstanceHandle> instances)
        {
            hash.ValueRO.FindInstanceOriginsWithinSphereMatching(filter, sphere, instances);
        }

        [BurstCompile]
        public static void FindInstanceOriginsWithinSphereMatchingWithBurst(
            this in NativeDataReference<CullingGrid> hash, in NativeArray<EntityId> prefabGameObjectIDs, in BoundingSphere sphere,
            in AllocatorManager.AllocatorHandle allocator, out NativeList<FloraInstanceHandle> instances)
        {
            instances = hash.ValueRO.FindInstanceOriginsWithinSphereMatching(prefabGameObjectIDs, sphere, allocator);
        }

        [BurstCompile]
        public static void FindInstanceOriginsWithinSphereMatchingNonAllocWithBurst(
            this in NativeDataReference<CullingGrid> hash, in NativeArray<EntityId> prefabGameObjectIDs, in BoundingSphere sphere,
            ref NativeList<FloraInstanceHandle> instances)
        {
            hash.ValueRO.FindInstanceOriginsWithinSphereMatching(prefabGameObjectIDs, sphere, instances);
        }

        [BurstCompile]
        public static void FindInstancesIntersectingBoxWithBurst(
            this in NativeDataReference<CullingGrid> hash, in AxisAlignedBox bounds,
            in AllocatorManager.AllocatorHandle allocator, out NativeList<FloraInstanceHandle> instances)
        {
            instances = hash.ValueRO.FindInstancesIntersectingBox(bounds, allocator);
        }

        [BurstCompile]
        public static void FindInstancesIntersectingBoxNonAllocWithBurst(
            this in NativeDataReference<CullingGrid> hash, in AxisAlignedBox bounds,
            ref NativeList<FloraInstanceHandle> instances)
        {
            hash.ValueRO.FindInstancesIntersectingBox(bounds, instances);
        }

        [BurstCompile]
        public static void FindInstancesIntersectingBoxMatchingWithBurst(
            this in NativeDataReference<CullingGrid> hash, in FloraInstanceFilter filter, in AxisAlignedBox bounds,
            in AllocatorManager.AllocatorHandle allocator, out NativeList<FloraInstanceHandle> instances)
        {
            instances = hash.ValueRO.FindInstancesIntersectingBoxMatching(filter, bounds, allocator);
        }

        [BurstCompile]
        public static void FindInstancesIntersectingBoxMatchingNonAllocWithBurst(
            this in NativeDataReference<CullingGrid> hash, in FloraInstanceFilter filter, in AxisAlignedBox bounds,
            ref NativeList<FloraInstanceHandle> instances)
        {
            hash.ValueRO.FindInstancesIntersectingBoxMatching(filter, bounds, instances);
        }

        [BurstCompile]
        public static void FindInstancesIntersectingBoxMatchingWithBurst(
            this in NativeDataReference<CullingGrid> hash, in NativeArray<EntityId> prefabGameObjectIDs, in AxisAlignedBox bounds,
            in AllocatorManager.AllocatorHandle allocator, out NativeList<FloraInstanceHandle> instances)
        {
            instances = hash.ValueRO.FindInstancesIntersectingBoxMatching(prefabGameObjectIDs, bounds, allocator);
        }

        [BurstCompile]
        public static void FindInstancesIntersectingBoxMatchingNonAllocWithBurst(
            this in NativeDataReference<CullingGrid> hash, in NativeArray<EntityId> prefabGameObjectIDs, in AxisAlignedBox bounds,
            ref NativeList<FloraInstanceHandle> instances)
        {
            hash.ValueRO.FindInstancesIntersectingBoxMatching(prefabGameObjectIDs, bounds, instances);
        }

        [BurstCompile]
        public static void FindInstanceOriginsWithinBoxWithBurst(
            this in NativeDataReference<CullingGrid> hash, in AxisAlignedBox bounds,
            in AllocatorManager.AllocatorHandle allocator, out NativeList<FloraInstanceHandle> instances)
        {
            instances = hash.ValueRO.FindInstanceOriginsWithinBox(bounds, allocator);
        }

        [BurstCompile]
        public static void FindInstanceOriginsWithinBoxNonAllocWithBurst(
            this in NativeDataReference<CullingGrid> hash, in AxisAlignedBox bounds,
            ref NativeList<FloraInstanceHandle> instances)
        {
            hash.ValueRO.FindInstanceOriginsWithinBox(bounds, instances);
        }

        [BurstCompile]
        public static void FindInstanceOriginsWithinBoxMatchingWithBurst(
            this in NativeDataReference<CullingGrid> hash, in FloraInstanceFilter filter, in AxisAlignedBox bounds,
            in AllocatorManager.AllocatorHandle allocator, out NativeList<FloraInstanceHandle> instances)
        {
            instances = hash.ValueRO.FindInstanceOriginsWithinBoxMatching(filter, bounds, allocator);
        }

        [BurstCompile]
        public static void FindInstanceOriginsWithinBoxMatchingNonAllocWithBurst(
            this in NativeDataReference<CullingGrid> hash, in FloraInstanceFilter filter, in AxisAlignedBox bounds,
            ref NativeList<FloraInstanceHandle> instances)
        {
            hash.ValueRO.FindInstanceOriginsWithinBoxMatching(filter, bounds, instances);
        }

        [BurstCompile]
        public static void FindInstanceOriginsWithinBoxMatchingWithBurst(
            this in NativeDataReference<CullingGrid> hash, in NativeArray<EntityId> prefabGameObjectIDs, in AxisAlignedBox bounds,
            in AllocatorManager.AllocatorHandle allocator, out NativeList<FloraInstanceHandle> instances)
        {
            instances = hash.ValueRO.FindInstanceOriginsWithinBoxMatching(prefabGameObjectIDs, bounds, allocator);
        }

        [BurstCompile]
        public static void FindInstanceOriginsWithinBoxMatchingNonAllocWithBurst(
            this in NativeDataReference<CullingGrid> hash, in NativeArray<EntityId> prefabGameObjectIDs, in AxisAlignedBox bounds,
            ref NativeList<FloraInstanceHandle> instances)
        {
            hash.ValueRO.FindInstanceOriginsWithinBoxMatching(prefabGameObjectIDs, bounds, instances);
        }
    }
}
