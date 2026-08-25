// Copyright © Magnetic Arcade. All Rights Reserved.

using MA.Flora.Editor.InternalBridge;
using UnityEditor;
using UnityEditor.EditorTools;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityObject = UnityEngine.Object;

namespace MA.Flora.Editor
{
    internal abstract class InstanceManipulationTool : EditorTool
    {
        private bool m_SavedSelection;

        internal static readonly GUIContent StaticLabel = L10n.TextContent("Static");

        public override void OnToolGUI(EditorWindow window)
        {
            if (window is not SceneView view)
                return;
            if (Tools.hidden || InstanceHandles.ViewToolActive)
                return;
            if (Selection.activeObject is not InstanceSelectionGroup selectionGroup || !selectionGroup.Target)
                return;
            if (StageUtilityBridge.IsGizmoCulledBySceneCullingMasksOrFocusedScene(selectionGroup.Target, Camera.current))
                return;

            var e = Event.current;
            switch (e.type)
            {
                case EventType.Layout:
                    InstanceInspectorOverlay.UpdateInspectors();
                    InstanceHandles.InvalidateHandlePosition(); // Some cases that should invalidate the cached position are not handled correctly yet so we refresh it once per frame
                    break;
                case EventType.MouseDown:
                    m_SavedSelection = false;
                    break;
            }

            bool isDisabled = ShouldToolGUIBeDisabled(out GUIContent disabledLabel);
            using (new EditorGUI.DisabledScope(isDisabled))
            {
                var handlePosition = InstanceHandles.HandlePosition;
                ToolGUI(view, handlePosition, isDisabled);

                if (isDisabled)
                    Handles.Label(handlePosition, disabledLabel);
            }
        }

        protected abstract void ToolGUI(SceneView view, Vector3 handlePosition, bool isStatic);

        protected void ResetGlobalHandleRotationIfNeeded()
        {
            if (InstanceHandles.PivotRotation == PivotRotation.Global && Event.current.GetTypeForControl(GUIUtility.hotControl) == EventType.MouseUp)
            {
                InstanceHandles.ResetGlobalHandleRotation();
            }
        }

        protected bool RecordSelection(string undoType)
        {
            if (!m_SavedSelection)
            {
                var selection = Selection.GetFiltered<InstanceSelectionGroup>(SelectionMode.Unfiltered);
                return RecordSelection(undoType, selection);
            }

            return false;
        }

        protected bool RecordSelection(string undoType, InstanceSelectionGroup[] selection)
        {
            if (!m_SavedSelection)
            {
                m_SavedSelection = true;

                var undoObjects = new UnityObject[selection.Length];
                string undoName = "";
                for (int i = 0; i < selection.Length; i++)
                {
                    undoName = selection[i].name;
                    undoObjects[i] = selection[i].Target;
                    selection[i].RecordUndo(undoType);
                    EditorSceneManager.MarkSceneDirty(selection[i].Target.scene);
                }

                Undo.RegisterCompleteObjectUndo(undoObjects, $"{undoType} Selected Instances");
                if (undoObjects.Length == 1)
                    Undo.SetCurrentGroupName($"{undoType}" + undoName);

                return true;
            }

            return false;
        }

        protected virtual bool ShouldToolGUIBeDisabled(out GUIContent disabledLabel)
        {
            disabledLabel = StaticLabel;

            if (EditorApplication.isPlaying && !Tools.hidden)
            {
                var selectionGroups = Selection.GetFiltered<InstanceSelectionGroup>(SelectionMode.Unfiltered);
                return ContainsMainStageGameObjects(selectionGroups) && ContainsStatic(selectionGroups);
            }

            return false;
        }

        protected static void RotateGroup(InstanceSelectionGroup group, Vector3 handlePosition, Quaternion startPivotRotation, Vector3 axis, float angleDeg)
        {
            foreach (int selectionIndex in group.SelectionIndices)
            {
                if (InstanceHandles.PivotMode == PivotMode.Center)
                {
                    // Axis is world-space already for both Global and Local gizmo modes.
                    var t = group.GetInstanceTransform(selectionIndex);
                    t = t.RotateAround(handlePosition, axis, angleDeg * Mathf.Deg2Rad);
                    group.UpdateInstanceTransform(selectionIndex, t);
                }
                else if (InstanceManipulator.IndividualSpace)
                {
                    // Local rotation (Pivot mode with Local axis).
                    FloraInstanceTransform t = group.GetInstanceTransform(selectionIndex);
                    t = t.Rotate(axis, angleDeg * Mathf.Deg2Rad);
                    group.UpdateInstanceTransform(selectionIndex, t);
                }
                else
                {
                    // Pivot mode with Global axis.
                    FloraInstanceTransform t = group.GetInstanceTransform(selectionIndex);
                    t = t.Rotate(startPivotRotation * axis, angleDeg * Mathf.Deg2Rad);
                    group.UpdateInstanceTransform(selectionIndex, t);
                }
            }
        }

        internal static bool ContainsStatic(InstanceSelectionGroup[] groups)
        {
            if (groups == null || groups.Length == 0)
                return false;

            for (int i = 0; i < groups.Length; i++)
            {
                if (groups[i] != null && groups[i].Target != null && groups[i].Target.isStatic)
                    return true;
            }

            return false;
        }

        internal static bool ContainsMainStageGameObjects(InstanceSelectionGroup[] groups)
        {
            if (groups == null || groups.Length == 0)
                return false;

            for (int i = 0; i < groups.Length; i++)
            {
                if (groups[i] != null && groups[i].Target != null && StageUtility.GetStageHandle(groups[i].Target).IsMainStage())
                    return true;
            }

            return false;
        }
    }
}
