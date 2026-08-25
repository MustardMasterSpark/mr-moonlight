// Copyright © Magnetic Arcade. All Rights Reserved.

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Burst.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Jobs.LowLevel.Unsafe;
using Unity.Mathematics;

namespace MA.Flora
{
    internal unsafe struct NativeBufferArrayMetadata
    {
        public byte* Buffer;
        public int ElementSize;
        public int InlineCapacity;
        public int Length;
        public int Capacity;

        internal NativeBufferHeader* GetNativeBufferHeader(int index, int elementSize)
        {
            return (NativeBufferHeader*)(Buffer + index * elementSize);
        }
    }

    internal unsafe struct NativeBufferArrayDispose
    {
        [NativeDisableUnsafePtrRestriction]
        public NativeBufferArrayMetadata* m_BufferArrayData;
        public AllocatorManager.AllocatorHandle m_AllocatorLabel;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
        [NativeContainer]
        public struct ArrayAccessor { internal AtomicSafetyHandle m_Safety; }
        public ArrayAccessor m_ArrayAccessor;

        [NativeContainer]
        public struct BufferAccessor { internal AtomicSafetyHandle m_Safety; }
        public BufferAccessor m_BufferAccessor;
#endif

        public void Dispose()
        {
            for (int i = 0; i < m_BufferArrayData->Length; i++)
            {
                var buffer = m_BufferArrayData->GetNativeBufferHeader(i, m_BufferArrayData->ElementSize);
                NativeBufferHeader.Destroy(buffer, m_AllocatorLabel);
            }

            AllocatorManager.Free(m_AllocatorLabel, m_BufferArrayData->Buffer);
            AllocatorManager.Free(m_AllocatorLabel, m_BufferArrayData);
        }
    }

    [BurstCompile]
    [GenerateTestsForBurstCompatibility]
    internal struct NativeDisposeBufferArrayJob : IJob
    {
        public NativeBufferArrayDispose Data;

