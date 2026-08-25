// Copyright © Magnetic Arcade. All Rights Reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using UnityEngine;
using UnityEngine.Rendering;
using NameAndTooltip = UnityEngine.Rendering.DebugUI.Widget.NameAndTooltip;

namespace MA.Flora
{
    [GenerateHLSL]
    public enum DebugInstanceDrawMode
    {
        None = 0,
        LOD = 1,
#if UNITY_EDITOR
        InstanceHandle = 2,
#endif
        RandomID = 3,
        Template = 4,
        Draw = 5,
        DrawVariant = 6,
        CullingBatch = 7,
        BatchDomain = 8,
    }

    public enum DebugSpatialHashMode
    {
        Disabled,
        Heatmap,
        Level,
    }

    [Flags]
    public enum DebugSpatialHashFlags
    {
        None   = 0,
        Blocks = 1 << 0,
        Cells  = 1 << 1,
        Chunks = 1 << 2,
    }

    public enum DebugLodMode
    {
        None,
        ForceLOD,
        OnlyLOD,
    }

    [SuppressMessage("ReSharper", "InconsistentNaming")]
    internal static class DebugShaderPropertyId
    {
        public static readonly int flora_DebugViewMode = Shader.PropertyToID("flora_DebugViewMode");
        public static readonly int flora_DebugOpacity = Shader.PropertyToID("flora_DebugOpacity");
        public static readonly int flora_DebugDrawVisibility = Shader.PropertyToID("flora_DebugDrawVisibility");
    }

    internal static class DebugGlobalKeywords
    {
        public static GlobalKeyword DebugDisplay;

        public static void Initialize()
        {
            DebugDisplay = GlobalKeyword.Create("DEBUG_DISPLAY");
        }
    }

    public struct FloraDebugDisplayProperties
    {
        public static readonly FloraDebugDisplayProperties Default = new()
        {
            InstanceDrawOpacity = 1.0f,
            InstanceDrawMode = DebugInstanceDrawMode.None,
            EnableGPUChecks = false,
            DisableDensityCulling = false,
            LODMode = DebugLodMode.None,
            LODIndex = 0,
            OcclusionTestOverlayEnabled = false,
            OcclusionOverlayCountVisible = false,
            OcclusionOverrideTestToAlwaysPass = false,
            OccluderDepthOverlayEnabled = false,
            OcclusionTestOverlayOpacity = 0.4f,
            OcclusionDepthViewRange = new Vector2(0.0f, 1.0f),
            SpatialHashMode = DebugSpatialHashMode.Disabled,
            SpatialHashFlags = DebugSpatialHashFlags.Cells,
            SpatialHashMaxDistance = 300,
        };

        public float InstanceDrawOpacity;
        public DebugInstanceDrawMode InstanceDrawMode;
        public bool EnableGPUChecks;
        public bool DisableDensityCulling;

        public DebugLodMode LODMode;
        public int LODIndex;

        public bool OcclusionTestOverlayEnabled;
        public float OcclusionTestOverlayOpacity;
        public bool OcclusionOverlayCountVisible;
        public bool OcclusionOverrideTestToAlwaysPass;
        public bool OccluderDepthOverlayEnabled;
        public Vector2 OcclusionDepthViewRange;

        public bool RenderSpatialHash => SpatialHashMode != DebugSpatialHashMode.Disabled;
        public DebugSpatialHashMode SpatialHashMode;
        public DebugSpatialHashFlags SpatialHashFlags;
        public float SpatialHashMaxDistance;

        public bool EnableCPUCullingStats;
        public bool EnableGPUCullingStats;

        public void Reset()
        {
            this = Default;
        }
    }

    public class FloraDebugDisplaySettings : DebugDisplaySettings<FloraDebugDisplaySettings>
    {
        public DebugDisplayFlora DisplayData { get; private set; }

        public FloraDebugDisplaySettings()
        {
            Reset();
        }

        public override void Reset()
        {
            base.Reset();
            DebugDisplayFlora.Properties.Reset();
            DisplayData = Add(new DebugDisplayFlora());
        }

        public void UpdateDisplay()
        {
            DisplayData?.Update();
        }
    }

