// Copyright © Magnetic Arcade. All Rights Reserved.

using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Inspector.GraphicsSettingsInspectors;
using UnityEditor.Rendering;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UIElements;

#if HAS_PACKAGE_UNITY_URP
using UnityEngine.Rendering.Universal;
#endif

namespace MA.Flora.Editor
{
    [CustomEditor(typeof(FloraSceneSettings))]
    internal class FloraSceneSettingsInspector : UnityEditor.Editor
    {
        private const string DefaultFloraGraphicsSettingsPropertyName = "m_DisableGPUOcclusionCulling";
        private const string SceneStyleSheetPath = "Packages/com.ma.flora/Editor/SceneSettingsInspector/FloraSceneSettingsInspector.uss";

        private enum SceneSettingsTab
        {
            Global = 0,
            Stats = 1,
        }

        [MenuItem("GameObject/Flora/Scene Settings", false, 10)]
        private static void CreateSceneManagerCommand()
        {
            var sceneManager = FindAnyObjectByType<FloraSceneSettings>();
            if (sceneManager == null)
            {
                var go = new GameObject("Flora Scene Settings");
                go.AddComponent<FloraSceneSettings>();
                Selection.activeGameObject = go;
            }
            else
            {
                Selection.activeGameObject = sceneManager.gameObject;
            }
        }

        private static class Styles
        {
            public static readonly GUIContent BRGShaderStrippingErrorMessage = L10n.TextContent("\"BatchRendererGroup Variants\" setting must be \"Keep All\". To fix, modify Graphics settings and set \"BatchRendererGroup Variants\" to \"Keep All\".");
            public static readonly GUIContent BRGShaderStrippingFixButton = L10n.TextContent("Fix", "Open Project Settings > Graphics and highlight BatchRendererGroup Variants.");

            public static readonly GUIContent RenderingDisabledInfo = L10n.TextContent("Flora rendering is disabled. Flora instances will not be visible.");
            public static readonly GUIContent GPUOcclusionDisabledInfo = L10n.TextContent("GPU Occlusion Culling is disabled in Flora Runtime Settings.");
            public static readonly GUIContent PerObjectMotionVectorsDisabledInfo = L10n.TextContent("Per-Object Motion Vectors are disabled in Flora Runtime Settings.");
            public static readonly GUIContent LegacyLightProbesDisabledInfo = L10n.TextContent("Legacy Light Probes are disabled in Flora Runtime Settings.");
            public static readonly GUIContent TerrainFoliageDisabledInfo = L10n.TextContent("Terrain foliage rendering is disabled. Flora will not render trees and details on registered terrains.");

            public static readonly GUIContent AddVolumeButton = L10n.TextContent("Add Render Settings Volume", "Add a global Volume with Flora Render Settings and Flora Density Settings components.");
            public static readonly GUIContent OpenRenderingInspectorButton = L10n.TextContent("Rendering Inspector", "Open the Flora Rendering Inspector window.");
            public static readonly GUIContent OpenRenderingDebuggerButton = L10n.TextContent("Rendering Debugger", "Open the Flora Rendering Debugger window for debugging rendering features.");
            public static readonly GUIContent OpenFloraGraphicsSettingsButton = L10n.TextContent("Flora Graphics Settings", "Open Project Settings > Graphics for project-wide Flora Runtime Settings.");
            public static readonly GUIContent OpenRuntimeSettingsButton = L10n.TextContent("Project Settings", "Open Project Settings > Graphics for project-wide Flora Runtime Settings.");

            public static readonly GUIContent AllowGPUOcclusionCullingLabel = L10n.TextContent("Allow GPU Occlusion Culling", "Enable or disable GPU occlusion culling for instances.");
            public static readonly GUIContent AllowDensityCullingLabel = L10n.TextContent("Allow Density Culling", "Enable or disable density-based culling for instances.");
            public static readonly GUIContent AllowPerObjectMotionVectorsLabel = L10n.TextContent("Allow Per-Object Motion Vectors", "Enable or disable per-object motion vectors for instances.");
            public static readonly GUIContent AllowLegacyLightProbesLabel = L10n.TextContent("Allow Legacy Light Probes", "Enable or disable legacy light probe data for instances.");

            public static readonly GUIContent FeatureFlagsSection = L10n.TextContent("Feature Flags", "Scene-wide Flora feature switches and runtime feature permissions.");
            public static readonly GUIContent TerrainFoliageSection = L10n.TextContent("Terrain Foliage", "Controls Flora terrain tree and detail rendering for registered terrains.");
            public static readonly GUIContent CPUCullingStatsSection = L10n.TextContent("CPU Culling Views", "Live CPU culling statistics by view.");
            public static readonly GUIContent GPUCullingStatsSection = L10n.TextContent("GPU Culling Views", "Live GPU culling statistics by view.");
            public static readonly GUIContent GraphicsBuffersStatsSection = L10n.TextContent("Graphics Buffers", "Live Flora graphics buffer allocation statistics.");

            public static readonly GUIContent AutoRegisterTerrainsLabel = L10n.TextContent("Auto Register Terrains", "Automatically register all active terrains in the scene.");
            public static readonly GUIContent AllowTreeMotionVectorsLabel = L10n.TextContent("Tree Motion Vectors", "Enable or disable per-instance motion vectors for terrain trees.");
            public static readonly GUIContent AllowTreeLightProbesLabel = L10n.TextContent("Tree Light Probes", "Enable or disable per-instance light probes for terrain trees.");
            public static readonly GUIContent AllowDetailMotionVectorsLabel = L10n.TextContent("Detail Motion Vectors", "Enable or disable per-instance motion vectors for terrain details.");
            public static readonly GUIContent AllowDetailLightProbesLabel = L10n.TextContent("Detail Light Probes", "Enable or disable per-instance light probes for terrain details.");
            public static readonly GUIContent DetailStreamingModeLabel = L10n.TextContent("Streaming Mode", "Immediate removes the per-frame limits. Streamed is the default mode and uses the responsiveness slider. Custom exposes Flora's internal budgets directly.");
            public static readonly GUIContent DetailStreamingResponsivenessLabel = L10n.TextContent("Streaming Responsiveness", "Higher values catch visible details up faster. Lower values spread work across more frames to reduce spikes. Ignored in Immediate and Custom modes.");
            public static readonly GUIContent CustomDetailPatchLayerBudgetLabel = L10n.TextContent("Patch-Layer Budget", "Advanced: how many patch-layer rebuilds Flora may schedule per frame. 0 means unbounded.");
            public static readonly GUIContent CustomDetailStructuralBudgetLabel = L10n.TextContent("Structural Instance Budget", "Advanced: how many detail create/destroy instance operations Flora may apply per frame. 0 means unbounded.");
            public static readonly GUIContent DetailUnloadHysteresisLabel = L10n.TextContent("Unload Grace Period", "How many seconds Flora keeps details alive after they leave range before unloading them. This only affects unloading and helps prevent rapid disappear/reappear popping near the terrain detail distance.");
            public static readonly GUIContent StreamedDetailBudgetInfo = L10n.TextContent(string.Empty, "Streaming responsiveness adjusts how much terrain detail work Flora may process each frame.");
        }

