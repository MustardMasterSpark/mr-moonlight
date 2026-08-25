// Copyright © Magnetic Arcade. All Rights Reserved.

using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.Categorization;
using UnityEngine.Rendering;
using UnityEngine.Serialization;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Rendering;
#endif

namespace MA.Flora
{
    /// <summary>
    /// A <see cref="IRenderPipelineResources"/> setting for all shaders used by Flora at runtime.
    /// </summary>
    [Serializable]
    [SupportedOnRenderPipeline]
    [CategoryInfo(Name = "Flora", Order = 1000)]
    public class FloraRuntimeResources : IRenderPipelineResources
    {
        public enum Version
        {
            Initial,
            Count,
            Latest = Count - 1
        }

        [SerializeField, HideInInspector] private Version m_Version = Version.Latest;

        /// <inheritdoc />
        bool IRenderPipelineGraphicsSettings.isAvailableInPlayerBuild => true;

        /// <inheritdoc />
        int IRenderPipelineGraphicsSettings.version => (int)m_Version;

        #region Buffers

        [Header("Buffers")]
        [SerializeField, ResourcePath("Runtime/Graphics/GraphicsBufferUtility.compute")]
        private ComputeShader m_GraphicsBufferUtilityCS;

        public ComputeShader GraphicsBufferUtilityCS
        {
            get => m_GraphicsBufferUtilityCS;
            set => this.SetValueAndNotify(ref m_GraphicsBufferUtilityCS, value);
        }

        [SerializeField, ResourcePath("Runtime/Core/InstanceBufferUpload.compute")]
        private ComputeShader m_InstanceBufferUploadCS;

        public ComputeShader InstanceBufferUploadCS
        {
            get => m_InstanceBufferUploadCS;
            set => this.SetValueAndNotify(ref m_InstanceBufferUploadCS, value);
        }

        #endregion

        #region Culling

        [Header("Culling")]
        [SerializeField, ResourcePath("Runtime/Culling/CullingGrid.compute")]
        private ComputeShader m_CullingGridCS;

        public ComputeShader CullingGridCS
        {
            get => m_CullingGridCS;
            set => this.SetValueAndNotify(ref m_CullingGridCS, value);
        }

        [SerializeField, ResourcePath("Runtime/Culling/IndirectCullingChunks.compute")]
        private ComputeShader m_IndirectCullingChunksCS;

        public ComputeShader IndirectCullingChunksCS
        {
            get => m_IndirectCullingChunksCS;
            set => this.SetValueAndNotify(ref m_IndirectCullingChunksCS, value);
        }

        [SerializeField, ResourcePath("Runtime/Culling/IndirectCullingInstances.compute")]
        [FormerlySerializedAs("m_IndirectCullingInstances")]
        private ComputeShader m_IndirectCullingInstancesCS;

        public ComputeShader IndirectCullingInstancesCS
        {
            get => m_IndirectCullingInstancesCS;
            set => this.SetValueAndNotify(ref m_IndirectCullingInstancesCS, value);
        }

        [SerializeField, ResourcePath("Runtime/Culling/IndirectCullingDraws.compute")]
        private ComputeShader m_IndirectCullingDrawsCS;

        public ComputeShader IndirectCullingDrawsCS
        {
            get => m_IndirectCullingDrawsCS;
            set => this.SetValueAndNotify(ref m_IndirectCullingDrawsCS, value);
        }

        #endregion

        #region Occclusion

        [Header("Occlusion")]
        [SerializeField]
        private ComputeShader m_OccluderDepthPyramidKernelsCS;

        public ComputeShader OccluderDepthPyramidKernelsCS
        {
            get => m_OccluderDepthPyramidKernelsCS;
            set => this.SetValueAndNotify(ref m_OccluderDepthPyramidKernelsCS, value);
        }

        [SerializeField] private ComputeShader m_OcclusionCullingDebugCS;

        public ComputeShader DebugOcclusionCS
        {
            get => m_OcclusionCullingDebugCS;
            set => this.SetValueAndNotify(ref m_OcclusionCullingDebugCS, value);
        }

        [SerializeField] private Shader m_DebugOccluderShader;

        public Shader DebugOccluderShader
        {
            get => m_DebugOccluderShader;
            set => this.SetValueAndNotify(ref m_DebugOccluderShader, value);
        }

        [SerializeField] private Shader m_DebugOcclusionTestShader;

        public Shader DebugOcclusionTestShader
        {
            get => m_DebugOcclusionTestShader;
            set => this.SetValueAndNotify(ref m_DebugOcclusionTestShader, value);
        }

        #endregion

        #region Terrain

