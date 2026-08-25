// Copyright © Magnetic Arcade. All Rights Reserved.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;

namespace MA.Flora
{
    [StructLayout(LayoutKind.Sequential)]
    [NativeContainer]
    internal unsafe struct NativeBitSet : IDisposable, IEnumerable<int>
    {
        [NativeDisableUnsafePtrRestriction] internal UnsafeBitSet* m_SetData;
#if ENABLE_UNITY_COLLECTIONS_CHECKS
        internal AtomicSafetyHandle m_Safety;
        private static readonly SharedStatic<int> StaticSafetyId = SharedStatic<int>.GetOrCreate<NativeBitSet>();
#endif

        public readonly bool IsCreated
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => m_SetData != null;
        }

        public readonly bool IsEmpty
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                CheckRead();
                return m_SetData->IsEmpty;
            }
        }

        public readonly int MaxLength
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                CheckRead();
                return m_SetData->MaxLength;
            }
        }

        public bool this[int index]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                CheckRead();
                return (*m_SetData)[index];
            }
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set
            {
                CheckWrite();
                (*m_SetData)[index] = value;
            }
        }

        public NativeBitSet(int capacity, AllocatorManager.AllocatorHandle allocator)
        {
            m_SetData = UnsafeBitSet.Create(capacity, allocator);
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            m_Safety = CollectionHelper.CreateSafetyHandle(allocator);
            AtomicSafetyHandle.SetStaticSafetyId(ref m_Safety, StaticSafetyId.Data);
#endif
        }

        public void Dispose()
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (!AtomicSafetyHandle.IsDefaultValue(m_Safety))
                AtomicSafetyHandle.CheckExistsAndThrow(m_Safety);
#endif

            if (!IsCreated)
                return;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            CollectionHelper.DisposeSafetyHandle(ref m_Safety);
#endif

            UnsafeBitSet.Destroy(m_SetData);
            m_SetData = null;
        }

        public JobHandle Dispose(JobHandle inputDeps)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (!AtomicSafetyHandle.IsDefaultValue(m_Safety))
                AtomicSafetyHandle.CheckExistsAndThrow(m_Safety);
#endif

            if (!IsCreated)
                return inputDeps;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            var jobHandle = new NativeBitSetDisposeJob { Data = new NativeBitSetDispose { m_Data = m_SetData, m_Safety = m_Safety } }.Schedule(inputDeps);
            AtomicSafetyHandle.Release(m_Safety);
#else
            var jobHandle = new NativeBitSetDisposeJob { Data = new NativeBitSetDispose { m_Data = m_SetData } }.Schedule(inputDeps);
#endif
            m_SetData = null;

            return jobHandle;
        }

        public void Clear()
        {
            CheckWrite();
            m_SetData->Clear();
        }

        public UnsafeBitSet* GetUnsafeSet() => m_SetData;

        public NativeBitSet Clone(AllocatorManager.AllocatorHandle allocator)
        {
            if (IsEmpty) return new NativeBitSet(0, allocator);
            var clone = new NativeBitSet(MaxLength, allocator);
            clone.CopyFrom(this);
            return clone;
        }

        public void ReserveCapacity(int capacity)
        {
            CheckWrite();
            m_SetData->ReserveCapacity(capacity);
        }

        public void CopyFrom(NativeBitSet other)
        {
            CheckWrite();
            m_SetData->CopyFrom(*other.m_SetData);
        }

        public readonly int Count()
        {
            CheckRead();
            return m_SetData->Count();
        }

        public readonly int CountInRange(int startIndex, int count)
        {
            CheckRead();
            return m_SetData->CountInRange(startIndex, count);
        }

        public readonly int CountChunks()
        {
            CheckRead();
            return m_SetData->CountChunks();
        }

        public void UnionWith(NativeBitSet other)
        {
            CheckWrite();
            m_SetData->UnionWith(*other.m_SetData);
        }

        public void UnionAt(int srcIndex, int dstIndex)
        {
            CheckWrite();
            m_SetData->UnionAt(srcIndex, dstIndex);
        }

        public void IntersectWith(NativeBitSet other)
        {
            CheckWrite();
            m_SetData->IntersectWith(*other.m_SetData);
        }

        public void ExceptWith(NativeBitSet other)
        {
            CheckWrite();
            m_SetData->ExceptWith(*other.m_SetData);
        }

        public void AddNoResize(int index)
        {
            CheckWrite();
            m_SetData->AddNoResize(index);
        }

        public void Add(int index)
        {
            CheckWrite();
            m_SetData->Add(index);
        }

        public bool TryAdd(int index)
        {
            CheckWrite();
            return m_SetData->TryAdd(index);
        }

        public void AddRange(NativeArray<int> indices)
        {
            CheckWrite();
            m_SetData->AddRange((int*)indices.GetUnsafeReadOnlyPtr(), indices.Length);
        }

        public void AddRangeNoResize(int* indices, int count)
        {
            CheckWrite();
            m_SetData->AddRangeNoResize(indices, count);
        }

        public void AddRangeNoResize(NativeArray<int> indices)
        {
            CheckWrite();
            m_SetData->AddRangeNoResize((int*)indices.GetUnsafeReadOnlyPtr(), indices.Length);
        }

        public void AddRange(int* indices, int count)
        {
            CheckWrite();
            m_SetData->AddRange(indices, count);
        }

        public void AddRange(int startIndex, int count)
        {
            CheckWrite();
            m_SetData->AddRange(startIndex, count);
        }

        public bool Remove(int index)
        {
            CheckWrite();
            return m_SetData->Remove(index);
        }

        public bool RemoveRange(int index, int count)
        {
            CheckWrite();
            return m_SetData->RemoveRange(index, count);
        }

        public readonly bool Contains(int index)
        {
            CheckRead();
            return m_SetData->Contains(index);
        }

        public readonly bool AnyInRange(int startIndex, int count)
        {
            CheckRead();
            return m_SetData->AnyInRange(startIndex, count);
        }

        public readonly int FindFreeIndex(int startIndex = 0, int count = -1)
        {
            CheckRead();
            return m_SetData->FindFreeIndex(startIndex, count);
        }

        public readonly NativeArray<ulong> AsChunkArray()
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckGetSecondaryDataPointerAndThrow(m_Safety);
            var arraySafety = m_Safety;
            AtomicSafetyHandle.UseSecondaryVersion(ref arraySafety);
