using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using sc.terrain.vegetationspawner;

namespace MrMoonlight.EditorTools
{
    /// <summary>
    /// Configures the Vegetation Spawner's grass/detail layer per biome, then spawns it.
    /// Run after BiomeVegetationSetup.
    ///
    /// TWO KINDS OF DETAIL, AND WHY
    /// ----------------------------
    /// **Texture billboards** (`GrassType.Texture`) for the GrassFlowers cards. These have no
    /// source mesh - they were only ever billboard textures - and Unity's terrain detail system
    /// cross-quads and wind-animates them for free. This is also the fix for the 12 GFF *prefabs*
    /// being unusable: they were built as crossed double-quads, i.e. two MeshRenderers, and the
    /// terrain detail system requires a single MeshRenderer. Feeding the textures in directly is
    /// both cheaper and the spawner's own default mode.
    ///
    /// **Meshes** (`GrassType.Mesh`) for the real 3D understory - ferns, bushes, grass clumps from
    /// Terrain Sample Assets and RetroRealism. These are valid detail meshes: single MeshRenderer,
    /// no LODGroup (VegetationTerrainPrep deliberately leaves the Detail tier without one, because
    /// the terrain rejects detail prefabs that have an LODGroup).
    ///
    /// PER-BIOME COLOUR COMES FREE
    /// ---------------------------
    /// Grass prototypes carry `mainColor`/`secondaryColor` tints, so autumn gets orange grass and
    /// the eerie forest gets drained grey-brown from the SAME textures - no material variants, no
    /// extra texture memory. Only the trees need real material variants. `linkColors = false` lets
    /// the two ends of the gradient differ, which is what stops a tinted field reading as flat.
    ///
    /// `alignToGround` is set high here on purpose: Carlos's rule is that trees stay upright but
    /// small things may follow the slope, and grass is exactly the "small things" case. Trees
    /// physically cannot tilt (a TreeInstance stores only a Y rotation), so the rule is enforced on
    /// both sides automatically.
    ///
    /// DENSITY WARNING
    /// ---------------
    /// Terrain detail density is bounded by detail resolution, not by these numbers. The terrain is
    /// 4103 x 7085 m at detailResolution 1024, i.e. one detail cell every ~4.0 x 6.9 m, and each
    /// cell holds a clump rather than a single blade. If grass reads as patchy rather than
    /// continuous, that is the cause - raise detailResolution (memory cost scales with its square)
    /// rather than pushing probability past 100.
    /// </summary>
    public static class BiomeGrassSetup
    {
        private const string VegRoot = "Assets/_Project/Prefabs/World/Vegetation";
        private const string GffTex = "Assets/_Project/Art/Environment/Vegetation/GrassFlowers/Textures";

        private const int LForest = 0, LGlade = 1, LAutumn = 2, LFlak = 3;
        private const int LRock = 4, LSand = 5, LMoss = 6, LEerie = 7;

        private const float SeaLevelY = 40f;

        /// <summary>
        /// Coverage for a 100%-probability species in CoverageMode. The main dial for how lush the
        /// ground reads; watch frame time when raising it, since ground cover is fill-rate bound
        /// rather than triangle bound.
        ///
        /// Billboards and meshes need very different numbers and it is not a subtle difference.
        /// Grass cards are small and flat, so dense is exactly what you want. The mesh understory
        /// is ferns and bushes 1-2 m across; at the same coverage they stop reading as scattered
        /// undergrowth and fuse into a continuous chest-height hedge that walls the player in.
        /// Tried at 6/6 first - the forest floor became a solid green mass.
        /// </summary>
        /// Billboards sit high because the GrassFlowers cards are thin stalks on a mostly empty
        /// 512x512 - a single card covers very little, so at 2.5 the ground still read as bare.
        /// They are 2 triangles each, so this is cheap in geometry and paid for in fill rate.
        private const float MaxCoverageBillboard = 6f;
        private const float MaxCoverageMesh = 0.7f;