    [DisplayInfo(name = "Flora", order = 5)]
    public class DebugDisplayFlora : IDebugDisplaySettingsData
    {
        private static FloraDebugDisplayProperties s_SharedProperties = FloraDebugDisplayProperties.Default;
        public static ref FloraDebugDisplayProperties Properties => ref s_SharedProperties;
        public static bool Active => ForceDisplay || DebugManager.instance.isAnyDebugUIActive;
        public static bool NeedsCullingDebug => Active && (Properties.InstanceDrawMode != DebugInstanceDrawMode.None || Properties.LODMode != DebugLodMode.None || Properties.EnableGPUChecks);
        public static bool ForceDisplay { get; set; }

        private static class Strings
        {
            public static NameAndTooltip CullingStats = new()
            {
                name = "Display Live Culling Stats",
                tooltip = "Collects CPU and GPU culling statistics for active views. GPU statistics require asynchronous readback and may affect performance."
            };
            public static NameAndTooltip EnableGPUChecks = new()
            {
                name = "Enable GPU Checks",
                tooltip = "Enables GPU validation checks in the culling system for debugging purposes. This may impact performance."
            };
        }

        public const string PanelName = "Flora";
        private const string FormatString = "{0}";
        private const float RefreshRate = 1f / 5f;
        private const int MaxViewCount = 32;
        private static readonly DebugInstanceDrawMode[] s_InstanceDrawModes = (DebugInstanceDrawMode[])Enum.GetValues(typeof(DebugInstanceDrawMode));

#region IDebugDisplaySettingsQuery

        /// <inheritdoc/>
        public bool AreAnySettingsActive => FloraSystem.Active && Active && Properties.InstanceDrawMode != DebugInstanceDrawMode.None;

        /// <inheritdoc/>
        public bool IsPostProcessingAllowed => !AreAnySettingsActive;

        /// <inheritdoc/>
        public bool IsLightingActive => !AreAnySettingsActive;

        /// <inheritdoc/>
        public bool TryGetScreenClearColor(ref Color color)
        {
            return false;
        }

        /// <inheritdoc/>
        IDebugDisplaySettingsPanelDisposable IDebugDisplaySettingsData.CreatePanel()
        {
            return new SettingsPanel(this);
        }

#endregion

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        public void Update()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            DebugInstanceDrawMode instanceDrawMode = Properties.InstanceDrawMode;
            Shader.SetGlobalInteger(DebugShaderPropertyId.flora_DebugViewMode, (int)instanceDrawMode);
            Shader.SetGlobalFloat(DebugShaderPropertyId.flora_DebugOpacity, instanceDrawMode != DebugInstanceDrawMode.None ? Properties.InstanceDrawOpacity : 0.0f);

            if (Active)
            {
                UpdateCachedCullingStats();
            }
#endif
        }

        private struct CombinedCullingStats
        {
            public EntityId ViewId;
            public BatchCullingViewType ViewType;
            public bool HasCPUStats;
            public bool HasGPUStats;
            public int VisibleChunkCount;
            public int CPUVisibleInstanceCount;
            public int DrawCommandCount;
            public int GPUVisibleInstanceCount;
            public int OccludedInstanceCount;
            public int VisibleDrawCount;
        }

        private static readonly List<CPUCullingStats> s_CachedCPUCullingStats = new List<CPUCullingStats>();
        private static readonly List<GPUCullingStats> s_CachedGPUCullingStats = new List<GPUCullingStats>();
        private static readonly List<CombinedCullingStats> s_CombinedCullingStats = new List<CombinedCullingStats>();

        private static void UpdateCachedCullingStats()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            CullingSystem cullingSystem = FloraSystem.Instance.CullingSystem;
            if (cullingSystem == null)
            {
                s_CachedCPUCullingStats.Clear();
                s_CachedGPUCullingStats.Clear();
                s_CombinedCullingStats.Clear();
                return;
            }

            cullingSystem.GetCPUCullingStats(s_CachedCPUCullingStats);
            cullingSystem.GetGPUCullingStats(s_CachedGPUCullingStats);
            s_CombinedCullingStats.Clear();

            for (int i = 0; i < s_CachedCPUCullingStats.Count; i++)
            {
                CPUCullingStats stats = s_CachedCPUCullingStats[i];
                s_CombinedCullingStats.Add(new CombinedCullingStats
                {
                    ViewId = stats.ViewId,
                    ViewType = stats.ViewType,
                    HasCPUStats = true,
                    VisibleChunkCount = stats.VisibleChunkCount,
                    CPUVisibleInstanceCount = stats.VisibleInstanceCount,
                    DrawCommandCount = stats.DrawCommandCount,
                });
            }

