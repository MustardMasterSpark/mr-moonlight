// Copyright © Magnetic Arcade. All Rights Reserved.

#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace MA.Flora
{
    internal sealed class FloraDiagnosticsSnapshot
    {
        public bool IsSystemCreated;
        public bool IsSystemActive;
        public bool IsRenderingEnabled;
        public bool HasCullingGridDetails;
        public int RegisteredInstanceCount;
        public int SourceCount;
        public int TemplateCount;
        public int ArchetypeCount;
        public int ChunkCount;
        public int DrawCount;
        public FloraDiagnosticsMemory Memory { get; } = new();
        public List<FloraDiagnosticsSource> Sources { get; } = new();
        public List<FloraDiagnosticsTemplate> Templates { get; } = new();
        public List<FloraDiagnosticsArchetype> Archetypes { get; } = new();
        public List<FloraDiagnosticsInstanceChunk> InstanceChunks { get; } = new();
        public List<FloraDiagnosticsDraw> Draws { get; } = new();
        public List<FloraDiagnosticsBatchDomain> BatchDomains { get; } = new();
        public List<FloraDiagnosticsCullingChunk> CullingChunks { get; } = new();
        public List<FloraDiagnosticsGraphicsBuffer> GraphicsBuffers { get; } = new();
        public DateTime CapturedAt { get; internal set; } = DateTime.Now;
    }

    [Flags]
    internal enum FloraDiagnosticsCaptureFlags
    {
        Default = 0,
        IncludeCullingGrid = 1 << 0,
    }

    internal sealed class FloraDiagnosticsSource
    {
        public int Index;
        public string Name;
        public string Type;
        public string Kind;
        public GameObject IdentitySource;
        public GameObject RenderSource;
        public LODGroup LodGroup;
        public FloraAdditionalRendererSettings AdditionalSettings;
        public Object PrimaryComponent;
        public string Scene;
        public int Layer;
        public int TemplateCount;
        public int InstanceCount;
        public int ComponentCount;
        public int RendererCount;
        public int RefCount;
        public int LightmapIndex;
        public string Flags;
        public List<int> TemplateIndices { get; } = new();
        public List<Object> Components { get; } = new();
        public List<Object> Renderers { get; } = new();
    }

    internal sealed class FloraDiagnosticsTemplate
    {
        public int Index;
        public string Name;
        public string Type;
        public string Flags;
        public GameObject RepresentativeRenderSource;
        public int SourceCount;
        public int InstanceCount;
        public int ChunkCount;
        public int CullingChunkCount;
        public int LodCount;
        public int DrawCount;
        public long TriangleCount;
        public long VertexCount;
        public int MaterialCount;
        public int Layer;
        public float MaxRenderDistance;
        public float MaxShadowDistance;
        public int BatchDomainIndex;
        public List<int> SourceIndices { get; } = new();
        public List<int> DrawIndices { get; } = new();
        public List<FloraDiagnosticsLod> Lods { get; } = new();
    }

    internal sealed class FloraDiagnosticsLod
    {
        public int Index;
        public float Height;
        public float TransitionHeight;
        public int CameraDrawCount;
        public int ShadowDrawCount;
        public long TriangleCount;
        public long VertexCount;
    }

    internal sealed class FloraDiagnosticsArchetype
    {
        public int Index;
        public int TemplateIndex;
        public string Name;
        public string Tags;
        public int InstanceCount;
        public int ChunkCount;
        public int ChunkCapacity;
        public int Layer;
        public string Scene;
        public int LightmapIndex;
        public string Flags;
        public Object Owner;
    }

    internal sealed class FloraDiagnosticsInstanceChunk
    {
        public int Index;
        public int ArchetypeIndex;
        public int TemplateIndex;
        public int BatchDomainIndex;
        public int InstanceCount;
        public int Capacity;
        public int InstanceOffset;
        public string Flags;
    }

    internal sealed class FloraDiagnosticsDraw
    {
        public int Index;
        public int RangeIndex;
        public int BatchDomainIndex;
        public string Name;
        public Mesh Mesh;
        public Material Material;
        public int LodIndex;
        public int ActiveMeshLod;
        public int SubMeshIndex;
        public MeshTopology Topology;
        public long IndexCount;
        public long PrimitiveCount;
        public long TriangleCount;
        public long VertexCount;
        public int CullingChunkCount;
        public string Flags;
        public int Layer;
    }

    internal sealed class FloraDiagnosticsBatchDomain
    {
        public int Index;
        public string BatchId;
        public int InstanceCapacity;
        public long LengthInBytes;
        public long BaseAddress;
        public int PropertyCount;
        public int OverriddenPropertyCount;
        public string Flags;
        public List<FloraDiagnosticsBatchProperty> Properties { get; } = new();
    }

    internal sealed class FloraDiagnosticsBatchProperty
    {
        public int NameID;
        public string DisplayName;
        public int TypeSizeInBytes;
        public int ElementCount;
        public long SizeInBytes;
        public long AlignedSizeInBytes;
        public long Address;
        public uint MetadataValue;
        public bool IsOverridden;
        public bool IsPerInstance;
    }

    internal sealed class FloraDiagnosticsMemory
    {
        public long InstanceBufferBytes;
        public long BatchDomainLayoutBytes;
        public int DomainCount;
        public int TemplateDataStride;
        public long TemplateDataBytes;
        public int ArchetypeDataStride;
        public long ArchetypeDataBytes;
        public long GraphicsBufferBytes;
        public int GraphicsBufferCount;
    }

    internal sealed class FloraDiagnosticsCullingChunk
    {
        public int Index;
        public int ArchetypeIndex;
        public int TemplateIndex;
        public int BatchDomainIndex;
        public int InstanceCount;
        public CellIndex CellIndex;
        public int CellLevel;
        public Vector3Int CellCoordinates;
        public BlockIndex BlockIndex;
        public int BlockLevel;
        public Vector3Int BlockCoordinates;
        public int CellIndexInBlock;
        public string CellLocation;
        public string BlockLocation;
        public Vector3 CellCenter;
        public float CellSize;
        public Bounds CellBounds;
        public bool IsValid => CellIndex > 0 && BlockIndex > 0;
    }

    internal sealed class FloraDiagnosticsGraphicsBuffer
    {
        public int Index;
        public string StoreType;
        public string Name;
        public long SizeInBytes;
        public int Count;
        public int Stride;
        public string Target;

        public string DisplayName => string.IsNullOrEmpty(Name) ? $"{StoreType} Buffer {Index}" : Name;
    }

    internal static class FloraDiagnostics
    {
        private static readonly List<GraphicsBufferStore.DebugBufferInfo> s_GraphicsBufferInfos = new();

        public static FloraDiagnosticsSnapshot CaptureSnapshot(FloraDiagnosticsCaptureFlags flags = FloraDiagnosticsCaptureFlags.Default)
        {
            var snapshot = new FloraDiagnosticsSnapshot
            {
                IsSystemCreated = FloraSystem.Exists,
                IsSystemActive = FloraSystem.Active,
                CapturedAt = DateTime.Now,
            };

            FloraSystem system = FloraSystem.Instance;
            if (system == null)
                return snapshot;

            snapshot.IsRenderingEnabled = system.RenderingEnabled;
            snapshot.RegisteredInstanceCount = system.RegisteredInstanceCount;

            system.InstanceManager.ValueRW.SyncJobsForMainThread();

            DrawManager drawManager = system.DrawManager.ValueRO;
            system.TemplateManager.ValueRO.AppendDiagnostics(snapshot, drawManager);
            system.InstanceManager.ValueRO.AppendDiagnostics(snapshot);
            drawManager.AppendDiagnostics(snapshot);

            snapshot.SourceCount = snapshot.Sources.Count;
            snapshot.TemplateCount = snapshot.Templates.Count;
            snapshot.ArchetypeCount = snapshot.Archetypes.Count;
            snapshot.DrawCount = snapshot.Draws.Count;

            system.InstanceBuffer.ValueRO.AppendDiagnostics(snapshot);
            if ((flags & FloraDiagnosticsCaptureFlags.IncludeCullingGrid) != 0)
            {
                system.CullingGrid.ValueRW.AppendDiagnostics(snapshot);
                snapshot.HasCullingGridDetails = true;
            }

            AppendGraphicsBuffers(snapshot);

            return snapshot;
        }

        private static void AppendGraphicsBuffers(FloraDiagnosticsSnapshot snapshot)
        {
            GraphicsBufferStore.GetDebugBufferInfos(s_GraphicsBufferInfos);
            long totalBytes = 0;

            for (int i = 0; i < s_GraphicsBufferInfos.Count; i++)
            {
                GraphicsBufferStore.DebugBufferInfo info = s_GraphicsBufferInfos[i];
                totalBytes += info.Descriptor.SizeInBytes;

                snapshot.GraphicsBuffers.Add(new FloraDiagnosticsGraphicsBuffer
                {
                    Index = i + 1,
                    StoreType = info.StoreType.ToString(),
#if UNITY_EDITOR || DEBUG_BUFFER_NAMES
                    Name = info.DebugName,
#else
                    Name = string.Empty,
#endif
                    SizeInBytes = info.Descriptor.SizeInBytes,
                    Count = info.Descriptor.Length,
                    Stride = info.Descriptor.Stride,
                    Target = info.Descriptor.Target.ToString(),
                });
            }

            snapshot.Memory.GraphicsBufferBytes = totalBytes;
            snapshot.Memory.GraphicsBufferCount = s_GraphicsBufferInfos.Count;
        }
    }

    internal static class FloraDiagnosticsUtility
    {
        private static readonly Dictionary<int, string> s_ShaderPropertyNames = new()
        {
            { ShaderPropertyId.unity_BaseColor, "unity_BaseColor" },
            { ShaderPropertyId.unity_SpecCube0_HDR, "unity_SpecCube0_HDR" },
            { ShaderPropertyId.unity_ObjectToWorld, "unity_ObjectToWorld" },
            { ShaderPropertyId.unity_WorldToObject, "unity_WorldToObject" },
            { ShaderPropertyId.unity_MatrixPreviousM, "unity_MatrixPreviousM" },
            { ShaderPropertyId.unity_MatrixPreviousMI, "unity_MatrixPreviousMI" },
            { ShaderPropertyId.flora_RandomID, "flora_RandomID" },
            { ShaderPropertyId.flora_VariationColor, "flora_VariationColor" },
            { ShaderPropertyId.unity_LightmapST, "unity_LightmapST" },
            { ShaderPropertyId.unity_SHCoefficients, "unity_SHCoefficients" },
            { ShaderPropertyId.unity_EntityId, "unity_EntityId" },
        };

        public static long GetPrimitiveCount(MeshTopology topology, long indexCount)
        {
            if (indexCount <= 0)
                return 0;

            return topology == MeshTopology.Triangles ? indexCount / 3 : indexCount;
        }

        public static long GetMeshVertexCount(Mesh mesh)
        {
            return mesh ? mesh.vertexCount : 0;
        }

        public static string FormatBytes(long bytes)
        {
            if (bytes < 1024)
                return $"{bytes} B";

            string[] suffixes = { "KB", "MB", "GB", "TB" };
            double value = bytes;
            int suffixIndex = -1;
            do
            {
                value /= 1024.0;
                suffixIndex++;
            } while (value >= 1024.0 && suffixIndex < suffixes.Length - 1);

            return $"{value:0.0} {suffixes[suffixIndex]}";
        }

        public static long Align16(long value) => (value + 15L) & ~15L;

        public static string GetShaderPropertyDisplayName(int nameId)
            => s_ShaderPropertyNames.TryGetValue(nameId, out string name) ? name : $"NameID {nameId}";

        public static string FormatArchetypeDifferentiators(FloraDiagnosticsArchetype archetype)
        {
            var parts = new List<string>();
            if (!string.IsNullOrEmpty(archetype.Scene))
                parts.Add($"Scene: {archetype.Scene}");
            if (archetype.Owner)
                parts.Add($"Owner: {archetype.Owner.name}");
            parts.Add($"Layer: {archetype.Layer}");
            if (archetype.LightmapIndex >= 0)
                parts.Add($"Lightmap: {archetype.LightmapIndex}");
            if (!string.IsNullOrEmpty(archetype.Tags))
                parts.Add($"Tags: {archetype.Tags}");
            if (!string.IsNullOrEmpty(archetype.Flags))
                parts.Add($"Flags: {archetype.Flags}");

            return parts.Count == 0 ? "No differentiators captured." : string.Join(" | ", parts);
        }

        public static string ObjectName(Object obj, string fallback)
        {
            return obj ? obj.name : fallback;
        }

        public static string ClassifySource(GameObject identitySource, GameObject renderSource, Object primaryComponent)
        {
            if (primaryComponent is FloraInstanceContainer)
                return "Container";

            if (primaryComponent is FloraInstanceRenderer)
                return "Scene Renderer";

            if (identitySource && !identitySource.scene.IsValid())
                return "Prefab";

            if (identitySource || renderSource)
                return "Scene Object";

            return "Unknown";
        }
    }

    internal unsafe partial struct TemplateManager
    {
        internal readonly void AppendDiagnostics(FloraDiagnosticsSnapshot snapshot, DrawManager drawManager)
        {
            foreach (SourceRecordIndex sourceRecordIndex in m_SourceRecordAllocated.AsType<SourceRecordIndex>())
            {
                SourceRecord record = m_SourceRecords[sourceRecordIndex];
                GameObject identitySource = record.IdentitySourceId.ToObject<GameObject>();
                GameObject renderSource = record.RenderSourceId.ToObject<GameObject>();
                Object primaryComponent = ResolvePrimarySourceComponent(sourceRecordIndex);
                var source = new FloraDiagnosticsSource
                {
                    Index = sourceRecordIndex.Index,
                    Name = FloraDiagnosticsUtility.ObjectName(identitySource, $"Source {sourceRecordIndex.Index}"),
                    Type = primaryComponent != null ? primaryComponent.GetType().Name : renderSource ? renderSource.GetType().Name : "Source",
                    Kind = FloraDiagnosticsUtility.ClassifySource(identitySource, renderSource, primaryComponent),
                    IdentitySource = identitySource,
                    RenderSource = renderSource,
                    LodGroup = record.LodGroupId.ToObject<LODGroup>(),
                    AdditionalSettings = record.AdditionalSettingsId.ToObject<FloraAdditionalRendererSettings>(),
                    PrimaryComponent = primaryComponent,
                    Scene = renderSource && renderSource.scene.IsValid() ? renderSource.scene.path : string.Empty,
                    Layer = renderSource ? renderSource.layer : identitySource ? identitySource.layer : 0,
                    TemplateCount = m_SourceRecordTemplates[sourceRecordIndex].Length,
                    InstanceCount = m_SourceRecordInstances[sourceRecordIndex].Length,
                    ComponentCount = m_SourceRecordComponentIds[sourceRecordIndex].Length,
                    RendererCount = m_SourceRecordRendererIds[sourceRecordIndex].Length,
                    RefCount = record.RefCount,
                    LightmapIndex = record.LightmapIndex,
                    Flags = record.LodGroupId.IsValid() ? "LODGroup" : "Renderer",
                };

                AppendObjects(m_SourceRecordComponentIds[sourceRecordIndex], source.Components);
                AppendObjects(m_SourceRecordRendererIds[sourceRecordIndex], source.Renderers);
                AppendTemplateIndices(m_SourceRecordTemplates[sourceRecordIndex], source.TemplateIndices);
                snapshot.Sources.Add(source);
            }

            foreach (TemplateIndex templateIndex in m_TemplateAllocated.AsType<TemplateIndex>())
            {
                FloraDiagnosticsTemplate template = BuildTemplateDiagnostics(templateIndex, drawManager);
                snapshot.Templates.Add(template);
                snapshot.ChunkCount += template.ChunkCount;
            }
        }

        private readonly FloraDiagnosticsTemplate BuildTemplateDiagnostics(TemplateIndex templateIndex, DrawManager drawManager)
        {
            GameObject representativeSource = m_TemplateRepresentativeRenderSourceIds[templateIndex].ToObject<GameObject>();
            var template = new FloraDiagnosticsTemplate
            {
                Index = templateIndex.Index,
                Name = FloraDiagnosticsUtility.ObjectName(representativeSource, $"Template {templateIndex.Index}"),
                Type = templateIndex.Type.ToString(),
                Flags = templateIndex.Flags.ToString(),
                RepresentativeRenderSource = representativeSource,
                SourceCount = m_TemplateSourceRecords[templateIndex].Length,
                ChunkCount = m_Chunks[templateIndex].Length,
                CullingChunkCount = m_CullingChunks[templateIndex].Length,
                LodCount = templateIndex.LodCount,
                Layer = TemplateStore.Data[templateIndex.Index].Layer,
                MaxRenderDistance = templateIndex.MaxRenderDistance,
                MaxShadowDistance = templateIndex.MaxShadowDistance,
                BatchDomainIndex = templateIndex.BatchDomainIndex.Index,
            };

            var materialIds = new HashSet<EntityId>();
            var drawIndices = m_RegisteredDrawIndices[templateIndex];
            template.DrawCount = drawIndices.Length;
            for (int i = 0; i < drawIndices.Length; i++)
            {
                template.DrawIndices.Add(drawIndices[i]);
                if (!drawManager.TryGetDiagnosticsDraw(drawIndices[i], out FloraDiagnosticsDraw draw))
                    continue;

                template.TriangleCount += draw.TriangleCount;
                template.VertexCount += draw.VertexCount;
                if (draw.Material)
                    materialIds.Add(draw.Material.GetEntityId());
            }

            template.MaterialCount = materialIds.Count;

            var sourceRecords = m_TemplateSourceRecords[templateIndex];
            for (int i = 0; i < sourceRecords.Length; i++)
            {
                template.SourceIndices.Add(sourceRecords[i].Index);
                template.InstanceCount += m_SourceRecordInstances[sourceRecords[i]].Length;
            }

            for (int lodIndex = 0; lodIndex < template.LodCount; lodIndex++)
            {
                FloraDiagnosticsLod lod = BuildLodDiagnostics(templateIndex, drawManager, lodIndex);
                template.Lods.Add(lod);
            }

            return template;
        }

        private readonly FloraDiagnosticsLod BuildLodDiagnostics(TemplateIndex templateIndex, DrawManager drawManager, int lodIndex)
        {
            var lod = new FloraDiagnosticsLod
            {
                Index = lodIndex,
                Height = TemplateStore.Data[templateIndex.Index].LODHeights[lodIndex],
                TransitionHeight = TemplateStore.Data[templateIndex.Index].LODTransitionHeights[lodIndex],
            };

            var cameraDraws = m_CameraDrawIndicesPerLod[templateIndex.Index * CullingConstants.MaxLodCount + lodIndex];
            lod.CameraDrawCount = cameraDraws.Length;
            for (int i = 0; i < cameraDraws.Length; i++)
            {
                if (!drawManager.TryGetDiagnosticsDraw(cameraDraws[i], out FloraDiagnosticsDraw draw))
                    continue;

                lod.TriangleCount += draw.TriangleCount;
                lod.VertexCount += draw.VertexCount;
            }

            var shadowDraws = m_ShadowDrawIndicesPerLod[templateIndex.Index * CullingConstants.MaxLodCount + lodIndex];
            lod.ShadowDrawCount = shadowDraws.Length;
            return lod;
        }

        private readonly Object ResolvePrimarySourceComponent(SourceRecordIndex sourceRecordIndex)
        {
            NativeBuffer<EntityId> componentIds = m_SourceRecordComponentIds[sourceRecordIndex];
            for (int i = 0; i < componentIds.Length; i++)
            {
                Object obj = componentIds[i].ToObject();
                if (obj is FloraInstanceContainer || obj is FloraInstanceRenderer)
                    return obj;
            }

            return componentIds.Length > 0 ? componentIds[0].ToObject() : null;
        }

        private static void AppendObjects(NativeBuffer<EntityId> ids, List<Object> objects)
        {
            for (int i = 0; i < ids.Length; i++)
            {
                Object obj = ids[i].ToObject();
                if (obj)
                    objects.Add(obj);
            }
        }

        private static void AppendTemplateIndices(NativeBuffer<TemplateIndex> ids, List<int> templates)
        {
            for (int i = 0; i < ids.Length; i++)
                templates.Add(ids[i].Index);
        }
    }

    internal partial struct InstanceManager
    {
        internal readonly void AppendDiagnostics(FloraDiagnosticsSnapshot snapshot)
        {
            foreach (ArchetypeIndex archetypeIndex in m_ArchetypeAllocated.AsType<ArchetypeIndex>())
            {
                ArchetypeKey key = archetypeIndex.Key;
                var archetype = new FloraDiagnosticsArchetype
                {
                    Index = archetypeIndex.Index,
                    TemplateIndex = key.Template.Index,
                    Name = $"Archetype {archetypeIndex.Index}",
                    Tags = key.Tags.ToString(),
                    InstanceCount = archetypeIndex.InstanceCount,
                    ChunkCount = archetypeIndex.ChunkCount,
                    ChunkCapacity = archetypeIndex.ChunkCount * ChunkCapacity,
                    Layer = key.Layer,
                    Scene = key.Scene.IsValid() ? key.Scene.name : string.Empty,
                    LightmapIndex = key.LightmapIndex,
                    Flags = key.IsEnabled ? "Enabled" : "Disabled",
                    Owner = key.ContainerEntity.ToObject(),
                };

                snapshot.Archetypes.Add(archetype);

                NativeBuffer<ChunkIndex> chunks = m_ArchetypeChunks[archetypeIndex];
                for (int i = 0; i < chunks.Length; i++)
                {
                    ChunkIndex chunk = chunks[i];
                    var flags = new List<string>();
                    if (m_ChunkEnabled.Contains(chunk))
                        flags.Add("Enabled");
                    if (m_ChunkStatic.Contains(chunk))
                        flags.Add("Static");
                    if (m_ChunkDynamic.Contains(chunk))
                        flags.Add("Dynamic");
                    if (m_ChunkHasProbes.Contains(chunk))
                        flags.Add("Light Probes");
                    if (m_ChunkHasRandomValue.Contains(chunk))
                        flags.Add("Random ID");
                    if (m_ChunkHasColorVariation.Contains(chunk))
                        flags.Add("Variation Color");
                    if (m_ChunkHasLightmapST.Contains(chunk))
                        flags.Add("Lightmap ST");
                    if (m_PendingSpatialUpdates.Contains(chunk))
                        flags.Add("Pending Spatial");
                    if (m_PendingInstanceUpload.Contains(chunk))
                        flags.Add("Pending Instance Upload");
                    if (m_PendingTransformUpload.Contains(chunk))
                        flags.Add("Pending Transform Upload");
                    if (m_PendingVariationColorUpload.Contains(chunk))
                        flags.Add("Pending Color Upload");
                    if (m_PendingLightmapSTUpload.Contains(chunk))
                        flags.Add("Pending Lightmap Upload");

                    snapshot.InstanceChunks.Add(new FloraDiagnosticsInstanceChunk
                    {
                        Index = chunk.Index,
                        ArchetypeIndex = archetypeIndex.Index,
                        TemplateIndex = key.Template.Index,
                        BatchDomainIndex = key.Template.BatchDomainIndex.Index,
                        InstanceCount = chunk.Count,
                        Capacity = ChunkIndex.Capacity,
                        InstanceOffset = chunk.AsInstanceOffset(),
                        Flags = flags.Count == 0 ? "None" : string.Join(", ", flags),
                    });
                }
            }
        }
    }

    internal partial struct DrawManager
    {
        internal readonly void AppendDiagnostics(FloraDiagnosticsSnapshot snapshot)
        {
            foreach (DrawBatchIndex drawIndex in m_DrawBatchIndices.AsType<DrawBatchIndex>())
            {
                if (TryGetDiagnosticsDraw(drawIndex, out FloraDiagnosticsDraw draw))
                    snapshot.Draws.Add(draw);
            }
        }

        internal readonly bool TryGetDiagnosticsDraw(DrawBatchIndex drawIndex, out FloraDiagnosticsDraw draw)
        {
            draw = null;
            if (!m_DrawBatchIndices.Contains(drawIndex))
                return false;

            DrawBatch batch = m_DrawBatches[drawIndex];
            DrawBatchKey key = batch.Key;
            Mesh mesh = key.MeshEntityId.ToObject<Mesh>();
            Material material = key.MaterialEntityId.ToObject<Material>();
            long indexCount = batch.MeshInfo.IndexCount;
            long primitiveCount = FloraDiagnosticsUtility.GetPrimitiveCount(batch.MeshInfo.Topology, indexCount);

            draw = new FloraDiagnosticsDraw
            {
                Index = drawIndex,
                RangeIndex = key.RangeIndex,
                BatchDomainIndex = key.BatchDomainIndex.Index,
                Name = mesh ? $"{mesh.name} / {(material ? material.name : "Material")}" : $"Draw {drawIndex}",
                Mesh = mesh,
                Material = material,
                LodIndex = key.LodIndex,
                ActiveMeshLod = key.ActiveMeshLod,
                SubMeshIndex = key.SubMeshIndex,
                Topology = batch.MeshInfo.Topology,
                IndexCount = indexCount,
                PrimitiveCount = primitiveCount,
                TriangleCount = batch.MeshInfo.Topology == MeshTopology.Triangles ? primitiveCount : 0,
                VertexCount = FloraDiagnosticsUtility.GetMeshVertexCount(mesh),
                CullingChunkCount = m_DrawBatchChunks[drawIndex].Length,
                Flags = key.Flags.ToString(),
                Layer = m_DrawRangeKeys[key.RangeIndex].Layer,
            };

            return true;
        }
    }

    internal partial struct InstanceBuffer
    {
        internal readonly void AppendDiagnostics(FloraDiagnosticsSnapshot snapshot)
        {
            snapshot.Memory.InstanceBufferBytes = AllocatedSizeInBytes;
            snapshot.Memory.DomainCount = DomainCount;

            foreach (int domainIndex in m_AllocatedDomains)
            {
                BatchDomainLayout layout = m_DomainLayouts[domainIndex];
                BatchID batchID = m_DomainBatches[domainIndex];
                var domain = new FloraDiagnosticsBatchDomain
                {
                    Index = domainIndex,
                    BatchId = batchID == BatchID.Null ? "Null" : batchID.value.ToString(),
                    InstanceCapacity = layout.IsCreated ? layout.InstanceCapacity : 0,
                    LengthInBytes = layout.IsCreated ? layout.LengthInBytes : 0,
                    BaseAddress = layout.IsCreated ? layout.BaseAddress : 0,
                    PropertyCount = layout.IsCreated ? layout.Properties.Length : 0,
                    Flags = m_DomainDescriptors[domainIndex].IsCreated ? $"Components {m_DomainDescriptors[domainIndex].ComponentCount}" : "Unbuilt",
                };

                if (layout.IsCreated)
                {
                    for (int i = 0; i < layout.Properties.Length; i++)
                    {
                        BatchPropertyInfo property = layout.Properties[i];
                        MetadataValue metadata = layout.MetadataValues[i];
                        if (property.IsOverriden)
                            domain.OverriddenPropertyCount++;

                        int elementCount = property.IsPerInstance ? layout.InstanceCapacity : 1;
                        long size = (long)property.TypeSizeInBytes * elementCount;
                        domain.Properties.Add(new FloraDiagnosticsBatchProperty
                        {
                            NameID = property.NameID,
                            DisplayName = FloraDiagnosticsUtility.GetShaderPropertyDisplayName(property.NameID),
                            TypeSizeInBytes = property.TypeSizeInBytes,
                            ElementCount = elementCount,
                            SizeInBytes = property.IsOverriden ? size : 0,
                            AlignedSizeInBytes = property.IsOverriden ? FloraDiagnosticsUtility.Align16(size) : 0,
                            Address = property.IsOverriden ? metadata.Address() : 0,
                            MetadataValue = metadata.Value,
                            IsOverridden = property.IsOverriden,
                            IsPerInstance = property.IsPerInstance,
                        });
                    }

                    snapshot.Memory.BatchDomainLayoutBytes += layout.LengthInBytes;
                }

                snapshot.BatchDomains.Add(domain);
            }

            snapshot.Memory.TemplateDataStride = UnsafeUtility.SizeOf<TemplateData>();
            snapshot.Memory.TemplateDataBytes = (long)snapshot.TemplateCount * snapshot.Memory.TemplateDataStride;
            snapshot.Memory.ArchetypeDataStride = UnsafeUtility.SizeOf<PackedArchetypeData>();
            snapshot.Memory.ArchetypeDataBytes = (long)snapshot.ChunkCount * snapshot.Memory.ArchetypeDataStride;
        }
    }

    internal partial struct CullingGrid
    {
        internal void AppendDiagnostics(FloraDiagnosticsSnapshot snapshot)
        {
            m_PreDispatchHandle.Complete();

            foreach (int chunkIndex in m_ChunkAllocated)
            {
                var chunk = new CullingChunkIndex(chunkIndex);
                if (chunk <= 0 || chunk >= m_ChunkCount.Length)
                    continue;

                CellIndex cellIndex = m_ChunkCell[chunk];
                CellLocation cellLocation = CellLocation.None;
                Bounds cellBounds = default;
                Vector3 cellCenter = default;
                Vector3Int cellCoordinates = default;
                int cellLevel = -1;
                BlockIndex blockIndex = BlockIndex.None;
                int blockLevel = -1;
                int cellIndexInBlock = -1;
                string blockLocationText = "None";
                float cellSize = 0f;
                Vector3Int blockCoordinates = default;

                if (cellIndex != CellIndex.None && cellIndex.BlockIndex.Index >= 0 && cellIndex.BlockIndex.Index < m_BlockLocations.Length)
                {
                    BlockLocation blockLocation = m_BlockLocations[cellIndex.BlockIndex];
                    if (blockLocation.IsValid())
                    {
                        cellLocation = CellLocation.FromBlock(blockLocation, cellIndex.IndexInBlock);
                        AABB aabb = cellLocation.AABB;
                        cellBounds = aabb.ToBounds();
                        cellCenter = cellBounds.center;
                        cellCoordinates = new Vector3Int(cellLocation.Coords.x, cellLocation.Coords.y, cellLocation.Coords.z);
                        cellLevel = cellLocation.Level;
                        blockIndex = cellIndex.BlockIndex;
                        blockLevel = blockLocation.Level;
                        blockCoordinates = new Vector3Int(blockLocation.Coords.x, blockLocation.Coords.y, blockLocation.Coords.z);
                        blockLocationText = blockLocation.ToString();
                        cellIndexInBlock = cellIndex.IndexInBlock;
                        cellSize = cellLocation.CellSize;
                    }
                }

                ArchetypeIndex archetype = m_ChunkArchetype[chunk];
                TemplateIndex template = archetype != ArchetypeIndex.None ? archetype.Key.Template : TemplateIndex.None;
                BatchDomainIndex batchDomain = m_ChunkBatchDomain[chunk];

                snapshot.CullingChunks.Add(new FloraDiagnosticsCullingChunk
                {
                    Index = chunk.Index,
                    ArchetypeIndex = archetype != ArchetypeIndex.None ? archetype.Index : -1,
                    TemplateIndex = template != TemplateIndex.None ? template.Index : -1,
                    BatchDomainIndex = batchDomain != BatchDomainIndex.None ? batchDomain.Index : -1,
                    InstanceCount = m_ChunkCount[chunk],
                    CellIndex = cellIndex,
                    CellLevel = cellLevel,
                    CellCoordinates = cellCoordinates,
                    BlockIndex = blockIndex,
                    BlockLevel = blockLevel,
                    BlockCoordinates = blockCoordinates,
                    CellIndexInBlock = cellIndexInBlock,
                    CellLocation = cellLocation.IsValid() ? cellLocation.ToString() : "None",
                    BlockLocation = blockLocationText,
                    CellCenter = cellCenter,
                    CellSize = cellSize,
                    CellBounds = cellBounds,
                });
            }
        }
    }
}

#endif
