// Copyright © Magnetic Arcade. All Rights Reserved.

using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine.Assertions;

namespace MA.Flora
{
    partial struct FloraInstanceHandle
    {
#if ENABLE_FLORA_DEBUG_NAMES
        internal string DebugName => FloraInstanceArray.Data.GetInstanceName(this).ToString();
#endif
    }
}

namespace MA.Flora
{
    internal struct FloraInstanceName
    {
        internal int Index;

        public void ToFixedString(ref FixedString64Bytes value)
        {
            FloraNameStorage.GetFixedString(Index, ref value);
        }

        [ExcludeFromBurstCompatTesting("Returns managed string")]
        public override string ToString()
        {
            FixedString64Bytes temp = default;
            ToFixedString(ref temp);
            return temp.ToString();
        }

        public void SetFixedString(in FixedString64Bytes value)
        {
            int tryIndex = FloraNameStorage.GetOrCreateIndex(in value);

            if (tryIndex >= 0)
                Index = tryIndex;
            else if (FloraNameStorage.SharedState.Data.HasLoggedError == 0)
            {
                UnityEngine.Debug.LogError(FloraNameStorage.SharedState.Data.KMaxEntriesMsg);
                FloraNameStorage.SharedState.Data.HasLoggedError++;
            }
        }
    }

    internal unsafe struct FloraNameStorage
    {
        internal struct Entry
        {
            public int Offset;
            public int Length;
        }

        internal struct State
        {
            public byte Initialized;
            internal byte HasLoggedError;
            public UnsafeList<byte> Buffer; // all the UTF-8 encoded bytes in one place
            public UnsafeList<Entry> Entry; // one offset for each text in "buffer"
            public UnsafeParallelMultiHashMap<int, int> Hash; // from string hash to table entry
            public int Chars; // bytes in buffer allocated so far
            public int Entries; // number of strings allocated so far
            public FixedString512Bytes KMaxEntriesMsg;
        }

        internal static readonly SharedStatic<State> SharedState = SharedStatic<State>.GetOrCreate<FloraNameStorage>();

        internal const int MaxEntries = 16 << 10;
        internal const int MaxChars = MaxEntries * 64;
        internal const int ErrorExceedMaxEntryCapacity = -1;
        internal const int InstanceNameMaxLengthBytes = 61;

        public static int Entries => SharedState.Data.Entries;

        public static void Initialize()
        {
            if (SharedState.Data.Initialized != 0)
                return;

            SharedState.Data.Buffer = new UnsafeList<byte>(MaxChars, Allocator.Persistent);
            SharedState.Data.Buffer.Length = SharedState.Data.Buffer.Capacity;
            SharedState.Data.Entry = new UnsafeList<Entry>(MaxEntries, Allocator.Persistent);
            SharedState.Data.Entry.Length = SharedState.Data.Entry.Capacity;
            SharedState.Data.Hash = new UnsafeParallelMultiHashMap<int, int>(MaxEntries, Allocator.Persistent);
            Clear();
            SharedState.Data.Initialized = 1;
            SharedState.Data.HasLoggedError = 0;
            SharedState.Data.KMaxEntriesMsg = "Max unique Instance Name capacity exceeded. If you require more storage, edit InstanceNameStorage.cs and change the value of kMaxEntries to pre-allocate more space.";
        }

        public static void Shutdown()
        {
            if (SharedState.Data.Initialized == 0)
                return;

            SharedState.Data.Buffer.Dispose();
            SharedState.Data.Entry.Dispose();
            SharedState.Data.Hash.Dispose();
            SharedState.Data.Initialized = 0;
            SharedState.Data.HasLoggedError = 0;
        }

        public static void Clear()
        {
            SharedState.Data.Chars = 0;
            SharedState.Data.Entries = 0;
            SharedState.Data.Hash.Clear();
            var temp = new FixedString64Bytes();
            GetOrCreateIndex(in temp); // make sure that Index=0 means empty string
        }