            for (int i = 0; i < s_CachedGPUCullingStats.Count; i++)
            {
                GPUCullingStats stats = s_CachedGPUCullingStats[i];
                int combinedIndex = FindCombinedCullingStats(stats.ViewId, stats.ViewType);
                CombinedCullingStats combinedStats = combinedIndex >= 0
                    ? s_CombinedCullingStats[combinedIndex]
                    : new CombinedCullingStats { ViewId = stats.ViewId, ViewType = stats.ViewType };

                combinedStats.HasGPUStats = true;
                combinedStats.GPUVisibleInstanceCount = stats.VisibleInstances;
                combinedStats.OccludedInstanceCount = stats.OccludedInstances;
                combinedStats.VisibleDrawCount = stats.VisibleDraws;

                if (combinedIndex >= 0)
                {
                    s_CombinedCullingStats[combinedIndex] = combinedStats;
                }
                else
                {
                    s_CombinedCullingStats.Add(combinedStats);
                }
            }

            s_CombinedCullingStats.Sort(CompareCombinedCullingStats);
#endif
        }

        private static int FindCombinedCullingStats(EntityId viewId, BatchCullingViewType viewType)
        {
            for (int i = 0; i < s_CombinedCullingStats.Count; i++)
            {
                CombinedCullingStats stats = s_CombinedCullingStats[i];
                if (stats.ViewId == viewId && stats.ViewType == viewType)
                {
                    return i;
                }
            }

            return -1;
        }

        private static int CompareCombinedCullingStats(CombinedCullingStats a, CombinedCullingStats b)
        {
            int viewTypeComparison = a.ViewType.CompareTo(b.ViewType);
            return viewTypeComparison != 0 ? viewTypeComparison : a.ViewId.GetHashCode().CompareTo(b.ViewId.GetHashCode());
        }

        private static int GetViewStatsCount()
        {
            return s_CombinedCullingStats.Count;
        }

        private static CombinedCullingStats GetViewStats(int viewStatsIndex)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (viewStatsIndex < 0 || viewStatsIndex >= s_CombinedCullingStats.Count)
            {
                return default;
            }

            return s_CombinedCullingStats[viewStatsIndex];
#else
            return default;
