// Copyright © Magnetic Arcade. All Rights Reserved.
// ReSharper disable InconsistentNaming

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace MA.Flora
{
    [StructLayout(LayoutKind.Sequential)]
    [GenerateHLSL(PackingRules.Exact, false)]
    internal struct BatchCullingAddresses
    {
        public uint localToWorld;
        public uint randomID;
        public uint unused0;
        public uint unused1;
    }

    [GenerateHLSL(PackingRules.Exact, false)]
    [StructLayout(LayoutKind.Sequential)]
    internal struct BatchTransformAddresses
    {
        public uint localToWorld;
        public uint worldToLocal;
        public uint prevLocalToWorld;
        public uint prevWorldToLocal;
    }

    internal struct BatchAllocation : IEquatable<BatchAllocation>
    {
        public static BatchAllocation None => new BatchAllocation { Domain = BatchDomainIndex.None };

        public BatchDomainIndex Domain;
        public int Offset;
        public int Length;

        public BatchAllocation(BatchDomainIndex domain, int offset, int count)
        {
            Domain = domain;
            Offset = offset;
            Length = count;
        }

        public bool IsValid() => Domain.IsCreated && Length > 0;
        public bool Equals(BatchAllocation other) => Domain.Equals(other.Domain) && Offset == other.Offset && Length == other.Length;
        public override bool Equals(object obj) => obj is BatchAllocation other && Equals(other);
        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = Domain.GetHashCode();
                hashCode = (hashCode * 397) ^ Offset;
                hashCode = (hashCode * 397) ^ Length;
                return hashCode;
            }
        }

        public static bool operator ==(BatchAllocation a, BatchAllocation b) => a.Equals(b);
        public static bool operator !=(BatchAllocation a, BatchAllocation b) => !a.Equals(b);
    }

    internal struct BatchDomainIndex : IEquatable<BatchDomainIndex>, IComparable<BatchDomainIndex>
    {
        public static BatchDomainIndex None => default;

        public int Index;

        public bool IsCreated
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Index > 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int CompareTo(BatchDomainIndex other) => Index - other.Index;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(BatchDomainIndex other) => Index == other.Index;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override bool Equals(object obj) => obj is BatchDomainIndex other && Equals(other);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override int GetHashCode() => Index;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override string ToString() => Equals(None) ? "BatchDomainID.None" : $"BatchDomainID({Index})";

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator int(BatchDomainIndex domainIndex) => domainIndex.Index;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator ==(BatchDomainIndex a, BatchDomainIndex b) => a.Index == b.Index;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator !=(BatchDomainIndex a, BatchDomainIndex b) => a.Index != b.Index;
    }

    internal struct BatchDomainLayout
    {
        public int InstanceCapacity;
        public long BaseAddress;
        public long LengthInBytes;
        public NativeArray<BatchPropertyInfo> Properties;
        public NativeArray<MetadataValue> MetadataValues;
        public UnsafeParallelHashMap<int, int> NameToIndexMap;
        public bool IsCreated => MetadataValues.IsCreated;

        public BatchDomainLayout(BatchMetadataDescriptor descriptor, long baseAddress, int instanceCapacity, bool lightProbesEnabled)
        {
            InstanceCapacity = instanceCapacity;
            BaseAddress = baseAddress;
            LengthInBytes = 0;
            Properties = new NativeArray<BatchPropertyInfo>(descriptor.ComponentCount, Allocator.Persistent);
            MetadataValues = new NativeArray<MetadataValue>(descriptor.ComponentCount, Allocator.Persistent);
            NameToIndexMap = new UnsafeParallelHashMap<int, int>(descriptor.ComponentCount, Allocator.Persistent);

            long propertyAddress = baseAddress;

            for (int index = 0; index < descriptor.ComponentCount; index++)
            {
                BatchPropertyInfo component = descriptor.ComponentArray[index];
                if (!lightProbesEnabled && component.NameID == ShaderPropertyId.unity_SHCoefficients)
                {
                    component.IsOverriden = false;
                    component.IsPerInstance = false;
                }

                MetadataValue metadata = new MetadataValue { NameID = component.NameID };
                if (component.IsOverriden)
                {
                    metadata.Value = (uint)propertyAddress | InstanceBuffer.OverridenBit;

                    int count = component.IsPerInstance ? InstanceCapacity : 1;
                    propertyAddress += count * component.TypeSizeInBytes;
                }

                MetadataValues[index] = metadata;
                Properties[index] = component;
                NameToIndexMap[component.NameID] = index;

                propertyAddress = MathUtility.NextMultipleOf(propertyAddress, 16);
            }

            LengthInBytes = propertyAddress - baseAddress;
        }

        public void Dispose()
        {
            Properties.Dispose();
            MetadataValues.Dispose();
            NameToIndexMap.Dispose();
        }

        public uint GetMetadata(int nameID)
        {
            if (!NameToIndexMap.TryGetValue(nameID, out int metadataIndex))
                return 0;

            return MetadataValues[metadataIndex].Value;
        }

        public uint GetPerInstanceMetadata(int nameID)
        {
            if (!NameToIndexMap.TryGetValue(nameID, out int metadataIndex))
                return 0;

            if (!Properties[metadataIndex].IsPerInstance ||
                !Properties[metadataIndex].IsOverriden)
                return 0;

            return MetadataValues[metadataIndex].Value;
        }

        public uint GetPerInstanceAddress(int nameID)
        {
            return GetPerInstanceMetadata(nameID) & InstanceBuffer.AddressMask;
        }

        public uint GetAddress(int nameID)
        {
            if (!NameToIndexMap.TryGetValue(nameID, out int metadataIndex))
                return 0;

            return MetadataValues[metadataIndex].Value & InstanceBuffer.AddressMask;
        }

        public bool IsOverriden(int nameID)
        {
            if (!NameToIndexMap.TryGetValue(nameID, out int metadataIndex))
                return false;

            return (MetadataValues[metadataIndex].Value & InstanceBuffer.OverridenBit) != 0;
        }

        public bool IsPerInstance(int nameID)
        {
            if (!NameToIndexMap.TryGetValue(nameID, out int metadataIndex))
                return false;

            return Properties[metadataIndex].IsPerInstance;
        }
    }

    internal static class MetadataValueUtility
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsOverriden(this MetadataValue metadataValue)
        {
            return (metadataValue.Value & InstanceBuffer.OverridenBit) != 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint Address(this MetadataValue metadataValue)
        {
            return metadataValue.Value & InstanceBuffer.AddressMask;
        }
    }

    internal partial struct InstanceBuffer : IDisposable
    {
        public const uint OverridenBit = 0x80000000;
        public const uint AddressMask  = 0x7fffffff;

        private NativeDataReference<InstanceManager> m_InstanceManager;

        private bool m_SceneHasLightProbes;
        private bool m_LayoutDirty;

        private int m_NextTypeId;
        private UnsafeParallelHashMap<BatchMetadataDescriptor, BatchDomainIndex> m_DomainHash;
        private UnsafeBitSet m_AllocatedDomains;
        private UnsafeList<BatchDomainIndex> m_FreeDomainsIDs;

        private NativeArray<BatchID> m_DomainBatches;
        private NativeArray<BatchMetadataDescriptor> m_DomainDescriptors;
        private NativeArray<NativeRegionAllocator> m_DomainAllocators;
        private NativeArray<BatchDomainLayout> m_DomainLayouts;
        private PersistentGraphicsBufferRef<uint> m_DomainRandomIdAddresses;
        private PersistentGraphicsBufferRef<uint> m_DomainVariationColorAddresses;
        private PersistentGraphicsBufferRef<uint> m_DomainLightmapSTAddresses;
        private PersistentGraphicsBufferRef<uint> m_DomainSHCoefficientsAddresses;
        private PersistentGraphicsBufferRef<uint> m_DomainEntityIdAddresses;
        private PersistentGraphicsBufferRef<BatchCullingAddresses> m_DomainCullingAddresses;
        private PersistentGraphicsBufferRef<BatchTransformAddresses> m_DomainTransformAddresses;
        private int m_DomainCount;

        private GraphicsBufferRef m_DataBuffer;

        private uint m_LayoutVersion;
        private uint m_ContentVersionScheduled;
        private uint m_ContentVersionApplied;

        public readonly uint LayoutVersion => m_LayoutVersion;
        public readonly uint ContentVersionScheduled => m_ContentVersionScheduled;
        public readonly uint ContentVersionApplied => m_ContentVersionApplied;
        public readonly int DomainCount => m_DomainCount;
        public readonly NativeArray<BatchID> DomainBatches => m_DomainBatches;
        public readonly NativeArray<BatchMetadataDescriptor> DomainDescriptors => m_DomainDescriptors;
        public readonly PersistentGraphicsBufferRef<uint> DomainRandomIdAddresses => m_DomainRandomIdAddresses;
        public readonly PersistentGraphicsBufferRef<uint> DomainVariationColorAddresses => m_DomainVariationColorAddresses;
        public readonly PersistentGraphicsBufferRef<uint> DomainLightmapSTAddresses => m_DomainLightmapSTAddresses;
        public readonly PersistentGraphicsBufferRef<uint> DomainSHCoefficientsAddresses => m_DomainSHCoefficientsAddresses;
        public readonly PersistentGraphicsBufferRef<uint> DomainEntityIdAddresses => m_DomainEntityIdAddresses;
        public readonly PersistentGraphicsBufferRef<BatchCullingAddresses> DomainCullingAddresses => m_DomainCullingAddresses;
        public readonly PersistentGraphicsBufferRef<BatchTransformAddresses> DomainTransformAddresses => m_DomainTransformAddresses;
        public readonly GraphicsBuffer DataBuffer => m_DataBuffer;
        public readonly long AllocatedSizeInBytes => m_DataBuffer.SizeInBytes;

        private const int InitialBatchCapacity = 8;
        private const long InitialBufferSize          = 256;
        private const long MaxAddressableBufferSize32 = 0x7FFFFFFF; // 2GB - 1 byte

        public void Initialize(InstanceContext instanceContext, FloraRuntimeResources runtimeResources)
        {
            InstanceBufferUpload.Initialize(runtimeResources);

            m_InstanceManager = instanceContext.InstanceManager;
            m_SceneHasLightProbes = CullingUtility.SceneHasLightProbes();
            m_NextTypeId = 1;
            m_DomainHash = new UnsafeParallelHashMap<BatchMetadataDescriptor, BatchDomainIndex>(InitialBatchCapacity, Allocator.Persistent);
            m_AllocatedDomains = new UnsafeBitSet(InitialBatchCapacity, Allocator.Persistent);
            m_FreeDomainsIDs = new UnsafeList<BatchDomainIndex>(InitialBatchCapacity, Allocator.Persistent);
            m_DomainBatches = new NativeArray<BatchID>(InitialBatchCapacity, Allocator.Persistent);
            m_DomainDescriptors = new NativeArray<BatchMetadataDescriptor>(InitialBatchCapacity, Allocator.Persistent);
            m_DomainAllocators = new NativeArray<NativeRegionAllocator>(InitialBatchCapacity, Allocator.Persistent);
            m_DomainLayouts = new NativeArray<BatchDomainLayout>(InitialBatchCapacity, Allocator.Persistent);
            m_DomainRandomIdAddresses = new PersistentGraphicsBufferRef<uint>(InitialBatchCapacity, "Flora.DomainRandomIDAddresses");
            m_DomainVariationColorAddresses = new PersistentGraphicsBufferRef<uint>(InitialBatchCapacity, "Flora.DomainVariationColorAddresses");
            m_DomainLightmapSTAddresses = new PersistentGraphicsBufferRef<uint>(InitialBatchCapacity, "Flora.DomainLightmapSTAddresses");
            m_DomainSHCoefficientsAddresses = new PersistentGraphicsBufferRef<uint>(InitialBatchCapacity, "Flora.DomainSHCoefficientsAddresses");
            m_DomainEntityIdAddresses = new PersistentGraphicsBufferRef<uint>(InitialBatchCapacity, "Flora.DomainEntityIDAddresses");
            m_DomainCullingAddresses = new PersistentGraphicsBufferRef<BatchCullingAddresses>(InitialBatchCapacity, "Flora.DomainCullingAddresses");
            m_DomainTransformAddresses = new PersistentGraphicsBufferRef<BatchTransformAddresses>(InitialBatchCapacity, "Flora.DomainTransformAddresses");
            m_DataBuffer = new GraphicsBufferRef(GraphicsBuffer.Target.Raw, (int)(InitialBufferSize / 4), 4, "Flora.InstanceData");
            m_LayoutVersion = 1;
        }

        public void Dispose()
        {
            foreach (int batchIndex in m_AllocatedDomains)
            {
                m_DomainLayouts[batchIndex].Dispose();
                m_DomainAllocators[batchIndex].Dispose();
                m_DomainDescriptors[batchIndex].Dispose();
            }

            m_DomainHash.Dispose();
            m_AllocatedDomains.Dispose();
            m_FreeDomainsIDs.Dispose();
            m_DomainBatches.Dispose();
            m_DomainDescriptors.Dispose();
            m_DomainAllocators.Dispose();
            m_DomainLayouts.Dispose();
            m_DomainRandomIdAddresses.Dispose();
            m_DomainVariationColorAddresses.Dispose();
            m_DomainLightmapSTAddresses.Dispose();
            m_DomainSHCoefficientsAddresses.Dispose();
            m_DomainEntityIdAddresses.Dispose();
            m_DomainCullingAddresses.Dispose();
            m_DomainTransformAddresses.Dispose();
            m_DataBuffer.Dispose();
        }

        public readonly bool Exists(BatchDomainIndex domainIndex)
        {
            if (domainIndex == BatchDomainIndex.None)
                return false;

            return m_AllocatedDomains[domainIndex];
        }

        public readonly BatchID GetBatchID(BatchDomainIndex domainIndex)
        {
            return m_DomainBatches[domainIndex];
        }

        public readonly int InstanceAddressOf(BatchDomainIndex domainIndex, int nameID)
        {
            if (Exists(domainIndex))
            {
                BatchDomainLayout layout = m_DomainLayouts[domainIndex];
                if (layout.IsCreated)
                    return (int)layout.GetAddress(nameID);
            }

            return 0;
        }

        public BatchDomainIndex GetOrCreateDomain(BatchBuiltinPropertyFlags flags)
        {
            using BatchMetadataDescriptor metadata = new BatchMetadataDescriptor(flags, Allocator.Temp);
            return GetOrCreateDomain(in metadata);
        }

        public BatchDomainIndex GetOrCreateDomain(in BatchMetadataDescriptor metadata)
        {
            if (m_DomainHash.TryGetValue(metadata, out BatchDomainIndex domainIndex))
                return domainIndex;

            domainIndex = m_FreeDomainsIDs.Length > 0 ? m_FreeDomainsIDs.Pop() : new BatchDomainIndex { Index = m_NextTypeId++ };
            if (domainIndex >= m_DomainBatches.Length)
            {
                int newCapacity = math.max(4, domainIndex * 2);
                m_DomainBatches.ResizeArray(newCapacity);
                m_DomainDescriptors.ResizeArray(newCapacity);
                m_DomainAllocators.ResizeArray(newCapacity);
                m_DomainLayouts.ResizeArray(newCapacity);
            }

            // Create a copy of the descriptor to store in the hash map
            BatchMetadataDescriptor metadataCopy = new BatchMetadataDescriptor(metadata, Allocator.Persistent);

            m_AllocatedDomains.Add(domainIndex);
            m_DomainHash.Add(metadataCopy, domainIndex);
            m_DomainDescriptors[domainIndex] = metadataCopy;
            m_DomainBatches[domainIndex] = BatchID.Null;
            m_DomainAllocators[domainIndex] = new NativeRegionAllocator(256, Allocator.Persistent);
            m_DomainLayouts[domainIndex] = default;
            m_LayoutDirty = true;
            m_DomainCount++;

            return domainIndex;
        }

        public BatchAllocation Allocate(BatchDomainIndex domainIndex, int instanceCount)
        {
            if (domainIndex == BatchDomainIndex.None || instanceCount <= 0)
                return BatchAllocation.None;

            NativeRegionAllocator instanceAllocator = m_DomainAllocators[domainIndex];
            int instanceOffset = instanceAllocator.Allocate(instanceCount);
            m_LayoutDirty = true;

            return new BatchAllocation(domainIndex, instanceOffset, instanceCount);
        }

        public void Free(BatchAllocation batchAllocation)
        {
            if (batchAllocation.IsValid())
            {
                NativeRegionAllocator instanceAllocator = m_DomainAllocators[batchAllocation.Domain];
                instanceAllocator.Free(batchAllocation.Offset, batchAllocation.Length);
                m_LayoutDirty = true;
            }
        }

        public bool UpdateLayout(BatchRendererGroup batchRendererGroup, bool forceUpdate = false)
        {
            bool sceneHasLightProbes = CullingUtility.SceneHasLightProbes();
            bool sceneProbesStatusChanged = sceneHasLightProbes != m_SceneHasLightProbes;
            if (!forceUpdate && (!m_LayoutDirty && !sceneProbesStatusChanged))
                return false;

            m_SceneHasLightProbes = sceneHasLightProbes;

            int nullByteSize = UnsafeUtility.SizeOf<float4x4>(); // Reserve space for one "null" matrix at the start of the buffer
            long newBufferSizeInBytes = nullByteSize;
            NativeParallelHashMap<int, BatchDomainLayout> oldLayouts = new NativeParallelHashMap<int, BatchDomainLayout>(m_AllocatedDomains.MaxLength, Allocator.Temp);
            bool domainsChanged = sceneProbesStatusChanged;

            foreach (int domainIndex in m_AllocatedDomains)
            {
                NativeRegionAllocator instanceAllocator = m_DomainAllocators[domainIndex];
                instanceAllocator.MergeFree();

                int newInstanceCapacity = instanceAllocator.MaxAllocatedSize;
                BatchDomainLayout oldLayout = m_DomainLayouts[domainIndex];
                bool addressChanged = oldLayout.BaseAddress != newBufferSizeInBytes;
                bool sizeChanged = oldLayout.InstanceCapacity != newInstanceCapacity;
                domainsChanged |= addressChanged || sizeChanged;

                BatchDomainLayout newLayout = new BatchDomainLayout(m_DomainDescriptors[domainIndex], newBufferSizeInBytes, newInstanceCapacity, sceneHasLightProbes);
                m_DomainLayouts[domainIndex] = newLayout;
                if (oldLayout.IsCreated)
                    oldLayouts[domainIndex] = oldLayout;

                newBufferSizeInBytes += newLayout.LengthInBytes;
                newBufferSizeInBytes = MathUtility.NextMultipleOf(newBufferSizeInBytes, 16);
            }

            GraphicsBufferRef oldDataBuffer = m_DataBuffer;
            GraphicsBufferRef newDataBuffer = m_DataBuffer;

            long oldCapacityBytes = m_DataBuffer.SizeInBytes;
            long newCapacityBytes = math.max(newBufferSizeInBytes, InitialBufferSize);
            if (newCapacityBytes > SystemInfo.maxGraphicsBufferSize)
            {
                newCapacityBytes = SystemInfo.maxGraphicsBufferSize;
                Debug.LogError("Flora: Current loaded scenes require more than the system's maximum graphics buffer size. Reduce the number of instances in the scene.");
            }

            if (newCapacityBytes > MaxAddressableBufferSize32)
            {
                newCapacityBytes = MaxAddressableBufferSize32;
                Debug.LogError("Flora: Current loaded scenes require more than 2GB of instance data which is not supported. Reduce the number of instances in the scene.");
            }

            domainsChanged |= newCapacityBytes > oldCapacityBytes ||
                              newCapacityBytes < oldCapacityBytes / 2;

            if (domainsChanged)
            {
                long newSizeInWords = newCapacityBytes / 4;
                newDataBuffer = new GraphicsBufferRef(GraphicsBuffer.Target.Raw, (int)newSizeInWords, 4, "Flora.InstanceBuffer");
                newDataBuffer.SetData(new NativeArray<float4x4>(1, Allocator.Temp), 0, 0, 1);

                if (oldDataBuffer.IsCreated)
                {
                    NativeList<BufferCopyCommand> copyCommands = new NativeList<BufferCopyCommand>(16, m_InstanceManager.ValueRW.FrameAllocatorHandle);

                    foreach (KeyValue<int, BatchDomainLayout> kvp in oldLayouts)
                    {
                        int domainIndex = kvp.Key;
                        BatchDomainLayout oldLayout = kvp.Value;
                        BatchDomainLayout newLayout = m_DomainLayouts[domainIndex];
                        if (!newLayout.IsCreated)
                            continue;

                        int maxInstanceCopy = math.min(oldLayout.InstanceCapacity, newLayout.InstanceCapacity);

                        for (int newMetadataIndex = 0; newMetadataIndex < newLayout.MetadataValues.Length; newMetadataIndex++)
                        {
                            BatchPropertyInfo propertyInfo = newLayout.Properties[newMetadataIndex];
                            if (propertyInfo.IsOverriden)
                            {
                                int instancesToCopy = propertyInfo.IsPerInstance ? maxInstanceCopy : 1;
                                if (instancesToCopy > 0 &&
                                    oldLayout.NameToIndexMap.TryGetValue(propertyInfo.NameID, out int oldMetadataIndex) &&
                                    oldLayout.MetadataValues[oldMetadataIndex].IsOverriden())
                                {
                                    var srcAddress = oldLayout.MetadataValues[oldMetadataIndex].Address();
                                    var dstAddress = newLayout.MetadataValues[newMetadataIndex].Address();

                                    copyCommands.Add(new BufferCopyCommand
                                    {
                                        srcAddress = srcAddress,
                                        dstAddress = dstAddress,
                                        stride = (uint)propertyInfo.TypeSizeInBytes,
                                        count = (uint)instancesToCopy,
                                    });
                                }
                            }
                        }
                    }

                    if (copyCommands.Length > 0)
                    {
                        InstanceBufferUpload.CopyComponents(oldDataBuffer, newDataBuffer, copyCommands.AsArray());
                    }
                }

                m_DomainRandomIdAddresses.ResizeIfNeeded(m_DomainCount, GraphicsBufferGrowPolicy.WithSlack);
                m_DomainVariationColorAddresses.ResizeIfNeeded(m_DomainCount, GraphicsBufferGrowPolicy.WithSlack);
                m_DomainLightmapSTAddresses.ResizeIfNeeded(m_DomainCount, GraphicsBufferGrowPolicy.WithSlack);
                m_DomainSHCoefficientsAddresses.ResizeIfNeeded(m_DomainCount, GraphicsBufferGrowPolicy.WithSlack);
                m_DomainEntityIdAddresses.ResizeIfNeeded(m_DomainCount, GraphicsBufferGrowPolicy.WithSlack);
                m_DomainCullingAddresses.ResizeIfNeeded(m_DomainCount, GraphicsBufferGrowPolicy.WithSlack);
                m_DomainTransformAddresses.ResizeIfNeeded(m_DomainCount, GraphicsBufferGrowPolicy.WithSlack);

                foreach (int domainIndex in m_AllocatedDomains)
                {
                    BatchID oldBatchID = m_DomainBatches[domainIndex];
                    if (oldBatchID != BatchID.Null)
                        batchRendererGroup.RemoveBatch(oldBatchID);

                    BatchDomainLayout layout = m_DomainLayouts[domainIndex];
                    BatchID newBatchID = batchRendererGroup.AddBatch(layout.MetadataValues, newDataBuffer.BufferHandle);
                    m_DomainBatches[domainIndex] = newBatchID;

                    m_DomainRandomIdAddresses[domainIndex] = layout.GetPerInstanceAddress(ShaderPropertyId.flora_RandomID);
                    m_DomainVariationColorAddresses[domainIndex] = layout.GetPerInstanceAddress(ShaderPropertyId.flora_VariationColor);
                    m_DomainLightmapSTAddresses[domainIndex] = layout.GetPerInstanceAddress(ShaderPropertyId.unity_LightmapST);
                    m_DomainSHCoefficientsAddresses[domainIndex] = layout.GetPerInstanceAddress(ShaderPropertyId.unity_SHCoefficients);
                    m_DomainEntityIdAddresses[domainIndex] = layout.GetPerInstanceAddress(ShaderPropertyId.unity_EntityId);

                    BatchCullingAddresses batchCullingAddresses = new BatchCullingAddresses
                    {
                        localToWorld = layout.GetPerInstanceAddress(ShaderPropertyId.unity_ObjectToWorld),
                        randomID = layout.GetPerInstanceAddress(ShaderPropertyId.flora_RandomID),
                    };
                    m_DomainCullingAddresses[domainIndex] = batchCullingAddresses;

                    BatchTransformAddresses batchTransformAddresses = new BatchTransformAddresses
                    {
                        localToWorld = layout.GetPerInstanceAddress(ShaderPropertyId.unity_ObjectToWorld),
                        worldToLocal = layout.GetPerInstanceAddress(ShaderPropertyId.unity_WorldToObject),
                        prevLocalToWorld = layout.GetPerInstanceAddress(ShaderPropertyId.unity_MatrixPreviousM),
                        prevWorldToLocal = layout.GetPerInstanceAddress(ShaderPropertyId.unity_MatrixPreviousMI),
                    };
                    m_DomainTransformAddresses[domainIndex] = batchTransformAddresses;
                }

                m_DomainRandomIdAddresses.UpdateBufferData();
                m_DomainVariationColorAddresses.UpdateBufferData();
                m_DomainLightmapSTAddresses.UpdateBufferData();
                m_DomainSHCoefficientsAddresses.UpdateBufferData();
                m_DomainEntityIdAddresses.UpdateBufferData();
                m_DomainCullingAddresses.UpdateBufferData();
                m_DomainTransformAddresses.UpdateBufferData();
            }

            foreach (KeyValue<int, BatchDomainLayout> kvp in oldLayouts)
                kvp.Value.Dispose();

            oldLayouts.Dispose();

            m_LayoutDirty = false;
            m_LayoutVersion++;

            if (newDataBuffer != oldDataBuffer)
            {
                m_DataBuffer = newDataBuffer;
                oldDataBuffer.Dispose();
            }

            return true;
        }


        #region Uploads

        public bool IsUploadScheduled()
        {
            return m_ContentVersionScheduled != 0;
        }

        public bool IsUploadScheduledFor(uint contentVersion)
        {
            return m_ContentVersionScheduled == contentVersion;
        }

        public bool HasStaleScheduledUpload(uint contentVersion)
        {
            return m_ContentVersionScheduled != 0 && m_ContentVersionScheduled != contentVersion;
        }

        public bool ScheduleUpload(uint contentVersion)
        {
            if (m_ContentVersionScheduled == contentVersion)
                return false; // Already scheduled
            if (!ChangeVersionUtility.DidChange(contentVersion, m_ContentVersionApplied))
                return false; // No changes

            m_ContentVersionScheduled = contentVersion;
            return true;
        }

        public void ApplyUpload()
        {
            if (m_ContentVersionScheduled == 0)
                return;

            m_ContentVersionApplied = m_ContentVersionScheduled;
            m_ContentVersionScheduled = 0;
        }

        #endregion
    }
}
