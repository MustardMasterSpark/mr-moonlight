using System.Text;
using UnityEditor;
using UnityEngine;

namespace MrMoonlight.EditorTools
{
    /// <summary>
    /// MRM-70 PSX material migration: URP/Lit -> Retro Shaders Pro's RetroLit (vegetation) /
    /// RetroTerrainLit (terrain).
    ///
    /// WHY THIS EXISTS
    /// ---------------
    /// Vertex snapping, affine texture wobble, and PS1-style colour/resolution limiting are all
    /// per-material shader features (Docs/pc-build-target.md §7) - no renderer feature can bolt them
    /// onto a URP/Lit material. RetroLit/RetroTerrainLit are BRG-safe (both include DOTS.hlsl), so
    /// this is verified safe for Flora's GPU-resident rendering.
    ///
    /// KEYWORD SYNC GOTCHA
    /// -------------------
    /// Retro Shaders Pro's KeywordEnum/Toggle properties only flip their shader keyword when drawn
    /// through RetroLitShaderGUI / RetroTerrainLitShaderGUI in the Inspector - setting the float
    /// value from script does NOT flip the keyword by itself. MaterialEditor.ApplyMaterialPropertyDrawers
    /// is the headless equivalent Unity ships for exactly this: it runs the same built-in
    /// Toggle/ToggleOff/KeywordEnum MaterialPropertyDrawers the Inspector runs on every repaint.
    /// _AlphaClip is [ToggleUI] (decorative only, no drawer attached), and the terrain-only
    /// _TERRAIN_BLEND_HEIGHT / _TERRAIN_INSTANCED_PERPIXEL_NORMAL keywords have no drawer either, so
    /// those three are synced by hand here, matching RetroLitShaderGUI.DrawSurfaceOptions and
    /// RetroTerrainLitShaderGUI.SetupMaterialKeywords line for line.
    ///
    /// PSX SETTING CHOICES - the project's approved RetroLit "profile". Any new vegetation material
    /// (new tree models, job 2) should land on exactly these values, not be re-tuned from scratch.
    ///
    /// Point Filtering ON, Vertex Snapping ON (both the shader defaults). Resolution Limit OFF,
    /// Colour Depth Limit OFF - neither has an actual on/off keyword, they are continuous values, so
    /// "off" means pushing the value past the point where it has any visible effect (resolution
    /// limit above any texture actually in the project; colour depth high enough that 256
    /// levels/channel reads as full precision).
    ///
    /// Affine Textures: originally migrated at 0 (off) per the initial reference screenshot: Carlos
    /// live-tuned it to 1 (full PS1-style affine warping) across every vegetation material on
    /// 2026-08-25 and asked to keep it - AffineTextureStrength_Approved below reflects that, not the
    /// screenshot default. Terrain has no equivalent property (RetroTerrainLit doesn't expose one).
    ///
    /// Run "Report" first - it changes nothing and prints exactly what "Apply" would do.
    /// Nothing here is committed to git, so a plain revert (GitHub Desktop "discard changes") is the
    /// rollback path if the look doesn't hold up.
    /// </summary>
    public static class PSXMaterialMigration
    {
        private const string RetroLitShaderName = "Retro Shaders Pro/Retro Lit";
        private const string RetroTerrainLitShaderName = "Retro Shaders Pro/Terrain/Lit";
        private const string UrpLitShaderName = "Universal Render Pipeline/Lit";
        private const string UrpTerrainLitShaderName = "Universal Render Pipeline/Terrain/Lit";

        private const string TerrainMaterialPath = "Assets/_Project/Art/Environment/Terrain/M_IslandTerrain.mat";

        private const int FilterMode_Point = 1;
        private const int SnapMode_View = 2;
        private const float AffineTextureStrength_Approved = 1f; // Carlos, 2026-08-25 - full affine warp, keep for all new vegetation materials
        private const int ResolutionLimit_Off = 8192; // >= any texture actually in the project -> lod clamps to 0
        private const int ColorBitDepth_Off = 256;     // 256 levels/channel reads as full precision

