// Copyright © Magnetic Arcade. All Rights Reserved.
// ReSharper disable InconsistentNaming

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Rendering;

namespace MA.Flora
{
    [GenerateHLSL(PackingRules.Exact, false)]
    [StructLayout(LayoutKind.Sequential)]
    internal struct PackedChunkUploadHeader
    {
        public uint batchDomainIndex;
        public uint packedStartCount;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public PackedChunkUploadHeader(ChunkIndex chunk)
        {
            batchDomainIndex = (uint)chunk.BatchAllocation.Domain.Index;
            packedStartCount = (uint)chunk.BatchAllocation.Offset << 8 | (uint)chunk.Count;
        }
    }

    [GenerateHLSL(PackingRules.Exact, false)]
    [StructLayout(LayoutKind.Sequential)]
    internal struct SHUpdatePacket
    {
        public float shr0, shr1, shr2, shr3, shr4, shr5, shr6, shr7, shr8;
        public float shg0, shg1, shg2, shg3, shg4, shg5, shg6, shg7, shg8;
        public float shb0, shb1, shb2, shb3, shb4, shb5, shb6, shb7, shb8;
    }

    [GenerateHLSL(PackingRules.Exact, false)]
    [StructLayout(LayoutKind.Sequential)]
    public struct BufferCopyCommand
    {
        public uint srcAddress;
        public uint dstAddress;
        public uint stride;
        public uint count;
    }

    internal struct BufferScatterData<T> where T : unmanaged
    {
        public NativeArray<PackedChunkUploadHeader> ChunkHeaders;
        public NativeArray<T> InstanceValues;

        public bool IsCreated => ChunkHeaders.IsCreated && InstanceValues.IsCreated;
    }

    internal static class InstanceBufferUpload
    {
        private static class Compute
        {
            public static ComputeShader InstanceUploadShader;
            public static int ScatterStaticTransformsKernel;
            public static int ScatterInitDynamicTransformsKernel;
            public static int ScatterUpdateDynamicTransformsKernel;
            public static int ScatterUpdatePrevTransformsKernel;
            public static int ScatterSHKernel;
            public static int ScatterUintKernel;
            public static int ScatterUint2Kernel;
            public static int ScatterUint4Kernel;
            public static int CopyComponentsKernel;
        }

        private static class LocalNameID
        {
            // Scatter Shaders
            public static readonly int _ChunkUploadCount = Shader.PropertyToID("_ChunkUploadCount");
            public static readonly int _ChunkUploadHeaders = Shader.PropertyToID("_ChunkUploadHeaders");
            public static readonly int _BatchDomainAddresses = Shader.PropertyToID("_BatchDomainAddresses");
            public static readonly int _BatchTransformAddresses = Shader.PropertyToID("_BatchTransformAddresses");
            public static readonly int _GraphicsMatrices = Shader.PropertyToID("_GraphicsMatrices");
            public static readonly int _SHPackets = Shader.PropertyToID("_SHPackets");
            public static readonly int _Occlusion = Shader.PropertyToID("_Occlusion");
            public static readonly int _ValuesUint1 = Shader.PropertyToID("_ValuesUint1");
            public static readonly int _ValuesUint2 = Shader.PropertyToID("_ValuesUint2");
            public static readonly int _ValuesUint4 = Shader.PropertyToID("_ValuesUint4");
            public static readonly int _InstanceBufferRW = Shader.PropertyToID("_InstanceBufferRW");

            // Copy Shader
            public static readonly int _CommandCount = Shader.PropertyToID("_CommandCount");
            public static readonly int _CopyCommands = Shader.PropertyToID("_CopyCommands");
            public static readonly int _SrcBuffer = Shader.PropertyToID("_SrcBuffer");
            public static readonly int _DstBuffer = Shader.PropertyToID("_DstBuffer");
        }