        public void Execute()
        {
            Data.Dispose();
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    [DebuggerDisplay("Length = {Length}, Capacity = {Capacity}, IsCreated = {IsCreated}")]
    [DebuggerTypeProxy(typeof(NativeBufferArrayDebugView<>))]
    internal unsafe struct NativeBufferArray<T> : IDisposable where T : unmanaged
    {
        [NativeDisableUnsafePtrRestriction] private NativeBufferArrayMetadata* m_Metadata;
        private AllocatorManager.AllocatorHandle m_AllocatorLabel;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
        [NativeContainer]
        internal struct ArrayAccessor
        {
            internal AtomicSafetyHandle m_Safety;
            internal static readonly SharedStatic<int> StaticSafetyId = SharedStatic<int>.GetOrCreate<NativeBufferArray<T>>();
        }

        [NativeContainer]
        internal struct BufferAccessor
        {
            internal AtomicSafetyHandle m_Safety;
            internal static readonly SharedStatic<int> StaticSafetyId = SharedStatic<int>.GetOrCreate<NativeBuffer<T>>();
        }

        private ArrayAccessor m_ArrayAccessor;
        private BufferAccessor m_BufferAccessor;
#endif

        private const int DefaultCapacityNumerator = 128;

        public NativeBufferArray(int length, int inlineCapacity, AllocatorManager.AllocatorHandle allocator)
        {
            if (inlineCapacity == -1)
                inlineCapacity = MathUtility.DivideAndRoundUp(DefaultCapacityNumerator, UnsafeUtility.SizeOf<T>());

            m_Metadata = AllocatorManager.Allocate<NativeBufferArrayMetadata>(allocator, 1);

            int capacity = math.max(4, length);
            int elementSize = UnsafeUtility.SizeOf<NativeBufferHeader>() + (inlineCapacity * UnsafeUtility.SizeOf<T>());
            byte* pointer = (byte*)AllocatorManager.Allocate(allocator, elementSize, JobsUtility.CacheLineSize, capacity);
            UnsafeUtility.MemClear(pointer, capacity * elementSize);

            m_Metadata->Buffer = pointer;
            m_Metadata->ElementSize = elementSize;
            m_Metadata->InlineCapacity = inlineCapacity;
            m_Metadata->Length = length;
            m_Metadata->Capacity = capacity;
            m_AllocatorLabel = allocator;

            for (int i = 0; i < length; i++)
            {
                var buffer = (NativeBufferHeader*)(pointer + i * m_Metadata->ElementSize);
                NativeBufferHeader.Initialize(buffer, m_Metadata->InlineCapacity);
            }

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            m_ArrayAccessor = new ArrayAccessor();
            m_ArrayAccessor.m_Safety = CollectionHelper.CreateSafetyHandle(allocator);
            AtomicSafetyHandle.SetAllowSecondaryVersionWriting(m_ArrayAccessor.m_Safety, false);
            CollectionHelper.SetStaticSafetyId(ref m_ArrayAccessor.m_Safety, ref ArrayAccessor.StaticSafetyId.Data, "NativeBufferArray");

            m_BufferAccessor = new BufferAccessor();
            m_BufferAccessor.m_Safety = CollectionHelper.CreateSafetyHandle(allocator);
            AtomicSafetyHandle.SetBumpSecondaryVersionOnScheduleWrite(m_BufferAccessor.m_Safety, true);
            CollectionHelper.SetStaticSafetyId(ref m_BufferAccessor.m_Safety, ref BufferAccessor.StaticSafetyId.Data, "NativeBuffer");
#endif
        }

        public NativeBufferArray(int length, AllocatorManager.AllocatorHandle allocator)
            : this(length, -1, allocator)
        {
        }

        public void Dispose()
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (!AtomicSafetyHandle.IsDefaultValue(m_ArrayAccessor.m_Safety))
                AtomicSafetyHandle.CheckExistsAndThrow(m_ArrayAccessor.m_Safety);
            if (!AtomicSafetyHandle.IsDefaultValue(m_BufferAccessor.m_Safety))
                AtomicSafetyHandle.CheckExistsAndThrow(m_BufferAccessor.m_Safety);
#endif
            if (!IsCreated)
                return;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            CollectionHelper.DisposeSafetyHandle(ref m_ArrayAccessor.m_Safety);
            CollectionHelper.DisposeSafetyHandle(ref m_BufferAccessor.m_Safety);
#endif

            if (m_AllocatorLabel > Allocator.None)
            {
                for (int i = 0; i < m_Metadata->Length; i++)
                {
                    var buffer = GetNativeBufferHeader(m_Metadata->Buffer, i, m_Metadata->ElementSize);
                    NativeBufferHeader.Destroy(buffer, m_AllocatorLabel);
                }

                AllocatorManager.Free(m_AllocatorLabel, m_Metadata->Buffer);
                AllocatorManager.Free(m_AllocatorLabel, m_Metadata);
            }

            m_Metadata = null;
            m_AllocatorLabel = Allocator.Invalid;
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            m_ArrayAccessor = default;
            m_BufferAccessor = default;
#endif
        }

        public JobHandle Dispose(JobHandle inputDeps)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (!AtomicSafetyHandle.IsDefaultValue(m_ArrayAccessor.m_Safety))
                AtomicSafetyHandle.CheckExistsAndThrow(m_ArrayAccessor.m_Safety);
            if (!AtomicSafetyHandle.IsDefaultValue(m_BufferAccessor.m_Safety))
                AtomicSafetyHandle.CheckExistsAndThrow(m_BufferAccessor.m_Safety);
#endif
            if (!IsCreated)
                return inputDeps;

            var disposeData = new NativeBufferArrayDispose
            {
                m_BufferArrayData = m_Metadata,
                m_AllocatorLabel = m_AllocatorLabel,
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                m_ArrayAccessor = new NativeBufferArrayDispose.ArrayAccessor { m_Safety = m_ArrayAccessor.m_Safety },
                m_BufferAccessor = new NativeBufferArrayDispose.BufferAccessor { m_Safety = m_BufferAccessor.m_Safety }
#endif
            };

            var jobHandle = new NativeDisposeBufferArrayJob
            {
                Data = disposeData
            }.Schedule(inputDeps);

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.Release(m_ArrayAccessor.m_Safety);
            AtomicSafetyHandle.Release(m_BufferAccessor.m_Safety);
#endif

            m_Metadata = null;
            m_AllocatorLabel = Allocator.Invalid;
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            m_ArrayAccessor = default;
            m_BufferAccessor = default;
#endif
            return jobHandle;
        }

        private static NativeBufferHeader* GetNativeBufferHeader(byte* ptr, int index, int elementSize)
        {
            return (NativeBufferHeader*)(ptr + index * elementSize);
        }

        public readonly bool IsCreated
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => m_Metadata != null;
        }

        public readonly int Capacity
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                CheckReadAccess();
                return m_Metadata->Capacity;
            }
        }

        public readonly int Length
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                CheckReadAccess();
                return m_Metadata->Length;
            }
        }

        public readonly NativeBuffer<T> this[int bufferIndex]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                CheckReadAccess();
                CheckIndex(bufferIndex);

                var header = GetNativeBufferHeader(m_Metadata->Buffer, bufferIndex, m_Metadata->ElementSize);
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                return new NativeBuffer<T>(header, m_Metadata->InlineCapacity, m_AllocatorLabel, m_ArrayAccessor, m_BufferAccessor);
#else
                return new NativeBuffer<T>(header, m_Metadata->InlineCapacity, m_AllocatorLabel);
