using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using sc.terrain.vegetationspawner;

namespace MrMoonlight.EditorTools
{
    /// <summary>
    /// Configures the Vegetation Spawner's tree/prop species for all 8 biomes, then spawns.
    ///
    /// HOW BIOMES ARE SEPARATED
    /// ------------------------
    /// Every species is masked to the terrain layer its biome painted (see BiomePainter). The
    /// spawner's mask test is effectively a hard cutoff at the mask's `threshold` - the arithmetic
    /// in VegetationSpawner.Trees.cs compares a 0-1 Random.value against a 0-100 spawn chance, so
    /// any splat weight above the threshold passes essentially always. That makes `threshold` a
    /// clean biome boundary control rather than a soft blend.
    ///
    /// Forest uses threshold 0.75 specifically to exclude the hinterland, which BiomePainter paints
    /// at 0.62 on the same layer. See the base-layer comment there.
    ///
    /// WHY EVERY TREE SPECIES HAS slopeRange.x = 1.5
    /// ---------------------------------------------
    /// TerrainComposer flattens exactly four pads - campsite, glade, oasis and flak tower - and
    /// those are precisely the four places the brief wants kept clear (tent space, a treeless
    /// centre for the telescope, an unobstructed statue, and an open enemy arena). A dead-flat pad
    /// has ~0 slope, so a 1.5 degree minimum keeps trees off all four without needing per-biome
    /// exclusion zones, while the sloped falloff ring around each pad still gets planted - which
    /// is what "circular open plain surrounded by pines" actually looks like.
    ///
    /// TREES ARE ALWAYS UPRIGHT
    /// ------------------------
    /// Free, and not by accident: a Unity TreeInstance stores only a Y rotation, so terrain trees
    /// physically cannot tilt to match a slope. Grass/details are the opposite - they expose
    /// `alignToGround`, which is where slope-following belongs.
    ///
    /// OVERLAP
    /// -------
    /// The spawner's Poisson disc guarantees no overlap WITHIN a species, but each species samples
    /// its own point set (item.seed + seed), so two different species can land on top of each
    /// other. The collision cache does not help - it ignores TerrainCollider, which is where tree
    /// colliders live. Carlos's "trees should never overlap" rule is therefore enforced afterwards
    /// by TreeOverlapCull, not here.
    ///
    /// MATERIAL VARIANTS ARE STILL PENDING. Autumn and eerie currently reuse the green RetroRealism
    /// material, so those biomes read green rather than orange/dead. Tinted atlas variants are the
    /// next step - see strategy doc §5.4.
    /// </summary>
    public static class BiomeVegetationSetup
    {
        private const string PrefabRoot = "Assets/_Project/Prefabs/World/Vegetation/RetroRealism";

        private const int LForest = 0, LGlade = 1, LAutumn = 2, LFlak = 3;
        private const int LRock = 4, LSand = 5, LMoss = 6, LEerie = 7;

        private const float SeaLevelY = 40f;
        private const float MinTreeSlope = 1.5f;

        /// <summary>
        /// Global density scale. Poisson point count goes as 1/distance², so distance is divided by
        /// sqrt(this) - 2.0 really does mean twice as many trees, not twice as close.
        ///
        /// The spawner clamps `distance` to a minimum of 1 m, so past roughly 4x the tighter
        /// species stop getting denser and only the wider-spaced ones keep responding.
        ///
        /// Raising this also raises how much TreeOverlapCull removes, since more trees compete for
        /// the same ground - lower its CanopyFactor alongside this if the cull starts eating the
        /// increase.
        /// </summary>
        private const float DensityMultiplier = 2.0f;

        private sealed class Species
        {
            public string Name;
            public int Layer;
            public float Threshold = 0.5f;
            public string[] Prefabs;       // first is primary; rest use SecondaryChance
            public float[] Chances;        // per-prefab probability, parallel to Prefabs
            public float Distance = 12f;   // Poisson min spacing, metres (spawner clamps to 1-25)
            public float Probability = 90f;
            public Vector2 Scale = new Vector2(0.85f, 1.2f);
            public Vector2 Slope = new Vector2(MinTreeSlope, 32f);
            public Vector2 Height = new Vector2(SeaLevelY, 1000f);
        }

