// Copyright © Magnetic Arcade. All Rights Reserved.

using System;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Rendering;

#if UNITY_EDITOR
#endif

namespace MA.Flora
{
    internal struct DrawRangeIndex : IEquatable<DrawRangeIndex>
    {
        public static DrawRangeIndex None => new DrawRangeIndex();

        private int m_Value;

        public DrawRangeIndex(int value) => m_Value = value;

        public int CompareTo(DrawRangeIndex other) => m_Value.CompareTo(other.m_Value);
        public bool Equals(DrawRangeIndex other) => m_Value == other.m_Value;
        public override bool Equals(object obj) => obj is DrawRangeIndex other && Equals(other);
        public override int GetHashCode() => m_Value;
        public override string ToString() => Equals(None) ? "DrawRangeIndex.None" : $"DrawRangeIndex({m_Value})";

        public static implicit operator int(DrawRangeIndex index) => index.m_Value;
        public static implicit operator DrawRangeIndex(int id) => new DrawRangeIndex(id);

        public static bool operator ==(DrawRangeIndex left, DrawRangeIndex right) => left.m_Value == right.m_Value;
        public static bool operator !=(DrawRangeIndex left, DrawRangeIndex right) => left.m_Value != right.m_Value;
    }

    internal struct DrawBatchIndex : IEquatable<DrawBatchIndex>
    {
        public static DrawBatchIndex None => new DrawBatchIndex();

        private int m_Value;

        public DrawBatchIndex(int value) => m_Value = value;

        public int CompareTo(DrawBatchIndex other) => m_Value.CompareTo(other.m_Value);
        public bool Equals(DrawBatchIndex other) => m_Value == other.m_Value;
        public override bool Equals(object obj) => obj is DrawBatchIndex other && Equals(other);
        public override int GetHashCode() => m_Value;
        public override string ToString() => Equals(None) ? "DrawIndex.None" : $"DrawIndex({m_Value})";

        public static implicit operator int(DrawBatchIndex index) => index.m_Value;
        public static implicit operator DrawBatchIndex(int id) => new DrawBatchIndex(id);

        public static bool operator ==(DrawBatchIndex left, DrawBatchIndex right) => left.m_Value == right.m_Value;
        public static bool operator !=(DrawBatchIndex left, DrawBatchIndex right) => left.m_Value != right.m_Value;
    }

    internal struct DrawRangeKey : IEquatable<DrawRangeKey>
    {
        public byte Layer;
        public uint RenderingLayerMask;
        public int RendererPriority;
        public MotionVectorGenerationMode MotionMode;
        public ShadowCastingMode ShadowCastingMode;
        public bool ReceiveShadows;
        public bool StaticShadowCaster;

        public bool IsInCameraPass => ShadowCastingMode != ShadowCastingMode.ShadowsOnly;
        public bool IsInShadowPass => ShadowCastingMode != ShadowCastingMode.Off;
        public bool IsInMotionPass => MotionMode != MotionVectorGenerationMode.Camera;

        public DrawRangeKey(Renderer renderer)
        {
            Layer = (byte)renderer.gameObject.layer;
            RenderingLayerMask = renderer.renderingLayerMask;
            RendererPriority = renderer.rendererPriority;
            MotionMode = renderer.motionVectorGenerationMode;
            ShadowCastingMode = renderer.shadowCastingMode;
            ReceiveShadows = renderer.receiveShadows;
            StaticShadowCaster = renderer.staticShadowCaster;
        }

        public bool Equals(DrawRangeKey rhs)
        {
            return Layer == rhs.Layer &&
                   RenderingLayerMask == rhs.RenderingLayerMask &&
                   RendererPriority == rhs.RendererPriority &&
                   MotionMode == rhs.MotionMode &&
                   ShadowCastingMode == rhs.ShadowCastingMode &&
                   ReceiveShadows == rhs.ReceiveShadows &&
                   StaticShadowCaster == rhs.StaticShadowCaster;
        }