        private SerializedProperty m_EnableRendering;
        private SerializedProperty m_AllowDensityCulling;
        private SerializedProperty m_AllowGPUOcclusionCulling;
        private SerializedProperty m_AllowPerObjectMotionVectors;
        private SerializedProperty m_AllowLegacyLightProbes;

        private SerializedProperty m_EnableTerrainFoliage;
        private SerializedProperty m_AutoRegisterTerrains;
        private SerializedProperty m_AllowPerTreeMotionVectors;
        private SerializedProperty m_AllowPerTreeLightProbes;
        private SerializedProperty m_AllowPerDetailMotionVectors;
        private SerializedProperty m_AllowPerDetailLightProbes;
        private SerializedProperty m_DetailStreamingMode;
        private SerializedProperty m_DetailStreamingResponsiveness;
        private SerializedProperty m_CustomDetailPatchLayerBudgetPerFrame;
        private SerializedProperty m_CustomDetailStructuralInstanceBudgetPerFrame;
        private SerializedProperty m_DetailUnloadHysteresisSeconds;

        private bool m_GPUOcclusionCullingAvailable = true;
        private bool m_LegacyLightProbesAvailable = true;
        private bool m_PerObjectMotionVectorsAvailable = true;
#if HAS_PACKAGE_UNITY_URP
        private bool m_UniversalRenderGraphEnabled = true;
#endif

        private SceneSettingsTab m_ActiveTab = SceneSettingsTab.Global;
        private VisualElement m_Root;
        private VisualElement m_Warnings;
        private EditorTabView m_TabView;
        private VisualElement m_TerrainTabContent;
        private VisualElement m_SettingsContent;
        private Button m_RenderingStatusChip;
        private Button m_FoliageStatusChip;
        private Button m_AutoRegisterTerrainsButton;
        private VisualElement m_StatsContent;
        private VisualElement m_StatsInactive;
        private VisualElement m_DetailStreamingCluster;
        private VisualElement m_StreamedDetailBudgetContainer;
        private VisualElement m_CustomDetailBudgetContainer;
        private VisualElement m_ImmediateDetailBudgetContainer;
        private Label m_StreamedDetailLayerBudgetLabel;
        private Label m_StreamedDetailInstanceBudgetLabel;
        private Label m_CpuStatsSummary;
        private Label m_GpuStatsSummary;
        private Label m_BufferStatsSummary;
        private VisualElement m_CpuStatsTable;
        private VisualElement m_GpuStatsTable;
        private VisualElement m_BufferStatsTable;
        private IVisualElementScheduledItem m_StatsRefreshItem;
        private IVisualElementScheduledItem m_ExternalSettingsRefreshItem;
        private BatchRendererGroupStrippingMode m_LastBRGShaderStrippingMode;
        private bool m_LastGPUOcclusionCullingAvailable;
        private bool m_LastLegacyLightProbesAvailable;
        private bool m_LastPerObjectMotionVectorsAvailable;
#if HAS_PACKAGE_UNITY_URP
        private bool m_LastUniversalRenderGraphEnabled;
#endif

        private PropertyField m_AllowDensityCullingField;
        private PropertyField m_AllowGPUOcclusionCullingField;
        private PropertyField m_AllowPerObjectMotionVectorsField;
        private PropertyField m_AllowLegacyLightProbesField;
        private PropertyField m_AllowPerTreeMotionVectorsField;
        private PropertyField m_AllowPerTreeLightProbesField;
        private PropertyField m_AllowPerDetailMotionVectorsField;
        private PropertyField m_AllowPerDetailLightProbesField;
        private PropertyField m_DetailStreamingResponsivenessField;
        private Button m_GPUOcclusionRuntimeSettingsButton;
        private Button m_PerObjectMotionVectorsRuntimeSettingsButton;
        private Button m_LegacyLightProbesRuntimeSettingsButton;

        internal int ActiveTabForTests => (int)m_ActiveTab;
        internal bool RootCreatedForTests => m_Root != null;
        internal bool ExternalSettingsRefreshScheduledForTests => m_ExternalSettingsRefreshItem != null;

        private void OnEnable()
        {
            m_EnableRendering = serializedObject.FindProperty("EnableRendering");
            m_AllowDensityCulling = serializedObject.FindProperty("AllowDensityCulling");
            m_AllowGPUOcclusionCulling = serializedObject.FindProperty("AllowGPUOcclusionCulling");
            m_AllowPerObjectMotionVectors = serializedObject.FindProperty("AllowPerObjectMotionVectors");
            m_AllowLegacyLightProbes = serializedObject.FindProperty("AllowLegacyLightProbes");

            m_EnableTerrainFoliage = serializedObject.FindProperty("EnableTerrainFoliage");
            m_AutoRegisterTerrains = serializedObject.FindProperty("AutoRegisterTerrains");
            m_AllowPerTreeMotionVectors = serializedObject.FindProperty("AllowPerTreeMotionVectors");
            m_AllowPerTreeLightProbes = serializedObject.FindProperty("AllowPerTreeLightProbes");
            m_AllowPerDetailMotionVectors = serializedObject.FindProperty("AllowPerDetailMotionVectors");
            m_AllowPerDetailLightProbes = serializedObject.FindProperty("AllowPerDetailLightProbes");
            m_DetailStreamingMode = serializedObject.FindProperty("DetailStreamingMode");
            m_DetailStreamingResponsiveness = serializedObject.FindProperty("DetailStreamingResponsiveness");
            m_CustomDetailPatchLayerBudgetPerFrame = serializedObject.FindProperty("CustomDetailPatchLayerBudgetPerFrame");
            m_CustomDetailStructuralInstanceBudgetPerFrame = serializedObject.FindProperty("CustomDetailStructuralInstanceBudgetPerFrame");
            m_DetailUnloadHysteresisSeconds = serializedObject.FindProperty("DetailUnloadHysteresisSeconds");

            RefreshRuntimeAvailability();
            SnapshotExternalSettingsState();
        }

        private void OnDisable()
        {
            m_StatsRefreshItem?.Pause();
            m_ExternalSettingsRefreshItem?.Pause();
            SetCullingStatsEnabled(false);
        }

        public override bool RequiresConstantRepaint()
            => m_ActiveTab == SceneSettingsTab.Stats && m_EnableRendering?.boolValue == true;

