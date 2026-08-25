// Copyright © Magnetic Arcade. All Rights Reserved.

using System;
using System.Collections.Generic;
using MA.InternalBridge;
using Unity.Collections;
using Unity.Jobs;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Profiling;

namespace MA.Flora
{
    partial class FloraSystem
    {
        private static readonly ProfilerMarker UpdateTrackingMarker = new ProfilerMarker("Flora.UpdateTracking");

        private void SetupTracking()
        {
            TeardownTracking();

            m_ObjectTracker = new UnityObjectDispatcher();
            const UnityObjectDispatcher.TypeTrackingFlags sceneAndAssets = UnityObjectDispatcher.TypeTrackingFlags.Assets | UnityObjectDispatcher.TypeTrackingFlags.SceneObjects;
            m_ObjectTracker.EnableTypeTracking<FloraAdditionalRendererSettings>(sceneAndAssets);
            m_ObjectTracker.EnableTypeTracking<MeshRenderer>(sceneAndAssets);
            m_ObjectTracker.EnableTypeTracking<MeshFilter>(sceneAndAssets);
            m_ObjectTracker.EnableTypeTracking<LODGroup>(sceneAndAssets);
            m_ObjectTracker.EnableTypeTracking<Terrain>();
            m_ObjectTracker.EnableTypeTracking<Material>();
            m_ObjectTracker.EnableTypeTracking<Mesh>();

            m_ObjectTracker.EnableTransformTracking<FloraInstanceContainer>(UnityObjectDispatcher.TransformTrackingType.GlobalTRS);
            m_ObjectTracker.EnableTransformTracking<FloraInstanceRenderer>(UnityObjectDispatcher.TransformTrackingType.GlobalTRS);
            m_ObjectTracker.EnableTransformTracking<Terrain>(UnityObjectDispatcher.TransformTrackingType.GlobalTRS);
        }

        private void TeardownTracking()
        {
            m_ObjectTracker?.Dispose();
            m_ObjectTracker = null;
        }