        public override int GetHashCode()
        {
            int hash = 13;
            hash = (hash * 23) + Layer;
            hash = (hash * 23) + (int)RenderingLayerMask;
            hash = (hash * 23) + (int)MotionMode;
            hash = (hash * 23) + (int)ShadowCastingMode;
            hash = (hash * 23) + (StaticShadowCaster ? 1 : 0);
            hash = (hash * 23) + (ReceiveShadows ? 1 : 0);
            hash = (hash * 23) + RendererPriority;
            return hash;
        }

        public bool IsValidForViewType(BatchCullingViewType viewType)
        {
            if (viewType == BatchCullingViewType.Light && ShadowCastingMode == ShadowCastingMode.Off)
                return false;
            if (viewType != BatchCullingViewType.Light && ShadowCastingMode == ShadowCastingMode.ShadowsOnly)
                return false;

            return true;
        }
    }

    internal struct DrawBatchKey : IEquatable<DrawBatchKey>
    {
        public DrawRangeIndex RangeIndex;
        public BatchDomainIndex BatchDomainIndex;
        public BatchMeshID MeshID;
        public EntityId MeshEntityId;
        public int LodIndex;
        public int ActiveMeshLod; // or -1 if this draw is not using mesh LOD
        public ushort SubMeshIndex;
        public BatchMaterialID MaterialID;
        public EntityId MaterialEntityId;
        public BatchDrawCommandFlags Flags;
        public IndirectStateFlags SupportedStateFlags;
        public byte SupportedStateMask;
        public MeshTopology Topology;
        public uint BaseVertex;
        public uint FirstIndex;
        public uint IndexCount;

        public bool Equals(DrawBatchKey other)
        {
            return RangeIndex.Equals(other.RangeIndex) &&
                   BatchDomainIndex == other.BatchDomainIndex &&
                   MeshEntityId == other.MeshEntityId &&
                   LodIndex == other.LodIndex &&
                   ActiveMeshLod == other.ActiveMeshLod &&
                   SubMeshIndex == other.SubMeshIndex &&
                   MaterialEntityId == other.MaterialEntityId &&
                   Flags == other.Flags &&
                   SupportedStateFlags == other.SupportedStateFlags &&
                   Topology == other.Topology &&
                   BaseVertex == other.BaseVertex &&
                   FirstIndex == other.FirstIndex &&
                   IndexCount == other.IndexCount;
        }

        public override int GetHashCode()
        {
            int hash = 13;
            hash = (hash * 23) + RangeIndex.GetHashCode();
            hash = (hash * 23) + BatchDomainIndex;
            hash = (hash * 23) + MeshEntityId.GetHashCode();
            hash = (hash * 23) + LodIndex;
            hash = (hash * 23) + ActiveMeshLod;
            hash = (hash * 23) + SubMeshIndex;
            hash = (hash * 23) + MaterialEntityId.GetHashCode();
            hash = (hash * 23) + (int)Flags;
            hash = (hash * 23) + (int)SupportedStateFlags;
            hash = (hash * 23) + (int)Topology;
            hash = (hash * 23) + (int)BaseVertex;
            hash = (hash * 23) + (int)FirstIndex;
            hash = (hash * 23) + (int)IndexCount;
            return hash;
        }
    }

    internal struct DrawDescriptor : IEquatable<DrawDescriptor>
    {
        public DrawRangeKey RangeKey;
        public BatchDomainIndex BatchDomainIndex;
        public EntityId MeshEntityId;
        public int LodIndex;
        public int ActiveMeshLod; // or -1 if this draw is not using mesh LOD
        public ushort SubMeshIndex;
        public EntityId MaterialEntityId;
        public BatchDrawCommandFlags Flags;
        public IndirectStateFlags SupportedStateFlags;
        public byte SupportedStateMask;
        public MeshTopology Topology;
        public uint BaseVertex;
        public uint FirstIndex;
        public uint IndexCount;

