// Copyright © Magnetic Arcade. All Rights Reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using MA.Flora.Editor.InternalBridge;
using UnityEditor;
using UnityEditor.Actions;
using UnityEditor.EditorTools;
using UnityEditor.ShortcutManagement;
using UnityEngine;
using UnityEngine.UIElements;

namespace MA.Flora.Editor
{
    [EditorToolContext("Instance Selection")]
    [Icon("Packages/com.ma.flora/Editor/EditorResources/Icons/Selection Icon.png")]
    public class InstanceSelectionContext : EditorToolContext
    {
        private InstanceSelector m_Selector = new();
        private HashSet<SceneView> m_SceneViews = new();
        private ScenePickerGameObject m_ScenePickerGameObject;

        [Shortcut("Flora/Activate Selection Context")]
        private static void ToggleContextActive()
        {
            if (ToolManager.activeContextType == typeof(InstanceSelectionContext))
                ToolManager.SetActiveContext(typeof(GameObjectToolContext));
            else
                ToolManager.SetActiveContext<InstanceSelectionContext>();
        }

        public override void OnActivated()
        {
            var pickerObjectGO = new GameObject("Flora Scene Picker Object");
            pickerObjectGO.hideFlags = HideFlags.HideAndDontSave;
            m_ScenePickerGameObject = pickerObjectGO.AddComponent<ScenePickerGameObject>();

            if (FloraSystem.Active)
                FloraSystem.Instance.AllowDensityCullingOverride = false;

            m_Selector.Register();
        }

        public override void OnWillBeDeactivated()
        {
            m_Selector.Unregister();

            if (m_ScenePickerGameObject)
            {
                DestroyImmediate(m_ScenePickerGameObject.gameObject);
                m_ScenePickerGameObject = null;
            }

            if (FloraSystem.Active)
                FloraSystem.Instance.AllowDensityCullingOverride = true;

            foreach (var view in m_SceneViews)
            {
                view.EnableRectSelector();
                break; // Only enable rect selector for the first SceneView
            }

            m_SceneViews.Clear();

            // Remove any SelectionGroupBase from the selection
            var selectedGroups = Selection.GetFiltered<InstanceSelectionGroup>(SelectionMode.Unfiltered);
            var selectedObjects = Selection.objects;
            Selection.objects = selectedObjects.Except(selectedGroups).ToArray();
        }

        public override void OnToolGUI(EditorWindow window)
        {
            if (window is SceneView view)
            {
                FloraSystem.Instance?.EditorRequiresFrameUpdate();

                if (m_SceneViews.Add(view))
                    view.DisableRectSelector();

                m_Selector.OnGUI(view);
            }
        }

        protected override Type GetEditorToolType(Tool tool)
        {
            var toolType = tool switch
            {
                Tool.Move      => typeof(MoveInstanceTool),
                Tool.Rotate    => typeof(RotateInstanceTool),
                Tool.Scale     => typeof(ScaleInstanceTool),
                Tool.Transform => typeof(TransformInstanceTool),
                _              => null
            };

            return toolType;
        }

        public override void PopulateMenu(DropdownMenu menu)
        {
            ContextMenuUtility.AddClipboardEntriesTo(
                menu: menu,
                cutEnabled: false,
                copyEnabled: false,
                pasteEnabled: false,
                duplicateEnabled: false,
                deleteEnabled: true);
        }
    }
}