        public override VisualElement CreateInspectorGUI()
        {
            serializedObject.Update();
            RefreshRuntimeAvailability();

            m_Root = new VisualElement();
            m_Root.AddToClassList("flora-scene-settings");
            m_Root.AddToClassList("flora-editor-surface");
            EditorElements.AddSharedStyleSheet(m_Root);
            EditorElements.AddStyleSheet(m_Root, SceneStyleSheetPath);

            BuildActionRow(m_Root);
            m_Warnings = new VisualElement();
            m_Warnings.AddToClassList("flora-scene-settings__warnings");
            m_Root.Add(m_Warnings);

            m_TabView = new EditorTabView();
            m_TabView.AddTab("Global", "Scene-wide Flora rendering settings", BuildGlobalTab());
            m_TabView.AddTab("Stats", "Live Flora culling and graphics buffer stats", BuildStatsTab());
            m_TabView.RegisterValueChangedCallback(evt =>
            {
                m_ActiveTab = (SceneSettingsTab)evt.newValue;
                UpdateStatsCollectionState();
                RefreshStats();
            });
            m_TabView.SetValueWithoutNotify((int)m_ActiveTab);
            m_Root.Add(m_TabView);

            RegisterStateTracking();
            m_Root.Bind(serializedObject);
            RefreshState();

            m_StatsRefreshItem = m_Root.schedule.Execute(RefreshStats).Every(500);
            m_ExternalSettingsRefreshItem = m_Root.schedule.Execute(RefreshExternalSettingsState).Every(500);
            UpdateStatsCollectionState();
            return m_Root;
        }

        private void BuildActionRow(VisualElement root)
        {
            var row = new VisualElement();
            row.AddToClassList("flora-editor-action-row");
            root.Add(row);

            var statusChips = new VisualElement();
            statusChips.AddToClassList("flora-editor-action-row__left");
            statusChips.AddToClassList("flora-scene-settings__status-chips");
            row.Add(statusChips);

            m_RenderingStatusChip = AddStatusChip(statusChips, "Rendering", "flora-scene-settings__status-chip--rendering");
            m_FoliageStatusChip = AddStatusChip(statusChips, "Foliage", "flora-scene-settings__status-chip--foliage");

            var utilityButtons = new VisualElement();
            utilityButtons.AddToClassList("flora-editor-action-row__right");
            row.Add(utilityButtons);

            EditorElements.AddIconButton(utilityButtons, Styles.OpenRenderingInspectorButton.tooltip, FloraMenuItems.OpenRenderingInspector, "summary");
            EditorElements.AddIconButton(utilityButtons, Styles.OpenRenderingDebuggerButton.tooltip, FloraMenuItems.OpenRenderingDebugger, "culling");
            var graphicsSettingsButton = EditorElements.AddIconButton(utilityButtons, Styles.OpenFloraGraphicsSettingsButton.tooltip, OpenFloraGraphicsSettings, "settings");
            graphicsSettingsButton.AddToClassList("flora-scene-settings__graphics-settings-button");
            graphicsSettingsButton.userData = (System.Action)OpenFloraGraphicsSettings;

            var sceneSettings = (FloraSceneSettings)target;
            if (!sceneSettings.TryGetComponent(out Volume _))
                EditorElements.AddIconButton(utilityButtons, Styles.AddVolumeButton.tooltip, AddGlobalRenderSettingsVolume, "add");
        }

        private VisualElement BuildGlobalTab()
        {
            var page = new VisualElement();
            page.AddToClassList("flora-editor-tab-page");

            m_SettingsContent = new VisualElement();
            m_SettingsContent.AddToClassList("flora-scene-settings__settings-content");
            page.Add(m_SettingsContent);

            var flags = new EditorSection(Styles.FeatureFlagsSection.text, iconClass: "property");
            ApplyTooltip(flags, Styles.FeatureFlagsSection.tooltip);
            m_SettingsContent.Add(flags);
            m_AllowDensityCullingField = AddPropertyField(flags.contentContainer, m_AllowDensityCulling, Styles.AllowDensityCullingLabel);
            m_AllowGPUOcclusionCullingField = AddRuntimeSettingsPropertyField(flags.contentContainer, m_AllowGPUOcclusionCulling, Styles.AllowGPUOcclusionCullingLabel);
            m_AllowPerObjectMotionVectorsField = AddRuntimeSettingsPropertyField(flags.contentContainer, m_AllowPerObjectMotionVectors, Styles.AllowPerObjectMotionVectorsLabel);
            m_AllowLegacyLightProbesField = AddRuntimeSettingsPropertyField(flags.contentContainer, m_AllowLegacyLightProbes, Styles.AllowLegacyLightProbesLabel);

            m_TerrainTabContent = new VisualElement();
            m_TerrainTabContent.AddToClassList("flora-scene-settings__terrain-content");
            m_SettingsContent.Add(m_TerrainTabContent);

            var terrain = new EditorSection(Styles.TerrainFoliageSection.text, iconClass: "grid");
            ApplyTooltip(terrain, Styles.TerrainFoliageSection.tooltip);
            m_TerrainTabContent.Add(terrain);
            m_AutoRegisterTerrainsButton = EditorElements.AddIconToggleButton(
                terrain.HeaderActions,
                m_AutoRegisterTerrains,
                Styles.AutoRegisterTerrainsLabel.text,
                Styles.AutoRegisterTerrainsLabel.tooltip,
                RefreshState,
                "refresh",
                "flora-scene-settings__terrain-auto-toggle");
            m_AutoRegisterTerrainsButton.AddToClassList("flora-editor-header-icon-button");

            var trees = new EditorSubgroup("Trees");
            trees.AddToClassList("flora-editor-subgroup--first");
            terrain.contentContainer.Add(trees);
            m_AllowPerTreeMotionVectorsField = AddPropertyField(trees.content, m_AllowPerTreeMotionVectors, Styles.AllowTreeMotionVectorsLabel);
            m_AllowPerTreeLightProbesField = AddPropertyField(trees.content, m_AllowPerTreeLightProbes, Styles.AllowTreeLightProbesLabel);

            var details = new EditorSubgroup("Details");
            terrain.contentContainer.Add(details);
            m_AllowPerDetailMotionVectorsField = AddPropertyField(details.content, m_AllowPerDetailMotionVectors, Styles.AllowDetailMotionVectorsLabel);
            m_AllowPerDetailLightProbesField = AddPropertyField(details.content, m_AllowPerDetailLightProbes, Styles.AllowDetailLightProbesLabel);

            m_DetailStreamingCluster = new VisualElement();
            m_DetailStreamingCluster.AddToClassList("flora-scene-settings__detail-streaming-cluster");
            details.content.Add(m_DetailStreamingCluster);
            AddPropertyField(m_DetailStreamingCluster, m_DetailStreamingMode, Styles.DetailStreamingModeLabel);
            m_DetailStreamingResponsivenessField = AddPropertyField(m_DetailStreamingCluster, m_DetailStreamingResponsiveness, Styles.DetailStreamingResponsivenessLabel);

            m_CustomDetailBudgetContainer = new VisualElement();
            m_CustomDetailBudgetContainer.AddToClassList("flora-scene-settings__custom-detail-budgets");
            m_DetailStreamingCluster.Add(m_CustomDetailBudgetContainer);
            AddPropertyField(m_CustomDetailBudgetContainer, m_CustomDetailPatchLayerBudgetPerFrame, Styles.CustomDetailPatchLayerBudgetLabel);
            AddPropertyField(m_CustomDetailBudgetContainer, m_CustomDetailStructuralInstanceBudgetPerFrame, Styles.CustomDetailStructuralBudgetLabel);

            AddPropertyField(m_DetailStreamingCluster, m_DetailUnloadHysteresisSeconds, Styles.DetailUnloadHysteresisLabel);

            m_StreamedDetailBudgetContainer = AddStreamingBudgetFooter(m_DetailStreamingCluster);
            m_StreamedDetailBudgetContainer.AddToClassList("flora-scene-settings__streamed-detail-budgets");

            m_ImmediateDetailBudgetContainer = AddStreamingNoteFooter(m_DetailStreamingCluster, "No per-frame detail streaming limit.");
            m_ImmediateDetailBudgetContainer.AddToClassList("flora-scene-settings__immediate-detail-budgets");
            return page;
        }

