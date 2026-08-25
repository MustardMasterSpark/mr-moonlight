//
// This file was automatically generated. Please don't edit by hand. Execute Editor command [ Edit > Rendering > Generate Shader Includes ] instead
//

#ifndef CULLINGVIEWSHADERVARIABLES_CS_HLSL
#define CULLINGVIEWSHADERVARIABLES_CS_HLSL
//
// MA.Flora.CullingViewShaderVariables:  static fields
//
#define MAX_SPLITS_PER_VIEW (6)
#define MAX_PLANES_PER_SPLIT (5)
#define MAX_PLANES_PER_VIEW (30)
#define VIEWTYPE_CAMERA (1)
#define VIEWTYPE_LIGHT (2)
#define VIEWTYPE_PICKING (3)
#define VIEWTYPE_SELECTION_OUTLINE (4)
#define VIEWTYPE_FILTERING (5)

// Generated from MA.Flora.CullingViewShaderVariables
// PackingRules = Exact
CBUFFER_START(CullingViewShaderVariables)
    float4 _ViewFrustumPlanes[30];
    float4 _ViewCameraPosition_ScreenMetric;
    float4 _ViewAnimLodPositionPrev;
    float4 _ViewAnimLodPositionCurr;
    float4 _ViewCullingParams0;
    float4 _ViewCullingParams1;
    float4 _ViewVolumeParams0;
    float4 _ViewVolumeParams1;
    float4 _ViewVolumeParams2;
    float4 _ViewVolumeParams3;
CBUFFER_END


#endif
