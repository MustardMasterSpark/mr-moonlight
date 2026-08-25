// Copyright © Magnetic Arcade. All Rights Reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.UIElements;

namespace MA.Flora.Editor
{
    internal enum FloraRenderingInspectorTab
    {
        All,
        Authoring,
        Structure,
        Grid,
        Rendering,
    }

    internal enum FloraRenderingInspectorSortMode
    {
        Default,
        Count,
    }

    internal sealed class FloraRenderingInspectorTabModel
    {
        public FloraRenderingInspectorTab Id;
        public string Label;
        public List<FloraRenderingInspectorNode> RootNodes { get; } = new();
        public List<TreeViewItemData<FloraRenderingInspectorNode>> RootItems { get; } = new();
        public int MatchCount;
        public string StructureSignature = string.Empty;
    }

    internal sealed class FloraRenderingInspectorModel
    {
        private readonly Dictionary<string, FloraRenderingInspectorNode> m_NodesByKey = new();
        private readonly Dictionary<string, FloraRenderingInspectorTab> m_TabByNodeKey = new();
        private readonly Dictionary<FloraRenderingInspectorTab, FloraRenderingInspectorTabModel> m_TabsById = new();
        private readonly Dictionary<int, List<FloraDiagnosticsTemplate>> m_TemplatesByDomain = new();
        private readonly Dictionary<int, List<FloraDiagnosticsTemplate>> m_TemplatesByDraw = new();
        private readonly Dictionary<int, List<FloraDiagnosticsDraw>> m_DrawsByDomain = new();
        private readonly Dictionary<int, List<FloraDiagnosticsArchetype>> m_ArchetypesByTemplate = new();
        private readonly Dictionary<int, List<FloraDiagnosticsInstanceChunk>> m_InstanceChunksByTemplate = new();
        private readonly Dictionary<int, List<FloraDiagnosticsInstanceChunk>> m_InstanceChunksByArchetype = new();
        private readonly Dictionary<int, List<FloraDiagnosticsCullingChunk>> m_CullingChunksByTemplate = new();
        private readonly Dictionary<int, List<FloraDiagnosticsCullingChunk>> m_CullingChunksByArchetype = new();
        private readonly Dictionary<int, List<FloraRenderingInspectorShaderPropertyUsage>> m_ShaderPropertiesByDomain = new();
        private FloraRenderingInspectorTab m_BuildingTab;
        private int m_NextTreeItemId = 1;

        public FloraDiagnosticsSnapshot Snapshot { get; private set; }
        public List<FloraRenderingInspectorNode> RootNodes { get; } = new();
        public List<TreeViewItemData<FloraRenderingInspectorNode>> RootItems { get; } = new();
        public List<FloraRenderingInspectorTabModel> Tabs { get; } = new();
        public Dictionary<int, FloraDiagnosticsSource> SourcesByIndex { get; } = new();
        public Dictionary<int, FloraDiagnosticsTemplate> TemplatesByIndex { get; } = new();
        public Dictionary<int, FloraDiagnosticsDraw> DrawsByIndex { get; } = new();
        public Dictionary<int, FloraDiagnosticsBatchDomain> DomainsByIndex { get; } = new();
        public Dictionary<int, FloraDiagnosticsArchetype> ArchetypesByIndex { get; } = new();
        public Dictionary<int, FloraDiagnosticsInstanceChunk> InstanceChunksByIndex { get; } = new();
        public Dictionary<int, FloraDiagnosticsCullingChunk> CullingChunksByIndex { get; } = new();
        public Dictionary<int, FloraRenderingInspectorShaderPropertyUsage> ShaderPropertyUsagesByNameID { get; } = new();
        public string SearchText { get; private set; } = string.Empty;
        public FloraRenderingInspectorSortMode SortMode { get; private set; }
        public string StructureSignature { get; private set; } = string.Empty;

        public static FloraRenderingInspectorModel Empty()
        {
            return Build(new FloraDiagnosticsSnapshot(), string.Empty, FloraRenderingInspectorSortMode.Default);
        }

        public static FloraRenderingInspectorModel Build(FloraDiagnosticsSnapshot snapshot, string searchText)
            => Build(snapshot, searchText, FloraRenderingInspectorSortMode.Default);

        public static FloraRenderingInspectorModel Build(FloraDiagnosticsSnapshot snapshot, string searchText, FloraRenderingInspectorSortMode sortMode)
        {
            var model = new FloraRenderingInspectorModel
            {
                Snapshot = snapshot,
                SearchText = searchText?.Trim() ?? string.Empty,
                SortMode = sortMode,
            };
            model.BuildIndexes();
            model.BuildTabs();
            model.ApplySearch(model.SearchText);
            return model;
        }

        public FloraRenderingInspectorTabModel GetTab(FloraRenderingInspectorTab tab)
            => m_TabsById.TryGetValue(tab, out var tabModel) ? tabModel : Tabs[0];

        public FloraRenderingInspectorTab GetOwningTab(string key)
            => !string.IsNullOrEmpty(key) && m_TabByNodeKey.TryGetValue(key, out var tab) ? tab : FloraRenderingInspectorTab.All;

        public FloraRenderingInspectorNode FindNode(FloraRenderingInspectorTab tab, string key)
        {
            if (string.IsNullOrEmpty(key))
                return null;

            var tabModel = GetTab(tab);
            foreach (var root in tabModel.RootNodes)
            {
                var node = FindNode(root, key);
                if (node != null)
                    return node;
            }

            return null;
        }

        public FloraRenderingInspectorNode FindNode(string key)
            => !string.IsNullOrEmpty(key) && m_NodesByKey.TryGetValue(key, out var node) ? node : null;

        public FloraRenderingInspectorNode FindCanonicalNode(FloraRenderingInspectorNode node)
        {
            if (node == null)
                return null;

            if (node.Kind == FloraRenderingInspectorNodeKind.Template &&
                node.Template != null &&
                node.Key != $"template:{node.Template.Index}")
                return FindTemplateNode(node.Template);

            var exact = FindNode(node.Key);
            if (exact != null)
                return exact;

            return node.Kind switch
            {
                FloraRenderingInspectorNodeKind.Source => FindSourceNode(node.Source),
                FloraRenderingInspectorNodeKind.Template => FindTemplateNode(node.Template),
                FloraRenderingInspectorNodeKind.Draw => FindDrawNode(node.Draw),
                FloraRenderingInspectorNodeKind.BatchDomain => FindBatchDomainNode(node.BatchDomain),
                FloraRenderingInspectorNodeKind.Archetype => FindArchetypeNode(node.Archetype),
                FloraRenderingInspectorNodeKind.InstanceChunk => FindInstanceChunkNode(node.InstanceChunk),
                FloraRenderingInspectorNodeKind.CullingChunk => FindCullingChunkNode(node.CullingChunk),
                FloraRenderingInspectorNodeKind.GraphicsBuffer => FindGraphicsBufferNode(node.GraphicsBuffer),
                FloraRenderingInspectorNodeKind.ShaderProperty => FindShaderPropertyNode(node.ShaderPropertyUsage),
                _ => null,
            };
        }