#endif

            var na = NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<ulong>(m_SetData->m_Bits.Ptr, m_SetData->m_Bits.Length, Allocator.None);

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            NativeArrayUnsafeUtility.SetAtomicSafetyHandle(ref na, arraySafety);
#endif
            return na;
        }

        public readonly ParallelBitArray AsParallelBitArray()
        {
            var chunkArray = AsChunkArray();
            return new ParallelBitArray
            {
                m_Bits = chunkArray.Reinterpret<long>(),
                m_Length = MaxLength
            };
        }

        public readonly void CopyToList(NativeList<int> list)
        {
            CheckRead();
            m_SetData->CopyToList(list);
        }

        public readonly NativeArray<int> ToArray(AllocatorManager.AllocatorHandle allocator)
        {
            CheckRead();
            return m_SetData->ToArray(allocator);
        }

        public readonly NativeArray<int> ToArray(ref RewindableAllocator allocator)
        {
            CheckRead();
            return m_SetData->ToArray(ref allocator);
        }

        public readonly NativeArray<T> ToArray<T>(AllocatorManager.AllocatorHandle allocator) where T : unmanaged
        {
            return ToArray(allocator).Reinterpret<T>();
        }

        public readonly NativeArray<T> ToArray<T>(ref RewindableAllocator allocator) where T : unmanaged
        {
            return ToArray(ref allocator).Reinterpret<T>();
        }

        public readonly NativeArray<int> ToChunkArray(AllocatorManager.AllocatorHandle allocator)
        {
            CheckRead();
            return m_SetData->ToChunkArray(allocator);
        }

        public readonly NativeArray<int> ToChunkArray(ref RewindableAllocator allocator)
        {
            CheckRead();
            return m_SetData->ToChunkArray(ref allocator);
        }

        public readonly NativeArray<T> ToChunkArray<T>(AllocatorManager.AllocatorHandle allocator) where T : unmanaged
        {
            CheckRead();
            return m_SetData->ToChunkArray(allocator).Reinterpret<T>();
        }

        public readonly NativeArray<T> ToChunkArray<T>(ref RewindableAllocator allocator) where T : unmanaged
        {
            CheckRead();
            return m_SetData->ToChunkArray(ref allocator).Reinterpret<T>();
        }

        public readonly SetBitEnumerator<int> IndicesInRange(int startIndex, int count)
        {
            CheckRead();
            return m_SetData->IndicesInRange(startIndex, count);
        }

        public readonly SetBitChunkEnumerator GetChunkEnumerator()
        {
            CheckRead();
            return m_SetData->GetChunkEnumerator();
        }

        public readonly SetBitEnumerator<int> GetEnumerator()
        {
            CheckRead();
            return m_SetData->GetEnumerator();
        }

        public readonly SetBitReverseEnumerator<int> GetReverseEnumerator()
        {
            CheckRead();
            return m_SetData->GetReverseEnumerator();
        }

        public readonly SetBitEnumerator<T> AsType<T>() where T : unmanaged
        {
            CheckRead();
            return m_SetData->AsType<T>();
        }

        readonly IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        readonly IEnumerator<int> IEnumerable<int>.GetEnumerator()
        {
            return GetEnumerator();
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private readonly void CheckRead()
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
#endif
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void CheckWrite()
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
#endif
        }

        [NativeContainer]
        internal struct NativeBitSetDispose
        {
            [NativeDisableUnsafePtrRestriction]
            public UnsafeBitSet* m_Data;
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            public AtomicSafetyHandle m_Safety;
#endif

            public void Dispose()
            {
                UnsafeBitSet.Destroy(m_Data);
            }
        }

        [BurstCompile]
        private struct NativeBitSetDisposeJob : IJob
        {
            public NativeBitSetDispose Data;

            public void Execute()
            {
                Data.Dispose();
            }
        }
    }
}
