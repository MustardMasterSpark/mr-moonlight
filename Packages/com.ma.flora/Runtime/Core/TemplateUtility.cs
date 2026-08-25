// Copyright © Magnetic Arcade. All Rights Reserved.

using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace MA.Flora
{
    internal enum TemplateRenderType
    {
        LodGroup,
        MeshRenderer,
        MeshLod,
        Billboard,
    }

    internal enum TemplateLightmapValidationError : byte
    {
        None = 0,
        MixedStaticLightingModes,
        MixedLightmapIndices,
        MixedLightmapScaleOffsets,
    }

    [Flags]
    internal enum TemplateRenderFlags : byte
    {
        None                      = 0,
        HasLightProbes            = 1 << 1,
        HasLightmaps              = 1 << 2,
        HasShadowCasters          = 1 << 3,
        HasPerObjectMotionVectors = 1 << 4,
        HasRandomID               = 1 << 5,
        HasVariationColor         = 1 << 6,
    }

    internal unsafe struct TemplateSourceInfo
    {
        public GameObject RenderSource;
        public FloraAdditionalRendererSettings AdditionalRendererSettings;
        public TemplateRenderType Type;
        public TemplateRenderFlags Flags;
        public Vector3 LocalAnchorPoint;
        public int LodCount;
        public LODFadeMode FadeMode;
        public bool HasAnyCrossFade => SupportsFadeKeyword && FadeMode != LODFadeMode.None;
        public bool HasAnimatedCrossFade;
        public bool SupportsFadeKeyword;
        public Vector3 LocalReferencePoint;
        public float LocalSize;
        public AABB LocalAABB;
        public bool LastLODIsBillboard;
        public int MeshLodForceLod;
        public float MeshLodSelectionBias;
        public float MeshLodBias;
        public float MeshLodSlope;
        public int LightmapIndex;
        public float4 LightmapScaleOffset;
        public TemplateLightmapValidationError LightmapValidationError;
        public fixed bool PercentageFlags[8];
        public fixed bool LODHasShadows[8];
        public fixed float LODHeights[8];
        public fixed float LODTransitionHeights[8];
    }

    internal static unsafe class TemplateUtility
    {
        private static class FrameCache
        {
            public static Dictionary<GameObject, LOD[]> LODCache = new();
            public static Dictionary<GameObject, MeshRenderer[]> FirstLODMeshRendererCache = new();
            public static Dictionary<GameObject, TemplateSourceInfo> TemplateInfoCache = new();
            public static Dictionary<GameObject, AxisAlignedBox> LocalBoundsCache = new();
            public static Dictionary<GameObject, AxisAlignedBox> WorldBoundsCache = new();
            public static Dictionary<GameObject, BoundingSphere> LowerBoundsCache = new();

            public static void Clear()
            {
                LODCache.Clear();
                FirstLODMeshRendererCache.Clear();
                TemplateInfoCache.Clear();
                LocalBoundsCache.Clear();
                WorldBoundsCache.Clear();
                LowerBoundsCache.Clear();
            }
        }

        private static readonly List<Mesh> MeshBuffer = new();
        private static readonly List<MeshRenderer> MeshRendererBuffer = new();

        public static void NextFrame()
        {
            FrameCache.Clear();
        }

        public static LOD[] GetLODs(this GameObject gameObject)
        {
            if (FrameCache.LODCache.TryGetValue(gameObject, out LOD[] lods))
                return lods;

            if (gameObject.TryGetComponent(out LODGroup lodGroup) && lodGroup.lodCount > 0)
            {
                lods = lodGroup.GetLODs();
            }
            else if (gameObject.TryGetComponent(out MeshRenderer meshRenderer) &&
                     gameObject.TryGetComponent(out MeshFilter meshFilter))
            {
                Mesh mesh = meshFilter.sharedMesh;
                if (mesh == null)
                {
                    lods = Array.Empty<LOD>();
                }
#if UNITY_6000_2_OR_NEWER
                else if (mesh.lodCount > 1)
                {
                    var lodCount = math.min(mesh.lodCount, 8);
                    lods = new LOD[lodCount];
                    for (int i = 0; i < lodCount; i++)
                    {
                        lods[i] = new LOD(0.0001f, new []{ meshRenderer });
                    }
                }
#endif
                else
                {
                    lods = new []{ new LOD(0.0001f, new []{ meshRenderer }) };
                }
            }
            else if (gameObject.TryGetComponent(out BillboardRenderer billboardRenderer))
            {
                lods = new []{ new LOD(0.0001f, new []{ billboardRenderer }) };
            }
            else
            {
                lods = Array.Empty<LOD>();
            }

            FrameCache.LODCache[gameObject] = lods;
            return lods;
        }

        public static MeshRenderer[] GetMeshRenderersForFirstLOD(this GameObject gameObject)
        {
            if (FrameCache.FirstLODMeshRendererCache.TryGetValue(gameObject, out MeshRenderer[] meshRenderers))
                return meshRenderers;

            LOD[] lods = gameObject.GetLODs();

            MeshRendererBuffer.Clear();
            foreach (Renderer renderer in lods[0].renderers)
            {
                if (renderer is MeshRenderer meshRenderer)
                {
                    MeshRendererBuffer.Add(meshRenderer);
                }
            }

            MeshRenderer[] lod0Renderers = MeshRendererBuffer.ToArray();
            FrameCache.FirstLODMeshRendererCache[gameObject] = lod0Renderers;
            return lod0Renderers;
        }

        public static TemplateSourceInfo ComputeTemplateSourceInfo(this GameObject gameObject)
        {
            if (gameObject == null)
                return default;

            if (FrameCache.TemplateInfoCache.TryGetValue(gameObject, out TemplateSourceInfo renderInfo))
                return renderInfo;

            renderInfo = new TemplateSourceInfo();
            renderInfo.RenderSource = gameObject;
            if (!gameObject.TryGetComponent(out renderInfo.AdditionalRendererSettings))
                renderInfo.AdditionalRendererSettings = ComponentSingleton<FloraAdditionalRendererSettings>.instance;

            renderInfo.Flags = TemplateRenderFlags.None;
            if ((renderInfo.AdditionalRendererSettings.AdditionalPerInstanceData & FloraAdditionalPerInstanceData.RandomID) != 0)
                renderInfo.Flags |= TemplateRenderFlags.HasRandomID;
            if ((renderInfo.AdditionalRendererSettings.AdditionalPerInstanceData & FloraAdditionalPerInstanceData.VariationColor) != 0)
                renderInfo.Flags |= TemplateRenderFlags.HasVariationColor;

            LOD[] lodGroupLods;
            if (gameObject.TryGetComponent(out LODGroup lodGroup))
            {
                lodGroupLods = gameObject.GetLODs();
                renderInfo.Type = TemplateRenderType.LodGroup;
                renderInfo.LodCount = (byte)lodGroup.lodCount;
                renderInfo.FadeMode = lodGroup.fadeMode;
                renderInfo.LocalSize = lodGroup.size;
                renderInfo.LocalReferencePoint = lodGroup.localReferencePoint;
                renderInfo.LastLODIsBillboard = lodGroup.lastLODBillboard;
                renderInfo.HasAnimatedCrossFade = lodGroup.fadeMode == LODFadeMode.CrossFade && lodGroup.animateCrossFading;
            }
            else if (gameObject.TryGetComponent(out MeshRenderer meshRenderer) &&
                     gameObject.TryGetComponent(out MeshFilter meshFilter))
            {
                Mesh mesh = meshFilter.sharedMesh;
                if (mesh == null)
                {
                    Debug.Log($"GameObject '{gameObject.name}' has a MeshRenderer without a valid MeshFilter. " +
                              "This GameObject can not be rendered by Flora.");
                    return default;
                }

#if UNITY_6000_2_OR_NEWER
                if (mesh.lodCount > 1)
                {
                    renderInfo.Type = TemplateRenderType.MeshLod;
                    renderInfo.LodCount = math.min(mesh.lodCount, 8);
                    renderInfo.MeshLodBias = mesh.lodSelectionCurve.lodBias;
                    renderInfo.MeshLodSlope = mesh.lodSelectionCurve.lodSlope;
                    renderInfo.MeshLodForceLod = meshRenderer.forceMeshLod;
                    renderInfo.MeshLodSelectionBias = meshRenderer.meshLodSelectionBias;
                }
                else
#endif
                {
                    renderInfo.Type = TemplateRenderType.MeshRenderer;
                    renderInfo.LodCount = 1;
                }

                lodGroupLods = gameObject.GetLODs();
            }
            else if (gameObject.TryGetComponent(out BillboardRenderer billboardRenderer))
            {
                renderInfo.Type = TemplateRenderType.Billboard;
                renderInfo.LodCount = 1;
                lodGroupLods = new LOD[1];
                lodGroupLods[0] = new LOD(0.000001f, new []{ billboardRenderer });
            }
            else
            {
                Debug.Log($"GameObject '{gameObject.name}' does not have a LODGroup or Renderer component. " +
                          "This GameObject can not be rendered by Flora.");
                return default;
            }

            int lodCount = math.clamp(renderInfo.LodCount, 1, 8);
            bool useDitheringCrossFade = renderInfo.FadeMode != LODFadeMode.None;
            bool useSpeedTreeCrossFade = renderInfo.FadeMode == LODFadeMode.SpeedTree;

            int crossFadeLODBegin = 0;
            if (useSpeedTreeCrossFade)
            {
                int lastLODIndex = lodCount - 1;
                bool hasBillboardLOD = lodCount > 0 && renderInfo.LastLODIsBillboard &&
                                       lodGroupLods[lastLODIndex].renderers.Length == 1;

                if (lodCount == 0)
                    crossFadeLODBegin = 0;
                else if (hasBillboardLOD)
                    crossFadeLODBegin = math.max(lodCount, 2) - 2;
                else
                    crossFadeLODBegin = lodCount - 1;
            }

            AxisAlignedBox localAABB = AxisAlignedBox.Empty;
            Transform rootTransform = gameObject.transform;
            bool hasAnchor = false;
            float3 localAnchorPoint = float3.zero;
            bool supportsFadeKeyword = true;
            bool hasStaticLightingMode = false;
            StaticLightingRenderMode rootStaticLightingMode = StaticLightingRenderMode.None;
            bool hasLightmapBinding = false;
            int rootLightmapIndex = -1;
            float4 rootLightmapScaleOffset = default;

            for (int lodIndex = 0; lodIndex < lodCount; lodIndex++)
            {
                LOD lod = lodGroupLods[lodIndex];

                float lodHeight = lod.screenRelativeTransitionHeight;
                renderInfo.LODHeights[lodIndex] = lodHeight;
                renderInfo.LODTransitionHeights[lodIndex] = lodHeight;

                renderInfo.PercentageFlags[lodIndex] = false;
                if (useSpeedTreeCrossFade && lodIndex < crossFadeLODBegin)
                {
                    // SpeedTree cross-fade is not used when the last LOD is a billboard.
                    renderInfo.PercentageFlags[lodIndex] = true;
                }
                else if (useDitheringCrossFade && lodIndex >= crossFadeLODBegin)
                {
                    float fadeTransitionWidth = lod.fadeTransitionWidth;
                    float prevLODHeight = lodIndex > 0 ? lodGroupLods[lodIndex - 1].screenRelativeTransitionHeight : 1.0f;
                    float transitionHeight = lodHeight + fadeTransitionWidth * (prevLODHeight - lodHeight);
                    renderInfo.LODTransitionHeights[lodIndex] = transitionHeight;
                }

                for (int i = 0; i < lod.renderers.Length; i++)
                {
                    Renderer renderer = lod.renderers[i];
                    if (renderer == null)
                        continue;

                    if (!hasAnchor && renderer.probeAnchor)
                    {
                        hasAnchor = true;
                        localAnchorPoint = GetTransformPositionInRootSpace(rootTransform, renderer.probeAnchor);
                    }

                    StaticLightingRenderMode staticLightingMode = CullingUtility.StaticLightingModeFromRenderer(renderer);
                    if (!ValidateStaticLightingConfiguration(
                            ref renderInfo,
                            staticLightingMode,
                            renderer.lightmapIndex,
                            renderer.lightmapScaleOffset,
                            ref hasStaticLightingMode,
                            ref rootStaticLightingMode,
                            ref hasLightmapBinding,
                            ref rootLightmapIndex,
                            ref rootLightmapScaleOffset))
                    {
                        continue;
                    }

                    if (staticLightingMode == StaticLightingRenderMode.LightMapped)
                        renderInfo.Flags |= TemplateRenderFlags.HasLightmaps;
                    else if (staticLightingMode == StaticLightingRenderMode.LightProbes)
                        renderInfo.Flags |= TemplateRenderFlags.HasLightProbes;

                    if (renderer.shadowCastingMode != ShadowCastingMode.Off)
                    {
                        renderInfo.Flags |= TemplateRenderFlags.HasShadowCasters;
                        renderInfo.LODHasShadows[lodIndex] = true;
                    }

                    if (renderer.motionVectorGenerationMode == MotionVectorGenerationMode.Object)
                        renderInfo.Flags |= TemplateRenderFlags.HasPerObjectMotionVectors;

                    AxisAlignedBox rendererBounds = GetRendererLocalBounds(renderer);
                    Matrix4x4 rendererToRootSpace = GetTransformToRootSpace(rootTransform, renderer.transform);
                    AxisAlignedBox aabbInRootSpace = rendererBounds.TransformBy(rendererToRootSpace);
                    localAABB += aabbInRootSpace;

                    Material[] materials = renderer.sharedMaterials;
                    foreach (Material material in materials)
                    {
                        if (material == null || material.shader == null)
                            continue;

                        bool materialHasFadeKeyword = material.shader.keywordSpace.FindKeyword("LOD_FADE_CROSSFADE") is { isValid: true, isOverridable: true };
                        if (!materialHasFadeKeyword && useDitheringCrossFade)
                        {
                            Debug.LogWarning($"Flora: Material '{material.name}' does not support LOD_FADE_CROSSFADE keyword. Crossfade will be disabled.", material);
                        }

                        supportsFadeKeyword &= materialHasFadeKeyword;
                    }
                }
            }

            renderInfo.LocalAnchorPoint = localAnchorPoint;
            renderInfo.LocalAABB = localAABB;
            renderInfo.SupportsFadeKeyword = supportsFadeKeyword;
            if (renderInfo.Type != TemplateRenderType.LodGroup)
                renderInfo.LocalSize = localAABB.MaxDim;

            if (!renderInfo.SupportsFadeKeyword && useDitheringCrossFade)
            {
                renderInfo.FadeMode = LODFadeMode.None;
                renderInfo.HasAnimatedCrossFade = false;
            }

            renderInfo.LightmapIndex = rootLightmapIndex;
            renderInfo.LightmapScaleOffset = rootLightmapScaleOffset;

            FrameCache.TemplateInfoCache[gameObject] = renderInfo;

            return renderInfo;
        }

        public static bool TryGetInstanceRendererSupportError(this GameObject gameObject, out string error)
        {
            error = null;

            TemplateSourceInfo templateSourceInfo = gameObject.ComputeTemplateSourceInfo();
            if (templateSourceInfo.RenderSource == null || templateSourceInfo.LodCount <= 0)
            {
                error = "Flora could not derive a valid renderable source from this object.";
                return false;
            }

            LOD[] lods = gameObject.GetLODs();
            for (int lodIndex = 0; lodIndex < lods.Length; lodIndex++)
            {
                Renderer[] renderers = lods[lodIndex].renderers;
                for (int i = 0; i < renderers.Length; i++)
                {
                    Renderer renderer = renderers[i];
                    if (renderer == null)
                        continue;

                    if (!TryGetStableRendererLocalBounds(renderer, out _))
                    {
                        error = $"FloraInstanceRenderer does not support renderer type '{renderer.GetType().Name}' on '{renderer.name}'. " +
                                "Use MeshRenderer, SkinnedMeshRenderer, or BillboardRenderer only.";
                        return false;
                    }
                }
            }

            switch (templateSourceInfo.LightmapValidationError)
            {
                case TemplateLightmapValidationError.None:
                    return true;
                case TemplateLightmapValidationError.MixedStaticLightingModes:
                    error = "FloraInstanceRenderer does not support mixed baked-lighting modes within a single root. Use consistent light probes or one shared lightmap binding.";
                    return false;
                case TemplateLightmapValidationError.MixedLightmapIndices:
                    error = "FloraInstanceRenderer does not support multiple baked lightmap indices within a single root.";
                    return false;
                case TemplateLightmapValidationError.MixedLightmapScaleOffsets:
                    error = "FloraInstanceRenderer does not support multiple baked lightmap scale/offset values within a single root.";
                    return false;
                default:
                    return true;
            }
        }

        private static bool ValidateStaticLightingConfiguration(
            ref TemplateSourceInfo renderInfo,
            StaticLightingRenderMode staticLightingMode,
            int rendererLightmapIndex,
            Vector4 rendererLightmapScaleOffset,
            ref bool hasStaticLightingMode,
            ref StaticLightingRenderMode rootStaticLightingMode,
            ref bool hasLightmapBinding,
            ref int rootLightmapIndex,
            ref float4 rootLightmapScaleOffset)
        {
            if (staticLightingMode != StaticLightingRenderMode.None)
            {
                hasStaticLightingMode = true;
                if (rootStaticLightingMode == StaticLightingRenderMode.None)
                {
                    rootStaticLightingMode = staticLightingMode;
                }
                else if (rootStaticLightingMode != staticLightingMode)
                {
                    renderInfo.LightmapValidationError = TemplateLightmapValidationError.MixedStaticLightingModes;
                    return false;
                }
            }
            else if (!hasStaticLightingMode)
            {
                hasStaticLightingMode = true;
            }

            if (staticLightingMode != StaticLightingRenderMode.LightMapped)
                return renderInfo.LightmapValidationError == TemplateLightmapValidationError.None;

            if (!hasLightmapBinding)
            {
                hasLightmapBinding = true;
                rootLightmapIndex = rendererLightmapIndex;
                rootLightmapScaleOffset = rendererLightmapScaleOffset;
                return true;
            }

            if (rootLightmapIndex != rendererLightmapIndex)
            {
                renderInfo.LightmapValidationError = TemplateLightmapValidationError.MixedLightmapIndices;
                return false;
            }

            if (!math.all(rootLightmapScaleOffset == (float4)rendererLightmapScaleOffset))
            {
                renderInfo.LightmapValidationError = TemplateLightmapValidationError.MixedLightmapScaleOffsets;
                return false;
            }

            return true;
        }

        private static AxisAlignedBox GetRendererLocalBounds(Renderer renderer)
        {
            if (TryGetStableRendererLocalBounds(renderer, out AxisAlignedBox bounds))
                return bounds;

            return renderer.bounds;
        }

        private static float3 GetTransformPositionInRootSpace(Transform root, Transform target)
        {
            Matrix4x4 targetToRoot = GetTransformToRootSpace(root, target);
            return targetToRoot.MultiplyPoint3x4(Vector3.zero);
        }

        private static Matrix4x4 GetTransformToRootSpace(Transform root, Transform target)
        {
            if (root == null || target == null)
                return Matrix4x4.identity;

            Matrix4x4 targetToRoot = Matrix4x4.identity;
            Transform current = target;

            while (current != null && current != root)
            {
                targetToRoot = Matrix4x4.TRS(current.localPosition, current.localRotation, current.localScale) * targetToRoot;
                current = current.parent;
            }

            if (current == root)
                return targetToRoot;

            // Fallback for unexpected hierarchies where the renderer is not parented under the source root.
            return root.worldToLocalMatrix * target.localToWorldMatrix;
        }

        private static bool TryGetStableRendererLocalBounds(Renderer renderer, out AxisAlignedBox bounds)
        {
            if (renderer is BillboardRenderer)
            {
                bounds = renderer.TryGetComponent(out TerrainDetailPlaceholder _)
                    ? CullingUtility.GetTerrainDetailBillboardMesh().bounds
                    : CullingUtility.GetBillboardMesh().bounds;
                return true;
            }

            if (renderer is SkinnedMeshRenderer skinnedMeshRenderer)
            {
                bounds = skinnedMeshRenderer.localBounds;
                return true;
            }

            if (renderer.TryGetComponent(out MeshFilter meshFilter) && meshFilter.sharedMesh)
            {
                bounds = meshFilter.sharedMesh.bounds;
                return true;
            }

            bounds = default;
            return false;
        }

        public static BoundingSphere CalculateLowestBoundingSphere(this GameObject gameObject)
        {
            if (gameObject == null)
                return default;

            if (FrameCache.LowerBoundsCache.TryGetValue(gameObject, out BoundingSphere lowestBoundingSphere))
                return lowestBoundingSphere;

            MeshRenderer[] meshRenderers = GetMeshRenderersForFirstLOD(gameObject);
            if (meshRenderers.Length == 0)
                return default;

            MeshBuffer.Clear();
            foreach (MeshRenderer meshRenderer in meshRenderers)
            {
                if (!meshRenderer.TryGetComponent(out MeshFilter meshFilter) || !meshFilter.sharedMesh)
                    continue;

                MeshBuffer.Add(meshFilter.sharedMesh);
            }

            Mesh.MeshDataArray meshDataArray;
#if UNITY_EDITOR
            meshDataArray = UnityEditor.MeshUtility.AcquireReadOnlyMeshData(MeshBuffer);
#else
            meshDataArray = Mesh.AcquireReadOnlyMeshData(MeshBuffer);
#endif

            if (meshDataArray.Length == 0)
                return default;

            float minX = float.MaxValue; float minZ = float.MaxValue;
            float maxX = float.MinValue; float maxZ = float.MinValue;
            AxisAlignedBox combinedBounds = AxisAlignedBox.Empty;

            for (int i = 0; i < meshDataArray.Length; i++)
            {
                Mesh.MeshData meshData = meshDataArray[i];
                NativeArray<float3> vertices = new NativeArray<float3>(meshData.vertexCount, Allocator.Temp);
                meshData.GetVertices(vertices.Reinterpret<Vector3>());

                Vector3 meshScale = meshRenderers[i].transform.lossyScale;
                AxisAlignedBox bounds = AxisAlignedBox.Empty;
                for (int subMeshIndex = 0; subMeshIndex < meshData.subMeshCount; subMeshIndex++)
                {
                    SubMeshDescriptor subMesh = meshData.GetSubMesh(subMeshIndex);
                    bounds += subMesh.bounds;
                }

                bounds.Extent *= meshScale;
                combinedBounds += bounds;

                // Calculate the bottom 10% of the mesh (configurable?)
                AxisAlignedBox meshLowerBounds = bounds;
                meshLowerBounds.Max.y = meshLowerBounds.Min.y + (meshLowerBounds.Max.y - meshLowerBounds.Min.y) * 0.1f;

                // Iterate over all vertices and find the min/max X and Z values in the lower 10% of the mesh
                for (int vertexIndex = 0; vertexIndex < vertices.Length; ++vertexIndex)
                {
                    float3 vertex = vertices[vertexIndex] * meshScale;
                    if (vertex.y < meshLowerBounds.Max.y)
                    {
                        minX = math.min(vertex.x, minX);
                        maxX = math.max(vertex.x, maxX);

                        minZ = math.min(vertex.z, minZ);
                        maxZ = math.max(vertex.z, maxZ);
                    }
                }
            }

            BoundingSphere lowBoundingSphere = new BoundingSphere
            {
                radius = math.sqrt(math.lengthsq(maxX - minX) + math.lengthsq(maxZ - minZ)) * 0.5f,
                position = new float3((minX + maxX) * 0.5f, combinedBounds.Min.y, (minZ + maxZ) * 0.5f)
            };

            FrameCache.LowerBoundsCache[gameObject] = lowBoundingSphere;

            return lowBoundingSphere;
        }
    }
}
