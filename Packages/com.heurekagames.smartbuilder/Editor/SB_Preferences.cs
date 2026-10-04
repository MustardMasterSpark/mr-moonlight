using System;
using System.IO;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEditor;
using UnityEngine;

namespace HeurekaGames.SmartBuilder
{
    [FilePath(AssetName + "/Preferences.asset", FilePathAttribute.Location.PreferencesFolder)]
    public class SB_Preferences : ScriptableSingleton<SB_Preferences>
    {
        [Serializable]
        public class PreferencesSet<T>
        {
            public T Value
            {
                get
                {
                    return PrefValue;
                }
                protected set
                {
                    this.PrefValue = value;
                }
            }

            [SerializeField] private T PrefValue;
            protected T OriginalValue;
            protected GUIContent GuiContent;
            protected static GUIContent ResetValueContent = new GUIContent("X", "Reset Value");
            protected Action OnChange;

            public PreferencesSet(T defaultValue, GUIContent preferenceText, Action onChange)
            {
                this.OriginalValue = defaultValue;
                this.PrefValue = defaultValue;
                this.GuiContent = preferenceText;
                this.OnChange = onChange;
            }

            protected void TryDrawReset()
            {
                if (IsModified)
                {
                    var origClr = GUI.color;
                    GUI.color = Color.yellow;

                    if (GUILayout.Button(ResetValueContent, GUILayout.ExpandWidth(false)))
                        Reset();

                    GUI.color = origClr;
                }
            }

            public void Reset()
            {
                PrefValue = OriginalValue;
            }

            private bool IsModified => (!Value.Equals(OriginalValue));

            protected void DrawValue(Action action)
            {
                var origFontStyle = EditorStyles.label.fontStyle;
                EditorStyles.label.fontStyle = IsModified ? FontStyle.Bold : FontStyle.Normal;

                EditorGUI.BeginChangeCheck();

                //Draw the specific layout element
                action?.Invoke();

                TryDrawReset();

                if (EditorGUI.EndChangeCheck())
                {
                    OnChange?.Invoke();
                }

                EditorStyles.label.fontStyle = origFontStyle;
            }
        }

        [Serializable]
        public class PreferencesIntSet : PreferencesSet<int>
        {
            public int Min;
            public int Max;

            public PreferencesIntSet(int defaultValue, GUIContent preferenceText, Action onChange, int min, int max) : base(defaultValue, preferenceText, onChange)
            {
                this.Min = min;
                this.Max = max;
            }

            internal void DrawSlider()
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    DrawValue(() =>
                    {
                        Value = EditorGUILayout.IntSlider(GuiContent, Value, Min, Max);
                    });
                }
            }

