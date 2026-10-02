using System.Collections.Generic;
using MrMoonlight.Enemies;
using UnityEditor;
using UnityEngine;

namespace MrMoonlight.EditorTools
{
    /// <summary>
    /// Builds the dissolve material variants for an enemy prefab and wires them into its
    /// <see cref="CorpseDissolve"/> component (MRM-85, corpse dissolve). For every material the prefab's
    /// renderers use (body, lamp, dropped props) it creates or refreshes
    /// <c>&lt;name&gt;_Dissolve.mat</c> next to it: a copy with the RetroLit dissolve feature on and the noise texture
    /// set. Re-run it after changing an enemy's materials. The dissolve look (colors, widths) lives in the
    /// variants; tweak them there, the tool only overwrites them when you run it again.
    /// </summary>
    public static class CorpseDissolveTool
    {
        private const string EnemyPrefab = "Assets/_Project/Prefabs/Enemies/Enemy_Spotter.prefab";
        private const string NoisePath = "Assets/_Project/Art/VFX/Dissolve/Dissolve_Noise.png";

        [MenuItem("Mr. Moonlight/Corpse Dissolve/Rebuild Material Variants")]
        public static void RebuildVariants()
        {
            ConfigureNoise();
            var noise = AssetDatabase.LoadAssetAtPath<Texture2D>(NoisePath);

            GameObject root = PrefabUtility.LoadPrefabContents(EnemyPrefab);
            try
            {
                var sources = new List<Material>();
                foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
                {
                    foreach (Material m in r.sharedMaterials)
                    {
                        if (m != null && !sources.Contains(m)) sources.Add(m);
                    }
                }

                var pairs = new List<CorpseDissolve.MaterialPair>();
                foreach (Material source in sources)
                {
                    string path = AssetDatabase.GetAssetPath(source);
                    if (string.IsNullOrEmpty(path) || !source.HasProperty("_DissolveAmount"))
                    {
                        Debug.LogWarning($"[CorpseDissolve] {source.name} is not a RetroLit material, skipped (it will not burn).");
                        continue;
                    }

                    string variantPath = path.Replace(".mat", "_Dissolve.mat");
                    var variant = AssetDatabase.LoadAssetAtPath<Material>(variantPath);
                    if (variant == null)
                    {
                        variant = new Material(source);
                        AssetDatabase.CreateAsset(variant, variantPath);
                    }
                    else
                    {
                        variant.CopyPropertiesFromMaterial(source);
                    }

                    variant.shader = source.shader;
                    variant.EnableKeyword("_USE_DISSOLVE");
                    variant.SetFloat("_UseDissolve", 1f);
                    variant.SetTexture("_DissolveNoise", noise);
                    variant.SetFloat("_DissolveAmount", 0f);
                    EditorUtility.SetDirty(variant);

                    pairs.Add(new CorpseDissolve.MaterialPair { source = source, dissolve = variant });
                }

                var component = root.GetComponent<CorpseDissolve>();
                if (component == null) component = root.AddComponent<CorpseDissolve>();
                component.SetMaterials(pairs.ToArray());
                EditorUtility.SetDirty(component);

                PrefabUtility.SaveAsPrefabAsset(root, EnemyPrefab);
                AssetDatabase.SaveAssets();
                Debug.Log($"[CorpseDissolve] {pairs.Count} material variants wired into {EnemyPrefab}.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void ConfigureNoise()
        {
            var importer = AssetImporter.GetAtPath(NoisePath) as TextureImporter;
            if (importer == null) return;

            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = false;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.filterMode = FilterMode.Bilinear;
            importer.maxTextureSize = 256;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }
    }
}
