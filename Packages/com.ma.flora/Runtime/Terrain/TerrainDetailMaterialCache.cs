using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace MA.Flora
{
    internal class TerrainDetailMaterialCache : IDisposable
    {
        private struct TerrainLayerBinding : IEquatable<TerrainLayerBinding>
        {
            public EntityId TerrainId;
            public int LayerIndex;

            public TerrainLayerBinding(EntityId terrainId, int layerIndex)
            {
                TerrainId = terrainId;
                LayerIndex = layerIndex;
            }

            public bool Equals(TerrainLayerBinding other) => TerrainId == other.TerrainId && LayerIndex == other.LayerIndex;
            public override bool Equals(object obj) => obj is TerrainLayerBinding other && Equals(other);
            public override int GetHashCode()
            {
                unchecked { return (TerrainId.GetHashCode() * 397) ^ LayerIndex; }
            }
        }

        private struct MaterialKey : IEquatable<MaterialKey>
        {
            public EntityId TextureId;
            public Color32 HealthyColor;
            public Color32 DryColor;
            public half2 MinMaxSize;
            public bool Billboard;

            public bool Equals(MaterialKey other)
            {
                return TextureId.Equals(other.TextureId) &&
                       HealthyColor.Equals(other.HealthyColor) &&
                       DryColor.Equals(other.DryColor) &&
                       MinMaxSize.Equals(other.MinMaxSize) &&
                       Billboard == other.Billboard;
            }

            public override bool Equals(object obj) => obj is MaterialKey other && Equals(other);
            public override int GetHashCode()
            {
                unchecked
                {
                    int hashCode = TextureId.GetHashCode();
                    hashCode = (hashCode * 397) ^ HealthyColor.GetHashCode();
                    hashCode = (hashCode * 397) ^ DryColor.GetHashCode();
                    hashCode = (hashCode * 397) ^ MinMaxSize.GetHashCode();
                    hashCode = (hashCode * 397) ^ Billboard.GetHashCode();
                    return hashCode;
                }
            }
        }

        private Dictionary<MaterialKey, int> m_GrassMaterialIndexLookup;
        private Dictionary<TerrainLayerBinding, int> m_GrassBindings;
        private Dictionary<EntityId, int> m_TerrainLayerCounts;
        private int m_NextGrassMaterialIndex = 0;
        private List<int> m_FreeGrassMaterialIndices;
        private MaterialKey[] m_GrassKeysByIndex;
        private int[] m_GrassRefCounts;
        private Material[] m_GrassMaterials;

        private GameObject m_GrassPlaceholderPrefab;
        private Material m_GrassMaterial;

        private List<TerrainLayerBinding> m_BindingsToRemoveBuffer = new List<TerrainLayerBinding>();
        private List<TerrainLayerBinding> m_BindingsToRemove = new List<TerrainLayerBinding>();
        private HashSet<int> m_PossibleFreeIndices = new HashSet<int>();

        private static readonly int HealthyColor = Shader.PropertyToID("_HealthyColor");
        private static readonly int DryColor = Shader.PropertyToID("_DryColor");
        private static readonly int MinMaxSize = Shader.PropertyToID("_MinMaxSize");

        public TerrainDetailMaterialCache(FloraRuntimeResources resources)
        {
            m_GrassMaterialIndexLookup = new Dictionary<MaterialKey, int>();
            m_GrassBindings = new Dictionary<TerrainLayerBinding, int>();
            m_TerrainLayerCounts = new Dictionary<EntityId, int>();
            m_NextGrassMaterialIndex = 0;
            m_FreeGrassMaterialIndices = new List<int>();
            m_GrassKeysByIndex = new MaterialKey[16];
            m_GrassRefCounts = new int[16];
            m_GrassMaterials = new Material[16];

            m_GrassPlaceholderPrefab = resources.TerrainGrassPlaceholderPrefab;
            m_GrassMaterial = resources.TerrainGrassMaterial;
        }

        public void Dispose()
        {
            foreach (Material mat in m_GrassMaterials)
                CoreUtils.Destroy(mat);
        }

        public GameObject GetTerrainGrassPlaceholderPrefab()
        {
            return m_GrassPlaceholderPrefab;
        }

        public void FreeUnusedMaterials()
        {
            if (m_PossibleFreeIndices.Count == 0)
                return;

            foreach (int index in m_PossibleFreeIndices)
            {
                if (m_GrassRefCounts[index] == 0)
                    DestroyMaterial(index);
            }

            m_PossibleFreeIndices.Clear();
        }

        public void OnTerrainRemoved(EntityId terrainId)
        {
            m_BindingsToRemove.Clear();

            foreach (TerrainLayerBinding binding in m_GrassBindings.Keys)
            {
                if (binding.TerrainId == terrainId)
                    m_BindingsToRemove.Add(binding);
            }

            foreach (TerrainLayerBinding binding in m_BindingsToRemove)
            {
                if (m_GrassBindings.Remove(binding, out int index))
                    DecrementMaterialRef(index);
            }
        }

        public void OnTerrainChanged(EntityId terrainId)
        {
            Terrain terrain = terrainId.ToObject<Terrain>();
            if (terrain == null)
                return;

            int layerCount = terrain.terrainData?.detailPrototypes?.Length ?? 0;

            if (m_TerrainLayerCounts.TryGetValue(terrainId, out int oldLayerCount))
            {
                if (layerCount < oldLayerCount)
                {
                    m_BindingsToRemove.Clear();
                    for (int layerIndex = layerCount; layerIndex < oldLayerCount; layerIndex++)
                    {
                        TerrainLayerBinding binding = new TerrainLayerBinding(terrainId, layerIndex);
                        if (m_GrassBindings.ContainsKey(binding))
                            m_BindingsToRemove.Add(binding);
                    }

                    foreach (TerrainLayerBinding binding in m_BindingsToRemove)
                    {
                        if (m_GrassBindings.Remove(binding, out int index))
                            DecrementMaterialRef(index);
                    }
                }
            }

            m_TerrainLayerCounts[terrainId] = layerCount;
        }

        public Material GetOrCreateMaterial(in TerrainDetailPrototype prototype)
        {
            Terrain terrain = prototype.Terrain;
            Texture texture = prototype.PrototypeTexture;

            Color healthyColor = prototype.HealthyColor;
            Color dryColor = prototype.DryColor;
            bool isBillboard = prototype.RenderMode == DetailRenderMode.GrassBillboard;

            MaterialKey key = new MaterialKey
            {
                TextureId    = prototype.PrototypeTexture,
                HealthyColor = healthyColor,
                DryColor     = dryColor,
                MinMaxSize   = new half2(new half(math.min(prototype.MinWidth, prototype.MinHeight)), new half(math.min(prototype.MaxWidth, prototype.MaxHeight))),
                Billboard    = isBillboard
            };

            Material grassMaterial;
            if (!m_GrassMaterialIndexLookup.TryGetValue(key, out int materialIndex))
            {
                materialIndex = AllocateMaterialIndex();
                grassMaterial = new Material(m_GrassMaterial) { hideFlags = HideFlags.HideAndDontSave };
#if UNITY_EDITOR
                grassMaterial.parent = m_GrassMaterial;
#endif
                m_GrassMaterials[materialIndex]  = grassMaterial;
                m_GrassMaterialIndexLookup[key] = materialIndex;
                m_GrassKeysByIndex[materialIndex] = key;
                m_GrassRefCounts[materialIndex] = 0;
            }
            else
            {
                grassMaterial = m_GrassMaterials[materialIndex];
            }

            TerrainLayerBinding binding = new TerrainLayerBinding(terrain.GetEntityId(), prototype.Index);
            if (!m_GrassBindings.TryGetValue(binding, out int oldIndex))
            {
                m_GrassBindings[binding] = materialIndex;
                IncrementMaterialRef(materialIndex);
            }
            else if (oldIndex != materialIndex)
            {
                m_GrassBindings[binding] = materialIndex;
                IncrementMaterialRef(materialIndex);
                DecrementMaterialRef(oldIndex);
            }

            grassMaterial.mainTexture = texture;
            grassMaterial.SetColor(HealthyColor, healthyColor);
            grassMaterial.SetColor(DryColor, dryColor);
            grassMaterial.SetVector(MinMaxSize, new Vector4(key.MinMaxSize.x, key.MinMaxSize.y, 0, 0));
            if (isBillboard) grassMaterial.EnableKeyword("BILLBOARD");
            else             grassMaterial.DisableKeyword("BILLBOARD");

            return grassMaterial;
        }

        private int AllocateMaterialIndex()
        {
            int index;
            if (m_FreeGrassMaterialIndices.Count > 0)
            {
                index = m_FreeGrassMaterialIndices[^1];
                m_FreeGrassMaterialIndices.RemoveAt(m_FreeGrassMaterialIndices.Count - 1);
            }
            else
            {
                index = m_NextGrassMaterialIndex++;
                if (index >= m_GrassMaterials.Length)
                {
                    int newSize = Mathf.NextPowerOfTwo(index + 1);
                    Array.Resize(ref m_GrassMaterials, newSize);
                    Array.Resize(ref m_GrassKeysByIndex, newSize);
                    Array.Resize(ref m_GrassRefCounts, newSize);
                }
            }

            return index;
        }

        private void IncrementMaterialRef(int index)
        {
            m_GrassRefCounts[index]++;
        }

        private void DecrementMaterialRef(int index)
        {
            m_PossibleFreeIndices.Add(index);
            m_GrassRefCounts[index] = math.max(0, m_GrassRefCounts[index] - 1);
        }

        private void DestroyMaterial(int index)
        {
            m_GrassMaterialIndexLookup.Remove(m_GrassKeysByIndex[index]);
            CoreUtils.Destroy(m_GrassMaterials[index]);
            m_GrassMaterials[index] = null;
            m_GrassKeysByIndex[index] = default;
            m_GrassRefCounts[index] = 0;
            m_FreeGrassMaterialIndices.Add(index);
        }
    }
}
