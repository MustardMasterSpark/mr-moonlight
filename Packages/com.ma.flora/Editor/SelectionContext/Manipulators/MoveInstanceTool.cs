// Copyright © Magnetic Arcade. All Rights Reserved.

using MA.Flora.Editor.InternalBridge;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

namespace MA.Flora.Editor
{
    internal class MoveInstanceTool : InstanceManipulationTool
    {
        public override GUIContent toolbarIcon => L10n.TextContentWithIcon("Instance Move Tool", "Move Instances", "MoveTool");

        public override bool gridSnapEnabled => Tools.pivotRotation == PivotRotation.Global;

        protected override void ToolGUI(SceneView view, Vector3 handlePosition, bool isStatic)
        {
            if (view.camera.transform.position.Equals(handlePosition))
                return;

            var selection = Selection.GetFiltered<InstanceSelectionGroup>(SelectionMode.Unfiltered);
            InstanceManipulator.BeginManipulationHandling(false);

            EditorGUI.BeginChangeCheck();
            float3 positionHandle = Handles.DoPositionHandle(handlePosition, InstanceManipulator.MouseDownHandleRotation);
            if (EditorGUI.EndChangeCheck() && !isStatic && InstanceManipulator.HandleHasMoved(positionHandle))
            {
                RecordSelection("Move", selection);
                InstanceManipulationUtility.SetMinDragDifferenceForPos(handlePosition);

                if (ToolsBridge.IsVertexDragging())
                    InstanceManipulationUtility.DisableMinDragDifference();

                InstanceManipulator.SetPositionDelta(positionHandle, InstanceManipulator.MouseDownHandlePosition);
            }

            InstanceManipulator.EndManipulationHandling();
        }
    }
}
