#if UNITY_EDITOR
// AST-301 Foliage Renormalizer, batch pass over the tree prefabs (Carlos, 2026-10-08).
//
// Lives outside Assets/_Project/Code on purpose: the vendor scripts have no asmdef (they compile into
// Assembly-CSharp-Editor) and MrMoonlight.Editor cannot reference that assembly.
//
// What it does: for every prefab in the list it generates an SDF proxy of the alpha-clipped (leaf) submeshes,
// transfers the proxy's normals onto the leaf cards, writes the result as a NEW mesh asset and points the prefab's
// MeshFilter at it. The prefab gets no extra component. Original mesh references are stored in the manifest, so
// Rollback puts every prefab back exactly as it was; Reapply swaps the generated meshes back in without regenerating.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using FoliageRenormalizer;

public static class FoliageRenormalizerBatch
{
    const string OutputFolder = "Assets/_Project/Art/Nature/Renormalized Trees";
    const string ManifestPath = "Assets/_Project/Tools/Editor/FoliageRenormalizerManifest.json";
    const string TreeFolder = "Assets/_Project/Prefabs/Nature/Trees";

    // Tool defaults from the vendor inspector, AO baking off (RetroLit samples only BaseColor + Normal).
    const int VoxelResolution = 128;
    const float Tightness = 0.7f;
    const int BlurIterations = 5;
    const float ProxyNormalInfluence = 1.0f;
    const float UpwardBias = 0.25f;

    [Serializable]
    public class Entry
    {
        public string prefabPath;
        public string rendererPath;
        public string originalMeshGuid;
        public long originalMeshFileId;
        public string originalMeshName;
        public string generatedMeshPath;
        public string submeshesProcessed;
        public float seconds;
        public string status; // applied | skipped-no-cutout | failed
        public string note;
    }

    [Serializable]
    public class Manifest { public List<Entry> entries = new List<Entry>(); }

    static Manifest Load()
    {
        if (!File.Exists(ManifestPath)) return new Manifest();
        return JsonUtility.FromJson<Manifest>(File.ReadAllText(ManifestPath)) ?? new Manifest();
    }

    static void Save(Manifest m)
    {
        File.WriteAllText(ManifestPath, JsonUtility.ToJson(m, true));
        AssetDatabase.ImportAsset(ManifestPath);
    }

    static string PathOf(Transform t, Transform root)
    {
        var parts = new List<string>();
        for (; t != null && t != root; t = t.parent) parts.Add(t.name);
        parts.Reverse();
        return string.Join("/", parts);
    }

    static bool IsCutout(Material m) => m != null && m.HasProperty("_AlphaClip") && m.GetFloat("_AlphaClip") > 0.5f;

    // Same job as the vendor's private BuildTempMaskedMesh.
    static Mesh MaskedCopy(Mesh src, bool[] mask)
    {
        var list = new List<CombineInstance>();
        for (int sm = 0; sm < src.subMeshCount; sm++)
            if (mask[sm]) list.Add(new CombineInstance { mesh = src, subMeshIndex = sm, transform = Matrix4x4.identity });
        if (list.Count == 0) return null;
        var tmp = new Mesh { name = src.name + "_Masked", indexFormat = src.indexFormat };
        tmp.CombineMeshes(list.ToArray(), true, false, false);
        tmp.RecalculateBounds();
        return tmp;
    }

