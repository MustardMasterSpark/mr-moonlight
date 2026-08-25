// Copyright © Magnetic Arcade. All Rights Reserved.

using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

namespace MA.Flora
{
    internal unsafe struct SetBitChunkEnumerator : IEnumerator<int>, IEnumerable<int>
    {
        [NoAlias] private readonly ulong* m_Chunks;
        private readonly int m_ChunkStart;
        private readonly int m_ChunkCount;

        private int m_ChunkIndex;
        private int m_Current;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public SetBitChunkEnumerator(ulong* chunks, int chunkStart, int chunkCount)
        {
            m_Chunks = chunks;
            m_ChunkStart = chunkStart;
            m_ChunkCount = chunkCount;

            m_ChunkIndex = -1;
            m_Current = -1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Dispose() { }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Reset()
        {
            m_ChunkIndex = -1;
            m_Current = -1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveNext()
        {
            if (m_Chunks == null)
                return false;

            int i = m_ChunkIndex;
            int last = m_ChunkCount - 1;

            while (i < last)
            {
                i++;
                if (m_Chunks[m_ChunkStart + i] != 0ul)
                {
                    m_ChunkIndex = i;
                    m_Current = m_ChunkStart + i;
                    return true;
                }
            }

            m_ChunkIndex = last;
            m_Current = -1;
            return false;
        }

        public int Current
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => m_Current;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public SetBitChunkEnumerator GetEnumerator() => new SetBitChunkEnumerator(m_Chunks, m_ChunkStart, m_ChunkCount);

        object IEnumerator.Current => m_Current;

        IEnumerator<int> IEnumerable<int>.GetEnumerator() => GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    internal unsafe struct SetBitEnumerator<TIndexType> : IEnumerator<TIndexType>, IEnumerable<TIndexType>
        where TIndexType : unmanaged
    {
        [NoAlias] private readonly ulong* m_Chunks;
        private readonly int m_Start;
        private readonly int m_End;
        private int m_ChunkIndex;
        private ulong m_Mask;
        private ulong m_RemainingMask;
        private ulong m_UnscannedBitMask;
        private int m_Index;
        private int m_BaseBitIndex;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public SetBitEnumerator(ulong* chunks, int index, int count)
        {
            m_Chunks = chunks;
            m_Start = index;
            m_End = index + count;
            m_Index = -1;
            m_ChunkIndex = 0;
            m_BaseBitIndex = 0;
            m_RemainingMask = 0;
            m_UnscannedBitMask = 0;
            m_Mask = 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Dispose() { }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Reset()
        {
            m_Index = -1;
            m_Mask = 0;
        }

        public bool MoveNext()
        {
            if (m_Chunks == null || m_Index >= m_End)
                return false;

            if (m_Index == -1)
            {
                m_ChunkIndex = m_Start >> 6;
                m_BaseBitIndex = m_Start & ~63;
                m_UnscannedBitMask = ulong.MaxValue << (m_Start & 63);
                m_RemainingMask = m_Chunks[m_ChunkIndex];
            }
            else
            {
                m_UnscannedBitMask &= ~m_Mask; // Mark visited
            }

            // Skip whole 64-bit words that are empty

            int lastChunkIndex = (m_End - 1) >> 6;
            ulong remainingMask = m_RemainingMask & m_UnscannedBitMask;

            if (remainingMask == 0)
            {
                int probe = m_ChunkIndex + 1;

                if (X86.Avx2.IsAvx2Supported)
                {
                    const int lanes = 4; // 4 × 64-bit = 256 bits
                    while (probe + lanes - 1 <= lastChunkIndex)
                    {
                        var v = X86.Avx.mm256_loadu_si256(m_Chunks + probe);
                        if (X86.Avx.mm256_testz_si256(v, v) == 0) break; // found a non-zero word
                        probe += lanes;
                    }
                }
                else if (Arm.Neon.IsNeonSupported)
                {
                    const int lanes = 2; // 2 × 64-bit = 128 bits
                    while (probe + lanes - 1 <= lastChunkIndex)
                    {
                        var v = Arm.Neon.vld1q_u64(m_Chunks + probe);
                        var any = Arm.Neon.vgetq_lane_u64(v, 0) | Arm.Neon.vgetq_lane_u64(v, 1);
                        if (any != 0) break; // found a non-zero word
                        probe += lanes;
                    }
                }

                // Scalar remainder or non-SIMD path

                while (probe <= lastChunkIndex && m_Chunks[probe] == 0)
                    ++probe;

                if (probe > lastChunkIndex)
                    return false;

                m_ChunkIndex = probe;
                m_BaseBitIndex = probe << 6;
                m_UnscannedBitMask = ulong.MaxValue;
                m_RemainingMask = remainingMask = m_Chunks[probe];
            }

            // Isolate the lowest set bit in the remaining mask

            ulong newRemainingMask = remainingMask & (remainingMask - 1);
            m_Mask = remainingMask ^ newRemainingMask;
            m_RemainingMask = newRemainingMask;
            m_Index = m_BaseBitIndex + math.tzcnt(m_Mask);

            return m_Index < m_End;
        }

        public TIndexType Current
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)] get => UnsafeUtility.As<int, TIndexType>(ref m_Index);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public SetBitEnumerator<TIndexType> GetEnumerator() => this;

        object IEnumerator.Current => m_Index;
        IEnumerator<TIndexType> IEnumerable<TIndexType>.GetEnumerator() => GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }


    internal unsafe struct SetBitReverseEnumerator<TIndexType> :
        IEnumerator<TIndexType>, IEnumerable<TIndexType>
        where TIndexType : unmanaged
    {
        private readonly ulong* m_Chunks;
        private readonly int m_Start;
        private readonly int m_End;
        private int m_ChunkIndex;
        private ulong m_Mask;
        private ulong m_RemainingMask;
        private ulong m_UnscannedBitMask;
        private int m_Index;
        private int m_BaseBitIndex;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public SetBitReverseEnumerator(ulong* chunks, int index, int count)
        {
            m_Chunks = chunks;
            m_Start = index;
            m_End = index + count;
            m_Index = -1;
            m_ChunkIndex = 0;
            m_BaseBitIndex = 0;
            m_RemainingMask = 0;
            m_UnscannedBitMask = 0;
            m_Mask = 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Dispose() { }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Reset()
        {
            m_Index = -1;
            m_Mask  = 0;
        }

        public bool MoveNext()
        {
            if (m_Chunks == null || (m_Index != -1 && m_Index <= m_Start))
                return false;

            if (m_Index == -1)
            {
                // start at the last bit that lies inside the range
                m_ChunkIndex = (m_End - 1) >> 6;
                m_BaseBitIndex = m_ChunkIndex << 6;

                int endOffset    = (m_End - 1) & 63;
                m_UnscannedBitMask =
                    (endOffset == 63)
                    ? ulong.MaxValue
                    : (ulong.MaxValue >> (63 - endOffset)); // keep bits ≤ endOffset

                m_RemainingMask  = m_Chunks[m_ChunkIndex];
            }
            else
            {
                // clear the bit we just returned
                m_UnscannedBitMask &= ~m_Mask;
            }

            ulong remainingMask = m_RemainingMask & m_UnscannedBitMask;
            int firstChunkIdx = m_Start >> 6;

            while (remainingMask == 0)
            {
                int probe = m_ChunkIndex - 1;

                while (probe >= firstChunkIdx && m_Chunks[probe] == 0)
                    --probe;

                if (probe < firstChunkIdx)
                    return false; // no more set bits in range

                m_ChunkIndex = probe;
                m_BaseBitIndex = probe << 6;
                m_UnscannedBitMask = ulong.MaxValue;

                // clamp the first chunk so we never return indices < m_Start
                if (probe == firstChunkIdx)
                    m_UnscannedBitMask &= ulong.MaxValue << (m_Start & 63);

                m_RemainingMask = remainingMask = m_Chunks[probe] & m_UnscannedBitMask;
            }

            int bitInChunk = 63 - math.lzcnt(remainingMask);
            m_Mask = 1UL << bitInChunk;
            m_RemainingMask = remainingMask & ~m_Mask;

            m_Index = m_BaseBitIndex + bitInChunk;
            return m_Index >= m_Start; // still inside range?
        }

        public TIndexType Current
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => UnsafeUtility.As<int, TIndexType>(ref m_Index);
        }

        object IEnumerator.Current => m_Index;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public SetBitReverseEnumerator<TIndexType> GetEnumerator() => this;

        IEnumerator<TIndexType> IEnumerable<TIndexType>.GetEnumerator() => GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
