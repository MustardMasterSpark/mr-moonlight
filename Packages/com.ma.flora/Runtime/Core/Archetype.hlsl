// Copyright © Magnetic Arcade. All Rights Reserved.

#ifndef FLORA_ARCHETYPE_INCLUDED
#define FLORA_ARCHETYPE_INCLUDED

#include "Packages/com.ma.flora/Runtime/Core/Archetype.cs.hlsl"

StructuredBuffer<PackedArchetypeData> _ArchetypeData;

PackedArchetypeData LoadArchetypeData(uint typeIndex)
{
    return _ArchetypeData[typeIndex];
}

#endif // FLORA_ARCHETYPE_INCLUDED
