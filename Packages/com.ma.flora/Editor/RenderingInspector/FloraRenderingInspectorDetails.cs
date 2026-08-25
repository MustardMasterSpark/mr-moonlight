// Copyright © Magnetic Arcade. All Rights Reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UIElements;

namespace MA.Flora.Editor
{
    internal sealed class FloraRenderingInspectorDetails : VisualElement
    {
        private const string DetailsHeaderTemplatePath = "Packages/com.ma.flora/Editor/RenderingInspector/UXML/DetailsHeader.uxml";
        private const string DetailsIconModifierClassPrefix = "flora-rendering-inspector__details-icon--";
        private static VisualTreeAsset s_DetailsHeaderTemplate;

        private readonly Action<FloraRenderingInspectorNode> m_OnSelectNode;
        private readonly VisualElement m_SelectionStrip;
        private readonly ScrollView m_Content;
        private readonly HashSet<string> m_ExpandedSections = new();
        private readonly HashSet<string> m_CollapsedSections = new();
        private readonly HashSet<string> m_ShowAllRelationshipLists = new();
        private readonly Dictionary<string, int> m_ActiveLodTabs = new();
        private readonly Dictionary<string, int> m_SectionNameCounts = new();
        private FloraRenderingInspectorTabView m_LodTabView;

        private FloraRenderingInspectorModel m_Model = FloraRenderingInspectorModel.Empty();
        private FloraRenderingInspectorNode m_SelectedNode;

        public FloraRenderingInspectorDetails(Action<FloraRenderingInspectorNode> onSelectNode)
        {
            m_OnSelectNode = onSelectNode;
            AddToClassList("flora-rendering-inspector__details");

            m_SelectionStrip = CloneSelectionStrip();
            Add(m_SelectionStrip);

            m_Content = new ScrollView(ScrollViewMode.Vertical);
            m_Content.AddToClassList("flora-rendering-inspector__details-content");
            Add(m_Content);
        }

        public VisualElement GetTabElement(int index) => m_LodTabView?.GetTabElement(index);

        public void SetActiveTab(int index)
        {
            if (m_SelectedNode?.Template == null)
                return;

            m_ActiveLodTabs[GetLodTabKey(m_SelectedNode.Template)] = Mathf.Max(0, index);
            Rebuild();
        }

        public void Refresh(FloraRenderingInspectorModel model, FloraRenderingInspectorNode selectedNode)
        {
            CaptureExpandedSections();
            var scrollOffset = m_Content.scrollOffset;

            m_Model = model ?? FloraRenderingInspectorModel.Empty();
            m_SelectedNode = selectedNode;
            Rebuild();

            m_Content.schedule.Execute(() => m_Content.scrollOffset = scrollOffset);
        }

        private void CaptureExpandedSections()
        {
            foreach (var section in m_Content.Query<FloraRenderingInspectorSection>().ToList())
            {
                if (string.IsNullOrEmpty(section.name))
                    continue;

                if (section.value)
                {
                    m_ExpandedSections.Add(section.name);
                    m_CollapsedSections.Remove(section.name);
                }
                else
                {
                    m_ExpandedSections.Remove(section.name);
                    m_CollapsedSections.Add(section.name);
                }
            }
        }

        private void Rebuild()
        {
            m_Content.Clear();
            m_SectionNameCounts.Clear();
            m_LodTabView = null;

            BuildSelectionStrip(m_SelectedNode);

            if (m_SelectedNode == null)
            {
                BuildSnapshotDetails();
                FlattenSingleSectionIfNeeded();
                return;
            }

            if (m_SelectedNode.HasWarning)
                FloraRenderingInspectorElements.AddWarning(m_Content, m_SelectedNode.Warning);

            // Keep the details pane task-ordered: primary facts, navigation relationships, then object references.
            BuildInfo(m_SelectedNode);
            BuildRelationshipSections(m_SelectedNode);
            BuildReferenceSections(m_SelectedNode);

            FlattenSingleSectionIfNeeded();
        }

        private void BuildSelectionStrip(FloraRenderingInspectorNode node)
        {
            var icon = m_SelectionStrip.Q<VisualElement>("icon") ?? m_SelectionStrip.Q(className: "flora-rendering-inspector__details-icon");
            RemoveClassPrefix(icon, DetailsIconModifierClassPrefix);
            if (FloraRenderingInspectorIcons.TryGetThumbnail(node, EditorIconSize.Large, out var thumbnail))
                icon.style.backgroundImage = thumbnail;
            else
            {
                icon.style.backgroundImage = StyleKeyword.Null;
                icon.AddToClassList($"{DetailsIconModifierClassPrefix}{FloraRenderingInspectorIcons.GetStyleClass(node)}");
            }

            var title = m_SelectionStrip.Q<Label>("title") ?? m_SelectionStrip.Q<Label>(className: "flora-rendering-inspector__selection-title");
            title.text = node?.Name ?? "Flora Rendering Inspector";
            title.tooltip = title.text;

            var snapshot = m_Model.Snapshot;
            var subtitle = GetSelectionSubtitle(node, snapshot);
            var subtitleLabel = m_SelectionStrip.Q<Label>("subtitle") ?? m_SelectionStrip.Q<Label>(className: "flora-rendering-inspector__selection-subtitle");
            subtitleLabel.text = subtitle;
            subtitleLabel.tooltip = subtitle;

        }

        private static VisualElement CloneSelectionStrip()
        {
            s_DetailsHeaderTemplate ??= AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(DetailsHeaderTemplatePath);
            return FloraRenderingInspectorElements.CloneTemplateRoot(s_DetailsHeaderTemplate, "flora-rendering-inspector__selection-strip");
        }

