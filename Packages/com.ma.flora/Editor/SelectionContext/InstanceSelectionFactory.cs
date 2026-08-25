// Copyright © Magnetic Arcade. All Rights Reserved.

using UnityEditor;
using UnityEngine;

namespace MA.Flora.Editor
{
    internal static class InstanceSelectionFactory
    {
        public static InstanceSelectionGroup CreateInstance(FloraInstanceHandle instance)
        {
            if (!FloraSystem.Active || !FloraSystem.Instance.InstanceExists(instance))
                return null;

            var instanceInContainer = FloraSystem.Instance.GetInstanceInContainer(instance);
            if (instanceInContainer.Container.IsValid() && !SceneVisibilityManager.instance.IsPickingDisabled(instanceInContainer.Container.Object.gameObject))
            {
                var result = ScriptableObject.CreateInstance<ContainerSelection>();
                result.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild | HideFlags.NotEditable;
                result.Initialize(instanceInContainer.Container.Object.gameObject, new[] { instanceInContainer.IndexInContainer });
                return result;
            }

            var treeInTerrain = FloraSystem.Instance.GetTreeInTerrain(instance);
            var terrain = treeInTerrain.TerrainEntity.ToObject<Terrain>();
            if (terrain && !SceneVisibilityManager.instance.IsPickingDisabled(terrain.gameObject))
            {
                var result = ScriptableObject.CreateInstance<TreeSelection>();
                result.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild | HideFlags.NotEditable;
                result.Initialize(terrain.gameObject, new[] { treeInTerrain.IndexInTreeInstances });
                return result;
            }

            return null;
        }

        public static InstanceSelectionGroup CreateInstance(Component component, int[] selectedIndices)
        {
            if (component == null)
                return null;

            return CreateInstance(component.gameObject, selectedIndices);
        }

        public static InstanceSelectionGroup CreateInstance(GameObject gameObject, int[] selectedIndices)
        {
            if (gameObject == null || selectedIndices == null || selectedIndices.Length == 0)
                return null;

            if (!FloraSystem.Active || SceneVisibilityManager.instance.IsPickingDisabled(gameObject))
                return null;

            if (gameObject.TryGetComponent(out FloraInstanceContainer instanceContainer))
            {
                var result = ScriptableObject.CreateInstance<ContainerSelection>();
                result.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild | HideFlags.NotEditable;
                result.Initialize(instanceContainer.gameObject, selectedIndices);
                return result;
            }
            else if (gameObject.TryGetComponent(out Terrain terrain))
            {
                var result = ScriptableObject.CreateInstance<TreeSelection>();
                result.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild | HideFlags.NotEditable;
                result.Initialize(terrain.gameObject, selectedIndices);
                return result;
            }

            return null;
        }
    }
}