        private static readonly string[] VegetationMaterialPaths =
        {
            "Assets/_Project/Art/Environment/Vegetation/GrassFlowers/Materials/M_GFF_Grass01.mat",
            "Assets/_Project/Art/Environment/Vegetation/GrassFlowers/Materials/M_GFF_Grass02.mat",
            "Assets/_Project/Art/Environment/Vegetation/GrassFlowers/Materials/M_GFF_GrassFlower01.mat",
            "Assets/_Project/Art/Environment/Vegetation/GrassFlowers/Materials/M_GFF_GrassFlower02.mat",
            "Assets/_Project/Art/Environment/Vegetation/GrassFlowers/Materials/M_GFF_GrassFlower03.mat",
            "Assets/_Project/Art/Environment/Vegetation/GrassFlowers/Materials/M_GFF_GrassFlower04.mat",
            "Assets/_Project/Art/Environment/Vegetation/GrassFlowers/Materials/M_GFF_GrassFlower05.mat",
            "Assets/_Project/Art/Environment/Vegetation/GrassFlowers/Materials/M_GFF_GrassFlower06.mat",
            "Assets/_Project/Art/Environment/Vegetation/GrassFlowers/Materials/M_GFF_GrassFlower07.mat",
            "Assets/_Project/Art/Environment/Vegetation/GrassFlowers/Materials/M_GFF_GrassFlower08.mat",
            "Assets/_Project/Art/Environment/Vegetation/GrassFlowers/Materials/M_GFF_GrassFlower09.mat",
            "Assets/_Project/Art/Environment/Vegetation/GrassFlowers/Materials/M_GFF_GrassFlower10.mat",
            "Assets/_Project/Art/Environment/Vegetation/RetroRealism/Materials/M_RF_Boulders.mat",
            "Assets/_Project/Art/Environment/Vegetation/RetroRealism/Materials/M_RF_BranchFir.mat",
            "Assets/_Project/Art/Environment/Vegetation/RetroRealism/Materials/M_RF_BranchFirDead.mat",
            "Assets/_Project/Art/Environment/Vegetation/RetroRealism/Materials/M_RF_Bush.mat",
            "Assets/_Project/Art/Environment/Vegetation/RetroRealism/Materials/M_RF_Dirt.mat",
            "Assets/_Project/Art/Environment/Vegetation/RetroRealism/Materials/M_RF_Fern.mat",
            "Assets/_Project/Art/Environment/Vegetation/RetroRealism/Materials/M_RF_Trees.mat",
            "Assets/_Project/Art/Environment/Vegetation/RetroRealism/Materials/M_RF_TreesDead.mat",
            "Assets/_Project/Art/Environment/Vegetation/TerrainSampleAssets/Materials/M_TSA_Bush.mat",
            "Assets/_Project/Art/Environment/Vegetation/TerrainSampleAssets/Materials/M_TSA_BushDry.mat",
            "Assets/_Project/Art/Environment/Vegetation/TerrainSampleAssets/Materials/M_TSA_Fern.mat",
            "Assets/_Project/Art/Environment/Vegetation/TerrainSampleAssets/Materials/M_TSA_Grass.mat",
            "Assets/_Project/Art/Environment/Vegetation/TerrainSampleAssets/Materials/M_TSA_GrassC.mat",
            "Assets/_Project/Art/Environment/Vegetation/TerrainSampleAssets/Materials/M_TSA_GrassDry.mat",
            "Assets/_Project/Art/Environment/Vegetation/TerrainSampleAssets/Materials/M_TSA_Heather.mat",
            "Assets/_Project/Art/Environment/Vegetation/TerrainSampleAssets/Materials/M_TSA_Plant.mat",
        };

        [MenuItem("Tools/Mr. Moonlight/Rendering/Report PSX Material Migration")]
        public static void Report() => Run(dryRun: true);

        /// <summary>Same as Report(), but returns the text - the Unity console truncates multi-line logs.</summary>
        public static string ReportToString() => Run(dryRun: true);

        [MenuItem("Tools/Mr. Moonlight/Rendering/Migrate To PSX Materials (RetroLit)")]
        public static void Apply()
        {
            if (!EditorUtility.DisplayDialog(
                    "Migrate to PSX materials",
                    $"This swaps {VegetationMaterialPaths.Length} vegetation materials and the terrain " +
                    "material from URP/Lit to Retro Shaders Pro's RetroLit/RetroTerrainLit, and applies " +
                    "the approved profile: Point Filtering + Vertex Snapping on, full Affine Textures " +
                    "(vegetation only), Resolution/Colour-Depth limits off.\n\n" +
                    "Nothing here is committed to git, so this is a plain revert if the look doesn't hold up.\n\n" +
                    "Run Report first if you have not. Continue?",
                    "Migrate", "Cancel"))
                return;

            Run(dryRun: false);
        }

        /// <summary>
        /// Re-applies the retro property defaults + keyword sync to materials that are already on
        /// RetroLit/RetroTerrainLit. Exists because SetInt on the Integer-typed retro properties
        /// (_FilterMode, _SnapMode, _ResolutionLimit, _ColorBitDepth, _DitherMode, _LightMode,
        /// _ReceiveShadowsMode) silently zeroed them on save the first time this ran - fixed to
        /// SetFloat in ApplyCommonRetroDefaults, but already-migrated materials need this to pick
        /// the fix up since Run() skips anything already on the Retro shader.
        /// </summary>
        public static string RepairToString()
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== PSX Material Migration (REPAIR - re-sync retro properties) ===");
            int fixedCount = 0;