        public bool Equals(DrawDescriptor other)
        {
            return RangeKey.Equals(other.RangeKey) &&
                   BatchDomainIndex == other.BatchDomainIndex &&
                   MeshEntityId == other.MeshEntityId &&
                   LodIndex == other.LodIndex &&
                   ActiveMeshLod == other.ActiveMeshLod &&
                   SubMeshIndex == other.SubMeshIndex &&
                   MaterialEntityId == other.MaterialEntityId &&
                   Flags == other.Flags &&
                   SupportedStateFlags == other.SupportedStateFlags &&
                   SupportedStateMask == other.SupportedStateMask &&
                   Topology == other.Topology &&
                   BaseVertex == other.BaseVertex &&
                   FirstIndex == other.FirstIndex &&
                   IndexCount == other.IndexCount;
        }

        public override bool Equals(object obj) => obj is DrawDescriptor other && Equals(other);

        public override int GetHashCode()
        {
            int hash = 13;
            hash = (hash * 23) + RangeKey.GetHashCode();
            hash = (hash * 23) + BatchDomainIndex;
            hash = (hash * 23) + MeshEntityId.GetHashCode();
            hash = (hash * 23) + LodIndex;
            hash = (hash * 23) + ActiveMeshLod;
            hash = (hash * 23) + SubMeshIndex;
            hash = (hash * 23) + MaterialEntityId.GetHashCode();
            hash = (hash * 23) + (int)Flags;
            hash = (hash * 23) + (int)SupportedStateFlags;
            hash = (hash * 23) + SupportedStateMask;
            hash = (hash * 23) + (int)Topology;
            hash = (hash * 23) + (int)BaseVertex;
            hash = (hash * 23) + (int)FirstIndex;
            hash = (hash * 23) + (int)IndexCount;
            return hash;
        }
    }

    internal struct DrawMeshInfo
    {
        public MeshTopology Topology;
        public uint BaseVertex;
        public uint FirstIndex;
        public uint IndexCount;
    }

    internal struct DrawBatch
    {
        public DrawBatchKey Key;
        public int KeyHash;
        public DrawMeshInfo MeshInfo;
    }

    internal partial struct DrawManager : IDisposable
    {
        private int m_NextDrawRangeIndex;
        private NativeBitSet m_DrawRangeIndices;
        private NativeList<DrawRangeIndex> m_DrawRangeFreeIndices;
        private NativeParallelHashMap<DrawRangeKey, DrawRangeIndex> m_DrawRangeHash;
        private NativeArray<DrawRangeKey> m_DrawRangeKeys;
        private NativeBufferArray<DrawBatchIndex> m_DrawRangeBatches;

        private NativeParallelHashMap<float4x4, int> m_DrawMatrixMap;
        private NativeList<float4x4> m_DrawMatrixKeys;

        private int m_NextDrawBatchIndex;
        private NativeBitSet m_DrawBatchIndices;
        private NativeList<DrawBatchIndex> m_DrawBatchFreeIndices;
        private NativeParallelHashMap<DrawBatchKey, DrawBatchIndex> m_DrawBatchHash;
        private NativeArray<DrawBatch> m_DrawBatches;
        private NativeArray<int> m_DrawBatchRefCounts;
        private NativeArray<DrawRangeIndex> m_DrawBatchRangeIndices;
        private NativeBufferArray<CullingChunkIndex> m_DrawBatchChunks;
        private bool m_NeedsRebuild;

        public int DrawRangeCount => m_DrawRangeIndices.MaxLength;
        public int DrawBatchCount => m_DrawBatchIndices.MaxLength;

        public NativeBitSet DrawRangeIndices => m_DrawRangeIndices;
        public NativeArray<DrawRangeKey> DrawRangeKeys => m_DrawRangeKeys;
        public NativeBufferArray<DrawBatchIndex> DrawRangeBatches => m_DrawRangeBatches;

