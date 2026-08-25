using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using MA.InternalBridge;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using UnityEngine.Assertions;

namespace MA.Flora
{
    internal struct ParallelBitArray : IDisposable
    {
        internal NativeArray<long> m_Bits;
        internal int m_Length;

        public readonly bool IsCreated
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => m_Bits.IsCreated;
        }

        public readonly int Length
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => m_Length;
        }

        public readonly int ChunkLength
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => m_Bits.Length;
        }

        public ParallelBitArray(int length, Allocator allocator, NativeArrayOptions options = NativeArrayOptions.ClearMemory)
        {
            m_Bits = new NativeArray<long>((length + 63) / 64, allocator, options);
            m_Length = length;
        }

        public ParallelBitArray(int length, ref RewindableAllocator allocator, NativeArrayOptions options = NativeArrayOptions.ClearMemory)
        {
            m_Bits = CollectionHelper.CreateNativeArray<long, RewindableAllocator>((length + 63) / 64, ref allocator, options);
            m_Length = length;
        }

        public static unsafe ParallelBitArray FromExternal(ulong* ptr, int length, Allocator allocator)
        {
            ParallelBitArray parallelBitArray;
            parallelBitArray.m_Bits = NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<long>(ptr, (length + 63) / 64, allocator);
            parallelBitArray.m_Length = length;
            return parallelBitArray;
        }

#if ENABLE_UNITY_COLLECTIONS_CHECKS
        internal void SetAtomicSafetyHandle(AtomicSafetyHandle handle)
        {
            NativeArrayUnsafeUtility.SetAtomicSafetyHandle(ref m_Bits, handle);
        }
