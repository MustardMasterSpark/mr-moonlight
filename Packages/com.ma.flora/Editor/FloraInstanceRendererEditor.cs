// Copyright © Magnetic Arcade. All Rights Reserved.

using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace MA.Flora.Editor
{
    [CanEditMultipleObjects]
    [CustomEditor(typeof(FloraInstanceRenderer))]
    internal class FloraInstanceRendererEditor : UnityEditor.Editor
    {
        private SerializedProperty m_Prefab;
        private Renderer[] m_PrefabRenderers;
        private List<(Renderer, int, Material)> m_InvalidMaterials = new();
        private bool m_ContainsInvalidMaterials;

        private void OnEnable()
        {
            m_Prefab = serializedObject.FindProperty("m_Prefab");

            var instanceRenderer = (FloraInstanceRenderer)target;
            var sourceObject = m_Prefab.objectReferenceValue != null
                ? (GameObject)m_Prefab.objectReferenceValue
                : instanceRenderer ? instanceRenderer.RenderSource : null;

            if (sourceObject != null)
            {
                m_PrefabRenderers = sourceObject.GetComponentsInChildren<Renderer>(true);
                foreach (var renderer in m_PrefabRenderers)
                {
                    if (TryGetInvalidMaterials(renderer, out m_InvalidMaterials))
                    {
                        m_ContainsInvalidMaterials = true;
                    }
                }
            }
        }

        public override void OnInspectorGUI()
        {
            var targetInstanceRenderer = (FloraInstanceRenderer)target;

            if (m_ContainsInvalidMaterials)
            {
                foreach ((Renderer renderer, int materialIndex, Material material) in m_InvalidMaterials)
                {
                    EditorGUILayout.HelpBox(L10n.Tr($"The material ({material.name}) at index ({materialIndex}) on the renderer ({renderer.name}) does not support DOTS instancing. " +
                                                    "Please use a shader that supports DOTS instancing to render with Flora."), MessageType.Error);
                }
                return;
            }

            serializedObject.Update();

            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.PropertyField(m_Prefab);
            EditorGUI.EndDisabledGroup();

            if (m_Prefab.objectReferenceValue == null)
            {
                EditorGUILayout.HelpBox(
                    L10n.Tr("This renderer is using the scene object itself as the Flora render source. " +
                            "Prefab-based filtering and revert workflows stay available only when a prefab identity exists."),
                    MessageType.Info);
            }

            serializedObject.ApplyModifiedProperties();
        }

        private bool HasFrameBounds()
        {
            return true;
        }

        private Bounds OnGetFrameBounds()
        {
            return ((FloraInstanceRenderer)target).gameObject.CalculateWorldBounds();
        }

        private static bool TryGetInvalidMaterials(Renderer renderer, out List<(Renderer, int, Material)> invalidMaterials)
        {
            invalidMaterials = new List<(Renderer, int, Material)>();

            if (renderer == null)
                return false;

            var materials = renderer.sharedMaterials;
            if (materials == null || materials.Length == 0)
                return false;

            for (var index = 0; index < materials.Length; index++)
            {
                Material material = materials[index];
                if (material && material.shader && material.shader.HasDOTSKeyword())
                    continue;

                invalidMaterials.Add((renderer, index, material));
            }

            return invalidMaterials.Count > 0;
        }
    }
}