        public NativeBitSet DrawBatchIndices => m_DrawBatchIndices;
        public NativeArray<DrawBatch> DrawBatches => m_DrawBatches;
        public NativeArray<DrawRangeIndex> DrawBatchRangeIndices => m_DrawBatchRangeIndices;
        public NativeBufferArray<CullingChunkIndex> DrawBatchChunks => m_DrawBatchChunks;
        public bool NeedsRebuild => m_NeedsRebuild;

        private const int InitialCapacity = 64;

        public void Initialize()
        {
            m_NextDrawRangeIndex = 1;
            m_DrawRangeIndices = new NativeBitSet(InitialCapacity, Allocator.Persistent);
            m_DrawRangeFreeIndices = new NativeList<DrawRangeIndex>(InitialCapacity, Allocator.Persistent);
            m_DrawRangeHash = new NativeParallelHashMap<DrawRangeKey, DrawRangeIndex>(InitialCapacity, Allocator.Persistent);
            m_DrawRangeKeys = new NativeArray<DrawRangeKey>(InitialCapacity, Allocator.Persistent);
            m_DrawRangeBatches = new NativeBufferArray<DrawBatchIndex>(InitialCapacity, 0, Allocator.Persistent);

            m_DrawMatrixMap = new NativeParallelHashMap<float4x4, int>(InitialCapacity, Allocator.Persistent);
            m_DrawMatrixMap.Add(float4x4.identity, 0);
            m_DrawMatrixKeys = new NativeList<float4x4>(InitialCapacity, Allocator.Persistent);
            m_DrawMatrixKeys.Add(float4x4.identity);

            m_NextDrawBatchIndex = 1;
            m_DrawBatchIndices = new NativeBitSet(InitialCapacity, Allocator.Persistent);
            m_DrawBatchFreeIndices = new NativeList<DrawBatchIndex>(InitialCapacity, Allocator.Persistent);
            m_DrawBatchHash = new NativeParallelHashMap<DrawBatchKey, DrawBatchIndex>(InitialCapacity, Allocator.Persistent);
            m_DrawBatches = new NativeArray<DrawBatch>(InitialCapacity, Allocator.Persistent);
            m_DrawBatchRefCounts = new NativeArray<int>(InitialCapacity, Allocator.Persistent);
            m_DrawBatchRangeIndices = new NativeArray<DrawRangeIndex>(InitialCapacity, Allocator.Persistent);
            m_DrawBatchChunks = new NativeBufferArray<CullingChunkIndex>(InitialCapacity, 0, Allocator.Persistent);
            m_NeedsRebuild = false;
        }

        public void Dispose()
        {
            m_DrawRangeIndices.Dispose();
            m_DrawRangeFreeIndices.Dispose();
            m_DrawRangeHash.Dispose();
            m_DrawRangeKeys.Dispose();
            m_DrawRangeBatches.Dispose();

            m_DrawMatrixMap.Dispose();
            m_DrawMatrixKeys.Dispose();

            m_DrawBatchIndices.Dispose();
            m_DrawBatchFreeIndices.Dispose();
            m_DrawBatchHash.Dispose();
            m_DrawBatches.Dispose();
            m_DrawBatchRefCounts.Dispose();
            m_DrawBatchRangeIndices.Dispose();
            m_DrawBatchChunks.Dispose();
        }

        public void ResetCullingChunks()
        {
            foreach (int drawId in m_DrawBatchIndices)
                m_DrawBatchChunks[drawId].Clear();
        }

        public void AddCullingChunks(NativeArray<DrawBatchIndex> drawIds, NativeArray<CullingChunkIndex> chunksToAdd)
        {
            new AddChunksToDrawsJob {
                DrawIDs = drawIds,
                ChunksToAdd = chunksToAdd,
                DrawChunks = m_DrawBatchChunks,
            }.Run();
        }

        public void ClearCullingChunks(DrawBatchIndex drawIndex)
        {
            if (m_DrawBatchIndices.Contains(drawIndex))
                m_DrawBatchChunks[drawIndex].Clear();
        }

