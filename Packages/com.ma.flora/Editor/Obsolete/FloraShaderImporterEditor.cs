// Copyright © Magnetic Arcade. All Rights Reserved.

using System;
using UnityEditor;
using UnityEditor.AssetImporters;

namespace MA.Flora.Editor
{
    [CanEditMultipleObjects]
    [CustomEditor(typeof(FloraShaderImporter))]
    [Obsolete]
    internal class FloraShaderImporterEditor : AssetImporterEditor
    {
        public override void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox(L10n.Tr("This importer is obsolete. Shader modifications are not required with BatchRendererGroup."), MessageType.Warning);
        }
    }
}
