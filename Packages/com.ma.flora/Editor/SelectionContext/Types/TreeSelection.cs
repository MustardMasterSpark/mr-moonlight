// Copyright © Magnetic Arcade. All Rights Reserved.
using System;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;

namespace MA.Flora.Editor
{
    //---------------------------------------------------------------------
    // Helpers – ID <-> Colour conversion
    //---------------------------------------------------------------------

    internal static class TreeIdUtil
    {
        public static Color32 Encode(uint id)
        {
            return new Color32((byte)(id & 0xFF), (byte)((id >> 8) & 0xFF), (byte)((id >> 16) & 0xFF), 0xFE);
        }

        public static uint Decode(Color32 c)
        {
            return c.a == 0xFE ? (uint)(c.r | (c.g << 8) | (c.b << 16)) : 0;
        }
    }

    //---------------------------------------------------------------------
    // Serializable mirror of TreeInstance
    //---------------------------------------------------------------------

    [Serializable]
    internal struct SerializableTreeInstance : IEquatable<SerializableTreeInstance>
    {
        public Vector3   Position;
        public float     WidthScale;
        public float     HeightScale;
        public float     Rotation;         // radians
        public Color32   Color;
        public Color32   LightmapColor;
        public int       PrototypeIndex;

        public static implicit operator TreeInstance(SerializableTreeInstance s)
        {
            return UnsafeUtility.As<SerializableTreeInstance, TreeInstance>(ref s);
        }

        public static implicit operator SerializableTreeInstance(TreeInstance t)
        {
            return UnsafeUtility.As<TreeInstance, SerializableTreeInstance>(ref t);
        }

        public bool Equals(SerializableTreeInstance other)
        {
            return Position == other.Position &&
                   WidthScale == other.WidthScale &&
                   HeightScale == other.HeightScale &&
                   Rotation == other.Rotation &&
                   Color.Equals(other.Color) &&
                   LightmapColor.Equals(other.LightmapColor) &&
                   PrototypeIndex == other.PrototypeIndex;
        }

