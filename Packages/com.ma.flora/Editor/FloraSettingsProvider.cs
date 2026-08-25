// Copyright © Magnetic Arcade. All Rights Reserved.

using UnityEditor;
using UnityEditor.SettingsManagement;

namespace MA.Flora.Editor
{
    internal static class FloraSettingsManager
    {
        private static Settings s_Instance;

        public static Settings Instance
        {
            get
            {
                if (s_Instance == null)
                {
                    s_Instance = new Settings("com.ma.flora", "Flora Settings");
                    s_Instance.afterSettingsSaved += FloraProjectSettings.OnSettingsSaved;
                }

                return s_Instance;
            }
        }

        public static T Get<T>(string key, SettingsScope scope = SettingsScope.Project, T fallback = default(T))
        {
            return Instance.Get(key, scope, fallback);
        }

        public static void Set<T>(string key, T value, SettingsScope scope = SettingsScope.Project)
        {
            Instance.Set(key, value, scope);
        }

        public static bool ContainsKey<T>(string key, SettingsScope scope = SettingsScope.Project)
        {
            return Instance.ContainsKey<T>(key, scope);
        }
    }

    internal static class FloraSettingsProvider
    {
        [SettingsProvider]
        private static SettingsProvider CreateSettingsProvider()
        {
            var provider = new UserSettingsProvider(
                "Project/Flora",
                FloraSettingsManager.Instance,
                new[] { typeof(FloraSettingsManager).Assembly },
                SettingsScope.Project)
            {
                keywords = new[] { "Flora", "Instancing" }
            };

            return provider;
        }
    }

    internal class FloraSetting<T> : UserSetting<T>
    {
        public FloraSetting(string key, T value, SettingsScope scope = SettingsScope.Project)
            : base(FloraSettingsManager.Instance, key, value, scope)
        {}

        private FloraSetting(Settings settings, string key, T value, SettingsScope scope = SettingsScope.Project)
            : base(settings, key, value, scope) { }
    }

    internal static class FloraProjectSettings
    {
        private static string BuildTargetKey => EditorUserBuildSettings.selectedStandaloneTarget.ToString();

        [UserSetting("Editor",
            "Disable ShaderGraph Injection",
            "Disables automatic patching of ShaderGraph shaders for Flora instancing. " +
            "Disabling this will require you to use the FloraInstancingNode in ShaderGraph. Place it before the vertex position slot.")]
        private static readonly FloraSetting<bool> AutoInjectionSetting = new("ShaderGraph.DisableShaderGraphInjection", false);

        public static bool DisableShaderGraphInjection => AutoInjectionSetting.value;

        public static void OnSettingsSaved()
        {
            FloraDefines.UpdateProjectDefines();
        }
    }
}
