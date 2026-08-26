using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace MrMoonlight.EditorTools
{
    /// <summary>
    /// Restores per-submesh materials on the RetroRealism vegetation prefabs.
    ///
    /// THE BUG
    /// -------
    /// The earlier MRM-70 prefab pass collapsed all three of a tree's submesh slots -
    /// `Trees1` (bark), `Dirt`, `BranchFir` (needles) - onto a single `M_RF_Trees` material, on the
    /// reasoning that the FBX's embedded materials carry no texture references and therefore all
    /// sample the same shared atlas.
    ///
    /// Half of that is true: the embedded materials really are textureless, and the per-tree
    /// `T_RF_Tree1..4_BaseColor.tga` files really are byte-identical hard links of the shared
    /// `T_RF_Trees_BaseColor.tga`. But the conclusion did not follow. `Trees.tga` is the BARK atlas
    /// and is RGB with **no alpha channel at all**; the needles live in a separate 256x256 RGBA
    /// `BranchFir.png` that was never imported.
    ///
    /// Consequence: the branch submesh - which is the overwhelming majority of every tree, 1,060 of
    /// 1,204 triangles on RF_Tree2 - alpha-clipped against a texture with no alpha. Every needle
    /// card came out as a solid opaque quad sampling bark pixels, which is why the trees read as
    /// grey slabs rather than firs.
    ///
    /// THE FIX
    /// -------
    /// Import the missing textures and bind each submesh to its correct material, using the RAW FBX
    /// renderer's own `sharedMaterials` order as the authority. That ordering is the only reliable
    /// source: `AssetDatabase.LoadAllAssetsAtPath` returns embedded materials alphabetically, which
    /// is NOT submesh order and would have bound bark to the needles on every tree.
    ///
    /// Dead variants (`TreesDead`, `BranchFirDead`) are imported and built at the same time - the
    /// eerie forest needs exactly those, and they cost nothing extra to set up here.
    /// </summary>
    public static class VegetationMaterialFix
    {
        private const string MatDir = "Assets/_Project/Art/Environment/Vegetation/RetroRealism/Materials";
        private const string TexDir = "Assets/_Project/Art/Environment/Vegetation/RetroRealism/Textures";
        private const string PrefabDir = "Assets/_Project/Prefabs/World/Vegetation/RetroRealism";
        private const string MeshDir = "Assets/_Project/Art/Environment/Vegetation/RetroRealism/Meshes";

        /// <summary>FBX embedded material name -> our material asset name.</summary>
        private static readonly Dictionary<string, string> Bind = new Dictionary<string, string>
        {
            { "Trees1",       "M_RF_Trees" },      // bark atlas
            { "Log",          "M_RF_Trees" },      // logs/stumps use the same bark atlas
            { "Dirt",         "M_RF_Dirt" },       // small ground patch at the base
            { "BranchFir",    "M_RF_BranchFir" },  // the needles - this is the one that was wrong
            { "Bush1",        "M_RF_Bush" },
            { "Plants",       "M_RF_Fern" },
            { "Material.001", "M_RF_Boulders" },
        };

        [MenuItem("Tools/Mr. Moonlight/Vegetation/Fix Vegetation Materials")]
        public static void FixMenu() => Fix();

        public static string Fix()
        {
            var log = new System.Text.StringBuilder();

            // 1. materials that did not exist before
            EnsureMaterial("M_RF_BranchFir", $"{TexDir}/T_RF_BranchFir_BaseColor.png", alphaClip: true, log);
            EnsureMaterial("M_RF_BranchFirDead", $"{TexDir}/T_RF_BranchFirDead_BaseColor.png", alphaClip: true, log);
            EnsureMaterial("M_RF_Dirt", $"{TexDir}/T_RF_Dirt_BaseColor.tga", alphaClip: false, log);
            EnsureMaterial("M_RF_TreesDead", $"{TexDir}/T_RF_TreesDead_BaseColor.tga", alphaClip: false, log);
            AssetDatabase.SaveAssets();

            // 2. rebind every prefab's submesh slots
            int fixedCount = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { PrefabDir }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null) continue;

                // Authoritative submesh order comes from the raw FBX's renderer, not from
                // LoadAllAssetsAtPath (which sorts alphabetically).
                string fbxPath = $"{MeshDir}/{prefab.name}.fbx";
                var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
                if (fbx == null) { log.AppendLine($"{prefab.name}: no source FBX, skipped"); continue; }

                var srcRend = fbx.GetComponentInChildren<MeshRenderer>();
                if (srcRend == null) continue;
                Material[] srcMats = srcRend.sharedMaterials;

                var target = new Material[srcMats.Length];
                var names = new List<string>();
                bool ok = true;

                for (int i = 0; i < srcMats.Length; i++)
                {
                    string srcName = srcMats[i] != null ? srcMats[i].name : "?";
                    if (!Bind.TryGetValue(srcName, out string matName))
                    {
                        log.AppendLine($"{prefab.name}: UNMAPPED source material '{srcName}' on submesh {i}");
                        ok = false;
                        break;
                    }
                    var mat = AssetDatabase.LoadAssetAtPath<Material>($"{MatDir}/{matName}.mat");
                    if (mat == null) { log.AppendLine($"{prefab.name}: missing {matName}.mat"); ok = false; break; }
                    target[i] = mat;
                    names.Add($"{i}:{srcName}->{matName}");
                }
                if (!ok) continue;

                GameObject inst = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var rend = inst.GetComponent<MeshRenderer>();
                    rend.sharedMaterials = target;
                    PrefabUtility.SaveAsPrefabAsset(inst, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(inst); }

                fixedCount++;
                log.AppendLine($"{prefab.name,-14} {string.Join("  ", names)}");
            }

            AssetDatabase.SaveAssets();
            log.AppendLine($"--- {fixedCount} prefabs rebound ---");
            return log.ToString();
        }

        private static void EnsureMaterial(string name, string texPath, bool alphaClip, System.Text.StringBuilder log)
        {
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
            if (tex == null) { log.AppendLine($"MISSING TEXTURE {texPath} - {name} not created"); return; }

            string path = $"{MatDir}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(mat, path);
                log.AppendLine($"created {name}");
            }

            mat.SetTexture("_BaseMap", tex);
            mat.SetColor("_BaseColor", Color.white);
            mat.SetFloat("_Smoothness", 0.1f);
            mat.SetFloat("_Metallic", 0f);
            mat.enableInstancing = true;               // required for GPU-instanced terrain scatter
            mat.SetFloat("_Cull", 0f);                 // two-sided: these are cards

            if (alphaClip)
            {
                mat.SetFloat("_AlphaClip", 1f);
                mat.SetFloat("_Cutoff", 0.5f);
                mat.EnableKeyword("_ALPHATEST_ON");
                mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
            }
            else
            {
                mat.SetFloat("_AlphaClip", 0f);
                mat.DisableKeyword("_ALPHATEST_ON");
                mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Geometry;
            }

            EditorUtility.SetDirty(mat);
        }
    }
}