        // Biome tints. Autumn/eerie exist because their TREE materials are not built yet - the
        // ground layer carries the seasonal read on its own in the meantime.
        private static readonly Color GreenA = new Color(0.82f, 0.95f, 0.75f);
        private static readonly Color GreenB = new Color(0.55f, 0.78f, 0.45f);
        private static readonly Color AutumnA = new Color(1.00f, 0.72f, 0.34f);
        private static readonly Color AutumnB = new Color(0.80f, 0.42f, 0.16f);
        private static readonly Color DryA = new Color(0.95f, 0.85f, 0.50f);
        private static readonly Color DryB = new Color(0.72f, 0.60f, 0.30f);
        private static readonly Color EerieA = new Color(0.42f, 0.40f, 0.38f);
        private static readonly Color EerieB = new Color(0.24f, 0.23f, 0.26f);
        private static readonly Color MossA = new Color(0.62f, 0.92f, 0.80f);
        private static readonly Color MossB = new Color(0.30f, 0.62f, 0.58f);

        private sealed class Detail
        {
            public string Name;
            public int Layer;
            public float Threshold = 0.5f;
            public string Prefab;         // "Pack/Name" -> mesh detail
            public string Texture;        // file name in GffTex -> billboard detail
            public float Probability = 40f;
            public Vector2 Height = new Vector2(0.6f, 1.1f);
            public Vector2 Width = new Vector2(0.8f, 1.3f);
            public Color A = Color.white, B = Color.white;
            public Vector2 Slope = new Vector2(0f, 40f);
            public Vector2 WorldY = new Vector2(SeaLevelY, 1000f);
        }

