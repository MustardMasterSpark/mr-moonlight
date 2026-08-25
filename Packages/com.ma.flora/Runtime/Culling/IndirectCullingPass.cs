// Copyright © Magnetic Arcade. All Rights Reserved.
// ReSharper disable InconsistentNaming


using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Rendering;

namespace MA.Flora
{
    [StructLayout(LayoutKind.Sequential)]
    [GenerateHLSL(PackingRules.Exact, false)]
    internal struct IndirectDrawPartition
    {
        public uint binOffset;
        public uint slotsPerLod;
        public uint stateMask;
        public uint stateIndices;
    }

    [StructLayout(LayoutKind.Sequential)]
    [GenerateHLSL(PackingRules.Exact, false)]
    [DebuggerDisplay("visibleStart={visibleStart}, visibleCount={visibleCount}, commandStart={commandStart}, commandCount={commandCount}")]
    internal struct IndirectDrawBin
    {
        public uint visibleStart;
        public uint visibleCount;
        public uint commandStart;
        public uint commandCount;
    }

    [StructLayout(LayoutKind.Sequential)]
    [GenerateHLSL(PackingRules.Exact, false)]
    [DebuggerDisplay("indexCountPerInstance={indexCountPerInstance}, startIndex={startIndex}, baseVertexIndex={baseVertexIndex}, startInstance={startInstance}")]
    internal struct IndirectDrawInfo
    {
        public uint indexCountPerInstance;
        public uint startIndex;
        public uint baseVertexIndex;
        public uint startInstance;
    }

    [StructLayout(LayoutKind.Sequential)]
    [GenerateHLSL(PackingRules.Exact, false)]
    internal struct IndirectDrawChunk
    {
        public uint packedChunkAndSplit;
        public uint packedArchetypeAndState;
        public uint drawPartitionIndex;
        public uint reserved;

        public IndirectDrawChunk(ArchetypeIndex archetype, CullingChunkIndex chunk, byte splitMask, byte supportedStateMask, uint drawPartitionIndex)
        {
            packedChunkAndSplit = (uint)chunk.Index << 8 | splitMask;
            packedArchetypeAndState = (uint)archetype.Index << 8 | supportedStateMask;
            this.drawPartitionIndex = drawPartitionIndex;
            reserved = 0;
        }
    }

    [Flags]
    [GenerateHLSL]
    internal enum IndirectStateFlags
    {
        None              = 0,
        HasFadeKeyword    = 1 << 0,
        HasMotion         = 1 << 1,
        HasFlippedWinding = 1 << 2,
        Count             = 3,
        All               = HasFadeKeyword | HasMotion | HasFlippedWinding,
        KeyCount          = 1 << Count
    }

    [GenerateHLSL]
    internal enum IndirectDispatchCounter
    {
        VisibleDraws,
        VisibleInstances,
        OccludedInstances,
        Count
    }

    [GenerateHLSL]
    internal enum GPUErrorCode
    {
        None,
        PerInstanceEmitOverflow,
        StateKeyOutOfRange,
        LodIndexOutOfRange,
        TemplateLodInconsistent,
        BinIndexOverflow,
        CommandCountZero,
        StateKeyNotSupported,
        PackedKeyOrLodOutOfRange,
        BinWritePastReservedEnd,
    }