        public FloraRenderingInspectorNode FindFirstSelectable()
            => m_NodesByKey.Values.FirstOrDefault(node => node.IsSelectable);

        public int GetTreeItemId(string key)
        {
            var node = FindNode(key);
            return node?.Id ?? -1;
        }

        public int GetTreeItemId(FloraRenderingInspectorTab tab, string key)
        {
            var node = FindNode(tab, key);
            return node?.Id ?? -1;
        }

        public IReadOnlyList<FloraDiagnosticsTemplate> GetTemplatesForDomain(int domainIndex)
            => GetList(m_TemplatesByDomain, domainIndex);

        public IReadOnlyList<FloraDiagnosticsTemplate> GetTemplatesForDraw(int drawIndex)
            => GetList(m_TemplatesByDraw, drawIndex);

        public IReadOnlyList<FloraDiagnosticsDraw> GetDrawsForDomain(int domainIndex)
            => GetList(m_DrawsByDomain, domainIndex);

        public IReadOnlyList<FloraDiagnosticsArchetype> GetArchetypesForTemplate(int templateIndex)
            => GetList(m_ArchetypesByTemplate, templateIndex);

        public IReadOnlyList<FloraDiagnosticsInstanceChunk> GetInstanceChunksForTemplate(int templateIndex)
            => GetList(m_InstanceChunksByTemplate, templateIndex);

        public IReadOnlyList<FloraDiagnosticsInstanceChunk> GetInstanceChunksForArchetype(int archetypeIndex)
            => GetList(m_InstanceChunksByArchetype, archetypeIndex);

        public IReadOnlyList<FloraDiagnosticsCullingChunk> GetCullingChunksForTemplate(int templateIndex)
            => GetList(m_CullingChunksByTemplate, templateIndex);

        public IReadOnlyList<FloraDiagnosticsCullingChunk> GetCullingChunksForArchetype(int archetypeIndex)
            => GetList(m_CullingChunksByArchetype, archetypeIndex);

        public IReadOnlyList<FloraRenderingInspectorShaderPropertyUsage> GetShaderPropertiesForDomain(int domainIndex)
            => GetList(m_ShaderPropertiesByDomain, domainIndex);

        public void ApplySearch(string searchText)
        {
            SearchText = searchText?.Trim() ?? string.Empty;
            RootItems.Clear();
            m_NextTreeItemId = 1;
            ResetTreeItemIds(RootNodes);

            foreach (var tab in Tabs)
                BuildTreeItems(tab, SearchText);

            var defaultTab = GetTab(FloraRenderingInspectorTab.All);
            RootItems.AddRange(defaultTab.RootItems);
        }

        private void BuildIndexes()
        {
            if (Snapshot == null)
                return;

            foreach (var source in Snapshot.Sources)
                SourcesByIndex[source.Index] = source;
            foreach (var template in Snapshot.Templates)
                TemplatesByIndex[template.Index] = template;
            foreach (var draw in Snapshot.Draws)
                DrawsByIndex[draw.Index] = draw;
            foreach (var domain in Snapshot.BatchDomains)
                DomainsByIndex[domain.Index] = domain;
            foreach (var archetype in Snapshot.Archetypes)
                ArchetypesByIndex[archetype.Index] = archetype;
            foreach (var chunk in Snapshot.InstanceChunks)
                InstanceChunksByIndex[chunk.Index] = chunk;
            foreach (var chunk in Snapshot.CullingChunks)
                CullingChunksByIndex[chunk.Index] = chunk;

            BuildGroupedIndexes();
            BuildShaderPropertyUsages();
        }

        private void BuildGroupedIndexes()
        {
            foreach (var template in Snapshot.Templates)
            {
                AddToLookup(m_TemplatesByDomain, template.BatchDomainIndex, template);
                foreach (var drawIndex in template.DrawIndices)
                    AddToLookup(m_TemplatesByDraw, drawIndex, template);
            }

            foreach (var draw in Snapshot.Draws)
                AddToLookup(m_DrawsByDomain, draw.BatchDomainIndex, draw);

            foreach (var archetype in Snapshot.Archetypes)
                AddToLookup(m_ArchetypesByTemplate, archetype.TemplateIndex, archetype);

            foreach (var chunk in Snapshot.InstanceChunks)
            {
                AddToLookup(m_InstanceChunksByTemplate, chunk.TemplateIndex, chunk);
                AddToLookup(m_InstanceChunksByArchetype, chunk.ArchetypeIndex, chunk);
            }

            foreach (var chunk in Snapshot.CullingChunks)
            {
                AddToLookup(m_CullingChunksByTemplate, chunk.TemplateIndex, chunk);
                AddToLookup(m_CullingChunksByArchetype, chunk.ArchetypeIndex, chunk);
            }

            SortLookup(m_TemplatesByDomain, (left, right) => string.Compare(left.Name, right.Name, StringComparison.OrdinalIgnoreCase));
            SortLookup(m_TemplatesByDraw, (left, right) => string.Compare(left.Name, right.Name, StringComparison.OrdinalIgnoreCase));
            SortLookup(m_DrawsByDomain, (left, right) => string.Compare(left.Name, right.Name, StringComparison.OrdinalIgnoreCase));
            SortLookup(m_ArchetypesByTemplate, (left, right) => left.Index.CompareTo(right.Index));
            SortLookup(m_InstanceChunksByTemplate, (left, right) => left.Index.CompareTo(right.Index));
            SortLookup(m_InstanceChunksByArchetype, (left, right) => left.Index.CompareTo(right.Index));
            SortLookup(m_CullingChunksByTemplate, CompareCullingChunks);
            SortLookup(m_CullingChunksByArchetype, CompareCullingChunks);
        }

        private FloraRenderingInspectorNode FindSourceNode(FloraDiagnosticsSource source)
            => source == null ? null : FindNode($"source:{source.Index}") ?? FindFirstNode(node => node.Source?.Index == source.Index);

        private FloraRenderingInspectorNode FindTemplateNode(FloraDiagnosticsTemplate template)
            => template == null ? null : FindNode($"template:{template.Index}") ?? FindFirstNode(node => node.Template?.Index == template.Index);

        private FloraRenderingInspectorNode FindDrawNode(FloraDiagnosticsDraw draw)
            => draw == null ? null : FindNode($"draw:{draw.Index}") ?? FindFirstNode(node => node.Draw?.Index == draw.Index);