        private void UpdateTracking()
        {
            using var _ = UpdateTrackingMarker.Auto();

            using var lodGroupPrefabChanges = m_ObjectTracker.GetTypeChangesAndClear<LODGroup>(Allocator.TempJob, noScriptingArray: true);
            if (lodGroupPrefabChanges.changedID.Length > 0 || lodGroupPrefabChanges.destroyedID.Length > 0)
                TemplateUtility.NextFrame();
            if (lodGroupPrefabChanges.changedID.Length > 0)
                m_NativeContext.TemplateManager.ValueRW.UpdateComponents(lodGroupPrefabChanges.changedID);
            if (lodGroupPrefabChanges.destroyedID.Length > 0)
                m_NativeContext.TemplateManager.ValueRW.DestroyComponents(lodGroupPrefabChanges.destroyedID);

            using var rendererPrefabChanges = m_ObjectTracker.GetTypeChangesAndClear<MeshRenderer>(Allocator.TempJob, noScriptingArray: true);
            if (rendererPrefabChanges.changedID.Length > 0 || rendererPrefabChanges.destroyedID.Length > 0)
                TemplateUtility.NextFrame();
            if (rendererPrefabChanges.changedID.Length > 0)
                m_NativeContext.TemplateManager.ValueRW.UpdateComponents(rendererPrefabChanges.changedID);
            if (rendererPrefabChanges.destroyedID.Length > 0)
                m_NativeContext.TemplateManager.ValueRW.DestroyComponents(rendererPrefabChanges.destroyedID);

            using var meshFilterChanges = m_ObjectTracker.GetTypeChangesAndClear<MeshFilter>(Allocator.TempJob, noScriptingArray: true);
            if (meshFilterChanges.changedID.Length > 0 || meshFilterChanges.destroyedID.Length > 0)
                TemplateUtility.NextFrame();
            if (meshFilterChanges.changedID.Length > 0)
                m_NativeContext.TemplateManager.ValueRW.UpdateComponents(meshFilterChanges.changedID);
            if (meshFilterChanges.destroyedID.Length > 0)
                m_NativeContext.TemplateManager.ValueRW.DestroyComponents(meshFilterChanges.destroyedID);

            using var additionalSettingsPrefabChanges = m_ObjectTracker.GetTypeChangesAndClear<FloraAdditionalRendererSettings>(Allocator.TempJob, noScriptingArray: true);
            if (additionalSettingsPrefabChanges.changedID.Length > 0 || additionalSettingsPrefabChanges.destroyedID.Length > 0)
                TemplateUtility.NextFrame();
            if (additionalSettingsPrefabChanges.changedID.Length > 0)
                m_NativeContext.TemplateManager.ValueRW.UpdateComponents(additionalSettingsPrefabChanges.changedID);
            if (additionalSettingsPrefabChanges.destroyedID.Length > 0)
                m_NativeContext.TemplateManager.ValueRW.DestroyComponents(additionalSettingsPrefabChanges.destroyedID);

            using var terrainChanges = m_ObjectTracker.GetTypeChangesAndClear<Terrain>(Allocator.TempJob, noScriptingArray: true);
            if (terrainChanges.changedID.Length > 0)
            {
                foreach (var id in terrainChanges.changedID)
                {

                    var terrain = id.ToObject<Terrain>();
                    if (terrain == null)
                    {
                        m_DisabledTerrains.Remove(id);
                        continue;
                    }

                    var isRegistered = m_Terrains.ContainsKey(id);
                    var isActiveAndEnabled = terrain.gameObject.activeInHierarchy && terrain.enabled;
                    switch (isRegistered)
                    {
                        case true when !isActiveAndEnabled:
                        {
                            UnregisterTerrain(id);
                            m_DisabledTerrains.Add(id);

                            continue;
                        }
                        case false when isActiveAndEnabled:
                        {
                            var shouldRegister = m_ResolvedSettings.IsAutoRegisterTerrainsEnabled || m_DisabledTerrains.Contains(id);
                            if (shouldRegister)
                            {
                                RegisterTerrain(terrain);
                                m_DisabledTerrains.Remove(id);
                            }

                            break;
                        }
                    }

                    if (m_Terrains.TryGetValue(id, out var registeredTerrain))
                    {
                        // Terrain inspector edits can toggle this flag; reassert Flora ownership for registered terrains.
                        ApplyTerrainFoliageOwnership(registeredTerrain, m_ResolvedSettings.IsTerrainFoliageEnabled);
                    }

                    BatchAssetManager.OnTerrainChanged(id);
                }

                m_NativeContext.TerrainManager.ValueRW.SetSettingsDirty(terrainChanges.changedID);
            }

            if (terrainChanges.destroyedID.Length > 0)
            {
                foreach (var id in terrainChanges.destroyedID)
                {
                    UnregisterTerrain(id);
                    BatchAssetManager.OnTerrainRemoved(id);
                }
            }

            using var materialChanges = m_ObjectTracker.GetTypeChangesAndClear<Material>(Allocator.TempJob, noScriptingArray: true);
            m_NativeContext.TemplateManager.ValueRW.MaterialsChanged(materialChanges.changedID);

            if (materialChanges.destroyedID.Length > 0)
            {
                BatchAssetManager.MaterialsDestroyed(materialChanges.destroyedID);
                m_NativeContext.TemplateManager.ValueRW.MaterialsDestroyed(materialChanges.destroyedID);
            }

            using var meshChanges = m_ObjectTracker.GetTypeChangesAndClear<Mesh>(Allocator.TempJob, noScriptingArray: true);
            m_NativeContext.TemplateManager.ValueRW.MeshesChanged(meshChanges.changedID);

            if (meshChanges.destroyedID.Length > 0)
            {
                BatchAssetManager.MeshesDestroyed(meshChanges.destroyedID);
                m_NativeContext.TemplateManager.ValueRW.MeshesDestroyed(meshChanges.destroyedID);
            }

            using var containerTransformChanges = m_ObjectTracker.GetTransformChangesAndClear<FloraInstanceContainer>(UnityObjectDispatcher.TransformTrackingType.GlobalTRS, Allocator.TempJob);
            if (containerTransformChanges.transformedID.Length > 0)
            {
                Profiler.BeginSample("Flora.UpdateTracking.ContainerTransform");
                var containerTransformHandle = m_NativeContext.InstanceManager.ValueRW.ScheduleUpdateTrackedContainerTransforms(containerTransformChanges, default);
                containerTransformHandle.Complete();
                Profiler.EndSample();
            }

            using var terrainTransformChanges = m_ObjectTracker.GetTransformChangesAndClear<Terrain>(UnityObjectDispatcher.TransformTrackingType.GlobalTRS, Allocator.TempJob);
            foreach (var id in terrainTransformChanges.transformedID)
                m_NativeContext.TerrainManager.ValueRW.SetTransformDirty(id);

            using var instanceRendererTransformChanges = m_ObjectTracker.GetTransformChangesAndClear<FloraInstanceRenderer>(UnityObjectDispatcher.TransformTrackingType.GlobalTRS, Allocator.TempJob);
            m_InstanceRendererManager.OnTransformDataChanged(instanceRendererTransformChanges);

            if (m_TransformChangedActions.Count > 0)
            {
                foreach (var type in m_TransformChangedActions.Keys)
                {
                    using var changes = m_ObjectTracker.GetTransformChangesAndClear(type, UnityObjectDispatcher.TransformTrackingType.GlobalTRS, Allocator.TempJob);
                    if (changes.transformedID.Length > 0)
                    {
                        if (m_TransformChangedActions.TryGetValue(type, out var actions))
                        {
                            foreach (var action in actions)
                            {
                                foreach (var id in changes.transformedID)
                                    action(id);
                            }
                        }
                    }
                }
            }
        }

        internal delegate void TransformChangedAction(EntityId entityId);

        private Dictionary<Type, HashSet<TransformChangedAction>> m_TransformChangedActions = new Dictionary<Type, HashSet<TransformChangedAction>>();

        internal void AddTransformChangedEvent<T>(TransformChangedAction onChanged) where T : Component
        {
            if (!m_TransformChangedActions.TryGetValue(typeof(T), out var actions))
            {
                actions = new HashSet<TransformChangedAction>();
                m_TransformChangedActions.Add(typeof(T), actions);
                m_ObjectTracker.EnableTransformTracking<T>(UnityObjectDispatcher.TransformTrackingType.GlobalTRS);
            }

            actions.Add(onChanged);
        }

        internal void RemoveTransformChangedEvent<T>(TransformChangedAction onChanged) where T : Component
        {
            if (m_TransformChangedActions.TryGetValue(typeof(T), out var actions))
            {
                actions.Remove(onChanged);
                if (actions.Count == 0)
                {
                    m_TransformChangedActions.Remove(typeof(T));
                    m_ObjectTracker.DisableTransformTracking<T>(UnityObjectDispatcher.TransformTrackingType.GlobalTRS);
                }
            }
        }
    }
}
