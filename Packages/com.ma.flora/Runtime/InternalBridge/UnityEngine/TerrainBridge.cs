// Copyright © Magnetic Arcade. All Rights Reserved.

using System;
using System.Collections.Generic;
using System.Reflection;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.Bindings;
using UnityObject = UnityEngine.Object;

namespace MA.InternalBridge
{
    internal static class TerrainBridge
    {
        private static class TerrainDataInternal
        {
            public delegate void GetInterpolatedNormalInjected(IntPtr terrainDataPtr, float x, float y, out Vector3 normal);
            public static GetInterpolatedNormalInjected GetInterpolatedNormal;

            public delegate void InternalGetTreeInstancesInjected(IntPtr terrainDataPtr, out BlittableArrayWrapper treeInstances);
            public static InternalGetTreeInstancesInjected GetTreeInstances;

            public delegate void InternalSetTreeInstancesInjected(IntPtr terrainDataPtr, ref ManagedSpanWrapper instances, bool snapToHeightmap);
            public static InternalSetTreeInstancesInjected SetTreeInstances;

            public delegate void ComputeDetailInstanceTransformsInjected(IntPtr terrainDataPtr, int patchX, int patchY, int layer, float density, out Bounds bounds, out BlittableArrayWrapper ret);
            public static ComputeDetailInstanceTransformsInjected ComputeDetailInstanceTransforms;

            public delegate void InternalSetDetailLayerInjected(IntPtr terrainDataPtr, int xBase, int yBase, int totalWidth, int totalHeight, int detailIndex, ref ManagedSpanWrapper data);
            public static InternalSetDetailLayerInjected SetDetailLayer;
        }

        private static class UnityObjectInternal
        {
#if UNITY_6000_3_OR_NEWER
            public delegate IntPtr GetPtrFromInstanceIDPrivate(EntityId instanceID, Type objectType, out bool isMonoBehaviour);
#else
            public delegate IntPtr GetPtrFromInstanceIDPrivate(int instanceID, Type objectType, out bool isMonoBehaviour);
#endif
            public static GetPtrFromInstanceIDPrivate GetPtrFromInstanceID;
        }

        static TerrainBridge()
        {
            TerrainDataInternal.GetInterpolatedNormal = (TerrainDataInternal.GetInterpolatedNormalInjected)typeof(TerrainData)
                .GetMethod("GetInterpolatedNormal_Injected", BindingFlags.NonPublic | BindingFlags.Static)?
                .CreateDelegate(typeof(TerrainDataInternal.GetInterpolatedNormalInjected));

            TerrainDataInternal.GetTreeInstances = (TerrainDataInternal.InternalGetTreeInstancesInjected)typeof(TerrainData)
                .GetMethod("Internal_GetTreeInstances_Injected", BindingFlags.NonPublic | BindingFlags.Static)?
                .CreateDelegate(typeof(TerrainDataInternal.InternalGetTreeInstancesInjected));

            TerrainDataInternal.SetTreeInstances = (TerrainDataInternal.InternalSetTreeInstancesInjected)typeof(TerrainData)
                .GetMethod("SetTreeInstances_Injected", BindingFlags.NonPublic | BindingFlags.Static)?
                .CreateDelegate(typeof(TerrainDataInternal.InternalSetTreeInstancesInjected));

            TerrainDataInternal.ComputeDetailInstanceTransforms = (TerrainDataInternal.ComputeDetailInstanceTransformsInjected)typeof(TerrainData)
                .GetMethod("ComputeDetailInstanceTransforms_Injected", BindingFlags.NonPublic | BindingFlags.Static)?
                .CreateDelegate(typeof(TerrainDataInternal.ComputeDetailInstanceTransformsInjected));

            TerrainDataInternal.SetDetailLayer = (TerrainDataInternal.InternalSetDetailLayerInjected)typeof(TerrainData)
                .GetMethod("Internal_SetDetailLayer_Injected", BindingFlags.NonPublic | BindingFlags.Static)?
                .CreateDelegate(typeof(TerrainDataInternal.InternalSetDetailLayerInjected));

            UnityObjectInternal.GetPtrFromInstanceID = (UnityObjectInternal.GetPtrFromInstanceIDPrivate)typeof(UnityObject)
                .GetMethod("GetPtrFromInstanceID", BindingFlags.NonPublic | BindingFlags.Static)?
                .CreateDelegate(typeof(UnityObjectInternal.GetPtrFromInstanceIDPrivate));

            if (TerrainDataInternal.GetInterpolatedNormal == null)
                Debug.LogError("Failed to find GetInterpolatedNormal_Injected method. Falling back to default implementation.");

            if (TerrainDataInternal.GetTreeInstances == null)
                Debug.LogError("Failed to find Internal_GetTreeInstances_Injected method. Falling back to default implementation.");

            if (TerrainDataInternal.SetTreeInstances == null)
                Debug.LogError("Failed to find SetTreeInstances_Injected method. Falling back to default implementation.");

            if (TerrainDataInternal.ComputeDetailInstanceTransforms == null)
                Debug.LogError("Failed to find ComputeDetailInstanceTransforms_Injected method. Falling back to default implementation.");

            if (TerrainDataInternal.SetDetailLayer == null)
                Debug.LogError("Failed to find SetDetailLayer_Injected method. Falling back to default implementation.");

            if (UnityObjectInternal.GetPtrFromInstanceID == null)
                Debug.LogError("Failed to find GetPtrFromInstanceID method. Falling back to default implementation.");
        }