        private FloraRenderingInspectorNode FindBatchDomainNode(FloraDiagnosticsBatchDomain domain)
            => domain == null ? null : FindNode($"domain:{domain.Index}") ?? FindFirstNode(node => node.BatchDomain?.Index == domain.Index);

        private FloraRenderingInspectorNode FindArchetypeNode(FloraDiagnosticsArchetype archetype)
            => archetype == null ? null : FindFirstNode(node => node.Archetype?.Index == archetype.Index);

        private FloraRenderingInspectorNode FindInstanceChunkNode(FloraDiagnosticsInstanceChunk chunk)
            => chunk == null ? null : FindNode($"chunk:{chunk.Index}") ?? FindFirstNode(node => node.InstanceChunk?.Index == chunk.Index);

        private FloraRenderingInspectorNode FindCullingChunkNode(FloraDiagnosticsCullingChunk chunk)
            => chunk == null ? null : FindNode($"grid:chunk:{chunk.Index}") ?? FindFirstNode(node => node.CullingChunk?.Index == chunk.Index);

        private FloraRenderingInspectorNode FindGraphicsBufferNode(FloraDiagnosticsGraphicsBuffer buffer)
            => buffer == null ? null : FindNode($"buffer:{buffer.Index}") ?? FindFirstNode(node => node.GraphicsBuffer?.Index == buffer.Index);

        private FloraRenderingInspectorNode FindShaderPropertyNode(FloraRenderingInspectorShaderPropertyUsage usage)
            => usage == null ? null : FindNode($"property:{usage.NameID}") ?? FindFirstNode(node => node.ShaderPropertyUsage?.NameID == usage.NameID);

        private FloraRenderingInspectorNode FindFirstNode(Func<FloraRenderingInspectorNode, bool> predicate)
        {
            foreach (var root in RootNodes)
            {
                var node = FindFirstNode(root, predicate);
                if (node != null)
                    return node;
            }

            return null;
        }

        private static FloraRenderingInspectorNode FindFirstNode(FloraRenderingInspectorNode node, Func<FloraRenderingInspectorNode, bool> predicate)
        {
            if (node != null && predicate(node))
                return node;

            foreach (var child in node?.Children ?? Enumerable.Empty<FloraRenderingInspectorNode>())
            {
                var match = FindFirstNode(child, predicate);
                if (match != null)
                    return match;
            }

            return null;
        }

        private void BuildTabs()
        {
            RootNodes.Clear();
            RootItems.Clear();
            Tabs.Clear();

            m_BuildingTab = FloraRenderingInspectorTab.All;
            AddTab(FloraRenderingInspectorTab.All, "All", BuildAllRoots());
            m_BuildingTab = FloraRenderingInspectorTab.Authoring;
            AddTab(FloraRenderingInspectorTab.Authoring, "Authoring", BuildAuthoringRoots());
            m_BuildingTab = FloraRenderingInspectorTab.Structure;
            AddTab(FloraRenderingInspectorTab.Structure, "Structure", BuildStructureRoots());
            m_BuildingTab = FloraRenderingInspectorTab.Grid;
            AddTab(FloraRenderingInspectorTab.Grid, "Grid", BuildGridRoots());
            m_BuildingTab = FloraRenderingInspectorTab.Rendering;
            AddTab(FloraRenderingInspectorTab.Rendering, "Rendering", BuildRenderingRoots());

            StructureSignature = ComputeStructureSignature(RootNodes);
            foreach (var tab in Tabs)
                tab.StructureSignature = ComputeStructureSignature(tab.RootNodes);
        }

        private void AddTab(FloraRenderingInspectorTab id, string label, IEnumerable<FloraRenderingInspectorNode> roots)
        {
            var tab = new FloraRenderingInspectorTabModel { Id = id, Label = label };
            foreach (var root in roots)
            {
                AddNode(root);
                tab.RootNodes.Add(root);
                RootNodes.Add(root);
            }

            Tabs.Add(tab);
            m_TabsById[id] = tab;
        }

        private IEnumerable<FloraRenderingInspectorNode> BuildAuthoringRoots()
        {
            foreach (var template in SortTemplates(Snapshot?.Templates ?? Enumerable.Empty<FloraDiagnosticsTemplate>()))
                yield return BuildTemplateBranch(template, $"template:{template.Index}", includeSources: true, includeArchetypes: false);
        }

        private IEnumerable<FloraRenderingInspectorNode> BuildAllRoots()
        {
            yield return BuildArchetypesTypeRoot();
            yield return BuildDrawsTypeRoot();
            yield return BuildGraphicsBuffersTypeRoot();
            yield return BuildDomainsTypeRoot();
            yield return BuildShaderPropertiesTypeRoot();
            yield return BuildSourcesTypeRoot();
            yield return BuildTemplatesTypeRoot();
        }

        private FloraRenderingInspectorNode BuildSourcesTypeRoot()
        {
            var root = FloraRenderingInspectorNode.Root("root:all:sources", "Sources", Count(Snapshot?.Sources.Count), "source", isSectionHeader: true);
            foreach (var source in SortSources(Snapshot?.Sources ?? Enumerable.Empty<FloraDiagnosticsSource>()))
            {
                var sourceNode = FloraRenderingInspectorNode.ForSource(source);
                AddNode(sourceNode);
                root.AddChild(sourceNode);
            }

            return root;
        }

        private FloraRenderingInspectorNode BuildTemplatesTypeRoot()
        {
            var root = FloraRenderingInspectorNode.Root("root:all:templates", "Templates", Count(Snapshot?.Templates.Count), "template", isSectionHeader: true);
            foreach (var template in SortTemplates(Snapshot?.Templates ?? Enumerable.Empty<FloraDiagnosticsTemplate>()))
            {
                var templateNode = FloraRenderingInspectorNode.ForTemplate(template);
                AddNode(templateNode);
                AddTemplateSubviewGroups(templateNode, template);
                root.AddChild(templateNode);
            }

            return root;
        }

        private FloraRenderingInspectorNode BuildArchetypesTypeRoot()
        {
            var root = FloraRenderingInspectorNode.Root("root:all:archetypes", "Archetypes", Count(Snapshot?.Archetypes.Count), "archetype", isSectionHeader: true);
            foreach (var archetype in SortArchetypes(Snapshot?.Archetypes ?? Enumerable.Empty<FloraDiagnosticsArchetype>()))
            {
                var archetypeNode = FloraRenderingInspectorNode.ForArchetype(archetype);
                AddNode(archetypeNode);
                root.AddChild(archetypeNode);
            }

            return root;
        }