        public void AddCullingChunks(DrawBatchIndex drawIndex, NativeArray<CullingChunkIndex> chunksToAdd)
        {
            if (chunksToAdd.Length == 0 || !m_DrawBatchIndices.Contains(drawIndex))
                return;

            m_DrawBatchChunks[drawIndex].AddRange(chunksToAdd);
        }

        public bool ContainsDraw(DrawBatchIndex drawIndex)
        {
            return m_DrawBatchIndices.Contains(drawIndex);
        }

        public void Rebuild()
        {
            new RebuildDrawBatchIndices {
                Draws = m_DrawBatchIndices,
                DrawBatches = m_DrawBatches,
                DrawsByRange = m_DrawRangeBatches,
            }.Run();
            m_NeedsRebuild = false;
        }

        private static readonly List<Material> MaterialBuffer = new List<Material>(8);

        private DrawRangeIndex GetOrCreateDrawRangeIndex(in DrawRangeKey rangeKey)
        {
            if (!m_DrawRangeHash.TryGetValue(rangeKey, out DrawRangeIndex rangeIndex))
            {
                rangeIndex = m_DrawRangeFreeIndices.Length > 0 ? m_DrawRangeFreeIndices.Pop() : m_NextDrawRangeIndex++;
                if (rangeIndex >= m_DrawRangeKeys.Length)
                {
                    int newSize = math.max(m_DrawRangeKeys.Length * 2, rangeIndex + 1);
                    m_DrawRangeKeys.ResizeArraySafe(newSize);
                    m_DrawRangeBatches.Resize(newSize);
                }

                m_DrawRangeIndices.Add(rangeIndex);
                m_DrawRangeHash[rangeKey] = rangeIndex;
                m_DrawRangeKeys[rangeIndex] = rangeKey;
            }

            return rangeIndex;
        }

