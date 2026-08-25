// Copyright © Magnetic Arcade. All Rights Reserved.

using System;
using System.Collections.Generic;
using MA.Flora.Editor.InternalBridge;
using UnityEditor;
using UnityEngine;

namespace MA.Flora.Editor
{
    internal static class InstanceManipulator
    {
        private const int MaxDecimals = 15;

        private static float RoundBasedOnMinimumDifference(float valueToRound, float minDifference)
        {
            if (minDifference == 0)
                return DiscardLeastSignificantDecimal(valueToRound);
            return (float)Math.Round(valueToRound, GetNumberOfDecimalsForMinimumDifference(minDifference), MidpointRounding.AwayFromZero);
        }

        private static int GetNumberOfDecimalsForMinimumDifference(float minDifference)
        {
            return Mathf.Clamp(-Mathf.FloorToInt(Mathf.Log10(Mathf.Abs(minDifference))), 0, MaxDecimals);
        }

        private static float DiscardLeastSignificantDecimal(float v)
        {
            int decimals = Mathf.Clamp((int)(5 - Mathf.Log10(Mathf.Abs(v))), 0, MaxDecimals);
            return (float)Math.Round(v, decimals, MidpointRounding.AwayFromZero);
        }

        private struct TransformData
        {
            public static readonly Quaternion[] Alignments =
            {
                Quaternion.LookRotation(Vector3.right, Vector3.up),
                Quaternion.LookRotation(Vector3.right, Vector3.forward),
                Quaternion.LookRotation(Vector3.up, Vector3.forward),
                Quaternion.LookRotation(Vector3.up, Vector3.right),
                Quaternion.LookRotation(Vector3.forward, Vector3.right),
                Quaternion.LookRotation(Vector3.forward, Vector3.up)
            };

            public InstanceSelectionGroup InstanceSelectionGroup;
            public int InstanceIndex;
            public Transform TransformParent;
            public Vector3 Position;
            public Vector3 CurrentPosition => InstanceSelectionGroup.GetInstancePosition(InstanceIndex);
            public Vector3 LocalPosition;
            public Quaternion Rotation;
            public Vector3 LocalScale;
            public Vector2 SizeDelta;

            public static TransformData GetData(InstanceSelectionGroup t, int instanceIndex)
            {
                var data = new TransformData();
                data.SetupTransformValues(t, instanceIndex);
                return data;
            }

            private static Quaternion GetRefAlignment(Quaternion targetRotation, Quaternion ownRotation)
            {
                var biggestDot = Mathf.NegativeInfinity;
                var refAlignment = Quaternion.identity;
                for (int i = 0; i < Alignments.Length; i++)
                {
                    var dot = Mathf.Min(
                        Mathf.Abs(Vector3.Dot(targetRotation * Vector3.right, ownRotation * Alignments[i] * Vector3.right)),
                        Mathf.Abs(Vector3.Dot(targetRotation * Vector3.up, ownRotation * Alignments[i] * Vector3.up)),
                        Mathf.Abs(Vector3.Dot(targetRotation * Vector3.forward, ownRotation * Alignments[i] * Vector3.forward)));

                    if (dot > biggestDot)
                    {
                        biggestDot = dot;
                        refAlignment = Alignments[i];
                    }
                }
                return refAlignment;
            }

            private void SetupTransformValues(InstanceSelectionGroup group, int instanceIndex)
            {
                InstanceSelectionGroup = group;
                InstanceIndex = instanceIndex;
                TransformParent = group.Target.transform;
                Position = group.GetInstancePosition(InstanceIndex);
                Rotation = group.GetInstanceRotation(InstanceIndex);
                LocalPosition = TransformParent.InverseTransformPoint(group.GetInstancePosition(InstanceIndex));
                LocalScale = TransformParent.InverseTransformScale(group.GetInstanceScale(InstanceIndex));
            }

            private void UpdateTransformValues()
            {
                TransformParent = InstanceSelectionGroup.Target.transform;
                LocalPosition = TransformParent != null ? TransformParent.InverseTransformPoint(Position) : Position;
            }

            private void SetScaleValue(Vector3 scale)
            {
                InstanceSelectionGroup.UpdateInstanceScale(InstanceIndex, TransformParent.TransformScale(scale));
            }