        private static List<Species> Plan() => new List<Species>
        {
            // ---- FOREST (blue) - dense green Alaskan forest, the Vibe_FoggyForestPath look -----
            new Species { Name = "Forest conifer", Layer = LForest, Threshold = 0.75f,
                Prefabs = new[]{ "RF_Tree1", "RF_Tree4" }, Chances = new[]{ 100f, 55f },
                Distance = 10f, Probability = 92f, Scale = new Vector2(0.85f, 1.3f) },
            // Tree2/Tree3 are 1,204 and 1,378 tris - accents only, never base density.
            new Species { Name = "Forest old growth", Layer = LForest, Threshold = 0.75f,
                Prefabs = new[]{ "RF_Tree2", "RF_Tree3" }, Chances = new[]{ 100f, 60f },
                Distance = 21f, Probability = 55f, Scale = new Vector2(0.9f, 1.15f), Slope = new Vector2(MinTreeSlope, 28f) },
            new Species { Name = "Forest sapling", Layer = LForest, Threshold = 0.75f,
                Prefabs = new[]{ "RF_Sapling2", "RF_Sapling1" }, Chances = new[]{ 100f, 70f },
                Distance = 9f, Probability = 60f, Scale = new Vector2(0.8f, 1.35f), Slope = new Vector2(MinTreeSlope, 36f) },
            new Species { Name = "Forest deadfall", Layer = LForest, Threshold = 0.75f,
                Prefabs = new[]{ "RF_Log1", "RF_Log2", "RF_Stump1" }, Chances = new[]{ 100f, 80f, 60f },
                Distance = 20f, Probability = 45f, Scale = new Vector2(0.9f, 1.2f), Slope = new Vector2(0f, 30f) },
            new Species { Name = "Forest boulder", Layer = LForest, Threshold = 0.75f,
                Prefabs = new[]{ "RF_Boulder2", "RF_Boulder3" }, Chances = new[]{ 100f, 70f },
                Distance = 23f, Probability = 40f, Scale = new Vector2(0.8f, 1.4f), Slope = new Vector2(0f, 34f) },

            // ---- AUTUMN (pink) - birch-ish stand, leaf litter, campsite pad stays clear --------
            new Species { Name = "Autumn tree", Layer = LAutumn,
                Prefabs = new[]{ "RF_Tree1", "RF_Tree4" }, Chances = new[]{ 100f, 40f },
                Distance = 11f, Probability = 88f, Scale = new Vector2(0.8f, 1.25f) },
            new Species { Name = "Autumn sapling", Layer = LAutumn,
                Prefabs = new[]{ "RF_Sapling1", "RF_Sapling2" }, Chances = new[]{ 100f, 80f },
                Distance = 10f, Probability = 55f, Scale = new Vector2(0.8f, 1.3f), Slope = new Vector2(MinTreeSlope, 36f) },
            new Species { Name = "Autumn deadfall", Layer = LAutumn,
                Prefabs = new[]{ "RF_Log2", "RF_Stump1", "RF_Log1" }, Chances = new[]{ 100f, 70f, 70f },
                Distance = 22f, Probability = 40f, Scale = new Vector2(0.9f, 1.2f), Slope = new Vector2(0f, 30f) },

            // ---- FLAK TOWER (red) - open arena, dry grass, a few pines on the perimeter --------
            new Species { Name = "Flak pine", Layer = LFlak,
                Prefabs = new[]{ "RF_Tree1" }, Chances = new[]{ 100f },
                Distance = 24f, Probability = 40f, Scale = new Vector2(0.85f, 1.15f), Slope = new Vector2(3f, 30f) },

            // ---- MOUNTAIN (orange) - rocks lead, vegetation secondary --------------------------
            // Threshold 0.6 catches the mountain region AND genuine cliffs (both painted rock at
            // 1.0) while excluding the hinterland's 0.38 rock component.
            new Species { Name = "Mountain boulder", Layer = LRock, Threshold = 0.6f,
                Prefabs = new[]{ "RF_Boulder5", "RF_Boulder4", "RF_Boulder3" }, Chances = new[]{ 100f, 85f, 70f },
                Distance = 9f, Probability = 80f, Scale = new Vector2(0.7f, 1.6f), Slope = new Vector2(0f, 45f) },
            new Species { Name = "Mountain scrub tree", Layer = LRock, Threshold = 0.6f,
                Prefabs = new[]{ "RF_Tree1", "RF_Sapling2" }, Chances = new[]{ 100f, 90f },
                Distance = 19f, Probability = 45f, Scale = new Vector2(0.7f, 1.0f), Slope = new Vector2(MinTreeSlope, 38f) },

            // ---- EERIE FOREST (black) - sparse dead trees so the well reads at distance --------
            // RF_Stump2 is a 4.4 m broken trunk rather than a stump; it reads as a snag, which is
            // exactly the eerie-forest silhouette.
            new Species { Name = "Eerie dead tree", Layer = LEerie,
                Prefabs = new[]{ "RF_Tree2", "RF_Tree1" }, Chances = new[]{ 100f, 80f },
                Distance = 16f, Probability = 60f, Scale = new Vector2(0.9f, 1.25f), Slope = new Vector2(MinTreeSlope, 34f) },
            new Species { Name = "Eerie snag", Layer = LEerie,
                Prefabs = new[]{ "RF_Stump2", "RF_Stump1" }, Chances = new[]{ 100f, 70f },
                Distance = 18f, Probability = 45f, Scale = new Vector2(0.85f, 1.3f), Slope = new Vector2(0f, 34f) },

            // ---- BEACH (yellow) - near-empty sand, sparse driftwood and rock -------------------
            // Height floor matters more here than anywhere: sand is 68% of the terrain because the
            // seabed is below sea level. rejectUnderwater plus this floor keeps props on the actual
            // beach rather than scattered across 20 km² of ocean floor.
            new Species { Name = "Beach driftwood", Layer = LSand, Threshold = 0.6f,
                Prefabs = new[]{ "RF_Log1", "RF_Log3", "RF_Log2" }, Chances = new[]{ 100f, 35f, 80f },
                Distance = 25f, Probability = 22f, Scale = new Vector2(0.85f, 1.25f),
                Slope = new Vector2(0f, 14f), Height = new Vector2(SeaLevelY + 1f, 56f) },
            new Species { Name = "Beach rock", Layer = LSand, Threshold = 0.6f,
                Prefabs = new[]{ "RF_Boulder1", "RF_Boulder2" }, Chances = new[]{ 100f, 75f },
                Distance = 17f, Probability = 30f, Scale = new Vector2(0.7f, 1.3f),
                Slope = new Vector2(0f, 20f), Height = new Vector2(SeaLevelY + 0.5f, 58f) },

            // ---- HINTERLAND - not explorable; just enough silhouette to read from the shore ----
            new Species { Name = "Hinterland tree", Layer = LForest, Threshold = 0.3f,
                Prefabs = new[]{ "RF_Tree1", "RF_Tree4" }, Chances = new[]{ 100f, 45f },
                Distance = 25f, Probability = 70f, Scale = new Vector2(0.85f, 1.25f), Slope = new Vector2(MinTreeSlope, 34f) },

            // GLADE (L1) and OASIS (L6) get NO tree species on purpose - both must stay open.
            // Their surrounding ring comes from the neighbouring biome's falloff.
        };