        private VisualElement BuildStatsTab()
        {
            var page = new VisualElement();
            page.AddToClassList("flora-editor-tab-page");

            m_StatsInactive = EditorElements.AddWarning(page, "Stats are unavailable while Flora rendering is disabled.", MessageType.Info);
            m_StatsInactive.AddToClassList("flora-scene-settings__stats-inactive");

            m_StatsContent = new VisualElement();
            m_StatsContent.AddToClassList("flora-scene-settings__stats-content");
            page.Add(m_StatsContent);

            m_CpuStatsTable = AddStatsSection(m_StatsContent, Styles.CPUCullingStatsSection, "culling-cpu", out m_CpuStatsSummary);
            m_GpuStatsTable = AddStatsSection(m_StatsContent, Styles.GPUCullingStatsSection, "culling-gpu", out m_GpuStatsSummary);
            m_BufferStatsTable = AddStatsSection(m_StatsContent, Styles.GraphicsBuffersStatsSection, "buffer", out m_BufferStatsSummary);

            return page;
        }

        private void RegisterStateTracking()
        {
            m_Root.TrackPropertyValue(m_EnableRendering, _ => RefreshState());
            m_Root.TrackPropertyValue(m_AllowDensityCulling, _ => RefreshState());
            m_Root.TrackPropertyValue(m_AllowGPUOcclusionCulling, _ => RefreshState());
            m_Root.TrackPropertyValue(m_AllowPerObjectMotionVectors, _ => RefreshState());
            m_Root.TrackPropertyValue(m_AllowLegacyLightProbes, _ => RefreshState());
            m_Root.TrackPropertyValue(m_EnableTerrainFoliage, _ => RefreshState());
            m_Root.TrackPropertyValue(m_AutoRegisterTerrains, _ => RefreshState());
            m_Root.TrackPropertyValue(m_AllowPerTreeMotionVectors, _ => RefreshState());
            m_Root.TrackPropertyValue(m_AllowPerTreeLightProbes, _ => RefreshState());
            m_Root.TrackPropertyValue(m_AllowPerDetailMotionVectors, _ => RefreshState());
            m_Root.TrackPropertyValue(m_AllowPerDetailLightProbes, _ => RefreshState());
            m_Root.TrackPropertyValue(m_DetailStreamingMode, _ => RefreshState());
            m_Root.TrackPropertyValue(m_DetailStreamingResponsiveness, _ => RefreshState());
        }

        private PropertyField AddPropertyField(VisualElement parent, SerializedProperty property, GUIContent label)
        {
            var field = new PropertyField(property, label.text)
            {
                tooltip = label.tooltip ?? string.Empty,
            };
            field.AddToClassList("unity-base-field__aligned");
            field.AddToClassList("flora-editor-property-field");
            field.RegisterCallback<GeometryChangedEvent>(_ => ApplyNativeInspectorAlignment(field));
            field.schedule.Execute(() => ApplyNativeInspectorAlignment(field));
            parent.Add(field);
            return field;
        }

        private PropertyField AddRuntimeSettingsPropertyField(VisualElement parent, SerializedProperty property, GUIContent label)
        {
            var row = new VisualElement();
            row.AddToClassList("flora-scene-settings__runtime-settings-field-row");
            parent.Add(row);
            return AddPropertyField(row, property, label);
        }

        private Button AddStatusChip(VisualElement parent, string text, string className)
        {
            SerializedProperty property = text == "Rendering" ? m_EnableRendering : m_EnableTerrainFoliage;
            void ToggleProperty()
            {
                property.serializedObject.Update();
                property.boolValue = !property.boolValue;
                property.serializedObject.ApplyModifiedProperties();
                RefreshState();
            }

            var chip = new Button(ToggleProperty)
            {
                text = text,
                tooltip = text,
            };
            chip.userData = (System.Action)ToggleProperty;
            chip.AddToClassList("flora-scene-settings__status-chip");
            chip.AddToClassList(className);
            parent.Add(chip);
            return chip;
        }

        private VisualElement AddStreamingBudgetFooter(VisualElement parent)
        {
            var footer = new VisualElement();
            footer.AddToClassList("flora-scene-settings__streaming-footer");
            footer.AddToClassList("flora-scene-settings__streaming-footer--compact");
            footer.AddToClassList("flora-scene-settings__streaming-footer--full-width");
            parent.Add(footer);

            var title = new Label("Streaming Budget");
            title.AddToClassList("flora-scene-settings__streaming-footer-title");
            title.tooltip = Styles.StreamedDetailBudgetInfo.tooltip;
            footer.Add(title);

            var values = new VisualElement();
            values.AddToClassList("flora-scene-settings__streaming-budget-values");
            footer.Add(values);

            m_StreamedDetailLayerBudgetLabel = AddStreamingBudgetChip(values, Styles.StreamedDetailBudgetInfo.tooltip);
            m_StreamedDetailInstanceBudgetLabel = AddStreamingBudgetChip(values, Styles.StreamedDetailBudgetInfo.tooltip);
            return footer;
        }

        private static VisualElement AddStreamingNoteFooter(VisualElement parent, string message)
        {
            var footer = new VisualElement();
            footer.AddToClassList("flora-scene-settings__streaming-footer");
            footer.AddToClassList("flora-scene-settings__streaming-footer--compact");
            footer.AddToClassList("flora-scene-settings__streaming-footer--full-width");
            footer.AddToClassList("flora-scene-settings__streaming-footer--note");
            parent.Add(footer);

            var label = new Label(message);
            label.AddToClassList("flora-scene-settings__streaming-footer-note");
            label.tooltip = message;
            footer.Add(label);
            return footer;
        }

        private static Label AddStreamingBudgetChip(VisualElement parent, string tooltip)
        {
            var chip = new Label();
            chip.AddToClassList("flora-scene-settings__streaming-budget-chip");
            chip.tooltip = tooltip;
            parent.Add(chip);
            return chip;
        }

        private static void ApplyNativeInspectorAlignment(VisualElement root)
        {
            if (root == null)
                return;

            root.AddToClassList("unity-base-field__aligned");
            foreach (VisualElement element in root.Query().ToList())
            {
                if (element.ClassListContains("unity-base-field"))
                    element.AddToClassList("unity-base-field__aligned");
            }
        }

