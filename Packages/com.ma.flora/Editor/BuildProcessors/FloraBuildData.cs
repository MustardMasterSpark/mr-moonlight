using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering;

namespace MA.Flora.Editor
{
    internal class FloraBuildData : IDisposable
    {
        private static FloraBuildData s_Instance = null;
        public static FloraBuildData Instance => s_Instance ??= new FloraBuildData(EditorUserBuildSettings.activeBuildTarget, Debug.isDebugBuild);

        public bool BuildingPlayerForRenderPipeline { get; private set; } = false;
        public Type CurrentRenderPipelineAssetType { get; private set; } = null;
        public List<RenderPipelineAsset> RenderPipelineAssets { get; } = new();

        public FloraRuntimeSettings Settings { get; private set; }
        public FloraRuntimeResources RuntimeResources { get; private set; }
        public Dictionary<EntityId, ComputeShader> ComputeShaderCache { get; } = new();
        public Dictionary<EntityId, ComputeShader> OcclusionComputeShaderCache { get; } = new();

        public bool IsDevelopmentBuild { get; private set; }
        public bool StripDebugVariants { get; private set; } = true;

        public FloraBuildData(BuildTarget buildTarget, bool isDevelopmentBuild)
        {
            if (!buildTarget.TryGetRenderPipelineAssets(RenderPipelineAssets))
                return;

            BuildingPlayerForRenderPipeline = true;
            CurrentRenderPipelineAssetType = RenderPipelineAssets[0].GetType();
            Settings = GraphicsSettings.GetRenderPipelineSettings<FloraRuntimeSettings>();
            RuntimeResources = GraphicsSettings.GetRenderPipelineSettings<FloraRuntimeResources>();
            RuntimeResources?.ForEachFieldOfType<ComputeShader>(computeShader => ComputeShaderCache.Add(computeShader.GetEntityId(), computeShader));
            IsDevelopmentBuild = isDevelopmentBuild;
            StripDebugVariants = !IsDevelopmentBuild || GraphicsSettings.GetRenderPipelineSettings<ShaderStrippingSetting>().stripRuntimeDebugShaders;

            s_Instance = this;
        }

        public void Dispose()
        {
            RenderPipelineAssets?.Clear();
            ComputeShaderCache?.Clear();
        }
    }
}
