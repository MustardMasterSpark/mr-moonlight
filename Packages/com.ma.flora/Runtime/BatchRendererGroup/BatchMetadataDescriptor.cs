// Copyright © Magnetic Arcade. All Rights Reserved.
// ReSharper disable InconsistentNaming

using System;
using System.Diagnostics;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine.Assertions;
using UnityEngine.Rendering;

namespace MA.Flora
{
    [Flags]
    internal enum BatchBuiltinPropertyFlags
    {
        None              = 0,
        LocalToWorld      = 1 << 0,
        PrevLocalToWorld  = 1 << 1,
        RandomID          = 1 << 2,
        VariationColor    = 1 << 3,
        LightmapST        = 1 << 4,
        ShCoefficients    = 1 << 5,
        EntityId          = 1 << 6,
        Required          = LocalToWorld,
    }

    [DebuggerDisplay("Component(Name={NameID}, IsOverriden={IsOverriden}, IsPerInstance={IsPerInstance}, Size={TypeSizeInBytes})")]
    internal struct BatchPropertyInfo : IEquatable<BatchPropertyInfo>, IComparable<BatchPropertyInfo>
    {
        public int NameID;
        public int TypeSizeInBytes;
        public bool IsOverriden;
        public bool IsPerInstance;
        public bool IsCreated => NameID != 0;

        public BatchPropertyInfo(int nameID, int typeSizeInBytes, bool isOverriden, bool isPerInstance)
        {
            Debug.Assert((typeSizeInBytes & 3) == 0, "Stride must be 4-byte aligned for ByteAddressBuffer.Load/Store.");
            NameID = nameID;
            TypeSizeInBytes = typeSizeInBytes;
            IsOverriden = isOverriden;
            IsPerInstance = isPerInstance;
        }

        public bool Equals(BatchPropertyInfo other)
        {
            return NameID == other.NameID &&
                   TypeSizeInBytes == other.TypeSizeInBytes &&
                   IsOverriden == other.IsOverriden &&
                   IsPerInstance == other.IsPerInstance;
        }

