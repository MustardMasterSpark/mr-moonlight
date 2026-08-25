// Copyright © Magnetic Arcade. All Rights Reserved.

using System;
using Unity.Collections;
using UnityEditor;
using UnityEngine;

namespace MA.Flora.Editor
{
    internal class ContainerSelection : InstanceSelectionGroup
    {
        [SerializeField] private FloraInstanceContainer m_InstanceContainer;

        public override void OnCreate(GameObject target, int[] selectedIndices)
        {
            m_InstanceContainer = target.GetComponent<FloraInstanceContainer>();
        }

        public override NativeArray<FloraInstanceHandle> GetSelection()
        {
            if (!IsValid)
                return new NativeArray<FloraInstanceHandle>(0, Allocator.Temp);

            var selectedHandles = new NativeArray<FloraInstanceHandle>(m_SelectionIndices.Length, Allocator.Temp);
            var containerInstanceHandles = m_InstanceContainer.InstanceHandles;

            for (int i = 0; i < m_SelectionIndices.Length; i++)
            {
                var containerIndex = m_SelectionIndices[i];
                if (containerInstanceHandles.IsValidIndex(containerIndex))
                {
                    var instanceHandle = containerInstanceHandles[containerIndex];
                    selectedHandles[i] = instanceHandle;
                }
            }

            return selectedHandles;
        }

        public override void RecordUndo(string name)
        {
            Undo.RegisterCompleteObjectUndo(m_InstanceContainer, name);
        }

        public override FloraInstanceHandle GetInstanceHandleAt(int selectionIndex)
        {
            if (!IsValid)
                return FloraInstanceHandle.Null;

            int instanceIndex = m_SelectionIndices[selectionIndex];
            return m_InstanceContainer.GetInstanceHandle(instanceIndex);
        }

        public override Bounds CalculateBounds()
        {
            if (!IsValid)
                return new Bounds();

            using var selectedInstanceHandles = new NativeList<FloraInstanceHandle>(m_SelectionIndices.Length, Allocator.TempJob);
            var containerInstanceHandles = m_InstanceContainer.InstanceHandles;
            for (int i = 0; i < m_SelectionIndices.Length; i++)
            {
                var instanceHandle = containerInstanceHandles[m_SelectionIndices[i]];
                selectedInstanceHandles.Add(instanceHandle);
            }

            return FloraSystem.Instance.CalculateInstanceBounds(selectedInstanceHandles.AsArray());
        }

        public override void DeleteSelected()
        {
            if (!IsValid)
                return;

            Undo.RegisterCompleteObjectUndo(m_InstanceContainer, "DeleteSelected");
            m_InstanceContainer.RemoveInstances(new NativeArray<int>(m_SelectionIndices, Allocator.Temp));
            m_SelectionIndices = Array.Empty<int>();
        }

        public override FloraInstanceTransform GetInstanceTransform(int selectionIndex)
        {
            return m_InstanceContainer.GetInstanceTransform(selectionIndex, Space.World);
        }

        public override void UpdateInstanceTransform(int selectionIndex, FloraInstanceTransform worldTransform)
        {
            m_InstanceContainer.UpdateInstanceTransform(selectionIndex, worldTransform, Space.World);
        }
    }
}