#endif
        }

        private static string GetViewName(int viewStatsIndex)
        {
            CombinedCullingStats stats = GetViewStats(viewStatsIndex);
            UnityEngine.Object view = stats.ViewId.ToObject();
            return view ? view.name : stats.ViewId.ToString();
        }

        private static object GetCPUStat(int viewStatsIndex, int value)
        {
            return GetViewStats(viewStatsIndex).HasCPUStats ? value : "—";
        }

        private static object GetGPUStat(int viewStatsIndex, int value)
        {
            return GetViewStats(viewStatsIndex).HasGPUStats ? value : "—";
        }

        private static DebugUI.Table.Row AddViewStatsDataRow(int viewStatsIndex)
        {
            return new DebugUI.Table.Row
            {
                displayName = "",
                opened = true,
                isHiddenCallback = () => viewStatsIndex >= GetViewStatsCount(),
                children =
                {
                    new DebugUI.Value { displayName = "View", refreshRate = RefreshRate, formatString = FormatString, getter = () => GetViewName(viewStatsIndex) },
                    new DebugUI.Value { displayName = "Type", refreshRate = RefreshRate, formatString = FormatString, getter = () => GetViewStats(viewStatsIndex).ViewType },
                    new DebugUI.Value
                    {
                        displayName = "CPU Chunks", refreshRate = RefreshRate, formatString = FormatString,
                        getter = () => GetCPUStat(viewStatsIndex, GetViewStats(viewStatsIndex).VisibleChunkCount)
                    },
                    new DebugUI.Value
                    {
                        displayName = "CPU Instances", refreshRate = RefreshRate, formatString = FormatString,
                        getter = () => GetCPUStat(viewStatsIndex, GetViewStats(viewStatsIndex).CPUVisibleInstanceCount)
                    },
                    new DebugUI.Value
                    {
                        displayName = "CPU Commands", refreshRate = RefreshRate, formatString = FormatString,
                        getter = () => GetCPUStat(viewStatsIndex, GetViewStats(viewStatsIndex).DrawCommandCount)
                    },
                    new DebugUI.Value
                    {
                        displayName = "GPU Visible", refreshRate = RefreshRate, formatString = FormatString,
                        getter = () => GetGPUStat(viewStatsIndex, GetViewStats(viewStatsIndex).GPUVisibleInstanceCount)
                    },
                    new DebugUI.Value
                    {
                        displayName = "GPU Occluded", refreshRate = RefreshRate, formatString = FormatString,
                        getter = () => GetGPUStat(viewStatsIndex, GetViewStats(viewStatsIndex).OccludedInstanceCount)
                    },
                    new DebugUI.Value
                    {
                        displayName = "GPU Draws", refreshRate = RefreshRate, formatString = FormatString,
                        getter = () => GetGPUStat(viewStatsIndex, GetViewStats(viewStatsIndex).VisibleDrawCount)
                    },
                }
            };
        }

        [DisplayInfo(name = "Flora", order = 5)]
        private class SettingsPanel : DebugDisplaySettingsPanel<DebugDisplayFlora>
        {
            public override string PanelName => "Flora";

            public override DebugUI.Flags Flags => DebugUI.Flags.EditorForceUpdate;

            public SettingsPanel(DebugDisplayFlora data)
                : base(data)
            {
                var helpBox = new DebugUI.MessageBox
                {
                    displayName = "Not Running",
                    style = DebugUI.MessageBox.Style.Info,
                    messageCallback = () => FloraSystem.Active ? string.Empty : "Flora is not currently active. " +
                                                                                "Please ensure that the Flora system is initialized and active in the scene.",
                    isHiddenCallback = () => FloraSystem.Active,
                };
                AddWidget(helpBox);

                AddWidget(CreateGeneralSettings());
                AddWidget(CreateCullingOverrideSettings());
                AddWidget(CreateLODSettings());
                AddWidget(CreateGPUOcclusionSettings());
                AddWidget(CreateSpatialHashSettings());
                AddWidget(CreateCullingStats());
            }

            private static int GetInstanceDrawModeIndex()
            {
                return Array.IndexOf(s_InstanceDrawModes, Properties.InstanceDrawMode);
            }

            private static void SetInstanceDrawModeIndex(int index)
            {
                Properties.InstanceDrawMode = s_InstanceDrawModes[index];
            }

            private DebugUI.Widget CreateGeneralSettings()
            {
                return new DebugUI.Container
                {
                    displayName = "General",
                    isHiddenCallback = () => !FloraSystem.Active,
                    children =
                    {
                        new DebugUI.EnumField
                        {
                            displayName = "Debug Shading Mode",
                            tooltip = "Set the instance debug shading mode.",
                            autoEnum = typeof(DebugInstanceDrawMode),
                            getter = () => (int)Properties.InstanceDrawMode,
                            setter = value => Properties.InstanceDrawMode = (DebugInstanceDrawMode)value,
                            getIndex = GetInstanceDrawModeIndex,
                            setIndex = SetInstanceDrawModeIndex
                        },
                        new DebugUI.FloatField
                        {
                            displayName = "Debug Shading Opacity",
                            tooltip = "Blends Flora debug shading with the normally shaded surface.",
                            getter = () => Properties.InstanceDrawOpacity,
                            setter = value => Properties.InstanceDrawOpacity = value,
                            min = () => 0.0f,
                            max = () => 1.0f,
                            isHiddenCallback = () => Properties.InstanceDrawMode == DebugInstanceDrawMode.None,
                        },
                        new DebugUI.BoolField
                        {
                            nameAndTooltip = Strings.CullingStats,
                            getter = () => Properties.EnableCPUCullingStats || Properties.EnableGPUCullingStats,
                            setter = value =>
                            {
                                Properties.EnableCPUCullingStats = value;
                                Properties.EnableGPUCullingStats = value;
                            }
                        },
                        new DebugUI.BoolField
                        {
                            nameAndTooltip = Strings.EnableGPUChecks,
                            getter = () => Properties.EnableGPUChecks,
                            setter = value => Properties.EnableGPUChecks = value
                        },
                    }
                };
            }

            private DebugUI.Widget CreateCullingOverrideSettings()
            {
                return new DebugUI.Container
                {
                    displayName = "Culling Overrides",
                    isHiddenCallback = () => !FloraSystem.Active,
                    children =
                    {
                        new DebugUI.BoolField
                        {
                            displayName = "Disable Density Culling",
                            tooltip = "Temporarily bypasses Flora density culling to diagnose missing or unexpectedly sparse instances.",
                            getter = () => Properties.DisableDensityCulling,
                            setter = value => Properties.DisableDensityCulling = value,
                        },
                        new DebugUI.BoolField
                        {
                            displayName = "Disable GPU Occlusion Culling",
                            tooltip = "Temporarily makes every GPU occlusion test pass to diagnose missing instances.",
                            getter = () => Properties.OcclusionOverrideTestToAlwaysPass,
                            setter = value => Properties.OcclusionOverrideTestToAlwaysPass = value,
                            isHiddenCallback = () => !FloraSystem.Instance.AllowGPUOcclusionCulling,
                        },
                    }
                };
            }

            private DebugUI.Widget CreateLODSettings()
            {
                return new DebugUI.Container
                {
                    displayName = "LOD",
                    isHiddenCallback = () => !FloraSystem.Active,
                    children =
                    {
                        new DebugUI.EnumField
                        {
                            displayName = "Mode",
                            tooltip = "Which LOD mode to use.",
                            autoEnum = typeof(DebugLodMode),
                            getter = () => (int)Properties.LODMode,
                            setter = value => Properties.LODMode = (DebugLodMode)value,
                            getIndex = () => (int)Properties.LODMode,
                            setIndex = value => Properties.LODMode = (DebugLodMode)value
                        },
                        new DebugUI.IntField
                        {
                            displayName = "LOD Index",
                            tooltip = "Which LOD index to use for the selected mode, if any.",
                            isHiddenCallback = () => Properties.LODMode == DebugLodMode.None,
                            getter = () => Properties.LODIndex,
                            setter = value => Properties.LODIndex = value,
                            min = () => 0,
                            max = () => 7
                        },
                    }
                };
            }

            private DebugUI.Widget CreateSpatialHashSettings()
            {
                return new DebugUI.Container
                {
                    displayName = "Culling Grid",
                    isHiddenCallback = () => !FloraSystem.Active,
                    children =
                    {
                        new DebugUI.EnumField
                        {
                            displayName = "Mode",
                            tooltip = "Draw procedural lines for visualizing the Flora culling grid.",
                            autoEnum = typeof(DebugSpatialHashMode),
                            getter = () => (int)Properties.SpatialHashMode,
                            setter = value => Properties.SpatialHashMode = (DebugSpatialHashMode)value,
                            getIndex = () => (int)Properties.SpatialHashMode,
                            setIndex = value => Properties.SpatialHashMode = (DebugSpatialHashMode)value,
                        },
                        new DebugUI.BitField
                        {
                            displayName = "Types",
                            tooltip = "Which elements of the culling grid to visualize.",
                            getter = () => Properties.SpatialHashFlags,
                            setter = value => Properties.SpatialHashFlags = (DebugSpatialHashFlags)value,
                            enumType = typeof(DebugSpatialHashFlags)
                        },
                        new DebugUI.FloatField
                        {
                            displayName = "Draw Distance",
                            tooltip = "The maximum distance at which culling-grid lines are drawn.",
                            getter = () => Properties.SpatialHashMaxDistance,
                            setter = value => Properties.SpatialHashMaxDistance = value,
                            min = () => 10.0f
                        },
                        new DebugUI.MessageBox
                        {
                            displayName = "Color Legend",
                            style = DebugUI.MessageBox.Style.Info,
                            messageCallback = () => Properties.SpatialHashMode == DebugSpatialHashMode.Heatmap
                                ? "Heatmap: cyan indicates lower occupancy; orange, red, and burgundy indicate progressively higher occupancy."
                                : "Level: color identifies the culling-grid level derived from the element size.",
                            isHiddenCallback = () => Properties.SpatialHashMode == DebugSpatialHashMode.Disabled,
                        },
                    }
                };
            }

            private DebugUI.Widget CreateGPUOcclusionSettings()
            {
                return new DebugUI.Container
                {
                    displayName = "GPU Occlusion",
                    isHiddenCallback = () => !FloraSystem.Active,
                    children =
                    {
                        new DebugUI.MessageBox
                        {
                            displayName = "GPU Occlusion Culling Info.",
                            messageCallback = () => FloraSystem.Instance.AllowGPUOcclusionCulling ? string.Empty :
                                "GPU Occlusion Culling is disabled. Please enable it to use these settings.",
                            style = DebugUI.MessageBox.Style.Info,
                            isHiddenCallback = () => FloraSystem.Instance.AllowGPUOcclusionCulling
                        },
                        new DebugUI.BoolField
                        {
                            displayName = "Occlusion Overlay",
                            tooltip = "Enable the occlusion overlay.",
                            getter = () => Properties.OcclusionTestOverlayEnabled,
                            setter = value => Properties.OcclusionTestOverlayEnabled = value,
                            isHiddenCallback = () => !FloraSystem.Instance.AllowGPUOcclusionCulling
                        },
                        new DebugUI.Container
                        {
                            children =
                            {
                                new DebugUI.FloatField
                                {
                                    displayName = "Opacity",
                                    tooltip = "The opacity of the occlusion overlay.",
                                    min = () => 0.0f,
                                    max = () => 1.0f,
                                    getter = () => Properties.OcclusionTestOverlayOpacity,
                                    setter = value => Properties.OcclusionTestOverlayOpacity = value,
                                    isHiddenCallback = () => !Properties.OcclusionTestOverlayEnabled
                                },
                                new DebugUI.BoolField
                                {
                                    displayName = "Count Visible",
                                    tooltip = "Show the number of visible instances in the occlusion overlay.",
                                    getter = () => Properties.OcclusionOverlayCountVisible,
                                    setter = value => Properties.OcclusionOverlayCountVisible = value,
                                    isHiddenCallback = () => !Properties.OcclusionTestOverlayEnabled
                                },
                            },
                            isHiddenCallback = () => !FloraSystem.Instance.AllowGPUOcclusionCulling
                        },
                        new DebugUI.BoolField
                        {
                            displayName = "Depth Overlay",
                            tooltip = "Enable the occluder pyramid debug view.",
                            getter = () => Properties.OccluderDepthOverlayEnabled,
                            setter = value => Properties.OccluderDepthOverlayEnabled = value,
                            isHiddenCallback = () => !FloraSystem.Instance.AllowGPUOcclusionCulling
                        },
                        new DebugUI.Container
                        {
                            children =
                            {
                                new DebugUI.FloatField
                                {
                                    displayName = "Range Min",
                                    isHiddenCallback = () => !Properties.OccluderDepthOverlayEnabled,
                                    tooltip = "The minimum range of the occluder debug view.",
                                    min = () => 0.0f,
                                    max = () => Properties.OcclusionDepthViewRange.y,
                                    getter = () => Properties.OcclusionDepthViewRange.x,
                                    setter = value => Properties.OcclusionDepthViewRange.x = value
                                },
                                new DebugUI.FloatField
                                {
                                    displayName = "Range Max",
                                    isHiddenCallback = () => !Properties.OccluderDepthOverlayEnabled,
                                    tooltip = "The maximum range of the occluder debug view.",
                                    min = () => Properties.OcclusionDepthViewRange.x,
                                    max = () => 1.0f,
                                    getter = () => Properties.OcclusionDepthViewRange.y,
                                    setter = value => Properties.OcclusionDepthViewRange.y = value
                                },
                            },
                            isHiddenCallback = () => !FloraSystem.Instance.AllowGPUOcclusionCulling
                        },
                    }
                };
            }

            private DebugUI.Widget CreateCullingStats()
            {
                var cullingStats = new DebugUI.Foldout
                {
                    displayName = "Culling Stats",
                    isHeader = true,
                    opened = true,
                    isHiddenCallback = () => !Properties.EnableCPUCullingStats && !Properties.EnableGPUCullingStats
                };

                cullingStats.children.Add(new DebugUI.ValueTuple
                {
                    displayName = "View Count",
                    values = new[]
                    {
                        new DebugUI.Value { refreshRate = RefreshRate, formatString = FormatString, getter = () => GetViewStatsCount() }
                    }
                });

                cullingStats.children.Add(new DebugUI.MessageBox
                {
                    displayName = "View Limit",
                    style = DebugUI.MessageBox.Style.Info,
                    messageCallback = () => $"Showing the first {MaxViewCount} of {GetViewStatsCount()} active views.",
                    isHiddenCallback = () => GetViewStatsCount() <= MaxViewCount,
                });


                DebugUI.Table viewTable = new DebugUI.Table
                {
                    displayName = "",
                    isReadOnly = true
                };

                // Always add all possible rows, they are dynamically hidden based on actual data
                for (int i = 0; i < MaxViewCount; i++)
                {
                    viewTable.children.Add(AddViewStatsDataRow(i));
                }

                var perViewStats = new DebugUI.Foldout
                {
                    displayName = "Per View Stats",
                    isHeader = true,
                    opened = false,
                    isHiddenCallback = () => !Properties.EnableCPUCullingStats && !Properties.EnableGPUCullingStats
                };
                perViewStats.children.Add(viewTable);
                cullingStats.children.Add(perViewStats);

                return cullingStats;
            }
        }
    }
}
