//
// This file was automatically generated. Please don't edit by hand. Execute Editor command [ Edit > Rendering > Generate Shader Includes ] instead
//

#ifndef INSTANCEBUFFER_CS_HLSL
#define INSTANCEBUFFER_CS_HLSL
// Generated from MA.Flora.BatchCullingAddresses
// PackingRules = Exact
struct BatchCullingAddresses
{
    uint localToWorld;
    uint randomID;
    uint unused0;
    uint unused1;
};

// Generated from MA.Flora.BatchTransformAddresses
// PackingRules = Exact
struct BatchTransformAddresses
{
    uint localToWorld;
    uint worldToLocal;
    uint prevLocalToWorld;
    uint prevWorldToLocal;
};


#endif