        public static void GetFixedString(int index, ref FixedString64Bytes temp)
        {
            Assert.IsTrue(index < SharedState.Data.Entries);
            var e = SharedState.Data.Entry[index];
            Assert.IsTrue(e.Length <= InstanceNameMaxLengthBytes);
            temp.Length = math.min(e.Length, temp.Capacity);
            UnsafeUtility.MemCpy(temp.GetUnsafePtr(), SharedState.Data.Buffer.Ptr + e.Offset, temp.Length);
        }

        public static int GetIndexFromHashAndFixedString(int hash, in FixedString64Bytes fixedString)
        {
            Assert.IsTrue(fixedString.Length <= InstanceNameMaxLengthBytes);
            Assert.AreEqual(hash, fixedString.GetHashCode()); // The inputted hash must be the hash of the FixedString.

            if (SharedState.Data.Hash.TryGetFirstValue(hash, out int itemIndex, out var iter))
            {
                do
                {
                    var e = SharedState.Data.Entry[itemIndex];
                    Assert.IsTrue(e.Length <= InstanceNameMaxLengthBytes);
                    if (e.Length == fixedString.Length)
                    {
                        int matches;
                        for (matches = 0; matches < e.Length; ++matches)
                            if (fixedString[matches] != SharedState.Data.Buffer[e.Offset + matches])
                                break;
                        if (matches == fixedString.Length)
                            return itemIndex;
                    }
                } while (SharedState.Data.Hash.TryGetNextValue(out itemIndex, ref iter));
            }

            return -1;
        }

        public static bool Contains(in FixedString64Bytes value)
        {
            int h = value.GetHashCode();
            return GetIndexFromHashAndFixedString(h, in value) != -1;
        }

        [ExcludeFromBurstCompatTesting("Takes managed string")]
        public static bool Contains(string value)
        {
            FixedString64Bytes temp = value;
            return Contains(in temp);
        }

        public static int GetOrCreateIndex(in FixedString64Bytes value)
        {
            int h = value.GetHashCode();
            var itemIndex = GetIndexFromHashAndFixedString(h, in value);

            if (itemIndex != ErrorExceedMaxEntryCapacity)
                return itemIndex;
            if (SharedState.Data.Entries >= MaxEntries)
                return ErrorExceedMaxEntryCapacity;

            Assert.IsTrue(SharedState.Data.Chars + value.Length <= MaxChars);
            var o = SharedState.Data.Chars;
            var l = (ushort)value.Length;
            for (var i = 0; i < l; ++i)
                SharedState.Data.Buffer[SharedState.Data.Chars++] = value[i];

            SharedState.Data.Entry[SharedState.Data.Entries] = new Entry { Offset = o, Length = l };
            SharedState.Data.Hash.Add(h, SharedState.Data.Entries);

            return SharedState.Data.Entries++;
        }
    }

    internal partial struct InstanceManager
    {
        public string GetName(FloraInstanceHandle entity)
        {
#if ENABLE_FLORA_DEBUG_NAMES
            if (!Exists(entity))
                return "INSTANCE_NOT_FOUND";

            return GetInstanceName(entity).ToString();
#else
            return "";
#endif
        }

        public void SetName(FloraInstanceHandle instance, in FixedString64Bytes name)
        {
#if ENABLE_FLORA_DEBUG_NAMES
            if (!Exists(instance))
                return;

            var instanceName = new InstanceName();
            instanceName.SetFixedString(in name);
            SetInstanceName(instance, instanceName);
            // m_NameStoreAccess.SetEntityName(entity, entityName);
            // m_NameStoreAccess.AddEntityWithNameSet(entity);
#endif
        }

#if ENABLE_FLORA_DEBUG_NAMES
        public InstanceName GetInstanceNameByIndex(int index)
        {
            return FloraInstanceArray.Data.GetInstanceNameByIndex(index);
        }

        public InstanceName GetInstanceName(FloraInstance instance)
        {
            return FloraInstanceArray.Data.GetInstanceName(instance);
        }

        public void SetInstanceName(FloraInstance instance, InstanceName name)
        {
            FloraInstanceArray.Data.SetInstanceName(instance, name);
        }

        public void SetInstanceName(FloraInstance* instances, int count, InstanceName name)
        {
            for (int i = 0; i < count; i++)
                FloraInstanceArray.Data.SetInstanceName(instances[i], name);
        }
#endif
    }
}
