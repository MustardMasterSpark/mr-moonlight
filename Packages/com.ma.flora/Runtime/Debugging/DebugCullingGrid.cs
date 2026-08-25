// Copyright © Magnetic Arcade. All Rights Reserved.
// ReSharper disable InconsistentNaming

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace MA.Flora
{
    [GenerateHLSL(PackingRules.Exact, false)]
    [StructLayout(LayoutKind.Sequential)]
    internal struct DebugLineVertex
    {
        public Vector3 position;
        public float weight;
        public Vector4 color;
    }

    [GenerateHLSL(needAccessors = false, generateCBuffer = true)]
    [StructLayout(LayoutKind.Sequential)]
    internal unsafe struct DebugCullingGridShaderVariables
    {
        [HLSLArray(6, typeof(Vector4))]
        public fixed float _FrustumPlanes[6 * 4];
        public Vector4 _CameraPositionAndDist; // x, y, z: position, w: MaxDistance
        public Vector4 _CullingSettings;       // x: VisualizationMode, y: MinLevel, z: MaxLevel, w: MaxAlpha
    }

    internal unsafe class DebugCullingGrid : IDisposable
    {
        private NativeDataReference<CullingGrid> m_CullingGrid;

        private FloraRuntimeResources m_Resources;
        private ComputeShader m_DebugCullingGridCS;
        private int m_BuildBlockLinesKernel;
        private int m_BuildCellLinesKernel;
        private int m_BuildChunkLinesKernel;
        private int m_BuildDrawArgsKernel;
        private int m_FrameIndex;

        private enum DrawType
        {
            Blocks,
            Cells,
            Chunks
        }

        private struct ContextKey : IEquatable<ContextKey>
        {
            public DrawType Type;
            public EntityId CameraId;

            public bool Equals(ContextKey other) => Type == other.Type && CameraId.Equals(other.CameraId);
            public override bool Equals(object obj) => obj is ContextKey other && Equals(other);
            public override int GetHashCode() => unchecked(((int)Type * 397) ^ CameraId.GetHashCode());
        }

        private class ContextHandle : IDisposable
        {
            public DrawType Type;
            public EntityId CameraId;
            public int LastUsedFrame;

            public GraphicsBuffer DebugShaderVariablesBuffer;
            public NativeArray<DebugCullingGridShaderVariables> DebugShaderVariablesData;

            public Material LineMaterial;
            public GraphicsBuffer LineVertexBuffer;
            public GraphicsBuffer LineCounterBuffer;
            public GraphicsBuffer LineDrawArgsBuffer;

            public ContextHandle(FloraRuntimeResources resources)
            {
                LineMaterial = new Material(resources.DebugLineShader);
            }

            public void Dispose()
            {
                CoreUtils.Destroy(LineMaterial);

                DebugShaderVariablesBuffer?.Dispose();
                DebugShaderVariablesData.Dispose();

                LineVertexBuffer?.Dispose();
                LineCounterBuffer?.Dispose();
                LineDrawArgsBuffer?.Dispose();
            }
        }

        private Dictionary<ContextKey, ContextHandle> m_Contexts = new Dictionary<ContextKey, ContextHandle>();

        private static class LocalNameID
        {
            public static readonly int DebugCullingGridShaderVariables = Shader.PropertyToID("DebugCullingGridShaderVariables");

            public static readonly int _BlockCount   = Shader.PropertyToID("_BlockCount");
            public static readonly int _BlockData    = Shader.PropertyToID("_BlockData");
            public static readonly int _BlockIndices = Shader.PropertyToID("_BlockIndices");

            public static readonly int _CellCount          = Shader.PropertyToID("_CellCount");
            public static readonly int _CellIndices        = Shader.PropertyToID("_CellIndices");
            public static readonly int _CellInstanceCounts = Shader.PropertyToID("_CellInstanceCounts");

            public static readonly int _ChunkCount             = Shader.PropertyToID("_ChunkCount");
            public static readonly int _ChunkIndices           = Shader.PropertyToID("_ChunkIndices");
            public static readonly int _CullingChunkBatches          = Shader.PropertyToID("_CullingChunkBatches");
            public static readonly int _CullingChunkCells      = Shader.PropertyToID("_CullingChunkCells");
            public static readonly int _CullingChunkAttributes = Shader.PropertyToID("_CullingChunkAttributes");

            public static readonly int _LineVertices = Shader.PropertyToID("_LineVertices");
            public static readonly int _LineVertexCounter = Shader.PropertyToID("_LineVertexCounter");
            public static readonly int _LineDrawArgs = Shader.PropertyToID("_LineDrawArgs");
        }

        private const int VerticesPerBox = 24;
        private const int FramesUntilRelease = 300;

        public DebugCullingGrid(InstanceContext context, FloraRuntimeResources resources)
        {
            m_CullingGrid = context.CullingGrid;
            m_Resources = resources;
            m_DebugCullingGridCS = resources.DebugCullingGridCS;
            m_BuildBlockLinesKernel = m_DebugCullingGridCS.FindKernel("BuildBlockLines");
            m_BuildCellLinesKernel = m_DebugCullingGridCS.FindKernel("BuildCellLines");
            m_BuildChunkLinesKernel = m_DebugCullingGridCS.FindKernel("BuildChunkLines");
            m_BuildDrawArgsKernel = m_DebugCullingGridCS.FindKernel("BuildDrawArgs");
        }

        public void Dispose()
        {
            foreach (var kvp in m_Contexts)
                kvp.Value.Dispose();
        }

        private ContextHandle GetOrCreateContext(DrawType type, Camera camera)
        {
            var key = new ContextKey { Type = type, CameraId = camera.GetEntityId() };
            if (!m_Contexts.TryGetValue(key, out var ctx))
            {
                ctx = new ContextHandle(m_Resources);
                ctx.Type = key.Type;
                ctx.CameraId = key.CameraId;
                m_Contexts.Add(key, ctx);
            }

            ctx.LastUsedFrame = m_FrameIndex;
            return ctx;
        }

        public void NextFrame()
        {
            using (ListPool<ContextKey>.Get(out var unusedContexts))
            {
                foreach (var kvp in m_Contexts)
                {
                    if (m_FrameIndex - kvp.Value.LastUsedFrame > FramesUntilRelease)
                        unusedContexts.Add(kvp.Key);
                }

                foreach (var key in unusedContexts)
                {
                    m_Contexts[key].Dispose();
                    m_Contexts.Remove(key);
                }
            }

            m_FrameIndex++;
        }

        public void UpdateDisplay(Camera camera)
        {
            if (!DebugDisplayFlora.Active)
                return;

            FloraDebugDisplayProperties debugSettings = DebugDisplayFlora.Properties;
            if (!debugSettings.RenderSpatialHash)
                return;

            bool drawBlocks = (debugSettings.SpatialHashFlags & DebugSpatialHashFlags.Blocks) != 0;
            if (drawBlocks)
                DrawBlocks(camera, in debugSettings);

            bool drawCells = (debugSettings.SpatialHashFlags & DebugSpatialHashFlags.Cells) != 0;
            if (drawCells)
                DrawCells(camera, in debugSettings);

            bool drawChunks = (debugSettings.SpatialHashFlags & DebugSpatialHashFlags.Chunks) != 0;
            if (drawChunks)
                DrawChunks(camera, in debugSettings);
        }

        private CommandBuffer BeginContextDraw(Camera camera, in ContextHandle ctx, in FloraDebugDisplayProperties debugSettings, int boxCount)
        {
            CommandBuffer cmd = CommandBufferPool.Get();
            EnsureLineBuffers(ctx, boxCount);
            GraphicsBufferUtility.Memset(cmd, ctx.LineCounterBuffer, 0, 0, 1);
            UpdateContextShaderVariables(cmd, ctx, camera, debugSettings.SpatialHashMaxDistance, debugSettings.SpatialHashMode == DebugSpatialHashMode.Heatmap);
            cmd.SetComputeConstantBufferParam(m_DebugCullingGridCS, LocalNameID.DebugCullingGridShaderVariables, ctx.DebugShaderVariablesBuffer, 0, ctx.DebugShaderVariablesBuffer.stride);
            return cmd;
        }

        private void SubmitContextDraw(CommandBuffer cmd, Camera camera, in ContextHandle ctx)
        {
            Graphics.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);

            ctx.LineMaterial.SetBuffer(LocalNameID._LineVertices, ctx.LineVertexBuffer);
            var renderParams = new RenderParams
            {
                renderingLayerMask = uint.MaxValue,
                worldBounds = new Bounds(camera.transform.position, Vector3.one * 1000000f),
                camera = camera,
                material = ctx.LineMaterial,
            };
            Graphics.RenderPrimitivesIndirect(renderParams, MeshTopology.Lines, ctx.LineDrawArgsBuffer);
        }

        private void DrawBlocks(Camera camera, in FloraDebugDisplayProperties debugSettings)
        {
            NativeBitSet allocatedBlocks = m_CullingGrid.ValueRO.BlockAllocated;
            int maxBlockCount = allocatedBlocks.Count();
            if (maxBlockCount == 0)
                return;

            var ctx = GetOrCreateContext(DrawType.Blocks, camera);

            CommandBuffer cmd = BeginContextDraw(camera, ctx, in debugSettings, maxBlockCount);
            {
                var blockIndices = allocatedBlocks.ToArray(Allocator.Temp);
                var cellInstanceCounts = m_CullingGrid.ValueRO.CellInstanceCount;

                var blockIndexBuffer = GraphicsBufferStore.RequestStructured(cmd, blockIndices);
                var cellInstanceCountBuffer = GraphicsBufferStore.RequestStructured(cmd, cellInstanceCounts);

                cmd.SetComputeIntParam(m_DebugCullingGridCS, LocalNameID._BlockCount, blockIndices.Length);
                cmd.SetComputeBufferParam(m_DebugCullingGridCS, m_BuildBlockLinesKernel, LocalNameID._BlockIndices, blockIndexBuffer);
                cmd.SetComputeBufferParam(m_DebugCullingGridCS, m_BuildBlockLinesKernel, LocalNameID._BlockData, m_CullingGrid.ValueRO.BlockDataBuffer);
                cmd.SetComputeBufferParam(m_DebugCullingGridCS, m_BuildBlockLinesKernel, LocalNameID._CellInstanceCounts, cellInstanceCountBuffer);

                cmd.SetComputeBufferParam(m_DebugCullingGridCS, m_BuildBlockLinesKernel, LocalNameID._LineVertices, ctx.LineVertexBuffer);
                cmd.SetComputeBufferParam(m_DebugCullingGridCS, m_BuildBlockLinesKernel, LocalNameID._LineVertexCounter, ctx.LineCounterBuffer);

                int3 threadGroups = ComputeUtility.WrapDispatchCount(blockIndices.Length, 64);
                cmd.DispatchCompute(m_DebugCullingGridCS, m_BuildBlockLinesKernel, threadGroups);

                cmd.SetComputeBufferParam(m_DebugCullingGridCS, m_BuildDrawArgsKernel, LocalNameID._LineVertexCounter, ctx.LineCounterBuffer);
                cmd.SetComputeBufferParam(m_DebugCullingGridCS, m_BuildDrawArgsKernel, LocalNameID._LineDrawArgs, ctx.LineDrawArgsBuffer);
                cmd.DispatchCompute(m_DebugCullingGridCS, m_BuildDrawArgsKernel, 1, 1, 1);
            }
            SubmitContextDraw(cmd, camera, ctx);
        }

        private void DrawCells(Camera camera, in FloraDebugDisplayProperties debugSettings)
        {
            NativeBitSet allocatedCells = m_CullingGrid.ValueRO.CellAllocated;
            int maxCellCount = allocatedCells.Count();
            if (maxCellCount == 0)
                return;

            var ctx = GetOrCreateContext(DrawType.Cells, camera);
            CommandBuffer cmd = BeginContextDraw(camera, ctx, in debugSettings, maxCellCount);
            {
                var cellIndices = allocatedCells.ToArray(Allocator.Temp);
                var cellInstanceCounts = m_CullingGrid.ValueRO.CellInstanceCount;

                var cellIndexBuffer = GraphicsBufferStore.RequestStructured(cmd, cellIndices);
                var cellInstanceCountBuffer = GraphicsBufferStore.RequestStructured(cmd, cellInstanceCounts);

                cmd.SetComputeIntParam(m_DebugCullingGridCS, LocalNameID._CellCount, cellIndices.Length);
                cmd.SetComputeBufferParam(m_DebugCullingGridCS, m_BuildCellLinesKernel, LocalNameID._CellIndices, cellIndexBuffer);
                cmd.SetComputeBufferParam(m_DebugCullingGridCS, m_BuildCellLinesKernel, LocalNameID._CellInstanceCounts, cellInstanceCountBuffer);
                cmd.SetComputeBufferParam(m_DebugCullingGridCS, m_BuildCellLinesKernel, LocalNameID._BlockData, m_CullingGrid.ValueRO.BlockDataBuffer);

                cmd.SetComputeBufferParam(m_DebugCullingGridCS, m_BuildCellLinesKernel, LocalNameID._LineVertices, ctx.LineVertexBuffer);
                cmd.SetComputeBufferParam(m_DebugCullingGridCS, m_BuildCellLinesKernel, LocalNameID._LineVertexCounter, ctx.LineCounterBuffer);

                int3 threadGroups = ComputeUtility.WrapDispatchCount(cellIndices.Length, 64);
                cmd.DispatchCompute(m_DebugCullingGridCS, m_BuildCellLinesKernel, threadGroups);

                cmd.SetComputeBufferParam(m_DebugCullingGridCS, m_BuildDrawArgsKernel, LocalNameID._LineVertexCounter, ctx.LineCounterBuffer);
                cmd.SetComputeBufferParam(m_DebugCullingGridCS, m_BuildDrawArgsKernel, LocalNameID._LineDrawArgs, ctx.LineDrawArgsBuffer);
                cmd.DispatchCompute(m_DebugCullingGridCS, m_BuildDrawArgsKernel, 1, 1, 1);
            }
            SubmitContextDraw(cmd, camera, ctx);
        }

        private void DrawChunks(Camera camera, in FloraDebugDisplayProperties debugSettings)
        {
            NativeBitSet allocatedChunks = m_CullingGrid.ValueRO.ChunkAllocated;
            int maxChunkCount = allocatedChunks.Count();
            if (maxChunkCount == 0)
                return;

            var ctx = GetOrCreateContext(DrawType.Chunks, camera);
            EnsureLineBuffers(ctx, maxChunkCount);

            CommandBuffer cmd = BeginContextDraw(camera, ctx, in debugSettings, maxChunkCount);
            {
                var chunkIndices = allocatedChunks.ToArray(Allocator.Temp);
                var chunkIndexBuffer = GraphicsBufferStore.RequestStructured(cmd, chunkIndices);

                cmd.SetComputeIntParam(m_DebugCullingGridCS, LocalNameID._ChunkCount, chunkIndices.Length);
                cmd.SetComputeBufferParam(m_DebugCullingGridCS, m_BuildChunkLinesKernel, LocalNameID._ChunkIndices, chunkIndexBuffer);
                cmd.SetComputeBufferParam(m_DebugCullingGridCS, m_BuildChunkLinesKernel, LocalNameID._BlockData, m_CullingGrid.ValueRO.BlockDataBuffer);
                cmd.SetComputeBufferParam(m_DebugCullingGridCS, m_BuildChunkLinesKernel, LocalNameID._CullingChunkBatches, m_CullingGrid.ValueRO.ChunkBatchBuffer);
                cmd.SetComputeBufferParam(m_DebugCullingGridCS, m_BuildChunkLinesKernel, LocalNameID._CullingChunkCells, m_CullingGrid.ValueRO.ChunkCellBuffer);
                cmd.SetComputeBufferParam(m_DebugCullingGridCS, m_BuildChunkLinesKernel, LocalNameID._CullingChunkAttributes, m_CullingGrid.ValueRO.ChunkAttributeBuffer);

                cmd.SetComputeBufferParam(m_DebugCullingGridCS, m_BuildChunkLinesKernel, LocalNameID._LineVertices, ctx.LineVertexBuffer);
                cmd.SetComputeBufferParam(m_DebugCullingGridCS, m_BuildChunkLinesKernel, LocalNameID._LineVertexCounter, ctx.LineCounterBuffer);

                int3 threadGroups = ComputeUtility.WrapDispatchCount(chunkIndices.Length, 64);
                cmd.DispatchCompute(m_DebugCullingGridCS, m_BuildChunkLinesKernel, threadGroups);

                cmd.SetComputeBufferParam(m_DebugCullingGridCS, m_BuildDrawArgsKernel, LocalNameID._LineVertexCounter, ctx.LineCounterBuffer);
                cmd.SetComputeBufferParam(m_DebugCullingGridCS, m_BuildDrawArgsKernel, LocalNameID._LineDrawArgs, ctx.LineDrawArgsBuffer);
                cmd.DispatchCompute(m_DebugCullingGridCS, m_BuildDrawArgsKernel, 1, 1, 1);
            }
            SubmitContextDraw(cmd, camera, ctx);
        }

        private void UpdateContextShaderVariables(CommandBuffer cmd, ContextHandle ctx, Camera camera, float maxDistance, bool isHeatmap)
        {
            if (ctx.DebugShaderVariablesBuffer == null)
            {
                ctx.DebugShaderVariablesData = new NativeArray<DebugCullingGridShaderVariables>(1, Allocator.Persistent);
                ctx.DebugShaderVariablesBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, 1, UnsafeUtility.SizeOf<DebugCullingGridShaderVariables>());
            }

            DebugCullingGridShaderVariables shaderVars = ctx.DebugShaderVariablesData[0];
            Plane[] frustumPlanes = GeometryUtility.CalculateFrustumPlanes(camera);
            for (int i = 0; i < 6; i++)
            {
                Vector4 plane = new Vector4(frustumPlanes[i].normal.x, frustumPlanes[i].normal.y, frustumPlanes[i].normal.z, frustumPlanes[i].distance);
                shaderVars._FrustumPlanes[i * 4 + 0] = plane.x;
                shaderVars._FrustumPlanes[i * 4 + 1] = plane.y;
                shaderVars._FrustumPlanes[i * 4 + 2] = plane.z;
                shaderVars._FrustumPlanes[i * 4 + 3] = plane.w;
            }

            shaderVars._CameraPositionAndDist = new Vector4(camera.transform.position.x, camera.transform.position.y, camera.transform.position.z, maxDistance);
            shaderVars._CullingSettings = new Vector4(isHeatmap ? 1f : 0f, CullingGrid.MinCellLevel, CullingGrid.MaxCellLevel, 0.8f);

            ctx.DebugShaderVariablesData[0] = shaderVars;
            cmd.SetBufferData(ctx.DebugShaderVariablesBuffer, ctx.DebugShaderVariablesData);
        }


        private static void EnsureLineBuffers(ContextHandle contextHandle, int maxLineCount)
        {
            int requiredVertexCount = maxLineCount * VerticesPerBox;
            if (contextHandle.LineVertexBuffer == null || contextHandle.LineVertexBuffer.count < requiredVertexCount)
            {
                contextHandle.LineVertexBuffer?.Dispose();
                contextHandle.LineVertexBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Append, requiredVertexCount, UnsafeUtility.SizeOf<DebugLineVertex>());
            }

            if (contextHandle.LineCounterBuffer == null || contextHandle.LineCounterBuffer.count < 1)
            {
                contextHandle.LineCounterBuffer?.Dispose();
                contextHandle.LineCounterBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Raw, 1, sizeof(int));
            }

            if (contextHandle.LineDrawArgsBuffer == null || contextHandle.LineDrawArgsBuffer.count < 4)
            {
                contextHandle.LineDrawArgsBuffer?.Dispose();
                contextHandle.LineDrawArgsBuffer = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments, 4, sizeof(int));
            }
        }

        private static void UpdateBitArrayBuffer(CommandBuffer cmd, NativeBitSet bitset, ref GraphicsBuffer buffer)
        {
            var chunks = bitset.AsChunkArray();
            int wordCount = chunks.Length * 2;
            if (wordCount == 0)
            {
                buffer?.Dispose();
                buffer = null;
                return;
            }

            if (buffer == null || buffer.count < wordCount)
            {
                buffer?.Dispose();
                buffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, wordCount, sizeof(uint));
            }

            cmd.SetBufferData(buffer, chunks);
        }
    }
}
