// Copyright © Magnetic Arcade. All Rights Reserved.

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

namespace MA.Flora
{
    internal static class NativeArrayExtensions
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsValidIndex<T>(this NativeArray<T> array, int index) where T : struct => index >= 0 && index < array.Length;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe void Fill<T>(this ref NativeArray<T> array, T value, int startIndex = 0, int length = -1) where T : unmanaged
        {
            if (length < 0) length = array.Length - startIndex;
            CheckIndexCount(array, startIndex, length);
            UnsafeUtility.MemCpyReplicate((T*)array.GetUnsafePtr() + startIndex, &value, UnsafeUtility.SizeOf<T>(), length);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe void MemClear<T>(this ref NativeArray<T> array, int startIndex = 0, int length = -1) where T : unmanaged
        {
            if (length < 0) length = array.Length - startIndex;
            CheckIndexCount(array, startIndex, length);
            UnsafeUtility.MemClear((T*)array.GetUnsafePtr() + startIndex, length * UnsafeUtility.SizeOf<T>());
        }

        public static void ResizeArraySafe<T>(this ref NativeArray<T> array, int newSize, Allocator allocator = Allocator.Persistent, NativeArrayOptions options = NativeArrayOptions.ClearMemory) where T : unmanaged
        {
            if (newSize == array.Length)
                return;

            NativeArray<T> newArray = new NativeArray<T>(newSize, allocator, options);
            if (array.IsCreated)
            {
                int copyLength = math.min(array.Length, newSize);
                if (copyLength > 0)
                    NativeArray<T>.Copy(array, newArray, copyLength);

                array.Dispose();
            }

            array = newArray;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe T* GetUnsafePtrT<T>(this NativeArray<T> array) where T : unmanaged => (T*)array.GetUnsafePtr();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe T* GetUnsafeReadOnlyPtrT<T>(this NativeArray<T> array) where T : unmanaged => (T*)array.GetUnsafeReadOnlyPtr();

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void CheckIndexCount<T>(NativeArray<T> array, int index, int count) where T : unmanaged
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (count < 0)
                throw new ArgumentOutOfRangeException($"Value for count {count} must be positive.");
            if (index < 0)
                throw new IndexOutOfRangeException($"Value for index {index} must be positive.");
            if (index > array.Length)
                throw new IndexOutOfRangeException($"Value for index {index} is out of bounds.");
            if (index + count > array.Length)
                throw new ArgumentOutOfRangeException($"Value for count {count} is out of bounds.");
#endif
        }
    }
}
