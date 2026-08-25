// Copyright © Magnetic Arcade. All Rights Reserved.

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Unity.Collections.LowLevel.Unsafe;

namespace MA.Flora
{
    internal static unsafe class UnsafeBitArrayExtensions
    {
        public static int FindFirst(this UnsafeBitArray bitArray, bool value, int startIndex = 0, int count = -1)
        {
            if (count < 0) count = bitArray.Length - startIndex;
            CheckArgs(bitArray, startIndex, count);
            return BitUtility.FindFirst(value, bitArray.Ptr, startIndex, count);
        }

        public static int FindLast(this UnsafeBitArray bitArray, bool value, int startIndex = 0, int count = -1)
        {
            if (count < 0) count = bitArray.Length - startIndex;
            CheckArgs(bitArray, startIndex, count);
            return BitUtility.FindLast(value, bitArray.Ptr, startIndex, count);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int FindFirstSetBit(this UnsafeBitArray bitArray, int startIndex = 0, int count = -1)
        {
            return FindFirst(bitArray, true, startIndex, count);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int FindLastSetBit(this UnsafeBitArray bitArray, int startIndex = 0, int count = -1)
        {
            return FindLast(bitArray, true, startIndex, count);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int FindFirstZeroBit(this UnsafeBitArray bitArray, int startIndex = 0, int count = -1)
        {
            return FindFirst(bitArray, false, startIndex, count);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int FindLastZeroBit(this UnsafeBitArray bitArray, int startIndex = 0, int count = -1)
        {
            return FindLast(bitArray, false, startIndex, count);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static SetBitEnumerator<int> SetBitEnumerator(this UnsafeBitArray bitArray)
        {
            if (bitArray.Length == 0) return default;
            return new SetBitEnumerator<int>(bitArray.Ptr, 0, bitArray.Length);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static SetBitEnumerator<int> SetBitEnumerator(this UnsafeBitArray bitArray, int pos, int numBits)
        {
            if (bitArray.Length == 0) return default;
            CheckArgs(bitArray, pos, numBits);
            return new SetBitEnumerator<int>(bitArray.Ptr, pos, numBits);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        private static void CheckArgs(UnsafeBitArray bitArray, int pos, int numBits)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (pos < 0
                || pos >= bitArray.Length
                || numBits < 1)
            {
                throw new ArgumentException($"BitArray invalid arguments: pos {pos} (must be 0-{bitArray.Length - 1}), numBits {numBits} (must be greater than 0).");
            }
#endif
        }
    }
}