        [SerializeField] [ResourcePath("Runtime/Materials/TerrainDetailPlaceholder.prefab")]
        private GameObject m_TerrainGrassPlaceholderPrefab;

        public GameObject TerrainGrassPlaceholderPrefab
        {
            get => m_TerrainGrassPlaceholderPrefab;
            set => this.SetValueAndNotify(ref m_TerrainGrassPlaceholderPrefab, value);
        }

        [SerializeField] [ResourcePath("Runtime/Materials/TerrainGrass.mat")]
        private Material m_TerrainGrassMaterial;

        public Material TerrainGrassMaterial
        {
            get => m_TerrainGrassMaterial;
            set => this.SetValueAndNotify(ref m_TerrainGrassMaterial, value);
        }

        #endregion

        #region Debug

        [Header("Debug")]
        [SerializeField, ResourcePath("Runtime/Debugging/DebugCullingGrid.compute")]
        private ComputeShader m_DebugCullingGridCS;

        public ComputeShader DebugCullingGridCS
        {
            get => m_DebugCullingGridCS;
            set => this.SetValueAndNotify(ref m_DebugCullingGridCS, value);
        }

        [SerializeField, ResourcePath("Runtime/Debugging/DebugLine.shader")]
        private Shader m_DebugLineShader;

        public Shader DebugLineShader
        {
            get => m_DebugLineShader;
            set => this.SetValueAndNotify(ref m_DebugLineShader, value);
        }

        #endregion

#if UNITY_EDITOR
        private const string GPUDrivenResourcesPath = "Packages/com.unity.render-pipelines.core/Runtime/RenderPipelineResources/GPUDriven/";

        public void EnsureShadersCompiled()
        {
            if (m_OccluderDepthPyramidKernelsCS == null ||
                m_OcclusionCullingDebugCS == null ||
                m_DebugOccluderShader == null ||
                m_DebugOcclusionTestShader == null)
            {
                m_OccluderDepthPyramidKernelsCS = AssetDatabase.LoadAssetAtPath<ComputeShader>(GPUDrivenResourcesPath + "OccluderDepthPyramidKernels.compute");
                m_OcclusionCullingDebugCS = AssetDatabase.LoadAssetAtPath<ComputeShader>(GPUDrivenResourcesPath + "OcclusionCullingDebug.compute");
                m_DebugOccluderShader = AssetDatabase.LoadAssetAtPath<Shader>(GPUDrivenResourcesPath + "DebugOccluder.shader");
                m_DebugOcclusionTestShader = AssetDatabase.LoadAssetAtPath<Shader>(GPUDrivenResourcesPath + "DebugOcclusionTest.shader");

                if (m_OccluderDepthPyramidKernelsCS == null ||
                    m_OcclusionCullingDebugCS == null ||
                    m_DebugOccluderShader == null ||
                    m_DebugOcclusionTestShader == null)
                {
                    throw new Exception("Failed to load some of the default GPU Driven shaders from the Render Pipeline Core package. " +
                                        "This can happen if the package is not installed correctly. " +
                                        "Flora will not run correctly until the error is fixed.\n");
                }
                else
                {
                    GraphicsSettings.TryGetCurrentRenderPipelineGlobalSettings(out var globalSettings);
                    if (globalSettings != null)
                        EditorUtility.SetDirty(globalSettings);
                }
            }

            void CheckComputeShaderMessages(ComputeShader computeShader)
            {
                if (computeShader == null)
                {
                    throw new Exception("A Compute Shader reference is null. Flora will not run correctly until the error is fixed.\n");
                }

                foreach (var message in ShaderUtil.GetComputeShaderMessages(computeShader))
                {
                    if (message.severity == ShaderCompilerMessageSeverity.Error)
                    {
                        throw new Exception(string.Format(
                            "Compute Shader compilation error on platform {0} in file {1}:{2}: {3}{4}\n" +
                            "Flora will not run correctly until the error is fixed.\n",
                            message.platform, message.file, message.line, message.message, message.messageDetails
                        ));
                    }
                }
            }

            // We iterate over all compute shader to verify if they are all compiled, if it's not the case then
            // we throw an exception to avoid allocating resources and crashing later on by using a null compute kernel.
            this.ForEachFieldOfType<ComputeShader>(CheckComputeShaderMessages, BindingFlags.Public | BindingFlags.Instance);
        }
#endif
    }

    [Serializable]
    [Obsolete]
    [HideInInspector]
    [SupportedOnRenderPipeline]
    internal class FloraRuntimeShaders : IRenderPipelineResources
    {
        // Obsolete class kept for compatibility with older versions of Flora.
        public int version => 0;
        public bool isAvailableInPlayerBuild => false;
    }
}
