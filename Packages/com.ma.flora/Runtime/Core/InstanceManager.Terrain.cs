// Copyright © Magnetic Arcade. All Rights Reserved.

using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace MA.Flora
{
    internal unsafe partial struct InstanceManager
    {
        [ExcludeFromBurstCompatTesting("Takes managed objects")]
        public void InstantiateTrees(Terrain terrain, GameObject prefab, NativeArray<FloraInstanceHandle> instances, NativeArray<FloraLocalToWorld> localToWorld)
        {
            var templateOptions = TemplateOptions.None;
            if (!FloraSystem.Instance.AllowPerTreeMotionVectors)
                templateOptions |= TemplateOptions.DisableMotionVectors;
            if (!FloraSystem.Instance.AllowPerTreeLightProbes)
                templateOptions |= TemplateOptions.DisableLightProbes;
            templateOptions |= TemplateOptions.DisableLightmaps;
            SourceTemplateBinding binding = m_TemplateManager.ValueRW.RegisterSourceBinding(prefab, prefab, templateOptions);

            CreateInstances(instances.GetUnsafePtrT(), localToWorld.GetUnsafePtrT(), instances.Length, new InstantiateParams
            {
                AdditionalTags = InstanceTag.TerrainTree,
                Scene = terrain.gameObject.scene,
                SourceRecord = binding.SourceRecord,
                Template = binding.Template,
                SceneEntityId = terrain.gameObject.GetEntityId(),
                LightmapIndex = -1,
                Layer = (byte)prefab.layer,
                LightmapST = new float4(1f, 1f, 0f, 0f),
                MaxRenderDistance = terrain.treeDistance,
                SceneCullingMask = terrain.gameObject.sceneCullingMask,
                HasLocalToWorld = false,
                ParentLocalToWorld = FloraLocalToWorld.Identity,
            });
        }

        public void InstantiateTreesWithBurst(
            EntityId terrainId, EntityObjectRef<GameObject> prefab,
            NativeArray<FloraInstanceHandle> instances, NativeArray<FloraLocalToWorld> localToWorld)
        {
            InstantiateTreesFromBurst(Self, terrainId, prefab, instances, localToWorld);
        }

        [BurstMonoInteropMethod]
        private static void _InstantiateTreesFromBurst(
            InstanceManager* data,
            in EntityId terrainId,
            in EntityObjectRef<GameObject> prefab,
            in NativeArray<FloraInstanceHandle> instances,
            in NativeArray<FloraLocalToWorld> matrices)
        {
            if (terrainId.IsValid() && prefab.IsValid() && instances.Length > 0)
            {
                data->InstantiateTrees(terrainId.ToObject<Terrain>(), prefab, instances, matrices);
            }
        }

        [ExcludeFromBurstCompatTesting("Takes managed objects")]
        public void InstantiateDetails(Terrain terrain, in TerrainDetailPrototype prototype,
            NativeArray<FloraInstanceHandle> instances, NativeArray<FloraLocalToWorld> localToWorld)
        {
            var templateOptions = TemplateOptions.None;
            if (!FloraSystem.Instance.AllowPerDetailMotionVectors)
                templateOptions |= TemplateOptions.DisableMotionVectors;
            if (!FloraSystem.Instance.AllowPerDetailLightProbes)
                templateOptions |= TemplateOptions.DisableLightProbes;
            templateOptions |= TemplateOptions.DisableLightmaps;

            GameObject prefab;
            Material grassMaterial = null;
            if (prototype.UsePrototypeMesh && prototype.Prototype.IsValid())
            {
                prefab = prototype.Prototype;
            }
            else if (!prototype.UsePrototypeMesh && prototype.PrototypeTexture.IsValid())
            {
                prefab = BatchAssetManager.GetTerrainGrassPlaceholderPrefab();
                if (prefab == null)
                {
                    Debug.LogError("Failed to get terrain grass placeholder prefab from BatchAssetManager.");
                    return;
                }

                grassMaterial = BatchAssetManager.GetOrCreateTerrainGrassMaterial(prototype);
                if (grassMaterial == null)
                {
                    Debug.LogError($"Invalid Terrain Detail Prototype for terrain '{terrain.name}'. " +
                                   $"Detail uses a Prototype Texture but does not have a valid Prototype Material assigned.");
                    return;
                }
            }
            else
            {
                Debug.LogError($"Invalid Terrain Detail Prototype for terrain '{terrain.name}'. " +
                               $"Detail must have either a valid Prototype Mesh or a valid Prototype Texture.");
                return;
            }

            SourceTemplateBinding binding = m_TemplateManager.ValueRW.RegisterSourceBinding(prefab, prefab, templateOptions, grassMaterial);

            CreateInstances(instances.GetUnsafePtrT(), localToWorld.GetUnsafePtrT(), instances.Length, new InstantiateParams
            {
                AdditionalTags = InstanceTag.TerrainDetail,
                Scene = terrain.gameObject.scene,
                SourceRecord = binding.SourceRecord,
                Template = binding.Template,
                SceneEntityId = terrain.gameObject.GetEntityId(),
                TerrainDetailLayerIndex = prototype.Index,
                LightmapIndex = -1,
                Layer = (byte)prefab.layer,
                LightmapST = new float4(1f, 1f, 0f, 0f),
                MaxRenderDistance = terrain.detailObjectDistance,
                SceneCullingMask = terrain.gameObject.sceneCullingMask,
                HasLocalToWorld = false,
                ParentLocalToWorld = FloraLocalToWorld.Identity,
            });
        }

        public void InstantiateDetailsFromBurst(
            EntityId terrain, TerrainDetailPrototype* prototype,
            NativeArray<FloraInstanceHandle> instances, NativeArray<FloraLocalToWorld> localToWorld)
        {
            InstantiateDetailsFromBurstInternal(Self, terrain, prototype, instances, localToWorld);
        }

        [BurstMonoInteropMethod]
        private static void _InstantiateDetailsFromBurstInternal(
            InstanceManager* data,
            in EntityId terrain,
            TerrainDetailPrototype* prototype,
            in NativeArray<FloraInstanceHandle> instances,
            in NativeArray<FloraLocalToWorld> matrices)
        {
            if (instances.Length > 0)
            {
                data->InstantiateDetails(terrain.ToObject<Terrain>(), *prototype, instances, matrices);
            }
        }

        public Terrain GetTerrain(FloraInstanceHandle instance)
        {
            if (!Exists(instance))
                return null;

            TreeInTerrain treeInTerrain = InstanceRegistry.Data.GetTreeInTerrain(instance);
            if (treeInTerrain.TerrainEntity.IsValid())
                return treeInTerrain.TerrainEntity.ToObject<Terrain>();

            DetailInTerrain detailInTerrain = InstanceRegistry.Data.GetDetailInTerrain(instance);
            if (detailInTerrain.Terrain.IsValid())
                return detailInTerrain.Terrain;

            return null;
        }

        public int GetTreeIndex(FloraInstanceHandle instance)
        {
            if (!Exists(instance))
                return -1;

            TreeInTerrain treeInTerrain = InstanceRegistry.Data.GetTreeInTerrain(instance);
            if (treeInTerrain.TerrainEntity.IsValid())
                return treeInTerrain.IndexInTreeInstances;

            return -1;
        }

        public int GetDetailPrototypeIndex(FloraInstanceHandle instance)
        {
            if (!Exists(instance))
                return -1;

            DetailInTerrain detailInTerrain = InstanceRegistry.Data.GetDetailInTerrain(instance);
            if (detailInTerrain.Terrain.IsValid())
                return detailInTerrain.LayerIndex;

            return -1;
        }

        public NativeParallelMultiHashMap<DetailInTerrain, FloraInstanceHandle> GetDetailInstanceMap(NativeArray<FloraInstanceHandle> instances, Allocator allocator)
        {
            GetDetailInstanceMapWithBurst(Self, instances, allocator, out var result);
            return result;
        }

        [BurstCompile]
        private static void GetDetailInstanceMapWithBurst(
            InstanceManager* data,
            in NativeArray<FloraInstanceHandle> instances,
            Allocator allocator,
            out NativeParallelMultiHashMap<DetailInTerrain, FloraInstanceHandle> result)
        {
            result = new NativeParallelMultiHashMap<DetailInTerrain, FloraInstanceHandle>(instances.Length, allocator);
            for (int i = 0; i < instances.Length; i++)
            {
                if (data->Exists(instances[i]))
                {
                    var detailInTerrain = InstanceRegistry.Data.GetDetailInTerrain(instances[i]);
                    if (detailInTerrain.Terrain.IsValid())
                        result.Add(detailInTerrain, instances[i]);
                }
            }
        }

        public NativeArray<TreeInTerrain> GetValidTreeInTerrains(NativeArray<FloraInstanceHandle> instances, Allocator allocator)
        {
            GetValidTreeInTerrainsWithBurst(Self, instances, allocator, out var result);
            return result;
        }

        [BurstCompile]
        private static void GetValidTreeInTerrainsWithBurst(
            InstanceManager* data,
            in NativeArray<FloraInstanceHandle> instances,
            Allocator allocator,
            out NativeArray<TreeInTerrain> result)
        {
            using var hash = new NativeParallelMultiHashMap<EntityId, TreeInTerrain>(instances.Length, allocator);
            var count = 0;

            for (int i = 0; i < instances.Length; i++)
            {
                if (data->Exists(instances[i]))
                {
                    var treeInTerrain = InstanceRegistry.Data.GetTreeInTerrain(instances[i]);
                    if (treeInTerrain.TerrainEntity.IsValid())
                    {
                        hash.Add(treeInTerrain.TerrainEntity, treeInTerrain);
                        count++;
                    }
                }
            }

            using var keys = hash.GetKeyArray(Allocator.TempJob);
            var keyCount = keys.Unique();
            var writeIndex = 0;
            result = new NativeArray<TreeInTerrain>(count, allocator, NativeArrayOptions.UninitializedMemory);

            for (int i = 0; i < keyCount; i++)
            {
                foreach (var instance in hash.GetValuesForKey(keys[i]))
                    result[writeIndex++] = instance;
            }
        }

        private struct TreeInTerrainIndexPair : IComparable<TreeInTerrainIndexPair>
        {
            public TreeInTerrain Tree;
            public int Index;
            public int CompareTo(TreeInTerrainIndexPair other) => Tree.CompareTo(other.Tree);
        }

        public NativeArray<TreeInTerrain> GetValidTreeInTerrainsAndIndices(NativeArray<FloraInstanceHandle> instances, Allocator allocator, out NativeArray<int> indices)
        {
            GetValidTreeInTerrainsAndIndicesWithBurst(Self, instances, allocator, out indices, out var result);
            return result;
        }

        [BurstCompile]
        private static void GetValidTreeInTerrainsAndIndicesWithBurst(
            InstanceManager* data,
            in NativeArray<FloraInstanceHandle> instances,
            Allocator allocator,
            out NativeArray<int> indices,
            out NativeArray<TreeInTerrain> result)
        {
            using var hash = new NativeParallelMultiHashMap<EntityId, TreeInTerrainIndexPair>(instances.Length, allocator);
            var count = 0;
            for (int i = 0; i < instances.Length; i++)
            {
                if (data->Exists(instances[i]))
                {
                    var treeInTerrain = InstanceRegistry.Data.GetTreeInTerrain(instances[i]);
                    if (treeInTerrain.TerrainEntity.IsValid())
                    {
                        hash.Add(treeInTerrain.TerrainEntity, new TreeInTerrainIndexPair { Tree = treeInTerrain, Index = i });
                        count++;
                    }
                }
            }

            using var keys = hash.GetKeyArray(Allocator.TempJob);
            var keyCount = keys.Unique();
            var writeIndex = 0;
            result = new NativeArray<TreeInTerrain>(count, allocator, NativeArrayOptions.UninitializedMemory);
            indices = new NativeArray<int>(count, allocator, NativeArrayOptions.UninitializedMemory);

            for (int i = 0; i < keyCount; i++)
            {
                foreach (var pair in hash.GetValuesForKey(keys[i]))
                {
                    result[writeIndex] = pair.Tree;
                    indices[writeIndex] = pair.Index;
                    writeIndex++;
                }
            }
        }

        public void SetTerrainTreeIndices(EntityId terrainEntity, NativeArray<FloraInstanceHandle> instances, NativeArray<int> indices)
        {
            if (instances.Length != indices.Length)
                throw new ArgumentException("Instance and index arrays must have the same length.");

            SetTerrainTreeIndicesWithBurst(terrainEntity, instances.GetUnsafePtrT(), indices.GetUnsafePtrT(), instances.Length);
        }

        [BurstCompile]
        private static void SetTerrainTreeIndicesWithBurst(in EntityId terrainEntity, FloraInstanceHandle* instances, int* indices, int instanceCount)
        {
            if (instanceCount > 0)
            {
                for (var i = 0; i < instanceCount; i++)
                {
                    InstanceRegistry.Data.SetTreeInTerrain(instances[i],
                        new TreeInTerrain { TerrainEntity = terrainEntity, IndexInTreeInstances = indices[i] });
                }
            }
        }

        public void SetTerrainDetailLayerIndex(EntityId terrainEntity, NativeArray<FloraInstanceHandle> instances, int layerIndex)
        {
            for (var i = 0; i < instances.Length; i++)
            {
                if (Exists(instances[i]))
                {
                    InstanceRegistry.Data.SetDetailInTerrain(instances[i],
                        new DetailInTerrain { TerrainEntity = terrainEntity, LayerIndex = layerIndex });
                }
            }
        }
    }
}
