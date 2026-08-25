// Copyright © Magnetic Arcade. All Rights Reserved.

using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace MA.Flora.Editor
{
    internal static class FloraEditorUtility
    {
        // [MenuItem("Window/Flora/Cleanup Render Pipeline Global Settings", false)]
        public static void CleanupGlobalGraphicsSettings()
        {
            Type currentRenderPipelineType = RenderPipelineManager.currentPipeline?.GetType();
            if (currentRenderPipelineType == null)
                return;

            RenderPipelineGlobalSettings currentInstance = GraphicsSettings.GetSettingsForRenderPipeline(currentRenderPipelineType);
            if (currentInstance == null)
                return;

            var methodInfo = currentInstance.GetType().GetMethod("CleanNullSettings", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            if (methodInfo == null)
                return;

            bool wasActive = FloraSystem.Active;
            if (wasActive)
                FloraSystem.Shutdown();

            methodInfo.Invoke(currentInstance, null);

            if (wasActive)
                FloraSystem.InitializeIfNeeded();
        }

        public static void ConvertContainerToGameObjects(FloraInstanceContainer container, bool instanced, bool undoCreate = true)
        {
            if (!container || !container.Prefab)
                return;

            var prefab = container.Prefab;
            var parentTransform = container.transform.parent;

            for (int instanceIndex = 0; instanceIndex < container.InstanceCount; instanceIndex++)
            {
                var worldTransform = container.GetInstanceTransform(instanceIndex, Space.World);
                var prefabInstance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parentTransform);
                prefabInstance.transform.CopyFrom(worldTransform, Space.World);
                if (instanced && !prefabInstance.TryGetComponent(out FloraInstanceRenderer _))
                    prefabInstance.AddComponent<FloraInstanceRenderer>();

                if (undoCreate)
                    Undo.RegisterCreatedObjectUndo(prefabInstance, "Revert To GameObjects");
            }

            Object.DestroyImmediate(container.gameObject);
        }
    }
}
