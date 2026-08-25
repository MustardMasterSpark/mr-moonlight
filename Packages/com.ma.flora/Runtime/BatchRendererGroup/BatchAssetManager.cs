using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;

namespace MA.Flora
{
    internal static class BatchAssetManager
    {
        private static class Storage
        {
            public static BatchRendererGroup BatchRendererGroup;
            public static bool IsInitialized => BatchRendererGroup != null;

            public static Dictionary<EntityId, BatchMaterialID> BatchMaterialLookup = new Dictionary<EntityId, BatchMaterialID>();
            public static Dictionary<EntityId, BatchMeshID> BatchMeshLookup = new Dictionary<EntityId, BatchMeshID>();
            public static Dictionary<BatchMeshID, EntityId> MeshEntityIdLookup = new Dictionary<BatchMeshID, EntityId>();

            public static TerrainDetailMaterialCache GrassMaterialCache;
        }

        #region Setup / Teardown

        public static void Initialize(BatchRendererGroup batchRendererGroup, FloraRuntimeResources resources)
        {
            Storage.BatchRendererGroup = batchRendererGroup;
            Storage.GrassMaterialCache = new TerrainDetailMaterialCache(resources);
        }

        public static void Shutdown()
        {
            Storage.BatchRendererGroup = null;
            Storage.BatchMaterialLookup.Clear();
            Storage.BatchMeshLookup.Clear();
            Storage.MeshEntityIdLookup.Clear();
            Storage.GrassMaterialCache.Dispose();
        }

        public static BatchRendererGroup GetBatchRendererGroup()
        {
            return Storage.BatchRendererGroup;
        }

        #endregion

        #region Terrain Grass

        public static GameObject GetTerrainGrassPlaceholderPrefab()
        {
            return Storage.GrassMaterialCache.GetTerrainGrassPlaceholderPrefab();
        }

        public static void NextFrame()
        {
            Storage.GrassMaterialCache.FreeUnusedMaterials();
        }

        public static void OnTerrainRemoved(EntityId terrainId)
        {
            Storage.GrassMaterialCache.OnTerrainRemoved(terrainId);
        }

        public static void OnTerrainChanged(EntityId terrainId)
        {
            Storage.GrassMaterialCache.OnTerrainChanged(terrainId);
        }

        public static Material GetOrCreateTerrainGrassMaterial(in TerrainDetailPrototype prototype)
        {
            return Storage.GrassMaterialCache.GetOrCreateMaterial(prototype);
        }

        #endregion

        #region Materials

        public static BatchMaterialID RegisterMaterial(Material material)
        {
            if (material == null || !Storage.IsInitialized)
                return BatchMaterialID.Null;

            var batchMaterialID = Storage.BatchRendererGroup.RegisterMaterial(material);
            if (batchMaterialID == BatchMaterialID.Null)
                return BatchMaterialID.Null;

            Storage.BatchMaterialLookup[material.GetEntityId()] = batchMaterialID;
            return batchMaterialID;
        }

        public static void UnregisterMaterial(BatchMaterialID batchMaterialID)
        {
            if (!Storage.IsInitialized || batchMaterialID == BatchMaterialID.Null)
                return;

            Storage.BatchRendererGroup.UnregisterMaterial(batchMaterialID);
            foreach (var kvp in Storage.BatchMaterialLookup)
            {
                if (kvp.Value == batchMaterialID)
                {
                    Storage.BatchMaterialLookup.Remove(kvp.Key);
                    break;
                }
            }
        }

        public static void MaterialsDestroyed(NativeArray<EntityId> destroyedMaterials)
        {
            if (!Storage.IsInitialized || destroyedMaterials.Length == 0)
                return;

            foreach (EntityId materialId in destroyedMaterials)
            {
                if (Storage.BatchMaterialLookup.Remove(materialId, out var batchMaterialID))
                {
                    Storage.BatchRendererGroup.UnregisterMaterial(batchMaterialID);
                }
            }
        }

        #endregion

        #region Meshes

        public static BatchMeshID RegisterMesh(Mesh mesh)
        {
            if (mesh == null || !Storage.IsInitialized)
                return BatchMeshID.Null;

            var meshID = Storage.BatchRendererGroup.RegisterMesh(mesh);
            if (meshID == BatchMeshID.Null)
                return BatchMeshID.Null;

            Storage.BatchMeshLookup[mesh.GetEntityId()] = meshID;
            Storage.MeshEntityIdLookup[meshID] = mesh.GetEntityId();
            return meshID;
        }

        public static void UnregisterMesh(BatchMeshID batchMesh)
        {
            if (!Storage.IsInitialized || batchMesh == BatchMeshID.Null)
                return;

            if (Storage.MeshEntityIdLookup.TryGetValue(batchMesh, out var meshInstanceID))
            {
                Storage.BatchMeshLookup.Remove(meshInstanceID);
                Storage.MeshEntityIdLookup.Remove(batchMesh);
                Storage.BatchRendererGroup.UnregisterMesh(batchMesh);
            }
        }

        public static Mesh GetRegisteredMesh(BatchMeshID batchMesh)
        {
            if (!Storage.IsInitialized || batchMesh == BatchMeshID.Null)
                return null;

            return Storage.BatchRendererGroup.GetRegisteredMesh(batchMesh);
        }

        public static void MeshesDestroyed(NativeArray<EntityId> destroyedMeshes)
        {
            if (!Storage.IsInitialized || destroyedMeshes.Length == 0)
                return;

            foreach (EntityId meshId in destroyedMeshes)
            {
                if (Storage.BatchMeshLookup.Remove(meshId, out var batchMeshID))
                {
                    Storage.MeshEntityIdLookup.Remove(batchMeshID);
                    Storage.BatchRendererGroup.UnregisterMesh(batchMeshID);
                }
            }
        }

        #endregion
    }
}
