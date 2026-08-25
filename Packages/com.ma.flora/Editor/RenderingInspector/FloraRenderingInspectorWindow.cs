// Copyright © Magnetic Arcade. All Rights Reserved.

using System;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace MA.Flora.Editor
{
    internal sealed class FloraRenderingInspectorWindow : EditorWindow
    {
        private const string WindowTitle = "Flora Rendering Inspector";
        private const string SummaryTooltip = "Show rendering summary";
        private const string RefreshTooltip = "Refresh Flora rendering information";
        private const string SearchTooltip = "Search rendering structure";
        private const string WindowTooltip = "Live inspector for Flora sources, templates, draws, batch domains, graphics buffers, and culling.";
        private const string WindowTemplatePath = "Packages/com.ma.flora/Editor/RenderingInspector/UXML/Window.uxml";

        private const float DefaultSplitDimension = 460f;
        private const float MinSplitDimension = 320f;
        private const float MaxSplitDimension = 760f;
        private const long SearchDebounceDelayMs = 150;

        private static readonly string[] s_StyleSheetPaths =
        {
            "Packages/com.ma.flora/Editor/RenderingInspector/FloraRenderingInspectorWindow.uss",
            "Packages/com.ma.flora/Editor/RenderingInspector/USS/Browser.uss",
            "Packages/com.ma.flora/Editor/RenderingInspector/USS/Section.uss",
        };
        private static VisualTreeAsset s_WindowTemplate;

        [SerializeField] private string m_SearchText = string.Empty;
        [SerializeField] private float m_SplitDimension = DefaultSplitDimension;
        [SerializeField] private string m_SelectedNodeKey = string.Empty;
        [SerializeField] private FloraRenderingInspectorTab m_ActiveTab = FloraRenderingInspectorTab.All;
        [SerializeField] private FloraRenderingInspectorSortMode m_SortMode = FloraRenderingInspectorSortMode.Default;

        private FloraRenderingInspectorModel m_Model = FloraRenderingInspectorModel.Empty();
        private FloraRenderingInspectorNode m_SelectedNode;
        private FloraRenderingInspectorBrowser m_Browser;
        private FloraRenderingInspectorDetails m_Details;
        private TextField m_SearchField;
        private int m_SearchRevision;
        private string m_PendingSearchText = string.Empty;

        public static void OpenWindow()
        {
            var window = GetWindow<FloraRenderingInspectorWindow>();
            window.titleContent = new GUIContent(WindowTitle, EditorIcons.Get("summary", EditorIconSize.Large), WindowTooltip);
            window.minSize = new Vector2(760, 420);
            window.Show();
        }

        public void CreateGUI()
        {
            rootVisualElement.Clear();

            EditorElements.AddSharedStyleSheet(rootVisualElement);
            foreach (var styleSheetPath in s_StyleSheetPaths)
            {
                var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(styleSheetPath);
                if (styleSheet != null)
                    rootVisualElement.styleSheets.Add(styleSheet);
            }

            rootVisualElement.AddToClassList("flora-rendering-inspector");
            titleContent = new GUIContent(WindowTitle, EditorIcons.Get("summary", EditorIconSize.Large), WindowTooltip);
            BuildToolbar();
            BuildContent();

            RefreshSnapshot();
        }

        private void BuildToolbar()
        {
            var toolbar = CloneWindowToolbar();
            rootVisualElement.Add(toolbar);

            var summaryButton = toolbar.Q<Button>("SummaryButton");
            summaryButton.clicked += ClearSelection;
            summaryButton.tooltip = SummaryTooltip;

            var refreshButton = toolbar.Q<Button>("RefreshButton");
            refreshButton.clicked += RefreshSnapshotAndShowSummary;
            refreshButton.tooltip = RefreshTooltip;

            var searchContainer = toolbar.Q<VisualElement>("SearchContainer");
            searchContainer.RegisterCallback<FocusInEvent>(_ => searchContainer.AddToClassList("flora-rendering-inspector__search-container--focused"));
            searchContainer.RegisterCallback<FocusOutEvent>(_ => searchContainer.RemoveFromClassList("flora-rendering-inspector__search-container--focused"));

            m_SearchField = toolbar.Q<TextField>("SearchField");
            m_SearchField.value = m_SearchText ?? string.Empty;
            m_SearchField.tooltip = SearchTooltip;
            m_SearchField.RegisterValueChangedCallback(evt => SetSearchText(evt.newValue ?? string.Empty));

            var optionsMenu = toolbar.Q<ToolbarMenu>("OptionsMenu");
            optionsMenu.tooltip = "Rendering inspector options";
            optionsMenu.menu.AppendAction("Refresh", _ => RefreshSnapshotAndShowSummary());
            optionsMenu.menu.AppendAction("Clear Search", _ => SetSearchText(string.Empty));
            optionsMenu.menu.AppendAction("Reset Layout", _ =>
            {
                m_SplitDimension = DefaultSplitDimension;
                CreateGUI();
            });
        }

        private static VisualElement CloneWindowToolbar()
        {
            s_WindowTemplate ??= AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(WindowTemplatePath);
            var container = s_WindowTemplate.CloneTree();
            var toolbar = container.Q<VisualElement>("Toolbar");
            toolbar.RemoveFromHierarchy();
            return toolbar;
        }

        private void BuildContent()
        {
            m_SplitDimension = ClampSplitDimension(m_SplitDimension <= 0 ? DefaultSplitDimension : m_SplitDimension);
            var splitView = new TwoPaneSplitView(0, m_SplitDimension, TwoPaneSplitViewOrientation.Horizontal);
            splitView.AddToClassList("flora-rendering-inspector__split-view");
            rootVisualElement.Add(splitView);

            m_Browser = new FloraRenderingInspectorBrowser(OnBrowserSelectionChanged, m_ActiveTab, OnBrowserTabChanged, m_SortMode, OnBrowserSortModeChanged);
            m_Browser.RegisterCallback<GeometryChangedEvent>(evt =>
            {
                if (evt.newRect.width > 0)
                    m_SplitDimension = ClampSplitDimension(evt.newRect.width);
            });
            splitView.Add(m_Browser);

            m_Details = new FloraRenderingInspectorDetails(OnRelationshipSelected);
            splitView.Add(m_Details);
        }

        private void OnBrowserTabChanged(FloraRenderingInspectorTab tab)
        {
            if (m_ActiveTab == tab)
                return;

            m_ActiveTab = tab;
            if (ShouldCaptureCullingGrid() && m_Model.Snapshot?.HasCullingGridDetails != true)
                RefreshSnapshot(true);
        }

        private void OnBrowserSortModeChanged(FloraRenderingInspectorSortMode sortMode)
        {
            if (m_SortMode == sortMode)
                return;

            m_SortMode = sortMode;
            m_Model = FloraRenderingInspectorModel.Build(m_Model.Snapshot, m_SearchText, m_SortMode);
            RestoreSelection();
            RefreshViews(true);
        }

        private void RefreshSnapshot() => RefreshSnapshot(true);

        private void RefreshSnapshot(bool forceVisualRefresh)
        {
            var snapshot = FloraDiagnostics.CaptureSnapshot(GetCaptureFlags());
            ApplySnapshot(snapshot, forceVisualRefresh);
        }

        private void RefreshSnapshotAndShowSummary()
        {
            RefreshSnapshot(true);
            ClearSelection();
        }

        private void ApplySnapshot(FloraDiagnosticsSnapshot snapshot, bool forceBrowserRebuild)
        {
            m_Model = FloraRenderingInspectorModel.Build(snapshot, m_SearchText, m_SortMode);
            UpdateStatus(snapshot);
            RestoreSelection();
            RefreshViews(forceBrowserRebuild);
        }

        private FloraDiagnosticsCaptureFlags GetCaptureFlags()
            => ShouldCaptureCullingGrid() ? FloraDiagnosticsCaptureFlags.IncludeCullingGrid : FloraDiagnosticsCaptureFlags.Default;

        private bool ShouldCaptureCullingGrid()
            => m_ActiveTab == FloraRenderingInspectorTab.Grid || m_SelectedNode?.Kind == FloraRenderingInspectorNodeKind.CullingChunk;

        private void UpdateStatus(FloraDiagnosticsSnapshot snapshot)
        {
            if (snapshot == null || !snapshot.IsSystemCreated)
            {
                rootVisualElement.tooltip = "No active Flora rendering information.";
                return;
            }

            rootVisualElement.tooltip = snapshot.IsRenderingEnabled ? "Flora rendering is enabled." : "Flora system is created; rendering is disabled.";
        }

        private void RestoreSelection()
        {
            var selected = string.IsNullOrEmpty(m_SelectedNodeKey) ? null : m_Model.FindNode(m_SelectedNodeKey);

            m_SelectedNode = selected;
            m_SelectedNodeKey = selected?.Key ?? string.Empty;
        }

        private void RefreshViews(bool forceBrowserRebuild = false)
        {
            var searching = !string.IsNullOrWhiteSpace(m_SearchText);
            m_Browser?.Refresh(m_Model, m_SelectedNodeKey, searching, forceBrowserRebuild);
            m_Details?.Refresh(m_Model, m_SelectedNode);
        }

        private void OnBrowserSelectionChanged(FloraRenderingInspectorNode node)
        {
            if (node == null || !node.IsSelectable)
                return;

            m_SelectedNode = node;
            m_SelectedNodeKey = node.Key;
            m_Details?.Refresh(m_Model, m_SelectedNode);
        }

        private void ClearSelection()
        {
            m_SelectedNode = null;
            m_SelectedNodeKey = string.Empty;
            m_Browser?.Select(string.Empty);
            m_Details?.Refresh(m_Model, null);
        }

        private void OnRelationshipSelected(FloraRenderingInspectorNode node)
        {
            var modelNode = m_Model.FindCanonicalNode(node);
            if (modelNode == null)
                return;

            if (m_Browser != null && m_Browser.RevealAndSelect(modelNode.Key))
            {
                m_ActiveTab = m_Browser.ActiveTab;
                m_SelectedNode = modelNode;
                m_SelectedNodeKey = modelNode.Key;
                m_Details?.Refresh(m_Model, m_SelectedNode);
                return;
            }

            OnBrowserSelectionChanged(modelNode);
        }

        private void SetSearchText(string value)
        {
            var text = value ?? string.Empty;
            if (m_SearchText == text && m_PendingSearchText == text)
            {
                if (m_SearchField != null && m_SearchField.value != text)
                    m_SearchField.SetValueWithoutNotify(text);
                return;
            }

            m_PendingSearchText = text;
            if (m_SearchField != null && m_SearchField.value != text)
                m_SearchField.SetValueWithoutNotify(text);

            var revision = ++m_SearchRevision;
            if (m_SearchField == null)
            {
                ApplyPendingSearch(revision);
                return;
            }

            m_SearchField.schedule.Execute(() => ApplyPendingSearch(revision)).StartingIn(SearchDebounceDelayMs);
        }

        private void ApplyPendingSearch(int revision)
        {
            if (revision != m_SearchRevision)
                return;

            ApplySearchText(m_PendingSearchText);
        }

        private void ApplySearchText(string value)
        {
            var text = value ?? string.Empty;
            if (m_SearchText == text)
                return;

            var previousSelectedNodeKey = m_SelectedNodeKey;
            m_SearchText = text;
            m_Model.ApplySearch(m_SearchText);
            RestoreSelection();

            var searching = !string.IsNullOrWhiteSpace(m_SearchText);
            m_Browser?.Refresh(m_Model, m_SelectedNodeKey, searching, true);
            if (m_SelectedNodeKey != previousSelectedNodeKey)
                m_Details?.Refresh(m_Model, m_SelectedNode);
        }

        private static float ClampSplitDimension(float value) => Mathf.Clamp(value, MinSplitDimension, MaxSplitDimension);
    }
}
