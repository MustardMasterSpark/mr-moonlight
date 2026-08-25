// Copyright © Magnetic Arcade. All Rights Reserved.

using UnityEditor.SceneManagement;
using UnityEngine;

namespace MA.Flora.Editor.InternalBridge
{
    internal enum StageContextRenderMode
    {
        Normal,
        GreyedOut,
        Hidden,
    }

    internal static class StageUtilityBridge
    {
        public const ulong DefaultSceneCullingMask = SceneCullingMasks.DefaultSceneCullingMask;
        public const ulong GameViewObjects = SceneCullingMasks.GameViewObjects;
        public const ulong MainStageSceneViewObjects = SceneCullingMasks.MainStageSceneViewObjects;
        public const ulong MainStageExcludingPrefabInstanceObjectsOpenInPrefabMode = SceneCullingMasks.MainStageExcludingPrefabInstanceObjectsOpenInPrefabMode;
        public const ulong MainStagePrefabInstanceObjectsOpenInPrefabMode = SceneCullingMasks.MainStagePrefabInstanceObjectsOpenInPrefabMode;
        public const ulong PrefabStagePrefabInstanceObjectsOpenInPrefabMode = SceneCullingMasks.PrefabStagePrefabInstanceObjectsOpenInPrefabMode;

        internal static bool IsPrefabInstanceHiddenForInContextEditing(GameObject obj)
        {
            return StageUtility.IsPrefabInstanceHiddenForInContextEditing(obj);
        }

        internal static StageContextRenderMode GetContextRenderMode()
        {
            return (StageContextRenderMode)StageNavigationManager.instance.contextRenderMode;
        }

        internal static bool IsMainStage(this StageHandle stageHandle)
        {
            return stageHandle.isMainStage;
        }

        internal static bool IsGizmoCulledBySceneCullingMasksOrFocusedScene(GameObject gameObject, Camera camera)
        {
            return StageUtility.IsGizmoCulledBySceneCullingMasksOrFocusedScene(gameObject, camera);
        }
    }
}