        private FloraRenderingInspectorNode BuildDrawsTypeRoot()
        {
            var root = FloraRenderingInspectorNode.Root("root:all:draws", "Draws", Count(Snapshot?.Draws.Count), "draw", isSectionHeader: true);
            foreach (var draw in SortDraws(Snapshot?.Draws ?? Enumerable.Empty<FloraDiagnosticsDraw>()))
            {
                var drawNode = FloraRenderingInspectorNode.ForDraw(draw);
                AddNode(drawNode);
                root.AddChild(drawNode);
            }

            return root;
        }

        private FloraRenderingInspectorNode BuildDomainsTypeRoot()
        {
            var root = FloraRenderingInspectorNode.Root("root:all:domains", "Graphics Domains", Count(Snapshot?.BatchDomains.Count), "domain", isSectionHeader: true);
            foreach (var domain in SortDomains(Snapshot?.BatchDomains ?? Enumerable.Empty<FloraDiagnosticsBatchDomain>()))
            {
                var domainNode = FloraRenderingInspectorNode.ForBatchDomain(domain);
                AddNode(domainNode);
                root.AddChild(domainNode);
            }

            return root;
        }

        private FloraRenderingInspectorNode BuildGraphicsBuffersTypeRoot()
        {
            var root = FloraRenderingInspectorNode.Root("root:all:buffers", "Graphics Buffers", Count(Snapshot?.GraphicsBuffers.Count), "buffer", isSectionHeader: true);
            foreach (var buffer in SortGraphicsBuffers(Snapshot?.GraphicsBuffers ?? Enumerable.Empty<FloraDiagnosticsGraphicsBuffer>()))
            {
                var bufferNode = FloraRenderingInspectorNode.ForGraphicsBuffer(buffer);
                AddNode(bufferNode);
                root.AddChild(bufferNode);
            }

            return root;
        }

        private FloraRenderingInspectorNode BuildShaderPropertiesTypeRoot()
            => BuildShaderPropertiesRoot("root:all:shader-properties");

        private void AddTemplateSubviewGroups(FloraRenderingInspectorNode templateNode, FloraDiagnosticsTemplate template)
        {
            AddDrawsGroup(templateNode, template);
            AddDomainGroup(templateNode, template);
            AddGridGroup(templateNode, template);
            AddLodsGroup(templateNode, template);
            AddSourcesGroup(templateNode, template);
        }

        private void AddSourcesGroup(FloraRenderingInspectorNode templateNode, FloraDiagnosticsTemplate template)
        {
            var root = FloraRenderingInspectorNode.Root($"{templateNode.Key}/sources", "Sources", Count(template.SourceIndices.Count), "source", countInSearch: true);
            foreach (var sourceIndex in template.SourceIndices)
            {
                if (!SourcesByIndex.TryGetValue(sourceIndex, out var source))
                    continue;

                var sourceNode = FloraRenderingInspectorNode.ForSource(source, $"{root.Key}/source:{source.Index}");
                AddNode(sourceNode);
                root.AddChild(sourceNode);
            }

            AddNode(root);
            templateNode.AddChild(root);
        }

        private void AddLodsGroup(FloraRenderingInspectorNode templateNode, FloraDiagnosticsTemplate template)
        {
            var root = FloraRenderingInspectorNode.Root($"{templateNode.Key}/lods", "LODs", Count(template.Lods.Count), "lod", countInSearch: true);
            foreach (var lod in template.Lods)
            {
                var lodNode = FloraRenderingInspectorNode.ForLod(lod, $"{root.Key}/lod:{lod.Index}");
                AddNode(lodNode);
                root.AddChild(lodNode);
            }

            AddNode(root);
            templateNode.AddChild(root);
        }

        private void AddGridGroup(FloraRenderingInspectorNode templateNode, FloraDiagnosticsTemplate template)
        {
            var chunks = GetCullingChunksForTemplate(template.Index);
            var root = FloraRenderingInspectorNode.Root($"{templateNode.Key}/grid", "Grid", Count(Snapshot?.HasCullingGridDetails == true ? chunks.Count : template.CullingChunkCount), "grid", countInSearch: true);
            AddGridTemplateChildren(root, template.Index, chunks);
            AddNode(root);
            templateNode.AddChild(root);
        }

        private void AddDrawsGroup(FloraRenderingInspectorNode templateNode, FloraDiagnosticsTemplate template)
        {
            var root = FloraRenderingInspectorNode.Root($"{templateNode.Key}/draws", "Draws", Count(template.DrawIndices.Count), "draw", countInSearch: true);
            foreach (var drawIndex in template.DrawIndices)
            {
                if (!DrawsByIndex.TryGetValue(drawIndex, out var draw))
                    continue;

                var drawNode = FloraRenderingInspectorNode.ForDraw(draw, $"{root.Key}/draw:{draw.Index}");
                AddNode(drawNode);
                root.AddChild(drawNode);
            }

            AddNode(root);
            templateNode.AddChild(root);
        }

        private void AddDomainGroup(FloraRenderingInspectorNode templateNode, FloraDiagnosticsTemplate template)
        {
            var root = FloraRenderingInspectorNode.Root($"{templateNode.Key}/domain", "Graphics Domain", DomainsByIndex.ContainsKey(template.BatchDomainIndex) ? "1" : "0", "domain", countInSearch: true);
            if (DomainsByIndex.TryGetValue(template.BatchDomainIndex, out var domain))
            {
                var domainNode = FloraRenderingInspectorNode.ForBatchDomain(domain, $"{root.Key}/domain:{domain.Index}");
                AddNode(domainNode);
                root.AddChild(domainNode);
            }

            AddNode(root);
            templateNode.AddChild(root);
        }

        private IEnumerable<FloraRenderingInspectorNode> BuildStructureRoots()
        {
            foreach (var archetype in SortArchetypes(Snapshot?.Archetypes ?? Enumerable.Empty<FloraDiagnosticsArchetype>()))
            {
                var archetypeNode = FloraRenderingInspectorNode.ForArchetype(archetype);
                AddNode(archetypeNode);

                foreach (var chunk in GetInstanceChunksForArchetype(archetype.Index))
                {
                    var chunkNode = FloraRenderingInspectorNode.ForInstanceChunk(chunk);
                    AddNode(chunkNode);
                    archetypeNode.AddChild(chunkNode);
                }

                yield return archetypeNode;
            }
        }