    internal ref struct IndirectCullingParams
    {
        // CPU Culling
        public int MaxChunkCount;
        public GraphicsBuffer DrawChunkBuffer;

        // Draw Commands
        public int DrawArgsCount;
        public GraphicsBuffer DrawInfoBuffer;

        // Draw Bins
        public int DrawBinCount;
        public DrawBinConfig BinConfig;
        public GraphicsBuffer DrawBinBuffer;

        // Instance
        public GraphicsBuffer InstanceBuffer;
        public GraphicsBuffer ArchetypeDataBuffer;
        public GraphicsBuffer BatchDomainAddressBuffer;
        public GraphicsBuffer TemplateDataBuffer;
        public GraphicsBuffer DrawPartitionBuffer;

        // Culling Grid
        public GraphicsBuffer BlockDataBuffer;
        public GraphicsBuffer CullingChunkBuffer;
        public GraphicsBuffer CullingChunkCellBuffer;
        public GraphicsBuffer CullingChunkFlagBuffer;
        public GraphicsBuffer CullingChunkBatchDomainBuffer;
        public GraphicsBuffer CullingChunkAttributeBuffer;
        public GraphicsBuffer CullingChunkIndirectOffsetBuffer;

        // Work Groups
        public GraphicsBuffer CullingWorkGroupArgsBuffer;
        public GraphicsBuffer CullingWorkGroupDataBuffer;

        // Outputs
        public GraphicsBuffer IndirectArgsBuffer;
        public GraphicsBuffer VisibleInstancesBuffer;

        // View
        public ConstantBufferRef<CullingViewShaderVariables> ViewShaderVariables;
        public OccluderHandles OccluderHandles;
        public BatchCullingViewType ViewType;
        public int InstanceCountMultiplier;

        // Debug
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public bool EnableDebug;
        public GraphicsBuffer DebugDispatchCounterBuffer;
        public GraphicsBuffer DebugInstanceDrawBuffer;
        public int DebugErrorCapacity;
        public GraphicsBuffer DebugErrorBuffer;
        public GraphicsBuffer DebugErrorCountBuffer;
#endif
        // Editor
#if UNITY_EDITOR
        public bool IsSceneViewCamera;
        public int IncludedChunkCount;
        public GraphicsBuffer IncludedInstancesBuffer;
#endif
    }

    internal sealed class IndirectCullingPass
    {
        private static class LocalNameID
        {
            // View Data
            public static readonly int CullingViewShaderVariables = Shader.PropertyToID("CullingViewShaderVariables");

            // Culling Grid
            public static readonly int _BlockData = Shader.PropertyToID("_BlockData");
            public static readonly int _CullingChunkCells = Shader.PropertyToID("_CullingChunkCells");
            public static readonly int _CullingChunkInfos = Shader.PropertyToID("_CullingChunkInfos");
            public static readonly int _CullingChunkFlags = Shader.PropertyToID("_CullingChunkFlags");
            public static readonly int _CullingChunkFlagChannelCount = Shader.PropertyToID("_CullingChunkFlagChannelCount");
            public static readonly int _CullingChunkBatches = Shader.PropertyToID("_CullingChunkBatches");
            public static readonly int _CullingChunkAttributes = Shader.PropertyToID("_CullingChunkAttributes");
            public static readonly int _CullingIndirectOffsets = Shader.PropertyToID("_CullingIndirectOffsets");

            // Culling Data
            public static readonly int _ArchetypeData = Shader.PropertyToID("_ArchetypeData");
            public static readonly int _BatchCullingAddresses = Shader.PropertyToID("_BatchCullingAddresses");
            public static readonly int _TemplateData = Shader.PropertyToID("_TemplateData");
            public static readonly int _DrawPartitions = Shader.PropertyToID("_DrawPartitions");

            // Chunk Data
            public static readonly int _DrawChunks = Shader.PropertyToID("_DrawChunks");
            public static readonly int _MaxChunkCount = Shader.PropertyToID("_MaxChunkCount");

            // Work Group Data
            public static readonly int _MaxWorkGroupCountX = Shader.PropertyToID("_MaxWorkGroupCountX");
            public static readonly int _MaxWorkGroupCountY = Shader.PropertyToID("_MaxWorkGroupCountY");
            public static readonly int _CullingWorkGroupArgsRW = Shader.PropertyToID("_CullingWorkGroupArgsRW");
            public static readonly int _CullingWorkGroupArgs = Shader.PropertyToID("_CullingWorkGroupArgs");
            public static readonly int _CullingWorkGroupsRW = Shader.PropertyToID("_CullingWorkGroupsRW");
            public static readonly int _CullingWorkGroups = Shader.PropertyToID("_CullingWorkGroups");

