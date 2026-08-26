using UnityEditor;
using UnityEngine;

namespace MrMoonlight.EditorTools
{
    /// <summary>
    /// Local terrain sculpting for composition - flattens the few places whose gameplay depends on
    /// being flat, and smooths the ground under them.
    ///
    /// WHY
    /// ---
    /// The MRM-58 block-out was generated from a heightmap at 1025 resolution over a 4103 x 7085 m
    /// terrain, i.e. one height sample every 4.0 x 6.9 m. That is fine for island silhouette and
    /// completely unsuitable for the handful of spots where the brief asks for a readable stage:
    ///
    ///   Glade      - "circular open plain ... open space highlights the telescope in the centre"
    ///                measured 8.3 m of fall across a 42 m radius.
    ///   Flak tower - "keep the area open ... a danger zone where enemies spawn"
    ///                measured 54.5 m across a 100 m radius, with part of it below the Y=40 sea
    ///                plane. Not somewhere a fight can happen.
    ///   Campsite   - needs a flat pad for tents; measured 19.5 m across 45 m.
    ///   Oasis      - the statue/well need a level floor; measured 19.5 m across 58 m.
    ///
    /// ORDER MATTERS: run this BEFORE BiomePainter. The painter's beach rule keys off terrain
    /// height (anything below Y=54 becomes sand), so sculpting after painting leaves the splatmap
    /// disagreeing with the ground.
    ///
    /// Each pad lerps toward a target height with a smooth falloff ring, so it blends into the
    /// surrounding hillside rather than stamping a mesa. Target defaults to the height at the pad
    /// centre, which keeps the pad sitting naturally in the existing landscape instead of dragging
    /// it up or down to an arbitrary number.
    /// </summary>
    public static class TerrainComposer
    {
        private struct Pad
        {
            public string Name;
            public float X, Z;      // world centre
            public float Radius;    // fully flat inside this
            public float Falloff;   // blends back to original across this band
            public float Target;    // NaN = use height at centre
        }

        private static readonly Pad[] Pads =
        {
            new Pad { Name = "Glade",      X = 1405f, Z = 4273f, Radius = 38f, Falloff = 34f, Target = float.NaN },
            new Pad { Name = "Campsite",   X = 1003f, Z = 4273f, Radius = 30f, Falloff = 28f, Target = float.NaN },
            new Pad { Name = "Oasis",      X = 1000f, Z = 5484f, Radius = 40f, Falloff = 32f, Target = float.NaN },
            new Pad { Name = "Flak tower", X = 1350f, Z = 4952f, Radius = 60f, Falloff = 55f, Target = float.NaN },
        };

        [MenuItem("Tools/Mr. Moonlight/Vegetation/Sculpt Composition Pads")]
        public static void SculptMenu() => Sculpt();

        public static string Sculpt()
        {
            var terrain = Object.FindFirstObjectByType<Terrain>();
            if (terrain == null) return "No Terrain in scene.";

            TerrainData td = terrain.terrainData;
            int res = td.heightmapResolution;
            Vector3 size = td.size;
            float[,] h = td.GetHeights(0, 0, res, res);

            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"Heightmap {res}x{res} = {size.x / (res - 1):F1} x {size.z / (res - 1):F1} m per sample");

            foreach (var pad in Pads)
            {
                float target = float.IsNaN(pad.Target)
                    ? td.GetInterpolatedHeight(pad.X / size.x, pad.Z / size.z)
                    : pad.Target;
                float targetNorm = target / size.y;

                float reach = pad.Radius + pad.Falloff;
                int x0 = Mathf.Max(0, Mathf.FloorToInt((pad.X - reach) / size.x * (res - 1)));
                int x1 = Mathf.Min(res - 1, Mathf.CeilToInt((pad.X + reach) / size.x * (res - 1)));
                int z0 = Mathf.Max(0, Mathf.FloorToInt((pad.Z - reach) / size.z * (res - 1)));
                int z1 = Mathf.Min(res - 1, Mathf.CeilToInt((pad.Z + reach) / size.z * (res - 1)));

                float before = -1f, after = -1f;
                float mn = 9999f, mx = -9999f;

                for (int z = z0; z <= z1; z++)
                {
                    float wz = z / (float)(res - 1) * size.z;
                    for (int x = x0; x <= x1; x++)
                    {
                        float wx = x / (float)(res - 1) * size.x;
                        float d = Mathf.Sqrt((wx - pad.X) * (wx - pad.X) + (wz - pad.Z) * (wz - pad.Z));
                        if (d > reach) continue;

                        // 1 inside the pad, smoothstepped to 0 across the falloff ring
                        float k = d <= pad.Radius
                            ? 1f
                            : Mathf.SmoothStep(1f, 0f, (d - pad.Radius) / pad.Falloff);

                        // heightmap is indexed [z, x]
                        float orig = h[z, x];
                        if (d <= pad.Radius)
                        {
                            mn = Mathf.Min(mn, orig * size.y);
                            mx = Mathf.Max(mx, orig * size.y);
                        }
                        h[z, x] = Mathf.Lerp(orig, targetNorm, k);
                    }
                }

                before = mx - mn;
                after = 0f;
                sb.AppendLine($"  {pad.Name,-11} r={pad.Radius,3}m target={target,6:F1}m  " +
                              $"spread {before,5:F1} m -> {after:F1} m");
            }

            td.SetHeights(0, 0, h);
            td.SyncHeightmap();
            terrain.Flush();

            EditorUtility.SetDirty(td);
            AssetDatabase.SaveAssets();

            sb.AppendLine("Heights written and saved. Re-run BiomePainter now - the beach rule reads height.");
            return sb.ToString();
        }
    }
}
