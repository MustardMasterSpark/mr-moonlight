// Copyright © Magnetic Arcade. All Rights Reserved.

using System;
using System.Runtime.CompilerServices;
using Unity.Mathematics;
using UnityEngine;

namespace MA.Flora
{
    using static math;
    using float3 = float3;
    using float4 = float4;

    internal static class FrustumUtility
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryIntersectPlanes3(Plane p0, Plane p1, Plane p2, out float3 intersectionPoint)
        {
            float3 n0 = p0.normal;
            float3 n1 = p1.normal;
            float3 n2 = p2.normal;

            float determinant = dot(cross(n0, n1), n2);
            if (abs(determinant) < MathConstants.ZeroTolerance)
            {
                intersectionPoint = float3.zero;
                return false;
            }

            intersectionPoint = ((cross(n2, n1) * p0.distance) +
                                 (cross(n0, n2) * p1.distance) -
                                 (cross(n0, n1) * p2.distance)) / determinant;
            return true;
        }

        public static void ComputeCorners(ReadOnlySpan<Plane> planes, Span<float3> vertices)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (planes.Length != 6)
                throw new InvalidOperationException("Must have 6 planes to calculate corners.");
            if (vertices.Length != 8)
                throw new InvalidOperationException("Must have 8 vertices to calculate corners.");
#endif

            TryIntersectPlanes3(planes[0], planes[3], planes[4], out vertices[0]); // Near bottom left
            TryIntersectPlanes3(planes[1], planes[3], planes[4], out vertices[1]); // Near bottom right
            TryIntersectPlanes3(planes[0], planes[2], planes[4], out vertices[2]); // Near top left
            TryIntersectPlanes3(planes[1], planes[2], planes[4], out vertices[3]); // Near top right
            TryIntersectPlanes3(planes[0], planes[3], planes[5], out vertices[4]); // Far bottom left
            TryIntersectPlanes3(planes[1], planes[3], planes[5], out vertices[5]); // Far bottom right
            TryIntersectPlanes3(planes[0], planes[2], planes[5], out vertices[6]); // Far top left
            TryIntersectPlanes3(planes[1], planes[2], planes[5], out vertices[7]); // Far top right
        }

        public static unsafe void ComputeCorners(in float4x4 invViewProjectionMatrix, float z, Span<float3> vertices)
        {
            Span<float3> clipSpaceFrustumCorners = stackalloc float3[4]
            {
                new float3(-1, -1, z),
                new float3( 1, -1, z),
                new float3(-1,  1, z),
                new float3( 1,  1, z),
            };

            for (int i = 0; i < 4; ++i)
            {
                float4 projected = mul(invViewProjectionMatrix, new float4(clipSpaceFrustumCorners[i], 1.0f));
                vertices[i] = projected.xyz * (1.0f / projected.w);
            }
        }

        public static AxisAlignedBox ComputeBounds(ReadOnlySpan<Plane> frustumPlanes)
        {
            Span<float3> corners = stackalloc float3[8];
            ComputeCorners(frustumPlanes, corners);

            AxisAlignedBox bounds = AxisAlignedBox.Empty;
            for (int i = 0; i < 8; ++i)
                bounds.Encapsulate(corners[i]);

            return bounds;
        }

        // --- Intersection ---

        public static int ComputeSIMDPacketCount(int planeCount)
        {
            return (planeCount + 3) >> 2;
        }

        public static void InitializeSIMDPackets(ReadOnlySpan<Plane> planes, Span<FrustumSIMDPacket> packets)
        {
            for (int i = 0; i < planes.Length; i++)
            {
                ref FrustumSIMDPacket packet = ref packets[i >> 2];
                int element = i & 3;
                packet.Nx[element] = planes[i].normal.x;
                packet.Ny[element] = planes[i].normal.y;
                packet.Nz[element] = planes[i].normal.z;
                packet.D[element]  = planes[i].distance;
                packet.AbsNx[element] = abs(packet.Nx[element]);
                packet.AbsNy[element] = abs(packet.Ny[element]);
                packet.AbsNz[element] = abs(packet.Nz[element]);
            }

            // Populate the remaining planes with values that are always "in"
            for (int i = planes.Length; i < 4 * packets.Length; ++i)
            {
                ref FrustumSIMDPacket packet = ref packets[i >> 2];
                int element = i & 3;
                packet.Nx[element] = 1.0f;
                packet.Ny[element] = 0.0f;
                packet.Nz[element] = 0.0f;
                // This value was before hardcoded to 32786.0f.
                // It was causing the culling system to discard the rendering of entities having a X coordinate approximately less than -32786.
                // We could not find anything relying on this number, so the value has been increased to 1 billion
                packet.D[element] = 1e9f;
                packet.AbsNx[element] = 1.0f;
                packet.AbsNy[element] = 0.0f;
                packet.AbsNz[element] = 0.0f;
            }
        }

        public static FrustumIntersectResult IntersectSphere(ReadOnlySpan<Plane> planes, float3 center, float radius)
        {
            int count = 0;

            for (int i = 0; i < planes.Length; i++)
            {
                float d = dot(planes[i].normal, center) + planes[i].distance;
                if (d < -radius)
                    return FrustumIntersectResult.Outside;

                if (d > radius)
                    count++;
            }

            return (count == planes.Length) ? FrustumIntersectResult.Inside : FrustumIntersectResult.Partial;
        }

        public static FrustumIntersectResult IntersectBounds(ReadOnlySpan<Plane> planes, AABB aabb)
        {
            int count = 0;

            for (int i = 0; i < planes.Length; i++)
            {
                float3 normal = planes[i].normal;
                float distance = dot(normal, aabb.Center.xyz) + planes[i].distance;
                float radius = dot(aabb.Extent.xyz, abs(normal));
                if (distance + radius < 0)
                    return FrustumIntersectResult.Outside;

                if (distance > radius)
                    count++;
            }

            return (count == planes.Length) ? FrustumIntersectResult.Inside : FrustumIntersectResult.Partial;
        }

        public static FrustumIntersectResult IntersectBoundsSIMD(ReadOnlySpan<FrustumSIMDPacket> packets, AABB aabb)
        {
            float4 cx = aabb.Center.xxxx;
            float4 cy = aabb.Center.yyyy;
            float4 cz = aabb.Center.zzzz;

            float4 ex = aabb.Extent.xxxx;
            float4 ey = aabb.Extent.yyyy;
            float4 ez = aabb.Extent.zzzz;

            int4 outCounts = 0;
            int4 inCounts = 0;

            for (int i = 0; i < packets.Length; i++)
            {
                FrustumSIMDPacket packet = packets[i];
                float4 distances = packet.Nx * cx + packet.Ny * cy + packet.Nz * cz + packet.D;
                float4 radii = packet.AbsNx * ex + packet.AbsNy * ey + packet.AbsNz * ez;

                inCounts += (int4)(distances >= radii);
                outCounts += (int4)(distances + radii < 0);
            }

            int inCount = csum(inCounts);
            int outCount = csum(outCounts);
            if (outCount != 0)
                return FrustumIntersectResult.Outside;
            else
                return (inCount == 4 * packets.Length) ? FrustumIntersectResult.Inside : FrustumIntersectResult.Partial;
        }
    }
}
