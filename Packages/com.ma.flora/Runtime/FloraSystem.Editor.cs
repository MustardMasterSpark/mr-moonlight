// Copyright © Magnetic Arcade. All Rights Reserved.

using System.Collections.Generic;
using System.Diagnostics;
using Unity.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace MA.Flora
{
    public sealed partial class FloraSystem
    {
        #region Editor

        [Conditional("UNITY_EDITOR")]
        internal void EditorRequiresFrameUpdate()
        {
#if UNITY_EDITOR
            m_FrameUpdateNeeded = true;
#endif
        }

        [Conditional("UNITY_EDITOR")]
        private void SetEditorDataChanged()
        {
#if UNITY_EDITOR
            m_SelectionChanged = true;
            m_SceneVisibilityChanged = true;
#endif
        }

#if UNITY_EDITOR
        // If running in the editor the player loop might not run
        // In order to still have a single frame update we keep track of the camera ids
        // A frame update happens in case the first camera is rendered again
        private void EditorFrameUpdate(List<Camera> cameras)
        {
            bool newFrame = false;
            foreach (Camera camera in cameras)
            {
                EntityId entityId = camera.GetEntityId();
                if (m_FrameCameraIDs.Length == 0 || m_FrameCameraIDs.Contains(entityId))
                {
                    newFrame = true;
                    m_FrameCameraIDs.Clear();
                }

                m_FrameCameraIDs.Add(entityId);
            }

            if (newFrame)
            {
                if (m_FrameUpdateNeeded)
                {
                    FrameInitialization();
                    FramePostLateUpdate();
                }
                else
                {
                    m_FrameUpdateNeeded = true;
                }
            }
        }

        private static bool s_WasRenderingActiveBeforeBake;

        private static void OnEditorBakeStarted()
        {
            if (Instance is not null)
            {
                s_WasRenderingActiveBeforeBake = Instance.RenderingEnabled;
                Instance.TeardownRendering();
            }
        }

        private static void OnEditorBakeCompleted()
        {
            if (Instance is not null)
            {
                Instance.RefreshInstanceRendererRenderSources();
                if (s_WasRenderingActiveBeforeBake)
                    Instance.SetupRendering();
            }

            s_WasRenderingActiveBeforeBake = false;
        }

        private static void OnEditorUndoRedo()
        {
            if (Instance != null)
            {
                Instance.SetSelectionDirty();

                if (Instance.RenderingEnabled)
                    Instance.DisableUnityRenderers();
            }
        }

        private static void OnSceneViewVisibilityChanged()
        {
            Instance?.SetSceneVisibilityDirty();
        }

        private void SetSelectionDirty()
        {
            m_SelectionChanged = true;
        }

        private void ProcessSelection()
        {
            if (!m_SelectionChanged)
                return;

            m_SelectionChanged = false;
            m_NativeContext.InstanceManager.ValueRW.ClearSelection();

            var selectionGroups = Selection.GetFiltered<InstanceSelectionGroup>(SelectionMode.Unfiltered);
            foreach (var selectionGroup in selectionGroups)
                m_NativeContext.InstanceManager.ValueRW.SetSelected(selectionGroup.GetSelection());

            var containers = Selection.GetFiltered<FloraInstanceContainer>(SelectionMode.Deep);
            foreach (var container in containers)
                m_NativeContext.InstanceManager.ValueRW.SetSelected(container.InstanceHandles);

            var instanceRenderers = Selection.GetFiltered<FloraInstanceRenderer>(SelectionMode.Deep);
            foreach (var instanceRenderer in instanceRenderers)
                m_NativeContext.InstanceManager.ValueRW.SetSelected(instanceRenderer.InstanceHandle);
        }

        private void SetSceneVisibilityDirty()
        {
            m_SceneVisibilityChanged = true;
        }

        private void ProcessSceneVisibility()
        {
            if (!m_SceneVisibilityChanged)
                return;

            m_SceneVisibilityChanged = false;
            m_NativeContext.InstanceManager.ValueRW.ClearHidden();
            m_NativeContext.TerrainManager.ValueRW.ClearHidden();

            bool isAnyObjectHidden = false;

            for (int i = 0; i < SceneManager.sceneCount; ++i)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (SceneVisibilityManager.instance.AreAnyDescendantsHidden(scene))
                {
                    isAnyObjectHidden = true;
                    break;
                }
            }

            if (!isAnyObjectHidden)
                return;

            foreach (var container in m_Containers.Values)
            {
                bool isHidden = SceneVisibilityManager.instance.IsHidden(container.gameObject);
                if (isHidden) m_NativeContext.InstanceManager.ValueRW.SetHidden(container.InstanceHandles);
            }

            foreach (var instanceRenderer in m_InstanceRenderers.Values)
            {
                bool isHidden = SceneVisibilityManager.instance.IsHidden(instanceRenderer.gameObject);
                if (isHidden) m_NativeContext.InstanceManager.ValueRW.SetHidden(instanceRenderer.InstanceHandle);
            }

            foreach (var terrain in m_Terrains.Values)
            {
                bool isHidden = SceneVisibilityManager.instance.IsHidden(terrain.gameObject);
                if (isHidden) m_NativeContext.TerrainManager.ValueRW.SetHidden(terrain);
            }
        }
#endif

        #endregion
    }
}
