// Copyright © Magnetic Arcade. All Rights Reserved.
// ReSharper disable InconsistentNaming

using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace MA.Flora
{
    /// Packages/com.unity.render-pipelines.core/Runtime/GPUDriven/InstanceOcclusionCuller.cs
    internal struct OccluderDerivedData
    {
        public Matrix4x4 viewProjMatrix; // from view-centered world space
        public Vector4 viewOriginWorldSpace;
        public Vector4 radialDirWorldSpace;
        public Vector4 facingDirWorldSpace;

        public static OccluderDerivedData FromParameters(in OccluderSubviewUpdate occluderSubviewUpdate)
        {
            var origin = occluderSubviewUpdate.viewOffsetWorldSpace + (Vector3)occluderSubviewUpdate.invViewMatrix.GetColumn(3); // view origin in world space
            var xViewVec = (Vector3)occluderSubviewUpdate.invViewMatrix.GetColumn(0); // positive x axis in world space
            var yViewVec = (Vector3)occluderSubviewUpdate.invViewMatrix.GetColumn(1); // positive y axis in world space
            var towardsVec = (Vector3)occluderSubviewUpdate.invViewMatrix.GetColumn(2); // positive z axis in world space

            var viewMatrixNoTranslation = occluderSubviewUpdate.viewMatrix;
            viewMatrixNoTranslation.SetColumn(3, new Vector4(0.0f, 0.0f, 0.0f, 1.0f));

            return new OccluderDerivedData
            {
                viewOriginWorldSpace = origin,
                facingDirWorldSpace = towardsVec.normalized,
                radialDirWorldSpace = (xViewVec + yViewVec).normalized,
                viewProjMatrix = occluderSubviewUpdate.gpuProjMatrix * viewMatrixNoTranslation,
            };
        }
    }

    /// Packages/com.unity.render-pipelines.core/Runtime/GPUDriven/InstanceOcclusionCuller.cs
    internal struct InstanceOcclusionTestSubviewSettings
    {
        public int testCount;
        public int occluderSubviewIndices;
        public int occluderSubviewMask;
        public int cullingSplitIndices;
        public int cullingSplitMask;

        public static InstanceOcclusionTestSubviewSettings FromSpan(ReadOnlySpan<SubviewOcclusionTest> subviewOcclusionTests)
        {
            InstanceOcclusionTestSubviewSettings settings = new InstanceOcclusionTestSubviewSettings();
            for (int testIndex = 0; testIndex < subviewOcclusionTests.Length; ++testIndex)
            {
                SubviewOcclusionTest occlusionSubview = subviewOcclusionTests[testIndex];
                settings.occluderSubviewIndices |= occlusionSubview.occluderSubviewIndex << (4 * testIndex);
                settings.occluderSubviewMask |= 1 << occlusionSubview.occluderSubviewIndex;
                settings.cullingSplitIndices |= occlusionSubview.cullingSplitIndex << (4 * testIndex);
                settings.cullingSplitMask |= 1 << occlusionSubview.cullingSplitIndex;
            }
            settings.testCount = subviewOcclusionTests.Length;
            return settings;
        }
    }

    /// Packages/com.unity.render-pipelines.core/Runtime/GPUDriven/InstanceOcclusionCuller.cs
    internal struct OccluderMipBounds
    {
        public Vector2Int offset;
        public Vector2Int size;
    }

    /// Packages/com.unity.render-pipelines.core/Runtime/GPUDriven/OcclusionCullingCommon.cs
    internal enum OcclusionCullingCommonConfig
    {
        MaxOccluderMips = 8,
        MaxOccluderSilhouettePlanes = 6,
        MaxSubviewsPerView = 6,
        DebugPyramidOffset = 4, // TODO: rename
    }

    /// Packages/com.unity.render-pipelines.core/Runtime/GPUDriven/OcclusionCullingCommon.cs
    internal enum OcclusionTestDebugFlag
    {
        AlwaysPass = (1 << 0),
        CountVisible = (1 << 1),
    }

    /// Packages/com.unity.render-pipelines.core/Runtime/GPUDriven/OccluderDepthPyramidConstants.cs
    internal unsafe struct OccluderDepthPyramidConstants
    {
        [HLSLArray(OcclusionContext.MaxSubviewsPerView, typeof(Matrix4x4))]
        public fixed float _InvViewProjMatrix[OcclusionContext.MaxSubviewsPerView * 16];

        [HLSLArray(OcclusionContext.MaxSilhouettePlanes, typeof(Vector4))]
        public fixed float _SilhouettePlanes[OcclusionContext.MaxSilhouettePlanes * 4];

        [HLSLArray(OcclusionContext.MaxSubviewsPerView, typeof(ShaderGenUInt4))]
        public fixed uint _SrcOffset[OcclusionContext.MaxSubviewsPerView * 4];

        [HLSLArray(5, typeof(ShaderGenUInt4))]
        public fixed uint _MipOffsetAndSize[5 * 4];

        public uint _OccluderMipLayoutSizeX;
        public uint _OccluderMipLayoutSizeY;
        public uint _OccluderDepthPyramidPad0;
        public uint _OccluderDepthPyramidPad1;

        public uint _SrcSliceIndices; // packed 4 bits each
        public uint _DstSubviewIndices; // packed 4 bits each
        public uint _MipCount;
        public uint _SilhouettePlaneCount;
    }

    /// Packages/com.unity.render-pipelines.core/Runtime/GPUDriven/OcclusionCullingCommonShaderVariables.cs
    internal unsafe struct OcclusionCullingCommonShaderVariables
    {
        [HLSLArray(OcclusionContext.MaxOccluderMips, typeof(ShaderGenUInt4))]
        public fixed uint _OccluderMipBounds[OcclusionContext.MaxOccluderMips * 4];

        [HLSLArray(OcclusionContext.MaxSubviewsPerView, typeof(Matrix4x4))]
        public fixed float _ViewProjMatrix[OcclusionContext.MaxSubviewsPerView * 16]; // from view-centered world space

        [HLSLArray(OcclusionContext.MaxSubviewsPerView, typeof(Vector4))]
        public fixed float _ViewOriginWorldSpace[OcclusionContext.MaxSubviewsPerView * 4];

        [HLSLArray(OcclusionContext.MaxSubviewsPerView, typeof(Vector4))]
        public fixed float _FacingDirWorldSpace[OcclusionContext.MaxSubviewsPerView * 4];

        [HLSLArray(OcclusionContext.MaxSubviewsPerView, typeof(Vector4))]
        public fixed float _RadialDirWorldSpace[OcclusionContext.MaxSubviewsPerView * 4];

        public Vector4 _DepthSizeInOccluderPixels;
        public Vector4 _OccluderDepthPyramidSize;

        public uint _OccluderMipLayoutSizeX;
        public uint _OccluderMipLayoutSizeY;
        public uint _OcclusionTestDebugFlags;
        public uint _OcclusionCullingCommonPad0;

        public int _OcclusionTestCount;
        public int _OccluderSubviewIndices; // packed 4 bits each
        public int _CullingSplitIndices; // packed 4 bits each
        public int _CullingSplitMask; // only used for early out

        internal OcclusionCullingCommonShaderVariables(in OcclusionContext occlusionContext, in InstanceOcclusionTestSubviewSettings testSubviewSettings, bool occlusionOverlayCountVisible, bool overrideOcclusionTestToAlwaysPass)
        {
            for (int i = 0; i < occlusionContext.SubviewCount; ++i)
            {
                if (occlusionContext.IsSubviewValid(i))
                {
                    for (int j = 0; j < 16; ++j)
                        _ViewProjMatrix[16 * i + j] = occlusionContext.SubviewData[i].viewProjMatrix[j];

                    for (int j = 0; j < 4; ++j)
                    {
                        _ViewOriginWorldSpace[4 * i + j] = occlusionContext.SubviewData[i].viewOriginWorldSpace[j];
                        _FacingDirWorldSpace[4 * i + j] = occlusionContext.SubviewData[i].facingDirWorldSpace[j];
                        _RadialDirWorldSpace[4 * i + j] = occlusionContext.SubviewData[i].radialDirWorldSpace[j];
                    }
                }
            }
            _OccluderMipLayoutSizeX = (uint)occlusionContext.OccluderMipLayoutSize.x;
            _OccluderMipLayoutSizeY = (uint)occlusionContext.OccluderMipLayoutSize.y;
            _OcclusionTestDebugFlags
                = (overrideOcclusionTestToAlwaysPass ? (uint)OcclusionTestDebugFlag.AlwaysPass : 0)
                | (occlusionOverlayCountVisible ? (uint)OcclusionTestDebugFlag.CountVisible : 0);
            _OcclusionCullingCommonPad0 = 0;

            _OcclusionTestCount = testSubviewSettings.testCount;
            _OccluderSubviewIndices = testSubviewSettings.occluderSubviewIndices;
            _CullingSplitIndices = testSubviewSettings.cullingSplitIndices;
            _CullingSplitMask = testSubviewSettings.cullingSplitMask;

            _DepthSizeInOccluderPixels = occlusionContext.DepthBufferSizeInOccluderPixels;

            Vector2Int textureSize = occlusionContext.OccluderDepthPyramidSize;
            _OccluderDepthPyramidSize = new Vector4(textureSize.x, textureSize.y, 1.0f / textureSize.x, 1.0f / textureSize.y);

            for (int i = 0; i < occlusionContext.OccluderMipBounds.Length; ++i)
            {
                var mipBounds = occlusionContext.OccluderMipBounds[i];
                _OccluderMipBounds[4*i + 0] = (uint)mipBounds.offset.x;
                _OccluderMipBounds[4*i + 1] = (uint)mipBounds.offset.y;
                _OccluderMipBounds[4*i + 2] = (uint)mipBounds.size.x;
                _OccluderMipBounds[4*i + 3] = (uint)mipBounds.size.y;
            }
        }
    }

    /// Packages/com.unity.render-pipelines.core/Runtime/GPUDriven/OcclusionCullingDebugShaderVariables.cs
    internal unsafe struct OcclusionCullingDebugShaderVariables
    {
        public Vector4 _DepthSizeInOccluderPixels;

        [HLSLArray(OcclusionContext.MaxOccluderMips, typeof(ShaderGenUInt4))]
        public fixed uint _OccluderMipBounds[OcclusionContext.MaxOccluderMips * 4];

        public uint _OccluderMipLayoutSizeX;
        public uint _OccluderMipLayoutSizeY;
        public uint _OcclusionCullingDebugPad0;
        public uint _OcclusionCullingDebugPad1;
    }
}