        private static List<Detail> Plan() => new List<Detail>
        {
            // ---- FOREST: thick green understory ----------------------------------------------
            new Detail { Name="Forest grass",   Layer=LForest, Threshold=0.75f, Texture="T_GFF_Grass01_BaseColor",
                         Probability=95f, Height=new Vector2(0.5f,1.0f), A=GreenA, B=GreenB },
            new Detail { Name="Forest grass 2", Layer=LForest, Threshold=0.75f, Texture="T_GFF_Grass02_BaseColor",
                         Probability=70f, Height=new Vector2(0.4f,0.9f), A=GreenA, B=GreenB },
            new Detail { Name="Forest fern",    Layer=LForest, Threshold=0.75f, Prefab="TerrainSampleAssets/TSA_Fern_A",
                         Probability=55f, Height=new Vector2(0.7f,1.2f), A=GreenA, B=GreenB },
            new Detail { Name="Forest fern B",  Layer=LForest, Threshold=0.75f, Prefab="RetroRealism/RF_Fern2",
                         Probability=45f, Height=new Vector2(0.8f,1.3f), A=GreenA, B=GreenB },
            new Detail { Name="Forest bush",    Layer=LForest, Threshold=0.75f, Prefab="RetroRealism/RF_Bush1",
                         Probability=35f, Height=new Vector2(0.8f,1.3f), A=GreenA, B=GreenB },
            new Detail { Name="Forest clump",   Layer=LForest, Threshold=0.75f, Prefab="TerrainSampleAssets/TSA_Grass_A",
                         Probability=45f, Height=new Vector2(0.7f,1.1f), A=GreenA, B=GreenB },

            // ---- AUTUMN: warm, weedy, orange -------------------------------------------------
            new Detail { Name="Autumn grass",   Layer=LAutumn, Texture="T_GFF_Grass01_BaseColor",
                         Probability=95f, Height=new Vector2(0.5f,1.1f), A=AutumnA, B=AutumnB },
            new Detail { Name="Autumn weeds",   Layer=LAutumn, Prefab="TerrainSampleAssets/TSA_GrassDry_A",
                         Probability=70f, Height=new Vector2(0.6f,1.2f), A=AutumnA, B=AutumnB },
            new Detail { Name="Autumn heather", Layer=LAutumn, Prefab="TerrainSampleAssets/TSA_Heather_A",
                         Probability=55f, Height=new Vector2(0.6f,1.0f), A=AutumnA, B=AutumnB },
            new Detail { Name="Autumn bush",    Layer=LAutumn, Prefab="RetroRealism/RF_Bush3",
                         Probability=30f, Height=new Vector2(0.8f,1.2f), A=AutumnA, B=AutumnB },
            new Detail { Name="Autumn flower",  Layer=LAutumn, Texture="T_GFF_GrassFlower04_BaseColor",
                         Probability=25f, Height=new Vector2(0.5f,0.9f), A=AutumnA, B=AutumnB },

            // ---- GLADE: dense grass plain, sparse flowers ------------------------------------
            new Detail { Name="Glade grass",    Layer=LGlade, Texture="T_GFF_Grass01_BaseColor",
                         Probability=100f, Height=new Vector2(0.6f,1.2f), A=GreenA, B=GreenB },
            new Detail { Name="Glade flower A", Layer=LGlade, Texture="T_GFF_GrassFlower01_BaseColor",
                         Probability=40f, Height=new Vector2(0.5f,0.9f), A=Color.white, B=GreenA },
            new Detail { Name="Glade flower B", Layer=LGlade, Texture="T_GFF_GrassFlower06_BaseColor",
                         Probability=30f, Height=new Vector2(0.5f,0.9f), A=Color.white, B=GreenA },

            // ---- FLAK TOWER: dry golden meadow, flowers, no leaf litter ----------------------
            new Detail { Name="Flak dry grass", Layer=LFlak, Texture="T_GFF_Grass02_BaseColor",
                         Probability=100f, Height=new Vector2(0.7f,1.3f), A=DryA, B=DryB },
            new Detail { Name="Flak weeds",     Layer=LFlak, Prefab="TerrainSampleAssets/TSA_GrassDry_B",
                         Probability=70f, Height=new Vector2(0.7f,1.2f), A=DryA, B=DryB },
            new Detail { Name="Flak flower",    Layer=LFlak, Texture="T_GFF_GrassFlower03_BaseColor",
                         Probability=35f, Height=new Vector2(0.5f,0.9f), A=Color.white, B=DryA },

            // ---- MOUNTAIN: scrub in the pockets between rocks --------------------------------
            new Detail { Name="Mountain scrub", Layer=LRock, Threshold=0.6f, Prefab="TerrainSampleAssets/TSA_Heather_B",
                         Probability=35f, Height=new Vector2(0.5f,0.9f), A=GreenB, B=DryB, Slope=new Vector2(0f,45f) },
            new Detail { Name="Mountain plant", Layer=LRock, Threshold=0.6f, Prefab="TerrainSampleAssets/TSA_Plant_D",
                         Probability=30f, Height=new Vector2(0.5f,0.9f), A=GreenB, B=DryB, Slope=new Vector2(0f,45f) },

            // ---- FOUNTAIN OASIS: the lush exception -----------------------------------------
            new Detail { Name="Oasis grass",    Layer=LMoss, Texture="T_GFF_Grass01_BaseColor",
                         Probability=100f, Height=new Vector2(0.6f,1.1f), A=MossA, B=MossB },
            new Detail { Name="Oasis flower A", Layer=LMoss, Texture="T_GFF_GrassFlower02_BaseColor",
                         Probability=85f, Height=new Vector2(0.5f,1.0f), A=Color.white, B=MossA },
            new Detail { Name="Oasis flower B", Layer=LMoss, Texture="T_GFF_GrassFlower07_BaseColor",
                         Probability=75f, Height=new Vector2(0.5f,1.0f), A=Color.white, B=MossA },
            new Detail { Name="Oasis plant",    Layer=LMoss, Prefab="TerrainSampleAssets/TSA_Plant_A",
                         Probability=60f, Height=new Vector2(0.6f,1.1f), A=MossA, B=MossB },

            // ---- EERIE: sparse, drained of colour --------------------------------------------
            new Detail { Name="Eerie grass",    Layer=LEerie, Texture="T_GFF_Grass02_BaseColor",
                         Probability=45f, Height=new Vector2(0.4f,0.8f), A=EerieA, B=EerieB },
            new Detail { Name="Eerie weeds",    Layer=LEerie, Prefab="TerrainSampleAssets/TSA_GrassDry_C",
                         Probability=30f, Height=new Vector2(0.5f,0.9f), A=EerieA, B=EerieB },

            // ---- BEACH: almost nothing, only above the tideline ------------------------------
            new Detail { Name="Beach tuft",     Layer=LSand, Threshold=0.6f, Prefab="TerrainSampleAssets/TSA_GrassDry_A",
                         Probability=10f, Height=new Vector2(0.4f,0.8f), A=DryA, B=DryB,
                         Slope=new Vector2(0f,18f), WorldY=new Vector2(SeaLevelY + 2f, 58f) },

            // ---- HINTERLAND: thin cover so distant land does not read as bare ----------------
            new Detail { Name="Hinterland grass", Layer=LForest, Threshold=0.3f, Texture="T_GFF_Grass01_BaseColor",
                         Probability=45f, Height=new Vector2(0.4f,0.9f), A=GreenB, B=DryB },
        };

