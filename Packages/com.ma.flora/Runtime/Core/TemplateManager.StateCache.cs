// Copyright © Magnetic Arcade. All Rights Reserved.

using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Assertions;

namespace MA.Flora
{
    internal unsafe partial struct TemplateManager
    {
        private const ulong FnvOffsetBasis64 = 14695981039346656037ul;
        private const ulong FnvPrime64 = 1099511628211ul;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ulong HashCombine(ulong hash, uint value) => (hash ^ value) * FnvPrime64;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ulong HashCombine(ulong hash, int value) => HashCombine(hash, unchecked((uint)value));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ulong HashCombine(ulong hash, float value) => HashCombine(hash, math.asuint(value));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool TemplateDataEquals(in TemplateData a, in TemplateData b)
        {
            TemplateData aData = a;
            TemplateData bData = b;
            return UnsafeUtility.MemCmp(&aData, &bData, UnsafeUtility.SizeOf<TemplateData>()) == 0;
        }

        private TemplateLayoutIndex ResolveTemplateLayoutForSource(GameObject source, TemplateOptions templateOptions, EntityId grassMaterialId, out TemplateSourceInfo templateSourceInfo)
        {
            templateSourceInfo = default;
            if (!source)
                return TemplateLayoutIndex.None;

            templateSourceInfo = source.ComputeTemplateSourceInfo();
            TemplateCapabilityProfile capabilityProfile = BuildTemplateCapabilityProfile(ref templateSourceInfo, templateOptions);

            using var rendererGroups = new NativeList<RendererGroupIndex>(math.max(1, templateSourceInfo.LodCount), Allocator.Temp);
            using var rendererStates = new NativeList<RendererStateIndex>(16, Allocator.Temp);

            Material detailBillboardMaterial = grassMaterialId.ToObject<Material>();
            LOD[] lods = templateSourceInfo.Type == TemplateRenderType.MeshLod ? null : source.GetLODs();
            for (int lodIndex = 0; lodIndex < templateSourceInfo.LodCount; lodIndex++)
            {
                rendererStates.Clear();
                GetRenderersForLod(source, lods, templateSourceInfo.Type, lodIndex, out Renderer[] renderers);
                for (int rendererIndex = 0; renderers != null && rendererIndex < renderers.Length; rendererIndex++)
                {
                    Renderer renderer = renderers[rendererIndex];
                    if (renderer == null || !renderer.enabled)
                        continue;

                    using var drawDescriptors = new NativeList<DrawDescriptor>(8, Allocator.Temp);
                    RendererStateRecord rendererStateRecord = BuildRendererStateRecord(
                        TemplateIndex.None,
                        source,
                        templateSourceInfo.Type,
                        renderer,
                        detailBillboardMaterial,
                        templateOptions,
                        capabilityProfile,
                        lodIndex,
                        drawDescriptors);

                    RendererStateIndex rendererState = ResolveRendererState(rendererStateRecord, drawDescriptors);
                    if (rendererState.IsCreated)
                        rendererStates.Add(rendererState);
                }

                RendererGroupRecord rendererGroupRecord = BuildRendererGroupRecord(lodIndex, rendererStates);
                RendererGroupIndex rendererGroup = ResolveRendererGroup(rendererGroupRecord, rendererStates);
                if (rendererGroup.IsCreated)
                    rendererGroups.Add(rendererGroup);
            }

            TemplateLayoutRecord layoutRecord = BuildTemplateLayoutRecord(
                grassMaterialId,
                capabilityProfile,
                ref templateSourceInfo,
                rendererGroups);
            return ResolveTemplateLayout(layoutRecord, rendererGroups);
        }

        private TemplateCapabilityProfile BuildTemplateCapabilityProfile(ref TemplateSourceInfo templateSourceInfo, TemplateOptions templateOptions)
        {
            TemplateRenderFlags effectiveFlags = templateSourceInfo.Flags;
            if (!CanInstancesHaveMotionVectors || (templateOptions & TemplateOptions.DisableMotionVectors) != 0)
                effectiveFlags &= ~TemplateRenderFlags.HasPerObjectMotionVectors;
            if (!CanInstancesHaveLightProbes || (templateOptions & TemplateOptions.DisableLightProbes) != 0)
                effectiveFlags &= ~TemplateRenderFlags.HasLightProbes;
            if ((templateOptions & TemplateOptions.DisableLightmaps) != 0)
                effectiveFlags &= ~TemplateRenderFlags.HasLightmaps;

            BatchBuiltinPropertyFlags metadataFlags = BatchBuiltinPropertyFlags.Required;
#if UNITY_EDITOR
            metadataFlags |= BatchBuiltinPropertyFlags.EntityId;
#endif
            if ((effectiveFlags & TemplateRenderFlags.HasLightProbes) != 0)
                metadataFlags |= BatchBuiltinPropertyFlags.ShCoefficients;
            if ((effectiveFlags & TemplateRenderFlags.HasLightmaps) != 0)
                metadataFlags |= BatchBuiltinPropertyFlags.LightmapST;
            if ((effectiveFlags & TemplateRenderFlags.HasPerObjectMotionVectors) != 0)
                metadataFlags |= BatchBuiltinPropertyFlags.PrevLocalToWorld;
            if ((effectiveFlags & TemplateRenderFlags.HasRandomID) != 0)
                metadataFlags |= BatchBuiltinPropertyFlags.RandomID;
            if ((effectiveFlags & TemplateRenderFlags.HasVariationColor) != 0)
                metadataFlags |= BatchBuiltinPropertyFlags.VariationColor;

            return new TemplateCapabilityProfile
            {
                MetadataFlags = metadataFlags,
                BatchDomainIndex = m_InstanceBuffer.ValueRW.GetOrCreateDomain(metadataFlags),
                EffectiveFlags = effectiveFlags,
                Options = templateOptions,
            };
        }

        private static MeshRenderer[] s_MeshLodRenderers = new MeshRenderer[1];
        private static BillboardRenderer[] s_BillboardRenderers = new BillboardRenderer[1];

        private static void GetRenderersForLod(GameObject source, LOD[] lods, TemplateRenderType type, int lodIndex, out Renderer[] renderers)
        {
            if (type == TemplateRenderType.MeshLod || type == TemplateRenderType.MeshRenderer)
            {
                s_MeshLodRenderers[0] = source.GetComponent<MeshRenderer>();
                renderers = s_MeshLodRenderers;
                return;
            }

            if (type == TemplateRenderType.Billboard)
            {
                s_BillboardRenderers[0] = source.GetComponent<BillboardRenderer>();
                renderers = s_BillboardRenderers;
                return;
            }

            renderers = lods != null && lodIndex < lods.Length ? lods[lodIndex].renderers : null;
        }

