using System;
using System.Runtime.CompilerServices;
using Unity.Mathematics;
using UnityEngine;

namespace MA.Flora
{
    internal struct AABB : IEquatable<AABB>
    {
        public static AABB Empty => new AABB(float3.zero, float3.zero);

        public float4 Center;
        public float4 Extent;

        public float4 Min => Center - Extent;
        public float4 Max => Center + Extent;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public AABB(float3 center, float3 extent)
        {
            Center = new float4(center, 0);
            Extent = new float4(extent, 0);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public AABB(float4 center, float4 extent)
        {
            Center = new float4(center.xyz, 0);
            Extent = new float4(extent.xyz, 0);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsEmpty() => math.all(Extent.xyz == 0);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Contains(float4 point)
        {
            float4 delta = math.abs(point - Center);
            return math.all(delta <= Extent);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IntersectsSphere(AABB aabb, float3 sphereCenter, float sphereRadius)
        {
            return IntersectsSphere(aabb, new float4(sphereCenter, 0), sphereRadius);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IntersectsSphere(AABB aabb, float4 sphereCenter, float sphereRadius)
        {
            float4 aabbMin = aabb.Center - aabb.Extent;
            float4 aabbMax = aabb.Center + aabb.Extent;
            float4 closestPoint = math.clamp(sphereCenter, aabbMin, aabbMax);
            float4 delta = closestPoint - sphereCenter;
            return math.dot(delta, delta) <= sphereRadius * sphereRadius;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IntersectsAABB(AABB a, AABB b)
        {
            float4 delta = math.abs(a.Center - b.Center);
            float4 extents = a.Extent + b.Extent;
            return math.all(delta.xyz <= extents.xyz);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static AABB TransformAABB(float4x4 matrix, AABB aabb)
        {
            float4 center = math.mul(matrix, new float4(aabb.Center.xyz, 1f));
            float4 extent = math.abs(matrix.c0) * aabb.Extent.xxxx +
                            math.abs(matrix.c1) * aabb.Extent.yyyy +
                            math.abs(matrix.c2) * aabb.Extent.zzzz;
            return new AABB(center, extent);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Bounds ToBounds() => new Bounds(Center.xyz, Extent.xyz * 2);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public AxisAlignedBox ToBox() => new AxisAlignedBox(Center.xyz - Extent.xyz, Center.xyz + Extent.xyz);


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(AABB other) => Center.Equals(other.Center) && Extent.Equals(other.Extent);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override bool Equals(object obj) => obj is AABB other && Equals(other);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override int GetHashCode() => (Center.GetHashCode() * 397) ^ Extent.GetHashCode();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override string ToString() => $"AABB(Center={Center}, Extent={Extent})";


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator AABB(Bounds bounds) => new AABB(bounds.center, bounds.extents);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator AABB(AxisAlignedBox box) => new AABB(box.Center, box.Extent);
    }

    internal struct AABBMinMax : IEquatable<AABBMinMax>
    {
        public static AABBMinMax Empty => new AABBMinMax(math.float4(float.PositiveInfinity), math.float4(float.NegativeInfinity));

        public float4 Min;
        public float4 Max;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public AABBMinMax(float3 min, float3 max)
        {
            Min = new float4(min, 0);
            Max = new float4(max, 0);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public AABBMinMax(float4 min, float4 max)
        {
            Min = new float4(min.xyz, 0);
            Max = new float4(max.xyz, 0);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsEmpty() => math.any(Max.xyz <= Min.xyz);


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Bounds ToBounds() => new Bounds((Min.xyz + Max.xyz) * 0.5f, (Max.xyz - Min.xyz));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public AxisAlignedBox ToBox() => new AxisAlignedBox(Min.xyz, Max.xyz);


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(AABBMinMax other) => Min.Equals(other.Min) && Max.Equals(other.Max);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override bool Equals(object obj) => obj is AABB other && Equals(other);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override int GetHashCode() => (Min.GetHashCode() * 397) ^ Max.GetHashCode();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override string ToString() => $"AABBMinMax(Min={Min}, Max={Max})";


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator AABBMinMax(AABB aabb) => new AABBMinMax(aabb.Center - aabb.Extent, aabb.Center + aabb.Extent);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator AABB(AABBMinMax aabb) => new AABB((aabb.Min + aabb.Max) * 0.5f, (aabb.Max - aabb.Min) * 0.5f);


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static AABBMinMax operator +(AABBMinMax a, AABBMinMax b) => new AABBMinMax(math.min(a.Min, b.Min), math.max(a.Max, b.Max));
    }
}