        private static class Profiling
        {
            public static readonly ProfilerMarker ScatterStaticTransforms = new ProfilerMarker("Flora.InstanceBufferUpload.ScatterStaticTransforms");
            public static readonly ProfilerMarker ScatterInitDynamicTransforms = new ProfilerMarker("Flora.InstanceBufferUpload.ScatterInitDynamicTransforms");
            public static readonly ProfilerMarker ScatterUpdateDynamicTransforms = new ProfilerMarker("Flora.InstanceBufferUpload.ScatterUpdateDynamicTransforms");
            public static readonly ProfilerMarker ScatterSH = new ProfilerMarker("Flora.InstanceBufferUpload.ScatterSH");
            public static readonly ProfilerMarker ScatterUint = new ProfilerMarker("Flora.InstanceBufferUpload.ScatterUint");
            public static readonly ProfilerMarker ScatterUint2 = new ProfilerMarker("Flora.InstanceBufferUpload.ScatterUint2");
            public static readonly ProfilerMarker ScatterUint4 = new ProfilerMarker("Flora.InstanceBufferUpload.ScatterUint4");
            public static readonly ProfilerMarker Copy = new ProfilerMarker("Flora.InstanceBufferUpload.Copy");
        }

        public static void Initialize(FloraRuntimeResources runtimeResources)
        {
            Compute.InstanceUploadShader = runtimeResources.InstanceBufferUploadCS;
            Compute.ScatterStaticTransformsKernel = Compute.InstanceUploadShader.FindKernel("ScatterStaticTransforms");
            Compute.ScatterInitDynamicTransformsKernel = Compute.InstanceUploadShader.FindKernel("ScatterInitDynamicTransforms");
            Compute.ScatterUpdateDynamicTransformsKernel = Compute.InstanceUploadShader.FindKernel("ScatterUpdateDynamicTransforms");
            Compute.ScatterUpdatePrevTransformsKernel = Compute.InstanceUploadShader.FindKernel("ScatterUpdatePrevTransforms");
            Compute.ScatterSHKernel = Compute.InstanceUploadShader.FindKernel("ScatterSH");
            Compute.ScatterUintKernel = Compute.InstanceUploadShader.FindKernel("ScatterUint");
            Compute.ScatterUint2Kernel = Compute.InstanceUploadShader.FindKernel("ScatterUint2");
            Compute.ScatterUint4Kernel = Compute.InstanceUploadShader.FindKernel("ScatterUint4");
            Compute.CopyComponentsKernel = Compute.InstanceUploadShader.FindKernel("CopyComponents");
        }

        #region Instances

        public static void ScatterStaticTransforms(CommandBuffer cmd, GraphicsBuffer instanceBuffer, GraphicsBuffer transformAddressBuffer,
            NativeArray<PackedChunkUploadHeader> chunkHeaders, NativeArray<GraphicsMatrix> matrices)
        {
            int chunkCount = chunkHeaders.Length;
            if (chunkCount == 0)
                return;

            using (new CommandBufferProfilerScope(cmd, Profiling.ScatterStaticTransforms))
            {
                var cs = Compute.InstanceUploadShader;
                var kernel = Compute.ScatterStaticTransformsKernel;

                var headerBuffer = GraphicsBufferStore.RequestStructured(cmd, chunkHeaders, "Flora.ChunkDataHeaders");
                var transformBuffer = GraphicsBufferStore.RequestStructured(cmd, matrices, "Flora.GraphicsMatrices");

                cmd.SetComputeIntParam(cs, LocalNameID._ChunkUploadCount, chunkCount);
                cmd.SetComputeBufferParam(cs, kernel, LocalNameID._ChunkUploadHeaders, headerBuffer);
                cmd.SetComputeBufferParam(cs, kernel, LocalNameID._BatchTransformAddresses, transformAddressBuffer);
                cmd.SetComputeBufferParam(cs, kernel, LocalNameID._GraphicsMatrices, transformBuffer);
                cmd.SetComputeBufferParam(cs, kernel, LocalNameID._InstanceBufferRW, instanceBuffer);

                int3 threadGroups = ComputeUtility.WrapGroupCount(chunkCount);
                cmd.DispatchCompute(cs, kernel, threadGroups.x, threadGroups.y, threadGroups.z);
            }
        }