        [MenuItem("Tools/Mr. Moonlight/Vegetation/Configure + Spawn Biome Vegetation")]
        public static void SetupMenu() => Setup();

        public static string Setup()
        {
            var terrain = Object.FindFirstObjectByType<Terrain>();
            if (terrain == null) return "No Terrain in scene.";

            TerrainData td = terrain.terrainData;
            var layerNames = td.terrainLayers.Select(l => l.name).ToArray();
            if (layerNames.Length != 8) return $"Expected 8 terrain layers, found {layerNames.Length}.";

            var go = GameObject.Find("VegetationSpawner");
            if (go == null) go = new GameObject("VegetationSpawner");
            var spawner = go.GetComponent<VegetationSpawner>() ?? go.AddComponent<VegetationSpawner>();

            spawner.terrains = new List<Terrain> { terrain };
            spawner.waterHeight = SeaLevelY;
            spawner.seed = 20260825;

            spawner.terrainSettings.treeDistance = 1500f;   // deliberate stress-test distance
            spawner.terrainSettings.billboardStart = 1500f; // our trees have no billboard LOD
            spawner.terrainSettings.maxMeshTrees = 4000;
            spawner.terrainSettings.preservePrefabLayer = true;
            spawner.terrainSettings.grassDistance = 110f;
            spawner.terrainSettings.grassDensity = 1f;

            spawner.treeTypes.Clear();

            var sb = new System.Text.StringBuilder();
            var missing = new List<string>();

            foreach (var s in Plan())
            {
                var type = SpawnerBase.TreeType.New();
                type.name = s.Name;
                type.seed = Mathf.Abs(s.Name.GetHashCode()) % 9999;
                type.enabled = true;
                type.probability = s.Probability;
                type.distance = Mathf.Clamp(s.Distance / Mathf.Sqrt(DensityMultiplier), 1f, 25f);
                type.scaleRange = s.Scale;
                type.slopeRange = s.Slope;
                type.heightRange = s.Height;
                type.curvatureRange = new Vector2(0f, 1f);
                type.rejectUnderwater = true;
                type.collisionCheck = true;
                type.sinkAmount = 0.15f;

                type.prefabs.Clear();
                for (int i = 0; i < s.Prefabs.Length; i++)
                {
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabRoot}/{s.Prefabs[i]}.prefab");
                    if (prefab == null) { missing.Add(s.Prefabs[i]); continue; }
                    type.prefabs.Add(new SpawnerBase.TreePrefab { prefab = prefab, probability = s.Chances[i] });
                }
                if (type.prefabs.Count == 0) continue;

                type.layerMasks.Clear();
                type.layerMasks.Add(new SpawnerBase.TerrainLayerMask(
                    layerNames[s.Layer], s.Layer, s.Threshold));

                spawner.treeTypes.Add(type);
                sb.AppendLine($"  {s.Name,-22} layer[{s.Layer}] thr={s.Threshold:F2} " +
                              $"dist={type.distance:F0}m prob={s.Probability:F0}% " +
                              $"slope {s.Slope.x:F1}-{s.Slope.y:F0} | {string.Join(", ", s.Prefabs)}");
            }

            spawner.CopySettingsToTerrains();
            EditorUtility.SetDirty(spawner);

            var head = new System.Text.StringBuilder();
            head.AppendLine($"Configured {spawner.treeTypes.Count} tree/prop species on '{terrain.name}':");
            if (missing.Count > 0) head.AppendLine("MISSING PREFABS: " + string.Join(", ", missing.Distinct()));
            head.Append(sb);
            return head.ToString();
        }
    }
}
