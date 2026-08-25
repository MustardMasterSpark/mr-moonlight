// Copyright © Magnetic Arcade. All Rights Reserved.
// ReSharper disable InconsistentNaming

using Unity.Collections;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Rendering;

namespace MA.Flora
{
    internal static class CullingGridCompute
    {
        private static class Compute
        {
            public static ComputeShader CullingGridCS;
            public static int UpdateChunkInfosKernel;
            public static int UpdateChunkFlagsKernel;
            public static int UpdateIndirectPagesKernel;
            public static int UpdateChunkAttributesKernel;
        }

        private static class LocalNameID
        {
            // Instance Buffer
            public static readonly int _BatchCullingAddresses = Shader.PropertyToID("_BatchCullingAddresses");
            public static readonly int _ArchetypeData = Shader.PropertyToID("_ArchetypeData");
            public static readonly int _TemplateData = Shader.PropertyToID("_TemplateData");

            // Culling Grid
            public static readonly int _BlockData = Shader.PropertyToID("_BlockData");
            public static readonly int _CullingChunkInfos = Shader.PropertyToID("_CullingChunkInfos");
            public static readonly int _CullingChunkCells = Shader.PropertyToID("_CullingChunkCells");
            public static readonly int _CullingChunkBatches = Shader.PropertyToID("_CullingChunkBatches");
            public static readonly int _CullingChunkAttributes = Shader.PropertyToID("_CullingChunkAttributes");
            public static readonly int _CullingIndirectOffsets = Shader.PropertyToID("_CullingIndirectOffsets");

            // Update Chunk Info
            public static readonly int _ChunkPacketCount = Shader.PropertyToID("_ChunkPacketCount");
            public static readonly int _ChunkPackets = Shader.PropertyToID("_ChunkPackets");
            public static readonly int _CullingChunkCellsRW = Shader.PropertyToID("_CullingChunkCellsRW");
            public static readonly int _CullingChunkInfosRW = Shader.PropertyToID("_CullingChunkInfosRW");
            public static readonly int _CullingChunkBatchesRW = Shader.PropertyToID("_CullingChunkBatchesRW");

            // Update Chunk Flags
            public static readonly int _CullingChunkFlagChannelCount = Shader.PropertyToID("_CullingChunkFlagChannelCount");
            public static readonly int _ChunkFlagsUpdateCount = Shader.PropertyToID("_ChunkFlagsUpdateCount");
            public static readonly int _ChunkFlagIndices = Shader.PropertyToID("_ChunkFlagIndices");
            public static readonly int _ChunkFlagUpdates = Shader.PropertyToID("_ChunkFlagUpdates");
            public static readonly int _ChunkFlagsRW = Shader.PropertyToID("_ChunkFlagsRW");

            // Update Indirect Pages
            public static readonly int _IndirectPageUpdateCount = Shader.PropertyToID("_IndirectPageUpdateCount");
            public static readonly int _IndirectPageUpdates = Shader.PropertyToID("_IndirectPageUpdates");
            public static readonly int _IndirectOffsetUpdates = Shader.PropertyToID("_IndirectOffsetUpdates");
            public static readonly int _IndirectInstanceOffsetsRW = Shader.PropertyToID("_IndirectInstanceOffsetsRW");

            // Update Chunk Attributes
            public static readonly int _ChunkAttributeUpdateCount = Shader.PropertyToID("_ChunkAttributeUpdateCount");
            public static readonly int _AttributeCellChunkIndices = Shader.PropertyToID("_AttributeCellChunkIndices");
            public static readonly int _CullingChunkAttributesRW = Shader.PropertyToID("_CullingChunkAttributesRW");
        }

        private static class Profiling
        {
            public static readonly ProfilerMarker UpdateChunks = new ProfilerMarker("CullingGrid.UpdateChunks");
            public static readonly ProfilerMarker ScatterIndirectPages = new ProfilerMarker("CullingGrid.ScatterIndirectPages");
            public static readonly ProfilerMarker UpdateChunkAttributes = new ProfilerMarker("CullingGrid.UpdateChunkAttributes");
        }