        private static void RemoveClassPrefix(VisualElement element, string prefix)
        {
            foreach (var className in element.GetClasses().Where(className => className.StartsWith(prefix, StringComparison.Ordinal)).ToArray())
                element.RemoveFromClassList(className);
        }

        private static string GetSelectionSubtitle(FloraRenderingInspectorNode node, FloraDiagnosticsSnapshot snapshot)
        {
            if (node == null)
            {
                return snapshot != null && snapshot.IsSystemCreated
                ? $"{snapshot.RegisteredInstanceCount:n0} instances | {snapshot.DrawCount:n0} draws | {FloraDiagnosticsUtility.FormatBytes(snapshot.Memory.InstanceBufferBytes)}"
                : "No active Flora system";
            }

            return node.Kind switch
            {
                FloraRenderingInspectorNodeKind.Source => $"{node.Source.Kind} source",
                FloraRenderingInspectorNodeKind.Template => node.Template.Type.ToString(),
                FloraRenderingInspectorNodeKind.Lod => $"{node.Lod.CameraDrawCount + node.Lod.ShadowDrawCount:n0} draws | {node.Lod.TriangleCount:n0} triangles",
                FloraRenderingInspectorNodeKind.Draw => node.Draw.Topology.ToString(),
                FloraRenderingInspectorNodeKind.BatchDomain => node.BatchDomain.BatchId.ToString(),
                FloraRenderingInspectorNodeKind.Archetype => $"{node.Archetype.InstanceCount:n0} instances",
                FloraRenderingInspectorNodeKind.InstanceChunk => $"{node.InstanceChunk.InstanceCount:n0}/{node.InstanceChunk.Capacity:n0} instances",
                FloraRenderingInspectorNodeKind.CullingChunk => $"{node.CullingChunk.InstanceCount:n0} instances",
                FloraRenderingInspectorNodeKind.GraphicsBuffer => node.GraphicsBuffer.StoreType.ToString(),
                FloraRenderingInspectorNodeKind.ShaderProperty => node.ShaderPropertyUsage == null
                    ? node.HeaderSubtitle
                    : $"{FloraRenderingInspectorNode.Pluralize(node.ShaderPropertyUsage.DomainCount, "graphics domain")} | {FloraRenderingInspectorNode.Pluralize(node.ShaderPropertyUsage.TemplateCount, "template")}",
                _ => node.HeaderSubtitle,
            };
        }

        private void BuildSnapshotDetails()
        {
            var snapshot = m_Model.Snapshot;
            if (snapshot == null || !snapshot.IsSystemCreated)
            {
                var status = AddSection("Status", showCount: false).contentContainer;
                FloraRenderingInspectorElements.AddValueRow(status, "State", "No Flora system is currently created.");
                AddRenderingSurfaceSections(snapshot);
                return;
            }

            var summary = AddSection("Summary", showCount: false, summary: snapshot.IsRenderingEnabled ? "Rendering enabled" : "Rendering disabled").contentContainer;
            FloraRenderingInspectorElements.AddValueRow(summary, "State", snapshot.IsRenderingEnabled ? "Rendering enabled" : "Rendering disabled");
            FloraRenderingInspectorElements.AddValueRow(summary, "Captured", snapshot.CapturedAt.ToLongTimeString());
            FloraRenderingInspectorElements.AddValueRow(summary, "Instances", snapshot.RegisteredInstanceCount);
            FloraRenderingInspectorElements.AddValueRow(summary, "Sources", snapshot.SourceCount);
            FloraRenderingInspectorElements.AddValueRow(summary, "Templates", snapshot.TemplateCount);
            FloraRenderingInspectorElements.AddValueRow(summary, "Archetypes", snapshot.ArchetypeCount);
            FloraRenderingInspectorElements.AddValueRow(summary, "Chunks", snapshot.ChunkCount);
            FloraRenderingInspectorElements.AddValueRow(summary, "Draws", snapshot.DrawCount);
            BuildMemorySection();
            AddRenderingSurfaceSections(snapshot);
        }

        private void AddRenderingSurfaceSections(FloraDiagnosticsSnapshot snapshot)
        {
            BuildDrawsSection(snapshot);
            BuildDomainsSection(snapshot);
            BuildCullingSection();
            BuildGraphicsBuffersSection();
        }

        private void BuildInfo(FloraRenderingInspectorNode node)
        {
            switch (node.Kind)
            {
                case FloraRenderingInspectorNodeKind.Source:
                    BuildSourceInfo(node.Source);
                    break;
                case FloraRenderingInspectorNodeKind.Template:
                    BuildTemplateInfo(node.Template);
                    break;
                case FloraRenderingInspectorNodeKind.Lod:
                    BuildLodInfo(node.Lod);
                    break;
                case FloraRenderingInspectorNodeKind.Draw:
                    BuildDrawInfo(node.Draw);
                    break;
                case FloraRenderingInspectorNodeKind.BatchDomain:
                    BuildDomainInfo(node.BatchDomain);
                    break;
                case FloraRenderingInspectorNodeKind.Archetype:
                    BuildArchetypeInfo(node.Archetype);
                    break;
                case FloraRenderingInspectorNodeKind.InstanceChunk:
                    BuildInstanceChunkInfo(node.InstanceChunk);
                    break;
                case FloraRenderingInspectorNodeKind.CullingChunk:
                    BuildCullingChunkInfo(node.CullingChunk);
                    break;
                case FloraRenderingInspectorNodeKind.GraphicsBuffer:
                    BuildGraphicsBufferInfo(node.GraphicsBuffer);
                    break;
                case FloraRenderingInspectorNodeKind.ShaderProperty:
                    BuildShaderPropertyInfo(node.ShaderPropertyUsage);
                    break;
            }
        }