        private RendererStateRecord BuildRendererStateRecord(
            TemplateIndex template,
            GameObject representativeRenderSource,
            TemplateRenderType sourceType,
            Renderer renderer,
            Material detailBillboardMaterial,
            TemplateOptions templateOptions,
            TemplateCapabilityProfile capabilityProfile,
            int lodIndex,
            NativeList<DrawDescriptor> drawDescriptors)
        {
            ulong descriptorSignature = CompileRendererDrawDescriptors(
                template,
                representativeRenderSource,
                renderer,
                detailBillboardMaterial,
                templateOptions,
                capabilityProfile.BatchDomainIndex,
                lodIndex,
                drawDescriptors);

            EntityId overrideMaterialId = detailBillboardMaterial ? detailBillboardMaterial.GetEntityId() : EntityId.None;
            TemplateRenderType rendererType = renderer is BillboardRenderer ? TemplateRenderType.Billboard : sourceType;

            var record = new RendererStateRecord
            {
                DescriptorCount = (ushort)math.min(drawDescriptors.Length, ushort.MaxValue),
                BatchDomainIndex = capabilityProfile.BatchDomainIndex,
                LodIndex = (byte)lodIndex,
                Type = rendererType,
                CapabilityProfile = capabilityProfile,
                RefCount = 0,
            };

            record.Key = new RendererStateKey
            {
                OverrideMaterialId = overrideMaterialId,
                DescriptorSignature = descriptorSignature,
                MetadataFlags = (uint)capabilityProfile.MetadataFlags,
                DescriptorCount = record.DescriptorCount,
                LodIndex = record.LodIndex,
                Type = (byte)record.Type,
            };

            return record;
        }

        private ulong CompileRendererDrawDescriptors(
            TemplateIndex template,
            GameObject representativeRenderSource,
            Renderer renderer,
            Material detailBillboardMaterial,
            TemplateOptions templateOptions,
            BatchDomainIndex batchDomainIndex,
            int lodIndex,
            NativeList<DrawDescriptor> drawDescriptors)
        {
            if (!renderer || batchDomainIndex == BatchDomainIndex.None)
                return FnvOffsetBasis64;

            NativeArray<DrawDescriptor> descriptors = m_DrawManager.ValueRW.BuildDrawDescriptors(
                template,
                representativeRenderSource,
                templateOptions,
                lodIndex,
                renderer,
                detailBillboardMaterial,
                batchDomainIndex,
                Allocator.Temp);

            ulong descriptorSignature = FnvOffsetBasis64;
            if (!descriptors.IsCreated)
                return descriptorSignature;

            for (int i = 0; i < descriptors.Length; i++)
            {
                drawDescriptors.Add(descriptors[i]);
                descriptorSignature = HashCombine(descriptorSignature, descriptors[i].GetHashCode());
            }

            descriptors.Dispose();
            return descriptorSignature;
        }

        private RendererGroupRecord BuildRendererGroupRecord(int lodIndex, NativeList<RendererStateIndex> rendererStates)
        {
            ulong stateSignature = FnvOffsetBasis64;
            for (int i = 0; i < rendererStates.Length; i++)
                stateSignature = HashCombine(stateSignature, rendererStates[i].Index);

            return new RendererGroupRecord
            {
                LodIndex = (byte)lodIndex,
                RefCount = 0,
                Key = new RendererGroupKey
                {
                    StateSignature = stateSignature,
                    StateCount = (ushort)math.min(rendererStates.Length, ushort.MaxValue),
                    LodIndex = (byte)lodIndex,
                }
            };
        }

        private TemplateLayoutRecord BuildTemplateLayoutRecord(
            EntityId grassMaterialId,
            TemplateCapabilityProfile capabilityProfile,
            ref TemplateSourceInfo templateSourceInfo,
            NativeList<RendererGroupIndex> rendererGroups)
        {
            var additionalRendererSettings = templateSourceInfo.AdditionalRendererSettings;
            int lodCount = math.clamp(templateSourceInfo.LodCount, 1, CullingConstants.MaxLodCount);
            templateSourceInfo.LodCount = lodCount;

            TemplateLayoutRecord record = new TemplateLayoutRecord
            {
                CapabilityProfile = capabilityProfile,
                GroupSignature = BuildRendererGroupSignature(rendererGroups),
                Type = templateSourceInfo.Type,
                Flags = capabilityProfile.EffectiveFlags,
                BatchDomainIndex = capabilityProfile.BatchDomainIndex,
                InitialVariationColor = (Vector4)additionalRendererSettings.InitialVariationColor,
                MaxRenderDistance = additionalRendererSettings.MaxRenderDistance,
                MaxShadowDistance = additionalRendererSettings.MaxShadowDistance,
                AffectedByGlobalDensity = additionalRendererSettings.AffectedByGlobalDensity,
                AffectedByRangeDensity = additionalRendererSettings.AffectedByRangeDensity,
                MinShadowLod = math.min(additionalRendererSettings.MinShadowLOD, lodCount - 1),
                LodCount = (byte)lodCount,
                LodFadeMode = templateSourceInfo.FadeMode,
                HasAnimatedCrossFade = templateSourceInfo.HasAnimatedCrossFade,
                SupportsFadeKeyword = templateSourceInfo.SupportsFadeKeyword,
                LocalAnchorPoint = templateSourceInfo.LocalAnchorPoint,
                LocalReferencePoint = templateSourceInfo.LocalReferencePoint,
                LocalSize = templateSourceInfo.LocalSize,
                LocalAABB = templateSourceInfo.LocalAABB,
                LodHeights0To3 = new float4(
                    templateSourceInfo.LODHeights[0],
                    templateSourceInfo.LODHeights[1],
                    templateSourceInfo.LODHeights[2],
                    templateSourceInfo.LODHeights[3]),
                LodHeights4To7 = new float4(
                    templateSourceInfo.LODHeights[4],
                    templateSourceInfo.LODHeights[5],
                    templateSourceInfo.LODHeights[6],
                    templateSourceInfo.LODHeights[7]),
                LodTransitionHeights0To3 = new float4(
                    templateSourceInfo.LODTransitionHeights[0],
                    templateSourceInfo.LODTransitionHeights[1],
                    templateSourceInfo.LODTransitionHeights[2],
                    templateSourceInfo.LODTransitionHeights[3]),
                LodTransitionHeights4To7 = new float4(
                    templateSourceInfo.LODTransitionHeights[4],
                    templateSourceInfo.LODTransitionHeights[5],
                    templateSourceInfo.LODTransitionHeights[6],
                    templateSourceInfo.LODTransitionHeights[7]),
                RefCount = 0,
            };

            record.TemplateData = BuildTemplateBufferData(in record, ref templateSourceInfo);
            record.Key = new TemplateLayoutKey
            {
                GrassMaterialId = grassMaterialId,
                CapabilityProfile = capabilityProfile,
                GroupSignature = record.GroupSignature,
                GroupCount = (ushort)math.min(rendererGroups.Length, ushort.MaxValue),
                Type = record.Type,
                InitialVariationColor = record.InitialVariationColor,
                LodFadeMode = record.LodFadeMode,
                HasAnimatedCrossFade = record.HasAnimatedCrossFade,
                SupportsFadeKeyword = record.SupportsFadeKeyword,
                LocalAnchorPoint = record.LocalAnchorPoint,
                TemplateData = record.TemplateData,
            };
            return record;
        }

