// Copyright © Magnetic Arcade. All Rights Reserved.

using System;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;

#if UNITY_EDITOR
#endif

namespace MA.Flora
{
    public sealed partial class FloraSystem
    {
        #region Terrain Registration

        /// <summary>
        /// Checks if a given terrain is registered with the Flora system.
        /// </summary>
        /// <param name="terrain">The terrain to check.</param>
        /// <returns>Returns true if the terrain is registered; otherwise, false.</returns>
        public bool IsTerrainRegistered(Terrain terrain)
        {
            if (terrain == null)
                throw new ArgumentNullException(nameof(terrain), $"{nameof(FloraSystem)}: Terrain cannot be null.");

            return m_Terrains.ContainsKey(terrain.GetEntityId());
        }

        /// <summary>
        /// Registers all active terrains in the scene for Flora-based rendering (disabling default terrain foliage).
        /// </summary>
        public void RegisterTerrains()
        {
            RegisterTerrains(Terrain.activeTerrains);
        }

        /// <summary>
        /// Registers the specified array of terrains for Flora-based rendering.
        /// </summary>
        /// <param name="terrains">The terrains to register.</param>
        public void RegisterTerrains(Terrain[] terrains)
        {
            foreach (var terrain in terrains)
                RegisterTerrain(terrain);
        }

        /// <summary>
        /// Registers a single terrain for Flora-based rendering.
        /// </summary>
        /// <param name="terrain">The terrain to register.</param>
        public void RegisterTerrain(Terrain terrain)
        {
            if (terrain == null)
                throw new ArgumentNullException(nameof(terrain), $"{nameof(FloraSystem)}: Terrain cannot be null.");

            EntityId terrainEntityId = terrain.GetEntityId();
            if (!m_Terrains.TryAdd(terrainEntityId, terrain))
                return;

            if (!terrain.TryGetComponent(out FloraTerrainProvider _))
                terrain.gameObject.AddComponent<FloraTerrainProvider>();

            if (m_ResolvedSettings.IsTerrainFoliageEnabled)
            {
                m_NativeContext.TerrainManager.ValueRW.Register(terrain);
            }

            ApplyTerrainFoliageOwnership(terrain, m_ResolvedSettings.IsTerrainFoliageEnabled);

            SetEditorDataChanged();
        }

        /// <summary>
        /// Unregisters all previously registered terrains.
        /// </summary>
        public void UnregisterTerrains()
        {
            using (HashSetPool<EntityId>.Get(out var entityIds))
            {
                foreach (var terrainEntityId in m_Terrains.Keys)
                    entityIds.Add(terrainEntityId);

                foreach (var terrain in entityIds)
                    UnregisterTerrain(terrain);
            }
        }

        /// <summary>
        /// Unregisters a single terrain from Flora-based rendering.
        /// </summary>
        /// <param name="terrain">The terrain to unregister.</param>
        public void UnregisterTerrain(Terrain terrain)
        {
            if (terrain == null)
                return;

            UnregisterTerrain(terrain.GetEntityId());
        }

        private void UnregisterTerrain(EntityId terrainEntityId)
        {
            if (m_Terrains.Remove(terrainEntityId, out var terrain))
            {
                m_NativeContext.TerrainManager.ValueRW.Unregister(terrainEntityId);
                if (terrain)
                    ApplyTerrainFoliageOwnership(terrain, floraOwnsTerrainFoliage: false);

                SetEditorDataChanged();
            }
        }

        #endregion

        #region Registered Objects

        /// <summary>
        /// Gets the list of active terrains registered with Flora.
        /// </summary>
        /// <param name="terrains">A list to populate with the active terrains.</param>
        public void GetActiveTerrains(List<Terrain> terrains)
        {
            terrains.Clear();
            foreach (var terrain in m_Terrains.Values)
                terrains.Add(terrain);
        }

        /// <summary>
        /// Gets the list of active instance containers.
        /// </summary>
        /// <param name="containers">A list to populate with the active containers.</param>
        public void GetActiveContainers(List<FloraInstanceContainer> containers)
        {
            containers.Clear();
            foreach (var container in m_Containers.Values)
                containers.Add(container);
        }

