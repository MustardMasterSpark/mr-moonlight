using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace MrMoonlight.EditorTools
{
    /// <summary>
    /// Paints the Island terrain's splatmap from a biome definition.
    ///
    /// WHY THIS IS THE FIRST STEP, NOT THE LAST
    /// ----------------------------------------
    /// Vegetation Spawner places by rule, not inside hand-drawn regions. The one mechanism that
    /// separates one biome's planting from another's is its terrain-layer mask ("an item will only
    /// spawn on the materials added to this list"). So the painted ground IS the biome map: paint
    /// first, then mask each species to its biome's layer. See
    /// Docs/mrm70-biome-vegetation-strategy.md §2.
    ///
    /// The same 8 layers also drive the footstep sound system, so this one pass covers two of
    /// MRM-70's acceptance criteria.
    ///
    /// LAYER ORDER IS LOAD-BEARING - it must match Terrain.terrainData.terrainLayers exactly.
    /// Indices 0-3 live in splatmap 0's RGBA, 4-7 in splatmap 1's.
    ///
    /// HOW A TEXEL GETS ITS WEIGHTS
    /// ----------------------------
    ///   1. A base layer from height/slope alone, so all 29 km² of island reads sensibly even
    ///      outside the playable slice (rock up high and on cliffs, sand at the waterline,
    ///      forest grass otherwise).
    ///   2. Each biome region layered on top, weighted 1.0 inside and ramping to 0 across its
    ///      falloff band. Overlapping falloffs blend, which is what gives the smooth biome
    ///      transitions the brief asks for.
    ///   3. The beach overrides everything below BeachTopY with falloff 0 - the brief explicitly
    ///      wants forest-to-beach to cut on a rough line rather than gradient.
    ///   4. Weights normalised so they sum to 1 (Unity requires this).
    ///
    /// The region coordinates come from anchoring Map/biomes.png against the nine real blockout
    /// marker positions rather than eyeballing the screenshot - see strategy doc §3 for the
    /// derivation. They are still estimates read off an image: expect to nudge them.
    /// </summary>
    public static class BiomePainter
    {
        // Layer indices - must match the Terrain's layer array order.
        private const int LForest = 0; // TL_TSA_Ground_Grass_A
        private const int LGlade = 1; // TL_YFGM_Grass05
        private const int LAutumn = 2; // TL_YFGM_GrassLeafs01
        private const int LFlak = 3; // TL_YFGM_Dry02
        private const int LRock = 4; // TL_TSA_Ground_Rock
        private const int LSand = 5; // TL_TSA_Ground_Sand
        private const int LMoss = 6; // TL_TSA_Ground_Grass_Moss
        private const int LEerie = 7; // TL_TTP_GroundDryLeaves01

        private const int LayerCount = 8;

        // Sea plane sits at Y=40. Beach is the band just above it. Hard top edge on purpose.
        private const float BeachTopY = 54f;
        private const float RockSlopeDeg = 34f;
        private const float RockHighY = 150f;

        // Signature of the non-biome hinterland. Must stay clearly below the mask thresholds used
        // by the biome species (0.75 for forest) so the two are separable - see the long comment
        // at the base-layer step. Also has to look like plausible scrubby upland, since a good
        // chunk of the island's visible-but-unreachable area is painted with it.
        private const float WildGrass = 0.62f;
        private const float WildRock = 0.38f;

        private sealed class Region
        {
            public string Name;
            public int Layer;
            public float Falloff;      // metres; 0 = hard edge
            public Vector2 Circle;     // XZ centre, used when Radius > 0
            public float Radius;
            public Vector2[] Poly;     // XZ polygon, used when Radius <= 0
            public Rect Bounds;        // cached, incl. falloff
        }

        // Regions in WORLD XZ. +X = east, +Z = north (see strategy doc §3 - the older
        // "-Z = north" note in the kickoff doc was wrong).
        private static List<Region> BuildRegions() => new List<Region>
        {
            // --- southern half -------------------------------------------------------------
            new Region {
                Name = "Autumn forest + campsite", Layer = LAutumn, Falloff = 70f,
                Poly = new[] {
                    new Vector2( 730f, 4545f), new Vector2( 800f, 4120f), new Vector2(1000f, 3890f),
                    new Vector2(1250f, 3875f), new Vector2(1450f, 4060f), new Vector2(1550f, 4290f),
                    new Vector2(1550f, 4545f)
                }
            },
            new Region {
                Name = "Forest", Layer = LForest, Falloff = 70f,
                Poly = new[] {
                    new Vector2( 735f, 4550f), new Vector2(1550f, 4550f), new Vector2(1550f, 4915f),
                    new Vector2(1120f, 4990f), new Vector2( 765f, 4900f)
                }
            },
            new Region { Name = "Glade",      Layer = LGlade, Falloff = 18f, Circle = new Vector2(1405f, 4273f), Radius = 42f },

            // --- northern half -------------------------------------------------------------
            new Region { Name = "Mountain",   Layer = LRock,  Falloff = 60f, Circle = new Vector2( 872f, 5020f), Radius = 105f },
            new Region { Name = "Flak tower", Layer = LFlak,  Falloff = 55f, Circle = new Vector2(1355f, 4950f), Radius = 100f },
            new Region {
                Name = "Eerie forest", Layer = LEerie, Falloff = 45f,
                Poly = new[] {
                    new Vector2( 890f, 5320f), new Vector2(1065f, 5320f), new Vector2(1065f, 5700f),
                    new Vector2(1010f, 5795f), new Vector2( 935f, 5795f), new Vector2( 890f, 5690f)
                }
            },
            // Oasis sits INSIDE the eerie forest and must win locally, so it is listed last and
            // uses a tight falloff.
            new Region { Name = "Fountain oasis", Layer = LMoss, Falloff = 22f, Circle = new Vector2(1000f, 5490f), Radius = 58f },
        };

        [MenuItem("Tools/Mr. Moonlight/Vegetation/Paint Biomes Onto Terrain")]
        public static void PaintMenu() => Paint();

        public static string Paint()
        {
            var terrain = Object.FindFirstObjectByType<Terrain>();
            if (terrain == null) return "No Terrain in scene.";

            TerrainData td = terrain.terrainData;
            if (td.terrainLayers.Length != LayerCount)
                return $"Expected {LayerCount} terrain layers, found {td.terrainLayers.Length}. " +
                       "Assign the biome layers first (strategy doc §2).";

            var regions = BuildRegions();
            foreach (var r in regions) CacheBounds(r);

            int res = td.alphamapResolution;
            Vector3 size = td.size;
            Vector3 origin = terrain.transform.position;

            float[,,] map = new float[res, res, LayerCount];
            var w = new float[LayerCount];

            for (int z = 0; z < res; z++)
            {
                if ((z & 63) == 0)
                    EditorUtility.DisplayProgressBar("Painting biomes", $"row {z}/{res}", (float)z / res);

                float nz = z / (float)(res - 1);
                float wz = origin.z + nz * size.z;

                for (int x = 0; x < res; x++)
                {
                    float nx = x / (float)(res - 1);
                    float wx = origin.x + nx * size.x;

                    // NOTE: alphamap is indexed [z, x] but GetInterpolatedHeight/Normal take
                    // (normalisedX, normalisedZ). Mixing these up silently paints the map
                    // transposed, which on a 4103x7085 terrain is not subtle.
                    float h = td.GetInterpolatedHeight(nx, nz);
                    Vector3 n = td.GetInterpolatedNormal(nx, nz);
                    float slope = Vector3.Angle(n, Vector3.up);

                    for (int i = 0; i < LayerCount; i++) w[i] = 0f;

                    // 1. base layer from terrain shape alone.
                    //
                    // The hinterland is deliberately painted as a BLEND rather than pure forest
                    // grass, and this is load-bearing rather than cosmetic. The forest biome and
                    // the generic land outside it would otherwise both read as "layer 0 at weight
                    // 1.0", and a species masked to layer 0 would spawn across all 7.9 km² of it -
                    // roughly 90,000 trees instead of the ~3,500 the forest biome wants.
                    //
                    // Giving the hinterland a distinct signature (WildGrass/WildRock) lets the
                    // spawner's per-mask `threshold` separate them: a mask on layer 0 with
                    // threshold 0.75 fires only inside the real forest, while a sparse
                    // background species with threshold 0.3 covers the hinterland for distant
                    // silhouette, which is all the brief asks for out there.
                    //
                    // The spawner's mask test is effectively a hard cutoff at `threshold`, not a
                    // gradient - see the arithmetic in VegetationSpawner.Trees.cs where the
                    // 0-1 Random.value is compared against a 0-100 spawn chance.
                    if (slope >= RockSlopeDeg || h >= RockHighY)
                    {
                        w[LRock] = 1f;
                    }
                    else
                    {
                        w[LForest] = WildGrass;
                        w[LRock] = WildRock;
                    }

                    // 2. biome regions
                    var p = new Vector2(wx, wz);
                    foreach (var r in regions)
                    {
                        if (!r.Bounds.Contains(p)) continue;
                        float infl = Influence(r, p);
                        if (infl <= 0f) continue;

                        // A region asserts itself over whatever is underneath rather than simply
                        // adding, otherwise the base layer keeps bleeding through the middle of
                        // every biome.
                        for (int i = 0; i < LayerCount; i++) w[i] *= (1f - infl);
                        w[r.Layer] += infl;
                    }

                    // 3. beach overrides, hard edge
                    if (h < BeachTopY)
                    {
                        for (int i = 0; i < LayerCount; i++) w[i] = 0f;
                        w[LSand] = 1f;
                    }

                    // 4. normalise
                    float sum = 0f;
                    for (int i = 0; i < LayerCount; i++) sum += w[i];
                    if (sum <= 0.0001f) { w[LForest] = 1f; sum = 1f; }
                    for (int i = 0; i < LayerCount; i++) map[z, x, i] = w[i] / sum;
                }
            }

            EditorUtility.ClearProgressBar();
            td.SetAlphamaps(0, 0, map);
            terrain.Flush();

            return Summarise(map, res, td);
        }

        private static float Influence(Region r, Vector2 p)
        {
            float d = r.Radius > 0f
                ? Vector2.Distance(p, r.Circle) - r.Radius
                : SignedDistanceToPoly(p, r.Poly);

            if (d <= 0f) return 1f;
            if (r.Falloff <= 0f) return 0f;
            return Mathf.Clamp01(1f - d / r.Falloff);
        }

        /// <summary>Distance to polygon edge; negative inside.</summary>
        private static float SignedDistanceToPoly(Vector2 p, Vector2[] poly)
        {
            float best = float.MaxValue;
            bool inside = false;

            for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
            {
                Vector2 a = poly[i], b = poly[j];
                if ((a.y > p.y) != (b.y > p.y) &&
                    p.x < (b.x - a.x) * (p.y - a.y) / (b.y - a.y) + a.x)
                    inside = !inside;

                Vector2 ab = b - a;
                float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(ab.sqrMagnitude, 1e-6f));
                best = Mathf.Min(best, Vector2.Distance(p, a + t * ab));
            }

            return inside ? -best : best;
        }

        private static void CacheBounds(Region r)
        {
            float pad = r.Falloff + 2f;
            if (r.Radius > 0f)
            {
                float e = r.Radius + pad;
                r.Bounds = new Rect(r.Circle.x - e, r.Circle.y - e, e * 2f, e * 2f);
                return;
            }

            float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
            foreach (var v in r.Poly)
            {
                minX = Mathf.Min(minX, v.x); maxX = Mathf.Max(maxX, v.x);
                minY = Mathf.Min(minY, v.y); maxY = Mathf.Max(maxY, v.y);
            }
            r.Bounds = new Rect(minX - pad, minY - pad, (maxX - minX) + pad * 2f, (maxY - minY) + pad * 2f);
        }

        private static string Summarise(float[,,] map, int res, TerrainData td)
        {
            var total = new double[LayerCount];
            for (int z = 0; z < res; z++)
                for (int x = 0; x < res; x++)
                    for (int i = 0; i < LayerCount; i++)
                        total[i] += map[z, x, i];

            double all = 0; foreach (var v in total) all += v;
            double areaKm2 = (td.size.x * td.size.z) / 1e6;

            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"Painted {res}x{res} alphamap over {areaKm2:F1} km²:");
            for (int i = 0; i < LayerCount; i++)
                sb.AppendLine($"  [{i}] {td.terrainLayers[i].name,-26} {total[i] / all * 100f,6:F2}%  " +
                              $"~{total[i] / all * areaKm2:F2} km²");
            return sb.ToString();
        }
    }
}