            // Draw Data
            public static readonly int _DrawBins = Shader.PropertyToID("_DrawBins");
            public static readonly int _DrawBinCount = Shader.PropertyToID("_DrawBinCount");
            public static readonly int _DrawInfos = Shader.PropertyToID("_DrawInfos");
            public static readonly int _DrawCount = Shader.PropertyToID("_DrawCount");
            public static readonly int _DrawArgsRW = Shader.PropertyToID("_DrawArgsRW");
            public static readonly int _DensityCullingEnabled = Shader.PropertyToID("_DensityCullingEnabled");

            // Visibility Data
            public static readonly int _InstanceMultiplierShift = Shader.PropertyToID("_InstanceMultiplierShift");
            public static readonly int _InstanceVisibilityRW = Shader.PropertyToID("_InstanceVisibilityRW");

            // Debug Data
            public static readonly int _DebugLODIndex = Shader.PropertyToID("_DebugLODIndex");
            public static readonly int _DebugLODMode = Shader.PropertyToID("_DebugLODMode");
            public static readonly int _DebugDispatchCounter = Shader.PropertyToID("_DebugDispatchCounter");
            public static readonly int _DebugCounterEnabled = Shader.PropertyToID("_DebugCounterEnabled");
            public static readonly int _DebugDrawVisibility = Shader.PropertyToID("_DebugDrawVisibility");
            public static readonly int _DebugShadingMode = Shader.PropertyToID("_DebugShadingMode");
            public static readonly int _DebugErrorCapacity = Shader.PropertyToID("_DebugErrorCapacity");
            public static readonly int _DebugErrorBuffer = Shader.PropertyToID("_DebugErrorBuffer");
            public static readonly int _DebugErrorCount = Shader.PropertyToID("_DebugErrorCount");

            // Editor Data
            public static readonly int _IncludedChunkCount = Shader.PropertyToID("_IncludedChunkCount");
            public static readonly int _IncludedInstances = Shader.PropertyToID("_IncludedInstances");
            public static readonly int _EditorViewPass = Shader.PropertyToID("_EditorViewPass");
        }

        private static readonly ProfilerMarker CameraVisibilityMarker = new ProfilerMarker("Flora.CameraVisibility");
        private static readonly ProfilerMarker LightVisibilityMarker = new ProfilerMarker("Flora.LightVisibility");
        private static readonly ProfilerMarker PickingVisibilityMarker = new ProfilerMarker("Flora.PickingVisibility");
        private static readonly ProfilerMarker SelectionOutlineVisibilityMarker = new ProfilerMarker("Flora.SelectionOutlineVisibility");
        private static readonly ProfilerMarker UnknownVisibilityMarker = new ProfilerMarker("Flora.UnknownVisibility");

        private enum SceneViewPass
        {
            Normal           = 0,
            Picking          = 1,
            SelectionOutline = 2,
        }

        private static ProfilerMarker GetDispatchMarker(BatchCullingViewType viewType)
        {
            return viewType switch
            {
                BatchCullingViewType.Camera           => CameraVisibilityMarker,
                BatchCullingViewType.Light            => LightVisibilityMarker,
                BatchCullingViewType.Picking          => PickingVisibilityMarker,
                BatchCullingViewType.SelectionOutline => SelectionOutlineVisibilityMarker,
                _                                     => UnknownVisibilityMarker,
            };
        }

        private struct ChunkCullingPass
        {
            public static readonly ProfilerMarker Marker = new ProfilerMarker("Flora.ChunkCulling");

            public ComputeShader ComputeShader;
            public int InitCullingWorkGroupArgsKernel;
            public int InitCullingWorkGroupsKernel;
            public int CullChunksKernel;
            public int SanitizeWorkGroupArgsKernel;

            public LocalKeyword UseOcclusionKeyword;
            public LocalKeyword ViewIsLightKeyword;
            public LocalKeyword DebugOcclusion;
            public LocalKeyword DebugEnabled;
            public LocalKeyword ViewIsEditorKeyword;