        public static void ScatterInitDynamicTransforms(CommandBuffer cmd, GraphicsBuffer instanceBuffer, GraphicsBuffer transformAddressBuffer,
            NativeArray<PackedChunkUploadHeader> chunkHeaders, NativeArray<GraphicsMatrix> matrices)
        {
            int chunkCount = chunkHeaders.Length;
            if (chunkCount == 0)
                return;

            using (new CommandBufferProfilerScope(cmd, Profiling.ScatterInitDynamicTransforms))
            {
                var cs = Compute.InstanceUploadShader;
                var kernel = Compute.ScatterInitDynamicTransformsKernel;

                var headerBuffer = GraphicsBufferStore.RequestStructured(cmd, chunkHeaders, "Flora.ChunkDataHeaders");
                var transformBuffer = GraphicsBufferStore.RequestStructured(cmd, matrices, "Flora.GraphicsMatrices");

                cmd.SetComputeIntParam(cs, LocalNameID._ChunkUploadCount, chunkCount);
                cmd.SetComputeBufferParam(cs, kernel, LocalNameID._ChunkUploadHeaders, headerBuffer);
                cmd.SetComputeBufferParam(cs, kernel, LocalNameID._BatchTransformAddresses, transformAddressBuffer);
                cmd.SetComputeBufferParam(cs, kernel, LocalNameID._GraphicsMatrices, transformBuffer);
                cmd.SetComputeBufferParam(cs, kernel, LocalNameID._InstanceBufferRW, instanceBuffer);

                int3 threadGroups = ComputeUtility.WrapGroupCount(chunkCount);
                cmd.DispatchCompute(cs, kernel, threadGroups.x, threadGroups.y, threadGroups.z);
            }
        }

        public static void ScatterUpdateDynamicTransforms(CommandBuffer cmd, GraphicsBuffer instanceBuffer, GraphicsBuffer transformAddressBuffer,
            NativeArray<PackedChunkUploadHeader> chunkHeaders, NativeArray<GraphicsMatrix> matrices)
        {
            int chunkCount = chunkHeaders.Length;
            if (chunkCount == 0)
                return;

            using (new CommandBufferProfilerScope(cmd, Profiling.ScatterUpdateDynamicTransforms))
            {
                var cs = Compute.InstanceUploadShader;
                var kernel = Compute.ScatterUpdateDynamicTransformsKernel;

                var headerBuffer = GraphicsBufferStore.RequestStructured(cmd, chunkHeaders, "Flora.ChunkDataHeaders");
                var transformBuffer = GraphicsBufferStore.RequestStructured(cmd, matrices, "Flora.GraphicsMatrices");

                cmd.SetComputeIntParam(cs, LocalNameID._ChunkUploadCount, chunkCount);
                cmd.SetComputeBufferParam(cs, kernel, LocalNameID._ChunkUploadHeaders, headerBuffer);
                cmd.SetComputeBufferParam(cs, kernel, LocalNameID._BatchTransformAddresses, transformAddressBuffer);
                cmd.SetComputeBufferParam(cs, kernel, LocalNameID._GraphicsMatrices, transformBuffer);
                cmd.SetComputeBufferParam(cs, kernel, LocalNameID._InstanceBufferRW, instanceBuffer);

                int3 threadGroups = ComputeUtility.WrapGroupCount(chunkCount);
                cmd.DispatchCompute(cs, kernel, threadGroups.x, threadGroups.y, threadGroups.z);
            }
        }

