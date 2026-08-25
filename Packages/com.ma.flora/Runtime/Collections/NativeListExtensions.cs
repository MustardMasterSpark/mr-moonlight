// Copyright © Magnetic Arcade. All Rights Reserved.

using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace MA.Flora
{
    internal static class NativeListExtensions
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsValidIndex<T>(in this NativeList<T> list, int index) where T : unmanaged
        {
            return index >= 0 && index < list.Length;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetOrAdd<T>(this NativeList<T> list, int index, T value) where T : unmanaged
        {
            if (index == list.Length)
            {
                list.Add(value);
            }
            else if (index > list.Length)
            {
                list.Resize(index, NativeArrayOptions.ClearMemory);
                list.Add(value);
            }
            else
            {
                list[index] = value;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T Pop<T>(this NativeList<T> list) where T : unmanaged
        {
            var index = list.Length - 1;
            var value = list[index];
            list.RemoveAtSwapBack(index);
            return value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe void Fill<T>(this NativeList<T> list, T value, int startIndex = 0, int length = -1) where T : unmanaged
        {
            if (length < 0) length = list.Length - startIndex;
            UnsafeUtility.MemCpyReplicate(list.GetUnsafePtr() + startIndex, &value, UnsafeUtility.SizeOf<T>(), length);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Initialize<T>(this NativeList<T> list, in T initValue, int count) where T : unmanaged
        {
            list.ResizeUninitialized(count);
            list.Fill(initValue);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Reserve<T>(this NativeList<T> list, int count) where T : unmanaged
        {
            if (list.Capacity < count)
                list.Capacity = count;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe NativeArray<T> TransferOwnershipToNativeArray<T>(ref this NativeList<T> list)
            where T : unmanaged
        {
            if (!list.IsCreated)
                return default;

            var listData = list.GetUnsafeList();
            var allocator = listData->Allocator;
            var array = CollectionHelper.ConvertExistingNativeListToNativeArray(ref list, list.Length, allocator);
            AllocatorManager.Free(allocator, listData);
            list = default;

            return array;
        }
    }
}