        private static void ApplyTooltip(VisualElement element, string tooltip)
        {
            if (element == null)
                return;

            tooltip ??= string.Empty;
            element.tooltip = tooltip;
            SetTooltip(element.Q<Toggle>(), tooltip);
            SetTooltip(element.Q<Label>(className: "flora-editor-section__label"), tooltip);
        }

        private static void SetTooltip(VisualElement element, string tooltip)
        {
            if (element != null)
                element.tooltip = tooltip ?? string.Empty;
        }

        private static VisualElement AddStatsSection(VisualElement parent, GUIContent title, string iconClass, out Label summaryLabel)
        {
            var section = new EditorSection(title.text, iconClass: iconClass);
            ApplyTooltip(section, title.tooltip);
            parent.Add(section);

            summaryLabel = new Label("-")
            {
                tooltip = title.tooltip ?? string.Empty,
            };
            summaryLabel.AddToClassList("flora-editor-table__summary");
            section.contentContainer.Add(summaryLabel);

            var table = new VisualElement();
            table.AddToClassList("flora-editor-table");
            section.contentContainer.Add(table);
            return table;
        }

        private void RefreshRuntimeAvailability()
        {
            if (GraphicsSettings.TryGetRenderPipelineSettings<FloraRuntimeSettings>(out var runtimeSettings))
            {
                m_GPUOcclusionCullingAvailable = !runtimeSettings.DisableGPUOcclusionCulling;
                m_LegacyLightProbesAvailable = !runtimeSettings.DisableLegacyLightProbes;
                m_PerObjectMotionVectorsAvailable = !runtimeSettings.DisablePerObjectMotionVectors;
            }

#if HAS_PACKAGE_UNITY_URP
            if (GraphicsSettings.TryGetRenderPipelineSettings<RenderGraphSettings>(out var urpSettings))
                m_UniversalRenderGraphEnabled = !urpSettings.enableRenderCompatibilityMode;
#endif
        }

        private void RefreshState()
        {
            serializedObject.UpdateIfRequiredOrScript();
            RefreshRuntimeAvailability();
            SnapshotExternalSettingsState();
            UpdateWarnings();
            UpdateFieldStates();
            UpdateStatsCollectionState();
            RefreshStats();
        }

        internal void RefreshExternalSettingsForTests()
            => RefreshExternalSettingsState();

        private void RefreshExternalSettingsState()
        {
            if (!ExternalSettingsStateChanged())
                return;

            RefreshState();
        }

        private bool ExternalSettingsStateChanged()
        {
            RefreshRuntimeAvailability();

            return m_LastBRGShaderStrippingMode != EditorGraphicsSettings.batchRendererGroupShaderStrippingMode
                || m_LastGPUOcclusionCullingAvailable != m_GPUOcclusionCullingAvailable
                || m_LastLegacyLightProbesAvailable != m_LegacyLightProbesAvailable
                || m_LastPerObjectMotionVectorsAvailable != m_PerObjectMotionVectorsAvailable
#if HAS_PACKAGE_UNITY_URP
                || m_LastUniversalRenderGraphEnabled != m_UniversalRenderGraphEnabled
#endif
                ;
        }

        private void SnapshotExternalSettingsState()
        {
            m_LastBRGShaderStrippingMode = EditorGraphicsSettings.batchRendererGroupShaderStrippingMode;
            m_LastGPUOcclusionCullingAvailable = m_GPUOcclusionCullingAvailable;
            m_LastLegacyLightProbesAvailable = m_LegacyLightProbesAvailable;
            m_LastPerObjectMotionVectorsAvailable = m_PerObjectMotionVectorsAvailable;
#if HAS_PACKAGE_UNITY_URP
            m_LastUniversalRenderGraphEnabled = m_UniversalRenderGraphEnabled;
#endif
        }

        private void UpdateWarnings()
        {
            m_Warnings.Clear();

            if (!m_EnableRendering.boolValue)
            {
                EditorElements.AddWarning(m_Warnings, Styles.RenderingDisabledInfo.text, MessageType.Info);
                return;
            }

#if HAS_PACKAGE_UNITY_URP && !UNITY_6000_3_OR_NEWER
            if (!m_UniversalRenderGraphEnabled)
            {
                EditorElements.AddWarning(
                    m_Warnings,
                    Styles.URPNeedsRenderGraph.text,
                    MessageType.Error,
                    Styles.DisableRenderGraphCompatibility.text,
                    Styles.DisableRenderGraphCompatibility.tooltip,
                    () =>
                {
                    var urpSettings = GraphicsSettings.GetRenderPipelineSettings<RenderGraphSettings>();
                    urpSettings.enableRenderCompatibilityMode = false;
                    m_UniversalRenderGraphEnabled = true;
                    RefreshState();
                },
                    "DisableRenderGraphCompatibility",
                    "flora-scene-settings__render-graph-fix-button");
            }
#endif

            if (EditorGraphicsSettings.batchRendererGroupShaderStrippingMode != BatchRendererGroupStrippingMode.KeepAll)
                AddBRGShaderStrippingWarning(m_Warnings);

            if (!m_GPUOcclusionCullingAvailable)
                AddRuntimeSettingsWarning(m_Warnings, Styles.GPUOcclusionDisabledInfo.text, "m_DisableGPUOcclusionCulling", "GPUOcclusion");
            if (!m_PerObjectMotionVectorsAvailable)
                AddRuntimeSettingsWarning(m_Warnings, Styles.PerObjectMotionVectorsDisabledInfo.text, "m_DisablePerObjectMotionVectors", "PerObjectMotionVectors");
            if (!m_LegacyLightProbesAvailable)
                AddRuntimeSettingsWarning(m_Warnings, Styles.LegacyLightProbesDisabledInfo.text, "m_DisableLegacyLightProbes", "LegacyLightProbes");
            if (!m_EnableTerrainFoliage.boolValue)
                EditorElements.AddWarning(m_Warnings, Styles.TerrainFoliageDisabledInfo.text, MessageType.Info);
        }

        internal static VisualElement AddRuntimeSettingsWarning(VisualElement parent, string message, string propertyName, string actionNamePrefix)
            => EditorElements.AddWarning(
                parent,
                message,
                MessageType.Info,
                Styles.OpenRuntimeSettingsButton.text,
                Styles.OpenRuntimeSettingsButton.tooltip,
                () => OpenFloraGraphicsSettings(propertyName),
                $"{actionNamePrefix}RuntimeSettings",
                "flora-scene-settings__runtime-settings-button");

        internal static VisualElement AddBRGShaderStrippingWarning(VisualElement parent)
            => EditorElements.AddWarning(
                parent,
                Styles.BRGShaderStrippingErrorMessage.text,
                MessageType.Warning,
                Styles.BRGShaderStrippingFixButton.text,
                Styles.BRGShaderStrippingFixButton.tooltip,
                OpenBRGShaderStrippingSettings,
                "BRGVariantsFix",
                "flora-scene-settings__brg-fix-button");

