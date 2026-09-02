// The burning core of a signal flare (MRM-34).
//
// Hand-authored rather than Shader Graph, matching the precedent already set by
// Art/Environment/Water/Water.shader. Carlos's stated preference was Shader Graph; the look here
// is a handful of radial gradients and a time-driven flicker, which is a dozen lines of HLSL and
// a forty-node graph. If it ever needs visual tweaking by hand this converts to a graph in an
// afternoon — the maths below is the spec for it.
//
// The mesh is expected to be a unit quad. It billboards toward the camera in the vertex stage, so
// nothing on the CPU has to orient it and a flare tumbling through the air still reads as a
// sphere of light.

Shader "MrMoonlight/VFX/FlareCore"
{
    Properties
    {
        [Header(Colour)]
        _BaseColor ("Core Tint", Color) = (1.0, 0.35, 0.12, 1)
        _HotColor ("White Hot Centre", Color) = (1.0, 0.95, 0.85, 1)
        _Intensity ("Overall Intensity", Range(0, 12)) = 4.5

        [Header(Shape)]
        _CoreSize ("White Hot Core Size", Range(0.01, 0.5)) = 0.10
        _GlowFalloff ("Glow Falloff Power", Range(0.5, 8)) = 2.6
        _HaloStrength ("Outer Halo Strength", Range(0, 1)) = 0.45

        [Header(Flicker)]
        // Flicker here is cosmetic scale/brightness jitter on the sprite only. The gameplay-visible
        // flicker is on the real Light and is driven from FlareProjectile.cs, because the light is
        // what actually changes how the forest is lit.
        _FlickerSpeed ("Flicker Speed", Range(0, 40)) = 18
        _FlickerAmount ("Flicker Amount", Range(0, 0.6)) = 0.22
        _PulseSpeed ("Size Pulse Speed", Range(0, 20)) = 7
        _PulseAmount ("Size Pulse Amount", Range(0, 0.5)) = 0.10
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

        // Additive, depth-tested but not depth-written: a flare should brighten what is behind it
        // and never occlude another flare drawn after it.
        Blend SrcAlpha One
        ZWrite Off
        Cull Off

        Pass
        {
            Name "FlareCore"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float  flicker    : TEXCOORD1;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _HotColor;
                float  _Intensity;
                float  _CoreSize;
                float  _GlowFalloff;
                float  _HaloStrength;
                float  _FlickerSpeed;
                float  _FlickerAmount;
                float  _PulseSpeed;
                float  _PulseAmount;
            CBUFFER_END

            // Cheap value noise. Deliberately not a texture lookup — this runs on a handful of
            // quads and a sampler would be the most expensive thing in the shader.
            float Hash11(float n)
            {
                return frac(sin(n) * 43758.5453123);
            }

            float ValueNoise(float x)
            {
                float i = floor(x);
                float f = frac(x);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(Hash11(i), Hash11(i + 1.0), f);
            }

            Varyings vert(Attributes input)
            {
                Varyings output;

                // Two independent time signals: a fast one for brightness, a slower one for size.
                // Driving both from the same value makes the flare "breathe" in a way that reads
                // as mechanical rather than as combustion.
                float flicker = 1.0 + (ValueNoise(_Time.y * _FlickerSpeed) - 0.5) * 2.0 * _FlickerAmount;
                float pulse   = 1.0 + (ValueNoise(_Time.y * _PulseSpeed + 17.3) - 0.5) * 2.0 * _PulseAmount;

                // Camera-facing billboard: take the object's origin in view space, then offset by
                // the vertex's own XY. Scale comes from the object's transform, so the prefab can
                // still be resized normally.
                float3 originVS = TransformWorldToView(TransformObjectToWorld(float3(0, 0, 0)));
                float3 scale = float3(
                    length(GetObjectToWorldMatrix()._m00_m10_m20),
                    length(GetObjectToWorldMatrix()._m01_m11_m21),
                    0);

                float3 positionVS = originVS
                                  + float3(input.positionOS.x * scale.x * pulse,
                                           input.positionOS.y * scale.y * pulse,
                                           0);

                output.positionCS = TransformWViewToHClip(positionVS);
                output.uv = input.uv;
                output.flicker = flicker;

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // Distance from the quad centre, normalised so the quad's edge is 1.
                float d = saturate(length(input.uv - 0.5) * 2.0);

                // Three stacked falloffs: a blown-out white centre, the coloured body of the
                // flame, and a wide soft halo that is what sells it at distance through fog.
                float core = 1.0 - smoothstep(0.0, _CoreSize, d);
                float body = pow(saturate(1.0 - d), _GlowFalloff);
                float halo = pow(saturate(1.0 - d), 1.0) * _HaloStrength;

                float3 colour = lerp(_BaseColor.rgb, _HotColor.rgb, core);
                float  energy = (body + halo + core * 2.0) * _Intensity * input.flicker;

                return half4(colour * energy, saturate(energy) * _BaseColor.a);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