        private void BuildSourceInfo(FloraDiagnosticsSource source)
        {
            var overview = AddOverviewGrid();
            FloraRenderingInspectorElements.AddObjectRow(overview, "Identity Source", source.IdentitySource);
            FloraRenderingInspectorElements.AddObjectRow(overview, "Render Source", source.RenderSource);
            FloraRenderingInspectorElements.AddObjectRow(overview, "Primary Component", source.PrimaryComponent);
            FloraRenderingInspectorElements.AddValueRow(overview, "Kind", source.Kind);
            FloraRenderingInspectorElements.AddValueRow(overview, "Source Index", source.Index);
            FloraRenderingInspectorElements.AddValueRow(overview, "Scene", FloraRenderingInspectorNode.DisplayScene(source.Scene));
            FloraRenderingInspectorElements.AddValueRow(overview, "Asset Path", FloraRenderingInspectorNode.DisplayAssetPath(source.IdentitySource ? source.IdentitySource : source.RenderSource));
            FloraRenderingInspectorElements.AddValueRow(overview, "Layer", source.Layer.ToString());
            FloraRenderingInspectorElements.AddValueRow(overview, "Instances", source.InstanceCount);
            FloraRenderingInspectorElements.AddValueRow(overview, "Templates", source.TemplateCount);
            FloraRenderingInspectorElements.AddValueRow(overview, "Renderers", source.RendererCount);
            FloraRenderingInspectorElements.AddValueRow(overview, "Reference Count", source.RefCount);
            FloraRenderingInspectorElements.AddValueRow(overview, "Lightmap Index", source.LightmapIndex.ToString());
            FloraRenderingInspectorElements.AddValueRow(overview, "Related Chunks", source.TemplateIndices.Sum(templateIndex => m_Model.GetArchetypesForTemplate(templateIndex).Sum(archetype => archetype.ChunkCount)));
        }

        private void BuildTemplateInfo(FloraDiagnosticsTemplate template)
        {
            var overview = AddOverviewGrid();
            FloraRenderingInspectorElements.AddObjectRow(overview, "Representative Source", template.RepresentativeRenderSource);
            FloraRenderingInspectorElements.AddValueRow(overview, "Template Index", template.Index);
            FloraRenderingInspectorElements.AddValueRow(overview, "Type", template.Type);
            FloraRenderingInspectorElements.AddValueRow(overview, "Instances", template.InstanceCount);
            FloraRenderingInspectorElements.AddValueRow(overview, "Sources", template.SourceCount);
            FloraRenderingInspectorElements.AddValueRow(overview, "Chunks", template.ChunkCount);
            FloraRenderingInspectorElements.AddValueRow(overview, "Culling Chunks", template.CullingChunkCount);
            FloraRenderingInspectorElements.AddValueRow(overview, "Layer", template.Layer.ToString());
            FloraRenderingInspectorElements.AddValueRow(overview, "Graphics Domain", template.BatchDomainIndex.ToString());
            FloraRenderingInspectorElements.AddValueRow(overview, "Max Render Distance", template.MaxRenderDistance.ToString("0.###"));
            FloraRenderingInspectorElements.AddValueRow(overview, "LODs", template.LodCount);
            FloraRenderingInspectorElements.AddValueRow(overview, "Draws", template.DrawCount);
            FloraRenderingInspectorElements.AddValueRow(overview, "Triangles", template.TriangleCount);
            FloraRenderingInspectorElements.AddValueRow(overview, "Vertices", template.VertexCount > 0 ? template.VertexCount.ToString("n0") : "Unknown");
            FloraRenderingInspectorElements.AddValueRow(overview, "Unique Materials", template.MaterialCount);
            FloraRenderingInspectorElements.AddValueRow(overview, "Flags", template.Flags);
        }

        private void BuildLodInfo(FloraDiagnosticsLod lod)
        {
            AddLodRows(AddOverviewGrid(), lod);
        }

        private void BuildDrawInfo(FloraDiagnosticsDraw draw)
        {
            var overview = AddOverviewGrid();
            FloraRenderingInspectorElements.AddObjectRow(overview, "Mesh", draw.Mesh);
            FloraRenderingInspectorElements.AddObjectRow(overview, "Material", draw.Material);
            FloraRenderingInspectorElements.AddValueRow(overview, "Draw Index", draw.Index);
            FloraRenderingInspectorElements.AddValueRow(overview, "Range Index", draw.RangeIndex);
            FloraRenderingInspectorElements.AddValueRow(overview, "Graphics Domain", draw.BatchDomainIndex);
            FloraRenderingInspectorElements.AddValueRow(overview, "LOD", draw.LodIndex);
            FloraRenderingInspectorElements.AddValueRow(overview, "Sub Mesh", draw.SubMeshIndex);
            FloraRenderingInspectorElements.AddValueRow(overview, "Topology", draw.Topology.ToString());
            FloraRenderingInspectorElements.AddValueRow(overview, "Index Count", draw.IndexCount);
            FloraRenderingInspectorElements.AddValueRow(overview, "Primitives", draw.PrimitiveCount);
            FloraRenderingInspectorElements.AddValueRow(overview, "Triangles", draw.Topology == MeshTopology.Triangles ? draw.TriangleCount.ToString("n0") : "Not applicable");
            FloraRenderingInspectorElements.AddValueRow(overview, "Culling Chunks", draw.CullingChunkCount);
            FloraRenderingInspectorElements.AddValueRow(overview, "Flags", draw.Flags);
        }

