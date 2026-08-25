// Copyright © Magnetic Arcade. All Rights Reserved.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.Burst.CompilerServices;

namespace MA.Flora
{
    [StructLayout(LayoutKind.Sequential)]
    internal unsafe struct ChunkIndex : IEquatable<ChunkIndex>, IComparable<ChunkIndex>
    {
        public static ChunkIndex None => new();

        public const int Capacity = InstanceManager.ChunkCapacity;

        public int Index;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int AsInstanceOffset() => Index << InstanceManager.ChunkShift;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ChunkIndex(int index) => Index = index;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int CompareTo(ChunkIndex other) => Index.CompareTo(other.Index);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(ChunkIndex other) => Index == other.Index;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override bool Equals(object obj) => obj is ChunkIndex other && Equals(other);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override int GetHashCode() => Index;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override string ToString() => Equals(None) ? "ChunkIndex.None" : $"ChunkIndex({Index}) in ({Archetype})";

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator int(ChunkIndex chunkIndex) => chunkIndex.Index;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator ==(ChunkIndex a, ChunkIndex b) => a.Index == b.Index;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator !=(ChunkIndex a, ChunkIndex b) => !(a == b);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator <(ChunkIndex a, ChunkIndex b) => a.Index < b.Index;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator >(ChunkIndex a, ChunkIndex b) => a.Index > b.Index;

        internal bool IsEnabled
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => (Archetype.Key.Tags & InstanceTag.Enabled) != 0;
        }

        internal bool IsFull
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Count == Capacity;
        }

        internal ArchetypeIndex Archetype
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => InstanceManager.ChunkStore.Data[Index].Type;
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            readonly set => InstanceManager.ChunkStore.Data[Index].Type = value;
        }

        internal ref readonly AABB LocalAABB
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref Archetype.LocalAABB;
        }

        internal int Count
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => GetInstanceCount(this);
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            readonly set => InstanceManager.ChunkStore.Data[Index].InstanceCount = value;
        }

        [return: AssumeRange(0, InstanceManager.ChunkCapacity)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int GetInstanceCount(ChunkIndex chunk)
        {
            return InstanceManager.ChunkStore.Data[chunk.Index].InstanceCount;
        }

        internal int SpaceRemaining
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Capacity - Count;
        }

        internal ulong AllocatedMask
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Count == 64 ? ulong.MaxValue : (1ul << Count) - 1;
        }

        internal int IndexInArchetype
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => InstanceManager.ChunkStore.Data[Index].IndexInArchetype;
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            readonly set => InstanceManager.ChunkStore.Data[Index].IndexInArchetype = value;
        }

        internal int IndexInArchetypeFreeSlotList
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => InstanceManager.ChunkStore.Data[Index].IndexInArchetypeFreeSlotList;
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            readonly set => InstanceManager.ChunkStore.Data[Index].IndexInArchetypeFreeSlotList = value;
        }

        internal int IndexInTemplateList
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => InstanceManager.ChunkStore.Data[Index].IndexInTemplateList;
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            readonly set => InstanceManager.ChunkStore.Data[Index].IndexInTemplateList = value;
        }

        internal BatchAllocation BatchAllocation
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => InstanceManager.ChunkStore.Data[Index].BatchAllocation;
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            readonly set => InstanceManager.ChunkStore.Data[Index].BatchAllocation = value;
        }
    }
}
