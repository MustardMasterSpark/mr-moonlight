// ----------------------------------------------------
// Copyright (c) Pampel Games e.K. All Rights Reserved.
// https://www.pampelgames.com
// ----------------------------------------------------

using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace PampelGames.Shared.Utility
{
    [AddComponentMenu("Pampel Games/Shared/Reflection Probe Baker")]
    [PGEditorAuto]
    public class ReflectionProbeBaker : MonoBehaviour
    {
        public enum CubemapResolution
        {
            _16 = 16,
            _32 = 32,
            _64 = 64,
            _128 = 128,
            _256 = 256,
            _512 = 512,
            _1024 = 1024,
            _2048 = 2048
        }

        [PGButtonMethod(nameof(AddProbesFromScene), 20f)]
        public string addProbesFromScene;

        public List<ReflectionProbe> reflectionProbes = new();

        public bool overrideResolution;

        [PGDisplaySelection(new[] {nameof(overrideResolution)})]
        public CubemapResolution resolution = CubemapResolution._128;


        [PGButtonMethod(nameof(BakeProbes))] public string bakeProbes;

        public void BakeProbes()
        {
#if UNITY_EDITOR

            var allProbesHaveBakedTextures = reflectionProbes.Count > 0;
            foreach (var probe in reflectionProbes)
                if (!probe || !probe.customBakedTexture)
                {
                    allProbesHaveBakedTextures = false;
                    break;
                }

            string folderPath;
            if (allProbesHaveBakedTextures)
            {
                var existingPath = AssetDatabase.GetAssetPath(reflectionProbes[0].customBakedTexture);
                var directoryPath = Path.GetDirectoryName(existingPath);
                folderPath = Path.Combine(Application.dataPath.Substring(0, Application.dataPath.Length - 6), directoryPath);
            }
            else
            {
                var defaultFolder = "Assets";
                if (reflectionProbes.Count > 0 && reflectionProbes[0] && reflectionProbes[0].customBakedTexture)
                {
                    var existingPath = AssetDatabase.GetAssetPath(reflectionProbes[0].customBakedTexture);
                    if (!string.IsNullOrEmpty(existingPath))
                    {
                        var directoryPath = Path.GetDirectoryName(existingPath);
                        if (!string.IsNullOrEmpty(directoryPath))
                            defaultFolder = Path.Combine(Application.dataPath.Substring(0, Application.dataPath.Length - 6), directoryPath);
                    }
                }

                folderPath = EditorUtility.SaveFolderPanel("Select Export Folder", defaultFolder, "");
                if (string.IsNullOrEmpty(folderPath)) return;

                if (!folderPath.StartsWith(Application.dataPath))
                {
                    EditorUtility.DisplayDialog("Export", "Please select a folder inside the Assets directory.", "OK");
                    return;
                }
            }

            if (!Directory.Exists(folderPath)) Directory.CreateDirectory(folderPath);

            var relativePath = "Assets" + folderPath.Substring(Application.dataPath.Length);

            for (var i = 0; i < reflectionProbes.Count; i++)
            {
                var probe = reflectionProbes[i];

                probe.mode = ReflectionProbeMode.Custom;
                if (overrideResolution) probe.resolution = (int) resolution;

                string exrPath;

                if (probe.customBakedTexture)
                {
                    exrPath = AssetDatabase.GetAssetPath(probe.customBakedTexture);
                }
                else
                {
                    var sceneName = probe.gameObject.scene.name;
                    exrPath = Path.Combine(relativePath, sceneName + "_" + probe.gameObject.name + ".exr");
                }

                Lightmapping.BakeReflectionProbe(probe, exrPath);

                var bakedTexture = AssetDatabase.LoadAssetAtPath<Cubemap>(exrPath);
                if (bakedTexture) probe.customBakedTexture = bakedTexture;

#if UNITY_EDITOR
                EditorUtility.SetDirty(probe);
#endif
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
#endif
        }

        public void AddProbesFromScene()
        {
            var probes = PGObjectUtility.FindObjectsByType<ReflectionProbe>();
            for (var i = 0; i < probes.Length; i++)
                if (!reflectionProbes.Contains(probes[i]))
                    reflectionProbes.Add(probes[i]);

#if UNITY_EDITOR
            EditorUtility.SetDirty(this);
#endif
        }
    }
}