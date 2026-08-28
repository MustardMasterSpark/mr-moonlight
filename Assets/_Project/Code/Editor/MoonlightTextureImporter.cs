using UnityEditor;
using UnityEngine;

namespace MrMoonlight.EditorTools
{
    /// <summary>
    /// Applies the texture import settings from Docs/3d-asset-pipeline.md §5.1
    /// automatically, based on the filename suffix.
    ///
    ///     Rock_BaseColor / _BC / _Albedo  -> sRGB, Point filter
    ///     Rock_Normal    / _N             -> normal map, Bilinear, half res
    ///     Rock_Mask      / _M             -> linear, Bilinear, half res
    ///     Rock_Emission  / _E             -> sRGB, Point filter
    ///
    /// Max size is a CEILING per art category, not a forced size (MRM-72).
    /// The wizard asks Carlos for a resolution per prop and texture_pass.py
    /// writes the file at exactly that size; Unity's maxTextureSize only ever
    /// clamps, so the authored size wins as long as the ceiling is above it.
    /// A flat 512 cap silently threw away the answer to that question on
    /// anything larger - characters and weapons in particular.
    ///
    /// The point of this is that Filter Mode defaults to Bilinear, which blurs
    /// the quantised pixels back into mush and silently undoes the whole
    /// pixelation pass. Normal and Mask stay Bilinear deliberately - point
    /// filtering a normal map gives faceted lighting.
    ///
    /// Only touches files whose name ends in a recognised suffix and that live
    /// under Assets/_Project/Art/. Anything else imports normally.
    /// </summary>
    public sealed class MoonlightTextureImporter : AssetPostprocessor
    {
        private const string ArtRoot = "Assets/_Project/Art/";

        // Ceilings by category. Support maps (Normal/Mask) are half the
        // BaseColor ceiling - they carry lower-frequency information and
        // halving them is the cheapest quality-neutral saving available.
        private const int CharacterBaseColorSize = 2048;
        private const int WeaponBaseColorSize = 1024;
        private const int DefaultBaseColorSize = 512;

        // Below this, uncompressed beats DXT: a 128 RGBA32 is 64 KB where a
        // 512 DXT1 is 128 KB, and DXT banding fights the colour quantisation.
        private const int UncompressedBelow = 128;

        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(ArtRoot, System.StringComparison.OrdinalIgnoreCase))
                return;

            string suffix = SuffixOf(assetPath);
            if (string.IsNullOrEmpty(suffix))
                return;

            var importer = (TextureImporter)assetImporter;

            switch (suffix)
            {
                case "basecolor":
                case "bc":
                case "albedo":
                    Configure(importer, sRGB: true, point: true, size: CeilingFor(assetPath));
                    break;

                case "emission":
                case "e":
                    Configure(importer, sRGB: true, point: true, size: CeilingFor(assetPath));
                    break;

                case "normal":
                case "n":
                    importer.textureType = TextureImporterType.NormalMap;
                    Configure(importer, sRGB: false, point: false, size: CeilingFor(assetPath) / 2);
                    break;

                case "mask":
                case "m":
                    Configure(importer, sRGB: false, point: false, size: CeilingFor(assetPath) / 2);
                    break;

                default:
                    return;
            }

            Debug.Log($"[MoonlightTextureImporter] Applied '{suffix}' preset to {assetPath} " +
                      $"(ceiling {CeilingFor(assetPath)})",
                      AssetDatabase.LoadAssetAtPath<Object>(assetPath));
        }

        private static void Configure(TextureImporter importer, bool sRGB, bool point, int size)
        {
            importer.sRGBTexture = sRGB;
            importer.filterMode = point ? FilterMode.Point : FilterMode.Bilinear;
            importer.anisoLevel = 0;
            importer.maxTextureSize = size;
            importer.mipmapEnabled = true;
            importer.isReadable = false;
            importer.wrapMode = TextureWrapMode.Repeat;

            importer.textureCompression = size < UncompressedBelow
                ? TextureImporterCompression.Uncompressed
                : TextureImporterCompression.Compressed;

            // WebGL must be DXT - ASTC is a mobile format and every desktop
            // browser exposes S3TC through WebGL 2.0. See webgl-budget.md §5.
            importer.SetPlatformTextureSettings(new TextureImporterPlatformSettings
            {
                name = "WebGL",
                overridden = true,
                maxTextureSize = size,
                format = TextureImporterFormat.Automatic,
                textureCompression = importer.textureCompression,
                crunchedCompression = false,
            });
        }

        /// <summary>
        /// BaseColor size ceiling for the art category the asset lives in.
        /// A ceiling, not a target: Unity never upscales, so a 512 file under
        /// Characters/ still imports at 512.
        /// </summary>
        private static int CeilingFor(string path)
        {
            if (path.StartsWith(ArtRoot + "Characters/", System.StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith(ArtRoot + "Enemies/", System.StringComparison.OrdinalIgnoreCase))
                return CharacterBaseColorSize;

            if (path.StartsWith(ArtRoot + "Weapons/", System.StringComparison.OrdinalIgnoreCase))
                return WeaponBaseColorSize;

            return DefaultBaseColorSize;
        }

        /// <summary>Trailing _Token of the filename, lowercased. Empty if none.</summary>
        private static string SuffixOf(string path)
        {
            string stem = System.IO.Path.GetFileNameWithoutExtension(path);
            int i = stem.LastIndexOf('_');
            return i < 0 ? string.Empty : stem.Substring(i + 1).ToLowerInvariant();
        }
    }
}
