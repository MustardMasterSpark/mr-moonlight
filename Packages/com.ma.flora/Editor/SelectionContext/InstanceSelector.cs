// Copyright © Magnetic Arcade.
// Example with real-time updates and flicker-reduction checks.

using System;
using System.Collections.Generic;
using System.Linq;
using MA.InternalBridge;
using UnityEditor;
using UnityEditor.EditorTools;
using UnityEditor.ShortcutManagement;
using UnityEngine;
using UnityObject = UnityEngine.Object;

namespace MA.Flora.Editor
{
    internal class InstancePickingShortcutContext : IShortcutContext
    {
        public SceneView Window => EditorWindow.focusedWindow as SceneView;
        public bool active => Window && ToolManager.activeToolType.IsSubclassOf(typeof(InstanceManipulationTool));
    }

    internal class InstanceSelector
    {
        private enum SelectionType { Normal, Additive, Subtractive }

        private Vector2 m_SelectMousePoint;
        private Vector2 m_StartPoint;

        private SelectionType m_CurrentSelectionType;
        private UnityObject[] m_SelectionStart;
        private UnityObject[] m_CurrentSelection;
        private UnityObject[] m_LastSelection;

        private bool m_IsNearestControl;
        private int  m_RectSelectionID;

        private InstancePickingShortcutContext m_PickingShortcutContext = new();

        public static InstanceSelector Instance { get; private set; }
        public static event Action RectSelectionStarting  = delegate { };
        public static event Action RectSelectionFinished  = delegate { };

        private const string RectSelectionNormal      = "Flora/Box Select";
        private const string RectSelectionAdditive    = "Flora/Add Box Select";
        private const string RectSelectionSubtractive = "Flora/Invert Box Select";
        private const string PickingNormal            = "Flora/Select";
        private const string PickingAdditive          = "Flora/Add Select";
        private const string PickingSubtractive       = "Flora/Invert Select";

        private const string PickingEventCommandName              = "PickingEventCommand";
        private const string SetRectSelectionHotControlEventName  = "SetRectSelectionEventCommand";

        public void Register()
        {
            if (Instance != null)
                return;

            Instance = this;
            m_RectSelectionID = GUIUtilityBridge.GetPermanentControlID();
            ShortcutManager.RegisterContext(m_PickingShortcutContext);
        }

        public void Unregister()
        {
            if (Instance != this)
                return;

            CompleteRectSelection();
            ShortcutManager.UnregisterContext(m_PickingShortcutContext);
            Instance = null;
        }

        #region Shortcut Attributes

        [ClutchShortcut(RectSelectionNormal, typeof(InstancePickingShortcutContext), KeyCode.Mouse0)]
        private static void OnNormalRectSelection(ShortcutArguments args)
        {
            InvokeRectSelect(args, SelectionType.Normal);
        }

        [ClutchShortcut(RectSelectionAdditive, typeof(InstancePickingShortcutContext), KeyCode.Mouse0, ShortcutModifiers.Shift)]
        private static void OnAdditiveRectSelection(ShortcutArguments args)
        {
            InvokeRectSelect(args, SelectionType.Additive);
        }

        [ClutchShortcut(RectSelectionSubtractive, typeof(InstancePickingShortcutContext), KeyCode.Mouse0, ShortcutModifiers.Action)]
        private static void OnSubtractiveRectSelection(ShortcutArguments args)
        {
            InvokeRectSelect(args, SelectionType.Subtractive);
        }

        [Shortcut(PickingNormal, typeof(InstancePickingShortcutContext), KeyCode.Mouse0)]
        private static void OnNormalPicking(ShortcutArguments args)
        {
            InvokePicking(args, SelectionType.Normal);
        }

        [Shortcut(PickingAdditive, typeof(InstancePickingShortcutContext), KeyCode.Mouse0, ShortcutModifiers.Shift)]
        private static void OnAdditivePicking(ShortcutArguments args)
        {
            InvokePicking(args, SelectionType.Additive);
        }

        [Shortcut(PickingSubtractive, typeof(InstancePickingShortcutContext), KeyCode.Mouse0, ShortcutModifiers.Action)]
        private static void OnSubtractivePicking(ShortcutArguments args)
        {
            InvokePicking(args, SelectionType.Subtractive);
        }

        #endregion

        private static void InvokeRectSelect(ShortcutArguments a, SelectionType t)
        {
            if (Instance != null && a.context is InstancePickingShortcutContext c)
                Instance.OnRectSelection(a, t, c.Window);
        }

        private static void InvokePicking(ShortcutArguments a, SelectionType t)
        {
            if (Instance != null && a.context is InstancePickingShortcutContext c)
                Instance.DelayPicking(c.Window, t);
        }

        private void OnRectSelection(ShortcutArguments args, SelectionType type, SceneView view)
        {
            bool hotOk = GUIUtility.hotControl == 0 || GUIUtility.hotControl == m_RectSelectionID;
            if (args.stage == ShortcutStage.Begin && hotOk)
            {
                m_CurrentSelectionType = type;
                StartRectSelection(view);
            }
            else if (args.stage == ShortcutStage.End)
            {
                CompleteRectSelection();
            }
        }

