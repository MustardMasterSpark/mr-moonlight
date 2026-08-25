// Copyright © Magnetic Arcade. All Rights Reserved.

using UnityEngine;
using UnityObject = UnityEngine.Object;

namespace MA.Flora
{
    internal static class UnityExtensions
    {
        public static EntityObjectRef<T> As<T>(this EntityId entityId) where T : UnityObject
        {
            return entityId.IsValid() ? new EntityObjectRef<T>(entityId) : EntityObjectRef<T>.None;
        }

        public static T ToObject<T>(this EntityId entityId) where T : UnityObject
        {
#if UNITY_6000_3_OR_NEWER
            return entityId.IsValid() ? (T)Resources.EntityIdToObject(entityId) : null;
#else
            return entityId.IsValid() ? (T)Resources.InstanceIDToObject(entityId) : null;
#endif
        }

        public static UnityObject ToObject(this EntityId entityId)
        {
#if UNITY_6000_3_OR_NEWER
            return entityId.IsValid() ? Resources.EntityIdToObject(entityId) : null;
#else
            return entityId.IsValid() ? Resources.InstanceIDToObject(entityId) : null;
#endif
        }

        public static bool IsValid(this EntityId entityId)
        {
#if UNITY_6000_3_OR_NEWER
            return Resources.EntityIdIsValid(entityId);
#else
            return Resources.InstanceIDIsValid(entityId);
#endif
        }
    }
}
