// Copyright © Magnetic Arcade. All Rights Reserved.

using UnityEditor;

namespace MA.Flora.Editor
{
    [CanEditMultipleObjects]
    [CustomEditor(typeof(FloraAdditionalCameraSettings))]
    internal class FloraAdditionalCameraSettingsEditor : UnityEditor.Editor
    {
        private SerializedProperty m_AllowGPUOcclusionCulling;
        private SerializedProperty m_DisableInstanceRendering;
        private SerializedProperty m_LODBiasScale;

        private void OnEnable()
        {
            m_AllowGPUOcclusionCulling = serializedObject.FindProperty("AllowGPUOcclusionCulling");
            m_DisableInstanceRendering = serializedObject.FindProperty("DisableInstanceRendering");
            m_LODBiasScale = serializedObject.FindProperty("LODBiasScale");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.PropertyField(m_AllowGPUOcclusionCulling);
            EditorGUILayout.PropertyField(m_DisableInstanceRendering);
            EditorGUILayout.PropertyField(m_LODBiasScale);

            serializedObject.ApplyModifiedProperties();
        }
    }
}