        public NativeArray<DrawDescriptor> BuildDrawDescriptors(
            TemplateIndex template,
            GameObject representativeRenderSource,
            TemplateOptions templateOptions,
            int lodIndex,
            Renderer renderer,
            Material detailBillboardMaterial,
            BatchDomainIndex batchDomainIndex,
            Allocator allocator)
        {
            if (renderer == null)
                return default;

            Mesh mesh = null;
            int startSubMeshIndex = 0;

            if (renderer is MeshRenderer meshRenderer)
            {
                MeshFilter meshFilter = renderer.GetComponent<MeshFilter>();
                if (meshFilter == null)
                {
                    Debug.LogWarning($"Flora: MeshFilter is null for representative render source: {representativeRenderSource} on renderer: {renderer.name}. Object will not be rendered.", representativeRenderSource);
                    return default;
                }

                mesh = meshFilter.sharedMesh;
                startSubMeshIndex = meshRenderer.subMeshStartIndex;
                renderer.GetSharedMaterials(MaterialBuffer);
            }
            else if (renderer is SkinnedMeshRenderer skinnedMeshRenderer)
            {
                mesh = skinnedMeshRenderer.sharedMesh;
                renderer.GetSharedMaterials(MaterialBuffer);
            }
            else if (renderer is BillboardRenderer billboardRenderer)
            {
                MaterialBuffer.Clear();
                if (detailBillboardMaterial)
                {
                    mesh = CullingUtility.GetTerrainDetailBillboardMesh();
                    MaterialBuffer.Add(detailBillboardMaterial);
                }
                else
                {
                    Debug.LogWarning("Flora: Billboards do not support DOTS instancing rendering. " +
                                     $"This object will not be rendered: {billboardRenderer.name}.", representativeRenderSource);
                    return default;
                }
            }

            if (mesh == null)
            {
                Debug.LogWarning($"Flora: Mesh is null for representative render source: {representativeRenderSource} on renderer: {renderer.name}. Object will not be rendered.", representativeRenderSource);
                return default;
            }

            if (MaterialBuffer.Count == 0)
            {
                Debug.LogWarning($"Flora: No materials found for representative render source: {representativeRenderSource} on renderer: {renderer.name}. Object will not be rendered.", representativeRenderSource);
                return default;
            }

            int subMeshCount = mesh.subMeshCount;
            DrawRangeKey rangeKey = new DrawRangeKey(renderer);
            if (rangeKey.MotionMode == MotionVectorGenerationMode.Object && (templateOptions & TemplateOptions.DisableMotionVectors) != 0)
                rangeKey.MotionMode = MotionVectorGenerationMode.Camera;

            int maxRenderableSubMeshCount = math.max(0, subMeshCount - startSubMeshIndex);
            var descriptors = new NativeList<DrawDescriptor>(math.min(maxRenderableSubMeshCount, MaterialBuffer.Count), Allocator.Temp);

            for (int materialIndex = 0; materialIndex < MaterialBuffer.Count; materialIndex++)
            {
                int submeshMaterialIndex = startSubMeshIndex + materialIndex;
                if (submeshMaterialIndex >= subMeshCount)
                {
                    Debug.LogWarning("Flora: Material count in the shared material list exceeds the renderable sub mesh range for the mesh. Object may be corrupted.", representativeRenderSource);
                    continue;
                }

                Material material = MaterialBuffer[materialIndex];
                if (material == null)
                {
                    Debug.LogWarning("Flora: Material in the shared materials list is null. Object will be partially rendered.", representativeRenderSource);
                    continue;
                }

                if (!material.shader.HasDOTSKeyword())
                {
                    Debug.LogWarning($"Flora: Material '{material.name}' does not support DOTS_INSTANCING_ON keyword. Object will be partially rendered.", material);
                    continue;
                }

                SubMeshDescriptor subMeshDesc = mesh.GetSubMesh(submeshMaterialIndex);

#if UNITY_6000_2_OR_NEWER
                if (mesh.lodCount > 1 && lodIndex >= 0)
                {
                    MeshLodRange meshLodRange = mesh.GetLod(submeshMaterialIndex, lodIndex);
                    subMeshDesc.indexStart += (int)meshLodRange.indexStart;
                    subMeshDesc.indexCount = (int)meshLodRange.indexCount;
                }
#endif

                bool supportsMotion = rangeKey.MotionMode != MotionVectorGenerationMode.Camera && material.FindPass("MotionVectors") != -1;
                bool supportsFadeKeyword = material.shader.HasLODFadeKeyword();

                IndirectStateFlags supportedStateFlags = IndirectStateFlags.HasFlippedWinding;
                if (supportsMotion)
                    supportedStateFlags |= IndirectStateFlags.HasMotion;
                if (supportsFadeKeyword)
                    supportedStateFlags |= IndirectStateFlags.HasFadeKeyword;

                descriptors.Add(new DrawDescriptor
                {
                    RangeKey = rangeKey,
                    BatchDomainIndex = batchDomainIndex,
                    MeshEntityId = mesh.GetEntityId(),
                    LodIndex = lodIndex,
                    SubMeshIndex = (ushort)submeshMaterialIndex,
                    MaterialEntityId = material.GetEntityId(),
                    Flags = BatchDrawCommandFlags.UseLegacyLightmapsKeyword | BatchDrawCommandFlags.LODCrossFadeValuePacked,
                    SupportedStateFlags = supportedStateFlags,
                    SupportedStateMask = DrawStateUtility.CreateStateMask(supportedStateFlags),
                    Topology = mesh.GetTopology(submeshMaterialIndex),
                    BaseVertex = (uint)subMeshDesc.baseVertex,
                    FirstIndex = (uint)subMeshDesc.indexStart,
                    IndexCount = (uint)subMeshDesc.indexCount,
                });
            }

            NativeArray<DrawDescriptor> result = new NativeArray<DrawDescriptor>(descriptors.Length, allocator);
            result.CopyFrom(descriptors.AsArray());
            descriptors.Dispose();
            return result;
        }

