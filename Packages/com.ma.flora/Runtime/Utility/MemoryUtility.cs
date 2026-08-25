// Copyright © Magnetic Arcade. All Rights Reserved.

using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace MA.Flora
{
    internal static unsafe class MemoryUtility
    {
        public static T* Allocate<T>(int count, Allocator allocator, NativeArrayOptions options) where T : unmanaged
        {
            var ptr = (T*)UnsafeUtility.Malloc(UnsafeUtility.SizeOf<T>() * count, UnsafeUtility.AlignOf<T>(), allocator);
            if (options == NativeArrayOptions.ClearMemory)
                UnsafeUtility.MemClear(ptr, UnsafeUtility.SizeOf<T>() * count);
            return ptr;
        }

        public static void Free<T>(T* ptr, Allocator allocator) where T : unmanaged
        {
            UnsafeUtility.Free(ptr, allocator);
        }
    }
}
