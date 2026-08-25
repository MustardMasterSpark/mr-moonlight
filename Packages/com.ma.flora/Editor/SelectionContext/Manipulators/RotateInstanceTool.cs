// Copyright © Magnetic Arcade. All Rights Reserved.

using UnityEditor;
using UnityEngine;

namespace MA.Flora.Editor
{
    internal class RotateInstanceTool : InstanceManipulationTool
    {
        public override GUIContent toolbarIcon => L10n.TextContentWithIcon("Rotate Instances Tool", "Rotate Instances Tool", "RotateTool");

        protected override void ToolGUI(SceneView view, Vector3 handlePosition, bool isStatic)
        {
            ResetGlobalHandleRotationIfNeeded();
            InstanceManipulator.BeginManipulationHandling(true);

            Quaternion before = InstanceHandles.HandleRotation;
            EditorGUI.BeginChangeCheck();
            Quaternion after = Handles.DoRotationHandle(before, handlePosition);

            InstanceManipulator.EndManipulationHandling();

            if (EditorGUI.EndChangeCheck() && !isStatic)
            {
                Quaternion deltaRotation = after * Quaternion.Inverse(before);
                deltaRotation.ToAngleAxis(out float angle, out Vector3 axis);

                if (!Mathf.Approximately(angle, 0f))
                {
                    var selection = Selection.GetFiltered<InstanceSelectionGroup>(SelectionMode.Unfiltered);
                    RecordSelection("Rotate", selection);

                    Quaternion initialRotation = InstanceManipulator.MouseDownHandleRotation;
                    foreach (InstanceSelectionGroup group in selection)
                        RotateGroup(group, handlePosition, initialRotation, axis, angle);
                }

                InstanceHandles.HandleRotation = after;
            }
        }
    }
}
