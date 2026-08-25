// Copyright © Magnetic Arcade. All Rights Reserved.
// ReSharper disable InconsistentNaming

using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Rendering;

namespace MA.Flora
{
    [StructLayout(LayoutKind.Sequential)]
    [GenerateHLSL(needAccessors = false, generateCBuffer = true)]
    internal unsafe struct CullingViewShaderVariables
    {
        public const int MaxSplitsPerView  = 6;
        public const int MaxPlanesPerSplit = 5;
        public const int MaxPlanesPerView  = MaxPlanesPerSplit * MaxSplitsPerView;

        public const int ViewtypeCamera           = (int)BatchCullingViewType.Camera;
        public const int ViewtypeLight            = (int)BatchCullingViewType.Light;
        public const int ViewtypePicking          = (int)BatchCullingViewType.Picking;
        public const int ViewtypeSelectionOutline = (int)BatchCullingViewType.SelectionOutline;
        public const int ViewtypeFiltering        = (int)BatchCullingViewType.Filtering;

        [HLSLArray(MaxPlanesPerView, typeof(Vector4))]
        public fixed float _ViewFrustumPlanes[MaxPlanesPerView * 4];

        public Vector4 _ViewCameraPosition_ScreenMetric;
        public Vector4 _ViewAnimLodPositionPrev;
        public Vector4 _ViewAnimLodPositionCurr;

        public Vector4 _ViewCullingParams0;
        public Vector4 _ViewCullingParams1;

        public Vector4 _ViewVolumeParams0;
        public Vector4 _ViewVolumeParams1;
        public Vector4 _ViewVolumeParams2;
        public Vector4 _ViewVolumeParams3;
    }
}