        public static void Initialize(FloraRuntimeResources resources)
        {
            Compute.CullingGridCS = resources.CullingGridCS;
            Compute.UpdateChunkInfosKernel = Compute.CullingGridCS.FindKernel("UpdateChunkInfos");
            Compute.UpdateChunkFlagsKernel = Compute.CullingGridCS.FindKernel("UpdateChunkFlags");
            Compute.UpdateIndirectPagesKernel = Compute.CullingGridCS.FindKernel("UpdateIndirectPages");
            Compute.UpdateChunkAttributesKernel = Compute.CullingGridCS.FindKernel("UpdateChunkAttributes");
        }

        #region Update Chunk Info

        public struct UpdateChunkInfoParams
        {
            public int PacketCount;
            public GraphicsBuffer ChunkPacketBuffer;
            public GraphicsBuffer ChunkCellBuffer;
            public GraphicsBuffer ChunkInfoBuffer;
            public GraphicsBuffer ChunkBatchBuffer;
        }

        public static void DispatchUpdateChunkInfo(CommandBuffer cmd, in UpdateChunkInfoParams input)
        {
            using (new CommandBufferProfilerScope(cmd, Profiling.UpdateChunks))
            {
                var cs = Compute.CullingGridCS;
                int kernel = Compute.UpdateChunkInfosKernel;

                cmd.SetComputeIntParam(cs, LocalNameID._ChunkPacketCount, input.PacketCount);
                cmd.SetComputeBufferParam(cs, kernel, LocalNameID._ChunkPackets, input.ChunkPacketBuffer);
                cmd.SetComputeBufferParam(cs, kernel, LocalNameID._CullingChunkCellsRW, input.ChunkCellBuffer);
                cmd.SetComputeBufferParam(cs, kernel, LocalNameID._CullingChunkInfosRW, input.ChunkInfoBuffer);
                cmd.SetComputeBufferParam(cs, kernel, LocalNameID._CullingChunkBatchesRW, input.ChunkBatchBuffer);

                int3 threadGroups = ComputeUtility.WrapDispatchCount(input.PacketCount, 64);
                cmd.DispatchCompute(cs, kernel, threadGroups);
            }
        }

        #endregion

        #region Update Chunk Flags

        public struct UpdateChunkFlagsParams
        {
            public int UpdateCount;
            public int ChannelCount;
            public GraphicsBuffer ChunkFlagUpdateBuffer;
            public GraphicsBuffer ChunkFlagIndexBuffer;
            public GraphicsBuffer ChunkFlagBuffer;
        }

        public static void DispatchUpdateChunkFlags(CommandBuffer cmd, in UpdateChunkFlagsParams input)
        {
            using (new CommandBufferProfilerScope(cmd, Profiling.UpdateChunks))
            {
                var cs = Compute.CullingGridCS;
                int kernel = Compute.UpdateChunkFlagsKernel;

                cmd.SetComputeIntParam(cs, LocalNameID._CullingChunkFlagChannelCount, input.ChannelCount);
                cmd.SetComputeIntParam(cs, LocalNameID._ChunkFlagsUpdateCount, input.UpdateCount);
                cmd.SetComputeBufferParam(cs, kernel, LocalNameID._ChunkFlagUpdates, input.ChunkFlagUpdateBuffer);
                cmd.SetComputeBufferParam(cs, kernel, LocalNameID._ChunkFlagIndices, input.ChunkFlagIndexBuffer);
                cmd.SetComputeBufferParam(cs, kernel, LocalNameID._ChunkFlagsRW, input.ChunkFlagBuffer);

                int3 threadGroups = ComputeUtility.WrapDispatchCount(input.UpdateCount, 64);
                cmd.DispatchCompute(cs, kernel, threadGroups);
            }
        }

        #endregion

        #region Update Indirect Pages

        public struct UpdateIndirectPagesParams
        {
            public int IndirectPageUpdateCount;
            public GraphicsBuffer IndirectPageUpdateBuffer;
            public GraphicsBuffer IndirectOffsetUpdateBuffer;
            public GraphicsBuffer IndirectOffsetBuffer;
        }