        internal static void OpenBRGShaderStrippingSettings()
        {
            SettingsService.OpenProjectSettings("Project/Graphics");
            CoreEditorUtils.Highlight("Project Settings", "m_BrgStripping", HighlightSearchMode.Identifier);
        }

        internal static void OpenFloraGraphicsSettings()
            => OpenFloraGraphicsSettings(DefaultFloraGraphicsSettingsPropertyName);

        private static void OpenFloraGraphicsSettings(string propertyName)
        {
            GraphicsSettingsInspectorUtility.OpenAndScrollTo<FloraRuntimeSettings>();
            string highlightPath = ResolveFloraGraphicsSettingsHighlightPath(propertyName);
            if (!string.IsNullOrEmpty(highlightPath))
                CoreEditorUtils.Highlight("Project Settings", highlightPath, HighlightSearchMode.Identifier);
        }

        internal static string ResolveFloraGraphicsSettingsHighlightPath(string propertyName)
        {
            if (string.IsNullOrEmpty(propertyName))
                return null;

            if (GraphicsSettings.TryGetCurrentRenderPipelineGlobalSettings(out RenderPipelineGlobalSettings globalSettings) && globalSettings != null)
            {
                string propertyPath = FindSerializedPropertyPath(new SerializedObject(globalSettings), propertyName);
                if (!string.IsNullOrEmpty(propertyPath))
                    return propertyPath;
            }

            return propertyName;
        }

        internal static string FindSerializedPropertyPath(SerializedObject serializedObject, string propertyName)
        {
            if (serializedObject == null || string.IsNullOrEmpty(propertyName))
                return null;

            serializedObject.UpdateIfRequiredOrScript();
            SerializedProperty property = serializedObject.GetIterator();
            while (property.Next(true))
            {
                if (string.Equals(property.name, propertyName, System.StringComparison.Ordinal))
                    return property.propertyPath;
            }

            return null;
        }

        private void UpdateFieldStates()
        {
            bool renderingEnabled = m_EnableRendering.boolValue;
            bool terrainEnabled = renderingEnabled && m_EnableTerrainFoliage.boolValue;

            m_SettingsContent?.EnableInClassList("flora-scene-settings__settings-content--disabled", !renderingEnabled);
            SetStatusChipState(m_RenderingStatusChip, renderingEnabled, renderingEnabled ? "Rendering is enabled." : "Rendering is disabled.");
            SetStatusChipState(m_FoliageStatusChip, terrainEnabled, terrainEnabled ? "Terrain foliage is enabled." : "Terrain foliage is disabled.");
            m_FoliageStatusChip?.SetEnabled(renderingEnabled);

            m_AllowDensityCullingField?.SetEnabled(renderingEnabled);
            m_AllowGPUOcclusionCullingField?.SetEnabled(renderingEnabled && m_GPUOcclusionCullingAvailable);
            m_AllowPerObjectMotionVectorsField?.SetEnabled(renderingEnabled && m_PerObjectMotionVectorsAvailable);
            m_AllowLegacyLightProbesField?.SetEnabled(renderingEnabled && m_LegacyLightProbesAvailable);
            UpdateRuntimeSettingsInlineButton(
                ref m_GPUOcclusionRuntimeSettingsButton,
                m_AllowGPUOcclusionCullingField,
                renderingEnabled && !m_GPUOcclusionCullingAvailable,
                "GPU Occlusion Culling is controlled by Flora Runtime Settings.",
                "m_DisableGPUOcclusionCulling");
            UpdateRuntimeSettingsInlineButton(
                ref m_PerObjectMotionVectorsRuntimeSettingsButton,
                m_AllowPerObjectMotionVectorsField,
                renderingEnabled && !m_PerObjectMotionVectorsAvailable,
                "Per-Object Motion Vectors are controlled by Flora Runtime Settings.",
                "m_DisablePerObjectMotionVectors");
            UpdateRuntimeSettingsInlineButton(
                ref m_LegacyLightProbesRuntimeSettingsButton,
                m_AllowLegacyLightProbesField,
                renderingEnabled && !m_LegacyLightProbesAvailable,
                "Legacy Light Probes are controlled by Flora Runtime Settings.",
                "m_DisableLegacyLightProbes");

            m_TerrainTabContent?.EnableInClassList("flora-scene-settings__terrain-content--disabled", !terrainEnabled);
            m_AutoRegisterTerrainsButton?.SetEnabled(terrainEnabled);
            SetIconToggleState(m_AutoRegisterTerrainsButton, terrainEnabled && m_AutoRegisterTerrains.boolValue);
            m_AllowPerTreeMotionVectorsField?.SetEnabled(terrainEnabled && m_AllowPerObjectMotionVectors.boolValue && m_PerObjectMotionVectorsAvailable);
            m_AllowPerDetailMotionVectorsField?.SetEnabled(terrainEnabled && m_AllowPerObjectMotionVectors.boolValue && m_PerObjectMotionVectorsAvailable);
            m_AllowPerTreeLightProbesField?.SetEnabled(terrainEnabled && m_AllowLegacyLightProbes.boolValue && m_LegacyLightProbesAvailable);
            m_AllowPerDetailLightProbesField?.SetEnabled(terrainEnabled && m_AllowLegacyLightProbes.boolValue && m_LegacyLightProbesAvailable);

            FloraDetailStreamingMode mode = (FloraDetailStreamingMode)m_DetailStreamingMode.intValue;
            m_DetailStreamingResponsivenessField?.SetEnabled(terrainEnabled && mode == FloraDetailStreamingMode.Streamed);
            if (m_CustomDetailBudgetContainer != null)
                m_CustomDetailBudgetContainer.style.display = mode == FloraDetailStreamingMode.Custom ? DisplayStyle.Flex : DisplayStyle.None;
            if (m_StreamedDetailBudgetContainer != null)
                m_StreamedDetailBudgetContainer.style.display = mode == FloraDetailStreamingMode.Streamed ? DisplayStyle.Flex : DisplayStyle.None;
            if (m_ImmediateDetailBudgetContainer != null)
                m_ImmediateDetailBudgetContainer.style.display = mode == FloraDetailStreamingMode.Immediate ? DisplayStyle.Flex : DisplayStyle.None;
            if (m_StatsInactive != null)
                m_StatsInactive.style.display = renderingEnabled ? DisplayStyle.None : DisplayStyle.Flex;
            if (m_StatsContent != null)
                m_StatsContent.style.display = renderingEnabled ? DisplayStyle.Flex : DisplayStyle.None;

            UpdateStreamingBudgetRows();
        }