        private void DelayPicking(SceneView sv, SelectionType type)
        {
            if (sv == null) return;
            m_CurrentSelectionType = type;
            sv.SendEvent(EditorGUIUtility.CommandEvent(PickingEventCommandName));
        }

        private void Pick(Vector2 mousePos)
        {
            UnityObject picked = HandleUtility.PickGameObject(mousePos, false);
            if (picked is GameObject pickerGameObject && pickerGameObject.TryGetComponent<ScenePickerGameObject>(out var picker))
                picked = picker.Picked;

            if (picked is GameObject pickedGamObject && !pickedGamObject.TryGetComponent<FloraInstanceRenderer>(out _))
                picked = null;

            UpdateSelection(m_SelectionStart, picked, m_CurrentSelectionType, false);
        }

        public void OnGUI(SceneView view)
        {
            Event e = Event.current;
            HandleSelectionCommands(view, e);

            Handles.BeginGUI();
            switch (e.GetTypeForControl(m_RectSelectionID))
            {
                case EventType.Layout:
                case EventType.MouseMove:
                    if (!Tools.viewToolActive)
                        HandleUtility.AddDefaultControl(m_RectSelectionID);
                    break;

                case EventType.MouseDown: HandleOnMouseDown(e); break;
                case EventType.MouseUp:   HandleOnMouseUp();    break;

                case EventType.MouseDrag:
                    if (GUIUtility.hotControl == m_RectSelectionID && m_IsNearestControl)
                    {
                        m_SelectMousePoint = e.mousePosition;
                        UnityObject[] rectObjs = InstanceSelectionUtility.PickRectInstances(
                            view: view,
                            rect: InstanceSelectionUtility.FromToRect(m_StartPoint, m_SelectMousePoint));

                        m_CurrentSelection = rectObjs;
                        m_LastSelection = rectObjs; // no deep compare; equality isn’t critical here
                        UpdateSelection(m_SelectionStart, rectObjs, m_CurrentSelectionType, true);
                        e.Use();
                    }
                    break;

                case EventType.KeyDown when e.keyCode == KeyCode.Escape && GUIUtility.hotControl == m_RectSelectionID:
                    CompleteRectSelection();
                    GUIUtility.hotControl = 0;
                    Selection.objects = m_SelectionStart;
                    HandleOnMouseUp();
                    break;

                case EventType.Repaint:
                    if (GUIUtility.hotControl == m_RectSelectionID && m_IsNearestControl &&
                        m_StartPoint != m_SelectMousePoint)
                    {
                        EditorStyles.selectionRect.Draw(
                            InstanceSelectionUtility.FromToRect(m_StartPoint, m_SelectMousePoint),
                            GUIContent.none, false, false, false, false);
                    }
                    break;

                case EventType.ExecuteCommand:
                    switch (e.commandName)
                    {
                        case PickingEventCommandName when m_IsNearestControl && !InstanceHandles.ViewToolActive:
                            Pick(m_StartPoint);
                            e.Use();
                            break;

                        case SetRectSelectionHotControlEventName:
                            GUIUtility.hotControl = m_RectSelectionID;
                            e.Use();
                            break;
                    }
                    break;
            }

            Handles.EndGUI();
        }

        private void HandleOnMouseDown(Event e)
        {
            if (m_IsNearestControl)
                m_IsNearestControl = false;

            if (GUIUtility.hotControl == 0 && HandleUtility.nearestControl == m_RectSelectionID)
            {
                m_StartPoint        = e.mousePosition;
                m_SelectMousePoint  = m_StartPoint;
                m_IsNearestControl  = true;
                m_LastSelection     = null;
            }

            m_SelectionStart  = Selection.objects;
            m_CurrentSelection = null;
            m_LastSelection    = null;
        }

        private void HandleOnMouseUp()
        {
            if (GUIUtility.hotControl == m_RectSelectionID)
            {
                CompleteRectSelection();
                m_IsNearestControl = false;
                GUIUtility.hotControl = 0;
            }
        }

        private void StartRectSelection(SceneView view)
        {
            view.SendEvent(EditorGUIUtility.CommandEvent(SetRectSelectionHotControlEventName));

            RectSelectionStarting();
            UpdateSelection(m_SelectionStart, m_CurrentSelection, m_CurrentSelectionType, true);
        }

        private void CompleteRectSelection()
        {
            RectSelectionFinished();
        }

        private void UpdateSelection(UnityObject[] existing, UnityObject newObj, SelectionType type, bool rect)
        {
            UpdateSelection(existing, newObj == null ? Array.Empty<UnityObject>() : new[] { newObj }, type, rect);
        }

        private static readonly Dictionary<GameObject, HashSet<int>> GroupHash = new();
        private static readonly HashSet<UnityObject> ObjectHash = new();

