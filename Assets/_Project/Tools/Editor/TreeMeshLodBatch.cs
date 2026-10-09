#if UNITY_EDITOR
// MRM-88 side track: native Unity 6 Mesh LOD for vegetation that has no LODGroup (Carlos, 2026-10-08/09).
//
// Ticks "Generate Mesh LODs" on the FBX importers behind the nature prefabs. Unity's own simplifier writes the extra index ranges
// and the lodSelectionCurve inside the same mesh: no extra GameObjects, no prefab change. Wood colliders are separate assets
// (Art/Nature/Tree Colliders/Wood Meshes) and are not touched. Every importer touched is recorded in the manifest;
// ROLLBACK unticks them again. Nothing is deleted.
// (An earlier version of this file built the lower levels itself by dropping leaf cards; replaced after the importer route proved to work.)
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class TreeMeshLodBatch
{
    static readonly string[] PrefabFolders =
    {
        "Assets/_Project/Prefabs/Nature/Trees",
        "Assets/_Project/Prefabs/Nature/Rocks & Logs",
    };
    const string ManifestPath = "Assets/_Project/Tools/Editor/TreeMeshLodManifest.json";
    const int MinTriangles = 1000; // below this the saving is not worth a LOD chain

    [Serializable]
    public class Entry { public string fbxPath; public string prefabPath; public int lod0Tris; public int levels; public int trisLastLevel; }
    [Serializable] public class Manifest { public List<Entry> entries = new List<Entry>(); }

    static Manifest Load() => File.Exists(ManifestPath) ? (JsonUtility.FromJson<Manifest>(File.ReadAllText(ManifestPath)) ?? new Manifest()) : new Manifest();
    static void Save(Manifest m) { File.WriteAllText(ManifestPath, JsonUtility.ToJson(m, true)); AssetDatabase.ImportAsset(ManifestPath); }

    public static string Run(int max)
    {
        var manifest = Load();
        var report = new System.Text.StringBuilder();
        var seen = new HashSet<string>();
        int done = 0, small = 0, hasLod = 0, notFbx = 0;
        foreach (var g in AssetDatabase.FindAssets("t:Prefab", PrefabFolders))
        {
            if (done >= max) break;
            string path = AssetDatabase.GUIDToAssetPath(g);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab.GetComponentInChildren<LODGroup>(true)) { hasLod++; continue; }
            foreach (var mf in prefab.GetComponentsInChildren<MeshFilter>(true))
            {
                var mesh = mf.sharedMesh;
                if (!mesh) continue;
                string fbx = AssetDatabase.GetAssetPath(mesh);
                if (!fbx.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase)) { notFbx++; continue; }
                if (!seen.Add(fbx)) continue;
                int tris = 0;
                for (int s = 0; s < mesh.subMeshCount; s++) tris += (int)(mesh.GetIndexCount(s) / 3);
                if (tris < MinTriangles) { small++; continue; }
                var imp = AssetImporter.GetAtPath(fbx) as ModelImporter;
                if (imp == null) continue;
                bool already = imp.generateMeshLods;
                if (!already) { imp.generateMeshLods = true; imp.SaveAndReimport(); }
                var m = AssetDatabase.LoadAssetAtPath<Mesh>(fbx);
                // The prefab may reference a different sub-mesh than the first one; read the one the prefab uses.
                var used = AssetDatabase.LoadAllAssetsAtPath(fbx).OfType<Mesh>().FirstOrDefault(x => x.name == mesh.name) ?? m;
                int last = 0;
                if (used.lodCount > 0) for (int s = 0; s < used.subMeshCount; s++) last += (int)(used.GetLods(s)[used.lodCount - 1].indexCount / 3);
                manifest.entries.RemoveAll(x => x.fbxPath == fbx);
                if (!already) manifest.entries.Add(new Entry { fbxPath = fbx, prefabPath = path, lod0Tris = tris, levels = used.lodCount, trisLastLevel = last });
                report.AppendLine($"{used.name}: tris {tris} -> levels {used.lodCount}, last level {last}{(already ? " (was already on, not recorded for rollback)" : "")}");
                done++;
            }
        }
        Save(manifest);
        report.AppendLine($"imported={done} skippedSmall={small} skippedHasLODGroup={hasLod} skippedNotFbx={notFbx}");
        return report.ToString();
    }

    [MenuItem("Tools/Tree Mesh LOD/Apply native Mesh LOD to nature prefabs (>= 1000 tris, no LODGroup)")]
    public static void ApplyAll() { Debug.Log("[TreeMeshLodBatch]\n" + Run(int.MaxValue)); }

    [MenuItem("Tools/Tree Mesh LOD/ROLLBACK (untick Generate Mesh LODs on every recorded FBX)")]
    public static void Rollback()
    {
        var manifest = Load(); int n = 0;
        foreach (var e in manifest.entries)
        {
            var imp = AssetImporter.GetAtPath(e.fbxPath) as ModelImporter;
            if (imp == null || !imp.generateMeshLods) continue;
            imp.generateMeshLods = false; imp.SaveAndReimport(); n++;
        }
        manifest.entries.Clear();
        Save(manifest);
        Debug.Log("[TreeMeshLodBatch] rollback importers=" + n);
    }
}
#endif