        public static void ScatterSH(CommandBuffer cmd, GraphicsBuffer instanceBuffer, GraphicsBuffer batchAddressBuffer,
            NativeArray<PackedChunkUploadHeader> chunkHeaders, NativeArray<SHUpdatePacket> shPackets, NativeArray<Vector4> occlusion)
        {
            int chunkCount = chunkHeaders.Length;
            if (chunkCount == 0)
                return;

            using (new CommandBufferProfilerScope(cmd, Profiling.ScatterSH))
            {
                var cs = Compute.InstanceUploadShader;
                var kernel = Compute.ScatterSHKernel;

                var headerBuffer = GraphicsBufferStore.RequestStructured(cmd, chunkHeaders, "Flora.ChunkDataHeaders");
                var shBuffer = GraphicsBufferStore.RequestStructured(cmd, shPackets, "Flora.ValuesSHPackets");
                var occlusionBuffer = GraphicsBufferStore.RequestStructured(cmd, occlusion, "Flora.ValuesOcclusion");

                cmd.SetComputeIntParam(cs, LocalNameID._ChunkUploadCount, chunkCount);
                cmd.SetComputeBufferParam(cs, kernel, LocalNameID._ChunkUploadHeaders, headerBuffer);
                cmd.SetComputeBufferParam(cs, kernel, LocalNameID._BatchDomainAddresses, batchAddressBuffer);
                cmd.SetComputeBufferParam(cs, kernel, LocalNameID._SHPackets, shBuffer);
                cmd.SetComputeBufferParam(cs, kernel, LocalNameID._Occlusion, occlusionBuffer);
                cmd.SetComputeBufferParam(cs, kernel, LocalNameID._InstanceBufferRW, instanceBuffer);

                int3 threadGroups = ComputeUtility.WrapGroupCount(chunkCount);
                cmd.DispatchCompute(cs, kernel, threadGroups.x, threadGroups.y, threadGroups.z);
            }
        }

        public static void ScatterUint(CommandBuffer cmd, GraphicsBuffer instanceBuffer, GraphicsBuffer batchAddressBuffer,
            NativeArray<PackedChunkUploadHeader> chunkHeaders, NativeArray<uint> values)
        {
            int chunkCount = chunkHeaders.Length;
            if (chunkCount == 0)
                return;

            using (new CommandBufferProfilerScope(cmd, Profiling.ScatterUint))
            {
                var cs = Compute.InstanceUploadShader;
                var kernel = Compute.ScatterUintKernel;

                var headerBuffer = GraphicsBufferStore.RequestStructured(cmd, chunkHeaders, "Flora.ChunkDataHeaders");
                var valuesBuffer = GraphicsBufferStore.RequestStructured(cmd, values, "Flora.ValuesUint1");

                cmd.SetComputeIntParam(cs, LocalNameID._ChunkUploadCount, chunkCount);
                cmd.SetComputeBufferParam(cs, kernel, LocalNameID._ChunkUploadHeaders, headerBuffer);
                cmd.SetComputeBufferParam(cs, kernel, LocalNameID._BatchDomainAddresses, batchAddressBuffer);
                cmd.SetComputeBufferParam(cs, kernel, LocalNameID._ValuesUint1, valuesBuffer);
                cmd.SetComputeBufferParam(cs, kernel, LocalNameID._InstanceBufferRW, instanceBuffer);

                int3 threadGroups = ComputeUtility.WrapGroupCount(chunkCount);
                cmd.DispatchCompute(cs, kernel, threadGroups.x, threadGroups.y, threadGroups.z);
            }
        }

