// Copyright © Magnetic Arcade. All Rights Reserved.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace MA.Flora
{
    /// <summary>
    /// A native container that manages a single unmanaged <typeparamref name="T"/> instance with memory safety checks.
    /// Provides read-write access to the stored value and properly disposes both the allocated memory and the managed resource.
    /// </summary>
    /// <typeparam name="T">An unmanaged type that implements <see cref="IDisposable"/>.</typeparam>
    [StructLayout(LayoutKind.Sequential)]
    [NativeContainer]
    internal unsafe struct NativeData<T> : IDisposable where T : unmanaged, IDisposable
    {
        [NativeDisableUnsafePtrRestriction] internal T* m_Data;
        private AllocatorManager.AllocatorHandle m_AllocatorLabel;
#if ENABLE_UNITY_COLLECTIONS_CHECKS
        internal AtomicSafetyHandle m_Safety;
        private static readonly SharedStatic<int> SafetyID = SharedStatic<int>.GetOrCreate<NativeData<T>>();
#endif

        /// <summary>
        /// Initializes a new instance of the <see cref="NativeData{T}"/> struct with an uninitialized <typeparamref name="T"/>.
        /// </summary>
        /// <param name="allocator">The allocator used for memory management.</param>
        public NativeData(AllocatorManager.AllocatorHandle allocator)
        {
            m_AllocatorLabel = allocator;
            m_Data = AllocatorManager.Allocate<T>(allocator);
            UnsafeUtility.MemClear(m_Data, UnsafeUtility.SizeOf<T>());

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            m_Safety = CollectionHelper.CreateSafetyHandle(allocator);
            if (UnsafeUtility.IsNativeContainerType<T>())
                AtomicSafetyHandle.SetNestedContainer(m_Safety, true);

            CollectionHelper.SetStaticSafetyId<NativeDataReference<T>>(ref m_Safety, ref SafetyID.Data);
#endif
        }

        /// <summary>
        /// Disposes the container and frees the allocated <typeparamref name="T"/>.
        /// Also calls <see cref="IDisposable.Dispose"/> on the contained object if applicable.
        /// </summary>
        public void Dispose()
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (!AtomicSafetyHandle.IsDefaultValue(m_Safety))
            {
                AtomicSafetyHandle.CheckExistsAndThrow(m_Safety);
            }
#endif
            if (!IsCreated)
            {
                return;
            }

            if (m_AllocatorLabel > Allocator.None)
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                CollectionHelper.DisposeSafetyHandle(ref m_Safety);
#endif
                m_Data->Dispose();

                AllocatorManager.Free(m_AllocatorLabel, m_Data);
            }

            m_Data = null;
            m_AllocatorLabel = Allocator.Invalid;
        }

        /// <summary>
        /// Indicates whether the underlying data has been allocated.
        /// </summary>
        public bool IsCreated
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => m_Data != null;
        }

        /// <summary>
        /// Provides read-only access to the contained value.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown if the safety checks fail or the container is not valid.</exception>
        public ref readonly T ValueRO
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
#endif
                return ref *m_Data;
            }
        }

        /// <summary>
        /// Provides read-write access to the contained value.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown if the safety checks fail or the container is not valid.</exception>
        public ref T ValueRW
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
#endif
                return ref *m_Data;
            }
        }

        /// <summary>
        /// Returns a raw pointer to the contained value for unsafe operations.
        /// </summary>
        /// <remarks>
        /// Use with caution as this pointer can bypass safety checks.
        /// </remarks>
        /// <exception cref="InvalidOperationException">Thrown if the safety checks fail or the container is not valid.</exception>
        /// <returns>A pointer to the stored <typeparamref name="T"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T* GetUnsafePtr()
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
#endif
            return m_Data;
        }
    }

    /// <summary>
    /// A reference type for <see cref="NativeData{T}"/>, providing safe read-write access to the same underlying data without ownership.
    /// </summary>
    /// <typeparam name="T">An unmanaged type that implements <see cref="IDisposable"/>.</typeparam>
    [StructLayout(LayoutKind.Sequential)]
    internal unsafe struct NativeDataReference<T> where T : unmanaged, IDisposable
    {
        /// <summary>
        /// A pointer to the referenced <typeparamref name="T"/> data.
        /// </summary>
        [NativeDisableUnsafePtrRestriction] private T* m_Data;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
        private AtomicSafetyHandle m_Safety;
#endif

        /// <summary>
        /// Initializes a new instance of the <see cref="NativeDataReference{T}"/> struct from an existing <see cref="NativeData{T}"/>.
        /// </summary>
        /// <param name="data">A native data container whose resource is referenced.</param>
        public NativeDataReference(NativeData<T> data)
        {
            m_Data = data.m_Data;
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            m_Safety = data.m_Safety;
#endif
        }

        /// <summary>
        /// Indicates whether the underlying data is valid and accessible.
        /// </summary>
        public bool IsCreated
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => m_Data != null;
        }

        /// <summary>
        /// Provides read-only access to the referenced value.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown if the safety checks fail or the reference is not valid.</exception>
        public ref readonly T ValueRO
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
#endif
                return ref *m_Data;
            }
        }

        /// <summary>
        /// Provides read-write access to the referenced value.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown if the safety checks fail or the reference is not valid.</exception>
        public ref T ValueRW
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
#endif
                return ref *m_Data;
            }
        }

        /// <summary>
        /// Returns a raw pointer to the referenced value for unsafe operations.
        /// </summary>
        /// <remarks>
        /// Use with caution as this pointer can bypass safety checks.
        /// </remarks>
        /// <exception cref="InvalidOperationException">Thrown if the safety checks fail or the reference is not valid.</exception>
        /// <returns>A pointer to the referenced <typeparamref name="T"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T* GetUnsafePtr()
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
#endif
            return m_Data;
        }

        /// <summary>
        /// Implicitly creates a new <see cref="NativeDataReference{T}"/> from a <see cref="NativeData{T}"/>.
        /// </summary>
        /// <param name="data">The native data container whose resource is referenced.</param>
        /// <returns>A new <see cref="NativeDataReference{T}"/> referencing the same resource.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator NativeDataReference<T>(NativeData<T> data) => new(data);
    }
}