        private IEnumerable<FloraRenderingInspectorNode> BuildGridRoots()
        {
            foreach (var templateGroup in Snapshot?.CullingChunks.GroupBy(chunk => chunk.TemplateIndex).OrderBy(group => GetTemplateName(group.Key), StringComparer.OrdinalIgnoreCase) ?? Enumerable.Empty<IGrouping<int, FloraDiagnosticsCullingChunk>>())
            {
                var chunks = templateGroup.ToList();
                var templateNode = BuildGridTemplateNode(templateGroup.Key, templateGroup.Count());
                AddNode(templateNode);

                var groupByBlock = chunks
                    .Where(HasValidGridCell)
                    .Select(chunk => chunk.BlockIndex)
                    .Distinct()
                    .Skip(1)
                    .Any();

                if (groupByBlock)
                {
                    foreach (var blockGroup in chunks.Where(HasValidGridCell).GroupBy(chunk => chunk.BlockIndex).OrderBy(group => group.Key))
                    {
                        var blockNode = BuildGridBlockNode(templateGroup.Key, blockGroup.Key, blockGroup);
                        AddNode(blockNode);
                        templateNode.AddChild(blockNode);
                        AddGridCellNodes(blockNode, templateGroup.Key, blockGroup, blockNode.Key);
                    }

                    var unassignedChunks = chunks.Where(chunk => !HasValidGridCell(chunk)).ToArray();
                    if (unassignedChunks.Any())
                    {
                        var unassignedNode = BuildGridCellNode(templateGroup.Key, unassignedChunks[0], unassignedChunks.Length, $"grid:template:{templateGroup.Key}", unassignedChunks);
                        AddNode(unassignedNode);
                        templateNode.AddChild(unassignedNode);
                        AddGridChunkNodes(unassignedNode, unassignedChunks);
                    }
                }
                else
                    AddGridCellNodes(templateNode, templateGroup.Key, chunks, $"grid:template:{templateGroup.Key}");

                yield return templateNode;
            }
        }

        private FloraRenderingInspectorNode BuildGridTemplateNode(int templateIndex, int chunkCount)
        {
            if (TemplatesByIndex.TryGetValue(templateIndex, out var template))
            {
                var node = FloraRenderingInspectorNode.ForTemplate(template, $"grid:template:{template.Index}");
                node.CountText = Count(chunkCount);
                node.Tooltip = $"{template.InstanceCount:n0} instances | {chunkCount:n0} grid chunks | Domain {template.BatchDomainIndex}";
                return node;
            }

            return FloraRenderingInspectorNode.Root($"grid:template:{templateIndex}", $"Template {templateIndex}", Count(chunkCount), "template", countInSearch: true);
        }

        private void AddGridCellNodes(FloraRenderingInspectorNode parent, int templateIndex, IEnumerable<FloraDiagnosticsCullingChunk> chunks, string parentKey)
        {
            foreach (var cellGroup
                     in chunks.GroupBy(GetGridCellGroupKey).OrderBy(group => GetGridCellSortKey(group.First())))
            {
                var cellChunks = cellGroup.OrderBy(chunk => chunk.Index).ToArray();
                var cellNode = BuildGridCellNode(templateIndex, cellChunks[0], cellChunks.Length, parentKey, cellChunks);
                AddNode(cellNode);
                parent.AddChild(cellNode);
                AddGridChunkNodes(cellNode, cellGroup);
            }
        }

        private void AddGridChunkNodes(FloraRenderingInspectorNode cellNode, IEnumerable<FloraDiagnosticsCullingChunk> chunks)
        {
            foreach (var chunk
                     in chunks.OrderBy(chunk => chunk.TemplateIndex).ThenBy(chunk => chunk.ArchetypeIndex).ThenBy(chunk => chunk.Index))
            {
                var chunkNode = FloraRenderingInspectorNode.ForCullingChunk(chunk, $"{cellNode.Key}/chunk:{chunk.Index}");
                AddNode(chunkNode);
                cellNode.AddChild(chunkNode);
            }
        }

        private void AddGridTemplateChildren(FloraRenderingInspectorNode templateNode, int templateIndex, IReadOnlyCollection<FloraDiagnosticsCullingChunk> chunks)
        {
            if (chunks == null || chunks.Count == 0)
                return;

            var groupByBlock = chunks
                .Where(HasValidGridCell)
                .Select(chunk => chunk.BlockIndex)
                .Distinct()
                .Skip(1)
                .Any();

            if (groupByBlock)
            {
                foreach (var blockGroup
                         in chunks.Where(HasValidGridCell).GroupBy(chunk => chunk.BlockIndex).OrderBy(group => group.Key))
                {
                    var blockNode = BuildGridBlockNode(templateIndex, blockGroup.Key, blockGroup, templateNode.Key);
                    AddNode(blockNode);
                    templateNode.AddChild(blockNode);
                    AddGridCellNodes(blockNode, templateIndex, blockGroup, blockNode.Key);
                }

                var unassignedChunks = chunks.Where(chunk => !HasValidGridCell(chunk)).ToArray();
                if (unassignedChunks.Any())
                {
                    var unassignedNode = BuildGridCellNode(templateIndex, unassignedChunks[0], unassignedChunks.Length, templateNode.Key, unassignedChunks);
                    AddNode(unassignedNode);
                    templateNode.AddChild(unassignedNode);
                    AddGridChunkNodes(unassignedNode, unassignedChunks);
                }
            }
            else
                AddGridCellNodes(templateNode, templateIndex, chunks, templateNode.Key);
        }

        private static FloraRenderingInspectorNode BuildGridBlockNode(int templateIndex, int blockIndex, IEnumerable<FloraDiagnosticsCullingChunk> blockGroup)
            => BuildGridBlockNode(templateIndex, blockIndex, blockGroup, $"grid:template:{templateIndex}");

        private static FloraRenderingInspectorNode BuildGridBlockNode(int templateIndex, int blockIndex, IEnumerable<FloraDiagnosticsCullingChunk> blockGroup, string parentKey)
        {
            var chunks = blockGroup.ToArray();
            var first = chunks.OrderBy(chunk => chunk.CellIndexInBlock).ThenBy(chunk => chunk.Index).First();

            var chunkCount = chunks.Length;
            var cellCount = chunks.Select(chunk => chunk.CellIndex).Distinct().Count();
            int minCell = chunks.Min(chunk => chunk.CellIndex);
            int maxCell = chunks.Max(chunk => chunk.CellIndex);

            var node = FloraRenderingInspectorNode.Root($"{parentKey}/block:{blockIndex}", $"Block {blockIndex}", Count(chunkCount), "grid-block", countInSearch: true);
            node.Subtitle = cellCount == 1 ? $"Cell {minCell}" : $"Cells {minCell}-{maxCell}";
            node.Tooltip = $"Block level {first.BlockLevel} | Block {blockIndex} | {FormatCoordinates(first.BlockCoordinates)} | {cellCount:n0} cells | {chunkCount:n0} grid chunks";
            node.BadgeText = FormatGridLevelBadge(first.BlockLevel);
            node.BadgeStyleClass = GetGridLevelBadgeStyleClass(first.BlockLevel);
            node.ShowBadgeOnRoot = true;

            if (TryGetFrameBounds(chunks, out var frameBounds))
                node.FrameBounds = frameBounds;
            return node;
        }

