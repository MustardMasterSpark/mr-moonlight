// Copyright © Magnetic Arcade. All Rights Reserved.

using System.Linq;
using UnityEditor;
using UnityEngine;

namespace MA.Flora.Editor
{
    [CanEditMultipleObjects]
    [CustomEditor(typeof(FloraInstanceContainer))]
    internal class FloraInstanceContainerEditor : UnityEditor.Editor
    {
        private FloraInstanceContainer[] m_Containers;

        private void OnEnable()
        {
            m_Containers = targets.Cast<FloraInstanceContainer>().ToArray();
        }

        public override void OnInspectorGUI()
        {
            var hasMultipleTargets = m_Containers.Length > 1;
            var hasDifferentPrototypes = m_Containers.Any(c => c.Prefab != m_Containers[0].Prefab);
            var container = (FloraInstanceContainer)target;

            EditorGUI.showMixedValue = hasMultipleTargets && hasDifferentPrototypes;
            var obj = EditorGUILayout.ObjectField(Styles.Prefab, container.Prefab, typeof(GameObject), false);
            if (obj != container.Prefab && obj is GameObject gameObject)
            {
                bool hasMesh = gameObject.TryGetComponent(out MeshFilter _);
                bool hasRenderer = gameObject.TryGetComponent(out MeshRenderer _);
                bool hasLODGroup = gameObject.TryGetComponent(out LODGroup _);
                if (!hasMesh && !hasRenderer && !hasLODGroup)
                {
                    EditorGUILayout.HelpBox(L10n.Tr("The prefab must have a MeshRenderer or LODGroup component."), MessageType.Error);
                }

                container.Prefab = gameObject;
                EditorUtility.SetDirty(container);
            }
            EditorGUI.showMixedValue = false;

            using (new EditorGUI.DisabledScope(true))
            {
                int totalInstanceCount = m_Containers.Sum(c => c.InstanceCount);
                EditorGUILayout.TextField(Styles.InstanceCount, StringUtility.FormatLargeNumber(totalInstanceCount));
            }
        }

        private bool HasFrameBounds()
        {
            foreach (FloraInstanceContainer container in m_Containers)
            {
                if (container.Prefab && container.InstanceCount > 0)
                    return true;
            }

            return false;
        }

        private Bounds OnGetFrameBounds()
        {
            AxisAlignedBox bounds = AxisAlignedBox.Empty;

            foreach (FloraInstanceContainer container in m_Containers)
            {
                if (container.Prefab && container.InstanceCount > 0)
                    bounds += container.CalculateBounds(Space.World);
            }

            return bounds;
        }

        private static readonly Color BoundsColor =  new(251 / 255f, 202 / 255f, 76 / 255f, 0.5f);

        [DrawGizmo(GizmoType.Pickable, typeof(FloraInstanceContainer))]
        private static void PickableGizmos(FloraInstanceContainer instanceContainer, GizmoType gizmoType)
        {
            if (instanceContainer.Prefab)
            {
                AxisAlignedBox bounds = instanceContainer.CalculateBounds(Space.Self);
                Gizmos.DrawIcon(bounds.Center, "Packages/com.ma.flora/Editor/Icons/InstancedMeshContainer Icon.png", true);
            }
        }

        [DrawGizmo(GizmoType.Selected, typeof(FloraInstanceContainer))]
        private static void SelectedGizmos(FloraInstanceContainer instanceContainer, GizmoType gizmoType)
        {
            if (instanceContainer.Prefab)
            {
                AxisAlignedBox bounds = instanceContainer.CalculateBounds(Space.Self);
                Gizmos.color = BoundsColor;
                Gizmos.matrix = instanceContainer.transform.localToWorldMatrix;
                Gizmos.DrawWireCube(bounds.Center, bounds.Size);
            }
        }

        private static class Styles
        {
            public static readonly GUIContent Prefab = L10n.TextContent("Prefab", "The model prefab that will be instanced.");
            public static readonly GUIContent InstanceCount = L10n.TextContent("Instance Count", "The number of instances in the container.");
        }
    }
}