            public void SetScaleDelta(Vector3 scaleDelta, Vector3 scalePivot, Quaternion scaleRotation)
            {
                SetPosition(scaleRotation * Vector3.Scale(Quaternion.Inverse(scaleRotation) * (Position - scalePivot), scaleDelta) + scalePivot);

                var minDifference = InstanceManipulationUtility.MinDragDifference;
                if (TransformParent != null)
                {
                    minDifference.x /= TransformParent.lossyScale.x;
                    minDifference.y /= TransformParent.lossyScale.y;
                    minDifference.z /= TransformParent.lossyScale.z;
                }

                var ownRotation = Rotation;
                var refAlignment = GetRefAlignment(scaleRotation, ownRotation);
                scaleDelta = refAlignment * scaleDelta;
                scaleDelta = Vector3.Scale(scaleDelta, refAlignment * Vector3.one);

                scaleDelta.x = RoundBasedOnMinimumDifference(scaleDelta.x, minDifference.x);
                scaleDelta.y = RoundBasedOnMinimumDifference(scaleDelta.y, minDifference.y);
                scaleDelta.z = RoundBasedOnMinimumDifference(scaleDelta.z, minDifference.z);
                SetScaleValue(Vector3.Scale(LocalScale, scaleDelta));
            }

            private void SetPosition(Vector3 newPosition)
            {
                SetPositionDelta(newPosition - Position, true);
            }

            public void SetPositionDelta(Vector3 positionDelta, bool applySmartRounding)
            {
                if (InstanceSelectionGroup.Target.transform != TransformParent)
                    UpdateTransformValues();

                var localPositionDelta = positionDelta;
                if (TransformParent != null)
                {
                    localPositionDelta = TransformParent.InverseTransformVector(localPositionDelta);

                    if (!applySmartRounding)
                        applySmartRounding = !TransformParent.localRotation.Equals(Quaternion.identity);
                }

                // If we are snapping, disable the smart rounding. If not the case, the transform will have the wrong snap value based on distance to screen.
                applySmartRounding &= !(EditorSnapSettings.incrementalSnapActive || EditorSnapSettings.gridSnapActive || EditorSnapSettingsBridge.IsVertexSnapActive());

                var zeroXDelta = false;
                var zeroYDelta = false;
                var zeroZDelta = false;
                var minDifference = InstanceManipulationUtility.MinDragDifference;
                if (applySmartRounding)
                {
                    // For zero delta, we don't want to change the value so we ignore rounding
                    zeroXDelta = Mathf.Approximately(localPositionDelta.x, 0f);
                    zeroYDelta = Mathf.Approximately(localPositionDelta.y, 0f);
                    zeroZDelta = Mathf.Approximately(localPositionDelta.z, 0f);

                    if (TransformParent != null)
                    {
                        minDifference.x /= TransformParent.lossyScale.x;
                        minDifference.y /= TransformParent.lossyScale.y;
                        minDifference.z /= TransformParent.lossyScale.z;
                    }
                }

                var newLocalPosition = LocalPosition + localPositionDelta;

                if (applySmartRounding)
                {
                    newLocalPosition.x = zeroXDelta ? LocalPosition.x : RoundBasedOnMinimumDifference(newLocalPosition.x, minDifference.x);
                    newLocalPosition.y = zeroYDelta ? LocalPosition.y : RoundBasedOnMinimumDifference(newLocalPosition.y, minDifference.y);
                    newLocalPosition.z = zeroZDelta ? LocalPosition.z : RoundBasedOnMinimumDifference(newLocalPosition.z, minDifference.z);
                }

                InstanceSelectionGroup.UpdateInstancePosition(InstanceIndex, TransformParent.TransformPoint(newLocalPosition));
            }
        }

        private static EventType s_EventTypeBefore = EventType.Ignore;
        private static List<TransformData> s_MouseDownState = new();
        private static Vector3 s_StartHandlePosition = Vector3.zero;
        private static Vector3 s_PreviousHandlePosition = Vector3.zero;
        private static Quaternion s_StartHandleRotation = Quaternion.identity;
        public static Vector3 MouseDownHandlePosition => s_StartHandlePosition;
        public static Quaternion MouseDownHandleRotation { get => s_StartHandleRotation; set => s_StartHandleRotation = value; }
        private static Vector3 s_StartLocalHandleOffset = Vector3.zero;
        private static int s_HotControl;
        private static bool s_LockHandle;

        public static bool Active => s_MouseDownState.Count > 0;
        public static bool IndividualSpace => InstanceHandles.PivotRotation == PivotRotation.Local && InstanceHandles.PivotMode == PivotMode.Pivot;

        private static void BeginEventCheck()
        {
            var previousEvent = s_EventTypeBefore;
            s_EventTypeBefore = Event.current.GetTypeForControl(s_HotControl);
            if (!Active || (previousEvent != EventType.MouseDown && s_EventTypeBefore == EventType.MouseDown))
            {
                s_StartHandleRotation = InstanceHandles.HandleRotation;
            }
        }

