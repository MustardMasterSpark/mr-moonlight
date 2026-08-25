// Copyright © Magnetic Arcade. All Rights Reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MA.Flora.Editor
{
    internal enum FloraRenderingInspectorNodeKind
    {
        Root,
        Source,
        Template,
        Lod,
        Draw,
        BatchDomain,
        Archetype,
        InstanceChunk,
        CullingChunk,
        GraphicsBuffer,
        ShaderProperty,
    }

    internal sealed class FloraRenderingInspectorShaderPropertyUsage
    {
        public int NameID;
        public string DisplayName;
        public FloraDiagnosticsBatchProperty RepresentativeProperty;
        public List<string> DisplayNameAliases { get; } = new();
        public List<FloraDiagnosticsBatchDomain> Domains { get; } = new();
        public List<FloraDiagnosticsTemplate> Templates { get; } = new();
        public List<FloraDiagnosticsDraw> Draws { get; } = new();
        public int DomainCount => Domains.Count;
        public int TemplateCount => Templates.Count;
        public int DrawCount => Draws.Count;
        public int OverriddenDomainCount => Domains.Count(domain => domain.Properties.Any(property => property.NameID == NameID && property.IsOverridden));
        public int InstanceBufferDomainCount => Domains.Count(domain => domain.Properties.Any(property => property.NameID == NameID && property.IsOverridden && property.IsPerInstance));
        public int SharedOverrideDomainCount => Domains.Count(domain => domain.Properties.Any(property => property.NameID == NameID && property.IsOverridden && !property.IsPerInstance));
        public long InstanceBufferBytes => Domains.Sum(domain => domain.Properties.Where(property => property.NameID == NameID && property.IsOverridden && property.IsPerInstance).Sum(property => property.SizeInBytes));
        public long SharedOverrideBytes => Domains.Sum(domain => domain.Properties.Where(property => property.NameID == NameID && property.IsOverridden && !property.IsPerInstance).Sum(property => property.SizeInBytes));
        public long OverriddenBytes => InstanceBufferBytes + SharedOverrideBytes;
        public bool HasPerInstanceUsage => Domains.Any(domain => domain.Properties.Any(property => property.NameID == NameID && property.IsPerInstance));
        public bool HasSharedUsage => Domains.Any(domain => domain.Properties.Any(property => property.NameID == NameID && !property.IsPerInstance));
    }

    internal sealed class FloraRenderingInspectorNode
    {
        private readonly List<string> m_SearchText = new();

        public int Id;
        public string Key;
        public string Name;
        public string Subtitle;
        public string HeaderSubtitle;
        public string BadgeText;
        public string CountText;
        public string Tooltip;
        public string Warning;
        public string IconClass;
        public string RowStyleClass;
        public string BadgeStyleClass;
        public Object Target;
        public Bounds? FrameBounds;
        public bool UseTargetThumbnail;
        public bool IsSectionHeader;
        public bool CountInSearch;
        public bool ShowBadgeOnRoot;
        public FloraRenderingInspectorNodeKind Kind;
        public FloraDiagnosticsSource Source;
        public FloraDiagnosticsTemplate Template;
        public FloraDiagnosticsLod Lod;
        public FloraDiagnosticsDraw Draw;
        public FloraDiagnosticsBatchDomain BatchDomain;
        public FloraDiagnosticsArchetype Archetype;
        public FloraDiagnosticsInstanceChunk InstanceChunk;
        public FloraDiagnosticsCullingChunk CullingChunk;
        public FloraDiagnosticsGraphicsBuffer GraphicsBuffer;
        public FloraRenderingInspectorShaderPropertyUsage ShaderPropertyUsage;
        public FloraRenderingInspectorNode Parent;

        public List<FloraRenderingInspectorNode> Children { get; } = new();
        public bool IsSelectable => Kind != FloraRenderingInspectorNodeKind.Root;
        public bool HasWarning => !string.IsNullOrEmpty(Warning);
        public IEnumerable<string> SearchText => m_SearchText;

        public static FloraRenderingInspectorNode Root(string key, string name, string countText, string iconClass, bool isSectionHeader = false, bool countInSearch = false)
        {
            return new FloraRenderingInspectorNode
            {
                Kind = FloraRenderingInspectorNodeKind.Root,
                Key = key,
                Name = name,
                HeaderSubtitle = name,
                CountText = countText,
                IconClass = iconClass,
                BadgeText = "Group",
                IsSectionHeader = isSectionHeader,
                CountInSearch = countInSearch,
            }.WithSearch(name, countText);
        }

        public static FloraRenderingInspectorNode ForSource(FloraDiagnosticsSource source, string key = null)
        {
            string warning = null;
            if (!source.IdentitySource && !source.RenderSource)
                warning = "Source objects are no longer resolvable.";
            else if (source.InstanceCount == 0)
                warning = "Source has no registered instances.";

            var target = source.PrimaryComponent ? source.PrimaryComponent : source.IdentitySource ? source.IdentitySource : source.RenderSource;
            return new FloraRenderingInspectorNode
            {
                Kind = FloraRenderingInspectorNodeKind.Source,
                Key = key ?? $"source:{source.Index}",
                Name = source.Name,
                Subtitle = GetSourceSubtitle(source),
                HeaderSubtitle = $"{source.Kind} source | Index {source.Index}",
                BadgeText = source.Kind,
                CountText = source.InstanceCount.ToString("n0"),
                Tooltip = $"{source.Kind} | {source.InstanceCount:n0} instances",
                Warning = warning,
                IconClass = GetSourceIcon(source),
                Target = target,
                UseTargetThumbnail = true,
                Source = source,
            }.WithSearch(source.Name, source.Kind, source.Type, source.Scene, ObjectName(source.IdentitySource), ObjectName(source.RenderSource));
        }

        public static FloraRenderingInspectorNode ForTemplate(FloraDiagnosticsTemplate template, string key = null)
        {
            return new FloraRenderingInspectorNode
            {
                Kind = FloraRenderingInspectorNodeKind.Template,
                Key = key ?? $"template:{template.Index}",
                Name = template.Name,
                Subtitle = $"{template.LodCount:n0} LODs, {template.DrawCount:n0} draws",
                HeaderSubtitle = $"Template {template.Index} | {template.Type}",
                BadgeText = "Template",
                CountText = template.InstanceCount.ToString("n0"),
                Tooltip = $"{template.InstanceCount:n0} instances | Domain {template.BatchDomainIndex} | {template.TriangleCount:n0} triangles",
                Warning = template.InstanceCount == 0 ? "Template has no registered instances." : null,
                IconClass = GetTemplateIcon(template),
                Target = template.RepresentativeRenderSource,
                Template = template,
            }.WithSearch(template.Name, template.Type, template.Flags, template.BatchDomainIndex.ToString(), ObjectName(template.RepresentativeRenderSource));
        }

        public static FloraRenderingInspectorNode ForLod(FloraDiagnosticsLod lod, string key)
        {
            return new FloraRenderingInspectorNode
            {
                Kind = FloraRenderingInspectorNodeKind.Lod,
                Key = key,
                Name = $"LOD {lod.Index}",
                Subtitle = $"{lod.CameraDrawCount:n0} camera draws | {lod.ShadowDrawCount:n0} shadow draws",
                HeaderSubtitle = $"LOD {lod.Index}",
                BadgeText = "LOD",
                CountText = lod.TriangleCount.ToString("n0"),
                IconClass = "lod",
                Lod = lod,
            }.WithSearch($"LOD {lod.Index}", lod.CameraDrawCount.ToString(), lod.ShadowDrawCount.ToString());
        }

        public static FloraRenderingInspectorNode ForDraw(FloraDiagnosticsDraw draw, string key = null)
        {
            var meshName = GetDrawMeshName(draw);
            var materialName = GetDrawMaterialName(draw);
            return new FloraRenderingInspectorNode
            {
                Kind = FloraRenderingInspectorNodeKind.Draw,
                Key = key ?? $"draw:{draw.Index}",
                Name = string.IsNullOrEmpty(meshName) ? $"Draw {draw.Index}" : $"LOD {draw.LodIndex} - {meshName}",
                Subtitle = $"LOD {draw.LodIndex}, {draw.CullingChunkCount:n0} chunks",
                HeaderSubtitle = $"Draw {draw.Index} | {draw.Topology}",
                BadgeText = "Draw",
                CountText = draw.CullingChunkCount.ToString("n0"),
                Tooltip = $"{draw.Name} | Domain {draw.BatchDomainIndex} | Sub Mesh {draw.SubMeshIndex} | {draw.Flags}",
                IconClass = "draw",
                Target = draw.Mesh ? draw.Mesh : draw.Material,
                Draw = draw,
            }.WithSearch(draw.Name, meshName, materialName, draw.Topology.ToString(), draw.Flags, ObjectName(draw.Mesh), ObjectName(draw.Material));
        }

        public static FloraRenderingInspectorNode ForBatchDomain(FloraDiagnosticsBatchDomain domain, string key = null)
        {
            return new FloraRenderingInspectorNode
            {
                Kind = FloraRenderingInspectorNodeKind.BatchDomain,
                Key = key ?? $"domain:{domain.Index}",
                Name = $"Graphics Domain {domain.Index}",
                Subtitle = $"{domain.Properties.Count:n0} properties",
                HeaderSubtitle = domain.BatchId,
                BadgeText = "Domain",
                CountText = FloraDiagnosticsUtility.FormatBytes(domain.LengthInBytes),
                Tooltip = $"{domain.BatchId} | {FloraDiagnosticsUtility.FormatBytes(domain.LengthInBytes)} | {domain.Flags}",
                IconClass = "domain",
                BatchDomain = domain,
            }.WithSearch(domain.BatchId, domain.Flags, domain.Index.ToString());
        }

        public static FloraRenderingInspectorNode ForArchetype(FloraDiagnosticsArchetype archetype, string key = null)
        {
            return new FloraRenderingInspectorNode
            {
                Kind = FloraRenderingInspectorNodeKind.Archetype,
                Key = key ?? $"archetype:{archetype.Index}",
                Name = string.IsNullOrEmpty(archetype.Name) ? $"Archetype {archetype.Index}" : archetype.Name,
                Subtitle = $"Template {archetype.TemplateIndex}",
                HeaderSubtitle = $"Archetype {archetype.Index}",
                BadgeText = "Archetype",
                CountText = archetype.InstanceCount.ToString("n0"),
                Tooltip = $"{archetype.ChunkCount:n0} chunks | {FloraDiagnosticsUtility.FormatArchetypeDifferentiators(archetype)}",
                IconClass = "archetype",
                Target = archetype.Owner,
                Archetype = archetype,
            }.WithSearch(archetype.Name, archetype.Tags, archetype.Scene, archetype.Flags);
        }

        public static FloraRenderingInspectorNode ForInstanceChunk(FloraDiagnosticsInstanceChunk chunk, string key = null)
        {
            return new FloraRenderingInspectorNode
            {
                Kind = FloraRenderingInspectorNodeKind.InstanceChunk,
                Key = key ?? $"chunk:{chunk.Index}",
                Name = $"Chunk {chunk.Index}",
                Subtitle = $"Archetype {chunk.ArchetypeIndex}",
                HeaderSubtitle = $"Instance chunk {chunk.Index}",
                BadgeText = "Chunk",
                CountText = $"{chunk.InstanceCount:n0}/{chunk.Capacity:n0}",
                Tooltip = $"Template {chunk.TemplateIndex} | Archetype {chunk.ArchetypeIndex} | Domain {chunk.BatchDomainIndex} | {chunk.Flags}",
                IconClass = "chunk",
                InstanceChunk = chunk,
            }.WithSearch(chunk.Index.ToString(), chunk.ArchetypeIndex.ToString(), chunk.TemplateIndex.ToString(), chunk.BatchDomainIndex.ToString(), chunk.Flags);
        }

        public static FloraRenderingInspectorNode ForCullingChunk(FloraDiagnosticsCullingChunk chunk, string key = null)
        {
            return new FloraRenderingInspectorNode
            {
                Kind = FloraRenderingInspectorNodeKind.CullingChunk,
                Key = key ?? $"grid:chunk:{chunk.Index}",
                Name = $"Grid Chunk {chunk.Index}",
                Subtitle = chunk.CellLevel >= 0 ? $"Level {chunk.CellLevel}" : $"Cell {chunk.CellIndex}",
                HeaderSubtitle = $"Template {chunk.TemplateIndex} | Archetype {chunk.ArchetypeIndex}",
                BadgeText = "Chunk",
                CountText = chunk.InstanceCount.ToString("n0"),
                Tooltip = $"Cell {chunk.CellIndex} | Template {chunk.TemplateIndex} | Domain {chunk.BatchDomainIndex}",
                IconClass = "grid-chunk",
                CullingChunk = chunk,
            }.WithSearch(chunk.Index.ToString(), chunk.CellIndex.ToString(), chunk.CellLocation, chunk.TemplateIndex.ToString(), chunk.ArchetypeIndex.ToString(), chunk.BatchDomainIndex.ToString());
        }

        public static FloraRenderingInspectorNode ForGraphicsBuffer(FloraDiagnosticsGraphicsBuffer buffer)
        {
            return new FloraRenderingInspectorNode
            {
                Kind = FloraRenderingInspectorNodeKind.GraphicsBuffer,
                Key = $"buffer:{buffer.Index}",
                Name = buffer.DisplayName,
                Subtitle = $"{buffer.StoreType} | {buffer.Target} | stride {buffer.Stride}",
                HeaderSubtitle = $"Graphics buffer {buffer.Index}",
                BadgeText = buffer.StoreType,
                CountText = FloraDiagnosticsUtility.FormatBytes(buffer.SizeInBytes),
                IconClass = "buffer",
                GraphicsBuffer = buffer,
            }.WithSearch(buffer.DisplayName, buffer.StoreType, buffer.Target, buffer.SizeInBytes.ToString());
        }

        public static FloraRenderingInspectorNode ForShaderProperty(FloraRenderingInspectorShaderPropertyUsage usage, string key = null)
        {
            var property = usage?.RepresentativeProperty;
            var displayName = usage == null || string.IsNullOrEmpty(usage.DisplayName) ? $"Property {usage?.NameID ?? 0}" : usage.DisplayName;
            var usageSummary = usage == null
                ? string.Empty
                : $"{Pluralize(usage.DomainCount, "graphics domain")}, {Pluralize(usage.TemplateCount, "template")}";
            var usageKind = usage == null
                ? "Property"
                : GetShaderPropertyBadgeText(usage);
            var footprintSummary = GetShaderPropertyFootprintSummary(usage);
            var subtitle = string.IsNullOrEmpty(footprintSummary)
                ? usageSummary
                : $"{usageSummary} | {footprintSummary}";

            return new FloraRenderingInspectorNode
            {
                Kind = FloraRenderingInspectorNodeKind.ShaderProperty,
                Key = key ?? $"property:{usage?.NameID ?? 0}",
                Name = displayName,
                Subtitle = subtitle,
                HeaderSubtitle = $"Shader property | Name ID {usage?.NameID ?? 0}",
                BadgeText = usageKind,
                CountText = (usage?.DomainCount ?? 0).ToString("n0"),
                Tooltip = property == null
                    ? subtitle
                    : $"{subtitle} | Name ID {usage.NameID} | Metadata 0x{property.MetadataValue:X8}",
                IconClass = GetShaderPropertyIconClass(usage),
                ShaderPropertyUsage = usage,
            }.WithSearch(displayName, usage?.NameID.ToString(), usageSummary, footprintSummary, property?.MetadataValue.ToString("X8"))
                .WithSearch(usage?.DisplayNameAliases.ToArray())
                .WithSearch(usage?.Domains.Select(domain => domain.BatchId).ToArray())
                .WithSearch(usage?.Domains.Select(domain => domain.Index.ToString()).ToArray())
                .WithSearch(usage?.Templates.Select(template => template.Name).ToArray())
                .WithSearch(usage?.Draws.Select(draw => draw.Name).ToArray());
        }

        private static string GetShaderPropertyBadgeText(FloraRenderingInspectorShaderPropertyUsage usage)
        {
            if (usage == null)
                return "Property";
            if (usage.InstanceBufferBytes > 0 && usage.SharedOverrideBytes > 0)
                return "Mixed";
            if (usage.InstanceBufferBytes > 0)
                return "Instance";
            if (usage.SharedOverrideBytes > 0)
                return "Shared";
            return "Metadata";
        }

        private static string GetShaderPropertyIconClass(FloraRenderingInspectorShaderPropertyUsage usage)
        {
            if (usage == null)
                return "property";
            if (usage.InstanceBufferBytes > 0 && usage.SharedOverrideBytes > 0)
                return "property-mixed";
            if (usage.InstanceBufferBytes > 0)
                return "property-instance";
            if (usage.SharedOverrideBytes > 0)
                return "property-shared";
            return "property-metadata";
        }

        private static string GetShaderPropertyFootprintSummary(FloraRenderingInspectorShaderPropertyUsage usage)
        {
            if (usage == null)
                return string.Empty;
            if (usage.InstanceBufferBytes > 0 && usage.SharedOverrideBytes > 0)
                return $"instance {FloraDiagnosticsUtility.FormatBytes(usage.InstanceBufferBytes)}, shared {FloraDiagnosticsUtility.FormatBytes(usage.SharedOverrideBytes)}";
            if (usage.InstanceBufferBytes > 0)
                return $"instance {FloraDiagnosticsUtility.FormatBytes(usage.InstanceBufferBytes)}";
            if (usage.SharedOverrideBytes > 0)
                return $"shared {FloraDiagnosticsUtility.FormatBytes(usage.SharedOverrideBytes)}";
            return "metadata only";
        }

        public void AddChild(FloraRenderingInspectorNode child)
        {
            child.Parent = this;
            Children.Add(child);
        }

        public FloraRenderingInspectorNode ShallowCopyWithoutChildren()
        {
            return (FloraRenderingInspectorNode)MemberwiseClone();
        }

        private FloraRenderingInspectorNode WithSearch(params string[] values)
        {
            if (values == null)
                return this;

            for (var i = 0; i < values.Length; i++)
            {
                if (!string.IsNullOrEmpty(values[i]))
                    m_SearchText.Add(values[i]);
            }

            return this;
        }

        internal static string ObjectName(Object obj) => obj ? obj.name : string.Empty;
        internal static string DisplayScene(string scene) => string.IsNullOrEmpty(scene) ? "None" : scene;
        internal static string Pluralize(int count, string singular)
            => $"{count:n0} {singular}{(count == 1 ? string.Empty : "s")}";

        internal static string DisplayAssetPath(Object obj)
        {
            if (!obj)
                return "None";

            var path = AssetDatabase.GetAssetPath(obj);
            return string.IsNullOrEmpty(path) ? "Scene Object" : path;
        }

        private static string GetSourceIcon(FloraDiagnosticsSource source)
        {
            return source.Kind switch
            {
                "Prefab" => "prefab",
                "Container" => "container",
                "Scene Renderer" => "renderer",
                _ => "source",
            };
        }

        private static string GetSourceSubtitle(FloraDiagnosticsSource source)
        {
            if (!source.IdentitySource && !source.RenderSource)
                return "Missing source";
            if (!string.IsNullOrEmpty(source.Scene))
                return $"Scene: {source.Scene}";
            if (source.Kind == "Container")
                return "Container";
            if (source.Kind == "Prefab")
                return "Prefab asset";
            return "Asset";
        }

        private static string GetDrawMeshName(FloraDiagnosticsDraw draw)
        {
            if (draw.Mesh)
                return draw.Mesh.name;

            var name = draw.Name ?? string.Empty;
            var separator = name.IndexOf(" / ", StringComparison.Ordinal);
            return separator > 0 ? name.Substring(0, separator) : string.Empty;
        }

        private static string GetDrawMaterialName(FloraDiagnosticsDraw draw)
        {
            if (draw.Material)
                return draw.Material.name;

            var name = draw.Name ?? string.Empty;
            var separator = name.IndexOf(" / ", StringComparison.Ordinal);
            return separator >= 0 && separator + 3 < name.Length ? name.Substring(separator + 3) : string.Empty;
        }

        private static string GetTemplateIcon(FloraDiagnosticsTemplate template)
        {
            return template.Type switch
            {
                "LodGroup" => "template-lodgroup",
                "MeshRenderer" => "template-meshrenderer",
                "MeshLod" => "template-meshlod",
                "Billboard" => "template-billboard",
                _ => "template",
            };
        }
    }
}