        private void BuildDomainInfo(FloraDiagnosticsBatchDomain domain)
        {
            var overview = AddOverviewGrid();
            FloraRenderingInspectorElements.AddValueRow(overview, "Domain Index", domain.Index);
            FloraRenderingInspectorElements.AddValueRow(overview, "Batch ID", domain.BatchId);
            FloraRenderingInspectorElements.AddValueRow(overview, "Capacity", domain.InstanceCapacity);
            FloraRenderingInspectorElements.AddValueRow(overview, "Length", FloraDiagnosticsUtility.FormatBytes(domain.LengthInBytes));
            FloraRenderingInspectorElements.AddValueRow(overview, "Base Address", domain.BaseAddress.ToString());
            FloraRenderingInspectorElements.AddValueRow(overview, "Properties", domain.PropertyCount);
            FloraRenderingInspectorElements.AddValueRow(overview, "Overridden", domain.OverriddenPropertyCount);
            FloraRenderingInspectorElements.AddValueRow(overview, "Flags", domain.Flags);

        }

        private void BuildArchetypeInfo(FloraDiagnosticsArchetype archetype)
        {
            var overview = AddOverviewGrid();
            FloraRenderingInspectorElements.AddObjectRow(overview, "Owner", archetype.Owner);
            FloraRenderingInspectorElements.AddValueRow(overview, "Archetype Index", archetype.Index);
            FloraRenderingInspectorElements.AddValueRow(overview, "Template", archetype.TemplateIndex);
            FloraRenderingInspectorElements.AddValueRow(overview, "Instances", archetype.InstanceCount);
            FloraRenderingInspectorElements.AddValueRow(overview, "Chunks", archetype.ChunkCount);
            FloraRenderingInspectorElements.AddValueRow(overview, "Scene", FloraRenderingInspectorNode.DisplayScene(archetype.Scene));
            FloraRenderingInspectorElements.AddValueRow(overview, "Layer", archetype.Layer);
            FloraRenderingInspectorElements.AddValueRow(overview, "Lightmap Index", archetype.LightmapIndex);
            FloraRenderingInspectorElements.AddValueRow(overview, "Tags", archetype.Tags);
            FloraRenderingInspectorElements.AddValueRow(overview, "Flags", archetype.Flags);
            FloraRenderingInspectorElements.AddValueRow(overview, "Differentiators", FloraDiagnosticsUtility.FormatArchetypeDifferentiators(archetype));
        }

        private void BuildInstanceChunkInfo(FloraDiagnosticsInstanceChunk chunk)
        {
            var overview = AddOverviewGrid();
            FloraRenderingInspectorElements.AddValueRow(overview, "Chunk Index", chunk.Index);
            FloraRenderingInspectorElements.AddValueRow(overview, "Archetype", $"Archetype {chunk.ArchetypeIndex}");
            FloraRenderingInspectorElements.AddValueRow(overview, "Template", $"Template {chunk.TemplateIndex}");
            FloraRenderingInspectorElements.AddValueRow(overview, "Graphics Domain", $"Graphics Domain {chunk.BatchDomainIndex}");
            FloraRenderingInspectorElements.AddValueRow(overview, "Instances", chunk.InstanceCount);
            FloraRenderingInspectorElements.AddValueRow(overview, "Capacity", chunk.Capacity);
            FloraRenderingInspectorElements.AddValueRow(overview, "Instance Offset", chunk.InstanceOffset);
            FloraRenderingInspectorElements.AddValueRow(overview, "Flags", chunk.Flags);

            var relationships = AddSection("Relationships", showCount: false).contentContainer;
            var relationshipNodes = new List<FloraRenderingInspectorNode>();
            if (m_Model.ArchetypesByIndex.TryGetValue(chunk.ArchetypeIndex, out var archetype))
                relationshipNodes.Add(Canonical(FloraRenderingInspectorNode.ForArchetype(archetype)));
            if (m_Model.TemplatesByIndex.TryGetValue(chunk.TemplateIndex, out var template))
                relationshipNodes.Add(Canonical(FloraRenderingInspectorNode.ForTemplate(template)));
            if (m_Model.DomainsByIndex.TryGetValue(chunk.BatchDomainIndex, out var domain))
                relationshipNodes.Add(Canonical(FloraRenderingInspectorNode.ForBatchDomain(domain)));
            AddRelationshipRows(relationships, $"instance-chunk:{chunk.Index}:relationships", relationshipNodes);
        }