            public ChunkCullingPass(ComputeShader cs)
            {
                ComputeShader = cs;
                InitCullingWorkGroupArgsKernel = cs.FindKernel("InitCullingWorkGroupArgs");
                InitCullingWorkGroupsKernel = cs.FindKernel("InitCullingWorkGroups");
                CullChunksKernel = cs.FindKernel("CullChunks");
                SanitizeWorkGroupArgsKernel = cs.FindKernel("SanitizeWorkGroupArgs");

                UseOcclusionKeyword = new LocalKeyword(cs, "USE_OCCLUSION");
                ViewIsLightKeyword = new LocalKeyword(cs, "VIEW_IS_LIGHT");
                DebugOcclusion = new LocalKeyword(cs, "DEBUG_OCCLUSION");
                DebugEnabled = new LocalKeyword(cs, "DEBUG_ENABLED");
                ViewIsEditorKeyword = new LocalKeyword(cs, "VIEW_IS_EDITOR");
            }
        }

        private struct InstanceCullingPass
        {
            public static readonly ProfilerMarker Marker = new ProfilerMarker("Flora.InstanceCulling");

            public ComputeShader ComputeShader;
            public int CullInstancesKernel;

            public LocalKeyword UseOcclusionKeyword;
            public LocalKeyword ViewIsLightKeyword;
            public LocalKeyword DebugOcclusion;
            public LocalKeyword DebugEnabled;
            public LocalKeyword ViewIsEditorKeyword;

            public InstanceCullingPass(ComputeShader cs)
            {
                ComputeShader = cs;
                CullInstancesKernel = cs.FindKernel("CullInstances");

                UseOcclusionKeyword = new LocalKeyword(cs, "USE_OCCLUSION");
                ViewIsLightKeyword = new LocalKeyword(cs, "VIEW_IS_LIGHT");
                DebugOcclusion = new LocalKeyword(cs, "DEBUG_OCCLUSION");
                DebugEnabled = new LocalKeyword(cs, "DEBUG_ENABLED");
                ViewIsEditorKeyword = new LocalKeyword(cs, "VIEW_IS_EDITOR");
            }
        }

        private struct ScatterDrawArgsPass
        {
            public static readonly ProfilerMarker Marker = new ProfilerMarker("Flora.ScatterDrawArgs");

            public ComputeShader ComputeShader;
            public int ResetDrawArgsKernel;
            public int ScatterDrawArgsKernel;
            public LocalKeyword DebugEnabled;

            public ScatterDrawArgsPass(ComputeShader cs)
            {
                ComputeShader = cs;
                ResetDrawArgsKernel = cs.FindKernel("ResetDrawArgs");
                ScatterDrawArgsKernel = cs.FindKernel("ScatterDrawArgs");
                DebugEnabled = new LocalKeyword(cs, "DEBUG_ENABLED");
            }
        }

        private ChunkCullingPass m_ChunkCullingPass;
        private InstanceCullingPass m_InstanceCullingPass;
        private ScatterDrawArgsPass m_ScatterDrawArgsPass;

        public ComputeShader[] ShadersWithOcclusion = new ComputeShader[2];

        public IndirectCullingPass(FloraRuntimeResources runtimeResources)
        {
            m_ChunkCullingPass = new ChunkCullingPass(runtimeResources.IndirectCullingChunksCS);
            m_InstanceCullingPass = new InstanceCullingPass(runtimeResources.IndirectCullingInstancesCS);
            m_ScatterDrawArgsPass = new ScatterDrawArgsPass(runtimeResources.IndirectCullingDrawsCS);

            ShadersWithOcclusion[0] = m_ChunkCullingPass.ComputeShader;
            ShadersWithOcclusion[1] = m_InstanceCullingPass.ComputeShader;
        }

        public void Dispatch(CommandBuffer cmd, in IndirectCullingParams input)
        {
            using (new CommandBufferProfilerScope(cmd, GetDispatchMarker(input.ViewType)))
            {
                DispatchCullChunks(cmd, input);
                DispatchCullInstances(cmd, input);
                DispatchScatterDrawArgs(cmd, input);
            }
        }

        #region Passes