        /// <summary>
        /// Gets the list of active Flora renderer groups.
        /// </summary>
        /// <param name="instanceRenderers">A list to populate with the active renderer groups.</param>
        public void GetActiveRenderers(List<FloraInstanceRenderer> instanceRenderers)
        {
            instanceRenderers.Clear();
            foreach (var renderer in m_InstanceRenderers.Values)
                instanceRenderers.Add(renderer);
        }

        #endregion

        #region Internal Registration

        internal void RegisterInstanceContainer(FloraInstanceContainer container)
        {
            if (container == null)
                return;

            EntityId entityId = container.GetEntityId();
            if (!m_Containers.TryAdd(entityId, container))
                return;

            m_NativeContext.InstanceManager.ValueRW.CreateInstances(container.Prefab, container.transform, entityId, container.InstanceHandles, container.LocalTransforms);
            if (container.InstanceCount > 0)
                m_NativeContext.InstanceManager.ValueRW.SetInstanceInContainerIndices(container.InstanceHandles, container);
            m_NativeContext.InstanceManager.ValueRW.RegisterTrackedContainer(entityId, container.InstanceHandles, container.LocalTransforms);
        }

        internal void UnregisterInstanceContainer(FloraInstanceContainer container)
        {
            EntityId containerEntityId = container.GetEntityId();
            m_Containers.Remove(containerEntityId);
            m_NativeContext.InstanceManager.ValueRW.UnregisterTrackedContainer(containerEntityId);
            m_NativeContext.InstanceManager.ValueRW.Destroy(container.InstanceHandles);
        }

        internal FloraInstanceHandle CreateContainerInstance(GameObject prefab, Transform parent, EntityId containerEntity, FloraInstanceTransform localTransform)
        {
            return m_NativeContext.InstanceManager.ValueRW.CreateInstance(prefab, parent, containerEntity, localTransform);
        }

        internal void CreateContainerInstances(GameObject prefab, Transform parent, EntityId containerEntity, NativeArray<FloraInstanceHandle> instanceHandles, NativeArray<FloraInstanceTransform> localTransforms)
        {
            m_NativeContext.InstanceManager.ValueRW.CreateInstances(prefab, parent, containerEntity, instanceHandles, localTransforms);
        }

        internal void AppendTrackedContainerInstances(EntityId containerEntity, NativeArray<FloraInstanceHandle> instanceHandles, NativeArray<FloraInstanceTransform> localTransforms)
        {
            m_NativeContext.InstanceManager.ValueRW.AppendTrackedContainerInstances(containerEntity, instanceHandles, localTransforms);
        }

        internal void UpdateTrackedContainerLocalTransforms(EntityId containerEntity, int startIndex, NativeArray<FloraInstanceTransform> localTransforms)
        {
            m_NativeContext.InstanceManager.ValueRW.UpdateTrackedContainerLocalTransforms(containerEntity, startIndex, localTransforms);
        }

        internal void UpdateTrackedContainerLocalTransforms(EntityId containerEntity, NativeArray<int> indices, NativeArray<FloraInstanceTransform> localTransforms)
        {
            m_NativeContext.InstanceManager.ValueRW.UpdateTrackedContainerLocalTransforms(containerEntity, indices, localTransforms);
        }

        internal void RemoveTrackedContainerInstance(EntityId containerEntity, int index)
        {
            m_NativeContext.InstanceManager.ValueRW.RemoveTrackedContainerInstance(containerEntity, index);
        }

        internal void ClearTrackedContainerInstances(EntityId containerEntity)
        {
            m_NativeContext.InstanceManager.ValueRW.ClearTrackedContainerInstances(containerEntity);
        }