#endif

        public void Dispose()
        {
            m_Bits.Dispose();
            m_Length = 0;
        }

        public void Dispose(JobHandle inputDeps)
        {
            m_Bits.Dispose(inputDeps);
            m_Length = 0;
        }

        public void Resize(int newLength)
        {
            int oldLength = m_Length;
            if (newLength == oldLength)
                return;

            int oldBitsLength = m_Bits.Length;
            int newBitsLength = (newLength + 63) / 64;
            if (newBitsLength != oldBitsLength)
            {
                var newBits = new NativeArray<long>(newBitsLength, m_Bits.GetAllocatorLabel(), NativeArrayOptions.UninitializedMemory);
                if (m_Bits.IsCreated)
                {
                    int copyLength = Math.Min(oldBitsLength, newBitsLength);
                    NativeArray<long>.Copy(m_Bits, newBits, copyLength);
                    m_Bits.Dispose();
                }

                m_Bits = newBits;
            }

            // mask off bits past the length
            int validLength = Math.Min(oldLength, newLength);
            int validBitsLength = Math.Min(oldBitsLength, newBitsLength);
            for (int chunkIndex = validBitsLength; chunkIndex < m_Bits.Length; ++chunkIndex)
            {
                int validBitCount = Math.Max(validLength - 64 * chunkIndex, 0);
                if (validBitCount < 64)
                {
                    ulong validMask = (1ul << validBitCount) - 1;
                    m_Bits[chunkIndex] &= (long)validMask;
                }
            }

            m_Length = newLength;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly bool IsValidIndex(int index)
        {
            return 0 <= index && index < m_Length;
        }

        public void FillZeroes(int length = -1)
        {
            if (length < 0) length = m_Length;
            length = Math.Min(length, m_Length);
            int chunkIndex = length / 64;
            int remainder = length & 63;

            m_Bits.MemClear(0, chunkIndex);

            if (remainder > 0)
            {
                long lastChunkMask = (1L << remainder) - 1;
                m_Bits[chunkIndex] &= ~lastChunkMask;
            }
        }

        public void FillOnes(int length = -1)
        {
            if (length < 0) length = m_Length;
            length = Math.Min(length, m_Length);
            int chunkIndex = length / 64;
            int remainder = length & 63;

            for (int i = 0; i < chunkIndex; ++i)
                m_Bits[i] = -1L;

            if (remainder > 0)
            {
                long lastChunkMask = (1L << remainder) - 1;
                m_Bits[chunkIndex] |= lastChunkMask;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Set(int index, bool value)
        {
            unsafe
            {
                Assert.IsTrue(0 <= index && index < m_Length);

                int entryIndex = index >> 6;
                long* entries = (long*)m_Bits.GetUnsafePtr();

                ulong bit = 1ul << (index & 0x3f);
                long andMask = (long)(~bit);
                long orMask = value ? (long)bit : 0;

                entries[entryIndex] = (entries[entryIndex] & andMask) | orMask;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetAtomic(int index, bool value)
        {
            unsafe
            {
                Assert.IsTrue(0 <= index && index < m_Length);

                int entryIndex = index >> 6;
                long* entries = (long*)m_Bits.GetUnsafePtr();

                ulong bit = 1ul << (index & 0x3f);
                long andMask = (long)(~bit);
                long orMask = value ? (long)bit : 0;

                long oldEntry, newEntry;
                do
                {
                    oldEntry = Interlocked.Read(ref entries[entryIndex]);
                    newEntry = (oldEntry & andMask) | orMask;
                } while (Interlocked.CompareExchange(ref entries[entryIndex], newEntry, oldEntry) != oldEntry);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Get(int index)
        {
            unsafe
            {
                Assert.IsTrue(0 <= index && index < m_Length);

                int entryIndex = index >> 6;
                long* entries = (long*)m_Bits.GetUnsafeReadOnlyPtr();

                ulong bit = 1ul << (index & 0x3f);
                long checkMask = (long)bit;
                return (entries[entryIndex] & checkMask) != 0;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly ulong GetChunk(int chunkIndex)
        {
            return (ulong)m_Bits[chunkIndex];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetChunk(int chunkIndex, ulong chunkBits)
        {
            m_Bits[chunkIndex] = (long)chunkBits;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly unsafe ulong InterlockedReadChunk(int chunkIndex)
        {
            long* entries = (long*)m_Bits.GetUnsafeReadOnlyPtr();
            return (ulong)Interlocked.Read(ref entries[chunkIndex]);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe void InterlockedOrChunk(int chunkIndex, ulong chunkBits)
        {
            long* entries = (long*)m_Bits.GetUnsafePtr();

            long oldEntry, newEntry;
            do
            {
                oldEntry = Interlocked.Read(ref entries[chunkIndex]);
                newEntry = oldEntry | (long)chunkBits;
            } while (Interlocked.CompareExchange(ref entries[chunkIndex], newEntry, oldEntry) != oldEntry);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly NativeArray<ulong> AsArray()
        {
            return m_Bits.Reinterpret<ulong>();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly ParallelBitArray GetSubArray(int length)
        {
            return new ParallelBitArray { m_Bits = m_Bits.GetSubArray(0, (length + 63) / 64), m_Length = length };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe UnsafeBitArray AsUnsafeBitArray()
        {
            return new UnsafeBitArray
            {
                Ptr = (ulong*)m_Bits.GetUnsafePtr(),
                Length = m_Length,
                Capacity = m_Bits.Length * 64,
                Allocator = Allocator.None
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly unsafe UnsafeBitArray AsReadOnlyUnsafeBitArray()
        {
            return new UnsafeBitArray
            {
                Ptr = (ulong*)m_Bits.GetUnsafeReadOnlyPtr(),
                Length = m_Length,
                Capacity = m_Bits.Length * 64,
                Allocator = Allocator.None
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetRange(int pos, bool value, int numBits)
        {
            AsUnsafeBitArray().SetBits(pos, value, numBits);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetRange(int pos, ulong bits, int numBits)
        {
            AsUnsafeBitArray().SetBits(pos, bits, numBits);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly int FindFirstSetBit(int startIndex = 0, int count = -1)
        {
            return AsReadOnlyUnsafeBitArray().FindFirstSetBit(startIndex, count);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly int FindFirstZeroBit(int startIndex = 0, int count = -1)
        {
            return AsReadOnlyUnsafeBitArray().FindFirstZeroBit(startIndex, count);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly int FindLastSetBit(int startIndex = 0, int count = -1)
        {
            return AsReadOnlyUnsafeBitArray().FindLastSetBit(startIndex, count);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly int CountBits(int startIndex = 0, int count = -1)
        {
            if (count < 0) count = m_Length - startIndex;
            if (count == 0) return 0;
            return AsReadOnlyUnsafeBitArray().CountBits(startIndex, count);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe long* GetUnsafeReadOnlyPtr()
        {
            return (long*)m_Bits.GetUnsafeReadOnlyPtr();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe long* GetUnsafePtr()
        {
            return (long*)m_Bits.GetUnsafePtr();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe long* GetUnsafePtrUnchecked()
        {
            return (long*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(m_Bits);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void CopyFrom(ParallelBitArray other)
        {
            m_Bits.CopyFrom(other.m_Bits);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void CopyFrom(ParallelBitArray other, int srcPos, int dstPos, int numBits)
        {
            if (numBits < 0) numBits = m_Length - srcPos;
            CheckArgs(srcPos, numBits);
            other.CheckArgs(srcPos, numBits);

            var otherUnsafe = other.AsReadOnlyUnsafeBitArray();
            var thisUnsafe = AsUnsafeBitArray();
            thisUnsafe.Copy(dstPos, ref otherUnsafe, srcPos, numBits);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly void CopyTo(ParallelBitArray other)
        {
            m_Bits.CopyTo(other.m_Bits);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Copy(int dstPos, int srcPos, int numBits)
        {
            AsUnsafeBitArray().Copy(dstPos, srcPos, numBits);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Copy(int dstPos, ref UnsafeBitArray srcBitArray, int srcPos, int numBits)
        {
            AsUnsafeBitArray().Copy(dstPos, ref srcBitArray, srcPos, numBits);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Or(ParallelBitArray other)
        {
            int count = Math.Min(m_Bits.Length, other.m_Bits.Length);
            for (int i = 0; i < count; ++i)
                m_Bits[i] |= other.m_Bits[i];
        }

        public readonly SetBitEnumerator<int> SetBits
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => AsReadOnlyUnsafeBitArray().SetBitEnumerator();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly unsafe SetBitEnumerator<int> SetBitEnumerator(int srcPos = 0, int numBits = -1)
        {
            if (numBits < 0) numBits = m_Length - srcPos;
            if (numBits == 0) return default;
            CheckArgs(srcPos, numBits);
            return new SetBitEnumerator<int>((ulong*)m_Bits.GetUnsafeReadOnlyPtr(), srcPos, numBits);
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private readonly void CheckArgs(int pos, int numBits)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (pos < 0
                || pos >= m_Length
                || numBits < 1)
            {
                throw new ArgumentException($"BitArray invalid arguments: pos {pos} (must be 0-{m_Length - 1}), numBits {numBits} (must be greater than 0).");
            }
#endif
        }
    }

    internal static class ParallelBitArrayHelpers
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ParallelBitArray AsParallelBitArray(this NativeArray<long> bits)
        {
            ParallelBitArray parallelBitArray;
            parallelBitArray.m_Bits = bits;
            parallelBitArray.m_Length = bits.Length;
            return parallelBitArray;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ParallelBitArray AsParallelBitArray(this NativeArray<ulong> bits)
        {
            ParallelBitArray parallelBitArray;
            parallelBitArray.m_Bits = bits.Reinterpret<long>();
            parallelBitArray.m_Length = bits.Length;
            return parallelBitArray;
        }
    }
}