            internal void IncreaseBy(int delta)
            {
                Value = Mathf.Clamp((Value + delta), Min, Max);
            }
        }

        [Serializable]
        public class PreferencesBoolSet : PreferencesSet<bool>
        {
            public PreferencesBoolSet(bool defaultValue, GUIContent preferenceText, Action onChange) : base(defaultValue, preferenceText, onChange) { }

            internal void DrawToggle()
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    DrawValue(() =>
                    {
                            Value = EditorGUILayout.Toggle(GuiContent, Value);
                    });
                }
            }

            internal void DrawToggle(int width)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    DrawValue(() =>
                    {
                        Value = EditorGUILayout.Toggle(GuiContent, Value, GUILayout.Width(width));
                    });
                }
            }
        }

        [Serializable]
        public class PreferencesEnumSet<T> : PreferencesSet<Enum> where T:Enum
        {
            public PreferencesEnumSet(Enum defaultValue, GUIContent preferenceText, Action onChange) : base(defaultValue, preferenceText, onChange) { }

            internal void DrawEnum()
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    DrawValue(() =>
                    {
                        Value = (T)EditorGUILayout.EnumPopup(GuiContent, Value);
                    });
                }
            }
        }

        [Serializable]
        public class PreferencesColorSet : PreferencesSet<Color>
        {
            public PreferencesColorSet(Color defaultValue, GUIContent preferenceText, Action onChange) : base(defaultValue, preferenceText, onChange) { }

            internal void DrawColor()
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    DrawValue(() =>
                    {
                        Value = EditorGUILayout.ColorField(GuiContent, Value);
                    });
                }
            }
        }

        [Serializable]
        public class PreferencesQuaternionSet : PreferencesSet<Quaternion>
        {
            public PreferencesQuaternionSet(Quaternion defaultValue, GUIContent preferenceText, Action onChange) : base(defaultValue, preferenceText, onChange) { }

            internal void DrawQuaternion()
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    DrawValue(() =>
                    {
                        Value = Quaternion.Euler(EditorGUILayout.Vector3Field(GuiContent, Value.eulerAngles));
                    });
                }
            }
        }

        [Serializable]
        public class PreferencesFloatSet : PreferencesSet<float>
        {
            public float Min;
            public float Max;

            public PreferencesFloatSet(float defaultValue, GUIContent preferenceText, Action onChange, float min, float max) : base(defaultValue, preferenceText, onChange)
            {
                this.Min = min;
                this.Max = max;
            }

            internal void DrawSlider()
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    DrawValue(() =>
                    {
                        if(GuiContent!=null)
                            Value = EditorGUILayout.Slider(GuiContent, Value, Min, Max);
                        else
                            Value = EditorGUILayout.Slider(Value, Min, Max);
                    });
                }
            }

            internal void DrawFloat()
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    DrawValue(() =>
                    {
                        Value = EditorGUILayout.FloatField(GuiContent, Value);
                    });
                }
            }
        }

        public enum ValidAnchors
        {
            UpperLeft = TextAnchor.UpperLeft,
            MiddleLeft = TextAnchor.MiddleLeft,
            LowerLeft = TextAnchor.LowerLeft,
            UpperCenter = TextAnchor.UpperCenter,
            LowerCenter = TextAnchor.LowerCenter,
            UpperRight = TextAnchor.UpperRight,
            MiddleRight = TextAnchor.MiddleRight,
            LowerRight = TextAnchor.LowerRight
        }

        public const string AssetName = "SmartBuilder";
        public const string AssetNameShort = "SmartBuilder";

        [SerializeField] private bool initialized;


        [SerializeField]
        public PreferencesIntSet PreviewImageSize = new PreferencesIntSet(100,
            new GUIContent("Preview image size", "Select the size of the preview images"),
            ()=> instance.SetNewValue(false),
            64, 128);

        [SerializeField]
        public PreferencesIntSet SimilarItemFontSize = new PreferencesIntSet(8,
            new GUIContent("Preview font size", "Select the size of the preview font"),
            () => instance.SetNewValue(false),
            6, 12);

        [SerializeField]
        public PreferencesIntSet ThreadChunkSize = new PreferencesIntSet(1000,
            new GUIContent("Thread chunk size", "How many many assets to we want each thread to process (Smaller = faster, but can affect slow down editor performance)"),
            () => instance.SetNewValue(false),
            50, 5000);

        [SerializeField]
        public PreferencesBoolSet AlwaysShowFileName = new PreferencesBoolSet(false,
            new GUIContent("Show filenames", "Always show filenames inside window"),
            () => instance.SetNewValue(false));

        [SerializeField]
        public PreferencesBoolSet UseCustomPreview = new PreferencesBoolSet(false,
            new GUIContent("Use Custom Preview", "Customize the previews (lighting, camera position etc)"),
            () => instance.SetNewValue(false));

        [SerializeField]
        public PreferencesBoolSet SimilarByTags = new PreferencesBoolSet(true,
            new GUIContent("Metrics: Tags", "Allow tags to influence similarity metrics"),
            () => instance.SetNewValue(true));

        [SerializeField]
        public PreferencesBoolSet SimilarByLabels = new PreferencesBoolSet(true,
            new GUIContent("Metrics: Labels", "Allow unity labels to influence similarity metrics"),
            () => instance.SetNewValue(true));

        [SerializeField]
        public PreferencesBoolSet SimilarByFileName = new PreferencesBoolSet(true,
            new GUIContent("Metrics: Filename", "Allow filename to influence similarity metrics (A bit slow)"),
            () => instance.SetNewValue(true));

        [SerializeField]
        public PreferencesBoolSet SimilarByDirectory = new PreferencesBoolSet(true,
            new GUIContent("Metrics: Directory", "Allow directory to influence similarity metrics (A bit slow)"),
            () => instance.SetNewValue(true));

        [SerializeField]
        public PreferencesBoolSet AutoPingSelection = new PreferencesBoolSet(false,
            new GUIContent("Auto ping selected", "Auto ping assets when selected in window"),
            () => instance.SetNewValue(false));

        [SerializeField]
        public PreferencesBoolSet AutoRefreshOnProjectChange = new PreferencesBoolSet(true,
            new GUIContent("Auto refresh", "Auto refresh cache when project changes"),
            () => instance.SetNewValue(false));