            foreach (var path in VegetationMaterialPaths)
            {
                var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat == null || mat.shader == null || mat.shader.name != RetroLitShaderName)
                {
                    sb.AppendLine($"SKIP (not on RetroLit): {path}");
                    continue;
                }

                bool alphaClip = mat.HasProperty("_AlphaClip") && mat.GetFloat("_AlphaClip") >= 0.5f;
                Undo.RecordObject(mat, "PSX Material Repair");
                mat.SetFloat("_AffineTextureStrength", AffineTextureStrength_Approved);
                ApplyCommonRetroDefaults(mat);
                SetKeyword(mat, "_ALPHATEST_ON", alphaClip);
                MaterialEditor.ApplyMaterialPropertyDrawers(mat);
                EditorUtility.SetDirty(mat);
                sb.AppendLine($"REPAIRED: {path}");
                fixedCount++;
            }

            var terrainMat = AssetDatabase.LoadAssetAtPath<Material>(TerrainMaterialPath);
            if (terrainMat != null && terrainMat.shader != null && terrainMat.shader.name == RetroTerrainLitShaderName)
            {
                Undo.RecordObject(terrainMat, "PSX Material Repair");
                ApplyCommonRetroDefaults(terrainMat);
                bool enableHeightBlend = terrainMat.HasProperty("_EnableHeightBlend") && terrainMat.GetFloat("_EnableHeightBlend") > 0f;
                SetKeyword(terrainMat, "_TERRAIN_BLEND_HEIGHT", enableHeightBlend);
                bool enableInstancedPerPixelNormal = terrainMat.HasProperty("_EnableInstancedPerPixelNormal") && terrainMat.GetFloat("_EnableInstancedPerPixelNormal") > 0f;
                SetKeyword(terrainMat, "_TERRAIN_INSTANCED_PERPIXEL_NORMAL", enableInstancedPerPixelNormal);
                MaterialEditor.ApplyMaterialPropertyDrawers(terrainMat);
                EditorUtility.SetDirty(terrainMat);
                sb.AppendLine($"REPAIRED (terrain): {TerrainMaterialPath}");
                fixedCount++;
            }

            sb.AppendLine();
            sb.AppendLine($"repaired={fixedCount}");

            if (fixedCount > 0)
                AssetDatabase.SaveAssets();