        private TemplateData BuildTemplateBufferData(in TemplateLayoutRecord layoutRecord, ref TemplateSourceInfo templateSourceInfo)
        {
            uint templateFlags = 0;
            switch (templateSourceInfo.Type)
            {
                case TemplateRenderType.LodGroup:
                    templateFlags |= TemplateData.TemplateFlagIsLodGroup;
                    if (templateSourceInfo.HasAnyCrossFade && layoutRecord.SupportsFadeKeyword)
                    {
                        templateFlags |= TemplateData.TemplateFlagHasCrossFade;
                        if (layoutRecord.HasAnimatedCrossFade)
                            templateFlags |= TemplateData.TemplateFlagHasAnimatedFade;
                    }
                    break;
                case TemplateRenderType.MeshLod:
                    templateFlags |= TemplateData.TemplateFlagIsMeshLod;
                    break;
            }

            if ((layoutRecord.Flags & TemplateRenderFlags.HasPerObjectMotionVectors) != 0)
                templateFlags |= TemplateData.TemplateFlagHasMotionVectors;
            if ((layoutRecord.Flags & TemplateRenderFlags.HasRandomID) != 0)
                templateFlags |= TemplateData.TemplateFlagHasRandomID;

            var additionalSettings = templateSourceInfo.AdditionalRendererSettings;
            if (additionalSettings.AffectedByGlobalDensity)
                templateFlags |= TemplateData.TemplateFlagAffectedByGlobalDensity;
            if (additionalSettings.AffectedByRangeDensity)
                templateFlags |= TemplateData.TemplateFlagAffectedByRangeDensity;
            if (additionalSettings.AffectedByMinimumScreenSize)
                templateFlags |= TemplateData.TemplateFlagAffectedByMinScreenSize;

            var templateData = new TemplateData
            {
                flags = templateFlags,
                maxRenderDistance = additionalSettings.MaxRenderDistance,
                maxShadowDistance = additionalSettings.MaxShadowDistance,
                localCenter = templateSourceInfo.LocalAABB.Center.xyz,
                localExtent = templateSourceInfo.LocalAABB.Extent.xyz,
                localBoundingRadius = math.length(templateSourceInfo.LocalAABB.Extent),
                lodPoint = templateSourceInfo.LocalReferencePoint,
                localSize = templateSourceInfo.LocalSize,
                lodCount = (uint)layoutRecord.LodCount,
                lodMax = (uint)(layoutRecord.LodCount - 1),
                lodMinShadow = (uint)layoutRecord.MinShadowLod,
                meshLodBias = templateSourceInfo.MeshLodBias,
                meshLodSlope = templateSourceInfo.MeshLodSlope,
                meshLodSelectionBias = templateSourceInfo.MeshLodSelectionBias,
            };

            for (int i = 0; i < layoutRecord.LodCount; i++)
            {
                if (templateSourceInfo.PercentageFlags[i])
                    templateData.packedLODFlags |= (uint)(1 << i);
                if (templateSourceInfo.LODHasShadows[i])
                    templateData.packedLODFlags |= (uint)(1 << (i + CullingConstants.MaxLodCount));

                float lodHeight = templateSourceInfo.LODHeights[i];
                float transitionHeight = templateSourceInfo.LODTransitionHeights[i];
                templateData.lodHeightRcp[i] = lodHeight > 0f ? 1.0f / lodHeight : float.PositiveInfinity;
                templateData.lodTransitionHeightRcp[i] = transitionHeight > 0f ? 1.0f / transitionHeight : 0f;
            }

            return templateData;
        }

        private static ulong BuildRendererGroupSignature(NativeList<RendererGroupIndex> rendererGroups)
        {
            ulong signature = FnvOffsetBasis64;
            for (int i = 0; i < rendererGroups.Length; i++)
                signature = HashCombine(signature, rendererGroups[i].Index);
            return signature;
        }

        private RendererStateIndex ResolveRendererState(in RendererStateRecord candidateRecord, NativeList<DrawDescriptor> candidateDescriptors)
        {
            RendererStateIndex rendererState = FindEquivalentRendererState(in candidateRecord, candidateDescriptors);
            if (!rendererState.IsCreated)
                rendererState = CreateRendererState(in candidateRecord, candidateDescriptors);
            return rendererState;
        }

        private RendererStateIndex FindEquivalentRendererState(in RendererStateRecord candidateRecord, NativeList<DrawDescriptor> candidateDescriptors)
        {
            if (!m_RendererStateByKey.TryGetFirstValue(candidateRecord.Key, out RendererStateIndex rendererState, out var iterator))
                return RendererStateIndex.None;

            do
            {
                if (IsRendererStateEquivalent(rendererState, in candidateRecord, candidateDescriptors))
                    return rendererState;
            } while (m_RendererStateByKey.TryGetNextValue(out rendererState, ref iterator));

            return RendererStateIndex.None;
        }

        private bool IsRendererStateEquivalent(RendererStateIndex rendererState, in RendererStateRecord candidateRecord, NativeList<DrawDescriptor> candidateDescriptors)
        {
            if (!rendererState.IsCreated || !m_RendererStateAllocated.Contains(rendererState))
                return false;

            RendererStateRecord existingRecord = m_RendererStateRecords[rendererState];
            if (existingRecord.BatchDomainIndex != candidateRecord.BatchDomainIndex ||
                existingRecord.CapabilityProfile.EffectiveFlags != candidateRecord.CapabilityProfile.EffectiveFlags)
            {
                return false;
            }

            return DrawDescriptorBufferEqualsList(m_RendererStateDrawDescriptors[rendererState], candidateDescriptors);
        }

        private static bool DrawDescriptorBufferEqualsList(NativeBuffer<DrawDescriptor> existing, NativeList<DrawDescriptor> candidate)
        {
            if (existing.Length != candidate.Length)
                return false;

            for (int i = 0; i < existing.Length; i++)
            {
                if (!existing[i].Equals(candidate[i]))
                    return false;
            }

            return true;
        }

