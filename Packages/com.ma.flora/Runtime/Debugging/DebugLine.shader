// Copyright © Magnetic Arcade. All Rights Reserved.

Shader "Hidden/Flora/DebugLinesProcedural"
{
    Properties { }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Cull Off
        ZWrite Off
        ZTest LEqual
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            Name "Blocks"

            HLSLPROGRAM

            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag

            #include "UnityCG.cginc"
            #include "Packages/com.ma.flora/Runtime/Debugging/DebugCullingGrid.cs.hlsl"

            StructuredBuffer<DebugLineVertex> _LineVertices;

            struct Varying
            {
                float4 positionCS : SV_POSITION;
                float4 color : COLOR0;
            };

            Varying Vert(uint vertexID : SV_VertexID)
            {
                DebugLineVertex vertex = _LineVertices[vertexID];

                Varying output;
                output.positionCS = mul(UNITY_MATRIX_VP, float4(vertex.position.xyz, 1.0));
                output.color = vertex.color;
                return output;
            }

            float4 Frag(Varying input) : SV_Target
            {
                return input.color;
            }

            ENDHLSL
        }
    }
    Fallback Off
}
