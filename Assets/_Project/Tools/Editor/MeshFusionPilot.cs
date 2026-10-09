#if UNITY_EDITOR
// MRM-88 side track: MeshFusion Pro (AST-146) pilot on the vegetation gallery, scene 05 (Carlos, 2026-10-08).
//
// What Apply does, on the ACTIVE scene only (it never saves the scene):
//   1. finds every gallery specimen (root with one MeshRenderer child, no LODGroup) at x <= DenseMaxX (the dense half of the gallery);
//   2. ticks Read/Write on the FBX importer behind each mesh (MeshFusion cannot read an unreadable mesh);
//   3. clears the Batching Static flag on those children (MeshFusion refuses a renderer that is part of a static batch);
//   4. adds a StaticMeshFusionSource to each child and one RuntimeMeshFusion controller ("MeshFusion Pilot") to the scene.
// At runtime the sources combine into cells (CellSize) and the original renderers are switched off; colliders are left alone.
// Everything it touched is recorded in the manifest; ROLLBACK undoes all of it. Nothing is deleted from disk except the components it added.
using System;
using System.Collections.Generic;
using System.IO;
using NGS.MeshFusionPro;
using UnityEditor;
using UnityEngine;

public static class MeshFusionPilot
{
    const float DenseMaxX = 260f;   // pilot group = the dense half of the gallery (build 47/48 comparison window)
    const int CellSize = 80;        // world units per merge cell (MeshFusion default)
    const string ControllerName = "MeshFusion Pilot";
    const string ManifestPath = "Assets/_Project/Tools/Editor/MeshFusionPilotManifest.json";

    [Serializable] public class FbxEntry { public string path; public bool wasReadable; }
    [Serializable] public class ObjEntry { public string name; public bool wasBatchingStatic; }
    [Serializable] public class Manifest { public List<FbxEntry> fbx = new List<FbxEntry>(); public List<ObjEntry> objects = new List<ObjEntry>(); }

    static Manifest Load() => File.Exists(ManifestPath) ? (JsonUtility.FromJson<Manifest>(File.ReadAllText(ManifestPath)) ?? new Manifest()) : new Manifest();
    static void Save(Manifest m) { File.WriteAllText(ManifestPath, JsonUtility.ToJson(m, true)); AssetDatabase.ImportAsset(ManifestPath); }

    static List<GameObject> Candidates()
    {
        var list = new List<GameObject>();
        foreach (var r in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
        {
            if (r.transform.childCount != 1 || r.transform.position.x > DenseMaxX) continue;
            if (r.GetComponentInChildren<LODGroup>(true) != null) continue;
            var c = r.transform.GetChild(0);
            var mf = c.GetComponent<MeshFilter>();
            if (c.GetComponent<MeshRenderer>() == null || mf == null || mf.sharedMesh == null) continue;
            list.Add(c.gameObject);
        }
        return list;
    }

    [MenuItem("Tools/MeshFusion Pilot/Apply to gallery dense half (scene 05, not saved)")]
    public static void Apply() { Debug.Log(Run()); }

    public static string Run()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (!scene.name.StartsWith("05")) return "Refused: active scene is '" + scene.name + "', the pilot is for scene 05 only.";
        var manifest = Load();
        var cands = Candidates();

        // 1. Read/Write on the FBX importers (one SaveAndReimport per distinct file)
        var seen = new HashSet<string>();
        foreach (var f in manifest.fbx) seen.Add(f.path);
        int fbxChanged = 0, notFbx = 0;
        AssetDatabase.StartAssetEditing();
        try
        {
            foreach (var go in cands)
            {
                string path = AssetDatabase.GetAssetPath(go.GetComponent<MeshFilter>().sharedMesh);
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

        // 2 + 3. Sources on the children, static batching flag off
        int added = 0, skippedUnreadable = 0;
        foreach (var go in cands)
        {
            var mesh = go.GetComponent<MeshFilter>().sharedMesh;
            if (!mesh.isReadable) { skippedUnreadable++; continue; }
            if (go.GetComponent<StaticMeshFusionSource>() != null) continue;
            var flags = GameObjectUtility.GetStaticEditorFlags(go);
            bool batching = (flags & StaticEditorFlags.BatchingStatic) != 0;
            manifest.objects.Add(new ObjEntry { name = go.transform.parent.name, wasBatchingStatic = batching });
            if (batching) GameObjectUtility.SetStaticEditorFlags(go, flags & ~StaticEditorFlags.BatchingStatic);
            var src = Undo.AddComponent<StaticMeshFusionSource>(go);
            src.CheckCompatibility();
            added++;
            EditorUtility.SetDirty(go);
        }

        // 4. Controller
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
        Save(manifest);
        return "MeshFusion pilot applied: candidates=" + cands.Count + ", sources added=" + added + ", FBX set readable=" + fbxChanged +
               ", non-FBX meshes=" + notFbx + ", unreadable skipped=" + skippedUnreadable + ", cellSize=" + CellSize + ". Scene NOT saved.";
    }

    [MenuItem("Tools/MeshFusion Pilot/ROLLBACK (remove sources + controller, restore Read/Write and static flags)")]
    public static void Rollback() { Debug.Log(RunRollback()); }

    public static string RunRollback()
    {
        var manifest = Load();
        int removed = 0;
        foreach (var r in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
        {
            foreach (var s in r.GetComponentsInChildren<StaticMeshFusionSource>(true))
            {
                var go = s.gameObject;
                var entry = manifest.objects.Find(o => o.name == (go.transform.parent != null ? go.transform.parent.name : go.name));
                UnityEngine.Object.DestroyImmediate(s);
                if (entry != null && entry.wasBatchingStatic)
                    GameObjectUtility.SetStaticEditorFlags(go, GameObjectUtility.GetStaticEditorFlags(go) | StaticEditorFlags.BatchingStatic);
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
        return "MeshFusion pilot rolled back: sources removed=" + removed + ", FBX Read/Write restored=" + restored + ".";
    }
}
#endif