        internal void RegisterInstanceRenderer(FloraInstanceRenderer instanceRenderer)
        {
            if (instanceRenderer == null)
                return;

            EntityId entityId = instanceRenderer.GetEntityId();
            if (m_InstanceRenderers.ContainsKey(entityId))
                return;

            if (!m_InstanceRendererManager.Register(instanceRenderer))
                return;

            m_InstanceRenderers.Add(entityId, instanceRenderer);
            SetEditorDataChanged();

            var childEntityIds = HashSetPool<EntityId>.Get();

            using (ListPool<Renderer>.Get(out var childRenderers))
            {
                if (instanceRenderer.TryGetComponent(out LODGroup lodGroup))
                {
                    var lods = lodGroup.GetLODs();
                    foreach (var lod in lods)
                    {
                        foreach (var lodRenderer in lod.renderers)
                        {
                            if (lodRenderer == null)
                                continue;

                            EntityId childEntityId = lodRenderer.GetEntityId();
                            if (m_Renderers.TryAdd(childEntityId, lodRenderer))
                            {
                                lodRenderer.forceRenderingOff = RenderingEnabled;
                                childEntityIds.Add(childEntityId);
                            }
                        }
                    }
                }
                else
                {
                    instanceRenderer.GetComponentsInChildren(childRenderers);
                    foreach (var childRenderer in childRenderers)
                    {
                        if (childRenderer == null)
                            continue;

                        EntityId childEntityId = childRenderer.GetEntityId();
                        if (m_Renderers.TryAdd(childEntityId, childRenderer))
                        {
                            childRenderer.forceRenderingOff = RenderingEnabled;
                            childEntityIds.Add(childEntityId);
                        }
                    }
                }

                m_InstanceRendererChildren.Add(entityId, childEntityIds);
            }
        }

        internal void UnregisterInstanceRenderer(FloraInstanceRenderer instanceRenderer)
        {
            EntityId rendererEntityId = instanceRenderer.GetEntityId();
            if (m_InstanceRenderers.Remove(rendererEntityId))
            {
                m_InstanceRendererManager.Destroy(instanceRenderer);

                if (m_InstanceRendererChildren.TryGetValue(rendererEntityId, out var childInstanceIds))
                {
                    foreach (var child in childInstanceIds)
                    {
                        if (m_Renderers.Remove(child, out var childRenderer) && childRenderer)
                            childRenderer.forceRenderingOff = false;
                    }

                    m_InstanceRendererChildren.Remove(rendererEntityId);
                    HashSetPool<EntityId>.Release(childInstanceIds);
                }

                SetEditorDataChanged();
            }
        }

        internal void RefreshInstanceRendererRenderSources()
        {
            TemplateUtility.NextFrame();

            using (ListPool<GameObject>.Get(out var renderSources))
            {
                m_InstanceRendererManager.GetRenderSourceObjects(renderSources);
                foreach (var renderSource in renderSources)
                {
                    if (renderSource != null)
                        m_NativeContext.TemplateManager.ValueRW.UpdateSource(renderSource);
                }
            }
        }

        private void EnableUnityTerrainRendering()
        {
            foreach (var terrain in m_Terrains.Values)
            {
                if (terrain)
                    ApplyTerrainFoliageOwnership(terrain, floraOwnsTerrainFoliage: false);
            }
        }

        private void DisableUnityTerrainRendering()
        {
            foreach (var terrain in m_Terrains.Values)
            {
                if (terrain)
                    ApplyTerrainFoliageOwnership(terrain, floraOwnsTerrainFoliage: true);
            }
        }

        private static void ApplyTerrainFoliageOwnership(Terrain terrain, bool floraOwnsTerrainFoliage)
        {
            if (terrain == null)
                return;

            terrain.drawTreesAndFoliage = !floraOwnsTerrainFoliage;
        }

        private void EnableUnityRenderers()
        {
            foreach (var renderer in m_Renderers.Values)
            {
                if (renderer)
                    renderer.forceRenderingOff = false;
            }
        }

        private void DisableUnityRenderers()
        {
            foreach (var renderer in m_Renderers.Values)
            {
                if (renderer)
                    renderer.forceRenderingOff = true;
            }
        }

        #endregion

        #region Internal Queries

        internal NativeArray<FloraInstanceHandle> FindInstancesInPlanes(NativeArray<Plane> planes, Allocator allocator)
        {
            m_NativeContext.InstanceManager.ValueRW.FlushPendingSpatialUpdates();
            m_NativeContext.CullingGrid.CullInstancesInSelectionPlanesWithBurst(0, 0, planes, allocator, out var instances);
            return instances;
        }

