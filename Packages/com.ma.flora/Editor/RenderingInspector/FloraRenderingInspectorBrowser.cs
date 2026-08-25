// Copyright © Magnetic Arcade. All Rights Reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace MA.Flora.Editor
{
    internal sealed class FloraRenderingInspectorBrowser : VisualElement
    {
        public const int RowHeight = 24;
        private const string RowTemplatePath = "Packages/com.ma.flora/Editor/RenderingInspector/UXML/BrowserRow.uxml";
        private const string RowRootClassName = "flora-rendering-inspector__browser-row--root";
        private const string RowChildClassName = "flora-rendering-inspector__browser-row--child";
        private const string RowSectionClassName = "flora-rendering-inspector__browser-row--section";
        private const string RowSelectableClassName = "flora-rendering-inspector__browser-row--selectable";
        private const string RowHasChildrenClassName = "flora-rendering-inspector__browser-row--has-children";
        private const string RowHasThumbnailClassName = "flora-rendering-inspector__browser-row--thumbnail";
        private const string RowGenericIconClassName = "flora-rendering-inspector__browser-row--generic-icon";
        private const string RowWarningClassName = "flora-rendering-inspector__browser-row--warning";
        private const string BrowserIconModifierClassPrefix = "flora-rendering-inspector__browser-icon--";
        private const string RowMetadataClassPrefix = "flora-rendering-inspector__browser-row--metadata-";
        private const string BadgeModifierClassPrefix = "flora-rendering-inspector__browser-badge--";
        private const string BrowserFrameButtonClassName = "flora-rendering-inspector__browser-frame-button";
        private static VisualTreeAsset s_RowTemplate;

        private readonly Action<FloraRenderingInspectorNode> m_OnSelectionChanged;
        private readonly Action<FloraRenderingInspectorTab> m_OnActiveTabChanged;
        private readonly Action<FloraRenderingInspectorSortMode> m_OnSortModeChanged;
        private readonly VisualElement m_TabHeader;
        private readonly VisualElement m_TabList;
        private readonly ToolbarMenu m_SortMenu;
        private readonly Label m_EmptyState;
        private readonly TreeView m_TreeView;
        private readonly HashSet<string> m_ExpandedKeys = new();
        private FloraRenderingInspectorModel m_Model = FloraRenderingInspectorModel.Empty();
        private HashSet<string> m_PreSearchExpandedKeys;
        private string m_TreeSignature = string.Empty;
        private string m_SelectedKey = string.Empty;
        private bool m_HasAppliedInitialExpansionState;
        private FloraRenderingInspectorTab m_ActiveTab;
        private FloraRenderingInspectorSortMode m_SortMode;

        internal FloraRenderingInspectorTab ActiveTab => m_ActiveTab;

        public FloraRenderingInspectorBrowser(
            Action<FloraRenderingInspectorNode> onSelectionChanged,
            FloraRenderingInspectorTab activeTab,
            Action<FloraRenderingInspectorTab> onActiveTabChanged,
            FloraRenderingInspectorSortMode sortMode,
            Action<FloraRenderingInspectorSortMode> onSortModeChanged)
        {
            m_OnSelectionChanged = onSelectionChanged;
            m_OnActiveTabChanged = onActiveTabChanged;
            m_OnSortModeChanged = onSortModeChanged;
            m_ActiveTab = activeTab;
            m_SortMode = sortMode;
            AddToClassList("flora-rendering-inspector__browser-pane");

            m_TabHeader = new VisualElement();
            m_TabHeader.AddToClassList("flora-rendering-inspector__browser-tabs");
            Add(m_TabHeader);

            m_TabList = new VisualElement();
            m_TabList.AddToClassList("flora-rendering-inspector__browser-tab-list");
            m_TabHeader.Add(m_TabList);

            m_SortMenu = new ToolbarMenu { text = string.Empty };
            m_SortMenu.AddToClassList("flora-rendering-inspector__sort-menu");
            m_SortMenu.tooltip = "Sort results";
            m_TabHeader.Add(m_SortMenu);
            BuildSortMenu();

            m_TreeView = new TreeView
            {
                fixedItemHeight = RowHeight,
                selectionType = SelectionType.Single,
                makeItem = MakeItem,
                bindItem = BindItem,
                showAlternatingRowBackgrounds = AlternatingRowBackground.None,
                showBorder = false,
            };
            m_TreeView.AddToClassList("flora-rendering-inspector__browser-tree");
            m_TreeView.selectionChanged += OnSelectionChange;
            Add(m_TreeView);
            m_EmptyState = new Label("No active Flora system.");
            m_EmptyState.AddToClassList("flora-rendering-inspector__empty-state");
            Add(m_EmptyState);
        }

        public void SetActiveTab(FloraRenderingInspectorTab tab)
        {
            if (m_ActiveTab == tab)
                return;

            CaptureExpandedKeys();
            m_ActiveTab = tab;
            m_TreeSignature = string.Empty;
            m_OnActiveTabChanged?.Invoke(tab);
            Refresh(m_Model, m_SelectedKey, !string.IsNullOrWhiteSpace(m_Model.SearchText), true);
        }

        public void SetSortMode(FloraRenderingInspectorSortMode sortMode)
        {
            if (m_SortMode == sortMode)
                return;

            m_SortMode = sortMode;
            m_OnSortModeChanged?.Invoke(sortMode);
        }

        public void Refresh(FloraRenderingInspectorModel model, string selectedKey, bool searching, bool forceRebuild = false)
        {
            var nextModel = model ?? FloraRenderingInspectorModel.Empty();
            m_SortMode = nextModel.SortMode;

            var activeTab = nextModel.GetTab(m_ActiveTab);
            var nextSignature = $"{m_ActiveTab}:{activeTab.StructureSignature}\nsearch:{nextModel.SearchText}";
            var rebuild = forceRebuild || nextSignature != m_TreeSignature;
            if (rebuild)
            {
                var hadTree = !string.IsNullOrEmpty(m_TreeSignature);
                if (hadTree)
                    CaptureExpandedKeys();
                if (searching && m_PreSearchExpandedKeys == null)
                    m_PreSearchExpandedKeys = new HashSet<string>(m_ExpandedKeys);

                m_Model = nextModel;
                RebuildTabHeader(searching);
                m_TreeView.SetRootItems(activeTab.RootItems);
                m_TreeSignature = nextSignature;
                m_TreeView.RefreshItems();

                if (searching)
                    ExpandAll(activeTab.RootNodes);
                else
                {
                    if (m_PreSearchExpandedKeys != null)
                    {
                        m_ExpandedKeys.Clear();
                        foreach (var key in m_PreSearchExpandedKeys)
                            m_ExpandedKeys.Add(key);
                        m_PreSearchExpandedKeys = null;
                    }

                    if (!m_HasAppliedInitialExpansionState)
                    {
                        m_ExpandedKeys.Clear();
                        m_TreeView.CollapseAll();
                        m_HasAppliedInitialExpansionState = true;
                    }
                    else
                        RestoreExpandedKeys();
                }
            }
            else
            {
                m_Model = nextModel;
                RebuildTabHeader(searching);
                m_TreeView.RefreshItems();
            }

            Select(selectedKey);
            UpdateEmptyState(searching, activeTab);
        }

        public void Select(string key)
        {
            var id = m_Model.GetTreeItemId(m_ActiveTab, key);
            if (id > 0)
            {
                m_SelectedKey = key;
                m_TreeView.SetSelectionById(id);
            }
            else
            {
                m_SelectedKey = string.Empty;
                m_TreeView.ClearSelection();
            }
        }

        public bool RevealAndSelect(string key)
        {
            var node = m_Model.FindNode(key);
            if (node == null)
                return false;

            node = m_Model.FindNode(m_ActiveTab, key);
            if (node == null)
            {
                SetActiveTab(FloraRenderingInspectorTab.All);
                node = m_Model.FindNode(m_ActiveTab, key);
            }

            if (node == null)
                return false;

            var ancestors = new List<FloraRenderingInspectorNode>();
            for (var parent = node.Parent; parent != null; parent = parent.Parent)
                ancestors.Add(parent);

            for (var i = ancestors.Count - 1; i >= 0; i--)
            {
                var ancestor = ancestors[i];
                if (ancestor.Id <= 0 || ancestor.Children.Count == 0)
                    continue;

                m_TreeView.ExpandItem(ancestor.Id, false);
                m_ExpandedKeys.Add(ancestor.Key);
            }

            Select(node.Key);
            return true;
        }

        private void ExpandAll(IEnumerable<FloraRenderingInspectorNode> nodes)
        {
            foreach (var node in nodes)
            {
                if (node.Id > 0 && node.Children.Count > 0)
                    m_TreeView.ExpandItem(node.Id, false);

                ExpandAll(node.Children);
            }
        }

        private void CaptureExpandedKeys()
        {
            var captured = new HashSet<string>();
            foreach (var node in m_Model.GetTab(m_ActiveTab).RootNodes)
                CaptureExpandedKey(node, captured);

            m_ExpandedKeys.Clear();
            foreach (var key in captured)
                m_ExpandedKeys.Add(key);
        }

        private void CaptureExpandedKey(FloraRenderingInspectorNode node, HashSet<string> captured)
        {
            if (node.Id > 0 && node.Children.Count > 0 && m_TreeView.IsExpanded(node.Id))
                captured.Add(node.Key);

            foreach (var child in node.Children)
                CaptureExpandedKey(child, captured);
        }

        private void RestoreExpandedKeys()
        {
            foreach (var node in m_Model.GetTab(m_ActiveTab).RootNodes)
                RestoreExpandedKey(node);
        }

        private void RestoreExpandedKey(FloraRenderingInspectorNode node)
        {
            if (node.Id > 0 && node.Children.Count > 0 && m_ExpandedKeys.Contains(node.Key))
                m_TreeView.ExpandItem(node.Id, false);

            foreach (var child in node.Children)
                RestoreExpandedKey(child);
        }

        private void UpdateEmptyState(bool searching, FloraRenderingInspectorTabModel activeTab)
        {
            var hasRows = activeTab.RootItems.Count > 0;
            m_EmptyState.text = searching ? "No rendering information matches the active search." : "No active Flora rendering information.";
            m_EmptyState.style.display = hasRows ? DisplayStyle.None : DisplayStyle.Flex;
        }

        private void RebuildTabHeader(bool searching)
        {
            m_TabList.Clear();

            foreach (var tab in m_Model.Tabs)
            {
                var text = searching && tab.MatchCount > 0 ? $"{tab.Label} {tab.MatchCount:n0}" : tab.Label;
                var label = new Label(text)
                {
                    focusable = true,
                    tooltip = searching ? $"{tab.MatchCount:n0} search matches" : tab.Label,
                };
                label.AddToClassList("flora-rendering-inspector__browser-tab");
                label.EnableInClassList("flora-rendering-inspector__browser-tab--active", tab.Id == m_ActiveTab);
                var tabId = tab.Id;
                label.RegisterCallback<PointerDownEvent>(evt =>
                {
                    if (evt.button != 0)
                        return;

                    SetActiveTab(tabId);
                    evt.StopPropagation();
                });
                label.RegisterCallback<KeyDownEvent>(evt =>
                {
                    if (evt.keyCode != UnityEngine.KeyCode.Return && evt.keyCode != UnityEngine.KeyCode.Space)
                        return;

                    SetActiveTab(tabId);
                    evt.StopPropagation();
                });
                m_TabList.Add(label);
            }
        }

        private void BuildSortMenu()
        {
            m_SortMenu.text = string.Empty;
            AppendSortAction(FloraRenderingInspectorSortMode.Default, "Alphabetical");
            AppendSortAction(FloraRenderingInspectorSortMode.Count, "Count");
        }

        private void AppendSortAction(FloraRenderingInspectorSortMode mode, string label)
        {
            m_SortMenu.menu.AppendAction(
                label,
                _ => SetSortMode(mode),
                _ => m_SortMode == mode ? DropdownMenuAction.Status.Checked : DropdownMenuAction.Status.Normal);
        }

        private VisualElement MakeItem()
            => MakeRow();

        private static VisualElement MakeRow()
        {
            s_RowTemplate ??= AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(RowTemplatePath);
            var container = s_RowTemplate.CloneTree();
            var row = container.Q(className: "flora-rendering-inspector__browser-row");
            row.RemoveFromHierarchy();
            InitializeFrameButton(row);
            return row;
        }

        private void BindItem(VisualElement element, int index)
        {
            var treeNode = m_TreeView.GetItemDataForIndex<FloraRenderingInspectorNode>(index);
            var node = m_Model.FindNode(treeNode.Key) ?? treeNode;
            BindRow(element, node);
            element.UnregisterCallback<ClickEvent>(OnRowClicked);
            element.RegisterCallback<ClickEvent>(OnRowClicked);
        }

        private static void BindRow(VisualElement element, FloraRenderingInspectorNode node)
        {
            var isRoot = node.Kind == FloraRenderingInspectorNodeKind.Root;
            var isSelectable = node.IsSelectable;
            var hasChildren = node.Children.Count > 0;
            var hasWarning = node.HasWarning;

            element.userData = node;
            element.tooltip = BuildTooltip(node);
            element.EnableInClassList(RowRootClassName, isRoot);
            element.EnableInClassList(RowChildClassName, !isRoot);
            element.EnableInClassList(RowSectionClassName, node.IsSectionHeader);
            element.EnableInClassList(RowSelectableClassName, isSelectable);
            element.EnableInClassList(RowHasChildrenClassName, hasChildren);
            element.EnableInClassList(RowWarningClassName, hasWarning);
            RemoveClassPrefix(element, RowMetadataClassPrefix);
            if (!string.IsNullOrEmpty(node.RowStyleClass))
                element.AddToClassList(node.RowStyleClass);

            var icon = element.Q(className: "flora-rendering-inspector__browser-icon");
            RemoveClassPrefix(icon, BrowserIconModifierClassPrefix);
            var usedThumbnail = FloraRenderingInspectorIcons.TryGetThumbnail(node, EditorIconSize.Regular, out var thumbnail);
            if (usedThumbnail)
                icon.style.backgroundImage = thumbnail;
            else
            {
                icon.style.backgroundImage = StyleKeyword.Null;
                icon.AddToClassList($"{BrowserIconModifierClassPrefix}{FloraRenderingInspectorIcons.GetStyleClass(node)}");
            }

            element.EnableInClassList(RowHasThumbnailClassName, usedThumbnail);
            element.EnableInClassList(RowGenericIconClassName, !usedThumbnail);
            icon.tooltip = usedThumbnail ? "Object thumbnail" : string.Empty;

            var name = element.Q<Label>(className: "flora-rendering-inspector__browser-name");
            name.text = node.Name;

            var badge = element.Q<Label>(className: "flora-rendering-inspector__browser-badge");
            var showBadge = (!isRoot || node.ShowBadgeOnRoot) && !string.IsNullOrEmpty(node.BadgeText);
            RemoveClassPrefix(badge, BadgeModifierClassPrefix);
            badge.text = showBadge ? node.BadgeText : string.Empty;
            badge.style.display = showBadge ? DisplayStyle.Flex : DisplayStyle.None;
            if (showBadge && !string.IsNullOrEmpty(node.BadgeStyleClass))
                badge.AddToClassList(node.BadgeStyleClass);

            var count = element.Q<Label>(className: "flora-rendering-inspector__browser-count");
            count.text = node.CountText;

            var frameButton = element.Q<Button>(className: BrowserFrameButtonClassName);
            var canFrame = node.FrameBounds.HasValue;
            frameButton.userData = canFrame ? node : null;
            frameButton.visible = canFrame;
            frameButton.SetEnabled(canFrame);
            frameButton.tooltip = canFrame ? "Frame grid bounds in Scene view" : string.Empty;

            var warning = element.Q(className: "flora-rendering-inspector__browser-warning");
            warning.style.display = hasWarning ? DisplayStyle.Flex : DisplayStyle.None;
            warning.tooltip = node.Warning;
        }

        private static void InitializeFrameButton(VisualElement row)
        {
            var frameButton = row.Q<Button>(className: BrowserFrameButtonClassName);
            frameButton.RegisterCallback<ClickEvent>(OnFrameButtonClicked);
            frameButton.visible = false;
            frameButton.SetEnabled(false);

            var label = frameButton.Q<Label>(className: "flora-rendering-inspector__icon-button-label");
            label.text = string.Empty;
            label.style.display = DisplayStyle.None;
        }

        private static void OnFrameButtonClicked(ClickEvent evt)
        {
            if (evt.currentTarget is not Button button || button.userData is not FloraRenderingInspectorNode node || !node.FrameBounds.HasValue)
                return;

            FloraRenderingInspectorElements.TryFrameBounds(node.FrameBounds.Value);
            evt.StopPropagation();
        }

        private static void RemoveClassPrefix(VisualElement element, string prefix)
        {
            foreach (var className in element.GetClasses().Where(className => className.StartsWith(prefix, StringComparison.Ordinal)).ToArray())
                element.RemoveFromClassList(className);
        }

        private static string BuildTooltip(FloraRenderingInspectorNode node)
        {
            if (node == null)
                return string.Empty;

            var parts = new List<string>();
            AddTooltipPart(parts, node.Tooltip);
            AddTooltipPart(parts, node.Subtitle);
            AddTooltipPart(parts, node.Warning);
            return string.Join("\n", parts);
        }

        private static void AddTooltipPart(List<string> parts, string value)
        {
            if (string.IsNullOrWhiteSpace(value) || parts.Contains(value))
                return;

            parts.Add(value);
        }

        private void OnRowClicked(ClickEvent evt)
        {
            if (evt.currentTarget is not VisualElement row || row.userData is not FloraRenderingInspectorNode node)
                return;

            if (!ToggleNode(node, true))
                return;

            evt.StopPropagation();
        }

        private bool ToggleNode(FloraRenderingInspectorNode node, bool selectIfSelectable)
        {
            if (node == null || node.Children.Count == 0 || node.Id <= 0)
                return false;

            if (m_TreeView.IsExpanded(node.Id))
            {
                m_TreeView.CollapseItem(node.Id, false);
                m_ExpandedKeys.Remove(node.Key);
            }
            else
            {
                m_TreeView.ExpandItem(node.Id, false);
                m_ExpandedKeys.Add(node.Key);
            }

            if (selectIfSelectable && node.IsSelectable)
            {
                Select(node.Key);
                m_OnSelectionChanged?.Invoke(node);
            }

            return true;
        }

        private void OnSelectionChange(IEnumerable<object> selectedItems)
        {
            var node = selectedItems.OfType<FloraRenderingInspectorNode>().FirstOrDefault();
            if (node == null || !node.IsSelectable)
                return;

            m_SelectedKey = node.Key;
            m_OnSelectionChanged?.Invoke(node);
        }
    }
}
