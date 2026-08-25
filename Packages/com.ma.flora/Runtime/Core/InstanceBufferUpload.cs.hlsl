//
// This file was automatically generated. Please don't edit by hand. Execute Editor command [ Edit > Rendering > Generate Shader Includes ] instead
//

#ifndef INSTANCEBUFFERUPLOAD_CS_HLSL
#define INSTANCEBUFFERUPLOAD_CS_HLSL
// Generated from MA.Flora.BufferCopyCommand
// PackingRules = Exact
struct BufferCopyCommand
{
    uint srcAddress;
    uint dstAddress;
    uint stride;
    uint count;
};

// Generated from MA.Flora.PackedChunkUploadHeader
// PackingRules = Exact
struct PackedChunkUploadHeader
{
    uint batchDomainIndex;
    uint packedStartCount;
};

// Generated from MA.Flora.SHUpdatePacket
// PackingRules = Exact
struct SHUpdatePacket
{
    float shr0;
    float shr1;
    float shr2;
    float shr3;
    float shr4;
    float shr5;
    float shr6;
    float shr7;
    float shr8;
    float shg0;
    float shg1;
    float shg2;
    float shg3;
    float shg4;
    float shg5;
    float shg6;
    float shg7;
    float shg8;
    float shb0;
    float shb1;
    float shb2;
    float shb3;
    float shb4;
    float shb5;
    float shb6;
    float shb7;
    float shb8;
};


#endif
