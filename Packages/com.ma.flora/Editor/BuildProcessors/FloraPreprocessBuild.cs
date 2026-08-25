// Copyright © Magnetic Arcade. All Rights Reserved.

using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace MA.Flora.Editor
{
    internal class FloraPreprocessBuild : IPreprocessBuildWithReport, IPostprocessBuildWithReport
    {
        private static FloraBuildData s_BuildData = null;

        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
            s_BuildData?.Dispose();

            bool isDevelopmentBuild = (report.summary.options & BuildOptions.Development) != 0;
            s_BuildData = new FloraBuildData(EditorUserBuildSettings.activeBuildTarget, isDevelopmentBuild);
        }

        public void OnPostprocessBuild(BuildReport report)
        {
            s_BuildData?.Dispose();
            s_BuildData = null;
        }
    }
}