#if UNITY_2021_1_OR_NEWER
        [SerializeField]
        public PreferencesBoolSet ShowSceneViewHelper = new PreferencesBoolSet(true,
            new GUIContent("Sceneview helper", "Show nested helper inside sceneview"),
            () => instance.SetNewValue(false));

        [SerializeField]
        public PreferencesBoolSet ShowSceneViewHelperHeader = new PreferencesBoolSet(true,
            new GUIContent("Show header", "Show the header inside the sceneview helper"),
            () => instance.SetNewValue(false));


        [SerializeField]
        public PreferencesEnumSet<ValidAnchors> SceneViewAnchor = new PreferencesEnumSet<ValidAnchors>(ValidAnchors.UpperCenter,
            new GUIContent("Anchor", "Choose helper window anchor position"),
            () => instance.SetNewValue(false));
#endif

        [SerializeField]
        public PreferencesIntSet MaxSimilarItems = new PreferencesIntSet(100,
            new GUIContent("Max similar items", "How many similar assets to maximally find"),
            () => instance.SetNewValue(false, () => SB_PreviewCacheBuilder.UpdateCacheSize()),
            20, 200);

        [SerializeField]
        public PreferencesColorSet SelectionColor = new PreferencesColorSet(new Color(0.73f, 0.73f, .84f, 1f),
            new GUIContent("Selection tint color", "Choose color to tint selection with"),
            () => instance.SetNewValue(false));

        [SerializeField]
        public PreferencesColorSet PreviewColor = new PreferencesColorSet(Color.black,
            new GUIContent("Preview background color", "Choose color to use as preview background color"),
            () => instance.SetNewValue(false, () => SB_PreviewCacheBuilder.SetPreviewsDirty()));

        [SerializeField]
        public PreferencesFloatSet PreviewLightIntensity = new PreferencesFloatSet(0.6f,
            new GUIContent("Preview lighting intensity", "Set the lighting intensity for the preview renders"),
            () => instance.SetNewValue(false, () => SB_PreviewCacheBuilder.SetPreviewsDirty()),
            0.0f, 3f);
        

        [SerializeField]
        public PreferencesQuaternionSet PreviewCameraRotation = new PreferencesQuaternionSet(Quaternion.Euler(25f, 235f, 0f),
            new GUIContent("Preview camera angle", "Choose a camerarotation for the previews"),
            () => instance.SetNewValue(false, () => SB_PreviewCacheBuilder.SetPreviewsDirty()));


        [SerializeField]
        public PreferencesFloatSet MetricLabelRequirement = new PreferencesFloatSet(.6f, null, () => instance.SetNewValue(true), .1f, 1f);

        [SerializeField]
        public  PreferencesFloatSet MetricLabelWeight = new PreferencesFloatSet(1f, null, () => instance.SetNewValue(true), .1f, 1f);

        [SerializeField]
        public PreferencesFloatSet MetricTagRequirement = new PreferencesFloatSet(.6f, null, () => instance.SetNewValue(true), .1f, 1f);

        [SerializeField]
        public PreferencesFloatSet MetricTagWeight = new PreferencesFloatSet(.4f, null, () => instance.SetNewValue(true), .1f, 1f);

        [SerializeField]
        public PreferencesFloatSet MetricFilenameRequirement = new PreferencesFloatSet(.45f, null, () => instance.SetNewValue(true), .1f, 1f);

        [SerializeField]
        public PreferencesFloatSet MetricFilenameWeight = new PreferencesFloatSet(1f, null, () => instance.SetNewValue(true), .1f, 1f);

        [SerializeField]
        public PreferencesFloatSet MetricDirectoryRequirement = new PreferencesFloatSet(.55f, null, () => instance.SetNewValue(true), .1f, 1f);

        [SerializeField]
        public PreferencesFloatSet MetricDirectoryWeight = new PreferencesFloatSet(.75f, null, () => instance.SetNewValue(true), .1f, 1f);

        [SerializeField] private ObservableStringList ignoredTags = new ObservableStringList();
        public static GUIContent IgnoredTagsContent = new GUIContent("Ignored tags", "The tags that is currently ignored (Only applies if selected asset contains them)");

        public ObservableStringList IgnoredTags { get => ignoredTags; set => ignoredTags = value; }
       
        internal void DoGUI()
        {
            InitializeIfNeeded();

            using (new EditorGUILayout.VerticalScope(GUILayout.ExpandWidth(false)))
            {
                using (new EditorGUILayout.VerticalScope("box"))
                {
                    EditorGUILayout.LabelField("Styling", EditorStyles.boldLabel);
                    PreviewImageSize.DrawSlider();

                    SelectionColor.DrawColor();
                    UseCustomPreview.DrawToggle();

                    AlwaysShowFileName.DrawToggle();

                    SimilarItemFontSize.DrawSlider();
                }

                if (UseCustomPreview.Value)
                {
                    using (new EditorGUILayout.VerticalScope("box"))
                    {
                        EditorGUILayout.LabelField("Custom Aset Preview Settings", EditorStyles.boldLabel);

                        PreviewColor.DrawColor();
                        PreviewCameraRotation.DrawQuaternion();
                        PreviewLightIntensity.DrawSlider();
                    }
                }

                using (new EditorGUILayout.VerticalScope("box"))
                {
                    EditorGUILayout.LabelField("Similarity comparison metrics", EditorStyles.boldLabel);
                    EditorGUILayout.HelpBox("Sets weights and requirements for each metric in search algorithm", MessageType.Info);

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        int curColumn = 0;
                        while (curColumn <= 2)
                        {
                            using (new EditorGUILayout.VerticalScope(GUILayout.ExpandWidth(false)))
                            {
                                switch (curColumn)
                                {
                                    case 0:
                                        EditorGUILayout.LabelField("", GUILayout.Width(1));
                                        SimilarByTags.DrawToggle();
                                        SimilarByLabels.DrawToggle();
                                        SimilarByDirectory.DrawToggle();
                                        SimilarByFileName.DrawToggle();
                                        break;
                                    case 1:
                                        EditorGUILayout.LabelField(new GUIContent("Weight", "How important is this particular metric for the search result"));
                                        drawMetricsSlider(MetricTagWeight, SimilarByTags.Value);
                                        drawMetricsSlider(MetricLabelWeight, SimilarByLabels.Value);
                                        drawMetricsSlider(MetricDirectoryWeight, SimilarByDirectory.Value);
                                        drawMetricsSlider(MetricFilenameWeight, SimilarByFileName.Value);
                                        EditorGUILayout.Space();
                                        break;
                                    case 2:
                                        EditorGUILayout.LabelField(new GUIContent("Required", "Minimum value for this metric to be seen as a match"));
                                        drawMetricsSlider(MetricTagRequirement, SimilarByTags.Value);
                                        drawMetricsSlider(MetricLabelRequirement, SimilarByLabels.Value);
                                        drawMetricsSlider(MetricDirectoryRequirement, SimilarByDirectory.Value);
                                        drawMetricsSlider(MetricFilenameRequirement, SimilarByFileName.Value);
                                        break;
                                }
                                curColumn++;
                            }
                        }
                    }
                }

                SB_GUIElementTags.Draw();


                using (new EditorGUILayout.VerticalScope("box"))
                {
                    EditorGUILayout.LabelField("Functionality", EditorStyles.boldLabel);
                    MaxSimilarItems.DrawSlider();
                    ThreadChunkSize.DrawSlider();

                    AutoPingSelection.DrawToggle();
                    AutoRefreshOnProjectChange.DrawToggle();
                }
#if UNITY_2021_1_OR_NEWER
                using (new EditorGUILayout.VerticalScope("box"))
                {
                    EditorGUILayout.LabelField("Default Prefab Parent Tooling", EditorStyles.boldLabel);
                    ShowSceneViewHelper.DrawToggle();
                    if (ShowSceneViewHelper.Value)
                    {
                        using (var change = new EditorGUI.ChangeCheckScope())
                        {
                            EditorGUILayout.BeginHorizontal();
                            EditorGUI.indentLevel++;
                            ShowSceneViewHelperHeader.DrawToggle();
                            EditorGUILayout.EndHorizontal();
                            EditorGUILayout.BeginHorizontal();
                            SceneViewAnchor.DrawEnum();
                            EditorGUI.indentLevel--;
                            EditorGUILayout.EndHorizontal();

                            if (change.changed)
                            {
                                SceneView.lastActiveSceneView.Repaint();
                            }
                        }
                    }
                }
#endif
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Reset all settings"))
                    reset();
            }
        }

        private void drawMetricsSlider(PreferencesFloatSet metric, bool enabled)
        {
            using (new EditorGUI.DisabledScope(!enabled))
            {
                metric.DrawSlider();
            }
        }

        public void InitializeIfNeeded()
        {
            if (!initialized)
                initialize();
        }
        private void reset()
        {
            initialize();

            var window = forceRepaint();

            if (window != null)
                window.ForceSelectionChange(true);
        }
        private void initialize()
        {
            PreviewImageSize.Reset();
            SimilarByTags.Reset();
            SimilarByLabels.Reset();
            SimilarByFileName.Reset();
            SimilarByDirectory.Reset();

            MetricDirectoryRequirement.Reset();
            MetricFilenameRequirement.Reset();
            MetricLabelRequirement.Reset();
            MetricTagRequirement.Reset();

            MetricDirectoryWeight.Reset();
            MetricFilenameWeight.Reset();
            MetricLabelWeight.Reset();
            MetricTagWeight.Reset();

            AutoPingSelection.Reset();
            AutoRefreshOnProjectChange.Reset();
            AlwaysShowFileName.Reset();

            MaxSimilarItems.Reset();
            SimilarItemFontSize.Reset();
            ThreadChunkSize.Reset();

#if UNITY_2021_1_OR_NEWER
            ShowSceneViewHelper.Reset();
            ShowSceneViewHelperHeader.Reset();

            SceneViewAnchor.Reset();
#endif

            SelectionColor.Reset();

            UseCustomPreview.Reset();
            PreviewCameraRotation.Reset();
            PreviewColor.Reset();
            PreviewLightIntensity.Reset();

            initialized = true;

            Save(true);
        }
        public void SetNewValue(bool requestUpdate, Action callback = null)
        {
                if (requestUpdate)
                    forceSelectionChange();
                else
                    forceRepaint();

                Save(true);
                callback?.Invoke();
        }
        private void forceSelectionChange()
        {
            var window = forceRepaint();

            if (window != null)
                window.ForceSelectionChange(true);
        }
        private SB_Window forceRepaint()
        {
            var buildSettingsType = typeof(SB_Window);
            var windows = Resources.FindObjectsOfTypeAll(buildSettingsType);
            if (windows != null && windows.Length > 0)
            {
                var window = (SB_Window)windows[0];
                if (window)
                    window.Repaint();

                return window;
            }

            return null;
        }
        internal void ForceSave()
        {
            Save(true);
        }
    }

    // Register a SettingsProvider using IMGUI for the drawing framework:
    static class SB_SettingsIMGUIRegister
    {
        [SettingsProvider]
        public static SettingsProvider CreateMyCustomSettingsProvider()
        {
            SB_Preferences.instance.InitializeIfNeeded();

            // First parameter is the path in the Settings window.
            // Second parameter is the scope of this setting: it only appears in the Project Settings window.
            var provider = new SettingsProvider("Preferences/" + SB_Preferences.AssetName, SettingsScope.User)
            {
                // By default the last token of the path is used as display name if no label is provided.
                label = SB_Preferences.AssetName,
                // Create the SettingsProvider and initialize its drawing (IMGUI) function in place:
                guiHandler = (searchContext) =>
                {
                    SB_Preferences.instance.DoGUI(); //GUILayoutUtility.GetLastRect().width);
                },

                // Populate the search keywords to enable smart search filtering and label highlighting:
                keywords = new HashSet<string>(new[] {
                    nameof(SB_Preferences.instance.PreviewImageSize),
                    nameof(SB_Preferences.instance.SimilarByDirectory),
                    nameof(SB_Preferences.instance.SimilarByFileName),
                    nameof(SB_Preferences.instance.SimilarByLabels),
                    nameof(SB_Preferences.instance.SimilarByTags),
                    nameof(SB_Preferences.instance.MaxSimilarItems),
                    nameof(SB_Preferences.instance.SimilarItemFontSize),
                    nameof(SB_Preferences.instance.SelectionColor),
                    nameof(SB_Preferences.instance.AutoPingSelection),
                    nameof(SB_Preferences.instance.AutoRefreshOnProjectChange)
                })

            };
            return provider;
        }
    }

    //Have to wrap my list in order to trigger save when it has been changed
    [System.Serializable]
    public class ObservableStringList
    {
        [SerializeField] protected List<string> entries = new List<string>();
        public ReadOnlyCollection<string> Entries { get => entries.AsReadOnly(); }
        public void Add(string item)
        {
            entries.Add(item);
            SB_Preferences.instance.ForceSave();
        }
        public void Remove(string item)
        {
            entries.Remove(item);
            SB_Preferences.instance.ForceSave();
        }
        internal void Clear()
        {
            entries.Clear();
            SB_Preferences.instance.ForceSave();
        }
        internal void RemoveMany(List<string> list)
        {
            entries.RemoveAll(x => list.Contains(x));
            SB_Preferences.instance.ForceSave();
        }
        internal bool Contains(string item)
        {
            return Entries.Contains(item);
        }
        internal void RemoveRange(int index, int count)
        {
            entries.RemoveRange(index, count);
        }
    }

    //Copy from unity 2020 sourcecode in order to have everything work with 2019 too