    /// <summary>Prefab paths for every tree prefab currently placed in the open scene, plus anything else in the tree folder if allInFolder.</summary>
    static List<string> CollectPrefabs(bool allInFolder)
    {
        var set = new SortedSet<string>();
        if (allInFolder)
        {
            foreach (var g in AssetDatabase.FindAssets("t:Prefab", new[] { TreeFolder }))
                set.Add(AssetDatabase.GUIDToAssetPath(g));
        }
        else
        {
            foreach (var go in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
            {
                var src = PrefabUtility.GetCorrespondingObjectFromSource(go);
                if (!src) continue;
                var p = AssetDatabase.GetAssetPath(src);
                if (p.StartsWith(TreeFolder + "/")) set.Add(p);
            }
        }
        return set.ToList();
    }

    [MenuItem("Tools/Foliage Renormalizer/Apply to trees in open scene")]
    public static void ApplyOpenScene() => Run(CollectPrefabs(false), int.MaxValue);

    [MenuItem("Tools/Foliage Renormalizer/Apply to ALL prefabs in the Trees folder")]
    public static void ApplyAllInTreeFolder() => Run(CollectPrefabs(true), int.MaxValue);

    // Callable from MCP with a small count to test first.
    public static void ApplyOpenSceneLimited(int max) => Run(CollectPrefabs(false), max);

    static void Run(List<string> prefabs, int max)
    {
        var manifest = Load();
        FolderUtil.EnsureFolders(OutputFolder);
        FoliageRenormalizerUtilityEditor.EnsureSavePathsLoaded();
        FoliageRenormalizerUtilityEditor.SaveToSourceMeshfolder = false;
        FoliageRenormalizerUtilityEditor.FoliageSavePath = OutputFolder;

        var tmpGo = new GameObject("FoliageRenormalizerBatch_tmp") { hideFlags = HideFlags.HideAndDontSave };
        var util = tmpGo.AddComponent<FoliageRenormalizerUtility>();
        util.proxy.voxelResolution = VoxelResolution;
        util.proxy.tightness = Tightness;
        util.proxy.blurIterations = BlurIterations;

        var settings = new FoliageNormalTransfer.Settings
        {
            TransferNormals = true,
            ProxyNormalInfluence = ProxyNormalInfluence,
            upwardBias = UpwardBias,
            BakeAO = false,
            BakeGroundAO = false,
        };

        int done = 0, skipped = 0, failed = 0;
        try
        {
            for (int i = 0; i < prefabs.Count && done < max; i++)
            {
                string path = prefabs[i];
                EditorUtility.DisplayProgressBar("Foliage Renormalizer", path, (float)i / prefabs.Count);
                if (manifest.entries.Any(e => e.prefabPath == path && e.status != "failed")) continue;

                var root = PrefabUtility.LoadPrefabContents(path);
                bool changed = false;
                try
                {
                    foreach (var mf in root.GetComponentsInChildren<MeshFilter>(true))
                    {
                        var mr = mf.GetComponent<MeshRenderer>();
                        var mesh = mf.sharedMesh;
                        if (!mr || !mesh) continue;
                        var e = new Entry { prefabPath = path, rendererPath = PathOf(mf.transform, root.transform), originalMeshName = mesh.name };
                        AssetDatabase.TryGetGUIDAndLocalFileIdentifier(mesh, out e.originalMeshGuid, out e.originalMeshFileId);
                        var sw = System.Diagnostics.Stopwatch.StartNew();
                        try
                        {
                            var mats = mr.sharedMaterials;
                            var mask = new bool[mesh.subMeshCount];
                            for (int s = 0; s < mask.Length; s++) mask[s] = s < mats.Length && IsCutout(mats[s]);
                            if (!mask.Any(b => b))
                            {
                                e.status = "skipped-no-cutout";
                                e.note = "no alpha-clipped submesh, nothing to renormalize";
                                skipped++;
                            }
                            else
                            {
                                var masked = MaskedCopy(mesh, mask);
                                masked.RecalculateBounds();
                                var proxy = SDFProxyBuilder.Build(masked, VoxelResolution, Tightness, BlurIterations);
                                SDFProxyBuilder.PostProcessProxy(proxy, util);
                                if (proxy == null) throw new Exception("proxy generation returned null");

                                util.OriginalFoliageMesh = null; util.RemeshedFoliageMesh = null;
                                FoliageNormalTransfer.Transfer(util, mf, proxy, settings, mask);
                                var generated = util.RemeshedFoliageMesh;
                                if (generated == null) throw new Exception("Transfer produced no mesh");

                                e.generatedMeshPath = AssetDatabase.GetAssetPath(generated);
                                e.submeshesProcessed = string.Join(",", Enumerable.Range(0, mask.Length).Where(k => mask[k]));
                                e.status = "applied";
                                changed = true;
                                done++;
                                UnityEngine.Object.DestroyImmediate(proxy);
                                UnityEngine.Object.DestroyImmediate(masked);
                            }
                        }
                        catch (Exception ex)
                        {
                            e.status = "failed"; e.note = ex.Message; failed++;
                            Debug.LogError($"[FoliageRenormalizerBatch] {path}: {ex}");
                            mf.sharedMesh = mesh; // keep original
                        }
                        e.seconds = (float)sw.Elapsed.TotalSeconds;
                        manifest.entries.Add(e);
                    }
                    if (changed) PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
                Save(manifest);
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
            UnityEngine.Object.DestroyImmediate(tmpGo);
            AssetDatabase.SaveAssets();
            Save(manifest);
        }
        Debug.Log($"[FoliageRenormalizerBatch] applied={done} skipped={skipped} failed={failed} manifest={ManifestPath}");
    }

    static Mesh FindOriginal(Entry e)
    {
        var p = AssetDatabase.GUIDToAssetPath(e.originalMeshGuid);
        if (string.IsNullOrEmpty(p)) return null;
        foreach (var o in AssetDatabase.LoadAllAssetsAtPath(p))
        {
            if (o is Mesh m && AssetDatabase.TryGetGUIDAndLocalFileIdentifier(m, out _, out long id) && id == e.originalMeshFileId)
                return m;
        }
        return null;
    }

    static void Swap(bool toOriginal)
    {
        var manifest = Load();
        int n = 0, bad = 0;
        foreach (var group in manifest.entries.Where(e => e.status == "applied").GroupBy(e => e.prefabPath))
        {
            var root = PrefabUtility.LoadPrefabContents(group.Key);
            try
            {
                foreach (var e in group)
                {
                    var t = string.IsNullOrEmpty(e.rendererPath) ? root.transform : root.transform.Find(e.rendererPath);
                    var mf = t ? t.GetComponent<MeshFilter>() : null;
                    var target = toOriginal ? FindOriginal(e) : AssetDatabase.LoadAssetAtPath<Mesh>(e.generatedMeshPath);
                    if (!mf || !target) { bad++; Debug.LogError($"[FoliageRenormalizerBatch] cannot swap {group.Key} / {e.rendererPath}"); continue; }
                    mf.sharedMesh = target; n++;
                }
                PrefabUtility.SaveAsPrefabAsset(root, group.Key);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        AssetDatabase.SaveAssets();
        Debug.Log($"[FoliageRenormalizerBatch] {(toOriginal ? "ROLLBACK" : "REAPPLY")} swapped={n} problems={bad}");
    }

    [MenuItem("Tools/Foliage Renormalizer/ROLLBACK to original meshes")]
    public static void Rollback() => Swap(true);

    [MenuItem("Tools/Foliage Renormalizer/Re-apply generated meshes (after a rollback)")]
    public static void Reapply() => Swap(false);

    /// <summary>Which mesh each manifest prefab currently uses: original / generated / other.</summary>
    public static string Status()
    {
        var manifest = Load();
        int orig = 0, gen = 0, other = 0;
        foreach (var e in manifest.entries.Where(x => x.status == "applied"))
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(e.prefabPath);
            var t = string.IsNullOrEmpty(e.rendererPath) ? prefab.transform : prefab.transform.Find(e.rendererPath);
            var mesh = t ? t.GetComponent<MeshFilter>()?.sharedMesh : null;
            if (!mesh) { other++; continue; }
            if (AssetDatabase.GetAssetPath(mesh) == e.generatedMeshPath) gen++;
            else if (mesh == FindOriginal(e)) orig++;
            else other++;
        }
        return $"entries={manifest.entries.Count} applied={manifest.entries.Count(x => x.status == "applied")} -> usingGenerated={gen} usingOriginal={orig} other={other}";
    }
}
#endif
