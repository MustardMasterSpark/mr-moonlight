// Copyright © Magnetic Arcade. All Rights Reserved.

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace MA.Flora
{
    [StructLayout(LayoutKind.Sequential)]
    internal unsafe struct NativeRegionAllocator : IDisposable
    {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
        internal AtomicSafetyHandle m_Safety;
        private static readonly SharedStatic<int> s_staticSafetyId = SharedStatic<int>.GetOrCreate<NativeRegionAllocator>();
#endif
        [NativeDisableUnsafePtrRestriction]
        internal UnsafeRegionAllocator* m_Data;
        internal AllocatorManager.AllocatorHandle m_Allocator;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public NativeRegionAllocator(int initialCapacity, AllocatorManager.AllocatorHandle allocator)
        {
            m_Allocator = allocator;
            m_Data = AllocatorManager.Allocate<UnsafeRegionAllocator>(allocator);
            *m_Data = new UnsafeRegionAllocator(initialCapacity, allocator);
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            m_Safety = CollectionHelper.CreateSafetyHandle(allocator);
            CollectionHelper.SetStaticSafetyId(ref m_Safety, ref s_staticSafetyId.Data, "Unity.Collections.NativeRegionAllocator");
#endif
        }

        public readonly bool IsCreated
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                CheckRead();
                return m_Data != null && m_Data->IsCreated;
            }
        }

        public readonly int AllocatedSize
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                CheckRead();
                return m_Data->AllocatedSize;
            }
        }

        public readonly int MaxAllocatedSize
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                CheckRead();
                return m_Data->MaxAllocatedSize;
            }
        }

        public readonly int AvailableBlocks
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                CheckRead();
                return m_Data->AvailableBlocks;
            }
        }

        public readonly int PendingFreeBlockCount
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                CheckRead();
                return m_Data->PendingFreeBlockCount;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
            m_Data->Dispose();
            AllocatorManager.Free(m_Allocator, m_Data);
            m_Data = null;
            m_Allocator = AllocatorManager.Invalid;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Clear()
        {
            CheckWrite();
            m_Data->Clear();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int Allocate(int count = 1)
        {
            CheckWrite();
            return m_Data->Allocate(count);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Free(int index, int count = 1)
        {
            CheckWrite();
            m_Data->Free(index, count);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void MergeFree()
        {
            CheckWrite();
            m_Data->MergeFree();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsElementFree(int index)
        {
            CheckRead();
            return m_Data->IsElementFree(index);
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
    }
}