        private void UpdateSelection(UnityObject[] existingObjs, UnityObject[] incomingObjs,
                                    SelectionType type, bool isRect)
        {
            if (existingObjs == null || incomingObjs == null) return;

            var existingGroups   = existingObjs.OfType<InstanceSelectionGroup>().ToArray();
            var existingNormals  = existingObjs.Where(o => o is not InstanceSelectionGroup).ToArray();
            var newGroups        = incomingObjs.OfType<InstanceSelectionGroup>().ToArray();
            var newObjects       = incomingObjs.Where(o => o is not InstanceSelectionGroup).ToArray();

            var groupResult = HandleGroups(existingGroups, newGroups, type, isRect);
            var objectResult = HandleObjects(existingNormals, newObjects, type);
            var combined = groupResult.Concat(objectResult);
            Selection.objects = combined.ToArray();
        }

        private static UnityObject[] HandleGroups(InstanceSelectionGroup[] existing, InstanceSelectionGroup[] incoming, SelectionType type, bool isRect)
        {
            if (type == SelectionType.Normal)
                return incoming;

            GroupHash.Clear();

            // seed with existing groups
            foreach (var g in existing)
                AddOrUnion(GroupHash, g, g.SelectionIndices);

            // modify with incoming groups
            if (incoming.Length > 0)
            {
                switch (type)
                {
                    case SelectionType.Additive:
                        foreach (var g in incoming)
                            AddOrUnion(GroupHash, g, g.SelectionIndices);
                        break;

                    case SelectionType.Subtractive:
                        foreach (var g in incoming)
                            if (GroupHash.TryGetValue(g.Target, out var set))
                                set.ExceptWith(g.SelectionIndices);
                        break;
                }
            }

            return GroupHash.Select(p => InstanceSelectionFactory.CreateInstance(p.Key, p.Value.ToArray())).Cast<UnityObject>().ToArray();
        }

        private static UnityObject[] HandleObjects(UnityObject[] existing, UnityObject[] incoming, SelectionType type)
        {
            ObjectHash.Clear();
            foreach (var o in existing)
                ObjectHash.Add(o);

            switch (type)
            {
                case SelectionType.Normal:
                    ObjectHash.Clear();
                    foreach (var o in incoming) ObjectHash.Add(o);
                    break;

                case SelectionType.Additive:
                    foreach (var o in incoming) ObjectHash.Add(o);
                    break;

                case SelectionType.Subtractive:
                    foreach (var o in incoming) ObjectHash.Remove(o);
                    break;
            }

            return ObjectHash.ToArray();
        }

        private static void AddOrUnion(Dictionary<GameObject, HashSet<int>> dict, InstanceSelectionGroup g, int[] indices)
        {
            if (!dict.TryGetValue(g.Target, out var set))
                dict[g.Target] = set = new HashSet<int>();
            set.UnionWith(indices);
        }

        private void HandleSelectionCommands(SceneView view, Event evt)
        {
            if (!HasFloraGroupInSelection())
                return;

            if (evt.type == EventType.ValidateCommand)
            {
                if (evt.commandName is "Delete" or "SoftDelete" or "FrameSelected")
                    evt.Use();
            }
            else if (evt.type == EventType.ExecuteCommand)
            {
                switch (evt.commandName)
                {
                    case "Delete":
                    case "SoftDelete":
                        DeleteSelected();
                        evt.Use();
                        break;

                    case "FrameSelected":
                        FrameSelected(view);
                        evt.Use();
                        break;
                }
            }
        }

        private bool HasFloraGroupInSelection()
        {
            return Selection.GetFiltered<InstanceSelectionGroup>(SelectionMode.Unfiltered).Length > 0;
        }

        private static void FrameSelected(SceneView view)
        {
            var bounds = AxisAlignedBox.Empty;
            foreach (var g in Selection.GetFiltered<InstanceSelectionGroup>(SelectionMode.Unfiltered))
                bounds += g.CalculateBounds();

            foreach (var o in Selection.GetFiltered<UnityObject>(SelectionMode.Unfiltered))
            {
                if (o is GameObject go)
                    bounds += go.CalculateWorldBounds();
                else if (o is Component c)
                    bounds += c.gameObject.CalculateWorldBounds();
            }

            if (!bounds.IsEmpty)
                view.Frame(bounds, false);
        }

        private static void DeleteSelected()
        {
            var groups = Selection.GetFiltered<InstanceSelectionGroup>(SelectionMode.Unfiltered);
            if (groups.Length != 0)
            {
                Undo.RegisterCompleteObjectUndo(groups, "Delete Selected Instances");

                foreach (var g in groups)
                    g.DeleteSelected();
            }

            var objects = Selection.GetFiltered<UnityObject>(SelectionMode.Unfiltered);
            if (objects.Length != 0)
            {
                foreach (var o in objects)
                {
                    if (o is GameObject go)
                        Undo.DestroyObjectImmediate(go);
                    else if (o is Component c)
                        Undo.DestroyObjectImmediate(c.gameObject);
                    else
                        Undo.DestroyObjectImmediate(o);
                }
            }

            Selection.objects = Array.Empty<UnityObject>();
        }
    }
}
