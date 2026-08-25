// Copyright © Magnetic Arcade. All Rights Reserved.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.Mathematics;
using UnityEngine;
using static Unity.Mathematics.math;
using float4x4 = Unity.Mathematics.float4x4;

namespace MA.Flora
{
    [Serializable]
    [StructLayout(LayoutKind.Sequential)]
    internal struct AxisAlignedBox : IEquatable<AxisAlignedBox>
    {
        public static readonly AxisAlignedBox Empty    = new(float3(float.PositiveInfinity), float3(float.NegativeInfinity));
        public static readonly AxisAlignedBox Infinite = new(float3(float.NegativeInfinity), float3(float.PositiveInfinity));
        public static readonly AxisAlignedBox Zero     = new(float3(0), float3(0));

        public float3 Min;
        public float3 Max;

        public readonly bool IsEmpty
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => any(Max <= Min);
        }

        public readonly float Width
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => max(Max.x - Min.x, 0);
        }

        public readonly float Height
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => max(Max.y - Min.y, 0);
        }

        public readonly float Depth
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => max(Max.z - Min.z, 0);
        }

        public readonly float Volume
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Width * Height * Depth;
        }

        public readonly float MinDim
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => cmin(Size);
        }

        public readonly float MaxDim
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => cmax(Size);
        }

        public float3 Center
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            readonly get => (Min + Max) * 0.5f;
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set
            {
                float3 extents = Extent;
                Min = value - extents;
                Max = value + extents;
            }
        }

        public float3 Extent
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            readonly get => (Max - Min) * 0.5f;
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set
            {
                float3 center = Center;
                Min = center - value;
                Max = center + value;
            }
        }

        public float3 Size
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            readonly get => Max - Min;
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set
            {
                float3 center = Center;
                Min = center - value * 0.5f;
                Max = center + value * 0.5f;
            }
        }

        public readonly float DiagonalLength
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => length(Max - Min);
        }

        public readonly float DiagonalLengthSq
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => lengthsq(Max - Min);
        }

        public readonly float Radius
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => length(Extent);
        }

        public readonly float RadiusSq
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => lengthsq(Extent);
        }

        public readonly float SurfaceArea
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => 2.0f * (Width * (Height + Depth) + Height * Depth);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static AxisAlignedBox FromExtents(float3 center, float3 extent)
        {
            return new AxisAlignedBox(center - extent, center + extent);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public AxisAlignedBox(float3 min, float3 max)
        {
            Min = min;
            Max = max;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public AxisAlignedBox(float3 a, float3 b, float3 c)
        {
            Min = new float3(
                cmin(float3(a.x, b.x, c.x)),
                cmin(float3(a.y, b.y, c.y)),
                cmin(float3(a.z, b.z, c.z)));
            Max = new float3(
                cmax(float3(a.x, b.x, c.x)),
                cmax(float3(a.y, b.y, c.y)),
                cmax(float3(a.z, b.z, c.z)));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public AxisAlignedBox(Bounds bounds)
        {
            Min = bounds.min;
            Max = bounds.max;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public BoundingSphere GetBoundingSphere()
        {
            return new BoundingSphere(Center, Radius);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly float Dimension(int axisIndex)
        {
            return max(Max[axisIndex] - Min[axisIndex], 0);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Encapsulate(float3 point)
        {
            Min = min(Min, point);
            Max = max(Max, point);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Encapsulate(AxisAlignedBox other)
        {
            Min = min(Min, other.Min);
            Max = max(Max, other.Max);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly bool Contains(float3 point)
        {
            return all(point >= Min & point <= Max);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly bool Contains(AxisAlignedBox rhs)
        {
            return all(Min <= rhs.Min & Max >= rhs.Max);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly bool IsInside(AxisAlignedBox rhs)
        {
            return rhs.Contains(this);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly float3 GetClosestPointTo(float3 point)
        {
            return clamp(point, Min, Max);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly float DistanceSquared(float3 point)
        {
            return distancesq(GetClosestPointTo(point), point);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly float DistanceSquared(AxisAlignedBox other)
        {
            return lengthsq(max(0, abs(other.Center - Center) - (other.Extent + Extent)));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly bool IntersectsSphereSq(float3 center, float radiusSq)
        {
            float3 q = clamp(center, Min, Max);
            float3 d = center - q;
            return dot(d, d) <= radiusSq;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly bool IntersectsSphere(float3 center, float radius)
        {
            return IntersectsSphereSq(center, radius * radius);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly bool IntersectsSphere(BoundingSphere sphere)
        {
            return IntersectsSphereSq(sphere.position, sphere.radius * sphere.radius);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly bool IntersectsAABB(AxisAlignedBox other)
        {
            return all(Max >= other.Min & Min <= other.Max);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly AxisAlignedBox Intersect(AxisAlignedBox other)
        {
            AxisAlignedBox intersection = new AxisAlignedBox(max(Min, other.Min), min(Max, other.Max));
            return intersection.Height <= 0f || intersection.Width <= 0f || intersection.Depth <= 0f
                ? Empty
                : intersection;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly AxisAlignedBox Translate(float3 offset)
        {
            return IsEmpty ? Empty : new AxisAlignedBox(Min + offset, Max + offset);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly void ComputeCorners(Span<float3> cornerVertices)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (cornerVertices.Length != 8)
                throw new ArgumentException("Corner vertices must have a length of 8.", nameof(cornerVertices));
#endif

            cornerVertices[0] = float3(Min);
            cornerVertices[1] = float3(Min.x, Min.y, Max.z);
            cornerVertices[2] = float3(Min.x, Max.y, Min.z);
            cornerVertices[3] = float3(Max.x, Min.y, Min.z);
            cornerVertices[4] = float3(Max.x, Max.y, Min.z);
            cornerVertices[5] = float3(Max.x, Min.y, Max.z);
            cornerVertices[6] = float3(Min.x, Max.y, Max.z);
            cornerVertices[7] = float3(Max);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly AxisAlignedBox RotateBy(float3x3 m)
        {
            if (IsEmpty)
                return Empty;

            float3 t1 = m.c0.xyz * Min.xxx;
            float3 t2 = m.c0.xyz * Max.xxx;
            bool3 minMask = t1 < t2;
            AxisAlignedBox rotated = new AxisAlignedBox(select(t2, t1, minMask), select(t2, t1, !minMask));

            t1 = m.c1.xyz * Min.yyy;
            t2 = m.c1.xyz * Max.yyy;
            minMask = t1 < t2;
            rotated.Min += select(t2, t1, minMask);
            rotated.Max += select(t2, t1, !minMask);

            t1 = m.c2.xyz * Min.zzz;
            t2 = m.c2.xyz * Max.zzz;
            minMask = t1 < t2;
            rotated.Min += select(t2, t1, minMask);
            rotated.Max += select(t2, t1, !minMask);

            return rotated;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly AxisAlignedBox TransformBy(float4x4 m)
        {
            float3 newCenter = transform(m, Center);
            float3 newExtent = RotateExtent(Extent, m.c0.xyz, m.c1.xyz, m.c2.xyz);
            bool isEmpty = IsEmpty;
            float3 newMin = select(newCenter - newExtent, Min, isEmpty);
            float3 newMax = select(newCenter + newExtent, Max, isEmpty);
            return new AxisAlignedBox(newMin, newMax);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly AxisAlignedBox InverseTransformBy(float4x4 matrix)
        {
            return TransformBy(inverse(matrix));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float3 RotateExtent(float3 extents, float3 m0, float3 m1, float3 m2)
        {
            return abs(m0 * extents.x) + abs(m1 * extents.y) + abs(m2 * extents.z);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly AxisAlignedBox TransformProjectBy(float4x4 projectionMatrix)
        {
            if (IsEmpty) return Empty;

            Span<float3> corners = stackalloc float3[8];
            ComputeCorners(corners);

            AxisAlignedBox projectedBox = Empty;
            for (int i = 0; i < corners.Length; i++)
            {
                float4 projectedVertex = mul(projectionMatrix, float4(corners[i], 1.0f));
                projectedBox += projectedVertex.xyz * rcp(projectedVertex.w);
            }

            return projectedBox;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Expand(float radius)
        {
            Max += radius;
            Min -= radius;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override string ToString()
        {
            return $"AxisAlignedBox(Min={Min}, Max={Max})";
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public string ToString(string format, IFormatProvider formatProvider)
        {
            return $"AxisAlignedBox(Min={Min.ToString(format, formatProvider)}, Max={Max.ToString(format, formatProvider)})";
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static AxisAlignedBox operator +(AxisAlignedBox lhs, float3 rhs)
        {
            lhs.Encapsulate(rhs);
            return lhs;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static AxisAlignedBox operator +(AxisAlignedBox lhs, AxisAlignedBox rhs)
        {
            lhs.Encapsulate(rhs);
            return lhs;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly bool Equals(AxisAlignedBox other)
        {
            return this == other;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override readonly bool Equals(object o)
        {
            return o is AxisAlignedBox converted && Equals(converted);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override readonly int GetHashCode()
        {
            return (int)(csum(asuint(Min) * uint3(0x713BD06Fu, 0x753AD6ADu, 0xD19764C7u) +
                              asuint(Max) * uint3(0xB5D0BF63u, 0xF9102C5Fu, 0x9881FB9Fu)) + 0x4FC93C25u);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator ==(AxisAlignedBox lhs, AxisAlignedBox rhs)
        {
            return all(lhs.Min == rhs.Min) && all(lhs.Max == rhs.Max);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator !=(AxisAlignedBox lhs, AxisAlignedBox rhs)
        {
            return any(lhs.Min != rhs.Min) || any(lhs.Max != rhs.Max);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator Bounds(AxisAlignedBox rhs)
        {
            return new Bounds(rhs.Center, rhs.Size);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator AxisAlignedBox(Bounds rhs)
        {
            return new AxisAlignedBox(rhs.min, rhs.max);
        }
    }
}
