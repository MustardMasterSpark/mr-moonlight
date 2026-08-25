// Copyright © Magnetic Arcade. All Rights Reserved.

#ifndef FLORA_INDIRECT_CULLING_PASS_INCLUDED
#define FLORA_INDIRECT_CULLING_PASS_INCLUDED

#include "Packages/com.ma.flora/Runtime/Culling/IndirectCullingPass.cs.hlsl"

//--------------------------------------------------------------------------------------------------
// Draw Info
//--------------------------------------------------------------------------------------------------

uint _DrawCount;
StructuredBuffer<IndirectDrawInfo> _DrawInfos;

//--------------------------------------------------------------------------------------------------
// Draw Bins
//--------------------------------------------------------------------------------------------------

uint _DrawBinCount;

#ifdef DRAW_BIN_WRITE
RWStructuredBuffer<IndirectDrawBin> _DrawBins;
#else
StructuredBuffer<IndirectDrawBin> _DrawBins;
#endif

IndirectDrawBin LoadDrawBin(uint index)
{
    return _DrawBins[index];
}

#ifdef DRAW_BIN_WRITE
uint AddInstancesToDrawBin(uint binIndex, uint instanceCount)
{
    uint indexInBin;
    InterlockedAdd(_DrawBins[binIndex].visibleCount, instanceCount, indexInBin);
    return indexInBin;
}
#endif

#endif // FLORA_INDIRECT_INCLUDED
