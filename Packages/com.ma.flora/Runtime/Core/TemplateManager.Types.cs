// Copyright © Magnetic Arcade. All Rights Reserved.

using System;
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace MA.Flora
{
    [StructLayout(LayoutKind.Sequential)]
    internal unsafe struct TemplateData
    {
        public const int MaxLodCount = CullingConstants.MaxLodCount;
        public const uint TemplateFlagIsLodGroup              = 1 << 0;
        public const uint TemplateFlagIsMeshLod               = 1 << 1;
        public const uint TemplateFlagHasMotionVectors        = 1 << 2;
        public const uint TemplateFlagHasCrossFade            = 1 << 3;
        public const uint TemplateFlagHasAnimatedFade         = 1 << 4;
        public const uint TemplateFlagAffectedByGlobalDensity = 1 << 5;
        public const uint TemplateFlagAffectedByRangeDensity  = 1 << 6;
        public const uint TemplateFlagAffectedByMinScreenSize = 1 << 7;
        public const uint TemplateFlagHasRandomID             = 1 << 8;

        public uint flags;
        public uint layer;
        public uint renderingLayerMask;
        public float maxRenderDistance;

        public Vector3 localCenter;
        public float maxShadowDistance;

        public Vector3 localExtent;
        public float localBoundingRadius;

        public Vector3 lodPoint;
        public float localSize;

        public uint lodCount;
        public uint lodMax;
        public uint lodMinShadow;
        public uint packedLODFlags;

        public float meshLodSlope;
        public float meshLodBias;
        public float meshLodSelectionBias;
        public float meshLodUnused;

        [HLSLArray(MaxLodCount, typeof(float))]
        public fixed float lodHeightRcp[MaxLodCount];

        [HLSLArray(MaxLodCount, typeof(float))]
        public fixed float lodTransitionHeightRcp[MaxLodCount];
    }

    internal unsafe struct TemplateStore
    {
        private struct StaticIdentifier
        {
            internal static readonly SharedStatic<TemplateStore> Ref = SharedStatic<TemplateStore>.GetOrCreate<StaticIdentifier>();
        }

        public static PerTemplateData* Data => StaticIdentifier.Ref.Data.m_PerTemplateData;
        public const int MaxPossiblePrefabCount = 1024 * 64;

        public struct PerTemplateData
        {
            public int Layer;
            public ulong SceneCullingMask;
            public BatchDomainIndex BatchDomainIndex;
            public TemplateRenderType Type;
            public TemplateRenderFlags Flags;
            public float4 InitialVariationColor;
            public float MaxRenderDistance;
            public float MaxShadowDistance;
            public bool AffectedByGlobalDensity;
            public bool AffectedByRangeDensity;
            public int MinShadowLod;
            public byte LodCount;
            public LODFadeMode LodFadeMode;
            public bool HasAnimatedCrossFade;
            public bool SupportsFadeKeyword;
            public Vector3 LocalAnchorPoint;
            public Vector3 LocalReferencePoint;
            public float LocalSize;
            public AABB LocalAABB;
            public fixed float LODHeights[TemplateData.MaxLodCount];
            public fixed float LODTransitionHeights[TemplateData.MaxLodCount];
        }

        private PerTemplateData* m_PerTemplateData;

        [BurstDiscard]
        internal static void Initialize()
        {
            if (StaticIdentifier.Ref.Data.m_PerTemplateData == null)
            {
                var data = AllocatorManager.Allocate<PerTemplateData>(Allocator.Persistent, MaxPossiblePrefabCount);
                StaticIdentifier.Ref.Data.m_PerTemplateData = data;

                void Shutdown()
                {
                    AllocatorManager.Free(Allocator.Persistent, StaticIdentifier.Ref.Data.m_PerTemplateData);
                    StaticIdentifier.Ref.Data.m_PerTemplateData = null;
                }

                AppDomain.CurrentDomain.DomainUnload += (_, _) => Shutdown();
                AppDomain.CurrentDomain.ProcessExit += (_, _) => Shutdown();
            }

            UnsafeUtility.MemClear(StaticIdentifier.Ref.Data.m_PerTemplateData, sizeof(PerTemplateData) * MaxPossiblePrefabCount);
        }

        internal static void Reset(TemplateIndex template)
        {
            if (StaticIdentifier.Ref.Data.m_PerTemplateData != null)
            {
                StaticIdentifier.Ref.Data.m_PerTemplateData[template] = default;
            }
        }
    }

    internal unsafe struct TemplateIndex : IEquatable<TemplateIndex>, IComparable<TemplateIndex>
    {
        public static TemplateIndex None => default;

        public int Index;
        public bool IsCreated => Index > 0;

        public TemplateIndex(int index)
        {
            Index = index;
        }

        public BatchDomainIndex BatchDomainIndex
        {
            get => TemplateStore.Data[Index].BatchDomainIndex;
            set => TemplateStore.Data[Index].BatchDomainIndex = value;
        }

        public TemplateRenderType Type
        {
            get => TemplateStore.Data[Index].Type;
            set => TemplateStore.Data[Index].Type = value;
        }

        public TemplateRenderFlags Flags
        {
            get => TemplateStore.Data[Index].Flags;
            set => TemplateStore.Data[Index].Flags = value;
        }

        public float4 InitialVariationColor
        {
            get => TemplateStore.Data[Index].InitialVariationColor;
            set => TemplateStore.Data[Index].InitialVariationColor = value;
        }

        public bool IsMeshLod => Type == TemplateRenderType.MeshLod;
        public bool IsBillboard => Type == TemplateRenderType.Billboard;
        public bool IsLodGroup => Type == TemplateRenderType.LodGroup;
        public bool HasMotionVectors => (Flags & TemplateRenderFlags.HasPerObjectMotionVectors) != 0;
        public bool HasShadowCasters => (Flags & TemplateRenderFlags.HasShadowCasters) != 0;
        public bool HasLightmaps => (Flags & TemplateRenderFlags.HasLightmaps) != 0;
        public bool HasLightProbes => (Flags & TemplateRenderFlags.HasLightProbes) != 0;
        public bool HasRandomID => (Flags & TemplateRenderFlags.HasRandomID) != 0;
        public bool HasVariationColor => (Flags & TemplateRenderFlags.HasVariationColor) != 0;

        public float MaxRenderDistance
        {
            get => TemplateStore.Data[Index].MaxRenderDistance;
            set => TemplateStore.Data[Index].MaxRenderDistance = value;
        }

        public float MaxShadowDistance
        {
            get => TemplateStore.Data[Index].MaxShadowDistance;
            set => TemplateStore.Data[Index].MaxShadowDistance = value;
        }

        public bool AffectedByGlobalDensity
        {
            get => TemplateStore.Data[Index].AffectedByGlobalDensity;
            set => TemplateStore.Data[Index].AffectedByGlobalDensity = value;
        }

        public bool AffectedByRangeDensity
        {
            get => TemplateStore.Data[Index].AffectedByRangeDensity;
            set => TemplateStore.Data[Index].AffectedByRangeDensity = value;
        }

        public int MinShadowLod
        {
            get => TemplateStore.Data[Index].MinShadowLod;
            set => TemplateStore.Data[Index].MinShadowLod = value;
        }

        public int LodCount
        {
            get => TemplateStore.Data[Index].LodCount;
            set => TemplateStore.Data[Index].LodCount = (byte)value;
        }

        public LODFadeMode LodFadeMode
        {
            get => TemplateStore.Data[Index].LodFadeMode;
            set => TemplateStore.Data[Index].LodFadeMode = value;
        }

        public bool HasCrossFade => SupportsFadeKeyword && LodFadeMode != LODFadeMode.None;

        public bool HasAnimatedCrossFade
        {
            get => TemplateStore.Data[Index].HasAnimatedCrossFade;
            set => TemplateStore.Data[Index].HasAnimatedCrossFade = value;
        }

        public bool SupportsFadeKeyword
        {
            get => TemplateStore.Data[Index].SupportsFadeKeyword;
            set => TemplateStore.Data[Index].SupportsFadeKeyword = value;
        }

        public Vector3 LocalReferencePoint
        {
            get => TemplateStore.Data[Index].LocalReferencePoint;
            set => TemplateStore.Data[Index].LocalReferencePoint = value;
        }

        public float LocalSize
        {
            get => TemplateStore.Data[Index].LocalSize;
            set => TemplateStore.Data[Index].LocalSize = value;
        }

        public ref AABB LocalAABB => ref TemplateStore.Data[Index].LocalAABB;

        public Vector3 LocalAnchorPoint
        {
            get => TemplateStore.Data[Index].LocalAnchorPoint;
            set => TemplateStore.Data[Index].LocalAnchorPoint = value;
        }

        public float* LODHeights => TemplateStore.Data[Index].LODHeights;
        public float* LODTransitionHeights => TemplateStore.Data[Index].LODTransitionHeights;

        public int CompareTo(TemplateIndex other) => Index - other.Index;
        public bool Equals(TemplateIndex other) => Index == other.Index;
        public override bool Equals(object obj) => obj is TemplateIndex other && Equals(other);
        public override int GetHashCode() => Index;
        public override string ToString() => Equals(None) ? "TemplateIndex.Null" : $"TemplateIndex({Index})";

        public static implicit operator TemplateIndex(int index) => new TemplateIndex(index);
        public static implicit operator int(TemplateIndex template) => template.Index;
        public static bool operator ==(TemplateIndex a, TemplateIndex b) => a.Index == b.Index;
        public static bool operator !=(TemplateIndex a, TemplateIndex b) => a.Index != b.Index;
    }

    [Flags]
    internal enum TemplateOptions
    {
        None                 = 0,
        DisableMotionVectors = 1 << 0,
        DisableLightProbes   = 1 << 1,
        DisableLightmaps     = 1 << 2,
        RandomID             = 1 << 3,
        VariationColor       = 1 << 4,
    }

    internal struct TemplateKey : IEquatable<TemplateKey>
    {
        public TemplateLayoutIndex Layout;

        public TemplateKey(TemplateLayoutIndex layout)
        {
            Layout = layout;
        }

        public bool Equals(TemplateKey other) => Layout.Equals(other.Layout);
        public override bool Equals(object obj) => obj is TemplateKey other && Equals(other);

        public override int GetHashCode()
        {
            return Layout.GetHashCode();
        }
    }

    internal struct SourceTemplateBinding
    {
        public SourceRecordIndex SourceRecord;
        public TemplateIndex Template;
    }

    [Flags]
    internal enum TemplateStateChangeMask : byte
    {
        None               = 0,
        DomainChanged      = 1 << 0,
        DrawChanged        = 1 << 1,
        TemplateDataChanged = 1 << 2,
        CapabilityChanged  = 1 << 3,
    }

    internal struct SourceRecordIndex : IEquatable<SourceRecordIndex>, IComparable<SourceRecordIndex>
    {
        public static SourceRecordIndex None => default;

        public int Index;
        public bool IsCreated => Index > 0;

        public SourceRecordIndex(int index) => Index = index;

        public int CompareTo(SourceRecordIndex other) => Index.CompareTo(other.Index);
        public bool Equals(SourceRecordIndex other) => Index == other.Index;
        public override bool Equals(object obj) => obj is SourceRecordIndex other && Equals(other);
        public override int GetHashCode() => Index;
        public override string ToString() => Equals(None) ? "SourceRecordIndex.None" : $"SourceRecordIndex({Index})";

        public static implicit operator int(SourceRecordIndex index) => index.Index;
        public static implicit operator SourceRecordIndex(int index) => new SourceRecordIndex(index);
        public static bool operator ==(SourceRecordIndex a, SourceRecordIndex b) => a.Index == b.Index;
        public static bool operator !=(SourceRecordIndex a, SourceRecordIndex b) => a.Index != b.Index;
    }

    internal struct SourceRecord
    {
        public EntityId IdentitySourceId;
        public EntityId RenderSourceId;
        public EntityId LodGroupId;
        public EntityId AdditionalSettingsId;
        public int LightmapIndex;
        public float4 LightmapScaleOffset;
        public int RefCount;
    }

    internal struct TemplateCapabilityProfile : IEquatable<TemplateCapabilityProfile>
    {
        public BatchBuiltinPropertyFlags MetadataFlags;
        public BatchDomainIndex BatchDomainIndex;
        public TemplateRenderFlags EffectiveFlags;
        public TemplateOptions Options;

        public bool Equals(TemplateCapabilityProfile other)
        {
            return MetadataFlags == other.MetadataFlags &&
                   BatchDomainIndex == other.BatchDomainIndex &&
                   EffectiveFlags == other.EffectiveFlags &&
                   Options == other.Options;
        }

        public override bool Equals(object obj) => obj is TemplateCapabilityProfile other && Equals(other);
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = (hash * 397) ^ (int)MetadataFlags;
                hash = (hash * 397) ^ BatchDomainIndex.GetHashCode();
                hash = (hash * 397) ^ (int)EffectiveFlags;
                hash = (hash * 397) ^ (int)Options;
                return hash;
            }
        }
    }

    internal struct RendererStateIndex : IEquatable<RendererStateIndex>, IComparable<RendererStateIndex>
    {
        public static RendererStateIndex None => default;

        public int Index;
        public bool IsCreated => Index > 0;

        public RendererStateIndex(int index) => Index = index;

        public int CompareTo(RendererStateIndex other) => Index.CompareTo(other.Index);
        public bool Equals(RendererStateIndex other) => Index == other.Index;
        public override bool Equals(object obj) => obj is RendererStateIndex other && Equals(other);
        public override int GetHashCode() => Index;
        public override string ToString() => Equals(None) ? "RendererStateIndex.None" : $"RendererStateIndex({Index})";

        public static implicit operator int(RendererStateIndex index) => index.Index;
        public static implicit operator RendererStateIndex(int index) => new RendererStateIndex(index);
        public static bool operator ==(RendererStateIndex a, RendererStateIndex b) => a.Index == b.Index;
        public static bool operator !=(RendererStateIndex a, RendererStateIndex b) => a.Index != b.Index;
    }

    internal struct RendererStateKey : IEquatable<RendererStateKey>
    {
        public EntityId OverrideMaterialId;
        public ulong DescriptorSignature;
        public uint MetadataFlags;
        public ushort DescriptorCount;
        public byte LodIndex;
        public byte Type;

        public bool Equals(RendererStateKey other)
        {
            return OverrideMaterialId.Equals(other.OverrideMaterialId) &&
                   DescriptorSignature == other.DescriptorSignature &&
                   MetadataFlags == other.MetadataFlags &&
                   DescriptorCount == other.DescriptorCount &&
                   LodIndex == other.LodIndex &&
                   Type == other.Type;
        }

        public override bool Equals(object obj) => obj is RendererStateKey other && Equals(other);
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = (hash * 397) ^ OverrideMaterialId.GetHashCode();
                hash = (hash * 397) ^ DescriptorSignature.GetHashCode();
                hash = (hash * 397) ^ (int)MetadataFlags;
                hash = (hash * 397) ^ DescriptorCount;
                hash = (hash * 397) ^ LodIndex;
                hash = (hash * 397) ^ Type;
                return hash;
            }
        }
    }

    internal struct RendererStateRecord
    {
        public RendererStateKey Key;
        public ushort DescriptorCount;
        public BatchDomainIndex BatchDomainIndex;
        public byte LodIndex;
        public TemplateRenderType Type;
        public TemplateCapabilityProfile CapabilityProfile;
        public int RefCount;
    }

    internal struct RendererGroupIndex : IEquatable<RendererGroupIndex>, IComparable<RendererGroupIndex>
    {
        public static RendererGroupIndex None => default;

        public int Index;
        public bool IsCreated => Index > 0;

        public RendererGroupIndex(int index) => Index = index;

        public int CompareTo(RendererGroupIndex other) => Index.CompareTo(other.Index);
        public bool Equals(RendererGroupIndex other) => Index == other.Index;
        public override bool Equals(object obj) => obj is RendererGroupIndex other && Equals(other);
        public override int GetHashCode() => Index;
        public override string ToString() => Equals(None) ? "RendererGroupIndex.None" : $"RendererGroupIndex({Index})";

        public static implicit operator int(RendererGroupIndex index) => index.Index;
        public static implicit operator RendererGroupIndex(int index) => new RendererGroupIndex(index);
        public static bool operator ==(RendererGroupIndex a, RendererGroupIndex b) => a.Index == b.Index;
        public static bool operator !=(RendererGroupIndex a, RendererGroupIndex b) => a.Index != b.Index;
    }

    internal struct RendererGroupKey : IEquatable<RendererGroupKey>
    {
        public ulong StateSignature;
        public ushort StateCount;
        public byte LodIndex;

        public bool Equals(RendererGroupKey other)
        {
            return StateSignature == other.StateSignature &&
                   StateCount == other.StateCount &&
                   LodIndex == other.LodIndex;
        }

        public override bool Equals(object obj) => obj is RendererGroupKey other && Equals(other);
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = (hash * 397) ^ StateSignature.GetHashCode();
                hash = (hash * 397) ^ StateCount;
                hash = (hash * 397) ^ LodIndex;
                return hash;
            }
        }
    }

    internal struct RendererGroupRecord
    {
        public RendererGroupKey Key;
        public byte LodIndex;
        public int RefCount;
    }

    internal struct TemplateLayoutIndex : IEquatable<TemplateLayoutIndex>, IComparable<TemplateLayoutIndex>
    {
        public static TemplateLayoutIndex None => default;

        public int Index;
        public bool IsCreated => Index > 0;

        public TemplateLayoutIndex(int index) => Index = index;

        public int CompareTo(TemplateLayoutIndex other) => Index.CompareTo(other.Index);
        public bool Equals(TemplateLayoutIndex other) => Index == other.Index;
        public override bool Equals(object obj) => obj is TemplateLayoutIndex other && Equals(other);
        public override int GetHashCode() => Index;
        public override string ToString() => Equals(None) ? "TemplateLayoutIndex.None" : $"TemplateLayoutIndex({Index})";

        public static implicit operator int(TemplateLayoutIndex index) => index.Index;
        public static implicit operator TemplateLayoutIndex(int index) => new TemplateLayoutIndex(index);
        public static bool operator ==(TemplateLayoutIndex a, TemplateLayoutIndex b) => a.Index == b.Index;
        public static bool operator !=(TemplateLayoutIndex a, TemplateLayoutIndex b) => a.Index != b.Index;
    }

    internal unsafe struct TemplateLayoutKey : IEquatable<TemplateLayoutKey>
    {
        public EntityId GrassMaterialId;
        public TemplateCapabilityProfile CapabilityProfile;
        public ulong GroupSignature;
        public ushort GroupCount;
        public TemplateRenderType Type;
        public float4 InitialVariationColor;
        public LODFadeMode LodFadeMode;
        public bool HasAnimatedCrossFade;
        public bool SupportsFadeKeyword;
        public Vector3 LocalAnchorPoint;
        public TemplateData TemplateData;

        public bool Equals(TemplateLayoutKey other)
        {
            return GrassMaterialId.Equals(other.GrassMaterialId) &&
                   CapabilityProfile.Equals(other.CapabilityProfile) &&
                   GroupSignature == other.GroupSignature &&
                   GroupCount == other.GroupCount &&
                   Type == other.Type &&
                   InitialVariationColor.Equals(other.InitialVariationColor) &&
                   LodFadeMode == other.LodFadeMode &&
                   HasAnimatedCrossFade == other.HasAnimatedCrossFade &&
                   SupportsFadeKeyword == other.SupportsFadeKeyword &&
                   LocalAnchorPoint.Equals(other.LocalAnchorPoint) &&
                   TemplateDataEquals(in TemplateData, in other.TemplateData);
        }

        public override bool Equals(object obj) => obj is TemplateLayoutKey other && Equals(other);
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = (hash * 397) ^ GrassMaterialId.GetHashCode();
                hash = (hash * 397) ^ CapabilityProfile.GetHashCode();
                hash = (hash * 397) ^ GroupSignature.GetHashCode();
                hash = (hash * 397) ^ GroupCount;
                hash = (hash * 397) ^ (int)Type;
                hash = (hash * 397) ^ InitialVariationColor.GetHashCode();
                hash = (hash * 397) ^ (int)LodFadeMode;
                hash = (hash * 397) ^ (HasAnimatedCrossFade ? 1 : 0);
                hash = (hash * 397) ^ (SupportsFadeKeyword ? 1 : 0);
                hash = (hash * 397) ^ LocalAnchorPoint.GetHashCode();
                hash = (hash * 397) ^ GetTemplateDataHash(in TemplateData);
                return hash;
            }
        }

        private static bool TemplateDataEquals(in TemplateData a, in TemplateData b)
        {
            TemplateData aData = a;
            TemplateData bData = b;
            return UnsafeUtility.MemCmp(&aData, &bData, UnsafeUtility.SizeOf<TemplateData>()) == 0;
        }

        private static int GetTemplateDataHash(in TemplateData data)
        {
            unchecked
            {
                TemplateData copy = data;
                byte* bytes = (byte*)&copy;
                int hash = 17;
                for (int i = 0; i < UnsafeUtility.SizeOf<TemplateData>(); i++)
                    hash = (hash * 397) ^ bytes[i];
                return hash;
            }
        }
    }

    internal struct TemplateLayoutRecord
    {
        public TemplateLayoutKey Key;
        public TemplateCapabilityProfile CapabilityProfile;
        public ulong GroupSignature;
        public TemplateRenderType Type;
        public TemplateRenderFlags Flags;
        public BatchDomainIndex BatchDomainIndex;
        public float4 InitialVariationColor;
        public float MaxRenderDistance;
        public float MaxShadowDistance;
        public bool AffectedByGlobalDensity;
        public bool AffectedByRangeDensity;
        public int MinShadowLod;
        public byte LodCount;
        public LODFadeMode LodFadeMode;
        public bool HasAnimatedCrossFade;
        public bool SupportsFadeKeyword;
        public Vector3 LocalAnchorPoint;
        public Vector3 LocalReferencePoint;
        public float LocalSize;
        public AABB LocalAABB;
        public float4 LodHeights0To3;
        public float4 LodHeights4To7;
        public float4 LodTransitionHeights0To3;
        public float4 LodTransitionHeights4To7;
        public TemplateData TemplateData;
        public int RefCount;
    }

    internal struct LodDrawEntry
    {
        public byte LodIndex;
        public DrawBatchIndex DrawIndex;
    }
}
