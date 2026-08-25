// Copyright © Magnetic Arcade. All Rights Reserved.
// ReSharper disable InconsistentNaming

using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MA.InternalBridge
{
    internal struct UnityTypeDispatchData : IDisposable
    {
        private TypeDispatchData m_TypeDispatchData;

        public Object[] changed => m_TypeDispatchData.changed;
#if UNITY_6000_5_OR_NEWER
        public NativeArray<EntityId> changedID => m_TypeDispatchData.changedID;
        public NativeArray<EntityId> destroyedID => m_TypeDispatchData.destroyedID;
#else
        public NativeArray<EntityId> changedID => m_TypeDispatchData.changedID.Reinterpret<EntityId>();
        public NativeArray<EntityId> destroyedID => m_TypeDispatchData.destroyedID.Reinterpret<EntityId>();
#endif

        public static implicit operator UnityTypeDispatchData(TypeDispatchData typeDispatchData) => new() { m_TypeDispatchData = typeDispatchData };
        public static implicit operator TypeDispatchData(UnityTypeDispatchData typeDispatchData) => typeDispatchData.m_TypeDispatchData;

        public void Dispose()
        {
            m_TypeDispatchData.Dispose();
        }

        public void Dispose(JobHandle inputDeps)
        {
            m_TypeDispatchData.changed = null;
            m_TypeDispatchData.changedID.Dispose(inputDeps);
            m_TypeDispatchData.destroyedID.Dispose(inputDeps);
        }
    }

    internal struct UnityTransformDispatchData : IDisposable
    {
        private TransformDispatchData m_TransformDispatchData;

#if UNITY_6000_5_OR_NEWER
        public NativeArray<EntityId> transformedID => m_TransformDispatchData.transformedID;
        public NativeArray<EntityId> parentID => m_TransformDispatchData.parentID;
#else
        public NativeArray<EntityId> transformedID => m_TransformDispatchData.transformedID.Reinterpret<EntityId>();
        public NativeArray<EntityId> parentID => m_TransformDispatchData.parentID.Reinterpret<EntityId>();
#endif
        public NativeArray<Matrix4x4> localToWorldMatrices => m_TransformDispatchData.localToWorldMatrices;
        public NativeArray<Vector3> positions => m_TransformDispatchData.positions;
        public NativeArray<Quaternion> rotations => m_TransformDispatchData.rotations;
        public NativeArray<Vector3> scales => m_TransformDispatchData.scales;

        public static implicit operator UnityTransformDispatchData(TransformDispatchData transformDispatchData) => new() { m_TransformDispatchData = transformDispatchData };
        public static implicit operator TransformDispatchData(UnityTransformDispatchData transformDispatchData) => transformDispatchData.m_TransformDispatchData;

        public void Dispose()
        {
            m_TransformDispatchData.Dispose();
        }

        public void Dispose(JobHandle inputDeps)
        {
            m_TransformDispatchData.transformedID.Dispose(inputDeps);
            m_TransformDispatchData.parentID.Dispose(inputDeps);
            m_TransformDispatchData.localToWorldMatrices.Dispose(inputDeps);
            m_TransformDispatchData.positions.Dispose(inputDeps);
            m_TransformDispatchData.rotations.Dispose(inputDeps);
            m_TransformDispatchData.scales.Dispose(inputDeps);
        }
    }

    internal class UnityObjectDispatcher : IDisposable
    {
        public enum TransformTrackingType
        {
            GlobalTRS,
            LocalTRS,
            Hierarchy,
        }

        [Flags]
        public enum TypeTrackingFlags
        {
            SceneObjects      = 1 << 0,
            Assets            = 1 << 1,
            EditorOnlyObjects = 1 << 2,
            Default           = Assets | SceneObjects,
            All               = Default | EditorOnlyObjects,
        }

        private readonly ObjectDispatcher m_ObjectDispatcher = new ObjectDispatcher();

        public int maxDispatchHistoryFramesCount
        {
            get => m_ObjectDispatcher.maxDispatchHistoryFramesCount;
            set => m_ObjectDispatcher.maxDispatchHistoryFramesCount = value;
        }

        public void Dispose()
        {
            m_ObjectDispatcher.Dispose();
        }

        public bool IsValid()
        {
            return m_ObjectDispatcher.valid;
        }

        public void DispatchTypeChangesAndClear(Type type, Action<TypeDispatchData> callback, bool sortByInstanceID = false, bool noScriptingArray = false)
        {
            m_ObjectDispatcher.DispatchTypeChangesAndClear(type, callback, sortByInstanceID, noScriptingArray);
        }

        public void DispatchTransformChangesAndClear(Type type, TransformTrackingType trackingType, Action<Component[]> callback, bool sortByInstanceID = false)
        {
            m_ObjectDispatcher.DispatchTransformChangesAndClear(type, (ObjectDispatcher.TransformTrackingType)trackingType, callback, sortByInstanceID);
        }

        public void DispatchTransformChangesAndClear(Type type, TransformTrackingType trackingType, Action<TransformDispatchData> callback)
        {
            m_ObjectDispatcher.DispatchTransformChangesAndClear(type, (ObjectDispatcher.TransformTrackingType)trackingType, callback);
        }

        public void ClearTypeChanges(Type type)
        {
            m_ObjectDispatcher.ClearTypeChanges(type);
        }

        public UnityTypeDispatchData GetTypeChangesAndClear(Type type, Allocator allocator, bool sortByInstanceID = false, bool noScriptingArray = false)
        {
            return m_ObjectDispatcher.GetTypeChangesAndClear(type, allocator, sortByInstanceID, noScriptingArray);
        }

        public void GetTypeChangesAndClear(Type type, List<Object> changed, out NativeArray<EntityId> changedID, out NativeArray<EntityId> destroyedID, Allocator allocator, bool sortByInstanceID = false)
        {
            m_ObjectDispatcher.GetTypeChangesAndClear(type, changed, out var changedInstanceIds, out var destroyedInstanceIds, allocator, sortByInstanceID);
#if UNITY_6000_5_OR_NEWER
            changedID = changedInstanceIds;
            destroyedID = destroyedInstanceIds;
#else
            changedID = changedInstanceIds.Reinterpret<EntityId>();
            destroyedID = destroyedInstanceIds.Reinterpret<EntityId>();
#endif
        }

        public Component[] GetTransformChangesAndClear(Type type, TransformTrackingType trackingType, bool sortByInstanceID = false)
        {
            return m_ObjectDispatcher.GetTransformChangesAndClear(type, (ObjectDispatcher.TransformTrackingType)trackingType, sortByInstanceID);
        }

        public UnityTransformDispatchData GetTransformChangesAndClear(Type type, TransformTrackingType trackingType, Allocator allocator)
        {
            return m_ObjectDispatcher.GetTransformChangesAndClear(type, (ObjectDispatcher.TransformTrackingType)trackingType, allocator);
        }

        public void EnableTypeTracking(TypeTrackingFlags typeTrackingMask, params Type[] types)
        {
            m_ObjectDispatcher.EnableTypeTracking((ObjectDispatcher.TypeTrackingFlags)typeTrackingMask, types);
        }

        public void EnableTypeTracking(params Type[] types)
        {
            m_ObjectDispatcher.EnableTypeTracking(types);
        }

        public void DisableTypeTracking(params Type[] types)
        {
            m_ObjectDispatcher.DisableTypeTracking(types);
        }

        public void EnableTransformTracking(TransformTrackingType trackingType, params Type[] types)
        {
            m_ObjectDispatcher.EnableTransformTracking((ObjectDispatcher.TransformTrackingType)trackingType, types);
        }

        public void DisableTransformTracking(TransformTrackingType trackingType, params Type[] types)
        {
            m_ObjectDispatcher.DisableTransformTracking((ObjectDispatcher.TransformTrackingType)trackingType, types);
        }

        public void DispatchTypeChangesAndClear<T>(Action<TypeDispatchData> callback, bool sortByInstanceID = false, bool noScriptingArray = false) where T : Object
        {
            m_ObjectDispatcher.DispatchTypeChangesAndClear(typeof(T), callback, sortByInstanceID, noScriptingArray);
        }

        public void DispatchTransformChangesAndClear<T>(TransformTrackingType trackingType, Action<Component[]> callback, bool sortByInstanceID = false) where T : Object
        {
            m_ObjectDispatcher.DispatchTransformChangesAndClear(typeof(T), (ObjectDispatcher.TransformTrackingType)trackingType, callback, sortByInstanceID);
        }

        public void DispatchTransformChangesAndClear<T>(TransformTrackingType trackingType, Action<TransformDispatchData> callback) where T : Object
        {
            m_ObjectDispatcher.DispatchTransformChangesAndClear(typeof(T), (ObjectDispatcher.TransformTrackingType)trackingType, callback);
        }

        public void ClearTypeChanges<T>() where T : Object
        {
            m_ObjectDispatcher.ClearTypeChanges(typeof(T));
        }

        public UnityTypeDispatchData GetTypeChangesAndClear<T>(Allocator allocator, bool sortByInstanceID = false, bool noScriptingArray = false) where T : Object
        {
            return m_ObjectDispatcher.GetTypeChangesAndClear(typeof(T), allocator, sortByInstanceID, noScriptingArray);
        }

        public void GetTypeChangesAndClear<T>(List<Object> changed, out NativeArray<EntityId> changedID, out NativeArray<EntityId> destroyedID, Allocator allocator, bool sortByInstanceID = false) where T : Object
        {
            m_ObjectDispatcher.GetTypeChangesAndClear(typeof(T), changed, out var changedInstanceIds, out var destroyedInstanceIds, allocator, sortByInstanceID);
#if UNITY_6000_5_OR_NEWER
            changedID = changedInstanceIds;
            destroyedID = destroyedInstanceIds;
#else
            changedID = changedInstanceIds.Reinterpret<EntityId>();
            destroyedID = destroyedInstanceIds.Reinterpret<EntityId>();
#endif
        }

        public Component[] GetTransformChangesAndClear<T>(TransformTrackingType trackingType, bool sortByInstanceID = false) where T : Object
        {
            return m_ObjectDispatcher.GetTransformChangesAndClear<T>((ObjectDispatcher.TransformTrackingType)trackingType, sortByInstanceID);
        }

        public UnityTransformDispatchData GetTransformChangesAndClear<T>(TransformTrackingType trackingType, Allocator allocator) where T : Object
        {
            return m_ObjectDispatcher.GetTransformChangesAndClear<T>((ObjectDispatcher.TransformTrackingType)trackingType, allocator);
        }

        public void EnableTypeTracking<T>(TypeTrackingFlags typeTrackingMask = TypeTrackingFlags.Default) where T : Object
        {
            m_ObjectDispatcher.EnableTypeTracking<T>((ObjectDispatcher.TypeTrackingFlags)typeTrackingMask);
        }

        public void DisableTypeTracking<T>() where T : Object
        {
            m_ObjectDispatcher.DisableTypeTracking(typeof(T));
        }

        public void EnableTransformTracking<T>(TransformTrackingType trackingType) where T : Object
        {
            m_ObjectDispatcher.EnableTransformTracking((ObjectDispatcher.TransformTrackingType)trackingType, typeof(T));
        }

        public void DisableTransformTracking<T>(TransformTrackingType trackingType) where T : Object
        {
            m_ObjectDispatcher.DisableTransformTracking((ObjectDispatcher.TransformTrackingType)trackingType, typeof(T));
        }
    }
}