        public static IntPtr MarshalFromInstanceId<T>(EntityId instanceId)
        {
            if (instanceId == EntityId.None)
                return IntPtr.Zero;

#if UNITY_6000_3_OR_NEWER
            return UnityObjectInternal.GetPtrFromInstanceID(instanceId, typeof(T), out bool _);
#else
            return UnityObjectInternal.GetPtrFromInstanceID(instanceId, typeof(T), out bool _);
#endif
        }

        public static void RemoveTreePrototype(TerrainData terrainData, int index)
        {
            terrainData.RemoveTreePrototype(index);
        }

        public static Vector3 GetInterpolatedNormal(IntPtr terrainDataPtr, float x, float y)
        {
            if (terrainDataPtr == IntPtr.Zero)
                throw new ArgumentNullException(nameof(terrainDataPtr));

            TerrainDataInternal.GetInterpolatedNormal(terrainDataPtr, x, y, out Vector3 normal);
            return normal;
        }

        public static unsafe void GetTreeInstances(TerrainData terrainData, List<TreeInstance> treeInstances)
        {
            treeInstances.Clear();

            if (TerrainDataInternal.GetTreeInstances == null)
            {
                treeInstances.AddRange(terrainData.treeInstances);
                return;
            }

            BlittableArrayWrapper ret = default;
            try
            {
                IntPtr unitySelf = UnityObject.MarshalledUnityObject.MarshalNotNull(terrainData);
                if (unitySelf == IntPtr.Zero)
                    ThrowHelper.ThrowNullReferenceException(terrainData);

                TerrainDataInternal.GetTreeInstances(unitySelf, out ret);
            }
            finally
            {
                GetValues(ref ret, treeInstances);
            }
        }

        public static void GetTreeInstances(TerrainData terrainData, NativeList<TreeInstance> treeInstances)
        {
            treeInstances.Clear();

            if (TerrainDataInternal.GetTreeInstances == null)
            {
                treeInstances.AddRange(new NativeArray<TreeInstance>(terrainData.treeInstances, Allocator.Temp));
                return;
            }

            BlittableArrayWrapper ret = default;
            try
            {
                IntPtr unitySelf = UnityObject.MarshalledUnityObject.MarshalNotNull(terrainData);
                if (unitySelf == IntPtr.Zero)
                    ThrowHelper.ThrowNullReferenceException(terrainData);

                TerrainDataInternal.GetTreeInstances(unitySelf, out ret);
            }
            finally
            {
                GetValues(ref ret, treeInstances);
            }
        }

        public static NativeArray<TreeInstance> GetTreeInstances(TerrainData terrainData, Allocator allocator)
        {
            if (TerrainDataInternal.GetTreeInstances == null)
                return new NativeArray<TreeInstance>(terrainData.treeInstances, allocator);

            NativeArray<TreeInstance> treeInstances = default;

            BlittableArrayWrapper ret = default;
            try
            {
                IntPtr unitySelf = UnityObject.MarshalledUnityObject.MarshalNotNull(terrainData);
                if (unitySelf == IntPtr.Zero)
                    ThrowHelper.ThrowNullReferenceException(terrainData);

                TerrainDataInternal.GetTreeInstances(unitySelf, out ret);
            }
            finally
            {
                treeInstances = ToNativeArray<TreeInstance>(ref ret, allocator);
            }

            return treeInstances;
        }

        public static NativeArray<TreeInstance> GetTreeInstances(IntPtr terrainDataPtr, Allocator allocator)
        {
            if (terrainDataPtr == IntPtr.Zero)
                throw new ArgumentNullException(nameof(terrainDataPtr));

            NativeArray<TreeInstance> treeInstances = default;
            BlittableArrayWrapper ret = default;
            try
            {
                TerrainDataInternal.GetTreeInstances(terrainDataPtr, out ret);
            }
            finally
            {
                treeInstances = ToNativeArray<TreeInstance>(ref ret, allocator);
            }

            return treeInstances;
        }

