using UnityEditor;
using UnityEngine;

namespace MrMoonlight.EditorTools
{
    /// <summary>
    /// Applies the texture import settings from Docs/3d-asset-pipeline.md §5.1
    /// automatically, based on the filename suffix.
    ///
    ///     Rock_BaseColor / _BC / _Albedo  -> sRGB, Point filter, 512
    ///     Rock_Normal    / _N             -> normal map, Bilinear, 256
    ///     Rock_Mask      / _M             -> linear, Bilinear, 256
    ///     Rock_Emission  / _E             -> sRGB, Point filter, 512
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

        private const int BaseColorSize = 512;
        private const int SupportMapSize = 256;

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
                    Configure(importer, sRGB: true, point: true, size: BaseColorSize);
                    break;

                case "emission":
                case "e":
                    Configure(importer, sRGB: true, point: true, size: BaseColorSize);
                    break;

                case "normal":
                case "n":
                    importer.textureType = TextureImporterType.NormalMap;
                    Configure(importer, sRGB: false, point: false, size: SupportMapSize);
                    break;

                case "mask":
                case "m":
                    Configure(importer, sRGB: false, point: false, size: SupportMapSize);
                    break;

                default:
                    return;
            }

            Debug.Log($"[MoonlightTextureImporter] Applied '{suffix}' preset to {assetPath}",
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

        /// <summary>Trailing _Token of the filename, lowercased. Empty if none.</summary>
        private static string SuffixOf(string path)
        {
            string stem = System.IO.Path.GetFileNameWithoutExtension(path);
            int i = stem.LastIndexOf('_');
            return i < 0 ? string.Empty : stem.Substring(i + 1).ToLowerInvariant();
        }
    }
}