        private RendererStateIndex CreateRendererState(in RendererStateRecord stateRecord, NativeList<DrawDescriptor> drawDescriptors)
        {
            RendererStateIndex rendererState = m_RendererStateFreeList.Length > 0 ? m_RendererStateFreeList.Pop() : m_NextRendererStateId++;
            EnsureRendererStateCapacity(rendererState + 1);
            EnsureRendererStateLookupCapacity();

            m_RendererStateAllocated.Add(rendererState);
            m_RendererStateByKey.Add(stateRecord.Key, rendererState);
            m_RendererStateRecords[rendererState] = stateRecord;
            m_RendererStateDrawDescriptors[rendererState].CopyFrom(drawDescriptors.AsArray());

            using var registeredDraws = new NativeList<DrawBatchIndex>(drawDescriptors.Length, Allocator.Temp);
            using var cameraDraws = new NativeList<DrawBatchIndex>(drawDescriptors.Length, Allocator.Temp);
            using var shadowDraws = new NativeList<DrawBatchIndex>(drawDescriptors.Length, Allocator.Temp);
            using var materialIds = new NativeList<EntityId>(drawDescriptors.Length, Allocator.Temp);
            using var meshIds = new NativeList<EntityId>(drawDescriptors.Length, Allocator.Temp);

            MaterializeDrawDescriptors(drawDescriptors, registeredDraws, cameraDraws, shadowDraws, materialIds, meshIds);

            CopyUniqueDrawIndices(m_RendererStateRegisteredDrawIndices[rendererState], registeredDraws);
            CopyUniqueDrawIndices(m_RendererStateCameraDrawIndices[rendererState], cameraDraws);
            CopyUniqueDrawIndices(m_RendererStateShadowDrawIndices[rendererState], shadowDraws);
            CopyUniqueEntityIds(m_RendererStateMaterialInstanceIds[rendererState], materialIds);
            CopyUniqueEntityIds(m_RendererStateMeshInstanceIds[rendererState], meshIds);

            NativeBuffer<EntityId> materials = m_RendererStateMaterialInstanceIds[rendererState];
            for (int i = 0; i < materials.Length; i++)
                m_RendererStatesByMaterial.Add(materials[i], rendererState);

            NativeBuffer<EntityId> meshes = m_RendererStateMeshInstanceIds[rendererState];
            for (int i = 0; i < meshes.Length; i++)
                m_RendererStatesByMesh.Add(meshes[i], rendererState);

            return rendererState;
        }

        private void MaterializeDrawDescriptors(
            NativeList<DrawDescriptor> drawDescriptors,
            NativeList<DrawBatchIndex> registeredDraws,
            NativeList<DrawBatchIndex> cameraDraws,
            NativeList<DrawBatchIndex> shadowDraws,
            NativeList<EntityId> materialIds,
            NativeList<EntityId> meshIds)
        {
            for (int i = 0; i < drawDescriptors.Length; i++)
            {
                DrawDescriptor descriptor = drawDescriptors[i];
                DrawBatchIndex drawIndex = m_DrawManager.ValueRW.RegisterDraw(descriptor);
                if (drawIndex == DrawBatchIndex.None)
                    continue;

                registeredDraws.Add(drawIndex);
                materialIds.Add(descriptor.MaterialEntityId);
                meshIds.Add(descriptor.MeshEntityId);

                if (descriptor.RangeKey.IsInCameraPass)
                    cameraDraws.Add(drawIndex);

                if (descriptor.RangeKey.IsInShadowPass)
                    shadowDraws.Add(drawIndex);
            }
        }

        private void RetainRendererState(RendererStateIndex rendererState)
        {
            RendererStateRecord record = m_RendererStateRecords[rendererState];
            record.RefCount++;
            m_RendererStateRecords[rendererState] = record;
        }

        private void ReleaseRendererState(RendererStateIndex rendererState)
        {
            if (!rendererState.IsCreated || !m_RendererStateAllocated.Contains(rendererState))
                return;

            RendererStateRecord record = m_RendererStateRecords[rendererState];
            record.RefCount--;
            Assert.IsTrue(record.RefCount >= 0, $"Renderer state refcount underflow: {rendererState}");
            m_RendererStateRecords[rendererState] = record;
            if (record.RefCount == 0)
                DestroyRendererState(rendererState);
        }

        private void DestroyRendererState(RendererStateIndex rendererState)
        {
            RendererStateRecord record = m_RendererStateRecords[rendererState];
            m_RendererStateByKey.Remove(record.Key, rendererState);

            NativeBuffer<DrawBatchIndex> drawRegistrations = m_RendererStateRegisteredDrawIndices[rendererState];
            for (int i = 0; i < drawRegistrations.Length; i++)
                m_DrawManager.ValueRW.ReleaseDraw(drawRegistrations[i]);
            drawRegistrations.Clear();

            NativeBuffer<EntityId> materials = m_RendererStateMaterialInstanceIds[rendererState];
            for (int i = 0; i < materials.Length; i++)
                m_RendererStatesByMaterial.Remove(materials[i], rendererState);
            materials.Clear();

            NativeBuffer<EntityId> meshes = m_RendererStateMeshInstanceIds[rendererState];
            for (int i = 0; i < meshes.Length; i++)
                m_RendererStatesByMesh.Remove(meshes[i], rendererState);
            meshes.Clear();

            m_RendererStateDrawDescriptors[rendererState].Clear();
            m_RendererStateCameraDrawIndices[rendererState].Clear();
            m_RendererStateShadowDrawIndices[rendererState].Clear();
            m_RendererStateRecords[rendererState] = default;
            m_RendererStateAllocated.Remove(rendererState);
            m_RendererStateFreeList.Add(rendererState);
        }

        private RendererGroupIndex ResolveRendererGroup(in RendererGroupRecord candidateRecord, NativeList<RendererStateIndex> candidateStates)
        {
            RendererGroupIndex rendererGroup = FindEquivalentRendererGroup(in candidateRecord, candidateStates);
            if (!rendererGroup.IsCreated)
                rendererGroup = CreateRendererGroup(in candidateRecord, candidateStates);
            return rendererGroup;
        }

        private RendererGroupIndex FindEquivalentRendererGroup(in RendererGroupRecord candidateRecord, NativeList<RendererStateIndex> candidateStates)
        {
            if (!m_RendererGroupByKey.TryGetFirstValue(candidateRecord.Key, out RendererGroupIndex rendererGroup, out var iterator))
                return RendererGroupIndex.None;

            do
            {
                if (IsRendererGroupEquivalent(rendererGroup, in candidateRecord, candidateStates))
                    return rendererGroup;
            } while (m_RendererGroupByKey.TryGetNextValue(out rendererGroup, ref iterator));

            return RendererGroupIndex.None;
        }

        private bool IsRendererGroupEquivalent(RendererGroupIndex rendererGroup, in RendererGroupRecord candidateRecord, NativeList<RendererStateIndex> candidateStates)
        {
            if (!rendererGroup.IsCreated || !m_RendererGroupAllocated.Contains(rendererGroup))
                return false;

            NativeBuffer<RendererStateIndex> existingStates = m_RendererGroupStates[rendererGroup];
            if (existingStates.Length != candidateStates.Length)
                return false;

            for (int i = 0; i < existingStates.Length; i++)
            {
                if (existingStates[i] != candidateStates[i])
                    return false;
            }

            return true;
        }

