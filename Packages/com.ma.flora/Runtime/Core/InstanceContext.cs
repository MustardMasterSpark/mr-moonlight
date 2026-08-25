// Copyright © Magnetic Arcade. All Rights Reserved.

using System;
using Unity.Collections;

namespace MA.Flora
{
    internal struct InstanceContext : IDisposable
    {
        private NativeData<InstanceManager> m_InstanceManager;
        private NativeData<CullingGrid> m_CullingGrid;
        private NativeData<DrawManager> m_DrawManager;
        private NativeData<TemplateManager> m_TemplateManager;
        private NativeData<InstanceBuffer> m_InstanceBuffer;
        private NativeData<StreamingSphereManager> m_StreamingManager;
        private NativeData<TerrainManager> m_TerrainManager;

        public bool IsCreated => m_InstanceManager.IsCreated;
        public NativeDataReference<InstanceManager> InstanceManager => m_InstanceManager;
        public NativeDataReference<CullingGrid> CullingGrid => m_CullingGrid;
        public NativeDataReference<DrawManager> DrawManager => m_DrawManager;
        public NativeDataReference<TemplateManager> TemplateManager => m_TemplateManager;
        public NativeDataReference<InstanceBuffer> InstanceBuffer => m_InstanceBuffer;
        public NativeDataReference<StreamingSphereManager> StreamingManager => m_StreamingManager;
        public NativeDataReference<TerrainManager> TerrainManager => m_TerrainManager;

        public InstanceContext(FloraRuntimeResources resources)
        {
            m_InstanceManager = new NativeData<InstanceManager>(Allocator.Persistent);
            m_CullingGrid = new NativeData<CullingGrid>(Allocator.Persistent);
            m_TemplateManager = new NativeData<TemplateManager>(Allocator.Persistent);
            m_DrawManager = new NativeData<DrawManager>(Allocator.Persistent);
            m_InstanceBuffer = new NativeData<InstanceBuffer>(Allocator.Persistent);
            m_StreamingManager = new NativeData<StreamingSphereManager>(Allocator.Persistent);
            m_TerrainManager = new NativeData<TerrainManager>(Allocator.Persistent);

            m_InstanceManager.ValueRW.Initialize(this);
            m_CullingGrid.ValueRW.Initialize(this, resources);
            m_TemplateManager.ValueRW.Initialize(this);
            m_DrawManager.ValueRW.Initialize();
            m_InstanceBuffer.ValueRW.Initialize(this, resources);
            m_StreamingManager.ValueRW.Initialize();
            m_TerrainManager.ValueRW.Initialize(this);
        }

        public void Dispose()
        {
            m_TerrainManager.Dispose();
            m_StreamingManager.Dispose();
            m_DrawManager.Dispose();
            m_TemplateManager.Dispose();
            m_InstanceManager.Dispose();
            m_CullingGrid.Dispose();
            m_InstanceBuffer.Dispose();
        }
    }
}
