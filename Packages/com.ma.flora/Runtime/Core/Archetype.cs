// Copyright © Magnetic Arcade. All Rights Reserved.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace MA.Flora
{
    internal struct ArchetypeKey : IEquatable<ArchetypeKey>
    {
        public static ArchetypeKey None => default;

        public InstanceTag Tags;
        public Scene Scene;
        public byte Layer;
        public ushort MaxRenderDistance;
        public int LightmapIndex;
        public TemplateIndex Template;
        public EntityId ContainerEntity;
#if UNITY_EDITOR
        public ulong SceneCullingMask;
#endif

        public bool IsEnabled => (Tags & InstanceTag.Enabled) != 0;
        public bool IsContainerOwned => (Tags & InstanceTag.ContainerOwned) != 0;

        public bool Equals(ArchetypeKey other)
        {
            return Tags == other.Tags &&
                   Scene == other.Scene &&
                   Layer == other.Layer &&
                   MaxRenderDistance == other.MaxRenderDistance &&
                   LightmapIndex == other.LightmapIndex &&
                   Template == other.Template &&
                   ContainerEntity == other.ContainerEntity
#if UNITY_EDITOR
                   && SceneCullingMask == other.SceneCullingMask
#endif
                ;
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = 17;
                hashCode = (hashCode * 397) ^ ((uint)Tags).GetHashCode();
                hashCode = (hashCode * 397) ^ Scene.GetHashCode();
                hashCode = (hashCode * 397) ^ Layer;
                hashCode = (hashCode * 397) ^ MaxRenderDistance.GetHashCode();
                hashCode = (hashCode * 397) ^ LightmapIndex;
                hashCode = (hashCode * 397) ^ Template.GetHashCode();
                hashCode = (hashCode * 397) ^ ContainerEntity.GetHashCode();
#if UNITY_EDITOR
                hashCode = (hashCode * 397) ^ SceneCullingMask.GetHashCode();
#endif
                return hashCode;
            }
        }
    }

    internal unsafe struct ArchetypeIndex : IEquatable<ArchetypeIndex>, IComparable<ArchetypeIndex>
    {
        public static ArchetypeIndex None => default;

        public int Index;

        public ArchetypeIndex(int index) => Index = index;

        public int CompareTo(ArchetypeIndex other) => Index.CompareTo(other.Index);
        public bool Equals(ArchetypeIndex other) => Index == other.Index;
        public override bool Equals(object obj) => obj is ArchetypeIndex other && Equals(other);
        public override int GetHashCode() => Index;
        public override string ToString() => Equals(None) ? "ArchetypeIndex.None" : $"ArchetypeIndex({Index})";

        public static implicit operator int(ArchetypeIndex index) => index.Index;
        public static bool operator ==(ArchetypeIndex a, ArchetypeIndex b) => a.Index == b.Index;
        public static bool operator !=(ArchetypeIndex a, ArchetypeIndex b) => !(a == b);
        public static bool operator <(ArchetypeIndex a, ArchetypeIndex b) => a.Index < b.Index;
        public static bool operator >(ArchetypeIndex a, ArchetypeIndex b) => a.Index > b.Index;

        public ref readonly ArchetypeKey Key
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref InstanceManager.ArchetypeStore.Data[Index].Key;
        }

        public ref readonly AABB LocalAABB
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref Key.Template.LocalAABB;
        }

        public int Version
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => InstanceManager.ArchetypeStore.Data[Index].Version;
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            readonly set => InstanceManager.ArchetypeStore.Data[Index].Version = value;
        }

        public int ChunkCount
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => InstanceManager.ArchetypeStore.Data[Index].ChunkCount;
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            readonly set => InstanceManager.ArchetypeStore.Data[Index].ChunkCount = value;
        }

        public int InstanceCount
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => InstanceManager.ArchetypeStore.Data[Index].InstanceCount;
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            readonly set => InstanceManager.ArchetypeStore.Data[Index].InstanceCount = value;
        }

        public bool Enabled
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => (Key.Tags & InstanceTag.Enabled) != 0;
        }

        public bool HasRandomID
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => (Key.Template.Flags & TemplateRenderFlags.HasRandomID) != 0;
        }

        public bool HasVariationColor
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => (Key.Template.Flags & TemplateRenderFlags.HasVariationColor) != 0;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    [GenerateHLSL(PackingRules.Exact, false)]
    internal struct PackedArchetypeData
    {
        public uint templateIndex;
        public uint batchDomainIndex;
        public uint batchInstanceOffset;
        public uint packedLayerAndMaxRenderDistance;

        public PackedArchetypeData(ArchetypeIndex archetype)
        {
            ArchetypeKey archetypeKey = archetype.Key;
            templateIndex = (uint)archetypeKey.Template.Index;
            batchDomainIndex = (uint)archetypeKey.Template.BatchDomainIndex.Index;
            batchInstanceOffset = 0;
            packedLayerAndMaxRenderDistance = archetypeKey.MaxRenderDistance | ((uint)archetypeKey.Layer << 16);
        }
    }
}
