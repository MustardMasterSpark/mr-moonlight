// Copyright © Magnetic Arcade. All Rights Reserved.

using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace MA.Flora
{
    internal partial struct TemplateManager
    {
        public void DestroyComponents(NativeArray<EntityId> sourceComponents)
        {
            for (int i = 0; i < sourceComponents.Length; i++)
            {
                EntityId componentId = sourceComponents[i];
                if (!m_SourceRecordByComponent.TryGetValue(componentId, out SourceRecordIndex sourceRecord))
                    continue;

                RemoveSourceComponent(sourceRecord, componentId);

                GameObject source = GetRenderSource(sourceRecord);
                if (!source || m_SourceRecordRendererIds[sourceRecord].Length == 0)
                {
                    DestroyTemplatesForSourceRecord(sourceRecord);
                }
                else
                {
                    UpdateSource(source);
                }
            }
        }

        public void UpdateComponents(NativeArray<EntityId> sourceComponents)
        {
            for (int i = 0; i < sourceComponents.Length; i++)
            {
                if (!m_SourceRecordByComponent.TryGetValue(sourceComponents[i], out SourceRecordIndex sourceRecord))
                    continue;

                UpdateSource(GetRenderSource(sourceRecord));
            }
        }

        public bool Exists(TemplateIndex template)
        {
            return m_TemplateAllocated.Contains(template);
        }

        public readonly GameObject GetIdentitySource(SourceRecordIndex sourceRecord)
        {
            if (!sourceRecord.IsCreated || !m_SourceRecordAllocated.Contains(sourceRecord))
                return null;

            return m_SourceRecords[sourceRecord].IdentitySourceId.ToObject<GameObject>();
        }

        public readonly EntityId GetIdentitySourceId(SourceRecordIndex sourceRecord)
        {
            if (!sourceRecord.IsCreated || !m_SourceRecordAllocated.Contains(sourceRecord))
                return EntityId.None;

            return m_SourceRecords[sourceRecord].IdentitySourceId;
        }

        public readonly GameObject GetRenderSource(SourceRecordIndex sourceRecord)
        {
            if (!sourceRecord.IsCreated || !m_SourceRecordAllocated.Contains(sourceRecord))
                return null;

            return m_SourceRecords[sourceRecord].RenderSourceId.ToObject<GameObject>();
        }

        public readonly EntityId GetRenderSourceId(SourceRecordIndex sourceRecord)
        {
            if (!sourceRecord.IsCreated || !m_SourceRecordAllocated.Contains(sourceRecord))
                return EntityId.None;

            return m_SourceRecords[sourceRecord].RenderSourceId;
        }

        [ExcludeFromBurstCompatTesting("Takes a managed object")]
        public SourceTemplateBinding RegisterSourceBinding(GameObject identitySource, GameObject renderSource, TemplateOptions options, Material grassMaterial = null)
        {
            if (identitySource == null || renderSource == null)
                return default;

            if (renderSource.TryGetComponent(out FloraAdditionalRendererSettings additionalRendererSettings))
            {
                if ((additionalRendererSettings.AdditionalPerInstanceData & FloraAdditionalPerInstanceData.RandomID) != 0)
                    options |= TemplateOptions.RandomID;
                if ((additionalRendererSettings.AdditionalPerInstanceData & FloraAdditionalPerInstanceData.VariationColor) != 0)
                    options |= TemplateOptions.VariationColor;
            }

            EntityId grassMaterialId = grassMaterial != null ? grassMaterial.GetEntityId() : EntityId.None;
            SourceRecordIndex sourceRecord = GetOrCreateSourceRecord(identitySource, renderSource);

            if (TryGetSourceRecordTemplateVariant(sourceRecord, grassMaterialId, options, out TemplateIndex existingTemplate))
            {
                return new SourceTemplateBinding
                {
                    SourceRecord = sourceRecord,
                    Template = existingTemplate,
                };
            }

            TemplateIndex template = ResolveSourceTemplate(sourceRecord, renderSource, options, grassMaterial);
            if (template == TemplateIndex.None)
                return default;

            BindSourceRecordToTemplate(sourceRecord, template);
            return new SourceTemplateBinding
            {
                SourceRecord = sourceRecord,
                Template = template,
            };
        }

        [ExcludeFromBurstCompatTesting("Takes a managed object")]
        public TemplateIndex RegisterSource(GameObject identitySource, GameObject renderSource, TemplateOptions options, Material grassMaterial = null)
        {
            return RegisterSourceBinding(identitySource, renderSource, options, grassMaterial).Template;
        }

        [ExcludeFromBurstCompatTesting("Takes a managed object")]
        public TemplateIndex RegisterSource(GameObject source, TemplateOptions options, Material grassMaterial = null)
        {
            return RegisterSourceBinding(source, source, options, grassMaterial).Template;
        }

        public void UpdateSource(EntityId sourceId)
        {
            UpdateSource(sourceId.ToObject<GameObject>());
        }

        [ExcludeFromBurstCompatTesting("Takes a managed object")]
        public void UpdateSource(GameObject source)
        {
            if (source == null)
                return;

            EntityId renderSourceId = source.GetEntityId();
            if (!m_SourceRecordBySource.TryGetValue(renderSourceId, out SourceRecordIndex sourceRecord))
                return;

            SourceRecord previousRecord = m_SourceRecords[sourceRecord];
            RefreshSourceRecord(source, sourceRecord);

            NativeArray<TemplateIndex> templates = m_SourceRecordTemplates[sourceRecord].AsArray();
            using var templatesCopy = new NativeArray<TemplateIndex>(templates.Length, Allocator.Temp);
            templatesCopy.CopyFrom(templates);

            for (int i = 0; i < templatesCopy.Length; i++)
            {
                TemplateIndex oldTemplate = templatesCopy[i];
                if (!m_TemplateAllocated.Contains(oldTemplate))
                    continue;

                Material grassMaterial = m_GrassMaterialIds[oldTemplate].ToObject<Material>();
                TemplateOptions templateOptions = m_TemplateOptions[oldTemplate];
                TemplateIndex newTemplate = ResolveSourceTemplate(sourceRecord, source, templateOptions, grassMaterial);
                if (newTemplate == TemplateIndex.None)
                    continue;

                SourceRecord updatedRecord = m_SourceRecords[sourceRecord];
                bool lightmapDataChanged =
                    previousRecord.LightmapIndex != updatedRecord.LightmapIndex ||
                    !math.all(previousRecord.LightmapScaleOffset == updatedRecord.LightmapScaleOffset);

                if (newTemplate == oldTemplate)
                {
                    if (lightmapDataChanged)
                    {
                        UpdateSourceRecordInstancesLightmapData(
                            sourceRecord,
                            oldTemplate,
                            updatedRecord.LightmapIndex,
                            updatedRecord.LightmapScaleOffset);
                    }

                    continue;
                }

                BindSourceRecordToTemplate(sourceRecord, newTemplate);
                MoveSourceRecordInstancesToTemplate(sourceRecord, oldTemplate, newTemplate, updatedRecord.LightmapIndex, updatedRecord.LightmapScaleOffset);
                UnbindSourceRecordFromTemplate(sourceRecord, oldTemplate);
            }
        }

        public void MaterialsChanged(NativeArray<EntityId> materialInstanceIds) =>
            InvalidateSourcesForAssetChanges(materialInstanceIds, invalidateMaterials: true, removeLookupEntries: false);

        public void MaterialsDestroyed(NativeArray<EntityId> materialInstanceIds) =>
            InvalidateSourcesForAssetChanges(materialInstanceIds, invalidateMaterials: true, removeLookupEntries: true);

        public void MeshesChanged(NativeArray<EntityId> meshInstanceIds) =>
            InvalidateSourcesForAssetChanges(meshInstanceIds, invalidateMaterials: false, removeLookupEntries: false);

        public void MeshesDestroyed(NativeArray<EntityId> meshInstanceIds) =>
            InvalidateSourcesForAssetChanges(meshInstanceIds, invalidateMaterials: false, removeLookupEntries: true);

        private void InvalidateSourcesForAssetChanges(NativeArray<EntityId> assetIds, bool invalidateMaterials, bool removeLookupEntries)
        {
            if (assetIds.Length == 0)
                return;

            using var uniqueSourceRecords = new NativeHashSet<SourceRecordIndex>(math.max(16, assetIds.Length * 4), Allocator.Temp);
            for (int i = 0; i < assetIds.Length; i++)
            {
                EntityId assetId = assetIds[i];
                if (invalidateMaterials)
                {
                    foreach (RendererStateIndex rendererState in m_RendererStatesByMaterial.GetValuesForKey(assetId))
                    {
                        if (m_RendererStateAllocated.Contains(rendererState))
                            CollectAffectedSourceRecords(rendererState, uniqueSourceRecords);
                    }
                }
                else
                {
                    foreach (RendererStateIndex rendererState in m_RendererStatesByMesh.GetValuesForKey(assetId))
                    {
                        if (m_RendererStateAllocated.Contains(rendererState))
                            CollectAffectedSourceRecords(rendererState, uniqueSourceRecords);
                    }
                }
            }

            using NativeArray<SourceRecordIndex> sourceRecords = uniqueSourceRecords.ToNativeArray(Allocator.Temp);
            for (int i = 0; i < sourceRecords.Length; i++)
                UpdateSource(GetRenderSource(sourceRecords[i]));

            if (!removeLookupEntries)
                return;

            for (int i = 0; i < assetIds.Length; i++)
            {
                if (invalidateMaterials)
                    m_RendererStatesByMaterial.Remove(assetIds[i]);
                else
                    m_RendererStatesByMesh.Remove(assetIds[i]);
            }
        }

        private void CollectAffectedSourceRecords(RendererStateIndex rendererState, NativeHashSet<SourceRecordIndex> uniqueSourceRecords)
        {
            foreach (RendererGroupIndex rendererGroup in m_RendererGroupsByState.GetValuesForKey(rendererState))
            {
                if (!m_RendererGroupAllocated.Contains(rendererGroup))
                    continue;

                foreach (TemplateLayoutIndex templateLayout in m_TemplateLayoutsByGroup.GetValuesForKey(rendererGroup))
                {
                    if (!templateLayout.IsCreated || !m_TemplateLayoutAllocated.Contains(templateLayout))
                        continue;

                    if (!m_TemplateByKey.TryGetValue(new TemplateKey(templateLayout), out TemplateIndex template) ||
                        !m_TemplateAllocated.Contains(template))
                    {
                        continue;
                    }

                    NativeBuffer<SourceRecordIndex> sourceRecords = m_TemplateSourceRecords[template];
                    for (int i = 0; i < sourceRecords.Length; i++)
                    {
                        SourceRecordIndex sourceRecord = sourceRecords[i];
                        if (m_SourceRecordAllocated.Contains(sourceRecord))
                            uniqueSourceRecords.Add(sourceRecord);
                    }
                }
            }
        }

        private SourceRecordIndex GetOrCreateSourceRecord(GameObject identitySource, GameObject renderSource)
        {
            EntityId renderSourceId = renderSource.GetEntityId();
            if (m_SourceRecordBySource.TryGetValue(renderSourceId, out SourceRecordIndex sourceRecord))
            {
                UpdateSourceRecordIdentity(sourceRecord, identitySource ? identitySource.GetEntityId() : EntityId.None);
                RefreshSourceRecord(renderSource, sourceRecord);
                return sourceRecord;
            }

            sourceRecord = m_SourceRecordFreeList.Length > 0 ? m_SourceRecordFreeList.Pop() : m_NextSourceRecordId++;
            EnsureSourceRecordCapacity(sourceRecord + 1);

            m_SourceRecordAllocated.Add(sourceRecord);
            m_SourceRecordBySource.Add(renderSourceId, sourceRecord);
            m_SourceRecords[sourceRecord] = new SourceRecord
            {
                IdentitySourceId = identitySource ? identitySource.GetEntityId() : EntityId.None,
                RenderSourceId = renderSourceId,
                LightmapIndex = -1,
                LightmapScaleOffset = new float4(1f, 1f, 0f, 0f),
                RefCount = 0,
            };

            RefreshSourceRecord(renderSource, sourceRecord);
            return sourceRecord;
        }

        private void UpdateSourceRecordIdentity(SourceRecordIndex sourceRecord, EntityId newIdentitySourceId)
        {
            if (!sourceRecord.IsCreated || !m_SourceRecordAllocated.Contains(sourceRecord))
                return;

            SourceRecord record = m_SourceRecords[sourceRecord];
            if (record.IdentitySourceId == newIdentitySourceId)
                return;

            record.IdentitySourceId = newIdentitySourceId;
            m_SourceRecords[sourceRecord] = record;
        }

        private bool TryGetSourceRecordTemplateVariant(SourceRecordIndex sourceRecord, EntityId grassMaterialId, TemplateOptions options, out TemplateIndex template)
        {
            if (sourceRecord.IsCreated && m_SourceRecordAllocated.Contains(sourceRecord))
            {
                NativeBuffer<TemplateIndex> templates = m_SourceRecordTemplates[sourceRecord];
                for (int i = 0; i < templates.Length; i++)
                {
                    TemplateIndex candidate = templates[i];
                    if (!m_TemplateAllocated.Contains(candidate))
                        continue;

                    if (m_GrassMaterialIds[candidate] == grassMaterialId && m_TemplateOptions[candidate] == options)
                    {
                        template = candidate;
                        return true;
                    }
                }
            }

            template = TemplateIndex.None;
            return false;
        }

        [ExcludeFromBurstCompatTesting("Takes managed objects")]
        private TemplateIndex ResolveSourceTemplate(SourceRecordIndex sourceRecord, GameObject renderSource, TemplateOptions options, Material grassMaterial)
        {
            if (!sourceRecord.IsCreated || !m_SourceRecordAllocated.Contains(sourceRecord) || renderSource == null)
                return TemplateIndex.None;

            EntityId grassMaterialId = grassMaterial ? grassMaterial.GetEntityId() : EntityId.None;
            TemplateLayoutIndex layout = ResolveTemplateLayoutForSource(renderSource, options, grassMaterialId, out TemplateSourceInfo templateSourceInfo);
            if (!layout.IsCreated)
                return TemplateIndex.None;

            SourceRecord record = m_SourceRecords[sourceRecord];
            record.LightmapIndex = templateSourceInfo.LightmapIndex;
            record.LightmapScaleOffset = templateSourceInfo.LightmapScaleOffset;
            m_SourceRecords[sourceRecord] = record;

            var key = new TemplateKey(layout);
            if (m_TemplateByKey.TryGetValue(key, out TemplateIndex template))
            {
                if (!GetTemplateRepresentativeRenderSourceId(template).IsValid())
                    SetTemplateRepresentativeRenderSource(template, sourceRecord);

                return template;
            }

            template = m_TemplateFreeList.Length > 0 ? m_TemplateFreeList.Pop() : m_NextTemplateId++;
            EnsureTemplateCapacity(template + 1);

            TemplateStore.Reset(template);
            m_TemplateAllocated.Add(template);
            m_TemplateByKey.Add(key, template);
            m_GrassMaterialIds[template] = grassMaterialId;
            m_TemplateOptions[template] = options;

            if (grassMaterialId.IsValid())
                m_TemplatesAreGrass.Add(template);

            SetTemplateRepresentativeRenderSource(template, sourceRecord);
            BindHandleToState(template, renderSource, layout);
            return template;
        }

        private void BindSourceRecordToTemplate(SourceRecordIndex sourceRecord, TemplateIndex template)
        {
            if (!sourceRecord.IsCreated || !template.IsCreated)
                return;
            if (!m_SourceRecordAllocated.Contains(sourceRecord) || !m_TemplateAllocated.Contains(template))
                return;
            if (HasSourceRecordTemplateBinding(sourceRecord, template))
                return;

            NativeBuffer<TemplateIndex> templates = m_SourceRecordTemplates[sourceRecord];
            templates.Add(template);
            m_TemplateSourceRecords[template].Add(sourceRecord);

            SourceRecord record = m_SourceRecords[sourceRecord];
            record.RefCount++;
            m_SourceRecords[sourceRecord] = record;

            if (!GetTemplateRepresentativeRenderSourceId(template).IsValid())
                SetTemplateRepresentativeRenderSource(template, sourceRecord);
        }

        private void UnbindSourceRecordFromTemplate(SourceRecordIndex sourceRecord, TemplateIndex template)
        {
            if (!sourceRecord.IsCreated || !template.IsCreated)
                return;
            if (!m_SourceRecordAllocated.Contains(sourceRecord) || !m_TemplateAllocated.Contains(template))
                return;

            NativeBuffer<TemplateIndex> sourceTemplates = m_SourceRecordTemplates[sourceRecord];
            for (int i = 0; i < sourceTemplates.Length; i++)
            {
                if (sourceTemplates[i] != template)
                    continue;

                sourceTemplates.RemoveAtSwapBack(i);
                break;
            }

            NativeBuffer<SourceRecordIndex> templateSources = m_TemplateSourceRecords[template];
            for (int i = 0; i < templateSources.Length; i++)
            {
                if (templateSources[i] != sourceRecord)
                    continue;

                templateSources.RemoveAtSwapBack(i);
                break;
            }

            SourceRecord record = m_SourceRecords[sourceRecord];
            record.RefCount--;
            m_SourceRecords[sourceRecord] = record;

            if (m_TemplateSourceRecords[template].Length == 0)
            {
                DestroyTemplate(template);
            }
            else if (GetTemplateRepresentativeRenderSourceId(template) == record.RenderSourceId)
            {
                RefreshTemplateRepresentativeRenderSource(template);
            }

            TryDestroySourceRecordIfUnused(sourceRecord);
        }

        private bool HasSourceRecordTemplateBinding(SourceRecordIndex sourceRecord, TemplateIndex template)
        {
            NativeBuffer<TemplateIndex> templates = m_SourceRecordTemplates[sourceRecord];
            for (int i = 0; i < templates.Length; i++)
            {
                if (templates[i] == template)
                    return true;
            }

            return false;
        }

        private void SetTemplateRepresentativeRenderSource(TemplateIndex template, SourceRecordIndex sourceRecord)
        {
            if (!template.IsCreated || !sourceRecord.IsCreated)
                return;
            if (!m_TemplateAllocated.Contains(template) || !m_SourceRecordAllocated.Contains(sourceRecord))
                return;

            SourceRecord record = m_SourceRecords[sourceRecord];
            GameObject renderSource = record.RenderSourceId.ToObject<GameObject>();
            m_TemplateRepresentativeRenderSourceIds[template] = renderSource ? renderSource.GetEntityId() : EntityId.None;
        }

        private void RefreshTemplateRepresentativeRenderSource(TemplateIndex template)
        {
            NativeBuffer<SourceRecordIndex> sourceRecords = m_TemplateSourceRecords[template];
            for (int i = 0; i < sourceRecords.Length; i++)
            {
                SourceRecordIndex sourceRecord = sourceRecords[i];
                if (m_SourceRecordAllocated.Contains(sourceRecord))
                {
                    SetTemplateRepresentativeRenderSource(template, sourceRecord);
                    return;
                }
            }

            m_TemplateRepresentativeRenderSourceIds[template] = EntityId.None;
        }

        private void RefreshSourceRecord(GameObject source, SourceRecordIndex sourceRecord)
        {
            ClearSourceRecordMappings(sourceRecord);

            SourceRecord record = m_SourceRecords[sourceRecord];
            record.RenderSourceId = source ? source.GetEntityId() : record.RenderSourceId;
            record.LodGroupId = EntityId.None;
            record.AdditionalSettingsId = EntityId.None;

            if (source == null)
            {
                m_SourceRecords[sourceRecord] = record;
                return;
            }

            if (source.TryGetComponent(out LODGroup lodGroup))
            {
                record.LodGroupId = lodGroup.GetEntityId();
                AddSourceRecordComponent(sourceRecord, record.LodGroupId, isRenderer: false);
            }

            if (source.TryGetComponent(out FloraAdditionalRendererSettings additionalSettings))
            {
                record.AdditionalSettingsId = additionalSettings.GetEntityId();
                AddSourceRecordComponent(sourceRecord, record.AdditionalSettingsId, isRenderer: false);
            }

            using (ListPool<Renderer>.Get(out List<Renderer> renderers))
            {
                CollectSourceRenderers(source, renderers);
                HashSet<EntityId> uniqueRendererIds = new HashSet<EntityId>();
                for (int i = 0; i < renderers.Count; i++)
                {
                    Renderer renderer = renderers[i];
                    if (!renderer)
                        continue;

                    EntityId rendererId = renderer.GetEntityId();
                    if (!uniqueRendererIds.Add(rendererId))
                        continue;

                    AddSourceRecordComponent(sourceRecord, rendererId, isRenderer: true);

                    if (renderer is MeshRenderer && renderer.TryGetComponent(out MeshFilter meshFilter))
                        AddSourceRecordComponent(sourceRecord, meshFilter.GetEntityId(), isRenderer: false);
                }
            }

            m_SourceRecords[sourceRecord] = record;
        }

        private static void CollectSourceRenderers(GameObject source, List<Renderer> renderers)
        {
            renderers.Clear();
            LOD[] lods = source.GetLODs();
            for (int lodIndex = 0; lodIndex < lods.Length; lodIndex++)
            {
                Renderer[] lodRenderers = lods[lodIndex].renderers;
                for (int i = 0; i < lodRenderers.Length; i++)
                {
                    if (lodRenderers[i] != null)
                        renderers.Add(lodRenderers[i]);
                }
            }
        }

        private void AddSourceRecordComponent(SourceRecordIndex sourceRecord, EntityId componentId, bool isRenderer)
        {
            if (!componentId.IsValid())
                return;

            if (m_SourceRecordByComponent.TryAdd(componentId, sourceRecord))
                m_SourceRecordComponentIds[sourceRecord].Add(componentId);

            if (isRenderer)
                m_SourceRecordRendererIds[sourceRecord].Add(componentId);
        }

        private void RemoveSourceComponent(SourceRecordIndex sourceRecord, EntityId componentId)
        {
            if (!sourceRecord.IsCreated || !m_SourceRecordAllocated.Contains(sourceRecord))
                return;

            NativeBuffer<EntityId> components = m_SourceRecordComponentIds[sourceRecord];
            for (int i = 0; i < components.Length; i++)
            {
                if (components[i] != componentId)
                    continue;

                components.RemoveAtSwapBack(i);
                break;
            }

            NativeBuffer<EntityId> renderers = m_SourceRecordRendererIds[sourceRecord];
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != componentId)
                    continue;

                renderers.RemoveAtSwapBack(i);
                break;
            }

            SourceRecord record = m_SourceRecords[sourceRecord];
            if (record.LodGroupId == componentId)
                record.LodGroupId = EntityId.None;
            if (record.AdditionalSettingsId == componentId)
                record.AdditionalSettingsId = EntityId.None;
            m_SourceRecords[sourceRecord] = record;

            m_SourceRecordByComponent.Remove(componentId);
        }

        private void ClearSourceRecordMappings(SourceRecordIndex sourceRecord)
        {
            NativeBuffer<EntityId> components = m_SourceRecordComponentIds[sourceRecord];
            for (int i = 0; i < components.Length; i++)
                m_SourceRecordByComponent.Remove(components[i]);

            components.Clear();
            m_SourceRecordRendererIds[sourceRecord].Clear();
        }

        private void TryDestroySourceRecordIfUnused(SourceRecordIndex sourceRecord)
        {
            if (!sourceRecord.IsCreated || !m_SourceRecordAllocated.Contains(sourceRecord))
                return;

            SourceRecord record = m_SourceRecords[sourceRecord];
            if (record.RefCount > 0 || m_SourceRecordInstances[sourceRecord].Length > 0)
                return;

            ClearSourceRecordMappings(sourceRecord);
            m_SourceRecordBySource.Remove(record.RenderSourceId);
            m_SourceRecordTemplates[sourceRecord].Clear();
            m_SourceRecordInstances[sourceRecord].Clear();
            m_SourceRecords[sourceRecord] = default;
            m_SourceRecordAllocated.Remove(sourceRecord);
            m_SourceRecordFreeList.Add(sourceRecord);
        }

        private void DestroyTemplatesForSourceRecord(SourceRecordIndex sourceRecord)
        {
            if (!sourceRecord.IsCreated || !m_SourceRecordAllocated.Contains(sourceRecord))
                return;

            DestroyInstancesForSourceRecord(sourceRecord);

            NativeArray<TemplateIndex> templates = m_SourceRecordTemplates[sourceRecord].AsArray();
            using var templatesCopy = new NativeArray<TemplateIndex>(templates.Length, Allocator.Temp);
            templatesCopy.CopyFrom(templates);

            for (int i = 0; i < templatesCopy.Length; i++)
            {
                if (m_TemplateAllocated.Contains(templatesCopy[i]))
                    UnbindSourceRecordFromTemplate(sourceRecord, templatesCopy[i]);
            }

            TryDestroySourceRecordIfUnused(sourceRecord);
        }

        private void DestroyInstancesForSourceRecord(SourceRecordIndex sourceRecord)
        {
            NativeBuffer<FloraInstanceHandle> instances = m_SourceRecordInstances[sourceRecord];
            if (instances.Length == 0)
                return;

            using var instancesCopy = new NativeArray<FloraInstanceHandle>(instances.Length, Allocator.Temp);
            instancesCopy.CopyFrom(instances.AsArray());
            m_InstanceManager.ValueRW.Destroy(instancesCopy);
        }

        [ExcludeFromBurstCompatTesting("Takes managed objects")]
        private void MoveSourceRecordInstancesToTemplate(SourceRecordIndex sourceRecord, TemplateIndex oldTemplate, TemplateIndex newTemplate, int lightmapIndex, float4 lightmapScaleOffset)
        {
            NativeBuffer<FloraInstanceHandle> instances = m_SourceRecordInstances[sourceRecord];
            if (instances.Length == 0)
                return;

            using var filteredInstances = new NativeList<FloraInstanceHandle>(instances.Length, Allocator.Temp);
            for (int i = 0; i < instances.Length; i++)
            {
                FloraInstanceHandle instance = instances[i];
                if (!m_InstanceManager.ValueRO.Exists(instance))
                    continue;

                InstanceInChunk instanceInChunk = InstanceRegistry.Data.GetInstanceInChunk(instance);
                if (instanceInChunk.Equals(InstanceInChunk.None))
                    continue;

                if (instanceInChunk.Chunk.Archetype.Key.Template == oldTemplate)
                    filteredInstances.Add(instance);
            }

            if (filteredInstances.Length > 0)
                m_InstanceManager.ValueRW.MoveInstancesToNewTemplate(filteredInstances.AsArray(), newTemplate, lightmapIndex, lightmapScaleOffset);
        }

        [ExcludeFromBurstCompatTesting("Takes managed objects")]
        private void UpdateSourceRecordInstancesLightmapData(SourceRecordIndex sourceRecord, TemplateIndex template, int lightmapIndex, float4 lightmapScaleOffset)
        {
            NativeBuffer<FloraInstanceHandle> instances = m_SourceRecordInstances[sourceRecord];
            if (instances.Length == 0)
                return;

            using var filteredInstances = new NativeList<FloraInstanceHandle>(instances.Length, Allocator.Temp);
            for (int i = 0; i < instances.Length; i++)
            {
                FloraInstanceHandle instance = instances[i];
                if (!m_InstanceManager.ValueRO.Exists(instance))
                    continue;

                InstanceInChunk instanceInChunk = InstanceRegistry.Data.GetInstanceInChunk(instance);
                if (instanceInChunk.Equals(InstanceInChunk.None))
                    continue;

                if (instanceInChunk.Chunk.Archetype.Key.Template == template)
                    filteredInstances.Add(instance);
            }

            if (filteredInstances.Length > 0)
                m_InstanceManager.ValueRW.UpdateInstancesLightmapData(filteredInstances.AsArray(), lightmapIndex, lightmapScaleOffset);
        }

        private void DestroyTemplate(TemplateIndex template)
        {
            if (!template.IsCreated || !m_TemplateAllocated.Contains(template))
                return;

            var templateKey = new TemplateKey(m_TemplateLayoutBindings[template]);

            UnbindHandleFromState(template, notifyStateChange: false);

            m_TemplateSourceRecords[template].Clear();
            m_Chunks[template].Clear();
            m_CullingChunks[template].Clear();
            m_TemplateOptions[template] = default;
            m_GrassMaterialIds[template] = default;
            m_TemplateRepresentativeRenderSourceIds[template] = default;
            TemplateStore.Reset(template);

            m_TemplateByKey.Remove(templateKey);
            m_TemplateFreeList.Add(template);
            m_TemplateAllocated.Remove(template);
            m_TemplatesAreGrass.Remove(template);
        }
    }
}