        private static FloraRenderingInspectorNode BuildGridCellNode(int templateIndex, FloraDiagnosticsCullingChunk first, int chunkCount, string parentKey, IEnumerable<FloraDiagnosticsCullingChunk> chunks)
        {
            var cellKey = GetGridCellGroupKey(first);
            var name = HasValidGridCell(first)
                ? $"Cell {FormatCoordinates(first.CellCoordinates)}"
                : "Unassigned";
            var subtitle = HasValidGridCell(first)
                ? $"Block {first.BlockIndex}, cell {first.CellIndexInBlock}"
                : "No valid grid cell";
            var tooltip = HasValidGridCell(first)
                ? $"Level {first.CellLevel} | Cell {first.CellIndex} | Block {first.BlockIndex} | {first.CellLocation}"
                : "Grid chunks without valid cell data.";

            var node = FloraRenderingInspectorNode.Root($"{parentKey}/cell:{cellKey}", name, Count(chunkCount), "grid-cell", countInSearch: true);
            node.Subtitle = subtitle;
            node.Tooltip = tooltip;
            node.BadgeText = FormatGridLevelBadge(first.CellLevel);
            node.BadgeStyleClass = GetGridLevelBadgeStyleClass(first.CellLevel);
            node.ShowBadgeOnRoot = true;

            if (TryGetFrameBounds(chunks, out var frameBounds))
                node.FrameBounds = frameBounds;

            return node;
        }

        private IEnumerable<FloraRenderingInspectorNode> BuildRenderingRoots()
        {
            return new[]
            {
                BuildDrawsRoot(),
                BuildGraphicsBuffersRoot(),
                BuildDomainsRoot(),
                BuildShaderPropertiesRoot("root:rendering:shader-properties"),
            };
        }

        private FloraRenderingInspectorNode BuildDrawsRoot()
        {
            var root = FloraRenderingInspectorNode.Root("root:rendering:draws", "Draws", Count(Snapshot?.Draws.Count), "draw", isSectionHeader: true);
            foreach (var draw in SortDraws(Snapshot?.Draws ?? Enumerable.Empty<FloraDiagnosticsDraw>()))
            {
                var drawNode = FloraRenderingInspectorNode.ForDraw(draw);
                AddNode(drawNode);
                root.AddChild(drawNode);
            }

            return root;
        }

        private FloraRenderingInspectorNode BuildDomainsRoot()
        {
            var root = FloraRenderingInspectorNode.Root("root:rendering:domains", "Graphics Domains", Count(Snapshot?.BatchDomains.Count), "domain", isSectionHeader: true);
            foreach (var domain in SortDomains(Snapshot?.BatchDomains ?? Enumerable.Empty<FloraDiagnosticsBatchDomain>()))
            {
                var domainNode = FloraRenderingInspectorNode.ForBatchDomain(domain);
                AddNode(domainNode);
                root.AddChild(domainNode);

                foreach (var template in GetTemplatesForDomain(domain.Index))
                {
                    var templateNode = FloraRenderingInspectorNode.ForTemplate(template, $"domain:{domain.Index}/template:{template.Index}");
                    AddNode(templateNode);
                    domainNode.AddChild(templateNode);
                }

                foreach (var draw in GetDrawsForDomain(domain.Index))
                {
                    var drawNode = FloraRenderingInspectorNode.ForDraw(draw, $"domain:{domain.Index}/draw:{draw.Index}");
                    AddNode(drawNode);
                    domainNode.AddChild(drawNode);
                }
            }

            return root;
        }

        private FloraRenderingInspectorNode BuildGraphicsBuffersRoot()
        {
            var root = FloraRenderingInspectorNode.Root("root:rendering:buffers", "Graphics Buffers", Count(Snapshot?.GraphicsBuffers.Count), "buffer", isSectionHeader: true);
            foreach (var buffer in SortGraphicsBuffers(Snapshot?.GraphicsBuffers ?? Enumerable.Empty<FloraDiagnosticsGraphicsBuffer>()))
            {
                var bufferNode = FloraRenderingInspectorNode.ForGraphicsBuffer(buffer);
                AddNode(bufferNode);
                root.AddChild(bufferNode);
            }

            return root;
        }

        private FloraRenderingInspectorNode BuildShaderPropertiesRoot(string key)
        {
            var propertyCount = ShaderPropertyUsagesByNameID.Count;
            var root = FloraRenderingInspectorNode.Root(key, "Shader Properties", Count(propertyCount), "property", isSectionHeader: true);
            foreach (var usage in SortShaderPropertyUsages(ShaderPropertyUsagesByNameID.Values))
            {
                var propertyNode = FloraRenderingInspectorNode.ForShaderProperty(usage);
                AddNode(propertyNode);
                root.AddChild(propertyNode);
            }

            return root;
        }

        private void BuildShaderPropertyUsages()
        {
            if (Snapshot == null)
                return;

            foreach (var domain in Snapshot.BatchDomains)
            {
                foreach (var property in domain.Properties)
                {
                    if (!ShaderPropertyUsagesByNameID.TryGetValue(property.NameID, out var usage))
                    {
                        usage = new FloraRenderingInspectorShaderPropertyUsage
                        {
                            NameID = property.NameID,
                            DisplayName = GetShaderPropertyDisplayName(property),
                            RepresentativeProperty = property,
                        };
                        ShaderPropertyUsagesByNameID[property.NameID] = usage;
                    }

                    if (string.IsNullOrEmpty(usage.DisplayName) && !string.IsNullOrEmpty(property.DisplayName))
                        usage.DisplayName = property.DisplayName;
                    if (usage.RepresentativeProperty == null || string.IsNullOrEmpty(usage.RepresentativeProperty.DisplayName) && !string.IsNullOrEmpty(property.DisplayName))
                        usage.RepresentativeProperty = property;
                    if (!string.IsNullOrEmpty(property.DisplayName) && !usage.DisplayNameAliases.Contains(property.DisplayName))
                        usage.DisplayNameAliases.Add(property.DisplayName);
                    if (usage.Domains.All(existing => existing.Index != domain.Index))
                        usage.Domains.Add(domain);
                }
            }

            foreach (var usage in ShaderPropertyUsagesByNameID.Values)
            {
                usage.Domains.Sort((left, right) => string.Compare(left.BatchId, right.BatchId, StringComparison.OrdinalIgnoreCase));
                var templateIndices = new HashSet<int>();
                var drawIndices = new HashSet<int>();
                foreach (var domain in usage.Domains)
                {
                    foreach (var template in GetTemplatesForDomain(domain.Index))
                    {
                        if (templateIndices.Add(template.Index))
                            usage.Templates.Add(template);
                    }

                    foreach (var draw in GetDrawsForDomain(domain.Index))
                    {
                        if (drawIndices.Add(draw.Index))
                            usage.Draws.Add(draw);
                    }
                }

                usage.Templates.Sort((left, right) => string.Compare(left.Name, right.Name, StringComparison.OrdinalIgnoreCase));
                usage.Draws.Sort((left, right) => string.Compare(left.Name, right.Name, StringComparison.OrdinalIgnoreCase));
            }

            foreach (var usage in SortShaderPropertyUsages(ShaderPropertyUsagesByNameID.Values))
            {
                foreach (var domain in usage.Domains)
                    AddToLookup(m_ShaderPropertiesByDomain, domain.Index, usage);
            }
        }

