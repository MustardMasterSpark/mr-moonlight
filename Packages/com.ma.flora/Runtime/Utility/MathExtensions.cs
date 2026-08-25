// Copyright © Magnetic Arcade. All Rights Reserved.

using System.Runtime.CompilerServices;
using Unity.Mathematics;
using UnityEngine;
using static Unity.Mathematics.math;

namespace MA.Flora
{
    internal static class MathExtensions
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ComputePerpendicularAxes(this Vector3 v, out Vector3 axis1, out Vector3 axis2)
        {
            float nx = abs(v.x);
            float ny = abs(v.y);
            float nz = abs(v.z);

            if (nz > nx && nz > ny)	axis1 = float3(1, 0, 0);
            else					axis1 = float3(0, 0, 1);

            float3 tmp = axis1 - v * dot(axis1, v);
            axis1 = normalizesafe(tmp);
            axis2 = cross(axis1, v);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ComputePerpendicularAxes(this float3 v, out float3 axis1, out float3 axis2)
        {
            float nx = abs(v.x);
            float ny = abs(v.y);
            float nz = abs(v.z);

            if (nz > nx && nz > ny)	axis1 = float3(1, 0, 0);
            else					axis1 = float3(0, 0, 1);

            float3 tmp = axis1 - v * dot(axis1, v);
            axis1 = normalizesafe(tmp);
            axis2 = cross(axis1, v);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsNormalized(this float2 v, float tolerance = MathConstants.ZeroTolerance)
        {
            return abs((v.x * v.x + v.y * v.y) - 1f) <= tolerance;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsNormalized(this float3 v, float tolerance = MathConstants.ZeroTolerance)
        {
            return abs((v.x * v.x + v.y * v.y + v.z * v.z) - 1f) <= tolerance;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool NearlyEquals(this float f, float other, float tolerance = MathConstants.ZeroTolerance)
        {
            return MathUtility.NearlyEquals(f, other, tolerance);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool NearlyEquals(this float2 v, float2 other, float tolerance = MathConstants.ZeroTolerance)
        {
            return MathUtility.NearlyEquals(v, other, tolerance);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool NearlyEquals(this float3 v, float3 other, float tolerance = MathConstants.ZeroTolerance)
        {
            return MathUtility.NearlyEquals(v, other, tolerance);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool NearlyEquals(this float4 v, float4 other, float tolerance = MathConstants.ZeroTolerance)
        {
            return MathUtility.NearlyEquals(v, other, tolerance);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool NearlyEquals(this float2x2 m, float2x2 other, float tolerance = MathConstants.ZeroTolerance)
        {
            bool2 c0Equal = abs(m.c0 - other.c0) <= tolerance;
            bool2 c1Equal = abs(m.c1 - other.c1) <= tolerance;
            return all(c0Equal) && all(c1Equal);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool NearlyEquals(this float2x4 m, float2x4 other, float tolerance = MathConstants.ZeroTolerance)
        {
            bool2 c0Equal = abs(m.c0 - other.c0) <= tolerance;
            bool2 c1Equal = abs(m.c1 - other.c1) <= tolerance;
            bool2 c2Equal = abs(m.c2 - other.c2) <= tolerance;
            bool2 c3Equal = abs(m.c3 - other.c3) <= tolerance;
            return all(c0Equal) && all(c1Equal) && all(c2Equal) && all(c3Equal);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool NearlyEquals(this float3x3 m, float3x3 other, float tolerance = MathConstants.ZeroTolerance)
        {
            bool3 c0Equal = abs(m.c0 - other.c0) <= tolerance;
            bool3 c1Equal = abs(m.c1 - other.c1) <= tolerance;
            bool3 c2Equal = abs(m.c2 - other.c2) <= tolerance;
            return all(c0Equal) && all(c1Equal) && all(c2Equal);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool NearlyEquals(this float4x4 m, float4x4 other, float tolerance = MathConstants.ZeroTolerance)
        {
            bool4 c0Equal = abs(m.c0 - other.c0) <= tolerance;
            bool4 c1Equal = abs(m.c1 - other.c1) <= tolerance;
            bool4 c2Equal = abs(m.c2 - other.c2) <= tolerance;
            bool4 c3Equal = abs(m.c3 - other.c3) <= tolerance;
            return all(c0Equal) && all(c1Equal) && all(c2Equal) && all(c3Equal);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool NearlyZero(this float2 v, float tolerance = MathConstants.ZeroTolerance)
        {
            return all(abs(v) <= tolerance);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool NearlyZero(this float3 v, float tolerance = MathConstants.ZeroTolerance)
        {
            return all(abs(v) <= tolerance);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool NearlyZero(this float4 v, float tolerance = MathConstants.ZeroTolerance)
        {
            return all(abs(v) <= tolerance);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool NearlyZero(this float2x2 v, float tolerance = MathConstants.ZeroTolerance)
        {
            bool2 c0Zero = abs(v.c0) <= tolerance;
            bool2 c1Zero = abs(v.c1) <= tolerance;
            return all(c0Zero) && all(c1Zero);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool NearlyZero(this float3x3 v, float tolerance = MathConstants.ZeroTolerance)
        {
            bool3 c0Zero = abs(v.c0) <= tolerance;
            bool3 c1Zero = abs(v.c1) <= tolerance;
            bool3 c2Zero = abs(v.c2) <= tolerance;
            return all(c0Zero) && all(c1Zero) && all(c2Zero);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool NearlyZero(this float4x4 v, float tolerance = MathConstants.ZeroTolerance)
        {
            bool4 c0Zero = abs(v.c0) <= tolerance;
            bool4 c1Zero = abs(v.c1) <= tolerance;
            bool4 c2Zero = abs(v.c2) <= tolerance;
            bool4 c3Zero = abs(v.c3) <= tolerance;
            return all(c0Zero) && all(c1Zero) && all(c2Zero) && all(c3Zero);
        }
    }
}