        private void DispatchCullChunks(CommandBuffer cmd, in IndirectCullingParams input)
        {
            using (new CommandBufferProfilerScope(cmd, ChunkCullingPass.Marker))
            {
                {
                    // Initialize work group args
                    var cs = m_ChunkCullingPass.ComputeShader;
                    var kernel = m_ChunkCullingPass.InitCullingWorkGroupArgsKernel;

                    cmd.SetComputeBufferParam(cs, kernel, LocalNameID._CullingWorkGroupArgsRW, input.CullingWorkGroupArgsBuffer);
                    cmd.DispatchCompute(cs, kernel, 1, 1, 1);
                }

                {
                    // Initialize work groups
                    var cs = m_ChunkCullingPass.ComputeShader;
                    var kernel = m_ChunkCullingPass.InitCullingWorkGroupsKernel;

                    cmd.SetComputeIntParam(cs, LocalNameID._MaxChunkCount, input.MaxChunkCount);
                    cmd.SetComputeBufferParam(cs, kernel, LocalNameID._CullingWorkGroupsRW, input.CullingWorkGroupDataBuffer);

                    int3 threadGroups = ComputeUtility.WrapDispatchCount(input.MaxChunkCount, 64);
                    cmd.DispatchCompute(cs, kernel, threadGroups);
                }

                {
                    // Cull chunks
                    var cs = m_ChunkCullingPass.ComputeShader;
                    var kernel = m_ChunkCullingPass.CullChunksKernel;

                    SetViewConstants(cmd, cs, input);
                    ConfigureLightView(cmd, cs, kernel, input, m_ChunkCullingPass.ViewIsLightKeyword);
                    ConfigureOcclusion(cmd, cs, kernel, input, m_ChunkCullingPass.UseOcclusionKeyword, m_ChunkCullingPass.DebugOcclusion);
                    ConfigureEditor(cmd, cs, kernel, input, m_ChunkCullingPass.ViewIsEditorKeyword);
                    ConfigureDebug(cmd, cs, kernel, input, m_ChunkCullingPass.DebugEnabled);

                    // Culling Grid
                    cmd.SetComputeIntParam(cs, LocalNameID._CullingChunkFlagChannelCount, (int)CullingFlagChannel.Count);
                    cmd.SetComputeBufferParam(cs, kernel, LocalNameID._BlockData, input.BlockDataBuffer);
                    cmd.SetComputeBufferParam(cs, kernel, LocalNameID._CullingChunkCells, input.CullingChunkCellBuffer);
                    cmd.SetComputeBufferParam(cs, kernel, LocalNameID._CullingChunkInfos, input.CullingChunkBatchDomainBuffer);
                    cmd.SetComputeBufferParam(cs, kernel, LocalNameID._CullingChunkFlags, input.CullingChunkFlagBuffer);
                    cmd.SetComputeBufferParam(cs, kernel, LocalNameID._CullingChunkBatches, input.CullingChunkBuffer);
                    cmd.SetComputeBufferParam(cs, kernel, LocalNameID._CullingChunkAttributes, input.CullingChunkAttributeBuffer);

                    // Inputs
                    cmd.SetComputeIntParam(cs, LocalNameID._MaxChunkCount, input.MaxChunkCount);
                    cmd.SetComputeBufferParam(cs, kernel, LocalNameID._DrawChunks, input.DrawChunkBuffer);

                    // Outputs
                    cmd.SetComputeBufferParam(cs, kernel, LocalNameID._CullingWorkGroupArgsRW, input.CullingWorkGroupArgsBuffer);
                    cmd.SetComputeBufferParam(cs, kernel, LocalNameID._CullingWorkGroupsRW, input.CullingWorkGroupDataBuffer);

                    int3 threadGroups = ComputeUtility.WrapDispatchCount(input.MaxChunkCount, 64);
                    cmd.DispatchCompute(cs, kernel, threadGroups);
                }

                {
                    // Sanitize work group args
                    var cs = m_ChunkCullingPass.ComputeShader;
                    var kernel = m_ChunkCullingPass.SanitizeWorkGroupArgsKernel;

                    cmd.SetComputeIntParam(cs, LocalNameID._MaxChunkCount, input.MaxChunkCount);
                    cmd.SetComputeIntParam(cs, LocalNameID._MaxWorkGroupCountX, SystemInfo.maxComputeWorkGroupSizeX);
                    cmd.SetComputeIntParam(cs, LocalNameID._MaxWorkGroupCountY, SystemInfo.maxComputeWorkGroupSizeY);
                    cmd.SetComputeBufferParam(cs, kernel, LocalNameID._CullingWorkGroupArgsRW, input.CullingWorkGroupArgsBuffer);
                    cmd.DispatchCompute(cs, kernel, 1, 1, 1);
                }
            }
        }

