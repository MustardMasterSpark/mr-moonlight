// Copyright © Magnetic Arcade. All Rights Reserved.

#ifndef FLORA_INSTANCING_INCLUDED
#define FLORA_INSTANCING_INCLUDED

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/UnityDOTSInstancing.hlsl"

//-----------------------------------------------------------------------------
// DOTS Instancing
//-----------------------------------------------------------------------------

static float  flora_Sampled_RandomID;
static float4 flora_Sampled_VariationColor;

#ifdef UNITY_DOTS_INSTANCING_ENABLED
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Random.hlsl"

#if defined(UNIVERSAL_PIPELINE_CORE_INCLUDED)
#define FLORA_UNIVERSAL_PIPELINE
#elif defined(UNITY_MATERIAL_INCLUDED) //TODO: Find a better way to detect HDRP?
#define FLORA_HDRP_PIPELINE
#else
#define FLORA_CUSTOM_PIPELINE
#endif

UNITY_DOTS_INSTANCING_START(UserPropertyMetadata)
    UNITY_DOTS_INSTANCED_PROP_OVERRIDE_SUPPORTED(float,  flora_RandomID)
    UNITY_DOTS_INSTANCED_PROP_OVERRIDE_SUPPORTED(float4, flora_VariationColor)
UNITY_DOTS_INSTANCING_END(UserPropertyMetadata)

void SetupFloraDOTSMaterialPropertyCaches()
{
    flora_Sampled_RandomID       = UNITY_ACCESS_DOTS_INSTANCED_PROP(float, flora_RandomID);
    flora_Sampled_VariationColor = UNITY_ACCESS_DOTS_INSTANCED_PROP(float4, flora_VariationColor);
}

#undef UNITY_SETUP_DOTS_MATERIAL_PROPERTY_CACHES
#define UNITY_SETUP_DOTS_MATERIAL_PROPERTY_CACHES() SetupFloraDOTSMaterialPropertyCaches()
#endif // UNITY_DOTS_INSTANCING_ENABLED

//-----------------------------------------------------------------------------
// Debug Display
//-----------------------------------------------------------------------------

#include "Packages/com.ma.flora/ShaderLibrary/DebugDisplay.hlsl"

//-----------------------------------------------------------------------------
// Properties
//-----------------------------------------------------------------------------

// Returns the per-instance random value as at float in the [0,1] range .
float LoadFloraInstancedData_RandomID() { return flora_Sampled_RandomID; }

// Returns the per-instance variation color.
float4 LoadFloraInstancedData_VariationColor() { return flora_Sampled_VariationColor; }

//-----------------------------------------------------------------------------
// Obsolete
//-----------------------------------------------------------------------------

void SetupFloraInstancingData()
{
    // Legacy
}

void FloraInstancingSetup()
{
    // Legacy
}

#endif // FLORA_INSTANCING_INCLUDED
