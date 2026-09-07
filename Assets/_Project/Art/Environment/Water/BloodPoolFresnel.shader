// A transparent "pool of blood" surface for the MainMenu scene (post-MRM-18, Carlos's ask).
//
// Hand-authored rather than Shader Graph, matching the precedent set by Water.shader and
// FlareCore.shader in this folder — the whole effect is a single Fresnel term, a few lines of
// HLSL rather than a graph. Straight-down/near-normal viewing angles read as mostly transparent,
// letting the terrain underneath show through tinted blood-red; grazing/edge angles read as a
// near-opaque red surface, the way a shallow pool of liquid catches the eye at its edges. No
// camera-based reflection capture is used (Crest and the AkilliMum mirror asset were both tried
// and abandoned for this plane) — this is deliberately just alpha blending over whatever URP has
// already drawn opaque beneath it, so it needs nothing more than a quad with this material.
Shader "MrMoonlight/Environment/BloodPoolFresnel"
{
    Properties
    {
        [Header(Blood Pool)]
        _BloodColor ("Blood Color", Color) = (0.35, 0.01, 0.01, 1)
        _FresnelPower ("Fresnel Power", Range(0.25, 8)) = 3.0
        _MinAlpha ("Center Alpha (See-Through)", Range(0, 1)) = 0.12
        _MaxAlpha ("Edge Alpha (Blood Opaque)", Range(0, 1)) = 0.92
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Back

        Pass
        {
            Name "BloodPoolFresnel"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _BloodColor;
                float  _FresnelPower;
                float  _MinAlpha;
                float  _MaxAlpha;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half3 normalWS = normalize(input.normalWS);
                half3 viewDirWS = GetWorldSpaceNormalizeViewDir(input.positionWS);

                // 0 looking straight down the normal (see-through), 1 at grazing angles (opaque).
                half fresnel = pow(1.0 - saturate(dot(normalWS, viewDirWS)), _FresnelPower);
                half alpha = lerp(_MinAlpha, _MaxAlpha, fresnel);

                return half4(_BloodColor.rgb, alpha);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
