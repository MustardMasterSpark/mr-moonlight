using System;
using BRGInstancedRenderer;
using UnityEngine;

namespace MrMoonlight.Editor.BRG
{
    /// <summary>
    /// The BRG options that are baked into a prototype and so cannot change at runtime: they are written
    /// to a <see cref="BRGPrototypeExtraData"/> on every instance (trees, rocks) or detail prefab, and take
    /// effect after <i>Mr. Moonlight/BRG/Apply Groups And Baked Options</i> (Revert, write, Convert).
    /// One entry per category; the defaults change nothing, so applying them once only assigns groups.
    /// The live options (density, LOD bias, shadows on/off, shadow distance) are on the F11 bench
    /// (<c>BRGOptionsBench</c>). Owner: MRM-88, 2026-10-30, see <c>Docs/brg-options.md</c>.
    /// </summary>
    [CreateAssetMenu(fileName = "BRGBakedOptions", menuName = "Mr. Moonlight/BRG Baked Options")]
    public sealed class BRGBakedOptions : ScriptableObject
    {
        [Serializable]
        public sealed class Category
        {
            [Tooltip("Trees, DeadWood, Rocks or Details. Decides which objects this entry applies to.")]
            public string name;

            [Tooltip("Filled by the apply menu: the BRGPrototypeGroup asset of this category (live knobs on the F11 bench).")]
            public BRGPrototypeGroup group;

            [Header("Shadows")]
            [Tooltip("Shadow cascades these prototypes render into. 4 = all (default). 2 = only the two nearest cascades, so far ones cast no shadow.")]
            [Range(1, 4)] public int maxShadowCascade = 4;

            [Tooltip("LOD used for the shadow mesh. 0 = same as the visible LOD (default). Higher = a cheaper LOD casts the shadow.")]
            [Range(0, 3)] public int minShadowLOD;

            [Header("Distance density (random thinning with distance)")]
            public bool enableDistanceDensity;
            public DistanceDensityMode distanceDensityMode = DistanceDensityMode.RawDistance;
            [Tooltip("RawDistance mode: full density up to here (m).")]
            public float densityStartDistance = 50f;
            [Tooltip("RawDistance mode: minimum fraction from here on (m).")]
            public float densityEndDistance = 150f;
            [Tooltip("ScreenSize mode: screen size (%) where thinning starts.")]
            public float densityStartScreenSize = 2f;
            [Tooltip("Fraction kept at the far end (0-1).")]
            [Range(0f, 1f)] public float densityMinFraction = 0.3f;
            [Tooltip("Width of the per-instance fade (0 = hard pop).")]
            public float densityFadeWidth = 0.05f;

            [Header("Min screen size cull")]
            [Tooltip("Cull below this screen size (%). 0 = off (non-LOD prototypes then use the config's defaultSmallMeshSize). " +
                     "Mesh-LOD trees apply it as the last LOD's lower bound.")]
            public float minScreenSize;
            public MinScreenSizeFadeMode minScreenSizeFadeMode = MinScreenSizeFadeMode.None;
            [Range(0f, 1f)] public float minScreenSizeFadeWidth = 0.5f;

            public void WriteTo(BRGPrototypeExtraData d)
            {
                d.prototypeGroup = group;
                d.maxShadowCascade = maxShadowCascade;
                d.minShadowLOD = minShadowLOD;
                d.enableDistanceDensity = enableDistanceDensity;
                d.distanceDensityMode = distanceDensityMode;
                d.densityStartDistance = densityStartDistance;
                d.densityEndDistance = densityEndDistance;
                d.densityStartScreenSize = densityStartScreenSize;
                d.densityMinFraction = densityMinFraction;
                d.densityFadeWidth = densityFadeWidth;
                d.minScreenSize = minScreenSize;
                d.minScreenSizeFadeMode = minScreenSizeFadeMode;
                d.minScreenSizeFadeWidth = minScreenSizeFadeWidth;
            }
        }

        public Category[] categories =
        {
            new Category { name = BRGTrialSetup.Trees },
            new Category { name = BRGTrialSetup.DeadWood },
            new Category { name = BRGTrialSetup.Rocks },
            new Category { name = BRGTrialSetup.Details },
        };

        public Category Find(string categoryName) =>
            Array.Find(categories, c => c != null && c.name == categoryName);
    }
}