        private RendererGroupIndex CreateRendererGroup(in RendererGroupRecord groupRecord, NativeList<RendererStateIndex> rendererStates)
        {
            RendererGroupIndex rendererGroup = m_RendererGroupFreeList.Length > 0 ? m_RendererGroupFreeList.Pop() : m_NextRendererGroupId++;
            EnsureRendererGroupCapacity(rendererGroup + 1);
            EnsureRendererGroupLookupCapacity();

            m_RendererGroupAllocated.Add(rendererGroup);
            m_RendererGroupByKey.Add(groupRecord.Key, rendererGroup);
            m_RendererGroupRecords[rendererGroup] = groupRecord;
            m_RendererGroupStates[rendererGroup].CopyFrom(rendererStates.AsArray());

            int maxDrawIndex = GetMaxDrawIndex(rendererStates);
            int materialCapacity = GetEntityIdCapacity(rendererStates, m_RendererStateMaterialInstanceIds);
            int meshCapacity = GetEntityIdCapacity(rendererStates, m_RendererStateMeshInstanceIds);
            using var registeredDrawsSeen = new NativeBitSet(math.max(1, maxDrawIndex + 1), Allocator.Temp);
            using var cameraDrawsSeen = new NativeBitSet(math.max(1, maxDrawIndex + 1), Allocator.Temp);
            using var shadowDrawsSeen = new NativeBitSet(math.max(1, maxDrawIndex + 1), Allocator.Temp);
            using var materialsSeen = new NativeHashSet<EntityId>(math.max(1, materialCapacity), Allocator.Temp);
            using var meshesSeen = new NativeHashSet<EntityId>(math.max(1, meshCapacity), Allocator.Temp);

            for (int i = 0; i < rendererStates.Length; i++)
            {
                RendererStateIndex rendererState = rendererStates[i];
                RetainRendererState(rendererState);
                m_RendererGroupsByState.Add(rendererState, rendererGroup);

                AppendUniqueDrawIndices(m_RendererGroupRegisteredDrawIndices[rendererGroup], m_RendererStateRegisteredDrawIndices[rendererState], registeredDrawsSeen);
                AppendUniqueDrawIndices(m_RendererGroupCameraDrawIndices[rendererGroup], m_RendererStateCameraDrawIndices[rendererState], cameraDrawsSeen);
                AppendUniqueDrawIndices(m_RendererGroupShadowDrawIndices[rendererGroup], m_RendererStateShadowDrawIndices[rendererState], shadowDrawsSeen);
                AppendUniqueEntityIds(m_RendererGroupMaterialInstanceIds[rendererGroup], m_RendererStateMaterialInstanceIds[rendererState], materialsSeen);
                AppendUniqueEntityIds(m_RendererGroupMeshInstanceIds[rendererGroup], m_RendererStateMeshInstanceIds[rendererState], meshesSeen);
            }

            return rendererGroup;
        }

        private void RetainRendererGroup(RendererGroupIndex rendererGroup)
        {
            RendererGroupRecord record = m_RendererGroupRecords[rendererGroup];
            record.RefCount++;
            m_RendererGroupRecords[rendererGroup] = record;
        }

        private void ReleaseRendererGroup(RendererGroupIndex rendererGroup)
        {
            if (!rendererGroup.IsCreated || !m_RendererGroupAllocated.Contains(rendererGroup))
                return;

            RendererGroupRecord record = m_RendererGroupRecords[rendererGroup];
            record.RefCount--;
            Assert.IsTrue(record.RefCount >= 0, $"Renderer group refcount underflow: {rendererGroup}");
            m_RendererGroupRecords[rendererGroup] = record;
            if (record.RefCount == 0)
                DestroyRendererGroup(rendererGroup);
        }

        private void DestroyRendererGroup(RendererGroupIndex rendererGroup)
        {
            NativeBuffer<RendererStateIndex> states = m_RendererGroupStates[rendererGroup];
            for (int i = 0; i < states.Length; i++)
            {
                m_RendererGroupsByState.Remove(states[i], rendererGroup);
                ReleaseRendererState(states[i]);
            }

            m_RendererGroupByKey.Remove(m_RendererGroupRecords[rendererGroup].Key, rendererGroup);
            states.Clear();
            m_RendererGroupRegisteredDrawIndices[rendererGroup].Clear();
            m_RendererGroupCameraDrawIndices[rendererGroup].Clear();
            m_RendererGroupShadowDrawIndices[rendererGroup].Clear();
            m_RendererGroupMaterialInstanceIds[rendererGroup].Clear();
            m_RendererGroupMeshInstanceIds[rendererGroup].Clear();
            m_RendererGroupRecords[rendererGroup] = default;
            m_RendererGroupAllocated.Remove(rendererGroup);
            m_RendererGroupFreeList.Add(rendererGroup);
        }

        private TemplateLayoutIndex ResolveTemplateLayout(in TemplateLayoutRecord candidateRecord, NativeList<RendererGroupIndex> candidateGroups)
        {
            TemplateLayoutIndex templateLayout = FindEquivalentTemplateLayout(in candidateRecord, candidateGroups);
            if (!templateLayout.IsCreated)
                templateLayout = CreateTemplateLayout(in candidateRecord, candidateGroups);
            return templateLayout;
        }

        private TemplateLayoutIndex FindEquivalentTemplateLayout(in TemplateLayoutRecord candidateRecord, NativeList<RendererGroupIndex> candidateGroups)
        {
            if (!m_TemplateLayoutByKey.TryGetFirstValue(candidateRecord.Key, out TemplateLayoutIndex templateLayout, out var iterator))
                return TemplateLayoutIndex.None;

            do
            {
                if (IsTemplateLayoutEquivalent(templateLayout, in candidateRecord, candidateGroups))
                    return templateLayout;
            } while (m_TemplateLayoutByKey.TryGetNextValue(out templateLayout, ref iterator));

            return TemplateLayoutIndex.None;
        }

        private bool IsTemplateLayoutEquivalent(TemplateLayoutIndex templateLayout, in TemplateLayoutRecord candidateRecord, NativeList<RendererGroupIndex> candidateGroups)
        {
            if (!templateLayout.IsCreated || !m_TemplateLayoutAllocated.Contains(templateLayout))
                return false;

            TemplateLayoutRecord existingRecord = m_TemplateLayoutRecords[templateLayout];
            if (!existingRecord.Key.Equals(candidateRecord.Key))
                return false;

            NativeBuffer<RendererGroupIndex> existingGroups = m_TemplateLayoutGroups[templateLayout];
            if (existingGroups.Length != candidateGroups.Length)
                return false;

            for (int i = 0; i < existingGroups.Length; i++)
            {
                if (existingGroups[i] != candidateGroups[i])
                    return false;
            }

            return true;
        }

        private TemplateLayoutIndex CreateTemplateLayout(in TemplateLayoutRecord layoutRecord, NativeList<RendererGroupIndex> rendererGroups)
        {
            TemplateLayoutIndex templateLayout = m_TemplateLayoutFreeList.Length > 0 ? m_TemplateLayoutFreeList.Pop() : m_NextTemplateLayoutId++;
            EnsureTemplateLayoutCapacity(templateLayout + 1);
            EnsureTemplateLayoutLookupCapacity();

            m_TemplateLayoutAllocated.Add(templateLayout);
            m_TemplateLayoutByKey.Add(layoutRecord.Key, templateLayout);
            m_TemplateLayoutRecords[templateLayout] = layoutRecord;
            m_TemplateLayoutGroups[templateLayout].CopyFrom(rendererGroups.AsArray());

            int maxDrawIndex = GetMaxDrawIndex(rendererGroups);
            using var registeredDrawsSeen = new NativeBitSet(math.max(1, maxDrawIndex + 1), Allocator.Temp);
            using var cameraDrawsSeen = new NativeBitSet(math.max(1, maxDrawIndex + 1), Allocator.Temp);
            using var shadowDrawsSeen = new NativeBitSet(math.max(1, maxDrawIndex + 1), Allocator.Temp);

            for (int i = 0; i < rendererGroups.Length; i++)
            {
                RendererGroupIndex rendererGroup = rendererGroups[i];
                RetainRendererGroup(rendererGroup);
                m_TemplateLayoutsByGroup.Add(rendererGroup, templateLayout);

                AppendUniqueDrawIndices(m_TemplateLayoutRegisteredDrawIndices[templateLayout], m_RendererGroupRegisteredDrawIndices[rendererGroup], registeredDrawsSeen);
                AppendUniqueDrawIndices(m_TemplateLayoutCameraDrawIndices[templateLayout], m_RendererGroupCameraDrawIndices[rendererGroup], cameraDrawsSeen);
                AppendUniqueDrawIndices(m_TemplateLayoutShadowDrawIndices[templateLayout], m_RendererGroupShadowDrawIndices[rendererGroup], shadowDrawsSeen);
            }

            return templateLayout;
        }