        private static void UpdateRuntimeSettingsInlineButton(
            ref Button button,
            PropertyField field,
            bool visible,
            string tooltip,
            string propertyName)
        {
            if (field == null)
                return;

            VisualElement parent = field.parent;
            if (parent == null)
                return;

            if (!visible)
            {
                button?.RemoveFromHierarchy();
                button = null;
                return;
            }

            if (button == null)
            {
                button = EditorElements.AddIconButton(
                    parent,
                    tooltip,
                    () => OpenFloraGraphicsSettings(propertyName),
                    "settings");
                button.AddToClassList("flora-scene-settings__runtime-settings-inline-button");
                button.userData = (System.Action)(() => OpenFloraGraphicsSettings(propertyName));
            }

            button.tooltip = tooltip;
        }

        private static void SetStatusChipState(Button chip, bool active, string tooltip)
        {
            if (chip == null)
                return;

            chip.EnableInClassList("flora-scene-settings__status-chip--active", active);
            chip.EnableInClassList("flora-scene-settings__status-chip--inactive", !active);
            chip.tooltip = tooltip;
        }

        private static void SetIconToggleState(Button button, bool enabled)
        {
            button?.EnableInClassList("flora-editor-icon-toggle-button--on", enabled);
            button?.EnableInClassList("flora-editor-icon-toggle-button--off", !enabled);
        }

        private void UpdateStreamingBudgetRows()
        {
            if (m_StreamedDetailLayerBudgetLabel == null || m_StreamedDetailInstanceBudgetLabel == null)
                return;

            float responsiveness = m_DetailStreamingResponsiveness.floatValue;
            SetValueLabel(
                m_StreamedDetailLayerBudgetLabel,
                $"{ResolveDetailPatchLayerBudget(responsiveness):n0} Layers");
            SetValueLabel(
                m_StreamedDetailInstanceBudgetLabel,
                $"{ResolveDetailStructuralBudget(responsiveness):n0} Instances");
            m_StreamedDetailLayerBudgetLabel.tooltip = Styles.StreamedDetailBudgetInfo.tooltip;
            m_StreamedDetailInstanceBudgetLabel.tooltip = Styles.StreamedDetailBudgetInfo.tooltip;
        }

        private static void SetValueLabel(Label label, string value)
        {
            if (label == null)
                return;

            label.text = string.IsNullOrEmpty(value) ? "-" : value;
            label.tooltip = label.text;
        }

        private static float EvaluateDetailStreamingResponsiveness(float responsiveness)
        {
            return Mathf.Pow(Mathf.Clamp01(responsiveness), 0.75f);
        }

        private static int ResolveDetailPatchLayerBudget(float responsiveness)
        {
            float t = EvaluateDetailStreamingResponsiveness(responsiveness);
            return Mathf.RoundToInt(Mathf.Lerp(1f, 24f, t));
        }

        private static int ResolveDetailStructuralBudget(float responsiveness)
        {
            float t = EvaluateDetailStreamingResponsiveness(responsiveness);
            return Mathf.RoundToInt(Mathf.Lerp(500f, 12000f, t));
        }

        private void UpdateStatsCollectionState()
        {
            bool shouldEnableStats = m_ActiveTab == SceneSettingsTab.Stats
                && m_EnableRendering.boolValue
                && FloraSystem.Active
                && FloraSystem.Instance.CullingSystem != null;

            SetCullingStatsEnabled(shouldEnableStats);

            if (m_StatsRefreshItem == null)
                return;

            if (m_ActiveTab == SceneSettingsTab.Stats && m_EnableRendering.boolValue)
                m_StatsRefreshItem.Resume();
            else
                m_StatsRefreshItem.Pause();
        }

        private static void SetCullingStatsEnabled(bool enabled)
        {
            if (!FloraSystem.Active || FloraSystem.Instance.CullingSystem == null)
                return;

            FloraSystem.Instance.CullingSystem.EnableCPUCullingStats = enabled;
            FloraSystem.Instance.CullingSystem.EnableGPUCullingStats = enabled;
        }

        private void RefreshStats()
        {
            if (m_CpuStatsTable == null || m_GpuStatsTable == null || m_BufferStatsTable == null)
                return;
            if (!m_EnableRendering.boolValue)
            {
                ClearStatsTables();
                SetStatsSummary(m_CpuStatsSummary, "Stats disabled.");
                SetStatsSummary(m_GpuStatsSummary, "Stats disabled.");
                SetStatsSummary(m_BufferStatsSummary, "Stats disabled.");
                return;
            }

            RefreshCpuStats();
            RefreshGpuStats();
            RefreshBufferStats();
        }

        private void ClearStatsTables()
        {
            m_CpuStatsTable?.Clear();
            m_GpuStatsTable?.Clear();
            m_BufferStatsTable?.Clear();
        }

        private void RefreshCpuStats()
        {
            m_CpuStatsTable.Clear();
            TableColumn[] columns = CpuStatsColumns;
            AddTableHeader(m_CpuStatsTable, columns);

            if (!FloraSystem.Active || FloraSystem.Instance.CullingSystem == null)
            {
                SetStatsSummary(m_CpuStatsSummary, "No active Flora culling system.");
                AddTableMessage(m_CpuStatsTable, "Flora system is not active.");
                return;
            }

            using (ListPool<CPUCullingStats>.Get(out List<CPUCullingStats> stats))
            {
                FloraSystem.Instance.CullingSystem.GetCPUCullingStats(stats);
                if (stats.Count == 0)
                {
                    SetStatsSummary(m_CpuStatsSummary, "Waiting for CPU culling stats.");
                    AddTableMessage(m_CpuStatsTable, "No CPU culling stats captured yet.");
                    return;
                }

                long chunkTotal = 0;
                long instanceTotal = 0;
                long drawInstanceTotal = 0;
                int rowCount = Mathf.Min(stats.Count, 24);
                for (int i = 0; i < rowCount; i++)
                {
                    CPUCullingStats stat = stats[i];
                    chunkTotal += stat.VisibleChunkCount;
                    instanceTotal += stat.VisibleInstanceCount;
                    drawInstanceTotal += stat.DrawInstanceCount;
                    Object view = stat.ViewId.ToObject();
                    AddTableRow(
                        m_CpuStatsTable,
                        columns,
                        view ? view.name : "Unknown",
                        stat.VisibleChunkCount.ToString("n0"),
                        stat.VisibleInstanceCount.ToString("n0"),
                        stat.DrawInstanceCount.ToString("n0"));
                }

                if (stats.Count > rowCount)
                    AddTableMessage(m_CpuStatsTable, $"+ {stats.Count - rowCount:n0} more views");

                SetStatsSummary(
                    m_CpuStatsSummary,
                    $"{stats.Count:n0} views  |  {chunkTotal:n0} chunks  |  {instanceTotal:n0} instances  |  {drawInstanceTotal:n0} draw instances");
            }
        }