#if UNITY_2019
    // Use the FilePathAttribute when you want to have your scriptable singleton to persist between unity sessions.
    // Example: [FilePathAttribute("Library/SearchFilters.ssf", FilePathAttribute.Location.ProjectFolder)]
    // Ensure to call Save() from client code (derived instance)
    [AttributeUsage(AttributeTargets.Class)]
    public class FilePathAttribute : Attribute
    {
        public enum Location { PreferencesFolder, ProjectFolder }

        private string m_FilePath;
        private string m_RelativePath;
        private Location m_Location;

        internal string filepath
        {
            get
            {
                if (m_FilePath == null && m_RelativePath != null)
                {
                    m_FilePath = CombineFilePath(m_RelativePath, m_Location);
                    m_RelativePath = null;
                }

                return m_FilePath;
            }
        }

        public FilePathAttribute(string relativePath, Location location)
        {
            if (string.IsNullOrEmpty(relativePath))
            {
                throw new ArgumentException("Invalid relative path (it is empty)");
            }

            m_RelativePath = relativePath;
            m_Location = location;
        }

        static string CombineFilePath(string relativePath, Location location)
        {
            // We do not want a slash as first char
            if (relativePath[0] == '/')
                relativePath = relativePath.Substring(1);

            switch (location)
            {
                case Location.PreferencesFolder: return UnityEditorInternal.InternalEditorUtility.unityPreferencesFolder + "/" + relativePath;
                case Location.ProjectFolder: return relativePath;
                default:
                    Debug.LogError("Unhandled enum: " + location);
                    return relativePath; // fallback to ProjectFolder relative path
            }
        }
    }

    public class ScriptableSingleton<T> : ScriptableObject where T : ScriptableObject
    {
        static T s_Instance;

        public static T instance
        {
            get
            {
                if (s_Instance == null)
                    CreateAndLoad();

                return s_Instance;
            }
        }

        // On domain reload ScriptableObject objects gets reconstructed from a backup. We therefore set the s_Instance here
        protected ScriptableSingleton()
        {
            if (s_Instance != null)
            {
                Debug.LogError("ScriptableSingleton already exists. Did you query the singleton in a constructor?");
            }
            else
            {
                object casted = this;
                s_Instance = casted as T;
                System.Diagnostics.Debug.Assert(s_Instance != null);
            }
        }

        private static void CreateAndLoad()
        {
            System.Diagnostics.Debug.Assert(s_Instance == null);

            // Load
            string filePath = GetFilePath();
            if (!string.IsNullOrEmpty(filePath))
            {
                // If a file exists the
                UnityEditorInternal.InternalEditorUtility.LoadSerializedFileAndForget(filePath);
            }

            if (s_Instance == null)
            {
                // Create
                T t = CreateInstance<T>();
                t.hideFlags = HideFlags.HideAndDontSave;
            }

            System.Diagnostics.Debug.Assert(s_Instance != null);
        }

        protected virtual void Save(bool saveAsText)
        {
            if (s_Instance == null)
            {
                Debug.LogError("Cannot save ScriptableSingleton: no instance!");
                return;
            }

            string filePath = GetFilePath();
            if (!string.IsNullOrEmpty(filePath))
            {
                string folderPath = Path.GetDirectoryName(filePath);
                if (!Directory.Exists(folderPath))
                    Directory.CreateDirectory(folderPath);

                UnityEditorInternal.InternalEditorUtility.SaveToSerializedFileAndForget(new[] { s_Instance }, filePath, saveAsText);
            }
        }

        protected static string GetFilePath()
        {
            Type type = typeof(T);
            object[] attributes = type.GetCustomAttributes(true);
            foreach (object attr in attributes)
            {
                if (attr is FilePathAttribute)
                {
                    FilePathAttribute f = attr as FilePathAttribute;
                    return f.filepath;
                }
            }
            return string.Empty;
        }
    }
#endif
}