        [MenuItem("Tools/Mr. Moonlight/Vegetation/Configure + Spawn Grass")]
        public static void SetupMenu() => Setup();

        public static string Setup()
        {
            var terrain = Object.FindFirstObjectByType<Terrain>();
            if (terrain == null) return "No Terrain in scene.";
            var layerNames = terrain.terrainData.terrainLayers.Select(l => l.name).ToArray();

            var go = GameObject.Find("VegetationSpawner");
            if (go == null) return "No VegetationSpawner - run BiomeVegetationSetup first.";
            var spawner = go.GetComponent<VegetationSpawner>();

            spawner.grassPrefabs.Clear();

            var sb = new System.Text.StringBuilder();
            var missing = new List<string>();

            foreach (var d in Plan())
            {
                var g = new SpawnerBase.GrassPrefab
                {
                    name = d.Name,
                    enabled = true,
                    seed = Mathf.Abs(d.Name.GetHashCode()) % 9999,
                    probability = d.Probability,
                    heightRange = d.WorldY,
                    slopeRange = d.Slope,
                    curvatureRange = new Vector2(0f, 1f),
                    rejectUnderwater = true,
                    collisionCheck = false, // details have no colliders; checking is wasted work
                    minMaxHeight = d.Height,
                    minMaxWidth = d.Width,
                    noiseSize = 0.12f,
                    mainColor = d.A,
                    secondaryColor = d.B,
                    linkColors = false,

                    // REQUIRED. The terrain runs in CoverageMode (Unity 2022.2+), where
                    // DetailPrototype.density IS the coverage amount - and the spawner copies it
                    // straight across (`d.density = item.density` in VegetationSpawner.Grass.cs).
                    // GrassPrefab defaults it to 0, which spawns 27 prototypes and zero blades.
                    // Derived from Probability so the two cannot drift apart: a 100% species gets
                    // full coverage, a 10% beach tuft gets a tenth of it.
                    useDensityScaling = true,
                };
                // density depends on billboard-vs-mesh, so it is set after the type is known
                // (just below) rather than in this initialiser.

                if (!string.IsNullOrEmpty(d.Texture))
                {
                    var tex = AssetDatabase.LoadAssetAtPath<Texture2D>($"{GffTex}/{d.Texture}.png");
                    if (tex == null) { missing.Add(d.Texture); continue; }
                    g.type = SpawnerBase.GrassType.Texture;
                    g.billboard = tex;
                    g.renderAsBillboard = true;
                }
                else
                {
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{VegRoot}/{d.Prefab}.prefab");
                    if (prefab == null) { missing.Add(d.Prefab); continue; }
                    if (prefab.GetComponent<LODGroup>() != null)
                    {
                        missing.Add(d.Prefab + " (has LODGroup - terrain details reject it)");
                        continue;
                    }
                    g.type = SpawnerBase.GrassType.Mesh;
                    g.prefab = prefab;
                    g.renderAsBillboard = false;
                }

                float maxCov = g.type == SpawnerBase.GrassType.Texture ? MaxCoverageBillboard : MaxCoverageMesh;
                g.density = Mathf.Max(d.Probability / 100f * maxCov, 0.15f);

                g.layerMasks.Clear();
                g.layerMasks.Add(new SpawnerBase.TerrainLayerMask(layerNames[d.Layer], d.Layer, d.Threshold));

                spawner.grassPrefabs.Add(g);
                sb.AppendLine($"  {d.Name,-18} L{d.Layer} thr={d.Threshold:F2} " +
                              $"{(g.type == SpawnerBase.GrassType.Texture ? "billboard" : "mesh")} " +
                              $"p={d.Probability:F0}%");
            }

            EditorUtility.SetDirty(spawner);

            var head = new System.Text.StringBuilder();
            head.AppendLine($"Configured {spawner.grassPrefabs.Count} grass/detail prototypes:");
            if (missing.Count > 0) head.AppendLine("MISSING: " + string.Join(", ", missing.Distinct()));
            head.Append(sb);
            return head.ToString();
        }
    }
}
