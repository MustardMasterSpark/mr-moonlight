// Copyright © Magnetic Arcade. All Rights Reserved.

using System.Runtime.CompilerServices;
using Unity.Mathematics;
using UnityEngine;

namespace MA.Flora
{
    internal static class BoundsUtility
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsEmpty(this Bounds b)
        {
            return b.extents.x <= 0 || b.extents.y <= 0 || b.extents.z <= 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float GetBoundingRadius(this Bounds b)
        {
            return math.length(b.extents);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Bounds TransformBy(this Bounds b, float4x4 m)
        {
            if (math.any((float3)b.extents <= 0.0f))
                return new Bounds(math.transform(m, b.center), float3.zero);

            float4 center = new float4(b.center, 0);
            float4 extent = new float4(b.extents, 0);

            float4 newExtent = math.abs(extent.xxxx * m.c0);
            newExtent += math.abs(extent.yyyy * m.c1);
            newExtent += math.abs(extent.zzzz * m.c2);

            float4 newCenter = center.xxxx * m.c0;
            newCenter += center.yyyy * m.c1;
            newCenter += center.zzzz * m.c2;
            newCenter += m.c3;

            return new Bounds
            {
                center = newCenter.xyz,
                extents = newExtent.xyz
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float3 GetClosestPointTo(this Bounds b, float3 point)
        {
            return math.clamp(point, b.min, b.max);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IntersectsSphereSq(this Bounds b, float3 center, float radiusSq)
        {
            if (b.IsEmpty()) return false;
            float3 closestPoint = b.GetClosestPointTo(center);
            float distanceSq = math.distancesq(closestPoint, center);
            return distanceSq <= radiusSq;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IntersectsSphereSq2D(this Bounds b, float3 center, float radiusSq)
        {
            if (b.IsEmpty()) return false;
            float3 min = b.min;
            float3 max = b.max;
            float2 closestPoint = math.clamp(center.xz, min.xz, max.xz);
            float distanceSq = math.distancesq(closestPoint, center.xz);
            return distanceSq <= radiusSq;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IntersectsSphere(this Bounds b, float3 center, float radius)
        {
            return IntersectsSphereSq(b, center, radius * radius);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IntersectsSphere(this Bounds b, BoundingSphere sphere)
        {
            return IntersectsSphereSq(b, sphere.position, sphere.radius * sphere.radius);
        }
    }
}
