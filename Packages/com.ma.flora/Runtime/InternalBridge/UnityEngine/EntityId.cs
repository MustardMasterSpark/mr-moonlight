// Unity C# reference source
// Copyright (c) Unity Technologies. For terms of use, see
// https://unity3d.com/legal/licenses/Unity_Reference_Only_License

#if !UNITY_6000_2_OR_NEWER
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine.Scripting;

namespace UnityEngine
{
    [StructLayout(LayoutKind.Sequential, Size = 4)]
    [Serializable]
    [Obsolete("Obsolete - Please use EntityId instead.")]
    public struct InstanceID : IEquatable<InstanceID>, IComparable<InstanceID>
    {
        [SerializeField]
        int m_Data;

        public static InstanceID None => default;
        public override bool Equals(object obj) => obj is InstanceID other && Equals(other);
        public bool Equals(InstanceID other) => m_Data == other.m_Data;
        public int CompareTo(InstanceID other) => m_Data.CompareTo(other.m_Data);
        public static bool operator ==(InstanceID left, InstanceID right) => left.Equals(right);
        public static bool operator !=(InstanceID left, InstanceID right) => !left.Equals(right);

        public static bool operator <(InstanceID left, InstanceID right)  => left.m_Data < right.m_Data;
        public static bool operator >(InstanceID left, InstanceID right)  => left.m_Data > right.m_Data;
        public static bool operator <=(InstanceID left, InstanceID right) => left.m_Data <= right.m_Data;
        public static bool operator >=(InstanceID left, InstanceID right)  => left.m_Data >= right.m_Data;

        public override int GetHashCode()
        {
            // We only want the lower bits, which is the Index
            uint a = (uint)m_Data;

            // Same Int hash as in the engine
            a = (a + 0x7ed55d16) + (a << 12);
            a = (a ^ 0xc761c23c) ^ (a >> 19);
            a = (a + 0x165667b1) + (a << 5);
            a = (a + 0xd3a2646c) ^ (a << 9);
            a = (a + 0xfd7046c5) + (a << 3);
            a = (a ^ 0xb55a4f09) ^ (a >> 16);

            return (int)a;
        }

        public bool IsValid()
        {
            return this != InstanceID.None;
        }

        public bool Equals(int other) => m_Data == (int)other;

        public static implicit operator int(InstanceID entityId) => entityId.m_Data;
        public static implicit operator InstanceID(int intValue) => new InstanceID {m_Data = intValue};

        public static implicit operator EntityId(InstanceID entityId) => (int)entityId;
        public static implicit operator InstanceID(EntityId entityId) => new InstanceID {m_Data = entityId};

        public override string ToString() => m_Data.ToString();
        public string ToString(string format) => m_Data.ToString(format);
    }

#pragma warning disable 612, 618
    [StructLayout(LayoutKind.Sequential, Size = 4)]
    [UsedByNativeCode]
    [Serializable]
    [NativeClass("EntityId")]
    public struct EntityId : IEquatable<EntityId>, IComparable<EntityId>
    {
        [SerializeField]
        int m_Data;

        public static EntityId None => default;
        public override bool Equals(object obj) => obj is EntityId other && Equals(other);
        public bool Equals(EntityId other) => m_Data == other.m_Data;
        public int CompareTo(EntityId other) => m_Data.CompareTo(other.m_Data);
        public static bool operator ==(EntityId left, EntityId right) => left.Equals(right);
        public static bool operator !=(EntityId left, EntityId right) => !left.Equals(right);

        public static bool operator <(EntityId left, EntityId right)  => left.m_Data < right.m_Data;
        public static bool operator >(EntityId left, EntityId right)  => left.m_Data > right.m_Data;
        public static bool operator <=(EntityId left, EntityId right) => left.m_Data <= right.m_Data;
        public static bool operator >=(EntityId left, EntityId right)  => left.m_Data >= right.m_Data;

        public override int GetHashCode()
        {
            // We only want the lower bits, which is the Index
            uint a = (uint)m_Data;

            // Same Int hash as in the engine
            a = (a + 0x7ed55d16) + (a << 12);
            a = (a ^ 0xc761c23c) ^ (a >> 19);
            a = (a + 0x165667b1) + (a << 5);
            a = (a + 0xd3a2646c) ^ (a << 9);
            a = (a + 0xfd7046c5) + (a << 3);
            a = (a ^ 0xb55a4f09) ^ (a >> 16);

            return (int)a;
        }

        public bool IsValid()
        {
            return this != EntityId.None;
        }

        public bool Equals(int other) => m_Data == (int)other;

        public static implicit operator int(EntityId entityId) => entityId.m_Data;
        public static implicit operator EntityId(int intValue) => new EntityId {m_Data = intValue};

        public static implicit operator EntityId(InstanceID entityId) => new EntityId {m_Data = entityId};
        public static implicit operator InstanceID(EntityId entityId) => (int)entityId;

        public override string ToString() => m_Data.ToString();
        public string ToString(string format) => m_Data.ToString(format);
    }

    static class EntityIdExtensions
    {
        public static int[] ToIntArray(this InstanceID[] instanceIds) => Array.ConvertAll(instanceIds, input => (int)input);
        public static InstanceID[] ToInstanceIDArray(this int[] instanceIdInts) => Array.ConvertAll(instanceIdInts, input => (InstanceID)input);
        public static List<int> ToIntList(this List<InstanceID> instanceIds) => instanceIds.ConvertAll(input => (int)input);
        public static List<InstanceID> ToInstanceIDList(this List<int> instanceIdInts) =>  instanceIdInts.ConvertAll(input => (InstanceID)input);

        public static int[] ToIntArray(this EntityId[] entityIds) => Array.ConvertAll(entityIds, input => (int)input);
        public static EntityId[] ToEntityIdArray(this int[] entityIdInts) => Array.ConvertAll(entityIdInts, input => (EntityId)input);
        public static List<int> ToIntList(this List<EntityId> entityIds) => entityIds.ConvertAll(input => (int)input);
        public static List<EntityId> ToEntityIdList(this List<int> entityIdInts) =>  entityIdInts.ConvertAll(input => (EntityId)input);

        /*
        [StructLayout(LayoutKind.Explicit)]
        struct UnsafeTypeCastInstanceIDArray // .GetType() won't be cast, so `is` and `as` won't work.
        {
            [FieldOffset(0)] public InstanceID[] instance_ids;
            [FieldOffset(0)] public ulong[] ulongs;
            [FieldOffset(0)] public long[] longs;

            public static implicit operator UnsafeTypeCastInstanceIDArray(InstanceID[] ids) => new() {instance_ids = ids};
            public static implicit operator UnsafeTypeCastInstanceIDArray(ulong[] ulongs) => new() {ulongs = ulongs};
            public static implicit operator UnsafeTypeCastInstanceIDArray(long[] longs) => new() {longs = longs};
            public static implicit operator InstanceID[](UnsafeTypeCastInstanceIDArray value) => value.instance_ids;
            public static implicit operator ulong[](UnsafeTypeCastInstanceIDArray value) => value.ulongs;
            public static implicit operator long[](UnsafeTypeCastInstanceIDArray value) => value.longs;
        }
        */
    }

    public static class ObjectEntityIdExtensions
    {
        public static EntityId GetEntityId(this Object instance) => instance == null ? EntityId.None : instance.GetInstanceID();
    }
    #pragma warning restore 612, 618
}
#endif
