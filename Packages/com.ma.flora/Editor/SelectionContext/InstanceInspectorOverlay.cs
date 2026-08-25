// Copyright © Magnetic Arcade. All Rights Reserved.

using System;
using UnityEditor;
using UnityEditor.EditorTools;
using UnityEditor.Overlays;
using UnityEngine;
using UnityEngine.UIElements;

namespace MA.Flora.Editor
{
    [Icon("Packages/com.ma.flora/Editor/EditorResources/Icons/Instance Icon.png")]
    [Overlay(typeof(SceneView), "instance-inspector", "Instance Inspector", "InstanceInspector", defaultDockPosition = DockPosition.Top, defaultDockZone = DockZone.LeftColumn, defaultDockIndex = 15, defaultLayout = Layout.Panel)]
    internal class InstanceInspectorOverlay : Overlay, ITransientOverlay
    {
        private static event Action ForceUpdateRequested;
        private static bool s_FirstUpdateSinceDomainReload = true;
        public static void ForceUpdate() => ForceUpdateRequested?.Invoke();

        private SelectionInspector m_SelectionGroupInspector;

        public bool visible => ToolManager.activeContextType == typeof(InstanceSelectionContext);

        public static void UpdateInspectors()
        {
            if (s_FirstUpdateSinceDomainReload)
            {
                s_FirstUpdateSinceDomainReload = false;
                ForceUpdate();
            }
        }

        public override VisualElement CreatePanelContent()
        {
            VisualElement root = new VisualElement();
            m_SelectionGroupInspector?.Dispose();
            root.Add(m_SelectionGroupInspector = new SelectionInspector());
            UpdateInspector();
            return root;
        }

        public override void OnCreated()
        {
            displayedChanged += OnDisplayedChange;
            Selection.selectionChanged += UpdateInspector;
            ForceUpdateRequested += UpdateInspector;
            Undo.undoRedoPerformed += OnUndoRedoPerformed;
        }

        public override void OnWillBeDestroyed()
        {
            m_SelectionGroupInspector?.Dispose();
            displayedChanged -= OnDisplayedChange;
            Selection.selectionChanged -= UpdateInspector;
            ForceUpdateRequested -= UpdateInspector;
            Undo.undoRedoPerformed -= OnUndoRedoPerformed;
        }

        private void OnDisplayedChange(bool displayed)
        {
            UpdateInspector();
        }

        private void UpdateInspector()
        {
            m_SelectionGroupInspector?.UpdateWithSelection();
        }

        private void OnUndoRedoPerformed()
        {
            ForceUpdate();
        }
    }
}
