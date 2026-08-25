// Copyright © Magnetic Arcade. All Rights Reserved.

#include "Packages/com.ma.flora/Runtime/Culling/CullingViewShaderVariables.cs.hlsl"

#define _CameraPosition         _ViewCameraPosition_ScreenMetric.xyz
#define _ScreenRelativeMetricSq _ViewCameraPosition_ScreenMetric.w

#define _ViewAnimLodPosition0            _ViewAnimLodPositionPrev.xyz
#define _ViewAnimScreenRelativeMetricSq0 _ViewAnimLodPositionPrev.w

#define _ViewAnimLodPosition1            _ViewAnimLodPositionCurr.xyz
#define _ViewAnimScreenRelativeMetricSq1 _ViewAnimLodPositionCurr.w

#define _ViewSplitCount             asuint(_ViewCullingParams0.x)
#define _ViewMaxRenderDistance      _ViewCullingParams0.y
#define _MeshLodSelectionConstantSq _ViewCullingParams0.z
#define _ViewMinLOD                 asuint(_ViewCullingParams0.w)
#define _ViewType                   asuint(_ViewCullingParams1.x)

#define _MinScreenSize                 _ViewVolumeParams0.x
#define _MinScreenSizeAffectsLodGroups _ViewVolumeParams0.y
#define _CameraIsOrthographic          _ViewVolumeParams0.z
#define _ViewAnimLodAlpha              _ViewVolumeParams0.w

#define _CameraRandomizeLodTransition           _ViewVolumeParams1.x
#define _VolumeGlobalDensityLayerMask           asuint(_ViewVolumeParams1.y)
#define _VolumeGlobalDensity                    _ViewVolumeParams1.z
#define _VolumeGlobalDensityObjectSizeThreshold _ViewVolumeParams1.w
#define _VolumeGlobalDensityAffectsLodGroups    _ViewVolumeParams3.z

#define _VolumeRangeDensityLayerMask        asuint(_ViewVolumeParams2.x)
#define _VolumeRangeDensity                 _ViewVolumeParams2.y
#define _VolumeRangeDensityFalloffExp       _ViewVolumeParams2.z
#define _VolumeRangeDensityAffectsLodGroups _ViewVolumeParams2.w
#define _VolumeRangeDensityHeightMin        _ViewVolumeParams3.x
#define _VolumeRangeDensityHeightMax        _ViewVolumeParams3.y
