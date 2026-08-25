// Copyright © Magnetic Arcade. All Rights Reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using MA.Flora.Editor.InternalBridge;
using UnityEditor;
using UnityEngine;

namespace MA.Flora.Editor
{
    internal static class FloraSceneHierarchyIcons
    {
        [InitializeOnLoadMethod]
        private static void Initialize()
        {
#if UNITY_6000_5_OR_NEWER
            EditorApplication.hierarchyWindowItemByEntityIdOnGUI += OnHierarchyItemGUI;
#else
            EditorApplication.hierarchyWindowItemOnGUI += OnHierarchyItemGUI;
#endif
            Undo.undoRedoPerformed += OnUndoRedoPerformed;
        }

        private static void OnUndoRedoPerformed()
        {
            EditorApplication.delayCall += EditorApplication.RepaintHierarchyWindow;
        }

        private static Texture2D s_ContainerIcon;
        private static Texture2D s_ContainerPrefabIcon;
        private static Texture2D s_InstanceRendererIcon;
        private static Texture2D s_InstanceRendererPrefabIcon;

        private static List<Type> s_TypesWithSceneHierarchyIcons = new();

        private static void InitializeIcons()
        {
            s_ContainerIcon = EditorGUIUtilityBridge.LoadIconRequired("Packages/com.ma.flora/Editor/EditorResources/Icons/InstanceContainer Normal Icon.png");
            s_ContainerPrefabIcon = EditorGUIUtilityBridge.LoadIconRequired("Packages/com.ma.flora/Editor/EditorResources/Icons/InstanceContainer Prefab Icon.png");

            s_InstanceRendererIcon = EditorGUIUtilityBridge.LoadIconRequired("Packages/com.ma.flora/Editor/EditorResources/Icons/InstanceRenderer Normal Icon.png");
            s_InstanceRendererPrefabIcon = EditorGUIUtilityBridge.LoadIconRequired("Packages/com.ma.flora/Editor/EditorResources/Icons/InstanceRenderer Prefab Icon.png");

            var componentTypesWithSceneHierarchyIcons = TypeCache.GetTypesWithAttribute<SceneIconAttribute>().Where(t => t.IsSubclassOf(typeof(Component)));
            s_TypesWithSceneHierarchyIcons.AddRange(componentTypesWithSceneHierarchyIcons);
        }

#if UNITY_6000_5_OR_NEWER
        private static void OnHierarchyItemGUI(EntityId entityId, Rect rect)
#else
        private static void OnHierarchyItemGUI(int instanceId, Rect rect)
#endif
        {
            if (s_ContainerIcon == null)
                InitializeIcons();

#if !UNITY_6000_5_OR_NEWER
            EntityId entityId = instanceId;
#endif
            GameObject gameObject = entityId.ToObject<GameObject>();
            if (gameObject == null)
                return;

            EditorWindow window = SceneHierarchyBridge.GetLastHierarchyWindow();
            if (!window) return;

#if UNITY_6000_5_OR_NEWER
            var item = SceneHierarchyBridge.GetItem(window, entityId);
#else
            var item = SceneHierarchyBridge.GetItem(window, instanceId);
#endif
            if (item == null) return;

            bool isPrefab = PrefabUtility.IsPartOfAnyPrefab(gameObject);
            bool isPrefabVariant = PrefabUtility.IsPartOfVariantPrefab(gameObject);

            if (gameObject.TryGetComponent(out FloraInstanceContainer _))
            {
                if (isPrefab || isPrefabVariant)
                    item.icon = s_ContainerPrefabIcon;
                else
                    item.icon = s_ContainerIcon;
            }
            else if (gameObject.TryGetComponent(out FloraInstanceRenderer rendererGroup))
            {
                if (rendererGroup.enabled)
                {
                    if (isPrefab || isPrefabVariant)
                        item.icon = s_InstanceRendererPrefabIcon;
                    else
                        item.icon = s_InstanceRendererIcon;
                }
            }

            foreach (var type in s_TypesWithSceneHierarchyIcons)
            {
                if (gameObject.TryGetComponent(type, out Component _))
                {
                    SceneIconAttribute attribute = (SceneIconAttribute)Attribute.GetCustomAttribute(type, typeof(SceneIconAttribute));
                    if (attribute != null)
                    {
                        var path = attribute.Path;
                        if (string.IsNullOrEmpty(path))
                        {
                            var iconAttribute = (IconAttribute)Attribute.GetCustomAttribute(type, typeof(IconAttribute));
                            if (iconAttribute != null)
                            {
                                path = iconAttribute.path;
                            }
                        }

                        if (string.IsNullOrEmpty(path))
                            continue;

                        item.icon = EditorGUIUtilityBridge.LoadIconRequired(path);
                    }
                }
            }
        }
    }
}
