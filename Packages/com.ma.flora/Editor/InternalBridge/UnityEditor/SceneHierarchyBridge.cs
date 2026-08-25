// Copyright © Magnetic Arcade. All Rights Reserved.

using System;
using UnityEditor;
using UnityEngine;

#if UNITY_6000_3_OR_NEWER
using UnityTreeViewItem = UnityEditor.IMGUI.Controls.TreeViewItem<UnityEngine.EntityId>;
#elif UNITY_6000_2_OR_NEWER
using UnityTreeViewItem = UnityEditor.IMGUI.Controls.TreeViewItem<int>;
#else
using UnityTreeViewItem = UnityEditor.IMGUI.Controls.TreeViewItem;
#endif

namespace MA.Flora.Editor.InternalBridge
{
    internal static class SceneHierarchyBridge
    {
        internal static EditorWindow GetLastHierarchyWindow()
        {
            return SceneHierarchyWindow.lastInteractedHierarchyWindow;
        }

#if UNITY_6000_5_OR_NEWER
        internal static void AddGetEntityIdFromIndex(Func<int, EntityId> callback)
        {
            HandleUtility.getEntityIdFromIndex += callback;
        }
#endif

#if UNITY_6000_5_OR_NEWER
        internal static UnityTreeViewItem GetItem(EditorWindow window, UnityEngine.EntityId entityId)
#else
        internal static UnityTreeViewItem GetItem(EditorWindow window, int instanceId)
#endif
        {
            if (window is SceneHierarchyWindow sceneHierarchyWindow)
            {

                var data = sceneHierarchyWindow.sceneHierarchy.treeView.data;
                if (data == null)
                    return null;

                var rows = data.GetRows();
#if UNITY_6000_5_OR_NEWER
                int itemRow = data.GetRow(entityId);
#else
                int itemRow = data.GetRow(instanceId);
#endif
                if (itemRow < 0 || itemRow >= rows.Count)
                    return null;

                return rows[itemRow];
            }

            return null;
        }
    }
}
