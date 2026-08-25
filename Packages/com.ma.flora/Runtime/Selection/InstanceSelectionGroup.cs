// Copyright © Magnetic Arcade. All Rights Reserved.

#if UNITY_EDITOR
using System;
using System.Linq;
using Unity.Collections;
using UnityEngine;
using Unity.Mathematics;
using UnityEditor;

namespace MA.Flora
{
    [Icon("Packages/com.ma.flora/Editor/EditorResources/Icons/Instance Icon.png")]
    internal abstract class InstanceSelectionGroup : ScriptableObject
    {
        [SerializeField] protected GameObject m_Target;
        [SerializeField] protected int[] m_SelectionIndices;
        [SerializeField] protected uint4 m_Hash;
        [NonSerialized] private int m_RefCount;

        //---------------------------------------------------------------------------------
        // Static selection event tracking
        //---------------------------------------------------------------------------------

        private static InstanceSelectionGroup[] s_LastSelected = Array.Empty<InstanceSelectionGroup>();

        [InitializeOnLoadMethod]
        private static void InitSelectionCallbacks()
        {
            Selection.selectionChanged += () =>
            {
                var currentSelection = Selection.GetFiltered<InstanceSelectionGroup>(SelectionMode.Deep);

                var addedSelectionGroups = currentSelection.Except(s_LastSelected);
                foreach (InstanceSelectionGroup selectionGroup in addedSelectionGroups)
                    selectionGroup.Retain();

                var removedSelectionGroups = s_LastSelected.Except(currentSelection);
                foreach (InstanceSelectionGroup selectionGroup in removedSelectionGroups)
                    selectionGroup.Release();

                s_LastSelected = currentSelection;
            };
            EditorApplication.playModeStateChanged += state =>
            {
                if (state is PlayModeStateChange.ExitingPlayMode or PlayModeStateChange.ExitingEditMode)
                {
                    var currentSelection = Selection.GetFiltered<InstanceSelectionGroup>(SelectionMode.Deep);
                    foreach (var selectionGroup in currentSelection)
                        selectionGroup.Release();
                }
            };
        }

        public static Bounds GetSelectionBounds()
        {
            var bounds = AxisAlignedBox.Empty;
            var currentSelection = Selection.GetFiltered<InstanceSelectionGroup>(SelectionMode.Deep);
            foreach (var selectionGroup in currentSelection)
                bounds.Encapsulate(selectionGroup.CalculateBounds());

            return bounds;
        }

        //---------------------------------------------------------------------------------
        // Properties
        //---------------------------------------------------------------------------------

        public bool IsValid => m_Target != null && Length > 0;

        public bool IsEmpty => !m_Target || Length == 0;

        public GameObject Target => m_Target;

        public uint4 Hash => m_Hash;

        public int Length => m_SelectionIndices?.Length ?? 0;

        public int[] SelectionIndices => m_SelectionIndices;

        public int ActiveSelectionIndex => m_SelectionIndices?.FirstOrDefault() ?? -1;

        //---------------------------------------------------------------------------------
        // Lifecycle
        //---------------------------------------------------------------------------------

        public void Initialize(GameObject target, int[] selectedIndices)
        {
            unsafe
            {
                m_Target = target;
                m_SelectionIndices = selectedIndices;

                var hash = new xxHash3.StreamingState(false, (ulong)GetType().GetHashCode());
                hash.Update(target.GetEntityId());
                hash.Update(selectedIndices.Length);
                fixed (int* indices = selectedIndices)
                    hash.Update((byte*)indices, selectedIndices.Length * sizeof(int));

                m_Hash = hash.DigestHash128();

                OnCreate(target, selectedIndices);

                int undoGroup = Undo.GetCurrentGroup();
                Undo.RegisterCreatedObjectUndo(this, $"Create {nameof(InstanceSelectionGroup)}:{target.name}:{selectedIndices.Length}");
                Undo.CollapseUndoOperations(undoGroup);
            }
        }

        public virtual void OnCreate(GameObject target, int[] selectedIndices)
        {
        }

        public void Retain()
        {
            m_RefCount++;
        }

        public void Release()
        {
            if (!this)
                return;

            m_RefCount--;
            if (m_RefCount <= 0)
            {
                OnRelease();
                var undoGroup = Undo.GetCurrentGroup();
                var undoGroupName = Undo.GetCurrentGroupName();
                Undo.DestroyObjectImmediate(this);
                Undo.SetCurrentGroupName(undoGroupName);
                Undo.CollapseUndoOperations(undoGroup);
            }
        }

        public virtual void OnRelease()
        {
        }

        public abstract void RecordUndo(string name);

        //---------------------------------------------------------------------------------
        // Selection
        //---------------------------------------------------------------------------------

        public abstract NativeArray<FloraInstanceHandle> GetSelection();
        public abstract void DeleteSelected();
        public abstract FloraInstanceHandle GetInstanceHandleAt(int selectionIndex);

        //---------------------------------------------------------------------------------
        // Transforms
        //---------------------------------------------------------------------------------

        public abstract Bounds CalculateBounds();

        public abstract FloraInstanceTransform GetInstanceTransform(int selectionIndex);

        public abstract void UpdateInstanceTransform(int selectionIndex, FloraInstanceTransform worldTransform);

        public Vector3 GetInstancePosition(int instanceIndex)
        {
            return GetInstanceTransform(instanceIndex).Position;
        }

        public Quaternion GetInstanceRotation(int instanceIndex)
        {
            return GetInstanceTransform(instanceIndex).Rotation;
        }

        public Vector3 GetInstanceScale(int instanceIndex)
        {
            return GetInstanceTransform(instanceIndex).Scale;
        }

        public void UpdateInstancePosition(int index, Vector3 position)
        {
            var xf = GetInstanceTransform(index);
            xf.Position = position;
            UpdateInstanceTransform(index, xf);
        }

        public void UpdateInstanceRotation(int index, Quaternion rotation)
        {
            var xf = GetInstanceTransform(index);
            xf.Rotation = rotation;
            UpdateInstanceTransform(index, xf);
        }

        public void UpdateInstanceScale(int index, Vector3 scale)
        {
            var xf = GetInstanceTransform(index);
            xf.Scale = scale;
            UpdateInstanceTransform(index, xf);
        }
    }
}
#endif