        internal NativeArray<FloraInstanceHandle> FindInstancesInPlanes(InstanceTag includeTags, InstanceTag excludeTags, NativeArray<Plane> planes, Allocator allocator)
        {
            m_NativeContext.InstanceManager.ValueRW.FlushPendingSpatialUpdates();
            m_NativeContext.CullingGrid.CullInstancesInSelectionPlanesWithBurst(includeTags, excludeTags, planes, allocator, out var instances);
            return instances;
        }

        internal void GetInstanceRendererObjects(NativeArray<FloraInstanceHandle> instances, List<GameObject> instanceRendererObjects)
        {
            m_InstanceRendererManager.GetInstanceRendererObjects(instances, instanceRendererObjects);
        }

        internal InstanceInContainer GetInstanceInContainer(FloraInstanceHandle instance)
        {
            return InstanceRegistry.Data.GetInstanceInContainer(instance);
        }

        internal FloraInstanceContainer GetParentInstanceContainer(FloraInstanceHandle instance)
        {
            return m_NativeContext.InstanceManager.ValueRO.GetParentInstanceContainer(instance);
        }

        internal int GetIndexInInstanceContainer(FloraInstanceHandle instance)
        {
            return m_NativeContext.InstanceManager.ValueRO.GetIndexInInstanceContainer(instance);
        }

        internal void SetInstanceInContainer(FloraInstanceHandle instance, FloraInstanceContainer instanceContainer, int index)
        {
            m_NativeContext.InstanceManager.ValueRW.SetInstanceInContainer(instance, instanceContainer, index);
        }

        internal void SetInstanceInContainerIndices(NativeArray<FloraInstanceHandle> instances, FloraInstanceContainer instanceContainer, int firstIndex = 0)
        {
            m_NativeContext.InstanceManager.ValueRW.SetInstanceInContainerIndices(instances, instanceContainer, firstIndex);
        }

        internal NativeParallelMultiHashMap<EntityId, int> GetContainerIndexMap(NativeArray<FloraInstanceHandle> instances, Allocator allocator)
        {
            return m_NativeContext.InstanceManager.ValueRO.GetInstanceContainerIndexMap(instances, allocator);
        }

        internal NativeArray<InstanceInContainer> GetInstanceInContainersWithIndices(NativeArray<FloraInstanceHandle> instances, Allocator allocator, out NativeArray<int> originalIndices)
        {
            return m_NativeContext.InstanceManager.ValueRO.GetInstanceInContainersAndIndices(instances, allocator, out originalIndices);
        }

        internal TreeInTerrain GetTreeInTerrain(FloraInstanceHandle instance)
        {
            return InstanceRegistry.Data.GetTreeInTerrain(instance);
        }

        internal void SetTerrainChanged(Terrain terrain, TerrainChangedFlags changedFlags)
        {
            if (terrain == null) return;
            EntityId terrainEntityId = terrain.GetEntityId();
            m_NativeContext.TerrainManager.ValueRW.SetDirty(terrainEntityId, changedFlags);
        }

        internal NativeParallelMultiHashMap<DetailInTerrain, FloraInstanceHandle> GetDetailInstanceMap(NativeArray<FloraInstanceHandle> instances, Allocator allocator)
        {
            return m_NativeContext.InstanceManager.ValueRO.GetDetailInstanceMap(instances, allocator);
        }

        internal NativeArray<TreeInTerrain> GetValidTreeInTerrains(NativeArray<FloraInstanceHandle> instances, Allocator allocator)
        {
            return m_NativeContext.InstanceManager.ValueRO.GetValidTreeInTerrains(instances, allocator);
        }

        internal NativeArray<TreeInTerrain> GetValidTreeInTerrainsWithIndices(NativeArray<FloraInstanceHandle> instances, Allocator allocator, out NativeArray<int> originalIndices)
        {
            return m_NativeContext.InstanceManager.ValueRO.GetValidTreeInTerrainsAndIndices(instances, allocator, out originalIndices);
        }

        #endregion
    }
}