        private void BuildCullingChunkInfo(FloraDiagnosticsCullingChunk chunk)
        {
            var overview = AddOverviewGrid();
            FloraRenderingInspectorElements.AddValueRow(overview, "Chunk Index", chunk.Index);
            FloraRenderingInspectorElements.AddValueRow(overview, "Instances", chunk.InstanceCount);
            FloraRenderingInspectorElements.AddValueRow(overview, "Template", $"Template {chunk.TemplateIndex}");
            FloraRenderingInspectorElements.AddValueRow(overview, "Archetype", $"Archetype {chunk.ArchetypeIndex}");
            FloraRenderingInspectorElements.AddValueRow(overview, "Graphics Domain", $"Graphics Domain {chunk.BatchDomainIndex}");
            FloraRenderingInspectorElements.AddValueRow(overview, "Cell Index", chunk.CellIndex);
            FloraRenderingInspectorElements.AddValueRow(overview, "Cell Level", chunk.CellLevel >= 0 ? chunk.CellLevel.ToString() : "Unknown");
            FloraRenderingInspectorElements.AddValueRow(overview, "Cell Coordinates", FormatCoordinates(chunk.CellCoordinates));
            FloraRenderingInspectorElements.AddValueRow(overview, "Block Index", chunk.BlockIndex >= 0 ? chunk.BlockIndex.ToString() : "Unknown");
            FloraRenderingInspectorElements.AddValueRow(overview, "Block Level", chunk.BlockLevel >= 0 ? chunk.BlockLevel.ToString() : "Unknown");
            FloraRenderingInspectorElements.AddValueRow(overview, "Block Coordinates", chunk.BlockLevel >= 0 ? FormatCoordinates(chunk.BlockCoordinates) : "Unknown");
            FloraRenderingInspectorElements.AddValueRow(overview, "Cell In Block", chunk.CellIndexInBlock >= 0 ? chunk.CellIndexInBlock.ToString() : "Unknown");
            FloraRenderingInspectorElements.AddValueRow(overview, "Cell Size", chunk.CellSize.ToString("0.###"));
            FloraRenderingInspectorElements.AddValueRow(overview, "Center", FormatVector(chunk.CellCenter));
            FloraRenderingInspectorElements.AddValueRow(overview, "Size", FormatVector(chunk.CellBounds.size));
            FloraRenderingInspectorElements.AddValueRow(overview, "Min", FormatVector(chunk.CellBounds.min));
            FloraRenderingInspectorElements.AddValueRow(overview, "Max", FormatVector(chunk.CellBounds.max));

            var relationships = AddSection("Relationships", showCount: false).contentContainer;
            var relationshipNodes = new List<FloraRenderingInspectorNode>();
            if (m_Model.TemplatesByIndex.TryGetValue(chunk.TemplateIndex, out var template))
                relationshipNodes.Add(Canonical(FloraRenderingInspectorNode.ForTemplate(template)));
            if (m_Model.ArchetypesByIndex.TryGetValue(chunk.ArchetypeIndex, out var archetype))
                relationshipNodes.Add(Canonical(FloraRenderingInspectorNode.ForArchetype(archetype)));
            if (m_Model.DomainsByIndex.TryGetValue(chunk.BatchDomainIndex, out var domain))
                relationshipNodes.Add(Canonical(FloraRenderingInspectorNode.ForBatchDomain(domain)));
            AddRelationshipRows(relationships, $"chunk:{chunk.Index}:relationships", relationshipNodes);
        }

        private void BuildGraphicsBufferInfo(FloraDiagnosticsGraphicsBuffer buffer)
        {
            var overview = AddOverviewGrid();
            FloraRenderingInspectorElements.AddValueRow(overview, "Name", buffer.DisplayName);
            FloraRenderingInspectorElements.AddValueRow(overview, "Store Type", buffer.StoreType);
            FloraRenderingInspectorElements.AddValueRow(overview, "Size", FloraDiagnosticsUtility.FormatBytes(buffer.SizeInBytes));
            FloraRenderingInspectorElements.AddValueRow(overview, "Count", buffer.Count);
            FloraRenderingInspectorElements.AddValueRow(overview, "Stride", buffer.Stride);
            FloraRenderingInspectorElements.AddValueRow(overview, "Target", buffer.Target);
        }

        private void BuildShaderPropertyInfo(FloraRenderingInspectorShaderPropertyUsage usage)
        {
            if (usage == null)
                return;

            var property = usage.RepresentativeProperty;
            var overview = AddOverviewGrid();
            FloraRenderingInspectorElements.AddValueRow(overview, "Name", usage.DisplayName);
            FloraRenderingInspectorElements.AddValueRow(overview, "Name ID", usage.NameID);
            FloraRenderingInspectorElements.AddValueRow(overview, "Graphics Domains", usage.DomainCount);
            FloraRenderingInspectorElements.AddValueRow(overview, "Templates", usage.TemplateCount);
            FloraRenderingInspectorElements.AddValueRow(overview, "Draws", usage.DrawCount);
            FloraRenderingInspectorElements.AddValueRow(overview, "Overridden Domains", usage.OverriddenDomainCount);
            FloraRenderingInspectorElements.AddValueRow(overview, "Instance Domains", usage.InstanceBufferDomainCount);
            FloraRenderingInspectorElements.AddValueRow(overview, "Shared Domains", usage.SharedOverrideDomainCount);
            FloraRenderingInspectorElements.AddValueRow(overview, "Instance Buffer", FloraDiagnosticsUtility.FormatBytes(usage.InstanceBufferBytes));
            FloraRenderingInspectorElements.AddValueRow(overview, "Shared Override", FloraDiagnosticsUtility.FormatBytes(usage.SharedOverrideBytes));
            FloraRenderingInspectorElements.AddValueRow(overview, "Usage", usage.HasPerInstanceUsage && usage.HasSharedUsage ? "Mixed" : usage.HasPerInstanceUsage ? "Per Instance" : "Shared");
            FloraRenderingInspectorElements.AddValueRow(overview, "Type Size", property != null ? FloraDiagnosticsUtility.FormatBytes(property.TypeSizeInBytes) : "Unknown");
            FloraRenderingInspectorElements.AddValueRow(overview, "Elements", property != null ? property.ElementCount.ToString("n0") : "Unknown");
            FloraRenderingInspectorElements.AddValueRow(overview, "Metadata", property != null ? $"0x{property.MetadataValue:X8}" : "Unknown");
        }

