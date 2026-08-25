// Copyright © Magnetic Arcade. All Rights Reserved.

using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering;

namespace MA.Flora.Editor
{
    internal class FloraComputeShaderVariantStripper : IComputeShaderVariantStripper
    {
        private FloraRuntimeResources m_Resources = FloraBuildData.Instance.RuntimeResources;

        public bool active => FloraBuildData.Instance.BuildingPlayerForRenderPipeline;

        public bool CanRemoveVariant(ComputeShader shader, string shaderVariant, ShaderCompilerData inputData)
        {
            bool disableGPUOcclusionCulling = FloraBuildData.Instance.Settings.DisableGPUOcclusionCulling;
            bool isDevelopmentBuild = FloraBuildData.Instance.IsDevelopmentBuild;
            bool isDevelopmentOnlyShader = shader == m_Resources.DebugCullingGridCS;
            if (isDevelopmentOnlyShader && !isDevelopmentBuild)
                return true;

            bool stripDebugVariants = FloraBuildData.Instance.StripDebugVariants;
            bool isCullingShader = shader == m_Resources.IndirectCullingInstancesCS ||
                                   shader == m_Resources.IndirectCullingChunksCS ||
                                   shader == m_Resources.IndirectCullingDrawsCS;
            if (isCullingShader)
            {
                ShaderKeyword[] editorOnlyKeywords =
                {
                    new ShaderKeyword(shader, "VIEW_IS_EDITOR"),
                };

                foreach (var keyword in editorOnlyKeywords)
                {
                    if (inputData.shaderKeywordSet.IsEnabled(keyword))
                        return true;
                }

                if (stripDebugVariants)
                {
                    ShaderKeyword[] debugOnlyKeywords =
                    {
                        new ShaderKeyword(shader, "DEBUG_ENABLED"),
                        new ShaderKeyword(shader, "DEBUG_OCCLUSION"),
                    };

                    foreach (var keyword in debugOnlyKeywords)
                    {
                        if (inputData.shaderKeywordSet.IsEnabled(keyword))
                            return true;
                    }
                }

                if (disableGPUOcclusionCulling)
                {
                    ShaderKeyword[] occlusionKeywords =
                    {
                        new ShaderKeyword(shader, "USE_OCCLUSION"),
                        new ShaderKeyword(shader, "DEBUG_OCCLUSION"),
                    };

                    foreach (var keyword in occlusionKeywords)
                    {
                        if (inputData.shaderKeywordSet.IsEnabled(keyword))
                            return true;
                    }
                }
            }

            return false;
        }
    }
}
