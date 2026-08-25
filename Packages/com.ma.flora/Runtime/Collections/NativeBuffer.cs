// Copyright © Magnetic Arcade. All Rights Reserved.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Burst.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

namespace MA.Flora
{
    [StructLayout(LayoutKind.Explicit)]
    [NoAlias]
    internal unsafe struct NativeBufferHeader
    {
        public const int MinimumCapacity = 8;

        [NoAlias]
        [FieldOffset(0)] public byte* Pointer;
        [FieldOffset(8)] public int Length;
        [FieldOffset(12)] public int Capacity;

        public enum TrashMode
        {
            TrashOldData,
            RetainOldData
        }

        public static byte* GetElementPointer(NativeBufferHeader* header)
        {
            if (header->Pointer != null)
                return header->Pointer;

            return (byte*)(header + 1);
        }

        public static void EnsureCapacity(NativeBufferHeader* header, int newCapacity, int typeSize, int alignment, AllocatorManager.AllocatorHandle allocator)
        {
            if (newCapacity <= header->Capacity)
                return;

            int adjustedCount = Math.Max(MinimumCapacity, Math.Max(2 * header->Capacity, newCapacity)); // stop pathological performance of ++Capacity allocating every time, tiny Capacities
            SetCapacity(header, adjustedCount, typeSize, alignment, 0, allocator);
        }

        public static void SetCapacity(NativeBufferHeader* header, int newCapacity, int typeSize, int alignment, int internalCapacity, AllocatorManager.AllocatorHandle allocator)
        {
            if (newCapacity == header->Capacity)
                return;

            byte* oldData = GetElementPointer(header);
            byte* newData = (newCapacity <= internalCapacity) ? (byte*)(header + 1) : (byte*)AllocatorManager.Allocate(allocator, typeSize, alignment, newCapacity);

            if (oldData != newData) // if at least one of them isn't the internal pointer...
            {
                long itemsToCopy = Math.Min(header->Length, newCapacity);
                long bytesToCopy = itemsToCopy * typeSize;
                UnsafeUtility.MemCpy(newData, oldData, bytesToCopy);

                // Note we're freeing the old buffer only if it was not using the internal capacity. Don't change this to 'oldData', because that would be a bug.
                if (header->Pointer != null)
                {
                    AllocatorManager.Free(allocator, header->Pointer);
                }
            }

            header->Pointer = (newData == (byte*)(header + 1)) ? null : newData;
            header->Capacity = newCapacity;
        }

        public static void Initialize(NativeBufferHeader* header, int bufferCapacity)
        {
            header->Pointer = null;
            header->Length = 0;
            header->Capacity = bufferCapacity;
        }

        public static void Destroy(NativeBufferHeader* header, AllocatorManager.AllocatorHandle allocator)
        {
            if (header->Pointer != null)
            {
                AllocatorManager.Free(allocator, header->Pointer);
            }

            Initialize(header, 0);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    [DebuggerDisplay("Length = {Length}, Capacity = {Capacity}, IsCreated = {IsCreated}")]
    [DebuggerTypeProxy(typeof(NativeBufferDebugView<>))]
    internal unsafe struct NativeBuffer<T> : IEnumerable<T> where T : unmanaged
    {
        [NoAlias, NativeDisableUnsafePtrRestriction]
        private NativeBufferHeader* m_Buffer;

        private int m_InternalCapacity; // Stores the original internal capacity of the buffer header, so heap excess can be removed entirely when trimming.
        private AllocatorManager.AllocatorHandle m_Allocator;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
        private NativeBufferArray<T>.ArrayAccessor m_ArrayAccessor;
        private NativeBufferArray<T>.BufferAccessor m_BufferAccessor;
#endif

#if ENABLE_UNITY_COLLECTIONS_CHECKS
        internal NativeBuffer(NativeBufferHeader* header, int internalCapacity, AllocatorManager.AllocatorHandle allocator,
            NativeBufferArray<T>.ArrayAccessor arrayAccessor, NativeBufferArray<T>.BufferAccessor bufferAccessor)
        {
            m_Buffer = header;
            m_InternalCapacity = internalCapacity;
            m_Allocator = allocator;
            m_ArrayAccessor = arrayAccessor;
            m_BufferAccessor = bufferAccessor;
        }
#else
        internal NativeBuffer(NativeBufferHeader* header, int internalCapacity, AllocatorManager.AllocatorHandle allocator)
        {
            m_Buffer = header;
            m_InternalCapacity = internalCapacity;
            m_Allocator = allocator;
        }
#endif

        public readonly int Length
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                CheckReadAccess();
                return m_Buffer->Length;
            }
        }

