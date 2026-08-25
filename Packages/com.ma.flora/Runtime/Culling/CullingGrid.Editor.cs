// Copyright © Magnetic Arcade. All Rights Reserved.

using System;
using Unity.Collections;

namespace MA.Flora
{
    internal partial struct CullingGrid
    {
        #region Editor Flags

#if UNITY_EDITOR
        public void ClearEditorSelected()
        {
            m_ChunkFlagsDirty.UnionWith(m_ChunkAllocated);
            UpdateContentVersion();
        }

        public void SetEditorSelectedFlag(int instanceIndex)
        {
            NativeArray<InstanceInCullingChunk> instanceInCullingChunks = m_InstanceManager.ValueRW.InstanceInCullingChunks;
            InstanceInCullingChunk instanceInCullingChunk = instanceInCullingChunks[instanceIndex];
            if (instanceInCullingChunk.Chunk == CullingChunkIndex.None) return;
            SetChunkFlagsDirty(instanceInCullingChunk.Chunk);
        }

        public void ClearEditorHidden()
        {
            m_ChunkFlagsDirty.UnionWith(m_ChunkAllocated);
            UpdateContentVersion();
        }

        public void SetEditorHiddenFlag(int instanceIndex)
        {
            NativeArray<InstanceInCullingChunk> instanceInCullingChunks = m_InstanceManager.ValueRW.InstanceInCullingChunks;
            InstanceInCullingChunk instanceInCullingChunk = instanceInCullingChunks[instanceIndex];
            if (instanceInCullingChunk.Chunk == CullingChunkIndex.None) return;
            SetChunkFlagsDirty(instanceInCullingChunk.Chunk);
        }
#endif

        #endregion
    }
}