        public int CompareTo(BatchPropertyInfo other)
        {
            return NameID.CompareTo(other.NameID);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 23 + NameID;
                hash = hash * 23 + TypeSizeInBytes;
                hash = hash * 23 + IsOverriden.GetHashCode();
                hash = hash * 23 + IsPerInstance.GetHashCode();
                return hash;
            }
        }
    }

    internal struct BatchMetadataDescriptor : IEquatable<BatchMetadataDescriptor>, IDisposable
    {
        public UnsafeParallelHashMap<int, int> ComponentMap;
        public UnsafeList<BatchPropertyInfo> ComponentArray;
        public int BuiltinComponentCount;
        public int HashCode;

        public bool IsCreated => ComponentArray.IsCreated;
        public int ComponentCount => ComponentArray.Length;
        public int AdditionalComponentCount => ComponentArray.Length - BuiltinComponentCount;

        public BatchMetadataDescriptor(BatchBuiltinPropertyFlags flags, AllocatorManager.AllocatorHandle allocator)
            : this(flags, default, allocator)
        {
        }

        public BatchMetadataDescriptor(BatchBuiltinPropertyFlags flags, NativeArray<BatchPropertyInfo> additionalComponents, AllocatorManager.AllocatorHandle allocator)
        {
            Assert.IsTrue((flags & BatchBuiltinPropertyFlags.Required) == BatchBuiltinPropertyFlags.Required, "The required built-in properties must be included in the flags.");

            ComponentMap = new UnsafeParallelHashMap<int, int>(4 + additionalComponents.Length, allocator);
            ComponentArray = new UnsafeList<BatchPropertyInfo>(4 + additionalComponents.Length, allocator);
            BuiltinComponentCount = 0;
            HashCode = 0;

            AddBuiltinComponent<float4>(ShaderPropertyId.unity_BaseColor, isOverriden: false, isPerInstance: false);
            AddBuiltinComponent<float4>(ShaderPropertyId.unity_SpecCube0_HDR, isOverriden: false, isPerInstance: false);

            AddBuiltinComponent<float4x3>(ShaderPropertyId.unity_ObjectToWorld, isOverriden: true, isPerInstance: true);
            AddBuiltinComponent<float4x3>(ShaderPropertyId.unity_WorldToObject, isOverriden: true, isPerInstance: true);
            AddBuiltinComponent<float4x3>(ShaderPropertyId.unity_MatrixPreviousM, isOverriden: (flags & BatchBuiltinPropertyFlags.PrevLocalToWorld) != 0, isPerInstance: true);
            AddBuiltinComponent<float4x3>(ShaderPropertyId.unity_MatrixPreviousMI, isOverriden: (flags & BatchBuiltinPropertyFlags.PrevLocalToWorld) != 0, isPerInstance: true);

            AddBuiltinComponent<float>(ShaderPropertyId.flora_RandomID, isOverriden: (flags & BatchBuiltinPropertyFlags.RandomID) != 0, isPerInstance: true);
            AddBuiltinComponent<float4>(ShaderPropertyId.flora_VariationColor, isOverriden: (flags & BatchBuiltinPropertyFlags.VariationColor) != 0, isPerInstance: true);
            AddBuiltinComponent<float4>(ShaderPropertyId.unity_LightmapST, isOverriden: (flags & BatchBuiltinPropertyFlags.LightmapST) != 0, isPerInstance: true);
            AddBuiltinComponent<SHCoefficients>(ShaderPropertyId.unity_SHCoefficients, isOverriden: (flags & BatchBuiltinPropertyFlags.ShCoefficients) != 0, isPerInstance: true);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            AddBuiltinComponent<uint2>(ShaderPropertyId.unity_EntityId, isOverriden: (flags & BatchBuiltinPropertyFlags.EntityId) != 0, isPerInstance: true);
#endif

            if (additionalComponents is { IsCreated: true, Length: > 0 })
            {
                foreach (var component in additionalComponents)
                    AddComponent(component);
            }

            ComponentArray.Sort();

            var hash = new xxHash3.StreamingState(false);
            hash.Update(BuiltinComponentCount);
            hash.Update(ComponentArray.Length);
            for (int i = 0; i < ComponentArray.Length; ++i)
            {
                hash.Update(ComponentArray[i].NameID);
                hash.Update(ComponentArray[i].TypeSizeInBytes);
                hash.Update(ComponentArray[i].IsOverriden);
                hash.Update(ComponentArray[i].IsPerInstance);
            }
            HashCode = hash.DigestHash128().GetHashCode();
        }

        public BatchMetadataDescriptor(in BatchMetadataDescriptor other, Allocator allocator)
        {
            ComponentArray = new UnsafeList<BatchPropertyInfo>(other.ComponentArray.Length, allocator);
            ComponentArray.CopyFrom(other.ComponentArray);
            ComponentArray.Sort();

            ComponentMap = new UnsafeParallelHashMap<int, int>(other.ComponentMap.Capacity, allocator);
            foreach (var pair in other.ComponentMap)
                ComponentMap[pair.Key] = pair.Value;

            BuiltinComponentCount = other.BuiltinComponentCount;
            HashCode = other.HashCode;
        }

        public void Dispose()
        {
            ComponentArray.Dispose();
            ComponentMap.Dispose();
        }

        private void AddComponent(BatchPropertyInfo info)
        {
            ComponentMap[info.NameID] = ComponentArray.Length;
            ComponentArray.Add(info);
        }

        private void AddComponent(int nameID, int sizeInBytes, bool isOverriden, bool isPerInstance)
        {
            AddComponent(new BatchPropertyInfo(nameID, sizeInBytes, isOverriden, isPerInstance));
        }

        private void AddComponent<T>(int nameID, bool isOverriden, bool isPerInstance)
            where T : unmanaged
        {
            AddComponent(nameID, UnsafeUtility.SizeOf<T>(), isOverriden, isPerInstance);
        }

        private void AddBuiltinComponent<T>(int nameID, bool isOverriden, bool isPerInstance)
            where T : unmanaged
        {
            AddComponent(nameID, UnsafeUtility.SizeOf<T>(), isOverriden, isPerInstance);
            BuiltinComponentCount++;
        }

        public int IndexOf(int nameID)
        {
            if (ComponentMap.TryGetValue(nameID, out int index))
                return index;

            return -1;
        }

        public bool TryGetPropertyInfo(int nameID, out BatchPropertyInfo info)
        {
            info = default;
            if (ComponentMap.TryGetValue(nameID, out int index))
            {
                info = ComponentArray[index];
                return true;
            }

            return false;
        }

        public bool TryGetPropertyIndex(int nameID, out int index)
        {
            return ComponentMap.TryGetValue(nameID, out index);
        }

        public bool Equals(BatchMetadataDescriptor other)
        {
            if (BuiltinComponentCount != other.BuiltinComponentCount)
                return false;
            if (ComponentArray.Length != other.ComponentArray.Length)
                return false;

            for (int i = 0; i < ComponentArray.Length; i++)
            {
                if (!ComponentArray[i].Equals(other.ComponentArray[i]))
                    return false;
            }

            return true;
        }

        public override int GetHashCode() => HashCode;
    }
}
