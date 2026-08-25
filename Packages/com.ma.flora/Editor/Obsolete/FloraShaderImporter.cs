// Copyright © Magnetic Arcade. All Rights Reserved.

using System;
using System.IO;
using MA.Flora.Editor.InternalBridge;
using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace MA.Flora.Editor
{
    [ExcludeFromPreset]
    [ScriptedImporter(Version, new[] { "florashader", "floraautoshader" })]
    [Obsolete]
    internal class FloraShaderImporter : ScriptedImporter
    {
        public const int Version = 4;
        public const string Extension = "florashader";

        public string SourceAssetGUID;
        public Shader PatchedShader;

        private const string k_ErrorShader = @"
Shader ""Hidden/FloraAutoShaderError""
{
    SubShader
    {
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            #pragma multi_compile _ UNITY_SINGLE_PASS_STEREO STEREO_INSTANCING_ON STEREO_MULTIVIEW_ON
            #include ""UnityCG.cginc""

            struct appdata_t {
                float4 vertex : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f {
                float4 vertex : SV_POSITION;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            v2f vert (appdata_t v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.vertex = UnityObjectToClipPos(v.vertex);
                return o;
            }
            fixed4 frag (v2f i) : SV_Target
            {
                return fixed4(1,0,1,1);
            }
            ENDCG
        }
    }
    Fallback Off
}";

        public override void OnImportAsset(AssetImportContext ctx)
        {
            // Read the serialized data
            string text = File.ReadAllText(ctx.assetPath);
            if (string.IsNullOrEmpty(text))
                return;

            FloraShaderData data = JsonUtility.FromJson<FloraShaderData>(text);
            if (data == null)
                return;

            if (string.IsNullOrEmpty(data.SourceGUID))
            {
                ctx.LogImportError($"Flora Shader (deprecated): Missing Source GUID for {ctx.assetPath}.");
                return;
            }

            SourceAssetGUID = data.SourceGUID;

            if (!TryLoadOriginalSource(SourceAssetGUID, out string originalSource, out string sourceAssetPath))
            {
                ctx.LogImportError($"Flora Shader (deprecated): Could not locate original shader for GUID {SourceAssetGUID} referenced by {ctx.assetPath}.");
                // Create a visible error shader so the issue is obvious
                PatchedShader = ShaderUtil.CreateShaderAsset(ctx, k_ErrorShader, true);
                ctx.AddObjectToAsset("MainAsset", PatchedShader);
                ctx.SetMainObject(PatchedShader);
                return;
            }

            PatchedShader = ShaderUtil.CreateShaderAsset(ctx, originalSource, true);

            if (ShaderUtil.ShaderHasError(PatchedShader))
            {
                foreach (var msg in ShaderUtil.GetShaderMessages(PatchedShader))
                {
                    if (msg.severity == UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error)
                        Debug.LogError(msg.message, PatchedShader);
                    else
                        Debug.LogWarning(msg.message, PatchedShader);
                }
            }
            else
            {
                ShaderUtil.ClearShaderMessages(PatchedShader);
                ShaderUtil.RegisterShader(PatchedShader);
            }

            Texture2D icon = EditorGUIUtilityBridge.LoadIconRequired("Packages/com.ma.flora/Editor/EditorResources/Icons/Shader Icon.png");
            ctx.AddObjectToAsset("MainAsset", PatchedShader, icon);
            ctx.SetMainObject(PatchedShader);

            var material = new Material(PatchedShader) { name = PatchedShader.name };
            ctx.AddObjectToAsset("Material", material);

            if (!string.IsNullOrEmpty(sourceAssetPath))
            {
                ctx.DependsOnSourceAsset(sourceAssetPath);
                foreach (var dep in AssetDatabase.GetDependencies(sourceAssetPath))
                    ctx.DependsOnSourceAsset(dep);
            }

            ctx.LogImportWarning(
                $"Flora Shader is deprecated and no longer patches shaders. " +
                $"This asset now compiles the ORIGINAL source shader found at:\n" +
                $"{sourceAssetPath}\n\n" +
                $"To fully migrate, point any materials to the original shader and delete this .{Extension} asset.");
        }

        private static bool TryLoadOriginalSource(string assetGUID, out string source, out string sourceAssetPath)
        {
            source = null;
            sourceAssetPath = AssetDatabase.GUIDToAssetPath(assetGUID);
            if (string.IsNullOrEmpty(sourceAssetPath))
                return false;

            if (!sourceAssetPath.EndsWith(".shader", StringComparison.InvariantCultureIgnoreCase))
                return false;

            try
            {
                source = File.ReadAllText(sourceAssetPath);
                return !string.IsNullOrEmpty(source);
            }
            catch
            {
                return false;
            }
        }
    }
}
