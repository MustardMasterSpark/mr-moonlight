// Copyright © Magnetic Arcade. All Rights Reserved.

using System;
using System.Runtime.CompilerServices;
using Unity.Collections.LowLevel.Unsafe;

namespace MA.Flora
{
    internal static class UnsafeListExtensions
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T Pop<T>(ref this UnsafeList<T> list) where T : unmanaged
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (list.Length == 0)
                throw new InvalidOperationException("The list is empty.");
#endif
            var value = list[^1];
            list.Length -= 1;
            return value;
        }
    }
}
