#if UNITY_EDITOR
// MRM-88 side track: MeshFusion Pro (AST-146) on the island, scene 07 (Carlos, 2026-10-08, build 51).
//
// What Apply does, on the ACTIVE scene only (it never saves the scene; the caller saves):
//   1. finds every painted tree: a MeshRenderer under "Gaia Terrains" that has a WoodCollider child (8,786 in scene 07);
//   2. ticks Read/Write on the FBX importer behind each mesh (MeshFusion refuses an unreadable mesh);
//   3. clears the Batching Static flag if it was set (none in scene 07, kept for safety);
//   4. (AST-145 was removed on 2026-10-09; this step used to disable its DC_SourceSettings component, now a no-op);
//   5. adds a StaticMeshFusionSource to the tree's renderer object and one RuntimeMeshFusion controller ("MeshFusion Island") to the scene.
// WoodCollider children are not touched. MeshFusion was tried and dropped (builds 51-52, worth ~2%); kept as a harmless editor tool.
// ROLLBACK removes the sources and the controller and restores Read/Write from the manifest.
using System;
using System.Collections.Generic;
using System.IO;
using NGS.MeshFusionPro;
using UnityEditor;
using UnityEngine;

public static class MeshFusionIsland
{
    const string TreeRootName = "Gaia Terrains";
    const int CellSize = 80;        // world units per merge cell (MeshFusion default, same as the gallery pilot)
    const string ControllerName = "MeshFusion Island";
    const string ManifestPath = "Assets/_Project/Tools/Editor/MeshFusionIslandManifest.json";

    [Serializable] public class FbxEntry { public string path; public bool wasReadable; }
    [Serializable] public class Manifest { public List<FbxEntry> fbx = new List<FbxEntry>(); }

    static Manifest Load() => File.Exists(ManifestPath) ? (JsonUtility.FromJson<Manifest>(File.ReadAllText(ManifestPath)) ?? new Manifest()) : new Manifest();
    static void Save(Manifest m) { File.WriteAllText(ManifestPath, JsonUtility.ToJson(m, true)); AssetDatabase.ImportAsset(ManifestPath); }

    static List<MeshRenderer> Candidates()
    {
        var list = new List<MeshRenderer>();
        var root = GameObject.Find(TreeRootName);
        if (root == null) return list;
        foreach (var mr in root.GetComponentsInChildren<MeshRenderer>(true))
        {
            var mf = mr.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) continue;
            if (mr.transform.Find("WoodCollider") == null && mr.GetComponent<StaticMeshFusionSource>() == null) continue;
            list.Add(mr);
        }
        return list;
    }

    [MenuItem("Tools/MeshFusion Island/Apply to scene 07 trees (scene not saved)")]
    public static void Apply() { Debug.Log(Run()); }

    public static string Run()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (!scene.name.StartsWith("07")) return "Refused: active scene is '" + scene.name + "', this tool is for scene 07 only.";
        var manifest = Load();
        var cands = Candidates();

        var seen = new HashSet<string>();
        foreach (var f in manifest.fbx) seen.Add(f.path);
        int fbxChanged = 0, notFbx = 0;
        AssetDatabase.StartAssetEditing();
        try
        {
            foreach (var mr in cands)
            {
                string path = AssetDatabase.GetAssetPath(mr.GetComponent<MeshFilter>().sharedMesh);
                if (!path.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase)) { notFbx++; continue; }
                if (!seen.Add(path)) continue;
                var imp = AssetImporter.GetAtPath(path) as ModelImporter;
                if (imp == null) continue;
                manifest.fbx.Add(new FbxEntry { path = path, wasReadable = imp.isReadable });
                if (!imp.isReadable) { imp.isReadable = true; imp.SaveAndReimport(); fbxChanged++; }
            }
        }
        finally { AssetDatabase.StopAssetEditing(); }
        AssetDatabase.Refresh();
        Save(manifest);

        int added = 0, skippedUnreadable = 0;
        foreach (var mr in cands)
        {
            var go = mr.gameObject;
            if (!mr.GetComponent<MeshFilter>().sharedMesh.isReadable) { skippedUnreadable++; continue; }
            if (go.GetComponent<StaticMeshFusionSource>() != null) continue;
            var flags = GameObjectUtility.GetStaticEditorFlags(go);
            if ((flags & StaticEditorFlags.BatchingStatic) != 0) GameObjectUtility.SetStaticEditorFlags(go, flags & ~StaticEditorFlags.BatchingStatic);
            var src = go.AddComponent<StaticMeshFusionSource>();
            src.CheckCompatibility();
            added++;
            EditorUtility.SetDirty(go);
        }

        var ctrlGo = GameObject.Find(ControllerName);
        if (ctrlGo == null)
        {
            ctrlGo = new GameObject(ControllerName);
            var ctrl = ctrlGo.AddComponent<RuntimeMeshFusion>();
            ctrl.CellSize = CellSize;
            ctrl.LimitVertices = true;
            ctrl.MeshType = MeshType.Standard;
            ctrl.MoveMethod = MoveMethod.Jobs;
            EditorUtility.SetDirty(ctrlGo);
        }
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
        return "MeshFusion island applied: candidates=" + cands.Count + ", sources added=" + added + ", FBX" +
               " set readable=" + fbxChanged + ", non-FBX meshes=" + notFbx + ", unreadable skipped=" + skippedUnreadable + ", cellSize=" + CellSize + ". Scene NOT saved.";
    }

    [MenuItem("Tools/MeshFusion Island/ROLLBACK (remove sources + controller, restore Read/Write)")]
    public static void Rollback() { Debug.Log(RunRollback()); }

    public static string RunRollback()
    {
        var manifest = Load();
        int removed = 0;
        var root = GameObject.Find(TreeRootName);
        if (root != null)
        {
            foreach (var s in root.GetComponentsInChildren<StaticMeshFusionSource>(true))
            {
                var go = s.gameObject;
                UnityEngine.Object.DestroyImmediate(s);
                EditorUtility.SetDirty(go);
                removed++;
            }
        }
        var ctrlGo = GameObject.Find(ControllerName);
        if (ctrlGo != null) UnityEngine.Object.DestroyImmediate(ctrlGo);
        int restored = 0;
        AssetDatabase.StartAssetEditing();
        try
        {
            foreach (var f in manifest.fbx)
            {
                var imp = AssetImporter.GetAtPath(f.path) as ModelImporter;
                if (imp != null && imp.isReadable != f.wasReadable) { imp.isReadable = f.wasReadable; imp.SaveAndReimport(); restored++; }
            }
        }
        finally { AssetDatabase.StopAssetEditing(); }
        AssetDatabase.Refresh();
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        Save(new Manifest());
        return "MeshFusion island rolled back: sources removed=" + removed + ", FBX Read/Write restored=" + restored + ".";
    }
}
#endif