        private void BindHandleToState(TemplateIndex template, GameObject source, TemplateLayoutIndex newLayout)
        {
            Assert.IsTrue(newLayout.IsCreated && m_TemplateLayoutAllocated.Contains(newLayout), "Cannot bind template to an invalid layout.");

            TemplateLayoutIndex oldLayout = m_TemplateLayoutBindings[template];
            if (oldLayout == newLayout)
            {
                m_TemplateRepresentativeRenderSourceIds[template] = source ? source.GetEntityId() : EntityId.None;
                return;
            }

            TemplateStateChangeMask changeMask = ComputeStateChangeMask(oldLayout, newLayout);

            RetainTemplateLayout(newLayout);
            m_TemplateLayoutBindings[template] = newLayout;
            ProjectLayoutToHandle(template, source, newLayout);

            if (oldLayout.IsCreated)
                ReleaseTemplateLayout(oldLayout);

            m_InstanceManager.ValueRW.OnTemplateHandleStateChanged(template, changeMask, oldLayout, newLayout);
        }

        private void UnbindHandleFromState(TemplateIndex template, bool notifyStateChange)
        {
            TemplateLayoutIndex oldLayout = m_TemplateLayoutBindings[template];
            if (!oldLayout.IsCreated)
            {
                ClearTemplateProjection(template);
                m_TemplateLayoutBindings[template] = TemplateLayoutIndex.None;
                ResetTemplateData(template);
                return;
            }

            TemplateStateChangeMask changeMask = ComputeStateChangeMask(oldLayout, TemplateLayoutIndex.None);
            ClearTemplateProjection(template);
            m_TemplateLayoutBindings[template] = TemplateLayoutIndex.None;
            ResetTemplateData(template);
            ReleaseTemplateLayout(oldLayout);

            if (notifyStateChange)
                m_InstanceManager.ValueRW.OnTemplateHandleStateChanged(template, changeMask, oldLayout, TemplateLayoutIndex.None);
        }

        private void ProjectLayoutToHandle(TemplateIndex template, GameObject source, TemplateLayoutIndex templateLayout)
        {
            ClearTemplateProjection(template);

            TemplateLayoutRecord layoutRecord = m_TemplateLayoutRecords[templateLayout];
            m_TemplateRepresentativeRenderSourceIds[template] = source ? source.GetEntityId() : EntityId.None;
            template.Type = layoutRecord.Type;
            template.Flags = layoutRecord.Flags;
            template.BatchDomainIndex = layoutRecord.BatchDomainIndex;
            template.InitialVariationColor = layoutRecord.InitialVariationColor;
            template.MaxRenderDistance = layoutRecord.MaxRenderDistance;
            template.MaxShadowDistance = layoutRecord.MaxShadowDistance;
            template.AffectedByGlobalDensity = layoutRecord.AffectedByGlobalDensity;
            template.AffectedByRangeDensity = layoutRecord.AffectedByRangeDensity;
            template.MinShadowLod = layoutRecord.MinShadowLod;
            template.LodCount = layoutRecord.LodCount;
            template.LodFadeMode = layoutRecord.LodFadeMode;
            template.HasAnimatedCrossFade = layoutRecord.HasAnimatedCrossFade;
            template.SupportsFadeKeyword = layoutRecord.SupportsFadeKeyword;
            template.LocalAnchorPoint = layoutRecord.LocalAnchorPoint;
            template.LocalReferencePoint = layoutRecord.LocalReferencePoint;
            template.LocalSize = layoutRecord.LocalSize;
            template.LocalAABB = layoutRecord.LocalAABB;

            m_TemplateDataArray[template] = layoutRecord.TemplateData;
            MarkTemplateDataDirty(template);

            for (int i = 0; i < CullingConstants.MaxLodCount; i++)
            {
                if (i < layoutRecord.LodCount)
                {
                    template.LODHeights[i] = i < 4 ? layoutRecord.LodHeights0To3[i] : layoutRecord.LodHeights4To7[i - 4];
                    template.LODTransitionHeights[i] = i < 4 ? layoutRecord.LodTransitionHeights0To3[i] : layoutRecord.LodTransitionHeights4To7[i - 4];
                }
                else
                {
                    template.LODHeights[i] = 0f;
                    template.LODTransitionHeights[i] = 0f;
                }
            }

            m_RegisteredDrawIndices[template].CopyFrom(m_TemplateLayoutRegisteredDrawIndices[templateLayout]);
            AddTemplateDrawOwnership(template, m_RegisteredDrawIndices[template]);

            m_CameraDrawIndices[template].CopyFrom(m_TemplateLayoutCameraDrawIndices[templateLayout]);
            m_ShadowDrawIndices[template].CopyFrom(m_TemplateLayoutShadowDrawIndices[templateLayout]);

            for (int i = 0; i < CullingConstants.MaxLodCount; i++)
            {
                m_CameraDrawIndicesPerLod[template * CullingConstants.MaxLodCount + i].Clear();
                m_ShadowDrawIndicesPerLod[template * CullingConstants.MaxLodCount + i].Clear();
            }

            NativeBuffer<RendererGroupIndex> groups = m_TemplateLayoutGroups[templateLayout];
            for (int i = 0; i < groups.Length; i++)
            {
                RendererGroupIndex rendererGroup = groups[i];
                byte lodIndex = m_RendererGroupRecords[rendererGroup].LodIndex;
                m_CameraDrawIndicesPerLod[template * CullingConstants.MaxLodCount + lodIndex].CopyFrom(m_RendererGroupCameraDrawIndices[rendererGroup]);
                m_ShadowDrawIndicesPerLod[template * CullingConstants.MaxLodCount + lodIndex].CopyFrom(m_RendererGroupShadowDrawIndices[rendererGroup]);
            }

            m_MaxUsedLodCount = math.max(m_MaxUsedLodCount, layoutRecord.LodCount);
        }

