// Copyright © Magnetic Arcade. All Rights Reserved.

using System.Runtime.CompilerServices;
using Unity.Mathematics;
using static Unity.Mathematics.math;

namespace MA.Flora
{
    internal static class MathConstants
    {
        public const float ZeroTolerance = 1e-6f;
        public const float Epsilon = 1e-5f;
    }

    internal static class MathUtility
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsNegative(float a)
        {
            return asuint(a) >= 0x80000000;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool2 Nearly(float2 a, float2 b, float tolerance = MathConstants.ZeroTolerance)
        {
            return abs(b - a) <= tolerance;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool3 Nearly(float3 a, float3 b, float tolerance = MathConstants.ZeroTolerance)
        {
            return abs(b - a) <= tolerance;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool4 Nearly(float4 a, float4 b, float tolerance = MathConstants.ZeroTolerance)
        {
            return abs(b - a) <= tolerance;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool NearlyEquals(float a, float b, float tolerance = MathConstants.ZeroTolerance)
        {
            return abs(b - a) <= tolerance;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool NearlyEquals(float2 a, float2 b, float tolerance = MathConstants.ZeroTolerance)
        {
            return all(abs(b - a) <= tolerance);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool NearlyEquals(float3 a, float3 b, float tolerance = MathConstants.ZeroTolerance)
        {
            return all(abs(b - a) <= tolerance);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool NearlyEquals(float4 a, float4 b, float tolerance = MathConstants.ZeroTolerance)
        {
            return all(abs(b - a) <= tolerance);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Repeat(float t, float length)
        {
            return clamp(t - floor(t / length) * length, 0.0f, length);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float GridSnap(float value, float grid)
        {
            return (grid == 0) ? value : (floor((value + (grid / 2f)) / grid) * grid);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int DivideAndRoundUp(int dividend, int divisor)
        {
            return (dividend + divisor - 1) / divisor;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int DivideAndRoundDown(int dividend, int divisor)
        {
            return dividend / divisor;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int DivideAndRoundNearest(int dividend, int divisor)
        {
            return (dividend >= 0)
                ? (dividend + divisor / 2) / divisor
                : (dividend - divisor / 2 + 1) / divisor;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int CeilLogTwo(ulong x)
        {
            return 32 - lzcnt(x - 1);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int NextMultipleOf(int input, int alignPow2)
        {
            return (input + (alignPow2 - 1)) & (~(alignPow2 - 1));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long NextMultipleOf(long input, long alignPow2)
        {
            return (input + (alignPow2 - 1)) & (~(alignPow2 - 1));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ulong NextMultipleOf(ulong input, ulong alignPow2)
        {
            return (input + (alignPow2 - 1)) & (~(alignPow2 - 1));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int NextMultipleOfNonPow2(int input, int alignment)
        {
            return (input % alignment) == 0 ? input : ((input + alignment) - (input % alignment));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long NextMultipleOfNonPow2(long input, long alignment)
        {
            return (input % alignment) == 0 ? input : ((input + alignment) - (input % alignment));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ulong NextMultipleOfNonPow2(ulong input, ulong alignment)
        {
            return (input % alignment) == 0 ? input : ((input + alignment) - (input % alignment));
        }
    }
}