        private void DispatchCullInstances(CommandBuffer cmd, in IndirectCullingParams input)
        {
            using (new CommandBufferProfilerScope(cmd, InstanceCullingPass.Marker))
            {
                // XR instancing with multipass rendering uses double the instance count but the same culling data.
                // We need to shift the instance index by one to get the correct instance in the culling shader.
                int instanceMultiplierShift = 0;
                if (input.ViewType is BatchCullingViewType.Camera && input.InstanceCountMultiplier == 2)
                    instanceMultiplierShift = 1;

                var cs = m_InstanceCullingPass.ComputeShader;
                var kernel = m_InstanceCullingPass.CullInstancesKernel;

                SetViewConstants(cmd, cs, input);
                ConfigureLightView(cmd, cs, kernel, input, m_InstanceCullingPass.ViewIsLightKeyword);
                ConfigureOcclusion(cmd, cs, kernel, input, m_InstanceCullingPass.UseOcclusionKeyword, m_InstanceCullingPass.DebugOcclusion);
                ConfigureEditor(cmd, cs, kernel, input, m_InstanceCullingPass.ViewIsEditorKeyword);
                ConfigureDebug(cmd, cs, kernel, input, m_InstanceCullingPass.DebugEnabled);

                // Instances
                cmd.SetComputeBufferParam(cs, kernel, LocalNameID._BatchCullingAddresses, input.BatchDomainAddressBuffer);
                cmd.SetComputeBufferParam(cs, kernel, LocalNameID._ArchetypeData, input.ArchetypeDataBuffer);
                cmd.SetComputeBufferParam(cs, kernel, LocalNameID._TemplateData, input.TemplateDataBuffer);
                cmd.SetComputeBufferParam(cs, kernel, ShaderPropertyId.unity_DOTSInstanceData, input.InstanceBuffer);

                // Culling grid
                cmd.SetComputeIntParam(cs, LocalNameID._CullingChunkFlagChannelCount, (int)CullingFlagChannel.Count);
                cmd.SetComputeBufferParam(cs, kernel, LocalNameID._CullingChunkFlags, input.CullingChunkFlagBuffer);
                cmd.SetComputeBufferParam(cs, kernel, LocalNameID._CullingChunkBatches, input.CullingChunkBuffer);
                cmd.SetComputeBufferParam(cs, kernel, LocalNameID._CullingIndirectOffsets, input.CullingChunkIndirectOffsetBuffer);

                // Inputs
                cmd.SetComputeIntParam(cs, LocalNameID._MaxChunkCount, input.MaxChunkCount);
                cmd.SetComputeBufferParam(cs, kernel, LocalNameID._CullingWorkGroupArgs, input.CullingWorkGroupArgsBuffer);
                cmd.SetComputeBufferParam(cs, kernel, LocalNameID._CullingWorkGroups, input.CullingWorkGroupDataBuffer);
                cmd.SetComputeBufferParam(cs, kernel, LocalNameID._DrawPartitions, input.DrawPartitionBuffer);

                // Binning Outputs
                cmd.SetComputeIntParam(cs, LocalNameID._DrawBinCount, input.DrawBinCount);
                cmd.SetComputeBufferParam(cs, kernel, LocalNameID._DrawBins, input.DrawBinBuffer);

                // Visibility Outputs
                cmd.SetComputeIntParam(cs, LocalNameID._InstanceMultiplierShift, instanceMultiplierShift);
                bool densityCullingEnabled = FloraSystem.Instance.AllowDensityCulling;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                densityCullingEnabled &= !(DebugDisplayFlora.Active && DebugDisplayFlora.Properties.DisableDensityCulling);
#endif
                cmd.SetComputeIntParam(cs, LocalNameID._DensityCullingEnabled, densityCullingEnabled ? 1 : 0);
                cmd.SetComputeBufferParam(cs, kernel, LocalNameID._InstanceVisibilityRW, input.VisibleInstancesBuffer);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if (DebugDisplayFlora.Active && DebugDisplayFlora.Properties.EnableGPUChecks)
                {
                    cmd.SetComputeIntParam(cs, LocalNameID._DebugErrorCapacity, input.DebugErrorCapacity);
                    cmd.SetComputeBufferParam(cs, kernel, LocalNameID._DebugErrorBuffer, input.DebugErrorBuffer);
                    cmd.SetComputeBufferParam(cs, kernel, LocalNameID._DebugErrorCount, input.DebugErrorCountBuffer);
                }
                else
                {
                    cmd.SetComputeIntParam(cs, LocalNameID._DebugErrorCapacity, 0);
                    cmd.SetComputeBufferParam(cs, kernel, LocalNameID._DebugErrorBuffer, CoreUtils.emptyBuffer);
                    cmd.SetComputeBufferParam(cs, kernel, LocalNameID._DebugErrorCount, CoreUtils.emptyBuffer);
                }
#endif

                // Dispatch indirectly based on the number of work groups generated in the chunk culling pass
                cmd.DispatchCompute(cs, kernel, input.CullingWorkGroupArgsBuffer, 0);
            }
        }