        private void ClearTemplateProjection(TemplateIndex template)
        {
            RemoveTemplateDrawOwnership(template, m_RegisteredDrawIndices[template]);
            m_RegisteredDrawIndices[template].Clear();
            m_CameraDrawIndices[template].Clear();
            m_ShadowDrawIndices[template].Clear();

            for (int i = 0; i < CullingConstants.MaxLodCount; i++)
            {
                m_CameraDrawIndicesPerLod[template * CullingConstants.MaxLodCount + i].Clear();
                m_ShadowDrawIndicesPerLod[template * CullingConstants.MaxLodCount + i].Clear();
            }
        }

        private TemplateStateChangeMask ComputeStateChangeMask(TemplateLayoutIndex oldLayout, TemplateLayoutIndex newLayout)
        {
            if (!oldLayout.IsCreated && !newLayout.IsCreated)
                return TemplateStateChangeMask.None;

            if (!oldLayout.IsCreated || !newLayout.IsCreated)
            {
                return TemplateStateChangeMask.DomainChanged |
                       TemplateStateChangeMask.DrawChanged |
                       TemplateStateChangeMask.TemplateDataChanged |
                       TemplateStateChangeMask.CapabilityChanged;
            }

            TemplateLayoutRecord oldRecord = m_TemplateLayoutRecords[oldLayout];
            TemplateLayoutRecord newRecord = m_TemplateLayoutRecords[newLayout];

            TemplateStateChangeMask changeMask = TemplateStateChangeMask.None;
            if (oldRecord.BatchDomainIndex != newRecord.BatchDomainIndex)
                changeMask |= TemplateStateChangeMask.DomainChanged;

            if (oldRecord.GroupSignature != newRecord.GroupSignature)
                changeMask |= TemplateStateChangeMask.DrawChanged;

            if (!TemplateDataEquals(in oldRecord.Key.TemplateData, in newRecord.Key.TemplateData))
                changeMask |= TemplateStateChangeMask.TemplateDataChanged;

            const TemplateRenderFlags capabilityMask =
                TemplateRenderFlags.HasLightProbes |
                TemplateRenderFlags.HasPerObjectMotionVectors |
                TemplateRenderFlags.HasRandomID |
                TemplateRenderFlags.HasVariationColor;

            if ((oldRecord.Flags & capabilityMask) != (newRecord.Flags & capabilityMask))
                changeMask |= TemplateStateChangeMask.CapabilityChanged;

            return changeMask;
        }

        private void RetainTemplateLayout(TemplateLayoutIndex templateLayout)
        {
            TemplateLayoutRecord record = m_TemplateLayoutRecords[templateLayout];
            record.RefCount++;
            m_TemplateLayoutRecords[templateLayout] = record;
        }

        private void ReleaseTemplateLayout(TemplateLayoutIndex templateLayout)
        {
            if (!templateLayout.IsCreated || !m_TemplateLayoutAllocated.Contains(templateLayout))
                return;

            TemplateLayoutRecord record = m_TemplateLayoutRecords[templateLayout];
            record.RefCount--;
            Assert.IsTrue(record.RefCount >= 0, $"Template layout refcount underflow: {templateLayout}");
            m_TemplateLayoutRecords[templateLayout] = record;
            if (record.RefCount == 0)
                DestroyTemplateLayout(templateLayout);
        }

        private void DestroyTemplateLayout(TemplateLayoutIndex templateLayout)
        {
            NativeBuffer<RendererGroupIndex> groups = m_TemplateLayoutGroups[templateLayout];
            for (int i = 0; i < groups.Length; i++)
            {
                m_TemplateLayoutsByGroup.Remove(groups[i], templateLayout);
                ReleaseRendererGroup(groups[i]);
            }

            m_TemplateLayoutByKey.Remove(m_TemplateLayoutRecords[templateLayout].Key, templateLayout);
            groups.Clear();
            m_TemplateLayoutRegisteredDrawIndices[templateLayout].Clear();
            m_TemplateLayoutCameraDrawIndices[templateLayout].Clear();
            m_TemplateLayoutShadowDrawIndices[templateLayout].Clear();
            m_TemplateLayoutRecords[templateLayout] = default;
            m_TemplateLayoutAllocated.Remove(templateLayout);
            m_TemplateLayoutFreeList.Add(templateLayout);
        }

        private void CopyUniqueDrawIndices(NativeBuffer<DrawBatchIndex> destination, NativeList<DrawBatchIndex> source)
        {
            destination.Clear();
            int maxDrawIndex = GetMaxDrawIndex(source);
            using var seen = new NativeBitSet(math.max(1, maxDrawIndex + 1), Allocator.Temp);
            AppendUniqueDrawIndices(destination, source, seen);
        }

        private void CopyUniqueEntityIds(NativeBuffer<EntityId> destination, NativeList<EntityId> source)
        {
            destination.Clear();
            using var seen = new NativeHashSet<EntityId>(math.max(1, source.Length), Allocator.Temp);
            AppendUniqueEntityIds(destination, source, seen);
        }

        private void AppendUniqueDrawIndices(NativeBuffer<DrawBatchIndex> destination, NativeBuffer<DrawBatchIndex> source, NativeBitSet seen)
        {
            for (int i = 0; i < source.Length; i++)
            {
                DrawBatchIndex drawIndex = source[i];
                if (seen.TryAdd(drawIndex))
                    destination.Add(drawIndex);
            }
        }

        private void AppendUniqueDrawIndices(NativeBuffer<DrawBatchIndex> destination, NativeList<DrawBatchIndex> source, NativeBitSet seen)
        {
            for (int i = 0; i < source.Length; i++)
            {
                DrawBatchIndex drawIndex = source[i];
                if (seen.TryAdd(drawIndex))
                    destination.Add(drawIndex);
            }
        }

        private void AppendUniqueEntityIds(NativeBuffer<EntityId> destination, NativeBuffer<EntityId> source, NativeHashSet<EntityId> seen)
        {
            for (int i = 0; i < source.Length; i++)
            {
                EntityId entityId = source[i];
                if (seen.Add(entityId))
                    destination.Add(entityId);
            }
        }

        private void AppendUniqueEntityIds(NativeBuffer<EntityId> destination, NativeList<EntityId> source, NativeHashSet<EntityId> seen)
        {
            for (int i = 0; i < source.Length; i++)
            {
                EntityId entityId = source[i];
                if (seen.Add(entityId))
                    destination.Add(entityId);
            }
        }

        private static int GetMaxDrawIndex(NativeList<DrawBatchIndex> drawIndices)
        {
            int maxDrawIndex = -1;
            for (int i = 0; i < drawIndices.Length; i++)
                maxDrawIndex = math.max(maxDrawIndex, (int)drawIndices[i]);

            return maxDrawIndex;
        }

        private int GetMaxDrawIndex(NativeList<RendererStateIndex> rendererStates)
        {
            int maxDrawIndex = -1;
            for (int i = 0; i < rendererStates.Length; i++)
            {
                maxDrawIndex = GetMaxDrawIndex(m_RendererStateRegisteredDrawIndices[rendererStates[i]], maxDrawIndex);
                maxDrawIndex = GetMaxDrawIndex(m_RendererStateCameraDrawIndices[rendererStates[i]], maxDrawIndex);
                maxDrawIndex = GetMaxDrawIndex(m_RendererStateShadowDrawIndices[rendererStates[i]], maxDrawIndex);
            }

            return maxDrawIndex;
        }

