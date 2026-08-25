// Copyright © Magnetic Arcade. All Rights Reserved.

using MA.Flora.Editor.InternalBridge;
using UnityEditor;
using UnityEditor.EditorTools;
using UnityEngine;

namespace MA.Flora.Editor
{
    [InitializeOnLoad]
    internal static class InstanceHandles
    {
        public static bool ViewToolActive =>
            // todo Tools.viewToolActive should be handling the modifier check, but 2022.2 broke this
            Tools.viewToolActive || Tools.current == Tool.View || (Event.current.modifiers & EventModifiers.Alt) == EventModifiers.Alt;

        static InstanceHandles()
        {
            Selection.selectionChanged += OnSelectionChange;
            Undo.undoRedoPerformed += OnUndoRedo;
            Tools.pivotModeChanged += OnPivotModeChanged;
            Tools.pivotRotationChanged += OnPivotRotationChanged;
            ToolManager.activeToolChanged += OnActiveToolChanged;
        }

        private static void OnSelectionChange()
        {
            ResetGlobalHandleRotation();
            InvalidateHandlePosition();
            LocalHandleOffset = Vector3.zero;
        }

        private static void OnUndoRedo()
        {
            s_GlobalHandleRotation = Tools.handleRotation;
            OnSelectionChange();
        }

        private static void OnPivotModeChanged()
        {
            InvalidateHandlePosition();
            ResetGlobalHandleRotation();
        }

        private static void OnPivotRotationChanged()
        {
            InvalidateHandlePosition();
            ResetGlobalHandleRotation();
        }

        private static void OnActiveToolChanged()
        {
            ResetGlobalHandleRotation();
        }

        public static void ResetGlobalHandleRotation()
        {
            s_GlobalHandleRotation = Quaternion.identity;
        }

        public static InstanceSelectionGroup ActiveInstanceSelectionGroup
        {
            get
            {
                if (Selection.activeObject is InstanceSelectionGroup { IsEmpty: false } group)
                    return group;

                return null;
            }
        }

        private static Vector3 s_HandlePosition;
        private static bool s_HandlePositionComputed;
        public static Vector3 CachedHandlePosition
        {
            get
            {
                if (!s_HandlePositionComputed)
                {
                    s_HandlePosition = GetHandlePosition();
                    s_HandlePositionComputed = true;
                }

                return s_HandlePosition;
            }
        }

        public static void InvalidateHandlePosition()
        {
            s_HandlePositionComputed = false;
        }

        public static Vector3 HandlePosition
        {
            get
            {
                if (!ActiveInstanceSelectionGroup)
                    return new Vector3(Mathf.Infinity, Mathf.Infinity, Mathf.Infinity);

                return s_LockHandlePositionActive ? s_LockHandlePosition : CachedHandlePosition;
            }
        }

        public static Vector3 GetHandlePosition()
        {
            if (!ActiveInstanceSelectionGroup)
                return new Vector3(Mathf.Infinity, Mathf.Infinity, Mathf.Infinity);

            Vector3 totalOffset = HandleOffset + HandleRotation * LocalHandleOffset;
            switch (PivotMode)
            {
                case PivotMode.Center:
                    return InstanceSelectionGroup.GetSelectionBounds().center + totalOffset;
                case PivotMode.Pivot:
                    return ActiveInstanceSelectionGroup.GetInstancePosition(ActiveInstanceSelectionGroup.ActiveSelectionIndex) + totalOffset;
                default:
                    return new Vector3(Mathf.Infinity, Mathf.Infinity, Mathf.Infinity);
            }
        }

        private static Vector3 s_LockHandlePosition;
        private static bool s_LockHandlePositionActive;

        public static void LockHandlePosition(Vector3 position)
        {
            s_LockHandlePosition = position;
            s_LockHandlePositionActive = true;
            HandlesBridge.LockHandlePosition(position);
        }

        public static void LockHandlePosition()
        {
            LockHandlePosition(HandlePosition);
        }

        public static void UnlockHandlePosition()
        {
            s_LockHandlePositionActive = false;
            HandlesBridge.UnlockHandlePosition();
        }

        public static PivotMode PivotMode
        {
            get => Tools.pivotMode;
            set => Tools.pivotMode = value;
        }

        public static Quaternion HandleRotation
        {
            get
            {
                switch (PivotRotation)
                {
                    case PivotRotation.Global:
                        return Tools.handleRotation = s_GlobalHandleRotation.normalized;
                    case PivotRotation.Local:
                        return HandleLocalRotation.normalized;
                }

                return Quaternion.identity;
            }
            set
            {
                if (PivotRotation == PivotRotation.Global)
                    Tools.handleRotation = s_GlobalHandleRotation = value.normalized;
            }
        }

        public static PivotRotation PivotRotation
        {
            get => Tools.pivotRotation;
            set => Tools.pivotRotation = value;
        }

        public static Vector3 HandleOffset;
        public static Vector3 LocalHandleOffset;

        private static Quaternion s_GlobalHandleRotation = Quaternion.identity;

        public static Quaternion HandleLocalRotation
        {
            get
            {
                if (!ActiveInstanceSelectionGroup)
                    return Quaternion.identity;

                return ActiveInstanceSelectionGroup.GetInstanceRotation(ActiveInstanceSelectionGroup.ActiveSelectionIndex).normalized;
            }
        }
    }
}