        private void DispatchScatterDrawArgs(CommandBuffer cmd, in IndirectCullingParams input)
        {
            using (new CommandBufferProfilerScope(cmd, ScatterDrawArgsPass.Marker))
            {
                var cs = m_ScatterDrawArgsPass.ComputeShader;
                cmd.SetComputeIntParam(cs, LocalNameID._DrawCount, input.DrawArgsCount);
                cmd.SetComputeIntParam(cs, LocalNameID._DrawBinCount, input.DrawBinCount);
                {
                    var kernel = m_ScatterDrawArgsPass.ResetDrawArgsKernel;

                    cmd.SetComputeBufferParam(cs, kernel, LocalNameID._DrawInfos, input.DrawInfoBuffer);
                    cmd.SetComputeBufferParam(cs, kernel, LocalNameID._DrawArgsRW, input.IndirectArgsBuffer);

                    if (input.DrawArgsCount > 0)
                    {
                        int3 threadGroups = ComputeUtility.WrapDispatchCount(input.DrawArgsCount, 64);
                        cmd.DispatchCompute(cs, kernel, threadGroups);
                    }
                }
                {
                    var kernel = m_ScatterDrawArgsPass.ScatterDrawArgsKernel;
                    ConfigureDebug(cmd, cs, kernel, input, m_ScatterDrawArgsPass.DebugEnabled);

                    cmd.SetComputeBufferParam(cs, kernel, LocalNameID._DrawBins, input.DrawBinBuffer);
                    cmd.SetComputeBufferParam(cs, kernel, LocalNameID._DrawArgsRW, input.IndirectArgsBuffer);

                    if (input.DrawBinCount > 0)
                    {
                        int3 threadGroups = ComputeUtility.WrapDispatchCount(input.DrawBinCount, 64);
                        cmd.DispatchCompute(cs, kernel, threadGroups);
                    }
                }
            }
        }

        #endregion

        #region Helpers

        private static void SetViewConstants(CommandBuffer cmd, ComputeShader cs, in IndirectCullingParams input)
        {
            cmd.SetComputeConstantBufferParam(cs, LocalNameID.CullingViewShaderVariables,   input.ViewShaderVariables);
        }