            var text = sb.ToString();
            Debug.Log(text);
            return text;
        }

        private static string Run(bool dryRun)
        {
            var sb = new StringBuilder();
            sb.AppendLine(dryRun ? "=== PSX Material Migration (REPORT, no changes) ===" : "=== PSX Material Migration (APPLYING) ===");

            int migrated = 0, skipped = 0, alreadyDone = 0;

            foreach (var path in VegetationMaterialPaths)
            {
                var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat == null)
                {
                    sb.AppendLine($"MISSING: {path}");
                    skipped++;
                    continue;
                }

                if (mat.shader != null && mat.shader.name == RetroLitShaderName)
                {
                    sb.AppendLine($"ALREADY MIGRATED: {path}");
                    alreadyDone++;
                    continue;
                }

                if (mat.shader == null || mat.shader.name != UrpLitShaderName)
                {
                    sb.AppendLine($"SKIP (unexpected shader '{mat.shader?.name}'): {path}");
                    skipped++;
                    continue;
                }

                var bumpTex = mat.HasProperty("_BumpMap") ? mat.GetTexture("_BumpMap") : null;
                var bumpScale = mat.HasProperty("_BumpScale") ? mat.GetFloat("_BumpScale") : 1f;
                bool alphaClip = mat.HasProperty("_AlphaClip") && mat.GetFloat("_AlphaClip") >= 0.5f;
                float cutoff = mat.HasProperty("_Cutoff") ? mat.GetFloat("_Cutoff") : 0.5f;

                sb.AppendLine($"{(dryRun ? "WOULD MIGRATE" : "MIGRATED")}: {path}");
                sb.AppendLine($"    bumpMap={(bumpTex != null ? bumpTex.name : "none")} alphaClip={alphaClip} cutoff={cutoff:0.00}");

                if (!dryRun)
                {
                    Undo.RecordObject(mat, "PSX Material Migration");
                    MigrateVegetationMaterial(mat, bumpTex, bumpScale, alphaClip);
                }

                migrated++;
            }

            var terrainMat = AssetDatabase.LoadAssetAtPath<Material>(TerrainMaterialPath);
            if (terrainMat == null)
            {
                sb.AppendLine($"MISSING TERRAIN MATERIAL: {TerrainMaterialPath}");
                skipped++;
            }
            else if (terrainMat.shader != null && terrainMat.shader.name == RetroTerrainLitShaderName)
            {
                sb.AppendLine($"ALREADY MIGRATED (terrain): {TerrainMaterialPath}");
                alreadyDone++;
            }
            else if (terrainMat.shader == null || terrainMat.shader.name != UrpTerrainLitShaderName)
            {
                sb.AppendLine($"SKIP TERRAIN (unexpected shader '{terrainMat.shader?.name}'): {TerrainMaterialPath}");
                skipped++;
            }
            else
            {
                sb.AppendLine($"{(dryRun ? "WOULD MIGRATE" : "MIGRATED")} (terrain): {TerrainMaterialPath}");
                if (!dryRun)
                {
                    Undo.RecordObject(terrainMat, "PSX Material Migration");
                    MigrateTerrainMaterial(terrainMat);
                }
                migrated++;
            }

            sb.AppendLine();
            sb.AppendLine($"migrated={migrated} alreadyDone={alreadyDone} skipped={skipped}");

            if (!dryRun && migrated > 0)
                AssetDatabase.SaveAssets();

            var text = sb.ToString();
            Debug.Log(text);
            return text;
        }

        private static void SetKeyword(Material mat, string keyword, bool enabled)
        {
            if (enabled) mat.EnableKeyword(keyword);
            else mat.DisableKeyword(keyword);
        }

        private static void ApplyCommonRetroDefaults(Material mat)
        {
            // These six are declared ShaderLab "Integer", which Unity 6 backs with a genuinely
            // separate m_Ints store from ordinary Float/Range properties (confirmed via
            // SerializedObject: a fresh RetroLit material already has an m_Ints entry for
            // _ResolutionLimit at the shader's own default, 64). SetInt/SetFloat write to the
            // Float store instead, so Unity logs "Property ... already exists in the property
            // sheet with a different type" and the write is silently dropped - GetInteger kept
            // reading the untouched shader default (64) after a SetInt round-trip that GetFloat
            // reported as if it had worked. SetInteger is the one that actually persists.
            mat.SetInteger("_FilterMode", FilterMode_Point);
            mat.SetInteger("_SnapMode", SnapMode_View);
            mat.SetInteger("_ResolutionLimit", ResolutionLimit_Off);
            mat.SetInteger("_ColorBitDepth", ColorBitDepth_Off);
            mat.SetFloat("_ColorBitDepthOffset", 0f);
            mat.SetInteger("_DitherMode", 0);          // Screen (shader default)
            mat.SetInteger("_LightMode", 1);           // TexelLit (shader default)
            mat.SetInteger("_ReceiveShadowsMode", 0);  // On (shader default)
        }

        private static void MigrateVegetationMaterial(Material mat, Texture bumpTex, float bumpScale, bool alphaClip)
        {
            mat.shader = Shader.Find(RetroLitShaderName);

            mat.SetFloat("_AffineTextureStrength", AffineTextureStrength_Approved);
            ApplyCommonRetroDefaults(mat);

            if (bumpTex != null)
            {
                mat.SetTexture("_NormalMap", bumpTex);
                mat.SetFloat("_NormalStrength", bumpScale);
            }

            if (alphaClip)
                mat.EnableKeyword("_ALPHATEST_ON");
            else
                mat.DisableKeyword("_ALPHATEST_ON");

            MaterialEditor.ApplyMaterialPropertyDrawers(mat);
            EditorUtility.SetDirty(mat);
        }

        private static void MigrateTerrainMaterial(Material mat)
        {
            mat.shader = Shader.Find(RetroTerrainLitShaderName);

            ApplyCommonRetroDefaults(mat);

            bool enableHeightBlend = mat.HasProperty("_EnableHeightBlend") && mat.GetFloat("_EnableHeightBlend") > 0f;
            SetKeyword(mat, "_TERRAIN_BLEND_HEIGHT", enableHeightBlend);

            bool enableInstancedPerPixelNormal = mat.HasProperty("_EnableInstancedPerPixelNormal") && mat.GetFloat("_EnableInstancedPerPixelNormal") > 0f;
            SetKeyword(mat, "_TERRAIN_INSTANCED_PERPIXEL_NORMAL", enableInstancedPerPixelNormal);

            MaterialEditor.ApplyMaterialPropertyDrawers(mat);
            EditorUtility.SetDirty(mat);
        }
    }
}
