// Copyright © Magnetic Arcade. All Rights Reserved.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace MA.Flora
{
    internal unsafe struct UnsafeBitSet : IDisposable, IEnumerable<int>
    {
        internal UnsafeList<ulong> m_Bits;
        internal int m_MinIndex;
        internal int m_MaxIndex;

        public readonly bool IsCreated
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => m_Bits.IsCreated;
        }

        public readonly bool IsEmpty
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => !IsCreated || (m_MaxIndex < m_MinIndex);
        }

        public readonly int MaxLength
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => IsEmpty ? 0 : m_MaxIndex + 1;
        }

        public readonly int Capacity
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => m_Bits.Capacity << 6;
        }

        public bool this[int index]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Contains(index);
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set
            {
                if (value) Add(index);
                else       Remove(index);
            }
        }

        public static UnsafeBitSet* Create(int capacity, AllocatorManager.AllocatorHandle allocator)
        {
            var set = AllocatorManager.Allocate<UnsafeBitSet>(allocator);
            *set = new UnsafeBitSet(capacity, allocator);
            return set;
        }

        public static void Destroy(UnsafeBitSet* set)
        {
            if (set == null) return;
            Destroy(set, set->m_Bits.Allocator);
        }

        public static void Destroy(UnsafeBitSet* set, AllocatorManager.AllocatorHandle allocator)
        {
            if (set == null) return;
            set->Dispose();
            AllocatorManager.Free(allocator, set);
        }

        public UnsafeBitSet(int capacity, AllocatorManager.AllocatorHandle allocator)
        {
            int capacityInWords = MathUtility.DivideAndRoundUp(capacity, 64);
            m_Bits = new UnsafeList<ulong>(capacityInWords, allocator);
            m_MinIndex = int.MaxValue;
            m_MaxIndex = int.MinValue;
        }

        public void Dispose()
        {
            if (!IsCreated) return;
            m_Bits.Dispose();
            m_MinIndex = int.MaxValue;
            m_MaxIndex = int.MinValue;
        }

        public void Dispose(JobHandle jobs)
        {
            if (!IsCreated) return;
            m_Bits.Dispose(jobs);
            m_MinIndex = int.MaxValue;
            m_MaxIndex = int.MinValue;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private readonly bool IsSet(int index)
        {
            return BitUtility.IsSet(m_Bits.Ptr, index);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SetBit(int index, bool value)
        {
            BitUtility.Set(m_Bits.Ptr, index, value);
        }

        private void UpdateMinMax()
        {
            int min = -1;
            for (int i = 0; i < m_Bits.Length; ++i)
            {
                ulong word = m_Bits.Ptr[i];
                if (word != 0)
                {
                    min = (i << 6) + math.tzcnt(word);
                    break;
                }
            }

            if (min == -1)
            {
                m_MinIndex = int.MaxValue;
                m_MaxIndex = int.MinValue;
                return;
            }

            int max = -1;
            for (int i = m_Bits.Length - 1; i >= 0; --i)
            {
                ulong word = m_Bits.Ptr[i];
                if (word != 0)
                {
                    max = (i << 6) + (63 - math.lzcnt(word));
                    break;
                }
            }

            if (max == -1)
            {
                m_MinIndex = int.MaxValue;
                m_MaxIndex = int.MinValue;
                return;
            }

            m_MinIndex = min;
            m_MaxIndex = max;

            int maxWords = MathUtility.DivideAndRoundUp(max + 1, 64);
            if (m_Bits.Length != maxWords)
                m_Bits.Resize(maxWords, NativeArrayOptions.ClearMemory);
        }

        public void Clear()
        {
            m_Bits.Clear();
            m_MinIndex = int.MaxValue;
            m_MaxIndex = int.MinValue;
        }

        public UnsafeBitSet Clone(Allocator allocator)
        {
            if (IsEmpty) return new UnsafeBitSet(0, allocator);
            var clone = new UnsafeBitSet(MaxLength, allocator);
            clone.CopyFrom(this);
            return clone;
        }

        public void ReserveCapacity(int capacity)
        {
            int requiredChunkLength = MathUtility.DivideAndRoundUp(capacity, 64);
            if (m_Bits.Capacity < requiredChunkLength)
                m_Bits.Capacity = requiredChunkLength;
        }

        private void EnsureLength(int length)
        {
            int chunkLength = MathUtility.DivideAndRoundUp(length, 64);
            if (chunkLength > m_Bits.Length)
                m_Bits.Resize(chunkLength, NativeArrayOptions.ClearMemory);
        }

        public void CopyFrom(UnsafeBitSet other)
        {
            if (other.IsEmpty)
            {
                Clear();
                return;
            }

            m_Bits.CopyFrom(other.m_Bits);
            m_MinIndex = other.m_MinIndex;
            m_MaxIndex = other.m_MaxIndex;
        }

        public readonly int Count()
        {
            return IsEmpty ? 0 : BitUtility.CountBits(m_Bits.Ptr, m_MinIndex, m_MaxIndex - m_MinIndex + 1);
        }

        public readonly int CountInRange(int startIndex, int count)
        {
            if (IsEmpty || count <= 0)
                return 0;

            int start = math.max(startIndex, m_MinIndex);
            int end   = math.min(startIndex + count - 1, m_MaxIndex);
            if (start > end)
                return 0;

            return BitUtility.CountBits(m_Bits.Ptr, start, end - start + 1);
        }

        public readonly int CountChunks()
        {
            if (IsEmpty)
                return 0;

            int firstChunk = m_MinIndex >> 6;
            int lastChunk = m_MaxIndex >> 6;
            if (firstChunk == lastChunk)
            {
                return m_Bits.Ptr[firstChunk] != 0 ? 1 : 0;
            }
            else
            {
                int count = 0;
                for (int i = firstChunk; i <= lastChunk; i++)
                {
                    if (m_Bits.Ptr[i] != 0)
                        count++;
                }
                return count;
            }
        }

        public void UnionWith(UnsafeBitSet other)
        {
            if (other.IsEmpty) return;
            if (IsEmpty) { CopyFrom(other); return; }
            EnsureLength(other.MaxLength);

            int dstCount = m_Bits.Length;
            int srcCount = other.m_Bits.Length;
            int orCount = math.min(dstCount, srcCount);
            for (int i = 0; i < orCount; i++)
                m_Bits.Ptr[i] |= other.m_Bits.Ptr[i];

            int tailCount = srcCount - orCount;
            if (tailCount > 0)
                UnsafeUtility.MemCpy(m_Bits.Ptr + orCount, other.m_Bits.Ptr + orCount, tailCount * sizeof(ulong));

            m_MinIndex = math.min(m_MinIndex, other.m_MinIndex);
            m_MaxIndex = math.max(m_MaxIndex, other.m_MaxIndex);
        }

        public void UnionAt(int srcIndex, int dstIndex)
        {
            if (IsEmpty || srcIndex >= MaxLength) return;
            EnsureLength(dstIndex + 1);

            var srcChunk = srcIndex >> 6;
            var srcShift = srcIndex & 63;

            var dstChunk = dstIndex >> 6;
            var dstShift = dstIndex & 63;

            var srcBit = (m_Bits.Ptr[srcChunk] >> srcShift) & 1ul;
            if (srcBit != 0)
            {
                m_Bits.Ptr[dstChunk] |= (1ul << dstShift);
                m_MinIndex = math.min(m_MinIndex, dstIndex);
                m_MaxIndex = math.max(m_MaxIndex, dstIndex);
            }
        }

        public void IntersectWith(UnsafeBitSet other)
        {
            if (IsEmpty || other.IsEmpty) { Clear(); return; }

            int dstCount = m_Bits.Length;
            int srcCount = other.m_Bits.Length;
            int andCount = math.min(dstCount, srcCount);
            for (int i = 0; i < andCount; i++)
                m_Bits.Ptr[i] &= other.m_Bits.Ptr[i];

            int clearCount = dstCount - andCount;
            if (clearCount > 0)
                UnsafeUtility.MemClear(m_Bits.Ptr + andCount, clearCount * sizeof(ulong));

            UpdateMinMax();
        }

        public void ExceptWith(in UnsafeBitSet other)
        {
            if (IsEmpty || other.IsEmpty) return;

            int dstCount = m_Bits.Length;
            int srcCount = other.m_Bits.Length;
            int andNotCount = math.min(dstCount, srcCount);
            for (int i = 0; i < andNotCount; i++)
                m_Bits.Ptr[i] &= ~other.m_Bits.Ptr[i];

            UpdateMinMax();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add(int index)
        {
            CheckNegative(index);
            EnsureLength(index + 1);
            SetBit(index, true);
            m_MinIndex = math.min(m_MinIndex, index);
            m_MaxIndex = math.max(m_MaxIndex, index);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddNoResize(int index)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (index < 0 || index >= Capacity)
                throw new IndexOutOfRangeException($"Index {index} is out of range of '{MaxLength}'");
#endif
            SetBit(index, true);
            m_MinIndex = math.min(m_MinIndex, index);
            m_MaxIndex = math.max(m_MaxIndex, index);
        }

        public bool TryAdd(int index)
        {
            if (!Contains(index))
            {
                EnsureLength(index + 1);
                SetBit(index, true);
                m_MinIndex = math.min(m_MinIndex, index);
                m_MaxIndex = math.max(m_MaxIndex, index);
                return true;
            }

            return false;
        }

        public void AddRange(int* indices, int count)
        {
            if (count <= 0) return;

            int maxIndex = m_MaxIndex;
            for (int i = 0; i < count; i++)
                maxIndex = math.max(maxIndex, indices[i]);

            EnsureLength(maxIndex + 1);

            for (int i = 0; i < count; i++)
            {
                int index = indices[i];
                SetBit(index, true);
                m_MinIndex = math.min(m_MinIndex, index);
            }

            m_MaxIndex = maxIndex;
        }

        public void AddRangeNoResize(int* indices, int count)
        {
            for (int i = 0; i < count; i++)
            {
                int index = indices[i];
                SetBit(index, true);
                m_MinIndex = math.min(m_MinIndex, index);
                m_MaxIndex = math.max(m_MaxIndex, index);
            }
        }

        public void AddRange(int startIndex, int count)
        {
            if (count <= 0) return;

            int lastIndex = startIndex + count - 1;
            EnsureLength(lastIndex + 1);

            BitUtility.SetBits(m_Bits.Ptr, startIndex, true, count);
            m_MinIndex = math.min(m_MinIndex, startIndex);
            m_MaxIndex = math.max(m_MaxIndex, lastIndex);
        }

        public bool Remove(int index)
        {
            if (Contains(index))
            {
                SetBit(index, false);
                if (index == m_MinIndex || index == m_MaxIndex)
                    UpdateMinMax();

                return true;
            }

            return false;
        }

        public bool RemoveRange(int index, int count)
        {
            if (IsEmpty || count <= 0)
                return false;

            int start = math.max(index, m_MinIndex);
            int end   = math.min(index + count - 1, m_MaxIndex);
            if (start > end)
                return false;

            int length = end - start + 1;
            BitUtility.SetBits(m_Bits.Ptr, start, false, length);
            if (start == m_MinIndex || end == m_MaxIndex)
                UpdateMinMax();

            return true;
        }

        public readonly bool Contains(int index)
        {
            if (index < m_MinIndex || index > m_MaxIndex)
                return false;

            return IsSet(index);
        }

        public readonly bool AnyInRange(int startIndex, int count)
        {
            if (IsEmpty || count <= 0) return false;

            int start = math.max(startIndex, m_MinIndex);
            int end   = math.min(startIndex + count - 1, m_MaxIndex);
            if (start > end) return false;

            return BitUtility.TestAny(m_Bits.Ptr, start, end - start + 1);
        }

        public readonly int FindFreeIndex(int startIndex = 0, int count = -1)
        {
            if (IsEmpty) return -1;
            if (count <= 0) count = m_MaxIndex - startIndex + 1;

            int start = math.max(startIndex, m_MinIndex);
            int end   = math.min(startIndex + count - 1, m_MaxIndex);
            if (start > end) return -1;

            return BitUtility.FindFirst(false, m_Bits.Ptr, start, end - start + 1);
        }

        public readonly void CopyToList(NativeList<int> list)
        {
            if (IsEmpty) return;
            list.Clear();
            foreach (int bitIndex in this)
                list.Add(bitIndex);
        }

        public readonly NativeArray<int> ToArray(AllocatorManager.AllocatorHandle allocator)
        {
            var setBitCount = IsEmpty ? 0 : BitUtility.CountBits(m_Bits.Ptr, m_MinIndex, m_MaxIndex - m_MinIndex + 1);
            var array = CollectionHelper.CreateNativeArray<int>(setBitCount, allocator, NativeArrayOptions.UninitializedMemory);
            var writeIndex = 0;
            foreach (int bitIndex in this)
                array[writeIndex++] = bitIndex;

            return array;
        }

        public readonly NativeArray<int> ToArray(ref RewindableAllocator allocator)
        {
            var setBitCount = IsEmpty ? 0 : BitUtility.CountBits(m_Bits.Ptr, m_MinIndex, m_MaxIndex - m_MinIndex + 1);
            var array = CollectionHelper.CreateNativeArray<int, RewindableAllocator>(setBitCount, ref allocator, NativeArrayOptions.UninitializedMemory);
            var writeIndex = 0;
            foreach (int bitIndex in this)
                array[writeIndex++] = bitIndex;

            return array;
        }

        public readonly NativeArray<int> ToChunkArray(AllocatorManager.AllocatorHandle allocator)
        {
            var chunkCount = CountChunks();
            var array = CollectionHelper.CreateNativeArray<int>(chunkCount, allocator, NativeArrayOptions.UninitializedMemory);
            var writeIndex = 0;
            foreach (int bitIndex in GetChunkEnumerator())
                array[writeIndex++] = bitIndex;

            return array;
        }

        public readonly NativeArray<int> ToChunkArray(ref RewindableAllocator allocator)
        {
            var chunkCount = CountChunks();
            var array = CollectionHelper.CreateNativeArray<int, RewindableAllocator>(chunkCount, ref allocator, NativeArrayOptions.UninitializedMemory);
            var writeIndex = 0;
            foreach (int bitIndex in GetChunkEnumerator())
                array[writeIndex++] = bitIndex;

            return array;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly NativeArray<T> ToArray<T>(Allocator allocator) where T : unmanaged
        {
            return ToArray(allocator).Reinterpret<T>();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly NativeArray<T> ToArray<T>(ref RewindableAllocator allocator) where T : unmanaged
        {
            return ToArray(ref allocator).Reinterpret<T>();
        }

        public readonly SetBitEnumerator<int> IndicesInRange(int startIndex, int count)
        {
            if (IsEmpty || count <= 0)
                return default;

            int start = math.max(startIndex, m_MinIndex);
            int end   = math.min(startIndex + count - 1, m_MaxIndex);
            if (start > end)
                return default;

            return new SetBitEnumerator<int>(m_Bits.Ptr, start, end - start + 1);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly SetBitEnumerator<T> AsType<T>() where T : unmanaged
        {
            return IsEmpty ? default : new SetBitEnumerator<T>(m_Bits.Ptr, m_MinIndex, m_MaxIndex - m_MinIndex + 1);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly SetBitChunkEnumerator GetChunkEnumerator()
        {
            return IsEmpty ? default : new SetBitChunkEnumerator(m_Bits.Ptr, m_MinIndex >> 6, (m_MaxIndex - m_MinIndex + 1) >> 6 + 1);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly SetBitEnumerator<int> GetEnumerator()
        {
            return IsEmpty ? default : new SetBitEnumerator<int>(m_Bits.Ptr, m_MinIndex, m_MaxIndex - m_MinIndex + 1);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly SetBitReverseEnumerator<int> GetReverseEnumerator()
        {
            return IsEmpty ? default : new SetBitReverseEnumerator<int>(m_Bits.Ptr, m_MinIndex, m_MaxIndex - m_MinIndex + 1);
        }

        readonly IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        readonly IEnumerator<int> IEnumerable<int>.GetEnumerator()
        {
            return GetEnumerator();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void CheckNegative(int index)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (index < 0) throw new ArgumentOutOfRangeException(nameof(index));
#endif
        }
    }
}