        public static void DispatchScatterIndirectPages(CommandBuffer cmd, in UpdateIndirectPagesParams input)
        {
            using (new CommandBufferProfilerScope(cmd, Profiling.ScatterIndirectPages))
            {
                var cs = Compute.CullingGridCS;
                int kernel = Compute.UpdateIndirectPagesKernel;

                cmd.SetComputeIntParam(cs, LocalNameID._IndirectPageUpdateCount, input.IndirectPageUpdateCount);
                cmd.SetComputeBufferParam(cs, kernel, LocalNameID._IndirectPageUpdates, input.IndirectPageUpdateBuffer);
                cmd.SetComputeBufferParam(cs, kernel, LocalNameID._IndirectOffsetUpdates, input.IndirectOffsetUpdateBuffer);
                cmd.SetComputeBufferParam(cs, kernel, LocalNameID._IndirectInstanceOffsetsRW, input.IndirectOffsetBuffer);

                int3 threadGroups = ComputeUtility.WrapGroupCount(input.IndirectPageUpdateCount);
                cmd.DispatchCompute(cs, kernel, threadGroups);
            }
        }

        #endregion

        #region Update Chunk Attributes

        public struct UpdateChunkAttributesParams
        {
            public int AttributeUpdateCount;
            public NativeArray<int2> CellChunkIndices;

            public GraphicsBuffer InstanceBuffer;
            public GraphicsBuffer BatchDomainAddressBuffer;
            public GraphicsBuffer ArchetypeDataBuffer;
            public GraphicsBuffer TemplateDataBuffer;

            public GraphicsBuffer BlockDataBuffer;
            public GraphicsBuffer ChunkBatchBuffer;
            public GraphicsBuffer ChunkInfoBuffer;
            public GraphicsBuffer ChunkAttributeBuffer;
            public GraphicsBuffer IndirectOffsetBuffer;
        }

        public static void DispatchUpdateChunkAttributes(CommandBuffer cmd, in UpdateChunkAttributesParams input)
        {
            using (new CommandBufferProfilerScope(cmd, Profiling.UpdateChunkAttributes))
            {
                var cellChunkIndexBuffer = GraphicsBufferStore.RequestStructured(cmd, input.CellChunkIndices);
                var cs = Compute.CullingGridCS;
                int kernel = Compute.UpdateChunkAttributesKernel;

                cmd.SetComputeBufferParam(cs, kernel, ShaderPropertyId.unity_DOTSInstanceData, input.InstanceBuffer);
                cmd.SetComputeBufferParam(cs, kernel, LocalNameID._BatchCullingAddresses, input.BatchDomainAddressBuffer);
                cmd.SetComputeBufferParam(cs, kernel, LocalNameID._ArchetypeData, input.ArchetypeDataBuffer);
                cmd.SetComputeBufferParam(cs, kernel, LocalNameID._TemplateData, input.TemplateDataBuffer);

                cmd.SetComputeBufferParam(cs, kernel, LocalNameID._BlockData, input.BlockDataBuffer);
                cmd.SetComputeBufferParam(cs, kernel, LocalNameID._CullingChunkInfos, input.ChunkInfoBuffer);
                cmd.SetComputeBufferParam(cs, kernel, LocalNameID._CullingChunkBatches, input.ChunkBatchBuffer);
                cmd.SetComputeBufferParam(cs, kernel, LocalNameID._CullingIndirectOffsets, input.IndirectOffsetBuffer);

                cmd.SetComputeIntParam(cs, LocalNameID._ChunkAttributeUpdateCount, input.AttributeUpdateCount);
                cmd.SetComputeBufferParam(cs, kernel, LocalNameID._AttributeCellChunkIndices, cellChunkIndexBuffer);
                cmd.SetComputeBufferParam(cs, kernel, LocalNameID._CullingChunkAttributesRW, input.ChunkAttributeBuffer);

                int3 threadGroups = ComputeUtility.WrapGroupCount(input.AttributeUpdateCount);
                cmd.DispatchCompute(cs, kernel, threadGroups);
            }
        }

        #endregion
    }
}
