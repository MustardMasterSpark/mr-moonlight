// Copyright © Magnetic Arcade. All Rights Reserved.

using UnityEditor;
using UnityEngine;

namespace MA.Flora.Editor
{
    [CanEditMultipleObjects]
    [CustomEditor(typeof(FloraAdditionalRendererSettings))]
    internal class FloraAdditionalRendererSettingsEditor : UnityEditor.Editor
    {
        private SerializedProperty m_AdditionalPerInstanceData;
        private SerializedProperty m_MaxRenderDistance;
        private SerializedProperty m_MaxShadowDistance;
        private SerializedProperty m_MinShadowLOD;
        private SerializedProperty m_AffectedByGlobalDensity;
        private SerializedProperty m_AffectedByRangeDensity;
        private SerializedProperty m_AffectedByMinimumScreenSize;

        private void OnEnable()
        {
            m_AdditionalPerInstanceData = serializedObject.FindProperty("AdditionalPerInstanceData");
            m_MaxRenderDistance = serializedObject.FindProperty("MaxRenderDistance");
            m_MaxShadowDistance = serializedObject.FindProperty("MaxShadowDistance");
            m_MinShadowLOD = serializedObject.FindProperty("MinShadowLOD");
            m_AffectedByGlobalDensity = serializedObject.FindProperty("AffectedByGlobalDensity");
            m_AffectedByRangeDensity = serializedObject.FindProperty("AffectedByRangeDensity");
            m_AffectedByMinimumScreenSize = serializedObject.FindProperty("AffectedByMinimumScreenSize");
        }

        public override void OnInspectorGUI()
        {
            var settings = (FloraAdditionalRendererSettings)target;
            if (!settings.TryGetComponent(out LODGroup _) && !settings.TryGetComponent(out MeshRenderer _))
            {
                EditorGUILayout.HelpBox("This component is only effective when attached to a GameObject with a LODGroup or MeshRenderer component.", MessageType.Info);
                return;
            }

            if (!PrefabUtility.IsPartOfAnyPrefab(settings.gameObject))
            {
                EditorGUILayout.HelpBox("This component's properties are only editable on the source prefab asset.", MessageType.Info);
                return;
            }

            serializedObject.Update();

            EditorGUILayout.PropertyField(m_AdditionalPerInstanceData);
            EditorGUILayout.PropertyField(m_MaxRenderDistance);
            EditorGUILayout.PropertyField(m_MaxShadowDistance);
            EditorGUILayout.PropertyField(m_MinShadowLOD);
            EditorGUILayout.PropertyField(m_AffectedByGlobalDensity);
            EditorGUILayout.PropertyField(m_AffectedByRangeDensity);
            EditorGUILayout.PropertyField(m_AffectedByMinimumScreenSize);

            serializedObject.ApplyModifiedProperties();
        }
    }
}