        public NativeArray<DrawBatchIndex> RegisterDrawDescriptors(NativeArray<DrawDescriptor> descriptors, Allocator allocator)
        {
            NativeArray<DrawBatchIndex> drawIds = new NativeArray<DrawBatchIndex>(descriptors.Length, allocator);
            for (int i = 0; i < descriptors.Length; i++)
                drawIds[i] = RegisterDraw(descriptors[i]);
            return drawIds;
        }

        public NativeArray<DrawBatchIndex> RegisterDraws(
            TemplateIndex template,
            GameObject representativeRenderSource,
            TemplateOptions templateOptions,
            int lodIndex,
            Renderer renderer,
            Material detailBillboardMaterial,
            BatchDomainIndex batchDomainIndex,
            Allocator allocator)
        {
            NativeArray<DrawDescriptor> descriptors = BuildDrawDescriptors(
                template,
                representativeRenderSource,
                templateOptions,
                lodIndex,
                renderer,
                detailBillboardMaterial,
                batchDomainIndex,
                Allocator.Temp);

            if (!descriptors.IsCreated)
                return default;

            NativeArray<DrawBatchIndex> drawIds = RegisterDrawDescriptors(descriptors, allocator);
            descriptors.Dispose();

            return drawIds;
        }

        public void ReleaseDraw(DrawBatchIndex drawIndex)
        {
            if (m_DrawBatchIndices.Contains(drawIndex))
            {
                int refCount = m_DrawBatchRefCounts[drawIndex];
                Assert.IsTrue(refCount > 0, $"Draw {drawIndex} refcount underflow.");
                refCount--;
                m_DrawBatchRefCounts[drawIndex] = refCount;
                if (refCount == 0)
                {
                    DrawBatchKey drawKey = m_DrawBatches[drawIndex].Key;
                    m_DrawBatchHash.Remove(drawKey);
                    m_DrawBatches[drawIndex] = default;
                    m_DrawBatchChunks[drawIndex].Clear();
                    m_DrawBatchIndices.Remove(drawIndex);
                    m_DrawBatchFreeIndices.Add(drawIndex);

                    BatchAssetManager.UnregisterMesh(drawKey.MeshID);
                    BatchAssetManager.UnregisterMaterial(drawKey.MaterialID);
                    m_NeedsRebuild = true;
                }
            }
        }

        public void ReleaseDraws(NativeArray<DrawBatchIndex> drawIndices)
        {
            for (int i = 0; i < drawIndices.Length; i++)
                ReleaseDraw(drawIndices[i]);
        }

