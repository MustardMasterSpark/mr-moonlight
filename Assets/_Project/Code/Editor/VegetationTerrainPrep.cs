using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace MrMoonlight.EditorTools
{
    /// <summary>
    /// Makes the MRM-70 vegetation prefabs usable as Unity terrain trees / terrain details.
    ///
    /// WHY THIS EXISTS
    /// ---------------
    /// Unity's terrain system does NOT apply a tree/detail prototype prefab's ROOT transform.
    /// It builds each instance matrix from the TreeInstance alone:
    ///
    ///     TRS(position, AngleAxis(rotation, Vector3.up), (widthScale, heightScale, widthScale))
    ///
    /// The RetroRealism source FBXs are authored Z-up at 1/100 scale, and the prefabs built in
    /// the earlier MRM-70 pass compensated for that on the prefab root - rotation (270.02, 0, 0),
    /// scale (100, 100, 100). That is fine for an ordinary GameObject in a scene, and it is why
    /// nobody noticed. As a terrain tree it renders flat on the ground at 1/100 size; verified in
    /// the editor on 2026-08-25 before this tool was written.
    ///
    /// bakeAxisConversion on the ModelImporter does not fix it: the FBX declares itself Y-up while
    /// the geometry inside is Z-up, so Unity has nothing to detect. Raising globalScale fixes size
    /// but not orientation. So the transform is baked into a new Mesh asset here instead, and the
    /// prefab root becomes identity.
    ///
    /// Baked meshes are written next to the source as `Meshes/Baked/<name>_Baked.asset` and follow
    /// the project's Blender convention (Docs/blender-export-process notes): 1 unit = 1 m, upright,
    /// feet origin (min Y = 0), centred on X/Z.
    ///
    /// TIERS (see Docs/mrm70-biome-vegetation-strategy.md §5)
    /// -----
    ///   Tree   - Tree1-4, Sapling1-2. Terrain trees. LODGroup + CapsuleCollider sized from the
    ///            dedicated *_Collision mesh (the trunk hull), so we get a thin tall capsule
    ///            rather than thousands of non-convex mesh colliders baked into the TerrainCollider.
    ///   Prop   - Boulder1-5, Log1-3, Stump1-2. Terrain trees. LODGroup + BoxCollider.
    ///            NOT a MeshCollider, convex or otherwise: Unity's TerrainCollider refuses them on
    ///            tree prototypes, logging "TerrainCollider: MeshCollider is not supported on
    ///            terrain at the moment." once per prototype and leaving those props with NO
    ///            collision. Only Capsule/Box/Sphere work on terrain trees. Found in the WebGL
    ///            console 2026-08-25 (18 warnings) - the editor never complains, because there
    ///            these are prefab assets rather than terrain instances, so it looks fine locally.
    ///            A box is a coarse fit for a boulder, but a boulder you can stand on and walk
    ///            around beats one you fall straight through.
    ///   Detail - Bush1-3, Fern1-2. Terrain DETAILS. No LODGroup (the terrain detail system
    ///            rejects prefabs that have one) and no collider (details never get colliders
    ///            anyway, and the player should walk through ferns).
    ///
    /// LODGroup is required on the tree tier for a second reason: Unity only applies
    /// TreeInstance.rotation to prototypes that have one. Without it every tree in the forest
    /// spawns at an identical Y rotation. The spawner always randomises rotation
    /// (VegetationSpawner.Trees.cs), so this is purely a rendering-side requirement.
    ///
    /// Run "Report" first - it changes nothing and prints exactly what "Apply" would do.
    /// </summary>
    public static class VegetationTerrainPrep
    {
        private const string RetroRoot = "Assets/_Project/Prefabs/World/Vegetation/RetroRealism";
        private const string BakedFolder = "Assets/_Project/Art/Environment/Vegetation/RetroRealism/Meshes/Baked";

        // Culling only - LOD0 covers everything up to it. This is a safety net so a 1500 m draw
        // distance cannot try to draw every full-detail trunk on the island, NOT a way to hide a
        // perf problem behind a short draw distance (fog deliberately stays at 1500 m). At the
        // default 60 deg FOV this culls roughly at distance = height / (1.155 * 0.015), i.e. a
        // 13.6 m Tree1 disappears around 780 m. Re-tune from a real WebGL build, not from taste.
        private const float LodCullScreenHeight = 0.015f;

        private enum Tier { Tree, Prop, Detail }

        private static Tier TierOf(string prefabName)
        {
            if (prefabName.Contains("Tree") || prefabName.Contains("Sapling")) return Tier.Tree;
            if (prefabName.Contains("Boulder") || prefabName.Contains("Log") || prefabName.Contains("Stump")) return Tier.Prop;
            return Tier.Detail; // Bush, Fern
        }

        [MenuItem("Tools/Mr. Moonlight/Vegetation/Report Terrain-Tree Readiness")]
        public static void Report() => Run(dryRun: true);

        /// <summary>Same as Report(), but returns the text - the Unity console truncates multi-line logs.</summary>
        public static string ReportToString() => Run(dryRun: true);

        [MenuItem("Tools/Mr. Moonlight/Vegetation/Prepare Vegetation For Terrain (bake + collide + LOD)")]
        public static void Apply()
        {
            if (!EditorUtility.DisplayDialog(
                    "Prepare vegetation for terrain",
                    "This rewrites the RetroRealism prefabs: bakes root rotation/scale into new mesh " +
                    "assets, resets the roots to identity, swaps colliders and adds LOD groups.\n\n" +
                    "Run Report first if you have not. Continue?",
                    "Prepare", "Cancel"))
                return;

            Run(dryRun: false);
        }

        private static string Run(bool dryRun)
        {
            var log = new System.Text.StringBuilder();
            log.AppendLine(dryRun ? "=== DRY RUN - nothing written ===" : "=== APPLYING ===");

            if (!dryRun && !AssetDatabase.IsValidFolder(BakedFolder))
            {
                var parent = Path.GetDirectoryName(BakedFolder).Replace('\\', '/');
                AssetDatabase.CreateFolder(parent, "Baked");
            }

            string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { RetroRoot });
            int changed = 0;

            foreach (string guid in guids.OrderBy(g => AssetDatabase.GUIDToAssetPath(g)))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null) continue;

                Tier tier = TierOf(prefab.name);
                var mf = prefab.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null)
                {
                    log.AppendLine($"{prefab.name}: SKIP (no MeshFilter/mesh)");
                    continue;
                }

                Transform root = prefab.transform;
                Matrix4x4 bake = Matrix4x4.TRS(Vector3.zero, root.localRotation, root.localScale);
                bool needsBake = root.localRotation != Quaternion.identity || root.localScale != Vector3.one;

                // The trunk hull, used to size the capsule. Baked with the same matrix so it lines
                // up with the render mesh; for props it is usually the render mesh itself.
                var colSrc = prefab.GetComponent<MeshCollider>();
                Mesh colMesh = colSrc != null ? colSrc.sharedMesh : null;

                if (dryRun)
                {
                    Bounds b = TransformedBounds(mf.sharedMesh, bake);
                    string colDesc = tier switch
                    {
                        Tier.Tree => $"CapsuleCollider r={CapsuleRadius(colMesh, bake, b):F2} h={b.size.y:F2}",
                        Tier.Prop => "BoxCollider",
                        _ => "none (detail tier)"
                    };
                    log.AppendLine($"{prefab.name} [{tier}]: bake={needsBake} " +
                                   $"size {mf.sharedMesh.bounds.size} -> {b.size} | {colDesc} | " +
                                   $"LODGroup={(tier != Tier.Detail)}");
                    continue;
                }

                // --- bake mesh -------------------------------------------------------------
                Mesh baked = BakeMesh(mf.sharedMesh, bake, out Bounds bakedBounds);
                string bakedPath = $"{BakedFolder}/{prefab.name}_Baked.asset";
                var existing = AssetDatabase.LoadAssetAtPath<Mesh>(bakedPath);
                if (existing != null)
                {
                    existing.Clear();
                    CopyMeshInto(baked, existing);
                    EditorUtility.SetDirty(existing);
                    baked = existing;
                }
                else
                {
                    AssetDatabase.CreateAsset(baked, bakedPath);
                }

                // --- rewrite prefab --------------------------------------------------------
                GameObject inst = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    inst.transform.localPosition = Vector3.zero;
                    inst.transform.localRotation = Quaternion.identity;
                    inst.transform.localScale = Vector3.one;

                    inst.GetComponent<MeshFilter>().sharedMesh = baked;

                    foreach (var c in inst.GetComponents<Collider>())
                        Object.DestroyImmediate(c, true);
                    foreach (var l in inst.GetComponents<LODGroup>())
                        Object.DestroyImmediate(l, true);

                    if (tier == Tier.Tree)
                    {
                        var cap = inst.AddComponent<CapsuleCollider>();
                        cap.direction = 1; // Y
                        cap.radius = CapsuleRadius(colMesh, bake, bakedBounds);
                        cap.height = bakedBounds.size.y;
                        cap.center = new Vector3(0f, bakedBounds.size.y * 0.5f, 0f);
                    }
                    else if (tier == Tier.Prop)
                    {
                        // Box, not mesh - see the Prop tier note at the top of this file.
                        var box = inst.AddComponent<BoxCollider>();
                        box.center = bakedBounds.center;
                        box.size = bakedBounds.size;
                    }

                    if (tier != Tier.Detail)
                    {
                        var lod = inst.AddComponent<LODGroup>();
                        var rend = inst.GetComponent<MeshRenderer>();
                        lod.SetLODs(new[] { new LOD(LodCullScreenHeight, new Renderer[] { rend }) });
                        lod.RecalculateBounds();
                    }

                    PrefabUtility.SaveAsPrefabAsset(inst, path);
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(inst);
                }

                changed++;
                log.AppendLine($"{prefab.name} [{tier}]: baked -> {bakedPath} (h={bakedBounds.size.y:F2} m)");
            }

            if (!dryRun)
            {
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }

            log.AppendLine($"--- {changed} prefab(s) {(dryRun ? "would be" : "")} updated ---");
            Debug.Log(log.ToString());
            return log.ToString();
        }

        /// <summary>
        /// Capsule radius for a tree trunk.
        ///
        /// Tree1-4 ship a dedicated *_Collision / UCX_ mesh, which is the trunk hull - use the
        /// tighter of its two horizontal extents so a stray branch in the hull does not inflate
        /// the trunk. Saplings have no such mesh, so the only thing available is the full canopy,
        /// which is 5-10x too wide: RF_Sapling1's canopy is 2.15 m across on a 2.42 m tall plant,
        /// and a 1.08 m capsule would stop the player a metre short of a shrub.
        ///
        /// Both cases are then capped at height/30, a real-world-ish trunk slenderness ratio. That
        /// matters most for RF_Tree4, whose hull measures 2.66 m across (root flare) on a 25 m
        /// tree - the cap brings it to a believable 0.84 m.
        /// </summary>
        private static float CapsuleRadius(Mesh colMesh, Matrix4x4 bake, Bounds renderBounds)
        {
            float r;
            if (colMesh != null)
            {
                Bounds b = TransformedBounds(colMesh, bake);
                r = Mathf.Min(b.size.x, b.size.z) * 0.5f;
            }
            else
            {
                r = Mathf.Min(renderBounds.size.x, renderBounds.size.z) * 0.15f;
            }

            r = Mathf.Min(r, renderBounds.size.y / 30f);
            return Mathf.Max(r, 0.15f);
        }

        private static Bounds TransformedBounds(Mesh src, Matrix4x4 m)
        {
            var v = src.vertices;
            if (v.Length == 0) return new Bounds();
            Vector3 p0 = m.MultiplyPoint3x4(v[0]);
            var b = new Bounds(p0, Vector3.zero);
            for (int i = 1; i < v.Length; i++) b.Encapsulate(m.MultiplyPoint3x4(v[i]));

            // feet origin + centred on X/Z, matching what BakeMesh does
            var offset = new Vector3(-b.center.x, -b.min.y, -b.center.z);
            b.center += offset;
            return b;
        }

        private static Mesh BakeMesh(Mesh src, Matrix4x4 m, out Bounds bounds)
        {
            var verts = src.vertices;
            var norms = src.normals;
            var tans = src.tangents;

            // Uniform scale here, but do it correctly anyway so a future non-uniform prefab
            // does not silently produce broken lighting.
            Matrix4x4 nm = m.inverse.transpose;

            for (int i = 0; i < verts.Length; i++) verts[i] = m.MultiplyPoint3x4(verts[i]);
            for (int i = 0; i < norms.Length; i++) norms[i] = nm.MultiplyVector(norms[i]).normalized;
            for (int i = 0; i < tans.Length; i++)
            {
                Vector3 t = nm.MultiplyVector(new Vector3(tans[i].x, tans[i].y, tans[i].z)).normalized;
                tans[i] = new Vector4(t.x, t.y, t.z, tans[i].w);
            }

            // recentre: feet origin, centred on X/Z
            var b = new Bounds(verts[0], Vector3.zero);
            for (int i = 1; i < verts.Length; i++) b.Encapsulate(verts[i]);
            var offset = new Vector3(-b.center.x, -b.min.y, -b.center.z);
            for (int i = 0; i < verts.Length; i++) verts[i] += offset;

            var dst = new Mesh { name = src.name + "_Baked" };
            dst.indexFormat = src.indexFormat;
            dst.vertices = verts;
            if (norms.Length > 0) dst.normals = norms;
            if (tans.Length > 0) dst.tangents = tans;
            dst.uv = src.uv;
            if (src.uv2 != null && src.uv2.Length > 0) dst.uv2 = src.uv2;
            if (src.colors32 != null && src.colors32.Length > 0) dst.colors32 = src.colors32;

            dst.subMeshCount = src.subMeshCount;
            for (int s = 0; s < src.subMeshCount; s++)
                dst.SetTriangles(src.GetTriangles(s), s);

            dst.RecalculateBounds();
            bounds = dst.bounds;
            return dst;
        }

        private static void CopyMeshInto(Mesh src, Mesh dst)
        {
            dst.indexFormat = src.indexFormat;
            dst.vertices = src.vertices;
            dst.normals = src.normals;
            dst.tangents = src.tangents;
            dst.uv = src.uv;
            dst.uv2 = src.uv2;
            dst.colors32 = src.colors32;
            dst.subMeshCount = src.subMeshCount;
            for (int s = 0; s < src.subMeshCount; s++) dst.SetTriangles(src.GetTriangles(s), s);
            dst.RecalculateBounds();
            dst.name = src.name;
        }
    }
}