        private void BuildRelationshipSections(FloraRenderingInspectorNode node)
        {
            if (node.Kind == FloraRenderingInspectorNodeKind.Template)
            {
                var template = node.Template;
                BuildLodTabs(template);

                var draws = AddSection("Draws", template.DrawIndices.Count).contentContainer;
                AddRelationshipRows(draws, $"template:{template.Index}:draws", template.DrawIndices
                    .Select(drawIndex => m_Model.DrawsByIndex.TryGetValue(drawIndex, out var draw) ? Canonical(FloraRenderingInspectorNode.ForDraw(draw)) : null)
                    .Where(drawNode => drawNode != null));

                var templateArchetypes = m_Model.GetArchetypesForTemplate(template.Index);
                var archetypes = AddSection("Archetypes", templateArchetypes.Count).contentContainer;
                AddRelationshipRows(archetypes, $"template:{template.Index}:archetypes", templateArchetypes
                    .Select(archetype => Canonical(FloraRenderingInspectorNode.ForArchetype(archetype))));

                var templateInstanceChunks = m_Model.GetInstanceChunksForTemplate(template.Index);
                var instanceChunks = AddSection("Instance Chunks", templateInstanceChunks.Count).contentContainer;
                AddRelationshipRows(instanceChunks, $"template:{template.Index}:instance-chunks", templateInstanceChunks
                    .Select(chunk => Canonical(FloraRenderingInspectorNode.ForInstanceChunk(chunk))));

                var templateCullingChunks = m_Model.GetCullingChunksForTemplate(template.Index);
                var gridChunkCount = m_Model.Snapshot?.HasCullingGridDetails == true ? templateCullingChunks.Count : template.CullingChunkCount;
                var cullingChunks = AddSection("Grid Chunks", gridChunkCount).contentContainer;
                AddRelationshipRows(cullingChunks, $"template:{template.Index}:grid-chunks", templateCullingChunks
                    .Select(chunk => Canonical(FloraRenderingInspectorNode.ForCullingChunk(chunk))));
                return;
            }

            if (node.Kind == FloraRenderingInspectorNodeKind.BatchDomain)
            {
                var domain = node.BatchDomain;

                var shaderProperties = m_Model.GetShaderPropertiesForDomain(domain.Index);
                var properties = AddSection("Shader Properties", shaderProperties.Count).contentContainer;
                AddRelationshipRows(properties, $"domain:{domain.Index}:shader-properties", shaderProperties
                    .Select(usage => Canonical(FloraRenderingInspectorNode.ForShaderProperty(usage))));

                var templates = m_Model.GetTemplatesForDomain(domain.Index);
                var templateRelationships = AddSection("Templates", templates.Count).contentContainer;
                AddRelationshipRows(templateRelationships, $"domain:{domain.Index}:templates", templates
                    .Select(template => Canonical(FloraRenderingInspectorNode.ForTemplate(template))));

                var draws = m_Model.GetDrawsForDomain(domain.Index);
                var drawRelationships = AddSection("Draws", draws.Count).contentContainer;
                AddRelationshipRows(drawRelationships, $"domain:{domain.Index}:draws", draws
                    .Select(draw => Canonical(FloraRenderingInspectorNode.ForDraw(draw))));
                return;
            }

            if (node.Children.Count == 0)
                return;

            var children = AddSection("Children", node.Children.Count).contentContainer;
            AddRelationshipRows(children, $"{node.Key}:children", node.Children);
        }

        private void BuildReferenceSections(FloraRenderingInspectorNode node)
        {
            if (node.Kind == FloraRenderingInspectorNodeKind.Source)
            {
                var refs = AddSection("Object References", node.Source.Renderers.Count + 2).contentContainer;
                FloraRenderingInspectorElements.AddObjectRow(refs, "LOD Group", node.Source.LodGroup);
                FloraRenderingInspectorElements.AddObjectRow(refs, "Additional Settings", node.Source.AdditionalSettings);
                FloraRenderingInspectorElements.AddObjectList(refs, "Renderers", node.Source.Renderers);
                return;
            }

            if (node.Kind == FloraRenderingInspectorNodeKind.Template)
            {
                var sources = AddSection("Sources", node.Template.SourceIndices.Count).contentContainer;
                AddRelationshipRows(sources, $"template:{node.Template.Index}:sources", node.Template.SourceIndices
                    .Select(sourceIndex => m_Model.SourcesByIndex.TryGetValue(sourceIndex, out var source) ? Canonical(FloraRenderingInspectorNode.ForSource(source)) : null)
                    .Where(sourceNode => sourceNode != null));

                var domain = AddSection("Graphics Domain", 1).contentContainer;
                if (m_Model.DomainsByIndex.TryGetValue(node.Template.BatchDomainIndex, out var batchDomain))
                    AddRelationshipRows(domain, $"template:{node.Template.Index}:domain", new[] { Canonical(FloraRenderingInspectorNode.ForBatchDomain(batchDomain)) });
                return;
            }

            if (node.Kind == FloraRenderingInspectorNodeKind.Draw)
            {
                var relatedCullingChunks = m_Model.GetTemplatesForDraw(node.Draw.Index)
                    .SelectMany(template => m_Model.GetCullingChunksForTemplate(template.Index))
                    .ToArray();
                var relatedChunks = AddSection("Grid Chunks", relatedCullingChunks.Length).contentContainer;
                AddRelationshipRows(relatedChunks, $"draw:{node.Draw.Index}:grid-chunks", relatedCullingChunks
                    .Select(chunk => Canonical(FloraRenderingInspectorNode.ForCullingChunk(chunk))));
                return;
            }

            if (node.Kind == FloraRenderingInspectorNodeKind.Archetype)
            {
                var relationships = AddSection("Relationships", showCount: false).contentContainer;
                var relationshipNodes = new List<FloraRenderingInspectorNode>();
                if (m_Model.TemplatesByIndex.TryGetValue(node.Archetype.TemplateIndex, out var template))
                    relationshipNodes.Add(Canonical(FloraRenderingInspectorNode.ForTemplate(template)));
                relationshipNodes.AddRange(m_Model.GetInstanceChunksForArchetype(node.Archetype.Index)
                    .Select(chunk => Canonical(FloraRenderingInspectorNode.ForInstanceChunk(chunk))));
                relationshipNodes.AddRange(m_Model.GetCullingChunksForArchetype(node.Archetype.Index)
                    .Select(chunk => Canonical(FloraRenderingInspectorNode.ForCullingChunk(chunk))));
                AddRelationshipRows(relationships, $"archetype:{node.Archetype.Index}:relationships", relationshipNodes);
                return;
            }

            if (node.Kind == FloraRenderingInspectorNodeKind.ShaderProperty && node.ShaderPropertyUsage != null)
            {
                var usage = node.ShaderPropertyUsage;
                var domains = AddSection("Graphics Domains", usage.DomainCount).contentContainer;
                AddRelationshipRows(domains, $"property:{usage.NameID}:domains", usage.Domains.Select(domain => Canonical(FloraRenderingInspectorNode.ForBatchDomain(domain))));

                var templates = AddSection("Templates", usage.TemplateCount).contentContainer;
                AddRelationshipRows(templates, $"property:{usage.NameID}:templates", usage.Templates.Select(template => Canonical(FloraRenderingInspectorNode.ForTemplate(template))));

                var draws = AddSection("Draws", usage.DrawCount).contentContainer;
                AddRelationshipRows(draws, $"property:{usage.NameID}:draws", usage.Draws.Select(draw => Canonical(FloraRenderingInspectorNode.ForDraw(draw))));
            }
        }

