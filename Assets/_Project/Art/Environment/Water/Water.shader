Shader "MrMoonlight/StylizedWater"
{
    Properties
    {
        [Header(Base)]
        _BaseColor ("Deep Water Color", Color) = (0.03, 0.16, 0.28, 0.75)
        _ShallowColor ("Shallow Ripple Tint", Color) = (0.15, 0.85, 0.8, 1)

        [Header(Ripple Pattern Near)]
        _DensityNear ("Cell Density Near", Range(1, 60)) = 14
        _SpeedNear ("Animation Speed Near", Range(0, 3)) = 0.25
        _EdgeWidthNear ("Edge Width Near", Range(0.01, 0.6)) = 0.1
        _PowerNear ("Edge Sharpness Near", Range(1, 12)) = 3

        [Header(Ripple Pattern Far)]
        _DensityFar ("Cell Density Far", Range(1, 60)) = 30
        _SpeedFar ("Animation Speed Far", Range(0, 3)) = 1.4
        _EdgeWidthFar ("Edge Width Far", Range(0.01, 0.6)) = 0.2
        _PowerFar ("Edge Sharpness Far", Range(1, 12)) = 3

        [Header(Shear And Detail)]
        _ShearStrength ("Radial Shear Strength", Range(0, 6)) = 2.5
        _DetailStrength ("Secondary Detail Layer Strength", Range(0, 1)) = 0.25

        [Header(Swell Vertex Motion)]
        _SwellAmplitudeNear ("Swell Amplitude Near m", Range(0, 3)) = 0.15
        _SwellAmplitudeFar ("Swell Amplitude Far m", Range(0, 8)) = 2.5
        _SwellWavelength ("Swell Wavelength m", Range(200, 3000)) = 1200
        _SwellSpeed ("Swell Speed", Range(0, 2)) = 0.35

        [Header(Distance Blend)]
        _NearDistance ("Calm Up To m", Range(0, 2000)) = 150
        _FarDistance ("Fully Aggressive By m", Range(0, 4000)) = 1200

        [Header(Alpha And Fresnel)]
        _AlphaBase ("Base Alpha", Range(0, 1)) = 0.5
        _AlphaRippleBoost ("Ripple Alpha Boost", Range(0, 1)) = 0.35
        _FresnelPower ("Fresnel Power", Range(0.5, 8)) = 4
        _FresnelStrength ("Fresnel Alpha Strength", Range(0, 1)) = 0.12
        _EmissionStrength ("Ripple Emission Strength", Range(0, 4)) = 0.6
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }
        LOD 100

        Pass
        {
            Name "ForwardUnlit"
            Tags { "LightMode"="UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _ShallowColor;
                float _DensityNear, _SpeedNear, _EdgeWidthNear, _PowerNear;
                float _DensityFar, _SpeedFar, _EdgeWidthFar, _PowerFar;
                float _ShearStrength, _DetailStrength;
                float _SwellAmplitudeNear, _SwellAmplitudeFar, _SwellWavelength, _SwellSpeed;
                float _NearDistance, _FarDistance;
                float _AlphaBase, _AlphaRippleBoost, _FresnelPower, _FresnelStrength, _EmissionStrength;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionWS  : TEXCOORD0;
                float3 normalWS    : TEXCOORD1;
                float  distBlend   : TEXCOORD2;
            };

            // ---- Small helpers -------------------------------------------------

            float2 Hash2(float2 p)
            {
                p = float2(dot(p, float2(127.1, 311.7)), dot(p, float2(269.5, 183.3)));
                return frac(sin(p) * 43758.5453123);
            }

            // Rotates a UV around 'center' by an angle that grows with radius --
            // hand-written equivalent of Shader Graph's Radial Shear node.
            float2 RadialShear(float2 uv, float2 center, float strength)
            {
                float2 delta = uv - center;
                float r = length(delta);
                float a = atan2(delta.y, delta.x) + strength * r;
                return center + r * float2(cos(a), sin(a));
            }

            // Worley/Voronoi F1-F2 edge pattern: bright thin veins at cell borders,
            // dark cell interiors -- animated by rotating each cell's jitter point
            // over time so the veins crawl/shift instead of sitting static.
            float VoronoiEdges(float2 uv, float density, float timeAngle, float edgeWidth)
            {
                float2 p = uv * density;
                float2 ip = floor(p);
                float2 fp = frac(p);
                float f1 = 8.0;
                float f2 = 8.0;
                float ca = cos(timeAngle);
                float sa = sin(timeAngle);

                [unroll]
                for (int y = -1; y <= 1; y++)
                {
                    [unroll]
                    for (int x = -1; x <= 1; x++)
                    {
                        float2 neighbor = float2(x, y);
                        float2 h = Hash2(ip + neighbor);
                        float2 c = h - 0.5;
                        float2 rot = float2(c.x * ca - c.y * sa, c.x * sa + c.y * ca);
                        float2 pt = 0.5 + rot * 0.5;
                        float d = length(neighbor + pt - fp);
                        if (d < f1) { f2 = f1; f1 = d; }
                        else if (d < f2) { f2 = d; }
                    }
                }
                float diff = f2 - f1;
                float aa = clamp(fwidth(diff) * 0.5, 0.0005, edgeWidth * 0.25);
                float edge = 1.0 - smoothstep(edgeWidth - aa, edgeWidth + aa, diff);
                return edge;
            }

            // Two low-frequency sine trains, summed -- this is the geometric swell
            // that displaces vertices. Its wavelength MUST stay large relative to
            // the mesh's own cell size (SeaGrid.mesh is a 64x64 grid over 30000m,
            // ~470m/cell) or the wave aliases into chaotic warped craters instead
            // of a smooth roll. Fine ripple detail lives only in the fragment
            // shader below, where resolution isn't a constraint.
            float Swell(float2 worldXZ, float time)
            {
                float k = TWO_PI / max(_SwellWavelength, 1.0);
                float w1 = sin(dot(worldXZ, normalize(float2(1.0, 0.35))) * k + time * _SwellSpeed);
                float w2 = sin(dot(worldXZ, normalize(float2(-0.45, 1.0))) * k * 1.37 + time * _SwellSpeed * 1.6 + 1.7);
                return (w1 + w2) * 0.5;
            }

            float DistanceBlend(float3 worldPos)
            {
                float d = distance(worldPos.xz, _WorldSpaceCameraPos.xz);
                float t = saturate((d - _NearDistance) / max(_FarDistance - _NearDistance, 1.0));
                return smoothstep(0.0, 1.0, t);
            }

            // ---- Vertex ----------------------------------------------------------

            Varyings Vert(Attributes IN)
            {
                Varyings OUT;

                float3 positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                float t = DistanceBlend(positionWS);
                float amplitude = lerp(_SwellAmplitudeNear, _SwellAmplitudeFar, t);

                float time = _Time.y;
                float eps = 20.0;
                float h0 = Swell(positionWS.xz, time);
                float hx = Swell(positionWS.xz + float2(eps, 0), time);
                float hz = Swell(positionWS.xz + float2(0, eps), time);

                positionWS.y += h0 * amplitude;

                // cheap finite-difference normal from the swell displacement
                float3 tangentX = normalize(float3(eps, (hx - h0) * amplitude, 0));
                float3 tangentZ = normalize(float3(0, (hz - h0) * amplitude, eps));
                float3 normalWS = normalize(cross(tangentZ, tangentX));

                OUT.positionWS = positionWS;
                OUT.normalWS = normalWS;
                OUT.distBlend = t;
                OUT.positionHCS = TransformWorldToHClip(positionWS);
                return OUT;
            }

            // ---- Fragment ----------------------------------------------------------

            float4 Frag(Varyings IN) : SV_Target
            {
                float t = IN.distBlend;
                float density   = lerp(_DensityNear, _DensityFar, t);
                float speed     = lerp(_SpeedNear, _SpeedFar, t);
                float edgeWidth = lerp(_EdgeWidthNear, _EdgeWidthFar, t);
                float power     = lerp(_PowerNear, _PowerFar, t);

                float2 worldXZ = IN.positionWS.xz;
                // Tile into a repeating local UV so the hash never sees huge world
                // coordinates (which would blow up sin()-based hashing precision).
                float tileSize = 200.0;
                float2 tileUV = frac(worldXZ / tileSize);
                tileUV = RadialShear(tileUV, float2(0.5, 0.5), _ShearStrength);

                float timeAngle = _Time.y * speed;
                float edgePrimary = VoronoiEdges(tileUV, density, timeAngle, edgeWidth);
                edgePrimary = pow(edgePrimary, power);

                float edgeDetail = VoronoiEdges(tileUV * 2.13 + 7.0, density * 1.8, -timeAngle * 1.3, edgeWidth * 0.7);
                edgeDetail = pow(edgeDetail, power);

                float ripple = saturate(edgePrimary + edgeDetail * _DetailStrength);

                float3 viewDirWS = normalize(_WorldSpaceCameraPos - IN.positionWS);
                float fresnel = pow(1.0 - saturate(dot(normalize(IN.normalWS), viewDirWS)), _FresnelPower);

                float3 color = lerp(_BaseColor.rgb, _ShallowColor.rgb, ripple);
                float3 emission = _ShallowColor.rgb * ripple * _EmissionStrength;

                float alpha = saturate(_AlphaBase + ripple * _AlphaRippleBoost + fresnel * _FresnelStrength);
                alpha *= _BaseColor.a;

                return float4(color + emission, alpha);
            }
            ENDHLSL
        }
    }
}