        private FloraRenderingInspectorNode BuildTemplateBranch(FloraDiagnosticsTemplate template, string key, bool includeSources, bool includeArchetypes)
        {
            var templateNode = FloraRenderingInspectorNode.ForTemplate(template, key);
            AddNode(templateNode);

            if (includeSources)
            {
                foreach (var sourceIndex in template.SourceIndices)
                {
                    if (!SourcesByIndex.TryGetValue(sourceIndex, out var source))
                        continue;

                    var sourceNode = FloraRenderingInspectorNode.ForSource(source, $"{key}/source:{source.Index}");
                    AddNode(sourceNode);
                    templateNode.AddChild(sourceNode);
                }
            }

            foreach (var lod in template.Lods)
            {
                var lodNode = FloraRenderingInspectorNode.ForLod(lod, $"{key}/lod:{lod.Index}");
                AddNode(lodNode);
                templateNode.AddChild(lodNode);
            }

            foreach (var drawIndex in template.DrawIndices)
            {
                if (DrawsByIndex.TryGetValue(drawIndex, out var draw))
                {
                    var drawNode = FloraRenderingInspectorNode.ForDraw(draw, $"{key}/draw:{draw.Index}");
                    AddNode(drawNode);
                    templateNode.AddChild(drawNode);
                }
            }

            if (DomainsByIndex.TryGetValue(template.BatchDomainIndex, out var domain))
            {
                var domainNode = FloraRenderingInspectorNode.ForBatchDomain(domain, $"{key}/domain:{domain.Index}");
                AddNode(domainNode);
                templateNode.AddChild(domainNode);
            }

            if (includeArchetypes)
            {
                foreach (var archetype in GetArchetypesForTemplate(template.Index))
                {
                    var archetypeNode = FloraRenderingInspectorNode.ForArchetype(archetype, $"{key}/archetype:{archetype.Index}");
                    AddNode(archetypeNode);
                    templateNode.AddChild(archetypeNode);
                }
            }

            return templateNode;
        }

        private void AddNode(FloraRenderingInspectorNode node)
        {
            if (node == null)
                return;

            if (m_NodesByKey.TryAdd(node.Key, node))
                m_TabByNodeKey[node.Key] = m_BuildingTab;
        }

        private static string ComputeStructureSignature(IEnumerable<FloraRenderingInspectorNode> roots)
        {
            var builder = new StringBuilder();
            foreach (var root in roots)
                AppendSignature(builder, root);

            return builder.ToString();
        }

        private static void AppendSignature(StringBuilder builder, FloraRenderingInspectorNode node)
        {
            builder.Append(node.Key);
            builder.Append('|');
            builder.Append((int)node.Kind);
            builder.Append('|');
            builder.Append(node.Children.Count);
            builder.Append('\n');

            foreach (var child in node.Children)
                AppendSignature(builder, child);
        }

        private void BuildTreeItems(FloraRenderingInspectorTabModel tab, string searchText)
        {
            tab.RootItems.Clear();
            var search = searchText?.Trim() ?? string.Empty;
            tab.MatchCount = string.IsNullOrWhiteSpace(search) ? 0 : CountMatches(tab.RootNodes, search);

            foreach (var root in tab.RootNodes)
            {
                if (CreateTreeItem(root, search, out var item))
                    tab.RootItems.Add(item);
            }
        }

        private bool CreateTreeItem(FloraRenderingInspectorNode node, string search, out TreeViewItemData<FloraRenderingInspectorNode> item)
        {
            var childItems = new List<TreeViewItemData<FloraRenderingInspectorNode>>();
            foreach (var child in node.Children)
            {
                if (CreateTreeItem(child, search, out var childItem))
                    childItems.Add(childItem);
            }

            var searching = !string.IsNullOrWhiteSpace(search);
            var matches = !searching || NodeMatches(node, search);
            var include = matches || childItems.Count > 0 || node.Kind == FloraRenderingInspectorNodeKind.Root && !searching;
            if (!include)
            {
                item = default;
                return false;
            }

            node.Id = m_NextTreeItemId++;
            item = childItems.Count > 0
                ? new TreeViewItemData<FloraRenderingInspectorNode>(node.Id, node, childItems)
                : new TreeViewItemData<FloraRenderingInspectorNode>(node.Id, node);
            return true;
        }

        private static int CountMatches(IEnumerable<FloraRenderingInspectorNode> nodes, string search)
        {
            var count = 0;
            foreach (var node in nodes)
            {
                if ((node.IsSelectable || node.CountInSearch) && NodeMatches(node, search))
                    count++;

                count += CountMatches(node.Children, search);
            }

            return count;
        }

        private static bool NodeMatches(FloraRenderingInspectorNode node, string search)
        {
            if (Contains(node.Name, search) || Contains(node.Subtitle, search) || Contains(node.BadgeText, search) || Contains(node.CountText, search))
                return true;

            foreach (var value in node.SearchText)
            {
                if (Contains(value, search))
                    return true;
            }

            return false;
        }

        private static bool Contains(string value, string search)
            => !string.IsNullOrEmpty(value) && value.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0;

        private static void ResetTreeItemIds(IEnumerable<FloraRenderingInspectorNode> nodes)
        {
            foreach (var node in nodes)
            {
                node.Id = 0;
                ResetTreeItemIds(node.Children);
            }
        }

        private static FloraRenderingInspectorNode FindNode(FloraRenderingInspectorNode node, string key)
        {
            if (node == null)
                return null;

            if (node.Key == key)
                return node;

            foreach (var child in node.Children)
            {
                var match = FindNode(child, key);
                if (match != null)
                    return match;
            }

            return null;
        }

        private static IReadOnlyList<T> GetList<T>(Dictionary<int, List<T>> lookup, int key)
            => lookup.TryGetValue(key, out var list) ? list : Array.Empty<T>();

        private static void AddToLookup<T>(Dictionary<int, List<T>> lookup, int key, T value)
        {
            if (!lookup.TryGetValue(key, out var values))
            {
                values = new List<T>();
                lookup[key] = values;
            }

            values.Add(value);
        }

        private static void SortLookup<T>(Dictionary<int, List<T>> lookup, Comparison<T> comparison)
        {
            foreach (var values in lookup.Values)
                values.Sort(comparison);
        }

