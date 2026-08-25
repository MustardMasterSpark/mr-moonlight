// Copyright © Magnetic Arcade. All Rights Reserved.

using UnityEditor;
using UnityEngine;
using UnityObject = UnityEngine.Object;

namespace MA.Flora.Editor
{
    [CanEditMultipleObjects]
    [CustomEditor(typeof(InstanceSelectionGroup))]
    internal class InstanceSelectionGroupEditor : UnityEditor.Editor
    {
        private void OnEnable()
        {
            foreach (UnityObject target in targets)
            {
                if (target is InstanceSelectionGroup group)
                    group.Retain();
            }

            AssemblyReloadEvents.beforeAssemblyReload += OnBeforeAssemblyReload;
        }

        private void OnDisable()
        {
            AssemblyReloadEvents.beforeAssemblyReload -= OnBeforeAssemblyReload;

            foreach (UnityObject target in targets)
            {
                if (target is InstanceSelectionGroup group)
                    group.Release();
            }
        }

        private bool HasFrameBounds()
        {
            return targets.Length > 0;
        }

        private Bounds OnGetFrameBounds()
        {
            AxisAlignedBox bounds = AxisAlignedBox.Empty;

            foreach (UnityObject target in targets)
            {
                if (target is InstanceSelectionGroup group)
                    bounds += group.CalculateBounds();
            }

            return bounds;
        }

        private void OnBeforeAssemblyReload()
        {
            // After a domain reload, it's impossible to retrieve the instance this editor is referring to. So before the domain is unloaded, we ensure that:
            // 1. The active selection or context is not an InstanceSelectionGroup, so we don't try to re-select it once the domain is reloaded.
            if (Selection.activeObject is InstanceSelectionGroup || Selection.activeContext is InstanceSelectionGroup)
                Selection.activeObject = null; // Note that changing the selection also clears the active context

            // 2. This editor no longer exists, so a locked inspector is not revived with invalid data once the domain is reloaded.
            DestroyImmediate(this);
        }

        public override void OnInspectorGUI()
        {
            int selectionCount = 0;
            foreach (UnityObject target in targets)
            {
                if (target is InstanceSelectionGroup group)
                    selectionCount += group.Length;
            }

            EditorGUILayout.LabelField("Selection Count", selectionCount.ToString());
        }
    }
}
