// Copyright © Magnetic Arcade. All Rights Reserved.

using UnityEditor;
using UnityEditor.Build;

namespace MA.Flora.Editor
{
    internal static class FloraDefines
    {
        public const string DisableShaderGraphInjection = "FLORA_DISABLE_SHADER_GRAPH_INJECTION";

        public static void UpdateProjectDefines()
        {
            if (FloraProjectSettings.DisableShaderGraphInjection)
                AddDefine(DisableShaderGraphInjection);
            else
                RemoveDefine(DisableShaderGraphInjection);
        }

        private static bool AddDefine(string newDefine)
        {
            if (string.IsNullOrEmpty(newDefine))
                return false;

            BuildTargetGroup buildTargetGroup = EditorUserBuildSettings.selectedBuildTargetGroup;
            string defines = GetTargetDefines();

            if (!defines.Contains(newDefine))
            {
                if (!string.IsNullOrEmpty(defines))
                    defines += ";";

                defines += newDefine;
                SetTargetDefines(buildTargetGroup, defines);
                return true;
            }

            return false;
        }

        private static bool RemoveDefine(string defineToRemove)
        {
            if (string.IsNullOrEmpty(defineToRemove))
                return false;

            BuildTargetGroup buildTargetGroup = EditorUserBuildSettings.selectedBuildTargetGroup;
            string defines = GetTargetDefines();

            if (defines.Contains(defineToRemove))
            {
                defines = defines.Replace(defineToRemove, "");
                SetTargetDefines(buildTargetGroup, defines);
                return true;
            }

            return false;
        }

        private static string GetTargetDefines()
        {
            BuildTargetGroup buildTargetGroup = EditorUserBuildSettings.selectedBuildTargetGroup;
            return PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.FromBuildTargetGroup(buildTargetGroup));
        }

        private static void SetTargetDefines(BuildTargetGroup targetGroup, string defines)
        {
            PlayerSettings.SetScriptingDefineSymbols(NamedBuildTarget.FromBuildTargetGroup(targetGroup), defines);
        }
    }
}