        public static void ScatterUint2(CommandBuffer cmd, GraphicsBuffer instanceBuffer, GraphicsBuffer batchAddressBuffer,
            NativeArray<PackedChunkUploadHeader> chunkHeaders, NativeArray<uint2> values)
        {
            int chunkCount = chunkHeaders.Length;
            if (chunkCount == 0)
                return;

            using (new CommandBufferProfilerScope(cmd, Profiling.ScatterUint2))
            {
                var cs = Compute.InstanceUploadShader;
                var kernel = Compute.ScatterUint2Kernel;

                var headerBuffer = GraphicsBufferStore.RequestStructured(cmd, chunkHeaders, "Flora.ChunkDataHeaders");
                var valuesBuffer = GraphicsBufferStore.RequestStructured(cmd, values, "Flora.ValuesUint2");

                cmd.SetComputeIntParam(cs, LocalNameID._ChunkUploadCount, chunkCount);
                cmd.SetComputeBufferParam(cs, kernel, LocalNameID._ChunkUploadHeaders, headerBuffer);
                cmd.SetComputeBufferParam(cs, kernel, LocalNameID._BatchDomainAddresses, batchAddressBuffer);
                cmd.SetComputeBufferParam(cs, kernel, LocalNameID._ValuesUint2, valuesBuffer);
                cmd.SetComputeBufferParam(cs, kernel, LocalNameID._InstanceBufferRW, instanceBuffer);

                int3 threadGroups = ComputeUtility.WrapGroupCount(chunkCount);
                cmd.DispatchCompute(cs, kernel, threadGroups.x, threadGroups.y, threadGroups.z);
            }
        }

        public static void ScatterUint4(CommandBuffer cmd, GraphicsBuffer instanceBuffer, GraphicsBuffer batchAddressBuffer,
            NativeArray<PackedChunkUploadHeader> chunkHeaders, NativeArray<uint4> values)
        {
            int chunkCount = chunkHeaders.Length;
            if (chunkCount == 0)
                return;

            using (new CommandBufferProfilerScope(cmd, Profiling.ScatterUint4))
            {
                var cs = Compute.InstanceUploadShader;
                var kernel = Compute.ScatterUint4Kernel;

                var headerBuffer = GraphicsBufferStore.RequestStructured(cmd, chunkHeaders, "Flora.ChunkDataHeaders");
                var valuesBuffer = GraphicsBufferStore.RequestStructured(cmd, values, "Flora.ValuesUint4");

                cmd.SetComputeIntParam(cs, LocalNameID._ChunkUploadCount, chunkCount);
                cmd.SetComputeBufferParam(cs, kernel, LocalNameID._ChunkUploadHeaders, headerBuffer);
                cmd.SetComputeBufferParam(cs, kernel, LocalNameID._BatchDomainAddresses, batchAddressBuffer);
                cmd.SetComputeBufferParam(cs, kernel, LocalNameID._ValuesUint4, valuesBuffer);
                cmd.SetComputeBufferParam(cs, kernel, LocalNameID._InstanceBufferRW, instanceBuffer);

                int3 threadGroups = ComputeUtility.WrapGroupCount(chunkCount);
                cmd.DispatchCompute(cs, kernel, threadGroups.x, threadGroups.y, threadGroups.z);
            }
        }

        #endregion

        #region Utility

        private const int MaxThreadGroupsPerCopyDispatch = 65535;

        public static void CopyComponents(GraphicsBuffer srcBuffer, GraphicsBuffer dstBuffer,
            NativeArray<BufferCopyCommand> commands)
        {
            if (commands.Length == 0)
                return;

            Assert.IsTrue(srcBuffer.target == GraphicsBuffer.Target.Raw);
            Assert.IsTrue(dstBuffer.target == GraphicsBuffer.Target.Raw);

            var cs = Compute.InstanceUploadShader;
            var kernel = Compute.CopyComponentsKernel;
            var copyCommandBuffer = GraphicsBufferStore.RequestStructured(commands, "Flora.CopyCommands");

            cs.SetInt(LocalNameID._CommandCount, commands.Length);
            cs.SetBuffer(kernel, LocalNameID._CopyCommands, copyCommandBuffer);
            cs.SetBuffer(kernel, LocalNameID._SrcBuffer, srcBuffer);
            cs.SetBuffer(kernel, LocalNameID._DstBuffer, dstBuffer);

            int3 threadGroups = ComputeUtility.WrapGroupCount(commands.Length);
            cs.Dispatch(kernel, threadGroups.x, threadGroups.y, threadGroups.z);
        }

        #endregion
    }
}
