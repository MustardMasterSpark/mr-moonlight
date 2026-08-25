// Copyright © Magnetic Arcade. All Rights Reserved.

using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MA.Flora.Editor
{
    internal enum EditorIconSize
    {
        Regular,
        Large,
    }

    internal static class EditorIcons
    {
        private const string CommonIconRootPath = "Packages/com.ma.flora/Editor/CommonUI/Icons";
        private const string RenderingInspectorIconRootPath = "Packages/com.ma.flora/Editor/RenderingInspector/Icons";
        private static readonly Dictionary<string, Texture2D> s_Cache = new();
        private static readonly Dictionary<string, string> s_PackageIconPathCache = new();

        public static bool TryGetThumbnail(Object target, EditorIconSize size, out Texture2D thumbnail)
        {
            thumbnail = GetThumbnail(target, size);
            return thumbnail;
        }

        public static Texture2D Get(string iconClass, EditorIconSize size = EditorIconSize.Regular)
        {
            Texture2D packageIcon = GetPackageIcon(iconClass, size);
            if (packageIcon)
                return packageIcon;

            string iconName = iconClass switch
            {
                "overview" => "UnityEditor.SceneView",
                "summary" => "d_UnityEditor.InspectorWindow",
                "info" => "console.infoicon",
                "more" => "_Menu",
                "group" => "Folder Icon",
                "prefab" => "Prefab Icon",
                "renderer" => "MeshRenderer Icon",
                "container" => "GameObject Icon",
                "template" => "PrefabVariant Icon",
                "template-lodgroup" => "LODGroup Icon",
                "template-meshrenderer" => "MeshRenderer Icon",
                "template-meshlod" => "Mesh Icon",
                "template-billboard" => "BillboardRenderer Icon",
                "lod" => "LODGroup Icon",
                "draw" => "Mesh Icon",
                "domain" => "ComputeShader Icon",
                "archetype" => "Grid.BoxTool",
                "chunk" => "Grid.BoxTool",
                "grid" => "Grid.BoxTool",
                "grid-block" => "Grid.BoxTool",
                "grid-cell" => "Grid.BoxTool",
                "culling" => "Camera Icon",
                "culling-cpu" => "d_UnityEditor.ProfilerWindow",
                "culling-gpu" => "ComputeShader Icon",
                "culling-chunk" => "Grid.BoxTool",
                "grid-chunk" => "Grid.BoxTool",
                "buffer" => "d_RenderTexture Icon",
                "property" => "Shader Icon",
                "property-instance" => "Shader Icon",
                "property-shared" => "Shader Icon",
                "property-mixed" => "Shader Icon",
                "property-metadata" => "Shader Icon",
                "warning" => "console.warnicon",
                "power" => "d_PowerButton",
                "refresh" => "Refresh",
                "settings" => "SettingsIcon",
                "add" => "CreateAddNew",
                "ping" => "d_ViewToolZoom",
                "select" => "UnityEditor.SceneHierarchyWindow",
                "frame" => "d_ViewToolZoom",
                "search" => "Search Icon",
                "filter" => "FilterByType",
                "source" => "GameObject Icon",
                _ => "GameObject Icon",
            };

            if (iconClass == "power")
                return FindFirst("d_PowerButton", "PowerButton", "d_preAudioLoopOff", "preAudioLoopOff") ?? Find("GameObject Icon");
            if (iconClass == "settings")
                return FindFirst("SettingsIcon", "d_SettingsIcon", "_Popup") ?? Find("GameObject Icon");

            return Find(iconName) ?? Find("GameObject Icon");
        }

        private static Texture2D GetPackageIcon(string iconClass, EditorIconSize size)
        {
            string path = GetPackageIconPath(iconClass, size);
            if (string.IsNullOrEmpty(path))
                return null;

            string cacheKey = $"asset:{path}";
            if (s_Cache.TryGetValue(cacheKey, out Texture2D texture))
                return texture;

            texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            s_Cache[cacheKey] = texture;
            return texture;
        }

        private static string GetPackageIconPath(string iconClass, EditorIconSize size)
        {
            string assetName = iconClass switch
            {
                "overview" => "World",
                "summary" => "Summary",
                "info" => null,
                "more" => "More",
                "group" => "Summary",
                "source" => "Source",
                "container" => "Source",
                "prefab" => "Prefab",
                "template" => "Template",
                "template-lodgroup" => "Template",
                "template-meshrenderer" => "Template",
                "template-meshlod" => "Template",
                "template-billboard" => "Template",
                "lod" => "LOD",
                "draw" => "Draw",
                "domain" => "BatchDomain",
                "archetype" => "Archetype",
                "chunk" => "Archetype",
                "grid" => "Culling",
                "grid-block" => "GridBlock",
                "grid-cell" => "GridCell",
                "culling" => "Culling",
                "culling-cpu" => "Culling",
                "culling-gpu" => "Culling",
                "culling-chunk" => "GridChunk",
                "grid-chunk" => "GridChunk",
                "buffer" => "GraphicsBuffer",
                "property" => "ShaderProperty",
                "property-instance" => "ShaderPropertyInstance",
                "property-shared" => "ShaderPropertyShared",
                "property-mixed" => "ShaderPropertyMixed",
                "property-metadata" => "ShaderPropertyMetadata",
                "search" => "Search",
                _ => null,
            };

            if (string.IsNullOrEmpty(assetName))
                return null;

            string theme = EditorGUIUtility.isProSkin ? "Dark" : "Light";
            int scale = size == EditorIconSize.Large || EditorGUIUtility.pixelsPerPoint > 1f ? 2 : 1;
            string cacheKey = $"{theme}:{assetName}:{scale}";
            if (s_PackageIconPathCache.TryGetValue(cacheKey, out string cachedPath))
                return cachedPath;

            string scaledSuffix = scale > 1 ? "@2x" : string.Empty;
            string commonPath = $"{CommonIconRootPath}/{theme}/{assetName}{scaledSuffix}.png";
            string renderingPath = $"{RenderingInspectorIconRootPath}/{theme}/{assetName}{scaledSuffix}.png";
            string resolvedPath = ResolveExistingPath(commonPath)
                ?? ResolveExistingPath(renderingPath)
                ?? (scale > 1
                    ? ResolveExistingPath($"{CommonIconRootPath}/{theme}/{assetName}.png")
                        ?? ResolveExistingPath($"{RenderingInspectorIconRootPath}/{theme}/{assetName}.png")
                    : null);

            s_PackageIconPathCache[cacheKey] = resolvedPath;
            return resolvedPath;
        }

        private static Texture2D GetThumbnail(Object target, EditorIconSize size)
        {
            if (!target)
                return null;

            if (size == EditorIconSize.Large)
            {
                Texture2D preview = AssetPreview.GetAssetPreview(target);
                if (preview)
                    return preview;
            }

            Texture2D miniThumbnail = AssetPreview.GetMiniThumbnail(target);
            if (miniThumbnail)
                return miniThumbnail;

            Texture image = EditorGUIUtility.ObjectContent(target, target.GetType()).image;
            return image as Texture2D;
        }

        private static Texture2D Find(string iconName)
        {
            if (s_Cache.TryGetValue(iconName, out Texture2D texture))
                return texture;

            texture = EditorGUIUtility.FindTexture(iconName);
            s_Cache[iconName] = texture;
            return texture;
        }

        private static Texture2D FindFirst(params string[] iconNames)
        {
            for (int i = 0; i < iconNames.Length; i++)
            {
                Texture2D texture = Find(iconNames[i]);
                if (texture)
                    return texture;
            }

            return null;
        }

        private static string ResolveExistingPath(string path)
        {
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path) != null ? path : null;
        }
    }
}