        private int GetMaxDrawIndex(NativeList<RendererGroupIndex> rendererGroups)
        {
            int maxDrawIndex = -1;
            for (int i = 0; i < rendererGroups.Length; i++)
            {
                maxDrawIndex = GetMaxDrawIndex(m_RendererGroupRegisteredDrawIndices[rendererGroups[i]], maxDrawIndex);
                maxDrawIndex = GetMaxDrawIndex(m_RendererGroupCameraDrawIndices[rendererGroups[i]], maxDrawIndex);
                maxDrawIndex = GetMaxDrawIndex(m_RendererGroupShadowDrawIndices[rendererGroups[i]], maxDrawIndex);
            }

            return maxDrawIndex;
        }

        private static int GetMaxDrawIndex(NativeBuffer<DrawBatchIndex> drawIndices, int maxDrawIndex = -1)
        {
            for (int i = 0; i < drawIndices.Length; i++)
                maxDrawIndex = math.max(maxDrawIndex, (int)drawIndices[i]);

            return maxDrawIndex;
        }

        private int GetEntityIdCapacity(NativeList<RendererStateIndex> rendererStates, NativeBufferArray<EntityId> sourceBuffers)
        {
            int capacity = 0;
            for (int i = 0; i < rendererStates.Length; i++)
                capacity += sourceBuffers[rendererStates[i]].Length;

            return capacity;
        }

        private void EnsureSourceRecordCapacity(int minCapacity)
        {
            if (minCapacity <= m_SourceRecords.Length)
                return;

            int newCapacity = math.max(minCapacity, m_SourceRecords.Length * 2);
            m_SourceRecordAllocated.ReserveCapacity(newCapacity);
            m_SourceRecords.ResizeArraySafe(newCapacity);
            m_SourceRecordComponentIds.Resize(newCapacity);
            m_SourceRecordRendererIds.Resize(newCapacity);
            m_SourceRecordTemplates.Resize(newCapacity);
            m_SourceRecordInstances.Resize(newCapacity);
        }

        private void EnsureRendererStateCapacity(int minCapacity)
        {
            if (minCapacity <= m_RendererStateRecords.Length)
                return;

            int newCapacity = math.max(minCapacity, m_RendererStateRecords.Length * 2);
            m_RendererStateAllocated.ReserveCapacity(newCapacity);
            m_RendererStateRecords.ResizeArraySafe(newCapacity);
            m_RendererStateDrawDescriptors.Resize(newCapacity);
            m_RendererStateRegisteredDrawIndices.Resize(newCapacity);
            m_RendererStateCameraDrawIndices.Resize(newCapacity);
            m_RendererStateShadowDrawIndices.Resize(newCapacity);
            m_RendererStateMaterialInstanceIds.Resize(newCapacity);
            m_RendererStateMeshInstanceIds.Resize(newCapacity);
        }

        private void EnsureRendererStateLookupCapacity(int additionalEntries = 1)
        {
            int requiredCapacity = m_RendererStateByKey.Count() + additionalEntries;
            if (requiredCapacity > m_RendererStateByKey.Capacity)
                m_RendererStateByKey.Capacity = math.max(requiredCapacity, m_RendererStateByKey.Capacity * 2);

            requiredCapacity = m_RendererStatesByMaterial.Count() + additionalEntries;
            if (requiredCapacity > m_RendererStatesByMaterial.Capacity)
                m_RendererStatesByMaterial.Capacity = math.max(requiredCapacity, m_RendererStatesByMaterial.Capacity * 2);

            requiredCapacity = m_RendererStatesByMesh.Count() + additionalEntries;
            if (requiredCapacity > m_RendererStatesByMesh.Capacity)
                m_RendererStatesByMesh.Capacity = math.max(requiredCapacity, m_RendererStatesByMesh.Capacity * 2);
        }

        private void EnsureRendererGroupCapacity(int minCapacity)
        {
            if (minCapacity <= m_RendererGroupRecords.Length)
                return;

            int newCapacity = math.max(minCapacity, m_RendererGroupRecords.Length * 2);
            m_RendererGroupAllocated.ReserveCapacity(newCapacity);
            m_RendererGroupRecords.ResizeArraySafe(newCapacity);
            m_RendererGroupStates.Resize(newCapacity);
            m_RendererGroupRegisteredDrawIndices.Resize(newCapacity);
            m_RendererGroupCameraDrawIndices.Resize(newCapacity);
            m_RendererGroupShadowDrawIndices.Resize(newCapacity);
            m_RendererGroupMaterialInstanceIds.Resize(newCapacity);
            m_RendererGroupMeshInstanceIds.Resize(newCapacity);
        }

        private void EnsureRendererGroupLookupCapacity(int additionalEntries = 1)
        {
            int requiredCapacity = m_RendererGroupByKey.Count() + additionalEntries;
            if (requiredCapacity > m_RendererGroupByKey.Capacity)
                m_RendererGroupByKey.Capacity = math.max(requiredCapacity, m_RendererGroupByKey.Capacity * 2);

            requiredCapacity = m_RendererGroupsByState.Count() + additionalEntries;
            if (requiredCapacity > m_RendererGroupsByState.Capacity)
                m_RendererGroupsByState.Capacity = math.max(requiredCapacity, m_RendererGroupsByState.Capacity * 2);
        }

        private void EnsureTemplateLayoutCapacity(int minCapacity)
        {
            if (minCapacity <= m_TemplateLayoutRecords.Length)
                return;

            int newCapacity = math.max(minCapacity, m_TemplateLayoutRecords.Length * 2);
            m_TemplateLayoutAllocated.ReserveCapacity(newCapacity);
            m_TemplateLayoutBindings.ResizeArraySafe(newCapacity);
            m_TemplateLayoutRecords.ResizeArraySafe(newCapacity);
            m_TemplateLayoutGroups.Resize(newCapacity);
            m_TemplateLayoutRegisteredDrawIndices.Resize(newCapacity);
            m_TemplateLayoutCameraDrawIndices.Resize(newCapacity);
            m_TemplateLayoutShadowDrawIndices.Resize(newCapacity);
        }

        private void EnsureTemplateLayoutLookupCapacity(int additionalEntries = 1)
        {
            int requiredCapacity = m_TemplateLayoutByKey.Count() + additionalEntries;
            if (requiredCapacity > m_TemplateLayoutByKey.Capacity)
                m_TemplateLayoutByKey.Capacity = math.max(requiredCapacity, m_TemplateLayoutByKey.Capacity * 2);

            requiredCapacity = m_TemplateLayoutsByGroup.Count() + additionalEntries;
            if (requiredCapacity > m_TemplateLayoutsByGroup.Capacity)
                m_TemplateLayoutsByGroup.Capacity = math.max(requiredCapacity, m_TemplateLayoutsByGroup.Capacity * 2);
        }
    }
}