        public DrawBatchIndex RegisterDraw(in DrawDescriptor descriptor)
        {
            DrawRangeIndex rangeIndex = GetOrCreateDrawRangeIndex(descriptor.RangeKey);

            DrawBatchKey key = new DrawBatchKey
            {
                RangeIndex = rangeIndex,
                BatchDomainIndex = descriptor.BatchDomainIndex,
                MeshID = BatchMeshID.Null,
                MeshEntityId = descriptor.MeshEntityId,
                LodIndex = descriptor.LodIndex,
                ActiveMeshLod = descriptor.ActiveMeshLod,
                SubMeshIndex = descriptor.SubMeshIndex,
                Flags = descriptor.Flags,
                MaterialID = BatchMaterialID.Null,
                MaterialEntityId = descriptor.MaterialEntityId,
                SupportedStateFlags = descriptor.SupportedStateFlags,
                SupportedStateMask = descriptor.SupportedStateMask,
                Topology = descriptor.Topology,
                BaseVertex = descriptor.BaseVertex,
                FirstIndex = descriptor.FirstIndex,
                IndexCount = descriptor.IndexCount,
            };

            if (m_DrawBatchHash.TryGetValue(key, out DrawBatchIndex existingDrawIndex))
            {
                m_DrawBatchRefCounts[existingDrawIndex] = m_DrawBatchRefCounts[existingDrawIndex] + 1;
                return existingDrawIndex;
            }

            Mesh mesh = descriptor.MeshEntityId.ToObject<Mesh>();
            Material material = descriptor.MaterialEntityId.ToObject<Material>();
            if (mesh == null || material == null)
                return DrawBatchIndex.None;

            BatchMeshID meshID = BatchAssetManager.RegisterMesh(mesh);
            BatchMaterialID materialID = BatchAssetManager.RegisterMaterial(material);
            key.MeshID = meshID;
            key.MaterialID = materialID;

            int drawIndex = m_DrawBatchFreeIndices.Length > 0 ? m_DrawBatchFreeIndices.Pop() : m_NextDrawBatchIndex++;
            if (drawIndex >= m_DrawBatches.Length)
            {
                int newSize = math.max(m_DrawBatches.Length * 2, drawIndex + 1);
                m_DrawBatches.ResizeArraySafe(newSize);
                m_DrawBatchRefCounts.ResizeArraySafe(newSize);
                m_DrawBatchRangeIndices.ResizeArraySafe(newSize);
                m_DrawBatchChunks.Resize(newSize);
            }

            m_DrawBatchIndices.Add(drawIndex);

            DrawBatch drawBatch = new DrawBatch
            {
                Key = key,
                KeyHash = key.GetHashCode(),
                MeshInfo = new DrawMeshInfo
                {
                    Topology = descriptor.Topology,
                    BaseVertex = descriptor.BaseVertex,
                    FirstIndex = descriptor.FirstIndex,
                    IndexCount = descriptor.IndexCount,
                },
            };

            m_DrawBatches[drawIndex] = drawBatch;
            m_DrawBatchRefCounts[drawIndex] = 1;
            m_DrawBatchRangeIndices[drawIndex] = rangeIndex;
            EnsureDrawBatchHashCapacity();
            m_DrawBatchHash.Add(key, drawIndex);
            m_NeedsRebuild = true;

            return drawIndex;
        }

        private void EnsureDrawBatchHashCapacity(int additionalEntries = 1)
        {
            int requiredCapacity = m_DrawBatchHash.Count() + additionalEntries;
            if (requiredCapacity <= m_DrawBatchHash.Capacity)
                return;

            m_DrawBatchHash.Capacity = math.max(requiredCapacity, m_DrawBatchHash.Capacity * 2);
        }

        [BurstCompile]
        private struct AddChunksToDrawsJob : IJob
        {
            [ReadOnly] public NativeArray<DrawBatchIndex> DrawIDs;
            [ReadOnly] public NativeArray<CullingChunkIndex> ChunksToAdd;

            public NativeBufferArray<CullingChunkIndex> DrawChunks;

            public void Execute()
            {
                for (int i = 0; i < DrawIDs.Length; i++)
                {
                    DrawBatchIndex drawIndex = DrawIDs[i];
                    DrawChunks[drawIndex].AddRange(ChunksToAdd);
                }
            }
        }

        [BurstCompile]
        private struct RebuildDrawBatchIndices : IJob
        {
            [ReadOnly] public NativeBitSet Draws;
            [ReadOnly] public NativeArray<DrawBatch> DrawBatches;

            public NativeBufferArray<DrawBatchIndex> DrawsByRange;

            public void Execute()
            {
                for (int i = 0; i < DrawsByRange.Length; i++)
                    DrawsByRange[i].Clear();

                foreach (DrawBatchIndex drawId in Draws)
                {
                    DrawBatch drawBatch = DrawBatches[drawId];
                    DrawRangeIndex rangeIndex = drawBatch.Key.RangeIndex;
                    NativeBuffer<DrawBatchIndex> rangeDrawIndices = DrawsByRange[rangeIndex];
                    rangeDrawIndices.Add(drawId);
                }
            }
        }
    }
}