        private void BuildMemorySection()
        {
            var snapshot = m_Model.Snapshot;
            var memory = AddSection("Memory", showCount: false, summary: FloraDiagnosticsUtility.FormatBytes(snapshot.Memory.InstanceBufferBytes)).contentContainer;
            FloraRenderingInspectorElements.AddValueRow(memory, "Instance Buffer", FloraDiagnosticsUtility.FormatBytes(snapshot.Memory.InstanceBufferBytes));
            FloraRenderingInspectorElements.AddValueRow(memory, "Graphics Domains", FloraDiagnosticsUtility.FormatBytes(snapshot.Memory.BatchDomainLayoutBytes));
            FloraRenderingInspectorElements.AddValueRow(memory, "Graphics Buffers", FloraDiagnosticsUtility.FormatBytes(snapshot.Memory.GraphicsBufferBytes));
            FloraRenderingInspectorElements.AddValueRow(memory, "Graphics Buffer Count", snapshot.Memory.GraphicsBufferCount);
            FloraRenderingInspectorElements.AddValueRow(memory, "Metadata Estimate", FloraDiagnosticsUtility.FormatBytes(snapshot.Memory.TemplateDataBytes + snapshot.Memory.ArchetypeDataBytes));
        }

        private void BuildDrawsSection(FloraDiagnosticsSnapshot snapshot)
        {
            var draws = AddSection("Draws", snapshot?.Draws.Count ?? 0).contentContainer;
            if (snapshot == null)
                return;

            AddRelationshipRows(draws, "summary:draws", snapshot.Draws
                .OrderBy(draw => draw.Index)
                .Select(draw => Canonical(FloraRenderingInspectorNode.ForDraw(draw))));
        }

        private void BuildDomainsSection(FloraDiagnosticsSnapshot snapshot)
        {
            var domains = AddSection("Graphics Domains", snapshot?.BatchDomains.Count ?? 0).contentContainer;
            if (snapshot == null)
                return;

            AddRelationshipRows(domains, "summary:domains", snapshot.BatchDomains
                .OrderBy(domain => domain.Index)
                .Select(domain => Canonical(FloraRenderingInspectorNode.ForBatchDomain(domain))));
        }

        private void BuildCullingSection()
        {
            var snapshot = m_Model.Snapshot;
            if (snapshot == null || !snapshot.HasCullingGridDetails)
            {
                var culling = AddSection("Grid", 0).contentContainer;
                FloraRenderingInspectorElements.AddValueRow(culling, "Chunks", "Grid details are not captured for this snapshot.");
                return;
            }

            if (snapshot.CullingChunks.Count == 0)
            {
                var culling = AddSection("Grid", 0).contentContainer;
                FloraRenderingInspectorElements.AddValueRow(culling, "Chunks", "No grid chunks are loaded.");
                return;
            }

            var chunks = AddSection("Grid Chunks", snapshot.CullingChunks.Count).contentContainer;
            AddRelationshipRows(chunks, "summary:grid:chunks", snapshot.CullingChunks
                .OrderBy(chunk => chunk.TemplateIndex)
                .ThenBy(chunk => chunk.CellIndex)
                .ThenBy(chunk => chunk.Index)
                .Select(chunk => Canonical(FloraRenderingInspectorNode.ForCullingChunk(chunk))));
        }

        private void BuildGraphicsBuffersSection()
        {
            var snapshot = m_Model.Snapshot;
            var buffers = AddSection("Graphics Buffers", snapshot?.GraphicsBuffers.Count ?? 0).contentContainer;
            if (snapshot == null)
                return;

            AddRelationshipRows(buffers, "summary:buffers", snapshot.GraphicsBuffers
                .OrderByDescending(buffer => buffer.SizeInBytes)
                .Select(buffer => Canonical(FloraRenderingInspectorNode.ForGraphicsBuffer(buffer))));
        }

        private FloraRenderingInspectorNode Canonical(FloraRenderingInspectorNode node)
            => m_Model.FindCanonicalNode(node) ?? node;