        public int Capacity
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            readonly get
            {
                CheckReadAccess();
                return m_Buffer->Capacity;
            }
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                if (value < Length)
                    throw new InvalidOperationException($"Capacity {value} can't be set smaller than Length {Length}");
#endif
                CheckWriteAccessAndInvalidateArrayAliases();
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                NativeBufferHeader.SetCapacity(m_Buffer, value, UnsafeUtility.SizeOf<T>(), UnsafeUtility.AlignOf<T>(), m_InternalCapacity, m_Allocator);
#else
                NativeBufferHeader.SetCapacity(m_Buffer, value, UnsafeUtility.SizeOf<T>(), UnsafeUtility.AlignOf<T>(), m_InternalCapacity, m_Allocator);
#endif
            }
        }

        public readonly bool IsEmpty
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => !IsCreated || Length == 0;
        }

        public readonly bool IsCreated
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => m_Buffer != null;
        }

        public T this[int index]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            readonly get
            {
                CheckReadAccess();
                CheckBounds(index);
                return UnsafeUtility.ReadArrayElement<T>(NativeBufferHeader.GetElementPointer(m_Buffer), index);
            }
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set
            {
                CheckWriteAccess();
                CheckBounds(index);
                UnsafeUtility.WriteArrayElement<T>(NativeBufferHeader.GetElementPointer(m_Buffer), index, value);
            }
        }

        public ref T ElementAt(int index)
        {
            CheckWriteAccess();
            CheckBounds(index);
            return ref UnsafeUtility.ArrayElementAsRef<T>(NativeBufferHeader.GetElementPointer(m_Buffer), index);
        }

        public void ResizeUninitialized(int length)
        {
            EnsureCapacity(length);
            m_Buffer->Length = length;
        }

        public void Resize(int length, NativeArrayOptions options = NativeArrayOptions.ClearMemory)
        {
            EnsureCapacity(length);

            int oldLength = m_Buffer->Length;
            m_Buffer->Length = length;
            if (options == NativeArrayOptions.ClearMemory && oldLength < length)
            {
                int num = length - oldLength;
                byte* ptr = NativeBufferHeader.GetElementPointer(m_Buffer);
                int sizeOf = UnsafeUtility.SizeOf<T>();
                UnsafeUtility.MemClear(ptr + oldLength * sizeOf, num * sizeOf);
            }
        }

        public void EnsureCapacity(int length)
        {
            CheckWriteAccessAndInvalidateArrayAliases();
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            NativeBufferHeader.EnsureCapacity(m_Buffer, length, UnsafeUtility.SizeOf<T>(), UnsafeUtility.AlignOf<T>(), m_Allocator);
#else
            NativeBufferHeader.EnsureCapacity(m_Buffer, length, UnsafeUtility.SizeOf<T>(), UnsafeUtility.AlignOf<T>(), m_Allocator);
#endif
        }

        public void Clear()
        {
            CheckWriteAccessAndInvalidateArrayAliases();
            m_Buffer->Length = 0;
        }

        public void TrimExcess()
        {
            CheckWriteAccessAndInvalidateArrayAliases();

            byte* oldPtr = m_Buffer->Pointer;
            int length = m_Buffer->Length;

            if (length == Capacity || oldPtr == null)
                return;

            int elemSize = UnsafeUtility.SizeOf<T>();
            int elemAlign = UnsafeUtility.AlignOf<T>();

            bool isInternal;
            byte* newPtr;

            // If the size fits in the internal buffer, prefer to move the elements back there.
            if (length <= m_InternalCapacity)
            {
                newPtr = (byte*)(m_Buffer + 1);
                isInternal = true;
            }
            else
            {
                newPtr = (byte*)AllocatorManager.Allocate(m_Allocator, elemSize, elemAlign, length);
                isInternal = false;
            }

            UnsafeUtility.MemCpy(newPtr, oldPtr, (long)elemSize * length);

            m_Buffer->Capacity = Math.Max(length, m_InternalCapacity);
            m_Buffer->Pointer = isInternal ? null : newPtr;

            AllocatorManager.Free(m_Allocator, oldPtr);
        }

        public int Add(T elem)
        {
            CheckWriteAccess();
            int length = m_Buffer->Length;
            ResizeUninitialized(length + 1);
            this[length] = elem;
            return length;
        }

        public void Insert(int index, T elem)
        {
            CheckWriteAccess();
            int length = m_Buffer->Length;
            ResizeUninitialized(length + 1);
            CheckBounds(index); //CheckBounds after ResizeUninitialized since index == length is allowed
            int elemSize = UnsafeUtility.SizeOf<T>();
            byte* basePtr = NativeBufferHeader.GetElementPointer(m_Buffer);
            UnsafeUtility.MemMove(basePtr + (index + 1) * elemSize, basePtr + index * elemSize, (long)elemSize * (length - index));
            this[index] = elem;
        }

        public void AddRange(NativeArray<T> newElems)
        {
            CheckWriteAccess();
            int elemSize = UnsafeUtility.SizeOf<T>();
            int oldLength = m_Buffer->Length;
            ResizeUninitialized(oldLength + newElems.Length);

            byte* basePtr = NativeBufferHeader.GetElementPointer(m_Buffer);
            UnsafeUtility.MemCpy(basePtr + (long)oldLength * elemSize, newElems.GetUnsafeReadOnlyPtr<T>(), (long)elemSize * newElems.Length);
        }

        public void AddRange(T* newElems, int length)
        {
            CheckWriteAccess();
            int elemSize = UnsafeUtility.SizeOf<T>();
            int oldLength = m_Buffer->Length;
            ResizeUninitialized(oldLength + length);

            byte* basePtr = NativeBufferHeader.GetElementPointer(m_Buffer);
            UnsafeUtility.MemCpy(basePtr + (long)oldLength * elemSize, newElems, (long)elemSize * length);
        }

        public void RemoveRange(int index, int count)
        {
            CheckWriteAccess();
            CheckBounds(index);
            if (count == 0)
                return;
            CheckBounds(index + count - 1);

            int elemSize = UnsafeUtility.SizeOf<T>();
            byte* basePtr = NativeBufferHeader.GetElementPointer(m_Buffer);

            UnsafeUtility.MemMove(basePtr + index * elemSize, basePtr + (index + count) * elemSize, (long)elemSize * (Length - count - index));

            m_Buffer->Length -= count;
        }

        public void RemoveRangeSwapBack(int index, int count)
        {
            CheckWriteAccess();
            CheckBounds(index);
            if (count == 0)
                return;
            CheckBounds(index + count - 1);

            ref int l = ref m_Buffer->Length;
            byte* basePtr = NativeBufferHeader.GetElementPointer(m_Buffer);
            int elemSize = UnsafeUtility.SizeOf<T>();
            int copyFrom = math.max(l - count, index + count);
            void* dst = basePtr + index * elemSize;
            void* src = basePtr + copyFrom * elemSize;
            UnsafeUtility.MemMove(dst, src, (l - copyFrom) * elemSize);
            l -= count;
        }

        public void RemoveAt(int index)
        {
            RemoveRange(index, 1);
        }

        public void RemoveAtSwapBack(int index)
        {
            CheckWriteAccess();
            CheckBounds(index);

            ref int l = ref m_Buffer->Length;
            l -= 1;
            int newLength = l;
            if (index != newLength)
            {
                byte* basePtr = NativeBufferHeader.GetElementPointer(m_Buffer);
                UnsafeUtility.WriteArrayElement(basePtr, index, UnsafeUtility.ReadArrayElement<T>(basePtr, newLength));
            }
        }

        public T* GetUnsafePtr()
        {
            CheckWriteAccess();
            return (T*)NativeBufferHeader.GetElementPointer(m_Buffer);
        }

        public T* GetUnsafeReadOnlyPtr()
        {
            CheckReadAccess();
            return (T*)NativeBufferHeader.GetElementPointer(m_Buffer);
        }

        public Span<T> AsSpan()
        {
            CheckWriteAccess();
            return new Span<T>(NativeBufferHeader.GetElementPointer(m_Buffer), Length);
        }

        public ReadOnlySpan<T> AsReadOnlySpan()
        {
            CheckReadAccess();
            return new ReadOnlySpan<T>(NativeBufferHeader.GetElementPointer(m_Buffer), Length);
        }

        public NativeBuffer<U> Reinterpret<U>() where U : unmanaged
        {
            AssertReinterpretSizesMatch<U>();
            // NOTE: We're forwarding the internal capacity along to this aliased, type-punned buffer.
            // That's OK, because if mutating operations happen they are all still the same size.
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            var arrayAccessor = new NativeBufferArray<U>.ArrayAccessor { m_Safety = m_ArrayAccessor.m_Safety };
            var bufferAccessor = new NativeBufferArray<U>.BufferAccessor { m_Safety = m_BufferAccessor.m_Safety };
            return new NativeBuffer<U>(m_Buffer, m_InternalCapacity, m_Allocator, arrayAccessor, bufferAccessor);
#else
            return new NativeBuffer<U>(m_Buffer, m_InternalCapacity, m_Allocator);
#endif
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public NativeArray<T> AsArray()
        {
            CheckReadAccess();
            NativeArray<T> shadow = NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<T>(NativeBufferHeader.GetElementPointer(m_Buffer), Length, Allocator.None);
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle handle = m_BufferAccessor.m_Safety;
            AtomicSafetyHandle.UseSecondaryVersion(ref handle);
            NativeArrayUnsafeUtility.SetAtomicSafetyHandle(ref shadow, handle);
#endif
            return shadow;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public NativeArray<T> GetSubArray(int index, int count)
        {
            return AsArray().GetSubArray(index, count);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public NativeArray<T> ToArray(AllocatorManager.AllocatorHandle allocator)
        {
            return CollectionHelper.CreateNativeArray<T>(AsArray(), allocator);
        }

        public void CopyTo(ref T[] array)
        {
            CheckReadAccess();

            if (array == null || array.Length != Length)
                array = new T[Length];

            fixed (void* ptr = array)
                UnsafeUtility.MemCpy(ptr, NativeBufferHeader.GetElementPointer(m_Buffer), Length * UnsafeUtility.SizeOf<T>());
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public NativeArray<T>.Enumerator GetEnumerator()
        {
            NativeArray<T> array = AsArray();
            return new NativeArray<T>.Enumerator(ref array);
        }
        IEnumerator IEnumerable.GetEnumerator() { throw new NotImplementedException(); }
        IEnumerator<T> IEnumerable<T>.GetEnumerator() { throw new NotImplementedException(); }

        public void CopyFrom(NativeArray<T> v)
        {
            if (v.Length == 0)
            {
                Clear();
                return;
            }

            //todo remove workaround: See DOTS-1454
            ResizeUninitialized(v.Length);
            NativeSlice<T> vs = new NativeSlice<T>(v);
            vs.CopyTo(AsArray());
        }

        public void CopyFrom(NativeSlice<T> v)
        {
            if (v.Length == 0)
            {
                Clear();
                return;
            }

            ResizeUninitialized(v.Length);
            v.CopyTo(AsArray());
        }

        public void CopyFrom(NativeBuffer<T> v)
        {
            if (v.Length == 0)
            {
                Clear();
                return;
            }

            ResizeUninitialized(v.Length);
            v.CheckReadAccess();
            CheckWriteAccess();

            UnsafeUtility.MemCpy(
                NativeBufferHeader.GetElementPointer(m_Buffer),
                NativeBufferHeader.GetElementPointer(v.m_Buffer), Length * UnsafeUtility.SizeOf<T>());
        }

        public void CopyFrom(T[] v)
        {
            if (v == null)
                throw new ArgumentNullException(nameof(v));

            if (v.Length == 0)
            {
                Clear();
                return;
            }

            ResizeUninitialized(v.Length);
            CheckWriteAccess();

            GCHandle gcHandle = GCHandle.Alloc((object)v, GCHandleType.Pinned);
            IntPtr num = gcHandle.AddrOfPinnedObject();

            UnsafeUtility.MemCpy(NativeBufferHeader.GetElementPointer(m_Buffer), (void*)num, Length * UnsafeUtility.SizeOf<T>());
            gcHandle.Free();
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        private readonly void CheckBounds(int index)
        {
            if (Hint.Unlikely(((uint)index >= (uint)Length)))
                throw new IndexOutOfRangeException($"Index {index} is out of range in NativeBuffer of '{Length}' Length.");
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        private readonly void CheckReadAccess()
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckReadAndThrow(m_ArrayAccessor.m_Safety);
            AtomicSafetyHandle.CheckReadAndThrow(m_BufferAccessor.m_Safety);
#endif
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        private void CheckWriteAccess()
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckWriteAndThrow(m_ArrayAccessor.m_Safety);
            AtomicSafetyHandle.CheckWriteAndThrow(m_BufferAccessor.m_Safety);
#endif
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        private void CheckWriteAccessAndInvalidateArrayAliases()
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckWriteAndThrow(m_ArrayAccessor.m_Safety);
            AtomicSafetyHandle.CheckWriteAndBumpSecondaryVersion(m_BufferAccessor.m_Safety);
#endif
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        private static void AssertReinterpretSizesMatch<U>() where U : struct
        {
            if (UnsafeUtility.SizeOf<U>() != UnsafeUtility.SizeOf<T>())
                throw new InvalidOperationException($"Types {typeof(U)} and {typeof(T)} are of different sizes; cannot reinterpret");
        }
    }

    internal class NativeBufferDebugView<T>
        where T : unmanaged
    {
        private NativeBuffer<T> m_Buffer;

        public NativeBufferDebugView(NativeBuffer<T> buffer)
        {
            m_Buffer = buffer;
        }

        public T[] Items => m_Buffer.AsArray().ToArray();
    }
}