        public static unsafe void SetTreeInstances(TerrainData terrainData, NativeArray<TreeInstance> instances, bool snapToHeightmap)
        {
            if (TerrainDataInternal.SetTreeInstances == null)
            {
                terrainData.SetTreeInstances(instances.ToArray(), snapToHeightmap);
                return;
            }

            if (!instances.IsCreated)
                throw new ArgumentNullException(nameof(instances));

            IntPtr terrainDataPtr = UnityObject.MarshalledUnityObject.MarshalNotNull(terrainData);
            if (terrainDataPtr == IntPtr.Zero)
                ThrowHelper.ThrowNullReferenceException(terrainData);

            ManagedSpanWrapper spanWrapper = new ManagedSpanWrapper(instances.GetUnsafePtr(), instances.Length);
            TerrainDataInternal.SetTreeInstances(terrainDataPtr, ref spanWrapper, snapToHeightmap);
        }

        public static unsafe void SetTreeInstances(IntPtr terrainDataPtr, NativeArray<TreeInstance> instances, bool snapToHeightmap)
        {
            if (!instances.IsCreated)
                throw new ArgumentNullException(nameof(instances));
            if (terrainDataPtr == IntPtr.Zero)
                throw new ArgumentNullException(nameof(terrainDataPtr));

            ManagedSpanWrapper spanWrapper = new ManagedSpanWrapper(instances.GetUnsafePtr(), instances.Length);
            TerrainDataInternal.SetTreeInstances(terrainDataPtr, ref spanWrapper, snapToHeightmap);
        }

        public static NativeArray<DetailInstanceTransform> ComputeDetailInstanceTransforms(
            TerrainData terrainData,
            int patchX,
            int patchY,
            int layer,
            float density,
            Allocator allocator,
            out Bounds bounds)
        {
            if (TerrainDataInternal.ComputeDetailInstanceTransforms == null)
                return new NativeArray<DetailInstanceTransform>(terrainData.ComputeDetailInstanceTransforms(patchX, patchY, layer, density, out bounds), allocator);

            NativeArray<DetailInstanceTransform> instanceTransforms = default;
            BlittableArrayWrapper ret = default;
            try
            {
                IntPtr terrainDataPtr = UnityObject.MarshalledUnityObject.MarshalNotNull(terrainData);
                if (terrainDataPtr == IntPtr.Zero)
                    ThrowHelper.ThrowNullReferenceException(terrainData);

                TerrainDataInternal.ComputeDetailInstanceTransforms(terrainDataPtr, patchX, patchY, layer, density, out bounds, out ret);
            }
            finally
            {
                instanceTransforms = ToNativeArray<DetailInstanceTransform>(ref ret, allocator);
            }

            return instanceTransforms;
        }

        public static NativeArray<DetailInstanceTransform> ComputeDetailInstanceTransforms(
            IntPtr terrainDataPtr,
            int patchX,
            int patchY,
            int layer,
            float density,
            Allocator allocator,
            out Bounds bounds)
        {
            if (terrainDataPtr == IntPtr.Zero)
                throw new ArgumentNullException(nameof(terrainDataPtr));

            NativeArray<DetailInstanceTransform> instanceTransforms = default;
            BlittableArrayWrapper ret = default;
            try
            {
                TerrainDataInternal.ComputeDetailInstanceTransforms(terrainDataPtr, patchX, patchY, layer, density, out bounds, out ret);
            }
            finally
            {
                instanceTransforms = ToNativeArray<DetailInstanceTransform>(ref ret, allocator);
            }

            return instanceTransforms;
        }

        public static unsafe void SetDetailLayer(TerrainData terrainData, int xBase, int yBase, int totalWidth, int totalHeight, int detailIndex, NativeArray<int> data)
        {
            if (!data.IsCreated)
                throw new ArgumentNullException(nameof(data), "data is not created");

            IntPtr terrainDataPtr = UnityObject.MarshalledUnityObject.MarshalNotNull(terrainData);
            if (terrainDataPtr == IntPtr.Zero)
                ThrowHelper.ThrowNullReferenceException(terrainData);

            ManagedSpanWrapper spanWrapper = new ManagedSpanWrapper(data.GetUnsafeReadOnlyPtr(), data.Length);
            TerrainDataInternal.SetDetailLayer(terrainDataPtr, xBase, yBase, totalWidth, totalHeight, detailIndex, ref spanWrapper);
        }