        private VisualElement AddOverviewGrid()
        {
            var overview = new VisualElement();
            overview.AddToClassList("flora-rendering-inspector__overview-grid");
            m_Content.Add(overview);
            return overview;
        }

        private FloraRenderingInspectorSection AddSection(string title, int? count = null, bool showCount = true, string summary = null, bool expanded = false, bool defaultExpanded = true, string iconClass = null)
        {
            var key = GetSectionKey(title);
            var shouldExpand = expanded || m_ExpandedSections.Contains(key) || defaultExpanded && !m_CollapsedSections.Contains(key);
            var section = new FloraRenderingInspectorSection(title, count, shouldExpand, showCount, summary, iconClass ?? GetSectionIconClass(title))
            {
                name = key,
            };
            m_Content.Add(section);
            return section;
        }

        private static string GetSectionIconClass(string title)
        {
            return title switch
            {
                "Status" => "overview",
                "Summary" => "overview",
                "Memory" => "buffer",
                "Draws" => "draw",
                "Graphics Domain" => "domain",
                "Graphics Domains" => "domain",
                "Shader Properties" => "property",
                "Relationships" => "group",
                "Children" => "group",
                "Object References" => "source",
                "Sources" => "source",
                "Archetypes" => "archetype",
                "Instance Chunks" => "chunk",
                "Grid" => "grid",
                "Culling" => "culling",
                "CPU Culling Views" => "culling-cpu",
                "GPU Culling Views" => "culling-gpu",
                "Culling Chunks" => "grid-chunk",
                "Grid Chunks" => "grid-chunk",
                "Graphics Buffers" => "buffer",
                "LODs" => "lod",
                _ => null,
            };
        }

        private void AddRelationshipRows(VisualElement parent, string key, IEnumerable<FloraRenderingInspectorNode> nodes, int maxVisible = 12)
        {
            var list = new FloraRenderingInspectorRelationshipList(
                key,
                nodes,
                m_OnSelectNode,
                m_ShowAllRelationshipLists.Contains(key),
                maxVisible,
                showAllKey =>
                {
                    m_ShowAllRelationshipLists.Add(showAllKey);
                    Rebuild();
                });
            parent.Add(list);
        }

        private void FlattenSingleSectionIfNeeded()
        {
            var children = m_Content.Children().ToList();
            var sections = children.OfType<FloraRenderingInspectorSection>().ToList();
            if (children.Count != 1 || sections.Count != 1)
                return;

            var section = sections[0];
            var sectionChildren = section.contentContainer.Children().ToList();
            section.RemoveFromHierarchy();
            foreach (var child in sectionChildren)
                m_Content.Add(child);
        }

        private string GetSectionKey(string title)
        {
            m_SectionNameCounts.TryGetValue(title, out var count);
            m_SectionNameCounts[title] = count + 1;
            return count == 0 ? title : $"{title}:{count}";
        }

        private void BuildLodTabs(FloraDiagnosticsTemplate template)
        {
            var section = AddSection("LODs", template.Lods.Count, summary: $"{template.DrawCount:n0} draws");
            if (template.Lods.Count == 0)
            {
                FloraRenderingInspectorElements.AddValueRow(section.contentContainer, "LODs", "None");
                return;
            }

            var key = GetLodTabKey(template);
            m_ActiveLodTabs.TryGetValue(key, out var activeIndex);
            activeIndex = Mathf.Clamp(activeIndex, 0, template.Lods.Count - 1);
            m_ActiveLodTabs[key] = activeIndex;

            var tabView = new FloraRenderingInspectorTabView();
            m_LodTabView = tabView;
            section.contentContainer.Add(tabView);

            for (var i = 0; i < template.Lods.Count; i++)
            {
                var lod = template.Lods[i];
                var tabContent = new VisualElement();
                tabContent.AddToClassList("flora-rendering-inspector__lod-tab-page");
                AddLodRows(tabContent, lod);

                tabView.AddTab(
                    $"LOD {lod.Index}",
                    $"{lod.CameraDrawCount + lod.ShadowDrawCount:n0} draws | {lod.TriangleCount:n0} triangles",
                    tabContent);
            }

            tabView.SetValueWithoutNotify(activeIndex);
            tabView.RegisterValueChangedCallback(evt =>
            {
                m_ActiveLodTabs[key] = evt.newValue;
            });
        }

        private static string GetLodTabKey(FloraDiagnosticsTemplate template) => $"template:{template.Index}:lods";

        private static void AddLodRows(VisualElement parent, FloraDiagnosticsLod lod)
        {
            FloraRenderingInspectorElements.AddValueRow(parent, "Height", lod.Height.ToString("0.###"));
            FloraRenderingInspectorElements.AddValueRow(parent, "Transition", lod.TransitionHeight.ToString("0.###"));
            FloraRenderingInspectorElements.AddValueRow(parent, "Camera Draws", lod.CameraDrawCount);
            FloraRenderingInspectorElements.AddValueRow(parent, "Shadow Draws", lod.ShadowDrawCount);
            FloraRenderingInspectorElements.AddValueRow(parent, "Triangles", lod.TriangleCount);
            FloraRenderingInspectorElements.AddValueRow(parent, "Vertices", lod.VertexCount > 0 ? lod.VertexCount.ToString("n0") : "Unknown");
        }

        private static string FormatVector(Vector3 value)
            => $"{value.x:0.###}, {value.y:0.###}, {value.z:0.###}";

        private static string FormatCoordinates(Vector3Int value)
            => $"{value.x}, {value.y}, {value.z}";

    }
}