        private static void ConfigureOcclusion(CommandBuffer cmd, ComputeShader cs, int kernel,
            in IndirectCullingParams input, LocalKeyword occlusionKeyword, LocalKeyword debugOcclusionKeyword)
        {
            bool useOcclusion = input.OccluderHandles.IsValid() && input.ViewType == BatchCullingViewType.Camera;
            cmd.SetKeyword(cs, occlusionKeyword, useOcclusion);
            if (useOcclusion)
            {
                var depth = input.OccluderHandles.GetDepthPyramid();
                cmd.SetComputeTextureParam(cs, kernel, OcclusionNameID.OccluderDepthPyramid, depth);
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            var debugOverlay = input.OccluderHandles.GetDebugOverlay();
            var debugOcclusion = useOcclusion && DebugDisplayFlora.Active && debugOverlay != null && debugOverlay.IsValid();
            cmd.SetKeyword(cs, debugOcclusionKeyword, debugOcclusion);
            if (debugOcclusion)
                cmd.SetComputeBufferParam(cs, kernel, OcclusionNameID.OcclusionDebugOverlay, debugOverlay);
#endif
        }

        private static void ConfigureLightView(CommandBuffer cmd, ComputeShader cs, int kernel,
            in IndirectCullingParams input, LocalKeyword lightKeyword)
        {
            cmd.SetKeyword(cs, lightKeyword, input.ViewType == BatchCullingViewType.Light);
        }

        [Conditional("UNITY_EDITOR")]
        private static void ConfigureEditor(CommandBuffer cmd, ComputeShader cs, int kernel,
            in IndirectCullingParams input, LocalKeyword editorViewKeyword)
        {
#if UNITY_EDITOR
            bool isEditorView = input.IsSceneViewCamera ||
                                input.ViewType == BatchCullingViewType.Picking ||
                                input.ViewType == BatchCullingViewType.SelectionOutline ||
                                input.ViewType == BatchCullingViewType.Filtering;

            cmd.SetKeyword(cs, editorViewKeyword, isEditorView);
            if (isEditorView)
            {
                int pass = input.ViewType switch
                {
                    BatchCullingViewType.Picking          => (int)SceneViewPass.Picking,
                    BatchCullingViewType.SelectionOutline => (int)SceneViewPass.SelectionOutline,
                    _                                     => (int)SceneViewPass.Normal
                };

                cmd.SetComputeIntParam(cs, LocalNameID._EditorViewPass, pass);
                bool hasIncludedInstances = input.IncludedChunkCount > 0 && input.IncludedInstancesBuffer != null;
                cmd.SetComputeIntParam(cs, LocalNameID._IncludedChunkCount, hasIncludedInstances ? input.IncludedChunkCount : 0);
                cmd.SetComputeBufferParam(cs, kernel, LocalNameID._IncludedInstances, hasIncludedInstances ? input.IncludedInstancesBuffer : CoreUtils.emptyBuffer);
            }
#endif
        }

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        private static void ConfigureDebug(CommandBuffer cmd, ComputeShader cs, int kernel,
            in IndirectCullingParams input, LocalKeyword enableDebugKeyword)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            cmd.SetKeyword(cs, enableDebugKeyword, input.EnableDebug);
            if (input.EnableDebug)
            {
                cmd.SetComputeIntParam(cs, LocalNameID._DebugLODMode, (int)DebugDisplayFlora.Properties.LODMode);
                cmd.SetComputeIntParam(cs, LocalNameID._DebugLODIndex, DebugDisplayFlora.Properties.LODIndex);

                bool hasDispatchCounters = input.DebugDispatchCounterBuffer != null;
                cmd.SetComputeIntParam(cs, LocalNameID._DebugCounterEnabled, hasDispatchCounters ? 1 : 0);
                cmd.SetComputeBufferParam(cs, kernel, LocalNameID._DebugDispatchCounter, hasDispatchCounters ? input.DebugDispatchCounterBuffer : CoreUtils.emptyBuffer);

                bool hasInstanceDrawBuffer = input.DebugInstanceDrawBuffer != null;
                cmd.SetComputeIntParam(cs, LocalNameID._DebugShadingMode, hasInstanceDrawBuffer ? (int)DebugDisplayFlora.Properties.InstanceDrawMode : 0);
                cmd.SetComputeBufferParam(cs, kernel, LocalNameID._DebugDrawVisibility, hasInstanceDrawBuffer ? input.DebugInstanceDrawBuffer : CoreUtils.emptyBuffer);
            }
#endif
        }

        #endregion
    }
}
