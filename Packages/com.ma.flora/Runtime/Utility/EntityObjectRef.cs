// Copyright © Magnetic Arcade. All Rights Reserved.

using System;
using System.Diagnostics;
using Unity.Collections;
using UnityEngine;
using UnityObject = UnityEngine.Object;

namespace MA.Flora
{
    [DebuggerDisplay("({Object}, {Value})")]
    internal struct EntityObjectRef<T> : IEquatable<EntityObjectRef<T>>, IComparable<EntityObjectRef<T>>
        where T : UnityObject
    {
        public static EntityObjectRef<T> None => default;

        public EntityId Value;

        public EntityObjectRef(EntityId value)
        {
            Value = value;
        }

        public T Object
        {
            readonly get => this;
            set => this = value;
        }

        public readonly bool IsValid() => Value.IsValid();

        public int CompareTo(EntityObjectRef<T> other) => Value.CompareTo(other.Value);
        public bool Equals(EntityObjectRef<T> other) => Value == other.Value;
        public override bool Equals(object obj) => obj is EntityObjectRef<T> other && Equals(other);
        public override int GetHashCode() => Value.GetHashCode();

        public static implicit operator EntityId(EntityObjectRef<T> obj) => obj.Value;

        public static implicit operator EntityObjectRef<T>(T instance)
        {
            EntityId entityId = instance == null ? EntityId.None : instance.GetEntityId();
            return new EntityObjectRef<T>(entityId);
        }

        public static implicit operator T(EntityObjectRef<T> obj)
        {
            if (obj.Value == EntityId.None) return null;
            return obj.Value.ToObject<T>();
        }

        public static bool operator ==(EntityObjectRef<T> left, EntityObjectRef<T> right) => left.Equals(right);
        public static bool operator !=(EntityObjectRef<T> left, EntityObjectRef<T> right) => !left.Equals(right);

        public override string ToString()
        {
            if (Equals(None))
                return "EntityObjectRef<T>.Null";

            return $"EntityObjectRef<T>({Value})";
        }

        public FixedString64Bytes ToFixedString()
        {
            if (Equals(None))
                return (FixedString64Bytes)"EntityObjectRef<T>.Null";

            var fs = new FixedString64Bytes();
            fs.Append("EntityObjectRef<T>(");
#if UNITY_6000_5_OR_NEWER
            fs.Append(EntityId.ToULong(Value));
#else
            fs.Append((int)Value);
#endif
            fs.Append(')');
            return fs;
        }
    }
}