        private static EventType EndEventCheck()
        {
            var usedEvent = (s_EventTypeBefore != Event.current.GetTypeForControl(s_HotControl) ? s_EventTypeBefore : EventType.Ignore);
            s_EventTypeBefore = EventType.Ignore;
            if (usedEvent == EventType.MouseDown)
                s_HotControl = GUIUtility.hotControl;
            else if (usedEvent == EventType.MouseUp)
                s_HotControl = 0;
            return usedEvent;
        }

        public static void BeginManipulationHandling(bool lockHandleWhileDragging)
        {
            BeginEventCheck();
            s_LockHandle = lockHandleWhileDragging;
        }

        public static EventType EndManipulationHandling()
        {
            var usedEvent = EndEventCheck();

            if (usedEvent == EventType.MouseDown)
            {
                RecordMouseDownState(Selection.GetFiltered<InstanceSelectionGroup>(SelectionMode.Unfiltered));
                s_StartHandlePosition = InstanceHandles.HandlePosition;
                s_PreviousHandlePosition = s_StartHandlePosition;
                s_StartLocalHandleOffset = InstanceHandles.LocalHandleOffset;
                if (s_LockHandle)
                    InstanceHandles.LockHandlePosition();
            }
            else if (s_MouseDownState.Count > 0 && (usedEvent == EventType.MouseUp || GUIUtility.hotControl != s_HotControl))
            {
                s_StartHandleRotation = InstanceHandles.HandleRotation;
                s_MouseDownState.Clear();
                if (s_LockHandle)
                    InstanceHandles.UnlockHandlePosition();

                InstanceManipulationUtility.DisableMinDragDifference();
            }

            return usedEvent;
        }

        private static void RecordMouseDownState(InstanceSelectionGroup[] selection)
        {
            s_MouseDownState.Clear();

            for (var i = 0; i < selection.Length; i++)
            {
                var group = selection[i];

                for (var selectedIndex = 0; selectedIndex < group.SelectionIndices.Length; selectedIndex++)
                {
                    int instanceIndex = group.SelectionIndices[selectedIndex];
                    s_MouseDownState.Add(TransformData.GetData(group, instanceIndex));
                }
            }
        }

        private static void SetLocalHandleOffsetScaleDelta(Vector3 scaleDelta, Quaternion pivotRotation)
        {
            var refAlignment = Quaternion.Inverse(InstanceHandles.HandleRotation) * pivotRotation;
            InstanceHandles.LocalHandleOffset = Vector3.Scale(Vector3.Scale(s_StartLocalHandleOffset, refAlignment * scaleDelta), refAlignment * Vector3.one);
        }

        public static void SetScaleDelta(Vector3 scaleDelta, Quaternion pivotRotation)
        {
            if (s_MouseDownState.Count == 0)
                return;

            SetLocalHandleOffsetScaleDelta(scaleDelta, pivotRotation);

            var pivot = InstanceHandles.HandlePosition;
            for (int i = 0; i < s_MouseDownState.Count; i++)
            {
                // Scale about handlePosition or local pivot based on pivotMode
                if (InstanceHandles.PivotMode == PivotMode.Pivot)
                    pivot = s_MouseDownState[i].Position;
                if (IndividualSpace)
                    pivotRotation = s_MouseDownState[i].Rotation;

                s_MouseDownState[i].SetScaleDelta(scaleDelta, pivot, pivotRotation);
            }
        }

        public static void SetResizeDelta(Vector3 scaleDelta, Vector3 pivotPosition, Quaternion pivotRotation)
        {
            if (s_MouseDownState.Count == 0)
                return;

            SetLocalHandleOffsetScaleDelta(scaleDelta, pivotRotation);

            for (int i = 0; i < s_MouseDownState.Count; i++)
                s_MouseDownState[i].SetScaleDelta(scaleDelta, pivotPosition, pivotRotation);
        }

        public static void SetPositionDelta(Vector3 newPosition, Vector3 oldPosition)
        {
            if (s_MouseDownState.Count == 0)
                return;

            s_PreviousHandlePosition = newPosition;
            var positionDelta = newPosition - oldPosition;

            s_MouseDownState[0].SetPositionDelta(positionDelta, true);
            var firstDelta = s_MouseDownState[0].CurrentPosition - s_MouseDownState[0].Position;

            for (int i = 1; i < s_MouseDownState.Count; i++)
                s_MouseDownState[i].SetPositionDelta(firstDelta, false);
        }

        public static bool HandleHasMoved(Vector3 position)
        {
            return position != s_PreviousHandlePosition;
        }
    }
}