        private void RefreshGpuStats()
        {
            m_GpuStatsTable.Clear();
            TableColumn[] columns = GpuStatsColumns;
            AddTableHeader(m_GpuStatsTable, columns);

            if (!FloraSystem.Active || FloraSystem.Instance.CullingSystem == null)
            {
                SetStatsSummary(m_GpuStatsSummary, "No active Flora culling system.");
                AddTableMessage(m_GpuStatsTable, "Flora system is not active.");
                return;
            }

            using (ListPool<GPUCullingStats>.Get(out List<GPUCullingStats> stats))
            {
                FloraSystem.Instance.CullingSystem.GetGPUCullingStats(stats);
                if (stats.Count == 0)
                {
                    SetStatsSummary(m_GpuStatsSummary, "Waiting for GPU culling stats.");
                    AddTableMessage(m_GpuStatsTable, "No GPU culling stats captured yet.");
                    return;
                }

                long drawTotal = 0;
                long occludedTotal = 0;
                long visibleTotal = 0;
                int rowCount = Mathf.Min(stats.Count, 24);
                for (int i = 0; i < rowCount; i++)
                {
                    GPUCullingStats stat = stats[i];
                    drawTotal += stat.VisibleDraws;
                    occludedTotal += stat.OccludedInstances;
                    visibleTotal += stat.VisibleInstances;
                    Object view = stat.ViewId.ToObject();
                    AddTableRow(
                        m_GpuStatsTable,
                        columns,
                        view ? view.name : "Unknown",
                        stat.VisibleDraws.ToString("n0"),
                        stat.OccludedInstances.ToString("n0"),
                        stat.VisibleInstances.ToString("n0"));
                }

                if (stats.Count > rowCount)
                    AddTableMessage(m_GpuStatsTable, $"+ {stats.Count - rowCount:n0} more views");

                SetStatsSummary(
                    m_GpuStatsSummary,
                    $"{stats.Count:n0} views  |  {drawTotal:n0} draws  |  {occludedTotal:n0} occluded  |  {visibleTotal:n0} visible");
            }
        }

        private void RefreshBufferStats()
        {
            m_BufferStatsTable.Clear();
            TableColumn[] columns = BufferStatsColumns;
            AddTableHeader(m_BufferStatsTable, columns);

            using (ListPool<GraphicsBufferStore.DebugBufferInfo>.Get(out var bufferInfos))
            {
                GraphicsBufferStore.GetDebugBufferInfos(bufferInfos);
                bufferInfos.Sort((a, b) => b.Descriptor.SizeInBytes.CompareTo(a.Descriptor.SizeInBytes));

                if (bufferInfos.Count == 0)
                {
                    SetStatsSummary(m_BufferStatsSummary, "No graphics buffers allocated.");
                    AddTableMessage(m_BufferStatsTable, "No graphics buffers allocated.");
                    return;
                }

                long sizeTotal = 0;
                for (int i = 0; i < bufferInfos.Count; i++)
                    sizeTotal += bufferInfos[i].Descriptor.SizeInBytes;

                int rowCount = Mathf.Min(bufferInfos.Count, 24);
                for (int i = 0; i < rowCount; i++)
                {
                    GraphicsBufferStore.DebugBufferInfo info = bufferInfos[i];
                    AddTableRow(
                        m_BufferStatsTable,
                        columns,
                        info.StoreType.ToString(),
                        string.IsNullOrEmpty(info.DebugName) ? "(Unnamed)" : info.DebugName,
                        StringUtility.FormatBytes(info.Descriptor.SizeInBytes));
                }

                if (bufferInfos.Count > rowCount)
                    AddTableMessage(m_BufferStatsTable, $"+ {bufferInfos.Count - rowCount:n0} more buffers");

                SetStatsSummary(
                    m_BufferStatsSummary,
                    $"{bufferInfos.Count:n0} buffers  |  {StringUtility.FormatBytes(sizeTotal)} shown");
            }
        }

        private readonly struct TableColumn
        {
            public readonly string Header;
            public readonly float Flex;
            public readonly bool Numeric;

            public TableColumn(string header, float flex, bool numeric = false)
            {
                Header = header;
                Flex = flex;
                Numeric = numeric;
            }
        }

        private static readonly TableColumn[] CpuStatsColumns =
        {
            new TableColumn("View", 2.2f),
            new TableColumn("Chunks", 0.9f, true),
            new TableColumn("Instances", 1.1f, true),
            new TableColumn("Draw Instances", 1.2f, true),
        };

        private static readonly TableColumn[] GpuStatsColumns =
        {
            new TableColumn("View", 2.2f),
            new TableColumn("Draws", 0.9f, true),
            new TableColumn("Occluded", 1.1f, true),
            new TableColumn("Visible", 1.1f, true),
        };

        private static readonly TableColumn[] BufferStatsColumns =
        {
            new TableColumn("Type", 1.0f),
            new TableColumn("Name", 2.2f),
            new TableColumn("Size", 0.9f, true),
        };

        private static void AddTableHeader(VisualElement parent, TableColumn[] columns)
        {
            string[] values = new string[columns.Length];
            for (int i = 0; i < columns.Length; i++)
                values[i] = columns[i].Header;

            AddTableRow(parent, "flora-editor-table__header", columns, values, true);
        }

        private static void AddTableRow(VisualElement parent, TableColumn[] columns, params string[] values)
        {
            AddTableRow(parent, "flora-editor-table__row", columns, values, false);
        }

        private static void AddTableRow(VisualElement parent, string rowClass, TableColumn[] columns, string[] values, bool header)
        {
            var row = new VisualElement();
            row.AddToClassList(rowClass);
            parent.Add(row);

            for (int i = 0; i < columns.Length; i++)
            {
                string value = i < values.Length ? values[i] : string.Empty;
                var cell = new Label(value);
                cell.AddToClassList("flora-editor-table__cell");
                if (columns[i].Numeric)
                    cell.AddToClassList("flora-editor-table__cell--numeric");
                if (header)
                    cell.AddToClassList("flora-editor-table__cell--header");
                cell.style.flexGrow = columns[i].Flex;
                cell.style.flexBasis = 0;
                cell.tooltip = value;
                row.Add(cell);
            }
        }

        private static void AddTableMessage(VisualElement parent, string message)
        {
            var row = new VisualElement();
            row.AddToClassList("flora-editor-table__row");
            parent.Add(row);

            var cell = new Label(message);
            cell.AddToClassList("flora-editor-table__cell");
            cell.tooltip = message;
            row.Add(cell);
        }

        private static void SetStatsSummary(Label label, string text)
        {
            if (label == null)
                return;

            label.text = string.IsNullOrEmpty(text) ? "-" : text;
            label.tooltip = label.text;
        }

        private void AddGlobalRenderSettingsVolume()
        {
            var sceneSettings = (FloraSceneSettings)target;
            var sceneSettingsName = sceneSettings.gameObject.name;

            var profile = VolumeProfileFactory.CreateVolumeProfile(sceneSettings.gameObject.scene, sceneSettingsName);
            VolumeProfileFactory.CreateVolumeComponent<FloraRenderSettings>(profile, false, false);
            VolumeProfileFactory.CreateVolumeComponent<FloraDensitySettings>(profile, false, false);

            var volume = sceneSettings.gameObject.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = profile;
            Undo.RegisterCreatedObjectUndo(volume, "Add Render Settings Volume");
            RefreshState();
        }
    }
}