        public override bool Equals(object obj)
        {
            return obj is SerializableTreeInstance other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 23 + Position.GetHashCode();
                hash = hash * 23 + WidthScale.GetHashCode();
                hash = hash * 23 + HeightScale.GetHashCode();
                hash = hash * 23 + Rotation.GetHashCode();
                hash = hash * 23 + Color.GetHashCode();
                hash = hash * 23 + LightmapColor.GetHashCode();
                hash = hash * 23 + PrototypeIndex.GetHashCode();
                return hash;
            }
        }
    }

    //---------------------------------------------------------------------
    // Tracked tree (selection element)
    //---------------------------------------------------------------------

    [Serializable]
    internal struct SelectionTree
    {
        public uint TreeId;
        public int OriginalTreeIndex;
        public int CurrentTreeIndex;
        public SerializableTreeInstance Original;
        public SerializableTreeInstance Current;
        public bool IsDirty => !Original.Equals(Current);
    }

    //---------------------------------------------------------------------
    // FloraTerrainSelection – with stable ID tracking
    //---------------------------------------------------------------------

    [PreferBinarySerialization]
    internal class TreeSelection : InstanceSelectionGroup
    {
        [SerializeField] private Terrain m_Terrain;
        [SerializeField] private SelectionTree[] m_SelectionTrees = Array.Empty<SelectionTree>();

        [NonSerialized] private NativeParallelHashMap<uint, int> m_TreeIdToTreeIndex = new(0, Allocator.Persistent);
        [NonSerialized] private Dictionary<uint, int> m_TreeIdToSelection = new();
        [NonSerialized] private Dictionary<int, int> m_OriginalTreeIndexToSelection = new();
        [NonSerialized] private NativeList<TreeInstance> m_TreeInstanceBuffer = new(0, Allocator.Persistent);
        [NonSerialized] private bool m_HasTreeChanges;

        private static uint s_NextSelectionId = 1; // 0 reserved ("no id")

        //---------------------------------------------------------------------
        // Jobs
        //---------------------------------------------------------------------

        [BurstCompile]
        private struct RebuildTreeIdMap : IJobParallelFor
        {
            [ReadOnly] public NativeArray<TreeInstance> TreeInstances;
            [WriteOnly] public NativeParallelHashMap<uint, int>.ParallelWriter TreeIdMap;

            public void Execute(int index)
            {
                uint treeID = TreeIdUtil.Decode(TreeInstances[index].lightmapColor);
                if (treeID != 0)
                    TreeIdMap.TryAdd(treeID, index);
            }
        }

        //------------------------------------------------------------------
        // Lifecycle
        //------------------------------------------------------------------

        ~TreeSelection()
        {
            m_TreeIdToTreeIndex.Dispose();
            m_TreeInstanceBuffer.Dispose();
        }

        private void OnEnable()
        {
            m_TreeInstanceBuffer.Clear();
            m_TreeIdToTreeIndex.Clear();
        }

        public override void OnCreate(GameObject target, int[] selectedIndices)
        {
            m_Terrain = target.GetComponent<Terrain>();
            if (!m_Terrain)
                throw new InvalidOperationException("FloraTerrainSelection requires a Terrain target");

            m_OriginalTreeIndexToSelection.Clear();
            m_TreeIdToSelection.Clear();
            m_TreeIdToTreeIndex.Clear();
            m_SelectionTrees = new SelectionTree[selectedIndices.Length];

            var terrainData = m_Terrain.terrainData;

            for (var i = 0; i < selectedIndices.Length; i++)
            {
                var treeIndex = selectedIndices[i];
                var treeInstance = terrainData.GetTreeInstance(treeIndex);
                var selectionId = s_NextSelectionId++;
                var original = treeInstance;
                treeInstance.lightmapColor = TreeIdUtil.Encode(selectionId);

                m_OriginalTreeIndexToSelection.Add(treeIndex, i);
                m_TreeIdToSelection.Add(selectionId, i);
                m_SelectionTrees[i] = new SelectionTree
                {
                    TreeId = selectionId,
                    OriginalTreeIndex = treeIndex,
                    CurrentTreeIndex = treeIndex,
                    Original = original,
                    Current = treeInstance
                };

                terrainData.SetTreeInstance(treeIndex, treeInstance);
            }
        }

        private bool TryGetTreeIndex(uint selectionId, out int treeIndex)
        {
            treeIndex = -1;

            // Fast‑path: validate cached index
            if (m_TreeIdToTreeIndex.TryGetValue(selectionId, out int cached) &&
                cached >= 0 &&
                cached < m_Terrain.terrainData.treeInstanceCount)
            {
                var treeInstance = m_Terrain.terrainData.GetTreeInstance(cached);
                if (TreeIdUtil.Decode(treeInstance.lightmapColor) == selectionId)
                {
                    treeIndex = cached;
                    return true;
                }
            }

            // Slow‑path: rebuild the map
            m_Terrain.terrainData.GetTreeInstances(m_TreeInstanceBuffer);
            m_TreeIdToTreeIndex.Clear();
            m_TreeIdToTreeIndex.Capacity = m_TreeInstanceBuffer.Length;
            new RebuildTreeIdMap {
                TreeInstances = m_TreeInstanceBuffer.AsArray(),
                TreeIdMap = m_TreeIdToTreeIndex.AsParallelWriter()
            }.Schedule(m_TreeInstanceBuffer.Length, 128).Complete();

            // Rebuild the selection indices
            foreach (var kvp in m_TreeIdToTreeIndex)
            {
                // Use the key that belongs to this map entry
                if (m_TreeIdToSelection.TryGetValue(kvp.Key, out int selectionIndex))
                {
                    var selectionTree = m_SelectionTrees[selectionIndex];
                    selectionTree.CurrentTreeIndex = kvp.Value;
                    m_SelectionTrees[selectionIndex] = selectionTree;
                }
            }

            return m_TreeIdToTreeIndex.TryGetValue(selectionId, out treeIndex) && treeIndex >= 0 && treeIndex < m_TreeInstanceBuffer.Length;
        }

        //------------------------------------------------------------------
        // Overrides
        //------------------------------------------------------------------

        public override void RecordUndo(string name)
        {
            Undo.RegisterCompleteObjectUndo(this, name);
        }

        public override FloraInstanceHandle GetInstanceHandleAt(int selectionIndex)
        {
            if (TryGetTreeIndex(m_SelectionTrees[selectionIndex].TreeId, out int treeIndex))
                return FloraSystem.Instance.GetTreeInstanceHandle(m_Terrain, treeIndex);

            throw new ArgumentOutOfRangeException(nameof(selectionIndex), "Instance index not found in selection");
        }

        public override NativeArray<FloraInstanceHandle> GetSelection()
        {
            if (m_SelectionTrees.Length == 0)
                return new NativeArray<FloraInstanceHandle>(0, Allocator.Temp);

            var handles = new NativeList<FloraInstanceHandle>(m_SelectionTrees.Length, Allocator.Temp);
            foreach (var selectionTree in m_SelectionTrees)
            {
                if (TryGetTreeIndex(selectionTree.TreeId, out int treeIndex))
                    handles.Add(FloraSystem.Instance.GetTreeInstanceHandle(m_Terrain, treeIndex));
            }

            return handles.TransferOwnershipToNativeArray();
        }

        public override Bounds CalculateBounds()
        {
            using var handles = FloraSystem.Instance.GetTreeInstanceHandles(m_Terrain, Allocator.TempJob);
            using var selected = new NativeList<FloraInstanceHandle>(m_SelectionTrees.Length, Allocator.TempJob);

            foreach (var selectionTree in m_SelectionTrees)
            {
                if (selectionTree.CurrentTreeIndex < handles.Length)
                    selected.Add(handles[selectionTree.CurrentTreeIndex]);
            }

            return FloraSystem.Instance.CalculateInstanceBounds(selected.AsArray());
        }

        public int[] GetTreeIndices()
        {
            var indicesToRemove = new List<int>(m_SelectionTrees.Length);
            for (int i = 0; i < m_SelectionTrees.Length; i++)
            {
                if (TryGetTreeIndex(m_SelectionTrees[i].TreeId, out int treeIndex))
                    indicesToRemove.Add(treeIndex);
            }

            indicesToRemove.Sort();
            return indicesToRemove.ToArray();
        }

        public FloraInstanceHandle[] GetInstanceHandles()
        {
            var indices = GetTreeIndices();
            var handles = new FloraInstanceHandle[indices.Length];
            for (int i = 0; i < indices.Length; i++)
            {
                handles[i] = FloraSystem.Instance.GetTreeInstanceHandle(m_Terrain, indices[i]);
            }

            return handles;
        }

        public override void DeleteSelected()
        {
            if (m_SelectionTrees.Length == 0)
                return;

            var indicesToRemove = new List<int>(m_SelectionTrees.Length);
            foreach (var selectionTree in m_SelectionTrees)
            {
                if (TryGetTreeIndex(selectionTree.TreeId, out int treeIndex))
                    indicesToRemove.Add(treeIndex);
            }

            if (indicesToRemove.Count == 0)
                return;

            indicesToRemove.Sort();

            m_Terrain.terrainData.GetTreeInstances(m_TreeInstanceBuffer);
            for (int i = indicesToRemove.Count - 1; i >= 0; --i)
                m_TreeInstanceBuffer.RemoveAtSwapBack(indicesToRemove[i]);

            Undo.RegisterCompleteObjectUndo(m_Terrain.terrainData, "Delete selected trees");
            m_Terrain.terrainData.SetTreeInstances(m_TreeInstanceBuffer.AsArray(), snapToHeightmap: false);

            m_TreeInstanceBuffer.Clear();
            m_TreeInstanceBuffer.TrimExcess();
            m_TreeIdToTreeIndex.Clear();
            m_SelectionTrees = Array.Empty<SelectionTree>();
            m_SelectionIndices = Array.Empty<int>();
            m_HasTreeChanges = false;
        }

        public override void OnRelease()
        {
            if (m_HasTreeChanges)
            {
                m_HasTreeChanges = false;
                m_Terrain.terrainData.GetTreeInstances(m_TreeInstanceBuffer);

                foreach (var selectionTree in m_SelectionTrees)
                {
                    if (TryGetTreeIndex(selectionTree.TreeId, out int treeIndex))
                    {
                        var treeInstance = selectionTree.Current;
                        treeInstance.LightmapColor = selectionTree.Original.LightmapColor;
                        m_TreeInstanceBuffer[treeIndex] = treeInstance;
                    }
                }

                Undo.RegisterCompleteObjectUndo(m_Terrain.terrainData, "Apply tree edits");
                m_Terrain.terrainData.SetTreeInstances(m_TreeInstanceBuffer.AsArray(), snapToHeightmap: false);
            }
            else
            {
                foreach (var selectionTree in m_SelectionTrees)
                {
                    if (TryGetTreeIndex(selectionTree.TreeId, out int treeIndex))
                    {
                        var treeInstance = m_Terrain.terrainData.GetTreeInstance(treeIndex);
                        treeInstance.lightmapColor = selectionTree.Original.LightmapColor;
                        m_Terrain.terrainData.SetTreeInstance(treeIndex, treeInstance);
                    }
                }
            }
        }

        //------------------------------------------------------------------
        // Transform helpers
        //------------------------------------------------------------------

        private FloraInstanceTransform ToTransform(SerializableTreeInstance treeInstance)
        {
            var origin = (float3)m_Terrain.GetPosition();
            var size = (float3)m_Terrain.terrainData.size;

            return new FloraInstanceTransform
            {
                Position = origin + treeInstance.Position * size,
                Rotation = quaternion.RotateY(treeInstance.Rotation),
                Scale    = new float3(treeInstance.WidthScale, treeInstance.HeightScale, treeInstance.WidthScale)
            };
        }

        private SerializableTreeInstance FromTransform(FloraInstanceTransform transform, SerializableTreeInstance treeInstance)
        {
            var origin = (float3)m_Terrain.GetPosition();
            var size = (float3)m_Terrain.terrainData.size;

            var normalizedPosition = math.saturate((transform.Position - origin) / size);
            treeInstance.Position = normalizedPosition;
            treeInstance.Rotation = math.radians(((Quaternion)transform.Rotation).eulerAngles.y);
            treeInstance.WidthScale = transform.Scale.x;
            treeInstance.HeightScale = transform.Scale.y;
            return treeInstance;
        }

        public override FloraInstanceTransform GetInstanceTransform(int originalTreeIndex)
        {
            if (!m_OriginalTreeIndexToSelection.TryGetValue(originalTreeIndex, out int selectionIndex))
                return FloraInstanceTransform.Identity;

            if (selectionIndex < 0 || selectionIndex >= m_SelectionTrees.Length)
                return FloraInstanceTransform.Identity;

            return ToTransform(m_SelectionTrees[selectionIndex].Current);
        }

        public override void UpdateInstanceTransform(int originalTreeIndex, FloraInstanceTransform worldTransform)
        {
            if (!m_OriginalTreeIndexToSelection.TryGetValue(originalTreeIndex, out int selectionIndex))
                return;

            var selectionTree = m_SelectionTrees[selectionIndex];
            selectionTree.Current = FromTransform(worldTransform, selectionTree.Current);
            m_SelectionTrees[selectionIndex] = selectionTree;
            m_HasTreeChanges = true;

            var handle = FloraSystem.Instance.GetTreeInstanceHandle(m_Terrain, selectionTree.CurrentTreeIndex);
            FloraSystem.Instance.UpdateInstanceLocalToWorldMatrix(handle, worldTransform.ToMatrix());

            Undo.RegisterCompleteObjectUndo(this, "Move tree instance");
        }
    }
}
