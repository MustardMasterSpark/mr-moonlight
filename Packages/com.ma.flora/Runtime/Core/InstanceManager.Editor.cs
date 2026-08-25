// Copyright © Magnetic Arcade. All Rights Reserved.

#if UNITY_EDITOR
using Unity.Burst;
using Unity.Collections;

namespace MA.Flora
{
    internal unsafe partial struct InstanceManager
    {
        #region SceneView Hidden

        internal void ClearHidden()
        {
            m_InstanceEditorHidden.FillZeroes();
            m_CullingGrid.ValueRW.ClearEditorHidden();
        }

        internal void SetHidden(FloraInstanceHandle instance)
        {
            SetHiddenWithBurst(Self, &instance, 1);
        }

        internal void SetHidden(NativeArray<FloraInstanceHandle> instances)
        {
            if (instances.Length > 0)
            {
                SetHiddenWithBurst(Self, instances.GetUnsafePtrT(), instances.Length);
            }
        }

        [BurstCompile]
        private static void SetHiddenWithBurst(InstanceManager* data, FloraInstanceHandle* instances, int count)
        {
            data->SetHiddenInternal(instances, count);
        }

        private void SetHiddenInternal(FloraInstanceHandle* instances, int count)
        {
            for (var i = 0; i < count; i++)
            {
                var instance = instances[i];
                if (Exists(instance))
                {
                    var instanceInChunk = InstanceRegistry.Data.GetInstanceInChunk(instance);
                    var instanceIndex = instanceInChunk.Chunk * ChunkCapacity + instanceInChunk.IndexInChunk;
                    m_InstanceEditorHidden.Set(instanceIndex, true);
                    m_CullingGrid.ValueRW.SetEditorHiddenFlag(instanceIndex);
                }
            }
        }

        #endregion

        #region SceneView Selected

        internal void ClearSelection()
        {
            m_InstanceEditorSelected.FillZeroes();
            m_CullingGrid.ValueRW.ClearEditorSelected();
        }

        internal void SetSelected(FloraInstanceHandle instance)
        {
            SetSelectedWithBurst(Self, &instance, 1);
        }

        internal void SetSelected(NativeArray<FloraInstanceHandle> instances)
        {
            if (instances.Length > 0)
            {
                SetSelectedWithBurst(Self, instances.GetUnsafePtrT(), instances.Length);
            }
        }

        [BurstCompile]
        private static void SetSelectedWithBurst(InstanceManager* data, FloraInstanceHandle* instances, int count)
        {
            data->SetSelectedInternal(instances, count);
        }

        private void SetSelectedInternal(FloraInstanceHandle* instances, int count)
        {
            for (var i = 0; i < count; i++)
            {
                var instance = instances[i];
                if (Exists(instance))
                {
                    var instanceInChunk = InstanceRegistry.Data.GetInstanceInChunk(instance);
                    var instanceIndex = instanceInChunk.Chunk * ChunkCapacity + instanceInChunk.IndexInChunk;
                    m_InstanceEditorSelected.Set(instanceIndex, true);
                    m_CullingGrid.ValueRW.SetEditorSelectedFlag(instanceIndex);
                }
            }
        }

        #endregion
    }
}
#endif