#endif
            }
        }

        public readonly T this[int bufferIndex, int elementIndex]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                CheckReadAccess();
                CheckIndex(bufferIndex);

                var header = GetNativeBufferHeader(m_Metadata->Buffer, bufferIndex, m_Metadata->ElementSize);
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                var buffer = new NativeBuffer<T>(header, m_Metadata->InlineCapacity, m_AllocatorLabel, m_ArrayAccessor, m_BufferAccessor);
#else
                var buffer = new NativeBuffer<T>(header, m_Metadata->InlineCapacity, m_AllocatorLabel);
#endif
                return buffer[elementIndex];
            }
        }

        private void SetCapacity(int newCapacity)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckWriteAndBumpSecondaryVersion(m_ArrayAccessor.m_Safety);
#endif

            newCapacity = math.max(newCapacity, CollectionHelper.CacheLineSize / sizeof(ulong));
            newCapacity = math.ceilpow2(newCapacity);
            if (newCapacity == m_Metadata->Capacity)
                return;

            byte* newPointer = (byte*)AllocatorManager.Allocate(m_AllocatorLabel, m_Metadata->ElementSize, JobsUtility.CacheLineSize, newCapacity);
            if (newPointer != null)
            {
                int bufferElementsToCopy = math.min(newCapacity, m_Metadata->Capacity);
                if (bufferElementsToCopy > 0)
                    UnsafeUtility.MemCpy(newPointer, m_Metadata->Buffer, bufferElementsToCopy * m_Metadata->ElementSize);

                for (int i = m_Metadata->Capacity; i < newCapacity; i++)
                {
                    var buffer = (NativeBufferHeader*)(newPointer + i * m_Metadata->ElementSize);
                    NativeBufferHeader.Initialize(buffer, m_Metadata->InlineCapacity);
                }
            }

            if (m_Metadata->Buffer != null)
            {
                for (int i = newCapacity; i < m_Metadata->Capacity; i++)
                {
                    var buffer = GetNativeBufferHeader(m_Metadata->Buffer, i, m_Metadata->ElementSize);
                    NativeBufferHeader.Destroy(buffer, m_AllocatorLabel);
                }
            }

            AllocatorManager.Free(m_AllocatorLabel, m_Metadata->Buffer);
            m_Metadata->Buffer = newPointer;
            m_Metadata->Capacity = newCapacity;
        }

        public void Resize(int newLength)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckWriteAndBumpSecondaryVersion(m_ArrayAccessor.m_Safety);
#endif

            if (newLength < 0)
                throw new ArgumentOutOfRangeException(nameof(newLength), "Length must be greater than or equal to zero.");
            if (newLength == m_Metadata->Length)
                return;

            if (newLength > m_Metadata->Capacity)
                SetCapacity(newLength);

            if (newLength > m_Metadata->Length)
            {
                for (int i = m_Metadata->Length; i < newLength; i++)
                {
                    var buffer = GetNativeBufferHeader(m_Metadata->Buffer, i, m_Metadata->ElementSize);
                    NativeBufferHeader.Initialize(buffer, m_Metadata->InlineCapacity);
                }
            }
            else if (newLength < m_Metadata->Length)
            {
                for (int i = newLength; i < m_Metadata->Length; i++)
                {
                    var buffer = GetNativeBufferHeader(m_Metadata->Buffer, i, m_Metadata->ElementSize);
                    NativeBufferHeader.Destroy(buffer, m_AllocatorLabel);
                }
            }

            m_Metadata->Length = newLength;
        }

        public void Clear()
        {
            for (int i = 0; i < m_Metadata->Length; i++)
                this[i].Clear();
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        private readonly void CheckReadAccess()
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckReadAndThrow(m_ArrayAccessor.m_Safety);
#endif
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        private readonly void CheckIndex(int index)
        {
            if (Hint.Unlikely(!IsCreated))
                throw new InvalidOperationException("The NativeBufferArray has been deallocated.");
            if (Hint.Unlikely(index < 0 || index >= m_Metadata->Length))
                throw new IndexOutOfRangeException($"Index '{index}' is out of range.");
        }
    }

    internal sealed class NativeBufferArrayDebugView<T>  where T : unmanaged
    {
        private NativeBufferArray<T> m_BufferArray;

        public NativeBufferArrayDebugView(NativeBufferArray<T> source)
        {
            m_BufferArray = source;
        }

        public T[][] Items
        {
            get
            {
                if (m_BufferArray.IsCreated)
                {
                    var items = new T[m_BufferArray.Length][];

                    for (int i = 0; i < m_BufferArray.Length; i++)
                    {
                        var buffer = m_BufferArray[i];
                        items[i] = new T[buffer.Length];
                        for (int j = 0; j < buffer.Length; j++)
                            items[i][j] = buffer[j];
                    }

                    return items;
                }

                return Array.Empty<T[]>();
            }
        }
    }
}
