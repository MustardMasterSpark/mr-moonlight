using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace MrMoonlight.EditorTools
{
    /// <summary>
    /// Removes terrain tree instances whose canopies intersect. Run AFTER BiomeVegetationSetup.
    ///
    /// WHY THE SPAWNER CANNOT DO THIS ITSELF
    /// -------------------------------------
    /// Vegetation Spawner samples a Poisson disc per SPECIES - VegetationSpawner.Trees.cs calls
    /// PoissonDisc.GetSpawnpoints(terrain, item.distance, item.seed + seed) once per TreeType, with
    /// a different seed each time. So "spawn points for a species will never overlap" is true, and
    /// says nothing at all about two different species landing on the same spot. With 16 species
    /// sharing the island that happens constantly.
    ///
    /// The collision cache is not an escape hatch either: it raycasts real scene colliders and
    /// explicitly skips TerrainCollider, which is exactly where terrain tree colliders live. Trees
    /// already placed are therefore invisible to it.
    ///
    /// WHAT COUNTS AS OVERLAP
    /// ----------------------
    /// Canopy radius from the prefab's baked mesh bounds, scaled by the instance's widthScale, then
    /// multiplied by CanopyFactor. At 1.0 two trees may not touch at all, which spaces a forest out
    /// far more than a real one - actual forest canopies interlock. At 0.8 trunks stay well clear
    /// and nothing reads as clipping into anything, while keeping the density the biome plan asks
    /// for. Lower it for a thicker wood, raise it toward 1.0 for parkland.
    ///
    /// Only tree-shaped prototypes are considered. Boulders, logs and stumps are deliberately
    /// exempt: rocks piling against each other is what makes a believable scree slope or a barrier
    /// wall, and driftwood tangles on a beach.
    ///
    /// Bigger trees win. Culling the smaller of an overlapping pair keeps mature trunks and thins
    /// the saplings crowding them, which is both what a real canopy does and what looks right.
    /// </summary>
    public static class TreeOverlapCull
    {
        // Lowered 0.8 -> 0.6 when density was doubled. At 0.8 the cull was eating most of the
        // increase rather than letting the forest thicken. 0.6 still leaves every trunk well
        // clear - the widest trunk capsule is 0.84 m on RF_Tree4, while 0.6 keeps two of them
        // 5.2 m apart - it just lets canopies interleave, which is what a real dense forest does.
        private const float CanopyFactor = 0.6f;

        [MenuItem("Tools/Mr. Moonlight/Vegetation/Cull Overlapping Trees")]
        public static void CullMenu() => Cull();

        public static string Cull()
        {
            var terrain = Object.FindFirstObjectByType<Terrain>();
            if (terrain == null) return "No Terrain in scene.";

            TerrainData td = terrain.terrainData;
            TreeInstance[] instances = td.treeInstances;
            TreePrototype[] protos = td.treePrototypes;
            Vector3 size = td.size;

            // Per-prototype canopy radius in metres, and whether it is a tree at all.
            var radius = new float[protos.Length];
            var isTree = new bool[protos.Length];
            for (int i = 0; i < protos.Length; i++)
            {
                var prefab = protos[i].prefab;
                if (prefab == null) continue;

                string n = prefab.name;
                isTree[i] = n.Contains("Tree") || n.Contains("Sapling");

                var mf = prefab.GetComponent<MeshFilter>();
                if (mf != null && mf.sharedMesh != null)
                {
                    Vector3 b = mf.sharedMesh.bounds.size;
                    radius[i] = Mathf.Max(b.x, b.z) * 0.5f;
                }
            }

            float maxR = 0f;
            for (int i = 0; i < radius.Length; i++)
                if (isTree[i]) maxR = Mathf.Max(maxR, radius[i]);

            float cell = Mathf.Max(2f * maxR * CanopyFactor, 4f);
            var grid = new Dictionary<long, List<int>>();

            // Largest first, so the survivor of any pair is the bigger tree.
            var order = new List<int>();
            for (int i = 0; i < instances.Length; i++)
                if (isTree[instances[i].prototypeIndex]) order.Add(i);
            order.Sort((a, b) =>
            {
                float ra = radius[instances[a].prototypeIndex] * instances[a].widthScale;
                float rb = radius[instances[b].prototypeIndex] * instances[b].widthScale;
                return rb.CompareTo(ra);
            });

            var keep = new bool[instances.Length];
            for (int i = 0; i < instances.Length; i++)
                keep[i] = !isTree[instances[i].prototypeIndex]; // non-trees always survive

            int culled = 0;

            foreach (int i in order)
            {
                TreeInstance inst = instances[i];
                float wx = inst.position.x * size.x;
                float wz = inst.position.z * size.z;
                float r = radius[inst.prototypeIndex] * inst.widthScale * CanopyFactor;

                int cx = Mathf.FloorToInt(wx / cell);
                int cz = Mathf.FloorToInt(wz / cell);

                bool blocked = false;
                for (int dz = -1; dz <= 1 && !blocked; dz++)
                {
                    for (int dx = -1; dx <= 1 && !blocked; dx++)
                    {
                        long key = ((long)(cx + dx) << 32) ^ (uint)(cz + dz);
                        if (!grid.TryGetValue(key, out var bucket)) continue;

                        foreach (int j in bucket)
                        {
                            TreeInstance other = instances[j];
                            float ox = other.position.x * size.x;
                            float oz = other.position.z * size.z;
                            float orr = radius[other.prototypeIndex] * other.widthScale * CanopyFactor;

                            float ddx = wx - ox, ddz = wz - oz;
                            float minD = r + orr;
                            if (ddx * ddx + ddz * ddz < minD * minD) { blocked = true; break; }
                        }
                    }
                }

                if (blocked) { culled++; continue; }

                keep[i] = true;
                long myKey = ((long)cx << 32) ^ (uint)cz;
                if (!grid.TryGetValue(myKey, out var list)) grid[myKey] = list = new List<int>();
                list.Add(i);
            }

            var survivors = new List<TreeInstance>(instances.Length - culled);
            for (int i = 0; i < instances.Length; i++)
                if (keep[i]) survivors.Add(instances[i]);

            td.SetTreeInstances(survivors.ToArray(), false);
            terrain.Flush();
            EditorUtility.SetDirty(td);

            int treesBefore = order.Count;
            return $"Trees: {treesBefore} -> {treesBefore - culled} ({culled} culled, " +
                   $"{(treesBefore > 0 ? culled * 100f / treesBefore : 0f):F1}% overlapping) " +
                   $"at canopy factor {CanopyFactor}. " +
                   $"Total instances incl. rocks/logs: {instances.Length} -> {survivors.Count}.";
        }
    }
}
