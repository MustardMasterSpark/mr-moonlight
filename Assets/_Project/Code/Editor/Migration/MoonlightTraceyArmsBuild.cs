using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace MrMoonlight.EditorTools.Migration
{
    /// <summary>
    /// Builds Tracey's first-person arm set and registers it on the vendor arms prefab.
    ///
    /// <para><b>What this is.</b> The player's hands are a <i>viewmodel</i>: a 44-bone Generic
    /// skeleton (<c>Arms_Root</c>) that lives under the camera, is driven entirely by HQ FPS's
    /// per-weapon clips, and shares nothing with Tracey's body rig. See
    /// <c>Docs/tracey-rig-strategy.md</c> for why the two skeletons must stay separate and why the
    /// weapon clips can never be retargeted onto a full body.</para>
    ///
    /// <para><b>Why this is one job and not fifteen.</b> Fifteen <c>FP_Arms_*.fbx</c> files ship
    /// with the asset, one per weapon, but they are <i>animation-clip containers only</i>. At
    /// runtime there is exactly one arms mesh, in <c>HQFPS_Wieldable_Arms.prefab</c>, and equipping
    /// a weapon merely swaps <c>Animator.runtimeAnimatorController</c>
    /// (<c>WieldableArmsAnimator.OnEnable</c>). So a new pair of arms is registered once and every
    /// weapon picks it up.</para>
    ///
    /// <para><b>Why a tool instead of hand-wiring the prefab.</b>
    /// <c>HQFPS_Wieldable_Arms.prefab</c> sits under <c>Assets/ThirdParty/</c>, which
    /// <c>.gitignore</c> excludes wholesale — a hand edit there is invisible to version control and
    /// gone on a fresh clone. Every durable asset this tool produces lands in <c>_Project</c> (which
    /// is tracked), and the prefab wiring is reproducible by re-running the tool. Same pattern as
    /// the MRM-25 weapon tools.</para>
    ///
    /// <para><b>The bindpose transplant, and why it is not optional.</b> The <c>.blend</c> that
    /// authors these arms was created by importing the vendor FBX, and Blender recalculates bone
    /// roll on import. Measured 2026-09-05: <b>15 of 44 bones</b> come back out with a bindpose
    /// rotation differing from the vendor's by up to <c>0.109</c> — the left hand's fingers and the
    /// whole right forearm. Binding such a mesh to the prefab's vendor-authored skeleton twists
    /// those joints. Because the exported bone <i>order</i> is identical, the fix is exact: copy the
    /// vendor mesh's bindpose array onto ours. This runs on every build, so it also covers meshes
    /// Carlos re-exports after remodelling.</para>
    ///
    /// <para><b>Shader.</b> The arms use <c>Shader Graphs/LitFieldOfView_SSS</c>, not RetroLit.
    /// The viewmodel is rendered by the same camera as the world and is pulled to its own 60° FOV
    /// by a vertex-shader override (<c>CameraFOVHandler</c>, global <c>_FOV</c> / <c>_FOVEnabled</c>
    /// properties). A RetroLit material has no such node, so RetroLit arms would render at world FOV
    /// and clip through geometry. If the hands are to be RetroLit — the MRM-9 ruling — the FOV nodes
    /// have to be ported into a RetroLit viewmodel variant first.</para>
    /// </summary>
    public static class MoonlightTraceyArmsBuild
    {
        private const string ArmsPrefab =
            "Assets/ThirdParty/PolymindGames/HQFPS/Prefabs/Wieldables/HQFPS_Wieldable_Arms.prefab";

        private const string TraceyFbx =
            "Assets/_Project/Art/Characters/Tracey/Arms/Tracey_FP_Arms.fbx";

        private const string OutFolder = "Assets/_Project/Art/Characters/Tracey/Arms";
        private const string BaseColor = OutFolder + "/Tracey_Arms_BaseColor.png";
        private const string MaterialPath = OutFolder + "/Tracey_Arms.mat";

        /// <summary>Name of the arm set as it appears in the inspector and in ToggleNextArmSet.</summary>
        private const string SetName = "Tracey";

        /// <summary>The vendor pair we copy bindposes, transform and render settings from.</summary>
        private const string DonorLeft = "Arm_Standard.l";
        private const string DonorRight = "Arm_Standard.r";

        private const string MineLeft = "Tracey_Arm_L";
        private const string MineRight = "Tracey_Arm_R";

        [MenuItem("Tools/MrMoonlight/Character/Build Tracey FP Arms")]
        public static void Build()
        {
            var log = new StringBuilder();
            log.AppendLine("=== Build Tracey FP Arms ===");

            if (!ForceImportSettings(log)) { Dump(log); return; }

            var material = BuildMaterial(log);
            if (material == null) { Dump(log); return; }

            // Prefab contents are the source of truth for the donor bindposes: they are what the
            // runtime skeleton is actually bound to.
            var contents = PrefabUtility.LoadPrefabContents(ArmsPrefab);
            if (contents == null)
            {
                log.AppendLine("ABORT: could not load prefab contents at " + ArmsPrefab);
                Dump(log);
                return;
            }

            try
            {
                var donorL = FindRenderer(contents, DonorLeft);
                var donorR = FindRenderer(contents, DonorRight);
                if (donorL == null || donorR == null)
                {
                    log.AppendLine("ABORT: donor renderers not found (" + DonorLeft + " / " + DonorRight + ").");
                    return;
                }

                var srcL = FindFbxRenderer(MineLeft, log);
                var srcR = FindFbxRenderer(MineRight, log);
                if (srcL == null || srcR == null) return;

                var meshL = BuildBindposeCorrectedMesh(srcL, donorL, MineLeft, log);
                var meshR = BuildBindposeCorrectedMesh(srcR, donorR, MineRight, log);
                if (meshL == null || meshR == null) return;

                var rendL = AttachRenderer(contents, donorL, MineLeft, meshL, material, log);
                var rendR = AttachRenderer(contents, donorR, MineRight, meshR, material, log);

                RegisterArmSet(contents, rendL, rendR, log);

                PrefabUtility.SaveAsPrefabAsset(contents, ArmsPrefab);
                log.AppendLine("Saved " + ArmsPrefab);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Verify(log);
            Dump(log);
        }

        /// <summary>
        /// Undoes <see cref="Build"/>: drops the arm set and both renderers from the arms prefab,
        /// leaving the vendor's four sets exactly as they shipped. Does <b>not</b> delete the source
        /// assets — that is a separate, deliberate act.
        ///
        /// <para>Exists because <see cref="Build"/> writes into a git-ignored vendor prefab, so
        /// "revert the change" cannot be done with git. Carlos asked for exactly this on 2026-09-05
        /// once the dry-run mesh was confirmed not to be the shipping art.</para>
        /// </summary>
        [MenuItem("Tools/MrMoonlight/Character/Remove Tracey FP Arms")]
        public static void Remove()
        {
            var log = new StringBuilder();
            log.AppendLine("=== Remove Tracey FP Arms ===");

            var contents = PrefabUtility.LoadPrefabContents(ArmsPrefab);
            if (contents == null)
            {
                log.AppendLine("ABORT: could not load " + ArmsPrefab);
                Dump(log);
                return;
            }

            try
            {
                MonoBehaviour handler = null;
                var behaviours = contents.GetComponentsInChildren<MonoBehaviour>(true);
                for (int i = 0; i < behaviours.Length; i++)
                    if (behaviours[i] != null && behaviours[i].GetType().Name == "WieldableArmsHandler")
                        handler = behaviours[i];

                if (handler != null)
                {
                    var so = new SerializedObject(handler);
                    var sets = so.FindProperty("_armSets");
                    int removed = 0;
                    for (int i = sets.arraySize - 1; i >= 0; i--)
                    {
                        var n = sets.GetArrayElementAtIndex(i).FindPropertyRelative("Name");
                        if (n != null && n.stringValue == SetName) { sets.DeleteArrayElementAtIndex(i); removed++; }
                    }
                    so.ApplyModifiedPropertiesWithoutUndo();
                    log.AppendLine("Removed " + removed + " arm set(s) named \"" + SetName + "\".");
                }

                foreach (string name in new[] { MineLeft, MineRight })
                {
                    var r = FindRenderer(contents, name);
                    if (r != null) { Object.DestroyImmediate(r.gameObject); log.AppendLine("Deleted renderer " + name); }
                    else log.AppendLine(name + " was not present.");
                }

                PrefabUtility.SaveAsPrefabAsset(contents, ArmsPrefab);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // Read back off disk — SaveAsPrefabAsset can report success without writing.
            var reread = AssetDatabase.LoadAssetAtPath<GameObject>(ArmsPrefab);
            var names = new List<string>();
            var bs = reread.GetComponentsInChildren<MonoBehaviour>(true);
            for (int i = 0; i < bs.Length; i++)
            {
                if (bs[i] == null || bs[i].GetType().Name != "WieldableArmsHandler") continue;
                var sets = new SerializedObject(bs[i]).FindProperty("_armSets");
                for (int s = 0; s < sets.arraySize; s++)
                    names.Add(sets.GetArrayElementAtIndex(s).FindPropertyRelative("Name").stringValue);
            }
            log.AppendLine("verify: renderers=" + reread.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length +
                           "  arm sets=[" + string.Join(", ", names.ToArray()) + "]");
            Dump(log);
        }

        // ------------------------------------------------------------------ import settings

        private static bool ForceImportSettings(StringBuilder log)
        {
            var mi = AssetImporter.GetAtPath(TraceyFbx) as ModelImporter;
            if (mi == null)
            {
                log.AppendLine("ABORT: no ModelImporter at " + TraceyFbx +
                               " — export the arms from Blender first.");
                return false;
            }

            bool changed =
                mi.animationType != ModelImporterAnimationType.Generic ||
                mi.importAnimation ||
                mi.materialImportMode != ModelImporterMaterialImportMode.None ||
                !Mathf.Approximately(mi.globalScale, 1f);

            mi.animationType = ModelImporterAnimationType.Generic;
            mi.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            mi.importAnimation = false;
            mi.importConstraints = false;
            mi.importCameras = false;
            mi.importLights = false;
            mi.importBlendShapes = false;
            mi.optimizeGameObjects = false;
            mi.globalScale = 1f;
            mi.useFileScale = true;
            mi.materialImportMode = ModelImporterMaterialImportMode.None;

            if (changed)
            {
                mi.SaveAndReimport();
                log.AppendLine("Import settings corrected on " + TraceyFbx);
            }
            else
            {
                log.AppendLine("Import settings already correct.");
            }

            return true;
        }

        // ------------------------------------------------------------------ material

        private static Material BuildMaterial(StringBuilder log)
        {
            var donorMat = AssetDatabase.LoadAssetAtPath<Material>(
                "Assets/ThirdParty/PolymindGames/HQFPS/Art/Meshes/Wieldables/Arms/Materials/FP_Arm_Standard.mat");
            if (donorMat == null)
            {
                log.AppendLine("ABORT: vendor material FP_Arm_Standard.mat not found.");
                return null;
            }

            var albedo = AssetDatabase.LoadAssetAtPath<Texture2D>(BaseColor);
            if (albedo == null)
                log.AppendLine("WARNING: " + BaseColor + " missing — material keeps the vendor texture.");

            var mat = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (mat == null)
            {
                mat = new Material(donorMat);
                AssetDatabase.CreateAsset(mat, MaterialPath);
                log.AppendLine("Created " + MaterialPath + " (shader " + mat.shader.name + ")");
            }
            else
            {
                mat.shader = donorMat.shader;
                mat.CopyPropertiesFromMaterial(donorMat);
                log.AppendLine("Refreshed " + MaterialPath + " (shader " + mat.shader.name + ")");
            }

            if (albedo != null)
            {
                // Swap every texture slot that still points at the vendor albedo. Property names
                // differ between LitFieldOfView and LitFieldOfView_SSS, so match by content.
                var shader = mat.shader;
                int count = ShaderUtil.GetPropertyCount(shader);
                var swapped = new List<string>();
                for (int i = 0; i < count; i++)
                {
                    if (ShaderUtil.GetPropertyType(shader, i) != ShaderUtil.ShaderPropertyType.TexEnv) continue;
                    string prop = ShaderUtil.GetPropertyName(shader, i);
                    var current = mat.GetTexture(prop);
                    if (current != null && current.name == "Arm_Standard")
                    {
                        mat.SetTexture(prop, albedo);
                        swapped.Add(prop);
                    }
                }

                if (swapped.Count == 0)
                {
                    // Fall back to the conventional slot so the material is never left vendor-skinned.
                    if (mat.HasProperty("_BaseMap")) { mat.SetTexture("_BaseMap", albedo); swapped.Add("_BaseMap"); }
                    else if (mat.HasProperty("_MainTex")) { mat.SetTexture("_MainTex", albedo); swapped.Add("_MainTex"); }
                }

                log.AppendLine("  albedo -> " + BaseColor + " on [" + string.Join(", ", swapped.ToArray()) + "]");
            }

            EditorUtility.SetDirty(mat);
            return mat;
        }

        // ------------------------------------------------------------------ mesh

        private static SkinnedMeshRenderer FindFbxRenderer(string name, StringBuilder log)
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(TraceyFbx);
            if (go == null)
            {
                log.AppendLine("ABORT: " + TraceyFbx + " did not import.");
                return null;
            }

            var all = go.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            for (int i = 0; i < all.Length; i++)
                if (all[i].name == name) return all[i];

            log.AppendLine("ABORT: " + name + " not found inside " + TraceyFbx +
                           " — check the object names in Blender.");
            return null;
        }

        private static SkinnedMeshRenderer FindRenderer(GameObject root, string name)
        {
            var all = root.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            for (int i = 0; i < all.Length; i++)
                if (all[i].name == name) return all[i];
            return null;
        }

        /// <summary>
        /// Copies the exported mesh and replaces its bindposes with the donor's, so it deforms
        /// identically on the prefab's skeleton. Aborts loudly if the bone lists disagree, because
        /// silently mis-binding a viewmodel is the kind of bug that only shows up mid-reload.
        /// </summary>
        private static Mesh BuildBindposeCorrectedMesh(SkinnedMeshRenderer src, SkinnedMeshRenderer donor,
                                                       string name, StringBuilder log)
        {
            if (src.bones.Length != donor.bones.Length)
            {
                log.AppendLine("ABORT: " + name + " has " + src.bones.Length + " bones, donor has " +
                               donor.bones.Length + ". The exported skeleton is not the vendor rig.");
                return null;
            }

            int mismatch = 0;
            for (int i = 0; i < src.bones.Length; i++)
                if (src.bones[i].name != donor.bones[i].name) mismatch++;

            if (mismatch > 0)
            {
                log.AppendLine("ABORT: " + name + " bone ORDER differs from the donor on " + mismatch +
                               " bones. Re-export from Blender without reordering the armature.");
                return null;
            }

            var donorBind = donor.sharedMesh.bindposes;
            float worst = 0f;
            for (int b = 0; b < donorBind.Length; b++)
                for (int r = 0; r < 4; r++)
                    for (int c = 0; c < 4; c++)
                    {
                        float d = Mathf.Abs(donorBind[b][r, c] - src.sharedMesh.bindposes[b][r, c]);
                        if (d > worst) worst = d;
                    }

            var mesh = Object.Instantiate(src.sharedMesh);
            mesh.name = name;
            mesh.bindposes = donorBind;
            mesh.RecalculateBounds();

            string path = OutFolder + "/" + name + ".asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null) AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(mesh, path);

            log.AppendLine(name + ": " + mesh.vertexCount + " verts, " + mesh.bindposes.Length +
                           " bindposes transplanted (max drift corrected: " + worst.ToString("F4") + ") -> " + path);
            return mesh;
        }

        // ------------------------------------------------------------------ prefab wiring

        private static SkinnedMeshRenderer AttachRenderer(GameObject contents, SkinnedMeshRenderer donor,
                                                          string name, Mesh mesh, Material material,
                                                          StringBuilder log)
        {
            var existing = FindRenderer(contents, name);
            if (existing != null)
            {
                Object.DestroyImmediate(existing.gameObject);
                log.AppendLine("Replaced existing " + name + " in the prefab.");
            }

            var go = new GameObject(name);
            go.transform.SetParent(donor.transform.parent, false);
            go.transform.localPosition = donor.transform.localPosition;
            go.transform.localRotation = donor.transform.localRotation;
            go.transform.localScale = donor.transform.localScale;
            go.layer = donor.gameObject.layer;

            var smr = go.AddComponent<SkinnedMeshRenderer>();
            smr.sharedMesh = mesh;
            smr.bones = donor.bones;          // same transforms, same order — verified above
            smr.rootBone = donor.rootBone;
            smr.sharedMaterial = material;
            smr.shadowCastingMode = donor.shadowCastingMode;
            smr.receiveShadows = donor.receiveShadows;
            smr.updateWhenOffscreen = donor.updateWhenOffscreen;
            smr.localBounds = donor.localBounds;
            smr.quality = donor.quality;

            // Registered as an arm set; WieldableArmsHandler.Awake enables index 0 and hides the rest.
            go.SetActive(false);

            log.AppendLine("Attached " + name + " under " + donor.transform.parent.name +
                           " (layer " + LayerMask.LayerToName(go.layer) + ", shadows " + smr.shadowCastingMode + ")");
            return smr;
        }

        /// <summary>
        /// Inserts (or refreshes) the "Tracey" entry at index 0 of WieldableArmsHandler._armSets.
        /// Index 0 is what Awake enables, so this makes Tracey the default hands.
        /// Reached through SerializedObject and matched by type name so this tool needs no assembly
        /// reference to the vendor code.
        /// </summary>
        private static void RegisterArmSet(GameObject contents, SkinnedMeshRenderer left,
                                           SkinnedMeshRenderer right, StringBuilder log)
        {
            MonoBehaviour handler = null;
            var behaviours = contents.GetComponentsInChildren<MonoBehaviour>(true);
            for (int i = 0; i < behaviours.Length; i++)
                if (behaviours[i] != null && behaviours[i].GetType().Name == "WieldableArmsHandler")
                    handler = behaviours[i];

            if (handler == null)
            {
                log.AppendLine("ABORT: WieldableArmsHandler not found on the arms prefab.");
                return;
            }

            var so = new SerializedObject(handler);
            var sets = so.FindProperty("_armSets");
            if (sets == null || !sets.isArray)
            {
                log.AppendLine("ABORT: _armSets is not a serialized array on WieldableArmsHandler.");
                return;
            }

            // Idempotent: drop any previous Tracey entry before re-inserting at the front.
            for (int i = sets.arraySize - 1; i >= 0; i--)
            {
                var nameProp = sets.GetArrayElementAtIndex(i).FindPropertyRelative("Name");
                if (nameProp != null && nameProp.stringValue == SetName)
                    sets.DeleteArrayElementAtIndex(i);
            }

            sets.InsertArrayElementAtIndex(0);
            var entry = sets.GetArrayElementAtIndex(0);
            entry.FindPropertyRelative("Name").stringValue = SetName;
            entry.FindPropertyRelative("LeftArm").objectReferenceValue = left;
            entry.FindPropertyRelative("RightArm").objectReferenceValue = right;
            so.ApplyModifiedPropertiesWithoutUndo();

            var names = new List<string>();
            for (int i = 0; i < sets.arraySize; i++)
                names.Add(sets.GetArrayElementAtIndex(i).FindPropertyRelative("Name").stringValue);
            log.AppendLine("Arm sets now: [" + string.Join(", ", names.ToArray()) + "]  (index 0 is the default)");
        }

        // ------------------------------------------------------------------ verification

        /// <summary>
        /// Reads the saved prefab back off disk. SaveAsPrefabAsset can report success while the
        /// asset on disk is unchanged, so nothing is reported as done until it survives a reload.
        /// </summary>
        private static void Verify(StringBuilder log)
        {
            log.AppendLine("--- verify (re-read from disk) ---");
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(ArmsPrefab);
            if (go == null) { log.AppendLine("FAILED: prefab did not reload."); return; }

            foreach (string name in new[] { MineLeft, MineRight })
            {
                var r = FindRenderer(go, name);
                if (r == null) { log.AppendLine("FAILED: " + name + " missing after save."); continue; }
                log.AppendLine(name + ": mesh=" + (r.sharedMesh != null ? r.sharedMesh.name : "NULL") +
                               " bones=" + r.bones.Length +
                               " mat=" + (r.sharedMaterial != null ? r.sharedMaterial.name : "NULL") +
                               " shader=" + (r.sharedMaterial != null ? r.sharedMaterial.shader.name : "-") +
                               " layer=" + LayerMask.LayerToName(r.gameObject.layer));
            }

            var behaviours = go.GetComponentsInChildren<MonoBehaviour>(true);
            for (int i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] == null || behaviours[i].GetType().Name != "WieldableArmsHandler") continue;
                var sets = new SerializedObject(behaviours[i]).FindProperty("_armSets");
                for (int s = 0; s < sets.arraySize; s++)
                {
                    var e = sets.GetArrayElementAtIndex(s);
                    var l = e.FindPropertyRelative("LeftArm").objectReferenceValue;
                    var r = e.FindPropertyRelative("RightArm").objectReferenceValue;
                    log.AppendLine("  set[" + s + "] " + e.FindPropertyRelative("Name").stringValue +
                                   " L=" + (l != null ? l.name : "NULL") + " R=" + (r != null ? r.name : "NULL"));
                }
            }
        }

        private static void Dump(StringBuilder log)
        {
            Debug.Log(log.ToString());
        }
    }
}