        private static unsafe NativeArray<T> ToNativeArray<T>(ref BlittableArrayWrapper array, Allocator allocator) where T : unmanaged
        {
#if UNITY_6000_5_OR_NEWER
            MarshalledArray marshalledArray = array.arrayWrapper;
            try
            {
                Span<T> source = marshalledArray.AsSpan<T>();
                var destination = new NativeArray<T>(source.Length, allocator, NativeArrayOptions.UninitializedMemory);
                source.CopyTo(destination.AsSpan());
                return destination;
            }
            finally
            {
                marshalledArray.Free();
                array = default;
            }
#else
            NativeArray<T> dst = default;

            switch (array.updateFlags)
            {
                case BlittableArrayWrapper.UpdateFlags.SizeChanged:
                case BlittableArrayWrapper.UpdateFlags.DataIsNativePointer:
                {
                    dst = new NativeArray<T>(array.size, allocator, NativeArrayOptions.UninitializedMemory);
                    UnsafeUtility.MemCpy(dst.GetUnsafePtr(), array.data, array.size * sizeof(T));
                    break;
                }
                case BlittableArrayWrapper.UpdateFlags.DataIsNativeOwnedMemory:
                {
                    dst = new NativeArray<T>(array.size, allocator, NativeArrayOptions.UninitializedMemory);
                    var src = BindingsAllocator.GetNativeOwnedDataPointer(array.data);
                    UnsafeUtility.MemCpy(dst.GetUnsafePtr(), src, array.size * sizeof(T));
                    BindingsAllocator.FreeNativeOwnedMemory(array.data);
                    break;
                }
                default:
                {
                    dst = new NativeArray<T>(0, allocator);
                    break;
                }
            }

            return dst;
#endif
        }

        private static unsafe void GetValues<T>(ref BlittableArrayWrapper array, NativeList<T> list) where T : unmanaged
        {
#if UNITY_6000_5_OR_NEWER
            MarshalledArray marshalledArray = array.arrayWrapper;
            try
            {
                Span<T> source = marshalledArray.AsSpan<T>();
                int destinationStart = list.Length;
                list.ResizeUninitialized(destinationStart + source.Length);
                source.CopyTo(list.AsArray().GetSubArray(destinationStart, source.Length).AsSpan());
            }
            finally
            {
                marshalledArray.Free();
                array = default;
            }
#else
            switch (array.updateFlags)
            {
                case BlittableArrayWrapper.UpdateFlags.SizeChanged:
                case BlittableArrayWrapper.UpdateFlags.DataIsNativePointer:
                {
                    list.AddRange(array.data, array.size);
                    break;
                }
                case BlittableArrayWrapper.UpdateFlags.DataIsNativeOwnedMemory:
                {
                    list.AddRange(BindingsAllocator.GetNativeOwnedDataPointer(array.data), array.size);
                    BindingsAllocator.FreeNativeOwnedMemory(array.data);
                    break;
                }
            }
#endif
        }

        private static unsafe void GetValues<T>(ref BlittableArrayWrapper array, List<T> list) where T : unmanaged
        {
#if UNITY_6000_5_OR_NEWER
            MarshalledArray marshalledArray = array.arrayWrapper;
            try
            {
                Span<T> source = marshalledArray.AsSpan<T>();
                EnsureListElemCount(list, source.Length);
                source.CopyTo(ExtractArrayFromListT(list).AsSpan(0, source.Length));
            }
            finally
            {
                marshalledArray.Free();
                array = default;
            }
#else
            switch (array.updateFlags)
            {
                case BlittableArrayWrapper.UpdateFlags.SizeChanged:
                case BlittableArrayWrapper.UpdateFlags.DataIsNativePointer:
                {
                    EnsureListElemCount(list, array.size);
                    new Span<T>(array.data, array.size).CopyTo(ExtractArrayFromListT(list).AsSpan(0, array.size));
                    break;
                }
                case BlittableArrayWrapper.UpdateFlags.DataIsNativeOwnedMemory:
                {
                    EnsureListElemCount(list, array.size);
                    new Span<T>(BindingsAllocator.GetNativeOwnedDataPointer(array.data), array.size).CopyTo(ExtractArrayFromListT(list).AsSpan(0, array.size));
                    BindingsAllocator.FreeNativeOwnedMemory(array.data);
                    break;
                }
            }
#endif
        }

        #region List<T> Private Access

        private class ListPrivateFieldAccess<T>
        {
            internal T[] _items;
            internal int _size;
            internal int _version;
        }

        private static T[] ExtractArrayFromListT<T>(List<T> list)
        {
            if (list == null)
                throw new ArgumentNullException(nameof(list));

            var privateFieldAccess = UnsafeUtility.As<List<T>, ListPrivateFieldAccess<T>>(ref list);
            return privateFieldAccess._items;
        }

        private static void EnsureListElemCount<T>(List<T> list, int count)
        {
            if (list == null)
                throw new ArgumentNullException(nameof (list));
            if (count < 0)
                throw new ArgumentException("invalid size to resize.", nameof (list));

            list.Clear();
            if (list.Capacity < count)
                list.Capacity = count;
            if (count == list.Count)
                return;

            var privateFieldAccess = UnsafeUtility.As<List<T>, ListPrivateFieldAccess<T>>(ref list);
            privateFieldAccess._size = count;
            ++privateFieldAccess._version;
        }

        #endregion
    }
}
