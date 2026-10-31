using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using BRGInstancedRenderer;
using MrMoonlight.DevTools.BRG;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MrMoonlight.Editor.BRG
{
    /// <summary>
    /// Two menus for AST-314 BRG Instanced Renderer (MRM-88, 2026-10-30, <c>Docs/brg-options.md</c>).
    /// <para><i>Mr. Moonlight/BRG/Convert Open Scene From Flora</i> replaces Flora in an island scene with BRG set up
    /// exactly like scene 09 (the decision of 2026-10-30, build 61), then runs the second menu. Used on 07, 02, 06, 08.</para>
    /// <para><i>Mr. Moonlight/BRG/Apply Groups And Baked Options</i>, on any scene that has a
    /// <see cref="BRGGameObjectGroup"/>:</para>
    /// <list type="number">
    /// <item>creates the four <see cref="BRGPrototypeGroup"/> assets (Trees, DeadWood, Rocks, Details) and the
    /// <see cref="BRGBakedOptions"/> asset in <c>Settings/BRG</c> if missing;</item>
    /// <item>reverts the <see cref="BRGGameObjectGroup"/>, writes a <see cref="BRGPrototypeExtraData"/> on every
    /// instance (category from its species folder name) and converts again, because the extra data is part
    /// of the prototype key (trap 4 of the handoff);</item>
    /// <item>writes the Details entry onto the root of every terrain detail prefab (an inert MonoBehaviour
    /// outside BRG, so the Flora scenes that share these prefabs are unaffected) and refreshes the terrain;</item>
    /// <item>adds or updates the F11 <see cref="BRGOptionsBench"/> with the four groups, and saves the scene.</item>
    /// </list>
    /// Re-run it after editing <c>BRGBakedOptions</c>; the defaults are neutral, so the first run only
    /// assigns groups. Every write is reported in the console and read back.
    /// </summary>
    public static class BRGTrialSetup
    {
        public const string Trees = "Trees";
        public const string DeadWood = "DeadWood";
        public const string Rocks = "Rocks";
        public const string Details = "Details";

        private const string Folder = "Assets/_Project/Settings/BRG";
        private const string OptionsPath = Folder + "/BRGBakedOptions.asset";
        private const string SpawnsRootName = "Gaia Game Object Spawns";

        // species folder names under "Gaia Game Object Spawns"
        private static readonly Regex RockName = new Regex("rock", RegexOptions.IgnoreCase);
        private static readonly Regex DeadName = new Regex("dead|break|fall|burnt|stump|broken|root|dry", RegexOptions.IgnoreCase);

        [MenuItem("Mr. Moonlight/BRG/Apply Groups And Baked Options")]
        public static void Apply()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (Object.FindFirstObjectByType<BRGGameObjectGroup>(FindObjectsInactive.Include) == null)
            {
                Debug.LogWarning("[BRG setup] No BRGGameObjectGroup in the open scene. Run 'Convert Open Scene From Flora' first.");
                return;
            }
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("[BRG setup] Stop play mode first.");
                return;
            }

            var options = LoadOrCreateOptions();

            // 1. trees and rocks: Revert -> write -> Convert
            var goGroup = Object.FindFirstObjectByType<BRGGameObjectGroup>(FindObjectsInactive.Include);
            var counts = new Dictionary<string, int>();
            if (goGroup != null)
            {
                var instances = new List<GameObject>();
                foreach (var link in goGroup.GetComponentsInChildren<BRGGameObjectLink>(true))
                    instances.Add(link.gameObject);
                if (instances.Count == 0)
                {
                    // already reverted: fall back to every renderer object under the species folders
                    foreach (var mr in goGroup.GetComponentsInChildren<MeshRenderer>(true))
                        instances.Add(mr.gameObject);
                }

                goGroup.RevertToGameObjects();

                // Without this BRG builds each prototype from the PREFAB ASSET, so extra data added on the
                // scene instances is silently ignored (first run: trees showed no group). On, it reads the
                // instance; same-species instances still share one prototype. Keeps the change in this scene
                // instead of editing the 88 tree prefabs the Flora scenes share.
                var groupSo = new SerializedObject(goGroup);
                var detect = groupSo.FindProperty("_detectPrefabOverrides");
                if (detect != null && !detect.boolValue)
                {
                    detect.boolValue = true;
                    groupSo.ApplyModifiedPropertiesWithoutUndo();
                }
                foreach (var go in instances)
                {
                    string category = Classify(go.transform, goGroup.transform);
                    var entry = options.Find(category);
                    if (entry == null) continue;
                    if (!go.TryGetComponent(out BRGPrototypeExtraData extra))
                        extra = go.AddComponent<BRGPrototypeExtraData>(); // no Undo: 8,786 records; the scene is backed up instead
                    entry.WriteTo(extra);
                    EditorUtility.SetDirty(extra);
                    counts.TryGetValue(category, out int c);
                    counts[category] = c + 1;
                }
                goGroup.ConvertChildren();
            }
            else
            {
                Debug.LogWarning("[BRG setup] No BRGGameObjectGroup in the scene; trees skipped.");
            }

            // 2. terrain detail prefabs
            int details = 0;
            var detailEntry = options.Find(Details);
            var registerers = Object.FindObjectsByType<TerrainBRGRegisterer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            var done = new HashSet<string>();
            foreach (var reg in registerers)
            {
                var terrain = reg.GetComponent<Terrain>();
                if (terrain == null || terrain.terrainData == null || detailEntry == null) continue;
                foreach (var proto in terrain.terrainData.detailPrototypes)
                {
                    if (!proto.usePrototypeMesh || proto.prototype == null) continue;
                    string path = AssetDatabase.GetAssetPath(proto.prototype);
                    if (string.IsNullOrEmpty(path) || !done.Add(path)) continue;

                    var root = PrefabUtility.LoadPrefabContents(path);
                    if (!root.TryGetComponent(out BRGPrototypeExtraData extra))
                        extra = root.AddComponent<BRGPrototypeExtraData>();
                    detailEntry.WriteTo(extra);
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    PrefabUtility.UnloadPrefabContents(root);
                    details++;
                }
            }
            AssetDatabase.SaveAssets();

            // read the prefabs back: SaveAsPrefabAsset can report success and write nothing
            int verified = 0;
            foreach (var path in done)
            {
                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (asset != null && asset.TryGetComponent(out BRGPrototypeExtraData e) && e.prototypeGroup == detailEntry.group)
                    verified++;
                else
                    Debug.LogWarning($"[BRG setup] Detail prefab not updated: {path}");
            }
            foreach (var reg in registerers) reg.Refresh();

            // 3. the F11 bench
            var bench = Object.FindFirstObjectByType<BRGOptionsBench>(FindObjectsInactive.Include);
            if (bench == null)
            {
                var go = new GameObject("BRG Options Bench");
                Undo.RegisterCreatedObjectUndo(go, "BRG Options Bench");
                bench = go.AddComponent<BRGOptionsBench>();
            }
            var so = new SerializedObject(bench);
            var groupsProp = so.FindProperty("groups");
            groupsProp.arraySize = options.categories.Length;
            for (int i = 0; i < options.categories.Length; i++)
                groupsProp.GetArrayElementAtIndex(i).objectReferenceValue = options.categories[i].group;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            int converted = goGroup != null ? goGroup.GetComponentsInChildren<BRGGameObjectLink>(true).Length : 0;
            counts.TryGetValue(Trees, out int t);
            counts.TryGetValue(DeadWood, out int d);
            counts.TryGetValue(Rocks, out int r);
            Debug.Log($"[BRG setup] done: {t} trees, {d} dead wood, {r} rocks given extra data; {converted} instance links after convert; " +
                      $"{details} detail prefabs written, {verified} verified; bench groups = {options.categories.Length}; scene saved.");
        }

        /// <summary>
        /// Flora to BRG, the scene 09 recipe (change record C-039) plus profile v1 (C-041): remove every Flora
        /// component, put <see cref="BRGMeshLodData"/> on each tree/rock renderer, a converted
        /// <see cref="BRGGameObjectGroup"/> (Static Multi Chunk, 256 m, detect prefab overrides) on the Gaia spawns
        /// root, a <see cref="TerrainBRGRegisterer"/> (useUnityTransforms ON, so the foliage set equals Flora's) on
        /// each terrain, the overlay camera excluded from BRG and the player camera's BRG LOD bias 0.6, then
        /// <see cref="Apply"/>. Flora is found by namespace so this editor assembly does not depend on it.
        /// </summary>
        [MenuItem("Mr. Moonlight/BRG/Convert Open Scene From Flora")]
        public static void ConvertFromFlora()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("[BRG convert] Stop play mode first.");
                return;
            }
            var scene = EditorSceneManager.GetActiveScene();
            var report = new System.Text.StringBuilder($"[BRG convert] {scene.name}: ");

            // 1. Flora out
            int floraInstances = 0, floraOther = 0;
            var spawnRoots = new HashSet<Transform>();
            foreach (var m in Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (m == null) continue;
                string ns = m.GetType().Namespace ?? "";
                if (!ns.StartsWith("MA.Flora")) continue;

                if (m.GetType().Name == "FloraInstanceRenderer")
                {
                    var root = FindAncestor(m.transform, SpawnsRootName);
                    if (root != null) spawnRoots.Add(root);
                    var mr = m.GetComponent<MeshRenderer>();
                    Object.DestroyImmediate(m);
                    // Flora hides its renderers with forceRenderingOff (not serialized); BRG hides them again on init
                    if (mr != null) mr.forceRenderingOff = false;
                    floraInstances++;
                }
                else
                {
                    var go = m.gameObject;
                    bool onlyThis = go.GetComponents<Component>().Length == 2 && go.transform.childCount == 0;
                    if (onlyThis && m.GetType().Name == "FloraSceneSettings") Object.DestroyImmediate(go);
                    else Object.DestroyImmediate(m);
                    floraOther++;
                }
            }
            report.Append($"removed {floraInstances} Flora instance renderers + {floraOther} other Flora components; ");

            if (spawnRoots.Count == 0)
            {
                foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                    if (t.name == SpawnsRootName) { spawnRoots.Add(t); break; }
            }

            // 2. trees and rocks
            foreach (var root in spawnRoots)
            {
                int lods = 0;
                foreach (var mr in root.GetComponentsInChildren<MeshRenderer>(true))
                {
                    if (mr.GetComponent<MeshFilter>() == null) continue;
                    if (mr.GetComponent<BRGMeshLodData>() == null) { mr.gameObject.AddComponent<BRGMeshLodData>(); lods++; }
                }

                if (!root.TryGetComponent(out BRGGameObjectGroup group))
                    group = root.gameObject.AddComponent<BRGGameObjectGroup>();
                var so = new SerializedObject(group);
                so.FindProperty("_chunkMode").intValue = 1; // Static Multi Chunk, as scene 09
                so.FindProperty("_chunkSize").floatValue = 256f;
                so.FindProperty("_detectPrefabOverrides").boolValue = true;
                so.ApplyModifiedPropertiesWithoutUndo();
                string parentName = root.parent != null ? root.parent.name : "";
                report.Append($"group on '{parentName}/{root.name}' (+{lods} BRGMeshLodData); ");
            }

            // 3. terrains
            int terrains = 0;
            foreach (var terrain in Object.FindObjectsByType<Terrain>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (!terrain.TryGetComponent(out TerrainBRGRegisterer reg))
                    reg = terrain.gameObject.AddComponent<TerrainBRGRegisterer>();
                var so = new SerializedObject(reg);
                so.FindProperty("useUnityTransforms").boolValue = true;
                so.ApplyModifiedPropertiesWithoutUndo();
                terrains++;
            }
            report.Append($"{terrains} terrain registerer(s); ");

            // 4. cameras (profile v1)
            foreach (var cam in Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                bool main = cam.CompareTag("MainCamera");
                if (!main && cam.name != "DamageNumbersOverlayCamera") continue;
                if (!cam.TryGetComponent(out BRGCameraSettings cs)) cs = cam.gameObject.AddComponent<BRGCameraSettings>();
                if (main) cs.lodBiasMultiplier = 0.6f;
                else cs.ExcludeFromRendering = true;
                EditorUtility.SetDirty(cs);
                report.Append(main ? $"'{cam.name}' LOD bias 0.6; " : $"'{cam.name}' excluded; ");
            }

            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log(report.ToString());

            // 5. groups, baked options, convert, bench, save
            Apply();
        }

        private static Transform FindAncestor(Transform t, string name)
        {
            for (var p = t; p != null; p = p.parent)
                if (p.name == name) return p;
            return null;
        }

        private static string Classify(Transform t, Transform groupRoot)
        {
            // the species folder is the direct child of the group root
            var folder = t;
            while (folder.parent != null && folder.parent != groupRoot) folder = folder.parent;
            string n = folder.name;
            if (RockName.IsMatch(n)) return Rocks;
            if (DeadName.IsMatch(n)) return DeadWood;
            return Trees;
        }

        private static BRGBakedOptions LoadOrCreateOptions()
        {
            if (!AssetDatabase.IsValidFolder(Folder))
            {
                Directory.CreateDirectory(Folder);
                AssetDatabase.Refresh();
            }

            var options = AssetDatabase.LoadAssetAtPath<BRGBakedOptions>(OptionsPath);
            if (options == null)
            {
                options = ScriptableObject.CreateInstance<BRGBakedOptions>();
                AssetDatabase.CreateAsset(options, OptionsPath);
            }

            foreach (var c in options.categories)
            {
                if (c == null || c.group != null) continue;
                string path = $"{Folder}/BRGGroup_{c.name}.asset";
                c.group = AssetDatabase.LoadAssetAtPath<BRGPrototypeGroup>(path);
                if (c.group == null)
                {
                    c.group = ScriptableObject.CreateInstance<BRGPrototypeGroup>();
                    AssetDatabase.CreateAsset(c.group, path);
                }
            }
            EditorUtility.SetDirty(options);
            AssetDatabase.SaveAssets();
            return options;
        }
    }
}
