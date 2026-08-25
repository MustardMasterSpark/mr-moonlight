// Copyright © Magnetic Arcade. All Rights Reserved.

using System;
using UnityEditor;
using UnityEngine.Serialization;

namespace MA.Flora.Editor
{
    [Obsolete]
    internal enum FloraSourceShaderType
    {
        Invalid,
        Shader,
        ShaderGraph,
        BetterShader,
        MicroVersePack,
        Unrecognized,
    }

    [Serializable]
    [Obsolete]
    internal class FloraShaderData
    {
        public int ImporterVersion;
        public FloraSourceShaderType SourceType;
        [FormerlySerializedAs("SourceGuid")]
        public string SourceGUID;
        public bool EnableDebugSymbols;
        public string GetSourceAssetPath() => string.IsNullOrEmpty(SourceGUID) ? "" : AssetDatabase.GUIDToAssetPath(SourceGUID);
    }
}
