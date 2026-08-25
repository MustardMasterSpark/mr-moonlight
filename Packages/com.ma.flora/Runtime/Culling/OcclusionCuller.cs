// Copyright © Magnetic Arcade. All Rights Reserved.

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace MA.Flora
{
    /// Packages/com.unity.render-pipelines.core@3a084e1eb46b/Runtime/GPUDriven/GPUResidentDrawTypes.cs
    /// Modified to support non-RenderGraph usage
    public struct OccluderParameters
    {
        public EntityId ViewId;
        public int SubviewCount;

        public RTHandle DepthTextureRT;
        public TextureHandle DepthTextureHandle;
        public Vector2Int DepthSize;
        public bool DepthIsArray;
        public RTHandle GetActiveDepthRT() => DepthTextureRT ?? DepthTextureHandle;

        public OccluderParameters(EntityId viewId)
        {
            ViewId = viewId;
            SubviewCount = 1;
            DepthTextureRT = null;
            DepthTextureHandle = TextureHandle.nullHandle;
            DepthSize = Vector2Int.zero;
            DepthIsArray = false;
        }
    }

    /// Packages/com.unity.render-pipelines.core@3a084e1eb46b/Runtime/GPUDriven/InstanceOcclusionCuller.cs
    /// Modified to support non-RenderGraph usage
    internal struct OccluderHandles
    {
        private RTHandle m_DepthPyramid;
        private GraphicsBuffer m_DebugOverlay;

        private bool m_ForRenderGraph;
        private TextureHandle m_DepthPyramidHandle;
        private BufferHandle m_DebugOverlayHandle;

        public void UseForOcclusionTest(IBaseRenderGraphBuilder builder)
        {
            builder.UseTexture(m_DepthPyramidHandle);
            if (m_DebugOverlayHandle.IsValid())
                builder.UseBuffer(m_DebugOverlayHandle, AccessFlags.ReadWrite);
        }

        public void UseForOccluderUpdate(IBaseRenderGraphBuilder builder)
        {
            builder.UseTexture(m_DepthPyramidHandle, AccessFlags.ReadWrite);
            if (m_DebugOverlayHandle.IsValid())
                builder.UseBuffer(m_DebugOverlayHandle, AccessFlags.ReadWrite);
        }

        public OccluderHandles(RenderGraph renderGraph, Vector2Int occluderDepthPyramidSize, RTHandle occluderDepthPyramid, GraphicsBuffer occlusionDebugOverlay)
        {
            m_ForRenderGraph = true;
            m_DepthPyramid = null;
            m_DebugOverlay = null;

            RenderTargetInfo rtInfo = new RenderTargetInfo
            {
                width = occluderDepthPyramidSize.x,
                height = occluderDepthPyramidSize.y,
                volumeDepth = 1,
                msaaSamples = 1,
                format = GraphicsFormat.R32_SFloat,
                bindMS = false,
            };

            m_DepthPyramidHandle = renderGraph.ImportTexture(occluderDepthPyramid, rtInfo);
            m_DebugOverlayHandle = occlusionDebugOverlay != null ? renderGraph.ImportBuffer(occlusionDebugOverlay) : BufferHandle.nullHandle;
        }

        public OccluderHandles(RTHandle depthPyramid, GraphicsBuffer debugOverlay)
        {
            m_ForRenderGraph = false;
            m_DepthPyramidHandle = TextureHandle.nullHandle;
            m_DebugOverlayHandle = BufferHandle.nullHandle;
            m_DepthPyramid = depthPyramid;
            m_DebugOverlay = debugOverlay;
        }

        public RTHandle GetDepthPyramid()
        {
            return m_ForRenderGraph ? m_DepthPyramidHandle : m_DepthPyramid;
        }

        public GraphicsBuffer GetDebugOverlay()
        {
            return m_ForRenderGraph ? m_DebugOverlayHandle : m_DebugOverlay;
        }

        public void DisableDebug()
        {
            m_DebugOverlayHandle = BufferHandle.nullHandle;
            m_DebugOverlay = null;
        }

        public bool IsValid()
        {
            if (m_ForRenderGraph)
                return m_DepthPyramidHandle.IsValid();

            return m_DepthPyramid != null;
        }

        public bool IsDebugValid()
        {
            if (m_ForRenderGraph)
                return m_DebugOverlayHandle.IsValid();

            return m_DebugOverlay != null;
        }
    }

    internal struct OcclusionDebugOutput
    {
        public RTHandle DepthPyramid;
        public GraphicsBuffer OcclusionDepthOverlay;
        public OcclusionCullingDebugShaderVariables Constants;
    }

    internal struct SilhouettePlaneCache : IDisposable
    {
        private struct Slot
        {
            public bool IsActive;
            public EntityId ViewEntityId;
            public int PlaneCount;  // planeIndex = slotIndex * kMaxSilhouettePlanes
            public int LastUsedFrameIndex;

            public Slot(EntityId viewEntityId, int planeCount, int frameIndex)
            {
                IsActive = true;
                ViewEntityId = viewEntityId;
                PlaneCount = planeCount;
                LastUsedFrameIndex = frameIndex;
            }
        }

        private const int MaxSilhouettePlanes = (int)OcclusionCullingCommonConfig.MaxOccluderSilhouettePlanes;
        private UnsafeParallelHashMap<EntityId, int> m_SubviewIDToIndexMap;
        private UnsafeList<int> m_SlotFreeList;
        private UnsafeList<Slot> m_Slots;
        private NativeList<Plane> m_PlaneStorage;

        public void Init()
        {
            m_SubviewIDToIndexMap = new UnsafeParallelHashMap<EntityId, int>(16, Allocator.Persistent);
            m_SlotFreeList = new UnsafeList<int>(16, Allocator.Persistent);
            m_Slots = new UnsafeList<Slot>(16, Allocator.Persistent);
            m_PlaneStorage = new NativeList<Plane>(16 * MaxSilhouettePlanes, Allocator.Persistent);
        }

        public void Dispose()
        {
            m_SubviewIDToIndexMap.Dispose();
            m_SlotFreeList.Dispose();
            m_Slots.Dispose();
            m_PlaneStorage.Dispose();
        }

        public void Update(EntityId viewEntityId, NativeArray<Plane> planes, int frameIndex)
        {
            var planeCount = math.min(planes.Length, MaxSilhouettePlanes);
            if (planeCount == 0)
                return;

            if (!m_SubviewIDToIndexMap.TryGetValue(viewEntityId, out int slotIndex))
            {
                if (m_SlotFreeList.Length > 0)
                {
                    // take a free slot from the free list
                    slotIndex = m_SlotFreeList[^1];
                    m_SlotFreeList.Length -= 1;
                }
                else
                {
                    // ensure we have capacity for a few more
                    if (m_Slots.Length == m_Slots.Capacity)
                    {
                        var newCapacity = m_Slots.Length + 8;
                        m_Slots.SetCapacity(newCapacity);
                        m_PlaneStorage.SetCapacity(newCapacity * MaxSilhouettePlanes);
                    }

                    // use the next slot in storage
                    slotIndex = m_Slots.Length;
                    var newSlotCount = slotIndex + 1;
                    m_Slots.Resize(newSlotCount);
                    m_PlaneStorage.ResizeUninitialized(newSlotCount * MaxSilhouettePlanes);
                }

                // associate with this view ID
                m_SubviewIDToIndexMap.Add(viewEntityId, slotIndex);
            }

            m_Slots[slotIndex] = new Slot(viewEntityId, planeCount, frameIndex);
            planes.CopyTo(m_PlaneStorage.AsArray().GetSubArray(slotIndex * MaxSilhouettePlanes, planeCount));
        }

        public void FreeUnusedSlots(int frameIndex, int maximumAge)
        {
            for (int slotIndex = 0; slotIndex < m_Slots.Length; ++slotIndex)
            {
                Slot slot = m_Slots[slotIndex];
                if (!slot.IsActive)
                    continue;

                if ((frameIndex - slot.LastUsedFrameIndex) > maximumAge)
                {
                    slot.IsActive = false;
                    m_Slots[slotIndex] = slot;
                    m_SubviewIDToIndexMap.Remove(slot.ViewEntityId);
                    m_SlotFreeList.Add(slotIndex);
                }
            }
        }

        public NativeArray<Plane> GetSubArray(EntityId viewEntityId)
        {
            int planeOffset = 0;
            int planeCount = 0;
            if (m_SubviewIDToIndexMap.TryGetValue(viewEntityId, out int slotIndex))
            {
                planeOffset = slotIndex * MaxSilhouettePlanes;
                planeCount = m_Slots[slotIndex].PlaneCount;
            }

            return m_PlaneStorage.AsArray().GetSubArray(planeOffset, planeCount);
        }
    }

    internal struct OcclusionContext : IDisposable
    {
        private static class LocalNameID
        {
            public static readonly int SrcDepth = Shader.PropertyToID("_SrcDepth");
            public static readonly int DstDepth = Shader.PropertyToID("_DstDepth");
            public static readonly int OccluderDepthPyramidConstants = Shader.PropertyToID("OccluderDepthPyramidConstants");
            public static readonly int OverlayOpacity = Shader.PropertyToID("_OverlayOpacity");
        }

        public const int FirstDepthMipIndex  = 3; // 8x8 tiles
        public const int MaxOccluderMips     = (int)OcclusionCullingCommonConfig.MaxOccluderMips;
        public const int MaxSilhouettePlanes = (int)OcclusionCullingCommonConfig.MaxOccluderSilhouettePlanes;
        public const int MaxSubviewsPerView  = (int)OcclusionCullingCommonConfig.MaxSubviewsPerView;

        public int Version;
        public Vector2Int DepthBufferSize;

        public NativeArray<OccluderDerivedData> SubviewData;
        public int SubviewCount => SubviewData.Length;
        public int SubviewValidMask;
        public bool IsSubviewValid(int subviewIndex) => subviewIndex < SubviewCount && (SubviewValidMask & (1 << subviewIndex)) != 0;

        public NativeArray<OccluderMipBounds> OccluderMipBounds;
        public Vector2Int OccluderMipLayoutSize; // total size of 2D layout specified by occluderMipBounds
        public Vector2Int OccluderDepthPyramidSize; // at least the size of N mip layouts tiled vertically (one per subview)

        public RTHandle OccluderDepthPyramid;
        public int OcclusionDebugOverlaySize;

        public GraphicsBuffer OcclusionDebugOverlay;
        public bool DebugNeedsClear;

        public GraphicsBuffer DepthPyramidConstantBuffer;
        public NativeArray<OccluderDepthPyramidConstants> DepthPyramidConstantBufferData;

        public Vector2 DepthBufferSizeInOccluderPixels
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                const float occluderPixelSize = (1 << FirstDepthMipIndex);
                return new Vector2(
                    DepthBufferSize.x / occluderPixelSize,
                    DepthBufferSize.y / occluderPixelSize);
            }
        }

        public bool IsCreated => SubviewData.IsCreated;
        public bool IsDebugValid => OcclusionDebugOverlaySize > 0;

        public void Dispose()
        {
            if (SubviewData.IsCreated)
                SubviewData.Dispose();

            if (OccluderMipBounds.IsCreated)
                OccluderMipBounds.Dispose();

            if (OccluderDepthPyramid != null)
            {
                OccluderDepthPyramid.Release();
                OccluderDepthPyramid = null;
            }

            if (OcclusionDebugOverlay != null)
            {
                OcclusionDebugOverlay.Release();
                OcclusionDebugOverlay = null;
            }

            if (DepthPyramidConstantBuffer != null)
            {
                DepthPyramidConstantBuffer.Release();
                DepthPyramidConstantBuffer = null;
            }

            if (DepthPyramidConstantBufferData.IsCreated)
                DepthPyramidConstantBufferData.Dispose();
        }

        private void UpdateMipBounds()
        {
            int occluderPixelSize = 1 << FirstDepthMipIndex;
            var topMipSize = (DepthBufferSize + (occluderPixelSize - 1) * Vector2Int.one) / occluderPixelSize;
            var totalSize = Vector2Int.zero;
            var mipOffset = Vector2Int.zero;
            var mipSize = topMipSize;

            if (!OccluderMipBounds.IsCreated)
                OccluderMipBounds = new NativeArray<OccluderMipBounds>(MaxOccluderMips, Allocator.Persistent);

            for (int mipIndex = 0; mipIndex < MaxOccluderMips; ++mipIndex)
            {
                OccluderMipBounds[mipIndex] = new OccluderMipBounds { offset = mipOffset, size = mipSize };

                totalSize.x = Mathf.Max(totalSize.x, mipOffset.x + mipSize.x);
                totalSize.y = Mathf.Max(totalSize.y, mipOffset.y + mipSize.y);

                if (mipIndex == 0)
                {
                    mipOffset.x = 0;
                    mipOffset.y += mipSize.y;
                }
                else
                {
                    mipOffset.x += mipSize.x;
                }
                mipSize.x = (mipSize.x + 1) / 2;
                mipSize.y = (mipSize.y + 1) / 2;
            }

            OccluderMipLayoutSize = totalSize;
        }

        private void AllocateTexturesIfNecessary(bool debugOverlayEnabled)
        {
            var minDepthPyramidSize = new Vector2Int(OccluderMipLayoutSize.x, OccluderMipLayoutSize.y * SubviewCount);
            if (OccluderDepthPyramidSize.x < minDepthPyramidSize.x || OccluderDepthPyramidSize.y < minDepthPyramidSize.y)
            {
                OccluderDepthPyramid?.Release();

                OccluderDepthPyramidSize = minDepthPyramidSize;
                OccluderDepthPyramid = RTHandles.Alloc(
                    OccluderDepthPyramidSize.x, OccluderDepthPyramidSize.y,
                    format: GraphicsFormat.R32_SFloat,
                    dimension: TextureDimension.Tex2D,
                    filterMode: FilterMode.Point,
                    wrapMode: TextureWrapMode.Clamp,
                    enableRandomWrite: true,
                    name: "Flora.OccluderDepths");
            }

            var newDebugOverlaySize = debugOverlayEnabled ? (minDepthPyramidSize.x * minDepthPyramidSize.y) : 0;
            if (OcclusionDebugOverlaySize < newDebugOverlaySize)
            {
                OcclusionDebugOverlay?.Dispose();
                OcclusionDebugOverlaySize = newDebugOverlaySize;
                DebugNeedsClear = true;

                // We use buffer instead of texture, because some platforms don't support atomic operations for Texture2D<uint>
                OcclusionDebugOverlay = new GraphicsBufferRef(
                    GraphicsBuffer.Target.Structured,
                    OcclusionDebugOverlaySize + (int)OcclusionCullingCommonConfig.DebugPyramidOffset,
                    sizeof(uint),
                    "Flora.OcclusionDebugOverlay");
            }

            if (newDebugOverlaySize == 0)
            {
                OcclusionDebugOverlay?.Dispose();
                OcclusionDebugOverlaySize = newDebugOverlaySize;
            }

            DepthPyramidConstantBuffer ??= new GraphicsBuffer(GraphicsBuffer.Target.Constant, 1, UnsafeUtility.SizeOf<OccluderDepthPyramidConstants>());
            if (!DepthPyramidConstantBufferData.IsCreated)
                DepthPyramidConstantBufferData = new NativeArray<OccluderDepthPyramidConstants>(1, Allocator.Persistent);
        }

        private OccluderDepthPyramidConstants SetupFarDepthPyramidConstants(ReadOnlySpan<OccluderSubviewUpdate> occluderSubviewUpdates, NativeArray<Plane> silhouettePlanes)
        {
            var cb = new OccluderDepthPyramidConstants();

            // write globals
            cb._OccluderMipLayoutSizeX = (uint)OccluderMipLayoutSize.x;
            cb._OccluderMipLayoutSizeY = (uint)OccluderMipLayoutSize.y;

            // write per-subview data
            int updateCount = occluderSubviewUpdates.Length;
            for (int updateIndex = 0; updateIndex < updateCount; ++updateIndex)
            {
                ref readonly OccluderSubviewUpdate update = ref occluderSubviewUpdates[updateIndex];

                int subviewIndex = update.subviewIndex;
                SubviewData[subviewIndex] = OccluderDerivedData.FromParameters(update);
                SubviewValidMask |= 1 << update.subviewIndex;

                var viewProjMatrix
                    = update.gpuProjMatrix
                    * update.viewMatrix
                    * Matrix4x4.Translate(-update.viewOffsetWorldSpace);
                var invViewProjMatrix = viewProjMatrix.inverse;

                unsafe
                {
                    for (int j = 0; j < 16; ++j)
                        cb._InvViewProjMatrix[16 * updateIndex + j] = invViewProjMatrix[j];

                    cb._SrcOffset[4 * updateIndex + 0] = (uint)update.depthOffset.x;
                    cb._SrcOffset[4 * updateIndex + 1] = (uint)update.depthOffset.y;
                    cb._SrcOffset[4 * updateIndex + 2] = 0;
                    cb._SrcOffset[4 * updateIndex + 3] = 0;
                }

                cb._SrcSliceIndices |= (((uint)update.depthSliceIndex & 0xf) << (4 * updateIndex));
                cb._DstSubviewIndices |= ((uint)subviewIndex << (4 * updateIndex));
            }

            // TODO: transform these planes from world space into NDC space planes
            for (int i = 0; i < MaxSilhouettePlanes; ++i)
            {
                var plane = new Plane(Vector3.zero, 0.0f);
                if (i < silhouettePlanes.Length)
                    plane = silhouettePlanes[i];
                unsafe
                {
                    cb._SilhouettePlanes[4 * i + 0] = plane.normal.x;
                    cb._SilhouettePlanes[4 * i + 1] = plane.normal.y;
                    cb._SilhouettePlanes[4 * i + 2] = plane.normal.z;
                    cb._SilhouettePlanes[4 * i + 3] = plane.distance;
                }
            }
            cb._SilhouettePlaneCount = (uint)silhouettePlanes.Length;

            return cb;
        }

        public void CreateDepthPyramid(
            CommandBuffer cmd, in OccluderParameters occluderParams, ReadOnlySpan<OccluderSubviewUpdate> occluderSubviewUpdates, in OccluderHandles occluderHandles,
            NativeArray<Plane> silhouettePlanes, ComputeShader occluderDepthPyramidCS, int occluderDepthDownscaleKernel)
        {
            var cb = SetupFarDepthPyramidConstants(occluderSubviewUpdates, silhouettePlanes);

            var c = occluderDepthPyramidCS;
            var k = occluderDepthDownscaleKernel;

            var srcKeyword = new LocalKeyword(c, "USE_SRC");
            var srcIsArrayKeyword = new LocalKeyword(c, "SRC_IS_ARRAY");
            var srcIsMsaaKeyword = new LocalKeyword(c, "SRC_IS_MSAA");

            var depthPyramid = occluderHandles.GetDepthPyramid();
            var depthTexture = occluderParams.GetActiveDepthRT();
            var srcIsArray = occluderParams.DepthIsArray;
            var srcIsMsaa = depthTexture?.isMSAAEnabled ?? false;

            int mipCount = FirstDepthMipIndex + MaxOccluderMips;
            for (int mipIndexBase = 0; mipIndexBase < mipCount - 1; mipIndexBase += 4)
            {
                cmd.SetComputeTextureParam(c, k, LocalNameID.DstDepth, depthPyramid);

                var useSrc = (mipIndexBase == 0);
                cmd.SetKeyword(c, srcKeyword, useSrc);
                cmd.SetKeyword(c, srcIsArrayKeyword, useSrc && srcIsArray);
                cmd.SetKeyword(c, srcIsMsaaKeyword, useSrc && srcIsMsaa);
                if (useSrc)
                    cmd.SetComputeTextureParam(c, k, LocalNameID.SrcDepth, depthTexture);

                cb._MipCount = (uint)Math.Min(mipCount - 1 - mipIndexBase, 4);

                Vector2Int srcSize = Vector2Int.zero;
                for (int i = 0; i < 5; ++i)
                {
                    Vector2Int offset = Vector2Int.zero;
                    Vector2Int size = Vector2Int.zero;
                    int mipIndex = mipIndexBase + i;
                    if (mipIndex == 0)
                    {
                        size = occluderParams.DepthSize;
                    }
                    else
                    {
                        int occMipIndex = mipIndex - FirstDepthMipIndex;
                        if (0 <= occMipIndex && occMipIndex < MaxOccluderMips)
                        {
                            offset = OccluderMipBounds[occMipIndex].offset;
                            size = OccluderMipBounds[occMipIndex].size;
                        }
                    }
                    if (i == 0)
                        srcSize = size;
                    unsafe
                    {
                        cb._MipOffsetAndSize[4 * i + 0] = (uint)offset.x;
                        cb._MipOffsetAndSize[4 * i + 1] = (uint)offset.y;
                        cb._MipOffsetAndSize[4 * i + 2] = (uint)size.x;
                        cb._MipOffsetAndSize[4 * i + 3] = (uint)size.y;
                    }
                }

                DepthPyramidConstantBufferData[0] = cb;
                cmd.SetBufferData(DepthPyramidConstantBuffer, DepthPyramidConstantBufferData);
                cmd.SetComputeConstantBufferParam(c, LocalNameID.OccluderDepthPyramidConstants, DepthPyramidConstantBuffer, 0, DepthPyramidConstantBuffer.stride);

                cmd.DispatchCompute(c, k, (srcSize.x + 15) / 16, (srcSize.y + 15) / 16, occluderSubviewUpdates.Length);
            }
        }

        // --- Debug Overlay / CommandBuffer API ---

        public void NextFrame()
        {
            DebugNeedsClear = true;
            OcclusionMaterials.DebugTestMaterial.SetFloat(LocalNameID.OverlayOpacity, DebugDisplayFlora.Properties.OcclusionTestOverlayOpacity);
        }

        public OccluderHandles Import(RenderGraph renderGraph) => new OccluderHandles(renderGraph, OccluderDepthPyramidSize, OccluderDepthPyramid, OcclusionDebugOverlay);
        public OccluderHandles Import() => new OccluderHandles(OccluderDepthPyramid, OcclusionDebugOverlay);

        public void PrepareOccluders(in OccluderParameters occluderParams)
        {
            if (SubviewCount != occluderParams.SubviewCount)
            {
                if (SubviewData.IsCreated)
                    SubviewData.Dispose();

                SubviewData = new NativeArray<OccluderDerivedData>(occluderParams.SubviewCount, Allocator.Persistent);
                SubviewValidMask = 0;
            }
            DepthBufferSize = occluderParams.DepthSize;

            // enable debug counters for cameras when the overlay is enabled
            var debugOverlayEnabled = DebugDisplayFlora.Active && DebugDisplayFlora.Properties.OcclusionTestOverlayEnabled;
            UpdateMipBounds();
            AllocateTexturesIfNecessary(debugOverlayEnabled);
        }

        public OcclusionDebugOutput GetDebugOutput()
        {
            var debugOutput = new OcclusionDebugOutput
            {
                DepthPyramid = OccluderDepthPyramid,
                OcclusionDepthOverlay = OcclusionDebugOverlay,
            };

            debugOutput.Constants._DepthSizeInOccluderPixels = DepthBufferSizeInOccluderPixels;
            debugOutput.Constants._OccluderMipLayoutSizeX = (uint)OccluderMipLayoutSize.x;
            debugOutput.Constants._OccluderMipLayoutSizeY = (uint)OccluderMipLayoutSize.y;

            for (int i = 0; i < OccluderMipBounds.Length; ++i)
            {
                var mipBounds = OccluderMipBounds[i];
                unsafe
                {
                    debugOutput.Constants._OccluderMipBounds[4 * i + 0] = (uint)mipBounds.offset.x;
                    debugOutput.Constants._OccluderMipBounds[4 * i + 1] = (uint)mipBounds.offset.y;
                    debugOutput.Constants._OccluderMipBounds[4 * i + 2] = (uint)mipBounds.size.x;
                    debugOutput.Constants._OccluderMipBounds[4 * i + 3] = (uint)mipBounds.size.y;
                }
            }

            return debugOutput;
        }
    }

    internal static class OcclusionNameID
    {
        public static readonly int OcclusionCullingCommonShaderVariables = Shader.PropertyToID("OcclusionCullingCommonShaderVariables");
        public static readonly int OcclusionCullingDebugShaderVariables = Shader.PropertyToID("OcclusionCullingDebugShaderVariables");

        public static readonly int OccluderDepthPyramid = Shader.PropertyToID("_OccluderDepthPyramid");
        public static readonly int OcclusionDebugOverlay = Shader.PropertyToID("_OcclusionDebugOverlay");
        public static readonly int OccluderTexture = Shader.PropertyToID("_OccluderTexture");
        public static readonly int ValidRange = Shader.PropertyToID("_ValidRange");
    }

    internal static class OcclusionMaterials
    {
        public static Material DebugTestMaterial;
        public static Material DebugViewMaterial;

        public static void Initialize(FloraRuntimeResources runtimeResources)
        {
            DebugTestMaterial = CoreUtils.CreateEngineMaterial(runtimeResources.DebugOcclusionTestShader);
            DebugViewMaterial = CoreUtils.CreateEngineMaterial(runtimeResources.DebugOccluderShader);
        }
    }

    internal static class OcclusionComputeShaders
    {
        public static ComputeShader DebugOcclusionCS;
        public static int DebugOcclusionClearKernel;

        public static ComputeShader BuildOcclusionDepthCS;
        public static int BuildOcclusionDepthDownscaleKernel;

        public static void Initialize(FloraRuntimeResources runtimeResources)
        {
            DebugOcclusionCS = runtimeResources.DebugOcclusionCS;
            DebugOcclusionClearKernel = DebugOcclusionCS.FindKernel("ClearOcclusionDebug");

            BuildOcclusionDepthCS = runtimeResources.OccluderDepthPyramidKernelsCS;
            BuildOcclusionDepthDownscaleKernel = BuildOcclusionDepthCS.FindKernel("OccluderDepthDownscale");
        }
    }

    internal static class OcclusionSamplers
    {
        public static ProfilingSampler UpdateOccluders;
        public static ProfilingSampler OcclusionTestOverlay;
        public static ProfilingSampler OccluderOverlay;

        public static void Initialize()
        {
            UpdateOccluders = new ProfilingSampler("Flora.UpdateOccluders");
            OcclusionTestOverlay = new ProfilingSampler("Flora.OcclusionTestOverlay");
            OccluderOverlay = new ProfilingSampler("Flora.OccluderOverlay");
        }
    }

    internal sealed class OcclusionCuller : IDisposable
    {
        public const int MaxUnusedFrames = 8;

        public struct ContextSlot
        {
            public bool Valid;
            public int LastUsedFrameIndex;
            public EntityId ViewId;
        }

        public SilhouettePlaneCache SilhouettePlaneCache;
        public NativeParallelHashMap<EntityId, int> ViewIDToSlotMap;
        public List<OcclusionContext> Contexts;
        public UnsafeList<ContextSlot> ContextSlots;
        public UnsafeList<int> FreeContextSlots;

        public ConstantBufferRef<OcclusionCullingCommonShaderVariables> CommonConstantBuffer;
        public ConstantBufferRef<OcclusionCullingDebugShaderVariables> DebugConstantBuffer;

        public int FrameIndex;

        public OcclusionCuller(FloraRuntimeResources runtimeResources)
        {
            OcclusionMaterials.Initialize(runtimeResources);
            OcclusionComputeShaders.Initialize(runtimeResources);
            OcclusionSamplers.Initialize();

            SilhouettePlaneCache.Init();

            ViewIDToSlotMap = new NativeParallelHashMap<EntityId, int>(16, Allocator.Persistent);
            Contexts = new List<OcclusionContext>(16);
            ContextSlots = new UnsafeList<ContextSlot>(16, Allocator.Persistent);
            FreeContextSlots = new UnsafeList<int>(16, Allocator.Persistent);

            CommonConstantBuffer = new ConstantBufferRef<OcclusionCullingCommonShaderVariables>("Flora.OcclusionCullingCommonShaderVariables");
            DebugConstantBuffer = new ConstantBufferRef<OcclusionCullingDebugShaderVariables>("Flora.OcclusionCullingDebugShaderVariables");

            FrameIndex = 0;
        }

        public void Dispose()
        {
            for (int i = 0; i < Contexts.Count; ++i)
            {
                if (ContextSlots[i].Valid)
                    Contexts[i].Dispose();
            }

            SilhouettePlaneCache.Dispose();

            ViewIDToSlotMap.Dispose();
            ContextSlots.Dispose();
            FreeContextSlots.Dispose();

            CommonConstantBuffer.Dispose();
            DebugConstantBuffer.Dispose();
        }

        private int NewContext(EntityId viewId)
        {
            int newSlot;
            var newCtxSlot = new ContextSlot { Valid = true, ViewId = viewId, LastUsedFrameIndex = FrameIndex };
            var newCtx = new OcclusionContext();
            if (FreeContextSlots.Length > 0)
            {
                newSlot = FreeContextSlots[^1];
                FreeContextSlots.RemoveAt(FreeContextSlots.Length - 1);
                Contexts[newSlot] = newCtx;
                ContextSlots[newSlot] = newCtxSlot;
            }
            else
            {
                newSlot = Contexts.Count;
                Contexts.Add(newCtx);
                ContextSlots.Add(newCtxSlot);
            }

            ViewIDToSlotMap.Add(viewId, newSlot);
            return newSlot;
        }

        private void DeleteContext(EntityId viewId)
        {
            if (!ViewIDToSlotMap.TryGetValue(viewId, out int contextIndex) || !ContextSlots[contextIndex].Valid)
                return;

            Contexts[contextIndex].Dispose();
            ContextSlots[contextIndex] = new ContextSlot { Valid = false };
            FreeContextSlots.Add(contextIndex);
            ViewIDToSlotMap.Remove(viewId);
        }

        public bool TryGetContext(EntityId viewId, out OcclusionContext occlusionContext)
        {
            if (ViewIDToSlotMap.TryGetValue(viewId, out int contextIndex) && ContextSlots[contextIndex].Valid)
            {
                occlusionContext = Contexts[contextIndex];
                return true;
            }

            occlusionContext = default;
            return false;
        }

        public OccluderHandles PrepareOcclusionHandles(in OccluderParameters input)
        {
            OccluderHandles occluderHandles = new OccluderHandles();
            if (input.DepthTextureRT != null)
            {
                if (!ViewIDToSlotMap.TryGetValue(input.ViewId, out int contextIndex))
                    contextIndex = NewContext(input.ViewId);

                OcclusionContext ctx = Contexts[contextIndex];
                ctx.PrepareOccluders(input);
                occluderHandles = ctx.Import();
                Contexts[contextIndex] = ctx;
            }
            else
            {
                DeleteContext(input.ViewId);
            }

            return occluderHandles;
        }

        public OccluderHandles PrepareOcclusionHandles(RenderGraph renderGraph, in OccluderParameters input)
        {
            OccluderHandles occluderHandles = new OccluderHandles();
            if (input.DepthTextureHandle.IsValid())
            {
                if (!ViewIDToSlotMap.TryGetValue(input.ViewId, out var contextIndex))
                    contextIndex = NewContext(input.ViewId);

                OcclusionContext ctx = Contexts[contextIndex];
                ctx.PrepareOccluders(input);
                occluderHandles = ctx.Import(renderGraph);
                Contexts[contextIndex] = ctx;
            }
            else
            {
                DeleteContext(input.ViewId);
            }
            return occluderHandles;
        }

        public void NextFrame()
        {
            for (int i = 0; i < Contexts.Count; ++i)
            {
                if (!ContextSlots[i].Valid)
                    continue;

                OcclusionContext occlusionContext = Contexts[i];
                ContextSlot slot = ContextSlots[i];

                if ((FrameIndex - slot.LastUsedFrameIndex) >= MaxUnusedFrames)
                {
                    DeleteContext(slot.ViewId);
                    continue;
                }

                occlusionContext.NextFrame();
                Contexts[i] = occlusionContext;
            }

            SilhouettePlaneCache.FreeUnusedSlots(FrameIndex, MaxUnusedFrames);
            FrameIndex++;
        }

        public OcclusionDebugOutput GetOcclusionTestDebugOutput(EntityId viewId)
        {
            if (ViewIDToSlotMap.TryGetValue(viewId, out int contextIndex) && ContextSlots[contextIndex].Valid)
                return Contexts[contextIndex].GetDebugOutput();

            return new OcclusionDebugOutput();
        }

        public void UpdateSilhouettePlanes(EntityId viewId, NativeArray<Plane> planes)
        {
            SilhouettePlaneCache.Update(viewId, planes, FrameIndex);
        }

        public void CreateDepthPyramid(CommandBuffer cmd, OccluderParameters input, ReadOnlySpan<OccluderSubviewUpdate> subviewUpdates, in OccluderHandles handles)
        {
            if (!ViewIDToSlotMap.TryGetValue(input.ViewId, out int contextIndex))
                return;

            var silhouettePlanes = SilhouettePlaneCache.GetSubArray(input.ViewId);

            OcclusionContext ctx = Contexts[contextIndex];
            ctx.CreateDepthPyramid(cmd, input, subviewUpdates, handles, silhouettePlanes, OcclusionComputeShaders.BuildOcclusionDepthCS, OcclusionComputeShaders.BuildOcclusionDepthDownscaleKernel);
            ctx.Version++;
            Contexts[contextIndex] = ctx;

            ContextSlot slot = ContextSlots[contextIndex];
            slot.LastUsedFrameIndex = FrameIndex;
            ContextSlots[contextIndex] = slot;
        }

        public void PrepareForCulling(CommandBuffer cmd, in OcclusionContext occlusionContext, in OcclusionCullingSettings settings, in InstanceOcclusionTestSubviewSettings testSubviewSettings, Span<ComputeShader> shaders)
        {
            bool debugOverlayCountVisible = false;
            bool overrideOcclusionTestToAlwaysPass = false;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (DebugDisplayFlora.Active)
            {
                debugOverlayCountVisible = DebugDisplayFlora.Properties.OcclusionOverlayCountVisible;
                overrideOcclusionTestToAlwaysPass = DebugDisplayFlora.Properties.OcclusionOverrideTestToAlwaysPass;
            }
#endif

            CommonConstantBuffer.Value = new OcclusionCullingCommonShaderVariables(in occlusionContext, testSubviewSettings, debugOverlayCountVisible, overrideOcclusionTestToAlwaysPass);
            cmd.SetBufferData(CommonConstantBuffer.Buffer, CommonConstantBuffer.Data);

            foreach (var cs in shaders)
            {
                Assert.IsNotNull(cs, "Failed to find occlusion compute shader.");
                cmd.SetComputeConstantBufferParam(cs, OcclusionNameID.OcclusionCullingCommonShaderVariables, CommonConstantBuffer);
            }

            DispatchDebugClear(cmd, settings.viewInstanceID);
        }

        public bool BuildOcclusionDepth(CommandBuffer cmd, in OccluderParameters input, ReadOnlySpan<OccluderSubviewUpdate> subviewUpdates)
        {
            OccluderHandles occluderHandles = PrepareOcclusionHandles(input);
            if (!occluderHandles.IsValid())
                return false;

            int subviewMask = 0;
            for (int i = 0; i < subviewUpdates.Length; ++i)
                subviewMask |= 1 << subviewUpdates[i].subviewIndex;

            CreateDepthPyramid(cmd, input, subviewUpdates, in occluderHandles);
            // batcher.instanceCullingBatcher.InstanceOccludersUpdated(data.occluderParams.viewInstanceID, subviewMask);

            return true;
        }

        public void DispatchDebugClear(CommandBuffer cmd, EntityId viewEntityId)
        {
            if (!ViewIDToSlotMap.TryGetValue(viewEntityId, out int contextIndex))
                return;

            OcclusionContext occlusionContext = Contexts[contextIndex];
            if (occlusionContext is { IsDebugValid: true, DebugNeedsClear: true })
            {
                var cs = OcclusionComputeShaders.DebugOcclusionCS;
                var kernel = OcclusionComputeShaders.DebugOcclusionClearKernel;

                cmd.SetComputeConstantBufferParam(cs, OcclusionNameID.OcclusionCullingCommonShaderVariables, CommonConstantBuffer, 0, CommonConstantBuffer.Stride);
                cmd.SetComputeBufferParam(cs, kernel, OcclusionNameID.OcclusionDebugOverlay, occlusionContext.OcclusionDebugOverlay);

                Vector2Int mip0Size = occlusionContext.OccluderMipBounds[0].size;
                cmd.DispatchCompute(cs, kernel, (mip0Size.x + 7) / 8, (mip0Size.y + 7) / 8, occlusionContext.SubviewCount);

                occlusionContext.DebugNeedsClear = false;
                Contexts[contextIndex] = occlusionContext;
            }
        }

        public void RenderDebugTestOverlay(CommandBuffer cmd, EntityId viewId)
        {
            if (!DebugDisplayFlora.Active || !DebugDisplayFlora.Properties.OcclusionTestOverlayEnabled)
                return;

            var debugOutput = GetOcclusionTestDebugOutput(viewId);
            if (debugOutput.OcclusionDepthOverlay == null)
                return;

            DebugConstantBuffer.Value = debugOutput.Constants;
            DebugConstantBuffer.UpdateData(cmd);

            OcclusionMaterials.DebugTestMaterial.SetConstantBuffer(OcclusionNameID.OcclusionCullingDebugShaderVariables, DebugConstantBuffer, 0, DebugConstantBuffer.Stride);
            cmd.SetGlobalBuffer(OcclusionNameID.OcclusionDebugOverlay, debugOutput.OcclusionDepthOverlay);
            CoreUtils.DrawFullScreen(cmd, OcclusionMaterials.DebugTestMaterial);
        }

        public void RenderDebugViewOverlay(CommandBuffer cmd, EntityId viewId, Vector2 positionScreen, float maxHeight)
        {
            if (!DebugDisplayFlora.Active || !DebugDisplayFlora.Properties.OccluderDepthOverlayEnabled)
                return;

            var occluderTexture = GetOcclusionTestDebugOutput(viewId).DepthPyramid;
            if (occluderTexture == null)
                return;

            Material debugMaterial = OcclusionMaterials.DebugViewMaterial;
            int passIndex = debugMaterial.FindPass("DebugOccluder");

            Vector2 outputSize = occluderTexture.referenceSize;
            var scaleFactor = maxHeight / outputSize.y;
            outputSize *= scaleFactor;
            Rect viewport = new Rect(positionScreen.x, positionScreen.y, outputSize.x, outputSize.y);

            using var _ = MaterialPropertyBlockPool.Get(out var mpb);
            mpb.SetTexture(OcclusionNameID.OccluderTexture, occluderTexture);
            mpb.SetVector(OcclusionNameID.ValidRange, DebugDisplayFlora.Properties.OcclusionDepthViewRange);

            cmd.SetViewport(viewport);
            cmd.DrawProcedural(Matrix4x4.identity, debugMaterial, passIndex, MeshTopology.Triangles, 3, 1, mpb);
        }
    }
}