        private static int CompareCullingChunks(FloraDiagnosticsCullingChunk left, FloraDiagnosticsCullingChunk right)
        {
            var templateComparison = left.TemplateIndex.CompareTo(right.TemplateIndex);
            if (templateComparison != 0)
                return templateComparison;

            var archetypeComparison = left.ArchetypeIndex.CompareTo(right.ArchetypeIndex);
            if (archetypeComparison != 0)
                return archetypeComparison;

            return left.Index.CompareTo(right.Index);
        }

        private IEnumerable<FloraDiagnosticsSource> SortSources(IEnumerable<FloraDiagnosticsSource> sources)
        {
            return SortMode switch
            {
                FloraRenderingInspectorSortMode.Count => sources.OrderByDescending(source => source.InstanceCount).ThenBy(source => source.Name, StringComparer.OrdinalIgnoreCase),
                _ => sources.OrderBy(source => source.Name, StringComparer.OrdinalIgnoreCase).ThenBy(source => source.Index),
            };
        }

        private IEnumerable<FloraDiagnosticsTemplate> SortTemplates(IEnumerable<FloraDiagnosticsTemplate> templates)
        {
            return SortMode switch
            {
                FloraRenderingInspectorSortMode.Count => templates.OrderByDescending(template => template.InstanceCount).ThenBy(template => template.Name, StringComparer.OrdinalIgnoreCase),
                _ => templates.OrderBy(template => template.Name, StringComparer.OrdinalIgnoreCase).ThenBy(template => template.Index),
            };
        }

        private IEnumerable<FloraDiagnosticsArchetype> SortArchetypes(IEnumerable<FloraDiagnosticsArchetype> archetypes)
        {
            return SortMode switch
            {
                FloraRenderingInspectorSortMode.Count => archetypes.OrderByDescending(archetype => archetype.InstanceCount).ThenBy(archetype => archetype.Name, StringComparer.OrdinalIgnoreCase),
                _ => archetypes.OrderBy(archetype => archetype.Name, StringComparer.OrdinalIgnoreCase).ThenBy(archetype => archetype.Index),
            };
        }

        private IEnumerable<FloraDiagnosticsDraw> SortDraws(IEnumerable<FloraDiagnosticsDraw> draws)
        {
            return SortMode switch
            {
                FloraRenderingInspectorSortMode.Count => draws.OrderByDescending(draw => draw.CullingChunkCount).ThenBy(draw => draw.Name, StringComparer.OrdinalIgnoreCase),
                _ => draws.OrderBy(draw => draw.Name, StringComparer.OrdinalIgnoreCase).ThenBy(draw => draw.Index),
            };
        }

        private IEnumerable<FloraDiagnosticsBatchDomain> SortDomains(IEnumerable<FloraDiagnosticsBatchDomain> domains)
        {
            return SortMode switch
            {
                FloraRenderingInspectorSortMode.Count => domains.OrderByDescending(domain => domain.InstanceCapacity).ThenBy(domain => domain.BatchId, StringComparer.OrdinalIgnoreCase),
                _ => domains.OrderBy(domain => domain.BatchId, StringComparer.OrdinalIgnoreCase).ThenBy(domain => domain.Index),
            };
        }

        private IEnumerable<FloraDiagnosticsGraphicsBuffer> SortGraphicsBuffers(IEnumerable<FloraDiagnosticsGraphicsBuffer> buffers)
        {
            return SortMode switch
            {
                FloraRenderingInspectorSortMode.Count => buffers.OrderByDescending(buffer => buffer.Count).ThenBy(buffer => buffer.DisplayName, StringComparer.OrdinalIgnoreCase),
                _ => buffers.OrderBy(buffer => buffer.DisplayName, StringComparer.OrdinalIgnoreCase).ThenBy(buffer => buffer.Index),
            };
        }

        private IEnumerable<FloraRenderingInspectorShaderPropertyUsage> SortShaderPropertyUsages(IEnumerable<FloraRenderingInspectorShaderPropertyUsage> usages)
        {
            return SortMode switch
            {
                FloraRenderingInspectorSortMode.Count => usages.OrderByDescending(usage => usage.DomainCount).ThenBy(usage => usage.DisplayName, StringComparer.OrdinalIgnoreCase),
                _ => usages.OrderBy(usage => usage.DisplayName, StringComparer.OrdinalIgnoreCase).ThenBy(usage => usage.NameID),
            };
        }

        private static string GetShaderPropertyDisplayName(FloraDiagnosticsBatchProperty property)
            => string.IsNullOrEmpty(property.DisplayName) ? $"Property {property.NameID}" : property.DisplayName;

        private string GetTemplateName(int templateIndex)
            => TemplatesByIndex.TryGetValue(templateIndex, out var template) ? template.Name : $"Template {templateIndex}";

        private static bool HasValidGridCell(FloraDiagnosticsCullingChunk chunk)
            => chunk.CellIndex > 0 && chunk.BlockIndex > 0;

        private static bool TryGetFrameBounds(IEnumerable<FloraDiagnosticsCullingChunk> chunks, out Bounds bounds)
        {
            bounds = default;
            var hasBounds = false;
            foreach (var chunk in chunks ?? Enumerable.Empty<FloraDiagnosticsCullingChunk>())
            {
                if (chunk.CellBounds.IsEmpty())
                    continue;

                if (hasBounds)
                    bounds.Encapsulate(chunk.CellBounds);
                else
                    bounds = chunk.CellBounds;
                hasBounds = true;
            }

            return hasBounds;
        }

        private static string GetGridCellGroupKey(FloraDiagnosticsCullingChunk chunk)
            => HasValidGridCell(chunk) ? chunk.CellIndex.ToString() : "unassigned";

        private static int GetGridCellSortKey(FloraDiagnosticsCullingChunk chunk)
            => HasValidGridCell(chunk) ? chunk.CellIndex : int.MaxValue;

        private static string FormatGridLevelBadge(int level)
            => level >= 0 ? $"Level {level}" : "--";

        private static string GetGridLevelBadgeStyleClass(int level)
        {
            if (level < 0)
                return "flora-rendering-inspector__browser-badge--unassigned";

            const int minLevel = CullingGrid.MinCellLevel;
            const int maxLevel = CullingGrid.MaxBlockLevel;
            var bucket = maxLevel > minLevel ? (level - minLevel) * 6 / (maxLevel - minLevel) : 0;
            bucket = Math.Max(0, Math.Min(6, bucket));
            return $"flora-rendering-inspector__browser-badge--level-{bucket}";
        }

        private static string FormatCoordinates(UnityEngine.Vector3Int value)
            => $"{value.x}, {value.y}, {value.z}";

        private static string Count(int? count) => (count ?? 0).ToString("n0");
    }
}
